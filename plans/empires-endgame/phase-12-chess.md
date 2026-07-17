# Phase 12 — Chess design gate and conditional implementation

Read the common contract and coverage matrix. Execute after P9 so current standard/mystic
card schemas and ordering are known. Chess remains disabled unless the raw design passes the
readiness gate below.

## First deliverable: executable rules table

Read channel `шахматы`, its linked board sketch/image, and every cross-reference in `карты`,
`персонажи`, and `общее`. Produce a table covering:

- board size/layout/setup and complete player/enemy roster;
- exact current card definition/instance mapping and owner/controller;
- movement, blocking, capture, turn/cadence, special moves, and randomness;
- Антон identity and shared-control “once every two turns” semantics;
- enemy-without-king victory, player loss, draw/repetition/move cap;
- entry trigger, reward, failure, abort/reload, and campaign card consequences;
- card level/inversion/death/hand/deck/discard eligibility;
- opponent policy information and hidden-state rules.

Known leads—Казна/Чистые улицы as rooks, family as pieces, Антон as a shared knight, enemy
without a king—do not fill the missing cells by familiarity with standard chess.

## Hard readiness gate

- If any rule required for legal moves, termination, roster construction, or settlement is
  semantically undefined, update the ledger/coverage matrix, keep `chess.enabled: false`, and
  stop before engine/UI implementation to ask the designer. Do not invent “eliminate all” or
  “survive N turns.” A disabled placeholder engine is not useful completion.
- If all mandatory semantics are defined and only tunable numbers are missing, continue with
  the conditional implementation below using config+ledger defaults.

The design-gate-only outcome is an audit, not a finished implementation phase; it does not
require a `GameVersion` bump when no shipped code/docs behavior changes.

## Conditional implementation

1. Add a disabled-by-default typed Chess config and current-to-next migration. Validate board
   bounds/setup, mappings, move/turn/victory rules, controller/cadence, entry/reward/abort,
   opponent policy, and positive caps. An incomplete imported ruleset cannot be enabled.

2. Build each session plan from **eligible current campaign card instances**, not static
   definition mappings alone. Snapshot stable instance ID, definition ID, current owner/zone,
   level, inversion, alive/eligible state, mapped role, and resolved rules identity. Recheck
   stale instances at begin/settlement; never dispatch on Russian display text.

3. Create a pure turn-based module—no fixed simulation clock—with typed state/commands,
   legal move generation, immutable apply, replay `(plan, seed, moveLog)`, stable ordering,
   and scripted policies. RNG is inert unless an authored rule/policy needs the serialized
   stream. Use turn indices and the common rules/config identity.

4. Implement only sourced move/capture/check/promotion/special/victory behavior. The enemy's
   missing king uses the authored alternative, not reinterpreted checkmate. Implement Антон
   only after exact identity/control/cooldown semantics are known.

5. Integrate begin/resolve/abort/reload through the shared envelope. Do not serialize a
   second board outside plan+move log. Settlement applies authored typed rewards/card
   consequences once; serving as a piece does not itself kill/invert a campaign card.

6. Build accessible `ChessMinigame` with keyboard/pointer board, selected/legal cells,
   controller/turn/cadence/outcome, and abort confirmation. Reuse card art for pieces when
   the mapping needs a concrete visual and the existing asset is available; provide text
   labels regardless. Bundled off means no player entry point; QA may enable a complete
   test-only fixture and fast resolve.

7. Add JSON Builder support and prevent enabling/saving incomplete rules. No polished custom
   board editor is required.

## Verification additions when implementation proceeds

- Table-driven legal moves for every used role; ownership/controller/cadence; stale current-
  instance plan rejection; level/inversion behavior; win/loss/draw/repetition/move cap.
- Replay/policy determinism, envelope exact-once/reload/abort, rules mismatch, bounded log,
  campaign card consequence, bundled toggle isolation, QA/Cypress, migration, and common gate.

## Designer questions

- Every unresolved cell in the executable rules table, especially board/setup and termination?
- Which current instances/zones count as “alive/important”; what if a mapped card is absent?
- Exact Казна/Чистые улицы/family/Антон IDs and level/inversion effects?
- Entry/reward/loss/abort/reload/card-consequence semantics?
