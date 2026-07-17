# Phase 11B — Tetris-inventory expedition packing

Read the common contract and coverage matrix. Execute after P11A. This change-set adds only
the deterministic falling-cart packing minigame and its integration with the existing
expedition provision plan.

## Guaranteed deliverable

Ship fixed-step cart packing, typed item-instance ownership, rotate/place controls,
deterministic scoring/efficiency, optional/required entry semantics, accessible UI, QA replay,
and exact transition from packing to P11A launch/assault.

## Required raw sources

Read `чо-добавить`, `экспедиции`, linked inventory/cart images, and every message defining
falling items, rotation, packing, provisions/equipment, failure, time, and skipped packing.
The compressed source establishes only a cart at the bottom with items falling in real time;
board/shapes/gravity/scoring require source or config+ledger.

## Work items

1. Add typed inventory config, plan, item instances/shapes/weights, commands, runtime, result,
   scoring, efficiency, failure/abort, skip policy, and expedition origin. Validate all item,
   resource/equipment, grid, timing, rotation, cap, and expedition references.

2. Create one fixed-step engine/replay using P3A rules identity and logical tick commands.
   Spawn/gravity/collision/rotation/placement/terminal rules are deterministic with stable
   ties, tick cap, background policy, and bounded logs.

3. Define trusted ownership: the plan snapshots eligible inventory instance IDs; resolution
   returns packed IDs/placements and expedition-specific provision outcome. Only packed items
   leave/are consumed by the expedition at canonical settlement; unpacked items remain in the
   origin inventory. Failed/aborted/stale results cannot delete or duplicate items.

4. Decide from raw design whether packing is optional. If skipped, use an authored direct-
   provision fallback/penalty from P11A. A player cannot replay packing for a better result
   without the shared attempt/abort consequence.

5. Extend the envelope with inventory plan/result arms. Begin from a P11A expedition in
   `packing`, replay/validate once, write the provision plan, then transition to launch/
   assault. Reload restarts plan/seed; do not serialize falling render/runtime state.

6. Build accessible `InventoryPacking` UI with cart/grid, next/current item, keyboard/pointer
   rotate/place, packed/unpacked summary, timing/score, skip/abort confirmation, and QA fast
   resolve. Add real fake-clock input coverage.

7. Add scripted policies and full QA round trip: prepare → pack/skip → launch → assault →
   settlement. P11A time/provision/complaint rules remain authoritative.

## Verification additions

- Spawn/gravity/rotation/collision/bounds/placement/scoring/termination across seeds and
  frame cadences; same plan/seed/log digest.
- Packed consumed once; unpacked retained; stale/duplicate/abort/failure/skip/reload; no item
  loss or duplication; provision outcome applied once.
- Rules mismatch, active-config safety, bounded log, background catch-up, real input smoke,
  QA/Cypress full chain, migration, and common gate.

## Designer questions

- Board/cart dimensions, item shapes/weights, gravity, controls, scoring, cap, and failure?
- Is packing optional; what direct fallback/penalty applies when skipped?
- Do packed items represent provisions, equipment, both, and when are they consumed/returned?
- How does packing score modify P11A duration/death risk without overwriting empire state?
