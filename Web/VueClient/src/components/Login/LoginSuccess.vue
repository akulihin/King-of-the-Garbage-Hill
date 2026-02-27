<template>
  <div>
    <div class="loginGlow"></div>
    <div class="loginBox">
      <div>
        <div class="checkmarkWrapper">
          <svg class="checkmarkSvg" viewBox="0 0 52 52">
            <circle class="checkmarkCircle" cx="26" cy="26" r="24" fill="none" />
            <path class="checkmarkCheck" fill="none" d="M14.1 27.2l7.1 7.2 16.7-16.8" />
          </svg>
        </div>
        <div class="version">kotgh-{{ version }}</div>
        <div class="user">
          <div class="loggedInMessage">Logged in as</div>
          <div class="userName">{{ username }}</div>
        </div>
      </div>
      <div>
        <div class="redirectMessage">
          You will be automatically redirected in {{ countdown }} seconds...
        </div>
        <div class="actionBox">
          <button class="okButton" @click="redirect">Ok</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'

defineProps<{
  version?: string
  username?: string
}>()

const emit = defineEmits<{
  continue: []
}>()

const countdown = ref(3)

function redirect() {
  emit('continue')
}

function startCountdown() {
  const interval = setInterval(() => {
    countdown.value--
    if (countdown.value === 0) {
      clearInterval(interval)
      redirect()
    }
  }, 1000)
}

onMounted(() => {
  startCountdown()
})
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
    rgba(72, 202, 180, 0.15) 0%,
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
  gap: 1.5rem;
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

/* Animated checkmark */
.checkmarkWrapper {
  justify-content: center;
  margin-bottom: 0.25rem;
}

.checkmarkSvg {
  width: 52px;
  height: 52px;
}

.checkmarkCircle {
  stroke: rgba(72, 202, 180, 0.8);
  stroke-width: 2;
  stroke-dasharray: 166;
  stroke-dashoffset: 166;
  animation: checkCircleStroke 0.6s cubic-bezier(0.65, 0, 0.45, 1) 0.15s forwards;
}

.checkmarkCheck {
  stroke: rgba(72, 202, 180, 0.9);
  stroke-width: 3;
  stroke-linecap: round;
  stroke-linejoin: round;
  stroke-dasharray: 48;
  stroke-dashoffset: 48;
  animation: checkStroke 0.35s cubic-bezier(0.65, 0, 0.45, 1) 0.6s forwards;
}

@keyframes checkCircleStroke {
  100% { stroke-dashoffset: 0; }
}

@keyframes checkStroke {
  100% { stroke-dashoffset: 0; }
}

.loginBox > div {
  flex-direction: column;
}

.loginBox > div:first-child {
  gap: 1.5rem;
}

.user {
  gap: 0.25rem;
  flex-direction: column;
}

.user > .loggedInMessage {
  font-size: 0.7rem;
  color: var(--kh-c-text-primary-700);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-weight: 700;
}

.user > .userName {
  font-size: 1.125rem;
  color: var(--kh-c-text-highlight-primary);
  background-color: var(--kh-c-neutrals-sat-800);
  border: 1px solid var(--kh-c-neutrals-pale-375);
  border-radius: 6px;
  justify-content: center;
  padding: 0.75rem;
  font-family: 'JetBrains Mono', monospace;
  font-weight: 700;
}

.loginBox > div:last-child {
  gap: 1rem;
}

.actionBox {
  justify-content: center;
}

.redirectMessage {
  font-size: 0.7rem;
  color: var(--kh-c-text-primary-700);
}

.okButton {
  width: 11.25rem;
  height: 2.75rem;
  justify-content: center;
  align-items: center;
  padding: 0.5rem;
  background-color: var(--kh-c-secondary-success-500);
  color: var(--kh-c-text-primary-500);
  border: none;
  border-radius: 6px;
  font-size: 1rem;
  font-weight: 700;
  z-index: 5;
  transition: all 0.15s ease-out;
  cursor: pointer;
}

.okButton:hover {
  background-color: var(--kh-c-secondary-success-300);
  box-shadow: 0 0 12px rgba(63, 167, 61, 0.25);
}

.okButton:active {
  background-color: var(--kh-c-secondary-success-600);
}

.version {
  color: var(--kh-c-text-primary-800);
  font-size: 0.7rem;
  justify-content: center;
  font-weight: 700;
  letter-spacing: 1px;
  text-transform: uppercase;
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

  .okButton {
    width: 50vw;
    height: 3.5rem;
    font-size: 1.25rem;
  }
}
</style>
