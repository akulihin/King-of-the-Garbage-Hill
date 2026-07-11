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

function toggleSkill(idx: number) {
  if (expandedSet.value.has(idx)) {
    expandedSet.value.delete(idx)
  } else {
    expandedSet.value.add(idx)
  }
  expandedSet.value = new Set(expandedSet.value)
}

function isExpanded(idx: number): boolean {
  return expandedSet.value.has(idx)
}

function passiveIndexByName(name: string): number {
  return props.player.character.passives.findIndex((p) => p.name === name)
}

// ── Cinematic VFX state ───────────────────────────────────────────────
const projectileEl = ref<HTMLElement | null>(null)
const flyActive = ref(false)
const ring = ref<{ x: number; y: number; w: number; h: number } | null>(null)
const unlockOverlay = ref<{ name: string } | null>(null)

function showRing(rect: DOMRect, ttl = 2200) {
  ring.value = { x: rect.left - 6, y: rect.top - 6, w: rect.width + 12, h: rect.height + 12 }
  window.setTimeout(() => {
    ring.value = null
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
async function triggerUnlockVfx(name: string, idx: number) {
  unlockOverlay.value = { name }
  playTheBoysUnlock()
  window.setTimeout(() => {
    unlockOverlay.value = null
  }, 3200)
  if (idx >= 0) {
    expandedSet.value.add(idx)
    expandedSet.value = new Set(expandedSet.value)
    await nextTick()
    const el = skillCardRefs.value[idx]
    if (el) showRing(el.getBoundingClientRect(), 3200)
  }
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
    const name = theBoys.value?.lastUnlockedUltimate ?? ''
    void triggerUnlockVfx(name, passiveIndexByName(name))
  },
)
</script>

<template>
  <div class="skills-panel">
    <div
      v-for="(passive, idx) in player.character.passives"
      :key="idx"
      :ref="(el) => { skillCardRefs[idx] = el as HTMLElement | null }"
      class="skill-card"
      :class="{ expanded: isExpanded(idx) }"
      @click="toggleSkill(idx)"
    >
      <div class="skill-header">
        <div class="skill-header-left">
          <span class="skill-dot dot-active" />
          <span class="skill-name">{{ passive.name }}</span>
        </div>
        <div class="skill-header-right">
          <span class="skill-chevron" :class="{ 'chevron-open': isExpanded(idx) }">▾</span>
        </div>
      </div>
      <Transition name="expand">
        <div v-if="isExpanded(idx)" class="skill-desc" v-html="formatPassiveDescription(translateText(passive.description))" />
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
    <Transition name="tb-unlock">
      <div v-if="unlockOverlay" class="tb-unlock-overlay">
        <div class="tb-unlock-card">
          <div class="tb-unlock-label">СПОСОБНОСТЬ ОТКРЫТА</div>
          <div class="tb-unlock-lock">🔓</div>
          <div class="tb-unlock-name">{{ unlockOverlay.name }}</div>
          <div class="tb-unlock-sub">The Boys</div>
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

.skill-card.expanded {
  box-shadow: 0 0 14px rgba(180, 150, 255, 0.15), inset 0 0 10px rgba(180, 150, 255, 0.05), 0 2px 8px rgba(0, 0, 0, 0.12);
  border-left-color: var(--accent-purple);
  border-color: rgba(180, 150, 255, 0.2);
}

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
.tb-unlock-card {
  text-align: center;
  padding: 28px 44px;
  border: 2px solid rgba(255, 70, 70, 0.7);
  border-radius: 14px;
  background: linear-gradient(160deg, rgba(40, 0, 0, 0.92), rgba(15, 0, 0, 0.92));
  box-shadow: 0 0 60px rgba(255, 40, 40, 0.5), inset 0 0 30px rgba(255, 40, 40, 0.15);
  animation: tb-unlock-pop 0.5s cubic-bezier(0.2, 1.4, 0.4, 1);
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
</style>
