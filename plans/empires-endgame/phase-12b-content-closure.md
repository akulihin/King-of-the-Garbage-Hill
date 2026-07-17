# Phase 12B — raw-source and deferred-content closure sweep

Read the common contract and coverage matrix. Execute after P6C, P9, P10, P11B, and the P12
Chess gate/outcome. This phase adds no new substrate. It closes content that is now fully
supported and turns every remaining item into an explicit blocked/review/out verdict.

## Guaranteed deliverable

Produce a complete raw-source→config→owner manifest and implement every existing or absent
definition whose semantics and substrate are now complete. No placeholder, orphaned raw
catalog item, silent absent system, or unexplained `deferredReason` remains outside the
coverage matrix and review ledger.

## Work items

1. Regenerate/read the actual current inventories: card faces by side, gifts/relics,
   resources, buildings/municipal buildings, units, technologies/reforms/steel, events,
   quests, minigames, governance definitions, map objects, and typed lifecycle configs.

2. Compare them against every current raw export channel and `COVERAGE-MATRIX.md`. Include
   content absent from config—not only current markers. Assign each row:
   `live`, `ready-now`, `blocked-semantic`, `blocked-substrate`, `review`, or `out`, with raw
   source, stable identity, owner, consumer, and test evidence.

3. Implement only `ready-now` rows using existing typed substrate. Each is an independent
   whole definition/face/choice contract with migration, UI, save, cleanup, and focused tests.
   This phase may not create a new combat/economy/quest/diplomacy/minigame lifecycle to force
   a row live.

4. Perform the final card-face reconciliation. Unique raw title + current config ID + side is
   mandatory; generic rank/suit placeholders never become authored faces by resemblance.
   Verify temporary cleanup, level scaling, activation zones, and every effect consumer.

5. Review remaining quest backlog, great houses/races, capital/map concepts, and missing
   building candidates. Port only complete graphs/definitions on existing substrate; otherwise
   preserve their review status and exact designer question.

6. Update `COVERAGE-MATRIX.md`, the append-only designer-review ledger, `docs/WEB-CLIENT.md`
   exact live/deferred inventory, config validation fixtures, and any generated audit output.
   No broad prose claim may say “full intent” while review/blocked items remain.

7. Add an automated coverage assertion or maintained manifest fixture that detects an
   unowned live/deferred config ID and an owned raw-catalog row with no disposition. Raw text
   extraction may remain a reviewed artifact; config ownership must be machine-checked.

## Verification additions

- Exact before/after carrier manifest; every removed marker has consumer tests; every retained
  marker is still rejected/unavailable; absent definitions have explicit disposition.
- Full config/reference/deferred validation, card-face cleanup, save migration for added
  definitions, QA smoke for each newly live surface, and common standing gate.

## Handoff

P13 receives the final live/deferred/review/out manifest. If this audit finds missing
substrate, do not expand P12B: add a future-tranche row with a designer verdict/owner.
