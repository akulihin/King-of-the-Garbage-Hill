# King of the Garbage Hill — Discord Interface (bot commands & in-game UI)

> Code-verified against the working tree of 2026-07-02 (v4.1.8). Companion docs: [ARCHITECTURE.md](ARCHITECTURE.md) (§1 topology — the bot shares the process and `GamesList` with the web server), [WEB-BACKEND.md](WEB-BACKEND.md) (the other frontend; web actions reuse the handlers described here), [GAME-DESIGN.md](GAME-DESIGN.md) (what the actions mean). Russian labels and custom-ids below are load-bearing strings — never paraphrase them.

## 1. Interaction model

- **Text commands** via `CommandService` with automatic module discovery (`CommandHandling.cs:47-50`). Prefix gate (`HandleCommandAsync`, `CommandHandling.cs:155-186`): literal `*` (with or without space), bot mention, the account's personal `MyPrefix`, **or any message at all in DMs** (`GuildName == "DM"`, `CommandHandling.cs:180`). Presence advertises `*st - Запустить игру` (`Program.cs:70`).
- **Buttons + select menus** are the only component interactions: `ButtonExecuted` and `SelectMenuExecuted` are wired in `DiscordEventDispatcher.InitializeAsync` (`DiscordEventDispatcher.cs:57` `DiscordEventDispatcher.cs:72`), both `DeferAsync()` then fan out to all four handlers — GameReaction, StoreReactions, TutorialReactions, LoreReactions — each self-selects by message/custom-id (`DiscordEventDispatcher.cs:76-92`).
- **Dead subsystems** (do not build on them): slash commands and context-menu commands are fully implemented but never registered nor subscribed — `Client_HandleSlashCommandAsync` (`CommandHandling.cs:193`) through the four registration helpers (up to `CommandHandling.cs:458`) have no callers; reaction handlers are commented out (`DiscordEventDispatcher.cs:192-214`); there are no modals. The live model is prefix commands + components only.
- **Command-message mirroring**: editing your command re-executes it and edits the bot's reply (`CommandHandling.cs:73-145`); deleting your command deletes the bot's reply (`CommandHandling.cs:52-70`, dispatched at `DiscordEventDispatcher.cs:160-165`). Failed commands reply `Error! {reason}` except silent "Unknown command" (`CommandResults`, `CommandHandling.cs:460-482`).
- No typing indicators (`UserIsTyping` is empty, `DiscordEventDispatcher.cs:94-96`).

## 2. Command inventory

Player-facing (module `General.cs` unless noted; aliases exact):

| Command | Aliases | Args | Does | Anchor |
|---|---|---|---|---|
| `игра` | `st`, `start`, `start game` | up to 6 `IUser` mentions | start a normal game; empty slots → bots (§3) | `General.cs:46-55` |
| `игра` | same | `int team` + mentions | team game, team layouts 2/3/4 (`General.cs:288-367`) | `General.cs:57-66` |
| `aram` | `ar`, `a` | mentions | ARAM game (reroll phase, `MoveListPage` 5) | `General.cs:68-75` |
| `stb` | — | mode (`ShowResult`/`Normal`/`Bot`), times | mass bot games via `BotGameFactory` | `General.cs:78-100` |
| `Сложность` | `difficulty`, `casual`, `normal` | — | toggle PlayerType 0↔1 (Обычная ↔ Казуальная — casual reveals more info) | `General.cs:103-123` |
| `время` | `uptime` | — | bot stats embed (uptime, command counters, latency) | `General.cs:125-151` |
| `myPrefix` | — | text | show/set personal prefix (<100 chars, no everyone/here) | `General.cs:154-188` |
| `stats` | — | `IUser` or raw id | personal stats embed (§9) | `General.cs:191-204` |
| `помощь` | `assist`, `help` | command name (opt) | per-command or module help, paged variants | `HelpModule.cs:25-26` `HelpModule.cs:80-81` |
| `Лор` | `l`, `lore` | — | character lore browser (embed + select) | `Lore.cs:26-27` |
| `магазин` | `store` | — | roll-chance shop (§9) | `Store.cs:24-25` |
| `обучение` | `tt`, `tutorial` | — | interactive DM tutorial (TutorialReactions) | `Tutorial.cs:22-23` |
| `rst` / `Ролл` | `Роллл`, `roll` | number(, times) | dice rolls / RNG distribution test | `DiceRoll.cs:23` `DiceRoll.cs:68-69` `DiceRoll.cs:114-115` |
| `чистка` | `purge`, `clean`, `убрать`, `clear`, `delete`, `remove` | amount, user (opt) | bulk delete, needs ManageMessages | `ServerManagement.cs:30-33` |
| `widget_s` | — | — | post the profile-widget OAuth "Authorize" link-button (client_id 901706293977432124) | `ServerManagement.cs:124-131` |
| `widget` | — | stat texts, number, id | push stats to the Discord profile widget; requires prior authorize (`WidgetAuthorized`, `ServerManagement.cs:157-165`) | `ServerManagement.cs:138` |

Admin (`AdminPanel.cs`; gated by `[RequireOwner]` or hardcoded owner-ID checks): `getInvite`/`leaveGuild`/`ShowGuildInfo`/`ShowGuilds` (`AdminPanel.cs:44-92`), `restart` (`AdminPanel.cs:108-109`), solo test-game `игра <int>` (`AdminPanel.cs:121-122`), `SetCharacter` (`AdminPanel.cs:195`), `SetType` 0/1/2 (`AdminPanel.cs:233`), and the `SetStat`/`set` cheats — numeric stats/score/round and character/passive add-remove (`AdminPanel.cs:266-267` `AdminPanel.cs:348-349`).

## 3. Game start & lobby lifecycle (`*st`)

`General.StartGame` (`General.cs:207-477`) is the whole flow:

1. Rejects the bot itself, unregistered bots, and mentioned players who are already `IsPlaying` (`General.cs:220-237`).
2. Mentioned humans get freed from any bot-seat via `SubstituteUserWithBot`; unspecified slots stay bots (`General.cs:241`); new sequential gameId (`General.cs:244`).
3. Character roll per mode: normal `HandleCharacterRoll`, aram `HandleAramRoll` + `MoveListPage` 5 + bots auto-confirmed (`General.cs:249-264`); shuffle + score-sort (`General.cs:268-269`).
4. **Draft pick applies to Discord games too** when `EnableDraftPick` is on and mode is normal (`General.cs:270-274` — same const as web, `WebGameService.cs:28`): natural roll + 2 alternatives per human, `MoveListPage` 6, bots auto-confirmed (`General.cs:428-446`).
5. Team modes split mentions into 2/3-player teams (`General.cs:288-367`); Суппорт + Sirinoks have a 22 % same-team bias (`General.cs:369-404`); teammates auto-predict each other (`General.cs:406-418`).
6. `GameClass` created with `IsCheckIfReady = false`; ARAM gets `TurnLengthInSecond` 600 (`General.cs:421-427`).
7. Every human gets the **two DM messages** via `WaitMess` (§4) (`General.cs:449`); Nanobot added, teams added, timer started, game listed (`General.cs:453-460`).
8. Round 0 init for plain-normal games only (draft/aram defer it) (`General.cs:464-468`), first `UpdateMessage` per player (`General.cs:470`), a **web link DM** `https://kotgh.ozvmusic.com/game/{gameId}` that auto-deletes next round (`General.cs:472-474`), then `IsCheckIfReady = true` hands control to the 100 ms loop (§6) (`General.cs:476`).

ARAM/draft phase resolution happens inside the loop: ARAM waits for all 6 `IsAramRollConfirmed` then re-sends character sheets and flips to page 1 (`CheckIfReady.cs:949-973`); draft waits for all `IsDraftPickConfirmed`, then runs the deferred init (fresh Nanobots/Exploit lists, round 0, timer restart) (`CheckIfReady.cs:978-1017`).

## 4. The in-game DM UI

**The game runs entirely in DMs.** Per human player two messages are tracked on `InGameDiscordStatus` (`InGameDiscordStatus.cs:13-14`): the static **character sheet** and the interactive **main game message**. All senders skip bots, web players, and `PreferWeb` players (`GameUpdateMess.cs:131-137` and every send/update guard like `GameUpdateMess.cs:101-121`) — toggling PreferWeb from the web (WEB-BACKEND.md §4) silences the Discord copy; `UpdateMessage` then reroutes any extraText into `WebMessages` instead (`GameUpdateMess.cs:1822-1830`).

- **Character sheet** (`GetCharacterMessage`, `GameUpdateMess.cs:53-99`): name, four stats — Sakura renames them Сексуальность/Грубость/Скорость/Нытье (`GameUpdateMess.cs:65-71`) — plus one field per *visible* passive; sent/updated via `SendCharacterMessage`/`UpdateCharacterMessage` (`GameUpdateMess.cs:108-129`).
- **Main message** is rebuilt by `UpdateMessage` switching on `MoveListPage` (`GameUpdateMess.cs:1837-1882`): 1 = FightPage + game buttons; 2 = RESERVED (empty; legacy logs page); 3 = LvlUpPage + level-up select (+ the decorative disabled `crutch` button "Riot style choice" for round-9 Дизмораль, `GameUpdateMess.cs:1857-1862`); 4 = debug (commented out); 5 = AramPickPage + reroll buttons; 6 = DraftPickPage + pick buttons (`GameUpdateMess.cs:1697-1759`).
- **FightPage anatomy** (`GameUpdateMess.cs:1052-1180`): title "King of the Garbage Hill", footer = `GetTimeLeft` (`(N/300с) | Версия…`, plus `Ожидаем других игроков •` when ready; mylorik/DeepList get the `(x+х)*19` gag — `GameUpdateMess.cs:1889-1901`); description = global logs (admin-hidden snippets stripped for non-admins, `GameUpdateMess.cs:1079-1084`) + stat block (Интеллект/Сила/Скорость/Психика + Справедливость/Мораль/Скилл + Мишень + Класс + `Множитель очков`, `GameUpdateMess.cs:1108-1124`) + the leaderboard (`LeaderBoard`, `GameUpdateMess.cs:158-191` — own score only; per-viewer prefixes/suffixes via the same CustomLeaderBoard methods the web reuses); fields `События прошлого раунда:` and `События этого раунда:` from personal logs split by `|||`, chunked at 1024 chars (`GameUpdateMess.cs:1127-1171`); avatar thumbnail unless `IsMobile` (`GameUpdateMess.cs:1174-1175`).

## 5. Buttons & selects catalog (main game message)

Composed by `GetGameButtons` (`GameUpdateMess.cs:1598-1668`): row 0 — Блок, Авто Ход (Tier > 3, non-ARAM, `GameUpdateMess.cs:1604-1607`), Изменить свой выбор, Завершить Игру, Дебаг (two owner IDs only, `GameUpdateMess.cs:1611-1614`); row 1 — attack select; row 2 — moral/skill buttons (`GameUpdateMess.cs:1618-1622`); row 3 — predict select (non-ARAM, hidden for the AdminPlayerType passive, `GameUpdateMess.cs:1624-1630`); row 4 — character buttons + Mobile Device in round 1 (`GameUpdateMess.cs:1633-1665`).

| custom-id | Label (exact) | Built at | Handled at |
|---|---|---|---|
| `block` | `Блок` (Success; disabled when acted or round > 10 unless Kratos-revenant) | `GameUpdateMess.cs:1761-1776` | `GameReactions.cs:312-362` (Спарта → "Спартанцы не капитулируют!!" `GameReactions.cs:313-317`; Aggress → "I. WONT. STOP." `GameReactions.cs:319-323`; Dopa `Макро` two-action logic `GameReactions.cs:326-349`) |
| `attack-select` | placeholder `Выбрать цель` → options `Напасть на {name}` | `GameUpdateMess.cs:1306-1381` | `GameReactions.cs:374-380` → `HandleAttack` (§6) |
| `auto-move` | `Авто Ход` (locked first 29 s except owners) | `GameUpdateMess.cs:1812-1820` | `GameReactions.cs:166-184` |
| `change-mind` | `Изменить свой выбор` (Dopa gets disabled `선택 변경`) | `GameUpdateMess.cs:1801-1810` | `GameReactions.cs:186-212` (un-readies, strikes the log line through) |
| `end` | `Завершить Игру` (Danger) | `GameUpdateMess.cs:1778-1793` | `GameReactions.cs:294-297` → `EndGame` (§8) |
| `confirm-prefict` [sic] | `Я подтверждаю свои предположения` | `GameUpdateMess.cs:1550-1552` | `GameReactions.cs:287-293` |
| `confirm-skip` | `Я подтверждаю пропуск хода` | `GameUpdateMess.cs:1553-1555` | `GameReactions.cs:214-224` |
| `moral` | `на N бонусных очков` ladder 20/13/8/5 морали → 10/5/2/1; DeepList disabled "Интересует только скилл"; М.М. компромат disables | `GameUpdateMess.cs:1518-1546` | `GameReactions.cs:363-365` → `HandleMoralForScore` |
| `skill` | `Обменять N Морали на M Cкилла` ladder 20→100 … 1→2 (Еврей extra 7→40 `GameUpdateMess.cs:1575-1577`); Булькает disabled | `GameUpdateMess.cs:1548-1596` | `GameReactions.cs:366-368` → `HandleMoralForSkill` |
| `predict-1` | placeholder `Сделать предположение`, options `{name} это...`; disabled from round 9; Булькает → `Бууууууль` | `GameUpdateMess.cs:1438-1474` | `GameReactions.cs:382-384` → `HandlePredic1` (builds `predict-2` with all character names, `GameReactions.cs:553-580`) |
| `predict-2` | character list + `Предыдущие меню` | `GameReactions.cs:559-572` | `GameReactions.cs:386-388` → `HandlePredic2` (upserts `player.Predict`, `GameReactions.cs:582-613`) |
| `lvl-up` | placeholder `Выбор прокачки` (Вампур_ garlic gag `GameUpdateMess.cs:1480-1481`; Ирелия `Выбор нерфа` `GameUpdateMess.cs:1483-1486`; round-9 Дизмораль psyche-only menu `GameUpdateMess.cs:1500-1510`), options Интеллект/Сила/Скорость/Психика = values 1-4 | `GameUpdateMess.cs:1477-1496` | `GameReactions.cs:370-373` → `HandleLvlUp` (`GameReactions.cs:616-625`) |
| `mobile-device` | `Mobile Device` (round 1 only) | `GameUpdateMess.cs:1384-1387` `GameUpdateMess.cs:1662-1665` | `GameReactions.cs:159-164` (drops the thumbnail) |
| `debug_info` | `Дебаг` | `GameUpdateMess.cs:1795-1798` | `GameReactions.cs:305-309` (toggles the dead page 4) |
| `stable-Darksci` / `not-stable-Darksci` | `Мне никогда не везёт...` / `Мне сегодня повезёт!` (round 1, + hint DM) | `GameUpdateMess.cs:1636-1647` | `GameReactions.cs:226-254` |
| `yong-gleb` | `Вспомнить Молодость` (round 1) | `GameUpdateMess.cs:1650-1654` | `GameReactions.cs:256-285` (in-place transform to Молодой Глеб) |
| `dopa-attack-select` | placeholder `Второе Действие` (Korean gags when skipped/gg) | `GameUpdateMess.cs:1389-1436` | **no case in the dispatch switch — selections are silently ignored** (finding **m23** in AUDIT-FINDINGS.md; Dopa's real second action runs through `attack-select`/`block` Макро branches) |
| `aram_reroll_1..4` / `aram_reroll_5` / `aram_roll_confirm` | `Reroll #N` (max 4 passive rerolls) / `Reroll Stats` (max 1) / `Confirm` → disabled `Wait for other players` | `GameUpdateMess.cs:1670-1694` | `GameReactions.cs:390-413` |
| `draft_pick_0/1/2` | `{Name} (FREE)` / `{Name} (cost 5 ZBS points)`; after pick `Ожидаем остальных` (`draft_pick_wait`, no handler) | `GameUpdateMess.cs:1734-1759` | `GameReactions.cs:414-418` → HandleDraftPick |

Attack-menu placeholder states (`GameUpdateMess.cs:1306-1367`): `Что-то заставило тебя скипнуть...`, `Вы поставили блок!`, `Вы использовали Авто Ход!`, `gg wp` (round > 10), Kratos event `УБИТЬ!` / `ЭТО БОГ ВОЙНЫ! БЕГИ!`, `Вы напали на {name}`, `Подтвердите свои предложение перед атакой!`, Butcher-ban `Обжаловать бан...` (`GameUpdateMess.cs:1355-1360`); empty menu → option `ТЫ ВСЕХ УБИЛ` (`GameUpdateMess.cs:1378`).

## 6. Dispatch & round resolution

All in-game components land in `GameReaction.ReactionAddedGameWindow` (`GameReactions.cs:135-423`):

1. **Ownership guard**: the click must come from a player of a live game whose tracked `SocketGameMessage` id equals the clicked message (`GameReactions.cs:137-142`) — clicks on other messages fall through to the other three handlers.
2. **Debounce**: < 700 ms since `LastButtonPress` → ephemeral `Ошибка: Слишком быстро! Нажми на кнопку еще раз.` (`GameReactions.cs:144-150`).
3. Switch on custom-id (`GameReactions.cs:157`), §5 table. `IsSolo` short-circuits re-renders when the resolution is imminent (e.g. `GameReactions.cs:177-179`).

**Attack trace** (`HandleAttack`, `GameReactions.cs:627-743`): parses the selected option as the target's player Guid (`GameReactions.cs:646-647`) — or, for bots/web (`IsAutoMove` trick, WEB-BACKEND.md §8), by leaderboard place (`GameReactions.cs:651`) — then **records `WhoToAttackThisTurn`** (`GameReactions.cs:657`), the state the fight loop consumes. Guards: pickle-Rick auto-confirm (`GameReactions.cs:661-662`), Клинки хаоса splash adds neighbours (`GameReactions.cs:666-676`), Weedwick/DeepList pet refusals (`GameReactions.cs:686-698`), round-10 Butcher ban (`GameReactions.cs:702-707`), СОсиновый кол repeat-target ban (`GameReactions.cs:710-715`), self-attack `Зачем ты себя бьешь?` (`GameReactions.cs:718-724`), Макро first-of-two (`GameReactions.cs:727-734`); otherwise ready + log `Вы напали на игрока {name}` (`GameReactions.cs:737-742`).

**Resolution is timer-driven** — clicking never resolves a round. `CheckIfReady` ticks every 100 ms (`CheckIfReady.cs:63-72`) into `CheckIfEveryoneIsReady` (`CheckIfReady.cs:917-943`): per game, finished → `HandleLastRound`; ARAM/draft phases (§3); dead players auto-ready (`CheckIfReady.cs:1031-1037`); humans get their message refreshed at 30/90/150/210/270 s of the turn (`CheckIfReady.cs:1039-1070`); a player only counts as ready in the first 50 s if `ConfirmedSkip` (`CheckIfReady.cs:1072-1074`). When all humans are ready **or** `TurnLengthInSecond` expires (`CheckIfReady.cs:1078-1080`): idle humans are force-auto-moved with `Вы не походили. Использовался Авто Ход` (`CheckIfReady.cs:1086-1098`), half-done Макро too (`CheckIfReady.cs:1100-1108`), bots act, transient messages are cleaned (`DeleteItAfterRound` per player, `CheckIfReady.cs:1293`), fights are computed (`CalculateAllFights`, `CheckIfReady.cs:1314`), and each human's DM message is re-rendered (`CheckIfReady.cs:1345`).

Message edits are serialized per player through an embed queue with 200 ms spins (`ModifyGameMessage`, `HelperFunctions.cs:213-257`); transient side-messages auto-delete at round end (`SendMsgAndDeleteItAfterRound`/`DeleteItAfterRound`, `HelperFunctions.cs:262` `HelperFunctions.cs:296`).

## 7. Privacy in Discord

- Each player has a **separate DM**, so personal logs are private by construction; the FightPage merges global + personal fields (§4).
- `SortLogs` masks other players' passive names in the viewer's logs — `Неизвестно` for Normal players, `❓ {name}` for Casual — except the deliberately public ones (Запах мусора, Чернильная завеса, Еврей, 2kxaoc) (`GameUpdateMess.cs` SortLogs; applied at `GameUpdateMess.cs:1133` `GameUpdateMess.cs:1156`).
- Admin-only fight math is carried as `HiddenGlobalLogSnippets` and stripped for PlayerType ≠ 2 (`GameUpdateMess.cs:1079-1084`) — same list the web strips (WEB-BACKEND.md §7).
- New players get a training wheel: `⟶` is expanded to `⟶ победил` in logs (`GameUpdateMess.cs:1043-1044`).

## 8. End of game

- `Завершить Игру` mid-game: `EndGame` swaps the leaver for a bot and DMs the multiplayer nudge `Спасибо за игру!…` (`GameUpdateMess.cs:781-792`; seat swap `SubstituteUserWithBot`, `HelperFunctions.cs:337`).
- Natural end (`HandleLastRound`, `CheckIfReady.cs:266`): sole winner gets `__**Победа! Теперь вы Король этой Мусорной Горы. Пока-что...**__` + a gif (`CheckIfReady.cs:585-595`); then per player (`CheckIfReady.cs:602-694`): final `UpdateMessage`, account reset (`IsPlaying` false, GameId parked), match history, **mastery points 10/7/5/3/2/1 by place** (`CheckIfReady.cs:617-624`), **ZBS Points 100/50/40/30/20/10** with score-tie = 100, team override 100/50, dead = 0 (`CheckIfReady.cs:632-667`), quest tracking (`CheckIfReady.cs:670`), **loot box for alive top-2** (`CheckIfReady.cs:672-676`), achievement evaluation (`CheckIfReady.cs:678-687`), tier-pity counters (`CheckIfReady.cs:689-694`).
- The finish path also triggers the web-side callbacks (final broadcast, replay save, AI story — WEB-BACKEND.md §6).

## 9. Accounts & meta surfaces

- Accounts are created lazily on first contact (`UserAccounts.GetAccount` → `CreateUserAccount`, `UserAccounts.cs:78-158`); key fields on `DiscordAccountClass`: `MyPrefix`/`IsPlaying`/`PlayerType` (`DiscordAccountClass.cs:21-22` `DiscordAccountClass.cs:47`), `ZbsPoints` (`DiscordAccountClass.cs:26`), `CharacterMastery` + `ReplayHashes` + `PendingLootBoxes` (`DiscordAccountClass.cs:37-39`), `Achievements` (`DiscordAccountClass.cs:28`), `MatchHistory` + `CharacterStatistics` (`DiscordAccountClass.cs:9-10`), `WidgetAuthorized` (`DiscordAccountClass.cs:14`).
- `*stats` renders ZBS Points / Тип Пользователя / Всего Игр and the rest of the profile embed (`StartGameLogic.cs:405-407`).
- **Shop** `*магазин` (StoreReactions): select `store-select-character` placeholder `Выбрать персонажа` (`StoreReactions.cs:41-42`), buttons `Поднять шанс на 1%`/`на 10%`, `Опустить шанс на 1%`/`на 10%`, `Сбросить все изменения`, `Сбросить все изменения за всех персонажей` with ids store-up-1/store-up-10/store-down-1/store-down-10/store-return-character/store-return-all-characters (`StoreReactions.cs:55-60`); dispatched in `ReactionAddedStore`, guarded by custom-id containing "store" (`StoreReactions.cs:150-154`, cases from `StoreReactions.cs:188`). Spends ZBS to bias per-character roll chances.
- Quests/loot boxes/achievements accrue here but their **browsing UI is the web lobby** (WEB-CLIENT.md §7); Discord only banks them (§8).
- Profile widget: `*widget_s` posts the OAuth link (`ServerManagement.cs:124-131`), `*widget` pushes texts/number after authorization (`ServerManagement.cs:138` `ServerManagement.cs:157-165`) — server side in WEB-BACKEND.md §12.

## 10. Claude integration (Geralt's Медитация)

When a **human** Geralt blocks/meditates, the passive resolves a not-yet-revealed enemy and asks `ClaudeHaikuService.GenerateWitcherHintAsync` for a one-line Russian witcher-style hint (`CP:4465`); on null (no key, 5 s timeout, HTTP error, `Disabled` in `--sim`) it falls back to the static `WitcherSensesHints` dictionary (`CP:4483`, table at `Geralt.cs:336`); the result lands in personal logs as `Чутьё: {hint} ({username})` (`CP:4486`). Service internals in WEB-BACKEND.md §11.

## 11. Known quirks

- The slash/context-menu subsystem and the reaction pipeline are dead code (§1) — the handler names still say "Reaction" but take components.
- `dopa-attack-select` renders but has no dispatch case (finding **m23**); `confirm-prefict` is a load-bearing typo; the `stats` case toggles the empty page 2 but no button builds it anymore (`GameReactions.cs:299-303`).
- The `Дебаг` button and early Авто Ход unlock are hardcoded to two owner Discord IDs (`GameUpdateMess.cs:1611` `GameUpdateMess.cs:1816-1817`).
- Anything DM'd mid-round via `SendMsgAndDeleteItAfterRound` disappears at round end by design — don't use it for persistent info.
- `PreferWeb`/web players silently skip every Discord render (§4); when debugging "the bot stopped updating me", check that flag first.
