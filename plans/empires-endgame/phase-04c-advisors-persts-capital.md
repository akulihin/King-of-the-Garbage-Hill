# Phase 4C — advisors, governor персты, and capital governance

Read the common contract and coverage matrix. Execute after P4B. This phase exists because
the previous packs named advisors/persts as entirely absent but assigned them no owner.

## Guaranteed deliverable

Create typed, save-compatible governance substrate for advisor selection/status, authored
advisor/card-suit interaction, Grand Advisor access, governor персты and their building-slot
effects, and confirmed capital-governance actions. Undefined great-house/race/lore content
remains review-scoped rather than silently omitted.

## Required raw sources

Read `DiscordExports/empire_prompt`; channels `общее`, `основной`, `персонажи`, `карты`,
`застройка`, `дома`, `регионы`, and relevant `Технологии_*`; inspect linked map/capital
assets. Separate executable rules from lore and secret-ending/meta material, which remains
out of scope.

Raw leads requiring reconciliation include:

- starting advisor roster and the “2 казнить / 1 помиловать” flow;
- advisor suit matching trump/card categories and the Grand Advisor gate for ♣ trump;
- перст/governor assignment and additional construction points (`2 > 2 > 1` lead);
- capital-only locations/actions such as Тетракорархос, Forum, Coliseum, academy, and white
  stone mine—only where this or another matrix owner supplies complete behavior.

## Work items

1. Produce a source-to-config governance table: advisor identity, suit/category, starting
   availability, selection/execution/pardon transition, effect, target, duration, and
   unresolved semantic. Add definitions only with stable raw identity; no display-name
   dispatch.

2. Add typed config/state for advisor roster/status and one transition funnel. Enforce legal
   state changes, deterministic ordering, exact-once consequences, save normalization, and
   UI/QA visibility. An advisor finale/secret-ending field is not needed.

3. Route confirmed suit/trump/Grand Advisor mechanics through canonical Durak selectors and
   card-effect activation, not page-only badges. Preserve the exact 53-card/mystic contracts
   and existing trump legality. Every advisor effect must have a real consumer.

4. Add typed governor/perst assignment and slot-capacity projection. Construction placement,
   upgrade availability, UI previews, save restore, and Builder validation use one helper.
   Reassignment/cost/cooldown exists only if authored. Never encode persts as anonymous flags.

5. Reconcile named capital systems against other owners. Capital Forum remains P4A; Military
   Academy P3B; white stone resource/event P6C. Implement only governance/slot actions unique
   to this phase. Coliseum/Тетракорархос or other absent definitions remain conditional when
   combat/event semantics are incomplete.

6. Inventory great houses and unique regional races. If they are complete event/quest
   definitions, route them to P7/P12B in the coverage matrix; do not absorb a regional
   narrative campaign into this substrate phase.

7. Add accessible advisor/governor/capital UI, JSON Builder support, QA scenario, and old-save
   migration. No authored player text is rewritten.

## Verification additions

- Advisor transition table, invalid/double transition, suit/trump and Grand Advisor gates,
  effect cleanup, reload/idempotence, and no standard-deck regression.
- Perst assignment, slot-capacity boundaries, construction/projected parity, reassignment
  rules if authored, save/config migration, dangling reference rejection.
- Capital owner boundaries and remaining review manifest; QA/Cypress for one advisor and one
  perst flow; common standing gate.

## Designer questions

- Exact starting advisors and legal selection/execution/pardon sequence?
- What mechanically changes when advisor suit matches trump, and how is Grand Advisor opened?
- Perst count, assignment timing/cost, slot sequence, reassignment, and loss conditions?
- Which capital systems are required now versus raw concepts for a later tranche?
- Which great-house/race definitions are sufficiently authored for P7/P12B?
