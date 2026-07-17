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
