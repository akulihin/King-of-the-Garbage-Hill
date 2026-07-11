# Interaction Matrix — cross-character rules

> Hand-maintained; verified 2026-07-11. **When adding/changing a character, add or update its row in every applicable table** — the M10–M12 bugs happened precisely at holes in these matrices. ⚠ = known hole (see AUDIT-FINDINGS). `CP` = CharacterPassives.cs, `CIR` = CheckIfReady.cs, `DM` = DoomsdayMachine.cs.

## 1. Forced-fight sources × untargetable / no-fight states

Forced-fight sources: **Монстр** two-turn no-escape (`CP:1024-1031`; `CIR:1371-1393`), **Шэн** below-position pull (`CIR:1277-1308`), **Штормяк** taunt (`CIR:1320-1354`), **Aggress** self auto-attack (`CIR:1264-1275`), **Геральт** contract multi-fight injection (`DM:315-370`), DooM Guy **BFG** wave injection (`DM:809-827`).

| State ↓ / Source → | Монстр | Шэн | Штормяк | Aggress (self) | Геральт inject | BFG wave |
|---|---|---|---|---|---|---|
| Dead player | ✓ excluded | ✓ excluded | ✓ excluded | ✓ targets exclude dead | n/a (dead don't fight) | ✓ excluded `DM:765` |
| Тигр round-10 ban | ✓ carve-out `CIR:1293` | ✓ carve-out (M11 fixed, `CIR:1213`) | ✓ carve-out (M11 fixed, `CIR:1247`) | n/a | targeting already blocked (`GR:702-707`) | not special-cased; ordinary block/skip still applies |
| Огурчик Рик (pickle) | ✓ active pickle strips IsBlock/IsSkip and wins (`DM:261-278,486-499`; M18) | pulls him; pickle accepts and wins | can taunt him; he accepts and wins | n/a | injection works; pickle accepts and wins | injected normally; pickle accepts and wins |
| Block | attack still marks before the no-fight gate; overridden on each of the next two turns | fight happens anyway (forced list bypasses block-skip `DM:425-439`) | taunt bypasses own block (`DM:503-506`) | Aggress can't block at all | blocked Геральт = no injection (`DM:325,348`) | block stops that branch; normal penalty applies |
| Skip (sleep/tilt/ban) | overridden (stripped) | fight happens anyway | fight happens anyway | Aggress can't skip | skipping Геральт = no injection | skip stops that branch |
| Ziggurat lock | position only — fights unaffected | position only | position only | n/a | n/a | position only — wave follows current board order |
| Premade Carry | n/a | n/a | n/a | n/a | anti-skip now exempts the round-10 Тигр ban (M10 fixed, `CP:5689-5704`) | n/a |
| Эрен: Атакующий Титан | no block remains to strip; forced target still resolves with +5 stats | forced target resolves with +5 stats | taunt target resolves with +5 stats | n/a | injection resolves each fight with +5 stats | wave branches resolve with +5 stats | Block is cleared in `DM:271-281`; boost is reapplied per fight `CP:62-72,443-447,1002-1006` |
| Мадара round 8 | targetable; own action is cleared after forced-action injection | targetable | targetable | own auto-action cleared | contract fights resolve normally | wave resolves normally | Correct locked predictions add another ordinary queued fight (`CIR:1376-1397`); Madara cannot attack (`Madara.cs:197-205`) |
| Мадара sealed | all queued targets sanitized | all queued targets sanitized | all queued targets sanitized | cannot act | pre-fight targets sanitized | ✓ excluded `DM:835-841` | `Madara.SanitizeSealedActions` runs after forced injections; direct targeting says `Игрок запечатан` (`Madara.cs:231-240`; `CIR:1419-1421`) |

Bot and auto-move action selection finalizes an existing Skip both on entry and again after pending level-ups (`BotsBehavior.cs:84-128,3706-3715`). The second gate is required for Darksci's round-9 Дизмораль (M32): it cannot become an ordinary bot attack, while the forced-fight sources above still work because they inject their targets later in the readiness/fight pipeline.

## 2. Kill sources × immunities

Kill sources: Кира's Тетрадь (`CP:3988-4066`), Кира's L-arrest (self-kill, `CP:4670-4711`), Кратос event kills (`CP:1655-1675, 2420-2436`), Монстр Пейзаж pawn deaths (`CP:4300-4333`), Геральт pitchfork displeasure (self, `CP:4483-4490`), Эрен's Rumbling (`CP:3484-3528`).

| Immunity ↓ / Source → | Тетрадь | Кратос kill | Пейзаж pawns | Rumbling | Notes |
|---|---|---|---|---|---|
| Стая Гоблинов ("нельзя убить") | ✓ `CP:4009` | ✓ `CP:1660` (+arrest `CP:4686`) | ✗ **die here — intended** (M12, ОК) | ✗ die `CP:3501-3512` | design: GameDesign.txt:509; Rumbling says all strictly-between players |
| Глаз Шусуи (Итачи) | revives next round | revives next round | revives next round | revives next round | one-time, any source (`CP:5365-5374`) |
| Боги мне не указ (Кратос) | ✓ revives +228 Skill | n/a | ✗ not covered (source ≠ "Kira") | ✗ not covered | source-check is `== "Kira"` only (`CP:5377-5386`) |
| Воскрешенное тело (Мадара) | ✓ `CP:4287-4300` | ✓ `CP:1725-1734` | ✓ `CP:4583-4594` | ✓ `CP:3527-3541` | unconditional external-kill immunity; sealing is an unable-to-act state, not death |
| Dead state effects | — | — | — | already dead excluded `CP:3506` | auto-block/ready, 0 ZBS, no mastery, excluded from forced pools (`CIR:1032-1037, 622-665`) |

## 3. Position movers × position locks

Movers (end-of-round order): Тигр-топ swap → Portal-Gun swap → HardKitty forced last → place assignment → **Ziggurat restore** → Storm-bite restore/swap → Quality Drop (`DM:1295-1499`). Mid-turn movers: AWDKA forced last (intended — M3, ОК) (`CIR:1112-1127`), HardKitty forced last (`CIR:1141-1151`), Шэн post-sort swap (`CP:6117-6142`).

| Lock ↓ / Mover → | Тигр-топ | Portal Gun | Quality Drop | Storm bite | Шэн | Овца forced-last |
|---|---|---|---|---|---|---|
| Ziggurat (`IsInZiggurat`) | ✓ blocked `DM:1313` | ✓ blocked `DM:1338` | ✓ can't drop onto/out `DM:1458-1469` | ✓ blocked `DM:1413,1438` | ✓ blocked `CP:6128` | n/a: Eren passives are not Standalone and name-gated |
| HardKitty at place 6 | n/a | n/a | ✓ can't drop onto `DM:1465` | n/a | n/a | mutually exclusive in natural games `StartGameLogic.cs:245-250` |
| Тигр ban (round 10) | ✓ swap suppressed `DM:1304-1306` | n/a | n/a | n/a | ✓ pull now respects the ban (M11 fixed, `CIR:1213`) | inactive after round 8 |

## 4. Steal / copy / redirect chains

| Mechanic | Direction | Interacts with | Verified behavior |
|---|---|---|---|
| Еврей (`HandleJews`, `CP:6594-6672`) | steals fight win point | Октопус ink | ink debits the Jew instead of the attacker (`CP:6691-6708`); Napoleon & fellow Евреи immune victims |
| PointFunnel (Баг) | copies regular points | Еврей | funnel copies only `AddWinPoints` — Jew's stolen points not funneled |
| Цукуеми (Итачи) | copies round earnings, deducts at end | Октопус ink | victim pays once: the round-11 ink restore **skips** its debit for a victim under Цукуеми (Итачи deducts instead); both Итачи and Octopus still get their point (D11 fixed, `CP:4773-4790`) |
| Цукуеми (Итачи) → Мадара | copies ordinary round earnings | Воскрешенное тело | score theft works normally; Madara receives the supplied personal reaction, labeled `Бог шиноби` so the hidden passive name is not leaked (`CP:4385-4397`; `CharactersPhrases.cs:352-359`) |
| Октопус ink | fake-win now, restore at r11 | DeepList first-fight | suppressed until DeepList's scripted loss happens (`CP:6678-6685`) |
| Kimiko Живое Оружие | **drains** attacker Justice | regular score | real transfer plus +1 regular point per Justice drained (`CP:802-815`) |
| Близнец (Монстр) | **copies** the highest attacker Justice on block + equal total bonus | generic block Justice | attacker keeps Justice; Monster gets no normal +1; multiple attackers use max, not sum (`CP:936-960`; `DM:564-568`) |
| Вампуризм | **copies** victim Justice (intended — D6) | Падальщик | +1 extra from the ignored point (`CP:1842-1846`) |
| Premade | **copies** Carry fight-moral (intended — D9) | — | `CP:2366-2369` |
| Кошачья засада (cats) | physically moves passives to enemy | Минька/Штормяк vs owner | transferred cat won't buff/taunt against Котики (`CP:3010-3013`, `CIR:1229`) |
| Ziggurat learn | copies a `Standalone` passive | everything | see §6 |
| Бензопила (DooM Guy) | replaces Gun with one victim passive | copied passive dispatch/state | offers the victim's first four non-admin passives; explicit first-charge priming exists for Portal Gun, Шэн, Изанаги and Глаза Итачи (`DoomGuy.cs:198-225`); ordinary name-gated/game-start-only passives retain their native gates |
| Eren passive copy | Eren's four passives are non-Standalone; Chainsaw may still offer one | DooM Guy Chainsaw | all four cases are `Name == "Эрен Йегер"` gated, so a Chainsaw copy is inert |
| Rick Most wanted | redirects random marks to Rick | Спартанец marks, L, Сверхразум, Комментатор, hunts, tea odds | `CP:118-128, 233-236, 3780-3783, 5175-5181, 3511-3515, 1088-1094, 5036-5038`; hunters follow portal swaps `CP:2069-2084` |
| Portal Gun swap | swaps positions + remaining attackers mid-round | Тetradь targets etc. | attacker lists rewritten `CP:2060-2067` |

## 5. Moral / psyche / Harm interceptors (checked inside `AddMoral` / `MinusPsycheLog` / `LowerQualityResist`)

| Interceptor | Effect | Anchor |
|---|---|---|
| Булькает (Братишка) | zeroes all moral; blocks all skill gains | CC:1125-1130, 963, 1010 |
| Геральт (by Name) | gains no moral at all | CC:1132-1134 |
| BlockMoralGain (cancer, Оковы) | blocks positive moral | CC:1137-1141 |
| Привет со дна | ignores losses; any gain becomes +4 (`isMoralPoints` exempt) | CC:1143-1153 |
| Спокойствие | ignores moral losses; immune to MinusPsycheLog | CC:1156-1160, GamePlayerBridgeClass.cs:93 |
| M.M. IsCalm (first M.M. upgrade) | immune to MinusPsycheLog; disabled by СуперМудень | GamePlayerBridgeClass.cs:102-116; `GameReactions.cs:1021-1029` |
| Безумие | psyche bypasses the 0-floor (can go negative) | CC:1295, 1344 |
| Boole Family | immune to ordinary Harm; СуперМудень bypasses | CC:205-210 |
| Kimiko active | TheBoys immune to ordinary Harm; СуперМудень bypasses | CC:217-228 |
| Маневры (DooM Guy) | after otherwise successful Harm, −1 persistent Speed | CC:204-208 |
| Испанец | ordinary Harm → +1 Moral instead; СуперМудень bypasses | CC:231-238 |
| Много выебывается | Harm from higher-skill enemy while #1 → self-Drop | CC:223-229 |
| Минька (winner) | deals no Harm and no fight-moral loss | DM:750, 805-806 |
| Let's Roll! (DooM Guy) | Moral is set to 0 and all later Moral mutations/conversions are rejected; predictions are cleared/disabled | DoomGuy.cs:113-126, CC:1136-1137, GameStateMapper.cs:121-122 |
| Воскрешенное тело (Мадара) | Skill/Moral/Psyche loss, negative stat mutations and predictions are rejected; ordinary Harm is rejected but СуперМудень bypasses it; Madara deals no Harm | Madara.cs:34-39; CC:205-210,781-846,911-1167,1239-1605; GamePlayerBridgeClass.cs:102-108; DM:831-935 |
| СуперМудень attacker | ignores every enemy Harm interceptor above; every applied Int/Str/Psyche break recursively queues Harm | CC:182-345; DM:928-984 |

## 6. Ziggurat-copyable inventory (`Standalone: true`)

Copy rule: random Standalone passive from the **last attacked** enemy, no duplicates, **«Еврей» excluded** (`CP:6075-6087`). Full behavior inventory in AUDIT-FINDINGS D10; headline rows:

| Copied passive | On a Goblin |
|---|---|
| Изанаги | works — 2 free defensive auto-wins |
| Еврей | **excluded from copy** — Goblins can't become a second Jew (D2 fixed) |
| Одиночество / Импакт / Панцирь / Неуязвимость / Привет со дна / marks & bites | work as written |
| Произошел троллинг | works **and** inherits the AWDKA forced-last quirk (intended — M3, ОК) |
| Сомнительная тактика | works — massive self-nerf (must lose first fights) |
| Булькает | self-brick: kills own moral & skill incl. Ziggurat income — **left as-is** (D10, intended; only «Еврей» is excluded — D2) |
| Лысина / Первая кровь / Похищение души | dead copies (game-start-only hooks) |
| Ведьмачьи заказы | dead copy (all hooks gated `Name == "Геральт"`) |
| Madara's first four passives | dead copies: every mechanic is gated by `Name == "Мадара"`; none is `Standalone`, and Chainsaw cannot offer the fifth hidden passive (`Madara.cs:34-45`; `DoomGuy.cs:198-225`) |

## 7. Same-target stacking notes

- Two attackers on one defender resolve **sequentially in leaderboard order** — the first fight's ForOneFight effects are reset before the second (`DM:388-411`, ResetFight per fight).
- BFG branches are appended to that same sequential target queue. The primary random-stage win fans out to both neighbours; every wave fight also auto-wins if it reaches Step 3 random, while a decisive pre-random loss/block/skip ends the branch; a target is visited at most once (`DM:764-827`).
- Со-attack interactions verified: Еврей steal (needs the Jew to also attack the target), Сайтама deferral (needs a co-attacker), Наполеон joint-attack auto-win (needs the ally to attack the same target).
- Эрен mutual attack is direction-safe: only Eren's own attack branch awards +2 regular, and a per-round enemy list prevents contract/BFG repeats from paying twice (`CP:2489-2500`). An attacking enemy's hatred mark is upgraded to 2 before resolution; a later ordinary Eren loss never downgrades it to 1 (`CP:438-441,2470-2478`).
- Мадара round 8 deliberately keeps same-target duplicates: a normal/hidden/second action and the correct-prediction clone each remain separate fights. Thresholds use **unique attackers**, including fights injected during resolution, while the sealing loss requirement counts resolved defense losses (`CIR:1376-1397`; `Madara.cs:64-131`; `DM:461`).
- Round-10 pre-settlement: Rumbling runs immediately after the fight loop and before every `HandleEndOfRound` passive; it ranks projected post-multiplier ordinary score, kills strict-between places, then later passives proceed (`DM:1419-1420`; `CP:3536-3584`).
- Round-10 settlement order (who claws back first): Пейзаж deaths & Saitama banking happen in round-10 `HandleEndOfRound`; Чернильная завеса restore and Ищет достойного (One Punch) at round-11 `HandleNextRound`; Запах мусора at round-11 after-sorting; then `HandleLastRound`: predictions → active M.M. ×компромат → active Francie virus → Цукуеми deduction → sort → AWDKA → Premade → Sakura. СуперМудень skips both disabled-member settlements (`CheckIfReady.cs:320-375`; GAME-DESIGN §8E).

## 8. Achievement V2 interaction observations

These hooks are **observational**: they record an already-resolved interaction and do not add a win, death, Harm, position change or resource effect. All seven cards are secret until unlocked; exact copy/rewards are catalogued in [ACHIEVEMENTS.md](ACHIEVEMENTS.md) §4.

| Achievement | Required characters | Observation point | Account(s) that earn it |
|---|---|---|---|
| `x_spartan_dragon` Dragon Slayer | Загадочный Спартанец в маске × Sirinoks/Дракон | DragonSlayer armed in the round-10 before-fight hook, then the Spartan actually wins that fight (`CP:1191-1202`; `DoomsdayMachine.cs:1307-1312`) | Spartan only (`AchievementClass.cs:528-533`) |
| `x_kira_kratos` Gods Don’t Tell Me What to Do | Кира × Кратос | Kira's correct Тетрадь смерти kill reaches Kratos, then Боги мне не указ revives him (`CP:5768-5790`) | Kratos only (`AchievementClass.cs:536-537`) |
| `x_itachi_madara` Eyes Meet Eyes | Итачи × Мадара | round-8 correct locked prediction grants the extra Клоны Сусано attack (`CheckIfReady.cs:1392-1402`) | Itachi only (`AchievementClass.cs:539-541`) |
| `x_deeplist_weedwick` Pet Project | DeepList × Weedwick | final authoritative board has both alive at places 1–3 (`AchievementClass.cs:543-551`) | both accounts |
| `x_spartan_mylorik` Mutual Respect | Загадочный Спартанец в маске × mylorik | mutual-Psyche respect is recorded, then a later resolved fight is won by the Spartan (`CP:1207-1218`; `DoomsdayMachine.cs:1382-1393`) | Spartan only (`AchievementClass.cs:529-533`) |
| `x_boys_madara` Nothing Is Immune | TheBoys/СуперМудень × Мадара/Воскрешенное тело | an actual Super Harm application passes the resurrected-body immunity (`DoomsdayMachine.cs:1003-1015`) | TheBoys only (`AchievementClass.cs:554-556`) |
| `x_monster_witness` I Saw the Beast | any non-pawn attacker × Монстр без имени | round-10 attacker receives the non-pawn Пейзаж конца света payout (`CP:4615-4626`) | that attacker (`AchievementClass.cs:558-559`) |

Active Вечное Цукуеми is a global privacy exception: game-end evaluation returns before every non-Madara achievement, including these interactions, so the real authoritative result cannot contradict the viewer-specific ending (`AchievementClass.cs:426-439`).
