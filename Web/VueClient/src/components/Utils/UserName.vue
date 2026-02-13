<template>
  <div class="username">{{ name }}</div>
</template>

<script setup lang="ts">
defineProps<{
  name?: string
}>()
</script>

<style scoped>
.username {
  color: var(--kh-c-text-primary-500);
}
</style>
