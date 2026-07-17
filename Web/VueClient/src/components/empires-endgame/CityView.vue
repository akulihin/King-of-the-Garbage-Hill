<script setup lang="ts">
import { computed, ref, watch, type Component } from 'vue'
import {
  AlertTriangle,
  Castle,
  Check,
  Clock3,
  Coins,
  Crown,
  Hammer,
  Landmark,
  LockKeyhole,
  Pickaxe,
  Settings2,
  ShipWheel,
  Shield,
  Sparkles,
  Trees,
  Users,
  Wheat,
  Wrench,
} from 'lucide-vue-next'

type CitySlotKind = 'farm' | 'lumber' | 'mine' | 'forge' | 'barracks' | 'unique' | 'maritime'
type PlacementSlotKind = CitySlotKind | 'municipal'

interface CityImprovementView {
  id: string
  name: string
  description?: string
  level?: number
  goldCost?: number
  timeCost?: number
  workforce?: number
  completed?: boolean
  busy?: boolean
  locked?: boolean
  prerequisites?: string[]
  imageUrl?: string
  deferredReason?: string
  deferredSubfeatures?: Array<{ id: string, reason: string }>
}

interface CityBuildingView {
  id: string
  slotId?: string
  slot: CitySlotKind
  name: string
  description?: string
  level: number
  maxLevel?: number
  baseLevel?: number
  baseMaxLevel?: number
  workforce?: number
  requiredWorkforce?: number
  output?: string
  outputValue?: number
  outputUnit?: string
  busy?: boolean
  locked?: boolean
  stateMessage?: string
  prerequisites?: string[]
  improvements?: CityImprovementView[]
  imageUrl?: string
  boostEligible?: boolean
  boosted?: boolean
  boostPercent?: number
  deferredReason?: string
  deferredSubfeatures?: Array<{ id: string, reason: string }>
}

interface MunicipalityView {
  id?: string
  name: string
  description?: string
  level?: number
  maxLevel?: number
  workforce?: number
  requiredWorkforce?: number
  output?: string
  outputValue?: number
  outputUnit?: string
  busy?: boolean
  locked?: boolean
  stateMessage?: string
  prerequisites?: string[]
  improvements?: CityImprovementView[]
  imageUrl?: string
  deferredReason?: string
  deferredSubfeatures?: Array<{ id: string, reason: string }>
}

interface CitySlotView {
  id: string
  kind: CitySlotKind
  label?: string
  hint?: string
}

interface CityPlacementOption {
  id: string
  name: string
  description?: string
  imageUrl?: string
  slot: PlacementSlotKind
  slotId?: string
  disabled?: boolean
  disabledReason?: string
}

interface RecruitableUnitView {
  id: string
  name: string
  description?: string
  imageUrl?: string
  count: number
  foodUpkeep: number
  populationCost: number
  timeCost: number
  loadoutId?: string
  resourceCosts?: string[]
  equipmentCosts?: string[]
  maxQuantity?: number
  quantity: number
  disabled?: boolean
  disabledReason?: string
  deferredReason?: string
}

interface EmpireCityView {
  id: string
  name: string
  regionName?: string
  epithet?: string
  population: number
  militaryPopulation: number
  foodProduced: number
  foodConsumed: number
  armyFoodConsumed?: number
  loyalty?: number
  accessible?: boolean
  disabledReason?: string
  armyMorale?: {
    value: number
    minimum: number
    maximum: number
  }
  equipmentStock?: Array<{
    id: string
    name: string
    value: number
  }>
  armyCohorts?: Array<{
    id: string
    unitName: string
    count: number
    loadoutId: string
    weaponName: string
    defenseName?: string
    recoveringCount?: number
    readyAtCon?: number
  }>
  epidemics?: Array<{
    instanceId: string
    name: string
    stageName: string
    severity: number
    turnsRemaining: number
    containment: { mode: 'undecided' | 'open' | 'sealed' }
    affectedClasses: Array<{ id: string, name: string, weight: number }>
    protection: Array<{ id: string, name: string, consequence: string, multiplier: number }>
    projectedNextImpact: {
      populationLoss: number
      productionLossPercent: number
      loyaltyDelta: number
      spreadChance: number
    }
    spreadWarning: string | null
  }>
  medicalTreatments?: Array<{
    veteranId: string
    unitName: string
    wounds: number
  }>
  resourceStockpiles?: Array<{
    id: string
    name: string
    value: number
    deferredReason?: string
  }>
  imageUrl?: string
  municipality?: MunicipalityView
  slots?: CitySlotView[]
  placementOptions?: CityPlacementOption[]
  recruitableUnits?: RecruitableUnitView[]
  buildings: CityBuildingView[]
}

interface SlotDefinition {
  kind: CitySlotKind
  label: string
  hint: string
  icon: Component
}

const props = withDefaults(defineProps<{
  cities: EmpireCityView[]
  activeCityId: string
  gold: number
  selectedBuildingId?: string | null
  editorMode?: boolean
}>(), {
  selectedBuildingId: null,
  editorMode: false,
})

const emit = defineEmits<{
  selectCity: [cityId: string]
  selectBuilding: [cityId: string, buildingId: string]
  upgrade: [cityId: string, buildingId: string, improvementId: string | null]
  selectMunicipality: [cityId: string]
  place: [cityId: string, slotIdOrKind: string, buildingId: string]
  recruit: [cityId: string, unitId: string, count: number]
  recruitQuantity: [cityId: string, unitId: string, count: number]
  toggleBoost: [cityId: string, buildingId: string, enabled: boolean]
  openPopulation: [cityId: string]
  editBuilding: [cityId: string, buildingId: string]
  editSlot: [cityId: string, slot: PlacementSlotKind]
  treatVeteran: [veteranId: string]
}>()

const slotDefinitions: SlotDefinition[] = [
  { kind: 'farm', label: 'Ферма', hint: 'Провизия и предел роста', icon: Wheat },
  { kind: 'lumber', label: 'Лесопилка', hint: 'Древесина и городские работы', icon: Trees },
  { kind: 'mine', label: 'Шахта', hint: 'Металл и камень', icon: Pickaxe },
  { kind: 'forge', label: 'Кузница', hint: 'Снаряжение и ремесло', icon: Hammer },
  { kind: 'barracks', label: 'Казарма', hint: 'Регулярная армия', icon: Shield },
  { kind: 'unique', label: 'Особое здание', hint: 'Храм, банк или региональный проект', icon: Sparkles },
  { kind: 'maritime', label: 'Морской участок', hint: 'Только прибрежный Морской порт', icon: ShipWheel },
]

const internalSelectedId = ref<string | null>(props.selectedBuildingId)
const placementSlotId = ref<string | null>(null)

const activeCity = computed(() => props.cities.find(city => city.id === props.activeCityId)
  ?? props.cities[0]
  ?? null)

const slots = computed(() => {
  const city = activeCity.value
  if (!city) return []
  const configuredSlots = city.slots?.length
    ? city.slots
    : slotDefinitions.map(slot => ({ id: slot.kind, kind: slot.kind }))

  return configuredSlots.map(slot => {
    const definition = slotDefinitions.find(item => item.kind === slot.kind)!
    return {
      ...definition,
      ...slot,
      label: slot.label || definition.label,
      hint: slot.hint || definition.hint,
      building: city.buildings.find(building => building.slotId === slot.id)
        ?? city.buildings.find(building => !building.slotId && building.slot === slot.kind)
        ?? null,
    }
  })
})

const selectedBuilding = computed(() => activeCity.value?.buildings
  .find(building => building.id === internalSelectedId.value) ?? null)

const municipalityId = computed(() => activeCity.value?.municipality?.id ?? 'municipal')

const selectedMunicipality = computed(() => activeCity.value?.municipality
  && internalSelectedId.value === municipalityId.value
  ? activeCity.value.municipality
  : null)

const selectedFacility = computed(() => {
  if (selectedBuilding.value) return {
    ...selectedBuilding.value,
    slotKind: selectedBuilding.value.slot as PlacementSlotKind,
    municipal: false,
  }
  if (selectedMunicipality.value) return {
    ...selectedMunicipality.value,
    id: municipalityId.value,
    level: selectedMunicipality.value.level ?? 0,
    slotKind: 'municipal' as const,
    municipal: true,
  }
  return null
})

const activePlacementSlot = computed(() => {
  if (!activeCity.value || !placementSlotId.value) return null
  if (placementSlotId.value === 'municipal') {
    return { id: 'municipal', kind: 'municipal' as const, label: 'Муниципальный центр', hint: 'Городское управление' }
  }
  return slots.value.find(slot => slot.id === placementSlotId.value) ?? null
})

const placementChoices = computed(() => {
  if (!activeCity.value || !activePlacementSlot.value) return []
  return (activeCity.value.placementOptions ?? []).filter(option =>
    option.slot === activePlacementSlot.value?.kind
    && (!option.slotId || option.slotId === activePlacementSlot.value?.id))
})

const selectedRecruitableUnits = computed(() => selectedBuilding.value?.slot === 'barracks'
  ? activeCity.value?.recruitableUnits ?? []
  : [])

const foodBalance = computed(() => {
  if (!activeCity.value) return 0
  return activeCity.value.foodProduced - activeCity.value.foodConsumed
})

const currentSlot = computed(() => slotDefinitions
  .find(slot => slot.kind === selectedFacility.value?.slotKind) ?? null)

watch(() => props.selectedBuildingId, buildingId => {
  internalSelectedId.value = buildingId
})

watch(activeCity, city => {
  if (!city?.buildings.some(building => building.id === internalSelectedId.value)
    && internalSelectedId.value !== (city?.municipality?.id ?? 'municipal')) {
    internalSelectedId.value = null
  }
  placementSlotId.value = null
}, { immediate: true })

function formatNumber(value: number) {
  return new Intl.NumberFormat('ru-RU', {
    notation: Math.abs(value) >= 10_000 ? 'compact' : 'standard',
    maximumFractionDigits: 1,
  }).format(value)
}

function signedNumber(value: number) {
  return `${value > 0 ? '+' : ''}${formatNumber(value)}`
}

function buildingOutput(building: { output?: string, outputValue?: number, outputUnit?: string }) {
  if (building.output) return building.output
  if (building.outputValue === undefined) return 'Нет активного выпуска'
  return `${signedNumber(building.outputValue)}${building.outputUnit ? ` ${building.outputUnit}` : ''}`
}

function chooseBuilding(building: CityBuildingView) {
  if (!activeCity.value) return
  placementSlotId.value = null
  internalSelectedId.value = building.id
  emit('selectBuilding', activeCity.value.id, building.id)
}

function chooseMunicipality() {
  if (!activeCity.value) return
  if (!activeCity.value.municipality) {
    if (props.editorMode) emit('editSlot', activeCity.value.id, 'municipal')
    else openPlacement('municipal')
    return
  }
  placementSlotId.value = null
  internalSelectedId.value = municipalityId.value
  emit('selectBuilding', activeCity.value.id, municipalityId.value)
  emit('selectMunicipality', activeCity.value.id)
}

function openPlacement(slotId: string) {
  internalSelectedId.value = null
  placementSlotId.value = slotId
}

function chooseSlot(slot: (typeof slots.value)[number]) {
  if (!activeCity.value) return
  if (slot.building) {
    chooseBuilding(slot.building)
  } else if (props.editorMode) {
    emit('editSlot', activeCity.value.id, slot.kind)
  } else {
    openPlacement(slot.id)
  }
}

function placeBuilding(option: CityPlacementOption) {
  if (!activeCity.value || !activePlacementSlot.value || option.disabled) return
  emit('place', activeCity.value.id, activePlacementSlot.value.id || activePlacementSlot.value.kind, option.id)
  placementSlotId.value = null
}

function chooseCity(event: Event) {
  const cityId = (event.target as HTMLSelectElement).value
  if (props.cities.find(city => city.id === cityId)?.accessible === false && !props.editorMode) return
  emit('selectCity', cityId)
}

function hideBrokenImage(event: Event) {
  ;(event.currentTarget as HTMLImageElement).hidden = true
}

function requestUpgrade(improvementId: string | null = null) {
  if (!activeCity.value || !selectedFacility.value) return
  emit('upgrade', activeCity.value.id, selectedFacility.value.id, improvementId)
}

function maxRecruitQuantity(unit: RecruitableUnitView) {
  if (!activeCity.value) return 1
  const populationLimit = unit.populationCost <= 0
    ? 99
    : Math.floor(activeCity.value.militaryPopulation / unit.populationCost)
  return Math.min(99, Math.max(0, populationLimit), unit.maxQuantity ?? 99)
}

function recruitQuantity(unit: RecruitableUnitView) {
  return Math.max(1, Math.min(maxRecruitQuantity(unit) || 1, Math.floor(unit.quantity)))
}

function changeRecruitQuantity(unit: RecruitableUnitView, event: Event) {
  if (!activeCity.value) return
  const raw = Number((event.target as HTMLInputElement).value)
  const count = Math.max(1, Math.min(maxRecruitQuantity(unit) || 1, Math.floor(raw || 1)))
  emit('recruitQuantity', activeCity.value.id, unit.id, count)
}

function recruitUnit(unit: RecruitableUnitView) {
  if (!activeCity.value || unit.disabled || maxRecruitQuantity(unit) < 1) return
  const count = recruitQuantity(unit)
  emit('recruit', activeCity.value.id, unit.id, count)
}

function toggleBoost() {
  if (!activeCity.value || !selectedBuilding.value) return
  emit('toggleBoost', activeCity.value.id, selectedBuilding.value.id, !selectedBuilding.value.boosted)
}
</script>

<template>
  <section
    v-if="activeCity"
    class="city-view"
    :class="{ 'editor-mode': editorMode, inaccessible: activeCity.accessible === false }"
    :data-testid="`city-view-${activeCity.id}`"
  >
    <header class="city-header">
      <div class="city-title-block">
        <span class="eyebrow">{{ editorMode ? 'Конструктор города' : activeCity.regionName || 'Имперский город' }}</span>
        <div class="city-selector-row">
          <Castle :size="21" aria-hidden="true" />
          <label>
            <span class="sr-only">Выберите город</span>
            <select :value="activeCity.id" @change="chooseCity">
              <option
                v-for="city in cities"
                :key="city.id"
                :value="city.id"
                :disabled="!editorMode && city.accessible === false"
              >
                {{ city.name }}{{ city.regionName ? ` · ${city.regionName}` : '' }}{{ city.accessible === false ? ' — НЕДОСТУПЕН' : '' }}
              </option>
            </select>
          </label>
        </div>
        <p v-if="activeCity.epithet">{{ activeCity.epithet }}</p>
      </div>

      <div v-if="editorMode" class="editor-flag"><Settings2 :size="14" /> Режим редактора</div>
      <div v-else-if="activeCity.accessible === false" class="lost-flag"><AlertTriangle :size="14" /> Город недоступен</div>
    </header>

    <template v-if="activeCity.accessible !== false || editorMode">
      <div class="economy-strip" aria-label="Экономика города">
      <div class="economy-stat gold-stat">
        <Coins :size="18" />
        <span>Золото империи</span>
        <strong>{{ formatNumber(gold) }}</strong>
        <small>общее</small>
      </div>
      <button class="economy-stat population-stat" type="button" @click="emit('openPopulation', activeCity.id)">
        <Users :size="18" />
        <span>Население</span>
        <strong>{{ formatNumber(activeCity.population) }}</strong>
        <small>{{ formatNumber(activeCity.militaryPopulation) }} военных</small>
      </button>
      <div class="economy-stat military-stat">
        <Shield :size="18" />
        <span>Военный резерв</span>
        <strong>{{ formatNumber(activeCity.militaryPopulation) }}</strong>
        <small>{{ activeCity.loyalty === undefined ? 'готовы к найму' : `лояльность ${signedNumber(activeCity.loyalty)}` }}</small>
      </div>
      <div class="economy-stat food-stat" :class="{ negative: foodBalance < 0 }">
        <Wheat :size="18" />
        <span>Баланс еды</span>
        <strong>{{ signedNumber(foodBalance) }}</strong>
        <small>
          {{ formatNumber(activeCity.foodProduced) }} − {{ formatNumber(activeCity.foodConsumed) }}
          <template v-if="activeCity.armyFoodConsumed !== undefined"> · армия {{ formatNumber(activeCity.armyFoodConsumed) }}</template>
        </small>
      </div>
      </div>

      <div v-if="activeCity.resourceStockpiles?.length" class="city-resources" aria-label="Запасы выбранного города">
        <strong>Запасы города</strong>
        <span
          v-for="resource in activeCity.resourceStockpiles"
          :key="resource.id"
          :data-testid="`city-resource-${resource.id}`"
          :data-city-id="activeCity.id"
        >
          <small>
            {{ resource.name }}
            <em v-if="resource.deferredReason" :title="resource.deferredReason">будущее</em>
          </small>
          <b>{{ formatNumber(resource.value) }}</b>
        </span>
      </div>

      <section
        v-if="activeCity.epidemics?.length"
        class="epidemic-ledger"
        :data-testid="`city-epidemics-${activeCity.id}`"
        aria-label="Эпидемии города"
      >
        <article v-for="epidemic in activeCity.epidemics" :key="epidemic.instanceId">
          <header>
            <strong>☣ {{ epidemic.name }} · {{ epidemic.stageName }}</strong>
            <span>тяжесть {{ epidemic.severity }} · осталось {{ epidemic.turnsRemaining }}</span>
          </header>
          <p>
            Сословия: {{ epidemic.affectedClasses.map(item => `${item.name} ×${item.weight}`).join(', ') }}.
            Режим: {{ epidemic.containment.mode === 'sealed' ? 'врата заперты' : epidemic.containment.mode === 'open' ? 'врата открыты' : 'не выбран' }}.
          </p>
          <p>
            Следующий итог: −{{ formatNumber(epidemic.projectedNextImpact.populationLoss) }} жителей,
            −{{ formatNumber(epidemic.projectedNextImpact.productionLossPercent) }}% производства,
            лояльность {{ signedNumber(epidemic.projectedNextImpact.loyaltyDelta) }}.
            <b v-if="epidemic.spreadWarning">{{ epidemic.spreadWarning }}</b>
          </p>
          <small v-if="epidemic.protection.length">
            Защита: {{ epidemic.protection.map(item => `${item.name}/${item.consequence} ×${formatNumber(item.multiplier)}`).join(' · ') }}
          </small>
          <small v-else>Активной медицинской защиты нет.</small>
        </article>
      </section>

      <section v-if="activeCity.medicalTreatments?.length" class="medical-ledger" aria-label="Лечение ветеранов">
        <strong>Медицинская академия · одно лечение за кон</strong>
        <button
          v-for="patient in activeCity.medicalTreatments"
          :key="patient.veteranId"
          type="button"
          @click="emit('treatVeteran', patient.veteranId)"
        >
          Лечить {{ patient.unitName }} · ран {{ patient.wounds }} · риск смерти 50%
        </button>
      </section>

      <section class="army-ledger" aria-label="Армия и снаряжение">
        <header>
          <span><Shield :size="14" /> Армия города</span>
          <b v-if="activeCity.armyMorale">
            Боевой дух {{ formatNumber(activeCity.armyMorale.value) }}
            <small>({{ formatNumber(activeCity.armyMorale.minimum) }}–{{ formatNumber(activeCity.armyMorale.maximum) }})</small>
          </b>
        </header>
        <div class="army-ledger-grid">
          <div class="equipment-ledger">
            <strong><Hammer :size="13" /> Общий склад снаряжения</strong>
            <span v-for="equipment in activeCity.equipmentStock" :key="equipment.id">
              <small>{{ equipment.name }}</small>
              <b>{{ formatNumber(equipment.value) }}</b>
            </span>
            <em v-if="!activeCity.equipmentStock?.length">Склад пуст</em>
          </div>
          <div class="cohort-ledger">
            <strong><Users :size="13" /> Снаряжённые когорты</strong>
            <span v-for="cohort in activeCity.armyCohorts" :key="cohort.id">
              <b>{{ cohort.unitName }} × {{ formatNumber(cohort.count) }}</b>
              <small>{{ cohort.weaponName }}<template v-if="cohort.defenseName"> · {{ cohort.defenseName }}</template> · {{ cohort.loadoutId }}</small>
              <em v-if="cohort.recoveringCount">На лечении {{ cohort.recoveringCount }} до кона {{ cohort.readyAtCon }}</em>
            </span>
            <em v-if="!activeCity.armyCohorts?.length">В городе нет когорт</em>
          </div>
        </div>
      </section>

      <div class="city-layout">
      <aside class="improvement-drawer" aria-live="polite">
        <template v-if="activePlacementSlot">
          <div class="drawer-visual placement-visual">
            <Landmark v-if="activePlacementSlot.kind === 'municipal'" :size="34" aria-hidden="true" />
            <component :is="slotDefinitions.find(slot => slot.kind === activePlacementSlot?.kind)?.icon || Sparkles" v-else :size="34" aria-hidden="true" />
            <span>{{ activePlacementSlot.label }}</span>
          </div>

          <div class="drawer-heading placement-heading">
            <div>
              <span>Новое строительство</span>
              <h3>Выберите здание</h3>
            </div>
          </div>
          <p class="drawer-description">{{ activePlacementSlot.hint }}</p>

          <div class="placement-list" role="list">
            <button
              v-for="option in placementChoices"
              :key="option.id"
              type="button"
              :disabled="option.disabled"
              :aria-describedby="option.disabledReason ? `placement-reason-${option.id}` : undefined"
              @click="placeBuilding(option)"
            >
              <span class="placement-image">
                <img v-if="option.imageUrl" :src="option.imageUrl" alt="" @error="hideBrokenImage" />
                <Hammer v-else :size="18" aria-hidden="true" />
              </span>
              <span>
                <strong>{{ option.name }}</strong>
                <small v-if="option.description">{{ option.description }}</small>
                <em v-if="option.disabledReason" :id="`placement-reason-${option.id}`">{{ option.disabledReason }}</em>
              </span>
            </button>
            <p v-if="!placementChoices.length" class="empty-improvements">
              Для этого участка пока нет доступных проектов.
            </p>
          </div>
        </template>

        <template v-else-if="selectedFacility">
          <div class="drawer-visual">
            <img
              v-if="selectedFacility.imageUrl"
              :src="selectedFacility.imageUrl"
              :alt="''"
              @error="hideBrokenImage"
            />
            <Crown v-else-if="selectedFacility.municipal" :size="30" aria-hidden="true" />
            <component :is="currentSlot?.icon || Wrench" v-else :size="30" aria-hidden="true" />
            <span>{{ selectedFacility.municipal ? 'Муниципальный центр' : currentSlot?.label }}</span>
          </div>

          <div class="drawer-heading">
            <div>
              <span>Улучшения здания</span>
              <h3>{{ selectedFacility.name }}</h3>
            </div>
            <b :data-testid="`building-level-${selectedFacility.id}`">{{ selectedFacility.level }}<small>/{{ selectedFacility.maxLevel ?? '∞' }}</small></b>
          </div>

          <p v-if="selectedFacility.description" class="drawer-description">{{ selectedFacility.description }}</p>
          <p v-if="selectedFacility.deferredReason" class="deferred-note" role="status">
            <LockKeyhole :size="13" />
            <span><strong>Будущая механика.</strong> {{ selectedFacility.deferredReason }}</span>
          </p>
          <div v-if="selectedFacility.deferredSubfeatures?.length" class="deferred-note deferred-parts" role="status">
            <LockKeyhole :size="13" />
            <span>
              <strong>Отложенные части.</strong>
              <em v-for="subfeature in selectedFacility.deferredSubfeatures" :key="subfeature.id">
                {{ subfeature.reason }}
              </em>
            </span>
          </div>
          <p
            v-if="selectedFacility.baseLevel !== undefined && (selectedFacility.level !== selectedFacility.baseLevel || selectedFacility.maxLevel !== selectedFacility.baseMaxLevel)"
            class="effective-level-note"
          >
            <Sparkles :size="13" />
            Реликвия: базовый уровень {{ selectedFacility.baseLevel }}/{{ selectedFacility.baseMaxLevel ?? '∞' }},
            эффективный {{ selectedFacility.level }}/{{ selectedFacility.maxLevel ?? '∞' }}.
          </p>

          <div class="state-row">
            <span v-if="selectedFacility.locked" class="state locked"><LockKeyhole :size="13" /> Закрыто</span>
            <span v-else-if="selectedFacility.busy" class="state busy"><Clock3 :size="13" /> Занято</span>
            <span v-else class="state ready"><Check :size="13" /> Доступно</span>
            <span v-if="selectedFacility.stateMessage" class="state-note">{{ selectedFacility.stateMessage }}</span>
          </div>

          <dl class="building-metrics">
            <div>
              <dt>Рабочая сила</dt>
              <dd>{{ formatNumber(selectedFacility.workforce ?? 0) }} / {{ formatNumber(selectedFacility.requiredWorkforce ?? selectedFacility.workforce ?? 0) }}</dd>
            </div>
            <div>
              <dt>Выпуск</dt>
              <dd>{{ buildingOutput(selectedFacility) }}</dd>
            </div>
          </dl>

          <div v-if="selectedFacility.prerequisites?.length" class="prerequisites">
            <span><LockKeyhole :size="13" /> Требования</span>
            <ul>
              <li v-for="requirement in selectedFacility.prerequisites" :key="requirement">{{ requirement }}</li>
            </ul>
          </div>

          <button
            v-if="editorMode"
            class="primary-action editor-action"
            type="button"
            @click="emit('editBuilding', activeCity.id, selectedFacility.id)"
          >
            <Settings2 :size="15" /> Настроить здание
          </button>
          <button
            v-else
            class="primary-action"
            type="button"
            :disabled="Boolean(selectedFacility.deferredReason) || selectedFacility.locked || selectedFacility.busy || (selectedFacility.maxLevel !== undefined && selectedFacility.level >= selectedFacility.maxLevel)"
            @click="requestUpgrade()"
          >
            <Hammer :size="15" />
            {{ selectedFacility.deferredReason ? 'Будущая механика' : selectedFacility.maxLevel !== undefined && selectedFacility.level >= selectedFacility.maxLevel ? 'Максимальный уровень' : 'Улучшить уровень' }}
          </button>

          <div
            v-if="!editorMode && selectedBuilding && ['farm', 'lumber', 'mine'].includes(selectedBuilding.slot) && (selectedBuilding.boostEligible !== undefined || selectedBuilding.boosted !== undefined || selectedBuilding.boostPercent !== undefined)"
            class="boost-action"
            :class="{ active: selectedBuilding.boosted }"
          >
            <div>
              <span><Sparkles :size="14" /> Целевое усиление</span>
              <small>{{ selectedBuilding.boosted ? `Производство работает на ${selectedBuilding.boostPercent ?? 200}%` : 'Назначьте выбранному производству имперский приоритет.' }}</small>
            </div>
            <button
              type="button"
              :disabled="!selectedBuilding.boosted && (!selectedBuilding.boostEligible || selectedBuilding.locked || selectedBuilding.busy)"
              @click="toggleBoost"
            >
              {{ selectedBuilding.boosted ? 'Снять' : `Усилить · ${selectedBuilding.boostPercent ?? 200}%` }}
            </button>
          </div>

          <div v-if="!editorMode && selectedBuilding?.slot === 'barracks'" class="recruitment-panel">
            <div class="list-heading">
              <span>Найм армии</span>
              <b>{{ selectedRecruitableUnits.length }}</b>
            </div>
            <p v-if="activeCity.armyFoodConsumed !== undefined" class="army-upkeep">
              <Wheat :size="13" /> Армия потребляет {{ formatNumber(activeCity.armyFoodConsumed) }} еды за фазу
            </p>
            <article v-for="unit in selectedRecruitableUnits" :key="unit.id" class="recruit-unit" :class="{ disabled: unit.disabled, deferred: Boolean(unit.deferredReason) }">
              <span class="unit-image">
                <img v-if="unit.imageUrl" :src="unit.imageUrl" alt="" @error="hideBrokenImage" />
                <Shield v-else :size="18" aria-hidden="true" />
              </span>
              <span class="unit-copy">
                <strong>{{ unit.name }} <em>× {{ formatNumber(unit.count) }}</em></strong>
                <small v-if="unit.description">{{ unit.description }}</small>
                <span v-if="unit.deferredReason" class="deferred-inline"><LockKeyhole :size="11" /> {{ unit.deferredReason }}</span>
                <span class="unit-costs">
                  <i><Wheat :size="11" /> {{ formatNumber(unit.foodUpkeep) }}</i>
                  <i><Users :size="11" /> {{ formatNumber(unit.populationCost) }}</i>
                  <i><Clock3 :size="11" /> {{ unit.timeCost }} д</i>
                </span>
                <span v-if="unit.loadoutId" class="unit-loadout">Комплект: {{ unit.loadoutId }}</span>
                <span v-if="unit.resourceCosts?.length" class="unit-price">Цена: {{ unit.resourceCosts.join(' · ') }}</span>
                <span v-if="unit.equipmentCosts?.length" class="unit-price">Снаряжение: {{ unit.equipmentCosts.join(' · ') }}</span>
                <em v-if="unit.disabledReason" class="disabled-reason">{{ unit.disabledReason }}</em>
              </span>
              <span class="recruit-controls">
                <label>
                  <span class="sr-only">Количество {{ unit.name }}</span>
                  <input
                    :value="recruitQuantity(unit)"
                    type="number"
                    inputmode="numeric"
                    min="1"
                    :max="Math.max(1, maxRecruitQuantity(unit))"
                    :disabled="unit.disabled || maxRecruitQuantity(unit) < 1"
                    @input="changeRecruitQuantity(unit, $event)"
                  />
                </label>
                <button
                  type="button"
                  :disabled="unit.disabled || maxRecruitQuantity(unit) < 1"
                  @click="recruitUnit(unit)"
                >Нанять</button>
              </span>
            </article>
            <p v-if="!selectedRecruitableUnits.length" class="empty-improvements">Нет доступных для найма подразделений.</p>
          </div>

          <div class="improvement-list">
            <div class="list-heading">
              <span>Ветви улучшений</span>
              <b>{{ selectedFacility.improvements?.length ?? 0 }}</b>
            </div>
            <button
              v-for="improvement in selectedFacility.improvements"
              :key="improvement.id"
              type="button"
              :disabled="!editorMode && (Boolean(improvement.deferredReason) || improvement.locked || improvement.busy || improvement.completed)"
              :class="{ completed: improvement.completed, locked: improvement.locked }"
              @click="editorMode ? emit('editBuilding', activeCity.id, selectedFacility.id) : requestUpgrade(improvement.id)"
            >
              <span class="improvement-icon">
                <img v-if="improvement.imageUrl" :src="improvement.imageUrl" alt="" @error="hideBrokenImage" />
                <Check v-else-if="improvement.completed" :size="16" />
                <LockKeyhole v-else-if="improvement.locked" :size="15" />
                <Wrench v-else :size="15" />
              </span>
              <span class="improvement-copy">
                <strong>{{ improvement.name }}</strong>
                <small v-if="improvement.description">{{ improvement.description }}</small>
                <em v-if="improvement.deferredReason" class="deferred-inline"><LockKeyhole :size="11" /> {{ improvement.deferredReason }}</em>
                <em v-if="improvement.prerequisites?.length">{{ improvement.prerequisites.join(' · ') }}</em>
              </span>
              <span class="improvement-cost">
                <b v-if="improvement.goldCost !== undefined">{{ formatNumber(improvement.goldCost) }} <Coins :size="11" /></b>
                <b v-if="improvement.timeCost !== undefined">{{ improvement.timeCost }} д <Clock3 :size="11" /></b>
              </span>
            </button>
            <p v-if="!selectedFacility.improvements?.length" class="empty-improvements">
              {{ editorMode ? 'Добавьте улучшения в конфигурации здания.' : 'Для этого уровня нет отдельных улучшений.' }}
            </p>
          </div>
        </template>

        <div v-else class="drawer-empty">
          <Landmark :size="34" />
          <h3>Выберите здание</h3>
          <p>Нажмите на городской слот, чтобы открыть уровни, занятость и зависимости.</p>
        </div>
      </aside>

      <div class="city-scene">
        <img v-if="activeCity.imageUrl" class="city-backdrop" :src="activeCity.imageUrl" alt="" @error="hideBrokenImage" />
        <div class="city-glow" aria-hidden="true" />

        <button
          v-for="slot in slots"
          :key="slot.id"
          type="button"
          class="building-slot"
          :data-testid="slot.building ? `city-building-${slot.building.id}` : undefined"
          :class="[
            `slot-${slot.kind}`,
            {
              selected: slot.building?.id === selectedBuilding?.id,
              locked: slot.building?.locked,
              busy: slot.building?.busy,
              deferred: Boolean(slot.building?.deferredReason),
              empty: !slot.building,
            },
          ]"
          :aria-pressed="slot.building?.id === selectedBuilding?.id"
          @click="chooseSlot(slot)"
        >
          <span class="slot-image">
            <img v-if="slot.building?.imageUrl" :src="slot.building.imageUrl" alt="" @error="hideBrokenImage" />
            <component :is="slot.icon" v-else :size="27" aria-hidden="true" />
          </span>
          <span class="slot-copy">
            <small>{{ slot.label }}</small>
            <strong>{{ slot.building?.name || (editorMode ? 'Добавить здание' : 'Пустой слот') }}</strong>
            <em v-if="slot.building" :data-testid="`city-building-level-${slot.building.id}`">Ур. {{ slot.building.level }}/{{ slot.building.maxLevel ?? '∞' }} · {{ buildingOutput(slot.building) }}</em>
            <em v-else>{{ slot.hint }}</em>
          </span>
          <span v-if="slot.building?.deferredReason" class="slot-state"><LockKeyhole :size="12" /> Будущее</span>
          <span v-else-if="slot.building?.locked" class="slot-state"><LockKeyhole :size="12" /> Закрыто</span>
          <span v-else-if="slot.building?.busy" class="slot-state"><Clock3 :size="12" /> Занято</span>
          <span v-else-if="editorMode" class="slot-state"><Settings2 :size="12" /> Правка</span>
        </button>

        <button
          class="municipality"
          type="button"
          :class="{
            selected: Boolean(selectedMunicipality),
            locked: activeCity.municipality?.locked,
            busy: activeCity.municipality?.busy,
            deferred: Boolean(activeCity.municipality?.deferredReason),
            empty: !activeCity.municipality,
          }"
          :aria-pressed="Boolean(selectedMunicipality)"
          @click="chooseMunicipality"
        >
          <span class="municipality-seal">
            <img v-if="activeCity.municipality?.imageUrl" :src="activeCity.municipality.imageUrl" alt="" @error="hideBrokenImage" />
            <Crown v-else :size="30" aria-hidden="true" />
          </span>
          <small>Муниципальный центр</small>
          <strong>{{ activeCity.municipality?.name || (editorMode ? 'Добавить управление' : 'Пустой муниципальный слот') }}</strong>
          <em v-if="activeCity.municipality">Ур. {{ activeCity.municipality.level ?? 0 }}</em>
          <em v-else>Выберите городской проект</em>
          <span v-if="activeCity.municipality?.deferredReason"><LockKeyhole :size="12" /> Будущая механика</span>
          <span v-else-if="activeCity.municipality?.locked"><LockKeyhole :size="12" /> Закрыто</span>
          <span v-else-if="activeCity.municipality?.busy"><Clock3 :size="12" /> Проект в работе</span>
          <span v-else-if="activeCity.municipality"><Landmark :size="12" /> Открыть улучшения</span>
          <span v-else-if="editorMode"><Settings2 :size="12" /> Настроить слот</span>
          <span v-else><Hammer :size="12" /> Построить управление</span>
        </button>
      </div>
      </div>

      <div v-if="foodBalance < 0" class="food-warning" role="status">
        <AlertTriangle :size="17" />
        <span><strong>Городу не хватает еды.</strong> При сохранении дефицита население начнёт сокращаться.</span>
      </div>
    </template>

    <div v-else class="city-inaccessible" role="status" data-testid="city-inaccessible">
      <AlertTriangle :size="38" />
      <h2>Город потерян</h2>
      <p>{{ activeCity.disabledReason || 'Империя больше не может управлять этим городом и использовать его здания или ресурсы.' }}</p>
      <span>Выберите доступный город в списке выше.</span>
    </div>
  </section>

  <section v-else class="city-view city-empty-state">
    <Castle :size="38" />
    <h2>В империи пока нет городов</h2>
    <p>Добавьте первый город в конфигурации карты.</p>
  </section>
</template>

<style scoped>
.city-view {
  --city-gold: #c9aa67;
  --city-gold-bright: #f0d79a;
  --city-ink: #11130f;
  --city-panel: #181b15;
  --city-line: rgba(226, 204, 158, 0.17);
  overflow: hidden;
  border: 1px solid var(--city-line);
  border-radius: 18px;
  color: #eee5d1;
  background: #10120e;
  box-shadow: 0 24px 70px rgba(0, 0, 0, 0.34);
}

.city-header {
  display: flex;
  min-height: 92px;
  align-items: center;
  justify-content: space-between;
  gap: 18px;
  padding: 17px 22px;
  border-bottom: 1px solid var(--city-line);
  background: linear-gradient(105deg, #211d16, #192019 62%, #151913);
}

.eyebrow {
  color: var(--city-gold);
  font: 800 0.64rem/1 var(--font-mono, monospace);
  letter-spacing: 0.14em;
  text-transform: uppercase;
}

.city-selector-row { display: flex; align-items: center; gap: 9px; margin-top: 6px; color: var(--city-gold-bright); }
.city-selector-row label { min-width: 0; }
.city-selector-row select {
  width: min(440px, 68vw);
  border: 0;
  color: #f6ecd6;
  background: transparent;
  font: 700 clamp(1.25rem, 2.5vw, 1.9rem)/1.05 Georgia, serif;
  cursor: pointer;
}
.city-selector-row select:focus-visible { outline: 2px solid var(--city-gold); outline-offset: 4px; }
.city-selector-row option { color: #eee5d1; background: #171a14; font: 600 0.95rem/1.2 sans-serif; }
.city-title-block p { margin: 5px 0 0 30px; color: rgba(238, 229, 209, 0.58); font-size: 0.76rem; }
.editor-flag { display: inline-flex; align-items: center; gap: 6px; padding: 7px 10px; border: 1px solid rgba(109, 181, 179, 0.34); border-radius: 999px; color: #9dd8d5; background: rgba(57, 135, 135, 0.1); font: 800 0.62rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.lost-flag { display: inline-flex; align-items: center; gap: 6px; padding: 7px 10px; border: 1px solid rgba(192, 92, 76, 0.38); border-radius: 999px; color: #e2a195; background: rgba(133, 48, 38, 0.12); font: 800 0.62rem/1 var(--font-mono, monospace); text-transform: uppercase; }

.economy-strip { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); border-bottom: 1px solid var(--city-line); background: #151812; }
.economy-stat {
  display: grid;
  min-height: 82px;
  grid-template-columns: 24px 1fr auto;
  grid-template-rows: auto auto;
  align-items: center;
  gap: 3px 7px;
  padding: 12px 16px;
  border: 0;
  border-right: 1px solid var(--city-line);
  color: inherit;
  text-align: left;
  background: transparent;
}
.economy-stat:last-child { border-right: 0; }
.economy-stat > svg { grid-row: 1 / 3; color: var(--city-gold); }
.economy-stat span { color: rgba(238, 229, 209, 0.57); font: 800 0.6rem/1 var(--font-mono, monospace); letter-spacing: 0.07em; text-transform: uppercase; }
.economy-stat strong { grid-column: 3; grid-row: 1 / 3; color: #f5ead3; font: 800 1.2rem/1 var(--font-mono, monospace); }
.economy-stat small { grid-column: 2; color: rgba(238, 229, 209, 0.42); font-size: 0.65rem; }
.population-stat { cursor: pointer; transition: 140ms ease; }
.population-stat:hover { background: rgba(198, 168, 107, 0.08); }
.population-stat:focus-visible { position: relative; z-index: 1; outline: 2px solid var(--city-gold); outline-offset: -3px; }
.food-stat.negative strong, .food-stat.negative > svg { color: #df7867; }
.city-resources { display: flex; min-height: 50px; align-items: stretch; gap: 1px; overflow-x: auto; border-bottom: 1px solid var(--city-line); background: #11140f; }
.city-resources > strong { display: grid; min-width: 118px; place-items: center; padding: 9px 12px; color: #c6a869; font: 800 0.57rem/1 var(--font-mono, monospace); letter-spacing: 0.08em; text-transform: uppercase; }
.city-resources > span { display: grid; min-width: 100px; align-content: center; gap: 4px; padding: 8px 12px; border-left: 1px solid var(--city-line); }
.city-resources small { color: rgba(238, 229, 209, 0.5); font-size: 0.57rem; }
.city-resources small em { margin-left: 4px; color: #d1a269; font-size: .48rem; font-style: normal; text-transform: uppercase; }
.city-resources b { color: #eee1c6; font: 800 0.77rem/1 var(--font-mono, monospace); }
.epidemic-ledger { display: grid; gap: 1px; border-bottom: 1px solid var(--city-line); background: #24180f; }
.epidemic-ledger article { display: grid; gap: 6px; padding: 11px 14px; background: rgba(82, 43, 15, .42); }
.epidemic-ledger header { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 8px; color: #ffdb91; }
.epidemic-ledger p { margin: 0; color: rgba(246, 231, 201, .78); font-size: .68rem; line-height: 1.45; }
.epidemic-ledger p b { margin-left: 6px; color: #f6ac85; }
.epidemic-ledger small { color: rgba(255, 224, 164, .62); font: 600 .57rem/1.35 var(--font-mono, monospace); }
.medical-ledger { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; padding: 9px 14px; border-bottom: 1px solid var(--city-line); background: #14201a; }
.medical-ledger strong { margin-right: auto; color: #aad2b4; font: 800 .58rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.medical-ledger button { border: 1px solid rgba(170, 210, 180, .3); border-radius: 5px; padding: 6px 8px; color: #dceadf; background: #20372a; font-size: .6rem; cursor: pointer; }
.army-ledger { border-bottom: 1px solid var(--city-line); background: #141710; }
.army-ledger > header { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 9px 14px; border-bottom: 1px solid rgba(226, 204, 158, .1); }
.army-ledger > header span,.equipment-ledger > strong,.cohort-ledger > strong { display: inline-flex; align-items: center; gap: 5px; color: #c9aa67; font: 800 .57rem/1 var(--font-mono, monospace); letter-spacing: .06em; text-transform: uppercase; }
.army-ledger > header b { color: #d8e0bd; font: 800 .65rem/1 var(--font-mono, monospace); }
.army-ledger > header small { color: rgba(216, 224, 189, .55); }
.army-ledger-grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); }
.equipment-ledger,.cohort-ledger { display: flex; min-height: 55px; align-items: center; gap: 9px; overflow-x: auto; padding: 8px 12px; }
.equipment-ledger { border-right: 1px solid var(--city-line); }
.equipment-ledger > strong,.cohort-ledger > strong { flex: 0 0 auto; }
.equipment-ledger > span,.cohort-ledger > span { display: grid; flex: 0 0 auto; gap: 3px; padding: 5px 8px; border: 1px solid rgba(226, 204, 158, .1); border-radius: 6px; background: rgba(255,255,255,.025); }
.equipment-ledger small,.cohort-ledger small { color: rgba(238,229,209,.52); font-size: .55rem; }
.equipment-ledger b,.cohort-ledger b { color: #eee1c6; font: 800 .65rem/1 var(--font-mono, monospace); }
.equipment-ledger > em,.cohort-ledger > em { color: rgba(238,229,209,.35); font-size: .6rem; font-style: normal; }

.city-layout { display: grid; min-height: 640px; grid-template-columns: 310px minmax(0, 1fr); }
.improvement-drawer { position: relative; z-index: 3; overflow: auto; max-height: 720px; padding: 17px; border-right: 1px solid var(--city-line); background: linear-gradient(180deg, #1b1d17, #131510); box-shadow: 12px 0 38px rgba(0, 0, 0, 0.22); }
.drawer-visual { position: relative; display: grid; height: 112px; place-items: center; overflow: hidden; border: 1px solid var(--city-line); border-radius: 12px; color: rgba(240, 215, 154, 0.82); background: radial-gradient(circle at 50% 30%, rgba(216, 187, 123, 0.18), transparent 48%), #11130f; }
.drawer-visual img { width: 100%; height: 100%; object-fit: cover; }
.drawer-visual > span { position: absolute; right: 8px; bottom: 7px; padding: 4px 6px; border-radius: 5px; color: #f1e5cc; background: rgba(8, 10, 8, 0.78); font: 800 0.56rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.drawer-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; margin-top: 14px; }
.drawer-heading span { color: var(--city-gold); font: 800 0.58rem/1 var(--font-mono, monospace); letter-spacing: 0.1em; text-transform: uppercase; }
.drawer-heading h3 { margin: 5px 0 0; color: #f5ead3; font: 700 1.25rem/1.05 Georgia, serif; }
.drawer-heading > b { display: flex; align-items: baseline; color: var(--city-gold-bright); font: 800 1.7rem/1 var(--font-mono, monospace); }
.drawer-heading > b small { color: rgba(238, 229, 209, 0.38); font-size: 0.65rem; }
.drawer-description { margin: 10px 0; color: rgba(238, 229, 209, 0.62); font-size: 0.73rem; line-height: 1.45; }
.deferred-note { display: flex; align-items: flex-start; gap: 6px; margin: 9px 0; padding: 8px 9px; border: 1px solid rgba(190, 132, 78, .3); border-radius: 7px; color: #e0b984; background: rgba(126, 75, 34, .11); font-size: .61rem; line-height: 1.4; }
.deferred-note svg { flex: 0 0 auto; margin-top: 1px; }
.deferred-note strong { color: #f0cca0; }
.effective-level-note { display: flex; align-items: flex-start; gap: 6px; margin: 9px 0; padding: 8px 9px; border: 1px solid rgba(117, 170, 105, 0.25); border-radius: 7px; color: #b8d7a8; background: rgba(77, 126, 68, 0.08); font-size: 0.61rem; line-height: 1.4; }
.effective-level-note svg { flex: 0 0 auto; margin-top: 1px; }
.state-row { display: flex; flex-wrap: wrap; align-items: center; gap: 7px; margin: 11px 0; }
.state { display: inline-flex; align-items: center; gap: 4px; padding: 5px 7px; border-radius: 999px; font: 800 0.56rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.state.ready { color: #a6d5a3; background: rgba(89, 157, 94, 0.13); }
.state.busy { color: #e8c97a; background: rgba(201, 170, 103, 0.13); }
.state.locked { color: #d48678; background: rgba(177, 82, 70, 0.13); }
.state-note { color: rgba(238, 229, 209, 0.52); font-size: 0.65rem; }
.building-metrics { display: grid; grid-template-columns: 1fr 1fr; gap: 7px; margin: 12px 0; }
.building-metrics div { min-width: 0; padding: 9px; border: 1px solid rgba(226, 204, 158, 0.11); border-radius: 8px; background: rgba(255, 255, 255, 0.025); }
.building-metrics dt { color: rgba(238, 229, 209, 0.42); font: 800 0.53rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.building-metrics dd { overflow: hidden; margin: 5px 0 0; color: #e9dcc2; font-size: 0.68rem; font-weight: 700; text-overflow: ellipsis; }
.prerequisites { margin: 10px 0; padding: 9px 10px; border-left: 2px solid #a96257; background: rgba(169, 98, 87, 0.08); }
.prerequisites > span { display: inline-flex; align-items: center; gap: 4px; color: #d99c90; font: 800 0.56rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.prerequisites ul { margin: 7px 0 0; padding-left: 17px; color: rgba(238, 229, 209, 0.62); font-size: 0.65rem; line-height: 1.5; }
.primary-action { display: flex; width: 100%; min-height: 38px; align-items: center; justify-content: center; gap: 7px; margin: 12px 0; border: 1px solid #b69554; border-radius: 8px; color: #211b10; background: linear-gradient(#e1c781, #b99651); cursor: pointer; font-weight: 800; }
.primary-action:hover:not(:disabled) { filter: brightness(1.08); transform: translateY(-1px); }
.primary-action:disabled { border-color: rgba(226, 204, 158, 0.12); color: rgba(238, 229, 209, 0.35); background: rgba(255, 255, 255, 0.035); cursor: not-allowed; }
.primary-action.editor-action { border-color: #5baaa7; color: #d8f1ef; background: rgba(61, 134, 133, 0.17); }
.placement-visual { border-style: dashed; }
.placement-heading { margin-bottom: 2px; }
.placement-list { display: grid; gap: 7px; margin-top: 14px; }
.placement-list > button { display: grid; grid-template-columns: 44px minmax(0, 1fr); align-items: center; gap: 10px; width: 100%; padding: 8px; border: 1px solid rgba(226, 204, 158, 0.14); border-radius: 9px; color: #e8dcc3; text-align: left; background: rgba(255, 255, 255, 0.027); cursor: pointer; }
.placement-list > button:hover:not(:disabled) { border-color: rgba(218, 186, 118, 0.56); background: rgba(201, 170, 103, 0.08); }
.placement-list > button:focus-visible { outline: 2px solid var(--city-gold-bright); outline-offset: 2px; }
.placement-list > button:disabled { opacity: 0.52; cursor: not-allowed; }
.placement-image { display: grid; width: 44px; height: 44px; place-items: center; overflow: hidden; border-radius: 8px; color: #d4ba76; background: rgba(201, 170, 103, 0.1); }
.placement-image img { width: 100%; height: 100%; object-fit: cover; }
.placement-list button > span:last-child { display: grid; min-width: 0; gap: 3px; }
.placement-list strong { color: #f0e4cc; font-size: 0.7rem; }
.placement-list small { color: rgba(238, 229, 209, 0.52); font-size: 0.59rem; line-height: 1.35; }
.placement-list em { color: #d18f83; font-size: 0.56rem; font-style: normal; }
.boost-action { display: grid; gap: 9px; margin: 12px 0; padding: 10px; border: 1px solid rgba(201, 170, 103, 0.19); border-radius: 9px; background: rgba(201, 170, 103, 0.055); }
.boost-action.active { border-color: rgba(106, 176, 105, 0.34); background: rgba(80, 141, 79, 0.09); }
.boost-action > div { display: grid; gap: 4px; }
.boost-action span { display: inline-flex; align-items: center; gap: 5px; color: #e1c982; font: 800 0.58rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.boost-action small { color: rgba(238, 229, 209, 0.56); font-size: 0.61rem; line-height: 1.4; }
.boost-action button { min-height: 32px; border: 1px solid rgba(219, 190, 123, 0.45); border-radius: 7px; color: #f2e5c8; background: rgba(201, 170, 103, 0.14); cursor: pointer; font-size: 0.65rem; font-weight: 800; }
.boost-action button:hover:not(:disabled) { border-color: var(--city-gold-bright); background: rgba(201, 170, 103, 0.21); }
.boost-action button:disabled { opacity: 0.42; cursor: not-allowed; }
.recruitment-panel { margin: 13px 0; padding-top: 1px; border-top: 1px solid rgba(226, 204, 158, 0.11); }
.unit-loadout,.unit-price { display: block; margin-top: 4px; color: rgba(238, 229, 209, .48); font-size: .55rem; line-height: 1.3; }
.unit-loadout { color: #b9c99a; font-family: var(--font-mono, monospace); }
.army-upkeep { display: flex; align-items: center; gap: 5px; margin: 0 0 8px; padding: 7px 8px; border-radius: 7px; color: #d8bf7e; background: rgba(201, 170, 103, 0.07); font-size: 0.59rem; }
.recruit-unit { display: grid; grid-template-columns: 34px minmax(0, 1fr); gap: 8px; margin-top: 7px; padding: 8px; border: 1px solid rgba(226, 204, 158, 0.12); border-radius: 8px; background: rgba(255, 255, 255, 0.025); }
.recruit-unit.disabled { opacity: 0.58; }
.recruit-unit.deferred { border-style: dashed; }
.unit-image { display: grid; width: 34px; height: 34px; place-items: center; overflow: hidden; border-radius: 7px; color: #d2b975; background: rgba(201, 170, 103, 0.1); }
.unit-image img { width: 100%; height: 100%; object-fit: cover; }
.unit-copy { display: grid; min-width: 0; gap: 3px; }
.unit-copy > strong { color: #eee2c9; font-size: 0.67rem; }
.unit-copy > strong em { color: #cdb87f; font: 700 0.54rem/1 var(--font-mono, monospace); font-style: normal; }
.unit-copy > small { color: rgba(238, 229, 209, 0.5); font-size: 0.57rem; line-height: 1.35; }
.deferred-inline { display: inline-flex; align-items: flex-start; gap: 4px; color: #d6a66f; font-size: .54rem; font-style: normal; line-height: 1.35; }
.deferred-inline svg { flex: none; margin-top: 1px; }
.deferred-parts span { display: grid; gap: 4px; }
.deferred-parts em { font-style: normal; }
.unit-costs { display: flex; flex-wrap: wrap; gap: 5px 8px; }
.unit-costs i { display: inline-flex; align-items: center; gap: 3px; color: #cfc09e; font: 700 0.52rem/1 var(--font-mono, monospace); font-style: normal; }
.unit-copy .disabled-reason { color: #d58e81; font-size: 0.54rem; font-style: normal; }
.recruit-controls { display: grid; grid-column: 1 / -1; grid-template-columns: 68px minmax(0, 1fr); gap: 6px; }
.recruit-controls input { width: 100%; min-height: 31px; box-sizing: border-box; border: 1px solid rgba(226, 204, 158, 0.2); border-radius: 6px; color: #eee4ce; text-align: center; background: #10120e; font: 700 0.67rem/1 var(--font-mono, monospace); }
.recruit-controls button { min-height: 31px; border: 1px solid rgba(201, 170, 103, 0.45); border-radius: 6px; color: #241d10; background: #c4a660; cursor: pointer; font-size: 0.63rem; font-weight: 800; }
.recruit-controls button:hover:not(:disabled) { filter: brightness(1.08); }
.recruit-controls input:disabled, .recruit-controls button:disabled { opacity: 0.45; cursor: not-allowed; }
.list-heading { display: flex; align-items: center; justify-content: space-between; margin: 17px 0 7px; color: rgba(238, 229, 209, 0.52); font: 800 0.57rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.list-heading b { display: grid; min-width: 21px; height: 21px; place-items: center; border-radius: 50%; color: #ddc47f; background: rgba(201, 170, 103, 0.13); }
.improvement-list { display: grid; gap: 6px; }
.improvement-list > button { display: grid; grid-template-columns: 31px minmax(0, 1fr) auto; align-items: center; gap: 8px; width: 100%; padding: 8px; border: 1px solid rgba(226, 204, 158, 0.12); border-radius: 8px; color: #e8dcc3; text-align: left; background: rgba(255, 255, 255, 0.026); cursor: pointer; }
.improvement-list > button:hover:not(:disabled) { border-color: rgba(218, 186, 118, 0.52); background: rgba(201, 170, 103, 0.07); }
.improvement-list > button:disabled { opacity: 0.55; cursor: not-allowed; }
.improvement-list > button.completed { border-color: rgba(93, 159, 99, 0.24); }
.improvement-icon { display: grid; width: 31px; height: 31px; place-items: center; overflow: hidden; border-radius: 7px; color: #d6bd78; background: rgba(201, 170, 103, 0.1); }
.improvement-icon img { width: 100%; height: 100%; object-fit: cover; }
.improvement-copy { display: grid; min-width: 0; gap: 2px; }
.improvement-copy strong { font-size: 0.68rem; }
.improvement-copy small { overflow: hidden; color: rgba(238, 229, 209, 0.5); font-size: 0.58rem; text-overflow: ellipsis; white-space: nowrap; }
.improvement-copy em { color: #c9897c; font-size: 0.54rem; font-style: normal; }
.improvement-cost { display: grid; gap: 2px; justify-items: end; }
.improvement-cost b { display: inline-flex; align-items: center; gap: 3px; color: #d7c184; font: 700 0.54rem/1 var(--font-mono, monospace); }
.empty-improvements { margin: 0; padding: 13px 10px; border: 1px dashed rgba(226, 204, 158, 0.13); border-radius: 8px; color: rgba(238, 229, 209, 0.42); text-align: center; font-size: 0.65rem; }
.drawer-empty { display: grid; min-height: 560px; place-content: center; justify-items: center; padding: 25px; color: rgba(238, 229, 209, 0.38); text-align: center; }
.drawer-empty h3 { margin: 12px 0 6px; color: #e8dcc4; font: 700 1.2rem/1 Georgia, serif; }
.drawer-empty p { max-width: 220px; margin: 0; font-size: 0.72rem; line-height: 1.5; }

.city-scene {
  position: relative;
  display: grid;
  min-height: 640px;
  grid-template-areas:
    'farm lumber mine'
    'forge municipality barracks'
    '. unique .';
  grid-template-columns: repeat(3, minmax(160px, 1fr));
  grid-template-rows: repeat(3, minmax(160px, 1fr));
  gap: 18px;
  overflow: hidden;
  padding: 34px;
  isolation: isolate;
  background:
    linear-gradient(rgba(12, 15, 11, 0.54), rgba(12, 14, 10, 0.86)),
    radial-gradient(circle at 50% 40%, #576047, #252b20 57%, #11140f);
}
.city-scene::before { content: ''; position: absolute; z-index: -1; inset: 0; opacity: 0.18; background-image: repeating-linear-gradient(32deg, transparent 0 30px, rgba(255, 255, 255, 0.04) 31px 32px), repeating-linear-gradient(-38deg, transparent 0 42px, rgba(0, 0, 0, 0.18) 43px 44px); }
.city-backdrop { position: absolute; z-index: -2; width: 100%; height: 100%; object-fit: cover; opacity: 0.42; }
.city-glow { position: absolute; z-index: -1; top: 38%; left: 50%; width: 410px; height: 250px; border: 1px solid rgba(226, 204, 158, 0.08); border-radius: 50%; background: radial-gradient(ellipse, rgba(201, 170, 103, 0.11), transparent 68%); transform: translate(-50%, -50%); pointer-events: none; }
.building-slot, .municipality { position: relative; z-index: 1; display: grid; min-width: 0; align-content: end; overflow: hidden; padding: 12px; border: 1px solid rgba(226, 204, 158, 0.22); border-radius: 14px; color: #eee4ce; text-align: left; background: linear-gradient(160deg, rgba(37, 39, 30, 0.94), rgba(18, 21, 16, 0.97)); box-shadow: 0 14px 28px rgba(0, 0, 0, 0.28); cursor: pointer; transition: 150ms ease; }
.building-slot:hover, .building-slot.selected, .municipality:hover, .municipality.selected { z-index: 2; border-color: var(--city-gold); transform: translateY(-3px); box-shadow: 0 18px 34px rgba(0, 0, 0, 0.36), 0 0 0 1px rgba(201, 170, 103, 0.18); }
.building-slot:focus-visible, .municipality:focus-visible { outline: 2px solid var(--city-gold-bright); outline-offset: 3px; }
.building-slot.locked { border-color: rgba(176, 92, 78, 0.34); filter: saturate(0.7); }
.building-slot.deferred { border-style: dashed; border-color: rgba(190, 132, 78, .38); filter: saturate(.55); }
.building-slot.busy { border-color: rgba(211, 169, 81, 0.42); }
.building-slot.empty { border-style: dashed; color: rgba(238, 229, 209, 0.55); background: rgba(15, 18, 13, 0.66); }
.slot-farm { grid-area: farm; }
.slot-lumber { grid-area: lumber; }
.slot-mine { grid-area: mine; }
.slot-forge { grid-area: forge; }
.slot-barracks { grid-area: barracks; }
.slot-unique { grid-area: unique; }
.slot-image { position: absolute; inset: 0; display: grid; place-items: center; color: rgba(232, 209, 155, 0.38); }
.slot-image::after { content: ''; position: absolute; inset: 0; background: linear-gradient(transparent 15%, rgba(11, 13, 10, 0.25) 50%, rgba(11, 13, 10, 0.96)); }
.slot-image img { width: 100%; height: 100%; object-fit: cover; opacity: 0.72; }
.slot-image svg { margin-bottom: 32px; }
.slot-copy { position: relative; z-index: 1; display: grid; gap: 3px; min-width: 0; }
.slot-copy small { color: var(--city-gold); font: 800 0.54rem/1 var(--font-mono, monospace); letter-spacing: 0.08em; text-transform: uppercase; }
.slot-copy strong { overflow: hidden; font: 700 1rem/1.05 Georgia, serif; text-overflow: ellipsis; white-space: nowrap; }
.slot-copy em { overflow: hidden; color: rgba(238, 229, 209, 0.56); font-size: 0.58rem; font-style: normal; text-overflow: ellipsis; white-space: nowrap; }
.slot-state { position: absolute; z-index: 2; top: 8px; right: 8px; display: inline-flex; align-items: center; gap: 4px; padding: 4px 6px; border-radius: 999px; color: #e8d7b1; background: rgba(10, 12, 9, 0.78); font: 800 0.5rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.municipality { grid-area: municipality; align-content: center; justify-items: center; padding: 16px; border-color: rgba(222, 194, 129, 0.4); text-align: center; background: radial-gradient(circle at 50% 35%, rgba(217, 183, 109, 0.2), transparent 45%), linear-gradient(155deg, #30291c, #171a14); }
.municipality-seal { display: grid; width: 64px; height: 64px; place-items: center; overflow: hidden; margin-bottom: 8px; border: 1px solid rgba(230, 202, 139, 0.36); border-radius: 50%; color: var(--city-gold-bright); background: rgba(9, 11, 8, 0.45); box-shadow: inset 0 0 22px rgba(201, 170, 103, 0.12); }
.municipality-seal img { width: 100%; height: 100%; object-fit: cover; }
.municipality > small { color: var(--city-gold); font: 800 0.55rem/1 var(--font-mono, monospace); letter-spacing: 0.08em; text-transform: uppercase; }
.municipality > strong { margin: 5px 0; font: 700 1.05rem/1.05 Georgia, serif; }
.municipality > em { color: rgba(238, 229, 209, 0.52); font: 700 0.58rem/1 var(--font-mono, monospace); font-style: normal; }
.municipality > span:last-child { display: inline-flex; align-items: center; gap: 4px; margin-top: 9px; color: #d9c388; font-size: 0.57rem; }
.municipality.locked { border-color: rgba(176, 92, 78, 0.34); }
.municipality.deferred { border-style: dashed; border-color: rgba(190, 132, 78, .38); filter: saturate(.55); }
.municipality.empty { border-style: dashed; color: rgba(238, 229, 209, 0.62); background: rgba(21, 24, 18, 0.79); }

.food-warning { display: flex; align-items: center; gap: 9px; padding: 10px 16px; border-top: 1px solid rgba(192, 100, 84, 0.25); color: #dfaa9f; background: rgba(151, 65, 52, 0.12); font-size: 0.71rem; }
.food-warning strong { color: #f0c5bc; }
.city-inaccessible { display: grid; min-height: 510px; place-content: center; justify-items: center; padding: 40px; color: #d89c90; text-align: center; background: repeating-linear-gradient(135deg, rgba(122, 47, 37, 0.055) 0 12px, transparent 13px 25px), #11130f; }
.city-inaccessible h2 { margin: 13px 0 6px; color: #efb8ad; font: 700 1.55rem/1 Georgia, serif; }
.city-inaccessible p { max-width: 520px; margin: 0; color: rgba(232, 183, 173, 0.72); font-size: 0.76rem; line-height: 1.5; }
.city-inaccessible span { margin-top: 9px; color: rgba(232, 183, 173, 0.46); font-size: 0.64rem; }
.city-empty-state { display: grid; min-height: 360px; place-content: center; justify-items: center; padding: 40px; color: rgba(238, 229, 209, 0.45); text-align: center; }
.city-empty-state h2 { margin: 13px 0 5px; color: #eee3ce; font: 700 1.5rem/1 Georgia, serif; }
.city-empty-state p { margin: 0; font-size: 0.75rem; }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; margin: -1px; padding: 0; border: 0; clip: rect(0, 0, 0, 0); white-space: nowrap; }

@media (max-width: 1080px) {
  .economy-strip { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .economy-stat:nth-child(2) { border-right: 0; }
  .economy-stat:nth-child(-n+2) { border-bottom: 1px solid var(--city-line); }
  .city-layout { grid-template-columns: 280px minmax(0, 1fr); }
  .city-scene { gap: 12px; padding: 22px; grid-template-columns: repeat(3, minmax(125px, 1fr)); }
}

@media (max-width: 820px) {
  .army-ledger-grid { grid-template-columns: 1fr; }
  .equipment-ledger { border-right: 0; border-bottom: 1px solid var(--city-line); }
  .city-layout { grid-template-columns: 1fr; }
  .improvement-drawer { order: 2; max-height: none; border-top: 1px solid var(--city-line); border-right: 0; }
  .drawer-empty { min-height: 220px; }
  .city-scene { min-height: 580px; }
}

@media (max-width: 600px) {
  .city-header { align-items: flex-start; padding: 14px; }
  .city-selector-row select { width: min(310px, 72vw); font-size: 1.18rem; }
  .city-title-block p { margin-left: 0; }
  .editor-flag { padding: 6px; font-size: 0; }
  .economy-stat { min-height: 74px; grid-template-columns: 20px 1fr; padding: 10px; }
  .economy-stat strong { grid-column: 2; grid-row: 2; justify-self: start; font-size: 1rem; }
  .economy-stat small { display: none; }
  .city-scene {
    min-height: auto;
    grid-template-areas:
      'municipality municipality'
      'farm lumber'
      'mine forge'
      'barracks unique';
    grid-template-columns: repeat(2, minmax(0, 1fr));
    grid-template-rows: 150px repeat(3, 145px);
    gap: 9px;
    padding: 13px;
  }
  .building-slot, .municipality { border-radius: 11px; padding: 9px; }
  .municipality-seal { width: 46px; height: 46px; }
  .slot-copy strong { font-size: 0.88rem; }
  .slot-state { top: 6px; right: 6px; max-width: 74px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
}
</style>
