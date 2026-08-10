<script setup lang="ts">
import { computed, onBeforeUnmount, watch } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'
import { message } from 'src/platform/localization'
import BoardGrid from '../BoardGrid.vue'
import BsIcon from '../BsIcon.vue'
import { occupiedCells, orientationLabel } from '../battleship-geometry'

const store = useBattleshipStore()
const blockedRows = new Set([8, 9])
const allowedRows = [0, 1, 2, 3, 4, 5, 6, 7]

const flagship = computed(() => store.myFleet.find(ship =>
  ship.definitionId === 'flint_fortune' || ship.definitionId === 'flint_freedom') ?? null)
const placementComplete = computed(() => !!store.myPlayer?.isReady)
const hoverCell = computed(() => store.flintPlacementHoverCell)

watch(
  [() => flagship.value?.id, placementComplete],
  ([shipId, complete]) => {
    store.selectedShipId = shipId && !complete ? shipId : null
  },
  { immediate: true },
)

const previewCells = computed(() => {
  if (!flagship.value || !hoverCell.value || placementComplete.value) return []
  return occupiedCells({
    ...flagship.value,
    row: hoverCell.value.row,
    col: hoverCell.value.col,
    orientation: store.placementOrientation,
  })
})

const previewValid = computed(() => previewCells.value.length === flagship.value?.deckCount
  && previewCells.value.every(cell =>
    cell.row >= 0 && cell.row < 8 && cell.col >= 0 && cell.col < 10))

function handleHover(row: number, col: number) {
  if (placementComplete.value) return
  store.flintPlacementHoverCell = { row, col }
}

async function handlePlacement(row: number, col: number) {
  if (!flagship.value || placementComplete.value || row >= 8) return
  store.flintPlacementHoverCell = { row, col }
  if (!previewValid.value) return
  await store.placeShip(flagship.value.id, row, col, store.placementOrientation)
}

onBeforeUnmount(() => {
  store.flintPlacementHoverCell = null
  if (store.selectedShipId === flagship.value?.id) store.selectedShipId = null
})
</script>

<template>
  <div class="flint-placement">
    <div v-if="placementComplete" class="bs-banner bs-banner--gold flint-waiting">
      {{ message('battleship.flint.placement.waiting') }}
    </div>

    <div class="flint-placement-layout">
      <section class="flint-board-section">
        <BoardGrid
          :board="store.myBoard"
          :ships="store.myFleet"
          :is-placement="true"
          :clickable="!placementComplete"
          :highlight-cells="previewValid ? previewCells : []"
          :zone-highlight-rows="allowedRows"
          :blocked-rows="blockedRows"
          @cell-click="handlePlacement"
          @cell-hover="handleHover"
        />
      </section>

      <aside class="bs-card flint-placement-controls">
        <span class="bs-kicker">
          <BsIcon icon="flag" :size="13" />
          {{ message('battleship.flint.placement.kicker') }}
        </span>
        <h3 class="bs-title">{{ message('battleship.flint.placement.title') }}</h3>
        <p>{{ message('battleship.flint.placement.hint') }}</p>

        <div v-if="flagship" class="flagship-chip">
          <span>{{ flagship.name }}</span>
          <span class="bs-mono">{{ flagship.deckCount }}П</span>
        </div>

        <button
          type="button"
          class="bs-btn orientation-btn"
          :disabled="placementComplete"
          @click="store.toggleOrientation()"
        >
          <BsIcon icon="rotate" :size="13" />
          {{ message('battleship.flint.placement.rotate') }}
          ({{ orientationLabel(store.placementOrientation, false) }})
        </button>
      </aside>
    </div>
  </div>
</template>

<style scoped>
.flint-placement { margin-top: 0.5rem; }
.flint-waiting {
  margin-bottom: 0.75rem;
  text-align: center;
  font-weight: 800;
}
.flint-placement-layout {
  display: flex;
  align-items: flex-start;
  justify-content: center;
  gap: 1.5rem;
  flex-wrap: wrap;
}
.flint-board-section {
  border-radius: 12px;
}
.flint-placement-controls {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
  width: min(330px, 100%);
  padding: 1rem;
}
.flint-placement-controls h3,
.flint-placement-controls p { margin: 0; }
.flint-placement-controls p {
  color: var(--text-muted);
  font-size: 0.74rem;
  line-height: 1.5;
}
.flagship-chip {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.55rem 0.7rem;
  color: var(--accent-gold);
  background: color-mix(in srgb, var(--accent-gold) 10%, transparent);
  border: 1px solid color-mix(in srgb, var(--accent-gold) 42%, transparent);
  border-radius: 9px;
  font-weight: 800;
}
.orientation-btn { align-self: flex-start; }
@media (max-width: 700px) {
  .flint-placement-layout { flex-direction: column; align-items: center; }
}
</style>
