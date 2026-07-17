# Phase 7 — quest and dialogue engine

Read the common contract and coverage matrix. Execute after P5 and P4C, so army, loyalty,
population, disease, advisor/governance, and post-minigame hooks are real rather than
conditional dependencies hidden inside the prompt.

## Guaranteed deliverable

Ship a config-driven deterministic quest/dialogue graph, persistent progress and memory,
atomic choices, accessible overlay/journal, JSON-first Builder support, a faithful Палач
port, and complete event bridges for Golden Idol and Witch Apprenticeship where raw
semantics exist.

## Required raw sources

Read channel `квесты`, every linked `Palach*.html` file and adjacent message, `события`,
`карты`, `персонажи`, and quest cross-references in `общее`/regional channels. Preserve
authored Russian text exactly; do not execute/import the HTML implementation itself.

## Required config/runtime contract

Definitions need stable IDs, `name`, `journalDescription`, typed trigger, stages with names
and entry nodes, nodes with speaker/text/optional image, and choices with label,
requirements, costs, effects, target continuation, and node/stage/complete/fail transition.

Supported trigger kinds are explicitly audited and validated:

`conReached`, `flag`, `event`, `building`, `minigameResult`, and `manual`.

Quest definitions and independently unsupported choices may carry `deferredReason`. A
deferred choice remains visible only according to authored UI rules and can never be
selected; it preserves a portable quest graph without inventing a bespoke branch. If a
deferred branch makes every terminal path unavailable, the entire quest stays deferred.

Runtime state owns status, stage/node, typed quest memory, trigger-consumed identity, and
active mandatory dialogue. Choice application is one atomic commit.

## Work items

1. Extend config migration/validation for unique IDs, known triggers/dependencies/effects,
   valid entry nodes/gotos/stages, graph reachability, orphan-node rejection, at least one
   reachable terminal path, and explicitly allowed cycles. Preserve valid custom quest data.

2. Add first-class runtime state and migration. Removed/renamed/deferred definitions and
   active saves pointing at missing stages/nodes need an explicit compatibility policy—do
   not reset silently or charge/apply a choice twice.

3. Implement pure trigger/graph/availability helpers and engine actions. Each non-repeatable
   trigger fires exactly once; stable order resolves simultaneous triggers. Evaluate at
   authored boundaries including empire start and successful minigame resolution.

4. Recheck choice requirements/costs/targets inside the engine, use existing trusted
   payment/dependency funnels, apply typed effects once, update memory/status/transition,
   and block unrelated actions only while a mandatory dialogue requires resolution.

5. **Time rule:** viewing dialogue and choosing a branch consumes **no empire days by
   default**. Days change only when the authored choice contains an explicit validated time
   cost/effect. Ledger and ask if raw Палач material contradicts this default.

6. Port every canonical Палач branch, exact string, condition, target, and consequence.
   Record HTML-to-node traceability. If demos conflict, choose no winner silently. If a
   bespoke interaction cannot be expressed yet, use per-choice deferral and keep reachable
   authored alternatives honest.

7. Bridge `event-golden-idol` and `event-witch-apprenticeship` through typed quest starts/
   resolutions. Preserve immediate resource choices and implement every retained monument,
   warrior/target, disease, unity, apprentice/population, alchemy, and regional-loyalty
   consequence before removing an event marker.

8. Add accessible `DialogueOverlay` and `QuestJournal`: focus/keyboard, speaker/text/image,
   requirement/cost/target preview, safe mandatory dismissal behavior, active/completed/
   failed lists, and visible deferred/blocked state as authored. UI never advances state on
   render.

9. Builder is JSON-first with production validation and full round-trip. A draggable graph
   editor is an optional later tooling change-set, not part of this gameplay gate.

10. Add QA `advance-dialogue` policies by choice ID/first legal and `quest-dialogue`
    scenario. Inventory the remaining regional quest backlog in the coverage matrix; only
    complete authored graphs enter P12B.

## Exact event gate

- `event-golden-idol`: every live choice uses typed consumed quest/resolution payloads.
- `event-witch-apprenticeship`: send/refuse, population target/loss, knowledge/alchemy, and
  typed regional loyalty must execute.
- No other existing carrier is promised by this phase; newly added incomplete quests remain
  deferred with source-backed questions.

## Verification additions

- All trigger kinds and exact-once/repeatable behavior; simultaneous ordering; broken refs,
  orphan/reachability/cycle/terminal validation; per-choice deferral.
- Requirement/cost/time rules, targets, atomicity, every goto kind, save at node, graph-change
  compatibility, memory round-trip, complete/fail/restart policy.
- Scripted Палач paths and both event bridges; accessible overlay/journal; QA/Cypress; current-
  to-next config migration; bounded quest/chronicle history; common standing gate.

## Designer questions

- Canonical Палач demo/version and terminal versus later-hook endings?
- May multiple mandatory dialogues queue; priority and postponement?
- Does any dialogue itself spend time without an explicit choice time effect?
- Exact Golden Idol and Witch target/duration/unity/alchemy semantics?
- Which quest memory is journal-visible and which quests repeat?
- Which backlog quests are next priority for P12B?
