<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { BattleshipPlayerState, BattleshipPendingSummon } from 'src/services/signalr'
import { renderIcon } from './battleship-icons'
import { useTip } from 'src/composables/useTip'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const props = defineProps<{
  myPlayer: BattleshipPlayerState | null
  phase: string
  shotCount: number
  canDeploySummon: boolean
  availableSummons: string[]
  summonDeployMode: {
    type: string
    pendingId?: string
    pendingCols?: number[]
    reentryDirection?: string
    reentryRow?: number
    reentryCol?: number
  } | null
}>()

const emit = defineEmits<{
  enterDeploy: []
  enterPendingDeploy: [ps: BattleshipPendingSummon]
  cancelDeploy: []
  setSummonType: [type: string]
}>()

// ── Internal State ──────────────────────────────────────────────
const summonType = ref('Ram')

watch(summonType, (val) => {
  emit('setSummonType', val)
})

// Keep the selection valid when availability changes (ТЗ #11/#12)
watch(() => props.availableSummons, (list) => {
  if (list.length > 0 && !list.includes(summonType.value)) summonType.value = list[0]
}, { immediate: true })

// ── Summon descriptions ─────────────────────────────────────────
const summonDescriptions: Record<string, string> = {
  Ram: 'Таран — скорость 2, урон 4, погибает при столкновении, разведка пути',
  Scout: 'Разведчик — скорость 1, отложенная разведка в Space, не открывает при заморозке',
  PirateBoat: 'Пиратская лодка — скорость 1, захватывает 1-2-палубные корабли, разбивается о 3-4-палубные',
  Brander: 'Брандер — скорость 1, дополнительный призыв (1 за матч), не проходит сквозь живые палубы, стреляйте в него для подрыва',
  CursedBoat: 'Проклятая лодка — опустошает корабль, затем меняет направление и продолжает путь',
}

// ── Helpers ─────────────────────────────────────────────────────
const summonTypeNameRu: Record<string, string> = {
  Ram: 'Таран',
  Scout: 'Разведчик',
  Brander: 'Брандер',
  CursedBoat: 'Проклятый',
  PirateBoat: 'Пиратская лодка',
}

const summonIconKey: Record<string, string> = {
  Ram: 'ram',
  Scout: 'scout',
  Brander: 'brander',
  CursedBoat: 'cursedBoat',
  PirateBoat: 'pirateBoat',
}

const summonOrder = ['Ram', 'Scout', 'PirateBoat', 'Brander', 'CursedBoat']

const selectedWaitingSummon = computed(() => props.myPlayer?.summons?.some(s =>
  s.type === summonType.value && s.waitingForTurnBack) ?? false)

function nameRu(type: string): string {
  return summonTypeNameRu[type] ?? type
}

function iconFor(type: string): string {
  const key = summonIconKey[type]
  return key ? renderIcon(key, 16) : ''
}

function posLabel(row: number, col: number): string {
  return String.fromCharCode(65 + col) + (row + 1)
}
</script>

<template>
  <div class="summon-bar-root">
    <!-- 1. Summon Deployment Bar -->
    <div class="summon-bar bs-bar">
      <span class="sb-label">Обычные призывы ({{ myPlayer?.summonSlotsUsed ?? 0 }}/{{ myPlayer?.maxSummonSlots ?? 4 }}):</span>
      <div class="bs-seg" role="group" aria-label="Тип призыва">
        <button
          v-for="type in summonOrder.filter(t => availableSummons.includes(t))"
          :key="type"
          class="bs-seg-btn"
          type="button"
          :aria-pressed="summonType === type"
          @mouseenter="showTip($event, summonDescriptions[type] ?? '')"
          @mousemove="moveTip"
          @mouseleave="hideTip"
          @click="summonType = type"
        >
          <span class="sb-seg-icon" v-html="iconFor(type)" />
          {{ nameRu(type) }}
        </button>
      </div>
      <button
        class="bs-btn bs-btn--primary sb-deploy-btn"
        :disabled="!canDeploySummon"
        @mouseenter="showTip($event, canDeploySummon ? (selectedWaitingSummon ? 'Выберите подсвеченную клетку на краю вражеского поля' : 'Выберите клетку на строке 1 вражеского поля') : '')"
        @mousemove="moveTip"
        @mouseleave="hideTip"
        @click="emit('enterDeploy')"
      >
        {{ selectedWaitingSummon ? 'Вернуть на карту' : 'Разместить на карте' }}
      </button>
      <span v-if="!canDeploySummon && myPlayer" class="sb-hint">
        <template v-if="!selectedWaitingSummon && summonType === 'Brander' && myPlayer.branderUsed">
          Брандер уже использован
        </template>
        <template v-else-if="!selectedWaitingSummon && summonType !== 'Brander' && myPlayer.summonSlotsUsed >= (myPlayer.maxSummonSlots ?? 4)">
          Лимит обычных призывов исчерпан
        </template>
        <template v-else-if="myPlayer.summonCooldownRemaining > 0">
          Перезарядка: {{ myPlayer.summonCooldownRemaining }} выстр.
        </template>
        <template v-else>
          Нужно разведать {{ 5 * ((myPlayer?.summonSlotsUsed ?? 0) + 1) }} клеток
        </template>
      </span>
    </div>

    <!-- 2. Active Summons Status -->
    <div v-if="myPlayer?.summons?.filter(s => s.isAlive).length" class="summon-status-bar bs-bar">
      <span class="sb-label">Активные призывы:</span>
      <span
        v-for="s in myPlayer!.summons.filter(ss => ss.isAlive)"
        :key="s.id"
        class="bs-chip"
        :class="'summon-' + s.type.toLowerCase()"
      >
        <span class="summon-chip-icon" v-html="iconFor(s.type)" />
        {{ nameRu(s.type) }}
        <span class="summon-pos bs-mono">{{ posLabel(s.row, s.col) }}</span>
        <span
          v-if="s.waitingForTurnBack"
          class="summon-wait"
          @mouseenter="showTip($event, 'Ожидает разворота')"
          @mousemove="moveTip"
          @mouseleave="hideTip"
        >&#x21A9;</span>
      </span>
    </div>

    <!-- 3. Pending Summons -->
    <div v-if="myPlayer?.pendingSummons?.length" class="pending-bar bs-bar">
      <span class="sb-label">Ожидающие призывы:</span>
      <div v-for="ps in myPlayer!.pendingSummons" :key="ps.id" class="pending-entry">
        <span class="pending-name">{{ ps.sourceShipName || ps.type }}</span>
        <span v-if="ps.isBoarding" class="bs-chip bs-chip--red boarding-badge">абордаж</span>
        <span v-if="ps.allowedColumns.length" class="sb-hint">
          (столбцы: {{ ps.allowedColumns.map(c => String.fromCharCode(65 + c)).join(', ') }})
        </span>
        <button class="bs-btn bs-btn--primary sb-deploy-btn" @click="emit('enterPendingDeploy', ps)">
          Разместить на карте
        </button>
      </div>
    </div>

    <!-- 4. Deploy Mode Banner -->
    <div v-if="summonDeployMode" class="bs-banner bs-banner--info deploy-banner">
      <span class="deploy-banner-text">
        <template v-if="summonDeployMode.reentryDirection">
          Выберите подсвеченную клетку на краю поля для возвращения {{ nameRu(summonDeployMode.type) }}
        </template>
        <template v-else>
          Выберите клетку на первой строке вражеского поля для размещения {{ nameRu(summonDeployMode.type) }}
        </template>
      </span>
      <span v-if="summonDeployMode.pendingCols" class="deploy-cols bs-mono">
        (столбцы: {{ summonDeployMode.pendingCols.map(c => String.fromCharCode(65 + c)).join(', ') }})
      </span>
      <button class="bs-btn bs-btn--sm sb-cancel-btn" @click="emit('cancelDeploy')">Отмена</button>
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
/* ── Summon Bar Root ────────────────────────────────────── */
.summon-bar-root {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-top: 0.5rem;
}

.pending-bar {
  flex-direction: column;
  align-items: flex-start;
}

/* ── Labels ────────────────────────────────────────────── */
.sb-label {
  font-weight: 900;
  font-size: 0.62rem;
  letter-spacing: 1.5px;
  text-transform: uppercase;
  color: var(--text-dim);
  white-space: nowrap;
  user-select: none;
}

.sb-seg-icon {
  display: inline-flex;
  align-items: center;
  line-height: 0;
}

/* ── Deploy button ─────────────────────────────────────── */
.sb-deploy-btn {
  min-height: 34px;
  padding: 4px 14px;
  font-size: 0.75rem;
}

/* ── Hint text ─────────────────────────────────────────── */
.sb-hint {
  font-size: 0.7rem;
  color: var(--text-dim);
}

/* ── Summon chips ──────────────────────────────────────── */
.summon-chip-icon {
  display: inline-flex;
  align-items: center;
  line-height: 0;
}

.summon-ram { --bs-chip-color: var(--accent-red); }
.summon-scout { --bs-chip-color: var(--accent-blue); }
.summon-brander { --bs-chip-color: var(--accent-orange); }
.summon-cursedboat { --bs-chip-color: var(--accent-purple); }
.summon-pirateboat { --bs-chip-color: var(--accent-gold); }

/* ── Position label in chip ────────────────────────────── */
.summon-pos {
  font-size: 0.65rem;
  opacity: 0.7;
}

/* ── Waiting-for-turn-back indicator ───────────────────── */
.summon-wait {
  font-size: 0.85rem;
  animation: sb-pulse 1.2s ease-in-out infinite;
}

@keyframes sb-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

/* ── Pending entries ───────────────────────────────────── */
.pending-entry {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.pending-name {
  font-weight: 600;
  color: var(--text-secondary);
  font-size: 0.8rem;
}

/* ── Boarding badge ────────────────────────────────────── */
.boarding-badge {
  text-transform: uppercase;
  letter-spacing: 0.3px;
  font-size: 0.6rem;
}

/* ── Deploy Mode Banner ────────────────────────────────── */
.deploy-banner {
  margin-bottom: 0;
}

.deploy-banner-text {
  font-weight: 700;
  font-size: 0.8rem;
}

.deploy-cols {
  font-size: 0.72rem;
}

.sb-cancel-btn {
  margin-left: 0.5rem;
}
</style>
