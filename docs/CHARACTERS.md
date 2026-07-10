# Character Reference — all mechanics as implemented

> Code-verified against the working tree of 2026-07-01 (v4.1.8). For each passive: the player-facing description is in `DataBase/characters.json`; here we document **what the code actually does**, with file:line anchors. `CP` = `Game/GameLogic/CharacterPassives.cs`. ⚠ marks divergences — details in [AUDIT-FINDINGS.md](AUDIT-FINDINGS.md).
>
> Hook names refer to the execution order table in [ARCHITECTURE.md](ARCHITECTURE.md) §3.

## Index

Batch-audited order: TheBoys, Стая Гоблинов, Рик Санчез, Сайтама, Мадара, Тигр, Итачи (recent reworks) · Кратос, Кира, Монстр без имени, Продавец, Dopa, Salldorum · Геральт, Котики, Toxic Mate, Napoleon, Таинственный Суппорт, Осьминожка · DeepList, mylorik, Глеб, LeCrisp, Толя, HardKitty · Sirinoks, Злой Школьник, AWDKA, Darksci, Братишка, Загадочный Спартанец · Вампур, Краборак, Weedwick, Молодой Глеб, Sakura, Баг, DooM Guy, Эрен Йегер

**L2/L3 bot pilots:** strict bots choose one `AiPlaystyle` once per match and retain it. Multi-plan characters: Dopa (four actual meta tactics), Darksci (Stable/Unstable), Глеб (Classic/Young), TheBoys (one of four member builds), Goblins (Horde/Army/Economy/Ziggurat), Rick (Portal/Beans), Itachi (Crows/Tsukuyomi), Kratos (GodHunter/Ragnarok), Cats (Ambush/Storm), Tolya (Count/Rammus), Monster (Twin/Apocalypse), Support (Carry/Stakes). Plans control builds, targets, defense and Moral policy; selection/persistence is `BotsBehavior.cs:107-209`, plan-specific decisions `:1260-1380, 1580-2300, 2700-3140, 3470-3670`.

Single-plan specialists also use their actual objective instead of generic win-chasing: Kira spends armed Eyes on a useful unrevealed non-L/non-Monster target; Saitama seeks solo Мишень wins; Seller spreads marks even though he expects to lose; Bug delays Exploit until round 10 while farming PointFunnel; Sakura attacks for top three unless that position is visibly threatened (`BotsBehavior.cs:1105-1133, 1690-1725, 2160-2180, 2248-2283, 3091-3130`). L1 retains its legacy bot behavior.

---

## TheBoys (Пацаны) — Tier 3, Int 3 / Str 5 / Speed 6 / Psyche 2

State: `Game/Characters/TheBoys.cs` (`FrancieClass`, `ButcherClass`, `KimikoClass`, `MMClass`; superhero list = Сайтама, Кратос, Загадочный Спартанец в маске, Кира). Per-player marks: `TheBoysSupMark`, `TheBoysVirus(+Source)`, `TheBoysMoralBlockedByMM`. UI serials for reveal/unlock animations.

- **Пацаны** — level-up gives **+2** to the chosen stat and levels the matching member: Int→Francie, Str→Butcher, Speed→Kimiko, Psyche→M.M. (`GameReactions.cs:907-991`). First upgrade appends the member's upgrade line to its description (reveal animation); 4th upgrade unlocks the ultimate (Visible=true + unlock animation).
- **Francie** — orders: first order at game start (`CP:283-300`), re-issued on rounds 4 and 7; window 3 rounds; completing = +1 bonus, expiring (checked on rounds 4/7/10) = −1 bonus (`CP:5528-5558`). Хим.оружие (level ≥1): on a win where TheBoys was **not** TooGood/TooStronk vs the victim, **+`chemLevel × difficulty`** bonus points, where difficulty = 1 + (enemy was TooGood for TheBoys ? 1 : 0) + (enemy TooStronk ? 1 : 0) — a harder enemy pays more (`CP:3224-3238`; m10 fixed).
- **Butcher** — sup marks assigned each round after sorting (`CP:5770-5804`): superheroes always marked; +2 rotating marks (prefer current Мишень class; never the Francie order target; none on round 1). Attacking a marked sup: +10 Skill (+20 under СуперМудень) on any resolved fight (win or loss, `CP:3259-3277`); **+1 bonus point (+2 SD) only if the attack Drops him** (`dropsAfter > dropsBefore` in the Harm path, `DoomsdayMachine.cs:880-888`; M7 fixed — the point is for a Drop "Скинуть", not any win). Poker multiplies fight skill: `SkillFightMultiplier = 1 + poker` (×2 under SD) (`CP:1491-1499`) and repeats Harm per poker (SD doubles). Under SD, each Harm that FULLY breaks the victim's Strength pool (underflow→reset→drop via `HandleDrop` — not a mere −1; Int/Psyche breaks don't count; place-6 victims register no drops) grants one more Harm, looping while new Str-drops land, safety 50 (`DoomsdayMachine.cs:839-868`, `CharacterClass.cs:299-323`).
- **Kimiko** — while active, TheBoys take **no Harm** (`CharacterClass.cs:202-210`). Defense: reduces attacker's Justice by RegenLevel for the fight (`CP:659-676`); +10 Skill on defense win, +20 on successful block (`CP:735-741`). Defensive loss disables her next round (`CP:893-900`); leveling her instantly re-enables (`GameReactions.cs:946-947`). **Живое Оружие** (x4): never disabled, steals attackers' *real* Justice after every defensive interaction regardless of outcome (`CP:744-755`).
- **M.M.** — team psyche: +1 if zero losses this round, −1 + психует if lost every fight (`CP:3321-3337`). Компромат: after a Psyche upgrade the next *actual* fight on attack files kompromat + a character hint (`CP:3285-3306`); round 8: if every kompromat target is correctly predicted, +5 Мораль each (`CP:3340-3353`); game end: prediction bonus ×kompromat count (`CheckIfReady.cs:310-338`). **Оковы Правосудия** (x4): steals all kompromat targets' current Мораль, permanently blocks their moral gains (`BlockMoralGain`), M.M. becomes calm = immune to psyche loss (`GameReactions.cs:965-986`, `GamePlayerBridgeClass.cs:97-100`).
- **Смертельный вирус** (Francie x4): next attack infects; spreads on any real fight between carrier and non-carrier (block/skip don't spread; Француз immune) (`CP:3233-3252, 2263-2288`); game end: −2 bonus per infected → +2 each to Francie (`CheckIfReady.cs:341-360`).
- **СуперМудень** (Butcher x4): disables Francie/Kimiko/M.M. cases, doubles Butcher bonuses.
- Bots/Discord/web: goblin-style member level-up labels, padlocked ultimates in SkillsPanel, sup/virus/calm badges (see theboys_update_commit).

## Стая Гоблинов — Tier 1, Int 0 / Str 0 / Speed 10 / Psyche 5

State: `GoblinSwarm.cs` — population 20; rates: Warrior 1/5, Worker 1/10, Hob 1/15; `ZigguratClass` (built positions, learned passives, locks).

- Population drives stats, re-applied at game start and every round end (`CP:269-280, 6022-6037`): Str = Hobs, Int = Hobs, Psyche = 5 + Hobs, +10% Skill per Warrior (delta-tracked). External Str/Int/Psyche changes on Goblins now **persist** across the round recompute (like Speed already did) — via `ApplyGoblinPopulationStats` (D7 fixed; was previously overwritten each round).
- **Тоннели Гоблинов** — defending with Speed ≥ attacker+2: 50% chance attacker can't win (`CP:641-652`).
- **Гоблины** — growth: +1+Hobs auto per round (`CP:6024-6026`); attack win: +2×(1+Hobs), +1 if enemy TooGood, +2 if TooStronk (`CP:2178-2189`). Any loss: −(10 + 0.5·R²/3)% ±5% per TooGood/TooStronk, min 1 goblin left, min 1 dead (`CP:2190-2200, 860-873`). Str=Int=Hobs, Psyche=5+Hobs recomputed from population each round via `ApplyGoblinPopulationStats`; external stat changes (e.g. Спартанец's −1 Сила) now **persist on top** of that recompute rather than being overwritten — Speed already did (D7 fixed).
- **Отличный рудник** — mines at leaderboard places 1, 2, 6. End of round on a mine: +Workers bonus points (in `HandleEndOfRound`, pre-sort position); attacking a player on a mine: +Workers bonus (`CP:1458-1471`).
- **Гоблины тупые, но не идиоты** (Ziggurat) — Block arms a build; resolved after sorting (`CP:6043-6113`): needs ≥1 of each goblin type **and score >3** (cost gate, intentionally absent from the player text — m11); costs −3 bonus + 1 permanent Worker deduction (−1 Трудяга, confirmed intended — m11); builds at current place (once per place, max 6); learns a random `Standalone: true` passive from the last attacked enemy (no duplicates; **«Еврей» excluded** so Goblins can't become a second Jew — D2). Standing on any built position: position locked for the round (immune to Тигр-топ/Portal-Gun/Шэн/Drops/Storm — enforced in `DoomsdayMachine.cs:1286-1356`) +1 next-round Justice +5 Мораль per round. **Round-10 Ziggurat at place 1 = a real enforced win** in `HandleLastRound` (bonus-point overtake mirroring Premade; M1 fixed).
- Kill-immune (Кира `CP:4686`, Кратос `CP:1660`) — **except** Монстр's "Пейзаж конца света", which **does** kill goblin pawns on round 10 (the one kill-source they aren't immune to; M12, intended). Custom level-ups (rates/festival, `GameReactions.cs:779-833`).

## Рик Санчез — Tier 5, Int 5 / Str 2 / Speed 5 / Psyche 4

State: `RickSanchez.cs` (beans, pickle, portal gun).

- **Гигантские бобы** — Int uncapped (`CharacterClass.cs:1206`). Each level-up: +BaseInt tracking, ingredients appear on up to 3 enemies without one (`GameReactions.cs:1141-1202`). Any win vs an ingredient carrier (attack `CP:2033-2049` or defense `CP:841-857`): +1 stack, −1 Str/Speed/Psyche, Int = BaseInt × stacks.
- **Most wanted** — no case of its own; every "random mark" passive force-targets Rick: Спартанец marks (`CP:118-128, 3780-3783`), Kira's L (`CP:233-236`), Сверхразум discovery (`CP:5175+`), Глеб tea odds (`CP:5036`), Weedwick's hunter senses Rick regardless of Justice (`CP:1088-1094`), and headhunters follow Rick through portal swaps (`CP:2069-2084`).
- **Огурчик Рик** — Block converts to pickle: 2 turns, not IsSkip (stays attackable), attackers **cannot win** (`DoomsdayMachine.cs:253-267`, `CP:598-604`). Player keeps control: skip button shown, may level up or fire the Portal Gun (choosing a target auto-confirms the skip) (`CP:5331-5351`, `GameReactions.cs:659-662`, `CheckIfReady.cs:1087-1090` excludes pickle from auto-move). If never attacked while pickled: +1 extra forced-skip penalty turn (`CP:3978-3985`).
- **Портальная пушка** — invented at Int ≥ 30 (checked at level-up and end of round), +1 charge per level-up once invented (`GameReactions.cs:1155-1165`, `CP:3952-3959`). With a charge, his attack ignores block/skip and can't lose (`CP:1328-1336`); on the win: charge spent, positions swap **mid-round** and remaining attackers are exchanged between Rick and the target (`CP:2052-2087`); at end of round the leaderboard swap is enforced (Ziggurat-protected; `DoomsdayMachine.cs:1331-1356`) and the round's regular points are **×2** ("две мульти-вселенные", disabled by Толя's Подсчет; `CP:3960-3975`).

## Сайтама — Tier 5, Int 1 / Str 10 / Speed 10 / Psyche 10

State: `Saitama.cs` — deferral ledger (round-multiplied), deferred moral, 2 "serious targets".

- **Лысина** — +1000 Skill at game start (`CP:210-213`).
- **Неприметность** — serious targets = top-2 enemies by `GetSkill()`, recomputed every round end (`CP:3933-3942`; round 1 uses game-start skills ⚠ effectively arbitrary). Rounds 1–9, defending vs a non-serious attacker: Saitama can't win; the foregone win point (round-multiplied) + underdog moral are **banked**, no upfront loss (`CP:567-596` — the fix from saitama_update). Attacking a target that a co-attacker also hit: his +1 is deferred to the recipient (−1 regular now, banked) along with moral (`CP:1998-2030`). Solo wins vs Мишень-class: см. На мели.
- **На мели** — win vs your Мишень-class target: +1 bonus; if nobody else attacked the victim: +1 regular and the fight is hidden from non-admin logs (`CP:1968-1995`).
- **Ищет достойного противника** — after round 10 (`RoundNo == 11`, `CP:4713-4762`): if Saitama's round-10 attack beat the then-#1, he reclaims the whole ledger (+bonus to himself, −banked amount from each recipient), restores deferred moral and force-converts it to score with a manual flush. On round 10 Неприметность is off (`CP:568, 2001`), so nothing new defers.

## Мадара — Tier 5, Int 7 / Str 9 / Speed 10 / Psyche 9

State and rules live in `Game/Characters/Madara.cs`; the four supplied player-facing descriptions are stored verbatim, followed by the empty hidden entry, in `characters.json:1449-1484`. `PassivesClass.Madara` tracks the unique attackers in the current/round-8 turn, fight count, round-8 results, sealing and the hidden ending (`PassivesClass.cs:169-173`).

- **Бог шиноби** — Madara's own TooGOOD is disabled until more than one unique enemy targets him in the turn; TooSTONK is disabled until more than two; more than three applies an exact 100-Skill `FightCharacter` override in every attack/defense fight (`Madara.cs:55-110`; `CalculateRounds.cs:228-344`; `CP:419-425,1019-1025`). Enemy TooGOOD/TooSTONK against Madara is unchanged.
- **Воскрешенное тело** — no level-up points, persistent Skill/Moral or predictions; no Skill target; every negative persistent or one-fight stat mutation is rejected; `MinusPsycheLog` and Harm do nothing (`CharacterClass.cs:198-203,781-846,911-1167,1239-1605`; `GamePlayerBridgeClass.cs:102-108`; `DoomsdayMachine.cs:1493-1510`). Madara deals no Harm when attacking (`DoomsdayMachine.cs:831-935`). He is immune to every external kill source: Death Note, Kratos, Rumbling and Monster pawn execution (`CP:1725-1734,3527-3541,4287-4300,4583-4594`).
- **Второй метеорит** — when Madara's attack is blocked, the ordinary −1 bonus penalty is replaced by +2 regular points (therefore multiplied at settlement); the supplied phrase is personal (`DoomsdayMachine.cs:519-544`; `CharactersPhrases.cs:1012-1021`).
- **Клоны Сусано** — on round 8 Madara is ready/locked and cannot submit an attack. Each enemy with a locked exact Madara prediction gets one additional queued attack on him; it is intentionally not deduplicated from a normal/hidden/second action, so every clone fight remains visible in the standard fight log. If more than two unique enemies target him, he immediately gains +1 live Justice (`CheckIfReady.cs:1376-1397`; `Madara.cs:64-110,197-217`). The supplied global challenge and theme are sent to every player at opening round 8; web playback is removed when the fights finish (`CP:4890-4920`; `DoomsdayMachine.cs:189-197,1251-1257`). Round 9 selects the supplied dialogue from round-8 unique attackers and resolved defense results. All five unique attackers plus at least five Madara losses seals him: he cannot act or be targeted and the hidden ending is cancelled (`Madara.cs:113-217`; `GameReactions.cs:677-702`).
- **Вечное Цукуеми** — hidden from every passive list, including Madara's. It arms when all five unique enemies target Madara in one turn, or Madara enters round 10 at place 1; sealing cancels it (`Madara.cs:47-110,189-194`; `CP:6126-6143`; `GameStateMapper.cs:980-993`). After authoritative round-10 settlement/rewards, each non-Madara viewer receives a final-only projection: that viewer is alive, place 1, won a fight, and has exactly the bonus needed to beat the best living score, sourced as `Вечное Цукуеми`. Madara alone sees the real winner and five skips; spectators receive no result data. The authoritative `GameClass`, account payouts and history are never mutated (`Madara.cs:219-259`; `GameStateMapper.cs:998-1123`; `GameUpdateMess.cs:163-188,1106-1112`). Because a shared artifact cannot represent six incompatible endings safely, activated games do not save a replay or generate an AI story (`CheckIfReady.cs:795-807`; `GameNotificationService.cs:78-88`).
- **Phrases/UI** — opening/first fight/second fight/first top-1/Itachi/meteor lines are the supplied text in `CharactersPhrases.cs:1012-1021`; first/top-1 counters are `Madara.cs:113-132` and `CP:6126-6143`. Itachi's correct round-8 prediction adds the supplied personal line (`CheckIfReady.cs:1382-1392`); an Itachi steal triggers Madara's supplied reaction (`CP:4385-4397`).

## Тигр — Tier 5, Int 1 / Str 9 / Speed 9 / Psyche 1

State: `Tigr.cs` (`TigrTopClass.TimeCount = 3`, three-zero series list).

- **Лучше с двумя, чем с адекватными** — end of each round: +3 bonus per unique player whose Int **or** Psyche equals Тигр's (`CP:3377-3393`). The loop counts Тигр himself, so he always gets a free +3 at the end of round 1 — **intended** (Тигр is a member of his own clan; M6, ОК).
- **3-0 обоссан** — per-enemy win series; 3 consecutive wins (loss resets, simultaneous win+loss at ≥2 keeps the series): +3 regular, +30 Skill, +3 Мораль; victim −1 Int −1 Psyche (MinusPsycheLog); once per enemy (`CP:3671-3739`).
- **Тигр топ, а ты холоп** — while `TimeCount > 0`, end-of-round swap into place 1 (Ziggurat-protected, consumes 1 count; suppressed on round 10 if banned) (`DoomsdayMachine.cs:1299-1327`). Random re-arm: on a `TigrTopWhen` round (1–8, 1–2 times) the counter resets to 3 (`CP:4860-4864`). Additionally fires **at game start**: `HandleEventsBeforeFirstRound` (`CP:170-183`) swaps him to place 1 and consumes a count from the initial 3 — an opening window (rounds 1–3), confirmed **intended** in addition to the random window (M5, ОК). Being #1 on rounds 2–9 (however achieved): +1 Psyche +3 Мораль (`CP:5822-5829`).
- **Стримснайпят и банят и банят и банят** — entering round 10: forced skip, stats set Psyche 0 / Int 0 / Str 10, "ЕБАННЫЕ БАНЫ" global (`CP:4842-4857`); can't be targeted (`GameReactions.cs:702-707`), Монстр's no-escape, Тигр-топ swap, Шэн pull and Штормяк taunt all respect the ban (`CheckIfReady.cs:1293/1213/1247`, `DoomsdayMachine.cs:1304-1306`; M11 fixed). AutoMoveTimes ≥ 9 winner is renamed "НейроБот" with a ban-specific allowance (`CheckIfReady.cs:514-516`).
- **Top Laner** — roll-time rarity decay only (`StartGameLogic.cs:157,172-177`).

## Итачи — Tier 5, Int 8 / Str 5 / Speed 1 / Psyche 9

State: `Itachi.cs` (crow counts per enemy, Izanagi 2 uses, Tsukuyomi charge/targets/stolen ledger).

- **Вороны** — any level-up arms a crow (`GameReactions.cs:1137-1138`); the next attack's fight (win or lose) places it (`CP:2898-2915`). Each crow on an enemy: −20% of their Speed (rounded up, floor 0) for fights in both directions (`CP:1339-1347, 607-615`).
- **Изанаги** — 2 uses; defending a lost fight auto-converts to a win (`DoomsdayMachine.cs:705-714`).
- **Аматерасу** — attacking an enemy **adjacent on the leaderboard** with less Speed than Итачи: they can't win (`CP:1350-1360`).
- **Глаза Итачи** (Цукуеми) — charge +1/round to 2 (`CP:4119-4125`); attacking with full charge marks the target (this round + next) (`CP:2919-2941`); each end-of-round the victim's earnings (regular×multiplier + bonus earned) are **copied** to Итачи as bonus points and recorded (`CP:4089-4117`); the victim only *loses* them at game end (`CheckIfReady.cs:363-379`). If the stolen earnings include a fake Осьминожка point, the ink restore skips its own debit so the victim pays it once — to Итачи (D11). Re-attacking the marked target cancels the effect. Recharge after use is 4 rounds (counter set to −2); the description's "2" is no bug — confirmed intended (m9, ОК).
- **Глаз Шусуи** — one-time self-resurrect at next round (`CP:5365-5374`).
- Web: only Tsukuyomi state is surfaced (`PlayerDto.TsukuyomiState`); crows/Izanagi are not shown to the owner (finding).

---

## Кратос — Tier 1, Int 0 / Str 9 / Speed 8 / Psyche 0

- **Клинки хаоса** — attacking a target also queues its leaderboard neighbors (place ±1) as extra fights (`GameReactions.cs:665-677`).
- **Похищение души** — `ClassSkillMultiplier = 2` from game start (`CP:73-75`): class skill perks pay double (Сильный +8/win, etc.). ×4 during the resurrection event (`CP:2446`).
- **Охота на богов** — vs his Мишень-class target: fight-skill ×2 (×4 during event) (`CP:1047-1056`), reset after each fight (`CP:2451-2453`).
- **Возвращение из мертвых** — losing any round-10 fight starts the event (`CP:2439-2447`): global warning, Kratos music (5 rounds), class mult ×4. Extra rounds 11–16: everyone else is forced to auto-block (`CheckIfReady.cs:1020-1026`), Kratos ignores blocks/skips (`CP:1038-1044`); every enemy he defeats **dies** (Goblins immune) and drops off (`CP:1655-1675`); a Kratos loss = his final death (`CP:2420-2436`); event ends at round ≥16 or when 5 players are dead (`CP:3360-3374`, `DoomsdayMachine.cs:1249-1255`; hard cap `RoundNo ≥ 20`).
- **Боги мне не указ** — one-time resurrection if killed by Kira, +228 Skill (`CP:5377-5386`).
- **God Of War** — hidden, flavor only: the "Zeus! Your son has returned" intro log (`CP:69-71`).

## Кира — Tier 1, Int 5+4 / Str 3 / Speed 6 / Psyche 4

- **Гений** — +4 Int at start (`CP:219-221`), −1 Int per Death-Note kill (`CP:4027`). Also strips character-revealing lines from Kira's own global-log view (`GameStateMapper.cs:1124`, `KiraHiddenLogSnippets`).
- **Тетрадь смерти** — replaces predictions (auto-confirmed, `CheckIfReady.cs:1339-1340`); one name per round via web/bot action; resolved at end of round (`CP:3988-4066`): 15% "писал на стекле" fizzle; correct → target dies (DeathSource "Kira", Goblins immune), Kira +2 regular (+4 if it was L), wrong → that target locked forever.
- **Глаза бога смерти** — activation costs 25 Мораль (`WebGameService.cs:747-764`; bots `BotsBehavior.cs:438`); next attack reveals the target's character; **not consumed** on L or Монстр (`CP:969-991`).
- **L** — random enemy (prefers humans; Most wanted forces Rick) (`CP:223-240`). +5 Мораль per round without fighting L (`CP:4069-4086`). From round 8: if L has predicted "Кира" on him, Kira is arrested — dies, −500 points (`CP:4670-4711`).
- End-game dialogue matrix in `HandlePostGameEvents` (`CheckIfReady.cs:187-262`).

## Монстр без имени — Tier 1, Int 8 / Str 1 / Speed 1 / Psyche 10

- **Монстр** — anyone he attacked can't block/skip next round (`CP:1651-1653` set, `CheckIfReady.cs:1266-1289` enforcement — random forced attack; round-10 Тигр-ban carve-out; cleared each round `CP:5522-5525`). +1 regular per any death (`CP:1670-1675, 4018-4023, 2430-2435`), +1 bonus per any Drop (`CharacterClass.cs:188-192`).
- **Близнец** — blocking steals the attacker's entire real Justice + bonus points equal to it (`CP:875-890`); attacking a stat-twin (any one equal stat, FightCharacter values): −1 Psyche (`CP:1473-1488`).
- **Выдуманный персонаж** — can't be predicted (`CheckIfReady.cs:301`, `GameStateMapper.cs:54`) or named by Глаза бога смерти. Entering round 9: everyone *Monster* guessed correctly becomes a Johan pawn; +3 bonus if anyone has a guess on Monster (`CP:5471-5498`).
- **Пейзаж конца света** — start of round 10: global warning, Тэнма's hint self-erases after ~10s (`CP:5501-5519`). End of round 10: pawns who didn't block/skip die (+1 regular each to Monster); non-pawns who attacked Monster that round get **+7 regular** (×4 round multiplier!) **+10 bonus** (`CP:4300-4333`).

## Продавец Сомнительных Тактик — Tier 2, all stats 0

- **Впарить говна** — attack "sells" a tactic (cd 2): target +500 Skill for 4 rounds (excluded from siphon), target's next attack force-loses, after which the target marks *the enemy they lost to* as their personal "outplay" (+1 bonus per win over outplay targets while marked; each such win also counts into the Seller debt ledger `SellerTacticBonusEarned`) (`CP:1362-1385, 1601-1608, 2314-2326`). Mark expiry removes the granted skill and harvests everything the target *gained* while marked into the Секретный билд box (`CP:5390-5419`).
- **Закуп** — level-up gives +10 instead of +1 (`GameReactions.cs:1007-1011`).
- **Выгодная сделка** — each win by any marked player (or by DeepList — "Сомнительная тактика" holder counts as a tactics user, `CP:2334`) = 1 deal: +5 Мораль immediately (`CP:2328-2340`) and +1 bonus per deal at end of round (`CP:4128-4138`). Round 10, Seller wins an attack: steals ⌈debt/2⌉ bonus from the victim's tactic earnings (`CP:1638-1649`).
- **Большой куш** — 10%: an attacker who beats the Seller steals 2 bonus (`CP:2343-2354`).
- **Секретный билд** — hidden: all skill siphoned from marked players lands on the Seller on round 10 (`CP:5421-5442`, siphon plumbing `CharacterClass.cs:87, 994-995, 1025-1026`).

## Dopa — Tier 1, Int 7 (показывается "200IQ") / Str 1 / Speed 3 / Psyche 6

- **Макро** — two actions per turn: two attacks, or block+attack (self-id placeholder in `WhoToAttackThisTurn`, `GameReactions.cs:326-349, 718-734`); the second fight is hidden from others' logs (`CP:1417-1421`); idle second action auto-moves (`CheckIfReady.cs:1101-1108`).
- **Пассивный импакт** — +1 bonus each round with ≥1 win (`CP:2090-2093, 4141-4148`).
- **Взгляд в будущее** — needs both targets: procs when one of the two targets actually attacked the other, +2 regular (+4 with Фарм) +50 Skill, cd 1 (`CP:4150-4175`).
- **Законодатель меты** — pick 1 of 4 at game start (web `WebGameService.cs:797-813`; bots random `CP:5461-5467`): Стомп +9 Str +99 Skill (then both passives removed, `CP:5728-5747`); Фарм doubles Взгляд; Доминация +20 Skill/win, victim −1 bonus, 33% −1 Psyche (`CP:2095-2104`); Роум wins vs non-adjacent: steal 1 bonus + 3 Мораль (`CP:2145-2159`).

## Salldorum — Tier 2, Int 6 / Str 1 / Speed 2 / Psyche 8

- **Шэн** — +1 charge per level-up (`GameReactions.cs:1168-1172`); activation (web) picks a leaderboard position: all players below it are forced to attack Salldorum (`CheckIfReady.cs:1184-1199`), and after sorting he *swaps into* that position (Ziggurat-protected) (`CP:6117-6142`). Combinable with block/skip/attack; deactivation refunds the charge (`WebGameService.cs:866-906`).
- **Очко** — +1 bonus when attacked from below (`CP:904-911`).
- **Временная капсула** — first Block buries the cola at the current position (`CheckIfReady.cs:1202-1214`); returning there ≥3 rounds later: +2 bonus + 5 Speed for one fight (pending → applied post-DeepCopy, `CP:5700-5720, 381-387, 948-954`); re-obtainable via history rewrite if he stood there that round (`WebGameService.cs:950-961`).
- **Великий летописец** — passive: ×3 fight skill vs whoever won most 3 rounds ago (ties → all of them) (`CP:2148-2176`); sees everyone's personal logs each round + 20% chance to corrupt a random line ("██…" or "Салдорум был здесь…") (`CP:4179-4211`); end-game "испорчено N записей" personal log (`CheckIfReady.cs:136-142`, now printing — C1). Active (once, before round 8): rewrite a past round — steals **1 × that round's multiplier** (×1 R≤4, ×2 R5-9) from each enemy who beat him then, **redirected to a Jew (Еврей) who co-won that round** if one pocketed the point (m15 fixed; best-effort — a Jew who attacked-and-lost-but-stole isn't reconstructable), +2 Psyche +2 Justice (`WebGameService.cs:908-976`).
- ⚠ Bot AI and the end-game corruption log check the dead Cyrillic name (finding C1).

## Геральт — Tier 5, Int 4 / Str 4 / Speed 5 / Psyche 10

- **Ведьмачьи заказы** — 4 of 5 enemies get monster types at start (fixed: Sirinoks→Драконы, Weedwick→Волколаки, Вампур→Вампиры, mylorik/Boole-family→Утопцы; rest balanced random) (`CP:311-370`); +1 random contract per round from round 2 (`CP:5582-5595`). Fighting a typed enemy consumes **all** contracts of their type and injects that many extra fights, both when Геральт attacks and when he's attacked (not while he blocks/skips) (`DoomsdayMachine.cs:293-348`); each contract fight +20 Skill (`CP:2204-2214, 913-939`); wins as attacker vs non-contract targets count as "Лут". Win-source strings "Контракт"/"Лут" feed the demand/invoice UI (`GeraltContractDemand`).
- **Медитация** — his skip becomes block (`DoomsdayMachine.cs:238-245`, `CheckIfReady.cs:1163-1168`); blocking applies oils, reveals one enemy's monster hint (AI-generated via Haiku for humans, static fallback; `CP:4337-4393`), **10%** one-time Lambert fumble (m16 fixed): loses **all** Skill for the next round (`CP:4395-4402, 688-693, 1590-1595`). Геральт gains no Мораль at all (`CharacterClass.cs:1132-1134`).
- **Масло** — level-ups craft oils (first gives tier 1 vs all types) (`GameReactions.cs:837-893`); active after a meditation, attack-only, vs the target's type: T1 −1 enemy Justice, T2 +2 Str, T3 ×3 Skill (`CP:1503-1535`).
- **Шевелись, Плотва** — first contract fight per round vs a higher-placed target: +Speed = place gap, +1 contract per typed player in between (`CP:1550-1587`).
- **Чеканная монета** (hidden demand economy — **web + bot only, no Discord UI**) — in the post-round ready phase the web demand panel (`PlayerCard.vue:1018-1047` → hub `DemandContractReward` → `WebGameService.cs:577-663`) offers two billings off an **invoice** of the previous round's per-target contract fights (`ContractDemandClass.CalculateInvoice`, `Geralt.cs:165-290` — scores wins/losses, target positions, Lambert/meditation penalties and reputation into a 0-2 **coin** / 0-3 **Displeasure** tier; snapshot at `CP:4429-4452`):
  - **За прошлый** (immediate) — pays the invoice's coins as regular points (+1 "Благодарность" when the tier is ≥ 2 coins), adds its Displeasure; "Выжил чудом" (losses > wins > 0) adds +1 coin and +3 Displeasure. Once per phase; needs a contract fought last round.
  - **За следующий** (advance) — +2 regular next round, resolved end-of-round (`CP:4454-4491`) with an invoice-based Displeasure swing (weak +5/+3/+2/+1, strong −1). Blocked at Displeasure ≥ 5.
  Bots auto-resolve both (`BotsBehavior.cs:154-182`). **Displeasure 0-11; ≥ 11 ⇒ death by pitchforks** (IsDead, −500 bonus "Вилы разъяренной толпы", global log), checked in both the web handler (`WebGameService.cs:652-660`) and the advance resolution (`CP:4483-4490`) (m20).
- Геральт at place 6 at game end = pitchfork **log line only** (`CheckIfReady.cs:270-276`, no death/points); the design note's "психует when a contract holder is killed" is **not implemented**. Both left as-is — m20 documents current behavior; wiring either up is a separate design decision.

## Котики — Tier 1, Int 3 / Str 2 / Speed 8 / Psyche 6

- **Минька** — +1 Мораль +10 Skill from every real fight, win or lose (`CP:3003-3019`); never deals Harm or fight-moral loss (`DoomsdayMachine.cs:750-806` isHarmless).
- **Штормяк** — blocking taunts a random enemy into attacking Котики as a forced extra action (once per unique enemy for the original; dead excluded; Котики immune to a *transferred* Storm's taunt) (`CheckIfReady.cs:1217-1251`); the taunt bypasses the block (fight happens, `DoomsdayMachine.cs:471-474`); if the taunted attacker loses: −1 Psyche and their top stat −1 → Котики +1 same stat (`CP:3023-3064`).
- **Кошачья засада** — attacking an enemy with no cat: deploys an off-cooldown cat 100% (50/50 if both ready); the cat's passive physically moves to the enemy (Storm also carries "Рандомное поведение") (`CP:3141-3195`). Re-attacking the carrier returns the cat (cd 2): Минька win → +2 bonus, +33 Skill × rounds she sat; Штормяк win → victim loses **half of the score they earned while the cat sat** (delta between the current score and a snapshot taken at deploy, `KotikiAmbush.StormScoreSnapshot`) + психует (`CP:3067-3138`).
- **lvl-мяк** — level-up gives only +1 live Justice (`GameReactions.cs:897-904`, keyed on Name).
- **Рандомное поведение** — hidden, rides with Storm: each round picks a trick (fight 3/7, bite 1/7, vase 3/7 once) (`CP:5599-5664`): *fight* — Storm jumps into a random fight, ±5 weighing; if that flips the result the +1 win point goes to the Storm carrier (`DoomsdayMachine.cs:350-386, 639-668, 770-773, 903-904`); *bite* — random non-#1 player is position-locked for a round; if still there next round: +10 bonus to the carrier (`CP:5622-5641, 6147-6170`, locks in `DoomsdayMachine.cs:1269-1283, 1403-1450`); *vase* (once per game) — a vase falls on a random player at end of round: catch chance = Skill/3 % (never with ≤20 Skill); caught → +1 bonus + permanent vase immunity, dropped → −1 bonus and the vase chain spreads to both leaderboard neighbors next round (`CP:5643-5659, 4559-4610`).

## Toxic Mate — Tier 3, Int 5 / Str 3 / Speed 7 / Psyche 3

- **Fuck this game, I'm done.** — −1000 Мораль at start (`CP:254-256`); floor-at-0 semantics turn this into a "never has moral" device (the deficit parks in MoralBonus and normalizes).
- **FF 20** — −20 bonus points at start (score floors at 0, so it erases early gains) (`CP:258-261`).
- **INT** — +1 regular per loss (`CP:2953-2965`); "Ok. I'm trolling." on first loss; **every win scores −1** (attack `DoomsdayMachine.cs:775-782`, defence `:905-908`; M4 fixed — HardKitty's "Никому не нужен" stays attack-only, "если напал и победил").
- **Get cancer** — first attack-win infects (victim can't gain Мораль) (`CP:2969-2990`); holder's attack-win passes it on (once/round); returning to Toxic Mate: +2×transfers bonus, then re-armable (`CP:2373-2414`).
- **Aggress** — can't block or skip, ever (button-blocked `GameReactions.cs:319-323`, force-cleared `DoomsdayMachine.cs:281-289`, `CP:1412-1419`, auto-attacks when idle `CheckIfReady.cs:1171-1182`); +1 regular +1 Justice when his attack fizzles on a block/skip (`CP:2992-3000`).
- **Tilted** — end of round: **+50 bonus only when the whole round had zero battles** (no player's `IsWonThisCalculation` is set — everyone skipped/blocked/no-showed; log jokes "+20") (`CP:4231-4239`). The old +1-per-*skip* bonus is removed (M8 fixed; JSON's "когда кто-то психует" is the designer's deliberately-vague voice).

## Napoleon Wonnafcuk — Tier 5 (TeamModeOnly), Int 7 / Str 3 / Speed 3 / Psyche 7

- **Вступить в союз** — first attack designates the ally (they're told); afterwards attacking a target the ally also attacks: Napoleon can't lose, +3 Мораль (`CP:1394-1409`). Ally targets shown via leaderboard ⚔ icon.
- **Завоеватель** — win vs an enemy placed strictly between Napoleon and his ally: +1 bonus (`CP:2122-2145`).
- **Мирный договор** — enemies who attack Napoleon's (or his ally's) block sign a treaty (`CP:713-720, 759-767`); their next attack on Napoleon/ally auto-fails, one-shot per treaty (`CP:620-628, 696-705`).
- **Меня надо знать в лицо** — each enemy's first attack on Napoleon auto-fails (`CP:630-639`).

## Таинственный Суппорт — Tier 5 (TeamModeOnly), Int 6 / Str 3 / Speed 3 / Psyche 8

- **Premade** — first attack marks the Carry; Carry's wins/losses = ±1 regular for the Support, and the Carry's fight-moral is **copied** to the Support ("передается" reads like a transfer, but it's a copy — the Carry keeps theirs; D9 confirmed intended) (`CP:1421-1428, 2356-2371`). Carry can't (involuntarily) skip: forced skips are cleared (`CP:5689-5704`) **except the round-10 Тигр ban** ("кроме банов"; M10 fixed).
- **Buffing** — attacking the Carry instead raises their lowest stat +2, silently (`CP:1431-1452`).
- **Stakes!** — every 3rd round (RoundNo % 3 == 0), attack-win vs a non-Carry: +1 regular (`CP:2944-2953`).
- **Protect** — blocking: +1 next-round Justice (`CP:4217-4224`). End-game: both in top-2 with Carry above → Support gets carry−support+1 bonus and takes 1st (enforced, `CheckIfReady.cs:472-494`).

## Осьминожка — Tier 4, Int 2 / Str 2 / Speed 8 / Psyche 10

- **Раскинуть щупальца** — +1 regular per new leaderboard place visited (rounds 2+) (`CP:5866-5876`).
- **Чернильная завеса** — defensive would-be-wins flip into attacker wins; the ledger records ±(round value): attacker (or the Еврей who stole the point) −1×mult, Octopus +1×mult (`DoomsdayMachine.cs:700-703`, `CP:6673-6741`). DeepList's first-fight-loss phase is exempt. Round 11: everyone's real score restored via bonus points — **but a victim's debit is skipped if they're under an Итачи Цукуеми** (D11: that point is repaid once, to Итачи; Octopus still keeps its own +N credit, so both receivers get it) (`CP:4765-4796`).
- **Неуязвимость** — attackers' Strength is 0 for the fight (`CP:423-425`); each *attack* Octopus loses: +1 counter → +1 bonus each at round 11 (`CP:1848-1850, 4777`).
- **Привет со дна** — Мораль interceptor: any non-button moral change becomes exactly +4, losses ignored (`CharacterClass.cs:1143-1153`); +1 bonus per block/skip **event** in the game each round (`CP:3605-3609`) — skips are counted per skipped *fight* (two attackers into one skipper = 2) plus one per blocking player; per-event is intended (m18, verdict ОК).

---

## DeepList — Tier 6, Int 10 / Str 5 / Speed 7 / Psyche 0

- **Сомнительная тактика** — must lose the first fight vs each unique enemy (attack `CP:1030-1036` and defense `CP:416-421` both set `IsAbleToWin=false`); the loss registers them as "known" (`CP:2466-2475`); afterwards +1 regular per win over them (`CP:2477+`). Suppresses Octopus ink and LeCrisp assassins while unmet (`CP:6678-6685, 489-494`).
- **Безумие** — 2(–3) scheduled rounds in 4–7 (`PassivesClass.cs:21`): all four stats random 0–10, skill multipliers ×4 ("квадра", `SetAnySkillMultiplier(3)`) (`CP:5217-5280`); restored next round with delta math; the Безумие flag makes `AddPsyche` skip the 0-floor — psyche can go negative afterwards ("энтропия") (`CharacterClass.cs:1295, 1344`).
- **Сверхразум** — 1(–3) scheduled discoveries in rounds 1–5: learns a random unknown enemy (Most-wanted Rick first) and auto-fills the prediction (`CP:5167-5213`).
- **Стёб** — second win vs a unique enemy: victim −1 Psyche (−2 for Злой Школьник; LeCrisp immune by name), +1 regular to DeepList; victims below 4 Psyche with Justice also lose 1 next-round Justice (LeCrisp immune) (`CP:2487-2542`).
- **DeepList Pet** — with Weedwick in game: both +4 Psyche at start (`CP:86-103`); the pair cannot attack each other (`GameReactions.cs:686-698`).

## mylorik — Tier 6, Int 4 / Str 8 / Speed 9 / Psyche 3

- **Месть** — first loss to each enemy registers the grudge; later beating them (not the same round): +2 regular, +3 Мораль, +1 Psyche, once per enemy (`CP:2544-2572`).
- **Спарта** — the Block button is refused ("Спартанцы не капитулируют!", `GameReactions.cs:312-317`). Attack losses stack per enemy; his next attacks on them use fight-skill ×2/×4/×8/×16 by stack (reset on win) (`CP:1263-1306, 1941-1961`).
- **Буль** — psyche <7: skip chance 1/(10+5×psyche) per round (`CP:4792-4805`); at psyche 0: +2 Str +22 Skill until psyche rises (`CP:4808-4821`).
- **Испанец** — on a loss: 50% (guaranteed every 2nd) +10 Skill −1 Psyche (`CP:2575-2599`); any Вред on mylorik instead gives him +1 Мораль (`CharacterClass.cs:213-221`).
- **Повторяет за myloran** — Int starts at 0 (`CP:82-84`); round 5: +4 Int; round 10: +228 Skill (`CP:4825-4838`).
- **Тупорылая Акула** — at psyche 10 (humans only): transforms into Братишка (name + passives swap properly) (`CP:6003-6010`).
- **Искусство** — hidden, flavor only: the "Какая честь - умереть на поле боя" intro log (`CP:77-80`; shared with Спартанец).

## Глеб — Tier 6, Int 7 / Str 6 / Speed 1 / Psyche 8

- **Спящее хуйло** — 2(+up to 2) scheduled sleep rounds in 1–9: forced skip, −30 Skill, sleepy avatar (`CP:5055-5081`); wakes if he chose an attack (the sleep clears, 33% "POSTAV ROLI" joke) (`CP:2603-2616`).
- **Претендент русского сервера** — scheduled as many times as sleeps (rounds 3–10, never on a sleep round); extra round-10 pity roll when below place 2 (chance 1/(40−place×4)) (`CP:5091-5134`): stats →9, +99 Skill, Мишень gains ×3; that round's regular points ×3; everything restored next round (−99 Skill) (`CP:3422-3453`).
- **Я щас приду** — 1-in-9 chance per defense to no-show (fight cancelled, attacker remembered) (`CP:458-483`); if the remembered attacker later fights him for real: +9 Мораль to them, once each (`CP:793-805`).
- **Я за чаем** — readiness rolls 1/8 per round (1/7 on challenger rounds, 1/4 with Rick in game, guaranteed by round 9) (`CP:5027-5053`); spending it on an attack: +1 regular, the target force-skips next round (`CP:1166-1177`) — unless they hold a charged Portal Gun (`CP:5670-5681`).
- **Yong Gleb** — round-1 button/web action replaces passives/stats/avatar with Молодой Глеб (+30 Skill consolation, sleep cleared); **keeps `Name == "Глеб"` by design** (prediction, bot AI, Geralt flavor and the roll/predict lists all key on it). The young form is identified by its `Main Ирелия` passive — the level-up caption (`GameUpdateMess.cs:1214`), AWDKA-troll line (`CheckIfReady.cs:427`) and the sleeping-Gleb psyche-10 phrase (`GameReactions.cs:1125`) now key on that passive, not the Name (m3 fixed).

## LeCrisp — Tier 6, Int 5 / Str 4 / Speed 5 / Psyche 8

- **Еврей** — when anyone else wins a fight vs a target LeCrisp also attacked this turn: the winner's +1 is suppressed and LeCrisp takes it (+1 regular) (`HandleJews`, `CP:6594-6672`); fellow "Еврей"/Napoleon holders are immune victims. Exclusive with Толя at roll time (`StartGameLogic.cs:180-194`). Moral→skill exchange gains the 7→+40 tier.
- **Булинг** — implemented by name checks: DeepList's Стёб doesn't take his psyche/justice; he never steals from DeepList (D4).
- **Гребанные ассассины** — attacker with Str ≥ his+3 (and not blocked/skipped, and not a first-fight DeepList): LeCrisp auto-loses (`CP:485-504`); every attacker who *wasn't* such an assassin gives +1 Psyche **for the next round only** (buffered, previous bonus removed each round) (`CP:776-784, 3471-3481`).
- **Импакт** — rounds without a defensive loss: ImpactTimes++, +1 bonus, +1 next-round Justice (`CP:3483-3499`, loss detection `CP:807-815`); wins: +（ImpactTimes+1) Мораль (`CP:2619-2628`). Also fixes his Speed-quality: resist 6 / kite 2 (`CharacterClass.cs:396-399, 434-442`).

## Толя — Tier 6, Int 8 / Str 6 / Speed 2 / Psyche 2

- **Еврей** — same as LeCrisp's (shared passive); the web PROFIT/Jew widget is LeCrisp-only, so Толя no longer shows a dead LeCrisp-state widget (M2 fixed).
- **Раммус мейн** — attackers into his block auto-lose (armor-break so the fight still happens) (`CP:506-515`); end of round: +1 next-round Justice + Мораль = attackers² (`CP:3565-3602`); his block-win doesn't reset Justice (`DoomsdayMachine.cs:938-940`).
- **Подсчет** — attack marks the target (initial cd 2–3; after use cd 4–5 — description's "2–3" counts from the **end** of the effect, so no bug; m8, ОК): next round each of the target's losses pays Толя +2 regular +2 next-round Justice (`CP:2298-2312`), and the target's round multiplier is forced to ×1 (`InGameStatusClass.cs:257-278`, also disables Rick's portal ×2).
- **Великий Комментатор** — rounds 3–6, 20%/round (max 2): leaks a random player's character to the global log and auto-fills everyone's predictions (Монстр immune; Rick prioritized) (`CP:3501-3563`).

## HardKitty — Tier 6, Int 3 / Str 5 / Speed 5 / Psyche 7

- **Одиночество** — starts at −30 **score** (the log's "−30 Морали" is an intentional text joke; m13, ОК) (`CP:159-168`); every attack on him: +1 regular (per fight, no round cap) + letter bookkeeping 1/2/4 by round for the endgame "вы принесли ему N очков" reveal (`CP:517-553`).
- **Доебаться** — failed attacks (loss/blocked/skipped) stack letters on the target (`CP:2631-2647`); beating the target cashes stacks ×2 regular (≥7 stacks: +10 extra "love") (`CP:2649-2667`); enemies who beat HardKitty clear their own stacks (`CP:832-839, 3741-3756`).
- **Никому не нужен** — forced to last place before bots act and at end-of-round sort (`CheckIfReady.cs:1141-1151`, `DoomsdayMachine.cs:1359-1369`); bonus points may take his score negative (`InGameStatusClass.cs:231-232`); nobody can be dropped onto him; letters broadcast on stat-up rounds 3/5/7/9 (`CP:5879-5894`).
- **Mute** — first successful attack win vs him per unique enemy: +1 regular to them (`CP:817-830`); his own attack-wins score −1 (`DoomsdayMachine.cs:775-781`); defense wins still +1 (consistent with "напал и победил").
- Excluded from team-mode rolls (`StartGameLogic.cs:70-71`).

## Sirinoks — Tier 6, Int 5 / Str 1 / Speed 5 / Psyche 4

- **Обучение** — losing an attack she initiated (with no active training) starts training on the winner's highest stat she's below (`CP:1852-1902`); +1 to that stat per round (`CP:3611-3663`) and on level-ups (`GameReactions.cs:1249-1297`); reaching the target: +3 Мораль +10% Skill, training clears (target can't change until done).
- **Заводить друзей** — first attack per unique enemy: friend +1 regular (`CP:1207-1212`).
- **Го играть** — implemented *inside* Заводить друзей (D4): attacking a befriended blocker/skipper sets Armor/SkipBreak (`CP:1193-1204`).
- **Дракон** — entering round 10: all stats 10, bonus points = Skill/10 − 1 per friend below her (`CP:5284-5315`). Counterplay: Спартанец's DragonSlayer (round-10 auto-win unless a Суппорт buffed her — `CP:1111-1129`).

## Злой Школьник — Tier 6, Int 9 / Str 9 / Speed 9 / Psyche 9 (file `Mitsuki.cs`)

- **Дерзкая школота** — +100 Skill at start (`CP:185-191`); every non-skip round: −20 Skill and two random stats −1 (can hit the same stat) (`CP:3807-3847`); round 1 bonus: +1 regular (`CP:4866-4874`).
- **Много выебывается** — starts at place 1 (`CP:200-208`); +1 regular per round at #1 (`CP:5832-5839`) and per round nobody attacked him (`CP:3849-3866`); +40 Skill for beating his Мишень target (`CP:1628-1636`); Вред from a higher-skill enemy while #1 → self-Drop (`CharacterClass.cs:223-229`).
- **Запах мусора** — logs every attacker (`CP:555-565`); after round 10: −5 bonus to everyone who attacked him ≥2 times (`CP:5841-5864`).
- **Школьник** — one scheduled round in 2–9: forced skip (brother takes the PC) + 5 next-round Justice (`CP:4876-4892`).

## AWDKA — Tier 6, Int 3 / Str 2 / Speed 4 / Psyche 6

- **Научите играть** — attacking records the target's best stat (`CP:1216-1244`); next round AWDKA's matching stat is set to it (volibir icon in the stat line), previous copy restored (`CP:4927-5025`).
- **Я пытаюсь!** — second loss to a unique enemy: +2 level-up points +20 Skill (`CP:2692-2705, 4911-4925`); fully-stacked enemies take his skill at ×2 (`CP:1246-1250`).
- **АФКА** — skip chance grows the longer he's without new Мораль: 1/(32−4×roundsSince), min 1/1 (`CP:4894-4909`).
- **Произошел троллинг** — tracks the score of every enemy he's beaten (at beat time) (`CP:2671-2690`); at game end: bonus = (top-1's recorded score+1)/2 + 1 per correct prediction — only if he ever beat the final #1 (`CheckIfReady.cs:389-459`). He is also silently shoved to last place before every fight calculation — **intended** hidden mechanic (M3, ОК).

## Darksci — Tier 6, Int 6 / Str 7 / Speed 8 / Psyche 5

- **Мне (не)везет** — game-start choice (web/button): stable = +20 Skill +2 Мораль immediately **and every round** (`CP:5897-5906`); unstable = doubles Повезло.
- **Повезло** — after his attacks have touched all 5 enemies: stable +100% of current score (+2 Psyche +2 Мораль), unstable +200% (+4/+4), once (`CP:1913-1938`). ⚠ only his own attacks count toward "touching" (defensive fights don't, JSON says "состоявшегося боя").
- **Не повезло** — −1 Psyche per loss (`CP:2721-2726`); psyche 0 ⇒ forced skip each round (split across `CP:5944-5964` and the level-up path).
- **Дизмораль** — round 9: −5 Psyche, fired from inside `GetLvlUp` (dodgeable by hoarding the round-9 point — **intended** tech; D1, ОК) (`GameReactions.cs:1226-1243`).

## Братишка — Tier 4, Int 0 / Str 0 / Speed 0 / Psyche 10 (file `Shark.cs`, class `Shark`)

- **Ничего не понимает** — attackers fight with Int 0 (`CP:445-456`); first attack per enemy also −1 permanent Int.
- **Булькает** — +1 live Justice per round below #1 (`CP:5764-5767`); cannot gain Skill or Мораль at all (`CharacterClass.cs:963, 1010, 1125-1130`) — moral buttons useless by design.
- **Лежит на дне** — +1 regular whenever a leaderboard neighbor loses a fight (`HandleShark`, `CP:6574-6592`).
- **Челюсти** — +1 Speed per unique enemy beaten (`CP:2744+`) and per new place visited (`CP:5807-5818`).
- Boole Family: immune to Вред (`CharacterClass.cs:199`); only one Boole-family (tier-4) character rolls per game.

## Загадочный Спартанец в маске — Tier 5, Int 7 / Str 10 / Speed 6 / Psyche 1 (file `Panth.cs`, class `Spartan`)

- **Им это не понравится** — 2 marks at start (Rick forced; Глеб/mylorik/Спартанец excluded; Школьник only from round 4, Вампур only before round 4), re-rolled on rounds 2/4/6/8 (`CP:109-157, 3758-3803`); wins vs marked: +1 regular +1 bonus (`CP:1825+`); marked blockers: armor-break + auto-win (`CP:1098-1107`).
- **Это привилегия - умереть от моей руки** — every win from round 5: the victim gains **+1 extra** next-round Justice and Спартанец loses **1 Int** (`CP:2782-2790`); additionally, vs top-3 victims his Harm deals extra Str-resist damage scaling with the skill ratio (extra Drops; "THIS. IS. SPARTA!") (`CharacterClass.cs:237-277`).
- **Первая кровь** — ×2 Skill all game (`SetAnySkillMultiplier(1)`, `CP:105-107`); the first attack's winner (him or the target) gets +1 Speed (`CP:1133-1136` + win handling).
- **Они позорят военное искусство** — first attack per unique enemy: −1 Str −1 Speed; mylorik and Кратос are spared and instead both sides gain +1 Psyche (`CP:1138-1163`).
- **DragonSlayer** — round-10 auto-win vs the Дракон unless Суппорт-buffed (`CP:1111-1129`). **Skill 228** — skill display/value capped at 228 (`CharacterClass.cs:902-907`). **Искусство** — flavor intro line (shared with mylorik, `CP:77-80`). **2kxaoc** — hidden; its only special handling is being *exempt* from the enemy-passive-name masking in the stats display, so the meme stays readable (`GameUpdateMess.cs:798-811`, D5).

## Вампур — Tier 5, Int 6 / Str 6 / Speed 6 / Psyche 6 (file `Vampyr.cs`)

- **Вампуризм** — attack-win: gains the victim's current Justice (+ the ignored point from Падальщик) as next-round Justice — **copies**, doesn't drain (intended; D6, ОК) (`CP:1842-1846`); every even round: +Мораль per active bite (`CP:3926-3931`).
- **Падальщик** — attacking someone who lost last round: their Justice −1 for the fight (`CP:1253-1261`); such wins +3 Мораль (`CP:1836-1840`).
- **Гематофагия** — first win vs each unique enemy: bite → +2 to a random stat he's below 10 in, applied at end of round (`CP:2803-2864, 3868-3893`). Hidden priority: while his Psyche ≤ 8 and he has fewer than two Psyche-bites, the bite is forced onto Psyche (`CP:2827-2842`).
- **СОсиновый кол** — can't target anyone who beat him last round (attack UI refuses, `GameReactions.cs:710-715`); losses remove a bite: −2 that stat −1 regular (`CP:2866-2885, 3895-3922`).
- **Vampyr Позорный** — Вампур **cannot level up stats**: each level-up zeroes the stat choice (`skillNumber = 0`) so no stat is added (the point is still spent), logging "Никаких статов для тебя" (`GameReactions.cs:994-1000`; m2 fixed — restored from commented-out).
- "Вампур" garlic level-up placeholder (m1 fixed — Name check was the typo "Вампур_"); "Vampyr" hidden passive = round-1 intro phrase (`CP:5319-5327`).

## Краборак — Tier 4, Int 2 / Str 3 / Speed 1 / Psyche 9 (file `CraboRack.cs`)

- **Панцирь** — first attack from each unique enemy: auto-block (+3 Мораль +33 Skill), block flag cleared after the interaction (`CP:427-439, 2455-2463`).
- **Болевой порог** — each incoming next-round Justice point: 50% converted to +1 regular instead (`CharacterClass.cs:1654-1672`).
- **Хождение боком** — attackers fight with Speed 0 (`CP:441-443`); 3 scheduled rounds: own Speed →10, restored next round (`CP:5139-5164, 3457-3468`).
- **Питается водорослями** — attacking places 4–6: +1 bonus (`CP:1309-1310`).
- Boole Family: Вред-immune. (The dead `BokoBoole` class was deleted — m6 fixed.)

## Weedwick — Tier 6, Int 3 / Str 3 / Speed 3 / Psyche 6 (file `WeedWick.cs`)

- **Оборотень** — swaps Strength with the target for the fight — attack only (defense case commented out) (`CP:1070-1080, 403-414`).
- **Безжалостный охотник** — always Armor/SkipBreak (`CP:1082-1086`); vs 0-Justice targets (or Rick): Speed ×2 for the fight (`CP:1088-1094`).
- **Ценная добыча** — win pays the victim's win-streak: regular if they're above him, bonus if below (`CP:1727-1741`); extra Вред rolls: 1/place, 1/5, +1/3 vs #1, range ignored (`CP:1772-1820`).
- **Weed** — every player's every win adds a Weed stack on themselves (`DoomsdayMachine.cs:64-66`); Weedwick's win harvests the victim's stacks as Мораль (`CP:1678-1723`); a round without smoking: −1 Psyche (`CP:5754-5760`).
- **Weedwick Pet** — see DeepList Pet.

## Молодой Глеб — Tier −2 (transform-only), Int 8 / Str 8 / Speed 8 / Psyche 8

- **Main Ирелия** — level-ups *subtract* 1 (`GameReactions.cs:1002-1005`). Like every character, on web she cannot continue her turn until the pending (forced-nerf) point is spent — the general level-up gate `WebGameService.LevelUpGate` (M15) — but keeps her own refusal line "Риоты не прощают, нерфа не избежать" instead of the generic prompt.
- **Коммуникация** — round 6 attack: pink-wards the target for everyone (global log + auto-predictions; Монстр immune) (`CP:1004-1027`).
- **Следит за игрой** — each round the meta marks **up to 3 targets**, chosen by a bot-style preference formula (justice gaps, places, last round's outcomes, Мишень/nemesis fit) (`CP:4627-4667`, `YongGlebMetaClass`); attacking any of them (or blocking while marked himself): +1 bonus (`CP:993-1002, 392-401`).
- **Спокойствие** — immune to Мораль loss (`CharacterClass.cs:1156-1160`) and psyche loss (`MinusPsycheLog` guard); tea action (cd 3): +1 regular, target force-skips next round (`CP:1179-1191, 5992-6001`).
- Never rolls naturally — Tier −2 is excluded from the roll pool by `CharactersPull.GetRollableCharacters` (Tier ≥ −1 filter, `CharactersPull.cs:43-50`); exists via Глеб's transform, which keeps `Name == "Глеб"` by design — the young form is detected by the `Main Ирелия` passive (m3 fixed).

## Sakura — Tier −1 (secret, rollable), Int 6 / Str 10 / Speed 6 / Psyche 10

- Tier −1 = secret: rollable by humans at range 40 (bots never roll tier <4), hidden from the predictions menu and character lists (`CharactersPull.cs:34-50`, `GameStateMapper.cs:54`) — so nobody can score a prediction on her.
- **Одна из трех** (hidden) — game end: if top-3 and alive, she's declared the winner (`top3Player`, `CheckIfReady.cs:514-524`). Her real place / leaderboard / MatchHistory stand **by fact**, but she is paid **first-place stats & rewards** — TotalWins +1, character-Wins +1, mastery 10, ZBS 100, top-2 loot box — via a `rewardPlace` of 1 in the payout loop (`CheckIfReady.cs:631-728`; D3 fixed: "статы и награды как за первое место").

## Баг — Tier −1 (secret, rollable; humans only), Int 1 / Str 3 / Speed 3 / Psyche 7 (no state file)

- **AdminPlayerType** — granted to `PlayerType == 2` players at runtime (`GameUpdateMess.cs:253`); sees everyone; auto-predicts all at game end (`CheckIfReady.cs:283-292`).
- **AutoWin** — targets can't win; Armor/SkipBreak (`CP:963-967`).
- **PointFunnel** — Баг earns no win points himself (`InGameStatusClass.cs:174-177`); his turn's target funnels a copy of their regular points to him for the round (`DoomsdayMachine.cs:143-166`, `InGameStatusClass.cs:179-191`, cleared `DoomsdayMachine.cs:1243`).
- **Exploit** — one rotating exploitable player per round (`GameClass.cs:141-180`); every loss while exploitable accrues to a global pot (`DoomsdayMachine.cs:73-76`); Баг's attack on the current carrier patches it (them permanently) and claims the pot as regular points (`CP:1613-1625`). The rotation only runs when a Баг player is in the game (`GameClass.cs:143-146`; m5 fixed).

## DooM Guy — Tier 4, Int 2 / Str 5 / Speed 5 / Psyche 5

State and module registry: `DoomGuy.cs:9-288`; the four hidden character passives are the load-bearing stage identifiers **Rune → Shield → Mission → Gun** (`characters.json:1383-1413`). Instead of stat upgrades, rounds 3/5/7/9 offer the modules configured for that stage in the account's Fortress loadout (`DoomGuy.cs:53-60, 107-111`; `GameReactions.cs:801-814`). Activating a module reveals that stage passive and replaces its description for the owner (`DoomGuy.cs:128-166`).

- **Rune** — **Вознесение** immediately gives +8 Int; every lost interaction removes 1 Int. **Маневры** gives +5 Speed; every received Harm removes 1 Speed (`DoomGuy.cs:141-149`; `CP:2431-2456`; `CharacterClass.cs:204-208`). Reward module **Истребление** records unique defeated enemies; after all five, +1 to every stat and bonus points equal to `max(0, 10 − current round)` (`CP:2440-2454`).
- **Shield** — **Щит-пила** makes an attacker stopped by DooM Guy's block lose 3 bonus points instead of 1. **Шоковый щит** makes the first attacker stopped by the block skip the next round; the charge is match-once (`DoomsdayMachine.cs:500-515`; `CP:4728-4745`). Reward **Адский блок** grants +666 Extra Skill once when at least two attacks hit the same block in one round (`CP:708-722, 3410-3415`).
- **Mission** — **Адеские гнезда** places one nest on a new living enemy when selected and at each next-round setup; attacking and resolving a fight against that carrier destroys it for +1 regular point. More than three active nests causes −20 immediate bonus and clears the outbreak. **Навести беспорядок** grants +1 regular for every resolved fight, win or loss (`DoomGuy.cs:150-154, 178-196`; `CP:2460-2476, 4746-4750`). Reward **Стань богом** gives +20 bonus after round 10 if DooM Guy never blocked and never lost after the module became active (`CP:3416-3427`).
- **Gun** — **BFG** starts charged. It is preserved by blocking and by decisive fights; on the next attack that reaches Step 3 random, it is spent and makes that primary result a win. A winning primary launches two leaderboard-neighbour branches; each secondary win continues its branch one place farther, while a loss/block/skip ends that branch (`DoomsdayMachine.cs:705-726, 756-769`). **Кулаки** sets persistent Strength to 0 and gives +2 regular on each win (`DoomGuy.cs:155-160`; `CP:2480-2485`). Reward **Бензопила** is spent on the next win, offers up to the first four non-admin passives of the victim, replaces Gun with the selected passive, and primes one use for the explicit charge passives Portal Gun, Шэн, Изанаги and Глаза Итачи (`CP:2486-2501`; `DoomGuy.cs:198-225`).
- **Let's Roll!** — round-1 opt-in. Sets Moral to 0, rejects future Moral conversion/gains, clears/disables predictions, and on each stage chooses randomly among the configured slots; every random module grants +2 regular points (`DoomGuy.cs:113-126, 170-175`; `CharacterClass.cs:1136-1137`; `DoomsdayMachine.cs:1446-1457`). Bots activate it automatically (`CP:4721-4726`).
- **Persistent Fortress/rewards** — every account starts with the two base modules in each category, four ordered slots per category, and newly unlocked rewards fill the first empty slot (`DoomGuy.cs:64-97, 234-260`). Only a DooM Guy finish rolls a module: place 4 starts at Rune, 3 at Shield, 2 at Mission, 1 at Gun; a completed category falls back one stage at a time. Per-stage chance is 80% while all of a multi-module reward pool remain and linearly declines to 5% for its last remaining module (`DoomGuy.cs:227-260`; `CheckIfReady.cs:666-675`). Human accounts below 10 completed games use an exact 30% DooM Guy branch when he is eligible (`StartGameLogic.cs:177-205`).

## Эрен Йегер — Tier 6, Злость 0 / Str 3 / Speed 3 / Самоуверенность 8

State: `Game/Characters/ErenYeager.cs:8-72`; owner counters plus the per-player 0/1/2 hatred mark are held in `PassivesClass.cs:267-271`. `Злость` and `Самоуверенность` are display names for the ordinary Intelligence/Psyche stats; the Discord stat panels/level-up menu and web card use those names, while fight math remains unchanged (`CharacterClass.cs:1209-1212, 1300-1303`; `GameUpdateMess.cs:59-77, 1098-1112`; `PlayerCard.vue:854-947`). The supplied four player-facing descriptions are stored verbatim in `characters.json:1424-1441`.

- **Овца в загоне** — mutually exclusive with HardKitty in natural rolls/drafts (`StartGameLogic.cs:99-102, 245-250, 300-303`). Eren is moved to place 6 before round 1, before every calculation through round 8, and after every sort while the newly opened round is ≤8 (`CP:182-186`; `CheckIfReady.cs:1215-1223`; `DoomsdayMachine.cs:1458-1465`). Rounds 1–8 emit the eight supplied phrases in order (`CharactersPhrases.cs:11-20`; `CP:185-186, 4880-4882`). At the start of rounds 2–8 he gains +1 persistent Intelligence/`Злость`, with an explicit gain cap of 8 (`CP:4869-4883`). ⚠ The written interval 2–8 contains seven triggers, so an unextended normal game gives +7, despite the mechanic note also saying «максимум получить +8».
- **Дрочун** — whenever an enemy starts an attack against Eren, its mark becomes 2 immediately; an Eren loss sets the winner's mark to at least 1 and never downgrades 2 (`CP:438-441, 2465-2478`). An Eren victory cashes only the defeated enemy's current mark as that many immediate bonus points and clears it (`CP:2480-2486`). If Eren's own attack target also targeted Eren that turn, Eren gets +2 regular points once per enemy per round, regardless of the result; the server increments an audio serial for `eren_tatake.mp3` (`CP:2489-2500`; `ErenYeager.cs:21-23`; `Game.vue:211-216`). Marks are shown as `🔥1/2` in Discord/web leaderboards and listed in the owner widget (`GameUpdateMess.cs:443-446`; `GameStateMapper.cs:349-370`).
- **Атакующий Титан** — during calculation, Eren's block flag is removed before block resolution, so he never blocks attacks; the Titan state/sound serial is armed (`DoomsdayMachine.cs:271-281`). Each fight that round reapplies exact +5 temporary Int/Str/Speed/Psyche overrides on the `FightCharacter`, so all incoming/forced fights receive the boost and values may exceed the normal persistent 10 cap (`CP:62-72, 443-447, 1002-1006`). If nobody targeted Eren that turn, end-of-round processing removes 2 Psyche/`Самоуверенность` through `MinusPsycheLog`; otherwise there is no penalty (`CP:3545-3555`). The client rolls `eren_attack_on_titan_use_most` at 50%; the other 50% is uniform over files 1–3 (`sound.ts:894-900`).
- **Rumbling** — every resolved loss increments the match loss counter (`CP:2505-2509`). Opening round 10 writes the supplied Armin warning verbatim (`CP:4886-4896`) and starts both rumbling audio files together for every connected web player (`Game.vue:218-228`; `sound.ts:902-905`). Immediately after all round-10 fights, before `HandleEndOfRound`, a living Eren with fewer than two losses projects the leaderboard using current score plus the pending round score with its real multiplier (including `Подсчет`) (`DoomsdayMachine.cs:1235-1236`; `ErenYeager.cs:37-71`; `CP:3484-3499`). Every living player strictly between Eren's projected place and place 6 dies with source `Rumbling`; place 6 (Eldia) itself survives. Goblins have no immunity; Shisui's general next-round revive still applies. Deaths feed Монстр's +1-per-death passive before later end-of-round passives execute (`CP:3501-3528`).
- **Audio/UI** — owner widget exposes gained Rage, losses toward the Rumbling gate, Titan state, hatred marks and resolved Rumbling place (`GameStateDto.cs:775-793`; `GameStateMapper.cs:349-370`; `PlayerCard.vue:1205-1224`). Tatake/Titan sounds are owner-only serial events; the Rumbling warning and `eren_game_win_theme.mp3` are global (`Game.vue:180-228`; `sound.ts:884-905`).
