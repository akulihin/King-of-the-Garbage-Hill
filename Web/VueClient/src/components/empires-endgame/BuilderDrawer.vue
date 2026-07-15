<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import {
  Braces,
  Building2,
  Check,
  Download,
  FlaskConical,
  Gift,
  LayoutTemplate,
  RotateCcw,
  Save,
  Settings2,
  SlidersHorizontal,
  ScrollText,
  Upload,
  X,
} from 'lucide-vue-next'
import { cloneEmpiresConfig, parseEmpiresConfig } from '../../features/empires-endgame/config'
import { EMPIRES_RANKS, EMPIRES_SUITS } from '../../features/empires-endgame/types'
import BuildingDependencyEditor from './BuildingDependencyEditor.vue'
import type {
  EmpiresCardDefinition,
  EmpiresBuildingDefinition,
  EmpiresEndgameConfig,
  EmpiresEventDefinition,
  EmpiresGiftDefinition,
  EmpiresRank,
  EmpiresSuit,
  EmpiresTechnologyDefinition,
} from '../../features/empires-endgame/types'

const props = defineProps<{
  config: EmpiresEndgameConfig
  dirty?: boolean
}>()

const emit = defineEmits<{
  'update:config': [config: EmpiresEndgameConfig]
  save: []
  reset: []
  export: []
  close: []
}>()

type BuilderTab = 'cards' | 'buildings' | 'technologies' | 'content' | 'rules' | 'json'
type BuildingGraphPatch = {
  buildingName?: string
  description?: string
  image?: string
  timeCostDays?: number
  foodCost?: number
  workerDemand?: number
}

const activeTab = ref<BuilderTab>('cards')
const selectedCardId = ref(props.config.cards[0]?.id ?? '')
const selectedBuildingId = ref(props.config.empire.buildings[0]?.id ?? '')
const selectedBuildingLevel = ref(props.config.empire.buildings[0]?.levels[0]?.level ?? 1)
const buildingEditorView = ref<'details' | 'graph'>('details')
const selectedTechnologyId = ref(props.config.empire.technologies[0]?.id ?? '')
const selectedGiftId = ref(props.config.gifts.definitions[0]?.id ?? '')
const selectedEventId = ref(props.config.empire.events[0]?.id ?? '')
const jsonDraft = ref('')
const jsonError = ref('')
const importInput = ref<HTMLInputElement | null>(null)

const selectedCard = computed(() => props.config.cards.find(card => card.id === selectedCardId.value)
  ?? props.config.cards[0]
  ?? null)
const selectedBuilding = computed(() => props.config.empire.buildings
  .find(building => building.id === selectedBuildingId.value)
  ?? props.config.empire.buildings[0]
  ?? null)
const selectedLevel = computed(() => selectedBuilding.value?.levels
  .find(level => level.level === selectedBuildingLevel.value)
  ?? selectedBuilding.value?.levels[0]
  ?? null)
const selectedTechnology = computed(() => props.config.empire.technologies
  .find(technology => technology.id === selectedTechnologyId.value)
  ?? props.config.empire.technologies[0]
  ?? null)
const selectedGift = computed(() => props.config.gifts.definitions
  .find(gift => gift.id === selectedGiftId.value)
  ?? props.config.gifts.definitions[0]
  ?? null)
const selectedEvent = computed(() => props.config.empire.events
  .find(event => event.id === selectedEventId.value)
  ?? props.config.empire.events[0]
  ?? null)
const buildingGraphNodes = computed(() => props.config.empire.buildings.flatMap((building, buildingIndex) => (
  building.levels.map(level => {
    const positioned = level as typeof level & { position?: { x: number, y: number } }
    return {
      id: `${building.id}:${level.level}`,
      buildingId: building.id,
      buildingName: level.name || building.name,
      level: level.level,
      slot: building.slot,
      x: positioned.position?.x ?? 10 + Math.min(level.level, 5) * 17,
      y: positioned.position?.y ?? 8 + buildingIndex % 8 * 11,
      dependencies: level.dependencies.flatMap(dependency => dependency.kind === 'building'
        ? [`${dependency.buildingId}:${dependency.level}`]
        : []),
      image: level.image ?? building.image,
      description: level.description,
      timeCostDays: level.timeCostDays,
      foodCost: level.foodCost,
      workerDemand: level.workerDemand ?? 0,
    }
  })
)))

watch(() => props.config, config => {
  if (!config.cards.some(card => card.id === selectedCardId.value)) {
    selectedCardId.value = config.cards[0]?.id ?? ''
  }
  if (!config.empire.buildings.some(building => building.id === selectedBuildingId.value)) {
    selectedBuildingId.value = config.empire.buildings[0]?.id ?? ''
  }
  const building = config.empire.buildings.find(item => item.id === selectedBuildingId.value)
  if (!building?.levels.some(level => level.level === selectedBuildingLevel.value)) {
    selectedBuildingLevel.value = building?.levels[0]?.level ?? 1
  }
  if (!config.empire.technologies.some(technology => technology.id === selectedTechnologyId.value)) {
    selectedTechnologyId.value = config.empire.technologies[0]?.id ?? ''
  }
  if (!config.gifts.definitions.some(gift => gift.id === selectedGiftId.value)) {
    selectedGiftId.value = config.gifts.definitions[0]?.id ?? ''
  }
  if (!config.empire.events.some(event => event.id === selectedEventId.value)) {
    selectedEventId.value = config.empire.events[0]?.id ?? ''
  }
  if (activeTab.value === 'json') jsonDraft.value = JSON.stringify(config, null, 2)
}, { deep: true })

watch(activeTab, tab => {
  if (tab === 'json') {
    jsonDraft.value = JSON.stringify(props.config, null, 2)
    jsonError.value = ''
  }
})

watch(selectedBuildingId, buildingId => {
  selectedBuildingLevel.value = props.config.empire.buildings
    .find(building => building.id === buildingId)?.levels[0]?.level ?? 1
})

function cloneConfig() {
  return cloneEmpiresConfig(props.config)
}

function updateRoot<K extends keyof EmpiresEndgameConfig>(key: K, value: EmpiresEndgameConfig[K]) {
  const next = cloneConfig()
  next[key] = value
  emit('update:config', next)
}

function updateCard(mutator: (card: EmpiresCardDefinition) => void) {
  if (!selectedCard.value) return
  const next = cloneConfig()
  const card = next.cards.find(candidate => candidate.id === selectedCard.value?.id)
  if (!card) return
  mutator(card)
  emit('update:config', next)
}

function updateBuilding(mutator: (building: EmpiresBuildingDefinition) => void) {
  if (!selectedBuilding.value) return
  const next = cloneConfig()
  const building = next.empire.buildings.find(candidate => candidate.id === selectedBuilding.value?.id)
  if (!building) return
  mutator(building)
  emit('update:config', next)
}

function updateBuildingLevel(mutator: (level: EmpiresBuildingDefinition['levels'][number]) => void) {
  updateBuilding(building => {
    const level = building.levels.find(candidate => candidate.level === selectedLevel.value?.level)
    if (level) mutator(level)
  })
}

function updateTechnology(mutator: (technology: EmpiresTechnologyDefinition) => void) {
  if (!selectedTechnology.value) return
  const next = cloneConfig()
  const technology = next.empire.technologies.find(candidate => candidate.id === selectedTechnology.value?.id)
  if (!technology) return
  mutator(technology)
  emit('update:config', next)
}

function updateGift(mutator: (gift: EmpiresGiftDefinition) => void) {
  if (!selectedGift.value) return
  const next = cloneConfig()
  const gift = next.gifts.definitions.find(candidate => candidate.id === selectedGift.value?.id)
  if (!gift) return
  mutator(gift)
  emit('update:config', next)
}

function updateEvent(mutator: (event: EmpiresEventDefinition) => void) {
  if (!selectedEvent.value) return
  const next = cloneConfig()
  const event = next.empire.events.find(candidate => candidate.id === selectedEvent.value?.id)
  if (!event) return
  mutator(event)
  emit('update:config', next)
}

function selectBuildingGraphNode(nodeId: string) {
  const separator = nodeId.lastIndexOf(':')
  const buildingId = nodeId.slice(0, separator)
  const level = Number(nodeId.slice(separator + 1))
  if (!props.config.empire.buildings.some(building => building.id === buildingId)) return
  selectedBuildingId.value = buildingId
  selectedBuildingLevel.value = level
}

function moveBuildingGraphNode(nodeId: string, x: number, y: number) {
  const separator = nodeId.lastIndexOf(':')
  const buildingId = nodeId.slice(0, separator)
  const levelNumber = Number(nodeId.slice(separator + 1))
  const next = cloneConfig()
  const level = next.empire.buildings.find(building => building.id === buildingId)?.levels
    .find(candidate => candidate.level === levelNumber) as (EmpiresBuildingDefinition['levels'][number] & { position?: { x: number, y: number } }) | undefined
  if (!level) return
  level.position = { x, y }
  emit('update:config', next)
}

function toggleBuildingDependency(fromId: string, toId: string) {
  const fromSeparator = fromId.lastIndexOf(':')
  const toSeparator = toId.lastIndexOf(':')
  const sourceBuildingId = fromId.slice(0, fromSeparator)
  const sourceLevel = Number(fromId.slice(fromSeparator + 1))
  const targetBuildingId = toId.slice(0, toSeparator)
  const targetLevel = Number(toId.slice(toSeparator + 1))
  const next = cloneConfig()
  const target = next.empire.buildings.find(building => building.id === targetBuildingId)?.levels
    .find(level => level.level === targetLevel)
  if (!target) return
  const existing = target.dependencies.findIndex(dependency => dependency.kind === 'building'
    && dependency.buildingId === sourceBuildingId
    && dependency.level === sourceLevel)
  if (existing >= 0) target.dependencies.splice(existing, 1)
  else target.dependencies.push({ kind: 'building', buildingId: sourceBuildingId, level: sourceLevel, scope: 'sameCity' })
  emit('update:config', next)
}

function addBuildingGraphNode(position?: { x: number, y: number }) {
  const next = cloneConfig()
  const building = next.empire.buildings.find(item => item.id === selectedBuildingId.value)
    ?? next.empire.buildings[0]
  if (!building) return
  const levelNumber = Math.max(0, ...building.levels.map(level => level.level)) + 1
  const level: EmpiresBuildingDefinition['levels'][number] & { position?: { x: number, y: number } } = {
    level: levelNumber,
    name: `${building.name} ${levelNumber}`,
    description: 'Новое улучшение здания.',
    timeCostDays: 1,
    foodCost: 0,
    resourceCosts: [],
    dependencies: [],
    facilityLocks: [],
    workerDemand: 0,
    production: [],
    effects: [],
    position: position ?? { x: 15 + Math.min(levelNumber, 4) * 18, y: 50 },
  }
  building.levels.push(level)
  selectedBuildingId.value = building.id
  selectedBuildingLevel.value = levelNumber
  emit('update:config', next)
}

function deleteBuildingGraphNode(nodeId: string) {
  const separator = nodeId.lastIndexOf(':')
  const buildingId = nodeId.slice(0, separator)
  const levelNumber = Number(nodeId.slice(separator + 1))
  const next = cloneConfig()
  const building = next.empire.buildings.find(item => item.id === buildingId)
  if (!building || building.levels.length <= 1) return
  building.levels = building.levels.filter(level => level.level !== levelNumber)
  for (const definition of next.empire.buildings) {
    for (const level of definition.levels) {
      level.dependencies = level.dependencies.filter(dependency => dependency.kind !== 'building'
        || dependency.buildingId !== buildingId
        || dependency.level !== levelNumber)
    }
  }
  selectedBuildingLevel.value = building.levels[0]?.level ?? 1
  emit('update:config', next)
}

function updateBuildingGraphNode(
  nodeId: string,
  patch: BuildingGraphPatch,
) {
  const separator = nodeId.lastIndexOf(':')
  const buildingId = nodeId.slice(0, separator)
  const levelNumber = Number(nodeId.slice(separator + 1))
  const next = cloneConfig()
  const level = next.empire.buildings.find(building => building.id === buildingId)?.levels
    .find(candidate => candidate.level === levelNumber)
  if (!level) return
  if (typeof patch.buildingName === 'string') level.name = patch.buildingName
  if (typeof patch.description === 'string') level.description = patch.description
  if ('image' in patch) level.image = patch.image
  if (typeof patch.timeCostDays === 'number') level.timeCostDays = patch.timeCostDays
  if (typeof patch.foodCost === 'number') level.foodCost = patch.foodCost
  if (typeof patch.workerDemand === 'number') level.workerDemand = patch.workerDemand
  emit('update:config', next)
}

function swapCardSuit(value: string) {
  const nextSuit = value as EmpiresSuit
  if (!selectedCard.value || selectedCard.value.suit === 'joker' || selectedCard.value.suit === nextSuit) return
  const next = cloneConfig()
  const card = next.cards.find(candidate => candidate.id === selectedCard.value?.id)
  const counterpart = next.cards.find(candidate => candidate.suit === nextSuit && candidate.rank === card?.rank)
  if (!card || !counterpart || counterpart.suit === 'joker') return
  const previousSuit = card.suit
  card.suit = nextSuit
  counterpart.suit = previousSuit
  emit('update:config', next)
}

function swapCardRank(value: string) {
  const nextRank = value as EmpiresRank
  if (!selectedCard.value || selectedCard.value.rank === 'joker' || selectedCard.value.rank === nextRank) return
  const next = cloneConfig()
  const card = next.cards.find(candidate => candidate.id === selectedCard.value?.id)
  const counterpart = next.cards.find(candidate => candidate.rank === nextRank && candidate.suit === card?.suit)
  if (!card || !counterpart || counterpart.rank === 'joker') return
  const previousRank = card.rank
  card.rank = nextRank
  counterpart.rank = previousRank
  emit('update:config', next)
}

function updateEmpireNumber(key: 'daysPerPhase' | 'eventChance' | 'defeatPopulationAtOrBelow', value: number) {
  const next = cloneConfig()
  next.empire[key] = value
  emit('update:config', next)
}

function updateDurakNumber(key: 'handSize' | 'maxAttackCards' | 'boutsPerCon', value: number) {
  const next = cloneConfig()
  next.durak[key] = value
  emit('update:config', next)
}

function applyJson() {
  try {
    emit('update:config', parseEmpiresConfig(jsonDraft.value))
    jsonError.value = ''
  }
  catch (error) {
    jsonError.value = error instanceof Error ? error.message : 'JSON не удалось применить.'
  }
}

async function importJson(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  try {
    const next = parseEmpiresConfig(await file.text())
    emit('update:config', next)
    jsonDraft.value = JSON.stringify(next, null, 2)
    jsonError.value = ''
  }
  catch (error) {
    jsonError.value = error instanceof Error ? error.message : 'Файл не удалось импортировать.'
  }
  ;(event.target as HTMLInputElement).value = ''
}
</script>

<template>
  <aside class="builder-drawer" data-testid="constructor-drawer" aria-label="Конструктор Empire's Endgame">
    <header>
      <div>
        <span>Встроенный конструктор</span>
        <h2><Settings2 :size="20" /> Сценарий игры</h2>
      </div>
      <button class="icon-button" type="button" aria-label="Закрыть конструктор" @click="emit('close')"><X :size="20" /></button>
    </header>

    <nav aria-label="Разделы конструктора">
      <button type="button" :class="{ active: activeTab === 'cards' }" @click="activeTab = 'cards'">
        <LayoutTemplate :size="15" /> Карты
      </button>
      <button type="button" :class="{ active: activeTab === 'buildings' }" @click="activeTab = 'buildings'">
        <Building2 :size="15" /> Здания
      </button>
      <button type="button" :class="{ active: activeTab === 'technologies' }" @click="activeTab = 'technologies'">
        <FlaskConical :size="15" /> Развитие
      </button>
      <button type="button" :class="{ active: activeTab === 'content' }" @click="activeTab = 'content'">
        <Gift :size="15" /> Дары и события
      </button>
      <button type="button" :class="{ active: activeTab === 'rules' }" @click="activeTab = 'rules'">
        <SlidersHorizontal :size="15" /> Правила
      </button>
      <button type="button" :class="{ active: activeTab === 'json' }" @click="activeTab = 'json'">
        <Braces :size="15" /> Весь JSON
      </button>
    </nav>

    <div class="builder-body">
      <section v-if="activeTab === 'cards'" class="builder-section card-builder">
        <label class="full-field">
          <span>Карта (полная колода из 53 карт)</span>
          <select v-model="selectedCardId">
            <option v-for="card in config.cards" :key="card.id" :value="card.id">
              {{ card.rank }} {{ card.suit }} · {{ card.name }}
            </option>
          </select>
        </label>

        <template v-if="selectedCard">
          <div class="field-grid">
            <label><span>Название</span><input :value="selectedCard.name" @input="updateCard(card => card.name = ($event.target as HTMLInputElement).value)" /></label>
            <label><span>ID</span><input :value="selectedCard.id" disabled /></label>
            <label><span>Масть</span><select :value="selectedCard.suit" :disabled="selectedCard.suit === 'joker'" @change="swapCardSuit(($event.target as HTMLSelectElement).value)"><option v-for="suit in EMPIRES_SUITS" :key="suit" :value="suit">{{ suit }}</option><option v-if="selectedCard.suit === 'joker'" value="joker">joker</option></select></label>
            <label><span>Достоинство</span><select :value="selectedCard.rank" :disabled="selectedCard.rank === 'joker'" @change="swapCardRank(($event.target as HTMLSelectElement).value)"><option v-for="rank in EMPIRES_RANKS" :key="rank" :value="rank">{{ rank }}</option><option v-if="selectedCard.rank === 'joker'" value="joker">joker</option></select></label>
            <label><span>Цена времени</span><input type="number" min="0" :value="selectedCard.timeCostDays" @input="updateCard(card => card.timeCostDays = Number(($event.target as HTMLInputElement).value))" /></label>
            <label><span>Ценность</span><input type="number" :value="selectedCard.value" @input="updateCard(card => card.value = Number(($event.target as HTMLInputElement).value))" /></label>
            <label><span>Улучшение при доборе</span><input type="number" min="0" :value="selectedCard.drawUpgrade" @input="updateCard(card => card.drawUpgrade = Number(($event.target as HTMLInputElement).value))" /></label>
            <label><span>Максимальный уровень</span><input type="number" min="0" :value="selectedCard.maxLevel ?? config.upgrades.defaultMaxLevel" @input="updateCard(card => card.maxLevel = Number(($event.target as HTMLInputElement).value))" /></label>
          </div>

          <article class="face-editor normal-face">
            <h3>Прямая сторона</h3>
            <label><span>Заголовок пассива</span><input :value="selectedCard.normal.title" @input="updateCard(card => card.normal.title = ($event.target as HTMLInputElement).value)" /></label>
            <label><span>Описание</span><textarea rows="4" :value="selectedCard.normal.description" @input="updateCard(card => card.normal.description = ($event.target as HTMLTextAreaElement).value)" /></label>
            <label><span>URL изображения (необязательно)</span><input :value="selectedCard.normal.image ?? ''" @input="updateCard(card => card.normal.image = ($event.target as HTMLInputElement).value || undefined)" /></label>
          </article>

          <article class="face-editor inverted-face">
            <h3>Перевёрнутая сторона</h3>
            <label><span>Заголовок пассива</span><input :value="selectedCard.inverted.title" @input="updateCard(card => card.inverted.title = ($event.target as HTMLInputElement).value)" /></label>
            <label><span>Описание</span><textarea rows="4" :value="selectedCard.inverted.description" @input="updateCard(card => card.inverted.description = ($event.target as HTMLTextAreaElement).value)" /></label>
            <label><span>URL изображения (необязательно)</span><input :value="selectedCard.inverted.image ?? ''" @input="updateCard(card => card.inverted.image = ($event.target as HTMLInputElement).value || undefined)" /></label>
          </article>
          <p class="builder-note">Эффекты обеих сторон, новые дары, события, города, здания и узлы технологий доступны во вкладке «Весь JSON». Позиции карты и дерева также меняются перетаскиванием прямо в игре.</p>
        </template>
      </section>

      <section v-else-if="activeTab === 'buildings'" class="builder-section">
        <label class="full-field">
          <span>Здание</span>
          <select v-model="selectedBuildingId">
            <option v-for="building in config.empire.buildings" :key="building.id" :value="building.id">
              {{ building.name }} · {{ building.slot }}
            </option>
          </select>
        </label>
        <div class="subview-toggle" role="group" aria-label="Режим редактора зданий">
          <button type="button" :class="{ active: buildingEditorView === 'details' }" @click="buildingEditorView = 'details'">Параметры</button>
          <button type="button" :class="{ active: buildingEditorView === 'graph' }" @click="buildingEditorView = 'graph'">Граф зависимостей</button>
        </div>

        <template v-if="selectedBuilding && buildingEditorView === 'details'">
          <div class="field-grid">
            <label><span>Название</span><input :value="selectedBuilding.name" @input="updateBuilding(building => building.name = ($event.target as HTMLInputElement).value)" /></label>
            <label><span>ID</span><input :value="selectedBuilding.id" disabled /></label>
            <label><span>Слот</span><select :value="selectedBuilding.slot" @change="updateBuilding(building => building.slot = ($event.target as HTMLSelectElement).value as EmpiresBuildingDefinition['slot'])"><option v-for="slot in ['farm', 'lumber', 'mine', 'smithy', 'barracks', 'unique', 'municipal']" :key="slot" :value="slot">{{ slot }}</option></select></label>
            <label><span>URL изображения здания</span><input :value="selectedBuilding.image ?? ''" @input="updateBuilding(building => building.image = ($event.target as HTMLInputElement).value || undefined)" /></label>
          </div>

          <label class="full-field">
            <span>Уровень / улучшение</span>
            <select v-model.number="selectedBuildingLevel">
              <option v-for="level in selectedBuilding.levels" :key="level.level" :value="level.level">Уровень {{ level.level }} · {{ level.name || selectedBuilding.name }}</option>
            </select>
          </label>

          <article v-if="selectedLevel" class="face-editor normal-face">
            <h3>Характеристики уровня {{ selectedLevel.level }}</h3>
            <div class="field-grid">
              <label><span>Название улучшения</span><input :value="selectedLevel.name ?? ''" @input="updateBuildingLevel(level => level.name = ($event.target as HTMLInputElement).value || undefined)" /></label>
              <label><span>URL изображения</span><input :value="selectedLevel.image ?? ''" @input="updateBuildingLevel(level => level.image = ($event.target as HTMLInputElement).value || undefined)" /></label>
              <label><span>Дней</span><input type="number" min="0" :value="selectedLevel.timeCostDays" @input="updateBuildingLevel(level => level.timeCostDays = Number(($event.target as HTMLInputElement).value))" /></label>
              <label><span>Рабочих</span><input type="number" min="0" :value="selectedLevel.workerDemand ?? 0" @input="updateBuildingLevel(level => level.workerDemand = Number(($event.target as HTMLInputElement).value))" /></label>
              <label><span>Цена едой</span><input type="number" min="0" :value="selectedLevel.foodCost" @input="updateBuildingLevel(level => level.foodCost = Number(($event.target as HTMLInputElement).value))" /></label>
            </div>
            <label><span>Описание</span><textarea rows="4" :value="selectedLevel.description ?? ''" @input="updateBuildingLevel(level => level.description = ($event.target as HTMLTextAreaElement).value || undefined)" /></label>
          </article>
          <p class="builder-note">Зависимости уровней редактируются в соседнем графе. Ресурсы, производительность, блокировки шахты/лесопилки и эффекты уровня доступны во вкладке «Весь JSON».</p>
        </template>
        <BuildingDependencyEditor
          v-else-if="selectedBuilding"
          :nodes="buildingGraphNodes"
          :selected-id="`${selectedBuilding.id}:${selectedBuildingLevel}`"
          editable
          @select="selectBuildingGraphNode"
          @move-node="moveBuildingGraphNode"
          @toggle-dependency="toggleBuildingDependency"
          @add-node="addBuildingGraphNode"
          @delete-node="deleteBuildingGraphNode"
          @update-node="updateBuildingGraphNode"
        />
      </section>

      <section v-else-if="activeTab === 'technologies'" class="builder-section">
        <label class="full-field">
          <span>Технология / реформа</span>
          <select v-model="selectedTechnologyId">
            <option v-for="technology in config.empire.technologies" :key="technology.id" :value="technology.id">{{ technology.name }} · {{ technology.category }}</option>
          </select>
        </label>

        <template v-if="selectedTechnology">
          <div class="field-grid">
            <label><span>Название</span><input :value="selectedTechnology.name" @input="updateTechnology(technology => technology.name = ($event.target as HTMLInputElement).value)" /></label>
            <label><span>ID</span><input :value="selectedTechnology.id" disabled /></label>
            <label><span>Категория</span><select :value="selectedTechnology.category" @change="updateTechnology(technology => technology.category = ($event.target as HTMLSelectElement).value as EmpiresTechnologyDefinition['category'])"><option v-for="category in ['technology', 'reform', 'doctrine', 'steel']" :key="category" :value="category">{{ category }}</option></select></label>
            <label><span>Ветка</span><input :value="selectedTechnology.groupId ?? ''" @input="updateTechnology(technology => technology.groupId = ($event.target as HTMLInputElement).value || undefined)" /></label>
            <label><span>Дней</span><input type="number" min="0" :value="selectedTechnology.timeCostDays" @input="updateTechnology(technology => technology.timeCostDays = Number(($event.target as HTMLInputElement).value))" /></label>
            <label><span>Уровень</span><input type="number" min="0" :value="selectedTechnology.tier ?? 0" @input="updateTechnology(technology => technology.tier = Number(($event.target as HTMLInputElement).value))" /></label>
            <label class="full-field"><span>URL изображения</span><input :value="selectedTechnology.image ?? ''" @input="updateTechnology(technology => technology.image = ($event.target as HTMLInputElement).value || undefined)" /></label>
          </div>
          <label><span>Описание и характеристики</span><textarea rows="6" :value="selectedTechnology.description ?? ''" @input="updateTechnology(technology => technology.description = ($event.target as HTMLTextAreaElement).value || undefined)" /></label>
          <p class="builder-note">Положение ноды меняется перетаскиванием на дереве, а связи — кнопкой «Связать ноды». Цены, эффекты и сложные условия доступны во вкладке «Весь JSON».</p>
        </template>
      </section>

      <section v-else-if="activeTab === 'content'" class="builder-section">
        <article class="content-editor">
          <h3><Gift :size="16" /> Божественные дары</h3>
          <label class="full-field"><span>Дар</span><select v-model="selectedGiftId"><option v-for="gift in config.gifts.definitions" :key="gift.id" :value="gift.id">{{ gift.name }} · {{ gift.rarity || gift.kind }}</option></select></label>
          <template v-if="selectedGift">
            <div class="field-grid">
              <label><span>Название</span><input :value="selectedGift.name" @input="updateGift(gift => gift.name = ($event.target as HTMLInputElement).value)" /></label>
              <label><span>URL изображения</span><input :value="selectedGift.image ?? ''" @input="updateGift(gift => gift.image = ($event.target as HTMLInputElement).value || undefined)" /></label>
              <label><span>Базовый вес</span><input type="number" min="0" step="0.1" :value="selectedGift.baseWeight" @input="updateGift(gift => gift.baseWeight = Number(($event.target as HTMLInputElement).value))" /></label>
              <label><span>Вес за перфоманс</span><input type="number" step="0.1" :value="selectedGift.performanceWeight" @input="updateGift(gift => gift.performanceWeight = Number(($event.target as HTMLInputElement).value))" /></label>
              <label><span>Применение</span><select :value="selectedGift.application" @change="updateGift(gift => gift.application = ($event.target as HTMLSelectElement).value as EmpiresGiftDefinition['application'])"><option value="once">один раз</option><option value="eachEmpire">каждую фазу</option></select></label>
            </div>
            <label><span>Описание</span><textarea rows="4" :value="selectedGift.description" @input="updateGift(gift => gift.description = ($event.target as HTMLTextAreaElement).value)" /></label>
          </template>
        </article>

        <article class="content-editor">
          <h3><ScrollText :size="16" /> Случайные события</h3>
          <label class="full-field"><span>Событие</span><select v-model="selectedEventId"><option v-for="event in config.empire.events" :key="event.id" :value="event.id">{{ event.name }}</option></select></label>
          <template v-if="selectedEvent">
            <div class="field-grid">
              <label><span>Название</span><input :value="selectedEvent.name" @input="updateEvent(event => event.name = ($event.target as HTMLInputElement).value)" /></label>
              <label><span>Вес выпадения</span><input type="number" min="0" step="0.1" :value="selectedEvent.weight" @input="updateEvent(event => event.weight = Number(($event.target as HTMLInputElement).value))" /></label>
              <label><span>Минимальный кон</span><input type="number" min="1" :value="selectedEvent.minimumCon ?? 1" @input="updateEvent(event => event.minimumCon = Number(($event.target as HTMLInputElement).value))" /></label>
              <label><span>Максимальный кон</span><input type="number" min="1" :value="selectedEvent.maximumCon ?? ''" @input="updateEvent(event => event.maximumCon = ($event.target as HTMLInputElement).value === '' ? undefined : Number(($event.target as HTMLInputElement).value))" /></label>
            </div>
            <label><span>Описание</span><textarea rows="4" :value="selectedEvent.description" @input="updateEvent(event => event.description = ($event.target as HTMLTextAreaElement).value)" /></label>
            <div class="choice-editor" v-for="(choice, index) in selectedEvent.choices" :key="choice.id">
              <strong>Вариант {{ index + 1 }}</strong>
              <label><span>Кнопка</span><input :value="choice.label" @input="updateEvent(event => { const target = event.choices[index]; if (target) target.label = ($event.target as HTMLInputElement).value })" /></label>
              <label><span>Пояснение</span><textarea rows="2" :value="choice.description ?? ''" @input="updateEvent(event => { const target = event.choices[index]; if (target) target.description = ($event.target as HTMLTextAreaElement).value || undefined })" /></label>
            </div>
          </template>
        </article>
        <p class="builder-note">Эффекты, цены вариантов и новые элементы каталогов редактируются во вкладке «Весь JSON».</p>
      </section>

      <section v-else-if="activeTab === 'rules'" class="builder-section">
        <label class="full-field"><span>Название сценария</span><input :value="config.title" @input="updateRoot('title', ($event.target as HTMLInputElement).value)" /></label>
        <label class="full-field"><span>Seed новой кампании</span><input :value="String(config.seed)" @input="updateRoot('seed', ($event.target as HTMLInputElement).value)" /></label>
        <div class="field-grid">
          <label><span>Карт на руке</span><input type="number" min="1" :value="config.durak.handSize" @input="updateDurakNumber('handSize', Number(($event.target as HTMLInputElement).value))" /></label>
          <label><span>Макс. карт в атаке</span><input type="number" min="1" :value="config.durak.maxAttackCards" @input="updateDurakNumber('maxAttackCards', Number(($event.target as HTMLInputElement).value))" /></label>
          <label><span>Партий в коне</span><input type="number" min="1" :value="config.durak.boutsPerCon" @input="updateDurakNumber('boutsPerCon', Number(($event.target as HTMLInputElement).value))" /></label>
          <label><span>Дней имперской фазы</span><input type="number" min="1" :value="config.empire.daysPerPhase" @input="updateEmpireNumber('daysPerPhase', Number(($event.target as HTMLInputElement).value))" /></label>
          <label><span>Шанс события (0–1)</span><input type="number" min="0" max="1" step="0.05" :value="config.empire.eventChance" @input="updateEmpireNumber('eventChance', Number(($event.target as HTMLInputElement).value))" /></label>
          <label><span>Порог поражения</span><input type="number" min="0" :value="config.empire.defeatPopulationAtOrBelow" @input="updateEmpireNumber('defeatPopulationAtOrBelow', Number(($event.target as HTMLInputElement).value))" /></label>
        </div>
        <div class="catalog-summary">
          <span><b>{{ config.cards.length }}</b> карт</span>
          <span><b>{{ config.gifts.definitions.length }}</b> даров</span>
          <span><b>{{ config.empire.map.regions.length }}</b> регионов</span>
          <span><b>{{ config.empire.cities.length }}</b> городов</span>
          <span><b>{{ config.empire.buildings.length }}</b> зданий</span>
          <span><b>{{ config.empire.units?.length ?? 0 }}</b> видов войск</span>
          <span><b>{{ config.empire.technologies.length }}</b> технологий</span>
          <span><b>{{ config.empire.events.length }}</b> событий</span>
        </div>
      </section>

      <section v-else class="builder-section json-builder">
        <div class="json-actions">
          <button type="button" @click="importInput?.click()"><Upload :size="14" /> Импорт</button>
          <button type="button" @click="emit('export')"><Download :size="14" /> Экспорт</button>
          <input ref="importInput" type="file" accept="application/json,.json" hidden @change="importJson" />
        </div>
        <label>
          <span>Полная конфигурация</span>
          <textarea v-model="jsonDraft" class="json-editor" spellcheck="false" />
        </label>
        <p v-if="jsonError" class="json-error">{{ jsonError }}</p>
        <button class="apply-json" type="button" @click="applyJson"><Check :size="15" /> Проверить и применить</button>
      </section>
    </div>

    <footer>
      <button class="reset-button" data-testid="constructor-reset" type="button" @click="emit('reset')"><RotateCcw :size="15" /> Сбросить</button>
      <button class="save-button" data-testid="constructor-save" type="button" @click="emit('save')"><Save :size="15" /> {{ dirty ? 'Сохранить изменения' : 'Сохранено' }}</button>
    </footer>
  </aside>
</template>

<style scoped>
.builder-drawer { position: fixed; z-index: 80; top: 0; right: 0; display: grid; width: min(590px, 100vw); height: 100dvh; grid-template-rows: auto auto minmax(0, 1fr) auto; color: #eee4ce; background: #151713; box-shadow: -24px 0 70px rgba(0, 0, 0, .55); }
.builder-drawer > header { display: flex; align-items: center; justify-content: space-between; padding: 18px 20px 14px; border-bottom: 1px solid rgba(221, 195, 136, .16); background: linear-gradient(120deg, #25251d, #171b18); }
.builder-drawer > header span { color: #bda66d; font: 800 .6rem/1 monospace; letter-spacing: .13em; text-transform: uppercase; }
.builder-drawer h2 { display: flex; align-items: center; gap: 8px; margin: 5px 0 0; font: 700 1.35rem/1.1 Georgia, serif; }
button { color: inherit; font: inherit; }
.icon-button { display: grid; width: 38px; height: 38px; place-items: center; border: 1px solid rgba(224, 201, 151, .2); border-radius: 50%; background: rgba(255, 255, 255, .04); cursor: pointer; }
.builder-drawer > nav { display: flex; gap: 5px; overflow-x: auto; padding: 9px 12px; border-bottom: 1px solid rgba(221, 195, 136, .11); background: #11130f; }
.builder-drawer > nav button { display: flex; flex: 0 0 auto; align-items: center; justify-content: center; gap: 6px; padding: 9px 11px; border: 1px solid transparent; border-radius: 7px; color: rgba(238, 228, 206, .58); background: transparent; cursor: pointer; }
.builder-drawer > nav button.active { border-color: rgba(205, 177, 112, .28); color: #ecd79f; background: rgba(205, 177, 112, .09); }
.builder-body { overflow: auto; padding: 18px; }
.builder-section { display: grid; gap: 15px; }
label { display: grid; gap: 6px; min-width: 0; }
label > span { color: rgba(238, 228, 206, .58); font: 700 .64rem/1 monospace; }
input, select, textarea { box-sizing: border-box; width: 100%; border: 1px solid rgba(218, 194, 142, .2); border-radius: 7px; outline: none; color: #f1e7d3; background: #0d100d; font: 500 .8rem/1.3 inherit; }
input, select { height: 38px; padding: 0 10px; }
textarea { resize: vertical; padding: 9px 10px; }
input:focus, select:focus, textarea:focus { border-color: #b99d5e; box-shadow: 0 0 0 2px rgba(185, 157, 94, .1); }
input:disabled { opacity: .55; }
.field-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.subview-toggle { display: grid; grid-template-columns: 1fr 1fr; gap: 5px; padding: 4px; border: 1px solid rgba(218, 194, 142, .13); border-radius: 8px; background: #0d100d; }
.subview-toggle button { min-height: 34px; border: 1px solid transparent; border-radius: 6px; color: rgba(238, 228, 206, .5); background: transparent; cursor: pointer; }
.subview-toggle button.active { border-color: rgba(205, 177, 112, .3); color: #ecd79f; background: rgba(205, 177, 112, .1); }
.face-editor { display: grid; gap: 10px; padding: 14px; border: 1px solid rgba(222, 196, 142, .16); border-radius: 10px; background: rgba(255, 255, 255, .025); }
.face-editor h3 { margin: 0; color: #d8bf80; font: 700 .95rem/1 Georgia, serif; }
.content-editor { display: grid; gap: 11px; padding: 14px; border: 1px solid rgba(222, 196, 142, .16); border-radius: 10px; background: rgba(255, 255, 255, .025); }
.content-editor h3 { display: flex; align-items: center; gap: 7px; margin: 0; color: #d8bf80; font: 700 1rem/1 Georgia, serif; }
.choice-editor { display: grid; gap: 8px; padding: 10px; border-left: 2px solid rgba(185, 157, 94, .45); background: rgba(185, 157, 94, .04); }
.choice-editor > strong { color: rgba(238, 228, 206, .68); font-size: .7rem; }
.inverted-face { border-color: rgba(160, 91, 128, .26); background: rgba(90, 39, 68, .09); }
.inverted-face h3 { color: #d6a6bf; }
.builder-note { margin: 0; padding: 10px 12px; border-left: 2px solid #927d4e; color: rgba(238, 228, 206, .52); background: rgba(185, 157, 94, .05); font-size: .69rem; line-height: 1.45; }
.catalog-summary { display: grid; grid-template-columns: repeat(3, 1fr); gap: 7px; }
.catalog-summary span { padding: 12px; border: 1px solid rgba(218, 194, 142, .13); border-radius: 8px; color: rgba(238, 228, 206, .55); background: rgba(255, 255, 255, .025); font-size: .68rem; }
.catalog-summary b { display: block; margin-bottom: 3px; color: #ddc47f; font: 800 1.25rem/1 Georgia, serif; }
.json-actions { display: flex; justify-content: flex-end; gap: 7px; }
.json-actions button, .apply-json { display: inline-flex; align-items: center; gap: 5px; padding: 8px 11px; border: 1px solid rgba(218, 194, 142, .2); border-radius: 7px; background: rgba(255, 255, 255, .04); cursor: pointer; }
.json-editor { min-height: 57vh; resize: vertical; color: #cbd5bc; font: 500 .66rem/1.5 ui-monospace, SFMono-Regular, Consolas, monospace; tab-size: 2; }
.json-error { margin: 0; padding: 9px 11px; border: 1px solid rgba(196, 87, 91, .3); border-radius: 7px; color: #efb2b5; background: rgba(126, 39, 44, .13); font-size: .7rem; }
.apply-json { justify-self: end; border-color: rgba(116, 161, 112, .32); color: #d5e8c5; background: rgba(72, 112, 67, .13); }
.builder-drawer > footer { display: flex; justify-content: space-between; gap: 10px; padding: 13px 18px; border-top: 1px solid rgba(221, 195, 136, .14); background: #10120f; }
.builder-drawer > footer button { display: inline-flex; align-items: center; justify-content: center; gap: 6px; min-height: 40px; padding: 0 15px; border-radius: 7px; cursor: pointer; }
.reset-button { border: 1px solid rgba(218, 194, 142, .16); color: rgba(238, 228, 206, .62); background: transparent; }
.save-button { border: 1px solid #9b8049; color: #251f15; background: linear-gradient(#e2c77e, #b99a54); font-weight: 800; }
@media (max-width: 560px) { .field-grid, .catalog-summary { grid-template-columns: 1fr; } .builder-body { padding: 13px; } }
</style>
