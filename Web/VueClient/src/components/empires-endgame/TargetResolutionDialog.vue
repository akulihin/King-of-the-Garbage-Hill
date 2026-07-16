<script setup lang="ts">
import { AlertTriangle, MapPin, Sparkles } from 'lucide-vue-next'

export interface TargetResolutionOption {
  id: string
  name: string
  regionName?: string
  summary?: string
  preview?: string[]
  disabled?: boolean
  disabledReason?: string
}

withDefaults(defineProps<{
  title: string
  description: string
  prompt?: string
  options: TargetResolutionOption[]
}>(), {
  prompt: 'Выберите город, к которому будет применён эффект.',
})

const emit = defineEmits<{
  choose: [targetId: string]
}>()
</script>

<template>
  <div class="target-dialog-backdrop">
    <section
      class="target-dialog"
      role="dialog"
      aria-modal="true"
      aria-labelledby="target-dialog-title"
      aria-describedby="target-dialog-description"
      data-testid="target-resolution-dialog"
    >
      <header>
        <span class="target-dialog-sigil"><Sparkles :size="22" /></span>
        <div>
          <span>Нужно указать цель</span>
          <h2 id="target-dialog-title">{{ title }}</h2>
        </div>
      </header>

      <p id="target-dialog-description" class="target-description">{{ description }}</p>
      <p class="target-prompt">{{ prompt }}</p>

      <div class="target-list" role="list">
        <button
          v-for="option in options"
          :key="option.id"
          :data-testid="`target-city-${option.id}`"
          type="button"
          :disabled="option.disabled"
          :aria-describedby="option.disabledReason ? `target-reason-${option.id}` : undefined"
          @click="emit('choose', option.id)"
        >
          <span class="target-icon"><MapPin :size="19" /></span>
          <span class="target-copy">
            <strong>{{ option.name }}</strong>
            <small v-if="option.regionName">{{ option.regionName }}</small>
            <span v-if="option.summary">{{ option.summary }}</span>
            <ul v-if="option.preview?.length">
              <li v-for="line in option.preview" :key="line">{{ line }}</li>
            </ul>
            <em
              v-if="option.disabledReason"
              :id="`target-reason-${option.id}`"
            ><AlertTriangle :size="12" />{{ option.disabledReason }}</em>
          </span>
        </button>
      </div>

      <p v-if="!options.length" class="target-empty" role="status">
        <AlertTriangle :size="17" />
        Нет доступных городов. Эффект нельзя завершить.
      </p>
    </section>
  </div>
</template>

<style scoped>
.target-dialog-backdrop {
  position: fixed;
  z-index: 120;
  inset: 0;
  display: grid;
  place-items: center;
  padding: 20px;
  background: rgba(5, 7, 5, 0.78);
  backdrop-filter: blur(8px);
}

.target-dialog {
  display: grid;
  width: min(720px, 100%);
  max-height: min(820px, calc(100dvh - 40px));
  overflow: auto;
  padding: 22px;
  border: 1px solid rgba(226, 196, 132, 0.34);
  border-radius: 16px;
  color: #efe5d0;
  background:
    radial-gradient(circle at 75% 0, rgba(201, 168, 94, 0.12), transparent 38%),
    #171a14;
  box-shadow: 0 30px 90px rgba(0, 0, 0, 0.62);
}

.target-dialog > header {
  display: flex;
  align-items: center;
  gap: 12px;
}

.target-dialog-sigil {
  display: grid;
  width: 48px;
  height: 48px;
  flex: 0 0 auto;
  place-items: center;
  border: 1px solid rgba(224, 190, 117, 0.4);
  border-radius: 50%;
  color: #e2c475;
  background: rgba(202, 169, 96, 0.09);
}

.target-dialog header div > span {
  color: #c4a861;
  font: 800 0.58rem/1 var(--font-mono, monospace);
  letter-spacing: 0.13em;
  text-transform: uppercase;
}

.target-dialog h2 {
  margin: 6px 0 0;
  font: 700 clamp(1.35rem, 3vw, 1.9rem)/1.05 Georgia, serif;
}

.target-description {
  margin: 16px 0 0;
  color: rgba(239, 229, 208, 0.68);
  font-size: 0.78rem;
  line-height: 1.55;
}

.target-prompt {
  margin: 13px 0 9px;
  color: #e7d29c;
  font-size: 0.72rem;
  font-weight: 800;
}

.target-list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.target-list > button {
  display: grid;
  min-width: 0;
  grid-template-columns: 38px minmax(0, 1fr);
  align-items: start;
  gap: 10px;
  padding: 12px;
  border: 1px solid rgba(226, 204, 158, 0.15);
  border-radius: 10px;
  color: #eee3cc;
  text-align: left;
  background: rgba(255, 255, 255, 0.028);
  cursor: pointer;
  transition: border-color 140ms ease, background 140ms ease, transform 140ms ease;
}

.target-list > button:hover:not(:disabled),
.target-list > button:focus-visible:not(:disabled) {
  border-color: rgba(232, 198, 125, 0.62);
  outline: none;
  background: rgba(201, 168, 94, 0.09);
  transform: translateY(-1px);
}

.target-list > button:disabled {
  color: rgba(238, 227, 204, 0.42);
  background: rgba(90, 63, 57, 0.07);
  cursor: not-allowed;
}

.target-icon {
  display: grid;
  width: 38px;
  height: 38px;
  place-items: center;
  border-radius: 9px;
  color: #e0c278;
  background: rgba(201, 168, 94, 0.1);
}

button:disabled .target-icon {
  color: #b4776c;
  background: rgba(155, 76, 63, 0.1);
}

.target-copy {
  display: grid;
  min-width: 0;
  gap: 4px;
}

.target-copy strong {
  overflow: hidden;
  font: 700 0.88rem/1.1 Georgia, serif;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.target-copy small {
  color: #bda66f;
  font: 700 0.55rem/1 var(--font-mono, monospace);
  text-transform: uppercase;
}

.target-copy > span {
  color: rgba(239, 229, 208, 0.58);
  font-size: 0.64rem;
  line-height: 1.4;
}

.target-copy ul {
  display: grid;
  gap: 2px;
  margin: 3px 0 0;
  padding-left: 15px;
  color: #d8c28c;
  font-size: 0.59rem;
  line-height: 1.35;
}

.target-copy em {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  margin-top: 3px;
  color: #d68e82;
  font-size: 0.59rem;
  font-style: normal;
  line-height: 1.35;
}

.target-empty {
  display: flex;
  align-items: center;
  gap: 7px;
  margin: 10px 0 0;
  padding: 12px;
  border: 1px solid rgba(186, 87, 73, 0.28);
  border-radius: 9px;
  color: #e0a39a;
  background: rgba(134, 51, 41, 0.1);
  font-size: 0.7rem;
}

@media (max-width: 620px) {
  .target-dialog-backdrop { padding: 10px; }
  .target-dialog { max-height: calc(100dvh - 20px); padding: 16px; }
  .target-list { grid-template-columns: 1fr; }
}
</style>
