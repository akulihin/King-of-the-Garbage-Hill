<script setup lang="ts">
import { ArrowLeft } from 'lucide-vue-next'
import AchievementBoard from 'src/components/AchievementBoard.vue'
import { currentLocale } from 'src/i18n'

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}
</script>

<template>
  <div class="achievements-page">
    <RouterLink class="back-link" to="/games">
      <ArrowLeft :size="16" aria-hidden="true" />
      {{ t('Back to Lobby', 'Назад в лобби') }}
    </RouterLink>
    <AchievementBoard />
  </div>
</template>

<style scoped>
.achievements-page { width: 100%; }
.back-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 36px;
  margin-bottom: 10px;
  padding: 4px 9px;
  color: var(--text-muted);
  border-radius: 7px;
  text-decoration: none;
  font-size: 11px;
  font-weight: 750;
}
.back-link:hover { color: var(--text-primary); background: rgba(255, 255, 255, 0.04); }

@media (max-width: 620px) {
  .back-link { min-height: 44px; }
}
</style>
