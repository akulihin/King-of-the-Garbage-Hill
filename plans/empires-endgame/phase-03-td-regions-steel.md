# Phase 3A — TD hardening, regional battlefields, and assault

Use this file as the opening instruction for a fresh Codex 5.6 Sol task. Read
`COMMON-EXECUTION-CONTRACT.md` and `COVERAGE-MATRIX.md`; both are binding. This is the
first prompt after the completed P0/P1 work and the active/near-complete P2 change-set.

Execute one change-set. Do not start until Phase 2 has a coherent handoff: its current
implementation, tests, docs, version bump, and commit-message proposal are either complete
or the exact unfinished items have been identified for the carryover gate below.

## Guaranteed deliverable

1. Reconcile—not redo—P2 and close only its concrete unfinished obligations.
2. Harden the shared minigame/TD contract with immutable rules identity, logical command
   times, active-config safety, bounded histories, background-tab behavior, accessible
   controls, and one real input-path test.
3. Expand TD to the five authored regional battlefields, castle/naval-enemy variants, the
   complete grade-choice data, and reusable assault mode.

Steel research/equipment progression is **not** part of this change-set; P3B owns it.

## Required raw sources

Read `DiscordExports/empire_prompt`; channels `тд`, `застройка`, and `общее`; the `EE_TD`
sketch; and any linked battlefield images. Read the current P2 TD/combat implementation and
tests before designing extensions.

Raw leads to verify:

- swamp: unreachable tower spots;
- forest: archers/tree build spots;
- north: catapult/trebuchet restriction plus defense against ships;
- desert: defender desiccation/attrition;
- center: the P2 Тетракор field;
- four sequential grades, four choices per grade, regional/general/general/regional-ultra;
- castle defense, deployed units, barricades, fort-post/camp/partisan concepts, assault,
  and Эдемская катапульта only where the raw source defines executable rules.

Enemy ships in a TD plan do not create a player fleet or persistent siege system. P6B may
not use this phase as proof that player-fleet semantics exist.

## Preflight and Phase-2 carryover gate

Audit actual P2 code/tests/docs against its frozen prompt. Produce a short checklist in the
task plan for:

- minigame session/result/origin and phase return;
- fixed-step replay and serialized seed/RNG;
- wave scheduling without duplicate queues;
- canonical settlement and exact-once resolution/abort;
- old-save/config migration;
- TD UI, QA fast resolve, Cypress settlement, and full autoplay;
- the exact P2 carriers: four units, Barracks, Smithy, `tech-ironwork`, `doctrine-war`,
  both ♥7 faces, and `gift-combat-spirit`.

If an item is already complete, regression-test it and leave it alone. If it is concretely
unfinished, complete it inside this P3A change-set before regional expansion. Do not reopen
P0/P1 or broaden P2 beyond this list. Preserve the user's current P2 worktree exactly.

## Work items

1. **Rules identity and active-config safety.** Extend the shared session with either a
   fully resolved simulation plan or `{configSchemaVersion, rulesDigest}` covering every
   TD/combat definition used by replay. Canonicalize digest input. Resolve/reload rejects
   stale or mismatched rules. Builder/import/config replacement while a session is active
   must be rejected or explicitly restart through the authored abort path—never mutate the
   battle silently.

2. **Logical command log.** Store commands with simulation tick indices and deterministic
   sequence numbers. Validate monotonic/bounded ticks, stable tie order, legal command kind,
   and plan/session identity. Do not log wall-clock timestamps.

3. **Runtime/storage bounds.** Define caps or compaction for command logs and
   `minigameResultLog`; retain enough identity/digest data for audit. Cap rAF catch-up work
   per frame or pause on backgrounding, with a deterministic fake-clock test proving frame
   cadence does not change the result.

4. **Regional config.** Extend the current `td` section with typed battlefield definitions,
   lane graphs, build spots, castle/fort objectives, allowed categories, environmental
   modifiers, grade choices, and defense/assault plan variants. Validate unique IDs and all
   tower/combat/region references. Old custom configs receive a safe fallback/disabled
   regional catalog through the current migration chain.

5. **One modifier engine.** Implement region behavior as typed data/rules, not branches on
   Russian display names. At minimum cover sourced swamp reachability, forest tree bonuses,
   north category restrictions/naval enemy path, and desert attrition. Stable-order all
   simultaneous ticks/targets.

6. **Assault/castle/naval-enemy variants.** Add `plan.mode` and typed objectives. Assault
   fields player units against authored defenses and returns through the same replay and
   settlement funnel as defense. Castle and ship-defense variants reuse that engine; do not
   create a second simulator or calculate campaign losses in the UI.

7. **Grade matrix and UI.** Port only the raw-authored grade structure/stats; missing numbers
   are config+ledger values. Update TD UI for battlefield rules, objectives, accessible
   tower/deployment choices, and keyboard/pointer commands. Canvas is not the sole state
   representation. Keep QA fast resolve, but add a deterministic component test that sends
   a real input through the production command/replay path.

8. **QA/Builder.** Add scenarios for each regional modifier and `battle-assault`; extend
   digest/trace with rules identity and bounded log metadata. Builder support is JSON-first
   with the production validator. Full campaign autoplay must cross scheduled defense and
   assault sessions without stalls.

## Carrier and scope gate

- No steel technology, Foundry, Military Academy, military-workshop addition, or steel card
  is un-deferred here; P3B owns them.
- Do not un-defer `event-northern-raids`: its authored choices are loyalty effects owned by
  P4A, not an invented TD battle.
- P2 carriers may change only under the carryover checklist above. Record each already-live,
  completed-here, or still-blocked result explicitly.

## Verification additions

- Same plan/seed/log and rules identity produce the same full digest across frame cadences,
  reload, and headless/UI replay; changed rules identity is rejected.
- Table-driven regional modifier and allowed-category cases; defense/assault/castle/naval
  terminal outcomes; no duplicate campaign settlement.
- Background pause/catch-up cap, command-log validation/retention, active config-import
  rejection, keyboard/pointer command smoke, and accessible state/disabled reasons.
- Previous config/save fixture; P2 carryover regression manifest; five-field QA sweep under
  tick/action caps; full standing gate from the common contract.

## Designer questions

- What are authoritative stats behind the 4×4 grades and “208 builds” count?
- Exact swamp, forest, north, and desert modifier values/edge cases?
- Which castle, barricade, camp, post, partisan, ship, and Эдемская-катапульта mechanics are
  executable now, and which remain deferred?
- What are the authored assault loss/abort/retry consequences?
- What command-log/history retention is sufficient for replay evidence in localStorage?
