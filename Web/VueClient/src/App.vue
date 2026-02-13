<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useGameStore } from './store/game'
import LoginProcess from 'src/components/Login/LoginProcess.vue'
import LoginSuccess from 'src/components/Login/LoginSuccess.vue'
import { useRouter } from 'vue-router'

const store = useGameStore()
const router = useRouter()

const showLogin = ref(true)
const loginSuccess = ref(false)
const loggedInUsername = ref('')

onMounted(async () => {
  const stored = localStorage.getItem('discordId')
  if (stored) {
    await connectAndAuth(stored)
  }
})

async function connectAndAuth(id: string) {
  if (!id || !/^\d+$/.test(id)) return
  await store.connect()
  await store.authenticate(id)
  localStorage.setItem('discordId', id)
  loggedInUsername.value = `ID: ${id}`
  loginSuccess.value = true
}

async function handleLogin(discordId: string) {
  await connectAndAuth(discordId)
}

function handleContinue() {
  showLogin.value = false
}

function handleLogout() {
  localStorage.removeItem('discordId')
  showLogin.value = true
  loginSuccess.value = false
  store.discordId = ''
  store.isAuthenticated = false
}
</script>

<template>
  <div class="app">
    <!-- Login screen (designer's layout) -->
    <div v-if="showLogin && !store.isAuthenticated" class="logins">
      <LoginProcess
        version="1.0"
        @login="handleLogin"
      />
    </div>

    <!-- Login success → redirect -->
    <div v-else-if="loginSuccess && showLogin" class="logins">
      <LoginSuccess
        version="1.0"
        :username="loggedInUsername"
        @continue="handleContinue"
      />
    </div>

    <!-- Main content -->
    <template v-else>
      <!-- Top bar -->
      <header class="top-bar">
        <div class="top-bar-left">
          <span class="logo-icon">👑</span>
          <RouterLink to="/" class="logo-text">KOTGH</RouterLink>

          <nav class="top-nav">
            <RouterLink to="/">Games</RouterLink>
            <RouterLink to="/home">Home</RouterLink>
          </nav>
        </div>

        <div class="top-bar-right">
          <span
            class="connection-dot"
            :class="{ connected: store.isConnected, disconnected: !store.isConnected }"
          />
          <span v-if="store.isAuthenticated" class="user-info">
            {{ store.discordId }}
          </span>
          <button class="top-btn" @click="handleLogout">Logout</button>
        </div>
      </header>

      <!-- Error toast -->
      <Transition name="fade">
        <div v-if="store.errorMessage" class="error-toast">
          {{ store.errorMessage }}
        </div>
      </Transition>

      <main class="main-content">
        <RouterView />
      </main>
    </template>
  </div>
</template>

<style>
/* ── App-level theme variables ─────────────────────────────────────
   Maps the game component vars to KOTGH designer palette.
   Existing game components (Leaderboard, etc.) use --bg-*, --accent-*
   so we keep those names but point them at the designer's colors.
──────────────────────────────────────────────────────────────────── */
:root {
  /* Backgrounds — mapped to KOTGH neutrals */
  --bg-primary: var(--kh-c-neutrals-sat-800);
  --bg-secondary: var(--kh-c-neutrals-sat-700);
  --bg-card: var(--kh-c-neutrals-sat-600);
  --bg-card-hover: var(--kh-c-neutrals-sat-500);

  /* Text — mapped to KOTGH text */
  --text-primary: var(--kh-c-text-primary-500);
  --text-secondary: var(--kh-c-text-primary-600);
  --text-muted: var(--kh-c-text-primary-700);

  /* Accents — keep distinct accent colors */
  --accent-gold: var(--kh-c-text-highlight-primary);
  --accent-blue: #4a9eff;
  --accent-green: var(--kh-c-secondary-success-200);
  --accent-red: #f87171;
  --accent-purple: #a78bfa;
  --accent-orange: #fb923c;

  /* Borders — mapped to KOTGH neutrals */
  --border-color: var(--kh-c-neutrals-pale-260);

  /* Misc */
  --radius: 8px;
  --radius-lg: 12px;
  --shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.3);
  --font-mono: 'JetBrains Mono', 'Fira Code', monospace;
}

/* ── Layout ───────────────────────────────────────────────────────── */
.app {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  width: 100%;
}

/* ── Login screen ─────────────────────────────────────────────────── */
.logins {
  display: flex;
  width: 100vw;
  flex-direction: row;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  min-height: 100svh;
  padding-bottom: 20vh;
}

.logins > div {
  display: flex;
  justify-content: center;
}

/* ── Top bar ──────────────────────────────────────────────────────── */
.top-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.5rem 1rem;
  background: var(--kh-c-neutrals-sat-700);
  border-bottom: 1px solid var(--kh-c-neutrals-pale-375);
}

.top-bar-left {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.logo-icon { font-size: 1.25rem; }

.logo-text {
  font-size: 1.125rem;
  font-weight: 700;
  color: var(--kh-c-text-highlight-primary);
  text-decoration: none;
}

.top-nav {
  display: flex;
  gap: 0.25rem;
  margin-left: 1rem;
}

.top-nav a {
  padding: 0.375rem 0.75rem;
  color: var(--kh-c-text-primary-700);
  text-decoration: none;
  font-size: 0.875rem;
  border: 1px solid transparent;
  transition: all 0.15s;
}

.top-nav a:hover {
  background-color: var(--kh-c-neutrals-pale-500);
  border-color: var(--kh-c-neutrals-pale-260);
  color: var(--kh-c-text-primary-600);
}

.top-nav a.router-link-exact-active {
  background-color: var(--kh-c-neutrals-pale-575);
  border-bottom: 2px solid var(--kh-c-text-highlight-primary);
  color: var(--kh-c-text-primary-500);
}

.top-bar-right {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.connection-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.connection-dot.connected {
  background: var(--kh-c-secondary-success-200);
  box-shadow: 0 0 6px var(--kh-c-secondary-success-200);
}

.connection-dot.disconnected {
  background: var(--accent-red);
  box-shadow: 0 0 6px var(--accent-red);
}

.user-info {
  color: var(--kh-c-text-primary-700);
  font-size: 0.8rem;
}

.top-btn {
  padding: 0.25rem 0.75rem;
  background: transparent;
  color: var(--kh-c-text-primary-700);
  border: 1px solid var(--kh-c-neutrals-pale-260);
  cursor: pointer;
  font-size: 0.8rem;
  transition: all 0.15s;
}

.top-btn:hover {
  background: var(--kh-c-neutrals-pale-500);
  color: var(--kh-c-text-primary-600);
}

/* ── Main content ─────────────────────────────────────────────────── */
.main-content {
  flex: 1;
  padding: 1.5rem;
  max-width: 1400px;
  margin: 0 auto;
  width: 100%;
}

/* ── Buttons (shared across game components) ──────────────────────── */
.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: var(--radius);
  font-size: 0.875rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.375rem;
}

.btn:disabled { opacity: 0.5; cursor: not-allowed; }

.btn-primary {
  background: var(--kh-c-secondary-success-500);
  color: var(--kh-c-text-primary-500);
}
.btn-primary:hover:not(:disabled) {
  background: var(--kh-c-secondary-success-300);
}

.btn-danger { background: var(--accent-red); color: white; }
.btn-danger:hover:not(:disabled) { background: #ef4444; }

.btn-success { background: var(--kh-c-secondary-success-200); color: var(--kh-c-neutrals-sat-800); }
.btn-success:hover:not(:disabled) { background: var(--kh-c-secondary-success-300); }

.btn-ghost {
  background: transparent;
  color: var(--kh-c-text-primary-700);
  border: 1px solid var(--kh-c-neutrals-pale-260);
}
.btn-ghost:hover:not(:disabled) {
  background: var(--kh-c-neutrals-pale-500);
  color: var(--kh-c-text-primary-600);
}

.btn-sm { padding: 0.25rem 0.75rem; font-size: 0.75rem; }
.btn-lg { padding: 0.75rem 1.5rem; font-size: 1rem; }

/* ── Inputs ───────────────────────────────────────────────────────── */
.input {
  padding: 0.5rem 1rem;
  background: var(--kh-c-neutrals-sat-650);
  border: 1px solid var(--kh-c-neutrals-pale-260);
  color: var(--kh-c-text-primary-500);
  font-size: 0.875rem;
  flex: 1;
  outline: none;
  transition: border-color 0.2s;
}

.input:focus { border-color: var(--kh-c-text-primary-700); }

/* ── Cards ────────────────────────────────────────────────────────── */
.card {
  background: var(--kh-c-neutrals-sat-600);
  border: 1px solid var(--kh-c-neutrals-sat-500);
  border-radius: var(--radius-lg);
  padding: 1.25rem;
}

.card-header {
  font-size: 1rem;
  font-weight: 700;
  margin-bottom: 1rem;
  color: var(--kh-c-text-highlight-primary);
}

/* ── Error Toast ──────────────────────────────────────────────────── */
.error-toast {
  position: fixed;
  top: 4rem;
  right: 1.5rem;
  background: var(--accent-red);
  color: white;
  padding: 0.75rem 1.5rem;
  border-radius: var(--radius);
  font-size: 0.875rem;
  z-index: 1000;
  box-shadow: var(--shadow);
}

/* ── Transitions ──────────────────────────────────────────────────── */
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

/* ── Stat Colors (used by game components) ────────────────────────── */
.stat-intelligence { color: #60a5fa; }
.stat-strength { color: #f87171; }
.stat-speed { color: #4ade80; }
.stat-psyche { color: #c084fc; }
.stat-skill { color: var(--kh-c-text-highlight-primary); }
.stat-moral { color: var(--accent-orange); }
.stat-justice { color: #e879f9; }
</style>
