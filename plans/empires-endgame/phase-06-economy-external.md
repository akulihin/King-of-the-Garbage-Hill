# Phase 6 — Economy buildings and the external world

Use this file as the opening prompt of a fresh **Codex 5.6 Sol** session on the designer's
machine. Work only on this phase. Phase 6 depends on Phase 4's loyalty/reputation system
and on the Phase 2/3 battle/external-threat state. Phase 9 owns the tavern minigame and
mystic cards; do not pull them into this change-set.

## Executor rules (binding)

1. **Design source discipline**: read the phase's named raw export channels in `DiscordExports/Empires_Endgame/` before implementing. On conflict: main export > `ZBS MAKING` (outdated background); `empire_prompt` defines the core loop. Export missing from the environment → stop and tell the user; do not guess.
2. **Never fabricate silently**: a mechanic without export numbers → implement with a configurable default in `game-config.json` + append a ledger entry. A mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
3. **Un-deferral discipline**: substrate + un-deferral (delete `deferredReason`, add executable effects) + `EMPIRES_LIVE_FLAG_ALLOWLIST` additions or typed payloads + tests, all in ONE change-set. `validateLiveEffects` must keep rejecting flags nothing reads.
4. **Determinism**: no `Date.now`/`Math.random` in any simulation — only the serialized RNG streams (`features/empires-endgame/rng.ts`). Minigames replay from `(plan, seed, commandLog)`; mid-minigame real-time state is never serialized.
5. Player-facing card/passive *texts* stay deliberately vague (repo philosophy — never "fix" them; the designer writes new player wording). Exact mechanics are documented in `docs/WEB-CLIENT.md` §12B.
6. Git: do NOT commit or push. Write the commit message to `docs/commit-messages/<date>.md` (one file per change-set; `-2`, `-3` suffixes for further change-sets the same day).

## Mission

Ship the external-world-lite substrate and its player-facing economy actions: deterministic
recurring Людовик trade offers, bank loans/repayment, insurance lifecycle, fair actions,
customs/trade flow, and the authored loyalty/reputation/Alliance-threat interactions.
Integrate the exact candidate buildings, technologies, gifts, relics, and events below only
when each definition's complete semantics are executable. The tavern receives passive
substrate only; no Phase 9 minigame/mystic-card work is included.

## Design source — read before implementing

Read the full current exports:

- channel `здания` — Bank, insurance bank, Fair, Tavern, Stable, Customs, Sea Port and
  their active/passive contracts;
- channel `экономика` — trade, currency, market, loans, repayments, tariffs, and Людовик;
- channel `события` — concessions, smuggling, horse theft, insurance, and white stone;
- channel `Технологии / доктрины-и-реформы` — trade/fair/compass/guild/banking unlocks,
  reputation prerequisites, and fair action chain;
- channel `божественные-награды` — earthquake, tailwind, currents, meteorite iron, and
  desert tsunami;
- channel `реликвии` — tithe and material exemptions;
- channel `дома / торговая-гавань` and `общее` — external trade/ports, reputation, and
  regional/external relationships.

Read `DiscordExports/empire_prompt` for the core loop. Main export wins over outdated
`ZBS MAKING`; where two main-channel messages conflict, identify chronology/context and
record unresolved semantics instead of averaging them. If any named export is absent,
**stop and tell the user; do not implement from this compression alone**.

Rules to preserve and reconcile:

- Reputation is `−9..+9` and gates trade/unions. Phase 6 must consume the Phase 4 scalar
  through typed prerequisites, not duplicate it in `external` state.
- Bank: the player can take current resources/credit and repays over later turns; гонения
  and other default consequences interact with loyalty/reputation. Principal, term,
  interest, eligibility, stacking, and default are config data.
- Insurance bank: an authored three-calm-turn contract protects a city, while
  `окружение` can cause self-liquidation/destruction. The design does not yet give an
  unambiguous proxy for `окружение`; §H #8 requires a designer decision. Do not silently
  equate it to any battle state.
- Fair actions form an authored progression such as Карнавал → Артисты → Табор → барон,
  with cooldown/timed loyalty/reputation/economic consequences. Reconcile conflicting
  cadence messages from the current export rather than trusting the shell.
- Tavern can contribute passive recruiting/morale substrate now, but Tavern minigame,
  Мария Брауз encounter, Пиковая Дама, and mystic cards remain Phase 9.
- Stable, Sea Port, tailwind, and other naval/cavalry carriers may require combat/fleet
  semantics not supplied by this phase. A trade-only partial reader is not enough to
  remove a whole definition's marker if retained effects remain inert.
- Людовик provides authored recurring trade offers; selection/cadence must be serialized
  and deterministic. Alliance threat from Phase 2/3 may gate/modify offers only as the
  export states.
- Gifts/relics/events need typed resolutions/lifecycles where a scalar flag cannot express
  their effect. Existing numbers are not proof that semantics are complete.

## Repo anchors — read these symbols and nearby code before editing

Relocate after prior phases; baseline lines are navigational only.

- `AGENTS.md`, `plans/empires-endgame/README.md` §A4/§A7, and
  `docs/WEB-CLIENT.md` §12B.
- `features/empires-endgame/types.ts`: `EmpiresEffect`, `EmpiresGiftResolution`,
  `EmpiresPendingGiftResolution`, `EmpiresDependency`, building/technology/event types,
  `EmpiresCityState`, `EmpiresEmpireState`, campaign state/envelope, and Phase 2
  `external`/army state.
- `features/empires-endgame/config.ts`: migration/validation chain,
  `validateDeferredReasons`, `EMPIRES_LIVE_FLAG_ALLOWLIST`, `validateLiveEffects`, and
  gift/event payload validation.
- `features/empires-endgame/engine.ts`: `chooseGift`, `resolvePendingTarget`,
  `applyFixedGiftResolution`, `applyRecurringGiftResolution`, `placeBuilding`,
  `upgradeBuilding`, `research`, `chooseEvent`, `startEmpirePhase`,
  `finishEmpireInternal`, `settleEmpireEconomy`, `startNextCon`,
  `firstMissingDependency`, `eventIsEligible`, `buildingResourceCosts`,
  `operationalBuildingFlagValue`, `tradeLevyGoldForCity`, and the Phase 4 loyalty/
  reputation funnels.
- Phase 2/3 integration: typed `external.allianceThreat`, TD battle outcomes, naval battle
  support if any, army morale/equipment, and destroyed/rebellious-region boundaries.
- Reconcile every exact ID in the un-deferral section against
  `Web/VueClient/public/empires-endgame/game-config.json`; read all effects,
  prerequisites, choices, resolutions, and `deferredReason`, not just the definition name.
- UI/editor/tests: `src/pages/EmpiresEndgame.vue`, `CityView.vue`, `GiftDraft.vue`,
  `TargetResolutionDialog.vue`, `EventDialog.vue`, `BuilderDrawer.vue`, engine/config/QA
  specs, and `cypress/e2e/empires-endgame.cy.ts`.

## Work items (in order)

1. **Reconcile the entire candidate inventory.** For each exact ID below, map every current
   effect/choice to raw semantics and to a concrete Phase 6 reader. Classify it `ready`,
   `needs configurable numbers`, or `semantics/substrate undefined`. Only `ready` and
   number-only cases may be implemented; keep the third class deferred and add a ledger
   question. Do not delete an effect merely to make a shell pass validation.

2. **External config/state and migration.** Extend the Phase 0 `external` state instead of
   creating parallel flags. A minimum sketch is:

   ```ts
   interface EmpiresExternalConfig {
     tradeOffers: EmpiresTradeOfferDefinition[]
     offersPerRefresh: number
     refreshEveryCons: number
     loans: EmpiresLoanRules
     insurance: EmpiresInsuranceRules
     fairActions: EmpiresFairActionDefinition[]
   }

   interface EmpiresExternalState {
     allianceThreat: number // preserve Phase 2 ownership
     activeTradeOfferIds: string[]
     tradeOfferHistory: string[]
     lastOfferRefreshCon: number
     loans: EmpiresLoanState[]
     insuranceContracts: EmpiresInsuranceContractState[]
     fairProgress: Record<string, EmpiresFairProgressState>
     timedModifiers: EmpiresExternalTimedModifier[]
   }
   ```

   Loan/insurance/fair entries need stable IDs, city/source provenance, start/due con,
   remaining obligations/cooldowns, and idempotence markers. Put authored definitions and
   every tunable in `config.empire.external`; normalize old saves and test prior-version
   restore. Backfill new config sections/payload fields through the migration chain before
   validation, and cover an old imported config fixture. Do not serialize derived
   reputation or duplicate Alliance threat.

3. **Deterministic Людовик offers.** Add a focused module such as
   `features/empires-endgame/external.ts` for eligibility, refresh, purchase, and expiry.
   Rebuild offer eligibility from trusted config, accessible regions/cities, reputation,
   prerequisites, and authored Alliance state. Use serialized RNG for weighted selection,
   stable ID tie-breaks, and a serialized refresh con so reload cannot reroll offers.
   Purchases use existing local/shared resource payment and typed results.

4. **Bank and repayment lifecycle.** Implement loan availability/action UI, trusted
   resource transfer, scheduled settlement at the authored con boundary, default/гонения
   consequences through Phase 4 loyalty/reputation, and save-safe idempotence. All
   principal/interest/term/stacking/default numbers belong in config and ledger if absent.
   Do not represent debt as a one-time `creditAvailable` flag.

5. **Insurance lifecycle.** Track the selected insured city and consecutive calm turns,
   protection activation/consumption, and invalidation. Do not implement the destructive
   `окружение` path until the raw export or designer identifies the exact existing state
   that triggers it. If this remains unresolved, keep `building-jewish-bank` and
   `event-bank-insurance` deferred even if their UI/state skeleton exists.

6. **Fair, Tavern passive, Stable, Customs, and Sea Port.** Implement each building only
   against its complete current definition:

   - Fair actions use typed cooldown/progression/timed effects and real loyalty/reputation
     readers; the later minigame is not implied.
   - Tavern may expose only the authored passive morale/recruiting source consumed by the
     Phase 2 army. Keep the Phase 9 minigame hook explicitly unavailable.
   - Stable requires real cavalry/equipment/active-effect consumers for retained effects.
   - Customs must affect an actual tariff/trade calculation and its smuggling event.
   - Sea Port must have the complete trade/shipbuilding/fleet consumers present in the raw
     definition. If Phase 3 supplies only enemy ships, that is not automatically a player
     fleet substrate.
   - Temple must execute preaching, tithe, and relic storage/slot behavior; its existing
     gold production is not enough while `templeAvailable` has no real consumer.

7. **Technology prerequisites/payoffs.** Make the four exact technology candidates unlock
   real Fair/transfer/guild/bank mechanics. `tech-compass` must alter a real transfer
   operation; `tech-merchant-guilds` must affect an authored offer/trade calculation.
   Preserve branch/day/cost/prerequisite rules and constructor round-trip. If a payoff is
   missing, keep that technology deferred.

8. **Typed gifts and relics.** Implement each exact candidate through existing gift target
   trust boundaries or a new typed resolution/lifecycle. Earthquake needs a consumed TD/
   enemy effect; tailwind needs a real naval consumer; fish currents need all authored
   outcomes and five-turn/world-disaster lifecycle; meteorite iron needs its radiation
   consequence as well as city resources if retained; desert tsunami needs persistent
   South/watermill/resort semantics. Tithe must change actual income. The resource
   exemption must be consumed by both retained smithy/stable costs. Any incomplete carrier
   stays deferred.

9. **Events.** Convert regional loyalty shells to Phase 4 typed effects and implement all
   choices for the five exact events. Smuggling needs real population/trade consequences;
   horse theft needs its repeat/disable/enemy-target/noble-loyalty lifecycle; insurance
   uses typed contracts; white stone needs real mine/mortality control. Event eligibility
   and restore must never expose a choice with a no-op effect. Reconcile the currently
   deferred resources `whiteStone` and `carpentry`: `event-white-stone` cannot become live
   while `whiteStone` is only a future/inert stockpile, and `carpentry` remains deferred
   unless the raw economy/building sources define both production and real consumers.

10. **Economy card faces.** Reconcile all authored economy/diplomacy faces in the raw
    `карты` channel. The exact baseline candidates are `card-diamonds-6` normal
    (`antimonopolyService`; inverted is already live) and `card-diamonds-ace` inverted
    (`externalTradeDisabled`/`internalTradeOnly`; normal is already live). Un-defer only
    the indicated side after every monopoly/Don or external/internal-trade consequence is
    consumed, level-scaled, cleaned up, visible, and tested. Placeholder diamond faces are
    mapped only when the raw export uniquely identifies their current config ID and side.

11. **UI/editor/QA.** Add an external/trade surface integrated with the empire page, plus
    building action dialogs for loans, insurance, and Fair actions. Show offer expiry,
    reputation gates, obligations, cooldowns, and deterministic failure reasons. Extend
    Builder config/payload editing and validation; no UI-only state or defaults.

## Un-deferral list

Do not add IDs to this phase beyond this inventory.

**Buildings**

- `building-bank`
- `building-jewish-bank`
- `building-fair`
- `building-tavern` — passive substrate only; Tavern minigame/mystic content remains P9
- `building-stable`
- `building-customs`
- `building-sea-port`
- `building-temple`

**Technologies**

- `tech-fair`
- `tech-compass`
- `tech-merchant-guilds`
- `tech-banking`

**Divine gifts**

- `gift-earthquake`
- `gift-tailwind`
- `gift-fish-currents`
- `gift-meteor-iron`
- `gift-desert-tsunami`

**Relics**

- `relic-tithe`
- `relic-resource-exemption`

**Events**

- `event-lumber-concession`
- `event-customs-smuggling`
- `event-horse-theft`
- `event-bank-insurance`
- `event-white-stone`

**Resources**

- `whiteStone` — prerequisite closure for `event-white-stone`; un-defer only with actual
  production, mine/mortality behavior, display, spending/consumer rules, and tests.
- `carpentry` — conditional Phase-6 ownership target. Un-defer only if the main export
  defines complete production and expense semantics for the construction/economy systems;
  otherwise retain its marker and ledger the missing consumers.

**Card faces**

- `card-diamonds-6` normal only — conditional on complete monopoly/Don semantics.
- `card-diamonds-ace` inverted only — conditional on real external/internal trade gates.
- Any other economy/diplomacy face only after unique raw-export ID/side reconciliation;
  generic placeholder faces remain deferred.

For every ID, remove `deferredReason` only when **all retained effects/choices and required
substrate semantics** execute, serialize, render, and have focused tests. If the export
defines mechanics but omits numbers, use config defaults + ledger. If it does not define
semantics or this phase lacks the required fleet/cavalry/siege/mortality substrate, keep
the marker and record the question; a smaller set of honest un-deferrals is a successful
phase. Update `config.spec.ts` inventory expectations to match only actual removals.

Prefer typed loans/contracts/offers/timed modifiers/gift resolutions. Add a flag to
`EMPIRES_LIVE_FLAG_ALLOWLIST` only with a real engine reader and a focused consumption
test. A lifecycle is never made live by adding its flag to the allowlist: loans,
repayments, insurance, offer refresh/expiry, Fair progression/cooldowns, currents,
disasters, horse-theft recurrence, and mine/mortality state require typed payload/state.
`validateLiveEffects` must reject unread credit, market, fleet, current, insurance,
customs, horse, mortality, tithe, or exemption flags.

## Verification

Add focused Vitest coverage for:

- config/save migration and restore of offers, loans, obligations, insurance, fair
  cooldowns, and timed modifiers without reroll/double settlement; include a previous
  config fixture without the external payload fields and assert migration backfill occurs
  before validation;
- deterministic Людовик eligibility/refresh/purchase/expiry for multiple seeds,
  reputation gates, inaccessible/rebellious/destroyed targets, and Alliance interaction;
- loan resource transfer, exact repayment schedule, insufficient funds/default, loyalty/
  reputation consequences, stacking rule, and idempotence;
- insurance calm-turn progression and consumption; if `окружение` remains unresolved,
  assert the building/event remain deferred and unavailable;
- Fair action order/cooldown/timed expiry, Tavern passive-only boundary, and every live
  Stable/Customs/Sea Port/Temple consumer;
- each live technology changing the intended real calculation;
- every live gift/relic typed outcome and lifecycle, including restore mid-effect;
- all choices of each live event, repeat/eligibility behavior, and no unread flag;
- `whiteStone` production/consumption and future-badge removal whenever its event is live;
  assert `carpentry` remains deferred unless both sides of its economy are implemented;
- both exact economy-card candidate sides, including level scaling, temporary cleanup,
  and assertions that their opposite already-live sides remain unchanged;
- `validateLiveEffects` rejecting a deliberately non-deferred inert Phase 6 shell.

Add a deterministic QA scenario such as `economy-external` that refreshes and buys a
Людовик offer, takes/repays or defaults a loan, advances a Fair/insurance lifecycle, saves
and restores, and terminates without a stall. Add Cypress coverage for the external market,
reputation-gated offer, bank obligation, Fair cooldown, exact deferred reasons for any
honestly unfinished carrier, and persistence. Wire all new specs into the established
test scripts/glob.

Complete the full gate:

- `pnpm --dir Web/VueClient run test:empires` and focused external/economy specs are green.
- `bash tools/test-empires-endgame.sh` is green.
- `pnpm --dir Web/VueClient build` is green; do **not** run the broken `type-check` task.
- `bash tools/verify-docs.sh --changed` is green.
- `docs/WEB-CLIENT.md` §12B reflects the shipped behavior; any newly found bug has the
  next free entry in `docs/AUDIT-FINDINGS.md`.
- Every invented number is ledgered by JSON pointer.
- Every schema/envelope bump restores a previous-version fixture.
- Same config/seed/actions yield the same offers, settlements, timed effects, and final
  digest; no wall-clock/global random calls.
- Increment the patch component of `GameVersion` once under `AGENTS.md`.
- Write the commit-message file; do not commit or push.

## Docs & ledger contract

Update affected `docs/WEB-CLIENT.md` §12B paragraphs with external state/migration,
Людовик offers, reputation gating, loan/insurance/Fair lifecycles, passive Tavern boundary,
gift/relic/event typed behavior, UI, and the exact carrier-by-carrier deferred/live result.
Add newly discovered bugs to `docs/AUDIT-FINDINGS.md` with the next free ID.

Append §H #8 (`building-jewish-bank` `окружение` proxy) and every invented offer cadence/
weight, loan term/interest/default, insurance counter, Fair cooldown/duration, tariff,
gift lifecycle, event recurrence, rounding, or threshold to the append-only review ledger,
keyed by JSON pointer. For every exact candidate left deferred, record the missing semantic
or substrate and designer question. Write `docs/commit-messages/<date>.md` or the next
same-day suffix.

## Designer questions (pre-seeded)

1. §H #8: what exact state counts as insurance-bank `окружение` now that TD exists — an
   active siege, a lost home-region defense, an enemy reaching the city, or something else?
2. What are the authoritative Bank principal, repayment term, interest, stacking, default,
   and гонения consequences where the main export gives examples rather than final values?
3. Which Fair cadence/message is current, and are Карнавал → Артисты → Табор → барон
   strictly sequential unlocks or independently repeatable actions?
4. What is Людовик's offer cadence, pool/weighting, reputation gate, and relationship to
   Alliance threat?
5. Does Phase 3 provide enough player-fleet substrate for `building-sea-port` and
   `gift-tailwind`, or must they remain deferred until a later fleet phase?
6. For each of the five gift candidates, which consequences are mandatory before the gift
   is honest (especially fish-current world disasters, meteor radiation, and South resort/
   watermill persistence)?
7. What are the complete Temple preaching/tithe/relic-storage rules and how many relic
   slots exist at each level?
8. What produces and spends `carpentry`, and which exact consumers make `whiteStone` more
   than a future stockpile?
9. Are `card-diamonds-6` normal and `card-diamonds-ace` inverted complete as currently
   authored, and what monopoly/Don or internal-trade lifecycle do their effects require?
