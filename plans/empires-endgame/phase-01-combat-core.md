# Phase 1 — Combat core (damage types, armor, counter matrix)

Use this file as the opening instruction for a fresh Codex task running 5.6 Sol. Execute
one complete change-set. Dependency: Phase 0 must already provide config migration v2 and
the disabled `combat` section; verify that from code/tests before editing.

## Executor rules (binding)

1. **Design source discipline**: read the phase's named raw export channels in `DiscordExports/Empires_Endgame/` before implementing. On conflict: main export > `ZBS MAKING` (outdated background); `empire_prompt` defines the core loop. Export missing from the environment → stop and tell the user; do not guess.
2. **Never fabricate silently**: a mechanic without export numbers → implement with a configurable default in `game-config.json` + append a ledger entry. A mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
3. **Un-deferral discipline**: substrate + un-deferral (delete `deferredReason`, add executable effects) + `EMPIRES_LIVE_FLAG_ALLOWLIST` additions or typed payloads + tests, all in ONE change-set. `validateLiveEffects` must keep rejecting flags nothing reads.
4. **Determinism**: no `Date.now`/`Math.random` in any simulation — only the serialized RNG streams (`features/empires-endgame/rng.ts`). Minigames replay from `(plan, seed, commandLog)`; mid-minigame real-time state is never serialized.
5. Player-facing card/passive *texts* stay deliberately vague (repo philosophy — never "fix" them; the designer writes new player wording). Exact mechanics are documented in `docs/WEB-CLIENT.md` §12B.
6. Git: do NOT commit or push. Write the commit message to `docs/commit-messages/<date>.md` (one file per change-set; `-2`, `-3` suffixes for further change-sets the same day).

## Codex preflight

- Read root `AGENTS.md` and `plans/empires-endgame/README.md`, run
  `git status --short`, and preserve all pre-existing changes.
- Verify Phase 0's config migration, disabled `combat` section, ledger, and test discovery.
  If any prerequisite is partial, stop and report the exact gap instead of merging phases.
- Make a task plan. Use targeted symbol reads; a read-only sub-agent may map export tables,
  but keep one owner for `types.ts`, `config.ts`, and `game-config.json`.

## Mission

Ship a pure, UI-independent `features/empires-endgame/combat/` module used later by TD,
expeditions, and events. It owns typed damage profiles, armor classes, the authored
counter matrix, automatic damage-type choice, passive suppression, and a config-driven
equipment catalog. This phase creates no battle loop and no player-visible combat.

## Design source

Read the `сталь` and `тд` channels in `DiscordExports/Empires_Endgame/`, with
`Steel-c748ae22139d6401.txt` as the latest steel source. Earlier steel drafts and
`ZBS MAKING` lose on conflict. Encode only rules verified there:

- damage types: ударное, дробящее, рубящее, режущее, колющее, with per-weapon levels;
- automatic priority: режущее against unarmored targets; колющее when its level exceeds
  the target's overall armor level; otherwise the best applicable authored type;
- counter matrix: Ударные > Кольчуга > Режущие; Рубящее > Бригантина > Ударное;
  Тканевые/поддоспешник > Ударные + Режущие; Эсток/Ледоруб counter everything; shields
  disable arrows; axes lower shields; a counter disables weapon passives; mixed profiles
  are not countered; two-type profiles such as Лютеранский молот can be countered through
  either side.

The export has only partial numeric weapon data (for example Клевец 6/4/3). Missing
numbers may become config defaults only with ledger entries. Undefined semantics remain
disabled/deferred. If the named files are missing, stop and tell the user before editing;
do not implement from this compression alone.

## Repo anchors (read before editing)

- `Web/VueClient/src/features/empires-endgame/types.ts`: Phase-0 `combat` config types and
  `EmpiresEndgameConfig`.
- `Web/VueClient/src/features/empires-endgame/config.ts`: migration-before-validation,
  dependency reference validation, live-effect validation.
- `Web/VueClient/src/features/empires-endgame/rng.ts`: determinism boundary; the pure
  damage resolver should not need RNG.
- `Web/VueClient/public/empires-endgame/game-config.json`: bundled v2 `combat` section and
  existing steel technology IDs/prerequisites.
- `Web/VueClient/src/components/empires-endgame/BuilderDrawer.vue`: JSON editor surface;
  do not build a large dedicated combat editor in this phase.
- `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md` and `docs/WEB-CLIENT.md` §12B.

## Work items (in order)

1. Add `features/empires-endgame/combat/types.ts` with stable config/runtime types:

   ```ts
   type CombatDamageTypeId = string
   interface CombatWeaponProfile {
     damageLevels: Partial<Record<CombatDamageTypeId, number>>
     tags: string[]
     mixed?: boolean
     twoTyped?: boolean
     passiveIds?: string[]
   }
   interface CombatArmorProfile { classId: string; level: number; tags?: string[] }
   interface CombatEquipmentDefinition {
     id: string
     name: string
     kind: 'weapon' | 'armor' | 'shield'
     profile: CombatWeaponProfile | CombatArmorProfile
     technologyId?: string
     deferredReason?: string
   }
   ```

   Define explicit counter-rule unions rather than magic flag strings.
2. Add `combat/damage.ts` with pure `autoSelectDamageType`, `isCountered`, and
   `resolveDamage`. Return an inspectable breakdown containing chosen type, raw damage,
   armor/counter rule, whether passives were disabled, and final damage. Do not mutate
   profiles and do not introduce a second rules path for later QA.
3. Populate bundled `combat` config with the verified damage types, armor classes,
   counter rules, and only the equipment entries supported by the raw export. Enable the
   section for the bundled game. Every invented level/multiplier goes into the ledger by
   exact JSON Pointer.
4. Extend config validation: unique IDs; finite/non-negative levels; known references;
   counter endpoints exist; equipment `technologyId` references a real technology;
   contradictory `mixed`/`twoTyped` shapes fail. A Phase-0 migrated custom config with
   `combat.enabled === false` and empty catalogs remains valid; an enabled incomplete
   section must fail. Add normalization/backfill coverage so stored v1/v2 custom configs
   do not become unimportable merely because Phase 1 populated the bundled section.
5. Expose the config through the existing Builder JSON tab (read-only or normal JSON
   editing is enough). Validate round-trip import/export; do not hard-code gameplay data
   in the component.

## Un-deferral list

None. All steel technologies, military buildings, units, and combat-dependent content
remain deferred until a real consumer exists. Assert that this phase does not reduce the
deferred carrier set.

## Verification

- `combat/damage.spec.ts`: table-driven row for every raw counter rule plus naked-target
  priority, piercing threshold equality/boundaries, mixed immunity, two-type dual
  counters, shield/arrows, axe/shield, and passive suppression.
- Purity/determinism: deep-freeze inputs; same inputs twice produce deep-equal breakdowns;
  no RNG state changes.
- Config specs: disabled-empty compatibility; enabled catalog validation; bad references;
  v1/v2 custom config migration/backfill; Builder import/export round-trip.
- Wire the new spec into `test:empires` and prove it runs.
- Run the complete standing gate:
  - `bash tools/test-empires-endgame.sh`
  - `pnpm --dir Web/VueClient build`
  - `bash tools/verify-docs.sh --changed`
- Inspect final diff/status and confirm no gameplay content was un-deferred.

## Docs & ledger contract

- Update `docs/WEB-CLIENT.md` §12B with the pure/config-driven combat contract and state
  clearly that no battle surface is live yet.
- Append every invented weapon/armor/counter number to
  `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md` using exact JSON Pointers.
- New bugs get the next free finding ID; do not mix unrelated fixes into this phase.
- Increment `GameVersion`'s patch component sequentially in
  `King-of-the-Garbage-Hill/Game/Classes/GameClass.cs` after implementation.
- Write `docs/commit-messages/<date>.md`; do not commit or push.

## Designer questions (pre-seeded)

- Which weapons beyond Клевец have authoritative per-type damage levels?
- Is a counter purely a rule/passive disable, a numeric damage multiplier, or both?
- What exactly distinguishes “mixed” from “two-type” weapons in resolution when only one
  constituent type is countered?
