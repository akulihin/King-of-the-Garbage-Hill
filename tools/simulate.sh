#!/bin/bash
# simulate.sh — headless bot-simulation harness (the behavioral safety net)
#
# Runs N all-bot games through the real game loop (no Discord, no web server),
# captures exceptions and frozen games, and writes a JSON report with
# per-character winrate/placement stats.
#
#   bash tools/simulate.sh                                  # default: --games 100 --coverage 1
#   bash tools/simulate.sh --games 20 --coverage 1          # quick change-set check
#   bash tools/simulate.sh --games 500                      # balance sweep (natural bot roll)
#   bash tools/simulate.sh --characters "Кратос,Тигр,..." --games 10   # fixed 6-char matchup
#
# Flags: --games N | --coverage K | --characters "6 comma-separated names"
#        --report PATH | --timeout-min M
#        --ai-difficulty N (0-3, default 3: 0 pure-random baseline, 1 legacy bots,
#        2 fair player-visible strategy, 3 fair strategy with longer memory/rule inference;
#        sim-only picker — Discord/web use the AiDifficulty default, currently 3)
#        --ai-probe N [--ai-probe-char "Name"] (run ONE bot at level N vs a field on --ai-difficulty)
#        --seed N (deterministic sequential run: a fixed seed reproduces the whole batch)
#        --ab-char "Name" [--ab-test N=3] [--ab-control N=1] (in-process paired A/B: plays the seeded
#        line-ups twice — char at control vs test level — and prints the paired delta; see tools/ab.sh)
# Exit code: 0 clean; 1 game errors and/or stuck games (see report); 2 harness failure.
# Report: DataBase/Simulations/sim-<timestamp>.json (relative to the project dir, gitignored).
#
# Sim errors are FINDINGS — triage them via /fix-finding, don't ignore them.
# Simulation account persistence is disabled; parallel simulator processes and a dev
# server do not share or mutate account JSON.
#
# Run from anywhere: bash tools/simulate.sh [flags]

set -u
cd "$(dirname "$0")/../King-of-the-Garbage-Hill" || exit 2

# The app resolves DataBase/* against the CWD. Running from the project dir means the sim
# reads the CURRENT characters.json (bin/Debug copies go stale). Simulation mode disables
# account persistence before DI starts, so bot accounts stay in this process's memory.
mkdir -p DataBase/UserAccounts DataBase/ServerAccounts DataBase/Simulations
if [ ! -f DataBase/config.json ]; then
    echo '{"Token":"","AnthropicApiKey":""}' > DataBase/config.json
    echo "[simulate.sh] Created blank DataBase/config.json (gitignored; sim never uses the values)"
fi

ARGS=("$@")
if [ ${#ARGS[@]} -eq 0 ]; then
    ARGS=(--games 100 --coverage 1)
fi

# tools/sweep.sh pre-builds once and sets this to skip the per-batch up-to-date check
RUN_FLAGS=()
[ "${KOTGH_SIM_NO_BUILD:-0}" = "1" ] && RUN_FLAGS+=(--no-build)

dotnet run "${RUN_FLAGS[@]}" -- --sim "${ARGS[@]}"
exit $?
