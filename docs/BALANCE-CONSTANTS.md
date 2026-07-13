# Balance Constants — every tunable number with its code anchor

> Hand-maintained. **Update the row when you change the number** (and re-run `tools/audit-passives.sh` for name changes). Verified against the working tree of 2026-07-13 (v4.4.6). `CP` = `Game/GameLogic/CharacterPassives.cs`, `CC` = `Game/Classes/CharacterClass.cs`, `DM` = `Game/GameLogic/DoomsdayMachine.cs`, `CIR` = `Game/GameLogic/CheckIfReady.cs`, `GR` = `Game/ReactionHandling/GameReactions.cs`.
>
> RNG note: `Luck(x)` ≈ x%, `Luck(a,b)` ≈ a-in-b (rounded to whole %); see `Helpers/SecureRandom.cs:35-45`.

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
| Rounds / game end | 10 normal fight rounds; human-Kratos event adds exactly six actions on r11-16 or ends early when Kratos/all enemies die; hard safety cap 20 | `CheckIfReady.TickAsync`; CP «Возвращение из мертвых» |
| Regular-point round multiplier | r1-4 ×1, r5-9 ×2, r10 ×4 | `InGameStatusClass.cs` `GetRoundScoreMultiplier` |
| Score floor / exceptions | regular + bonus floor at 0; HardKitty may go negative; Kira arrest and Geralt pitchfork explicitly apply true −500 through the floor | `InGameStatusClass` `AddBonusPointsCore`/`AddScoreWithMultiplier`/`AddBonusPointsIgnoringFloor` |
| Level-up rounds | 3, 5, 7, 9 (+1 point each) | DM:1375-1379 |
| Turn length | 300 s default; ARAM round 2+ = 300 s | GameClass.cs:13, DM:1260-1263 |
| Block: attacker cost / defender gain | −1 bonus point / +1 next-round justice | DM:489-491 |
| Prediction bonus | +1 bonus (+2 Великий летописец); ×компромат for M.M.; Sakura/unknown_bug are inadmissible | `CheckIfReady.HandleLastRound`; `Sakura`; `UnknownBug` |
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
| Loot-box inventory award | reward-place top-2 and alive (Sakura top-3 soft win uses reward place 1) | CIR:648-651,730-732 |

## Character store economy

The multiplier changes a character's relative roll weight, not a standalone final probability; tier weights, pity, roster exclusions and the other eligible characters still apply.

| Constant | Value | Anchor |
|---|---|---|
| Roll-weight bounds / step | ×0.50–×2.00; ±0.01 per purchased percentage point | `WebGameService.cs` `StoreMinMultiplier`/`StoreMaxMultiplier`/`AdjustStoreCharacter`; Discord guards `StoreReactions.cs:217-340` |
| Step price | step N costs 10 + all prior purchased steps ZBS; 10-step action sums ten sequential prices | `WebGameService.cs` `CalculateStoreCost`; Discord `_basePrice` `StoreReactions.cs:20` |
| Refund | free; returns the exact arithmetic-series cost of every purchased step and restores ×1.00 | `WebGameService.cs` `ResetStoreCharacter`/`ResetStoreAllCharacters`/`CalculateStoreRefund`; Discord `StoreReactions.cs:416-515` |
| unknown_bug shop weight | fixed at the untouched ×1.00 baseline; never listed, discovered or adjustable. Legacy investments are refunded during account migration | `StartGameLogic.cs` roll weighting; `UserAccounts.MigrateUnknownBugAccount`; web/Discord store filters |

## Generated story

| Constant | Value | Anchor |
|---|---|---|
| Anthropic response budget | max 1,800 tokens per enabled language request | GameStoryService.cs:31-32,663-674 |
| Prompted short-form ceiling / shape | max 250 words and 1,700 characters; 8–15 very short lines covering only the 3–5 strongest character interactions | GameStoryService.cs:448-492 |

## Daily Quest rewards

The full 12-contract catalog, selection, privacy and migration rules are in [DAILY-QUESTS.md](DAILY-QUESTS.md). The three lanes always pay at most 80 card ZBS; completing the daily and weekly paths brings the direct full-board total to 100 ZBS/day plus the optional mastery box (`QuestClass.cs:200-260,638-678`).

| Constant | Value | Anchor |
|---|---|---|
| Anchor card | fixed finish-one-match goal; 20 ZBS | QuestClass.cs:208-213 |
| Skirmish card | one of 5 personalized goals; 30 ZBS | QuestClass.cs:215-234 |
| Ambition card | one of 6 personalized goals; 30 ZBS | QuestClass.cs:236-260 |
| Daily completion | any 2/3; +20 ZBS, streak + weekly stamp | QuestClass.cs:200-206,651-661 |
| Daily mastery | 3/3; +1 loot box | QuestClass.cs:200-206,663-668 |
| Weekly journey | any 5 UTC daily completions in one ISO week; +100 ZBS | QuestClass.cs:200-206,670-675,690-723 |
| Free reroll | 1 per UTC day; unfinished Skirmish/Ambition only | QuestClass.cs:414-474,510-540 |

## Mini-game rewards

| Constant | Value | Anchor |
|---|---|---|
| Battleship first win of the UTC day | 10 ZBS; once per account/day after combat starts | BattleshipService.cs:15-16,75-95 |

## Achievement & loot-box rewards

Achievement progress targets and the complete 103-entry rule catalog are in [ACHIEVEMENTS.md](ACHIEVEMENTS.md). Reward values are centralized by rarity; the live catalog contains 11 Common, 25 Uncommon, 20 Rare, 33 Epic and 14 Legendary cards, totalling **8,227 ZBS + 61 boxes** (`AchievementClass.cs` `AchievementDefinition`/`AllAchievements`).

| Constant | Value | Anchor |
|---|---|---|
| Common achievement reward | 10 ZBS | AchievementClass.cs:65-76 |
| Uncommon achievement reward | 25 ZBS | AchievementClass.cs:65-76 |
| Rare achievement reward | 50 ZBS | AchievementClass.cs:65-76 |
| Epic achievement reward | 100 ZBS + 1 loot box | AchievementClass.cs:65-76 |
| Legendary achievement reward | 228 ZBS + 2 loot boxes | AchievementClass.cs:65-76 |
| Loot Common | 60%; 15–30 ZBS inclusive | QuestClass.cs:268-274 |
| Loot Uncommon | 25%; 40–75 ZBS inclusive | QuestClass.cs:268-274 |
| Loot Rare | 12%; 100–175 ZBS inclusive | QuestClass.cs:268-274 |
| Loot Epic | 2.5%; 300–450 ZBS inclusive | QuestClass.cs:268-274 |
| Loot Legendary | 0.5%; 750 ZBS | QuestClass.cs:268-274 |
| Loot rarity RNG | `SecureRandom.Next(1,10000)` inclusive; exact cumulative cutoffs 50/300/1500/4000 | QuestClass.cs:903-912 |
| Rare+ pity | after 9 consecutive below-Rare results, box 10 preserves natural Rare+ or upgrades Common/Uncommon to Rare; Rare+ resets counter | QuestClass.cs:799-801,873-922 |

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
| Tier ranges | 6→150, 5→100, 4→90, 3→80, 2→70, 1→60, 0→50, −1→40 | StartGameLogic.cs:47-61 |
| Tier semantics | −1 secret-rollable, −2 transform-only. unknown_bug is additionally excluded from every public/draft/store/achievement surface and its roll ignores store multipliers | `CharactersPull.cs` `GetRollableCharacters`; `StartGameLogic.cs` roll weighting; `UnknownBug` guards |
| Tier pity | +3% per game without that tier; reset round 2 | StartGameLogic.cs:181-182, CIR:1317-1324 |
| Bot rules | tier 4 ×3; no tier <4 except Кира at ½ tier-1 range | StartGameLogic.cs:177-179 |
| Top Laner decay | ×1.0 → −0.2 per Top Laner rolled (floor 0) | StartGameLogic.cs:180, 172-177 |
| Exclusivity | LeCrisp ⊕ Толя; single Tier-4 per game; no repeat of last character | StartGameLogic.cs:204-229 |
| Naruto roster eligibility | FFA only; human original requires ≥2 strict bots, bot original requires ≥3 strict bots total; exactly 2 become clones | `StartGameLogic.cs` `CanNaturallyRollNaruto`; `Naruto.cs` `CanUseRoster`, `InitializeTeam` |

## Per-character numbers

| Character | Constant | Value | Anchor |
|---|---|---|---|
| DeepList | Безумие schedule / skill mult / stats | 2(+≤1) rounds in 4-7; ×4 multipliers; stats random 0-10 | PassivesClass.cs:21, CP:5387-5450 |
| DeepList | Сверхразум discoveries | 1(+≤2) in rounds 1-5 | PassivesClass.cs:16 |
| DeepList | Стёб | 2nd win: −1 Psyche (Школьник −2), +1 regular; <4 Psyche → −1 J | CP:2556-2616 |
| mylorik | Спарта multipliers | ×2/×4/×8/×16 by losses to target | CP:1341-1366 |
| mylorik | Буль | skip 1/(10+5·psyche) below 7; psyche-0 rage +2 Str +22 Skill | CP:4906-4935 |
| mylorik | Испанец | 50% (pity: every 2nd) → +10 Skill −1 Psyche; Harm→+1 Moral | CP:2649-2673, CC:213-221 |
| mylorik | Месть | +2 regular +3 Moral +1 Psyche per revenge | CP:2618-2646 |
| Глеб | Сон / Претендент | 2(+≤2) sleeps (−30 Skill); Претендент: stats 9, +99 Skill, Мишень ×3, points ×3; round-10 pity 1/(40−place×4) | PassivesClass.cs:44-61, CP:5217-5301, 3422-3453 |
| Глеб | Чай | ready 1/8 (1/7 chall., 1/4 Rick), guaranteed r9; +1 regular, target skips | CP:5145-5215, 1166-1177 |
| LeCrisp | Ассассины | surrender at Str diff ≥ 3; +1 Psyche/next-round per non-assassin | CP:527-546, 776-784 |
| LeCrisp | Импакт | +1 bonus +1 J per clean round; wins +(streak+1) Moral; Speed-resist 6 / kite 2 | CP:3560-3576, 2619-2628, CC:396-442 |
| Толя | Раммус | +1 J, Moral = attackers² | CP:3642-3679 |
| Толя | Подсчет | initial cd 2-3; recharge 4-5 (⚠ m8); +2 regular +2 J per target loss; target ×1 multiplier | PassivesClass.cs:31, CP:1123-1131, 2298-2312 |
| Толя | Комментатор | rounds 3-6, 20%/round, max 2 reveals | CP:3578-3640 |
| HardKitty | Одиночество | −30 score at start; +1 regular per attack; letters 1/2/4 by round | CP:201-210, 517-553 |
| HardKitty | Доебаться | stacks ×2 regular on cash-in; ≥7 stacks +10 | CP:2723-2741 |
| Sirinoks | Обучение | +1 stat/round; completion +3 Moral +10% Skill | CP:3688-3759 |
| Sirinoks | Дракон | stats 10; bonus = Skill/10 − friends below | CP:5450-5452 |
| Школьник | Дерзкая школота | +100 Skill start; −20 Skill & −2 random stats/round | CP:227-233, 3807-3847 |
| Школьник | Запах мусора | −5 bonus per double-attacker after r10 | CP:5979-6002 |
| Школьник | Школьник | 1 forced skip in rounds 2-9; +5 next-round J | PassivesClass.cs:39, CP:4990-5006 |
| AWDKA | Я пытаюсь | 2nd loss: +2 lvl-ups +20 Skill; ×2 skill vs stacked | CP:5025-5039, 1246-1250 |
| AWDKA | АФКА | skip 1/(32−4·roundsSinceMoral), min 1/1 | CP:5008-5023 |
| AWDKA | Троллинг | (top1-recorded-score+1)/2 + correct predictions | CIR:434-445 |
| Осьминожка | Привет со дна | any moral gain = +4; +1 bonus per block/skip event | CC:1143-1153, CP:3682-3686 |
| Darksci | Повезло | stable +100% score/+2/+2; unstable +200%/+4/+4 | CP:1980-2005 |
| Darksci | Не повезло (stable) | +20 Skill +2 Moral per round | CP:6035-6044 |
| Darksci | Дизмораль | −5 Psyche on round 9 (via level-up ⚠ D1) | GR:1226-1231 |
| Тигр | Лучше с двумя | +3 bonus per Int/Psyche match (⚠ self-counts, M6) | CP:3454-3470 |
| Тигр | 3-0 | 3 wins: +3 regular +30 Skill +3 Moral; victim −1 Int −1 Psyche | CP:3767-3835 |
| Тигр | Тигр топ | TimeCount 3; random re-arm 1(+≤1) in rounds 1-8 (⚠ game-start window, M5); #1 → +1 Psyche +3 Moral (r2-9) | Tigr.cs:10, PassivesClass.cs:26-28, CP:5960-5967 |
| Братишка | Челюсти | +1 Speed per unique win & new place | CP:2821-2834, 5807-5818 |
| Спартанец | Привилегия | win r5+: victim +1 extra J, self −1 Int; extra Str-Harm vs top-3 by skill ratio | CP:2859-2867, CC:237-277 |
| Спартанец | Первая кровь | ×2 skill all game; ±1 Speed on first-attack outcome | CP:147-149, 2759-2778 |
| Спартанец | Позорят | −1 Str −1 Speed per unique first attack (mylorik/Кратос spared: +1 Psyche both) | CP:1204-1229 |
| Вампур | Гематофагия | +2 stat per unique win; Psyche-priority ≤8 (max 2) | CP:2880-2941 |
| Вампур | СОсиновый кол | loss: −2 stat −1 regular | CP:3985-4012 |
| Вампур | Вампуризм | +victim J (copy) next round; even rounds +Moral/bite | CP:1909-1913, 3926-3931 |
| Краборак | Панцирь | first attack per enemy: auto-block +3 Moral +33 Skill | CP:469-481 |
| Краборак | Болевой порог | 50% per J point → +1 regular instead | CC:1654-1672 |
| Краборак | Хождение боком | attacker Speed 0; 3 scheduled Speed-10 rounds | CP:483-485, PassivesClass.cs:64-66 |
| Краборак | Водоросли | +1 bonus attacking places 4-6 | CP:1375-1376 |
| Weedwick | Охотник | Speed ×2 vs 0-J/Rick | CP:1153-1159 |
| Weedwick | Добыча | +winstreak points; Harm rolls 1/place, 1/5, +1/3 vs #1 | CP:1793-1887 |
| Weedwick | Weed | −1 Psyche after 2 dry rounds | CP:5892-5898 |
| Кратос | Класс-мульт / event | ×2 base, ×4 in event; human-only; exactly six actions r11-16, only Kratos acts | CP:73-112,2795-2806,3872-3882; `DoomsdayMachine.EnforceKratosEventActions` |
| Молодой Глеб | Спокойствие чай | cd 3; +1 regular, target skips | CP:1245-1257, 5992-6001 |
| Молодой Глеб | Мета | up to 3 targets/round; +1 bonus per hit | CP:4740-4780 |
| Сайтама | Лысина | +1000 Skill | CP:252-255 |
| Сайтама | Неприметность | serious = top-2 by Skill (recomputed each round); off at r10 | CP:284-293, 3933-3942 |
| Мадара | base / rarity | Int 7, Str 9, Speed 10, Psyche 9; Tier 5 | characters.json:1486-1521 |
| Мадара | Бог шиноби thresholds | >1 unique attacker: TooGOOD; >2: TooSTONK; >3: fight Skill = 100 | Madara.cs:55-110; CP:461-467,1019-1025 |
| Мадара | Второй метеорит | blocked attack: no −1 bonus; +2 regular | DM:519-544 |
| Мадара | Клоны Сусано | round 8; live strict-bot reaction delay 30 s; L0/L1 exact prediction but ordinary action; strict-bot Наруто/Sakura/Итачи exact-predict + attack at every level; +1 live Justice at >2 unique attackers; seal at all 5 unique + ≥5 losses | `Madara.RoundEightBotReactionDelaySeconds`; `Madara.ForceRoundEightBotPrediction`; `Madara.MustAcceptRoundEightBotChallenge`; `Madara.RefreshIncomingEffects` |
| Мадара | Вечное Цукуеми | arm at all 5 unique attackers in one turn or place 1 entering r10; authoritative r10 = total Skip/no combat; viewer bonus = max living score − viewer score + 1 (0 if sole winner) | `Madara.PrepareEternalTsukuyomiRound`; `Madara.GetIllusoryBonus` |
| Рик | Пушка | invention Int ≥ 30; +1 charge/lvl-up; fired round ×2 regular points | GR:1155-1165, CP:4042-4066 |
| Рик | Бобы | stack: −1 Str/Speed/Psyche, Int = base×stacks; ≤3 ingredients per lvl-up | CP:2100-2116, GR:1174-1202 |
| Рик | Огурчик | 2 pickle turns; +1 penalty turn if never attacked | CP:4069-4076 |
| Кира | Тетрадь | +2 regular per kill (+4 for L); 15% glass fizzle; Гений −1 Int per kill | CP:4079-4157 |
| Кира | Глаза | 25 Moral; not consumed on L/Монстр/Sakura/unknown_bug | `WebGameService.ShinigamiEyes`; CP:1143-1166 |
| Кира | L | +5 Moral per round avoiding L; arrest from round 8, true −500 through floor | CP:4160-4177,5351-5388 |
| Итачи | Вороны | −20% Speed per crow (both directions) | CP:1405-1413, 607-615 |
| Итачи | Изанаги | 2 uses | Itachi.cs:18 |
| Итачи | Цукуеми | charge 2; recharge from −2 (⚠ 4 rounds, m9); steals round earnings ×multiplier | CP:3016, 4089-4125 |
| Продавец | Впарить | +500 Skill, 4 rounds, cd 2 | CP:1428-1451 |
| Продавец | Закуп | level-up +10 | GR:1007-1011 |
| Продавец | Сделка | +1 bonus & +5 Moral per deal; round-10 debt steal ⌈debt/2⌉ | CP:2397-2409, 4128-4138, 1638-1649 |
| Продавец | Куш | 10% → attacker steals 2 bonus | CP:2412-2423 |
| Dopa | Взгляд | +2 regular (+4 Фарм) +50 Skill, cd 1 | CP:4241-4266 |
| Dopa | Тактики | Стомп +9 Str +99 Skill; Доминация +20 Skill/−1 bonus/33% −1 Psyche; Роум steal 1 bonus + 3 Moral | CP:5866-5885, 2134-2159 |
| Napoleon | Союз | joint attack: can't lose, +3 Moral; Завоеватель +1 bonus | CP:1460-1475, 2122-2145 |
| Суппорт | Premade | ±1 regular per carry result; Stakes every 3rd round +1; Protect +1 J | CP:2425-2440, 2944-2953, 4217-4224 |
| Суппорт | End-game | both top-2 → support = carry − support + 1 bonus | CIR:472-494 |
| Гоблины | Population | start 20; rates W 1/5, Worker 1/10, Hob 1/15; growth 1+Hobs (×2 +1/+2 on wins) | GoblinSwarm.cs:10-15, CP:2245-2258 |
| Гоблины | Deaths | (10 + 0.5R²/3)% +5/+5 TooGood/Stronk, min 1 | CP:2259-2269 |
| Гоблины | Upgrades | Hob 14→11; Warrior 4→2 (floor 2); Worker 9→6; Festival ×2 once | GR:779-833 |
| Гоблины | Ziggurat | needs 1 of each + score > 3; −3 bonus −1 worker; +1 J +5 Moral/round | CP:6604-6677 |
| Гоблины | Tunnels | 50% escape if Speed ≥ attacker+2 | CP:683-694 |
| Гоблины | Mines | places 1, 2, 6 → +Workers bonus | CP:4335-4349, 1458-1471 |
| Котики | Засада | Минька return +2 bonus +33×rounds Skill; Штормяк eats ½ total score (⚠ M9); cd 2 | CP:3144-3215 |
| Котики | Тrick pool | fight 3/7, bite 1/7 (+10 bonus), vase 3/7 once (catch Skill/3 %, ±1 bonus) | CP:5747-5801, 4559-4610 |
| Toxic Mate | Стартовые | −1000 Moral, −20 bonus | CP:296-303 |
| Toxic Mate | Cancer | +2×transfers on return | CP:2459-2467 |
| Toxic Mate | Tilted | +1 bonus per skip (⚠ M8); +50 if all passive | CP:4317-4333 |
| Монстр | Пейзаж | pawn deaths +1 regular each; attackers +7 regular (×4!) +10 bonus (⚠ D8) | CP:4391-4424 |
| Монстр | no-escape | every attack marks before Block/Skip; next 2 turns attack-only; per-target overlapping expiry | CP:1066-1073; CIR:1371-1393 |
| Монстр | Близнец | no generic block J; copies max attacker J without draining (+total bonus = max J) | CP:978-1002; DM:564-568 |
| Монстр | Выдуманный | +3 bonus on r9 if guessed-at | CP:5608-5635 |
| TheBoys | Члены | +2 stat per lvl-up; ultimates at ×4; post-СуперМудень upgrades inert/consumed | GR:968-1057 |
| TheBoys | Francie | orders r1/4/7, window 3, completion/expiry ±1 bonus; chem `level × (1 + harder-tier)` per eligible win | CP:356-372, 3405-3437, 5914-5943 |
| TheBoys | Butcher | 2 rotating sup marks + permanent heroes/Young Gleb/Challenger Gleb; hunt Skill +10/+20; **+1/+2 regular per actual Drop**; poker Skill/Harm ×`(1+n)` / exact SD ×`2(1+n)`; recursive any-pool breaks, 50 Drops/turn | CP:1627-1637,3464-3483,6181-6215; DM:928-1006 |
| TheBoys | Kimiko | Regen x1+ ignores up to level Justice and enables disable/recovery; base defense +10/+20 Skill; Living Weapon drains Justice + same regular points | CP:746-765,793-816,951-961,5947-5972 |
| TheBoys | M.M. | ±1 team Psyche; calm immunity from x1; r8 kompromat +5 Moral each; predictions ×kompromat; x4 steals/blocks Moral | CP:3689-3732,7176-7308; GR:1021-1050; CIR:320-350 |
| TheBoys | Смертельный вирус | −2/+2 bonus per infected at game end; disabled under СуперМудень | CIR:353-375 |
| TheBoys | СуперМудень | exact ×2 Butcher Skill/Harm/drop payout; one extra Harm per applied pool break; cap 50 Drops per turn; score-0 stop | DM:928-1006; CC:182-345 |
| Salldorum | Шэн/капсула/летописец | +1 charge/lvl-up, auto-spent by next attack; successful forward dash uses the target's exact cell, holds through the next action round and redirects one existing primary attack per crossed player (adds 0 fights); capsule after 3 rounds = +2 bonus +5 Speed for next fight, one natural drink + at most one history-only second drink (matching history bypasses the natural wait); rewrite −/+ historical multiplier per distinct winner, +2 Psyche +2 buffered J; ×3 Skill attacking or defending vs 3-rounds-ago win leader(s) | `Salldorum.cs` `ResolveShenDashes`/`ApplyShenPositionHolds`/`TryDrinkTimeCapsule`/`RewriteHistory`; `GameReactions.cs` level-up handler; CP:481-483,1112-1116,1779-1807 |
| Геральт | Заказы | +1 contract/round; +20 Skill per contract fight; oils T1 −1 J / T2 +2 Str / T3 ×3 Skill | CP:5719-5732, 2204-2214, 1503-1535 |
| Геральт | Медитация | Lambert 10% once (skill 0 next round; m16); демандна экономика: advance +2 regular, смерть при Displeasure ≥ 11 (true −500 through floor) | CP:5007-5028; `WebGameService.DemandContractReward` |
| unknown_bug | Exploit | +1 pot when a copied source win defeats the current carrier; direct carrier win adds +1; any direct carrier attack then closes globally and pays raw pot as regular × current round multiplier; full-screen commit alarm at post-multiplier >20 | `UnknownBug.RecordResolvedFight` / `TryCommitExploit`; `GameClass.RollExploit` / `CloseExploit` |
| Sakura | Одна из трех | solo-only complete top-3 cutoff; a fourth living tie suppresses; factual place but first-place rewards | `Sakura.HasUncontestedSoloTopThree`; `CheckIfReady.HandleLastRound` |
| DooM Guy | base / newcomer | Int 2, Str 5, Speed 5, Psyche 5, Tier 4; exact 30% protected roll while TotalPlays < 10 | characters.json:1383-1413, StartGameLogic.cs:201-233 |
| DooM Guy | stages / random mode | Rune r3, Shield r5, Mission r7, Gun r9; Let's Roll random pick pays +2 regular each stage | DoomGuy.cs:53-60, 170-175 |
| DooM Guy | Rune | Вознесение +8 Int, at most 8 × −1/loss; Маневры +5 Speed, at most 5 × −1/Harm; Истребление +1 all stats + max(0, 10−round) bonus; Glory kill neighbour Skill ×2 and win +1 all stats | DoomGuy.cs `ApplySelectedModule`/`ApplyFightModules`; CP:2661-2706; CharacterClass.cs:213-221 |
| DooM Guy | Shield | Щит-пила block penalty −3; Шоковый щит 1 auto-submitted forced skip; Адский блок +666 Skill once after 2 blocked attacks; Контр-атака next-turn fight Skill/Justice 0; Щит-акула block→1-turn Ничего не понимает stance | DM:240-249,584-613; CP:813-830,5062-5085; DoomGuy.cs `ApplyFightModules` |
| DooM Guy | Mission | 1 new nest/setup, overflow >3 → −20 bonus + clear; only attack-win nest kill +1 regular; every resolved fight +1 regular; flawless no-block mission +20 bonus; Ближник neighbour melee bonus ×2 (Кулаки +4, Glory total Skill ×3/+2 stats, Бензопила 2 picks) | DoomGuy.cs `SpawnDemonNest`/`ApplyFightModules`; CP:2694-2759,3682-3695 |
| DooM Guy | Gun | BFG 1 charge; primary + every wave Step-3 random auto-wins; Кулаки Str=0 and +2 regular/win; Бензопила 1 victim, up to 4 choices and 1 pick; Рельса 1 charge and whole selected side, Block/Skip bypass except Тигр ban; Приручить дракона = round-10 Дракон transform | DoomGuy.cs `ApplySelectedModule`/`CopyChainsawPassive`; DM:445-491,569-650,810-881; CP:2730-2759,5767-5797 |
| DooM Guy | module reward | place 4/3/2/1 ceiling = Rune/Shield/Mission/Gun; fallback downward; standard chance = 0 complete, 5% last, otherwise `5 + 75×(remaining−1)/(total−1)`; Приручить дракона excluded and guaranteed only after round-10 win over Sirinoks/Дракон | DoomGuy.cs `TryAwardModule`/`TryAwardDragonTaming`; CP:2469-2479; CheckIfReady.cs:758-768 |
| Эрен Йегер | base / rarity / exclusion | Злость(Int) 0, Str 4, Speed 4, Самоуверенность(Psyche) 10; Tier 6; cannot naturally coexist with HardKitty | characters.json:1416-1446, StartGameLogic.cs:273-278 |
| Эрен Йегер | Овца в загоне | forced place 6 through r8; scheduled +1 Int at starts r2-8 (**+7 max**), +1 after every loss; −2 after every win; opening r9 bonus = post-sort place | CP:248-255,2612-2618,5173-5187,6418-6422; `CheckIfReady.TickAsync`; `DoomsdayMachine.CalculateAllFights` |
| Эрен Йегер | Дрочун marks / cash-in | loss mark 1; attacking-Eren mark 2; cap 2; victory cashes target mark as 1/2 bonus | PassivesClass.cs:270-274, CP:480-483,2465-2486 |
| Эрен Йегер | Дрочун mutual attack | +2 regular once per mutual enemy per round | CP:2558-2569 |
| Эрен Йегер | Атакующий Титан | off-cooldown block removed; +5 each stat per fight for the turn; no incoming target → −2 Psyche; cooldown 1 full next turn | DM:282-294; CP:62-72,517-521,1119-1123,3739-3758 |
| Эрен Йегер | Titan audio roll | `use_most` 50%; files 1–3 split the other 50% uniformly | sound.ts:990-995 |
| Эрен Йегер | Rumbling gate / reach | round 10; acting bots at opening places strictly between Eren and 6 must attack Eren; fewer than 2 losses **during round 10 only**; kills projected places strictly between Eren and place 6 | `BotsBehavior.cs` `TryForceRumblingAttack`; CP:2662-2667,3672-3718; ErenYeager.cs:38-53 |
| Наруто | base / rarity | Int 3, Str 3, Speed 4, Psyche 5; Tier 5 | characters.json:1449-1483 |
| Наруто | Гарем но джутсу | Block replacement while ready; +1 regular per canceled valid fight in each reaching attacker's whole queue; cooldown 2 full following turns after every use | `Naruto.cs` `HaremCooldownTurns`, `ResolveHaremQueues`, `TryCancelHaremFights`; CP:3771-3784 |
| Наруто | Теневые | 2 independent strict-bot clones; sibling attacks illegal but living siblings are virtual L0/L1 action slots; r10 settlement immediately after Rumbling; sibling prediction value 0; correct enemy predictions +1 projected once; clone score/death seats end at 0 / bottom two | `Naruto.cs` `InitializeTeam`, `GetBotActionTargetSlotCount`, `ProjectClonePredictionPoints`, `SettleShadowClones`, `OrderLeaderboard` |
| Наруто | Расенган | 2 joint attackers: summed Justice, +2 Str each; 3: summed Justice, +3 Int/Str/Speed/Psyche each | CP:74-108; `Naruto.cs` `SnapshotJustice`, `GetJointAttackers` |
| Наруто | Призыв | exactly 1 Naruto on target; prior-round loss to that target with target TooGOOD or TooSTONK → terminal auto-win, otherwise refusal only | `Naruto.cs` `IsSoloAttack`, `WonPoweredFightLastRound`; DM:880-899 |

## Bot AI difficulty

Per-game `AiDifficulty` is 0/1/2/3 and defaults to **3** for Discord, web and simulation. `--ai-difficulty N` overrides the simulation field; `--ai-probe N [--ai-probe-char "Name"]` and `--ab-char` provide per-seat A/B measurement through `GamePlayerBridgeClass.AiDifficulty` and `BotsBehavior.EffectiveDifficulty`.

**L2 and L3 share one hard visibility boundary.** They may use the acting bot's own state, public leaderboard projection/markers, legal target menu, sanitized global logs, the acting player's current/last personal logs, public resolved fight outcomes and exact detail from fights in which that bot participated. They may not read an opponent's current Block/Skip/attack, real character, stats, passives, score, Justice or private histories. `BotInformation.CaptureVisibleRound` persists only that player-visible evidence in `GamePlayerBridgeClass.AiKnowledge`. L3 differs only in how it reasons over the evidence: longer horizons, confidence-weighted hypotheses, public roster/rule constraints and deterministic best-target selection. The full decision catalogue is [BOT-AI-DESIGNER-REVIEW.md](BOT-AI-DESIGNER-REVIEW.md).

| Constant / rule | Value | Meaning | Anchor |
|---|---:|---|---|
| `AiDifficulty` | **3** | default level; 0 random, 1 frozen legacy, 2 fair strategic, 3 fair advanced inference | `GameClass.AiDifficulty`; `BotsBehavior.EffectiveDifficulty` |
| `--ai-difficulty` / `--ai-probe` / `--ab-char` | 0-3 | whole-field override, one-seat probe and paired seeded A/B measurement | `SimulationRunner`; `BotGameFactory.CreatePlayers` |
| L0 action policy | uniform legal choice | random legal level-up and random target/Block slot; Naruto's two living illegal siblings remain virtual attack slots only for these odds; respects cannot-block, rejected targets and Макро's second action | `BotsBehavior.HandleBotAttackRandom`; `Naruto.GetBotActionTargetSlotCount` |
| L1 knowledge policy | legacy privileged | historical control path is intentionally frozen and is **not** covered by the L2/L3 fairness guarantee; simulation exact-prediction prefill remains L1-only | `BotsBehavior.HandleBotAttack`; `BotGameFactory.CreatePlayers` |
| `AiPlaystyle` | once/match | L2/L3 retain one coherent build/character plan; sim reports record it | `BotsBehavior.EnsureBotPlaystyle`; `GamePlayerBridgeClass.AiPlaystyle` |
| Bot action preparation | spend every point first | level-ups complete before forced Skip, Madara response, Kira action or attack/Block choice | `BotsBehavior.HandleBotBehavior` |
| visible-memory retention | 10 global rounds | stored sanitized global-log snapshots older than completed round − 9 are discarded; structured opponent history remains match-scoped | `BotInformation.CaptureVisibleRound` |
| L2 / L3 general target horizon | 3 / 6 rounds | weighted public results, targeting and defense patterns used by target scoring | `BotsBehavior.ApplyFairUniversalPreference` |
| L2 / L3 incoming-attack horizon | 2 / 5 rounds | estimates how often each opponent publicly attacked this bot; replaces live attack-queue reads | `BotsBehavior.HistoricalIncoming` |
| L2 / L3 target selection | weighted / best | L2 keeps variety and doubles the unique best target's weight; L3 selects the highest score, random only across exact ties | `SmartCommitMultiplier` = 2; `BotsBehavior.PickFairTarget` |
| `SmartTargetTaretNumberEarly` / `...Late` | +3 / +2 | earned Мишень-class preference through round 4 / afterward | `BotsBehavior.ApplyFairUniversalPreference` |
| known nemesis / reverse nemesis | +5 / −2 L2, −4 L3 | applied only after an earned class tell | `BotsBehavior.ApplyFairUniversalPreference` |
| observed Justice comparison | −5…+5 | uses Justice seen in the bot's own fight and advances it only from later public wins/losses; unknown stays unknown | `BotsBehavior.EstimateObservedJustice` |
| public defense-rate trigger | 60% | recent resolved Block/Skip rate causes −2 L2 or −4 L3 caution (−1 when the bot's own kit still progresses) | `BotInformation.DefenseRate`; `BotsBehavior.ApplyFairUniversalPreference` |
| fair fight-edge blend | 65% estimate / 35% observation | L3 estimates public-catalogue stats and blends the most recent own-fight observation; thresholds ±4 / ±10 affect target score. L2 uses only its recent observed own-fight edge thresholds ±5 | `BotsBehavior.EstimateFairFightEdge`; `BotsBehavior.ApplyFairUniversalPreference` |
| `SmartPredictAvoidNumber` | −2 | caution for sufficiently confident hypotheses about punish-passives; L3 scales it by confidence up to ×2.5 | `BotsBehavior.ApplyPredictedOpponentCaution` |
| prediction tier prior | 18/14/12/9/8/7/6 | public-catalogue weight for tiers ≥6/5/4/3/2/1/other | `CharacterPassives.FairPredictionPrior` |
| L2 inferred prediction | 25% prior; 40-85% evidence | stable catalogue prior when evidence score <45, otherwise earned class/own-fight/log evidence; stronger old evidence is retained | `CharacterPassives.HandleFairBotPredict` |
| L3 inferred prediction | 35-92% | same legal evidence plus public natural-roll and incompatibility rules, longer history and an all-different assignment; it never reads the real roster | `CharacterPassives.HandleFairBotPredict`; `ApplyFairRosterConstraints` |
| exact prediction confidence | 100% | public Толя/Коммуникация, owner-only Сверхразум/Naruto reveal, or the explicit round-8 strict-bot Naruto/Sakura/Itachi Madara challenge | `CharacterPassives.SeedFairExactPredictions`; `Madara.ForceRoundEightBotPrediction` |
| Monster hypothesis | abstain | the bot may infer `Монстр без имени`, but leaves the submitted prediction blank because it cannot score and can punish a guess | `CharacterPassives.RecordFairPredictionChoice` |
| L2 generic Block odds | 1/4 neutral | PreferBlock 1/2; PreferAttack 1/5; 0 Justice + best score <7 gives 1/2; top two + historical incoming ≥1.5 + best <10 gives 2/3 | `BotsBehavior.ShouldFairBotBlock` |
| L3 generic Block rules | deterministic | PreferBlock if best <15; top two with incoming ≥1.5 and best <12; bottom three with own Justice ≤1 and best <7; otherwise only if best <5 | `BotsBehavior.ShouldFairBotBlock` |
| round-10 generic economy | leader Block; others attack | only the acting bot's public place is used; character-specific forced action plans still win | `BotsBehavior.ShouldFairBotBlock` |
| `SmartMoralWaitPlace3` / `Place4` / `Leader` | 8 / 13 / 8 Moral | score-conversion patience for L2/L3 | `BotsBehavior.HandleBotMoral` |
| `SmartPsycheFloor` | 4 | generic level-up raises Psyche once the best stat is at least 8; character plans override | `BotsBehavior.HandleLvlUpBot` |
| `SmartSellerMarkFloor` | 20 | makes an unmarked Seller target dominant while the mark is ready | `BotsBehavior.ApplyFairCharacterPreference` |
| persistent plans | random once | Dopa 4; Darksci 2; Глеб 2; TheBoys 4; Goblins 4; Rick/Itachi/Kratos/Cats/Tolya/Monster/Support 2 each; other characters use Adaptive | `BotsBehavior.EnsureBotPlaystyle` |
| fair Kira confidence | L2 25%; L3 35-90%; reveal 100% | L2 uses public catalogue priors; L3 scores legal logs/class/place history; Shinigami Eyes reads an exact identity only after the target ID is in the owner's reveal list | `BotsBehavior.HandleFairBotKira`; `FairKiraGuessScore` |
| L3 Rumbling inference | 82% | public round-10 warning plus a unique opponent observed at place 6 in at least 5 of rounds 1-8; the guess may be absent or wrong | `BotsBehavior.InferPublicRulePatterns` |
