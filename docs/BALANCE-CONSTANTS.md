# Balance Constants — every tunable number with its code anchor

> Hand-maintained. **Update the row when you change the number** (and re-run `tools/audit-passives.sh` for name changes). Verified against the working tree of 2026-07-25 (v5.1.9). `CP` = `Game/GameLogic/CharacterPassives.cs`, `CC` = `Game/Classes/CharacterClass.cs`, `DM` = `Game/GameLogic/DoomsdayMachine.cs`, `CIR` = `Game/GameLogic/CheckIfReady.cs`, `GR` = `Game/ReactionHandling/GameReactions.cs`.
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
| Rounds / game end | 10 normal fight rounds; a human-Kratos r10 fight loss or fatal Kira note with his revive available starts exactly six event actions on r11-16; ends early when Kratos/all enemies die; hard safety cap 20 | `CharacterPassives.StartKratosEvent`; `CheckIfReady.TickAsync`; CP «Возвращение из мертвых» |
| Regular-point round multiplier | r1-4 ×1, r5-9 ×2, r10 ×4 | `InGameStatusClass.cs` `GetRoundScoreMultiplier` |
| Score floor / exceptions | regular + bonus floor at 0; HardKitty may go negative; Kira arrest and Geralt pitchfork explicitly apply true −500 through the floor | `InGameStatusClass` `AddBonusPointsCore`/`AddScoreWithMultiplier`/`AddBonusPointsIgnoringFloor` |
| Джон Сноу royal bonus multiplier / score floor | Король Сервера doubles every positive or negative bonus mutation ×2 before the ordinary floor; blocked at place 4 | `InGameStatusClass.AddBonusPointsCore`; `JonSnow.IsKingActive` |
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
| Refund | free; returns the exact arithmetic-series cost of every purchased step and restores the paid component to ×1.00; permanent loot-box points remain | `WebGameService.cs` `ResetStoreCharacter`/`ResetStoreAllCharacters`/`CalculateStoreRefund`; Discord `StoreReactions.cs:416-515` |
| Loot-box roll-weight bonus | Common/Uncommon/Rare/Epic/Legendary add +1/+2/+3/+5/+10 percentage points to one public character, non-refundable; effective weight remains capped at ×2.00 | `QuestService.LootBoxOdds`/`GenerateLootBox`; `CharacterChances.GetEffectiveMultiplier` |
| unknown_bug shop weight | fixed at the untouched ×1.00 baseline; never listed, discovered or adjustable. Legacy investments are refunded during account migration | `StartGameLogic.cs` roll weighting; `UserAccounts.MigrateUnknownBugAccount`; web/Discord store filters |

## Generated story

| Constant | Value | Anchor |
|---|---|---|
| Anthropic response budget | max 1,800 tokens per enabled language request | GameStoryService.cs:31-32,663-674 |
| Prompted short-form ceiling / shape | max 250 words and 1,700 characters; 8–15 very short lines covering only the 3–5 strongest character interactions | GameStoryService.cs:448-492 |

## 99 Last Chances standalone prototype

The complete browser-prototype definition remains externally editable in `Web/VueClient/public/99lc/game-config.json`; schema v9 keeps the weapon catalog, all three control schemes, the stamina block, every collider/trace and every behavior-specific number in that no-rebuild definition. The Builder exposes the control/timing/feedback values below and preserves bindings, per-node overrides and remaining `tuning` keys through raw JSON import/export.

| Constant | Value | Anchor |
|---|---:|---|
| Basic-tap cooldown | none; `tap` bypasses the special cooldown map and shipped tap fields are 0 ms | `features/last-chances/engine.ts` `performAttack`; `public/99lc/game-config.json` weapon `tap` entries |
| DeepList basic-combo continuation | 900 ms | `public/99lc/game-config.json` `input.tapComboWindowMs`; `features/last-chances/engine.ts` `advanceTapCombo` |
| DeepList recognition delays | double tap/basic-tap resolution/provisional parry 260 ms; hold 650 ms; first-hold combo ceiling 2,300 ms; hold-follow-up 480 ms; individual charged actions may extend to their own `charge.maxMs` | `public/99lc/game-config.json` `input.doubleTapMs`/`holdMs`/`holdMaxMs`/`holdThenDoubleTapWindowMs`; `features/last-chances/gestures.ts` `LastChancesGestureRecognizer`; `features/last-chances/engine.ts` `beginProvisionalTapParry` |
| `mylorik` recognition delays | technique hold 650 ms; one-intent buffer 150 ms; continuation/digital-gate step 480 ms | `public/99lc/game-config.json` `input.mylorik`; `features/last-chances/control-schemes.ts` `MylorikControlRecognizer`; `features/last-chances/engine.ts` `updateKeyboardDualSenseTriggers` |
| DualSense input hysteresis / gates | activation 0.25; release 0.16; hysteresis 0.06; shallow/medium/deep/final gates 0.25/0.50/0.75/0.95; pocket arm 450 ms; telegraph repeat 900 ms | `public/99lc/game-config.json` `input.dualsense`; `features/last-chances/control-schemes.ts` `DualSenseControlRecognizer` |
| DualSense output caps | magnitude 0.70; effect duration 900 ms; repeated blocked cue 240 ms | `public/99lc/game-config.json` `input.dualsense.feedback`; `features/last-chances/feedback.ts` `DualSenseFeedbackController` |
| DualSense click/ramp/band profiles | each tuple is start–end / resistance / force / transition ms / effect ms / magnitude: click .18–.30/.24/.28/35/90/.22; ramp .25–.86/.30/.55/140/160/.27; light band .20–.38/.22/.25/30/80/.20; medium band .40–.62/.34/.42/35/95/.34; strong band .64–.84/.48/.58/40/110/.48 | `public/99lc/game-config.json` `input.dualsense.feedback.profiles`; `features/last-chances/feedback.ts` |
| DualSense gate/follow-up/blocked profiles | gate .50–.80/.52/.62/70/140/.32; follow-up .30–.56/.30/.36/45/140/.30; blocked .12–.34/.46/.42/45/160/.32 | `public/99lc/game-config.json` `input.dualsense.feedback.profiles`; `features/last-chances/feedback.ts` |
| DualSense impact/tension profiles | impact .16–.44/.38/.50/25/120/.55; tension .26–.82/.44/.58/100/160/.28; a combo node may override any safe profile field, and an armed pocket may add its partial `armedTriggerOverride` | `public/99lc/game-config.json` `input.dualsense.feedback.profiles` and `weapons[*].controls.*.dualsense.nodes[*]`; `features/last-chances/feedback.ts` |
| DualSense depth-tick rulers | Spear/v2 primary .85/.90; Spear/v2 secondary .85; Chain .35/.60/.85; Knife-spider .35/.60/.85/.90; Axe primary .90 and secondary .60/.85; Sword primary/secondary .60/.88. Claws, Katana and Fang deliberately have none. Each set accepts 1–8 strictly increasing marks, every mark at least .03 from a gameplay gate | `public/99lc/game-config.json` `weapons[*].controls.*.dualsense.haptics.depthTicks`; `features/last-chances/config.ts` `validateWeaponHaptics` |
| DualSense armed feedback limits | `armMs` and `telegraphPeriodMs` each accept 100–2,000 ms; telegraph/armed-cue patterns accept 1–8 pulses ending within 2,000 ms. Default invitation is two 35 ms pulses at delays 0/90 ms, magnitude .58; telegraph priority 15 sits between wriggle 10 and click 20 | `features/last-chances/config.ts` `validateDualSenseInput`/`validateFeedbackPattern`; `features/last-chances/engine.ts` `DEFAULT_ARMED_INVITATION`; `features/last-chances/feedback.ts` `TELEGRAPH_PRIORITY` |
| Axe DualSense max bands | `axe-max`: 1,650 ms, damage ×1.65, knockback ×1.80, duration ×1.30, stamina 3. `axe-leap-max`: 1,700 ms, range ×1.85, damage ×1.70, knockback ×1.75, duration ×1.35, stamina 3 | `public/99lc/game-config.json` `twohand-axe.attacks.doubleTapHold.charge` / `secondaryAttacks.holdThenDoubleTap.charge`; `features/last-chances/config.ts` `AXE_MAX_BAND`/`AXE_LEAP_MAX_BAND` |
| Nine-weapon catalog / Chance prices | Spear 3; Chain 2; Claws 2; capture-only Knife-spider 0; Axe 3; Katana 3; Меч наемника 2; Двуручное копьё v2 3. Only Claws, Katana, Axe, Chain and Меч наемника are actually purchasable; both spears and the fang are reachable only through the Builder or the shipped loadout, which is now Двуручное копьё v2 | `public/99lc/game-config.json` `weapons[*].chanceCost`/`loadout.primaryWeaponId`; `rooms[*].interaction` |
| Swarm creep presentation / release rate | swarm creeps render at one third of their former visual radius while retaining authored collision; after the initial burst, queued creeps spawn every 200 ms (three times the former 600 ms rate) | `public/99lc/game-config.json` swarm room `spawnIntervalMs`; `features/last-chances/engine.ts` `renderEnemy` |
| Knife-spider enemy durability | 48 HP (double the former 24) and 2 armor (formerly 0) | `public/99lc/game-config.json` enemy `spider-knife` `maxHp`/`armor` |
| Artifact effects | Талисман ясного разума reduces mental damage 25%; Якорь сознания reduces it 40%; Кровавый идол restores HP equal to 20% of actual damage dealt | `public/99lc/game-config.json` `artifacts`; `features/last-chances/engine.ts` `applyMentalDamage`/`applyLifesteal` |
| Outfit effects | Одежда ниндзя grants an empty-right-hand right-click dash of 165 units over 180 ms with a 900 ms cooldown; Броня рыцаря adds 14 armor and multiplies movement speed by 0.84 | `public/99lc/game-config.json` `outfits`; `features/last-chances/engine.ts` `tryNinjaDash`/`effectiveArmor`/`effectiveMoveSpeed` |
| Spear distance/charge sectors | damaging bodies must clear the authored strict inner radius (52/56/62 units on basics); ram 650/1,050/1,500 ms; release 650/1,125/1,650 ms; overhead spin 1,125/1,650 ms; charged-kick target distance is max(80, resolved knockback), reaching 167.4 on the late band | `public/99lc/game-config.json` `twohand-spear` colliders/charge/tuning; `features/last-chances/colliders.ts` inner exclusion; `features/last-chances/engine.ts` spear kick branch |
| Двуручное копьё v2 tap chain / «Прокол» | Охота, a second thrust, then a slash — all `cooldownMs` 0 at 2 stamina each, dispatched on the press edge rather than after the 260 ms double-tap window. «Прокол» keeps v1's 25 damage / 900 ms cooldown at 5 stamina and opens a fixed 2,000 ms window in which every tap re-fires it: the interception runs ahead of the cooldown and action lock and interrupts the live thrust, so mashing rate — bounded only by 5 stamina a throw — is the real ceiling. The window never extends, and «Прорыв» closes it | `public/99lc/game-config.json` `twohand-spear-v2.attacks.tap`/`tapCombo`/`attacks.doubleTap.tuning.mashWindowMs`; `features/last-chances/engine.ts` `press`/`morphIntoPierce`/`pierceMashActive` |
| Двуручное копьё v2 «Прорыв» | Morphs out of a live «Прокол» once the second press outlasts 200 ms (`breakthroughHoldMs`, deliberately below the global 650 ms `holdMs`). Runs while held to a 2,000 ms ceiling at 0.5× base speed rising to 1× over 250 ms and on to 2× over the next 1,750 ms (110 → 220 → 440 units/s), steers with the cursor, costs 1 stamina per 100 ms, re-hits a body every 380 ms up to 3 times, shoves bodies 46 units to the side they were passed on, and coasts to a stop over 300 ms on release, exhaustion or the cap | `public/99lc/game-config.json` `twohand-spear-v2.attacks.doubleTapHold.tuning`; `features/last-chances/engine.ts` `updateBreakthrough`/`startBreakthrough`/`updateBreakthroughInput`/`damageEnemy` breakthrough branch |
| Двуручное копьё v2 замах / «Акали» bands | Both keep the lance's 650/1,125/1,650 ms thresholds. Замах: «Заколоть» (×2.2 damage, ×0.14 range, radius 34, 70°, no shaft guard) / throw-and-stun / piercing wall-pin at 10/15/20 stamina. «Акали» gains a `spin-early` band at 650 ms carrying v1's wide rassekatel (×1.25 damage, ×1.28 range, 165°, single hit) ahead of the two overhead spins, at 15/20/25 stamina. The DualSense follow-up gate moves from `requiredChargeBandId: 'middle'` to `'early'` to match | `public/99lc/game-config.json` `twohand-spear-v2.attacks.hold`/`holdThenDoubleTap` charge bands and `controls.primary`; `features/last-chances/config.ts` `ATTACK_SET_CONTROL_SEEDS`; `features/last-chances/engine.ts` `performSpearReleaseV2`/`performSpearOverheadSpin` |
| Chain loss/recovery | bind and slow last 7,000 ms; chain stays unavailable until the target dies or a boss actually loses at least 18% max HP from one hit, which also stuns it for 700 ms | `public/99lc/game-config.json` `secondary-chain` resource/`chainBind`/tuning; `features/last-chances/engine.ts` chain resource branches |
| Claw parity/rhythm/dash | bleed every 2 damaging claw hits; alternating-hand window 360 ms; resulting microstun 90 ms; charged dash traversal deals 0 contact damage and resolves one 82-unit/110° endpoint scratch | `public/99lc/game-config.json` `either-claws.tuning`; `features/last-chances/engine.ts` `damageEnemy`/dash landing branch |
| Knife-spider durability | 72 HP; −2 per action and −1 per successful hit; charged throw consumes all remaining durability | `public/99lc/game-config.json` `secondary-spider-knife` resource/tuning/`consumeAllResource`; `features/last-chances/engine.ts` spider durability branches |
| Axe transition values | recovery-cancel tap ×1.35 damage; every hit pulls 16 world units; matching 160 px horizontal aim travel on ordinary Tap grants up to +25% damage; leap damage/stun occurs only in its 85-unit landing circle | `public/99lc/game-config.json` `twohand-axe.tuning` and `axeLeap.tuning`; `features/last-chances/engine.ts` `performAttack`/`updateActiveAreas`/landing branch |
| Katana dodge/cooldown flow | flurry treats dodge ≥ 0.25 as high dodge and misses each second hit; each skill authors its own other-skill refund; Flash resets only its own cooldown on kill | `public/99lc/game-config.json` `twohand-katana`; `features/last-chances/engine.ts` dash/`damageEnemy` cooldown branches |
| Меч наемника rhythm / stagger / execution | ideal rhythm starts at 500 ms and remains perfect through 600 ms; only a tap faster than 500 ms is a miss, a slower one is a harmless 'late'; 2,000 ms fatigue triggers after 3 total room misses or 2 consecutive misses and resets both counters, and multiplies this weapon's stamina cost ×10 instead of blocking any action; each Zornhaw still executes and advances 14.4 units at 144 units/s; empty off-hand multiplies all Sword damage ×1.5; Zornhaw staggers 500 ms; elite/boss targets become Unstoppable for 5,000 ms after 3,000 ms accumulated stagger; opening every 3 basic hits for 1,600 ms; primary Oberhaw 26 base damage, ×0.55 without opening or ×3.25 with opening, 2,800 ms cooldown, 48-unit collider width and 500 ms post-action recovery; legacy secondary Oberhaw cooldown 1,900 ms with the same 500 ms recovery; Unterhaw hold gate 1,000 ms, 900 ms claim window past that gate and independent cooldown ×3 (8,400/5,700 ms) | `public/99lc/game-config.json` `hybrid-sword`; `features/last-chances/engine.ts` `applySwordRhythm`/`applySwordStagger`/`damageEnemy`/`prepareSwordOberhau`/`expirePendingUnterhau`/`executePendingUnterhau` |
| Stamina pool / regeneration | maximum 100, eroded 0 per death in every tier; 5/s regained while any enemy is noticing/alerted/chasing/attacking, 25/s otherwise (the cleared-room loop always counts as out of combat) | `public/99lc/game-config.json` `player.baseStats.maxStamina`/`progression.tiers[*].erosion.maxStamina`/`stamina`; `features/last-chances/engine.ts` `updateStamina`/`combatPressurePerSecond` |
| Stamina action cost / refunds | 2 per action unless the action authors its own `staminaCost` — which a charge band may override per band, so a deeper wind-up can cost more — ×10 while that weapon is fatigued; +2 for continuing a chain inside the 900 ms combo window and a further +5 for acting with the hand that did not act last; the debit is never waived, so a chained tap nets 0 | `public/99lc/game-config.json` `stamina.attackCost`/`comboRestore`/`handAlternationRestore`/`comboWindowMs`, `hybrid-sword.tuning.fatigueStaminaMultiplier`; `features/last-chances/engine.ts` `staminaCostFor`/`settleStaminaForAttack` |
| Stamina equipment | Лёгкие бегуна ×1.25 maximum (2 Chances); Второе дыхание +10/s (3 Chances); Дыхательная упряжь ×2 restoration from every source (2 Chances) | `public/99lc/game-config.json` `artifacts`/`outfits`/`rooms` `merchant-crossing`/`chest-gallery`; `features/last-chances/engine.ts` `effectivePlayerStats`/`staminaRegenPerSecond`/`scaleStaminaGain` |
| Vital-atmosphere onsets | corner breath fog starts under 60% stamina and reaches full at 0, breath cycle 5,200 ms → 1,400 ms; purple mental vignette starts under 80% mental health and reaches full at 0 | `features/last-chances/vital-atmosphere.ts` `resolveLastChancesVitalAtmosphere` |
| Enemy idle scan / preferred attack-range ratio | Слуга 0.28 / 0.72; Стражник 0.20 / 0.78; Химера 0.34 / 0.68; Нож-паук 0.52 / 0.62; Тень Куратора 0.16 / 0.80 | `public/99lc/game-config.json` enemy `idleTurnRadiansPerSecond` / `preferredAttackRangeRatio`; `features/last-chances/engine.ts` `updateEnemies` |
| Attack target budget | every executor damages `pierce + 1` unique targets; `pierce: 0` means one target; repeat actions still obey their authored repeat count/interval | `features/last-chances/engine.ts` `performProjectile`/`performDash`/`startActiveArea` |
| Invulnerability / critical feedback | an authored invulnerability timer is raised to at least the resolved charged action duration; critical sweet spots and authored critical behaviors show the common cue for 650 ms unless overridden | `features/last-chances/engine.ts` `performAttack`/`damageEnemy`; `public/99lc/game-config.json` `invulnerabilityMs`/critical tuning |
| Authored attack/collider/spawn tuning | every basic-combo step, special, charge band, status, collider width/dead-zone/rotation/trace and named formation carries its complete numeric definition in the no-rebuild runtime JSON | `public/99lc/game-config.json` `weapons[*]` / `rooms[*].spawnLayouts`; `features/last-chances/colliders.ts` |

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
| Battleship combo-hit response window | 8 seconds only when the defending human can legally deploy a summon; otherwise 2 seconds after a hit that preserves the shooter's turn; 0 after a miss | `BattleshipService.ComboHitDelay`/`FastComboHitDelay`/`ApplyComboShotDelay`; `GameHub.RunBattleshipBotPump` |

## Achievement & loot-box rewards

Achievement progress targets and the complete 110-entry rule catalog are in [ACHIEVEMENTS.md](ACHIEVEMENTS.md). Reward values are centralized by rarity; the live catalog contains 11 Common, 27 Uncommon, 20 Rare, 36 Epic and 16 Legendary cards, totalling **9,033 ZBS + 68 boxes** (`AchievementClass.cs` `AchievementDefinition`/`AllAchievements`).

| Constant | Value | Anchor |
|---|---|---|
| Common achievement reward | 10 ZBS | AchievementClass.cs:65-76 |
| Uncommon achievement reward | 25 ZBS | AchievementClass.cs:65-76 |
| Rare achievement reward | 50 ZBS | AchievementClass.cs:65-76 |
| Epic achievement reward | 100 ZBS + 1 loot box | AchievementClass.cs:65-76 |
| Legendary achievement reward | 228 ZBS + 2 loot boxes | AchievementClass.cs:65-76 |
| Loot Common | 60%; 15–30 ZBS inclusive; random public character +1 roll-weight point | `QuestService.LootBoxOdds` |
| Loot Uncommon | 25%; 40–75 ZBS inclusive; random public character +2 roll-weight points | `QuestService.LootBoxOdds` |
| Loot Rare | 12%; 100–175 ZBS inclusive; random public character +3 roll-weight points | `QuestService.LootBoxOdds` |
| Loot Epic | 2.5%; 300–450 ZBS inclusive; Tier 1–2 character +5 roll-weight points and queued for next new game | `QuestService.LootBoxOdds` |
| Loot Legendary | 0.5%; 750 ZBS; Tier 1 character +10 roll-weight points and queued for next new game | `QuestService.LootBoxOdds` |
| Loot rarity RNG | `SecureRandom.Next(1,10000)` inclusive; exact cumulative cutoffs 50/300/1500/4000 | `QuestService.RollLootBoxTier` |
| Rare+ pity | after 9 consecutive below-Rare results, box 10 preserves natural Rare+ or upgrades Common/Uncommon to Rare; Rare+ resets counter | `QuestService.OpenLootBox`/`GenerateLootBox`/`NormalizeLootBoxPity` |

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
| Ктулху natural weight | 40 (Tier −1); bot carve-out keeps this range, while team/all-four-adept guards can remove it | `StartGameLogic.GetRangeFromTier`; `Cthulhu.CanNaturallyRoll` |
| Tier semantics | −1 secret-rollable, −2 transform-only. unknown_bug is additionally excluded from every public/draft/store/achievement surface and its roll ignores store/loot multipliers | `CharactersPull.cs` `GetRollableCharacters`; `StartGameLogic.cs` roll weighting; `UnknownBug` guards |
| unknown_bug all-rolled boost | ×100 natural weight (Tier −1 baseline 40 → 4000) once unknown_bug has naturally rolled at least once for every human participant; the private bit is persisted at natural assignment, forced/admin/test-preliminary selection does not count, bot seats are ignored and ordinary eligibility/no-repeat gates still apply; ambiguous legacy statistics do not backfill it | `DiscordAccountClass.HasNaturallyRolledUnknownBug`; `StartGameLogic.HandleCharacterRoll`; `WebGameService.CreateTestGame` |
| Tier pity | +3% per game without that tier; reset round 2 | StartGameLogic.cs:181-182, CIR:1317-1324 |
| Bot rules | tier 4 ×3; no tier <4 except Кира at ½ tier-1 range | StartGameLogic.cs:177-179 |
| Top Laner decay | ×1.0 → −0.2 per Top Laner rolled (floor 0) | StartGameLogic.cs:180, 172-177 |
| Exclusivity | LeCrisp ⊕ Толя and HardKitty ⊕ Эрен Йегер across natural, guaranteed, draft and admin-test roster assignments; single Tier-4 per game; no repeat of last character | StartGameLogic.cs:47-60; WebGameService.cs:298-316,490-494,1194-1213 |
| Naruto roster eligibility | FFA only; human original requires ≥2 strict bots, bot original requires ≥3 strict bots total; exactly 2 become clones | `StartGameLogic.cs` `CanNaturallyRollNaruto`; `Naruto.cs` `CanUseRoster`, `InitializeTeam` |

## Per-character numbers

| Character | Constant | Value | Anchor |
|---|---|---|---|
| Homelander | base stats / Tier | Int 5, Str 10, Speed 9, Psyche 1; Tier 5 | `characters.json` Homelander |
| Homelander | Праведность leader payout / rage trigger / cap | +5 Moral per turn at place 1; otherwise +20% rage on the leader; +20% per enemy attacking win; cap 100% | `Homelander.ApplyRighteousness`/`RecordAttackingWin`; `RagePerTrigger`/`MaximumRage` |
| Homelander | Праведность laser | next attack at 100% auto-wins and adds 2 guaranteed Drops; once per enemy | `Homelander.ArmLaser`/`ApplyLaserDrops` |
| Homelander | Башня Vought | unique enemy positive-income leader −1 regular; unique Homelander leader receives one extra copy of every positive score entry earned that turn | `Homelander.ApplyVoughtTower` |
| Homelander | Молоко | +1 regular after first attack on HardKitty | `Homelander.TryMilk` |
| Omni-man | base stats / Tier | Int 1, Str 10, Speed 10, Psyche 6; Tier 3 | `characters.json` Omni-man |
| Omni-man | Подумай, Марк! check / payout | target resists at Omni-man Int +2; first failure per enemy pays +3 bonus | `OmniMan.IntelligenceAdvantageToResist`/`IntelligenceBattlePoints`; `HandleIntelligenceWin` |
| Omni-man | invasion delay / deadline | triggers after 1 further full turn; scheduling disabled from turn 10, so turn 9 → end of turn 10 is the latest path | `OmniMan.EvaluateInvasion`/`TryTriggerInvasion` |
| Omni-man | Стражи Земли | one unique positive-income enemy maximum sleeps for 1 following turn; turns 1–9 only | `OmniMan.ApplyGuardiansOfTheGlobe` |
| Omni-man | Частица нашей силы | each incoming enemy attack grants its attacker +10 Skill | `OmniMan.SkillPerIncomingAttack`; `HandleEnemyAttack` |
| Ктулху | Морок bonus steal | victim −1 / Вестник +1 immediate bonus point; unknown_bug exempt | `Cthulhu.HandleResolvedFight` |
| Ктулху | Нечто isolated fight stats / Justice | 10 / 10 / 10 / 10; Justice 0 | `Cthulhu.ResolveNechtoAttacks` |
| Ктулху | Нечто victory reward | +2 regular points (then ×1/×2/×4 by round) | `Cthulhu.ResolveNechtoAttacks` |
| Ктулху | Космический ужас idle trigger | 2 complete consecutive rounds with no Нечто attack | `Cthulhu.HandleEndOfRound` |
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
| Sirinoks | Дракон | stats 10; +1 live Justice; bonus = Skill/10 − friends below | CP:6209-6244 |
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
| Кратос | Класс-мульт / event | ×2 base, ×4 in event; human-only; starts from an r10 fight loss or fatal Kira note with unused revive; exactly six Kratos actions r11-16. unknown_bug alone also retains its own action and defeats Kratos in any resolved mutual fight | `CharacterPassives.StartKratosEvent`/`CanKratosReturnFromKira`; `DoomsdayMachine.EnforceKratosEventActions`; `UnknownBug.Is` |
| Молодой Глеб | Спокойствие чай | cd 3; +1 regular, target skips | CP:1245-1257, 5992-6001 |
| Молодой Глеб | Мета | up to 3 targets/round; +1 bonus per hit | CP:4740-4780 |
| Сайтама | Лысина | +1000 Skill | CP:252-255 |
| Сайтама | Неприметность | serious = top-2 by Skill (recomputed each round); off at r10 | CP:284-293, 3933-3942 |
| Мадара | base / rarity | Int 7, Str 9, Speed 10, Psyche 9; Tier 5 | characters.json:1486-1521 |
| Мадара | Бог шиноби thresholds | >1 unique attacker: TooGOOD; >2: TooSTONK; >3: fight Skill = 100 | Madara.cs:55-110; CP:461-467,1019-1025 |
| Мадара | Второй метеорит | blocked attack: no −1 bonus; +2 regular | DM:519-544 |
| Мадара | Клоны Сусано | round 8; live strict-bot reaction delay 30 s; every strict bot with an ordinary prediction sheet fills exact Madara; strict-bot Наруто/Sakura/Итачи also attack at every level; +1 live Justice at >2 unique attackers; seal at all 5 unique + ≥5 losses | `Madara.RoundEightBotReactionDelaySeconds`; `Madara.ForceRoundEightBotPrediction`; `Madara.MustAcceptRoundEightBotChallenge`; `Madara.RefreshIncomingEffects` |
| Мадара | Вечное Цукуеми | arm at all 5 unique attackers in one turn or place 1 entering r10; authoritative r10 skips every ordinary player, while unknown_bug and reserved-Wake Gordon retain real actions; fooled-viewer bonus = max living score − viewer score + 1 (0 if sole winner) | `Madara.PrepareEternalTsukuyomiRound`; `Madara.GetIllusoryBonus`; `UnknownBug.Is` |
| Рик | Пушка | invention Int ≥ 30; +1 charge/lvl-up; fired round ×2 regular points | GR:1155-1165, CP:4042-4066 |
| Рик | Бобы | stack: −1 Str/Speed/Psyche, Int = base×stacks; ≤3 ingredients per lvl-up | CP:2100-2116, GR:1174-1202 |
| Рик | Огурчик | 2 pickle turns; +1 penalty turn if never attacked | CP:4069-4076 |
| Кира | Тетрадь | +2 regular per kill (+4 for L); 15% glass fizzle; Гений −1 Int per kill | CP:4079-4157 |
| Кира | Глаза | 25 Moral; not consumed on L/Монстр/Sakura/unknown_bug | `WebGameService.ShinigamiEyes`; CP:1143-1166 |
| Кира | L | +5 Moral per round avoiding L; arrest from round 8, true −500 through floor | CP:4160-4177,5351-5388 |
| Итачи | Вороны | −20% Speed per crow (both directions); lands on blocked/skipped attack targets too (m58) | CP:768-777, 1621-1630, 3406-3431 |
| Итачи | Изанаги | 2 uses | Itachi.cs:18 |
| Итачи | Цукуеми | charge 2; recharge from −2 (⚠ 4 rounds, m9); steals round earnings ×multiplier; **once per enemy per game** (caught list, refusal keeps the charge) | CP:3433-3464, 4824-4871 |
| Продавец | Впарить | +500 Skill, 4 rounds, cd 2 | CP:1608-1631 |
| Продавец | Закуп | level-up +10 | GR:1359-1363 |
| Продавец | Сделка | +1 base bonus & +3 Moral per deal; round-10 debt steal = actual tactic credits/2 exactly | CP:1903-1916, 2606-2633, 4822-4832 |
| Продавец | Куш | 33% → attacker steals 3 bonus | CP:2677-2688 |
| Dopa | Взгляд | +2 regular (+4 Фарм) +50 Skill per attack **aimed** between his two Макро participants; both directions → everything ×2 (points/Skill/achievement); a target's Block/Skip is irrelevant; cd 1 | CP:4835-4872 |
| Dopa | Законодатель меты (2nd level-up) | consumes the upgrade with no stat; Стомп +9 Str +99 Skill; Доминация +20 Skill/−1 bonus/33% −1 Psyche; Роум steal 1 bonus + 3 Moral | `GameReactions.cs:1075-1095`; `CharacterPassives.ApplyDopaChoice` |
| Dopa | Permaban | entering round 10 at place 1 → shared Тигр ban: Skip, stats 0 Int / 10 Str / 0 Psyche | `CharacterPassives.cs:6718-6723`; `Tigr.ApplyRoundTenBan` |
| Dopa | IQ display | Int 7/8/9/10 → IQ 200/209/218/228; below 7 subtract 1 IQ per Int | `CharacterClass.GetIntelligenceString` |
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
| TheBoys | Члены / 4th upgrade | +2 linked stat per lvl-up; focused x4 unlocks ultimate; the exact first four distributed upgrades resolve at most one combination | `GameReactions.GetLvlUp` / `ResolveTheBoysFourthUpgrade` |
| TheBoys | Комбинации | F2+K2: dangerous-enemy Justice transfer; F/K/MM no B: Butcher off + J floor 1; only B+K: accumulated Skill ×2; F/B/MM no K: +1 Int/Str/Psy; 1 each: +1 member level, no stats | `GameReactions.ResolveTheBoysFourthUpgrade`; `TheBoys.ApplyKillingCoupleJustice` |
| TheBoys | Зарплата комбинации TheBoys | active 1/1/1/1 combination + authoritative match win + actual place 1 → **+69 ZBS** (`От самого призедента`) | `TheBoys.GovernmentSalaryZbs`; `CheckIfReady.HandleLastRound` |
| TheBoys | Francie | orders r1/4/7, window 3, completion/expiry **±1 regular** through round multiplier; chem `level × (1 + harder-tier)` bonus per eligible win | `CharacterPassives` Francie cases |
| TheBoys | Butcher | 2 rotating sup marks + permanent heroes/Young Gleb/Challenger Gleb; hunt Skill +10/+20; +1/+2 regular per actual Drop; poker Skill/Harm ×`(1+n)` / exact SD ×`2(1+n)`; recursive any-pool breaks, 50 Drops/turn; all off after Нахер Бучера | `CharacterPassives` Butcher cases; `DoomsdayMachine` Butcher Harm path |
| TheBoys | Kimiko | x0 ignores 1 J and can disable; x1+ never disables, blocks Harm, steals `level+1` J (2→5) before defense; x4 pays same regular points and strips non-Block/non-Skip attack targets' passives pre-fight | `CharacterPassives` Kimiko cases; `TheBoys.DisablePassivesBeforeFights` |
| TheBoys | M.M. | ±1 team Psyche; calm immunity from x1; r8 kompromat +5 Moral each; predictions ×kompromat; x4 steals/blocks Moral | CP:3689-3732,7176-7308; GR:1021-1050; CIR:320-350 |
| TheBoys | Смертельный вирус | −3/+3 bonus per infected mark at game end; infected/source death does not cancel settlement; disabled under СуперМудень | `TheBoys.VirusPointsPerInfected`; `CheckIfReady.HandleLastRound` |
| TheBoys | СуперМудень | locks every non-Butcher card; exact ×2 Butcher Skill/Harm/drop payout; one extra Harm per applied pool break; cap 50 Drops/turn; score-0 stop | `TheBoys.LockNonButcherPassives`; `DoomsdayMachine` Butcher Harm path |
| Salldorum | Шэн/Очко/капсула/летописец | +1 charge/lvl-up, auto-spent by next attack; dash from either direction uses the target's exact cell, holds through the next action round and redirects only that target's existing primary action (adds 0 fights); Очко = +1 regular per incoming fight only from the nearest living non-team enemy below; capsule after 3 rounds = +2 bonus +5 Speed for next fight, one natural drink + at most one history-only second drink (matching history bypasses the natural wait); rewrite is usable through turn 9 and converts every chosen-round loss separately: +1 victory point at Salldorum's actual old multiplier, historical underdog Moral and any +4×class-multiplier Сильный win Skill, while every original positive-point recipient loses its own multiplied point; then +2 Psyche/+2 buffered J; ×3 Skill attacking or defending vs 3-rounds-ago win leader(s) | `Salldorum.cs` `RecordHistoricalLoss`/`ResolveShenDashes`/`ApplyShenPositionHolds`/`IsNearestLowerEnemy`/`TryDrinkTimeCapsule`/`RewriteHistory`; `GameReactions.cs` level-up handler; CP:481-483,1078-1084,1112-1116,1779-1807 |
| Геральт | Заказы | +1 contract/round; +20 Skill per contract fight; oils T1 −1 J / T2 +2 Str / T3 ×3 Skill | CP:5719-5732, 2204-2214, 1503-1535 |
| Геральт | Медитация | Lambert 10% once (skill 0 next round; m16); демандна экономика: advance +2 regular, смерть при Displeasure ≥ 11 (true −500 through floor) | CP:5007-5028; `WebGameService.DemandContractReward` |
| unknown_bug | Exploit | +1 pot when a copied source win defeats the current carrier or unknown_bug defeats the carrier as attacker/defender; unknown_bug directly attacking the carrier then closes globally and pays raw pot as regular × current round multiplier; full-screen commit alarm at post-multiplier >20 | `UnknownBug.RecordResolvedFight` / `TryCommitExploit`; `GameClass.RollExploit` / `CloseExploit` |
| Sakura | Одна из трех | solo-only complete top-3 cutoff; a fourth living tie suppresses; factual place but first-place rewards | `Sakura.HasUncontestedSoloTopThree`; `CheckIfReady.HandleLastRound` |
| DooM Guy | base / newcomer | Int 2, Str 5, Speed 5, Psyche 5, Tier 4; exact 30% protected roll while TotalPlays < 10 | characters.json:1383-1413, StartGameLogic.cs:201-233 |
| DooM Guy | stages / random mode | Rune r3, Shield r5, Mission r7, Gun r9; Let's Roll random pick pays +2 regular each stage | DoomGuy.cs:53-60, 170-175 |
| DooM Guy | Rune | Вознесение +8 Int, at most 8 × −1/loss; Маневры +5 Speed, at most 5 × −1/Harm; Истребление +1 all stats + max(0, 10−round) bonus; Glory kill neighbour Skill ×2 and win +1 all stats | DoomGuy.cs `ApplySelectedModule`/`ApplyFightModules`; CP:2661-2706; CharacterClass.cs:213-221 |
| DooM Guy | Shield | Щит-пила block penalty −3; Шоковый щит 1 player-confirmed forced skip; Адский блок +666 Skill once after 2 blocked attacks; Контр-атака next-turn fight Skill/Justice 0; Щит-акула block→1-turn Ничего не понимает stance | DM:309-314,772-800; CP:895-915,5521-5539; DoomGuy.cs `ApplyFightModules` |
| DooM Guy | Mission | 1 new nest/setup, overflow >3 → −20 bonus + clear; only attack-win nest kill +1 regular; every resolved fight +1 regular; flawless no-block mission +20 bonus; Ближник neighbour melee bonus ×2 (Кулаки +4, Glory total Skill ×3/+2 stats, Бензопила 2 picks) | DoomGuy.cs `SpawnDemonNest`/`ApplyFightModules`; CP:2694-2759,3682-3695 |
| DooM Guy | Gun | BFG 1 charge; primary + every wave Step-3 random auto-wins; Кулаки Str=0 and +2 regular/win; Бензопила 1 victim, up to 4 choices and 1 pick; Рельса 1 charge and whole selected side, Block/Skip bypass except Тигр ban; Приручить дракона = round-10 Дракон transform | DoomGuy.cs `ApplySelectedModule`/`CopyChainsawPassive`; DM:445-491,569-650,810-881; CP:2730-2759,5767-5797 |
| DooM Guy | module reward | place 4/3/2/1 ceiling = Rune/Shield/Mission/Gun; fallback downward; standard chance = 0 complete, 5% last, otherwise `5 + 75×(remaining−1)/(total−1)`; Приручить дракона excluded and guaranteed only after round-10 win over Sirinoks/Дракон | DoomGuy.cs `TryAwardModule`/`TryAwardDragonTaming`; CP:2469-2479; CheckIfReady.cs:758-768 |
| Эрен Йегер | base / rarity / exclusion | Злость(Int) 0, Str 4, Speed 4, Самоуверенность(Psyche) 10; Tier 6; cannot coexist with HardKitty through a normal roster assignment | characters.json:1416-1446; StartGameLogic.cs:47-60 |
| Эрен Йегер | Овца в загоне | forced place 6 through r8; scheduled +1 Int at starts r2-8 (**+7 max**), +1 after every loss; −2 after every win; opening r9 bonus = post-sort place | CP:248-255,2612-2618,5173-5187,6418-6422; `CheckIfReady.TickAsync`; `DoomsdayMachine.CalculateAllFights` |
| Эрен Йегер | Дрочун marks / cash-in | loss mark 1; attacking-Eren mark 2; cap 2; victory cashes target mark as 1/2 bonus | PassivesClass.cs:270-274, CP:480-483,2465-2486 |
| Эрен Йегер | Дрочун mutual attack | +2 regular once per mutual enemy per round | CP:2558-2569 |
| Эрен Йегер | Атакующий Титан | off-cooldown block removed; +5 each stat per fight for the turn; no incoming target → −2 Psyche; cooldown 1 full next turn | DM:282-294; CP:62-72,517-521,1119-1123,3739-3758 |
| Эрен Йегер | Titan audio roll | `use_most` 50%; files 1–3 split the other 50% uniformly | sound.ts:1002-1006 |
| Эрен Йегер | Rumbling gate / reach | round 10; only if no Monster exists, every acting bot below Eren (including place 6) must attack selectable Eren; fewer than 2 losses **during round 10 only**; kills projected places strictly between Eren and place 6 | `BotsBehavior.TryForceRoundTenBossAttack`; CP:2662-2667,3672-3718; ErenYeager.cs:38-53 |
| Наруто | base / rarity | Int 3, Str 3, Speed 4, Psyche 5; Tier 5 | characters.json:1449-1483 |
| Наруто | Гарем но джутсу | original-only Block replacement while ready; +1 regular and «Техника соблазнения!» per enemy converted into Skip (not per canceled queued fight); cooldown 2 full following turns after every use | `Naruto.cs` `ResolveHaremQueues`, `RewardHaremSkip`, `TryCancelHaremFights`; CP `HandleEndOfRound` |
| Наруто | Теневые / bot focus | 2 independent strict-bot clones; clones cannot Block and attack at every AI level (no legal target → Skip); sibling attacks illegal; living siblings are virtual L0/L1 action slots only for the original; target already queued by another living Naruto gets `+3` L1 interest or L2/L3 score; r10 settlement immediately after Rumbling; sibling prediction value 0; correct enemy predictions +1 projected once; clone score/death seats end at 0 / bottom two | `Naruto.cs:40-67`; `BotsBehavior.OtherNarutoTargets`; `GameReactions.cs:913-936` |
| Наруто | Расенган | 2 joint attackers: summed Justice, +2 Str each, «РАСЕНГАН!»; 3: summed Justice, +3 Int/Str/Speed/Psyche each, «РАСЕНШУРИКЕН!!!!!!!!111» | CP:85-121; `Naruto.cs` `SnapshotJustice`, `GetJointAttackers` |
| Наруто | Призыв | exactly 1 Naruto on target; prior-round loss to that target with target TooGOOD or TooSTONK → terminal auto-win, otherwise refusal only | `Naruto.cs` `IsSoloAttack`, `WonPoweredFightLastRound`; DM:880-899 |
| Гордон Фримен | base / rarity / copy exclusions | Int 7, Str 2, Speed 3, Psyche 9; Tier 5; all 4 passives non-Standalone; complete kit excluded from ARAM and Бензопила | `characters.json` Гордон Фримен; `CharactersPull.GetAramPassives`; CP:2790-2793 |
| Гордон Фримен | Монтировка | every 3rd resolved attack-or-defense fight is a terminal Gordon win; Gordon's submitted attacks calculate before incoming defences can spend the charge; no H.E.V. Justice-to-stat conversion | `GordonFreeman.BeginResolvedFight`; DM Gordon-first order/crowbar outcome override |
| Гордон Фримен | headcrab schedule / rescue | 2 unique eligible targets before r1 and at starts r4/r7/r10; maturity after 3 turns (after r3/r6/r9 for batches that can mature); resolved Gordon attack rescue +3 bonus | `GordonFreeman.PlantInitialHeadcrabs`/`PlantHeadcrabs`/`MatureHeadcrabs`/`RescueHeadcrab` |
| Гордон Фримен | zombie penalty | mature target loses all current persistent Int and persistent/one-fight Int stays capped at 0 for the game; only when all 5 other roster players are zombies, Gordon settled score = 0 and pending regular = 0; dead seats still count and any Краборак prevents the condition | `GordonFreeman.MatureHeadcrabs`/`ApplyAllZombiesPenalty`; `CharacterClass.IntelligenceCappedAtZero` |
| Гордон Фримен | Просыпайтесь, мистер Фримен | 1 use/game; any living non-Kratos-event Skip or active Глаза Итачи target (wake cancels that target); optional r9 reserve only while Вечное Цукуеми is already armed preserves Gordon's real r10 action | `GordonFreeman.CanWake`/`Wake`/`CancelItachiEyes`; `Madara.IsEternalTsukuyomiActive`/`PrepareEternalTsukuyomiRound` |
| Гордон Фримен | Halflife 3 | 1 announcement r3–r7 inclusive; attempt settlement `P^P` (3→27, 4→256); Подсчет disables it to ordinary ×1; success at `P ≥ 3`; first human success with transfers left offers one release/wait decision, then success auto-releases; Itachi steals transformed value, Octopus/Saitama ledgers retain ordinary value; failure timeout 20 s, bot auto-postpones; 3 flat-cost postponements at 1/2/3 points, unaffordable/next failure cancels | `GordonFreeman.AnnounceHalfLife3`/`PrepareHalfLifeSettlement`/`ProjectRegularSettlement`/`ResolveHalfLifeDecision` |
| Джон Сноу | base / rarity / copy exclusions | Int 1, Str 4, Speed 5, Psyche 8; Tier 5; all 6 passives non-Standalone; complete kit excluded from ARAM and Бензопила | `characters.json` Джон Сноу; `CharactersPull.GetAramPassives`; CP Бензопила choice filter |
| Джон Сноу | Тупой бастард / Король Сервера | Skill gains ×2; transformation at 228 effective Skill replaces the Bastard's list slot; toogood/toostronk wins +1/+2 Int until transformation; King bonus mutations ×2; score-sort floor at place 3 | `JonSnow.Initialize`/`TryBecomeKing`/`HandleResolvedFight`/`ApplyLeaderboardRules`; `InGameStatus.AddBonusPointsCore` |
| Джон Сноу | Я Джон Сноу | current Justice added to all 4 fight stats; toogood +1 Justice/stats, toostronk +2 Justice/stats | `JonSnow.ApplyBaseJustice`/`ApplyDifficultyJustice` |
| Джон Сноу | Еще один бастард | 2 lowest-Skill non-Jon players marked by the inverse of Сайтама's formula; first matching queued weak target redirects to Jon; redirected defence +1 Justice and +1 all stats | `JonSnow.RefreshWeakestPlayers`/`RedirectBastardAttacks`/`ApplyDifficultyJustice`; CP `Неприметность` |
| Джон Сноу | Черный Замок | starts at marked place 4; exact hold for action rounds 1–3 and 3 action rounds on every re-entry; +1 bonus per **other** same-side player's resolved win, never Jon's own win and none while at place 4; King turns an eligible award into 2 royal points and logs 2 phrases | `JonSnow.BlackCastlePlace`/`BlackCastleTurns`; `FinalizeInitialPositions`/`AwardBlackCastleLoyalty`/`FinalizePositionEffects` |
| Джон Сноу | Мой дозор окончен | 1 death overcome; current Int →0 and capped at 0 permanently; ordinary Gordon zombie flag applied; 2 trigger lines at 50% each | `JonSnow.TryEndWatch`; `CharacterClass.IntelligenceCappedAtZero` |

## Empire's Endgame executable-ledger defaults

These are the designer-accepted fallback values introduced by the 2026-07-21 executable
supersession in `EMPIRES-ENDGAME-DESIGN-REVIEW.md`. The JSON remains the editable scenario
source; the engine anchors below are the trusted consumers.

| System | Tunable | Accepted value | Anchor |
|---|---|---:|---|
| Card fallback — Clubs | all-city loyalty | upright `+1 + 1×level`; inverted `-1 - 1×level` | `public/empires-endgame/game-config.json` card faces; `EmpiresEndgameEngine.applyEffects` |
| Card fallback — Diamonds | gold multiplier | upright `1.05 + 0.02×level`; inverted `0.95 - 0.02×level` | `public/empires-endgame/game-config.json` card faces; `EmpiresEndgameEngine.applyEffects` |
| Card fallback — Hearts | population | upright `+25,000 + 25,000×level`; inverted negative of same | `public/empires-endgame/game-config.json` card faces; `EmpiresEndgameEngine.applyEffects` |
| Card fallback — Spades | knowledge | upright `+500 + 500×level`; inverted negative of same | `public/empires-endgame/game-config.json` card faces; `EmpiresEndgameEngine.applyEffects` |
| Card fallback — Joker | time | upright `+2 + 1×level` days; inverted negative of same | `public/empires-endgame/game-config.json` card faces; `EmpiresEndgameEngine.applyEffects` |
| Maria | encounter / player win / target wins | `0.33` / `0.55` / `2`; victory grants gunpowder knowledge | `tavern.maria`; `EmpiresEndgameEngine.settleTavernOutcome` |
| Mystic recruitment / Queen | recruit `500` gold; appease `1` upgrade point; pulse every `3` cons; return delay `3` cons | exact | `tavern.mystics`; `tavern.queen`; `mysticCards`; `EmpiresEndgameEngine.tickMysticCards` |
| Лист | upright gold+food / inverted population+loyalty | `×1.1`; `-1,000` and `-1` all cities | `mystic-list` faces |
| Лорик | maximum combat spirit | `+1` / `-1` | `mystic-lorik` faces |
| Анатолий | gold+food multiplier | `×1.9967893333333` / `×0.5` | `mystic-anatoliy` faces |
| Chess rules | board / draw / Anton | `8×8`; `100` plies; threefold repetition; Anton extra opportunity every `2` white turns | `chess.rules`; `chess.anton`; `chess/engine.ts` |
| Chess settlement | win / loss / draw / abort | `+2,500` gold, `+1,000` knowledge / `-1` all-city loyalty / `0` / `-1` all-city loyalty | `chess.settlement`; `EmpiresEndgameEngine.settleChessOutcome` |
| Capital Tetrakorarchos | cost / cooldown / effect | `1,000` gold / `2` cons / `+1` all-city loyalty, `+500` knowledge | `governance.capital.sites[capital-tetrakorarchos]`; `activateCapitalSite` |
| Capital Forum | cost / cooldown / effect | `750` gold + `50` carpentry / `2` cons / `+3` days, `+1` reputation | `governance.capital.sites[capital-forum]`; `activateCapitalSite` |
| Capital Coliseum | cost / cooldown | free / `1` con; launches Chess | `governance.capital.sites[capital-coliseum]`; `activateCapitalSite` |
| Capital Academy | cost / cooldown / effect | `1,500` gold + `500` iron + `25,000` white stone / `3` cons / `+1` max combat spirit | `governance.capital.sites[capital-military-academy]`; `activateCapitalSite` |
| Capital white-stone mine | cost / cooldown / effect | `500` gold / `1` con / `+50,000` white stone, `-1,000` population | `governance.capital.sites[capital-white-stone]`; `activateCapitalSite` |
| Academy building | delayed army / elite | `2` free units per war technology after `1` con; `militaryElite +1` | `building-military-academy` level effects |
| Foundry | discounts / instant cadence | `15%` resource, time and upkeep; one instant order every `2` cons | `building-foundry` level effects |
| Sea Port | expedition speed | `+10%` | `building-sea-port` level effects |
| Fair | exchange / external discount | `50%` exchange rate; `10%` external-trade discount | `building-fair` level effects |
| Printing | knowledge multiplier | `×1.1` | `tech-printing` effects |
| Technocracy | knowledge / loyalty / reputation | `×1.25`; `-1` all-city loyalty; `-1` reputation | `reform-technocracy` effects |
| Generals | max spirit / rally | `+1` / `1` | `tech-generals` effects; Generals rally engine action |
| Lumber carpentry | per-settlement level 1 / 2 / 3 | `50` / `100` / `200` carpentry | `building-lumber` production |
| Earthquake | charges | `1`; removes one scheduled TD enemy once | `gift-earthquake`; `EmpiresEndgameEngine.consumeEarthquakeChargeForWave` |
| Tailwind | city-transfer speed | `+50%` | `gift-tailwind`; external-economy transfer quote reader |
| Fish Currents | duration / food / terminal consequence | `5` settlements / `600,000` food each / `-1` loyalty in every city | `gift-fish-currents`; `EmpiresEndgameEngine.settleFishCurrents` |
| Meteor Iron | target reward | `+500,000` iron and one plague start | `gift-meteor-iron` effects |
| Desert Tsunami | per-empire multipliers | food `×1.25`, iron `×1.1`, gold `×0.9` | `gift-desert-tsunami` effects |
| Alchemy recipes | salvage / poison wall / clinical lattice | `+300` stone / `+500` stone / `+400` knowledge; clinical lattice requires Medicine + Printing | `alchemy.recipes`; `settleAlchemyOutcome` |
| Alchemy explosion | epidemic / delayed aftermath | severity `×1.5`; after `2` cons: `-10,000` population and `-1` loyalty | `alchemy.explosion`; delayed-aftermath engine handlers |
| Inventory equipment | cap / target / Trevor | `2` equipment items; `1` target unit each; Trevor adds `2` slots | `inventory.equipmentPacking`; `inventory.perstPacker`; `beginExpeditionPacking` |
| Expedition veteran | qualification / removal / later speed | health ≤ `50%`; removed at `2` wounds; later deployment `+10%` | `expeditions.veteran`; TD deployment builder |
| South zone reward | first victory | `+1,000` gold and `+500` knowledge | `expeditions.zones[zone-south-beyond-dunes]`; `settleExpeditionResult` |
| Regional TD grade 1 | east/west/south cost and four options | cost `15`; range `+90`, HP `+75`, interval `-4`, or impact `+1` | `td.gradeChoices`; `td.towers` regional grade-1 rows |
| Regional TD grade 4 | east/west/south cost and four options | cost `30`; impact `+3`, range `+100`, interval `-6`, or HP `+150` | `td.gradeChoices`; `td.towers` regional grade-4 rows |
| Smith production | five line shares | basic/polearm/armor/firearms/artillery each `0.2` of `smithCapacity` | `td.equipmentProductionLines`; `settleEquipmentProduction` |
| Accepted weapon profiles | Voulge / Lance / ice pick / lancet arrow / misericorde / Desmond fork | `3 chop,3 pierce,2 cut` / `6 pierce,2 impact` / `4 pierce,2 impact` / `3 pierce,1 cut` / `5 pierce,1 cut` / `4 pierce,2 impact` | `combat.equipment` matching IDs; `phase3b.spec.ts` |
| Accepted ranged/artillery profiles | ship cannon / hand bombard / arquebus | `7 impact,6 crushing` / `5 impact,3 piercing` / `6 piercing,2 impact` | `combat.equipment` matching IDs; `phase3b.spec.ts` |
| Accepted armor levels | mail progression / helms / breastplate / cuirass / brigandine / padded jack / shield | mail `1/2/3/4/5`; helms `1/2/2`; breastplate `3`; cuirass `4`; brigandine `4`; padded jack `2`; shield `2` | `combat.equipment` matching IDs; `phase3b.spec.ts` |

## Bot AI difficulty

Per-game `AiDifficulty` is 0/1/2/3 and defaults to **3** for Discord, web and simulation. The curated god-admin web lobby may set each explicitly added bot to Legacy/V2/V3 = **1/2/3** through `GamePlayerBridgeClass.AiDifficulty`; auto-filled empty seats retain `-1` and inherit the game default. `--ai-difficulty N` overrides the simulation field; `--ai-probe N [--ai-probe-char "Name"]` and `--ab-char` provide per-seat A/B measurement through the same override and `BotsBehavior.EffectiveDifficulty`.

**L2 and L3 share one hard ordinary-visibility boundary.** They may use the acting bot's own state, public leaderboard projection/markers, legal target menu, sanitized global logs, the acting player's current/last personal logs, public resolved fight outcomes and exact detail from fights in which that bot participated. They may not otherwise read an opponent's current Block/Skip/attack, real character, stats, passives, score, Justice or private histories. The explicit scripted exceptions are exact Madara prediction during round-8 Клоны Сусано, exact Monster/Eren target selection on round 10, and Naruto's live sibling-target focus. `BotInformation.CaptureVisibleRound` persists only ordinary player-visible evidence in `GamePlayerBridgeClass.AiKnowledge`. L3 differs from L2 in how it reasons over that evidence: longer horizons, confidence-weighted hypotheses, public roster/rule constraints and deterministic best-target selection. The full decision catalogue is [BOT-AI-DESIGNER-REVIEW.md](BOT-AI-DESIGNER-REVIEW.md).

| Constant / rule | Value | Meaning | Anchor |
|---|---:|---|---|
| `AiDifficulty` | **3** | default level; 0 random, 1 legacy, 2 fair strategic, 3 fair advanced inference; curated lobby picker maps Legacy/V2/V3 to per-seat 1/2/3 | `GameClass.AiDifficulty`; `GamePlayerBridgeClass.AiDifficulty`; `AdminLobbyService.StartGameAsync`; `BotsBehavior.EffectiveDifficulty` |
| `--ai-difficulty` / `--ai-probe` / `--ab-char` | 0-3 | whole-field override, one-seat probe and paired seeded A/B measurement | `SimulationRunner`; `BotGameFactory.CreatePlayers` |
| L0 action policy | uniform legal choice | random legal level-up and random target/Block slot; Naruto's two living illegal siblings remain virtual attack slots only for these odds; respects cannot-block, rejected targets and Макро's second action | `BotsBehavior.HandleBotAttackRandom`; `Naruto.GetBotActionTargetSlotCount` |
| L1 knowledge policy | legacy privileged | historical control path is intentionally frozen and is **not** covered by the L2/L3 fairness guarantee; simulation exact-prediction prefill remains L1-only | `BotsBehavior.HandleBotAttack`; `BotGameFactory.CreatePlayers` |
| `AiPlaystyle` | once/match | L2/L3 retain one coherent build/character plan; sim reports record it | `BotsBehavior.EnsureBotPlaystyle`; `GamePlayerBridgeClass.AiPlaystyle` |
| Bot action preparation | spend every point first | level-ups complete before Madara prediction, forced Skip, Kira action or attack/Block choice | `BotsBehavior.HandleBotBehavior` |
| round-10 scripted target | Monster; else lower-than-Eren | every level attacks selectable Monster first; only a roster without Monster lets bots below Eren (including place 6) attack Eren | `BotsBehavior.TryForceRoundTenBossAttack` |
| Naruto shared-target interest | `+3` | L1 classical interest and L2/L3 fair score when another living Naruto already queued the target | `BotsBehavior.OtherNarutoTargets` |
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
| exact prediction confidence | 100% | public Толя/Коммуникация, owner-only Сверхразум/Naruto reveal, or exact Madara for every strict bot with an ordinary prediction sheet during round-8 Клоны Сусано | `CharacterPassives.SeedFairExactPredictions`; `Madara.ForceRoundEightBotPrediction` |
| Monster hypothesis | abstain | the bot may infer `Монстр без имени`, but leaves the submitted prediction blank because it cannot score and can punish a guess | `CharacterPassives.RecordFairPredictionChoice` |
| L2 generic Block odds | 1/4 neutral | PreferBlock 1/2; PreferAttack 1/5; 0 Justice + best score <7 gives 1/2; top two + historical incoming ≥1.5 + best <10 gives 2/3 | `BotsBehavior.ShouldFairBotBlock` |
| L3 generic Block rules | deterministic | PreferBlock if best <15; top two with incoming ≥1.5 and best <12; bottom three with own Justice ≤1 and best <7; otherwise only if best <5 | `BotsBehavior.ShouldFairBotBlock` |
| maximum consecutive voluntary Blocks | **2** | AI levels 1–3 must attack on the next turn when a target exists; attack or forced Skip resets the seat counter; L0 remains unrestricted random | `MaximumConsecutiveBotBlocks`; `GamePlayerBridgeClass.ConsecutiveBotBlocks`; `BotsBehavior.CanVoluntarilyBlock` |
| round-10 generic economy | leader Block; others attack | Monster/Eren scripted targeting wins first; otherwise only the acting bot's public place is used and character-specific forced action plans win | `BotsBehavior.TryForceRoundTenBossAttack`; `BotsBehavior.ShouldFairBotBlock` |
| `SmartMoralWaitPlace3` / `Place4` / `Leader` | 8 / 13 / 8 Moral | score-conversion patience for L2/L3 | `BotsBehavior.HandleBotMoral` |
| `SmartPsycheFloor` | 4 | generic level-up raises Psyche once the best stat is at least 8; character plans override | `BotsBehavior.HandleLvlUpBot` |
| `SmartSellerMarkFloor` | 20 | makes an unmarked Seller target dominant while the mark is ready | `BotsBehavior.ApplyFairCharacterPreference` |
| Джон Сноу fair target weights | places 1–4 `+3`; current weakest mark `−4`; ForceAttack | keeps **Еще один бастард** armed while preferring non-weak upper/Castle targets | `BotsBehavior.ApplyFairCharacterPreference`/`GetFairBlockPlan` |
| persistent plans | random once | Dopa 4; Darksci 2; Глеб 2; TheBoys 9 (4 focused + 5 combinations); Goblins 4; Rick/Itachi/Kratos/Cats/Tolya/Monster/Support 2 each; other characters use Adaptive | `BotsBehavior.EnsureBotPlaystyle` |
| fair Kira confidence | L2 25%; L3 35-90%; reveal 100% | L2 uses public catalogue priors; L3 scores legal logs/class/place history; Shinigami Eyes reads an exact identity only after the target ID is in the owner's reveal list | `BotsBehavior.HandleFairBotKira`; `FairKiraGuessScore` |
