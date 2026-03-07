<script setup lang="ts">
import { computed } from 'vue'
import type { BattleshipBoard, BattleshipShip } from 'src/services/signalr'
import CellComponent from './CellComponent.vue'
import { renderIcon } from './battleship-icons'

const props = withDefaults(
  defineProps<{
    board: BattleshipBoard | null
    ships?: BattleshipShip[]
    isEnemy?: boolean
    isPlacement?: boolean
    clickable?: boolean
    selectedShipId?: string | null
    highlightCells?: { row: number; col: number }[]
    spaceHighlightCells?: { row: number; col: number }[]
    zoneHighlightRows?: number[]
    blockedRows?: Set<number>
    shotType?: string
    animatedCells?: Map<string, string>
    lastShotCell?: { row: number; col: number } | null
    markedCells?: Set<string>
    summonTrailCells?: Map<string, string>
    shipNameMap?: Map<string, string>
    rangeOverlayCells?: Map<string, string>
    cellSize?: number
  }>(),
  {
    cellSize: 32,
  },
)

const emit = defineEmits<{
  (e: 'cellClick', row: number, col: number): void
  (e: 'cellHover', row: number, col: number): void
  (e: 'cellRightClick', row: number, col: number): void
  (e: 'tipShow', ev: MouseEvent, text: string): void
  (e: 'tipMove', ev: MouseEvent): void
  (e: 'tipHide'): void
}>()

const colLabels = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J']
const rowLabels = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '10']

const compassIcon = computed(() => renderIcon('compass', 14))

const cells = computed(() => {
  if (!props.board) return []
  return props.board.cells
})

function getCell(row: number, col: number) {
  return cells.value.find(c => c.row === row && c.col === col)
}

function isHighlighted(row: number, col: number) {
  return props.highlightCells?.some(h => h.row === row && h.col === col) ?? false
}

function isSpaceHighlighted(row: number, col: number) {
  return props.spaceHighlightCells?.some(h => h.row === row && h.col === col) ?? false
}

function isZoneHighlighted(row: number) {
  return props.zoneHighlightRows?.includes(row) ?? false
}

function isBlocked(row: number) {
  return props.blockedRows?.has(row) ?? false
}

function getCellAnim(row: number, col: number): string | undefined {
  return props.animatedCells?.get(`${row},${col}`)
}

const shipEdgeMap = computed(() => {
  const map = new Map<string, { top: boolean; right: boolean; bottom: boolean; left: boolean }>()
  if (!props.board || props.isEnemy) return map
  const cellMap = new Map(props.board.cells.map(c => [`${c.row},${c.col}`, c]))
  for (const cell of props.board.cells) {
    if (!cell.hasShip || !cell.shipId) continue
    const key = `${cell.row},${cell.col}`
    const top = cellMap.get(`${cell.row - 1},${cell.col}`)
    const bottom = cellMap.get(`${cell.row + 1},${cell.col}`)
    const left = cellMap.get(`${cell.row},${cell.col - 1}`)
    const right = cellMap.get(`${cell.row},${cell.col + 1}`)
    map.set(key, {
      top: !top || top.shipId !== cell.shipId,
      right: !right || right.shipId !== cell.shipId,
      bottom: !bottom || bottom.shipId !== cell.shipId,
      left: !left || left.shipId !== cell.shipId,
    })
  }
  return map
})

function isLastShot(row: number, col: number): boolean {
  return props.lastShotCell?.row === row && props.lastShotCell?.col === col
}

function isMarked(row: number, col: number): boolean {
  return props.markedCells?.has(`${row},${col}`) ?? false
}

function getSummonTrailType(row: number, col: number): string | false {
  return props.summonTrailCells?.get(`${row},${col}`) ?? false
}

function getShipEdges(row: number, col: number) {
  return shipEdgeMap.value.get(`${row},${col}`)
}

function getShipName(row: number, col: number): string | undefined {
  const cell = getCell(row, col)
  if (!cell?.shipId) return undefined
  return props.shipNameMap?.get(cell.shipId)
}

function getRangeOverlay(row: number, col: number): string | undefined {
  return props.rangeOverlayCells?.get(`${row},${col}`)
}

function handleRightClick(row: number, col: number, event: Event) {
  event.preventDefault()
  emit('cellRightClick', row, col)
}

function handleClick(row: number, col: number) {
  if (props.clickable) {
    emit('cellClick', row, col)
  }
}

function handleHover(row: number, col: number) {
  emit('cellHover', row, col)
}
</script>

<template>
  <div class="board-container bs-pirate">
    <div class="board-grid" :style="{ '--cell-size': cellSize + 'px' }">
      <!-- Column labels -->
      <div class="grid-row label-row">
        <div class="corner-cell" v-html="compassIcon" />
        <div v-for="label in colLabels" :key="label" class="label-cell col-label">{{ label }}</div>
      </div>

      <!-- Grid rows -->
      <div v-for="r in 10" :key="r" class="grid-row">
        <div class="label-cell row-label">{{ rowLabels[r - 1] }}</div>
        <CellComponent
          v-for="c in 10"
          :key="`${r - 1}-${c - 1}`"
          :cell="getCell(r - 1, c - 1)"
          :is-enemy="isEnemy"
          :is-placement="isPlacement"
          :clickable="clickable"
          :shot-type="shotType"
          :highlighted="isHighlighted(r - 1, c - 1)"
          :space-highlight="isSpaceHighlighted(r - 1, c - 1)"
          :zone-highlight="isZoneHighlighted(r - 1)"
          :blocked="isBlocked(r - 1)"
          :anim="getCellAnim(r - 1, c - 1)"
          :last-shot="isLastShot(r - 1, c - 1)"
          :marked="isMarked(r - 1, c - 1)"
          :summon-trail="getSummonTrailType(r - 1, c - 1)"
          :ship-edges="getShipEdges(r - 1, c - 1)"
          :ship-name="getShipName(r - 1, c - 1)"
          :range-overlay="getRangeOverlay(r - 1, c - 1)"
          @click="handleClick(r - 1, c - 1)"
          @mouseenter="handleHover(r - 1, c - 1)"
          @contextmenu="handleRightClick(r - 1, c - 1, $event)"
          @tip-show="(ev: MouseEvent, text: string) => emit('tipShow', ev, text)"
          @tip-move="(ev: MouseEvent) => emit('tipMove', ev)"
          @tip-hide="emit('tipHide')"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.board-container {
  overflow-x: auto;
}

.board-grid {
  display: inline-flex;
  flex-direction: column;
  gap: 1px;
  /* Ocean gradient background visible through gaps */
  background: linear-gradient(180deg,
    var(--bs-sea-dark, #0e1f3d) 0%,
    var(--bs-sea-mid, #142c54) 50%,
    var(--bs-sea-dark, #0e1f3d) 100%);
  /* Wood frame border */
  border: 3px solid var(--bs-wood-mid, #4a2f1a);
  border-radius: 4px;
  padding: 2px;
  /* Subtle inner shadow for depth */
  box-shadow:
    inset 0 0 8px rgba(0, 0, 0, 0.3),
    0 2px 8px rgba(0, 0, 0, 0.4);
}

.grid-row {
  display: flex;
  gap: 1px;
}

.corner-cell {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--bs-parchment-dim, #b09a78);
  font-size: 0.5rem;
}

.label-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.65rem;
  font-weight: 700;
  color: var(--bs-parchment-dim, #b09a78);
  font-family: "Crimson Text", "Georgia", serif;
  user-select: none;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
}

.col-label {
  width: var(--cell-size, 32px);
  height: 24px;
}

.row-label {
  width: 24px;
  height: var(--cell-size, 32px);
}

/* Pass cell size down to CellComponent */
.board-grid :deep(.cell) {
  width: var(--cell-size, 32px);
  height: var(--cell-size, 32px);
}

@media (max-width: 480px) {
  .col-label { width: var(--cell-size, 24px); height: 20px; font-size: 0.5rem; }
  .row-label { width: 20px; height: var(--cell-size, 24px); font-size: 0.5rem; }
  .corner-cell { width: 20px; height: 20px; }
}
</style>
