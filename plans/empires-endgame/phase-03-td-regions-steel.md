# Phase 3 — TD regional depth + steel tree

Use this file as the opening instruction for a fresh Codex task running 5.6 Sol. Execute
one complete change-set. Dependency: Phase 2 must provide the fixed-step TD, minigame
envelope, live army, morale, and `tech-ironwork`; verify those contracts first.

## Executor rules (binding)

1. **Design source discipline**: read the phase's named raw export channels in `DiscordExports/Empires_Endgame/` before implementing. On conflict: main export > `ZBS MAKING` (outdated background); `empire_prompt` defines the core loop. Export missing from the environment → stop and tell the user; do not guess.
2. **Never fabricate silently**: a mechanic without export numbers → implement with a configurable default in `game-config.json` + append a ledger entry. A mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
3. **Un-deferral discipline**: substrate + un-deferral (delete `deferredReason`, add executable effects) + `EMPIRES_LIVE_FLAG_ALLOWLIST` additions or typed payloads + tests, all in ONE change-set. `validateLiveEffects` must keep rejecting flags nothing reads.
4. **Determinism**: no `Date.now`/`Math.random` in any simulation — only the serialized RNG streams (`features/empires-endgame/rng.ts`). Minigames replay from `(plan, seed, commandLog)`; mid-minigame real-time state is never serialized.
5. Player-facing card/passive *texts* stay deliberately vague (repo philosophy — never "fix" them; the designer writes new player wording). Exact mechanics are documented in `docs/WEB-CLIENT.md` §12B.
6. Git: do NOT commit or push. Write the commit message to `docs/commit-messages/<date>.md` (one file per change-set; `-2`, `-3` suffixes for further change-sets the same day).

## Codex preflight

- Read root `AGENTS.md` and the prompt-pack README; run `git status --short` and preserve
  unrelated work.
- Verify P2's actual state: TD replay determinism, schema-v2 restore, central battlefield,
  combat profiles, equipment stock, morale, `doctrine-war`, `tech-ironwork`, and all P2
  un-deferrals. Stop with evidence if any prerequisite is absent or partial.
- Inventory current steel IDs/effects/prerequisites from config before editing. The current
  22 nodes cover only five config groups, not the full ten-branch export model; do not
  confuse “all existing nodes” with “the full authored tree.”

## Mission

Expand TD from the central vertical slice to five authored regional battlefields, castle
and naval variants, the complete grade data, and reusable assault mode. Port the latest
steel tree faithfully, make every executable steel payoff flow through combat/equipment
and army production, and bring the military academy/foundry/morale relic live only with
their full authored consumers.

## Design source

Read `DiscordExports/Empires_Endgame/` channel `тд`, the `EE_TD` sketch, channel
`застройка`, and `Steel-c748ae22139d6401.txt` (the latest steel version under the
`Технологии_*` material). Older steel files and `ZBS MAKING` lose on conflict.

Verify and encode:

- swamp: unreachable tower spots; forest: archers in trees; north: only
  catapults/trebuchets plus a ships-defense variant; desert: defender desiccation; central:
  the P2 generic battlefield;
- four sequential grades—regional → common → common → regional ultra—with four choices
  per grade, stacking, and the tower/shooter/projectile/regional tier scheme;
- castle defense, units, barricades, fortress posts, partisan/mercenary camps, and
  Эдемская катапульта only where the raw sources define their role;
- steel branches, fork pricing, generation half-steps, delayed-free “+” generations,
  |Элитное| gates, and production/equipment prerequisites.

If any named source is missing, stop and tell the user before editing. Numeric gaps may be
configurable and ledgered; semantic gaps stay deferred.

## Repo anchors (read before editing)

- `features/empires-endgame/td/`, `combat/`, and P2 army/minigame types.
- `features/empires-endgame/engine.ts`: `research` near `:636`, `startNextCon` near
  `:1371`, snapshot normalization near `:1002`, and battle settlement.
- `features/empires-endgame/types.ts`: `EmpiresTechnologyDefinition`, map objects, army,
  minigame session/result.
- `features/empires-endgame/config.ts`: reference/live-effect validation and migration.
- `src/components/empires-endgame/TechTree.vue`: authored node positions; TD UI from P2.
- `game-config.json`: steel IDs `steel-laurel-spearhead` through `steel-arquebus`,
  `building-foundry`, `building-military-academy`, `relic-spirit-floor`, plus prerequisite
  `tech-generals`/`tech-foundry`.
- `qa.ts` and the P2 `battle-defense` scenario/action.

## Work items (in order)

1. Extend `td` config with typed battlefield modifiers rather than region-name branches.
   Author five layouts, region rule parameters, castle/ship variants, lane/build-spot
   references, and full grade choices. Keep migrated P2 custom configs valid when only the
   central field exists; enabled definitions must validate every reference.
2. Implement each region rule in the fixed-step sim and replay path. UI selects/displays
   the planned field and rejects illegal builds before emitting commands. Add castle,
   barricade, camp, post, naval, and Эдемская-катапульта mechanics only when verified; an
   undefined sketch item remains config-disabled with a ledger question.
3. Add `plan.mode: 'defense' | 'assault'`. Assault deploys player units against authored
   defenses/fort HP and returns the same discriminated TD result/settlement contract. Do
   not fork a second simulator; Phase 11 consumes this mode.
4. Port the full latest steel source into config. Reconcile it explicitly:
   - preserve and implement the 22 existing stable IDs listed below;
   - add missing authored branches/nodes from the latest file with deterministic stable
     IDs, exact Russian names, prerequisites, positions, and equipment payoffs;
   - if a missing node lacks executable numbers, add it with `deferredReason` and a ledger
     question rather than pretending the current five groups are the full tree.
5. Add typed persisted steel progress, for example:

   ```ts
   steelProgress: {
     forkedFromGroupIds: string[]
     priceMultipliers: Record<string, number>
     pendingFreeResearch: Array<{ technologyId: string; dueCon: number }>
   }
   ```

   Implement fork/source ×2 pricing, half-step generations, delayed-free “+” research,
   gear/method prerequisites, and |Элитное| gating in the existing research path. Normalize
   P2/v2 saves with empty state and test restore; do not bump the envelope for an additive
   field unless a real semantic migration requires it.
6. Replace dead steel flags with actual typed consumers/equipment unlocks. The existing
   payload set includes `polearmGeneration`, `voulgeProduction`, `halberdProduction`,
   `lanceProduction`, `mailGeneration`, `plateGeneration`,
   `cheapHelmetMassProduction`, `forgeMaxLevel`, `powderGeneration`,
   `shipCannonProduction`, `handBombardProduction`, and `arquebusProduction`. Each must
   alter equipment availability, production, forge capacity, or combat resolution and
   have a focused test; allowlisting alone is forbidden.
7. Implement the full authored military-building effects:
   - academy: `freeUnitsPerWarTechnology` and `academyDeliveryTurns` via deterministic
     typed pending deliveries; do not silently reinterpret it as the source of
     `militaryElite` unless the export says so;
   - foundry: `armyProductionDiscountPercent` and `instantUnitEveryTurns` in recruitment/
     production, with save-safe cadence;
   - relic: `minimumCombatSpirit` through P2's typed morale floor.
8. Add prerequisite closure `tech-generals` and `tech-foundry` with real consumers; P2
   already owns `tech-ironwork`. Preserve authored cross-phase gates: `steel-bucket-helm`
   may remain unavailable until P4 un-defers `reform-theocracy`. For `steel-lance`, find
   the authoritative `militaryElite` producer; if none exists, keep that node deferred and
   record the exception instead of inventing a producer.
9. Reconcile both faces of `card-hearts-ace` against the raw `карты`/`тд` sources. Its
   current authored shells enable/disable unit morale and active abilities. Un-defer a face
   only if this phase supplies the complete active-ability catalog, TD execution, morale
   interaction, cleanup, UI, and tests; P2's minimal morale scalar alone is insufficient.
10. Extend Builder/TechTree and TD surfaces only as needed to expose new data, requirements,
   assault mode, and equipment payoff. Keep config as the source of truth.

## Un-deferral list (exact current carriers)

Target these 22 existing steel IDs, subject to the no-fabrication exception above:

`steel-laurel-spearhead`, `steel-lancet-spearhead`, `steel-diamond-spearhead`,
`steel-cross-spearhead`, `steel-voulge`, `steel-halberd`, `steel-lance`,
`steel-butted-mail`, `steel-riveted-mail`, `steel-full-mail`, `steel-double-mail`,
`steel-steel-mail`, `steel-nasal-helm`, `steel-bucket-helm`, `steel-kettle-hat`,
`steel-iron-breastplate`, `steel-steel-cuirass`, `steel-water-hammer`,
`steel-heavy-water-hammer`, `steel-ship-cannon`, `steel-hand-bombard`, `steel-arquebus`.

Also un-defer only after full consumers/tests exist:

- `tech-generals`, `tech-foundry` (prerequisite closure);
- `building-foundry`, `building-military-academy`;
- `relic-spirit-floor`.
- `card-hearts-ace` normal and inverted are conditional whole-contract candidates for the
  TD unit-morale/active substrate. Keep either face deferred if its active semantics remain
  undefined or unimplemented.

Do **not** un-defer `event-northern-raids` here. Its authored choices modify
`loyaltyNorth`/`loyaltyWest`, not TD; Phase 4 owns the loyalty substrate and event. Do not
replace that event with an invented battle.

## Verification

- TD specs: deterministic case for all five region rules; central regression; north legal
  tower restrictions and ship variant; castle/barricade/camp mechanics that shipped;
  assault win/loss/abort; same replay across render frame chunking.
- Steel table tests: every live node has known prerequisites and an observable equipment/
  production payoff; fork ×2 state; half-step/delayed-free timing; elite and theocracy
  gates; config import/migration/backfill; P2 save normalization.
- Building/relic tests: academy delayed delivery (including restore), foundry discount and
  cadence, morale floor, plus all twelve current steel payload consumers.
- QA: add `battle-assault`; expand full-campaign autoplay across defense/assault; Cypress
  uses QA fast-resolve and asserts field/rule/research payoff rather than real-time play.
- Full standing gate:
  - `bash tools/test-empires-endgame.sh`
  - `pnpm --dir Web/VueClient build`
  - `bash tools/verify-docs.sh --changed`
- Inspect final status/diff, forbidden RNG/time APIs, and the remaining deferred list.

## Docs & ledger contract

- Update `docs/WEB-CLIENT.md` §12B for regional rules, naval/castle/assault behavior, the
  real steel branch inventory, pricing/generation/elite rules, and exact un-deferrals.
- Ledger every invented tower/equipment number, missing weapon level, steel price/timer,
  military-elite source decision, and any latest-export node kept deferred, keyed by JSON
  Pointer.
- New bugs receive finding IDs; do not mix unrelated fixes.
- Increment the patch component of `GameVersion` sequentially.
- Write `docs/commit-messages/<date>.md`; do not commit or push.

## Designer questions (pre-seeded)

- What are the authoritative stats behind the 4×4 grades and “208 builds” count?
- Which per-weapon damage levels remain unspecified in the steel source?
- Are fork ×2 pricing, delayed-free timing, and |Элитное| costs fully numeric anywhere?
- What creates `militaryElite`, and is it intentionally different from the academy's
  delayed free-unit effect?
- Does Эдемская катапульта ship as a normal regional tower, a quest reward, or stay deferred?
- What are the exact unit active abilities enabled/disabled by `card-hearts-ace`, and does
  its morale behavior belong to all TD modes or only particular troops/battles?
