# 99LC DualSense pass 2: armed gates, telegraph rumble, push-through finishers

> **Supersedes the removed 2026-07-19 four-gate draft** (never implemented). That draft's fixed scope decisions carry over unchanged: depth selects charge band (no invented attacks — only existing authored gestures are routed); no perfect-release timing windows (bands stay monotonic, "release correctly" = release in the band the rumble names); sub-gate ticks are pure per-weapon texture with zero gameplay meaning. Its line anchors were stale (stamina, sword rhythm/fatigue rework, spear-v2 and the Fang all landed after it); every anchor below was re-verified on 2026-07-25 against schema v7.

## Context

The 99 Last Chances mini-game (`Web/VueClient/src/features/last-chances/`, shipped config `Web/VueClient/public/99lc/game-config.json`, spec `docs/WEB-CLIENT.md` §12A) already has a complete first DualSense haptics pass: a node ladder on gates `0.22/0.48/0.72/0.90`, discrete gate-entry ticks, pulse-count charge-band rumble, per-weapon `commitPattern` signatures, persistent trigger detents with the "moving detent" gearbox feel, the spider-knife wriggle, and a production WebHID Tier-2 transport.

What the trigger still cannot do — and what this pass adds:

1. **A ladder the hand can count** — gates move to `25% / 50% / 75% / full press`, and every rung fires a real action on release (most weapons currently use only 2–3 of the 4 gates, and several first rungs sit at the 0.22 activation floor where they are indistinguishable from "touched the trigger").
2. **Charge you can hear through your hands** — pulse-count band rumble stays, and depth now *floors* the band (`chargeBandOverrideId`), so a deeper pull is never weaker than the rumble promised.
3. **Press-to-gate-and-release as the core verb** — release at 25 / 50 / 75 / full are four distinct outcomes per set (where the weapon's identity supports four; see exceptions).
4. **Hold-at-gate telegraphs** — dwelling at a rung *arms* it and the rumble names what release will do (the action's signature, quietly), looping while you hold.
5. **Push-through finishers** — an armed rung can invite a push all the way down: a distinct invitation knock plays, the deep wall physically softens, and bottoming out fires a finisher that a raw fast pull cannot reach.
6. **Sub-gate depth ticks** — authored positional ticks (e.g. `0.60/0.85/0.90`) between gates: an approach ruler before deep walls and per-weapon texture (chain spool links, spider ratchet). Feedback-only, never gameplay.

Designer decisions fixed for this pass (2026-07-25): per-weapon exceptions are allowed where the four-rung grammar would fight a weapon's identity — **sword** keeps a 2-rung rhythm scheme, **Fang** gets a 1-rung cooldown-dagger scheme, and the **spider-knife's raw full press is blocked** so its self-destruct throw is reachable only through the armed push-through. All other sets get the full grammar.

## Verified current state (all anchors re-checked 2026-07-25)

| Fact | Where |
|---|---|
| Gates `0.22/0.48/0.72/0.90`, activation `0.22`, release `0.16`, hysteresis `0.06` | `config.ts:209-235` `DEFAULT_DUALSENSE_INPUT`; mirrored in `game-config.json` `input.dualsense` |
| Recognizer advances nodes by `maxValue`; release below `releaseThreshold` dispatches; `preGateGesture` quick action; illegal-pull blocked buzz; no dwell/arming, no armed-only branches, no depth ticks | `control-schemes.ts:394-648` `DualSenseControlRecognizer` |
| Node schema already has `requiredChargeBandId` (time-charge branch gate), `adaptiveOverride`, `entryTick` | `types.ts:382-402` `LastChancesDualSenseComboNodeDefinition` |
| Haptics block today: `baseTrigger`, `gateTick`, `bandTick`, `commitPattern`, `wriggle` | `types.ts:432-438` |
| Feedback states: `ready/charge/continuation/tension/blocked/impact/wriggle`; profile priorities `click 20 … impact 100`, `WRIGGLE_PRIORITY 10` | `types.ts:143-151`; `feedback.ts:133-147` |
| Detent plumbing: `pushTriggerBaseline` / `armTriggerDetent` / `releaseTriggerDetent` | `engine.ts:1084-1106`, called from `handleSemanticInput` at `engine.ts:3140,3198` |
| Charge guard refuses band-less charged attacks | `engine.ts:3178` |
| `requiredChargeBandId` readiness sites (**two** — both must be floored identically) | `engine.ts:3101-3104` and `engine.ts:7508-7511` |
| Band-edge pulse-count rumble (once per band per hold, `bandLight/Medium/Strong`) | `engine.ts:4837-4850` |
| Band resolution `.filter(minMs).at(-1)`-style selection | `weapon-runtime.ts:36` `resolveLastChancesChargedAttack` |
| Spider wriggle scheduler | `engine.ts:2894` `updateSpiderKnifeWriggle` |
| 9 weapons, 14 `controls.*.dualsense` blocks; Fang's block has **zero nodes** (trigger dead) | `game-config.json` `weapons[*].controls` |
| `'tap'` is a legal node gesture | `types.ts:2-8` `LAST_CHANCES_GESTURES` |
| Spear-v2 follow-up gate already uses `requiredChargeBandId: 'early'` | `docs/BALANCE-CONSTANTS.md` row 91; `game-config.json` `twohand-spear-v2.controls.primary` |

**Charge-band inventory** (floors below reference only these existing ids, plus two new append-only bands):
spear/spear-v2 `hold`: `early 650 / middle 1125 / late 1650`; spear `doubleTapHold`: `ram-short 650 / ram-strong 1050 / ram-max 1500`; spear `holdThenDoubleTap`: `spin-middle 1125 / spin-late 1650` (v2 adds `spin-early 650`); spear secondary `doubleTapHold`: `brace 650 / kick 1050`; chain `hold`: `hook-near 650 / hook-mid 1100 / hook-far 1550`; chain `doubleTapHold`: `wrap 650 / heave 1050`; claws `hold`: `claw-dash-short 650 / claw-dash-long 950`; spider `doubleTapHold`: `spider-throw-ready 650 / spider-throw-strain 1250`; axe `doubleTapHold`: `axe-aim 650 / axe-heave 1150`; axe secondary `holdThenDoubleTap`: `axe-leap-near 650 / axe-leap-far 1250`; katana `hold`: `katana-charge 650 / katana-full 1100`; katana secondary `hold`: `iaido-ready 650 / iaido-full 1050`. **Sword and Fang have no bands — deliberately kept that way.**

**Hard correctness rule (inherited):** `engine.ts:3178` refuses a charged attack that resolves to no band. Adding a `charge` block to a currently chargeless attack would therefore gate legacy/mylorik players out of it unless the block includes a `minMs: 0` neutral base band. This pass avoids the problem entirely: the only new bands (`axe-max`, `axe-leap-max`) are **appended to attacks that already have bands**.

## 1. The grammar: four rungs, armed pockets, push-through

Rungs (new defaults + shipped config): **R1 `0.25` / R2 `0.50` / R3 `0.75` / R4 `0.95`**, activation `0.25`. Release stays `0.16`, hysteresis `0.06` (`0.25 − 0.16 = 0.09 ≥ 0.06`, still valid). R4 at `0.95`, not literal `1.00`: worn triggers under-report and `startPosition ≤ endPosition` overrides must stay authorable — `0.95` *is* "pressed all the way down" as far as the hand can tell.

Every trigger pull resolves to exactly one of:

| Motion | Outcome | Feel |
|---|---|---|
| Quick click below R1, release | `preGateGesture` quick action (existing) | one soft click |
| Pull to rung N, release | rung N's action, band floored by the node's `chargeBandOverrideId` | gate tick per rung crossed; detent moves with you |
| Dwell at rung N ≥ `armMs` (default **450 ms**) | node **arms**: telegraph rumble names the release outcome, looping every **900 ms** while held | quiet signature preview — you know what you're holding |
| Armed + node authors a push-through branch | **invitation** double-knock (2×35 ms strong pulses), R4 wall softens via `armedTriggerOverride` | the weapon opens the door |
| Armed, push to R4 | the finisher (`entryRequiresArmed` node, press dispatch) | wall gives way, heavy commit signature |
| Raw fast pull to R4, release | R4's own release action — the weapon's "smash" | one coalesced sweep tick on the way down, then the commit |
| Crossing an authored depth tick (rising edge) | nothing (gameplay) | one texture tick — ruler/identity only |

Telegraph resolution chain: node `telegraph` pattern → else a **×0.4-magnitude echo of the outcome's `commitPattern`** (players learn signature = action for free) → else the band pulse-count at the currently floored band. Channel nodes (press-dispatch: chain hook, axe spin, spider flurry, spear stance, spear-v2 Прорыв) stay silent while channeling (`tension`/`tick: null` rule) — for them, arming only gates push-through legality and the invitation knock.

Arming definition: `activeNodeId` unchanged for ≥ `armMs` while the trigger stays above `releaseThreshold`. Time-based `requiredChargeBandId` (existing) and dwell-based `entryRequiresArmed` (new) are independent gates and may combine (katana's dance requires both the `katana-charge` band *and* an armed pocket).

Rumble stays **discrete, never continuous** — the first pass's core rule (M119) is preserved. Anti-noise rules: a sweep crossing ≥2 gates within **200 ms** plays one sweep tick instead of per-gate ticks; telegraph priority **15** (`TELEGRAPH_PRIORITY`, between `WRIGGLE_PRIORITY 10` and `click 20`) so any deliberate cue evicts it; depth ticks ride the `click` profile and are dropped by the priority queue when a gate tick lands the same frame; authored depth-tick positions must keep ≥ `0.03` distance from every gate value.

## 2. New schema fields (all optional → schemaVersion stays 7)

- `LastChancesDualSenseComboNodeDefinition` (`types.ts:382-402`) gains:
  - `armMs?: number` — dwell time to arm (default 450, global default in `input.dualsense`).
  - `entryRequiresArmed?: boolean` — node is eligible only while the current active node is armed.
  - `telegraph?: LastChancesFeedbackPulseDefinition[]` — authored armed-loop pattern.
  - `armedCue?: LastChancesFeedbackPulseDefinition[]` — one-shot invitation on arming (default double-knock when a push-through branch exists).
  - `armedTriggerOverride?: Partial<LastChancesAdaptiveTriggerProfileDefinition>` — detent applied on arming (the "wall softens").
  - `chargeBandOverrideId?: string` — depth floor for band resolution.
- `LastChancesWeaponHapticsDefinition` (`types.ts:432-438`) gains `depthTicks?: { position: number; tick: LastChancesGateTickDefinition }[]`.
- `LastChancesSemanticInputEvent` (`control-schemes.ts:16-40`) gains `phase: 'arm'` (new `LastChancesControlPhase` member, commit:false, feedback-only) and `depthTickIndex?: number`.
- `LAST_CHANCES_FEEDBACK_STATES` (`types.ts:143-151`) gains `'telegraph'`.
- `input.dualsense` gains `armMs?` and `telegraphPeriodMs?` global defaults.

## 3. Per-weapon control schemes

Legend: **dTap/dTH/hTDT** = doubleTap / doubleTapHold / holdThenDoubleTap; **floor X** = `chargeBandOverrideId: 'X'`; **PT** = push-through (R4 node with `entryRequiresArmed`); *ch* = press-dispatch channel node. Gate ticks, `baseTrigger`, `commitPattern` and `adaptiveOverride` ladders carry over from pass 1, repositioned onto the new grid.

| Set (personality) | R1 `0.25` | R2 `0.50` | R3 `0.75` | R4 `0.95` raw | R4 push-through | Depth ticks | Telegraphs |
|---|---|---|---|---|---|---|---|
| **spear:primary** (lance gearbox) | hold замах, floor `early` | hold замах, floor `middle` | dTH ram, floor `ram-short` | dTH ram, floor `ram-strong` | from armed R2/R3: hTDT spin, floor `spin-middle` | `.85 .90` approach | R1/R2 band count; R3 ram echo |
| **spear-v2:primary** (instant lance) | hold Заколоть, floor `early` | hold Заколоть, floor `middle` | dTH Прорыв *ch* (run accelerates while held deep) | hold, floor `late` | from armed R2: hTDT spin (`requiredChargeBandId: 'early'` kept), floor `spin-middle` | `.85 .90` | R1/R2 band count; R3 silent channel |
| **spear/v2:secondary** (pole brace) | hold stance *ch* (silent tension) | dTH kick, floor `brace` | dTH kick, floor `kick` | dTH kick, floor `kick` | from armed R1 stance: hTDT vault | `.85` pole-plant | R2/R3 kick echo |
| **chain:primary** (tension spool) | hold hook *ch*, floor `hook-near` | dTap spin (press) | dTH throw, floor `wrap` (spin ctx) | dTH throw, floor `heave` | tether ctx, armed: hTDT bind (press) | `.35 .60 .85` spool links | R3 throw echo; R1 silent |
| **claws:primary** (predator spring) | dTap rend | hold dash, floor `claw-dash-short` | dTH disarm | hold dash, floor `claw-dash-long` | from armed R2/R3: hTDT deep strike (dash ctx, press) | none — crisp identity | single sharp arm tick only |
| **spider-knife:primary** (ratcheting impale) | dTap impale | hold flurry *ch* | hTDT twist (flurry ctx) | **blocked buzz** — throw is unreachable raw | **armed-only**: dTH throw, floor `spider-throw-ready`; wriggle spikes to panic tier while armed | `.35 .60 .85 .90` ratchet | R3 twist echo; throw-armed = the knife *screams* |
| **axe:primary** (grapple lever) | dTap grapple (press) | dTH throw, floor `axe-aim` (grapple ctx) | dTH throw, floor `axe-heave` | dTH throw, floor `axe-heave` | from armed R3: dTH throw, floor **`axe-max`** *(new band)* | `.90` near-lock warning | R2/R3 band count |
| **axe:secondary** (flywheel) | hold spin *ch* (reflects during spin) | hTDT leap, floor `axe-leap-near` (spin ctx) | hTDT leap, floor `axe-leap-far` | hTDT leap, floor `axe-leap-far` | from armed R1 spin: hTDT leap, floor **`axe-leap-max`** *(new band)* | `.60 .85` flywheel notches | R2/R3 band count; R1 silent |
| **katana:primary** (draw-and-flow rail) | dTap overhead | hold charge, floor `katana-charge` | dTH flurry (continuation) | hold, floor `katana-full` | from armed R2 (`requiredChargeBandId: 'katana-charge'` + armed): hTDT dance (press) | none — smooth rail | R2 band count; dance invitation is the katana's one loud moment |
| **katana:secondary** | dTap hop | hold iaido, floor `iaido-ready` | dTH hop-slash (continuation) | hold, floor `iaido-full` | from armed R2: hTDT flash (press) | none | mirror of primary |
| **sword:primary & secondary** (opening breaker — *rhythm exception*) | dTap Oberhau (press) | — climb zone | dTH Unterhau (continuation, release) | — | — | `.60 .88` climb to the breaker | armed R3: Unterhau echo |
| **fang:primary** (coiled snake — *cooldown exception*) | `tap` thrust (press) | — wall | — | — | — | none | cooldown feel (below) |

**Deliberate exceptions** (designer-approved 2026-07-25):
- **Sword** keeps 2 rungs. Its identity is *timing*: a tap faster than `rhythmPerfectStartMs` is a miss, and Unterhau is a follow-up (`doubleTapHold` is not standalone). Depth charge floors would fight the rhythm/fatigue mechanic, so sword gets **no new bands** — but the full haptic grammar still applies: `.60/.88` depth ticks climb toward the breaker, and dwelling on the primed Unterhau node telegraphs its echo.
- **Fang** is a 5-second-cooldown single-stab dagger. One node (`tap`, press dispatch, `0.25`), stiff `baseTrigger` wall above it. Its haptic identity is the **cooldown**: while the Tap is cooling the resting trigger block is heavy (the snake is coiled and won't strike), at ready-edge the resistance relaxes and a soft `ready` click plays. Its `commitPattern` gains one tail pulse per 5 accumulated +5%-damage stacks (cap +4 pulses) — "the snake grows heavier". Pulling against the cooldown wall and releasing = the existing blocked cue.
- **Spider-knife raw R4 is blocked.** The throw destroys the weapon; it must never fire from a panic pull. The R4 throw node has `entryRequiresArmed`, and with no plain R4 node the raw deep pull hits the recognizer's existing illegal-pull blocked path. While the throw is armed, the wriggle scheduler is forced to its panic tier — the knife knows what you're about to do.

**New charge bands (append-only):** `axe-max` after `axe-heave` (`twohand-axe.attacks.doubleTapHold`), `axe-leap-max` after `axe-leap-far` (`twohand-axe.secondaryAttacks.holdThenDoubleTap`). Multipliers follow each weapon's existing escalation shape (~+25–35% over the previous band, stamina cost scaled the same way) — tune against the neighbouring rows in `docs/BALANCE-CONSTANTS.md`, don't invent a new curve. No `minMs: 0` base needed: both attacks already have bands.

## 4. Implementation steps (edit order = dependency order)

**1. `types.ts`** — all schema additions from §2.

**2. `control-schemes.ts` — recognizer** (`DualSenseControlRecognizer`):
- `DualSenseTriggerState` gains `armedNodeId: string | null` and `nodeEnteredAt: number`. In `update()` (per-frame, `:617-629`): if `activeNodeId` stable for ≥ node's `armMs` and value > `releaseThreshold` and not yet armed → set armed, emit one `phase:'arm'` event (commit:false) carrying the node.
- Eligibility filter (`:493-511`): a node with `entryRequiresArmed` is eligible only when `state.armedNodeId === state.activeNodeId !== null`; tie-break at equal `activationThreshold`: armed-required node wins while armed, is skipped otherwise.
- Depth ticks: capture `previous = state.value` before assignment (`:437`); after the active-guard, for each authored `haptics.depthTicks` entry with `previous < position ≤ normalized`, emit a commit:false event with `depthTickIndex` (rising edge only, once per pull per tick). Keyboard Q/E emulation funnels through `updateTrigger`, so one path covers both sources.
- Sweep coalescing: recognizer tracks gate-tick emissions per pull; when a second node advance lands within 200 ms of pull start, its (and subsequent) entry events carry a `coalesced` flag the engine maps to the single sweep tick.

**3. `weapon-runtime.ts`** — `resolveLastChancesChargedAttack` (`:36`) gains optional `minBandId?: string`: after time-based selection, if `minBandId` names a band in the sorted list, return whichever of the two sits later in that list. All other callers unchanged.

**4. `engine.ts`**:
- `handleSemanticInput` (`:3030`): handle `phase:'arm'` — start the telegraph loop (scheduler pattern mirroring `updateSpiderKnifeWriggle` `:2894`: replay every `telegraphPeriodMs` while the recognizer snapshot still reports the same armed node), play `armedCue` when a push-through branch exists, and re-arm the detent via `armTriggerDetent` (`:1092`) with `armedTriggerOverride`. Handle `depthTickIndex` events → `{state:'charge', profile:'click'}` tick. Stop the loop on release/advance/cancel/pause/blur/death/scheme-change/feedback-off (same guard list the wriggle uses).
- Thread the active node's `chargeBandOverrideId` into **both** readiness sites (`:3101-3104`, `:7508-7511`) and the real resolution behind `:3178`/`:3683` via the new `minBandId` param — a floored rung must pass readiness and resolve identically.
- Band-edge rumble (`:4837-4850`): compute the shown band as the later of the time band and the active node's floor, so the pulse count always matches what release will do.
- Spider: while the throw node is armed, force the wriggle scheduler to its panic-tier interval/magnitude.
- Fang: on Tap cooldown edges, swap the resting baseline between the authored stiff `baseTrigger` and a relaxed block (`pushTriggerBaseline` `:1084`), and emit a soft `ready` click at ready-edge; append stack tail pulses when building its commit pattern.
- `updateKeyboardDualSenseTriggers`: verify Q/E ramps still reach `0.95`.
- Sweep tick: map `coalesced` node-entry events to one `gateTick` at sweep end instead of per-node ticks.

**5. `feedback.ts`** — `'telegraph'` state with `TELEGRAPH_PRIORITY = 15` alongside `WRIGGLE_PRIORITY` (`:146-147`); telegraph loops are cancelled by any higher-priority emit (existing pattern-eviction path at `:404-465`); telegraph refused outright while a combat cue is pending (same rule as wriggle).

**6. `config.ts`**:
- `DEFAULT_DUALSENSE_INPUT` (`:209-235`): activation `0.25`, gates `0.25/0.50/0.75/0.95`, `armMs: 450`, `telegraphPeriodMs: 900`.
- `DEFAULT_ADAPTIVE_PROFILES`: shift gate-anchored starts to the new grid (`gate` start `0.48→0.50`, end `0.78→0.80`; `ramp` start `0.22→0.25`).
- Seeds (`ATTACK_SET_CONTROL_SEEDS` + `dualSenseNode`): accept the new node fields; apply the §3 table so Builder imports adopt it.
- Validation (`validateAttackSetControls`): `armMs` 100–2000; `depthTicks` 1–8 entries, positions strictly increasing, each ≥0.03 from every gate; `telegraph`/`armedCue` like `commitPattern` (≤8 pulses, span ≤2000 ms); `armedTriggerOverride` via the existing partial-profile validator; `chargeBandOverrideId` must name a band of the node's own gesture's attack; `entryRequiresArmed` requires a `next`-path predecessor that can arm (i.e. the node is not a start node); at most one `entryRequiresArmed` node per threshold per set.
- Graph checks: reachability/ambiguity key on `entryContext|activationThreshold` — the armed-required R4 node coexisting with a plain R4 node needs the armed flag folded into that ambiguity key.

**7. `public/99lc/game-config.json`** — mirror everything: `input.dualsense` thresholds/gates/`armMs`/`telegraphPeriodMs`, the two profile retunes, all 14 `controls.*.dualsense` blocks per §3 (node thresholds, floors, `entryRequiresArmed` R4 nodes, telegraphs, armed cues/overrides, depth ticks, Fang's new single-node block), and the two new axe bands.

**8. Docs + finding + commit message** — see §5.

## 5. Documentation contract (same change-set, per CLAUDE.md)

- **`docs/WEB-CLIENT.md` §12A** — the two DualSense paragraphs: grammar paragraph gains armed pockets / telegraph loop / invitation + wall-softening / push-through / depth-tick ruler / sweep coalescing / spider armed-throw safety / Fang cooldown feel, keeping the "rumble is discrete, never continuous" sentence; the Tier-2 paragraph gains `armedTriggerOverride` in the moving-detent description.
- **`docs/BALANCE-CONSTANTS.md`** — row 78: activation `0.25`, release `0.16`, hysteresis `0.06`, gates `0.25/0.50/0.75/0.95`, `armMs 450`, `telegraphPeriodMs 900`; rows 80–82: retuned gate/ramp anchors; new rows: depth ticks, telegraph/invitation caps, `axe-max`/`axe-leap-max` multipliers; row 91 stays true (spear-v2 `requiredChargeBandId: 'early'` is kept).
- **`docs/AUDIT-FINDINGS.md`** — add **M144**: row 78 documents release `0.14` / hysteresis `0.08` but the shipped config has had `0.16` / `0.06` since the first DualSense pass (doc drift). Mark fixed in the same change-set; update the summary count.
- **Commit message** → `docs/commit-messages/<date>.md` (next free `-N` suffix). **No `git commit` / `git push`** — the user commits.

## 6. Verification

Per designer instruction: **no new test/spec files.** Two existing spec files carry golden values the retune will break and must be retuned in place, not extended: `config.spec.ts` (shipped-route golden strings encode `0.22/0.48/0.72` thresholds and node counts) and `engine.spec.ts` (literal trigger drives at the old gate values and the baseline-detent assertions).

1. `cd Web/VueClient && pnpm build` — the type gate (`pnpm type-check` is broken env-wide; `pnpm lint` also currently crashes on any input).
2. `bash tools/verify-docs.sh --changed`.
3. Manual DualSense pass at `/99lc?qa=1&fixture=controls`, per weapon:
   - Four distinct press-to-rung-and-release outcomes on full-grammar sets; the band pulse-count always matches what release then does (floors included).
   - Dwell at each rung: telegraph loop names the outcome; it never plays while any combat cue is active.
   - Where authored: invitation knock → wall visibly softens → bottoming out fires the finisher; a raw fast pull instead lands the R4 raw action (spear ram, chain heave, katana full) — and on the spider, the blocked buzz, never the throw.
   - Sword: rhythm unaffected, `.60/.88` climb reads under the finger, Unterhau echo on the primed node. Fang: trigger heavy on cooldown, relaxes + clicks at ready, stack pulses audible in the commit.
   - Fast sweep bottom-to-top = one sweep tick, no tick storm; depth ticks feel like texture, not events.
   - Tier 1 (no WebHID): everything above minus trigger feel still reads by rumble alone; keyboard Q/E reaches `0.95`; Reduced mode = blocked/impact only; Off = total silence including telegraph and wriggle.
   - **Legacy and mylorik schemes play identically to before** (no new charge blocks on chargeless attacks; floors only apply through dualsense nodes).
4. `/99lc/dualsense-harness.html` for Tier-2 byte iteration if packets misbehave.

## 7. Risks

- **Floor threading is the one real correctness risk**: `chargeBandOverrideId` must reach both readiness sites (`engine.ts:3101`, `:7508`) *and* the resolution path, or a floored rung refuses or under-fires. Verify with the katana dance and axe-max paths specifically.
- Armed-required vs plain node at the same threshold is new routing ambiguity — the tie-break and the validation rule (≤1 armed-required node per threshold) must land together.
- Telegraph loops add a recurring scheduler; every guard the wriggle honors (pause/blur/death/scheme/feedback-off) must stop them, and specs use the injected scheduler — no real `setTimeout`.
- `0.95` may be hard on worn triggers — Builder-tunable; fall back to `0.92` if hardware testing complains.
- Spear-v2's Прокол/Прорыв/Заколоть gesture→attack wiring moved in the v2 rework; re-verify its `controls.primary` gesture mapping against `performSpearReleaseV2` before authoring its block.
- Existing browser-stored config overrides keep the old ladder until re-applied — pre-existing documented behavior, no migration.
- `engine.ts` line anchors drift fast in this file (9.6k lines, active development) — re-verify anchors at implementation time; symbols (`handleSemanticInput`, `armTriggerDetent`, `updateSpiderKnifeWriggle`, `resolveLastChancesChargedAttack`) are the stable handles.
