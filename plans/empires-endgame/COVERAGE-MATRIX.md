# Empire's Endgame — scope and ownership matrix

This is the closure ledger for the completion program. It prevents content that is absent
from the current config from disappearing between phase prompts. Update it whenever a raw
source, config carrier, designer verdict, or phase boundary changes.

Status meanings:

- **Complete** — shipped before or during the current program; later phases only regress it.
- **Owned** — guaranteed substrate/change-set owner.
- **Conditional** — the owner reconciles it, but it stays deferred when semantics or a
  required substrate remain undefined.
- **Review** — present in raw intent but not confirmed for this tranche; do not implement
  until the designer supplies a verdict and an owner.
- **Out** — explicitly outside the confirmed 2026-07-16 scope.

## Program phases

| System / deliverable | Owner | Status | Closure rule |
|---|---|---|---|
| Config migration chain, test discovery, review ledger | P0 | Complete | Historical prompt; regression only. |
| Pure combat/damage/counter catalog | P1 | Complete | Historical prompt; P3B consumes it. |
| Shared minigame envelope, central TD slice, army substrate | P2 | Active/near complete | P3A reconciles only unfinished obligations. |
| Replay config identity, tick-index commands, bounded logs, input/accessibility carryover | P3A | Owned | Applies to the P2 envelope/TD before additional minigames depend on it. |
| Five TD regions, castle/naval enemy mode, grade matrix, reusable assault | P3A | Owned | No steel progression in this change-set. |
| Full latest steel source, equipment production, military buildings | P3B | Owned | Existing 22 IDs are not assumed to be the whole authored tree. |
| City/region loyalty, reputation, rebellion, class gates, chronicle | P4A | Owned | Includes TD-loss funnel and northern-raids event. |
| Seasons, technology light/dark sides, political reforms, crime substrate | P4B | Owned/Conditional | Undefined political/crime semantics remain deferred. |
| Advisors, advisor suit/Grand Advisor flow, governor персты, capital slot governance | P4C | Owned/Conditional | New definitions require exact raw identity and semantics. |
| Epidemic lifecycle and medical chain | P5 | Owned | Depends on P4A/P4B; Alchemy may retain a minigame subfeature deferral until P10. |
| Domestic economy: Bank, insurance contract, Fair, Temple, passive Tavern | P6A | Owned/Conditional | No external diplomacy or Tavern minigame. |
| Diplomacy/external trade, Людовик, Customs, Sea Port, Stable, missing trade buildings | P6B | Owned/Conditional | Real relationships/unions replace “external-world-lite”; fleet-only effects wait for a real fleet. |
| Economy gifts/relics/events/resources/card-face closure | P6C | Owned/Conditional | Complete retained consequences or keep each carrier deferred. |
| Quest/dialogue engine, Палач, golden-idol/witch event bridges | P7 | Owned | JSON Builder support first; graph editor optional. |
| God presence | P8 | Owned | Deck memory, anti-bito, authored lines, Милость confirmation. |
| Tavern minigame, mystics, Мария/Пиковая Дама | P9 | Owned | Owns final Tavern minigame subfeature and whole-building closure. |
| Tetris-alchemy | P10 | Owned | Owns final Alchemy active/minigame subfeature and whole-building closure. |
| Expedition lifecycle, typed forts/zones, provisions, veterans, TD assault | P11A | Owned | No falling-cart simulator in this change-set. |
| Tetris-inventory packing | P11B | Owned | Integrates with P11A; packed/unpacked inventory has explicit ownership. |
| Chess | P12 | Conditional design gate | Disabled until an executable rules table exists; no invented standard chess. |
| Remaining ready carriers and raw-to-config coverage sweep | P12B | Owned | No new substrate; implement ready content or record explicit blocker/verdict. |
| Full-chain compatibility, storage, performance, and integrated campaign stabilization | P13 | Owned | Final v1→latest and all-minigame gate. |

## Building and governance catalog

| Design item | Owner | Status / note |
|---|---|---|
| Farm, Lumber, Mine, Small Temple, Granary, Trade Levy | Historical | Complete; regression coverage only. |
| Barracks, Smithy | P2/P3A carryover | Finish only if P2 leaves a concrete incomplete contract. |
| Foundry, Military Academy | P3B | Existing conditional carriers. |
| Мастерская, Баллиста, Двор Гвардейской Дружины | P3B | Config-absent candidates; add only with complete authored military consumers. |
| Capital Forum | P4A | Loyalty in both directions plus every retained effect. |
| Книгопечатный пресс | P4B | Config-absent cultural/technology candidate; conditional. |
| Hospital, Medical Academy | P5 | Whole-effect closure required. |
| Alchemy | P5 + P10 | P5 owns epidemic/passive substrate; P10 owns active minigame and final marker closure. |
| Bank, Jewish/Insurance Bank, Fair, Temple | P6A | Conditional on complete typed lifecycles/effects. |
| Tavern | P6A + P9 | P6A owns passive substrate; P9 owns minigame and final marker closure. |
| Stable, Customs, Sea Port | P6B | Sea Port is coastal-only and empire-capped at four. Fleet effects require player-fleet substrate. |
| Чёрный рынок, Посольство, Внешний рынок | P6B | Config-absent candidates; conditional on authored diplomacy/espionage/trade semantics. |
| Advisors, Grand Advisor, governor персты, capital slot governance | P4C | Absent substrate, now explicitly owned. |
| Great houses and unique regional races | P7/P12B review | Review: quest/event content needs exact authored definitions and designer priority. |
| Coliseum and other named capital/map systems without current carriers | P4C/P12B review | Inventory and seek verdict; do not silently omit or invent. |

## Content families

| Family | Owner | Status / rule |
|---|---|---|
| Military units, ♥7, combat-spirit gift | P2/P3A carryover | Reconcile only unfinished P2 work. |
| Steel technologies, war buildings, morale relic, conditional ♥A | P3B | Exact current IDs plus raw-tree additions with stable IDs. |
| Loyalty/political cards, northern raids, political reforms | P4A/P4B | Each face/reform is an independent closure contract. |
| Vaccination/disease faces, epidemic relic/event | P5 | Дженна mapping remains unproven until raw/config identity matches. |
| Economy technologies, gifts, relics, events, resources, card faces | P6A–P6C | Exact inventories live in owning prompts; absent definitions are not ignored. |
| Палач and event-shaped quests | P7 | Guaranteed. Other quest backlog enters P12B only with authored executable graphs. |
| Mystic definitions and Мария candidate | P9 | Separate from the standard 53-card catalog. |
| Logistics techs and ♠3 | P11A | Conditional whole-contract closure. |
| Remaining placeholder/authored card faces | P12B | Unique raw title + config ID + side required; no rank/suit guessing. |

## Explicit review and out-of-scope inventory

| Item | Status | Reason |
|---|---|---|
| Remaining regional quest backlog (крестоносцы-вёдра, север↔лес, упыри Аматы, титановая рыба, руда в лесу, etc.) | Review/P12B | Engine is P7; individual graphs need priority and complete authored branches. |
| Full player fleet/siege campaign | Review | TD may contain enemy/naval battlefields, but that is not automatically a player fleet or persistent siege system. |
| Full great-house/race content | Review | Present in raw model but not confirmed as a complete implementation tranche. |
| Sound | Out | Designer-confirmed out of scope. |
| Scripted tutorial staging | Out | Designer-confirmed out of scope. |
| Lore implementation, advisor finales, secret ending/meta ladder | Out | Designer-confirmed out of scope; raw lore remains reference only. |
