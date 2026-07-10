# King of the Garbage Hill — Web Backend (API, SignalR, push, auth)

> Code-verified against the working tree of 2026-07-10 (v4.1.8). Companion docs: [ARCHITECTURE.md](ARCHITECTURE.md) (§1 process topology, §6 state→screen), [WEB-CLIENT.md](WEB-CLIENT.md) (the Vue SPA consuming this surface), [DISCORD-INTERFACE.md](DISCORD-INTERFACE.md) (the other frontend). This doc is the deep catalog of the ASP.NET Core surface: every endpoint, hub method, pushed event, and visibility rule.

## 1. Startup & topology

Process topology is in ARCHITECTURE.md §1; the web half in one paragraph: `StartWebApi()` runs on a fire-and-forget background task (`Program.cs:68`) inside the same process as the Discord bot. It builds a separate ASP.NET Core DI container and **bridges the Lamar singletons into it by instance** — `Global`, `GameReaction`, `CheckIfReady`, `GameUpdateMess`, `HelperFunctions`, `CharactersPull`, `CharacterPassives`, `Config`, `HttpClient`, `StartGameLogic`, `UserAccounts`, `DiscordWidgetService` (`Program.cs:96-107`) — so both frontends mutate the same live `GamesList` (`Global.cs:17`). Web-only singletons are registered on top: `WebGameService`, `GameNotificationService`, `GameStoryService`, `BlackjackService`, `BattleshipService`, `ReplayService` (`Program.cs:110-115`).

- JSON is camelCase for both SignalR (`Program.cs:118-123`) and controllers (`Program.cs:126-131`) — the TS interfaces in `signalr.ts` mirror C# DTOs 1:1 modulo casing.
- CORS default policy (`Program.cs:134-150`): origins `http://localhost:5173` (Vite), `http://localhost:3535`, `http://localhost`, `http://kotgh.ozvmusic.com`, `https://kotgh.ozvmusic.com`; any header/method + credentials.
- Middleware order (`Program.cs:152-196`): `UseDefaultFiles` → `UseStaticFiles` (wwwroot = built Vue SPA, `Program.cs:155-156`) → static `/art` from DataBase/art (`Program.cs:159-168`) → static `/sound` from DataBase/sound (`Program.cs:174-187`) → `UseRouting` → `UseCors` (`Program.cs:189-190`) → `MapControllers` (`Program.cs:192`) → `MapHub` at /gamehub (`Program.cs:193`) → SPA fallback `MapFallbackToFile` to `index.html` (`Program.cs:196`) — this fallback is what makes the client's history-mode routes work on refresh.
- Port: `KOTGH_PORT` env var, default 80, binds 0.0.0.0 (`Program.cs:198-200`). The env var is read **only** here; Config holds only `Token` and `AnthropicApiKey` (`Config.cs:9-28`, loaded from DataBase/config.json at `Config.cs:16`).
- In `--sim` mode neither Discord nor Kestrel starts (`Program.cs:61-66`); anything that needs a hub context (story push, notifications) simply never runs headless.
- Global web-relevant surface: `GamesList` (`Global.cs:17`), `FinishedGamesList` (`Global.cs:35`), and three callbacks — `OnGameFinished` (`Global.cs:41`) and `OnReplaySave` (`Global.cs:46`) registered by GameNotificationService, `SimErrorSink` for the sim harness (`Global.cs:52`). Game IDs are sequential via `GetNewtGamePlayingAndId` (`Global.cs:92-96`, note the "Newt" typo — it's load-bearing API).

## 2. Identity & auth — the trust model (Доверие)

**There is no password, token, cookie, or server-side session for normal play. The client asserts a Discord ID and the server trusts it.** Impersonation is possible by design (hobby-scale); the only OAuth-verified path is the profile widget (§12).

| Channel | Mechanism | Anchor |
|---|---|---|
| SignalR | client invokes `Authenticate(string discordIdStr)`; parsed ulong stored in `Context.Items["discordId"]`, connection registered | `GameHub.cs:61-78` |
| REST | `X-Discord-Id` header parsed per request; missing/unparseable → 401 | `GameController.cs:204-210` |
| Widget | Discord OAuth Bearer token verified against users/@me | `DiscordWidgetService.cs:116-143` |

IDs travel as **strings** over SignalR because Discord snowflakes exceed JS safe-integer range (`GameHub.cs:15-16`; string reply at `GameHub.cs:76`). On disconnect the connection is unregistered and the only "session" — the live connection's `Context.Items` — is gone (`GameHub.cs:45-53`).

**Account ID ranges** (routing in `UserAccounts.GetAccount`, `UserAccounts.cs:78-100`):

| Range | Kind | Created by |
|---|---|---|
| ≤ 1 000 000 | bot | `CreateBotAccount`, PlayerType 404 (`UserAccounts.cs:160-177`) |
| normal snowflakes | Discord human | `CreateUserAccount`, PlayerType 0, prefix `"*"` (`UserAccounts.cs:131-158`) |
| ≥ 9 000 000 000 000 000 000 | web-only | `CreateWebAccount` (`UserAccounts.cs:185-202`) via hub `RegisterWebAccount` (`GameHub.cs:152-167`): validates username 1-32 chars, allocates a monotonic ID (`GenerateWebUserId`, `UserAccounts.cs:204-210`; counter resumed past max existing web ID at startup, `UserAccounts.cs:37-46`), auto-authenticates the connection and replies `WebAccountCreated` |

`PlayerType` semantics: 0 Normal, 1 Casual, 2 Admin, 404 Bot (comment at `UserAccounts.cs:138`). Admin unlocks raw logs, full fight math, unmasked opponents (§7) and `CreateTestGame` (`GameHub.cs:510-535`).

**Persistence**: accounts live in a `ConcurrentDictionary` (`UserAccounts.cs:17`) loaded at construction and flushed to flat JSON every 60 s by timer (`UserAccounts.cs:49-59`); `IsPlaying` is force-reset for everyone at startup (`ClearPlayingStatus`, `UserAccounts.cs:61-65`).

## 3. REST endpoints (api/game, api/widget)

`GameController` (route api/game, `GameController.cs:16-19`) is the HTTP alternative to the hub — **only a subset of actions exists here**; character-specific actions, draft, quests, mini-games are hub-only. Auth = `X-Discord-Id` header except where noted. All action endpoints return success:true or a 400 error payload.

| Verb & route | Body / query | Auth | Delegates to | Anchor |
|---|---|---|---|---|
| GET `lobby` | — | none | `GetLobbyState` | `GameController.cs:34-38` |
| GET `{gameId}` | — | header | `GetGameState` (scoped DTO) | `GameController.cs:40-48` |
| GET `{gameId}/spectate` | — | none | spectator DTO | `GameController.cs:50-55` |
| POST `{gameId}/attack` | `AttackRequest` (TargetPlace) | header | `WebGameService.Attack` | `GameController.cs:59-67` |
| POST `{gameId}/block` | — | header | `.Block` | `GameController.cs:69-77` |
| POST `{gameId}/auto-move` | — | header | `.AutoMove` | `GameController.cs:79-87` |
| POST `{gameId}/change-mind` | — | header | `.ChangeMind` | `GameController.cs:89-97` |
| POST `{gameId}/confirm-skip` | — | header | `.ConfirmSkip` | `GameController.cs:99-107` |
| POST `{gameId}/confirm-predict` | — | header | `.ConfirmPredict` | `GameController.cs:109-117` |
| POST `{gameId}/level-up` | `LevelUpRequest` (StatIndex: 1=Int 2=Str 3=Speed 4=Psyche) | header | `.LevelUp` | `GameController.cs:119-127` |
| POST `{gameId}/moral-to-points` | — | header | `.MoralToPoints` | `GameController.cs:129-137` |
| POST `{gameId}/moral-to-skill` | — | header | `.MoralToSkill` | `GameController.cs:139-147` |
| POST `{gameId}/predict` | `PredictRequest` (TargetPlayerId, CharacterName) | header | `.Predict` | `GameController.cs:149-157` |
| POST `{gameId}/aram-reroll` | `AramRerollRequest` (Slot: 1-4 passives, 5 stats) | header | `.AramReroll` | `GameController.cs:159-167` |
| POST `{gameId}/aram-confirm` | — | header | `.AramConfirm` | `GameController.cs:169-177` |
| GET `replay/{hash}` | — | none | `ReplayService.LoadReplay` | `GameController.cs:181-186` |
| GET `replays` | query limit (default 20, clamped 1-50) | header | `account.ReplayHashes` → `LoadReplaysByHashes` | `GameController.cs:188-200` |

Request DTOs: `GameStateDto.cs:271-292`. `WidgetController` has the single endpoint POST api/widget/sync with body `SyncRequest` (AccessToken) → `TryVerifyAndAuthorizeAsync` (`WidgetController.cs:11-35`), see §12.

## 4. SignalR hub `/gamehub` — client-callable methods

`GameHub` (`GameHub.cs:20`). Connection life-cycle: `OnConnectedAsync` logs only (`GameHub.cs:41-45`); `OnDisconnectedAsync` unregisters the connection (`GameHub.cs:47-55`). Rooms: SignalR group `game-{gameId}` joined in `JoinGame` (`GameHub.cs:99-105`), battleship group `bs-{gameId}` (`GameHub.cs:982-993`). Connection→player and connection→game maps live in GameNotificationService, not the hub (`GameNotificationService.cs:32-35`).

Every game-action method starts with `GetDiscordId()` (`GameHub.cs:1314-1319`) and replies event `Error` = "Not authenticated. Call Authenticate first." if unset (`SendNotAuthenticated`, `GameHub.cs:1321-1324`). Action methods reply `ActionResult {action, success, error}` and then push fresh personal state via `PushStateToPlayer` (`GameHub.cs:1326-1335`) — on success only, **except `Attack` which pushes unconditionally** (`GameHub.cs:228-235`).

**Session / lobby / rooms:**

| Method (args) | Effect | Anchor |
|---|---|---|
| `Authenticate(discordIdStr)` | bind ID to connection; replies `Authenticated {success, discordId, playerType, lastPlayedCharacter}` | `GameHub.cs:63-78` |
| `JoinGame(gameId)` | join room, remember `Context.Items["gameId"]`, push scoped `GameState` (spectator DTO if unauthenticated or not a player), re-send stored AI story | `GameHub.cs:99-138` |
| `LeaveGame(gameId)` | leave room, unregister | `GameHub.cs:140-144` |
| `RegisterWebAccount(username)` | create web account (§2), auto-auth, `WebAccountCreated` | `GameHub.cs:152-167` |
| `CreateWebGame()` | `WebGameService.CreateGame` (1 human + 5 bots, §8), auto-join room, `GameCreated` | `GameHub.cs:174-194` |
| `JoinWebGame(gameId)` | replace a bot (§8), auto-join room, `GameJoined` + state push | `GameHub.cs:200-223` |
| `CreateTestGame(characterName)` | **admin-only**, non-admin gets `Error` (`GameHub.cs:515-519`); forced-character game (§8) | `GameHub.cs:510-535` |
| `GetCharacterList()` | replies `CharacterList` (name/avatar/tier of rollable characters) | `GameHub.cs:501-505` |
| `FinishGame(gameId)` | leave mid-game → replaced by bot via `EndGame` (`WebGameService.cs:900-908`) | `GameHub.cs:543-550` |
| `SetPreferWeb(gameId, preferWeb)` | sets `player.PreferWeb` directly — suppresses the player's Discord DMs (see DISCORD-INTERFACE.md §4) | `GameHub.cs:558-573` |
| `RequestGameState(gameId)` | on-demand push (player-scoped, falls back to spectator) | `GameHub.cs:767-787` |
| `RequestLobbyState()` | replies `LobbyState` | `GameHub.cs:789-793` |

**Core round actions** (all delegate to WebGameService, §8): `Attack(gameId, targetPlace)` `GameHub.cs:228-235`; `Block` `GameHub.cs:239-246`; `DoAutoMove` `GameHub.cs:250-257`; `ChangeMind` `GameHub.cs:261-268`; `ConfirmSkip` `GameHub.cs:272-279`; `ConfirmPredict` `GameHub.cs:283-290`; `LevelUp(gameId, statIndex)` `GameHub.cs:294-301`; `MoralToPoints` `GameHub.cs:305-312`; `MoralToSkill` `GameHub.cs:316-323`; `DemandContractReward(gameId, demandType)` (Geralt; demandType = previous|next) `GameHub.cs:327-334`; `Predict(gameId, targetPlayerId, characterName)` `GameHub.cs:338-345`; `AramReroll(gameId, slot)` `GameHub.cs:349-356`; `AramConfirm` `GameHub.cs:360-367`; `DraftSelect(gameId, characterName)` `GameHub.cs:373-380`.

**Character-specific actions:** `DarksciChoice(gameId, isStable)`; `YoungGleb(gameId)`; DooM Guy `DoomRoll(gameId)` / `DoomChainsaw(gameId, passiveName)` (`GameHub.cs:386-423`); `DopaChoice(gameId, tactic)`; `DeathNoteWrite(gameId, targetPlayerId, characterName)` (Kira); `ShinigamiEyes(gameId)`; Salldorum's `ActivateShen(gameId, position)`, `DeactivateShen(gameId)`, `RewriteHistory(gameId, roundNumber)` (`GameHub.cs:426-492`).

**Meta (account-level, no game):** DooM Guy `RequestDoomFortress()` → `DoomFortressState` and `EquipDoomModule(stage, slotIndex, moduleName)` → validates category, ownership and 0–3 slot, then swaps/sets without permitting a user-created empty slot (`GameHub.cs:578-644`); `RequestQuests()` → `QuestState`; `OpenLootBox()` → decrements `PendingLootBoxes`, rolls loot, replies `LootBoxOpened`; `RequestAchievements()` → full board with secret achievements masked as `"???"` until unlocked; `ClearNewAchievements()` (`GameHub.cs:646-763`).

**Blackjack (dead-player mini-game, §10):** `BlackjackJoin` `GameHub.cs:797-813`; `BlackjackHit` `GameHub.cs:815-828`; `BlackjackStand` `GameHub.cs:830-843`; `BlackjackNewRound` `GameHub.cs:845-858`; `BlackjackSendMessage(gameId, words[])` `GameHub.cs:860-878` — the composed message is injected into the **main game's global logs** as `[Шинигами] {author}: "{message}"` and broadcast (`InjectGlobalLogMessage`, `GameHub.cs:898-916`). State pushes are personalized per seated player (`PushBlackjackState`, `GameHub.cs:880-896`).

**Battleship (standalone mini-game, §10):** `RequestBattleshipLobby` `GameHub.cs:918-922`; `CreateBattleshipGame` `GameHub.cs:924-944`; `JoinBattleshipWebGame` `GameHub.cs:946-964`; `LeaveBattleshipWebGame` `GameHub.cs:966-980`; `JoinBattleshipGame` (room + state, spectator-capable) `GameHub.cs:982-994`; `LeaveBattleshipGame` `GameHub.cs:996-999`; `BattleshipConfirmReady` `GameHub.cs:1001-1014`; `BattleshipSelectArmy(gameId, faction)` `GameHub.cs:1016-1029`; `BattleshipSelectFleet(gameId, selections)` `GameHub.cs:1031-1052`; `BattleshipPlaceShip(gameId, shipId, row, col, orientation)` `GameHub.cs:1054-1067`; `BattleshipRemoveShip` `GameHub.cs:1069-1082`; `BattleshipConfirmPlacement` `GameHub.cs:1084-1097`; `BattleshipShoot(gameId, row, col)` `GameHub.cs:1099-1132`; `BattleshipShootOwnBoard` `GameHub.cs:1134-1166`; `BattleshipSelectWeapon(gameId, weaponType, shotType)` `GameHub.cs:1168-1175`; `BattleshipDeploySummon(gameId, summonType, col)` `GameHub.cs:1177-1190`; `BattleshipDeployPendingSummon` `GameHub.cs:1192-1205`; `BattleshipManualMove(gameId, shipId, direction, distance)` `GameHub.cs:1207-1220`; `BattleshipSetCursedBoatDirection` `GameHub.cs:1222-1235`; `BattleshipForfeit` `GameHub.cs:1237-1250`; `RequestBattleshipState` `GameHub.cs:1252-1262`; `RequestShipCatalog` `GameHub.cs:1264-1268`. Note battleship game IDs are **strings**. Pushes: personalized to both players + spectator DTO to the room minus players (`PushBattleshipStateToAll`, `GameHub.cs:1270-1310`).

## 5. Server→client events

| Event | Payload | Emitted when | Anchor |
|---|---|---|---|
| `Authenticated` | success, discordId (string), playerType, lastPlayedCharacter | after `Authenticate` | `GameHub.cs:76` |
| `WebAccountCreated` | discordId (string), username | after `RegisterWebAccount` | `GameHub.cs:152-167` |
| `GameCreated` / `GameJoined` | gameId | create/join | `GameHub.cs:194` `GameHub.cs:220` `GameHub.cs:535` |
| `GameState` | full GameStateDto (§6, shape `GameStateDto.cs:8-58`) | on join/request, after each action (actor only), timer broadcast, finish | `GameHub.cs:113` `GameHub.cs:1326-1335` `GameNotificationService.cs:165-172` |
| `ActionResult` | action, success, error | after every hub action | e.g. `GameHub.cs:234` |
| `Error` | string | invalid input / not authenticated / not found | `GameHub.cs:67` `GameHub.cs:1321-1324` |
| `GameEvent` | envelope `{eventType, data}` | eventType `RoundChanged` on round flip (`GameNotificationService.cs:258-261`); `GameFinished` (`GameNotificationService.cs:83`); `GameStory` (`GameStoryService.cs:76-77`; re-sent on join, `GameHub.cs:134-137`); `BlackjackMessage` (`GameHub.cs:914`) | |
| `LobbyState` | LobbyStateDto | on request | `GameHub.cs:789-792` |
| `QuestState` / `LootBoxOpened` / `AchievementBoard` | quest / loot / achievement DTOs | on request/open | `GameHub.cs:646-676` `GameHub.cs:681-706` `GameHub.cs:711-752` |
| `CharacterList` | name/avatar/tier list | on request | `GameHub.cs:501-504` |
| `DoomFortressState` | four stages: slots, unlocked module definitions, remaining reward count/chance | request or successful equip | `GameHub.cs:578-644` |
| `BlackjackState` | personalized table state | after any blackjack action | `GameHub.cs:880-896` |
| `BattleshipLobby`, `BattleshipGameCreated`, `BattleshipGameJoined`, `BattleshipState`, `BattleshipEvent` (eventType `ShotResult`), `ShipCatalog` | battleship DTOs | battleship flow | `GameHub.cs:918-922` `GameHub.cs:924-944` `GameHub.cs:946-964` `GameHub.cs:982-994` `GameHub.cs:1099-1132` `GameHub.cs:1264-1268` |

## 6. Push model — who sends `GameState` when

1. **Per-action**: the hub pushes to the **acting player's connections only** right after each action (`PushStateToPlayer`, `GameHub.cs:1326-1335`).
2. **Timer**: GameNotificationService polls every 300 ms (`GameNotificationService.cs:97-103`) and broadcasts a game when the round number changed, elapsed time moved > 0.5 s, or any player's `IsReady` flipped (`PushUpdates`, `GameNotificationService.cs:218-269`; triggers at `GameNotificationService.cs:242-247`); a `RoundChanged` event accompanies round flips (`GameNotificationService.cs:258-261`). **Finished games are skipped by the timer** (`GameNotificationService.cs:236-240`) — their final state would race the lootbox/achievement stamping in the finish path.
3. **On finish**: the game loop fires `OnGameFinished`, which does the final personalized broadcast, emits `GameFinished`, conditionally kicks off story generation, and drops snapshots/room tracking (kept while a Blackjack table is still open) (`GameNotificationService.cs:78-96`). An active Madara `Вечное Цукуеми` skips the shared story because it would reveal the real ending (`GameNotificationService.cs:82-88`).
4. **Broadcast shape**: `BroadcastGameState` sends a **personalized DTO to every player with a web connection**, then one **spectator DTO** to the remaining connections in the room (`GameNotificationService.cs:178-208`). `SendGameStateToPlayer` is the single-player variant used by game logic (`GameNotificationService.cs:163-171`).
5. Replay saving rides the same finish path: `OnReplaySave` builds + persists the replay and appends its hash to each human's `ReplayHashes` (`GameNotificationService.cs:50-67`). Activated `Вечное Цукуеми` games never invoke that shared callback (`CheckIfReady.cs:795-807`).

## 7. State mapping & hidden information (`GameStateMapper`)

`GameStateMapper.ToDto(game, requestingPlayer = null)` (`GameStateMapper.cs:79-189`) produces the per-viewer projection; null = spectator; `isAdmin` means PlayerType 2 (`GameStateMapper.cs:81`). Top-level shape: `GameStateDto.cs:8-58`; per-player `PlayerDto`: `GameStateDto.cs:62-133`.

Visibility rules (the exact reason the web can't leak hidden info):

| Data | Rule | Anchor |
|---|---|---|
| Opponent character | non-admin, unfinished → name "???", unknown-avatar, stats −1 sentinels, skill/moral "?", empty passives | `GameStateMapper.cs:842-867` |
| Own/admin/finished character | real stats + resists and quality-bonus texts (own only) | `GameStateMapper.cs:869-899` |
| Passive list | owner sees all; other viewers only `Visible` ones (reachable for admin/finished viewers); `Вечное Цукуеми` is omitted for **every** viewer, including Madara/admin | `GameStateMapper.cs:980-993` |
| Score | −1 unless isMe/admin/finished (`canSeeScore`); `Place` always visible | `GameStateMapper.cs:922` `GameStateMapper.cs:937-938` |
| Personal logs, `ScoreSource`, `LvlUpPoints`, `MoveListPage`, `DirectMessages` (from `WebMessages`), `MediaMessages`, ARAM reroll counters | isMe-gated | `GameStateMapper.cs:945-963` |
| `ScoreBreakdown` (multipliers + per-source entries) | isMe/admin/finished | `GameStateMapper.cs:967-980` |
| Predictions | owner always; **everyone at game end** with correctness + actual character/avatar; Madara's owner list is always empty | `GameStateMapper.cs:213-237` |
| `DeathNote` / `PortalGun` / `ExploitState` / `TsukuyomiState`, Darksci/Gleb/Dopa choice flags | isMe-gated blocks on the player DTO | `GameStateMapper.cs:236-330` |
| Баг viewer | sees `IsExploitable` / `IsExploitFixed` markers on every player | `GameStateMapper.cs:133-134` `GameStateMapper.cs:333-337` |
| TheBoys viewer | marked enemy `PlayerDto`s alone receive `IsTheBoysSupTarget=true`; a marked target's own projection and spectators receive false, so Butcher's choice stays secret | `GameStateMapper.cs:137-145,345-351`; `GameStateDto.cs:100-108` |
| Widget states (`PassiveAbilityStates`) | entire per-passive switch runs only for isMe; keyed on `PassiveName` | `GameStateMapper.cs:340-347` |
| DooM Guy state | owner-only widget contains active/options/nests/BFG/Chainsaw; Let's Roll viewers receive empty character catalogs so predictions cannot be reconstructed client-side | `GameStateMapper.cs:121-122, 350-369` |
| Эрен state | owner-only `ErenStateDto`: gained Rage, total losses, Titan/Tatake audio serials, Rumbling result, and an aggregation of all per-player hatred marks | `GameStateDto.cs:775-793` `GameStateMapper.cs:349-370` |
| Мадара normal state | empty prediction catalogs/list, empty Skill/Moral/target and zero resist/quality texts; no character-specific DTO was added | `GameStateMapper.cs:123-126,215-239,965-978` |
| Вечное Цукуеми final state | final player-scoped DTO only: non-Madara requester becomes alive/place 1 with projected place history, exactly-needed bonus source + one synthetic win; Madara gets real standings and five synthetic skips. A spectator gets only `Результат игры скрыт.` with scores/places/history/fights/predictions cleared. Real game state is untouched | `GameStateMapper.cs:998-1123`; `Madara.cs:219-259` |
| Marks on me (`SellerMark`, TheBoys virus/moral-block, cancer, cat, Johan pawn, Geralt monster type) | mapped after the switch **onto the affected player's own card**; Butcher sup is deliberately excluded (owner-only row marker above) | `GameStateMapper.cs:817-893` |
| Global logs | admin raw; others get `StripHiddenLogs` (removes `HiddenGlobalLogSnippets`; additionally strips Kira-related snippets for viewers with passive "Гений") | `GameStateMapper.cs:116-117` `GameStateMapper.cs:1118-1138` |
| Fight log | hidden-from-non-admin entries filtered out; non-participants get `ScopeFightEntry` — outcome/participants/drops kept, every numeric zeroed, `TotalPointsWon` reduced to sign | `GameStateMapper.cs:127-131` `GameStateMapper.cs:1051-1114` |
| Full chronicle (Летопись) | built only when finished; usernames replaced by character names | `GameStateMapper.cs:155-158` `GameStateMapper.cs:1145-1192` |
| Newly unlocked achievements | requesting player only, finished games | `GameStateMapper.cs:161-186` |

Draft options are serialized only for the requesting player during the draft phase; first option cost 0, others 5, and Madara's hidden passive is filtered before the option DTO is built (`GameStateMapper.cs:94-115`). Avatars are rewritten to local /art/avatars when the file exists (`GetLocalAvatarUrl`). The character catalog for prediction dropdowns loads once from characters.json, excluding negative tiers and "Выдуманный персонаж" (`GameStateMapper.cs:49-68`).

After mapping, `PopulateCustomLeaderboard` adds the per-viewer leaderboard annotations: the same `CustomLeaderBoardAfterPlayer` / `CustomLeaderBoardBeforeNumber` strings the Discord leaderboard renders, converted from Discord markdown/emoji to HTML, plus the `IsInMyHarmRange` flag from speed-quality range vs place distance (`WebGameService.cs:167-199`; emoji map `WebGameService.cs:99-137`; `ConvertDiscordToWeb` `WebGameService.cs:140-161`). Madara never receives a Harm-range flag (`WebGameService.cs:171-176`). Both REST (`WebGameService.cs:86`) and SignalR (`GameNotificationService.cs:169` `GameNotificationService.cs:189`) run it.

## 8. `WebGameService` — the bridge into game logic

Web actions operate on the **same objects and mostly the same handlers as Discord buttons** (`WebGameService.cs:18-24`). Pattern: `FindGameAndPlayer` (`WebGameService.cs:381-386`), validate, then mutate or delegate. `CanAct` = not ready and not skipping (`WebGameService.cs:1072-1075`).

| Web action | Path into game logic | Anchor |
|---|---|---|
| Attack | `HandleAttack(player, null, targetPlace)` — `IsAutoMove` is temporarily forced so the handler reads the numeric botChoice instead of Discord component data; on refusal the message is popped from `WebMessages` and returned as the error | `WebGameService.cs:390-420` |
| Block | direct: `Спарта` → "Спартанцы не капитулируют!!", `Aggress` → "I. WONT. STOP."; Dopa `Макро` registers block as one of two actions via `WhoToAttackThisTurn`; else block+ready, log "Вы поставили блок" | `WebGameService.cs:422-470` |
| AutoMove | direct: auto-move+ready + phrase log | `WebGameService.cs:472-488` |
| ChangeMind | direct: reset ready/block/targets, strike the previous log line through | `WebGameService.cs:490-514` |
| ConfirmSkip / ConfirmPredict | set `ConfirmedSkip` / `ConfirmedPredict` | `WebGameService.cs:516-536` |
| LevelUp | `HandleLvlUp(player, null, statIndex)` (same `IsAutoMove` trick) | `WebGameService.cs:538-553` |
| MoralToPoints / MoralToSkill | `HandleMoralForScore` / `HandleMoralForSkill` (min 5 / min 1 moral) | `WebGameService.cs:555-575` |
| DemandContractReward | full Geralt invoice logic inline (coins, displeasure, advance; ≥11 displeasure → death by "Вилы разъяренной толпы", −500) | `WebGameService.cs:577-663` |
| Predict | upsert into `player.Predict` | `WebGameService.cs:665-681` |
| AramReroll / AramConfirm | `HandlePassiveRoll` (slots 1-4) / `HandleBasicStatRoll` (slot 5); confirm sets `IsAramRollConfirmed` | `WebGameService.cs:683-715` |
| DeathNoteWrite / ShinigamiEyes | direct Kira state writes (once per round; eyes cost 25 moral) | `WebGameService.cs:719-764` |
| DarksciChoice / DopaChoice / YoungGleb | Darksci direct; Dopa validates the tactic (Стомп, Фарм, Доминация, Роум) then `ApplyDopaChoice`; Gleb transform copies the "Молодой Глеб" character sheet in place | `WebGameService.cs:768-846` |
| DoomRoll / DoomChainsaw | round-1 roll-mode activation / validated pending Chainsaw passive choice | `WebGameService.cs:870-889` |
| FinishGame | `EndGame` (bot substitution — same as the Discord Завершить Игру button) | `WebGameService.cs:853-862` |
| ActivateShen / DeactivateShen / RewriteHistory | direct Salldorum state writes (rewrite steals 1 point per round-loser, +2 psyche, +2 buffered justice, cola time-travel pickup) | `WebGameService.cs:866-976` |

Madara action gates are enforced server-side, not only hidden in Vue: round-8/sealed attack attempts and targeting sealed Madara are rejected by the shared `HandleAttack` path (`GameReactions.cs:677-708`); `ChangeMind` rejects sealed Madara; `LevelUp` and `Predict` reject every Madara request (`WebGameService.cs:505-515,553-562,687-699`).

**Game creation** (`CreateGame`, `WebGameService.cs:206-275`): rolls a full 6-bot game via `HandleCharacterRoll`, replaces the first bot with the creator and re-snapshots a possible DooM Guy loadout from the creator account (`WebGameService.cs:224-235`), then creates `GameClass` + Nanobot. Draft mode places a newcomer-protected DooM Guy option first/free (`WebGameService.cs:242-260`). `JoinWebGame` likewise reinitializes a DooM seat from the joining account (`WebGameService.cs:280-313`); `DraftSelect` always builds a fresh bridge and initializes the selected account loadout (`WebGameService.cs:317-381`). `CreateTestGame` = `CreateGame` + forced character, swapping conflicts if needed (`WebGameService.cs:995-1111`).

## 9. Replays

- **Capture**: `CaptureRound` snapshots every round — global logs, deep-copied fight log, and **each player's own full-visibility DTO** plus the leaderboard as that player saw it (`ReplayService.cs:41-84`).
- **Finalize**: on game end `BuildReplayData` assembles `ReplayDataDto` (8-char `ReplayHash`, player summaries, all rounds, full chronicle) (`ReplayService.cs:88-133`; DTO shapes `ReplayDto.cs:8-63`), saved as camelCase JSON into the Replays folder (`ReplayService.cs:20` `ReplayService.cs:137-144`).
- **Madara privacy exception**: an active `Вечное Цукуеми` has six incompatible final views, so finalization/save is skipped rather than storing one viewer's projection or the authoritative ending (`CheckIfReady.cs:795-807`).
- **Serve**: REST endpoints in §3; the AI story is backfilled into the replay file when generated (`AttachStory`, `ReplayService.cs:173-188`).

## 10. Mini-games

- **Blackjack "Игра 21"** — for dead players of a running game, keyed by the main gameId. `BlackjackService` holds tables in memory (`BlackjackService.cs:14`), max 5 seats (`BlackjackService.cs:49-75`), stale tables cleaned every 5 min (`BlackjackService.cs:38-44`); winners earn the right to compose a message from a fixed word list (`blackjack_words.json`, loaded at `BlackjackService.cs:21-35`) which lands in the living players' global logs (§4). The web client auto-joins it when the player dies to Kira (WEB-CLIENT.md §6).
- **Battleship (Морской Бой)** — fully standalone 1-v-1 minigame with its own string game IDs, vs-bot creation (`BattleshipService.cs:51-74`), lobby (`BattleshipService.cs:30-49`), phase/fleet/weapon/summon logic under the Battleship folder, in-memory games with 5-min cleanup (`BattleshipService.cs:13-26`). Surface = the 22 hub methods in §4; no REST, no Discord surface. Game log entries are `LogEntry { Text, VisibleTo }` (`BattleshipModels.cs`, `game.AddLog`/`game.AddLogFor`); `ToDto` filters by viewer — `VisibleTo == null` is shared, otherwise only that player sees it, spectators see everything — and the client still receives a plain `string[]` (used for Мачта warnings, Драккар freeze messages). The battleship `CellDto` carries `IsBurnResistMarked` (burn_resist ship survived fire/explosion — dark-green cell on both views); explosions go through `BattleshipGameEngine.ExplodeArea` (barge radius 2, Brander radius 1) which writes miss/kill statuses visible to both players. `BattleshipPlayerDto` carries `BranderUsed` (Brander is outside the 4-slot summon limit, once per match) and `ShipDto` carries `Regions` — the client gates summon availability on fleet regions (Ram⇒West, Scout⇒East, PirateBoat⇒South), mirroring the server checks in `DeploySummon`. Maneuvering Double activation is per-ship: `ShipDto.HasManeuvered` (the player-level `ManeuveringDoubleUsed` field was removed); on the own board killed decks derive from the live `Deck` state (`IsDeckDestroyedAt`) so the mark follows the ship after a manual move, while the fog view keeps the `WasShipHit` snapshot at the old cells. `CellDto.IsDodgeMarked` (Юркая единичка dodged a ballista — салатовый mark, both views) and `SummonTrail` now includes the summon's spawn/re-entry cell.

## 11. LLM services (outbound Anthropic API)

Two independent callers, both model `claude-haiku-4-5-20251001` (`GameStoryService.cs:27` `ClaudeHaikuService.cs:20`), key from `AnthropicApiKey` (`Config.cs:28`):

- **`GameStoryService`** (`GameStoryService.cs:19-39`; limits `GameStoryService.cs:26-29`): fire-and-forget post-game narrative, max 1024 tokens. Triggered from the finish path (§6); skips missing key and **bot-only games** (`GameStoryService.cs:45-52`); renders markdown→HTML, keeps the latest 50 stories in memory (`StoreStory`, `GameStoryService.cs:95-109`), pushes eventType `GameStory` to the room (`GameStoryService.cs:76-77`), re-serves on join via `GetStory` (`GameHub.cs:134-137`), and backfills the replay file (`OnStoryGenerated`, `GameNotificationService.cs:70-74`). Raw API call: `CallClaudeApi` (`GameStoryService.cs:327-361`).
- **`ClaudeHaikuService`** (`ClaudeHaikuService.cs:14-29`): single method `GenerateWitcherHintAsync` — a Russian Geralt-flavored hint about a hidden enemy, max 100 tokens, 5 s timeout, returns null on any failure so the caller falls back to static hints (`ClaudeHaikuService.cs:37-90`). Called from Geralt's Медитация passive for human players only (see DISCORD-INTERFACE.md §10). `Disabled` is forced true in `--sim` (`Program.cs:61-66`) so simulations never spend credits.

## 12. Discord profile widget (the one OAuth-verified flow)

- The Discord command `*widget_s` posts an OAuth authorize link-button (DISCORD-INTERFACE.md §9); the user approves and Discord redirects to the SPA /widget page with an access token in the URL fragment, which the page POSTs to the sync endpoint (§3).
- `TryVerifyAndAuthorizeAsync` resolves the real user via users/@me with the Bearer token, sets `WidgetAuthorized` on the account, then syncs (`DiscordWidgetService.cs:116-143`).
- `SyncAsync` PATCHes the user's Discord profile identity (application ClientId `901706293977432124`, `DiscordWidgetService.cs:18`, bot-token auth) with the 4 most recent characters' avatars and win rates from `CharacterStatistics` (`DiscordWidgetService.cs:36-114`). A 403 clears `WidgetAuthorized` (`DiscordWidgetService.cs:95-100`).
- Unrelated to the `SetPreferWeb` toggle (`GameHub.cs:558-573`) despite the shared "widget/web" vocabulary.

## 13. Per-account localization

`GameHub.SetLanguage` normalizes/persists `ru` or `en`, updates the process locale registry and immediately re-pushes the current personalized state (`GameHub.cs:83-96`). The mapper translates only viewer-owned logs, score sources, direct messages and media after the existing privacy gates; canonical `GameClass` logs and opponents' hidden state are not mutated (`GameStateMapper.cs:1159-1171`). Character/passive DTO text remains canonical until the Vue presentation boundary so prediction/draft values stay valid.

The English catalog is copied to the deployed `DataBase` output and loaded lazily by `GameLocalization` (`GameLocalization.cs:225-258`). Game stories now request paired native RU/EN tagged adaptations (max 1800 tokens) and store both in one replay-safe HTML value (`GameStoryService.cs:26-29, 68-77, 172-196`). Geralt's hint request receives the owner's locale and uses matching static dictionaries on failure (`ClaudeHaikuService.cs:37-65`, `CP:4657-4688`). Full contract: [LOCALIZATION.md](LOCALIZATION.md).

## 14. Known quirks & pitfalls (backend)

- **Asserted-ID auth**: any client can claim any Discord ID (§2). Never build features assuming web identity is verified; the only verified flag is `WidgetAuthorized` (`DiscordWidgetService.cs:141`).
- **REST is a subset** — new player-facing actions must be added to the hub (and usually only the hub); adding them to GameController is optional parity.
- **ARAM has hub/REST methods but no web UI** — AramReroll/AramConfirm are callable and the reroll counters are serialized, but the Vue client never renders an ARAM pick screen (finding **m24** in AUDIT-FINDINGS.md; WEB-CLIENT.md §13).
- `Attack` pushes state even when rejected (`GameHub.cs:228-235`) — the client relies on this to re-sync after refusals; other actions push on success only.
- Finished games are invisible to the 300 ms timer (§6) — if you add finish-time data to the DTO, populate it **before** the finish callback fires or it will never reach clients.
- New per-player web state needs the whole §7 pipeline: DTO member (`GameStateDto.cs`), mapper case with the right isMe/isAdmin gate (`GameStateMapper.cs`), TS mirror + widget (ARCHITECTURE.md §7 steps 11-14). Missing the gate = information leak to opponents/spectators.
- `KOTGH_PORT` is env-only (`Program.cs:198`); Config carries just the bot token and the Anthropic key (`Config.cs:9-28`).
- Discord IDs cross the hub as strings; the server parses with `ulong.TryParse` (`GameHub.cs:61-66`) — never let the client send them as JS numbers.
- Blackjack tables and battleship games are in-memory only (no persistence, 5-min stale cleanup) — restarts drop them.
