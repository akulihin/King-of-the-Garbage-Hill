<script setup lang="ts">
import { computed } from 'vue'
import type { BattleshipBoard, BattleshipShip } from 'src/services/signalr'
import CellComponent from './CellComponent.vue'
import { renderIcon } from './battleship-icons'
import {
  bowDirectionForOrientation,
  occupiedDeckCells,
  type BattleshipBowDirection,
} from './battleship-geometry'

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
    shipNameMap?: Map<string, string>
    rangeOverlayCells?: Map<string, string>
    maneuverActive?: boolean
    maneuverShipCells?: { row: number; col: number }[]
    maneuverTargetCells?: { row: number; col: number }[]
    replacementOptionCells?: { row: number; col: number; option: number }[]
    captureFocus?: boolean
    captureShipCells?: { row: number; col: number }[]
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
  (e: 'cellPointerDown', row: number, col: number, event: PointerEvent): void
  (e: 'cellPointerUp', row: number, col: number, event: PointerEvent): void
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

const deckVisualMap = computed(() => {
  const map = new Map<string, { symbols: string[]; bowDirection?: BattleshipBowDirection }>()
  for (const ship of props.ships ?? []) {
    if (!ship.isPlaced) continue
    const shipCells = occupiedDeckCells(ship)
    for (let i = 0; i < shipCells.length; i++) {
      const { row, col, deckIndex } = shipCells[i]
      const deck = ship.decks.find(value => value.index === deckIndex)
      const symbols: string[] = []
      if (deck?.module && !deck.moduleDestroyed) {
        const moduleKey: Record<string, string> = {
          ballista: 'ballista',
          tetracatapult: 'catapult',
          mast: 'mast',
          boiler: 'boiler',
          incendiary: 'incendiary',
        }
        const icon = moduleKey[deck.module]
        if (icon) symbols.push(icon)
      }
      if ((deck?.maxHp ?? 0) > 2) symbols.push('armor')
      map.set(`${row},${col}`, {
        symbols,
        bowDirection: i === 0
          ? bowDirectionForOrientation(
              ship.orientation,
              ship.abilities.includes('diagonal_shape'),
            )
          : undefined,
      })
    }
  }
  return map
})

function isLastShot(row: number, col: number): boolean {
  return props.lastShotCell?.row === row && props.lastShotCell?.col === col
}

function isMarked(row: number, col: number): boolean {
  return props.markedCells?.has(`${row},${col}`) ?? false
}

function getShipEdges(row: number, col: number) {
  return shipEdgeMap.value.get(`${row},${col}`)
}

function getShipName(row: number, col: number): string | undefined {
  const cell = getCell(row, col)
  if (cell?.sunkShipName) return cell.sunkShipName
  if (!cell?.shipId) return undefined
  return props.shipNameMap?.get(cell.shipId)
}

function getRangeOverlay(row: number, col: number): string | undefined {
  return props.rangeOverlayCells?.get(`${row},${col}`)
}

function getDeckVisual(row: number, col: number) {
  return deckVisualMap.value.get(`${row},${col}`)
}

function isManeuverShipCell(row: number, col: number): boolean {
  return props.maneuverShipCells?.some(cell => cell.row === row && cell.col === col) ?? false
}

function isManeuverTarget(row: number, col: number): boolean {
  return props.maneuverTargetCells?.some(cell => cell.row === row && cell.col === col) ?? false
}

function replacementOptionAt(row: number, col: number, option: number): boolean {
  return props.replacementOptionCells
    ?.some(cell => cell.row === row && cell.col === col && cell.option === option) ?? false
}

function isCaptureShipCell(row: number, col: number): boolean {
  return props.captureShipCells?.some(cell => cell.row === row && cell.col === col) ?? false
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

function handlePointerDown(row: number, col: number, event: PointerEvent) {
  if (event.button === 0) emit('cellPointerDown', row, col, event)
}

function handlePointerUp(row: number, col: number, event: PointerEvent) {
  if (event.button === 0) emit('cellPointerUp', row, col, event)
}
</script>

<template>
  <div class="board-container">
    <div class="board-grid" :style="{ '--cell-size': cellSize + 'px' }">
      <!-- Column labels -->
      <div class="grid-row label-row">
        <div class="corner-cell" v-html="compassIcon" />
        <div v-for="label in colLabels" :key="label" class="label-cell col-label">{{ label }}</div>
      </div>

      <!-- Grid rows -->
      <div v-for="r in 10" :key="r" class="grid-row" :class="{ 'grid-row--far': r >= 9 }">
        <div class="label-cell row-label">{{ rowLabels[r - 1] }}</div>
        <CellComponent
          v-for="c in 10"
          :key="`${r - 1}-${c - 1}`"
          :cell="getCell(r - 1, c - 1)"
          :is-enemy="isEnemy"
          :is-placement="isPlacement"
          :clickable="clickable && (!maneuverActive || isManeuverTarget(r - 1, c - 1))"
          :shot-type="shotType"
          :highlighted="isHighlighted(r - 1, c - 1)"
          :space-highlight="isSpaceHighlighted(r - 1, c - 1)"
          :zone-highlight="isZoneHighlighted(r - 1)"
          :blocked="isBlocked(r - 1)"
          :anim="getCellAnim(r - 1, c - 1)"
          :last-shot="isLastShot(r - 1, c - 1)"
          :marked="isMarked(r - 1, c - 1)"
          :ship-edges="getShipEdges(r - 1, c - 1)"
          :ship-name="getShipName(r - 1, c - 1)"
          :range-overlay="getRangeOverlay(r - 1, c - 1)"
          :deck-symbols="getDeckVisual(r - 1, c - 1)?.symbols"
          :bow-direction="getDeckVisual(r - 1, c - 1)?.bowDirection"
          :maneuver-active="maneuverActive"
          :maneuver-ship-cell="isManeuverShipCell(r - 1, c - 1)"
          :maneuver-target="isManeuverTarget(r - 1, c - 1)"
          :replacement-option-a="replacementOptionAt(r - 1, c - 1, 0)"
          :replacement-option-b="replacementOptionAt(r - 1, c - 1, 1)"
          :capture-focus="captureFocus"
          :capture-ship-cell="isCaptureShipCell(r - 1, c - 1)"
          @click="handleClick(r - 1, c - 1)"
          @mouseenter="handleHover(r - 1, c - 1)"
          @pointerdown="handlePointerDown(r - 1, c - 1, $event)"
          @pointerup="handlePointerUp(r - 1, c - 1, $event)"
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
  /* Deep-water gradient visible through the 1px cell gaps */
  background: linear-gradient(180deg,
    color-mix(in srgb, var(--accent-blue) 14%, var(--bg-primary)) 0%,
    color-mix(in srgb, var(--accent-blue) 22%, var(--bg-primary)) 50%,
    color-mix(in srgb, var(--accent-blue) 14%, var(--bg-primary)) 100%);
  border: 1px solid var(--glass-border);
  border-radius: 10px;
  padding: 3px;
  box-shadow:
    inset 0 0 10px rgba(0, 0, 0, 0.3),
    0 4px 14px rgba(0, 0, 0, 0.3),
    inset 0 1px 0 var(--glass-highlight);
}

.grid-row {
  display: flex;
  gap: 1px;
}

.grid-row--far {
  filter: brightness(0.72) saturate(0.78);
}
.grid-row--far .row-label {
  color: color-mix(in srgb, var(--text-dim) 72%, #020617);
}

.corner-cell {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-dim);
  font-size: 0.5rem;
}

.label-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.62rem;
  font-weight: 700;
  color: var(--text-dim);
  font-family: var(--font-mono);
  user-select: none;
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
