<script setup lang="ts">
import { nextTick, ref } from 'vue'

export type BattleshipProjectileKind = 'arrow' | 'stone' | 'buckshot' | 'fire' | 'electric' | 'cannon'

type ProjectileView = {
  id: number
  kind: BattleshipProjectileKind
}

const projectiles = ref<ProjectileView[]>([])
let nextId = 1

function cellCenter(stage: HTMLElement | null, row: number, col: number): { x: number; y: number } | null {
  const cell = stage?.querySelector<HTMLElement>(`.cell[data-row="${row}"][data-col="${col}"]`)
  if (!cell) return null
  const rect = cell.getBoundingClientRect()
  return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 }
}

function removeProjectile(id: number) {
  projectiles.value = projectiles.value.filter(projectile => projectile.id !== id)
}

function fire(
  sourceStage: HTMLElement | null,
  targetStage: HTMLElement | null,
  sourceRow: number,
  sourceCol: number,
  targetRow: number,
  targetCol: number,
  kind: BattleshipProjectileKind,
  onImpact: () => void,
  durationMs?: number,
): boolean {
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return false
  const target = cellCenter(targetStage, targetRow, targetCol)
  if (!target) return false

  const source = cellCenter(sourceStage, sourceRow, sourceCol) ?? {
    // Hidden enemy weapon positions stay private: enter from outside the visible target board.
    x: target.x,
    y: Math.max(12, target.y - 240),
  }
  const id = nextId++
  projectiles.value.push({ id, kind })

  void nextTick(() => {
    const element = document.querySelector<HTMLElement>(`[data-bs-projectile="${id}"]`)
    if (!element) {
      removeProjectile(id)
      onImpact()
      return
    }

    const dx = target.x - source.x
    const dy = target.y - source.y
    const angle = Math.atan2(dy, dx) * 180 / Math.PI
    const arc = Math.min(source.y, target.y) - Math.max(70, Math.abs(dx) * 0.18)
    const midX = source.x + dx * 0.52
    const spin = kind === 'stone' || kind === 'buckshot' || kind === 'cannon' ? 300 : 0
    const animation = element.animate([
      { transform: `translate3d(${source.x}px, ${source.y}px, 0) rotate(${angle}deg) scale(.78)`, opacity: 0 },
      { transform: `translate3d(${midX}px, ${arc}px, 0) rotate(${angle + spin * 0.5}deg) scale(1.08)`, opacity: 1, offset: 0.5 },
      { transform: `translate3d(${target.x}px, ${target.y}px, 0) rotate(${angle + spin}deg) scale(.9)`, opacity: 1 },
    ], {
      duration: durationMs ?? (kind === 'arrow' ? 430 : 520),
      easing: 'cubic-bezier(.22,.72,.22,1)',
      fill: 'forwards',
    })

    animation.onfinish = () => {
      removeProjectile(id)
      onImpact()
    }
    animation.oncancel = () => removeProjectile(id)
  })
  return true
}

defineExpose({ fire })
</script>

<template>
  <Teleport to="body">
    <div class="projectile-layer" aria-hidden="true">
      <div
        v-for="projectile in projectiles"
        :key="projectile.id"
        class="projectile"
        :class="`projectile--${projectile.kind}`"
        :data-bs-projectile="projectile.id"
      >
        <span v-if="projectile.kind === 'buckshot'" v-for="pellet in 5" :key="pellet" class="projectile-pellet" />
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.projectile-layer {
  position: fixed;
  inset: 0;
  pointer-events: none;
  z-index: 2000;
  overflow: hidden;
}

.projectile {
  position: absolute;
  left: -10px;
  top: -10px;
  transform-origin: center;
  filter: drop-shadow(0 2px 3px rgba(0, 0, 0, .72));
  will-change: transform, opacity;
}

.projectile--arrow {
  width: 28px;
  height: 4px;
  border-radius: 3px;
  background: linear-gradient(90deg, #5a331d 0 68%, #d7dde7 68% 84%, transparent 84%);
}
.projectile--arrow::after {
  content: '';
  position: absolute;
  right: -5px;
  top: -3px;
  border-left: 7px solid #d7dde7;
  border-top: 5px solid transparent;
  border-bottom: 5px solid transparent;
}

.projectile--stone {
  width: 15px;
  height: 15px;
  border-radius: 48% 55% 45% 52%;
  background: radial-gradient(circle at 32% 28%, #f2f0e8, #a9a69e 52%, #595852 100%);
}

.projectile--cannon {
  width: 17px;
  height: 17px;
  border: 1px solid #64748b;
  border-radius: 50%;
  background: radial-gradient(circle at 30% 25%, #cbd5e1 0 8%, #475569 30%, #111827 72%, #020617 100%);
  box-shadow: 0 0 8px rgba(248, 250, 252, 0.24);
}
.projectile--cannon::after {
  content: '';
  position: absolute;
  right: 12px;
  top: 6px;
  width: 26px;
  height: 4px;
  border-radius: 999px;
  background: linear-gradient(90deg, transparent, rgba(226, 232, 240, 0.55));
  filter: blur(1px);
}

.projectile--fire {
  width: 17px;
  height: 17px;
  border-radius: 58% 42% 60% 40%;
  background: radial-gradient(circle at 35% 35%, #fff8a6, #ff9f2f 42%, #d62d20 76%, transparent 78%);
  box-shadow: 0 0 12px rgba(255, 111, 32, .85);
}

.projectile--electric {
  width: 26px;
  height: 8px;
  border-radius: 50%;
  background: linear-gradient(90deg, transparent, #99f6e4 22% 78%, transparent);
  box-shadow: 0 0 7px #2dd4bf, 0 0 15px rgba(34, 211, 238, 0.85);
}
.projectile--electric::before,
.projectile--electric::after {
  content: '';
  position: absolute;
  inset: 2px -4px;
  border-top: 2px solid #67e8f9;
  transform: skewX(-28deg) rotate(7deg);
}
.projectile--electric::after {
  transform: skewX(28deg) rotate(-8deg);
  opacity: 0.72;
}

.projectile--buckshot {
  width: 22px;
  height: 22px;
}
.projectile-pellet {
  position: absolute;
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: radial-gradient(circle at 30% 30%, #e9e6dc, #74716a 68%, #3d3b37);
}
.projectile-pellet:nth-child(1) { left: 7px; top: 0; }
.projectile-pellet:nth-child(2) { left: 0; top: 7px; }
.projectile-pellet:nth-child(3) { right: 0; top: 7px; }
.projectile-pellet:nth-child(4) { left: 3px; bottom: 0; }
.projectile-pellet:nth-child(5) { right: 3px; bottom: 0; }
</style>
