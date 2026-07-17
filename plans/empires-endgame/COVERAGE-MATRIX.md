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
| Shared minigame envelope, central TD slice, army substrate | P2 | Complete | Historical baseline; P3A regression-checks it without reopening P2. |
| Replay config identity, tick-index commands, bounded logs, input/accessibility hardening | P3A | Complete | Landed with schema-v3 config identity, capped command/result histories and production pointer/keyboard replay coverage. |
| Five TD regions, castle/naval enemy mode, grade matrix, reusable assault | P3A | Complete | Missing outer regional choices remain explicitly deferred; no steel progression was pulled forward. |
| Latest steel source reconciliation, honest equipment production, military buildings | P3B | Complete/Conditional | All 71 named equipment nodes plus ten method/gear prerequisites are inventoried. Four exact spearhead nodes, equipped cohorts, shared-capacity Smithy production, Foundry and the morale-floor relic are live; every incomplete carrier remains explicitly deferred. New regular recruitment requires researched, produced spear stock; only schema-v2 aggregate regulars retain their frozen compatibility profile without a new charge. |
| City/region loyalty, reputation, rebellion, class gates, chronicle | P4A | Complete/Conditional | Typed political state, exact-once TD-loss funnel, northern-raids event, reversible rebellion and bounded chronicle are live. Capital Forum and inverted club 2 remain honestly deferred where their complete mechanics are undefined. |
| Seasons, technology light/dark sides, political reforms, crime substrate | P4B | Complete/Conditional | Derived Summer/Winter food production, greenhouse equalization, typed exact-once side/hidden-combination state and four complete reforms are live. P5 consumed the epidemic policy and hidden-combination boundaries. No crime scalar is invented; Technocracy, City Gates as a whole, Hearts political faces and printing effects remain explicitly deferred. |
| Advisors, advisor suit/Grand Advisor flow, governor персты, capital slot governance | P4C | Complete/Conditional | Three role-stable starting advisors use one persisted 1-pardon/2-execution funnel and gate real reforms; trump effects use one configurable critical multiplier, ♣ remains Grand-Advisor-gated, two source-named персты permanently unlock the authored `2 > 2 > 1` city-site layers, and capital carriers retain explicit owning-phase blockers. The Grand Advisor unlock trigger, remaining персты, and incomplete capital actions stay deferred. |
| Epidemic lifecycle and medical chain | P5 | Complete/Conditional | Typed deterministic stages, class-weighted impacts, simultaneous seeded spread, containment, protection stacking, settlement-before-famine, medical recovery/treatment, UI/QA, both vaccination faces, the epidemic relic and the Granary+Alchemy trigger are live. Alchemy retains only its P10 minigame capability deferral; City Gates stays wholly deferred on crime, and Quarantine/Pharmaceuticals remain carrier-less hooks. |
| Domestic economy: Bank, insurance contract, Fair, Temple, passive Tavern | P6A | Complete/Conditional | Schema-v9/save-v7 typed lifecycles, exact-once settlement, UI/QA and Builder validation are live. Undefined exchange/market/baron/Temple-branch capabilities and the P9 Tavern minigame remain explicit subfeature blockers. |
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
| Barracks, Smithy | P2 | Complete; P3A/P3B consume them, and later changes require a verified new requirement or regression. |
| Foundry | P3B | Complete with ledger defaults in non-capital cities: three 15% reductions and a persisted two-con instant cadence are executable. The `capital-sixth-slot` subfeature stays deferred because the capital's unswappable Temple occupies that slot; exact values and capital placement remain open for designer review. |
| Military Academy | P3B | Conditional/deferred after reconciliation: unit identities/choice, permanent-stat action, elite grant and central/capital placement remain undefined, so the carrier stays unavailable. |
| Мастерская, Баллиста, Двор Гвардейской Дружины | P3B | Reviewed/deferred and config-absent: respectively blocked on artillery/siege ownership, persistent wall/tower-site placement, and crime/loyalty/city-type/rebellion substrate. |
| Capital Forum | P4A | Conditional/deferred: the two-direction operational loyalty reader is implemented and tested, but the bundled carrier remains unavailable because “progress acceleration” has no executable target, amount, clock or cleanup. |
| Книгопечатный пресс | P4B | Reviewed/config-present/deferred: the existing `tech-printing` carrier is stable, but discarded-card extension and neighboring-city propaganda lack complete prerequisite, target, duration and cleanup contracts. |
| Hospital, Medical Academy | P5 | Complete: operational protection, Hospital-gated healer and 2→1-con recovery, Academy three-con free secondary technology and once-per-con 50%-fatal veteran treatment are all consumed. Configurable defaults are ledgered. |
| Alchemy | P5 + P10 | P5 complete/conditional: the live building participates in the exact-once Granary plague combination; its `alchemyMinigame` capability remains explicitly deferred to P10. |
| Bank, Jewish/Insurance Bank, Fair, Temple | P6A | Complete/Conditional: Bank has frozen-income scheduled debt, default and гонения; Insurance Bank has calm activation, typed epidemic/meteor payout and expiry; Fair has ordered timed actions; Temple has preaching, tithe and operational relic slots. Market-price mutation, siege/raid/nuclear carriers, Fair exchange/auction/external market/baron actions and five Temple branches retain capability blockers. |
| Tavern | P6A + P9 | P6A complete/conditional: each operational level adds configured recruitment capacity and maximum morale through P2/P3 army readers. Only `tavernMinigame` remains explicitly P9-owned; P9 owns final whole-building closure. |
| Stable, Customs, Sea Port | P6B | Sea Port is coastal-only and empire-capped at four. Fleet effects require player-fleet substrate. |
| Чёрный рынок, Посольство, Внешний рынок | P6B | Config-absent candidates; conditional on authored diplomacy/espionage/trade semantics. |
| Advisors, Grand Advisor, governor персты, capital slot governance | P4C | Complete/Conditional: typed config/save state, exact-once advisor transitions, canonical trump/card-effect consumption, permanent named-перст assignment, shared city accessibility and a capital ownership manifest are live. Grand Advisor access has no player-authored unlock and is exposed only through a source-ID grant funnel for later quest/event ownership. |
| Great houses and unique regional races | P7/P12B review | Review: raw house/race identities are broad narrative/event candidates, not complete executable definitions; quest/event content still needs exact authored branches and designer priority. |
| Coliseum, Тетракорархос and other named capital/map systems without complete actions | P4C/P12B review | Inventoried in `/governance/capital/sites` with owning phase and blocker; do not un-defer until the action, target, cost, timing and cleanup are authored. |

## Content families

| Family | Owner | Status / rule |
|---|---|---|
| Military units, ♥7, combat-spirit gift | P2 | Complete; regression coverage only unless a later owned feature requires an explicit extension. |
| Steel technologies, war buildings, morale relic, conditional ♥A | P3B | Complete/Conditional: four exact Древковые production nodes and the floor-2 relic are live. Eighteen old steel carriers, 51 additional named nodes, Academy, both ♥A faces and missing military buildings remain in the exact P3B manifest. |
| Loyalty/political cards, northern raids, political reforms | P4A/P4B | Northern raids, Принуждение, Геройские похороны, Контроль кузнецов and Теократия are complete. Inverted club 2, both ♥5/♥K faces, Технократия and Городские врата remain deferred for missing whole-contract semantics. Seasons and typed technology-side/hidden-combination substrate are complete; crime remains intentionally absent. |
| Advisors, trump specialization and персты | P4C | Starting Научный/Торговый/Военный advisors and the Grand Advisor role are stable. One pardoned advisor gates its exact reform; trump-suit card effects are critical and matching an active advisor exposes specialization. ♣ is unavailable until a future authored source grants Grand Advisor access. Четвёртый Перст Однорукий Трэвор and Десятый are the only source-stable current governor identities. |
| Vaccination/disease faces, epidemic relic/event | P5 | Complete/Conditional: both `card-spades-10` faces and `relic-epidemic-ward` are live. Raw `персонажи` uniquely maps Дженна to `card-clubs-ace`, but both faces remain deferred for incomplete numeric contracts. City Gates' two epidemic choices are executable typed payloads while the event/reform remain deferred on the missing crime side. |
| Economy technologies, gifts, relics, events, resources, card faces | P6A–P6C | `tech-fair` and `tech-banking` are live action prerequisites from P6A. P6C still owns economy gifts/relic payoffs, events, resources, remaining technologies and card faces; absent definitions are not ignored. |
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
