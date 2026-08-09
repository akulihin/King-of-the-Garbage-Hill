<script setup lang="ts">
import { computed } from 'vue'
import { useTip } from 'src/composables/useTip'
import { message } from 'src/platform/localization'
import BsIcon from './BsIcon.vue'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const props = withDefaults(defineProps<{
  gameId: string | null
  phase: string
  turnNumber: number
  shotCount: number
  overheatShotCount?: number | null
  isMyTurn?: boolean
  /** 'player' shows the your-turn indicator + forfeit; 'spectator' shows who is acting. */
  mode?: 'player' | 'spectator'
  /** Spectator mode: name of the player whose turn it is. */
  currentTurnName?: string
}>(), {
  isMyTurn: false,
  overheatShotCount: null,
  mode: 'player',
  currentTurnName: '',
})

const emit = defineEmits<{
  forfeit: []
}>()

const inCombat = computed(() => props.phase === 'Combat' || props.phase === 'Boarding')

const phaseBadgeClass = computed(() => 'phase-' + props.phase.toLowerCase())
</script>

<template>
  <div class="game-header bs-bar">
    <div class="header-left">
      <RouterLink to="/battleship" class="bs-btn bs-btn--sm back-btn">
        <BsIcon icon="back" :size="13" />
        Назад
      </RouterLink>
      <span class="game-tag bs-mono">#{{ gameId }}</span>
      <span class="phase-badge" :class="phaseBadgeClass">{{ phase }}</span>
    </div>
    <div class="header-right">
      <template v-if="inCombat || mode === 'spectator'">
        <span v-if="turnNumber > 0" class="turn-badge bs-mono" @mouseenter="showTip($event, 'Номер текущего хода')" @mousemove="moveTip" @mouseleave="hideTip">Ход {{ turnNumber }}</span>
        <span v-if="shotCount > 0" class="turn-badge bs-mono" @mouseenter="showTip($event, 'Общий счётчик выстрелов в матче')" @mousemove="moveTip" @mouseleave="hideTip">Выстрел {{ shotCount }}</span>
        <span
          v-if="overheatShotCount !== null"
          class="overheat-badge bs-mono"
          :class="{ 'overheat-badge--critical': overheatShotCount >= 15 }"
          :style="{ '--overheat-progress': `${overheatShotCount / 20 * 100}%` }"
          role="status"
          @mouseenter="showTip($event, message('battleship.ability.overheat.description'))"
          @mousemove="moveTip"
          @mouseleave="hideTip"
        >
          <BsIcon icon="flame" :size="13" />
          {{ message('battleship.overheat.counterLabel') }} {{ overheatShotCount }}/20
        </span>
      </template>
      <template v-if="mode === 'player' && inCombat">
        <span class="turn-indicator" :class="{ 'my-turn': isMyTurn }">
          {{ isMyTurn ? 'Ваш ход' : 'Ход противника' }}
        </span>
        <button class="bs-btn bs-btn--sm bs-btn--danger" @mouseenter="showTip($event, 'Сдаться и проиграть матч')" @mousemove="moveTip" @mouseleave="hideTip" @click="emit('forfeit')">Сдаться</button>
      </template>
      <template v-else-if="mode === 'spectator' && currentTurnName">
        <span class="turn-indicator my-turn">
          Ходит: {{ currentTurnName }}
        </span>
      </template>
    </div>
  </div>

  <!-- Tooltip -->
  <Teleport to="body">
    <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
      {{ tipText }}
    </div>
  </Teleport>
</template>

<style scoped>
.game-header {
  justify-content: space-between;
  margin-bottom: 1rem;
}
.header-left, .header-right {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}
.back-btn {
  gap: 4px;
}
.game-tag {
  color: var(--text-dim);
  font-size: 0.75rem;
}

/* ── Phase badges ── */
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

/* ── Turn info ── */
.turn-badge {
  font-size: 0.72rem;
  color: var(--text-dim);
}
.overheat-badge {
  --overheat-progress: 0%;
  position: relative;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  overflow: hidden;
  padding: 3px 8px;
  border: 1px solid color-mix(in srgb, var(--accent-orange) 42%, transparent);
  border-radius: 8px;
  color: var(--accent-orange);
  background:
    linear-gradient(
      90deg,
      color-mix(in srgb, var(--accent-orange) 20%, transparent) var(--overheat-progress),
      transparent var(--overheat-progress)
    ),
    rgba(255, 255, 255, 0.025);
  font-size: 0.72rem;
  font-weight: 800;
  transition: border-color 0.2s, box-shadow 0.2s, color 0.2s;
}
.overheat-badge--critical {
  color: var(--accent-red);
  border-color: color-mix(in srgb, var(--accent-red) 58%, transparent);
  box-shadow: 0 0 12px color-mix(in srgb, var(--accent-red) 25%, transparent);
}
.turn-indicator {
  font-size: 0.78rem;
  font-weight: 700;
  padding: 3px 10px;
  border-radius: 8px;
  color: var(--text-muted);
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid var(--glass-border);
  transition: all 0.15s;
}
.turn-indicator.my-turn {
  color: var(--accent-gold);
  background: color-mix(in srgb, var(--accent-gold) 12%, transparent);
  border-color: color-mix(in srgb, var(--accent-gold) 45%, transparent);
  box-shadow: var(--glow-gold);
}
</style>
