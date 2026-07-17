# Phase 6B — diplomacy, external trade, and trade buildings

Read the common contract and coverage matrix. Execute after P3B and P6A. This phase replaces
the vague “external-world-lite” boundary with a typed relationship/trade substrate while
keeping undefined player-fleet/siege mechanics deferred.

## Guaranteed deliverable

Ship deterministic external relationships/offers, explicit Людовик accept/decline flow,
reputation-gated trade/unions where authored, and complete Stable/Customs/Sea Port behavior
that has real substrate. Audit and route config-absent Чёрный рынок, Посольство, and Внешний
рынок instead of silently omitting them.

## Required raw sources

Read `экономика`, `здания`, `общее`, `дома / торговая-гавань`, `события`, `застройка`,
and trade/diplomacy material in `Технологии / доктрины-и-реформы`; inspect all Людовик,
Alliance, union, spy, market, horse, port, and ship messages.

## Work items

1. Add typed external config/state for actors/relationships, reputation requirements,
   authored unions/contracts, offer definitions/weights/cadence, active offers, stable offer
   history, and expiry. Do not duplicate P2/P3 Alliance threat or P4A reputation.

2. Implement deterministic Людовик eligibility/refresh from serialized RNG and stable IDs.
   Provide explicit **accept and decline** engine actions. Both record/expire the offer once;
   refusal consequences exist only when authored. QA policies deterministically accept,
   decline, or choose by ID; reload cannot reroll.

3. Implement trade/payment through existing trusted city/shared resource plans. Relationship,
   reputation, region accessibility, prerequisites, offer stock, and expiry are rechecked in
   the engine. UI shows exact accept/decline effects and failure reasons.

4. Implement Customs against real tariff/import/export calculations and its P6C smuggling
   event hook. Implement Stable only with livestock/farm eligibility and complete cavalry/
   equipment/active consumers from P3B; preserve raw western horse/resource asymmetry.

5. Implement Sea Port placement and operation from raw city data: coastal/maritime cities
   only, dedicated slot eligibility, **maximum four across the empire**, and complete
   trade/shipbuilding/fleet effects. `placeBuilding`, upgrade, Builder, and UI use one rule.
   If no player-fleet substrate exists, retain the fleet capability/whole marker rather than
   misusing enemy naval TD.

6. Audit config-absent buildings:
   - `Чёрный рынок`: hostile-city technology purchase only with real relationship, price,
     availability, and research integration;
   - `Посольство`: diplomat/spy, bribery, theft, route discovery only when fully authored;
   - `Внешний рынок`: external/rebellious-city exchanges with real targets/contracts.
   Add stable definitions only with complete semantics; otherwise keep explicit review rows.

7. Implement `tech-compass` and `tech-merchant-guilds` only through real transfer/trade/
   offer calculations. Preserve dependency/day/cost behavior.

8. Add external/diplomacy UI, JSON Builder validation, QA actor/offer scenario, and Cypress
   coverage including accept, decline, reputation denial, non-coastal Port rejection, and
   fifth-Port rejection.

## Conditional carrier gate

- `building-stable`, `building-customs`, `building-sea-port`, `tech-compass`, and
  `tech-merchant-guilds`: complete retained effects required.
- Чёрный рынок, Посольство, Внешний рынок: config-absent conditional definitions, never
  invented merely to make the catalog count complete.
- Player fleet/persistent siege remains review-scoped unless exact substrate ships here.
- Gifts, relics, events, resources, and economy card faces remain P6C.

## Verification additions

- Offer eligibility/weight/refresh/accept/decline/expiry/refusal, reload determinism, and
  relationship/reputation gates.
- Customs calculation parity, Stable prerequisites/consumers, Sea Port coastal/max-four
  rules across placement/upgrade/Builder/UI, and missing-building reference validation.
- Active offers/config rules identity and history retention; QA/Cypress; migration; common
  standing gate.

## Designer questions

- Людовик cadence/pool/weights, relationship changes, and refusal consequences?
- Which unions/relationship actions constitute the confirmed diplomacy tranche?
- Stable western synergy and complete cavalry active semantics?
- Which cities are maritime, and are the four allowed Ports exactly Tetrakor plus three
  regions as the raw building note suggests?
- Does a player fleet exist in this tranche; if not, which Port effects stay deferred?
- Final Black Market/Embassy/External Market definitions and prerequisites?
