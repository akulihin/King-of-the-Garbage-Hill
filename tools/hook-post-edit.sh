#!/bin/bash
# PostToolUse hook: after any Edit/Write touching passive-bearing files, re-run the
# passive audit and alert Claude ONLY if NEW warnings appeared (vs the previous
# docs/PASSIVE-MAP.md). Regenerates the map either way so it never goes stale.
#
# Hook contract: reads the tool payload JSON on stdin; exit 0 = silent success,
# exit 2 = stderr is fed back to Claude as feedback.

set -u
cd "$(dirname "$0")/.." || exit 0

payload=$(cat)
file=$(printf '%s' "$payload" | jq -r '.tool_input.file_path // empty' 2>/dev/null)
[ -z "$file" ] && exit 0

case "$file" in
  *DataBase/characters.json|*Game/GameLogic/CharacterPassives.cs|*Game/Characters/*.cs|*Game/Classes/PassivesClass.cs) ;;
  *) exit 0 ;;
esac

warn_grep='^- (GHOST|BAD-NAME|ORPHAN)|\*\*ORPHAN\*\*'
prev=$(grep -E "$warn_grep" docs/PASSIVE-MAP.md 2>/dev/null | sort -u)
bash tools/audit-passives.sh >/dev/null 2>&1
new=$(grep -E "$warn_grep" docs/PASSIVE-MAP.md 2>/dev/null | sort -u)

delta=$(comm -13 <(printf '%s\n' "$prev") <(printf '%s\n' "$new") | grep -v '^$' || true)
if [ -n "$delta" ]; then
  {
    echo "audit-passives: NEW warning(s) after editing $file — a passive/character string is broken:"
    echo "$delta"
    echo "Fix the string (or, if intentional, add it to the known-warnings list in tools/audit-passives.sh with a finding ID)."
  } >&2
  exit 2
fi
exit 0
