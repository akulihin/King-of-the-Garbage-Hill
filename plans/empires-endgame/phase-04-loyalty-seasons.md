# Phase 4 — Loyalty, reputation, seasons, and technology dark sides

Use this file as the opening prompt of a fresh **Codex 5.6 Sol** session on the designer's
machine. Work only on this phase. Phase 4 depends on Phase 2's TD battle settlement and on
the migration/state scaffolding from Phase 0; inspect the repository and confirm those
dependencies before editing.

## Executor rules (binding)

1. **Design source discipline**: read the phase's named raw export channels in `DiscordExports/Empires_Endgame/` before implementing. On conflict: main export > `ZBS MAKING` (outdated background); `empire_prompt` defines the core loop. Export missing from the environment → stop and tell the user; do not guess.
2. **Never fabricate silently**: a mechanic without export numbers → implement with a configurable default in `game-config.json` + append a ledger entry. A mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
3. **Un-deferral discipline**: substrate + un-deferral (delete `deferredReason`, add executable effects) + `EMPIRES_LIVE_FLAG_ALLOWLIST` additions or typed payloads + tests, all in ONE change-set. `validateLiveEffects` must keep rejecting flags nothing reads.
4. **Determinism**: no `Date.now`/`Math.random` in any simulation — only the serialized RNG streams (`features/empires-endgame/rng.ts`). Minigames replay from `(plan, seed, commandLog)`; mid-minigame real-time state is never serialized.
5. Player-facing card/passive *texts* stay deliberately vague (repo philosophy — never "fix" them; the designer writes new player wording). Exact mechanics are documented in `docs/WEB-CLIENT.md` §12B.
6. Git: do NOT commit or push. Write the commit message to `docs/commit-messages/<date>.md` (one file per change-set; `-2`, `-3` suffixes for further change-sets the same day).

## Mission

Ship first-class city and regional loyalty, a consumed reputation scalar, reversible
regional rebellion, season-aware food production, population-class building gates, and
the light/dark technology framework with a persistent chronicle. Wire TD losses into the
loyalty funnel, migrate old saves, expose the systems in the existing empire UI, and
un-defer only the exact authored content whose complete behavior now has a real consumer.

## Design source — read before implementing

Read the complete current exports, not just search excerpts:

- channel `застройка` — city workforce, negative-loyalty shutdown, the capital forum,
  army-loss consequences, and class/building relationships;
- channel `общее` — reputation, city/region loyalty, rebellion, and social classes;
- channel `Технологии / доктрины-и-реформы` (the current `Технологии_*` source) — light
  and dark sides, cultural suppression, greenhouses, forum, and class gates;
- channel `карты` — `Чистые улицы` and any actually authored Народ-suit loyalty faces.
- channel `события` — `Набеги за древесиной` and its North/West loyalty choices.

Also read `DiscordExports/empire_prompt` for the loop. `ZBS MAKING - empires-endgame` is
outdated background and loses on conflict to the main export. If any named export is
missing in the session, **stop and tell the user; do not implement from this compressed
model alone**.

Rules to preserve while reconciling the raw messages:

- Loyalty is bounded at `−9..+9`. A city has its own value and receives its region's
  modifier. Strong negative regional loyalty can cause a rebellion; rebellion must be a
  reversible state, not destruction or an irreversible flag.
- Effective workforce follows the authored anchors: loyalty `−9 → divisor 19`, `0 → 9`,
  `+9 → 1`. Intermediate values are not authoritative unless the export defines them;
  make the curve configurable and ledger every invented entry/formula.
- Negative loyalty can make buildings non-operational. Population classes are
  load-bearing gameplay data; in particular, the export ties smithy operation/upgrades
  to мещане loyalty. Do not collapse class loyalty into generic population mood if the
  export distinguishes them.
- Reputation is also bounded `−9..+9` and gates trade/unions. It must be read by actual
  prerequisites/formulas, not accepted as an inert flag.
- A loss of at least 10% of a city's/region's deployed soldiers causes loyalty `−1` in
  the authored army notes. Reconcile the exact denominator and target with Phase 2's
  battle result before coding it.
- Seasons are derived deterministically from campaign time/con count. Summer/winter can
  change the food limit/production by `×2`; greenhouses equalize the seasonal effect.
  Do not serialize a second source of truth if `currentSeason(con, config)` can derive it.
- Every technology can have a light and dark side. Revealing/using a dark side lowers
  rating/reputation; the cultural passive disables dark sides. Persist decisions and
  consequences in a chronicle so save/restore cannot replay them.
- `Столичный форум` multiplies loyalty in both directions. Determine from the export
  whether that is a delta multiplier, a current-value transition, or an operational
  modifier; never silently choose temporal semantics.
- `card-clubs-2` inverted currently carries `streetCleanliness`. Additional Народ faces
  are candidates only after exact raw-export title ↔ current config ID ↔ side
  reconciliation. Placeholder faces are not authored content.
- `event-northern-raids` is a political loyalty event, not a Phase 3 combat event: its
  current choices change `loyaltyNorth`/`loyaltyWest` (and one grants wood). Route those
  choices through typed regional loyalty effects in this phase.
- The political chain `reform-coercion` → `reform-heroic-funerals`, plus
  `reform-control-smiths`, `reform-theocracy`, and `reform-technocracy`, can close only
  when every retained combat/class/political effect has a typed consumer. Theocracy is
  also a prerequisite used by a Phase 3 steel node; test that dependency end to end.

## Repo anchors — read these symbols and nearby code before editing

Line numbers may have drifted after earlier phases; relocate by symbol name.

- Repository contract: `AGENTS.md`; program contract and §A4/§A5 compression:
  `plans/empires-endgame/README.md`; current behavior: `docs/WEB-CLIENT.md` §12B.
- State/effects: `Web/VueClient/src/features/empires-endgame/types.ts`
  `EmpiresEffect`, `EmpiresCityState`, `EmpiresEmpireState`, `EmpiresCampaignState`, and
  the snapshot envelope (baseline anchors approximately `:49`, `:460`, `:485`, `:513`).
- Config migration/deferral boundary: `features/empires-endgame/config.ts`
  `migrateEmpiresConfig`, `validateDeferredReasons`, `EMPIRES_LIVE_FLAG_ALLOWLIST`, and
  `validateLiveEffects` (baseline `:119-220`).
- Restore/phase/economy: `features/empires-endgame/engine.ts`
  `validateAndCloneSnapshot`, `startEmpirePhase`, `finishEmpireInternal`,
  `settleEmpireEconomy`, `startNextCon`, `refreshProductions`,
  `updateOperationalBuildings`, `firstMissingDependency`, and `applyEffects`.
- Phase 2 integration: locate `settleBattleOutcome`, the serialized army/deployment
  result, and its city/region provenance. Read the TD result types before adding a
  loyalty hook; do not infer losses from UI text.
- Existing content to reconcile in
  `Web/VueClient/public/empires-endgame/game-config.json`:
  `municipal-capital-forum`, `card-clubs-2` inverted, existing `loyalty*`,
  `streetCleanliness`, `loyaltyMultiplierPercent`, and `progressAcceleration` flags;
  `event-northern-raids`; and `reform-coercion`, `reform-heroic-funerals`,
  `reform-control-smiths`, `reform-theocracy`, `reform-technocracy`, plus all candidate
  card faces found in the raw export.
- UI/editor/tests: `src/pages/EmpiresEndgame.vue`,
  `src/components/empires-endgame/CityView.vue`, `TechTree.vue`, `BuilderDrawer.vue`,
  `features/empires-endgame/engine.spec.ts`, `config.spec.ts`, `qa.ts`, `qa.spec.ts`, and
  `cypress/e2e/empires-endgame.cy.ts`.

## Work items (in order)

1. **Reconcile first.** Produce a working table from the named exports to current config
   IDs/sides. Record every undefined number/semantic in
   `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md` before choosing a default. In particular,
   enumerate authored Народ faces separately from placeholder faces.

2. **Migrate config and save state.** Extend the Phase 0 config migration rather than
   hard-throwing on the previous schema. The planned minimum shape is:

   ```ts
   interface EmpiresLoyaltyConfig {
     minimum: -9
     maximum: 9
     workforceDivisors: Record<string, number>
     rebellionThreshold: number
     recoveryThreshold: number
     tdLossPercentThreshold: number
   }

   interface EmpiresSeasonDefinition {
     id: string
     durationCons: number
     foodMultiplier: number
   }

   interface EmpiresCityState {
     loyalty: number
     populationClassLoyalty: Record<string, number> // only if confirmed by the export
   }

   interface EmpiresEmpireState {
     regionLoyalty: Record<string, number>
     rebelliousRegionIds: string[]
     chronicle: EmpiresChronicleEntry[]
     // reputation remains an empire-wide scalar with a real reader, e.g. flags.reputation
   }
   ```

   `config.empire.loyalty` owns all thresholds/curves; `config.empire.seasons` owns the
   ordered season cycle and greenhouse behavior. Normalize absent fields in
   `validateAndCloneSnapshot`. The program targets the Phase 4 save-envelope migration to
   v3; if an earlier executed phase already advanced farther, use the next sequential
   version rather than reusing a number. Ship a previous-version restore fixture.

3. **One loyalty mutation funnel.** Add typed loyalty effects and a single
   `applyLoyaltyDelta(target, delta, source)` path. It must clamp, apply an operational
   forum modifier exactly once according to the reconciled semantics, update rebellion
   state, append one chronicle entry, and refresh dependent production/building state.
   Convert authored regional `loyaltyWest`/`loyaltyNorth`-style shells to the typed path
   when their owning content becomes live; do not proliferate magic flag names.

4. **Operational consequences.** Make workforce availability use the configurable curve
   in the same projected/effective-level paths used for construction, production, food,
   and UI previews. Add negative-loyalty shutdown and confirmed population-class gates to
   `updateOperationalBuildings`/dependency reporting so the engine and display cannot
   disagree. Rebellion removes a region from normal control/production/targets while
   preserving inspectable history and a recovery path; do not reuse
   `destroyedRegionIds`.

5. **TD-loss hook.** Consume the typed Phase 2 battle settlement once. Attribute losses
   to their origin, compute the configured percentage against the authored denominator,
   and route qualifying penalties through `applyLoyaltyDelta`. A restored settled battle
   must not apply the penalty twice.

6. **Reputation.** Give the empire-wide reputation scalar a clamp and typed mutation
   helper, then make confirmed trade/union prerequisites consume it via
   `firstMissingDependency` or a typed dependency. Do not un-defer Phase 6 trade content
   here; provide the substrate and visible value only.

7. **Seasons.** Implement pure `currentSeason(con, config)` and route its multiplier
   through `cityProduction`, settlement projections, famine checks, and displayed city
   metrics. Greenhouses equalize the authored seasonal difference only where the export
   says they apply. Season changes append deterministic chronicle entries at most once.

8. **Technology sides and chronicle.** Extend technology config/state with typed
   light/dark-side choice/reveal data. Dark-side selection must have an executable
   reputation/rating consequence; cultural suppression must prevent the effect through a
   real dependency. Preserve research branch limits and constructor round-tripping. Do
   not invent or un-defer individual dark-side technologies whose semantics are absent.

9. **Political prerequisite closure.** Reconcile and implement the full retained effects
   of the five named reform candidates before removing a marker: coercion's building
   override plus loyalty cost; heroic funerals' casualty-loyalty and recruit-growth
   protection; smith specialization plus мещане/дворяне loyalty changes; theocracy's
   political/dark-experiment behavior and steel prerequisite; technocracy's civil-war
   risk. Model transitions/consequences as typed state/effects, not allowlist-only flags.
   If any effect lacks a Phase 4/earlier substrate or raw semantics, keep that reform
   deferred and document the blocked dependency (including any affected steel node).

10. **Northern-raids event.** Convert both `event-northern-raids` choices from regional
    loyalty flag shells to typed effects, preserve the authored wood outcome, and test
    eligibility, choice application, chronicle output, and restore. Remove the marker only
    when both choices are fully executable.

11. **UI.** Show city and regional loyalty, class gates, reputation, current/next season,
   rebellion state, and recent chronicle entries in the existing map/city/development
   surfaces. Every disabled building/research action must expose the same engine reason.
   Extend the Builder for new config data without creating a second validation model.

12. **Un-defer through the gate below.** Remove a marker only after its complete effect is
    consumed, visible, serializable, and covered by focused tests.

## Un-deferral list

- `municipal-capital-forum`: un-defer only after both positive and negative loyalty
  behavior and progress acceleration (if still present on the live definition) have real
  readers. A partly consumed level definition remains deferred.
- `card-clubs-2` **inverted face only** (`streetCleanliness`): reconcile the export,
  replace/map the shell to a consumed loyalty/cleanliness mechanic, and test level scaling
  plus temporary-card-effect cleanup. Do not alter its player-facing text.
- Additional Народ-suit loyalty faces: **do not pre-name or fabricate IDs**. Un-defer only
  a face whose authored title and side match one unique existing config card after reading
  `карты`; keep every placeholder/ambiguous face deferred and put the mismatch in the
  ledger. Normal and inverted sides are separate contracts.
- `card-hearts-5` normal/inverted: conditional exact candidates because their current
  `royalAgitation`/`sabotageRisk` shells belong to influence/political state. Un-defer only
  after the raw `карты` source confirms both semantics and P4 provides consumed typed
  effects, cleanup, UI, and tests.
- `card-hearts-king` normal/inverted: the current titles are authored but both mechanics
  are explicitly undefined. Port/un-defer only if the raw main export supplies an exact
  legitimacy contract for these faces; otherwise retain both markers and ledger the gap.
- `event-northern-raids`: un-defer here, not in Phase 3, only after both choices use typed
  `loyaltyNorth`/`loyaltyWest` targets and the wood outcome remains executable.
- `reform-coercion`, `reform-heroic-funerals`, `reform-control-smiths`,
  `reform-theocracy`, `reform-technocracy`: conditional closure candidates. Each remains
  deferred unless **all** of its current effects and prerequisites have typed consumers
  and tests. Theocracy's Phase 3 steel dependency must be included in its test.

Prefer typed `loyalty`/reputation payloads. If a live flag is genuinely the right model,
add it to `EMPIRES_LIVE_FLAG_ALLOWLIST` only in the same change that adds an engine reader
and a rejection/consumption test. `validateLiveEffects` must still fail a non-deferred,
unread flag. Update `config.spec.ts` expected deferred inventories only for markers
actually removed. In particular, `loyaltyMultiplierPercent`, `progressAcceleration`, and
`streetCleanliness` are currently unsupported shells: every one must become a typed effect
or acquire a real reader/test; allowlisting alone is not completion.

## Verification

Add focused Vitest coverage for:

- old-save/config migration, round-trip, default normalization, and no duplicate
  chronicle or TD-loss application after restore;
- clamp boundaries, forum behavior in both directions, regional modifier composition,
  rebellion/recovery, and inaccessible-vs-destroyed distinction;
- workforce anchors `−9 → /19`, `0 → /9`, `+9 → /1`, every configured intermediate,
  projected construction/food parity, negative shutdown, and confirmed мещане gate;
- reputation prerequisites and dark-side penalty/cultural suppression;
- deterministic season cycling, summer/winter food math, greenhouse equalization, famine
  ordering, and displayed/settled production parity;
- `card-clubs-2` inverted level scaling and card-flag cleanup; no placeholder Народ face
  accidentally becomes live;
- both `event-northern-raids` choices, plus full-effect tests for each political reform
  actually un-deferred; assert every incomplete reform remains unavailable;
- theocracy satisfying the exact Phase 3 steel prerequisite without bypassing ordinary
  research/dependency rules;
- `validateLiveEffects` rejecting an unread loyalty/reputation flag.

Add a deterministic QA scenario such as `loyalty-rebellion` that crosses a TD-loss
threshold, enters and recovers from rebellion, advances a season, saves/restores, and
terminates without a stall. Add Cypress coverage for the loyalty/reputation/season UI,
disabled-building reason, chronicle, and restore. Wire every new spec into the existing
`test:empires` / `test:empires:e2e` scripts or the Phase 0 glob.

Before handing off, complete the full gate:

- `pnpm --dir Web/VueClient run test:empires` and the new focused specs are green.
- `bash tools/test-empires-endgame.sh` is green (Vitest + Cypress).
- `pnpm --dir Web/VueClient build` is green; do **not** use the broken `type-check` task.
- `bash tools/verify-docs.sh --changed` is green.
- `docs/WEB-CLIENT.md` §12B reflects the shipped behavior; any newly found bug has the
  next free entry in `docs/AUDIT-FINDINGS.md`.
- Every invented number is appended to the review ledger with its JSON pointer.
- Every schema/envelope bump restores a previous-version fixture.
- Repeating the same seed and action sequence yields the same state/chronicle digest; no
  simulation path uses wall-clock/random globals.
- Increment the patch component of `GameVersion` once, as required by `AGENTS.md`.
- Write the commit message file; do not commit or push.

## Docs & ledger contract

Update only the affected `docs/WEB-CLIENT.md` §12B paragraphs: state shape/migration,
loyalty/rebellion/workforce/class gates, reputation prerequisites, season calculation,
technology sides/chronicle, UI, and the exact un-deferred inventory. If a new defect is
found, add the next free finding to `docs/AUDIT-FINDINGS.md` and handle it under the repo
contract.

Append-only ledger entries must include at least §H item #1 (the `/19`, `/9`, `/1` curve)
and every invented intermediate divisor, rebellion/recovery threshold, season duration or
multiplier, TD-loss denominator choice, forum temporal interpretation, class-gate
threshold, dark-side rating value, and political-reform fallback. Record any reform kept
deferred and the affected prerequisite closure. Key numerical defaults by JSON pointer.
Write the change-set message to `docs/commit-messages/<date>.md` (or the next same-day
suffix).

## Designer questions (pre-seeded)

1. §H #1: what are the intended intermediate workforce divisors between loyalty `−9`,
   `0`, and `+9`?
2. At what regional loyalty does rebellion begin and recover, and what player action can
   restore control?
3. Does `Столичный форум` multiply each incoming loyalty delta, immediately transform the
   current value, or modify the effective value only while operational? Does its progress
   acceleration ship in this phase, and with what exact behavior?
4. Which class-loyalty value and scope gates the smithy: city мещане, region мещане, or a
   combined value?
5. Which authored Народ card titles/sides map to which existing config IDs? Any
   placeholder or non-unique mapping stays deferred pending this answer.
6. How many cons compose each season, and does `×2` mean production, storage/food limit,
   consumption tolerance, or more than one of those?
7. For each conditional political reform, are all current config effects authoritative,
   especially technocracy's civil-war risk and theocracy's dark-experiment behavior?
8. What exact political/reputation consequences do both `card-hearts-5` faces implement,
   and does the raw export now define either `card-hearts-king` legitimacy face?
