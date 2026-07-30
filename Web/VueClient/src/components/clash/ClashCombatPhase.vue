<script setup lang="ts">
import { computed } from 'vue'
import { useClashStore } from 'src/store/clash'
import { clashResolutionIdentity } from 'src/features/clash/visuals'
import ClashActionTimeline from './ClashActionTimeline.vue'
import ClashBoard from './ClashBoard.vue'

const store = useClashStore()
const state = computed(() => store.gameState)
const resolution = computed(() => store.activeResolution ?? state.value?.latestResolution ?? null)
const resolutionIdentity = computed(() => {
  if (!resolution.value) return 'waiting'
  return clashResolutionIdentity(
    resolution.value.gameId,
    resolution.value.revision,
    resolution.value.clashNumber,
  )
})
</script>

<template>
  <section v-if="state" class="clash-phase clash-combat">
    <header class="clash-phase__header">
      <div>
        <span class="clash-eyebrow">Клэш {{ state.clashNumber }}</span>
        <h1>Армии вступили в бой</h1>
        <p>Быстрые юниты действуют первыми. Урон применяется точно в момент удара.</p>
      </div>
      <div class="clash-speed-legend" aria-label="Порядок скорости">
        <span v-for="speed in [9, 7, 5, 3, 1]" :key="speed" :style="{ '--speed': speed }">
          {{ speed }}
        </span>
      </div>
    </header>

    <div class="clash-combat__layout">
      <ClashBoard
        :width="state.width"
        :length="state.length"
        :cells="store.boardCells"
        :catalog-by-id="store.catalogById"
        :viewer="store.myPlayer"
        :visual-overrides="store.visualOverrides"
        label="Клэш"
      />

      <ClashActionTimeline
        v-if="resolution"
        :identity="resolutionIdentity"
        :events="resolution.events"
        :duration-ms="resolution.durationMs"
        :started-at-utc="resolution.startedAtUtc"
        @start="store.startTimelineEvent"
        @impact="store.impactTimelineEvent"
        @complete="store.finishTimeline"
      />
      <aside v-else class="clash-timeline clash-empty">
        Синхронизируем действия армий…
      </aside>
    </div>
  </section>
</template>
