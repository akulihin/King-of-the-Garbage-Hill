<script setup lang="ts">
import { useTip } from 'src/composables/useTip'
import { renderIcon } from './battleship-icons'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

defineProps<{
  selectedShotType: string
  availableWeapons: Array<{
    type: string; shotType: string; label: string; ammo: number
    hasAmmo: boolean; shipName: string; shipRange: string; shipRow: number; aimSpeed: number
  }>
  shotDelayActive: boolean
  shotDelayRemaining: number
  phase: string
}>()

const emit = defineEmits<{
  (e: 'selectWeapon', weaponType: string, shotType: string): void
}>()

const shotTypeToIconKey: Record<string, string> = {
  Ballista: 'ballista',
  WhiteStone: 'whiteStone',
  Buckshot: 'buckshot',
  Incendiary: 'incendiary',
  GreekFire: 'greekFire',
}

function weaponTooltip(shotType: string): string {
  switch (shotType) {
    case 'Ballista': return 'Баллиста — урон 2, безлимитно'
    case 'WhiteStone': return 'Белый камень — урон 8, оглушает (2 выстрела), разрушает модуль палубы'
    case 'Buckshot': return 'Дробь — урон 1, область 2x2 клетки'
    case 'Incendiary': return 'Горючка — сжигает весь корабль, можно по подбитым клеткам'
    case 'GreekFire': return 'Греческий огонь — перманентный огонь на клетке, сжигает корабли'
    default: return ''
  }
}

function handleSelect(weaponType: string, shotType: string) {
  emit('selectWeapon', weaponType, shotType)
}
</script>

<template>
  <div class="wb-bar bs-bar">
    <span class="wb-label">Оружие:</span>

    <div class="bs-seg" role="group" aria-label="Оружие">
      <!-- Ballista (always available) -->
      <button
        class="bs-seg-btn wb-weapon"
        type="button"
        :aria-pressed="selectedShotType === 'Ballista'"
        @mouseenter="showTip($event, weaponTooltip('Ballista') + ' [1]')"
        @mousemove="moveTip"
        @mouseleave="hideTip"
        @click="handleSelect('Ballista', 'Ballista')"
      >
        <span class="wb-icon" v-html="renderIcon(shotTypeToIconKey['Ballista'], 18)"></span>
        <span class="wb-weapon-text">Баллиста</span>
        <span class="wb-hotkey bs-mono">1</span>
      </button>

      <!-- Special weapons -->
      <button
        v-for="(w, wi) in availableWeapons.filter(w => w.type !== 'Ballista')"
        :key="w.shotType + w.shipName"
        class="bs-seg-btn wb-weapon"
        type="button"
        :class="[
          !w.hasAmmo ? 'wb-weapon--used' : '',
          w.aimSpeed > 0 ? 'wb-weapon--charging' : ''
        ]"
        :aria-pressed="selectedShotType === w.shotType"
        :disabled="!w.hasAmmo || w.aimSpeed > 0"
        @mouseenter="showTip($event, weaponTooltip(w.shotType) + (w.aimSpeed > 0 ? ` (заряжается: ${w.aimSpeed} выстр.)` : '') + ` [${wi + 2}]`)"
        @mousemove="moveTip"
        @mouseleave="hideTip"
        @click="w.hasAmmo && w.aimSpeed <= 0 && handleSelect(w.type, w.shotType)"
      >
        <span class="wb-icon" v-html="renderIcon(shotTypeToIconKey[w.shotType] ?? '', 18)"></span>
        <span class="wb-weapon-text">{{ w.label }}</span>
        <span v-if="w.ammo >= 0" class="wb-ammo bs-mono">({{ w.ammo }})</span>
        <span v-if="w.aimSpeed > 0" class="wb-aim-charge">
          <span class="wb-aim-charge-fill" :style="{ width: Math.max(5, (1 - w.aimSpeed / 20) * 100) + '%' }"></span>
          <span class="wb-aim-charge-text bs-mono">{{ w.aimSpeed }}</span>
        </span>
        <span class="wb-source">{{ w.shipName }}</span>
        <span class="wb-hotkey bs-mono">{{ wi + 2 }}</span>
      </button>
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
.wb-bar {
  margin-bottom: 0.5rem;
}

.wb-label {
  font-weight: 900;
  font-size: 0.62rem;
  letter-spacing: 1.5px;
  text-transform: uppercase;
  color: var(--text-dim);
  white-space: nowrap;
  user-select: none;
}

/* ── Used weapon (no ammo) ───────────────────────────────────────────── */
.wb-weapon--used {
  opacity: 0.3;
  pointer-events: none;
  text-decoration: line-through;
}

/* ── Charging weapon (aim loading) ───────────────────────────────────── */
.wb-weapon--charging {
  cursor: wait;
  animation: wb-charge-pulse 1.5s ease-in-out infinite;
}

@keyframes wb-charge-pulse {
  0%, 100% { opacity: 0.55; }
  50% { opacity: 0.35; }
}

/* ── Weapon icon ─────────────────────────────────────────────────────── */
.wb-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  flex-shrink: 0;
}

/* ── Weapon text label ───────────────────────────────────────────────── */
.wb-weapon-text {
  white-space: nowrap;
}

/* ── Ammo count ──────────────────────────────────────────────────────── */
.wb-ammo {
  font-size: 0.68rem;
  color: var(--accent-gold);
  font-variant-numeric: tabular-nums;
}

/* ── Source ship name ────────────────────────────────────────────────── */
.wb-source {
  font-size: 0.62rem;
  font-weight: 600;
  color: var(--text-dim);
  margin-left: 2px;
  white-space: nowrap;
}

/* ── Hotkey badge ────────────────────────────────────────────────────── */
.wb-hotkey {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  border-radius: 4px;
  font-size: 0.55rem;
  font-weight: 700;
  border: 1px solid var(--glass-border);
  background: rgba(255, 255, 255, 0.04);
  color: var(--text-dim);
  margin-left: 4px;
}

/* ── Aim charge bar ──────────────────────────────────────────────────── */
.wb-aim-charge {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  width: 40px;
  height: 6px;
  background: var(--bg-inset);
  border-radius: 3px;
  overflow: hidden;
  position: relative;
  margin-left: 4px;
  border: 1px solid var(--glass-border);
}

.wb-aim-charge-fill {
  height: 100%;
  background: var(--accent-gold);
  border-radius: 3px;
  transition: width 0.3s ease;
}

.wb-aim-charge-text {
  position: absolute;
  font-size: 0.45rem;
  color: var(--text-primary);
  left: 50%;
  transform: translateX(-50%);
  text-shadow: 0 0 2px rgba(0, 0, 0, 0.8);
}
</style>
