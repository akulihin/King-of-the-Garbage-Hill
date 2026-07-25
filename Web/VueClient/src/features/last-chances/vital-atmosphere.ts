/**
 * Screen-wide ambience derived from the player's vitals, so the HUD numbers are also felt.
 *
 * Two independent channels:
 *  - draining stamina fogs the corners of the screen and shortens the breath cycle, the way
 *    vision narrows and breathing quickens when you are winded;
 *  - draining mental health pulls a purple vignette inward from every edge.
 *
 * Both are pure functions of the current ratios so the curve can be unit-tested and reused by
 * the DOM overlay and the canvas alike.
 */

export interface LastChancesVitalAtmosphere {
  /** Corner fog strength, 0 (rested) through 1 (spent). */
  fogIntensity: number
  /** One full breath in milliseconds. Slow while rested, urgent while spent. */
  breathPeriodMs: number
  /** Purple edge vignette strength, 0 (lucid) through 1 (collapsing). */
  vignetteIntensity: number
}

/** Above this share of stamina the player is not winded at all and the screen stays clear. */
const FOG_ONSET_RATIO = 0.6
/** Above this share of mental health nothing creeps in from the edges yet. */
const VIGNETTE_ONSET_RATIO = 0.8

const RESTED_BREATH_MS = 5200
const SPENT_BREATH_MS = 1400

function clamp01(value: number): number {
  if (!Number.isFinite(value)) return 0
  return Math.max(0, Math.min(1, value))
}

/** Eased 0..1 ramp so the effect fades in gently instead of snapping on at the threshold. */
function ramp(ratio: number, onset: number): number {
  if (onset <= 0) return 0
  const drained = clamp01((onset - clamp01(ratio)) / onset)
  return drained * drained * (3 - 2 * drained)
}

export function resolveLastChancesVitalAtmosphere(
  staminaRatio: number,
  mentalRatio: number,
): LastChancesVitalAtmosphere {
  const fogIntensity = ramp(staminaRatio, FOG_ONSET_RATIO)
  return {
    fogIntensity,
    breathPeriodMs: Math.round(
      RESTED_BREATH_MS - (RESTED_BREATH_MS - SPENT_BREATH_MS) * fogIntensity,
    ),
    vignetteIntensity: ramp(mentalRatio, VIGNETTE_ONSET_RATIO),
  }
}
