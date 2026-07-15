import { reactive } from 'vue'
import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, validateEmpiresConfig } from './config'
import type { EmpiresEndgameConfig } from './types'

describe('Empire\'s Endgame configuration', () => {
  it('clones a Vue-reactive constructor definition without DataCloneError', () => {
    const source = reactive(defaultConfigJson as unknown as EmpiresEndgameConfig)

    const clone = cloneEmpiresConfig(source)

    expect(clone).not.toBe(source)
    expect(clone.cards).not.toBe(source.cards)
    expect(clone.cards).toHaveLength(53)
    expect(() => validateEmpiresConfig(clone)).not.toThrow()
  })
})
