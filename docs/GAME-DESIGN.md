# King of the Garbage Hill — Game Design Document

> Code-verified against the working tree of 2026-07-12 (v4.3.29). Every mechanic below was read from the source, not from player-facing descriptions. File references are `path:line` as of this tree.
>
> Companion docs: [ARCHITECTURE.md](ARCHITECTURE.md) (code structure), [CHARACTERS.md](CHARACTERS.md) (all 40 character definitions, including special-purpose entries), [AUDIT-FINDINGS.md](AUDIT-FINDINGS.md) (design-vs-code audit).

## 1. What the game is

A 6-player simultaneous-turn king-of-the-hill game, 10 rounds long. Each player secretly plays one of 40 defined characters (some special-purpose) with hidden stats and passive abilities. Each round every player picks one action; all fights resolve at once; the leaderboard re-sorts by score. After round 10 the player with the most points wins ("Король Мусорной Горы").

The fun is information warfare: you see usernames and behaviors, not characters. You infer who is who ("Предположения" — end-game prediction bonuses), read class tells from fight logs, and play around 30 wildly asymmetric passive kits.

Runs as a Discord bot + web client (Vue/SignalR) in one process; humans and bots mix freely (see ARCHITECTURE.md).

## 2. Core loop

```mermaid
flowchart TD
    A[Turn opens<br/>~300s timer] --> B[Each player picks:<br/>Attack X / Block / Skip<br/>+ spend Мораль, LvlUp on 3/5/7/9<br/>+ Predictions until round 8]
    B --> C{All humans ready<br/>or timer expired?}
    C -->|no| B
    C -->|yes| D[Forced actions & auto-moves<br/>bots decide]
    D --> E[CalculateAllFights<br/>all fights resolve in leaderboard order]
    E --> F[HandleEndOfRound passives<br/>justice & score commit]
    F --> G[RoundNo++ → HandleNextRound passives]
    G --> H[Sort by score<br/>position swaps & locks<br/>Quality Drop]
    H --> I{Round 11?}
    I -->|no| A
    I -->|yes| J[HandleLastRound:<br/>predictions, end-game passives,<br/>final winner]
```

- Turn timer: default 300 s (`GameClass.cs:13`); a 100 ms timer polls readiness (`CheckIfReady.cs:63-74`).
- A human who does nothing gets an auto-move (bot picks for them, `CheckIfReady.cs:1098-1109`); a player who can't be auto-moved auto-blocks (`:1253`).
- Round 10 is the last fighting round. `RoundNo >= 11` ⇒ `HandleLastRound` (`CheckIfReady.cs:945`), except during the Kratos resurrection event (hard cap `RoundNo >= 20`, `:937`).

### Actions
| Action | Effect |
|---|---|
| **Attack X** | Fight X this round. You are the attacker; X defends (X keeps their own chosen action). |
| **Block** (Блок) | Fights against you don't happen; each blocked attacker loses 1 bonus point ("Блок"), you gain +1 next-round Justice — same `AddJusticeForNextRoundFromFight` as a fight loss. Naruto is the explicit replacement: **Гарем но джутсу** clears Block and instead cancels every fight of each attacker whose finalized queue reaches him (`Naruto.cs` `ResolveHaremQueues`). |
| **Skip** (Пропуск) | Fights against you don't happen, attacker pays nothing. Mostly forced by passives (sleep, tilt, ban…). Voluntary skip isn't in the normal UI. |

Attackers with `IsArmorBreak` ignore blocks; `IsSkipBreak` ignores skips (granted by specific passives). Several characters can't block/skip or force others to fight (see CHARACTERS.md).

## 3. Stats and the class system

Four visible stats, integer **0–10** (clamps in `CharacterClass.cs:1398-1401` etc.). Exception: Рик's "Гигантские бобы" lets Intelligence exceed 10 (`:1206`).

| Stat | Fight role | Quality resist (see §7) | Stat-10 bonus (permanent flag) |
|---|---|---|---|
| **Интеллект** (Int) | scale + versatility; defines Умный class | pool 1/2/3 → breaking it costs −10% Skill | +10% Skill (`:469`) |
| **Сила** (Str) | scale + versatility; defines Сильный class | pool 1/2/3 → breaking it causes **Drop** | your Harm hits Str pools for −2 (`:482`) |
| **Скорость** (Speed) | scale + versatility; defines Быстрый class | pool 1/2/**5** = your **Harm reach** as attacker (§4/§7); never consumed by incoming Harm | +1 **kite** — shrinks incoming Harm reach (`:495`) |
| **Психика** (Psyche) | scale + own weighing term | pool 1/2/3 → breaking it costs −20% Мораль | +20% Мораль (`:508`, `:1085-1088`) |

### Class (Класс)
Your class = your **highest** of Int/Str/Speed, computed live (`GetSkillClassType`, `CharacterClass.cs:756-765`; ties resolve Int > Str > Speed; all three 0 ⇒ **Буль**/None). Class is never stored in `characters.json`.

```mermaid
flowchart LR
    INT[Умный<br/>Интеллект] -->|counters| SPD[Быстрый<br/>Скорость]
    SPD -->|counters| STR[Сильный<br/>Сила]
    STR -->|counters| INT
```

- **Nemesis (контр)**: rock-paper-scissors above (`NemesisOf`, `CharacterClass.cs:739-745`). Having nemesis over the opponent: +2 weighing, ×1.5 on your skill term (attacker side), +1 skill multiplier in the fight (`CalculateRounds.cs:74-95`). Losing a fight where nemesis fully decided it leaks your class to the victim's log ("вас обманул/пресанул/обогнал", `DoomsdayMachine.cs:39-53, 1061-1069`).
- **Class perks** (each fight, `DoomsdayMachine.cs:619-629, 754-755`): Умный +6×mult Skill when target has 0 Justice; Быстрый +2×mult Skill in every fight (both roles); Сильный +4×mult Skill on every win.
- **Мишень (class target)**: every player has a private rotating target class (`CurrentSkillTarget`), first randomly rolled, then cycling Int→Str→Speed each round (`RollSkillTargetForNextRound`, `CharacterClass.cs:826-838`). Attacking someone whose class matches your Мишень grants Main-Skill (diminishing 10,9,8…1 — and the same amount again as Extra Skill, so effectively 20,18,…,2 — `AddMainSkill`, `CharacterClass.cs:961-1000`) and writes their class tag into your notes.

## 4. Fight resolution

All in `CalculateRounds.cs` (called from `DoomsdayMachine.cs:758-899`). A fight computes `pointsWined` from the attacker's perspective: **Step 1 (stats)** ±1, **Step 2 (Justice)** ±1; if the sum is 0, **Step 3 (random)** decides ±1. Final ≥1 ⇒ attacker wins.

```mermaid
flowchart TD
    S1["Step 1 — weighing machine<br/>nemesis ±2 · scale diff · versatility ±5<br/>psyche diff ±1/2/4 · skill diff · justice diff"] -->|"weighing > 0 → +1<br/>weighing < 0 → −1"| SUM
    S2["Step 2 — Justice tiebreak<br/>higher RealJustice → ±1"] --> SUM{sum}
    SUM -->|"≠ 0"| RES[attacker wins if ≥ 1]
    SUM -->|"= 0"| S3["Step 3 — random roll<br/>roll 1..maxRandom ≤ randomForPoint → +1"]
    S3 --> RES
```

**Step 1 — weighing machine** (`CalculateRounds.cs:56-410`), accumulator starts at 0, `randomForPoint` starts at 50:
1. **Nemesis**: ±2 weighing; attacker with nemesis also gets `nemesisMultiplier = 1.5` on his skill term; each side with nemesis gets +1 fight skill multiplier.
2. **Scale**: `Int+Str+Speed+Psyche + Skill(mult)/60` per side; add the difference (`:130-132`).
3. **Versatility**: compare Int, Str, Speed head-to-head; majority winner +5 (`:150-184`). Terminology note: historical player-facing texts call *this* bonus «Контр» and call the nemesis RPS «Анти-класс» — the meaning of "контр" flipped between those texts and the code/docs naming.
4. **Psyche diff**: 1–3 → ±1, 4–5 → ±2, ≥6 → ±4 (`:200-220`).
5. **TooGOOD**: |weighing| ≥ 13 ⇒ `randomForPoint` = 70 (or 30 against you) (`:228-251`).
6. **Skill difference**: `scaleMe×(1+mySkill/650×nemesisMult) − scaleTarget×(1+targetSkill/650) − scaleMe + scaleTarget` added to weighing (`:264-282`).
7. **TooSTONK**: |weighing| ≥ 30 ⇒ `randomForPoint` += weighing/2, capped ±20 (`:303-344`).
8. **Justice**: `+ (myJustice − targetJustice)` weighing (`:352`).
9. **Skill random modifier**: `randomForPoint += (mySkill − targetSkill)/60` — uncapped, so huge skill gaps effectively decide Step 3 (`:370-374`).

Madara's own TooGOOD/TooSTONK branches are attacker-count gated: >1 unique incoming enemy enables TooGOOD, >2 enables TooSTONK, and >3 sets his per-fight Skill to 100; the opposing character's thresholds still work normally (`Madara.cs:55-110`; `CalculateRounds.cs:228-344`).

**Step 2** (`:415-436`): +1/−1 to whoever has higher live Justice.

**Step 3** (`:438-492`), only on a tie: `maxRandom = 100 − (myJustice×nemesisMult − targetJustice)×5` (when either side has Justice > 1); roll 1..maxRandom; roll ≤ randomForPoint ⇒ attacker wins. Justice therefore shrinks the loss window *and* shifts the threshold.

**Overrides after the math**: `IsAbleToWin=false` on a side adds ∓50 (passives use it to force outcomes); Котики's Storm can nudge ±5 and redirect the win point; Осьминожка's Неуязвимость/Itachi's Изанаги can flip the result outright. A successful Naruto **Призыв** is applied after those defensive replacers and is therefore a terminal attacker win (`DoomsdayMachine.cs:777-899`).

### Win/loss consequences (`DoomsdayMachine.cs:776-985`)
- Winner: +1 **regular point** (`AddWinPoints`; Еврей passives may steal it; "Никому не нужен"/"INT" holders get **−1** instead when they attack-and-win). **На мели** is the defensive exception: Saitama gets no victory point when he wins as defender (`DoomsdayMachine.cs:1160-1181`). Any win sets `IsWonThisRound` (Justice reset at end of round), including an unpaid defensive Saitama win.
- **Мораль transfer**: `moral = attackerPlace − defenderPlace`; flows only when the *lower-placed* side wins, and only from round 2 (`:798-815, 920-936`). Winner +moral, loser −moral (Минька deals no moral loss).
- Loser: `AddJusticeForNextRoundFromFight()` (+1 next-round Justice). A loss staged by Saitama's **Неприметность** is the exception and grants him no Justice (`CharacterPassives.cs:667-690`; `DoomsdayMachine.cs:1022-1023`).
- **Вред (Harm)**: winner-attacker applies `LowerQualityResist` to the loser if the leaderboard distance ≤ attacker's Speed-resist range minus defender's kite bonus (`:913-926`). Butcher deals `1 + butcherState.PokerCount` base Harms; СуперМудень doubles the **complete** number, so Кочерга #4 = 5→10 (`DoomsdayMachine.cs:962-998`). Under СуперМудень, every Harm that underflows an Int/Strength/Psyche pool and actually applies its −10% Skill / Drop / −20% Moral effect queues one more Harm; new breaks recursively enqueue more (`CharacterClass.cs:296-345`; `DoomsdayMachine.cs:973-1018`). It bypasses enemy Harm interceptors, lets a place-6 victim keep losing Drop points, stops at score 0, and caps at **50 actual Drops across the attacker's whole turn**; the counter resets next turn (`CharacterClass.cs:182-238`; `TheBoys.cs:60-67`; `CP:6273-6276`).
- **Madara exception**: `Воскрешенное тело` rejects incoming Harm and Madara never deals Harm as attacker; Skill, Moral, predictions, level-ups and negative stat mutations are also disabled (`CharacterClass.cs:198-203,781-846,911-1167,1239-1605`; `DoomsdayMachine.cs:860-969`).
- Defender wins: symmetric, but no Harm is dealt (Harm is attacker-only). Toxic Mate's `INT` still applies its negative point; **На мели** suppresses Saitama's defensive victory point (`DoomsdayMachine.cs:1160-1176`).

## 5. Resources

### Skill (Скилл)
Two internal pools — `SkillMain` (only from Мишень) and `SkillExtra` (everything else). Effective skill = `(Main+Extra) × (SkillFightMultiplier + fight bonuses) × IntelligenceQualitySkillBonus`, hard-capped at 228 for "Skill 228" holders (`CharacterClass.cs:892-908`). Skill enters fights via the scale term (/60), the skill-difference term (/650), and the Step-3 modifier (/60). Multiplier knobs used by passives: `TargetSkillMultiplier` (Мишень gains), `ExtraSkillMultiplier` (extra gains), `ClassSkillMultiplier` (class perk gains), `SkillFightMultiplier` (combat usage).

### Justice (Справедливость)
Clamped **0–5** (`CharacterClass.cs:1677-1712`). Gained (next round) by losing a fight, being attacked into your block, or from skills (`AddJusticeForNextRoundFrom{Fight,Skill}`). **Близнец is the block exception**: no generic +1; Monster instead copies the highest blocked attacker's live Justice without draining it (`DoomsdayMachine.cs:591-595`; `CharacterPassives.cs:978-1002`). **Неприметность is the loss exception**: Saitama gains no Justice when he only pretends to lose (`CharacterPassives.cs:667-690`; `DoomsdayMachine.cs:1022-1023`). **Winning any fight this round zeroes your Justice before the buffer lands** (`HandleEndOfRoundJustice`, `CharacterClass.cs:1631-1689`). Effects: weighing term, Step-2 tiebreak, Step-3 window shrink; "Умный" only milks skill from 0-Justice targets. `SeenJusticeNow` (what enemies can infer) only counts fight-sourced justice. Краборак's "Болевой порог" coin-flips each incoming justice point into a regular point instead (`CharacterClass.cs:1654-1673`).

### Мораль (Moral)
A spendable currency, gained mostly by beating players placed above you (see §4). Spend at any time (`GameReactions.cs:45-133`):
| Мораль spent | → Skill (`HandleMoralForSkill`) | → Bonus points (`HandleMoralForScore`) |
|---|---|---|
| 1 / 2 / 3 | +2 / +6 / +10 | — |
| 5 | +18 | +1 |
| 7 (Еврей only) | +40 | — |
| 8 | +30 | +2 |
| 13 | +50 | +5 |
| 20 | +100 | +10 |

One tier per press, largest affordable tier first. Score conversion stages into `BonusPointsFromMoral`, flushed into real score at the start of the next round's calculation (`DoomsdayMachine.cs:218-224`). On round 10 all moral ≥5 is force-converted to score before the final fights (`CheckIfReady.cs:1309-1312`). Psyche ≥ 10 inflates displayed moral by +20% per bonus tier (`SetMoralBonus`). Blockers: Булькает zeroes moral, Геральт gains none, cancer blocks gains, Привет со дна fixes any gain at +4 and ignores losses, Спокойствие ignores losses (`CharacterClass.cs:1118-1177`).

### Psyche as a resource
No engine rule punishes 0 Psyche — all tilt/skip effects are per-character passives (Дизмораль, Буль, АФКА…). Psyche loss must run through `MinusPsycheLog` (logs "{user} психанул" globally; respects Спокойствие and M.M.'s Оковы immunity, `GamePlayerBridgeClass.cs:91-104`). Безумие (DeepList) bypasses the 0-floor so psyche can go negative (`CharacterClass.cs:1295`).

## 6. Score

Two currencies (`InGameStatusClass.cs`):
- **Regular points** (`AddRegularPoints` / `AddWinPoints`): buffered during the round in `ScoresToGiveAtEndOfRound`, then multiplied and committed at end of round (`InGameStatusClass.cs` `CombineRoundScoreAndGameScore`, `GetRoundScoreMultiplier`). **Multiplier: rounds 1–4 ×1, 5–9 ×2, round 10 ×4.** Толя's "Подсчет" forces a victim's multiplier to ×1 for a round.
- **Bonus points** (`AddBonusPoints`): applied to Score immediately, never multiplied. Total score floors at 0 (except HardKitty's "Никому не нужен", which may go negative — including via `HardKittyMinus`).

Leaderboard = players sorted by Score desc; ties keep list order (first-come); a tied top score at game end prints "Ничья". Place matters mechanically: moral transfers, Harm range, many passives (mines at places 1/2/6, "Лежит на дне" neighbors, etc.). The two dispersed Naruto clones are the exception: their score is zeroed and every later/final sort keeps them below all four non-clones (`Naruto.cs` `OrderLeaderboard`, `MoveDispersedClonesToBottom`).

## 7. Quality, Вред (Harm), Скинуть (Drop)

Each character carries per-stat **resist pools** sized by the stat: 0–3→1, 4–7→2, ≥8→3 (Speed: ≥8→**5**) (`SetXResist`, `CharacterClass.cs:459-509`); pools re-arm on stat changes proportionally (`UpdateXResist`). The Speed pool is offense, not defense: it is never consumed by Harm — it sets your attacker-side **Harm reach** (§4), while incoming reach is shrunk by the **kite** bonus (Speed-10 flag +1, plus passive-granted range, `GetSpeedQualityKiteBonus`, `:390-402`). A stat of 10 grants the pool's bonus flag permanently (+10% skill / +1 drop pierce / +1 kite / +20% moral).

One **Вред** = `LowerQualityResist(victim)` (`CharacterClass.cs:195-325`): −1 to Int, Psyche and Str pools (attacker with 10 Str pierces Str for −2). Round 1 is Harm-free. When a pool underflows:
- Int pool → re-arm + **−10% Skill** permanently (`IntelligenceQualitySkillBonus--`);
- Psyche pool → re-arm + **−20% Мораль**;
- Str pool → re-arm + **Drop**: `HandleDrop` = −1 bonus point, global "Они скинули **X**! Сволочи!", and at end of round the player is pushed one leaderboard slot down per Drop (`DoomsdayMachine.cs:1498-1532`). Place 6 can't drop; you can't be dropped onto HardKitty or onto a Ziggurat-locked Goblin. Монстр без имени gains +1 bonus point on *every* drop in the game (`CharacterClass.cs:188-192`).

Immunities/specials: Boole Family rejects ordinary Harm; Kimiko guards TheBoys; Испанец converts ordinary Harm into +1 Мораль for mylorik; Madara's Воскрешенное тело rejects ordinary Harm. **СуперМудень bypasses all four** (`CharacterClass.cs:205-238`). Спартанец's "Это привилегия…" adds extra Str-pool damage vs top-3 targets from round 5 scaling with skill ratio (`CharacterClass.cs:246-295`).

## 8. Exact round pipeline

Order matters constantly; this is the canonical sequence.

**A. Turn close**: readiness/timeout (plus a live 30-second round-8 Madara bot-reaction gate) → pre-arm an already-active round-10 Вечное Цукуеми total Skip → auto-move idlers → Dopa second-action automove → **AWDKA shoved to last place in the list** → bots + auto-movers act (round-8 strict bots force only the exact Madara prediction) → HardKitty shoved last → Геральт skip→block unless total Цукуеми → Aggress/cola/Штормяк/auto-block/Монстр forced-action layers → round-8 Madara prediction attacks → sealed/Naruto targets sanitized → unless total Цукуеми is active, charged Шэн consumes its holder's next submitted attack, leaps immediately ahead of that target, arms its one-round random-target magnet and injects the below-position pull → sealed/Naruto targets are sanitized again; a matching aged cola cell is consumed → re-snapshot Madara attackers and clear every real action if Цукуеми is active → round-10 moral dump → `CalculateAllFights` (`CheckIfReady.TickAsync`; `Salldorum.ResolveShenDashes`).

**B. Fight phase**: clear the previous web fight log → capture replay-v2 pre-fight state → flush `BonusPointsFromMoral` → reassert the authoritative Цукуеми skip → **DeepCopy GameCharacter→FightCharacter**. In an active round-10 Цукуеми, combat conversions, contract/Harem/PointFunnel/Justice setup and every fight are bypassed; each seat traverses only ordinary no-target Skip cleanup, leaving `AnyFightThisRound=false`. Otherwise the normal path continues: conversions (Медитация, Pickle, Titan, Portal, Aggress) → Геральт contract injection → Naruto queue/PointFunnel/Justice setup → Storm pre-pick → Railgun → **fight loop in leaderboard order**. After the loop (or total skip), Rumbling resolves, then **Теневые**, then `HandleEndOfRound`, so fight-independent round-10 effects remain authoritative (`Madara.PrepareEternalTsukuyomiRound`; `DoomsdayMachine.CalculateAllFights`).

**C. End of round** (`DoomsdayMachine.cs:1510-1627`): `HandleEndOfRound` (big per-passive dispatcher, after the Rumbling pre-settlement above) → per player: clear flags, auto-ready dead, `SetSpeedResist`, `NormalizeMoral`, **`HandleEndOfRoundJustice`**, **`CombineRoundScoreAndGameScore`** (×1/×2/×4), clear logs, clear PointFunnel → Kratos all-gods-dead check → freeze this round's replay fight/global-log stream (with a provisional result fallback) → `RoundNo++`.

**D. Next-round setup**: `HandleNextRound` → save position locks → **sort by score** → Тигр/Portal/HardKitty movers → level-ups + place/target/history → restore locks → Storm/Drop → post-sort effects → force dispersed Naruto clones back to score 0 and the bottom → bot predictions/replay finalization → restart timer (`Naruto.cs` `OrderLeaderboard`, `MoveDispersedClonesToBottom`).

**E. Game end** (`HandleLastRound`): Геральт pitchfork → predictions (+1, or +2 with Великий летописец) → M.M. multiplier → Francie virus → Цукуеми → sort → AWDKA → Premade → Sakura → rewards/replay. Naruto-to-sibling guesses always pay 0. Because the bot clones are already dead here, their correct **non-sibling** guesses are projected into the immediate Теневые pot before dispersal and the ordinary prediction loop excludes them, preventing both loss and double payment (`Naruto.cs:313-378`; `CheckIfReady.cs:307-323`).

Note the **settlement layers before HandleLastRound**: Rumbling is first, Теневые is second, then the ordinary `HandleEndOfRound`, `HandleNextRound` and post-sort layers run. Dispersed clones are death-state seats: later score mutations are discarded rather than transferred a second time, and they cannot receive the round-10 Пейзаж fighter reward (`CharacterPassives.cs` `Пейзаж конца света`; `Naruto.cs` `OrderLeaderboard`). Full map in CHARACTERS.md.

## 9. Predictions (Предположения)

Each player assigns a character guess to each enemy. Editable until round 8 and scored at game end (§8E). Naruto's trio starts with the other two Narutos filled correctly, but those sibling guesses deliberately pay 0; ordinary enemy guesses still score, including clone guesses projected into Теневые settlement (`Naruto.cs` `SeedSiblingPredictions`, `ProjectClonePredictionPoints`). Bots otherwise use `HandleBotPredict`; AI difficulty 3 auto-fills correct enemies, except Монстр без имени. Кира writes Death-Note names instead of predicting.

## 10. Team mode

`game.Teams` (2v2v2 / 3v3). Fights between teammates exchange **nothing** — no points, moral, justice, Harm. Team win = highest summed score. HardKitty and Naruto are removed from natural team rolls; `TeamModeOnly` characters never roll in solo (`StartGameLogic.cs` `HandleCharacterRoll`).

## 11. Character acquisition (roll system)

Weighted roll by **Tier** uses range 150/100/90/80/70/60/50/40 for tiers 6/5/4/3/2/1/0/−1. Naruto is Tier 5 but is eligible only in FFA when a human original has two strict bot seats or a bot original is the third strict bot. Round-0 initialization converts exactly two strict bots to independent clones; web joins reserve those seats. Naruto is excluded from natural team rolls and all four passives are excluded from ARAM (`StartGameLogic.cs` `CanNaturallyRollNaruto`, `RollDraftOptions`; `Naruto.cs` `InitializeTeam`; `CharactersPull.cs` `GetAramPassives`). The remaining tier/secret/template and mutual-exclusion rules are unchanged.

**DooM Guy newcomer/meta progression:** for a human with fewer than 10 completed games, when DooM Guy is eligible and was not the previous character, his normal-roll branch is exactly 30%; a miss excludes him from that weighted fallback (`StartGameLogic.cs:201-233`). DooM Guy has a persistent account loadout (`DiscordAccountClass.cs:41`, `DoomGuy.cs` `EnsureFortress`): four stage categories with four slots each. Finishing as him at place 4/3/2/1 can unlock standard Rune/Shield/Mission/Gun reward modules respectively, falling back through lower incomplete stages; 5th/6th never roll a standard module. The drop chance is calculated per category from its standard reward registry, linearly 80%→5% as the pool is exhausted (`DoomGuy.cs` `GetStandardRewardModules`/`TryAwardModule`; payout `CheckIfReady.cs:736-746`). The special Gun **Приручить дракона** never enters those pools or their displayed remaining count/chance: it unlocks deterministically only after DooM Guy wins a round-10 fight against Sirinoks while she carries **Дракон** (`DoomGuy.cs` `TryAwardDragonTaming`; `CP:2467-2477`).

## 12. Global systemics

- **Exploit**: one non-Баг player is "exploitable" each round, rotating sequentially (`RollExploit`, `GameClass.cs:141-180`). Losing while exploitable increments a global counter; the Баг character cashes it in (`CharacterPassives.cs:1678-1689`). The rotation is gated on a Баг player being in the game (`GameClass.cs:143-146`; m5 fixed — it used to run as harmless no-op bookkeeping in every game).
- **Kratos event** (Возвращение из мертвых): after a round-10 death Kratos resurrects, everyone else is forced to block each extra round, defeated players fall off the board (die); ends when all five are dead or Kratos loses (`CheckIfReady.cs:1030-1040`, `DoomsdayMachine.cs:1295-1301`).
- **Death** (Kira, Kratos, Монстр, Rumbling, Теневые): `Passives.IsDead` + `DeathSource`; dead players auto-block/auto-ready, get 0 ZBS and no mastery. Теневые additionally fixes its two dispersed clone seats at zero score/bottom place, while only a newly caused death pays Монстр (`Naruto.cs` `SettleShadowClones`).
- **Bots** (`BotsBehavior.cs:72-128,3706-3715`): round >10 → block; finalize any existing forced Skip; spend Moral by place/character thresholds; spend pending level-ups; finalize a Skip created by that level-up (notably Darksci's round-9 Дизмораль); Кира writes notes; then choose attack/block through the per-game `Nanobot` preference model. The model combines Justice gaps, leaderboard position, Мишень/nemesis, prior fight outcomes, visible defense, Harm/Drop reach and character objectives. This whole path also drives auto-moved AFK humans (`CheckIfReady.cs:1183-1234`), who inherit the game's difficulty but do not roll a bot-only persistent playstyle. Legitimate forced fights are injected only afterward, so the bot gates do not disable Шэн/Штормяк/Монстр effects (M32).
- **Bot AI difficulty** (`GameClass.AiDifficulty`, **default 3 everywhere**; sim override `--ai-difficulty N`, range **0-3**): **0** = pure-random sim baseline, still respecting legal actions; **1** = frozen legacy behavior; **2** = stronger use of visible/earned information and one coherent random character plan for the whole match; **3** = L2 plus omniscience from `AiFullKnowledgeRound` (default round 3), auto-predictions (except Монстр) and a side-effect-free estimate of Step-1 fight terms. L2 understands Мишень, nemesis, versatility signals, visible Block/Skip and Armor/SkipBreak, Harm range, primed Strength-pool Drops, Justice economics, Moral conversion tiers and opponent punish-passives; it does **not** get L3's exact hidden-stat/real-Justice read. Кира's Death Note is never omniscient. Full rule/number catalogue: `docs/BALANCE-CONSTANTS.md` → “Bot AI difficulty” (`BotsBehavior.cs:45-70, 858-1080`).
- **Persistent L2/L3 playstyles**: selected once and stored in `AiPlaystyle` (`GamePlayerBridgeClass.cs:57-59`; picker `BotsBehavior.cs:131-199`). Multi-plan roster: Dopa (Стомп/Фарм/Доминация/Роум), Darksci (Stable/Unstable), Глеб (Classic/Young), TheBoys (Francie/Butcher/Kimiko/M.M.), Goblins (Horde/Army/Economy/Ziggurat), Rick (Portal/Beans), Itachi (Crows/Tsukuyomi), Kratos (GodHunter/Ragnarok), Cats (Ambush/Storm), Tolya (Count/Rammus), Monster (Twin/Apocalypse), Support (Carry/Stakes). The plan governs target priorities, block/Moral policy and level-up path for the match; characters without multiple builds use `Adaptive`. Sim reports include the plan so each branch can be A/B-measured (`SimulationRunner.cs:630-643`).
- **Achievement V2**: 105 account achievements observe 12 global mechanics, paired normal/hard stories for all 40 character definitions/forms (80 cards), and 13 secret cross-character interactions. Normal/hard describes requirement difficulty; reward rarity is catalogued separately and preserves the original 34 cards' rewards. Multi-step progress is the best result from one match, never a cumulative total; unlock rewards are credited exactly once at authoritative game-end settlement (`AchievementClass.cs` `AllAchievements`/`TrackGameEnd`). The original 34 V2 IDs and unlocks remain valid; the 71 added IDs begin tracking after deployment. Epic/Legendary achievements can award loot boxes into the same inventory as alive top-two finishes. The complete rule/reward/loot catalog is [ACHIEVEMENTS.md](ACHIEVEMENTS.md).
- **Daily Quests V2**: every UTC day gives one fixed Anchor plus personalized Skirmish/Ambition contracts, all character-neutral. Cards pay immediately; 2/3 completes the day (+20 ZBS and a weekly stamp), 3/3 adds a loot box, one unfinished random card can be rerolled, and any 5/7 days pay +100 ZBS (`QuestClass.cs:200-260,358-474,638-723`). Full contract: [DAILY-QUESTS.md](DAILY-QUESTS.md).
- **Debug/admin**: `PlayerType == 2` grants AdminPlayerType passive at runtime (sees all); a hardcoded Discord id gets TooSTONK debug lines (`CalculateRounds.cs:330`).

**Out of scope of this document** (they exist, deliberately not covered here): the ZBS store, character mastery/pity beyond §11, the replay system, the Discord/web rendering layers, and the unrelated side-games (Battleship, Blackjack) that share the process. Daily Quests are specified in [DAILY-QUESTS.md](DAILY-QUESTS.md); Achievements and Loot Boxes in [ACHIEVEMENTS.md](ACHIEVEMENTS.md).
