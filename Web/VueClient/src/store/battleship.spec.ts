import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  signalrService,
  type BattleshipGameState,
} from 'src/services/signalr'
import { useBattleshipStore } from './battleship'

function gameState(): BattleshipGameState {
  return {
    gameId: 'game-1',
    phase: 'Boarding',
    turnNumber: 3,
    shotCount: 8,
    isFinished: false,
    winnerId: null,
    currentTurnPlayerId: 'me',
    isMyTurn: true,
    myPlayerId: 'me',
    gameLog: [],
    player1: null,
    player2: null,
    shipCatalog: null,
    myEndReward: null,
  }
}

beforeEach(() => {
  setActivePinia(createPinia())
  vi.restoreAllMocks()
})

describe('Battleship deployment actions', () => {
  it('passes the exact waiting summon ID through the store action', async () => {
    const deploy = vi.spyOn(signalrService, 'battleshipDeploySummon')
      .mockResolvedValue()
    const store = useBattleshipStore()
    store.gameState = gameState()

    await store.deploySummon('Ram', 4, 'waiting-ram-2')

    expect(deploy).toHaveBeenCalledWith('game-1', 'Ram', 4, 'waiting-ram-2')
  })

  it('passes the exact Devastated ship ID to PirateBoat restoration', async () => {
    const restore = vi.spyOn(signalrService, 'battleshipRestoreShipWithPirateBoat')
      .mockResolvedValue()
    const store = useBattleshipStore()
    store.gameState = gameState()

    await store.restoreShipWithPirateBoat('devastated-ship')

    expect(restore).toHaveBeenCalledWith('game-1', 'devastated-ship')
  })
})
