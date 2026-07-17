# Phase 11 — Expeditions + Tetris-inventory

You are executing Phase 11 of the Empire's Endgame completion program with Codex 5.6
Sol. Read `AGENTS.md` and `plans/empires-endgame/README.md` first. This is one
implementation change-set. It depends on Phase 3's real TD assault path and combat/equipment
model, Phase 7's quest/dialogue engine, and the shared minigame envelope from Phase 2.

## Executor rules (binding)

1. **Design source discipline**: read the phase's named raw export channels in `DiscordExports/Empires_Endgame/` before implementing. On conflict: main export > `ZBS MAKING` (outdated background); `empire_prompt` defines the core loop. Export missing from the environment → stop and tell the user; do not guess.
2. **Never fabricate silently**: a mechanic without export numbers → implement with a configurable default in `game-config.json` + append a ledger entry. A mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
3. **Un-deferral discipline**: substrate + un-deferral (delete `deferredReason`, add executable effects) + `EMPIRES_LIVE_FLAG_ALLOWLIST` additions or typed payloads + tests, all in ONE change-set. `validateLiveEffects` must keep rejecting flags nothing reads.
4. **Determinism**: no `Date.now`/`Math.random` in any simulation — only the serialized RNG streams (`features/empires-endgame/rng.ts`). Minigames replay from `(plan, seed, commandLog)`; mid-minigame real-time state is never serialized.
5. Player-facing card/passive *texts* stay deliberately vague (repo philosophy — never "fix" them; the designer writes new player wording). Exact mechanics are documented in `docs/WEB-CLIENT.md` §12B.
6. Git: do NOT commit or push. Write the commit message to `docs/commit-messages/<date>.md` (one file per change-set; `-2`, `-3` suffixes for further change-sets the same day).

## Codex 5.6 Sol preflight and dependency audit

Before editing:

1. Run `git status --short`; preserve all unrelated worktree changes. Read this prompt,
   the README, `docs/WEB-CLIENT.md` §12B, and the named raw exports. Maintain a live task
   plan and keep a single owner for `types.ts`, `config.ts`, `engine.ts`,
   `game-config.json`, `qa.ts`, and `EmpiresEndgame.vue`.
2. Prove Phase 2 landed: locate the serialized minigame envelope, replay contract,
   `beginMinigame`/`resolveMinigame`/`abortMinigame`, reload `attempt + 1`, and QA
   `resolve-minigame` action with tests.
3. Prove Phase 3 landed: locate one shared `features/empires-endgame/combat/` resolver,
   `features/empires-endgame/td/`, typed `plan.mode: 'assault'`, a deterministic TD replay,
   army/equipment state, and the canonical battle-outcome settlement path. Run or inspect
   an existing assault-mode test; a defense-only TD shell is not sufficient.
4. Prove Phase 7 landed: locate typed quest state/config, trigger evaluation, dialogue
   advancement, Journal/overlay UI, and the post-minigame trigger hook. Expedition
   complaints and unlock quests must reuse this engine rather than create another dialogue
   state machine.
5. Inspect current config/save schema migrations, `EmpiresMapObjectDefinition`, the exact
   existing `map-south-fortress` object, unit/veteran representation, map lock/unlock state,
   `provisionEfficiencyPercent`, config validation, prior-version fixtures, QA catalog, and
   test script wiring.
6. If assault mode, quest integration, combat/equipment, or the envelope is missing or only
   exists in a prompt, **stop before code edits and report exact evidence**. Do not fold a
   prerequisite phase into Phase 11.

## Mission

Ship first-class expeditions that start from authored quest/map triggers, pack provisions
through a deterministic Tetris-inventory minigame, reuse the one TD assault simulator to
attack border fortresses, persist roster wounds/veterans and expedition progress, unlock
authored zones after fortress destruction, and route regional complaints through the
existing quest/dialogue system. There must be one combat resolution path and one minigame
envelope; UI, QA, and headless replay all consume them.

## Design source — read before implementing

Resolve and read the complete relevant export files on the designer's machine:

- `DiscordExports/empire_prompt` for the campaign loop.
- `DiscordExports/Empires_Endgame/` channel `экспедиции` — authoritative expedition
  lifecycle, provisioning, fortresses, wounds/veterans, rewards, complaints, and regions.
- Channel `чо-добавить` — authoritative Tetris-inventory/cart packing notes.
- Cross-references in `тд` and the linked `EE_TD` sketch for assault/fort rules; `сталь`
  and `Steel-c748ae22139d6401.txt` for equipment/damage-type references; `квесты` for
  unlocks and regional complaint dialogue. Main export wins over `ZBS MAKING`.
- Read any linked image/HTML/sketch named by these messages rather than inferring its
  content from a filename.

If `DiscordExports/Empires_Endgame/`, `экспедиции`, or `чо-добавить` is missing, **stop and
tell the user before editing**. Use this compressed model only to navigate the raw source:

- Killing a border fortress opens a zone; expedition combat reuses TD assault mode.
- Provision equipment gives a one-time reduction, but the compression does not establish
  reduction target, amount, stacking, or consumption timing.
- Regions can complain about expedition actions; those consequences belong in quests.
- A unit above 50% HP after the relevant outcome becomes a Veteran; a second wound removes
  it. Confirm the exact timing, comparison, and meaning of “second wound” in the export.
- Enemy profiles differ by damage/armor type: South is described as unarmored, West uses
  bone/leather, and swamp enemies are creatures. Exact stats and encounters are absent.
- Tetris-inventory packs an expedition cart while items fall in real time. Board, shapes,
  gravity, controls, scoring, and failure rules require `чо-добавить`.

## Repo anchors — inspect before editing

- `Web/VueClient/public/empires-endgame/game-config.json`:
  `empire.map.objects`, exact live object `map-south-fortress` (`kind: 'fortress'`, legacy
  `properties.defenseLayer`), regions/subregions, units, resources, and all deferrals.
- `features/empires-endgame/types.ts`: `EmpiresMapObjectDefinition`, campaign/army state,
  combat/TD plans and results, quest state, and minigame discriminated unions.
- `features/empires-endgame/config.ts`: migration chain, reference validation, deferral and
  live-effect contracts. `provisionEfficiencyPercent` is already allowlisted, but that is
  not permission to misuse it as per-expedition lifecycle state.
- `features/empires-endgame/td/` and `combat/`: assault plan/replay/result and equipment /
  damage-type resolution. Extend through public contracts, not copied internals.
- `engine.ts` or extracted `engine/` modules: minigame transition, battle settlement,
  quest trigger hooks, map access, restore normalization, and phase progression.
- `features/empires-endgame/quests.ts`, `qa.ts`, and `rng.ts`.
- `src/components/empires-endgame/EmpireMap.vue`, `TdBattle.vue`, `DialogueOverlay.vue`,
  `QuestJournal.vue`, `BuilderDrawer.vue`, and page `src/pages/EmpiresEndgame.vue`.
- `persistence.ts`, package test scripts, `tools/test-empires-endgame.sh`,
  `docs/WEB-CLIENT.md` §12B, the designer-review ledger, and
  `King-of-the-Garbage-Hill/Game/Classes/GameClass.cs`.

Re-locate anchors by symbol if prior phases extracted files or shifted lines.

## Work items (in order)

1. **Transcribe the expedition contract.** From the raw exports, enumerate exact expedition
   IDs, trigger quest IDs, origin/target, fortress/map object, target zone, stages, eligible
   roster, enemy profile, provision rules, complaint triggers, rewards, loss/abort rules,
   and repeatability. Record every missing number as a config default plus ledger entry;
   undefined semantics stay disabled/deferred.

2. **Add typed expedition config and migration.** Advance the actual config schema one
   migration step if required. A target shape is:

   ```ts
   interface EmpiresExpeditionsConfig {
     enabled: boolean
     definitions: EmpiresExpeditionDefinition[]
     enemyProfiles: EmpiresExpeditionEnemyProfile[]
     provisionItems: EmpiresProvisionItemDefinition[]
     inventory: EmpiresInventoryConfig
   }

   interface EmpiresExpeditionDefinition {
     id: string
     triggerQuestId?: string
     originCityId?: string
     fortressObjectId: string
     unlockZoneId: string
     assaultPlanId: string
     enemyProfileId: string
     complaintQuestIds: string[]
     rewards: EmpiresEffect[]
     deferredReason?: string
   }
   ```

   Validate unique IDs and every city, region, map object, zone, TD plan, quest, item,
   equipment, resource, damage type, and reward reference. Migrate the previous config to a
   safe disabled/empty expedition section without changing behavior.

3. **Make fortress objects typed.** Replace fortress-specific reliance on the generic
   `properties` bag with a discriminated fortress payload, while preserving other map
   object kinds. For example, the fortress arm must carry typed `expeditionId`,
   `assaultPlanId`, `unlockZoneId`, and authored defense data. Migrate
   `map-south-fortress` without changing its identity/position and add other fortresses only
   when the export names them. Builder/map validators must reject dangling references.

4. **Add first-class campaign state.** Normalize a state shaped around:

   ```ts
   interface EmpiresExpeditionState {
     definitionId: string
     status: 'locked' | 'available' | 'packing' | 'assault' | 'won' | 'failed'
     rosterUnitIds: string[]
     provision: EmpiresExpeditionProvisionState
     assaultAttempts: number
     complaintQuestIdsTriggered: string[]
   }
   ```

   Store canonical veteran/wound/HP data on the existing typed army unit entity and refer
   to unit IDs from expeditions; do not duplicate mutable unit records or encode them in
   global flags. Persist unlocked zones/fortress outcomes in one canonical location.

5. **Implement `features/empires-endgame/expeditions.ts` (or a focused directory).** Add
   pure availability/planning helpers and one settlement funnel that consumes the canonical
   TD assault result. It applies losses/wounds/veteran transitions once, consumes
   provisions, unlocks the zone on authored victory, grants rewards once, updates status,
   and invokes existing quest trigger evaluation for complaints/follow-ups. It must never
   run a second combat calculation.

6. **Create `features/empires-endgame/inventory/`.** Add typed plan, commands, runtime state,
   result, fixed-step engine, replay, scripted policies, and specs. Falling/cart behavior
   advances exactly `tickMs`; render frame rate is irrelevant. Replay is pure
   `(plan, seed, commandLog) -> EmpiresInventoryResult`, with stable tie-breaking and a
   command/tick cap. The result should identify packed item instances and a typed
   expedition-specific provision outcome, not just an unexplained score.

7. **Reuse the shared minigame envelope.** Add the inventory kind/plan/result arms. Begin it
   with an expedition origin, resolve by canonical replay, and transition the same expedition
   from `packing` to `assault`. Reload restarts from plan/seed with `attempt + 1`; abort uses
   the authored penalty. Never serialize falling items, timers, or render positions.

8. **Apply provisions with the right scope.** The already-consumed
   `provisionEfficiencyPercent` building flag may be an input bonus if the export says so;
   keep the packed-cart result in `EmpiresExpeditionProvisionState`. Define the compressed
   “one-time reduction” as typed resource/upkeep/damage mitigation only after confirming
   its target and consumption point. Do not overwrite an empire-wide flag per expedition.

9. **Expedition technology and map-card closure.** Implement
   `tech-military-logistics` through actual expedition travel/availability timing and
   `tech-supply-corps` through typed installment/provision state; consume their existing
   `expeditionSpeedPercent` and `expeditionProvisionInstallmentTurns` payloads (or replace
   them with typed effects) rather than allowlisting dead flags. Reconcile both faces of
   `card-spades-3` with the raw `карты` source: un-defer only when world-map/logistics,
   diplomacy/personal-quest, map-confusion, level scaling, cleanup, and UI semantics are
   all executable across P7/P11.

10. **Integrate quests and map flow.** Use Phase 7's trigger/stage/dialogue graph for unlock
   offers, regional complaints, choices, and aftermath. Clicking an available typed
   fortress in `EmpireMap.vue` opens an expedition planning surface; victory changes map
   access through typed state. Do not special-case Russian display names or create a second
   quest journal.

11. **Build UI and Builder support.** Add
    `src/components/empires-endgame/ExpeditionPanel.vue` and
    `InventoryPacking.vue` (split only as needed): roster/equipment/provision planning,
    accessible falling-piece controls/HUD, typed fortress state, veteran/wound feedback,
    complaint links, and `?qa=1` fast resolution. The real-time view uses an accumulator
    over the same fixed-step inventory engine used by QA. Builder editing covers expedition,
    fortress, enemy, item, and inventory configs with reference errors visible.

12. **Migrate/backfill saves.** Add normalized empty expedition/unlocked-zone/veteran fields
    to legacy saves without inventing completed progress. Preserve current army IDs and map
    state. Bump the save envelope only for a semantic move, and add a fixture restoring the
    immediately previous version.

## Un-deferral list

The expedition system itself has no deferred definition carrier. These existing definitions
are Phase-11 whole-contract candidates:

- `tech-military-logistics` — only with a real expedition speed/timing consumer.
- `tech-supply-corps` — only with typed provision installment/cadence state and consumer.
- `card-spades-3` normal and inverted — conditional on complete world-map/logistics,
  diplomacy/quest, and confusion behavior from the raw export.

`map-south-fortress` is already live and is not a `deferredReason` carrier; migrate it to a
typed fortress payload without pretending the expedition lifecycle already existed.
`provisionEfficiencyPercent` is already live/allowlisted and is not an un-deferral.

Never delete unrelated markers on units, buildings, technologies, events, gifts, cards, or
relics. If raw expedition content depends on a still-deferred carrier, report its exact ID
and JSON path. Only include it here if this same change-set supplies its complete executable
substrate, typed/consumed effects, UI, and tests; otherwise leave it deferred for a separate
scope. New expedition definitions with unresolved semantics must carry `deferredReason`.

## Verification

- Expedition unit specs: availability and reference gates; packing→assault→settlement
  transition; fortress victory unlocks exactly the authored zone and reward once; loss/
  retry/abort behavior; no duplicated TD resolution.
- Veteran table tests around the sourced threshold (below, exactly at, and above 50% as
  applicable), first wound, second wound/removal, dead units, retries, and save restore.
- Provision tests for packed items, building efficiency input, one-time consumption,
  stacking, resource accounting, and expedition isolation.
- Focused consumers for both expedition technologies and both `card-spades-3` faces if
  reconciled; otherwise assert the affected face remains deferred with its exact reason.
- Enemy-profile tests prove the shared combat resolver receives the authored South/West/
  swamp armor/damage types; do not snapshot a parallel formula.
- `inventory/engine.spec.ts`: identical replay digest for the same
  `(plan, seed, commandLog)`, frame-cadence independence, collision/rotation/placement,
  cart bounds, scoring/efficiency, failure, and termination under a cap for three seeds ×
  three policies.
- Save/config migration fixtures restore the immediately previous schemas with empty,
  non-completed expedition state and unchanged `map-south-fortress` identity.
- QA scenarios such as `expedition-assault` and `inventory-packing`; scripted
  `resolve-minigame`; digest/trace coverage; quest complaint progression; full-campaign
  autoplay without stalls.
- Cypress asserts map fortress state, planning/packing HUD, QA fast resolution, TD assault
  transition, veteran/provision result, zone unlock, and complaint dialogue. Wire new specs.

Complete every standing gate:

- `bash tools/test-empires-endgame.sh` green (Vitest + Cypress); wire every new spec into
  `Web/VueClient/package.json` `test:empires` / `test:empires:e2e` according to the actual
  Phase-0 test-script state.
- `pnpm --dir Web/VueClient build` green; do **not** run the broken `type-check` command.
- `bash tools/verify-docs.sh --changed` green.
- Update `docs/WEB-CLIENT.md` §12B; new bugs go into `docs/AUDIT-FINDINGS.md` under the next
  free ID.
- Append every invented number to `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md`, keyed by exact
  JSON pointer.
- Every config/save schema bump has a previous-version migration/restore fixture.
- Write `docs/commit-messages/<date>.md` or the next same-day suffix; no commit/push.

## Docs & ledger contract

- Document expedition availability, typed fortresses/zones, roster/provisions, TD assault
  reuse, veteran/wound rules, complaints/quests, inventory replay, abort/reload behavior,
  and intentionally deferred definitions in `docs/WEB-CLIENT.md` §12B.
- Ledger every invented fort stat, enemy profile, provision value, threshold interpretation,
  inventory dimension/timing/shape/score, retry/abort penalty, quest cadence, and QA cap by
  JSON pointer. Preserve the provenance of any exact export value.
- Increment only the patch component of the current `GameVersion` in
  `King-of-the-Garbage-Hill/Game/Classes/GameClass.cs` sequentially by one after all work
  is complete. Do not use a version copied from this prompt.
- The commit-message file summarizes first-class expedition state, shared TD/quest reuse,
  typed fortress migration, deterministic inventory, UI/QA, compatibility tests, docs, and
  version bump.

## Designer questions

1. Which exact expedition definitions, trigger quest IDs, origin cities, border fortress
   object IDs, and zones ship in this phase? Is `map-south-fortress` the first target?
2. What does “killing” a fortress mean in TD assault, and exactly when/permanently how does
   its zone unlock? What are retry, failure, and abort consequences?
3. Which units may join; how are HP and wounds carried from ordinary TD; does Veteran mean
   strictly `> 50%` HP or `>= 50%`; and what exactly counts as the second wound/removal?
4. What is “провизия-экипировка”, what receives the one-time reduction, how large is it,
   when is it consumed, and how does it stack with `provisionEfficiencyPercent`?
5. Which expedition actions trigger which regional complaints, at what stage, and which
   authored quest/dialogue nodes and consequences resolve them?
6. What exact enemy units, armor/damage types, stats, waves, and rewards belong to South,
   West, swamp, North, and center?
7. What are the cart/board dimensions, item shapes/weights, spawn and gravity timing,
   controls/rotation, collision, scoring-to-efficiency mapping, success/failure, and cap?
8. What is the inventory minigame's authored abort/reload penalty, and can packing be
   retried before assault?
9. What exact operations are accelerated by `tech-military-logistics`, how do supply-corps
   installments work, and do both `card-spades-3` faces belong wholly to expeditions or
   also require separate diplomacy/personal-quest behavior?

Undefined semantics remain disabled/deferred and become explicit ledger questions; do not
silently resolve them with conventional game assumptions.
