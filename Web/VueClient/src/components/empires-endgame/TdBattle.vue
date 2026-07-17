<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, shallowRef } from 'vue'
import {
  consumeTdFrameTime,
  createTdSimulation,
  nextTdTowerGrade,
  replayTdBattle,
  stepTdSimulation,
  tdCommandDisabledReason,
} from '../../features/empires-endgame/td/engine'
import {
  resolveTdWithPolicy,
  TD_QA_POLICIES,
  type TdQaPolicy,
} from '../../features/empires-endgame/td/qa'
import type {
  EmpiresMinigameSession,
  TdBattleResult,
  TdBattlefieldModifierDefinition,
  TdCommand,
  TdPoint,
  TdTowerBaseDefinition,
  TdTowerChoiceDefinition,
} from '../../features/empires-endgame/types'

const props = defineProps<{
  session: EmpiresMinigameSession
  qaMode?: boolean
}>()

const emit = defineEmits<{
  resolved: [result: TdBattleResult]
  abort: []
}>()

interface CommandOption<T extends TdTowerBaseDefinition | TdTowerChoiceDefinition> {
  definition: T
  command: TdCommand
  reason: string | null
}

const canvas = ref<HTMLCanvasElement | null>(null)
const simulation = shallowRef(createTdSimulation(props.session.plan, props.session.seed))
const commandLog = ref<TdCommand[]>([])
const pendingCommands = ref<TdCommand[]>([])
const paused = ref(true)
const backgrounded = ref(typeof document !== 'undefined' && document.hidden)
const speed = ref<1 | 2 | 4>(1)
const selectedSpotId = ref(props.session.plan.battlefield.buildSpots[0]?.id ?? '')
const qaPolicy = ref<TdQaPolicy>('balanced')
const commandStatus = ref('')
const emitted = ref(false)
let frameId = 0
let previousFrame: number | null = null
let accumulator = 0
const previousEntityPositions = new Map<string, TdPoint>()

const plan = computed(() => props.session.plan)
const isRunning = computed(() => !paused.value && !backgrounded.value
  && simulation.value.terminalReason === null)
const modeLabel = computed(() => plan.value.mode === 'assault' ? 'Штурм' : 'Оборона')
const actionLabel = computed(() => plan.value.mode === 'assault' ? 'штурм' : 'оборону')
const selectedSpot = computed(() => plan.value.battlefield.buildSpots
  .find(spot => spot.id === selectedSpotId.value) ?? null)
const selectedTower = computed(() => simulation.value.towers
  .find(tower => tower.spotId === selectedSpotId.value) ?? null)
const nextGrade = computed(() => selectedTower.value ? nextTdTowerGrade(selectedTower.value) : 1)
const currentGradeSet = computed(() => plan.value.gradeChoices.find(set => (
  set.regionId === plan.value.battlefield.regionId && set.grade === nextGrade.value
)) ?? null)
const enemiesRemaining = computed(() => simulation.value.enemies.filter(enemy => enemy.hp > 0).length)
const totalEnemies = computed(() => plan.value.wave.groups.reduce((sum, group) => sum + group.count, 0))
const deploymentCount = computed(() => plan.value.deployments.reduce((sum, item) => sum + item.count, 0))
const objectiveHealth = computed(() => Math.max(0, Math.ceil(simulation.value.objectiveHp)))
const commandCapacity = computed(() => Math.max(0, plan.value.maxCommands - commandLog.value.length))

const towerRows = computed(() => simulation.value.towers.map((tower) => {
  const base = plan.value.towerBases.find(item => item.id === tower.towerBaseId)
  return {
    id: tower.spotId,
    name: base?.name ?? tower.towerBaseId,
    grade: tower.choiceIds.length,
    hp: Math.max(0, Math.ceil(tower.hp)),
  }
}))

const squadRows = computed(() => simulation.value.squads.map((squad) => ({
  id: squad.deploymentId,
  name: `${squad.unitId} · ${squad.cityId}`,
  count: squad.count,
  hp: Math.max(0, Math.ceil(squad.hp)),
  maxHp: Math.ceil(squad.maxHp),
})))

const enemyGroupRows = computed(() => plan.value.wave.groups.map(group => ({
  id: group.id,
  name: group.id,
  categories: group.categoryIds.join(', '),
  spawned: simulation.value.spawnedByGroup[group.id] ?? 0,
  total: group.count,
  alive: simulation.value.enemies.filter(enemy => enemy.groupId === group.id && enemy.hp > 0).length,
})))

const regionalRules = computed(() => {
  const rules = plan.value.battlefield.modifiers.map(describeModifier)
  if (plan.value.battlefield.allowedTowerCategoryIds.length > 0) {
    rules.unshift(`Разрешённые категории башен: ${plan.value.battlefield.allowedTowerCategoryIds.join(', ')}.`)
  }
  if (rules.length === 0) rules.push('Особых региональных модификаторов нет.')
  return rules
})

function commandIdentity() {
  return {
    tick: simulation.value.tick,
    sequence: commandLog.value.length,
    sessionId: plan.value.sessionId,
    planId: plan.value.id,
  }
}

function buildCommand(towerBaseId: string): TdCommand {
  return {
    ...commandIdentity(),
    kind: 'build-tower',
    spotId: selectedSpotId.value,
    towerBaseId,
  }
}

function upgradeCommand(choiceId: string): TdCommand {
  return {
    ...commandIdentity(),
    kind: 'upgrade-tower',
    spotId: selectedSpotId.value,
    choiceId,
  }
}

function globalCommandDisabledReason(): string | null {
  if (simulation.value.terminalReason) return `Бой завершён: ${simulation.value.terminalReason}.`
  if (backgrounded.value) return 'Бой приостановлен, пока вкладка находится в фоне.'
  if (paused.value) return `Запустите ${actionLabel.value}, чтобы отдавать приказы.`
  if (commandLog.value.length >= plan.value.maxCommands) {
    return `Достигнут лимит журнала: ${plan.value.maxCommands} команд.`
  }
  if (pendingCommands.value.length > 0) return 'Предыдущая команда ожидает следующего тика.'
  return null
}

function disabledReason(command: TdCommand): string | null {
  return globalCommandDisabledReason()
    ?? tdCommandDisabledReason(plan.value, simulation.value, command)
}

const buildOptions = computed<CommandOption<TdTowerBaseDefinition>[]>(() => {
  if (selectedTower.value) return []
  return plan.value.towerBases
    .filter(base => plan.value.battlefield.towerBaseIds.includes(base.id))
    .map((definition) => {
      const command = buildCommand(definition.id)
      return { definition, command, reason: disabledReason(command) }
    })
})

const upgradeOptions = computed<CommandOption<TdTowerChoiceDefinition>[]>(() => {
  if (!selectedTower.value || !currentGradeSet.value || currentGradeSet.value.deferredReason) return []
  const choices = new Map(plan.value.towerChoices.map(choice => [choice.id, choice]))
  return currentGradeSet.value.choiceIds.flatMap((choiceId) => {
    const definition = choices.get(choiceId)
    if (!definition) return []
    const command = upgradeCommand(definition.id)
    return [{ definition, command, reason: disabledReason(command) }]
  })
})

const choicePanelMessage = computed(() => {
  if (!selectedSpot.value) return 'На этом поле нет доступной позиции для башни.'
  if (!selectedTower.value) {
    return buildOptions.value.length > 0
      ? 'Сначала выберите основу башни.'
      : 'Для этой позиции нет доступной основы башни.'
  }
  if (nextGrade.value > 4) return 'Башня получила все четыре последовательных грейда.'
  if (currentGradeSet.value?.deferredReason) return currentGradeSet.value.deferredReason
  if (!currentGradeSet.value) return `Грейд ${nextGrade.value} не настроен для этого региона.`
  return 'Выберите одно из доступных улучшений.'
})

function describeModifier(modifier: TdBattlefieldModifierDefinition): string {
  if (modifier.kind === 'tower-targeting') {
    const terrain = modifier.terrainIds.join(', ')
    if (modifier.targetableByEnemyCategoryIds.length === 0) {
      return `Башни на участках ${terrain} недоступны для атак врагов.`
    }
    return `Башни на участках ${terrain} могут атаковать только враги категорий: ${modifier.targetableByEnemyCategoryIds.join(', ')}.`
  }
  if (modifier.kind === 'tower-stat') {
    return `Башни на участках ${modifier.terrainIds.join(', ')}: дальность ×${modifier.rangeMultiplier}, прочность ×${modifier.maxHpMultiplier}.`
  }
  const modes = modifier.modes.map(mode => mode === 'assault' ? 'штурм' : 'оборона').join(', ')
  return `Истощение (${modes}): каждые ${modifier.intervalTicks} тиков отряды теряют ${modifier.damagePerUnit} HP за бойца.`
}

function canvasPoint(point: TdPoint): TdPoint {
  const field = plan.value.battlefield
  return {
    x: point.x / field.width * 1_000,
    y: point.y / field.height * 600,
  }
}

function drawCircle(
  context: CanvasRenderingContext2D,
  point: TdPoint,
  radius: number,
  fill: string,
  stroke = 'transparent',
) {
  const position = canvasPoint(point)
  context.beginPath()
  context.arc(position.x, position.y, radius, 0, Math.PI * 2)
  context.fillStyle = fill
  context.fill()
  if (stroke !== 'transparent') {
    context.strokeStyle = stroke
    context.stroke()
  }
}

function interpolatedEntityPoint(id: string, current: TdPoint): TdPoint {
  const previous = previousEntityPositions.get(id)
  if (!previous || !isRunning.value) return current
  const alpha = Math.max(0, Math.min(1, accumulator / plan.value.tickMs))
  return {
    x: previous.x + (current.x - previous.x) * alpha,
    y: previous.y + (current.y - previous.y) * alpha,
  }
}

function render() {
  const context = canvas.value?.getContext('2d')
  if (!context) return
  const field = plan.value.battlefield
  context.clearRect(0, 0, 1_000, 600)
  const gradient = context.createLinearGradient(0, 0, 1_000, 600)
  gradient.addColorStop(0, plan.value.mode === 'assault' ? '#241b1a' : '#17281f')
  gradient.addColorStop(1, '#0b1715')
  context.fillStyle = gradient
  context.fillRect(0, 0, 1_000, 600)

  const nodes = new Map(field.laneGraph.nodes.map(node => [node.id, node]))
  context.lineWidth = 24
  context.lineCap = 'round'
  context.strokeStyle = 'rgba(151, 124, 76, .34)'
  for (const edge of field.laneGraph.edges) {
    const from = nodes.get(edge.fromNodeId)
    const to = nodes.get(edge.toNodeId)
    if (!from || !to) continue
    const start = canvasPoint(from)
    const end = canvasPoint(to)
    context.beginPath()
    context.moveTo(start.x, start.y)
    context.lineTo(end.x, end.y)
    context.stroke()
  }

  for (const spot of field.buildSpots) {
    const selected = spot.id === selectedSpotId.value
    drawCircle(context, spot, 16, selected ? '#d6b35f' : '#3d5146', '#e5ce91')
  }
  for (const tower of simulation.value.towers) {
    const spot = field.buildSpots.find(item => item.id === tower.spotId)
    if (!spot) continue
    const position = canvasPoint(spot)
    context.fillStyle = '#d8c08a'
    context.fillRect(position.x - 12, position.y - 28, 24, 30)
    context.fillStyle = '#705c36'
    context.fillRect(position.x - 16, position.y - 32, 32, 8)
  }
  for (const squad of simulation.value.squads.filter(item => item.hp > 0)) {
    drawCircle(
      context,
      interpolatedEntityPoint(`squad:${squad.deploymentId}`, squad),
      11,
      '#5ca6bd',
      '#c3eaf4',
    )
  }
  for (const enemy of simulation.value.enemies.filter(item => item.hp > 0)) {
    drawCircle(
      context,
      interpolatedEntityPoint(`enemy:${enemy.id}`, enemy),
      8,
      '#a34e4e',
      '#f0a9a1',
    )
  }
  const objective = nodes.get(field.objectiveNodeId)
  if (objective) {
    const position = canvasPoint(objective)
    context.fillStyle = plan.value.objective.owner === 'enemy' ? '#9a655d' : '#c9b279'
    context.fillRect(position.x - 30, position.y - 36, 60, 58)
    context.fillStyle = '#7d6a40'
    context.fillRect(position.x - 36, position.y - 45, 18, 18)
    context.fillRect(position.x + 18, position.y - 45, 18, 18)
  }
}

function rememberEntityPositions() {
  previousEntityPositions.clear()
  for (const squad of simulation.value.squads) {
    previousEntityPositions.set(`squad:${squad.deploymentId}`, { x: squad.x, y: squad.y })
  }
  for (const enemy of simulation.value.enemies) {
    previousEntityPositions.set(`enemy:${enemy.id}`, { x: enemy.x, y: enemy.y })
  }
}

function finishFromReplay() {
  if (emitted.value || !simulation.value.terminalReason) return
  emitted.value = true
  paused.value = true
  emit('resolved', replayTdBattle(plan.value, props.session.seed, commandLog.value))
}

function runFrame(timestamp: number) {
  if (previousFrame === null) previousFrame = timestamp
  const elapsed = Math.max(0, timestamp - previousFrame)
  previousFrame = timestamp

  if (isRunning.value) {
    const clock = consumeTdFrameTime(
      accumulator,
      elapsed * speed.value,
      plan.value.tickMs,
      plan.value.maxCatchUpTicksPerFrame,
    )
    accumulator = clock.accumulatorMs
    if (clock.ticks > 0) rememberEntityPositions()
    for (let index = 0; index < clock.ticks && !simulation.value.terminalReason; index += 1) {
      const tick = simulation.value.tick
      const commands = pendingCommands.value.filter(command => command.tick === tick)
      if (commands.length > 0) {
        pendingCommands.value = pendingCommands.value.filter(command => command.tick !== tick)
      }
      stepTdSimulation(plan.value, simulation.value, commands)
    }
    if (clock.ticks > 0) {
      simulation.value = { ...simulation.value }
      finishFromReplay()
    }
  } else {
    accumulator = 0
  }

  render()
  frameId = window.requestAnimationFrame(runFrame)
}

function queueCommand(command: TdCommand, reason: string | null) {
  if (reason) {
    commandStatus.value = reason
    return
  }
  commandLog.value.push(command)
  pendingCommands.value.push(command)
  commandStatus.value = `Команда ${command.sequence + 1} принята на тик ${command.tick}.`
}

function togglePause() {
  if (backgrounded.value) {
    commandStatus.value = 'Вернитесь на вкладку, чтобы продолжить бой.'
    return
  }
  if (simulation.value.terminalReason) return
  paused.value = !paused.value
  accumulator = 0
  previousFrame = null
}

function setSpeed(multiplier: 1 | 2 | 4) {
  speed.value = multiplier
  accumulator = 0
  previousFrame = null
}

function handleVisibilityChange() {
  backgrounded.value = document.hidden
  accumulator = 0
  previousFrame = null
  commandStatus.value = backgrounded.value
    ? 'Бой приостановлен: вкладка находится в фоне.'
    : 'Вкладка активна; бой продолжится без накопленного фонового времени.'
}

function fastResolve() {
  if (!props.qaMode || emitted.value) return
  emitted.value = true
  paused.value = true
  emit('resolved', resolveTdWithPolicy(plan.value, props.session.seed, qaPolicy.value))
}

onMounted(() => {
  document.addEventListener('visibilitychange', handleVisibilityChange)
  render()
  frameId = window.requestAnimationFrame(runFrame)
})

onUnmounted(() => {
  document.removeEventListener('visibilitychange', handleVisibilityChange)
  window.cancelAnimationFrame(frameId)
})
</script>

<template>
  <section class="td-battle" data-testid="td-battle">
    <header class="td-hud" data-testid="td-hud">
      <div>
        <span>{{ modeLabel }}</span>
        <strong>{{ plan.battlefield.name }}</strong>
        <small>{{ plan.objective.name }} · {{ plan.objective.kind === 'castle' ? 'замок' : 'форт' }}</small>
      </div>
      <dl aria-label="Состояние боя">
        <div>
          <dt>{{ plan.objective.owner === 'player' ? 'Наша цель' : 'Вражеская цель' }}</dt>
          <dd data-testid="td-objective-health">{{ objectiveHealth }} / {{ plan.objective.maxHp }}</dd>
        </div>
        <div><dt>Враги на поле</dt><dd>{{ enemiesRemaining }} / {{ totalEnemies }}</dd></div>
        <div><dt>Ресурс</dt><dd>{{ simulation.buildResources }}</dd></div>
        <div><dt>Тик</dt><dd>{{ simulation.tick }} / {{ plan.maxTicks }}</dd></div>
        <div><dt>Команды</dt><dd>{{ commandLog.length }} / {{ plan.maxCommands }}</dd></div>
      </dl>
      <div class="td-controls">
        <button
          type="button"
          data-testid="td-start"
          :aria-pressed="!paused"
          :disabled="Boolean(simulation.terminalReason)"
          @click="togglePause"
        >
          {{ paused ? (simulation.tick === 0 ? `Начать ${actionLabel}` : 'Продолжить') : 'Пауза' }}
        </button>
        <button
          v-for="multiplier in ([1, 2, 4] as const)"
          :key="multiplier"
          type="button"
          :aria-label="`Скорость ${multiplier}`"
          :aria-pressed="speed === multiplier"
          :class="{ active: speed === multiplier }"
          @click="setSpeed(multiplier)"
        >×{{ multiplier }}</button>
        <button type="button" class="danger" data-testid="td-abort" @click="emit('abort')">Отступить</button>
      </div>
    </header>

    <p
      class="command-status"
      data-testid="td-command-status"
      role="status"
      aria-live="polite"
    >
      {{ commandStatus || (backgrounded ? 'Бой приостановлен в фоне.' : `Осталось мест в журнале команд: ${commandCapacity}.`) }}
    </p>

    <section class="regional-rules" data-testid="td-regional-rules" aria-labelledby="td-regional-rules-title">
      <h3 id="td-regional-rules-title">Правила поля</h3>
      <ul><li v-for="rule in regionalRules" :key="rule">{{ rule }}</li></ul>
    </section>

    <section class="deployment" data-testid="td-deployment" aria-labelledby="td-deployment-title">
      <strong id="td-deployment-title">{{ plan.mode === 'assault' ? 'Штурмовые отряды' : 'План развёртывания' }} · {{ deploymentCount }} бойцов</strong>
      <span v-if="plan.deployments.length === 0">Армия не прибыла — поле удерживают башни.</span>
      <span v-for="item in plan.deployments" :key="item.id">
        {{ item.unitId }} · {{ item.cityId }} · ×{{ item.count }} · {{ item.speedPerSecond }} ед/с
      </span>
    </section>

    <div class="td-stage">
      <canvas
        ref="canvas"
        width="1000"
        height="600"
        role="img"
        :aria-label="`${modeLabel}: ${plan.battlefield.name}; цель — ${plan.objective.name}`"
      />
      <nav class="spot-picker" aria-label="Позиции башен">
        <button
          v-for="(spot, index) in plan.battlefield.buildSpots"
          :key="spot.id"
          type="button"
          :aria-pressed="selectedSpotId === spot.id"
          :class="{ active: selectedSpotId === spot.id }"
          @click="selectedSpotId = spot.id"
        >Позиция {{ index + 1 }} · {{ spot.terrainId }}</button>
      </nav>
    </div>

    <div class="state-panels" data-testid="td-text-state">
      <section aria-labelledby="td-enemies-title">
        <h3 id="td-enemies-title">Вражеские группы</h3>
        <ul>
          <li v-for="group in enemyGroupRows" :key="group.id">
            <strong>{{ group.name }}</strong>
            <span>{{ group.alive }} на поле · {{ group.spawned }} / {{ group.total }} появились · {{ group.categories }}</span>
          </li>
        </ul>
      </section>
      <section aria-labelledby="td-squads-title">
        <h3 id="td-squads-title">Наши отряды</h3>
        <p v-if="squadRows.length === 0">Развёрнутых отрядов нет.</p>
        <ul v-else>
          <li v-for="squad in squadRows" :key="squad.id">
            <strong>{{ squad.name }} · ×{{ squad.count }}</strong>
            <span>{{ squad.hp }} / {{ squad.maxHp }} HP</span>
          </li>
        </ul>
      </section>
      <section aria-labelledby="td-towers-title">
        <h3 id="td-towers-title">Башни</h3>
        <p v-if="towerRows.length === 0">Построенных башен нет.</p>
        <ul v-else>
          <li v-for="tower in towerRows" :key="tower.id">
            <strong>{{ tower.name }} · {{ tower.id }}</strong>
            <span>Грейдов: {{ tower.grade }} · {{ tower.hp }} HP</span>
          </li>
        </ul>
      </section>
    </div>

    <aside class="grade-drawer" data-testid="td-grade-drawer" aria-labelledby="td-choice-title">
      <div>
        <span>{{ selectedTower ? 'Последовательный грейд' : 'Основа башни' }}</span>
        <strong id="td-choice-title">{{ selectedTower ? (nextGrade <= 4 ? `Грейд ${nextGrade}` : 'Башня завершена') : 'Строительство' }}</strong>
        <small>{{ choicePanelMessage }}</small>
      </div>
      <button
        v-for="option in buildOptions"
        :key="option.definition.id"
        type="button"
        :data-testid="`td-build-${option.definition.id}`"
        :disabled="Boolean(option.reason)"
        :aria-describedby="option.reason ? `td-reason-${option.definition.id}` : undefined"
        @click="queueCommand(option.command, option.reason)"
      >
        <strong>{{ option.definition.name }}</strong>
        <span>{{ option.definition.cost }} ресурса · {{ option.definition.categoryIds.join(', ') }}</span>
        <small v-if="option.reason" :id="`td-reason-${option.definition.id}`" class="disabled-reason">{{ option.reason }}</small>
      </button>
      <button
        v-for="option in upgradeOptions"
        :key="option.definition.id"
        type="button"
        :data-testid="`td-upgrade-${option.definition.id}`"
        :disabled="Boolean(option.reason)"
        :aria-describedby="option.reason ? `td-reason-${option.definition.id}` : undefined"
        @click="queueCommand(option.command, option.reason)"
      >
        <strong>{{ option.definition.name }}</strong>
        <span>{{ option.definition.cost }} ресурса · грейд {{ option.definition.grade }}</span>
        <small v-if="option.reason" :id="`td-reason-${option.definition.id}`" class="disabled-reason">{{ option.reason }}</small>
      </button>
    </aside>

    <div v-if="qaMode" class="qa-fast-resolve" data-testid="td-qa-controls">
      <label>
        QA-политика
        <select v-model="qaPolicy" data-testid="td-qa-policy">
          <option v-for="policy in TD_QA_POLICIES" :key="policy" :value="policy">{{ policy }}</option>
        </select>
      </label>
      <button type="button" data-testid="td-fast-resolve" @click="fastResolve">Быстро разрешить</button>
    </div>
  </section>
</template>

<style scoped>
.td-battle { display:grid; gap:14px; color:#eadfca; }
.td-hud { display:grid; grid-template-columns:minmax(180px,1fr) auto minmax(260px,1fr); align-items:center; gap:18px; padding:13px 16px; border:1px solid rgba(214,179,95,.25); border-radius:10px; background:rgba(13,25,22,.93); }
.td-hud > div:first-child { display:grid; gap:3px; }
.td-hud > div:first-child span,.grade-drawer > div span { color:#b18e4f; font:800 .58rem/1 monospace; letter-spacing:.12em; text-transform:uppercase; }
.td-hud > div:first-child strong { font:700 1.15rem/1.1 Georgia,serif; }
.td-hud > div:first-child small { color:rgba(234,223,202,.58); }
.td-hud dl { display:flex; gap:7px; margin:0; }
.td-hud dl div { min-width:78px; padding:7px 9px; border:1px solid rgba(235,217,178,.12); border-radius:6px; text-align:center; }
.td-hud dt { color:rgba(234,223,202,.5); font-size:.57rem; }
.td-hud dd { margin:4px 0 0; color:#e1c477; font:800 .78rem/1 monospace; }
.td-controls { display:flex; justify-content:flex-end; gap:5px; }
.td-controls button,.spot-picker button,.qa-fast-resolve button,.qa-fast-resolve select { padding:7px 9px; border:1px solid rgba(213,182,111,.26); border-radius:5px; color:#eadfca; background:#26342c; cursor:pointer; }
.td-controls button.active,.spot-picker button.active { border-color:#d5b767; color:#211b10; background:#d5b767; }
.td-controls .danger { border-color:rgba(188,91,83,.45); color:#e4a49d; }
.command-status { margin:0; padding:8px 12px; border-left:3px solid #6f9e9f; color:#b9d7d5; background:rgba(22,48,49,.55); font-size:.68rem; }
.regional-rules { padding:10px 14px; border:1px solid rgba(214,179,95,.17); border-radius:8px; background:rgba(28,35,29,.72); }
.regional-rules h3,.state-panels h3 { margin:0 0 7px; color:#d6b35f; font:700 .72rem/1.1 Georgia,serif; }
.regional-rules ul { display:grid; gap:4px; margin:0; padding-left:18px; color:#c9bea8; font-size:.66rem; }
.deployment { display:flex; align-items:center; gap:14px; padding:10px 14px; overflow:auto; border:1px dashed rgba(116,173,183,.3); border-radius:8px; color:#a9ced2; background:rgba(23,50,52,.52); font-size:.67rem; white-space:nowrap; }
.td-stage { position:relative; overflow:hidden; border:1px solid rgba(214,179,95,.24); border-radius:12px; background:#0b1715; box-shadow:0 20px 55px rgba(0,0,0,.25); }
.td-stage canvas { display:block; width:100%; height:auto; aspect-ratio:5/3; }
.spot-picker { position:absolute; right:10px; bottom:10px; display:flex; gap:5px; max-width:calc(100% - 20px); padding:6px; overflow:auto; border-radius:7px; background:rgba(8,16,14,.83); }
.state-panels { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:8px; }
.state-panels section { min-height:94px; padding:11px; border:1px solid rgba(116,173,183,.18); border-radius:8px; background:rgba(15,30,29,.72); }
.state-panels ul { display:grid; gap:6px; margin:0; padding:0; list-style:none; }
.state-panels li,.state-panels p { display:grid; gap:2px; margin:0; color:rgba(234,223,202,.62); font-size:.62rem; }
.state-panels li strong { color:#d8cfbd; }
.grade-drawer { display:grid; grid-template-columns:180px repeat(4,minmax(130px,1fr)); gap:8px; }
.grade-drawer > div,.grade-drawer > button { min-height:82px; padding:12px; border:1px solid rgba(214,179,95,.2); border-radius:8px; color:#eadfca; background:rgba(24,35,30,.9); text-align:left; }
.grade-drawer > div { display:grid; align-content:center; gap:5px; }
.grade-drawer small { color:rgba(234,223,202,.52); }
.grade-drawer > button { display:grid; align-content:center; gap:6px; cursor:pointer; }
.grade-drawer > button span { color:#c7aa67; font-size:.62rem; }
.grade-drawer > button:disabled { cursor:not-allowed; opacity:.58; }
.grade-drawer .disabled-reason { color:#dda79f; font-size:.58rem; }
.qa-fast-resolve { display:flex; justify-content:flex-end; align-items:center; gap:8px; padding:9px; border:1px solid rgba(105,174,178,.3); border-radius:8px; background:rgba(16,35,37,.8); }
.qa-fast-resolve label { display:flex; align-items:center; gap:8px; color:#9acacc; font-size:.65rem; }
button:focus-visible,select:focus-visible { outline:3px solid #8bd1dc; outline-offset:3px; }
button:disabled { cursor:not-allowed; }
@media (max-width: 900px) { .td-hud { grid-template-columns:1fr; }.td-controls { justify-content:flex-start; flex-wrap:wrap; }.td-hud dl { overflow:auto; }.grade-drawer { grid-template-columns:1fr 1fr; }.grade-drawer > div { grid-column:1/-1; }.spot-picker { position:static; max-width:none; border-radius:0; }.td-stage canvas { min-width:720px; }.td-stage { overflow:auto; }.state-panels { grid-template-columns:1fr; } }
</style>
