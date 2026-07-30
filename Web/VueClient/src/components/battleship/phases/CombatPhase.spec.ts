import { cleanup, fireEvent, render, waitFor } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type {
  BattleshipCell,
  BattleshipGameState,
  BattleshipPlayerState,
} from 'src/services/signalr'
import { useBattleshipStore } from 'src/store/battleship'
import CombatPhase from './CombatPhase.vue'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

function cell(overrides: Partial<BattleshipCell>): BattleshipCell {
  return {
    row: 0,
    col: 0,
    isRevealed: true,
    isHit: false,
    isMiss: false,
    isBurning: false,
    hasShip: false,
    shipId: null,
    hasSummon: false,
    summonOwnerId: null,
    summonType: null,
    isScratched: false,
    ...overrides,
  }
}

function player(
  discordId: string,
  isMe: boolean,
  cells: BattleshipCell[],
): BattleshipPlayerState {
  return {
    discordId,
    username: discordId,
    isBot: false,
    isMe,
    faction: 'Alliance',
    coinsRemaining: 0,
    isReady: true,
    summonSlotsUsed: 0,
    maxSummonSlots: 4,
    branderUsed: false,
    selectedShotType: 'Ballista',
    selectedWeaponId: null,
    revealedCellCount: 20,
    stunShotExpiry: -1,
    hasPenalty: false,
    hasShotThisTurn: false,
    hasPendingBoardingDeployment: false,
    mandatoryBoardingSummonSlots: 0,
    mandatoryBoardingBrander: false,
    boardingDeploymentCapacity: 0,
    pendingManeuver: null,
    pendingCursedBoatDirection: null,
    pendingAssembly: null,
    shotDelayRemainingMs: 0,
    shotDelayDurationMs: 0,
    summonCooldownRemaining: 0,
    canDeployAnySummon: true,
    fleet: [],
    board: { cells },
    summons: [],
    pendingSummons: [],
    selectedShips: [],
    availableWeapons: [],
    canPassBoarding: false,
  }
}

describe('CombatPhase PirateBoat restore mode', () => {
  it('highlights the full eligible ship, restores by exact ID, and keeps enemy entry available', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const store = useBattleshipStore()
    const myCells = [
      cell({
        row: 2,
        col: 1,
        hasShip: true,
        shipId: 'devastated',
        isDevastated: true,
      }),
      cell({
        row: 2,
        col: 2,
        hasShip: true,
        shipId: 'devastated',
        isDevastated: true,
      }),
      cell({
        row: 4,
        col: 4,
        hasShip: true,
        shipId: 'intact',
      }),
    ]
    const state: BattleshipGameState = {
      gameId: 'game-restore',
      phase: 'Boarding',
      turnNumber: 3,
      shotCount: 8,
      isFinished: false,
      winnerId: null,
      currentTurnPlayerId: 'me',
      isMyTurn: true,
      myPlayerId: 'me',
      gameLog: [],
      player1: player('me', true, myCells),
      player2: player('enemy', false, [cell({ row: 0, col: 0 })]),
      shipCatalog: null,
      myEndReward: null,
    }
    store.gameState = state
    store.summonDeployMode = { type: 'PirateBoat' }
    store.vfxEnabled = false
    const restore = vi.spyOn(store, 'restoreShipWithPirateBoat').mockResolvedValue()

    const view = render(CombatPhase, {
      global: {
        plugins: [pinia],
        stubs: {
          ActionBar: true,
          BattleLogPanel: true,
          FleetPanel: true,
          ProjectileLayer: true,
          SummonBar: true,
          VfxCanvas: true,
          WeaponBar: true,
        },
      },
    })

    const restoreDecks = view.container.querySelectorAll(
      '.board-mine .cell-maneuver-target',
    )
    expect(restoreDecks).toHaveLength(2)
    expect(view.container.querySelector(
      '.board-mine .cell[data-row="4"][data-col="4"]',
    )?.classList.contains('cell-maneuver-target')).toBe(false)
    expect(view.container.querySelector(
      '.board-enemy .cell[data-row="0"][data-col="0"]',
    )?.classList.contains('cell-clickable')).toBe(true)

    await fireEvent.click(restoreDecks[0]!)
    expect(restore).toHaveBeenCalledWith('devastated')
    await waitFor(() => expect(store.summonDeployMode).toBeNull())
  })
})
