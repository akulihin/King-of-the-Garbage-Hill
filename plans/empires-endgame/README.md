# Empire's Endgame — confirmed completion program (prompt pack)

This directory is an execution program for the designer-confirmed Empire's Endgame
completion tranche (`/empires-endgame`, `Web/VueClient/src/features/empires-endgame/`). It
does not silently equate every raw sketch with committed scope: every known system and
catalog item has an owner, conditional gate, review status, or explicit exclusion in
`COVERAGE-MATRIX.md`.

Each phase/subphase file is its historical opening prompt for a fresh Codex task using
**5.6 Sol** on the designer's machine. Read it with `COMMON-EXECUTION-CONTRACT.md`.
Subphases were separate change-sets and ran in dependency order; the scheduled program is
now complete through P13.

**Hard requirement**: the raw design export `DiscordExports/Empires_Endgame/`
(+ `DiscordExports/empire_prompt`, the Palach HTML demos, the `EE_TD` sketch) exists only
on the designer's machine and is deliberately NOT committed. If a phase's named export
files are missing from the session's environment, **stop and tell the user** — do not
implement from the compressed model in this README alone. The compression (A below) is
navigation and scope; the raw channels are the source of truth for numbers and semantics.

Baseline verified 2026-07-16: the July audit wave (M54–M61, M83–M91, all fixed) made the
implementation honest — 175 explicit `deferredReason` markers in
`Web/VueClient/public/empires-endgame/game-config.json`; engine + UI refuse to spend on
deferred content (`validateDeferredReasons` / `validateLiveEffects` in
`features/empires-endgame/config.ts`). Counts after completed phases come from the actual
config/tests, not this historical baseline.

Current execution status, including final stabilization completed on 2026-07-21:

- **Phase 0 — complete.** Its prompt is historical; do not amend/re-run it.
- **Phase 1 — complete.** Its prompt is historical; do not amend/re-run it.
- **Phase 2 — complete.** Its prompt is historical; do not amend or re-run it. Phase 3A
  regression-checked the landed baseline before applying its explicit post-P2 hardening.
- **Phase 3A — complete.** Rules identity, bounded replay/history, accessible real-input TD,
  five regional fields and reusable assault landed as one change-set.
- **Phase 3B — complete.** Schema-v4 steel research, four exact spearhead production
  payoffs, equipped cohorts, shared Smithy capacity, Foundry, the morale floor and the
  complete latest-source/deferral ledger landed as one change-set.
- **Phase 4A — complete.** Schema-v5 typed loyalty/reputation, workforce and class gates,
  reversible regional rebellion, bounded political chronicle, exact-once TD-loss pressure
  and northern raids landed as one change-set. Capital Forum and inverted club 2 retain
  explicit blockers for their undefined mechanics.
- **Phase 4B — complete.** Schema-v6 seasons, typed technology sides and hidden
  combinations, complete political reforms, smith specialization and explicit crime/content
  deferrals landed as one change-set.
- **Phase 4C — complete.** Schema-v7 advisor/perst/capital governance, save-envelope v5,
  canonical trump critical effects, exact advisor judgment, permanent named-перст region
  expansion and the capital/review manifest landed as one change-set.
- **Phase 5 — complete.** Schema-v8 typed epidemics/medical configuration, save-envelope
  v6, deterministic settlement/spread/containment, medical buildings and healer/recovery,
  vaccination faces, epidemic relic, hidden plague combination and epidemic UI/QA landed
  as one change-set. City Gates remains honestly deferred on its undefined crime side.
- **Phase 6A — complete.** Schema-v9 domestic-economy configuration, save-envelope v7,
  scheduled Bank debt/default/гонения, calm-turn insurance, ordered Fair activities,
  Temple preaching/relic slots, passive Tavern army hooks and economy UI/QA landed as one
  change-set. Capability-level blockers retain every undefined market, siege, Temple-branch,
  baron and Tavern-minigame behavior.
- **Phase 6B — complete.** Schema-v10 external-economy configuration, save-envelope v8,
  deterministic weighted offers, explicit accept/decline history, trusted-resource trade
  and transfers, Customs tariffs, western Stable consumers, coastal Sea Ports and
  diplomacy UI/QA landed as one change-set. Unauthored unions, relationship actions,
  player fleet/shipbuilding and the three reviewed absent buildings remain explicit.
- **Phase 6C — complete.** Schema-v11 economy-content configuration, save-envelope v9,
  live tithe/material relics, targeted Customs smuggling, horse theft, insurance offers,
  inverted ♦A trade isolation and exact deferred/raw-absent manifests landed as one
  change-set.
- **Phase 7 — complete.** Schema-v12 quest configuration, save-envelope v10, deterministic
  triggers/graphs, persistent memory, atomic choices, mandatory dialogue, journal/overlay,
  all 43 later-demo Палач passages, Golden Idol/Witch typed event bridges and quest QA landed
  as one change-set. The Idol monument branch and the inventoried regional backlog remain
  explicit future-tranche review items after the P12B closure audit.
- **Phase 8 — complete.** Schema-v13 God configuration, save-envelope v11, immutable
  next-draw-first deck memory, serialized limited-use mode, deterministic capped
  consecutive-`бито` intervention/history, isolated authored God dialogue and accessible
  Божественная Милость confirmation with versioned device-local opt-out landed as one
  change-set. No card/gift carrier was un-deferred; inverted Joker remains deferred and P9
  subsequently consumed the mystic/Tavern handoff.
- **Phase 9 — complete/conditional.** Schema-v14 Tavern/mystic configuration,
  save-envelope v12, cross-campaign `0/1/0.33` arrival progression, deterministic Tavern
  replay/settlement, accessible two-section UI, separate ordered mystic zone, 3–7–Т observer
  and atomic Пиковая Дама neighbor pulse landed as one change-set. The broad Tavern
  marker is closed; Maria 2×2/powder legacy, trio passives/leave and Queen appeasement keep
  exact source-gap blockers.
- **Phase 10 — complete/conditional.** Schema-v15 Alchemy configuration, save-envelope v13,
  deterministic four-side Assembly, reagents, arithmetic acceleration, replay-authenticated
  explosion→epidemic settlement, con-scoped laboratory lock, accessible controls and QA landed
  as one change-set. The broad Alchemy marker is closed; Disassembly launch rules, poison/wall
  recipes, science unlock mappings and mutant aftermath retain exact source-gap blockers.
- **Phase 11A — complete/conditional.** Schema-v16 expedition configuration, save-envelope
  v14, typed fortress/zone payloads, canonical per-soldier roster, origin provisioning,
  abstract logistics time, complaint quest and the shared TD assault/settlement path landed
  as one change-set. Opened-zone content, the later veteran bonus, `Жнец`/`пшено`, the
  carrier-less `Рыцарь Хладной Руки` plate exception and inverted ♠3 retain exact source-gap
  blockers.
- **Phase 11B — complete/conditional.** Schema-v17 inventory configuration, save-envelope
  v15, deterministic fixed-step falling-cart packing, immutable origin item ownership,
  exact-once packed-provision settlement, accessible controls, QA replay and the full
  prepare → pack/skip → assault → settlement path landed as one change-set. Packing is an
  optional provision-only route; equipment packing and the perst packer retain exact
  source-gap blockers.
- **Phase 12 — design gate complete/blocked.** The current board image plus every named
  `карты`/`персонажи`/`общее` cross-reference were audited into the P12 executable-rules
  table. The current image contains a black king while the only old no-king/capture-all note
  is lower-priority background; movement, complete roster/instance eligibility, player
  loss/draw, entry/settlement and opponent/information policy are also semantically missing.
  Chess therefore remains absent from config/runtime/UI with no version bump.
- **Phase 12B — complete/conditional.** The complete raw-source→config→owner manifest
  covers live, deferred, reviewed, absent and excluded content. The final audit has no
  `ready-now` rows, so schema 17/save 15 remain unchanged and no executable mechanic/carrier
  is added or un-deferred; three deferred display identities are reconciled. Residual
  semantic/substrate gaps are explicitly routed to a future content tranche.
  Machine coverage now rejects an unowned config carrier, a raw catalog group without a
  disposition, and any final `ready-now` row.
- **Phase 13 — complete/conditional.** Config remains schema 17; stabilization-only save
  state advances to schema 16. The authentic v1 config and campaign/save chain, every
  representative config generation, all four live minigame restore/settlement paths,
  runtime lifecycle state, bounded histories/storage/replay work, real input/accessibility/
  background behavior and the deterministic campaign seed matrix are covered. The P12B
  manifest remains frozen and Chess remains absent behind its recorded semantic gate.
- **Completion program closed.** There is no next prompt in this pack. Any later content
  tranche must begin with explicit designer verdicts and a new scoped plan rather than
  treating P13 as permission to un-defer content.

Scope decisions confirmed by the designer (2026-07-16):

1. **Combat = Tower Defense minigame directly** — no interim abstract combat; army content goes live through TD.
2. **All minigames in scope**: TD, Tetris-alchemy, Tavern + mystic cards, Chess, Tetris-inventory (expedition packing).
3. **Missing numbers**: implement mechanics with configurable defaults in `game-config.json` + maintain a designer-review ledger of every invented number (`docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md`, append-only).
4. **Also in scope**: quest/dialogue engine, God presence (deck memory, anti-bito, God lines, Милость confirmations). **Out of scope**: sound, scripted tutorial staging, lore/secret endings.
5. Everything stays client-side (localStorage), consistent with current architecture.

## Phase order and dependencies

```mermaid
graph TD
  P0[0 complete] --> P1[1 complete]
  P1 --> P2[2 complete]
  P2 --> P3A[3A complete]
  P3A --> P3B[3B complete: steel + military production]
  P3A --> P4A[4A loyalty + reputation + rebellion]
  P3B --> P4B[4B seasons + tech sides + politics]
  P4A --> P4B
  P4B --> P4C[4C advisors + persts + capital governance]
  P4B --> P5[5 complete: epidemics]
  P4C --> P6A[6A complete: domestic economy]
  P5 --> P6A
  P3B --> P6B[6B diplomacy + external trade]
  P6A --> P6B
  P6B --> P6C[6C complete: economy content closure]
  P5 --> P7[7 complete: quests + dialogue]
  P4C --> P7
  P3A --> P8[8 complete: God presence]
  P6A --> P9[9 complete: Tavern + mystics]
  P7 --> P9
  P8 --> P9
  P3A --> P10[10 complete: Tetris-alchemy]
  P5 --> P10
  P3B --> P11A[11A complete: expeditions]
  P7 --> P11A
  P11A --> P11B[11B complete: Tetris-inventory]
  P9 --> P12[12 Chess gate complete: blocked on design]
  P6C --> P12B[12B complete: content closure]
  P10 --> P12B
  P11B --> P12B
  P12 --> P12B
  P12B --> P13[13 complete: stabilization]
```

| File | Ships |
|---|---|
| `phase-00-scaffolding.md` | **Complete/historical.** Config/save migrations, ledger, test discovery. |
| `phase-01-combat-core.md` | **Complete/historical.** Pure combat/damage/counter catalog. |
| `phase-02-td-vertical-slice.md` | **Complete/historical.** Central TD, envelope, army. |
| `phase-03-td-regions-steel.md` | **Complete/historical.** P2 baseline regression, replay/config identity, input/accessibility hardening, five regional TD fields, castle/naval enemy variants, assault. |
| `phase-03b-steel-equipment.md` | **Complete/historical.** Latest steel inventory, schema-v4 research state, exact spear equipment/cohorts, Smithy capacity, Foundry/relic closure and military-building deferral audit. |
| `phase-04-loyalty-seasons.md` | **Complete/historical. 4A:** loyalty, reputation, rebellion, class gates, chronicle, northern raids. |
| `phase-04b-seasons-tech-sides.md` | **Complete/historical. 4B:** seasons, technology sides, political reforms, honest crime deferral. |
| `phase-04c-advisors-persts-capital.md` | **Complete/historical. 4C:** advisor flow, suit/Grand Advisor rules, governor персты, capital slot governance. |
| `phase-05-epidemics.md` | **Complete/historical.** Typed epidemic lifecycle, medical chain, vaccination faces, relic/combo closure and honest City Gates/Pharmaceuticals/Quarantine deferrals. |
| `phase-06-economy-external.md` | **Complete/historical. 6A:** Bank/insurance/Fair/Temple and passive Tavern substrate. |
| `phase-06b-diplomacy-external.md` | **Complete/historical. 6B:** real diplomacy/external trade, Людовик, Stable/Customs/Sea Port, missing trade buildings. |
| `phase-06c-economy-content.md` | **Complete/historical. 6C:** reconciled economy gifts, relics, events, resources, technologies, and card faces; implemented every candidate with complete typed consumers. |
| `phase-07-quests-dialogue.md` | **Complete/historical.** Quest/dialogue graph engine, journal/overlay UI, 43-passage Палач port and Golden Idol/Witch event bridges. |
| `phase-08-god-presence.md` | **Complete/historical.** Deck-memory, deterministic capped anti-bito, isolated authored God lines, Милость confirmation and device preference. |
| `phase-09-tavern-mystic.md` | **Complete/conditional.** Deterministic Tavern, separate ordered mystic catalog/zone, 3–7–Т observer and Пиковая Дама pulse; exact incomplete subfeatures remain blocked. |
| `phase-10-tetris-alchemy.md` | **Complete/conditional.** Deterministic Assembly, reagents, acceleration and configured explosion → typed epidemic; incomplete Disassembly/recipe/aftermath semantics retain exact blockers. |
| `phase-11-expeditions-inventory.md` | **Complete/conditional. 11A:** typed forts/zones, provisions, canonical soldiers/veterans, complaints and shared TD assault; exact residual source gaps remain blocked. |
| `phase-11b-tetris-inventory.md` | **Complete/conditional. 11B:** deterministic falling-cart provision packing, immutable item ownership, optional direct-provision fallback, accessible UI and QA/full-chain coverage; equipment and perst semantics remain blocked. |
| `phase-12-chess.md` | **Gate complete/conditional.** Current sources audited; implementation remains absent until the blocking P12 ledger cells receive complete designer verdicts. |
| `phase-12b-content-closure.md` | **Complete/conditional.** Exhaustive raw/config ownership manifest and machine assertion; no ready-now residue, schema/save change or invented substrate. |
| `phase-13-stabilization.md` | Earliest-to-latest compatibility, all-minigame integration, storage/performance stabilization. |

## P3B handoff

- The compatibility baseline is config schema 4 and campaign/save envelope 3. Old aggregate
  unit counts migrate once into equipped cohorts; schema-v2 regulars alone are grandfathered
  to a frozen `legacy-default` laurel profile without consuming new stock. New regular
  recruitment has no invented pre-steel weapon and remains blocked until a live steel spear
  loadout is researched and produced. Later phases must preserve cohort identity and cannot
  infer loadouts from the latest researched technology.
- Four exact Древковые nodes are live through produced stock and TD. This is deliberately
  not “the full steel tree”: 18 old carriers and 51 latest-source named items remain
  deferred/absent with per-row blockers in `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md`.
- Research and recruitment availability/cost are engine quotes, not UI rules. TD identity
  covers combat, TD, technologies, units, buildings and steel settings; later changes to any
  of those inputs must continue to invalidate mismatched active sessions.
- Foundry defaults and Smithy capacity splits remain open ledger values. Foundry is live in
  non-capital cities, while `capital-sixth-slot` is a visible deferred subfeature because the
  capital's unswappable Temple occupies its unique slot. TechTree and building details expose
  subfeature reasons. Military Academy, ♥A, Ударные, Мастерская, Баллиста and Двор
  Гвардейской Дружины are not follow-up cleanup; their exact missing semantics/substrate must
  arrive through the owning later phase or a designer verdict.
- Compatible old saves migrate deterministically. If an active old-rule minigame cannot
  satisfy current rules identity, the UI rejects reinterpretation and offers an explicit
  discard-and-restart recovery.

## P4A handoff

- The compatibility baseline is config schema 5 and campaign/save envelope 4. Loyalty and
  reputation are typed state; legacy political flags and the schema-v3 pending battle-loss
  queue migrate once and are removed. Later phases must use the mutation/dependency helpers,
  not recreate magic loyalty or reputation flags.
- City loyalty plus the regional modifier feeds one effective-value reader used by the
  configured workforce divisor, building operation, construction, recruitment, production
  and settlement. Smithy also has the separate Мещане class gate. Rebellion is reversible
  and blocks the shared region-access path without entering `destroyedRegionIds`.
- Political history is sequence-numbered, bounded and newest-first in the UI. Canonical TD
  loss identities and event provenance survive restore; later sources must enter through
  `applyLoyaltyDelta`, `applyReputationDelta`, or `consumeBattleLoss`.
- `event-northern-raids` is live. Capital Forum remains bundled-deferred until its progress
  effect is executable, though its temporal positive/negative loyalty reader is tested.
  Inverted club 2 remains deferred for missing numeric effect, scaling and cleanup. Seasons,
  technology sides, reforms and crime remain P4B scope.

## P4B handoff

- The compatibility baseline is config schema 6 and campaign/save envelope 4. Seasons are
  derived from con rather than saved twice. Technology-side selection, disclosure,
  suppression and hidden-combination triggers are persisted exact-once state.
- Принуждение, Геройские похороны, Контроль кузнецов and Теократия are live whole
  contracts. Advisor prerequisites added by P4C must remain real engine dependencies on
  those reforms, not UI badges or replacement flags.
- Crime/public order, Technocracy civil war, City Gates effects, Hearts political faces and
  printing remain explicitly deferred. P5 owns epidemic consequences and must consume the
  existing typed epidemic-policy boundary without inventing crime.

## P4C handoff

- The compatibility baseline is config schema 7 and campaign/save envelope 5. Advisor
  status, transition sequence/source/con and permanent governor assignments are canonical
  state. Schema-4 saves already using ♣ trump are narrowly grandfathered to an active Grand
  Advisor; other old saves retain unresolved starting advisors.
- Научный, Торговый and Военный advisor roles enter one exact `1` pardon / `2` execution
  funnel and gate `reform-theocracy`, `reform-treasury` and `reform-coercion`. Trump card
  effects use the configured critical multiplier through the engine effect path. ♣ is never
  a fresh trump until `advisor-grand` receives an authored, source-ID-bearing access grant.
- A permanent assignment of Четвёртый Перст Однорукий Трэвор or Десятый opens all three
  gated city sites in that region, producing the sourced five-city `2 > 2 > 1` topology.
  Every city action and preview uses `cityAccessBlockedReason`; later phases must not bypass
  it for economy, epidemic, quest or diplomacy targets.
- Forum (P4A), Military Academy (P3B), white stone (P6C), Колизей and Тетракорархос
  (future-tranche review after P12B) remain explicitly deferred in the capital manifest.
  P6B made the raw coastal city markers authoritative for dedicated Sea Port slots. Great
  houses and unique races remain future-tranche review rather than anonymous governance
  flags.

## P5 handoff

- The compatibility baseline is config schema 8 and campaign/save envelope 6. Epidemic
  definitions, protection inputs and medical rules are config data; instances, source
  provenance, containment, spread memory, exact impact con and army recovery are canonical
  saved state. Schema-v7 custom configs migrate with epidemics and medicine disabled.
- `finishEmpireInternal` settles epidemics before it refreshes population/production and
  evaluates fresh famine eligibility. A single typed start funnel serves cards, gifts,
  events, hidden combinations and later alchemy/quest results; epidemic rules identity
  prevents an active instance from being reinterpreted under changed definitions.
- Hospital's authored local 10% reduction and one-con post-battle recovery are live, along
  with Hospital-gated healers. Medical Academy's half-consequence protection, three-con
  free secondary technology cadence and once-per-con 50%-fatal treatment are live. Numeric
  epidemic stages, class weights, healer combat stats and deterministic free-tech choice
  are configurable defaults retained in the designer-review ledger.
- Alchemy was live here only for its plague-combination substrate; P10 subsequently closed
  the broad minigame handoff with a deterministic calibration Assembly. The Granary+Alchemy plague trigger consumes
  P4B's exact-once hidden-combination memory and selects the lowest stable operational
  Alchemy city. Trainers/rats remain deferred without a stable carrier or consequences.
- Both `card-spades-10` faces and the epidemic relic are live. Raw `персонажи` uniquely
  identifies `card-clubs-ace` as Дженна, but both faces remain deferred because their
  numerical disease/birth/autonomy effects are incomplete. Quarantine and Фармацевтика
  are validated protection hooks only; no carrier ID was invented, and the farm-production
  tradeoff remains deferred.
- City Gates' seal/open epidemic choices are typed and executable, including exact-once
  containment and already-spread behavior, but the bundled reform/event remain unavailable
  because the light crime/public-order contract is still undefined. No generic epidemic
  recruitment or facility lock was invented; stage fields exist and bundled values are
  explicitly empty/false.

## P6A handoff

- The compatibility baseline is config schema 9 and campaign/save envelope 7. Domestic
  economy definitions are config data; loan/installment identity, insurance provenance,
  Fair activity/cooldown memory, persecution, Temple assignments and bounded obligation
  histories are canonical saved state. Schema-v8 custom configs migrate with the section
  disabled, and legacy saves migrate claimed relics without leaving old always-on flags.
- Bank principal and every installment freeze the current trusted gold income at
  origination. Scheduled settlement, manual repayment, default and гонения share one typed
  obligation model and P4A's loyalty/reputation/chronicle funnels. Later price systems may
  consume the retained persecution market blocker, but must not recreate debt as a flag.
- Insurance activates after three calm settlements and covers only typed epidemic and
  meteor incidents. Raid, nuclear and `окружение` remain explicit capability blockers;
  neither a home TD loss nor enemy naval categories are a siege proxy. P11 must supply
  real incident state before adding those coverages.
- Fair progression is `Карнавал → Подкуп бродячих артистов → Пустить в Империю циганский
  табор → барон` with persisted cooldown/activity identity. The baron is an integration
  point only; exchange, auctions, external market and baron trade remain deferred to their
  named owners.
- Temple preaching/tithe and operational relic slots are live. Relic flags are effective
  only while assigned to an accessible operational Temple; already-claimed legacy relics
  migrate without duplicate one-time resolution. P6C subsequently delivered the two
  economy relic consumers without changing the slot lifecycle. Tavern levels feed the
  canonical army recruitment and maximum morale readers. P9 subsequently closed the broad
  `tavernMinigame` handoff and retained only its exact incomplete capability blockers.

## P6B handoff

- The compatibility baseline is config schema 10 and campaign/save envelope 8. External
  actor relationships, offer lifecycle/history, Customs trade memory and city-transfer
  history are canonical saved state. Schema-v9 custom configs migrate with external
  economy disabled; schema-v7 saves receive the disabled/default external state without
  changing prior campaign outcomes.
- Offer refresh is serialized-RNG weighted selection without replacement. Stable instance
  IDs, a canonical rules digest, expiry and bounded accepted/declined/expired history
  prevent reload rerolls, reinterpretation or duplicate resolution. Every acceptance
  rechecks phase, stock, expiry, relationship,
  reputation, region access, technology/dependencies and the existing city/shared/Temple
  resource payment plan.
- Customs tariffs, Sea Port trade price/knowledge effects, Bank persecution pricing,
  `tech-merchant-guilds` and Compass transfer timing are one config-driven quote path.
  Stable is western-livestock-only with Farm II; the live knight requires a same-city
  Stable and spends horses. The exact review defaults remain in the designer ledger.
- Seven cities with authored coastal governance metadata receive a dedicated maritime slot;
  placement and upgrade share the coastal rule and the empire-wide cap of four. No player
  fleet was inferred from enemy naval TD, so fleet, shipbuilding and expedition-return
  capabilities remain blocked.
- Чёрный рынок, Посольство and Внешний рынок remain config-absent reviewed identities. No
  unions, relationship-changing actions or refusal consequences were invented. P6C
  subsequently consumed the Customs trade marker without changing the external offer core.

## P6C handoff

- The compatibility baseline is config schema 11 and campaign/save envelope 9. The new
  canonical state owns bounded event decisions, one-shot Customs policy, horse-theft
  disable/cooldown/hostile pact, offered insurance cities and stable event targets. A
  genuine schema-v10 config receives this section disabled; schema-v8 saves receive empty
  lifecycle state.
- `relic-tithe` changes the P6A preaching quote with one final floor; the material relic
  removes Smithy iron and Stable horses only through canonical construction helpers. Both
  contribute only from an accessible operational Temple slot.
- Customs smuggling requires real P6B trade provenance and freezes its Customs city.
  Stop/tax consequences settle once during the next con and expire. Horse theft requires
  the P6A барон plus an operational Stable; hunt disables, ignore cools down and deal
  persists one hostile actor while paying through the horses ledger. The insurance event
  starts the existing P6A contract rather than duplicating coverage state.
- Inverted ♦A scales its owned temporary flags, blocks external offers, blocks cross-region
  transfers and cross-region Temple donors, still permits same-region transfers, and
  cleans up at the normal card boundary. Normal ♦6 remains deferred because monopoly,
  cartel and Дон state do not exist.
- The exact deferred candidates are the five P6C gifts, lumber concession, white-stone
  event/resource, carpentry and normal ♦6. Fish-current subbranches, meteor radiation,
  South resort/watermills, white-stone consumers and monopoly/Дон remain config-absent
  future-tranche review rows rather than anonymous live content.

## P9 handoff

- The compatibility baseline is config schema 14 and campaign/save envelope 12. Genuine
  schema-v13 custom configs receive an empty mystic catalog and disabled Tavern scaffold;
  legacy saves receive empty Tavern/mystic lifecycle state without retroactive spawns.
- Tavern sessions reuse the generic minigame envelope and are immutable
  `plan + seed + commandLog` replays with a Tavern-specific rules digest. Settlement spends
  gold and writes cohorts, spirits, rumor audit and result history exactly once; a rejected
  or error replay cannot mutate the campaign.
- Cross-campaign completed-run ordinals live in
  `empires-endgame:profile:tavern:v1`, outside campaign saves. First run is absent, second is
  guaranteed and later runs use `0.33` at con four. Character identity is not inferred;
  exact reset/counting questions remain in `P9-01`.
- The standard catalog remains exactly 52 suited definitions plus Joker. Mystic instances
  use a separate authoritative ordered zone and never enter Durak legality/trump/refill/
  winner accounting. After a persisted Maria victory, standard ranks 3–7–Т spawn a distinct
  Queen; her configured start-next-con pulse atomically toggles immediate mystic neighbors.
- `building-tavern` has no broad deferral. The complete residual manifest is
  `maria2x2`, `mariaGunpowderLegacy`, `mysticTrioPassives`, `mysticLeaveAction` and
  `queenAppeasement`, plus face-level reasons on Лист, Лорик and Анатолий. Later phases
  must not treat these typed seams as proof of missing semantics.

## P10 handoff

- The compatibility baseline is config schema 15 and campaign/save envelope 13. Genuine
  schema-v14 custom configs receive a disabled empty Alchemy scaffold; schema-v12 saves
  receive an empty bounded explosion summary without changing earlier outcomes.
- Alchemy reuses the generic immutable `plan + seed + commandLog` envelope. Fixed logical
  ticks own spawn, movement, nearest-piece control, lock, reagent use, triangular
  acceleration and terminal results. Reload increments the attempt and replays from the
  frozen plan; active config/rules mismatch is rejected.
- The live bundled calibration recipe is Assembly only. Tetrominoes approach from all four
  sides after serialized 2–5-second delays, may overlap in flight, and cannot move outward
  or pass the central construction. Inward input moves ×3. Remove-color, add-gray and
  acceleration-reset reagents each have one configured charge.
- Exceeding, not merely reaching, the configured 400% threshold creates a trusted plague
  request at the originating laboratory through P5's epidemic funnel. Severity is frozen in
  the plan, the canonical building-interaction lock prevents a same-con retry, and restore
  cannot clear or duplicate either consequence.
- `building-alchemy` has no whole-item or `alchemyMinigame` marker. The complete residual
  manifest is `disassemblyRules`, `poisonWallRecipes`, `scienceRecipeUnlocks` and
  `mutantAftermath`. The pure engine has a complementary remove-on-contact mode, but the
  bundled Disassembly launch remains unavailable because the raw source gives conflicting
  control and success sketches. No later phase may infer poison inputs/outputs, science
  recipe mapping or a mutant event/TD lifecycle from these seams.

## P11A handoff

- The compatibility baseline is config schema 16 and campaign/save envelope 14. Genuine
  schema-v15 custom configs receive discriminated map payloads plus a disabled empty
  expedition scaffold; schema-v13 saves receive canonical soldier instances and empty
  expedition state without retroactive launches, wounds or opened zones.
- The unchanged `map-south-fortress` is the only live fort and opens the direct-provision
  planner for `expedition-south-fortress`. Launch freezes stable soldier IDs/loadouts,
  withdraws origin-region food exactly once, spends preparation days and uses abstract
  effective travel cons for requirements, risk and complaint history. Abort never refunds.
- Военная логистика, Снабженцы and normal ♠3 are consumed by the same authoritative
  planning quote. The inverted ♠3 remains deferred because confusion cleanup and scope are
  incomplete; P11B adds packed item identities/efficiency only to the frozen provision plan
  and does not replace it with an empire-wide flag.
- Expedition assault is an ordinary rules-identified TD replay with purpose `expedition`.
  Its common settlement path owns casualties, first/second wounds, veteran threshold,
  recovery, reward/zone/complaint exact-once guards and bounded digest-compacted history.
- The exact residual manifest is `opened-zone-content`, the null later-battle veteran bonus,
  the stable raw `Рыцарь Хладной Руки` identity without a current unit/passive/acquisition
  carrier, `Жнец`/`пшено` semantics and every fort beyond South. West and swamp profiles are
  configured combat inventories, not live routes.

## P11B handoff

- The compatibility baseline is config schema 17 and campaign/save envelope 15. Genuine
  schema-v16 custom configs receive a disabled empty inventory scaffold, so migration never
  silently activates bundled shapes or packing behavior.
- Packing snapshots the selected roster, origin-region provision item IDs, shapes, amounts,
  seed and rules identity into the shared minigame envelope. The falling runtime remains
  derived. Restore replays the immutable plan with `attempt + 1`; stale, duplicate, malformed
  or rules-mismatched results cannot mutate origin resources.
- The bundled `10×14` field has an eight-row cart, deterministic gravity/rotation/collision,
  a tick/command cap, bounded result history and hidden-tab pause/catch-up limits. Only packed
  item IDs are withdrawn at verified packing settlement; unpacked items remain in their
  canonical cities. Score and efficiency describe that result and do not create a second
  expedition modifier: P11A's actual withdrawn provision amount remains the death-risk input.
- Packing is optional because the source never states it is mandatory or defines a skip
  penalty. The confirmed skip uses P11A's existing direct-provision path behind an explicit
  confirmation. Aborting packing aborts the expedition attempt without deleting provision;
  it cannot be restarted as the same attempt.
- The exact residual inventory manifest is `equipmentPacking` and `perstPacker`. The raw
  export contains no linked inventory/cart attachment, equipment instance mapping, packer
  bonus or required-entry rule, so later work must not infer those contracts from the joke or
  from the provision implementation.

## P12 handoff

- The P12 readiness gate was executed against the current `шахматы` attachment and every
  named cross-reference. Its complete executable-rules table and stable source identities
  are in `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md`; P12B and P13 should use that audit rather
  than the older compressed Chess summary.
- The current source image visually shows an `8×8` board, a full black roster including a
  king on `e8`, candidate player card placements, and a marked `g8` knight. The lower-priority
  `ZBS MAKING` note instead says the enemy has no king and capture-all is victory. This
  unresolved conflict blocks roster, check and termination semantics.
- Legal movement/capture/special moves, complete player and enemy roster identities,
  current-instance zone eligibility, Антон's full shared-controller cadence, player
  loss/draw/repetition, entry/reward/failure/abort/card consequences, and opponent/privacy
  policy are also missing semantics. They are not tunable-number gaps and may not receive
  standard-Chess defaults.
- Config schema remains 17 and campaign/save envelope remains 15. There is no `/chess`
  carrier, fifth runtime minigame kind, player/debug entry, Builder surface, QA fixture or
  placeholder engine. `GameVersion` is unchanged because the gate shipped no game behavior.
- P12B may proceed with Chess unavailable and should preserve/assert that isolation. A later
  Chess implementation must first receive complete designer verdicts for `P12-02` through
  `P12-09`, then execute the conditional half of `phase-12-chess.md` as its own change-set.

## P12B handoff

- Config schema remains 17 and campaign/save envelope remains 15. The closure sweep found
  no `ready-now` row, un-deferred no marker and introduced no new content substrate. Its
  exact boundary moves from 159 to 165 `deferredReason` properties because normal
  `card-clubs-2`, normal `card-joker-jester`, inverted `card-diamonds-6`, normal
  `card-diamonds-ace`, inverted `card-spades-5` and inverted `card-spades-8` now expose
  missing or partial empire passives as explicit blockers; 34 entries remain across 15
  `deferredSubfeatures` arrays, plus one each of the Grand Advisor `accessDeferredReason`,
  Tavern Maria `encounterDeferredReason` and expedition veteran
  `laterBattleBonusDeferredReason` markers. The surviving binary effects on ♥7 and ♦A are
  capped at level 1 so card-improvement points cannot be spent on no-op levels.
- `Web/VueClient/src/features/empires-endgame/content-coverage.ts` enumerates every owned
  config carrier, including root, city-slot, quest-stage/node, unit-loadout, loyalty-gate and
  nested TD IDs. `content-coverage-manifest.ts` freezes all 33 raw source entries, a
  1,149-message JSON-export spine, residual review/out policy, stable identities, owners,
  consumers, evidence, final dispositions and exact raw→config link counts.
  `__tests__/phase12b.spec.ts` fails closed on an unowned nested carrier, a changed marker,
  source-message drift, a removed link/disposition, any final `ready-now` row and Chess
  leakage. Its maintained projection fingerprint also catches same-count message-ID and
  valid-but-wrong-link substitutions. Raw semantic extraction remains a manual reviewed
  artifact; the source spine/fingerprint is an ownership/drift boundary, not automatic
  concept discovery. The extensionless prompt, population PNG and parent-message
  `Palach`/`Palach2`, `EE_TD`, `EE_chest` and text attachments were manually inspected but
  are not content-digested by the 1,149-ID spine.
- The final configured inventory is 106 standard card faces (`10` live / `96` deferred),
  with Антон де Лорян, Мария Брауз and Конрад Лоуренс reconciled to their exact current
  definition identities without un-deferring either side;
  17 gifts/relics (`12` / `5`), 9 resources (`7` / `2`), 22 buildings (`20` / `2`),
  5 live units, 63 technologies/reforms/steel definitions (`42` unmarked / `21`
  marker-deferred), 10 events
  (`7` / `3`) and 4 live quests. Golden Idol retains one deferred choice. The separate
  mystic catalog has four definitions (`1` live / `3` deferred); combat equipment has 32
  (`22` unmarked / `10` deferred), while only eight have a reachable campaign acquisition
  path and the other 24 remain blocked support definitions. TD grade-choice sets have 20
  (`10` / `10`). The exact machine inventory has 1,112 config carriers (`823` live / `142`
  blocked-semantic / `13` blocked-substrate / `134` review) and 355 stable raw semantic
  identities (`18` live / `140` blocked-semantic / `56` blocked-substrate / `126` review /
  `15` out) across 33 source entries and 1,149 frozen JSON message IDs. The population image
  supplies the `500,000` total, an approximately half-area nonworking band and several class
  labels. `общее` message `1511700113230397530` supplies Крепостные / Мещане / Дворяне /
  Духовенство, while `застройка` message `1511796892000850051` links крестьяне→фермы,
  мещане→ремесло and духовенство→храм. Together they own the five-class topology; the bundled
  `250,000` / `200,000` / `30,000` / `10,000` / `10,000` class amounts are configuration
  defaults rather than image-derived values. P6B-03 represents trade-harbor
  message `1440660308434882642` through the live favorable Sea Port quote proxy, while a
  separate blocked-semantic row retains the literal land-tax/water-zero contract pending
  transaction-medium classification, exemption scope and Customs interaction. The
  raw inventory explicitly retains construction message `1515084481201967266` and the seven
  categories `Имперцы — легионеры/гвардия`, `Пограничные феодалы`, `Региональные феодалы`,
  `Наемники`, `Дружина`, `Ополчение`, `Солдаты`; P3A's 12 semantic
  and 3 substrate residuals; P3B's 51 absent steel nodes, 8 absent methods/gear prerequisites
  and 3 absent military buildings; current technology/doctrine gaps, including the review
  link from source `Плотничество` to differently modeled `resource:carpentry`, the one-off
  `Госпиталь` relation to live `Больница`; and exact
  P7/P8/P11/card seams. The exact P8 anti-bito line from `zbs` message
  `1287539702731247656` is linked live. The 45 selected war named-message samples sit beneath
  the complete 230-message war source spine and are not claimed to be an exhaustive unit
  catalog.
- TD, Tavern, Alchemy and Inventory remain the four live minigame lifecycles. Chess remains
  blocked by the P12 semantic gate and absent from config, runtime, Builder, QA and UI.
  Great houses/races, incomplete capital actions, the quest backlog, missing economy
  branches and all other raw-absent sketches are future-content-tranche review or blocked
  rows, not P13 work.
- P13 receives this frozen manifest for compatibility, storage, performance and integrated
  campaign stabilization. Any later content work must first update the manifest and obtain
  the recorded designer verdict; it may not use P13 to create missing substrate.
- The closure assertion/review change-set advances `GameVersion` once to `5.0.28`; config
  and campaign/save schemas remain unchanged.

## P13 handoff

- Config remains schema 17. Campaign/save envelope advances from 15 to 16 only for
  stabilization state: canonical minigame settlement sequences/watermark, epidemic-history
  compaction and consumed-battle-loss compaction. The v16 storage key reads all supported
  older keys. Import requires matching source envelope/state versions and preserves that
  old state version until engine normalization, closing the migration path that previously
  stamped a legacy state current before its version-conditioned fills ran.
- The checked-in authentic pre-P0 v1 configuration and representative custom definitions
  from every schema generation exercise direct and explicit stepwise v1→v17 migration,
  non-mutation, idempotence, disabled/partial sections, dangling references and future-v18
  rejection. The authentic pre-P0 campaign fixture also retains production-engine save
  checkpoints v1→v15; direct v1 restore and every checkpoint independently normalize to the
  same v16 state without input mutation. Save v1→v16 coverage includes the previously shipped
  runtime lifecycles and every live minigame: TD defense/assault, Tavern, Alchemy and
  Inventory. Restored active sessions preserve immutable plan/rules identity, increment
  attempt once, retain abort and stale-rules guards and settle through deterministic replay
  exactly once. Current v16 restore rejects missing quest, quest-runtime, expedition or
  loyalty roots and active-session origin/context that contradicts its kind, plan or
  expedition lifecycle.
- Every new live minigame session owns a monotonic sequence and canonical identity.
  Settlement advances its watermark only after canonical effects succeed; retained results
  share the smallest configured TD/Alchemy/Inventory tail (32 bundled), while compacted
  canonical sequences and up to 64 legacy non-canonical IDs preserve the exact-once guard.
  A campaign rejects skipped/stale sequence identities and stops at 4,096 settlements.
- `stabilization.ts` centralizes the shipped ceilings: 10,000 QA actions/ticks, 512 commands,
  16 catch-up ticks per frame, 300 seconds logical replay, 256 result/history entries,
  4,096 board cells, 256 immutable plan items, 64 Tavern offers and a 512 KiB serialized
  save. Bundled TD/Alchemy/Inventory baselines remain below them. Ended epidemics retain 32
  readable records, while battle-loss consumption and expedition complaints each retain 64
  recent IDs, with older detail folded into digests or attempt watermarks; the existing
  chronicle, economy, God, quest, mystic and expedition histories keep their bounded tails
  and compaction provenance. Autosave and manual export share the bounded serializer and a
  persistent `role="alert"` on failure. Startup engine-validates stored candidates
  newest-to-oldest before choosing one, so an invalid newest key does not mask an older
  recoverable campaign.
- Alchemy and Inventory global keyboard shortcuts no longer consume Enter/Space/arrow input
  from focused native controls. Their shared labelled abort `alertdialog` pauses simulation,
  blocks game input, starts on the safe continue action, traps Tab and restores trigger
  focus/frame origin on cancel. Fake-clock component tests cover pointer/keyboard commands,
  semantic text alternatives, focus, background-tab backlog discard and modal pause.
- QA adds a restorable active `inventory-packing` fixture and exposes result retention plus
  the settlement watermark in its digest; the visible QA strip includes the readable result
  count. TD Cypress proves fast resolve changes that count from zero to one. The integrated
  campaign uses one continuously accumulated engine for `phase13-alpha`, `phase13-beta`
  and `phase13-gamma` under the 10,000-action cap. Every run owns 13 ordered checkpoints
  and one six-result log; beta repeats to the exact final snapshot, digest and result. The
  runner stages Maria's unavailable encounter prerequisite only, then crosses the live
  3→7→Ace observer through real card actions and verifies the Пиковая Дама spawn/history.
  Final-day Inventory settlement persists its watermark before normal next-con wave
  scheduling. The older traced autoplay matrix remains
  `qa-seed-1`, `qa-seed-2`, numeric `1701`, with `qa-repeatable` reproducing its trace,
  event sequence and snapshot.
- P12B's exact inventory remains unchanged: 1,112 config carriers, 355 stable raw semantic
  identities, 33 source entries and the 1,149-message spine, with no `ready-now` row. TD,
  Tavern, Alchemy and Inventory remain the only runtime minigame kinds. Chess still has no
  config carrier, runtime/UI/QA/Builder entry or placeholder engine until `P12-02`–`P12-09`
  receive complete designer verdicts.
- Final verification is the complete `tools/test-empires-endgame.sh` unit/browser gate,
  frontend production build, changed-doc verifier, passive audit, backend build and
  simulation safety net. Exact measured timings/save-size baseline and command outcomes
  belong in the final change-set handoff rather than this maintained behavior contract.
  The stabilization change-set advances the current sequential `GameVersion` once from
  `5.0.31` to `5.0.32`.

## How to execute the completed prompts

The scheduled program is complete through P13. Historically, each phase/subphase ran as one
fresh Codex task and one change-set. Do not combine
`3A`+`3B`, `4A`+`4B`+`4C`, `6A`+`6B`+`6C`, `11A`+`11B`, or `12`+`12B` in one prompt.
Finish, review, and let the user commit (or otherwise cleanly isolate) one change-set before
starting its dependent prompt. The lettered files are dependency boundaries, not chapters
to paste into one large task.

The graph permits some separate-worktree parallelism, but these phases commonly touch the
same config, types, engine, docs, and version file. The completed chain through stabilization
is:

`7 → 8 → 9 → 10 → 11A → 11B → 12 → 12B → 13`.

There is no remaining single-worktree prompt in this completion program. The template below
is retained only as the execution shape for a separately approved future tranche; it does
not authorize one.

Use this opening prompt, replacing `<phase-file>` with exactly one file:

```text
Execute plans/empires-endgame/<phase-file>.md completely as one change-set.
Read AGENTS.md, plans/empires-endgame/README.md,
plans/empires-endgame/COMMON-EXECUTION-CONTRACT.md, and
plans/empires-endgame/COVERAGE-MATRIX.md first. Treat the selected phase file as
authoritative for phase scope and ownership, its named raw design sources as authoritative
for mechanics and numbers, and the common execution contract as binding.

Verify prerequisites and prior-phase handoffs from the current code, tests, documentation,
migrations, and git status. Preserve unrelated work. Do not reopen completed phases or begin
later subphases. In Ultra, use subagents only for bounded audits/tests and retain one writer
for shared implementation files.

Complete the implementation, affected documentation, sequential GameVersion patch bump,
commit-message preparation, coverage/deferred-ledger updates, and all feasible verification
gates. Do not commit or push.
```

## Binding execution and coverage contracts

- `COMMON-EXECUTION-CONTRACT.md` owns source hierarchy, preflight, honest un-deferral,
  replay/config identity, migration, accessibility, QA, docs, version, and git rules for
  every phase in this completed program. Phase prompts contain only phase-specific deltas.
- `COVERAGE-MATRIX.md` owns program scope and routing, including design items absent from
  current `game-config.json`. A phase may update the matrix but may not silently omit an item.
- Phase prompts override this overview only for a narrower, explicitly named contract.

## Architecture cornerstones

- **One shared envelope for every implemented minigame**: TD, Tavern, Alchemy and Inventory use `EmpiresMinigameSession {kind, plan, seed, attempt, origin, rulesIdentity}` / `EmpiresMinigameResult`; campaign methods `beginMinigame` / `resolveMinigame` / `abortMinigame` (abort = authored penalty, no save-scumming). A reload restarts from immutable `plan + seed` with `attempt + 1`. Chess remains absent until its P12 semantic gates are resolved; if implemented later, it must join this same lifecycle rather than introduce a parallel one.
- **Fixed-timestep sims** (deliberate divergence from last-chances' rAF-delta loop at `features/last-chances/engine.ts:724`): `step()` advances exactly `tickMs`; battle result = pure `f(plan, seed, commandLog)`; headless QA runs the *same* sim — a single resolution path. The rAF loop only accumulates time and interpolates rendering.
- **Replay identity**: plans embed resolved simulation data or carry an immutable config/rules digest; commands use tick/turn indices. Config changes cannot mutate an active session.
- **Shared combat module** `features/empires-endgame/combat/` (damage types, armor classes, counter matrix, equipment catalog) consumed by TD, expeditions, and events; steel techs pay off as `equipment` entries with tech prerequisites.
- **State discipline**: reputation and loyalty are first-class typed political state. Keep flags only for temporary/configured scalar modifiers; seasons use a pure `currentSeason()`. Other per-entity/lifecycle data (epidemics, army/morale/veterans, quests, the minigame session) also remains typed.
- **Config migrations**: `migrateEmpiresConfig` chain applied before `validateEmpiresConfig` (replacing the hard `schemaVersion !== 1` throw), modeled on `migrateLastChancesConfig` (`features/last-chances/config.ts:55`). Save migrations continue the `validateAndCloneSnapshot` field-normalization style; bump the envelope version only for semantic moves.
- **Compatibility sequence**: Phase 0 moves config v1→v2 and adds disabled future
  sections. Later phases backfill additive section fields before validation or advance to
  the next sequential config version when semantics demand it. Phase 2 moves campaign/
  envelope v1→v2 for the real minigame phase; P3A moves config v2→v3; P3B moves config
  v3→v4 and campaign/envelope v2→v3 for equipped cohorts; P4A moves config v4→v5 and
  campaign/envelope v3→v4 for typed political state, consuming legacy loyalty/reputation
  flags and the pending TD-loss queue exactly once. P4B moves config to v6; P4C moves config
  to v7 and saves to v5; P5 moves config/save to v8/v6; P6A moves them to v9/v7 for typed
  domestic economy and migrates old relic flags into Temple-owned activation; P6B moves
  them to v10/v8 for typed external relationships, offers, trades and transfers; P6C moves
  them to v11/v9 for economy-content events, exact targets and bounded decision history;
  P7 moves them to v12/v10 for quest graphs and dialogue memory; P8 moves them to v13/v11
  for God configuration, cosmetic RNG, anti-bito history and deck-memory usage; P9 moves
  them to v14/v12 for Tavern progression/replay and mystic-zone lifecycle; P10 moves them to
  v15/v13 for Alchemy; P11A to v16/v14 for expeditions; P11B to v17/v15 for Inventory; P12
  and P12B keep those versions; P13 keeps config v17 and advances saves to v16 for bounded
  stabilization identity/compaction state. Never reuse a hard-coded version if the executed
  repository is already farther ahead.
- **Component homes**: `src/components/empires-endgame/` (`TdBattle.vue`, `DialogueOverlay.vue`, `QuestJournal.vue`, `DeckMemoryPanel.vue`, `MinigameAbortDialog.vue`, …); feature modules live under `features/empires-endgame/` (`combat/`, `td/`, `quests.ts`, `alchemy/`, `tavern/`, `inventory/`). Chess deliberately has no module until its P12 gate is resolved.
- `engine.ts` remains a large central class; extract internal modules (an `engine/` dir) only when a scoped change already touches that cluster, never as a big-bang refactor.

## Standing per-phase gate

Use `COMMON-EXECUTION-CONTRACT.md` §7. In particular: focused tests, the complete Empires
gate, production build, changed-doc verification, exact remaining-deferred manifest,
sequential version bump, and commit-message proposal are mandatory for each subphase.

## Verification contract (program-wide)

- Focused unit tests per phase: `pnpm --dir Web/VueClient run test:empires`.
- Final named QA scenarios include `battle-defense`, `battle-assault`, regional defenses,
  `epidemic-outbreak`, `quest-dialogue`, `mystic-tavern`, `alchemy-experiment`,
  `expedition-planning`, `inventory-packing`, `anti-bito`, governance and economy fixtures;
  actions include `resolve-minigame` (with scripted policies) and `advance-dialogue`. The QA
  harness lives in `features/empires-endgame/qa.ts` (`digestEmpiresQaState`, trace + stall
  diagnostics, bounded autoplay loop).
- Cypress specs drive settlement via `?qa=1&scenario=…&seed=…`; deterministic component
  tests must also prove real keyboard/pointer input reaches the production simulator.
- TD determinism gate: the same `(plan, seed, commandLog)` run twice yields an identical result digest (asserted in `td/engine.spec.ts`); headless autoplay terminates under a tick cap for 3 seeds × 3 policies.
- Standing integration test: full-campaign autoplay across battles/quests without stalls (the digest/trace stall detector already exists in `qa.ts`).

## Key repo anchors (verified 2026-07-16 — re-locate by symbol name if lines drift)

| What | Where |
|---|---|
| Engine class / restore | `features/empires-endgame/engine.ts:208` `EmpiresEndgameEngine`; `validateAndCloneSnapshot` `:1002` |
| Bout/phase pipeline | `resolveBout` `engine.ts:1122`; `startEmpirePhase` `:1193`; `startNextCon` `:1371` |
| Dependency gate | `firstMissingDependency` `engine.ts:1477` |
| Live ♥7-inverted executor | `militaryArson` reads `engine.ts:1069`, applied `:1240` |
| Deferral contract | `validateDeferredReasons` `config.ts:119`; `EMPIRES_LIVE_FLAG_ALLOWLIST` `config.ts:150` (20 flags); `validateLiveEffects` `config.ts:173` |
| Schema hard-checks | config `schemaVersion !== 1` throw `config.ts:242`; 53-card check `config.ts:246`; save envelope checks `persistence.ts:8-12` |
| State model | `EMPIRES_PHASES` `types.ts:18`; `EmpiresCampaignState` `types.ts:513`; envelope `types.ts:533` |
| QA harness | `digestEmpiresQaState` `qa.ts:464`; autoplay/stall loop `qa.ts:814+`; fixtures for pending-take/divine-gift/targeting/empire/destroyed-west |
| UI | page `src/pages/EmpiresEndgame.vue` (~1.7k lines); components in `src/components/empires-endgame/` (incl. `EmpireMap.vue` object editor, `TechTree.vue` node editor, `BuilderDrawer.vue`) |
| Patterns to mirror (not modify) | `features/last-chances/`: `migrateLastChancesConfig` `config.ts:55`; rAF-delta loop `engine.ts:724` (what sims must NOT copy) |
| Test wiring | `Web/VueClient/package.json` scripts `test:empires` / `test:empires:e2e`; `tools/test-empires-endgame.sh` |
| Docs | `docs/WEB-CLIENT.md` §12B (note: its "Ten bouts form a con" contradicts config `boutsPerCon: 3` — ledger item #14) |

---

## A. Design source model (compressed from the export; channel names in parentheses)

### A1. Core loop (empire_prompt, общее)
1. **Durak vs God** — 53-card deck (2..A ×4 + Joker "Шут"); card = suit, rank, name, time-cost, value, art, passive; every card has an **inverted form** (taken from God's attack → gothic art, negative mirror passive). Kon = several bouts (configurable); configurable scoring → points; points spent on card upgrade or un-inverting. After kon → **divine gift** draft 1-of-3, value scales with performance.
2. **Empire phase**: hand cards' passives apply; budget 59 days minus hand time-costs; actions cost days; random events. Loop until durak ends or empire dies. 1 kon ≈ 2 empire months.
3. Meta: scripted 3-stage tutorial → roguelike (OUT OF SCOPE); meta-ladder region lore → advisor finales → secret ending (OUT OF SCOPE).

### A2. Cards (карты, персонажи, таверна; ZBS for older card drafts)
- Suit themes: ♥ королевская семья/влияние; ♠ прогресс/науч.советник; ♦ экономика+дипломатия/торг.советник; ♣ народ. Trump crits; trump+advisor same suit = min-max. Крести trump only when Grand Advisor opened.
- Documented cards (details in channels): К♥ Легитимность Томаса; В♥ похищение сына; Т♥ Mr.G/дед-квесты; 7♥ Зазывалы (inverted = Поджог: −1 lvl военного здания, лок на ход, юнит-потери) [executor exists, face deferred until army]; Т♦ банкир/валюта; 8♦ подати; 10♦ сателлит; 3♠ карты мира/логистика; 5♠ Образование; 8♠ 200% ферм; К♠ Конрад (очки улучшений); Д♠ Мария Брауз (порох); В♠ Антон де Лорян (интел; необнаружимый переворот; работает в любой руке); Т♣ Дженна (рождаемость/болезни); 8♣ Стандарт питания ±50%; категории Экономика/Влияние/Народ/Прогресс с нумерованными парами светлая/тёмная.
- Lifecycle: бито = потеря (персонаж выбыл); отдано богу = неактивен; перевёрнута в руке = вредит. Draw from deck → +1 upgrade. Card upgrade example: effect persists 1 round after loss.
- Mystic cards (таверна): Лист/Лорик/Анатолий — без масти/ранга, возвращаются сами перевёрнутыми; Пиковая Дама (спавн после Марии Брауз + комбо 3-7-Т на столе) периодически переворачивает соседние карты.
- God behaviors: face-up shuffle → Том запоминает порядок колоды (deck-memory feature); anti-bito (возврат части бито в колоду если игра кончается слишком быстро); реплики бога; подтверждение траты Божественной Милости с "не показывать больше".

### A3. Map, regions, cities (застройка, дома, регионы, лор)
- 5 regions (N лёд / W лес / S пустыня / E болото / C Тетракор) + 10 subregions; fixed oblique camera; minimap. Resource asymmetry: W/E мало шахт; N/S нет лесопилок; W лошади; S нет воды (кактусовые фермы).
- 13 cities: 2/region периметр (500k) + 4 Тетракор (3M) + столица (8M); перст-губернатор → доп. точки застройки (2>2>1); морские города → слот Морской порт (max 4); столица: Тетракорархос, Форум, Колизей, Военная академия, шахта белого камня.
- Region great houses ×4 с шаблоном черт + выходка-ивент; уникальные расы; региональная лояльность → восстание; late-game предательства → региональные жертвы-дары.

### A4. City economy (застройка, здания, экономика, общее)
- Slots: ферма, лесопилка, шахта, военная кузница (Оружейник/Бронник), казарма, unique 6th, municipal. Food/pop rules (implemented). Busy-locks (implemented for mine/lumber). Seasons: лето/зима ×2 лимит еды; парники выравнивают. 50% населения не работает; worker shortage shutdown mine→lumber→farm by level (implemented).
- **Loyalty**: −9..+9 city + region modifier; effective workforce divisor −9→/19, 0→/9, +9→/1; отрицательная лояльность выключает здания; классы (крестьяне/мещане/дворяне/духовенство) привязаны к зданиям (кузня требует лояльности мещан). **Reputation**: −9..+9, gates trade/unions.
- **Army** (застройка message-d27f1e9af25bf194.txt): 7 типов (регулярка-подписка, пограничные феодалы, региональные феодалы за ресурсы, наёмники, дружина по типам городов, ополчение, пороховые солдаты); прирост через казарма←кузня←шахта; потери → −призыв/прирост (×множители), 10%+ потерь → лояльность −1; % армии от населения 1→5→+5→20; кузнецы: 10/город, годовые объёмы (5000 стрел…5 великих мечей).
- Buildings catalog (здания): Банк (кредит/гонения), Еврейский банк (страховка 3 хода, окружение → самоликвидация), Амбар 2 ступени, Храм (5 веток + тёмные), Алхимическая лавка, Конюшня, Трактир, Ярмарка (Карнавал→Артисты→Табор→барон), Мастерская, Чёрный рынок, Посольство, Внешний рынок, Военная/Медицинская академии, Таможня, Больница, Книгопечатный пресс, Малый храм (реализован), Литейная, Баллиста, Пристань/Морской порт, Столичный Форум (лояльность ×2 в обе стороны), Двор Гвардейской Дружины, Торговый сбор (реализован).

### A5. Tech/doctrines/reforms/steel (Технологии_*)
- 4 ветки + Культура; советники: 3 в начале, "2 казнить 1 помиловать"; теократия→технократия. Rules: техи ≤1/ход/ветвь (реализовано), реформы ≤1/ход/доктрина (реализовано); реформы = технологические + муниципальные.
- **Каждая технология имеет светлую и тёмную сторону**; раскрытие тёмной → падение рейтинга; культурная пассивка отключает тёмные; скрытые комбо (Амбар+Алхимия=чума; +Дрессировщики=чумные крысы; химера).
- Общая ветка: Образование → (Ремесло, Фермерство, Плотничество, Сталелитейное, Рынок, Церковь, Репутация, Посольство, Корабли, Ментовка, Тюрьмы, Храм); разовые: Амбар, Госпиталь, Карантин, Книгопечатанье, Дрессировщики. Логистика вкладка (частично реализована). Кав. таран chain. Гвардейская/стенная ветвь. Мельницы: ветряная vs водяная (реализованы базово; водяная gates кузню 4+; тёмная сторона воробьёв). Именованные реформы: Казначейство (реализована), Принуждение, Геройские похороны, Городские врата, Контроль кузнецов, Фармацевтика.
- **Сталь** (Steel-c748ae22139d6401.txt = latest v; полное дерево): 6 веток оружия (Ударные — закрыта, крадётся; Древковые; Рубящие; Клинковые; Стрелковые; Пудра) + Особые изобретения + 3 ветки брони (Кольчуга/Доспехи/Поддоспешник); развилка = вход в соседнюю ветку, исходная ×2 цены; поколения (−/+ полушаги, + бесплатно через пару ходов); |Элитное| gated военной элитой; gear/method prerequisites (водяной молот и т.д.).
- **Damage-type system** (сталь, тд): ударное/дробящее/рубящее/режущее/колющее с уровнями per weapon; авто-приоритет (режущее по голым; колющее если уровень > общего уровня брони); контр-матрица: Ударные>Кольчуга>Режущие; Рубящее>Бригантина>Ударное; Тканевые>Ударные+Режущие; Эсток/Ледоруб контрят всё; щиты выключают стрелы, топоры опускают щиты; контра выключает пассивки; смешанные не контрятся; двухтиповые (Лютеранский молот) контрятся обеими.

### A6. Minigames (тд, тетрис-алхимия, чо-добавить, шахматы, таверна)
- **ТД**: attack/defense на границах регионов vs Альянс; волны каждые 4 месяца (≈2 кона); башни 4 последовательных грейда (региональный→общий→общий→региональный ультра) × 4 варианта, стакаются; тир-схема (башня/стрелковый тип/снаряды/региональное); регион-правила: болото — недосягаемые вышки, лес — лучники на деревьях, север — только катапульты/требушеты + ТД против кораблей, пустыня — иссушение при дэфе; замковый дэф; юниты на дэф, заслоны, крепость-пост, партизаны/наёмники-кемпы (EE_TD sketch); Эдемская катапульта.
- **Тетрис-алхимия**: Сбор (фигуры с 4 сторон к центральной конструкции; управление ближайшей; нельзя двигать назад; ускорение ×3 к центру) и Разбор; реагенты (убрать цвет, добавить серые, сброс ускорения); ускорение арифм. прогрессией, cap 400% → взрыв → эпидемия/мутанты у лаборатории; poison-craft путь со стенками.
- **Тетрис-инвентарь**: паковка экспедиции, тележка, вещи падают в реальном времени.
- **Шахматы**: current sketch image shows candidate card placements and a full black roster
  including a king; older background says казна/чистые улицы = ладьи, family = pieces,
  shared Антон and no enemy king/capture-all. P12 found the sources non-executable and in
  conflict, so Chess remains absent pending `P12-02`–`P12-09` designer verdicts.
- **Таверна**: спавн ближе к лейту (не в 1-м прохождении; 100% на 2-м, потом 33%); две секции; найм наёмников, спиртное, слухи; Мария Брауз 33% (карты 2×2 → комбо 3-7-Т → Пиковая Дама).

### A7. Other systems (экспедиции, события, реликвии, божественные-награды, квесты)
- **Экспедиции**: убийство пограничной крепости открывает зону; провизия-экипировка (разовое снижение); жалобы регионов; Ветеран (>50% hp → ветеран; второе ранение → выбывает); враги по типам урона (юг голые, запад кость/кожа, болото твари).
- **События**: reigns-подобные с последствиями; известные: голод (реализовано), северные набеги, концессия, контрабанда, золотой идол, эпидемия у врат, кража коней, страховка, белый камень, ведьмы.
- **Реликвии**: слоты Храма; 25% от эпидемий, боевой дух min 2, десятина +50%, кузня/конюшня без ресурсов, +1 lvl ферм/лесопилок (последняя реализована).
- **Божественные награды**: землетрясение, муссоны/ветер, +макс очко боевого духа, рыбные течения, метеор (реализован) + метеоритное железо, региональные жертвы (реализованы), цунами в пустыне.
- **Боевой дух**: очки на юнитах → повтор активки; источники трактир/вино/реликвии; шкала дезертирства (ZBS, minimal).
- **Квесты**: Палач (готовые интерактивные HTML-демо в экспорте: `Palach*.html`), золотой идол → Маг, крестоносцы-вёдра (6 фаз), воробьи/саранча/Людовик, север↔лес, упыри Аматы, титановая рыба, руда в лесу; лвл-апы регионов.

---

## §H. Designer-review ledger seed (known unknowns; Phase 0 copies these into the ledger)

1. Loyalty→workforce divisor curve (/19, /9, /1) — raw note, needs tuning (P4).
2. Wave cadence: «каждые 4 месяца» ⇒ `waveEveryCons: 2` — confirm clock (P2).
3. All tower stats (4×4 grades; «208 билдов» combinatorics) — invented defaults (P2/P3).
4. Anti-bito thresholds; deck-memory availability (always vs N/con) (P8).
5. Пиковая Дама spawn combo + neighbor-inversion period; hand order becoming gameplay-relevant (P9).
6. Per-weapon damage-type levels — partial in export (Клевец 6/4/3 etc.), rest invented (P1/P3).
7. Steel pricing (развилка ×2, элитное gating) — notation complete, numbers absent (P3).
8. Jewish-bank «окружение» proxy (no siege system) (P6).
9. Epidemic severity/spread; alchemy explosion consequences (P5/P10).
10. Morale scale (ZBS-era) — minimal scalar + floor now (P2).
11. Alliance strength curve — linear default (P2).
12. Chess — designer's own sketch; expect redesign (P12).
13. Battle-abort penalty values (P2).
14. `boutsPerCon: 3` (config) vs "Ten bouts form a con" (`docs/WEB-CLIENT.md` §12B) vs "десять раундов" design text — reconcile all three (P0 ledger entry; designer decides).

---

## Appendix: historical deferred-content baseline (verified 2026-07-16)

This appendix records the pre-program baseline only. Use actual config/spec output plus
`COVERAGE-MATRIX.md` for current closure; do not copy these counts into a later phase.

**Live card faces** (everything else is deferred): `card-clubs-2` N (no effects), `card-clubs-8` both, `card-spades-5` both, `card-spades-8` both, `card-diamonds-6` inverted, `card-diamonds-ace` N, `card-joker-jester` N (no effects). Named/authored deferred faces already in config: `card-clubs-2` inv (`streetCleanliness`), `card-hearts-5` (Агитаторы), `card-hearts-7` (Зазывалы/Поджог — effects authored, executor live), `card-hearts-king` (Легитимность Томаса), `card-hearts-ace`, `card-spades-3` (карты мира), `card-spades-10` (Прививки), `card-diamonds-6` N. All other ranks are placeholder faces («Тройка треф»…) — авторские карты для них берутся из каналов `карты`/`персонажи`.

**Deferred gifts** (10): `gift-earthquake`, `gift-tailwind`, `gift-combat-spirit`, `gift-fish-currents`, `gift-meteor-iron`, `gift-desert-tsunami`, `relic-epidemic-ward`, `relic-spirit-floor`, `relic-tithe`, `relic-resource-exemption`.

**Deferred resources** (2): `carpentry`, `whiteStone`.

**Deferred buildings** (16): `building-smithy`, `building-barracks`, `building-temple`, `building-bank`, `building-fair`, `building-tavern`, `building-stable`, `building-alchemy`, `building-hospital`, `building-customs`, `building-medical-academy`, `building-military-academy`, `building-foundry`, `building-sea-port`, `building-jewish-bank`, `municipal-capital-forum`.

**Deferred units** (4): `unit-light`, `unit-regular`, `unit-heavy`, `unit-knight`.

**Deferred technologies** (38): `doctrine-war`; non-steel: `tech-fair`, `tech-ironwork`, `tech-compass`, `tech-merchant-guilds`, `tech-banking`, `tech-generals`, `tech-foundry`, `tech-military-logistics`, `tech-supply-corps`, `reform-coercion`, `reform-heroic-funerals`, `reform-control-smiths`, `reform-theocracy`, `reform-technocracy`, `reform-city-gates`; steel (22): `steel-laurel-spearhead`, `steel-lancet-spearhead`, `steel-diamond-spearhead`, `steel-cross-spearhead`, `steel-voulge`, `steel-halberd`, `steel-lance`, `steel-butted-mail`, `steel-riveted-mail`, `steel-full-mail`, `steel-double-mail`, `steel-steel-mail`, `steel-nasal-helm`, `steel-bucket-helm`, `steel-kettle-hat`, `steel-iron-breastplate`, `steel-steel-cuirass`, `steel-water-hammer`, `steel-heavy-water-hammer`, `steel-ship-cannon`, `steel-hand-bombard`, `steel-arquebus`.

**Deferred events** (9): `event-northern-raids`, `event-lumber-concession`, `event-customs-smuggling`, `event-golden-idol`, `event-city-gates-epidemic`, `event-horse-theft`, `event-bank-insurance`, `event-white-stone`, `event-witch-apprenticeship`.

**Absent systems** (no code at all): combat/TD, expeditions, epidemics, diplomacy/external world, loyalty/reputation, seasons, advisors/persts, quests/dialogue, tavern/chess/tetris minigames, mystic cards, deck-memory, anti-bito, God dialogue/confirmations, morale, damage-type system.
