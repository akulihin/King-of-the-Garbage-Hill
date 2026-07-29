<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useBattleshipStore, type BattleshipImpactType } from 'src/store/battleship'
import type {
  BattleshipOrientation,
  BattleshipPendingSummon,
  BattleshipShotResult,
} from 'src/services/signalr'
import { useTip } from 'src/composables/useTip'
import BoardGrid from '../BoardGrid.vue'
import BsIcon from '../BsIcon.vue'
import WeaponBar from '../WeaponBar.vue'
import FleetPanel from '../FleetPanel.vue'
import BattleLogPanel from '../BattleLogPanel.vue'
import SummonBar from '../SummonBar.vue'
import ActionBar from '../ActionBar.vue'
import VfxCanvas from '../VfxCanvas.vue'
import ProjectileLayer, { type BattleshipProjectileKind } from '../ProjectileLayer.vue'
import { renderIcon } from '../battleship-icons'
import {
  BATTLESHIP_ORIENTATIONS,
  deckOffsetVector,
  occupiedCells as occupiedCellPositions,
  occupiedDeckCells,
  orientationLabel,
} from '../battleship-geometry'

const store = useBattleshipStore()
const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const phase = computed(() => store.phase)
const isMyTurn = computed(() => store.isMyTurn)
const myPlayer = computed(() => store.myPlayer)
const enemyPlayer = computed(() => store.enemyPlayer)
const myFleet = computed(() => store.myFleet)
const gameLog = computed(() => store.gameLog)
const pendingManeuver = computed(() => myPlayer.value?.pendingManeuver ?? null)
const pendingManeuverShip = computed(() =>
  myFleet.value.find(ship => ship.id === pendingManeuver.value?.shipId) ?? null)
const maneuverShipCells = computed(() =>
  pendingManeuverShip.value ? occupiedCellPositions(pendingManeuverShip.value) : [])
const maneuverTargetCells = computed(() =>
  pendingManeuver.value?.options.map(option => ({ row: option.row, col: option.col })) ?? [])
const pendingCursedBoatDirection = computed(() =>
  myPlayer.value?.pendingCursedBoatDirection ?? null)
const pendingAssembly = computed(() => myPlayer.value?.pendingAssembly ?? null)
const assemblyOrientation = ref<BattleshipOrientation>('Horizontal')
const assemblyOrientations = computed(() => BATTLESHIP_ORIENTATIONS.filter(orientation =>
  pendingAssembly.value?.options.some(option => option.orientation === orientation)))
const assemblyTargetCells = computed(() =>
  pendingAssembly.value?.options
    .filter(option => option.orientation === assemblyOrientation.value)
    .map(option => ({ row: option.row, col: option.col })) ?? [])

watch(() => pendingAssembly.value?.groupId, () => {
  assemblyOrientation.value = pendingAssembly.value?.options[0]?.orientation ?? 'Horizontal'
}, { immediate: true })

function cycleAssemblyOrientation() {
  const orientations = assemblyOrientations.value
  if (orientations.length < 2) return
  const current = orientations.indexOf(assemblyOrientation.value)
  assemblyOrientation.value = orientations[(current + 1) % orientations.length]!
}

const cursedBoatShipCells = computed(() => pendingCursedBoatDirection.value
  ? [{ row: pendingCursedBoatDirection.value.row, col: pendingCursedBoatDirection.value.col }]
  : [])
const cursedBoatTargetCells = computed(() =>
  pendingCursedBoatDirection.value?.options.map(option => ({ row: option.row, col: option.col })) ?? [])
const mandatoryInteractionActive = computed(() =>
  !!pendingManeuver.value || !!pendingCursedBoatDirection.value || !!pendingAssembly.value)

watch(mandatoryInteractionActive, (active) => {
  if (active) store.cancelSummonDeploy()
}, { immediate: true })

// ── Weapon selection (state lives in the store) ───────────────
const selectedWeaponShip = computed(() => {
  const selectedId = myPlayer.value?.selectedWeaponId
  const w = store.availableWeapons.find(w => w.id === selectedId && w.shotType === store.selectedShotType)
    ?? store.availableWeapons.find(w => w.shotType === store.selectedShotType)
    ?? store.availableWeapons.find(w => w.type === store.selectedWeaponType)
  return w ?? null
})

const farBlockedRows = computed<Set<number>>(() => {
  const w = selectedWeaponShip.value
  if (w && w.shipRange === 'Far' && w.shipRow >= 8) {
    return new Set([8, 9])
  }
  return new Set()
})

const activeBlockedRows = computed(() => store.summonDeployMode ? new Set<number>() : farBlockedRows.value)

// ── Summon deployment ─────────────────────────────────────────
const capturedShipIds = computed(() => new Set(
  (store.myBoard?.cells ?? []).flatMap(c => c.isCaptured && c.shipId ? [c.shipId] : []),
))
const capturedShipCells = computed(() =>
  (store.myBoard?.cells ?? [])
    .filter(cell => cell.isCaptured)
    .map(cell => ({ row: cell.row, col: cell.col })))
const hasCapturedShip = computed(() =>
  store.myBoard?.cells.some(cell => cell.isCaptured && !cell.isDestroyed) ?? false)
const captureFocusActive = computed(() =>
  hasCapturedShip.value && !mandatoryInteractionActive.value)

const hasBranderUpgrade = computed(() => {
  return myPlayer.value?.fleet?.some(s => !s.isDestroyed && !capturedShipIds.value.has(s.id) &&
    s.abilities?.includes('brander_summon')) ?? false
})

// ТЗ #11/#12: summon availability is gated by fleet regions (Таран⇒Запад, Разведчик⇒Восток,
// Пират⇒Юг); Brander needs the boiler upgrade and is once per match (ТЗ #10)
const availableSummons = computed<string[]>(() => {
  const regions = new Set(myPlayer.value?.fleet?.filter(s => !capturedShipIds.value.has(s.id))
    .flatMap(s => s.regions ?? []) ?? [])
  const list: string[] = []
  if (regions.has('West')) list.push('Ram')
  if (regions.has('East')) list.push('Scout')
  if (regions.has('South')) list.push('PirateBoat')
  const hasWaitingBrander = myPlayer.value?.summons?.some(s => s.type === 'Brander' && s.waitingForTurnBack) ?? false
  if (hasBranderUpgrade.value && (!myPlayer.value?.branderUsed || hasWaitingBrander)) list.push('Brander')
  for (const waiting of myPlayer.value?.summons?.filter(s => s.waitingForTurnBack) ?? []) {
    if (!list.includes(waiting.type)) list.push(waiting.type)
  }
  return list
})

const hasEnemySummonOnMyBoard = computed(() => {
  const myId = store.gameState?.myPlayerId
  return store.myBoard?.cells.some(c => c.hasSummon && c.summonOwnerId && c.summonOwnerId !== myId) ?? false
})

const boardingPlacementPending = computed(() =>
  !!myPlayer.value?.hasPendingBoardingDeployment || !!enemyPlayer.value?.hasPendingBoardingDeployment)

// Penalty zone: rows 0-2 highlighted when enemy summons present (#3)
const penaltyZoneRows = computed<number[]>(() => {
  if (!hasEnemySummonOnMyBoard.value) return []
  const myId = store.gameState?.myPlayerId
  const hasInPenaltyZone = store.myBoard?.cells.some(c =>
    c.hasSummon && c.summonOwnerId && c.summonOwnerId !== myId && c.row <= 2
  ) ?? false
  return hasInPenaltyZone ? [0, 1, 2] : []
})

function canDeploySummonType(type: string): boolean {
  if (!myPlayer.value || !enemyPlayer.value) return false
  if (boardingPlacementPending.value) return false
  const p = myPlayer.value
  if (!p.canDeployAnySummon) return false
  if (!availableSummons.value.includes(type)) return false
  const isReentry = p.summons?.some(s =>
    s.type === type && s.waitingForTurnBack) ?? false
  // ТЗ #10: Brander is outside the four normal per-match uses
  if (!isReentry && type !== 'Brander' && p.summonSlotsUsed >= p.maxSummonSlots) return false
  const threshold = 5 * (p.summonSlotsUsed + 1)
  if (!isReentry && phase.value !== 'Boarding' && p.revealedCellCount < threshold) return false
  if (phase.value !== 'Boarding' && p.summonCooldownRemaining > 0) return false
  return true
}

const canDeploySummon = computed(() => canDeploySummonType(store.summonType))
const deployableSummons = computed(() => availableSummons.value.filter(canDeploySummonType))

function enterSummonDeployMode() {
  if (!canDeploySummon.value) return
  const waiting = myPlayer.value?.summons?.find(s =>
    s.type === store.summonType && s.waitingForTurnBack)
  store.summonDeployMode = waiting
    ? {
        type: waiting.type,
        reentryDirection: waiting.moveDirection,
        reentryRow: waiting.row,
        reentryCol: waiting.col,
      }
    : { type: store.summonType }
}

function enterPendingSummonDeployMode(ps: BattleshipPendingSummon) {
  if (boardingPlacementPending.value && !ps.isBoarding) return
  store.summonDeployMode = {
    type: ps.type,
    pendingId: ps.id,
    pendingCols: ps.allowedColumns.length ? ps.allowedColumns : undefined,
  }
}

const summonDeployHighlight = ref<{ row: number; col: number }[]>([])

function updateSummonDeployHighlight(row: number, col: number) {
  if (!store.summonDeployMode) { summonDeployHighlight.value = []; return }
  const allowed = summonDeployAllowedCells.value.some(cell => cell.row === row && cell.col === col)
  if (!allowed) {
    summonDeployHighlight.value = []
    return
  }
  summonDeployHighlight.value = [{ row, col }]
}

const summonDeployAllowedCells = computed<{ row: number; col: number }[]>(() => {
  if (!store.summonDeployMode) return []
  const mode = store.summonDeployMode
  if (mode.reentryDirection) {
    if (mode.reentryDirection === 'Up' || mode.reentryDirection === 'Down') {
      const row = mode.reentryDirection === 'Down' ? 9 : 0
      const center = mode.reentryCol ?? 0
      return [center - 1, center, center + 1]
        .filter(col => col >= 0 && col < 10)
        .map(col => ({ row, col }))
    }
    const col = mode.reentryDirection === 'Right' ? 9 : 0
    const center = mode.reentryRow ?? 0
    return [center - 1, center, center + 1]
      .filter(row => row >= 0 && row < 10)
      .map(row => ({ row, col }))
  }
  const cols = store.summonDeployMode.pendingCols
  if (!cols) return Array.from({ length: 10 }, (_, i) => ({ row: 0, col: i }))
  return cols.map(c => ({ row: 0, col: c }))
})

// ── Shot delay countdown ────────────────────────────────────
const shotDelayRemaining = ref(0)
const shotDelayTotalSeconds = computed(() =>
  Math.max(0.1, store.shotDelayDurationMs / 1000))
const shotDelayPlayerName = computed(() => {
  if (store.shotDelayOwnerId === myPlayer.value?.discordId)
    return myPlayer.value?.username ?? 'Вы'
  if (store.shotDelayOwnerId === enemyPlayer.value?.discordId)
    return enemyPlayer.value?.username ?? 'Противник'
  return 'Игрок'
})
let shotDelayTimer: ReturnType<typeof setInterval> | null = null

watch(
  [() => store.shotDelayActive, () => store.shotDelayInitialRemainingMs],
  ([active, initialRemainingMs]) => {
    if (shotDelayTimer) {
      clearInterval(shotDelayTimer)
      shotDelayTimer = null
    }
    if (active) {
      shotDelayRemaining.value = Math.max(0.1, initialRemainingMs / 1000)
      shotDelayTimer = setInterval(() => {
        shotDelayRemaining.value = Math.max(0, +(shotDelayRemaining.value - 0.1).toFixed(1))
        if (shotDelayRemaining.value <= 0 && shotDelayTimer) {
          clearInterval(shotDelayTimer)
          shotDelayTimer = null
        }
      }, 100)
    } else {
      shotDelayRemaining.value = 0
    }
  },
)

// ── AoE cursor previews ─────────────────────────────────────
const isBuckshotMode = computed(() => store.selectedShotType === 'Buckshot')
const isIncendiaryMode = computed(() =>
  store.selectedShotType === 'Incendiary' || store.selectedShotType === 'EvilIncendiary')
const isGreekFireMode = computed(() =>
  store.selectedShotType === 'GreekFire' || store.selectedShotType === 'EvilGreekFire')
const isEvilGreekFireResponse = computed(() =>
  store.selectedShotType === 'EvilGreekFire'
  && !isMyTurn.value
  && store.shotDelayActive
  && store.shotDelayOwnerId === enemyPlayer.value?.discordId)
const canUseOwnBoardWeapon = computed(() =>
  (isMyTurn.value && !store.shotDelayActive) || isEvilGreekFireResponse.value)
const catapultReady = computed(() => isMyTurn.value && !store.shotDelayActive &&
  !boardingPlacementPending.value && !hasCapturedShip.value &&
  store.availableWeapons.some(w => w.type === 'Tetracatapult' && w.aimSpeed <= 0 && w.hasAmmo))
const aoeHighlight = ref<{ row: number; col: number }[]>([])

function updateAoeHighlight(row: number, col: number) {
  if (isBuckshotMode.value) {
    aoeHighlight.value = [
      { row, col }, { row, col: col + 1 },
      { row: row + 1, col }, { row: row + 1, col: col + 1 }
    ].filter(c => c.row < 10 && c.col < 10)
  } else if (isIncendiaryMode.value) {
    aoeHighlight.value = [{ row, col }]
  } else {
    aoeHighlight.value = []
  }
}

const enemyHighlight = computed(() => {
  if (store.summonDeployMode) {
    const allowed = summonDeployAllowedCells.value
    return [...allowed, ...summonDeployHighlight.value]
  }
  if (isBuckshotMode.value || isIncendiaryMode.value) return aoeHighlight.value
  return []
})

// ── Handlers ─────────────────────────────────────────────────
async function handleEnemyCellClick(row: number, col: number) {
  if (pendingManeuver.value || pendingAssembly.value) return
  if (pendingCursedBoatDirection.value) {
    const option = pendingCursedBoatDirection.value.options
      .find(value => value.row === row && value.col === col)
    if (option)
      await store.setCursedBoatDirection(
        pendingCursedBoatDirection.value.summonId, option.direction)
    return
  }
  if (store.summonDeployMode) {
    const mode = store.summonDeployMode
    const allowed = summonDeployAllowedCells.value.some(cell => cell.row === row && cell.col === col)
    if (!allowed) return
    if (mode.pendingId) {
      await store.deployPendingSummon(mode.pendingId, col)
    } else {
      const reentryLane = mode.reentryDirection === 'Left' || mode.reentryDirection === 'Right'
        ? row
        : col
      await store.deploySummon(mode.type, reentryLane)
    }
    store.summonDeployMode = null
    return
  }
  if (!isMyTurn.value || (phase.value !== 'Combat' && phase.value !== 'Boarding')) return
  if (myPlayer.value?.pendingSummons?.some(p => p.isBoarding)) return
  if (store.shotDelayActive) return
  if (isGreekFireMode.value || hasCapturedShip.value) return
  if (farBlockedRows.value.has(row)) return
  await store.shoot(row, col)
}

async function handleMyBoardCellClick(row: number, col: number) {
  if (phase.value !== 'Combat' && phase.value !== 'Boarding') return
  if (pendingAssembly.value) {
    const option = pendingAssembly.value.options.find(value =>
      value.row === row
      && value.col === col
      && value.orientation === assemblyOrientation.value)
    if (option) {
      await store.assembleShip(
        pendingAssembly.value.groupId,
        option.row,
        option.col,
        option.orientation,
      )
    }
    return
  }
  if (pendingCursedBoatDirection.value) return
  if (pendingManeuver.value) {
    if (!isMyTurn.value) return
    const option = pendingManeuver.value.options.find(value => value.row === row && value.col === col)
    if (option)
      await store.manualMove(pendingManeuver.value.shipId, option.direction, option.distance)
    return
  }
  if (!isMyTurn.value && !isEvilGreekFireResponse.value) return
  if (store.shotDelayActive && !isEvilGreekFireResponse.value) return
  if (myPlayer.value?.pendingSummons?.some(p => p.isBoarding)) return
  const cell = store.myBoard?.cells.find(c => c.row === row && c.col === col)
  if (isEvilGreekFireResponse.value) {
    await store.shootOwnBoard(row, col)
    return
  }
  if (hasCapturedShip.value) {
    if (cell?.isCaptured && !cell.isDestroyed) await store.shootOwnBoard(row, col)
    return
  }
  // Greek Fire is an own-board-only Boiler shot.
  if (isGreekFireMode.value) {
    await store.shootOwnBoard(row, col)
    return
  }
  const myId = store.gameState?.myPlayerId
  if (!cell || !cell.hasSummon || !cell.summonOwnerId || cell.summonOwnerId === myId) return
  await store.shootOwnBoard(row, col)
}

function handleEnemyHover(row: number, col: number) {
  if (mandatoryInteractionActive.value) return
  if (store.summonDeployMode) updateSummonDeployHighlight(row, col)
  else updateAoeHighlight(row, col)
}

async function handleWeaponSelect(weaponType: string, shotType: string, weaponId: string) {
  await store.selectWeapon(weaponType, shotType, weaponId)
}

function handleEnemyRightClick(row: number, col: number) {
  store.toggleMarkedCell(row, col)
}

function factionLabel(faction: string | undefined): string {
  if (faction === 'Alliance') return 'Альянс'
  if (faction === 'Empire') return 'Империя'
  return faction ?? ''
}

// ── Banners / result presentation ────────────────────────────
const firstTurnBanner = computed(() => {
  if ((store.shotCount ?? 0) > 1) return null
  const name = isMyTurn.value ? (myPlayer.value?.username ?? 'Вы') : (enemyPlayer.value?.username ?? 'Противник')
  return `Первый ход: ${name}`
})

const shotResultClass = computed(() => {
  const r = store.lastShotResult
  if (!r) return {}
  return {
    'shot-hit': r.hit && !r.scratched,
    'shot-miss': r.miss && !r.scratched,
    'shot-scratch': r.scratched && !r.miss,
    'shot-dodge': r.dodged,
    'shot-sunk': r.shipSunk,
    'shot-burn': r.burned,
    'shot-destroy': r.destroyed && !r.shipSunk,
  }
})

const enemyShipNameMap = computed(() => {
  const map = new Map<string, string>()
  if (!enemyPlayer.value?.fleet) return map
  for (const s of enemyPlayer.value.fleet) map.set(s.id, s.name)
  return map
})
const myShipNameMap = computed(() => {
  const map = new Map<string, string>()
  for (const s of myFleet.value) map.set(s.id, s.name)
  return map
})

const enemyLastShot = computed(() => {
  const c = store.lastShotCell
  if (!c || c.target !== 'enemy') return null
  return { row: c.row, col: c.col }
})
const myLastShot = computed(() => {
  const c = store.lastShotCell
  if (!c || c.target !== 'my') return null
  return { row: c.row, col: c.col }
})

// ТЗ #2: trails are viewer-relative. MY summons sail on the ENEMY board — their trail renders
// there; ENEMY summons sail on MY board — their trail (incl. spawn cell) renders there.
const enemySummonTrails = computed(() => {
  const trails = new Map<string, string[]>()
  for (const cell of store.enemyBoard?.cells ?? []) {
    if (cell.summonTrails?.length)
      trails.set(`${cell.row},${cell.col}`, [...new Set(cell.summonTrails)])
  }
  return trails
})
const mySummonTrails = computed(() => {
  const trails = new Map<string, string[]>()
  for (const cell of store.myBoard?.cells ?? []) {
    if (cell.summonTrails?.length)
      trails.set(`${cell.row},${cell.col}`, [...new Set(cell.summonTrails)])
  }
  return trails
})

// ── Range overlays ───────────────────────────────────────────
const myBoardRangeOverlays = computed(() => {
  const map = new Map<string, string>()
  if (!myFleet.value) return map

  for (const ship of myFleet.value) {
    if (ship.isDestroyed || !ship.isPlaced) continue
    const abilities = ship.abilities ?? []

    if (abilities.includes('poison_cone')) {
      const [firstRow, firstCol] = getOccupiedCells(ship)[0] ?? [ship.row, ship.col]
      const sternStep = deckOffsetVector(
        ship.orientation,
        ship.abilities.includes('diagonal_shape'),
      )
      const [forwardRow, forwardCol] = [-sternStep.row, -sternStep.col]
      const [sideRow, sideCol] = [sternStep.col, -sternStep.row]
      for (let depth = 1; depth <= 2; depth++) {
        for (let side = -depth; side <= depth; side++) {
          addCell(map,
            firstRow + forwardRow * depth + sideRow * side,
            firstCol + forwardCol * depth + sideCol * side,
            'poison')
        }
      }
    }

    if (abilities.includes('explode_on_hit')) {
      const radius = ship.explosionRadius || ship.space || 1
      const occupied = getOccupiedCells(ship)
      for (const [r, c] of occupied) {
        for (let dr = -radius; dr <= radius; dr++) {
          for (let dc = -radius; dc <= radius; dc++) {
            const key = `${r + dr},${c + dc}`
            if (!occupied.some(([or, oc]) => or === r + dr && oc === c + dc)) {
              addCell(map, r + dr, c + dc, 'explosion')
            }
          }
        }
      }
    }

    if (abilities.includes('freeze_nearby')) {
      const radius = ship.space ?? 1
      const occupied = getOccupiedCells(ship)
      for (const [r, c] of occupied) {
        for (let dr = -radius; dr <= radius; dr++) {
          for (let dc = -radius; dc <= radius; dc++) {
            if (!occupied.some(([or, oc]) => or === r + dr && oc === c + dc)) {
              addCell(map, r + dr, c + dc, 'freeze')
            }
          }
        }
      }
    }
  }

  if (hasEnemySummonOnMyBoard.value) {
    const myId = store.gameState?.myPlayerId
    for (const c of store.myBoard?.cells ?? []) {
      if (c.hasSummon && c.summonOwnerId && c.summonOwnerId !== myId) {
        addCell(map, c.row, c.col, 'ownboard-target')
      }
    }
  }
  if (hasCapturedShip.value) {
    for (const c of store.myBoard?.cells ?? []) {
      if (c.isCaptured && !c.isDestroyed) addCell(map, c.row, c.col, 'ownboard-target')
    }
  }

  // Penalty zone: rows 0-2 when enemy summons present (#3)
  if (penaltyZoneRows.value.length > 0) {
    for (const row of penaltyZoneRows.value) {
      for (let col = 0; col < 10; col++) {
        addCell(map, row, col, 'penalty-zone')
      }
    }
  }

  return map
})

const enemyBoardRangeOverlays = computed(() => {
  const map = new Map<string, string>()

  const myId = store.gameState?.myPlayerId
  for (const cell of store.enemyBoard?.cells ?? []) {
    if (cell.hasSummon && cell.summonOwnerId === myId && cell.summonType === 'Brander') {
      for (let dr = -1; dr <= 1; dr++) {
        for (let dc = -1; dc <= 1; dc++) {
          if (dr === 0 && dc === 0) continue
          addCell(map, cell.row + dr, cell.col + dc, 'brander')
        }
      }
    }
  }

  return map
})

function addCell(map: Map<string, string>, row: number, col: number, type: string) {
  if (row < 0 || row >= 10 || col < 0 || col >= 10) return
  const key = `${row},${col}`
  if (!map.has(key)) map.set(key, type)
}

function hasOverlayType(map: Map<string, string>, type: string): boolean {
  for (const v of map.values()) { if (v === type) return true }
  return false
}

function hasTrailType(map: Map<string, string[]>, type: string): boolean {
  for (const values of map.values()) { if (values.includes(type)) return true }
  return false
}

function getOccupiedCells(ship: {
  row: number
  col: number
  deckCount: number
  orientation: BattleshipOrientation
  abilities?: string[]
  decks?: Array<{ index: number }>
}): [number, number][] {
  return occupiedCellPositions(ship).map(cell => [cell.row, cell.col])
}

// ── Weapon cursor ────────────────────────────────────────────
const weaponCursorClass = computed(() => {
  if (!isMyTurn.value || mandatoryInteractionActive.value || hasCapturedShip.value) return ''
  switch (store.selectedShotType) {
    case 'Buckshot': return 'cursor-buckshot'
    case 'WhiteStone': return 'cursor-whitestone'
    case 'Incendiary': return 'cursor-incendiary'
    case 'EvilIncendiary': return 'cursor-incendiary'
    case 'GreekFire': return 'cursor-greekfire'
    case 'EvilGreekFire': return 'cursor-greekfire'
    default: return 'cursor-ballista'
  }
})

// ── Boarding zoom cinematic ──────────────────────────────────
const boardingZoomActive = ref(false)

watch(phase, (val) => {
  if (val === 'Boarding') {
    boardingZoomActive.value = true
    setTimeout(() => { boardingZoomActive.value = false }, 1200)
  }
})

// ── VFX: real projectile handshake ───────────────────────────
const enemyVfxRef = ref<InstanceType<typeof VfxCanvas> | null>(null)
const myVfxRef = ref<InstanceType<typeof VfxCanvas> | null>(null)
const projectileLayerRef = ref<InstanceType<typeof ProjectileLayer> | null>(null)
const enemyStageRef = ref<HTMLElement | null>(null)
const myStageRef = ref<HTMLElement | null>(null)

function projectileKindFor(result: BattleshipShotResult | null): BattleshipProjectileKind {
  switch (result?.projectileType) {
    case 'Stone': return 'stone'
    case 'Buckshot': return 'buckshot'
    case 'Fire': return 'fire'
    default: return 'arrow'
  }
}

function impactTypeFor(result: BattleshipShotResult | null): BattleshipImpactType {
  if (!result) return 'miss'
  if (result.dodged) return 'scratch'
  if (result.shipSunk) return 'sunk'
  if (result.burned) return 'burn'
  if (result.destroyed) return 'destroy'
  if (result.scratched) return 'scratch'
  if (result.hit) return 'hit'
  return 'miss'
}

onMounted(() => {
  store.setShotVfxHandler((row, col, target, fire) => {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return false
    const result = store.lastShotResult
    const canvas = target === 'enemy' ? enemyVfxRef.value : myVfxRef.value
    const targetStage = target === 'enemy' ? enemyStageRef.value : myStageRef.value
    if (!canvas || !targetStage || !projectileLayerRef.value) return false

    const sourceShip = myFleet.value.find(ship => ship.id === result?.sourceShipId)
    const sourceCells = sourceShip ? occupiedDeckCells(sourceShip) : []
    const fallback = sourceCells.find(cell => cell.deckIndex === result?.sourceDeckIndex)
      ?? sourceCells[0]
      ?? { row: 0, col: 0 }
    const sourceRow = (result?.sourceRow ?? -1) >= 0 ? result!.sourceRow : fallback.row
    const sourceCol = (result?.sourceCol ?? -1) >= 0 ? result!.sourceCol : fallback.col
    const myId = store.gameState?.myPlayerId
    const enemyId = enemyPlayer.value?.discordId
    const sourceStage = result?.sourceBoardPlayerId === myId
      ? myStageRef.value
      : result?.sourceBoardPlayerId === enemyId
        ? enemyStageRef.value
        : sourceShip
          ? myStageRef.value
          : null

    return projectileLayerRef.value.fire(
      sourceStage,
      targetStage,
      sourceRow,
      sourceCol,
      row,
      col,
      projectileKindFor(result),
      () => {
        fire()
        canvas.spawnImpact(row, col, impactTypeFor(result))
      },
      result?.hit ? 3200 : undefined,
    )
  })

  store.setCellVfxHandler((target, row, col, type) => {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return
    const canvas = target === 'enemy' ? enemyVfxRef.value : myVfxRef.value
    canvas?.spawnImpact(row, col, type)
  })
})

onUnmounted(() => {
  store.setShotVfxHandler(null)
  store.setCellVfxHandler(null)
  if (shotDelayTimer) clearInterval(shotDelayTimer)
})
</script>

<template>
  <div
    class="phase-content"
    :class="{
      'catapult-ready': catapultReady && !mandatoryInteractionActive,
      'mandatory-lock': mandatoryInteractionActive,
      'capture-attention': captureFocusActive,
    }"
  >
    <div v-if="pendingAssembly" class="bs-banner bs-banner--warning maneuver-banner assembly-banner">
      Обязательная сборка: выберите положение трёхпалубного корабля.
      <button
        class="bs-btn bs-btn--sm assembly-rotate"
        type="button"
        :disabled="assemblyOrientations.length < 2"
        @click="cycleAssemblyOrientation"
      >
        <BsIcon icon="rotate" :size="13" />
        {{ orientationLabel(assemblyOrientation, false) }}
      </button>
    </div>
    <div v-else-if="pendingManeuver" class="bs-banner bs-banner--warning maneuver-banner">
      Обязательный манёвр: выберите ярко-зелёную клетку для корабля
      «{{ pendingManeuver.shipName }}».
    </div>
    <div v-else-if="pendingCursedBoatDirection" class="bs-banner bs-banner--warning maneuver-banner">
      Проклятая лодка меняет курс: выберите ярко-зелёную соседнюю клетку.
    </div>
    <div v-if="firstTurnBanner" class="bs-banner bs-banner--gold first-turn-banner">{{ firstTurnBanner }}</div>
    <div v-if="phase === 'Boarding'" class="bs-banner bs-banner--warning">Абордаж! Близкие корабли идут на таран.</div>

    <!-- Weapon Bar -->
    <WeaponBar
      :selected-shot-type="store.selectedShotType"
      :selected-weapon-id="myPlayer?.selectedWeaponId ?? null"
      :available-weapons="store.availableWeapons"
      :shot-delay-active="store.shotDelayActive"
      :shot-delay-remaining="shotDelayRemaining"
      :phase="phase"
      @select-weapon="handleWeaponSelect"
    />

    <!-- Summons stay directly below weapons; choosing an icon immediately opens cell selection. -->
    <SummonBar
      :my-player="myPlayer"
      :phase="phase"
      :shot-count="store.shotCount"
      :can-deploy-summon="canDeploySummon"
      :boarding-placement-pending="boardingPlacementPending"
      :deployable-summons="deployableSummons"
      :available-summons="availableSummons"
      :summon-deploy-mode="store.summonDeployMode"
      @enter-deploy="enterSummonDeployMode"
      @enter-pending-deploy="enterPendingSummonDeployMode"
      @cancel-deploy="store.cancelSummonDeploy()"
      @set-summon-type="(t: string) => store.summonType = t"
    />

    <div v-if="catapultReady" class="catapult-ready-label" role="status">Камнемёт готов к выстрелу!</div>

    <!-- Status Banners -->
    <div v-if="myPlayer?.hasPendingBoardingDeployment" class="bs-banner bs-banner--warning">
      Разместите все абордажные корабли перед выстрелом!
    </div>
    <div v-else-if="enemyPlayer?.hasPendingBoardingDeployment" class="bs-banner bs-banner--warning">
      Противник расставляет абордажные корабли. Бой продолжится после завершения расстановки.
    </div>
    <div v-if="myPlayer?.hasPenalty" class="bs-banner bs-banner--warning">
      Штраф: следующий ход будет пропущен!
    </div>
    <div v-if="myPlayer && store.gameState && myPlayer.stunShotExpiry >= store.gameState.shotCount" class="bs-banner bs-banner--warning">
      Оглушение! Вы пропускаете ход.
    </div>
    <div v-if="store.shotDelayActive" class="bs-banner bs-banner--info shot-delay-banner">
      Перезарядка — {{ shotDelayPlayerName }}:
      <span class="delay-countdown bs-mono">{{ shotDelayRemaining.toFixed(1) }}с</span>
      <div
        class="delay-progress"
        :style="{ width: Math.min(100, Math.max(0, (shotDelayTotalSeconds - shotDelayRemaining) / shotDelayTotalSeconds * 100)) + '%' }"
      ></div>
    </div>
    <div
      v-if="isEvilGreekFireResponse"
      class="bs-banner bs-banner--gold"
    >
      Окно ответа: Злой Греческий огонь можно применить на своей доске.
    </div>
    <div
      v-else-if="!isMyTurn && store.shotDelayActive && myPlayer?.canDeployAnySummon"
      class="bs-banner bs-banner--gold"
    >
      Окно ответа: можно выпустить сумона до следующего выстрела противника.
    </div>

    <!-- Battle Boards -->
    <div
      class="combat-layout"
      :class="{
        'board-shake': store.screenShake,
        'boarding-zoom': boardingZoomActive,
        'maneuver-focus': !!pendingManeuver || !!pendingAssembly,
        'assembly-focus': !!pendingAssembly,
        'cursed-focus': !!pendingCursedBoatDirection,
        'capture-focus': captureFocusActive,
      }"
    >
      <!-- Enemy Board (primary) -->
      <div class="board-section board-enemy" :class="[{ 'board-active': !isMyTurn }, weaponCursorClass]">
        <div class="board-label">
          <span class="player-label">{{ enemyPlayer?.username ?? 'Противник' }}</span>
          <span v-if="enemyPlayer" class="faction-label">{{ factionLabel(enemyPlayer.faction) }}</span>
          <span v-if="enemyPlayer" class="indicator-badges">
            <span v-if="enemyPlayer.stunShotExpiry >= store.shotCount" class="bs-badge bs-badge--stun" @mouseenter="showTip($event, 'Оглушён')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('stun', 12)"></span>
            <span v-if="enemyPlayer.hasPenalty" class="bs-badge bs-badge--penalty" @mouseenter="showTip($event, 'Штраф')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('penalty', 12)"></span>
            <span v-if="phase === 'Boarding'" class="bs-badge bs-badge--boarding" @mouseenter="showTip($event, 'Абордаж')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('crossbones', 12)"></span>
          </span>
          <span v-if="enemyPlayer" class="revealed-count bs-mono">Разведано: {{ enemyPlayer.revealedCellCount }}/100</span>
        </div>
        <div ref="enemyStageRef" class="board-stage">
          <BoardGrid
            :board="store.enemyBoard"
            :is-enemy="true"
            :cell-size="42"
            :shot-type="store.selectedShotType"
            :clickable="!!pendingCursedBoatDirection || (!pendingManeuver && !pendingAssembly && ((isMyTurn && !store.shotDelayActive && !boardingPlacementPending && !isGreekFireMode && !hasCapturedShip) || !!store.summonDeployMode))"
            :highlight-cells="enemyHighlight"
            :blocked-rows="activeBlockedRows"
            :animated-cells="store.enemyAnimatedCells"
            :last-shot-cell="enemyLastShot"
            :marked-cells="store.markedCells"
            :summon-trail-cells="enemySummonTrails"
            :ship-name-map="enemyShipNameMap"
            :range-overlay-cells="enemyBoardRangeOverlays"
            :maneuver-active="!!pendingCursedBoatDirection"
            :maneuver-ship-cells="cursedBoatShipCells"
            :maneuver-target-cells="cursedBoatTargetCells"
            @cell-click="handleEnemyCellClick"
            @cell-hover="handleEnemyHover"
            @cell-right-click="handleEnemyRightClick"
            @tip-show="showTip" @tip-move="moveTip" @tip-hide="hideTip"
          />
          <VfxCanvas v-if="store.vfxEnabled" ref="enemyVfxRef" />
        </div>
        <div v-if="enemyBoardRangeOverlays.size > 0" class="range-legend">
          <span v-if="hasOverlayType(enemyBoardRangeOverlays, 'brander')" class="legend-item legend-explosion" @mouseenter="showTip($event, 'Радиус подрыва Брандера — стреляйте в него для детонации')" @mousemove="moveTip" @mouseleave="hideTip">
            <span v-html="renderIcon('brander', 12)"></span> Подрыв Брандера
          </span>
        </div>
        <div v-if="enemySummonTrails.size > 0" class="range-legend">
          <span v-if="hasTrailType(enemySummonTrails, 'Ram')" class="legend-item legend-trail-ram"><span v-html="renderIcon('ram', 12)"></span> След: Таран</span>
          <span v-if="hasTrailType(enemySummonTrails, 'Scout')" class="legend-item legend-trail-scout"><span v-html="renderIcon('scout', 12)"></span> След: Разведчик</span>
          <span v-if="hasTrailType(enemySummonTrails, 'Brander')" class="legend-item legend-trail-brander"><span v-html="renderIcon('brander', 12)"></span> След: Брандер</span>
          <span v-if="hasTrailType(enemySummonTrails, 'CursedBoat')" class="legend-item legend-trail-cursed"><span v-html="renderIcon('cursedBoat', 12)"></span> След: Проклятая лодка</span>
          <span v-if="hasTrailType(enemySummonTrails, 'PirateBoat')" class="legend-item legend-trail-pirate"><span v-html="renderIcon('pirateBoat', 12)"></span> След: Пиратская лодка</span>
        </div>
      </div>

      <!-- My Board (overview) -->
      <div class="board-section board-mine" :class="{ 'board-active': isMyTurn }">
        <div class="board-label">
          <span class="player-label">{{ myPlayer?.username ?? 'Вы' }}</span>
          <span v-if="myPlayer" class="faction-label">{{ factionLabel(myPlayer.faction) }}</span>
          <span v-if="myPlayer" class="indicator-badges">
            <span v-if="myPlayer.stunShotExpiry >= store.shotCount" class="bs-badge bs-badge--stun" @mouseenter="showTip($event, 'Оглушён')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('stun', 12)"></span>
            <span v-if="myPlayer.hasPenalty" class="bs-badge bs-badge--penalty" @mouseenter="showTip($event, 'Штраф')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('penalty', 12)"></span>
          </span>
          <span v-if="myPlayer" class="revealed-count bs-mono">Разведано: {{ myPlayer.revealedCellCount }}/100</span>
        </div>
        <div ref="myStageRef" class="board-stage">
          <BoardGrid
            :board="store.myBoard"
            :ships="myFleet"
            :cell-size="34"
            :animated-cells="store.myAnimatedCells"
            :last-shot-cell="myLastShot"
            :summon-trail-cells="mySummonTrails"
            :ship-name-map="myShipNameMap"
            :range-overlay-cells="myBoardRangeOverlays"
            :clickable="!!pendingAssembly || !!pendingManeuver || ((hasCapturedShip || hasEnemySummonOnMyBoard || isGreekFireMode) && canUseOwnBoardWeapon && !boardingPlacementPending)"
            :maneuver-active="!!pendingManeuver || !!pendingAssembly"
            :maneuver-ship-cells="maneuverShipCells"
            :maneuver-target-cells="pendingAssembly ? assemblyTargetCells : maneuverTargetCells"
            :capture-focus="captureFocusActive"
            :capture-ship-cells="capturedShipCells"
            @cell-click="handleMyBoardCellClick"
            @tip-show="showTip" @tip-move="moveTip" @tip-hide="hideTip"
          />
          <VfxCanvas v-if="store.vfxEnabled" ref="myVfxRef" />
        </div>
        <div v-if="myBoardRangeOverlays.size > 0" class="range-legend">
          <span v-if="hasOverlayType(myBoardRangeOverlays, 'poison')" class="legend-item legend-poison" @mouseenter="showTip($event, 'Ядовитый конус — убивает всё в зоне')" @mousemove="moveTip" @mouseleave="hideTip">
            <span v-html="renderIcon('skull', 12)"></span> Яд
          </span>
          <span v-if="hasOverlayType(myBoardRangeOverlays, 'explosion')" class="legend-item legend-explosion" @mouseenter="showTip($event, 'Радиус взрыва — горючая баржа')" @mousemove="moveTip" @mouseleave="hideTip">
            <span v-html="renderIcon('burning', 12)"></span> Взрыв
          </span>
          <span v-if="hasOverlayType(myBoardRangeOverlays, 'freeze')" class="legend-item legend-freeze" @mouseenter="showTip($event, 'Аура заморозки — убивает вражеских призывов')" @mousemove="moveTip" @mouseleave="hideTip">
            <span v-html="renderIcon('frozen', 12)"></span> Заморозка
          </span>
          <span v-if="hasOverlayType(myBoardRangeOverlays, 'ownboard-target')" class="legend-item legend-target" @mouseenter="showTip($event, 'Вражеский призыв — кликните для уничтожения')" @mousemove="moveTip" @mouseleave="hideTip">
            <span v-html="renderIcon('hit', 12)"></span> Вражеский призыв
          </span>
          <span v-if="hasOverlayType(myBoardRangeOverlays, 'penalty-zone')" class="legend-item legend-penalty" @mouseenter="showTip($event, 'Штраф за убийство суммона в этой зоне')" @mousemove="moveTip" @mouseleave="hideTip">
            <span v-html="renderIcon('penalty', 12)"></span> Штрафная зона
          </span>
        </div>
        <div v-if="mySummonTrails.size > 0" class="range-legend">
          <span v-if="hasTrailType(mySummonTrails, 'Ram')" class="legend-item legend-trail-ram"><span v-html="renderIcon('ram', 12)"></span> След: Таран</span>
          <span v-if="hasTrailType(mySummonTrails, 'Scout')" class="legend-item legend-trail-scout"><span v-html="renderIcon('scout', 12)"></span> След: Разведчик</span>
          <span v-if="hasTrailType(mySummonTrails, 'Brander')" class="legend-item legend-trail-brander"><span v-html="renderIcon('brander', 12)"></span> След: Брандер</span>
          <span v-if="hasTrailType(mySummonTrails, 'CursedBoat')" class="legend-item legend-trail-cursed"><span v-html="renderIcon('cursedBoat', 12)"></span> След: Проклятая лодка</span>
          <span v-if="hasTrailType(mySummonTrails, 'PirateBoat')" class="legend-item legend-trail-pirate"><span v-html="renderIcon('pirateBoat', 12)"></span> След: Пиратская лодка</span>
        </div>
      </div>
    </div>
    <ProjectileLayer v-if="store.vfxEnabled" ref="projectileLayerRef" />

    <!-- Mobile Minimap -->
    <div class="minimap-wrapper">
      <div class="minimap-label">{{ myPlayer?.username ?? 'Вы' }}</div>
      <BoardGrid :board="store.myBoard" :ships="myFleet" :animated-cells="store.myAnimatedCells" />
    </div>

    <!-- Bottom Panels -->
    <div class="bottom-panels">
      <FleetPanel v-if="myFleet.length" :fleet="myFleet" :shot-count="store.shotCount" />
      <BattleLogPanel :entries="gameLog" />
    </div>

    <!-- Action Bar -->
    <ActionBar
      :maneuverable-ships="[]"
      :shot-result="store.lastShotResult"
      :shot-result-class="shotResultClass"
      :is-my-turn="isMyTurn"
      :can-pass-boarding="myPlayer?.canPassBoarding ?? false"
      @pass-boarding="store.passBoardingTurn()"
    />

    <!-- Tooltip -->
    <Teleport to="body">
      <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
        {{ tipText }}
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.phase-content { margin-top: 0.5rem; }

.maneuver-banner {
  position: sticky;
  top: 8px;
  z-index: 40;
  margin-bottom: 0.65rem;
  border: 2px solid #86efac;
  box-shadow: 0 0 24px rgba(34, 197, 94, 0.45);
  text-align: center;
  font-weight: 900;
}
.assembly-banner {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.65rem;
  flex-wrap: wrap;
}
.assembly-rotate {
  color: #0f172a;
  border-color: #86efac;
  background: #bbf7d0;
}
.mandatory-lock > :not(.combat-layout):not(.maneuver-banner):not(.pc-tooltip) {
  filter: grayscale(1) brightness(0.48);
  opacity: 0.5;
  pointer-events: none;
}
.maneuver-focus .board-enemy {
  filter: grayscale(1) brightness(0.42);
  opacity: 0.44;
  pointer-events: none;
}
.maneuver-focus .board-mine {
  position: relative;
  z-index: 4;
  padding: 0.55rem;
  border: 2px solid #facc15;
  border-radius: 12px;
  background: color-mix(in srgb, var(--bg-primary) 92%, #facc15);
  box-shadow: 0 0 26px rgba(250, 204, 21, 0.34);
}
.cursed-focus .board-mine {
  filter: grayscale(1) brightness(0.42);
  opacity: 0.44;
  pointer-events: none;
}
.cursed-focus .board-enemy {
  position: relative;
  z-index: 4;
  padding: 0.55rem;
  border: 2px solid #facc15;
  border-radius: 12px;
  background: color-mix(in srgb, var(--bg-primary) 92%, #facc15);
  box-shadow: 0 0 26px rgba(250, 204, 21, 0.34);
}
.capture-attention > :not(.combat-layout):not(.pc-tooltip) {
  filter: grayscale(1) brightness(0.68);
  opacity: 0.68;
}
.capture-focus .board-enemy {
  filter: grayscale(1) brightness(0.52);
  opacity: 0.5;
}
.capture-focus .board-mine .board-label,
.capture-focus .board-mine .range-legend {
  filter: grayscale(1) brightness(0.62);
  opacity: 0.58;
}

.catapult-ready::before {
  content: '';
  position: fixed;
  inset: 7px;
  z-index: 1000;
  pointer-events: none;
  border: 3px solid rgba(255, 255, 255, 0.92);
  border-radius: 16px;
  box-shadow:
    inset 0 0 42px 10px rgba(255, 255, 255, 0.34),
    0 0 36px 12px rgba(255, 255, 255, 0.7);
  animation: catapult-screen-ready 1.1s ease-in-out infinite alternate;
}
.catapult-ready-label {
  position: relative;
  z-index: 2;
  margin: 0.35rem 0 0.5rem;
  padding: 0.45rem 0.75rem;
  color: #0f172a;
  background: rgba(255, 255, 255, 0.94);
  border-radius: 8px;
  text-align: center;
  font-weight: 950;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  box-shadow: 0 0 26px 8px rgba(255, 255, 255, 0.55);
  animation: catapult-label-ready 0.85s ease-in-out infinite alternate;
}
@keyframes catapult-screen-ready {
  from { opacity: 0.42; filter: brightness(0.9); }
  to { opacity: 1; filter: brightness(1.35); }
}
@keyframes catapult-label-ready {
  from { transform: scale(0.99); }
  to { transform: scale(1.015); }
}
@media (prefers-reduced-motion: reduce) {
  .catapult-ready::before,
  .catapult-ready-label { animation: none; }
}

.first-turn-banner {
  font-weight: 700;
  animation: bs-banner-appear 0.5s ease-out;
}

/* ── Shot delay ── */
.shot-delay-banner {
  position: relative;
  overflow: hidden;
}
.delay-countdown { font-weight: 700; font-size: 0.85rem; }
.delay-progress {
  position: absolute;
  bottom: 0;
  left: 0;
  height: 3px;
  background: var(--accent-blue);
  transition: width 0.1s linear;
  border-radius: 0 0 10px 10px;
}

/* ── Board layout ── */
.combat-layout {
  display: flex;
  gap: 1.5rem;
  flex-wrap: wrap;
  justify-content: center;
}
.board-section {
  display: flex;
  flex-direction: column;
  align-items: center;
}
.board-stage {
  position: relative;
}
.board-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.375rem;
}
.player-label {
  font-size: 0.85rem;
  font-weight: 700;
  color: var(--text-primary);
}
.faction-label {
  padding: 1px 7px;
  border: 1px solid color-mix(in srgb, var(--accent-gold) 42%, transparent);
  border-radius: 999px;
  color: var(--accent-gold);
  background: color-mix(in srgb, var(--accent-gold) 9%, transparent);
  font-size: 0.62rem;
  font-weight: 800;
  white-space: nowrap;
}
.revealed-count {
  font-size: 0.68rem;
  color: var(--text-dim);
}

.indicator-badges {
  display: flex;
  gap: 0.25rem;
}

.board-active {
  animation: board-turn-flash 0.6s ease-out;
}
@keyframes board-turn-flash {
  0% { box-shadow: 0 0 12px color-mix(in srgb, var(--accent-gold) 40%, transparent); }
  100% { box-shadow: none; }
}

/* ── Bottom panels ── */
.bottom-panels {
  display: flex;
  gap: 0.75rem;
  margin-top: 0.75rem;
  flex-wrap: wrap;
}
.bottom-panels > * {
  flex: 1;
  min-width: 200px;
}

/* ── Range legend ── */
.range-legend {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.25rem;
  flex-wrap: wrap;
  justify-content: center;
}
.legend-item {
  --legend-color: var(--text-muted);
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 0.6rem;
  font-weight: 600;
  padding: 1px 6px;
  border-radius: 6px;
  cursor: help;
  white-space: nowrap;
  color: var(--legend-color);
  background: color-mix(in srgb, var(--legend-color) 11%, transparent);
  border: 1px solid color-mix(in srgb, var(--legend-color) 35%, transparent);
}
.legend-poison { --legend-color: var(--accent-green); }
.legend-explosion { --legend-color: var(--accent-orange); }
.legend-freeze { --legend-color: var(--accent-blue); }
.legend-target { --legend-color: var(--accent-red); }
.legend-penalty { --legend-color: var(--accent-red); }
.legend-trail-ram { --legend-color: var(--accent-red); }
.legend-trail-scout { --legend-color: var(--accent-blue); }
.legend-trail-brander { --legend-color: var(--accent-orange); }
.legend-trail-cursed { --legend-color: var(--accent-purple); }
.legend-trail-pirate { --legend-color: var(--accent-gold); }

/* ═══════ Weapon cursors ═══════ */
.cursor-ballista { cursor: crosshair; }
.cursor-buckshot {
  cursor: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='24' height='24'%3E%3Ccircle cx='12' cy='12' r='8' fill='none' stroke='%23d4a847' stroke-width='1.5' stroke-dasharray='3 3'/%3E%3Ccircle cx='12' cy='12' r='2' fill='%23d4a847'/%3E%3C/svg%3E") 12 12, crosshair;
}
.cursor-whitestone {
  cursor: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='24' height='24'%3E%3Cline x1='4' y1='4' x2='20' y2='20' stroke='%23c0392b' stroke-width='2.5'/%3E%3Cline x1='20' y1='4' x2='4' y2='20' stroke='%23c0392b' stroke-width='2.5'/%3E%3C/svg%3E") 12 12, crosshair;
}
.cursor-incendiary, .cursor-greekfire {
  cursor: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='24' height='24'%3E%3Cpath d='M12 2c0 6-5 8-5 13a5 5 0 0010 0c0-5-5-7-5-13z' fill='none' stroke='%23e67e22' stroke-width='1.5'/%3E%3C/svg%3E") 12 12, crosshair;
}

/* ═══════ Board shake ═══════ */
.board-shake { animation: shake-boards 150ms ease-out; }
@keyframes shake-boards {
  0% { transform: translateX(0); }
  20% { transform: translateX(-3px); }
  40% { transform: translateX(3px); }
  60% { transform: translateX(-2px); }
  80% { transform: translateX(1px); }
  100% { transform: translateX(0); }
}

/* ═══════ Boarding zoom ═══════ */
.boarding-zoom {
  animation: boarding-scale 1200ms ease-in-out;
}
@keyframes boarding-scale {
  0% { transform: scale(1); }
  20% { transform: scale(0.92); }
  80% { transform: scale(0.92); }
  100% { transform: scale(1); }
}

/* ═══════ Mobile ═══════ */
.minimap-wrapper { display: none; }

@media (max-width: 900px) {
  .combat-layout {
    flex-direction: column;
    align-items: center;
  }
  .bottom-panels {
    flex-direction: column;
  }
}

@media (max-width: 768px) {
  .minimap-wrapper {
    display: block;
    position: fixed;
    bottom: 8px;
    right: 8px;
    z-index: 30;
    transform: scale(0.35);
    transform-origin: bottom right;
    opacity: 0.7;
    border: 1px solid var(--glass-border);
    border-radius: 8px;
    background: var(--bg-primary);
    padding: 4px;
    pointer-events: none;
  }
  .minimap-label {
    font-size: 1.5rem;
    font-weight: 700;
    color: var(--text-muted);
    text-align: center;
    margin-bottom: 2px;
  }
}
</style>
