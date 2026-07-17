# Phase 2 — TD vertical slice → army goes live

> **Status: complete (2026-07-17).** This file is retained as the historical execution
> prompt. Do not re-run or expand it; Phase 3A owns subsequent TD hardening and regional work.

This phase was executed as one change-set after Phase 0's migrations/state homes and Phase
1's `combat/` module. Its detailed instructions below are retained as implementation history.

## Executor rules (binding)

1. **Design source discipline**: read the phase's named raw export channels in `DiscordExports/Empires_Endgame/` before implementing. On conflict: main export > `ZBS MAKING` (outdated background); `empire_prompt` defines the core loop. Export missing from the environment → stop and tell the user; do not guess.
2. **Never fabricate silently**: a mechanic without export numbers → implement with a configurable default in `game-config.json` + append a ledger entry. A mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
3. **Un-deferral discipline**: substrate + un-deferral (delete `deferredReason`, add executable effects) + `EMPIRES_LIVE_FLAG_ALLOWLIST` additions or typed payloads + tests, all in ONE change-set. `validateLiveEffects` must keep rejecting flags nothing reads.
4. **Determinism**: no `Date.now`/`Math.random` in any simulation — only the serialized RNG streams (`features/empires-endgame/rng.ts`). Minigames replay from `(plan, seed, commandLog)`; mid-minigame real-time state is never serialized.
5. Player-facing card/passive *texts* stay deliberately vague (repo philosophy — never "fix" them; the designer writes new player wording). Exact mechanics are documented in `docs/WEB-CLIENT.md` §12B.
6. Git: do NOT commit or push. Write the commit message to `docs/commit-messages/<date>.md` (one file per change-set; `-2`, `-3` suffixes for further change-sets the same day).

## Codex preflight

- Read root `AGENTS.md`, `plans/empires-endgame/README.md`, and the attached/raw sources
  below. Run `git status --short` and preserve unrelated changes.
- Verify Phase 0's disabled `td` config, state defaults, test discovery, and config
  migration; verify Phase 1's `resolveDamage` and combat catalog. If either prerequisite
  is partial, stop and report the exact missing contract instead of folding it into P2.
- Make a task plan. Parallelize bounded read-only export/config/test audits if useful, but
  keep one writer for `engine.ts`, `types.ts`, `config.ts`, `game-config.json`, and the page.

## Mission

Ship the program's first playable combat slice: the shared minigame envelope, a
fixed-timestep deterministic Tower Defense on one central defense battlefield, periodic
Alliance waves, campaign battle settlement, army/morale/veteran state, and the associated
army content un-deferrals. A campaign must enter TD, render or resolve the same simulator,
return to the correct campaign phase exactly once, and remain replayable/headless in QA.

## Design source

Read these sources before implementation:

- `DiscordExports/Empires_Endgame/` channel `тд` and the `EE_TD` sketch file;
- channel `застройка`, especially `message-d27f1e9af25bf194.txt` for army/recruitment;
- channels `общее`, `карты`, and `божественные-награды` for cadence, ♥7, and morale gift;
- `DiscordExports/empire_prompt` for the core con/empire loop.

The compressed rules are: Alliance attack/defense at regional borders; waves roughly
every four months (currently modeled as two cons pending confirmation); towers have four
sequential grades with four choices per grade; the army has seven authored categories,
losses suppress recruitment/growth, losses of at least 10% create loyalty pressure, and
survivors above 50% HP may become veterans. The first slice uses one central battlefield,
one generic tower family, a castle, and defense mode only. Missing export numbers become
config defaults plus ledger rows; missing semantics stay deferred. If any named source is
missing, stop and tell the user before editing—do not implement from this compression.

## Architecture (binding)

- Shared envelope:
  `EmpiresMinigameSession { kind, plan, seed, attempt, origin }` and a discriminated
  `EmpiresMinigameResult`; add `'minigame'` to `EMPIRES_PHASES`; campaign methods
  `beginMinigame`, `resolveMinigame`, and `abortMinigame`. Origin must encode the return
  phase/context. Resolution/abort is idempotent and appends one result record.
- Mid-battle real-time state is never serialized. Restoring a save with an active session
  restarts from the same plan/seed with `attempt + 1`; command input starts fresh. A result
  may retain the command log or digest for deterministic replay/audit.
- TD uses a fixed tick: `step()` advances exactly `config.td.tickMs`; a battle is
  `f(plan, seed, commandLog) -> result`. Rendering rAF only accumulates wall time and
  interpolates. Headless QA calls the exact same step/replay code.
- Every hit goes through Phase 1's `combat/damage.ts`. There is no abstract parallel
  battle resolver.

## Repo anchors (read before editing)

- `features/empires-endgame/engine.ts`: `resolveBout` near `:1122`, `startEmpirePhase`
  `:1193`, `startNextCon` `:1371`, `validateAndCloneSnapshot` `:1002`,
  `firstMissingDependency` `:1477`, `recruitUnits` near `:526`, and the existing
  `militaryArson` reads/application at `:1069`/`:1240`.
- `features/empires-endgame/types.ts`: `EMPIRES_PHASES` `:18`, unit/city/campaign types.
- `features/empires-endgame/config.ts`: `validateDeferredReasons`,
  `EMPIRES_LIVE_FLAG_ALLOWLIST`, and `validateLiveEffects`.
- `features/empires-endgame/qa.ts`: `digestEmpiresQaState` near `:464`, fixtures, and
  trace/stall autoplay near `:814+`; extend this harness rather than forking it.
- `features/empires-endgame/rng.ts`; `features/last-chances/engine.ts:724` is a render-loop
  reference only—the rAF-delta simulation model is deliberately not copied.
- `src/pages/EmpiresEndgame.vue` and `src/components/empires-endgame/` for phase routing/UI.
- Bundled carriers: four unit IDs, barracks/smithy, `doctrine-war`, `tech-ironwork`,
  `card-hearts-7`, and `gift-combat-spirit` in `game-config.json`.

## Work items (in order)

1. Add the generic minigame envelope/types and engine API. Bump campaign-state/snapshot
   envelope schema sequentially from v1 to v2 because `'minigame'` becomes a semantic
   phase; ship a v1→v2 restore migration/fixture. Preserve Phase-0 defaults and normalize
   missing result logs/origin fields. Never serialize timers, entities, or canvas state.
2. Add `features/empires-endgame/td/types.ts`, `engine.ts`, `qa.ts`, and `engine.spec.ts`.
   Define typed plan/state/command/result, lane graph, build spots, castle, spawner,
   targeting, tower upgrades, deployed units, and terminal reasons. Make `stepTdSimulation`
   the only state transition and `replayTdBattle(plan, seed, commandLog)` the only result
   path. Enforce a configured tick cap/error instead of hanging.
3. Populate the bundled `td` section and enable it: `tickMs`; one central battlefield;
   generic 4-grade × 4-choice tower data; Alliance wave table/curve; starting build
   resources; `waveEveryCons: 2`; abort penalty; loss/veteran thresholds. A migrated
   custom config with `td.enabled: false` and empty catalogs stays valid; enabled configs
   require a complete, referentially valid definition. Backfill/compat tests are mandatory.
4. Extend typed state, preserving Phase-0 ownership:

   ```ts
   army: {
     equipmentStock: Record<string, number>
     pendingLoyaltyDeltas: Array<{ cityId?: string; regionId?: string; amount: number; sourceId: string }>
     morale: number
     maxMorale: number
     veterans: Record<string, { unitId: string; wounds: number }>
     recruitmentPenalties: Record<string, number>
   }
   external: { allianceThreat: number; nextWaveCon: number; pendingOffers: [] }
   ```

   Normalize every new additive field on old v1/v2 saves.
5. Schedule waves without duplication across saves: advance `nextWaveCon` exactly once,
   queue a battle when due, and enter the minigame at a safe campaign boundary with a
   pre-battle deployment plan. Use only serialized RNG to derive the session seed. Resolve
   event/divine-gift ordering explicitly; never strand the campaign between phases.
6. Implement `settleBattleOutcome(result)`: validate the result against the active plan;
   reduce per-city recruited units; apply configured recruitment/growth penalties; write a
   typed pending loyalty delta for ≥10% loss (Phase 4 owns the final funnel); mark eligible
   >50%-HP survivors as veterans; apply configured victory/defeat/abort consequences; and
   clear/return from the session exactly once.
7. Wire army substrate to existing content: TD/combat profiles on the four existing units;
   barracks tiers and recruitment; smithy capacity and actual production into
   `army.equipmentStock`; war doctrine branch availability; minimal morale/max-morale
   state. Resolve the export's seven army categories against the four current IDs—add new
   definitions only when the raw source establishes a stable mapping; otherwise keep them
   deferred and ledger the question.
8. Add `src/components/empires-endgame/TdBattle.vue`: canvas render, HUD, grade drawer,
   deployment/pre-battle state, and ×1/×2/×4 controls implemented as ticks-per-frame (not
   dt scaling). QA mode alone exposes a policy-based fast-resolve control.
9. Extend QA actions/digest/trace for `resolve-minigame`, named policies `passive`,
   `greedy-build`, `balanced`, and the `battle-defense` scenario. Existing full-campaign
   autoplay must cross battles without a second simulator or stall suppression hack.

## Un-deferral list (substrate + consumer + tests in this change-set)

- `unit-light`, `unit-regular`, `unit-heavy`, `unit-knight`: typed TD/combat profiles,
  recruitment, deployment, casualties, and tests.
- `building-barracks`: authored `equippedRecruitCapacity` remains read by recruitment and
  gains tests for each live level.
- `building-smithy`: `smithCapacity` must be consumed by typed equipment production—not
  merely added to the flag allowlist.
- `tech-ironwork`: prerequisite closure for smithy and later steel roots. Its
  `smithyUnlocked` payload must have an actual availability consumer or be replaced by a
  typed unlock; do not make a dead flag live.
- `doctrine-war`: its `doctrineWar` effect must be consumed by the war branch/TD substrate
  or represented as a typed unlock.
- Both faces of `card-hearts-7`: keep the authored flags and current semantics exactly—
  `unlimitedTavernRecruitment` normal; `militaryArson` + `recruitmentDisabled` inverted.
  These already have consumers/allowlist entries; add face-level regression tests.
- `gift-combat-spirit`: consume `maxCombatSpirit` through the typed morale cap (or a typed
  resolution), with draft/application/save tests.

Before deleting each marker, prove its prerequisites are reachable and its complete
effect is executed. No allowlist-only implementation is acceptable.

## Verification

- TD unit specs: fixed-step boundary cases, legal/illegal commands, combat-module hits,
  castle win/loss, tick-cap termination, and immutable plan/config inputs.
- Determinism gate: identical `(plan, seed, commandLog)` twice yields identical result and
  digest. Headless autoplay terminates for 3 seeds × all 3 policies; different rendering
  frame chunking yields the same result.
- Campaign specs: cadence/no duplicate wave after restore; minigame begin/resolve/abort;
  result validation/idempotency; unit losses; recruitment penalties; pending loyalty;
  veterans; morale cap; every un-deferred consumer.
- Save compatibility: restore a genuine v1 fixture; active v2 minigame reload increments
  `attempt` and restarts from plan/seed; result logs round-trip.
- QA/Cypress: add `battle-defense`, `resolve-minigame`, and
  `cypress/e2e/empires-endgame-td.cy.ts`; wire both scripts. Cypress asserts HUD and uses
  QA fast-resolve—never real-time play.
- Full standing gate:
  - `bash tools/test-empires-endgame.sh`
  - `pnpm --dir Web/VueClient build`
  - `bash tools/verify-docs.sh --changed`
- Inspect the final diff/status and search new sim code for forbidden wall-clock/random APIs.

## Docs & ledger contract

- Update `docs/WEB-CLIENT.md` §12B for the minigame envelope, fixed-step TD, wave
  scheduler, army settlement, save schema v2, and exact un-deferrals.
- Record every invented tower stat, wave cadence, Alliance curve, abort/loss consequence,
  morale value, and mapping/default by JSON Pointer in the append-only ledger.
- New pre-existing bugs get the next finding ID; do not hide unrelated fixes in P2.
- Increment `GameVersion`'s patch component sequentially in
  `King-of-the-Garbage-Hill/Game/Classes/GameClass.cs`.
- Write `docs/commit-messages/<date>.md`; do not commit or push.

## Designer questions (pre-seeded)

- Does “every four months” mean exactly `waveEveryCons: 2` under the current clock?
- What are the authoritative 4×4 tower stats and how is “208 builds” counted?
- What are the morale scale/floor/cap and the authored battle-abort penalty?
- What Alliance strength curve and defeat consequences are intended?
- Which seven authored army categories map to the four current unit IDs, and which require
  new definitions now rather than in a later phase?
