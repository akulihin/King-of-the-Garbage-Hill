# Phase 6C — economy gifts, relics, events, resources, and card faces

Read the common contract and coverage matrix. Execute after P6B. This is a content-closure
change-set on existing P4–P6 substrate, not permission to add a new lifecycle casually.

## Guaranteed deliverable

Reconcile the exact economy/external candidate inventory below, implement every candidate
whose complete semantics now have typed consumers, and leave every other carrier explicitly
deferred with a source-backed blocker. Add no unrelated building/system substrate.

## Required raw sources

Read `божественные-награды`, `реликвии`, `события`, `карты`, `экономика`, `здания`,
`общее`, and every cross-reference for targets/duration/negative consequences. A positive
number or existing flag is not proof that the lifecycle is complete.

## Candidate inventory

**Gifts:** `gift-earthquake`, `gift-tailwind`, `gift-fish-currents`,
`gift-meteor-iron`, `gift-desert-tsunami`.

**Relics:** `relic-tithe`, `relic-resource-exemption`.

**Events:** `event-lumber-concession`, `event-customs-smuggling`, `event-horse-theft`,
`event-bank-insurance`, `event-white-stone`.

**Resources:** `whiteStone`, `carpentry`.

**Card faces:** `card-diamonds-6` normal, `card-diamonds-ace` inverted, plus any other
economy/diplomacy face only after unique raw title + config ID + side reconciliation.

## Work items

1. Create a carrier table: exact current effects/choices, raw semantic, target, duration,
   typed consumer, prerequisite closure, invented values, and outcome (`live`, `blocked`,
   `review`). Update the coverage matrix for raw content absent from config.

2. Implement each gift through trusted typed resolution/lifecycle state. Earthquake needs
   its full enemy/city consequence; tailwind a real naval consumer; fish currents all
   authored outcomes and world-disaster duration; meteorite iron its radiation/negative
   consequence as well as resource gain; desert tsunami its persistent South/watermill/
   resort behavior. Incomplete gifts stay deferred.

3. Make tithe alter actual income through P6A Temple/relic slots. Make material exemption
   change both Smithy and Stable resource requirements through canonical cost helpers.

4. Implement all choices/eligibility/recurrence/restore for the five events. Smuggling uses
   P6B Customs/trade and typed population consequences; horse theft has repeat/disable/
   target/noble-loyalty behavior; insurance uses P6A contracts; white stone has real mine/
   mortality/production/spending state; concession uses P4A regional loyalty/resource rules.

5. Give `whiteStone` real production, mortality/risk, display, and at least one authored
   consumer before its event becomes live. Give `carpentry` production and construction
   expense consumers or keep it deferred.

6. Implement the exact two baseline card faces only when monopoly/Don or internal/external
   trade state, level scaling, temporary cleanup, UI, and tests are complete. Placeholder
   diamonds remain deferred.

7. Add focused UI/QA/Builder coverage for every carrier actually made live and an exact
   remaining manifest for all candidates.

## Verification additions

- Every typed gift target is revalidated on restore; duration/expiry and negative effects
  execute once; no dead live flag.
- Relic cost/income calculations, all event branches/recurrence, resource production/risk/
  spending, card scaling/cleanup, invalid references, migration, QA/Cypress, and common gate.

## Designer questions

- Complete mandatory consequence for each gift, especially fleet/weather/world-disaster,
  radiation, and South persistence?
- Tithe base/rounding and material-exemption scope?
- White-stone mine mortality and consumers; carpentry production/spending?
- Exact monopoly/Don and internal-trade lifecycle for the two card candidates?
