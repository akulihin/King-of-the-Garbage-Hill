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
  type BattleshipStats,
  type BattleshipCell,
} from 'src/services/signalr'
import {
  playBattleshipShot,
  playBattleshipHit,
  playBattleshipMiss,
  playBattleshipShipSunk,
  playBattleshipDeploy,
  playBattleshipBurn,
  playBattleshipDodge,
  playBattleshipFreeze,
  playBattleshipExplode,
  playBattleshipPhaseChange,
  playBattleshipTurnStart,
  playBattleshipWeaponSelect,
} from 'src/services/sound'

/** Weapon entry shown in the weapon bar / consumed by keyboard shortcuts. */
export interface BattleshipWeaponOption {
  id: string
  shipId: string
  type: string
  shotType: string
  label: string
  ammo: number
  hasAmmo: boolean
  shipName: string
  shipRange: string
  shipRow: number
  aimSpeed: number
  deckIndex: number
}

export interface BattleshipSummonDeployMode {
  type: string
  pendingId?: string
  pendingCols?: number[]
  reentryDirection?: string
  reentryRow?: number
  reentryCol?: number
}

/** VFX impact vocabulary (mirrors useVfx ImpactType). */
export type BattleshipImpactType = 'hit' | 'miss' | 'burn' | 'sunk' | 'destroy' | 'scratch' | 'freeze'

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

  // Combat state (shared by the page shell's keyboard handler and the phases)
  const selectedShotType = ref('Ballista')
  const selectedWeaponType = ref('Ballista')
  const shotDelayActive = ref(false)
  const summonDeployMode = ref<BattleshipSummonDeployMode | null>(null)
  const summonType = ref('Ram')

  // Animation state
  const enemyAnimatedCells = ref(new Map<string, string>())
  const myAnimatedCells = ref(new Map<string, string>())

  // Last shot marker (which board + coordinates)
  const lastShotCell = ref<{ target: 'enemy' | 'my'; row: number; col: number } | null>(null)
  let pendingShotTarget: 'enemy' | 'my' | null = null

  // Kill streak — consecutive deck-destroying hits
  const killStreak = ref(0)
  const killStreakDisplay = ref(0) // snapshot for UI display (resets after timeout)
  let killStreakTimer: ReturnType<typeof setTimeout> | null = null

  // Player-marked cells (right-click to mark/unmark)
  const markedCells = ref(new Set<string>()) // "row,col" on enemy board

  // Phase transition
  const previousPhase = ref<string | null>(null)
  const phaseTransitionActive = ref(false)

  // Screen shake on heavy impacts
  const screenShake = ref(false)

  // VFX toggle
  const vfxEnabled = ref(true)

  // Client-side match stats (enemy fleet is hidden in player DTOs, so the
  // victory screen tracks the player's own performance from ShotResult events)
  const myShotsFired = ref(0)
  const myShotsHit = ref(0)
  const myShipsSunk = ref(0)

  // Persistent meta (W/L record, streak, first-win bonus) for the lobby panel
  const statsState = ref<BattleshipStats | null>(null)
  const isStatsLoading = ref(false)
  let statsRefreshPending = false

  // -- VFX handshake ----------------------------------------------
  // CombatPhase registers a handler that launches the projectile and calls
  // `fire()` on impact. Returning false (or having no handler) means the
  // caller fires the effects immediately — the fallback for reduced motion,
  // the opponent's shots, or an unmounted canvas.

  type ShotVfxHandler = (row: number, col: number, target: 'enemy' | 'my', fire: () => void) => boolean
  type CellVfxHandler = (target: 'enemy' | 'my', row: number, col: number, type: BattleshipImpactType) => void

  let shotVfxHandler: ShotVfxHandler | null = null
  let cellVfxHandler: CellVfxHandler | null = null

  function setShotVfxHandler(handler: ShotVfxHandler | null) {
    shotVfxHandler = handler
  }

  function setCellVfxHandler(handler: CellVfxHandler | null) {
    cellVfxHandler = handler
  }

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

  const isWinner = computed(() =>
    !!gameState.value?.winnerId && gameState.value.winnerId === myPlayer.value?.discordId)

  const myEndReward = computed(() => gameState.value?.myEndReward ?? null)

  const availableWeapons = computed<BattleshipWeaponOption[]>(() => {
    if (!myPlayer.value) return []
    const weapons: BattleshipWeaponOption[] = []
    for (const w of myPlayer.value.availableWeapons ?? []) {
      const ship = myFleet.value.find(s => s.id === w.shipId)
      const base = {
        id: w.id, shipId: w.shipId, type: w.type, ammo: w.ammo, hasAmmo: true,
        shipName: w.shipName, shipRange: ship?.range ?? 'Close', shipRow: ship?.row ?? 0,
        aimSpeed: w.aimRemaining, deckIndex: w.deckIndex,
      }
      if (w.type === 'Tetracatapult') {
        weapons.push({ ...base, shotType: 'WhiteStone', label: 'Белый камень' })
        weapons.push({ ...base, shotType: 'Buckshot', label: 'Дробь' })
      } else {
        const label = w.type === 'Incendiary' ? 'Горючка'
          : w.type === 'GreekFire' ? 'Греческий огонь'
          : w.type === 'Ballista' ? 'Баллиста' : w.type
        weapons.push({ ...base, shotType: w.type, label })
      }
    }
    return weapons
  })

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

  function triggerShotAnim(result: BattleshipShotResult, target: 'enemy' | 'my') {
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
    let explodeSoundPlayed = false
    for (const cell of newCells) {
      const key = `${cell.row},${cell.col}`
      if (map.value.has(key)) continue // already animating from shot result
      const old = oldMap.get(key)
      if (!old) continue

      // Detect newly changed states
      if (cell.isShipSunk && !old.isShipSunk) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-sunk', 800)
      } else if (cell.isDestroyed && !old.isDestroyed) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-destroy', 500)
      } else if ((cell.isBurning || cell.isFirePermanent) && !old.isBurning && !old.isFirePermanent) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-burn-ignite', 600)
      } else if (cell.isFrozen && !old.isFrozen) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-freeze', 600)
        cellVfxHandler?.(target, cell.row, cell.col, 'freeze')
        if (!freezeSoundPlayed) { playBattleshipFreeze(); freezeSoundPlayed = true }
      } else if (cell.isDevastated && !old.isDevastated) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-devastate', 600)
        if (!explodeSoundPlayed) { playBattleshipExplode(); explodeSoundPlayed = true }
      } else if (cell.isCaptured && !old.isCaptured) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-capture', 600)
      } else if (cell.isRevealed && !old.isRevealed) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-reveal', 400)
      }
    }
  }

  function newLogEntries(oldLogs: string[], newLogs: string[]): string[] {
    const maxOverlap = Math.min(oldLogs.length, newLogs.length)
    for (let overlap = maxOverlap; overlap > 0; overlap--) {
      const oldTail = oldLogs.slice(oldLogs.length - overlap)
      const newHead = newLogs.slice(0, overlap)
      if (oldTail.every((entry, index) => entry === newHead[index])) {
        return newLogs.slice(overlap)
      }
    }
    return newLogs
  }

  /** Only a logged detonation explodes; freeze/collision remove Brander silently. */
  function detectBranderDetonation(oldState: BattleshipGameState | null, newState: BattleshipGameState) {
    if (!oldState) return
    const oldSummons = [
      ...(oldState.player1?.summons ?? []),
      ...(oldState.player2?.summons ?? []),
    ].filter(s => s.type === 'Brander' && s.isAlive)
    if (oldSummons.length === 0) return
    const stillAlive = new Set(
      [...(newState.player1?.summons ?? []), ...(newState.player2?.summons ?? [])]
        .filter(s => s.isAlive)
        .map(s => s.id),
    )
    const branderDisappeared = oldSummons.some(s => !stillAlive.has(s.id))
    const detonationLogged = newLogEntries(oldState.gameLog, newState.gameLog)
      .some(entry => entry.includes('Брандер взорвался!'))
    if (branderDisappeared && detonationLogged) playBattleshipExplode()
  }

  // -- SignalR Callbacks ------------------------------------------

  function initCallbacks() {
    signalrService.onBattleshipState = (state) => {
      // Snapshot old board cells before updating state (for diff animations)
      const oldEnemyCells = enemyPlayer.value?.board?.cells
      const oldMyCells = myPlayer.value?.board?.cells

      const oldState = gameState.value
      const oldPhase = oldState?.phase ?? null
      const wasMyTurn = oldState?.isMyTurn ?? false

      // New game → reset per-match client-side stats
      if (oldState?.gameId !== state.gameId) {
        myShotsFired.value = 0
        myShotsHit.value = 0
        myShipsSunk.value = 0
        killStreak.value = 0
        killStreakDisplay.value = 0
        lastShotResult.value = null
        lastShotCell.value = null
        markedCells.value = new Set()
      }

      detectBranderDetonation(oldState, state)

      gameState.value = state

      // Sync selectedShotType from server (auto-reset after WhiteStone/Buckshot)
      const me = state.player1?.isMe ? state.player1 : state.player2
      if (me?.selectedShotType) {
        selectedShotType.value = me.selectedShotType
        selectedWeaponType.value = me.selectedShotType === 'WhiteStone' || me.selectedShotType === 'Buckshot'
          ? 'Tetracatapult'
          : me.selectedShotType
      }

      // Phase transition detection
      if (oldPhase && state.phase !== oldPhase) {
        previousPhase.value = oldPhase
        phaseTransitionActive.value = true
        playBattleshipPhaseChange()
        setTimeout(() => { phaseTransitionActive.value = false }, 1200)
      }
      if (state.shipCatalog) {
        shipCatalog.value = state.shipCatalog
      }

      // Turn start — my turn just began mid-combat
      if (!wasMyTurn && state.isMyTurn && (state.phase === 'Combat' || state.phase === 'Boarding')) {
        playBattleshipTurnStart()
      }

      // Diff boards for multi-cell animations (sunk ship cells, burn spread, freeze, etc.)
      const newEnemyCells = enemyPlayer.value?.board?.cells
      const newMyCells = myPlayer.value?.board?.cells
      diffBoardAnimations(oldEnemyCells, newEnemyCells, 'enemy')
      diffBoardAnimations(oldMyCells, newMyCells, 'my')
      // Win/lose sounds play from the GameOverCelebration modal on mount.
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
        if (result.wasSkipped) {
          lastShotResult.value = null
          return
        }
        lastShotResult.value = result

        // Track last shot position
        const shotTarget: 'enemy' | 'my' = result.targetPlayerId
          ? (result.targetPlayerId === gameState.value?.myPlayerId ? 'my' : 'enemy')
          : (isMyTurn.value ? (pendingShotTarget ?? 'enemy') : 'my')
        pendingShotTarget = null
        lastShotCell.value = { target: shotTarget, row: result.row, col: result.col }

        // Client-side match stats (my shots only)
        if (isMyTurn.value) {
          myShotsFired.value++
          if (result.hit && !result.miss) myShotsHit.value++
          if (result.shipSunk) myShipsSunk.value++
        }

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
          triggerShotAnim(result, shotTarget)
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

        // A mounted combat view animates both sides. Hidden enemy source coordinates stay
        // private; the handler launches those projectiles from outside the visible board.
        const handled = vfxEnabled.value
          && (shotVfxHandler?.(result.row, result.col, shotTarget, fireShotEffects) ?? false)
        if (!handled) fireShotEffects()

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

    signalrService.onBattleshipStats = (stats) => {
      statsState.value = stats
      const refreshAgain = statsRefreshPending
      statsRefreshPending = false
      isStatsLoading.value = false
      if (refreshAgain) void loadStats()
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
    signalrService.onBattleshipStats = null
  }

  // -- Actions ----------------------------------------------------

  async function refreshLobby() {
    await signalrService.requestBattleshipLobby()
  }

  async function loadStats() {
    if (isStatsLoading.value) {
      statsRefreshPending = true
      return
    }
    isStatsLoading.value = true
    try {
      await signalrService.requestBattleshipStats()
    }
    catch {
      const refreshAgain = statsRefreshPending
      statsRefreshPending = false
      isStatsLoading.value = false
      if (refreshAgain) void loadStats()
    }
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
    pendingShotTarget = 'enemy'
    await signalrService.battleshipShoot(gameId.value, row, col)
  }

  async function shootOwnBoard(row: number, col: number) {
    if (!gameId.value || shotDelayActive.value) return
    pendingShotTarget = 'my'
    await signalrService.battleshipShootOwnBoard(gameId.value, row, col)
  }

  // Map weapon types to their actual shot behavior (must match backend WeaponTypeToShotType)
  function weaponToShotType(weaponType: string): string {
    switch (weaponType) {
      case 'Tetracatapult': return 'WhiteStone'
      case 'Incendiary': return 'Incendiary'
      case 'GreekFire': return 'GreekFire'
      default: return 'Ballista'
    }
  }

  async function selectWeapon(weaponType: string, shotType: string, weaponId: string) {
    if (!gameId.value) return
    selectedWeaponType.value = weaponType
    summonDeployMode.value = null
    playBattleshipWeaponSelect()
    // Tetracatapult can fire as WhiteStone or Buckshot — use client-sent shotType
    selectedShotType.value = weaponType === 'Tetracatapult' ? shotType : weaponToShotType(weaponType)
    await signalrService.battleshipSelectWeapon(gameId.value, weaponType, shotType, weaponId)
  }

  async function passBoardingTurn() {
    if (!gameId.value) return
    await signalrService.battleshipPassBoardingTurn(gameId.value)
  }

  async function deploySummon(summonTypeName: string, col: number) {
    if (!gameId.value) return
    playBattleshipDeploy()
    await signalrService.battleshipDeploySummon(gameId.value, summonTypeName, col)
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

  function cancelSummonDeploy() {
    summonDeployMode.value = null
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
    selectedWeaponType,
    shotDelayActive,
    summonDeployMode,
    summonType,
    enemyAnimatedCells,
    myAnimatedCells,
    lastShotCell,
    killStreak,
    killStreakDisplay,
    markedCells,
    previousPhase,
    phaseTransitionActive,
    screenShake,
    vfxEnabled,
    myShotsFired,
    myShotsHit,
    myShipsSunk,
    statsState,
    isStatsLoading,

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
    isWinner,
    myEndReward,
    availableWeapons,

    // Actions
    initCallbacks,
    cleanupCallbacks,
    setShotVfxHandler,
    setCellVfxHandler,
    refreshLobby,
    loadStats,
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
    passBoardingTurn,
    deploySummon,
    deployPendingSummon,
    manualMove,
    setCursedBoatDirection,
    requestState,
    requestCatalog,
    toggleOrientation,
    cancelSummonDeploy,
    toggleMarkedCell,
    clearMarkedCells,
  }
})
