# Achievements & Loot Boxes V2

> Code-verified reference for the account-level reward systems introduced in V2. Gameplay terms and Russian character/passive names are canonical identifiers; the English and Russian achievement copy is presentation metadata, not dispatch state.

## 1. Achievement model

The live catalog contains exactly **33** achievements: **11 Global**, **15 Character**, and **7 Interaction**. `AchievementDefinition` owns paired EN/RU names, descriptions and secret hints plus category, icon, rarity, target, related characters and rewards (`AchievementClass.cs:12-88`). `tools/audit-achievements.sh` hard-fails unless all 33 expected IDs are unique, present and evaluated.

Progress has two layers:

- `InGameAchievementTracker` records match-local observations only; its sets use player/character IDs where uniqueness matters (`AchievementClass.cs:94-134`).
- `AchievementProgress.Current` is the **best result achieved in one match**, not a cumulative total. `SetBestProgress` compares the current attempt with the stored best for display, but tests the **current attempt** against the target before unlocking; two partial matches can never combine (`AchievementClass.cs:362-403`).
- An unlock is permanent and exactly-once. Under the account monitor it stamps `UnlockedAt`, queues the ID in `NewlyUnlocked`, credits the rarity reward immediately, and refuses later credits once `IsUnlocked` is true (`AchievementClass.cs:375-401`).
- Game-end evaluation runs after final place/reward-place and match facts are known, in the same account-locked settlement that pays ZBS, quests and top-two loot boxes (`CheckIfReady.cs:705-737`; evaluator `AchievementClass.cs:412-560`).

Unless a row says otherwise, a target greater than 1 means “within one match.” Solo gates are explicit: team mode cannot unlock `g_clean_sweep`, `g_round10_comeback`, or `g_untouchable` (`AchievementClass.cs:421-456`).

## 2. Global achievements (11)

| ID | Achievement (EN / RU) | Single-match requirement | Rarity / reward | Code |
|---|---|---|---|---|
| `g_bottom_feeder` | Bottom Feeder / Со дна | Attack from current place 6 and defeat the current place-1 player. | Uncommon · 25 ZBS | definition `AchievementClass.cs:187-190`; fight observation/evaluation `DoomsdayMachine.cs:1295-1304`, `AchievementClass.cs:442` |
| `g_class_advantage` | Rock, Paper, Crown / Камень, ножницы, корона | Win 3 resolved fights while holding the Nemesis class advantage; attack and defence both count. | Rare · 50 ZBS | `AchievementClass.cs:191-194,443`; both sides `DoomsdayMachine.cs:1295-1320` |
| `g_target_routine` | Bullseye / В яблочко | Gain Main Skill from Мишень in 3 different rounds. Repeats within one round count once. | Common · 10 ZBS | `AchievementClass.cs:195-198,444`; `DoomsdayMachine.cs:643-650` |
| `g_maximum_sentence` | Maximum Sentence / Высшая мера | Win a resolved fight while holding 5 live Justice. | Uncommon · 25 ZBS | `AchievementClass.cs:199-202,445`; `DoomsdayMachine.cs:1295-1320` |
| `g_three_drops` | Down the Chute / Вниз по жёлобу | Personally cause 3 Drops. Multi-Drop effects count the actual number caused. | Rare · 50 ZBS | `AchievementClass.cs:203-206,446`; `DoomsdayMachine.cs:1017-1023` |
| `g_twenty_moral` | Moral Bankruptcy / Моральное банкротство | In one exchange, convert 20 Moral into 10 bonus points. | Uncommon · 25 ZBS | `AchievementClass.cs:207-210,447`; `GameReactions.cs:102-114` |
| `g_open_book` | Open Book / Открытая книга | Correctly predict every eligible opponent, with at least 3 eligible targets. Admin, Кира, Мадара and Let's Roll attempts are ineligible; fictional/admin targets are excluded. | Rare · 50 ZBS | `AchievementClass.cs:211-214,448`; eligibility `AchievementClass.cs:562-585` |
| `g_clean_sweep` | Garbage Collector / Сборщик мусора | In solo mode, defeat all 5 distinct opponents at least once. | Rare · 50 ZBS | `AchievementClass.cs:215-218,449`; unique-victim tracking `DoomsdayMachine.cs:1325-1334` |
| `g_round10_comeback` | From Sixth to King / Из шестого — в короли | In solo mode, open round 10 in place 6, then finish alive at actual place 1. | Epic · 100 ZBS + 1 box | `AchievementClass.cs:219-222,450-451`; round-10 snapshot `DoomsdayMachine.cs:1769-1777` |
| `g_untouchable` | Untouchable / Неприкасаемый | Win a solo match alive with at least 5 resolved fight wins and 0 resolved fight losses. | Epic · 100 ZBS + 1 box | `AchievementClass.cs:223-226,453-454`; fight totals `DoomsdayMachine.cs:1325-1334` |
| `g_quad_damage` | Quad Damage / Четверной урон | Receive at least 20 net regular points from round 10, measured after its real multiplier. | Rare · 50 ZBS | `AchievementClass.cs:227-230,455-456`; settlement delta `DoomsdayMachine.cs:1495-1501` |

## 3. Character achievements (15)

Character achievements require the named character, even if another holder can copy a related passive.

| ID | Achievement (EN / RU) | Single-match requirement | Rarity / reward | Code |
|---|---|---|---|---|
| `c_boys_orders` | French Connection / Французская связь | As TheBoys, complete all 3 Francie orders. | Uncommon · 25 ZBS | `AchievementClass.cs:233-236,459-460` |
| `c_goblin_summit` | Built Different / Особая постройка | As Стая Гоблинов, finish alive at actual place 1 with a place-1 Ziggurat and receive its enforced win. | Legendary · 228 ZBS + 2 boxes | `AchievementClass.cs:237-240,462-466` |
| `c_rick_portals` | Portal Authority / Портальная власть | As Рик Санчез, successfully fire Портальная пушка twice. | Rare · 50 ZBS | `AchievementClass.cs:241-244,469-470`; fire counter `CharacterPassives.cs:2128-2139` |
| `c_saitama_one_punch` | One Punch / Один удар | As Сайтама, reclaim at least 20 deferred points through Ищет достойного противника. | Epic · 100 ZBS + 1 box | `AchievementClass.cs:245-248,472-473`; reclaim total `CharacterPassives.cs:5097-5113` |
| `c_madara_tsukuyomi` | Wake Up to Reality / Очнись и вернись в реальность | As Мадара, finish with Вечное Цукуеми active and not sealed. Secret. | Legendary · 228 ZBS + 2 boxes | `AchievementClass.cs:249-255`; private-ending evaluator `AchievementClass.cs:426-439` |
| `c_tigr_six_zero` | Six–Zero / Шесть — ноль | As Тигр, complete 3-0 обоссан against 2 different enemies. | Epic · 100 ZBS + 1 box | `AchievementClass.cs:256-259,483-485` |
| `c_itachi_tax` | Tax Collector / Сборщик налогов | As Итачи, copy at least 20 total points through Глаза Итачи. | Rare · 50 ZBS | `AchievementClass.cs:260-263,487-489` |
| `c_kratos_olympus` | Ghost of Sparta / Призрак Спарты | As Кратос, personally kill all 5 other players during Возвращение из мертвых. Kratos's own death is not a victim. | Legendary · 228 ZBS + 2 boxes | `AchievementClass.cs:264-267,491-492`; distinct victims `CharacterPassives.cs:1737-1745` |
| `c_kira_perfect_crime` | Perfect Crime / Идеальное преступление | As Кира, make 3 successful Тетрадь смерти kills against distinct victims. | Epic · 100 ZBS + 1 box | `AchievementClass.cs:268-271`; distinct correct entries `AchievementClass.cs:494-501` |
| `c_monster_apocalypse` | Beautiful Apocalypse / Прекрасный апокалипсис | As Монстр без имени, execute at least 2 pawns through Пейзаж конца света. | Epic · 100 ZBS + 1 box | `AchievementClass.cs:272-275,504-505`; `CharacterPassives.cs:4596-4607` |
| `c_geralt_contracts` | Witcher’s Payday / Ведьмачья получка | As Геральт, resolve 3 contract fights. | Uncommon · 25 ZBS | `AchievementClass.cs:276-279,507-508`; `DoomsdayMachine.cs:1357-1369` |
| `c_kotiki_reunion` | The Cats Came Back / Котики вернулись | As Котики, reclaim both Минька and Штормяк by winning their return attacks. | Rare · 50 ZBS | `AchievementClass.cs:280-283,510-511`; unique cat returns `CharacterPassives.cs:3284-3293` |
| `c_darksci_unstable` | Against All Odds / Вопреки всему | As Darksci, choose unstable, trigger Повезло, and finish alive at actual place 1. | Epic · 100 ZBS + 1 box | `AchievementClass.cs:284-287,513-519` |
| `c_eren_rumbling` | The Rumbling / Гул Земли | As Эрен Йегер, kill at least 2 distinct players with Rumbling. | Epic · 100 ZBS + 1 box | `AchievementClass.cs:288-291,522-523`; victims `CharacterPassives.cs:3548-3558` |
| `c_doom_bfg` | BFG Division / Дивизия BFG | As DooM Guy, defeat at least 3 distinct players in one BFG wave, including its primary target. | Epic · 100 ZBS + 1 box | `AchievementClass.cs:292-295,525-526`; wave tracking `DoomsdayMachine.cs:1295-1305` |

## 4. Interaction achievements (7, all secret)

Locked interaction cards expose only a deliberately vague hint. The exact rules below are implementation documentation, not player-facing card copy.

| ID | Achievement (EN / RU) | Single-match interaction | Rarity / reward | Code |
|---|---|---|---|---|
| `x_spartan_dragon` | Dragon Slayer / Убийца драконов | As Загадочный Спартанец в маске, trigger DragonSlayer against round-10 Sirinoks/Дракон and defeat her. | Epic · 100 ZBS + 1 box | `AchievementClass.cs:298-304,528-533`; trigger/result `CharacterPassives.cs:1191-1202`, `DoomsdayMachine.cs:1307-1312` |
| `x_kira_kratos` | Gods Don’t Tell Me What to Do / Боги мне не указ | As Кратос, die to Kira's Тетрадь смерти and revive through Боги мне не указ. | Legendary · 228 ZBS + 2 boxes | `AchievementClass.cs:305-311,536-537`; revive state `CharacterPassives.cs:5768-5790` |
| `x_itachi_madara` | Eyes Meet Eyes / Глаза встретились | As Итачи, correctly lock Мадара in round 8 and receive the extra Клоны Сусано attack. | Rare · 50 ZBS | `AchievementClass.cs:312-318,539-541`; grant `CheckIfReady.cs:1392-1402` |
| `x_deeplist_weedwick` | Pet Project / Любимый проект | As DeepList or Weedwick, finish with both characters alive in the final top 3. Both accounts earn it. | Epic · 100 ZBS + 1 box | `AchievementClass.cs:319-325,543-551` |
| `x_spartan_mylorik` | Mutual Respect / Взаимное уважение | As Загадочный Спартанец в маске, trigger the mutual-Psyche interaction with mylorik, then defeat him in a later fight. | Rare · 50 ZBS | `AchievementClass.cs:326-332,529-533`; respect/result `CharacterPassives.cs:1207-1218`, `DoomsdayMachine.cs:1382-1393` |
| `x_boys_madara` | Nothing Is Immune / Нет неприкасаемых | As TheBoys with СуперМудень, successfully deal Harm through Мадара's Воскрешенное тело. | Legendary · 228 ZBS + 2 boxes | `AchievementClass.cs:333-339,554-556`; bypass observation `DoomsdayMachine.cs:1003-1015` |
| `x_monster_witness` | I Saw the Beast / Я видел Зверя | As a non-pawn, attack Монстр без имени in round 10 and receive the Пейзаж конца света payout. | Rare · 50 ZBS | `AchievementClass.cs:340-346,558-559`; witness flag `CharacterPassives.cs:4615-4626` |

These seven relationships are also indexed by subsystem in [INTERACTION-MATRIX.md](INTERACTION-MATRIX.md) §8.

## 5. Rarity rewards and catalog totals

| Rarity | Unlock reward | Catalog use |
|---|---:|---:|
| Common | 10 ZBS | 1 |
| Uncommon | 25 ZBS | 5 |
| Rare | 50 ZBS | 11 |
| Epic | 100 ZBS + 1 loot box | 11 |
| Legendary | 228 ZBS + 2 loot boxes | 5 |

The reward switch is centralized in the definition constructor (`AchievementClass.cs:65-76`). Completing the current catalog awards **2,925 ZBS and 21 loot boxes** in total. `AchievementBoard` reports earned/current-catalog totals by summing live unlocked definitions; these numbers are a catalog summary, not a historical transaction ledger (`GameHub.cs:757-785`).

## 6. Secrets, queues, and Вечное Цукуеми

- A locked secret's exact name, descriptions and character list are not sent to the client; only its hint, category, rarity, target/reward framing and a stable SHA-256-derived opaque public ID are exposed. Once unlocked, the real ID and full paired metadata are returned (`GameHub.cs:745-788,1394-1426`; DTO `GameStateDto.cs:1080-1112`).
- `NewlyUnlocked` is an acknowledgement queue, not a match-local toast flag. Match completion does not clear it. `AcknowledgeAchievements` removes only the live IDs actually shown and saves a non-empty removal; `ClearNewAchievements` remains as a legacy clear-all method. This selective acknowledgement prevents a concurrent unlock from being erased with an older popup (`AchievementClass.cs:89-92`; `GameHub.cs:791-844`).
- The finished personalized game-state DTO carries full entries for that player's queued live IDs so the in-game UI can celebrate them; spectators and other players receive none (`GameStateMapper.cs:173-208`).
- While Вечное Цукуеми is active, evaluating real-result achievements for non-Madara accounts would reveal or contradict their private projected ending. Achievement V2 therefore evaluates only Madara's own hidden-ending achievement and returns without evaluating anything else for any player (`AchievementClass.cs:426-439`).

## 7. V1 migration and compatibility

V2 is an intentional fresh catalog. Its 33 `g_…` / `c_…` / `x_…` IDs are disjoint from the legacy achievement IDs, so old unlocks do **not** grant V2 rewards or appear as V2 completions. Existing account JSON remains readable:

- `EnsureInitialized` null-fills the account containers without deleting unknown legacy progress rows (`AchievementClass.cs:355-360`).
- Board totals and detached entries are built only from `AllAchievements`, under the account monitor, and queued IDs are filtered to the live set (`GameHub.cs:757-786`).
- Legacy match-tracker fields remain deserializable for old snapshots/hooks but are explicitly not evaluated by V2 (`AchievementClass.cs:133-179`).

There is no retroactive reconstruction from match history because most requirements need per-fight/per-passive facts that history never stored. A player's V2 bests and unlocks therefore begin with matches completed after deployment.

## 8. Loot boxes V2

### Base odds and rewards

| Rarity | Base chance | ZBS reward (inclusive) |
|---|---:|---:|
| Common | 60% | 15–30 |
| Uncommon | 25% | 40–75 |
| Rare | 12% | 100–175 |
| Epic | 2.5% | 300–450 |
| Legendary | 0.5% | 750 |

The server owns both the table and roll; the client receives the table only for explanation. Rarity uses `SecureRandom` over 10,000 equally likely outcomes with exact cumulative thresholds, and variable rewards use its inclusive bounds (`QuestClass.cs:132-140`; `QuestClass.cs:371-410`). A box is earned by finishing alive in the reward top 2, including Sakura's first-place reward treatment; Epic/Legendary achievements add boxes to the same inventory (`CheckIfReady.cs:648-651`; `CheckIfReady.cs:730-737`). Existing pending inventory is preserved and uses the V2 table when opened.

### Rare+ pity

`LootBoxPity` counts consecutive final Common/Uncommon results. After 9 such boxes, the 10th opening is guaranteed Rare+: a natural Rare/Epic/Legendary is preserved, while a natural Common/Uncommon is upgraded to Rare. Any Rare+ result resets the counter to 0 (`QuestClass.cs:296-299`; `QuestClass.cs:371-379`; `QuestClass.cs:413-420`). `GuaranteedRareIn` is server-derived and both the lobby and reveal screen visualize the remaining distance.

The displayed chances are **base odds**. On the guaranteed opening, pity changes only the below-Rare outcomes as described above; it does not reroll or downgrade a natural Rare+.

### Atomic open, acknowledgement, reconnect

- Opening is serialized on the account monitor. If a prior result has not been acknowledged, repeated `OpenLootBox` calls return that same `OpeningId` and do not consume another box or grant ZBS again (`QuestClass.cs:318-350`).
- A new open decrements exactly one pending box, rolls and credits the result, and stores a result snapshot containing rarity, amount, balance, remaining inventory, pity state, timestamp and `WasPityUpgrade` (`QuestClass.cs:343-397`).
- `AcknowledgeLootBox(openingId)` marks only the matching current result; stale/mismatched IDs are safe no-ops (`QuestClass.cs:352-368`).
- `RequestQuests` snapshots under the account monitor and includes `LastUnacknowledgedLootBox`, odds and pity fields. After a disconnect/reload, the client resumes that already-determined reveal rather than rolling again (`GameHub.cs:649-694`; `GameStateDto.cs:1032-1076`).
- Economy-changing opens, successful acknowledgements, non-empty achievement-queue removals and real-player game-end awards are saved immediately. `SaveAccount` locks through serialization/write; storage writes a unique same-directory temp then replaces the canonical JSON, and startup ignores noncanonical temp/backup filenames (`UserAccounts.cs:113-139`; `UsersDataStorage.cs:28-76`; `UsersDataStorage.cs:81-110`; calls `GameHub.cs:721-740`; `GameHub.cs:799-843`; `CheckIfReady.cs:705-777`).

## 9. Web experience and accessibility

The reward experience has three coordinated surfaces:

1. **Rewards hub in `/games`** — always-visible ZBS balance, pending-box inventory, Rare+ distance/base legendary chance, achievement completion ring, new badge and earned/current-catalog reward totals. It requests quest and achievement state together and restores an unacknowledged reveal (`router.ts:24-28`; `Lobby.vue:27-112`; `Lobby.vue:127-136`; `Lobby.vue:210-320`).
2. **Dedicated `/achievements` page** — an achievement center with overall completion/reward summaries, nearest visible completions, category/status/rarity filters, bilingual search, character portraits, progress bars, secret hints and responsive rarity treatments (`router.ts:49-57`; `Achievements.vue:1-22`; `AchievementBoard.vue:22-193,196-430`). Icons are real Lucide components selected through one controlled mapping (`AchievementIcon.vue:1-55`).
3. **Celebrations** — unlocks appear sequentially after the game-over podium, with rarity color/audio, particles, character portraits and explicit ZBS/box rewards. The modal traps focus, supports Escape/Skip all, isolates the background and restores prior focus (`AchievementPopup.vue:20-173,176-270`); its reduced-motion media query disables nonessential movement (`AchievementPopup.vue:422-436`). The game-over podium retains priority (`Game.vue:1481-1485`). Authentication requests the board and rehydrates full queued cards; `App.vue` globally hosts that recovered celebration on every authenticated non-game route, so reloads and deep links do not reduce it to a badge (`game.ts:246-259,296-315`; `App.vue:13-15,190-194`).

The loot-box dialog is staged: anticipation/opening first, then the already server-determined result; rarity-specific audio/visual treatment, balance/inventory/pity updates, base-odds disclosure, pity-upgrade badge and an “Open another” path are all in the same modal (`LootBox.vue:35-174,213-380`). It cannot be dismissed during opening or while acknowledgement is saving. Continue/Open another closes and clears the local result only after acknowledgement succeeds; an error stays in the modal with retry context (`Lobby.vue:71-112`; `LootBox.vue:123-127,167-201,213-380`). Keyboard focus isolation/restoration, Escape behavior, ARIA modal/progress semantics, compact mobile layout and reduced-motion fallbacks are part of the contract (`LootBox.vue:55-127,177-201,213-380`).

## 10. Verification

Run all of the following for changes to this system:

```bash
bash tools/audit-achievements.sh
bash tools/verify-docs.sh --changed
dotnet build King-of-the-Garbage-Hill/King-of-the-Garbage-Hill.csproj
pnpm --dir Web/VueClient build
bash tools/simulate.sh
```

`audit-achievements.sh` verifies the exact IDs/category counts, duplicate absence, evaluator references and required bilingual/reward metadata. The build checks the SignalR DTO/TypeScript mirrors; the simulation protects the gameplay hooks used as observations.
