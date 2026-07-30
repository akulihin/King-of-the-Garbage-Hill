<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import ClashUnit from './ClashUnit.vue'
import type {
  ClashUnitDefinition,
  ClashUnitState,
} from 'src/features/clash/types'

const props = withDefaults(defineProps<{
  units: ClashUnitDefinition[]
  currentHand?: ClashUnitState[]
  selectedDefinitionIds?: string[]
  width: number
  length: number
  disabled?: boolean
}>(), {
  currentHand: () => [],
  disabled: false,
})

const emit = defineEmits<{
  confirm: [unitDefinitionIds: string[]]
}>()

const search = ref('')
const faction = ref('Все')
const counts = ref(new Map<string, number>())

const minimumSize = computed(() => props.width * 3)
const maximumSize = computed(() => props.width * props.length)
const selectedCount = computed(() =>
  [...counts.value.values()].reduce((sum, count) => sum + count, 0))
const canConfirm = computed(() =>
  !props.disabled
  && selectedCount.value >= minimumSize.value
  && selectedCount.value <= maximumSize.value)

const factions = computed(() => [
  'Все',
  ...new Set(props.units.map(unit => unit.faction).filter(Boolean)),
])

const filteredUnits = computed(() => {
  const query = search.value.trim().toLocaleLowerCase('ru')
  return props.units.filter((unit) => {
    const matchesFaction = faction.value === 'Все' || unit.faction === faction.value
    const matchesSearch = !query
      || unit.name.toLocaleLowerCase('ru').includes(query)
      || unit.tags.some(tag => tag.toLocaleLowerCase('ru').includes(query))
    return matchesFaction && matchesSearch
  })
})

watch(
  () => props.selectedDefinitionIds
    ? `ids:${props.selectedDefinitionIds.join('|')}`
    : `hand:${props.currentHand.map(unit => unit.definitionId).join('|')}`,
  () => {
    const next = new Map<string, number>()
    const selectedIds = props.selectedDefinitionIds
      ?? props.currentHand.map(unit => unit.definitionId)
    for (const definitionId of selectedIds) {
      next.set(definitionId, (next.get(definitionId) ?? 0) + 1)
    }
    counts.value = next
  },
  { immediate: true },
)

function countFor(unitId: string) {
  return counts.value.get(unitId) ?? 0
}

function add(unitId: string) {
  if (props.disabled || selectedCount.value >= maximumSize.value) return
  const next = new Map(counts.value)
  next.set(unitId, (next.get(unitId) ?? 0) + 1)
  counts.value = next
}

function remove(unitId: string) {
  if (props.disabled) return
  const current = counts.value.get(unitId) ?? 0
  if (current <= 0) return
  const next = new Map(counts.value)
  if (current === 1) next.delete(unitId)
  else next.set(unitId, current - 1)
  counts.value = next
}

function confirm() {
  if (!canConfirm.value) return
  const ids: string[] = []
  for (const unit of props.units) {
    for (let index = 0; index < countFor(unit.id); index++) ids.push(unit.id)
  }
  emit('confirm', ids)
}
</script>

<template>
  <section class="clash-panel clash-army-builder">
    <header class="clash-panel__header">
      <div>
        <span class="clash-eyebrow">Военный совет</span>
        <h2>Наберите армию в руку</h2>
      </div>
      <div
        class="clash-army-builder__count"
        :class="{
          'is-valid': canConfirm,
          'is-low': selectedCount < minimumSize,
        }"
      >
        <strong>{{ selectedCount }}</strong>
        <span>{{ minimumSize }}–{{ maximumSize }} юнитов</span>
      </div>
    </header>

    <div class="clash-army-builder__filters">
      <label>
        <span>Поиск</span>
        <input v-model="search" type="search" placeholder="Имя или тег..." />
      </label>
      <label>
        <span>Фракция</span>
        <select v-model="faction">
          <option v-for="item in factions" :key="item" :value="item">{{ item }}</option>
        </select>
      </label>
    </div>

    <div v-if="units.length === 0" class="clash-empty">
      Каталог юнитов загружается…
    </div>
    <div v-else class="clash-army-builder__catalog">
      <article
        v-for="unit in filteredUnits"
        :key="unit.id"
        class="clash-unit-card"
        :class="{ 'is-picked': countFor(unit.id) > 0 }"
      >
        <ClashUnit :definition="unit" />
        <p v-if="unit.passives.length">{{ unit.passives.map(item => item.name).join(' · ') }}</p>
        <p v-else class="is-muted">{{ unit.isRanged ? 'Дальний бой' : 'Ближний бой' }}</p>
        <div class="clash-unit-card__counter">
          <button
            type="button"
            aria-label="Убрать юнита"
            :disabled="disabled || countFor(unit.id) === 0"
            @click="remove(unit.id)"
          >−</button>
          <output :aria-label="`Выбрано: ${countFor(unit.id)}`">{{ countFor(unit.id) }}</output>
          <button
            type="button"
            aria-label="Добавить юнита"
            :disabled="disabled || selectedCount >= maximumSize"
            @click="add(unit.id)"
          >+</button>
        </div>
      </article>
    </div>

    <footer class="clash-panel__footer">
      <p v-if="selectedCount < minimumSize">
        Добавьте ещё {{ minimumSize - selectedCount }} юн.
      </p>
      <p v-else>Армия готова к подтверждению.</p>
      <button
        type="button"
        class="clash-btn clash-btn--primary"
        :disabled="!canConfirm"
        @click="confirm"
      >
        Сохранить руку
      </button>
    </footer>
  </section>
</template>
