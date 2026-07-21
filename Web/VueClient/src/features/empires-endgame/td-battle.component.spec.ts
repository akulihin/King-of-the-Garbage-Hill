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
  const rulesIdentity = createTdRulesIdentity(config.schemaVersion, config.combat, config.td, {
    technologies: config.empire.technologies,
    units: config.empire.units ?? [],
    buildings: config.empire.buildings,
    steelResearch: config.empire.steelResearch,
  })
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
    equipmentStock: {},
    deployments: [],
  }
  return {
    id: sessionId,
    sequence: 1,
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

async function runNextFrame(timestamp: number) {
  const frame = frameCallbacks.shift()
  if (!frame) throw new Error('TD component did not schedule the expected frame')
  frame(timestamp)
  await Promise.resolve()
}

function setDocumentHidden(hidden: boolean) {
  Object.defineProperty(document, 'hidden', { configurable: true, value: hidden })
  document.dispatchEvent(new Event('visibilitychange'))
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

  it('exposes canvas and text alternatives while discarding hidden-tab elapsed time', async () => {
    const onResolved = vi.fn<(result: TdBattleResult) => void>()
    const view = render(TdBattle, { props: { session: session(), onResolved } })
    expect(view.getByRole('img', { name: /Оборона:/ })).toBeTruthy()
    expect(view.getByTestId('td-text-state').textContent).toContain('Вражеские группы')

    await fireEvent.click(view.getByTestId('td-start'))
    await runNextFrame(1)
    setDocumentHidden(true)
    await runNextFrame(50_001)
    expect(view.getByTestId('td-command-status').textContent).toContain('фоне')
    expect(onResolved).not.toHaveBeenCalled()

    setDocumentHidden(false)
    await runNextFrame(100_001)
    expect(onResolved).not.toHaveBeenCalled()
    await runNextFrame(100_001 + config.td.tickMs!)
    await waitFor(() => expect(onResolved).toHaveBeenCalledTimes(1))
    expect(onResolved.mock.calls[0][0].terminalReason).toBe('tick-cap')
  })
})
