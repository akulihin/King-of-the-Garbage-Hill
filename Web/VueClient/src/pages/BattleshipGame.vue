<script setup lang="ts">
import { onMounted, onUnmounted, computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useBattleshipStore } from 'src/store/battleship'
import { signalrService } from 'src/services/signalr'
import type { BattleshipPendingSummon } from 'src/services/signalr'
import BoardGrid from 'src/components/battleship/BoardGrid.vue'
import FleetBuilder from 'src/components/battleship/FleetBuilder.vue'
import WeaponBar from 'src/components/battleship/WeaponBar.vue'
import FleetPanel from 'src/components/battleship/FleetPanel.vue'
import BattleLog from 'src/components/battleship/BattleLog.vue'
import SummonBar from 'src/components/battleship/SummonBar.vue'
import ActionBar from 'src/components/battleship/ActionBar.vue'
import VfxCanvas from 'src/components/battleship/VfxCanvas.vue'
import { ICONS, renderIcon } from 'src/components/battleship/battleship-icons'
import { useTip } from 'src/composables/useTip'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const props = defineProps<{ gameId: string }>()

const store = useBattleshipStore()
const router = useRouter()

const phase = computed(() => store.phase)
const isMyTurn = computed(() => store.isMyTurn)
const myPlayer = computed(() => store.myPlayer)
const enemyPlayer = computed(() => store.enemyPlayer)
const myFleet = computed(() => store.myFleet)
const gameLog = computed(() => store.gameLog)

// ── Weapon selection ──────────────────────────────────────────
const selectedWeaponType = ref('Ballista')
const shootableTypes = new Set(['Ballista', 'Catapult', 'Tetracatapult', 'Incendiary', 'GreekFire'])

const availableWeapons = computed(() => {
  if (!myPlayer.value?.fleet) return []
  const weapons: { type: string; shotType: string; label: string; ammo: number; hasAmmo: boolean; shipName: string; shipRange: string; shipRow: number; aimSpeed: number }[] = []
  for (const ship of myPlayer.value.fleet) {
    if (ship.isDestroyed) continue
    for (const w of ship.weapons) {
      if (!shootableTypes.has(w.type)) continue
      if (w.type === 'Tetracatapult') {
        weapons.push({ type: w.type, shotType: 'WhiteStone', label: 'Белый камень', ammo: w.ammo, hasAmmo: w.hasAmmo, shipName: ship.name, shipRange: ship.range, shipRow: ship.row, aimSpeed: w.aimSpeed })
        if (phase.value !== 'Boarding') {
          weapons.push({ type: w.type, shotType: 'Buckshot', label: 'Дробь', ammo: w.ammo, hasAmmo: w.hasAmmo, shipName: ship.name, shipRange: ship.range, shipRow: ship.row, aimSpeed: w.aimSpeed })
        }
      } else {
        const label = w.type === 'Incendiary' ? 'Горючка'
          : w.type === 'GreekFire' ? 'Греческий огонь'
          : w.type
        weapons.push({ type: w.type, shotType: w.type, label, ammo: w.ammo, hasAmmo: w.hasAmmo, shipName: ship.name, shipRange: ship.range, shipRow: ship.row, aimSpeed: w.aimSpeed })
      }
    }
  }
  return weapons
})

const selectedWeaponShip = computed(() => {
  const w = availableWeapons.value.find(w => w.shotType === selectedWeaponType.value)
    ?? availableWeapons.value.find(w => w.type === selectedWeaponType.value)
  return w ?? null
})

const farBlockedRows = computed<Set<number>>(() => {
  const w = selectedWeaponShip.value
  if (w && w.shipRange === 'Far' && w.shipRow >= 8) {
    return new Set([8, 9])
  }
  return new Set()
})

// ── Summon deployment mode ──────────────────────────────────
const summonDeployMode = ref<{
  type: string
  pendingId?: string
  pendingCols?: number[]
} | null>(null)

const summonType = ref('Ram')

const hasBranderUpgrade = computed(() => {
  return myPlayer.value?.fleet?.some(s => !s.isDestroyed && s.abilities?.includes('brander_summon')) ?? false
})

const hasEnemySummonOnMyBoard = computed(() => {
  if (phase.value !== 'Combat' && phase.value !== 'Boarding') return false
  const myId = store.gameState?.myPlayerId
  return store.myBoard?.cells.some(c => c.hasSummon && c.summonOwnerId && c.summonOwnerId !== myId) ?? false
})

const canDeploySummon = computed(() => {
  if (!myPlayer.value || !enemyPlayer.value) return false
  const p = myPlayer.value
  if (p.summonSlotsUsed >= p.maxSummonSlots) return false
  const threshold = 5 * (p.summonSlotsUsed + 1)
  if (phase.value !== 'Boarding' && enemyPlayer.value.revealedCellCount < threshold) return false
  if (phase.value !== 'Boarding' && p.summonCooldownRemaining > 0) return false
  return true
})

function enterSummonDeployMode() {
  if (!canDeploySummon.value) return
  summonDeployMode.value = { type: summonType.value }
}

function enterPendingSummonDeployMode(ps: BattleshipPendingSummon) {
  summonDeployMode.value = {
    type: ps.type,
    pendingId: ps.id,
    pendingCols: ps.allowedColumns.length ? ps.allowedColumns : undefined,
  }
}

function cancelSummonDeploy() {
  summonDeployMode.value = null
}

const summonDeployHighlight = ref<{ row: number; col: number }[]>([])

function updateSummonDeployHighlight(row: number, col: number) {
  if (!summonDeployMode.value) { summonDeployHighlight.value = []; return }
  if (row !== 0) { summonDeployHighlight.value = []; return }
  if (summonDeployMode.value.pendingCols && !summonDeployMode.value.pendingCols.includes(col)) {
    summonDeployHighlight.value = []
    return
  }
  summonDeployHighlight.value = [{ row: 0, col }]
}

// ── Shot delay countdown ────────────────────────────────────
const shotDelayRemaining = ref(0)
let shotDelayTimer: ReturnType<typeof setInterval> | null = null

watch(() => store.shotDelayActive, (active) => {
  if (active) {
    shotDelayRemaining.value = 2.0
    shotDelayTimer = setInterval(() => {
      shotDelayRemaining.value = Math.max(0, +(shotDelayRemaining.value - 0.1).toFixed(1))
      if (shotDelayRemaining.value <= 0 && shotDelayTimer) {
        clearInterval(shotDelayTimer)
        shotDelayTimer = null
      }
    }, 100)
  } else {
    shotDelayRemaining.value = 0
    if (shotDelayTimer) { clearInterval(shotDelayTimer); shotDelayTimer = null }
  }
})

// ── AoE cursor previews ─────────────────────────────────────
const isBuckshotMode = computed(() => store.selectedShotType === 'Buckshot')
const isIncendiaryMode = computed(() => store.selectedShotType === 'Incendiary' || store.selectedShotType === 'GreekFire')
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

// ── Placement ────────────────────────────────────────────────
const placementHoverCell = ref<{ row: number; col: number } | null>(null)

function handlePlacementHover(row: number, col: number) {
  placementHoverCell.value = { row, col }
}

const zoneHighlightRows = computed<number[]>(() => {
  if (phase.value !== 'ShipPlacement' || !store.selectedShipId) return []
  const ship = myFleet.value.find(s => s.id === store.selectedShipId)
  if (!ship) return []
  if (ship.range === 'Tetra' || ship.range === 'Far') return [8, 9]
  return [0, 1, 2, 3, 4, 5, 6, 7]
})

const placementHighlight = computed(() => {
  if (phase.value !== 'ShipPlacement' || !store.selectedShipId || !placementHoverCell.value) return []
  const ship = myFleet.value.find(s => s.id === store.selectedShipId)
  if (!ship) return []
  const { row, col } = placementHoverCell.value
  const orientation = store.placementOrientation
  const cells: { row: number; col: number; valid: boolean }[] = []
  for (let i = 0; i < ship.deckCount; i++) {
    const r = orientation === 'Vertical' ? row + i : row
    const c = orientation === 'Horizontal' ? col + i : col
    cells.push({ row: r, col: c, valid: r >= 0 && r < 10 && c >= 0 && c < 10 })
  }
  return cells.filter(c => c.row >= 0 && c.row < 10 && c.col >= 0 && c.col < 10)
})

const placementSpaceHighlight = computed(() => {
  if (phase.value !== 'ShipPlacement' || !store.selectedShipId || !placementHoverCell.value) return []
  const ship = myFleet.value.find(s => s.id === store.selectedShipId)
  if (!ship) return []
  const space = ship.space ?? 1
  const { row, col } = placementHoverCell.value
  const orientation = store.placementOrientation
  const shipCells = new Set<string>()
  for (let i = 0; i < ship.deckCount; i++) {
    const r = orientation === 'Vertical' ? row + i : row
    const c = orientation === 'Horizontal' ? col + i : col
    shipCells.add(`${r},${c}`)
  }
  const zoneCells = new Set<string>()
  for (let i = 0; i < ship.deckCount; i++) {
    const dr = orientation === 'Vertical' ? row + i : row
    const dc = orientation === 'Horizontal' ? col + i : col
    for (let sr = -space; sr <= space; sr++) {
      for (let sc = -space; sc <= space; sc++) {
        const r = dr + sr
        const c = dc + sc
        const key = `${r},${c}`
        if (r >= 0 && r < 10 && c >= 0 && c < 10 && !shipCells.has(key)) {
          zoneCells.add(key)
        }
      }
    }
  }
  return [...zoneCells].map(k => {
    const [r, c] = k.split(',').map(Number)
    return { row: r, col: c }
  })
})

const summonDeployAllowedCols = computed<{ row: number; col: number }[]>(() => {
  if (!summonDeployMode.value) return []
  const cols = summonDeployMode.value.pendingCols
  if (!cols) return Array.from({ length: 10 }, (_, i) => ({ row: 0, col: i }))
  return cols.map(c => ({ row: 0, col: c }))
})

const enemyHighlight = computed(() => {
  if (summonDeployMode.value) {
    const allowed = summonDeployAllowedCols.value
    return [...allowed, ...summonDeployHighlight.value]
  }
  if (isBuckshotMode.value || isIncendiaryMode.value) return aoeHighlight.value
  return []
})

// ── Keyboard shortcuts ───────────────────────────────────────
function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && summonDeployMode.value) {
    cancelSummonDeploy()
    e.preventDefault()
    return
  }
  if (phase.value !== 'Combat' && phase.value !== 'Boarding') return
  if (e.key === '1') { handleWeaponSelect('Ballista', 'Ballista'); return }
  const specials = availableWeapons.value.filter(w => w.type !== 'Ballista')
  const idx = parseInt(e.key) - 2
  if (idx >= 0 && idx < specials.length) {
    const w = specials[idx]
    if (w.hasAmmo && w.aimSpeed <= 0) handleWeaponSelect(w.type, w.shotType)
  }
}

// ── Lifecycle ────────────────────────────────────────────────
onMounted(async () => {
  store.initCallbacks()
  window.addEventListener('keydown', handleKeydown)
  await signalrService.joinBattleshipGame(props.gameId)
  if (!store.gameState) {
    await signalrService.requestBattleshipState(props.gameId)
  }
})

onUnmounted(async () => {
  window.removeEventListener('keydown', handleKeydown)
  if (shotDelayTimer) clearInterval(shotDelayTimer)
  await signalrService.leaveBattleshipGame(props.gameId)
  store.cleanupCallbacks()
})

// ── Handlers ─────────────────────────────────────────────────
async function handleReady() { await store.confirmReady() }

function selectShipForPlacement(shipId: string) {
  store.selectedShipId = shipId
}

async function handlePlacementClick(row: number, col: number) {
  if (!store.selectedShipId) return
  await store.placeShip(store.selectedShipId, row, col, store.placementOrientation)
  store.selectedShipId = null
}

function handlePlacementWheel(_e: WheelEvent) {
  store.toggleOrientation()
}

async function handleConfirmPlacement() {
  await store.confirmPlacement()
}

async function handleEnemyCellClick(row: number, col: number) {
  if (summonDeployMode.value) {
    if (row !== 0) return
    const mode = summonDeployMode.value
    if (mode.pendingCols && !mode.pendingCols.includes(col)) return
    if (mode.pendingId) {
      await store.deployPendingSummon(mode.pendingId, col)
    } else {
      await store.deploySummon(mode.type, col)
    }
    summonDeployMode.value = null
    return
  }
  if (!isMyTurn.value || (phase.value !== 'Combat' && phase.value !== 'Boarding')) return
  if (myPlayer.value?.pendingSummons?.some(p => p.isBoarding)) return
  if (store.shotDelayActive) return
  if (farBlockedRows.value.has(row)) return
  const cell = store.enemyBoard?.cells.find(c => c.row === row && c.col === col)
  if (cell && (cell.isHit || cell.isMiss || cell.isRevealed)) {
    const incendiaryRetarget = store.selectedShotType === 'Incendiary' && cell.isHit && cell.hasShip
    const scratchedRetarget = cell.isScratched && cell.hasShip
    if (!incendiaryRetarget && !scratchedRetarget) return
  }
  await store.shoot(row, col)
}

async function handleMyBoardCellClick(row: number, col: number) {
  if (!isMyTurn.value || (phase.value !== 'Combat' && phase.value !== 'Boarding')) return
  if (store.shotDelayActive) return
  const myId = store.gameState?.myPlayerId
  const cell = store.myBoard?.cells.find(c => c.row === row && c.col === col)
  if (!cell || !cell.hasSummon || !cell.summonOwnerId || cell.summonOwnerId === myId) return
  await store.shootOwnBoard(row, col)
}

function handleEnemyHover(row: number, col: number) {
  if (summonDeployMode.value) updateSummonDeployHighlight(row, col)
  else updateAoeHighlight(row, col)
}

async function handleWeaponSelect(weaponType: string, shotType: string) {
  selectedWeaponType.value = weaponType
  cancelSummonDeploy()
  await store.selectWeapon(weaponType, shotType)
}

async function handleManualMove(shipId: string, direction: string, distance: number) {
  if (!store.gameId) return
  await signalrService.battleshipManualMove(store.gameId, shipId, direction, distance)
}

async function handleForfeit() {
  if (!confirm('Вы уверены, что хотите сдаться?')) return
  await store.forfeit()
}

async function handleLeave() {
  await store.leaveWebGame(props.gameId)
  router.push('/battleship')
}

// ── Computed for sub-components ──────────────────────────────
const firstTurnBanner = computed(() => {
  if (phase.value !== 'Combat' && phase.value !== 'Boarding') return null
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
    'shot-dodge': r.scratched && r.miss,
    'shot-sunk': r.shipSunk,
    'shot-burn': r.burned,
    'shot-destroy': r.destroyed && !r.shipSunk,
  }
})

const isWin = computed(() => store.gameState?.winnerId === myPlayer.value?.discordId)
const winnerName = computed(() => {
  if (!store.gameState?.winnerId) return ''
  if (store.gameState.player1?.discordId === store.gameState.winnerId) return store.gameState.player1?.username ?? ''
  return store.gameState.player2?.username ?? ''
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

const enemySummonTrails = computed(() => store.getSummonTrailCells('enemy'))
const mySummonTrails = computed(() => store.getSummonTrailCells('my'))

const killStreakLabel = computed(() => {
  const k = store.killStreakDisplay
  if (k < 2) return null
  if (k === 2) return 'Двойное попадание!'
  if (k === 3) return 'Тройное попадание!'
  if (k === 4) return 'ЧЕТВЁРКА!'
  return `${k}x КОМБО!`
})

const enemyFleetIntel = computed(() => {
  if (!enemyPlayer.value?.fleet) return []
  return enemyPlayer.value.fleet.filter(s => {
    return s.isDestroyed || s.decks.some(d => d.isDestroyed || d.currentHp < d.maxHp)
  })
})

function handleEnemyRightClick(row: number, col: number) {
  store.toggleMarkedCell(row, col)
}

// ── Range overlays ───────────────────────────────────────────
const myBoardRangeOverlays = computed(() => {
  const map = new Map<string, string>()
  if (phase.value !== 'Combat' && phase.value !== 'Boarding') return map
  if (!myFleet.value) return map

  for (const ship of myFleet.value) {
    if (ship.isDestroyed || !ship.isPlaced) continue
    const abilities = ship.abilities ?? []

    if (abilities.includes('poison_cone')) {
      const baseRow = ship.row
      const baseCol = ship.col
      for (let dc = -1; dc <= 1; dc++) addCell(map, baseRow - 1, baseCol + dc, 'poison')
      for (let dc = -2; dc <= 2; dc++) addCell(map, baseRow - 2, baseCol + dc, 'poison')
      if (ship.definitionId === 'alchi_iceberg') {
        for (let dc = -3; dc <= 3; dc++) addCell(map, baseRow - 3, baseCol + dc, 'poison')
      }
    }

    if (abilities.includes('explode_on_hit')) {
      const radius = ship.definitionId === 'incendiary_barge' ? 2 : (ship.space ?? 1)
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

  return map
})

const enemyBoardRangeOverlays = computed(() => {
  const map = new Map<string, string>()
  if (phase.value !== 'Combat' && phase.value !== 'Boarding') return map

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

function getOccupiedCells(ship: { row: number; col: number; deckCount: number; orientation: string }): [number, number][] {
  const cells: [number, number][] = []
  for (let i = 0; i < ship.deckCount; i++) {
    const r = ship.orientation === 'Vertical' ? ship.row + i : ship.row
    const c = ship.orientation === 'Horizontal' ? ship.col + i : ship.col
    cells.push([r, c])
  }
  return cells
}

// ── Maneuverable ships for ActionBar ─────────────────────────
const maneuverableShips = computed(() => {
  if (!isMyTurn.value || !myPlayer.value || myPlayer.value.maneuveringDoubleUsed) return []
  return myFleet.value
    .filter(s => s.abilities.includes('manual_move_after_hit') && !s.isDestroyed && s.decks.some(d => d.isDestroyed))
    .map(s => ({ id: s.id, name: s.name, orientation: s.orientation }))
})

const cursedBoatSummons = computed(() => {
  return (myPlayer.value?.summons?.filter(s => s.waitingForDirectionChoice) ?? [])
    .map(s => ({ id: s.id, waitingForDirectionChoice: true }))
})

// ── VFX: Weapon cursor ──────────────────────────────────
const weaponCursorClass = computed(() => {
  if (phase.value !== 'Combat' && phase.value !== 'Boarding') return ''
  if (!isMyTurn.value) return ''
  switch (store.selectedShotType) {
    case 'Buckshot': return 'cursor-buckshot'
    case 'WhiteStone': return 'cursor-whitestone'
    case 'Incendiary': return 'cursor-incendiary'
    case 'GreekFire': return 'cursor-greekfire'
    default: return 'cursor-ballista'
  }
})

// ── VFX: Turn transition sweep ──────────────────────────
const turnTransitionActive = ref(false)

watch(isMyTurn, (val) => {
  if (val && (phase.value === 'Combat' || phase.value === 'Boarding')) {
    turnTransitionActive.value = true
    setTimeout(() => { turnTransitionActive.value = false }, 800)
  }
})

// ── VFX: Boarding cinematic ─────────────────────────────
const boardingCinematicActive = ref(false)

watch(phase, (val) => {
  if (val === 'Boarding') {
    boardingCinematicActive.value = true
    setTimeout(() => { boardingCinematicActive.value = false }, 1200)
  }
})

// ── VFX: Victory sequence ───────────────────────────────
const victorySequenceActive = ref(false)
const confettiParticles = ref<{ id: number; x: number; color: string; delay: number; rotation: number }[]>([])

watch(phase, (val) => {
  if (val === 'GameOver') {
    victorySequenceActive.value = true
    if (isWin.value) {
      const clrs = ['#f0c850', '#ff4444', '#6eaaf0', '#3fa73d', '#ff6600', '#c088ff']
      confettiParticles.value = Array.from({ length: 25 }, (_, i) => ({
        id: i, x: Math.random() * 100,
        color: clrs[Math.floor(Math.random() * clrs.length)],
        delay: Math.random() * 2, rotation: Math.random() * 720 - 360,
      }))
    }
  }
})
</script>

<template>
  <div class="bs-game bs-pirate">
    <!-- Header -->
    <div class="game-header">
      <div class="header-left">
        <RouterLink to="/battleship" class="btn-pirate btn-sm">Назад</RouterLink>
        <span class="game-tag">#{{ store.gameId }}</span>
        <span class="phase-badge" :class="'phase-' + phase.toLowerCase()">{{ phase }}</span>
      </div>
      <div class="header-right">
        <template v-if="phase === 'Combat' || phase === 'Boarding'">
          <span class="turn-badge" @mouseenter="showTip($event, 'Номер текущего хода')" @mousemove="moveTip" @mouseleave="hideTip">Ход {{ store.turnNumber }}</span>
          <span class="turn-badge" @mouseenter="showTip($event, 'Общий счётчик выстрелов в матче')" @mousemove="moveTip" @mouseleave="hideTip">Выстрел {{ store.shotCount }}</span>
          <span class="turn-indicator" :class="{ 'my-turn': isMyTurn }">
            {{ isMyTurn ? 'Ваш ход' : 'Ход противника' }}
          </span>
          <button class="btn-pirate btn-sm forfeit-btn" @mouseenter="showTip($event, 'Сдаться и проиграть матч')" @mousemove="moveTip" @mouseleave="hideTip" @click="handleForfeit">Сдаться</button>
        </template>
      </div>
    </div>

    <!-- Lobby Phase -->
    <div v-if="phase === 'Lobby'" class="phase-content">
      <div class="card-parchment centered-card">
        <h3 class="card-title bs-font-title">Ожидание противника...</h3>
        <p class="card-subtitle bs-font-body">{{ myPlayer?.username ?? '' }} vs {{ enemyPlayer?.username ?? 'Бот' }}</p>
        <button class="btn-pirate btn-lg" @click="handleReady">Начать игру</button>
        <button class="btn-pirate btn-sm leave-btn" @click="handleLeave">Выйти</button>
      </div>
    </div>

    <!-- Army Selection Phase -->
    <div v-else-if="phase === 'ArmySelection'" class="phase-content">
      <div class="card-parchment centered-card">
        <h3 class="card-title bs-font-title">Выберите армию</h3>
        <div class="army-options">
          <button class="btn-pirate btn-lg" @click="store.selectArmy('Empire')">Империя</button>
          <button class="btn-pirate btn-lg" disabled @mouseenter="showTip($event, 'Скоро')" @mousemove="moveTip" @mouseleave="hideTip">Альянс (скоро)</button>
        </div>
      </div>
    </div>

    <!-- Fleet Building Phase -->
    <div v-else-if="phase === 'FleetBuilding'" class="phase-content">
      <FleetBuilder />
    </div>

    <!-- Ship Placement Phase -->
    <div v-else-if="phase === 'ShipPlacement'" class="phase-content">
      <div class="placement-layout">
        <div class="placement-board">
          <h4 class="section-label bs-font-title">Ваше поле</h4>
          <BoardGrid
            :board="store.myBoard"
            :ships="myFleet"
            :is-placement="true"
            :clickable="!!store.selectedShipId"
            :highlight-cells="placementHighlight"
            :space-highlight-cells="placementSpaceHighlight"
            :zone-highlight-rows="zoneHighlightRows"
            @cell-click="handlePlacementClick"
            @cell-hover="handlePlacementHover"
            @wheel.prevent="handlePlacementWheel"
            @tip-show="showTip" @tip-move="moveTip" @tip-hide="hideTip"
          />
        </div>

        <div class="placement-controls">
          <h4 class="section-label bs-font-title">Расстановка кораблей</h4>
          <button class="btn-pirate btn-sm orientation-btn" @click="store.toggleOrientation()">
            Повернуть ({{ store.placementOrientation === 'Horizontal' ? 'горизонт.' : 'вертик.' }})
          </button>

          <div class="ship-list">
            <div
              v-for="ship in myFleet"
              :key="ship.id"
              class="card-wood ship-item"
              :class="{ 'ship-selected': store.selectedShipId === ship.id, 'ship-placed': ship.isPlaced }"
              @click="selectShipForPlacement(ship.id)"
            >
              <span class="ship-name">{{ ship.name }}</span>
              <span class="ship-decks bs-font-data">{{ ship.deckCount }}П</span>
              <span v-if="ship.isPlaced" class="placed-mark">Размещён</span>
            </div>
          </div>

          <button
            class="btn-pirate btn-lg"
            :disabled="myFleet.some(s => !s.isPlaced)"
            @mouseenter="showTip($event, myFleet.some(s => !s.isPlaced) ? `Не размещено: ${myFleet.filter(s => !s.isPlaced).map(s => s.name).join(', ')}` : 'Подтвердить и начать бой')"
            @mousemove="moveTip" @mouseleave="hideTip"
            @click="handleConfirmPlacement"
          >
            Подтвердить расстановку
          </button>
        </div>
      </div>
    </div>

    <!-- Combat / Boarding Phase -->
    <div v-else-if="phase === 'Combat' || phase === 'Boarding'" class="phase-content">
      <div v-if="firstTurnBanner" class="first-turn-banner bs-font-body">{{ firstTurnBanner }}</div>
      <div v-if="phase === 'Boarding'" class="boarding-banner bs-font-body">Абордаж! Близкие корабли идут на таран.</div>

      <!-- Weapon Bar -->
      <WeaponBar
        :selected-shot-type="store.selectedShotType"
        :available-weapons="availableWeapons"
        :shot-delay-active="store.shotDelayActive"
        :shot-delay-remaining="shotDelayRemaining"
        :phase="phase"
        @select-weapon="handleWeaponSelect"
      />

      <!-- Status Banners -->
      <div v-if="myPlayer?.pendingSummons?.some(p => p.isBoarding)" class="status-banner status-warning">
        Разместите все абордажные корабли перед выстрелом!
      </div>
      <div v-if="myPlayer?.hasPenalty" class="status-banner status-warning">
        Штраф: следующий ход будет пропущен!
      </div>
      <div v-if="myPlayer && store.gameState && myPlayer.stunShotExpiry >= store.gameState.shotCount" class="status-banner status-warning">
        Оглушение! Вы пропускаете ход.
      </div>
      <div v-if="store.shotDelayActive" class="status-banner status-info shot-delay-banner">
        Прицеливание... <span class="delay-countdown bs-font-data">{{ shotDelayRemaining.toFixed(1) }}с</span>
        <div class="delay-progress" :style="{ width: ((2 - shotDelayRemaining) / 2 * 100) + '%' }"></div>
      </div>
      <div v-if="summonDeployMode" class="status-banner status-info summon-deploy-banner">
        Выберите клетку на первой строчке вражеского поля для размещения {{ summonDeployMode.type }}
        <span v-if="summonDeployMode.pendingCols" class="deploy-cols bs-font-data">
          (столбцы: {{ summonDeployMode.pendingCols.map(c => String.fromCharCode(65 + c)).join(', ') }})
        </span>
        <button class="btn-pirate btn-sm cancel-deploy-btn" @click="cancelSummonDeploy">Отмена</button>
      </div>

      <!-- Battle Boards -->
      <div class="combat-layout" :class="{ 'board-shake': store.screenShake, 'boarding-zoom': boardingCinematicActive }">
        <!-- Enemy Board (primary) -->
        <div class="board-section board-enemy" :class="[{ 'board-active': !isMyTurn }, weaponCursorClass]">
          <div class="board-label">
            <span class="player-label bs-font-body">{{ enemyPlayer?.username ?? 'Противник' }}</span>
            <span v-if="enemyPlayer" class="indicator-badges">
              <span v-if="enemyPlayer.stunShotExpiry >= store.shotCount" class="bs-badge bs-badge--stun" @mouseenter="showTip($event, 'Оглушён')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('stun', 12)"></span>
              <span v-if="enemyPlayer.hasPenalty" class="bs-badge bs-badge--penalty" @mouseenter="showTip($event, 'Штраф')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('penalty', 12)"></span>
              <span v-if="phase === 'Boarding'" class="bs-badge bs-badge--boarding" @mouseenter="showTip($event, 'Абордаж')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('crossbones', 12)"></span>
            </span>
            <span v-if="enemyPlayer" class="revealed-count bs-font-data">Разведано: {{ enemyPlayer.revealedCellCount }}/100</span>
          </div>
          <BoardGrid
            :board="store.enemyBoard"
            :is-enemy="true"
            :cell-size="42"
            :shot-type="store.selectedShotType"
            :clickable="(isMyTurn && !store.shotDelayActive) || !!summonDeployMode"
            :highlight-cells="enemyHighlight"
            :blocked-rows="farBlockedRows"
            :animated-cells="store.enemyAnimatedCells"
            :last-shot-cell="enemyLastShot"
            :marked-cells="store.markedCells"
            :summon-trail-cells="enemySummonTrails"
            :ship-name-map="enemyShipNameMap"
            :range-overlay-cells="enemyBoardRangeOverlays"
            @cell-click="handleEnemyCellClick"
            @cell-hover="handleEnemyHover"
            @cell-right-click="handleEnemyRightClick"
            @tip-show="showTip" @tip-move="moveTip" @tip-hide="hideTip"
          />
          <div v-if="enemyBoardRangeOverlays.size > 0" class="range-legend">
            <span v-if="hasOverlayType(enemyBoardRangeOverlays, 'brander')" class="legend-item legend-explosion" @mouseenter="showTip($event, 'Радиус подрыва Брандера — стреляйте в него для детонации')" @mousemove="moveTip" @mouseleave="hideTip">
              <span v-html="renderIcon('brander', 12)"></span> Подрыв Брандера
            </span>
          </div>
          <div v-if="enemySummonTrails.size > 0" class="range-legend">
            <span v-if="hasOverlayType(enemySummonTrails, 'Ram')" class="legend-item legend-trail-ram"><span v-html="renderIcon('ram', 12)"></span> Таран</span>
            <span v-if="hasOverlayType(enemySummonTrails, 'Scout')" class="legend-item legend-trail-scout"><span v-html="renderIcon('scout', 12)"></span> Разведчик</span>
            <span v-if="hasOverlayType(enemySummonTrails, 'Brander')" class="legend-item legend-trail-brander"><span v-html="renderIcon('brander', 12)"></span> Брандер</span>
            <span v-if="hasOverlayType(enemySummonTrails, 'CursedBoat')" class="legend-item legend-trail-cursed"><span v-html="renderIcon('cursedBoat', 12)"></span> Проклятый</span>
            <span v-if="hasOverlayType(enemySummonTrails, 'PirateBoat')" class="legend-item legend-trail-pirate"><span v-html="renderIcon('pirateBoat', 12)"></span> Пират</span>
          </div>
        </div>

        <!-- My Board (overview) -->
        <div class="board-section board-mine" :class="{ 'board-active': isMyTurn }">
          <div class="board-label">
            <span class="player-label bs-font-body">{{ myPlayer?.username ?? 'Вы' }}</span>
            <span v-if="myPlayer" class="indicator-badges">
              <span v-if="myPlayer.stunShotExpiry >= store.shotCount" class="bs-badge bs-badge--stun" @mouseenter="showTip($event, 'Оглушён')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('stun', 12)"></span>
              <span v-if="myPlayer.hasPenalty" class="bs-badge bs-badge--penalty" @mouseenter="showTip($event, 'Штраф')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('penalty', 12)"></span>
            </span>
            <span v-if="myPlayer" class="revealed-count bs-font-data">Разведано: {{ myPlayer.revealedCellCount }}/100</span>
          </div>
          <BoardGrid
            :board="store.myBoard"
            :ships="myFleet"
            :cell-size="34"
            :animated-cells="store.myAnimatedCells"
            :last-shot-cell="myLastShot"
            :summon-trail-cells="mySummonTrails"
            :ship-name-map="myShipNameMap"
            :range-overlay-cells="myBoardRangeOverlays"
            :clickable="hasEnemySummonOnMyBoard && isMyTurn && !store.shotDelayActive"
            @cell-click="handleMyBoardCellClick"
            @tip-show="showTip" @tip-move="moveTip" @tip-hide="hideTip"
          />
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
          </div>
          <div v-if="mySummonTrails.size > 0" class="range-legend">
            <span v-if="hasOverlayType(mySummonTrails, 'Ram')" class="legend-item legend-trail-ram"><span v-html="renderIcon('ram', 12)"></span> Таран</span>
            <span v-if="hasOverlayType(mySummonTrails, 'Scout')" class="legend-item legend-trail-scout"><span v-html="renderIcon('scout', 12)"></span> Разведчик</span>
            <span v-if="hasOverlayType(mySummonTrails, 'Brander')" class="legend-item legend-trail-brander"><span v-html="renderIcon('brander', 12)"></span> Брандер</span>
            <span v-if="hasOverlayType(mySummonTrails, 'CursedBoat')" class="legend-item legend-trail-cursed"><span v-html="renderIcon('cursedBoat', 12)"></span> Проклятый</span>
            <span v-if="hasOverlayType(mySummonTrails, 'PirateBoat')" class="legend-item legend-trail-pirate"><span v-html="renderIcon('pirateBoat', 12)"></span> Пират</span>
          </div>
        </div>
      </div>

      <!-- Kill Streak Popup -->
      <Transition name="streak-pop">
        <div v-if="killStreakLabel" class="kill-streak" :key="store.killStreakDisplay"
          :class="{ 'streak-2x': store.killStreakDisplay >= 2, 'streak-3x': store.killStreakDisplay >= 3, 'streak-4x': store.killStreakDisplay >= 4 }">
          {{ killStreakLabel }}
        </div>
      </Transition>

      <!-- Mobile Minimap -->
      <div class="minimap-wrapper">
        <div class="minimap-label bs-font-body">{{ myPlayer?.username ?? 'Вы' }}</div>
        <BoardGrid :board="store.myBoard" :ships="myFleet" :animated-cells="store.myAnimatedCells" />
      </div>

      <!-- Bottom Panels -->
      <div class="bottom-panels">
        <FleetPanel v-if="myFleet.length" :fleet="myFleet" :shot-count="store.shotCount" />
        <FleetPanel v-if="enemyFleetIntel.length" :fleet="enemyFleetIntel" :is-enemy="true" :shot-count="store.shotCount" />
        <BattleLog :entries="gameLog" />
      </div>

      <!-- Summon Bar -->
      <SummonBar
        :my-player="myPlayer"
        :phase="phase"
        :shot-count="store.shotCount"
        :can-deploy-summon="canDeploySummon"
        :has-brander-upgrade="hasBranderUpgrade"
        :summon-deploy-mode="summonDeployMode"
        @enter-deploy="enterSummonDeployMode"
        @enter-pending-deploy="enterPendingSummonDeployMode"
        @cancel-deploy="cancelSummonDeploy"
        @set-summon-type="(t: string) => summonType = t"
      />

      <!-- Action Bar -->
      <ActionBar
        :maneuverable-ships="maneuverableShips"
        :cursed-boat-summons="cursedBoatSummons"
        :shot-result="store.lastShotResult"
        :shot-result-class="shotResultClass"
        :is-my-turn="isMyTurn"
        :maneuvering-double-used="myPlayer?.maneuveringDoubleUsed ?? true"
        @manual-move="handleManualMove"
        @set-cursed-direction="(id: string, dir: string) => store.setCursedBoatDirection(id, dir)"
      />
    </div>

    <!-- Game Over Phase -->
    <div v-else-if="phase === 'GameOver'" class="phase-content">
      <div class="victory-banner" :class="{ 'is-win': isWin }">
        <div class="victory-icon" v-html="renderIcon(isWin ? 'anchor' : 'skull', 48)"></div>
        <h2 class="victory-title bs-font-title" :class="isWin ? 'win-text' : 'lose-text'">
          {{ isWin ? 'Победа!' : 'Поражение' }}
        </h2>
        <p class="winner-name bs-font-body">{{ winnerName }}</p>
      </div>

      <div class="combat-layout gameover-boards" :class="{ 'victory-active': victorySequenceActive }">
        <div class="board-section" :class="{ 'gameover-enemy-board': victorySequenceActive }">
          <div class="board-label">
            <span class="player-label bs-font-body">{{ enemyPlayer?.username ?? 'Противник' }}</span>
          </div>
          <BoardGrid :board="store.enemyBoard" :is-enemy="true" :cell-size="38" :animated-cells="store.enemyAnimatedCells" />
        </div>
        <div class="board-section" :class="{ 'gameover-winner-board': victorySequenceActive && isWin }">
          <div class="board-label">
            <span class="player-label bs-font-body">{{ myPlayer?.username ?? 'Вы' }}</span>
          </div>
          <BoardGrid :board="store.myBoard" :ships="myFleet" :cell-size="38" :animated-cells="store.myAnimatedCells" />
        </div>
      </div>

      <BattleLog :entries="gameLog" max-height="300px" />

      <div class="gameover-action">
        <RouterLink to="/battleship" class="btn-pirate btn-lg">Вернуться в лобби</RouterLink>
      </div>
    </div>

    <!-- Loading -->
    <div v-else class="phase-content">
      <div class="loading bs-font-body">Загрузка игры...</div>
    </div>

    <!-- Phase Transition Overlay -->
    <Transition name="phase-transition">
      <div v-if="store.phaseTransitionActive" class="phase-overlay" :key="phase">
        <div class="phase-overlay-text bs-font-title">{{ phase === 'Combat' ? 'К бою!' : phase === 'Boarding' ? 'Абордаж!' : phase === 'ShipPlacement' ? 'Расстановка' : phase === 'GameOver' ? 'Конец игры' : phase }}</div>
      </div>
    </Transition>

    <!-- Turn Transition Sweep -->
    <Transition name="turn-sweep">
      <div v-if="turnTransitionActive" class="turn-sweep-overlay" :key="'turn-' + store.turnNumber">
        <div class="turn-sweep-band"></div>
        <div class="turn-sweep-text bs-font-title">ВАШ ХОД!</div>
      </div>
    </Transition>

    <!-- Boarding Cinematic -->
    <Transition name="boarding-cine">
      <div v-if="boardingCinematicActive" class="boarding-cine-overlay">
        <div class="boarding-cine-slash"></div>
        <div class="boarding-cine-text bs-font-title">АБОРДАЖ!</div>
      </div>
    </Transition>

    <!-- Kill Streak Screen Flash -->
    <Transition name="streak-flash">
      <div v-if="store.killStreakDisplay >= 4" class="streak-screen-flash" :key="'flash-' + store.killStreakDisplay" />
    </Transition>

    <!-- Victory Confetti -->
    <div v-if="confettiParticles.length" class="confetti-container">
      <div v-for="c in confettiParticles" :key="c.id" class="confetti-piece"
        :style="{ left: c.x + '%', background: c.color, animationDelay: c.delay + 's', '--c-rot': c.rotation + 'deg' }"
      />
    </div>

    <!-- Error Toast -->
    <Transition name="toast-fade">
      <div v-if="store.errorMessage" class="error-toast">{{ store.errorMessage }}</div>
    </Transition>

    <!-- Tooltip -->
    <Teleport to="body">
      <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
        {{ tipText }}
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
@import 'src/components/battleship/battleship-theme.css';

.bs-game {
  max-width: 1100px;
  margin: 0 auto;
  background: var(--bs-sea-deep, #0a1628);
  min-height: 100vh;
  padding: 0.5rem;
}

/* ═══════ Header ═══════ */
.game-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  padding: 0.5rem 0.75rem;
  background: var(--bs-wood-dark, #2a1a0e);
  background-image: repeating-linear-gradient(88deg, transparent, transparent 8px, rgba(74, 47, 26, 0.3) 8px, rgba(74, 47, 26, 0.3) 9px);
  border: 2px solid var(--bs-wood-mid, #4a2f1a);
  border-radius: 4px;
}
.header-left, .header-right {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}
.game-tag {
  font-family: "JetBrains Mono", monospace;
  color: var(--bs-parchment-dim, #b09a78);
  font-size: 0.8rem;
}

/* ── Phase badges ── */
.phase-badge {
  font-size: 0.7rem;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 4px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-family: "Crimson Text", serif;
}
.phase-lobby { background: rgba(41, 128, 185, 0.2); color: var(--bs-ocean-blue, #2980b9); }
.phase-armyselection,
.phase-fleetbuilding { background: rgba(212, 168, 71, 0.2); color: var(--bs-gold, #d4a847); }
.phase-shipplacement { background: rgba(39, 174, 96, 0.2); color: var(--bs-poison-green, #27ae60); }
.phase-combat,
.phase-boarding { background: rgba(192, 57, 43, 0.2); color: var(--bs-fire-red, #c0392b); }
.phase-gameover { background: rgba(176, 154, 120, 0.15); color: var(--bs-parchment-dim, #b09a78); }

/* ── Turn info ── */
.turn-badge {
  font-family: "JetBrains Mono", monospace;
  font-size: 0.75rem;
  color: var(--bs-parchment-dim, #b09a78);
}
.turn-indicator {
  font-size: 0.8rem;
  font-weight: 700;
  padding: 2px 10px;
  border-radius: 4px;
  color: var(--bs-parchment-dim, #b09a78);
  background: var(--bs-wood-dark, #2a1a0e);
  border: 1px solid var(--bs-wood-mid, #4a2f1a);
  font-family: "Crimson Text", serif;
  transition: all 0.15s;
}
.turn-indicator.my-turn {
  color: var(--bs-gold-bright, #f0c850);
  background: rgba(212, 168, 71, 0.15);
  border-color: var(--bs-gold, #d4a847);
  box-shadow: 0 0 8px rgba(212, 168, 71, 0.3);
}
.forfeit-btn {
  color: var(--bs-fire-red, #c0392b) !important;
  font-size: 0.7rem;
}

/* ═══════ Phase content ═══════ */
.phase-content { margin-top: 0.5rem; }

/* ── Centered card ── */
.centered-card {
  text-align: center;
  padding: 2rem;
  max-width: 400px;
  margin: 2rem auto;
}
.card-title {
  color: var(--bs-gold, #d4a847);
  margin: 0 0 0.5rem;
  font-size: 1.5rem;
}
.card-subtitle {
  color: var(--bs-parchment-dim, #b09a78);
  margin: 0 0 1rem;
}
.army-options {
  display: flex;
  gap: 1rem;
  justify-content: center;
  margin-top: 1rem;
}
.leave-btn { margin-top: 0.5rem; opacity: 0.7; }

/* ═══════ Placement ═══════ */
.placement-layout {
  display: flex;
  gap: 1.5rem;
  flex-wrap: wrap;
}
.section-label {
  margin: 0 0 0.5rem;
  font-size: 1rem;
  color: var(--bs-gold, #d4a847);
}
.placement-controls {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-width: 200px;
}
.orientation-btn { margin-bottom: 0.25rem; }
.ship-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.ship-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.375rem 0.5rem;
  cursor: pointer;
  transition: all 0.15s;
}
.ship-item:hover { border-color: var(--bs-wood-light, #6b4226); }
.ship-selected { border-color: var(--bs-gold, #d4a847) !important; box-shadow: 0 0 8px rgba(212, 168, 71, 0.3); }
.ship-placed { opacity: 0.6; }
.ship-name { font-weight: 700; color: var(--bs-parchment, #e8d5b0); font-size: 0.85rem; font-family: "Crimson Text", serif; }
.ship-decks { color: var(--bs-parchment-dim, #b09a78); font-size: 0.75rem; }
.placed-mark { font-size: 0.65rem; color: var(--bs-poison-green, #27ae60); font-weight: 700; margin-left: auto; }

/* ═══════ Combat ═══════ */

.first-turn-banner {
  text-align: center;
  padding: 0.5rem 1rem;
  margin-bottom: 0.5rem;
  border-radius: 4px;
  font-size: 0.9rem;
  font-weight: 700;
  color: var(--bs-gold, #d4a847);
  background: rgba(212, 168, 71, 0.12);
  border: 1px solid rgba(212, 168, 71, 0.3);
  animation: banner-appear 0.5s ease-out;
}

.boarding-banner {
  text-align: center;
  padding: 0.5rem 1rem;
  margin-bottom: 0.5rem;
  border-radius: 4px;
  font-size: 0.9rem;
  font-weight: 700;
  color: var(--bs-fire-red, #c0392b);
  background: rgba(192, 57, 43, 0.12);
  border: 1px solid rgba(192, 57, 43, 0.3);
  animation: banner-appear 0.5s ease-out;
}

/* ── Status banners ── */
.status-banner {
  text-align: center;
  padding: 0.375rem 0.75rem;
  margin-bottom: 0.5rem;
  border-radius: 4px;
  font-size: 0.8rem;
  font-family: "Crimson Text", serif;
}
.status-warning {
  background: rgba(192, 57, 43, 0.15);
  color: var(--bs-fire-red, #c0392b);
  font-weight: 600;
  border: 1px solid rgba(192, 57, 43, 0.3);
}
.status-info {
  background: rgba(41, 128, 185, 0.15);
  color: var(--bs-ocean-blue, #2980b9);
  border: 1px solid rgba(41, 128, 185, 0.3);
}
.summon-deploy-banner { font-weight: 600; }

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
  background: var(--bs-ocean-blue, #2980b9);
  transition: width 0.1s linear;
  border-radius: 0 0 4px 4px;
}
.cancel-deploy-btn { margin-left: 0.5rem; }
.deploy-cols { font-size: 0.75rem; }

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
.board-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.375rem;
}
.player-label {
  font-size: 0.85rem;
  font-weight: 700;
  color: var(--bs-parchment, #e8d5b0);
}
.revealed-count {
  font-size: 0.7rem;
  color: var(--bs-parchment-dim, #b09a78);
}

.indicator-badges {
  display: flex;
  gap: 0.25rem;
}

.board-active {
  animation: board-turn-flash 0.6s ease-out;
}
@keyframes board-turn-flash {
  0% { box-shadow: 0 0 12px rgba(212, 168, 71, 0.4); }
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
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 0.6rem;
  padding: 1px 6px;
  border-radius: 3px;
  cursor: help;
  white-space: nowrap;
  font-family: "Crimson Text", serif;
}
.legend-poison { background: rgba(39, 174, 96, 0.15); border: 1px solid rgba(39, 174, 96, 0.4); color: var(--bs-poison-green, #27ae60); }
.legend-explosion { background: rgba(230, 126, 34, 0.15); border: 1px solid rgba(230, 126, 34, 0.4); color: var(--bs-fire-orange, #e67e22); }
.legend-freeze { background: rgba(116, 185, 255, 0.15); border: 1px solid rgba(116, 185, 255, 0.4); color: var(--bs-ice-blue, #74b9ff); }
.legend-target { background: rgba(192, 57, 43, 0.15); border: 1px solid rgba(192, 57, 43, 0.4); color: var(--bs-fire-red, #c0392b); }
.legend-trail-ram { background: rgba(192, 57, 43, 0.15); border: 1px solid rgba(192, 57, 43, 0.4); color: var(--bs-fire-red, #c0392b); }
.legend-trail-scout { background: rgba(41, 128, 185, 0.15); border: 1px solid rgba(41, 128, 185, 0.4); color: var(--bs-ocean-blue, #2980b9); }
.legend-trail-brander { background: rgba(230, 126, 34, 0.15); border: 1px solid rgba(230, 126, 34, 0.4); color: var(--bs-fire-orange, #e67e22); }
.legend-trail-cursed { background: rgba(142, 68, 173, 0.15); border: 1px solid rgba(142, 68, 173, 0.4); color: var(--bs-cursed-purple, #8e44ad); }
.legend-trail-pirate { background: rgba(212, 168, 71, 0.15); border: 1px solid rgba(212, 168, 71, 0.4); color: var(--bs-gold, #d4a847); }

/* ═══════ Game Over ═══════ */
.victory-banner {
  text-align: center;
  padding: 2rem;
  border-radius: 6px;
  background: rgba(192, 57, 43, 0.1);
  border: 2px solid var(--bs-fire-red, #c0392b);
  animation: banner-appear 0.5s ease-out;
}
.victory-banner.is-win {
  background: rgba(212, 168, 71, 0.1);
  border-color: var(--bs-gold, #d4a847);
  box-shadow: 0 0 30px rgba(212, 168, 71, 0.15);
}
.victory-icon { margin-bottom: 0.5rem; color: var(--bs-parchment, #e8d5b0); }
.is-win .victory-icon { color: var(--bs-gold, #d4a847); }
.victory-title { margin: 0 0 0.25rem; font-size: 2.5rem; }
.win-text { color: var(--bs-gold, #d4a847); }
.lose-text { color: var(--bs-fire-red, #c0392b); }
.winner-name { color: var(--bs-parchment-dim, #b09a78); font-size: 1rem; margin: 0; }

@keyframes banner-appear {
  from { opacity: 0; transform: scale(0.9); }
  to { opacity: 1; transform: scale(1); }
}

.gameover-boards { margin-top: 1rem; }
.gameover-action { text-align: center; margin-top: 1rem; }
.gameover-enemy-board {
  filter: grayscale(0.7) brightness(0.7);
  transition: filter 1s ease;
}
.gameover-winner-board {
  animation: winner-glow 2s ease-in-out infinite alternate;
}
@keyframes winner-glow {
  0% { box-shadow: 0 0 10px rgba(212, 168, 71, 0.2); }
  100% { box-shadow: 0 0 25px rgba(212, 168, 71, 0.5); }
}
.victory-banner.is-win .victory-title {
  animation: victory-bloom 1.5s ease-out;
}
@keyframes victory-bloom {
  0% { text-shadow: 0 0 0 rgba(212, 168, 71, 0); transform: scale(0.8); }
  30% { text-shadow: 0 0 40px rgba(212, 168, 71, 0.8), 0 0 80px rgba(212, 168, 71, 0.4); transform: scale(1.05); }
  100% { text-shadow: 0 0 20px rgba(212, 168, 71, 0.6), 0 2px 8px rgba(0,0,0,0.5); transform: scale(1); }
}

/* ═══════ Error Toast ═══════ */
.error-toast {
  position: fixed;
  bottom: 1.5rem;
  left: 50%;
  transform: translateX(-50%);
  background: var(--bs-fire-red, #c0392b);
  color: white;
  padding: 0.5rem 1.25rem;
  border-radius: 4px;
  font-size: 0.85rem;
  font-weight: 600;
  z-index: 100;
  pointer-events: none;
  font-family: "Crimson Text", serif;
}
.toast-fade-enter-active, .toast-fade-leave-active { transition: opacity 0.3s ease; }
.toast-fade-enter-from, .toast-fade-leave-to { opacity: 0; }

/* ═══════ Loading ═══════ */
.loading {
  text-align: center;
  color: var(--bs-parchment-dim, #b09a78);
  padding: 3rem;
}

/* ═══════ Kill streak ═══════ */
.kill-streak {
  position: fixed;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  z-index: 50;
  font-size: 2rem;
  font-weight: 900;
  color: var(--bs-gold, #d4a847);
  text-shadow: 0 0 20px rgba(212, 168, 71, 0.6), 0 2px 8px rgba(0,0,0,0.5);
  pointer-events: none;
  animation: streak-appear 0.6s ease-out forwards;
  white-space: nowrap;
  font-family: "Pirata One", serif;
}
@keyframes streak-appear {
  0% { transform: translate(-50%, -50%) scale(0.3); opacity: 0; }
  40% { transform: translate(-50%, -50%) scale(1.2); opacity: 1; }
  100% { transform: translate(-50%, -50%) scale(1); opacity: 0.9; }
}
.streak-pop-enter-active { animation: streak-appear 0.6s ease-out; }
.streak-pop-leave-active { transition: opacity 0.3s ease-out; }
.streak-pop-leave-to { opacity: 0; }
.streak-2x { font-size: 2rem; }
.streak-3x { font-size: 2.5rem; color: var(--bs-fire-orange, #e67e22); text-shadow: 0 0 25px rgba(230, 126, 34, 0.6), 0 2px 8px rgba(0,0,0,0.5); }
.streak-4x { font-size: 3rem; color: var(--bs-fire-red, #c0392b); text-shadow: 0 0 30px rgba(192, 57, 43, 0.8), 0 0 60px rgba(192, 57, 43, 0.4), 0 2px 8px rgba(0,0,0,0.5); }

.streak-screen-flash {
  position: fixed;
  inset: 0;
  background: rgba(255, 255, 255, 0.15);
  pointer-events: none;
  z-index: 190;
}
.streak-flash-enter-active { transition: opacity 50ms ease-out; }
.streak-flash-leave-active { transition: opacity 100ms ease-out; }
.streak-flash-enter-from, .streak-flash-leave-to { opacity: 0; }

/* ═══════ Phase transition overlay ═══════ */
.phase-overlay {
  position: fixed;
  inset: 0;
  z-index: 200;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(10, 22, 40, 0.9);
  pointer-events: none;
}
.phase-overlay-text {
  font-size: 3rem;
  color: var(--bs-gold, #d4a847);
  text-shadow: 0 0 40px rgba(212, 168, 71, 0.5);
  animation: phase-text-zoom 1.2s ease-out forwards;
}
@keyframes phase-text-zoom {
  0% { transform: scale(0.3); opacity: 0; }
  30% { transform: scale(1.15); opacity: 1; }
  60% { transform: scale(1); }
  100% { transform: scale(1); opacity: 0; }
}
.phase-transition-enter-active { animation: phase-overlay-in 0.4s ease-out; }
.phase-transition-leave-active { animation: phase-overlay-out 0.8s ease-in forwards; }
@keyframes phase-overlay-in { from { opacity: 0; } to { opacity: 1; } }
@keyframes phase-overlay-out { from { opacity: 1; } to { opacity: 0; } }

/* ═══════ Turn transition sweep ═══════ */
.turn-sweep-overlay {
  position: fixed;
  inset: 0;
  z-index: 180;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: none;
  overflow: hidden;
}
.turn-sweep-band {
  position: absolute;
  inset: 0;
  background: linear-gradient(90deg, transparent, rgba(212, 168, 71, 0.15), transparent);
  animation: sweep-slide 600ms ease-out forwards;
}
@keyframes sweep-slide {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(100%); }
}
.turn-sweep-text {
  font-size: 2rem;
  color: var(--bs-gold, #d4a847);
  text-shadow: 0 0 20px rgba(212, 168, 71, 0.5);
  animation: sweep-text 800ms ease-out forwards;
}
@keyframes sweep-text {
  0% { filter: blur(8px); opacity: 0; }
  30% { filter: blur(0); opacity: 1; }
  70% { opacity: 1; }
  100% { opacity: 0; }
}
.turn-sweep-enter-active { animation: phase-overlay-in 0.2s ease-out; }
.turn-sweep-leave-active { transition: opacity 0.3s ease-out; }
.turn-sweep-leave-to { opacity: 0; }

/* ═══════ Boarding cinematic ═══════ */
.boarding-zoom {
  animation: boarding-scale 1200ms ease-in-out;
}
@keyframes boarding-scale {
  0% { transform: scale(1); }
  20% { transform: scale(0.92); }
  80% { transform: scale(0.92); }
  100% { transform: scale(1); }
}
.boarding-cine-overlay {
  position: fixed;
  inset: 0;
  z-index: 180;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: none;
  overflow: hidden;
}
.boarding-cine-slash {
  position: absolute;
  inset: 0;
  background: linear-gradient(135deg, transparent 40%, rgba(192, 57, 43, 0.3) 48%, rgba(192, 57, 43, 0.5) 50%, rgba(192, 57, 43, 0.3) 52%, transparent 60%);
  animation: boarding-slash 300ms 200ms ease-out both;
}
@keyframes boarding-slash {
  0% { transform: translateX(-100%) translateY(-100%); opacity: 0; }
  50% { opacity: 1; }
  100% { transform: translateX(100%) translateY(100%); opacity: 0; }
}
.boarding-cine-text {
  font-size: 3rem;
  color: var(--bs-fire-red, #c0392b);
  text-shadow: 0 0 30px rgba(192, 57, 43, 0.6);
  animation: boarding-text-pop 1200ms ease-out forwards;
}
@keyframes boarding-text-pop {
  0% { transform: scale(0.3); opacity: 0; }
  30% { transform: scale(1.1); opacity: 1; }
  60% { transform: scale(1); opacity: 1; }
  100% { opacity: 0; }
}
.boarding-cine-enter-active { animation: phase-overlay-in 0.2s ease-out; }
.boarding-cine-leave-active { transition: opacity 0.3s ease-out; }
.boarding-cine-leave-to { opacity: 0; }

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

/* ═══════ Confetti ═══════ */
.confetti-container {
  position: fixed;
  inset: 0;
  pointer-events: none;
  z-index: 50;
  overflow: hidden;
}
.confetti-piece {
  position: absolute;
  top: -10px;
  width: 8px;
  height: 12px;
  border-radius: 2px;
  animation: confetti-fall 3s ease-in forwards;
}
@keyframes confetti-fall {
  0% { transform: translateY(0) rotate(0); opacity: 1; }
  100% { transform: translateY(100vh) rotate(var(--c-rot, 720deg)); opacity: 0; }
}

/* ═══════ Mobile minimap ═══════ */
.minimap-wrapper { display: none; }

/* ═══════ Responsive ═══════ */
@media (max-width: 768px) {
  .combat-layout, .placement-layout {
    flex-direction: column;
    align-items: center;
  }
  .game-header {
    flex-direction: column;
    gap: 0.5rem;
    align-items: flex-start;
  }
  .bottom-panels {
    flex-direction: column;
  }
  .minimap-wrapper {
    display: block;
    position: fixed;
    bottom: 8px;
    right: 8px;
    z-index: 30;
    transform: scale(0.35);
    transform-origin: bottom right;
    opacity: 0.7;
    border: 2px solid var(--bs-wood-mid, #4a2f1a);
    border-radius: 4px;
    background: var(--bs-sea-deep, #0a1628);
    padding: 4px;
    pointer-events: none;
  }
  .minimap-label {
    font-size: 1.5rem;
    font-weight: 700;
    color: var(--bs-parchment-dim, #b09a78);
    text-align: center;
    margin-bottom: 2px;
  }
}

@media (max-width: 480px) {
  .bs-game { padding: 0 4px; }
  .kill-streak { font-size: 1.5rem; }
}

/* ── btn-sm helper ── */
.btn-sm {
  font-size: 0.75rem;
  padding: 4px 10px;
}
.btn-lg {
  font-size: 1rem;
  padding: 10px 24px;
}
</style>
