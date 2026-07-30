import { describe, expect, it } from 'vitest'
import {
  lastChancesCenteredFanOffsets,
  reflectLastChancesVector,
  resolveLastChancesBowCharge,
  resolveLastChancesBowCadencePose,
  sweepLastChancesCircleAgainstArena,
  sweepLastChancesCircleAgainstCircle,
} from './bow-runtime'

const emptyArena = {
  width: 100,
  height: 80,
  obstacles: [],
}

describe('99LC Bow continuous geometry', () => {
  it.each([
    {
      edge: 'left',
      start: { x: 50, y: 40 },
      end: { x: -10, y: 40 },
      point: { x: 5, y: 40 },
      normal: { x: 1, y: 0 },
      t: 0.75,
    },
    {
      edge: 'right',
      start: { x: 50, y: 40 },
      end: { x: 110, y: 40 },
      point: { x: 95, y: 40 },
      normal: { x: -1, y: 0 },
      t: 0.75,
    },
    {
      edge: 'top',
      start: { x: 50, y: 40 },
      end: { x: 50, y: -10 },
      point: { x: 50, y: 5 },
      normal: { x: 0, y: 1 },
      t: 0.7,
    },
    {
      edge: 'bottom',
      start: { x: 50, y: 40 },
      end: { x: 50, y: 90 },
      point: { x: 50, y: 75 },
      normal: { x: 0, y: -1 },
      t: 0.7,
    },
  ])('returns the exact $edge boundary impact and normal', ({ start, end, point, normal, t }) => {
    const impact = sweepLastChancesCircleAgainstArena(start, end, 5, emptyArena)
    expect(impact).toMatchObject({
      kind: 'boundary',
      point,
      normal,
    })
    expect(impact?.t).toBeCloseTo(t)
  })

  it.each([
    {
      side: 'left',
      start: { x: 10, y: 40 },
      end: { x: 80, y: 40 },
      point: { x: 38, y: 40 },
      normal: { x: -1, y: 0 },
    },
    {
      side: 'right',
      start: { x: 90, y: 40 },
      end: { x: 20, y: 40 },
      point: { x: 62, y: 40 },
      normal: { x: 1, y: 0 },
    },
    {
      side: 'top',
      start: { x: 50, y: 10 },
      end: { x: 50, y: 70 },
      point: { x: 50, y: 28 },
      normal: { x: 0, y: -1 },
    },
    {
      side: 'bottom',
      start: { x: 50, y: 70 },
      end: { x: 50, y: 10 },
      point: { x: 50, y: 52 },
      normal: { x: 0, y: 1 },
    },
  ])('returns the exact $side obstacle normal', ({ start, end, point, normal }) => {
    const impact = sweepLastChancesCircleAgainstArena(start, end, 2, {
      ...emptyArena,
      obstacles: [{ x: 40, y: 30, width: 20, height: 20 }],
    })
    expect(impact).toMatchObject({
      kind: 'obstacle',
      obstacleIndex: 0,
      point,
      normal,
    })
  })

  it('chooses the obstacle when its exact TOI precedes the boundary', () => {
    const impact = sweepLastChancesCircleAgainstArena(
      { x: 10, y: 40 },
      { x: 120, y: 40 },
      2,
      {
        ...emptyArena,
        obstacles: [{ x: 60, y: 30, width: 10, height: 20 }],
      },
    )
    expect(impact).toMatchObject({
      kind: 'obstacle',
      obstacleIndex: 0,
      point: { x: 58, y: 40 },
    })
    expect(impact?.t).toBeCloseTo(48 / 110)
  })

  it('keeps the boundary when an obstacle lies beyond its exact TOI', () => {
    const impact = sweepLastChancesCircleAgainstArena(
      { x: 10, y: 40 },
      { x: 120, y: 40 },
      2,
      {
        ...emptyArena,
        obstacles: [{ x: 105, y: 30, width: 10, height: 20 }],
      },
    )
    expect(impact).toMatchObject({
      kind: 'boundary',
      point: { x: 98, y: 40 },
      normal: { x: -1, y: 0 },
    })
  })

  it('expands obstacles by the swept circle radius', () => {
    const arena = {
      ...emptyArena,
      obstacles: [{ x: 40, y: 30, width: 20, height: 20 }],
    }
    const pointImpact = sweepLastChancesCircleAgainstArena(
      { x: 10, y: 40 },
      { x: 80, y: 40 },
      0,
      arena,
    )
    const circleImpact = sweepLastChancesCircleAgainstArena(
      { x: 10, y: 40 },
      { x: 80, y: 40 },
      5,
      arena,
    )
    expect(pointImpact?.point.x).toBe(40)
    expect(circleImpact?.point.x).toBe(35)
  })

  it('does not report the square-expanded AABB near a rounded corner as a hit', () => {
    const impact = sweepLastChancesCircleAgainstArena(
      { x: 30, y: 26 },
      { x: 36, y: 26 },
      5,
      {
        ...emptyArena,
        obstacles: [{ x: 40, y: 30, width: 20, height: 20 }],
      },
    )
    expect(impact).toBeNull()
  })

  it('returns a rounded-corner TOI and normal that produce the exact reflection', () => {
    const impact = sweepLastChancesCircleAgainstArena(
      { x: 30, y: 27 },
      { x: 45, y: 27 },
      5,
      {
        ...emptyArena,
        obstacles: [{ x: 40, y: 30, width: 20, height: 20 }],
      },
    )
    expect(impact).toMatchObject({
      kind: 'obstacle',
      obstacleIndex: 0,
      point: { x: 36, y: 27 },
    })
    expect(impact?.t).toBeCloseTo(0.4)
    expect(impact?.normal.x).toBeCloseTo(-0.8)
    expect(impact?.normal.y).toBeCloseTo(-0.6)

    const reflected = reflectLastChancesVector({ x: 10, y: 0 }, impact!.normal)
    expect(reflected.x).toBeCloseTo(-2.8)
    expect(reflected.y).toBeCloseTo(-9.6)
  })

  it('solves moving-circle target impact without frame-step tunnelling', () => {
    const impact = sweepLastChancesCircleAgainstCircle(
      { x: 0, y: 0 },
      { x: 10, y: 0 },
      1,
      { x: 5, y: 0 },
      1,
    )
    expect(impact).toMatchObject({
      point: { x: 3, y: 0 },
      normal: { x: -1, y: 0 },
    })
    expect(impact?.t).toBeCloseTo(0.3)
  })

  it('reports an initial circle overlap at t=0', () => {
    expect(sweepLastChancesCircleAgainstCircle(
      { x: 4, y: 0 },
      { x: 10, y: 0 },
      1,
      { x: 5, y: 0 },
      1,
    )).toMatchObject({
      t: 0,
      point: { x: 4, y: 0 },
      normal: { x: -1, y: 0 },
    })
  })
})

describe('99LC Bow charge and ricochet helpers', () => {
  const drawAttack = {
    charge: { maxMs: 1000 },
    tuning: {
      goldStartMs: 670,
      goldEndMs: 760,
      drawMaxHoldMs: 2000,
    },
  }

  it('reflects a velocity with v - 2 dot(v, n) n', () => {
    expect(reflectLastChancesVector(
      { x: 3, y: -4 },
      { x: 0, y: 1 },
    )).toEqual({ x: 3, y: 4 })
  })

  it('treats both golden endpoints as inclusive', () => {
    expect(resolveLastChancesBowCharge(drawAttack, 670).inGoldenWindow).toBe(true)
    expect(resolveLastChancesBowCharge(drawAttack, 760).inGoldenWindow).toBe(true)
    expect(resolveLastChancesBowCharge(drawAttack, 669.999).inGoldenWindow).toBe(false)
    expect(resolveLastChancesBowCharge(drawAttack, 760.001).inGoldenWindow).toBe(false)
  })

  it('caps power and hold progress on their independent clocks', () => {
    expect(resolveLastChancesBowCharge(drawAttack, -50)).toEqual({
      powerProgress: 0,
      holdProgress: 0,
      inGoldenWindow: false,
      goldStartRatio: 0.67,
      goldEndRatio: 0.76,
    })
    expect(resolveLastChancesBowCharge(drawAttack, 1500)).toMatchObject({
      powerProgress: 1,
      holdProgress: 0.75,
      inGoldenWindow: false,
    })
    expect(resolveLastChancesBowCharge(drawAttack, 3000)).toMatchObject({
      powerProgress: 1,
      holdProgress: 1,
      inGoldenWindow: false,
    })
  })

  it('returns symmetric deterministic radian fan offsets', () => {
    const offsets = lastChancesCenteredFanOffsets(5, 40)
    const expectedDegrees = [-20, -10, 0, 10, 20]
    expectedDegrees.forEach((degrees, index) => {
      expect(offsets[index]).toBeCloseTo(degrees * Math.PI / 180)
    })
    expect(offsets[0]).toBeCloseTo(-offsets[4])
    expect(offsets[1]).toBeCloseTo(-offsets[3])
    expect(lastChancesCenteredFanOffsets(1, 40)).toEqual([0])
  })

  it('shows a short recoil and a fresh draw inside every rapid-fire interval', () => {
    const released = resolveLastChancesBowCadencePose(0, 120, 0)
    const drawing = resolveLastChancesBowCadencePose(60, 120, 60)
    const armed = resolveLastChancesBowCadencePose(119, 120, 119)

    expect(released.recoilDurationMs).toBeLessThan(60)
    expect(released.drawProgress).toBeLessThan(drawing.drawProgress)
    expect(drawing.drawProgress).toBeLessThan(armed.drawProgress)
    expect(drawing.recoil).toBe(0)
  })
})
