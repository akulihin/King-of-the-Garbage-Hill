import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'
import type { Player, PlayerStatus, ReplayData, ReplayRound } from 'src/services/signalr'
import {
  buildShiftedPlayer,
  getReplayDisplayRound,
  getReplayRoundWindow,
  getReplaySettlementLogs,
  isLegacyReplay,
  useReplayStore,
} from './replay'

function status(tag: string, overrides: Partial<PlayerStatus> = {}): PlayerStatus {
  return {
    score: 0,
    place: 6,
    isReady: false,
    isBlock: false,
    isSkip: false,
    isAutoMove: false,
    confirmedPredict: false,
    confirmedSkip: false,
    lvlUpPoints: 0,
    moveListPage: 1,
    personalLogs: `${tag}_PERSONAL`,
    previousRoundLogs: `${tag}_PREVIOUS`,
    allPersonalLogs: `${tag}_ALL`,
    scoreSource: tag,
    directMessages: [`${tag}_DM`],
    mediaMessages: [],
    isAramRollConfirmed: false,
    isDraftPickConfirmed: false,
    aramRerolledPassivesTimes: 0,
    aramRerolledStatsTimes: 0,
    placeHistory: [],
    scoreBreakdown: null,
    ...overrides,
  }
}

function player(tag: string, playerStatus: PlayerStatus): Player {
  return {
    playerId: 'player-1',
    discordUsername: tag,
    isBot: false,
    isWebPlayer: true,
    teamId: 0,
    isDead: false,
    deathSource: '',
    isKira: false,
    isTerminalMode: false,
    character: { name: tag } as Player['character'],
    status: playerStatus,
    customLeaderboardPrefix: `${tag}_PREFIX`,
    customLeaderboardText: `${tag}_TEXT`,
  }
}

function replay(rounds: ReplayRound[], replayFormatVersion?: number): ReplayData {
  return {
    replayFormatVersion,
    rounds,
  } as ReplayData
}

describe('replay round alignment', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('keeps legacy deep-link keys while hiding the empty first and fake final boundary numbers', () => {
    const legacy = replay([
      { roundNo: 1, players: [] } as ReplayRound,
      { roundNo: 2, players: [] } as ReplayRound,
      { roundNo: 10, players: [] } as ReplayRound,
      { roundNo: 11, players: [] } as ReplayRound,
    ])

    expect(isLegacyReplay(legacy)).toBe(true)
    expect(getReplayRoundWindow(legacy)).toMatchObject({
      playableKeys: [2, 10, 11],
      firstPlayableKey: 2,
      lastPlayableKey: 11,
      totalRounds: 10,
    })
    expect(getReplayDisplayRound(legacy, 10)).toBe(9)
    expect(getReplayDisplayRound(legacy, 11)).toBe(10)
  })

  it('uses actual round numbers for v2 pre/post snapshots', () => {
    const preFightPlayers = [{ playerId: 'player-1' }] as ReplayRound['preFightPlayers']
    const v2 = replay([
      { roundNo: 1, preFightPlayers, players: [] } as ReplayRound,
      { roundNo: 10, preFightPlayers, players: [] } as ReplayRound,
    ], 2)

    expect(isLegacyReplay(v2)).toBe(false)
    expect(getReplayRoundWindow(v2)).toMatchObject({
      playableKeys: [1, 10],
      firstPlayableKey: 1,
      lastPlayableKey: 10,
      totalRounds: 10,
    })
    expect(getReplayDisplayRound(v2, 10)).toBe(10)
  })

  it('pairs pre-fight actions and stats with the settled result instead of next-round skip state', () => {
    const preFight = player('PRE', status('PRE', {
      score: 5,
      place: 3,
      isReady: true,
      confirmedPredict: true,
      lvlUpPoints: 1,
      previousRoundLogs: 'ROUND_8',
    }))
    const result = player('RESULT', status('RESULT', {
      score: 42,
      place: 1,
      isReady: true,
      isSkip: true,
      previousRoundLogs: 'ROUND_9_ACTION\nROUND_9_SETTLED',
      personalLogs: 'NEXT_ROUND_BAN',
      scoreBreakdown: {
        roundMultiplier: 2,
        expectedRoundMultiplier: 2,
        entries: [{ source: 'Kira arrest', points: -500, isBonus: true }],
      },
    }))
    result.isDead = true
    result.deathSource = 'Kira'

    const aligned = buildShiftedPlayer(result, preFight)

    expect(aligned.character.name).toBe('PRE')
    expect(aligned.status.isSkip).toBe(false)
    expect(aligned.status.lvlUpPoints).toBe(1)
    expect(aligned.status.score).toBe(42)
    expect(aligned.status.place).toBe(1)
    expect(aligned.status.scoreBreakdown).toBe(result.status.scoreBreakdown)
    expect(aligned.status.scoreBreakdown?.entries[0]?.points).toBe(-500)
    expect(aligned.isDead).toBe(true)
    expect(aligned.deathSource).toBe('Kira')
    expect(aligned.status.personalLogs).toBe('ROUND_9_ACTION\nROUND_9_SETTLED')
    expect(aligned.status.personalLogs).not.toContain('NEXT_ROUND_BAN')
    expect(aligned.status.previousRoundLogs).toBe('ROUND_8')
  })

  it('keeps explicit final settlement logs without replaying the round-11 setup buffer', () => {
    const preFight = player('PRE', status('PRE', { previousRoundLogs: 'ROUND_9' }))
    const result = player('RESULT', status('RESULT', {
      previousRoundLogs: 'ROUND_10_SETTLED',
      personalLogs: 'ROUND_11_TILT',
    }))

    const aligned = buildShiftedPlayer(result, preFight, 'FINAL_SETTLEMENT')

    expect(aligned.status.personalLogs).toBe('ROUND_10_SETTLED\nFINAL_SETTLEMENT')
    expect(aligned.status.personalLogs).not.toContain('ROUND_11_TILT')
  })

  it('keeps the legacy final raw buffer when no explicit v2 settlement delta exists', () => {
    const legacyPlayer = {
      playerId: 'player-1',
      playerState: player('LEGACY', status('LEGACY', { personalLogs: 'LEGACY_FINAL_LOGS' })),
      // Old JSON receives this default during the API deserialize/serialize round-trip.
      finalSettlementLogs: '',
      customLeaderboardView: [],
    }

    expect(getReplaySettlementLogs(legacyPlayer, true)).toBe('LEGACY_FINAL_LOGS')
    expect(getReplaySettlementLogs(legacyPlayer, false)).toBe('')
  })

  it('uses the explicit final delta through the production store path', () => {
    const preFight = player('PRE', status('PRE', { isSkip: false }))
    const result = player('RESULT', status('RESULT', {
      score: 90,
      place: 1,
      isSkip: true,
      previousRoundLogs: 'ROUND_10_SETTLED',
      personalLogs: 'ROUND_11_TILT',
    }))
    const roundPlayer = {
      playerId: 'player-1',
      playerState: result,
      finalSettlementLogs: 'FINAL_PREDICTION_BONUS',
      customLeaderboardView: [],
    }
    const data = {
      replayFormatVersion: 2,
      gameId: 1,
      gameVersion: '4.3.4',
      gameMode: 'Normal',
      playerSummaries: [{ playerId: 'player-1' }],
      allCharacterNames: [],
      allCharacters: [],
      teams: [],
      rounds: [{
        roundNo: 10,
        globalLogs: '',
        allGlobalLogs: '',
        finalSettlementGlobalLogs: 'PITCHFORKS_AND_WINNER',
        finalSettlementAllGlobalLogs: 'PITCHFORKS_AND_WINNER',
        fightLog: [],
        preFightPlayers: [{
          playerId: 'player-1',
          playerState: preFight,
          customLeaderboardView: [],
        }],
        players: [roundPlayer],
      }],
    } as unknown as ReplayData

    const store = useReplayStore()
    store.replayData = data
    store.currentRound = 10

    const aligned = store.computedGameState?.players[0]
    expect(aligned?.status.score).toBe(90)
    expect(aligned?.status.place).toBe(1)
    expect(aligned?.status.isSkip).toBe(false)
    expect(aligned?.status.personalLogs).toBe('ROUND_10_SETTLED\nFINAL_PREDICTION_BONUS')
    expect(aligned?.status.personalLogs).not.toContain('ROUND_11_TILT')
    expect(store.computedGameState?.globalLogs).toBe('PITCHFORKS_AND_WINNER')
  })
})
