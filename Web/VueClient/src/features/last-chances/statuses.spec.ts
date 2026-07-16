import { describe, expect, it } from 'vitest'
import {
  applyLastChancesStatusEffects,
  captureLastChancesDot,
  consumeLastChancesBleed,
  createLastChancesStatuses,
  refreshLastChancesBleed,
  spreadLastChancesDot,
  updateLastChancesStatuses,
} from './statuses'

describe('99LC combat statuses', () => {
  it('ticks stacked bleed deterministically and expires it', () => {
    const status = createLastChancesStatuses()
    applyLastChancesStatusEffects(status, [{
      status: 'bleed',
      durationMs: 500,
      stacks: 2,
      tickDamage: 0.9,
      tickMs: 250,
    }])
    let damage = 0
    updateLastChancesStatuses(status, 250, amount => { damage += amount })
    expect(damage).toBeCloseTo(1.8)
    updateLastChancesStatuses(status, 250, amount => { damage += amount })
    expect(damage).toBeCloseTo(3.6)
    expect(status.dots.bleed.stacks).toBe(0)
  })

  it('captures only the strongest non-bleed DOT and spreads it without consuming bleed', () => {
    const source = createLastChancesStatuses()
    applyLastChancesStatusEffects(source, [
      { status: 'bleed', durationMs: 5000, stacks: 4, tickDamage: 1, tickMs: 500 },
      { status: 'poison', durationMs: 6000, tickDamage: 3, tickMs: 1000 },
      { status: 'burn', durationMs: 2500, tickDamage: 4, tickMs: 250 },
    ])
    const stored = captureLastChancesDot(source)
    expect(stored?.kind).toBe('burn')
    expect(source.dots.bleed.stacks).toBe(4)

    const target = createLastChancesStatuses()
    spreadLastChancesDot(target, stored!)
    expect(target.dots.burn).toMatchObject({
      stacks: 1,
      tickDamage: 4,
      tickMs: 250,
      remainingMs: 2500,
    })
  })

  it('consumes or refreshes bleed for katana and spider finishers', () => {
    const status = createLastChancesStatuses()
    applyLastChancesStatusEffects(status, [{
      status: 'bleed',
      durationMs: 4000,
      stacks: 3,
      tickDamage: 0.9,
      tickMs: 500,
    }])
    refreshLastChancesBleed(status, 7000, 2)
    expect(status.dots.bleed).toMatchObject({ stacks: 6, remainingMs: 7000 })
    expect(consumeLastChancesBleed(status)).toBeCloseTo(75.6)
    expect(status.dots.bleed.stacks).toBe(0)
  })

  it('keeps chain binding distinct from its separately authored movement and attack slows', () => {
    const status = createLastChancesStatuses()
    applyLastChancesStatusEffects(status, [
      { status: 'bound', durationMs: 7000 },
      { status: 'slow', durationMs: 7000, magnitude: 0.42 },
      { status: 'attackSlow', durationMs: 7000, magnitude: 4 },
    ])

    expect(status).toMatchObject({
      boundMs: 7000,
      stunMs: 0,
      disarmMs: 0,
      slowMultiplier: 0.42,
      attackSlowMultiplier: 4,
    })
    updateLastChancesStatuses(status, 1000, () => undefined)
    expect(status).toMatchObject({
      boundMs: 6000,
      slowMs: 6000,
      attackSlowMs: 6000,
      stunMs: 0,
      disarmMs: 0,
    })
  })

  it('lets healing block preserve a bleeding wound while its damage still ticks', () => {
    const status = createLastChancesStatuses()
    applyLastChancesStatusEffects(status, [
      { status: 'bleed', durationMs: 1000, stacks: 1, tickDamage: 2, tickMs: 250 },
      { status: 'healingBlocked', durationMs: 500, magnitude: 1 },
    ])
    let damage = 0

    updateLastChancesStatuses(status, 500, amount => { damage += amount })
    expect(damage).toBe(4)
    expect(status.dots.bleed.remainingMs).toBe(1000)
    expect(status.antiHealMs).toBe(0)

    updateLastChancesStatuses(status, 500, amount => { damage += amount })
    expect(damage).toBe(8)
    expect(status.dots.bleed.remainingMs).toBe(500)
  })
})
