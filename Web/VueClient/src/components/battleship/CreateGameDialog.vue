<script setup lang="ts">
import { ref } from 'vue'
import { useFocusTrapDialog } from 'src/composables/useFocusTrapDialog'
import { message } from 'src/platform/localization'
import type { BattleshipBotVersion } from 'src/services/signalr'

const props = defineProps<{
  isCreating: boolean
  errorMessage: string | null
}>()

const emit = defineEmits<{
  close: []
  create: [vsBot: boolean, botVersion: BattleshipBotVersion]
}>()

const vsBot = ref(true)
const botVersion = ref<BattleshipBotVersion>(2)
const botVersions: BattleshipBotVersion[] = [1, 2, 3]
const { overlayRef, dialogRef, trapTabKey } = useFocusTrapDialog()

function botVersionLabel(version: BattleshipBotVersion): string {
  return message(`battleship.lobby.botVersion${version}`)
}

function requestClose(): void {
  if (props.isCreating) return
  emit('close')
}

function handleSubmit(): void {
  if (props.isCreating) return
  emit('create', vsBot.value, botVersion.value)
}

function onDialogKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    requestClose()
    return
  }
  trapTabKey(event)
}
</script>

<template>
  <Teleport to="body">
    <div
      ref="overlayRef"
      class="create-modal"
      @click.self="requestClose"
    >
      <form
        ref="dialogRef"
        class="bs-card create-card"
        role="dialog"
        aria-modal="true"
        aria-labelledby="battleship-create-title"
        :aria-describedby="errorMessage ? 'battleship-create-error' : undefined"
        :aria-busy="isCreating"
        tabindex="-1"
        @keydown="onDialogKeydown"
        @submit.prevent="handleSubmit"
      >
        <header class="create-card-header">
          <div>
            <span class="bs-kicker">{{ message('battleship.lobby.createEyebrow') }}</span>
            <h3 id="battleship-create-title" class="bs-title create-title">
              {{ message('battleship.lobby.createTitle') }}
            </h3>
          </div>
          <button
            type="button"
            class="create-close"
            :aria-label="message('battleship.lobby.close')"
            :disabled="isCreating"
            @click="requestClose"
          >
            ×
          </button>
        </header>

        <p
          v-if="errorMessage"
          id="battleship-create-error"
          class="create-error"
          role="alert"
        >
          {{ errorMessage }}
        </p>

        <fieldset class="create-fieldset opponent-picker" :disabled="isCreating">
          <legend>{{ message('battleship.lobby.opponentLabel') }}</legend>
          <label :class="{ active: vsBot }">
            <input v-model="vsBot" type="radio" :value="true" />
            <strong>{{ message('battleship.lobby.opponentBot') }}</strong>
            <small>{{ message('battleship.lobby.opponentBotHint') }}</small>
          </label>
          <label :class="{ active: !vsBot }">
            <input v-model="vsBot" type="radio" :value="false" />
            <strong>{{ message('battleship.lobby.opponentPlayer') }}</strong>
            <small>{{ message('battleship.lobby.opponentPlayerHint') }}</small>
          </label>
        </fieldset>

        <fieldset v-if="vsBot" class="create-fieldset version-picker" :disabled="isCreating">
          <legend>{{ message('battleship.lobby.botVersionLabel') }}</legend>
          <label
            v-for="version in botVersions"
            :key="version"
            :class="{ active: botVersion === version }"
          >
            <input v-model="botVersion" type="radio" :value="version" />
            <strong>{{ botVersionLabel(version) }}</strong>
          </label>
        </fieldset>

        <footer class="create-actions">
          <button
            type="button"
            class="bs-btn bs-btn--sm"
            :disabled="isCreating"
            @click="requestClose"
          >
            {{ message('battleship.lobby.cancel') }}
          </button>
          <button type="submit" class="bs-btn bs-btn--primary" :disabled="isCreating">
            {{ isCreating
              ? message('battleship.lobby.creating')
              : message('battleship.lobby.createAction') }}
          </button>
        </footer>
      </form>
    </div>
  </Teleport>
</template>

<style scoped>
.create-modal {
  --bs-accent: var(--accent-blue);

  position: fixed;
  inset: 0;
  z-index: 3200;
  display: grid;
  place-items: center;
  padding: 1rem;
  background: rgb(2 8 18 / 74%);
  backdrop-filter: blur(8px);
  animation: create-modal-in 160ms ease both;
}

.create-card {
  width: min(100%, 520px);
  display: grid;
  gap: 1rem;
  padding: 1.25rem;
  outline: none;
  box-shadow: 0 24px 80px rgb(0 0 0 / 45%);
  animation: create-card-in 160ms ease both;
}

.create-card-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
}

.create-title {
  margin: 0.25rem 0 0;
  font-size: 1.25rem;
}

.create-close {
  width: 40px;
  height: 40px;
  display: grid;
  place-items: center;
  border: 1px solid var(--border-subtle);
  border-radius: 10px;
  color: var(--text-muted);
  background: transparent;
  font: inherit;
  font-size: 1.4rem;
  cursor: pointer;
}

.create-close:hover:not(:disabled) {
  color: var(--text-primary);
  border-color: var(--accent-blue);
}

.create-error {
  margin: 0;
  padding: 0.65rem 0.75rem;
  border: 1px solid color-mix(in srgb, var(--accent-red) 45%, var(--border-subtle));
  border-radius: 10px;
  color: var(--accent-red);
  background: color-mix(in srgb, var(--accent-red) 9%, var(--bg-card));
  font-size: 0.82rem;
}

.create-fieldset {
  min-width: 0;
  margin: 0;
  padding: 0;
  border: 0;
}

.create-fieldset legend {
  margin-bottom: 0.5rem;
  color: var(--text-muted);
  font-size: 0.75rem;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.opponent-picker,
.version-picker {
  display: grid;
  gap: 0.55rem;
}

.opponent-picker {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.version-picker {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.opponent-picker legend,
.version-picker legend {
  grid-column: 1 / -1;
}

.create-fieldset label {
  position: relative;
  min-height: 64px;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 0.2rem;
  padding: 0.65rem 0.8rem;
  border: 1px solid var(--border-subtle);
  border-radius: 10px;
  color: var(--text-muted);
  background: color-mix(in srgb, var(--bg-card) 82%, transparent);
  cursor: pointer;
  transition: border-color 150ms ease, background 150ms ease, color 150ms ease;
}

.create-fieldset label:hover,
.create-fieldset label:focus-within {
  border-color: color-mix(in srgb, var(--accent-blue) 65%, var(--border-subtle));
}

.create-fieldset label.active {
  color: var(--text-primary);
  border-color: var(--accent-blue);
  background: color-mix(in srgb, var(--accent-blue) 12%, var(--bg-card));
}

.create-fieldset:disabled label {
  cursor: wait;
  opacity: 0.7;
}

.create-fieldset input {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  margin: 0;
  opacity: 0;
  cursor: pointer;
}

.create-fieldset strong,
.create-fieldset small {
  pointer-events: none;
}

.create-fieldset small {
  color: var(--text-dim);
  font-size: 0.72rem;
}

.version-picker label {
  min-height: 48px;
  align-items: center;
  padding: 0.5rem;
}

.create-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.6rem;
  padding-top: 0.25rem;
}

@keyframes create-modal-in {
  from { opacity: 0; }
}

@keyframes create-card-in {
  from { transform: translateY(10px) scale(0.98); }
}

@media (max-width: 520px) {
  .opponent-picker {
    grid-template-columns: 1fr;
  }

  .create-actions {
    display: grid;
    grid-template-columns: 1fr 1fr;
  }
}

@media (prefers-reduced-motion: reduce) {
  .create-modal,
  .create-card {
    animation: none;
  }
}
</style>
