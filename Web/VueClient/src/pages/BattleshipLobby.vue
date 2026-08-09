<script setup lang="ts">
import { onMounted, onUnmounted, computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useBattleshipStore } from 'src/store/battleship'
import { useGameStore } from 'src/store/game'
import {
  signalrService,
  type BattleshipBotVersion,
  type BattleshipLobbyGame,
} from 'src/services/signalr'
import { useTip } from 'src/composables/useTip'
import { message } from 'src/platform/localization'
import 'src/components/battleship/battleship.css'
import BsIcon from 'src/components/battleship/BsIcon.vue'
import CreateGameDialog from 'src/components/battleship/CreateGameDialog.vue'
import StatsPanel from 'src/components/battleship/StatsPanel.vue'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const store = useBattleshipStore()
const gameStore = useGameStore()
const router = useRouter()

let pollInterval: ReturnType<typeof setInterval> | null = null

const games = computed(() => store.lobbyState?.games ?? [])
const createOpen = ref(false)

function resolvedBotVersion(version: BattleshipLobbyGame['botVersion']): BattleshipBotVersion {
  return version === 2 || version === 3 ? version : 1
}

function playerDisplayName(
  name: string,
  isBot: boolean,
  version: BattleshipLobbyGame['botVersion'],
) {
  if (isBot) {
    return message('battleship.lobby.botDisplayName', {
      version: resolvedBotVersion(version),
    })
  }
  return name || message('battleship.lobby.waitingForPlayer')
}

function botTooltip(version: BattleshipLobbyGame['botVersion']) {
  return message('battleship.lobby.botManagedTooltip', {
    version: resolvedBotVersion(version),
  })
}

function canJoinGame(game: BattleshipLobbyGame) {
  return game.canJoin ?? (
    game.vsBot !== true
    && game.player2IsBot
    && game.phase === 'Lobby'
  )
}

function openCreate() {
  if (store.isCreating) return
  store.errorMessage = null
  createOpen.value = true
}

function closeCreate() {
  if (store.isCreating) return
  createOpen.value = false
}

function phaseBadgeClass(phase: string) {
  return 'phase-' + phase.toLowerCase()
}

const phaseDescriptions: Record<string, string> = {
  Lobby: 'Ожидание игроков',
  ArmySelection: 'Выбор армии',
  FleetBuilding: 'Сборка флота',
  ShipPlacement: 'Расстановка кораблей',
  Combat: 'Идёт бой',
  Boarding: 'Абордаж',
  GameOver: 'Игра окончена',
}

onMounted(() => {
  store.initCallbacks()
  store.refreshLobby()
  void store.loadStats()

  pollInterval = setInterval(() => {
    if (gameStore.isConnected) store.refreshLobby()
  }, 3000)

  signalrService.onBattleshipGameCreated = (data) => {
    store.isCreating = false
    createOpen.value = false
    router.push(`/battleship/${data.gameId}`)
  }

  signalrService.onBattleshipGameJoined = (data) => {
    router.push(`/battleship/${data.gameId}`)
  }
})

onUnmounted(() => {
  if (pollInterval) clearInterval(pollInterval)
  signalrService.onBattleshipGameCreated = null
  signalrService.onBattleshipGameJoined = null
  store.cleanupCallbacks()
})

async function handleCreate(vsBot: boolean, botVersion: BattleshipBotVersion) {
  if (store.isCreating) return
  try {
    await store.createGame(vsBot, botVersion)
  }
  catch {
    // The store exposes the localized transport failure in this dialog.
  }
}

async function handleJoin(gameId: string) {
  await store.joinWebGame(gameId)
}
</script>

<template>
  <div class="bs-page bs-lobby bs-phase-lobby">
    <!-- Header -->
    <div class="lobby-header">
      <h2 class="lobby-title bs-title">
        <span class="anchor-deco"><BsIcon icon="anchor" :size="20" /></span>
        MORSKOY BOY
        <span class="anchor-deco"><BsIcon icon="anchor" :size="20" /></span>
      </h2>
      <button
        class="bs-btn bs-btn--primary"
        :disabled="store.isCreating"
        @click="openCreate"
      >
        <BsIcon icon="plus" :size="15" />
        {{ store.isCreating ? 'Создание...' : 'Новая игра' }}
      </button>
    </div>

    <!-- W/L record, streak, first-win bonus -->
    <StatsPanel />

    <div v-if="store.errorMessage && !createOpen" class="lobby-error" role="alert">
      {{ store.errorMessage }}
    </div>

    <!-- Empty state -->
    <div v-if="games.length === 0" class="empty-state">
      <span class="empty-icon"><BsIcon icon="compass" :size="26" /></span>
      <p>Нет активных игр. Создайте новую, чтобы начать!</p>
    </div>

    <!-- Game list -->
    <div v-else class="game-list">
      <div v-for="game in games" :key="game.gameId" class="bs-card game-card">
        <div class="game-card-top">
          <span class="game-id bs-mono">#{{ game.gameId }}</span>
          <span class="phase-badge" :class="phaseBadgeClass(game.phase)" @mouseenter="showTip($event, phaseDescriptions[game.phase] ?? game.phase)" @mousemove="moveTip" @mouseleave="hideTip">{{ game.phase }}</span>
        </div>

        <div class="game-players">
          <span class="player-name" :class="{ 'is-bot': game.player1IsBot }" @mouseenter="game.player1IsBot ? showTip($event, botTooltip(game.botVersion)) : undefined" @mousemove="moveTip" @mouseleave="hideTip">
            {{ playerDisplayName(game.player1Name, game.player1IsBot, game.botVersion) }}
          </span>
          <span class="vs">vs</span>
          <span class="player-name" :class="{ 'is-bot': game.player2IsBot }" @mouseenter="game.player2IsBot ? showTip($event, botTooltip(game.botVersion)) : undefined" @mousemove="moveTip" @mouseleave="hideTip">
            {{ playerDisplayName(game.player2Name, game.player2IsBot, game.botVersion) }}
          </span>
        </div>

        <div class="game-card-footer">
          <span v-if="game.turnNumber > 0" class="turn-info bs-mono" @mouseenter="showTip($event, 'Текущий ход в матче')" @mousemove="moveTip" @mouseleave="hideTip">Ход {{ game.turnNumber }}</span>
          <span v-else />
          <button
            v-if="canJoinGame(game)"
            class="bs-btn bs-btn--primary btn-join"
            @click="handleJoin(game.gameId)"
          >
            Присоединиться
          </button>
          <RouterLink
            v-else
            :to="`/battleship/spectate/${game.gameId}`"
            class="bs-btn bs-btn--sm btn-spectate"
          >
            <BsIcon icon="eye" :size="13" />
            Наблюдать
          </RouterLink>
        </div>
      </div>
    </div>

    <CreateGameDialog
      v-if="createOpen"
      :is-creating="store.isCreating"
      :error-message="store.errorMessage"
      @close="closeCreate"
      @create="handleCreate"
    />

    <!-- Tooltip -->
    <Teleport to="body">
      <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
        {{ tipText }}
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
/* ── Page root ─────────────────────────────────────────── */
.bs-lobby {
  max-width: 800px;
  padding: 2rem 1rem;
}

/* ── Header ────────────────────────────────────────────── */
.lobby-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.lobby-title {
  font-size: 1.4rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  letter-spacing: 2px;
}

.anchor-deco {
  display: inline-flex;
  color: var(--accent-gold);
  opacity: 0.8;
}

/* ── Empty state ───────────────────────────────────────── */
.empty-state {
  text-align: center;
  color: var(--text-muted);
  padding: 3rem 1rem;
  font-size: 0.95rem;
}

.empty-state p {
  margin: 0.75rem 0 0;
}

.empty-icon {
  display: inline-flex;
  color: var(--text-dim);
  opacity: 0.7;
}

/* ── Game list ─────────────────────────────────────────── */
.game-list {
  display: grid;
  gap: 0.75rem;
}

.game-card {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.game-card-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.game-id {
  color: var(--text-dim);
  font-size: 0.75rem;
}

/* ── Phase badges ──────────────────────────────────────── */
.phase-badge {
  --badge-color: var(--text-muted);
  font-size: 0.62rem;
  font-weight: 800;
  padding: 2px 8px;
  border-radius: 7px;
  text-transform: uppercase;
  letter-spacing: 0.8px;
  color: var(--badge-color);
  background: color-mix(in srgb, var(--badge-color) 12%, transparent);
  border: 1px solid color-mix(in srgb, var(--badge-color) 28%, transparent);
}

.phase-lobby { --badge-color: var(--accent-blue); }
.phase-armyselection,
.phase-fleetbuilding { --badge-color: var(--accent-gold); }
.phase-shipplacement { --badge-color: var(--accent-green); }
.phase-combat,
.phase-boarding { --badge-color: var(--accent-red); }
.phase-gameover { --badge-color: var(--accent-purple); }

/* ── Players ───────────────────────────────────────────── */
.game-players {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.92rem;
}

.player-name {
  color: var(--text-primary);
  font-weight: 600;
}

.player-name.is-bot {
  color: var(--text-muted);
  font-style: italic;
}

.vs {
  color: var(--text-dim);
  font-size: 0.72rem;
}

/* ── Footer ────────────────────────────────────────────── */
.game-card-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.turn-info {
  color: var(--text-dim);
  font-size: 0.72rem;
}

.btn-join {
  min-height: 44px;
}

.btn-spectate {
  opacity: 0.85;
}

.lobby-error {
  margin: 0.75rem 0;
  padding: 0.7rem 0.8rem;
  border: 1px solid color-mix(in srgb, var(--accent-red) 45%, var(--border-subtle));
  border-radius: 10px;
  color: var(--accent-red);
  background: color-mix(in srgb, var(--accent-red) 9%, var(--bg-card));
  font-size: 0.82rem;
}
</style>
