import { cleanup, fireEvent, render } from '@testing-library/vue'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import bundledConfigJson from '../../../public/empires-endgame/game-config.json'
import TavernEncounter from '../../components/empires-endgame/TavernEncounter.vue'
import { cloneEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
import type { EmpiresTavernMinigameSession, TavernResult } from './types'

function session(): { session: EmpiresTavernMinigameSession, mysticCards: ReturnType<typeof cloneEmpiresConfig>['mysticCards'] } {
  const config = cloneEmpiresConfig(bundledConfigJson)
  config.tavern.maria.encounterChance = 1
  const state = new EmpiresEndgameEngine(config, undefined, { tavernRunOrdinal: 2 }).snapshot()
  state.phase = 'empire'
  state.con = config.tavern.spawn.eligibleCon
  state.tavern.spawnChecked = true
  state.tavern.spawned = true
  state.tavern.spawnedAtCon = state.con
  state.empire.resources[config.empire.domesticEconomy.goldResourceId] = 100_000
  const engine = new EmpiresEndgameEngine(config, state)
  const result = engine.startTavernVisit(state.empire.cities[0].id)
  if (!result.ok || engine.state.minigame?.kind !== 'tavern') {
    throw new Error(`Could not start component Tavern fixture: ${result.message}`)
  }
  return { session: engine.state.minigame, mysticCards: config.mysticCards }
}

afterEach(cleanup)

describe('Empire\'s Endgame Tavern production input path', () => {
  it('routes hiring, rumors, and Maria through deterministic replay', async () => {
    const fixture = session()
    const onResolved = vi.fn<(result: TavernResult) => void>()
    const view = render(TavernEncounter, {
      props: { ...fixture, qaMode: true, onResolved },
    })

    expect(view.getByRole('heading', { name: 'Таверна «У List\'a»' })).toBeTruthy()
    expect(view.getByRole('navigation', { name: 'Секции Таверны' })).toBeTruthy()
    const hire = view.getByTestId(`tavern-hire-${fixture.session.plan.mercenaryOffers[0].id}`)
    await fireEvent.click(hire)
    await fireEvent.click(view.getByRole('button', { name: /Барная стойка/ }))
    const rumor = view.getByTestId('tavern-buy-rumor')
    rumor.focus()
    await userEvent.keyboard('{Enter}')

    expect(view.getByTestId('tavern-maria')).toBeTruthy()
    const maria = view.getByRole('button', { name: 'Сыграть двое на двое' }) as HTMLButtonElement
    expect(maria.disabled).toBe(false)
    await fireEvent.click(maria)
    expect(view.getByText(/Победа: пороховое наследие|Мария выиграла эту партию/)).toBeTruthy()
    expect(view.getByText(/можно пригласить в Совет карт/i)).toBeTruthy()
    await fireEvent.click(view.getByTestId('tavern-qa-resolve'))

    expect(onResolved).toHaveBeenCalledTimes(1)
    const result = onResolved.mock.calls[0][0]
    expect(result.error).toBeNull()
    expect(result.commandLog.map(command => command.kind)).toEqual([
      'hire',
      'buy-rumor',
      'play-maria',
      'finish',
    ])
    expect(result.mariaPlayed).toBe(true)
    expect(result.commandDigest).toMatch(/^[0-9a-f]{16}$/)
  })
})
