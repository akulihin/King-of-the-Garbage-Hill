<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'
import type {
  BattleshipOrientation,
  BattleshipShip,
  BattleshipWeaponLoadout,
} from 'src/services/signalr'
import { useTip } from 'src/composables/useTip'
import BoardGrid from '../BoardGrid.vue'
import BsIcon from '../BsIcon.vue'
import ConfirmDialog from '../ConfirmDialog.vue'
import {
  anchorForDeck,
  deckOffsetVector,
  occupiedCells,
  occupiedDeckCells,
  orientationLabel,
} from '../battleship-geometry'

const store = useBattleshipStore()
const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const myFleet = computed(() => store.myFleet)
const myPlayer = computed(() => store.myPlayer)
const enemyPlayer = computed(() => store.enemyPlayer)
const placementLocked = computed(() => myPlayer.value?.isReady ?? false)
const canCancelConfirmation = computed(() =>
  placementLocked.value && !!enemyPlayer.value && !enemyPlayer.value.isReady)
const cancelDialogVisible = ref(false)

type TetracatapultChoice = 'WhiteStone' | 'Buckshot'
const tetracatapultLoadouts = ref<Record<string, TetracatapultChoice>>({})
const tetracatapults = computed(() => myFleet.value.flatMap(ship => {
  if (!ship.isPlaced || ship.isDestroyed) return []
  return ship.weapons
    .filter(weapon => {
      if (weapon.type !== 'Tetracatapult') return false
      const deck = ship.decks.find(value => value.index === weapon.deckIndex)
      return !!deck && !deck.isDestroyed && !deck.moduleDestroyed
    })
    .map(weapon => ({ ...weapon, shipName: ship.name }))
}))
const allTetracatapultsConfigured = computed(() =>
  tetracatapults.value.every(weapon =>
    tetracatapultLoadouts.value[weapon.id] === 'WhiteStone'
    || tetracatapultLoadouts.value[weapon.id] === 'Buckshot'))

watch(tetracatapults, weapons => {
  const next: Record<string, TetracatapultChoice> = {}
  for (const weapon of weapons) {
    const selected = tetracatapultLoadouts.value[weapon.id]
    const configured = weapon.configuredShotType
    if (selected === 'WhiteStone' || selected === 'Buckshot') next[weapon.id] = selected
    else if (configured === 'WhiteStone' || configured === 'Buckshot') next[weapon.id] = configured
  }
  tetracatapultLoadouts.value = next
}, { immediate: true })

// ── Placement hover / previews ───────────────────────────────
const placementHoverCell = ref<{ row: number; col: number } | null>(null)
const dragState = ref<{ shipId: string; deckOffset: number } | null>(null)
let suppressNextClick = false

watch(placementLocked, locked => {
  if (locked) {
    store.selectedShipId = null
    dragState.value = null
  }
})

function handlePlacementHover(row: number, col: number) {
  if (placementLocked.value) return
  placementHoverCell.value = { row, col }
}

const previewAnchor = computed(() => {
  if (!placementHoverCell.value) return null
  const orientation = store.placementOrientation
  const offset = dragState.value?.deckOffset ?? 0
  const selected = myFleet.value.find(ship => ship.id === (dragState.value?.shipId ?? store.selectedShipId))
  return selected
    ? anchorForDeck(selected, placementHoverCell.value, orientation, offset)
    : placementHoverCell.value
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
  return occupiedCells({ ...ship, row, col, orientation })
    .filter(cell => cell.row >= 0 && cell.row < 10 && cell.col >= 0 && cell.col < 10)
})

const placementSpaceHighlight = computed(() => {
  if (!store.selectedShipId || !previewAnchor.value) return []
  const ship = myFleet.value.find(s => s.id === store.selectedShipId)
  if (!ship) return []
  const space = ship.space ?? 1
  const { row, col } = previewAnchor.value
  const orientation = store.placementOrientation
  const previewCells = occupiedCells({ ...ship, row, col, orientation })
  const shipCells = new Set(previewCells.map(cell => `${cell.row},${cell.col}`))
  const zoneCells = new Set<string>()
  for (const { row: dr, col: dc } of previewCells) {
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
  const sternStep = deckOffsetVector(ship.orientation, ship.abilities.includes('diagonal_shape'))
  const [forwardRow, forwardCol] = [-sternStep.row, -sternStep.col]
  const [sideRow, sideCol] = [sternStep.col, -sternStep.row]
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
    const deck = occupiedDeckCells(ship).find(cell => cell.row === row && cell.col === col)
    if (deck) return { ship, deckIndex: deck.deckIndex }
  }
  return null
}

// ── Handlers ─────────────────────────────────────────────────
function selectShipForPlacement(shipId: string) {
  if (placementLocked.value) return
  store.selectedShipId = shipId
  const ship = myFleet.value.find(s => s.id === shipId)
  if (ship?.isPlaced) store.placementOrientation = ship.orientation
}

async function handlePlacementClick(row: number, col: number) {
  if (placementLocked.value) return
  if (suppressNextClick) { suppressNextClick = false; return }
  if (!store.selectedShipId) return
  const anchor = previewAnchor.value ?? { row, col }
  await store.placeShip(store.selectedShipId, anchor.row, anchor.col, store.placementOrientation)
  store.selectedShipId = null
}

function handlePlacementPointerDown(row: number, col: number, event: PointerEvent) {
  if (placementLocked.value) return
  const hit = shipAt(row, col)
  if (!hit) return
  event.preventDefault()
  store.selectedShipId = hit.ship.id
  store.placementOrientation = hit.ship.orientation
  dragState.value = { shipId: hit.ship.id, deckOffset: hit.deckIndex }
  placementHoverCell.value = { row, col }
}

async function handlePlacementPointerUp(row: number, col: number, event: PointerEvent) {
  if (placementLocked.value || !dragState.value) return
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
  if (placementLocked.value) return
  store.toggleOrientation()
}

async function handleConfirmPlacement() {
  if (!allTetracatapultsConfigured.value) return
  const loadouts: BattleshipWeaponLoadout[] = tetracatapults.value.map(weapon => ({
    weaponId: weapon.id,
    shotType: tetracatapultLoadouts.value[weapon.id]!,
  }))
  await store.confirmPlacement(loadouts)
}

async function handleCancelConfirmation() {
  cancelDialogVisible.value = false
  await store.cancelPlacement()
}

function currentOrientationLabel(): string {
  const ship = myFleet.value.find(value => value.id === store.selectedShipId)
  return orientationLabel(
    store.placementOrientation as BattleshipOrientation,
    ship?.abilities.includes('diagonal_shape') ?? false,
  )
}

onMounted(() => window.addEventListener('pointerup', cancelDrag))
onBeforeUnmount(() => window.removeEventListener('pointerup', cancelDrag))
</script>

<template>
  <div class="phase-content">
    <div v-if="placementLocked" class="bs-banner bs-banner--gold placement-waiting">
      Расстановка подтверждена. Ожидаем подтверждения противника.
    </div>

    <div class="placement-layout">
      <div class="placement-board">
        <h4 class="section-label">Ваше поле</h4>
        <BoardGrid
          :board="store.myBoard"
          :ships="myFleet"
          :is-placement="true"
          :clickable="!!store.selectedShipId && !placementLocked"
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
        <p v-if="!placementLocked" class="drag-hint">Зажмите ЛКМ на поставленном корабле, чтобы перетащить его.</p>
      </div>

      <div class="placement-controls" :class="{ 'placement-controls--locked': placementLocked }">
        <h4 class="section-label">Расстановка кораблей</h4>
        <button
          class="bs-btn bs-btn--sm orientation-btn"
          :disabled="placementLocked"
          @click="store.toggleOrientation()"
        >
          <BsIcon icon="rotate" :size="13" />
          Повернуть
          ({{ currentOrientationLabel() }})
        </button>

        <div class="ship-list">
          <div
            v-for="ship in myFleet"
            :key="ship.id"
            class="bs-card ship-item"
            :class="{
              'ship-selected': store.selectedShipId === ship.id,
              'ship-placed': ship.isPlaced,
              'ship-item--locked': placementLocked,
            }"
            @click="selectShipForPlacement(ship.id)"
          >
            <span class="ship-name">{{ ship.name }}</span>
            <span class="ship-decks bs-mono">{{ ship.deckCount }}П</span>
            <span v-if="ship.isPlaced" class="placed-mark">Размещён</span>
          </div>
        </div>

        <div v-if="tetracatapults.length" class="catapult-loadouts bs-card">
          <div class="loadout-title">Заряды тетракамнемётов</div>
          <div
            v-for="(weapon, index) in tetracatapults"
            :key="weapon.id"
            class="loadout-row"
          >
            <span class="loadout-source">{{ weapon.shipName }} · №{{ index + 1 }}</span>
            <div class="bs-seg" role="radiogroup" :aria-label="`Заряд: ${weapon.shipName}`">
              <button
                class="bs-seg-btn"
                type="button"
                role="radio"
                :disabled="placementLocked"
                :aria-checked="tetracatapultLoadouts[weapon.id] === 'WhiteStone'"
                :class="{ 'loadout-selected': tetracatapultLoadouts[weapon.id] === 'WhiteStone' }"
                @click="tetracatapultLoadouts[weapon.id] = 'WhiteStone'"
              >Белый камень</button>
              <button
                class="bs-seg-btn"
                type="button"
                role="radio"
                :disabled="placementLocked"
                :aria-checked="tetracatapultLoadouts[weapon.id] === 'Buckshot'"
                :class="{ 'loadout-selected': tetracatapultLoadouts[weapon.id] === 'Buckshot' }"
                @click="tetracatapultLoadouts[weapon.id] = 'Buckshot'"
              >Дробь</button>
            </div>
          </div>
          <p v-if="!allTetracatapultsConfigured && !placementLocked" class="loadout-warning">
            Выберите один заряд для каждого тетракамнемёта.
          </p>
        </div>

        <button
          v-if="!placementLocked"
          class="bs-btn bs-btn--primary bs-btn--lg"
          :disabled="myFleet.some(s => !s.isPlaced) || !allTetracatapultsConfigured"
          @mouseenter="showTip(
            $event,
            myFleet.some(s => !s.isPlaced)
              ? `Не размещено: ${myFleet.filter(s => !s.isPlaced).map(s => s.name).join(', ')}`
              : !allTetracatapultsConfigured
                ? 'Сначала выберите заряд каждого тетракамнемёта'
                : 'Подтвердить и начать бой',
          )"
          @mousemove="moveTip" @mouseleave="hideTip"
          @click="handleConfirmPlacement"
        >
          Подтвердить расстановку
        </button>
        <button
          v-else-if="canCancelConfirmation"
          class="bs-btn bs-btn--lg cancel-confirmation-btn"
          type="button"
          @click="cancelDialogVisible = true"
        >
          Отменить подтверждение
        </button>
        <p v-else class="opponent-ready-note">Противник уже подтвердил расстановку.</p>
      </div>
    </div>

    <ConfirmDialog
      v-if="cancelDialogVisible"
      title="Отменить подтверждение?"
      message="Вы снова сможете перемещать корабли и выбирать заряды, пока противник не подтвердил свою расстановку."
      confirm-label="Отменить подтверждение"
      cancel-label="Оставить как есть"
      @confirm="handleCancelConfirmation"
      @cancel="cancelDialogVisible = false"
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
.placement-waiting {
  margin-bottom: 0.75rem;
  text-align: center;
  font-weight: 800;
}

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
.placement-controls--locked .ship-list,
.placement-controls--locked .orientation-btn {
  opacity: 0.5;
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
.ship-item--locked {
  cursor: default;
  pointer-events: none;
}
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
.catapult-loadouts {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.65rem;
  margin-top: 0.25rem;
}
.loadout-title {
  color: var(--text-muted);
  font-size: 0.64rem;
  font-weight: 900;
  letter-spacing: 1px;
  text-transform: uppercase;
}
.loadout-row {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}
.loadout-source {
  color: var(--text-secondary);
  font-size: 0.72rem;
  font-weight: 700;
}
.loadout-selected {
  color: var(--accent-gold);
  border-color: color-mix(in srgb, var(--accent-gold) 62%, transparent);
  background: color-mix(in srgb, var(--accent-gold) 14%, transparent);
}
.loadout-warning,
.opponent-ready-note {
  margin: 0;
  color: var(--accent-orange);
  font-size: 0.68rem;
}
.cancel-confirmation-btn {
  color: var(--accent-orange);
  border-color: color-mix(in srgb, var(--accent-orange) 55%, transparent);
}

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
