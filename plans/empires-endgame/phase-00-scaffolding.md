# Phase 0 — Scaffolding + review ledger (no behavior change)

Use this file as the opening instruction for a fresh Codex task running 5.6 Sol. Execute
the phase as one complete change-set; do not merely describe the implementation. This
phase has no gameplay behavior change, but every migration, compatibility test, document,
and repository bookkeeping item below is part of the deliverable.

## Executor rules (binding)

1. **Design source discipline**: read the phase's named raw export channels in `DiscordExports/Empires_Endgame/` before implementing. On conflict: main export > `ZBS MAKING` (outdated background); `empire_prompt` defines the core loop. Export missing from the environment → stop and tell the user; do not guess.
2. **Never fabricate silently**: a mechanic without export numbers → implement with a configurable default in `game-config.json` + append a ledger entry. A mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
3. **Un-deferral discipline**: substrate + un-deferral (delete `deferredReason`, add executable effects) + `EMPIRES_LIVE_FLAG_ALLOWLIST` additions or typed payloads + tests, all in ONE change-set. `validateLiveEffects` must keep rejecting flags nothing reads.
4. **Determinism**: no `Date.now`/`Math.random` in any simulation — only the serialized RNG streams (`features/empires-endgame/rng.ts`). Minigames replay from `(plan, seed, commandLog)`; mid-minigame real-time state is never serialized.
5. Player-facing card/passive *texts* stay deliberately vague (repo philosophy — never "fix" them; the designer writes new player wording). Exact mechanics are documented in `docs/WEB-CLIENT.md` §12B.
6. Git: do NOT commit or push. Write the commit message to `docs/commit-messages/<date>.md` (one file per change-set; `-2`, `-3` suffixes for further change-sets the same day).

## Codex preflight

- Read root `AGENTS.md` and `plans/empires-endgame/README.md` completely.
- Run `git status --short`; preserve every pre-existing/unrelated change. Never reset or
  discard the user's worktree.
- Establish a task plan and inspect the symbols below before editing. Use targeted `rg`,
  not a broad load of the game codebase.
- This is the root phase, so there is no prior phase to trust. Record the current config,
  snapshot, package-script, and docs behavior as the compatibility baseline.

## Mission

Prepare Empire's Endgame for the twelve content phases with a real config-migration chain,
forward-compatible additive snapshot normalization, disabled/empty config sections for
future systems, an append-only designer-review ledger, and test discovery that cannot omit
new feature specs. Existing campaigns and the bundled game must behave identically after
the change.

## Design source

Before editing, read `DiscordExports/empire_prompt` and the `общее` channel under
`DiscordExports/Empires_Endgame/`. They establish the alternating Durak/empire loop, the
approximately two-month con clock, and the client-side/local-save boundary. This phase
does not implement those mechanics; it only reserves typed/configured homes for later
phases. Also reconcile the raw wording about con length with the current bundled
`durak.boutsPerCon: 3` and `docs/WEB-CLIENT.md` §12B's “Ten bouts” statement—do not choose a
new number in this phase. If either source is missing, stop and tell the user before edits;
do not use this prompt's compression as a replacement.

## Repo anchors (read before editing; re-locate by symbol if lines drift)

- `Web/VueClient/src/features/empires-endgame/config.ts`: hard config-schema rejection near
  `config.ts:242`, `validateDeferredReasons`, `EMPIRES_LIVE_FLAG_ALLOWLIST`,
  `validateLiveEffects`, load/import/clone paths.
- `Web/VueClient/src/features/last-chances/config.ts:55`:
  `migrateLastChancesConfig`, the migration-before-validation pattern to mirror.
- `Web/VueClient/src/features/empires-endgame/types.ts`: `EmpiresEndgameConfig`,
  `EmpiresCampaignState` near `:513`, `EmpiresSnapshotEnvelope` near `:533`.
- `Web/VueClient/src/features/empires-endgame/engine.ts:1002`:
  `validateAndCloneSnapshot`, including current field-normalization repairs.
- `Web/VueClient/src/features/empires-endgame/persistence.ts:8-12`: envelope validation and
  localStorage boundary.
- `Web/VueClient/src/features/empires-endgame/config.spec.ts`, `engine.spec.ts`,
  `qa.spec.ts`: compatibility fixture/test style.
- `Web/VueClient/package.json`: `test:empires` currently enumerates three files;
  `test:empires:e2e` currently pins one Cypress file.
- `docs/WEB-CLIENT.md` §12B and
  `King-of-the-Garbage-Hill/Game/Classes/GameClass.cs` `GameVersion`.

## Work items (in order)

1. Add `migrateEmpiresConfig(raw: unknown)` and invoke it before
   `validateEmpiresConfig`/`validateEmpiresEndgameConfig` on bundled load, stored config,
   JSON import, and clone boundaries. Implement an explicit `1 → 2` step; clone inputs,
   make current-version migration idempotent, and reject unknown future versions.
2. Change the current config type and bundled `game-config.json` to `schemaVersion: 2`.
   The v1→v2 step injects disabled, additive homes without enabling behavior:
   `combat`, `td`, `god`, `quests`, `empire.seasons`, and `empire.loyalty`. Use stable
   shapes such as `{ enabled: false, ...empty catalogs }`; later phases may populate them.
   Validators must distinguish “disabled and empty” from “enabled but incomplete.”
3. Extend `validateAndCloneSnapshot` with additive defaults, keeping legacy snapshot and
   envelope schema 1 in this phase because no semantic state move occurs:

   ```ts
   minigame: null
   minigameResultLog: []
   army: { equipmentStock: {}, pendingLoyaltyDeltas: [], morale: 0, veterans: {} }
   external: { allianceThreat: 0, pendingOffers: [] }
   epidemics: []
   quests: {}
   city.loyalty: 0
   durak.godInterventions: 0
   ```

   Put these fields in the proper typed state owners; do not use a catch-all bag or
   serialize future real-time minigame state. Fresh state and restored v1 state must
   normalize to the same shape.
4. Create `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md` as append-only. Define a row/template
   keyed by JSON Pointer with phase, raw source, chosen default, rationale, status, and
   designer verdict. Seed all fourteen questions from README §H, including the
   `boutsPerCon` mismatch. Do not “resolve” any seed without designer evidence.
5. Change `test:empires` so Vitest discovers every
   `src/features/empires-endgame/**/*.spec.ts` file (a directory target is acceptable if
   verified). Keep Cypress wiring explicit or switch it to a verified suite glob; in
   either case document the rule so later `.cy.ts` files cannot be skipped.
6. Keep the change behavior-neutral: no content un-deferrals, no new flags in the live
   allowlist, no enabled future system, and no player-facing rewrite.

## Un-deferral list

None. Assert in a focused test or script that the set/count of existing
`deferredReason` carriers is unchanged by this phase. Do not remove or add gameplay
deferral markers as a side effect of migration.

## Verification

- Config specs: v1 fixture migrates to v2; v2 migration is idempotent; input is not
  mutated; a future version is rejected; disabled empty sections validate; enabled
  incomplete sections fail with specific messages; JSON import follows the same path.
- Save specs: restore a pre-phase v1 campaign/envelope with every new field absent and
  assert exact defaults; save and restore it again; confirm current state digest/gameplay
  decisions are unchanged.
- Test-wiring check: add or temporarily target a nested sample spec in the test itself so
  discovery behavior is proven rather than assumed.
- Run the full standing gate:
  - `bash tools/test-empires-endgame.sh`
  - `pnpm --dir Web/VueClient build` (never use the broken environment-wide type-check)
  - `bash tools/verify-docs.sh --changed`
- Inspect the final diff and `git status --short`; ensure no unrelated file was altered.

## Docs & ledger contract

- Update only the affected paragraphs in `docs/WEB-CLIENT.md` §12B: config migration,
  additive legacy-save normalization, and test-discovery contract. Do not describe future
  systems as implemented.
- Create and seed `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md` as specified above.
- New bugs discovered during the work get the next free ID in
  `docs/AUDIT-FINDINGS.md`; do not silently fold unrelated fixes into this phase.
- Increment the patch component of `GameVersion` sequentially in
  `King-of-the-Garbage-Hill/Game/Classes/GameClass.cs` after the implementation is
  finished.
- Write the proposed commit message to `docs/commit-messages/<date>.md`; do not commit.

## Designer questions (pre-seeded)

- Does one con contain the bundled three bouts, the documented ten bouts, or another
  authored value? Keep all three sources visible in the ledger until answered.
- Are empty disabled future sections acceptable in exported custom configs, or should the
  Builder hide them until their owning phase enables them?
