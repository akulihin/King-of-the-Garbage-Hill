<template>
  <div>
    <div class="loginBox">
      <div class="version">kotgh-{{ version }}</div>
      <div class="info">
        King of the Garbage Hill — a turn-based tactical game.
        Sign in with Discord or create a web account to play.
      </div>

      <!-- Discord login -->
      <div class="loginSection">
        <div class="sectionLabel">Discord</div>
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

      <div class="divider"><span>OR</span></div>

      <!-- Web account -->
      <div class="loginSection">
        <div class="sectionLabel">Play without Discord</div>
        <div class="loginForm">
          <input
            v-model="webUsernameInput"
            type="text"
            class="discordInput"
            placeholder="Choose a username"
            maxlength="32"
            @keyup.enter="handleWebLogin"
          >
          <button class="loginButton webButton" :disabled="loading" @click="handleWebLogin">
            {{ loading ? 'Creating...' : 'Create Web Account' }}
          </button>
        </div>
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
  webLogin: [username: string]
}>()

const discordIdInput = ref(localStorage.getItem('discordId') || '')
const webUsernameInput = ref('')
const loading = ref(false)

function handleLogin() {
  const id = discordIdInput.value.trim()
  if (!id || !/^\d+$/.test(id)) return
  loading.value = true
  emit('login', id)
}

function handleWebLogin() {
  const name = webUsernameInput.value.trim()
  if (!name || name.length > 32) return
  loading.value = true
  emit('webLogin', name)
}
</script>

<style scoped>
* { display: flex; }

.loginBox {
  flex-direction: column;
  width: 30vw;
  min-width: 320px;
  background-color: var(--kh-c-neutrals-sat-700);
  border: 1px solid var(--kh-c-neutrals-pale-375);
  border-radius: 10px;
  justify-content: center;
  padding: 2rem 1.75rem;
  gap: 1.25rem;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
}

.info {
  color: var(--kh-c-text-primary-600);
  font-size: 0.85rem;
  line-height: 1.5;
}

.version {
  color: var(--kh-c-text-primary-800);
  font-size: 0.7rem;
  justify-content: center;
  font-weight: 700;
  letter-spacing: 1px;
  text-transform: uppercase;
}

.loginSection {
  flex-direction: column;
  gap: 0.5rem;
}

.sectionLabel {
  color: var(--kh-c-text-primary-700);
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.loginForm {
  flex-direction: column;
  gap: 0.75rem;
}

.discordInput {
  width: 100%;
  height: 2.75rem;
  padding: 0.5rem 1rem;
  border: 1px solid var(--kh-c-neutrals-pale-375);
  border-radius: 6px;
  background-color: var(--kh-c-neutrals-sat-800);
  color: var(--kh-c-text-primary-500);
  font-size: 0.9rem;
  outline: none;
  font-family: 'JetBrains Mono', monospace;
  transition: border-color 0.15s;
}

.discordInput:focus {
  border-color: var(--kh-c-text-highlight-primary);
}

.discordInput::placeholder {
  color: var(--kh-c-text-primary-800);
  font-family: 'Inter', sans-serif;
}

.loginButton {
  display: flex;
  justify-content: center;
  width: 100%;
  height: 2.75rem;
  align-items: center;
  padding: 0.5rem;
  cursor: pointer;
  border: none;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 700;
  letter-spacing: 0.3px;
  background-color: var(--kh-c-secondary-success-500);
  color: var(--kh-c-text-primary-500);
  transition: all 0.15s ease-out;
}

.loginButton:hover:not(:disabled) {
  background-color: var(--kh-c-secondary-success-300);
  box-shadow: 0 0 12px rgba(63, 167, 61, 0.25);
}

.loginButton:active:not(:disabled) {
  background-color: var(--kh-c-secondary-success-600);
}

.loginButton:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.webButton {
  background-color: var(--kh-c-secondary-info-500);
}

.webButton:hover:not(:disabled) {
  background-color: var(--kh-c-secondary-info-300);
  box-shadow: 0 0 12px rgba(110, 170, 240, 0.25);
}

.webButton:active:not(:disabled) {
  background-color: var(--kh-c-secondary-info-600);
}

.divider {
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  color: var(--kh-c-text-primary-800);
  font-size: 0.7rem;
  font-weight: 700;
}

.divider::before,
.divider::after {
  content: '';
  flex: 1;
  height: 1px;
  background: var(--kh-c-neutrals-pale-375);
}

.hint {
  color: var(--kh-c-text-primary-800);
  font-size: 0.7rem;
  line-height: 1.5;
}

@media only screen and (max-width: 800px) {
  .loginBox {
    width: 100vw;
    padding: 2.375rem 1.75rem;
    gap: 1.5rem;
    border-radius: 0;
  }
}
</style>
