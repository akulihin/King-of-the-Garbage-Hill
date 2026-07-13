# Design-vs-Code Audit — Findings

> Original audit of `DataBase/characters.json` (+ `Game/GameDesign.txt` intent notes, root-level update notes) against the 2026-07-01 working tree (v4.1.8); statuses and fix notes re-verified through 2026-07-12. Historical “Code” bullets describe the pre-fix implementation when a later **Fixed** note is present. `CP` = `Game/GameLogic/CharacterPassives.cs`.
>
> Severity: **Critical** = player-visible wrong outcome / broken kit promise; **Major** = mechanic silently missing/misfiring or balance-relevant hidden behavior; **Minor** = cosmetic, flavor, dead code, small numeric drift; **Design question** = code self-consistent but intent ambiguous.

## Critical

### C1. Cyrillic "Салдорум" vs JSON name "Salldorum" — four dead branches
- **JSON**: character `Name` is Latin `"Salldorum"` (`characters.json:1316`). Live logic (fights, web actions, bot moral/target/block/build cases at `BotsBehavior.cs:597, 2193, 3119, 3602`) correctly checks `"Salldorum"`.
- **Dead branches checking Cyrillic `"Салдорум"`** (can never match):
  - `CheckIfReady.cs:137` — end-game "Великий летописец: испорчено N записей" log never prints.
  - `BotsBehavior.cs:328` — bot moral policy (always moral→skill) never applies.
  - `BotsBehavior.cs:1410` — bot attack-preference case never applies (it also reads the *legacy* `SaldorumKhokholList` — stale even if renamed).
  - `BotsBehavior.cs:2663` — bot level-up preference (PSY-first) never applies.
- **Impact**: bot Salldorum plays with untuned moral/level-up/targeting AI; an end-game log is lost. Human play unaffected.
- **Fix direction**: rename the four checks to `"Salldorum"`; rewrite the `:1409` preference for the current kit (Шэн/rewrite), not the Khokhol legacy.
- **Fixed:** 2026-07-03 (pre-approved string bug) — renamed the three `Name == "Салдорум"` checks to `"Salldorum"` (current anchors: `CheckIfReady.cs:145-146`, `BotsBehavior.cs:597` moral, `:3602-3603` level-up). The dead Cyrillic targeting case was **deleted**, not renamed; the live Chronicler case is `BotsBehavior.cs:2194-2221`. Removed `BAD-NAME|Салдорум|C1` from `tools/known-warnings.txt`. (`SaldorumKhokholList` remains dead — leave for m6.)

## Major

### M1. Goblin "round-10 Ziggurat at place 1 ⇒ win" is a log line, not a win
- **Described** (`GameDesign.txt:508`): "Если на 10м ходу Гоблины строят зиккурат, находясь на 1м месте — они выигрывают."
- **Actual**: `DoomsdayMachine.cs:1535-1546` fires at the **start** of round 10 (after `RoundNo++`), requires place 1 + a ziggurat built at position 1, and only prints "…побеждает!". `HandleLastRound` re-sorts purely by score (`CheckIfReady.cs:390`) with no ziggurat rule. A ziggurat built *during* round-10 processing (`CP:6135-6205`, runs when `RoundNo` is already 11) can never trigger the message.
- **Impact**: the documented win condition doesn't win; a premature "wins!" message can appear a round early and then be false.
- **Fix direction**: enforce at `HandleLastRound` (like Premade's enforced win, `CheckIfReady.cs:481-503`) or drop the message + design line.
- **Fixed:** 2026-07-03 (designer verdict БАГ) — removed the premature/broken log in `CalculateAllFights` (it checked `BuiltPositions` before the round-10 Ziggurat is built) and added an authoritative enforced win in `HandleLastRound` (`CheckIfReady.cs`, right after the Premade block): a non-dead Стая Гоблинов with a Ziggurat at place 1 (`BuiltPositions.Contains(1)`) is bonus-pointed to 1st, re-sorted, and announced — mirroring Premade's overtake.

### M2. "Еврей" web widget renders for Толя with LeCrisp's state
- `GameStateMapper.cs:364-367` keys the widget on passive "Еврей" (both LeCrisp and Толя have it) but fills it from `LeCrispAssassins.AdditionalPsycheCurrent` — LeCrisp-only state. A Толя player sees a dead widget with stolen-psyche 0.
- **Fix direction**: gate on `Name == "LeCrisp"` (pattern: the Геральт case at `:686`).
- **Fixed:** 2026-07-03 — gated the mapper's Еврей case on `player.GameCharacter.Name == "LeCrisp"` (`GameStateMapper.cs:364`, mirroring the Геральт Name gate at `:686`), so the Jew/PROFIT widget is emitted only for LeCrisp; Толя (shares the passive but has no LeCrisp assassin state) no longer gets a dead PROFIT:0 widget. Frontend unchanged — the widget block (`PlayerCard.vue:1177-1185`) renders only when its jew key is present, now undefined for Толя. (The value shown is LeCrisp's assassin-psyche gain, the intended PROFIT display.)

### M3. AWDKA is silently forced to last place for every fight calculation
- `CheckIfReady.cs:1123-1138`: right before bots act (and before all fights), the "Произошел троллинг" holder is moved to the end of `PlayersList` and places re-assigned (comment `//end //AWDKA last`). Score order returns only at end of round.
- **Impact**: during fights AWDKA's place is ~6 regardless of score — inflates his underdog moral, changes Harm kite ranges, place-based passives and bot targeting against him. Documented nowhere.
- **Fix direction**: confirm intent; either document it in the passive description or delete the block (it mirrors the HardKitty "Никому не нужен" block right below it, so it may be a copy-paste leftover).

### M4. Toxic Mate "INT" negative-win rule applies only when he attacks
- `DoomsdayMachine.cs:804-810` negates the winner's point for "Никому не нужен"/"INT" holders only in the attacker-win branch; a defending Toxic Mate who wins gets a normal +1 (`:901-913`), and `CP:3028-3039` adds nothing on wins. JSON: "Побеждая — теряет очки" (unqualified). HardKitty's Mute wording ("если напал и победил") matches the code; INT's does not.
- **Fix direction**: extend the negation to the defense branch for "INT" (or reword the passive).
- **Fixed:** 2026-07-03 (designer verdict БАГ) — added an INT-only negation in the defender-win branch (`DoomsdayMachine.cs:939-942`): a defending Toxic Mate who wins now gets `AddWinPoints(-1)` like the attacker branch. Scoped to `PassiveName == "INT"` so HardKitty's "Никому не нужен" keeps its attacker-only "если напал и победил" behavior.

### M5. "Тигр топ, а ты холоп" has an undocumented second window at game start
- Initial `TimeCount = 3` (`Tigr.cs:10`) **plus** a swap in `HandleEventsBeforeFirstRound` (`CP:212-225`) put Тигр at place 1 immediately, consuming one count; the end-of-round swap (`DoomsdayMachine.cs:1345-1373`) then keeps him there for rounds 2–3. The random trigger (`TigrTopWhen`, rounds 1–8, 1–2 times) later resets the counter to 3 (`CP:4950-4954`) for the *described* "случайный момент" window. Тигр also collects +1 Psyche/+3 Мораль per round at #1 (rounds 2–9, `CP:5914-5921`).
- **Evidence of unintendedness**: designer note `GameDesign.txt:74-75` — "перемена местами должна срабатывать только когда тигр не топ1 (недавно оно вообще будто 2 раза за игру сработало)".
- **Fix direction**: start `TimeCount = 0` and drop the first-round case (keep only the random window), or document the opening window as intended.

### M6. Тигр "Лучше с двумя, чем с адекватными" counts Тигр himself
- `CP:3449-3465` loops `game.PlayersList` without excluding `player` — Тигр's own Int/Psyche trivially match, so at the end of round 1 he pockets +3 bonus points for "recruiting" himself (once, via FriendList dedupe).
- **Fix direction**: `if (t.GetPlayerId() == player.GetPlayerId()) continue;`.

### M7. Butcher pays his point on any win, spec says on a Drop
- the_boys.txt / theboys_update_commit: "+1 point **on drop**" ("очко если удалось его **Скинуть**" — Скинуть is the established Drop term). Code: `CP:3341-3342` awards +1 bonus (+2 SD) whenever Butcher *wins* against a marked sup. Wins are far more common than Drops — balance-relevant.
- **Fix direction**: decide win-vs-drop; if drop, hook into the Harm/Drop path (compare `dropsAfter > dropsBefore` in `DoomsdayMachine.cs:864-910`).
- **Fixed:** 2026-07-03 (designer verdict БАГ — "10 скилла за нападение, и очко если удалось ЕГО скинуть… скинуть при нападении") — the +1 bonus (+2 SD) moved out of the win check (`CP:3345-3346` removed) into the attacker-win Harm/Drop path (`DoomsdayMachine.cs:917-922`): awarded only when `dropsAfter > dropsBefore` and the attacker is Butcher and the target has `TheBoysSupMark`. The +10 Skill hunt bonus (win or loss) stays in the CP "Butcher" case.
- **Corrected:** 2026-07-10 — the first fix still paid only once per fight and used non-multiplying bonus points. The reward is now **`dropsAfter − dropsBefore` regular points** (×2 under СуперМудень), so five Drops of a marked sup pay +5 ordinary (+10 SD), all multiplied at round settlement (`DoomsdayMachine.cs:1036-1046`).

### M8. Toxic Mate "Tilted" rewards skips, not "психует"
- JSON: "Получает бонусное очко каждый раз, когда кто-то __психует__". Code (`CP:4303-4311`): +1 bonus per enemy whose `IsSkip` is set at end of round — no connection to psyche-loss ("психанул") events at all. (The +50 "все не смогли походить" half matches, `CP:4313-4318`, including the intentional "+20" joke log.)
- **Fix direction**: hook the +1 into `MinusPsycheLog`/psyche-rage events, or reword the passive to "за каждый пропуск хода".
- **Fixed:** 2026-07-03 (designer verdict БАГ — skips only pay when no battle happened) — removed the +1-per-skip bonus entirely; the payout is now the single **+50** given only when the whole round had **zero battles** (`game.PlayersList.All(x => x.Status.IsWonThisCalculation == Guid.Empty)`, `CP:4308-4316`). Uses the fight-resolution signal rather than the old all-blocked/skipped proxy, so forced fights (Монстр/Штормяк) correctly suppress the payout. Kept the intentional "+20" joke log; JSON text untouched.

### M9. Котики "Кошачья засада" (Штормяк) eats half of *total* score
- JSON: "сожрёт половину очков, которые враг получил **пока на нём сидел** этот кусок кота". Code (`CP:3171-3183`): on the return win, victim loses `Floor(GetScore()/2)` — half of their **entire score**, regardless of when it was earned (no snapshot at deploy time exists).
- **Impact**: a late Storm return can wipe 20+ points instead of the earned-while-sat handful — swingiest single effect in the game.
- **Fix direction**: snapshot the victim's score at deploy (`KotikiAmbush`) and halve the delta.
- **Fixed:** 2026-07-03 (designer verdict БАГ) — added `AmbushClass.StormScoreSnapshot` (`Kotiki.cs:21`), captured at Storm deploy (`CP:3246-3248`) and reset on return (`CP:3184`); the return steal is now `Floor((currentScore − snapshot) / 2)`, i.e. half of what the victim earned while the cat sat, not half of their total score (`CP:3171-3186`). Non-positive delta steals nothing; the −1 Psyche on the win is unchanged.

### M10. Premade's anti-skip un-bans a round-10-banned Carry
- JSON: "Carry никогда не пропустит ход. **(кроме банов)**". Code (`CP:5777-5790`) clears *any* involuntary skip (`IsSkip && !ConfirmedSkip`) on the Carry — including Тигр's round-10 "Стримснайпят и банят" ban (`CP:4932-4939` sets exactly that state) and Школьник's brother-ban. The freed Carry may then act on round 10 despite being "banned" (other systems — targeting refusal, Тигр-топ suppression — still assume he's banned).
- **Fix direction**: skip the anti-skip when the skip source is a ban (e.g. check the ban passive + round, mirroring `CheckIfReady.cs:1281`).
- **Fixed:** 2026-07-03 (designer verdict БАГ "добавляй") — the Premade anti-skip (`CP:5788-5796`) now computes `markedIsBanned` (round 10 + "Стримснайпят и банят и банят и банят") and leaves a banned Carry skipped. Scoped to the canonical Тигр round-10 ban (the "ban" the description's "кроме банов" means); ordinary involuntary skips (Митсуки no-PC, АФКА) are still lifted.

## Minor

### m1. "Вампур_" typo kills a flavor Easter egg
- Before the fix, the condition now at `GameUpdateMess.cs:1505` checked `Name == "Вампур_"` (JSON: "Вампур") — the garlic level-up placeholder never showed.
- **Fixed:** 2026-07-03 (pre-approved string bug) — `Name == "Вампур_"` → `"Вампур"` (`GameUpdateMess.cs:1505`); removed `BAD-NAME|Вампур_|m1` from `tools/known-warnings.txt` (audit re-run: no reappearance).

### m2. "Vampyr Позорный" logic is commented out
- `GameReactions.cs:1001-1007` (level-up denial) disabled; only the phrase object remains. Remove or restore.
- **Fixed:** 2026-07-03 (designer verdict — Вампур не должен прокачивать статы; если качает — забрать) — **restored** the block (`GameReactions.cs:1001-1007`): a Вампур level-up sets `skillNumber = 0`, so the stat switch adds nothing (the point is still spent at `:1134`) and "Никаких статов для тебя" is logged. Вампур has the `Vampyr Позорный` passive (`characters.json:588`), so the check is live, not a GHOST. Gematophagia bites (a separate win-reward mechanic) are unaffected.

### m3. Young Gleb transform keeps `Name == "Глеб"` → three misfiring Name checks
- Transform (`GameReactions.cs:256-268`) deliberately doesn't set the name (mylorik's Акула transform at `CP:6095-6102` *does*). Consequences: `GameUpdateMess.cs:1214` "Понизить один из статов" caption never shows post-transform; `CheckIfReady.cs:436` AWDKA-trolling flavor for Молодой Глеб unreachable; `GameReactions.cs:1132` old-Gleb psyche-10 phrase can fire for the transformed character. (`GameStateMapper.cs:294` / `GameUpdateMess.cs:1596` guards are harmlessly always-true.)
- **Fixed:** 2026-07-03 — kept the deliberate design (the transform leaves Name as Глеб so prediction, bot AI and Geralt logic keep matching) and repointed the three cosmetic sites to the young form's Main Ирелия passive — the unique marker the level-up nerf already uses (verified single occurrence in characters.json; Глеб lacks it). The level-up caption (`GameUpdateMess.cs:1214`) and the AWDKA-troll line (`CheckIfReady.cs:436`) now show the young-form text when that passive is present; the sleeping-Gleb psyche-10 phrase (`GameReactions.cs:1132`) is suppressed for it. Added a warning comment at the transform (`GameReactions.cs:258`) not to uncomment the rename. The level-up *mechanic* was never affected — the nerf keys on the Main Ирелия passive, not the Name — so this was cosmetic only. The two always-true guards (`GameStateMapper.cs:294`, `GameUpdateMess.cs:1596`) were left as-is per the finding.

### m4. `PassivesClass.GlebSkip` declared as `bool … = new()`
- `PassivesClass.cs:91` — compiles to `false`; clearly unintended syntax.
- **Fixed:** 2026-07-03 — changed the initializer to `= false` (`PassivesClass.cs:91`). It was a copy-paste of the surrounding reference-type `= new()` lines; for a bool `new()` already yields `false`, so no behavior change — GlebSkip is a plain flag (set at `CP:511`, tested/reset at `CP:2674/2687`). Pure clarity fix.

### m5. Exploit rotation runs in games without Баг
- `GameClass.RollExploit` + `DoomsdayMachine.cs:73-76` rotate/count exploit state even when nobody can consume it. Harmless bookkeeping.
- **Fixed:** 2026-07-04 — `RollExploit` now early-returns when no Баг player is in the game (`ExploitPlayersList.Count == PlayersList.Count`, i.e. nobody holds the "Exploit" passive; `GameClass.cs:149-156`). The list is rebuilt on the draft path (`CheckIfReady.cs:1089-1093`) and the next-round pipeline re-rolls after sorting (`DoomsdayMachine.cs:1887`), so the gate stays correct for drafted Баг. No observable change (both the Discord "EXPLOIT N" flair and the web ExploitState were already viewer-gated on holding "Exploit"); the rotation just no longer flips flags nobody reads.

### m6. Dead legacy code catalogue
- `LolGod.cs` + `PassivesClass.LolGodUdyrList` — "Бог ЛоЛа" doesn't exist; the only live reference is an always-true guard inside Darksci's "Не повезло" (`CP:2880`).
- `Saldorum.cs` (single-L "Хохол" design) vs live `Salldorum.cs`: orphaned cases "Парень с сюрпризом" (`CP:902, 2161`), "Сало" (`CP:916, 2175`; `GameUpdateMess.cs:634`), "Ниндзя" (`CP:1488, 2194`) — those passive names exist in no character and are never added at runtime.
- `CraboRack.BokoBoole` (in `CraboRack.cs`, pre-deletion lines 16-19) — zero references.
- "Молодой Глеб" JSON entry has `Tier: -2` — excluded from the roll pool by `CharactersPull.GetRollableCharacters` (Tier ≥ −1, `CharactersPull.cs:44-51`); transform template only. Note the tier semantics (`CharactersPull.cs:29-33`): **Tier −1 = secret but rollable** — Sakura and Баг do roll for humans (range 40; bots never roll tier <4) while staying hidden from prediction menus. *(Corrected in verification — originally attributed to a range-0 roll.)*
- **Fixed:** 2026-07-04 — deleted: `LolGod.cs` and `Saldorum.cs` (whole files); the six orphaned GHOST cases (CP defense «Парень с сюрпризом»/«Сало», before-fight «Ниндзя», attack «Парень с сюрпризом»/«Сало»/«Ниндзя») and the «Сало» display case in `GameUpdateMess.cs`; the commented-out "LOL GOD, EXAMPLE" block (the "Бог ЛоЛа" BAD-NAME source) inside Darksci's «Не повезло»; dead state `PassivesClass.LolGodUdyrList`/`SaldorumKhokholList`/`SaldorumNinjaHidden` (live `SaldorumCorruptionCount` kept); dead phrases `SaldorumSurprise`/`SaldorumSalo`/`SaldorumNinja` (live `SaldorumChronicler` kept); `CraboRack.BokoBoole`. Removed the four `|m6` lines from `tools/known-warnings.txt` (audit re-run: clean). The Молодой Глеб tier note is informational — no change. In-code anchors above are historical (pre-deletion coordinates).

### m7. Stale comments (cosmetic)
- `CalculateRounds.cs:27` says TooGood sets "75 or 25" — code sets 70/30 (`:238, :249`).
- `CP:684` comment says tunnel escape is 33% — code rolls 50% (`:645`).
- **Fixed:** 2026-07-04 — the tunnel-escape comment now says 50% (`CP:682`). The `CalculateRounds.cs:27` half was already fixed in an earlier change-set (the comment reads "(sets 70 or 30)"); no code values touched, comments only.

### m8. Толя "Подсчет" recharge is 4–5 rounds, description says 2–3
- Initial cooldown *is* 2–3 (`PassivesClass.cs:31`), but after each use `Cooldown = Random(4,5)` (`CP:1130`), decremented once per round (`CP:6061-6070`). Net: ~2 uses per game instead of ~3.

### m9. Итачи Цукуеми recharge is 4 rounds, description says 2
- On activation `ChargeCounter = -2` (`CP:3011`); +1 per round (`CP:4196-4202`) → 4 rounds to full. Initial charge (0→2) matches the described 2.

### m10. Francie Хим.оружие ignores enemy-difficulty scaling
- Design note (the_boys.txt): "(normal +1, toogood +1, toostronk +1; если мы ту-гуд/стронк = +0) × прокачки". Code (`CP:3293-3302`): flat `chemLevel` bonus, zeroed when TheBoys were TooGood/TooStronk vs the victim. The "harder enemy pays more" half is missing.
- **Fixed:** 2026-07-03 (designer verdict БАГ "добавляй") — the bonus is now `chemLevel × (1 + (enemy TooGood for TheBoys ? 1 : 0) + (enemy TooStronk ? 1 : 0))` (`CP:3296-3310`), read from the attacker's `FightEnemyWasTooGood/Stronk` flags (= "my enemy was too good/stronk"). Normal enemy ×1, harder enemy ×2 (the TooGood/TooStronk tiers are set in exclusive threshold branches in `CalculateRounds`, so a win vs either pays double). The existing "+0 if TheBoys was TooGood/TooStronk vs the victim" gate is unchanged.

### m11. Ziggurat costs differ from the design note
- Code (`CP:6140-6185`): requires ≥1 of each type **and score ≥ 3** (undocumented gate), costs −3 bonus + a *permanent* −1 Worker deduction. Design note: "умирает 1 Трудяга (т.е. если каждый 9й — Трудяга, то умирает 9 гоблинов)" — i.e. population loss, not a permanent worker-slot loss. Current implementation is milder early, harsher late.
- **Fixed:** 2026-07-03 (designer verdict: build needs **>3** points, and exactly **−1 Трудяга** is correct — description unchanged) — the score gate `GetScore() < 3` became `<= 3` (`CP`), so the build now requires strictly more than 3 points; the −1 permanent Worker deduction was already the intended behavior and is kept. Documented the >3 gate in CHARACTERS.md (not in the player-facing text, by design).

### m12. Saitama's round-1 "serious targets" are effectively arbitrary
- SeriousTargets = top-2 by `GetSkill()` (`CP:284-293`), which at game start is 0 for almost everyone → stable-sort picks the first two in list order. Recomputed properly from the end of round 1 (`CP:4010-4019`). Also note "боевая мощь" = skill only (stats ignored) — Кратос-style stat monsters are never "serious".

### m13. HardKitty's opening −30 is score, logged as Мораль
- `CP:201-203`: `HardKittyMinus(-30)` lowers **Score** by 30 (bypassing the floor), while the personal log says "Никому не нужен: -30 *Морали*". One of the two is wrong; players reading the log get misdirected.

### m14. Butcher sup marks only exist from round 2
- Marks are assigned in `HandleNextRoundAfterSorting` (`CP:5862-5896`), which first runs at the end of round 1 — no sups (not even superheroes) during round 1. Probably fine; worth one line in the passive text if intended.

### m15. Salldorum's history rewrite ignores the round multiplier and the Еврей redirect
- Design note (`GameDesign.txt:549`): steal "(1 × множитель раунда)" from each winner, "Но следи за Евреями. Если они украли эти очки — то отнимается у евреев". Code (`WebGameService.cs:975-986`): flat −1/+1 bonus per winner, no multiplier (the comment even says "could scale"), no Jew redirection.
- **Fixed:** 2026-07-03 (designer verdict БАГ — "сделай как должно быть по описанию") — the steal became `1 × roundMultiplier(rewrittenRound)` (`roundNumber switch { <=4 => 1, <=9 => 2, _ => 4 }`) with the then-available co-winning-Еврей redirect.
- **Rework follow-up:** 2026-07-12 — the fight resolver now records the actual recipient(s) of every winning point against Salldorum after Jew and Штормяк redirects. The shared rewrite resolver uses that ledger, so an attacking Jew who lost but stole another winner's point—and multiple Jews who each received the duplicated point—are all debited exactly; repeat losses to the same winner remain deduplicated, while scoreless ally losses are explicitly ignored.

### m16. Геральт's Lambert fumble is 20%, design note says 10%
- `CP:4485` (`_rand.Luck(20)`, one-time) vs `GameDesign.txt:654` "10% Шанс". Also worth knowing: the meditation hint for human players calls the Anthropic Haiku API synchronously inside the round pipeline (`CP:4442-4470`) with a static fallback.
- **Fixed:** 2026-07-03 (designer verdict 10%) — `_rand.Luck(20)` → `_rand.Luck(10)` (`CP:4487`); BALANCE-CONSTANTS row updated. (`Luck(p)` with no range = `p >= rand(0,100)` ≈ p%.)

### m17. Dopa "Взгляд в будущее" also procs on blocks
- Proc condition (`CP:4240-4244`): either dual-target attacked the other **or either target blocked**. The description only promises the "attacked his next target" case. Lenient in Dopa's favor.
- **Fixed:** 2026-07-05 — removed the two block-based proc conditions; Vision now fires only when one of Dopa's two targets actually attacked the other (`CP:4227-4252`). The stale Фарм bot block heuristic was removed too; current Dopa targeting uses actual co-attacker plans (`BotsBehavior.cs:1739-1801`).

### m18. "Привет со дна" counts skip *events*, not skipping players
- `CP:3678`: bonus = `game.SkipPlayersThisRound` (incremented once per skipped **fight**, `DoomsdayMachine.cs:556` — two attackers into one skipper = 2) + count of blockers. Mildly inflated vs "когда кто-то пропускает ход".
- **Confirmed intended** 2026-07-04 (designer verdict «ОК — по событиям»: каждый сорванный бой = очко). No code change; exact per-event behavior documented in CHARACTERS.md.

### m22. Latin "Saitama" vs JSON "Сайтама" — dead "👑 King" flair
- Before the fix, the leaderboard condition now at `GameUpdateMess.cs:751` checked `Name == "Saitama"` while the character is named "Сайтама", so the current #1 never received Saitama's "👑 King" marker. Same bug family as C1/m1. *(Found by `tools/audit-passives.sh` on its first run.)*
- **Fixed:** 2026-07-03 (pre-approved string bug) — `Name == "Saitama"` → `"Сайтама"` (`GameUpdateMess.cs:751`); removed `BAD-NAME|Saitama|m22` from `tools/known-warnings.txt` (audit re-run: no reappearance).

### m23. Dopa's `dopa-attack-select` menu is dead UI — selections silently ignored
- Before the fix, the now-deleted GetDopaMenu built a second-action select with custom-id dopa-attack-select and a dead "Dopa" passive branch attached it in the game-buttons builder. The component dispatch switch had **no case for it** (`GameReactions.cs:157,417-421`) — a click would defer and do nothing.
- The working Макро second action flows through the regular attack/block handlers instead (`GameReactions.cs:730-744`, `GameReactions.cs:329-352`), so the menu is pure decoration that looks interactive. *(Found 2026-07-04 during the interface-docs audit; docs/DISCORD-INTERFACE.md §5.)*
- **Fix direction**: delete `GetDopaMenu` + its attach (Макро already works via `attack-select` and `block`), or route the custom-id into `HandleAttack`.
- **Fixed:** 2026-07-04 — deleted `GetDopaMenu` and its `case "Dopa":` attach from `GameUpdateMess.cs` (Макро's real second action already flows through the regular `attack-select`/`block` handlers). Verification correction to the finding: the menu never actually **rendered** — the attach switch iterates `passive.PassiveName` and no passive named "Dopa" exists in `characters.json` (the character's passives are Макро/Пассивный импакт/…), nor is one added at runtime, so the `case "Dopa":` was itself dead and the select was unreachable UI rather than silently-ignored UI. Removed the `dopa-attack-select` row + §11 quirk line from `docs/DISCORD-INTERFACE.md`; downstream `GameUpdateMess.cs` anchors in the docs re-pointed (−49/−53 lines).

### m24. ARAM pick phase has no web UI (hub methods exist, screen doesn't)
- The backend and contract fully support web ARAM picks: `AramReroll`/`AramConfirm` on the hub (`GameHub.cs:332-352`) and REST (`GameController.cs:159-177`), `isAramPickPhase` + reroll counters serialized (`GameStateMapper.cs:947-965`), store wrappers wired (`game.ts:439-447`) — but **no Vue component calls them**; Game.vue's phase branches cover only the Draft overlay.
- During an ARAM game a web-preferring player sees only the waiting screen and must reroll/confirm from the Discord ARAM page (`GameUpdateMess.cs:1613-1637`). *(Found 2026-07-04 during the interface-docs audit; docs/WEB-CLIENT.md §13.)*
- **Fix direction**: an ARAM overlay in Game.vue mirroring the draft overlay (buttons → the store's `aramReroll` slots 1-5 / `aramConfirm`), or suppress the web-link DM during ARAM picks.

### m25. audit-passives.sh truncated PASSIVE-MAP.md when killed by the hook timeout
- The script wrote the report **directly into `docs/PASSIVE-MAP.md` while generating it**, and its per-passive greps took ~90 s on a `/mnt/*` (WSL2 9P) checkout — longer than the 60 s `PostToolUse` hook timeout (`.claude/settings.json`; the hook re-runs the audit after every edit to a passive-bearing file, `tools/hook-post-edit.sh`). A killed hook run left a clean-looking but truncated map (committed history oscillates: 171 → 119 → 171 → 118 table rows across `a17d86e`/`c6762f0`/`ce6a772`/`e0f7384`) and silently dropped the GHOST/BAD-NAME sections — which also broke the hook's own new-warning diff on the next edit. *(Found 2026-07-04 when the user noticed ~53 rows vanish from the map.)*
- **Fixed:** 2026-07-04 — two changes to `tools/audit-passives.sh`: (1) **atomic write** — the report is generated into `$OUT.tmp.$$` and `mv`-ed over the map at the end, so a killed run can never leave a partial file; (2) **single-pass indexing** — owners (one `jq`), CP case counts (one `grep -oP | uniq -c`) and cross-file refs (one `grep -HoF -f patterns` over the code list) are pre-computed into assoc arrays instead of ~350 per-passive greps. Runtime 86 s → **2.3 s**; output verified byte-identical to the last complete map (`ce6a772`) modulo the m6 deletions, and deterministic across runs.

### m27. Butcher's sup marks leak to their targets but are missing from the owner's leaderboard
- The owner DTO carried the marked players' nicknames in `TheBoysStateDto.SupMarks` and rendered them inside TheBoys' own left-side widget, rather than marking the corresponding enemy rows. Conversely, `TheBoysSupOnMe` was serialized onto a marked enemy's own private card and explicitly told them `Помечен как «суп»`. These removed DTO/UI fields are retained here as the pre-fix finding, not as current code references.
- **Impact**: the owner has to cross-reference a nickname list instead of seeing the table target, while enemies receive hidden Butcher targeting information they should not know.
- **Fix direction**: expose a viewer-scoped boolean on each marked enemy row only when the requester is TheBoys; render a distinct sup icon on that row; remove the owner nickname chips and the target-facing sup DTO/widget.
- **Fixed:** 2026-07-10 — added viewer-scoped `PlayerDto.IsTheBoysSupTarget`, populated only when the requester owns `Пацаны` (`GameStateDto.cs:103-111`; `GameStateMapper.cs:137-145,345-351`). Web enemy rows render a 🦸 `СУПЕР` badge (`PlayerCard.vue:738-741,4242-4260`); Discord appends 🦸 to those leaderboard rows only in TheBoys' DM (`GameUpdateMess.cs:697-719`). Removed the obsolete SupMarks nickname chips and TheBoysSupOnMe DTO/type/widget, so targets and spectators receive no mark disclosure.

### m21. `SecureRandom` is not secure (naming hazard)
- `Helpers/SecureRandom.cs:25-45`: the crypto implementation is commented out; the service is a plain `System.Random` wrapper. Fine for a game, but the name misleads — and `PassivesClass` carries a private copy that *does* use `RandomNumberGenerator` (`PassivesClass.cs:280-299`), so trigger schedules are crypto-random while combat rolls aren't. Unify or rename.
- **Fixed:** 2026-07-04 (user chose **unify**) — one RNG for the whole game: `SecureRandom` gained a static core `Next(min,max)` wrapping `RandomNumberGenerator.GetInt32` (thread-safe — the old shared `System.Random` instance wasn't); the instance `Random` and `Luck` delegate to it, and the `PassivesClass` private crypto copy was deleted (ctor Толя-cooldown roll + `GetWhenToTrigger` now call `SecureRandom.Next`). All call-site semantics preserved exactly: inclusive max, the `Random(n, n−1) → n` edge (relied on by `GetWhenToTrigger(…, range 0)`), Luck's 0–100 roll. Out of scope, documented in ARCHITECTURE §9: the handful of direct `new Random()`/`Random.Shared` sites (exclusive-max semantics; converting them risks off-by-ones for no behavioral gain). In-code anchors above are historical.

## Design questions

### D1. Darksci can dodge "Дизмораль" by hoarding the round-9 level-up
- The −5 Psyche fires only inside `GetLvlUp` while `RoundNo == 9` (`GameReactions.cs:1233-1238`); saving the point until round 10 skips it (and the psyche-0 skip check). Bots always spend immediately. Intended tech or loophole?
- **Resolved 2026-07-03 (designer chose consistency, reversing the earlier «ОК»)**: the hoard was only ever possible on the WebUI via the level-up banking bug (M15) — on Discord the forced level-up page and the round-end auto-move both spend the point in round 9. M15's general web gate closes it, so Darksci now eats the −5 on both platforms.

### D2. Goblin Ziggurat can duplicate "Еврей" (and other Standalone passives)
- LeCrisp's "Еврей" is `Standalone: true` (`characters.json:140`), so Goblins can learn it (`CP:6169-6179`) despite the roll-time LeCrisp/Толя exclusivity (`StartGameLogic.cs:204-222`). `HandleJews` supports multiple jews (`CP:6687-6765`), each earning +1 while the victim's point is suppressed once. Verify which Standalone passives are safe to copy (full matrix in the Phase-3 audit).
- **Fixed:** 2026-07-03 (designer verdict ЗАПРЕТИТЬ) — the Ziggurat copy filter (`CP`, `standalonePassives` where-clause) now excludes `PassiveName == "Еврей"`, so Goblins can never learn it. Other `Standalone` copies are left as-is per D10.

### D3. Sakura's "Одна из трех" is a narrative win only
- `CheckIfReady.cs:505-517`: top-3 Sakura is declared `playerWhoWon` (logs, phrases; ZBS-100 only if scores tie) but keeps her real place for stats/mastery/TotalWins. Note she *is* a rollable secret character (Tier −1, m6) that nobody can predict — the soft win may be the intended compensation; confirm.
- **Fixed:** 2026-07-03 (designer verdict: place stays by fact, stats & rewards as 1st place) — the payout loop now computes a per-player `rewardPlace` = 1 for the top-3 `top3Player` (Sakura) else the real place, and keys TotalWins, mastery, ZBS, the top-2 loot box, and per-character Wins off it (`CheckIfReady.cs:642-739`). Her `GetPlaceAtLeaderBoard()` and MatchHistory record her real finish. The actual 1st-place player is unaffected (still gets their 1st-place payouts).

### D4. Passives whose logic keys on character Name, not the passive
- "Булинг": DeepList's "Стёб" spares LeCrisp by name (`CP:2581-2599`); `HandleJews` skips stealing from DeepList by name (`CP:6714-6718`).
- "Го играть": the block/skip bypass vs friends is implemented inside "Заводить друзей" (`CP:1258-1269`) — the passive named "Го играть" has zero references of its own.
- "lvl-мяк": the +1-Justice level-up is `Name == "Котики"` (`GameReactions.cs:904-911`).
- All three work today but break silently on rename/transfer; consider keying on the passive names. (Also the inverse hazard: transferred Standalone passives *do* dispatch for new holders — e.g. Ziggurat copies.)

### D5. "2kxaoc" exists only to stay visible
- Its only special handling: `GameUpdateMess.cs:798-811` masks *other players'* passive names in the stats display ("Неизвестно"/"❓ …"), and "2kxaoc" is one of four names **exempt from masking** (with Запах мусора, Чернильная завеса, Еврей) — the meme is deliberately left readable. No gameplay effect; confirm none is intended. *(Corrected in verification — the original finding described this backwards.)*

### D6. Вампуризм copies Justice instead of draining it
- "подсасывает себе **всю** Справедливость цели" — code adds the target's current Justice to Вампур's next-round buffer (`CP:1907-1911`) but never removes it from the target (contrast: Kimiko's Живое Оружие, which drains it; Близнец was changed in M26 to copy without draining). Confirm copy-vs-drain.

### D7. External stat changes on Стая Гоблинов are overwritten every round
- `CP:6120-6122` re-`Set`s Str/Int/Psyche from population each round end — debuffs like Спартанец's −1 Str vanish; Speed debuffs persist (Speed isn't population-driven). Inherent to the population design; document or special-case.
- **Fixed:** 2026-07-03 (designer verdict БАГ — external debuffs should persist) — added `GoblinPopulationClass.LastApplied{Str,Int,Psyche}Base` and a shared `ApplyGoblinPopulationStats` helper (`CharacterPassives.cs`) used at both the before-first-round init and the end-of-round recompute. It sets each stat to `populationBase + externalDelta`, where `externalDelta = currentStat − lastAppliedBase` (0 on the first run), so external Str/Int/Psyche changes now carry across the recompute like Speed already did.

### D8. "Пейзаж конца света" +7 очков is round-multiplied ×4
- The non-pawn reward for attacking Монстр on round 10 is +7 **regular** points (`CP:4406`) — committed with the round-10 ×4 multiplier = effectively **+28** (+10 bonus on top). If "+7 очков" was meant literally, use bonus points.

### D9. Premade copies the Carry's fight-moral instead of transferring it
- `CP:2433-2436`: the Support `AddMoral(carryMoral)` while the Carry keeps theirs. JSON "Добываемая в боях Мораль так же передается" reads as a transfer. Same copy-vs-drain question as Вампуризм (D6).

## Phase 3 — cross-character interactions

### M11. Шэн and Штормяк forced attacks ignore the round-10 Тигр ban
- Монстр's no-escape has an explicit ban carve-out (`CheckIfReady.cs:1277-1282`). The other two forced-fight sources don't:
  - **Шэн** (`CheckIfReady.cs:1195-1210`) forces everyone below the position to attack Salldorum — a round-10-banned Тигр (IsSkip, stats nuked) gets Salldorum added to `WhoToAttackThisTurn`, and the fight loop processes forced fights even for skipping players (`DoomsdayMachine.cs:398-412`) — the "banned, can't act, can't be targeted" promise breaks (0-stat Тигр is forced to fight).
  - **Штормяк taunt** (`CheckIfReady.cs:1228-1262`) excludes dead players but not the banned Тигр — he can be provoked into attacking Котики on round 10.
- **Fix direction**: reuse the Монстр carve-out condition in both sites (and consider dead-player checks ✓ already present).
- **Fixed:** 2026-07-03 (designer verdict БАГ "если нет исключения, Тигр остаётся в бане") — mirrored the Монстр carve-out `!(game.RoundNo == 10 && …Passive.Any(x => x.PassiveName == "Стримснайпят и банят и банят и банят"))` into the Шэн below-position pull (`CheckIfReady.cs:1224`) and the Штормяк taunt eligible-targets filter (`CheckIfReady.cs:1258`), so a round-10-banned Тигр is no longer forced to fight.
- **Rework follow-up:** 2026-07-12 — Шэн's pull now follows its attack-bound leap, but `Salldorum.ResolveShenDashes` retains the same explicit Тигр exclusion; Штормяк retains its readiness-stage carve-out.

### M12. Монстр's apocalypse can kill Стая Гоблинов
- Every other kill source has an explicit goblin immunity: Кира's note (`CP:4086`), L-arrest (`CP:4775`), Кратос (`CP:1725`). "Пейзаж конца света" pawn deaths (`CP:4381-4392`) have **no** goblin check — Goblins guessed by Монстр become pawns and die on round 10, contradicting `GameDesign.txt:509` "Гоблинов нельзя Убить механикой 'убийства'".
- **Fix direction**: `if (pawn.GameCharacter.Name == "Стая Гоблинов") continue;` in the pawn loop (or block goblins from becoming pawns).

### D10. Ziggurat-copyable (`Standalone`) passives — a risk inventory
The Ziggurat copies any `Standalone: true` passive from the last attacked enemy (`CP:6167-6179`). Current inventory by behavior when a Goblin holds them:
- **Fully functional** (probably intended): Одиночество, Месть, Импакт, Еврей (see D2), Обучение, Лучше с двумя, 3-0 обоссан, Запах мусора, Я пытаюсь!, Произошел троллинг (⚠ also inherits the M3 forced-last!), Неуязвимость, Привет со дна, Лежит на дне, Ничего не понимает, Им это не понравится, Гематофагия, Панцирь, Болевой порог, Хождение боком, Питается водорослями, Оборотень, Безжалостный охотник, Клинки хаоса, Вороны, Изанаги (2 free auto-win defenses!), Аматерасу, Сомнительная тактика (huge self-nerf — must lose first fight vs everyone).
- **Self-brick**: **Булькает** — a Goblin who learns it loses all Мораль/Skill gains including the Ziggurat's own +5 Мораль (`CharacterClass.cs:963, 1010, 1125`). Funny, probably not intended to be learnable.
- **Dead copies** (game-start-only or Name-gated hooks): Лысина, Первая кровь, Похищение души (init-only — no effect when learned mid-game); Ведьмачьи заказы (every case gated `Name == "Геральт"`).
- **Fix direction**: maintain an explicit copyable-whitelist, or at least exclude Булькает and the dead copies.

### D11. Цукуеми × Чернильная завеса double-charges the same point
- If a player beats Осьминожка (ink fake-win: they get +1 now, owe it back at round 11) while under Итачи's Цукуеми, Итачи *also* copies that +1 at end of round and deducts it again at game end (`CP:4166-4194`, `CheckIfReady.cs:385-402`) — the victim repays the same point twice (once to Octopus's restore, once to Итачи). Rare, but both effects are "secretly repaid later" designs that don't know about each other.
- **Fixed:** 2026-07-03 (designer verdict — Итачи и Осьминожка each get their point, victim loses it once; duplicate for two receivers) — the round-11 ink restore (`CP:4863-4880`) now **skips its victim-debit** when that victim is in any Itachi's `ItachiTsukuyomi.StolenFromPlayers` ledger (the Цукуеми deduction at `CheckIfReady.cs:381` charges them instead). Octopus still applies its own `+N` credit (a positive RealScore entry), so the point is duplicated for both receivers while the victim pays once. Best-effort: the ledger is per-victim not per-round, so a victim stolen-from in a *different* round than the Octopus beat would also be skipped — a rare double-edge.

### Phase-3 checks that passed (selection)
Монстр no-escape spares pickle-Рика (he has neither IsBlock nor IsSkip); dead players are excluded from every forced-attack pool; Geralt's contract injection skips while he blocks/skips; Тигр-топ/Portal-Gun/Шэн/Storm-bite/Drops all respect the Ziggurat lock; the Глеб/Молодой-Глеб tea skip spares a charged Portal Gun; Premade anti-skip doesn't touch pickle-Рика; Котики are immune to a *transferred* Storm's taunt and a transferred Минька/Штормяк won't buff against its owner; PointFunnel points bypass Еврей theft (funnel copies only `AddWinPoints`); Октопус's ink correctly debits Евреи who stole the point.

## Phase 4 — plumbing (web/state layers)

### m19. Итачи's Crows and Izanagi are invisible in the web UI
- Only Tsukuyomi state is mapped (`PlayerDto.TsukuyomiState`, `GameStateMapper.cs:323`); crow counts per enemy and remaining Izanagi charges exist only in backend state. Recently-added character with the least UI coverage; every comparable kit (Goblins, TheBoys, Геральт) has a full widget.

### m20. Geralt's "Чеканная монета" demand economy is entirely undocumented
- A full hidden system: post-round demand/advance buttons, invoice totals, a Displeasure ledger, +2 regular per advance, and **death by pitchforks with −500 at Displeasure ≥ 11** (`CP:4543-4580`). Neither `characters.json` nor `GameDesign.txt` mentions displeasure or the death. (The design note's *other* hidden Geralt mechanics: "психует when a contract holder is killed" — **not implemented** anywhere; "dies on place 6" — implemented as a log line only, `CheckIfReady.cs:270-276`.)
- Also of note: the meditation hint calls the Anthropic API synchronously inside the round pipeline (`CP:4442-4470`).
- **Fixed:** 2026-07-05 (documentation-only) — the full demand economy is now documented in `docs/CHARACTERS.md` (Геральт's «Чеканная монета» entry): the two web/bot-only billings (immediate «За прошлый» + deferred «За следующий» advance), the `CalculateInvoice` coin/Displeasure tiers, and the pitchfork death checked in **both** the web handler (`WebGameService.cs:695-703`) and the end-of-round advance resolution (`CP:4572-4579`), plus the place-6 pitchfork *log-only* line. Web plumbing was already covered (WEB-BACKEND.md hub/service rows, WEB-CLIENT.md widget rows, BALANCE-CONSTANTS row, INTERACTION-MATRIX kill-source, GAME-DESIGN end-game). **Two design-note mechanics were deliberately left unimplemented and flagged for a designer decision, not built here:** "психует when a contract holder is killed" (absent anywhere) and "dies on place 6" (log line only). `characters.json`/`GameDesign.txt` are the designer's surface — not edited (CLAUDE.md).

### m28. Final Mitsuki/Осьминожка point debits are absent from the web score feed
- `CombineRoundScoreAndGameScore` snapshots `ScoreEntries` into `PreviousRoundScoreEntries` before the round-11 passive hooks (`InGameStatusClass.cs:238-321`). `Запах мусора` and Осьминожка's `Чернильная завеса` repayment then call `AddBonusPoints(-N)` after that snapshot (`CP:6354-6374,5142-5167`). The score itself changes, but `MapStatus` used only `PreviousRoundScoreEntries`, so the final web combo showed no negative row.
- **Fixed:** 2026-07-10, tightened 2026-07-11 — the finished `ScoreBreakdown` now concatenates still-current **awarded bonus** entries after `PreviousRoundScoreEntries` (`GameStateMapper.cs:1106-1126`). Pending regular entries belong to a never-played next round and are excluded. In-progress rounds keep the old snapshot-only timing, and the Vue feed renders the signed actual/multiplied value (`PlayerCard.vue:1768-1825`).

### M24. Replay snapshots attach previous fights to next-round state
- **Intended:** replay round R must combine the actions/stats locked before round R combat, round R's own fight/log stream, and the settled score/place from round R. Later `HandleNextRound` effects must not appear between those fights.
- **Actual before the fix:** the boundary snapshot was taken before the previous `WebFightLog` was cleared, then paired that old fight list with a freshly mapped current player state. The game-end capture appended the round-11 boundary as another apparent combat round. The client compensated only by taking the previous snapshot's character while retaining the current snapshot's status. Thus Тигр's round-10 ban and Darksci's next-round tilt/skip could appear amid round-9 fights, while round 1 was empty and the final result appeared as round 11.
- **Impact:** the replay falsely suggested that bans/skips interrupted already-submitted attacks and that combat was resolving against different turn states. This was display/persistence desynchronization; the live fight loop itself still locked the round's actions before resolving fights in leaderboard order (`CheckIfReady.cs:1089-1422`; `DoomsdayMachine.cs:201-205`).
- **Fixed:** 2026-07-11 — replay format v2 stores explicit `PreFightPlayers`, combat logs and settled `Players` for the same logical round, plus separate game-end log suffixes (`ReplayDto.cs:9-76`). `BeginRound` runs only after the old fight log is cleared; `CaptureRoundResult` freezes fights/global logs before `RoundNo++`; after next-round score effects and sorting, `FinalizeRound` remaps score, place, death and breakdown together while the fight-facing state stays frozen (`ReplayService.cs:35-104,301-342`; `DoomsdayMachine.cs:201-205`; transition `DoomsdayMachine.cs:1622-1627`; finalize `DoomsdayMachine.cs:1887-1888`). Final settlement refreshes that existing last round rather than appending a fake round 11, and exposes only log text added after the round-11 setup baseline, so Darksci tilt cannot leak back into round 10 (`ReplayService.cs:106-144,352-366`; `CheckIfReady.cs:823-831`). The Vue adapter keeps old raw URL round keys working while pairing each legacy result boundary with its predecessor; v2 uses real round numbers and explicit settlement deltas (`replay.ts:21-120,133-268`). Historical final raw log buffers remain visible for legacy files because they cannot be split after the fact. Regression fixtures cover legacy deep links/logs, v2 numbering, post-setup score/death, skip isolation and final personal/global settlement (`replay.spec.ts:66-223`).

### Plumbing checks that passed
Every remaining `PassiveAbilityStatesDto` member is mapped and rendered (no dead DTO/TS fields); mapper `case` strings all exist in `characters.json`; cross-character target marks are deliberately absent from the affected player's DTO and remain visible only to the passive owner (or explicitly public by design); `Баг` state rides on `PlayerDto` (ExploitState) by design.

## Verified-consistent highlights (no finding)

Worth stating because they're easy to suspect: Francie's final-turn contract win is settled in the per-fight hook and clears `OrderTarget` before the round-transition expiry/new-target branch; the display counter is no longer an eligibility gate, and any still-active target at the boundary always incurs its −1 (`CP:3554-3567,6114-6135`). Saitama's Неприметность deferral/reclaim matches the recent fix note exactly (incl. the manual round-10 moral flush, `CP:4840-4847`); Rick's pickle/portal player-control flow matches rick_update (bot never takes over, pickle stays attackable, gun charge music one-shot); the Тигр round-10 ban is respected by targeting, Монстр forced attacks and the Тигр-топ swap; Кира's +2/+4(L) numbers, 25-Мораль eyes cost, L-arrest from round 8 and −500 all match; Goblin growth/death percentages match the goblins commit (death is 10+0.5R²/3%, the *older* design note's 1·R²/3 was rebalanced); Ziggurat Standalone-only learning with duplicate protection; Выгодная сделка pays both the +1 bonus per deal and +5 Мораль per deal; Октопус's ink ledger correctly redirects debits to Евреи who stole the point; the Глеб-tea skip spares a charged Portal Gun ("ничто не помешает").

## Phase 5 — simulation harness & bot robustness

### M13. Discord service-channel debug calls freeze headless sims and mask the real exception
- Four diagnostic Discord-channel calls in the per-tick hot path were **not** null-guarded. In headless simulation (and during a Discord reconnect) they could throw and freeze the round. Current guarded sites are the two bot fallbacks (`BotsBehavior.cs:3284-3303`), the attack-handler catch (`HandleBotAttack` entry `BotsBehavior.cs:770`; log + `SimErrorSink` at `BotsBehavior.cs:3343-3346` before the guarded send), and the ready-check fallback (`CheckIfReady.cs:1313-1326`).
- **Impact**: ~1/1000 sim games froze (sweep-20260702-191714 hit 5 batches) and the true exception (M14) was invisible — every recorded error read as the generic `CheckIfReady.cs:1272` NRE. In production a Discord reconnect at the wrong moment can freeze a live game the same way until reconnect.
- **Fixed:** 2026-07-03 — added null-safe `Global.TrySendServiceMessage(string)`, routed the bot fallbacks through it (`BotsBehavior.cs:3284-3303`), and reordered the catch to `_logs.Critical` + `SimErrorSink?.Invoke(...)` before the guarded send (`BotsBehavior.cs:3343-3346`). Verified: previously-freezing line-up 0 stuck; 5000-game natural sim 0 errors/0 stuck.

### M14. Bot `HandleBotAttack` throws IndexOutOfRange when it has no valid targets
- The random fallback builds `players = allTargets.ToList()` (`BotsBehavior.cs:3300`). When the pool was empty after deaths/late-round filters (`:794-819`), indexing it threw an argument-out-of-range exception. Кира-correlated; observed rounds 8–10.
- **Impact**: the bot's turn crashes. Post-M13 it is recorded as a per-game sim error; in production the bot fails its action and auto-blocks (`CheckIfReady.cs:1264` fallback) plus debug-channel spam.
- **Fixed:** 2026-07-03 — `if (players.Count == 0)` blocks, resets preferences and returns (`BotsBehavior.cs:3300-3309`). Verified: Kira line-up 500 games → 0 errors/0 stuck. (The early `allTargets.First()` remains round-<5 guarded.) **See M16** — its sibling `Братишка` branch (`BotsBehavior.cs:2767-2783`) runs earlier.

### M15. WebUI let players bank level-up points instead of spending before continuing
- On Discord a granted level-up (rounds 3/5/7/9, `DoomsdayMachine.cs:1438-1442`) flips `Status.MoveListPage` to 3, so only the level-up menu renders. The web action handlers originally set readiness regardless of `LvlUpPoints`; a web player could act while carrying points into a later round. Bot/auto-move spending occurs before action at `BotsBehavior.cs:110-128` (called by `CheckIfReady.cs:1183-1234`).
- **Impact**: platform inconsistency and a real advantage — banking points to dump on demand, and dodging the round-9 Дизмораль −5 Psyche (see D1). Discord-impossible; WebUI-only.
- **Fixed:** 2026-07-03 — generalized the `Main Ирелия` guard into `WebGameService.LevelUpGate` (blocks any `LvlUpPoints > 0` from the four turn-ending actions; Ирелия keeps "Риоты не прощают, нерфа не избежать", everyone else gets "Остались очки прокачки — потрать их!"). Mirrored client-side: `store/game.ts` adds a `mustSpendLevelUp` computed + early-returns in `attack/block/autoMove/confirmSkip`; `pages/Game.vue` disables Block/Auto/Skip, tightens the Leaderboard `:can-attack`, and shows the same prompt. No soft-lock: every character always has an enabled level-up button in `PlayerCard.vue` while points remain, and the round-end auto-move force-spends any leftover.

### M16. Bot `HandleBotAttack` throws "Sequence contains no elements" in the Братишка per-character switch on an empty target pool
- The `Братишка` block branch (`BotsBehavior.cs:2739`) runs `allTargets.Min(...Justice...)` (`:2749`). When the late-round filters leave no targets, that used to throw before M14's fallback guard. Кира-correlated; all three failures in the cited sweep were round 10 with Братишка + Тигр + Кира.
- **Impact**: the Братишка bot's turn crashes on round 10 when no legal target remains. Post-M13 it is recorded as a per-game sim error (3/100300 in the sweep); in production the bot fails its action and auto-blocks (`CheckIfReady.cs:1264` fallback).
- **Fixed:** 2026-07-03 — wrapped the Justice-min nudge in `if (allTargets.Count > 0)` (`BotsBehavior.cs:2746-2756`); with no targets the ordinary block path handles the turn. The early `:1280` use is round-<5 guarded and the later checks use `.Any()`.

### M17. СуперМудень does not implement its x2 / resist-chain / disable contract
- Exact x2 is off by the untouched base term: fight Skill and base Harm use normal `1 + PokerCount`, but СуперМудень uses `1 + 2×PokerCount` (`CP:1626-1633`, `DoomsdayMachine.cs:962-971`). At Кочерга #4 that is ×5 → ×9, only ×1.8 instead of ×10. The resist chain then watches only Strength drops (`DoomsdayMachine.cs:973-985`), ignoring Intelligence/Psyche pool breaks, and ordinary Harm immunity interceptors still return before damage (`CharacterClass.cs:198-229`).
- The promised shutdown of other members also leaks: an already seeded virus spreads (`CP:2399-2430`) and pays at game end (`CheckIfReady.cs:359-379`), M.M.'s final multiplier and `IsCalm` immunity remain live (`CheckIfReady.cs:326-356`, `GamePlayerBridgeClass.cs:102-116`), a stale Francie order still excludes a sup candidate (`CP:6267-6282`), and Kimiko's next-round state can still emit a recovery phrase (`CP:6029-6047`).
- **Impact**: the advertised capstone is materially weaker in its own bonuses but simultaneously retains forbidden benefits from three disabled members; the missing Int/Psyche recursion changes the capstone's central late-game damage loop.
- **Fix direction**: double the full normal Skill/Harm result; have every actual Int/Strength/Psyche negative resist effect enqueue another Harm, with the designer's 50-drop-per-turn and zero-score stops; bypass enemy Harm immunities; gate every remaining Francie/Kimiko/M.M. path while SuperМудень is active.
- **Fixed:** 2026-07-10 — fight Skill and base Harm now double the complete `(1+poker)` result (Кочерга #4 ×5→×10 / 5→10); `LowerQualityResist` returns the number of actual Int/Str/Psyche negative effects, and the Super chain queues one Harm per effect recursively (`CP:1626-1636`; `DoomsdayMachine.cs:962-1018`; `CharacterClass.cs:198-345`). Super Harm bypasses Madara/Boole/Kimiko/Испанец, may keep deducting Drop points at place 6, stops at victim score 0, and shares a 50-Drop counter across the turn (`TheBoys.cs:60-67`; `CP:6273-6276`). Virus spread/end payout, final компромат, calm immunity, stale-order exclusion and Kimiko recovery are all gated; post-Super member level-ups are inert/consumed (`CP:2399-2436,5914-5972`; `CheckIfReady.cs:326-383`; `GamePlayerBridgeClass.cs:102-116`; `GameReactions.cs:975-981`).

### M18. Active Огурчик Рик can block/skip an incoming fight and lose his guaranteed win
- **Intended:** the player text says the pickle loses the ability to act but defeats everyone who attacks him (`characters.json:864`, Рик Санчез → «Огурчик Рик»); designer report 2026-07-10 confirms he must always accept the fight and win.
- **Actual:** block conversion cleared `IsBlock` only on the activation calculation (`DoomsdayMachine.cs:260-269` before this fix). An already-active pickle could retain a newly applied Block/Skip and hit the no-fight branch. Separately, the pickle's defensive `IsAbleToWin` override ran before attacker hooks (`CP:686-692`; `DM:472-486`), so a later attacker auto-win could set Rick unable to win and cancel the guarantee.
- **Impact:** attacks against the active pickle could be logged/resolved as Block/Skip, or resolve by ordinary fight math instead of a Rick win.
- **Fixed:** 2026-07-10 — active pickle state now strips Block and Skip at calculation start (`DoomsdayMachine.cs:257-274`). After both before-fight dispatchers, the fight loop authoritatively clears them again, disables the attacker win and explicitly enables Rick's win before the Block/Skip gates (`DoomsdayMachine.cs:547-576`).

### M19. Legacy achievements/loot boxes were mostly catalogue-shaped, not a reliable reward system
- **Actual before the fix:** the 69-entry achievement list mixed lifetime counters, generic thresholds, character facts and secrets, while many evaluator fields were never or inconsistently populated by live hooks. It had no unlock economy, no paired RU/EN metadata, no character relationships, and exposed semantic secret IDs. Loot opening used an unsynchronized check/decrement/credit with no pity, opening identity, acknowledgement or reconnect recovery; a sparse dismissible overlay was the entire reward moment.
- **Impact:** displayed requirements could be impossible, accidental or unrelated to the real mechanic; collection progress did not give players meaningful goals or rewards. Concurrent/retried loot calls were not transaction-safe, and a disconnect could lose the reveal even though account state had changed. The UI made both systems feel administrative rather than celebratory.
- **Fixed:** 2026-07-10 — replaced the list with 33 code-wired goals: 11 global mechanics, 15 character stories and 7 secret interactions, all audited by `tools/audit-achievements.sh`. Multi-step progress is explicitly best-single-match; unlocks pay rarity-based ZBS/boxes exactly once and survive in an acknowledgement queue (`AchievementClass.cs:182-403,412-560`). Locked secrets use hints plus opaque DTO IDs (`GameHub.cs:1645-1679`). Loot has published base odds/reward ranges, a 10-box Rare+ guarantee, cryptographic rolls, idempotent stored openings and explicit acknowledgement/reconnect recovery (`QuestClass.cs:799-923`; `GameHub.cs:649-899`). Economy settlement is account-locked and immediately persisted (`CheckIfReady.cs:722-796`; `UserAccounts.cs:113-139`; `UsersDataStorage.cs:28-80`). The Vue experience is a persistent reward hub + dedicated achievement center, staged loot reveal and accessible rarity-scaled celebration (`Lobby.vue:248-358`; `AchievementBoard.vue:196-430`; `AchievementPopup.vue:195-301`; `LootBox.vue:225-387`). Full spec: [ACHIEVEMENTS.md](ACHIEVEMENTS.md).

### M20. Achievement/loot economy is not one durable transaction boundary
- **Actual:** V2 loot/game-end paths use `lock (account)`, but draft spends and every Discord-store spend/refund mutate `ZbsPoints` outside that monitor (`WebGameService.cs:386-397`; `GameReactions.cs:519-525`; `StoreReactions.cs:203-329`). Storage catches write/replace failures and returns no status (`UsersDataStorage.cs:28-55`), so the hub emits a loot result even when its debit/reward was not persisted (`GameHub.cs:696-724`). Finished-game mapping also enumerates the same live achievement lists that another tab can acknowledge concurrently (`GameStateMapper.cs:174-206`; `GameHub.cs:813-843`).
- **Impact:** simultaneous reward/spend operations can lose ZBS updates; disk failure can show a reward that disappears after restart; a concurrent acknowledgement can abort the final SignalR broadcast with `InvalidOperationException`.
- **Fix direction:** serialize every account-economy mutation on the account monitor, make persistence success observable and keep retryable state on failure, and attach an immutable finish-time achievement snapshot to the match DTO path.
- **Fixed:** 2026-07-11 — paid web and Discord draft picks plus all six store transactions now validate/mutate under the shared account monitor, persist before confirmation, and restore their debit/chance snapshot on failure (`WebGameService.cs:368-463`; `GameReactions.cs:512-610`; `StoreReactions.cs:196-515`). `SaveAccount`/storage report atomic-replace success (`UserAccounts.cs:113-139`; `UsersDataStorage.cs:28-80`). V2 loot open and both acknowledgement families snapshot, save-before-send and rollback with a retryable hub error; legacy cached clients use one-shot compatibility, while recovered reveals receive current totals (`GameHub.cs:649-1018,1599-1643`). Final DTO mapping reads a detached achievement snapshot (`CheckIfReady.cs:751-754`; `AchievementClass.cs:592-608`); a failed game-end save is visible in critical logs and its settled memory remains for the periodic retry (`CheckIfReady.cs:790-796`).

### M21. Reward-modal and reconnect failure paths can hide or deadlock celebrations
- **Actual:** the global achievement popup and lobby loot dialog could mount together with independent background isolation and focus traps (hosts `App.vue:13-15,202-206`; `Lobby.vue:109-113,369-381`; isolation `AchievementPopup.vue:41-68`; `LootBox.vue:68-95`). A failed first SignalR `start()` left a non-null dead connection that blocked later retries (`signalr.ts:1055-1062,1170-1173`); reconnect reauthenticated but did not refresh quests/loot. Loot opening had no concurrent timeout/cancel/error state, and achievement dismissal cleared locally before acknowledgement succeeded (`Lobby.vue:51-68`; `LootBox.vue:167-181`; `game.ts:667-676`). The mandatory five-second game-over gate also ignored reduced-motion preference (`Game.vue:616-620,1481-1485`).
- **Impact:** the visible reward modal can become inert, a transient network failure can require a page reload, recovered inventory/reveals can stay stale, and a hung/failing acknowledgement can strand or prematurely erase the most important reward moment.
- **Fix direction:** centralize modal priority, reset failed connections and rehydrate both reward states after auth/reconnect, give opening/acknowledgement explicit retry UX, and honor reduced motion in the podium gate.
- **Fixed:** 2026-07-11 — the store now owns loot-modal priority and Lobby uses render-tick handoffs, so the two focus traps never coexist (`App.vue:13-17`; `Lobby.vue:43-146`). Failed/stale SignalR connections are disposed, login is visibly retryable, authenticated reward calls require session readiness, and every auth/re-auth hydrates quests plus achievements (`signalr.ts:1053-1262`; `game.ts:302-324`; `LoginProcess.vue:1-87`). Loot opening has a bounded idempotent retry/return state; achievement acknowledgement keeps the popup visible with busy/error retry until success (`game.ts:666-704,742-774`; `LootBox.vue:129-201,267-387`; `AchievementPopup.vue:95-166,195-301`). Reduced-motion users bypass the podium delay and animation (`Game.vue:609-632,3122-3132`).

### M22. Daily Quests were random-roster hostile, misleading and structurally repetitive
- **Actual:** the seven-goal pool could roll nested finish-1 + finish-3 + finish-5 goals, used process-dependent `string.GetHashCode()`, included “play 3 different characters” despite random/repeating rolls, and mixed one-game and five-game effort at the same advertised 25 ZBS. Worse, a completed card paid nothing unless all three finished: the UI promised each reward while partial completions expired unpaid. The lobby surface was a hardcoded-English list with no reset contract, receipts, agency, loading/error state or accessible progress semantics (pre-V2 implementation replaced by `QuestClass.cs:200-260,638-678`; UI replacement `Lobby.vue:365-375`).
- **Impact:** the board could be monotonous or effectively blocked by random character assignment; players were given false reward feedback and a missed/unlucky goal destroyed both the day and the old 500-ZBS seven-day cliff.
- **Fixed:** 2026-07-11 — Daily Quest V2 now assigns a fixed Anchor plus personalized SHA-256-stable Skirmish/Ambition lanes from 12 bilingual, character-neutral contracts (`QuestClass.cs:200-260,510-540`). Cards auto-pay 20/30/30 exactly once; any 2/3 pays +20 and advances streak/week, 3/3 adds one loot box, and any 5/7 completed days pays +100 (`QuestClass.cs:638-723`). One unfinished random card can be rerolled from persisted all-day metrics with save-before-send rollback (`QuestClass.cs:414-508`; `GameHub.cs:685-725`). Non-Madara Цукуеми views count only privacy-safe participation (`QuestClass.cs:380-408`). The inline Daily Quest board adds paired copy, reset countdown, receipts, free-swap announcement/focus recovery, clearly labelled weekly stamps, controlled icons, ARIA progress/loading/error states, mobile and reduced-motion support without adding a competing modal; in-flight refreshes queue one trailing request so UTC rollover is not dropped (`Lobby.vue:8,365-375`; `DailyQuestBoard.vue:1-604,1490-1621`; `game.ts:244-255,685-717`). `tools/audit-quests.sh` enforces the catalog and character neutrality. Full spec: [DAILY-QUESTS.md](DAILY-QUESTS.md).

### M23. Public replay links are hidden behind an unrelated account login
- **Intended:** replay files are shareable and the single-replay REST action is anonymous (`GameController.cs:179-186`); `Replay.vue` needs only that public payload through `replay.ts:164-186`.
- **Actual before the fix:** the app-wide login branch rendered for every unauthenticated route, including the `/replay/:gameId` route (`router.ts:42-47`; `App.vue:180-188`), so a recipient had to create a throwaway web account before the already-public replay UI could mount.
- **Impact:** shared replay URLs could not be watched anonymously, adding account friction to replay review and player support even though no protected data or authenticated operation was involved.
- **Fixed:** 2026-07-11 — `App.vue` now recognizes the named replay route as public and suppresses only the login overlay there (`App.vue:11-18,180-188`). All other routes retain the existing authentication screen; replay loading continues to use the anonymous endpoint unchanged.

### M25. Монстр's no-escape mark was tied to a resolved fight and could not represent two overlapping turns
- **Intended:** every declared Monster attack marks its target even if Block/Skip prevents the fight; that target must attack on its next two turns, and different victims can have overlapping windows.
- **Actual before the fix:** the mark was a single bool assigned from `HandleAttackAfterFight`, after both no-fight exits, and cleared wholesale by Monster's next-round case. A blocked/skipped target was never marked and the state could not encode an independent two-turn expiry.
- **Fixed:** 2026-07-11 — marking moved to the attack-before-fight dispatcher, ahead of the Block/Skip gates, and stores an absolute per-victim `MonsterNoEscapeUntilRound = max(old, current+2)` (`CP:1066-1073`; `PassivesClass.cs:280-285`). The readiness pass enforces the attack-only state through the expiry while preserving the round-10 Тигр ban carve-out (`CheckIfReady.cs:1382-1404`).

### M26. Близнец drained and summed every block attacker's Justice and also received generic block Justice
- **Intended:** Monster gets no ordinary +1 Justice from blocks. He copies, without draining, the highest Justice among enemies attacking that block and receives bonus points equal to that maximum.
- **Actual before the fix:** every blocked attacker was zeroed and their full Justice was added to Monster immediately; several attackers therefore summed, and the generic block path also buffered +1 Justice.
- **Fixed:** 2026-07-11 — the block path suppresses generic Justice for a Близнец holder (`DoomsdayMachine.cs:591-595`). A per-round maximum now sets Monster's live Justice without touching the attacker and awards only the incremental difference, so total bonus equals the maximum rather than the sum (`CP:978-1002,5936-5939`; state `PassivesClass.cs:283`).

### M27. Round-8 bot games could wait out the turn gate before challenging Мадара
- **Intended:** strict bots immediately accept the Клоны Сусано challenge on round 8; a game where Madara is the only human must not sit on the ordinary readiness delay.
- **Actual before the fix:** bot actions were chosen only after the human readiness/timer gate opened. Madara was unable to act, but bot attacks did not exist yet, so bot-heavy games could appear paused until timeout processing.
- **Fixed:** 2026-07-11 — before readiness counting, every live, non-skipping strict bot is idempotently committed to one ordinary attack on Madara (`Madara.cs:208-230`; `CheckIfReady.cs:1039`). Bot dispatch preserves that forced choice, and round-8 Madara is exempt from the ordinary 50-second readiness floor (`BotsBehavior.cs:94-105`; `CheckIfReady.cs:1166-1168`). Genuine forced skips remain authoritative.
- **Live-human follow-up fixed:** 2026-07-11 — after round 7 entered round 8, the post-calculation human prediction loop overwrote Madara's earlier `ConfirmedPredict = true` with `false`; all-bot simulations skipped that loop, while human Madara had no prediction control that could restore readiness (`CheckIfReady.cs:1477-1501`). The loop now preserves/reasserts Madara's full unable-to-act state, and the readiness precommit reasserts the same invariant every tick before counting (`Madara.cs:197-230`). Web Block/Auto Move/Change Mind/Skip are rejected server-side during the locked round, `isMyTurn` stays false, and the misleading Change button is hidden (`WebGameService.cs:502-613,1151-1163`; `game.ts:112-116`; `Game.vue:1220-1237`).
- **Superseded by designer rework, 2026-07-12:** bots must no longer auto-attack Madara or advance immediately. A live round 8 now waits 30 seconds; after spending level-ups, every strict bot receives only the exact Madara prediction and chooses its ordinary action through normal AI (`Madara.ForceRoundEightBotPrediction`; `BotsBehavior.HandleBotBehavior`; `CheckIfReady.TickAsync`). The Madara action lock and web gates from the follow-up remain.

### M28. Rumbling counted Eren's losses from the entire match instead of round 10
- **Intended:** the fewer-than-two-loss gate considers only resolved losses during round 10.
- **Actual before the fix:** every resolved loss from rounds 1–10 incremented the same `Eren.State.Losses` counter, so early-game losses could permanently disable Rumbling before its round existed.
- **Fixed:** 2026-07-11 — the loss hook now requires `game.RoundNo == 10`; the existing post-fight gate therefore reads only round-10 losses (`CP:2660-2665,3672-3680`). The owner DTO shape is unchanged; its loss field now has the intended round-10 meaning.

### M29. Шоковый щит's forced skip could be replaced by bot automation and still waited for confirmation
- **Intended:** the first attacker stopped by the one-use shield automatically skips their next turn.
- **Actual before the fix:** the next-round hook set `IsSkip` but left it unconfirmed; bot behavior did not honor attacker-side `IsSkip` and could immediately choose an attack, while a human could remain in the readiness wait.
- **Fixed:** 2026-07-11 — the shield clears queued attacks and auto-submits the skip (`IsReady`, `ConfirmedSkip`, `ConfirmedPredict`) when it lands (`CP:5173-5184`). Bot behavior now treats an existing forced skip as a complete action and returns without selecting an attack (`BotsBehavior.cs:84-93,3712-3720`).

### M30. DooM Rune penalties consumed base and externally earned stats without a floor
- **Intended:** Вознесение may take back only its granted +8 Int, and Маневры only its granted +5 Speed.
- **Actual before the fix:** every later loss/Harm applied another −1 forever, even after all Rune-granted points had already been removed.
- **Fixed:** 2026-07-11 — module activation seeds remaining-grant counters (`DoomGuy.cs:145-153,281-285`); loss/Harm penalties decrement and apply only while the matching counter is positive (`CP:2633-2643`; `CharacterClass.cs:213-221`). Base and later externally earned stats are no longer consumed by the Rune clawback.

### M31. BFG destroyed Step-3 randomness only for the primary attack
- **Intended:** every fight in the BFG shockwave that reaches the random stage must destroy that random stage exactly like the primary attack.
- **Actual before the fix:** the primary spent the charge and forced `pointsWined = 1`, but secondary direction-marked wave fights called ordinary `CalculateStep3`; a random loss could stop a branch.
- **Fixed:** 2026-07-11 — direction-marked BFG fights now use the same Step-3 override without trying to spend the already-consumed charge (`DoomsdayMachine.cs:793-826`). Decisive pre-random losses, blocks and skips still stop a branch, while random-stage wave fights win and continue through the existing queue logic (`:809-827`).

### M32. Bot Darksci can attack after round-9 Дизмораль sets his Psyche to 0 and skips him
- **Intended:** spending Darksci's mandatory round-9 level-up applies −5 Psyche; if that leaves him at 0 Psyche, «Да всё нахуй эту игру» clears his action and the turn remains skipped.
- **Actual before the fix:** `HandleBotBehavior` checked `IsSkip` only on entry, then spent the pending level-up (`BotsBehavior.cs:72-128`). `GetLvlUp` subsequently set `IsSkip` and cleared the target list (`GameReactions.cs:861-1353`), but bot processing continued into `HandleBotAttack`, which queued a new ordinary target. The fight loop deliberately processes a skipped player with a non-empty queue for legitimate forced fights (`DoomsdayMachine.cs:428-452`), so the illegal bot action resolved. Against Геральт, the contract injector rejected the skipped attacker and added no repeats (`DoomsdayMachine.cs:348-371`), producing the reported signature of exactly one real fight.
- **Impact:** bot and auto-moved Darksci can act on the round where 0 Psyche is supposed to remove their action; the same missing post-level-up gate can turn any future level-up-triggered forced skip into an ordinary bot attack.
- **Fix direction:** re-run the existing bot forced-skip completion immediately after pending level-ups, before any character sub-action or attack selection. Keep the fight-loop forced-action behavior unchanged.
- **Fixed:** 2026-07-11 — extracted the existing skip finalization into `CompleteForcedSkip` and invoke it both on bot entry and immediately after pending level-ups (`BotsBehavior.cs:84-128,3706-3715`). A Дизмораль-triggered Skip now returns before attack selection; later readiness-stage forced-fight injection remains unchanged.

### M33. Fight win/lose audio and the R3 random bar could drift away from the visual timeline
- **Expected:** each round result starts on its reveal; R3 shows modifiers, visibly rolls to its outcome, announces that settled result, and only then reveals the whole-fight result.
- **Actual before the fix:** every `playClipsBatched` awaited the slowest primary/percussion/vocal/meme fetch before starting any source, with no lateness bound (`sound.ts:286-313`). A cold optional layer therefore delayed an already-cached win/lose clip and could start it after later visual steps. The 4.0.6 fight-layout extraction also collapsed R3 modifiers and roll into one 800 ms step, ran the needle for only 500–700 ms and announced the R3 result when the roll began rather than when it settled (`FightAnimation.vue`, pre-fix step/sound/needle blocks).
- **Impact:** the issue appears intermittent after several fights because each result sequence and random decorative layer has its own cache entry; on a cold combination, several voices can arrive late/out of order. R3 itself ends too quickly and pushes final visuals ahead of its audio.
- **Fixed (superseded timing):** 2026-07-11 — timing-critical result/draw/random clips now preload and batched playback gives the primary a 180 ms maximum start window (`sound.ts:287-374`). The first implementation admitted only already-cached optional layers; M36 repairs that regression without relaxing the bound. This change-set also restored a settlement-owned R3 timeline and made the between-fight delay cancelable; the exact current timing is recorded by the follow-up below.
- **Timing follow-up fixed:** 2026-07-12 — all R1/R2/optional R3/final visual reveals now use the speed-scaled `[0,850,850,850]` ms grid. R3 begins with R2 and is authoritatively snapped at 850 ms; its result audio normally lands with that snap, except `3_lww` starts at 750 ms so Final follows it after 950 ms without moving either visual beat (`FightAnimation.vue:103-110,692-823,956-985,1111-1234`). Replay navigation clears both scheduled R3 callbacks, and settlement cancels the animation-frame loop before snapping so a late frame cannot move the needle again (`FightAnimation.vue:538-544,609-679,807-817`).

### M34. Bot fast paths could commit attacks or forced skips before spending level-up points
- **Intended:** every strict bot spends all pending level-up points before choosing or confirming any attack, defense or skip; this also applies to the early round-8 Клоны Сусано challenge.
- **Actual before the fix:** ordinary attack selection followed level-up, but `CompleteForcedSkip` and the Madara round-8 branch returned first. The M27 readiness precommit also assigned the Madara attack before bot behavior ran at all.
- **Impact:** forced-skipping bots and every bot challenging Madara on round 8 could carry unspent points into a committed action, making their fight snapshot weaker and violating the same spend-before-action rule enforced for humans.
- **Fixed:** 2026-07-11 — bot playstyle selection and the complete level-up loop now run before every action fast path (`BotsBehavior.cs:72-132`). The readiness tick also prepares every live strict bot before the idempotent Madara challenge precommit (`CheckIfReady.cs:1038-1042`), so the M27 no-wait behavior remains intact without bypassing progression.
- **Designer rework follow-up, 2026-07-12:** the Madara readiness precommit was removed with M27's forced attack. The invariant remains: `HandleBotBehavior` spends all pending points before the forced exact round-8 prediction, a forced Skip, or the bot's ordinary action (`BotsBehavior.HandleBotBehavior`).

### M35. Character-specific leaderboard icons reveal masked opponents
- **Expected:** before the game finishes, character identity remains a guessing mechanic. Character-specific leaderboard prefixes and widget state must be visible only to the character owner (or an admin), while spectators and opponents receive no annotation that identifies a masked character. Тигр's round-10 `🚫` is the explicit exception: it represents a public system ban and remains visible to everyone as part of the joke.
- **Actual before the fix:** the shared Discord/web prefix builder exposed Goblin mines, Ziggurats and protection, DooM Guy demon nests, sealed Мадара and active Salldorum Шэн to every player (`GameUpdateMess.cs:219-270`). Recipient-side `🤝`/`⚔️` annotations also identified Napoleon to his ally and Support to the marked Carry (`GameUpdateMess.cs:730-748`). Because the web custom-leaderboard prefix/text reuse those methods, the same leak reached live games and player-perspective replays; only spectators escaped it because they receive no populated custom board.
- **Impact:** a single special icon could solve an opponent's character without a reveal mechanic, undermining predictions and the core hidden-roster game.
- **Fixed:** 2026-07-12 — character-derived board objects are now owner/admin-scoped except for Тигр's intentionally public system-ban `🚫`. Goblin and DooM owners retain their target/position markers, self-only seal/Шэн status remains available, and Napoleon/Support retain their complete owner-side alliance views; opponents and recipients no longer receive the other identifying icons. The owner-only `PassiveAbilityStates` mapper and all other per-character annotations were audited and already satisfied this boundary (`GameUpdateMess.cs:219-811`; `GameStateMapper.cs:362-825`).
- **Rework follow-up:** 2026-07-12 — the obsolete active-Шэн icon was replaced by the fixed-cell cola 🥤, still projected only to Salldorum/admin through `CustomLeaderBoardBeforeNumber`; the owner widget carries charges/drink history without exposing either to opponents.

### M36. Preloaded fight-result audio silently excluded every cold randomized layer
- **Expected:** eligible folk percussion, lose memes and Геральт vocals play on the same Web-Audio frame as their corresponding win/lose clip. Random selection and the percussion no-repeat pool must not make a selected layer inaudible.
- **Actual before the fix:** the M33 bounded batch started as soon as its preloaded primary was available, then included an optional layer only if its randomized URL was already in `audioBufferCache` (`sound.ts:297-334`). The selected URL's fetch had only just started, so it was almost always omitted; folk percussion then removed that unheard URL from its per-fight pool, repeating the failure for each new selection.
- **Impact:** after the fight-animation timing repair, the primary sounds remained correctly synchronized but percussion, lose-meme and Геральт vocal layers appeared to stop working.
- **Fixed:** 2026-07-12 — a batch now gives cold selected layers the unused portion of the same 180 ms primary deadline and starts every buffer that reaches that single cutoff on one AudioContext frame (`sound.ts:297-345`). The optional wait cannot extend or reorder primary playback, so M33's anti-drift bound remains intact.

### m29. Three V2 achievement descriptions do not match their evaluators
- `x_spartan_mylorik` RU copy says the **next** fight, while the tracker intentionally accepts any later fight (`AchievementClass.cs:326-332`; `DoomsdayMachine.cs:1418-1436`). `c_darksci_unstable` requires finishing alive at actual place 1 but omits “alive” in both languages (`AchievementClass.cs:284-287,513-519`). `c_kratos_olympus` says five “enemies,” although team mode counts every other player, including teammates (`AchievementClass.cs:264-267,491-492`; `CharacterPassives.cs:1795-1809`).
- **Impact:** the achievement center can tell players a stricter, looser, or team-inaccurate requirement than the code actually evaluates.
- **Fix direction:** change presentation metadata only: “later fight,” “finish alive,” and “all five other players”; do not change the mechanics or canonical passive strings.
- **Fixed:** 2026-07-11 — aligned the paired catalog copy with the existing evaluators: “later fight,” “finish alive in 1st,” and “all 5 other players” (`AchievementClass.cs:264-287,326-332`). No gameplay condition or canonical passive identifier changed.

### m30. Localized logs and dynamic UI labels fell through to their source language
- **Expected:** a viewer's RU/EN choice applies to every presentation string without translating canonical character/passive/action identifiers in game state.
- **Actual before the fix:** `GameLocalization.Text` tried `exact` only against the entire input and returned immediately after adapting any `|>Phrase<|` entry. Since personal/global logs are multi-line blocks, the presence of one character phrase prevented the remaining system/stat lines from reaching normal rules, while exact translations for individual lines could never match the whole block. The Vue translator had the same whole-node limitation in both directions, so interpolated English widget labels stayed English in RU mode. Finished chronicles and Eternal Tsukuyomi projections also overwrote/bypassed the localized log projection, and four Cyrillic passive titles had descriptions but no English display-name mapping.
- **Impact:** English players regularly saw Russian passive titles and personal/global/chronicle system text; Russian players saw English widget/status/navigation labels. Gameplay dispatch remained correct because the untranslated values were presentation-only.
- **Fixed:** 2026-07-11 — both localization engines now run dynamic templates and longest-first exact fragments across composite strings, with character-phrase adaptation continuing into the rest of the log (`GameLocalization.cs:89-142`; `Web/VueClient/src/i18n.ts:47-61,155-196`). The finished chronicle and Eternal Tsukuyomi projection now pass through the viewer locale (`GameStateMapper.cs:168-175,932-960`). The shared catalog fills the four passive-title holes and the reported RU widget/UI labels; `tools/audit-localization.sh` now requires display mappings for every Cyrillic character/passive identifier and scans `terms` values for Cyrillic leaks. Canonical Russian character phrases and all dispatch strings are unchanged.

### m31. English replay viewers still saw Russian phrase bodies, system logs and class values
- **Expected:** changing a public replay to English must localize the selected player's saved log perspective and every presentation-only class label without changing canonical replay/action identifiers.
- **Actual before the fix:** replay snapshots store each player's mapped private perspective (`ReplayService.cs:301-339`), so an RU-origin snapshot can later be opened by an English viewer. The Vue translator understood ordinary exact/term fragments but not the `|>Phrase<|Passive: quip` protocol; it translated the passive title and left the Russian quip, producing mixed lines such as `Party of One: Что делаешь?`. Its dynamic rules also omitted the timeout, `#life`, Eternal Tsukuyomi winner and score-source forms, while `PlayerCard` split `Сильный`/`Быстрый` into standalone nodes with no English entries. `SkillsPanel` converted the complete canonical passive description to marked-up HTML before localization, splitting an otherwise-catalogued translation into unmatchable fragments (`PlayerCard.vue:769-842`; `SkillsPanel.vue:125-142`). Reproduced in replay `39567a79`, round 11, player 0.
- **Impact:** an English replay could still require Russian to understand personal logs and the class card even after the broader m30 localization pass. Live English `#life` logs had the same missing dynamic template.
- **Fixed:** 2026-07-11 — Vue now decodes canonical and older partially translated phrase records before ordinary fragment replacement, using the same passive-aware fallback catalog as the backend (`Web/VueClient/src/i18n.ts:146-196`; `GameLocalization.cs:128-142`; catalog `localization.en.json:939-1067`). Added every reported status/score/winner/class fragment, and localize complete passive descriptions before markdown rendering (`SkillsPanel.vue:125-142`; catalog `localization.en.json:44-64`). Character/passive source and English descriptions are paired bidirectionally at load time, without changing any Russian `characters.json` field (`Web/VueClient/src/i18n.ts:16-31`). Focused fixtures cover the reported strings, old mixed phrase records, class values and RU↔EN passive descriptions (`Web/VueClient/src/i18n.spec.ts:6-51`). The audit now requires display/fallback coverage for all 132 `PhraseClass` identifiers and rejects Cyrillic in every English catalog value (`tools/audit-localization.sh:13-78`).
- **Completed phrase rework:** 2026-07-11 — the passive-wide fallback is now legacy-only. `phrases.en.json` supplies a context-aware English counterpart for every one of the 796 runtime Russian variants across all 235 fields. `PhraseClass` selects and consumes aligned indices and stores both rendered variants in replay-safe payloads; media DTOs likewise retain both languages. Vue, Discord and story boundaries resolve the same record without altering Russian text (`PhraseLocalization.cs`; `CharactersPhrases.cs:1713-1857`; `i18n.ts:151-208`). Remaining hardcoded live phrase markers for Madara, Rick and Eren were migrated too. Startup plus `audit-phrases.sh` enforce complete parity; focused fixtures cover AWDKA's Wukong/Ignite line, Mylorik's Vietnamese Tristana line, RU switching and direct messages.
- **Follow-up fixed:** 2026-07-11 — completed the remaining mixed replay/UI vocabulary and dynamic fragments: `Последний шанс!`, mixed-script `Cкилла`, bold-split `обычных`/`бонусных`, all seven Justice encouragements (including emoji spacing), every AWDKA troll-result variant, and the dynamic last-second point line. Both canonical and already-partially-translated replay forms are covered bidirectionally in the shared catalog; regression fixtures reproduce all seven reported leaks (`localization.en.json`; `i18n.spec.ts`). Russian producers remain unchanged.
- **Full legacy-pair follow-up:** 2026-07-11 — the English-only phrase arrays were replaced with validated `{ russian, english }` pairs plus paired passive titles for all 235 fields / 796 variants. Backend and Vue now resolve old marker records by passive context and exact body before fallback, including duplicate passive titles, ASCII-only memes and multi-line phrases. A regression loop exercises every catalogued legacy form; targeted fixtures cover the newly reported Saitama one-punch/training/quips, Rick's full name, target-class rewards, Weedwick drop, mixed attack text, Geralt Meditation/Witcher senses, and doubled Contract score source (`PhraseLocalization.cs:19-198`; `i18n.ts`; `i18n.spec.ts`). Geralt's 31 English static hints moved from a duplicate C# dictionary into the shared catalog. Russian source text and dispatch identifiers are unchanged.
- **`afffa305` follow-up fixed:** 2026-07-11 — the Chronicle had converted Discord markdown to HTML before localization, splitting templates around bold dynamic player names. It now translates the complete source first. Mirrored backend/client rules finish canonical and already-mixed Auto Move, Darksci tilt, and all three dynamic nemesis defeat connectors without constraining or changing the username. Added the missing Character Highlights heading, uppercase Carapace widget title and HardKitty finale adaptation; replay-specific fixtures cover every reported form plus a punctuation-heavy synthetic name (`FightAnimation.vue:1024-1039`; `GameLocalization.cs:34-49`; `i18n.ts:104-119`; `i18n.spec.ts:69-92`). The replay's canonical Russian records and all Russian producers remain unchanged.
- **UI/character-log follow-up fixed:** 2026-07-11 — added the two missing empty-state labels and adapted the deterministic All Talk, Armin, Young Schoolkid and Dragon lines in the backend-owned exact catalog. Mirrored dynamic rules complete canonical or mixed Block actions, Rumbling places and Mitsuki point totals; the Shock Shield suffix covers both `ход` and a previously projected `turn` (`GameLocalization.cs:34-55`; `i18n.ts:104-125`; `i18n.spec.ts:101-123`). No canonical Russian log producer, character description or passive identifier changed.

### m32. AI-generated Witcher hints and malformed Stories could be permanently single-language
- **Expected:** every generated artifact stored in live state or a replay must retain both adapted languages so the viewer can switch RU/EN later.
- **Actual before the fix:** `GenerateWitcherHintAsync` asked Haiku for only Geralt's current account language, then wrote the arbitrary response as a plain personal-log string (`ClaudeHaikuService.cs`, pre-fix method; `CharacterPassives.cs`, pre-fix Медитация branch). No catalog can translate that unknown prose later. `GameStoryService` requested both tagged stories, but if either tag was absent it formatted and published the raw response, allowing the same permanent language leak into the Story tab and replay file.
- **Impact:** changing language after a successful AI hint—or opening its replay under another locale—showed the generation language; malformed Story responses could do the same for every viewer.
- **Fixed:** 2026-07-11 — Witcher hints now request matching `<ru>`/`<en>` adaptations once, validate/normalize both, fall back as a pair, and store the result in a self-contained PhraseV2 payload (`ClaudeHaikuService.cs:34-108`; `CP:4767-4802`). At the time, Story used the shared tagged parser, retried malformed output once, and rejected a second invalid response rather than publishing raw single-language HTML (`BilingualGeneratedText.cs:6-32`; pre-m35 `GameStoryService.GenerateStoryAsync`). The localization audit forbids the old hint generator, and the client fixture switches arbitrary generated hint prose in both directions.
- **Story follow-up:** the tagged one-call Story transport was superseded by m35 after production responses repeatedly omitted one or both wrapper tags. Witcher hints still use the compact paired-response parser.

### m33. Madara web audio did not enforce its CDN and private-ending boundaries
- **Expected:** the round-8 `madara_tsukuemi_theme.mp3` streams from `https://r2.ozvmusic.com/kotgh/sound/character_passives/madara/madara_tsukuemi_theme.mp3`. Under Вечное Цукуеми, each fooled enemy locally hears only their own character's victory theme, when that character has one.
- **Actual before the fix:** ordinary sound helpers used the R2 base, but `MediaMessages` passed the server's relative `/sound/...` URL directly to `Audio`, leaving Madara's theme dependent on the app's static-file route. End-game themes used the generic place-1 scan and had no explicit branch for the private fake-victory source.
- **Fixed:** 2026-07-11 — the shared resolver now normalizes relative media sound paths to the R2 CDN and `MediaMessages` uses the resolved URL for autoplay, replay and expiry bookkeeping (`sound.ts:170-180`; `MediaMessages.vue:90-130`). The finish watcher recognizes the viewer-owned Вечное Цукуеми score entry and dispatches only that viewer's character theme through the supported-theme map (`Game.vue:233-274`; `sound.ts:974-983`).

### m34. AI stories expose the hidden terminal boundary as a playable round
- **Expected:** a normal story covers combat rounds 1–10; a Kratos resurrection story may continue through every actually captured combat round. `HandleLastRound` additions are an epilogue, not another fight round, and round-11 setup noise must stay excluded just as it is in replay v2.
- **Actual before the fix:** `GameStoryService.CaptureSnapshot` copied the terminal `game.RoundNo` (11 in a normal game) into `RoundCount`, and `BuildPrompt` emitted every integer through that value as `<round>`. The prompt therefore advertised a fake round 11 and could attach the hidden setup buffer to it; extended games inherited the same terminal-counter off-by-one. Story input also ignored replay v2's same-round `FightLog`, leaving the model to infer character interactions from truncated prose logs (pre-fix `GameStoryService.CaptureSnapshot` / `BuildPrompt`; replay contract `ReplayService.cs:35-164`).
- **Impact:** the generated Story tab could describe a non-playable round and was biased toward repetitive, single-character chronological summaries even though the replay beside it showed the correct playable range.
- **Fix direction:** derive story rounds from `game.ReplayRounds`, pass structured attacker/defender outcomes, isolate explicit `FinalSettlement*` suffixes as an epilogue, and vary the narrative framing deterministically per game without changing recorded facts.
- **Fixed:** 2026-07-12 — story snapshots now enumerate `game.ReplayRounds`, so normal games expose 1–10 while Kratos automatically includes every captured extension; the replay's explicit settlement suffixes are emitted under `final-settlement`, never as another round (`GameStoryService.cs:168-314`; replay isolation `ReplayService.cs:106-144`). Each round now carries structured attacker/defender/outcome facts plus an aggregate rivalry summary. A game-ID-seeded director card independently selects one of six frames, structures and chaos devices (216 combinations), while the rewritten prompt prioritizes multi-character action→reaction chains and permits invented presentation but not invented gameplay (`GameStoryService.cs:335-563`).

### m35. Tagged bilingual Story responses could reject the entire generated feature
- **Expected:** a valid story in either requested language should reach the Story tab and replay even if the other language is disabled for testing or its API request fails.
- **Actual before the fix:** `GameStoryService` asked one request to return both `<ru>` and `<en>` blocks. Production responses omitted the required wrapper structure twice (`Invalid bilingual output` followed by `Rejected non-bilingual output`), so the parser discarded otherwise usable prose and no Story event or replay backfill occurred (pre-fix `GameStoryService.GenerateStoryAsync`; production report 2026-07-12 16:13).
- **Impact:** Story mode could disappear completely after a successful Anthropic generation, and testing the Russian path always spent tokens on English.
- **Fixed:** 2026-07-12 — Russian and English now use independent, language-specific requests run concurrently; each accepts plain non-empty prose and retries only its own empty result (`GameStoryService.cs:65-141,335-445`). Failure in one language no longer discards the other, and the combined replay-safe HTML supplies a localized unavailable message for a missing side (`GameStoryService.cs:77-95,637-647`). `GameStoryEnglishEnabled` defaults off, skipping the English request unless it is explicitly enabled in `DataBase/config.json` (`Config.cs:27-29`; `GameStoryService.cs:37-42,65-71`).

### m36. Death Note could kill an already-dead round-10 settlement victim twice
- **Actual before the fix:** Rumbling and then Naruto's Теневые settlement run before the ordinary round-10 `HandleEndOfRound` dispatcher. If Kira's queued note targeted one of those newly dead players, the Death Note branch set `IsDead`/`DeathSource` again, paid Монстр another death point and counted a kill even though the target had already died.
- **Fixed:** 2026-07-12 — the Death Note resolves an already-dead target as an invalid duplicate: it writes a private explanation, clears the queued target/name and exits before the kill, Kira reward and Монстр payout (`CharacterPassives.cs` Death Note branch). This applies to every earlier death source, including Rumbling and Теневые.

### m26. `HandleBotAttack` scoring flags are never reset inside the per-target loop
- The boolean flags declared once per `HandleBotAttack` invocation (method `BotsBehavior.cs:770`; flags `:836-855`) remain set and are not cleared between targets. Per-character compensations later read them (mylorik `:1380-1410`, Глеб `:1595-1640`) and can therefore compensate a different target.
- **Impact**: mild bot mis-weighting in mixed line-ups; not player-visible and not exception-producing. Flagged during the AI-difficulty change-set because L2-4 (fight-history horizon) deliberately **reuses** the existing latching flag/number so it stays balance-compatible with those compensations.
- **Status:** observation, **not fixed** — the AI-difficulty change-set is L1-preserving by contract, so touching this shared scoring pass was out of scope. Fixing it (reset the flags at the top of each iteration) would change L1 bot behavior and should be a standalone change with its own sim comparison.

## Phase 6 — full cross-mechanic re-audit (2026-07-12)

> **Scope:** end-game resolution order (`HandleLastRound`), round-10/11 specials × the Кратос extension, interaction-matrix re-verification with missing-cell enumeration, mechanical invariant greps (raw psyche/justice, ForOneFight placement, DeepCopy coverage, `RoundNo` hardcodes), all characters incl. the mid-audit Наруто/DooM additions, plus simulation batches (baseline 106×2 + 8 special-character lineups ×30 + Наруто lineup ×30 — **586 games, 0 exceptions, 0 freezes**). Premade/team-mode verified by code reading only (not sim-testable). Anchors verified against the post-`e5b537f` tree.

### M37. A dead top-scorer keeps place 1 and takes the win statistics from the announced winner
- **Actual:** the final sort is purely by score with dead players included (`CheckIfReady.cs:404-408`). The *announced* winner is the first **alive** player (`:554-556`, `:629-633`), but the payout loop derives `rewardPlace` from the raw board place (`:662`), so a dead player at place 1 receives `TotalWins++`, character `Wins++` and place-1 performance stats (`:730`, `:769-778`) while the announced alive winner (place 2+) gets no win credit. (ZBS/lootbox/mastery dead-gates are correct; quest `isMatchWinner` follows the announced winner — so quest-wins and account-wins disagree too.)
- **Reachability:** any kill source that leaves score intact — Кира killing the round-8-10 leader, Пейзаж, Rumbling, Кратос event. Sim evidence: **48 of 346 games** finished with a dead player at place 1 (e.g. dead HardKitty at place 1 with 146–153 points in the Мадара lineup; 3 occurrences even in the 106-game baseline).
- **Impact:** player-visible: the profile records a win for a game the announcement said someone else won.
- **Fix direction:** designer decision — either sort dead players below all alive players at game end, or make `rewardPlace`/win-stats follow the announced winner. (Наруто's dispersed clones already do the former via `MoveDispersedClonesToBottom` — the pattern exists.)

### M38. A bot Кратос never fights his own resurrection event
- **Actual:** `HandleBotBehavior` hard-blocks every bot after round 10: `botChoice = -10` is a literal Block (`BotsBehavior.cs:89-93`; `GameReactions.cs:692-700`). This catches **Кратос himself**, so the event-aware bot logic (`CanBreakKnownDefense`, `BotsBehavior.cs:241-246`) is unreachable during the event. With everyone else force-blocked (`CheckIfReady.cs:1122-1128`) and the only kill path being Кратос winning **as attacker** (`CP:1817-1830`), a bot-Кратос event produces zero kills.
- **Evidence:** the bot even has a `Kratos:Ragnarok` playstyle that engineers the round-10 loss to *trigger* the event it then blocks through. Кратос winrate across event lineups: 0–6.7%.
- **Impact:** the marquee event is dead content in bot/mostly-bot games; see M39 for the compounding grind.
- **Fix direction:** exempt the event Кратос (and only him) from the round>10 bot block, routing him into `HandleBotAttack`.

### M39. The Кратос event usually grinds to the round-20 hard cap instead of ending at 16
- **Described:** `docs/CHARACTERS.md` (Кратос): "event ends at round ≥16 or when 5 players are dead".
- **Actual:** the round-16 check additionally requires **fewer than 5 alive** (i.e. ≥2 dead, `CP:3831-3841`); otherwise only the `RoundNo >= 20` hard cap ends the game (`CheckIfReady.cs:1032-1035`). Combined with M38, most event games run rounds 11–19 with everyone blocking, **each round scoring ×4** (`InGameStatusClass.cs` `GetRoundScoreMultiplier`, `_ => 4` arm).
- **Evidence:** rounds distribution in event lineups: 24/30 (settlers lineup), 28/30 (Спартанец lineup), 16/30 (special-win lineup) games hit the round-20 cap. Block-proof round-farming kits dominate those games — Осьминожка won **80%** of the settlers lineup.
- **Impact:** balance-relevant hidden behavior: up to 9 bonus rounds of ×4 income for kits that monetize passive rounds.
- **Fix direction:** designer decision — make round ≥16 end the event unconditionally (matching the docs), or deliberately document/curate the ×4 extension economy.

### M40. A dead Штормяк holder keeps taunting, forcing the living to fight a corpse
- **Actual:** dead players are auto-set `IsBlock = true` every tick (`CheckIfReady.cs:1133-1138`), and the Штормяк taunt loop selects taunters by `IsBlock` + passive with **no `IsDead` filter** (`:1339-1373`). The taunt-bypass then turns the forced fight into a *real* fight against the blocking corpse (`DoomsdayMachine.cs:597-600`), and the fight-target selection has no dead guard either (`DoomsdayMachine.cs:450-455`). The corpse's `FightCharacter` has full stats, so it can win and keep earning score after death (feeding M37).
- **Reachability:** Котики die to Кира/Пейзаж/Rumbling/Кратос. The original Котики is capped at one taunt per enemy per game; a **transferred** Storm cat (Кошачья засада) taunts every round, forever.
- **Fix direction:** add `!t.Passives.IsDead` to the taunter filter; matrix §1 needs a "dead as *source*" row (the table only covers dead as target).

### M41. BattleShip_update was checkmarked while action boundaries still broke its limits and turn rules
- **Actual before the fix:** `ShootOwnBoard` never called `ProcessTurnStart`, so a player could bypass a queued Penalty/Stun by firing at an approaching summon. `SelectWeapon` changed `SelectedShotType` before proving that a live loaded weapon existed, and `Shoot` trusted that state; a forged Greek Fire/Incendiary selection worked without the upgrade, while a depleted Incendiary stayed selected and fired forever. Two own-board summon-death paths decremented `SummonSlotsUsed`, turning the four-normal-summons-per-match limit into a partially reusable counter. Conversely, free ship/boarding pending summons were blocked by and consumed that normal limit despite `PendingSummonDeploy.IsFree` explicitly promising no slot cost; full normal usage could therefore soft-lock mandatory boarding deployment.
- **Fixed:** 2026-07-12 — own-board fire shares the turn-start gate and skipped results carry `WasSkipped`; every special selection resolves to a living loaded weapon before state changes, every shot revalidates ammo, and the final finite projectile restores Ballista. Normal uses are never refunded, while free pending/boarding summons ignore the normal cap. Turn-back redeployment reuses the existing summon without a new cap/reveal check, and forged fresh Cursed Boats are rejected (`BattleshipService.Shoot`, `ShootOwnBoard`, `SelectWeapon`, `DeploySummon`, `DeployPendingSummon`; death paths in `BattleshipGameEngine`).

### m37. Simulation winrate counts only place 1 — special winners and dead leaders are misattributed
- **Actual:** `SimulationRunner` credits a "win" solely to final place 1 (`SimulationRunner.cs:668,679`). Sakura's «Одна из трех» soft wins are invisible (she actually won **11 of 61** sampled games while every report shows 0%); dead place-1 players (M37) are counted as winners; Наруто's clone seats inflate his denominator (21/90 for 30 games).
- **Impact:** harness-only, but it skews every balance read taken from sim winrates.
- **Fix direction:** attribute wins by the game's announced winner; count Наруто as one entity.

### m38. A score tie suppresses Sakura's declared win but not her win announcement
- **Actual:** Sakura's win fires her global line and reveals the passive (`CheckIfReady.cs:542-552`), then the final announcement separately prints «Ничья» if anyone ties `playerWhoWon`'s score (`:629-633`). A Sakura tied at top-3 gets both: «Я одна из легендарной тройки…» **and** «Ничья» (while still being paid first-place rewards via `rewardPlace`).
- **Fix direction:** designer call on whether a tie beats «Одна из трех»; make the two messages agree either way.

### m39. `HandlePostGameEvents` computes its own winner, so two win narratives can both print
- **Actual:** the flavor dispatcher recomputes `playerWhoWon` as *first alive by score* (`CheckIfReady.cs:88-91`), ignoring the Sakura override decided later — with a Sakura soft-win, the real place-1 character's win phrase (Кратос/Сайтама/Монстр/Рик/HardKitty) prints alongside Sakura's win line.
- **Fix direction:** pass the settled winner in, or skip character win-phrases when «Одна из трех» triggered.

### m40. The score floor at 0 exists only in `AddBonusPoints` — the regular-point flush can leave anyone negative
- **Actual:** `AddBonusPoints` floors `Score` at 0 unless the player has «Никому не нужен» (`InGameStatusClass.cs:227-233`); the end-of-round flush `AddScoreWithMultiplier` has **no floor** (`:324-350`). Multiplied negative regular points (e.g. «СОсиновый кол» −1 ×2/×4) can push a non-HardKitty player's visible score below zero, where it stays until the next bonus event incidentally floors it.
- **Fix direction:** either floor in the flush too, or declare negative scores legal for everyone (then the `AddBonusPoints` floor is the anomaly).

### m41. Random forced-attack pools exclude the dead but not the round-10-banned Тигр
- **Actual:** the Монстр no-escape random redirect (`CheckIfReady.cs:1404-1421`) and the Aggress auto-attack (`:1288-1301`) pick any living target, including a round-10-banned Тигр — unlike the Шэн/Штормяк/Монстр-victim carve-outs. The attack fizzles on his forced skip (no penalty), so the victim just wastes their forced action and inflates the skip-event counter.
- **Fix direction:** add the ban carve-out to both random pools for consistency.

### m42. Round-8 Клоны Сусано attacks are granted to dead predictors
- **Actual:** dead players are auto-`ConfirmedPredict` (`CheckIfReady.cs:1133-1138`) and the clone-attack loop has no `IsDead` filter (`:1418-1443`), so a player killed before round 8 (Death Note resolves from round 1) whose locked prediction named Мадару attacks him as a corpse.
- **Fix direction:** add `!p.Passives.IsDead` to the predictor filter.

### m43. Кира's L-arrest "−500 очков" actually floors to 0
- **Actual:** the arrest applies `AddBonusPoints(-500, "Арест Киры")` (`CP:5272`), which floors at 0 (`InGameStatusClass.cs:232`); the global log and `docs/CHARACTERS.md` both claim −500. Zeroing is probably the intent (drop him to the bottom), but a 0-score Кира ties other zero-score players instead of sitting below them.
- **Fix direction:** designer wording call: keep the floor and fix the log/doc text, or bypass the floor for the arrest.

### m44. Чернильная завеса entries created during Кратос-event rounds never restore
- **Actual:** the ink restore runs exactly once, at round 11 (`CP:5343-5380`). Ink ledger entries created in event rounds 11–19 (fights still reach Осьминожка via taunt/no-escape/event bypasses) are never flipped — the fake result becomes permanent.
- **Fix direction:** re-run the restore at game end for entries newer than round 11, or stop ink recording after round 10.

### m45. INTERACTION-MATRIX §7 omitted the Goblin Ziggurat step in the end-game order
- **Actual:** the settlement order in `docs/INTERACTION-MATRIX.md` §7 listed "…sort → AWDKA → Premade → Sakura" while the code runs the M1 Goblin-Ziggurat enforced win between Premade and Sakura (`CheckIfReady.cs:524-540`).
- **Fixed:** 2026-07-12 — §7 order line now names the Goblin step (this change-set; docs-only).

### m46. Battleship feedback lied about skipped shots, Brander deaths, trails and re-entry coordinates
- **Actual before the fix:** every disappearing Brander played the explosion sound, including quiet Drakkar freeze and harmless live-deck collision. A skipped Penalty/Stun turn was emitted as a miss at default A1, inflating client shot statistics and triggering fake projectile/miss feedback. Mast re-entry warnings always formatted row 1 even when a summon entered at row 10 or from a side. The trail cache recorded the previous state rather than the incoming spawn/re-entry state, and penalty-zone copy omitted the no-penalty spawn exception.
- **Fixed:** 2026-07-12 — Brander sound now requires both disappearance and a newly received detonation log; `wasSkipped` suppresses shot VFX/sound/stat changes; Mast warnings receive actual row+column; incoming state positions seed trails and new matches clear cached trails/marks; the tooltip states the spawn exception. `SummonDto.moveDirection` now drives a dedicated “Вернуть на карту” flow that highlights the actual bottom/side edge and adjacent lanes, so a sideways Cursed Boat is operable without using a fake row-1 proxy (`useBattleshipStore`, `CombatPhase.vue`, `SummonBar.vue`, `CellComponent.vue`, `GenerateMastWarning`, `GameHub.BattleshipShoot*`).

### M42. Salldorum's active kit was unreachable or resolved after it could matter
- **Actual before the fix:** Шэн required hidden web-only `ActivateShen`/`DeactivateShen` calls with no Vue/store caller, then cleared `ActiveThisTurn` in `HandleEndOfRound` before its post-sort movement case, so it could taunt but never move. Великий летописец set its ×3 Skill multiplier in the after-fight hook, after `CalculateRounds` had already decided the result. The Chronicle rewrite existed in Hub/SignalR but had no player-facing button; bots merely flipped rewrite flags without any score/stat/cola effects. Finally, the cola never cleared `Buried`, so its pickup flag reset allowed repeated natural drinks.
- **Fixed:** 2026-07-12 — the shared readiness-stage `Salldorum.ResolveShenDashes` auto-spends the next submitted attack, performs the leap before fight calculation, retains the below-position pull and arms the one-round random-target magnet; Цукуеми suppresses the whole activation and post-pull Madara/Naruto sanitation protects sealed/mutual queues. A forced second Геральт target receives the consolidated contracts. The ×3 multiplier moved to both attack-before and defense-before and is reset per opponent; bots and web use the same `Salldorum.RewriteHistory`, whose server/UI gates reject dead Salldorum; every live completed Chronicle round exposes a guarded glowing action; and the central cola resolver consumes/hides the fixed-cell object while permitting only the specified one historical second drink. `c_salldorum_double_cola` covers that intended timeline interaction.

### D12. Every bonus-point "steal" mints points at the 0-floor
- Because `AddBonusPoints` floors the victim at 0 (`InGameStatusClass.cs:232`), each transfer-shaped mechanic credits the taker the **full** amount while the victim may lose less: «Выгодная сделка» (`CP:1803-1815`), Смертельный вирус, Глаза Итачи deduction, Сайтама's ledger reclaim, «Запах мусора». Zero-sum on paper, positive-sum at the floor. Acceptable pity cushion, or should takers receive only what victims actually lose?

### D13. Forced-fight sources pierce the Кратос event's "everyone blocks" rule
- During the event the loop forces `IsBlock = true` on all non-Кратос players every tick (`CheckIfReady.cs:1122-1128`) — but Штормяк's taunt *triggers on blocking* (`:1339`), and a Монстр no-escape window spanning into round 11 un-blocks its victim and forces a random attack (`:1404`). Aggress/Атакующий Титан/pickle conversions likewise strip the forced block in the fight phase. Result: real fights (at ×4) between non-Кратос players during the event, and an Октопус-style fake loss can even hand Кратос his final death. Intended chaos or event-integrity bug?

### D14. Sakura's «Одна из трех» applies inside team mode
- In a team game the announcement path is team-score-based and ignores `top3Player`, but the payout loop still pays a top-3 Sakura individual first-place rewards (`CheckIfReady.cs:566-626` vs `:662`). Should the hidden solo win condition exist in team games at all?

### Phase-6 checks that passed (selection)
- Mechanical invariants (re-run after the Наруто/DooM commits): no raw `AddJustice`; no live `GameCharacter.*ForOneFight` (only the commented-out Оборотень blocks, `CP:494-502,1240-1241`); every raw `AddPsyche(-…)` is a self-cost («Безумие», «Дерзкая школота», «СОсиновый кол») or the documented Дизмораль exception; `CharacterClass.DeepCopy` covers its only collection.
- End-game resolution order matches the §7 spec; Итачи deduction correctly precedes the sort; AWDKA re-sort correctly precedes the Sakura check; Sakura's alive-gate, the dead-player ZBS/mastery/lootbox gates and the Мадара projection's authoritative-payout isolation are all correct (post-`041dbab` round-8 flow matches its docs).
- Round-10 forced moral conversion cannot loop forever: every `AddMoral` interceptor still decrements or zeroes (Let's Roll zeroes moral at activation).
- Kill census: exactly 7 `IsDead = true` sites = the 6 documented sources + Кратос's own event death; no undocumented kill source; kill×immunity rows verified incl. the Мадара/Гоблин exemptions inside the event kill path.
- «Претендент русского сервера» ×3 regular, Тигр round-10 ban state, `RollExploit` m5 guard, Сайтама round-11 reclaim (zero-sum ledger + manual `BonusPointsFromMoral` flush), `HandleOctopus`×Еврей redirect, Пейзаж survival rules, Монстр death/drop rewards, Маневры/Вознесение remaining-grant caps — all match their docs.
- The 35-name prediction pool is deliberate (tier <0 + «Выдуманный персонаж» exclusions enforced consistently in scoring, bots and UI).
- Наруто integration spot-checks: `ResolveScoreSuccessor` redirects all five late liabilities (virus `CheckIfReady.cs:369`, Цукуеми `:395`, Сайтама `CP:5314`, Октопус `CP:5367`, Школьник `CP:6480`); `SettleShadowClones` runs right after Rumbling (`DoomsdayMachine.cs:1510-1511`); clone deaths incidentally satisfy the M39 `alive<5` gate, so Наруто games end events at 16. Наруто-forced sim: 30/30 clean.
- Simulations: 586 games across 11 runs — 0 exceptions, 0 stuck games.

### Phase-6 balance observations (sim data — for the designer, not findings)
Fixed-lineup winrates, 30 games each, AI difficulty 3; subject to the sim-noise caveat (never tune off one sweep):
- **Стая Гоблинов dominated every lineup they appeared in**: 80% (kill lineup), 86.7% (copy-chain lineup), 43.3% (Мадара lineup).
- **AWDKA >45% twice** (53.3% special-win lineup, 46.7% Спартанец lineup) — the troll-win converts very reliably at difficulty 3.
- **Осьминожка 80%** in the Кратос-settlers lineup is mostly the M38/M39 grind payoff.
- Bottom across lineups: mylorik 0%, Рик Санчез 0%, Sirinoks 0–3.3%, Котики 0–17.6%, Кира 0–3.3%, Кратос 0–6.7% (see M38).
- Sakura converted top-3-alive in 11/61 games (18%) but died in 34/61 — kill-heavy metas hit her hardest (invisible in reports until m37 is fixed).

## Unfinished work backlog (2026-07-12)

### Still-open findings
- **m12** — Сайтама's round-1 "serious targets" are effectively arbitrary (skill is 0 at game start).
- **m19** — Итачи's Crows/Izanagi charges have no web-UI representation (only Tsukuyomi state is mapped).
- **m24** — ARAM pick phase has no web UI (hub/REST/serialization exist, no Vue component).
- **m26** — `HandleBotAttack` scoring flags never reset per target (deliberately deferred; needs its own sim-compared change).

### Bot-strength plan (Phases 2–3 remaining)
Plan file `~/.claude/plans/mossy-mixing-hickey.md`: Phase 0 (seeded A/B harness) and Phase 1 (universal L2/L3 mastery) are done. **Phase 2**: bot random-rolls for Darksci (`IsStableType`) and Глеб→Молодой Глеб transform, mirroring the Dopa `HandleNextRound` pattern. **Phase 3**: probe-sweep the roster L2/L3-vs-L1, bespoke work on the weakest kits (expected: Продавец, Sirinoks, Краборак).

### Unbuilt GameDesign.txt characters
Торик, Rey, Lewdweak, Второй Крисп, Leshkinson a.k.a. Удир, Кнусклес, Таинственный Клоун, Бог ЛоЛа, and the Хранитель МСД / Машина Судного Дня event boss. (Named in rarity tables only, no kits: тоширка, Hitler.)

### Unbuilt systems & intents (GameDesign.txt)
Full team-mode ruleset (2х2х2/3х3 team-score win, forced ally predictions — only the TeamModeOnly pair is seeded); new-player onboarding/tier gating (no bot predictions vs newbies, tier unlocks every 10 games); player statistics command (most points/wins/performance/elo with defined formula); donation multiplier shop item; pre-release cleanup list (rename internal log strings «евреи»/«тебя усыпили», «Ты»→«Вы» sweep, hide round 11 from view, split `HandleNextRoundAfterSorting` round-11 handling into a post-loop pass). Designer margin note that Толя «Подсчет» was only half-implemented («ты это на половину сделал»).

### Code TODOs & dormant blocks
- `CheckIfReady.cs:648` — `//todo: need to redo this system` (disabled `_finishedGameLog.CreateNewLog`).
- `GeneralCommands/HelpModule.cs:116` — `//TODO Move it as service`.
- Commented-out «Оборотень» stat-swap blocks (`CP:494-502`, `CP:1240-1241`) — dead code awaiting a decision.
- Root `BattleShip_update` ТЗ: all 23 items were re-verified against backend, bot and Vue paths on 2026-07-12; M41/m46 document and close the cross-action/feedback gaps found behind the old ✅ annotations.

## Summary count

**1 Critical** (C1) · **42 Major** (M1–M42) · **46 Minor** (m1–m46) · **14 Design questions** (D1–D14). Phase-6 triage order: M37 (dead-winner stats), M38+M39 (Кратос event — one conversation), M40 (dead taunter), then m38–m44 and the D12–D14 verdicts. (M13–M36/M41/M42 fixed; m5/m6/m7/m17/m21/m23/m25/m27/m28/m29/m30/m31/m32/m33/m34/m35/m36/m45/m46 fixed; m18 confirmed intended; m20 documented. Still open: m12, m19, m24, m26, M37–M40, m37–m44, D12–D14.)

## Verification addendum (second pass, 2026-07-01)

A full re-verification was run over these docs: every file:line anchor (~230) was mechanically dumped and compared against the cited code — **all anchors correct**; the eight highest-risk claim clusters (agent-sourced or never personally read in the first pass) were re-read at the source; and a completeness diff was run (every `PassiveName` in characters.json and every `case` label in CharacterPassives.cs vs the docs).

**Corrected during verification** (all fixes applied in place):
1. `SecureRandom` was described as a crypto RNG in ARCHITECTURE §9 — it is a plain `System.Random` wrapper (→ new finding m21).
2. Tier semantics: Sakura and Баг (Tier −1) were described as non-rolling specials — they are **secret rollable** characters (range 40, humans only, hidden from prediction menus); Молодой Глеб's exclusion comes from the `CharactersPull` Tier ≥ −1 filter, not a range-0 roll (m6, D3, CHARACTERS.md, GAME-DESIGN §11).
3. D5 ("2kxaoc") described the masking backwards — the passive is *exempt from* enemy-passive-name masking, not hidden by it.
4. CHARACTERS.md under-documented three verified mechanics, now added: Спартанец's "Это привилегия - умереть от моей руки" per-win rider (+1 extra Justice to the victim, −1 Int to himself, `CP:2854-2862`); Вампур's Гематофагия Psyche-priority rule (`CP:2899-2914`); Молодой Глеб's "Следит за игрой" marks up to **3** targets (`CP:4716-4756`).
5. Two flavor-only hidden passives were missing entirely: "God Of War" (Кратос) and "Искусство" (mylorik/Спартанец) — added.
6. Exact passive-name hygiene: "Стримснайпят и банят и банят и банят" and "Это привилегия - умереть от моей руки" appeared with typographic substitutions (…/—) in places; fixed to exact strings (they are load-bearing identifiers).

**Confirmed unchanged** (spot-listed because they were the highest hallucination risks): all `Luck()` probability figures (Luck(a,b) ≈ a-in-b, `SecureRandom.cs:35-45`); m19 (Itachi crows/Izanagi truly absent from the web layer); M2 (Jew widget state source); the bot moral thresholds 20/13/8/5/3 and round>10 auto-block; the AdminPlayerType runtime injection (`GameUpdateMess.cs:253`); the ARCHITECTURE §3 handler line table (all rows). No finding was retracted; C1–M12 all stand.

## Doc sweep — designer verdicts on ambiguous findings (2026-07-03)

The designer reviewed every ambiguous finding in `docs/DESIGNER-REVIEW.md`. The **БАГ** ones were fixed (each carries a `**Fixed:**` note above). The following were confirmed **ОК / intended** — no code change; the ⚠ divergence marks in `docs/CHARACTERS.md` and `docs/INTERACTION-MATRIX.md` were converted to "intended".

| Finding | Verdict (ОК) as confirmed by the designer |
|---|---|
| **M3** | AWDKA silently forced to last place before every fight calculation — intended hidden mechanic. |
| **M5** | "Тигр топ" opening window at game start (rounds 1–3) — intended, in addition to the later random window. |
| **M6** | "Лучше с двумя" counts Тигр himself — intended: Тигр is a member of his own clan. |
| **M12** | Монстр's "Пейзаж конца света" **does** kill goblin pawns — intended (the one kill-source goblins aren't immune to). |
| **m8** | Толя "Подсчет" cd 4–5 — no bug; the description counts from the **end of the effect**. |
| **m9** | Итачи Цукуеми recharge 4 rounds — no bug; changed nothing. |
| **m13** | HardKitty's opening −30 **score** logged as "−30 Морали" — intentional text joke. |
| **m14** | Butcher sup marks only from round 2 — intended. |
| **D1** | ~~Darksci can dodge Дизмораль by hoarding the round-9 level-up — intended tech.~~ **Reversed 2026-07-03**: web-only banking artifact; closed by M15 (web level-up gate). Darksci now eats the −5 on both platforms. |
| **D4** | "Булинг" / "Го играть" / "lvl-мяк" / "2kxaoc" keyed on character Name — intended (name-specific jokes). |
| **D5** | "2kxaoc" is a visible-only meme (exempt from enemy-passive masking) — intended. |
| **D6** | Вампуризм **copies** the victim's Justice (doesn't drain) — intended. |
| **D8** | "Пейзаж конца света" +7 **regular** (×4 = +28) — intended; changed nothing. |
| **D9** | Premade **copies** the Carry's fight-moral (Carry keeps theirs) — intended. |
| **D10** | Ziggurat copies any `Standalone` passive incl. Булькает / dead copies — left as-is (only «Еврей» excluded, D2). |

(**D7** was *not* ОК — the designer reclassified it as БАГ in chat; fixed in the goblin batch. See D7's `**Fixed:**` note.)
