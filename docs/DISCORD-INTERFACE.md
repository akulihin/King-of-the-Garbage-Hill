# King of the Garbage Hill — Discord Interface (bot commands & in-game UI)

> Code-verified against the working tree of 2026-07-11 (v4.3.1). Companion docs: [ARCHITECTURE.md](ARCHITECTURE.md) (§1 topology — the bot shares the process and `GamesList` with the web server), [WEB-BACKEND.md](WEB-BACKEND.md) (the other frontend; web actions reuse the handlers described here), [GAME-DESIGN.md](GAME-DESIGN.md) (what the actions mean). Russian labels and custom-ids below are load-bearing strings — never paraphrase them.

## 1. Interaction model

- **Text commands** via Discord.Net's command service with automatic module discovery (`CommandHandling.cs:47-50`). Prefix gate (`HandleCommandAsync`, `CommandHandling.cs:155-186`): literal `*` (with or without space), bot mention, the account's personal `MyPrefix`, **or any message at all in DMs** (`GuildName == "DM"`, `CommandHandling.cs:180`). Presence advertises `*st - Запустить игру` (`Program.cs:70`).
- **Buttons + select menus** are the only component interactions: `ButtonExecuted` and `SelectMenuExecuted` are wired in the dispatcher's `InitializeAsync` (`DiscordEventDispatcher.cs:37-74`), both `DeferAsync()` then fan out to all four handlers — GameReaction, StoreReactions, TutorialReactions, LoreReactions — each self-selects by message/custom-id (`DiscordEventDispatcher.cs:76-92`).
- **Dead subsystems** (do not build on them): slash commands and context-menu commands are fully implemented but never registered nor subscribed — `Client_HandleSlashCommandAsync` (`CommandHandling.cs:193`) through the four registration helpers (up to `CommandHandling.cs:458`) have no callers; reaction handlers are commented out (`DiscordEventDispatcher.cs:192-214`); there are no modals. The live model is prefix commands + components only.
- **Command-message mirroring**: editing your command re-executes it and edits the bot's reply (`CommandHandling.cs:73-145`); deleting your command deletes the bot's reply (`CommandHandling.cs:52-70`, dispatched at `DiscordEventDispatcher.cs:160-165`). Failed commands reply `Error! {reason}` except silent "Unknown command" (`CommandResults`, `CommandHandling.cs:460-482`).
- No typing indicators (`UserIsTyping` is empty, `DiscordEventDispatcher.cs:94-96`).

## 2. Command inventory

Player-facing (module `General.cs` unless noted; aliases exact):

| Command | Aliases | Args | Does | Anchor |
|---|---|---|---|---|
| `игра` | `st`, `start`, `start game` | up to 6 `IUser` mentions | start a normal game; empty slots → bots (§3) | `General.cs:46-55` |
| `игра` | same | `int team` + mentions | team game, team layouts 2/3/4 (`General.cs:288-367`) | `General.cs:57-66` |
| `aram` | `ar`, `a` | mentions | ARAM game (reroll phase, `MoveListPage` 5, `General.cs:256-259`) | `General.cs:68-75` |
| `stb` | — | mode (`ShowResult`/`Normal`/`Bot`), times | mass bot games via `CreateBotGameAsync` | `General.cs:78-100` |
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

Admin (`AdminPanel.cs`; gated by `[RequireOwner]` or hardcoded owner-ID checks): `getInvite`/`leaveGuild`/`ShowGuildInfo`/`ShowGuilds` (`AdminPanel.cs:44-92`), `restart` (`AdminPanel.cs:108-109`), `lootbox <user> <amount>` — grant pending loot boxes to an account, amount validated > 0 and capped (`AdminPanel.cs:121-165`), solo test-game `игра <int>` (`AdminPanel.cs:168-169`), `SetCharacter` (`AdminPanel.cs:242`), `SetType` 0/1/2 (`AdminPanel.cs:280`), and the `SetStat`/`set` cheats — numeric stats/score/round and character/passive add-remove (`AdminPanel.cs:313-314` `AdminPanel.cs:395-396`).

## 3. Game start & lobby lifecycle (`*st`)

`General.StartGame` (`General.cs:207-479`) is the whole flow:

1. Rejects the bot itself, unregistered bots, and mentioned players who are already `IsPlaying` (`General.cs:220-237`).
2. Mentioned humans get freed from any bot-seat via `SubstituteUserWithBot`; unspecified slots stay bots (`General.cs:241`); new sequential gameId (`General.cs:244`).
3. Character roll per mode: normal `HandleCharacterRoll`, aram `HandleAramRoll` + `MoveListPage` 5 + bots auto-confirmed (`General.cs:249-264`); shuffle + score-sort (`General.cs:268-269`).
4. **Draft pick applies to Discord games too** when `EnableDraftPick` is on and mode is normal (`General.cs:270-274` — same const as web, `WebGameService.cs:28`): natural roll + 2 alternatives per human, `MoveListPage` 6, bots auto-confirmed (`General.cs:428-446`).
5. Team modes split mentions into 2/3-player teams (`General.cs:288-367`); Суппорт + Sirinoks have a 22 % same-team bias (`General.cs:369-404`); teammates auto-predict each other (`General.cs:406-418`).
6. `GameClass` created with `IsCheckIfReady = false`; ARAM gets `TurnLengthInSecond` 600 (`General.cs:421-427`).
7. Every human gets the **two DM messages** via `WaitMess` (§4) (`General.cs:449`); Nanobot added, teams added, timer started, game listed (`General.cs:454-462`).
8. Round 0 init for plain-normal games only (draft/aram defer it) (`General.cs:466-470`), first `UpdateMessage` per player (`General.cs:472`), a **web link DM** `https://kotgh.ozvmusic.com/game/{gameId}` that auto-deletes next round (`General.cs:474-476`), then `IsCheckIfReady = true` hands control to the 100 ms loop (§6) (`General.cs:478`).

ARAM/draft phase resolution happens inside the loop: ARAM waits for all 6 `IsAramRollConfirmed` then re-sends character sheets and flips to page 1 (`CheckIfReady.cs:989-1013`); draft waits for all `IsDraftPickConfirmed`, then runs the deferred init (fresh Nanobots/Exploit lists, round 0, timer restart) (`CheckIfReady.cs:1018-1057`).

Naruto is a natural/draft option only in free-for-all games with two strict bot seats available for clones (a bot original therefore requires a third bot). Round-0 initialization converts those two seats and pre-fills each Naruto's predictions for the other two; ARAM never offers his passive set (`StartGameLogic.cs` `CanNaturallyRollNaruto`, `RollDraftOptions`; `General.cs` draft setup; `Naruto.cs` `InitializeTeam`; `CharactersPull.cs` `GetAramPassives`).

## 4. The in-game DM UI

**The game runs entirely in DMs.** Per human player two messages are tracked on `InGameDiscordStatus` (`InGameDiscordStatus.cs:5-14`): the static **character sheet** and the interactive **main game message**. All senders skip bots, web players, and `PreferWeb` players (`GameUpdateMess.cs:131-137` and every send/update guard like `GameUpdateMess.cs:101-121`) — toggling PreferWeb from the web (WEB-BACKEND.md §4) silences the Discord copy; `UpdateMessage` then reroutes any extraText into `WebMessages` instead (`GameUpdateMess.cs:1765-1773`).

- **Character sheet** (`GetCharacterMessage`, `GameUpdateMess.cs:53-99`): name, four stats — Sakura renames them Сексуальность/Грубость/Скорость/Нытье (`GameUpdateMess.cs:65-71`) — plus one field per *visible* passive (no placeholder for hidden ones; dynamically revealed passives become ordinary fields on a later update); sent/updated via `SendCharacterMessage`/`UpdateCharacterMessage` (`GameUpdateMess.cs:108-129`).
- **Main message** is rebuilt by `UpdateMessage` switching on `MoveListPage`: 1 = FightPage + game buttons; 2 = RESERVED (empty; legacy logs page); 3 = LvlUpPage + level-up select (+ the decorative disabled `crutch` button "Riot style choice" for round-9 Дизмораль); 4 = debug (commented out); 5 = AramPickPage + reroll buttons; 6 = DraftPickPage + pick buttons. Madara's hidden fifth passive is explicitly omitted from ARAM as well as the ordinary character sheet/draft (`GameUpdateMess.cs:95,1295-1345,1743-1772`).
- **FightPage anatomy** (`GameUpdateMess.cs:1068-1205`): title "King of the Garbage Hill", footer = `GetTimeLeft` (`(N/300с) | Версия…`, plus `Ожидаем других игроков •` when ready; mylorik/DeepList get the `(x+х)*19` gag); description = global logs (admin-hidden snippets stripped for non-admins) + stat block (Интеллект/Сила/Скорость/Психика + Справедливость/Мораль/Скилл + Мишень + Класс + `Множитель очков`) + the leaderboard. Character-derived target/status markers are viewer-scoped: owners see the enemies, positions and objectives their own kit tracks, while affected opponents, allies and unrelated viewers receive no identifying icon. This includes Goblin mines/Ziggurats, DooM nests, sealed Мадара, active Шэн and Napoleon/Support alliance annotations. Тигр's round-10 `🚫` is deliberately public because it represents a system ban (`GameUpdateMess.cs:219-811`; M35). Madara's variant keeps base stats, Justice and Class but removes all Harm-resist, Moral, Skill and target rows (`GameUpdateMess.cs:1141-1178`). The two personal-log fields and avatar behavior are otherwise unchanged.

## 5. Buttons & selects catalog (main game message)

Composed by `GetGameButtons` (`GameUpdateMess.cs:1545-1611`): row 0 — Блок, Авто Ход (Tier > 3, non-ARAM, `GameUpdateMess.cs:1551-1554`), Изменить свой выбор, Завершить Игру, Дебаг (two owner IDs only, `GameUpdateMess.cs:1558-1561`); row 1 — attack select; row 2 — moral/skill buttons (`GameUpdateMess.cs:1565-1569`); row 3 — predict select (non-ARAM, hidden for the AdminPlayerType passive, `GameUpdateMess.cs:1571-1577`); row 4 — character buttons + Mobile Device in round 1 (`GameUpdateMess.cs:1580-1608`).

| custom-id | Label (exact) | Built at | Handled at |
|---|---|---|---|
| `block` | `Блок` (Success; disabled when acted or round > 10 unless Kratos-revenant) | `GameUpdateMess.cs:1704-1719` | `GameReactions.cs:315-365` (Спарта → "Спартанцы не капитулируют!!" `GameReactions.cs:316-320`; Aggress → "I. WONT. STOP." `GameReactions.cs:322-326`; Dopa `Макро` two-action logic `GameReactions.cs:329-352`) |
| `attack-select` | placeholder `Выбрать цель` → options `Напасть на {name}` | `GameUpdateMess.cs:1301-1376` | `GameReactions.cs:377-383` → `HandleAttack` (§6) |
| `auto-move` | `Авто Ход` (locked first 29 s except owners) | `GameUpdateMess.cs:1755-1763` | `GameReactions.cs:166-184` |
| `change-mind` | `Изменить свой выбор` (Dopa gets disabled `선택 변경`) | `GameUpdateMess.cs:1744-1753` | `GameReactions.cs:186-212` (un-readies, strikes the log line through) |
| `end` | `Завершить Игру` (Danger) | `GameUpdateMess.cs:1721-1736` | `GameReactions.cs:297-300` → `EndGame` (§8) |
| `confirm-prefict` [sic] | `Я подтверждаю свои предположения` | `GameUpdateMess.cs:1497-1499` | `GameReactions.cs:290-296` |
| `confirm-skip` | `Я подтверждаю пропуск хода` | `GameUpdateMess.cs:1500-1502` | `GameReactions.cs:214-224` |
| `moral` | `на N бонусных очков` ladder 20/13/8/5 морали → 10/5/2/1; DeepList disabled "Интересует только скилл"; М.М. компромат disables | `GameUpdateMess.cs:1465-1493` | `GameReactions.cs:366-368` → `HandleMoralForScore` |
| `skill` | `Обменять N Морали на M Cкилла` ladder 20→100 … 1→2 (Еврей extra 7→40 `GameUpdateMess.cs:1522-1524`); Булькает disabled | `GameUpdateMess.cs:1495-1543` | `GameReactions.cs:369-371` → `HandleMoralForSkill` |
| `predict-1` | placeholder `Сделать предположение`, options `{name} это...`; disabled from round 9; Булькает → `Бууууууль` | `GameUpdateMess.cs:1384-1420` | `GameReactions.cs:402-404` → `HandlePredic1` (builds `predict-2` with all character names, `GameReactions.cs:612-640`) |
| `predict-2` | character list + `Предыдущие меню` | `GameReactions.cs:618-631` | `GameReactions.cs:406-408` → `HandlePredic2` (upserts `player.Predict`, `GameReactions.cs:642-680`) |
| `lvl-up` | placeholder `Выбор прокачки` (Вампур_ garlic gag `GameUpdateMess.cs:1426-1427`; Ирелия `Выбор нерфа` `GameUpdateMess.cs:1429-1432`; round-9 Дизмораль psyche-only menu `GameUpdateMess.cs:1447-1457`), options Интеллект/Сила/Скорость/Психика = values 1-4 | `GameUpdateMess.cs:1423-1443` | `GameReactions.cs:373-376` → `HandleLvlUp` (`GameReactions.cs:619-628`) |
| `mobile-device` | `Mobile Device` (round 1 only) | `GameUpdateMess.cs:1379-1382` `GameUpdateMess.cs:1605-1608` | `GameReactions.cs:159-164` (drops the thumbnail) |
| `debug_info` | `Дебаг` | `GameUpdateMess.cs:1738-1741` | `GameReactions.cs:308-312` (toggles the dead page 4) |
| `stable-Darksci` / `not-stable-Darksci` | `Мне никогда не везёт...` / `Мне сегодня повезёт!` (round 1, + hint DM) | `GameUpdateMess.cs:1583-1594` | `GameReactions.cs:226-254` |
| `yong-gleb` | `Вспомнить Молодость` (round 1) | `GameUpdateMess.cs:1597-1601` | `GameReactions.cs:256-288` (in-place transform to Молодой Глеб) |
| `doom-roll` | `Let's Roll!` (DooM Guy, round 1) | `GameUpdateMess.cs:1634-1637` | `GameReactions.cs:292-300` (disables Moral/predictions; later stages randomize) |
| `doom-chainsaw` | `Бензопила: выбрать пассивку` (pending after the first Chainsaw win) | `GameUpdateMess.cs:1640-1651` | `GameReactions.cs:301-307` |
| `aram_reroll_1..4` / `aram_reroll_5` / `aram_roll_confirm` | `Reroll #N` (max 4 passive rerolls) / `Reroll Stats` (max 1) / `Confirm` → disabled `Wait for other players` | `GameUpdateMess.cs:1613-1637` | `GameReactions.cs:393-416` |
| `draft_pick_0` / `draft_pick_1` / `draft_pick_2` | `{Name} (FREE)` / `{Name} (cost 5 ZBS points)`; after pick `Ожидаем остальных` (`draft_pick_wait`, no handler). Picks serialize with the web path; duplicate validation precedes any debit, and a paid pick saves or restores the 5 ZBS before the bridge is replaced. | `GameUpdateMess.cs:1677-1702` | `GameReactions.cs:417-438,512-610` |

Attack-menu placeholder states (`GameUpdateMess.cs:1299-1360`): `Что-то заставило тебя скипнуть...`, `Вы поставили блок!`, `Вы использовали Авто Ход!`, `gg wp` (round > 10), Kratos event `УБИТЬ!` / `ЭТО БОГ ВОЙНЫ! БЕГИ!`, `Вы напали на {name}`, `Подтвердите свои предложение перед атакой!`, Butcher-ban `Обжаловать бан...` (`GameUpdateMess.cs:1350-1355`); empty menu → option `ТЫ ВСЕХ УБИЛ` (`GameUpdateMess.cs:1373`). An initialized Naruto's two siblings are omitted from this attack menu, while their pre-filled Naruto predictions remain visible/editable through the ordinary prediction menu (`GameUpdateMess.cs` `GetAttackMenu`, `GetPredictMenu`; `Naruto.cs` `SeedSiblingPredictions`).

DooM Guy replaces the normal `lvl-up` options with the current stage's up-to-four configured modules (`GameUpdateMess.cs` `GetLvlUpMenu`; selection still reaches `HandleLvlUp`, whose DooM branch maps the selected index to that module, `GameReactions.cs` `GetLvlUp`). In Let's Roll mode that menu is never shown: the stage point is consumed automatically after sorting. Moral buttons and prediction menus are disabled/omitted. Demon-nest `🔥` and current Counter-attack `🎯` markers are visible only in DooM Guy's own leaderboard (or an admin inspection view); carriers/marked enemies and unrelated players receive no identifying marker (`GameUpdateMess.cs:256-266`; M35 privacy boundary).

Madara likewise omits the Moral-to-Skill, Moral-to-Points and prediction components entirely (`GameUpdateMess.cs:1647-1667`). On round 8 his action is pre-locked while incoming attacks remain valid; after sealing he sees his own `🚫`, cannot change his choice, is removed from every attack menu, and direct attempts return the exact text `Игрок запечатан`; opponents do not receive the identifying seal icon (`Madara.cs:189-217`; `GameUpdateMess.cs:253-254,1421-1430`; `GameReactions.cs:677-708`; M35). The global round-8 phrase is written into common logs, while the theme file is sent separately to every human DM (`CharacterPassives.cs:4980-5010`).

Эрен keeps the ordinary stat/level-up mechanics, but his character card, fight page and level-up menu display Intelligence as `Злость` and Psyche as `Самоуверенность` (`GameUpdateMess.cs:59-77, 1098-1112, 1240-1248, 1476-1487`). To Eren, enemies with hatred carry a public-in-his-DM `🔥1/2` leaderboard suffix (`GameUpdateMess.cs:443-446`). `Овца в загоне` also moves him to the last leaderboard row before every fight calculation through round 8 (`CheckIfReady.cs:1226-1234`); on block, the calculation clears the block into `Атакующий Титан`, so the normal block acknowledgement is only the submitted action, never a resolved shield (`DoomsdayMachine.cs:267-277`).

## 6. Dispatch & round resolution

All in-game components land in `GameReaction.ReactionAddedGameWindow` (`GameReactions.cs:135-426`):

1. **Ownership guard**: the click must come from a player of a live game whose tracked `SocketGameMessage` id equals the clicked message (`GameReactions.cs:137-142`) — clicks on other messages fall through to the other three handlers.
2. **Debounce**: < 700 ms since `LastButtonPress` → ephemeral `Ошибка: Слишком быстро! Нажми на кнопку еще раз.` (`GameReactions.cs:144-150`).
3. Switch on custom-id (`GameReactions.cs:157`), §5 table. `IsSolo` short-circuits re-renders when the resolution is imminent (e.g. `GameReactions.cs:177-179`).

**Attack trace** (`GameReactions.cs` `HandleAttack`): parses the selected option as the target's player Guid — or, for bots/web (`IsAutoMove` trick, WEB-BACKEND.md §8), by leaderboard place — then records `WhoToAttackThisTurn`, the state the fight loop consumes. Before recording, sealed Madara is rejected as actor or target with `Игрок запечатан`, round-8 Madara is rejected as actor, and an initialized Naruto sibling is rejected with `Наруто не могут нападать друг на друга.`. The remaining guards are pickle-Rick auto-confirm, Клинки хаоса splash, Weedwick/DeepList pets, round-10 Butcher ban, СОсиновый кол, self-attack and Макро first-of-two; otherwise the action becomes ready with `Вы напали на игрока {name}`.

**Resolution is timer-driven** — clicking never resolves a round. `CheckIfReady` ticks every 100 ms (`CheckIfReady.cs:63-72`) into `CheckIfEveryoneIsReady` (`CheckIfReady.cs:957-983`): per game, finished → `HandleLastRound`; ARAM/draft phases (§3); dead players auto-ready (`CheckIfReady.cs:1071-1077`); humans get their message refreshed at 30/90/150/210/270 s of the turn (`CheckIfReady.cs:1079-1110`); a player only counts as ready in the first 50 s if `ConfirmedSkip` (`CheckIfReady.cs:1112-1114`). When all humans are ready **or** `TurnLengthInSecond` expires (`CheckIfReady.cs:1118-1120`): idle humans are force-auto-moved with `Вы не походили. Использовался Авто Ход` (`CheckIfReady.cs:1126-1138`), half-done Макро too (`CheckIfReady.cs:1140-1148`), bots act, transient messages are cleaned (`DeleteItAfterRound` per player, `CheckIfReady.cs:1338`), fights are computed (`CalculateAllFights`, `CheckIfReady.cs:1359`), and each human's DM message is re-rendered (`CheckIfReady.cs:1390`).

Message edits are serialized per player through an embed queue with 200 ms spins (`ModifyGameMessage`, `HelperFunctions.cs:213-257`); transient side-messages auto-delete at round end (`SendMsgAndDeleteItAfterRound`/`DeleteItAfterRound`, `HelperFunctions.cs:262` `HelperFunctions.cs:296`).

## 7. Privacy in Discord

- Each player has a **separate DM**, so personal logs are private by construction; the FightPage merges global + personal fields (§4).
- `SortLogs` masks other players' passive names in the viewer's logs — `Неизвестно` for Normal players, `❓ {name}` for Casual — except the deliberately public ones (Запах мусора, Чернильная завеса, Еврей, 2kxaoc) (`GameUpdateMess.cs:794-810`; applied to both log fields at `GameUpdateMess.cs:1126` `GameUpdateMess.cs:1149`).
- Admin-only fight math is carried as `HiddenGlobalLogSnippets` and stripped for PlayerType ≠ 2 (`GameUpdateMess.cs:1072-1077`) — same list the web strips (WEB-BACKEND.md §7).
- New players get a training wheel: `⟶` is expanded to `⟶ победил` in logs (`GameUpdateMess.cs:1036-1037`).

## 8. End of game

- `Завершить Игру` mid-game: `EndGame` swaps the leaver for a bot and DMs the multiplayer nudge `Спасибо за игру!…` (`GameUpdateMess.cs:774-785`; seat swap `SubstituteUserWithBot`, `HelperFunctions.cs:337`).
- Natural end (`HandleLastRound`, `CheckIfReady.cs:276`): sole winner gets `__**Победа! Теперь вы Король этой Мусорной Горы. Пока-что...**__` + a gif (`CheckIfReady.cs:617-627`); then each real account is settled atomically under its account monitor: history/statistics/mastery, place ZBS, character-neutral Daily Quest facts/rewards, an alive reward-top-2 loot box, Achievement V2 progress/rewards, character/tier pity, and an immediate save attempt (`CheckIfReady.cs:659-801`). A failed write is critical-logged while the settled in-memory account remains eligible for the 60 s persistence retry. A DooM Guy account additionally evaluates the deterministic round-10 Sirinoks/Дракон unlock for **Приручить дракона**, then rolls its separate place-gated standard module reward; every successful unlock is sent as its own Fortress reward DM (`CheckIfReady.cs:736-746,793-803`).
- With active Madara `Вечное Цукуеми`, final FightPage and LeaderBoard are viewer-specific: each non-Madara player sees themself at place 1 with the exact necessary bonus from that source; Madara sees the real winner and `Все игроки пропустили ход...`. Account payout/history remains based on the real board (`GameUpdateMess.cs:163-188,1106-1112`; `Madara.cs:219-259`).
- The finish path also triggers the web-side callbacks (final broadcast, replay save, AI story — WEB-BACKEND.md §6).

## 9. Accounts & meta surfaces

- Accounts are created lazily on first contact (`UserAccounts.GetAccount` → `CreateUserAccount`, `UserAccounts.cs:78-158`); account meta includes the persistent DooM Fortress unlock/loadout field (`DiscordAccountClass.cs:41`) in addition to the existing profile, currency, mastery, replay, loot, achievement and history fields. Fortress editing itself is web-Home-only; Discord consumes the saved loadout in-game.
- The `stats` embed (`General.cs:191-204`) renders ZBS Points / Тип Пользователя / Всего Игр and the rest of the profile (`StartGameLogic.cs:440-442`).
- **Shop** — the `магазин` command (`Store.cs:24-25`, components in StoreReactions): select `store-select-character` placeholder `Выбрать персонажа` (`StoreReactions.cs:41-42`), buttons `Поднять шанс на 1%`/`на 10%`, `Опустить шанс на 1%`/`на 10%`, `Сбросить все изменения`, `Сбросить все изменения за всех персонажей` with ids store-up-1/store-up-10/store-down-1/store-down-10/store-return-character/store-return-all-characters (`StoreReactions.cs:55-60`); dispatched in `ReactionAddedStore`, guarded by custom-id containing "store" (`StoreReactions.cs:150-179`). All four spends and both refunds validate/mutate under the account monitor, persist before the message confirms, and restore both balance and chance records with a retry error if saving fails (`StoreReactions.cs:196-515`).
- Daily Quests/Loot Boxes/Achievements accrue from Discord games, but Discord only banks them (§8). The web lobby hosts the bilingual 2-of-3 Daily Quest board, free reroll, 3-of-3 mastery box, weekly journey and loot opener; `/achievements` is the permanent collection (`DailyQuestBoard.vue:271-604`; `router.ts:53-57`). Achievement unlock celebrations remain web-only and acknowledgement-backed (`AchievementClass.cs:89-92`; `GameHub.cs:903-1018`). Complete rules: [DAILY-QUESTS.md](DAILY-QUESTS.md) and [ACHIEVEMENTS.md](ACHIEVEMENTS.md).
- Profile widget: `*widget_s` posts the OAuth link (`ServerManagement.cs:124-131`), `*widget` pushes texts/number after authorization (`ServerManagement.cs:138` `ServerManagement.cs:157-165`) — server side in WEB-BACKEND.md §12.

## 10. Claude integration (Geralt's Медитация)

When a **human** Geralt blocks/meditates, the passive resolves a not-yet-revealed enemy and asks `ClaudeHaikuService.GenerateWitcherHintPairAsync` for matching Russian and naturally adapted English clues in one request (`CP:4767-4778`). Missing keys, the 5 s timeout, HTTP/malformed output, or the simulation kill-switch fall back to the canonical `WitcherSensesHints` entry plus its shared-catalog English pair (`CP:4791-4798`; table `Geralt.cs:335-369`). Both generated and static paths enter personal logs as one `PhraseV2` payload, so Discord resolves the current account language without losing the other replay variant (`CP:4800-4802`). Service internals in WEB-BACKEND.md §11.

## 11. Russian / English presentation

The persisted account locale defaults to Russian; the commands **\*язык**, **\*language** and **\*lang** toggle or explicitly select `ru`/`en` (`DiscordAccountClass.cs:27`, `General.cs:128-149`). Ordinary command responses pass through `ModuleBaseCustom`; in-game embeds, transient text and component labels/options are localized at send time (`ModuleBaseCustom.cs:14-18, 61-65`, `HelperFunctions.cs:237-244`). Custom ids and select option values remain canonical.

Character/passive names and descriptions use the shared adapted catalog. Historical phrase pools remain fully intact in Russian; English uses a direct translation where available and otherwise a passive-aware adapted quip rather than exposing untranslated Cyrillic (`GameLocalization.cs:63-178`, `CharactersPhrases.cs:1816,1905`). Geralt's AI/static hint path is locale-aware (`CP:4746-4777`). Full rules: [LOCALIZATION.md](LOCALIZATION.md).

## 12. Known quirks

- The slash/context-menu subsystem and the reaction pipeline are dead code (§1) — the handler names still say "Reaction" but take components.
- `confirm-prefict` is a load-bearing typo (`GameUpdateMess.cs:1497-1499`); the `stats` case toggles the empty page 2 but no button builds it anymore (`GameReactions.cs:302-306`). (Dopa's dead `dopa-attack-select` menu was removed 2026-07-04 — finding **m23**; his Макро second action runs through the normal attack/block branches, `GameReactions.cs:329-352` `GameReactions.cs:730-744`.)
- The `Дебаг` button (`GameUpdateMess.cs:1738-1741`) and early Авто Ход unlock are hardcoded to two owner Discord IDs (`GameUpdateMess.cs:1558` `GameUpdateMess.cs:1759-1760`).
- Anything DM'd mid-round via `SendMsgAndDeleteItAfterRound` disappears at round end by design — don't use it for persistent info.
- `PreferWeb`/web players silently skip every Discord render (§4); when debugging "the bot stopped updating me", check that flag first.
