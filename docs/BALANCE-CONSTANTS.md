# Balance Constants — every tunable number with its code anchor

> Hand-maintained. **Update the row when you change the number** (and re-run `tools/audit-passives.sh` for name changes). Verified against the working tree of 2026-07-01. `CP` = `Game/GameLogic/CharacterPassives.cs`, `CC` = `Game/Classes/CharacterClass.cs`, `DM` = `Game/GameLogic/DoomsdayMachine.cs`, `CIR` = `Game/GameLogic/CheckIfReady.cs`, `GR` = `Game/ReactionHandling/GameReactions.cs`.
>
> RNG note: `Luck(x)` ≈ x%, `Luck(a,b)` ≈ a-in-b (rounded to whole %); see `Helpers/SecureRandom.cs:51-61`.

## Fight math (CalculateRounds.cs)

| Constant | Value | Anchor |
|---|---|---|
| Nemesis weighing bonus | ±2 | :83, :90 |
| Nemesis skill multiplier (attacker side) | ×1.5 | :79 |
| Scale: skill contribution divisor | /60 | :130-131 |
| Versatility bonus (majority of Int/Str/Speed) | ±5 | :171, :181 |
| Psyche-diff weighing | 1–3→±1, 4–5→±2, ≥6→±4 | :200-220 |
| TooGOOD threshold / effect | \|weighing\| ≥ 13 → randomForPoint = 70 / 30 | :228-251 |
| Skill-difference divisor | /650 | :268-272 |
| TooSTONK threshold / effect | \|weighing\| ≥ 30 → += weighing/2, cap ±20 | :303-344 |
| Justice weighing term | +(myJ − targetJ) | :352 |
| Step-3 random base | roll 1..100 vs 50 base | :63, :446 |
| Step-3 justice window shrink | (myJ×nemesis − targetJ) × 5, when either J > 1 | :447-452 |
| Skill random modifier | (mySkill − targetSkill)/60, uncapped | :370-374 |
| IsAbleToWin override | ±50 pointsWined | DM:623-632 |

## Score, rounds, game flow

| Constant | Value | Anchor |
|---|---|---|
| Rounds / game end | 10 fight rounds; end at RoundNo ≥ 11; hard cap 20 (Kratos) | CIR:934-937 |
| Regular-point round multiplier | r1-4 ×1, r5-9 ×2, r10 ×4 | InGameStatusClass.cs:288-300 |
| Level-up rounds | 3, 5, 7, 9 (+1 point each) | DM:1375-1379 |
| Turn length | 300 s default; ARAM round 2+ = 300 s | GameClass.cs:13, DM:1260-1263 |
| Block: attacker cost / defender gain | −1 bonus point / +1 next-round justice | DM:489-491 |
| Prediction bonus | +1 bonus (+2 Великий летописец); ×компромат for M.M. | CIR:294-338 |
| Predictions editable until | round 8 (round 9 auto-confirm) | CIR:1333-1343 |
| Moral→Skill tiers | 1→2, 2→6, 3→10, 5→18, 8→30, 13→50, 20→100; Еврей 7→40 | GR:45-100 |
| Moral→Score tiers | 5→+1, 8→+2, 13→+5, 20→+10 (staged, flushed next round) | GR:102-133 |
| Round-10 forced moral dump | while moral ≥ 5 → score | CIR:1298-1301 |
| Justice clamp | 0–5; win zeroes before buffer lands | CC:1631-1712 |
| Psyche clamp / moral bonus | 0–10; ≥10 → +20%/tier moral | CC:1292-1302, 1035-1065 |
| Мишень main-skill gains | 10,9,8,…,1 (added twice: Main+Extra) ×(1+TargetSkillMultiplier) | CC:961-1000 |
| Class perks | Умный +6 (vs 0-J), Быстрый +2, Сильный +4 — ×ClassSkillMultiplier | DM:591-601, 754-755 |
| ZBS payout | 100/50/40/30/20/10 by place (ties with winner = 100; team 100/50) | CIR:632-667 |
| Mastery payout | 10/7/5/3/2/1 by place (alive only) | CIR:618-624 |
| Loot boxes | top-2 alive | CIR:673-676 |

## Quality / resists / Harm

| Constant | Value | Anchor |
|---|---|---|
| Resist pool by stat | 0-3→1, 4-7→2, ≥8→3 (Speed ≥8→**5**) | CC:459-509 |
| Stat-10 bonus flags | +10% skill / +1 drop pierce / +1 kite / +20% moral | CC:469, 482, 495, 508 |
| Harm effects | −1 Int/Psyche/Str pools; Str 10 attacker pierces Str −2 | CC:232-284 |
| Pool break penalties | Int −10% Skill; Psyche −20% Moral; Str → Drop | CC:287-323 |
| Drop cost | −1 bonus point + 1 slot down (place 6 immune) | CC:179-193, DM:1452-1485 |
| Harm range | attacker Speed-resist − defender kite bonus ≥ place gap; round 1 Harm-free | DM:824-837, CC:198 |

## Roll system

| Constant | Value | Anchor |
|---|---|---|
| Tier ranges | 6→150, 5→100, 4→90, 3→80, 2→70, 1→60, 0→50, −1→40 | StartGameLogic.cs:38-52 |
| Tier semantics | −1 secret-rollable (hidden from menus), −2 transform-only | CharactersPull.cs:28-50 |
| Tier pity | +3% per game without that tier; reset round 2 | StartGameLogic.cs:158-159, CIR:1317-1324 |
| Bot rules | tier 4 ×3; no tier <4 except Кира at ½ tier-1 range | StartGameLogic.cs:154-156 |
| Top Laner decay | ×1.0 → −0.2 per Top Laner rolled (floor 0) | StartGameLogic.cs:157, 172-177 |
| Exclusivity | LeCrisp ⊕ Толя; single Tier-4 per game; no repeat of last character | StartGameLogic.cs:180-201 |

## Per-character numbers

| Character | Constant | Value | Anchor |
|---|---|---|---|
| DeepList | Безумие schedule / skill mult / stats | 2(+≤1) rounds in 4-7; ×4 multipliers; stats random 0-10 | PassivesClass.cs:21, CP:5311-5374 |
| DeepList | Сверхразум discoveries | 1(+≤2) in rounds 1-5 | PassivesClass.cs:16 |
| DeepList | Стёб | 2nd win: −1 Psyche (Школьник −2), +1 regular; <4 Psyche → −1 J | CP:2569-2624 |
| mylorik | Спарта multipliers | ×2/×4/×8/×16 by losses to target | CP:1305-1330 |
| mylorik | Буль | skip 1/(10+5·psyche) below 7; psyche-0 rage +2 Str +22 Skill | CP:4886-4915 |
| mylorik | Испанец | 50% (pity: every 2nd) → +10 Skill −1 Psyche; Harm→+1 Moral | CP:2657-2681, CC:213-221 |
| mylorik | Месть | +2 regular +3 Moral +1 Psyche per revenge | CP:2626-2654 |
| Глеб | Сон / Претендент | 2(+≤2) sleeps (−30 Skill); Претендент: stats 9, +99 Skill, Мишень ×3, points ×3; round-10 pity 1/(40−place×4) | PassivesClass.cs:44-61, CP:5149-5228, 3516-3547 |
| Глеб | Чай | ready 1/8 (1/7 chall., 1/4 Rick), guaranteed r9; +1 regular, target skips | CP:5121-5147, 1196-1207 |
| LeCrisp | Ассассины | surrender at Str diff ≥ 3; +1 Psyche/next-round per non-assassin | CP:485-504, 776-784 |
| LeCrisp | Импакт | +1 bonus +1 J per clean round; wins +(streak+1) Moral; Speed-resist 6 / kite 2 | CP:3577-3593, 2701-2710, CC:396-442 |
| Толя | Раммус | +1 J, Moral = attackers² | CP:3659-3696 |
| Толя | Подсчет | initial cd 2-3; recharge 4-5 (⚠ m8); +2 regular +2 J per target loss; target ×1 multiplier | PassivesClass.cs:31, CP:1088-1096, 2380-2394 |
| Толя | Комментатор | rounds 3-6, 20%/round, max 2 reveals | CP:3595-3657 |
| HardKitty | Одиночество | −30 score at start; +1 regular per attack; letters 1/2/4 by round | CP:159-168, 517-553 |
| HardKitty | Доебаться | stacks ×2 regular on cash-in; ≥7 stacks +10 | CP:2731-2749 |
| Sirinoks | Обучение | +1 stat/round; completion +3 Moral +10% Skill | CP:3705-3757 |
| Sirinoks | Дракон | stats 10; bonus = Skill/10 − friends below | CP:5378-5409 |
| Школьник | Дерзкая школота | +100 Skill start; −20 Skill & −2 random stats/round | CP:185-191, 3901-3941 |
| Школьник | Запах мусора | −5 bonus per double-attacker after r10 | CP:5935-5958 |
| Школьник | Школьник | 1 forced skip in rounds 2-9; +5 next-round J | PassivesClass.cs:39, CP:4970-4986 |
| AWDKA | Я пытаюсь | 2nd loss: +2 lvl-ups +20 Skill; ×2 skill vs stacked | CP:5005-5019, 1276-1280 |
| AWDKA | АФКА | skip 1/(32−4·roundsSinceMoral), min 1/1 | CP:4988-5003 |
| AWDKA | Троллинг | (top1-recorded-score+1)/2 + correct predictions | CIR:434-445 |
| Осьминожка | Привет со дна | any moral gain = +4; +1 bonus per block/skip event | CC:1143-1153, CP:3699-3703 |
| Darksci | Повезло | stable +100% score/+2/+2; unstable +200%/+4/+4 | CP:1952-1977 |
| Darksci | Не повезло (stable) | +20 Skill +2 Moral per round | CP:5991-6000 |
| Darksci | Дизмораль | −5 Psyche on round 9 (via level-up ⚠ D1) | GR:1226-1231 |
| Тигр | Лучше с двумя | +3 bonus per Int/Psyche match (⚠ self-counts, M6) | CP:3471-3487 |
| Тигр | 3-0 | 3 wins: +3 regular +30 Skill +3 Moral; victim −1 Int −1 Psyche | CP:3765-3833 |
| Тигр | Тигр топ | TimeCount 3; random re-arm 1(+≤1) in rounds 1-8 (⚠ game-start window, M5); #1 → +1 Psyche +3 Moral (r2-9) | Tigr.cs:10, PassivesClass.cs:26-28, CP:5916-5923 |
| Братишка | Челюсти | +1 Speed per unique win & new place | CP:2838-2851, 5901-5912 |
| Спартанец | Привилегия | win r5+: victim +1 extra J, self −1 Int; extra Str-Harm vs top-3 by skill ratio | CP:2876-2884, CC:237-277 |
| Спартанец | Первая кровь | ×2 skill all game; ±1 Speed on first-attack outcome | CP:105-107, 2853-2872 |
| Спартанец | Позорят | −1 Str −1 Speed per unique first attack (mylorik/Кратос spared: +1 Psyche both) | CP:1168-1193 |
| Вампур | Гематофагия | +2 stat per unique win; Psyche-priority ≤8 (max 2) | CP:2897-2958 |
| Вампур | СОсиновый кол | loss: −2 stat −1 regular | CP:3989-4016 |
| Вампур | Вампуризм | +victim J (copy) next round; even rounds +Moral/bite | CP:1881-1885, 4020-4025 |
| Краборак | Панцирь | first attack per enemy: auto-block +3 Moral +33 Skill | CP:427-439 |
| Краборак | Болевой порог | 50% per J point → +1 regular instead | CC:1654-1672 |
| Краборак | Хождение боком | attacker Speed 0; 3 scheduled Speed-10 rounds | CP:441-443, PassivesClass.cs:64-66 |
| Краборак | Водоросли | +1 bonus attacking places 4-6 | CP:1339-1340 |
| Weedwick | Охотник | Speed ×2 vs 0-J/Rick | CP:1118-1124 |
| Weedwick | Добыча | +winstreak points; Harm rolls 1/place, 1/5, +1/3 vs #1 | CP:1766-1859 |
| Weedwick | Weed | −1 Psyche after 2 dry rounds | CP:5848-5854 |
| Кратос | Класс-мульт | ×2 base, ×4 in event; event = rounds 11-16 | CP:73-75, 2528, 3456 |
| Молодой Глеб | Спокойствие чай | cd 3; +1 regular, target skips | CP:1209-1221, 6086-6095 |
| Молодой Глеб | Мета | up to 3 targets/round; +1 bonus per hit | CP:4721-4761 |
| Сайтама | Лысина | +1000 Skill | CP:210-213 |
| Сайтама | Неприметность | serious = top-2 by Skill (recomputed each round); off at r10 | CP:242-251, 4027-4036 |
| Рик | Пушка | invention Int ≥ 30; +1 charge/lvl-up; fired round ×2 regular points | GR:1155-1165, CP:4046-4069 |
| Рик | Бобы | stack: −1 Str/Speed/Psyche, Int = base×stacks; ≤3 ingredients per lvl-up | CP:2072-2088, GR:1174-1202 |
| Рик | Огурчик | 2 pickle turns; +1 penalty turn if never attacked | CP:4072-4079 |
| Кира | Тетрадь | +2 regular per kill (+4 for L); 15% glass fizzle; Гений −1 Int per kill | CP:4082-4160 |
| Кира | Глаза | 25 Moral; not consumed on L/Монстр | WebGameService.cs:747-764 |
| Кира | L | +5 Moral per round avoiding L; arrest from round 8, −500 | CP:4163-4180, 4764-4805 |
| Итачи | Вороны | −20% Speed per crow (both directions) | CP:1369-1377, 607-615 |
| Итачи | Изанаги | 2 uses | Itachi.cs:18 |
| Итачи | Цукуеми | charge 2; recharge from −2 (⚠ 4 rounds, m9); steals round earnings ×multiplier | CP:3033, 4183-4219 |
| Продавец | Впарить | +500 Skill, 4 rounds, cd 2 | CP:1392-1415 |
| Продавец | Закуп | level-up +10 | GR:1007-1011 |
| Продавец | Сделка | +1 bonus & +5 Moral per deal; round-10 debt steal ⌈debt/2⌉ | CP:2410-2422, 4222-4232, 1677-1688 |
| Продавец | Куш | 10% → attacker steals 2 bonus | CP:2425-2436 |
| Dopa | Взгляд | +2 regular (+4 Фарм) +50 Skill, cd 1 | CP:4244-4271 |
| Dopa | Тактики | Стомп +9 Str +99 Skill; Доминация +20 Skill/−1 bonus/33% −1 Psyche; Роум steal 1 bonus + 3 Moral | CP:5822-5841, 2134-2159 |
| Napoleon | Союз | joint attack: can't lose, +3 Moral; Завоеватель +1 bonus | CP:1433-1448, 2204-2227 |
| Суппорт | Premade | ±1 regular per carry result; Stakes every 3rd round +1; Protect +1 J | CP:2438-2453, 3038-3047, 4311-4318 |
| Суппорт | End-game | both top-2 → support = carry − support + 1 bonus | CIR:472-494 |
| Гоблины | Population | start 20; rates W 1/5, Worker 1/10, Hob 1/15; growth 1+Hobs (×2 +1/+2 on wins) | GoblinSwarm.cs:10-15, CP:2260-2271 |
| Гоблины | Deaths | (10 + 0.5R²/3)% +5/+5 TooGood/Stronk, min 1 | CP:2272-2282 |
| Гоблины | Upgrades | Hob 14→11; Warrior 4→2 (floor 2); Worker 9→6; Festival ×2 once | GR:779-833 |
| Гоблины | Ziggurat | needs 1 of each + score ≥ 3; −3 bonus −1 worker; +1 J +5 Moral/round | CP:6142-6207 |
| Гоблины | Tunnels | 50% escape if Speed ≥ attacker+2 | CP:641-652 |
| Гоблины | Mines | places 1, 2, 6 → +Workers bonus | CP:4338-4352, 1497-1510 |
| Котики | Засада | Минька return +2 bonus +33×rounds Skill; Штормяк eats ½ total score (⚠ M9); cd 2 | CP:3161-3232 |
| Котики | Тrick pool | fight 3/7, bite 1/7 (+10 bonus), vase 3/7 once (catch Skill/3 %, ±1 bonus) | CP:5704-5758, 4653-4704 |
| Toxic Mate | Стартовые | −1000 Moral, −20 bonus | CP:254-261 |
| Toxic Mate | Cancer | +2×transfers on return | CP:2472-2480 |
| Toxic Mate | Tilted | +1 bonus per skip (⚠ M8); +50 if all passive | CP:4320-4336 |
| Монстр | Пейзаж | pawn deaths +1 regular each; attackers +7 regular (×4!) +10 bonus (⚠ D8) | CP:4394-4427 |
| Монстр | Близнец | steals all attacker J on block (+bonus = J) | CP:905-920 |
| Монстр | Выдуманный | +3 bonus on r9 if guessed-at | CP:5565-5592 |
| TheBoys | Члены | +2 stat per lvl-up; ultimates at ×4 | GR:907-991 |
| TheBoys | Francie | orders r1/4/7, window 3, ±1 bonus; chem +level bonus per fair win | CP:283-300, 5622-5652, 3315-3324 |
| TheBoys | Butcher | 2 sup marks (+superheroes always); +10/20 Skill, +1/2 bonus on win (⚠ M7); poker ×(1+n) skill & Harm | CP:5864-5898, 3350-3368, 1530-1538 |
| TheBoys | Kimiko | −RegenLevel attacker J; +10/+20 Skill; virus steals 2/infected | CP:659-676, 735-741, CIR:341-360 |
| TheBoys | M.M. | ±1 team Psyche; r8 kompromat +5 Moral each; predictions ×kompromat | CP:3415-3447, CIR:310-338 |
| Salldorum | Шэн/капсула/летописец | +1 charge/lvl-up; capsule +2 bonus +5 Speed after 3 rounds; rewrite −1/+1 per winner (⚠ m15), +2 Psyche +2 J; ×3 skill vs 3-rounds-ago winner | GR:1168-1172, CP:5794-5814, WebGameService.cs:908-967, CP:2230-2258 |
| Геральт | Заказы | +1 contract/round; +20 Skill per contract fight; oils T1 −1 J / T2 +2 Str / T3 ×3 Skill | CP:5676-5689, 2286-2296, 1542-1574 |
| Геральт | Медитация | Lambert 10% once (skill 0 next round; m16); демандна экономика: advance +2 regular, смерть при Displeasure ≥ 11 (−500) | CP:4489-4496, 4548-4585 |
| Баг | Exploit | pot = losses of exploitable players; claim on patch | DM:73-76, CP:1652-1664 |
| Sakura | — | top-3 = narrative win | CIR:496-508 |
