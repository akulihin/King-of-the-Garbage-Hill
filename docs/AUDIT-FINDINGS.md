# Design-vs-Code Audit — Findings

> Audit of `DataBase/characters.json` (+ `Game/GameDesign.txt` intent notes, root-level update notes) against the code, working tree of 2026-07-01 (v4.1.8). Every finding was verified by reading the cited code, not inferred. No code was changed — this is a report for triage. `CP` = `Game/GameLogic/CharacterPassives.cs`.
>
> Severity: **Critical** = player-visible wrong outcome / broken kit promise; **Major** = mechanic silently missing/misfiring or balance-relevant hidden behavior; **Minor** = cosmetic, flavor, dead code, small numeric drift; **Design question** = code self-consistent but intent ambiguous.

## Critical

### C1. Cyrillic "Салдорум" vs JSON name "Salldorum" — four dead branches
- **JSON**: character `Name` is Latin `"Salldorum"` (`characters.json:1316`). Live logic (fights, web actions, two bot cases at `BotsBehavior.cs:1605, 2374`) correctly checks `"Salldorum"`.
- **Dead branches checking Cyrillic `"Салдорум"`** (can never match):
  - `CheckIfReady.cs:137` — end-game "Великий летописец: испорчено N записей" log never prints.
  - `BotsBehavior.cs:328` — bot moral policy (always moral→skill) never applies.
  - `BotsBehavior.cs:1409` — bot attack-preference case never applies (it also reads the *legacy* `SaldorumKhokholList` — stale even if renamed).
  - `BotsBehavior.cs:2662` — bot level-up preference (PSY-first) never applies.
- **Impact**: bot Salldorum plays with untuned moral/level-up/targeting AI; an end-game log is lost. Human play unaffected.
- **Fix direction**: rename the four checks to `"Salldorum"`; rewrite the `:1409` preference for the current kit (Шэн/rewrite), not the Khokhol legacy.
- **Fixed:** 2026-07-03 (pre-approved string bug) — renamed the three `Name == "Салдорум"` checks to `"Salldorum"` (`CheckIfReady.cs:137`, `BotsBehavior.cs:328` moral, `:2672` level-up). The dead `case "Салдорум":` (`BotsBehavior.cs:1409`) was **deleted** rather than renamed — it sat in the same `switch (bot.GameCharacter.Name)` as the live `case "Salldorum":` (`:1605`, the current Chronicler targeting), so renaming would have duplicated the label; its Khokhol-legacy `SaldorumKhokholList` targeting is obsolete. Removed `BAD-NAME|Салдорум|C1` from `tools/known-warnings.txt`. (`SaldorumKhokholList` field is now fully dead — leave for m6.)

## Major

### M1. Goblin "round-10 Ziggurat at place 1 ⇒ win" is a log line, not a win
- **Described** (`GameDesign.txt:508`): "Если на 10м ходу Гоблины строят зиккурат, находясь на 1м месте — они выигрывают."
- **Actual**: `DoomsdayMachine.cs:1488-1499` fires at the **start** of round 10 (after `RoundNo++`), requires place 1 + a ziggurat built at position 1, and only prints "…побеждает!". `HandleLastRound` re-sorts purely by score (`CheckIfReady.cs:382`) with no ziggurat rule. A ziggurat built *during* round-10 processing (`CP:6043-6113`, runs when `RoundNo` is already 11) can never trigger the message.
- **Impact**: the documented win condition doesn't win; a premature "wins!" message can appear a round early and then be false.
- **Fix direction**: enforce at `HandleLastRound` (like Premade's enforced win, `CheckIfReady.cs:472-494`) or drop the message + design line.
- **Fixed:** 2026-07-03 (designer verdict БАГ) — removed the premature/broken log in `CalculateAllFights` (it checked `BuiltPositions` before the round-10 Ziggurat is built) and added an authoritative enforced win in `HandleLastRound` (`CheckIfReady.cs`, right after the Premade block): a non-dead Стая Гоблинов with a Ziggurat at place 1 (`BuiltPositions.Contains(1)`) is bonus-pointed to 1st, re-sorted, and announced — mirroring Premade's overtake.

### M2. "Еврей" web widget renders for Толя with LeCrisp's state
- `GameStateMapper.cs:362-365` keys the widget on passive "Еврей" (both LeCrisp and Толя have it) but fills it from `LeCrispAssassins.AdditionalPsycheCurrent` — LeCrisp-only state. A Толя player sees a dead widget with stolen-psyche 0.
- **Fix direction**: gate on `Name == "LeCrisp"` (pattern: the Геральт case at `:686`).
- **Fixed:** 2026-07-03 — gated the mapper's Еврей case on `player.GameCharacter.Name == "LeCrisp"` (`GameStateMapper.cs:362`, mirroring the Геральт Name gate at `:686`), so the Jew/PROFIT widget is emitted only for LeCrisp; Толя (shares the passive but has no LeCrisp assassin state) no longer gets a dead PROFIT:0 widget. Frontend unchanged — the widget block (`PlayerCard.vue:1180`) renders only when its jew key is present, now undefined for Толя. (The value shown is LeCrisp's assassin-psyche gain, the intended PROFIT display.)

### M3. AWDKA is silently forced to last place for every fight calculation
- `CheckIfReady.cs:1112-1127`: right before bots act (and before all fights), the "Произошел троллинг" holder is moved to the end of `PlayersList` and places re-assigned (comment `//end //AWDKA last`). Score order returns only at end of round.
- **Impact**: during fights AWDKA's place is ~6 regardless of score — inflates his underdog moral, changes Harm kite ranges, place-based passives and bot targeting against him. Documented nowhere.
- **Fix direction**: confirm intent; either document it in the passive description or delete the block (it mirrors the HardKitty "Никому не нужен" block right below it, so it may be a copy-paste leftover).

### M4. Toxic Mate "INT" negative-win rule applies only when he attacks
- `DoomsdayMachine.cs:775-781` negates the winner's point for "Никому не нужен"/"INT" holders only in the attacker-win branch; a defending Toxic Mate who wins gets a normal +1 (`:901-913`), and `CP:2956-2967` adds nothing on wins. JSON: "Побеждая — теряет очки" (unqualified). HardKitty's Mute wording ("если напал и победил") matches the code; INT's does not.
- **Fix direction**: extend the negation to the defense branch for "INT" (or reword the passive).
- **Fixed:** 2026-07-03 (designer verdict БАГ) — added an INT-only negation in the defender-win branch (`DoomsdayMachine.cs:905-908`): a defending Toxic Mate who wins now gets `AddWinPoints(-1)` like the attacker branch. Scoped to `PassiveName == "INT"` so HardKitty's "Никому не нужен" keeps its attacker-only "если напал и победил" behavior.

### M5. "Тигр топ, а ты холоп" has an undocumented second window at game start
- Initial `TimeCount = 3` (`Tigr.cs:10`) **plus** a swap in `HandleEventsBeforeFirstRound` (`CP:170-183`) put Тигр at place 1 immediately, consuming one count; the end-of-round swap (`DoomsdayMachine.cs:1299-1327`) then keeps him there for rounds 2–3. The random trigger (`TigrTopWhen`, rounds 1–8, 1–2 times) later resets the counter to 3 (`CP:4860-4864`) for the *described* "случайный момент" window. Тигр also collects +1 Psyche/+3 Мораль per round at #1 (rounds 2–9, `CP:5822-5829`).
- **Evidence of unintendedness**: designer note `GameDesign.txt:74-75` — "перемена местами должна срабатывать только когда тигр не топ1 (недавно оно вообще будто 2 раза за игру сработало)".
- **Fix direction**: start `TimeCount = 0` and drop the first-round case (keep only the random window), or document the opening window as intended.

### M6. Тигр "Лучше с двумя, чем с адекватными" counts Тигр himself
- `CP:3377-3393` loops `game.PlayersList` without excluding `player` — Тигр's own Int/Psyche trivially match, so at the end of round 1 he pockets +3 bonus points for "recruiting" himself (once, via FriendList dedupe).
- **Fix direction**: `if (t.GetPlayerId() == player.GetPlayerId()) continue;`.

### M7. Butcher pays his point on any win, spec says on a Drop
- the_boys.txt / theboys_update_commit: "+1 point **on drop**" ("очко если удалось его **Скинуть**" — Скинуть is the established Drop term). Code: `CP:3269-3270` awards +1 bonus (+2 SD) whenever Butcher *wins* against a marked sup. Wins are far more common than Drops — balance-relevant.
- **Fix direction**: decide win-vs-drop; if drop, hook into the Harm/Drop path (compare `dropsAfter > dropsBefore` in `DoomsdayMachine.cs:835-876`).
- **Fixed:** 2026-07-03 (designer verdict БАГ — "10 скилла за нападение, и очко если удалось ЕГО скинуть… скинуть при нападении") — the +1 bonus (+2 SD) moved out of the win check (`CP:3273-3274` removed) into the attacker-win Harm/Drop path (`DoomsdayMachine.cs:883-888`): awarded only when `dropsAfter > dropsBefore` and the attacker is Butcher and the target has `TheBoysSupMark`. The +10 Skill hunt bonus (win or loss) stays in the CP "Butcher" case.

### M8. Toxic Mate "Tilted" rewards skips, not "психует"
- JSON: "Получает бонусное очко каждый раз, когда кто-то __психует__". Code (`CP:4226-4234`): +1 bonus per enemy whose `IsSkip` is set at end of round — no connection to psyche-loss ("психанул") events at all. (The +50 "все не смогли походить" half matches, `CP:4236-4241`, including the intentional "+20" joke log.)
- **Fix direction**: hook the +1 into `MinusPsycheLog`/psyche-rage events, or reword the passive to "за каждый пропуск хода".
- **Fixed:** 2026-07-03 (designer verdict БАГ — skips only pay when no battle happened) — removed the +1-per-skip bonus entirely; the payout is now the single **+50** given only when the whole round had **zero battles** (`game.PlayersList.All(x => x.Status.IsWonThisCalculation == Guid.Empty)`, `CP:4231-4239`). Uses the fight-resolution signal rather than the old all-blocked/skipped proxy, so forced fights (Монстр/Штормяк) correctly suppress the payout. Kept the intentional "+20" joke log; JSON text untouched.

### M9. Котики "Кошачья засада" (Штормяк) eats half of *total* score
- JSON: "сожрёт половину очков, которые враг получил **пока на нём сидел** этот кусок кота". Code (`CP:3099-3111`): on the return win, victim loses `Floor(GetScore()/2)` — half of their **entire score**, regardless of when it was earned (no snapshot at deploy time exists).
- **Impact**: a late Storm return can wipe 20+ points instead of the earned-while-sat handful — swingiest single effect in the game.
- **Fix direction**: snapshot the victim's score at deploy (`KotikiAmbush`) and halve the delta.
- **Fixed:** 2026-07-03 (designer verdict БАГ) — added `AmbushClass.StormScoreSnapshot` (`Kotiki.cs:21`), captured at Storm deploy (`CP:3174-3176`) and reset on return (`CP:3112`); the return steal is now `Floor((currentScore − snapshot) / 2)`, i.e. half of what the victim earned while the cat sat, not half of their total score (`CP:3099-3114`). Non-positive delta steals nothing; the −1 Psyche on the win is unchanged.

### M10. Premade's anti-skip un-bans a round-10-banned Carry
- JSON: "Carry никогда не пропустит ход. **(кроме банов)**". Code (`CP:5685-5698`) clears *any* involuntary skip (`IsSkip && !ConfirmedSkip`) on the Carry — including Тигр's round-10 "Стримснайпят и банят" ban (`CP:4842-4849` sets exactly that state) and Школьник's brother-ban. The freed Carry may then act on round 10 despite being "banned" (other systems — targeting refusal, Тигр-топ suppression — still assume he's banned).
- **Fix direction**: skip the anti-skip when the skip source is a ban (e.g. check the ban passive + round, mirroring `CheckIfReady.cs:1270`).
- **Fixed:** 2026-07-03 (designer verdict БАГ "добавляй") — the Premade anti-skip (`CP:5696-5704`) now computes `markedIsBanned` (round 10 + "Стримснайпят и банят и банят и банят") and leaves a banned Carry skipped. Scoped to the canonical Тигр round-10 ban (the "ban" the description's "кроме банов" means); ordinary involuntary skips (Митсуки no-PC, АФКА) are still lifted.

## Minor

### m1. "Вампур_" typo kills a flavor Easter egg
- `GameUpdateMess.cs:1424` checks `Name == "Вампур_"` (JSON: "Вампур") — the garlic level-up placeholder never shows.
- **Fixed:** 2026-07-03 (pre-approved string bug) — `Name == "Вампур_"` → `"Вампур"` (`GameUpdateMess.cs:1424`); removed `BAD-NAME|Вампур_|m1` from `tools/known-warnings.txt` (audit re-run: no reappearance).

### m2. "Vampyr Позорный" logic is commented out
- `GameReactions.cs:994-1000` (level-up denial) disabled; only the phrase object remains. Remove or restore.
- **Fixed:** 2026-07-03 (designer verdict — Вампур не должен прокачивать статы; если качает — забрать) — **restored** the block (`GameReactions.cs:994-1000`): a Вампур level-up sets `skillNumber = 0`, so the stat switch adds nothing (the point is still spent at `:1134`) and "Никаких статов для тебя" is logged. Вампур has the `Vampyr Позорный` passive (`characters.json:588`), so the check is live, not a GHOST. Gematophagia bites (a separate win-reward mechanic) are unaffected.

### m3. Young Gleb transform keeps `Name == "Глеб"` → three misfiring Name checks
- Transform (`GameReactions.cs:256-268`) deliberately doesn't set the name (mylorik's Акула transform at `CP:6003-6010` *does*). Consequences: `GameUpdateMess.cs:1214` "Понизить один из статов" caption never shows post-transform; `CheckIfReady.cs:427` AWDKA-trolling flavor for Молодой Глеб unreachable; `GameReactions.cs:1125` old-Gleb psyche-10 phrase can fire for the transformed character. (`GameStateMapper.cs:292` / `GameUpdateMess.cs:1595` guards are harmlessly always-true.)
- **Fixed:** 2026-07-03 — kept the deliberate design (the transform leaves Name as Глеб so prediction, bot AI and Geralt logic keep matching) and repointed the three cosmetic sites to the young form's Main Ирелия passive — the unique marker the level-up nerf already uses (verified single occurrence in characters.json; Глеб lacks it). The level-up caption (`GameUpdateMess.cs:1214`) and the AWDKA-troll line (`CheckIfReady.cs:427`) now show the young-form text when that passive is present; the sleeping-Gleb psyche-10 phrase (`GameReactions.cs:1125`) is suppressed for it. Added a warning comment at the transform (`GameReactions.cs:258`) not to uncomment the rename. The level-up *mechanic* was never affected — the nerf keys on the Main Ирелия passive, not the Name — so this was cosmetic only. The two always-true guards (`GameStateMapper.cs:292`, `GameUpdateMess.cs:1595`) were left as-is per the finding.

### m4. `PassivesClass.GlebSkip` declared as `bool … = new()`
- `PassivesClass.cs:91` — compiles to `false`; clearly unintended syntax.
- **Fixed:** 2026-07-03 — changed the initializer to `= false` (`PassivesClass.cs:91`). It was a copy-paste of the surrounding reference-type `= new()` lines; for a bool `new()` already yields `false`, so no behavior change — GlebSkip is a plain flag (set at `CP:469`, tested/reset at `CP:2602/2687`). Pure clarity fix.

### m5. Exploit rotation runs in games without Баг
- `GameClass.RollExploit` + `DoomsdayMachine.cs:73-76` rotate/count exploit state even when nobody can consume it. Harmless bookkeeping.
- **Fixed:** 2026-07-04 — `RollExploit` now early-returns when no Баг player is in the game (`ExploitPlayersList.Count == PlayersList.Count`, i.e. nobody holds the "Exploit" passive; `GameClass.cs:143-146`). The list is rebuilt on the draft path (`CheckIfReady.cs:1025`) and `HandleNextRound` re-rolls after it, so the gate stays correct for drafted Баг. No observable change (both the Discord "EXPLOIT N" flair and the web ExploitState were already viewer-gated on holding "Exploit"); the rotation just no longer flips flags nobody reads.

### m6. Dead legacy code catalogue
- `LolGod.cs` + `PassivesClass.LolGodUdyrList` — "Бог ЛоЛа" doesn't exist; the only live reference is an always-true guard inside Darksci's "Не повезло" (`CP:2808`).
- `Saldorum.cs` (single-L "Хохол" design) vs live `Salldorum.cs`: orphaned cases "Парень с сюрпризом" (`CP:860, 2161`), "Сало" (`CP:874, 2175`; `GameUpdateMess.cs:634`), "Ниндзя" (`CP:1423, 2194`) — those passive names exist in no character and are never added at runtime.
- `CraboRack.BokoBoole` (in `CraboRack.cs`, pre-deletion lines 16-19) — zero references.
- "Молодой Глеб" JSON entry has `Tier: -2` — excluded from the roll pool by `CharactersPull.GetRollableCharacters` (Tier ≥ −1, `CharactersPull.cs:43-50`); transform template only. Note the tier semantics (`CharactersPull.cs:28-32`): **Tier −1 = secret but rollable** — Sakura and Баг do roll for humans (range 40; bots never roll tier <4) while staying hidden from prediction menus. *(Corrected in verification — originally attributed to a range-0 roll.)*
- **Fixed:** 2026-07-04 — deleted: `LolGod.cs` and `Saldorum.cs` (whole files); the six orphaned GHOST cases (CP defense «Парень с сюрпризом»/«Сало», before-fight «Ниндзя», attack «Парень с сюрпризом»/«Сало»/«Ниндзя») and the «Сало» display case in `GameUpdateMess.cs`; the commented-out "LOL GOD, EXAMPLE" block (the "Бог ЛоЛа" BAD-NAME source) inside Darksci's «Не повезло»; dead state `PassivesClass.LolGodUdyrList`/`SaldorumKhokholList`/`SaldorumNinjaHidden` (live `SaldorumCorruptionCount` kept); dead phrases `SaldorumSurprise`/`SaldorumSalo`/`SaldorumNinja` (live `SaldorumChronicler` kept); `CraboRack.BokoBoole`. Removed the four `|m6` lines from `tools/known-warnings.txt` (audit re-run: clean). The Молодой Глеб tier note is informational — no change. In-code anchors above are historical (pre-deletion coordinates).

### m7. Stale comments (cosmetic)
- `CalculateRounds.cs:27` says TooGood sets "75 or 25" — code sets 70/30 (`:238, :249`).
- `CP:642` comment says tunnel escape is 33% — code rolls 50% (`:645`).
- **Fixed:** 2026-07-04 — the tunnel-escape comment now says 50% (`CP:640`). The `CalculateRounds.cs:27` half was already fixed in an earlier change-set (the comment reads "(sets 70 or 30)"); no code values touched, comments only.

### m8. Толя "Подсчет" recharge is 4–5 rounds, description says 2–3
- Initial cooldown *is* 2–3 (`PassivesClass.cs:31`), but after each use `Cooldown = Random(4,5)` (`CP:1065`), decremented once per round (`CP:5969-5978`). Net: ~2 uses per game instead of ~3.

### m9. Итачи Цукуеми recharge is 4 rounds, description says 2
- On activation `ChargeCounter = -2` (`CP:2939`); +1 per round (`CP:4119-4125`) → 4 rounds to full. Initial charge (0→2) matches the described 2.

### m10. Francie Хим.оружие ignores enemy-difficulty scaling
- Design note (the_boys.txt): "(normal +1, toogood +1, toostronk +1; если мы ту-гуд/стронк = +0) × прокачки". Code (`CP:3221-3230`): flat `chemLevel` bonus, zeroed when TheBoys were TooGood/TooStronk vs the victim. The "harder enemy pays more" half is missing.
- **Fixed:** 2026-07-03 (designer verdict БАГ "добавляй") — the bonus is now `chemLevel × (1 + (enemy TooGood for TheBoys ? 1 : 0) + (enemy TooStronk ? 1 : 0))` (`CP:3224-3238`), read from the attacker's `FightEnemyWasTooGood/Stronk` flags (= "my enemy was too good/stronk"). Normal enemy ×1, harder enemy ×2 (the TooGood/TooStronk tiers are set in exclusive threshold branches in `CalculateRounds`, so a win vs either pays double). The existing "+0 if TheBoys was TooGood/TooStronk vs the victim" gate is unchanged.

### m11. Ziggurat costs differ from the design note
- Code (`CP:6048-6093`): requires ≥1 of each type **and score ≥ 3** (undocumented gate), costs −3 bonus + a *permanent* −1 Worker deduction. Design note: "умирает 1 Трудяга (т.е. если каждый 9й — Трудяга, то умирает 9 гоблинов)" — i.e. population loss, not a permanent worker-slot loss. Current implementation is milder early, harsher late.
- **Fixed:** 2026-07-03 (designer verdict: build needs **>3** points, and exactly **−1 Трудяга** is correct — description unchanged) — the score gate `GetScore() < 3` became `<= 3` (`CP`), so the build now requires strictly more than 3 points; the −1 permanent Worker deduction was already the intended behavior and is kept. Documented the >3 gate in CHARACTERS.md (not in the player-facing text, by design).

### m12. Saitama's round-1 "serious targets" are effectively arbitrary
- SeriousTargets = top-2 by `GetSkill()` (`CP:242-251`), which at game start is 0 for almost everyone → stable-sort picks the first two in list order. Recomputed properly from the end of round 1 (`CP:3933-3942`). Also note "боевая мощь" = skill only (stats ignored) — Кратос-style stat monsters are never "serious".

### m13. HardKitty's opening −30 is score, logged as Мораль
- `CP:159-161`: `HardKittyMinus(-30)` lowers **Score** by 30 (bypassing the floor), while the personal log says "Никому не нужен: -30 *Морали*". One of the two is wrong; players reading the log get misdirected.

### m14. Butcher sup marks only exist from round 2
- Marks are assigned in `HandleNextRoundAfterSorting` (`CP:5770-5804`), which first runs at the end of round 1 — no sups (not even superheroes) during round 1. Probably fine; worth one line in the passive text if intended.

### m15. Salldorum's history rewrite ignores the round multiplier and the Еврей redirect
- Design note (`GameDesign.txt:549`): steal "(1 × множитель раунда)" from each winner, "Но следи за Евреями. Если они украли эти очки — то отнимается у евреев". Code (`WebGameService.cs:932-943`): flat −1/+1 bonus per winner, no multiplier (the comment even says "could scale"), no Jew redirection.
- **Fixed:** 2026-07-03 (designer verdict БАГ — "сделай как должно быть по описанию") — the steal is now `1 × roundMultiplier(rewrittenRound)` (`roundNumber switch { <=4 => 1, <=9 => 2, _ => 4 }`, `WebGameService.cs:934-951`), and each stolen point is reclaimed from a Jew (Еврей) who **co-won** that round (pocketed the winner's point via `HandleJews`) rather than the direct winner. Best-effort redirect: a Jew who attacked-and-lost but still stole cannot be reconstructed from `WhoToLostEveryRound` (would need steal-time tracking) — noted in CHARACTERS.md.

### m16. Геральт's Lambert fumble is 20%, design note says 10%
- `CP:4396` (`_rand.Luck(20)`, one-time) vs `GameDesign.txt:654` "10% Шанс". Also worth knowing: the meditation hint for human players calls the Anthropic Haiku API synchronously inside the round pipeline (`CP:4365-4381`) with a static fallback.
- **Fixed:** 2026-07-03 (designer verdict 10%) — `_rand.Luck(20)` → `_rand.Luck(10)` (`CP:4398`); BALANCE-CONSTANTS row updated. (`Luck(p)` with no range = `p >= rand(0,100)` ≈ p%.)

### m17. Dopa "Взгляд в будущее" also procs on blocks
- Proc condition (`CP:4163-4167`): either dual-target attacked the other **or either target blocked**. The description only promises the "attacked his next target" case. Lenient in Dopa's favor.
- **Fixed:** 2026-07-05 — removed the two `IsBlock` proc lines (`CP:4174-4175`); Vision now fires only when one of Dopa's two targets actually attacked the other, matching the description. Also removed the stale "Vision triggers on block too" Фарм bot heuristic (`BotsBehavior.cs:1338-1340`). CHARACTERS.md / BALANCE-CONSTANTS.md re-anchored `CP:4150-4177` → `CP:4150-4175`.

### m18. "Привет со дна" counts skip *events*, not skipping players
- `CP:3606`: bonus = `game.SkipPlayersThisRound` (incremented once per skipped **fight**, `DoomsdayMachine.cs:530` — two attackers into one skipper = 2) + count of blockers. Mildly inflated vs "когда кто-то пропускает ход".
- **Confirmed intended** 2026-07-04 (designer verdict «ОК — по событиям»: каждый сорванный бой = очко). No code change; exact per-event behavior documented in CHARACTERS.md.

### m22. Latin "Saitama" vs JSON "Сайтама" — dead "👑 King" flair
- `GameUpdateMess.cs:713`: Saitama's leaderboard view should mark the current #1 as "👑 King", but the check is `Name == "Saitama"` while the character is named "Сайтама" — never renders. Same bug family as C1/m1. *(Found by `tools/audit-passives.sh` on its first run.)*
- **Fixed:** 2026-07-03 (pre-approved string bug) — `Name == "Saitama"` → `"Сайтама"` (`GameUpdateMess.cs:713`); removed `BAD-NAME|Saitama|m22` from `tools/known-warnings.txt` (audit re-run: no reappearance).

### m23. Dopa's `dopa-attack-select` menu is dead UI — selections silently ignored
- `GetDopaMenu` builds the second-action select with custom-id `dopa-attack-select` (`GameUpdateMess.cs:1384-1431`) and it is attached for the "Dopa" passive in the game-buttons builder (`GameUpdateMess.cs:1652-1654`), but the component dispatch switch (`GameReactions.cs:157` through `GameReactions.cs:417-421`) has **no case for it** — a click defers and nothing happens.
- The working Макро second action flows through the regular attack/block handlers instead (`GameReactions.cs:730-737`, `GameReactions.cs:329-352`), so the menu is pure decoration that looks interactive. *(Found 2026-07-04 during the interface-docs audit; docs/DISCORD-INTERFACE.md §5.)*
- **Fix direction**: delete `GetDopaMenu` + its attach (Макро already works via `attack-select` and `block`), or route the custom-id into `HandleAttack`.
- **Fixed:** 2026-07-04 — deleted `GetDopaMenu` and its `case "Dopa":` attach from `GameUpdateMess.cs` (Макро's real second action already flows through the regular `attack-select`/`block` handlers). Verification correction to the finding: the menu never actually **rendered** — the attach switch iterates `passive.PassiveName` and no passive named "Dopa" exists in `characters.json` (the character's passives are Макро/Пассивный импакт/…), nor is one added at runtime, so the `case "Dopa":` was itself dead and the select was unreachable UI rather than silently-ignored UI. Removed the `dopa-attack-select` row + §11 quirk line from `docs/DISCORD-INTERFACE.md`; downstream `GameUpdateMess.cs` anchors in the docs re-pointed (−49/−53 lines).

### m24. ARAM pick phase has no web UI (hub methods exist, screen doesn't)
- The backend and contract fully support web ARAM picks: `AramReroll`/`AramConfirm` on the hub (`GameHub.cs:332-352`) and REST (`GameController.cs:159-177`), `isAramPickPhase` + reroll counters serialized (`GameStateMapper.cs:945-963`), store wrappers wired (`game.ts:439-447`) — but **no Vue component calls them**; Game.vue's phase branches cover only the Draft overlay.
- During an ARAM game a web-preferring player sees only the waiting screen and must reroll/confirm from the Discord ARAM page (`GameUpdateMess.cs:1612-1636`). *(Found 2026-07-04 during the interface-docs audit; docs/WEB-CLIENT.md §13.)*
- **Fix direction**: an ARAM overlay in Game.vue mirroring the draft overlay (buttons → the store's `aramReroll` slots 1-5 / `aramConfirm`), or suppress the web-link DM during ARAM picks.

### m25. audit-passives.sh truncated PASSIVE-MAP.md when killed by the hook timeout
- The script wrote the report **directly into `docs/PASSIVE-MAP.md` while generating it**, and its per-passive greps took ~90 s on a `/mnt/*` (WSL2 9P) checkout — longer than the 60 s `PostToolUse` hook timeout (`.claude/settings.json`; the hook re-runs the audit after every edit to a passive-bearing file, `tools/hook-post-edit.sh`). A killed hook run left a clean-looking but truncated map (committed history oscillates: 171 → 119 → 171 → 118 table rows across `a17d86e`/`c6762f0`/`ce6a772`/`e0f7384`) and silently dropped the GHOST/BAD-NAME sections — which also broke the hook's own new-warning diff on the next edit. *(Found 2026-07-04 when the user noticed ~53 rows vanish from the map.)*
- **Fixed:** 2026-07-04 — two changes to `tools/audit-passives.sh`: (1) **atomic write** — the report is generated into `$OUT.tmp.$$` and `mv`-ed over the map at the end, so a killed run can never leave a partial file; (2) **single-pass indexing** — owners (one `jq`), CP case counts (one `grep -oP | uniq -c`) and cross-file refs (one `grep -HoF -f patterns` over the code list) are pre-computed into assoc arrays instead of ~350 per-passive greps. Runtime 86 s → **2.3 s**; output verified byte-identical to the last complete map (`ce6a772`) modulo the m6 deletions, and deterministic across runs.

### m21. `SecureRandom` is not secure (naming hazard)
- `Helpers/SecureRandom.cs:25-45`: the crypto implementation is commented out; the service is a plain `System.Random` wrapper. Fine for a game, but the name misleads — and `PassivesClass` carries a private copy that *does* use `RandomNumberGenerator` (`PassivesClass.cs:277-296`), so trigger schedules are crypto-random while combat rolls aren't. Unify or rename.
- **Fixed:** 2026-07-04 (user chose **unify**) — one RNG for the whole game: `SecureRandom` gained a static core `Next(min,max)` wrapping `RandomNumberGenerator.GetInt32` (thread-safe — the old shared `System.Random` instance wasn't); the instance `Random` and `Luck` delegate to it, and the `PassivesClass` private crypto copy was deleted (ctor Толя-cooldown roll + `GetWhenToTrigger` now call `SecureRandom.Next`). All call-site semantics preserved exactly: inclusive max, the `Random(n, n−1) → n` edge (relied on by `GetWhenToTrigger(…, range 0)`), Luck's 0–100 roll. Out of scope, documented in ARCHITECTURE §9: the handful of direct `new Random()`/`Random.Shared` sites (exclusive-max semantics; converting them risks off-by-ones for no behavioral gain). In-code anchors above are historical.

## Design questions

### D1. Darksci can dodge "Дизмораль" by hoarding the round-9 level-up
- The −5 Psyche fires only inside `GetLvlUp` while `RoundNo == 9` (`GameReactions.cs:1226-1231`); saving the point until round 10 skips it (and the psyche-0 skip check). Bots always spend immediately. Intended tech or loophole?
- **Resolved 2026-07-03 (designer chose consistency, reversing the earlier «ОК»)**: the hoard was only ever possible on the WebUI via the level-up banking bug (M15) — on Discord the forced level-up page and the round-end auto-move both spend the point in round 9. M15's general web gate closes it, so Darksci now eats the −5 on both platforms.

### D2. Goblin Ziggurat can duplicate "Еврей" (and other Standalone passives)
- LeCrisp's "Еврей" is `Standalone: true` (`characters.json:140`), so Goblins can learn it (`CP:6077-6087`) despite the roll-time LeCrisp/Толя exclusivity (`StartGameLogic.cs:180-194`). `HandleJews` supports multiple jews (`CP:6594-6672`), each earning +1 while the victim's point is suppressed once. Verify which Standalone passives are safe to copy (full matrix in the Phase-3 audit).
- **Fixed:** 2026-07-03 (designer verdict ЗАПРЕТИТЬ) — the Ziggurat copy filter (`CP`, `standalonePassives` where-clause) now excludes `PassiveName == "Еврей"`, so Goblins can never learn it. Other `Standalone` copies are left as-is per D10.

### D3. Sakura's "Одна из трех" is a narrative win only
- `CheckIfReady.cs:496-508`: top-3 Sakura is declared `playerWhoWon` (logs, phrases; ZBS-100 only if scores tie) but keeps her real place for stats/mastery/TotalWins. Note she *is* a rollable secret character (Tier −1, m6) that nobody can predict — the soft win may be the intended compensation; confirm.
- **Fixed:** 2026-07-03 (designer verdict: place stays by fact, stats & rewards as 1st place) — the payout loop now computes a per-player `rewardPlace` = 1 for the top-3 `top3Player` (Sakura) else the real place, and keys TotalWins, mastery, ZBS, the top-2 loot box, and per-character Wins off it (`CheckIfReady.cs:631-728`). Her `GetPlaceAtLeaderBoard()` and MatchHistory record her real finish. The actual 1st-place player is unaffected (still gets their 1st-place payouts).

### D4. Passives whose logic keys on character Name, not the passive
- "Булинг": DeepList's "Стёб" spares LeCrisp by name (`CP:2514-2532`); `HandleJews` skips stealing from DeepList by name (`CP:6621-6625`).
- "Го играть": the block/skip bypass vs friends is implemented inside "Заводить друзей" (`CP:1193-1204`) — the passive named "Го играть" has zero references of its own.
- "lvl-мяк": the +1-Justice level-up is `Name == "Котики"` (`GameReactions.cs:897-904`).
- All three work today but break silently on rename/transfer; consider keying on the passive names. (Also the inverse hazard: transferred Standalone passives *do* dispatch for new holders — e.g. Ziggurat copies.)

### D5. "2kxaoc" exists only to stay visible
- Its only special handling: `GameUpdateMess.cs:798-811` masks *other players'* passive names in the stats display ("Неизвестно"/"❓ …"), and "2kxaoc" is one of four names **exempt from masking** (with Запах мусора, Чернильная завеса, Еврей) — the meme is deliberately left readable. No gameplay effect; confirm none is intended. *(Corrected in verification — the original finding described this backwards.)*

### D6. Вампуризм copies Justice instead of draining it
- "подсасывает себе **всю** Справедливость цели" — code adds the target's current Justice to Вампур's next-round buffer (`CP:1842-1846`) but never removes it from the target (contrast: Kimiko's Живое Оружие and Близнец, which zero the victim's Justice — `CP:744-755, 875-889`). Confirm copy-vs-drain.

### D7. External stat changes on Стая Гоблинов are overwritten every round
- `CP:6028-6030` re-`Set`s Str/Int/Psyche from population each round end — debuffs like Спартанец's −1 Str vanish; Speed debuffs persist (Speed isn't population-driven). Inherent to the population design; document or special-case.
- **Fixed:** 2026-07-03 (designer verdict БАГ — external debuffs should persist) — added `GoblinPopulationClass.LastApplied{Str,Int,Psyche}Base` and a shared `ApplyGoblinPopulationStats` helper (`CharacterPassives.cs`) used at both the before-first-round init and the end-of-round recompute. It sets each stat to `populationBase + externalDelta`, where `externalDelta = currentStat − lastAppliedBase` (0 on the first run), so external Str/Int/Psyche changes now carry across the recompute like Speed already did.

### D8. "Пейзаж конца света" +7 очков is round-multiplied ×4
- The non-pawn reward for attacking Монстр on round 10 is +7 **regular** points (`CP:4329`) — committed with the round-10 ×4 multiplier = effectively **+28** (+10 bonus on top). If "+7 очков" was meant literally, use bonus points.

### D9. Premade copies the Carry's fight-moral instead of transferring it
- `CP:2366-2369`: the Support `AddMoral(carryMoral)` while the Carry keeps theirs. JSON "Добываемая в боях Мораль так же передается" reads as a transfer. Same copy-vs-drain question as Вампуризм (D6).

## Phase 3 — cross-character interactions

### M11. Шэн and Штормяк forced attacks ignore the round-10 Тигр ban
- Монстр's no-escape has an explicit ban carve-out (`CheckIfReady.cs:1266-1271`). The other two forced-fight sources don't:
  - **Шэн** (`CheckIfReady.cs:1184-1199`) forces everyone below the position to attack Salldorum — a round-10-banned Тигр (IsSkip, stats nuked) gets Salldorum added to `WhoToAttackThisTurn`, and the fight loop processes forced fights even for skipping players (`DoomsdayMachine.cs:393-407`) — the "banned, can't act, can't be targeted" promise breaks (0-stat Тигр is forced to fight).
  - **Штормяк taunt** (`CheckIfReady.cs:1217-1251`) excludes dead players but not the banned Тигр — he can be provoked into attacking Котики on round 10.
- **Fix direction**: reuse the Монстр carve-out condition in both sites (and consider dead-player checks ✓ already present).
- **Fixed:** 2026-07-03 (designer verdict БАГ "если нет исключения, Тигр остаётся в бане") — mirrored the Монстр carve-out `!(game.RoundNo == 10 && …Passive.Any(x => x.PassiveName == "Стримснайпят и банят и банят и банят"))` into the Шэн below-position pull (`CheckIfReady.cs:1213`) and the Штормяк taunt eligible-targets filter (`CheckIfReady.cs:1247`), so a round-10-banned Тигр is no longer forced to fight.

### M12. Монстр's apocalypse can kill Стая Гоблинов
- Every other kill source has an explicit goblin immunity: Кира's note (`CP:4009`), L-arrest (`CP:4686`), Кратос (`CP:1660`). "Пейзаж конца света" pawn deaths (`CP:4304-4315`) have **no** goblin check — Goblins guessed by Монстр become pawns and die on round 10, contradicting `GameDesign.txt:509` "Гоблинов нельзя Убить механикой 'убийства'".
- **Fix direction**: `if (pawn.GameCharacter.Name == "Стая Гоблинов") continue;` in the pawn loop (or block goblins from becoming pawns).

### D10. Ziggurat-copyable (`Standalone`) passives — a risk inventory
The Ziggurat copies any `Standalone: true` passive from the last attacked enemy (`CP:6075-6087`). Current inventory by behavior when a Goblin holds them:
- **Fully functional** (probably intended): Одиночество, Месть, Импакт, Еврей (see D2), Обучение, Лучше с двумя, 3-0 обоссан, Запах мусора, Я пытаюсь!, Произошел троллинг (⚠ also inherits the M3 forced-last!), Неуязвимость, Привет со дна, Лежит на дне, Ничего не понимает, Им это не понравится, Гематофагия, Панцирь, Болевой порог, Хождение боком, Питается водорослями, Оборотень, Безжалостный охотник, Клинки хаоса, Вороны, Изанаги (2 free auto-win defenses!), Аматерасу, Сомнительная тактика (huge self-nerf — must lose first fight vs everyone).
- **Self-brick**: **Булькает** — a Goblin who learns it loses all Мораль/Skill gains including the Ziggurat's own +5 Мораль (`CharacterClass.cs:963, 1010, 1125`). Funny, probably not intended to be learnable.
- **Dead copies** (game-start-only or Name-gated hooks): Лысина, Первая кровь, Похищение души (init-only — no effect when learned mid-game); Ведьмачьи заказы (every case gated `Name == "Геральт"`).
- **Fix direction**: maintain an explicit copyable-whitelist, or at least exclude Булькает and the dead copies.

### D11. Цукуеми × Чернильная завеса double-charges the same point
- If a player beats Осьминожка (ink fake-win: they get +1 now, owe it back at round 11) while under Итачи's Цукуеми, Итачи *also* copies that +1 at end of round and deducts it again at game end (`CP:4089-4117`, `CheckIfReady.cs:363-379`) — the victim repays the same point twice (once to Octopus's restore, once to Итачи). Rare, but both effects are "secretly repaid later" designs that don't know about each other.
- **Fixed:** 2026-07-03 (designer verdict — Итачи и Осьминожка each get their point, victim loses it once; duplicate for two receivers) — the round-11 ink restore (`CP:4773-4790`) now **skips its victim-debit** when that victim is in any Itachi's `ItachiTsukuyomi.StolenFromPlayers` ledger (the Цукуеми deduction at `CheckIfReady.cs:373` charges them instead). Octopus still applies its own `+N` credit (a positive RealScore entry), so the point is duplicated for both receivers while the victim pays once. Best-effort: the ledger is per-victim not per-round, so a victim stolen-from in a *different* round than the Octopus beat would also be skipped — a rare double-edge.

### Phase-3 checks that passed (selection)
Монстр no-escape spares pickle-Рика (he has neither IsBlock nor IsSkip); dead players are excluded from every forced-attack pool; Geralt's contract injection skips while he blocks/skips; Тигр-топ/Portal-Gun/Шэн/Storm-bite/Drops all respect the Ziggurat lock; the Глеб/Молодой-Глеб tea skip spares a charged Portal Gun; Premade anti-skip doesn't touch pickle-Рика; Котики are immune to a *transferred* Storm's taunt and a transferred Минька/Штормяк won't buff against its owner; PointFunnel points bypass Еврей theft (funnel copies only `AddWinPoints`); Октопус's ink correctly debits Евреи who stole the point.

## Phase 4 — plumbing (web/state layers)

### m19. Итачи's Crows and Izanagi are invisible in the web UI
- Only Tsukuyomi state is mapped (`PlayerDto.TsukuyomiState`, `GameStateMapper.cs:321`); crow counts per enemy and remaining Izanagi charges exist only in backend state. Recently-added character with the least UI coverage; every comparable kit (Goblins, TheBoys, Геральт) has a full widget.

### m20. Geralt's "Чеканная монета" demand economy is entirely undocumented
- A full hidden system: post-round demand/advance buttons, invoice totals, a Displeasure ledger, +2 regular per advance, and **death by pitchforks with −500 at Displeasure ≥ 11** (`CP:4454-4491`). Neither `characters.json` nor `GameDesign.txt` mentions displeasure or the death. (The design note's *other* hidden Geralt mechanics: "психует when a contract holder is killed" — **not implemented** anywhere; "dies on place 6" — implemented as a log line only, `CheckIfReady.cs:270-276`.)
- Also of note: the meditation hint calls the Anthropic API synchronously inside the round pipeline (`CP:4365-4381`).
- **Fixed:** 2026-07-05 (documentation-only) — the full demand economy is now documented in `docs/CHARACTERS.md` (Геральт's «Чеканная монета» entry): the two web/bot-only billings (immediate «За прошлый» + deferred «За следующий» advance), the `CalculateInvoice` coin/Displeasure tiers, and the pitchfork death checked in **both** the web handler (`WebGameService.cs:652-660`) and the end-of-round advance resolution (`CP:4483-4490`), plus the place-6 pitchfork *log-only* line. Web plumbing was already covered (WEB-BACKEND.md hub/service rows, WEB-CLIENT.md widget rows, BALANCE-CONSTANTS row, INTERACTION-MATRIX kill-source, GAME-DESIGN end-game). **Two design-note mechanics were deliberately left unimplemented and flagged for a designer decision, not built here:** "психует when a contract holder is killed" (absent anywhere) and "dies on place 6" (log line only). `characters.json`/`GameDesign.txt` are the designer's surface — not edited (CLAUDE.md).

### Plumbing checks that passed
Every `PassiveAbilityStatesDto` member is mapped and rendered (no dead DTO/TS fields); mapper `case` strings all exist in `characters.json` (the only orphans are the legacy Saldorum combat cases, m6); per-player marks (SellerMark, virus, cancer, cat, pawn, monster-type, sup) all follow the SellerMark pattern end-to-end; `Sakura`/`Кратос` intentionally have no widgets; `Баг` state rides on `PlayerDto` (ExploitState) by design.

## Verified-consistent highlights (no finding)

Worth stating because they're easy to suspect: Saitama's Неприметность deferral/reclaim matches the recent fix note exactly (incl. the manual round-10 moral flush, `CP:4750-4757`); Rick's pickle/portal player-control flow matches rick_update (bot never takes over, pickle stays attackable, gun charge music one-shot); the Тигр round-10 ban is respected by targeting, Монстр forced attacks and the Тигр-топ swap; Кира's +2/+4(L) numbers, 25-Мораль eyes cost, L-arrest from round 8 and −500 all match; Goblin growth/death percentages match the goblins commit (death is 10+0.5R²/3%, the *older* design note's 1·R²/3 was rebalanced); Ziggurat Standalone-only learning with duplicate protection; Выгодная сделка pays both the +1 bonus per deal and +5 Мораль per deal; Октопус's ink ledger correctly redirects debits to Евреи who stole the point; the Глеб-tea skip spares a charged Portal Gun ("ничто не помешает").

## Phase 5 — simulation harness & bot robustness

### M13. Discord service-channel debug calls freeze headless sims and mask the real exception
- Four diagnostic `_global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340).SendMessageAsync(...)` calls in the per-tick hot path were **not** null-guarded. In headless `--sim` (and during any live Discord reconnect) `GetGuild(...)` returns null → NullReferenceException. Sites: `BotsBehavior.cs:2484` ("Поставил блок, а ему нельзя", only when `maxRandomNumber>0` — the throw skips the attack at `:2490`), `BotsBehavior.cs:2497` ("не напал ни на кого" — the throw skips the force-block at `:2500`, leaving the bot with no action), the `HandleBotAttack` catch `BotsBehavior.cs:2539-2545` (ran the Discord send **before** `_logs.Critical`, masking the real exception and never calling `SimErrorSink`), and `CheckIfReady.cs:1261` ("didn't do anything" fallback — the throw is caught by the outer catch at `:1355`, aborting round resolution before `game.IsCheckIfReady = true` at `:1353` → round never completes → 30 s watchdog marks the game STUCK). The already-correct sibling at `CheckIfReady.cs:1360-1370` shows the intended pattern; the fix was never propagated.
- **Impact**: ~1/1000 sim games froze (sweep-20260702-191714 hit 5 batches) and the true exception (M14) was invisible — every recorded error read as the generic `CheckIfReady.cs:1261` NRE. In production a Discord reconnect at the wrong moment can freeze a live game the same way until reconnect.
- **Fixed:** 2026-07-03 — added null-safe `Global.TrySendServiceMessage(string)` (no-ops when guild/channel is null), routed all unguarded service-channel sends through it (`BotsBehavior.cs:2484/2497`, `CheckIfReady.cs:1261`, and the three unguarded ELO sends `CheckIfReady.cs:830/849/868`), and reordered the `HandleBotAttack` catch to `_logs.Critical` + `SimErrorSink?.Invoke(game.GameId, game.RoundNo, e)` **first**, then the guarded send. The two already-guarded siblings (`:876`, `:1362/1364`) were left as-is. Verified: previously-freezing line-up 0 stuck; 5000-game natural sim 0 errors/0 stuck.

### M14. Bot `HandleBotAttack` throws IndexOutOfRange when it has no valid targets
- `BotsBehavior.cs:2481`: the random-attack fallback does `players[_rand.Random(0, players.Count - 1)]` with `players = allTargets.ToList()`. When `allTargets` is empty — all other players dead, or removed by the round-10 Тигр-ban / round-9-10 "Нахуй эту игру" filters (`BotsBehavior.cs:506-527`) — `players.Count - 1 == -1` and the list indexer throws `ArgumentOutOfRangeException`. Кира-correlated (Death-Note kills shrink the pool late-game); observed rounds 8–10. Was masked into a frozen game by M13; surfaced once M13 was fixed.
- **Impact**: the bot's turn crashes. Post-M13 it is recorded as a per-game sim error; in production the bot fails its action and auto-blocks (`CheckIfReady.cs:1253` fallback) plus debug-channel spam.
- **Fixed:** 2026-07-03 — guard `if (players.Count == 0)` → block (`_gameReaction.HandleAttack(bot, null, -10)`) + `ResetTens(allTargets)` + `return`, mirroring the block-and-return at `BotsBehavior.cs:2455-2461`. Verified: Kira line-up 500 games → 0 errors/0 stuck. (Related latent `allTargets.First()` at the `RoundNo<5`-guarded early path left as-is — unreachable with an empty pool that early.) **See M16** — this guard sits at the `:2476` fallback, which runs *after* the per-character switch; the `case "Братишка"` `.Min()` at `:2065` was a sibling empty-pool site it did not cover (surfaced by sweep-20260703-143146).

### M15. WebUI let players bank level-up points instead of spending before continuing
- On Discord a granted level-up (rounds 3/5/7/9, `DoomsdayMachine.cs:1392-1396`) flips `Status.MoveListPage` to 3, so `GameUpdateMess.cs:1777` renders only the level-up menu — the fight controls are hidden until the points are spent (page reset at `GameReactions.cs:1221`). The web action handlers never inherited that gate: `Attack`/`Block`/`AutoMove`/`ConfirmSkip` (`WebGameService.cs:390/422/472/516`) set `IsReady` regardless of `LvlUpPoints`, and the only guard was character-specific (`Main Ирелия`, four copies at `:400/428/477/521`). A web player could act (attack/block/skip — none set `IsAutoMove`, so they escape the round-end auto-spend in `HandleBotBehavior`, `BotsBehavior.cs:56-57`) while carrying level-up points into a later round.
- **Impact**: platform inconsistency and a real advantage — banking points to dump on demand, and dodging the round-9 Дизмораль −5 Psyche (see D1). Discord-impossible; WebUI-only.
- **Fixed:** 2026-07-03 — generalized the `Main Ирелия` guard into `WebGameService.LevelUpGate` (blocks any `LvlUpPoints > 0` from the four turn-ending actions; Ирелия keeps "Риоты не прощают, нерфа не избежать", everyone else gets "Остались очки прокачки — потрать их!"). Mirrored client-side: `store/game.ts` adds a `mustSpendLevelUp` computed + early-returns in `attack/block/autoMove/confirmSkip`; `pages/Game.vue` disables Block/Auto/Skip, tightens the Leaderboard `:can-attack`, and shows the same prompt. No soft-lock: every character always has an enabled level-up button in `PlayerCard.vue` while points remain, and the round-end auto-move force-spends any leftover.

### M16. Bot `HandleBotAttack` throws "Sequence contains no elements" in the Братишка per-character switch on an empty target pool
- `BotsBehavior.cs:2065`: `case "Братишка"` runs `allTargets.Min(x => …Justice.GetSeenJusticeNow())` to nudge its block chance. When `allTargets` is empty this throws `InvalidOperationException: Sequence contains no elements`. Same empty-pool family as M14 (opponents dead + round-10 Тигр-ban `:510-516` + round-9/10 "Нахуй эту игру" filter `:518-527`), but M14's guard sits at the random-attack *fallback* (`:2476`), which runs **after** the per-character switch (`:1977+`) — so the `.Min()` throws first and never reaches it. Кира-correlated (Death-Note kills empty the pool late-game); all three `sweep-20260703-143146` failures were round 10 with the Братишка + Тигр + Кира trio (games 889, 1876, 1629).
- **Impact**: the Братишка bot's turn crashes on round 10 when no legal target remains. Post-M13 it is recorded as a per-game sim error (3/100300 in the sweep); in production the bot fails its action and auto-blocks (`CheckIfReady.cs:1253` fallback).
- **Fixed:** 2026-07-03 — wrapped the Justice-min block-nudge in `if (allTargets.Count > 0)`; with no targets the nudge is moot and the bot blocks via the existing `isBlock == 0` → block-and-return path (`:2450`), matching every other character in that state. No other switch case consumes `allTargets` non-empty-safely; the latent `:858` (`RoundNo<5`-guarded) and `:2535` (`.Any()`-guarded) sites are unaffected.

### m26. `HandleBotAttack` scoring flags are never reset inside the per-target loop
- The boolean flags declared once per `HandleBotAttack` invocation (`isTargetTooGood`, `isLostLastRoundAndTargetIsBetter`, `isJusticeTheSame`, `isTargetFirst`, … `BotsBehavior.cs:567-575`) latch to `true` when set for any target and are **never cleared between iterations** of the `foreach (var target in allTargets)` loop. A handful of per-character compensations read these flags *after* the loop or on a later target and so can fire for a target that never actually received the penalty — e.g. mylorik's compensation (`BotsBehavior.cs:988`) and Глеб's (`:1149`) key off `isLostLastRoundAndTargetIsBetter`.
- **Impact**: mild bot mis-weighting in mixed line-ups; not player-visible and not exception-producing. Flagged during the AI-difficulty change-set because L2-4 (fight-history horizon) deliberately **reuses** the existing latching flag/number so it stays balance-compatible with those compensations.
- **Status:** observation, **not fixed** — the AI-difficulty change-set is L1-preserving by contract, so touching this shared scoring pass was out of scope. Fixing it (reset the flags at the top of each iteration) would change L1 bot behavior and should be a standalone change with its own sim comparison.

## Summary count

**1 Critical** (C1) · **16 Major** (M1–M16) · **26 Minor** (m1–m26) · **11 Design questions** (D1–D11). Recommended triage order: C1, M5/M6 (Тигр), M9 (Котики), M7 (Butcher), M11/M12 (forced fights & kills), M1 (Goblin win), M4/M8 (Toxic Mate), then the rest. (M13/M14/M15/M16 fixed 2026-07-03; m5/m6/m7/m21/m23/m25 fixed 2026-07-04; m18 confirmed intended 2026-07-04; m17 fixed 2026-07-05; m20 documented 2026-07-05. Still open: m12, m19, m24, m26.)

## Verification addendum (second pass, 2026-07-01)

A full re-verification was run over these docs: every file:line anchor (~230) was mechanically dumped and compared against the cited code — **all anchors correct**; the eight highest-risk claim clusters (agent-sourced or never personally read in the first pass) were re-read at the source; and a completeness diff was run (every `PassiveName` in characters.json and every `case` label in CharacterPassives.cs vs the docs).

**Corrected during verification** (all fixes applied in place):
1. `SecureRandom` was described as a crypto RNG in ARCHITECTURE §9 — it is a plain `System.Random` wrapper (→ new finding m21).
2. Tier semantics: Sakura and Баг (Tier −1) were described as non-rolling specials — they are **secret rollable** characters (range 40, humans only, hidden from prediction menus); Молодой Глеб's exclusion comes from the `CharactersPull` Tier ≥ −1 filter, not a range-0 roll (m6, D3, CHARACTERS.md, GAME-DESIGN §11).
3. D5 ("2kxaoc") described the masking backwards — the passive is *exempt from* enemy-passive-name masking, not hidden by it.
4. CHARACTERS.md under-documented three verified mechanics, now added: Спартанец's "Это привилегия - умереть от моей руки" per-win rider (+1 extra Justice to the victim, −1 Int to himself, `CP:2782-2790`); Вампур's Гематофагия Psyche-priority rule (`CP:2827-2842`); Молодой Глеб's "Следит за игрой" marks up to **3** targets (`CP:4627-4667`).
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
