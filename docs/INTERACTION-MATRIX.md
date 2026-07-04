# Interaction Matrix — cross-character rules

> Hand-maintained; verified 2026-07-01. **When adding/changing a character, add or update its row in every applicable table** — the M10–M12 bugs happened precisely at holes in these matrices. ⚠ = known hole (see AUDIT-FINDINGS). `CP` = CharacterPassives.cs, `CIR` = CheckIfReady.cs, `DM` = DoomsdayMachine.cs.

## 1. Forced-fight sources × untargetable / no-fight states

Forced-fight sources: **Монстр** no-escape (`CIR:1266-1289`), **Шэн** below-position pull (`CIR:1184-1199`), **Штормяк** taunt (`CIR:1217-1251`), **Aggress** self auto-attack (`CIR:1171-1182`), **Геральт** contract multi-fight injection (`DM:293-348`).

| State ↓ / Source → | Монстр | Шэн | Штормяк | Aggress (self) | Геральт inject |
|---|---|---|---|---|---|
| Dead player | ✓ excluded | ✓ excluded | ✓ excluded | ✓ targets exclude dead | n/a (dead don't fight) |
| Тигр round-10 ban | ✓ carve-out `CIR:1293` | ✓ carve-out (M11 fixed, `CIR:1213`) | ✓ carve-out (M11 fixed, `CIR:1247`) | n/a | targeting already blocked (`GR:702-707`) |
| Огурчик Рик (pickle) | ✓ unaffected (no IsBlock/IsSkip) | pulls him, but pickle can't lose | can taunt him; he can't lose | n/a | injection works; pickle still can't lose |
| Block | overridden (stripped) | fight happens anyway (forced list bypasses block-skip `DM:393-407`) | taunt bypasses own block (`DM:471-474`) | Aggress can't block at all | blocked Геральт = no injection (`DM:303,326`) |
| Skip (sleep/tilt/ban) | overridden (stripped) | fight happens anyway | fight happens anyway | Aggress can't skip | skipping Геральт = no injection |
| Ziggurat lock | position only — fights unaffected | position only | position only | n/a | n/a |
| Premade Carry | n/a | n/a | n/a | n/a | anti-skip now exempts the round-10 Тигр ban (M10 fixed, `CP:5689-5704`) |

## 2. Kill sources × immunities

Kill sources: Кира's Тетрадь (`CP:3988-4066`), Кира's L-arrest (self-kill, `CP:4670-4711`), Кратос event kills (`CP:1655-1675, 2420-2436`), Монстр Пейзаж pawn deaths (`CP:4300-4333`), Геральт pitchfork displeasure (self, `CP:4483-4490`).

| Immunity ↓ / Source → | Тетрадь | Кратос kill | Пейзаж pawns | Notes |
|---|---|---|---|---|
| Стая Гоблинов ("нельзя убить") | ✓ `CP:4009` | ✓ `CP:1660` (+arrest `CP:4686`) | ✗ **die here — intended** (M12, ОК) | design: GameDesign.txt:509 |
| Глаз Шусуи (Итачи) | revives next round | revives next round | revives next round | one-time, any source (`CP:5365-5374`) |
| Боги мне не указ (Кратос) | ✓ revives +228 Skill | n/a | ✗ not covered (source ≠ "Kira") | source-check is `== "Kira"` only (`CP:5377-5386`) |
| Dead state effects | — | — | — | auto-block/ready, 0 ZBS, no mastery, excluded from forced pools (`CIR:1032-1037, 622-665`) |

## 3. Position movers × position locks

Movers (end-of-round order): Тигр-топ swap → Portal-Gun swap → HardKitty forced last → place assignment → **Ziggurat restore** → Storm-bite restore/swap → Quality Drop (`DM:1295-1499`). Mid-turn movers: AWDKA forced last (intended — M3, ОК) (`CIR:1112-1127`), HardKitty forced last (`CIR:1141-1151`), Шэн post-sort swap (`CP:6117-6142`).

| Lock ↓ / Mover → | Тигр-топ | Portal Gun | Quality Drop | Storm bite | Шэн |
|---|---|---|---|---|---|
| Ziggurat (`IsInZiggurat`) | ✓ blocked `DM:1313` | ✓ blocked `DM:1338` | ✓ can't drop onto/out `DM:1458-1469` | ✓ blocked `DM:1413,1438` | ✓ blocked `CP:6128` |
| HardKitty at place 6 | n/a | n/a | ✓ can't drop onto `DM:1465` | n/a | n/a |
| Тигр ban (round 10) | ✓ swap suppressed `DM:1304-1306` | n/a | n/a | n/a | ✓ pull now respects the ban (M11 fixed, `CIR:1213`) |

## 4. Steal / copy / redirect chains

| Mechanic | Direction | Interacts with | Verified behavior |
|---|---|---|---|
| Еврей (`HandleJews`, `CP:6594-6672`) | steals fight win point | Октопус ink | ink debits the Jew instead of the attacker (`CP:6691-6708`); Napoleon & fellow Евреи immune victims |
| PointFunnel (Баг) | copies regular points | Еврей | funnel copies only `AddWinPoints` — Jew's stolen points not funneled |
| Цукуеми (Итачи) | copies round earnings, deducts at end | Октопус ink | victim pays once: the round-11 ink restore **skips** its debit for a victim under Цукуеми (Итачи deducts instead); both Итачи and Octopus still get their point (D11 fixed, `CP:4773-4790`) |
| Октопус ink | fake-win now, restore at r11 | DeepList first-fight | suppressed until DeepList's scripted loss happens (`CP:6678-6685`) |
| Kimiko Живое Оружие | **drains** attacker Justice | — | real transfer (`CP:744-755`) |
| Близнец (Монстр) | **drains** attacker Justice on block + bonus | — | real transfer (`CP:875-890`) |
| Вампуризм | **copies** victim Justice (intended — D6) | Падальщик | +1 extra from the ignored point (`CP:1842-1846`) |
| Premade | **copies** Carry fight-moral (intended — D9) | — | `CP:2366-2369` |
| Кошачья засада (cats) | physically moves passives to enemy | Минька/Штормяк vs owner | transferred cat won't buff/taunt against Котики (`CP:3010-3013`, `CIR:1229`) |
| Ziggurat learn | copies a `Standalone` passive | everything | see §6 |
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
| M.M. IsCalm (Оковы) | immune to MinusPsycheLog | GamePlayerBridgeClass.cs:97-100 |
| Безумие | psyche bypasses the 0-floor (can go negative) | CC:1295, 1344 |
| Boole Family | immune to Harm entirely | CC:199 |
| Kimiko active | TheBoys immune to Harm | CC:202-210 |
| Испанец | Harm → +1 Moral instead | CC:213-221 |
| Много выебывается | Harm from higher-skill enemy while #1 → self-Drop | CC:223-229 |
| Минька (winner) | deals no Harm and no fight-moral loss | DM:750, 805-806 |

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

## 7. Same-target stacking notes

- Two attackers on one defender resolve **sequentially in leaderboard order** — the first fight's ForOneFight effects are reset before the second (`DM:388-411`, ResetFight per fight).
- Со-attack interactions verified: Еврей steal (needs the Jew to also attack the target), Сайтама deferral (needs a co-attacker), Наполеон joint-attack auto-win (needs the ally to attack the same target).
- Round-10 settlement order (who claws back first): Пейзаж deaths & Saitama banking happen in round-10 `HandleEndOfRound`; Чернильная завеса restore and Ищет достойного (One Punch) at round-11 `HandleNextRound`; Запах мусора at round-11 after-sorting; then `HandleLastRound`: predictions → M.M. ×компромат → TheBoys virus → Цукуеми deduction → sort → AWDKA → Premade → Sakura (GAME-DESIGN §8E).
