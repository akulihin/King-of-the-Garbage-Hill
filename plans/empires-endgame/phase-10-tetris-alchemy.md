# Phase 10 — Tetris-alchemy

You are executing Phase 10 of the Empire's Endgame completion program with Codex 5.6
Sol. Read `AGENTS.md` and `plans/empires-endgame/README.md` before doing anything else.
This is one implementation change-set. It depends on Phase 2's shared minigame envelope
and Phase 5's typed epidemic lifecycle actually existing in the working tree.

## Executor rules (binding)

1. **Design source discipline**: read the phase's named raw export channels in `DiscordExports/Empires_Endgame/` before implementing. On conflict: main export > `ZBS MAKING` (outdated background); `empire_prompt` defines the core loop. Export missing from the environment → stop and tell the user; do not guess.
2. **Never fabricate silently**: a mechanic without export numbers → implement with a configurable default in `game-config.json` + append a ledger entry. A mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
3. **Un-deferral discipline**: substrate + un-deferral (delete `deferredReason`, add executable effects) + `EMPIRES_LIVE_FLAG_ALLOWLIST` additions or typed payloads + tests, all in ONE change-set. `validateLiveEffects` must keep rejecting flags nothing reads.
4. **Determinism**: no `Date.now`/`Math.random` in any simulation — only the serialized RNG streams (`features/empires-endgame/rng.ts`). Minigames replay from `(plan, seed, commandLog)`; mid-minigame real-time state is never serialized.
5. Player-facing card/passive *texts* stay deliberately vague (repo philosophy — never "fix" them; the designer writes new player wording). Exact mechanics are documented in `docs/WEB-CLIENT.md` §12B.
6. Git: do NOT commit or push. Write the commit message to `docs/commit-messages/<date>.md` (one file per change-set; `-2`, `-3` suffixes for further change-sets the same day).

## Codex 5.6 Sol preflight and dependency audit

Before editing:

1. Run `git status --short`. Preserve every pre-existing/unrelated change; never reset,
   discard, commit, or push it. Create a live task plan and keep one owner for shared
   hotspots (`types.ts`, `config.ts`, `engine.ts`, `game-config.json`, and
   `EmpiresEndgame.vue`).
2. Read this prompt, the README architecture/verification sections, `docs/WEB-CLIENT.md`
   §12B, and the raw design sources named below in full. Use targeted symbol searches,
   not a whole-codebase context dump.
3. Prove Phase 2 landed by locating the actual `EmpiresMinigameSession` /
   `EmpiresMinigameResult` unions, campaign phase `'minigame'`, and working
   `beginMinigame`, `resolveMinigame`, and `abortMinigame` paths with replay/restore tests.
4. Prove Phase 5 landed by locating first-class epidemic state and the one typed
   `startEpidemic` entry point used by config/event resolution, including its city target,
   protection stack, save normalization, and tests. Do not create a second epidemic
   implementation or mutate epidemic arrays directly from the alchemy simulator/UI.
5. Confirm `building-alchemy` is already live from Phase 5 and that its city/building
   identity can be carried in a minigame `origin`. Confirm the config migration chain,
   snapshot normalization, QA `resolve-minigame` action, test globs/scripts, review
   ledger, and sequential `GameVersion` workflow exist.
6. If either prerequisite is missing, stubbed, incompatible, or only described by a
   prompt, **stop before code edits and report the exact missing symbols/tests**. Do not
   absorb Phase 2 or Phase 5 into this change-set.

## Mission

Ship the authored Tetris-alchemy minigame through the shared minigame envelope: deterministic
fixed-timestep Assembly (`Сбор`) and Disassembly (`Разбор`), reagent commands, science/recipe
payoffs that are actually present in the export, and an explosion outcome routed through
Phase 5's typed `startEpidemic` at the originating laboratory city. The renderer may be
real-time, but the result must be the pure replay
`(plan, seed, commandLog) -> EmpiresAlchemyResult`. Undefined poison-crafting or explosion
semantics remain explicitly deferred rather than being presented as finished gameplay.

## Design source — read before implementing

Resolve and read the relevant files under the designer-machine-only export; use
`rg --files DiscordExports` to find the exact generated filenames:

- `DiscordExports/empire_prompt` for the campaign/minigame loop.
- `DiscordExports/Empires_Endgame/` channel `тетрис-алхимия` — authoritative board,
  control, speed, reagent, recipe, failure, and reward rules.
- Cross-references in channels `Технологии_*`, `здания`, and `события` for science-branch
  unlocks, the Алхимическая лавка/laboratory origin, poison crafting, and epidemic or
  mutant consequences.
- Any linked media/sketches named by those messages. `ZBS MAKING` is background only;
  the main export wins on conflict.

If the export directory or the essential `тетрис-алхимия` material is absent, **stop and
tell the user before editing**. The compressed model below is a navigation aid, not enough
authority to invent missing semantics:

- `Сбор`: pieces approach a central construction from four sides; the player controls the
  nearest eligible piece; it cannot be moved backward; the note says movement toward the
  center can accelerate ×3.
- `Разбор`: a complementary dismantling mode; its exact target, legal actions, and scoring
  must come from the raw channel.
- Reagents are described as removing a color, adding gray pieces, and resetting
  acceleration. Their costs, charges, targeting, and order are not defined by the
  compression.
- Acceleration follows an arithmetic progression. The compressed note says a 400% cap
  explodes the laboratory and starts an epidemic/mutants nearby. Do **not** hard-code 400%
  solely from this sentence: use 400 only when the raw export confirms it, or when the
  explosion semantics are confirmed but the number is absent and 400 is stored as a
  configurable, ledgered default. If the explosion semantics themselves are unclear,
  keep that outcome disabled/deferred.
- A poison-crafting route uses walls, and science-branch technology can affect alchemy;
  exact recipes, wall behavior, and technology IDs require the export.

## Repo anchors — inspect these symbols before editing

- `Web/VueClient/public/empires-endgame/game-config.json` — current schema, live
  `building-alchemy`, technology IDs, and all existing `deferredReason` carriers.
- `Web/VueClient/src/features/empires-endgame/types.ts` — minigame plan/result/session,
  campaign phase/state, typed epidemic payload, and config types.
- `config.ts` — `migrateEmpiresConfig`, validation, `validateDeferredReasons`, and
  `validateLiveEffects`; add structural alchemy validation here, not permissive casts.
- `engine.ts` (and any extracted `engine/` modules) — `beginMinigame`,
  `resolveMinigame`, `abortMinigame`, restore normalization, and the Phase-5
  `startEpidemic` funnel.
- `rng.ts` — only permitted randomness source.
- `features/empires-endgame/qa.ts` — scenario fixtures, `resolve-minigame`, state digest,
  trace/stall loop, and scripted policies.
- `src/pages/EmpiresEndgame.vue` and `src/components/empires-endgame/` — minigame routing,
  modal/full-surface conventions, HUD, Builder, and QA fast-resolve controls.
- `persistence.ts`, `Web/VueClient/package.json`, `tools/test-empires-endgame.sh`,
  `docs/WEB-CLIENT.md` §12B, `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md`, and
  `King-of-the-Garbage-Hill/Game/Classes/GameClass.cs`.
- For timing architecture only, inspect the Phase-2 TD fixed-step implementation. Do not
  copy `features/last-chances/engine.ts`'s rAF-delta simulation.

Re-locate anchors by symbol if line numbers have drifted.

## Work items (in order)

1. **Transcribe an executable spec from the export.** Record the exact entry trigger,
   origin, modes, board geometry, piece set, legal commands, speed progression, reagent
   behavior, scoring, rewards, failure, abort penalty, and science/recipe hooks. Separate
   sourced numbers from configurable defaults; append every invented default to the
   ledger by JSON pointer before relying on it.

2. **Add typed config plus migration.** Advance the current config schema by one step only
   if a schema change is required; never assume the version number from this prompt. A
   target shape is:

   ```ts
   interface EmpiresAlchemyConfig {
     enabled: boolean
     tickMs: number
     assembly: EmpiresAlchemyModeConfig
     disassembly: EmpiresAlchemyModeConfig
     acceleration: {
       initialPercent: number
       stepPercent: number
       maximumPercent: number
       inwardMultiplier: number
     }
     reagents: EmpiresAlchemyReagentDefinition[]
     recipes: EmpiresAlchemyRecipeDefinition[]
     explosion?: EmpiresAlchemyExplosionConfig
   }
   ```

   Make mode/piece/reagent/recipe IDs unique and validate every reference, positive timing
   value, board bound, probability, and technology/building/epidemic ID. Migration must
   backfill a safe disabled/default section for the previous config fixture. An undefined
   recipe or poison path gets its own non-empty `deferredReason`; a missing semantic is not
   represented by a magic zero.

3. **Create `features/empires-endgame/alchemy/`.** At minimum add `types.ts`, `engine.ts`,
   `replay.ts` (or equivalent pure resolver), and focused specs. Define typed
   `EmpiresAlchemyPlan`, `EmpiresAlchemyCommand`, runtime state, and result. `step()` advances
   exactly one configured `tickMs`; collision, nearest-piece selection, four-sided spawn,
   no-backward movement, acceleration, reagent application, completion, and explosion are
   deterministic. Use the serialized minigame seed/RNG only; stable-sort every tie.

4. **Extend the shared envelope, do not fork it.** Add the alchemy kind and typed plan/result
   arms to the existing discriminated unions. Runtime board state stays in the component/
   simulator and is never saved. Replay accepts `(plan, seed, commandLog)`; reload restarts
   from the serialized plan/seed with `attempt + 1` according to the shared contract.
   `abortMinigame` uses an authored/configured penalty, not a free reset.

5. **Route outcomes through campaign services.** `resolveMinigame` validates/replays the
   submitted command log once, records the canonical result, and applies rewards once.
   An explosion result may carry a typed epidemic request such as
   `{ cityId, source: 'alchemy', epidemicDefinitionId, severity }`, but the campaign engine
   must pass it to Phase 5's existing `startEpidemic`; the simulator and Vue component must
   never mutate campaign epidemic state. Derive `cityId` from the verified laboratory
   `origin` and reject stale/inaccessible origins.

6. **Persist only campaign consequences.** If the export requires durable alchemy outcomes,
   use a small normalized summary such as completed recipe IDs/crafted item IDs and an
   explosion count; do not save falling pieces, clocks, or interpolated positions. Add the
   next save-normalization default for any new campaign field. Bump the save envelope only
   for a semantic migration and restore a previous-version fixture in tests.

7. **Wire science and poison crafting honestly.** Reference exact post-prerequisite
   technology IDs and existing inventory/resource contracts. If the raw export does not
   define walls, recipe inputs/outputs, or where crafted poisons live, leave those entries
   disabled with `deferredReason` and ledger questions; do not invent an inventory system
   ahead of Phase 11.

8. **Build the UI in `src/components/empires-endgame/AlchemyMinigame.vue`** (split smaller
   components only if useful). Use a rAF accumulator for rendering that calls the same
   fixed-step engine used by replay/QA; render interpolation must not affect rules. Include
   Assembly/Disassembly HUD, reagent controls, speed/explosion warning, keyboard and pointer
   controls, accessible labels, pause/abort confirmation, and a `?qa=1` fast-resolve control.

9. **Integrate Builder and QA.** Expose the typed `alchemy` config in the Builder with
   validation feedback; do not build a bespoke second config store. Add scripted policies
   and scenarios (successful Assembly, Disassembly, reagent use, and explosion) to the
   existing QA action/digest/stall infrastructure. Full-campaign autoplay must be able to
   enter and resolve alchemy without a second simulation path.

## Un-deferral list

**Guaranteed existing carriers to un-defer in Phase 10: none.** `building-alchemy` belongs
to Phase 5 and must already be live; verify it, but do not delete or rewrite its marker here
to hide a failed prerequisite. No current card, gift, building, technology, event, or relic
is assigned to Phase 10 by the program inventory.

Never delete unrelated `deferredReason` fields. New alchemy subfeatures whose semantics are
not established by the raw export remain disabled and carry their own `deferredReason`.
If the export unexpectedly identifies an existing deferred carrier as essential, report
the exact ID/path and either leave it for a separately scoped change-set or un-defer it only
when its complete substrate, typed payload/consumed flag, and tests genuinely fit this phase.

## Verification

- `alchemy/engine.spec.ts`: same `(plan, seed, commandLog)` twice produces byte-for-byte
  identical result/digest; fixed-step behavior is independent of render frame cadence.
- Table-driven specs for four-side spawn, nearest eligible control, no-backward commands,
  collision/locking, sourced ×3 inward behavior, arithmetic acceleration, each reagent,
  Assembly and Disassembly completion/failure, and a tick/move cap.
- Explosion spec at the configured threshold: exactly one canonical result and exactly one
  typed `startEpidemic` call at the origin city; below-threshold and stale-origin cases do
  not start one. Test the 400% value only if it was confirmed or explicitly ledgered.
- Envelope/save specs: reload increments `attempt` and restarts from plan/seed; abort applies
  the authored penalty; previous config and save fixtures migrate/backfill correctly.
- QA scenarios `alchemy-assembly` and `alchemy-explosion` (names may follow the established
  catalog convention), scripted `resolve-minigame` policy, digest coverage, and termination
  under a command/tick cap for at least three seeds.
- Cypress exercises the visible alchemy HUD and uses only QA fast resolution; wire any new
  `.cy.ts` file into `test:empires:e2e`.
- Full-campaign autoplay crosses the alchemy session and its epidemic consequence without
  a stall or duplicate reward.

Complete every standing gate:

- `bash tools/test-empires-endgame.sh` green (Vitest + Cypress); wire every new spec into
  `Web/VueClient/package.json` `test:empires` / `test:empires:e2e` as required by the actual
  Phase-0 script state.
- `pnpm --dir Web/VueClient build` green; do **not** use the broken `type-check` command.
- `bash tools/verify-docs.sh --changed` green.
- Update `docs/WEB-CLIENT.md` §12B for exact shipped behavior; catalogue any newly found
  bug in `docs/AUDIT-FINDINGS.md` with the next free ID.
- Append every invented number to `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md`, keyed by its
  exact JSON pointer.
- Every config/save schema bump includes a previous-version restore/migration fixture.
- Write `docs/commit-messages/<date>.md` (or the next same-day suffix); do not commit/push.

## Docs & ledger contract

- Document the envelope kind, deterministic replay, Assembly/Disassembly rules, reagents,
  explosion threshold/consequence, epidemic routing, abort/reload behavior, and any
  intentionally deferred recipe path in `docs/WEB-CLIENT.md` §12B.
- Resolve/append ledger seed #9 (epidemic severity/spread and alchemy explosion) and add
  entries for board dimensions, timings, acceleration, reagent values, rewards, penalties,
  recipes, and QA caps whenever they are not raw-source numbers.
- Increment only the patch component of the current `GameVersion` in
  `King-of-the-Garbage-Hill/Game/Classes/GameClass.cs`, sequentially by one after the
  implementation is complete. Do not hard-code a target version from this prompt.
- The commit-message file should summarize the deterministic alchemy minigame, typed
  epidemic integration, migrations, UI/QA, tests, docs, and version bump.

## Designer questions

1. What action/building/technology opens each mode, what does an attempt cost, and what is
   the authored abort/reload penalty?
2. What are the board dimensions, piece shapes/colors, spawn order, central target, legal
   movement axes, lock rules, and exact success/failure conditions for both modes?
3. Does “nearest” mean nearest to the center, cursor, or active face, and how are ties
   resolved?
4. Is inward movement exactly ×3, and is explosion exactly at 400%, above 400%, or after a
   grace tick? Is 400 a sourced rule or a tuning default?
5. What is the arithmetic acceleration step/cadence, and which actions reset or alter it?
6. What are each reagent's acquisition cost, charges, target scope, ordering, and gray-piece
   behavior?
7. Which epidemic definition, severity, duration/spread rules, population effects, and lab
   city target follow an explosion? Are mutants separate typed state or flavor only?
8. Which exact science technology IDs modify alchemy, and what are the poison-wall recipes,
   outputs, storage, and consumers?

If a question changes semantics rather than a number and the raw export does not answer it,
keep that mechanic disabled/deferred and record the question; do not choose silently.
