<script setup lang="ts">
import { computed } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'
import FleetBuilder from '../FleetBuilder.vue'
import FlintFleetBuilder from '../FlintFleetBuilder.vue'

const store = useBattleshipStore()
const isCaptainFlint = computed(() => store.myPlayer?.faction === 'CaptainFlint')
</script>

<template>
  <div class="phase-content">
    <FlintFleetBuilder v-if="isCaptainFlint" />
    <FleetBuilder v-else />
  </div>
</template>

<style scoped>
.phase-content { margin-top: 0.5rem; }
</style>
