<template>
  <div>
    <div class="loginBox">
      <div class="version">kotgh-{{ version }}</div>
      <div class="info">
        King of the Garbage Hill uses your Discord account to play.
        Enter your Discord ID below to connect.
      </div>

      <div class="loginForm">
        <input
          v-model="discordIdInput"
          type="text"
          class="discordInput"
          placeholder="Discord User ID"
          @keyup.enter="handleLogin"
        >
        <button class="loginButton" :disabled="loading" @click="handleLogin">
          {{ loading ? 'Connecting...' : 'Login with Discord' }}
        </button>
      </div>

      <div class="hint">
        Enable Developer Mode in Discord settings, then right-click your name
        and select "Copy User ID".
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

defineProps<{
  version?: string
}>()

const emit = defineEmits<{
  login: [discordId: string]
}>()

const discordIdInput = ref(localStorage.getItem('discordId') || '')
const loading = ref(false)

function handleLogin() {
  const id = discordIdInput.value.trim()
  if (!id || !/^\d+$/.test(id)) return
  loading.value = true
  emit('login', id)
}
</script>

<style scoped>
* { display: flex; }

.loginBox {
  flex-direction: column;
  width: 30vw;
  min-width: 320px;
  background-color: var(--kh-c-neutrals-pale-300);
  justify-content: center;
  padding: 2rem 1.75rem;
  gap: 1.5rem;
}

.info {
  color: var(--kh-c-text-primary-600);
  font-size: 0.875rem;
}

.version {
  color: var(--kh-c-text-primary-700);
  font-size: 0.75rem;
  justify-content: center;
}

.loginForm {
  flex-direction: column;
  gap: 0.75rem;
}

.discordInput {
  width: 100%;
  height: 3rem;
  padding: 0.5rem 1rem;
  border: 1px solid var(--kh-c-neutrals-pale-260);
  background-color: var(--kh-c-neutrals-sat-650);
  color: var(--kh-c-text-primary-500);
  font-size: 0.95rem;
  outline: none;
}

.discordInput:focus {
  border-color: var(--kh-c-text-primary-700);
}

.discordInput::placeholder {
  color: var(--kh-c-text-primary-800);
}

.loginButton {
  display: flex;
  justify-content: center;
  width: 100%;
  height: 3rem;
  align-items: center;
  padding: 0.5rem;
  cursor: pointer;
  border: none;
  font-size: 0.95rem;
  background-color: var(--kh-c-secondary-success-500);
  color: var(--kh-c-text-primary-500);
  transition: background-color 0.15s ease-out, color 0.15s ease-out;
}

.loginButton:hover:not(:disabled) {
  background-color: var(--kh-c-secondary-success-300);
}

.loginButton:active:not(:disabled) {
  background-color: var(--kh-c-secondary-success-600);
}

.loginButton:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.hint {
  color: var(--kh-c-text-primary-800);
  font-size: 0.75rem;
  line-height: 1.5;
}

/* Mobile */
@media only screen and (max-width: 800px) {
  .loginBox {
    width: 100vw;
    padding: 2.375rem 1.75rem;
    gap: 1.5rem;
  }

  .loginButton {
    font-size: 1rem;
  }
}
</style>
