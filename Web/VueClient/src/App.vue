<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { useGameStore } from './store/game'
import LoginProcess from 'src/components/Login/LoginProcess.vue'
import LoginSuccess from 'src/components/Login/LoginSuccess.vue'
import { installGlobalButtonSound } from 'src/services/sound'

const store = useGameStore()

const showLogin = ref(true)
const loginSuccess = ref(false)
const loggedInUsername = ref('')
let removeGlobalButtonSound: (() => void) | null = null

const currentTheme = ref(localStorage.getItem('kotgh_theme') || '')

function setTheme(theme: string) {
  currentTheme.value = theme
  if (theme) {
    document.documentElement.setAttribute('data-theme', theme)
    localStorage.setItem('kotgh_theme', theme)
  } else {
    document.documentElement.removeAttribute('data-theme')
    localStorage.removeItem('kotgh_theme')
  }
}

onMounted(async () => {
  removeGlobalButtonSound = installGlobalButtonSound()
  if (currentTheme.value) {
    document.documentElement.setAttribute('data-theme', currentTheme.value)
  }
  // Check for saved web account first
  const savedWebId = localStorage.getItem('kotgh_web_id')
  const savedWebUsername = localStorage.getItem('kotgh_web_username')
  if (savedWebId && savedWebUsername) {
    await connectAndAuthWeb(savedWebId, savedWebUsername)
    return
  }
  const stored = localStorage.getItem('discordId')
  if (stored) {
    await connectAndAuth(stored)
  }
})

onUnmounted(() => {
  if (removeGlobalButtonSound) {
    removeGlobalButtonSound()
    removeGlobalButtonSound = null
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

async function connectAndAuthWeb(webId: string, username: string) {
  await store.connect()
  await store.authenticate(webId)
  store.webUsername = username
  store.isWebAccount = true
  loggedInUsername.value = username
  loginSuccess.value = true
}

async function handleLogin(discordId: string) {
  await connectAndAuth(discordId)
}

async function handleWebLogin(username: string) {
  await store.connect()
  await store.registerWebAccount(username)
  loggedInUsername.value = username
  loginSuccess.value = true
}

function handleContinue() {
  showLogin.value = false
}

function handleLogout() {
  localStorage.removeItem('discordId')
  localStorage.removeItem('kotgh_web_id')
  localStorage.removeItem('kotgh_web_username')
  showLogin.value = true
  loginSuccess.value = false
  store.discordId = ''
  store.isAuthenticated = false
  store.isWebAccount = false
  store.webUsername = ''
}
</script>

<template>
  <div class="app">
    <!-- Login screen (designer's layout) -->
    <div v-if="showLogin && !store.isAuthenticated" class="logins">
      <LoginProcess
        version="1.0"
        @login="handleLogin"
        @web-login="handleWebLogin"
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
            <RouterLink to="/games">Games</RouterLink>
            <RouterLink to="/home">Home</RouterLink>
          </nav>
        </div>

        <div class="top-bar-right">
          <span
            class="connection-dot"
            :class="{ connected: store.isConnected, disconnected: !store.isConnected }"
          />
          <span v-if="store.isAuthenticated" class="user-info">
            {{ store.isWebAccount ? store.webUsername : store.discordId }}
          </span>
          <button class="top-btn" @click="handleLogout">Logout</button>
          <select class="theme-select" :value="currentTheme" @change="setTheme(($event.target as HTMLSelectElement).value)">
            <option value="">Default</option>
            <option value="blood">Blood</option>
            <option value="neon">Neon</option>
            <option value="forest">Forest</option>
          </select>
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
──────────────────────────────────────────────────────────────────── */
:root {
  /* Backgrounds — layered surfaces */
  --bg-primary: var(--kh-c-neutrals-sat-800);       /* deepest background */
  --bg-secondary: var(--kh-c-neutrals-sat-700);     /* panel/row background */
  --bg-surface: var(--kh-c-neutrals-sat-650);       /* raised surface */
  --bg-card: var(--kh-c-neutrals-sat-600);          /* card background */
  --bg-card-hover: var(--kh-c-neutrals-sat-500);    /* hovered card */
  --bg-inset: var(--kh-c-neutrals-sat-750);         /* inset/recessed areas */

  /* Glass surfaces */
  --glass-bg: var(--kh-c-glass-bg);
  --glass-bg-heavy: var(--kh-c-glass-bg-heavy);
  --glass-border: var(--kh-c-glass-border);
  --glass-highlight: var(--kh-c-glass-highlight);

  /* Text — hierarchy */
  --text-primary: var(--kh-c-text-primary-500);     /* headings, important */
  --text-secondary: var(--kh-c-text-primary-600);   /* body text */
  --text-muted: var(--kh-c-text-primary-700);       /* labels, inactive */
  --text-dim: var(--kh-c-text-primary-800);         /* disabled, faint */

  /* Accents */
  --accent-gold: var(--kh-c-text-highlight-primary);
  --accent-gold-dim: var(--kh-c-text-highlight-dim);
  --accent-teal: var(--kh-c-secondary-success-200);
  --accent-teal-dim: var(--kh-c-secondary-success-500);
  --accent-blue: var(--kh-c-secondary-info-300);
  --accent-green: var(--kh-c-secondary-success-200);
  --accent-green-dim: var(--kh-c-secondary-success-500);
  --accent-coral: var(--kh-c-secondary-danger-200);
  --accent-red: var(--kh-c-secondary-danger-200);
  --accent-red-dim: var(--kh-c-secondary-danger-500);
  --accent-purple: var(--kh-c-secondary-purple-200);
  --accent-orange: #e6944a;

  /* Borders */
  --border-color: var(--kh-c-neutrals-pale-350);
  --border-subtle: var(--kh-c-neutrals-pale-500);

  /* Misc */
  --radius: 6px;
  --radius-lg: 10px;
  --shadow: 0 2px 8px rgba(0, 0, 0, 0.35);
  --shadow-lg: 0 8px 24px rgba(0, 0, 0, 0.5);
  --shadow-glow: 0 4px 16px rgba(0, 0, 0, 0.4);
  --font-mono: 'JetBrains Mono', 'Fira Code', monospace;
  --glow-gold: 0 0 12px rgba(240, 200, 80, 0.25), 0 0 4px rgba(240, 200, 80, 0.15);
  --glow-teal: 0 0 10px rgba(63, 167, 61, 0.3);
  --glow-green: 0 0 10px rgba(63, 167, 61, 0.3);
  --glow-coral: 0 0 10px rgba(239, 128, 128, 0.3);
  --glow-red: 0 0 10px rgba(239, 128, 128, 0.3);
  --glow-purple: 0 0 10px rgba(180, 150, 255, 0.3);
  --glow-blue: 0 0 10px rgba(110, 170, 240, 0.3);

  /* Easing */
  --ease-spring: cubic-bezier(0.34, 1.56, 0.64, 1);
  --ease-out: cubic-bezier(0.0, 0, 0.2, 1);
  --ease-in-out: cubic-bezier(0.4, 0, 0.2, 1);
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
  position: relative;
  overflow: hidden;
  /* Aurora background — layered drifting radial gradients */
  background:
    radial-gradient(ellipse 80% 60% at 20% 30%, rgba(72, 202, 180, 0.15) 0%, transparent 70%),
    radial-gradient(ellipse 70% 50% at 75% 65%, rgba(255, 127, 110, 0.08) 0%, transparent 70%),
    radial-gradient(ellipse 60% 70% at 50% 20%, rgba(240, 200, 80, 0.08) 0%, transparent 65%),
    radial-gradient(ellipse 50% 60% at 85% 25%, rgba(160, 130, 220, 0.08) 0%, transparent 60%),
    var(--bg-primary);
}

/* Animated aurora overlay using a pseudo-like approach with background animation */
.logins::before {
  content: '';
  position: absolute;
  inset: -50%;
  width: 200%;
  height: 200%;
  background:
    radial-gradient(ellipse 40% 35% at 30% 40%, rgba(72, 202, 180, 0.12) 0%, transparent 70%),
    radial-gradient(ellipse 35% 40% at 70% 60%, rgba(255, 127, 110, 0.07) 0%, transparent 70%),
    radial-gradient(ellipse 45% 30% at 55% 30%, rgba(240, 200, 80, 0.06) 0%, transparent 65%),
    radial-gradient(ellipse 30% 45% at 20% 70%, rgba(160, 130, 220, 0.07) 0%, transparent 60%);
  animation: auroraDrift 20s ease-in-out infinite alternate;
  pointer-events: none;
  z-index: 0;
}

/* Floating particles — small dots drifting upward */
.logins::after {
  content: '';
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  background-image:
    radial-gradient(1.5px 1.5px at 10% 80%, rgba(72, 202, 180, 0.5) 50%, transparent 100%),
    radial-gradient(1px 1px at 25% 60%, rgba(240, 200, 80, 0.4) 50%, transparent 100%),
    radial-gradient(2px 2px at 40% 90%, rgba(160, 130, 220, 0.35) 50%, transparent 100%),
    radial-gradient(1.5px 1.5px at 55% 70%, rgba(72, 202, 180, 0.4) 50%, transparent 100%),
    radial-gradient(1px 1px at 70% 85%, rgba(255, 127, 110, 0.35) 50%, transparent 100%),
    radial-gradient(2px 2px at 85% 65%, rgba(240, 200, 80, 0.3) 50%, transparent 100%),
    radial-gradient(1px 1px at 15% 40%, rgba(160, 130, 220, 0.4) 50%, transparent 100%),
    radial-gradient(1.5px 1.5px at 60% 50%, rgba(72, 202, 180, 0.3) 50%, transparent 100%),
    radial-gradient(1px 1px at 90% 45%, rgba(240, 200, 80, 0.35) 50%, transparent 100%),
    radial-gradient(1.5px 1.5px at 35% 35%, rgba(255, 127, 110, 0.3) 50%, transparent 100%),
    radial-gradient(1px 1px at 50% 25%, rgba(160, 130, 220, 0.35) 50%, transparent 100%),
    radial-gradient(2px 2px at 75% 30%, rgba(72, 202, 180, 0.25) 50%, transparent 100%);
  background-size:
    100% 100%, 100% 100%, 100% 100%,
    100% 100%, 100% 100%, 100% 100%,
    100% 100%, 100% 100%, 100% 100%,
    100% 100%, 100% 100%, 100% 100%;
  animation: particlesFloat 16s linear infinite;
  pointer-events: none;
  z-index: 0;
}

.logins > div {
  display: flex;
  justify-content: center;
  position: relative;
  z-index: 1;
}

/* Aurora drift — slow organic movement */
@keyframes auroraDrift {
  0% {
    transform: translate(0%, 0%) rotate(0deg) scale(1);
  }
  33% {
    transform: translate(5%, -3%) rotate(2deg) scale(1.05);
  }
  66% {
    transform: translate(-3%, 5%) rotate(-1deg) scale(0.97);
  }
  100% {
    transform: translate(2%, -2%) rotate(1deg) scale(1.02);
  }
}

/* Particles float upward and reset */
@keyframes particlesFloat {
  0% {
    transform: translateY(0%);
    opacity: 0.7;
  }
  50% {
    opacity: 1;
  }
  100% {
    transform: translateY(-100%);
    opacity: 0.4;
  }
}

/* ── Top bar ──────────────────────────────────────────────────────── */
.top-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 1rem;
  height: 44px;
  background: var(--glass-bg-heavy);
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  border-bottom: 1px solid var(--glass-border);
  box-shadow: 0 1px 12px rgba(0, 0, 0, 0.3);
  position: relative;
  z-index: 50;
}

.top-bar-left {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.logo-icon { font-size: 1.125rem; }

.logo-text {
  font-size: 1rem;
  font-weight: 800;
  color: var(--accent-gold);
  text-decoration: none;
  letter-spacing: 1px;
  text-shadow: 0 0 10px rgba(240, 200, 80, 0.3);
}

.top-nav {
  display: flex;
  gap: 2px;
  margin-left: 1rem;
}

.top-nav a {
  padding: 0.375rem 0.75rem;
  color: var(--text-muted);
  text-decoration: none;
  font-size: 0.8rem;
  font-weight: 600;
  border-radius: var(--radius);
  transition: all 0.15s;
}

.top-nav a:hover {
  background-color: var(--kh-c-neutrals-pale-500);
  color: var(--text-secondary);
}

.top-nav a.router-link-exact-active {
  background-color: var(--kh-c-neutrals-pale-575);
  color: var(--accent-gold);
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
  background: var(--accent-green);
  box-shadow: var(--glow-green);
}

.connection-dot.disconnected {
  background: var(--accent-red);
  box-shadow: var(--glow-red);
}

.user-info {
  color: var(--text-muted);
  font-size: 0.8rem;
  font-family: var(--font-mono);
}

.top-btn {
  padding: 0.25rem 0.75rem;
  background: transparent;
  color: var(--text-muted);
  border: 1px solid var(--border-color);
  border-radius: var(--radius);
  cursor: pointer;
  font-size: 0.75rem;
  font-weight: 600;
  transition: all 0.15s;
}

.top-btn:hover {
  background: var(--kh-c-neutrals-pale-500);
  color: var(--text-secondary);
  border-color: var(--kh-c-neutrals-pale-240);
}

.theme-select {
  padding: 2px 6px;
  background: var(--bg-inset);
  color: var(--text-muted);
  border: 1px solid var(--border-color);
  border-radius: var(--radius);
  font-size: 0.7rem;
  cursor: pointer;
  outline: none;
}

/* ── Main content ─────────────────────────────────────────────────── */
.main-content {
  flex: 1;
  padding: 1rem 1.5rem;
  max-width: 1440px;
  margin: 0 auto;
  width: 100%;
}

/* ── Buttons (shared across game components) ──────────────────────── */
.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: var(--radius);
  font-size: 0.8rem;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.15s;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.375rem;
  letter-spacing: 0.3px;
}

.btn:disabled { opacity: 0.4; cursor: not-allowed; }

.btn-primary {
  background: var(--kh-c-secondary-success-500);
  color: var(--text-primary);
}
.btn-primary:hover:not(:disabled) {
  background: var(--kh-c-secondary-success-300);
  box-shadow: var(--glow-green);
}

.btn-danger { background: var(--accent-red-dim); color: white; }
.btn-danger:hover:not(:disabled) { background: var(--accent-red); box-shadow: var(--glow-red); }

.btn-success { background: var(--accent-green); color: var(--bg-primary); font-weight: 800; }
.btn-success:hover:not(:disabled) { background: var(--kh-c-secondary-success-300); }

.btn-ghost {
  background: transparent;
  color: var(--text-muted);
  border: 1px solid var(--border-color);
}
.btn-ghost:hover:not(:disabled) {
  background: var(--kh-c-neutrals-pale-500);
  color: var(--text-secondary);
  border-color: var(--kh-c-neutrals-pale-240);
}

.btn-sm { padding: 0.25rem 0.75rem; font-size: 0.7rem; }
.btn-lg { padding: 0.75rem 1.5rem; font-size: 1rem; }

/* ── Inputs ───────────────────────────────────────────────────────── */
.input {
  padding: 0.5rem 1rem;
  background: var(--bg-inset);
  border: 1px solid var(--border-color);
  border-radius: var(--radius);
  color: var(--text-primary);
  font-size: 0.875rem;
  flex: 1;
  outline: none;
  transition: border-color 0.2s;
}

.input:focus { border-color: var(--accent-gold-dim); }

/* ── Cards ────────────────────────────────────────────────────────── */
.card {
  background: var(--glass-bg);
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-lg);
  padding: 1rem;
  box-shadow: var(--shadow-glow), inset 0 1px 0 var(--glass-highlight);
}

.card-header {
  font-size: 0.85rem;
  font-weight: 700;
  margin-bottom: 0.75rem;
  color: var(--accent-gold);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  padding-bottom: 0.5rem;
  border-bottom: 2px solid transparent;
  border-image: linear-gradient(90deg, var(--accent-gold-dim), transparent 80%) 1;
}

/* ── Error Toast ──────────────────────────────────────────────────── */
.error-toast {
  position: fixed;
  top: 3.5rem;
  right: 1.5rem;
  background: var(--accent-red-dim);
  color: white;
  padding: 0.75rem 1.5rem;
  border-radius: var(--radius);
  font-size: 0.85rem;
  font-weight: 600;
  z-index: 1000;
  box-shadow: var(--shadow-lg);
  border: 1px solid var(--accent-red);
}

/* ── Transitions ──────────────────────────────────────────────────── */
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

/* ── Stat Colors (used by game components) ────────────────────────── */
.stat-intelligence { color: var(--kh-c-secondary-info-200); }
.stat-strength { color: var(--kh-c-secondary-danger-200); }
.stat-speed { color: var(--kh-c-secondary-success-200); }
.stat-psyche { color: var(--kh-c-secondary-purple-200); }
.stat-skill { color: var(--accent-gold); }
.stat-moral { color: var(--accent-orange); }
.stat-justice { color: #e879f9; }
.stat-class { color: var(--accent-gold); }
.stat-target { color: var(--text-primary); }

/* ── Text icon badges (INT, STR, SPD, PSY, RND, DEF) ────────────── */
.gi {
  display: inline-block;
  font-size: 9px;
  font-weight: 800;
  padding: 2px 5px;
  border-radius: 4px;
  letter-spacing: 0.5px;
  line-height: 1.3;
  vertical-align: middle;
  text-transform: uppercase;
  transition: transform 0.2s var(--ease-spring);
}
.gi-int { background: rgba(110, 170, 240, 0.12); color: var(--kh-c-secondary-info-200); }
.gi-str { background: rgba(239, 128, 128, 0.12); color: var(--kh-c-secondary-danger-200); }
.gi-spd { background: rgba(200, 185, 50, 0.12); color: var(--kh-c-text-highlight-dim); }
.gi-psy { background: rgba(232, 121, 249, 0.12); color: #e879f9; }
.gi-rnd { background: rgba(255, 255, 255, 0.06); color: hsl(0, 85%, 72%); animation: gi-rainbow 4s linear infinite; }
@keyframes gi-rainbow {
  0%   { color: hsl(0, 85%, 72%); }
  16%  { color: hsl(40, 90%, 65%); }
  33%  { color: hsl(120, 55%, 55%); }
  50%  { color: hsl(200, 80%, 70%); }
  66%  { color: hsl(270, 70%, 72%); }
  83%  { color: hsl(330, 80%, 70%); }
  100% { color: hsl(360, 85%, 72%); }
}
.gi-def { background: rgba(63, 167, 61, 0.12); color: var(--kh-c-secondary-success-200); }
/* Action badges */
.gi-auto { background: rgba(110, 170, 240, 0.10); color: var(--kh-c-secondary-info-300); }
.gi-undo { background: rgba(230, 148, 74, 0.10); color: #e6944a; }
.gi-skip { background: rgba(148, 156, 164, 0.10); color: var(--kh-c-text-primary-700); }
/* Larger variant for stat rows / prominent display */
.gi-lg { font-size: 10px; padding: 2px 5px; }
/* XL variant for lobby / big displays */
.gi-xl { font-size: 16px; padding: 6px 12px; border-radius: 6px; }
/* OK / fail markers */
.gi-ok { color: var(--kh-c-secondary-success-200); font-weight: 800; }
.gi-fail { color: var(--kh-c-secondary-danger-200); font-weight: 800; }
.gi-tie { color: var(--text-muted); }
</style>
