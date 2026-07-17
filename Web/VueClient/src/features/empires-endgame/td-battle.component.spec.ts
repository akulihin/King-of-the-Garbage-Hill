import { cleanup, fireEvent, render, waitFor } from '@testing-library/vue'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import TdBattle from '../../components/empires-endgame/TdBattle.vue'
import { cloneEmpiresConfig } from './config'
import { createTdRulesIdentity } from './td/engine'
import type {
  EmpiresMinigameSession,
  TdBattleResult,
  TdBattlePlan,
} from './types'

const config = cloneEmpiresConfig(defaultConfigJson)
let frameCallbacks: FrameRequestCallback[] = []

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function session(): EmpiresMinigameSession {
  const variant = config.td.planVariants!.find(item => item.id === 'central-castle-defense')!
  const battlefield = config.td.battlefields.find(item => item.id === variant.battlefieldId)!
  const wave = config.td.waves.find(item => item.id === variant.waveId)!
  const rulesIdentity = createTdRulesIdentity(config.schemaVersion, config.combat, config.td)
  const sessionId = 'td-component-session'
  const plan: TdBattlePlan = {
    id: 'td-component-plan',
    sessionId,
    rulesIdentity: clone(rulesIdentity),
    mode: 'defense',
    scheduledCon: 2,
    threat: 0,
    tickMs: config.td.tickMs!,
    maxTicks: 1,
    maxCommands: config.td.maxCommands!,
    maxCatchUpTicksPerFrame: config.td.maxCatchUpTicksPerFrame!,
    startingBuildResources: config.td.startingBuildResources!,
    battlefield: clone(battlefield),
    objective: clone(variant.objective),
    towerBases: clone(config.td.towerBases!.filter(base => battlefield.towerBaseIds.includes(base.id))),
    towerChoices: clone(config.td.towers),
    gradeChoices: clone(config.td.gradeChoices!.filter(set => set.regionId === battlefield.regionId)),
    wave: clone(wave),
    combat: clone(config.combat),
    deployments: [],
  }
  return {
    id: sessionId,
    kind: 'td',
    plan,
    rulesIdentity,
    seed: 'td-component-seed',
    attempt: 0,
    origin: {
      returnPhase: 'cards',
      context: { kind: 'alliance-wave', scheduledCon: 2, waveId: wave.id },
    },
  }
}

async function advanceOneTick() {
  const firstFrame = frameCallbacks.shift()
  if (!firstFrame) throw new Error('TD component did not schedule its first frame')
  firstFrame(1)
  const secondFrame = frameCallbacks.shift()
  if (!secondFrame) throw new Error('TD component did not schedule its second frame')
  secondFrame(1 + config.td.tickMs!)
  await Promise.resolve()
}

beforeEach(() => {
  frameCallbacks = []
  Object.defineProperty(document, 'hidden', { configurable: true, value: false })
  vi.spyOn(window, 'requestAnimationFrame').mockImplementation((callback) => {
    frameCallbacks.push(callback)
    return frameCallbacks.length
  })
  vi.spyOn(window, 'cancelAnimationFrame').mockImplementation(() => {})
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockImplementation(() => null)
})

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

describe('Empire\'s Endgame TD production input path', () => {
  it.each(['pointer', 'keyboard'] as const)(
    'records a deterministic build command from %s input and resolves it through replay',
    async (input) => {
      const activeSession = session()
      const base = activeSession.plan.towerBases[0]
      const onResolved = vi.fn<(result: TdBattleResult) => void>()
      const view = render(TdBattle, {
        props: { session: activeSession, onResolved },
      })

      expect((view.getByTestId(`td-build-${base.id}`) as HTMLButtonElement).disabled).toBe(true)
      await fireEvent.click(view.getByTestId('td-start'))
      const buildButton = view.getByTestId(`td-build-${base.id}`) as HTMLButtonElement
      expect(buildButton.disabled).toBe(false)

      if (input === 'pointer') {
        await fireEvent.click(buildButton)
      } else {
        buildButton.focus()
        await userEvent.keyboard('{Enter}')
      }

      await advanceOneTick()
      await waitFor(() => expect(onResolved).toHaveBeenCalledTimes(1))
      const result = onResolved.mock.calls[0][0]
      expect(result.commandLog).toEqual([{
        tick: 0,
        sequence: 0,
        sessionId: activeSession.id,
        planId: activeSession.plan.id,
        kind: 'build-tower',
        spotId: activeSession.plan.battlefield.buildSpots[0].id,
        towerBaseId: base.id,
      }])
      expect(result.sessionId).toBe(activeSession.id)
      expect(result.rulesIdentity).toEqual(activeSession.rulesIdentity)
      expect(result.terminalReason).toBe('tick-cap')
    },
  )
})
