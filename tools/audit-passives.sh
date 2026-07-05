#!/bin/bash
# audit-passives.sh — regenerates docs/PASSIVE-MAP.md
#
# Cross-references every PassiveName and character Name in DataBase/characters.json
# against the game code and flags:
#   ORPHAN      — a JSON passive with no code reference at all (dead or purely descriptive)
#   NAME-KEYED  — a JSON passive whose mechanic is keyed on the character Name instead
#   GHOST       — a `case "…":` label in CharacterPassives.cs matching no JSON passive
#   BAD-NAME    — a character-Name string compared in code that matches no JSON character
#                 (catches Cyrillic/Latin & typo bugs like "Салдорум" / "Saitama")
#
# Known-and-accepted warnings live in tools/known-warnings.txt (KIND|value|finding-id).
# Exit code: 0 when only known warnings occur; 1 on anything NEW (CI-friendly).
# Removing a line from known-warnings.txt asserts the finding is fixed — the script
# fails until the code agrees. Output is deterministic (safe to commit the map).
#
# Run from anywhere: bash tools/audit-passives.sh

set -u
cd "$(dirname "$0")/.." || exit 1
B=King-of-the-Garbage-Hill
OUT=docs/PASSIVE-MAP.md
JSON=$B/DataBase/characters.json
KNOWN=tools/known-warnings.txt

# Structural whitelists (script logic, not findings):
# case labels in CharacterPassives.cs that are internal tokens, not passive names
CASE_WHITELIST=("Int" "Str" "Spd" "Psy" "(**Умный** ?) " "(**Сильный** ?) " "(**Быстрый** ?) ")
# character-Name strings that are legitimately not in characters.json
NAME_WHITELIST=("ARAM")

known() { # known <KIND> <value> → exit 0 if listed
  grep -v '^#' "$KNOWN" 2>/dev/null | awk -F'|' -v k="$1" -v v="$2" '$1==k && $2==v {found=1; exit} END{exit !found}'
}
known_id() { grep -v '^#' "$KNOWN" 2>/dev/null | awk -F'|' -v k="$1" -v v="$2" '$1==k && $2==v {print $3; exit}'; }

fail=0
NEW_WARN=$(mktemp); PATS=$(mktemp); TMP_OUT="$OUT.tmp.$$"
trap 'rm -f "$NEW_WARN" "$PATS" "$TMP_OUT"' EXIT

mapfile -t PASSIVES < <(jq -r '.[].Passive[].PassiveName' "$JSON" | sort -u)
mapfile -t CHARS    < <(jq -r '.[].Name' "$JSON" | sort -u)

CODE_CS=$(find $B/Game $B/API -name '*.cs')
CODE_ALL="$CODE_CS $(find Web/VueClient/src -name '*.ts' -o -name '*.vue' 2>/dev/null | tr '\n' ' ')"

# Pre-index everything in single passes (m25): the old per-passive greps took
# ~90s on a /mnt/* checkout — longer than the 60s post-edit hook timeout, and a
# killed run used to truncate the committed map mid-write.
declare -A OWNERS CASE_COUNT REFS
while IFS=$'\t' read -r p n; do
  if [ -n "${OWNERS[$p]:-}" ]; then OWNERS[$p]="${OWNERS[$p]}, $n"; else OWNERS[$p]="$n"; fi
done < <(jq -r '.[] | .Name as $n | .Passive[]?.PassiveName | . + "\t" + $n' "$JSON")

while read -r cnt lbl; do CASE_COUNT[$lbl]=$cnt; done < <(
  grep -oP 'case "\K[^"]+(?=":)' "$B/Game/GameLogic/CharacterPassives.cs" | sort | uniq -c | sed -E 's/^ *([0-9]+) /\1 /')

printf '"%s"\n' "${PASSIVES[@]}" > "$PATS"
while IFS=$'\t' read -r m cnt; do REFS[$m]=$cnt; done < <(
  grep -HoF -f "$PATS" $CODE_ALL 2>/dev/null \
  | grep -v "CharactersPhrases.cs" \
  | sed 's/:"/\t"/' | sort -u | cut -f2 \
  | sort | uniq -c | sed -E 's/^ *([0-9]+) (.*)$/\2\t\1/')

{
echo "# PASSIVE-MAP (generated — do not edit)"
echo
echo "> Regenerate with \`bash tools/audit-passives.sh\` after ANY change to characters.json,"
echo "> CharacterPassives.cs or per-character logic. Known warnings are read from"
echo "> \`tools/known-warnings.txt\`; anything NEW fails the script and must be fixed"
echo "> or added there with a finding ID."
echo
echo "## Passive coverage"
echo
echo "| Passive | Owner(s) | CP case | Other refs | Status |"
echo "|---|---|---:|---:|---|"

for p in "${PASSIVES[@]}"; do
  owners=${OWNERS[$p]:-}
  cases=${CASE_COUNT[$p]:-0}
  refs=${REFS["\"$p\""]:-0}
  status="ok"
  if known "NAME-KEYED" "$p"; then
    status="NAME-KEYED ($(known_id "NAME-KEYED" "$p"))"
  elif [ "$cases" -eq 0 ] && [ "$refs" -eq 0 ]; then
    status="**ORPHAN**"; echo "ORPHAN: $p" >> "$NEW_WARN"; fail=1
  fi
  echo "| $p | $owners | $cases | $refs | $status |"
done

echo
echo "## GHOST case labels (in CharacterPassives.cs; neither a passive nor a character name)"
echo
mapfile -t CASES < <(grep -oP 'case "\K[^"]+(?=":)' $B/Game/GameLogic/CharacterPassives.cs | sort -u)
ghost_found=0
for c in "${CASES[@]}"; do
  skip=0
  for w in "${CASE_WHITELIST[@]}"; do [ "$c" = "$w" ] && skip=1; done
  for ch in "${CHARS[@]}"; do [ "$c" = "$ch" ] && skip=1; done   # Name-switch labels are fine
  [ $skip -eq 1 ] && continue
  found=0
  for p in "${PASSIVES[@]}"; do [ "$c" = "$p" ] && found=1 && break; done
  if [ $found -eq 0 ]; then
    ghost_found=1
    if known "GHOST" "$c"; then
      echo "- GHOST (known, $(known_id "GHOST" "$c")): \`case \"$c\":\`"
    else
      echo "- **GHOST (NEW)**: \`case \"$c\":\`"; echo "GHOST: $c" >> "$NEW_WARN"; fail=1
    fi
  fi
done
[ $ghost_found -eq 0 ] && echo "- none"

echo
echo "## BAD-NAME character comparisons (\`.Name ==/!=/is \"…\"\` string not in characters.json)"
echo
mapfile -t NAMEREFS < <({ grep -hoP '\.Name (==|!=) "\K[^"]+' $CODE_CS; \
                          grep -hoP '\.Name is ("[^"]+"( or "[^"]+")*)' $CODE_CS | grep -oP '"[^"]*"' | tr -d '"'; } | sort -u)
bad_found=0
for n in "${NAMEREFS[@]}"; do
  skip=0
  for w in "${NAME_WHITELIST[@]}"; do [ "$n" = "$w" ] && skip=1; done
  [ $skip -eq 1 ] && continue
  found=0
  for ch in "${CHARS[@]}"; do [ "$n" = "$ch" ] && found=1 && break; done
  if [ $found -eq 0 ]; then
    bad_found=1
    if known "BAD-NAME" "$n"; then
      echo "- BAD-NAME (known, $(known_id "BAD-NAME" "$n")): \`\"$n\"\`"
    else
      echo "- **BAD-NAME (NEW)**: \`\"$n\"\`"; echo "BAD-NAME: $n" >> "$NEW_WARN"; fail=1
    fi
  fi
done
[ $bad_found -eq 0 ] && echo "- none"
echo
echo "_Limitation: \`switch\` case labels over a Name variable outside CharacterPassives.cs (e.g. BotsBehavior) are not scanned — the Cyrillic \`case \"Салдорум\":\` at BotsBehavior.cs:1409 is only caught via its sibling \`==\` checks._"
} > "$TMP_OUT"
mv -f "$TMP_OUT" "$OUT"   # atomic: a killed run can never leave a truncated map (m25)

echo "Wrote $OUT"
if [ -s "$NEW_WARN" ]; then
  echo "NEW warnings (fix the string or add to tools/known-warnings.txt with a finding ID):"
  cat "$NEW_WARN"
else
  echo "No new warnings (known ones listed in the map)."
fi
exit $fail
