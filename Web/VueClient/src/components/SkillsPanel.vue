<script setup lang="ts">
import { ref, watch, nextTick, computed } from 'vue'
import type { Player } from 'src/services/signalr'
import { playTheBoysReveal, playTheBoysUnlock } from 'src/services/sound'
import { formatPassiveDescription } from 'src/services/textFormatting'
import { translateText } from 'src/i18n'

const props = defineProps<{
  player: Player
}>()

const expandedSet = ref<Set<number>>(new Set())
const skillCardRefs = ref<(HTMLElement | null)[]>([])

const theBoys = computed(() => props.player.passiveAbilityStates?.theBoys ?? null)
const jonSnow = computed(() => props.player.passiveAbilityStates?.jonSnow ?? null)
const isTerminalMode = computed(() => props.player.isTerminalMode ?? false)
const SERVER_KING_NAME = 'Король Сервера'

function toggleSkill(idx: number) {
  if (isTerminalMode.value || !props.player.character.passives[idx]?.visible) return
  if (expandedSet.value.has(idx)) {
    expandedSet.value.delete(idx)
  } else {
    expandedSet.value.add(idx)
  }
  expandedSet.value = new Set(expandedSet.value)
}

function isExpanded(idx: number): boolean {
  return (isTerminalMode.value || expandedSet.value.has(idx))
    && props.player.character.passives[idx]?.visible !== false
}

function passiveIndexByName(name: string): number {
  return props.player.character.passives.findIndex((p) => p.name === name)
}

// ── Cinematic VFX state ───────────────────────────────────────────────
const projectileEl = ref<HTMLElement | null>(null)
const flyActive = ref(false)
const ring = ref<{ x: number; y: number; w: number; h: number } | null>(null)
const unlockOverlay = ref<{ name: string; isCombination: boolean } | null>(null)
const jonKingOverlay = ref(false)
const jonKingHighlightIndex = ref<number | null>(null)
const jonKingRing = ref<{ x: number; y: number; w: number; h: number } | null>(null)

function showRing(rect: DOMRect, ttl = 2200) {
  ring.value = { x: rect.left - 6, y: rect.top - 6, w: rect.width + 12, h: rect.height + 12 }
  window.setTimeout(() => {
    ring.value = null
  }, ttl)
}

function showJonKingRing(rect: DOMRect, ttl = 3200) {
  jonKingRing.value = {
    x: rect.left - 8,
    y: rect.top - 8,
    w: rect.width + 16,
    h: rect.height + 16,
  }
  window.setTimeout(() => {
    jonKingRing.value = null
  }, ttl)
}

// Reveal: fly a projectile from the left (stat area) to the skill card on the right, then circle the new text.
async function triggerRevealVfx(idx: number) {
  await nextTick()
  const el = skillCardRefs.value[idx]
  if (!el) return
  const rect = el.getBoundingClientRect()
  const toX = rect.left + 14
  const toY = rect.top + rect.height / 2
  const fromX = Math.max(rect.left - 340, 24)
  const fromY = toY + (Math.random() * 60 - 30)

  flyActive.value = true
  await nextTick()
  const p = projectileEl.value
  if (!p) {
    showRing(rect)
    playTheBoysReveal()
    return
  }
  const anim = p.animate(
    [
      { transform: `translate(${fromX}px, ${fromY}px) scale(0.5)`, opacity: 0 },
      { transform: `translate(${(fromX + toX) / 2}px, ${fromY - 90}px) scale(1.15)`, opacity: 1, offset: 0.55 },
      { transform: `translate(${toX}px, ${toY}px) scale(0.8)`, opacity: 1 },
    ],
    { duration: 760, easing: 'cubic-bezier(0.35, 0, 0.2, 1)' },
  )
  playTheBoysReveal()
  anim.onfinish = () => {
    flyActive.value = false
    showRing(rect)
  }
}

// Unlock: full-screen announce overlay + a strong ring drawing attention to the freshly-unlocked card.
async function triggerUnlockVfx(name: string, idx: number, isCombination: boolean) {
  unlockOverlay.value = { name, isCombination }
  if (isCombination) playTheBoysReveal()
  else playTheBoysUnlock()
  window.setTimeout(() => {
    unlockOverlay.value = null
  }, isCombination ? 2200 : 3200)
  if (idx >= 0) {
    expandedSet.value.add(idx)
    expandedSet.value = new Set(expandedSet.value)
    await nextTick()
    const el = skillCardRefs.value[idx]
    if (el) showRing(el.getBoundingClientRect(), isCombination ? 2200 : 3200)
  }
}

async function triggerServerKingVfx() {
  const idx = passiveIndexByName(SERVER_KING_NAME)
  if (idx < 0) return

  // The transformation owns the player's attention: keep only the new passive open.
  expandedSet.value = new Set([idx])
  jonKingHighlightIndex.value = idx
  jonKingOverlay.value = true
  window.setTimeout(() => {
    jonKingOverlay.value = false
  }, 3600)
  window.setTimeout(() => {
    jonKingHighlightIndex.value = null
  }, 4000)

  await nextTick()
  window.setTimeout(() => {
    const card = skillCardRefs.value[idx]
    if (!card) return
    const description = card.querySelector<HTMLElement>('.skill-desc')
    showJonKingRing((description ?? card).getBoundingClientRect())
  }, 360)
}

watch(
  () => theBoys.value?.revealSerial,
  (nv, ov) => {
    // ov == null means this is the initial data load — skip so we only animate genuine in-session upgrades.
    if (nv == null || ov == null || nv === ov) return
    const member = theBoys.value?.lastRevealedMember
    if (!member) return
    const idx = passiveIndexByName(member)
    if (idx < 0) return
    expandedSet.value.add(idx)
    expandedSet.value = new Set(expandedSet.value)
    void triggerRevealVfx(idx)
  },
)

watch(
  () => theBoys.value?.unlockSerial,
  (nv, ov) => {
    if (nv == null || ov == null || nv === ov) return
    const name = theBoys.value?.lastUnlockedAbility ?? ''
    void triggerUnlockVfx(
      name,
      passiveIndexByName(name),
      theBoys.value?.lastUnlockWasCombination ?? false,
    )
  },
)

watch(
  () => jonSnow.value?.isKing,
  (isKing, wasKing) => {
    // Initial/reconnected King state is not a new transformation.
    if (isKing !== true || wasKing !== false) return
    void triggerServerKingVfx()
  },
)
</script>

<template>
  <div class="skills-panel" :class="{ 'is-terminal-editor': isTerminalMode }">
    <div v-if="isTerminalMode" class="terminal-editor-chrome" aria-hidden="true">
      <span class="terminal-editor-dot red" />
      <span class="terminal-editor-dot amber" />
      <span class="terminal-editor-dot green" />
      <span class="terminal-editor-tab">runtime/passives.cs</span>
      <span class="terminal-editor-mode">INSERT // UTF-8</span>
    </div>
    <div
      v-for="(passive, idx) in player.character.passives"
      :key="idx"
      :ref="(el) => { skillCardRefs[idx] = el as HTMLElement | null }"
      class="skill-card"
      :class="{
        expanded: isExpanded(idx),
        locked: !passive.visible,
        'terminal-code-block': isTerminalMode,
        'jon-king-highlight': jonKingHighlightIndex === idx,
        'deep-highlight': passive.theme === 'deep',
      }"
      :tabindex="isTerminalMode || !passive.visible ? -1 : 0"
      :role="isTerminalMode || !passive.visible ? undefined : 'button'"
      @click="toggleSkill(idx)"
      @keydown.enter.prevent="toggleSkill(idx)"
      @keydown.space.prevent="toggleSkill(idx)"
    >
      <div class="skill-header">
        <div class="skill-header-left">
          <span v-if="!isTerminalMode && passive.visible" class="skill-dot dot-active" />
          <span v-else-if="!isTerminalMode" class="skill-lock" aria-label="Закрытая способность">🔒</span>
          <span v-else class="terminal-line-no">{{ String((idx * 4) + 1).padStart(2, '0') }}</span>
          <span v-if="isTerminalMode || passive.visible" class="skill-name">{{ isTerminalMode ? `// ${passive.name}` : passive.name }}</span>
        </div>
        <div class="skill-header-right">
          <span v-if="!isTerminalMode && passive.visible" class="skill-chevron" :class="{ 'chevron-open': isExpanded(idx) }">▾</span>
        </div>
      </div>
      <Transition name="expand">
        <div v-if="passive.visible && isExpanded(idx)" class="skill-desc" :class="{ 'terminal-code-copy': isTerminalMode }" v-html="formatPassiveDescription(translateText(passive.description))" />
      </Transition>
    </div>

    <div v-if="player.character.passives.length === 0" class="no-skills">
      No passives available.
    </div>
  </div>

  <!-- Cinematic VFX (teleported to body so they overlay the whole screen) -->
  <Teleport to="body">
    <div v-if="flyActive" ref="projectileEl" class="tb-projectile" />
    <div
      v-if="ring"
      class="tb-ring"
      :style="{ left: `${ring.x}px`, top: `${ring.y}px`, width: `${ring.w}px`, height: `${ring.h}px` }"
    />
    <div
      v-if="jonKingRing"
      class="jon-king-ring"
      :style="{
        left: `${jonKingRing.x}px`,
        top: `${jonKingRing.y}px`,
        width: `${jonKingRing.w}px`,
        height: `${jonKingRing.h}px`,
      }"
    />
    <Transition name="tb-unlock">
      <div v-if="unlockOverlay" class="tb-unlock-overlay" :class="{ 'is-combination': unlockOverlay.isCombination }">
        <div class="tb-unlock-card">
          <div class="tb-unlock-label">{{ unlockOverlay.isCombination ? 'КОМБИНАЦИЯ ОТКРЫТА' : 'СПОСОБНОСТЬ ОТКРЫТА' }}</div>
          <div class="tb-unlock-lock">🔓</div>
          <div class="tb-unlock-name">{{ unlockOverlay.name }}</div>
          <div class="tb-unlock-sub">The Boys</div>
        </div>
      </div>
    </Transition>
    <Transition name="jon-king-unlock">
      <div v-if="jonKingOverlay" class="jon-king-overlay" aria-hidden="true">
        <div class="jon-king-announcement">
          <div class="jon-king-label">ПАССИВКА ОТКРЫТА</div>
          <div class="jon-king-sigil">❄️</div>
          <div class="jon-king-name">{{ translateText(SERVER_KING_NAME) }}</div>
          <div class="jon-king-sub">{{ translateText('Джон Сноу') }}</div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.skills-panel {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.skills-panel.is-terminal-editor {
  position: relative;
  gap: 0;
  overflow: hidden;
  border: 1px solid rgba(0, 255, 65, 0.4);
  border-radius: 6px;
  background:
    repeating-linear-gradient(0deg, transparent 0 3px, rgba(0, 255, 65, 0.025) 4px),
    #000b03;
  box-shadow: 0 0 18px rgba(0, 255, 65, 0.12), inset 0 0 24px rgba(0, 255, 65, 0.035);
  font-family: var(--font-mono);
}
.skills-panel.is-terminal-editor::after {
  content: '';
  position: absolute;
  inset: 0;
  pointer-events: none;
  background: linear-gradient(90deg, transparent 0 48%, rgba(0, 255, 65, 0.035) 50%, transparent 52%);
  background-size: 190px 100%;
  animation: terminal-editor-scan 7s linear infinite;
}
.terminal-editor-chrome {
  position: relative;
  z-index: 2;
  display: flex;
  align-items: center;
  gap: 5px;
  min-height: 28px;
  padding: 0 8px;
  border-bottom: 1px solid rgba(0, 255, 65, 0.28);
  background: #07130a;
  color: rgba(126, 255, 157, 0.58);
  font: 700 8px/1 var(--font-mono);
}
.terminal-editor-dot { width: 7px; height: 7px; border-radius: 50%; }
.terminal-editor-dot.red { background: #c84c4c; }
.terminal-editor-dot.amber { background: #c99b38; }
.terminal-editor-dot.green { background: #00d63a; box-shadow: 0 0 5px rgba(0, 255, 65, 0.65); }
.terminal-editor-tab {
  align-self: stretch;
  display: inline-flex;
  align-items: center;
  margin-left: 5px;
  padding: 0 9px;
  border-inline: 1px solid rgba(0, 255, 65, 0.18);
  background: #000b03;
  color: #78ff99;
}
.terminal-editor-mode { margin-left: auto; }
@keyframes terminal-editor-scan {
  to { background-position: 190px 0; }
}

.skill-card {
  background: var(--glass-bg);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  border: 1px solid var(--glass-border);
  border-left: 3px solid var(--accent-purple);
  border-radius: var(--radius);
  padding: 9px 12px;
  transition: all 0.25s var(--ease-in-out);
  cursor: pointer;
  user-select: none;
  box-shadow: inset 0 1px 0 var(--glass-highlight);
}

.skill-card:hover {
  border-color: rgba(180, 150, 255, 0.25);
  border-left-color: var(--kh-c-secondary-purple-300);
  transform: translateX(2px);
  box-shadow: 0 0 12px rgba(180, 150, 255, 0.12), 0 2px 8px rgba(0, 0, 0, 0.15), inset 0 1px 0 var(--glass-highlight);
}

.skill-card.locked,
.skill-card.locked:hover {
  min-height: 36px;
  border-left-color: rgba(150, 150, 165, 0.35);
  border-color: rgba(150, 150, 165, 0.16);
  background: rgba(25, 25, 32, 0.48);
  box-shadow: inset 0 0 14px rgba(0, 0, 0, 0.25);
  cursor: default;
  transform: none;
}

.skill-lock {
  width: 18px;
  color: rgba(205, 205, 220, 0.72);
  font-size: 13px;
  line-height: 1;
  text-align: center;
  filter: grayscale(0.35);
}

.skill-card.expanded {
  box-shadow: 0 0 14px rgba(180, 150, 255, 0.15), inset 0 0 10px rgba(180, 150, 255, 0.05), 0 2px 8px rgba(0, 0, 0, 0.12);
  border-left-color: var(--accent-purple);
  border-color: rgba(180, 150, 255, 0.2);
}

.skill-card.jon-king-highlight {
  border-color: rgba(150, 225, 255, 0.72);
  border-left-color: #b9efff;
  box-shadow: 0 0 24px rgba(105, 205, 255, 0.48), inset 0 0 18px rgba(165, 232, 255, 0.12);
}

.skill-card.jon-king-highlight .skill-name {
  color: #e9fbff;
  text-shadow: 0 0 12px rgba(125, 220, 255, 0.9);
}

.skill-card.jon-king-highlight .skill-desc {
  color: #e5f8ff;
  border-bottom: 2px solid rgba(145, 225, 255, 0.9);
  box-shadow: inset 0 -8px 8px -8px rgba(100, 210, 255, 0.8);
  text-shadow: 0 0 8px rgba(120, 215, 255, 0.35);
  animation: jon-king-description-glow 1s ease-in-out infinite alternate;
}

.skill-card.deep-highlight {
  border-color: rgba(25, 194, 184, 0.72);
  border-left-color: #3ee6c8;
  background: linear-gradient(100deg, rgba(2, 24, 34, 0.95), rgba(3, 13, 24, 0.82));
  box-shadow: 0 0 24px rgba(25, 194, 184, 0.34), inset 0 0 18px rgba(62, 230, 200, 0.08);
}

.skill-card.deep-highlight .skill-name {
  color: #3ee6c8;
  text-shadow: 0 0 12px rgba(62, 230, 200, 0.72);
}

@keyframes jon-king-description-glow {
  from { border-bottom-color: rgba(145, 225, 255, 0.5); }
  to { border-bottom-color: #effcff; }
}

.skill-card.terminal-code-block,
.skill-card.terminal-code-block.expanded {
  position: relative;
  z-index: 1;
  margin: 0;
  padding: 9px 10px 10px;
  border: 0;
  border-bottom: 1px solid rgba(0, 255, 65, 0.13);
  border-left: 28px solid rgba(0, 255, 65, 0.035);
  border-radius: 0;
  background: transparent;
  box-shadow: none;
  backdrop-filter: none;
  cursor: default;
  user-select: text;
}
.skill-card.terminal-code-block:hover {
  transform: none;
  border-color: rgba(0, 255, 65, 0.13);
  border-left-color: rgba(0, 255, 65, 0.055);
  background: rgba(0, 255, 65, 0.025);
  box-shadow: inset 0 0 14px rgba(0, 255, 65, 0.025);
}
.terminal-code-block .skill-header-left { position: relative; }
.terminal-line-no {
  position: absolute;
  right: calc(100% + 16px);
  color: rgba(126, 255, 157, 0.24);
  font: 600 9px/1 var(--font-mono);
}
.terminal-code-block .skill-name {
  color: #6aff8d;
  font: 700 11px/1.35 var(--font-mono);
  letter-spacing: 0;
  text-shadow: 0 0 6px rgba(0, 255, 65, 0.52);
}
.terminal-code-copy {
  padding: 5px 0 0;
  color: #b6e8c2;
  font: 500 10px/1.55 var(--font-mono);
  white-space: pre-line;
}
.terminal-code-copy :deep(code) {
  display: inline-block;
  padding: 1px 4px;
  border: 1px solid rgba(0, 255, 65, 0.16);
  border-radius: 2px;
  background: rgba(0, 255, 65, 0.065);
  color: #d1ffdc;
  text-shadow: 0 0 5px rgba(0, 255, 65, 0.35);
}
.terminal-code-copy :deep(em) { color: #57baff; }
.terminal-code-copy :deep(strong) { color: #ffe977; }

.skill-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.skill-header-left {
  display: flex;
  align-items: center;
  gap: 6px;
}

.skill-header-right {
  display: flex;
  align-items: center;
  gap: 6px;
}

.skill-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  flex-shrink: 0;
  transition: box-shadow 0.3s;
}

.dot-active {
  background: var(--accent-green);
  box-shadow: 0 0 6px rgba(63, 167, 61, 0.5);
  animation: dot-glow 2s ease-in-out infinite;
}

@keyframes dot-glow {
  0%, 100% { box-shadow: 0 0 4px rgba(63, 167, 61, 0.4); }
  50% { box-shadow: 0 0 8px rgba(63, 167, 61, 0.7); }
}

.skill-name {
  font-weight: 800;
  font-size: 12px;
  color: var(--accent-gold);
  letter-spacing: 0.2px;
  text-shadow: 0 0 6px rgba(240, 200, 80, 0.15);
}

.skill-chevron {
  font-size: 11px;
  color: var(--text-dim);
  transition: transform 0.3s var(--ease-spring), color 0.2s;
  line-height: 1;
}

.chevron-open {
  transform: rotate(180deg);
  color: var(--accent-purple);
}

.skill-desc {
  font-size: 11px;
  line-height: 1.5;
  color: var(--text-secondary);
  padding-top: 6px;
  padding-left: 12px;
}

.skill-desc :deep(strong) { color: var(--text-primary); font-weight: 800; }
.skill-desc :deep(em) { color: var(--accent-blue); font-style: italic; }
.skill-desc :deep(u) { text-decoration-thickness: 1px; text-underline-offset: 2px; }

/* Expand transition */
.expand-enter-active {
  transition: all 0.35s var(--ease-spring);
  overflow: hidden;
}
.expand-leave-active {
  transition: all 0.2s ease;
  overflow: hidden;
}
.expand-enter-from,
.expand-leave-to {
  opacity: 0;
  max-height: 0;
  padding-top: 0;
}
.expand-enter-to,
.expand-leave-from {
  opacity: 1;
  max-height: 400px;
}

.no-skills {
  color: var(--text-dim);
  font-style: italic;
  text-align: center;
  padding: 20px;
  font-size: 11px;
}

@media (prefers-reduced-motion: reduce) {
  .skills-panel.is-terminal-editor::after,
  .dot-active,
  .skill-card.jon-king-highlight .skill-desc { animation: none; }
  .skill-card.terminal-code-block { transition: none; }
}
</style>

<style>
/* Global (un-scoped) — teleported VFX live on <body> */
.tb-projectile {
  position: fixed;
  left: 0;
  top: 0;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: radial-gradient(circle, #fff 0%, #ff5252 45%, rgba(255, 30, 30, 0) 75%);
  box-shadow: 0 0 18px 6px rgba(255, 60, 60, 0.7), 0 0 40px 12px rgba(255, 40, 40, 0.3);
  pointer-events: none;
  z-index: 3000;
}

.tb-ring {
  position: fixed;
  border: 3px solid #ff5252;
  border-radius: 10px;
  box-shadow: 0 0 18px rgba(255, 60, 60, 0.7), inset 0 0 18px rgba(255, 60, 60, 0.35);
  pointer-events: none;
  z-index: 2999;
  animation: tb-ring-draw 0.5s cubic-bezier(0.2, 0.9, 0.2, 1), tb-ring-pulse 1.1s ease-in-out 0.5s infinite;
}

.jon-king-ring {
  position: fixed;
  border: 3px solid #bcefff;
  border-radius: 10px;
  box-shadow:
    0 0 20px rgba(105, 215, 255, 0.92),
    inset 0 0 18px rgba(185, 240, 255, 0.48);
  pointer-events: none;
  z-index: 3130;
  animation: jon-king-ring-draw 0.55s cubic-bezier(0.2, 0.9, 0.2, 1),
    jon-king-ring-pulse 1s ease-in-out 0.55s infinite;
}
@keyframes jon-king-ring-draw {
  0% { transform: scale(1.35); opacity: 0; filter: blur(5px); }
  100% { transform: scale(1); opacity: 1; filter: blur(0); }
}
@keyframes jon-king-ring-pulse {
  0%, 100% { box-shadow: 0 0 14px rgba(105, 215, 255, 0.62), inset 0 0 14px rgba(185, 240, 255, 0.3); }
  50% { box-shadow: 0 0 32px rgba(165, 235, 255, 1), inset 0 0 25px rgba(205, 247, 255, 0.58); }
}
@keyframes tb-ring-draw {
  0% { transform: scale(1.35); opacity: 0; }
  100% { transform: scale(1); opacity: 1; }
}
@keyframes tb-ring-pulse {
  0%, 100% { box-shadow: 0 0 14px rgba(255, 60, 60, 0.5), inset 0 0 14px rgba(255, 60, 60, 0.25); }
  50% { box-shadow: 0 0 26px rgba(255, 80, 80, 0.9), inset 0 0 26px rgba(255, 80, 80, 0.45); }
}

.tb-unlock-overlay {
  position: fixed;
  inset: 0;
  z-index: 3100;
  display: flex;
  align-items: center;
  justify-content: center;
  background: radial-gradient(circle at 70% 50%, rgba(60, 0, 0, 0.55), rgba(0, 0, 0, 0.78));
  pointer-events: none;
}
.tb-unlock-overlay.is-combination {
  background: radial-gradient(circle at 70% 50%, rgba(48, 18, 4, 0.34), rgba(0, 0, 0, 0.56));
}
.tb-unlock-card {
  text-align: center;
  padding: 28px 44px;
  border: 2px solid rgba(255, 70, 70, 0.7);
  border-radius: 14px;
  background: linear-gradient(160deg, rgba(40, 0, 0, 0.92), rgba(15, 0, 0, 0.92));
  box-shadow: 0 0 60px rgba(255, 40, 40, 0.5), inset 0 0 30px rgba(255, 40, 40, 0.15);
  animation: tb-unlock-pop 0.5s cubic-bezier(0.2, 1.4, 0.4, 1);
}
.tb-unlock-overlay.is-combination .tb-unlock-card {
  padding: 20px 34px;
  border-color: rgba(255, 174, 90, 0.55);
  background: linear-gradient(160deg, rgba(42, 20, 3, 0.9), rgba(18, 8, 1, 0.9));
  box-shadow: 0 0 34px rgba(255, 145, 55, 0.32), inset 0 0 20px rgba(255, 145, 55, 0.1);
  animation-duration: 0.38s;
}
.tb-unlock-overlay.is-combination .tb-unlock-lock {
  font-size: 36px;
}
.tb-unlock-overlay.is-combination .tb-unlock-name {
  font-size: 24px;
  text-shadow: 0 0 12px rgba(255, 160, 70, 0.65);
}
.tb-unlock-overlay.is-combination .tb-unlock-label,
.tb-unlock-overlay.is-combination .tb-unlock-sub {
  color: #ffb067;
}
.tb-unlock-label {
  font-size: 12px;
  letter-spacing: 4px;
  color: #ff9a9a;
  font-weight: 800;
  animation: tb-unlock-reveal 0.4s ease 0.35s both;
}
.tb-unlock-lock {
  font-size: 46px;
  margin: 6px 0;
  animation: tb-unlock-bounce 0.6s ease 0.15s both;
}
.tb-unlock-name {
  font-size: 30px;
  font-weight: 900;
  color: #fff;
  text-shadow: 0 0 18px rgba(255, 60, 60, 0.9);
  animation: tb-unlock-reveal 0.4s ease 0.5s both;
}
.tb-unlock-sub {
  margin-top: 6px;
  font-size: 13px;
  letter-spacing: 6px;
  color: #ff5252;
  font-weight: 700;
  animation: tb-unlock-reveal 0.4s ease 0.65s both;
}
@keyframes tb-unlock-pop {
  0% { transform: scale(0.6); opacity: 0; }
  100% { transform: scale(1); opacity: 1; }
}
@keyframes tb-unlock-bounce {
  0% { transform: scale(0.2) rotate(-25deg); opacity: 0; }
  60% { transform: scale(1.3) rotate(8deg); }
  100% { transform: scale(1) rotate(0); opacity: 1; }
}
@keyframes tb-unlock-reveal {
  0% { transform: translateY(10px); opacity: 0; }
  100% { transform: translateY(0); opacity: 1; }
}
.tb-unlock-enter-active { transition: opacity 0.3s ease; }
.tb-unlock-leave-active { transition: opacity 0.5s ease; }
.tb-unlock-enter-from,
.tb-unlock-leave-to { opacity: 0; }

.jon-king-overlay {
  position: fixed;
  inset: 0;
  z-index: 3120;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  background:
    radial-gradient(circle at 50% 48%, rgba(20, 85, 120, 0.24), rgba(2, 15, 28, 0.78)),
    linear-gradient(180deg, rgba(185, 235, 255, 0.08), rgba(20, 65, 95, 0.18));
  box-shadow: inset 0 0 120px 28px rgba(160, 225, 255, 0.38);
  pointer-events: none;
}
.jon-king-overlay::before,
.jon-king-overlay::after {
  content: '';
  position: absolute;
  inset: -35vh -10vw 0;
  background-repeat: repeat;
  opacity: 0.9;
  animation: jon-king-snowfall 3.4s linear infinite;
}
.jon-king-overlay::before {
  background-image:
    radial-gradient(circle, rgba(255, 255, 255, 0.95) 0 2px, transparent 2.7px),
    radial-gradient(circle, rgba(205, 240, 255, 0.85) 0 1.5px, transparent 2.2px);
  background-position: 12px 8px, 54px 46px;
  background-size: 76px 76px, 112px 112px;
}
.jon-king-overlay::after {
  background-image:
    radial-gradient(circle, rgba(255, 255, 255, 0.78) 0 1px, transparent 1.8px),
    radial-gradient(circle, rgba(170, 225, 255, 0.7) 0 0.8px, transparent 1.5px);
  background-position: 20px 30px, 75px 5px;
  background-size: 48px 48px, 87px 87px;
  opacity: 0.62;
  animation-duration: 5.2s;
  animation-direction: reverse;
}
.jon-king-announcement {
  position: relative;
  z-index: 1;
  min-width: min(430px, calc(100vw - 40px));
  padding: 28px 42px;
  border: 2px solid rgba(190, 240, 255, 0.84);
  border-radius: 16px;
  background: linear-gradient(160deg, rgba(15, 62, 88, 0.88), rgba(3, 25, 43, 0.94));
  box-shadow: 0 0 70px rgba(120, 220, 255, 0.62), inset 0 0 34px rgba(180, 238, 255, 0.2);
  text-align: center;
  animation: jon-king-card-arrive 0.65s cubic-bezier(0.18, 1.35, 0.35, 1);
}
.jon-king-label {
  color: #bcefff;
  font-size: 12px;
  font-weight: 900;
  letter-spacing: 4px;
}
.jon-king-sigil {
  margin: 8px 0 3px;
  font-size: 48px;
  filter: drop-shadow(0 0 14px rgba(190, 245, 255, 0.95));
  animation: jon-king-sigil-spin 1.8s ease-in-out infinite alternate;
}
.jon-king-name {
  color: #f3fdff;
  font-size: clamp(26px, 5vw, 38px);
  font-weight: 950;
  text-shadow: 0 0 22px rgba(140, 225, 255, 0.95);
}
.jon-king-sub {
  margin-top: 7px;
  color: #8ed9f3;
  font-size: 13px;
  font-weight: 800;
  letter-spacing: 6px;
}
@keyframes jon-king-snowfall {
  from { transform: translate3d(-2vw, -25vh, 0); }
  to { transform: translate3d(7vw, 70vh, 0); }
}
@keyframes jon-king-card-arrive {
  0% { transform: scale(0.72); opacity: 0; filter: blur(8px); }
  100% { transform: scale(1); opacity: 1; filter: blur(0); }
}
@keyframes jon-king-sigil-spin {
  from { transform: rotate(-10deg) scale(0.92); }
  to { transform: rotate(10deg) scale(1.08); }
}
.jon-king-unlock-enter-active { transition: opacity 0.35s ease; }
.jon-king-unlock-leave-active { transition: opacity 0.7s ease; }
.jon-king-unlock-enter-from,
.jon-king-unlock-leave-to { opacity: 0; }

@media (prefers-reduced-motion: reduce) {
  .jon-king-ring,
  .jon-king-overlay::before,
  .jon-king-overlay::after,
  .jon-king-announcement,
  .jon-king-sigil { animation: none; }
}
</style>
