<script setup lang="ts">
defineProps<{
  logs: string
  title: string
}>()

function formatLogs(text: string): string {
  return text
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/__(.*?)__/g, '<u>$1</u>')
    .replace(/\*(.*?)\*/g, '<em>$1</em>')
    .replace(/~~(.*?)~~/g, '<del>$1</del>')
    .replace(/\|>Phrase<\|/g, '')
    .replace(/\n/g, '<br>')
}
</script>

<template>
  <div class="game-log card">
    <div class="card-header">
      {{ title }}
    </div>
    <div
      v-if="logs"
      class="log-content"
      v-html="formatLogs(logs)"
    />
    <div v-else class="log-empty">
      No logs yet this round.
    </div>
  </div>
</template>

<style scoped>
.log-content {
  font-size: 13px;
  line-height: 1.7;
  color: var(--text-secondary);
  max-height: 500px;
  overflow-y: auto;
  padding: 8px;
  background: var(--bg-primary);
  border-radius: var(--radius);
  font-family: var(--font-mono);
}

.log-content :deep(strong) {
  color: var(--accent-gold);
}

.log-content :deep(em) {
  color: var(--accent-blue);
}

.log-content :deep(u) {
  color: var(--accent-green);
}

.log-content :deep(del) {
  color: var(--text-muted);
  text-decoration: line-through;
}

.log-empty {
  color: var(--text-muted);
  font-style: italic;
  padding: 20px;
  text-align: center;
}
</style>
