<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'
import type { BattleshipShip } from 'src/services/signalr'
import { useTip } from 'src/composables/useTip'
import BoardGrid from '../BoardGrid.vue'
import BsIcon from '../BsIcon.vue'

const store = useBattleshipStore()
const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const myFleet = computed(() => store.myFleet)

// ── Placement hover / previews ───────────────────────────────
const placementHoverCell = ref<{ row: number; col: number } | null>(null)
const dragState = ref<{ shipId: string; deckOffset: number } | null>(null)
let suppressNextClick = false

function handlePlacementHover(row: number, col: number) {
  placementHoverCell.value = { row, col }
}

const previewAnchor = computed(() => {
  if (!placementHoverCell.value) return null
  const orientation = store.placementOrientation
  const offset = dragState.value?.deckOffset ?? 0
  return {
    row: placementHoverCell.value.row - (orientation === 'Vertical' ? offset : 0),
    col: placementHoverCell.value.col - (orientation === 'Horizontal' ? offset : 0),
  }
})

const zoneHighlightRows = computed<number[]>(() => {
  if (!store.selectedShipId) return []
  const ship = myFleet.value.find(s => s.id === store.selectedShipId)
  if (!ship) return []
  if (ship.range === 'Tetra' || ship.range === 'Far') return [8, 9]
  return [0, 1, 2, 3, 4, 5, 6, 7]
})

const placementHighlight = computed(() => {
  if (!store.selectedShipId || !previewAnchor.value) return []
  const ship = myFleet.value.find(s => s.id === store.selectedShipId)
  if (!ship) return []
  const { row, col } = previewAnchor.value
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
  if (!store.selectedShipId || !previewAnchor.value) return []
  const ship = myFleet.value.find(s => s.id === store.selectedShipId)
  if (!ship) return []
  const space = ship.space ?? 1
  const { row, col } = previewAnchor.value
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

type ZoneType = 'freeze' | 'poison' | 'explosion'
type PlacementPose = Pick<BattleshipShip, 'row' | 'col' | 'orientation' | 'deckCount' | 'space' | 'explosionRadius' | 'abilities'>

function occupiedCells(ship: PlacementPose): { row: number; col: number }[] {
  return Array.from({ length: ship.deckCount }, (_, i) => ({
    row: ship.orientation === 'Vertical' ? ship.row + i : ship.row,
    col: ship.orientation === 'Horizontal' ? ship.col + i : ship.col,
  }))
}

function squareZone(ship: PlacementPose, radius: number): { row: number; col: number }[] {
  const cells = new Map<string, { row: number; col: number }>()
  for (const center of occupiedCells(ship)) {
    for (let dr = -radius; dr <= radius; dr++) {
      for (let dc = -radius; dc <= radius; dc++) {
        const row = center.row + dr
        const col = center.col + dc
        if (row >= 0 && row < 10 && col >= 0 && col < 10)
          cells.set(`${row},${col}`, { row, col })
      }
    }
  }
  return [...cells.values()]
}

function poisonCone(ship: PlacementPose): { row: number; col: number }[] {
  const bow = occupiedCells(ship)[0]
  if (!bow) return []
  const [forwardRow, forwardCol] = ship.orientation === 'Vertical' ? [-1, 0] : [0, -1]
  const [sideRow, sideCol] = ship.orientation === 'Vertical' ? [0, 1] : [1, 0]
  const cells: { row: number; col: number }[] = []
  for (let depth = 1; depth <= 2; depth++) {
    for (let side = -depth; side <= depth; side++) {
      const row = bow.row + forwardRow * depth + sideRow * side
      const col = bow.col + forwardCol * depth + sideCol * side
      if (row >= 0 && row < 10 && col >= 0 && col < 10) cells.push({ row, col })
    }
  }
  return cells
}

const zoneBuilders: Record<string, (ship: PlacementPose) => { type: ZoneType; cells: { row: number; col: number }[] }> = {
  freeze_nearby: ship => ({ type: 'freeze', cells: squareZone(ship, ship.space ?? 1) }),
  poison_cone: ship => ({ type: 'poison', cells: poisonCone(ship) }),
  explode_on_hit: ship => ({ type: 'explosion', cells: squareZone(ship, ship.explosionRadius || ship.space || 1) }),
}

const placementEffectZones = computed(() => {
  const overlays = new Map<string, string>()
  for (const original of myFleet.value) {
    if (!original.isPlaced && original.id !== store.selectedShipId) continue
    const isPreview = original.id === store.selectedShipId && previewAnchor.value
    const ship: PlacementPose = isPreview
      ? { ...original, ...previewAnchor.value!, orientation: store.placementOrientation }
      : original
    for (const ability of ship.abilities ?? []) {
      const zone = zoneBuilders[ability]?.(ship)
      if (!zone) continue
      for (const cell of zone.cells) overlays.set(`${cell.row},${cell.col}`, zone.type)
    }
  }
  return overlays
})

function shipAt(row: number, col: number): { ship: BattleshipShip; deckIndex: number } | null {
  for (const ship of myFleet.value) {
    if (!ship.isPlaced) continue
    const deckIndex = occupiedCells(ship).findIndex(cell => cell.row === row && cell.col === col)
    if (deckIndex >= 0) return { ship, deckIndex }
  }
  return null
}

// ── Handlers ─────────────────────────────────────────────────
function selectShipForPlacement(shipId: string) {
  store.selectedShipId = shipId
  const ship = myFleet.value.find(s => s.id === shipId)
  if (ship?.isPlaced) store.placementOrientation = ship.orientation as 'Horizontal' | 'Vertical'
}

async function handlePlacementClick(row: number, col: number) {
  if (suppressNextClick) { suppressNextClick = false; return }
  if (!store.selectedShipId) return
  const anchor = previewAnchor.value ?? { row, col }
  await store.placeShip(store.selectedShipId, anchor.row, anchor.col, store.placementOrientation)
  store.selectedShipId = null
}

function handlePlacementPointerDown(row: number, col: number, event: PointerEvent) {
  const hit = shipAt(row, col)
  if (!hit) return
  event.preventDefault()
  store.selectedShipId = hit.ship.id
  store.placementOrientation = hit.ship.orientation as 'Horizontal' | 'Vertical'
  dragState.value = { shipId: hit.ship.id, deckOffset: hit.deckIndex }
  placementHoverCell.value = { row, col }
}

async function handlePlacementPointerUp(row: number, col: number, event: PointerEvent) {
  if (!dragState.value) return
  event.preventDefault()
  placementHoverCell.value = { row, col }
  const shipId = dragState.value.shipId
  const anchor = previewAnchor.value
  dragState.value = null
  suppressNextClick = true
  if (anchor) await store.placeShip(shipId, anchor.row, anchor.col, store.placementOrientation)
  store.selectedShipId = null
}

function cancelDrag() {
  if (!dragState.value) return
  dragState.value = null
  store.selectedShipId = null
}

function handlePlacementWheel(_e: WheelEvent) {
  store.toggleOrientation()
}

async function handleConfirmPlacement() {
  await store.confirmPlacement()
}

onMounted(() => window.addEventListener('pointerup', cancelDrag))
onBeforeUnmount(() => window.removeEventListener('pointerup', cancelDrag))
</script>

<template>
  <div class="phase-content">
    <div class="placement-layout">
      <div class="placement-board">
        <h4 class="section-label">Ваше поле</h4>
        <BoardGrid
          :board="store.myBoard"
          :ships="myFleet"
          :is-placement="true"
          :clickable="!!store.selectedShipId"
          :highlight-cells="placementHighlight"
          :space-highlight-cells="placementSpaceHighlight"
          :range-overlay-cells="placementEffectZones"
          :zone-highlight-rows="zoneHighlightRows"
          @cell-click="handlePlacementClick"
          @cell-hover="handlePlacementHover"
          @cell-pointer-down="handlePlacementPointerDown"
          @cell-pointer-up="handlePlacementPointerUp"
          @wheel.prevent="handlePlacementWheel"
          @tip-show="showTip" @tip-move="moveTip" @tip-hide="hideTip"
        />
        <div v-if="placementEffectZones.size" class="placement-zone-legend">
          <span v-if="[...placementEffectZones.values()].includes('freeze')" class="zone-chip zone-freeze">Заморозка</span>
          <span v-if="[...placementEffectZones.values()].includes('poison')" class="zone-chip zone-poison">Ядовитый конус</span>
          <span v-if="[...placementEffectZones.values()].includes('explosion')" class="zone-chip zone-explosion">Радиус взрыва</span>
        </div>
        <p class="drag-hint">Зажмите ЛКМ на поставленном корабле, чтобы перетащить его.</p>
      </div>

      <div class="placement-controls">
        <h4 class="section-label">Расстановка кораблей</h4>
        <button class="bs-btn bs-btn--sm orientation-btn" @click="store.toggleOrientation()">
          <BsIcon icon="rotate" :size="13" />
          Повернуть ({{ store.placementOrientation === 'Horizontal' ? 'горизонт.' : 'вертик.' }})
        </button>

        <div class="ship-list">
          <div
            v-for="ship in myFleet"
            :key="ship.id"
            class="bs-card ship-item"
            :class="{ 'ship-selected': store.selectedShipId === ship.id, 'ship-placed': ship.isPlaced }"
            @click="selectShipForPlacement(ship.id)"
          >
            <span class="ship-name">{{ ship.name }}</span>
            <span class="ship-decks bs-mono">{{ ship.deckCount }}П</span>
            <span v-if="ship.isPlaced" class="placed-mark">Размещён</span>
          </div>
        </div>

        <button
          class="bs-btn bs-btn--primary bs-btn--lg"
          :disabled="myFleet.some(s => !s.isPlaced)"
          @mouseenter="showTip($event, myFleet.some(s => !s.isPlaced) ? `Не размещено: ${myFleet.filter(s => !s.isPlaced).map(s => s.name).join(', ')}` : 'Подтвердить и начать бой')"
          @mousemove="moveTip" @mouseleave="hideTip"
          @click="handleConfirmPlacement"
        >
          Подтвердить расстановку
        </button>
      </div>
    </div>

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

.placement-layout {
  display: flex;
  gap: 1.5rem;
  flex-wrap: wrap;
}
.section-label {
  margin: 0 0 0.5rem;
  font-size: 0.68rem;
  font-weight: 900;
  letter-spacing: 1.5px;
  text-transform: uppercase;
  color: var(--text-muted);
}
.placement-controls {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-width: 220px;
}
.orientation-btn {
  align-self: flex-start;
  margin-bottom: 0.25rem;
}
.ship-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.ship-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.45rem 0.6rem;
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.15s;
}
.ship-item:hover { border-color: var(--border-color); }
.ship-selected {
  border-color: color-mix(in srgb, var(--accent-gold) 60%, transparent) !important;
  box-shadow: var(--glow-gold);
}
.ship-placed { opacity: 0.55; }
.ship-name { font-weight: 700; color: var(--text-primary); font-size: 0.82rem; }
.ship-decks { color: var(--text-dim); font-size: 0.72rem; }
.placed-mark { font-size: 0.62rem; color: var(--accent-green); font-weight: 700; margin-left: auto; }
.placement-zone-legend {
  display: flex;
  gap: 0.4rem;
  flex-wrap: wrap;
  margin-top: 0.45rem;
}
.drag-hint { margin: 0.35rem 0 0; color: var(--text-dim); font-size: 0.68rem; }
.zone-chip {
  padding: 2px 8px;
  border-radius: 999px;
  font-size: 0.65rem;
  font-weight: 800;
  border: 1px solid currentColor;
}
.zone-freeze { color: var(--accent-blue); }
.zone-poison { color: var(--accent-green); }
.zone-explosion { color: var(--accent-orange); }

@media (max-width: 768px) {
  .placement-layout {
    flex-direction: column;
    align-items: center;
  }
  .placement-controls {
    width: 100%;
    max-width: 420px;
  }
}
</style>
