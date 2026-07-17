# Phase 5 — Epidemics and the medical chain

Use this file as the opening prompt of a fresh **Codex 5.6 Sol** session on the designer's
machine. Work only on this phase. Phase 5 depends on Phase 4's loyalty/reputation,
chronicle, operational-building, and save-migration contracts.

## Executor rules (binding)

1. **Design source discipline**: read the phase's named raw export channels in `DiscordExports/Empires_Endgame/` before implementing. On conflict: main export > `ZBS MAKING` (outdated background); `empire_prompt` defines the core loop. Export missing from the environment → stop and tell the user; do not guess.
2. **Never fabricate silently**: a mechanic without export numbers → implement with a configurable default in `game-config.json` + append a ledger entry. A mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
3. **Un-deferral discipline**: substrate + un-deferral (delete `deferredReason`, add executable effects) + `EMPIRES_LIVE_FLAG_ALLOWLIST` additions or typed payloads + tests, all in ONE change-set. `validateLiveEffects` must keep rejecting flags nothing reads.
4. **Determinism**: no `Date.now`/`Math.random` in any simulation — only the serialized RNG streams (`features/empires-endgame/rng.ts`). Minigames replay from `(plan, seed, commandLog)`; mid-minigame real-time state is never serialized.
5. Player-facing card/passive *texts* stay deliberately vague (repo philosophy — never "fix" them; the designer writes new player wording). Exact mechanics are documented in `docs/WEB-CLIENT.md` §12B.
6. Git: do NOT commit or push. Write the commit message to `docs/commit-messages/<date>.md` (one file per change-set; `-2`, `-3` suffixes for further change-sets the same day).

## Mission

Ship a deterministic, first-class epidemic lifecycle that can start in a city, progress,
damage population/production/loyalty, be contained or spread, and survive save/restore.
Settle epidemics before the existing famine roll, implement the authored multiplicative
medical-protection stack, make the city-gates event executable, expose epidemic state in
the city UI, and un-defer only the exact medical buildings/relic/card faces whose complete
semantics are backed by the raw export and real consumers.

## Design source — read before implementing

Read the complete current exports:

- channel `здания` — Hospital, Medical Academy, Alchemy, treatment and city effects;
- channel `события` — epidemic-at-the-gates and disease consequences;
- channel `Технологии / доктрины-и-реформы` — Hospital/Quarantine/Фармацевтика, city
  gates, dark sides, and the hidden Амбар + Алхимия plague combination;
- channel `реликвии` — epidemic protection;
- channel `карты` — vaccinations and any claimed Дженна mapping;
- channel `общее` — diseases split across population classes and loyalty consequences.

Read `DiscordExports/empire_prompt` for phase ordering. Main export wins over the outdated
`ZBS MAKING` file. If any named export is unavailable, **stop and tell the user; do not
implement from this compression alone**.

Rules to preserve and reconcile:

- Epidemics are city/lifecycle state, not a permanent scalar flag. Their severity,
  duration, affected classes, containment, spread candidates, and source must serialize.
- `settleEpidemics()` runs before the famine roll in `finishEmpireInternal`, so epidemic
  population/production changes are visible to famine eligibility and the same end-of-con
  settlement. Ordering must not be duplicated when an event pauses and resumes settlement.
- Hospital, Quarantine, Фармацевтика, Medical Academy, relic, and vaccination sources can
  protect differently. The plan requires a **multiplicative** protection stack; obtain
  exact semantics/numbers from the export, make missing numbers configurable + ledgered,
  and keep semantically undefined sources deferred.
- `Городские врата`: sealing contains disease but makes the local epidemic `×2`; opening
  keeps ordinary local impact and allows spread. Both choices need typed epidemic
  payloads, not inert `epidemicContained`/`epidemicSpreadRisk` flags.
- Hidden combo `Амбар + Алхимическая = чума` requires the correct event/choice from the
  raw export. The later `+ Дрессировщики` rat/creature chain is not automatically in this
  phase.
- The exact authored config card is `card-spades-10` (`Прививки`) on both sides. Its normal
  face protects; its inverted face spreads/causes a pandemic according to the export.
- The plan draft's phrase “Дженна ♣A inverted” is **not an exact config mapping**. Current
  `card-clubs-ace` is a generic `Туз треф` placeholder with both mechanics undefined. Do
  not rename it, rewrite its player text, add guessed effects, or remove either marker
  unless the raw export proves a unique mapping to this exact ID and side. Undefined or
  ambiguous faces remain deferred.

## Repo anchors — read these symbols and nearby code before editing

Relocate by symbol after earlier phases; line numbers are baseline only.

- `AGENTS.md`, `plans/empires-endgame/README.md` §A5/§A7, and
  `docs/WEB-CLIENT.md` §12B.
- `features/empires-endgame/types.ts`: `EmpiresEffect`, `EmpiresCityState`,
  `EmpiresEmpireState`, `EmpiresEventChoiceDefinition`, `EmpiresEventState`, campaign
  state/envelope, and any Phase 4 chronicle/loyalty types.
- `features/empires-endgame/config.ts`: migration chain, `validateDeferredReasons`,
  `EMPIRES_LIVE_FLAG_ALLOWLIST`, `validateLiveEffects`, and effect/payload validation.
- `features/empires-endgame/engine.ts`: `validateAndCloneSnapshot`, `chooseEvent`,
  `startEmpirePhase`, `finishEmpireInternal` (baseline `:1256`),
  `settleEmpireEconomy`, `startNextCon`, `eventIsEligible`, `firstMissingDependency`,
  `refreshProductions`, `updateOperationalBuildings`, and the Phase 4 loyalty funnel.
- Reconcile these exact config carriers in
  `Web/VueClient/public/empires-endgame/game-config.json`:
  `building-hospital`, `building-medical-academy`, `building-alchemy`,
  `relic-epidemic-ward`, `event-city-gates-epidemic`, and both faces of
  `card-spades-10`, plus the event prerequisite `reform-city-gates`. Inspect
  `municipal-granary`, `tech-medicine`, and any current quarantine/Фармацевтика
  definitions by symbol; do not invent an ID if none exists.
- Explicit ambiguity check: inspect `card-clubs-ace` and the raw `карты` source before
  touching it. A title/theme resemblance is not proof.
- UI/editor/tests: `src/pages/EmpiresEndgame.vue`,
  `src/components/empires-endgame/CityView.vue`, `EventDialog.vue`, `BuilderDrawer.vue`,
  `engine.spec.ts`, `config.spec.ts`, `qa.ts`, `qa.spec.ts`, and the Empires Cypress spec.

## Work items (in order)

1. **Reconcile epidemic content.** Build a source-to-config matrix for disease definitions,
   protection sources, events, and card sides. Put every missing numeric value under an
   explicit config pointer in the ledger. If the export does not define lifecycle
   semantics (not merely a number), leave the carrier deferred and ask the designer.

2. **Add typed config/state and migration.** A minimum shape is:

   ```ts
   interface EmpiresEpidemicDefinition {
     id: string
     durationTurns: number
     baseSeverity: number
     populationLossByStage: number[]
     productionMultiplierByStage: number[]
     loyaltyDeltaByStage: number[]
     spread: EmpiresEpidemicSpreadConfig
   }

   interface EmpiresEpidemicState {
     id: string
     diseaseId: string
     cityId: string
     stage: number
     turnsRemaining: number
     contained: boolean
     sourceId: string
     settledThroughCon: number
   }

   interface EmpiresEmpireState {
     epidemics: EmpiresEpidemicState[]
   }

   type EmpiresEpidemicStart = {
     diseaseId: string
     target: EmpiresEpidemicTarget
     severity?: number
     containment?: 'contained' | 'open'
     sourceId: string
   }
   ```

   Put definitions/rules in `config.empire.epidemics`; keep state to current lifecycle.
   Adapt names to established Phase 0 types, but retain typed targets and provenance.
   Normalize old saves to `epidemics: []`, validate city/disease IDs, discard or safely
   migrate inaccessible targets according to the established destroyed-region contract,
   and add a previous-version restore fixture. Extend the config migration to backfill the
   epidemic section, payload defaults, and known prerequisite shape for old imported
   configs before validation; do not require users to hand-edit a previous schema.

3. **Typed start path.** Add `startEpidemic(payload)` as a validated effect/resolution
   payload mirroring the trust boundary of `EmpiresGiftResolution`: rebuild eligible
   targets from config on restore, reject unknown disease/city/source IDs, and choose a
   random eligible city only through serialized RNG. Do not encode lifecycle transitions
   as arbitrary flags.

4. **Deterministic settlement.** Call `settleEpidemics()` exactly once at the start of
   `finishEmpireInternal`, before `uncoveredFoodDeficit()` and the famine event selection.
   Sort simultaneous transitions deterministically, apply population/production/class and
   loyalty consequences through existing funnels, append chronicle entries, advance or
   clear states, and mark the con settled before any event can pause. Save/restore during
   `event` must not repeat an epidemic tick.

5. **Protection stack.** Resolve only operational buildings, active relics, researched
   technology/reforms, and the currently held card side. Apply authored sources in a
   documented multiplicative formula, with config bounds/rounding and tests. A source may
   not go live merely because its current shell has an `epidemicProtectionPercent` flag;
   add a reader or replace it with a typed protection payload.

6. **Containment and spread.** Implement city-gates choices using typed epidemic state.
   Spread can target only eligible accessible cities, uses serialized RNG, and records the
   exact source. Sealed gates double local impact exactly where the export says; open gates
   use normal impact plus configured spread. Prevent self-duplication and define/test what
   happens if a target already has the same disease.

7. **City-gates prerequisite closure.** Reconcile `reform-city-gates` before making its
   event reachable. Its retained crime reduction, epidemic containment, and locked-city
   multiplier all need real typed consumers/tests; if the crime substrate or any semantic
   remains unavailable, keep the reform and therefore the gated event deferred. Do not
   bypass `firstMissingDependency` merely to exercise the event.

8. **Building and hidden-combo integration.** Implement the complete live effects of
   Hospital, Medical Academy, and the passive Alchemy substrate before un-deferring them.
   If Medical Academy's current non-epidemic effect (for example a free secondary tech
   cadence) remains on the definition, it also needs a real reader/test or the building
   remains deferred. Trigger `Амбар + Алхимия = чума` only after reconciling the authored
   event/choice; do not include the later Дрессировщики chain by implication. The
   Tetris-alchemy minigame remains Phase 10.

9. **Cards/relic.** Map both `card-spades-10` faces to consumed epidemic/protection
   mechanics with level scaling and temporary-card cleanup. Implement
   `relic-epidemic-ward` through the same protection resolver. Perform the separate Дженна
   mapping audit but leave every ambiguous/undefined face unchanged and deferred.

10. **UI/editor/QA.** Show active epidemic stage, turns, containment, protection breakdown,
   projected next impact, and spread warning in the relevant city/event UI. Expose the
   same eligibility/error reasons as the engine. Extend Builder schema controls and JSON
   validation for epidemic definitions/payloads; do not create UI-only defaults.

## Un-deferral list

These are candidates, not permission to delete markers blindly:

- `building-hospital` — complete epidemic and any retained healing effect must execute.
- `building-medical-academy` — epidemic divisor plus every retained free-tech/other effect
  must execute; otherwise keep the whole building deferred.
- `building-alchemy` — only the passive epidemic/hidden-combo substrate is in Phase 5;
  poison/crafting/minigame semantics remain Phase 10. If the live definition cannot be
  made honest without those missing semantics, keep it deferred.
- `relic-epidemic-ward` — its protection must be consumed by the multiplicative resolver.
- `event-city-gates-epidemic` — both choices require typed containment/spread behavior and
  deterministic tests, and its `reform-city-gates` prerequisite must be honestly live.
- `reform-city-gates` — prerequisite candidate; un-defer only when its crime reduction,
  epidemic containment, and locked-city multiplier all have typed consumers/tests. If it
  remains deferred, `event-city-gates-epidemic` remains deferred too.
- `card-spades-10` **normal and inverted faces** — reconcile and implement each side
  independently; do not assume one side's readiness makes the other live.

`card-clubs-ace`/“Дженна ♣A” is not on the exact list. Touch or un-defer a side only after
raw-export/config-ID reconciliation proves the mapping; otherwise keep both placeholder
faces deferred and record the ambiguity. Do not un-defer any newly invented
Quarantine/Фармацевтика ID: if the export has no matching current carrier, add a designer
question and implement only the substrate hook.

Prefer typed epidemic/protection payloads. Add a flag to
`EMPIRES_LIVE_FLAG_ALLOWLIST` only with an actual reader and focused test in this
change-set. `validateLiveEffects` must reject `epidemic*`, vaccination, academy, or alchemy
flags that remain inert. The current shells `alchemySupplies`, `epidemicContained`,
`epidemicImpactDivisor`, `epidemicProtectionPercent`, `epidemicSpreadRisk`,
`freeSecondaryTechEveryThreeTurns`, `healingTurnsReduction`, and
`localEpidemicMultiplier` each require a typed consumer (or replacement typed payload),
not allowlisting alone. The city-gates `epidemicContainment`/
`lockedCityEpidemicMultiplier` and retained crime effect have the same whole-contract
rule. Update deferred-inventory assertions only for markers truly removed.

## Verification

Add focused Vitest coverage for:

- config/save migration, invalid disease/target rejection, previous-version restore, and
  epidemic tick idempotence across an event-pause save; include a previous config fixture
  missing the epidemic section/payload fields and verify migration backfills before
  validation;
- exact ordering: epidemic tick → refreshed deficit/production → famine roll → economy
  settlement, including a case where epidemic impact changes famine eligibility;
- seeded spread determinism, containment, local `×2`, inaccessible/destroyed cities,
  duplicate-disease handling, expiration, and stable ordering of simultaneous epidemics;
- multiplicative protection, rounding/bounds, operational/deferred/locked buildings,
  relic and `card-spades-10` level scaling, and cleanup when the card leaves the hand;
- Hospital/Medical Academy/Alchemy complete retained effects and the hidden-combo trigger;
- both city-gates choices and restore without double application;
- `reform-city-gates` prerequisite enforcement and every retained reform effect, or an
  assertion that both reform and event remain deferred when a consumer is unavailable;
- `validateLiveEffects` rejecting every unread epidemic/vaccination flag and the generic
  `card-clubs-ace` remaining deferred unless uniquely reconciled.

Add the named deterministic QA scenario `epidemic-outbreak`: start an outbreak, exercise
sealed and open paths in separate seeded fixtures, advance through settlement, save and
restore during the event, and terminate without a stall. Add Cypress coverage for city
epidemic/protection UI, event choices, projected impact, and persistence. Wire all new
specs into `test:empires` / `test:empires:e2e` or the established glob.

Complete the full gate:

- `pnpm --dir Web/VueClient run test:empires` and focused epidemic specs are green.
- `bash tools/test-empires-endgame.sh` is green.
- `pnpm --dir Web/VueClient build` is green; do **not** run the broken `type-check` task.
- `bash tools/verify-docs.sh --changed` is green.
- `docs/WEB-CLIENT.md` §12B reflects the shipped behavior; any newly found bug has the
  next free entry in `docs/AUDIT-FINDINGS.md`.
- Every invented number is ledgered by JSON pointer.
- Every schema/envelope bump restores a previous-version fixture.
- Same config/seed/actions produce the same epidemic and campaign digest; no wall-clock or
  global randomness enters settlement/spread.
- Increment the patch component of `GameVersion` once under the repository contract.
- Write the commit-message file; do not commit or push.

## Docs & ledger contract

Update the affected `docs/WEB-CLIENT.md` §12B paragraphs with epidemic state and ordering,
typed start/containment/spread contracts, protection math, building/card/relic behavior,
UI, migration, and the exact markers removed. Add a finding for any newly discovered bug
under `docs/AUDIT-FINDINGS.md`; do not disguise it as a plan assumption.

Append §H #9 (epidemic severity/spread; later alchemy-explosion consequences remain Phase
10) and every invented duration, severity, loss, spread, rounding, stacking, targeting,
or duplicate-disease value to `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md`, keyed by JSON
pointer. Record the Дженна/config-ID reconciliation outcome even if the result is “remain
deferred.” Write `docs/commit-messages/<date>.md` or the next suffix.

## Designer questions (pre-seeded)

1. §H #9: what are the epidemic severity scale, duration, per-stage population/production/
   loyalty damage, spread probability, and duplicate-disease behavior?
2. Which protection sources multiply which consequence, in what order and with what
   rounding? Do Quarantine and Фармацевтика have current config carriers or wait for later
   authored content?
3. Does sealing `Городские врата` double all local consequences or only progression
   speed, and can an already-open spread occur more than once per con? What consumes the
   reform's retained crime-reduction effect in this phase?
4. What exact event/choice activates `Амбар + Алхимия = чума`, and is Alchemy otherwise
   allowed live before the Phase 10 minigame?
5. Does the raw `Дженна` card uniquely map to current `card-clubs-ace`, another ID, or no
   current carrier? Until answered/proven, that placeholder remains untouched.
