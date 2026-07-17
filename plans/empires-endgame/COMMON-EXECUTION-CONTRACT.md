# Empire's Endgame — common execution contract (Phase 3 onward)

This contract is binding for every remaining phase prompt. Read it together with root
`AGENTS.md`, `plans/empires-endgame/README.md`, `plans/empires-endgame/COVERAGE-MATRIX.md`,
the selected phase, and the phase's named raw design sources. A phase may add stricter
requirements but may not weaken this contract.

Phases 0 and 1 are complete. Phase 2 is an active implementation change-set and its prompt
is now historical; do not reopen completed work merely because a later prompt describes a
better contract. Phase 3A owns a targeted reconciliation of any unfinished Phase-2 work.

## 1. Source and scope discipline

- The current `DiscordExports/Empires_Endgame/` channels and linked attachments are the
  source of truth. `DiscordExports/empire_prompt` defines the core loop. Main export beats
  `ZBS MAKING`, which is background only. If a required raw source is missing, stop before
  code edits and report the exact missing file/channel.
- A missing **number** may become a configurable default plus an append-only entry in
  `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md`. Missing **semantics** are blocking: keep the
  carrier or subfeature deferred, record the question, and test that it remains unavailable.
- `COVERAGE-MATRIX.md` assigns ownership. Do not pull later-phase work forward or treat a
  catalog mention as permission to invent it. If the raw source reveals an unlisted carrier,
  update the matrix in the same change before deciding whether it belongs in scope.
- Preserve Russian names, passive titles, and англицизмы exactly. Do not rewrite vague
  player-facing card/passive descriptions; exact mechanics belong in documentation.

## 2. Preflight and change-set boundary

- Run `git status --short`; preserve all pre-existing and unrelated work. Never reset,
  discard, stage, commit, or push.
- Verify prerequisite phases from actual types, config, migrations, tests, and UI—not from
  filenames or plan text. A partial prerequisite is either an explicitly assigned carryover
  or a blocker; do not silently merge unrelated phase scopes.
- Reconcile every candidate definition's complete current effects, prerequisites, choices,
  resolutions, and both card faces before removing a `deferredReason`.
- Each implementation phase/subphase is one independently reviewable change-set with one
  sequential `GameVersion` patch increment and one
  `docs/commit-messages/<date>[-N].md` proposal. A design gate that stops before shipped
  code/behavior changes records its audit outcome but does not fabricate a version bump.

## 3. Ownership and honest un-deferral

- Substrate, executable consumer, removal of `deferredReason`, config validation, UI, save
  compatibility, and focused tests ship together. Adding a flag to
  `EMPIRES_LIVE_FLAG_ALLOWLIST` without a real reader is never completion.
- A whole-definition marker may be removed only when every retained effect is executable.
  When one building/card/quest contains independently staged subfeatures, use a validated
  subfeature/face/choice-level deferral instead of creating a cross-phase ownership deadlock.
- Every phase ends with an exact remaining-deferred manifest for the carriers it inspected.
  A smaller honest live set is acceptable only when the phase's guaranteed substrate and
  minimum deliverable are complete and every blocker is explicit.

## 4. Determinism, replay identity, and storage bounds

- Simulation and authored random selection use serialized RNG streams only—never
  `Date.now`, `Math.random`, render cadence, locale order, or object iteration accidents.
- Every minigame result is replayable from `(plan, seed, commandLog)`. Commands use logical
  turn numbers or simulation tick indices, never wall-clock timestamps.
- A plan must either embed all simulation-relevant resolved definitions or carry an
  immutable rules/config digest and schema version. Reload/replay must reject a mismatch;
  importing or changing config may not silently alter an active session.
- UI rendering and headless QA call the same transition/replay path. Real-time minigames use
  a fixed tick; turn-based minigames use logged turns and no simulation clock.
- Every command log, chronicle, result log, offer history, intervention history, and other
  append-only collection needs a configured or documented retention/compaction policy that
  preserves required replay/audit data without unbounded localStorage growth.
- Background-tab/catch-up behavior must be explicit: cap accumulated work or pause cleanly;
  never process an unbounded wall-time backlog.

## 5. State, migration, and exact-once effects

- Use typed first-class state for entity/lifecycle data and consumed flags only for true
  empire-wide scalars. Do not create parallel sources of truth.
- Config always follows an explicit current-to-next migration chain before validation.
  Save fields receive additive normalization where possible; bump the envelope only for a
  semantic move. Never reuse a version number assumed by an older prompt.
- Every schema/envelope bump includes previous-version fixtures, non-mutating/idempotent
  migration tests, future-version rejection, and stored custom-config import coverage.
- Settlement, minigame resolution, quest choice, event choice, reward, and migration effects
  are idempotent. Reload/retry cannot charge, reward, spawn, or apply a consequence twice.

## 6. UI, accessibility, Builder, and QA

- Engine/config validation is authoritative. UI projections, disabled reasons, Builder
  import/export, and QA must not implement a second rules model.
- Every new interactive surface supports keyboard and pointer input, visible focus, useful
  labels, and deterministic disabled/error reasons. Canvas may render visuals but cannot be
  the only accessible interaction/state representation.
- Each real-time minigame gets at least one deterministic fake-clock component/input test
  proving a real keyboard/pointer action becomes a command and advances the production
  simulator. Cypress may use QA fast-resolve for end-to-end settlement, but fast-resolve is
  not a substitute for input-path coverage.
- Builder support is JSON-first unless a visual editor is itself required product scope.
  A polished graph/canvas editor is a follow-up, not a reason to block core gameplay.

## 7. Standing completion gate

Before handing off a phase/subphase:

1. Run all focused/new specs and `pnpm --dir Web/VueClient run test:empires`.
2. Run `bash tools/test-empires-endgame.sh`.
3. Run `pnpm --dir Web/VueClient build` (not the broken environment-wide `type-check`).
4. Run `bash tools/verify-docs.sh --changed`.
5. Update only affected `docs/WEB-CLIENT.md` §12B behavior/contracts; catalogue new bugs in
   `docs/AUDIT-FINDINGS.md` with the next free ID.
6. Append every invented value/semantic decision to the review ledger by JSON pointer or
   unique code reference, including the raw source and status.
7. Inspect final diff/status, confirm unrelated files are untouched, increment
   `GameVersion` once, and write the proposed commit message. Do not commit or push.
