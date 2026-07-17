<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, shallowRef } from 'vue'
import {
  createTdSimulation,
  replayTdBattle,
  stepTdSimulation,
} from '../../features/empires-endgame/td/engine'
import {
  resolveTdWithPolicy,
  TD_QA_POLICIES,
  type TdQaPolicy,
} from '../../features/empires-endgame/td/qa'
import type {
  EmpiresMinigameSession,
  TdBattleResult,
  TdCommand,
  TdPoint,
} from '../../features/empires-endgame/types'

const props = defineProps<{
  session: EmpiresMinigameSession
  qaMode?: boolean
}>()

const emit = defineEmits<{
  resolved: [result: TdBattleResult]
  abort: []
}>()

const canvas = ref<HTMLCanvasElement | null>(null)
const simulation = shallowRef(createTdSimulation(props.session.plan, props.session.seed))
const commandLog = ref<TdCommand[]>([])
const pendingCommands = ref<TdCommand[]>([])
const paused = ref(true)
const speed = ref<1 | 2 | 4>(1)
const selectedSpotId = ref(props.session.plan.battlefield.buildSpots[0]?.id ?? '')
const qaPolicy = ref<TdQaPolicy>('balanced')
const emitted = ref(false)
let frameId = 0
let previousFrame = 0
let accumulator = 0
const previousEntityPositions = new Map<string, TdPoint>()

const plan = computed(() => props.session.plan)
const selectedTower = computed(() => simulation.value.towers
  .find(tower => tower.spotId === selectedSpotId.value) ?? null)
const nextGrade = computed(() => (selectedTower.value?.choiceIds.length ?? 0) + 1)
const gradeChoices = computed(() => plan.value.towerChoices
  .filter(choice => choice.grade === nextGrade.value))
const enemiesRemaining = computed(() => simulation.value.enemies.filter(enemy => enemy.hp > 0).length)
const totalEnemies = computed(() => plan.value.wave.groups.reduce((sum, group) => sum + group.count, 0))
const deploymentCount = computed(() => plan.value.deployments.reduce((sum, item) => sum + item.count, 0))

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
  if (!previous || paused.value) return current
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
  gradient.addColorStop(0, '#17281f')
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
  const castle = nodes.get(field.castleNodeId)
  if (castle) {
    const position = canvasPoint(castle)
    context.fillStyle = '#c9b279'
    context.fillRect(position.x - 30, position.y - 36, 60, 58)
    context.fillStyle = '#7d6a40'
    context.fillRect(position.x - 36, position.y - 45, 18, 18)
    context.fillRect(position.x + 18, position.y - 45, 18, 18)
  }
}

function finishFromReplay() {
  if (emitted.value || !simulation.value.terminalReason) return
  emitted.value = true
  paused.value = true
  emit('resolved', replayTdBattle(plan.value, props.session.seed, commandLog.value))
}

function runFrame(timestamp: number) {
  if (previousFrame === 0) previousFrame = timestamp
  accumulator += Math.min(250, timestamp - previousFrame)
  previousFrame = timestamp
  if (paused.value) accumulator = 0
  if (!paused.value && !simulation.value.terminalReason && accumulator >= plan.value.tickMs) {
    previousEntityPositions.clear()
    for (const squad of simulation.value.squads) {
      previousEntityPositions.set(`squad:${squad.deploymentId}`, { x: squad.x, y: squad.y })
    }
    for (const enemy of simulation.value.enemies) {
      previousEntityPositions.set(`enemy:${enemy.id}`, { x: enemy.x, y: enemy.y })
    }
    while (accumulator >= plan.value.tickMs && !simulation.value.terminalReason) {
      accumulator -= plan.value.tickMs
      for (let index = 0; index < speed.value && !simulation.value.terminalReason; index += 1) {
        const commands = pendingCommands.value.filter(command => command.tick === simulation.value.tick)
        if (commands.length > 0) {
          pendingCommands.value = pendingCommands.value.filter(command => command.tick !== simulation.value.tick)
        }
        stepTdSimulation(plan.value, simulation.value, commands)
      }
    }
    simulation.value = { ...simulation.value }
    finishFromReplay()
  }
  render()
  frameId = window.requestAnimationFrame(runFrame)
}

function queueChoice(choiceId: string) {
  if (paused.value || simulation.value.terminalReason || pendingCommands.value.length > 0) return
  const command: TdCommand = {
    tick: simulation.value.tick,
    kind: selectedTower.value ? 'upgrade-tower' : 'build-tower',
    spotId: selectedSpotId.value,
    choiceId,
  }
  commandLog.value.push(command)
  pendingCommands.value.push(command)
}

function fastResolve() {
  if (!props.qaMode || emitted.value) return
  emitted.value = true
  paused.value = true
  emit('resolved', resolveTdWithPolicy(plan.value, props.session.seed, qaPolicy.value))
}

onMounted(() => {
  render()
  frameId = window.requestAnimationFrame(runFrame)
})

onUnmounted(() => window.cancelAnimationFrame(frameId))
</script>

<template>
  <section class="td-battle" data-testid="td-battle">
    <header class="td-hud" data-testid="td-hud">
      <div>
        <span>Оборона</span>
        <strong>{{ plan.battlefield.name }}</strong>
      </div>
      <dl>
        <div><dt>Замок</dt><dd>{{ Math.ceil(simulation.castleHp) }} / {{ plan.battlefield.castleMaxHp }}</dd></div>
        <div><dt>Враги</dt><dd>{{ enemiesRemaining }} / {{ totalEnemies }}</dd></div>
        <div><dt>Ресурс</dt><dd>{{ simulation.buildResources }}</dd></div>
        <div><dt>Тик</dt><dd>{{ simulation.tick }}</dd></div>
      </dl>
      <div class="td-controls">
        <button type="button" data-testid="td-start" @click="paused = !paused">
          {{ paused ? (simulation.tick === 0 ? 'Начать оборону' : 'Продолжить') : 'Пауза' }}
        </button>
        <button
          v-for="multiplier in ([1, 2, 4] as const)"
          :key="multiplier"
          type="button"
          :class="{ active: speed === multiplier }"
          @click="speed = multiplier"
        >×{{ multiplier }}</button>
        <button type="button" class="danger" data-testid="td-abort" @click="emit('abort')">Отступить</button>
      </div>
    </header>

    <div v-if="simulation.tick === 0" class="deployment" data-testid="td-deployment">
      <strong>План развёртывания · {{ deploymentCount }} бойцов</strong>
      <span v-if="plan.deployments.length === 0">Армия не успела прибыть — оборону держат башни.</span>
      <span v-for="item in plan.deployments" :key="item.id">
        {{ item.unitId }} · {{ item.cityId }} · ×{{ item.count }}
      </span>
    </div>

    <div class="td-stage">
      <canvas
        ref="canvas"
        width="1000"
        height="600"
        aria-label="Поле центральной обороны"
      />
      <nav class="spot-picker" aria-label="Позиции башен">
        <button
          v-for="(spot, index) in plan.battlefield.buildSpots"
          :key="spot.id"
          type="button"
          :class="{ active: selectedSpotId === spot.id }"
          @click="selectedSpotId = spot.id"
        >Позиция {{ index + 1 }}</button>
      </nav>
    </div>

    <aside class="grade-drawer" data-testid="td-grade-drawer">
      <div>
        <span>Последовательный грейд</span>
        <strong>{{ nextGrade <= 4 ? `Грейд ${nextGrade}` : 'Башня завершена' }}</strong>
        <small>{{ paused ? 'Запустите бой, чтобы отдавать приказы.' : 'Выберите одно из четырёх улучшений.' }}</small>
      </div>
      <button
        v-for="choice in gradeChoices"
        :key="choice.id"
        type="button"
        :disabled="paused || pendingCommands.length > 0 || simulation.buildResources < choice.cost"
        @click="queueChoice(choice.id)"
      >
        <strong>{{ choice.name }}</strong>
        <span>{{ choice.cost }} ресурса</span>
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
.td-hud > div:first-child span,.grade-drawer span { color:#b18e4f; font:800 .58rem/1 monospace; letter-spacing:.12em; text-transform:uppercase; }
.td-hud > div:first-child strong { font:700 1.15rem/1.1 Georgia,serif; }
.td-hud dl { display:flex; gap:7px; margin:0; }
.td-hud dl div { min-width:72px; padding:7px 9px; border:1px solid rgba(235,217,178,.12); border-radius:6px; text-align:center; }
.td-hud dt { color:rgba(234,223,202,.5); font-size:.57rem; }
.td-hud dd { margin:4px 0 0; color:#e1c477; font:800 .78rem/1 monospace; }
.td-controls { display:flex; justify-content:flex-end; gap:5px; }
.td-controls button,.spot-picker button,.qa-fast-resolve button,.qa-fast-resolve select { padding:7px 9px; border:1px solid rgba(213,182,111,.26); border-radius:5px; color:#eadfca; background:#26342c; cursor:pointer; }
.td-controls button.active,.spot-picker button.active { border-color:#d5b767; color:#211b10; background:#d5b767; }
.td-controls .danger { border-color:rgba(188,91,83,.45); color:#e4a49d; }
.deployment { display:flex; align-items:center; gap:14px; padding:10px 14px; overflow:auto; border:1px dashed rgba(116,173,183,.3); border-radius:8px; color:#a9ced2; background:rgba(23,50,52,.52); font-size:.67rem; white-space:nowrap; }
.td-stage { position:relative; overflow:hidden; border:1px solid rgba(214,179,95,.24); border-radius:12px; background:#0b1715; box-shadow:0 20px 55px rgba(0,0,0,.25); }
.td-stage canvas { display:block; width:100%; height:auto; aspect-ratio:5/3; }
.spot-picker { position:absolute; right:10px; bottom:10px; display:flex; gap:5px; padding:6px; border-radius:7px; background:rgba(8,16,14,.83); }
.grade-drawer { display:grid; grid-template-columns:180px repeat(4,minmax(130px,1fr)); gap:8px; }
.grade-drawer > div,.grade-drawer > button { min-height:82px; padding:12px; border:1px solid rgba(214,179,95,.2); border-radius:8px; color:#eadfca; background:rgba(24,35,30,.9); text-align:left; }
.grade-drawer > div { display:grid; align-content:center; gap:5px; }
.grade-drawer small { color:rgba(234,223,202,.45); }
.grade-drawer > button { display:grid; align-content:center; gap:6px; cursor:pointer; }
.grade-drawer > button span { color:#c7aa67; font-size:.62rem; }
.grade-drawer > button:disabled { cursor:not-allowed; opacity:.42; }
.qa-fast-resolve { display:flex; justify-content:flex-end; align-items:center; gap:8px; padding:9px; border:1px solid rgba(105,174,178,.3); border-radius:8px; background:rgba(16,35,37,.8); }
.qa-fast-resolve label { display:flex; align-items:center; gap:8px; color:#9acacc; font-size:.65rem; }
@media (max-width: 900px) { .td-hud { grid-template-columns:1fr; }.td-controls { justify-content:flex-start; flex-wrap:wrap; }.td-hud dl { overflow:auto; }.grade-drawer { grid-template-columns:1fr 1fr; }.grade-drawer > div { grid-column:1/-1; }.spot-picker { position:static; overflow:auto; border-radius:0; }.td-stage canvas { min-width:720px; }.td-stage { overflow:auto; } }
</style>
