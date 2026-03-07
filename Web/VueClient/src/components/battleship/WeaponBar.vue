<script setup lang="ts">
import { useTip } from 'src/composables/useTip'
import { ICONS, renderIcon } from './battleship-icons'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const props = defineProps<{
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
  <div class="wb-bar bs-wood-texture">
    <!-- Iron fitting (left cap) -->
    <div class="wb-fitting wb-fitting--left"></div>

    <span class="wb-label bs-font-body">Оружие:</span>

    <!-- Ballista (always available) -->
    <button
      class="wb-weapon"
      :class="{ 'wb-weapon--active': selectedShotType === 'Ballista' }"
      @mouseenter="showTip($event, weaponTooltip('Ballista') + ' [1]')"
      @mousemove="moveTip"
      @mouseleave="hideTip"
      @click="handleSelect('Ballista', 'Ballista')"
    >
      <span class="wb-icon" v-html="renderIcon(shotTypeToIconKey['Ballista'], 18)"></span>
      <span class="wb-weapon-text">Баллиста</span>
      <span class="wb-hotkey">1</span>
    </button>

    <!-- Special weapons -->
    <button
      v-for="(w, wi) in availableWeapons.filter(w => w.type !== 'Ballista')"
      :key="w.shotType + w.shipName"
      class="wb-weapon"
      :class="[
        selectedShotType === w.shotType ? 'wb-weapon--active' : '',
        !w.hasAmmo ? 'wb-weapon--used' : '',
        w.aimSpeed > 0 ? 'wb-weapon--charging' : ''
      ]"
      :disabled="!w.hasAmmo || w.aimSpeed > 0"
      @mouseenter="showTip($event, weaponTooltip(w.shotType) + (w.aimSpeed > 0 ? ` (заряжается: ${w.aimSpeed} выстр.)` : '') + ` [${wi + 2}]`)"
      @mousemove="moveTip"
      @mouseleave="hideTip"
      @click="w.hasAmmo && w.aimSpeed <= 0 && handleSelect(w.type, w.shotType)"
    >
      <span class="wb-icon" v-html="renderIcon(shotTypeToIconKey[w.shotType] ?? '', 18)"></span>
      <span class="wb-weapon-text">{{ w.label }}</span>
      <span v-if="w.ammo >= 0" class="wb-ammo">({{ w.ammo }})</span>
      <span v-if="w.aimSpeed > 0" class="wb-aim-charge">
        <span class="wb-aim-charge-fill" :style="{ width: Math.max(5, (1 - w.aimSpeed / 20) * 100) + '%' }"></span>
        <span class="wb-aim-charge-text">{{ w.aimSpeed }}</span>
      </span>
      <span class="wb-source">{{ w.shipName }}</span>
      <span class="wb-hotkey">{{ wi + 2 }}</span>
    </button>

    <!-- Iron fitting (right cap) -->
    <div class="wb-fitting wb-fitting--right"></div>
  </div>

  <!-- Tooltip -->
  <Teleport to="body">
    <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
      {{ tipText }}
    </div>
  </Teleport>
</template>

<style scoped>
/* ═══════════════════════════════════════════════════════════════════════════
   WeaponBar — Pirate-themed weapon selection rack
   ═══════════════════════════════════════════════════════════════════════════ */

/* ── Bar container (wooden plank rack) ────────────────────────────────── */
.wb-bar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  margin-bottom: 0.5rem;
  flex-wrap: wrap;
  border: 3px solid var(--bs-wood-mid, #4a2f1a);
  border-radius: 6px;
  position: relative;
}

/* ── Iron fittings (decorative caps) ──────────────────────────────────── */
.wb-fitting {
  width: 6px;
  height: 24px;
  background: linear-gradient(
    180deg,
    #6b6b6b 0%,
    #4a4a4a 30%,
    #7a7a7a 50%,
    #4a4a4a 70%,
    #6b6b6b 100%
  );
  border-radius: 2px;
  flex-shrink: 0;
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.15),
    0 1px 2px rgba(0, 0, 0, 0.4);
}

/* ── Label ────────────────────────────────────────────────────────────── */
.wb-label {
  font-weight: 700;
  font-size: 0.8rem;
  color: var(--bs-parchment-dim, #b09a78);
  white-space: nowrap;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.5);
}

/* ── Weapon button (base) ────────────────────────────────────────────── */
.wb-weapon {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 4px 10px;
  border: 2px solid var(--bs-wood-mid, #4a2f1a);
  border-radius: 4px;
  cursor: pointer;
  user-select: none;

  /* Wood-dark background */
  background-color: var(--bs-wood-dark, #2a1a0e);
  background-image:
    repeating-linear-gradient(
      92deg,
      transparent,
      transparent 6px,
      rgba(74, 47, 26, 0.4) 6px,
      rgba(74, 47, 26, 0.4) 7px
    ),
    repeating-linear-gradient(
      88deg,
      transparent,
      transparent 2px,
      rgba(107, 66, 38, 0.15) 2px,
      rgba(107, 66, 38, 0.15) 3px
    );

  color: var(--bs-parchment, #e8d5b0);
  font-family: 'Crimson Text', 'Georgia', serif;
  font-size: 0.8rem;
  font-weight: 600;
  letter-spacing: 0.02em;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.6);

  box-shadow:
    0 1px 3px rgba(0, 0, 0, 0.3),
    inset 0 1px 0 rgba(255, 255, 255, 0.05);

  transition: transform 0.15s ease, box-shadow 0.15s ease, border-color 0.15s ease;
}

.wb-weapon:hover:not(:disabled) {
  border-color: var(--bs-gold, #d4a847);
  transform: translateY(-1px);
  box-shadow:
    0 3px 8px rgba(0, 0, 0, 0.35),
    0 1px 4px rgba(212, 168, 71, 0.1),
    inset 0 1px 0 rgba(255, 255, 255, 0.08);
}

.wb-weapon:active:not(:disabled) {
  transform: translateY(1px) scale(0.98);
  box-shadow:
    0 1px 2px rgba(0, 0, 0, 0.4),
    inset 0 2px 4px rgba(0, 0, 0, 0.2);
}

/* ── Active weapon (selected) ────────────────────────────────────────── */
.wb-weapon--active {
  border-color: var(--bs-gold, #d4a847);
  transform: translateY(-2px);
  color: var(--bs-gold-bright, #f0c850);
  animation: bs-glow-gold 2s ease-in-out infinite;
  box-shadow:
    0 4px 12px rgba(0, 0, 0, 0.4),
    0 2px 8px rgba(212, 168, 71, 0.2),
    inset 0 1px 0 rgba(255, 255, 255, 0.08);
}

.wb-weapon--active .wb-icon {
  color: var(--bs-gold-bright, #f0c850);
}

/* ── Used weapon (no ammo) ───────────────────────────────────────────── */
.wb-weapon--used {
  opacity: 0.25;
  pointer-events: none;
  text-decoration: line-through;
  border-color: rgba(74, 47, 26, 0.3);
}

/* ── Charging weapon (aim loading) ───────────────────────────────────── */
.wb-weapon--charging {
  opacity: 0.5;
  cursor: wait;
  border-color: rgba(74, 47, 26, 0.4);
  animation: wb-charge-pulse 1.5s ease-in-out infinite;
}

@keyframes wb-charge-pulse {
  0%, 100% { opacity: 0.5; }
  50% { opacity: 0.3; }
}

/* ── Weapon icon ─────────────────────────────────────────────────────── */
.wb-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  color: var(--bs-parchment, #e8d5b0);
  flex-shrink: 0;
}

/* ── Weapon text label ───────────────────────────────────────────────── */
.wb-weapon-text {
  white-space: nowrap;
}

/* ── Ammo count ──────────────────────────────────────────────────────── */
.wb-ammo {
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  font-size: 0.7rem;
  color: var(--bs-gold, #d4a847);
  font-variant-numeric: tabular-nums;
}

/* ── Source ship name ────────────────────────────────────────────────── */
.wb-source {
  font-size: 0.65rem;
  color: var(--bs-parchment-dim, #b09a78);
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
  border-radius: 3px;
  font-size: 0.55rem;
  font-weight: 700;
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  background: linear-gradient(180deg, rgba(107, 107, 107, 0.35) 0%, rgba(60, 60, 60, 0.35) 100%);
  color: var(--bs-parchment-dim, #b09a78);
  margin-left: 4px;
  vertical-align: middle;
  border: 1px solid rgba(107, 107, 107, 0.25);
}

/* ── Aim charge bar ──────────────────────────────────────────────────── */
.wb-aim-charge {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  width: 40px;
  height: 6px;
  background: rgba(74, 47, 26, 0.4);
  border-radius: 3px;
  overflow: hidden;
  position: relative;
  margin-left: 4px;
  border: 1px solid rgba(107, 66, 38, 0.3);
}

.wb-aim-charge-fill {
  height: 100%;
  background: var(--bs-gold, #d4a847);
  border-radius: 3px;
  transition: width 0.3s ease;
}

.wb-aim-charge-text {
  position: absolute;
  font-size: 0.45rem;
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  color: white;
  left: 50%;
  transform: translateX(-50%);
  text-shadow: 0 0 2px rgba(0, 0, 0, 0.8);
}
</style>
