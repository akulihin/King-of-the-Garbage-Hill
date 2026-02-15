<template>
  <div>
    <div class="loginBox">
      <div>
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

.loginBox {
  flex-direction: column;
  width: 30vw;
  min-width: 320px;
  background-color: var(--kh-c-neutrals-sat-700);
  border: 1px solid var(--kh-c-neutrals-pale-375);
  border-radius: 10px;
  justify-content: center;
  padding: 2rem 1.75rem;
  gap: 1.5rem;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
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

  .okButton {
    width: 50vw;
    height: 3.5rem;
    font-size: 1.25rem;
  }
}
</style>
