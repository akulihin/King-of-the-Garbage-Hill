import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type {
  ReplayData,
  ReplayRound,
  GameState,
  Player,
} from 'src/services/signalr'

const API_BASE = import.meta.env.VITE_API_BASE || ''

export const useReplayStore = defineStore('replay', () => {
  // ── State ─────────────────────────────────────────────────────────
  const replayData = ref<ReplayData | null>(null)
  const currentRound = ref(1) // 1-based
  const currentPlayerIndex = ref(0) // index into playerSummaries
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  // ── Derived ───────────────────────────────────────────────────────

  const totalRounds = computed(() => replayData.value?.totalRounds ?? 0)

  const currentRoundData = computed<ReplayRound | null>(() => {
    if (!replayData.value) return null
    return replayData.value.rounds.find(r => r.roundNo === currentRound.value) ?? null
  })

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

    // Build custom leaderboard lookup from the selected player's perspective
    const lbView = new Map(
      (myRoundPlayer.customLeaderboardView ?? []).map(e => [e.playerId, e])
    )

    // Build players array: full data for selected player, other players from their own perspective
    const players: Player[] = round.players.map(rp => {
      const lbEntry = lbView.get(rp.playerId)
      if (rp.playerId === playerId) {
        // Apply own custom leaderboard view to self too
        return lbEntry
          ? { ...myRoundPlayer.playerState, customLeaderboardPrefix: lbEntry.customLeaderboardPrefix, customLeaderboardText: lbEntry.customLeaderboardText }
          : myRoundPlayer.playerState
      }
      // For other players, use their own playerState but strip private data,
      // then overlay custom leaderboard from the selected player's perspective
      return {
        ...rp.playerState,
        predictions: undefined,
        deathNote: undefined,
        portalGun: undefined,
        exploitState: undefined,
        tsukuyomiState: undefined,
        passiveAbilityStates: undefined,
        customLeaderboardPrefix: lbEntry?.customLeaderboardPrefix ?? rp.playerState.customLeaderboardPrefix,
        customLeaderboardText: lbEntry?.customLeaderboardText ?? rp.playerState.customLeaderboardText,
      } as Player
    })

    return {
      gameId: data.gameId,
      roundNo: round.roundNo,
      turnLengthInSecond: 0,
      timePassedSeconds: 0,
      gameVersion: data.gameVersion,
      gameMode: data.gameMode,
      isFinished: true, // hides all action buttons
      isAramPickPhase: false,
      isDraftPickPhase: false,
      draftOptions: null,
      isKratosEvent: false,
      globalLogs: round.globalLogs ?? '',
      allGlobalLogs: round.allGlobalLogs ?? '',
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
        const maxRound = Math.max(...replayData.value.rounds.map(r => r.roundNo), 1)
        currentRound.value = maxRound
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
    const maxRound = Math.max(...replayData.value.rounds.map(r => r.roundNo), 1)
    currentRound.value = Math.max(1, Math.min(n, maxRound))
  }

  function setPlayer(idx: number) {
    if (!replayData.value) return
    currentPlayerIndex.value = Math.max(0, Math.min(idx, replayData.value.playerSummaries.length - 1))
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
    isLoading.value = false
    error.value = null
  }

  return {
    replayData,
    currentRound,
    currentPlayerIndex,
    isLoading,
    error,
    totalRounds,
    currentRoundData,
    currentPlayerId,
    computedGameState,
    loadReplay,
    setRound,
    setPlayer,
    setPlayerById,
    $reset,
  }
})
