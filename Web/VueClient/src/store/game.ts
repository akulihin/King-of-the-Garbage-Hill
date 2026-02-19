import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import {
  signalrService,
  type GameState,
  type Player,
  type LobbyState,
  type ActionResult,
  type GameEvent,
} from 'src/services/signalr'
import {
  playBlockSound,
  playLevelUpDefaultSound,
  playLevelUpStatSound,
  playMoralForPointsSound,
  playMoralForSkillSound,
} from 'src/services/sound'

type StatKey = 'intelligence' | 'strength' | 'speed' | 'psyche'
type PendingLevelUp = { stat: StatKey; startedAt: number }

const STAT_KEYS: StatKey[] = ['intelligence', 'strength', 'speed', 'psyche']
const STAT_INDEX_TO_KEY: Record<number, StatKey> = {
  1: 'intelligence',
  2: 'strength',
  3: 'speed',
  4: 'psyche',
}

function classLabel(classStatDisplayText: string): string {
  if (!classStatDisplayText) return ''
  return classStatDisplayText.split('||')[0].trim()
}

function increasedStats(previous: Player, next: Player): StatKey[] {
  return STAT_KEYS.filter((key) => next.character[key] > previous.character[key])
}

function isLevelUpAction(action: string): boolean {
  return action.toLowerCase().includes('levelup')
}

export const useGameStore = defineStore('game', () => {
  // ── State ─────────────────────────────────────────────────────────

  const discordId = ref<string>('')
  const isAuthenticated = ref(false)
  const isConnected = ref(false)

  const gameState = ref<GameState | null>(null)
  const lobbyState = ref<LobbyState | null>(null)
  const lastAction = ref<ActionResult | null>(null)
  const lastEvent = ref<GameEvent | null>(null)
  const errorMessage = ref<string | null>(null)
  const isLoading = ref(false)
  const pendingLevelUp = ref<PendingLevelUp | null>(null)
  const lastMoralToPointsRound = ref<number | null>(null)
  const lastMoralToSkillRound = ref<number | null>(null)

  // ── Derived State ─────────────────────────────────────────────────

  const myPlayer = computed<Player | null>(() => {
    if (!gameState.value) return null
    // Use the server-provided myPlayerId for reliable identification
    if (gameState.value.myPlayerId) {
      return gameState.value.players.find((p: Player) => p.playerId === gameState.value!.myPlayerId) ?? null
    }
    return null // spectator — no "my" player
  })

  const opponents = computed<Player[]>(() => {
    if (!gameState.value || !myPlayer.value) return []
    return gameState.value.players.filter(
      (p: Player) => p.playerId !== myPlayer.value!.playerId,
    )
  })

  const isMyTurn = computed(() => {
    if (!myPlayer.value) return false
    return !myPlayer.value.status.isReady && !myPlayer.value.status.isSkip
  })

  const roundTimeLeft = computed(() => {
    if (!gameState.value) return 0
    return Math.max(0, gameState.value.turnLengthInSecond - gameState.value.timePassedSeconds)
  })

  const isInGame = computed(() => gameState.value !== null && !gameState.value.isFinished)

  const isAdmin = computed(() => (gameState.value?.myPlayerType ?? 0) === 2)

  // ── Actions ───────────────────────────────────────────────────────

  async function connect() {
    isLoading.value = true
    try {
      // Set up event handlers before connecting
      signalrService.onGameState = (state) => {
        const previousState = gameState.value
        const previousMyPlayer = previousState?.myPlayerId
          ? previousState.players.find((p: Player) => p.playerId === previousState.myPlayerId) ?? null
          : null

        gameState.value = state
        errorMessage.value = null

        const nextMyPlayer = state.myPlayerId
          ? state.players.find((p: Player) => p.playerId === state.myPlayerId) ?? null
          : null

        if (pendingLevelUp.value && Date.now() - pendingLevelUp.value.startedAt > 8000) {
          pendingLevelUp.value = null
        }

        if (previousMyPlayer && nextMyPlayer) {
          const statsUp = increasedStats(previousMyPlayer, nextMyPlayer)
          const classChanged = classLabel(previousMyPlayer.character.classStatDisplayText)
            !== classLabel(nextMyPlayer.character.classStatDisplayText)

          const pendingStat = pendingLevelUp.value?.stat ?? null
          const leveledStat = pendingStat && statsUp.includes(pendingStat)
            ? pendingStat
            : (statsUp.length === 1 ? statsUp[0] : null)

          if (leveledStat) {
            const reachedMax = previousMyPlayer.character[leveledStat] < 10
              && nextMyPlayer.character[leveledStat] >= 10
            if (reachedMax) {
              playLevelUpStatSound(leveledStat, true)
            }
            else if (classChanged) {
              playLevelUpStatSound(leveledStat, false)
            }
          }

          if (
            pendingStat
            && (
              previousMyPlayer.character[pendingStat] !== nextMyPlayer.character[pendingStat]
              || previousMyPlayer.status.lvlUpPoints !== nextMyPlayer.status.lvlUpPoints
            )
          ) {
            pendingLevelUp.value = null
          }
        }
      }

      signalrService.onLobbyState = (state) => {
        lobbyState.value = state
      }

      signalrService.onActionResult = (result) => {
        lastAction.value = result
        if (!result.success && isLevelUpAction(result.action)) {
          pendingLevelUp.value = null
        }
        if (!result.success && result.error) {
          errorMessage.value = result.error
          setTimeout(() => {
            if (errorMessage.value === result.error) errorMessage.value = null
          }, 3000)
        }
      }

      signalrService.onGameEvent = (event) => {
        lastEvent.value = event
      }

      signalrService.onError = (error) => {
        errorMessage.value = error
      }

      signalrService.onAuthenticated = (data) => {
        if (data.success) {
          isAuthenticated.value = true
          // Keep as string to preserve precision on large snowflake IDs
          discordId.value = String(data.discordId)
        }
      }

      signalrService.onConnectionChanged = (connected) => {
        isConnected.value = connected
      }

      await signalrService.connect()
    }
    catch (err) {
      errorMessage.value = `Failed to connect: ${err}`
    }
    finally {
      isLoading.value = false
    }
  }

  async function authenticate(id: string) {
    discordId.value = id
    await signalrService.authenticate(id)
  }

  async function joinGame(gameId: number) {
    await signalrService.joinGame(gameId)
  }

  async function leaveGame(gameId: number) {
    await signalrService.leaveGame(gameId)
    gameState.value = null
  }

  async function refreshLobby() {
    await signalrService.requestLobbyState()
  }

  async function refreshGameState(gameId: number) {
    await signalrService.requestGameState(gameId)
  }

  // ── Game Action Wrappers ──────────────────────────────────────────

  async function attack(targetPlace: number) {
    if (!gameState.value) return
    await signalrService.attack(gameState.value.gameId, targetPlace)
  }

  async function block() {
    if (!gameState.value) return
    playBlockSound()
    await signalrService.block(gameState.value.gameId)
  }

  async function autoMove() {
    if (!gameState.value) return
    await signalrService.autoMove(gameState.value.gameId)
  }

  async function changeMind() {
    if (!gameState.value) return
    await signalrService.changeMind(gameState.value.gameId)
  }

  async function confirmSkip() {
    if (!gameState.value) return
    await signalrService.confirmSkip(gameState.value.gameId)
  }

  async function confirmPredict() {
    if (!gameState.value) return
    await signalrService.confirmPredict(gameState.value.gameId)
  }

  async function levelUp(statIndex: number) {
    if (!gameState.value) return
    const stat = STAT_INDEX_TO_KEY[statIndex]
    if (!stat) return
    pendingLevelUp.value = { stat, startedAt: Date.now() }
    playLevelUpDefaultSound()
    await signalrService.levelUp(gameState.value.gameId, statIndex)
  }

  async function moralToPoints() {
    if (!gameState.value) return
    if (lastMoralToPointsRound.value !== gameState.value.roundNo) {
      playMoralForPointsSound()
      lastMoralToPointsRound.value = gameState.value.roundNo
    }
    await signalrService.moralToPoints(gameState.value.gameId)
  }

  async function moralToSkill() {
    if (!gameState.value) return
    if (lastMoralToSkillRound.value !== gameState.value.roundNo) {
      playMoralForSkillSound()
      lastMoralToSkillRound.value = gameState.value.roundNo
    }
    await signalrService.moralToSkill(gameState.value.gameId)
  }

  async function predict(targetPlayerId: string, characterName: string) {
    if (!gameState.value) return
    await signalrService.predict(gameState.value.gameId, targetPlayerId, characterName)
  }

  async function aramReroll(slot: number) {
    if (!gameState.value) return
    await signalrService.aramReroll(gameState.value.gameId, slot)
  }

  async function aramConfirm() {
    if (!gameState.value) return
    await signalrService.aramConfirm(gameState.value.gameId)
  }

  async function setPreferWeb(preferWeb: boolean) {
    if (!gameState.value) return
    await signalrService.setPreferWeb(gameState.value.gameId, preferWeb)
  }

  async function finishGame() {
    if (!gameState.value) return
    await signalrService.finishGame(gameState.value.gameId)
  }

  return {
    // State
    discordId,
    isAuthenticated,
    isConnected,
    gameState,
    lobbyState,
    lastAction,
    lastEvent,
    errorMessage,
    isLoading,
    // Computed
    myPlayer,
    opponents,
    isMyTurn,
    roundTimeLeft,
    isInGame,
    isAdmin,
    // Actions
    connect,
    authenticate,
    joinGame,
    leaveGame,
    refreshLobby,
    refreshGameState,
    attack,
    block,
    autoMove,
    changeMind,
    confirmSkip,
    confirmPredict,
    levelUp,
    moralToPoints,
    moralToSkill,
    predict,
    aramReroll,
    aramConfirm,
    setPreferWeb,
    finishGame,
  }
})
