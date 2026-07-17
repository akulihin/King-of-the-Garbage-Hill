# Phase 11A — expeditions, forts, provisions, and veterans

Read the common contract and coverage matrix. Execute after P3B and P7. Reuse P4A loyalty,
P3A assault, P3B combat/equipment, and P7 quests. Tetris-inventory is P11B.

## Guaranteed deliverable

Ship typed expedition definitions/state, fortress/zone map flow, trusted provisioning,
canonical roster/veteran/wound behavior, regional complaints through quests, regional enemy
profiles through combat, and one TD assault settlement path. A complete expedition can run
with direct provisioning before P11B adds the packing minigame.

## Required raw sources

Read `экспедиции`, `тд`, `чо-добавить`, relevant `застройка`, `карты`, `квесты`, and
linked map/TD assets. Reconcile the live `map-south-fortress` object and current army/
veteran representation before selecting canonical ownership.

Raw provisioning says equipping an expedition causes a one-time provision reduction in the
origin region; the amount carried affects troop death risk over the planned duration. Treat
that as the lead to verify, not as an undefined generic “reduction.”

## Work items

1. Transcribe exact expedition IDs, trigger quest, origin/target, typed fortress, zone,
   stages, eligible roster, enemy profile, provision/duration rule, complaint triggers,
   rewards, retry/loss/abort, and repeatability. Missing numbers use config+ledger; missing
   semantics keep the expedition definition deferred.

2. Add typed expedition config/state and migration. State owns status, origin/fort/zone,
   roster stable instance IDs, provision plan, duration/timing, assault attempts, outcome,
   reward/complaint idempotence, and result history under retention bounds.

3. Replace fortress-specific generic `properties` with a discriminated typed payload while
   preserving other map object kinds. Migrate `map-south-fortress` without changing identity/
   position; add other forts only when authored. Reject dangling expedition/TD/zone refs.

4. Reconcile P2/P3 veteran ownership before changing it. Keep HP/wounds/veteran status on
   one canonical army unit entity; expedition state stores IDs/snapshots needed for replay,
   not duplicate mutable units.

5. Implement expedition availability/planning and one settlement funnel consuming P3A TD
   assault result. It applies losses/wounds, provisions, zone unlock/reward, complaint/quest
   triggers, status, and restore guards once; never recalculate combat.

6. Implement provision withdrawal once from the origin region at the trusted launch
   boundary. Planned duration/amount modifies the authored attrition/death risk. Abort before
   launch versus after withdrawal, retries, installment tech, returned supplies, and region
   famine interaction need explicit rules. P11B later supplies packed item IDs/efficiency to
   this same plan—never an empire-wide overwritten flag.

7. Implement veteran threshold and wound transitions at the sourced boundary. A second
   wound/removal and death are distinct. Veterans must have a typed/configured payoff in a
   later battle if the raw source defines one; otherwise record the missing bonus and do not
   invent it merely to justify the status.

8. Add regional enemy profiles through P1/P3 combat: South unarmored, West bone/leather,
   swamp creatures, and other regions only as sourced. Stable IDs/stats live in config.

9. Implement regional complaints through P7 quest triggers and P4A loyalty, with origin,
   frequency/provision/duration/loss criteria as authored. Clicking an available fortress
   opens one expedition planning UI; victory changes the canonical zone state.

10. Reconcile `tech-military-logistics`, `tech-supply-corps`, and both `card-spades-3` faces.
    Speed/timing, installment provisions, world-map/diplomacy/quest/confusion, level scaling,
    cleanup, and UI must all execute before markers are removed.

11. Decide whether expedition preparation/launch consumes empire days, advances across cons,
    or both. Encode one time model in config/state and use it in availability, provisions,
    complaints, quest triggers, UI, and restore.

## Verification additions

- Typed fort migration, availability, direct provision withdrawal once, duration/attrition,
  abort/retry, assault result once, zone/reward/complaint once, restore.
- Veteran threshold boundaries, first/second wound/removal, configured bonus consumer if
  sourced, ordinary-TD compatibility, canonical unit identity.
- Regional combat profiles, logistics/supply/card full effects, day/con timing, QA full
  expedition without packing, Cypress map/planning/assault, migration, and common gate.

## Designer questions

- Exact forts/zones/triggers/rewards and what an opened zone grants?
- Provision amount/duration/death-risk formula, refund/abort/installment, and origin famine?
- Day-budget versus multi-con expedition timing?
- Veteran threshold, second-wound definition, and actual later-battle bonus?
- Complaint triggers/choices and launch card integrations?
