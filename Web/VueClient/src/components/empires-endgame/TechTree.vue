<script setup lang="ts">
import { computed, ref } from 'vue'
import {
  Check,
  FlaskConical,
  Hammer,
  Link2,
  LockKeyhole,
  Move,
  ScrollText,
  Shield,
  Sparkles,
  Store,
  Unlink,
} from 'lucide-vue-next'

interface TechnologyNodeView {
  id: string
  name: string
  description: string
  branch: string
  tier?: number
  x: number
  y: number
  requires: string[]
  costKnowledge: number
  costGold: number
  costs?: string[]
  timeCost: number
  researched?: boolean
  available?: boolean
  darkSide?: string
  blockedReason?: string
  image?: string
  deferredReason?: string
}

const props = withDefaults(defineProps<{
  nodes: TechnologyNodeView[]
  editable?: boolean
  selectedId?: string | null
  knowledge?: number
  gold?: number
  days?: number
}>(), {
  editable: false,
  selectedId: null,
  knowledge: 0,
  gold: 0,
  days: 0,
})

const emit = defineEmits<{
  select: [nodeId: string]
  research: [nodeId: string]
  moveNode: [nodeId: string, x: number, y: number]
  toggleDependency: [fromId: string, toId: string]
}>()

const linking = ref(false)
const linkSourceId = ref<string | null>(null)
const viewport = ref<HTMLElement | null>(null)
const isPanning = ref(false)

const NODE_WIDTH = 148
const NODE_HEIGHT = 58
const WORLD_GUTTER = 36
const MIN_CANVAS_WIDTH = 840
const MIN_CANVAS_HEIGHT = 600

interface PanState {
  pointerId: number
  clientX: number
  clientY: number
  scrollLeft: number
  scrollTop: number
}

const panState = ref<PanState | null>(null)
const dragOffset = ref({ x: 0, y: 0 })

const nodeById = computed(() => new Map(props.nodes.map(node => [node.id, node])))
const selected = computed(() => props.nodes.find(node => node.id === props.selectedId) ?? null)
const branches = computed(() => Array.from(new Set(props.nodes.map(node => node.branch))))
const edges = computed(() => props.nodes.flatMap(node => node.requires
  .map(requiredId => ({ from: nodeById.value.get(requiredId), to: node }))
  .filter((edge): edge is { from: TechnologyNodeView, to: TechnologyNodeView } => Boolean(edge.from))))
const canvasMetrics = computed(() => {
  if (!props.nodes.length) {
    return {
      width: MIN_CANVAS_WIDTH,
      height: MIN_CANVAS_HEIGHT,
      originX: WORLD_GUTTER + NODE_WIDTH / 2,
      originY: WORLD_GUTTER + NODE_HEIGHT / 2,
      minX: 0,
      maxX: 0,
      minY: 0,
      maxY: 0,
    }
  }

  const xs = props.nodes.map(node => node.x)
  const ys = props.nodes.map(node => node.y)
  const minX = Math.min(...xs)
  const maxX = Math.max(...xs)
  const minY = Math.min(...ys)
  const maxY = Math.max(...ys)
  const originX = WORLD_GUTTER + NODE_WIDTH / 2 - minX
  const originY = WORLD_GUTTER + NODE_HEIGHT / 2 - minY
  const editorWorkspace = props.editable ? 180 : 72

  return {
    width: Math.max(MIN_CANVAS_WIDTH, maxX + originX + NODE_WIDTH / 2 + WORLD_GUTTER + editorWorkspace),
    height: Math.max(MIN_CANVAS_HEIGHT, maxY + originY + NODE_HEIGHT / 2 + WORLD_GUTTER + editorWorkspace),
    originX,
    originY,
    minX,
    maxX,
    minY,
    maxY,
  }
})
const tierMarkers = computed(() => {
  const markerByTier = new Map<number, number>()
  for (const node of props.nodes) {
    if (node.tier === undefined) continue
    const current = markerByTier.get(node.tier)
    if (current === undefined || node.x < current) markerByTier.set(node.tier, node.x)
  }
  return Array.from(markerByTier, ([tier, x]) => ({ tier, x })).sort((a, b) => a.x - b.x)
})
const viewportLabel = computed(() => props.editable
  ? 'Конструктор древа технологий. Перетаскивайте ноды, чтобы изменять их мировые координаты.'
  : 'Древо технологий. Прокручивайте или перетаскивайте фон, чтобы смотреть всё древо.')

const branchColors: Record<string, string> = {
  general: '#c6a86b',
  science: '#69aeb2',
  trade: '#d09c55',
  war: '#bb665c',
  reform: '#9878b8',
  steel: '#91a2aa',
}

function colorFor(branch: string) {
  return branchColors[branch] ?? '#9ca676'
}

function iconFor(branch: string) {
  if (branch === 'science') return FlaskConical
  if (branch === 'trade') return Store
  if (branch === 'war') return Shield
  if (branch === 'steel') return Hammer
  if (branch === 'reform') return ScrollText
  return Sparkles
}

function chooseNode(node: TechnologyNodeView) {
  if (linking.value) {
    if (!linkSourceId.value) {
      linkSourceId.value = node.id
      return
    }
    if (linkSourceId.value !== node.id) emit('toggleDependency', linkSourceId.value, node.id)
    linkSourceId.value = null
    return
  }
  emit('select', node.id)
}

function beginDrag(event: DragEvent, nodeId: string) {
  if (!props.editable || !event.dataTransfer) return
  event.dataTransfer.effectAllowed = 'move'
  event.dataTransfer.setData('application/x-ee-tech-node', nodeId)
  const bounds = (event.currentTarget as HTMLElement).getBoundingClientRect()
  dragOffset.value = {
    x: event.clientX - (bounds.left + bounds.width / 2),
    y: event.clientY - (bounds.top + bounds.height / 2),
  }
}

function dropNode(event: DragEvent) {
  if (!props.editable) return
  const nodeId = event.dataTransfer?.getData('application/x-ee-tech-node')
  const target = viewport.value
  if (!nodeId || !target) return
  const bounds = target.getBoundingClientRect()
  const worldX = event.clientX - bounds.left - target.clientLeft + target.scrollLeft - canvasMetrics.value.originX - dragOffset.value.x
  const worldY = event.clientY - bounds.top - target.clientTop + target.scrollTop - canvasMetrics.value.originY - dragOffset.value.y
  emit(
    'moveNode',
    nodeId,
    Math.max(0, Math.round(worldX)),
    Math.max(0, Math.round(worldY)),
  )
  dragOffset.value = { x: 0, y: 0 }
}

function beginPan(event: PointerEvent) {
  const target = event.target as HTMLElement
  const scroller = viewport.value
  if (!scroller || event.pointerType !== 'mouse' || (event.button !== 0 && event.button !== 1) || target.closest('.tech-node')) return
  const bounds = scroller.getBoundingClientRect()
  if (event.clientX - bounds.left > scroller.clientWidth || event.clientY - bounds.top > scroller.clientHeight) return
  panState.value = {
    pointerId: event.pointerId,
    clientX: event.clientX,
    clientY: event.clientY,
    scrollLeft: scroller.scrollLeft,
    scrollTop: scroller.scrollTop,
  }
  isPanning.value = true
  scroller.setPointerCapture(event.pointerId)
  event.preventDefault()
}

function panCanvas(event: PointerEvent) {
  const scroller = viewport.value
  const start = panState.value
  if (!scroller || !start || event.pointerId !== start.pointerId) return
  scroller.scrollLeft = start.scrollLeft - (event.clientX - start.clientX)
  scroller.scrollTop = start.scrollTop - (event.clientY - start.clientY)
}

function endPan(event: PointerEvent) {
  const scroller = viewport.value
  const start = panState.value
  if (!start || event.pointerId !== start.pointerId) return
  if (scroller?.hasPointerCapture(event.pointerId)) scroller.releasePointerCapture(event.pointerId)
  panState.value = null
  isPanning.value = false
}

function panWithKeyboard(event: KeyboardEvent) {
  const scroller = viewport.value
  if (!scroller || event.target !== event.currentTarget) return
  const step = event.shiftKey ? 180 : 64
  if (event.key === 'ArrowLeft') scroller.scrollBy({ left: -step, behavior: 'smooth' })
  else if (event.key === 'ArrowRight') scroller.scrollBy({ left: step, behavior: 'smooth' })
  else if (event.key === 'ArrowUp') scroller.scrollBy({ top: -step, behavior: 'smooth' })
  else if (event.key === 'ArrowDown') scroller.scrollBy({ top: step, behavior: 'smooth' })
  else if (event.key === 'PageUp') scroller.scrollBy({ top: -scroller.clientHeight * .85, behavior: 'smooth' })
  else if (event.key === 'PageDown') scroller.scrollBy({ top: scroller.clientHeight * .85, behavior: 'smooth' })
  else if (event.key === 'Home') scroller.scrollTo({ left: 0, top: 0, behavior: 'smooth' })
  else if (event.key === 'End') scroller.scrollTo({ top: scroller.scrollHeight, behavior: 'smooth' })
  else return
  event.preventDefault()
}

function toggleLinking() {
  linking.value = !linking.value
  linkSourceId.value = null
}

function hideBrokenImage(event: Event) {
  ;(event.currentTarget as HTMLImageElement).hidden = true
}
</script>

<template>
  <section class="tech-tree" :class="{ editable, linking }">
    <header class="tech-header">
      <div>
        <span>{{ editable ? 'Конструктор связей' : 'Имперское развитие' }}</span>
        <h2>Доктрины и технологии</h2>
        <small class="mode-label" :class="{ editor: editable }">
          {{ editable ? 'Режим редактора · ноды можно двигать' : 'Режим игры · ноды только выбираются' }}
        </small>
      </div>
      <div class="tech-legend">
        <span v-for="branch in branches" :key="branch">
          <i :style="{ background: colorFor(branch) }" />{{ branch }}
        </span>
      </div>
      <button v-if="editable" type="button" :class="{ active: linking }" :aria-pressed="linking" @click="toggleLinking">
        <component :is="linking ? Unlink : Link2" :size="15" />
        {{ linking ? 'Закончить связи' : 'Связать ноды' }}
      </button>
    </header>

    <div class="tech-layout">
      <div class="canvas-frame">
        <div class="canvas-mode-hint" :class="{ editor: editable }" aria-hidden="true">
          <Move :size="13" />
          <span>{{ editable ? 'Тяните ноду — меняются x/y' : 'Тяните фон или используйте прокрутку' }}</span>
        </div>
        <div
          ref="viewport"
          class="tech-viewport"
          data-testid="technology-viewport"
          :class="{ 'is-panning': isPanning }"
          role="region"
          tabindex="0"
          :aria-label="viewportLabel"
          @dragover.prevent
          @drop.prevent="dropNode"
          @pointerdown="beginPan"
          @pointermove="panCanvas"
          @pointerup="endPan"
          @pointercancel="endPan"
          @keydown="panWithKeyboard"
        >
          <div
            class="tech-canvas"
            :style="{ width: `${canvasMetrics.width}px`, height: `${canvasMetrics.height}px` }"
          >
            <div class="knowledge-bands" aria-hidden="true">
              <span
                v-for="marker in tierMarkers"
                :key="marker.tier"
                :style="{ left: `${marker.x + canvasMetrics.originX}px` }"
              >{{ marker.tier }} ур. знаний</span>
            </div>
            <svg
              class="tech-edges"
              :viewBox="`0 0 ${canvasMetrics.width} ${canvasMetrics.height}`"
              preserveAspectRatio="none"
              aria-hidden="true"
            >
              <line
                v-for="edge in edges"
                :key="`${edge.from.id}-${edge.to.id}`"
                :x1="edge.from.x + canvasMetrics.originX"
                :y1="edge.from.y + canvasMetrics.originY"
                :x2="edge.to.x + canvasMetrics.originX"
                :y2="edge.to.y + canvasMetrics.originY"
                :class="{ researched: edge.from.researched && edge.to.researched }"
              />
            </svg>

            <button
              v-for="node in nodes"
              :key="node.id"
              class="tech-node"
              :data-testid="`technology-node-${node.id}`"
              :class="{
                researched: node.researched,
                available: node.available,
                selected: node.id === selectedId,
                deferred: Boolean(node.deferredReason),
                'link-source': node.id === linkSourceId,
              }"
              :style="{
                left: `${node.x + canvasMetrics.originX}px`,
                top: `${node.y + canvasMetrics.originY}px`,
                '--node-accent': colorFor(node.branch),
              }"
              :draggable="editable"
              :aria-pressed="node.id === selectedId"
              :aria-label="`${node.name}. ${node.deferredReason ? 'Будущая механика' : node.researched ? 'Изучено' : node.available ? 'Доступно' : 'Заблокировано'}. Координаты ${node.x}, ${node.y}`"
              type="button"
              @dragstart="beginDrag($event, node.id)"
              @dragend="dragOffset = { x: 0, y: 0 }"
              @click="chooseNode(node)"
            >
              <span class="node-icon">
                <img v-if="node.image" :src="node.image" alt="" @error="hideBrokenImage" />
                <Check v-else-if="node.researched" :size="17" />
                <component :is="iconFor(node.branch)" v-else-if="node.available || editable" :size="17" />
                <LockKeyhole v-else :size="15" />
              </span>
              <strong>{{ node.name }}</strong>
              <small>{{ node.deferredReason ? 'будущая механика' : `${node.timeCost}д · ${node.costKnowledge} зн.` }}</small>
              <Move v-if="editable" class="move-mark" :size="12" />
            </button>

            <div v-if="!nodes.length" class="tech-empty">
              <FlaskConical :size="30" />
              <strong>Древо пока пусто</strong>
              <span>Добавьте первую технологию в конструкторе.</span>
            </div>
          </div>
        </div>
      </div>

      <aside class="tech-detail">
        <template v-if="selected">
          <img v-if="selected.image" class="detail-image" :src="selected.image" alt="" @error="hideBrokenImage" />
          <span class="detail-branch" :style="{ color: colorFor(selected.branch) }">{{ selected.branch }}</span>
          <h3>{{ selected.name }}</h3>
          <p>{{ selected.description }}</p>
          <div v-if="selected.deferredReason" class="deferred-reason" role="status">
            <LockKeyhole :size="14" />
            <span><strong>Будущая механика.</strong> {{ selected.deferredReason }}</span>
          </div>
          <dl>
            <div><dt>Время</dt><dd>{{ selected.timeCost }} дней</dd></div>
            <div><dt>Знания</dt><dd>{{ selected.costKnowledge }}</dd></div>
            <div><dt>Золото</dt><dd>{{ selected.costGold }}</dd></div>
          </dl>
          <div v-if="selected.costs?.length" class="configured-costs">
            <span>Полная цена</span>
            <b v-for="cost in selected.costs" :key="cost">{{ cost }}</b>
          </div>
          <div v-if="selected.requires.length" class="requires">
            <span>Требует</span>
            <b v-for="id in selected.requires" :key="id">{{ nodeById.get(id)?.name || id }}</b>
          </div>
          <div v-if="selected.darkSide" class="dark-side">
            <strong>Тёмная сторона</strong>
            <span>{{ selected.darkSide }}</span>
          </div>
          <div v-if="!editable && !selected.researched && !selected.deferredReason && selected.blockedReason" class="blocked-reason" role="status">
            <LockKeyhole :size="14" />
            <span>{{ selected.blockedReason }}</span>
          </div>
          <button
            v-if="!editable && !selected.researched"
            class="research-button"
            type="button"
            :disabled="!selected.available || knowledge < selected.costKnowledge || gold < selected.costGold || days < selected.timeCost"
            @click="emit('research', selected.id)"
          >
            <FlaskConical :size="16" /> {{ selected.deferredReason ? 'Будущая механика' : 'Изучить' }}
          </button>
          <div v-else-if="selected.researched" class="researched-label"><Check :size="15" /> Изучено</div>
        </template>
        <template v-else>
          <ScrollText :size="30" />
          <h3>Выберите ноду</h3>
          <p>{{ editable ? 'Перетаскивайте ноды и связывайте зависимости прямо на полотне.' : 'Посмотрите цену, последствия и место технологии в развитии империи.' }}</p>
        </template>
      </aside>
    </div>
  </section>
</template>

<style scoped>
.tech-tree { overflow: hidden; border: 1px solid rgba(220, 196, 145, 0.18); border-radius: 16px; color: #eee4cf; background: #141712; }
.node-icon img { width: 100%; height: 100%; border-radius: inherit; object-fit: cover; }
.detail-image { width: 100%; max-height: 150px; border: 1px solid rgba(222, 197, 143, .18); border-radius: 9px; object-fit: cover; }
.tech-header { display: flex; min-height: 76px; align-items: center; gap: 18px; padding: 14px 18px; border-bottom: 1px solid rgba(220,196,145,0.13); background: linear-gradient(100deg, #211e17, #18201b); }
.tech-header > div:first-child { margin-right: auto; }
.tech-header > div:first-child > span { color: #c6a86b; font: 800 0.62rem/1 var(--font-mono, monospace); letter-spacing: .12em; text-transform: uppercase; }
.tech-header h2 { margin: 4px 0 0; font: 700 1.35rem/1 Georgia, serif; }
.mode-label { display: inline-flex; margin-top: 7px; padding: 4px 7px; border: 1px solid rgba(142, 170, 143, .2); border-radius: 4px; color: #9eb09e; background: rgba(95, 126, 99, .08); font: 700 .55rem/1 var(--font-mono, monospace); letter-spacing: .03em; }
.mode-label.editor { border-color: rgba(223, 180, 83, .3); color: #e0bd6d; background: rgba(209, 173, 98, .1); }
.tech-header > button { display: inline-flex; align-items: center; gap: 6px; padding: 8px 10px; border: 1px solid rgba(221,200,157,.22); border-radius: 7px; color: #dfd3ba; background: rgba(255,255,255,.04); cursor: pointer; font-size: .68rem; }
.tech-header > button.active { border-color: #d1ad62; color: #ffe8ad; background: rgba(209,173,98,.13); }
.tech-legend { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 5px 10px; }
.tech-legend span { display: inline-flex; align-items: center; gap: 4px; color: rgba(238,228,207,.6); font: 700 .58rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.tech-legend i { width: 7px; height: 7px; border-radius: 50%; }

.tech-layout { display: grid; min-height: 0; grid-template-columns: minmax(0, 1fr) 270px; }
.canvas-frame { position: relative; min-width: 0; height: clamp(600px, 68vh, 720px); overflow: hidden; background: #10130f; }
.canvas-mode-hint { position: absolute; z-index: 8; top: 12px; right: 22px; display: inline-flex; align-items: center; gap: 6px; max-width: calc(100% - 44px); padding: 7px 9px; border: 1px solid rgba(148, 174, 151, .2); border-radius: 6px; color: #a7b6a7; background: rgba(16, 22, 17, .9); box-shadow: 0 5px 18px rgba(0, 0, 0, .28); font: 700 .58rem/1.2 var(--font-mono, monospace); pointer-events: none; }
.canvas-mode-hint.editor { border-color: rgba(216, 178, 91, .35); color: #e1bf71; background: rgba(42, 35, 21, .94); }
.tech-viewport { position: absolute; inset: 0; overflow: auto; overscroll-behavior: contain; scrollbar-color: rgba(198, 168, 107, .55) rgba(255, 255, 255, .035); scrollbar-width: auto; cursor: grab; touch-action: pan-x pan-y; }
.tech-viewport.is-panning { cursor: grabbing; user-select: none; }
.tech-viewport:focus-visible { outline: 2px solid #d1ad62; outline-offset: -3px; }
.tech-canvas { position: relative; overflow: hidden; background: radial-gradient(circle at center, rgba(198,168,107,.06), transparent 34%), linear-gradient(90deg, rgba(255,255,255,.018) 1px, transparent 1px), linear-gradient(rgba(255,255,255,.018) 1px, transparent 1px), #10130f; background-size: auto, 32px 32px, 32px 32px; }
.knowledge-bands { position: absolute; inset: 0; pointer-events: none; }
.knowledge-bands span { position: absolute; top: 0; bottom: 0; width: 160px; padding: 11px 8px; border-left: 1px dashed rgba(220,196,145,.12); color: rgba(238,228,207,.2); font: 800 .52rem/1 var(--font-mono, monospace); text-transform: uppercase; transform: translateX(-50%); }
.tech-edges { position: absolute; inset: 0; width: 100%; height: 100%; overflow: visible; pointer-events: none; }
.tech-edges line { stroke: rgba(206,190,156,.3); stroke-width: 1.4; vector-effect: non-scaling-stroke; }
.tech-edges line.researched { stroke: #b99b60; stroke-width: 2; filter: drop-shadow(0 0 2px rgba(218,184,112,.45)); }
.tech-node { --node-accent: #c6a86b; position: absolute; z-index: 1; display: grid; width: 148px; height: 58px; grid-template-columns: 30px minmax(0, 1fr); grid-template-rows: 1fr auto; align-items: center; gap: 1px 6px; padding: 6px 17px 6px 7px; border: 1px solid color-mix(in srgb, var(--node-accent) 40%, #413a2b); border-radius: 9px; color: rgba(238,228,207,.55); background: linear-gradient(150deg, rgba(35,37,29,.97), rgba(21,23,18,.98)); box-shadow: 0 7px 20px rgba(0,0,0,.24); cursor: pointer; text-align: left; transform: translate(-50%, -50%); transition: 140ms ease; }
.tech-node:hover,.tech-node.selected { z-index: 3; border-color: var(--node-accent); color: #f4ead5; transform: translate(-50%, -50%) scale(1.04); }
.tech-node.available { color: #eee4cf; box-shadow: 0 0 0 1px color-mix(in srgb, var(--node-accent) 34%, transparent), 0 8px 22px rgba(0,0,0,.3); }
.tech-node.researched { border-color: var(--node-accent); color: #fff1cf; background: linear-gradient(150deg, color-mix(in srgb, var(--node-accent) 20%, #24251d), #171a15); }
.tech-node.deferred { border-style: dashed; color: rgba(229, 191, 139, .68); filter: saturate(.55); }
.tech-node.deferred small { color: #d9aa73; opacity: 1; text-transform: uppercase; }
.tech-node.link-source { outline: 2px solid #f1cd74; outline-offset: 3px; }
.editable .tech-node { cursor: grab; }
.editable .tech-node:active { cursor: grabbing; }
.tech-node:focus-visible { z-index: 4; outline: 2px solid #f1cd74; outline-offset: 2px; }
.node-icon { display: grid; width: 28px; height: 28px; grid-row: 1 / 3; place-items: center; border-radius: 50%; color: var(--node-accent); background: color-mix(in srgb, var(--node-accent) 13%, transparent); }
.tech-node strong { display: -webkit-box; align-self: end; overflow: hidden; font: 700 .67rem/1.12 Georgia, serif; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
.tech-node small { color: currentColor; font: 600 .52rem/1 var(--font-mono, monospace); opacity: .58; }
.move-mark { position: absolute; top: 5px; right: 5px; opacity: .28; }
.tech-empty { position: absolute; inset: 0; display: grid; place-content: center; place-items: center; gap: 5px; color: rgba(238,228,207,.55); text-align: center; }
.tech-empty span { font-size: .72rem; }

.tech-detail { display: flex; min-width: 0; min-height: 0; flex-direction: column; align-items: flex-start; overflow-y: auto; padding: 20px; border-left: 1px solid rgba(220,196,145,.13); background: #181a15; }
.detail-branch { font: 900 .62rem/1 var(--font-mono, monospace); letter-spacing: .12em; text-transform: uppercase; }
.tech-detail h3 { margin: 8px 0; font: 700 1.25rem/1.15 Georgia, serif; }
.tech-detail p { margin: 0 0 16px; color: rgba(238,228,207,.62); font-size: .75rem; line-height: 1.55; }
.tech-detail dl { display: grid; width: 100%; grid-template-columns: repeat(3, 1fr); gap: 5px; margin: 0 0 14px; }
.tech-detail dl div { padding: 8px 5px; border-radius: 6px; background: rgba(255,255,255,.035); text-align: center; }
.tech-detail dt { color: rgba(238,228,207,.42); font: 700 .52rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.tech-detail dd { margin: 4px 0 0; font: 800 .64rem/1 var(--font-mono, monospace); }
.configured-costs,.requires { display: flex; width: 100%; flex-wrap: wrap; gap: 4px; margin-bottom: 12px; }
.configured-costs > span,.requires > span { width: 100%; color: rgba(238,228,207,.4); font: 700 .53rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.configured-costs b,.requires b { padding: 4px 6px; border-radius: 4px; color: #d8c79f; background: rgba(198,168,107,.08); font-size: .57rem; }
.dark-side { display: grid; gap: 4px; padding: 10px; border: 1px solid rgba(171,93,104,.24); border-radius: 7px; color: #d69da5; background: rgba(116,49,59,.09); font-size: .65rem; line-height: 1.4; }
.deferred-reason { display: flex; width: 100%; align-items: flex-start; gap: 7px; margin-bottom: 12px; padding: 9px; border: 1px solid rgba(190, 132, 78, .3); border-radius: 7px; color: #e0b984; background: rgba(126, 75, 34, .11); font-size: .64rem; line-height: 1.4; }
.deferred-reason svg { flex: none; margin-top: 1px; }
.deferred-reason strong { color: #f0cca0; }
.blocked-reason { display: flex; width: 100%; align-items: flex-start; gap: 7px; margin-top: 10px; padding: 9px; border: 1px solid rgba(187, 102, 92, .26); border-radius: 7px; color: #d8a39c; background: rgba(117, 52, 45, .1); font-size: .64rem; line-height: 1.4; }
.blocked-reason svg { flex: none; margin-top: 1px; }
.research-button { display: inline-flex; width: 100%; min-height: 38px; align-items: center; justify-content: center; gap: 7px; margin-top: auto; border: 1px solid #c6a86b; border-radius: 7px; color: #241d11; background: #c6a86b; cursor: pointer; font-weight: 900; }
.research-button:disabled { border-color: rgba(198,168,107,.2); color: rgba(238,228,207,.3); background: rgba(255,255,255,.025); cursor: not-allowed; }
.researched-label { display: inline-flex; align-items: center; gap: 5px; margin-top: auto; color: #a7c17f; font: 800 .7rem/1 var(--font-mono, monospace); }

@media (max-width: 900px) {
  .tech-header { align-items: flex-start; flex-wrap: wrap; }
  .tech-legend { order: 3; width: 100%; justify-content: flex-start; }
  .tech-layout { grid-template-columns: 1fr; }
  .canvas-frame { height: clamp(440px, 64vh, 560px); min-height: 0; }
  .canvas-mode-hint { right: 18px; max-width: calc(100% - 36px); }
  .tech-detail { min-height: 210px; border-top: 1px solid rgba(220,196,145,.13); border-left: 0; }
}
</style>
