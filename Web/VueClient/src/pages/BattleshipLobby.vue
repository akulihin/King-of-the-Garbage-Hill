<script setup lang="ts">
import { onMounted, onUnmounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useBattleshipStore } from 'src/store/battleship'
import { useGameStore } from 'src/store/game'
import { signalrService } from 'src/services/signalr'
import { useTip } from 'src/composables/useTip'
import { renderIcon } from 'src/components/battleship/battleship-icons'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const store = useBattleshipStore()
const gameStore = useGameStore()
const router = useRouter()

let pollInterval: ReturnType<typeof setInterval> | null = null

const games = computed(() => store.lobbyState?.games ?? [])

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

const anchorIcon = renderIcon('anchor', 16)
const compassIcon = renderIcon('compass', 20)

onMounted(() => {
  store.initCallbacks()
  store.refreshLobby()

  pollInterval = setInterval(() => {
    if (gameStore.isConnected) store.refreshLobby()
  }, 3000)

  signalrService.onBattleshipGameCreated = (data) => {
    store.isCreating = false
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

async function handleCreate() {
  if (store.isCreating) return
  await store.createGame()
}

async function handleJoin(gameId: string) {
  await store.joinWebGame(gameId)
}
</script>

<template>
  <div class="bs-pirate bs-lobby bs-ocean-bg">
    <!-- Header -->
    <div class="lobby-header">
      <h2 class="lobby-title bs-font-title">
        <span class="anchor-deco" v-html="anchorIcon"></span>
        MORSKOY BOY
        <span class="anchor-deco" v-html="anchorIcon"></span>
      </h2>
      <button
        class="btn-pirate"
        :disabled="store.isCreating"
        @click="handleCreate"
      >
        {{ store.isCreating ? 'Создание...' : 'Новая игра' }}
      </button>
    </div>

    <!-- Empty state -->
    <div v-if="games.length === 0" class="empty-state">
      <span class="empty-icon" v-html="compassIcon"></span>
      <p>Нет активных игр. Создайте новую, чтобы начать!</p>
    </div>

    <!-- Game list -->
    <div v-else class="game-list">
      <div v-for="game in games" :key="game.gameId" class="card-wood game-card">
        <div class="game-card-top">
          <span class="game-id bs-font-data">#{{ game.gameId }}</span>
          <span class="phase-badge" :class="phaseBadgeClass(game.phase)" @mouseenter="showTip($event, phaseDescriptions[game.phase] ?? game.phase)" @mousemove="moveTip" @mouseleave="hideTip">{{ game.phase }}</span>
        </div>

        <div class="game-players">
          <span class="player-name" :class="{ 'is-bot': game.player1IsBot }" @mouseenter="game.player1IsBot ? showTip($event, 'Управляется компьютером') : undefined" @mousemove="moveTip" @mouseleave="hideTip">
            {{ game.player1Name || '\u2014' }}
          </span>
          <span class="vs">vs</span>
          <span class="player-name" :class="{ 'is-bot': game.player2IsBot }" @mouseenter="game.player2IsBot ? showTip($event, 'Управляется компьютером') : undefined" @mousemove="moveTip" @mouseleave="hideTip">
            {{ game.player2Name || '\u2014' }}
          </span>
        </div>

        <div class="game-card-footer">
          <span v-if="game.turnNumber > 0" class="turn-info bs-font-data" @mouseenter="showTip($event, 'Текущий ход в матче')" @mousemove="moveTip" @mouseleave="hideTip">Ход {{ game.turnNumber }}</span>
          <span v-else />
          <button
            v-if="game.player2IsBot && game.phase === 'Lobby'"
            class="btn-join"
            @click="handleJoin(game.gameId)"
          >
            Присоединиться
          </button>
          <RouterLink
            v-else
            :to="`/battleship/spectate/${game.gameId}`"
            class="btn-spectate"
          >
            Наблюдать
          </RouterLink>
        </div>
      </div>
    </div>

    <!-- Tooltip -->
    <Teleport to="body">
      <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
        {{ tipText }}
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
@import 'src/components/battleship/battleship-theme.css';

/* ── Page root ─────────────────────────────────────────── */
.bs-lobby {
  max-width: 800px;
  margin: 0 auto;
  min-height: 100vh;
  padding: 2rem 1rem;
  background-color: var(--bs-sea-deep);
}

/* ── Header ────────────────────────────────────────────── */
.lobby-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1.5rem;
}

.lobby-title {
  font-size: 1.5rem;
  font-weight: 800;
  color: var(--bs-gold);
  margin: 0;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  text-shadow: 0 2px 6px rgba(212, 168, 71, 0.3);
}

.anchor-deco {
  display: inline-flex;
  color: var(--bs-gold);
  opacity: 0.7;
}

/* ── Empty state ───────────────────────────────────────── */
.empty-state {
  text-align: center;
  color: var(--bs-parchment-dim);
  padding: 3rem 1rem;
  font-family: 'Crimson Text', 'Georgia', serif;
  font-size: 1rem;
}

.empty-state p {
  margin: 0.75rem 0 0;
}

.empty-icon {
  display: inline-flex;
  color: var(--bs-parchment-dim);
  opacity: 0.5;
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
  color: var(--bs-parchment-dim);
  font-size: 0.8rem;
}

/* ── Phase badges ──────────────────────────────────────── */
.phase-badge {
  font-size: 0.7rem;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 4px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-family: 'Crimson Text', serif;
}

.phase-lobby {
  background: rgba(41, 128, 185, 0.2);
  color: var(--bs-ocean-blue);
}

.phase-armyselection,
.phase-fleetbuilding {
  background: rgba(212, 168, 71, 0.2);
  color: var(--bs-gold);
}

.phase-shipplacement {
  background: rgba(39, 174, 96, 0.2);
  color: var(--bs-poison-green);
}

.phase-combat,
.phase-boarding {
  background: rgba(192, 57, 43, 0.2);
  color: var(--bs-fire-red);
}

.phase-gameover {
  background: rgba(176, 154, 120, 0.15);
  color: var(--bs-parchment-dim);
}

/* ── Players ───────────────────────────────────────────── */
.game-players {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.95rem;
}

.player-name {
  color: var(--bs-parchment);
  font-family: 'Crimson Text', 'Georgia', serif;
  font-weight: 600;
}

.player-name.is-bot {
  color: var(--bs-parchment-dim);
  font-style: italic;
}

.vs {
  color: var(--bs-parchment-dim);
  font-size: 0.75rem;
}

/* ── Footer ────────────────────────────────────────────── */
.game-card-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.turn-info {
  color: var(--bs-parchment-dim);
  font-size: 0.75rem;
}

/* ── Join button (wax-seal style) ──────────────────────── */
.btn-join {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 48px;
  min-height: 48px;
  padding: 10px 18px;
  border: none;
  border-radius: 50% / 45%;
  cursor: pointer;
  user-select: none;

  background: var(--bs-fire-red);
  color: var(--bs-gold-bright);
  font-family: 'Crimson Text', 'Georgia', serif;
  font-size: 0.8rem;
  font-weight: 700;
  letter-spacing: 0.03em;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.5);

  box-shadow:
    0 3px 8px rgba(0, 0, 0, 0.4),
    inset 0 2px 4px rgba(255, 255, 255, 0.12),
    inset 0 -2px 4px rgba(0, 0, 0, 0.2);

  transition: transform 0.15s ease, box-shadow 0.15s ease;
}

.btn-join:hover {
  transform: translateY(-2px) scale(1.04);
  box-shadow:
    0 5px 14px rgba(0, 0, 0, 0.5),
    0 2px 8px rgba(212, 168, 71, 0.2),
    inset 0 2px 4px rgba(255, 255, 255, 0.15),
    inset 0 -2px 4px rgba(0, 0, 0, 0.2);
}

.btn-join:active {
  transform: translateY(1px) scale(0.97);
  box-shadow:
    0 1px 3px rgba(0, 0, 0, 0.4),
    inset 0 3px 6px rgba(0, 0, 0, 0.3);
}

/* ── Spectate button (ghost style) ─────────────────────── */
.btn-spectate {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 6px 14px;
  border: 1px solid transparent;
  border-radius: 4px;
  cursor: pointer;
  user-select: none;
  text-decoration: none;

  background: transparent;
  color: var(--bs-parchment-dim);
  font-family: 'Crimson Text', 'Georgia', serif;
  font-size: 0.8rem;
  font-weight: 600;
  letter-spacing: 0.02em;

  transition: color 0.15s ease, border-color 0.15s ease;
}

.btn-spectate:hover {
  color: var(--bs-parchment);
  border-color: var(--bs-wood-light);
}
</style>
