<script setup lang="ts">
import { onMounted } from 'vue'
import FortressOfDoom from 'src/components/Home/FortressOfDoom.vue'
import { useGameStore } from 'src/store/game'

const store = useGameStore()

onMounted(() => {
  if (store.isAuthenticated) void store.requestDoomFortress()
})
</script>

<template>
  <div class="fortress-page">
    <FortressOfDoom />
  </div>
</template>

<style scoped>
.fortress-page {
  width: 100%;
  max-width: 78rem;
  margin: 0 auto;
  padding: 1rem;
}
</style>
