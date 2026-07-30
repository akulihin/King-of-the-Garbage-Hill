<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useClashStore } from 'src/store/clash'
import {
  fieldCellKey,
  globalRowToLocalRow,
  localRowToGlobalRow,
  placementActionLabel,
  type ClashUnitState,
} from 'src/features/clash/types'
import ClashBoard from './ClashBoard.vue'
import ClashUnit from './ClashUnit.vue'

const store = useClashStore()
const selectedUnitId = ref<string | null>(null)

const state = computed(() => store.gameState)
const me = computed(() => store.myPlayer)
const isParallelFront = computed(() => state.value?.phase === 'InitialFrontPlacement')
const canAct = computed(() =>
  !!me.value
  && (!!state.value?.canPlace || !!state.value?.canRemove)
  && !store.isBusy)
const isOurPlacement = computed(() =>
  !!state.value
  && (state.value.canPlace || state.value.canRemove || state.value.canConfirmPlacement))

const reserves = computed(() =>
  (me.value?.hand ?? []).filter(unit => !unit.deployed && unit.alive))

const phaseLocalRow = computed(() => {
  if (state.value?.phase.includes('ThirdRow')) return 2
  if (state.value?.phase.includes('SecondRow')) return 1
  return 0
})
const requiredLocalRow = computed(() =>
  state.value?.requiredPlacementRow ?? phaseLocalRow.value)
const requiredGlobalRow = computed(() => {
  if (!state.value || !me.value) return null
  return localRowToGlobalRow(requiredLocalRow.value, state.value.length, me.value.isHost)
})

const selectableKeys = computed(() => {
  const result = new Set<string>()
  if (!state.value || !canAct.value || requiredGlobalRow.value == null) return result
  for (let column = 0; column < state.value.width; column++) {
    result.add(fieldCellKey(requiredGlobalRow.value, column))
  }
  return result
})

const actionLabel = computed(() =>
  state.value?.placementActionLabel || placementActionLabel(state.value?.phase ?? ''))

const rowPlacedCount = computed(() => {
  if (requiredGlobalRow.value == null || !me.value) return 0
  return store.boardUnits.filter(unit =>
    unit.ownerId === me.value!.playerId
    && unit.boardRow === requiredGlobalRow.value
    && unit.alive).length
})

const selectedUnit = computed(() =>
  reserves.value.find(unit => unit.instanceId === selectedUnitId.value) ?? null)

watch(reserves, (next) => {
  if (selectedUnitId.value && next.some(unit => unit.instanceId === selectedUnitId.value)) return
  selectedUnitId.value = next[0]?.instanceId ?? null
}, { immediate: true })

async function handleCellClick(payload: {
  boardRow: number
  column: number
  unit: ClashUnitState | null
}) {
  if (!state.value || !me.value || !canAct.value) return
  if (payload.unit?.ownerId === me.value.playerId) {
    await store.removeUnit(payload.unit.instanceId)
    return
  }
  if (!selectedUnitId.value || payload.unit) return
  const localRow = globalRowToLocalRow(payload.boardRow, state.value.length, me.value.isHost)
  await store.placeUnit(selectedUnitId.value, localRow, payload.column)
}
</script>

<template>
  <section v-if="state && me" class="clash-phase clash-deployment">
    <header class="clash-phase__header">
      <div>
        <span class="clash-eyebrow">Расстановка · ряд {{ requiredLocalRow + 1 }}</span>
        <h1>{{ actionLabel }}</h1>
        <p v-if="isParallelFront">
          Первая полоса скрыта. Соперник увидит её только после готовности обеих сторон.
        </p>
        <p v-else-if="isOurPlacement">
          Заполните ряд и подтвердите построение. После этого он станет виден сопернику.
        </p>
        <p v-else>
          Соперник строит свой ряд. Его построение откроется после подтверждения.
        </p>
      </div>
      <div class="clash-phase__turn" :class="{ 'is-mine': isOurPlacement }">
        <strong>{{ rowPlacedCount }} / {{ state.width }}</strong>
        <span>{{ isOurPlacement ? 'Ваше построение' : 'Ожидание' }}</span>
      </div>
    </header>

    <div class="clash-deployment__layout">
      <ClashBoard
        :width="state.width"
        :length="state.length"
        :cells="store.boardCells"
        :catalog-by-id="store.catalogById"
        :viewer="me"
        :selectable-keys="selectableKeys"
        :selected-unit-id="selectedUnitId"
        label="Поле расстановки"
        @cell-click="handleCellClick"
      />

      <aside class="clash-reserve">
        <header>
          <span class="clash-eyebrow">Рука</span>
          <strong>{{ reserves.length }} в резерве</strong>
        </header>
        <div class="clash-reserve__list">
          <button
            v-for="unit in reserves"
            :key="unit.instanceId"
            type="button"
            :class="{ 'is-selected': selectedUnitId === unit.instanceId }"
            :disabled="!canAct"
            @click="selectedUnitId = unit.instanceId"
          >
            <ClashUnit
              :unit="unit"
              :definition="store.catalogById.get(unit.definitionId)"
              :selected="selectedUnitId === unit.instanceId"
              compact
            />
          </button>
        </div>
        <div v-if="reserves.length === 0" class="clash-empty">Резерв исчерпан.</div>
      </aside>
    </div>

    <footer class="clash-phase__actions">
      <span v-if="isParallelFront && me.initialFrontConfirmed">Построение подтверждено. Ждём соперника.</span>
      <span v-else>Нажатие фиксирует весь текущий ряд.</span>
      <button
        type="button"
        class="clash-btn clash-btn--primary clash-btn--large"
        :disabled="!state.canConfirmPlacement || store.isBusy || rowPlacedCount !== state.width"
        @click="store.confirmPlacement()"
      >
        {{ actionLabel }}
      </button>
    </footer>
  </section>
</template>
