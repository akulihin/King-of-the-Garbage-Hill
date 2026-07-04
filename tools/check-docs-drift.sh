#!/bin/bash
# Stop hook: when the working tree has game-code changes but docs/ wasn't touched,
# remind Claude once (per unique change-set) about the documentation maintenance
# contract (see CLAUDE.md). Exit 2 feeds the reminder back to Claude; a cache file
# prevents nagging repeatedly about the same diff.

set -u
cd "$(dirname "$0")/.." || exit 0

payload=$(cat 2>/dev/null || true)
# Never block twice in a row (hook-loop protection per the hooks contract)
if printf '%s' "$payload" | jq -e '.stop_hook_active == true' >/dev/null 2>&1; then
  exit 0
fi

changed=$(git status --porcelain 2>/dev/null | awk '{print $2}')
game_changed=$(printf '%s\n' "$changed" | grep -E '^(King-of-the-Garbage-Hill/(Game|API|GeneralCommands|DiscordFramework|Helpers|LocalPersistentData)/|King-of-the-Garbage-Hill/(Program|Global|Config)\.cs|Web/VueClient/src/)' || true)
# generated files and commit messages don't count as a docs update
docs_changed=$(printf '%s\n' "$changed" | grep -E '^(docs/|CLAUDE\.md|tools/)' \
  | grep -vE '^docs/(commit-messages/|PASSIVE-MAP\.md)' || true)

[ -z "$game_changed" ] && exit 0
[ -n "$docs_changed" ] && exit 0

hash=$(printf '%s\n' "$game_changed" | sort | sha1sum | cut -c1-12)
cache="/tmp/kotgh-docs-drift-$hash"
[ -f "$cache" ] && exit 0
touch "$cache" 2>/dev/null || true

{
  echo "Docs-drift check: game code changed but docs/ was not updated."
  echo "Per the maintenance contract (CLAUDE.md): update the affected docs/CHARACTERS.md entry,"
  echo "INTERACTION-MATRIX/BALANCE-CONSTANTS rows, regenerate PASSIVE-MAP (tools/audit-passives.sh),"
  echo "or state explicitly why no doc change is needed."
} >&2
exit 2
