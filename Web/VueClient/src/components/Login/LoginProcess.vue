<template>
  <div>
    <div class="loginGlow"></div>
    <div class="loginBox">
      <div class="crownIcon">&#128081;</div>
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

/* Glow effect behind the login box */
.loginGlow {
  position: absolute;
  width: 30vw;
  min-width: 320px;
  height: 100%;
  border-radius: 16px;
  background: radial-gradient(ellipse 100% 80% at 50% 50%,
    rgba(72, 202, 180, 0.12) 0%,
    rgba(240, 200, 80, 0.06) 40%,
    transparent 70%);
  filter: blur(40px);
  pointer-events: none;
  z-index: 0;
  animation: glowPulse 6s ease-in-out infinite alternate;
}

@keyframes glowPulse {
  0% { opacity: 0.6; transform: scale(1); }
  100% { opacity: 1; transform: scale(1.08); }
}

.loginBox {
  flex-direction: column;
  width: 30vw;
  min-width: 320px;
  /* Glassmorphism */
  background: rgba(30, 34, 42, 0.7);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 10px;
  justify-content: center;
  padding: 2rem 1.75rem;
  gap: 1.25rem;
  box-shadow:
    0 8px 32px rgba(0, 0, 0, 0.4),
    inset 0 1px 0 rgba(255, 255, 255, 0.05);
  position: relative;
  z-index: 1;
  /* Fade-in entrance animation */
  animation: loginFadeIn 0.7s cubic-bezier(0.0, 0, 0.2, 1) both;
}

@keyframes loginFadeIn {
  0% {
    opacity: 0;
    transform: translateY(24px) scale(0.97);
  }
  100% {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}

/* Crown icon */
.crownIcon {
  justify-content: center;
  font-size: 2.25rem;
  line-height: 1;
  filter: drop-shadow(0 0 8px rgba(240, 200, 80, 0.4));
  animation: crownBob 3s ease-in-out infinite;
}

@keyframes crownBob {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-4px); }
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

  .loginGlow {
    width: 100vw;
  }
}
</style>
