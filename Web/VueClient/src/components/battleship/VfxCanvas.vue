<script setup lang="ts">
import { onMounted, onUnmounted, watch } from 'vue'
import { useVfx } from '@/composables/useVfx'

const props = withDefaults(
  defineProps<{
    boardWidth: number
    boardHeight: number
    cellSize?: number
  }>(),
  { cellSize: 42 },
)

const LABEL_OFFSET = 24

const vfx = useVfx()
const canvasRef = vfx.canvasRef

function initVfx() {
  vfx.init(props.cellSize, LABEL_OFFSET, LABEL_OFFSET)
}

onMounted(() => {
  initVfx()
})

onUnmounted(() => {
  vfx.destroy()
})

watch(
  () => [props.boardWidth, props.boardHeight],
  () => {
    initVfx()
  },
)

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
})
</script>

<template>
  <canvas
    ref="canvasRef"
    class="vfx-canvas"
    :width="boardWidth"
    :height="boardHeight"
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
