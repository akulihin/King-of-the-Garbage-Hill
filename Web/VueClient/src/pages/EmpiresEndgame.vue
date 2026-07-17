<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef, triggerRef } from 'vue'
import {
  BookOpen,
  Braces,
  Building2,
  CalendarDays,
  CircleHelp,
  Coins,
  Crown,
  Download,
  FlaskConical,
  Gift,
  Landmark,
  Map as MapIcon,
  Play,
  RotateCcw,
  ScrollText,
  Settings2,
  Shield,
  Sparkles,
  Swords,
  Upload,
  Wheat,
} from 'lucide-vue-next'
import BuilderDrawer from '../components/empires-endgame/BuilderDrawer.vue'
import CityView from '../components/empires-endgame/CityView.vue'
import DurakTable from '../components/empires-endgame/DurakTable.vue'
import EmpireCard from '../components/empires-endgame/EmpireCard.vue'
import EmpireMap from '../components/empires-endgame/EmpireMap.vue'
import EventDialog from '../components/empires-endgame/EventDialog.vue'
import GiftDraft from '../components/empires-endgame/GiftDraft.vue'
import PopulationDialog from '../components/empires-endgame/PopulationDialog.vue'
import TargetResolutionDialog, {
  type TargetResolutionOption,
} from '../components/empires-endgame/TargetResolutionDialog.vue'
import TechTree from '../components/empires-endgame/TechTree.vue'
import TdBattle from '../components/empires-endgame/TdBattle.vue'
import {
  clearCustomEmpiresConfig,
  cloneEmpiresConfig,
  downloadEmpiresJson,
  empiresConfigReplacementDisabledReason,
  loadBundledEmpiresConfig,
  loadEmpiresConfig,
  saveEmpiresConfig,
  validateEmpiresConfig,
} from '../features/empires-endgame/config'
import { EmpiresEndgameEngine } from '../features/empires-endgame/engine'
import {
  createEmpiresQaScenarios,
  digestEmpiresQaState,
  EMPIRES_QA_SCENARIO_NAMES,
  runEmpiresQaAutoplay,
  type EmpiresQaScenarioFixture,
  type EmpiresQaScenarioName,
} from '../features/empires-endgame/qa'
import {
  clearEmpiresCampaign,
  importEmpiresCampaign,
  loadEmpiresCampaign,
  saveEmpiresCampaign,
} from '../features/empires-endgame/persistence'
import type {
  EmpiresActionResult,
  EmpiresBuildingDefinition,
  EmpiresBuildingLevelDefinition,
  EmpiresCampaignState,
  EmpiresCardInstance,
  EmpiresCityState,
  EmpiresDependency,
  EmpiresEffect,
  EmpiresEndgameConfig,
  EmpiresMapObjectDefinition,
  EmpiresPendingGiftResolution,
  EmpiresPoint,
  TdBattleResult,
  TdCommand,
} from '../features/empires-endgame/types'

type EmpireTab = 'map' | 'city' | 'technology' | 'council'

const config = ref<EmpiresEndgameConfig | null>(null)
const editorConfig = ref<EmpiresEndgameConfig | null>(null)
const engine = shallowRef<EmpiresEndgameEngine | null>(null)
const state = ref<EmpiresCampaignState | null>(null)
const loading = ref(true)
const fatalError = ref('')
const fatalSaveRecoverable = ref(false)
const lastMessage = ref('Добро пожаловать на последнюю игру империи.')
const godBusy = ref(false)
const editorOpen = ref(false)
const editorDirty = ref(false)
const activeEmpireTab = ref<EmpireTab>('map')
const activeRegionId = ref('')
const activeCityId = ref('')
const selectedBuildingId = ref<string | null>(null)
const selectedTechnologyId = ref<string | null>(null)
const selectedCouncilCardId = ref<string | null>(null)
const populationCityId = ref<string | null>(null)
const recruitQuantities = ref<Record<string, number>>({})
const saveInput = ref<HTMLInputElement | null>(null)
const qaMode = ref(false)
const qaScenarioName = ref<EmpiresQaScenarioName>('pending-take')
const qaSeed = ref('empires-browser-qa')
const qaScenarios = ref<Record<EmpiresQaScenarioName, EmpiresQaScenarioFixture> | null>(null)
const qaAutoplaySummary = ref('')
let unsubscribe: (() => void) | null = null
let toastTimer: number | null = null

const workingConfig = computed(() => editorOpen.value && editorConfig.value
  ? editorConfig.value
  : config.value)

const activeTdPlan = computed(() => state.value?.minigame?.plan ?? null)

const phaseCopy = computed(() => ({
  cards: ['Карточная партия', 'Переиграйте Бога Азарта в подкидного дурака.'],
  divineGift: ['Божественный дар', 'Выберите одно из трёх последствий вашей игры.'],
  empire: ['Два месяца власти', 'Распорядитесь временем, людьми и наследием империи.'],
  event: ['Имперское событие', 'Ваше решение изменит следующий кон.'],
  minigame: activeTdPlan.value?.mode === 'assault'
    ? ['Наступление', `Прорвитесь к цели «${activeTdPlan.value.objective.name}».`]
    : ['Оборона империи', `Защитите цель «${activeTdPlan.value?.objective.name ?? 'крепость'}».`],
  victory: ['Империя спасена', state.value?.outcomeReason || 'Эпоха выдержала последнюю ставку.'],
  defeat: ['Конец империи', state.value?.outcomeReason || 'Последняя ставка оказалась роковой.'],
}[state.value?.phase ?? 'cards']))

const phaseSteps = [
  { id: 'cards', label: 'Карты', icon: Swords },
  { id: 'divineGift', label: 'Дар', icon: Gift },
  { id: 'empire', label: 'Империя', icon: Crown },
  { id: 'event', label: 'Событие', icon: ScrollText },
  { id: 'minigame', label: 'Бой', icon: Shield },
]

const resourceRows = computed(() => {
  if (!state.value || !workingConfig.value) return []
  return workingConfig.value.empire.resources.map(resource => ({
    ...resource,
    value: state.value?.empire.resources[resource.id] ?? 0,
  }))
})

const goldResourceId = computed(() => workingConfig.value?.empire.resources.find(resource =>
  /gold|coin|money|talent|золот|монет/i.test(`${resource.id} ${resource.name}`),
)?.id ?? workingConfig.value?.empire.resources[0]?.id ?? '')

const knowledgeResourceId = computed(() => workingConfig.value?.empire.resources.find(resource =>
  /knowledge|science|research|знан|наук/i.test(`${resource.id} ${resource.name}`),
)?.id ?? '')

const foodResourceId = computed(() => workingConfig.value?.empire.foodResourceId ?? 'food')

const currentEvent = computed(() => {
  const eventId = state.value?.event?.eventId
  return workingConfig.value?.empire.events.find(event => event.id === eventId) ?? null
})

const giftChoices = computed(() => {
  if (!state.value || !workingConfig.value) return []
  const ids = new Set(state.value.giftChoiceIds)
  return workingConfig.value.gifts.definitions.filter(gift => ids.has(gift.id))
})

const giftDraftChoices = computed(() => giftChoices.value.map(gift => ({
  id: gift.id,
  name: gift.name,
  description: gift.description,
  kind: gift.kind,
  rarity: gift.rarity || gift.kind,
  weight: Math.max(0, gift.baseWeight + gift.performanceWeight * (state.value?.performanceScore ?? 0)),
  imageUrl: gift.image,
  effects: gift.effects.map(effectText),
  disabled: Boolean(gift.deferredReason),
  disabledReason: gift.deferredReason,
})))

const pendingResolution = computed<EmpiresPendingGiftResolution | null>(() =>
  state.value?.pendingResolution ?? null)

const pendingGift = computed(() => workingConfig.value?.gifts.definitions
  .find(gift => gift.id === pendingResolution.value?.giftId) ?? null)

function targetPreview(
  pending: EmpiresPendingGiftResolution,
  city: EmpiresCityState,
): string[] {
  if (pending.kind === 'cityResources') {
    return (pendingGift.value?.effects ?? []).flatMap((effect) => {
      if (effect.kind !== 'resource') return []
      const resource = workingConfig.value?.empire.resources.find(item => item.id === effect.resourceId)
      const before = city.resources[effect.resourceId] ?? 0
      return [`${resource?.name ?? effect.resourceId}: ${formatNumber(before)} → ${formatNumber(before + effect.amount)}`]
    })
  }

  const target = Object.entries(city.buildingLevels)
    .filter(([buildingId, level]) => {
      const building = workingConfig.value?.empire.buildings.find(item => item.id === buildingId)
      return level > 0 && Boolean(building && !building.deferredReason)
    })
    .sort(([leftId, leftLevel], [rightId, rightLevel]) => (
      rightLevel - leftLevel || leftId.localeCompare(rightId)
    ))[0]
  if (target) {
    const [buildingId, level] = target
    const building = workingConfig.value?.empire.buildings.find(item => item.id === buildingId)
    return [`${building?.name ?? buildingId}: ${level} → ${Math.max(0, level - pending.damageLevels)}`]
  }
  return ['В городе нет построенных зданий, которые может повредить удар.']
}

const targetResolutionOptions = computed<TargetResolutionOption[]>(() => {
  const pending = pendingResolution.value
  if (!pending || !engine.value || !workingConfig.value || !state.value) return []
  const eligible = new Set(pending.eligibleTargetIds)
  return state.value.empire.cities
    .filter(city => eligible.has(city.id))
    .map((city) => {
      const definition = workingConfig.value?.empire.cities.find(item => item.id === city.id)
      const region = workingConfig.value?.empire.map.regions.find(item => item.id === city.regionId)
      const accessible = engine.value?.isCityAccessible(city.id) ?? false
      return {
        id: city.id,
        name: definition?.name ?? city.name,
        regionName: region?.name,
        summary: pending.kind === 'cityResources'
          ? 'Все указанные ресурсы поступят только в этот городской запас.'
          : `Метеорит повредит одну самую развитую постройку на ${pending.damageLevels} ур.`,
        preview: targetPreview(pending, city),
        disabled: !accessible,
        disabledReason: accessible ? undefined : 'Город находится в уничтоженном регионе.',
      }
    })
})

const targetResolutionPrompt = computed(() => pendingResolution.value?.kind === 'meteorCity'
  ? 'Выберите город для удара. Изменение необратимо.'
  : 'Выберите единственный город, который получит ресурсы.')

const eventChoiceViews = computed(() => currentEvent.value?.choices.map(choice => {
  const missing = choice.resourceCosts?.find(cost =>
    (state.value?.empire.resources[cost.resourceId] ?? 0) < cost.amount,
  )
  return {
    id: choice.id,
    name: choice.label,
    description: choice.description ?? '',
    costs: costsText(choice.resourceCosts),
    effects: choice.effects.map(effectText),
    disabled: Boolean(currentEvent.value?.deferredReason || choice.deferredReason || missing),
    disabledReason: currentEvent.value?.deferredReason
      ?? choice.deferredReason
      ?? (missing ? `Не хватает ресурса: ${missing.resourceId}` : undefined),
  }
}) ?? [])

function showMessage(message: string) {
  lastMessage.value = message
  if (toastTimer !== null) window.clearTimeout(toastTimer)
  toastTimer = window.setTimeout(() => {
    lastMessage.value = ''
    toastTimer = null
  }, 6500)
}

function formatNumber(value: number) {
  return new Intl.NumberFormat('ru-RU', {
    notation: Math.abs(value) >= 10_000 ? 'compact' : 'standard',
    maximumFractionDigits: 1,
  }).format(value)
}

function rankLabel(rank: string) {
  return ({ jack: 'J', queen: 'Q', king: 'K', ace: 'A', joker: '★' } as Record<string, string>)[rank] ?? rank
}

function effectText(effect: EmpiresEffect) {
  if (effect.kind === 'resource') {
    const name = workingConfig.value?.empire.resources.find(item => item.id === effect.resourceId)?.name ?? effect.resourceId
    return `${effect.amount >= 0 ? '+' : ''}${formatNumber(effect.amount)} ${name}`
  }
  if (effect.kind === 'resourceMultiplier') {
    const name = workingConfig.value?.empire.resources.find(item => item.id === effect.resourceId)?.name ?? effect.resourceId
    return `${name}: ×${effect.multiplier}`
  }
  if (effect.kind === 'time') return `${effect.days >= 0 ? '+' : ''}${effect.days} дней`
  if (effect.kind === 'foodProduction') return `${effect.amount >= 0 ? '+' : ''}${formatNumber(effect.amount)} еды`
  if (effect.kind === 'population') return `${effect.amount >= 0 ? '+' : ''}${formatNumber(effect.amount)} населения`
  return `${effect.flagId}: ${effect.amount >= 0 ? '+' : ''}${effect.amount}`
}

function costsText(costs: Array<{ resourceId: string, amount: number }> | undefined) {
  if (!costs?.length) return ['Без ресурсной платы']
  return costs.map(cost => {
    const name = workingConfig.value?.empire.resources.find(resource => resource.id === cost.resourceId)?.name ?? cost.resourceId
    return `${formatNumber(cost.amount)} ${name}`
  })
}

function actionReasonText(reason: string | null | undefined) {
  if (!reason) return undefined
  const exact: Record<string, string> = {
    'Research is only available in the empire phase.': 'Исследования доступны только во время управления империей.',
    'That research is already complete.': 'Эта разработка уже изучена.',
    'This + steel stage is not unlocked yet.': 'Эта стадия «+» ещё не открыта предыдущей стадией «−».',
    'A military elite is required for this research.': 'Для этой разработки нужна военная элита.',
    'That research group was already used this empire phase.': 'В этой исследовательской ветке уже сделан выбор в текущей фазе.',
    'Not enough days remain.': 'Не хватает дней в текущей имперской фазе.',
    'Units can only be recruited in the empire phase.': 'Найм доступен только во время управления империей.',
    'Unit count must be a positive integer.': 'Количество должно быть целым положительным числом.',
    'Recruitment is disabled for this empire phase.': 'Набор войск отключён эффектом карты на эту имперскую фазу.',
    'Unknown city or unit.': 'Неизвестный город или вид войск.',
    'That city is not accessible.': 'Город находится в уничтоженном регионе.',
    'The city has reached its equipped recruitment capacity.': 'Город исчерпал лимит снаряжённого набора.',
    'Not enough recruitable population.': 'Не хватает военного резерва.',
  }
  if (exact[reason]) return exact[reason]
  const delayed = reason.match(/^This \+ steel stage unlocks automatically at con (\d+)\.$/)
  if (delayed) return `Стадия «+» откроется бесплатно в коне ${delayed[1]}.`
  const missingPrerequisite = reason.match(/^Missing prerequisite: (.+)\.$/)
  if (missingPrerequisite) return `Нужно: ${missingPrerequisite[1]}`
  const missingEquipment = reason.match(/^Not enough equipment: (.+)\.$/)
  if (missingEquipment) {
    const equipment = workingConfig.value?.combat.equipment.find(item => item.id === missingEquipment[1])
    return `Не хватает снаряжения: ${equipment?.name ?? missingEquipment[1]}`
  }
  const missingResource = reason.match(/^Not enough (.+)\.$/)
  if (missingResource) return `Не хватает ресурса: ${missingResource[1]}`
  const deferredResearch = reason.match(/^That research is deferred: (.+)$/)
  if (deferredResearch) return deferredResearch[1]
  const deferredUnit = reason.match(/^That unit is deferred: (.+)$/)
  if (deferredUnit) return deferredUnit[1]
  return reason
}

function initializeEngine(nextConfig: EmpiresEndgameConfig, snapshot?: EmpiresCampaignState | null) {
  unsubscribe?.()
  const nextEngine = new EmpiresEndgameEngine(nextConfig, snapshot ?? undefined)
  engine.value = nextEngine
  state.value = structuredClone(nextEngine.state)
  unsubscribe = nextEngine.subscribe(nextState => {
    state.value = structuredClone(nextState)
    triggerRef(engine)
    if (!qaMode.value) saveEmpiresCampaign(nextState)
  })
  const firstRegion = nextConfig.empire.map.regions[0]
  const firstCity = nextConfig.empire.cities[0]
  if (!activeRegionId.value || !nextConfig.empire.map.regions.some(region => region.id === activeRegionId.value)) {
    activeRegionId.value = firstRegion?.id ?? ''
  }
  if (!activeCityId.value || !nextConfig.empire.cities.some(city => city.id === activeCityId.value)) {
    activeCityId.value = firstCity?.id ?? ''
  }
  if (!qaMode.value) saveEmpiresCampaign(nextEngine.state)
  void runGodTurns()
}

function isQaScenarioName(value: string | null): value is EmpiresQaScenarioName {
  return Boolean(value && (EMPIRES_QA_SCENARIO_NAMES as readonly string[]).includes(value))
}

function isEmpireTab(value: string | null): value is EmpireTab {
  return value === 'map' || value === 'city' || value === 'technology' || value === 'council'
}

function loadQaScenario(name = qaScenarioName.value) {
  if (!config.value || !qaScenarios.value) return
  qaScenarioName.value = name
  qaAutoplaySummary.value = ''
  if (name === 'empire-council-with-points') activeEmpireTab.value = 'council'
  initializeEngine(config.value, qaScenarios.value[name].snapshot)
  const url = new URL(window.location.href)
  url.searchParams.set('qa', '1')
  url.searchParams.set('scenario', name)
  url.searchParams.set('seed', qaSeed.value)
  window.history.replaceState({}, '', url)
}

function regenerateQaScenarios() {
  if (!config.value) return
  qaScenarios.value = createEmpiresQaScenarios(config.value, { seed: qaSeed.value })
  loadQaScenario()
}

function runQaAutoplay() {
  if (!config.value) return
  const report = runEmpiresQaAutoplay(config.value, { seed: qaSeed.value, maxSteps: 2_000 })
  qaAutoplaySummary.value = report.completed
    ? `OK · ${report.steps} действий · ${report.checkedPlayerTurns} проверок хода`
    : `СТОП · ${report.stall?.code ?? 'неизвестно'} · ${report.stall?.message ?? ''}`
}

const qaDigest = computed(() => engine.value ? digestEmpiresQaState(engine.value) : null)

async function boot() {
  loading.value = true
  fatalError.value = ''
  fatalSaveRecoverable.value = false
  try {
    const loadedConfig = await loadEmpiresConfig()
    config.value = loadedConfig
    editorConfig.value = cloneEmpiresConfig(loadedConfig)
    const query = new URLSearchParams(window.location.search)
    qaMode.value = query.get('qa') === '1'
    const requestedScenario = query.get('scenario')
    if (isQaScenarioName(requestedScenario)) qaScenarioName.value = requestedScenario
    const requestedTab = query.get('tab')
    if (qaMode.value && isEmpireTab(requestedTab)) activeEmpireTab.value = requestedTab
    qaSeed.value = query.get('seed') || String(loadedConfig.seed)
    if (qaMode.value) {
      qaScenarios.value = createEmpiresQaScenarios(loadedConfig, { seed: qaSeed.value })
      initializeEngine(loadedConfig, qaScenarios.value[qaScenarioName.value].snapshot)
      if (!isEmpireTab(requestedTab) && qaScenarioName.value === 'empire-council-with-points') {
        activeEmpireTab.value = 'council'
      }
    }
    else {
      const savedCampaign = loadEmpiresCampaign(loadedConfig.id)
      fatalSaveRecoverable.value = savedCampaign !== null
      initializeEngine(loadedConfig, savedCampaign)
    }
  }
  catch (error) {
    fatalError.value = error instanceof Error ? error.message : 'Игру не удалось запустить.'
  }
  finally {
    loading.value = false
  }
}

async function discardIncompatibleSave() {
  if (!window.confirm('Удалить несовместимое сохранение и начать новую кампанию?')) return
  clearEmpiresCampaign()
  await boot()
}

function action(result: EmpiresActionResult, continueGod = true) {
  showMessage(result.message)
  if (result.ok && continueGod) void runGodTurns()
}

async function runGodTurns() {
  if (!engine.value || godBusy.value) return
  godBusy.value = true
  try {
    let safety = 0
    while (engine.value.state.phase === 'cards' && safety < 80) {
      safety += 1
      const actor = engine.value.currentActor()
      const forcedEmptyHandFinish = actor === 'player'
        && engine.value.state.durak.playerHand.length === 0
        && engine.value.canEndAttack('player')
      if (actor !== 'god' && !forcedEmptyHandFinish) break
      await new Promise(resolve => window.setTimeout(resolve, actor === 'god' ? 260 : 60))
      const result = actor === 'god'
        ? engine.value.advanceGod()
        : engine.value.endAttack('player')
      if (result.message) lastMessage.value = result.message
      if (!result.ok) break
    }
    if (safety >= 80) showMessage('Бог Азарта задумался слишком надолго. Сохранение осталось доступно.')
  }
  finally {
    godBusy.value = false
  }
}

function cardView(instanceId: string) {
  if (!engine.value || !state.value) return null
  const instance = state.value.cards[instanceId]
  if (!instance) return null
  const definition = engine.value.getDefinition(instanceId)
  const face = instance.inverted ? definition.inverted : definition.normal
  return {
    id: instance.id,
    name: definition.name,
    title: face.title || definition.name,
    suit: definition.suit,
    rank: rankLabel(definition.rank),
    timeCost: definition.timeCostDays,
    value: definition.value,
    description: face.description,
    image: face.image,
    deferredReason: face.deferredReason,
    inverted: instance.inverted,
    upgrades: instance.level,
    trump: definition.suit === state.value.durak.trumpSuit,
  }
}

const playerHandViews = computed(() => state.value?.durak.playerHand
  .map(cardView)
  .filter((card): card is NonNullable<ReturnType<typeof cardView>> => Boolean(card)) ?? [])

const tableViews = computed(() => state.value?.durak.table.flatMap(pair => {
  const attack = cardView(pair.attackCardId)
  if (!attack) return []
  return [{ attack, defense: pair.defenseCardId ? cardView(pair.defenseCardId) : null }]
}) ?? [])

const trumpCardView = computed(() => {
  const cardId = state.value?.durak.deck[0]
  return cardId ? cardView(cardId) : null
})

const legalPlayerCardIds = computed(() => {
  if (!engine.value || !state.value || engine.value.currentActor() !== 'player') return []
  if (state.value.durak.stage === 'defense') {
    const attackIndex = state.value.durak.table.findIndex(pair => !pair.defenseCardId)
    return engine.value.legalDefenseCardIds('player', attackIndex < 0 ? undefined : attackIndex)
  }
  return engine.value.legalAttackCardIds('player')
})

const playerCanTake = computed(() => engine.value?.canTake('player') ?? false)

const playerCanFinish = computed(() => engine.value?.canEndAttack('player') ?? false)

function playCard(cardId: string) {
  if (!engine.value || !state.value) return
  const attackIndex = state.value.durak.stage === 'defense'
    ? state.value.durak.table.findIndex(pair => !pair.defenseCardId)
    : undefined
  action(engine.value.playCard(cardId, attackIndex === -1 ? undefined : attackIndex))
}

function chooseGift(giftId: string) {
  if (!engine.value) return
  const result = engine.value.chooseGift(giftId)
  action(result, false)
  if (result.ok && !engine.value.state.pendingResolution && engine.value.state.phase === 'empire') {
    activeEmpireTab.value = 'map'
  }
}

function resolvePendingTarget(cityId: string) {
  if (!engine.value) return
  const result = engine.value.resolvePendingTarget(cityId)
  action(result, false)
  if (!result.ok || engine.value.state.phase !== 'empire') return
  const city = workingConfig.value?.empire.cities.find(item => item.id === cityId)
  activeCityId.value = cityId
  if (city) activeRegionId.value = city.regionId
  activeEmpireTab.value = 'city'
}

function chooseEvent(choiceId: string) {
  if (!engine.value) return
  action(engine.value.chooseEvent(choiceId))
}

function finishEmpire() {
  if (engine.value) action(engine.value.finishEmpire())
}

function resolveTdBattle(result: TdBattleResult) {
  if (engine.value) action(engine.value.resolveMinigame(result))
}

function abortTdBattle(commandLog: TdCommand[], abortTick: number) {
  if (engine.value) action(engine.value.abortMinigame(commandLog, abortTick))
}

function startNewCampaign(ask = true) {
  if (!config.value) return
  const prompt = qaMode.value
    ? 'Начать новую кампанию во временном QA-стенде? Основное автосохранение не изменится.'
    : 'Начать новую кампанию? Текущее автосохранение будет заменено.'
  if (ask && !window.confirm(prompt)) return
  if (!qaMode.value) clearEmpiresCampaign()
  initializeEngine(config.value)
  showMessage(qaMode.value
    ? 'Новая ставка запущена только в QA. Основное автосохранение не изменено.'
    : 'Новая ставка сделана. Карты розданы заново.')
}

function exportSave() {
  if (!engine.value) return
  downloadEmpiresJson('empires-endgame-save.json', engine.value.snapshotEnvelope())
}

async function importSave(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file || !engine.value || !config.value) return
  try {
    const raw: unknown = JSON.parse(await file.text())
    engine.value.restore(importEmpiresCampaign(raw, config.value.id))
    state.value = structuredClone(engine.value.state)
    if (!qaMode.value) saveEmpiresCampaign(engine.value.state)
    showMessage(qaMode.value
      ? 'Кампания загружена во временный QA-стенд. Основное автосохранение не изменено.'
      : 'Кампания загружена.')
    void runGodTurns()
  }
  catch (error) {
    showMessage(error instanceof Error ? error.message : 'Сохранение не удалось загрузить.')
  }
  ;(event.target as HTMLInputElement).value = ''
}

function openEditor() {
  if (!config.value) return
  editorConfig.value = cloneEmpiresConfig(config.value)
  editorDirty.value = false
  editorOpen.value = true
}

function updateEditorConfig(next: EmpiresEndgameConfig) {
  editorConfig.value = next
  editorDirty.value = true
}

function mutateEditor(mutator: (draft: EmpiresEndgameConfig) => void) {
  if (!editorConfig.value) return
  const next = cloneEmpiresConfig(editorConfig.value)
  mutator(next)
  updateEditorConfig(next)
}

function rejectConfigReplacementDuringMinigame(): boolean {
  const reason = empiresConfigReplacementDisabledReason(state.value)
  if (!reason) return false
  showMessage(reason)
  return true
}

function saveEditor() {
  if (!editorConfig.value) return
  if (rejectConfigReplacementDuringMinigame()) return
  try {
    validateEmpiresConfig(editorConfig.value)
    if (!qaMode.value) saveEmpiresConfig(editorConfig.value)
    config.value = cloneEmpiresConfig(editorConfig.value)
    editorDirty.value = false
    if (qaMode.value) {
      qaScenarios.value = createEmpiresQaScenarios(config.value, { seed: qaSeed.value })
      loadQaScenario()
      showMessage('Правила применены только к QA-стенду. Локальный сценарий и кампания не изменены.')
    }
    else {
      initializeEngine(config.value)
      showMessage('Сценарий сохранён локально. Начата новая кампания с обновлёнными правилами.')
    }
  }
  catch (error) {
    showMessage(error instanceof Error ? error.message : 'Сценарий не удалось сохранить.')
  }
}

async function resetEditor() {
  if (rejectConfigReplacementDuringMinigame()) return
  const prompt = qaMode.value
    ? 'Вернуть встроенный сценарий во временном QA-стенде? Основные локальные данные останутся без изменений.'
    : 'Вернуть встроенный сценарий и удалить локальные правки?'
  if (!window.confirm(prompt)) return
  if (!qaMode.value) {
    clearCustomEmpiresConfig()
    clearEmpiresCampaign()
  }
  const bundled = await loadBundledEmpiresConfig()
  config.value = bundled
  editorConfig.value = cloneEmpiresConfig(bundled)
  editorDirty.value = false
  if (qaMode.value) {
    qaScenarios.value = createEmpiresQaScenarios(bundled, { seed: qaSeed.value })
    loadQaScenario()
    showMessage('Встроенный сценарий восстановлен только в QA. Основные локальные данные не изменены.')
  }
  else {
    initializeEngine(bundled)
    showMessage('Встроенный сценарий восстановлен.')
  }
}

function exportConfig() {
  if (editorConfig.value) downloadEmpiresJson('empires-endgame-config.json', editorConfig.value)
}

function regionBounds(regionId: string, editing = false) {
  const map = (editing ? editorConfig.value : workingConfig.value)?.empire.map
  const region = map?.regions.find(item => item.id === regionId)
  const points = region?.polygon.length ? region.polygon : [
    { x: 0, y: 0 },
    { x: map?.width ?? 100, y: map?.height ?? 100 },
  ]
  const minX = Math.min(...points.map(point => point.x))
  const maxX = Math.max(...points.map(point => point.x))
  const minY = Math.min(...points.map(point => point.y))
  const maxY = Math.max(...points.map(point => point.y))
  const rawWidth = Math.max(1, maxX - minX)
  const rawHeight = Math.max(1, maxY - minY)
  return {
    minX: minX - rawWidth * 0.06,
    minY: minY - rawHeight * 0.06,
    width: rawWidth * 1.12,
    height: rawHeight * 1.12,
  }
}

function mapPointToPercent(point: EmpiresPoint, axis: 'x' | 'y', regionId: string) {
  const bounds = regionBounds(regionId)
  const minimum = axis === 'x' ? bounds.minX : bounds.minY
  const span = axis === 'x' ? bounds.width : bounds.height
  return Math.max(0, Math.min(100, (point[axis] - minimum) / span * 100))
}

function percentToMapPoint(x: number, y: number, regionId: string): EmpiresPoint {
  const bounds = regionBounds(regionId, true)
  return {
    x: bounds.minX + x / 100 * bounds.width,
    y: bounds.minY + y / 100 * bounds.height,
  }
}

function objectVisualKind(object: EmpiresMapObjectDefinition) {
  const custom = typeof object.properties?.visualKind === 'string' ? object.properties.visualKind : ''
  if (['city', 'capital', 'fortress', 'mountain', 'river', 'forest', 'landmark'].includes(custom)) return custom
  if (['city', 'fortress', 'mountain', 'river', 'landmark'].includes(object.kind)) return object.kind
  return object.kind === 'resource' ? 'landmark' : 'forest'
}

const mapRegionViews = computed(() => {
  const current = workingConfig.value
  if (!current) return []
  const cityObjectIds = new Set(current.empire.map.objects.flatMap(object => {
    const cityId = typeof object.properties?.cityId === 'string' ? object.properties.cityId : null
    return cityId ? [cityId] : []
  }))
  return current.empire.map.regions.map(region => {
    const regionAccessible = editorOpen.value || (engine.value?.isRegionAccessible(region.id) ?? true)
    const objects = current.empire.map.objects.filter(object => object.regionId === region.id).map((object) => {
      const cityId = typeof object.properties?.cityId === 'string' ? object.properties.cityId : undefined
      const cityAccessible = !cityId || editorOpen.value || (engine.value?.isCityAccessible(cityId) ?? true)
      return {
        id: object.id,
        kind: objectVisualKind(object),
        label: object.name,
        x: mapPointToPercent(object.position, 'x', region.id),
        y: mapPointToPercent(object.position, 'y', region.id),
        cityId,
        image: object.image,
        size: object.size ? {
          x: object.size.x / regionBounds(region.id).width * 100,
          y: object.size.y / regionBounds(region.id).height * 100,
        } : undefined,
        rotation: object.rotation,
        accessible: cityAccessible,
        disabledReason: cityAccessible ? undefined : 'Город недоступен: регион уничтожен.',
      }
    })
    current.empire.cities.filter(city => city.regionId === region.id && !cityObjectIds.has(city.id)).forEach(city => {
      const cityAccessible = editorOpen.value || (engine.value?.isCityAccessible(city.id) ?? true)
      objects.push({
        id: `city:${city.id}`,
        kind: city.id.toLowerCase().includes('capital') ? 'capital' : 'city',
        label: city.name,
        x: mapPointToPercent(city.position, 'x', region.id),
        y: mapPointToPercent(city.position, 'y', region.id),
        cityId: city.id,
        image: undefined,
        size: undefined,
        rotation: undefined,
        accessible: cityAccessible,
        disabledReason: cityAccessible ? undefined : 'Город недоступен: регион уничтожен.',
      })
    })
    return {
      id: region.id,
      name: region.name,
      shortName: region.name.split(' ')[0],
      biome: region.biome,
      accent: ({ ice: '#b7d5df', forest: '#8ba36d', desert: '#d6a45d', swamp: '#739488', central: '#cfb46d' } as Record<string, string>)[region.biome] ?? '#cfb46d',
      description: regionAccessible
        ? `${region.subregionIds.length} земель · ${region.cityIds.length} городов`
        : 'Регион уничтожен. Города, здания и ресурсы больше недоступны.',
      accessible: regionAccessible,
      disabledReason: regionAccessible
        ? undefined
        : 'Эта земля была принесена в жертву и навсегда потеряна для империи.',
      objects,
    }
  })
})

const mapSubregionViews = computed(() => {
  const current = workingConfig.value
  if (!current) return []
  return current.empire.map.subregions.map(subregion => ({
    id: subregion.id,
    regionId: subregion.regionId,
    name: subregion.name,
    biome: subregion.biome,
    polygon: subregion.polygon.map(point => ({
      x: mapPointToPercent(point, 'x', subregion.regionId),
      y: mapPointToPercent(point, 'y', subregion.regionId),
    })),
  }))
})

function moveMapObject(regionId: string, objectId: string, x: number, y: number) {
  mutateEditor(draft => {
    const point = percentToMapPoint(x, y, regionId)
    if (objectId.startsWith('city:')) {
      const city = draft.empire.cities.find(item => item.id === objectId.slice(5))
      if (city) city.position = point
      return
    }
    const object = draft.empire.map.objects.find(item => item.id === objectId && item.regionId === regionId)
    if (object) object.position = point
  })
}

function addMapObject(regionId: string, kind: string, x: number, y: number) {
  mutateEditor(draft => {
    const normalizedKind = ['city', 'fortress', 'mountain', 'river', 'landmark'].includes(kind)
      ? kind as EmpiresMapObjectDefinition['kind']
      : 'custom'
    const id = `custom-${kind}-${Date.now().toString(36)}`
    draft.empire.map.objects.push({
      id,
      name: `Новый объект: ${kind}`,
      kind: normalizedKind,
      regionId,
      position: percentToMapPoint(x, y, regionId),
      size: normalizedKind === 'river' ? { x: regionBounds(regionId, true).width * 0.28, y: 30 } : undefined,
      rotation: normalizedKind === 'river' ? -8 : 0,
      draggable: true,
      properties: { visualKind: kind },
    })
  })
}

function updateMapObject(
  regionId: string,
  objectId: string,
  patch: { label?: string, image?: string, size?: EmpiresPoint, rotation?: number },
) {
  mutateEditor(draft => {
    const object = draft.empire.map.objects.find(item => item.id === objectId && item.regionId === regionId)
    if (!object) return
    if (patch.label !== undefined) object.name = patch.label
    if ('image' in patch) object.image = patch.image
    if (patch.size) {
      const bounds = regionBounds(regionId, true)
      object.size = { x: patch.size.x / 100 * bounds.width, y: patch.size.y / 100 * bounds.height }
    }
    if (patch.rotation !== undefined) object.rotation = patch.rotation
  })
}

function removeMapObject(regionId: string, objectId: string) {
  mutateEditor(draft => {
    const index = draft.empire.map.objects.findIndex(item => item.id === objectId && item.regionId === regionId)
    if (index >= 0) draft.empire.map.objects.splice(index, 1)
  })
}

function addSubregion(regionId: string) {
  mutateEditor(draft => {
    const region = draft.empire.map.regions.find(item => item.id === regionId)
    if (!region) return
    const bounds = regionBounds(regionId, true)
    const id = `subregion-${regionId}-${Date.now().toString(36)}`
    const point = (x: number, y: number) => ({ x: bounds.minX + bounds.width * x, y: bounds.minY + bounds.height * y })
    draft.empire.map.subregions.push({
      id,
      regionId,
      name: 'Новая земля',
      biome: region.biome,
      polygon: [point(.28, .28), point(.58, .28), point(.58, .58), point(.28, .58)],
    })
    region.subregionIds.push(id)
  })
}

function updateSubregion(regionId: string, subregionId: string, patch: { name?: string, biome?: string }) {
  mutateEditor(draft => {
    const subregion = draft.empire.map.subregions.find(item => item.id === subregionId && item.regionId === regionId)
    if (!subregion) return
    if (patch.name !== undefined) subregion.name = patch.name
    if (patch.biome !== undefined) subregion.biome = patch.biome
  })
}

function removeSubregion(regionId: string, subregionId: string) {
  mutateEditor(draft => {
    draft.empire.map.subregions = draft.empire.map.subregions.filter(item => item.id !== subregionId)
    const region = draft.empire.map.regions.find(item => item.id === regionId)
    if (region) region.subregionIds = region.subregionIds.filter(id => id !== subregionId)
    for (const object of draft.empire.map.objects) {
      if (object.subregionId === subregionId) object.subregionId = undefined
    }
  })
}

function openCity(cityId: string) {
  if (!editorOpen.value && engine.value && !engine.value.isCityAccessible(cityId)) {
    showMessage('Этот город недоступен: его регион уничтожен.')
    return
  }
  activeCityId.value = cityId
  const city = workingConfig.value?.empire.cities.find(item => item.id === cityId)
  if (city) activeRegionId.value = city.regionId
  activeEmpireTab.value = 'city'
  void nextTick()
}

function cityProduction(cityId: string) {
  try {
    return engine.value?.cityProduction(cityId) ?? {}
  }
  catch {
    return {}
  }
}

type CityViewSlot = 'farm' | 'lumber' | 'mine' | 'forge' | 'barracks' | 'unique' | 'municipal'

function cityViewSlot(slot: EmpiresBuildingDefinition['slot']): CityViewSlot {
  return slot === 'smithy' ? 'forge' : slot
}

function dependencyLabel(dependency: EmpiresDependency) {
  if (dependency.kind === 'technology') {
    return workingConfig.value?.empire.technologies.find(item => item.id === dependency.technologyId)?.name
      ?? dependency.technologyId
  }
  if (dependency.kind === 'flag') return `${dependency.flagId} ≥ ${dependency.minimum}`
  const building = workingConfig.value?.empire.buildings.find(item => item.id === dependency.buildingId)
  return `${building?.name ?? dependency.buildingId} · ур. ${dependency.level}`
}

function firstMissingDependency(
  dependencies: readonly EmpiresDependency[],
  cityId?: string,
  operationalSameCityBuildings = false,
) {
  if (!state.value) return null
  const city = state.value.empire.cities.find(item => item.id === cityId)
  for (const dependency of dependencies) {
    if (dependency.kind === 'technology') {
      const technology = workingConfig.value?.empire.technologies.find(
        item => item.id === dependency.technologyId,
      )
      if (
        !technology
        || technology.deferredReason
        || !state.value.empire.researchedTechnologyIds.includes(dependency.technologyId)
      ) return dependencyLabel(dependency)
      continue
    }
    if (dependency.kind === 'flag') {
      if ((state.value.empire.flags[dependency.flagId] ?? 0) < dependency.minimum) return dependencyLabel(dependency)
      continue
    }
    const candidateCities = (dependency.scope !== 'anyCity' && city ? [city] : state.value.empire.cities)
      .filter(candidate => engine.value?.isCityAccessible(candidate.id) ?? true)
    const useOperational = operationalSameCityBuildings && dependency.scope !== 'anyCity' && Boolean(city)
    const available = candidateCities.some((candidate) => {
      const building = workingConfig.value?.empire.buildings.find(
        item => item.id === dependency.buildingId,
      )
      if (!building || building.deferredReason) return false
      if (candidate.buildingInteractionLocks[dependency.buildingId] === state.value?.con) return false
      const levels = useOperational ? candidate.operationalBuildingLevels : candidate.buildingLevels
      const rawLevel = levels[dependency.buildingId] ?? 0
      if (rawLevel <= 0) return false
      const purchasedLevel = candidate.buildingLevels[dependency.buildingId] ?? 0
      const effectivePurchased = engine.value?.effectiveBuildingLevel(candidate.id, dependency.buildingId)
        ?? purchasedLevel
      const levelBonus = Math.max(0, effectivePurchased - purchasedLevel)
      return rawLevel + levelBonus >= dependency.level
    })
    if (!available) return dependencyLabel(dependency)
  }
  return null
}

function isStableBuilding(building: EmpiresBuildingDefinition) {
  const identity = `${building.id} ${building.name}`.toLocaleLowerCase('ru-RU')
  return identity.includes('stable') || identity.includes('конюш')
}

function constructionDependencies(
  building: EmpiresBuildingDefinition,
  level: EmpiresBuildingLevelDefinition,
) {
  if (!isStableBuilding(building) || (state.value?.empire.flags.stableWithoutLivestock ?? 0) <= 0) {
    return level.dependencies
  }
  return level.dependencies.filter(
    dependency => dependency.kind !== 'flag' || dependency.flagId !== 'livestockAvailable',
  )
}

function constructionResourceCosts(
  building: EmpiresBuildingDefinition,
  level: EmpiresBuildingLevelDefinition,
) {
  return level.resourceCosts.filter((cost) => {
    if (building.slot === 'smithy'
      && cost.resourceId === 'iron'
      && (state.value?.empire.flags.smithyWithoutIron ?? 0) > 0) return false
    if (isStableBuilding(building)
      && cost.resourceId === 'horses'
      && (state.value?.empire.flags.stableWithoutLivestock ?? 0) > 0) return false
    return true
  })
}

function combinedResourceAmount(city: EmpiresCityState, resourceId: string) {
  return engine.value?.cityAvailableResource(city.id, resourceId)
    ?? (city.resources[resourceId] ?? 0) + (state.value?.empire.resources[resourceId] ?? 0)
}

function constructionBlockedReason(
  city: EmpiresCityState,
  building: EmpiresBuildingDefinition,
  level: EmpiresBuildingLevelDefinition,
) {
  if (!state.value || !workingConfig.value) return 'Состояние империи недоступно'
  if (building.deferredReason) return building.deferredReason
  if (!(engine.value?.isCityAccessible(city.id) ?? true)) return 'Город находится в уничтоженном регионе'
  if (building.allowedCityIds && !building.allowedCityIds.includes(city.id)) {
    return 'Постройка недоступна в этом городе'
  }
  if (city.buildingInteractionLocks[building.id] === state.value.con) {
    return 'Постройка заблокирована до следующего кона'
  }
  const missingDependency = firstMissingDependency(constructionDependencies(building, level), city.id)
  if (missingDependency) return `Нужно: ${missingDependency}`
  if (state.value.empire.daysRemaining < level.timeCostDays) return `Нужно ${level.timeCostDays} дней`
  const missingResource = constructionResourceCosts(building, level).find(cost =>
    combinedResourceAmount(city, cost.resourceId) < cost.amount,
  )
  if (missingResource) {
    const name = workingConfig.value.empire.resources.find(item => item.id === missingResource.resourceId)?.name
      ?? missingResource.resourceId
    return `Не хватает ресурса: ${name}`
  }
  for (const lock of level.facilityLocks) {
    if (city.lockedFacilities[lock]) return `${lock === 'mine' ? 'Шахта' : 'Лесопилка'} уже занята`
    const providerId = workingConfig.value.empire.lockProviderBuildingIds[lock]
    if ((city.operationalBuildingLevels[providerId] ?? 0) < 1) {
      return `Нет работающей ${lock === 'mine' ? 'шахты' : 'лесопилки'}`
    }
  }
  return null
}

function recruitmentMaximum(cityId: string, unitId: string) {
  const currentEngine = engine.value
  if (!currentEngine) return 0
  let available = 0
  let blocked = 99
  while (available < blocked) {
    const candidate = Math.ceil((available + blocked) / 2)
    if (currentEngine.recruitmentQuote(cityId, unitId, candidate).blockedReason) {
      blocked = candidate - 1
    } else {
      available = candidate
    }
  }
  return available
}

const cityViews = computed(() => {
  if (!state.value || !workingConfig.value) return []
  return state.value.empire.cities.map(city => {
    const currentConfig = workingConfig.value
    const definition = currentConfig.empire.cities.find(item => item.id === city.id)
    const region = currentConfig.empire.map.regions.find(item => item.id === city.regionId)
    const production = cityProduction(city.id)
    const slotAssignments = Object.fromEntries((definition?.slots ?? []).map(slot => [
      slot.id,
      editorOpen.value ? slot.buildingId : city.buildingSlotAssignments[slot.id] ?? slot.buildingId,
    ])) as Record<string, string | undefined>
    const assignedBuildingIds = new Set(Object.values(slotAssignments).filter((id): id is string => Boolean(id)))

    const facilityView = (building: EmpiresBuildingDefinition, slotId: string) => {
      const baseLevel = city.buildingLevels[building.id] ?? 0
      const baseMaxLevel = Math.max(0, ...building.levels.map(item => item.level))
      const level = editorOpen.value
        ? baseLevel
        : engine.value?.effectiveBuildingLevel(city.id, building.id) ?? baseLevel
      const maxLevel = editorOpen.value
        ? baseMaxLevel
        : engine.value?.effectiveBuildingMaxLevel(building.id) ?? baseMaxLevel
      const levelBonus = Math.max(0, level - baseLevel)
      const currentLevel = editorOpen.value
        ? building.levels.find(item => item.level === baseLevel)
        : engine.value?.projectedBuildingLevel(building.id, level)
          ?? building.levels.find(item => item.level === baseLevel)
      const operationalLevel = editorOpen.value
        ? city.operationalBuildingLevels[building.id] ?? baseLevel
        : engine.value?.effectiveOperationalBuildingLevel(city.id, building.id) ?? 0
      const productiveLevel = editorOpen.value
        ? building.levels.find(item => item.level === operationalLevel)
        : engine.value?.projectedBuildingLevel(building.id, operationalLevel)
      const nextLevel = building.levels.find(item => item.level === baseLevel + 1)
      const blockedReason = nextLevel ? constructionBlockedReason(city, building, nextLevel) : null
      const busyLock = building.slot === 'mine' || building.slot === 'lumber'
        ? city.lockedFacilities[building.slot]
        : undefined
      const interactionLocked = city.buildingInteractionLocks[building.id] === state.value?.con
      const boosted = engine.value?.hasProductionBoost(city.id, building.id) ?? false
      const boostMultiplier = boosted ? (state.value?.empire.flags.productionBoostPercent ?? 200) / 100 : 1
      const output = productiveLevel?.production?.map((item) => {
        const outputName = currentConfig.empire.resources.find(resource => resource.id === item.resourceId)?.name
          ?? item.resourceId
        const amount = item.amount * boostMultiplier
        return `${amount >= 0 ? '+' : ''}${formatNumber(amount)} ${outputName}`
      }).join(' · ')
      return {
        id: building.id,
        slotId,
        slot: cityViewSlot(building.slot),
        name: currentLevel?.name || building.name,
        description: currentLevel?.description ?? (level === 0 ? 'Здание ещё не возведено.' : undefined),
        imageUrl: currentLevel?.image ?? building.image,
        level,
        maxLevel,
        baseLevel,
        baseMaxLevel,
        workforce: productiveLevel?.workerDemand ?? 0,
        requiredWorkforce: currentLevel?.workerDemand ?? 0,
        output,
        busy: Boolean(busyLock),
        locked: Boolean(blockedReason || interactionLocked),
        boostEligible: ['farm', 'lumber', 'mine'].includes(building.slot) && operationalLevel >= 1,
        boosted,
        boostPercent: state.value?.empire.flags.productionBoostPercent ?? 200,
        deferredReason: building.deferredReason,
        deferredSubfeatures: building.deferredSubfeatures,
        stateMessage: interactionLocked
          ? 'Постройка повреждена и недоступна до следующего кона'
          : busyLock
          ? `Занято проектом ${busyLock}`
          : operationalLevel < level
          ? `Рабочих хватает только на ${operationalLevel} из ${level} эффективных уровней`
          : blockedReason ?? (city.lastStarvationLoss > 0
          ? `После голода потеряно ${formatNumber(city.lastStarvationLoss)}`
          : undefined),
        prerequisites: nextLevel?.dependencies.map(dependencyLabel),
        improvements: building.levels.filter(item => item.level > baseLevel).slice(0, 3).map((item) => {
          const targetEffectiveLevel = editorOpen.value ? item.level : item.level + levelBonus
          const projectedLevel = editorOpen.value
            ? item
            : engine.value?.projectedBuildingLevel(building.id, targetEffectiveLevel) ?? item
          const improvementName = projectedLevel.name || `${building.name} · уровень ${targetEffectiveLevel}`
          return {
            id: `${building.id}:${item.level}`,
            name: !editorOpen.value && levelBonus > 0
              ? `${improvementName} · эфф. ур. ${targetEffectiveLevel}`
              : improvementName,
            description: projectedLevel.description,
            level: targetEffectiveLevel,
            goldCost: item.resourceCosts.find(cost => cost.resourceId === goldResourceId.value)?.amount,
            timeCost: item.timeCostDays,
            workforce: projectedLevel.workerDemand,
            completed: false,
            busy: item.level === baseLevel + 1 && Boolean(busyLock),
            locked: interactionLocked
              || item.level > baseLevel + 1
              || (item.level === baseLevel + 1 && Boolean(blockedReason)),
            prerequisites: item.dependencies.map(dependencyLabel),
            imageUrl: projectedLevel.image ?? building.image,
            deferredReason: building.deferredReason,
          }
        }),
      }
    }

    const visibleSlots = (definition?.slots ?? []).filter(slot => slot.kind !== 'municipal')
    const buildings = visibleSlots.flatMap((slot) => {
      const buildingId = slotAssignments[slot.id]
      const building = currentConfig.empire.buildings.find(item => item.id === buildingId)
      return building && building.slot !== 'municipal' ? [facilityView(building, slot.id)] : []
    })
    const municipalitySlot = definition?.slots.find(slot => slot.kind === 'municipal')
    const municipalityBuildingId = municipalitySlot ? slotAssignments[municipalitySlot.id] : undefined
    const municipalityDefinition = currentConfig.empire.buildings.find(item =>
      item.id === municipalityBuildingId && item.slot === 'municipal',
    )
    const municipality = municipalityDefinition && municipalitySlot
      ? facilityView(municipalityDefinition, municipalitySlot.id)
      : undefined

    const placementOptions = (definition?.slots ?? []).flatMap((slot) => {
      if (slotAssignments[slot.id]) return []
      return currentConfig.empire.buildings.filter(building =>
        building.slot === slot.kind
        && !assignedBuildingIds.has(building.id)
        && (city.buildingLevels[building.id] ?? 0) === 0,
      ).flatMap((building) => {
        const firstLevel = building.levels.find(level => level.level === 1)
        if (!firstLevel) return []
        const disabledReason = constructionBlockedReason(city, building, firstLevel)
        return [{
          id: building.id,
          name: building.name,
          description: firstLevel.description,
          imageUrl: firstLevel.image ?? building.image,
          slot: cityViewSlot(building.slot),
          slotId: slot.kind === 'municipal' ? 'municipal' : slot.id,
          disabled: Boolean(disabledReason),
          disabledReason: disabledReason ?? undefined,
          deferredReason: building.deferredReason,
        }]
      })
    })

    const recruitableUnits = (currentConfig.empire.units ?? []).map((unit) => {
      const maxQuantity = recruitmentMaximum(city.id, unit.id)
      const desiredQuantity = Math.max(1, Math.min(
        maxQuantity || 1,
        recruitQuantities.value[`${city.id}:${unit.id}`] ?? 1,
      ))
      const quote = engine.value?.recruitmentQuote(city.id, unit.id, desiredQuantity)
      const disabledReason = actionReasonText(quote?.blockedReason)
      return {
        id: unit.id,
        name: unit.name,
        description: unit.description,
        imageUrl: unit.image,
        count: engine.value?.cityRecruitedUnitCount(city.id, unit.id) ?? 0,
        foodUpkeep: unit.foodUpkeep,
        populationCost: unit.populationCost,
        timeCost: quote && !quote.blockedReason ? quote.timeCostDays : unit.timeCostDays,
        loadoutId: quote?.loadoutId || undefined,
        resourceCosts: quote?.blockedReason ? [] : costsText(quote?.resourceCosts ?? unit.resourceCosts),
        equipmentCosts: (quote?.blockedReason ? [] : quote?.equipmentCosts ?? unit.equipmentCosts ?? []).map(cost => {
          const equipment = currentConfig.combat.equipment.find(item => item.id === cost.equipmentId)
          return `${formatNumber(cost.amount)} ${equipment?.name ?? cost.equipmentId}`
        }),
        maxQuantity,
        quantity: desiredQuantity,
        disabled: Boolean(disabledReason),
        disabledReason: disabledReason ?? undefined,
        deferredReason: unit.deferredReason,
      }
    })

    return {
      id: city.id,
      name: city.name,
      regionName: region?.name,
      epithet: definition ? `${definition.slots.length} городских участков` : undefined,
      accessible: editorOpen.value || (engine.value?.isCityAccessible(city.id) ?? true),
      disabledReason: editorOpen.value || (engine.value?.isCityAccessible(city.id) ?? true)
        ? undefined
        : 'Регион уничтожен: управление, строительство, производство и городской запас заблокированы.',
      resourceStockpiles: currentConfig.empire.resources.map(resource => ({
        id: resource.id,
        name: resource.name,
        value: city.resources[resource.id] ?? 0,
        deferredReason: resource.deferredReason,
      })),
      population: city.population,
      militaryPopulation: city.militaryPopulation,
      foodProduced: production[foodResourceId.value] ?? city.lastProduction[foodResourceId.value] ?? 0,
      foodConsumed: engine.value?.cityFoodConsumption(city.id) ?? city.population,
      armyFoodConsumed: engine.value?.cityArmyFoodUpkeep(city.id) ?? 0,
      loyalty: state.value?.empire.flags[`loyalty:${city.id}`] ?? 0,
      armyMorale: {
        value: state.value.army.morale,
        minimum: Math.max(
          currentConfig.td.morale?.minimum ?? 0,
          state.value.empire.flags.minimumCombatSpirit ?? 0,
        ),
        maximum: state.value.army.maxMorale,
      },
      equipmentStock: Object.entries(state.value.army.equipmentStock)
        .filter(([, value]) => value > 0)
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([equipmentId, value]) => ({
          id: equipmentId,
          name: currentConfig.combat.equipment.find(item => item.id === equipmentId)?.name ?? equipmentId,
          value,
        })),
      armyCohorts: city.recruitedUnitCohorts
        .filter(cohort => cohort.count > 0)
        .sort((left, right) => left.id.localeCompare(right.id))
        .map(cohort => ({
          id: cohort.id,
          unitName: currentConfig.empire.units?.find(unit => unit.id === cohort.unitId)?.name ?? cohort.unitId,
          count: cohort.count,
          loadoutId: cohort.loadoutId,
          weaponName: currentConfig.combat.equipment.find(item => item.id === cohort.weaponEquipmentId)?.name
            ?? cohort.weaponEquipmentId,
          defenseName: cohort.defenseEquipmentId
            ? currentConfig.combat.equipment.find(item => item.id === cohort.defenseEquipmentId)?.name
              ?? cohort.defenseEquipmentId
            : undefined,
        })),
      municipality,
      slots: visibleSlots.map(slot => ({ id: slot.id, kind: cityViewSlot(slot.kind) as Exclude<CityViewSlot, 'municipal'> })),
      placementOptions,
      recruitableUnits,
      buildings,
    }
  })
})

function upgradeBuilding(cityId: string, buildingId: string) {
  selectedBuildingId.value = buildingId
  if (engine.value) action(engine.value.upgradeBuilding(cityId, buildingId))
}

function placeBuilding(cityId: string, slotIdOrKind: string, buildingId: string) {
  if (!engine.value || !workingConfig.value) return
  const city = workingConfig.value.empire.cities.find(item => item.id === cityId)
  const slotId = city?.slots.find(slot => slot.id === slotIdOrKind || slot.kind === slotIdOrKind)?.id
    ?? slotIdOrKind
  action(engine.value.placeBuilding(cityId, slotId, buildingId))
  selectedBuildingId.value = buildingId
}

function recruitUnits(cityId: string, unitId: string, count: number) {
  if (engine.value) action(engine.value.recruitUnits(cityId, unitId, count))
}

function setRecruitQuantity(cityId: string, unitId: string, count: number) {
  recruitQuantities.value = {
    ...recruitQuantities.value,
    [`${cityId}:${unitId}`]: Math.max(1, Math.floor(count)),
  }
}

function toggleProductionBoost(cityId: string, buildingId: string, enabled: boolean) {
  if (!engine.value) return
  action(enabled
    ? engine.value.assignProductionBoost(cityId, buildingId)
    : engine.value.clearProductionBoost(cityId, buildingId))
}

const technologyNodes = computed(() => {
  if (!state.value || !workingConfig.value) return []
  const technologies = workingConfig.value.empire.technologies
  const authoredXs = technologies.flatMap(technology => technology.position ? [technology.position.x] : [])
  const fallbackStartX = authoredXs.length > 0 ? Math.max(...authoredXs) + 250 : 100
  const missingTiers = Array.from(new Set(technologies.flatMap((technology, index) => (
    technology.position ? [] : [Math.max(1, technology.tier ?? Math.floor(index / 8) + 1)]
  )))).sort((left, right) => left - right)
  const fallbackColumnByTier = new globalThis.Map(missingTiers.map((tier, index) => [tier, index]))
  const fallbackRowsByColumn = new Map<number, number>()
  return technologies.map((technology, index) => {
    const fallbackTier = Math.max(1, technology.tier ?? Math.floor(index / 8) + 1)
    const fallbackColumn = fallbackColumnByTier.get(fallbackTier) ?? 0
    const fallbackRow = fallbackRowsByColumn.get(fallbackColumn) ?? 0
    if (!technology.position) fallbackRowsByColumn.set(fallbackColumn, fallbackRow + 1)
    const quote = editorOpen.value ? null : engine.value?.researchQuote(technology.id)
    const requires = quote?.requiredTechnologyIds
      ?? technology.prerequisites.flatMap(dependency => dependency.kind === 'technology' ? [dependency.technologyId] : [])
    const exactCosts = quote?.resourceCosts ?? technology.resourceCosts
    const researched = quote?.researched
      ?? state.value.empire.researchedTechnologyIds.includes(technology.id)
    const blockedReason = actionReasonText(quote?.blockedReason)
    const available = !researched && !blockedReason
    const entryTechnology = quote?.entryFromTechnologyId
      ? technologies.find(candidate => candidate.id === quote.entryFromTechnologyId)
      : null
    return {
      id: technology.id,
      name: technology.name,
      description: technology.description ?? 'Имперская разработка.',
      branch: technology.category === 'technology' ? technology.groupId || 'science' : technology.category,
      tier: technology.tier,
      x: technology.position?.x ?? fallbackStartX + fallbackColumn * 210,
      y: technology.position?.y ?? 80 + fallbackRow * 88,
      requires,
      costKnowledge: exactCosts.find(cost => cost.resourceId === knowledgeResourceId.value)?.amount ?? 0,
      costGold: exactCosts.find(cost => cost.resourceId === goldResourceId.value)?.amount ?? 0,
      costs: costsText(exactCosts),
      timeCost: quote?.timeCostDays ?? technology.timeCostDays,
      costMultiplier: quote?.costMultiplier ?? 1,
      entryFromName: entryTechnology?.name,
      freeEligibleCon: quote?.freeEligibleCon ?? null,
      researched,
      available,
      blockedReason: blockedReason ?? undefined,
      deferredReason: technology.deferredReason,
      steelBranch: technology.steel?.branchId,
      steelGeneration: technology.steel?.generation,
      steelStage: technology.steel?.stage,
      steelElite: technology.steel?.eliteRequired,
      steelPayoff: technology.steel?.payoff,
      deferredSubfeatures: technology.deferredSubfeatures,
      darkSide: technology.tags?.includes('dark-side') ? 'Эта разработка открывает опасную ветвь.' : undefined,
      image: technology.image,
    }
  })
})

function research(technologyId: string) {
  selectedTechnologyId.value = technologyId
  if (engine.value) action(engine.value.research(technologyId))
}

function moveTechnology(technologyId: string, x: number, y: number) {
  mutateEditor(draft => {
    const technology = draft.empire.technologies.find(item => item.id === technologyId)
    if (technology) technology.position = { x, y }
  })
}

function toggleTechnologyDependency(fromId: string, toId: string) {
  mutateEditor(draft => {
    const target = draft.empire.technologies.find(item => item.id === toId)
    if (!target) return
    const index = target.prerequisites.findIndex(item => item.kind === 'technology' && item.technologyId === fromId)
    if (index >= 0) target.prerequisites.splice(index, 1)
    else target.prerequisites.push({ kind: 'technology', technologyId: fromId })
  })
}

const populationCity = computed(() => state.value?.empire.cities.find(city => city.id === populationCityId.value) ?? null)
const populationCategories = computed(() => {
  if (!populationCity.value || !workingConfig.value) return []
  return workingConfig.value.empire.populationClasses.filter(category => category.canWork).map((category, index) => ({
    id: category.id,
    name: category.name,
    amount: populationCity.value?.populationClasses[category.id] ?? 0,
    color: ['#c8ab69', '#779d89', '#8296ba', '#a86e68', '#8c7aa8', '#b58c55'][index % 6],
  }))
})
const nonWorkingPopulation = computed(() => {
  if (!populationCity.value || !workingConfig.value) return 0
  return workingConfig.value.empire.populationClasses.filter(category => !category.canWork)
    .reduce((total, category) => total + (populationCity.value?.populationClasses[category.id] ?? 0), 0)
})

function savePopulation(categories: Array<{ id: string, name: string, amount: number }>) {
  if (!editorOpen.value || !populationCity.value) return
  const cityId = populationCity.value.id
  mutateEditor(draft => {
    const city = draft.empire.cities.find(item => item.id === cityId)
    if (!city) return
    const workingDefinitionIds = new Set(draft.empire.populationClasses
      .filter(definition => definition.canWork)
      .map(definition => definition.id))
    const nonWorkingEntries = Object.entries(city.populationClasses)
      .filter(([classId]) => !workingDefinitionIds.has(classId))
    city.populationClasses = {
      ...Object.fromEntries(nonWorkingEntries),
      ...Object.fromEntries(categories.map(category => [category.id, category.amount])),
    }
    city.population = Object.values(city.populationClasses).reduce((total, amount) => total + amount, 0)
    city.militaryPopulation = Math.min(city.militaryPopulation, city.population)
    categories.forEach(category => {
      const definition = draft.empire.populationClasses.find(item => item.id === category.id)
      if (definition) definition.name = category.name
      else draft.empire.populationClasses.push({
        id: category.id,
        name: category.name,
        description: 'Пользовательская трудоспособная группа.',
        canWork: true,
        canRecruit: true,
        foodPerPerson: 1,
        workerPriority: draft.empire.populationClasses.length + 1,
      })
    })
    const retainedIds = new Set(categories.map(category => category.id))
    draft.empire.populationClasses = draft.empire.populationClasses.filter(definition => (
      !definition.canWork
      || retainedIds.has(definition.id)
      || draft.empire.cities.some(otherCity => otherCity.id !== cityId && (otherCity.populationClasses[definition.id] ?? 0) > 0)
    ))
  })
  populationCityId.value = null
}

const councilCards = computed(() => {
  if (!state.value) return []
  return state.value.durak.playerHand.map(id => ({ instance: state.value?.cards[id], view: cardView(id) }))
    .filter((item): item is { instance: EmpiresCardInstance, view: NonNullable<ReturnType<typeof cardView>> } => Boolean(item.instance && item.view))
})

function selectCouncilCard(cardId: string) {
  selectedCouncilCardId.value = cardId
  if (!workingConfig.value || !state.value) return
  if (state.value.upgradePoints < Math.min(
    workingConfig.value.upgrades.improveCost,
    workingConfig.value.upgrades.restoreCost,
  )) {
    showMessage('Карта выбрана. Очки улучшений выдаются за выполненные условия кона.')
  }
}

onMounted(() => void boot())
onUnmounted(() => {
  unsubscribe?.()
  if (toastTimer !== null) window.clearTimeout(toastTimer)
})
</script>

<template>
  <main
    class="endgame-page"
    :class="{ 'editor-active': editorOpen }"
    :data-phase="state?.phase"
    :data-revision="state?.revision"
  >
    <div class="endgame-atmosphere" aria-hidden="true"><i /><b /><span /></div>

    <section v-if="loading" class="state-screen">
      <Sparkles class="loading-sigil" :size="42" />
      <h1>Empire's Endgame</h1>
      <p>Бог Азарта тасует судьбу империи…</p>
    </section>

    <section v-else-if="fatalError" class="state-screen error-screen">
      <CircleHelp :size="42" />
      <h1>Игра не загрузилась</h1>
      <p>{{ fatalError }}</p>
      <button type="button" @click="boot"><RotateCcw :size="16" /> Попробовать снова</button>
      <button v-if="fatalSaveRecoverable" type="button" class="danger" @click="discardIncompatibleSave">
        Начать без старого сохранения
      </button>
    </section>

    <template v-else-if="state && workingConfig && engine">
      <header class="campaign-header">
        <div class="title-lockup">
          <span class="royal-seal"><Crown :size="23" /></span>
          <div><span>Одиночная кампания</span><h1>{{ workingConfig.title }}</h1></div>
        </div>

        <div class="campaign-metrics">
          <span><b>{{ state.con }}</b><small>кон</small></span>
          <span><b>{{ state.upgradePoints }}</b><small>очков</small></span>
          <span><b>{{ state.empire.daysRemaining }}</b><small>дней</small></span>
        </div>

        <div class="header-actions">
          <button type="button" title="Экспортировать сохранение" @click="exportSave"><Download :size="16" /></button>
          <button type="button" title="Импортировать сохранение" @click="saveInput?.click()"><Upload :size="16" /></button>
          <input ref="saveInput" data-testid="import-campaign" type="file" accept="application/json,.json" hidden @change="importSave" />
          <button data-testid="new-campaign" type="button" title="Новая кампания" @click="startNewCampaign()"><RotateCcw :size="16" /></button>
          <button class="builder-button" data-testid="open-constructor" type="button" @click="openEditor"><Settings2 :size="16" /><span>Конструктор</span></button>
        </div>
      </header>

      <aside v-if="qaMode && qaScenarios" class="qa-panel" data-testid="qa-panel">
        <strong>QA · детерминированный стенд</strong>
        <label>
          Сценарий
          <select v-model="qaScenarioName" data-testid="qa-scenario" @change="loadQaScenario()">
            <option v-for="name in EMPIRES_QA_SCENARIO_NAMES" :key="name" :value="name">
              {{ qaScenarios[name].title }}
            </option>
          </select>
        </label>
        <label>
          Seed
          <input v-model="qaSeed" data-testid="qa-seed" type="text" @change="regenerateQaScenarios" />
        </label>
        <button data-testid="qa-reload" type="button" @click="loadQaScenario()"><RotateCcw :size="14" /> Сбросить сценарий</button>
        <button data-testid="qa-autoplay" type="button" @click="runQaAutoplay"><Play :size="14" /> Автотест кампании</button>
        <code v-if="qaDigest" data-testid="qa-digest">{{ qaDigest.phase }} · r{{ qaDigest.revision }} · {{ qaDigest.currentActor ?? '—' }} / {{ qaDigest.stage }}</code>
        <span v-if="qaAutoplaySummary" data-testid="qa-autoplay-result">{{ qaAutoplaySummary }}</span>
      </aside>

      <section class="phase-banner">
        <div>
          <span>{{ editorOpen ? 'Режим конструктора' : `Глава ${state.con}` }}</span>
          <h2>{{ editorOpen ? 'Настройка Empire\'s Endgame' : phaseCopy[0] }}</h2>
          <p>{{ editorOpen ? 'Карта, города и деревья остаются интерактивными; сохранение применит правила к новой кампании.' : phaseCopy[1] }}</p>
        </div>
        <ol>
          <li v-for="step in phaseSteps" :key="step.id" :class="{ active: step.id === (editorOpen ? 'empire' : state.phase), passed: !editorOpen && phaseSteps.findIndex(item => item.id === state.phase) > phaseSteps.findIndex(item => item.id === step.id) }">
            <component :is="step.icon" :size="14" /><span>{{ step.label }}</span>
          </li>
        </ol>
      </section>

      <div v-if="lastMessage" class="campaign-toast" role="status"><Sparkles :size="14" />{{ lastMessage }}</div>

      <section v-if="state.phase === 'cards' && !editorOpen" class="phase-content cards-phase">
        <DurakTable
          :player-hand="playerHandViews"
          :god-hand-count="state.durak.godHand.length"
          :table="tableViews"
          :deck-count="state.durak.deck.length"
          :discard-count="state.durak.discard.length"
          :trump-suit="state.durak.trumpSuit"
          :trump-card="trumpCardView"
          :attacker="state.durak.attacker"
          :stage="state.durak.stage"
          :message="godBusy ? 'Бог Азарта выбирает карту…' : lastMessage"
          :legal-card-ids="legalPlayerCardIds"
          :can-take="playerCanTake"
          :can-finish="playerCanFinish"
          :disabled="godBusy"
          @play="playCard"
          @take="action(engine.takeCards('player'))"
          @finish="action(engine.endAttack('player'))"
        />
        <aside class="rules-note"><BookOpen :size="16" /><span><b>Подкидной дурак:</b> бейтесь мастью или козырем, подкидывайте только достоинства со стола. Добранная из обычной колоды карта усиливается; взятая у Бога приходит перевёрнутой.</span></aside>
      </section>

      <section v-else-if="state.phase === 'divineGift' && !editorOpen" class="phase-content gift-phase">
        <GiftDraft
          v-if="!pendingResolution"
          :choices="giftDraftChoices"
          title="Бог предлагает три дара"
          :description="`Результат партии: ${state.performanceScore}. Чем лучше вы сыграли, тем сильнее выбор склоняется в вашу пользу — но любой дар остаётся частью сделки.`"
          @choose="chooseGift"
        />
        <TargetResolutionDialog
          v-else
          :title="`${pendingGift?.name || 'Божественный дар'}: выберите город`"
          :description="pendingGift?.description || 'Дар требует выбрать город перед переходом к управлению империей.'"
          :prompt="targetResolutionPrompt"
          :options="targetResolutionOptions"
          @choose="resolvePendingTarget"
        />
      </section>

      <section v-else-if="state.phase === 'empire' || editorOpen" class="phase-content empire-phase">
        <div class="empire-toolbar">
          <nav aria-label="Разделы империи">
            <button data-testid="tab-map" :class="{ active: activeEmpireTab === 'map' }" type="button" @click="activeEmpireTab = 'map'"><MapIcon :size="15" /> Карта</button>
            <button data-testid="tab-city" :class="{ active: activeEmpireTab === 'city' }" type="button" @click="activeEmpireTab = 'city'"><Building2 :size="15" /> Города</button>
            <button data-testid="tab-technology" :class="{ active: activeEmpireTab === 'technology' }" type="button" @click="activeEmpireTab = 'technology'"><FlaskConical :size="15" /> Развитие</button>
            <button data-testid="tab-council" :class="{ active: activeEmpireTab === 'council' }" type="button" @click="activeEmpireTab = 'council'"><Braces :size="15" /> Совет карт</button>
          </nav>
          <div class="days-left"><CalendarDays :size="17" /><span><b>{{ state.empire.daysRemaining }}</b> из {{ workingConfig.empire.daysPerPhase }} дней</span></div>
          <button v-if="!editorOpen" class="end-empire-button" type="button" @click="finishEmpire"><Play :size="15" /> Завершить управление</button>
        </div>

        <div class="resource-ribbon">
          <span v-for="resource in resourceRows" :key="resource.id" :title="resource.deferredReason">
            <Coins v-if="resource.id === goldResourceId" :size="14" />
            <Wheat v-else-if="resource.id === foodResourceId" :size="14" />
            <Landmark v-else :size="14" />
            <small>{{ resource.name }}{{ resource.deferredReason ? ' · будущее' : '' }}</small>
            <b>{{ formatNumber(resource.value) }}</b>
          </span>
        </div>

        <EmpireMap
          v-if="activeEmpireTab === 'map'"
          :regions="mapRegionViews"
          :subregions="mapSubregionViews"
          :active-region-id="activeRegionId"
          :editable="editorOpen"
          @select-region="activeRegionId = $event"
          @open-city="openCity"
          @move-object="moveMapObject"
          @add-object="addMapObject"
          @update-object="updateMapObject"
          @remove-object="removeMapObject"
          @add-subregion="addSubregion"
          @update-subregion="updateSubregion"
          @remove-subregion="removeSubregion"
        />

        <CityView
          v-else-if="activeEmpireTab === 'city'"
          :cities="cityViews"
          :active-city-id="activeCityId"
          :gold="state.empire.resources[goldResourceId] ?? 0"
          :selected-building-id="selectedBuildingId"
          :editor-mode="editorOpen"
          @select-city="activeCityId = $event"
          @select-building="(_, buildingId) => selectedBuildingId = buildingId"
          @upgrade="upgradeBuilding"
          @place="placeBuilding"
          @recruit="recruitUnits"
          @recruit-quantity="setRecruitQuantity"
          @toggle-boost="toggleProductionBoost"
          @open-population="populationCityId = $event"
          @edit-building="showMessage('Откройте вкладку «Здания» конструктора для параметров и графа зависимостей.')"
          @edit-slot="showMessage('Состав городских слотов доступен в полном JSON конструктора.')"
        />

        <TechTree
          v-else-if="activeEmpireTab === 'technology'"
          :nodes="technologyNodes"
          :editable="editorOpen"
          :selected-id="selectedTechnologyId"
          :knowledge="state.empire.resources[knowledgeResourceId] ?? 0"
          :gold="state.empire.resources[goldResourceId] ?? 0"
          :days="state.empire.daysRemaining"
          @select="selectedTechnologyId = $event"
          @research="research"
          @move-node="moveTechnology"
          @toggle-dependency="toggleTechnologyDependency"
        />

        <div v-else class="council-view">
          <header><div><span>Последствия карточной партии</span><h2>Совет карт</h2></div><b>{{ state.upgradePoints }} очков улучшений</b></header>
          <div v-if="councilCards.length" class="council-grid">
            <article v-for="card in councilCards" :key="card.instance.id">
              <EmpireCard
                v-bind="card.view"
                interactive
                :selected="selectedCouncilCardId === card.instance.id"
                :data-testid="`council-card-${card.instance.id}`"
                @choose="selectCouncilCard"
              />
              <div class="council-actions">
                <button :data-testid="`council-improve-${card.instance.id}`" type="button" :disabled="state.upgradePoints < workingConfig.upgrades.improveCost" @click="action(engine.improveCard(card.instance.id), false)"><Sparkles :size="14" /> Улучшить · {{ workingConfig.upgrades.improveCost }}</button>
                <button v-if="card.instance.inverted" :data-testid="`council-restore-${card.instance.id}`" type="button" :disabled="state.upgradePoints < workingConfig.upgrades.restoreCost" @click="action(engine.restoreCard(card.instance.id), false)"><RotateCcw :size="14" /> Восстановить · {{ workingConfig.upgrades.restoreCost }}</button>
                <small v-if="state.upgradePoints < workingConfig.upgrades.improveCost">Нужно {{ workingConfig.upgrades.improveCost }} очко кона</small>
              </div>
            </article>
          </div>
          <div v-else class="empty-council"><Shield :size="38" /><h3>Рука императора пуста</h3><p>Карты, оставшиеся после партии, станут пассивами вашего следующего периода правления.</p></div>
        </div>
      </section>

      <section v-else-if="state.phase === 'minigame' && state.minigame" class="phase-content minigame-phase">
        <TdBattle
          :key="`${state.minigame.id}:${state.minigame.attempt}`"
          :session="state.minigame"
          :qa-mode="qaMode"
          @resolved="resolveTdBattle"
          @abort="abortTdBattle"
        />
      </section>

      <section v-else-if="state.phase === 'event' && currentEvent" class="phase-content event-phase">
        <div class="event-stage"><ScrollText :size="44" /><span>Имперский совет ожидает решения</span></div>
        <EventDialog
          :open="true"
          :title="currentEvent.name"
          :description="currentEvent.description"
          :category="`Событие · кон ${state.con}`"
          :choices="eventChoiceViews"
          @choose="chooseEvent"
        />
      </section>

      <section v-else class="phase-content outcome-phase" :class="state.phase">
        <div class="outcome-sigil"><Crown v-if="state.phase === 'victory'" :size="52" /><Swords v-else :size="52" /></div><span>{{ state.phase === 'victory' ? 'Эпоха продолжается' : 'Летопись окончена' }}</span><h2>{{ phaseCopy[0] }}</h2><p>{{ phaseCopy[1] }}</p><dl><div><dt>Конов</dt><dd>{{ state.con }}</dd></div><div><dt>Очков</dt><dd>{{ state.upgradePoints }}</dd></div><div><dt>Население</dt><dd>{{ formatNumber(state.empire.cities.reduce((sum, city) => sum + city.population, 0)) }}</dd></div></dl><button type="button" @click="startNewCampaign(false)"><RotateCcw :size="16" /> Сделать новую ставку</button>
      </section>

      <PopulationDialog
        v-if="populationCity"
        :open="true"
        :city-name="populationCity.name"
        :total="populationCity.population"
        :non-working="nonWorkingPopulation"
        :loyalty="state.empire.flags[`loyalty:${populationCity.id}`] ?? 0"
        :categories="populationCategories"
        :editor-mode="editorOpen"
        @save="savePopulation"
        @close="populationCityId = null"
      />

      <BuilderDrawer
        v-if="editorOpen && editorConfig"
        :config="editorConfig"
        :dirty="editorDirty"
        @update:config="updateEditorConfig"
        @save="saveEditor"
        @reset="resetEditor"
        @export="exportConfig"
        @close="editorOpen = false"
      />
    </template>
  </main>
</template>

<style scoped>
.endgame-page { --gold: #d1b264; --ink: #ede3cd; position: relative; min-height: calc(100vh - 58px); overflow: hidden; padding: 20px clamp(12px, 2.6vw, 38px) 46px; color: var(--ink); background: #0b100d; font-family: Inter, system-ui, sans-serif; }
.endgame-atmosphere { position: fixed; inset: 58px 0 0; overflow: hidden; pointer-events: none; }
.endgame-atmosphere::before { content: ''; position: absolute; inset: 0; opacity: .35; background: radial-gradient(circle at 18% 12%, rgba(102, 128, 106, .22), transparent 32%), radial-gradient(circle at 88% 4%, rgba(77, 95, 127, .17), transparent 30%), repeating-linear-gradient(112deg, transparent 0 44px, rgba(255,255,255,.01) 45px 46px); }
.endgame-atmosphere i,.endgame-atmosphere b,.endgame-atmosphere span { position: absolute; width: 440px; height: 440px; border: 1px solid rgba(210, 179, 101, .035); border-radius: 50%; }
.endgame-atmosphere i { top: 12%; left: -250px; }.endgame-atmosphere b { top: 36%; right: -300px; }.endgame-atmosphere span { bottom: -310px; left: 40%; }
.state-screen { position: relative; z-index: 1; display: grid; min-height: 70vh; place-content: center; place-items: center; text-align: center; }
.state-screen h1 { margin: 14px 0 4px; font: 700 clamp(2rem,5vw,4rem)/1 Georgia,serif; }.state-screen p { color: rgba(237,227,205,.55); }.loading-sigil { color: var(--gold); animation: pulse 1.4s ease infinite; }.error-screen { color: #e0b9b0; }.state-screen button { display: inline-flex; align-items:center; gap:6px; padding:10px 14px; border:1px solid #94784a; border-radius:7px; color:#efe3ca; background:#332b1e; cursor:pointer; }
.campaign-header { position: relative; z-index: 2; display: grid; min-height: 68px; grid-template-columns: minmax(230px,1fr) auto minmax(230px,1fr); align-items: center; gap: 18px; max-width: 1500px; margin: 0 auto 13px; padding: 0 16px; border: 1px solid rgba(219,193,137,.16); border-radius: 12px; background: rgba(18,22,18,.93); box-shadow: 0 13px 40px rgba(0,0,0,.22); }
.qa-panel { position:relative; z-index:3; display:flex; max-width:1500px; align-items:center; gap:10px; margin:0 auto 13px; padding:9px 12px; border:1px solid rgba(105,174,178,.38); border-radius:9px; color:#c8e7e6; background:rgba(16,35,37,.96); font-size:.65rem; }.qa-panel > strong { margin-right:auto; color:#78c5c8; text-transform:uppercase; }.qa-panel label { display:flex; align-items:center; gap:5px; color:rgba(200,231,230,.62); }.qa-panel select,.qa-panel input,.qa-panel button { height:30px; border:1px solid rgba(120,197,200,.28); border-radius:5px; color:#d9efed; background:#142c2d; font:inherit; }.qa-panel select,.qa-panel input { padding:0 7px; }.qa-panel input { width:130px; }.qa-panel button { display:inline-flex; align-items:center; gap:4px; padding:0 9px; cursor:pointer; }.qa-panel code { color:#b4d9d8; }.qa-panel > span { color:#8ed19a; }
.title-lockup { display:flex; align-items:center; gap:10px; }.royal-seal { display:grid; width:42px; height:42px; place-items:center; border:1px solid rgba(212,180,101,.35); border-radius:50%; color:var(--gold); background:rgba(209,178,100,.07); }.title-lockup > div > span { color:#9e8d68; font:800 .55rem/1 monospace; letter-spacing:.12em; text-transform:uppercase; }.title-lockup h1 { margin:3px 0 0; font:700 1.22rem/1 Georgia,serif; }
.campaign-metrics { display:flex; gap:6px; }.campaign-metrics span { display:grid; min-width:58px; padding:7px 9px; border:1px solid rgba(217,191,133,.12); border-radius:7px; text-align:center; background:rgba(255,255,255,.022); }.campaign-metrics b { color:#e1c77f; font:800 .95rem/1 Georgia,serif; }.campaign-metrics small { margin-top:3px; color:rgba(237,227,205,.42); font:700 .5rem/1 monospace; text-transform:uppercase; }
.header-actions { display:flex; justify-content:flex-end; gap:5px; }.header-actions button { display:inline-flex; height:36px; align-items:center; justify-content:center; gap:5px; padding:0 10px; border:1px solid rgba(217,191,133,.15); border-radius:6px; color:#d8cdb7; background:rgba(255,255,255,.035); cursor:pointer; }.header-actions .builder-button { border-color:rgba(210,177,99,.32); color:#e3cc91; background:rgba(210,177,99,.08); }
.phase-banner { position:relative; z-index:2; display:flex; max-width:1500px; align-items:flex-end; justify-content:space-between; gap:25px; margin:0 auto 14px; padding:22px 24px; border:1px solid rgba(216,190,133,.14); border-radius:14px; background:linear-gradient(110deg,rgba(29,34,27,.96),rgba(19,25,24,.9)); }.phase-banner > div > span { color:var(--gold); font:800 .58rem/1 monospace; letter-spacing:.13em; text-transform:uppercase; }.phase-banner h2 { margin:5px 0 2px; font:700 clamp(1.6rem,3vw,2.35rem)/1 Georgia,serif; }.phase-banner p { margin:0; color:rgba(237,227,205,.53); font-size:.78rem; }.phase-banner ol { display:flex; margin:0; padding:0; list-style:none; }.phase-banner li { display:flex; min-width:76px; align-items:center; justify-content:center; gap:5px; padding:8px 10px; border-bottom:2px solid rgba(237,227,205,.12); color:rgba(237,227,205,.32); font-size:.62rem; }.phase-banner li.active { border-color:var(--gold); color:#ead28f; }.phase-banner li.passed { border-color:#6d8e72; color:#8eaa8f; }
.campaign-toast { position:sticky; z-index:30; top:68px; display:flex; width:fit-content; max-width:min(680px,90vw); align-items:center; gap:7px; margin:0 auto 12px; padding:8px 12px; border:1px solid rgba(216,186,111,.26); border-radius:30px; color:#dfcc9b; background:rgba(27,28,22,.96); box-shadow:0 10px 30px rgba(0,0,0,.28); font-size:.69rem; }
.phase-content { position:relative; z-index:2; max-width:1500px; margin:0 auto; }.cards-phase { display:grid; gap:10px; }.rules-note { display:flex; align-items:flex-start; gap:8px; padding:11px 14px; border:1px solid rgba(216,190,133,.12); border-radius:8px; color:rgba(237,227,205,.52); background:rgba(20,24,20,.82); font-size:.68rem; line-height:1.5; }.rules-note svg { flex:none; color:#b99e5d; }
.gift-phase { min-height:580px; padding:36px 0; }.gift-intro { max-width:640px; margin:0 auto 28px; text-align:center; }.gift-intro > svg { color:var(--gold); }.gift-intro > span { display:block; margin:9px 0 5px; color:#a9925f; font:800 .6rem/1 monospace; letter-spacing:.12em; text-transform:uppercase; }.gift-intro h2 { margin:0; font:700 2.2rem/1 Georgia,serif; }.gift-intro p { color:rgba(237,227,205,.5); }.gift-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:14px; max-width:1100px; margin:auto; }.gift-grid > button { position:relative; display:grid; min-height:360px; grid-template-rows:auto auto auto 1fr auto auto; place-items:center; padding:21px; overflow:hidden; border:1px solid rgba(211,182,112,.25); border-radius:14px; color:#eee3cc; background:radial-gradient(circle at 50% 20%,rgba(200,169,94,.11),transparent 34%),#171a15; text-align:center; cursor:pointer; transition:transform .15s,border-color .15s; }.gift-grid > button:hover { border-color:#c5a760; transform:translateY(-5px); }.gift-rarity { justify-self:end; color:#b29a64; font:800 .52rem/1 monospace; text-transform:uppercase; }.gift-sigil { display:grid; width:76px; height:76px; place-items:center; margin:11px; border:1px solid rgba(218,187,112,.28); border-radius:50%; color:#d4b564; background:rgba(210,177,95,.06); }.gift-grid h3 { margin:3px; font:700 1.35rem/1.1 Georgia,serif; }.gift-grid p { color:rgba(238,227,204,.54); font-size:.73rem; line-height:1.45; }.gift-grid ul { margin:0; padding:0; color:#c8b786; font-size:.67rem; list-style:none; }.gift-grid strong { align-self:end; margin-top:14px; padding:8px 13px; border-radius:6px; color:#261e12; background:#d6b866; font-size:.72rem; }
.empire-toolbar { display:flex; align-items:center; justify-content:space-between; gap:12px; margin-bottom:9px; padding:8px; border:1px solid rgba(216,190,133,.14); border-radius:10px; background:#141814; }.empire-toolbar nav { display:flex; flex-wrap:wrap; gap:4px; }.empire-toolbar nav button,.end-empire-button { display:inline-flex; min-height:36px; align-items:center; gap:5px; padding:0 11px; border:1px solid transparent; border-radius:6px; color:rgba(237,227,205,.5); background:transparent; cursor:pointer; }.empire-toolbar nav button.active { border-color:rgba(209,177,98,.25); color:#e1c779; background:rgba(209,177,98,.08); }.days-left { display:flex; align-items:center; gap:6px; color:#b8a77f; font-size:.68rem; }.days-left b { color:#e1c779; }.end-empire-button { border-color:#8f7845; color:#261f14; background:#c9aa5e; font-weight:800; }.resource-ribbon { display:flex; gap:6px; margin-bottom:9px; overflow-x:auto; }.resource-ribbon > span { display:grid; min-width:105px; grid-template-columns:auto 1fr; align-items:center; gap:2px 6px; padding:7px 10px; border:1px solid rgba(217,191,133,.12); border-radius:7px; background:rgba(18,22,18,.9); }.resource-ribbon svg { grid-row:1/3; color:#ac945a; }.resource-ribbon small { color:rgba(237,227,205,.42); font-size:.52rem; }.resource-ribbon b { font:800 .76rem/1 Georgia,serif; }
.council-view { min-height:550px; padding:20px; border:1px solid rgba(216,190,133,.15); border-radius:16px; background:rgba(18,22,18,.93); }.council-view > header { display:flex; align-items:end; justify-content:space-between; margin-bottom:18px; }.council-view header span { color:#a9935f; font:800 .56rem/1 monospace; text-transform:uppercase; }.council-view h2 { margin:4px 0 0; font:700 1.8rem/1 Georgia,serif; }.council-view header > b { color:#ddc37b; }.council-grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(220px,1fr)); gap:18px; }.council-grid article { display:grid; justify-items:center; align-content:start; gap:9px; }.council-actions { display:grid; width:184px; gap:5px; }.council-actions > button { display:flex; align-items:center; justify-content:center; gap:5px; min-height:34px; border:1px solid rgba(212,180,100,.28); border-radius:6px; color:#deca94; background:rgba(210,176,95,.08); font-size:.65rem; cursor:pointer; }.council-actions > button:disabled { opacity:.42; cursor:not-allowed; }.council-actions > small { color:rgba(237,227,205,.5); font-size:.58rem; text-align:center; }.empty-council { display:grid; min-height:390px; place-content:center; place-items:center; color:rgba(237,227,205,.4); text-align:center; }.empty-council h3 { margin:12px 0 4px; color:#e5d9c2; }
.event-phase { display:grid; min-height:600px; place-items:center; }.event-card { max-width:760px; padding:34px; border:1px solid rgba(215,184,108,.24); border-radius:16px; background:radial-gradient(circle at 50% 10%,rgba(189,158,90,.1),transparent 28%),#171a15; text-align:center; box-shadow:0 28px 90px rgba(0,0,0,.4); }.event-card > span { color:#ae955c; font:800 .58rem/1 monospace; letter-spacing:.12em; text-transform:uppercase; }.event-icon { display:grid; width:85px; height:85px; place-items:center; margin:18px auto; border:1px solid rgba(217,184,105,.27); border-radius:50%; color:#d2b464; }.event-card h2 { margin:0; font:700 2rem/1 Georgia,serif; }.event-card > p { color:rgba(237,227,205,.55); line-height:1.55; }.event-choices { display:grid; gap:8px; margin-top:24px; text-align:left; }.event-choices button { display:grid; gap:4px; padding:13px 15px; border:1px solid rgba(215,184,108,.16); border-radius:8px; color:#eadfc8; background:rgba(255,255,255,.025); cursor:pointer; }.event-choices button:hover { border-color:#b59856; background:rgba(181,152,86,.07); }.event-choices span { color:rgba(237,227,205,.52); font-size:.7rem; }.event-choices small { color:#bca873; }.event-choices em { color:#83a883; font-size:.65rem; font-style:normal; }
.outcome-phase { display:grid; min-height:620px; place-content:center; place-items:center; text-align:center; }.outcome-sigil { display:grid; width:120px; height:120px; place-items:center; border:1px solid rgba(212,179,99,.34); border-radius:50%; color:#d7b763; background:rgba(208,175,94,.07); }.outcome-phase.defeat .outcome-sigil { border-color:rgba(169,74,76,.35); color:#bd6e70; background:rgba(130,42,46,.1); }.outcome-phase > span { margin-top:18px; color:#aa935d; font:800 .58rem/1 monospace; letter-spacing:.14em; text-transform:uppercase; }.outcome-phase h2 { margin:7px 0; font:700 2.8rem/1 Georgia,serif; }.outcome-phase > p { max-width:580px; color:rgba(237,227,205,.53); }.outcome-phase dl { display:flex; gap:8px; }.outcome-phase dl div { min-width:105px; padding:11px; border:1px solid rgba(216,190,133,.12); border-radius:8px; }.outcome-phase dt { color:rgba(237,227,205,.42); font-size:.57rem; }.outcome-phase dd { margin:5px 0 0; color:#dec47c; font:800 1.1rem/1 Georgia,serif; }.outcome-phase > button { display:flex; align-items:center; gap:6px; margin-top:20px; padding:11px 15px; border:1px solid #927746; border-radius:7px; color:#271f12; background:#d0af5d; font-weight:800; cursor:pointer; }
.endgame-page.editor-active { padding-right: min(620px, 43vw); }
@keyframes pulse { 50% { opacity:.45; transform:scale(.94); } }
@media (max-width: 900px) { .campaign-header { grid-template-columns:1fr auto; }.campaign-metrics { display:none; }.qa-panel { align-items:stretch; flex-wrap:wrap; }.qa-panel > strong { width:100%; }.phase-banner { align-items:flex-start; flex-direction:column; }.phase-banner ol { width:100%; }.phase-banner li { min-width:0; flex:1; }.gift-grid { grid-template-columns:1fr; max-width:530px; }.gift-grid > button { min-height:300px; }.empire-toolbar { align-items:stretch; flex-direction:column; }.days-left { align-self:center; }.end-empire-button { justify-content:center; }.header-actions .builder-button span { display:none; } }
@media (max-width: 900px) { .endgame-page.editor-active { padding-right: clamp(12px, 2.6vw, 38px); } }
@media (max-width: 600px) { .endgame-page, .endgame-page.editor-active { padding-inline:7px; }.campaign-header { min-height:58px; padding:0 9px; }.royal-seal { display:none; }.title-lockup h1 { font-size:.95rem; }.header-actions button { width:32px; height:32px; padding:0; }.phase-banner { padding:16px; }.phase-banner p { font-size:.69rem; }.phase-banner li span { display:none; }.phase-banner li { min-height:32px; }.empire-toolbar nav { display:grid; grid-template-columns:1fr 1fr; }.outcome-phase dl { flex-wrap:wrap; justify-content:center; } }
</style>
