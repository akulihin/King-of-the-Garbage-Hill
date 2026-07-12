<script setup lang="ts">
import { nextTick, onMounted, onUnmounted } from 'vue'
import { useVfx } from 'src/composables/useVfx'

/**
 * Particle/projectile canvas overlaying one board. Mount it inside a
 * position:relative `.board-stage` that wraps a BoardGrid: the canvas sizes
 * itself to the stage (ResizeObserver) and derives the cell origin/pitch from
 * the real bounding rects of cells (0,0) and (1,1), so grid gaps, borders and
 * responsive cell sizes are all accounted for.
 */
const vfx = useVfx()
const canvasRef = vfx.canvasRef

let resizeObserver: ResizeObserver | null = null

function measure() {
  const canvas = canvasRef.value
  const stage = canvas?.parentElement
  if (!canvas || !stage) return

  canvas.width = stage.clientWidth
  canvas.height = stage.clientHeight

  const rows = stage.querySelectorAll<HTMLElement>('.grid-row:not(.label-row)')
  const cell00 = rows[0]?.querySelector<HTMLElement>('.cell')
  const cell11 = rows[1]?.querySelectorAll<HTMLElement>('.cell')[1]
  if (!cell00) return

  const stageRect = stage.getBoundingClientRect()
  const rect00 = cell00.getBoundingClientRect()
  const originX = rect00.left - stageRect.left + rect00.width / 2
  const originY = rect00.top - stageRect.top + rect00.height / 2

  let pitchX = rect00.width + 1
  let pitchY = rect00.height + 1
  if (cell11) {
    const rect11 = cell11.getBoundingClientRect()
    pitchX = (rect11.left + rect11.width / 2) - (rect00.left + rect00.width / 2)
    pitchY = (rect11.top + rect11.height / 2) - (rect00.top + rect00.height / 2)
  }

  vfx.init({ originX, originY, pitchX, pitchY })
}

onMounted(async () => {
  await nextTick()
  measure()
  const stage = canvasRef.value?.parentElement
  if (stage) {
    resizeObserver = new ResizeObserver(() => measure())
    resizeObserver.observe(stage)
  }
})

onUnmounted(() => {
  resizeObserver?.disconnect()
  resizeObserver = null
  vfx.destroy()
})

function fireCannonball(targetRow: number, targetCol: number, onImpact?: () => void): void {
  vfx.spawnCannonball(targetRow, targetCol, onImpact)
}

function spawnImpact(
  row: number,
  col: number,
  type: 'hit' | 'miss' | 'burn' | 'sunk' | 'destroy' | 'scratch' | 'freeze',
): void {
  vfx.spawnImpact(row, col, type)
}

function spawnConfetti(count?: number): void {
  vfx.spawnConfetti(count)
}

function spawnWake(
  row: number,
  col: number,
  direction: 'up' | 'down' | 'left' | 'right',
): void {
  vfx.spawnWake(row, col, direction)
}

defineExpose({
  fireCannonball,
  spawnImpact,
  spawnConfetti,
  spawnWake,
  remeasure: measure,
})
</script>

<template>
  <canvas
    ref="canvasRef"
    class="vfx-canvas"
    aria-hidden="true"
  />
</template>

<style scoped>
.vfx-canvas {
  position: absolute;
  top: 0;
  left: 0;
  pointer-events: none;
  z-index: 10;
}
</style>
