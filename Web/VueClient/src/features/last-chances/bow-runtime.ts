import type { LastChancesVector } from './types'

export interface LastChancesBowObstacle {
  x: number
  y: number
  width: number
  height: number
}

export interface LastChancesBowArena {
  width: number
  height: number
  obstacles: readonly LastChancesBowObstacle[]
}

export type LastChancesBowSurfaceKind = 'obstacle' | 'boundary'

export interface LastChancesBowSurfaceImpact {
  /** Normalized time of impact along `start -> end`. */
  t: number
  /** Position of the moving circle's centre at impact. */
  point: LastChancesVector
  /** Unit surface normal pointing away from the hit surface. */
  normal: LastChancesVector
  kind: LastChancesBowSurfaceKind
  obstacleIndex?: number
}

export interface LastChancesBowCircleImpact {
  /** Normalized time of impact along `start -> end`. */
  t: number
  /** Position of the moving circle's centre at impact. */
  point: LastChancesVector
  /** Unit normal from the target centre toward the moving circle. */
  normal: LastChancesVector
}

export interface LastChancesBowChargeSource {
  charge?: {
    maxMs: number
  } | null
  tuning?: Readonly<Record<string, number>>
}

export interface LastChancesBowChargeResolution {
  /** Effective shot power, capped when `charge.maxMs` is reached. */
  powerProgress: number
  /** Lifetime of the held action, capped when `drawMaxHoldMs` is reached. */
  holdProgress: number
  /** Both authored endpoints count as successful golden releases. */
  inGoldenWindow: boolean
  /** Golden marker positions on the effective-power progress line. */
  goldStartRatio: number
  goldEndRatio: number
}

export interface LastChancesBowCadencePose {
  /** Visible string pull after the short post-shot release phase. */
  drawProgress: number
  /** Normalized 0→1→0 kick used to move the held bow backward. */
  recoil: number
  recoilDurationMs: number
}

interface SurfaceCandidate {
  t: number
  normal: LastChancesVector
}

const SWEEP_EPSILON = 1e-9

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.max(minimum, Math.min(maximum, value))
}

function finiteNonNegative(value: number, fallback = 0): number {
  return Number.isFinite(value) ? Math.max(0, value) : fallback
}

function pointAlong(
  start: LastChancesVector,
  end: LastChancesVector,
  t: number,
): LastChancesVector {
  return {
    x: start.x + (end.x - start.x) * t,
    y: start.y + (end.y - start.y) * t,
  }
}

function normalizedOr(
  vector: LastChancesVector,
  fallback: LastChancesVector,
): LastChancesVector {
  const length = Math.hypot(vector.x, vector.y)
  if (length <= SWEEP_EPSILON) return { ...fallback }
  return {
    x: vector.x / length,
    y: vector.y / length,
  }
}

function insideRectangleNormal(
  point: LastChancesVector,
  lowX: number,
  highX: number,
  lowY: number,
  highY: number,
): LastChancesVector {
  const faces = [
    { distance: point.x - lowX, normal: { x: -1, y: 0 } },
    { distance: highX - point.x, normal: { x: 1, y: 0 } },
    { distance: point.y - lowY, normal: { x: 0, y: -1 } },
    { distance: highY - point.y, normal: { x: 0, y: 1 } },
  ]
  return faces.reduce((nearest, candidate) => (
    candidate.distance < nearest.distance ? candidate : nearest
  )).normal
}

/**
 * Returns an outward normal when `point` starts inside the exact Minkowski sum of an
 * axis-aligned rectangle and a circle. Unlike a square-expanded AABB, the sum has
 * quarter-circle corners.
 */
function roundedRectangleOverlapNormal(
  point: LastChancesVector,
  radius: number,
  lowX: number,
  highX: number,
  lowY: number,
  highY: number,
): LastChancesVector | null {
  const closest = {
    x: clamp(point.x, lowX, highX),
    y: clamp(point.y, lowY, highY),
  }
  const offset = {
    x: point.x - closest.x,
    y: point.y - closest.y,
  }
  const distanceSquared = offset.x * offset.x + offset.y * offset.y
  if (distanceSquared > radius * radius + SWEEP_EPSILON) return null
  if (distanceSquared > SWEEP_EPSILON) {
    return normalizedOr(offset, { x: 1, y: 0 })
  }
  return insideRectangleNormal(point, lowX, highX, lowY, highY)
}

function firstQuadraticCircleEntry(
  start: LastChancesVector,
  end: LastChancesVector,
  centre: LastChancesVector,
  radius: number,
): number | null {
  const direction = {
    x: end.x - start.x,
    y: end.y - start.y,
  }
  const offset = {
    x: start.x - centre.x,
    y: start.y - centre.y,
  }
  const quadraticA = direction.x * direction.x + direction.y * direction.y
  if (quadraticA <= SWEEP_EPSILON) return null
  const quadraticB = 2 * (offset.x * direction.x + offset.y * direction.y)
  const quadraticC = offset.x * offset.x + offset.y * offset.y - radius * radius
  const discriminant = quadraticB * quadraticB - 4 * quadraticA * quadraticC
  if (discriminant < -SWEEP_EPSILON) return null
  const root = (-quadraticB - Math.sqrt(Math.max(0, discriminant))) / (2 * quadraticA)
  if (root < -SWEEP_EPSILON || root > 1 + SWEEP_EPSILON) return null
  return clamp(root, 0, 1)
}

/**
 * Exact continuous collision against a rectangle dilated by `radius`: four offset
 * face segments plus four quarter-circle corner arcs.
 */
function sweepCircleAgainstRectangle(
  start: LastChancesVector,
  end: LastChancesVector,
  radius: number,
  lowX: number,
  highX: number,
  lowY: number,
  highY: number,
): SurfaceCandidate | null {
  const initialNormal = roundedRectangleOverlapNormal(
    start,
    radius,
    lowX,
    highX,
    lowY,
    highY,
  )
  if (initialNormal) return { t: 0, normal: initialNormal }

  const direction = {
    x: end.x - start.x,
    y: end.y - start.y,
  }
  const candidates: SurfaceCandidate[] = []
  const addFaceCandidate = (
    t: number,
    crossAxis: number,
    crossLow: number,
    crossHigh: number,
    normal: LastChancesVector,
  ) => {
    if (t < -SWEEP_EPSILON || t > 1 + SWEEP_EPSILON) return
    if (crossAxis < crossLow - SWEEP_EPSILON
      || crossAxis > crossHigh + SWEEP_EPSILON) return
    if (direction.x * normal.x + direction.y * normal.y > SWEEP_EPSILON) return
    candidates.push({ t: clamp(t, 0, 1), normal })
  }

  if (direction.x > SWEEP_EPSILON) {
    const t = (lowX - radius - start.x) / direction.x
    addFaceCandidate(t, start.y + direction.y * t, lowY, highY, { x: -1, y: 0 })
  } else if (direction.x < -SWEEP_EPSILON) {
    const t = (highX + radius - start.x) / direction.x
    addFaceCandidate(t, start.y + direction.y * t, lowY, highY, { x: 1, y: 0 })
  }
  if (direction.y > SWEEP_EPSILON) {
    const t = (lowY - radius - start.y) / direction.y
    addFaceCandidate(t, start.x + direction.x * t, lowX, highX, { x: 0, y: -1 })
  } else if (direction.y < -SWEEP_EPSILON) {
    const t = (highY + radius - start.y) / direction.y
    addFaceCandidate(t, start.x + direction.x * t, lowX, highX, { x: 0, y: 1 })
  }

  if (radius > SWEEP_EPSILON) {
    const corners = [
      { centre: { x: lowX, y: lowY }, xSign: -1, ySign: -1 },
      { centre: { x: highX, y: lowY }, xSign: 1, ySign: -1 },
      { centre: { x: lowX, y: highY }, xSign: -1, ySign: 1 },
      { centre: { x: highX, y: highY }, xSign: 1, ySign: 1 },
    ]
    corners.forEach(({ centre, xSign, ySign }) => {
      const t = firstQuadraticCircleEntry(start, end, centre, radius)
      if (t === null) return
      const point = pointAlong(start, end, t)
      const offset = {
        x: point.x - centre.x,
        y: point.y - centre.y,
      }
      if (offset.x * xSign < -SWEEP_EPSILON
        || offset.y * ySign < -SWEEP_EPSILON) return
      const normal = normalizedOr(offset, { x: xSign, y: ySign })
      if (direction.x * normal.x + direction.y * normal.y > SWEEP_EPSILON) return
      candidates.push({ t, normal })
    })
  }

  return candidates.reduce<SurfaceCandidate | null>((best, candidate) => (
    !best || candidate.t < best.t - SWEEP_EPSILON ? candidate : best
  ), null)
}

function boundaryImpact(
  start: LastChancesVector,
  end: LastChancesVector,
  radius: number,
  arena: LastChancesBowArena,
): LastChancesBowSurfaceImpact | null {
  const lowX = radius
  const highX = arena.width - radius
  const lowY = radius
  const highY = arena.height - radius

  const outsideNormal = start.x < lowX - SWEEP_EPSILON
    ? { x: 1, y: 0 }
    : start.x > highX + SWEEP_EPSILON
      ? { x: -1, y: 0 }
      : start.y < lowY - SWEEP_EPSILON
        ? { x: 0, y: 1 }
        : start.y > highY + SWEEP_EPSILON
          ? { x: 0, y: -1 }
          : null
  if (outsideNormal) {
    return {
      t: 0,
      point: { ...start },
      normal: outsideNormal,
      kind: 'boundary',
    }
  }

  const direction = {
    x: end.x - start.x,
    y: end.y - start.y,
  }
  const candidates: Array<{ t: number, normal: LastChancesVector }> = []
  if (direction.x < -SWEEP_EPSILON) {
    candidates.push({ t: (lowX - start.x) / direction.x, normal: { x: 1, y: 0 } })
  } else if (direction.x > SWEEP_EPSILON) {
    candidates.push({ t: (highX - start.x) / direction.x, normal: { x: -1, y: 0 } })
  }
  if (direction.y < -SWEEP_EPSILON) {
    candidates.push({ t: (lowY - start.y) / direction.y, normal: { x: 0, y: 1 } })
  } else if (direction.y > SWEEP_EPSILON) {
    candidates.push({ t: (highY - start.y) / direction.y, normal: { x: 0, y: -1 } })
  }

  const earliest = candidates
    .filter(candidate => candidate.t >= -SWEEP_EPSILON && candidate.t <= 1 + SWEEP_EPSILON)
    .reduce<{ t: number, normal: LastChancesVector } | null>((best, candidate) => (
      !best || candidate.t < best.t - SWEEP_EPSILON ? candidate : best
    ), null)
  if (!earliest) return null
  const t = clamp(earliest.t, 0, 1)
  return {
    t,
    point: pointAlong(start, end, t),
    normal: earliest.normal,
    kind: 'boundary',
  }
}

/**
 * Finds the first solid-surface impact of a circle travelling through an arena.
 * Obstacles use exact rounded-corner Minkowski rectangles, while arena boundaries use
 * inset planes. The returned point is never a stepped approximation.
 */
export function sweepLastChancesCircleAgainstArena(
  start: LastChancesVector,
  end: LastChancesVector,
  radius: number,
  arena: LastChancesBowArena,
): LastChancesBowSurfaceImpact | null {
  const safeRadius = finiteNonNegative(radius)
  let earliest = boundaryImpact(start, end, safeRadius, arena)

  arena.obstacles.forEach((obstacle, obstacleIndex) => {
    const lowX = Math.min(obstacle.x, obstacle.x + obstacle.width)
    const highX = Math.max(obstacle.x, obstacle.x + obstacle.width)
    const lowY = Math.min(obstacle.y, obstacle.y + obstacle.height)
    const highY = Math.max(obstacle.y, obstacle.y + obstacle.height)
    const impact = sweepCircleAgainstRectangle(
      start,
      end,
      safeRadius,
      lowX,
      highX,
      lowY,
      highY,
    )
    if (!impact || (earliest && impact.t >= earliest.t - SWEEP_EPSILON)) return
    earliest = {
      t: impact.t,
      point: pointAlong(start, end, impact.t),
      normal: impact.normal,
      kind: 'obstacle',
      obstacleIndex,
    }
  })

  return earliest
}

/**
 * Exact quadratic time of impact for one moving and one stationary circle.
 * Existing overlap is deliberately reported at t=0 so a fast projectile cannot tunnel
 * through a target merely because the frame began with intersecting radii.
 */
export function sweepLastChancesCircleAgainstCircle(
  start: LastChancesVector,
  end: LastChancesVector,
  movingRadius: number,
  target: LastChancesVector,
  targetRadius: number,
): LastChancesBowCircleImpact | null {
  const combinedRadius = finiteNonNegative(movingRadius) + finiteNonNegative(targetRadius)
  const offset = {
    x: start.x - target.x,
    y: start.y - target.y,
  }
  const direction = {
    x: end.x - start.x,
    y: end.y - start.y,
  }
  const radiusSquared = combinedRadius * combinedRadius
  const initialDistanceSquared = offset.x * offset.x + offset.y * offset.y
  if (initialDistanceSquared <= radiusSquared + SWEEP_EPSILON) {
    return {
      t: 0,
      point: { ...start },
      normal: normalizedOr(offset, normalizedOr({
        x: -direction.x,
        y: -direction.y,
      }, { x: 1, y: 0 })),
    }
  }

  const quadraticA = direction.x * direction.x + direction.y * direction.y
  if (quadraticA <= SWEEP_EPSILON) return null
  const quadraticB = 2 * (offset.x * direction.x + offset.y * direction.y)
  const quadraticC = initialDistanceSquared - radiusSquared
  const discriminant = quadraticB * quadraticB - 4 * quadraticA * quadraticC
  if (discriminant < -SWEEP_EPSILON) return null
  const root = (-quadraticB - Math.sqrt(Math.max(0, discriminant))) / (2 * quadraticA)
  if (root < -SWEEP_EPSILON || root > 1 + SWEEP_EPSILON) return null

  const t = clamp(root, 0, 1)
  const point = pointAlong(start, end, t)
  return {
    t,
    point,
    normal: normalizedOr({
      x: point.x - target.x,
      y: point.y - target.y,
    }, normalizedOr({
      x: -direction.x,
      y: -direction.y,
    }, { x: 1, y: 0 })),
  }
}

/** Reflects a velocity around an authored unit normal: v - 2 dot(v, n) n. */
export function reflectLastChancesVector(
  velocity: LastChancesVector,
  normal: LastChancesVector,
): LastChancesVector {
  const dot = velocity.x * normal.x + velocity.y * normal.y
  return {
    x: velocity.x - 2 * dot * normal.x,
    y: velocity.y - 2 * dot * normal.y,
  }
}

/**
 * Resolves the Bow's two simultaneous clocks: power reaches its cap at `charge.maxMs`,
 * while the held action may continue draining stamina until `drawMaxHoldMs`.
 */
export function resolveLastChancesBowCharge(
  attack: LastChancesBowChargeSource,
  heldMs: number,
): LastChancesBowChargeResolution {
  const powerMaxMs = finiteNonNegative(attack.charge?.maxMs ?? 0)
  const authoredHoldMaxMs = attack.tuning?.drawMaxHoldMs
  const holdMaxMs = typeof authoredHoldMaxMs === 'number'
    && Number.isFinite(authoredHoldMaxMs)
    && authoredHoldMaxMs > 0
    ? authoredHoldMaxMs
    : powerMaxMs
  const clampedHeldMs = clamp(finiteNonNegative(heldMs), 0, holdMaxMs)
  const authoredGoldStartMs = attack.tuning?.goldStartMs
  const authoredGoldEndMs = attack.tuning?.goldEndMs
  const hasGoldenWindow = typeof authoredGoldStartMs === 'number'
    && Number.isFinite(authoredGoldStartMs)
    && typeof authoredGoldEndMs === 'number'
    && Number.isFinite(authoredGoldEndMs)
  const rawGoldStartMs = finiteNonNegative(authoredGoldStartMs ?? 0)
  const rawGoldEndMs = finiteNonNegative(
    authoredGoldEndMs ?? rawGoldStartMs,
    rawGoldStartMs,
  )
  const goldStartMs = Math.min(rawGoldStartMs, rawGoldEndMs)
  const goldEndMs = Math.max(rawGoldStartMs, rawGoldEndMs)

  return {
    powerProgress: powerMaxMs > 0 ? clamp(clampedHeldMs / powerMaxMs, 0, 1) : 0,
    holdProgress: holdMaxMs > 0 ? clamp(clampedHeldMs / holdMaxMs, 0, 1) : 0,
    inGoldenWindow: hasGoldenWindow
      && clampedHeldMs >= goldStartMs
      && clampedHeldMs <= goldEndMs,
    goldStartRatio: powerMaxMs > 0 ? clamp(goldStartMs / powerMaxMs, 0, 1) : 0,
    goldEndRatio: powerMaxMs > 0 ? clamp(goldEndMs / powerMaxMs, 0, 1) : 0,
  }
}

/**
 * Rapid fire owns a short recoil inside every authored shot interval, then visibly draws again.
 * Keeping the recoil below half an interval prevents a 120 ms Чреда from looking permanently
 * slack merely because the ordinary single-shot recoil lasts 170 ms.
 */
export function resolveLastChancesBowCadencePose(
  shotAccumulatorMs: number,
  intervalMs: number,
  shotAgeMs: number,
): LastChancesBowCadencePose {
  const safeIntervalMs = Math.max(1, finiteNonNegative(intervalMs, 1))
  const cycleProgress = clamp(finiteNonNegative(shotAccumulatorMs) / safeIntervalMs, 0, 1)
  const recoilDurationMs = Math.min(44, safeIntervalMs * 0.35)
  const safeShotAgeMs = finiteNonNegative(shotAgeMs, Number.POSITIVE_INFINITY)
  const recoiling = safeShotAgeMs <= recoilDurationMs
  const recoilProgress = recoiling
    ? clamp(safeShotAgeMs / Math.max(1, recoilDurationMs), 0, 1)
    : 1
  const baseDraw = 0.18 + 0.77 * Math.pow(cycleProgress, 0.82)
  return {
    drawProgress: baseDraw * (recoiling ? 0.12 + recoilProgress * 0.88 : 1),
    recoil: recoiling ? Math.sin(recoilProgress * Math.PI) : 0,
    recoilDurationMs,
  }
}

/**
 * Produces deterministic offsets spanning the full fan, centred around zero.
 * For example, three arrows over 20 degrees yield [-10°, 0°, 10°].
 */
export function lastChancesCenteredFanOffsets(
  count: number,
  fanDegrees: number,
): number[] {
  const safeCount = Number.isFinite(count) ? Math.max(0, Math.floor(count)) : 0
  if (safeCount === 0) return []
  if (safeCount === 1) return [0]
  const fanRadians = (Number.isFinite(fanDegrees) ? fanDegrees : 0) * Math.PI / 180
  const step = fanRadians / (safeCount - 1)
  return Array.from({ length: safeCount }, (_, index) => -fanRadians / 2 + step * index)
}
