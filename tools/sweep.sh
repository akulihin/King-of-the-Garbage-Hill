#!/bin/bash
# sweep.sh — compare every bot AI level in one large, concurrent balance sweep.
#
# One command runs AI levels 0, 1, 2 and 3 at the same time, with exactly 100 000
# requested games per level by default (400 000 total):
#
#   bash tools/sweep.sh                  # 4 × 100 000 games, batches of 5 000
#   bash tools/sweep.sh 1000 250         # small 4 × 1 000 comparison
#
# Each level first runs forced-coverage batches, then spends the rest of its exact
# game budget on natural bot rolls. The default coverage target is 1% of the per-level
# budget (1 000 appearances per rollable character at the default 100k); override with
# KOTGH_SWEEP_COVERAGE, or set it to 0 for a natural-meta-only run.
#
# Why 5 000-game batches: every game in one simulator process is live concurrently.
# The game loop advances them on one timer thread, so larger batches increase the delay
# between rounds even when RAM is plentiful. The watchdog counts actual per-game loop
# visits, so unrelated CPU load can make a sweep slower but cannot create false STUCK rows.
#
# Output: King-of-the-Garbage-Hill/DataBase/Simulations/sweep-<timestamp>/
#   ai-{0,1,2,3}/batch-*.json + merged.json + sweep.log
#   comparison.json, comparison.csv, summary.md, index.html
#   winrate.svg, avg-place.svg, avg-score.svg
#
# Level 1 is the comparison baseline. Chart cells show absolute values and improvement
# versus L1: blue = better, red = worse, neutral = no meaningful difference; saturation
# reflects the size of the difference relative to sampling noise.
#
# Exit: 0 = all four levels clean; 1 = errors/stuck games in any level;
#       2 = invalid input, build/report/harness failure. Ctrl-C stops all arms.
# Run from anywhere. Simulation account persistence is disabled, so the four arms do
# not touch each other or a running development server's account files.

set -uo pipefail
cd "$(dirname "$0")/.." || exit 2

TOTAL=${1:-100000}
BATCH=${2:-5000}
if [ "$#" -gt 2 ] || ! [[ "$TOTAL" =~ ^[0-9]+$ ]] || ! [[ "$BATCH" =~ ^[0-9]+$ ]] || \
   [ "$TOTAL" -lt 1 ] || [ "$BATCH" -lt 1 ]; then
    echo "[sweep] usage: bash tools/sweep.sh [games-per-level] [batch-size]"
    echo "[sweep]        both optional values must be positive integers"
    exit 2
fi

if ! command -v python3 >/dev/null; then
    echo "[sweep] python3 is required to merge reports and render charts"
    exit 2
fi

DEFAULT_COVERAGE=$(( (TOTAL + 99) / 100 ))
COVERAGE=${KOTGH_SWEEP_COVERAGE:-$DEFAULT_COVERAGE}
TIMEOUT_MIN=${KOTGH_SWEEP_TIMEOUT_MIN:-30}
if ! [[ "$COVERAGE" =~ ^[0-9]+$ ]] || ! [[ "$TIMEOUT_MIN" =~ ^[0-9]+([.][0-9]+)?$ ]] || \
   [[ "$TIMEOUT_MIN" =~ ^0+([.]0+)?$ ]]; then
    echo "[sweep] KOTGH_SWEEP_COVERAGE must be a non-negative integer"
    echo "[sweep] KOTGH_SWEEP_TIMEOUT_MIN must be a positive number"
    exit 2
fi

TS=$(date +%Y%m%d-%H%M%S)
SWEEP_REL="DataBase/Simulations/sweep-$TS"
SWEEP_ABS="King-of-the-Garbage-Hill/$SWEEP_REL"
mkdir -p "$SWEEP_ABS"

echo "[sweep] AI 0/1/2/3 concurrently: $TOTAL games each ($((TOTAL * 4)) total), batches ≤$BATCH"
echo "[sweep] L1 baseline; forced coverage target: $COVERAGE appearance(s)/character/level"
echo "[sweep] output: $SWEEP_ABS"
echo "[sweep] building once…"
if ! dotnet build King-of-the-Garbage-Hill >"$SWEEP_ABS/build.log" 2>&1; then
    echo "[sweep] build FAILED (see $SWEEP_ABS/build.log)"
    exit 2
fi

run_batch() {
    local level=$1
    local games=$2
    local coverage=$3
    local report=$4
    local log=$5

    KOTGH_SIM_NO_BUILD=1 bash tools/simulate.sh \
        --games "$games" \
        --coverage "$coverage" \
        --timeout-min "$TIMEOUT_MIN" \
        --ai-difficulty "$level" \
        --report "$report" >>"$log" 2>&1
}

run_level() {
    local level=$1
    local level_dir="$SWEEP_ABS/ai-$level"
    local level_rel="$SWEEP_REL/ai-$level"
    local log="$level_dir/sweep.log"
    local failed_batches=0
    local requested=0
    local remaining=$TOTAL
    local batch_no=0
    local smoke_batches=0
    local start
    start=$(date +%s)
    mkdir -p "$level_dir"

    if [ "$COVERAGE" -gt 0 ]; then
        # One coverage pass reveals the stable generated games/pass for the current
        # catalogue. Use it to keep later coverage processes under the same batch cap.
        local coverage_batch_no=1
        local coverage_done=1
        local coverage_report_name="batch-coverage-001.json"
        printf '[sweep:L%d] coverage calibration (1 pass)… ' "$level"
        run_batch "$level" 0 1 "$level_rel/$coverage_report_name" "$log"
        local rc=$?
        case $rc in
            0) echo "ok" ;;
            1) echo "ERRORS/STUCK — continuing"; failed_batches=$((failed_batches + 1)) ;;
            130|143) echo "interrupted"; return "$rc" ;;
            *) echo "harness failure (exit $rc; see $log)"; return 2 ;;
        esac

        local games_per_coverage_pass
        games_per_coverage_pass=$(python3 tools/sweep-report.py games-requested "$level_dir/$coverage_report_name") || return 2
        requested=$games_per_coverage_pass
        local projected_coverage_games=$((games_per_coverage_pass * COVERAGE))
        if [ "$projected_coverage_games" -gt "$TOTAL" ]; then
            echo "[sweep:L$level] coverage needs about $projected_coverage_games games, exceeding the $TOTAL-game budget"
            echo "[sweep:L$level] lower KOTGH_SWEEP_COVERAGE or raise games-per-level"
            return 2
        fi
        local coverage_passes_per_batch=$((BATCH / games_per_coverage_pass))
        [ "$coverage_passes_per_batch" -lt 1 ] && coverage_passes_per_batch=1
        local coverage_batches=$((1 + (COVERAGE - 1 + coverage_passes_per_batch - 1) / coverage_passes_per_batch))

        while [ "$coverage_done" -lt "$COVERAGE" ]; do
            coverage_batch_no=$((coverage_batch_no + 1))
            local coverage_chunk=$coverage_passes_per_batch
            local coverage_left=$((COVERAGE - coverage_done))
            [ "$coverage_left" -lt "$coverage_chunk" ] && coverage_chunk=$coverage_left
            coverage_report_name=$(printf 'batch-coverage-%03d.json' "$coverage_batch_no")
            printf '[sweep:L%d] coverage batch %d/%d (×%d passes)… ' \
                "$level" "$coverage_batch_no" "$coverage_batches" "$coverage_chunk"
            run_batch "$level" 0 "$coverage_chunk" "$level_rel/$coverage_report_name" "$log"
            rc=$?
            case $rc in
                0) echo "ok" ;;
                1) echo "ERRORS/STUCK — continuing"; failed_batches=$((failed_batches + 1)) ;;
                130|143) echo "interrupted"; return "$rc" ;;
                *) echo "harness failure (exit $rc; see $log)"; return 2 ;;
            esac
            local coverage_batch_games
            coverage_batch_games=$(python3 tools/sweep-report.py games-requested "$level_dir/$coverage_report_name") || return 2
            requested=$((requested + coverage_batch_games))
            coverage_done=$((coverage_done + coverage_chunk))
        done

        if [ "$requested" -gt "$TOTAL" ]; then
            echo "[sweep:L$level] coverage needs $requested games, exceeding the $TOTAL-game budget"
            echo "[sweep:L$level] lower KOTGH_SWEEP_COVERAGE or raise games-per-level"
            return 2
        fi
        remaining=$((TOTAL - requested))
    fi

    smoke_batches=$(( (remaining + BATCH - 1) / BATCH ))
    local completed=0
    while [ "$remaining" -gt 0 ]; do
        batch_no=$((batch_no + 1))
        local n=$BATCH
        [ "$remaining" -lt "$BATCH" ] && n=$remaining
        remaining=$((remaining - n))
        local report_name
        report_name=$(printf 'batch-%03d.json' "$batch_no")
        printf '[sweep:L%d] batch %d/%d (%d games)… ' "$level" "$batch_no" "$smoke_batches" "$n"
        run_batch "$level" "$n" 0 "$level_rel/$report_name" "$log"
        local rc=$?
        completed=$((completed + 1))
        local elapsed=$(( $(date +%s) - start ))
        local eta=0
        [ "$completed" -lt "$smoke_batches" ] && eta=$((elapsed * (smoke_batches - completed) / completed))
        case $rc in
            0) echo "ok (${elapsed}s elapsed, ~${eta}s left)" ;;
            1) echo "ERRORS/STUCK — continuing (see $log)"; failed_batches=$((failed_batches + 1)) ;;
            130|143) echo "interrupted"; return "$rc" ;;
            *) echo "harness failure (exit $rc; see $log)"; return 2 ;;
        esac
    done

    [ "$failed_batches" -gt 0 ] && return 1
    return 0
}

declare -a PIDS=()
declare -a LEVELS=(0 1 2 3)
INTERRUPTED=0
stop_arms() {
    INTERRUPTED=1
    echo
    echo "[sweep] interrupt received — stopping all AI levels…"
    [ "${#PIDS[@]}" -gt 0 ] && kill "${PIDS[@]}" 2>/dev/null || true
}
trap stop_arms INT TERM

for level in "${LEVELS[@]}"; do
    run_level "$level" &
    PIDS[$level]=$!
done

OVERALL_RC=0
for level in "${LEVELS[@]}"; do
    wait "${PIDS[$level]}"
    rc=$?
    case $rc in
        0) ;;
        1) [ "$OVERALL_RC" -lt 1 ] && OVERALL_RC=1 ;;
        *) OVERALL_RC=2 ;;
    esac
done

if [ "$INTERRUPTED" -eq 1 ]; then
    echo "[sweep] interrupted; partial batch reports remain in $SWEEP_ABS"
    exit 2
fi

if [ "$OVERALL_RC" -eq 2 ]; then
    echo "[sweep] one or more AI levels had a harness failure; partial reports remain in $SWEEP_ABS"
    exit 2
fi

echo "[sweep] merging all four levels and rendering reports…"
if ! python3 tools/sweep-report.py build "$SWEEP_ABS" "$TOTAL"; then
    echo "[sweep] report generation FAILED"
    exit 2
fi

echo "[sweep] ──────────────────────────────────────────"
echo "[sweep] comparison dashboard: $SWEEP_ABS/index.html"
echo "[sweep] charts: winrate.svg · avg-place.svg · avg-score.svg"
echo "[sweep] data: comparison.json · comparison.csv · summary.md"
if [ "$OVERALL_RC" -eq 1 ]; then
    echo "[sweep] exit 1 — errors/stuck games are findings (triage via /fix-finding)"
fi
exit "$OVERALL_RC"
