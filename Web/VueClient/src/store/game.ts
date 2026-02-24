import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import {
  signalrService,
  type GameState,
  type Player,
  type LobbyState,
  type ActionResult,
  type GameEvent,
  type BlackjackTableState,
} from 'src/services/signalr'
import {
  playBlockSound,
  playAnyMoveTurn10PlusLayer,
  isLateGameCharacter,
  playLevelUpDefaultSound,
  playLevelUpStatSound,
  playMoralForPointsSound,
  playMoralForSkillSound,
} from 'src/services/sound'

type StatKey = 'intelligence' | 'strength' | 'speed' | 'psyche'
type PendingLevelUp = { stat: StatKey; startedAt: number }

const STAT_KEYS: StatKey[] = ['intelligence', 'strength', 'speed', 'psyche']
/** Priority order for level-up sound selection (INT > STR > SPD > PSY) */
const STAT_PRIORITY: StatKey[] = ['intelligence', 'strength', 'speed', 'psyche']
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
  const webUsername = ref<string>('')
  const isWebAccount = ref(false)

  const gameState = ref<GameState | null>(null)
  const lobbyState = ref<LobbyState | null>(null)
  const lastAction = ref<ActionResult | null>(null)
  const lastEvent = ref<GameEvent | null>(null)
  const errorMessage = ref<string | null>(null)
  const isLoading = ref(false)
  const pendingLevelUp = ref<PendingLevelUp | null>(null)
  const lastMoralToPointsRound = ref<number | null>(null)
  const lastMoralToSkillRound = ref<number | null>(null)
  const gameStory = ref<string | null>(null)
  const blackjackState = ref<BlackjackTableState | null>(null)

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

  const isKira = computed(() => myPlayer.value?.isKira ?? false)

  const myPortalGun = computed(() => myPlayer.value?.portalGun ?? null)

  const isBug = computed(() => myPlayer.value?.isBug ?? false)

  const myExploitState = computed(() => myPlayer.value?.exploitState ?? null)

  const myPickleRick = computed(() => myPlayer.value?.passiveAbilityStates?.pickleRick ?? null)

  const myGiantBeans = computed(() => myPlayer.value?.passiveAbilityStates?.giantBeans ?? null)

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

          // Check ALL increased stats in priority order for max sounds
          let playedMaxSound = false
          for (const stat of STAT_PRIORITY) {
            if (statsUp.includes(stat)
              && previousMyPlayer.character[stat] < 10
              && nextMyPlayer.character[stat] >= 10) {
              playLevelUpStatSound(stat, true)
              playedMaxSound = true
              break
            }
          }

          // If no max sound played, check for class-change sound
          if (!playedMaxSound && classChanged) {
            // Pick the pending stat if it increased, otherwise first increased stat by priority
            const pendingStat = pendingLevelUp.value?.stat ?? null
            const soundStat = (pendingStat && statsUp.includes(pendingStat))
              ? pendingStat
              : STAT_PRIORITY.find(s => statsUp.includes(s)) ?? null
            if (soundStat) {
              playLevelUpStatSound(soundStat, false)
            }
          }

          const pendingStat = pendingLevelUp.value?.stat ?? null
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

        // Game-start max check: first state received with a stat already at 10
        if (!previousMyPlayer && nextMyPlayer) {
          for (const stat of STAT_PRIORITY) {
            if (nextMyPlayer.character[stat] >= 10) {
              playLevelUpStatSound(stat, true)
              break
            }
          }
        }

        // Auto-join Blackjack when killed by Kira
        if (nextMyPlayer?.isDead && nextMyPlayer?.deathSource === 'Kira' && !(previousMyPlayer?.isDead && previousMyPlayer?.deathSource === 'Kira')) {
          signalrService.blackjackJoin(state.gameId)
        }
      }

      signalrService.onBlackjackState = (state) => {
        blackjackState.value = state
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
        if (event.eventType === 'GameStory') {
          const data = event.data as { story: string } | undefined
          if (data?.story) {
            gameStory.value = data.story
          }
        }
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

      signalrService.onWebAccountCreated = (data) => {
        isAuthenticated.value = true
        isWebAccount.value = true
        discordId.value = data.discordId
        webUsername.value = data.username
        // Persist for session restoration
        localStorage.setItem('kotgh_web_id', data.discordId)
        localStorage.setItem('kotgh_web_username', data.username)
      }

      signalrService.onGameCreated = (_data) => {
        // Navigation handled by the caller
      }

      signalrService.onGameJoined = (_data) => {
        // Navigation handled by the caller
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
    gameStory.value = null
    await signalrService.joinGame(gameId)
  }

  async function leaveGame(gameId: number) {
    await signalrService.leaveGame(gameId)
    gameState.value = null
    gameStory.value = null
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
    if (gameState.value.roundNo >= 10) {
      const charName = myPlayer.value?.character.name
      playAnyMoveTurn10PlusLayer(charName ? isLateGameCharacter(charName) : false)
    }
    await signalrService.block(gameState.value.gameId)
  }

  async function autoMove() {
    if (!gameState.value) return
    if (gameState.value.roundNo >= 10) {
      const charName = myPlayer.value?.character.name
      playAnyMoveTurn10PlusLayer(charName ? isLateGameCharacter(charName) : false)
    }
    await signalrService.autoMove(gameState.value.gameId)
  }

  async function changeMind() {
    if (!gameState.value) return
    await signalrService.changeMind(gameState.value.gameId)
  }

  async function confirmSkip() {
    if (!gameState.value) return
    if (gameState.value.roundNo >= 10) {
      const charName = myPlayer.value?.character.name
      playAnyMoveTurn10PlusLayer(charName ? isLateGameCharacter(charName) : false)
    }
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

  async function darksciChoice(isStable: boolean) {
    if (!gameState.value) return
    await signalrService.darksciChoice(gameState.value.gameId, isStable)
  }

  async function youngGleb() {
    if (!gameState.value) return
    await signalrService.youngGleb(gameState.value.gameId)
  }

  async function dopaChoice(tactic: string) {
    if (!gameState.value) return
    await signalrService.dopaChoice(gameState.value.gameId, tactic)
  }

  async function deathNoteWrite(targetPlayerId: string, characterName: string) {
    if (!gameState.value) return
    await signalrService.deathNoteWrite(gameState.value.gameId, targetPlayerId, characterName)
  }

  async function shinigamiEyes() {
    if (!gameState.value) return
    await signalrService.shinigamiEyes(gameState.value.gameId)
  }

  // ── Blackjack Actions ──────────────────────────────────────────────

  async function blackjackJoin() {
    if (!gameState.value) return
    await signalrService.blackjackJoin(gameState.value.gameId)
  }

  async function blackjackHit() {
    if (!gameState.value) return
    await signalrService.blackjackHit(gameState.value.gameId)
  }

  async function blackjackStand() {
    if (!gameState.value) return
    await signalrService.blackjackStand(gameState.value.gameId)
  }

  async function blackjackNewRound() {
    if (!gameState.value) return
    await signalrService.blackjackNewRound(gameState.value.gameId)
  }

  async function blackjackSendMessage(words: string[]) {
    if (!gameState.value) return
    await signalrService.blackjackSendMessage(gameState.value.gameId, words)
  }

  async function setPreferWeb(preferWeb: boolean) {
    if (!gameState.value) return
    await signalrService.setPreferWeb(gameState.value.gameId, preferWeb)
  }

  async function finishGame() {
    if (!gameState.value) return
    await signalrService.finishGame(gameState.value.gameId)
  }

  async function registerWebAccount(username: string) {
    await signalrService.registerWebAccount(username)
  }

  async function createWebGame() {
    await signalrService.createWebGame()
  }

  async function joinWebGame(gameId: number) {
    await signalrService.joinWebGame(gameId)
  }

  /** Restore a previously created web account from localStorage */
  async function restoreWebSession() {
    const savedId = localStorage.getItem('kotgh_web_id')
    const savedUsername = localStorage.getItem('kotgh_web_username')
    if (savedId && savedUsername) {
      discordId.value = savedId
      webUsername.value = savedUsername
      isWebAccount.value = true
      // Authenticate with the saved web ID
      await signalrService.authenticate(savedId)
    }
  }

  return {
    // State
    discordId,
    isAuthenticated,
    isConnected,
    webUsername,
    isWebAccount,
    gameState,
    lobbyState,
    lastAction,
    lastEvent,
    errorMessage,
    isLoading,
    gameStory,
    blackjackState,
    // Computed
    myPlayer,
    opponents,
    isMyTurn,
    roundTimeLeft,
    isInGame,
    isAdmin,
    isKira,
    myPortalGun,
    isBug,
    myExploitState,
    myPickleRick,
    myGiantBeans,
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
    darksciChoice,
    youngGleb,
    dopaChoice,
    deathNoteWrite,
    shinigamiEyes,
    blackjackJoin,
    blackjackHit,
    blackjackStand,
    blackjackNewRound,
    blackjackSendMessage,
    setPreferWeb,
    finishGame,
    registerWebAccount,
    createWebGame,
    joinWebGame,
    restoreWebSession,
  }
})
