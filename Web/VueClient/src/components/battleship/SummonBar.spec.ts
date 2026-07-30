import { cleanup, fireEvent, render } from '@testing-library/vue'
import { afterEach, describe, expect, it } from 'vitest'
import type {
  BattleshipPendingSummon,
  BattleshipPlayerState,
  BattleshipSummon,
} from 'src/services/signalr'
import SummonBar from './SummonBar.vue'

afterEach(cleanup)

function summon(overrides: Partial<BattleshipSummon> = {}): BattleshipSummon {
  return {
    id: 'summon-1',
    type: 'Ram',
    row: 9,
    col: 3,
    speed: 2,
    isAlive: true,
    moveDirection: 'Down',
    waitingForTurnBack: false,
    waitingForDirectionChoice: false,
    isBoardingShip: false,
    sourceShipName: null,
    ...overrides,
  }
}

function pending(overrides: Partial<BattleshipPendingSummon> = {}): BattleshipPendingSummon {
  return {
    id: 'pending-1',
    type: 'PirateBoat',
    allowedColumns: [],
    isBoarding: false,
    isMandatoryBoarding: false,
    sourceShipName: 'Пираты',
    ...overrides,
  }
}

function player(overrides: Partial<BattleshipPlayerState> = {}): BattleshipPlayerState {
  return {
    discordId: 'me',
    username: 'Player',
    isBot: false,
    isMe: true,
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
    board: null,
    summons: [],
    pendingSummons: [],
    selectedShips: [],
    availableWeapons: [],
    canPassBoarding: false,
    ...overrides,
  }
}

function renderBar(
  myPlayer: BattleshipPlayerState,
  overrides: Partial<InstanceType<typeof SummonBar>['$props']> = {},
) {
  return render(SummonBar, {
    props: {
      myPlayer,
      phase: 'Boarding',
      shotCount: 1,
      canDeploySummon: true,
      boardingPlacementPending: false,
      waitingRamReturnActive: false,
      deployableSummons: ['Ram', 'PirateBoat'],
      availableSummons: ['Ram', 'PirateBoat'],
      summonDeployMode: null,
      ...overrides,
    },
  })
}

describe('SummonBar boarding presentation and locks', () => {
  it('shows only the source name for active boarding ships and keeps ordinary names', () => {
    const view = renderBar(player({
      summons: [
        summon({
          id: 'boarding',
          isBoardingShip: true,
          sourceShipName: 'Single 1',
        }),
        summon({ id: 'ordinary', row: 4 }),
      ],
    }))

    expect(view.container.textContent).toContain('Single 1')
    expect(view.container.textContent).toContain('Таран')
    expect(view.container.textContent).not.toContain('Абордажный корабль (Single 1)')
  })

  it('makes exact waiting Ram chips selectable and blocks every competing summon action', async () => {
    const first = summon({
      id: 'ram-first',
      waitingForTurnBack: true,
      col: 2,
    })
    const second = summon({
      id: 'ram-boarding',
      waitingForTurnBack: true,
      isBoardingShip: true,
      sourceShipName: 'Drakkar',
      col: 7,
    })
    const view = renderBar(player({
      summons: [first, second],
      pendingSummons: [pending({ isMandatoryBoarding: true })],
    }), {
      waitingRamReturnActive: true,
      boardingPlacementPending: true,
      canDeploySummon: false,
    })

    const normalChoices = [...view.container.querySelectorAll('.summon-bar .bs-seg-btn')]
    expect(normalChoices.length).toBeGreaterThan(0)
    expect(normalChoices.every(button => (button as HTMLButtonElement).disabled)).toBe(true)
    expect((view.container.querySelector('.pending-entry') as HTMLButtonElement).disabled).toBe(true)

    const returnButtons = [...view.container.querySelectorAll('.summon-wait')]
    expect(returnButtons).toHaveLength(2)
    await fireEvent.click(returnButtons[0]!)
    await fireEvent.click(returnButtons[1]!)
    expect(view.emitted().enterReentryDeploy?.map(event => event[0].id))
      .toEqual(['ram-first', 'ram-boarding'])
  })

  it('allows only mandatory pending items during the global Boarding barrier', () => {
    const view = renderBar(player({
      hasPendingBoardingDeployment: true,
      mandatoryBoardingSummonSlots: 2,
      mandatoryBoardingBrander: true,
      boardingDeploymentCapacity: 4,
      pendingSummons: [
        pending({ id: 'optional' }),
        pending({ id: 'mandatory', isMandatoryBoarding: true }),
      ],
    }), {
      boardingPlacementPending: true,
    })

    const pendingButtons = [...view.container.querySelectorAll('.pending-entry')]
      .map(element => element as HTMLButtonElement)
    expect(pendingButtons[0]?.disabled).toBe(true)
    expect(pendingButtons[1]?.disabled).toBe(false)
    expect(view.container.textContent).toContain('Обязательно: 4 · мест: 4')
  })
})
