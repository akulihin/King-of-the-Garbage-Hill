<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useBattleshipStore } from 'src/store/battleship'
import { signalrService } from 'src/services/signalr'
import 'src/components/battleship/battleship.css'
import GameHeader from 'src/components/battleship/GameHeader.vue'
import GameOverlays from 'src/components/battleship/GameOverlays.vue'
import ConfirmDialog from 'src/components/battleship/ConfirmDialog.vue'
import LobbyPhase from 'src/components/battleship/phases/LobbyPhase.vue'
import ArmySelectPhase from 'src/components/battleship/phases/ArmySelectPhase.vue'
import FleetBuildPhase from 'src/components/battleship/phases/FleetBuildPhase.vue'
import PlacementPhase from 'src/components/battleship/phases/PlacementPhase.vue'
import CombatPhase from 'src/components/battleship/phases/CombatPhase.vue'
import GameOverPhase from 'src/components/battleship/phases/GameOverPhase.vue'

const props = defineProps<{ gameId: string }>()

const store = useBattleshipStore()
const router = useRouter()

const phase = computed(() => store.phase)
const overheatShotCount = computed(() => {
  const disabledShipIds = new Set((store.myBoard?.cells ?? []).flatMap(cell =>
    cell.shipId && (cell.isCaptured || cell.isDevastated || cell.isFrozen)
      ? [cell.shipId]
      : []))
  const hasLivingOverheatShip = store.myFleet.some(ship =>
    !ship.isDestroyed
    && !disabledShipIds.has(ship.id)
    && ship.abilities.includes('overheat_after_20_shots'))
  if (!hasLivingOverheatShip || (phase.value !== 'Combat' && phase.value !== 'Boarding')) return null
  return Math.min(store.myPlayer?.totalShotsFired ?? 0, 20)
})
const combatKeyboardLocked = computed(() => !!(
  store.myPlayer?.pendingManeuver
  || store.myPlayer?.pendingCursedBoatDirection
  || store.myPlayer?.pendingAssembly
  || store.myPlayer?.hasPendingMatryoshka
  || store.enemyPlayer?.hasPendingMatryoshka
  || store.myPlayer?.hasPendingBoardingDeployment
  || store.enemyPlayer?.hasPendingBoardingDeployment
  || store.myPlayer?.summons.some(summon =>
    summon.isAlive && summon.type === 'Ram' && summon.waitingForTurnBack)
))

const phaseAccentClass = computed(() => {
  switch (phase.value) {
    case 'ArmySelection':
    case 'FleetBuilding': return 'bs-phase-fleet'
    case 'ShipPlacement': return 'bs-phase-placement'
    case 'Combat':
    case 'Boarding': return 'bs-phase-combat'
    case 'GameOver': return 'bs-phase-gameover'
    default: return 'bs-phase-lobby'
  }
})

// ── Forfeit confirmation ─────────────────────────────────────
const forfeitDialogVisible = ref(false)

function requestForfeit() {
  forfeitDialogVisible.value = true
}

async function confirmForfeit() {
  forfeitDialogVisible.value = false
  await store.forfeit()
}

// ── Keyboard shortcuts ───────────────────────────────────────
function handleKeydown(e: KeyboardEvent) {
  if (forfeitDialogVisible.value) return
  if (e.key === 'Escape' && store.summonDeployMode) {
    store.cancelSummonDeploy()
    e.preventDefault()
    return
  }
  if (e.key === ' ' && phase.value === 'ShipPlacement') {
    e.preventDefault()
    store.toggleOrientation()
    return
  }
  if (phase.value !== 'Combat' && phase.value !== 'Boarding') return
  if (combatKeyboardLocked.value) {
    if (/^[1-9]$/.test(e.key)) e.preventDefault()
    return
  }
  const idx = parseInt(e.key) - 1
  if (idx >= 0 && idx < store.availableWeapons.length) {
    const w = store.availableWeapons[idx]
    if (w.hasAmmo && w.aimSpeed <= 0) void store.selectWeapon(w.type, w.shotType, w.id)
  }
}

// ── Lifecycle ────────────────────────────────────────────────
onMounted(async () => {
  store.initCallbacks()
  window.addEventListener('keydown', handleKeydown)
  await signalrService.joinBattleshipGame(props.gameId)
  if (!store.gameState) {
    await signalrService.requestBattleshipState(props.gameId)
  }
})

onUnmounted(async () => {
  window.removeEventListener('keydown', handleKeydown)
  await signalrService.leaveBattleshipGame(props.gameId)
  store.cleanupCallbacks()
  store.setShotVfxHandler(null)
  store.setCellVfxHandler(null)
})

async function handleLeave() {
  await store.leaveWebGame(props.gameId)
  router.push('/battleship')
}
</script>

<template>
  <div class="bs-page bs-game" :class="phaseAccentClass">
    <GameHeader
      :game-id="store.gameId"
      :phase="phase"
      :turn-number="store.turnNumber"
      :shot-count="store.shotCount"
      :overheat-shot-count="overheatShotCount"
      :is-my-turn="store.isMyTurn"
      mode="player"
      @forfeit="requestForfeit"
    />

    <LobbyPhase v-if="phase === 'Lobby'" @leave="handleLeave" />
    <ArmySelectPhase v-else-if="phase === 'ArmySelection'" />
    <FleetBuildPhase v-else-if="phase === 'FleetBuilding'" />
    <PlacementPhase v-else-if="phase === 'ShipPlacement'" />
    <CombatPhase v-else-if="phase === 'Combat' || phase === 'Boarding'" />
    <GameOverPhase v-else-if="phase === 'GameOver'" />

    <!-- Loading -->
    <div v-else class="phase-content">
      <div class="loading">Загрузка игры...</div>
    </div>

    <!-- Full-screen overlays (phase/turn/boarding/kill-streak) -->
    <GameOverlays />

    <!-- Forfeit confirmation -->
    <ConfirmDialog
      v-if="forfeitDialogVisible"
      title="Сдаться"
      message="Вы уверены, что хотите сдаться?"
      confirm-label="Сдаться"
      cancel-label="Отмена"
      @confirm="confirmForfeit"
      @cancel="forfeitDialogVisible = false"
    />

    <!-- Error Toast -->
    <Transition name="toast-fade">
      <div v-if="store.errorMessage" class="error-toast">{{ store.errorMessage }}</div>
    </Transition>
  </div>
</template>

<style scoped>
.phase-content { margin-top: 0.5rem; }

/* ═══════ Error Toast ═══════ */
.error-toast {
  position: fixed;
  bottom: 1.5rem;
  left: 50%;
  transform: translateX(-50%);
  background: var(--accent-red);
  color: var(--bg-primary);
  padding: 0.5rem 1.25rem;
  border-radius: 10px;
  font-size: 0.85rem;
  font-weight: 700;
  z-index: 100;
  pointer-events: none;
  box-shadow: var(--glow-red);
}
.toast-fade-enter-active, .toast-fade-leave-active { transition: opacity 0.3s ease; }
.toast-fade-enter-from, .toast-fade-leave-to { opacity: 0; }

/* ═══════ Loading ═══════ */
.loading {
  text-align: center;
  color: var(--text-muted);
  padding: 3rem;
}
</style>
