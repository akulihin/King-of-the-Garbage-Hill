import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../../public/99lc/game-config.json'
import {
  clearLastChancesConfig,
  cloneLastChancesConfig,
  loadLastChancesConfig,
  saveLastChancesConfig,
  validateLastChancesConfig,
} from './config'
import { buildLastChancesPlan } from './plan'
import type { LastChancesConfig } from './types'

const defaultConfig = defaultConfigJson as unknown as LastChancesConfig

describe('99LC config and deterministic plan', () => {
  beforeEach(() => clearLastChancesConfig())
  afterEach(() => vi.unstubAllGlobals())

  it('accepts the shipped builder config and its terminal boss tier', () => {
    const result = validateLastChancesConfig(defaultConfig)

    expect(result.errors).toEqual([])
    expect(defaultConfig.chances).toBe(99)
    expect(defaultConfig.progression.tiers).toHaveLength(7)
    expect(defaultConfig.progression.tiers.slice(0, 6).every(tier => tier.kind === 'normal')).toBe(true)
    expect(defaultConfig.progression.tiers[defaultConfig.progression.tiers.length - 1]?.kind).toBe('boss')
    expect(defaultConfig.progression.tiers.every(tier => tier.deathCost === 1)).toBe(true)
    expect(defaultConfig.enemies.map(enemy => enemy.name)).toEqual(expect.arrayContaining([
      'Слуга',
      'Стражник',
      'Химера',
      'Нож-паук',
      'Тень Куратора',
    ]))
  })

  it('rebuilds identical rooms, enemies, and links for the same generation', () => {
    const first = buildLastChancesPlan(defaultConfig, 3)
    const retry = buildLastChancesPlan(defaultConfig, 3)
    const nextGeneration = buildLastChancesPlan(defaultConfig, 4)

    expect(retry).toEqual(first)
    expect(nextGeneration).not.toEqual(first)
    expect(first.tiers).toHaveLength(defaultConfig.progression.tiers.length)
    expect(first.tiers[first.tiers.length - 1]).toHaveLength(1)
  })

  it('clones deeply and loads a validated browser builder override', async () => {
    const override = cloneLastChancesConfig(defaultConfig)
    override.seed = 'builder-override'
    override.progression.tiers[0].deathCost = 2
    saveLastChancesConfig(override)

    const loaded = await loadLastChancesConfig({ url: '/fetch-must-not-run.json' })

    expect(loaded.seed).toBe('builder-override')
    expect(loaded.progression.tiers[0].deathCost).toBe(2)
    loaded.progression.tiers[0].deathCost = 7
    expect(override.progression.tiers[0].deathCost).toBe(2)
  })

  it('rejects a progression without a terminal boss component', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.progression.tiers[invalid.progression.tiers.length - 1].kind = 'normal'

    expect(validateLastChancesConfig(invalid).errors).toContain(
      'progression.tiers must end with a boss tier',
    )
  })

  it('fetches runtime JSON without using the HTTP cache', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => defaultConfig,
    })
    vi.stubGlobal('fetch', fetchMock)

    await loadLastChancesConfig({ url: '/99lc/test-config.json', useBrowserOverride: false })

    expect(fetchMock).toHaveBeenCalledWith('/99lc/test-config.json', {
      cache: 'no-store',
      signal: undefined,
    })
  })
})
