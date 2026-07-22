import { cleanup, fireEvent, render } from '@testing-library/vue'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ChessBoard from '../../../components/empires-endgame/ChessBoard.vue'
import type { EmpiresChessMinigameSession } from '../types'
import { createChessPlanFromConfig, createRecommendedChessConfig } from './engine'
import type { ChessCommand, ChessResult } from './types'

function session(): EmpiresChessMinigameSession {
  const config = createRecommendedChessConfig()
  config.setup = [
    { id: 'white-king', side: 'white', role: 'king', square: 'a1' },
    { id: 'white-rook', side: 'white', role: 'rook', square: 'g1' },
    { id: 'black-anton', side: 'black', role: 'knight', square: 'g8', anton: true },
  ]
  const plan = createChessPlanFromConfig(19, config, {
    id: 'chess-component-plan',
    sessionId: 'chess-component-session',
  })
  return {
    id: plan.sessionId,
    sequence: 1,
    kind: 'chess',
    plan,
    rulesIdentity: plan.rulesIdentity,
    seed: 'component-seed',
    attempt: 0,
    origin: {
      returnPhase: 'empire',
      context: {
        kind: 'capital-chess',
        capitalSiteId: 'capital-coliseum',
        cityId: 'city-capital',
        con: 1,
      },
    },
  }
}

afterEach(cleanup)

describe('Empire\'s Endgame Chess production input path', () => {
  it('exposes an accessible 8x8 board and resolves keyboard moves through deterministic replay', async () => {
    const activeSession = session()
    const onResolved = vi.fn<(result: ChessResult) => void>()
    const view = render(ChessBoard, { props: { session: activeSession, onResolved } })

    expect(view.getAllByRole('gridcell')).toHaveLength(64)
    expect(view.getByRole('grid', { name: 'Шахматная доска, белые снизу' })).toBeTruthy()
    const from = view.getByTestId('chess-square-g1') as HTMLButtonElement
    const target = view.getByTestId('chess-square-g8') as HTMLButtonElement
    expect(from.getAttribute('aria-label')).toMatch(/g1.*Белая ладья/i)
    from.focus()
    await userEvent.keyboard('{Enter}')
    expect(from.getAttribute('aria-pressed')).toBe('true')
    target.focus()
    await userEvent.keyboard('{Enter}')

    expect(onResolved).toHaveBeenCalledTimes(1)
    expect(onResolved.mock.calls[0][0]).toMatchObject({
      kind: 'chess',
      outcome: 'white-win',
      terminalReason: 'black-army-captured',
      commandLog: [expect.objectContaining({ kind: 'move', from: 'g1', to: 'g8' })],
    })
    expect((view.getByTestId('chess-square-a1') as HTMLButtonElement).disabled).toBe(true)
  })

  it('requires confirmation before emitting an abort with the exact command log', async () => {
    const onAbort = vi.fn<(commandLog: ChessCommand[]) => void>()
    const view = render(ChessBoard, { props: { session: session(), onAbort } })

    await fireEvent.click(view.getByTestId('chess-open-abort'))
    expect(view.getByRole('alertdialog', { name: 'Покинуть шахматную партию?' })).toBeTruthy()
    expect(view.getByText(/лояльность каждого города снизится на 1/i)).toBeTruthy()
    await fireEvent.click(view.getByTestId('chess-confirm-abort'))

    expect(onAbort).toHaveBeenCalledOnce()
    expect(onAbort).toHaveBeenCalledWith([])
  })
})
