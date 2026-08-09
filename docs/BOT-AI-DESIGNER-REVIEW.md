# Bot AI levels 1–4: game-designer review

This document is the review surface for bot decisions. It describes what the bot is trying to do in plain language, then names the implementation rule so a designer can choose **Keep**, **Edit**, or **Remove** without reading the whole AI file.

The central contract is deliberate:

- **Level 1 is the legacy baseline.** Its large historical decision body predates the fair-information boundary and may read privileged internal state. It is retained for replay and simulation comparison, while the explicit cross-level event/fun rules still apply to it.
- **Level 2 is the competent fair bot.** It uses only information its character or an ordinary player could see. The rewrite preserves its strategic goals; uncertainty and weighted choice keep it fallible.
- **Level 3 is the advanced fair bot.** It gets no additional secret facts. It is stronger because it remembers more, knows the public rules exactly, combines clues, tracks confidence, and makes firmer choices.
- **Level 4 is Legacy+.** It is the ordinary-game default: Legacy's character personality and weighted variety are retained, while explicitly separated global channels blend fair V2 evidence into target and Block decisions. Its fair channels obey the player-information boundary, but the retained Legacy half deliberately may use live actions and hidden state, so the complete mode is not advertised as fair.
- Level 0 exists as a legal random simulation control, but is outside this level 1–4 design review.

Primary implementation: `Game/GameLogic/BotsBehavior.cs` (`HandleBotBehavior`, `HandleFairBotAttack`, `HandleFairBotKira`, `HandleLvlUpBot`, `TryForceRoundTenSuspectedMonsterAttack`), `Game/GameLogic/CharacterPassives.cs` (`HandleBotPredict`, `HandleFairBotPredict`, `RankRoundTenMonsterSuspects`), `Game/GameLogic/BotInformation.cs`, and `Game/Classes/GamePlayerBridgeClass.cs` (`BotKnowledgeState`).

## 1. Level identity

| ID | Designer view | Technical detail |
|---|---|---|
| AI-LEVEL-1 | Historical opponent. Keep its old personality and weaknesses so AI changes can be measured against it. It is **not guaranteed fair**. | Effective difficulty `1` retains the legacy attack body, but its ordinary prediction sheet and round-10 Monster task use the shared viewer-scoped inference path. Simulation no longer preloads the true roster. |
| AI-LEVEL-2 | Competent player-like opponent. It understands its own kit, remembers recent visible results, targets sensible enemies, and sometimes takes a non-best option. | Effective difficulty `2` enters `HandleFairBotAttack`, `HandleFairBotPredict`, and `HandleFairBotKira`. Target choice is weighted random; the best target receives double weight. Most recent-history windows are 3 rounds; incoming-pressure windows are 2 rounds. |
| AI-LEVEL-3 | Strong player-like opponent. It uses the same facts as L2 but performs better inference and commits to its best conclusion. | Effective difficulty `3` uses the L2 fair path with `Advanced == true`: longer windows, confidence-scaled deductions, public-roster assignment, inferred fight edge, and max-score target choice. There is no omniscient round and no true-roster preload. |
| AI-LEVEL-4 | Legacy+ entertainment opponent. It keeps every operative Legacy actor-specific rule, build, economy, memory and weighted target choice, but blends selected global channels with V2 and uses advanced fair identity inference. | Effective difficulty `4` stays in the Legacy attack body and enters its explicit Legacy+ factor/blend helpers. It is never treated as numeric “V3+”: `Smart` is exactly 2/3, `Advanced` exactly 3, and Legacy+ is routed separately; an actually missing personal dimension uses its explicit V2 fallback. |

`GameClass.AiDifficulty` supplies the ordinary-game default, now **4**. Ranked creation explicitly sets **3**. `GamePlayerBridgeClass.AiDifficulty` is an optional per-bot admin/simulation override. `BotsBehavior.EffectiveDifficulty` resolves them.

## 2. Fair-information contract for shared inference, L2/L3 and the fair channels of Legacy+

Every prediction-capable strict bot at L0–L4 uses the inputs below for its ordinary prediction sheet, and every strict bot at L0–L4 uses them for the round-10 Monster task. The complete L2/L3 policy has the same boundary. Legacy+ applies it to its V2-derived information, fight-estimate, prediction and Kira channels; its separately weighted Legacy channels retain the privileged reads catalogued in section 12.

### Allowed inputs

| ID | What the bot may know | Why a player may know it | Technical source |
|---|---|---|---|
| AI-INFO-OWN | Its own character, exact stats, Skill, Psyche, Moral, Justice, score, cooldowns, marks, passive state, submitted actions, and private history. | These are the bot character's own sheet and personal UI. | The acting `GamePlayerBridgeClass`, its `GameCharacter`, `Status`, and `Passives`. |
| AI-INFO-LOGS | Sanitized global logs, its current personal logs, and its previous-round personal logs. | These are the normal character log panels. | `BotInformation.VisibleCurrentGlobalLogs`; retained `VisibleGlobalLogsByRound`; owner `InGamePersonalLogs`/`InGamePersonalLogsAll`. Hidden snippets are removed and `unknown_bug` is rendered as `???`. |
| AI-INFO-BOARD | Usernames, public place, team membership, dead/alive state, public bot flag, legal attack menu, and owner-visible leaderboard annotations such as marks, counters, target class/type tells, or `🚫`. | These are visible on the leaderboard or in that character's own annotated leaderboard/menu. | Target id/username/place/team plus `GameUpdateMess.CustomLeaderBoardBeforeNumber` and `CustomLeaderBoardAfterPlayer`; `KnownPlayerClass`; legal `Nanobot` targets. |
| AI-INFO-RESULTS | Resolved visible attacks, targets, blocks/skips, wins/losses, and place history. | A player can remember completed public rounds. | `BotInformation.CaptureVisibleRound` records `WebFightLog` rows unless hidden from that viewer, plus public places. |
| AI-INFO-OWN-FIGHT | When the bot personally fought someone, the exact class and Justice shown in that fight and a summary of how strong the opponent looked. | The participant sees more detail in its own fight than an uninvolved spectator. | `LastObservedClass`, `LastObservedJustice`, and `LastObservedFightEdge` in `BotOpponentKnowledge`. |
| AI-INFO-RULES | Public character definitions, base stats, tier/roll rules, mutual-exclusion rules, fight math, and passive behavior. | A player can study the published rules; L3/L4 apply them more strongly, but every strict-bot prediction/Monster inference may use them. | `CharactersPull.GetVisibleCharacters` plus rule-coded inference in `BotsBehavior` and `CharacterPassives`. |
| AI-INFO-REVEAL | An exact identity when a public effect or the bot's own ability reveals it. | It is no longer hidden from that viewer. | Public `Толя` and `Коммуникация` reveals; owner-visible `Сверхразум`, Naruto pairing, and `Глаза бога смерти`. Exact evidence is stored with `IsExactReveal`. |
| AI-INFO-SCRIPT | Two explicit designer events may use information outside ordinary inference. | They create coordinated set pieces rather than ordinary deduction. | Round-8 Клоны Сусано reveals Madara to strict bots with prediction sheets; a Naruto may follow another living Naruto's submitted target. The round-10 Monster task and every ordinary prediction row stay inside the viewer boundary and receive no real Monster/Eren seat. |

### Forbidden inputs

| ID | Shared strict-bot inference and L2/L3 must not know | Practical rule |
|---|---|---|
| AI-INFO-NO-ACTION | Whether an opponent is currently blocking, skipping, ready, or whom they selected this unresolved turn. | Never read an opponent's live `IsBlock`, `IsSkip`, `ConfirmedSkip`, or `WhoToAttackThisTurn` to choose an ordinary action. Historical resolved defense is allowed. The sole live-action exception is Naruto sibling target focus. The web mapper also redacts opponent block/skip fields (`GameStateMapper.MapStatus`). |
| AI-INFO-NO-IDENTITY | A hidden opponent character, passives, transform, or exact six-character roster assignment. | Predictions and the round-10 hunt come from the visible catalogue and evidence. Do not dereference `target.GameCharacter.Name` or target passive state except behind a viewer-owned exact reveal or the scripted Madara rule above. In particular, the server never supplies the real Monster or Eren seat to the hunt. |
| AI-INFO-NO-STATS | Exact opponent stats, Skill, Psyche, Moral, score, Justice, or private marks not shown to this viewer. | Use public place, shown annotations, own-fight observations, and estimates. Unknown Justice remains unknown. |
| AI-INFO-NO-PRIVATE-LOG | Another player's personal logs, admin-only/hidden fights, hidden global snippets, or a raw cumulative log containing information the viewer missed. | Consume the viewer-scoped projection in `BotInformation`; do not parse raw opponent logs or hidden rows. |
| AI-INFO-NO-ORACLE | A simulator, server helper, or exact fight function must not answer a strategic question using the real target state. | Simulations at every level start without a true-roster prediction preload. Fight edges and the round-10 Monster ranking are estimates from legal evidence, not roster or fight oracles. |

The boundary is structural: the fair attack method builds a `FairTarget` projection containing public identity/place/team/markers, viewer memory, a prediction, and estimates. The shared prediction solver and `RankRoundTenMonsterSuspects` consume the same viewer-scoped evidence without reading a target's real identity. The latter is transient event reasoning rather than a submitted prediction: it compares the Monster score with that target's best admissible alternative. Legacy+ builds the ordinary fair projection before its Legacy actor switch and consumes it only in the named fair channels; its privileged half remains visibly isolated in `CalculateLegacyPlusGlobalPreference` and the legacy enemy-information delta.

## 3. Shared turn pipeline

| ID | Designer view | Technical detail |
|---|---|---|
| AI-TURN-01 | A dead bot does nothing. | `HandleBotBehavior` returns when `Passives.IsDead`. |
| AI-TURN-02 | Every strict-bot level remembers the just-resolved player-visible round for shared prediction and the round-10 Monster task. | `BotInformation.CaptureVisibleRound` snapshots one completed round once per bot. It retains up to ten sanitized global-log snapshots and per-opponent public result memory. L2/L3 consume it throughout their policy; Legacy+ consumes it for prediction and its named fair blend channels; L0/L1 consume it only where the shared inference contract applies. |
| AI-TURN-03 | A strategic bot commits to one character plan for the game. | `EnsureBotPlaystyle` writes `AiPlaystyle` once for strict L2/L3 bots. See section 9. |
| AI-TURN-04 | The bot spends all pending level-up points and ScamRat Carry tokens before any action. | `HandleLvlUpBot` loops until `LvlUpPoints == 0`, then `ScamRat.SpendCarryPointsForBot` buys the currently weakest non-max stat until its independent balance is empty, including before forced skips. |
| AI-TURN-05 | Forced skips stay skips; the ordinary AI may not overwrite them. | `CompleteForcedSkip` clears the bot's own queue and confirms the turn. |
| AI-TURN-06 | Post-round-10 and locked `Мадара` states cannot take a normal action. Every prediction-capable strict bot answers Клоны Сусано; three named shinobi also attack. | Round `> 10` submits the no-target action. `Мадара` is action-locked on round 8 or while sealed. From the start of round 8, every strict bot except ARAM/Kira/Madara/Admin/Let's Roll is forced to keep exact Madara before inference, after every prediction pass and before action. Strict-bot Наруто, Sakura and Итачи additionally attack after forced-Skip handling; the later clone injection adds their second fight. |
| AI-TURN-07 | The bot decides how to spend Moral, then uses character widgets such as Kira's notebook, then attacks or blocks. | `HandleBotMoral` → `HandleBotKira` when applicable → `HandleBotAttack`. L0 skips the strategic Moral/Kira stages. |
| AI-TURN-08 | Ordinary prediction inference is a separate between-round decision and locks after round 8. | Through round 8, every prediction-capable strict L0–L4 bot runs the shared fair solver and fills every admissible enemy row. On rounds 9+, `HandleBotPredict` preserves every existing row and only fills missing rows for a late/partially inherited bot sheet. Dopa's Macro deduction and scripted Madara are reasserted, then Sakura-forbidden rows are removed. |

## 4. Universal fair attack policy

The same scoring skeleton is used by L2 and L3. Character rules add or subtract from it; they do not grant secret information.

| ID | Designer view | Technical detail |
|---|---|---|
| AI-ATTACK-ELIGIBLE | Only selectable living opponents are considered; the bot does not voluntarily attack itself or its Naruto pair. Public round-10 bans and the visible Darksci quit line remove targets. Removing two Naruto siblings must not itself increase Block odds. | `HandleBotAttack` builds the legal `Nanobot` list, uses the viewer's `🚫` annotation for L2/L3/Legacy+ round-10 bans, and parses only sanitized visible logs for their `Нахуй эту игру` filter. L0/L1/Legacy+ count each living sibling as a virtual attack slot only in the action roll. A clone that exhausts every ordinary target queues a living sibling through the separate forced fallback instead of Block/Skip (`Naruto.GetBotActionTargetSlotCount`/`TryForceCloneSiblingAttack`; `GameReactions.AttackInsteadOfBlock`). |
| AI-ATTACK-R10 | On round 10, every strict bot must attack the player it independently considers most likely to be `Монстр без имени`. | `RankRoundTenMonsterSuspects` scores every selectable target as Monster and subtracts that target's best admissible non-Monster score. Targets with an exact non-Monster reveal are ranked after unresolved targets; equal relative/absolute scores are randomized. `TryForceRoundTenSuspectedMonsterAttack` retries the ranked candidates until one legal submission succeeds, before ordinary target or Block policy. It never receives the real Monster/Eren seat. Dopa then submits the highest-ranked distinct second target required by Макро. This task supersedes the former Eren-first rule recorded by M189. |
| AI-ATTACK-BASE | Every legal enemy begins plausible. A target ahead of the bot gets a small urgency bonus; first place gets a slight anti-dogpile penalty. | Start at 10; first place `-1`; a place numerically above the bot `+1`. |
| AI-ATTACK-LOSS | Do not immediately repeat a clearly awful matchup. Prefer opponents the bot has beaten over opponents that have beaten it. | A recent `TooGOOD` personal loss is `-7`; a better-stats loss is `-5`. Public win/loss history adds a clamped `-2..+2`. L2 looks back 3 rounds; L3 6. |
| AI-ATTACK-CROWD | Avoid blindly piling onto someone already targeted, unless the character benefits from attention. | Recent public target count subtracts up to 2. `Толя`, LeCrisp and several character rules intentionally reverse or reuse this signal. Naruto is the live exception: a target already queued by another living Naruto receives `+3`. |
| AI-ATTACK-DEFENSE | A history of frequent blocks/skips makes a target less attractive, but a kit that progresses through expected defense is less discouraged. The bot never knows the current block. | At a weighted historical defense rate of at least 60%, L2 applies `-2` (`-1` for a bypass/progress kit); L3 applies `-4` (`-1` for a bypass/progress kit). Progress kits include the bot's own `Автопобеда`, `Безжалостный охотник`, charged portal, eligible Spartan/Sirinoks marks, or the named attack-through-defense kits. |
| AI-ATTACK-CLASS | Use a target-type or class tell when the bot actually owns that information. | Matching the bot's skill target is `+3` early/`+2` late; a known nemesis advantage is `+5`; a known reverse nemesis is `-2` at L2 and `-4` at L3. No hidden target class is read. |
| AI-ATTACK-JUSTICE | Use exact Justice only after personally seeing it, then update the estimate from public results. | The last own-fight Justice is reset after a public win and incremented by public losses. A visible WUF marker implies zero. Relative Justice changes score by up to 5; equal known Justice is `-3`. If never observed, the value stays unknown. |
| AI-ATTACK-EDGE | Use observed or estimated matchup strength, without asking the server for the true answer. | Public catalogue averages are narrowed by a known class, blended with the predicted character by confidence, and advanced for plausible level-ups. The edge combines the bot's real stats with estimated target stats, Psyche term, known Justice, and 35% of an earlier own-fight observation. L2's universal rule only reacts to a recent observed edge (`+2`/`-3`); L3 reacts to inferred thresholds at ±4 and ±10. Character pilots may use the same estimate at both levels. |
| AI-ATTACK-PREDICT | Guesses matter only when confidence is earned. Avoid early HardKitty/Sirinoks and risky first contacts; exploit likely Darksci on round 9. | `ApplyPredictedOpponentCaution` starts at confidence 35. L3 scales most caution by confidence; L2 uses a flat value. The cases are HardKitty, Sirinoks, Darksci, `Краборак`, `Осьминожка`, `Монстр без имени`, Toxic Mate, mylorik, and `Толя`. |
| AI-ATTACK-TEAM | A teammate is normally score 0. Only character mechanics that need friendly interaction may opt in. | `AllowTeamAttack` is set only by AWDKA, eligible DeepList mockery, untouched Darksci, eligible mylorik revenge, Sirinoks friendship, historically crowded `Толя`/LeCrisp, uncollected `Вампур`, or the Support carry. Engine legality still wins. |
| AI-ATTACK-CHOOSE | L2 is competent but not perfectly predictable; L3 commits to the highest evaluated target. | L2 weighted-randomizes positive scores and doubles the best score's weight. L3 chooses the maximum score, randomizing only exact ties. Failed engine submissions are removed and retried. |
| AI-ATTACK-MACRO | Dopa's `Макро` always tries to finish its required second distinct attack. | The same fair projection is reused; L2 weights the remaining choices, L3 takes the best, and the round-10 task uses the next ranked suspect. The bot may read its **own** first submitted target. If a strict-bot Macro deduction lands on a secret/Monster target, it preserves that target's existing admissible fair hypothesis; when that hypothesis is absent, it deterministically takes the highest-Tier public-catalogue prior (ordinal character-name tie-break). Only a human uses the random non-secret fallback. |

## 5. Fair block policy

Block decisions use the bot's own state, public place, target scores, and **historical** incoming attacks. They never poll whether opponents are currently attacking or blocking.

| ID | Designer view | Technical detail |
|---|---|---|
| AI-BLOCK-HARD | A literal cannot-block passive always attacks. | `Спарта` and `Aggress` override every block plan. |
| AI-BLOCK-PLAN | Character pilots may force attack, force block, or merely prefer one. | `GetFairBlockPlan`; the per-character decisions are catalogued in section 10. |
| AI-BLOCK-L2 | L2 sometimes blocks based on a preference, low own Justice, leader pressure, or a generic 25% chance. | Prefer-block is 50%; prefer-attack blocks 20%; neutral at zero Justice with weak targets is 50%; a pressured top-two bot is 2/3; otherwise 1/4. |
| AI-BLOCK-L3 | L3 makes threshold decisions instead of rolling. | Prefer-block blocks if best score `<15`; prefer-attack never blocks; a pressured top-two bot blocks below 12; a trailing low-Justice bot blocks below 7; otherwise it blocks below 5. |
| AI-BLOCK-STREAK | Levels 1–4 cannot turn optimal defense into a boring permanent loop. | After 2 consecutive voluntary Blocks, the next turn must attack when a target exists. A successful attack or forced Skip resets `ConsecutiveBotBlocks`; no-target fallbacks remain legal. L0 is unchanged. |
| AI-BLOCK-R10 | Round 10 globally requires the Monster-hunt Attack whenever a selectable target accepts it. | The shared L0–L4 hunt runs before every ordinary Block plan and retries suspects. Only when no ranked target can be submitted does the bot reach its normal L2/L3 or Legacy/Legacy+ Block policy. |
| AI-BLOCK-NO-TARGET | No positive/legal target becomes a block/no-target action. | Empty target list or all rejected submissions call `HandleAttack(..., -10)`. |

## 6. Prediction policy

| ID | Designer view | Technical detail |
|---|---|---|
| AI-PRED-CATALOG | Unknown characters are guessed from the same visible catalogue a player can study, not from the actual six seats. | Fair candidates exclude Sakura and `unknown_bug`, exclude team-only entries in FFA, exclude the bot's own known name, and deduplicate names. Literal `Монстр без имени` is an internal comparison candidate for the round-10 task, never an admissible submitted guess. |
| AI-PRED-EXACT | Exact public or owner-visible admissible reveals override guesses permanently. The post-round-7 Madara rule is the overriding designer-scripted identity exception. | `Толя` public reveal; `Коммуникация` when one public name maps to one Pink-Ward id; owner `Сверхразум`; Naruto sibling. From round 8 onward every strict bot with an ordinary prediction sheet receives 100%-confidence exact Madara through `EnforcePostRoundSevenBotPrediction`; it is reasserted after every competing path. Kira/Madara/ARAM/Admin/Let's Roll remain excluded. A stale or owner-written literal Monster value is removed rather than treated as an exact reveal. |
| AI-PRED-RULES | Known roster rules eliminate impossible combinations. | LeCrisp, `Толя` and ScamRat are pairwise exclusive; HardKitty and `Эрен Йегер` exclude each other; once a tier-4 identity is exactly known, other tier-4 candidates are removed. |
| AI-PRED-EVIDENCE | Use owned class tells, own-fight class, current/previous personal logs, sanitized public tells, and AWDKA's own Volibear stat feedback. | `FairPredictionScore`, `AwdkaFairPredictionScore`. Tells include `Ничего не понимает`, `Они позорят военное искусство`, `Стёб`, `Панцирь`, Darksci's named public lines, and failed `Толя` reveal. Transferred effects make some tells strong evidence, not certainty. |
| AI-PRED-ALL | Every prediction-capable strict L0–L4 bot must submit one admissible hypothesis for every admissible enemy, regardless of confidence. | The common solver always selects the highest-scoring non-Monster catalogue value. A weak result still becomes a row at confidence 25; exact admissible evidence remains fixed. Ordinary inference never asks which seat really is Monster or Eren. |
| AI-PRED-L2 | L0/L1/L2 guess each player separately with the common non-advanced solver. Duplicate guessed names are possible because these levels do not solve the whole roster. | The highest visible-evidence/catalogue score wins; an old hypothesis wins an exact score tie, otherwise the tie is randomized. Score `<45` records confidence 25; strong evidence starts at confidence 40 and caps at 85. Tier priors are 18/14/12/9/8/7/6 from highest to lowest bands. |
| AI-PRED-L3 | L3 solves the lineup like a logic puzzle: assign the most constrained seat first and do not reuse that guessed character. | L3 ranks every target/candidate pair, chooses by evidence margin, removes the assigned name, and stores 35–92 confidence. It additionally uses longer-lived own-fight class evidence, public strict-bot roll prior, attack/defense tendencies, public speaker tells, place-six clues, and prior consistency. |
| AI-PRED-L4 | Legacy+ fills every ordinary enemy row with the advanced all-different public-roster solver. | Exact admissible reveals stay fixed at 100%. Every other row is assigned from earned evidence and the most likely remaining visible non-Monster candidate, so all five enemy rows are filled without duplicate inferred names. |
| AI-PRED-MONSTER | A bot must describe every enemy, including the real Monster seat, as some admissible non-Monster character without being told which seat that is. | Literal `Монстр без имени` is atomically removed from both submitted rows and ordinary strategic evidence; every missing/invalid row receives the best admissible similarity just like any other target. `RankRoundTenMonsterSuspects` separately and transiently compares each selectable target's Monster score with its best non-Monster alternative and cancels any stale literal-Monster evidence boost, so the round-10 attack cannot turn into an oracle or a hidden prediction row. The former oracle-style override has been removed. |
| AI-PRED-SAKURA | Never submit forbidden Sakura predictions. | `Sakura.RemoveForbiddenPredictions` runs in `finally` for every bot prediction pass. |

## 7. Kira policy

Kira does not use the ordinary prediction widget; `Тетрадь смерти` has its own decision path.

| ID | Designer view | Technical detail |
|---|---|---|
| AI-KIRA-EYES | Save 25 Moral for `Глаза бога смерти`. Use it for an unrevealed top-two player, from round 7 onward, or occasionally earlier. | If an unrevealed living opponent exists and Moral is at least 25, activate for a top-two target, late game, or a 1-in-4 roll. |
| AI-KIRA-TARGET | Write against the highest-ranked living admissible target that has not already failed. | Public targets are sorted by place; failed target ids and Sakura are excluded before either Eyes or notebook selection (M243). |
| AI-KIRA-REVEAL | An Eyes-revealed identity is exact and is used immediately. | Reading the target's real `GameCharacter.Name` is permitted only after its id appears in Kira's owner-visible `RevealedPlayers`; evidence is stored at 100. |
| AI-KIRA-L2 | Without Eyes, L2 makes a plausible but broad guess from the public catalogue. | Exclude the bot's name, known revealed names, names already failed on this target, Sakura, `unknown_bug`, and illegal team-only definitions; then weighted-randomize by tier prior at confidence 25. |
| AI-KIRA-L3 | L3 ranks the same legal names using accumulated evidence. | `FairKiraGuessScore` uses an owned class tell/own-fight class, sanitized Darksci and failed-`Толя` tells, repeated public place-six history for `Эрен Йегер`, and its previous hypothesis. Confidence is 35–90 based on the winning margin. |
| AI-KIRA-L4 | Legacy+ uses the same legal catalogue and advanced evidence ranking as L3. | It never falls back to Legacy Kira's actual six-seat roster read; Eyes remains the only ordinary exact-identity source. |

## 8. Moral policy

L3 deliberately uses the same Moral policy as L2. Its advantage comes from information processing and target decisions, not free economy.

| ID | Designer view | Technical detail |
|---|---|---|
| AI-MORAL-LEADER | A top-two bot with less than 5 Moral buys Skill rather than attempting a score conversion. | First branch of `HandleBotMoral`. |
| AI-MORAL-POINT-TIERS | Trailing bots wait for better conversion tiers; the actual leader uses the reachable 5-Moral tier. | Before round 10, place 6 waits for 20, place 5 for 13. For points, L1/Legacy+ wait 8 at place 4 and 5 at place 3; L2/L3 wait 13 at place 4, 8 at places 3 and 2, and 5 only at place 1. Place 2 can still replenish Moral by attacking the leader; place 1 cannot. Point conversion spends while Moral `>=5` (M239). |
| AI-MORAL-SKILL-TIERS | Skill-focused bots may hoard based on place, then spend all available Moral into Skill. | Place thresholds are 20/13/8/5/3 for places 6/5/4/3/2. HardKitty, `Осьминожка`, and `Вампур` wait for 20; Sirinoks round 9, DeepList madness rounds, and Darksci round 6+ may override waits. |
| AI-MORAL-SAITAMA | `Сайтама` builds Skill through round 8, then converts for points on rounds 9–10. | Character branch in `HandleBotMoral`. |
| AI-MORAL-TOXIC | Toxic Mate does not attempt Moral conversion. | It starts at a deliberately unusable Moral value. |
| AI-MORAL-DOPA | `Стомп` and `Доминация` buy Skill; `Фарм` buys points; L2/L3 `Роум` buys Skill. | Uses the persistent `DopaMetaChoice.ChosenTactic`. L1's non-smart `Роум` falls through to points. |
| AI-MORAL-KIRA | Kira hoards until Eyes can be afforded while any living opponent is unrevealed, then buys Skill. | Moral `<25` returns while an unrevealed target exists. |
| AI-MORAL-SKILL-KITS | `Рик Санчез`, `Таинственный Суппорт`, Salldorum, Napoleon Wonnafcuk, Sirinoks, DeepList, and `Загадочный Спартанец в маске` prioritize Skill. | Direct character branches. Darksci forces conversion from round 6 in either conversion helper. |
| AI-MORAL-POINT-KITS | `Стая Гоблинов`, `Котики`, and TheBoys prioritize points. | Direct character branches. Weedwick refuses point conversion before round 10; HardKitty and `Осьминожка` also hold in their point helper. |
| AI-MORAL-SELLER | `Продавец Сомнительных Тактик` buys points while `Впарить говна` is ready, otherwise Skill. | Checks the bot's own cooldown. |
| AI-MORAL-SPECIAL | `Геральт` uses the demand/invoice system instead of Moral. `Вампур`, LeCrisp, and mylorik use their own timing gates. | Geralt demands an advance at safe displeasure and a prior invoice only when it predicts coins with no displeasure. `Вампур` hoards by place; LeCrisp buys early Skill and later points under its condition; mylorik buys Skill when revenge completion/round pressure requires it, otherwise defaults to points. |
| AI-MORAL-DEFAULT | Everyone else converts for points using the place thresholds. | Final `HandleBotMoralForPoints` fallback. Round-10 engine handling flushes leftovers. |

## 9. Persistent plans and level-up policy

### Plans chosen once by L2/L3

| ID | Character | Choices and purpose |
|---|---|---|
| AI-PLAN-DOPA | Dopa | Chooses the intended `Законодатель меты` plan (`Стомп`, `Фарм`, `Доминация`, or `Роум`) once, but does not apply it during setup. The ordinary first level-up raises a stat; the second routes the plan through the same no-stat meta choice used by humans. |
| AI-PLAN-DARKSCI | Darksci | V2 always chooses `Unstable`; V3 may choose `Stable` or `Unstable`. Legacy and Legacy+ also force the volatile choice without creating an `AiPlaystyle`. `Stable` applies the own-kit Skill/Moral benefit; `Unstable` prefers fights whose estimated edge is not too bad. |
| AI-PLAN-GLEB | Young-capable `Глеб` | V2 always chooses `Classic`; V3 may choose `Classic` or `Young`. Legacy/Legacy+ stay Classic because they do not create an `AiPlaystyle`. `Young` performs the real transformation and seeds three META targets using only owned class tells and public place. |
| AI-PLAN-BOYS | TheBoys | Random one of 9 plans: four focused member builds plus five combination builds; concentrates level-ups toward that ultimate/combination. |
| AI-PLAN-GOBLINS | `Стая Гоблинов` | Random `Horde`, `Army`, `Economy`, or `Ziggurat`; controls custom upgrades and some block/target priorities. |
| AI-PLAN-RICK | `Рик Санчез` | Random `Portal` or `Beans`. The current fair pilot still responds primarily to actual invented portal/active ingredient state; see limitation AI-LIMIT-PLAN. |
| AI-PLAN-ITACHI | `Итачи` | Random `Crows` or `Tsukuyomi`; changes the level-up build. |
| AI-PLAN-KRATOS | `Кратос` | Random `GodHunter` or `Ragnarok`; changes build and round-10 target valuation. Legacy+ also uses this plan because Kratos has no operative Legacy target/build pilot. |
| AI-PLAN-CATS | `Котики` | Random `Ambush` or `Storm`. The fair pilot currently reacts to deployed cats/taunts more than the label itself; see limitation AI-LIMIT-PLAN. |
| AI-PLAN-TOLYA | `Толя` | Random `Count` or `Rammus`; changes build and attack/block posture. |
| AI-PLAN-MONSTER | `Монстр без имени` | Random `Twin` or `Apocalypse`; changes build and block posture. |
| AI-PLAN-SUPPORT | `Таинственный Суппорт` | Random `Carry` or `Stakes`; mainly changes when the pilot insists on attacking. |
| AI-PLAN-DEFAULT | Other characters | `Adaptive`; their fixed character rules still apply. |

### Level-ups

| ID | Designer view | Technical detail |
|---|---|---|
| AI-LVL-GENERIC | Specialize the current best stat instead of spreading points. L2/L3 protect a dangerously low Psyche pool before over-stacking. | Default picks the highest stat below 10. If L2/L3 Psyche `<4` and the highest stat is at least 8, pick Psyche. |
| AI-LVL-STR | `Братишка`, LeCrisp, early `Глеб`, `Злой Школьник`, `Сайтама`, and `Таинственный Суппорт` build Strength for their fight plans. | Character overrides in `HandleLvlUpBot`; `Сайтама` follows with Psyche, and `Злой Школьник` follows with Intelligence. Young `Глеб` always concentrates Strength. |
| AI-LVL-PSY | `Вампур`, mylorik, Darksci, Salldorum, and Napoleon Wonnafcuk prioritize Psyche. | Raise to 10 unless a more specific branch supersedes it. |
| AI-LVL-TIMED | HardKitty builds Speed before round 6 and Psyche after; `Тигр` keeps Psyche at least equal to the round then builds Intelligence; Spartan builds early Psyche then Speed. | Exact round/stat guards in `HandleLvlUpBot`. |
| AI-LVL-WEED | Weedwick reaches Speed 5, then Psyche 10, then Intelligence. | Fixed ordered build. |
| AI-LVL-SIRINOKS | Sirinoks maximizes Intelligence. | Fixed build. |
| AI-LVL-TOLYA | Base `Толя` reaches Strength 8. `Count` continues Strength to 10 then Intelligence; `Rammus` raises Psyche to 8 then Intelligence. | Uses `AiPlaystyle`. |
| AI-LVL-BOYS | TheBoys focuses Intelligence/Strength/Speed/Psyche for Francie/Butcher/Kimiko/M.M. respectively. | One stat per persistent plan. |
| AI-LVL-KRATOS | `GodHunter`: Strength, Speed, Intelligence. `Ragnarok`: Speed, Strength, Intelligence. | Each stat advances to 10 in order. |
| AI-LVL-MONSTER | `Apocalypse`: Speed and Strength to 8, then Intelligence, Psyche, remaining Speed/Strength. `Twin`: Intelligence, Psyche, Strength, Speed. | Persistent-plan build. |
| AI-LVL-RICK | `Рик Санчез` always takes Intelligence for portal invention. | Fixed build. |
| AI-LVL-ITACHI | `Crows`: Speed, Intelligence, Psyche. `Tsukuyomi`: Intelligence, Psyche, Speed. | Persistent-plan build. |
| AI-LVL-SELLER | `Закуп` builds Intelligence, Psyche, Strength, Speed. | Passive-based override, in that order. |
| AI-LVL-DOPA | `Доминация`: Strength/Speed/Intelligence. `Роум`: Speed, Strength to 8, Intelligence. `Фарм`: Speed to 8, Psyche, Intelligence. Other/`Стомп`: Intelligence, Psyche, Speed, Strength. | Uses the actual meta choice. |
| AI-LVL-GERALT | `Геральт` upgrades the oil type with the most contracts, breaking ties toward the lowest oil tier. | Level-up buttons are mapped to Drowners/Werewolves/Vampires/Dragons oil, not ordinary stat intent. |
| AI-LVL-GOBLINS | `Horde` raises Hobs; `Army` raises Warriors; `Economy` raises Workers; `Ziggurat` raises Workers to 2; plans then use `Фестиваль` and fallback upgrades. | Custom Goblin level-up meanings, not normal stats. L1 retains the legacy Warriors-first sequence. |
| AI-LVL-SCAMRAT | ScamRat receives no scheduled level-ups and spends every Sharing is CARRYING! token immediately on a currently weakest stat. | Ties among non-max minimum stats are random; each purchase uses the same one-token, +1-stat path as a human. |

## 10. Character-specific fair attack and block decisions

These rules sit on top of the universal score. “Estimate” means the legal confidence-weighted estimate from section 4, never the true hidden value.

| ID | Character | Simple designer intent | Technical rule (L2/L3 unless noted) |
|---|---|---|---|
| AI-CHAR-WEEDWICK | Weedwick | Farm visible weed/bong value, catch players ahead, and exploit WUF; never feed a convincing DeepList. | Adds visible weed count, double bong count, place pressure; WUF multiplies score by 4; predicted DeepList zeroes it. Block is neutral. |
| AI-CHAR-DEEPLIST | DeepList | Hit the known target class during madness, respect Weedwick, manage `лол`/`кек` and mockery progress. | Matching owned class tell `+3`; predicted Weedwick zero; visible `**лол** +2`, `**кек** -2`; may hit teammate only for the owned one-win mockery state. Forces attack on scheduled madness rounds. |
| AI-CHAR-KIRA | `Кира` | Avoid L, pressure leaders, and spend an active Eyes attack on a useful unrevealed target while avoiding likely Monster. | L target zero; top two `+3`; active Eyes gives unrevealed `+12`, revealed `-6`, likely Monster `-8`. Separate notebook logic is section 7. |
| AI-CHAR-KRATOS | `Кратос` | Hunt the owned target class and attacks that hit adjacent leaderboard positions. Ragnarok seeks a difficult round-10 fight. | Class match `+10`; each adjacent seat `+3`; Ragnarok subtracts estimated edge on round 10. Always attacks rather than voluntarily blocks. |
| AI-CHAR-TIGER | `Тигр` | Finish visible series that are already going well. | Visible `2:0` receives a large commit if viable; `1:0` receives `+7`. Block neutral. |
| AI-CHAR-AWDKA | AWDKA | Use visible rank/training marks, train teammates when required, and challenge the strongest estimate early. | Bronze `+5`, platinum `-2`, owned streak mark `+4`; L3 makes the highest estimated max stat mandatory on round 1. Friendly training is allowed; before round 7 non-team targets may be zeroed until teammates are trained. Always attacks. |
| AI-CHAR-DARKSCI | Darksci | Spread `Не повезло`; Unstable takes plausible risks. | Untouched target `+5` and may include teammate; Unstable uses estimated edge (`+6` at `>=-3`, otherwise `-4`). Low Psyche on rounds 3/5/9 prefers block. |
| AI-CHAR-SCHOOLKID | `Злой Школьник` | Hunt the owned target class; avoid early HardKitty, then deliberately challenge it after the safe window. | Class `+3`; predicted HardKitty zero before round 5 and mandatory after round 5 if viable. Forces attack before round 8. |
| AI-CHAR-MYLORIK | mylorik | Open and finish revenge tracks before time runs out. | New target urgency, early estimated-Justice opportunity, completed/unfinished track scoring, and remaining-round multiplier all come from own revenge state. Friendly attacks only when revenge completion permits. Always attacks. |
| AI-CHAR-KRABORAK | `Краборак` | Prefer lower leaderboard positions and slightly avoid likely HardKitty. | If a viable place 4–6 target exists, place 1–3 is `-4`; likely HardKitty `-1`. Always attacks. |
| AI-CHAR-BRATISHKA | `Братишка` | Fight adjacent leaderboard seats. | Adjacent to own place `+5`. Other bots slightly avoid seats adjacent to a confidently predicted `Братишка`. Block neutral. |
| AI-CHAR-SIRINOKS | Sirinoks | Build the required friend set and use owned class tells, even through legal friendly fights. | After round 1, class match `+3`/mismatch `-3`; unfriend `+5`; one-friend and deadline logic can make attacks mandatory. Team permission follows own friend state. Forces attack rounds 1 and 10. |
| AI-CHAR-TOLYA | `Толя` | Revisit last count target, exploit historically crowded players, and invert preference when Count is ready. | Last counted target is doubled `+7`; ready Count maps score to `13-score`; otherwise public historical attention is valuable. Teammate allowed only after high historical attention. `Count` can force attack; `Rammus` prefers block under historical incoming pressure; ready Count force-blocks rounds 3/8 if not using its attack plan. |
| AI-CHAR-LECRISP | LeCrisp | Seek players who historically attract fights. | Historical target count `×6`; high attention permits team attack. Block neutral. |
| AI-CHAR-GLEB | `Глеб` | Classic Gleb respects tea/challenger timing. Young Gleb commits to the three META targets and uses tea on leaders. | Young META target `+16` and mandatory; ready tea adds place value and avoids likely `Рик Санчез`. Classic tea rounds zero targets; challenger rounds `+7`. Always attacks. |
| AI-CHAR-SPARTAN | `Загадочный Спартанец в маске` | Use owned mark/shame state to choose training fights. | Early: shamed `-3`, marked `+10`; later marked `+6`, unmarked `-4`. Block neutral. |
| AI-CHAR-SAITAMA | `Сайтама` | Before round 10, collect useful class and fresh-opponent progress. On round 10, fight first place. | Pre-10 class `+10`, low historical attention `+6`; round-10 first place mandatory. Prefer block rounds 1–3, neutral 4–6, prefer attack 7–9, force attack 10. |
| AI-CHAR-TOXIC | Toxic Mate | Before Cancer, seek a loss; after the first loss seek a win; while active pressure leaders and avoid the holder. | Estimated negative edge `+8` before first loss; estimated nonnegative edge `+10` afterward; active top two `+8`, holder `-5`. Always attacks. |
| AI-CHAR-DOPA | Dopa | Follow the selected meta plan and complete both `Макро` attacks. | `Стомп` seeks winnable middle places; `Фарм` values historical attention, especially when Vision is ready; `Доминация` seeks winnable leaders; `Роум` seeks distant places. Always attacks. |
| AI-CHAR-RICK | `Рик Санчез` | Spend an invented portal on first place and finish active ingredient targets when plausible. | Charged portal makes first place mandatory; active ingredient target gets `+22` if plausible or `+6` otherwise; invented empty gun `-2`. Forces attack during Pickle/penalty turns or with a charged portal. |
| AI-CHAR-ITACHI | `Итачи` | Stack crows, avoid wasting the active Tsukuyomi target, exploit estimated Speed, and pressure leaders when charged. Always answer Madara's round-8 challenge. | Active Tsukuyomi target `-25`; crows `+5` each plus `+10` at 3; Speed advantage and adjacency bonuses; charge 2+ versus top two `+8`. Always attacks; round 8 exact-predicts/attacks Madara before the clone injection. |
| AI-CHAR-VAMPUR | `Вампур` | Collect unvisited Hematophagia targets with useful Justice and do not immediately rematch last round's loss. | Estimated Justice `×2`; already-collected target discouraged until set complete; last-round personal loss zeroes score; eligible uncollected teammate is allowed. Block neutral. |
| AI-CHAR-NAPOLEON | Napoleon Wonnafcuk | Before alliance, recruit a leader it can plausibly fight. After alliance, protect ally and answer visible war targets. | No ally: top two `+10`, nonnegative edge `+5`. With ally: ally zero, visible `⚔️ +15`, treaty enemy `-3`. Forces attack until allied; later prefers block under historical pressure or every third round. |
| AI-CHAR-SUPPORT | `Таинственный Суппорт` | Choose a strong leader as carry, then perform the periodic support fight and interact with its carry. | Before mark, adds estimated offensive stats and `+10` for top two. Every third round non-carry `+12` plus edge; carry `+15` and friendly permission. Forces attack while choosing carry, every third round, or under `Carry`; otherwise even rounds prefer block. |
| AI-CHAR-GOBLINS | `Стая Гоблинов` | Seek useful standalone passives for Ziggurat, valuable mining positions, and plausible wins. Build/defend while population is small. | Confident new standalone passive `+12`; places 1/2/6 gain worker-scaled value; estimated edge bonuses/penalties. Force-block to build at useful place/late/Ziggurat conditions; otherwise prefer block below 10 goblins. |
| AI-CHAR-CATS | `Котики` | Follow deployed cats, spread Storm taunts, and prefer strong but beatable prey. | Deployed Minka/Storm target `+20`; untaunted `+5`, taunted `-3`; estimated max stat and edge add value. Force attack late or after deployment; otherwise force block every third round from round 3. |
| AI-CHAR-MONSTER | `Монстр без имени` | Avoid opponents estimated to tie one of its stats, seek observed Justice, and become aggressive at the end. Every other actionable strict bot pursues its own best Monster hypothesis for the finale. | Near-equal own stat `-8`; estimated Justice `×2`; round 10 `+10`, leaders another `+5`. Force-block rounds 1–2; `Apocalypse` attacks from round 3; `Twin` may block under historical pressure; always attack round 10. The cross-roster inference task is AI-ATTACK-R10 and does not reveal the actual Monster seat. |
| AI-CHAR-THEBOYS | TheBoys | Complete Francie orders, spread Kompromat, use chemical weapon on plausible fights, and pursue visible hero/leader value. | Order target `+20` and mandatory at one round left; new Kompromat `+10`; chemical level, visible `🦸`, armed virus, and top-three bonuses. Always attacks. |
| AI-CHAR-SELLER | `Продавец Сомнительных Тактик` | Spread marks whenever ready; later exploit marked players, especially the round-10 leader. | Ready unmarked targets get at least 20, marked target `-5`; cooldown fights prefer nonnegative edge; round-10 marked leader `+15`, other marked `+6`. Forces attack when ready. |
| AI-CHAR-SALLDORUM | Salldorum | Use the three-round Chronicle signal, spend Shen on players ahead, and value observed zero Justice. | Public win exactly three rounds ago `+5`; charged Shen versus player ahead `+8`; estimated Justice zero `+3`. Before block evaluation, may rewrite the best owned loss round during rounds 5–7; force-block once in rounds 1–3 for Time Capsule. |
| AI-CHAR-GERALT | `Геральт` | Hunt the owner-visible monster type with contracts/oil, and use fights to climb toward leaders. | Parses own leaderboard monster-type text; contracts `×4`, applied oil tier `×3`, 3+ contracts `+8`; unknown type `-5`; place-climb and top-two bonuses. Force-block without oil; prefer meditation blocks while fewer than 3 enemies revealed through round 6; force attack from round 8 or with oil after reveal goal. |
| AI-CHAR-EREN | `Эрен Йегер` | As Eren, value visible fire marks. Other bots receive no special Eren identity on round 10. | Own pilot adds visible fire count `×3`. Eren may be attacked by AI-ATTACK-R10 only when the acting bot's viewer-scoped evidence ranks that selectable seat as a Monster suspect; the former exact Eren-first rule from M189 is superseded. |
| AI-CHAR-NARUTO | `Наруто` | Coordinate the trio into joint attacks without making siblings voluntary targets, and never spend a clone turn on Block/Skip. | Another living Naruto's already submitted target gets `+3` classical L1 interest or fair L2/L3 score. Clones force an ordinary attack; after every non-sibling candidate is rejected, a living sibling becomes the final forced target. The original keeps its ordinary Block/Harem policy, and L0 remains random apart from clone no-defense legality (`Naruto.TryForceCloneSiblingAttack`; `GameReactions.AttackInsteadOfBlock`). |
| AI-CHAR-JON | `Джон Сноу` | Always submit an attack so **Еще один бастард** can protect a marked weak player; prefer useful upper/Castle targets without farming the current weakest marks directly. | Places 1–4 get `+3`; either owner-visible weakest target gets `−4`. Both fair levels force attack instead of voluntary Block, and legacy bots also always attack (`BotsBehavior.ApplyFairCharacterPreference`/`GetFairBlockPlan`/legacy block policy). |
| AI-CHAR-HARD-OCTO | HardKitty / `Осьминожка` | Their own attack uses the universal target policy; both avoid voluntary block. Other bots treat a sufficiently predicted identity with the caution in section 4. | `GetFairBlockPlan` returns force-attack. No separate owner target-score case. |
| AI-CHAR-SAKURA | Sakura | Use the universal target policy; defend a late top-three position only when earlier public rounds show pressure. Always answer Madara's round-8 challenge. | From round 8 at place 1–3 with historical incoming attacks, prefer block; otherwise prefer attack. On the challenge round the exact Madara attack wins first. Sakura prediction remains forbidden. |
| AI-CHAR-DEFAULT | DooM Guy, `Мадара`, `unknown_bug`, and any unlisted/transform-only entry | Use universal targeting unless the engine/character owns a separate forced action. | Neutral fair block plan by default. `Мадара` turn locks are handled in the shared pipeline. `unknown_bug` identity remains masked/unguessable. |

## 11. Exact L3 advantages over L2

This list is the designer guarantee: L3 is smarter by reasoning, not by receiving more truth.

| ID | L3 advantage | L2 behavior |
|---|---|---|
| AI-L3-MEMORY | Uses 6-round weighted attack/result history and 5-round incoming-pressure history. | Uses 3-round history and 2-round incoming pressure. |
| AI-L3-CHOICE | Chooses the highest positive target score, with random choice only on an exact tie. | Weighted random; the best target gets double weight but is not guaranteed. |
| AI-L3-EDGE | Uses confidence-weighted public character/stat estimates and prior own-fight evidence in the universal win plan. | Universal win plan reacts only to a recent own-fight edge, although character pilots may use the shared estimate. |
| AI-L3-CONFIDENCE | Scales predicted-character caution by confidence and often accepts lower confidence thresholds for character-specific inference. | Flat caution and usually stricter character thresholds. |
| AI-L3-PREDICT | Solves an all-different public roster, prioritizes the most constrained seat, and uses public roll/action/defense patterns and longer-lived evidence. | Guesses seats independently; weak seats use a tier prior or prior guess. |
| AI-L3-KIRA | Ranks notebook names using class, log, place-history, and prior evidence with a margin-based confidence. | Draws a tier-weighted legal catalogue name unless Eyes revealed the truth. |
| AI-L3-BLOCK | Applies deterministic target-score and historical-pressure thresholds. | Preserves human-like random block probabilities. |
| AI-L3-COUNTERPLAY | Penalizes reverse nemesis and historically defensive targets more strongly. | Uses smaller penalties so uncertain evidence does not overrule its baseline. |

No L3 branch reads the real opponent identity, live block, exact private stats, exact private Justice, or simulator truth.

## 12. L1 legacy behavior and fairness warning

L1 retains its legacy action body as a comparison control, but ordinary prediction, the round-10 Monster task, Madara, Naruto focus and the two-Block streak cap use their shared cross-level rules. It should not be advertised as a fair human-equivalent bot overall.

| ID | Legacy decision | Designer consequence |
|---|---|---|
| AI-L1-ATTACK | Large character-specific target switch with weighted preferences and many hard block/attack overrides. | Preserves old bot personality and replays, but parts of the decision use live opponent objects. The analogous intended character goals are listed in section 10 where L2/L3 preserve them fairly. |
| AI-L1-CURRENT-ACTION | May inspect current `IsBlock`, `IsSkip`, or `WhoToAttackThisTurn`. | It can counter an unresolved choice a player could not see. This is forbidden for L2/L3. |
| AI-L1-EXACT-STATE | May inspect exact hidden character/passive, opponent stats, score, Justice, resources, and private histories. | It can choose a technically perfect matchup for the wrong reason. This is forbidden for L2/L3. |
| AI-L1-LOGS | Parses legacy raw global/personal/leaderboard strings directly. | Some deductions are legitimate, but the old path does not enforce the viewer filter as a single boundary. |
| AI-L1-PREDICT | Uses the same viewer-scoped, complete non-advanced prediction solver as L0/L2. | Every admissible enemy receives the best admissible non-Monster hypothesis even at low confidence. Simulation and live L1 start without a true-roster preload. |
| AI-L1-KIRA | Prioritizes leaders but chooses unknown names from the **actual current roster** after eliminating revealed/failed names. | This is explicit identity cheating and remains only because L1 is frozen. L2/L3/Legacy+ use the visible catalogue. |
| AI-L1-MADARA-EREN | Shares the exact Madara prediction and the viewer-scoped round-10 Monster hunt. | Naruto/Sakura/Itachi additionally make the ordinary Madara attack. The hunt receives neither the real Eren nor Monster seat; it supersedes the old Eren-first/M189 behavior. |
| AI-L1-MORAL | Uses the common character Moral policy but earlier/wasteful point thresholds at places 3–4. | L2 was not made dumber here: it keeps the existing improved trailing-place thresholds. Every level now converts a leader's reachable 5 Moral (M239). |
| AI-L1-LEVEL | Uses the common fixed character builds but has no L2 persistent-plan branches or Psyche-floor correction. | L2/L3 retain and extend the stronger build logic. |
| AI-L1-BLOCK | Uses legacy random slots plus character overrides, sometimes informed by current opponent actions. | L2/L3 replace the information source while keeping character intent; Legacy+ blends both sources; every level 1–4 is forced to attack after two consecutive voluntary Blocks when a target exists. |

## 13. Legacy+ hybrid policy

Legacy+ changes only global bot policy. The Legacy character switch remains authoritative wherever that decision dimension has a real Legacy branch; the implementation detects missing branches rather than using a chronological character cutoff. Saitama already had Legacy behavior and is not a “new character” boundary.

| ID | Legacy+ rule | Exact behavior |
|---|---|---|
| AI-L4-PERSONAL | Preserve Legacy character personality. | Actor-specific Legacy target and Block rules, mandatory actions, Moral thresholds, fixed builds, ordinary lack of persistent `AiPlaystyle`, memory horizon, weighted-random choice and retry behavior remain unchanged. V2 target fallback exists only for ScamRat, `Джон Сноу`, `Эрен Йегер` and `Кратос`; Block fallback only for ScamRat, `Кратос` and Sakura. Kratos also receives its V2 `GodHunter`/`Ragnarok` plan and matching build because its apparent Legacy cases were entirely `Smart`-gated and therefore never operative at L1. Naruto's fair `+3` duplicates the retained shared Legacy rule and is not added twice. |
| AI-L4-UTILITY | Blend global target utility 80% Legacy / 20% V2. | The score begins at 10. Legacy placement utility contributes ×0.80; V2's public place/urgency utility contributes ×0.20. Character-specific deltas are applied afterward at their full Legacy value. |
| AI-L4-INFO | Blend information-derived global weights 50% Legacy / 50% V2. | Live Legacy crowd/action reads and the exact hidden target-identity switch contribute half their delta. V2's three-round resolved public crowd/defense signal plus sanitized logs and a confidence-scaled predicted-identity replay contribute the other half. Legacy Block/Skip/action mechanisms remain available by design. |
| AI-L4-MEMORY | Keep Legacy general memory. | Legacy's own one/two-round loss/win facts remain at full weight; V2's public win/loss score is not imported. Resolved V2 history enters only where another requested channel needs it: three-round crowd/defense for fair information, two-round incoming pressure for the fair Block half, advanced prediction, and the latest own-fight observation below. |
| AI-L4-FIGHT | Blend fight-chance terms 50% Legacy / 50% V2. | Exact Legacy Justice/class/nemesis terms contribute half. The V2 half uses only earned known class, estimated Justice and the latest recent fight personally observed by this bot. No true fight oracle is added. |
| AI-L4-BLOCK | Blend Block probabilities 50% Legacy / 50% V2, rounding down. | Each policy becomes basis points, including V2's 6666-basis-point pressured-leader case; `floor((legacy + V2) / 2)` is drawn once, and a character with no personal Block rule receives another `floor(p × 0.80)` reduction. A hard personal `ForceAttack`/`ForceBlock`, cannot-Block rule, mandatory attack, clone rule and two-Block cap remain authoritative and are not softened. |
| AI-L4-PREDICT | Infer all five enemies like the advanced fair solver. | Exact admissible earned/public/owner reveals stay 100%; other rows use public evidence, confidence, roster incompatibilities and an all-different most-likely non-Monster assignment. The real Monster seat receives no target-specific override because the solver never knows which seat it is. |
| AI-L4-KIRA | Keep notebook identity guesses fair. | Legacy+ Kira uses the visible catalogue and L3-style evidence ranking; it never samples an unknown name from the actual hidden six-seat roster. |
| AI-L4-ECONOMY | Keep Legacy economy/build/choice policy except shared corrections, explicit fun-mode gates and a genuinely missing personal fallback. | Legacy+ keeps Legacy place-3/4 Moral timing and level-ups; only Kratos uses its V2 plan/build as specified by AI-L4-PERSONAL. Like every level, a place-1 leader converts at 5 Moral. Legacy/Legacy+/V2 always choose volatile Darksci and Classic Gleb; only V3 may choose the stable/Young variants. |

## 14. Known limitations and follow-up choices

| ID | Limitation | Why it matters / possible designer choice |
|---|---|---|
| AI-LIMIT-L1 | L1 still cheats in several paths by design. | **Keep** as an internal baseline, **Edit** into a second fair personality, or **Remove** it from player-facing configuration after enough L2/L3 comparison data exists. |
| AI-LIMIT-L4 | Legacy+ deliberately is only partly fair. | Its fair half is auditable, but its retained Legacy live-action/identity channels can still react to unresolved or hidden state. Judge it as an entertainment personality, not as a fairness benchmark. |
| AI-LIMIT-PERF | “L2 is not dumber” is a design target, not a proven win-rate statement. Fair uncertainty can change matchups even when strategic intent is preserved. | Run paired probes by character and playstyle. Edit score weights only after separating inference accuracy from target-policy quality. |
| AI-LIMIT-MEMORY | Only sanitized global log text is capped to ten retained rounds; per-opponent dictionaries persist for the game, while scoring usually applies a 2/3/5/6-round window. | Keep for human-like recent memory, or edit horizons if L3 feels forgetful/overconfident. |
| AI-LIMIT-MIRROR | `BotInformation` constructs the ordinary-player observation directly from game logs rather than deserializing `GameStateDto`. | It currently mirrors the web visibility rules, but any future hidden-log/fight visibility change must update both `GameStateMapper` and `BotInformation` and add a boundary regression test. |
| AI-LIMIT-PARSER | Some evidence still depends on exact player-facing log phrases and leaderboard emoji/text. | These strings are load-bearing. UI wording changes require matching parser updates and tests. |
| AI-LIMIT-INFERENCE | Public tells can be caused by transferred/copied passives. L3 treats most as confidence, but a few public/owner reveals are intentionally exact. | Review which effects should be definitive versus merely suggestive. |
| AI-LIMIT-PLAN | `Rick:Portal/Beans` and `Котики:Ambush/Storm` are recorded, but much of the fair target policy reacts to actual own passive state rather than the plan label. | Keep as harmless telemetry, remove the unused distinction, or edit the pilots so the choice creates visibly different play. |
| AI-LIMIT-DISCONNECT | Strict autonomous bots are `PlayerType == 404`; disconnected humans may be driven through bot entry points but do not own the same persistent AI memory contract. | If disconnect takeover must be fair and strategically identical, give it an explicit viewer-memory lifecycle rather than silently treating it as a strict bot. |
| AI-LIMIT-ESTIMATE | Public-base-stat estimates assume likely level-up allocation; they do not reconstruct every hidden Moral-to-Skill choice or passive transfer. | This is deliberate uncertainty. Edit only if L3 misses obvious player-visible deductions. |
| AI-LIMIT-SIM | Bot win rate is bot-meta; shared prediction and the round-10 task are fair at every strict-bot level, but L1/L4 ordinary action policy still retains privileged Legacy channels. | Compare levels with the same lineups/seeds and separate shared-inference quality from the rest of each action policy. |

## 15. Designer Keep / Edit / Remove checklist

Write a verdict beside each stable ID. “Edit” should state the desired player-facing behavior, not a code instruction.

### Foundation

- [ ] AI-LEVEL-1 — Keep / Edit / Remove: ___
- [ ] AI-LEVEL-2 — Keep / Edit / Remove: ___
- [ ] AI-LEVEL-3 — Keep / Edit / Remove: ___
- [ ] AI-LEVEL-4 — Keep / Edit / Remove: ___
- [ ] AI-INFO-OWN through AI-INFO-SCRIPT — Keep / Edit / Remove: ___
- [ ] AI-INFO-NO-ACTION through AI-INFO-NO-ORACLE — Keep / Edit / Remove: ___
- [ ] AI-TURN-01 through AI-TURN-08 — Keep / Edit / Remove: ___

### General strategy

- [ ] AI-ATTACK-ELIGIBLE through AI-ATTACK-MACRO (including AI-ATTACK-R10) — Keep / Edit / Remove: ___
- [ ] AI-BLOCK-HARD through AI-BLOCK-NO-TARGET (including AI-BLOCK-STREAK) — Keep / Edit / Remove: ___
- [ ] AI-PRED-CATALOG through AI-PRED-SAKURA — Keep / Edit / Remove: ___
- [ ] AI-KIRA-EYES through AI-KIRA-L3 — Keep / Edit / Remove: ___
- [ ] AI-MORAL-LEADER through AI-MORAL-DEFAULT — Keep / Edit / Remove: ___
- [ ] AI-LVL-GENERIC through AI-LVL-GOBLINS — Keep / Edit / Remove: ___

### Persistent personalities

- [ ] AI-PLAN-DOPA / AI-PLAN-DARKSCI / AI-PLAN-GLEB — Keep / Edit / Remove: ___
- [ ] AI-PLAN-BOYS / AI-PLAN-GOBLINS / AI-PLAN-RICK — Keep / Edit / Remove: ___
- [ ] AI-PLAN-ITACHI / AI-PLAN-KRATOS / AI-PLAN-CATS — Keep / Edit / Remove: ___
- [ ] AI-PLAN-TOLYA / AI-PLAN-MONSTER / AI-PLAN-SUPPORT / AI-PLAN-DEFAULT — Keep / Edit / Remove: ___

### Character pilots

- [ ] AI-CHAR-WEEDWICK / AI-CHAR-DEEPLIST / AI-CHAR-KIRA / AI-CHAR-KRATOS — Keep / Edit / Remove: ___
- [ ] AI-CHAR-TIGER / AI-CHAR-AWDKA / AI-CHAR-DARKSCI / AI-CHAR-SCHOOLKID — Keep / Edit / Remove: ___
- [ ] AI-CHAR-MYLORIK / AI-CHAR-KRABORAK / AI-CHAR-BRATISHKA / AI-CHAR-SIRINOKS — Keep / Edit / Remove: ___
- [ ] AI-CHAR-TOLYA / AI-CHAR-LECRISP / AI-CHAR-GLEB / AI-CHAR-SPARTAN — Keep / Edit / Remove: ___
- [ ] AI-CHAR-SAITAMA / AI-CHAR-TOXIC / AI-CHAR-DOPA / AI-CHAR-RICK — Keep / Edit / Remove: ___
- [ ] AI-CHAR-ITACHI / AI-CHAR-VAMPUR / AI-CHAR-NAPOLEON / AI-CHAR-SUPPORT — Keep / Edit / Remove: ___
- [ ] AI-CHAR-GOBLINS / AI-CHAR-CATS / AI-CHAR-MONSTER / AI-CHAR-THEBOYS — Keep / Edit / Remove: ___
- [ ] AI-CHAR-SELLER / AI-CHAR-SALLDORUM / AI-CHAR-GERALT / AI-CHAR-EREN / AI-CHAR-NARUTO — Keep / Edit / Remove: ___
- [ ] AI-CHAR-HARD-OCTO / AI-CHAR-SAKURA / AI-CHAR-DEFAULT — Keep / Edit / Remove: ___

### L3/Legacy+ identity and limitations

- [ ] AI-L3-MEMORY through AI-L3-COUNTERPLAY — Keep / Edit / Remove: ___
- [ ] AI-L1-ATTACK through AI-L1-BLOCK — Keep / Edit / Remove: ___
- [ ] AI-L4-PERSONAL through AI-L4-ECONOMY — Keep / Edit / Remove: ___
- [ ] AI-LIMIT-L1 through AI-LIMIT-SIM — Accept / Follow up: ___

## 16. Verification guidance

Fairness and strength are separate checks.

1. **Static fairness review:** search the L2/L3 fair methods, the shared prediction path and `RankRoundTenMonsterSuspects` for opponent `.GameCharacter`, `.Passives`, `GetScore`, `GetRealJusticeNow`, `IsBlock`, `IsSkip`, and `WhoToAttackThisTurn`. Every occurrence must be either the acting bot's own state, public projection construction, an engine legality filter, or an explicitly owner-revealed identity. The scorer and round-10 task must not use a raw opponent answer.
2. **Build and docs:** run `dotnet build`, `bash tools/audit-passives.sh`, and `bash tools/verify-docs.sh --changed`.
3. **Safety simulation:** run `bash tools/simulate.sh --ai-difficulty 0` through `--ai-difficulty 4`; exit 0 means no game exception/freeze.
4. **Strength comparison:** use `--ai-probe 4 --ai-probe-char "Name"` against a Legacy field, or the paired `bash tools/ab.sh <character> 4 <coverage> <seed> 1`. Compare win rate, average place and score; compare `AiPlaystyle` only for L2/L3 because Legacy+ intentionally has none.
5. **Information test cases:** specifically stage an opponent choosing block late, then confirm an L2/L3 action is unchanged until that block becomes a resolved visible result. Repeat with hidden identity, hidden score, private logs, hidden fights and forced hidden rosters; changing only the real Monster/Eren seat must not feed the shared solver directly.
6. **Inference test cases:** verify every prediction-capable L0–L4 strict bot fills every admissible enemy row with a non-Monster value, including at low confidence; L3/L4 must remain all-different and exact admissible reveals fixed. Lock the sheet after round 8, then verify late processing fills only missing rows. For round 10, verify relative Monster-vs-best-alternative ordering, unresolved-before-exact-non-Monster ordering, randomized exact ties, rejection retry and Dopa's distinct second target. On a secret/Monster Макро target, a strict bot must preserve its fair hypothesis or, when none exists, use the deterministic highest-Tier/name-tied public prior; only the human fallback remains random. Separately retain the exact Madara and Naruto-focus scripted rules.
7. **Designer play-test:** watch one replay for every AI-CHAR ID and record whether the decision looks understandable from the information visible to that bot. A strong result reached for a hidden reason is still a failed fairness test.
