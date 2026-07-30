<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useClashStore } from 'src/store/clash'
import {
  fieldCellKey,
  globalRowToLocalRow,
  localRowToGlobalRow,
  type ClashAbilityDefinition,
  type ClashUnitState,
} from 'src/features/clash/types'
import ClashBoard from './ClashBoard.vue'
import ClashUnit from './ClashUnit.vue'

interface ActiveOption {
  source: ClashUnitState
  ability: ClashAbilityDefinition
  used: boolean
}

const store = useClashStore()
const selectedReserveId = ref<string | null>(null)
const selectedActive = ref<ActiveOption | null>(null)

const state = computed(() => store.gameState)
const me = computed(() => store.myPlayer)
const opponent = computed(() => store.opponent)
const isReinforcement = computed(() =>
  state.value?.phase === 'GuestReinforcement' || state.value?.phase === 'HostReinforcement')
const canAct = computed(() =>
  !!me.value
  && (isReinforcement.value ? !!state.value?.canPlaceReinforcement : !!state.value?.canUseActive)
  && !me.value.hasContinued
  && !store.isBusy)
const isOurTurn = computed(() =>
  !!state.value && (state.value.canPlaceReinforcement || state.value.canUseActive || state.value.canContinue))

const reserves = computed(() =>
  (me.value?.hand ?? []).filter(unit => !unit.deployed && unit.alive))

const occupiedKeys = computed(() => new Set(
  store.boardCells.filter(cell => !!cell.unit).map(cell => fieldCellKey(cell.boardRow, cell.column)),
))

const reinforcementKeys = computed(() => {
  const result = new Set<string>()
  if (!state.value || !me.value || !canAct.value || !isReinforcement.value) return result
  for (let localRow = 2; localRow < state.value.length; localRow++) {
    const boardRow = localRowToGlobalRow(localRow, state.value.length, me.value.isHost)
    for (let column = 0; column < state.value.width; column++) {
      const key = fieldCellKey(boardRow, column)
      if (!occupiedKeys.value.has(key)) result.add(key)
    }
  }
  return result
})

const activeOptions = computed<ActiveOption[]>(() => {
  if (!me.value) return []
  return store.boardUnits
    .filter(unit => unit.ownerId === me.value!.playerId && unit.alive)
    .flatMap((unit) => {
      const definition = store.catalogById.get(unit.definitionId)
      return (definition?.abilities ?? []).map(ability => ({
        source: unit,
        ability,
        used: me.value!.usedActiveIds.includes(ability.id)
          || me.value!.usedActiveIds.includes(`${unit.instanceId}:${ability.id}`),
      }))
    })
})

const targetableKeys = computed(() => {
  const result = new Set<string>()
  if (!state.value || !canAct.value || isReinforcement.value || !selectedActive.value) return result
  const target = selectedActive.value.ability.target
  for (let boardRow = 0; boardRow < state.value.length * 2; boardRow++) {
    for (let column = 0; column < state.value.width; column++) {
      const key = fieldCellKey(boardRow, column)
      const cell = store.boardCells.find(item => item.boardRow === boardRow && item.column === column)
      if (target === 'self' && cell?.unit?.instanceId !== selectedActive.value.source.instanceId) continue
      if (target === 'ally' && cell?.unit?.ownerId !== me.value?.playerId) continue
      if (target === 'enemy' && (!cell?.unit || cell.unit.ownerId === me.value?.playerId)) continue
      result.add(key)
    }
  }
  return result
})

const selectableKeys = computed(() =>
  isReinforcement.value ? reinforcementKeys.value : targetableKeys.value)

const activeLimitText = computed(() => {
  if (!me.value || me.value.morale === 0) return 'Активки заблокированы'
  return `${me.value.activeSelectionsUsed} / ${me.value.activeSelectionLimit} использований`
})
const canRepeatActive = computed(() =>
  !!me.value
  && me.value.canRepeatActive
  && me.value.morale === 4
  && me.value.activeSelectionsUsed === 3)

watch(reserves, (next) => {
  if (selectedReserveId.value && next.some(unit => unit.instanceId === selectedReserveId.value)) return
  selectedReserveId.value = next[0]?.instanceId ?? null
}, { immediate: true })

watch(() => state.value?.revision, () => {
  if (!store.isMyTurn) selectedActive.value = null
})

function activeDisabled(option: ActiveOption) {
  if (!canAct.value || !me.value || me.value.morale === 0) return true
  if (me.value.activeSelectionsUsed >= me.value.activeSelectionLimit) return true
  return option.used && !canRepeatActive.value
}

async function handleCellClick(payload: {
  boardRow: number
  column: number
  unit: ClashUnitState | null
}) {
  if (!state.value || !me.value || !canAct.value) return
  if (isReinforcement.value) {
    if (!selectedReserveId.value || payload.unit) return
    const localRow = globalRowToLocalRow(payload.boardRow, state.value.length, me.value.isHost)
    await store.placeReinforcement(selectedReserveId.value, localRow, payload.column)
    return
  }
  if (!selectedActive.value) return
  await store.useActive(
    selectedActive.value.source.instanceId,
    selectedActive.value.ability,
    payload.unit?.instanceId ?? null,
    payload.boardRow,
    payload.column,
  )
  selectedActive.value = null
}

async function useTargetlessActive() {
  if (!selectedActive.value) return
  const option = selectedActive.value
  const selfTarget = option.ability.target === 'self' ? option.source.instanceId : null
  await store.useActive(option.source.instanceId, option.ability, selfTarget, null, null)
  selectedActive.value = null
}
</script>

<template>
  <section v-if="state && me" class="clash-phase clash-between">
    <header class="clash-phase__header">
      <div>
        <span class="clash-eyebrow">Между клэшами · {{ isReinforcement ? 'Подкрепление' : 'Активки' }}</span>
        <h1>{{ isReinforcement ? 'Подведите резерв' : 'Переломите ход битвы' }}</h1>
        <p v-if="isOurTurn">
          {{ isReinforcement
            ? 'Один новый юнит автоматически передаст очередь сопернику.'
            : 'Использование активки автоматически передаст очередь сопернику.' }}
        </p>
        <p v-else>Сейчас действует {{ opponent?.username || 'соперник' }}.</p>
      </div>
      <div class="clash-phase__turn" :class="{ 'is-mine': isOurTurn }">
        <strong>{{ isOurTurn ? 'Ваш ход' : 'Ход соперника' }}</strong>
        <span>Клэш {{ state.clashNumber }}</span>
      </div>
    </header>

    <div class="clash-morale-strip">
      <div
        class="clash-morale"
        :class="{
          'is-repeat': canRepeatActive,
          'is-doubled': me.activeEffectsDoubled,
          'is-empty': me.morale === 0,
        }"
      >
        <span>Ваш боевой дух</span>
        <div class="clash-morale__pips" :aria-label="`Боевой дух ${me.morale} из 5`">
          <i v-for="pip in 5" :key="pip" :class="{ active: pip <= me.morale }" />
        </div>
        <strong>+{{ me.morale }}</strong>
        <em v-if="canRepeatActive">IV · разрешён один повтор</em>
        <em v-if="me.activeEffectsDoubled">V · все четыре эффекта ×2</em>
      </div>
      <div class="clash-morale is-enemy">
        <span>{{ opponent?.username || 'Соперник' }}</span>
        <div class="clash-morale__pips" :aria-label="`Боевой дух соперника ${opponent?.morale ?? 0} из 5`">
          <i v-for="pip in 5" :key="pip" :class="{ active: pip <= (opponent?.morale ?? 0) }" />
        </div>
        <strong>+{{ opponent?.morale ?? 0 }}</strong>
      </div>
    </div>

    <div class="clash-between__layout">
      <ClashBoard
        :width="state.width"
        :length="state.length"
        :cells="store.boardCells"
        :catalog-by-id="store.catalogById"
        :viewer="me"
        :selectable-keys="selectableKeys"
        :selected-unit-id="selectedReserveId"
        label="Поле между клэшами"
        @cell-click="handleCellClick"
      />

      <aside v-if="isReinforcement" class="clash-reserve">
        <header>
          <span class="clash-eyebrow">Подкрепление</span>
          <strong>Только 3-й ряд и глубже</strong>
        </header>
        <div class="clash-reserve__list">
          <button
            v-for="unit in reserves"
            :key="unit.instanceId"
            type="button"
            :class="{ 'is-selected': selectedReserveId === unit.instanceId }"
            :disabled="!canAct"
            @click="selectedReserveId = unit.instanceId"
          >
            <ClashUnit
              :unit="unit"
              :definition="store.catalogById.get(unit.definitionId)"
              :selected="selectedReserveId === unit.instanceId"
              compact
            />
          </button>
        </div>
        <div v-if="reserves.length === 0" class="clash-empty">В руке нет подкреплений.</div>
      </aside>

      <aside v-else class="clash-actives">
        <header>
          <span class="clash-eyebrow">Активные умения</span>
          <strong>{{ activeLimitText }}</strong>
        </header>
        <div v-if="activeOptions.length" class="clash-actives__list">
          <button
            v-for="option in activeOptions"
            :key="`${option.source.instanceId}:${option.ability.id}`"
            type="button"
            :class="{
              'is-selected': selectedActive?.source.instanceId === option.source.instanceId
                && selectedActive?.ability.id === option.ability.id,
              'is-used': option.used,
              'is-repeatable': option.used && canRepeatActive,
            }"
            :disabled="activeDisabled(option)"
            @click="selectedActive = option"
          >
            <span>{{ store.catalogById.get(option.source.definitionId)?.name }}</span>
            <strong>{{ option.ability.name }}</strong>
            <small>{{ option.ability.description || `Цель: ${option.ability.target || 'по правилам умения'}` }}</small>
            <em v-if="option.used">{{ canRepeatActive ? 'Можно повторить' : 'Уже использовано' }}</em>
          </button>
        </div>
        <div v-else class="clash-empty">У выживших юнитов нет доступных активок.</div>
        <button
          v-if="selectedActive && ['none', 'self'].includes(selectedActive.ability.target ?? '')"
          type="button"
          class="clash-btn clash-btn--accent"
          :disabled="!canAct"
          @click="useTargetlessActive"
        >
          Применить {{ selectedActive.ability.name }}
        </button>
        <p v-else-if="selectedActive" class="clash-target-hint">
          Выберите цель на поле.
        </p>
      </aside>
    </div>

    <footer class="clash-phase__actions">
      <span>«Продолжить» завершает вашу очередь без действия.</span>
      <button
        type="button"
        class="clash-btn clash-btn--primary clash-btn--large"
        :disabled="!state.canContinue || store.isBusy"
        @click="store.continuePhase()"
      >
        Продолжить
      </button>
    </footer>
  </section>
</template>
