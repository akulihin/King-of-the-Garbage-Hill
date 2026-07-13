import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type {
  ReplayData,
  ReplayRound,
  ReplayRoundPlayer,
  GameState,
  Player,
} from 'src/services/signalr'

const API_BASE = import.meta.env.VITE_API_BASE || ''

export type ReplayRoundWindow = {
  legacy: boolean
  playableKeys: number[]
  firstPlayableKey: number
  lastPlayableKey: number
  totalRounds: number
}

export function isLegacyReplay(data: ReplayData): boolean {
  return (data.replayFormatVersion ?? 0) < 2
    || data.rounds.some(round => !round.preFightPlayers?.length)
}

/** Raw URL keys stay unchanged for legacy deep links; display numbers are logical combat rounds. */
export function getReplayRoundWindow(data: ReplayData): ReplayRoundWindow {
  const orderedKeys = [...new Set(data.rounds.map(round => round.roundNo))].sort((a, b) => a - b)
  const legacy = isLegacyReplay(data)
  const playableKeys = legacy && orderedKeys.length > 1 ? orderedKeys.slice(1) : orderedKeys
  const firstPlayableKey = playableKeys[0] ?? 1
  const lastPlayableKey = playableKeys[playableKeys.length - 1] ?? firstPlayableKey
  const initialBoundary = orderedKeys[0] ?? 1
  const totalRounds = legacy
    ? Math.max(1, lastPlayableKey - initialBoundary)
    : Math.max(1, lastPlayableKey)

  return { legacy, playableKeys, firstPlayableKey, lastPlayableKey, totalRounds }
}

export function getReplayDisplayRound(data: ReplayData, rawRoundKey: number): number {
  if (!isLegacyReplay(data)) return rawRoundKey
  const roundKeys = data.rounds.map(round => round.roundNo)
  const initialBoundary = roundKeys.length ? Math.min(...roundKeys) : 1
  return Math.max(1, rawRoundKey - initialBoundary)
}

export function getReplaySettlementLogs(
  player: ReplayRoundPlayer,
  includeLegacyFinalBuffer: boolean,
): string {
  if (includeLegacyFinalBuffer) return player.playerState.status.personalLogs
  return player.finalSettlementLogs ?? ''
}

/**
 * Combines one round's pre-fight state with its settled result state.
 * Fight-facing stats/actions come from preFight; score/place/breakdown come from result.
 */
export function buildShiftedPlayer(
  result: Player,
  preFight: Player | undefined,
  finalSettlementLogs = '',
): Player {
  if (!preFight) return result

  const personalLogs = [
    result.status.previousRoundLogs,
    finalSettlementLogs,
  ].filter(log => log?.trim()).join('\n')

  return {
    // Identity and settled leaderboard result.
    playerId: result.playerId,
    discordUsername: result.discordUsername,
    isBot: result.isBot,
    isWebPlayer: result.isWebPlayer,
    teamId: result.teamId,
    customLeaderboardPrefix: result.customLeaderboardPrefix,
    customLeaderboardText: result.customLeaderboardText,
    characterMasteryPoints: result.characterMasteryPoints,
    isInMyHarmRange: preFight.isInMyHarmRange,
    status: {
      ...result.status,
      isReady: preFight.status.isReady,
      isBlock: preFight.status.isBlock,
      isSkip: preFight.status.isSkip,
      isAutoMove: preFight.status.isAutoMove,
      confirmedPredict: preFight.status.confirmedPredict,
      confirmedSkip: preFight.status.confirmedSkip,
      lvlUpPoints: preFight.status.lvlUpPoints,
      moveListPage: preFight.status.moveListPage,
      personalLogs,
      previousRoundLogs: preFight.status.previousRoundLogs,
      directMessages: preFight.status.directMessages,
      mediaMessages: preFight.status.mediaMessages,
      isAramRollConfirmed: preFight.status.isAramRollConfirmed,
      isDraftPickConfirmed: preFight.status.isDraftPickConfirmed,
      aramRerolledPassivesTimes: preFight.status.aramRerolledPassivesTimes,
      aramRerolledStatsTimes: preFight.status.aramRerolledStatsTimes,
    },
    // Fight-facing character/passive state.
    character: preFight.character,
    isDead: result.isDead,
    deathSource: result.deathSource,
    isKira: preFight.isKira,
    isTerminalMode: preFight.isTerminalMode,
    deathNote: preFight.deathNote,
    portalGun: preFight.portalGun,
    terminalState: preFight.terminalState,
    tsukuyomiState: preFight.tsukuyomiState,
    passiveAbilityStates: preFight.passiveAbilityStates,
    hasTerminalMarker: preFight.hasTerminalMarker,
    darksciChoiceNeeded: preFight.darksciChoiceNeeded,
    youngGlebAvailable: preFight.youngGlebAvailable,
    dopaChoiceNeeded: preFight.dopaChoiceNeeded,
    predictions: preFight.predictions,
  }
}

export const useReplayStore = defineStore('replay', () => {
  // ── State ─────────────────────────────────────────────────────────
  const replayData = ref<ReplayData | null>(null)
  const currentRound = ref(1) // 1-based
  const currentPlayerIndex = ref(0) // index into playerSummaries
  const currentFightIndex = ref(0) // 0-based fight within current round
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  // ── Derived ───────────────────────────────────────────────────────

  const roundWindow = computed<ReplayRoundWindow>(() => replayData.value
    ? getReplayRoundWindow(replayData.value)
    : { legacy: false, playableKeys: [], firstPlayableKey: 1, lastPlayableKey: 1, totalRounds: 0 })
  const totalRounds = computed(() => roundWindow.value.totalRounds)
  const displayRound = computed(() => replayData.value
    ? getReplayDisplayRound(replayData.value, currentRound.value)
    : 1)
  const canPreviousRound = computed(() => {
    const index = roundWindow.value.playableKeys.indexOf(currentRound.value)
    return index > 0
  })
  const canNextRound = computed(() => {
    const index = roundWindow.value.playableKeys.indexOf(currentRound.value)
    return index >= 0 && index < roundWindow.value.playableKeys.length - 1
  })

  const currentRoundData = computed<ReplayRound | null>(() => {
    if (!replayData.value) return null
    return replayData.value.rounds.find(r => r.roundNo === currentRound.value) ?? null
  })

  const currentPreFightPlayers = computed<ReplayRoundPlayer[]>(() => {
    const data = replayData.value
    const round = currentRoundData.value
    if (!data || !round) return []
    if (!roundWindow.value.legacy) return round.preFightPlayers ?? []

    const ordered = [...data.rounds].sort((left, right) => left.roundNo - right.roundNo)
    const resultIndex = ordered.findIndex(candidate => candidate.roundNo === round.roundNo)
    return resultIndex > 0 ? ordered[resultIndex - 1].players : []
  })

  const includeLegacyFinalBuffer = computed(() =>
    roundWindow.value.legacy && currentRound.value === roundWindow.value.lastPlayableKey)

  const currentPlayerId = computed(() => {
    if (!replayData.value || currentPlayerIndex.value >= replayData.value.playerSummaries.length) return null
    return replayData.value.playerSummaries[currentPlayerIndex.value].playerId
  })

  /**
   * Reconstructs a GameState from the selected round + player perspective.
   * Populates enough fields for existing components to render.
   */
  const computedGameState = computed<GameState | null>(() => {
    const data = replayData.value
    const round = currentRoundData.value
    const playerId = currentPlayerId.value
    if (!data || !round || !playerId) return null

    // Find the selected player's full PlayerDto for this round
    const myRoundPlayer = round.players.find(rp => rp.playerId === playerId)
    if (!myRoundPlayer) return null

    // Explicit in v2; for legacy files this is reconstructed from the preceding boundary snapshot.
    const prePlayerMap = new Map<string, ReplayRoundPlayer>(
      currentPreFightPlayers.value.map((rp: ReplayRoundPlayer) => [rp.playerId, rp] as [string, ReplayRoundPlayer])
    )

    // Fight-facing annotations use the selected viewer's pre-fight perspective.
    const preMyRoundPlayer = prePlayerMap.get(playerId)
    const lbView = new Map(
      (preMyRoundPlayer?.customLeaderboardView ?? myRoundPlayer.customLeaderboardView ?? [])
        .map(e => [e.playerId, e])
    )

    // Build players array: full data for selected player, other players from their own perspective
    const players: Player[] = round.players.map(rp => {
      const lbEntry = lbView.get(rp.playerId)
      const preRp = prePlayerMap.get(rp.playerId)
      if (rp.playerId === playerId) {
        // Apply own custom leaderboard view to self too
        const base = lbEntry
          ? { ...myRoundPlayer.playerState, customLeaderboardPrefix: lbEntry.customLeaderboardPrefix, customLeaderboardText: lbEntry.customLeaderboardText }
          : myRoundPlayer.playerState
        return buildShiftedPlayer(
          base,
          preRp?.playerState,
          getReplaySettlementLogs(rp, includeLegacyFinalBuffer.value),
        )
      }
      // For other players, use their own playerState but strip private data,
      // then overlay custom leaderboard from the selected player's perspective
      const otherBase = {
        ...rp.playerState,
        predictions: undefined,
        deathNote: undefined,
        portalGun: undefined,
        terminalState: undefined,
        tsukuyomiState: undefined,
        passiveAbilityStates: undefined,
        customLeaderboardPrefix: lbEntry?.customLeaderboardPrefix ?? rp.playerState.customLeaderboardPrefix,
        customLeaderboardText: lbEntry?.customLeaderboardText ?? rp.playerState.customLeaderboardText,
      } as Player
      const preOtherBase = preRp ? {
        ...preRp.playerState,
        predictions: undefined,
        deathNote: undefined,
        portalGun: undefined,
        terminalState: undefined,
        tsukuyomiState: undefined,
        passiveAbilityStates: undefined,
      } as Player : undefined
      return buildShiftedPlayer(
        otherBase,
        preOtherBase,
        getReplaySettlementLogs(rp, includeLegacyFinalBuffer.value),
      )
    })

    return {
      gameId: data.gameId,
      roundNo: displayRound.value,
      turnLengthInSecond: 0,
      timePassedSeconds: 0,
      gameVersion: data.gameVersion,
      gameMode: data.gameMode,
      isFinished: true, // hides all action buttons
      isAramPickPhase: false,
      isDraftPickPhase: false,
      draftOptions: null,
      isKratosEvent: false,
      globalLogs: [round.globalLogs, round.finalSettlementGlobalLogs]
        .filter(log => log?.trim()).join('\n'),
      allGlobalLogs: [round.allGlobalLogs, round.finalSettlementAllGlobalLogs]
        .filter(log => log?.trim()).join('\n'),
      fullChronicle: data.fullChronicle ?? undefined,
      myPlayerId: playerId,
      myPlayerType: 2, // admin-level visibility for replays
      preferWeb: true,
      allCharacterNames: data.allCharacterNames,
      allCharacters: data.allCharacters,
      players,
      teams: data.teams,
      fightLog: round.fightLog ?? [],
    }
  })

  // ── Actions ───────────────────────────────────────────────────────

  async function loadReplay(gameId: number | string) {
    isLoading.value = true
    error.value = null
    try {
      const resp = await fetch(`${API_BASE}/api/game/replay/${gameId}`)
      if (!resp.ok) {
        error.value = resp.status === 404 ? 'Replay not found' : `Error ${resp.status}`
        return
      }
      replayData.value = await resp.json()
      // Default to last round, first player
      if (replayData.value) {
        const window = getReplayRoundWindow(replayData.value)
        currentRound.value = window.lastPlayableKey
        currentPlayerIndex.value = 0
      }
    } catch (e) {
      error.value = 'Failed to load replay'
      console.error('[Replay]', e)
    } finally {
      isLoading.value = false
    }
  }

  function setRound(n: number) {
    if (!replayData.value) return
    const keys = getReplayRoundWindow(replayData.value).playableKeys
    if (!keys.length) return
    currentRound.value = keys.reduce((closest, key) =>
      Math.abs(key - n) < Math.abs(closest - n) ? key : closest, keys[0])
    currentFightIndex.value = 0
  }

  function previousRound() {
    const keys = roundWindow.value.playableKeys
    const index = keys.indexOf(currentRound.value)
    if (index > 0) setRound(keys[index - 1])
  }

  function nextRound() {
    const keys = roundWindow.value.playableKeys
    const index = keys.indexOf(currentRound.value)
    if (index >= 0 && index < keys.length - 1) setRound(keys[index + 1])
  }

  function setPlayer(idx: number) {
    if (!replayData.value) return
    currentPlayerIndex.value = Math.max(0, Math.min(idx, replayData.value.playerSummaries.length - 1))
    currentFightIndex.value = 0
  }

  function setFight(idx: number) {
    currentFightIndex.value = Math.max(0, idx)
  }

  function setPlayerById(id: string) {
    if (!replayData.value) return
    const idx = replayData.value.playerSummaries.findIndex(p => p.playerId === id)
    if (idx >= 0) currentPlayerIndex.value = idx
  }

  function $reset() {
    replayData.value = null
    currentRound.value = 1
    currentPlayerIndex.value = 0
    currentFightIndex.value = 0
    isLoading.value = false
    error.value = null
  }

  return {
    replayData,
    currentRound,
    currentPlayerIndex,
    currentFightIndex,
    isLoading,
    error,
    totalRounds,
    displayRound,
    canPreviousRound,
    canNextRound,
    currentRoundData,
    currentPreFightPlayers,
    includeLegacyFinalBuffer,
    currentPlayerId,
    computedGameState,
    loadReplay,
    setRound,
    previousRound,
    nextRound,
    setPlayer,
    setPlayerById,
    setFight,
    $reset,
  }
})
