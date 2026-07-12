# Daily Quests V2

> Code-verified against the working tree of 2026-07-11 (v4.3.2). Daily Quests are account progression, not character mechanics: every objective is available with every randomly assigned character. Loot-box rules remain in [ACHIEVEMENTS.md](ACHIEVEMENTS.md); transport and presentation details live in [WEB-BACKEND.md](WEB-BACKEND.md) and [WEB-CLIENT.md](WEB-CLIENT.md).

## 1. Player loop

Every UTC day gives an account three ordered contracts: the fixed **Anchor**, one personalized **Skirmish**, and one personalized **Ambition** (`QuestClass.cs:13-68,208-260`). The board deliberately has no character/passive names, different-character requirements, predictions, score thresholds or block counts. Progress settles only when a match finishes, from generic match facts already observed by the fight pipeline (`QuestClass.cs:358-412`; settlement call `CheckIfReady.cs:699-734`).

Rewards are automatic and exactly once:

- each card immediately credits its own 20/30/30 ZBS when it first completes;
- any **2 of 3** cards complete the day, credit another 20 ZBS, advance the consecutive-day streak and stamp the weekly journey;
- **3 of 3** additionally adds one loot box to the same resumable inventory used by top-two and Achievement rewards;
- any five daily completions in one UTC ISO week credit 100 ZBS. Missing one or two days does not destroy the weekly journey (`QuestClass.cs:200-206,638-678,681-723`).

Thus a full board remains 100 direct ZBS—the old daily maximum—but reward delivery is transparent and the optional mastery box connects the system to Loot Boxes. At the published 57.5-ZBS base expected box value, seven full days plus the weekly reward are 1,202.5 ZBS-equivalent, effectively the old 1,200-ZBS weekly maximum before pity.

## 2. Catalog (12 character-neutral contracts)

The persisted unit is a stable quest ID. Display copy, lane, icon, aggregation, target and reward come from the server-owned bilingual catalog; account JSON stores a progress/reward snapshot, not executable rules (`QuestClass.cs:26-95,208-263`). `DailySum` accumulates across that UTC day; `BestMatch` keeps the best single-match fact observed that day (`QuestClass.cs:20-24,102-116,621-636`).

| Lane | ID | EN / RU | Requirement | Aggregation | Reward |
|---|---|---|---|---|---:|
| Anchor | `dq_clock_in` | Clock In / На смену | finish one match | daily sum | 20 ZBS |
| Skirmish | `dq_thick_of_it` | In the Thick of It / В гуще событий | take part in 4 resolved fights | daily sum | 30 ZBS |
| Skirmish | `dq_throw_hands` | Throw Hands / Распустить руки | win 2 fights | daily sum | 30 ZBS |
| Skirmish | `dq_rival_tour` | Rival Tour / Тур по соперникам | defeat 2 different opponents in one match | best match | 30 ZBS |
| Skirmish | `dq_hot_streak` | Hot Streak / Горячая серия | win 2 fights in a row in one match | best match | 30 ZBS |
| Skirmish | `dq_counterplay` | Counterplay / Контригра | win with class advantage | daily sum | 30 ZBS |
| Ambition | `dq_still_standing` | Still Standing / Остаться в строю | finish alive | best match | 30 ZBS |
| Ambition | `dq_podium` | Podium Finish / На пьедестале | finish alive in the top 3 | best match | 30 ZBS |
| Ambition | `dq_claw_back` | Claw Back / Выкарабкаться | finish 2 places above the lowest place reached | best match | 30 ZBS |
| Ambition | `dq_top_seat` | Top Seat / Первое кресло | spend 2 completed rounds at place 1 | best match | 30 ZBS |
| Ambition | `dq_balanced_scales` | Balanced Scales / Равные весы | reach 3 Justice in one match | best match | 30 ZBS |
| Ambition | `dq_take_hill` | Take the Hill / Взять гору | earn a winning solo result or play for the winning team | best match | 30 ZBS |

Exact paired copy and evaluator wiring are centralized together (`QuestClass.cs:208-260`). The audit hard-fails on missing/duplicate IDs, lane counts, changed metric wiring, incomplete bilingual metadata, unpersisted metrics, and any canonical character name leaking into this block (`tools/audit-quests.sh`).

## 3. Selection and player agency

The Anchor is always present. Skirmish and Ambition are selected independently with SHA-256 over catalog version, UTC date, account ID, lane and purpose; selection is personalized, deterministic across server restarts and stored on the active day (`QuestClass.cs:510-540`). This replaces the process-dependent `string.GetHashCode()` roll and prevents duplicate families such as the former play-1/play-3/play-5 board.

Each day includes one free swap for an unfinished Skirmish or Ambition card. The server validates ownership, completion, lane and allowance, chooses another goal from the same lane, and recomputes it from **all** generic metrics already earned that day—progress before the swap is not thrown away (`QuestClass.cs:98-116,414-474`). A reroll can therefore complete immediately if the replacement was already satisfied. The Anchor and completed cards cannot be swapped.

`RerollDailyQuest(questId)` runs under the account monitor. It snapshots quest state, ZBS and box inventory, persists before sending the new `QuestState`, and restores the exact snapshot on validation/save failure (`GameHub.cs:685-725`; snapshot `QuestClass.cs:491-508`).

## 4. Match facts and privacy

The match settlement contributes: eligible matches; resolved fights; fight wins; best distinct defeated opponents; best win streak; class-advantage wins; alive/top-three finishes; best climb from a match-low; rounds at first; maximum Justice; and a team-aware winning result (`QuestClass.cs:102-116,358-412`). These are generic systems rather than named-character hooks. Team victory counts every member of the winning team; solo uses the existing alive winner-result semantics, including the established reward treatment for ties and Sakura's soft win (`CheckIfReady.cs:648-704`).

When `Вечное Цукуеми` is active, a non-Madara account receives a private projected ending. Daily Quests still count safe participation (match completion and resolved-fight participation) but suppress every real outcome/result fact, so progress cannot contradict or reveal that private view (`QuestClass.cs:380-408`). Madara owns and sees the authoritative result, so his account evaluates normally.

## 5. Reset, streak and weekly journey

- Day keys and reset timestamps use UTC; the next board begins at 00:00 UTC (`QuestClass.cs:476-489,725-731`).
- The visible streak advances once when 2/3 is first reached. A missed UTC day resets the current streak on the next initialization, while `BestStreakDays` remains (`QuestClass.cs:283-355,681-688`).
- The weekly journey uses ISO year/week, stores distinct completed date keys, resets at the next ISO week, and pays at 5/7 (`QuestClass.cs:690-731`).
- One `DateTimeOffset` is captured for the whole game settlement so six account updates cannot split across midnight (`CheckIfReady.cs:287,734`).

Battleship has a separate UTC win-day streak and first-win reward; it does not advance this Daily Quest streak or the weekly journey (WEB-BACKEND.md §10; value in BALANCE-CONSTANTS.md).

## 6. Persistence and V1 migration

`QuestData` keeps the active day, streak/best, weekly journey **and** the existing loot opening/pity fields in one backward-compatible container (`QuestClass.cs:118-153`). Migration replaces only the daily board; it never drops `LastLootBox`, `LastLootBoxGameId` or `LootBoxPity`.

Missing/null containers and unknown/duplicate lane entries are normalized. An unpaid V1 board starts V2 immediately. If the V1 board already paid its all-three reward on the current UTC day, V2 represents that day as settled and disables its reroll without issuing any card/daily/mastery reward twice (`QuestClass.cs:283-355,542-613`). The deep account snapshot preserves all daily/weekly fields and the existing loot reference for rollback (`QuestClass.cs:491-508,734-797`).

Game-end reward settlement already runs under the account monitor and persists once after quests, top-two loot and achievements are applied. A failed real-account save keeps the settled in-memory state for the periodic retry (`CheckIfReady.cs:711-785`). Lazy day/week initialization and user rerolls are stricter: they save before the hub publishes state and restore on failure (`GameHub.cs:649-725`).

## 7. Web contract and experience

`QuestState` carries the paired catalog, lane/icon/aggregation, exact reward receipts, active/reset/server timestamps, 2/3 and 3/3 milestones, reroll allowance, current/best streak, 5/7 journey, ZBS/box inventory, loot odds/pity and any resumable box opening (`GameStateDto.cs:1032-1082`; mapping `GameHub.cs:728-803`). Keeping loot fields in the same response preserves reconnect recovery for cached and current clients.

The lobby's `DailyQuestBoard` is an inline reward surface rather than a third modal. It renders:

- bilingual title/requirement copy selected directly from typed DTO pairs;
- a reset countdown calibrated from server time and a one-shot refresh at zero;
- 2/3 daily, 3/3 mastery, streak and an explicitly labelled 5-day weekly summary (a day is stamped at 2/3, not mastery);
- stable lane cards with controlled Lucide icons, real ARIA progress bars, credited receipts and a free-swap action;
- skeleton, retry/error/empty states, transition-only completion feedback, and an announced/focus-restored replacement after reroll—including an immediately completed replacement;
- mobile layout and reduced-motion fallbacks (`DailyQuestBoard.vue:1-604,1490-1621`).

Lobby refreshes quests on mount, reconnect/authentication, visibility return and countdown rollover—never on its 3-second game-list poll (`Lobby.vue:150-177,216-237,362-375`; `game.ts:244-255,685-717`). If a refresh is already in flight when UTC midnight arrives, Pinia queues one trailing request so the rollover cannot be dropped. Request/reroll failures stay inline while the existing loot/achievement modal-priority contract remains untouched.

## 8. Verification

Run:

```bash
bash tools/audit-quests.sh
bash tools/verify-docs.sh --changed
dotnet build King-of-the-Garbage-Hill/King-of-the-Garbage-Hill.csproj
pnpm --dir Web/VueClient build
bash tools/simulate.sh
```

The quest audit is intentionally strict about the current positional catalog syntax. If the definition table is refactored to object initializers or indirect metric selectors, update the audit in the same change.
