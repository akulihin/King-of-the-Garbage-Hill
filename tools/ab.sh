#!/bin/bash
# ab.sh — in-process paired A/B: does a higher AI level pilot a character better than the baseline?
#
# Runs the sim's --ab-char mode: ONE process plays the same seeded line-up plan twice — a control
# arm (char at the control level) and a test arm (char at the test level), same per-game seeds — and
# prints the paired per-game delta (win% / avgPlace / avgScore) with a 95% CI, plus a verdict.
# Because both arms share the process and the seed, they are paired game-by-game and the only
# intended difference is the probed character's AI level.
#
#   bash tools/ab.sh "Тигр"                     # L3 vs L1, coverage 60, seed 1
#   bash tools/ab.sh "Sirinoks" 2 150 7         # test L2 vs L1, coverage 150, seed 7
#   bash tools/ab.sh "Продавец Сомнительных Тактик" 3 200 1 1
#
# Args: <char> [test-level=3] [coverage=60] [seed=1] [control-level=1]
# Trust the paired avgPlace Δ first (lower variance than win%). If the verdict is "inconclusive",
# raise coverage. NOTE: a few hash-order-sensitive characters (e.g. Стая Гоблинов, Котики) have
# residual per-run nondeterminism the seed can't pin, so their pairing is looser — use more coverage
# for those (the console prints a self-test line when test==control so you can gauge it).
# Exit: 0 measurement done; 2 on failure.

set -u
CHAR="${1:?usage: ab.sh <char> [test-level=3] [coverage=60] [seed=1] [control-level=1]}"
TEST="${2:-3}"
COV="${3:-60}"
SEED="${4:-1}"
CONTROL="${5:-1}"

DIR="$(cd "$(dirname "$0")/.." && pwd)"
( cd "$DIR/King-of-the-Garbage-Hill" && dotnet build -clp:ErrorsOnly >/dev/null 2>&1 ) || { echo "[ab] build failed"; exit 2; }
export KOTGH_SIM_NO_BUILD=1

bash "$DIR/tools/simulate.sh" --coverage "$COV" --games 0 \
    --ai-difficulty "$CONTROL" --ab-char "$CHAR" --ab-test "$TEST" --ab-control "$CONTROL" --seed "$SEED" \
    --report "$DIR/King-of-the-Garbage-Hill/DataBase/Simulations/ab-last.json"
