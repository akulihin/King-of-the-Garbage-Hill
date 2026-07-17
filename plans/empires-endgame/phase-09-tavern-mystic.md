# Phase 9 — Tavern minigame and mystic cards

Use this file as the opening instruction of a fresh Codex task running **5.6 Sol** on the
designer's machine. This phase is one change-set. It consumes the shared minigame envelope
and the Tavern hook; it must not redesign the core 53-card Durak deck.

## Executor rules (binding)

1. **Design source discipline**: read the phase's named raw export channels in `DiscordExports/Empires_Endgame/` before implementing. On conflict: main export > `ZBS MAKING` (outdated background); `empire_prompt` defines the core loop. Export missing from the environment → stop and tell the user; do not guess.
2. **Never fabricate silently**: a mechanic without export numbers → implement with a configurable default in `game-config.json` + append a ledger entry. A mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
3. **Un-deferral discipline**: substrate + un-deferral (delete `deferredReason`, add executable effects) + `EMPIRES_LIVE_FLAG_ALLOWLIST` additions or typed payloads + tests, all in ONE change-set. `validateLiveEffects` must keep rejecting flags nothing reads.
4. **Determinism**: no `Date.now`/`Math.random` in any simulation — only the serialized RNG streams (`features/empires-endgame/rng.ts`). Minigames replay from `(plan, seed, commandLog)`; mid-minigame real-time state is never serialized.
5. Player-facing card/passive *texts* stay deliberately vague (repo philosophy — never "fix" them; the designer writes new player wording). Exact mechanics are documented in `docs/WEB-CLIENT.md` §12B.
6. Git: do NOT commit or push. Write the commit message to `docs/commit-messages/<date>.md` (one file per change-set; `-2`, `-3` suffixes for further change-sets the same day).

## Codex preflight and prerequisite check

1. Read repository `AGENTS.md`, `plans/empires-endgame/README.md`, this prompt,
   `docs/WEB-CLIENT.md` §12B, and the card/minigame/state sections of
   `docs/ARCHITECTURE.md`. Run `git status --short`; preserve unrelated changes.
2. Verify Phase 2's minigame envelope from code/tests: `'minigame'` is a real campaign phase,
   `EmpiresMinigameSession`/result exist, `beginMinigame`/`resolveMinigame`/`abortMinigame`
   are authoritative, reload restarts from `(plan, seed)` with `attempt + 1`, and QA resolves
   the same simulation path used by UI.
3. Verify Phase 6 completely: `building-tavern` has no `deferredReason`, all of its authored
   effects have real consumers, and an engine/UI hook can open a Tavern session. If Tavern is
   still deferred or its hook is only a dormant flag, stop and report the prerequisite gap;
   do not absorb Phase 6 into this change-set.
4. Verify Phase 7 quest hooks and Phase 8 God/deck-memory/anti-bito behavior from tests. This
   phase must not let mystic cards leak into anti-bito or deck inspection unless the raw
   export explicitly says so and the integration is tested.
5. Capture the current config/save schema versions and the exact existing deck invariant.
   `config.cards` must still validate as exactly 53 standard cards (52 suit/rank cards plus
   Joker). Confirm the current orientation and every legality/winner helper before editing.
6. Confirm `card-spades-queen` still exists and inspect both faces. It is only a candidate
   carrier for Мария Брауз when the raw export proves that mapping and supplies exact
   authored content. Do not infer it from the English/Russian rank name.
7. Use a live Codex plan. Sub-agents may audit raw Tavern/card messages and deck invariants,
   but keep one owner for card types, engine zones/order, config, and migrations.

## Mission

Ship a deterministic Tavern minigame through the common replay envelope, activate the
Phase-6 Tavern entry point, add the authored Лист/Лорик/Анатолий mystic-card lifecycle, and
implement the Мария Брауз 2×2 encounter and 3–7–Т combination that can spawn Пиковая Дама.
Serialize hand order because neighbor position becomes gameplay state. Preserve the standard
Durak deck as exactly 53 cards: mystic extras are typed separately and are excluded from
attack, throw-in, defense, trump, rank, draw-deck, discard-winner, and winner checks.

## Design source (read before implementation)

Read all of the following on the designer's machine:

- `DiscordExports/empire_prompt` for the canonical card-game/campaign loop.
- Every main-export file for channels `таверна` and `карты` beneath
  `DiscordExports/Empires_Endgame/`, plus all cross-linked Мария Брауз, Пиковая Дама,
  Лист, Лорик, and Анатолий messages in `персонажи` and `общее`.
- The Phase-6 source messages for `building-tavern`, mercenaries, drink/Боевой дух, and
  rumors so this minigame activates the authored hook instead of creating a second Tavern.

Compressed mechanics, for navigation only: Tavern does not spawn on the first authored
“run,” spawns with 100% chance on the second, then 33%; clarify what “run” counts from the
raw export. Лист/Лорик/Анатолий have no normal suit/rank and return themselves inverted under
authored rules. Мария Брауз has a 33% Tavern encounter using a 2×2 card layout; the authored
3–7–Т combination leads to Пиковая Дама. Пиковая Дама periodically inverts neighboring
cards, which makes hand order persistent gameplay state. The Tavern also has two sections,
mercenaries, drink, and rumors where the Phase-6 substrate supports them.

If any named export channel or linked character message is missing, **stop before editing
and tell the user**. Do not implement from this compression or assume that
`card-spades-queen` is Мария Брауз.

## Repo anchors (read before editing; re-locate by symbol)

- `Web/VueClient/src/features/empires-endgame/types.ts`: `EMPIRES_SUITS`, `EMPIRES_RANKS`,
  `EmpiresCardDefinition`, `EmpiresDurakState`, `EmpiresCampaignState`, and the Phase-2
  minigame envelope types.
- `Web/VueClient/src/features/empires-endgame/config.ts`: exact 53-card validation (baseline
  around `:246`), migration chain, deferred/live-effect validation.
- `Web/VueClient/src/features/empires-endgame/engine.ts`: definition map and initial 53-card
  shuffle, `legalAttackCardIds`, throw-in/defense legality, `canCardBeat`, `resolveTrumpSuit`,
  `rankStrength`, `drawToHand`, `finishedCardGameWinner`, Phase-8 anti-bito/deck inspection,
  Phase-2 minigame resolution, `startNextCon` (baseline `:1371`), and snapshot normalization.
- `Web/VueClient/public/empires-endgame/game-config.json`: `building-tavern` (baseline around
  `:5067`) and placeholder `card-spades-queen` (baseline around `:1355`).
- `Web/VueClient/src/features/empires-endgame/qa.ts`: minigame policies/actions, campaign
  digest, fixture registry, and stall detector.
- `Web/VueClient/src/components/empires-endgame/EmpireCard.vue`, `DurakTable.vue`,
  `BuilderDrawer.vue`, and `Web/VueClient/src/pages/EmpiresEndgame.vue`: card rendering,
  ordered hand display, Tavern entry, minigame routing, and Builder integration.

## Work items (in order)

1. **Keep core and extras separate.** Do not add `'mystic'` to `EMPIRES_SUITS`, `'none'` to
   `EMPIRES_RANKS`, or mystic definitions to the standard `config.cards` grid. Introduce a
   separate catalog such as:

   ```ts
   interface EmpiresMysticCardDefinition {
     id: string
     suit: 'mystic'
     rank: 'none'
     name: string
     normal: EmpiresCardFace
     inverted: EmpiresCardFace
     lifecycle: EmpiresMysticLifecycle
     deferredReason?: string
   }
   interface EmpiresTavernConfig {
     enabled: boolean
     spawn: { first: number, second: number, later: number, counterScope: string }
     encounters: EmpiresTavernEncounterDefinition[]
     mysticCards: EmpiresMysticCardDefinition[]
     abortPenalty: EmpiresEffect[]
   }
   ```

   `config.cards` must continue rejecting 52 or 54 standard cards and duplicate/missing
   suit-rank cells. Validate mystic ids separately and reject collisions with every standard
   card/instance id. A combined read-only definition lookup is allowed, but initial shuffle,
   trump selection, and core-deck validation consume only `config.cards`.
2. **Config compatibility.** Inspect the current schema after Phases 6–8. Add a current→next
   sequential migration only if required, or extend additive backfill otherwise. Old custom
   configs receive a safe disabled/empty Tavern/mystic section without losing values; bundled
   config receives the exact authored definitions. Add previous-version import and JSON
   round-trip tests.
3. **Serialized Tavern/card state.** Use typed state along these lines:

   ```ts
   tavern: {
     visitCount: number
     encounterCount: number
     recruitedMysticInstanceIds: string[]
     queenSpawned: boolean
   }
   handOrder: { player: string[], god: string[] }
   ```

   Refine the location to current state conventions. Normalize legacy `handOrder` from the
   existing serialized hands without re-sorting. Centralize all draw/take/return/spawn/remove
   mutations so each hand-order array is always an exact permutation of its hand zone; reject
   duplicates/orphans on restore. Do not make two independently mutable sources of truth
   without an invariant checked after every engine commit.
4. **`features/empires-endgame/tavern/`.** Add `types.ts`, a deterministic engine/reducer,
   replay helper, QA scripted policy, and specs. Define `TavernPlan`, typed commands, and
   `TavernResult`; resolve through the existing
   `EmpiresMinigameSession { kind: 'tavern', plan, seed, attempt, origin }`. Result must be
   pure `f(plan, seed, commandLog)`. Mid-session UI state is never serialized; reload and
   abort follow the common envelope contract and authored penalty.
5. **Activate the Phase-6 hook.** The existing Tavern building action opens a planned Tavern
   session only when the authored availability/spawn rules pass. Use a serialized RNG stream
   and persist the counter that gives first/second/later their meaning. Reuse live mercenary,
   Боевой дух/drink, rumor/quest, and `unlimitedTavernRecruitment` consumers; do not fork
   economy or morale state.
6. **Mystic lifecycle.** Add raw-authored definitions for Лист, Лорик, and Анатолий. Spawn
   instances through typed Tavern results. Implement their exact self-return/inversion timing
   in one `mysticCardTick()` called from `startNextCon`; make it idempotent per con and test
   save/reload immediately before and after the tick.
7. **Durak exclusion contract.** Every legality and terminal helper must explicitly filter to
   core standard/Joker cards: attack, throw-in, defense, `canCardBeat`, rank matching,
   trump/retained-trump selection, draw/discard handling, anti-bito eligibility, deck-memory
   projection, empty-hand/winner logic, and QA invariants. Mystic extras can be displayed in
   ordered hands only as authored passive entities; they can never block or create a Durak
   win. Add exhaustive table tests so a future union expansion cannot silently admit them.
8. **Мария/2×2/Пиковая Дама.** Implement the 33% encounter, 2×2 rules, 3–7–Т recognition,
   spawn condition, periodic tick, and neighbor inversion exactly from the raw messages.
   Neighbor lookup uses serialized `handOrder`, defines edge behavior, and commits all
   inversions atomically. Distinguish Мария Брауз's standard-card content from the spawned
   Пиковая Дама entity; do not conflate them unless the raw export explicitly does.
9. **Conditional `card-spades-queen` port.** Only if the authoritative export explicitly maps
   this id/rank to Мария Брауз and defines the relevant face semantics, port that content and
   remove only the supported face `deferredReason` values. Preserve exact supplied player
   text; if exact wording is absent, ask the designer rather than rewriting the placeholder.
   If the mapping or mechanics are ambiguous, leave both faces deferred and ledger the gap.
10. **UI and Builder.** Add the Tavern minigame surface under
    `src/components/empires-endgame/`, with the authored two sections, accessible 2×2 board,
    result/abort handling, and QA fast-resolve. Render mystic extras and neighbor order
    without making them draggable into illegal core plays. Add Tavern/mystic config editing
    and graph/list validation to `BuilderDrawer.vue`.

## Un-deferral list (exact)

- There is **no guaranteed existing carrier** to un-defer in this phase.
- `building-tavern` must already be live from Phase 6. It is a prerequisite/hook to activate,
  not a Phase-9 un-deferral. If it still has `deferredReason`, stop and report the failed
  prerequisite.
- `card-spades-queen` may be ported and un-deferred only when the raw main export provides an
  exact Мария Брауз mapping, mechanics for the affected face(s), and exact player-facing text
  or explicit permission to retain existing vague text. Otherwise it remains deferred.
- New mystic definitions are new typed content rather than removal of an old carrier. Any
  definition whose semantics are incomplete gets `deferredReason`; no unread flag is added
  to `EMPIRES_LIVE_FLAG_ALLOWLIST`.

## Verification

- Add Tavern engine specs proving identical `(plan, seed, commandLog)` results, command
  validation, abort semantics, first/second/later spawn boundaries, 33% seeded encounter,
  2×2 combo recognition, mystic return/inversion, Queen tick idempotence, and neighbor edges.
- Add core-deck regression tests: exactly 53 standard cards still required; 52/54 rejected;
  extra typed mystics accepted separately; no mystic can attack, throw in, defend, beat, set
  trump, enter core deck/discard, affect anti-bito, or prevent/cause a winner.
- Add save/config tests for legacy hand-order normalization, duplicate/orphan rejection,
  ordered reload, pre/post tick restore, and previous config schema migration. If
  `card-spades-queen` is conditionally ported, test every newly live face effect.
- Add QA scenario `mystic-tavern` and scripted `resolve-minigame` policy. Extend digest/trace
  with Tavern state, mystic zones/order, and tick counters; full campaign autoplay must cross
  Tavern sessions without stalls.
- Add `cypress/e2e/empires-endgame-tavern.cy.ts`, wire it into `test:empires:e2e`, load
  `?qa=1&scenario=mystic-tavern&seed=...`, assert Tavern/2×2/order UI, use QA fast-resolve,
  and assert the committed mystic result. Never play a random real-time session.
- Run `pnpm --dir Web/VueClient run test:empires`; confirm all new specs are discovered. Run
  `bash tools/test-empires-endgame.sh` with all Vitest + Cypress suites green.
- Run `pnpm --dir Web/VueClient build` — not the broken environment-wide `type-check`.
- Run `bash tools/verify-docs.sh --changed`.
- Update `docs/WEB-CLIENT.md` §12B; catalogue any new bug in `docs/AUDIT-FINDINGS.md` with the
  next free ID; ledger every invented number by JSON pointer. Any config/schema bump ships a
  previous-version import/restore spec.

## Docs & ledger contract

- Document Tavern availability, common minigame-envelope use, mystic/core type separation,
  exact 53-card invariant, serialized hand order, lifecycle/tick order, Queen neighbor rules,
  Phase-6 hook activation, QA surface, and the exact conditional un-deferral result in
  `docs/WEB-CLIENT.md` §12B.
- Ledger the meaning of “run,” spawn/encounter rates if not fully authored, abort penalty,
  2×2/combo details, Queen period/edge behavior, hand-order migration choice, and every
  invented card/minigame value by JSON pointer.
- Increment the patch component of `GameVersion` sequentially in
  `King-of-the-Garbage-Hill/Game/Classes/GameClass.cs` after implementation and docs are
  complete.
- Write the proposed commit message to `docs/commit-messages/<date>.md` (or next same-day
  suffix). Do not stage, commit, push, reset, or discard files.

## Designer questions

- Does “not on the first run, 100% on the second, then 33%” count campaigns, Tavern visits,
  eligible cons, or a profile-wide meta progression, and when does that counter reset?
- Are Лист, Лорик, and Анатолий held in the normal hand as passive extras, in a separate
  mystic row, or in another zone; exactly when and to whom do they return inverted?
- What are the exact 2×2 actions and win/fail conditions, and does 3–7–Т mean ranks 3, 7,
  and Туз in any positions/order?
- Is Мария Брауз definitively `card-spades-queen`; which normal/inverted mechanics and exact
  player text belong to her?
- Is spawned Пиковая Дама a separate mystic instance, a transformation of Мария, or another
  state; how often does she invert neighbors, which side(s), and what happens at hand edges?
- Does rearranging hand order require a player action/cost, or is order only determined by
  draws, takes, Tavern spawns, and returns?
- Which Tavern sections, mercenary options, drinks, and rumors must ship now versus remain
  deferred behind later quests/content?
