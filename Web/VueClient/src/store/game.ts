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
  type QuestState,
  type LootBoxResult,
  type AchievementBoard,
  type AchievementEntry,
  type StoreState,
  type CharacterListEntry,
  type DoomFortressState,
} from 'src/services/signalr'
import {
  playBlockSound,
  playAnyMoveTurn10PlusLayer,
  isLateGameCharacter,
  playLevelUpDefaultSound,
  playLevelUpStatSound,
  playMoralForPointsSound,
  playMoralForSkillSound,
  playGeraltMeditation,
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
  const rewritingHistoryRound = ref<number | null>(null)
  const gameStory = ref<string | null>(null)
  const blackjackState = ref<BlackjackTableState | null>(null)
  const questState = ref<QuestState | null>(null)
  const isQuestsLoading = ref(false)
  const questsError = ref<string | null>(null)
  const rerollingQuestId = ref<string | null>(null)
  let questsRefreshPending = false
  const lootBoxResult = ref<LootBoxResult | null>(null)
  const isLootBoxFlowActive = ref(false)
  const achievementBoard = ref<AchievementBoard | null>(null)
  const newlyUnlockedAchievements = ref<AchievementEntry[]>([])
  const achievementsDismissedForGame = ref<string>('')
  const isAchievementsLoading = ref(false)
  const achievementsError = ref<string | null>(null)
  const isAcknowledgingAchievements = ref(false)
  const achievementAcknowledgeError = ref<string | null>(null)
  const storeState = ref<StoreState | null>(null)
  const isStoreLoading = ref(false)
  const storeAction = ref<string | null>(null)
  const storeError = ref<string | null>(null)
  const accountPlayerType = ref(0)
  const lastPlayedCharacter = ref('')
  const characterList = ref<CharacterListEntry[]>([])
  const doomFortressState = ref<DoomFortressState | null>(null)

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
    if (gameState.value?.isRoundTransitionPaused) return false
    if (gameState.value?.roundNo === 8 && myPlayer.value.character.name === 'Мадара') return false
    return !myPlayer.value.status.isReady && !myPlayer.value.status.isSkip
  })

  // A pending level-up must be spent before the player can continue — matches Discord, which
  // hides the fight controls until the points are spent. Gates the four turn-ending actions. (M15)
  const mustSpendLevelUp = computed(() => (myPlayer.value?.status.lvlUpPoints ?? 0) > 0)
  const isLevelingUp = computed(() => pendingLevelUp.value !== null)

  const roundTimeLeft = computed(() => {
    if (!gameState.value) return 0
    return Math.max(0, gameState.value.turnLengthInSecond - gameState.value.timePassedSeconds)
  })

  const isInGame = computed(() => gameState.value !== null && !gameState.value.isFinished)

  const isAdmin = computed(() => (gameState.value?.myPlayerType ?? 0) === 2)

  const isLobbyAdmin = computed(() => accountPlayerType.value === 2)

  const isKira = computed(() => myPlayer.value?.isKira ?? false)

  const myPortalGun = computed(() => myPlayer.value?.portalGun ?? null)

  const isTerminalMode = computed(() => myPlayer.value?.isTerminalMode ?? false)
  const isDeepSession = computed(() => myPlayer.value?.isDeepSession ?? false)
  const depthsCallPromptActive = computed(
    () => myPlayer.value?.depthsCallPromptActive ?? false,
  )

  const myTerminalState = computed(() => myPlayer.value?.terminalState ?? null)

  const myPickleRick = computed(() => myPlayer.value?.passiveAbilityStates?.pickleRick ?? null)

  const myGiantBeans = computed(() => myPlayer.value?.passiveAbilityStates?.giantBeans ?? null)

  // Pickle Rick may still fire a charged Portal Gun during his (otherwise "ready") pickle turn.
  const canFireGunDuringPickle = computed(() => {
    return (
      (myPickleRick.value?.pickleTurnsRemaining ?? 0) > 0 &&
      (myPortalGun.value?.invented ?? false) &&
      (myPortalGun.value?.charges ?? 0) > 0
    )
  })

  // ── Actions ───────────────────────────────────────────────────────

  async function connect() {
    isLoading.value = true
    errorMessage.value = null
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

        // Detect newly unlocked achievements from finished game state (only once per game)
        if (state.isFinished && state.newlyUnlockedAchievements?.length && achievementsDismissedForGame.value !== String(state.gameId)) {
          achievementAcknowledgeError.value = null
          newlyUnlockedAchievements.value = state.newlyUnlockedAchievements
        }
      }

      signalrService.onBlackjackState = (state) => {
        blackjackState.value = state
      }

      signalrService.onQuestState = (state) => {
        const refreshAgain = questsRefreshPending
        questsRefreshPending = false
        questState.value = state
        isQuestsLoading.value = false
        questsError.value = null
        rerollingQuestId.value = null
        if (state.lastUnacknowledgedLootBox && !lootBoxResult.value) {
          lootBoxResult.value = state.lastUnacknowledgedLootBox
        }
        if (refreshAgain) void requestQuests()
      }

      signalrService.onLootBoxOpened = (result) => {
        lootBoxResult.value = result
      }

      signalrService.onAchievementBoard = (board) => {
        achievementBoard.value = board
        // The server keeps unlocks unacknowledged until the celebration is dismissed.
        // Rehydrate the full cards after a refresh/reconnect so the reward moment is
        // not reduced to a small badge on the achievements page.
        if (newlyUnlockedAchievements.value.length === 0 && board.newlyUnlocked.length > 0) {
          const unseenIds = new Set(board.newlyUnlocked)
          achievementAcknowledgeError.value = null
          newlyUnlockedAchievements.value = board.achievements.filter(
            achievement => achievement.isUnlocked && unseenIds.has(achievement.id),
          )
        }
        isAchievementsLoading.value = false
        achievementsError.value = null
      }

      signalrService.onStoreState = (state) => {
        storeState.value = state
        isStoreLoading.value = false
        storeAction.value = null
        storeError.value = null
        if (questState.value) {
          questState.value = { ...questState.value, zbsPoints: state.zbsPoints }
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
        if (event.eventType === 'GameStory') {
          const data = event.data as { story: string } | undefined
          if (data?.story) {
            gameStory.value = data.story
          }
        }
      }

      signalrService.onError = (error) => {
        errorMessage.value = error
        if (isQuestsLoading.value || rerollingQuestId.value) {
          isQuestsLoading.value = false
          rerollingQuestId.value = null
          questsError.value = error
        }
        if (isAchievementsLoading.value) {
          isAchievementsLoading.value = false
          achievementsError.value = error
        }
        if (isStoreLoading.value || storeAction.value) {
          isStoreLoading.value = false
          storeAction.value = null
          storeError.value = error
        }
      }

      signalrService.onAuthenticated = (data) => {
        if (data.success) {
          isAuthenticated.value = true
          errorMessage.value = null
          // Keep as string to preserve precision on large snowflake IDs
          discordId.value = String(data.discordId)
          accountPlayerType.value = data.playerType ?? 0
          lastPlayedCharacter.value = data.lastPlayedCharacter ?? ''
          const requests = [requestAchievements(), requestQuests()]
          if (storeState.value) requests.push(requestStore())
          void Promise.allSettled(requests)
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
        errorMessage.value = null
        void Promise.allSettled([requestAchievements(), requestQuests()])
      }

      signalrService.onGameCreated = (_data) => {
        // Navigation handled by the caller
      }

      signalrService.onGameJoined = (_data) => {
        // Navigation handled by the caller
      }

      signalrService.onCharacterList = (list) => {
        characterList.value = list
      }

      signalrService.onDoomFortressState = (state) => {
        doomFortressState.value = state
      }

      signalrService.onConnectionChanged = (connected) => {
        isConnected.value = connected
      }

      await signalrService.connect()
    }
    catch (err) {
      errorMessage.value = `Failed to connect: ${err}`
      throw err
    }
    finally {
      isLoading.value = false
    }
  }

  async function authenticate(id: string) {
    if (discordId.value && discordId.value !== id) {
      resetAccountState()
      isConnected.value = signalrService.isConnected
    }
    isLoading.value = true
    errorMessage.value = null
    discordId.value = id
    try {
      await signalrService.authenticate(id)
      if (!isAuthenticated.value || discordId.value !== id) {
        throw new Error(errorMessage.value ?? 'Authentication failed. Check the account ID and try again.')
      }
    }
    catch (error) {
      if (!errorMessage.value) {
        errorMessage.value = error instanceof Error ? error.message : String(error)
      }
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  function resetAccountState() {
    discordId.value = ''
    isAuthenticated.value = false
    isConnected.value = false
    webUsername.value = ''
    isWebAccount.value = false

    gameState.value = null
    lobbyState.value = null
    lastAction.value = null
    lastEvent.value = null
    errorMessage.value = null
    isLoading.value = false
    pendingLevelUp.value = null
    lastMoralToPointsRound.value = null
    lastMoralToSkillRound.value = null
    rewritingHistoryRound.value = null
    gameStory.value = null
    blackjackState.value = null

    questState.value = null
    isQuestsLoading.value = false
    questsError.value = null
    rerollingQuestId.value = null
    questsRefreshPending = false
    lootBoxResult.value = null
    isLootBoxFlowActive.value = false
    achievementBoard.value = null
    newlyUnlockedAchievements.value = []
    achievementsDismissedForGame.value = ''
    isAchievementsLoading.value = false
    achievementsError.value = null
    isAcknowledgingAchievements.value = false
    achievementAcknowledgeError.value = null
    storeState.value = null
    isStoreLoading.value = false
    storeAction.value = null
    storeError.value = null
    accountPlayerType.value = 0
    lastPlayedCharacter.value = ''
    doomFortressState.value = null
  }

  async function logout() {
    localStorage.removeItem('discordId')
    localStorage.removeItem('kotgh_web_id')
    localStorage.removeItem('kotgh_web_username')
    resetAccountState()
    try {
      await signalrService.disconnect()
    }
    finally {
      // Transport callbacks already queued before disconnect may still run while
      // the connection is stopping. Keep logout as a hard session boundary.
      resetAccountState()
    }
  }

  async function setLanguage(language: 'ru' | 'en') {
    await signalrService.setLanguage(language)
  }

  async function joinGame(gameId: number) {
    gameStory.value = null
    lootBoxResult.value = null
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
    if (mustSpendLevelUp.value) return
    await signalrService.attack(gameState.value.gameId, targetPlace)
  }

  async function block() {
    if (!gameState.value) return
    if (mustSpendLevelUp.value) return
    playBlockSound()
    if (myPlayer.value?.character.name === 'Геральт') playGeraltMeditation()
    if (gameState.value.roundNo >= 10) {
      const charName = myPlayer.value?.character.name
      playAnyMoveTurn10PlusLayer(charName ? isLateGameCharacter(charName) : false)
    }
    await signalrService.block(gameState.value.gameId)
  }

  async function announceHalfLife3() {
    if (!gameState.value || gameState.value.isRoundTransitionPaused) return
    if (mustSpendLevelUp.value) return
    await signalrService.announceHalfLife3(gameState.value.gameId)
  }

  async function wakeGordon() {
    if (!gameState.value || gameState.value.isRoundTransitionPaused) return
    await signalrService.wakeGordon(gameState.value.gameId)
  }

  async function resolveHalfLife3Decision(
    decisionSerial: number,
    choice: 'freeze' | 'postpone' | 'release',
  ) {
    if (!gameState.value) return
    await signalrService.resolveHalfLife3Decision(
      gameState.value.gameId,
      decisionSerial,
      choice,
    )
  }

  async function autoMove() {
    if (!gameState.value) return
    if (mustSpendLevelUp.value) return
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
    if (mustSpendLevelUp.value) return
    if (myPlayer.value?.character.name === 'Геральт') playGeraltMeditation()
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

  async function demandContractReward(demandType: 'previous' | 'next') {
    if (!gameState.value) return
    await signalrService.demandContractReward(gameState.value.gameId, demandType)
  }

  async function predict(targetPlayerId: string, characterName: string) {
    if (!gameState.value) return
    await signalrService.predict(gameState.value.gameId, targetPlayerId, characterName)
  }

  async function draftSelect(characterName: string) {
    if (!gameState.value) return
    await signalrService.draftSelect(gameState.value.gameId, characterName)
  }

  async function depthsCallChoice(agree: boolean) {
    if (!gameState.value) return
    await signalrService.depthsCallChoice(gameState.value.gameId, agree)
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

  async function doomRoll() {
    if (!gameState.value) return
    await signalrService.doomRoll(gameState.value.gameId)
  }

  async function doomChainsaw(passiveName: string) {
    if (!gameState.value) return
    await signalrService.doomChainsaw(gameState.value.gameId, passiveName)
  }

  async function deathNoteWrite(targetPlayerId: string, characterName: string) {
    if (!gameState.value) return
    await signalrService.deathNoteWrite(gameState.value.gameId, targetPlayerId, characterName)
  }

  async function shinigamiEyes() {
    if (!gameState.value) return
    await signalrService.shinigamiEyes(gameState.value.gameId)
  }

  async function rewriteHistory(roundNumber: number) {
    if (!gameState.value || rewritingHistoryRound.value !== null) return
    rewritingHistoryRound.value = roundNumber
    try {
      await signalrService.rewriteHistory(gameState.value.gameId, roundNumber)
    }
    finally {
      rewritingHistoryRound.value = null
    }
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
    isLoading.value = true
    errorMessage.value = null
    try {
      await signalrService.registerWebAccount(username)
      if (!isAuthenticated.value) {
        throw new Error(errorMessage.value ?? 'Web account creation failed. Try again.')
      }
    }
    catch (error) {
      if (!errorMessage.value) {
        errorMessage.value = error instanceof Error ? error.message : String(error)
      }
      throw error
    }
    finally {
      isLoading.value = false
    }
  }

  async function createWebGame() {
    await signalrService.createWebGame()
  }

  async function joinWebGame(gameId: number) {
    await signalrService.joinWebGame(gameId)
  }

  async function fetchCharacterList() {
    await signalrService.getCharacterList()
  }

  async function createTestGame(characterName: string) {
    await signalrService.createTestGame(characterName)
  }

  async function requestQuests() {
    if (isQuestsLoading.value) {
      questsRefreshPending = true
      return
    }
    isQuestsLoading.value = true
    questsError.value = null
    try {
      await signalrService.requestQuests()
    }
    catch (error) {
      const refreshAgain = questsRefreshPending
      questsRefreshPending = false
      isQuestsLoading.value = false
      questsError.value = error instanceof Error ? error.message : String(error)
      if (refreshAgain && isAuthenticated.value && isConnected.value) void requestQuests()
    }
  }

  async function rerollDailyQuest(questId: string) {
    if (!questId || rerollingQuestId.value) return
    rerollingQuestId.value = questId
    questsError.value = null
    try {
      await signalrService.rerollDailyQuest(questId)
    }
    catch (error) {
      questsError.value = error instanceof Error ? error.message : String(error)
    }
    finally {
      rerollingQuestId.value = null
    }
  }

  async function openLootBox() {
    lootBoxResult.value = null
    const waitForResult = new Promise<void>((resolve, reject) => {
      const startedAt = Date.now()
      const timer = window.setInterval(() => {
        if (lootBoxResult.value) {
          window.clearInterval(timer)
          resolve()
        }
        else if (Date.now() - startedAt >= 12_000) {
          window.clearInterval(timer)
          reject(new Error('The reward server took too long to respond. Retry this same opening or return to the lobby.'))
        }
      }, 50)
    })

    const invocation = signalrService.openLootBoxV2()
    await Promise.race([invocation, waitForResult])

    // SignalR normally delivers LootBoxOpened before the invocation completion,
    // but allow a short delivery window without leaving the opening screen stuck.
    if (!lootBoxResult.value) {
      await Promise.race([
        waitForResult,
        new Promise<never>((_, reject) => window.setTimeout(
          () => reject(new Error('The opening was accepted, but its reveal did not arrive. Retry safely to recover it.')),
          1_500,
        )),
      ])
    }
  }

  async function acknowledgeLootBox(openingId: string) {
    await signalrService.acknowledgeLootBox(openingId)
    await signalrService.requestQuests()
  }

  function clearLootBoxResult(openingId: string) {
    if (lootBoxResult.value?.openingId === openingId) {
      lootBoxResult.value = null
    }
  }

  function setLootBoxFlowActive(active: boolean) {
    isLootBoxFlowActive.value = active
  }

  async function requestAchievements() {
    isAchievementsLoading.value = true
    achievementsError.value = null
    try {
      await signalrService.requestAchievements()
    }
    catch (error) {
      isAchievementsLoading.value = false
      achievementsError.value = error instanceof Error ? error.message : String(error)
    }
  }

  async function clearNewAchievements() {
    await signalrService.clearNewAchievements()
    newlyUnlockedAchievements.value = []
  }

  async function requestDoomFortress() {
    await signalrService.requestDoomFortress()
  }

  async function requestStore() {
    if (isStoreLoading.value) return
    isStoreLoading.value = true
    storeError.value = null
    try {
      await signalrService.requestStore()
    }
    catch (error) {
      isStoreLoading.value = false
      storeError.value = error instanceof Error ? error.message : String(error)
    }
  }

  async function adjustStoreCharacter(characterName: string, percentagePoints: number) {
    if (storeAction.value) return
    const action = `adjust:${characterName}`
    storeAction.value = action
    storeError.value = null
    try {
      await signalrService.adjustStoreCharacter(characterName, percentagePoints)
    }
    catch (error) {
      storeError.value = error instanceof Error ? error.message : String(error)
    }
    finally {
      if (storeAction.value === action) storeAction.value = null
    }
  }

  async function resetStoreCharacter(characterName: string) {
    if (storeAction.value) return
    const action = `reset:${characterName}`
    storeAction.value = action
    storeError.value = null
    try {
      await signalrService.resetStoreCharacter(characterName)
    }
    catch (error) {
      storeError.value = error instanceof Error ? error.message : String(error)
    }
    finally {
      if (storeAction.value === action) storeAction.value = null
    }
  }

  async function resetStoreAllCharacters() {
    if (storeAction.value) return
    const action = 'reset:all'
    storeAction.value = action
    storeError.value = null
    try {
      await signalrService.resetStoreAllCharacters()
    }
    catch (error) {
      storeError.value = error instanceof Error ? error.message : String(error)
    }
    finally {
      if (storeAction.value === action) storeAction.value = null
    }
  }

  async function equipDoomModule(stage: string, slotIndex: number, moduleName: string) {
    await signalrService.equipDoomModule(stage, slotIndex, moduleName)
  }

  async function dismissAchievements() {
    if (isAcknowledgingAchievements.value) return
    const acknowledgedIds = newlyUnlockedAchievements.value.map(achievement => achievement.id)
    if (acknowledgedIds.length === 0) return

    isAcknowledgingAchievements.value = true
    achievementAcknowledgeError.value = null
    try {
      await signalrService.acknowledgeAchievements(acknowledgedIds)
      const acknowledged = new Set(acknowledgedIds)
      newlyUnlockedAchievements.value = newlyUnlockedAchievements.value.filter(
        achievement => !acknowledged.has(achievement.id),
      )
      if (achievementBoard.value) {
        achievementBoard.value = {
          ...achievementBoard.value,
          newlyUnlocked: achievementBoard.value.newlyUnlocked.filter(id => !acknowledged.has(id)),
        }
      }
      if (gameState.value) {
        achievementsDismissedForGame.value = String(gameState.value.gameId)
      }
    }
    catch (error) {
      achievementAcknowledgeError.value = error instanceof Error ? error.message : String(error)
    }
    finally {
      isAcknowledgingAchievements.value = false
    }
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
    rewritingHistoryRound,
    gameStory,
    blackjackState,
    questState,
    isQuestsLoading,
    questsError,
    rerollingQuestId,
    lootBoxResult,
    isLootBoxFlowActive,
    achievementBoard,
    newlyUnlockedAchievements,
    isAchievementsLoading,
    achievementsError,
    isAcknowledgingAchievements,
    achievementAcknowledgeError,
    storeState,
    isStoreLoading,
    storeAction,
    storeError,
    // Computed
    myPlayer,
    opponents,
    isMyTurn,
    mustSpendLevelUp,
    isLevelingUp,
    roundTimeLeft,
    isInGame,
    isAdmin,
    isLobbyAdmin,
    accountPlayerType,
    lastPlayedCharacter,
    characterList,
    doomFortressState,
    isKira,
    myPortalGun,
    isTerminalMode,
    isDeepSession,
    depthsCallPromptActive,
    myTerminalState,
    myPickleRick,
    myGiantBeans,
    canFireGunDuringPickle,
    // Actions
    connect,
    authenticate,
    logout,
    setLanguage,
    joinGame,
    leaveGame,
    refreshLobby,
    refreshGameState,
    attack,
    block,
    announceHalfLife3,
    wakeGordon,
    resolveHalfLife3Decision,
    autoMove,
    changeMind,
    confirmSkip,
    confirmPredict,
    levelUp,
    moralToPoints,
    moralToSkill,
    demandContractReward,
    predict,
    draftSelect,
    depthsCallChoice,
    aramReroll,
    aramConfirm,
    darksciChoice,
    youngGleb,
    doomRoll,
    doomChainsaw,
    deathNoteWrite,
    shinigamiEyes,
    rewriteHistory,
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
    fetchCharacterList,
    createTestGame,
    requestQuests,
    rerollDailyQuest,
    openLootBox,
    acknowledgeLootBox,
    clearLootBoxResult,
    setLootBoxFlowActive,
    requestAchievements,
    clearNewAchievements,
    dismissAchievements,
    requestStore,
    adjustStoreCharacter,
    resetStoreCharacter,
    resetStoreAllCharacters,
    requestDoomFortress,
    equipDoomModule,
    restoreWebSession,
  }
})
