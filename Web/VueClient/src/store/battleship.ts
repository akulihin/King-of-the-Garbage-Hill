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
  type BattleshipBotVersion,
  type BattleshipOrientation,
  type BattleshipWeaponLoadout,
  type BattleshipSummon,
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
  playComboStack,
} from 'src/services/sound'
import { message } from 'src/platform/localization'

/** Weapon entry shown in the weapon bar / consumed by keyboard shortcuts. */
export interface BattleshipWeaponOption {
  id: string
  shipId: string
  type: string
  shotType: string
  label: string
  ammo: number
  maxAmmo: number
  hasAmmo: boolean
  isShared: boolean
  sources: string[]
  shipName: string
  shipRange: string
  shipRow: number
  aimSpeed: number
  deckIndex: number
}

export interface BattleshipSummonDeployMode {
  type: string
  summonId?: string
  displayName?: string
  pendingId?: string
  pendingCols?: number[]
  reentryDirection?: string
  reentryRow?: number
  reentryCol?: number
}

/** VFX impact vocabulary (mirrors useVfx ImpactType). */
export type BattleshipImpactType = 'hit' | 'miss' | 'burn' | 'sunk' | 'destroy' | 'scratch' | 'freeze'

export interface BattleshipTurnSkipNotice {
  id: number
  skippedPlayerId: string | null
  reason: 'Penalty' | 'Stun' | 'Unknown'
}

export const useBattleshipStore = defineStore('battleship', () => {
  // -- State ------------------------------------------------------

  const gameState = ref<BattleshipGameState | null>(null)
  const lobbyState = ref<BattleshipLobbyState | null>(null)
  const shipCatalog = ref<BattleshipShipCatalogEntry[]>([])
  const lastShotResult = ref<BattleshipShotResult | null>(null)
  const errorMessage = ref<string | null>(null)
  let errorMessageTimer: ReturnType<typeof setTimeout> | null = null
  const isCreating = ref(false)

  // Placement mode state
  const selectedShipId = ref<string | null>(null)
  const placementOrientation = ref<BattleshipOrientation>('Horizontal')
  const flintPlacementHoverCell = ref<{ row: number; col: number } | null>(null)

  // Combat state (shared by the page shell's keyboard handler and the phases)
  const selectedShotType = ref('Ballista')
  const selectedWeaponType = ref('Ballista')
  const shotDelayActive = ref(false)
  const shotDelayInitialRemainingMs = ref(0)
  const shotDelayDurationMs = ref(0)
  const shotDelayOwnerId = ref<string | null>(null)
  let shotDelayTimeout: ReturnType<typeof setTimeout> | null = null
  const summonDeployMode = ref<BattleshipSummonDeployMode | null>(null)
  const summonType = ref('Ram')

  // Animation state
  const enemyAnimatedCells = ref(new Map<string, string>())
  const myAnimatedCells = ref(new Map<string, string>())
  const parrotTransitionActive = ref(false)
  const parrotStatusSnapshot = ref<BattleshipSummon | null>(null)
  let shotImpactPending = false
  let shotImpactDurationMs = 0
  let parrotTransitionTimer: ReturnType<typeof setTimeout> | null = null
  let parrotImpactFallbackTimer: ReturnType<typeof setTimeout> | null = null
  let pendingTurnStartSound = false
  const pendingParrotAnimations: Array<{
    target: 'enemy' | 'my'
    row: number
    col: number
    anim: string
    durationMs: number
  }> = []

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

  const turnSkipNotice = ref<BattleshipTurnSkipNotice | null>(null)
  const pendingTurnSkipNotices: BattleshipTurnSkipNotice[] = []
  let turnSkipNoticeTimer: ReturnType<typeof setTimeout> | null = null
  let turnSkipNoticeSerial = 0
  const penaltyFeedbackId = ref(0)
  let penaltyFeedbackTimer: ReturnType<typeof setTimeout> | null = null

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

  function activateShotDelay(remainingMs: number, durationMs = remainingMs, ownerId: string | null = null) {
    if (shotDelayTimeout) clearTimeout(shotDelayTimeout)
    if (remainingMs <= 0) {
      shotDelayActive.value = false
      shotDelayInitialRemainingMs.value = 0
      shotDelayDurationMs.value = 0
      shotDelayOwnerId.value = null
      shotDelayTimeout = null
      return
    }
    shotDelayActive.value = true
    shotDelayInitialRemainingMs.value = remainingMs
    shotDelayDurationMs.value = Math.max(remainingMs, durationMs)
    shotDelayOwnerId.value = ownerId
    shotDelayTimeout = setTimeout(() => {
      shotDelayActive.value = false
      shotDelayInitialRemainingMs.value = 0
      shotDelayDurationMs.value = 0
      shotDelayOwnerId.value = null
      shotDelayTimeout = null
    }, remainingMs)
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

  const displayedMySummons = computed<BattleshipSummon[]>(() => {
    const summons = myPlayer.value?.summons ?? []
    const snapshot = parrotStatusSnapshot.value
    if (!parrotTransitionActive.value || !snapshot) return summons
    return [...summons.filter(summon => summon.id !== snapshot.id), snapshot]
  })

  const availableWeapons = computed<BattleshipWeaponOption[]>(() => {
    if (!myPlayer.value) return []
    const weapons: BattleshipWeaponOption[] = []
    for (const w of myPlayer.value.availableWeapons ?? []) {
      const ship = myFleet.value.find(s => s.id === w.shipId)
      const base = {
        id: w.id, shipId: w.shipId, type: w.type, ammo: w.ammo, hasAmmo: w.ammo !== 0,
        maxAmmo: w.maxAmmo, isShared: w.isShared, sources: w.sources ?? [w.shipName],
        shipName: w.shipName, shipRange: ship?.range ?? 'Close', shipRow: ship?.row ?? 0,
        aimSpeed: w.aimRemaining, deckIndex: w.deckIndex,
      }
      const shotType = w.shotType || weaponToShotType(w.type)
      const label = shotType === 'WhiteStone' ? 'Белый камень'
        : shotType === 'Buckshot' ? 'Дробь'
          : shotType === 'Neptune' ? message('battleship.weapon.neptune.name')
          : shotType === 'Incendiary' ? 'Горючка'
            : shotType === 'EvilIncendiary' ? 'Злая горючка'
              : shotType === 'GreekFire' ? 'Греческий огонь'
                : shotType === 'EvilGreekFire' ? 'Злой Греческий огонь'
                  : shotType === 'Cannon' ? message('battleship.weapon.cannon.name')
                    : shotType === 'Fortuna' ? message('battleship.weapon.fortuna.name')
                      : shotType === 'Warming' ? message('battleship.weapon.warming.name')
                        : shotType === 'Ballista' ? 'Баллиста' : shotType
      weapons.push({ ...base, shotType, label })
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

  function triggerParrotAnim(
    target: 'enemy' | 'my',
    row: number,
    col: number,
    anim: string,
    durationMs: number,
  ) {
    if (shotImpactPending) {
      parrotTransitionActive.value = true
      pendingParrotAnimations.push({ target, row, col, anim, durationMs })
      if (anim !== 'anim-parrot-death') {
        const map = target === 'enemy' ? enemyAnimatedCells : myAnimatedCells
        map.value = new Map(map.value.set(`${row},${col}`, 'anim-parrot-pending'))
      }
      if (!parrotImpactFallbackTimer) {
        parrotImpactFallbackTimer = setTimeout(() => {
          parrotImpactFallbackTimer = null
          shotImpactPending = false
          flushParrotAnimations()
        }, 4500)
      }
      return
    }
    triggerCellAnim(target, row, col, anim, durationMs)
    scheduleParrotTransitionEnd(durationMs)
  }

  function scheduleParrotTransitionEnd(durationMs: number) {
    if (parrotTransitionTimer) clearTimeout(parrotTransitionTimer)
    parrotTransitionActive.value = true
    parrotTransitionTimer = setTimeout(() => {
      parrotTransitionTimer = null
      parrotTransitionActive.value = false
      parrotStatusSnapshot.value = null
      if (pendingTurnStartSound) {
        pendingTurnStartSound = false
        playBattleshipTurnStart()
      }
    }, durationMs)
  }

  function flushParrotAnimations() {
    if (parrotImpactFallbackTimer) clearTimeout(parrotImpactFallbackTimer)
    parrotImpactFallbackTimer = null
    for (const map of [enemyAnimatedCells, myAnimatedCells]) {
      map.value = new Map([...map.value].filter(([, anim]) =>
        !anim.startsWith('anim-parrot-flight-')))
    }
    const animations = pendingParrotAnimations.splice(0)
    for (const animation of animations) {
      triggerCellAnim(
        animation.target,
        animation.row,
        animation.col,
        animation.anim,
        animation.durationMs,
      )
    }
    if (animations.length > 0)
      scheduleParrotTransitionEnd(Math.max(...animations.map(value => value.durationMs)))
  }

  function triggerShotAnim(result: BattleshipShotResult, target: 'enemy' | 'my') {
    const { row, col } = result

    if (result.shipSunk) triggerCellAnim(target, row, col, 'anim-sunk', 800)
    else if (result.burned) triggerCellAnim(target, row, col, 'anim-burn-ignite', 700)
    else if (result.destroyed) triggerCellAnim(target, row, col, 'anim-destroy', 500)
    else if (result.dodged) triggerCellAnim(target, row, col, 'anim-dodge', 500)
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
    const oldParrot = oldCells.find(cell => cell.hasSummon && cell.summonType === 'Parrot')
    const newParrot = newCells.find(cell => cell.hasSummon && cell.summonType === 'Parrot')
    if (oldParrot && newParrot
      && (oldParrot.row !== newParrot.row || oldParrot.col !== newParrot.col)) {
      const rowDelta = newParrot.row - oldParrot.row
      const colDelta = newParrot.col - oldParrot.col
      const direction = Math.abs(colDelta) >= Math.abs(rowDelta)
        ? (colDelta >= 0 ? 'right' : 'left')
        : (rowDelta >= 0 ? 'down' : 'up')
      if (shotImpactPending) {
        triggerCellAnim(
          target,
          oldParrot.row,
          oldParrot.col,
          `anim-parrot-flight-${direction}-${shotImpactDurationMs > 1000 ? 'long' : shotImpactDurationMs <= 450 ? 'arrow' : 'short'}`,
          Math.max(450, shotImpactDurationMs + 120),
        )
        triggerParrotAnim(target, newParrot.row, newParrot.col, 'anim-parrot-settle', 360)
      }
      else {
        triggerParrotAnim(target, newParrot.row, newParrot.col, `anim-parrot-arrive-${direction}`, 720)
      }
    }
    else if (oldParrot && !newParrot) {
      const direction = oldParrot.summonMoveDirection ?? 'Down'
      const step = direction === 'Up' ? { row: -1, col: 0 }
        : direction === 'Left' ? { row: 0, col: -1 }
          : direction === 'Right' ? { row: 0, col: 1 }
            : { row: 1, col: 0 }
      const deathRow = oldParrot.row + step.row
      const deathCol = oldParrot.col + step.col
      if (deathRow >= 0 && deathRow < 10 && deathCol >= 0 && deathCol < 10) {
        if (shotImpactPending) {
          triggerCellAnim(
            target,
            oldParrot.row,
            oldParrot.col,
            `anim-parrot-flight-${direction.toLowerCase()}-${shotImpactDurationMs > 1000 ? 'long' : shotImpactDurationMs <= 450 ? 'arrow' : 'short'}`,
            Math.max(450, shotImpactDurationMs + 120),
          )
        }
        triggerParrotAnim(target, deathRow, deathCol, 'anim-parrot-death', 820)
      }
    }
    let freezeSoundPlayed = false
    let explodeSoundPlayed = false
    let summonSpawnSoundPlayed = false
    for (const cell of newCells) {
      const key = `${cell.row},${cell.col}`
      if (map.value.has(key)) continue // already animating from shot result
      const old = oldMap.get(key)
      if (!old) continue

      // Detect newly changed states
      if ((cell.frozenSummonDeathIndices?.length ?? 0) >
          (old.frozenSummonDeathIndices?.length ?? 0)) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-freeze', 600)
        cellVfxHandler?.(target, cell.row, cell.col, 'freeze')
        if (!freezeSoundPlayed) { playBattleshipFreeze(); freezeSoundPlayed = true }
      } else if (cell.hasSummon && !old.hasSummon) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-summon-spawn', 1000)
        if (!summonSpawnSoundPlayed) { playComboStack(1); summonSpawnSoundPlayed = true }
      } else if (cell.isBurnResistMarked && !old.isBurnResistMarked) {
        triggerCellAnim(target, cell.row, cell.col, 'anim-scratch', 500)
      } else if (cell.isShipSunk && !old.isShipSunk) {
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

  function showNextTurnSkipNotice() {
    if (turnSkipNotice.value || pendingTurnSkipNotices.length === 0) return
    turnSkipNotice.value = pendingTurnSkipNotices.shift() ?? null
    turnSkipNoticeTimer = setTimeout(() => {
      turnSkipNotice.value = null
      turnSkipNoticeTimer = setTimeout(() => {
        turnSkipNoticeTimer = null
        showNextTurnSkipNotice()
      }, 80)
    }, 1500)
  }

  function enqueueTurnSkipNotice(result: BattleshipShotResult) {
    pendingTurnSkipNotices.push({
      id: ++turnSkipNoticeSerial,
      skippedPlayerId: result.skippedPlayerId
        ?? gameState.value?.currentTurnPlayerId
        ?? null,
      reason: result.skipReason ?? 'Unknown',
    })
    showNextTurnSkipNotice()
  }

  function clearTurnSkipNotices() {
    if (turnSkipNoticeTimer) clearTimeout(turnSkipNoticeTimer)
    turnSkipNoticeTimer = null
    pendingTurnSkipNotices.length = 0
    turnSkipNotice.value = null
  }

  function showPenaltyFeedback() {
    const id = penaltyFeedbackId.value + 1
    penaltyFeedbackId.value = id
    if (penaltyFeedbackTimer) clearTimeout(penaltyFeedbackTimer)
    penaltyFeedbackTimer = setTimeout(() => {
      if (penaltyFeedbackId.value === id) penaltyFeedbackId.value = 0
      penaltyFeedbackTimer = null
    }, 1400)
  }

  function initCallbacks() {
    signalrService.onBattleshipState = (state) => {
      // Snapshot old board cells before updating state (for diff animations)
      const oldSpectatorView = !!gameState.value && !gameState.value.myPlayerId
      const oldEnemyCells = oldSpectatorView
        ? gameState.value?.player2?.board?.cells
        : enemyPlayer.value?.board?.cells
      const oldMyCells = oldSpectatorView
        ? gameState.value?.player1?.board?.cells
        : myPlayer.value?.board?.cells

      const oldState = gameState.value
      const oldPhase = oldState?.phase ?? null
      const wasMyTurn = oldState?.isMyTurn ?? false
      const oldMyParrot = myPlayer.value?.summons?.find(summon =>
        summon.type === 'Parrot' && summon.isAlive)
      const nextMe = state.player1?.isMe ? state.player1
        : state.player2?.isMe ? state.player2
          : null
      const nextMyParrot = nextMe?.summons?.find(summon =>
        summon.type === 'Parrot' && summon.isAlive)
      if (oldState?.gameId === state.gameId && oldMyParrot
        && (!nextMyParrot
          || oldMyParrot.row !== nextMyParrot.row
          || oldMyParrot.col !== nextMyParrot.col)) {
        parrotStatusSnapshot.value = { ...oldMyParrot }
      }

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
        shotImpactPending = false
        shotImpactDurationMs = 0
        pendingParrotAnimations.length = 0
        if (parrotTransitionTimer) clearTimeout(parrotTransitionTimer)
        if (parrotImpactFallbackTimer) clearTimeout(parrotImpactFallbackTimer)
        parrotTransitionTimer = null
        parrotImpactFallbackTimer = null
        parrotTransitionActive.value = false
        parrotStatusSnapshot.value = null
        pendingTurnStartSound = false
        clearTurnSkipNotices()
        activateShotDelay(0)
      }

      detectBranderDetonation(oldState, state)

      gameState.value = state

      // Sync selectedShotType from server (auto-reset after WhiteStone/Buckshot)
      const me = state.player1?.isMe ? state.player1 : state.player2
      const delayPlayer = state.currentTurnPlayerId === state.player1?.discordId
        ? state.player1
        : state.currentTurnPlayerId === state.player2?.discordId
          ? state.player2
          : null
      activateShotDelay(
        delayPlayer?.shotDelayRemainingMs ?? 0,
        delayPlayer?.shotDelayDurationMs ?? 0,
        delayPlayer?.discordId ?? null,
      )
      if (me?.selectedShotType) {
        selectedShotType.value = me.selectedShotType
        selectedWeaponType.value = me.selectedShotType === 'WhiteStone' || me.selectedShotType === 'Buckshot'
          ? 'Tetracatapult'
          : me.availableWeapons?.find(w => w.id === me.selectedWeaponId)?.type ?? me.selectedShotType
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

      // Diff boards for multi-cell animations (sunk ship cells, burn spread, freeze, etc.)
      const spectatorView = !state.myPlayerId
      const newEnemyCells = spectatorView
        ? state.player2?.board?.cells
        : enemyPlayer.value?.board?.cells
      const newMyCells = spectatorView
        ? state.player1?.board?.cells
        : myPlayer.value?.board?.cells
      diffBoardAnimations(oldEnemyCells, newEnemyCells, 'enemy')
      diffBoardAnimations(oldMyCells, newMyCells, 'my')

      // Turn start — wait until an arriving/dying Parrot finishes its boundary animation.
      if (!wasMyTurn && state.isMyTurn && !turnSkipNotice.value
          && (state.phase === 'Combat' || state.phase === 'Boarding')) {
        if (parrotTransitionActive.value) pendingTurnStartSound = true
        else playBattleshipTurnStart()
      }
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
        const isAutomaticShot = result.isAutomaticShot === true
        if (result.wasSkipped) {
          lastShotResult.value = null
          pendingShotTarget = null
          enqueueTurnSkipNotice(result)
          return
        }
        lastShotResult.value = result
        if (result.penaltyApplied) showPenaltyFeedback()

        // Start the public reload visualization before launching the projectile. Hit
        // flights are intentionally long, so the reload bar overlaps their animation.
        if (!isAutomaticShot) {
          activateShotDelay(
            result.shotDelayMs ?? 0,
            result.shotDelayMs ?? 0,
            gameState.value?.currentTurnPlayerId ?? null,
          )
        }

        // Track last shot position
        const spectatorView = !gameState.value?.myPlayerId
        const shotTarget: 'enemy' | 'my' = result.targetPlayerId
          ? (spectatorView
              ? (result.targetPlayerId === gameState.value?.player1?.discordId ? 'my' : 'enemy')
              : (result.targetPlayerId === gameState.value?.myPlayerId ? 'my' : 'enemy'))
          : (isMyTurn.value ? (pendingShotTarget ?? 'enemy') : 'my')
        pendingShotTarget = null
        lastShotCell.value = { target: shotTarget, row: result.row, col: result.col }

        // Client-side match stats (my shots only)
        if (isMyTurn.value && !isAutomaticShot) {
          myShotsFired.value++
          if (result.hit && !result.miss) myShotsHit.value++
          if (result.shipSunk) myShipsSunk.value++
        }

        // Automatic Fortune volleys have no player-action attribution in the event,
        // so they must not award or reset the local manual-shot combo.
        if (!isAutomaticShot) {
          if (result.destroyed || result.shipSunk || result.burned) {
            killStreak.value++
            killStreakDisplay.value = killStreak.value
            if (killStreakTimer) clearTimeout(killStreakTimer)
            killStreakTimer = setTimeout(() => { killStreakDisplay.value = 0 }, 3000)
          } else if (result.miss || result.dodged) {
            killStreak.value = 0
          }
        }

        // Sound + animation helper
        const fireShotEffects = () => {
          if (!isAutomaticShot) {
            shotImpactPending = false
            shotImpactDurationMs = 0
          }
          triggerShotAnim(result, shotTarget)
          if (!isAutomaticShot) flushParrotAnimations()
          if (result.shipSunk) playBattleshipShipSunk()
          else if (result.burned) playBattleshipBurn()
          else if (result.dodged) playBattleshipDodge()
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
        if (!isAutomaticShot) {
          shotImpactPending = handled
          shotImpactDurationMs = handled
            ? (result.hit ? 3200 : result.projectileType === 'Arrow' ? 430 : 520)
            : 0
        }
        if (!handled) fireShotEffects()

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
      isCreating.value = false
      showError(error)
    }
  }

  function cleanupCallbacks() {
    signalrService.onBattleshipState = null
    signalrService.onBattleshipLobby = null
    signalrService.onBattleshipGameCreated = null
    signalrService.onBattleshipEvent = null
    signalrService.onShipCatalog = null
    signalrService.onBattleshipStats = null
    clearTurnSkipNotices()
    if (penaltyFeedbackTimer) clearTimeout(penaltyFeedbackTimer)
    penaltyFeedbackTimer = null
    penaltyFeedbackId.value = 0
    shotImpactPending = false
    shotImpactDurationMs = 0
    pendingParrotAnimations.length = 0
    if (parrotTransitionTimer) clearTimeout(parrotTransitionTimer)
    if (parrotImpactFallbackTimer) clearTimeout(parrotImpactFallbackTimer)
    parrotTransitionTimer = null
    parrotImpactFallbackTimer = null
    parrotTransitionActive.value = false
    parrotStatusSnapshot.value = null
    pendingTurnStartSound = false
    activateShotDelay(0)
  }

  // -- Actions ----------------------------------------------------

  function showError(error: string) {
    if (errorMessageTimer) clearTimeout(errorMessageTimer)
    errorMessage.value = error
    errorMessageTimer = setTimeout(() => {
      errorMessage.value = null
      errorMessageTimer = null
    }, 4000)
  }

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

  async function createGame(
    vsBot = true,
    botVersion: BattleshipBotVersion = 2,
  ) {
    errorMessage.value = null
    isCreating.value = true
    try {
      await signalrService.createBattleshipGameWithOptions(vsBot, botVersion)
    }
    catch (error) {
      isCreating.value = false
      showError(message('battleship.error.createFailed'))
      throw error
    }
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

  async function placeShip(shipId: string, row: number, col: number, orientation: BattleshipOrientation) {
    if (!gameId.value) return
    await signalrService.battleshipPlaceShip(gameId.value, shipId, row, col, orientation)
  }

  async function removeShip(shipId: string) {
    if (!gameId.value) return
    await signalrService.battleshipRemoveShip(gameId.value, shipId)
  }

  async function confirmPlacement(
    loadouts: BattleshipWeaponLoadout[],
    useSharedTetracatapultAmmo = true,
    useGhostSummons = true,
  ) {
    if (!gameId.value) return
    await signalrService.battleshipConfirmPlacement(
      gameId.value,
      loadouts,
      useSharedTetracatapultAmmo,
      useGhostSummons,
    )
  }

  async function cancelPlacement() {
    if (!gameId.value) return
    await signalrService.battleshipCancelPlacement(gameId.value)
  }

  async function shoot(row: number, col: number) {
    if (!gameId.value || shotDelayActive.value || parrotTransitionActive.value) return
    pendingShotTarget = 'enemy'
    await signalrService.battleshipShoot(gameId.value, row, col)
  }

  async function shootOwnBoard(row: number, col: number) {
    if (!gameId.value || parrotTransitionActive.value) return
    const evilGreekFireResponse = selectedShotType.value === 'EvilGreekFire'
      && shotDelayActive.value
      && !!shotDelayOwnerId.value
      && shotDelayOwnerId.value !== myPlayer.value?.discordId
    if (shotDelayActive.value && !evilGreekFireResponse) return
    pendingShotTarget = 'my'
    await signalrService.battleshipShootOwnBoard(gameId.value, row, col)
  }

  // Map weapon types to their actual shot behavior (must match backend WeaponTypeToShotType)
  function weaponToShotType(weaponType: string): string {
    switch (weaponType) {
      case 'Tetracatapult': return 'WhiteStone'
      case 'Neptune': return 'Neptune'
      case 'Incendiary': return 'Incendiary'
      case 'EvilIncendiary': return 'EvilIncendiary'
      case 'GreekFire': return 'GreekFire'
      case 'EvilGreekFire': return 'EvilGreekFire'
      case 'Cannon': return 'Cannon'
      case 'Fortuna': return 'Fortuna'
      case 'Warming': return 'Warming'
      default: return 'Ballista'
    }
  }

  async function selectWeapon(weaponType: string, shotType: string, weaponId: string) {
    if (!gameId.value || parrotTransitionActive.value) return
    selectedWeaponType.value = weaponType
    summonDeployMode.value = null
    playBattleshipWeaponSelect()
    selectedShotType.value = shotType || weaponToShotType(weaponType)
    await signalrService.battleshipSelectWeapon(gameId.value, weaponType, shotType, weaponId)
  }

  async function passBoardingTurn() {
    if (!gameId.value || parrotTransitionActive.value) return
    await signalrService.battleshipPassBoardingTurn(gameId.value)
  }

  async function deploySummon(summonTypeName: string, col: number, summonId?: string) {
    if (!gameId.value || parrotTransitionActive.value) return
    playBattleshipDeploy()
    await signalrService.battleshipDeploySummon(gameId.value, summonTypeName, col, summonId)
  }

  async function deployPendingSummon(pendingId: string, col: number) {
    if (!gameId.value || parrotTransitionActive.value) return
    playBattleshipDeploy()
    await signalrService.battleshipDeployPendingSummon(gameId.value, pendingId, col)
  }

  async function restoreShipWithPirateBoat(shipId: string) {
    if (!gameId.value || parrotTransitionActive.value) return
    playBattleshipDeploy()
    await signalrService.battleshipRestoreShipWithPirateBoat(gameId.value, shipId)
  }

  async function manualMove(shipId: string, direction: string, distance: number = 1) {
    if (!gameId.value || parrotTransitionActive.value) return
    await signalrService.battleshipManualMove(gameId.value, shipId, direction, distance)
  }

  async function setCursedBoatDirection(summonId: string, direction: string) {
    if (!gameId.value || parrotTransitionActive.value) return
    await signalrService.battleshipSetCursedBoatDirection(gameId.value, summonId, direction)
  }

  async function setParrotDirection(summonId: string, direction: string) {
    if (!gameId.value || parrotTransitionActive.value) return
    await signalrService.battleshipSetParrotDirection(gameId.value, summonId, direction)
  }

  async function assembleShip(
    groupId: string,
    row: number,
    col: number,
    orientation: BattleshipOrientation,
  ) {
    if (!gameId.value || parrotTransitionActive.value) return
    await signalrService.battleshipAssembleShip(gameId.value, groupId, row, col, orientation)
  }

  async function deployMatryoshka(
    parentShipId: string,
    row: number,
    col: number,
    orientation: BattleshipOrientation,
  ): Promise<boolean> {
    const activeGameId = gameId.value
    if (!activeGameId || parrotTransitionActive.value
      || myPlayer.value?.pendingMatryoshka?.parentShipId !== parentShipId)
      return false
    await signalrService.battleshipDeployMatryoshka(
      activeGameId,
      parentShipId,
      row,
      col,
      orientation,
    )
    const accepted = gameId.value === activeGameId
      && myPlayer.value?.pendingMatryoshka?.parentShipId !== parentShipId
    if (accepted) playBattleshipDeploy()
    return accepted
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
    switch (placementOrientation.value) {
      case 'Horizontal': placementOrientation.value = 'Vertical'; break
      case 'Vertical': placementOrientation.value = 'HorizontalReverse'; break
      case 'HorizontalReverse': placementOrientation.value = 'VerticalReverse'; break
      default: placementOrientation.value = 'Horizontal'
    }
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
    flintPlacementHoverCell,
    selectedShotType,
    selectedWeaponType,
    shotDelayActive,
    shotDelayInitialRemainingMs,
    shotDelayDurationMs,
    shotDelayOwnerId,
    summonDeployMode,
    summonType,
    enemyAnimatedCells,
    myAnimatedCells,
    parrotTransitionActive,
    lastShotCell,
    killStreak,
    killStreakDisplay,
    markedCells,
    previousPhase,
    phaseTransitionActive,
    screenShake,
    turnSkipNotice,
    penaltyFeedbackId,
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
    displayedMySummons,

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
    cancelPlacement,
    shoot,
    shootOwnBoard,
    forfeit,
    selectWeapon,
    passBoardingTurn,
    deploySummon,
    deployPendingSummon,
    restoreShipWithPirateBoat,
    manualMove,
    setCursedBoatDirection,
    setParrotDirection,
    assembleShip,
    deployMatryoshka,
    requestState,
    requestCatalog,
    toggleOrientation,
    cancelSummonDeploy,
    toggleMarkedCell,
    clearMarkedCells,
  }
})
