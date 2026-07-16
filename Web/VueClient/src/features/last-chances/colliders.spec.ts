import { describe, expect, it } from 'vitest'
import {
  colliderHitsCircle,
  colliderHitsSweptCircle,
  colliderTracePath,
  resolveAttackCollider,
  type LastChancesAttackColliderDefinition,
  type LastChancesRuntimeCollider,
} from './colliders'
import type { LastChancesAttackDefinition } from './types'

function attack(
  collider: LastChancesAttackColliderDefinition,
  overrides: Partial<LastChancesAttackDefinition> = {},
): LastChancesAttackDefinition & { collider: LastChancesAttackColliderDefinition } {
  return {
    name: 'Test attack',
    kind: 'melee',
    damage: 10,
    cooldownMs: 0,
    range: 120,
    radius: 8,
    arcDegrees: 90,
    durationMs: 200,
    projectileSpeed: 0,
    pierce: 0,
    knockback: 0,
    color: '#fff',
    ...overrides,
    collider,
  }
}

function expectTraceBoundaryToHit(collider: LastChancesRuntimeCollider): void {
  const paths = colliderTracePath(collider)
  expect(paths.length).toBeGreaterThan(0)
  for (const path of paths) {
    expect(path.closed).toBe(true)
    expect(path.points.length).toBeGreaterThan(3)
    for (const point of path.points) {
      expect(colliderHitsCircle(collider, point, 0)).toBe(true)
    }
  }
}

describe('99LC collider geometry', () => {
  it('preserves the spear inner dead zone while accepting the damaging band', () => {
    const collider = resolveAttackCollider(
      { x: 0, y: 0 },
      { x: 1, y: 0 },
      attack({ shape: 'sector', innerRange: 48 }, { range: 150, arcDegrees: 70 }),
      1,
    )

    expect(collider).toMatchObject({
      shape: 'sector',
      innerRadius: 48,
      outerRadius: 150,
    })
    expect(colliderHitsCircle(collider, { x: 24, y: 0 }, 8)).toBe(false)
    expect(colliderHitsCircle(collider, { x: 56, y: 0 }, 8)).toBe(true)
  })

  it('keeps the spear tip boundary inclusive and rejects targets beyond its sweet edge', () => {
    const collider = resolveAttackCollider(
      { x: 10, y: 20 },
      { x: 1, y: 0 },
      attack({ shape: 'sector', innerRange: 42 }, { range: 150, arcDegrees: 54 }),
      1,
    )

    expect(colliderHitsCircle(collider, { x: 165, y: 20 }, 5)).toBe(true)
    expect(colliderHitsCircle(collider, { x: 165.01, y: 20 }, 5)).toBe(false)
  })

  it('uses full authored capsule width and keeps narrow thrusts narrow', () => {
    const collider = resolveAttackCollider(
      { x: 0, y: 0 },
      { x: 1, y: 0 },
      attack({ shape: 'capsule', innerRange: 20, width: 10 }, { range: 140 }),
      1,
    )

    expect(collider).toMatchObject({
      shape: 'capsule',
      start: { x: 20, y: 0 },
      end: { x: 140, y: 0 },
      radius: 5,
    })
    expect(colliderHitsCircle(collider, { x: 80, y: 8 }, 3)).toBe(true)
    expect(colliderHitsCircle(collider, { x: 80, y: 8.01 }, 3)).toBe(false)
  })

  it('honors sector facing and authored arc', () => {
    const collider = resolveAttackCollider(
      { x: 0, y: 0 },
      { x: 1, y: 0 },
      attack({ shape: 'sector' }, { range: 100, arcDegrees: 60 }),
      1,
    )
    const insideAngle = 25 * Math.PI / 180
    const outsideAngle = 40 * Math.PI / 180

    expect(colliderHitsCircle(collider, {
      x: Math.cos(insideAngle) * 70,
      y: Math.sin(insideAngle) * 70,
    }, 0)).toBe(true)
    expect(colliderHitsCircle(collider, {
      x: Math.cos(outsideAngle) * 70,
      y: Math.sin(outsideAngle) * 70,
    }, 0)).toBe(false)
  })

  it('supports circular rings instead of filling their protected center', () => {
    const collider = resolveAttackCollider(
      { x: 5, y: -5 },
      { x: 1, y: 0 },
      attack({ shape: 'circle', innerRange: 30 }, { range: 100 }),
      1,
    )

    expect(colliderHitsCircle(collider, { x: 5, y: -5 }, 10)).toBe(false)
    expect(colliderHitsCircle(collider, { x: 30, y: -5 }, 5)).toBe(true)
    expect(colliderHitsCircle(collider, { x: 105, y: -5 }, 0)).toBe(true)
    expect(colliderHitsCircle(collider, { x: 105.01, y: -5 }, 0)).toBe(false)
  })

  it('applies authored rotation and the live sweep angle', () => {
    const collider = resolveAttackCollider(
      { x: 0, y: 0 },
      { x: 1, y: 0 },
      attack(
        { shape: 'sweep', innerRange: 10, width: 12, rotationDegrees: 90 },
        { range: 110 },
      ),
      1,
      45,
    )

    expect(collider.shape).toBe('sweep')
    if (collider.shape !== 'sweep') return
    expect(collider.angleRadians).toBeCloseTo(135 * Math.PI / 180)
    expect(collider.end.x).toBeCloseTo(-110 / Math.sqrt(2))
    expect(collider.end.y).toBeCloseTo(110 / Math.sqrt(2))
    expect(colliderHitsCircle(collider, {
      x: -70 / Math.sqrt(2),
      y: 70 / Math.sqrt(2),
    }, 0)).toBe(true)
    expect(colliderHitsCircle(collider, { x: 70, y: 0 }, 0)).toBe(false)
  })

  it('returns collider-faithful trace boundaries for sectors, rings, capsules, and sweeps', () => {
    const colliders = [
      resolveAttackCollider(
        { x: 0, y: 0 },
        { x: 1, y: 0 },
        attack({ shape: 'sector', innerRange: 35 }, { range: 125, arcDegrees: 110 }),
        1,
      ),
      resolveAttackCollider(
        { x: 20, y: 10 },
        { x: 1, y: 0 },
        attack({ shape: 'circle', innerRange: 25 }, { range: 90 }),
        1,
      ),
      resolveAttackCollider(
        { x: -10, y: 8 },
        { x: 1, y: 1 },
        attack({ shape: 'capsule', innerRange: 15, width: 18 }, { range: 105 }),
        1,
      ),
      resolveAttackCollider(
        { x: 3, y: 7 },
        { x: 1, y: 0 },
        attack({ shape: 'sweep', width: 20, rotationDegrees: -30 }, { range: 115 }),
        1,
        80,
      ),
    ]

    colliders.forEach(expectTraceBoundaryToHit)
  })

  it('detects a fast projectile crossing a narrow parry capsule between frames', () => {
    const collider = resolveAttackCollider(
      { x: 0, y: 0 },
      { x: 1, y: 0 },
      attack({ shape: 'capsule', innerRange: 25, width: 12 }, { range: 85 }),
      1,
    )

    expect(colliderHitsSweptCircle(
      collider,
      { x: 55, y: -70 },
      { x: 55, y: 70 },
      3,
    )).toBe(true)
    expect(colliderHitsSweptCircle(
      collider,
      { x: 110, y: -70 },
      { x: 110, y: 70 },
      3,
    )).toBe(false)
  })
})
