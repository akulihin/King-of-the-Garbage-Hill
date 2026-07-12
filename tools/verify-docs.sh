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
# printed. Candidate identity uses the nearest Markdown heading rather than the doc
# line number, so inserting prose does not invalidate the whole baseline. After
# reviewing new candidates (and fixing real drift), accept the rest with
# --baseline-update.
#
# The script also hard-fails when the hand-maintained catalogs lose coverage of a
# character definition, public GameHub method, REST route, Vue route, or Discord
# command. These exact inventory checks complement (but do not replace) semantic
# review of prose and tunable values.
#
# --changed limits the check to anchors pointing into files currently modified in
# the working tree (fast post-edit mode; /fix-finding runs this).

set -u
cd "$(dirname "$0")/.." || exit 1
B=King-of-the-Garbage-Hill
V=Web/VueClient/src
MODE="${1:---all}"

resolve() {
  case "$1" in
    CP) echo "$B/Game/GameLogic/CharacterPassives.cs";;
    CharacterPassives.cs) echo "$B/Game/GameLogic/CharacterPassives.cs";;
    DoomsdayMachine.cs|CheckIfReady.cs|BotsBehavior.cs|StartGameLogic.cs|CalculateRounds.cs)
      echo "$B/Game/GameLogic/$1";;
    GameReactions.cs|StoreReactions.cs|LoreReactions.cs|TutorialReactions.cs)
      echo "$B/Game/ReactionHandling/$1";;
    CharacterClass.cs|InGameStatusClass.cs|GamePlayerBridgeClass.cs|PassivesClass.cs|GameClass.cs|DiscordAccountClass.cs|InGameDiscordStatus.cs|AchievementClass.cs|QuestClass.cs)
      echo "$B/Game/Classes/$1";;
    SecureRandom.cs|ClaudeHaikuService.cs|HelperFunctions.cs|GameLocalization.cs|PhraseLocalization.cs|BilingualGeneratedText.cs) echo "$B/Helpers/$1";;
    SimulationRunner.cs|BotGameFactory.cs|SimReport.cs) echo "$B/Game/Simulation/$1";;
    Program.cs|Global.cs|Config.cs) echo "$B/$1";;
    General.cs|AdminPanel.cs|HelpModule.cs|Lore.cs|Store.cs|Tutorial.cs|DiceRoll.cs|ServerManagement.cs)
      echo "$B/GeneralCommands/$1";;
    UserAccounts.cs|UsersDataStorage.cs) echo "$B/LocalPersistentData/UsersAccounts/$1";;
    GameStateMapper.cs|WebGameService.cs|GameNotificationService.cs|GameStoryService.cs|BlackjackService.cs|BattleshipService.cs|ReplayService.cs)
      echo "$B/API/Services/$1";;
    BattleshipModels.cs) echo "$B/Battleship/Models/$1";;
    GameStateDto.cs|ReplayDto.cs) echo "$B/API/DTOs/$1";;
    GameHub.cs) echo "$B/API/$1";;
    GameController.cs|WidgetController.cs) echo "$B/API/Controllers/$1";;
    DiscordWidgetService.cs) echo "$B/Game/Services/$1";;
    CommandHandling.cs|DiscordEventDispatcher.cs) echo "$B/DiscordFramework/$1";;
    ModuleBaseCustom.cs) echo "$B/DiscordFramework/Extensions/$1";;
    GameUpdateMess.cs) echo "$B/Game/DiscordMessages/$1";;
    CharactersPhrases.cs|CharactersPull.cs) echo "$B/Game/MemoryStorage/$1";;
    characters.json|localization.en.json|phrases.en.json) echo "$B/DataBase/$1";;
    GameDesign.txt) echo "$B/Game/$1";;
    signalr.ts|sound.ts|textFormatting.ts) echo "$V/services/$1";;
    game.ts|replay.ts|replay.spec.ts|battleship.ts) echo "$V/store/$1";;
    router.ts|main.ts|App.vue|i18n.ts|i18n.spec.ts) echo "$V/$1";;
    vite.config.ts) echo "Web/VueClient/$1";;
    useTip.ts|useVfx.ts|useFocusTrapDialog.ts) echo "$V/composables/$1";;
    Game.vue|Lobby.vue|Spectate.vue|Replay.vue|Widget.vue|Home.vue|Achievements.vue|BattleshipLobby.vue|BattleshipGame.vue|BattleshipSpectate.vue)
      echo "$V/pages/$1";;
    LoginProcess.vue|LoginSuccess.vue) echo "$V/components/Login/$1";;
    PlayerCard.vue|SkillsPanel.vue|Leaderboard.vue|FightAnimation.vue|DeathNote.vue|RoundTimer.vue|MediaMessages.vue|BattleLog.vue|ActionPanel.vue|AchievementBoard.vue|AchievementPopup.vue|LootBox.vue|DailyQuestBoard.vue|ScoreOdometer.vue)
      echo "$V/components/$1";;
    AchievementIcon.vue) echo "$V/components/achievements/$1";;
    ActionBar.vue|BattleLogPanel.vue|BoardGrid.vue|BsIcon.vue|CellComponent.vue|ConfirmDialog.vue|FleetBuilder.vue|FleetPanel.vue|GameHeader.vue|GameOverCelebration.vue|GameOverlays.vue|StatsPanel.vue|SummonBar.vue|VfxCanvas.vue|WeaponBar.vue)
      echo "$V/components/battleship/$1";;
    ArmySelectPhase.vue|CombatPhase.vue|FleetBuildPhase.vue|GameOverPhase.vue|LobbyPhase.vue|PlacementPhase.vue)
      echo "$V/components/battleship/phases/$1";;
    FortressOfDoom.vue) echo "$V/components/Home/$1";;
    *.cs) f=$(find $B/Game/Characters -name "$1" 2>/dev/null | head -1); echo "$f";;
    *) echo "";;
  esac
}

changed_files=""
declare -A CHANGED_FILES=()
if [ "$MODE" = "--changed" ]; then
  changed_files=$(git status --porcelain | awk '{print $2}')
  [ -z "$changed_files" ] && { echo "verify-docs: no working-tree changes; nothing to check."; exit 0; }
  while IFS= read -r changed_file; do
    [ -n "$changed_file" ] && CHANGED_FILES["$changed_file"]=1
  done <<< "$changed_files"
fi

fail=0
DRIFTS=$(mktemp)
trap 'rm -f "$DRIFTS"' EXIT
export LC_ALL=C
declare -A LINECOUNT   # file → wc -l cache (biggest win: avoid re-stat'ing the same file per anchor)
for doc in docs/GAME-DESIGN.md docs/ARCHITECTURE.md docs/CHARACTERS.md docs/AUDIT-FINDINGS.md docs/BALANCE-CONSTANTS.md docs/INTERACTION-MATRIX.md docs/WEB-BACKEND.md docs/WEB-CLIENT.md docs/DISCORD-INTERFACE.md docs/LOCALIZATION.md docs/ACHIEVEMENTS.md docs/DAILY-QUESTS.md; do
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
        [ -n "${CHANGED_FILES[$file]:-}" ] && any_in_changed=1
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
        :*) continue;;                                                                              # bare continuation anchors (`:N-M`)
      esac
      probe=$token
      case "$token" in *.*) probe=${token##*.};; esac   # Class.Member → match Member
      # A here-string avoids SIGPIPE from `grep -q` closing a large printf pipeline
      # after its first match (the Codex shell runs with pipefail enabled).
      if ! grep -qF -- "$probe" <<< "$ctx"; then
        heading=$(awk -v n="$docline" 'NR>n {exit} /^#{1,6} / {h=$0} END {print h}' "$doc")
        [ -z "$heading" ] && heading="# (document start)"
        echo "DRIFT?: \`$token\` in $doc [$heading] not found near its cited lines" >> "$DRIFTS"
      fi
    done < <(printf '%s\n' "$tokens")
  done < <(grep -noE "([A-Za-z0-9_.]+\.(cs|ts|json|txt|vue)|CP):[0-9]+(-[0-9]+)?" "$doc" \
            | sed 's/:/|/' | awk -F'|' '{a[$1]=a[$1]" "$2} END{for (l in a) print l "|" a[l]}' | sort -n)
done

# Exact catalog coverage. These checks intentionally ask only whether every source
# entry has a documented counterpart; the prose still needs human semantic review.
catalog_fail=0
JSON="$B/DataBase/characters.json"
expected_characters=$(jq 'length' "$JSON")
documented_characters=$(( $(grep -c '^## ' docs/CHARACTERS.md) - 1 )) # exclude "## Index"
if [ "$documented_characters" -ne "$expected_characters" ]; then
  echo "CATALOG-MISMATCH: CHARACTERS.md has $documented_characters character headings; characters.json has $expected_characters."
  catalog_fail=1
fi
while IFS= read -r name; do
  grep -qF -- "## $name" docs/CHARACTERS.md || {
    echo "CATALOG-MISSING: character heading \`$name\` in docs/CHARACTERS.md"
    catalog_fail=1
  }
done < <(jq -r '.[].Name' "$JSON")

hub_methods=0
while IFS= read -r method; do
  [ -z "$method" ] && continue
  hub_methods=$((hub_methods + 1))
  grep -qF -- "$method" docs/WEB-BACKEND.md || {
    echo "CATALOG-MISSING: public GameHub method \`$method\` in docs/WEB-BACKEND.md"
    catalog_fail=1
  }
done < <(grep -oP '^\s*public async Task \K[A-Za-z0-9_]+' "$B/API/GameHub.cs")

rest_routes=0
while IFS= read -r route; do
  [ -z "$route" ] && continue
  rest_routes=$((rest_routes + 1))
  grep -qF -- "$route" docs/WEB-BACKEND.md || {
    echo "CATALOG-MISSING: REST route \`$route\` in docs/WEB-BACKEND.md"
    catalog_fail=1
  }
done < <(grep -hoP '\[Http(?:Get|Post|Put|Delete)\("\K[^"]+' "$B/API/Controllers/"*.cs)

vue_routes=0
while IFS= read -r route; do
  [ -z "$route" ] && continue
  vue_routes=$((vue_routes + 1))
  grep -qF -- "\`$route\`" docs/WEB-CLIENT.md || {
    echo "CATALOG-MISSING: Vue route \`$route\` in docs/WEB-CLIENT.md"
    catalog_fail=1
  }
done < <(grep -oP "path:\s*'\K[^']+" "$V/router.ts")

discord_commands=0
while IFS= read -r command; do
  [ -z "$command" ] && continue
  discord_commands=$((discord_commands + 1))
  grep -qF -- "$command" docs/DISCORD-INTERFACE.md || {
    echo "CATALOG-MISSING: Discord command \`$command\` in docs/DISCORD-INTERFACE.md"
    catalog_fail=1
  }
done < <(grep -rhoP '\[Command\("\K[^"]+' "$B/GeneralCommands" | sort -u)

if [ "$catalog_fail" -eq 0 ]; then
  echo "verify-docs: exact catalogs cover $expected_characters characters, $hub_methods hub methods, $rest_routes REST routes, $vue_routes Vue routes, and $discord_commands Discord commands."
else
  fail=1
fi

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
