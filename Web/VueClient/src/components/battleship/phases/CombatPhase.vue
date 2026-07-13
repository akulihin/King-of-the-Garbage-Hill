<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useBattleshipStore, type BattleshipImpactType } from 'src/store/battleship'
import type { BattleshipPendingSummon, BattleshipShotResult } from 'src/services/signalr'
import { useTip } from 'src/composables/useTip'
import BoardGrid from '../BoardGrid.vue'
import WeaponBar from '../WeaponBar.vue'
import FleetPanel from '../FleetPanel.vue'
import BattleLogPanel from '../BattleLogPanel.vue'
import SummonBar from '../SummonBar.vue'
import ActionBar from '../ActionBar.vue'
import VfxCanvas from '../VfxCanvas.vue'
import { renderIcon } from '../battleship-icons'

const store = useBattleshipStore()
const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const phase = computed(() => store.phase)
const isMyTurn = computed(() => store.isMyTurn)
const myPlayer = computed(() => store.myPlayer)
const enemyPlayer = computed(() => store.enemyPlayer)
const myFleet = computed(() => store.myFleet)
const gameLog = computed(() => store.gameLog)

// ── Weapon selection (state lives in the store) ───────────────
const selectedWeaponShip = computed(() => {
  const w = store.availableWeapons.find(w => w.shotType === store.selectedShotType)
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
const hasBranderUpgrade = computed(() => {
  return myPlayer.value?.fleet?.some(s => !s.isDestroyed && s.abilities?.includes('brander_summon')) ?? false
})

// ТЗ #11/#12: summon availability is gated by fleet regions (Таран⇒Запад, Разведчик⇒Восток,
// Пират⇒Юг); Brander needs the boiler upgrade and is once per match (ТЗ #10)
const availableSummons = computed<string[]>(() => {
  const regions = new Set(myPlayer.value?.fleet?.flatMap(s => s.regions ?? []) ?? [])
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

// Penalty zone: rows 0-2 highlighted when enemy summons present (#3)
const penaltyZoneRows = computed<number[]>(() => {
  if (!hasEnemySummonOnMyBoard.value) return []
  const myId = store.gameState?.myPlayerId
  const hasInPenaltyZone = store.myBoard?.cells.some(c =>
    c.hasSummon && c.summonOwnerId && c.summonOwnerId !== myId && c.row <= 2
  ) ?? false
  return hasInPenaltyZone ? [0, 1, 2] : []
})

const canDeploySummon = computed(() => {
  if (!myPlayer.value || !enemyPlayer.value) return false
  const p = myPlayer.value
  if (!availableSummons.value.includes(store.summonType)) return false
  const isReentry = p.summons?.some(s =>
    s.type === store.summonType && s.waitingForTurnBack) ?? false
  // ТЗ #10: Brander is outside the four normal per-match uses
  if (!isReentry && store.summonType !== 'Brander' && p.summonSlotsUsed >= p.maxSummonSlots) return false
  const threshold = 5 * (p.summonSlotsUsed + 1)
  if (!isReentry && phase.value !== 'Boarding' && enemyPlayer.value.revealedCellCount < threshold) return false
  if (phase.value !== 'Boarding' && p.summonCooldownRemaining > 0) return false
  return true
})

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
const isGreekFireMode = computed(() => store.selectedShotType === 'GreekFire')
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
  if (farBlockedRows.value.has(row)) return
  await store.shoot(row, col)
}

async function handleMyBoardCellClick(row: number, col: number) {
  if (!isMyTurn.value || (phase.value !== 'Combat' && phase.value !== 'Boarding')) return
  if (store.shotDelayActive) return
  // Greek Fire may target any own cell (ТЗ #23)
  if (isGreekFireMode.value) {
    await store.shootOwnBoard(row, col)
    return
  }
  const myId = store.gameState?.myPlayerId
  const cell = store.myBoard?.cells.find(c => c.row === row && c.col === col)
  if (!cell || !cell.hasSummon || !cell.summonOwnerId || cell.summonOwnerId === myId) return
  await store.shootOwnBoard(row, col)
}

function handleEnemyHover(row: number, col: number) {
  if (store.summonDeployMode) updateSummonDeployHighlight(row, col)
  else updateAoeHighlight(row, col)
}

async function handleWeaponSelect(weaponType: string, shotType: string) {
  await store.selectWeapon(weaponType, shotType)
}

async function handleManualMove(shipId: string, direction: string, distance: number) {
  await store.manualMove(shipId, direction, distance)
}

function handleEnemyRightClick(row: number, col: number) {
  store.toggleMarkedCell(row, col)
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
    'shot-dodge': r.scratched && r.miss,
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
  const trails = store.getSummonTrailCells('my')
  for (const cell of store.enemyBoard?.cells ?? []) {
    if (cell.summonTrail) {
      const key = `${cell.row},${cell.col}`
      if (!trails.has(key)) trails.set(key, 'Ram') // generic trail marker
    }
  }
  return trails
})
const mySummonTrails = computed(() => {
  const trails = store.getSummonTrailCells('enemy')
  for (const cell of store.myBoard?.cells ?? []) {
    if (cell.summonTrail) {
      const key = `${cell.row},${cell.col}`
      if (!trails.has(key)) trails.set(key, 'Ram') // generic trail marker
    }
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
  if (!isMyTurn.value || !myPlayer.value) return []
  // ТЗ #21: activation is per ship — each Maneuvering Double moves once
  return myFleet.value
    .filter(s => s.abilities.includes('manual_move_after_hit') && !s.isDestroyed && !s.hasManeuvered && s.decks.some(d => d.isDestroyed))
    .map(s => ({ id: s.id, name: s.name, orientation: s.orientation }))
})

const cursedBoatSummons = computed(() => {
  return (myPlayer.value?.summons?.filter(s => s.waitingForDirectionChoice) ?? [])
    .map(s => ({ id: s.id, waitingForDirectionChoice: true }))
})

// ── Weapon cursor ────────────────────────────────────────────
const weaponCursorClass = computed(() => {
  if (!isMyTurn.value) return ''
  switch (store.selectedShotType) {
    case 'Buckshot': return 'cursor-buckshot'
    case 'WhiteStone': return 'cursor-whitestone'
    case 'Incendiary': return 'cursor-incendiary'
    case 'GreekFire': return 'cursor-greekfire'
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

function impactTypeFor(result: BattleshipShotResult | null): BattleshipImpactType {
  if (!result) return 'miss'
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
    const canvas = target === 'enemy' ? enemyVfxRef.value : myVfxRef.value
    if (!canvas) return false
    const result = store.lastShotResult
    canvas.fireCannonball(row, col, () => {
      fire()
      canvas.spawnImpact(row, col, impactTypeFor(result))
    })
    return true
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
  <div class="phase-content">
    <div v-if="firstTurnBanner" class="bs-banner bs-banner--gold first-turn-banner">{{ firstTurnBanner }}</div>
    <div v-if="phase === 'Boarding'" class="bs-banner bs-banner--warning">Абордаж! Близкие корабли идут на таран.</div>

    <!-- Weapon Bar -->
    <WeaponBar
      :selected-shot-type="store.selectedShotType"
      :available-weapons="store.availableWeapons"
      :shot-delay-active="store.shotDelayActive"
      :shot-delay-remaining="shotDelayRemaining"
      :phase="phase"
      @select-weapon="handleWeaponSelect"
    />

    <!-- Status Banners -->
    <div v-if="myPlayer?.pendingSummons?.some(p => p.isBoarding)" class="bs-banner bs-banner--warning">
      Разместите все абордажные корабли перед выстрелом!
    </div>
    <div v-if="myPlayer?.hasPenalty" class="bs-banner bs-banner--warning">
      Штраф: следующий ход будет пропущен!
    </div>
    <div v-if="myPlayer && store.gameState && myPlayer.stunShotExpiry >= store.gameState.shotCount" class="bs-banner bs-banner--warning">
      Оглушение! Вы пропускаете ход.
    </div>
    <div v-if="store.shotDelayActive" class="bs-banner bs-banner--info shot-delay-banner">
      Прицеливание... <span class="delay-countdown bs-mono">{{ shotDelayRemaining.toFixed(1) }}с</span>
      <div class="delay-progress" :style="{ width: ((2 - shotDelayRemaining) / 2 * 100) + '%' }"></div>
    </div>

    <!-- Battle Boards -->
    <div class="combat-layout" :class="{ 'board-shake': store.screenShake, 'boarding-zoom': boardingZoomActive }">
      <!-- Enemy Board (primary) -->
      <div class="board-section board-enemy" :class="[{ 'board-active': !isMyTurn }, weaponCursorClass]">
        <div class="board-label">
          <span class="player-label">{{ enemyPlayer?.username ?? 'Противник' }}</span>
          <span v-if="enemyPlayer" class="indicator-badges">
            <span v-if="enemyPlayer.stunShotExpiry >= store.shotCount" class="bs-badge bs-badge--stun" @mouseenter="showTip($event, 'Оглушён')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('stun', 12)"></span>
            <span v-if="enemyPlayer.hasPenalty" class="bs-badge bs-badge--penalty" @mouseenter="showTip($event, 'Штраф')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('penalty', 12)"></span>
            <span v-if="phase === 'Boarding'" class="bs-badge bs-badge--boarding" @mouseenter="showTip($event, 'Абордаж')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('crossbones', 12)"></span>
          </span>
          <span v-if="enemyPlayer" class="revealed-count bs-mono">Разведано: {{ enemyPlayer.revealedCellCount }}/100</span>
        </div>
        <div class="board-stage">
          <BoardGrid
            :board="store.enemyBoard"
            :is-enemy="true"
            :cell-size="42"
            :shot-type="store.selectedShotType"
            :clickable="(isMyTurn && !store.shotDelayActive) || !!store.summonDeployMode"
            :highlight-cells="enemyHighlight"
            :blocked-rows="activeBlockedRows"
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
          <VfxCanvas v-if="store.vfxEnabled" ref="enemyVfxRef" />
        </div>
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
          <span class="player-label">{{ myPlayer?.username ?? 'Вы' }}</span>
          <span v-if="myPlayer" class="indicator-badges">
            <span v-if="myPlayer.stunShotExpiry >= store.shotCount" class="bs-badge bs-badge--stun" @mouseenter="showTip($event, 'Оглушён')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('stun', 12)"></span>
            <span v-if="myPlayer.hasPenalty" class="bs-badge bs-badge--penalty" @mouseenter="showTip($event, 'Штраф')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('penalty', 12)"></span>
          </span>
          <span v-if="myPlayer" class="revealed-count bs-mono">Разведано: {{ myPlayer.revealedCellCount }}/100</span>
        </div>
        <div class="board-stage">
          <BoardGrid
            :board="store.myBoard"
            :ships="myFleet"
            :cell-size="34"
            :animated-cells="store.myAnimatedCells"
            :last-shot-cell="myLastShot"
            :summon-trail-cells="mySummonTrails"
            :ship-name-map="myShipNameMap"
            :range-overlay-cells="myBoardRangeOverlays"
            :clickable="(hasEnemySummonOnMyBoard || isGreekFireMode) && isMyTurn && !store.shotDelayActive"
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
          <span v-if="hasOverlayType(mySummonTrails, 'Ram')" class="legend-item legend-trail-ram"><span v-html="renderIcon('ram', 12)"></span> Таран</span>
          <span v-if="hasOverlayType(mySummonTrails, 'Scout')" class="legend-item legend-trail-scout"><span v-html="renderIcon('scout', 12)"></span> Разведчик</span>
          <span v-if="hasOverlayType(mySummonTrails, 'Brander')" class="legend-item legend-trail-brander"><span v-html="renderIcon('brander', 12)"></span> Брандер</span>
          <span v-if="hasOverlayType(mySummonTrails, 'CursedBoat')" class="legend-item legend-trail-cursed"><span v-html="renderIcon('cursedBoat', 12)"></span> Проклятый</span>
          <span v-if="hasOverlayType(mySummonTrails, 'PirateBoat')" class="legend-item legend-trail-pirate"><span v-html="renderIcon('pirateBoat', 12)"></span> Пират</span>
        </div>
      </div>
    </div>

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

    <!-- Summon Bar -->
    <SummonBar
      :my-player="myPlayer"
      :phase="phase"
      :shot-count="store.shotCount"
      :can-deploy-summon="canDeploySummon"
      :available-summons="availableSummons"
      :summon-deploy-mode="store.summonDeployMode"
      @enter-deploy="enterSummonDeployMode"
      @enter-pending-deploy="enterPendingSummonDeployMode"
      @cancel-deploy="store.cancelSummonDeploy()"
      @set-summon-type="(t: string) => store.summonType = t"
    />

    <!-- Action Bar -->
    <ActionBar
      :maneuverable-ships="maneuverableShips"
      :cursed-boat-summons="cursedBoatSummons"
      :shot-result="store.lastShotResult"
      :shot-result-class="shotResultClass"
      :is-my-turn="isMyTurn"
      @manual-move="handleManualMove"
      @set-cursed-direction="(id: string, dir: string) => store.setCursedBoatDirection(id, dir)"
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
