# Phase 5 — epidemics and the medical chain

Read the common contract and coverage matrix. Execute one change-set after P4A/P4B. P4C is
not a gameplay prerequisite unless the raw source explicitly ties an epidemic action to an
advisor/perst.

## Guaranteed deliverable

Ship a deterministic first-class epidemic lifecycle with typed sources/stages/classes,
spread/containment/decay, population/production/loyalty consequences, multiplicative
medical protection, exact settlement order, map/city UI, and the complete medical content
that has real consumers. P10 owns the Alchemy minigame and final active-lab closure.

## Required raw sources

Read `здания`, `события`, `Технологии / доктрины-и-реформы`, `реликвии`, `карты`,
`общее`, and relevant `экспедиции` disease/enemy context. Reconcile Hospital, Medical
Academy, Alchemy, Quarantine, Фармацевтика, City Gates, vaccination faces, epidemic relic,
and the Амбар+Алхимия combination against current config IDs/effects.

Do not infer Дженна from rank/suit. Current `card-spades-10` vaccination faces are exact
baseline candidates; `card-clubs-ace` remains a placeholder unless raw identity proves
otherwise.

## Work items

1. Add epidemic definitions/state with stable IDs and at least source/provenance, origin,
   stage/severity, started con, remaining duration, containment, spread state, and explicit
   affected population-class IDs/weights. The population-class impact distribution is
   config data; do not silently default to proportional losses without raw support/ledger.

2. Add one typed `startEpidemic` funnel used by events, gifts, hidden combinations, later
   alchemy results, and future quests. Validate trusted city/source/definition targets,
   duplicate-disease semantics, and inaccessible-origin behavior.

3. Add deterministic `settleEpidemics()` inside end-of-empire settlement **before** fresh
   deficit/famine evaluation. Stable-order simultaneous stage/impact/spread/end transitions;
   use serialized RNG; apply population/production/class/loyalty effects once; append bounded
   chronicle entries; refresh projections before famine.

4. Add pure protection resolution from operational Hospital, Medical Academy, confirmed
   Quarantine/Фармацевтика state, vaccination, and `relic-epidemic-ward`. Define which
   consequence each source reduces, multiplicative order, rounding, and bounds. The relic's
   raw “25” must be resolved as impact reduction versus outbreak prevention—never guessed.

5. Implement City Gates containment/open behavior through typed state. Both choices need
   executable local/spread effects; P4B's reform must be honestly live or the event remains
   blocked. Destroyed/rebellious/inaccessible targets, already-open spread, and duplicate
   targets are explicit.

6. Implement the confirmed Амбар+Алхимия plague trigger through P4B's hidden-combo memory.
   Trigger once at the authored boundary; disclosure and reload cannot reroll/reapply.

7. Reconcile buildings as whole contracts: Hospital treatment/healing plus epidemic effect;
   Medical Academy protection/free-tech/treatment; Alchemy passive/combo behavior. Introduce
   validated capability-level deferral when a building is otherwise complete but its P10
   active minigame is not. P10 may receive a live passive building with a deferred
   `alchemyMinigame` capability, or a still-deferred building with an explicit handoff; it
   owns final active-lab closure either way.

8. Reconcile `card-spades-10` normal/inverted independently and the epidemic relic. Perform
   a separate raw/config identity audit for Дженна; do not modify the placeholder unless
   uniquely proven.

9. Add epidemic badges to `EmpireMap` markers and detailed city state: stage, turns,
   affected classes, containment, protection breakdown, projected next impact, and spread
   warning. Add JSON Builder validation and `epidemic-outbreak` QA scenario.

10. Decide from raw evidence whether epidemics additionally block recruitment or lock
    facilities. Implement as typed stage effects only when authored; otherwise ledger the
    question and do not infer generic locks from population loss.

## Conditional carrier gate

- `building-hospital`, `building-medical-academy`: every retained effect required.
- `building-alchemy`: passive/capability split above; P10 owns final active closure.
- `relic-epidemic-ward`, `event-city-gates-epidemic`, `reform-city-gates`, and both
  `card-spades-10` faces: complete typed behavior or remain deferred.
- Quarantine/Фармацевтика with no stable current carrier become substrate hooks plus review
  questions, not invented live IDs.

## Verification additions

- Stage boundaries, affected-class distribution/rounding, exact-once impact/decay, seeded
  spread, duplicate/inaccessible cases, and restore.
- Exact order: epidemic → refreshed population/production/deficit → famine roll → remaining
  economy settlement, including changed famine eligibility.
- Each protection source and complete stack; operational/locked/deferred sources; relic 25
  semantics; both gate choices; hidden combo; card faces; capability-level Alchemy handoff.
- Map badge/city UI, QA/Cypress, previous config/save migration, active rules identity where
  minigame results start disease, and common standing gate.

## Designer questions

- Severity/stages/duration, spread, duplicate behavior, and impact per population class?
- Protection order/scope/rounding and exact relic “25” meaning?
- Does an epidemic block recruitment or facilities independently of population/operation?
- Exact City Gates open/seal behavior and crime interaction?
- Which event/choice activates Амбар+Алхимия, and what remains for P10?
- Does Дженна map to any current definition/side?
