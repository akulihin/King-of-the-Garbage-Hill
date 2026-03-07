import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import {
  signalrService,
  type BattleshipGameState,
  type BattleshipLobbyState,
  type BattleshipPlayerState,
  type BattleshipFleetSelection,
  type BattleshipShipCatalogEntry,
  type BattleshipEvent,
  type BattleshipShotResult,
  type BattleshipCell,
} from 'src/services/signalr'
import {
  playBattleshipShot,
  playBattleshipHit,
  playBattleshipMiss,
  playBattleshipShipSunk,
  playBattleshipWin,
  playBattleshipLose,
  playBattleshipDeploy,
  playBattleshipBurn,
  playBattleshipDodge,
  playBattleshipFreeze,
  playBattleshipStun,
} from 'src/services/sound'

export const useBattleshipStore = defineStore('battleship', () => {
  // -- State ------------------------------------------------------

  const gameState = ref<BattleshipGameState | null>(null)
  const lobbyState = ref<BattleshipLobbyState | null>(null)
  const shipCatalog = ref<BattleshipShipCatalogEntry[]>([])
  const lastShotResult = ref<BattleshipShotResult | null>(null)
  const errorMessage = ref<string | null>(null)
  const isCreating = ref(false)

  // Placement mode state
  const selectedShipId = ref<string | null>(null)
  const placementOrientation = ref<'Horizontal' | 'Vertical'>('Horizontal')

  // Combat state
  const selectedShotType = ref('Ballista')
  const shotDelayActive = ref(false)

  // Animation state
  const enemyAnimatedCells = ref(new Map<string, string>())
  const myAnimatedCells = ref(new Map<string, string>())

  // Last shot marker (which board + coordinates)
  const lastShotCell = ref<{ target: 'enemy' | 'my'; row: number; col: number } | null>(null)

  // Kill streak — consecutive deck-destroying hits
  const killStreak = ref(0)
  const killStreakDisplay = ref(0) // snapshot for UI display (resets after timeout)
  let killStreakTimer: ReturnType<typeof setTimeout> | null = null

  // Player-marked cells (right-click to mark/unmark)
  const markedCells = ref(new Set<string>()) // "row,col" on enemy board

  // Summon trails — cells summons have passed through
  const summonTrails = ref(new Map<string, Set<string>>()) // summonId -> set of "row,col"

  // Phase transition
  const previousPhase = ref<string | null>(null)
  const phaseTransitionActive = ref(false)

  // Screen shake on heavy impacts
  const screenShake = ref(false)

  // Cannonball projectile arc
  const projectileState = ref<{ row: number; col: number } | null>(null)

  // VFX toggle
  const vfxEnabled = ref(true)

  // -- Derived State ----------------------------------------------

  const phase = computed(() => gameState.value?.phase ?? 'Lobby')
  const isMyTurn = computed(() => gameState.value?.isMyTurn ?? false)
  const isFinished = computed(() => gameState.value?.isFinished ?? false)
  const gameId = computed(() => gameState.value?.gameId ?? null)
  const turnNumber = computed(() => gameState.value?.turnNumber ?? 0)
  const shotCount = computed(() => gameState.value?.shotCount ?? 0)
  const gameLog = computed(() => gameState.value?.gameLog ?? [])

  const myPlayer = computed<BattleshipPlayerState | null>(() => {
    if (!gameState.value) return null
    if (gameState.value.player1?.isMe) return gameState.value.player1
    if (gameState.value.player2?.isMe) return gameState.value.player2
    return null
  })

  const enemyPlayer = computed<BattleshipPlayerState | null>(() => {
    if (!gameState.value) return null
    if (gameState.value.player1?.isMe) return gameState.value.player2
    if (gameState.value.player2?.isMe) return gameState.value.player1
    // Spectator — player1 is "enemy" for display
    return gameState.value.player2
  })

  const myBoard = computed(() => myPlayer.value?.board ?? null)
  const enemyBoard = computed(() => enemyPlayer.value?.board ?? null)
  const myFleet = computed(() => myPlayer.value?.fleet ?? [])
  const coinsRemaining = computed(() => myPlayer.value?.coinsRemaining ?? 40)

  // -- Animation helpers ------------------------------------------

  function triggerCellAnim(target: 'enemy' | 'my', row: number, col: number, anim: string, durationMs = 600) {
    const map = target === 'enemy' ? enemyAnimatedCells : myAnimatedCells
    const key = `${row},${col}`
    map.value = new Map(map.value.set(key, anim))
    setTimeout(() => {
      const m = target === 'enemy' ? enemyAnimatedCells : myAnimatedCells
      const next = new Map(m.value)
      // Only delete if the animation hasn't been replaced
      if (next.get(key) === anim) next.delete(key)
      m.value = next
    }, durationMs)
  }

  function triggerShotAnim(result: BattleshipShotResult) {
    // If it's my turn, I'm shooting the enemy board; otherwise enemy shoots mine
    const target: 'enemy' | 'my' = isMyTurn.value ? 'enemy' : 'my'
    const { row, col } = result

    if (result.shipSunk) triggerCellAnim(target, row, col, 'anim-sunk', 800)
    else if (result.burned) triggerCellAnim(target, row, col, 'anim-burn-ignite', 700)
    else if (result.destroyed) triggerCellAnim(target, row, col, 'anim-destroy', 500)
    else if (result.scratched && result.miss) triggerCellAnim(target, row, col, 'anim-dodge', 500)
    else if (result.scratched) triggerCellAnim(target, row, col, 'anim-scratch', 500)
    else if (result.hit) triggerCellAnim(target, row, col, 'anim-hit', 400)
    else if (result.miss) triggerCellAnim(target, row, col, 'anim-miss', 400)
  }

  function diffBoardAnimations(
    oldCells: BattleshipCell[] | undefined,
    newCells: BattleshipCell[] | undefined,
    target: 'enemy' | 'my',
  ) {
    if (!oldCells || !newCells) return
    const map = target === 'enemy' ? enemyAnimatedCells : myAnimatedCells
    const oldMap = new Map(oldCells.map(c => [`${c.row},${c.col}`, c]))
    let freezeSoundPlayed = false
    for (const cell of newCells) {
      const key = `${cell.row},${cell.col}`
      if (map.value.has(key)) continue // already animating from shot result
      const old = oldMap.get(key)
      if (!old) continue

      // Detect newly changed states
      if (cell.isDestroyed && !old.isDestroyed) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-sunk', 800)
      } else if ((cell.isBurning || cell.isFirePermanent) && !old.isBurning && !old.isFirePermanent) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-burn-ignite', 600)
      } else if (cell.isFrozen && !old.isFrozen) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-freeze', 600)
        if (!freezeSoundPlayed) { playBattleshipFreeze(); freezeSoundPlayed = true }
      } else if (cell.isDevastated && !old.isDevastated) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-devastate', 600)
      } else if (cell.isCaptured && !old.isCaptured) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-capture', 600)
      } else if (cell.isRevealed && !old.isRevealed) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-reveal', 400)
      }
    }
  }

  // -- SignalR Callbacks ------------------------------------------

  function initCallbacks() {
    signalrService.onBattleshipState = (state) => {
      // Snapshot old board cells before updating state (for diff animations)
      const oldEnemyCells = enemyPlayer.value?.board?.cells
      const oldMyCells = myPlayer.value?.board?.cells

      const wasFinished = gameState.value?.isFinished ?? false
      const oldPhase = gameState.value?.phase ?? null

      // Track summon positions for trail visualization
      const allSummons = [
        ...(myPlayer.value?.summons ?? []),
        ...(enemyPlayer.value?.summons ?? []),
      ]
      for (const s of allSummons) {
        if (!s.isAlive) continue
        const key = s.id
        if (!summonTrails.value.has(key)) summonTrails.value.set(key, new Set())
        summonTrails.value.get(key)!.add(`${s.row},${s.col}`)
      }

      gameState.value = state

      // Phase transition detection
      if (oldPhase && state.phase !== oldPhase) {
        previousPhase.value = oldPhase
        phaseTransitionActive.value = true
        setTimeout(() => { phaseTransitionActive.value = false }, 1200)
      }
      if (state.shipCatalog) {
        shipCatalog.value = state.shipCatalog
      }

      // Diff boards for multi-cell animations (sunk ship cells, burn spread, freeze, etc.)
      const newEnemyCells = enemyPlayer.value?.board?.cells
      const newMyCells = myPlayer.value?.board?.cells
      diffBoardAnimations(oldEnemyCells, newEnemyCells, 'enemy')
      diffBoardAnimations(oldMyCells, newMyCells, 'my')

      // Play win/lose sound on game over transition
      if (state.isFinished && !wasFinished) {
        const me = state.player1?.isMe ? state.player1 : state.player2
        if (me && state.winnerId === me.discordId) playBattleshipWin()
        else playBattleshipLose()
      }
    }

    signalrService.onBattleshipLobby = (state) => {
      lobbyState.value = state
    }

    signalrService.onBattleshipGameCreated = (_data) => {
      isCreating.value = false
    }

    signalrService.onBattleshipEvent = (event: BattleshipEvent) => {
      if (event.eventType === 'ShotResult') {
        const result = event.data as BattleshipShotResult
        lastShotResult.value = result

        // Track last shot position
        const shotTarget: 'enemy' | 'my' = isMyTurn.value ? 'enemy' : 'my'
        lastShotCell.value = { target: shotTarget, row: result.row, col: result.col }

        // Kill streak tracking
        if (result.destroyed || result.shipSunk || result.burned) {
          killStreak.value++
          killStreakDisplay.value = killStreak.value
          if (killStreakTimer) clearTimeout(killStreakTimer)
          killStreakTimer = setTimeout(() => { killStreakDisplay.value = 0 }, 3000)
        } else if (result.miss || (result.scratched && result.miss)) {
          killStreak.value = 0
        }

        // Sound + animation helper
        const fireShotEffects = () => {
          triggerShotAnim(result)
          if (result.shipSunk) playBattleshipShipSunk()
          else if (result.burned) playBattleshipBurn()
          else if (result.scratched && result.miss) playBattleshipDodge()
          else if (result.hit) playBattleshipHit()
          else if (result.miss) playBattleshipMiss()
          else playBattleshipShot()
          // Screen shake on heavy impacts
          if (result.shipSunk || result.burned || result.destroyed) {
            screenShake.value = true
            setTimeout(() => { screenShake.value = false }, 150)
          }
        }

        if (isMyTurn.value) {
          // Cannonball arc then impact
          projectileState.value = { row: result.row, col: result.col }
          setTimeout(() => {
            projectileState.value = null
            fireShotEffects()
          }, 250)
        } else {
          fireShotEffects()
        }

        // On hit that continues turn, add 2s delay before allowing next shot
        if (result.hit && result.turnContinues) {
          shotDelayActive.value = true
          setTimeout(() => { shotDelayActive.value = false }, 2000)
        }
      }
    }

    signalrService.onShipCatalog = (catalog) => {
      shipCatalog.value = catalog
    }

    signalrService.onError = (error) => {
      errorMessage.value = error
      setTimeout(() => { errorMessage.value = null }, 4000)
    }
  }

  function cleanupCallbacks() {
    signalrService.onBattleshipState = null
    signalrService.onBattleshipLobby = null
    signalrService.onBattleshipGameCreated = null
    signalrService.onBattleshipEvent = null
    signalrService.onShipCatalog = null
  }

  // -- Actions ----------------------------------------------------

  async function refreshLobby() {
    await signalrService.requestBattleshipLobby()
  }

  async function createGame() {
    isCreating.value = true
    await signalrService.createBattleshipGame()
  }

  async function joinWebGame(id: string) {
    await signalrService.joinBattleshipWebGame(id)
  }

  async function leaveWebGame(id: string) {
    await signalrService.leaveBattleshipWebGame(id)
  }

  async function joinGame(id: string) {
    await signalrService.joinBattleshipGame(id)
  }

  async function leaveGame(id: string) {
    await signalrService.leaveBattleshipGame(id)
  }

  async function confirmReady() {
    if (!gameId.value) return
    await signalrService.battleshipConfirmReady(gameId.value)
  }

  async function selectArmy(faction: string) {
    if (!gameId.value) return
    await signalrService.battleshipSelectArmy(gameId.value, faction)
  }

  async function selectFleet(selections: BattleshipFleetSelection[]) {
    if (!gameId.value) return
    await signalrService.battleshipSelectFleet(gameId.value, selections)
  }

  async function placeShip(shipId: string, row: number, col: number, orientation: string) {
    if (!gameId.value) return
    await signalrService.battleshipPlaceShip(gameId.value, shipId, row, col, orientation)
  }

  async function removeShip(shipId: string) {
    if (!gameId.value) return
    await signalrService.battleshipRemoveShip(gameId.value, shipId)
  }

  async function confirmPlacement() {
    if (!gameId.value) return
    await signalrService.battleshipConfirmPlacement(gameId.value)
  }

  async function shoot(row: number, col: number) {
    if (!gameId.value || shotDelayActive.value) return
    await signalrService.battleshipShoot(gameId.value, row, col)
  }

  async function shootOwnBoard(row: number, col: number) {
    if (!gameId.value || shotDelayActive.value) return
    await signalrService.battleshipShootOwnBoard(gameId.value, row, col)
  }

  // Map weapon types to their actual shot behavior (must match backend WeaponTypeToShotType)
  function weaponToShotType(weaponType: string): string {
    switch (weaponType) {
      case 'Catapult': return 'Buckshot'
      case 'Tetracatapult': return 'WhiteStone'
      case 'Incendiary': return 'Incendiary'
      case 'GreekFire': return 'GreekFire'
      default: return 'Ballista'
    }
  }

  async function selectWeapon(weaponType: string, shotType: string) {
    if (!gameId.value) return
    // Tetracatapult can fire as WhiteStone or Buckshot — use client-sent shotType
    selectedShotType.value = weaponType === 'Tetracatapult' ? shotType : weaponToShotType(weaponType)
    await signalrService.battleshipSelectWeapon(gameId.value, weaponType, shotType)
  }

  async function deploySummon(summonType: string, col: number) {
    if (!gameId.value) return
    playBattleshipDeploy()
    await signalrService.battleshipDeploySummon(gameId.value, summonType, col)
  }

  async function deployPendingSummon(pendingId: string, col: number) {
    if (!gameId.value) return
    playBattleshipDeploy()
    await signalrService.battleshipDeployPendingSummon(gameId.value, pendingId, col)
  }

  async function manualMove(shipId: string, direction: string, distance: number = 1) {
    if (!gameId.value) return
    await signalrService.battleshipManualMove(gameId.value, shipId, direction, distance)
  }

  async function setCursedBoatDirection(summonId: string, direction: string) {
    if (!gameId.value) return
    await signalrService.battleshipSetCursedBoatDirection(gameId.value, summonId, direction)
  }

  async function forfeit() {
    if (!gameId.value) return
    await signalrService.battleshipForfeit(gameId.value)
  }

  async function requestState() {
    if (!gameId.value) return
    await signalrService.requestBattleshipState(gameId.value)
  }

  async function requestCatalog() {
    await signalrService.requestShipCatalog()
  }

  function toggleOrientation() {
    placementOrientation.value = placementOrientation.value === 'Horizontal' ? 'Vertical' : 'Horizontal'
  }

  function toggleMarkedCell(row: number, col: number) {
    const key = `${row},${col}`
    const next = new Set(markedCells.value)
    if (next.has(key)) next.delete(key)
    else next.add(key)
    markedCells.value = next
  }

  function clearMarkedCells() {
    markedCells.value = new Set()
  }

  // Compute summon trail cells for a given board target
  function getSummonTrailCells(target: 'enemy' | 'my'): Map<string, string> {
    const result = new Map<string, string>()
    const summons = target === 'enemy'
      ? (enemyPlayer.value?.summons ?? [])
      : (myPlayer.value?.summons ?? [])
    for (const s of summons) {
      const trail = summonTrails.value.get(s.id)
      if (trail) {
        for (const pos of trail) result.set(pos, s.type ?? 'PirateBoat')
      }
    }
    return result
  }

  return {
    // State
    gameState,
    lobbyState,
    shipCatalog,
    lastShotResult,
    errorMessage,
    isCreating,
    selectedShipId,
    placementOrientation,
    selectedShotType,
    shotDelayActive,
    enemyAnimatedCells,
    myAnimatedCells,
    lastShotCell,
    killStreak,
    killStreakDisplay,
    markedCells,
    summonTrails,
    previousPhase,
    phaseTransitionActive,
    screenShake,
    projectileState,
    vfxEnabled,

    // Computed
    phase,
    isMyTurn,
    isFinished,
    gameId,
    turnNumber,
    shotCount,
    gameLog,
    myPlayer,
    enemyPlayer,
    myBoard,
    enemyBoard,
    myFleet,
    coinsRemaining,

    // Actions
    initCallbacks,
    cleanupCallbacks,
    refreshLobby,
    createGame,
    joinWebGame,
    leaveWebGame,
    joinGame,
    leaveGame,
    confirmReady,
    selectArmy,
    selectFleet,
    placeShip,
    removeShip,
    confirmPlacement,
    shoot,
    shootOwnBoard,
    forfeit,
    selectWeapon,
    deploySummon,
    deployPendingSummon,
    manualMove,
    setCursedBoatDirection,
    requestState,
    requestCatalog,
    toggleOrientation,
    toggleMarkedCell,
    clearMarkedCells,
    getSummonTrailCells,
  }
})
