# Interaction Matrix — cross-character rules

> Hand-maintained; verified 2026-07-12. **When adding/changing a character, add or update its row in every applicable table** — the M10–M12 bugs happened precisely at holes in these matrices. ⚠ = known hole (see AUDIT-FINDINGS). `CP` = CharacterPassives.cs, `CIR` = CheckIfReady.cs, `DM` = DoomsdayMachine.cs.

## 1. Forced-fight sources × untargetable / no-fight states

Forced-fight sources: **Монстр** two-turn no-escape (`CP:1066-1073`; `CIR:1371-1393`), **Шэн** below-position pull (`CIR:1277-1308`), **Штормяк** taunt (`CIR:1320-1354`), **Aggress** self auto-attack (`CIR:1264-1275`), **Геральт** contract multi-fight injection (`DM:315-370`), DooM Guy **BFG** wave injection (`DM:855-881`) and **Рельса** one-side injection (`DM:445-491`).

| State ↓ / Source → | Монстр | Шэн | Штормяк | Aggress (self) | Геральт inject | BFG wave | Рельса |
|---|---|---|---|---|---|---|---|
| Dead player | ✓ excluded | ✓ excluded | ✓ excluded | ✓ targets exclude dead | n/a (dead don't fight) | ✓ excluded | ✓ excluded |
| Тигр round-10 ban | ✓ carve-out `CIR:1293` | ✓ carve-out (M11 fixed, `CIR:1213`) | ✓ carve-out (M11 fixed, `CIR:1247`) | n/a | targeting already blocked (`GR:702-707`) | not special-cased; ordinary block/skip still applies | ✓ excluded from fan-out `DM:459-470` |
| Огурчик Рик (pickle) | ✓ active pickle strips IsBlock/IsSkip and wins (`DM:267-284,535-548`; M18) | pulls him; pickle accepts and wins | can taunt him; he accepts and wins | n/a | injection works; pickle accepts and wins | injected normally; pickle accepts and wins | injected; pickle's authoritative defense still wins |
| Block | attack still marks before the no-fight gate; overridden on each of the next two turns | fight happens anyway (forced list bypasses block-skip `DM:431-445`) | taunt bypasses own block | Aggress can't block at all | blocked Геральт = no injection | block stops that branch; normal penalty applies | ignored for each Railgun fight `DM:569-573` |
| Skip (sleep/tilt/ban) | overridden (stripped) | fight happens anyway | fight happens anyway | Aggress can't skip | skipping Геральт = no injection | skip stops that branch | ignored except the excluded Тигр ban `DM:650` |
| Ziggurat lock | position only — fights unaffected | position only | position only | n/a | n/a | position only — wave follows current board order | position only — side is captured from current order |
| Premade Carry | n/a | n/a | n/a | n/a | anti-skip now exempts the round-10 Тигр ban (M10 fixed) | n/a | n/a |
| Эрен: Атакующий Титан | no block remains to strip; forced target still resolves with +5 stats | forced target resolves with +5 stats | taunt target resolves with +5 stats | n/a | injection resolves each fight with +5 stats | wave branches resolve with +5 stats | every injected fight resolves with the per-fight +5 boost |
| Наруто: active Гарем | if no-escape must choose a fallback, siblings are excluded; a queue reaching Harem is canceled in full | pull queue canceled in full | taunt queue canceled in full | self-auto queue canceled in full | contracts expand and are spent first, then the complete queue is canceled | BFG branches exist only after the preceding win; reaching Harem cancels that branch/current+remaining fights, not already resolved fights | if the expanded side contains Harem, the whole fan-out is canceled (`Naruto.cs` `ResolveHaremQueues`/`TryCancelHaremFights`; `CIR:1391-1443`; `DM:381-388`) |
| Мадара round 8 | targetable; own action is cleared after forced-action injection | targetable | targetable | own auto-action cleared | contract fights resolve normally | wave resolves normally | included if on the selected side | Correct locked predictions add another ordinary queued fight; Madara's action lock remains |
| Active Вечное Цукуеми round 10 | erased by total Skip | erased | erased | suppressed | no contract expansion | no wave can start | no fan-out | the authoritative queue is cleared before combat; only non-combat/post-round passive settlement remains (`Madara.PrepareEternalTsukuyomiRound`; `DM:251-405`) |
| Мадара sealed | all queued targets sanitized | all queued targets sanitized | all queued targets sanitized | cannot act | pre-fight targets sanitized | ✓ excluded | ✓ excluded `DM:464` | `Madara.SanitizeSealedActions` runs after forced injections; direct targeting says `Игрок запечатан` |

Bot and auto-move action selection finalizes an existing Skip both on entry and again after pending level-ups (`BotsBehavior.cs:84-128,3706-3715`). The second gate is required for Darksci's round-9 Дизмораль (M32): it cannot become an ordinary bot attack, while the forced-fight sources above still work because they inject their targets later in the readiness/fight pipeline.

Rumbling's round-10 bot march is an action-selection override, not a fight injection: every bot at an opening place strictly between Eren and place 6 attacks Eren before difficulty-specific AI runs. Dead, forced-skipping, round-8/sealed Madara and any other bot that cannot act retain that state; Dopa Macro submits Eren first and then its required second distinct target (`BotsBehavior.cs:73-105,3769-3801`). Eren can still block or activate an off-cooldown Attack Titan against those ordinary incoming fights.

## 2. Kill sources × immunities

Kill sources: Кира's Тетрадь (`CP:4441-4509`), Кира's L-arrest (self-kill, `CP:5233-5270`), Кратос event kills (`CP:1816-1837,2762-2775`), Монстр Пейзаж pawn deaths (`CP:4754-4791`), Геральт pitchfork displeasure (self, `CP:4918-4954`), Эрен's Rumbling (`CP:3664-3710`), Naruto's round-10 Теневые dispersal (`Naruto.cs` `SettleShadowClones`).

| Immunity ↓ / Source → | Тетрадь | Кратос kill | Пейзаж pawns | Rumbling | Теневые | Notes |
|---|---|---|---|---|---|---|
| Стая Гоблинов ("нельзя убить") | ✓ `CP:4473-4474` | ✓ `CP:1820-1821` (+arrest `CP:5248-5249`) | ✗ **die here — intended** (M12, ОК) | ✗ die `CP:3681-3686` | n/a: source targets only the two Naruto clone seats | design: GameDesign.txt:509; Rumbling says all strictly-between players |
| Глаз Шусуи (Итачи) | revives next round | revives next round | revives next round | revives next round | n/a: clones have fresh Naruto-only passives | one-time, any source (`CP:5944-5955`) |
| Боги мне не указ (Кратос) | ✓ revives +228 Skill | n/a | ✗ not covered (source ≠ "Kira") | ✗ not covered | n/a: clones have fresh Naruto-only passives | source-check is `== "Kira"` only (`CP:5958-5972`) |
| Воскрешенное тело (Мадара) | ✓ `CP:4473-4474` | ✓ `CP:1820-1821` | ✓ `CP:4764-4766` | ✓ `CP:3681-3686` | n/a: clones have fresh Naruto-only passives | unconditional external-kill immunity; sealing is an unable-to-act state, not death |
| Dead state effects | a note aimed at an already-dead target is cleared without a second kill/Монстр payout (m36) | — | — | already dead excluded | score is still transferred and clone is marked dispersed, but no second death/Монстр payout | auto-block/ready, 0 ZBS, no mastery, excluded from forced pools (`Naruto.cs` `SettleShadowClones`; `CP` Death Note guard) |

## 3. Position movers × position locks

Movers (end-of-round order): Тигр-топ swap → Portal-Gun swap → HardKitty forced last → place assignment → **Ziggurat restore** → Storm-bite restore/swap → Quality Drop → post-sort effects → dispersed Naruto clones forced to the bottom (`DM:1295-1499,1877-1879`). Mid-turn movers: AWDKA forced last (intended — M3, ОК) (`CIR:1112-1127`), HardKitty forced last (`CIR:1141-1151`), Шэн post-sort swap (`CP:6209-6234`).

| Lock ↓ / Mover → | Тигр-топ | Portal Gun | Quality Drop | Storm bite | Шэн | Овца forced-last | Теневые dispersal |
|---|---|---|---|---|---|---|---|
| Ziggurat (`IsInZiggurat`) | ✓ blocked `DM:1313` | ✓ blocked `DM:1338` | ✓ can't drop onto/out `DM:1458-1469` | ✓ blocked `DM:1413,1438` | ✓ blocked `CP:6220` | n/a: Eren passives are not Standalone and name-gated | only clone seats move; all four non-clones retain relative order but shift above them |
| HardKitty at place 6 | n/a | n/a | ✓ can't drop onto `DM:1465` | n/a | n/a | mutually exclusive in natural games `StartGameLogic.cs:273-278` | clone-bottom invariant wins; HardKitty becomes one of places 1–4 |
| Тигр ban (round 10) | ✓ swap suppressed `DM:1304-1306` | n/a | n/a | n/a | ✓ pull now respects the ban (M11 fixed, `CIR:1213`) | inactive after round 8 | clone-only move; banned Тигр remains above both clones |

## 4. Steal / copy / redirect chains

| Mechanic | Direction | Interacts with | Verified behavior |
|---|---|---|---|
| Еврей (`HandleJews`, `CP:6687-6765`) | steals fight win point | Октопус ink | ink debits the Jew instead of the attacker (`CP:6785-6802`); Napoleon & fellow Евреи immune victims |
| PointFunnel (Баг) | copies regular points | Еврей | funnel copies only `AddWinPoints` — Jew's stolen points not funneled |
| Цукуеми (Итачи) | copies round earnings, deducts at end | Октопус ink | victim pays once: the round-11 ink restore **skips** its debit for a victim under Цукуеми (Итачи deducts instead); both Итачи and Octopus still get their point (D11 fixed, `CP:4863-4880`) |
| Цукуеми (Итачи) → Мадара | copies ordinary round earnings | Воскрешенное тело | score theft works normally; Madara receives the supplied personal reaction, labeled `Бог шиноби` so the hidden passive name is not leaked (`CP:4474-4486`; `CharactersPhrases.cs:357-364`) |
| Октопус ink | fake-win now, restore at r11 | DeepList first-fight | suppressed until DeepList's scripted loss happens (`CP:6772-6779`) |
| Kimiko Живое Оружие | **drains** attacker Justice | Расенган snapshot | real Justice is drained after the fight and pays +1 regular per point, but every joint Naruto's fight Justice comes from the once-per-calculation pre-fight snapshot; the persistent drain remains (`Naruto.cs` `SnapshotJustice`; `CP:853-875,1084-1087`) |
| Близнец (Монстр) | **copies** the highest attacker Justice on block + equal total bonus | generic block Justice | attacker keeps Justice; Monster gets no normal +1; multiple attackers use max, not sum (`CP:978-1002`; `DM:564-568`) |
| Неприметность (Сайтама) | stages a defensive loss against a non-serious attacker | generic loser Justice | Saitama receives no next-round Justice for the staged loss; genuine losses still grant it (`CP:667-690`; `DM:1022-1023,1138-1140`) |
| Вампуризм | **copies** victim Justice (intended — D6) | Падальщик | +1 extra from the ignored point (`CP:1907-1911`) |
| Premade | **copies** Carry fight-moral (intended — D9) | — | `CP:2433-2436` |
| Кошачья засада (cats) | physically moves passives to enemy | Минька/Штормяк vs owner | transferred cat won't buff/taunt against Котики (`CP:3082-3085`, `CIR:1229`) |
| Ziggurat learn | copies a `Standalone` passive | everything | see §6 |
| Бензопила (DooM Guy) | replaces Gun with one victim passive | copied passive dispatch/state | offers the victim's first four non-admin passives; explicit first-charge priming exists for Portal Gun, Шэн, Изанаги and Глаза Итачи (`DoomGuy.cs:198-225`); ordinary name-gated/game-start-only passives retain their native gates |
| Щит-акула (DooM Guy) | temporarily adds Братишка's `Ничего не понимает` instead of resolving a submitted block | defensive passive dispatch/state | DooM Guy accepts incoming fights for that round; attackers use Int 0 and each unique attacker loses 1 persistent Int on first contact, using DooM Guy's own `SharkBoole` state; only the temporary passive is removed at round end (`DoomGuy.cs` `PrepareSharkShield`; `CP:545-555,3744-3757`) |
| Приручить дракона (DooM Guy) | permanently adds hidden `Дракон` and changes Geralt's monster classification to Драконы when the Gun module is selected | Sirinoks transformation + passive-name interactions | the shared round-10 `Дракон` dispatcher supplies stats/score; Geralt contracts/oils/threat text, DragonSlayer and the web dragon state all see DooM Guy as a dragon (`DoomGuy.cs` `ApplySelectedModule`; `CP:1269-1282,5767-5797`) |
| Eren passive copy | Eren's four passives are non-Standalone; Chainsaw may still offer one | DooM Guy Chainsaw | all four cases are `Name == "Эрен Йегер"` gated, so a Chainsaw copy is inert |
| Naruto passive copy | Naruto's four passives are non-Standalone; Chainsaw may still offer one | Ziggurat / DooM Guy Chainsaw / ARAM | Ziggurat cannot learn them and ARAM excludes the whole Naruto definition; all central/dispatch hooks require a real initialized `Name == "Наруто"`, so a Chainsaw copy is inert (`characters.json:1455-1479`; `CharactersPull.cs:60-72`; `Naruto.cs:31-46`; `CP:1084-1105,3721-3724`) |
| Теневые score transfer | drains two clone pots into original | Rumbling / Подсчет / predictions / delayed liabilities / Монстр | runs after Rumbling; each pot is committed score + pending regular at the clone's actual multiplier plus legal non-sibling prediction points. It is added directly to the original, not exposed as fresh round earnings, and only newly dead clones pay Монстр. Later Saitama/Octopus/Mitsuki/virus/Tsukuyomi debits follow the inherited pot through `ResolveScoreSuccessor`; new post-death rewards do not (`Naruto.cs` `ProjectClonePredictionPoints`, `SettleShadowClones`, `ResolveScoreSuccessor`; `InGameStatusClass.cs` transfer helpers) |
| Rick Most wanted | redirects random marks to Rick | Спартанец marks, L, Сверхразум, Комментатор, hunts, tea odds | `CP:160-170, 233-236, 3780-3783, 5175-5181, 3511-3515, 1088-1094, 5036-5038`; hunters follow portal swaps `CP:2134-2149` |
| Portal Gun swap | swaps positions + remaining attackers mid-round | Тetradь targets / Naruto siblings etc. | attacker lists are rewritten, then any newly produced Naruto-to-sibling targets are immediately removed (`CP:2235-2255`; `Naruto.cs` `SanitizeMutualTargets`) |

## 5. Moral / psyche / Harm interceptors (checked inside `AddMoral` / `MinusPsycheLog` / `LowerQualityResist`)

| Interceptor | Effect | Anchor |
|---|---|---|
| Булькает (Братишка) | zeroes all moral; blocks all skill gains | CC:1125-1130, 963, 1010 |
| Геральт (by Name) | gains no moral at all | CC:1132-1134 |
| BlockMoralGain (cancer, Оковы) | blocks positive moral | CC:1137-1141 |
| Привет со дна | ignores losses; any gain becomes +4 (`isMoralPoints` exempt) | CC:1143-1153 |
| Спокойствие | ignores moral losses; immune to MinusPsycheLog | CC:1156-1160, GamePlayerBridgeClass.cs:93 |
| M.M. IsCalm (first M.M. upgrade) | immune to MinusPsycheLog; disabled by СуперМудень | GamePlayerBridgeClass.cs:102-116; `GameReactions.cs:1028-1036` |
| Безумие | psyche bypasses the 0-floor (can go negative) | CC:1295, 1344 |
| Boole Family | immune to ordinary Harm; СуперМудень bypasses | CC:205-210 |
| Kimiko active | TheBoys immune to ordinary Harm; СуперМудень bypasses | CC:217-228 |
| Маневры (DooM Guy) | after otherwise successful Harm, −1 persistent Speed | CC:204-208 |
| Контр-атака (DooM Guy) | during a marked enemy's next turn, every fight with DooM Guy forces that enemy's fight Skill and Justice to 0 | DoomGuy.cs `ApplyFightModules`; marks `CP:813-830` |
| Испанец | ordinary Harm → +1 Moral instead; СуперМудень bypasses | CC:231-238 |
| Много выебывается | Harm from higher-skill enemy while #1 → self-Drop | CC:223-229 |
| Минька (winner) | deals no Harm and no fight-moral loss | DM:750, 805-806 |
| Let's Roll! (DooM Guy) | Moral is set to 0 and all later Moral mutations/conversions are rejected; predictions are cleared/disabled | DoomGuy.cs:113-126, CC:1136-1137, GameStateMapper.cs:121-122 |
| Воскрешенное тело (Мадара) | Skill/Moral/Psyche loss, negative stat mutations and predictions are rejected; ordinary Harm is rejected but СуперМудень bypasses it; Madara deals no Harm | Madara.cs:34-39; CC:205-210,781-846,911-1167,1239-1605; GamePlayerBridgeClass.cs:102-108; DM:831-935 |
| СуперМудень attacker | ignores every enemy Harm interceptor above; every applied Int/Str/Psyche break recursively queues Harm | CC:182-345; DM:928-984 |

## 6. Ziggurat-copyable inventory (`Standalone: true`)

Copy rule: random Standalone passive from the **last attacked** enemy, no duplicates, **«Еврей» excluded** (`CP:6167-6179`). Full behavior inventory in AUDIT-FINDINGS D10; headline rows:

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
| Naruto's four passives | excluded: all are `Standalone: false`; ARAM also excludes Naruto's entire passive set. A Chainsaw copy can be offered but remains inert because every path is name/initialized-trio gated (`characters.json:1455-1479`; `CharactersPull.cs:60-72`; `Naruto.cs:31-46`) |

## 7. Same-target stacking notes

- Two attackers on one defender resolve **sequentially in leaderboard order** — the first fight's ForOneFight effects are reset before the second (`DM:388-411`, ResetFight per fight).
- BFG branches are appended to that same sequential target queue. The primary random-stage win fans out to both neighbours; every wave fight also auto-wins if it reaches Step 3 random, while a decisive pre-random loss/block/skip ends the branch; a target is visited at most once (`DM:810-881`).
- Рельса spends its one charge when it expands a submitted attack. The selected target stays first; every other living enemy on the same leaderboard side is appended once, while teammates, dead/sealed players and the round-10 banned Тигр are excluded. Railgun-tagged fights bypass Block/Skip independently and still traverse the ordinary hook/reset/outcome pipeline (`DM:445-491,531-650`).
- DooM melee adjacency is evaluated for each resolved fight from current places. Ближник doubles only the module's bonus: Кулаки +2→+4, Glory's extra +1×Skill→+2×Skill (×3 total) and +1→+2 each stat, Бензопила one→two selections. These tags and amplifier details remain design-only (`DoomGuy.cs` `IsNearestEnemy`/`ApplyFightModules`; `CP:2692-2757`).
- Со-attack interactions verified: Еврей steal (needs the Jew to also attack the target), Сайтама deferral (needs a co-attacker), Наполеон joint-attack auto-win (needs the ally to attack the same target).
- Эрен mutual attack is direction-safe: only Eren's own attack branch awards +2 regular, and a per-round enemy list prevents contract/BFG repeats from paying twice (`CP:2556-2567`). An attacking enemy's hatred mark is upgraded to 2 before resolution; a later ordinary Eren loss never downgrades it to 1 (`CP:480-483,2470-2478`).
- Расенган counts distinct living, acting members of the initialized Naruto trio whose live queue contains that target: exactly two use summed snapshotted Justice and +2 Strength; all three use the three-way Justice sum and +3 to all four stats. The Justice snapshot is taken after Harem cancellation but before any fight, so earlier Justice gains/drains do not change a later joint attack that calculation. Each Naruto receives and resets its own ForOneFight overrides independently (`Naruto.cs` `SnapshotJustice`/`GetJointAttackers`; `CP:74-109,1084-1087`; `DM:381-388`).
- Призыв and Расенган are mutually exclusive for a target in the same calculation: exactly one queued Naruto gets the refusal/prior-round revenge check; two or three get the joint boost. A successful summon is terminal and bypasses defender Block/Skip plus the Pickle/Octopus/Izanagi outcome; it creates no Octopus ink entry and spends no Izanagi use (`Naruto.cs` `IsSoloAttack`, `WonPoweredFightLastRound`; `CP` attack-before hook; `DM` summon gates).
- Мадара round 8 deliberately keeps same-target duplicates: a normal/hidden/second/forced action and the correct-prediction clone each remain separate fights. Strict bots wait 30 seconds in live games, are forced to predict Madara exactly, then choose the ordinary action themselves; they are no longer precommitted to attack him (`Madara.ForceRoundEightBotPrediction`; `BotsBehavior.HandleBotBehavior`; `CheckIfReady.TickAsync`). Thresholds use **unique attackers**, while the sealing loss requirement counts resolved defense losses.
- Round-10 pre-settlement: Rumbling runs immediately after the fight loop and ranks projected post-multiplier ordinary score. Теневые then drains/kills the clones and transfers their projected pots; both precede every `HandleEndOfRound` passive, so Rumbling does not see the transferred score (`DM:1508-1510`; `Naruto.cs` `SettleShadowClones`).
- Round-10 settlement order (who claws back first): Rumbling → Теневые prediction projection/transfer/deaths → Пейзаж deaths & Saitama banking in round-10 `HandleEndOfRound`; Чернильная завеса restore and Ищет достойного (One Punch) at round-11 `HandleNextRound`; Запах мусора at round-11 after-sorting; then `HandleLastRound`: ordinary predictions (dispersed clones excluded; sibling guesses pay 0) → active M.M. ×компромат → active Francie virus → Цукуеми deduction → sort → AWDKA → Premade → Goblin Ziggurat enforced win (M1, `CheckIfReady.cs:524-540`) → Sakura. Delayed clone liabilities in those layers resolve against the original; the dead seats stay zero/bottom (`CheckIfReady.cs:307-405`; `Naruto.cs` `ResolveScoreSuccessor`; GAME-DESIGN §8E).

## 8. Achievement V2 interaction observations

These hooks are **observational**: they record an already-resolved interaction and do not add a win, death, Harm, position change or resource effect. All seven cards are secret until unlocked; exact copy/rewards are catalogued in [ACHIEVEMENTS.md](ACHIEVEMENTS.md) §4.

| Achievement | Required characters | Observation point | Account(s) that earn it |
|---|---|---|---|
| `x_spartan_dragon` Dragon Slayer | Загадочный Спартанец в маске × Sirinoks/Дракон | DragonSlayer armed in the round-10 before-fight hook, then the Spartan actually wins that fight (`CP:1256-1267`; `DoomsdayMachine.cs:1353-1358`) | Spartan only (`AchievementClass.cs:528-533`) |
| `x_kira_kratos` Gods Don’t Tell Me What to Do | Кира × Кратос | Kira's correct Тетрадь смерти kill reaches Kratos, then Боги мне не указ revives him (`CP:5860-5882`) | Kratos only (`AchievementClass.cs:536-537`) |
| `x_itachi_madara` Eyes Meet Eyes | Итачи × Мадара | round-8 correct locked prediction grants the extra Клоны Сусано attack (`CheckIfReady.cs:1403-1415`) | Itachi only (`AchievementClass.cs:539-541`) |
| `x_deeplist_weedwick` Pet Project | DeepList × Weedwick | final authoritative board has both alive at places 1–3 (`AchievementClass.cs:543-551`) | both accounts |
| `x_spartan_mylorik` Mutual Respect | Загадочный Спартанец в маске × mylorik | mutual-Psyche respect is recorded, then a later resolved fight is won by the Spartan (`CP:1272-1283`; `DoomsdayMachine.cs:1428-1439`) | Spartan only (`AchievementClass.cs:529-533`) |
| `x_boys_madara` Nothing Is Immune | TheBoys/СуперМудень × Мадара/Воскрешенное тело | an actual Super Harm application passes the resurrected-body immunity (`DoomsdayMachine.cs:1043-1055`) | TheBoys only (`AchievementClass.cs:554-556`) |
| `x_monster_witness` I Saw the Beast | any non-pawn attacker × Монстр без имени | round-10 attacker receives the non-pawn Пейзаж конца света payout (`CP:4704-4715`) | that attacker (`AchievementClass.cs:558-559`) |

Active Вечное Цукуеми is a global privacy exception: game-end evaluation returns before every non-Madara achievement, including these interactions, so the real authoritative result cannot contradict the viewer-specific ending (`AchievementClass.cs:426-439`).
