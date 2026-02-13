<template>
  <div class="version">kotgh-{{ version }}</div>
</template>

<script setup lang="ts">
defineProps<{
  version?: string
}>()
</script>

<style scoped>
.version {
  color: var(--kh-c-text-primary-700);
  font-size: 0.75rem;
}
</style>
