import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/99lc/game-config.json'
import { resolveLastChancesSpearChargeVisual } from './spear-visuals'
import type { LastChancesConfig } from './types'

const defaultConfig = defaultConfigJson as unknown as LastChancesConfig
const spearRelease = defaultConfig.weapons
  .find(weapon => weapon.id === 'twohand-spear')!
  .attacks.hold

describe('99LC Spear presentation', () => {
  it('raises and draws back the weapon through the real three release thresholds', () => {
    expect(resolveLastChancesSpearChargeVisual(spearRelease, 0)).toMatchObject({
      stage: 'early',
      armed: false,
      liftRadii: 0,
      forwardRadii: 0,
    })
    expect(resolveLastChancesSpearChargeVisual(spearRelease, 650)).toMatchObject({
      stage: 'early',
      armed: true,
      liftRadii: 1.45,
      forwardRadii: 0.58,
      verticalTilt: 0.18,
    })
    expect(resolveLastChancesSpearChargeVisual(spearRelease, 1125)).toMatchObject({
      stage: 'middle',
      armed: true,
      liftRadii: 1.95,
      forwardRadii: 0.08,
      verticalTilt: 0,
    })
    expect(resolveLastChancesSpearChargeVisual(spearRelease, 1650)).toMatchObject({
      stage: 'late',
      armed: true,
      liftRadii: 2.35,
      forwardRadii: -0.68,
      verticalTilt: -0.16,
    })
  })

  it('keeps the current telegraph band while easing toward the next pose', () => {
    const transition = resolveLastChancesSpearChargeVisual(spearRelease, 900)!
    expect(transition.stage).toBe('early')
    expect(transition.previewBand.id).toBe('early')
    expect(transition.liftRadii).toBeGreaterThan(1.45)
    expect(transition.liftRadii).toBeLessThan(1.95)
    expect(transition.forwardRadii).toBeLessThan(0.58)
    expect(transition.forwardRadii).toBeGreaterThan(0.08)
  })
})
