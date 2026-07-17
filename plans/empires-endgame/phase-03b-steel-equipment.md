# Phase 3B — steel tree, equipment production, and military buildings

Read the common contract, coverage matrix, and P3A handoff. Execute one change-set after
P3A's regional TD/assault path and replay identity are real and tested.

## Guaranteed deliverable

Reconcile the **latest complete steel source**, implement its research/pricing/generation/
elite rules, make equipment payoffs flow through P1 combat and P2/P3A TD, and close only the
military technologies/buildings/relic/card effects with complete consumers.

## Required raw sources

Read channel `Технологии / сталь`, especially the latest
`Steel-c748ae22139d6401.txt`; `Технологии / доктрины-и-реформы`; `здания`; `застройка` army
notes; `тд`; `карты`; and `реликвии`. Earlier Steel files and `ZBS MAKING` are background.

The current 22 `steel-*` IDs cover only part of the authored branch model. Do not call them
“the entire tree.” Reconcile:

- six weapon branches (Ударные closed/steal-only, Древковые, Рубящие, Клинковые,
  Стрелковые, Пудра), Особые изобретения, and three armor branches;
- fork/neighbor entry pricing, generation half-steps and delayed-free “+” nodes;
- |Элитное| gating and equipment/method prerequisites such as water-hammer chains;
- weapon/armor production versus unlock-only payoffs.

## Work items

1. Build a source-to-config table for every existing steel node and every additional
   authored node absent from config: stable ID, branch, position, generation, prerequisites,
   cost, equipment payoff, production requirement, and unresolved field. Add absent nodes
   only when identity and semantics are stable; otherwise record them in the coverage/ledger.

2. Extend research state/config with typed fork-price and generation timing data. Persist
   branch-entry history and delayed-free eligibility; restore cannot re-award or re-price.
   Research UI and `firstMissingDependency` show the same reason/cost.

3. Extend the P1 equipment catalog with technology-linked weapons/armor/shields and sourced
   damage profiles. TD units/towers resolve one canonical available/equipped loadout. When
   raw design requires production, consume Smithy/Foundry capacity and `army.equipmentStock`;
   an unlocked technology alone is not fabricated equipment.

4. Implement complete retained effects for `tech-generals`, `tech-foundry`,
   `building-foundry`, `building-military-academy`, and `relic-spirit-floor`. Academy elite
   gates, delayed unit awards, Foundry discounts/timing/upkeep, and morale floor all need
   typed consumers and tests.

5. Audit config-absent `Мастерская`, `Баллиста`, and `Двор Гвардейской Дружины` from the raw
   building catalog. Add a live definition only if this phase supplies every retained
   artillery/guard/active consumer; otherwise add a deferred definition or keep it in
   designer review with the exact missing substrate. Do not use invented player-fleet/siege
   behavior to make them appear complete.

6. Reconcile `card-hearts-ace` normal/inverted as conditional whole-face candidates. A TD
   observer is not enough: every active/morale/unit effect, level scaling, cleanup, UI, and
   save behavior must execute before a face becomes live.

## Exact existing steel inventory

Reconcile these current carriers, subject to the no-fabrication gate:

`steel-laurel-spearhead`, `steel-lancet-spearhead`, `steel-diamond-spearhead`,
`steel-cross-spearhead`, `steel-voulge`, `steel-halberd`, `steel-lance`,
`steel-butted-mail`, `steel-riveted-mail`, `steel-full-mail`, `steel-double-mail`,
`steel-steel-mail`, `steel-nasal-helm`, `steel-bucket-helm`, `steel-kettle-hat`,
`steel-iron-breastplate`, `steel-steel-cuirass`, `steel-water-hammer`,
`steel-heavy-water-hammer`, `steel-ship-cannon`, `steel-hand-bombard`, `steel-arquebus`.

Ударные remains closed/steal-only unless an exact acquisition system exists. A designer
question is not replaced with a normal research edge.

## Verification additions

- Fork pricing state machine, generation timer boundaries, elite gate, restore idempotence,
  branch/day limit compatibility, and exact dependency reasons.
- Every live steel payoff changes canonical combat/TD resolution; production-gated gear is
  unavailable without stock and consumed/produced exactly once.
- Full effects for each building/relic/card actually un-deferred; incomplete candidates
  remain rejected by `validateLiveEffects` and appear in the remaining manifest.
- Deterministic TD replay under multiple loadouts and rules identity; old custom-config
  migration/backfill; Builder round-trip; common standing gate.

## Designer questions

- Authoritative prices, generation delay, elite source, and branch-entry timing?
- Exact entry mechanism for the closed Ударные branch?
- Which missing latest-tree nodes are required now, and what are their stable identities?
- Are Мастерская, Баллиста, Guard Courtyard, and Эдемская катапульта ordinary buildings,
  regional variants, quest rewards, or later siege/fleet content?
- Which ♥A face semantics are complete enough to ship?
