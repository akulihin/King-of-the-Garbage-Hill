import { describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../../public/99lc/game-config.json'
import { LastChancesEngine } from './engine'
import type {
  LastChancesConfig,
  LastChancesGamePlan,
  LastChancesSnapshot,
} from './types'

const defaultConfig = defaultConfigJson as unknown as LastChancesConfig

function makeCanvas(): HTMLCanvasElement {
  const noop = () => undefined
  const gradient = { addColorStop: noop }
  const context = new Proxy({
    createRadialGradient: () => gradient,
  } as Record<PropertyKey, unknown>, {
    get(target, property) {
      return property in target ? target[property] : noop
    },
    set(target, property, value) {
      target[property] = value
      return true
    },
  })
  const canvas = document.createElement('canvas')
  Object.defineProperty(canvas, 'getContext', { value: () => context })
  Object.defineProperty(canvas, 'getBoundingClientRect', {
    value: () => ({
      x: 0,
      y: 0,
      top: 0,
      left: 0,
      right: 960,
      bottom: 640,
      width: 960,
      height: 640,
      toJSON: noop,
    }),
  })
  return canvas
}

describe('99LC engine attempt lifecycle', () => {
  it('retries the same generated room while retaining Chance cost and stat erosion', () => {
    const snapshots: LastChancesSnapshot[] = []
    let plan: LastChancesGamePlan | null = null
    const engine = new LastChancesEngine(makeCanvas(), defaultConfig, {
      onPlan: nextPlan => { plan = nextPlan },
      onSnapshot: snapshot => snapshots.push(snapshot),
    })

    try {
      const firstNodeId = snapshots.at(-1)?.availableNodeIds[0]
      expect(firstNodeId).toBeTruthy()
      expect(engine.chooseNode(firstNodeId as string)).toBe(true)
      const firstRoom = snapshots.at(-1) as LastChancesSnapshot
      const firstEnemies = firstRoom.enemies.map(enemy => ({
        id: enemy.id,
        definitionId: enemy.definitionId,
        position: enemy.position,
        facing: enemy.facing,
      }))
      const firstPlan = JSON.stringify(plan)

      const testAccess = engine as unknown as { killPlayer: (reason: string) => void }
      testAccess.killPlayer('Prototype test')
      const dead = snapshots.at(-1) as LastChancesSnapshot
      expect(dead.phase).toBe('dead')
      expect(dead.chances).toBe(defaultConfig.chances - defaultConfig.progression.tiers[0].deathCost)
      expect(dead.player.stats.maxHp).toBe(
        defaultConfig.player.baseStats.maxHp - defaultConfig.progression.tiers[0].erosion.maxHp,
      )
      expect(dead.player.stats.attackPower).toBe(
        defaultConfig.player.baseStats.attackPower - defaultConfig.progression.tiers[0].erosion.attackPower,
      )

      expect(engine.retryAttempt()).toBe(true)
      expect(engine.chooseNode(firstNodeId as string)).toBe(true)
      const retriedRoom = snapshots.at(-1) as LastChancesSnapshot
      expect(retriedRoom.enemies.map(enemy => ({
        id: enemy.id,
        definitionId: enemy.definitionId,
        position: enemy.position,
        facing: enemy.facing,
      }))).toEqual(firstEnemies)
      expect(JSON.stringify(plan)).toBe(firstPlan)
      expect(retriedRoom.player.stats).toEqual(dead.player.stats)
      expect(retriedRoom.player.hp).toBe(dead.player.stats.maxHp)
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('lets a connected standard gamepad select and enter the opening route', () => {
    const axes = [0, 0, 0, 0]
    const buttons = Array.from({ length: 16 }, () => ({ pressed: false, value: 0 }))
    const gamepad = {
      axes,
      buttons,
      connected: true,
      id: 'DualSense Wireless Controller',
      index: 0,
      mapping: 'standard',
    }
    vi.stubGlobal('navigator', { getGamepads: () => [gamepad] })
    const snapshots: LastChancesSnapshot[] = []
    const engine = new LastChancesEngine(makeCanvas(), defaultConfig, {
      onSnapshot: snapshot => snapshots.push(snapshot),
    })
    const testAccess = engine as unknown as { pollGamepad: () => void }

    try {
      const opening = snapshots.at(-1) as LastChancesSnapshot
      expect(opening.availableNodeIds.length).toBeGreaterThan(1)
      expect(opening.selectedNodeId).toBe(opening.availableNodeIds[0])

      axes[0] = 1
      testAccess.pollGamepad()
      const cycled = snapshots.at(-1) as LastChancesSnapshot
      expect(cycled.selectedNodeId).toBe(opening.availableNodeIds[1])
      expect(cycled.gamepad).toMatchObject({
        connected: true,
        id: 'DualSense Wireless Controller',
        profile: 'standard',
      })

      axes[0] = 0
      testAccess.pollGamepad()
      buttons[0] = { pressed: true, value: 1 }
      testAccess.pollGamepad()

      const entered = snapshots.at(-1) as LastChancesSnapshot
      expect(entered.phase).toBe('playing')
      expect(entered.currentNodeId).toBe(opening.availableNodeIds[1])
      expect(entered.selectedNodeId).toBeNull()
    } finally {
      engine.destroy()
      vi.unstubAllGlobals()
      vi.restoreAllMocks()
    }
  })
})
