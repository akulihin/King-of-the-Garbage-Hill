import type {
  LastChancesAttackDefinition,
  LastChancesChargeBandDefinition,
} from './types'

export type LastChancesSpearReleaseStage = 'early' | 'middle' | 'late'

export interface LastChancesSpearChargeVisual {
  stage: LastChancesSpearReleaseStage
  previewBand: LastChancesChargeBandDefinition
  armed: boolean
  /** Height above the ordinary grip point, measured in rendered player radii. */
  liftRadii: number
  /** Positive stays in front of the hero; negative draws back behind them. */
  forwardRadii: number
  /** Screen-space vertical bias: positive points the spearhead toward the floor. */
  verticalTilt: number
}

interface SpearPoseKeyframe {
  liftRadii: number
  forwardRadii: number
  verticalTilt: number
}

const IDLE_POSE: SpearPoseKeyframe = {
  liftRadii: 0,
  forwardRadii: 0,
  verticalTilt: 0,
}

const RELEASE_POSES: Record<LastChancesSpearReleaseStage, SpearPoseKeyframe> = {
  early: {
    liftRadii: 1.45,
    forwardRadii: 0.58,
    verticalTilt: 0.18,
  },
  middle: {
    liftRadii: 1.95,
    forwardRadii: 0.08,
    verticalTilt: 0,
  },
  late: {
    liftRadii: 2.35,
    forwardRadii: -0.68,
    verticalTilt: -0.16,
  },
}

const MAXIMUM_WINDUP_POSE: SpearPoseKeyframe = {
  liftRadii: 2.48,
  forwardRadii: -0.84,
  verticalTilt: -0.2,
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.max(minimum, Math.min(maximum, value))
}

function smoothstep(progress: number): number {
  const clamped = clamp(progress, 0, 1)
  return clamped * clamped * (3 - 2 * clamped)
}

function interpolatePose(
  from: SpearPoseKeyframe,
  to: SpearPoseKeyframe,
  progress: number,
): SpearPoseKeyframe {
  const eased = smoothstep(progress)
  return {
    liftRadii: from.liftRadii + (to.liftRadii - from.liftRadii) * eased,
    forwardRadii: from.forwardRadii + (to.forwardRadii - from.forwardRadii) * eased,
    verticalTilt: from.verticalTilt + (to.verticalTilt - from.verticalTilt) * eased,
  }
}

function releaseBands(attack: LastChancesAttackDefinition): Record<
  LastChancesSpearReleaseStage,
  LastChancesChargeBandDefinition
> | null {
  const charge = attack.charge
  if (!charge) return null
  const early = charge.bands.find(band => band.id === 'early')
  const middle = charge.bands.find(band => band.id === 'middle')
  const late = charge.bands.find(band => band.id === 'late')
  return early && middle && late ? { early, middle, late } : null
}

/**
 * Converts the Spear's real release thresholds into one continuous authored pose.
 * The telegraph changes only when a release band is genuinely armed, while the
 * weapon itself eases toward the next pose throughout the charge.
 */
export function resolveLastChancesSpearChargeVisual(
  attack: LastChancesAttackDefinition,
  heldMs: number,
): LastChancesSpearChargeVisual | null {
  const bands = releaseBands(attack)
  const charge = attack.charge
  if (!bands || !charge) return null
  const clampedHeldMs = clamp(heldMs, 0, charge.maxMs)

  let pose: SpearPoseKeyframe
  if (clampedHeldMs < bands.early.minMs) {
    pose = interpolatePose(IDLE_POSE, RELEASE_POSES.early, clampedHeldMs / bands.early.minMs)
  } else if (clampedHeldMs < bands.middle.minMs) {
    pose = interpolatePose(
      RELEASE_POSES.early,
      RELEASE_POSES.middle,
      (clampedHeldMs - bands.early.minMs) / (bands.middle.minMs - bands.early.minMs),
    )
  } else if (clampedHeldMs < bands.late.minMs) {
    pose = interpolatePose(
      RELEASE_POSES.middle,
      RELEASE_POSES.late,
      (clampedHeldMs - bands.middle.minMs) / (bands.late.minMs - bands.middle.minMs),
    )
  } else {
    pose = interpolatePose(
      RELEASE_POSES.late,
      MAXIMUM_WINDUP_POSE,
      (clampedHeldMs - bands.late.minMs) / (charge.maxMs - bands.late.minMs),
    )
  }

  const stage: LastChancesSpearReleaseStage = clampedHeldMs >= bands.late.minMs
    ? 'late'
    : clampedHeldMs >= bands.middle.minMs ? 'middle' : 'early'
  const previewBand = bands[stage]
  return {
    stage,
    previewBand,
    armed: clampedHeldMs >= previewBand.minMs,
    ...pose,
  }
}
