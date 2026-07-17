# Phase 4B — seasons, technology sides, political reforms, and crime substrate

Read the common contract and coverage matrix. Execute after P3B and P4A. Reuse P4A's
reputation/chronicle funnels and P3B's real steel prerequisites.

## Guaranteed deliverable

Ship deterministic seasons and food effects, typed technology light/dark sides with
reputation/chronicle consequences, cultural suppression, and the political/crime substrate
needed by later events. Close only reforms whose complete retained behavior is executable.

## Required raw sources

Read `общее`, `застройка`, `здания`, `события`, `карты`, and all current
`Технологии / доктрины-и-реформы` messages, including greenhouses, cultural passives,
Принуждение, Геройские похороны, Городские врата, Контроль кузнецов, Теократия,
Технократия, and Книгопечатный пресс. Reconcile current config effects before designing.

## Work items

1. Add pure `currentSeason(con, config)` over an ordered config cycle. Route seasonal food
   effects through production projection, settlement, famine eligibility, UI, and QA. Do not
   serialize a second season truth. Implement greenhouse equalization only as authored.

2. Add typed technology side definitions/state: selection/reveal/disclosure trigger,
   executable effects, cultural-suppression tag, and idempotence. Dark-side consequence uses
   P4A reputation and chronicle; restore cannot reveal/apply twice. Preserve research
   branch/day limits and Builder/config round-trip.

3. Add typed hidden-combination schema and deterministic trigger memory. P5 owns actual
   epidemic consequences; this phase supplies only validated combinations/disclosure hooks.

4. Reconcile a first-class crime/public-order scalar or lifecycle only if the raw source
   defines its mutation and consumers. `reform-city-gates` cannot become live merely because
   it has an `crimeReductionPercent` shell. Expose a typed hook for P5 containment while
   keeping undefined crime semantics deferred.

5. Reconcile these whole-contract reform candidates:
   `reform-coercion`, `reform-heroic-funerals`, `reform-control-smiths`,
   `reform-theocracy`, `reform-technocracy`, and `reform-city-gates`. Implement building
   override/loyalty, casualty/recruit protection, specialization/class effects, steel
   prerequisite, political risks, dark experiments, and crime/epidemic behavior only where
   raw semantics exist. Partial reforms stay deferred.

6. Reconcile `card-hearts-5` and `card-hearts-king` faces as conditional political content.
   Undefined legitimacy/agitation/sabotage semantics remain unavailable; no generic flag is
   made live.

7. Audit config-absent `Книгопечатный пресс`. Add a stable definition only if its complete
   cultural/technology effects and prerequisites are authored and consumed; otherwise retain
   it as an explicit coverage-matrix review item.

8. Add season/technology/politics UI and QA using existing surfaces. Chronicle displays
   season transitions and technology disclosures without duplicate entries. JSON Builder
   support is sufficient.

## Verification additions

- Season boundary/rounding, summer/winter production and famine ordering, greenhouse scope,
  save/restore, and projected/displayed parity.
- Side selection/reveal, cultural suppression, reputation/chronicle exact-once behavior,
  hidden-combo trigger memory, and previous-version migration.
- Full-effect tests for every reform/card actually un-deferred; theocracy satisfies the
  exact P3B steel prerequisite without bypass; unread political/crime flags are rejected.
- Deterministic QA scenario crossing a season and one disclosure; common standing gate.

## Designer questions

- Season duration and whether ×2 affects production, limit, consumption tolerance, or more?
- Which existing technologies receive sides retroactively, and what triggers disclosure?
- Exact cultural suppression scope?
- Crime/public-order sources and consumers; what does city-gates crime reduction change?
- Complete authoritative semantics for each political reform and conditional heart face?
- Is Книгопечатный пресс part of this confirmed tranche?
