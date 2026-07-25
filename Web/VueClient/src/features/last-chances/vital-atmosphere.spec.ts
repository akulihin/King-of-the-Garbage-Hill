import { describe, expect, it } from 'vitest'
import { resolveLastChancesVitalAtmosphere } from './vital-atmosphere'

describe('99LC vital atmosphere', () => {
  it('keeps the screen clear while both vitals are healthy', () => {
    const atmosphere = resolveLastChancesVitalAtmosphere(1, 1)
    expect(atmosphere.fogIntensity).toBe(0)
    expect(atmosphere.vignetteIntensity).toBe(0)
    expect(atmosphere.breathPeriodMs).toBe(5200)
  })

  it('fogs the corners only once stamina falls under its onset, then ramps to full', () => {
    expect(resolveLastChancesVitalAtmosphere(0.6, 1).fogIntensity).toBe(0)
    expect(resolveLastChancesVitalAtmosphere(0.3, 1).fogIntensity).toBeGreaterThan(0)
    expect(resolveLastChancesVitalAtmosphere(0.3, 1).fogIntensity).toBeLessThan(1)
    expect(resolveLastChancesVitalAtmosphere(0, 1).fogIntensity).toBe(1)
  })

  it('shortens the breath cycle as stamina drains', () => {
    const rested = resolveLastChancesVitalAtmosphere(1, 1).breathPeriodMs
    const winded = resolveLastChancesVitalAtmosphere(0.3, 1).breathPeriodMs
    const spent = resolveLastChancesVitalAtmosphere(0, 1).breathPeriodMs
    expect(winded).toBeLessThan(rested)
    expect(spent).toBeLessThan(winded)
    expect(spent).toBe(1400)
  })

  it('closes the vignette in as mental health drops, independently of stamina', () => {
    expect(resolveLastChancesVitalAtmosphere(1, 0.8).vignetteIntensity).toBe(0)
    expect(resolveLastChancesVitalAtmosphere(1, 0.4).vignetteIntensity).toBeGreaterThan(0)
    expect(resolveLastChancesVitalAtmosphere(1, 0).vignetteIntensity).toBe(1)
    expect(resolveLastChancesVitalAtmosphere(0, 0.4).vignetteIntensity)
      .toBe(resolveLastChancesVitalAtmosphere(1, 0.4).vignetteIntensity)
  })

  it('clamps nonsense ratios instead of producing invalid CSS values', () => {
    expect(resolveLastChancesVitalAtmosphere(-3, 4).fogIntensity).toBe(1)
    expect(resolveLastChancesVitalAtmosphere(4, -3).vignetteIntensity).toBe(1)
    expect(resolveLastChancesVitalAtmosphere(Number.NaN, Number.NaN)).toMatchObject({
      fogIntensity: 1,
      vignetteIntensity: 1,
    })
  })
})
