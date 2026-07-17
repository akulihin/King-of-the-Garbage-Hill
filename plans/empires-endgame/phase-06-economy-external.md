# Phase 6A — domestic economy lifecycles

Read the common contract and coverage matrix. Execute one change-set after P4C and P5.
P6B owns diplomacy/external trade; P6C owns the large gifts/relics/events/card batch.

## Guaranteed deliverable

Ship typed domestic Bank credit/repayment, insurance contracts, Fair progression/actions,
Temple functions, and passive Tavern recruitment/morale substrate. Preserve loyalty,
reputation, epidemic, battle, and governance ownership from earlier phases.

## Required raw sources

Read `здания`, `экономика`, `общее`, relevant `Технологии / доктрины-и-реформы`,
`таверна`, `реликвии`, `события`, and `дома`. Reconcile the complete current definitions of
`building-bank`, `building-jewish-bank`, `building-fair`, `building-temple`, and
`building-tavern`, plus `tech-fair` and `tech-banking`.

## Work items

1. Add typed, migrated domestic-economy config/state for loans, repayment schedules,
   insurance contracts, Fair actions/cooldowns/progression, Temple slots/actions, and
   Tavern passive availability. Stable IDs/provenance and exact-once settlement are required.

2. Implement Bank actions and settlement: trusted resource transfer, principal/term/
   interest/stacking/eligibility, scheduled payments, default/гонения through P4A loyalty/
   reputation, chronicle, UI, and save-safe idempotence. Debt is not a one-use flag.

3. Implement insurance selection, three-calm-turn or raw-authored activation, covered-event
   payout/compensation, consumption/expiry, and UI. Do not equate `окружение` with a lost
   home-region battle. If the source still lacks a real siege state, represent that retained
   capability as explicitly deferred and keep the whole carrier honest.

4. Implement Fair progression/actions such as Карнавал → Артисты → Табор → барон only in
   the raw-authored order/cadence. Timed loyalty/reputation/resource consequences use typed
   state, survive reload, expire once, and expose deterministic availability reasons.

5. Implement Temple preaching, tithe substrate, and relic storage/slot behavior with real
   consumers. P6C owns specific economy relic payoffs; this phase owns the slot/action model.
   Existing gold production alone is not whole-building closure.

6. Implement Tavern's authored passive morale/recruitment/spirits hook against P2/P3 army
   state. Introduce a validated `tavernMinigame` capability deferral when the passive
   building is otherwise complete. P9 owns that capability, Tavern minigame/mystics, and
   final whole-building closure. Do not require P9 to pretend P6A fully completed it.

7. Make `tech-fair` and `tech-banking` unlock actual Fair/Bank operations, with normal
   dependency/day/cost behavior. No allowlist-only technology.

8. Add domestic-economy UI/QA and JSON Builder support: obligations, due/expiry/cooldown,
   selected city, exact unavailable reason, Temple slots, and passive Tavern state.

## Conditional carrier gate

- `building-bank`, `building-jewish-bank`, `building-fair`, `building-temple`:
  full retained effects or explicit capability-level blockers.
- `building-tavern`: passive substrate here; `tavernMinigame` remains P9-owned.
- `tech-fair`, `tech-banking`: real action consumers required.
- No Stable, Customs, Sea Port, Black Market, Embassy, External Market, Людовик offers,
  economy gifts/relics/events/cards, or resource additions in this change-set.

## Verification additions

- Loan take/repay/default/гонения, insufficient funds, stacking, due-boundary, reload, and
  duplicate-settlement tests.
- Insurance calm-turn activation, covered/uncovered outcomes, payout once, expiry, unresolved
  `окружение` rejection; Fair progression/cooldown/expiry; Temple slots/actions; Tavern
  passive and capability handoff.
- QA/Cypress domestic flow, old-save/config migration, bounded histories, and common gate.

## Designer questions

- Bank principal, term, interest, stacking, default, and гонения consequences?
- Exact insurance coverage/payout and what current state truly means `окружение`?
- Current Fair ordering, cadence, repeatability, duration, and барон hook?
- Temple preaching/tithe/relic-slot rules and capacity per level?
- Which passive Tavern effects belong before P9, and what exact capability remains deferred?
