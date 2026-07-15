<script lang="ts">
export interface BuildingDependencyNodeView {
  id: string
  buildingId: string
  buildingName: string
  level: number
  slot: string
  x: number
  y: number
  dependencies: string[]
  image?: string
  description?: string
  timeCostDays: number
  foodCost: number
  workerDemand: number
}

export interface BuildingNodePosition {
  x: number
  y: number
}

export type BuildingDependencyNodePatch = Partial<Pick<BuildingDependencyNodeView,
  | 'buildingName'
  | 'description'
  | 'image'
  | 'timeCostDays'
  | 'foodCost'
  | 'workerDemand'
>>
</script>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, useId } from 'vue'
import {
  Building2,
  Clock3,
  Image as ImageIcon,
  Link2,
  Move,
  Network,
  Plus,
  Trash2,
  Unlink,
  Users,
  Wheat,
} from 'lucide-vue-next'

const props = withDefaults(defineProps<{
  nodes: BuildingDependencyNodeView[]
  editable?: boolean
  selectedId?: string | null
}>(), {
  editable: false,
  selectedId: null,
})

const emit = defineEmits<{
  select: [nodeId: string]
  moveNode: [nodeId: string, x: number, y: number]
  toggleDependency: [fromId: string, toId: string]
  addNode: [position?: BuildingNodePosition]
  deleteNode: [nodeId: string]
  updateNode: [nodeId: string, patch: BuildingDependencyNodePatch]
}>()

interface DragState {
  nodeId: string
  startClientX: number
  startClientY: number
  startX: number
  startY: number
  bounds: DOMRect
  moved: boolean
}

const canvas = ref<HTMLElement | null>(null)
const localSelectedId = ref<string | null>(props.selectedId)
const linking = ref(false)
const linkSourceId = ref<string | null>(null)
const dragState = ref<DragState | null>(null)
const dragPreview = ref<{ nodeId: string, x: number, y: number } | null>(null)
const suppressClickId = ref<string | null>(null)
const markerId = `building-arrow-${useId().replace(/:/g, '')}`
let clickReleaseTimer: ReturnType<typeof setTimeout> | undefined

const nodeById = computed(() => new Map(props.nodes.map(node => [node.id, node])))
const effectiveSelectedId = computed(() => props.selectedId ?? localSelectedId.value)
const selected = computed(() => nodeById.value.get(effectiveSelectedId.value ?? '') ?? null)
const edges = computed(() => props.nodes.flatMap(node => node.dependencies
  .map(dependencyId => ({ from: nodeById.value.get(dependencyId), to: node }))
  .filter((edge): edge is { from: BuildingDependencyNodeView, to: BuildingDependencyNodeView } => Boolean(edge.from))))

const slotColors: Record<string, string> = {
  farm: '#94a568',
  lumber: '#a77a52',
  mine: '#7e96a0',
  smithy: '#bd7659',
  barracks: '#ad6262',
  unique: '#9a78b3',
  municipal: '#c8aa69',
}

function colorFor(slot: string) {
  return slotColors[slot] ?? '#9b956f'
}

function clamp(value: number, minimum: number, maximum: number) {
  return Math.min(maximum, Math.max(minimum, value))
}

function nodePosition(node: BuildingDependencyNodeView) {
  if (dragPreview.value?.nodeId === node.id) {
    return dragPreview.value
  }
  return { x: node.x, y: node.y }
}

function chooseNode(nodeId: string) {
  if (suppressClickId.value === nodeId) return

  localSelectedId.value = nodeId
  emit('select', nodeId)

  if (!props.editable || !linking.value) return
  if (!linkSourceId.value) {
    linkSourceId.value = nodeId
    return
  }
  if (linkSourceId.value === nodeId) {
    linkSourceId.value = null
    return
  }
  emit('toggleDependency', linkSourceId.value, nodeId)
  linkSourceId.value = null
}

function toggleLinking() {
  linking.value = !linking.value
  linkSourceId.value = null
}

function beginPointerDrag(event: PointerEvent, node: BuildingDependencyNodeView) {
  if (!props.editable || linking.value || event.button !== 0 || !canvas.value) return
  dragState.value = {
    nodeId: node.id,
    startClientX: event.clientX,
    startClientY: event.clientY,
    startX: node.x,
    startY: node.y,
    bounds: canvas.value.getBoundingClientRect(),
    moved: false,
  }
  window.addEventListener('pointermove', continuePointerDrag, { passive: false })
  window.addEventListener('pointerup', finishPointerDrag, { once: true })
  window.addEventListener('pointercancel', cancelPointerDrag, { once: true })
}

function continuePointerDrag(event: PointerEvent) {
  const drag = dragState.value
  if (!drag) return
  const dx = event.clientX - drag.startClientX
  const dy = event.clientY - drag.startClientY
  if (!drag.moved && Math.hypot(dx, dy) < 4) return
  drag.moved = true
  event.preventDefault()
  dragPreview.value = {
    nodeId: drag.nodeId,
    x: clamp(drag.startX + dx / Math.max(1, drag.bounds.width) * 100, 4, 96),
    y: clamp(drag.startY + dy / Math.max(1, drag.bounds.height) * 100, 7, 93),
  }
}

function removePointerListeners() {
  window.removeEventListener('pointermove', continuePointerDrag)
  window.removeEventListener('pointerup', finishPointerDrag)
  window.removeEventListener('pointercancel', cancelPointerDrag)
}

function finishPointerDrag() {
  const drag = dragState.value
  const preview = dragPreview.value
  if (drag?.moved && preview) {
    emit('moveNode', drag.nodeId, preview.x, preview.y)
    suppressClickId.value = drag.nodeId
    clearTimeout(clickReleaseTimer)
    clickReleaseTimer = setTimeout(() => {
      suppressClickId.value = null
    }, 0)
  }
  dragState.value = null
  dragPreview.value = null
  removePointerListeners()
}

function cancelPointerDrag() {
  dragState.value = null
  dragPreview.value = null
  removePointerListeners()
}

function moveWithKeyboard(event: KeyboardEvent, node: BuildingDependencyNodeView) {
  if (!props.editable || linking.value || !['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(event.key)) return
  event.preventDefault()
  const step = event.shiftKey ? 5 : 1
  const x = clamp(node.x + (event.key === 'ArrowLeft' ? -step : event.key === 'ArrowRight' ? step : 0), 4, 96)
  const y = clamp(node.y + (event.key === 'ArrowUp' ? -step : event.key === 'ArrowDown' ? step : 0), 7, 93)
  emit('moveNode', node.id, x, y)
}

function addAtCenter() {
  const offset = props.nodes.length % 5 * 3
  emit('addNode', { x: clamp(50 + offset, 10, 90), y: clamp(50 + offset, 10, 90) })
}

function addAtPointer(event: MouseEvent) {
  if (!props.editable || !canvas.value) return
  const bounds = canvas.value.getBoundingClientRect()
  emit('addNode', {
    x: clamp((event.clientX - bounds.left) / Math.max(1, bounds.width) * 100, 4, 96),
    y: clamp((event.clientY - bounds.top) / Math.max(1, bounds.height) * 100, 7, 93),
  })
}

function updateSelected(patch: BuildingDependencyNodePatch) {
  if (selected.value && props.editable) emit('updateNode', selected.value.id, patch)
}

function updateNumber(key: 'timeCostDays' | 'foodCost' | 'workerDemand', event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  updateSelected({ [key]: Number.isFinite(value) ? Math.max(0, value) : 0 })
}

function unlinkDependency(dependencyId: string) {
  if (selected.value) emit('toggleDependency', dependencyId, selected.value.id)
}

function hideBrokenImage(event: Event) {
  ;(event.currentTarget as HTMLImageElement).hidden = true
}

onBeforeUnmount(() => {
  clearTimeout(clickReleaseTimer)
  removePointerListeners()
})
</script>

<template>
  <section class="building-editor" :class="{ editable, linking }" aria-labelledby="building-editor-title">
    <header class="editor-header">
      <div class="heading">
        <span>Имперский архитектор</span>
        <h2 id="building-editor-title"><Building2 :size="21" /> Чертёж построек</h2>
      </div>

      <div v-if="editable" class="editor-actions">
        <button type="button" @click="addAtCenter">
          <Plus :size="15" /> Добавить уровень
        </button>
        <button type="button" :class="{ active: linking }" :aria-pressed="linking" @click="toggleLinking">
          <Unlink v-if="linking" :size="15" />
          <Link2 v-else :size="15" />
          {{ linking ? 'Завершить связи' : 'Связать уровни' }}
        </button>
      </div>
    </header>

    <p v-if="editable" class="editor-hint" aria-live="polite">
      <template v-if="linking && linkSourceId">
        Источник «{{ nodeById.get(linkSourceId)?.buildingName || linkSourceId }}» выбран. Укажите зависимый уровень.
      </template>
      <template v-else-if="linking">
        Выберите требуемый уровень, затем уровень, который должен от него зависеть. Повторный выбор связи удалит её.
      </template>
      <template v-else>
        Перетаскивайте узлы мышью или стрелками клавиатуры. Двойной щелчок по полотну добавляет уровень.
      </template>
    </p>

    <div class="editor-layout">
      <div
        ref="canvas"
        class="blueprint-canvas"
        role="group"
        aria-label="Схема уровней построек и их зависимостей"
        @dblclick.self="addAtPointer"
      >
        <div class="canvas-label" aria-hidden="true"><Network :size="14" /> Архитектурная сеть</div>

        <svg class="dependency-lines" viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true">
          <defs>
            <marker :id="markerId" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto" markerUnits="strokeWidth">
              <path d="M0,0 L7,3.5 L0,7 Z" />
            </marker>
          </defs>
          <line
            v-for="edge in edges"
            :key="`${edge.from.id}-${edge.to.id}`"
            :x1="nodePosition(edge.from).x"
            :y1="nodePosition(edge.from).y"
            :x2="nodePosition(edge.to).x"
            :y2="nodePosition(edge.to).y"
            :marker-end="`url(#${markerId})`"
          />
        </svg>

        <button
          v-for="node in nodes"
          :key="node.id"
          class="building-node"
          :class="{
            selected: node.id === effectiveSelectedId,
            'link-source': node.id === linkSourceId,
            dragging: node.id === dragPreview?.nodeId,
          }"
          :style="{
            left: `${nodePosition(node).x}%`,
            top: `${nodePosition(node).y}%`,
            '--node-accent': colorFor(node.slot),
          }"
          type="button"
          :aria-label="`${node.buildingName}, уровень ${node.level}, слот ${node.slot}`"
          :aria-pressed="node.id === effectiveSelectedId"
          @pointerdown="beginPointerDrag($event, node)"
          @keydown="moveWithKeyboard($event, node)"
          @click="chooseNode(node.id)"
        >
          <span class="node-art">
            <img v-if="node.image" :key="node.image" :src="node.image" alt="" @error="hideBrokenImage" />
            <Building2 v-else :size="20" />
          </span>
          <span class="node-copy">
            <small>{{ node.slot }} · {{ node.buildingId }}</small>
            <strong>{{ node.buildingName }}</strong>
            <em>Уровень {{ node.level }}</em>
          </span>
          <Move v-if="editable && !linking" class="move-mark" :size="13" aria-hidden="true" />
          <Link2 v-if="node.id === linkSourceId" class="link-mark" :size="14" aria-hidden="true" />
        </button>

        <div v-if="!nodes.length" class="empty-blueprint">
          <Building2 :size="34" />
          <strong>Чертёж пока пуст</strong>
          <span>{{ editable ? 'Добавьте первый уровень постройки.' : 'В сценарии ещё нет построек.' }}</span>
          <button v-if="editable" type="button" @click="addAtCenter"><Plus :size="15" /> Добавить узел</button>
        </div>
      </div>

      <aside class="node-inspector" aria-label="Свойства выбранного уровня">
        <template v-if="selected">
          <div class="inspector-title">
            <span class="inspector-icon" :style="{ '--node-accent': colorFor(selected.slot) }">
              <img v-if="selected.image" :key="selected.image" :src="selected.image" alt="" @error="hideBrokenImage" />
              <Building2 v-else :size="20" />
            </span>
            <div>
              <small>{{ selected.slot }} · {{ selected.buildingId }}</small>
              <h3>Уровень {{ selected.level }}</h3>
            </div>
          </div>

          <template v-if="editable">
            <label>
              <span>Название</span>
              <input :value="selected.buildingName" @input="updateSelected({ buildingName: ($event.target as HTMLInputElement).value })" />
            </label>
            <label>
              <span>Описание</span>
              <textarea rows="4" :value="selected.description ?? ''" @input="updateSelected({ description: ($event.target as HTMLTextAreaElement).value || undefined })" />
            </label>
            <label>
              <span><ImageIcon :size="13" /> URL изображения</span>
              <input type="url" :value="selected.image ?? ''" placeholder="https://…" @input="updateSelected({ image: ($event.target as HTMLInputElement).value || undefined })" />
            </label>
            <div class="number-grid">
              <label>
                <span><Clock3 :size="13" /> Дней</span>
                <input type="number" min="0" :value="selected.timeCostDays" @input="updateNumber('timeCostDays', $event)" />
              </label>
              <label>
                <span><Wheat :size="13" /> Еды</span>
                <input type="number" min="0" :value="selected.foodCost" @input="updateNumber('foodCost', $event)" />
              </label>
              <label>
                <span><Users :size="13" /> Рабочих</span>
                <input type="number" min="0" :value="selected.workerDemand" @input="updateNumber('workerDemand', $event)" />
              </label>
            </div>
          </template>

          <template v-else>
            <p class="read-description">{{ selected.description || 'Описание уровня не задано.' }}</p>
            <dl class="read-values">
              <div><dt><Clock3 :size="13" /> Время</dt><dd>{{ selected.timeCostDays }} д.</dd></div>
              <div><dt><Wheat :size="13" /> Еда</dt><dd>{{ selected.foodCost }}</dd></div>
              <div><dt><Users :size="13" /> Рабочие</dt><dd>{{ selected.workerDemand }}</dd></div>
            </dl>
          </template>

          <div class="dependency-list">
            <span>Зависит от</span>
            <p v-if="!selected.dependencies.length">Независимый уровень</p>
            <div v-for="dependencyId in selected.dependencies" :key="dependencyId" class="dependency-chip">
              <button type="button" @click="chooseNode(dependencyId)">
                {{ nodeById.get(dependencyId)?.buildingName || dependencyId }}
                <small v-if="nodeById.get(dependencyId)">ур. {{ nodeById.get(dependencyId)?.level }}</small>
              </button>
              <button
                v-if="editable"
                class="unlink-button"
                type="button"
                :aria-label="`Удалить зависимость от ${nodeById.get(dependencyId)?.buildingName || dependencyId}`"
                @click="unlinkDependency(dependencyId)"
              ><Unlink :size="13" /></button>
            </div>
          </div>

          <button v-if="editable" class="delete-button" type="button" @click="emit('deleteNode', selected.id)">
            <Trash2 :size="15" /> Удалить уровень
          </button>
        </template>

        <template v-else>
          <Network :size="34" />
          <h3>Выберите узел</h3>
          <p>Здесь появятся свойства уровня, стоимость и список необходимых построек.</p>
        </template>
      </aside>
    </div>
  </section>
</template>

<style scoped>
* { box-sizing: border-box; }
.building-editor { overflow: hidden; border: 1px solid rgba(221, 198, 149, .18); border-radius: 16px; color: #eee5d2; background: #121510; box-shadow: 0 22px 55px rgba(0, 0, 0, .22); }
button, input, textarea { color: inherit; font: inherit; }
.editor-header { display: flex; min-height: 78px; align-items: center; gap: 18px; padding: 14px 18px; border-bottom: 1px solid rgba(221, 198, 149, .14); background: linear-gradient(105deg, #292419, #172019); }
.heading { margin-right: auto; }
.heading > span { color: #c7a765; font: 800 .59rem/1 var(--font-mono, monospace); letter-spacing: .14em; text-transform: uppercase; }
.heading h2 { display: flex; align-items: center; gap: 8px; margin: 5px 0 0; font: 700 1.35rem/1.1 Georgia, serif; }
.editor-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 7px; }
.editor-actions button, .empty-blueprint button { display: inline-flex; min-height: 36px; align-items: center; justify-content: center; gap: 6px; padding: 0 11px; border: 1px solid rgba(221, 198, 149, .23); border-radius: 7px; color: #ddd0b4; background: rgba(255, 255, 255, .035); cursor: pointer; }
.editor-actions button:hover, .editor-actions button:focus-visible, .empty-blueprint button:hover { border-color: #c3a25f; color: #ffe5a8; }
.editor-actions button.active { border-color: #d5b15f; color: #251d0e; background: #d5b15f; }
.editor-hint { min-height: 35px; margin: 0; padding: 10px 18px; border-bottom: 1px solid rgba(221, 198, 149, .1); color: rgba(238, 229, 210, .56); background: rgba(194, 160, 91, .045); font-size: .66rem; line-height: 1.35; }
.linking .editor-hint { color: #ebd391; background: rgba(194, 160, 91, .08); }

.editor-layout { display: grid; min-height: 620px; grid-template-columns: minmax(0, 1fr) 300px; }
.blueprint-canvas { position: relative; min-height: 620px; overflow: hidden; background: radial-gradient(circle at 50% 45%, rgba(191, 158, 88, .07), transparent 36%), linear-gradient(90deg, rgba(224, 205, 166, .025) 1px, transparent 1px), linear-gradient(rgba(224, 205, 166, .025) 1px, transparent 1px), #0e120e; background-size: auto, 28px 28px, 28px 28px; }
.blueprint-canvas::after { position: absolute; inset: 12px; border: 1px solid rgba(205, 181, 127, .055); content: ''; pointer-events: none; }
.canvas-label { position: absolute; z-index: 1; top: 12px; left: 14px; display: flex; align-items: center; gap: 6px; color: rgba(238, 229, 210, .2); font: 800 .55rem/1 var(--font-mono, monospace); letter-spacing: .1em; text-transform: uppercase; }
.dependency-lines { position: absolute; inset: 0; width: 100%; height: 100%; overflow: visible; pointer-events: none; }
.dependency-lines line { stroke: rgba(202, 179, 128, .38); stroke-width: .34; vector-effect: non-scaling-stroke; }
.dependency-lines marker path { fill: rgba(202, 179, 128, .72); }

.building-node { --node-accent: #9b956f; position: absolute; z-index: 2; display: grid; width: 168px; min-height: 86px; grid-template-columns: 40px minmax(0, 1fr); align-items: center; gap: 9px; padding: 10px; border: 1px solid color-mix(in srgb, var(--node-accent) 40%, #3f392c); border-radius: 10px; outline: none; color: #e7ddc8; background: linear-gradient(145deg, color-mix(in srgb, var(--node-accent) 8%, #26271f), #171a16); box-shadow: 0 8px 25px rgba(0, 0, 0, .28); cursor: pointer; touch-action: none; transform: translate(-50%, -50%); transition: border-color 130ms ease, box-shadow 130ms ease, transform 130ms ease; }
.editable .building-node { cursor: grab; }
.linking .building-node { cursor: crosshair; }
.building-node:hover, .building-node:focus-visible, .building-node.selected { z-index: 4; border-color: var(--node-accent); box-shadow: 0 0 0 2px color-mix(in srgb, var(--node-accent) 18%, transparent), 0 11px 30px rgba(0, 0, 0, .36); transform: translate(-50%, -50%) scale(1.025); }
.building-node.dragging { z-index: 6; cursor: grabbing; opacity: .9; transform: translate(-50%, -50%) scale(1.04); transition: none; }
.building-node.link-source { z-index: 5; outline: 2px solid #f1cc72; outline-offset: 3px; }
.node-art, .inspector-icon { --node-accent: #9b956f; display: grid; width: 40px; height: 40px; flex: 0 0 auto; place-items: center; overflow: hidden; border: 1px solid color-mix(in srgb, var(--node-accent) 42%, transparent); border-radius: 50%; color: var(--node-accent); background: color-mix(in srgb, var(--node-accent) 12%, #171a16); }
.node-art img, .inspector-icon img { width: 100%; height: 100%; object-fit: cover; }
.node-copy { display: flex; min-width: 0; flex-direction: column; align-items: flex-start; text-align: left; }
.node-copy small { width: 100%; overflow: hidden; color: color-mix(in srgb, var(--node-accent) 75%, #eee5d2); font: 700 .49rem/1 var(--font-mono, monospace); text-overflow: ellipsis; text-transform: uppercase; white-space: nowrap; }
.node-copy strong { display: -webkit-box; overflow: hidden; margin: 5px 0 3px; font: 700 .73rem/1.13 Georgia, serif; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
.node-copy em { color: rgba(238, 229, 210, .48); font: 700 .53rem/1 var(--font-mono, monospace); font-style: normal; }
.move-mark, .link-mark { position: absolute; top: 5px; right: 5px; color: var(--node-accent); opacity: .38; }
.link-mark { color: #f1cc72; opacity: 1; }
.empty-blueprint { position: absolute; inset: 0; display: grid; place-content: center; place-items: center; gap: 7px; color: rgba(238, 229, 210, .45); text-align: center; }
.empty-blueprint strong { color: #ddd1b9; font: 700 1rem/1 Georgia, serif; }
.empty-blueprint span { font-size: .7rem; }
.empty-blueprint button { margin-top: 7px; }

.node-inspector { display: flex; min-width: 0; flex-direction: column; align-items: stretch; gap: 12px; padding: 20px; border-left: 1px solid rgba(221, 198, 149, .13); background: linear-gradient(155deg, #1c1d17, #151813); }
.node-inspector > svg { align-self: center; margin-top: auto; color: rgba(212, 188, 137, .3); }
.node-inspector > h3 { margin: 0; text-align: center; font: 700 1.15rem/1 Georgia, serif; }
.node-inspector > p { margin: 0 auto auto; color: rgba(238, 229, 210, .48); font-size: .7rem; line-height: 1.5; text-align: center; }
.inspector-title { display: flex; align-items: center; gap: 10px; padding-bottom: 12px; border-bottom: 1px solid rgba(221, 198, 149, .12); }
.inspector-title small { color: rgba(238, 229, 210, .42); font: 700 .54rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.inspector-title h3 { margin: 5px 0 0; font: 700 1.12rem/1 Georgia, serif; }
.node-inspector label { display: grid; gap: 6px; }
.node-inspector label > span { display: flex; align-items: center; gap: 5px; color: rgba(238, 229, 210, .56); font: 700 .59rem/1 var(--font-mono, monospace); }
.node-inspector input, .node-inspector textarea { width: 100%; border: 1px solid rgba(221, 198, 149, .19); border-radius: 7px; outline: none; color: #f1e8d5; background: #0c100c; font-size: .75rem; }
.node-inspector input { height: 37px; padding: 0 9px; }
.node-inspector textarea { padding: 9px; resize: vertical; line-height: 1.4; }
.node-inspector input:focus, .node-inspector textarea:focus { border-color: #c2a05d; box-shadow: 0 0 0 2px rgba(194, 160, 93, .1); }
.number-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 7px; }
.read-description { margin: 0; color: rgba(238, 229, 210, .62); font-size: .72rem; line-height: 1.5; }
.read-values { display: grid; grid-template-columns: repeat(3, 1fr); gap: 6px; margin: 0; }
.read-values div { padding: 9px 5px; border-radius: 7px; background: rgba(255, 255, 255, .035); text-align: center; }
.read-values dt { display: flex; align-items: center; justify-content: center; gap: 4px; color: rgba(238, 229, 210, .42); font: 700 .52rem/1 var(--font-mono, monospace); }
.read-values dd { margin: 5px 0 0; font: 800 .64rem/1 var(--font-mono, monospace); }
.dependency-list { display: grid; gap: 6px; padding-top: 4px; }
.dependency-list > span { color: rgba(238, 229, 210, .42); font: 800 .54rem/1 var(--font-mono, monospace); letter-spacing: .08em; text-transform: uppercase; }
.dependency-list > p { margin: 0; color: rgba(238, 229, 210, .36); font-size: .65rem; }
.dependency-chip { display: grid; grid-template-columns: minmax(0, 1fr) auto; overflow: hidden; border: 1px solid rgba(209, 183, 128, .15); border-radius: 6px; background: rgba(191, 158, 88, .05); }
.dependency-chip button { min-width: 0; padding: 7px 8px; border: 0; color: #d9c89f; background: transparent; cursor: pointer; font-size: .63rem; text-align: left; }
.dependency-chip button:first-child:hover { color: #ffe3a1; background: rgba(191, 158, 88, .07); }
.dependency-chip small { margin-left: 4px; color: rgba(238, 229, 210, .4); }
.dependency-chip .unlink-button { display: grid; width: 31px; place-items: center; border-left: 1px solid rgba(209, 183, 128, .13); color: #bf8580; text-align: center; }
.dependency-chip .unlink-button:hover { color: #f0aba4; background: rgba(151, 61, 56, .1); }
.delete-button { display: inline-flex; min-height: 36px; align-items: center; justify-content: center; gap: 6px; margin-top: auto; border: 1px solid rgba(181, 80, 75, .28); border-radius: 7px; color: #d89a94; background: rgba(130, 48, 44, .08); cursor: pointer; font-size: .68rem; }
.delete-button:hover, .delete-button:focus-visible { border-color: #c36d64; color: #f3b0a8; background: rgba(130, 48, 44, .15); }

@media (max-width: 900px) {
  .editor-header { align-items: flex-start; flex-wrap: wrap; }
  .editor-actions { width: 100%; justify-content: flex-start; }
  .editor-layout { grid-template-columns: 1fr; }
  .blueprint-canvas { min-height: 540px; }
  .node-inspector { min-height: 260px; border-top: 1px solid rgba(221, 198, 149, .13); border-left: 0; }
}

@media (max-width: 560px) {
  .editor-header { padding: 13px; }
  .heading h2 { font-size: 1.15rem; }
  .editor-actions button { flex: 1; padding: 0 7px; font-size: .65rem; }
  .editor-hint { padding: 9px 13px; }
  .blueprint-canvas { min-height: 480px; }
  .building-node { width: 132px; min-height: 76px; grid-template-columns: 31px minmax(0, 1fr); gap: 6px; padding: 7px; }
  .node-art { width: 31px; height: 31px; }
  .node-copy strong { font-size: .65rem; }
  .node-copy small { max-width: 85px; }
  .node-inspector { padding: 15px; }
  .number-grid { grid-template-columns: 1fr 1fr 1fr; }
}

@media (prefers-reduced-motion: reduce) {
  .building-node { transition: none; }
}
</style>
