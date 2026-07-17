# Phase 10 — Tetris-alchemy

Read the common contract and coverage matrix. Execute after P3A's hardened minigame
envelope and P5's typed epidemic lifecycle.

## Ownership handoff

P5 may hand off a passive-live Alchemy building with deferred `alchemyMinigame` capability,
or a still-deferred whole building whose epidemic/combo substrate is complete. Both are
valid. P10 owns the active lab/minigame capability and final whole-building closure. Stop
only when the P5 epidemic funnel or building origin is missing—not merely because a marker
remains.

## Guaranteed deliverable

Ship deterministic fixed-tick Assembly (`Сбор`) and Disassembly (`Разбор`), reagent
commands, acceleration/explosion, recipes/rewards, typed explosion→epidemic settlement at
the laboratory city, accessible UI, QA replay, and final Alchemy building closure where all
retained effects are complete.

## Required raw sources

Read channel `тетрис-алхимия`, linked images, science/Alchemy messages in `Технологии_*`,
`здания`, `карты`, and epidemic/mutant references. Verify raw source directly:

- four-sided pieces approach a central construction;
- nearest eligible piece control and no backward movement;
- sourced inward speed boost (lead ×3);
- complementary Disassembly;
- remove-color, add-gray, and reset-acceleration reagents;
- arithmetic acceleration and possible 400% explosion threshold;
- epidemic/mutants near the lab and poison crafting with walls.

Do not treat compressed “400%” or undefined Disassembly/recipe semantics as automatically
authoritative.

## Work items

1. Add typed Alchemy config, plan, command, runtime, result, recipes, reagents, reward/
   failure/abort, science prerequisites, and explosion consequence. Validate all board,
   timing, probability, building/city, technology, recipe, epidemic, and effect references.

2. Create one pure fixed-step engine/replay. Collision, four-side spawn, nearest selection,
   tie order, control transfer, movement, lock, reagent application, acceleration,
   completion/failure, and explosion advance only by logical ticks/commands. Use P3A
   rules identity, active-config safety, log bounds, and background policy.

3. Extend the shared envelope with Alchemy plan/result arms. Reload restarts from immutable
   plan/seed with `attempt + 1`; abort uses authored penalty. Runtime board/timers/render
   interpolation are never campaign state.

4. Validate/replay submitted results in the campaign engine and settle once. Success applies
   typed recipe rewards; abort/failure applies configured consequence. Explosion emits a
   trusted `{originCityId, epidemicDefinitionId, severity, source}` request through P5's one
   `startEpidemic` funnel—never direct array/UI mutation.

5. **Explosion retry lock:** if the raw source establishes a disabled/damaged lab consequence,
   lock the originating laboratory interaction for the rest of the con using canonical
   building interaction state. If not authored, make it a configurable ledgered anti-retry
   penalty rather than an invisible assumption. Restore cannot clear or duplicate it.

6. Add persisted campaign summaries only for authored recipe/crafted/explosion outcomes.
   Poison/wall recipes and science unlocks become live only with exact inputs, outputs,
   storage/consumer, and reward semantics; otherwise they retain subfeature deferral.

7. Build accessible `AlchemyBoard` with Assembly/Disassembly state, keyboard/pointer command
   path, reagent controls, acceleration/explosion warning, pause/abort confirmation, and QA
   fast resolve. Add a real fake-clock input test plus Cypress settlement; explosion fixture
   must visibly produce the P5 map/city epidemic state.

8. Clear `alchemyMinigame` capability and final whole-building marker only after all retained
   P5+P10 effects execute. Do not un-defer unrelated cards/gifts/technologies.

## Verification additions

- Four-side spawn, nearest/ties/control, no-backward rule, collision/lock, sourced speed,
  acceleration boundaries, each reagent, Assembly/Disassembly completion, deterministic
  replay under frame cadences.
- Success/abort/failure/explosion exact-once settlement, stale/inaccessible origin rejection,
  typed epidemic and lab-lock/anti-retry behavior, restore/rules mismatch.
- Three seeds × three policies under tick cap, real input smoke, QA/Cypress visible epidemic,
  migration, final Alchemy contract, and common gate.

## Designer questions

- Exact board/pieces/colors/spawn/lock/success rules for both modes?
- Meaning/ties for “nearest,” exact ×3 behavior, and explosion boundary (at/above 400%)?
- Reagent acquisition/charges/scope/order?
- Epidemic definition/severity and whether mutants are TD/event/state?
- Does explosion lock/damage the lab; what is abort/reload/day cost?
- Launch recipes/rewards, science IDs, poison walls, and storage/consumers?
