# King of the Garbage Hill — Web Backend (API, SignalR, push, auth)

> Code-verified against the working tree of 2026-07-11 (v4.3.2). Companion docs: [ARCHITECTURE.md](ARCHITECTURE.md) (§1 process topology, §6 state→screen), [WEB-CLIENT.md](WEB-CLIENT.md) (the Vue SPA consuming this surface), [DISCORD-INTERFACE.md](DISCORD-INTERFACE.md) (the other frontend), and [DAILY-QUESTS.md](DAILY-QUESTS.md) (the quest economy). This doc is the deep catalog of the ASP.NET Core surface: every endpoint, hub method, pushed event, and visibility rule.

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

**Persistence**: accounts live in a `ConcurrentDictionary` (`UserAccounts.cs:17`) loaded at construction; `IsPlaying` is force-reset for everyone at startup (`ClearPlayingStatus`, `UserAccounts.cs:61-65`). The guarded 60 s fallback flush and reward-critical writes both route through boolean `SaveAccount`, which locks the account across serialization/write and lets user-triggered transactions restore state when a write fails (`UserAccounts.cs:113-139`). Storage reports write success after replacing the canonical JSON through a unique same-directory temporary file; startup loads only `discordAccount-{numericId}.json`, ignoring temporary/backup artifacts (`UsersDataStorage.cs:28-80,83-116`).

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

`GameHub` (`GameHub.cs:20`). Connection life-cycle: `OnConnectedAsync` logs only (`GameHub.cs:41-45`); `OnDisconnectedAsync` unregisters the connection (`GameHub.cs:47-55`). Rooms: SignalR group `game-{gameId}` joined in `JoinGame` (`GameHub.cs:99-105`), battleship group `bs-{gameId}` (`GameHub.cs:1172-1253`). Connection→player and connection→game maps live in GameNotificationService, not the hub (`GameNotificationService.cs:32-35`).

Every game-action method starts with `GetDiscordId()` (`GameHub.cs:1682-1687`) and replies event `Error` = "Not authenticated. Call Authenticate first." if unset (`SendNotAuthenticated`, `GameHub.cs:1689-1692`). Action methods reply `ActionResult {action, success, error}` and then push fresh personal state via `PushStateToPlayer` (`GameHub.cs:1694-1702`) — on success only, **except `Attack` which pushes unconditionally** (`GameHub.cs:228-235`).

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
| `FinishGame(gameId)` | leave mid-game → replaced by bot via `EndGame` (`WebGameService.cs:922-930`) | `GameHub.cs:543-550` |
| `SetPreferWeb(gameId, preferWeb)` | sets `player.PreferWeb` directly — suppresses the player's Discord DMs (see DISCORD-INTERFACE.md §4) | `GameHub.cs:558-573` |
| `RequestGameState(gameId)` | on-demand push (player-scoped, falls back to spectator) | `GameHub.cs:1021-1041` |
| `RequestLobbyState()` | replies `LobbyState` | `GameHub.cs:1043-1047` |

**Core round actions** (all delegate to WebGameService, §8): `Attack(gameId, targetPlace)` `GameHub.cs:228-235`; `Block` `GameHub.cs:239-246`; `DoAutoMove` `GameHub.cs:250-257`; `ChangeMind` `GameHub.cs:261-268`; `ConfirmSkip` `GameHub.cs:272-279`; `ConfirmPredict` `GameHub.cs:283-290`; `LevelUp(gameId, statIndex)` `GameHub.cs:294-301`; `MoralToPoints` `GameHub.cs:305-312`; `MoralToSkill` `GameHub.cs:316-323`; `DemandContractReward(gameId, demandType)` (Geralt; demandType = previous|next) `GameHub.cs:327-334`; `Predict(gameId, targetPlayerId, characterName)` `GameHub.cs:338-345`; `AramReroll(gameId, slot)` `GameHub.cs:349-356`; `AramConfirm` `GameHub.cs:360-367`; `DraftSelect(gameId, characterName)` `GameHub.cs:373-380`.

**Character-specific actions:** `DarksciChoice(gameId, isStable)`; `YoungGleb(gameId)`; DooM Guy `DoomRoll(gameId)` / `DoomChainsaw(gameId, passiveName)` (`GameHub.cs:386-423`); `DopaChoice(gameId, tactic)`; `DeathNoteWrite(gameId, targetPlayerId, characterName)` (Kira); `ShinigamiEyes(gameId)`; Salldorum's `ActivateShen(gameId, position)`, `DeactivateShen(gameId)`, `RewriteHistory(gameId, roundNumber)` (`GameHub.cs:426-492`).

**Meta (account-level, no game):** DooM Guy `RequestDoomFortress()` → `DoomFortressState` and `EquipDoomModule(stage, slotIndex, moduleName)` → validates category, ownership and 0–3 slot, then swaps/sets without permitting a user-created empty slot (`GameHub.cs:578-646`). `RequestQuests()` durably initializes/migrates the UTC day and snapshots the typed bilingual quests, 2/3 + 3/3 milestones, reroll allowance, streak/5-of-7 journey, ZBS/box inventory, Rare+ pity, base odds and any resumable opening (`GameHub.cs:649-682,728-803`). `RerollDailyQuest(questId)` validates one unfinished random-lane card, saves the recomputed replacement before sending it and restores the full quest/economy snapshot on failure (`GameHub.cs:685-725`). Full quest semantics: [DAILY-QUESTS.md](DAILY-QUESTS.md). Current clients call `OpenLootBoxV2()`: debit/roll/credit are saved before `LootBoxOpened`, rollback on save failure, and remain resumable until `AcknowledgeLootBox(openingId)` also saves. Cached pre-V2 clients retain one-shot `OpenLootBox()` compatibility (`GameHub.cs:811-899`). `RequestAchievements()` snapshots the 33-entry board, progress, queue and current-catalog reward totals; locked secret names/descriptions/characters and semantic IDs are masked. Selective `AcknowledgeAchievements(achievementIds)` and legacy `ClearNewAchievements()` both restore the queue and reject retryably if persistence fails (`GameHub.cs:903-1018`). Full Achievement/Loot semantics: [ACHIEVEMENTS.md](ACHIEVEMENTS.md).

**Blackjack (dead-player mini-game, §10):** `BlackjackJoin` `GameHub.cs:1051-1067`; `BlackjackHit` `GameHub.cs:1069-1082`; `BlackjackStand` `GameHub.cs:1084-1097`; `BlackjackNewRound` `GameHub.cs:1099-1112`; `BlackjackSendMessage(gameId, words[])` `GameHub.cs:1114-1132` — the composed message is injected into the **main game's global logs** as `[Шинигами] {author}: "{message}"` and broadcast (`InjectGlobalLogMessage`, `GameHub.cs:1152-1170`). State pushes are personalized per seated player (`PushBlackjackState`, `GameHub.cs:1134-1150`).

**Battleship (standalone mini-game, §10):** `RequestBattleshipLobby` `GameHub.cs:1172-1176`; `CreateBattleshipGame` `GameHub.cs:1178-1198`; `JoinBattleshipWebGame` `GameHub.cs:1200-1218`; `LeaveBattleshipWebGame` `GameHub.cs:1220-1234`; `JoinBattleshipGame` (room + state, spectator-capable) `GameHub.cs:1236-1248`; `LeaveBattleshipGame` `GameHub.cs:1250-1253`; `BattleshipConfirmReady` `GameHub.cs:1255-1268`; `BattleshipSelectArmy(gameId, faction)` `GameHub.cs:1270-1283`; `BattleshipSelectFleet(gameId, selections)` `GameHub.cs:1285-1306`; `BattleshipPlaceShip(gameId, shipId, row, col, orientation)` `GameHub.cs:1308-1321`; `BattleshipRemoveShip` `GameHub.cs:1323-1336`; `BattleshipConfirmPlacement` `GameHub.cs:1338-1351`; `BattleshipShoot(gameId, row, col)` `GameHub.cs:1353-1386`; `BattleshipShootOwnBoard` `GameHub.cs:1388-1420`; `BattleshipSelectWeapon(gameId, weaponType, shotType)` `GameHub.cs:1422-1429`; `BattleshipDeploySummon(gameId, summonType, col)` `GameHub.cs:1431-1444`; `BattleshipDeployPendingSummon` `GameHub.cs:1446-1459`; `BattleshipManualMove(gameId, shipId, direction, distance)` `GameHub.cs:1461-1474`; `BattleshipSetCursedBoatDirection` `GameHub.cs:1476-1489`; `BattleshipForfeit` `GameHub.cs:1491-1504`; `RequestBattleshipState` `GameHub.cs:1506-1516`; `RequestShipCatalog` `GameHub.cs:1518-1522`. Note battleship game IDs are **strings**. Pushes: personalized to both players + spectator DTO to the room minus players (`PushBattleshipStateToAll`, `GameHub.cs:1524-1553`).

## 5. Server→client events

| Event | Payload | Emitted when | Anchor |
|---|---|---|---|
| `Authenticated` | success, discordId (string), playerType, lastPlayedCharacter | after `Authenticate` | `GameHub.cs:66-81` |
| `WebAccountCreated` | discordId (string), username | after `RegisterWebAccount` | `GameHub.cs:152-167` |
| `GameCreated` / `GameJoined` | gameId | create/join | `GameHub.cs:194` `GameHub.cs:220` `GameHub.cs:535` |
| `GameState` | full GameStateDto (§6, shape `GameStateDto.cs:8-58`) | on join/request, after each action (actor only), timer broadcast, finish | `GameHub.cs:113,1694-1702` `GameNotificationService.cs:165-172` |
| `ActionResult` | action, success, error | after every hub action | e.g. `GameHub.cs:234` |
| `Error` | string | invalid input / not authenticated / not found | `GameHub.cs:66-71,1689-1692` |
| `GameEvent` | envelope `{eventType, data}` | eventType `RoundChanged` on round flip (`GameNotificationService.cs:258-261`); `GameFinished` (`GameNotificationService.cs:83`); `GameStory` (`GameStoryService.cs:76-77`; re-sent on join, `GameHub.cs:134-137`); `BlackjackMessage` (`GameHub.cs:1152-1170`) | |
| `LobbyState` | LobbyStateDto | on request | `GameHub.cs:1043-1047` |
| `QuestState` | bilingual quests, daily/mastery/weekly receipts, reset/server times, reroll allowance, streak/ZBS/inventory, pity/odds and optional resumable opening | on request or successful reroll | `GameHub.cs:649-803`; DTO `GameStateDto.cs:1032-1082` |
| `LootBoxOpened` | immutable opening result: ID, rarity/ZBS, current balance/inventory, opening pity and pity-upgrade flag | durable new open or retry before acknowledgement | `GameHub.cs:811-869`; DTO `GameStateDto.cs:1084-1103` |
| `AchievementBoard` | all live entries plus unlocked/catalog totals and unacknowledged live IDs | on request | `GameHub.cs:903-947`; DTO `GameStateDto.cs:1105-1139` |
| `CharacterList` | name/avatar/tier list | on request | `GameHub.cs:501-504` |
| `DoomFortressState` | four stages: slots, unlocked module definitions, remaining reward count/chance | request or successful equip | `GameHub.cs:578-644` |
| `BlackjackState` | personalized table state | after any blackjack action | `GameHub.cs:1051-1170` |
| `BattleshipLobby`, `BattleshipGameCreated`, `BattleshipGameJoined`, `BattleshipState`, `BattleshipEvent` (eventType `ShotResult`), `ShipCatalog` | battleship DTOs | battleship flow | `GameHub.cs:1172-1218` `GameHub.cs:1236-1248` `GameHub.cs:1353-1420` `GameHub.cs:1518-1553` |

## 6. Push model — who sends `GameState` when

1. **Per-action**: the hub pushes to the **acting player's connections only** right after each action (`PushStateToPlayer`, `GameHub.cs:1694-1702`).
2. **Timer**: GameNotificationService polls every 300 ms (`GameNotificationService.cs:97-103`) and broadcasts a game when the round number changed, elapsed time moved > 0.5 s, or any player's `IsReady` flipped (`PushUpdates`, `GameNotificationService.cs:218-269`; triggers at `GameNotificationService.cs:242-247`); a `RoundChanged` event accompanies round flips (`GameNotificationService.cs:258-261`). **Finished games are skipped by the timer** (`GameNotificationService.cs:236-240`) — their final state would race the lootbox/achievement stamping in the finish path.
3. **On finish**: the game loop fires `OnGameFinished`, which does the final personalized broadcast, emits `GameFinished`, conditionally kicks off story generation, and drops snapshots/room tracking (kept while a Blackjack table is still open) (`GameNotificationService.cs:78-96`). An active Madara `Вечное Цукуеми` skips the shared story because it would reveal the real ending (`GameNotificationService.cs:82-88`).
4. **Broadcast shape**: `BroadcastGameState` sends a **personalized DTO to every player with a web connection**, then one **spectator DTO** to the remaining connections in the room (`GameNotificationService.cs:178-208`). `SendGameStateToPlayer` is the single-player variant used by game logic (`GameNotificationService.cs:163-171`).
5. Replay saving rides the same finish path: `OnReplaySave` builds + persists the replay and appends its hash to each human's `ReplayHashes` (`GameNotificationService.cs:50-67`). Activated `Вечное Цукуеми` games never invoke that shared callback (`CheckIfReady.cs:817-825`).

## 7. State mapping & hidden information (`GameStateMapper`)

`GameStateMapper.ToDto(game, requestingPlayer = null)` (`GameStateMapper.cs:80-204`) produces the per-viewer projection; null = spectator; `isAdmin` means PlayerType 2 (`GameStateMapper.cs:82`). Top-level shape: `GameStateDto.cs:8-58`; per-player `PlayerDto`: `GameStateDto.cs:62-133`.

Visibility rules (the exact reason the web can't leak hidden info):

| Data | Rule | Anchor |
|---|---|---|
| Opponent character | non-admin, unfinished → name "???", unknown-avatar, stats −1 sentinels, skill/moral "?", empty passives | `GameStateMapper.cs:828-853` |
| Own/admin/finished character | real stats + resists and quality-bonus texts (resists/bonuses remain own-only) | `GameStateMapper.cs:855-900` |
| Passive list | every viewer, including the owner/admin/finished view, receives only `Visible` passives; a hidden passive is absent until its mechanic sets `Visible=true` (TheBoys/DooM unlocks, Итачи/Кратос resurrection, Sakura top-3 win). `Вечное Цукуеми` is omitted permanently | `GameStateMapper.cs:902-915`; reveals `CharacterPassives.cs:5757-5782`, `CheckIfReady.cs:533-543` |
| Score | −1 unless isMe/admin/finished (`canSeeScore`); `Place` always visible | `GameStateMapper.cs:1060-1081` |
| Personal logs, `ScoreSource`, `LvlUpPoints`, `MoveListPage`, `DirectMessages` (from `WebMessages`), `MediaMessages`, ARAM reroll counters | isMe-gated | `GameStateMapper.cs:1053-1093` |
| `ScoreBreakdown` (multipliers + per-source entries) | isMe/admin/finished; the finished projection appends still-current entries created after the round snapshot, so final Mitsuki/Осьминожка debits are not lost | `GameStateMapper.cs:1108-1129` |
| Predictions | owner always; **everyone at game end** with correctness + actual character/avatar; Madara's owner list is always empty | `GameStateMapper.cs:226-251` |
| `DeathNote` / `PortalGun` / `ExploitState` / `TsukuyomiState`, Darksci/Gleb/Dopa choice flags | isMe-gated blocks on the player DTO | `GameStateMapper.cs:253-359` |
| Баг viewer | sees `IsExploitable` / `IsExploitFixed` markers on every player | `GameStateMapper.cs:358-363` |
| TheBoys viewer | marked enemy `PlayerDto`s alone receive `IsTheBoysSupTarget=true`; a marked target's own projection and spectators receive false, so Butcher's choice stays secret | `GameStateMapper.cs:356-358`; `GameStateDto.cs:100-113` |
| Widget states (`PassiveAbilityStates`) | entire per-passive switch runs only for isMe; keyed on `PassiveName`; contains owner state only, no generic target-facing `…OnMe` fields | `GameStateMapper.cs:360-823`; DTO `GameStateDto.cs:562-603` |
| DooM Guy state | owner-only widget contains active/options/nests/BFG/Chainsaw; Let's Roll viewers receive empty character catalogs so predictions cannot be reconstructed client-side | `GameStateMapper.cs:130-133,396-425` |
| Эрен state | owner-only `ErenStateDto`: gained Rage, **round-10 Rumbling losses**, Titan/Tatake audio serials, Rumbling result, and an aggregation of all per-player hatred marks | `GameStateDto.cs:746-764` `GameStateMapper.cs:370-394` |
| Мадара normal state | empty prediction catalogs/list, empty Skill/Moral/target and zero resist/quality texts; no character-specific DTO was added | `GameStateMapper.cs:130-133,226-251,887-900` |
| Вечное Цукуеми final state | final player-scoped DTO only: non-Madara requester becomes alive/place 1 with projected place history, exactly-needed bonus source + one synthetic win; Madara gets real standings and five synthetic skips. A spectator gets only `Результат игры скрыт.` with scores/places/history/fights/predictions cleared. Real game state is untouched | `GameStateMapper.cs:920-1045`; `Madara.cs:219-259` |
| Cross-character marks | not serialized to the affected player's card. Seller/Kотики/TheBoys/Toxic Mate/Йохан/Геральт owners see their own targets through owner widgets or viewer-scoped leaderboard annotations; only explicitly public objects (e.g. Ziggurat, DooM nests) remain global | `GameStateMapper.cs:349-358,360-823`; `GameUpdateMess.cs:219-811` |
| Global logs | admin raw; others get `StripHiddenLogs` (removes `HiddenGlobalLogSnippets`; additionally strips Kira-related snippets for viewers with passive "Гений") | `GameStateMapper.cs:119-126,1251-1273` |
| Fight log | hidden-from-non-admin entries filtered out; non-participants get `ScopeFightEntry` — outcome/participants/drops kept, every numeric zeroed, `TotalPointsWon` reduced to sign | `GameStateMapper.cs:136-142,1182-1249` |
| Full chronicle (Летопись) | built only when finished; usernames replaced by character names | `GameStateMapper.cs:167-171,1275-1327` |
| Newly unlocked achievements | requesting player only, finished games; full paired metadata/rewards from a detached finish-time account snapshot, so another tab cannot mutate the enumerated queue | `CheckIfReady.cs:740-743`; `AchievementClass.cs:592-608`; `GameStateMapper.cs:173-208` |
| Locked secret achievement board entries | name/nameRu = `???`, exact descriptions replaced by hints, character list empty, ID changed to stable SHA-256-derived opaque `hidden-…`; full metadata after unlock | `GameHub.cs:1645-1679` |

Draft options are serialized only for the requesting player during the draft phase; first option cost 0, others 5, and every hidden passive is filtered before the option DTO is built (`GameStateMapper.cs:95-117`). Avatars are rewritten to local /art/avatars when the file exists (`GetLocalAvatarUrl`). The character catalog for prediction dropdowns loads once from characters.json, excluding negative tiers and "Выдуманный персонаж" (`GameStateMapper.cs:45-68`).

After mapping, `PopulateCustomLeaderboard` adds the per-viewer leaderboard annotations: the same `CustomLeaderBoardAfterPlayer` / `CustomLeaderBoardBeforeNumber` strings the Discord leaderboard renders, converted from Discord markdown/emoji to HTML, plus the `IsInMyHarmRange` flag from speed-quality range vs place distance (`WebGameService.cs:167-199`; emoji map `WebGameService.cs:99-137`; `ConvertDiscordToWeb` `WebGameService.cs:140-161`). Madara never receives a Harm-range flag (`WebGameService.cs:171-176`). Both REST (`WebGameService.cs:86`) and SignalR (`GameNotificationService.cs:169` `GameNotificationService.cs:189`) run it.

## 8. `WebGameService` — the bridge into game logic

Web actions operate on the **same objects and mostly the same handlers as Discord buttons** (`WebGameService.cs:18-24`). Pattern: `FindGameAndPlayer` (`WebGameService.cs:413-425`), validate, then mutate or delegate. `CanAct` = not ready and not skipping (`WebGameService.cs:1144-1151`).

| Web action | Path into game logic | Anchor |
|---|---|---|
| Attack | `HandleAttack(player, null, targetPlace)` — `IsAutoMove` is temporarily forced so the handler reads the numeric botChoice instead of Discord component data; on refusal the message is popped from `WebMessages` and returned as the error | `WebGameService.cs:427-457` |
| Block | direct: `Спарта` → "Спартанцы не капитулируют!!", `Aggress` → "I. WONT. STOP."; Dopa `Макро` registers block as one of two actions via `WhoToAttackThisTurn`; else block+ready, log "Вы поставили блок" | `WebGameService.cs:459-507` |
| AutoMove | direct: auto-move+ready + phrase log | `WebGameService.cs:509-525` |
| ChangeMind | direct: reset ready/block/targets, strike the previous log line through | `WebGameService.cs:527-552` |
| ConfirmSkip / ConfirmPredict | set `ConfirmedSkip` / `ConfirmedPredict` | `WebGameService.cs:554-574` |
| LevelUp | `HandleLvlUp(player, null, statIndex)` (same `IsAutoMove` trick) | `WebGameService.cs:576-596` |
| MoralToPoints / MoralToSkill | `HandleMoralForScore` / `HandleMoralForSkill` (min 5 / min 1 moral) | `WebGameService.cs:598-620` |
| DemandContractReward | full Geralt invoice logic inline (coins, displeasure, advance; ≥11 displeasure → death by "Вилы разъяренной толпы", −500) | `WebGameService.cs:622-708` |
| Predict | upsert into `player.Predict` | `WebGameService.cs:710-730` |
| AramReroll / AramConfirm | `HandlePassiveRoll` (slots 1-4) / `HandleBasicStatRoll` (slot 5); confirm sets `IsAramRollConfirmed` | `WebGameService.cs:732-766` |
| DeathNoteWrite / ShinigamiEyes | direct Kira state writes (once per round; eyes cost 25 moral) | `WebGameService.cs:768-815` |
| DarksciChoice / DopaChoice / YoungGleb | Darksci direct; Dopa validates the tactic (Стомп, Фарм, Доминация, Роум) then `ApplyDopaChoice`; Gleb transform copies the "Молодой Глеб" character sheet in place | `WebGameService.cs:817-895` |
| DoomRoll / DoomChainsaw | round-1 roll-mode activation / validated pending Chainsaw passive choice | `WebGameService.cs:897-920` |
| FinishGame | `EndGame` (bot substitution — same as the Discord Завершить Игру button) | `WebGameService.cs:922-933` |
| ActivateShen / DeactivateShen / RewriteHistory | direct Salldorum state writes (rewrite steals 1 point per round-loser, +2 psyche, +2 buffered justice, cola time-travel pickup) | `WebGameService.cs:935-1050` |

Madara action gates are enforced server-side, not only hidden in Vue: round-8/sealed attack attempts and targeting sealed Madara are rejected by the shared `HandleAttack` path (`GameReactions.cs:688-735`); `ChangeMind` rejects sealed Madara; `LevelUp` and `Predict` reject every Madara request (`WebGameService.cs:527-552,576-596,710-730`).

**Game creation** (`CreateGame`, `WebGameService.cs:206-275`): rolls a full 6-bot game via `HandleCharacterRoll`, replaces the first bot with the creator and re-snapshots a possible DooM Guy loadout from the creator account (`WebGameService.cs:224-235`), then creates `GameClass` + Nanobot. Draft mode places a newcomer-protected DooM Guy option first/free (`WebGameService.cs:242-260`). `JoinWebGame` likewise reinitializes a DooM seat from the joining account (`WebGameService.cs:280-313`); `DraftSelect` serializes on the game monitor, rejects duplicates before any debit, then validates/persists a paid pick under the account monitor and restores it on failure before replacing the bridge (`WebGameService.cs:328-420`). `CreateTestGame` = `CreateGame` + forced character, swapping conflicts if needed (`WebGameService.cs:1017-1133`).

## 9. Replays

- **Round contract (format v2)**: `ReplayDataDto.ReplayFormatVersion = 2`; each logical `ReplayRoundDto` owns **PreFightPlayers** (the action-locked, per-viewer state before that round's fight calculation), the same round's deep-copied `FightLog`/global logs, and one authoritative post-setup `Players` result. The last round also has explicit personal/global settlement suffixes that exclude the already-captured round-11 setup buffer (`ReplayDto.cs:9-17`; shapes `ReplayDto.cs:45-76`; `ReplayService.cs:35-144`). Each player snapshot still contains that player's own full-visibility DTO plus the leaderboard exactly as that player saw it (`ReplayService.cs:301-339`).
- **Capture lifecycle**: `CalculateAllFights` clears the prior web fight log before `BeginRound` (`DoomsdayMachine.cs:173-205`); `CaptureRoundResult` freezes only combat/global logs before `RoundNo++` so Тигр bans, Darksci tilt and other next-turn messages cannot enter that stream (`DoomsdayMachine.cs:1529-1534`). After next-round effects and sorting, `FinalizeRound` remaps result players atomically, including only current **awarded bonus** score entries (not pending regular points for a never-played next round), so score/place/death/breakdown agree while action flags and combat stats still come from **PreFightPlayers** (`ReplayService.cs:82-104,301-342`; `DoomsdayMachine.cs:1784-1794`).
- **Game end**: `CaptureFinalState` refreshes the existing last combat result after final debits/predictions instead of adding a fake round 11. It subtracts per-player and global post-setup baselines and stores only `HandleLastRound` additions, preserving Geralt's pitchfork/winner/final-score logs without replaying round-11 Darksci tilt (`CheckIfReady.cs:812-820`; `ReplayService.cs:106-144,350-364`).
- **Finalize**: on game end `BuildReplayData` assembles `ReplayDataDto` (8-char `ReplayHash`, player summaries, max captured logical round, full chronicle) (`ReplayService.cs:148-195`), saved as camelCase JSON into the Replays folder (`ReplayService.cs:20,200-206`). Legacy JSON deserializes with format version 0 and an empty pre-fight snapshot list; it remains served unchanged and the Vue replay store adapts its boundary snapshots client-side. Its inseparable final raw log buffer is retained for compatibility.
- **Madara privacy exception**: an active `Вечное Цукуеми` has six incompatible final views, so finalization/save is skipped rather than storing one viewer's projection or the authoritative ending (`CheckIfReady.cs:816-825`).
- **Serve**: REST endpoints in §3; the AI story is backfilled into the replay file when generated (`AttachStory`, `ReplayService.cs:236-251`).

## 10. Mini-games

- **Blackjack "Игра 21"** — for dead players of a running game, keyed by the main gameId. `BlackjackService` holds tables in memory (`BlackjackService.cs:14`), max 5 seats (`BlackjackService.cs:49-75`), stale tables cleaned every 5 min (`BlackjackService.cs:38-44`); winners earn the right to compose a message from a fixed word list (`blackjack_words.json`, loaded at `BlackjackService.cs:21-35`) which lands in the living players' global logs (§4). The web client auto-joins it when the player dies to Kira (WEB-CLIENT.md §6).
- **Battleship (Морской Бой)** — fully standalone 1-v-1 minigame with its own string game IDs, vs-bot creation (`BattleshipService.cs:51-74`), lobby (`BattleshipService.cs:30-49`), phase/fleet/weapon/summon logic under the Battleship folder, in-memory games with 5-min cleanup (`BattleshipService.cs:13-26`). Surface = the 22 hub methods in §4; no REST, no Discord surface. Game log entries are `LogEntry { Text, VisibleTo }` (`BattleshipModels.cs`, `game.AddLog`/`game.AddLogFor`); `ToDto` filters by viewer — `VisibleTo == null` is shared, otherwise only that player sees it, spectators see everything — and the client still receives a plain `string[]` (used for Мачта warnings, Драккар freeze messages). The battleship `CellDto` carries `IsBurnResistMarked` (burn_resist ship survived fire/explosion — dark-green cell on both views); explosions go through `BattleshipGameEngine.ExplodeArea` (barge radius 2, Brander radius 1) which writes miss/kill statuses visible to both players. `BattleshipPlayerDto` carries `BranderUsed` (Brander is outside the 4-slot summon limit, once per match) and `ShipDto` carries `Regions` — the client gates summon availability on fleet regions (Ram⇒West, Scout⇒East, PirateBoat⇒South), mirroring the server checks in `DeploySummon`. Maneuvering Double activation is per-ship: `ShipDto.HasManeuvered` (the player-level `ManeuveringDoubleUsed` field was removed); on the own board killed decks derive from the live `Deck` state (`IsDeckDestroyedAt`) so the mark follows the ship after a manual move, while the fog view keeps the `WasShipHit` snapshot at the old cells. `CellDto.IsDodgeMarked` (Юркая единичка dodged a ballista — салатовый mark, both views) and `SummonTrail` now includes the summon's spawn/re-entry cell.

## 11. LLM services (outbound Anthropic API)

Two independent callers, both model `claude-haiku-4-5-20251001` (`GameStoryService.cs:27` `ClaudeHaikuService.cs:20`), key from `AnthropicApiKey` (`Config.cs:28`):

- **`GameStoryService`** (`GameStoryService.cs:19-39`; limits `GameStoryService.cs:26-29`): fire-and-forget post-game narrative, max 1800 tokens. Triggered from the finish path (§6); skips missing key and **bot-only games** (`GameStoryService.cs:45-52`); renders markdown→HTML, keeps the latest 50 stories in memory (`StoreStory`, `GameStoryService.cs:95-110`), pushes eventType `GameStory` to the room (`GameStoryService.cs:81-82`), re-serves on join via `GetStory` (`GameHub.cs:134-137`), and backfills the replay file (`OnStoryGenerated`, `GameNotificationService.cs:70-74`). Raw API call: `CallClaudeApi` (`GameStoryService.cs:343-370`).
- **`ClaudeHaikuService`** (`ClaudeHaikuService.cs:14-29`): single method `GenerateWitcherHintAsync` — a Russian Geralt-flavored hint about a hidden enemy, max 100 tokens, 5 s timeout, returns null on any failure so the caller falls back to static hints (`ClaudeHaikuService.cs:37-90`). Called from Geralt's Медитация passive for human players only (see DISCORD-INTERFACE.md §10). `Disabled` is forced true in `--sim` (`Program.cs:61-66`) so simulations never spend credits.

## 12. Discord profile widget (the one OAuth-verified flow)

- The Discord command `*widget_s` posts an OAuth authorize link-button (DISCORD-INTERFACE.md §9); the user approves and Discord redirects to the SPA /widget page with an access token in the URL fragment, which the page POSTs to the sync endpoint (§3).
- `TryVerifyAndAuthorizeAsync` resolves the real user via users/@me with the Bearer token, sets `WidgetAuthorized` on the account, then syncs (`DiscordWidgetService.cs:116-143`).
- `SyncAsync` PATCHes the user's Discord profile identity (application ClientId `901706293977432124`, `DiscordWidgetService.cs:18`, bot-token auth) with the 4 most recent characters' avatars and win rates from `CharacterStatistics` (`DiscordWidgetService.cs:36-114`). A 403 clears `WidgetAuthorized` (`DiscordWidgetService.cs:95-100`).
- Unrelated to the `SetPreferWeb` toggle (`GameHub.cs:558-573`) despite the shared "widget/web" vocabulary.

## 13. Per-account localization

`GameHub.SetLanguage` normalizes/persists `ru` or `en`, updates the process locale registry and immediately re-pushes the current personalized state (`GameHub.cs:83-96`). The mapper translates only viewer-owned logs, score sources, direct messages, media and the finished chronicle after the existing privacy gates; canonical `GameClass` logs and opponents' hidden state are not mutated (`GameStateMapper.cs:115-126,167-174,1088-1097`). Eternal Tsukuyomi's final-log replacement is localized for the viewer too (`GameStateMapper.cs:932-960`). Character/passive DTO text remains canonical until the Vue presentation boundary so prediction/draft values stay valid.

The general English catalog is copied to deployed `DataBase` and loaded lazily by `GameLocalization`; Vue imports it for ordinary bidirectional presentation. Longest-first fragments include markup-split and partially projected replay forms, such as `**обычных** очков`, `Class: +2 Cкилла`, Justice encouragements, troll outcomes and username-prefixed winner lines. Character flavor uses the separate backend-owned `phrases.en.json`: startup pairs all 235 `PhraseClass` fields and validates all 796 indices (`PhraseLocalization.cs:19-62`). A selected flavor line is stored as a compact bilingual payload, so live web projection and replay snapshots retain the exact RU and EN rendering; media DTOs expose paired fields (`GameStateMapper.cs:1088-1106`). Discord resolves payloads before sorting and stories resolve them before prompt cleanup. Legacy `|>Phrase<|` records retain passive-aware fallback support. `tools/audit-localization.sh` runs the exhaustive phrase parity audit. Full contract: [LOCALIZATION.md](LOCALIZATION.md).

## 14. Account-reward transaction boundary

All account-economy operations synchronize on the `DiscordAccountClass` instance: the finish path settles account/history/mastery/ZBS/quest/top-two-box/achievement/statistics together; Daily Quest initialization/reroll, loot debit+roll+credit, reward reads/acknowledgements, paid draft picks and Discord-store changes use that same monitor (`CheckIfReady.cs:711-785`; `QuestClass.cs:358-508,638-723`; `GameHub.cs:649-725,811-1018`; `WebGameService.cs:328-420`; `GameReactions.cs:512-610`; `StoreReactions.cs:196-515`). This prevents simultaneous reward, reroll, pick or shop actions from losing a balance/box/progress update.

Daily Quest day/week initialization is a durable user-visible mutation: `RequestQuests` snapshots the complete quest/economy state, normalizes/migrates with one UTC timestamp, saves before sending, and restores on failure. `RerollDailyQuest` uses the same contract, including replacement progress and any immediately earned card/daily/mastery/weekly rewards (`QuestClass.cs:283-355,414-508`; `GameHub.cs:649-725`). Game-end progress uses the ordinary settlement policy: retain the completed in-memory transaction for periodic retry if the immediate save fails. Full semantics: [DAILY-QUESTS.md](DAILY-QUESTS.md).

Loot results are retry-safe rather than client-animated randomness. `QuestData.LastLootBox` persists the last `OpeningId`; until acknowledged, every V2 open returns it without another debit/credit. `RequestQuests` returns that unacknowledged record with current account totals for reload/reconnect recovery. `OpenLootBoxV2` and acknowledgement snapshot/restore the affected account fields around the save; legacy `OpenLootBox` acknowledges inside the durable transaction so cached clients without the new acknowledgement call do not become stuck (`QuestClass.cs:804-871`; `GameHub.cs:649-899,1599-1643`). The reveal animation never determines the reward.

New results use `SecureRandom`: reward amounts use its inclusive bounds and rarity rolls use 10,000 equally likely outcomes with cumulative cutoffs 50/300/1500/4000, exactly preserving the published 0.5/2.5/12/25/60% base distribution (`QuestClass.cs:873-912`; RNG implementation `SecureRandom.cs:17-25`). Pity upgrades only a tenth consecutive below-Rare natural result; it does not replace a natural Rare+.

User-triggered economy writes are durable before confirmation: Daily Quest reroll, V2 loot open, loot acknowledgement, achievement acknowledgement, paid draft and Discord-store transactions save while holding the account monitor, then restore their exact in-memory snapshot before returning a retryable error if replacement fails (`GameHub.cs:685-725,818-899,949-1018`; `WebGameService.cs:328-420`; `GameReactions.cs:512-610`; `StoreReactions.cs:196-515`). `SaveAccount` reports the atomic-replace result (`UserAccounts.cs:113-139`; `UsersDataStorage.cs:28-80`). A real-player game-end save failure is critical-logged and retains its settled in-memory state for the periodic retry; bot accounts deliberately skip the immediate disk write (`CheckIfReady.cs:779-785`).

Achievement V2 intentionally filters old IDs rather than migrating them. Unknown legacy progress remains deserializable in account JSON but cannot enter board totals or the unlock queue; no retroactive reward is inferred from match history (`AchievementClass.cs:133-179,355-360`; `GameHub.cs:903-947`). Daily Quest V1 instead has an explicit same-day economy migration: paid legacy days become settled without a second payout; unpaid days begin V2, while loot state is preserved (`QuestClass.cs:283-355,734-797`).

## 15. Known quirks & pitfalls (backend)

- **Asserted-ID auth**: any client can claim any Discord ID (§2). Never build features assuming web identity is verified; the only verified flag is `WidgetAuthorized` (`DiscordWidgetService.cs:141`).
- **REST is a subset** — new player-facing actions must be added to the hub (and usually only the hub); adding them to GameController is optional parity.
- **ARAM has hub/REST methods but no web UI** — AramReroll/AramConfirm are callable and the reroll counters are serialized, but the Vue client never renders an ARAM pick screen (finding **m24** in AUDIT-FINDINGS.md; WEB-CLIENT.md §13).
- `Attack` pushes state even when rejected (`GameHub.cs:228-235`) — the client relies on this to re-sync after refusals; other actions push on success only.
- Finished games are invisible to the 300 ms timer (§6) — if you add finish-time data to the DTO, populate it **before** the finish callback fires or it will never reach clients.
- New per-player web state needs the whole §7 pipeline: DTO member (`GameStateDto.cs`), mapper case with the right isMe/isAdmin gate (`GameStateMapper.cs`), TS mirror + widget (ARCHITECTURE.md §7 steps 11-14). Missing the gate = information leak to opponents/spectators.
- `KOTGH_PORT` is env-only (`Program.cs:198`); Config carries just the bot token and the Anthropic key (`Config.cs:9-28`).
- Discord IDs cross the hub as strings; the server parses with `ulong.TryParse` (`GameHub.cs:61-66`) — never let the client send them as JS numbers.
- Blackjack tables and battleship games are in-memory only (no persistence, 5-min stale cleanup) — restarts drop them.
