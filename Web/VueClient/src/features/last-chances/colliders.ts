import type {
  LastChancesAttackDefinition,
  LastChancesVector,
} from './types'

export const LAST_CHANCES_COLLIDER_SHAPES = [
  'sector',
  'capsule',
  'circle',
  'sweep',
] as const

export type LastChancesColliderShape = typeof LAST_CHANCES_COLLIDER_SHAPES[number]

/**
 * Structural mirror of the collider definition carried by the weapon schema.
 * Keeping the resolver structural lets schema-v1 attacks continue to use their
 * executor-derived fallback shape while schema-v2+ attacks opt into exact geometry.
 */
export interface LastChancesAttackColliderDefinition {
  shape: LastChancesColliderShape
  innerRange?: number
  strictInnerRange?: boolean
  /** Missing/false means room obstacles occlude the collider. */
  passesThroughWalls?: boolean
  /** Full collider width for capsule and sweep shapes. */
  width?: number
  /** Static clockwise rotation from the supplied attack direction. */
  rotationDegrees?: number
}

export type LastChancesAttackWithCollider = LastChancesAttackDefinition & {
  collider?: LastChancesAttackColliderDefinition
}

export interface LastChancesRuntimeSectorCollider {
  shape: 'sector'
  origin: LastChancesVector
  direction: LastChancesVector
  innerRadius: number
  outerRadius: number
  halfArcRadians: number
  innerExclusion?: LastChancesRuntimeCircleCollider
}

export interface LastChancesRuntimeCapsuleCollider {
  shape: 'capsule'
  start: LastChancesVector
  end: LastChancesVector
  radius: number
  innerExclusion?: LastChancesRuntimeCircleCollider
}

export interface LastChancesRuntimeCircleCollider {
  shape: 'circle'
  center: LastChancesVector
  innerRadius: number
  outerRadius: number
  innerExclusion?: LastChancesRuntimeCircleCollider
}

export interface LastChancesRuntimeSweepCollider {
  shape: 'sweep'
  pivot: LastChancesVector
  start: LastChancesVector
  end: LastChancesVector
  radius: number
  angleRadians: number
  innerExclusion?: LastChancesRuntimeCircleCollider
}

export type LastChancesRuntimeCollider =
  | LastChancesRuntimeSectorCollider
  | LastChancesRuntimeCapsuleCollider
  | LastChancesRuntimeCircleCollider
  | LastChancesRuntimeSweepCollider

export interface LastChancesColliderTracePath {
  points: LastChancesVector[]
  closed: boolean
}

const EPSILON = 0.000001
const TAU = Math.PI * 2

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.max(minimum, Math.min(maximum, value))
}

function finiteOr(value: number | undefined, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
}

function vectorLength(value: LastChancesVector): number {
  return Math.hypot(value.x, value.y)
}

function normalize(
  value: LastChancesVector,
  fallback: LastChancesVector = { x: 1, y: 0 },
): LastChancesVector {
  const length = vectorLength(value)
  if (length <= EPSILON) return { ...fallback }
  return { x: value.x / length, y: value.y / length }
}

function rotate(value: LastChancesVector, radians: number): LastChancesVector {
  const cosine = Math.cos(radians)
  const sine = Math.sin(radians)
  return {
    x: value.x * cosine - value.y * sine,
    y: value.x * sine + value.y * cosine,
  }
}

function addScaled(
  origin: LastChancesVector,
  direction: LastChancesVector,
  distance: number,
): LastChancesVector {
  return {
    x: origin.x + direction.x * distance,
    y: origin.y + direction.y * distance,
  }
}

function distanceSquared(a: LastChancesVector, b: LastChancesVector): number {
  const x = a.x - b.x
  const y = a.y - b.y
  return x * x + y * y
}

function distanceToSegmentSquared(
  point: LastChancesVector,
  start: LastChancesVector,
  end: LastChancesVector,
): number {
  const segment = { x: end.x - start.x, y: end.y - start.y }
  const lengthSquared = segment.x * segment.x + segment.y * segment.y
  if (lengthSquared <= EPSILON) return distanceSquared(point, start)
  const projection = clamp(
    ((point.x - start.x) * segment.x + (point.y - start.y) * segment.y) / lengthSquared,
    0,
    1,
  )
  return distanceSquared(point, {
    x: start.x + segment.x * projection,
    y: start.y + segment.y * projection,
  })
}

function cross(
  origin: LastChancesVector,
  first: LastChancesVector,
  second: LastChancesVector,
): number {
  return (first.x - origin.x) * (second.y - origin.y)
    - (first.y - origin.y) * (second.x - origin.x)
}

function pointOnSegment(
  point: LastChancesVector,
  start: LastChancesVector,
  end: LastChancesVector,
): boolean {
  return Math.abs(cross(start, end, point)) <= EPSILON
    && point.x >= Math.min(start.x, end.x) - EPSILON
    && point.x <= Math.max(start.x, end.x) + EPSILON
    && point.y >= Math.min(start.y, end.y) - EPSILON
    && point.y <= Math.max(start.y, end.y) + EPSILON
}

function segmentsIntersect(
  firstStart: LastChancesVector,
  firstEnd: LastChancesVector,
  secondStart: LastChancesVector,
  secondEnd: LastChancesVector,
): boolean {
  const firstA = cross(firstStart, firstEnd, secondStart)
  const firstB = cross(firstStart, firstEnd, secondEnd)
  const secondA = cross(secondStart, secondEnd, firstStart)
  const secondB = cross(secondStart, secondEnd, firstEnd)
  if (((firstA > EPSILON && firstB < -EPSILON) || (firstA < -EPSILON && firstB > EPSILON))
    && ((secondA > EPSILON && secondB < -EPSILON) || (secondA < -EPSILON && secondB > EPSILON))) {
    return true
  }
  return (Math.abs(firstA) <= EPSILON && pointOnSegment(secondStart, firstStart, firstEnd))
    || (Math.abs(firstB) <= EPSILON && pointOnSegment(secondEnd, firstStart, firstEnd))
    || (Math.abs(secondA) <= EPSILON && pointOnSegment(firstStart, secondStart, secondEnd))
    || (Math.abs(secondB) <= EPSILON && pointOnSegment(firstEnd, secondStart, secondEnd))
}

function distanceBetweenSegmentsSquared(
  firstStart: LastChancesVector,
  firstEnd: LastChancesVector,
  secondStart: LastChancesVector,
  secondEnd: LastChancesVector,
): number {
  if (segmentsIntersect(firstStart, firstEnd, secondStart, secondEnd)) return 0
  return Math.min(
    distanceToSegmentSquared(firstStart, secondStart, secondEnd),
    distanceToSegmentSquared(firstEnd, secondStart, secondEnd),
    distanceToSegmentSquared(secondStart, firstStart, firstEnd),
    distanceToSegmentSquared(secondEnd, firstStart, firstEnd),
  )
}

function normalizeAngle(radians: number): number {
  let normalized = radians % TAU
  if (normalized > Math.PI) normalized -= TAU
  if (normalized < -Math.PI) normalized += TAU
  return normalized
}

function polarPoint(
  origin: LastChancesVector,
  radius: number,
  angle: number,
): LastChancesVector {
  return {
    x: origin.x + Math.cos(angle) * radius,
    y: origin.y + Math.sin(angle) * radius,
  }
}

function pointInsideAnnulus(
  distance: number,
  innerRadius: number,
  outerRadius: number,
): boolean {
  return distance + EPSILON >= innerRadius && distance <= outerRadius + EPSILON
}

function distanceToArc(
  point: LastChancesVector,
  origin: LastChancesVector,
  radius: number,
  facingAngle: number,
  halfArcRadians: number,
): number {
  const relative = {
    x: point.x - origin.x,
    y: point.y - origin.y,
  }
  const distance = vectorLength(relative)
  const pointAngle = distance <= EPSILON ? facingAngle : Math.atan2(relative.y, relative.x)
  const angleDelta = normalizeAngle(pointAngle - facingAngle)
  if (Math.abs(angleDelta) <= halfArcRadians + EPSILON) {
    return Math.abs(distance - radius)
  }
  const first = polarPoint(origin, radius, facingAngle - halfArcRadians)
  const second = polarPoint(origin, radius, facingAngle + halfArcRadians)
  return Math.sqrt(Math.min(distanceSquared(point, first), distanceSquared(point, second)))
}

function fallbackColliderShape(attack: LastChancesAttackDefinition): LastChancesColliderShape {
  if (attack.kind === 'burst') return 'circle'
  if (attack.kind === 'projectile' || attack.kind === 'dash') return 'capsule'
  return 'sector'
}

/**
 * Converts one authored attack into its current world-space collider.
 *
 * `progress` expands range-bearing shapes from innerRange to attack.range.
 * `sweepAngleDegrees` is an additional live rotation used by channelled spins;
 * it is applied only to sweep colliders, after authored rotationDegrees.
 */
export function resolveAttackCollider(
  origin: LastChancesVector,
  direction: LastChancesVector,
  attack: LastChancesAttackWithCollider,
  progress = 1,
  sweepAngleDegrees = 0,
): LastChancesRuntimeCollider {
  const collider = attack.collider
  const shape = collider?.shape ?? fallbackColliderShape(attack)
  const innerRange = Math.max(0, finiteOr(collider?.innerRange, 0))
  const authoredOuterRange = Math.max(innerRange, finiteOr(attack.range, innerRange))
  const normalizedProgress = clamp(finiteOr(progress, 1), 0, 1)
  const outerRange = innerRange + (authoredOuterRange - innerRange) * normalizedProgress
  const authoredRotation = finiteOr(collider?.rotationDegrees, 0)
  const liveRotation = shape === 'sweep' ? finiteOr(sweepAngleDegrees, 0) : 0
  const angleRadians = (authoredRotation + liveRotation) * Math.PI / 180
  const resolvedDirection = rotate(normalize(direction), angleRadians)
  const width = Math.max(0, finiteOr(collider?.width, Math.max(0, attack.radius) * 2))
  const innerExclusion = collider?.strictInnerRange && innerRange > EPSILON
    ? {
        shape: 'circle' as const,
        center: { ...origin },
        innerRadius: 0,
        outerRadius: innerRange,
      }
    : undefined

  if (shape === 'circle') {
    return {
      shape,
      center: { ...origin },
      innerRadius: innerRange,
      outerRadius: outerRange,
      ...(innerExclusion ? { innerExclusion } : {}),
    }
  }

  if (shape === 'sector') {
    return {
      shape,
      origin: { ...origin },
      direction: resolvedDirection,
      innerRadius: innerRange,
      outerRadius: outerRange,
      halfArcRadians: clamp(Math.max(0, attack.arcDegrees), 0, 360) * Math.PI / 360,
      ...(innerExclusion ? { innerExclusion } : {}),
    }
  }

  const start = addScaled(origin, resolvedDirection, innerRange)
  const end = addScaled(origin, resolvedDirection, outerRange)
  const radius = width / 2
  if (shape === 'sweep') {
    return {
      shape,
      pivot: { ...origin },
      start,
      end,
      radius,
      angleRadians: Math.atan2(resolvedDirection.y, resolvedDirection.x),
      ...(innerExclusion ? { innerExclusion } : {}),
    }
  }

  return {
    shape,
    start,
    end,
    radius,
    ...(innerExclusion ? { innerExclusion } : {}),
  }
}

function annulusHitsCircle(
  center: LastChancesVector,
  innerRadius: number,
  outerRadius: number,
  target: LastChancesVector,
  targetRadius: number,
): boolean {
  const distance = Math.sqrt(distanceSquared(center, target))
  return distance - targetRadius <= outerRadius + EPSILON
    && distance + targetRadius + EPSILON >= innerRadius
}

function sectorHitsCircle(
  collider: LastChancesRuntimeSectorCollider,
  target: LastChancesVector,
  targetRadius: number,
): boolean {
  if (collider.halfArcRadians >= Math.PI - EPSILON) {
    return annulusHitsCircle(
      collider.origin,
      collider.innerRadius,
      collider.outerRadius,
      target,
      targetRadius,
    )
  }

  const relative = {
    x: target.x - collider.origin.x,
    y: target.y - collider.origin.y,
  }
  const distance = vectorLength(relative)
  const facingAngle = Math.atan2(collider.direction.y, collider.direction.x)
  const targetAngle = distance <= EPSILON ? facingAngle : Math.atan2(relative.y, relative.x)
  const insideAngle = Math.abs(normalizeAngle(targetAngle - facingAngle))
    <= collider.halfArcRadians + EPSILON
  if (insideAngle && pointInsideAnnulus(distance, collider.innerRadius, collider.outerRadius)) {
    return true
  }

  const radiusSquared = targetRadius * targetRadius
  if (distanceToArc(
    target,
    collider.origin,
    collider.outerRadius,
    facingAngle,
    collider.halfArcRadians,
  ) <= targetRadius + EPSILON) {
    return true
  }
  if (collider.innerRadius > EPSILON && distanceToArc(
    target,
    collider.origin,
    collider.innerRadius,
    facingAngle,
    collider.halfArcRadians,
  ) <= targetRadius + EPSILON) {
    return true
  }

  for (const angle of [
    facingAngle - collider.halfArcRadians,
    facingAngle + collider.halfArcRadians,
  ]) {
    const inner = polarPoint(collider.origin, collider.innerRadius, angle)
    const outer = polarPoint(collider.origin, collider.outerRadius, angle)
    if (distanceToSegmentSquared(target, inner, outer) <= radiusSquared + EPSILON) return true
  }
  return false
}

export function colliderHitsCircle(
  collider: LastChancesRuntimeCollider,
  target: LastChancesVector,
  radius: number,
): boolean {
  const targetRadius = Math.max(0, finiteOr(radius, 0))
  if (collider.innerExclusion) {
    const centerDistance = Math.sqrt(distanceSquared(collider.innerExclusion.center, target))
    if (centerDistance - targetRadius < collider.innerExclusion.outerRadius - EPSILON) return false
  }
  if (collider.shape === 'circle') {
    return annulusHitsCircle(
      collider.center,
      collider.innerRadius,
      collider.outerRadius,
      target,
      targetRadius,
    )
  }
  if (collider.shape === 'sector') return sectorHitsCircle(collider, target, targetRadius)
  return distanceToSegmentSquared(target, collider.start, collider.end)
    <= (collider.radius + targetRadius) ** 2 + EPSILON
}

/** Tests a moving circle against the same runtime collider used for traces. */
export function colliderHitsSweptCircle(
  collider: LastChancesRuntimeCollider,
  start: LastChancesVector,
  end: LastChancesVector,
  radius: number,
): boolean {
  const sweptRadius = Math.max(0, finiteOr(radius, 0))
  if (collider.shape === 'capsule' || collider.shape === 'sweep') {
    return distanceBetweenSegmentsSquared(collider.start, collider.end, start, end)
      <= (collider.radius + sweptRadius) ** 2 + EPSILON
  }
  const travel = Math.sqrt(distanceSquared(start, end))
  const steps = Math.max(1, Math.ceil(travel / Math.max(4, sweptRadius)))
  for (let step = 0; step <= steps; step += 1) {
    const progress = step / steps
    if (colliderHitsCircle(collider, {
      x: start.x + (end.x - start.x) * progress,
      y: start.y + (end.y - start.y) * progress,
    }, sweptRadius)) return true
  }
  return false
}

function arcPoints(
  origin: LastChancesVector,
  radius: number,
  startAngle: number,
  endAngle: number,
  minimumSteps = 8,
): LastChancesVector[] {
  const span = Math.abs(endAngle - startAngle)
  const steps = Math.max(minimumSteps, Math.ceil(span / (Math.PI / 18)))
  return Array.from({ length: steps + 1 }, (_, index) => {
    const angle = startAngle + (endAngle - startAngle) * index / steps
    return polarPoint(origin, radius, angle)
  })
}

function circlePath(center: LastChancesVector, radius: number): LastChancesColliderTracePath {
  const points = arcPoints(center, radius, 0, TAU, 36)
  points.pop()
  return { points, closed: true }
}

function capsulePath(
  start: LastChancesVector,
  end: LastChancesVector,
  radius: number,
): LastChancesColliderTracePath {
  const delta = { x: end.x - start.x, y: end.y - start.y }
  if (vectorLength(delta) <= EPSILON) return circlePath(start, radius)
  const angle = Math.atan2(delta.y, delta.x)
  const startCap = arcPoints(start, radius, angle + Math.PI / 2, angle + Math.PI * 1.5, 12)
  const endCap = arcPoints(end, radius, angle - Math.PI / 2, angle + Math.PI / 2, 12)
  return {
    points: [...startCap, ...endCap],
    closed: true,
  }
}

/**
 * Returns world-space outlines. Closed entries are polygons; open entries are
 * polylines. Annular circles expose independent outer and inner outlines,
 * while annular sectors return one closed band polygon.
 */
export function colliderTracePath(
  collider: LastChancesRuntimeCollider,
): LastChancesColliderTracePath[] {
  if (collider.shape === 'circle') {
    const paths = [circlePath(collider.center, collider.outerRadius)]
    if (collider.innerRadius > EPSILON) paths.push(circlePath(collider.center, collider.innerRadius))
    return paths
  }

  if (collider.shape === 'sector') {
    const facingAngle = Math.atan2(collider.direction.y, collider.direction.x)
    if (collider.halfArcRadians >= Math.PI - EPSILON) {
      const paths = [circlePath(collider.origin, collider.outerRadius)]
      if (collider.innerRadius > EPSILON) paths.push(circlePath(collider.origin, collider.innerRadius))
      return paths
    }
    const startAngle = facingAngle - collider.halfArcRadians
    const endAngle = facingAngle + collider.halfArcRadians
    const outer = arcPoints(collider.origin, collider.outerRadius, startAngle, endAngle)
    if (collider.innerRadius <= EPSILON) {
      return [{
        points: [{ ...collider.origin }, ...outer],
        closed: true,
      }]
    }
    const inner = arcPoints(collider.origin, collider.innerRadius, endAngle, startAngle)
    return [{
      points: [...outer, ...inner],
      closed: true,
    }]
  }

  return [capsulePath(collider.start, collider.end, collider.radius)]
}
