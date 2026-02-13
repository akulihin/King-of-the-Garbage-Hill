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
  background-color: var(--kh-c-neutrals-pale-300);
  justify-content: center;
  padding: 2rem 1.75rem;
  gap: 1.5rem;
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
  font-size: 0.75rem;
  color: var(--kh-c-text-primary-600);
}

.user > .userName {
  font-size: 1.25rem;
  color: var(--kh-c-text-primary-500);
  background-color: var(--kh-c-neutrals-sat-650);
  justify-content: center;
  padding: 0.8rem;
}

.loginBox > div:last-child {
  gap: 1rem;
}

.actionBox {
  justify-content: center;
}

.redirectMessage {
  font-size: 0.75rem;
  color: var(--kh-c-text-primary-600);
}

.okButton {
  width: 11.25rem;
  height: 3rem;
  justify-content: center;
  align-items: center;
  padding: 0.5rem;
  background-color: var(--kh-c-secondary-success-500);
  color: var(--kh-c-text-primary-500);
  border: none;
  font-size: 1.125rem;
  z-index: 5;
  transition: background-color 0.15s ease-out, color 0.15s ease-out;
  cursor: pointer;
}

.okButton:hover {
  background-color: var(--kh-c-text-primary-600);
  color: var(--kh-c-secondary-success-300);
  border: 1px solid var(--kh-c-secondary-success-500);
}

.okButton:active {
  background-color: transparent;
  color: var(--kh-c-secondary-success-300);
  border: 1px solid var(--kh-c-secondary-success-500);
}

.version {
  color: var(--kh-c-text-primary-700);
  font-size: 0.75rem;
  justify-content: center;
}

/* Mobile */
@media only screen and (max-width: 800px) {
  .loginBox {
    width: 100vw;
    padding: 2.375rem 1.75rem;
    gap: 1.5rem;
  }

  .okButton {
    width: 50vw;
    height: 4rem;
    font-size: 1.5rem;
  }
}
</style>
