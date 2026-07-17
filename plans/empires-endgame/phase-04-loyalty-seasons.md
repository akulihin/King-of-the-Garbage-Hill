# Phase 4A — loyalty, reputation, rebellion, and chronicle

Read the common contract and coverage matrix. Execute one change-set after P3A. P3B may
run in parallel, but P4B waits for both.

## Guaranteed deliverable

Ship first-class city/region loyalty, consumed reputation, reversible rebellion,
population-class building gates, a deterministic chronicle, and the P3A battle-loss hook.
This phase owns the political `event-northern-raids`. Seasons, technology light/dark sides,
crime, and political reforms belong to P4B.

## Required raw sources

Read channels `застройка`, `общее`, `дома`, `регионы`, `карты`, `события`, and relevant
`Технологии_*` messages. Read P3A's canonical battle result/settlement types before mapping
losses. Main export beats `ZBS MAKING`.

Verify these anchors rather than treating them as complete formulas:

- city loyalty `−9..+9` plus a regional modifier;
- workforce anchors `−9 → /19`, `0 → /9`, `+9 → /1`;
- negative loyalty may stop buildings; мещане loyalty gates Smithy behavior where authored;
- sustained negative region loyalty can cause reversible rebellion, not destruction;
- at least 10% attributed military losses cause a loyalty penalty;
- reputation `−9..+9` gates later trade/unions;
- Capital Forum affects loyalty in both directions;
- northern-raids choices modify North/West loyalty and preserve the authored wood outcome.

## Work items

1. Add typed loyalty config/state, including bounds, workforce curve, rebellion/recovery
   rules, class gates, and battle-loss threshold. Missing intermediate curve values and
   thresholds are config+ledger entries.

2. Add one `applyLoyaltyDelta(target, amount, source)` funnel. It clamps, applies confirmed
   operational modifiers once, records provenance, updates rebellion/recovery, refreshes
   dependent building/production state, and appends one chronicle entry. No magic
   `loyaltyNorth`/`loyaltyWest` state remains live without a typed reader/migration.

3. Route effective workforce, construction/production projections, building operation,
   recruitment targeting, settlement, and UI through the same loyalty/class-gate helpers.
   Rebellion removes a region from normal control while preserving history and an authored
   recovery path; do not reuse `destroyedRegionIds`.

4. Consume P3A's typed battle loss exactly once. Use its city/region provenance and authored
   denominator; save/reload cannot reapply the delta.

5. Add a clamped, typed reputation mutation/helper and visible state. Later phases consume
   it through real dependencies; do not un-defer P6 trade merely because the scalar exists.

6. Add `chronicle` state with deterministic ordering, stable IDs, newest-first UI projection,
   bounded retention from the common contract, and entries for loyalty, reputation,
   rebellion/recovery, and consumed battle loss.

7. Implement `event-northern-raids` through typed regional loyalty effects and the existing
   event/payment pipeline. Both choices, eligibility, wood, chronicle, and restore must work.

8. Reconcile `municipal-capital-forum`, `card-clubs-2` inverted, and uniquely mapped
   Народ-suit loyalty faces. Forum semantics must be temporal and operationally precise;
   every retained effect needs a reader. Placeholder faces remain deferred.

9. Add UI/QA for city/region loyalty, class gates, reputation, rebellion, chronicle, and
   exact disabled reasons. Add a `loyalty-rebellion` scenario crossing battle loss,
   rebellion/recovery, save/restore, and bounded chronicle behavior.

## Conditional carrier gate

- `municipal-capital-forum`: complete both loyalty directions and every retained progress
  effect or keep it deferred.
- `card-clubs-2` inverted: unique raw mapping, consumed effect, level scaling, cleanup, UI,
  and tests required.
- Additional Народ faces: only unique title + current config ID + side mappings.
- `event-northern-raids`: guaranteed owner here; never replace it with a TD battle.
- Hearts/political faces and reforms remain P4B.

## Verification additions

- Bounds and every workforce table entry; projected/settled parity; loyalty vs worker
  shortage; class gates; forum positive/negative behavior.
- Rebellion/recovery through every accessibility path; destroyed vs rebellious distinction;
  exact-once TD-loss application and restore.
- Reputation dependency substrate, chronicle order/retention, both northern-raids choices,
  carrier cleanup, old-save/config migration, QA/Cypress UI, and common standing gate.

## Designer questions

- Intended intermediate workforce curve and rounding?
- Rebellion/recovery thresholds, duration, and player recovery action?
- Forum delta/current/effective-value semantics and progress effect?
- Exact class scope/threshold for Smithy and other buildings?
- Battle-loss denominator and city/region attribution?
- Which authored Народ titles map uniquely to current faces?
