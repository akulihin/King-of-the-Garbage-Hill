<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import type { ClashResolutionEvent } from 'src/features/clash/types'
import {
  clashEventIcon,
  clashResolutionElapsedMs,
  prefersReducedClashMotion,
} from 'src/features/clash/visuals'

const props = withDefaults(defineProps<{
  identity: string
  events: ClashResolutionEvent[]
  durationMs: number
  startedAtUtc: string
}>(), {
  events: () => [],
  durationMs: 0,
  startedAtUtc: '',
})

const emit = defineEmits<{
  start: [event: ClashResolutionEvent]
  impact: [event: ClashResolutionEvent]
  complete: []
}>()

const activeSequences = ref(new Set<number>())
const completedSequences = ref(new Set<number>())
const progress = ref(0)
const elapsedMs = ref(0)
const timers = new Set<ReturnType<typeof setTimeout>>()
let progressTimer: ReturnType<typeof setInterval> | null = null
let playGeneration = 0

const visibleEvents = computed(() => props.events.slice(-12))

function clearPlayback() {
  for (const timer of timers) clearTimeout(timer)
  timers.clear()
  if (progressTimer) {
    clearInterval(progressTimer)
    progressTimer = null
  }
  activeSequences.value = new Set()
  completedSequences.value = new Set()
  progress.value = 0
  elapsedMs.value = 0
}

function schedule(delay: number, callback: () => void) {
  const timer = setTimeout(() => {
    timers.delete(timer)
    callback()
  }, Math.max(0, delay))
  timers.add(timer)
}

function play() {
  clearPlayback()
  const generation = ++playGeneration
  if (props.events.length === 0) {
    emit('complete')
    return
  }

  if (prefersReducedClashMotion()) {
    for (const event of props.events) {
      emit('start', event)
      emit('impact', event)
    }
    completedSequences.value = new Set(props.events.map(event => event.sequence))
    progress.value = 1
    elapsedMs.value = props.durationMs
    emit('complete')
    return
  }

  const startedAt = performance.now()
  const fallbackEnd = Math.max(
    ...props.events.map(event => Math.max(event.startOffsetMs, event.impactOffsetMs)),
    0,
  ) + 500
  const totalDuration = Math.max(350, props.durationMs, fallbackEnd)
  const initialElapsed = Math.min(
    totalDuration,
    clashResolutionElapsedMs(props.startedAtUtc),
  )

  progressTimer = setInterval(() => {
    elapsedMs.value = Math.min(
      totalDuration,
      initialElapsed + performance.now() - startedAt,
    )
    progress.value = elapsedMs.value / totalDuration
  }, 50)

  for (const event of props.events) {
    const impactOffset = Math.max(event.startOffsetMs, event.impactOffsetMs)
    if (event.startOffsetMs <= initialElapsed) {
      activeSequences.value = new Set(activeSequences.value).add(event.sequence)
      emit('start', event)
    }
    else {
      schedule(event.startOffsetMs - initialElapsed, () => {
        if (generation !== playGeneration) return
        activeSequences.value = new Set(activeSequences.value).add(event.sequence)
        emit('start', event)
      })
    }

    if (impactOffset <= initialElapsed) {
      const active = new Set(activeSequences.value)
      active.delete(event.sequence)
      activeSequences.value = active
      completedSequences.value = new Set(completedSequences.value).add(event.sequence)
      emit('impact', event)
    }
    else {
      schedule(impactOffset - initialElapsed, () => {
        if (generation !== playGeneration) return
        const active = new Set(activeSequences.value)
        active.delete(event.sequence)
        activeSequences.value = active
        completedSequences.value = new Set(completedSequences.value).add(event.sequence)
        emit('impact', event)
      })
    }
  }

  if (initialElapsed >= totalDuration) {
    progress.value = 1
    elapsedMs.value = totalDuration
    if (progressTimer) {
      clearInterval(progressTimer)
      progressTimer = null
    }
    emit('complete')
    return
  }

  elapsedMs.value = initialElapsed
  progress.value = initialElapsed / totalDuration
  schedule(totalDuration - initialElapsed, () => {
    if (generation !== playGeneration) return
    progress.value = 1
    elapsedMs.value = totalDuration
    if (progressTimer) {
      clearInterval(progressTimer)
      progressTimer = null
    }
    emit('complete')
  })
}

watch(() => props.identity, play, { immediate: true })
onBeforeUnmount(clearPlayback)
</script>

<template>
  <aside class="clash-timeline" aria-live="polite">
    <header class="clash-timeline__header">
      <div>
        <span class="clash-eyebrow">Хронология клэша</span>
        <strong>{{ Math.round(elapsedMs / 100) / 10 }} сек.</strong>
      </div>
      <div
        class="clash-timeline__progress"
        role="progressbar"
        aria-label="Ход разрешения клэша"
        :aria-valuenow="Math.round(progress * 100)"
        aria-valuemin="0"
        aria-valuemax="100"
      >
        <span :style="{ width: `${progress * 100}%` }" />
      </div>
    </header>
    <ol class="clash-timeline__events">
      <li
        v-for="event in visibleEvents"
        :key="event.sequence"
        :class="{
          'is-active': activeSequences.has(event.sequence),
          'is-complete': completedSequences.has(event.sequence),
        }"
      >
        <span class="clash-timeline__icon" aria-hidden="true">{{ clashEventIcon(event) }}</span>
        <span class="clash-timeline__copy">
          <strong>Скорость {{ event.speed }}</strong>
          <span>{{ event.message || event.type }}</span>
        </span>
        <time>{{ (event.impactOffsetMs / 1000).toFixed(1) }}с</time>
      </li>
    </ol>
  </aside>
</template>
