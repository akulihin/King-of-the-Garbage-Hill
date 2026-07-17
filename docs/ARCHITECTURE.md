# King of the Garbage Hill — Architecture

> Code-verified against the working tree of 2026-07-14 (v4.5.6). Companion docs: [GAME-DESIGN.md](GAME-DESIGN.md), [CHARACTERS.md](CHARACTERS.md), [AUDIT-FINDINGS.md](AUDIT-FINDINGS.md); account progression: [DAILY-QUESTS.md](DAILY-QUESTS.md), [ACHIEVEMENTS.md](ACHIEVEMENTS.md); interface deep-dives: [WEB-BACKEND.md](WEB-BACKEND.md), [WEB-CLIENT.md](WEB-CLIENT.md), [DISCORD-INTERFACE.md](DISCORD-INTERFACE.md); bot policy review: [BOT-AI-DESIGNER-REVIEW.md](BOT-AI-DESIGNER-REVIEW.md).

## 1. Process topology

One process hosts both frontends:

```mermaid
flowchart LR
    subgraph proc[Single .NET 10 process]
        DB[Discord bot<br/>Discord.Net 3.18<br/>CommandHandling] --- GLOB[(Global singleton<br/>GamesList / FinishedGamesList<br/>WinRates)]
        WEB[Kestrel<br/>GameController /api/game<br/>GameHub /gamehub SignalR] --- GLOB
        TIMER[CheckIfReady<br/>100ms Timer] --- GLOB
        NOTIF[GameNotificationService<br/>broadcast timer] --- GLOB
    end
    DISCORD((Discord)) <--> DB
    BROWSER((Vue 3 client)) <--> WEB
    NOTIF -->|state to group game-id| BROWSER
```

- **Program.cs** starts the Discord bot thread and Kestrel in parallel; port via `KOTGH_PORT` (default 80).
- **Global.cs** holds the live `GamesList` — the single source of truth shared by both frontends. No database: accounts are flat JSON files (`DataBase/UserAccounts/discordAccount-{id}.json`) loaded into a `ConcurrentDictionary` at startup (`UsersDataStorage.cs`).
- **DI**: Lamar; anything implementing `IServiceSingleton`/`IServiceTransient` is auto-registered (`AddSingletonAutomatically`).
- **Game loop driver**: `CheckIfReady.LoopingTimer` ticks every 100 ms over all games (`CheckIfReady.cs:63-74, 917-943`), guarded by an interlocked re-entrancy latch and per-game `IsCheckIfReady` flag.
- **Web push**: `GameNotificationService` broadcasts mapped state to SignalR group `game-{gameId}`; `GameStateMapper` produces per-viewer DTOs (each player sees only what they should).
- Deploy: `deploy_to_prod` → build, tar, scp to EC2, systemd unit `kotgh`.

## 2. Core state model

```mermaid
classDiagram
    class GameClass {
        RoundNo / GameMode / Teams
        PlayersList: List~GamePlayerBridgeClass~
        WinnerPlayerIds: List~Guid~
        GlobalLogs / AllGameGlobalLogs
        WebFightLog / ReplayRounds
        ExploitPlayersList / TotalExploit / ExploitClosed
        IsRoundTransitionPaused / TransitionDeadlineUtc / StateRevision
        Phrases: CharactersUniquePhrase
    }
    class GamePlayerBridgeClass {
        DiscordId / DiscordUsername / PlayerType
        GameCharacter: CharacterClass
        FightCharacter: CharacterClass
        Status: InGameStatus
        Passives: PassivesClass
        Predict: List~PredictClass~
        AiPlaystyle / AiKnowledge
        MinusPsycheLog()
    }
    class BotKnowledgeState {
        VisibleGlobalLogsByRound
        Opponents: Dictionary~Guid,BotOpponentKnowledge~
        PredictionEvidence
    }
    class BotOpponentKnowledge {
        public place/action/result history
        viewer-fight class/Justice/edge observations
    }
    class CharacterClass {
        Name / Tier / Passive: List~Passive~
        Int/Str/Speed/Psyche + ForOneFight
        SkillMain / SkillExtra / multipliers
        Moral / BonusPointsFromMoral
        Quality resists + bonuses
        Justice: JusticeClass
    }
    class InGameStatus {
        Score / ScoresToGiveAtEndOfRound
        PlaceAtLeaderBoard + History
        IsBlock/IsSkip/IsReady/WhoToAttackThisTurn
        IsWonThisCalculation / WhoToLostEveryRound
        personal logs / ForOneFightMods
    }
    class PassivesClass {
        per-character state fields
        DoomGuy: per-game modules/objectives
        per-player marks (virus, cancer, cat...)
        IsDead / DeathSource
        AchievementTracker
    }
    GameClass --> GamePlayerBridgeClass
    GamePlayerBridgeClass --> CharacterClass : GameCharacter
    GamePlayerBridgeClass --> CharacterClass : FightCharacter (DeepCopy each round)
    GamePlayerBridgeClass --> InGameStatus
    GamePlayerBridgeClass --> PassivesClass
    GamePlayerBridgeClass --> BotKnowledgeState : viewer-scoped AI memory
    BotKnowledgeState --> BotOpponentKnowledge
    CharacterClass --> InGameStatus : shared Status ref
```

### GameCharacter vs FightCharacter — the rule that breaks people

Each bridge holds **two** `CharacterClass` instances (`GamePlayerBridgeClass.cs:30-31`):

- **`GameCharacter`** — persistent truth. All lasting changes (`AddIntelligence`, `AddExtraSkill`, `AddMoral`…) go here.
- **`FightCharacter`** — snapshot taken **once per round** at the start of calculation (`DeepCopyGameCharacterToFightCharacter`, `DoomsdayMachine.cs:135-141,243`). The fight engine (`CalculateRounds.cs`) reads **only** `FightCharacter`.

`DeepCopy` is `MemberwiseClone` + explicit deep-copy of the `Passive` list (`CharacterClass.cs:36-48`). Consequences:
- Value-type stats are independent per copy.
- **`Status` and `Justice` are intentionally the SAME instance** on both copies — Justice edits work from either side; anything reached through `Status` is shared.
- If you add a mutable reference field (List/Dictionary) to `CharacterClass`, you **must** add a deep-copy line, or both copies will share it.

Reward state deliberately follows the same lifetime split. `PassivesClass.AchievementTracker` is per-match and JSON-ignored; Daily Quests read its character-neutral observations only at settlement. `DiscordAccountClass.Achievements`, `Quests`, `PendingLootBoxes`, ZBS, each `CharacterChances.LootBoxBonusPercentagePoints`, and `LootBoxCharacterQueue` are persistent account state (`PassivesClass.cs:155-165`; `DiscordAccountClass.cs:29-82`; quest facts `QuestClass.cs:358-412`). `GamePlayerBridgeClass.IsLootBoxCharacterReward` is only an ephemeral draft-lock marker after the FIFO head is assigned (`GamePlayerBridgeClass.cs:80`). The final payout attaches the persistent `AchievementData` reference to the player only so the personalized finished-game mapper can carry that account's unacknowledged unlocks (`CheckIfReady.cs:739-776`; `GameStateMapper.cs:174-209`).

Strict-bot knowledge has a third, separate lifetime: `GamePlayerBridgeClass.AiKnowledge` persists on the seat for one match and is neither part of `CharacterClass` nor copied into `FightCharacter`. After a resolved round, `BotInformation.CaptureVisibleRound` freezes only what that viewer could retain: sanitized global logs, public places/actions/results and, when the bot participated in the fight, the class/Justice/fight-edge detail shown to that participant. `BotPredictionEvidence` distinguishes fallible hypotheses from exact player-facing reveals. The memory is not serialized to clients or replays. L2/L3 action and prediction code consumes this projection plus the bot's own current/previous personal logs and owner-visible leaderboard annotations; direct opponent `GameCharacter`, `Passives`, score, Justice, private logs and current action flags are outside the boundary (`BotsBehavior.HandleFairBotAttack`; `CharacterPassives.HandleFairBotPredict`).

**ForOneFight overrides** (sentinel `-228`): `SetIntelligenceForOneFight`, `SetStrengthForOneFight`, `SetSpeedForOneFight` (+`AddSpeedForOneFight` delta), `SetPsycheForOneFight`, `SetSkillForOneFight`, `SetJusticeForOneFight`. Each records a `ForOneFightMod` (for the web fight animation) and raises a flag on shared `Status`; `ResetFight` clears the override on **both** copies after every single fight (`DoomsdayMachine.cs:79-123`).

Rules of thumb (violations are real bugs — see CLAUDE.md):
- ForOneFight overrides must be set on **FightCharacter** (CalculateRounds reads it). Justice is the one exception (shared).
- Stat *reads* inside before-fight handlers should use **FightCharacter** so earlier overrides are respected.
- Persistent changes go to **GameCharacter**; they take effect next round (or immediately for anything read from GameCharacter, e.g. skill gains land in the *current* fight only via the class-perk calls that use GameCharacter directly).

## 3. Passive hook execution order

All hooks live in `CharacterPassives.cs` as `switch (passive.PassiveName)` dispatchers. Call sites verified:

| # | Hook | Called from | When / notes |
|---|---|---|---|
| 1 | `HandleEventsBeforeFirstRound` (114) | game creation / draft & ARAM confirm | initial marks, L assignment, stat rewrites |
| 2 | `HandleDefenseBeforeFight` (467) | fight loop, defender first (`DoomsdayMachine.cs:632`) | defensive ForOneFight overrides |
| 3 | `HandleAttackBeforeFight` (1082) | fight loop, after defense (`DoomsdayMachine.cs:643`) | offensive overrides — sees defender's mods |
| 4 | block path | `DoomsdayMachine.cs:684-770` | attacker −1 bonus; Близнец copies the highest blocked attacker's real Justice here, otherwise the defender gains generic next-round Justice; then hooks 8/9 run for the defender |
| 5 | skip path | `DoomsdayMachine.cs:779-815` | hook 9 for defender |
| 6 | `HandleDefenseAfterFight` (923) | after resolution (`DoomsdayMachine.cs:1510`) | defender-only reactions (counter effects) |
| 7 | `HandleAttackAfterFight` (1828) | `DoomsdayMachine.cs:1515` | attacker-only rewards/steals |
| 8 | `HandleDefenseAfterBlockOrFight` (822) | fight `DoomsdayMachine.cs:1511` + block `DoomsdayMachine.cs:765` | block-inclusive defensive effects |
| 9 | `HandleDefenseAfterBlockOrFightOrSkip` (906) | fight `DoomsdayMachine.cs:1512` + block `DoomsdayMachine.cs:766` + skip `DoomsdayMachine.cs:811` | always-trigger defensive effects |
| 10 | `HandleCharacterAfterFight` (2485) | both sides, all outcomes incl. own block/skip (`DoomsdayMachine.cs:513,763-764,809-810,1518-1519`) | per-interaction cleanup/rewards |
| 11 | `HandleShark` (7739) | after each resolved fight (`DoomsdayMachine.cs:1521`) | "Лежит на дне" neighbor check |
| 12 | `HandleEndOfRound` (3766) | once per round (`DoomsdayMachine.cs:1738`) | flags still set; round-end effects; Gordon's resumable release decision is checked immediately afterward |
| 13 | `HandleNextRound` (5222) | after `RoundNo++` (`DoomsdayMachine.cs:1833`) | per-round setup, trigger rolls |
| 14 | `HandleNextRoundAfterSorting` (6443) | after sort/swaps/drops (`DoomsdayMachine.cs:2080`) | position-dependent effects |
| 15 | `HandleBotPredict` (7401) | `DoomsdayMachine.cs:2088` | difficulty-routed bot prediction heuristics; L2/L3 use the fair viewer projection |

Special dispatchers outside the table include `HandleJews`, `HandleOctopus`, unknown_bug's `HandleEventsBeforeCalculation`/resolved-fight observer, and `HandleRumblingAfterFights` (`CharacterPassives.cs:3687`; call `DoomsdayMachine.cs:1633`). `UnknownBug.SelectStreamTarget` chooses one deterministic primary target after every authoritative action-isolation layer is finalized. `RecordResolvedFight` is the sole authoritative observer for resolved-fight payload stacks: it copies the selected target's win and records unknown_bug defeating the active Exploit carrier from either side without fabricating a second combat result. It runs before `TryCommitExploit`, so a direct attacking win is already in the pot and commit must not add it again. Block/Skip exits may still close the objective without a resolved-win stack; Harem queue cancellation excludes unknown_bug, while Eternal Tsukuyomi and the Kratos event preserve its real action rather than creating another no-fight commit path. Naruto adds an explicit cross-pipeline coordinator rather than duplicating four passive cases: `InitializeTeam` runs before the first passive hook; `ResolveHaremQueues`/`TryCancelHaremFights` operate on finalized and dynamically expanded target queues; `SnapshotJustice` feeds per-fight Расенган; and `SettleShadowClones` is called immediately after Rumbling as the second post-fight settlement (`UnknownBug.RecordResolvedFight` / `TryCommitExploit`; `Naruto.cs`; `DoomsdayMachine.cs:414-424`; `DoomsdayMachine.cs:534-565`; `DoomsdayMachine.cs:1633-1634`). Further per-character logic is embedded in `CheckIfReady`, `GameReactions`, `BotsBehavior`, `GameUpdateMess`, `CharacterClass`, `InGameStatusClass` and `GamePlayerBridgeClass`.

TheBoys adds two ordered boundaries around those ordinary hooks. After finalized Harem/action queues and the Kratos action override, `TheBoys.DisablePassivesBeforeFights` clears every non-Block/non-Skip Живое Оружие target's persistent and already-created fight-snapshot passive lists before the fight loop/Madara/Naruto snapshots. During each fight, `TheBoys.ApplyKillingCoupleJustice` runs only after both ordinary before-fight hooks and DooM module overrides, but before Block/Skip resolution and `CalculateStep1`; it first proves that the fight will resolve, then evaluates TooGood/TooStronk without Justice and transfers the dangerous enemy's remaining Justice. unknown_bug is rejected at both boundaries (`DoomsdayMachine.CalculateAllFights`; `TheBoys.cs`).

Salldorum also has a readiness-stage coordinator: unless Вечное Цукуеми already erased the action, `Salldorum.ResolveShenDashes` consumes the next real attack. A higher target selects its exact pre-dash cell; the coordinator mutates the live order, stores the cell through the following action round, arms the current-round random-target magnet and replaces—never appends—the first real target for each crossed player's existing action. `ApplyShenPositionHolds` runs after readiness movers and after end-sort movers, with Ziggurat locks authoritative. Madara/Naruto sanitation is repeated over the redirected queues before Madara's incoming-attacker re-snapshot. A same-cell aged cola pickup is queued in passive state; its +5 Speed is applied to `FightCharacter` by the ordinary defense/attack-before hooks after DeepCopy (`CheckIfReady.cs` readiness pipeline; `DoomsdayMachine.cs` sort pipeline; `Salldorum.cs` coordinators; `CharacterPassives.cs` before-fight hooks).

Gordon adds a resumable inter-round boundary rather than another passive hook. Immediately after `HandleEndOfRound`, `GordonFreeman.PrepareHalfLifeSettlement` may pause a failed human Halflife 3 release review for 20 seconds. `DoomsdayMachine` stores that game's `ReplayRoundDto` and stopped calculation watch in a per-game continuation, then returns before action/status reset, regular-score settlement, replay finalization, `RoundNo++`, next-round hooks and timer restart. `GameClass.IsRoundTransitionPaused`, `TransitionDeadlineUtc` and monotonic `StateRevision` expose the pause to both frontends. On later ticks, `CheckIfReady` tries `ResumePendingRound` before ordinary readiness; an unresolved game is skipped while the loop continues processing other games, and a submitted decision or expired deadline (default: freeze) enters the same `CompleteRoundAsync` tail exactly once (`DoomsdayMachine.cs:166-187,1738-1754`; `CheckIfReady.cs:1103-1115`; `GameClass.cs:63-65`).

**Passive-matching convention**: logic keys on **exact `PassiveName` strings** from `characters.json`; some places key on **character `Name`** instead. Both are stringly-typed — renames in JSON silently orphan code branches (this has happened; see AUDIT-FINDINGS C1, m1, m3 and D4).

## 4. Logging system

- **Personal logs** (`Status.AddInGamePersonalLogs`, auto-called by most stat mutators): visible to that player only. Stat methods log by default — pass `isLog: false` to suppress; do **not** double-log.
- **Global logs** (`game.AddGlobalLogs`): all players; per-round buffer is re-sorted at end of round (`SortGameLogs`) then reset. `HiddenGlobalLogSnippets`/`KiraHiddenLogSnippets` strip fight lines for non-admins per feature.
- **Phrases** (`Game/MemoryStorage/CharactersPhrases.cs`): `PhraseClass` objects with index-paired canonical `PassiveLogRus` and authored `PassiveLogEng` arrays loaded from `DataBase/phrases.en.json`. `.SendLog` stores both selected renderings in a replay-safe payload; media stores paired fields; consumed variants are removed from both arrays together (`Helpers/PhraseLocalization.cs`).
- **FightingData** (`Status.AddFightingData`): verbose per-fight numeric trace (admin/debug view + web fight animation source).

## 5. Score plumbing

`InGameStatus.Score` is private; mutation paths:
- `AddRegularPoints` → round buffer → `CombineRoundScoreAndGameScore` multiplies (×1/×2/×4; Подсчет override) and commits (`InGameStatusClass.cs` `AddRegularPoints`, `GetRoundScoreMultiplier`, `CombineRoundScoreAndGameScore`).
- `AddWinPoints` = ordinary fight-win regular points, except unknown_bug's own base win point is discarded. PointFunnel copies are outcome-observer calls to `AddRegularPoints(1)` and therefore ignore the source winner's payout transforms (`InGameStatusClass.AddWinPoints`; `UnknownBug.RecordResolvedFight`).
- `AddBonusPoints` → immediate, floor-at-0 unless "Никому не нужен" (`:221-233`); `HardKittyMinus` bypasses the floor.
- `SetScoreToThisNumber` (Октопус restore etc.).
`ScoreEntries`/`PreviousRoundScoreEntries` feed the web score breakdown; `GetBonusPointsEarnedThisRound` feeds passives like Выгодная сделка.

Джон Сноу's **Король Сервера** is implemented at the central bonus receiver boundary: `AddBonusPointsCore` doubles positive and negative amounts before logging/flooring and labels them королевские, but only while the visible King passive exists and Jon is not at place 4. His leaderboard rules are applied by `Naruto.OrderLeaderboard` after alive/dead/score sorting and by Eren's projected Rumbling order: an active Castle hold restores exact place 4, otherwise the King floors at place 3. Explicit movers still run afterward and may break those score-derived places (`InGameStatusClass.AddBonusPointsCore`; `JonSnow.ApplyLeaderboardRules`; `ErenYeager.ProjectRoundEndLeaderboard`).

unknown_bug adds a receiver invariant above those ordinary paths: a negative regular, bonus or direct score mutation is a no-op, so its committed and buffered score never decrease. Transfer-shaped callers also filter unknown_bug before recording a debt, loss log or paired taker credit, rather than minting the nominal amount from an immune target. Its `GetRoundScoreMultiplier` branch ignores Толя's reduction and retains the normal round ×1/×2/×4. Negative-effect immunity follows the same receiver-boundary pattern across the central character/resource mutators and direct target branches: drains, marks, kills/fight fan-out, action and position rewrites are rejected before source rewards/progress are created; match-wide Rumbling also filters it from the victim set. The resolved-fight pipeline then reapplies AutoWin after every terminal result replacer, guaranteeing a Bug win from either side (`UnknownBug.Is`; `InGameStatusClass`; `CharacterClass`; `GamePlayerBridgeClass`; readiness/passive guards; `DoomsdayMachine` terminal outcome; `CharacterPassives.HandleRumblingAfterFights`).

Naruto's clone settlement uses the same canonical `GetRoundScoreMultiplier` as ordinary round commit and Eren's projected Rumbling order. `DrainSettledScoreForTransfer` converts committed + pending clone score exactly once; `AddSettledScore` credits the original without exposing the historical pot as fresh Цукуеми earnings. After dispersal, `ResolveScoreSuccessor` redirects delayed liabilities (Saitama/Octopus/Mitsuki/virus/Tsukuyomi) to the original that inherited the pot, while later genuinely new clone rewards are suppressed. Central `OrderLeaderboard` sorts every dead seat below every living seat and keeps dispersed clones last (`InGameStatusClass.cs:253-329`; `Naruto.cs` `ResolveScoreSuccessor`/`SettleShadowClones`/`OrderLeaderboard`).

Score mutation has one central floor contract: `AddBonusPointsCore` and `AddScoreWithMultiplier` clamp ordinary characters at zero; HardKitty bypasses by passive and only audited lethal effects call `AddBonusPointsIgnoringFloor` (Kira arrest, Geralt pitchfork). Dead ordinary seats do not flush buffered regular points; the unknown_bug receiver guard is defense in depth against any stale death-state cleanup reducing its score. `GameClass.WinnerPlayerIds` records the authoritative declared winner(s) independently of raw place so profile settlement, special wins and simulation reports share one result source (`InGameStatusClass`; `DoomsdayMachine.CompleteRoundAsync`; `CheckIfReady.HandleLastRound`).

## 6. Web layer (state → screen)

```
GameClass ──GameStateMapper.MapPlayer(viewer-specific)──▶ GameStateDto
   │                                                        ├─ PlayerDto (per player: stats*, viewer-scoped flags)
   │                                                        └─ PassiveAbilityStatesDto (owner widgets only)
   └─GameNotificationService (timer) ─▶ SignalR group game-{id} ─▶ Vue store (Pinia) ─▶ PlayerCard/SkillsPanel/Game.vue
```

- Mapper keys widgets on **PassiveName** (`GameStateMapper.cs:367-830`) and a few on character Name. Cross-character marks stay server-side and are projected only to the passive owner through their widget/leaderboard annotations; affected players receive no `…OnMe` state unless a mechanic explicitly declares the effect public (`GameStateMapper.cs:356-365,360-823`; `GameUpdateMess.cs:219-811`).
- Some character state rides directly on `PlayerDto` instead of `PassiveAbilityStatesDto`: DeathNote, PortalGun, owner-only `TerminalState`, TsukuyomiState, choice flags (Darksci/YoungGleb/Dopa). `IsTerminalMode` and every `HasTerminalMarker` bit are emitted only into that owner's projection; the neutral naming is intentional so the public client bundle contains no secret identity.
- Naruto's `PlayerDto.IsNarutoAlly` is viewer-scoped rather than a widget: it is true only for the other two members of the requesting Naruto's initialized trio, allowing Vue to suppress illegal attack affordances without revealing anything to unrelated viewers or spectators (`GameStateMapper.cs` `MapPlayer`; `GameStateDto.cs` `PlayerDto`).
- Gordon's `PassiveAbilityStatesDto.Gordon` is likewise owner-only: it carries crowbar/fight progress, wake state, active headcrab targets and countdowns, zombie/rescue totals, and the Halflife 3 release/decision payload including whether Подсчет disabled the super multiplier. The corresponding 🦀/🧟 leaderboard markers are rendered only in Gordon's viewer projection; affected players and spectators receive no identifying mark (`GameStateMapper.cs` Gordon projection; `GameUpdateMess.cs` `CustomLeaderBoardBeforeNumber`).
- Jon's `PassiveAbilityStatesDto.JonSnow` is owner-only and carries Skill/228 progress, transformation/castle status, removable Bastard Intelligence, hold countdown, loyalty victories, spent-Watch state and the two weakest player names. The `🐺` weakest marks are the deliberate public exception: every leaderboard projection receives them because the design explicitly places those marks in the table. Before the hidden Watch fires, the widget renders no hint; its empty passive description is never exposed (`GameStateMapper.cs` Jon projection; `GameUpdateMess.cs` `CustomLeaderBoardBeforeNumber`; `PlayerCard.vue` Jon block).
- Frontend mirror: `signalr.ts` `PassiveAbilityStates` (camelCase 1:1), widgets in `PlayerCard.vue`, per-member skill UI in `SkillsPanel.vue` (TheBoys special-cased), sounds keyed by character name in `sound.ts`/`store/game.ts`.
- Web auth via Discord ID as string; web-only accounts via `RegisterWebAccount` (`signalr.ts:1488-1494`). Spectate + Replay: format-v2 `ReplayService` stores action-locked pre-fight state, pre-transition combat logs, and an atomic post-setup score/place/death result for each same-numbered round. `HandleLastRound` refreshes that existing result and emits only explicit final log suffixes; legacy boundary files are aligned by the Vue adapter (`ReplayService.cs:35-164`; `CheckIfReady.cs:845-853`; `replay.ts:21-120,133-268`). Games containing unknown_bug are deliberately never persisted or story-generated; replay loaders reject legacy files containing either identity before returning/listing them (`GameNotificationService`; `ReplayService.ContainsPrivateRoster`; `GameStoryService.GenerateStoryAsync`). Normal post-game AI stories consume captured round numbers and fight pairs, with settlement kept as a non-round epilogue, so normal stories stop at 10 while Kratos extensions remain visible (`GameStoryService.cs:168-314`).
- This section is the overview only — the full interface catalogs live in [WEB-BACKEND.md](WEB-BACKEND.md) (every endpoint/hub method/event + visibility rules), [WEB-CLIENT.md](WEB-CLIENT.md) (routes/stores/contract/widgets), and [DISCORD-INTERFACE.md](DISCORD-INTERFACE.md) (commands/custom-ids/DM flow).
- Account rewards and the character store use separate owner-only request paths rather than the 300 ms game broadcast: game-end hooks populate the account under its monitor; quests/achievements/loot retain their snapshot and acknowledgement contracts; loot atomically mutates ZBS plus `CharacterChance`, `SeenCharacters` and the FIFO `LootBoxCharacterQueue`. `RequestStore` returns only the caller's seen-character effective roll weights, distinguishes their permanent loot component, and all paid spends/refunds save before publishing the replacement snapshot (`GameHub` loot/store handlers; `WebGameService` character-store block). Full reward rules: [DAILY-QUESTS.md](DAILY-QUESTS.md) and [ACHIEVEMENTS.md](ACHIEVEMENTS.md).
- **Turn actions** (`WebGameService.{Attack,Block,AutoMove,ConfirmSkip}`, exposed via `GameHub`/`GameController`) set `IsReady`/`ConfirmedSkip` — the web analogue of Discord's fight buttons. They enforce the level-up gate Discord gets for free from `MoveListPage 3` (which hides the fight controls until points are spent): `WebGameService.LevelUpGate` blocks all four while `LvlUpPoints > 0`, mirrored client-side by the `mustSpendLevelUp` store computed (`store/game.ts`) that disables the buttons and tightens `:can-attack` in `Game.vue` (finding M15). `LevelUp`/`ChangeMind` stay ungated so points can be spent / un-readied.
- DooM Guy splits state deliberately: `DiscordAccountClass.DoomFortress` is persistent unlocked/equipped account data (`DiscordAccountClass.cs:41`), while `PassivesClass.DoomGuy` is the per-match active modules, BFG/Railgun charges, finite Rune grant counters, Counter-attack expiry marks, temporary Shark stance and objective progress (`PassivesClass.cs:264-265`, `DoomGuy.cs` `DoomGuyState`). Every bridge-creation or human seat-substitution path initializes the match snapshot from the current account loadout (`DoomGuy.cs` `InitializeForGame`; web paths `WebGameService.cs:317`, `WebGameService.cs:424`, `WebGameService.cs:497`, `WebGameService.cs:1129`, `WebGameService.cs:1159`). The mutable fight queue carries separate BFG-direction and Railgun-bypass metadata: BFG extends a winning branch and suppresses Step-3 randomness, while Railgun pre-expands one board side and bypasses each target's Block/Skip without skipping the ordinary per-fight hooks/reset (`DoomsdayMachine.cs:487-538,810-881`).
- Эрен splits owner state (`PassivesClass.Eren`, including Attack-Titan cooldown) from hatred marks stored on each affected player's `ErenHatredMark`; the owner-only DTO aggregates those per-player marks (`PassivesClass.cs:267-274`; `GameStateMapper.cs:389-410`). His round-wide Titan boost is implemented as a per-fight `FightCharacter` override reapplied in both attack/defense hooks, because `ResetFight` correctly clears each override after one fight (`CharacterPassives.cs:62-72,517-521,1119-1123`).
- Эрен bot policy: on round 10, L0/L1 retain the historical scripted target override after legitimate unable-to-act states return. L2/L3 do not inspect Эрен's hidden identity: L3 may associate the public warning with a unique repeated-place-6 pattern and both fair levels act only on confidence-weighted prediction evidence (`BotsBehavior.TryForceRumblingAttack`; `BotsBehavior.InferPublicRulePatterns`; `BotsBehavior.ApplyFairCharacterPreference`).
- Мадара keeps match state in `PassivesClass.Madara`, not `CharacterClass`, so its attacker sets and illusion-target dictionary are persistent per player and do not belong in `CharacterClass.DeepCopy` (`PassivesClass.cs:169-173`; `Madara.State`). His combat Skill 100 is written to `FightCharacter` in both before-fight hooks (`CharacterPassives.cs:478-481,1128-1131`).
- Мадара bot policy: on round 8 he stays action-locked and the readiness flow retains its 30-second reaction delay. Frozen L1 keeps the historical scripted exact Madara prediction; L2/L3 receive no hidden player-id answer and can record only evidence supported by the ordinary visible prediction path (`Madara.ForceRoundEightBotPrediction`; `BotsBehavior.HandleBotBehavior`; `CharacterPassives.HandleFairBotPredict`; `CheckIfReady.TickAsync`).
- Active `Вечное Цукуеми` is an **authoritative round-10 Skip plus final per-viewer projection**: every ordinary queue is cleared before combat and fight conversions/injections are suppressed. unknown_bug is immune to that rewrite and retains its submitted action; the existing Gordon wake reservation does the same. Madara, Gordon-with-reservation and unknown_bug receive the real ending, with Bug retaining its actual fight log; every other non-Madara viewer receives output-only synthetic wins/place/score-source, with their fight animation completing before the podium/theme. Spectators receive no result data (`Madara.PrepareEternalTsukuyomiRound`; `GordonFreeman.SeesEternalTsukuyomiReality`; `UnknownBug.Is`; `GameStateMapper.ApplyEternalTsukuyomiProjection`; `Game.vue`; `FightAnimation.vue`). A replay/story is shared rather than viewer-scoped, so activated games still suppress both artifacts (`CheckIfReady.HandleLastRound`; `GameNotificationService`).

## 7. Per-character plumbing pattern (the "~14 files")

For character **X** with passive "P" (details per character in CHARACTERS.md):

1. `DataBase/characters.json` — name, stats, tier, avatar, passives (`PassiveName`/`PassiveDescription`/`Visible`/`Standalone`).
2. `Game/Characters/X.cs` — nested state classes (counters, cooldowns, trigger schedules). ⚠ file/class names don't always match the character (`Panth.cs`→class `Spartan`→"Загадочный Спартанец в маске", `Mitsuki.cs`→"Злой Школьник", `Shark.cs`→"Братишка"; `UnknownBug.cs` owns the renamed ultra-secret character's identity, privacy helpers and mechanics).
3. `Game/Classes/PassivesClass.cs` — instances of those state classes per player; also per-player marks other characters put on you.
4. `Game/MemoryStorage/CharactersPhrases.cs` — `PhraseClass` fields + registration.
5. `Game/GameLogic/CharacterPassives.cs` — `case "P":` in the relevant hooks (§3).
6. `Game/GameLogic/DoomsdayMachine.cs` — only for core-fight-mechanics characters (block/skip conversions, fight injection, damage multipliers, position swaps).
7. `Game/GameLogic/CheckIfReady.cs` — forced attacks / turn-flow injection / last-round scoring.
8. `Game/ReactionHandling/GameReactions.cs` — level-up overrides (`GetLvlUp` and friends), moral-button overrides.
9. `Game/GameLogic/BotsBehavior.cs` — bot moral thresholds + action selection when a bot plays X.
10. `Game/DiscordMessages/GameUpdateMess.cs` — leaderboard icons/prefixes (`CustomLeaderBoardBeforeNumber`/`AfterPlayer`).
11. `API/DTOs/GameStateDto.cs` — `XStateDto` + owner-only member on `PassiveAbilityStatesDto` (or an explicitly viewer-scoped `PlayerDto` flag).
12. `API/Services/GameStateMapper.cs` — `case "P":` in the owner-widget switch. Do not serialize another character's mark to the affected player unless the design explicitly makes it public.
13. `Web/VueClient/src/services/signalr.ts` — TS interface mirror.
14. `Web/VueClient/src/components/PlayerCard.vue` (+`SkillsPanel.vue`, `sound.ts`) — widget/UI/audio.

DooM Guy exercises the expanded form of this pattern: its persistent Home surface also touches `DiscordAccountClass`, `GameHub` meta methods, `store/game.ts`, `pages/Home.vue` and `components/Home/FortressOfDoom.vue`; BFG and Railgun mutate the live target queue inside `DoomsdayMachine` so every secondary fight still traverses the standard hook/reset pipeline (`DoomsdayMachine.cs:487-538,810-881`). Runtime copies used by **Щит-акула** and **Приручить дракона** add existing load-bearing passive names rather than duplicating their dispatch logic; Shark's copy is removed at round end, Dragon's remains for the match.

Naruto is another expanded form: roster eligibility and two strict-bot seat conversion touch `StartGameLogic`/`WebGameService`; sibling legality is enforced independently in Discord/web menus, the shared action handler, bot target selection, forced-target sanitization and the core loop; final death/score ownership crosses `DoomsdayMachine`, `CheckIfReady` and `InGameStatus`. The helper identity is the original player's Guid stored in all three `PassivesClass.Naruto` states; the clones receive fresh deserialized `CharacterClass` and `PassivesClass` instances so no status, stat, level-up or passive state is shared. Count-based L0/L1 action odds add the two living illegal siblings back as virtual attack slots only, so the smaller legal menu does not inflate Block (`Naruto.cs` `InitializeTeam` / `GetBotActionTargetSlotCount`).

Gordon exercises a different expanded form: `GordonFreeman.cs` owns both the holder state and the headcrab mark placed on other players; `DoomsdayMachine` counts only fights that survive Block/Skip and owns the resumable Halflife 3 settlement boundary; `CheckIfReady` resumes the parked continuation; `GameReactions` and `GameUpdateMess` expose wake/release controls and private crab markers to Discord; `GameController`, `GameHub` and `WebGameService` provide REST/SignalR parity; and Vue adds the owner widget plus a focus-trapped release decision/wait overlay. The decision serial is part of the contract so a reconnect or duplicate click cannot resolve a newer release review (`GordonFreeman.cs`; `WebGameService.ResolveHalfLife3Decision`; `HalfLife3Transition.vue`).

Jon Snow exercises the cross-pipeline score/position/death form: `CharacterClass` Skill mutators own the immediate 228 transformation boundary; `InGameStatusClass` owns royal bonus multiplication; `DoomsdayMachine` injects per-resolving-fight difficulty Justice and observes each resolved result; `CheckIfReady` redirects finalized weak-target queues and evaluates the final Castle place; `Naruto.OrderLeaderboard` and Eren's projection apply the exact-place/floor rules; the existing Kira/Kratos/Monster/Rumbling kill sites call the one-shot immediate resurrection helper while retaining their source rewards and counters. The public `🐺` marker is explicitly different from ordinary owner-only cross-character marks (`JonSnow.cs`; `CharacterPassives.cs`; `DoomsdayMachine.cs`; `CheckIfReady.cs`).

## 8. File map (backend)

| File | Role |
|---|---|
| `Game/GameLogic/CharacterPassives.cs` | all passive hooks (§3 line map) |
| `Game/GameLogic/BotsBehavior.cs` | bot action pipeline; frozen legacy L1 plus viewer-scoped L2/L3 tactics, persistent plans and fair attack/Block selection (`HandleBotBehavior`, `HandleFairBotAttack`) |
| `Game/GameLogic/BotInformation.cs` | L2/L3 visibility boundary and persistent resolved-round observation helpers (`CaptureVisibleRound`, `VisibleCurrentGlobalLogs`) |
| `Game/GameLogic/DoomsdayMachine.cs` | fight execution + round pipeline |
| `Game/GameLogic/CheckIfReady.cs` | turn loop, forced actions, game end |
| `Game/GameLogic/CalculateRounds.cs` | pure fight math (3 steps) |
| `Game/GameLogic/StartGameLogic.cs` | rolls (normal/ARAM/draft), tiers |
| `Game/Classes/CharacterClass.cs` | stats, skill, moral, justice, quality |
| `Game/Classes/InGameStatusClass.cs` | score, place, flags, personal logs |
| `Game/Classes/PassivesClass.cs` | per-player passive state container |
| `Game/Classes/GameClass.cs` | game container, exploit roll |
| `Game/Classes/GamePlayerBridgeClass.cs` | account↔player bridge, `MinusPsycheLog`, `AiPlaystyle` and viewer-scoped `AiKnowledge` |
| `Game/Classes/AchievementClass.cs` | V2 definitions, match observations, best-single-match evaluation and unlock rewards |
| `Game/Classes/QuestClass.cs` | Daily Quest V2 catalog/metrics/selection/rewards/migration plus server-owned loot odds, pity and idempotent opening |
| `Game/ReactionHandling/GameReactions.cs` | buttons: lvl-up, moral, predictions |
| `Game/DiscordMessages/GameUpdateMess.cs` | Discord rendering |
| `API/Services/GameStateMapper.cs` | web DTO mapping |

Legacy/dead code is catalogued in AUDIT-FINDINGS; the m6 batch (2026-07-04) deleted the known dead files (LolGod.cs, single-L Saldorum.cs and their orphaned passive cases/state/phrases, CraboRack.BokoBoole). `GameDesign.txt` still holds unbuilt future characters by design.

## 9. Conventions & pitfalls (verified)

- Namespaces `King_of_the_Garbage_Hill.*`; JSON CamelCase; mixed RU/EN comments; RU game-term strings are load-bearing identifiers.
- `Passive` has a 3-arg constructor + `Standalone` property (object-initializer style only for `Standalone`).
- No `AddJustice` — only `AddJusticeForNextRoundFromSkill/FromFight` (buffered) or `AddRealJusticeNow`/`SetRealJusticeNow` (immediate, rare).
- Psyche loss goes through `player.MinusPsycheLog(...)` (immunity + global log), never raw `AddPsyche(-N)` (exceptions: unique-logged effects like Дизмораль).
- Score: `AddBonusPoints` = immediate; `AddRegularPoints` = multiplied at round end; `GetScore()` = committed total (buffered regular points are *not* included mid-round).
- Dead-player dispatch is opt-out by invariant, not per-passive design: core fight selection drops dead sources/targets; readiness skips dead bots and forced sources; end/next-round dispatchers skip dead holders. Only `Глаз Шусуи` and `Боги мне не указ` may run while dead because their purpose is revival.
- Round-10 nuance: `BonusPointsFromMoral` staged after the round-10 flush must be flushed manually. Rumbling then runs first after combat and Naruto's Теневые settlement second; delayed liabilities against a dispersed clone must resolve through its original rather than against the zeroed dead seat.
- Transferred/copied passives (Goblin Ziggurat learns any `Standalone: true` passive; Котики cats carry Минька/Штормяк; mylorik's Акула transform) need their own immunity checks — the passive-name dispatch will happily run the case for the new holder.
- The `-228` sentinel means "not set" for every ForOneFight field; don't use −228 as a real value (there is a joke passive "Skill 228" that caps skill at 228 — unrelated).
- Randomness: unified (m21 fixed) — `SecureRandom` really is cryptographic now: the static core `SecureRandom.Next(min,max)` wraps `RandomNumberGenerator.GetInt32` (thread-safe, `Helpers/SecureRandom.cs:17-25`); the instance `Random(min,max)` and `Luck` delegate to it, and `PassivesClass` trigger schedules call the same static (its former private crypto copy is deleted). Semantics unchanged: `Random(min,max)` is **inclusive** of max; `Luck(x)` ≈ x% ; `Luck(a,b)` ≈ a-in-b (internally rounded to a whole percent, rolled against 0–100). A few places still use `new Random()`/`Random.Shared` directly (justice phrases `CharacterClass.cs:1657-1684`, Мишень initial roll `CharacterClass.cs:831`, forced-target picks in `CheckIfReady.cs`, Blackjack, the Sirinoks team bias `General.cs:380`) — exclusive-max semantics, deliberately left alone.
- Every account-economy mutation that can race—game-end rewards, Daily Quest rerolls, lobby loot, paid web/Discord draft picks, and both store frontends—uses the same account monitor (web store `WebGameService.cs:1209-1422`; Discord store `StoreReactions.cs:160-549`). User-triggered quest/loot/spend/ack operations save before confirming and restore their in-memory snapshot if the write fails; game-end retains the settled in-memory state for the 60 s retry after logging a critical save failure. `SaveAccount` reports success while serializing under that monitor, and storage replaces the account JSON through a unique same-directory temporary file (`UserAccounts.cs:113-139`; `UsersDataStorage.cs:28-80`). The loader accepts only canonical numeric account filenames (`UsersDataStorage.cs:83-116`).

### 9.1 Localization boundary

Russian character/passive/action strings remain canonical, load-bearing state. Per-account RU/EN translation happens only at Discord/web presentation boundaries through `GameLocalization`; the Vue layer likewise changes rendered text without mutating Pinia values (`GameLocalization.cs:16-20`; see `Web/VueClient/src/i18n.ts`). Both presentation engines apply dynamic rules plus longest-first exact fragments inside composite/multi-line values, so localization does not depend on a whole log or DOM node matching one catalog key (`GameLocalization.cs:89-142`; `Web/VueClient/src/i18n.ts:47-61,155-196`). The catalog contains every character biography and unique passive description, keyed by the canonical Russian identifiers. Full contract and extension workflow: [LOCALIZATION.md](LOCALIZATION.md).

## 10. Simulation harness (headless bot games)

`dotnet run -- --sim …` (wrapper: `bash tools/simulate.sh`, default `--games 100 --coverage 1`) mass-runs all-bot games through the production loop with **no Discord login and no Kestrel** — the behavioral safety net for a repo without tests. 518 games take ~14s.

- **Entry**: `Program.cs:65` — after the Lamar container is built (the `CheckIfReady` 100ms timer is already running from its constructor), `--sim` branches into `SimulationRunner.RunAsync` (`Program.cs:65-74`) and exits with its code; Discord login/Kestrel are never reached. `ClaudeHaikuService.Disabled` is set first (`Program.cs:67`) so sims never spend Anthropic API credits (Geralt hints fall back to static), and `ReplayService.CaptureEnabled` is set false (`Program.cs:71`): the harness never saves or serves a replay, while capturing one costs a full six-player state projection — localization included — three times per round, which used to dominate the simulator's runtime and heap (finding M51; WEB-BACKEND.md §9).
- **Game creation**: `CreateBotGameAsync` (`Game/Simulation/BotGameFactory.cs:15-20, 46`) — verbatim extraction of the old `*stb` loop body; the Discord command calls the same method now (`General.cs:98`). All-bot games self-advance one round per timer tick: zero humans → the ready-check never waits (`CheckIfReady.cs:979`).
- **Forced line-ups**: `forcedCharacters` on `HandleCharacterRoll` assigns characters by list order, bypassing the roll (`StartGameLogic.cs:117-120,227-253`). Needed because `CharacterToGiveNextTime` only works for non-null IUser slots. The caller owns both mutual-exclusion constraints (LeCrisp/Толя and HardKitty/Эрен Йегер) plus ≤1 Tier-4; unlike live guaranteed/draft/admin-test replacements, an explicit simulator lineup may intentionally violate them for a targeted repro (`StartGameLogic.cs:47-97`). Natural coverage still excludes `TeamModeOnly`; explicit `--characters` validation intentionally permits team-only characters through the existing admin/test force path, so Napoleon and Support pilots can be measured (`SimulationRunner.cs:121-154`; `BotGameFactory.cs:42-50`).
- **Modes** (flag parsing `Game/Simulation/SimulationRunner.cs:77-86`): smoke `--games N` = natural bot roll (tier-skewed — bots skip Tier<4 except Кира, so smoke alone does NOT cover all characters); coverage `--coverage K` = generated line-ups so every rollable non-team character appears ≥ K times (`SimulationRunner.cs:121-156`); matchup `--characters "6 comma-separated names"` = fixed line-up, validated (`SimulationRunner.cs:126-142`), for bug repros and team-only/character testing. Smoke+coverage are additive; matchup replaces both.
- **AI difficulty** `--ai-difficulty N` (0-3, **default 3**; validated by `SimulationRunner.RunInternalAsync`, clamped by `BotGameFactory.CreateBotGameAsync`, echoed to `options.aiDifficulty`, stored on `GameClass.AiDifficulty`). **0** = legal pure-random simulation control; **1** = frozen legacy behavior that may read privileged internal state; **2** = viewer-scoped strategy with broad character tactics and persistent character plans; **3** = the same legal information with longer memory, confidence-weighted prediction/rule inference, estimated opponent terms and best-target selection. L2/L3 never read a current opponent Block/Skip/action, true identity/stats/passives/score/Justice or private logs. Their plan is stored once in `GamePlayerBridgeClass.AiPlaystyle`; observations persist separately in `AiKnowledge` and are populated through `BotInformation.CaptureVisibleRound`. The simulation's historical exact prediction prefill runs **only for L1** (`BotGameFactory.CreateBotGameAsync`); L2/L3 use `CharacterPassives.HandleFairBotPredict` like live strict bots. The explicit designer-scripted exception is round-8 strict-bot Naruto/Sakura/Itachi: every level is given Madara's exact row and attacks it (`Madara.MustAcceptRoundEightBotChallenge`). No Discord/web picker exists; real games use the default. Full decision review: [BOT-AI-DESIGNER-REVIEW.md](BOT-AI-DESIGNER-REVIEW.md); tunables: `docs/BALANCE-CONSTANTS.md` → “Bot AI difficulty”.
- **AI measurement probe** `--ai-probe N` (0-3) + optional `--ai-probe-char "Name"` runs one bot at a different level from the field (override `GamePlayerBridgeClass.AiDifficulty`; resolve `BotsBehavior.cs:46-57`; set `BotGameFactory.cs:93-98`). `--ab-char "Name" [--ab-test N] [--ab-control N]` is the paired form: same process, line-up and per-game seed for both arms (`SimulationRunner.cs:406-543`). Every player result includes `AiPlaystyle` (`SimReport.cs:93-100`; mapper `SimulationRunner.cs:631-646`), so aggregate gains can be split by persistent branch instead of allowing one strong build to hide a weak one.
- **Failure capture**: `Global.SimErrorSink` (`Global.cs:52`) receives round-pipeline exceptions (`CheckIfReady.cs:1600-1608`) and bot-action exceptions (`BotsBehavior.cs:3556-3564`). Service-channel diagnostics use null-safe `Global.TrySendServiceMessage` (`Global.cs:58-70`; bot fallbacks `BotsBehavior.cs:3478-3517`). `CheckIfReady` increments a per-game `ReadinessLoopVisits` counter only when the single timer loop actually reaches that game (`GameClass.cs:206-208`; `CheckIfReady.cs:1120-1124`). The simulator removes a game as stuck after **5 visits at the same round**, so a slow pass or external CPU contention cannot freeze games that are merely waiting their turn (`StuckGameVisitsWithoutProgress`, `SimulationRunner.cs:28,324-349`). No global progress for 60s (a calculation pinning the loop) or the `--timeout-min` cap still aborts the batch (`SimulationRunner.cs:79,352-373`; finding M51).
- **Report**: JSON to `DataBase/Simulations/sim-<timestamp>.json` (gitignored) — per-game outcomes include authoritative `isWinner` and structural-clone flags alongside characters, playstyle, scores, places, deaths and rounds. Character winrate uses `GameClass.WinnerPlayerIds`, not place 1, and dispersed Naruto clone seats are excluded from both wins and denominator. Aggregates still include top1–6/avg score/avg place; failures contain stack + line-up. Each stuck row records `loopVisitsWithoutProgress` as the classification evidence and retains `secondsStalled` only as timing diagnostics. Exit codes: **0** clean, **1** errors and/or stuck games (each is a finding — triage via `/fix-finding`), **2** harness failure (`SimReport`; `SimulationRunner.BuildCharacterRows`).
- **Data isolation**: the wrapper runs from the project dir, so relative DataBase/* paths resolve to the *source* tree and sims read the **current** `characters.json` (the bin/Debug copy goes stale: only blackjack_words.json and wwwroot have csproj copy rules). Simulation mode sets `UserAccounts.DisableDiskPersistence` before the container is built (`Program.cs:40`), starts with an empty per-process account store and never writes it; bot accounts (ids ≤ 1 000 000, auto-created by `GetAccount`) therefore cannot touch real/dev account JSON or another simulator process.
- **Large AI comparison sweep**: `bash tools/sweep.sh [games-per-level] [batch]` builds once, then runs four independent arms for AI 0/1/2/3 concurrently. The default is **exactly 100 000 requested games per level / 400 000 total**, with batches of at most 5 000 (except when the chosen cap is smaller than one indivisible coverage pass). Each arm runs one coverage pass to calibrate its generated game count, chunks the remaining forced coverage under the same cap, then subtracts all coverage games from that arm's natural-roll budget, so coverage does not silently exceed 100k. The default coverage is `ceil(total/100)` appearances per rollable non-team character (1 000 at 100k), configurable through `KOTGH_SWEEP_COVERAGE` (`0` = natural-only). The 5k cap bounds the live per-process state and round latency; larger batches can be slower even with free RAM because each process advances all games on one timer thread (`tools/sweep.sh`).
- **Sweep reports**: each arm retains its batch JSON and receives an exact `merged.json`; `tools/sweep-report.py` then emits `comparison.json`, Excel-friendly `comparison.csv`, `summary.md`, an offline `index.html` dashboard and three SVG heatmaps (declared win rate, average place, average score). L1 is the visual baseline: blue means improvement, red regression, colour strength follows the combined standard error, and ★ marks an approximate 95% difference. Every cell also prints its sample count; the dashboard includes largest-gain/regression tables and confidence intervals. Aggregation replays the per-game `isWinner`/score/place rows rather than multiplying rounded batch averages or treating `top1` as a declared win (m37). Each arm is a **homogeneous field**, so its deltas show which character policies gain/lose relative to same-level opponents; use `--ai-probe` or `tools/ab.sh` for direct head-to-head AI strength. A failed sweep normally refuses a comparison because all arms must have their exact budget; to inspect surviving evidence only, run `python3 tools/sweep-report.py build <sweep-dir> <expected-games> --allow-partial`. It adds a prominent partial/unhealthy warning to every output and must not be used as a final comparison.
- **Parallel sims are safe, but only because the harness owns no disk state** (finding M52): the four sweep arms have empty, independent in-memory accounts and never read/write the shared account directory. Before that guard, one process could observe another process's replace gap and abort with exit 134, while each startup reset the other arms' pity/history and invalidated the experiment. Anything new the harness persists must be per-process or it reintroduces both bugs.
- **Why per-game cost remains important after the M51 watchdog fix**: one tick pass advances *every* live game on *one* thread, so the wall time between two rounds scales with batch size and competing CPU load. The old wall-clock watchdog therefore culled ~1 600 healthy games when a 2 000-game pass exceeded 30 seconds, then created secondary NREs by removing games mid-tick. Stuck classification now requires five actual visits to the same game at an unchanged round; a game waiting behind thousands of others has no new visits and cannot be falsely culled. The 60-second no-global-progress guard still catches a calculation that pins the loop. Per-game cost still controls sweep duration, so headless mode keeps viewer-facing work disabled — especially replay capture, which otherwise performs full six-player projections three times per round (`Program.cs:61-70`; WEB-BACKEND.md §9).
- **Seeded determinism + paired A/B** `--seed N`: runs games **sequentially, one at a time**, reseeding the game RNG per game (`SecureRandom.SetSeed`, `Helpers/SecureRandom.cs`) and reproducing the line-up plan. `--ab-char "Name" [--ab-test N=3] [--ab-control N=1]` plays the plan twice and prints paired win%/avgPlace/avgScore deltas with 95% CIs (wrapper: `bash tools/ab.sh <char> [test] [coverage] [seed] [control]`; runner `SimulationRunner.cs:406-543`). For multi-plan bots, group the `AiPlaystyle` result field (`SimReport.cs:92-99`) before accepting the aggregate: each branch must be checked independently. A few hash-order-sensitive characters (notably Goblins/Cats) keep residual nondeterminism; raise coverage for them.
- **Caveats**: winrate tables are bot-meta statistics (heuristic bots, tier-skewed smoke rolls, plus explicit forced coverage in `sweep.sh`) — use them for *drift* detection at N ≥ several hundred appearances, not as human-meta balance truth. Homogeneous L0/L1/L2/L3 arms compare per-character policy fit; average place and overall win share are structurally constrained across an all-bot field, so direct level strength needs the probe/paired A/B workflow.
