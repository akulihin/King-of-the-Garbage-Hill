<script setup lang="ts">
import { computed } from 'vue'
import { Check, ChevronRight, LockKeyhole, Map as MapIcon, Skull, Sparkles, Swords, X } from 'lucide-vue-next'

export type LastChancesLocale = 'ru' | 'en'
export type RunNodeState = 'locked' | 'available' | 'current' | 'visited' | 'cleared'

export type RunMapNode = {
  id: string
  name: string
  tier: number
  kind: string
  state: RunNodeState
}

export type RunMapEdge = {
  from: string
  to: string
}

type PositionedNode = RunMapNode & {
  x: number
  y: number
}

const props = withDefaults(defineProps<{
  open: boolean
  locale: LastChancesLocale
  nodes: RunMapNode[]
  edges: RunMapEdge[]
  seed: string
  allowClose?: boolean
}>(), {
  allowClose: true,
})

const emit = defineEmits<{
  close: []
  choose: [nodeId: string]
}>()

const copy = {
  en: {
    eyebrow: 'The run remembers',
    title: 'Choose what waits ahead',
    seed: 'Generation',
    close: 'Return to the room',
    route: 'Route map',
    tier: 'Tier',
    boss: 'Boss',
    available: 'Available',
    current: 'Current room',
    visited: 'Visited',
    cleared: 'Cleared',
    locked: 'Unknown',
    choose: 'Enter',
    empty: 'The route has not formed yet.',
  },
  ru: {
    eyebrow: 'Забег всё помнит',
    title: 'Выберите, что ждёт впереди',
    seed: 'Генерация',
    close: 'Вернуться в комнату',
    route: 'Карта забега',
    tier: 'Уровень',
    boss: 'Босс',
    available: 'Доступно',
    current: 'Текущая комната',
    visited: 'Посещено',
    cleared: 'Зачищено',
    locked: 'Неизвестно',
    choose: 'Войти',
    empty: 'Маршрут ещё не сформирован.',
  },
} as const

const t = computed(() => copy[props.locale])
const maxTier = computed(() => Math.max(1, ...props.nodes.map(node => node.tier)))
const minTier = computed(() => Math.min(1, ...props.nodes.map(node => node.tier)))
const routeSubtitle = computed(() => {
  const normalTierCount = Math.max(0, maxTier.value - 1)
  return props.locale === 'ru'
    ? `Обычных уровней: ${normalTierCount}, затем босс. Один путь наверх. Пройденные комнаты остаются позади; открыты только светящиеся маршруты.`
    : `${normalTierCount} normal ${normalTierCount === 1 ? 'tier' : 'tiers'}, then the boss. One route upward. Cleared rooms stay behind you; only glowing paths are open.`
})

const positionedNodes = computed<PositionedNode[]>(() => {
  const tiers = new Map<number, RunMapNode[]>()
  for (const node of props.nodes) {
    const entries = tiers.get(node.tier) ?? []
    entries.push(node)
    tiers.set(node.tier, entries)
  }

  const span = Math.max(1, maxTier.value - minTier.value)
  return [...tiers.entries()].flatMap(([tier, nodes]) => {
    const sorted = [...nodes].sort((a, b) => a.id.localeCompare(b.id))
    return sorted.map((node, index) => ({
      ...node,
      x: ((index + 1) / (sorted.length + 1)) * 100,
      y: 90 - ((tier - minTier.value) / span) * 80,
    }))
  })
})

const nodePositions = computed(() => new Map(positionedNodes.value.map(node => [node.id, node])))
const positionedEdges = computed(() => props.edges.flatMap((edge) => {
  const from = nodePositions.value.get(edge.from)
  const to = nodePositions.value.get(edge.to)
  return from && to ? [{ ...edge, fromNode: from, toNode: to }] : []
}))

function nodeStateLabel(state: RunNodeState): string {
  return t.value[state]
}

function nodeIcon(kind: string) {
  const normalized = kind.toLowerCase()
  if (normalized.includes('boss')) return Skull
  if (normalized.includes('combat') || normalized.includes('fight')) return Swords
  if (normalized.includes('reward') || normalized.includes('rest') || normalized.includes('event')) return Sparkles
  return MapIcon
}

function edgeState(from: PositionedNode, to: PositionedNode): string {
  if (from.state === 'cleared' && to.state === 'cleared') return 'cleared'
  if (from.state === 'cleared' && ['available', 'current', 'cleared', 'visited'].includes(to.state)) return 'open'
  if (from.state === 'current' || to.state === 'current') return 'current'
  return 'locked'
}
</script>

<template>
  <Transition name="lc-map-fade">
    <section
      v-if="open"
      class="lc-map-backdrop"
      role="dialog"
      aria-modal="true"
      :aria-label="t.route"
      @keydown.esc="allowClose && emit('close')"
    >
      <div class="lc-map-shell">
        <header class="lc-map-header">
          <div>
            <p class="lc-map-eyebrow">{{ t.eyebrow }}</p>
            <h2>{{ t.title }}</h2>
            <p class="lc-map-subtitle">{{ routeSubtitle }}</p>
          </div>
          <div class="lc-map-header-actions">
            <span class="lc-map-seed"><span>{{ t.seed }}</span>{{ seed }}</span>
            <button
              v-if="allowClose"
              class="lc-map-close"
              type="button"
              :aria-label="t.close"
              :title="t.close"
              @click="emit('close')"
            >
              <X :size="20" aria-hidden="true" />
            </button>
          </div>
        </header>

        <div v-if="positionedNodes.length" class="lc-route" aria-live="polite">
          <div class="lc-tier-rail" aria-hidden="true">
            <span
              v-for="tier in maxTier"
              :key="tier"
              :style="{ top: `${90 - ((tier - minTier) / Math.max(1, maxTier - minTier)) * 80}%` }"
            >
              {{ tier === maxTier ? t.boss : `${t.tier} ${tier}` }}
            </span>
          </div>

          <svg class="lc-route-lines" viewBox="0 0 1000 700" preserveAspectRatio="none" aria-hidden="true">
            <line
              v-for="edge in positionedEdges"
              :key="`${edge.from}-${edge.to}`"
              :class="`is-${edgeState(edge.fromNode, edge.toNode)}`"
              :x1="edge.fromNode.x * 10"
              :y1="edge.fromNode.y * 7"
              :x2="edge.toNode.x * 10"
              :y2="edge.toNode.y * 7"
            />
          </svg>

          <button
            v-for="node in positionedNodes"
            :key="node.id"
            class="lc-route-node"
            :class="[`is-${node.state}`, `is-${node.kind.toLowerCase()}`]"
            :style="{ left: `${node.x}%`, top: `${node.y}%` }"
            :disabled="node.state !== 'available'"
            type="button"
            :aria-label="`${node.name}. ${t.tier} ${node.tier}. ${nodeStateLabel(node.state)}`"
            @click="emit('choose', node.id)"
          >
            <span class="lc-node-orbit" aria-hidden="true" />
            <span class="lc-node-medallion">
              <Check v-if="node.state === 'cleared'" :size="18" aria-hidden="true" />
              <LockKeyhole v-else-if="node.state === 'locked'" :size="16" aria-hidden="true" />
              <component :is="nodeIcon(node.kind)" v-else :size="19" aria-hidden="true" />
            </span>
            <span class="lc-node-copy">
              <small>{{ t.tier }} {{ node.tier }} · {{ nodeStateLabel(node.state) }}</small>
              <strong>{{ node.name }}</strong>
              <span v-if="node.state === 'available'">{{ t.choose }} <ChevronRight :size="12" aria-hidden="true" /></span>
            </span>
          </button>
        </div>

        <p v-else class="lc-route-empty">{{ t.empty }}</p>

        <footer class="lc-map-legend" aria-label="Legend">
          <span v-for="state in (['available', 'current', 'cleared', 'visited', 'locked'] as RunNodeState[])" :key="state">
            <i :class="`is-${state}`" aria-hidden="true" />{{ nodeStateLabel(state) }}
          </span>
        </footer>
      </div>
    </section>
  </Transition>
</template>

<style scoped>
.lc-map-backdrop {
  position: fixed;
  z-index: 4100;
  inset: 0;
  display: grid;
  place-items: center;
  padding: clamp(0.5rem, 2.5vw, 2rem);
  color: #ebe8df;
  background:
    radial-gradient(circle at 50% 15%, rgba(137, 33, 40, 0.18), transparent 34%),
    rgba(4, 5, 6, 0.9);
  backdrop-filter: blur(12px) saturate(0.75);
}

.lc-map-shell {
  width: min(72rem, 100%);
  height: min(52rem, calc(100dvh - 2rem));
  display: grid;
  grid-template-rows: auto minmax(24rem, 1fr) auto;
  overflow: hidden;
  border: 1px solid rgba(205, 190, 163, 0.18);
  border-radius: 1rem;
  background:
    linear-gradient(115deg, rgba(255, 255, 255, 0.025), transparent 34%),
    radial-gradient(circle at 50% 110%, rgba(119, 25, 32, 0.15), transparent 43%),
    #0b0d0e;
  box-shadow: 0 2rem 6rem rgba(0, 0, 0, 0.72), inset 0 0 5rem rgba(0, 0, 0, 0.44);
}

.lc-map-header {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  padding: clamp(1rem, 2.5vw, 1.75rem) clamp(1rem, 3vw, 2.25rem) 0.75rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
}

.lc-map-eyebrow {
  margin: 0 0 0.2rem;
  color: #b54c53;
  font-size: 0.68rem;
  font-weight: 800;
  letter-spacing: 0.2em;
  text-transform: uppercase;
}

.lc-map-header h2 {
  margin: 0;
  color: #f5f0e6;
  font: 600 clamp(1.3rem, 3vw, 2.15rem)/1.08 Georgia, 'Times New Roman', serif;
  letter-spacing: -0.025em;
}

.lc-map-subtitle {
  max-width: 45rem;
  margin: 0.45rem 0 0;
  color: #8f9291;
  font-size: 0.78rem;
  line-height: 1.5;
}

.lc-map-header-actions {
  display: flex;
  align-items: flex-start;
  gap: 0.65rem;
}

.lc-map-seed {
  display: grid;
  min-width: 8rem;
  padding: 0.45rem 0.7rem;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 0.5rem;
  color: #d3c7b2;
  background: rgba(255, 255, 255, 0.025);
  font: 600 0.69rem/1.2 var(--font-mono, monospace);
}

.lc-map-seed span {
  color: #6f7473;
  font-size: 0.55rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
}

.lc-map-close {
  width: 2.35rem;
  height: 2.35rem;
  display: grid;
  place-items: center;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 50%;
  color: #aaa9a4;
  background: rgba(255, 255, 255, 0.035);
}

.lc-map-close:hover { color: #fff; border-color: rgba(255, 255, 255, 0.28); }

.lc-route {
  position: relative;
  min-height: 0;
  margin: 0.6rem 1rem;
  overflow: hidden;
  background:
    linear-gradient(90deg, transparent 49.9%, rgba(255, 255, 255, 0.025) 50%, transparent 50.1%),
    repeating-linear-gradient(0deg, transparent 0 15.9%, rgba(255, 255, 255, 0.025) 16% 16.2%);
}

.lc-tier-rail {
  position: absolute;
  z-index: 3;
  inset: 0 auto 0 0;
  width: 5rem;
  pointer-events: none;
}

.lc-tier-rail span {
  position: absolute;
  left: 0;
  transform: translateY(-50%);
  color: #4d5251;
  font: 700 0.58rem/1 var(--font-mono, monospace);
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.lc-route-lines {
  position: absolute;
  z-index: 1;
  inset: 0;
  width: 100%;
  height: 100%;
  overflow: visible;
}

.lc-route-lines line {
  vector-effect: non-scaling-stroke;
  stroke: rgba(113, 116, 113, 0.19);
  stroke-width: 1.5;
  stroke-dasharray: 5 7;
}

.lc-route-lines line.is-cleared { stroke: rgba(155, 151, 134, 0.42); stroke-dasharray: none; }
.lc-route-lines line.is-current { stroke: rgba(184, 68, 74, 0.62); stroke-dasharray: none; }
.lc-route-lines line.is-open { stroke: rgba(211, 185, 127, 0.62); stroke-dasharray: none; filter: drop-shadow(0 0 4px rgba(205, 168, 91, 0.32)); }

.lc-route-node {
  position: absolute;
  z-index: 4;
  width: clamp(7.5rem, 16vw, 11.5rem);
  min-height: 3.5rem;
  display: grid;
  grid-template-columns: auto 1fr;
  align-items: center;
  gap: 0.55rem;
  padding: 0.45rem 0.65rem 0.45rem 0.45rem;
  transform: translate(-50%, -50%);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 2.2rem 0.6rem 0.6rem 2.2rem;
  color: #9fa19e;
  background: rgba(11, 13, 14, 0.92);
  box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.28);
  text-align: left;
}

.lc-route-node:disabled { cursor: default; }
.lc-route-node.is-locked { opacity: 0.42; filter: saturate(0); }
.lc-route-node.is-visited { opacity: 0.66; }
.lc-route-node.is-cleared { color: #aaa897; border-color: rgba(159, 157, 138, 0.24); }
.lc-route-node.is-current { color: #f0e8da; border-color: rgba(182, 62, 69, 0.6); box-shadow: 0 0 1.5rem rgba(160, 32, 40, 0.17); }
.lc-route-node.is-available {
  color: #f1e5cc;
  border-color: rgba(207, 176, 108, 0.58);
  background: linear-gradient(110deg, rgba(104, 77, 28, 0.26), rgba(12, 13, 14, 0.95) 48%);
  box-shadow: 0 0 1.7rem rgba(204, 163, 79, 0.14);
}

.lc-route-node.is-available:hover,
.lc-route-node.is-available:focus-visible {
  transform: translate(-50%, -50%) scale(1.045);
  border-color: rgba(238, 207, 139, 0.92);
  box-shadow: 0 0 2rem rgba(204, 163, 79, 0.24);
}

.lc-node-medallion {
  position: relative;
  width: 2.4rem;
  height: 2.4rem;
  display: grid;
  place-items: center;
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 50%;
  background: #141718;
}

.is-available .lc-node-medallion { color: #efd89e; border-color: #a78035; }
.is-current .lc-node-medallion { color: #f0b1af; border-color: #a8373e; }
.is-cleared .lc-node-medallion { color: #a6aa91; }

.lc-node-orbit {
  position: absolute;
  z-index: -1;
  left: 1.62rem;
  width: 3rem;
  height: 3rem;
  border: 1px solid transparent;
  border-radius: 50%;
}

.is-available .lc-node-orbit {
  border-color: rgba(221, 187, 111, 0.3);
  animation: lc-node-pulse 2.2s ease-in-out infinite;
}

.lc-node-copy { min-width: 0; display: grid; gap: 0.06rem; }
.lc-node-copy small { overflow: hidden; color: #737775; font-size: 0.53rem; font-weight: 700; letter-spacing: 0.07em; text-overflow: ellipsis; text-transform: uppercase; white-space: nowrap; }
.lc-node-copy strong { overflow: hidden; font-size: 0.7rem; font-weight: 700; line-height: 1.2; text-overflow: ellipsis; white-space: nowrap; }
.lc-node-copy > span { display: inline-flex; align-items: center; color: #d6b96f; font-size: 0.56rem; font-weight: 700; text-transform: uppercase; }

.lc-route-empty { place-self: center; color: #777b79; }

.lc-map-legend {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 0.8rem 1.4rem;
  padding: 0.8rem 1rem 1rem;
  border-top: 1px solid rgba(255, 255, 255, 0.05);
  color: #727674;
  font-size: 0.62rem;
  letter-spacing: 0.07em;
  text-transform: uppercase;
}

.lc-map-legend span { display: inline-flex; align-items: center; gap: 0.35rem; }
.lc-map-legend i { width: 0.55rem; height: 0.55rem; border: 1px solid #555b58; border-radius: 50%; }
.lc-map-legend i.is-available { border-color: #d3ac57; box-shadow: 0 0 0.45rem #d3ac57; }
.lc-map-legend i.is-current { border-color: #b63f46; background: #7c242a; }
.lc-map-legend i.is-cleared { border-color: #a3a28c; background: #55574c; }
.lc-map-legend i.is-visited { border-color: #777a76; }
.lc-map-legend i.is-locked { opacity: 0.4; }

@keyframes lc-node-pulse {
  0%, 100% { opacity: 0.25; transform: scale(0.92); }
  50% { opacity: 0.8; transform: scale(1.08); }
}

.lc-map-fade-enter-active,
.lc-map-fade-leave-active { transition: opacity 0.2s ease; }
.lc-map-fade-enter-from,
.lc-map-fade-leave-to { opacity: 0; }

@media (max-width: 720px) {
  .lc-map-backdrop { padding: 0; }
  .lc-map-shell { width: 100%; height: 100dvh; border: 0; border-radius: 0; }
  .lc-map-header { padding: 0.85rem; }
  .lc-map-subtitle { display: none; }
  .lc-map-seed { display: none; }
  .lc-route { margin-inline: 0.2rem; }
  .lc-tier-rail { width: 3.3rem; }
  .lc-tier-rail span { font-size: 0.49rem; }
  .lc-route-node { width: clamp(5rem, 26vw, 7.2rem); min-height: 2.9rem; gap: 0.35rem; padding: 0.3rem; }
  .lc-node-medallion { width: 1.85rem; height: 1.85rem; }
  .lc-node-orbit { left: 1.1rem; width: 2.3rem; height: 2.3rem; }
  .lc-node-copy small { font-size: 0.43rem; }
  .lc-node-copy strong { font-size: 0.57rem; }
  .lc-node-copy > span { display: none; }
  .lc-map-legend { gap: 0.5rem 0.8rem; font-size: 0.5rem; }
}

@media (prefers-reduced-motion: reduce) {
  .lc-node-orbit,
  .lc-map-fade-enter-active,
  .lc-map-fade-leave-active { animation: none; transition: none; }
}
</style>
