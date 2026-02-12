<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useGameStore } from './store/game'

const store = useGameStore()
const discordIdInput = ref('')
const showAuth = ref(true)

onMounted(async () => {
  // Check if we have a stored Discord ID
  const stored = localStorage.getItem('discordId')
  if (stored) {
    discordIdInput.value = stored
    await connectAndAuth(stored)
  }
})

async function connectAndAuth(id: string) {
  if (!id || !/^\d+$/.test(id)) return
  await store.connect()
  // Send as raw string — never convert to Number (would lose precision on snowflake IDs)
  await store.authenticate(id)
  localStorage.setItem('discordId', id)
  showAuth.value = false
}

async function handleLogin() {
  const id = discordIdInput.value.trim()
  if (!id || !/^\d+$/.test(id)) return
  await connectAndAuth(id)
}

function handleLogout() {
  localStorage.removeItem('discordId')
  showAuth.value = true
  store.discordId = ''
  store.isAuthenticated = false
}
</script>

<template>
  <div class="app">
    <!-- Connection status bar -->
    <header class="top-bar">
      <div class="logo">
        <span class="logo-icon">👑</span>
        <span class="logo-text">King of the Garbage Hill</span>
      </div>
      <div class="status-bar">
        <span
          class="connection-dot"
          :class="{ connected: store.isConnected, disconnected: !store.isConnected }"
        />
        <span v-if="store.isAuthenticated" class="user-info">
          ID: {{ store.discordId }}
          <button class="btn btn-sm btn-ghost" @click="handleLogout">
            Logout
          </button>
        </span>
      </div>
    </header>

    <!-- Auth screen -->
    <div v-if="showAuth && !store.isAuthenticated" class="auth-screen">
      <div class="auth-card">
        <h1>King of the Garbage Hill</h1>
        <p class="subtitle">
          Enter your Discord ID to connect
        </p>
        <div class="auth-form">
          <input
            v-model="discordIdInput"
            type="text"
            placeholder="Discord User ID"
            class="input"
            @keyup.enter="handleLogin"
          >
          <button class="btn btn-primary" :disabled="store.isLoading" @click="handleLogin">
            {{ store.isLoading ? 'Connecting...' : 'Connect' }}
          </button>
        </div>
        <p class="hint">
          You can find your Discord ID by enabling Developer Mode in Discord settings,
          then right-clicking your name and selecting "Copy User ID".
        </p>
      </div>
    </div>

    <!-- Main content -->
    <main v-else class="main-content">
      <!-- Error toast -->
      <Transition name="fade">
        <div v-if="store.errorMessage" class="error-toast">
          {{ store.errorMessage }}
        </div>
      </Transition>

      <RouterView />
    </main>
  </div>
</template>

<style>
/* ── CSS Reset & Variables ────────────────────────────────────────── */
:root {
  --bg-primary: #0f0f1a;
  --bg-secondary: #1a1a2e;
  --bg-card: #16213e;
  --bg-card-hover: #1a2744;
  --text-primary: #e0e0e0;
  --text-secondary: #a0a0b0;
  --text-muted: #6b6b80;
  --accent-gold: #ffd700;
  --accent-blue: #4a9eff;
  --accent-green: #4ade80;
  --accent-red: #f87171;
  --accent-purple: #a78bfa;
  --accent-orange: #fb923c;
  --border-color: #2a2a4a;
  --radius: 8px;
  --radius-lg: 12px;
  --shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.3);
  --font-mono: 'JetBrains Mono', 'Fira Code', monospace;
}

* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

body {
  font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  background: var(--bg-primary);
  color: var(--text-primary);
  min-height: 100vh;
  overflow-x: hidden;
}

/* ── Scrollbar ────────────────────────────────────────────────────── */
::-webkit-scrollbar { width: 8px; }
::-webkit-scrollbar-track { background: var(--bg-primary); }
::-webkit-scrollbar-thumb { background: var(--border-color); border-radius: 4px; }
::-webkit-scrollbar-thumb:hover { background: var(--text-muted); }

/* ── Layout ───────────────────────────────────────────────────────── */
.app {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
}

.top-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 24px;
  background: var(--bg-secondary);
  border-bottom: 1px solid var(--border-color);
  z-index: 100;
}

.logo {
  display: flex;
  align-items: center;
  gap: 8px;
}

.logo-icon {
  font-size: 24px;
}

.logo-text {
  font-size: 18px;
  font-weight: 700;
  background: linear-gradient(135deg, var(--accent-gold), var(--accent-orange));
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.status-bar {
  display: flex;
  align-items: center;
  gap: 12px;
}

.connection-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  display: inline-block;
}

.connection-dot.connected { background: var(--accent-green); box-shadow: 0 0 8px var(--accent-green); }
.connection-dot.disconnected { background: var(--accent-red); box-shadow: 0 0 8px var(--accent-red); }

.user-info {
  color: var(--text-secondary);
  font-size: 14px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.main-content {
  flex: 1;
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
  width: 100%;
}

/* ── Auth Screen ──────────────────────────────────────────────────── */
.auth-screen {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
}

.auth-card {
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  padding: 48px;
  text-align: center;
  max-width: 480px;
  width: 100%;
}

.auth-card h1 {
  font-size: 28px;
  margin-bottom: 8px;
  background: linear-gradient(135deg, var(--accent-gold), var(--accent-orange));
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.subtitle {
  color: var(--text-secondary);
  margin-bottom: 24px;
}

.auth-form {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
}

.hint {
  color: var(--text-muted);
  font-size: 12px;
  line-height: 1.5;
}

/* ── Buttons ──────────────────────────────────────────────────────── */
.btn {
  padding: 10px 20px;
  border: none;
  border-radius: var(--radius);
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-primary {
  background: linear-gradient(135deg, var(--accent-blue), var(--accent-purple));
  color: white;
}
.btn-primary:hover:not(:disabled) { transform: translateY(-1px); box-shadow: 0 4px 12px rgba(74, 158, 255, 0.3); }

.btn-danger {
  background: var(--accent-red);
  color: white;
}
.btn-danger:hover:not(:disabled) { background: #ef4444; }

.btn-success {
  background: var(--accent-green);
  color: #1a1a2e;
}
.btn-success:hover:not(:disabled) { background: #22c55e; }

.btn-warning {
  background: var(--accent-orange);
  color: #1a1a2e;
}
.btn-warning:hover:not(:disabled) { background: #f97316; }

.btn-ghost {
  background: transparent;
  color: var(--text-secondary);
  border: 1px solid var(--border-color);
}
.btn-ghost:hover:not(:disabled) { background: var(--bg-card); color: var(--text-primary); }

.btn-sm {
  padding: 4px 12px;
  font-size: 12px;
}

.btn-lg {
  padding: 14px 28px;
  font-size: 16px;
}

/* ── Inputs ───────────────────────────────────────────────────────── */
.input {
  padding: 10px 16px;
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius);
  color: var(--text-primary);
  font-size: 14px;
  flex: 1;
  outline: none;
  transition: border-color 0.2s;
}

.input:focus {
  border-color: var(--accent-blue);
}

/* ── Cards ────────────────────────────────────────────────────────── */
.card {
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  padding: 20px;
}

.card-header {
  font-size: 16px;
  font-weight: 700;
  margin-bottom: 16px;
  color: var(--accent-gold);
}

/* ── Error Toast ──────────────────────────────────────────────────── */
.error-toast {
  position: fixed;
  top: 80px;
  right: 24px;
  background: var(--accent-red);
  color: white;
  padding: 12px 24px;
  border-radius: var(--radius);
  font-size: 14px;
  z-index: 1000;
  box-shadow: var(--shadow);
}

/* ── Transitions ──────────────────────────────────────────────────── */
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

/* ── Stat Colors ──────────────────────────────────────────────────── */
.stat-intelligence { color: #60a5fa; }
.stat-strength { color: #f87171; }
.stat-speed { color: #4ade80; }
.stat-psyche { color: #c084fc; }
.stat-skill { color: var(--accent-gold); }
.stat-moral { color: var(--accent-orange); }
.stat-justice { color: #e879f9; }
</style>
