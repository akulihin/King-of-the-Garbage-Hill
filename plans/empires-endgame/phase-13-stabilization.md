# Phase 13 — full-chain compatibility and stabilization

Read the common contract, coverage matrix, every completed phase handoff, and the P12B final
manifest. This is the last implementation change-set. Add no new feature/content scope;
fix integration defects found by the mandatory gates and catalogue them under the repository
bug contract.

## Guaranteed deliverable

Prove the earliest supported config/save can reach the final version, every minigame/session
survives restore and rules identity, a representative campaign crosses all live systems, UI
input reaches production simulators, and localStorage/performance/history behavior is bounded.

## Compatibility matrix

1. Restore/import the original pre-P0 config v1 and campaign/envelope v1 directly through
   every migration to latest. Compare with sequential step-by-step migration and assert the
   same normalized result without input mutation.

2. Restore representative custom configs from each schema generation, including disabled
   empty future sections, partially populated catalogs, and valid custom values. Future
   versions and dangling references fail specifically.

3. Restore active sessions for every implemented kind: TD defense/assault, Tavern, Alchemy,
   Inventory, and Chess only if enabled/implemented. Verify immutable plan/rules identity,
   `attempt + 1`, abort, stale config, and exact-once settlement.

4. Restore representative runtime lifecycle states: rebellion, epidemic, loan/insurance/
   offer/Fair, active quest dialogue, God intervention/line, mystic/Queen tick, expedition,
   governance/advisor/perst, and pending target/event resolution.

## Integrated campaign and UI gates

5. Add one deterministic long QA campaign/scenario crossing Durak, empire settlement,
   defense/assault, loyalty/rebellion/recovery, epidemic/famine ordering, domestic/external
   economy, quest/dialogue, God presence, Tavern/mystics, Alchemy explosion, expedition/
   packing, and final save/reload. Conditional/disabled Chess is asserted accordingly.

6. For each interactive minigame, retain a deterministic fake-clock keyboard/pointer input
   test that emits production commands and advances the real simulator. Add accessible state/
   focus/label smoke for non-canvas alternatives. Cypress validates major transitions and
   settlement without manually playing long real-time sessions.

7. Set and enforce reasonable budgets for fixed-step catch-up, headless tick/action caps,
   replay duration, long-campaign save size, and component memory/list size. Test background
   pause/cap behavior. Do not optimize blindly; record measured baselines and thresholds.

8. Exercise retention/compaction for command/result logs, chronicle, offers, God dialogue/
   interventions, quests, and expedition histories. Required replay identity remains while
   obsolete detail cannot grow localStorage without bound.

## Final closure gates

9. Run the config ownership/deferred coverage assertion and compare with P12B manifest. Every
   live carrier has a reader/test; every deferred/review/out item is explicit. No generated/
   hand-maintained count drifts silently.

10. Run focused and complete frontend tests, all Cypress Empires specs, production build,
    changed-doc verifier, and deterministic QA across a defined seed matrix. Repeat critical
    digests to prove reproducibility.

11. Inspect documentation against shipped behavior and final schema, not plan intent. Update
    only stale contracts/inventories. Record any remaining designer questions as future scope,
    not claims of completion.

12. Fix in-scope integration/migration/storage/accessibility defects discovered by these
    gates, add findings as required, increment `GameVersion` once for the completed
    stabilization implementation, and write the final commit-message proposal. Do not commit.

## Completion definition

The program is complete when all guaranteed/live scope is implemented and verified, while
every unresolved raw idea is visibly classified as blocked, review, or out. Completion does
not require fabricating unresolved semantics or deleting honest deferrals.

## Execution outcome — 2026-07-21

Status: **Complete/Conditional.** The guaranteed live scope is stabilized; conditional means
only that P12's Chess semantics and P12B's explicitly blocked/review/out content remain
outside the executable program. No content carrier was added or un-deferred.

- The authentic pre-P0 config fixture and representative schemas v1–v17 cover direct versus
  stepwise migration, input immutability, idempotence, disabled/partial custom sections,
  dangling references and future-v18 rejection. A second authentic fixture records the same
  pre-P0 campaign at production save checkpoints v1–v15; direct v1 restore and every
  historical checkpoint reach the same v16 state without mutating their inputs. Config
  schema remains 17.
- Campaign/save advances to schema 16 for stabilization state only. Legacy envelope import
  now preserves its matching source state version until engine normalization. Save v1–v16
  and active TD defense/assault, Tavern, Alchemy and Inventory sessions retain immutable
  plan/rules identity, restore with `attempt + 1`, reject stale rules and settle/abort once.
  Current saves cannot erase required quest, quest-runtime, expedition or loyalty roots, and
  a current active session must retain a well-formed origin/context consistent with its
  kind, plan and expedition lifecycle.
- Canonical minigame sequences plus a persisted settlement watermark keep exact-once
  identity after readable result detail is compacted. Legacy non-canonical IDs have a
  bounded compatibility tail. Epidemic and consumed-battle-loss histories gained typed
  compaction; expedition complaints/results retain attempt watermarks after their 64-entry
  readable identity tails compact. These join the already bounded chronicle, economy, God,
  quest, mystic and expedition histories.
- Shipped ceilings are enforced for ticks, commands, catch-up, logical replay duration,
  QA actions, result/history retention, board/plan/offer sizes, settled minigames and the
  512 KiB serialized save boundary. Bundled TD/Alchemy/Inventory values are recorded below
  those ceilings in `docs/WEB-CLIENT.md`. Autosave and manual export use the same bounded
  serializer and expose a persistent `role="alert"` when that boundary blocks either path.
  Startup validates candidate saves through the production engine newest-to-oldest, so a
  structurally readable but semantically invalid newest key cannot hide an older valid save.
- Alchemy and Inventory ignore game shortcuts from native interactive controls. Their shared
  abort dialog pauses simulation, blocks input, exposes labelled modal semantics, starts on
  the safe action, traps focus and restores focus/frame origin. TD, Alchemy and Inventory
  fake-clock tests cover production keyboard/pointer commands, semantic alternatives and
  background-tab backlog discard.
- QA includes an active Inventory fixture, the visible result count and settlement-watermark
  diagnostics. One continuously accumulated engine per `phase13-alpha`, `phase13-beta` and
  `phase13-gamma` run records 13 ordered checkpoints and six minigame settlements; beta
  repeats to the exact final snapshot, digest and result. The campaign stages only Maria's
  still-deferred prerequisite, then performs the live 3→7→Ace card sequence through public
  actions and retains the resulting Пиковая Дама spawn/history. Final-day minigame settlement
  records its exact-once watermark before the next-con wave is scheduled. The hard action
  cap is 10,000. The older traced-autoplay seed matrix and `qa-repeatable` trace check remain
  as complementary coverage. TD Cypress verifies a visible zero-to-one settlement result
  instead of only checking that the board disappears.
- The P12B closure assertion remains the final content boundary: 1,112 config carriers, 355
  raw semantic identities, 33 sources, the 1,149-message spine and zero `ready-now` rows.
  Chess still has no config/runtime/UI/QA/Builder surface until `P12-02`–`P12-09` are decided.
- Final command outcomes and measured timing/save-size observations are reported in the
  change-set handoff after the full unit/browser gate, production build, documentation
  verifier, backend build, passive audit and simulation run; this plan records the stable
  acceptance contract rather than machine-specific timing claims.
