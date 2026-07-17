# Phase 9 — Tavern minigame and mystic cards

Read the common contract and coverage matrix. Execute after P6A, P7, P8, and P3A. This
phase consumes the shared envelope and passive Tavern substrate without redesigning the
standard 53-card Durak catalog.

## Ownership handoff

P6A may hand off either:

- a passive-live Tavern with a validated deferred `tavernMinigame` capability; or
- a still-deferred Tavern whose passive substrate/hook is complete but whose whole marker
  could not honestly be removed.

Both are valid prerequisites. P9 owns the minigame capability and final whole-building
closure. Do not stop merely because the marker remains; stop only if passive substrate/hook
is absent or incomplete.

## Guaranteed deliverable

Ship a deterministic Tavern encounter, exact spawn progression, mercenary/spirits/rumors
sections, Мария Брауз 2×2 flow, a separate mystic-card catalog/lifecycle, persistent hand
ordering where gameplay needs it, and Пиковая Дама's sourced behavior.

## Required raw sources

Read `таверна`, `карты`, `персонажи`, `общее`, and `empire_prompt`, including all messages
for Лист, Лорик, Анатолий, Мария Брауз, Пиковая Дама, and the 3–7–Т combination. Reconcile
`card-spades-queen` but never infer identity from rank alone.

## Work items

1. Add a **separate mystic definition catalog** with no standard suit/rank. Preserve exactly
   52 suit/rank cards + Joker in the core catalog. Mystic instances occupy a typed zone and
   cannot enter attack/defense/trump/throw/refill/empty-hand/winner accounting unless the raw
   source explicitly defines an exception.

2. Make the existing serialized hand/zone arrays authoritative order. Do not add a duplicate
   mutable `handOrder` source. Centralize draw/take/return/spawn/remove mutations and validate
   unique stable instance IDs/order. Player reordering exists only if raw design gives a cost/
   action; otherwise draws/takes/spawns/returns define order deterministically.

3. Implement exact Лист/Лорик/Анатолий spawn, ownership, passive, leave-hand, return-inverted,
   and timing rules. Deferred per-card behavior cannot affect standard Durak completion.

4. Add Tavern config/session/result with spawn progression lead “not first run, 100% second,
   then 33%.” First determine what “run” counts and where/reset it persists. Use typed profile
   prefs only if the raw source truly means cross-campaign meta progression; do not assume
   localStorage scope from the percentages alone.

5. Implement two authored Tavern sections: hiring/mercenaries and spirits/rumors. Results
   use P3 army/morale, P7 dialogue/quest hooks, and P8 authored God-line/deck-hint hooks.
   Rumors never reveal unearned deck information or select cosmetic lines through gameplay RNG.

6. Implement the 33% Мария encounter and 2×2 exchange, exact 3–7–Т recognition, and Queen
   spawn only after raw identity/conditions are proven. Distinguish standard Мария card
   content from a spawned Queen/mystic entity.

7. Implement Queen periodic neighbor inversion against the authoritative ordered zone. Define
   tick boundary, side, edge/single-card behavior, atomic inversion, removal/appeasement, and
   restore idempotence only as sourced.

8. Build accessible Tavern/2×2 UI, mystic row/order presentation, Queen feedback, and QA
   fast resolve. Clear the `tavernMinigame` capability and remove the whole Tavern marker only
   after every retained P6A+P9 effect is executable.

## Carrier gate

- `building-tavern`: final owner here under the handoff above.
- `card-spades-queen`: conditional Мария carrier; unique mapping and both relevant face
  contracts required.
- New mystic definitions may be live only with complete raw passives; incomplete definitions
  retain their own deferrals. No generic flag is allowlisted.

## Verification additions

- Every standard Durak legality/trump/refill/winner path with mystics present; exact core 53
  invariant; zone/order uniqueness; lifecycle and restore.
- Spawn first/second/later boundaries, counter scope/reset, 33% deterministic encounter,
  2×2 positive/near-miss, Queen tick/edges/idempotence, rumor/God RNG isolation.
- Final Tavern whole-contract test, active-session rules identity, real input smoke,
  QA/Cypress, migration, bounded history, and common gate.

## Designer questions

- What does “run” count and where/reset is progression stored?
- Exact mystic zones, owners, passives, leave/return-inverted timing?
- Exact 2×2 rules and meaning/order/suit of 3–7–Т?
- Is Мария definitively `card-spades-queen`; what are both face semantics?
- Queen entity, period, side, edges, removal, and reorder interaction?
