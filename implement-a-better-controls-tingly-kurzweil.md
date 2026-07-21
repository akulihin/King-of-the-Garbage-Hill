# 99LC DualSense Rework: Per-Weapon Haptics + Adaptive Triggers

## Context

The 99 Last Chances mini-game (`Web/VueClient/src/features/last-chances/`, config `Web/VueClient/public/99lc/game-config.json`, docs `docs/WEB-CLIENT.md` §12A) has DualSense support that "rumbles all the time instead of indicating charges" and feels sloppy. Verified root causes:

1. **Tier-2 rumble literally never stops** — `DualSenseWebHidDriver.play()` (`dualsense-hid.ts:217-239`) writes one HID report and never writes a motor-off packet after `durationMs`; motors stay energized until the next effect. New finding **M119**.
2. **Long "state" profiles buzz on every trigger move** — every recognizer node advance emits at `engine.ts:2273` with `strength: event.value`; profiles `ramp`=600ms / `tension`=520ms / `gate`=320ms (game-config.json `input.dualsense.feedback.profiles`) chain into near-continuous rumble.
3. **All 7 weapons share the same 10 generic profiles** — nothing distinguishes weapons or charge bands by touch. Single-shot effects only; no pattern support.
4. **Trigger resistance exists only while an effect plays** — no persistent "armed" trigger state, so gates aren't physically felt.

User decisions: spider-knife wriggle **escalates as durability drops** (overrides the old contract note "durability stays visual-only" in `plans/99lc/control-scheme-test.md:271`); scope = **haptics + trigger feel** (no button remapping). Per-weapon trigger personalities follow the existing contract table (`plans/99lc/control-scheme-test.md:266-274`): spear="lance gearbox", chain="tension spool", claws="predator spring", knife-spider="ratcheting impale", axe="grapple lever", katana="draw-and-flow rail", sword="opening breaker".

Compat: all new config fields optional with defaults — **no schemaVersion bump** (v4 stays; `migrateLastChancesConfig` passes same-version configs through, `config.ts:921-929`).

## Design summary

- **Patterns**: `DualSenseFeedbackController` gains multi-pulse patterns (`pattern: [{delayMs, durationMs, magnitude, hand?}]`) via an injectable `schedule` seam; each matured pulse enqueues as a normal single-shot effect through the existing priority queue. Higher-priority emit cancels remaining pulses. One active pattern at a time.
- **M119 fix**: after Tier-2 plays an effect, schedule a stop at `durationMs` that writes the **baseline** (motors off + current base trigger blocks) instead of full neutral.
- **Kill the buzz**: `engine.ts:2273` non-commit cues become short gate-entry **ticks** (~30ms) — one per node crossed, no `strength: event.value` scaling; `tension`/channel states become `tick: null` = trigger-resistance-only, zero motor. Retune shared profiles: `ramp` 600→160ms, `tension` 520→160ms, `gate` 320→140ms, magnitudes −30% (both game-config.json and `DEFAULT_ADAPTIVE_PROFILES` in `config.ts:82-173`).
- **Charge bands = pulse-count coding**: band 1/2/3 → 1/2/3 short pulses (40ms pulse, 70ms gap, rising magnitude) at the existing edge-triggered site `engine.ts:3642-3657`. Unambiguous by touch on both tiers.
- **Per-weapon authoring**: new optional `haptics` block on `weapon.controls.<set>.dualsense` + per-node `entryTick`: `{baseTrigger?, gateTick?, bandTick?, commitPattern?, wriggle?}`. Personalities data-authored in game-config.json for all 11 dualsense attack-set blocks.
- **Persistent trigger detents**: serializers gain `baseline(state)` (per-hand trigger block, motors 0, reusing `triggerBlock`/`neutralTriggerBlock`); driver gains `setBaseline`/`writeBaseline` with dedup. Engine pushes baseline on weapon equip / scheme change / enable, and moves the detent to the active node's profile as gates advance ("moving detent" gearbox feel); restores resting on release. Only verified HID modes 0x01/0x02/0x05.
- **Spider wriggle**: engine scheduler `updateSpiderKnifeWriggle(dt)` — random bursts of 1-3 pulses **alternating left/right hands from a random side**; interval lerps calm→panic `[2800,4600]→[320,720]ms` and magnitude `0.10→0.45` as `resource/maxResource` drops (curve exponent 1.6). Seeded via existing `createLastChancesRng` (`rng.ts:11`). New feedback state `'wriggle'`, priority 10 (below `click` 20) — never delays combat cues; any combat cue cancels an in-flight wriggle. Stops instantly on unequip/throw (auto-unequip at 0 durability, `engine.ts:3742-3751`), pause, blur, death, scheme change, feedback off. Excluded from Reduced mode automatically (`effectAllowedInReducedMode`).
- **Tier-1 hand mapping fix**: `StandardsGamepadHapticsOutput.play` (`feedback.ts:176-202`) currently always drives both motors; change to spatial-ish mapping matching Tier-2 semantics: `left`→strong only, `right`→weak only, `both`→strong+0.65×weak. Makes wriggle wobble even on Tier 1.
- **Threshold retune**: keep global thresholds (per-weapon overrides deferred); raise `releaseThreshold` 0.14→0.16 (still satisfies validation constraints) for crisper release dispatch. Crispness otherwise comes from detents + noise removal.

## Steps

**1. `feedback.ts` (+`types.ts`): scheduler, patterns, ticks, hand mapping**
- New types: `LastChancesFeedbackPulse`; event gains `pattern?` and `tick?: {durationMs, magnitude} | null`; add `'wriggle'` to `LAST_CHANCES_FEEDBACK_STATES` (`types.ts:125-132`).
- Injectable `schedule` on controller outputs (default `setTimeout`, returns cancel fn). Pattern playback + cancel-on-higher-priority. Tier-2 stop scheduling → `enhancedOutput.writeBaseline?.() ?? neutralize`. `setTriggerBaseline` passthrough. Relax `magnitude <= 0` guard (`feedback.ts:367`) for `tick: null` trigger-only effects (Tier-2 only). Priority 10 for `'wriggle'`. Tier-1 hand→motor change.
- Tests `feedback.spec.ts`: pattern order/timing with manual scheduler; stop fires at durationMs and is superseded by newer effect; higher-priority cancels pulses; wriggle refused while combat cue pending; trigger-only reaches Tier 2 only; updated Tier-1 mapping assertions (~line 361); off/neutralize/dispose cancel timers.

**2. `dualsense-serializers.ts` + `dualsense-hid.ts`: baseline**
- Serializer interface + both transports: `baseline({left, right})` — trigger blocks per hand, motors 0, same valid flags as neutral payload; BT via existing `packet()` CRC path.
- Driver: `setBaseline` (dedup deep-equal) / `writeBaseline` through the serialized `writer` chain; optional methods on `LastChancesEnhancedFeedbackOutput`.
- Tests: baseline byte layout USB/BT, dedup skip, in-flight ordering, failure demotes.

**3. `config.ts` + `types.ts` + `game-config.json`: schema + authored personalities**
- Types: `LastChancesWeaponHapticsDefinition {baseTrigger?, gateTick?, bandTick?, commitPattern?, wriggle?}`, `LastChancesGateTickDefinition`, `LastChancesWeaponWriggleDefinition`; node gains `entryTick?`.
- Validation in `validateAttackSetControls` (`config.ts:1974`): magnitudes ≤1, durations ≤ maxDurationMs, commitPattern ≤8 pulses / span ≤2000ms, wriggle interval min<max. Mirror into `ATTACK_SET_CONTROL_SEEDS` (`config.ts:247-530`) so Builder imports adopt them.
- game-config.json: retuned shared profiles; `releaseThreshold` 0.16; `haptics` + `entryTick` for all 11 dualsense blocks per the personality table (spear heavy escalating notches; chain tension-spool `tick:null` holds; claws crisp 18ms clicks; spider ratchet + `wriggle {calmIntervalMs:[2800,4600], panicIntervalMs:[320,720], calmMagnitude:0.10, panicMagnitude:0.45, pulseMs:60, pulsesPerBurst:[1,3], curveExponent:1.6}` — note spider's set key is `controls.primary`; axe massive 90ms commit; katana light 15ms; sword quiet-at-rest).
- Tests `config.spec.ts`: shipped config validates; absence valid; each invalid-field case; v1 import gains seeded haptics.

**4. `engine.ts`: cue rework**
- `engine.ts:2273`: emit tick (node `entryTick` → set `gateTick` → default `{30ms, 0.2}`), drop `strength: event.value`; tension/channel → `tick: null`. `engine.ts:3642-3657`: band pattern of (index+1) pulses, keep `bandLight/Medium/Strong` profile + once-per-band-per-hold rule. `engine.ts:2324`: attach the set's `commitPattern`. Leave blocked/followUp/parry/impact sites unchanged.
- Tests `engine.spec.ts` (existing `driveDualSenseTrigger` helpers): one tick per crossed gate, zero long motor events on a deep pull; 1/2/3-pulse band patterns once per hold; commit carries weapon pattern.

**5. `engine.ts`: baseline wiring**
- Push baseline at end of `rebuildWeapons()` (~4674), in `setControlScheme` (~796; neutral on leaving dualsense), after `enableDualSenseFeatures()` (~832); move detent on node advance (step-4 site); restore resting baseline on release (pressed→false in `updateHeldWeaponMechanics`, ~3633); clear in `cleanupControlInputs` (~5543).
- Tests: fake enhanced output records `setBaseline` calls — equip/advance/release/scheme-change sequences.

**6. `engine.ts`: spider wriggle**
- `spiderWriggle` state + `updateSpiderKnifeWriggle(deltaMs)` called from `update()`; guards (scheme/phase/pause/feedback-mode/weapon-with-wriggle-equipped); RNG seeded `` `${node seed}:wriggle:${generation}` ``; escalation lerp as designed; emits `{state:'wriggle', profile:'click', pattern}` with per-pulse alternating hands.
- Tests: fixed seed → deterministic schedule; escalation as resource drops; alternating hands; instant stop on pause/unequip/death/scheme change; silent under mylorik/off.

**7. Docs + finding + commit message**
- `docs/WEB-CLIENT.md` §12A: ¶218 (ticks, pulse-count coding, patterns, wriggle, Tier-1 mapping), ¶220 (baseline packets, scheduled motor-stop, coalescing), ¶210 (living-knife wriggle escalates with durability), ¶222 (haptics blocks in Builder; v4 browser-override caveat).
- `plans/99lc/control-scheme-test.md`: tactile grammar 213-221 (tick + wriggle rows; bands = n pulses), state machine 236-254 (persistent baseline/detents), personality table 266-274 (authored numbers; rewrite Knife-spider cell — wriggle supersedes "durability stays visual"), routing matrix intro.
- `docs/AUDIT-FINDINGS.md`: add **M119** (Tier-2 effects never stop), mark fixed same change; update summary count.
- Commit message → `docs/commit-messages/2026-07-18.md` (next free suffix if taken). **No git commit** (user commits).

## Verification

1. `cd Web/VueClient && pnpm test:99lc` — all 13 spec files green.
2. `pnpm build` (type gate; `pnpm type-check` is broken env-wide).
3. `bash tools/verify-docs.sh --changed`.
4. Manual DualSense checklist (`?qa=1&fixture=controls`; `/99lc/dualsense-harness.html` for byte iteration):
   - Tier 1 USB: no continuous buzz on trigger sweep; 1/2/3 band ticks; 7 weapons distinguishable blind by commit signature; wriggle alternates motors and escalates as durability is spent.
   - Tier 2 USB: motors silent between cues (M119); per-weapon resting resistance; detent moves with gates; effects restore baseline, not off.
   - Tier 2 BT: input keeps flowing via WebHID reader during wriggle+combat overlap (write-rate check); disable/power-cycle recovers.
   - Reduced mode: only blocked/impact; Off: total silence incl. wriggle; pause/blur/death stop everything instantly.

## Risks

- BT write amplification (patterns + stops + baselines share one writer) — mitigated by dedup/coalescing; verify on hardware.
- Tier-1 `playEffect` promise timing varies by browser — pattern pacing uses the injected scheduler, never promise chaining.
- Tier-1 hand-mapping change alters feel of existing per-hand cues — deliberate consistency fix, play-check it.
- All new timing paths must use injected schedule/now/seeded RNG — no real `setTimeout` in specs.
- Existing v4 browser overrides keep old controls until re-applied (documented).
