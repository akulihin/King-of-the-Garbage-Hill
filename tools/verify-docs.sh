#!/bin/bash
# verify-docs.sh [--all|--changed|--baseline-update] — anchor-drift detector for docs/*.md
#
# Extracts every file:line anchor (File.ext:N, File.ext:N-M, CP:N…) from the docs,
# resolves it, and:
#   * HARD-FAILS (exit 1) on UNRESOLVED files or OUT-OF-RANGE line numbers
#   * lists DRIFT candidates: a `code`-span token on the citing doc line that no
#     longer appears within ±3 lines of the cited location (advisory — eyeball them)
#
# Known/accepted DRIFT candidates live in tools/known-drift.txt — only NEW ones are
# printed. After reviewing new candidates (and fixing real drift), accept the rest
# with --baseline-update.
#
# --changed limits the check to anchors pointing into files currently modified in
# the working tree (fast post-edit mode; /fix-finding runs this).

set -u
cd "$(dirname "$0")/.." || exit 1
B=King-of-the-Garbage-Hill
MODE="${1:---all}"

resolve() {
  case "$1" in
    CP) echo "$B/Game/GameLogic/CharacterPassives.cs";;
    CharacterPassives.cs) echo "$B/Game/GameLogic/CharacterPassives.cs";;
    DoomsdayMachine.cs|CheckIfReady.cs|BotsBehavior.cs|StartGameLogic.cs|CalculateRounds.cs)
      echo "$B/Game/GameLogic/$1";;
    GameReactions.cs) echo "$B/Game/ReactionHandling/$1";;
    CharacterClass.cs|InGameStatusClass.cs|GamePlayerBridgeClass.cs|PassivesClass.cs|GameClass.cs)
      echo "$B/Game/Classes/$1";;
    SecureRandom.cs) echo "$B/Helpers/$1";;
    GameStateMapper.cs|WebGameService.cs) echo "$B/API/Services/$1";;
    GameStateDto.cs) echo "$B/API/DTOs/$1";;
    GameUpdateMess.cs) echo "$B/Game/DiscordMessages/$1";;
    CharactersPhrases.cs|CharactersPull.cs) echo "$B/Game/MemoryStorage/$1";;
    characters.json) echo "$B/DataBase/$1";;
    GameDesign.txt) echo "$B/Game/$1";;
    signalr.ts) echo "Web/VueClient/src/services/$1";;
    PlayerCard.vue|SkillsPanel.vue) echo "Web/VueClient/src/components/$1";;
    *.cs) f=$(find $B/Game/Characters -name "$1" 2>/dev/null | head -1); echo "$f";;
    *) echo "";;
  esac
}

changed_files=""
if [ "$MODE" = "--changed" ]; then
  changed_files=$(git status --porcelain | awk '{print $2}')
  [ -z "$changed_files" ] && { echo "verify-docs: no working-tree changes; nothing to check."; exit 0; }
fi

fail=0
DRIFTS=$(mktemp)
trap 'rm -f "$DRIFTS"' EXIT
export LC_ALL=C
declare -A LINECOUNT   # file → wc -l cache (biggest win: avoid re-stat'ing the same file per anchor)
for doc in docs/GAME-DESIGN.md docs/ARCHITECTURE.md docs/CHARACTERS.md docs/AUDIT-FINDINGS.md docs/BALANCE-CONSTANTS.md docs/INTERACTION-MATRIX.md; do
  [ -f "$doc" ] || continue
  # Group all anchors per citing doc line so drift is judged against their COMBINED context
  while IFS='|' read -r docline anchors; do
    [ -z "$anchors" ] && continue
    # Extract tokens first — if the citing line has none, skip the expensive context reads
    citing=$(sed -n "${docline}p" "$doc")
    tokens=$(printf '%s' "$citing" | grep -oP '\x60[^\x60]+\x60' | sed 's/^\x60//; s/\x60$//')
    ctx=""
    any_in_changed=0
    for a in $anchors; do
      fname=${a%%:*}; lines=${a#*:}
      file=$(resolve "$fname")
      if [ -z "$file" ] || [ ! -f "$file" ]; then
        echo "UNRESOLVED: $fname (at $doc:$docline)"; fail=1; continue
      fi
      if [ "$MODE" = "--changed" ]; then
        echo "$changed_files" | grep -qF -- "$file" && any_in_changed=1
      fi
      start=${lines%%-*}; end=${lines##*-}
      if [ -z "${LINECOUNT[$file]:-}" ]; then LINECOUNT[$file]=$(wc -l < "$file"); fi
      total=${LINECOUNT[$file]}
      if [ "$start" -gt "$total" ] || [ "$end" -gt "$total" ]; then
        echo "OUT-OF-RANGE: $fname:$lines cited at $doc:$docline (file has $total lines)"; fail=1; continue
      fi
      [ -z "$tokens" ] && continue   # nothing to drift-check; range check was all we needed
      ctx_start=$((start > 3 ? start - 3 : 1)); ctx_end=$((end + 3))
      ctx+=$(sed -n "${ctx_start},${ctx_end}p" "$file")$'\n'
    done
    [ "$MODE" = "--changed" ] && [ $any_in_changed -eq 0 ] && continue
    [ -z "$ctx" ] && continue
    while read -r token; do
      [ -z "$token" ] && continue
      [ ${#token} -lt 4 ] && continue
      case "$token" in
        *"|"*|*"("*|*")"*|*"⚠"*|*"→"*|*"…"*|*" "*) continue;;                # table/prose fragments
        *".cs"*|*".ts"*|*".json"*|*".vue"*|*".txt"*|*.md|*.sh|CP:*|CC:*|DM:*|CIR:*|GR:*) continue;;  # anchors/paths
      esac
      probe=$token
      case "$token" in *.*) probe=${token##*.};; esac   # Class.Member → match Member
      printf '%s' "$ctx" | grep -qF -- "$probe" || \
        echo "DRIFT?: \`$token\` at $doc:$docline not found near its cited lines" >> "$DRIFTS"
    done < <(printf '%s\n' "$tokens")
  done < <(grep -noE "([A-Za-z_.]+\.(cs|ts|json|txt|vue)|CP):[0-9]+(-[0-9]+)?" "$doc" \
            | sed 's/:/|/' | awk -F'|' '{a[$1]=a[$1]" "$2} END{for (l in a) print l "|" a[l]}' | sort -n)
done
BASELINE=tools/known-drift.txt
sort -u "$DRIFTS" > "$DRIFTS.sorted"
if [ "$MODE" = "--baseline-update" ]; then
  cp "$DRIFTS.sorted" "$BASELINE"
  echo "verify-docs: baseline updated ($(grep -c . "$BASELINE" || true) accepted DRIFT candidates in $BASELINE)."
  rm -f "$DRIFTS.sorted"; exit $fail
fi
new_drift=$(comm -13 <(sort -u "$BASELINE" 2>/dev/null) "$DRIFTS.sorted" | grep -c . || true)
known_drift=$(( $(grep -c . "$DRIFTS.sorted" || true) - new_drift ))
comm -13 <(sort -u "$BASELINE" 2>/dev/null) "$DRIFTS.sorted" | head -40
rm -f "$DRIFTS.sorted"

if [ $fail -eq 1 ]; then
  echo "verify-docs: FAILED (unresolved/out-of-range anchors — the docs cite code that moved or vanished)."
  exit 1
fi
echo "verify-docs: all anchors resolve and are in range. NEW drift candidates: $new_drift (known/accepted: $known_drift). Review new ones, then accept with --baseline-update."
exit 0
