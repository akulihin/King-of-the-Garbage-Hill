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

## Major

### M1. Goblin "round-10 Ziggurat at place 1 ⇒ win" is a log line, not a win
- **Described** (`GameDesign.txt:508`): "Если на 10м ходу Гоблины строят зиккурат, находясь на 1м месте — они выигрывают."
- **Actual**: `DoomsdayMachine.cs:1488-1499` fires at the **start** of round 10 (after `RoundNo++`), requires place 1 + a ziggurat built at position 1, and only prints "…побеждает!". `HandleLastRound` re-sorts purely by score (`CheckIfReady.cs:382`) with no ziggurat rule. A ziggurat built *during* round-10 processing (`CP:6137-6207`, runs when `RoundNo` is already 11) can never trigger the message.
- **Impact**: the documented win condition doesn't win; a premature "wins!" message can appear a round early and then be false.
- **Fix direction**: enforce at `HandleLastRound` (like Premade's enforced win, `CheckIfReady.cs:472-494`) or drop the message + design line.

### M2. "Еврей" web widget renders for Толя with LeCrisp's state
- `GameStateMapper.cs:362-365` keys the widget on passive "Еврей" (both LeCrisp and Толя have it) but fills it from `LeCrispAssassins.AdditionalPsycheCurrent` — LeCrisp-only state. A Толя player sees a dead widget with stolen-psyche 0.
- **Fix direction**: gate on `Name == "LeCrisp"` (pattern: the Геральт case at `:686`).

### M3. AWDKA is silently forced to last place for every fight calculation
- `CheckIfReady.cs:1112-1127`: right before bots act (and before all fights), the "Произошел троллинг" holder is moved to the end of `PlayersList` and places re-assigned (comment `//end //AWDKA last`). Score order returns only at end of round.
- **Impact**: during fights AWDKA's place is ~6 regardless of score — inflates his underdog moral, changes Harm kite ranges, place-based passives and bot targeting against him. Documented nowhere.
- **Fix direction**: confirm intent; either document it in the passive description or delete the block (it mirrors the HardKitty "Никому не нужен" block right below it, so it may be a copy-paste leftover).

### M4. Toxic Mate "INT" negative-win rule applies only when he attacks
- `DoomsdayMachine.cs:775-781` negates the winner's point for "Никому не нужен"/"INT" holders only in the attacker-win branch; a defending Toxic Mate who wins gets a normal +1 (`:901-913`), and `CP:3050-3061` adds nothing on wins. JSON: "Побеждая — теряет очки" (unqualified). HardKitty's Mute wording ("если напал и победил") matches the code; INT's does not.
- **Fix direction**: extend the negation to the defense branch for "INT" (or reword the passive).

### M5. "Тигр топ, а ты холоп" has an undocumented second window at game start
- Initial `TimeCount = 3` (`Tigr.cs:10`) **plus** a swap in `HandleEventsBeforeFirstRound` (`CP:170-183`) put Тигр at place 1 immediately, consuming one count; the end-of-round swap (`DoomsdayMachine.cs:1299-1327`) then keeps him there for rounds 2–3. The random trigger (`TigrTopWhen`, rounds 1–8, 1–2 times) later resets the counter to 3 (`CP:4954-4958`) for the *described* "случайный момент" window. Тигр also collects +1 Psyche/+3 Мораль per round at #1 (rounds 2–9, `CP:5916-5923`).
- **Evidence of unintendedness**: designer note `GameDesign.txt:74-75` — "перемена местами должна срабатывать только когда тигр не топ1 (недавно оно вообще будто 2 раза за игру сработало)".
- **Fix direction**: start `TimeCount = 0` and drop the first-round case (keep only the random window), or document the opening window as intended.

### M6. Тигр "Лучше с двумя, чем с адекватными" counts Тигр himself
- `CP:3471-3487` loops `game.PlayersList` without excluding `player` — Тигр's own Int/Psyche trivially match, so at the end of round 1 he pockets +3 bonus points for "recruiting" himself (once, via FriendList dedupe).
- **Fix direction**: `if (t.GetPlayerId() == player.GetPlayerId()) continue;`.

### M7. Butcher pays his point on any win, spec says on a Drop
- the_boys.txt / theboys_update_commit: "+1 point **on drop**" ("очко если удалось его **Скинуть**" — Скинуть is the established Drop term). Code: `CP:3363-3364` awards +1 bonus (+2 SD) whenever Butcher *wins* against a marked sup. Wins are far more common than Drops — balance-relevant.
- **Fix direction**: decide win-vs-drop; if drop, hook into the Harm/Drop path (compare `dropsAfter > dropsBefore` in `DoomsdayMachine.cs:835-876`).

### M8. Toxic Mate "Tilted" rewards skips, not "психует"
- JSON: "Получает бонусное очко каждый раз, когда кто-то __психует__". Code (`CP:4320-4328`): +1 bonus per enemy whose `IsSkip` is set at end of round — no connection to psyche-loss ("психанул") events at all. (The +50 "все не смогли походить" half matches, `CP:4330-4335`, including the intentional "+20" joke log.)
- **Fix direction**: hook the +1 into `MinusPsycheLog`/psyche-rage events, or reword the passive to "за каждый пропуск хода".

### M9. Котики "Кошачья засада" (Штормяк) eats half of *total* score
- JSON: "сожрёт половину очков, которые враг получил **пока на нём сидел** этот кусок кота". Code (`CP:3193-3205`): on the return win, victim loses `Floor(GetScore()/2)` — half of their **entire score**, regardless of when it was earned (no snapshot at deploy time exists).
- **Impact**: a late Storm return can wipe 20+ points instead of the earned-while-sat handful — swingiest single effect in the game.
- **Fix direction**: snapshot the victim's score at deploy (`KotikiAmbush`) and halve the delta.

### M10. Premade's anti-skip un-bans a round-10-banned Carry
- JSON: "Carry никогда не пропустит ход. **(кроме банов)**". Code (`CP:5779-5792`) clears *any* involuntary skip (`IsSkip && !ConfirmedSkip`) on the Carry — including Тигр's round-10 "Стримснайпят и банят" ban (`CP:4936-4943` sets exactly that state) and Школьник's brother-ban. The freed Carry may then act on round 10 despite being "banned" (other systems — targeting refusal, Тигр-топ suppression — still assume he's banned).
- **Fix direction**: skip the anti-skip when the skip source is a ban (e.g. check the ban passive + round, mirroring `CheckIfReady.cs:1270`).

## Minor

### m1. "Вампур_" typo kills a flavor Easter egg
- `GameUpdateMess.cs:1480` checks `Name == "Вампур_"` (JSON: "Вампур") — the garlic level-up placeholder never shows.

### m2. "Vampyr Позорный" logic is commented out
- `GameReactions.cs:994-1000` (level-up denial) disabled; only the phrase object remains. Remove or restore.

### m3. Young Gleb transform keeps `Name == "Глеб"` → three misfiring Name checks
- Transform (`GameReactions.cs:256-268`) deliberately doesn't set the name (mylorik's Акула transform at `CP:6097-6104` *does*). Consequences: `GameUpdateMess.cs:1221` "Понизить один из статов" caption never shows post-transform; `CheckIfReady.cs:427` AWDKA-trolling flavor for Молодой Глеб unreachable; `GameReactions.cs:1125` old-Gleb psyche-10 phrase can fire for the transformed character. (`GameStateMapper.cs:292` / `GameUpdateMess.cs:1651` guards are harmlessly always-true.)

### m4. `PassivesClass.GlebSkip` declared as `bool … = new()`
- `PassivesClass.cs:91` — compiles to `false`; clearly unintended syntax.

### m5. Exploit rotation runs in games without Баг
- `GameClass.RollExploit` + `DoomsdayMachine.cs:73-76` rotate/count exploit state even when nobody can consume it. Harmless bookkeeping.

### m6. Dead legacy code catalogue
- `LolGod.cs` + `PassivesClass.LolGodUdyrList` — "Бог ЛоЛа" doesn't exist; the only live reference is an always-true guard inside Darksci's "Не повезло" (`CP:2808`).
- `Saldorum.cs` (single-L "Хохол" design) vs live `Salldorum.cs`: orphaned cases "Парень с сюрпризом" (`CP:860, 2161`), "Сало" (`CP:874, 2175`; `GameUpdateMess.cs:634`), "Ниндзя" (`CP:1423, 2194`) — those passive names exist in no character and are never added at runtime.
- `CraboRack.BokoBoole` (`CraboRack.cs:16-19`) — zero references.
- "Молодой Глеб" JSON entry has `Tier: -2` — excluded from the roll pool by `CharactersPull.GetRollableCharacters` (Tier ≥ −1, `CharactersPull.cs:43-50`); transform template only. Note the tier semantics (`CharactersPull.cs:28-32`): **Tier −1 = secret but rollable** — Sakura and Баг do roll for humans (range 40; bots never roll tier <4) while staying hidden from prediction menus. *(Corrected in verification — originally attributed to a range-0 roll.)*

### m7. Stale comments (cosmetic)
- `CalculateRounds.cs:27` says TooGood sets "75 or 25" — code sets 70/30 (`:238, :249`).
- `CP:642` comment says tunnel escape is 33% — code rolls 50% (`:645`).

### m8. Толя "Подсчет" recharge is 4–5 rounds, description says 2–3
- Initial cooldown *is* 2–3 (`PassivesClass.cs:31`), but after each use `Cooldown = Random(4,5)` (`CP:1095`), decremented once per round (`CP:6063-6072`). Net: ~2 uses per game instead of ~3.

### m9. Итачи Цукуеми recharge is 4 rounds, description says 2
- On activation `ChargeCounter = -2` (`CP:3033`); +1 per round (`CP:4213-4219`) → 4 rounds to full. Initial charge (0→2) matches the described 2.

### m10. Francie Хим.оружие ignores enemy-difficulty scaling
- Design note (the_boys.txt): "(normal +1, toogood +1, toostronk +1; если мы ту-гуд/стронк = +0) × прокачки". Code (`CP:3315-3324`): flat `chemLevel` bonus, zeroed when TheBoys were TooGood/TooStronk vs the victim. The "harder enemy pays more" half is missing.

### m11. Ziggurat costs differ from the design note
- Code (`CP:6142-6187`): requires ≥1 of each type **and score ≥ 3** (undocumented gate), costs −3 bonus + a *permanent* −1 Worker deduction. Design note: "умирает 1 Трудяга (т.е. если каждый 9й — Трудяга, то умирает 9 гоблинов)" — i.e. population loss, not a permanent worker-slot loss. Current implementation is milder early, harsher late.

### m12. Saitama's round-1 "serious targets" are effectively arbitrary
- SeriousTargets = top-2 by `GetSkill()` (`CP:242-251`), which at game start is 0 for almost everyone → stable-sort picks the first two in list order. Recomputed properly from the end of round 1 (`CP:4027-4036`). Also note "боевая мощь" = skill only (stats ignored) — Кратос-style stat monsters are never "serious".

### m13. HardKitty's opening −30 is score, logged as Мораль
- `CP:159-161`: `HardKittyMinus(-30)` lowers **Score** by 30 (bypassing the floor), while the personal log says "Никому не нужен: -30 *Морали*". One of the two is wrong; players reading the log get misdirected.

### m14. Butcher sup marks only exist from round 2
- Marks are assigned in `HandleNextRoundAfterSorting` (`CP:5864-5898`), which first runs at the end of round 1 — no sups (not even superheroes) during round 1. Probably fine; worth one line in the passive text if intended.

### m15. Salldorum's history rewrite ignores the round multiplier and the Еврей redirect
- Design note (`GameDesign.txt:549`): steal "(1 × множитель раунда)" from each winner, "Но следи за Евреями. Если они украли эти очки — то отнимается у евреев". Code (`WebGameService.cs:932-943`): flat −1/+1 bonus per winner, no multiplier (the comment even says "could scale"), no Jew redirection.

### m16. Геральт's Lambert fumble is 20%, design note says 10%
- `CP:4490` (`_rand.Luck(20)`, one-time) vs `GameDesign.txt:654` "10% Шанс". Also worth knowing: the meditation hint for human players calls the Anthropic Haiku API synchronously inside the round pipeline (`CP:4459-4475`) with a static fallback.

### m17. Dopa "Взгляд в будущее" also procs on blocks
- Proc condition (`CP:4257-4261`): either dual-target attacked the other **or either target blocked**. The description only promises the "attacked his next target" case. Lenient in Dopa's favor.

### m18. "Привет со дна" counts skip *events*, not skipping players
- `CP:3700`: bonus = `game.SkipPlayersThisRound` (incremented once per skipped **fight**, `DoomsdayMachine.cs:530` — two attackers into one skipper = 2) + count of blockers. Mildly inflated vs "когда кто-то пропускает ход".

### m22. Latin "Saitama" vs JSON "Сайтама" — dead "👑 King" flair
- `GameUpdateMess.cs:720`: Saitama's leaderboard view should mark the current #1 as "👑 King", but the check is `Name == "Saitama"` while the character is named "Сайтама" — never renders. Same bug family as C1/m1. *(Found by `tools/audit-passives.sh` on its first run.)*

### m21. `SecureRandom` is not secure (naming hazard)
- `Helpers/SecureRandom.cs:25-45`: the crypto implementation is commented out; the service is a plain `System.Random` wrapper. Fine for a game, but the name misleads — and `PassivesClass` carries a private copy that *does* use `RandomNumberGenerator` (`PassivesClass.cs:281-300`), so trigger schedules are crypto-random while combat rolls aren't. Unify or rename.

## Design questions

### D1. Darksci can dodge "Дизмораль" by hoarding the round-9 level-up
- The −5 Psyche fires only inside `GetLvlUp` while `RoundNo == 9` (`GameReactions.cs:1226-1231`); saving the point until round 10 skips it (and the psyche-0 skip check). Bots always spend immediately. Intended tech or loophole?

### D2. Goblin Ziggurat can duplicate "Еврей" (and other Standalone passives)
- LeCrisp's "Еврей" is `Standalone: true` (`characters.json:140`), so Goblins can learn it (`CP:6171-6181`) despite the roll-time LeCrisp/Толя exclusivity (`StartGameLogic.cs:180-194`). `HandleJews` supports multiple jews (`CP:6688-6766`), each earning +1 while the victim's point is suppressed once. Verify which Standalone passives are safe to copy (full matrix in the Phase-3 audit).

### D3. Sakura's "Одна из трех" is a narrative win only
- `CheckIfReady.cs:496-508`: top-3 Sakura is declared `playerWhoWon` (logs, phrases; ZBS-100 only if scores tie) but keeps her real place for stats/mastery/TotalWins. Note she *is* a rollable secret character (Tier −1, m6) that nobody can predict — the soft win may be the intended compensation; confirm.

### D4. Passives whose logic keys on character Name, not the passive
- "Булинг": DeepList's "Стёб" spares LeCrisp by name (`CP:2596-2614`); `HandleJews` skips stealing from DeepList by name (`CP:6715-6719`).
- "Го играть": the block/skip bypass vs friends is implemented inside "Заводить друзей" (`CP:1223-1234`) — the passive named "Го играть" has zero references of its own.
- "lvl-мяк": the +1-Justice level-up is `Name == "Котики"` (`GameReactions.cs:897-904`).
- All three work today but break silently on rename/transfer; consider keying on the passive names. (Also the inverse hazard: transferred Standalone passives *do* dispatch for new holders — e.g. Ziggurat copies.)

### D5. "2kxaoc" exists only to stay visible
- Its only special handling: `GameUpdateMess.cs:805-818` masks *other players'* passive names in the stats display ("Неизвестно"/"❓ …"), and "2kxaoc" is one of four names **exempt from masking** (with Запах мусора, Чернильная завеса, Еврей) — the meme is deliberately left readable. No gameplay effect; confirm none is intended. *(Corrected in verification — the original finding described this backwards.)*

### D6. Вампуризм copies Justice instead of draining it
- "подсасывает себе **всю** Справедливость цели" — code adds the target's current Justice to Вампур's next-round buffer (`CP:1881-1885`) but never removes it from the target (contrast: Kimiko's Живое Оружие and Близнец, which zero the victim's Justice — `CP:744-755, 905-919`). Confirm copy-vs-drain.

### D7. External stat changes on Стая Гоблинов are overwritten every round
- `CP:6122-6124` re-`Set`s Str/Int/Psyche from population each round end — debuffs like Спартанец's −1 Str vanish; Speed debuffs persist (Speed isn't population-driven). Inherent to the population design; document or special-case.

### D8. "Пейзаж конца света" +7 очков is round-multiplied ×4
- The non-pawn reward for attacking Монстр on round 10 is +7 **regular** points (`CP:4423`) — committed with the round-10 ×4 multiplier = effectively **+28** (+10 bonus on top). If "+7 очков" was meant literally, use bonus points.

### D9. Premade copies the Carry's fight-moral instead of transferring it
- `CP:2448-2451`: the Support `AddMoral(carryMoral)` while the Carry keeps theirs. JSON "Добываемая в боях Мораль так же передается" reads as a transfer. Same copy-vs-drain question as Вампуризм (D6).

## Phase 3 — cross-character interactions

### M11. Шэн and Штормяк forced attacks ignore the round-10 Тигр ban
- Монстр's no-escape has an explicit ban carve-out (`CheckIfReady.cs:1266-1271`). The other two forced-fight sources don't:
  - **Шэн** (`CheckIfReady.cs:1184-1199`) forces everyone below the position to attack Salldorum — a round-10-banned Тигр (IsSkip, stats nuked) gets Salldorum added to `WhoToAttackThisTurn`, and the fight loop processes forced fights even for skipping players (`DoomsdayMachine.cs:393-407`) — the "banned, can't act, can't be targeted" promise breaks (0-stat Тигр is forced to fight).
  - **Штормяк taunt** (`CheckIfReady.cs:1217-1251`) excludes dead players but not the banned Тигр — he can be provoked into attacking Котики on round 10.
- **Fix direction**: reuse the Монстр carve-out condition in both sites (and consider dead-player checks ✓ already present).

### M12. Монстр's apocalypse can kill Стая Гоблинов
- Every other kill source has an explicit goblin immunity: Кира's note (`CP:4103`), L-arrest (`CP:4780`), Кратос (`CP:1699`). "Пейзаж конца света" pawn deaths (`CP:4398-4409`) have **no** goblin check — Goblins guessed by Монстр become pawns and die on round 10, contradicting `GameDesign.txt:509` "Гоблинов нельзя Убить механикой 'убийства'".
- **Fix direction**: `if (pawn.GameCharacter.Name == "Стая Гоблинов") continue;` in the pawn loop (or block goblins from becoming pawns).

### D10. Ziggurat-copyable (`Standalone`) passives — a risk inventory
The Ziggurat copies any `Standalone: true` passive from the last attacked enemy (`CP:6169-6181`). Current inventory by behavior when a Goblin holds them:
- **Fully functional** (probably intended): Одиночество, Месть, Импакт, Еврей (see D2), Обучение, Лучше с двумя, 3-0 обоссан, Запах мусора, Я пытаюсь!, Произошел троллинг (⚠ also inherits the M3 forced-last!), Неуязвимость, Привет со дна, Лежит на дне, Ничего не понимает, Им это не понравится, Гематофагия, Панцирь, Болевой порог, Хождение боком, Питается водорослями, Оборотень, Безжалостный охотник, Клинки хаоса, Вороны, Изанаги (2 free auto-win defenses!), Аматерасу, Сомнительная тактика (huge self-nerf — must lose first fight vs everyone).
- **Self-brick**: **Булькает** — a Goblin who learns it loses all Мораль/Skill gains including the Ziggurat's own +5 Мораль (`CharacterClass.cs:963, 1010, 1125`). Funny, probably not intended to be learnable.
- **Dead copies** (game-start-only or Name-gated hooks): Лысина, Первая кровь, Похищение души (init-only — no effect when learned mid-game); Ведьмачьи заказы (every case gated `Name == "Геральт"`).
- **Fix direction**: maintain an explicit copyable-whitelist, or at least exclude Булькает and the dead copies.

### D11. Цукуеми × Чернильная завеса double-charges the same point
- If a player beats Осьминожка (ink fake-win: they get +1 now, owe it back at round 11) while under Итачи's Цукуеми, Итачи *also* copies that +1 at end of round and deducts it again at game end (`CP:4183-4211`, `CheckIfReady.cs:363-379`) — the victim repays the same point twice (once to Octopus's restore, once to Итачи). Rare, but both effects are "secretly repaid later" designs that don't know about each other.

### Phase-3 checks that passed (selection)
Монстр no-escape spares pickle-Рика (he has neither IsBlock nor IsSkip); dead players are excluded from every forced-attack pool; Geralt's contract injection skips while he blocks/skips; Тигр-топ/Portal-Gun/Шэн/Storm-bite/Drops all respect the Ziggurat lock; the Глеб/Молодой-Глеб tea skip spares a charged Portal Gun; Premade anti-skip doesn't touch pickle-Рика; Котики are immune to a *transferred* Storm's taunt and a transferred Минька/Штормяк won't buff against its owner; PointFunnel points bypass Еврей theft (funnel copies only `AddWinPoints`); Октопус's ink correctly debits Евреи who stole the point.

## Phase 4 — plumbing (web/state layers)

### m19. Итачи's Crows and Izanagi are invisible in the web UI
- Only Tsukuyomi state is mapped (`PlayerDto.TsukuyomiState`, `GameStateMapper.cs:321`); crow counts per enemy and remaining Izanagi charges exist only in backend state. Recently-added character with the least UI coverage; every comparable kit (Goblins, TheBoys, Геральт) has a full widget.

### m20. Geralt's "Чеканная монета" demand economy is entirely undocumented
- A full hidden system: post-round demand/advance buttons, invoice totals, a Displeasure ledger, +2 regular per advance, and **death by pitchforks with −500 at Displeasure ≥ 11** (`CP:4548-4585`). Neither `characters.json` nor `GameDesign.txt` mentions displeasure or the death. (The design note's *other* hidden Geralt mechanics: "психует when a contract holder is killed" — **not implemented** anywhere; "dies on place 6" — implemented as a log line only, `CheckIfReady.cs:270-276`.)
- Also of note: the meditation hint calls the Anthropic API synchronously inside the round pipeline (`CP:4459-4475`).

### Plumbing checks that passed
Every `PassiveAbilityStatesDto` member is mapped and rendered (no dead DTO/TS fields); mapper `case` strings all exist in `characters.json` (the only orphans are the legacy Saldorum combat cases, m6); per-player marks (SellerMark, virus, cancer, cat, pawn, monster-type, sup) all follow the SellerMark pattern end-to-end; `Sakura`/`Кратос` intentionally have no widgets; `Баг` state rides on `PlayerDto` (ExploitState) by design.

## Verified-consistent highlights (no finding)

Worth stating because they're easy to suspect: Saitama's Неприметность deferral/reclaim matches the recent fix note exactly (incl. the manual round-10 moral flush, `CP:4844-4851`); Rick's pickle/portal player-control flow matches rick_update (bot never takes over, pickle stays attackable, gun charge music one-shot); the Тигр round-10 ban is respected by targeting, Монстр forced attacks and the Тигр-топ swap; Кира's +2/+4(L) numbers, 25-Мораль eyes cost, L-arrest from round 8 and −500 all match; Goblin growth/death percentages match the goblins commit (death is 10+0.5R²/3%, the *older* design note's 1·R²/3 was rebalanced); Ziggurat Standalone-only learning with duplicate protection; Выгодная сделка pays both the +1 bonus per deal and +5 Мораль per deal; Октопус's ink ledger correctly redirects debits to Евреи who stole the point; the Глеб-tea skip spares a charged Portal Gun ("ничто не помешает").

## Summary count

**1 Critical** (C1) · **12 Major** (M1–M12) · **22 Minor** (m1–m22) · **11 Design questions** (D1–D11). Recommended triage order: C1, M5/M6 (Тигр), M9 (Котики), M7 (Butcher), M11/M12 (forced fights & kills), M1 (Goblin win), M4/M8 (Toxic Mate), then the rest.

## Verification addendum (second pass, 2026-07-01)

A full re-verification was run over these docs: every file:line anchor (~230) was mechanically dumped and compared against the cited code — **all anchors correct**; the eight highest-risk claim clusters (agent-sourced or never personally read in the first pass) were re-read at the source; and a completeness diff was run (every `PassiveName` in characters.json and every `case` label in CharacterPassives.cs vs the docs).

**Corrected during verification** (all fixes applied in place):
1. `SecureRandom` was described as a crypto RNG in ARCHITECTURE §9 — it is a plain `System.Random` wrapper (→ new finding m21).
2. Tier semantics: Sakura and Баг (Tier −1) were described as non-rolling specials — they are **secret rollable** characters (range 40, humans only, hidden from prediction menus); Молодой Глеб's exclusion comes from the `CharactersPull` Tier ≥ −1 filter, not a range-0 roll (m6, D3, CHARACTERS.md, GAME-DESIGN §11).
3. D5 ("2kxaoc") described the masking backwards — the passive is *exempt from* enemy-passive-name masking, not hidden by it.
4. CHARACTERS.md under-documented three verified mechanics, now added: Спартанец's "Это привилегия - умереть от моей руки" per-win rider (+1 extra Justice to the victim, −1 Int to himself, `CP:2876-2884`); Вампур's Гематофагия Psyche-priority rule (`CP:2921-2936`); Молодой Глеб's "Следит за игрой" marks up to **3** targets (`CP:4721-4761`).
5. Two flavor-only hidden passives were missing entirely: "God Of War" (Кратос) and "Искусство" (mylorik/Спартанец) — added.
6. Exact passive-name hygiene: "Стримснайпят и банят и банят и банят" and "Это привилегия - умереть от моей руки" appeared with typographic substitutions (…/—) in places; fixed to exact strings (they are load-bearing identifiers).

**Confirmed unchanged** (spot-listed because they were the highest hallucination risks): all `Luck()` probability figures (Luck(a,b) ≈ a-in-b, `SecureRandom.cs:51-61`); m19 (Itachi crows/Izanagi truly absent from the web layer); M2 (Jew widget state source); the bot moral thresholds 20/13/8/5/3 and round>10 auto-block; the AdminPlayerType runtime injection (`GameUpdateMess.cs:253`); the ARCHITECTURE §3 handler line table (all rows). No finding was retracted; C1–M12 all stand.
