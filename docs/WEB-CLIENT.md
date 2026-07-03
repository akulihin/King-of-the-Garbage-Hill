# King of the Garbage Hill — Web Client (Vue 3 SPA)

> Code-verified against the working tree of 2026-07-02 (v4.1.8). Companion docs: [WEB-BACKEND.md](WEB-BACKEND.md) (the server this SPA talks to), [ARCHITECTURE.md](ARCHITECTURE.md) (§6 state→screen, §7 the per-character 14-file pattern — steps 13-14 land here), [DISCORD-INTERFACE.md](DISCORD-INTERFACE.md). Client root: `Web/VueClient/`. All anchors below are into `Web/VueClient/src/` unless the file name says otherwise.

## 1. Stack & build

- Vue 3.5 + TypeScript + Pinia 2 + vue-router 4, SignalR client `@microsoft/signalr` 8, icons `lucide-vue-next` (package.json dependencies). Scripts: dev = vite, build = vite build, serve = vite preview, type-check = vue-tsc (broken in this environment — **`pnpm build` is the verifier**, per CLAUDE.md), lint = eslint --fix.
- Build outputs **straight into the backend's wwwroot**: `outDir` ../../King-of-the-Garbage-Hill/wwwroot with `emptyOutDir` (`vite.config.ts:15-19`) — deploying the backend ships the SPA.
- Dev proxy forwards `/api`, `/gamehub` (ws), `/art`, `/sound` to the **production EC2 box** http://3.65.44.127 (`vite.config.ts:24-44`) — local dev talks to prod by default; a commented localhost:3535 alternative sits in the .env file. Prod build uses same-origin: .env.production sets empty VITE_API_HOST and VITE_SIGNALR_HUB=/gamehub.
- Path alias `src` → ./src (`vite.config.ts:10-14`); vitest config references a missing ./src/setupTests.ts (`vite.config.ts:45-67`) — tests are effectively not runnable.
- Gotcha: `VITE_API_BASE` is read in two places but **never defined** in any env file — it resolves to '' so fetches stay relative (`replay.ts:11`, `Lobby.vue:9`); works only because of the proxy/same-origin setup.

## 2. App shell & routing

Entry `main.ts:1-12`: createApp + createPinia + router + assets/main.css. `App.vue` is the shell:

- **Login gate**: until authenticated, renders `LoginProcess` (`App.vue:102-108`), then a `LoginSuccess` splash (`App.vue:111-117`), then top bar + RouterView (`App.vue:120-164`).
- **Auto-login on mount** (`App.vue:28-44`): web account from localStorage `kotgh_web_id` + `kotgh_web_username` first (`App.vue:33-39`), else Discord `discordId` (`App.vue:40-43`).
- **Top bar** (`App.vue:122-152`): nav Lobby / `Морской Бой - minigame` / Home (`App.vue:127-131`), connection dot bound to `store.isConnected` (`App.vue:135-138`), user label, Logout (clears all three localStorage keys, `App.vue:86-96`), theme dropdown (`App.vue:143-150`).
- **Themes**: dark by default; five alternates applied as a `data-theme` attribute persisted in localStorage `kotgh_theme` (`App.vue:15-26`) — blood, neon, forest, dark-light, siri, defined in assets/base.css; `App.vue:172-230` maps the palette onto semantic vars (--bg-*, --text-*, --accent-gold, glows, easing).
- **Error toast** bound to `store.errorMessage` (`App.vue:155-159`).

Routes (`router.ts:15-74`, history mode `router.ts:76-79` — needs the backend SPA fallback, WEB-BACKEND.md §1):

| Path | Name | Page | Purpose | Anchor |
|---|---|---|---|---|
| `/` and `/game` | — | redirect → /games | | `router.ts:16-23` |
| `/games` | lobby | pages/Lobby.vue | game list, quests, loot, replays | `router.ts:24-28` |
| `/game/:gameId` | game | pages/Game.vue | the in-game view | `router.ts:29-34` |
| `/spectate/:gameId` | spectate | pages/Spectate.vue | read-only live view | `router.ts:35-40` |
| `/replay/:gameId` | replay | pages/Replay.vue | REST-loaded replay browser | `router.ts:41-46` |
| `/home` | home | pages/Home.vue | **non-functional mock** (§13) | `router.ts:47-51` |
| `/widget` | widget | pages/Widget.vue | Discord OAuth widget sync (§13 in WEB-BACKEND §12 chain) | `router.ts:52-56` |
| `/battleship` | battleship | pages/BattleshipLobby.vue | minigame lobby | `router.ts:57-61` |
| `/battleship/:gameId` | battleshipGame | pages/BattleshipGame.vue | minigame board | `router.ts:62-67` |
| `/battleship/spectate/:gameId` | battleshipSpectate | pages/BattleshipSpectate.vue | minigame spectate | `router.ts:68-73` |

## 3. Identity & session (client side)

Two login paths in `LoginProcess.vue`: a numeric **Discord User ID** input validated `/^\d+$/` with a "Copy User ID via Developer Mode" hint (`LoginProcess.vue:20-29`, validation `LoginProcess.vue:74`), or a **web username** (max 32 chars) that emits `webLogin` (`LoginProcess.vue:44` `LoginProcess.vue:65` `LoginProcess.vue:83`). `App.vue` then connects + authenticates and persists `discordId` (`App.vue:53-60`) or lets the server mint a web account via `registerWebAccount` (`App.vue:75-80`) — the store saves `kotgh_web_id`/`kotgh_web_username` when `WebAccountCreated` arrives (`game.ts:280-288`). `restoreWebSession` re-authenticates from those keys (`game.ts:554-565`). There is no password — see the trust model in WEB-BACKEND.md §2.

## 4. SignalR client (`signalr.ts`)

Singleton `signalrService` (`signalr.ts:1413`); hub URL from VITE_SIGNALR_HUB, default /gamehub (`signalr.ts:3`).

- **Lifecycle**: `connect()` builds the connection `withAutomaticReconnect` at 0/1s/2s/5s/10s/30s (`signalr.ts:1001-1008`), registers every handler (`signalr.ts:1011-1090`), tracks state via `onreconnecting`/`onreconnected`/`onclose` (`signalr.ts:1092-1112`); **on reconnect it re-authenticates with the remembered ID and re-joins the remembered game** (`signalr.ts:1098-1106`, remembered in `authenticate`/`joinGame`, `signalr.ts:1131-1140`).
- **Server events → store** (handlers assigned in `useGameStore.connect()`, `game.ts:140-314`): `GameState` (`signalr.ts:1011`) → state + side effects (§6); `LobbyState` (`signalr.ts:1015`); `ActionResult` (`signalr.ts:1019`) → `lastAction`, failure shows `errorMessage` for 3 s (`game.ts:243-254`); `GameEvent` (`signalr.ts:1023`) → `lastEvent`, eventType GameStory fills `gameStory` (`game.ts:256-264`); `Error` (`signalr.ts:1027`); `Authenticated` (`signalr.ts:1032`) → `isAuthenticated`, `accountPlayerType`, `lastPlayedCharacter` (`game.ts:270-278`); `WebAccountCreated` (`signalr.ts:1036`); `GameCreated`/`GameJoined` (`signalr.ts:1040-1046`) → navigation is done by the Lobby page; `BlackjackState` (`signalr.ts:1048`); `QuestState` (`signalr.ts:1052`); `LootBoxOpened` (`signalr.ts:1056`); `AchievementBoard` (`signalr.ts:1060`); `CharacterList` (`signalr.ts:1064`); `BattleshipLobby`/`BattleshipState`/`BattleshipGameCreated`/`BattleshipGameJoined`/`BattleshipEvent`/`ShipCatalog` (`signalr.ts:1068-1090`) → battleship store.
- **Invoke wrappers** mirror the hub 1:1 (WEB-BACKEND.md §4 for server behavior): session `authenticate`/`joinGame`/`leaveGame`/`requestGameState`/`requestLobbyState` (`signalr.ts:1131-1153`); core actions attack…aramConfirm (`signalr.ts:1157-1207`); draftSelect (`signalr.ts:1211-1213`); character actions darksciChoice/youngGleb/dopaChoice/deathNoteWrite/shinigamiEyes/setPreferWeb/activateShen/deactivateShen/rewriteHistory/finishGame (`signalr.ts:1217-1259`); blackjack ×5 (`signalr.ts:1263-1281`); account/lobby registerWebAccount/createWebGame/joinWebGame/getCharacterList/createTestGame (`signalr.ts:1285-1303`); meta requestQuests/openLootBox/requestAchievements/clearNewAchievements (`signalr.ts:1305-1319`); battleship ×23 (`signalr.ts:1323-1409`).

## 5. The GameState contract (TS mirror of the server DTOs)

Defined at the top of `signalr.ts`; camelCase mirror of `GameStateDto.cs` (WEB-BACKEND.md §7 for what gets masked and when):

| Type | Key contents | Anchor |
|---|---|---|
| `GameState` | gameId, roundNo, turnLengthInSecond/timePassedSeconds (timer), gameVersion/gameMode, isFinished, isAramPickPhase, isDraftPickPhase + draftOptions, isKratosEvent, globalLogs/allGlobalLogs/fullChronicle, **myPlayerId (null = spectator)**, myPlayerType (2 = admin), preferWeb, allCharacterNames/allCharacters (predict dropdowns), pinkWardRevealedPlayerIds, players, teams, fightLog, newlyUnlockedAchievements | `signalr.ts:5-40` |
| `Player` | identity + isBot/isWebPlayer/teamId, isDead/deathSource, isKira/isBug, owner-only blocks deathNote/portalGun/exploitState/tsukuyomiState/passiveAbilityStates, exploit markers, choice flags darksciChoiceNeeded/youngGlebAvailable/dopaChoiceNeeded, character, status, predictions, customLeaderboardPrefix/customLeaderboardText (server-rendered HTML), characterMasteryPoints, isInMyHarmRange | `signalr.ts:42-87` |
| `Character` | name/avatar/description/tier, the four stats, skillDisplay/moralDisplay (display strings), justice/seenJustice, skillClass/skillTarget, classStatDisplayText (format "Class \|\| description"), per-stat resists + bonus texts, passives | `signalr.ts:381-410` |
| `PlayerStatus` | score (−1 when hidden), place, isReady/isBlock/isSkip/isAutoMove, confirmedPredict/confirmedSkip, lvlUpPoints, moveListPage, personalLogs/previousRoundLogs/allPersonalLogs (rounds split by `\|\|\|`), scoreSource, directMessages, mediaMessages, ARAM flags + reroll counters, placeHistory, scoreBreakdown | `signalr.ts:430-453` |
| `PassiveAbilityStates` | the **widget contract**: ~40 optional keys (bulk, tea, jew, hardKitty, training, dragon, garbage, copycat, inkScreen, tigerTop, jaws, privilege, vampirism, weed, saitama, shinigamiEyes, seller, sellerMark, dopa, goblinSwarm, kotiki, kotikiCatOnMe, monster, monsterPawnOnMe, pickleRick, giantBeans, tolyaCount, impact, darksci, deepList, craboRack, napoleon, support, toxicMate, toxicMateCancerOnMe, yongGleb, theBoys, theBoysSupOnMe, theBoysVirusOnMe, theBoysMoralBlocked, salldorum, geralt, geraltMonsterOnMe) | `signalr.ts:129-173`, shapes `signalr.ts:175-337` |
| `FightEntry` | structured per-fight data for the animation: participants, outcome, class/versatility, weighing deltas, justice, random roll, moral changes, drops, **attackerForOneFightMods/defenderForOneFightMods** and Storm fields (stormAppeared/stormWeighingDelta/stormFlipped) | `signalr.ts:530-633`, `ForOneFightMod` `signalr.ts:635-641` |
| Misc | Prediction (`signalr.ts:455-464`), Team (`signalr.ts:466-469`), LobbyState/ActiveGame (`signalr.ts:471-488`), CharacterInfo (`signalr.ts:490-499`), DraftOptionDto (`signalr.ts:507-518`), MediaMessage (`signalr.ts:520-528`), Quest/LootBox (`signalr.ts:645-665`), AchievementBoard/Entry (`signalr.ts:669-689`), ActionResult (`signalr.ts:691-695`), Blackjack types (`signalr.ts:341-379`), Battleship types (`signalr.ts:699-886`), Replay types (`signalr.ts:890-954`), GameEvent (`signalr.ts:956-964`) | |

Sentinels from the server: stats −1 and name "???" for masked opponents, score −1 when hidden — components branch on these rather than on nullability.

**Adding a new widget/state** (ARCHITECTURE.md §7 steps 13-14): extend `PassiveAbilityStates` + its state type here, then render in PlayerCard.vue (§9).

## 6. Stores (Pinia)

**`useGameStore`** (`game.ts:54`) — the core store.
- State refs (`game.ts:57-81`): identity (discordId, isAuthenticated, isConnected, webUsername, isWebAccount, accountPlayerType, lastPlayedCharacter), gameState, lobbyState, lastAction/lastEvent/errorMessage, pendingLevelUp, gameStory, blackjackState, questState, lootBoxResult, achievementBoard, newlyUnlockedAchievements, characterList.
- Getters: `myPlayer` by myPlayerId — null for spectators (`game.ts:85-92`); `opponents` (`game.ts:94-99`); `isMyTurn` = not ready and not skip (`game.ts:101-104`); `roundTimeLeft` = turnLength − timePassed (`game.ts:106-109`); `isInGame`, `isAdmin` (game-scoped), `isLobbyAdmin` (account-scoped) (`game.ts:111-115`); character helpers isKira/myPortalGun/isBug/myExploitState/myPickleRick/myGiantBeans/canFireGunDuringPickle (`game.ts:117-136`).
- `connect()` (`game.ts:140-314`) wires every SignalR callback; the `GameState` handler additionally: diffs own stats to fire level-up/stat-max sounds and class-change stingers (`game.ts:161-210`), **auto-joins Blackjack when newly dead to Kira** (`game.ts:212-215`), and captures finish-time achievements once per game (`game.ts:217-220`).
- Action wrappers (`game.ts:344-565`) mirror the hub and layer client SFX: block plays block + Geralt meditation + turn-10 layer (`game.ts:349-358`), levelUp records `pendingLevelUp` for sound resolution (`game.ts:389-396`), moral exchanges play once per round (`game.ts:398-414`).

**`useReplayStore`** (`replay.ts:52`) — replay playback. State: replayData/currentRound/currentPlayerIndex/currentFightIndex (`replay.ts:53-59`). `computedGameState` (`replay.ts:79-160`) **reconstructs a full GameState** from the chosen round + player perspective so the normal in-game components render replays unchanged: strips other players' private blocks (`replay.ts:113-123`), forces isFinished true and myPlayerType 2 (`replay.ts:143` `replay.ts:152`), and `buildShiftedPlayer` shows pre-fight stats from the previous round (`replay.ts:17-50`). `loadReplay` fetches the REST replay (`replay.ts:164-186`).

**`useBattleshipStore`** (store/battleship.ts) — minigame state/lobby/placement/combat/VFX state with its own signalr callback wiring; independent of the main game store (§12).

## 7. Pages & flows

- **Lobby** (`Lobby.vue`): polls `refreshLobby` every 3 s while connected (`Lobby.vue:62-64`); sections — Daily Quests with streak (`Lobby.vue:129-133`), loot-box open button (`Lobby.vue:181`) + overlay, `AchievementBoard` overlay (`Lobby.vue:204`), Active Games grid (join → `joinWebGame` `Lobby.vue:101`, spectate → `Lobby.vue:109`), recent replays fetched over REST with the `X-Discord-Id` header (`Lobby.vue:49-50`, open → `Lobby.vue:113`), static "How to Play" cards (`Lobby.vue:393-396`). Create game → `createWebGame` (`Lobby.vue:83`); navigation happens on `GameCreated`/`GameJoined` (`Lobby.vue:66-71`). Admins additionally get "Last Play X" / "Test New Game" with a character picker → `createTestGame` (`Lobby.vue:228` `Lobby.vue:236` `Lobby.vue:97`).
- **Game** (`Game.vue`, 2936 lines — the main view): three-column layout `game-layout` (`Game.vue:1086` `Game.vue:1639`; single-column on mobile `Game.vue:1682`): left = PlayerCard + action buttons, center = leaderboard + fight panel + log panels, right = big avatar + SkillsPanel. Phase branches: skeleton while no state, **draft-pick overlay** with free/paid switch buttons calling `draftSelect` (`Game.vue:1003-1078`), waiting screen. Action buttons: Block / Auto Move / Change Mind / Confirm Skip (`Game.vue:1107-1116`), Darksci choice `Мне не везёт...` / `Мне повезёт!` (`Game.vue:1121-1126`), `Вспомнить Молодость` (`Game.vue:1130-1132`), Dopa tactics Стомп/Фарм/Доминация/Роум (`Game.vue:1136-1140`) + second-target hint `Выберите вторую цель (скрытая атака)` (`Game.vue:1150`); attack itself comes from the Leaderboard row click → `onAttack` (`Game.vue:94` `Game.vue:1250`). Header controls: PreferWeb toggle (`Game.vue:401`), layout-order cycle persisted as `kotgh_layout_order` (`Game.vue:435-453`), fight-panel size `kotgh_fight_panel_fixed` (`Game.vue:465-469`), fight-style cycle v3/v2/v1 `kotgh_fight_style` (`Game.vue:475-489`), `RoundTimer` (`Game.vue:1217`), Finish with confirm (`Game.vue:493-494` `Game.vue:1226`). Overlays: round-announce cinematic (`Game.vue:927-933`), game-over podium, reconnect veil. `DeathNote` panel for Kira (`Game.vue:1259`).
- **Spectate** (`Spectate.vue`): joins the game room read-only (`Spectate.vue:31`) and renders `FightAnimation` (`Spectate.vue:68`), `Leaderboard` (`Spectate.vue:80`), `BattleLog` (`Spectate.vue:96`); no action UI because the myPlayer getter is null.
- **Replay** (`Replay.vue`): feeds the reconstructed state (§6) into the same Leaderboard/PlayerCard/SkillsPanel/FightAnimation; round + player-perspective pickers (store setters `replay.ts:188-209`).
- **Widget** (`Widget.vue`): parses `access_token` from the URL fragment (`Widget.vue:22`), scrubs it from history (`Widget.vue:31-32`), POSTs to the widget sync endpoint (`Widget.vue:38`) — the tail of the OAuth chain in WEB-BACKEND.md §12.
- **Home** (`Home.vue`): placeholder mock (§13).

## 8. Component inventory (main game)

| Component | Renders | User actions → store | Anchor |
|---|---|---|---|
| `PlayerCard.vue` (4313 ln) | own stats/resists/justice/moral/skill/class, score, level-up UI, all §9 widgets | stat + → `levelUp` (`PlayerCard.vue:595`), moral buttons → `moralToPoints`/`moralToSkill` (`PlayerCard.vue:599-603` `PlayerCard.vue:994-1004`), Kira eyes → `shinigamiEyes` (`PlayerCard.vue:1010`), Geralt demand → `demandContractReward` (`PlayerCard.vue:1040-1047`) | |
| `Leaderboard.vue` | hill-tiered rows, avatars, masked mini-stats, predictions, attack cursors | row click → emit attack with target place (`Leaderboard.vue:199`); predict picker → emit predict (`Leaderboard.vue:143`) | |
| `FightAnimation.vue` | 4 tabs `Бои раунда` / `Все бои` / `Летопись` / `История` (`FightAnimation.vue:1266-1269`), step-by-step fight replay in 3 styles (v3 default, v2 `FightArenaCards`, v1 `FightArenaClassic`, `FightAnimation.vue:1404` `FightAnimation.vue:1462`) | playback controls; emits drive PlayerCard sounds | |
| `SkillsPanel.vue` | collapsible passive cards; TheBoys locked ultimates show `Скрытая способность` + `Заблокировано` (`SkillsPanel.vue:140-143`) | reveal/unlock cinematics watch theBoys revealSerial/unlockSerial (`SkillsPanel.vue:102` `SkillsPanel.vue:117`) | |
| `DeathNote.vue` | Kira's kill list + history + revealed names | emits write {targetPlayerId, characterName} (`DeathNote.vue:77`) and shinigamiEyes (`DeathNote.vue:15-17`); wired in Game.vue | |
| `RoundTimer.vue` | m:ss countdown from `roundTimeLeft` (`RoundTimer.vue:20`), urgent < 30 s / critical < 15 s + screen vignette (`RoundTimer.vue:33-46`) | display-only | |
| `MediaMessages.vue` | character phrase cards (text/audio/image); audio elements managed **imperatively** so 300 ms state re-renders don't restart playback (`MediaMessages.vue:10-17`) | local play/pause | |
| `BattleLog.vue` | parses global-log text into headers/rows | display-only | |
| `Blackjack21.vue` | dead-player table "Мир Шинигами — Игра 21": dealer/player hands, hit/stand (`Ещё`/`Хватит`), new round, Dark-Souls-style word-composer message | blackjack store actions (line anchors unverifiable — digit filename, see tools/verify-docs.sh regex) | |
| `LootBox.vue` / `AchievementBoard.vue` / `AchievementPopup.vue` | loot reveal overlay; achievement grid with secret masking; sequential unlock popups | dismiss/request actions | |
| `ScoreOdometer.vue` | rolling-digit score | display-only | |
| `ActionPanel.vue` | **orphaned** — buttons moved inline into Game.vue (comment `Game.vue:8`, remnants `Game.vue:356` `Game.vue:1169`) | do not extend | |

## 9. Widget system (PlayerCard)

Widgets render **only for the owning player**: `passiveStates` computed returns the player's `passiveAbilityStates` (`PlayerCard.vue:153`), which the server only populates for isMe (WEB-BACKEND.md §7). Standard shell class pc-passive-widget with pw-header/pw-value markup.

- **Level-up overrides** replace the stat + buttons per character (badge + Irelia nerf variant `PlayerCard.vue:617-619` `PlayerCard.vue:891-900`): Goblin upgrade menu (Правильное питание / Контрактная армия / Трудовые условия / Праздник Гоблинов → `levelUp(1-4)`, `PlayerCard.vue:623-642`), TheBoys member upgrades (`PlayerCard.vue:668`), Geralt oil upgrades (`PlayerCard.vue:715`), Kotiki single justice button (`PlayerCard.vue:790`).
- **Action-hosting widgets**: Geralt contract-demand panel `Потребовать больше монет за заказ` → previous/next (`PlayerCard.vue:1018-1047`), Kira Shinigami-eyes button (`PlayerCard.vue:1010`). Moral exchange rates are computed client-side to mirror backend formulas (`PlayerCard.vue:282-310`).
- **Widget blocks** keyed on `passiveStates` members (each renders when its key is present): pickleRick `PlayerCard.vue:1109`, giantBeans `PlayerCard.vue:1133`, bulk `PlayerCard.vue:1153`, tea `PlayerCard.vue:1170`, jew `PlayerCard.vue:1180`, hardKitty `PlayerCard.vue:1191`, training `PlayerCard.vue:1202`, dragon `PlayerCard.vue:1217`, garbage `PlayerCard.vue:1229`, copycat `PlayerCard.vue:1241`, inkScreen `PlayerCard.vue:1258`, tigerTop `PlayerCard.vue:1275`, jaws (animated shark) `PlayerCard.vue:1289`, privilege `PlayerCard.vue:1314`, vampirism `PlayerCard.vue:1325`, weed `PlayerCard.vue:1342`, saitama `PlayerCard.vue:1359`, shinigamiEyes `PlayerCard.vue:1376`, seller `PlayerCard.vue:1386`, dopa `PlayerCard.vue:1407`, sellerMark `PlayerCard.vue:1425`, goblinSwarm (population bars) `PlayerCard.vue:1443`, kotiki `PlayerCard.vue:1483`, kotikiCatOnMe `PlayerCard.vue:1521`, monster `PlayerCard.vue:1533`, monsterPawnOnMe `PlayerCard.vue:1546`, tolyaCount `PlayerCard.vue:1557`, impact `PlayerCard.vue:1567`, darksci `PlayerCard.vue:1577`, deepList `PlayerCard.vue:1585`, craboRack `PlayerCard.vue:1602`, napoleon `PlayerCard.vue:1610`, support `PlayerCard.vue:1627`, toxicMate `PlayerCard.vue:1635`, toxicMateCancerOnMe `PlayerCard.vue:1655`, yongGleb `PlayerCard.vue:1663`, theBoys (member grid + ultimates) `PlayerCard.vue:1673`, theBoysSupOnMe `PlayerCard.vue:1754`, theBoysVirusOnMe `PlayerCard.vue:1758`, theBoysMoralBlocked `PlayerCard.vue:1762`, salldorum `PlayerCard.vue:1768`, geralt order board `PlayerCard.vue:1794`, geraltMonsterOnMe `PlayerCard.vue:1827`. Special top-level (non-passiveStates) blocks: Portal Gun / Exploit / Tsukuyomi around `PlayerCard.vue:1064-1092`.

## 10. Logs & real-time UX

- Two log panels in Game.vue: current-round merged personal+global and previous-round; entries are parsed and color-classified by `parsePrevLogs` with a Russian keyword filter (`Game.vue:667-737`); gold point entries feed PlayerCard's combo feed instead of the panel.
- `Летопись` (chronicle) uses server `fullChronicle` when the game is finished (`Game.vue:620-626`); the `История` tab shows the AI story when present (`FightAnimation.vue:1269`).
- Discord custom emoji in server-rendered strings are mapped to local /art/emojis images via `discordEmojiMap` (`Game.vue:530-559`).
- Round countdown: `RoundTimer` re-derives from `roundTimeLeft` each second (§8) — the server pushes time every ≤0.5 s drift (WEB-BACKEND.md §6).
- Server "direct messages" (`WebMessages`) and media messages surface as popups/cards on the owning client only.

## 11. Sound & art

- Web-Audio engine in `sound.ts`; all clips stream from the R2 CDN base https://r2.ozvmusic.com/kotgh/sound/ (`sound.ts:1`); URL builder (`sound.ts:168`).
- Master volume persisted under `kotgh-master-volume` (`sound.ts:39`); per-group gains fetched from /sound-config.json — a static file in Web/VueClient/public/, fetched no-store (`sound.ts:63`). (Never line-anchor that filename: the verify-docs regex would truncate it to a bogus anchor.)
- `setSoundContext('game'|'menu')` gates ambient themes (`sound.ts:114`); a global click-SFX listener is installed by App.vue (`sound.ts:361` `App.vue:29`); opt-out/re-skin per button via data attributes data-sfx-skip-default / data-sfx-utility / data-sfx-fight-tab / data-sfx-predict (`sound.ts:2-5`).
- Character avatars come from the R2 CDN or local /art/avatars when present (server rewrites, WEB-BACKEND.md §7); emojis always local /art/emojis (§10).

## 12. Battleship client

Standalone minigame UI: pages BattleshipLobby.vue / BattleshipGame.vue / BattleshipSpectate.vue (routes §2), `useBattleshipStore` (store/battleship.ts) with its own callbacks, components under components/battleship/ (board grid, fleet builder, weapon/summon bars, VFX canvas via composables/useVfx.ts, tooltips via composables/useTip.ts). Its BattleLog.vue there shares a name with the main-game one — **only components/BattleLog.vue is line-anchorable** (duplicate basename; the battleship one must be referenced by path without line numbers).

## 13. Known gaps & dead surface

- **ARAM pick phase has no web UI**: the contract carries isAramPickPhase and the store exposes `aramReroll`/`aramConfirm` (`game.ts:431-439`), but no component calls them — a web-preferring player in an ARAM game has no pick surface (finding **m24** in AUDIT-FINDINGS.md).
- `ActionPanel.vue` is orphaned (§8) — extend the inline buttons in Game.vue instead.
- Home.vue and its components/Home/ children are hardcoded mocks (fake profile/currency, lorem patch notes) with a dead /about "Encyclopedia" link — no route registers /about (`router.ts:15-74`).
- `VITE_API_BASE` undefined (§1); vitest setup file missing (§1); the eslint config ignores a generated src/services/api.ts that does not exist.
- Blackjack21.vue line anchors are invisible to tools/verify-docs.sh (digits in the basename don't match the anchor regex) — keep its references file-level.
