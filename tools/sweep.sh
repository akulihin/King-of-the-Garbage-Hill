#!/bin/bash
# sweep.sh — large manual balance sweeps: run N games as a series of simulate.sh batches
# and merge the reports into one winrate table.
#
# Why batches: the harness runs every game of a batch CONCURRENTLY, which is unsafe past
# ~10k games in one process — the single timer thread's tick time trips the 30s stuck
# watchdog, GetFreeBot mints 6 bot accounts per live game (the 60s account flush would
# write them all to disk), and the heap holds every live game. 1000-game batches keep
# peak concurrency ~800 and each batch memory-isolated. See docs/ARCHITECTURE.md §10.
#
#   bash tools/sweep.sh                # 100 000 games, 1 000 per batch (~1 hour)
#   bash tools/sweep.sh 20000          # 20 000 games, 1 000 per batch
#   bash tools/sweep.sh 20000 500      # 20 000 games, 500 per batch
#
# Smoke roll (natural tier-skewed bot-meta population) PLUS coverage line-ups so every rollable
# non-team character appears — each batch runs --coverage ${KOTGH_SWEEP_COVERAGE:-1} (set =0 for the
# old smoke-only sweep). Bots skip Tier<4 except Кира, so smoke alone only covers ~22 of 36; coverage
# fills in the rest. NOTE: coverage games are forced line-ups, not the natural meta — the merged
# winrate table reads as bot-meta for Tier≥4 and as forced-matchup "does it run / rough signal" for
# the Tier<4 characters. Raise KOTGH_SWEEP_COVERAGE for a larger low-tier sample.
# Output: King-of-the-Garbage-Hill/DataBase/Simulations/sweep-<timestamp>/
#         batch-*.json + merged.json + sweep.log (gitignored).
# Exit: 0 = all batches clean; 1 = errors and/or stuck games somewhere (see merged.json);
#       2 = build/harness failure. Ctrl-C finishes the current batch, then merges what ran.
# Don't run while a dev server shares the same DataBase/.
#
# Run from anywhere: bash tools/sweep.sh [total] [batch-size]

set -u
cd "$(dirname "$0")/.." || exit 2

TOTAL=${1:-100000}
BATCH=${2:-1000}
if ! [[ "$TOTAL" =~ ^[0-9]+$ ]] || ! [[ "$BATCH" =~ ^[0-9]+$ ]] || [ "$TOTAL" -lt 1 ] || [ "$BATCH" -lt 1 ]; then
    echo "[sweep] usage: bash tools/sweep.sh [total-games] [batch-size] (positive integers)"
    exit 2
fi

TS=$(date +%Y%m%d-%H%M%S)
SWEEP_REL="DataBase/Simulations/sweep-$TS"          # relative to the project dir (runner CWD)
SWEEP_ABS="King-of-the-Garbage-Hill/$SWEEP_REL"
BATCHES=$(( (TOTAL + BATCH - 1) / BATCH ))
mkdir -p "$SWEEP_ABS"

echo "[sweep] $TOTAL games in $BATCHES batch(es) of ≤$BATCH → $SWEEP_ABS"
echo "[sweep] building once…"
if ! dotnet build King-of-the-Garbage-Hill >> "$SWEEP_ABS/sweep.log" 2>&1; then
    echo "[sweep] build FAILED (see $SWEEP_ABS/sweep.log)"
    exit 2
fi

INTERRUPTED=0
trap 'INTERRUPTED=1' INT
FAILED_BATCHES=0
START=$(date +%s)
DONE=0
remaining=$TOTAL

for i in $(seq 1 "$BATCHES"); do
    if [ $INTERRUPTED -eq 1 ]; then
        echo "[sweep] interrupted — stopping before batch $i"
        break
    fi

    n=$BATCH
    [ "$remaining" -lt "$BATCH" ] && n=$remaining
    remaining=$((remaining - n))

    printf '[sweep] batch %d/%d (%d games)… ' "$i" "$BATCHES" "$n"
    KOTGH_SIM_NO_BUILD=1 bash tools/simulate.sh --games "$n" --timeout-min 30 \
        --coverage "${KOTGH_SWEEP_COVERAGE:-1}" \
        --report "$SWEEP_REL/batch-$(printf '%03d' "$i").json" >> "$SWEEP_ABS/sweep.log" 2>&1
    rc=$?

    DONE=$((DONE + 1))
    elapsed=$(( $(date +%s) - START ))
    if [ "$DONE" -lt "$BATCHES" ]; then
        eta=$(( elapsed * (BATCHES - DONE) / DONE ))
    else
        eta=0
    fi

    case $rc in
        0)   echo "ok (${elapsed}s elapsed, ~${eta}s left)";;
        1)   echo "ERRORS/STUCK — continuing (details in the batch report)"
             FAILED_BATCHES=$((FAILED_BATCHES + 1));;
        130|143) echo "interrupted"
             INTERRUPTED=1;;
        *)   echo "harness failure (exit $rc) — aborting sweep (see $SWEEP_ABS/sweep.log)"
             exit 2;;
    esac
done

# ── Merge ─────────────────────────────────────────────────────────────
shopt -s nullglob
reports=("$SWEEP_ABS"/batch-*.json)
if [ ${#reports[@]} -eq 0 ]; then
    echo "[sweep] no batch reports produced — nothing to merge"
    exit 2
fi

if ! command -v jq > /dev/null; then
    echo "[sweep] jq not found — skipping merge; batch reports are in $SWEEP_ABS"
    [ $FAILED_BATCHES -gt 0 ] && exit 1
    exit 0
fi

jq -s '{
    batches: length,
    totalRequested: (map(.gamesRequested) | add),
    totalFinished:  (map(.gamesFinished)  | add),
    totalStuck:     (map(.gamesStuck)     | add),
    errors:     (map(.errors)     | add),
    stuckGames: (map(.stuckGames) | add),
    characters: ([.[].characters[]] | group_by(.name) | map(
        (map(.games) | add) as $g |
        {name: .[0].name, games: $g,
         top1: (map(.top1) | add), top2: (map(.top2) | add), top3: (map(.top3) | add),
         top4: (map(.top4) | add), top5: (map(.top5) | add), top6: (map(.top6) | add),
         winRate:  (10000 * (map(.top1) | add) / $g | round / 100),
         avgScore: ((map(.avgScore * .games) | add) / $g * 10 | round / 10),
         avgPlace: ((map(.avgPlace * .games) | add) / $g * 100 | round / 100)})
      | sort_by(-.winRate))
}' "${reports[@]}" > "$SWEEP_ABS/merged.json" || { echo "[sweep] merge failed"; exit 2; }

TOT_FIN=$(jq -r '.totalFinished' "$SWEEP_ABS/merged.json")
TOT_STUCK=$(jq -r '.totalStuck' "$SWEEP_ABS/merged.json")
TOT_ERR=$(jq -r '.errors | length' "$SWEEP_ABS/merged.json")

echo "[sweep] ──────────────────────────────────────────"
echo "[sweep] merged ${#reports[@]} batch(es): $TOT_FIN games finished, $TOT_STUCK stuck, $TOT_ERR errors"
[ $INTERRUPTED -eq 1 ] && echo "[sweep] NOTE: sweep was interrupted — results are partial"
echo "[sweep] winrates (bot-meta; WR% · top1/games · avg score · avg place):"
jq -r '.characters[] | "\(.winRate)%\t\(.top1)/\(.games)\t\(.avgScore)\t\(.avgPlace)\t\(.name)"' \
    "$SWEEP_ABS/merged.json"
echo "[sweep] merged report: $SWEEP_ABS/merged.json"

if [ "$TOT_ERR" -gt 0 ] || [ "$TOT_STUCK" -gt 0 ]; then
    echo "[sweep] exit 1 — errors/stuck games are findings (triage via /fix-finding)"
    exit 1
fi
exit 0
