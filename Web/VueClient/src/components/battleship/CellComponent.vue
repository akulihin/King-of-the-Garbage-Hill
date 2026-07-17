<script setup lang="ts">
import { computed } from 'vue'
import type { BattleshipCell } from 'src/services/signalr'
import { renderIcon } from './battleship-icons'

const props = defineProps<{
  cell: BattleshipCell | undefined
  isEnemy?: boolean
  isPlacement?: boolean
  clickable?: boolean
  highlighted?: boolean
  zoneHighlight?: boolean
  spaceHighlight?: boolean
  blocked?: boolean
  shotType?: string
  anim?: string
  shipName?: string
  lastShot?: boolean
  marked?: boolean
  shipEdges?: { top: boolean; right: boolean; bottom: boolean; left: boolean }
  summonTrail?: string | boolean
  rangeOverlay?: string
}>()

const cellStyle = computed(() => {
  if (!props.cell) return {}
  return { '--cell-row': props.cell.row, '--cell-col': props.cell.col } as Record<string, string | number>
})

const cellClass = computed(() => {
  if (!props.cell) return ['cell', 'cell-unknown']
  const classes = ['cell']

  // Priority order per spec section 11
  if (props.cell.isDestroyed) classes.push('cell-destroyed')
  else if (props.cell.isDevastated) classes.push('cell-devastated')
  else if (props.cell.isFirePermanent) classes.push('cell-fire-permanent')
  else if (props.cell.isBurning) classes.push('cell-burning')
  else if (props.cell.isFrozen) classes.push('cell-frozen')
  else if (props.cell.isBurnResistMarked) classes.push('cell-burn-resist')
  else if (props.cell.isScratched) classes.push('cell-scratched')
  else if (props.cell.isHit && props.cell.hasShip) classes.push('cell-hit')
  else if (props.cell.isHit) classes.push('cell-hit-empty')
  else if (props.cell.isCaptured) classes.push('cell-captured')
  else if (props.cell.isDodgeMarked) classes.push('cell-dodge-mark')
  else if (props.cell.isMiss) classes.push('cell-miss')
  else if (props.cell.hasShip && !props.isEnemy) classes.push('cell-ship')
  else if (props.cell.hasShip && props.cell.isRevealed) classes.push('cell-revealed-ship')
  else if (!props.cell.isRevealed && props.isEnemy) classes.push('cell-fog')
  else classes.push('cell-empty')

  // ТЗ #17: creatures render as an orange icon overlay — the base class above keeps
  // showing the cell's own status (hit/miss/fire/…) underneath
  if (props.cell.hasSummon) classes.push('cell-has-summon')

  if (props.clickable) {
    classes.push('cell-clickable')
  }
  if (props.blocked) classes.push('cell-blocked')
  if (props.spaceHighlight && props.isPlacement) classes.push('cell-space')
  if (props.zoneHighlight && props.isPlacement) classes.push('cell-zone')
  if (props.highlighted && props.isPlacement) classes.push('cell-preview')
  else if (props.highlighted) classes.push('cell-highlighted')

  // Shot animation class
  if (props.anim) classes.push(props.anim)

  // Last shot marker
  if (props.lastShot) classes.push('cell-last-shot')

  // Marked cell overlay
  if (props.marked) classes.push('cell-marked')

  // Ship silhouette borders
  if (props.shipEdges) {
    classes.push('cell-ship-outline')
    if (props.shipEdges.top) classes.push('ship-edge-top')
    if (props.shipEdges.right) classes.push('ship-edge-right')
    if (props.shipEdges.bottom) classes.push('ship-edge-bottom')
    if (props.shipEdges.left) classes.push('ship-edge-left')
  }

  // Summon trail (type-specific)
  if (props.summonTrail) {
    classes.push('cell-summon-trail')
    if (typeof props.summonTrail === 'string') {
      classes.push('trail-' + props.summonTrail.toLowerCase())
    }
  }

  // Range overlay (poison, explosion, freeze, brander)
  if (props.rangeOverlay) {
    classes.push('cell-range-overlay', 'range-' + props.rangeOverlay)
  }

  return classes
})

const cellIconHtml = computed(() => {
  if (!props.cell) return ''
  // ТЗ #17: the creature icon always wins — the cell status shows through the background
  if (props.cell.hasSummon) {
    switch (props.cell.summonType) {
      case 'Ram': return renderIcon('ram', 14)
      case 'Scout': return renderIcon('scout', 14)
      case 'Brander': return renderIcon('brander', 14)
      case 'CursedBoat': return renderIcon('cursedBoat', 14)
      case 'PirateBoat': return renderIcon('pirateBoat', 14)
      default: return renderIcon('anchor', 14)
    }
  }
  if (props.cell.isDestroyed) return renderIcon('destroyed', 16)
  if (props.cell.isDevastated) return renderIcon('devastated', 16)
  if (props.cell.isFirePermanent) return renderIcon('firePermanent', 14)
  if (props.cell.isBurning) return renderIcon('burning', 14)
  if (props.cell.isFrozen) return renderIcon('frozen', 14)
  if (props.cell.isScratched) return renderIcon('scratched', 14)
  if (props.cell.isHit && props.cell.hasShip) return renderIcon('hit', 14)
  if (props.cell.isCaptured) return renderIcon('captured', 14)
  if (props.cell.isMiss) return renderIcon('miss', 10)
  if (props.blocked) return ''
  return ''
})

const colLabels = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J']

const summonNames: Record<string, string> = {
  Ram: 'Таран', Scout: 'Разведчик', Brander: 'Брандер',
  CursedBoat: 'Проклятый корабль', PirateBoat: 'Пиратская лодка',
}

defineEmits<{
  (e: 'tipShow', ev: MouseEvent, text: string): void
  (e: 'tipMove', ev: MouseEvent): void
  (e: 'tipHide'): void
}>()

const cellTooltip = computed(() => {
  if (!props.cell) return ''
  const coord = ` (${colLabels[props.cell.col] ?? props.cell.col}${props.cell.row + 1})`
  const ship = props.shipName ? ` — ${props.shipName}` : ''

  let base = ''
  if (props.cell.hasSummon) {
    base = (props.cell.summonType && summonNames[props.cell.summonType]) ?? 'Призыв'
    // ТЗ #1: enemy creature in the penalty zone (rows 1-3 of the own board)
    if (!props.isEnemy && props.cell.row <= 2) {
      base = `Штраф за убийство суммона в этой зоне (кроме убийства сразу после появления) | ${base}`
    }
  }
  else if (props.cell.isDestroyed) base = `Уничтожено${ship}`
  else if (props.cell.isDevastated) base = `Опустошено`
  else if (props.cell.isBurning || props.cell.isFirePermanent) base = `Горит${ship}`
  else if (props.cell.isBurnResistMarked) base = `Огнеупорный корабль — устоял против огня${ship}`
  else if (props.cell.isFrozen) base = `Заморожено`
  else if (props.cell.isCaptured) base = `Захвачено`
  else if (props.cell.isScratched) base = `Поцарапано — можно стрелять повторно`
  else if (props.cell.isHit && props.cell.hasShip) base = `Попадание${ship}`
  else if (props.cell.isDodgeMarked) base = `Юркая единичка увернулась — баллиста бессильна`
  else if (props.cell.isMiss) base = `Промах`
  else if (props.cell.hasShip && !props.isEnemy) base = `Корабль${ship}`
  else if (props.cell.hasShip && props.cell.isRevealed) base = `Обнаружен корабль`
  else if (props.isEnemy && !props.cell.isRevealed) base = `Неизведано`

  const extras: string[] = []
  const addState = (active: boolean, label: string) => {
    if (active && base !== label && !extras.includes(label)) extras.push(label)
  }
  addState(props.cell.isDestroyed, 'Уничтожено')
  addState(props.cell.isDevastated, 'Опустошено')
  addState(props.cell.isFirePermanent || props.cell.isBurning, 'Горит')
  addState(props.cell.isFrozen, 'Заморожено')
  addState(props.cell.isBurnResistMarked, 'Огнеупорность')
  addState(props.cell.isScratched, 'Поцарапано')
  addState(props.cell.isCaptured, 'Захвачено')
  addState(props.cell.isDodgeMarked, 'Уклонение')
  if (props.summonTrail) {
    extras.push(typeof props.summonTrail === 'string'
      ? `След: ${summonNames[props.summonTrail] ?? props.summonTrail}`
      : 'След призыва')
  }
  if (props.lastShot) extras.push('Последний выстрел')
  if (props.marked) extras.push('Метка')

  const parts = [base, ...extras].filter(Boolean)
  if (parts.length === 0) return coord.trim()
  return parts.join(' | ') + coord
})
</script>

<template>
  <div :class="cellClass" :style="cellStyle"
    @mouseenter="$emit('tipShow', $event, cellTooltip)"
    @mousemove="$emit('tipMove', $event)"
    @mouseleave="$emit('tipHide')"
  >
    <span v-if="cellIconHtml" class="cell-icon" v-html="cellIconHtml"></span>
  </div>
</template>

<style scoped>
.cell {
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.8rem;
  font-weight: 700;
  transition: all 0.15s;
  position: relative;
  user-select: none;
}

/* -- Base states -------------------------------------------------- */
.cell-empty { background: color-mix(in srgb, var(--accent-blue) 8%, var(--bg-primary)); }
.cell-fog {
  background: color-mix(in srgb, var(--accent-blue) 4%, var(--bg-card));
  background-image: linear-gradient(
    160deg,
    transparent 30%,
    var(--glass-highlight) 50%,
    transparent 70%
  );
}
.cell-unknown { background: color-mix(in srgb, var(--accent-blue) 8%, var(--bg-primary)); }

.cell-ship {
  background: color-mix(in srgb, var(--accent-blue) 42%, var(--bg-card));
  background-image: linear-gradient(
    90deg,
    rgba(255, 255, 255, 0.06) 25%,
    transparent 25%,
    transparent 50%,
    rgba(255, 255, 255, 0.06) 50%,
    rgba(255, 255, 255, 0.06) 75%,
    transparent 75%
  );
  background-size: 8px 8px;
  opacity: 0.8;
  animation: ship-bob 3s ease-in-out infinite;
  animation-delay: calc(var(--cell-row, 0) * 0.15s + var(--cell-col, 0) * 0.1s);
}

.cell-revealed-ship {
  background: color-mix(in srgb, var(--accent-purple, #8b5cf6) 22%, var(--bg-card));
  box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--accent-purple, #8b5cf6) 55%, transparent);
}

.cell-hit {
  background: color-mix(in srgb, var(--bs-hit, var(--accent-red)) 30%, var(--bg-primary));
  color: var(--bs-hit, var(--accent-red));
  transform: rotate(0.5deg);
}
.cell-hit-empty {
  background: color-mix(in srgb, var(--accent-blue) 8%, var(--bg-primary));
  color: var(--text-dim);
}
.cell-miss {
  background: color-mix(in srgb, var(--accent-blue) 8%, var(--bg-primary));
  color: color-mix(in srgb, var(--accent-blue) 70%, var(--text-muted));
}

.cell-scratched {
  background: color-mix(in srgb, var(--bs-gold, var(--accent-gold)) 20%, var(--bg-primary));
  background-image: linear-gradient(
    135deg,
    transparent 40%,
    rgba(255, 255, 255, 0.12) 45%,
    rgba(255, 255, 255, 0.12) 55%,
    transparent 60%
  );
  color: var(--bs-gold, var(--accent-gold));
}

.cell-burning {
  background: color-mix(in srgb, var(--bs-burn, var(--accent-orange)) 55%, var(--bg-primary));
  color: var(--text-primary);
  animation: burn-pulse 0.8s ease-in-out infinite alternate;
}

/* Creature = icon overlay, no background fill so cell statuses stay visible */
.cell-has-summon {
  color: var(--bs-burn, var(--accent-orange));
  font-size: 0.7rem;
}
.cell-has-summon :deep(svg) {
  filter: drop-shadow(0 0 2px rgba(0, 0, 0, 0.9));
}

/* A nimble ship dodged a ballista here — static green mark */
.cell-dodge-mark {
  background: color-mix(in srgb, var(--bs-poison, var(--accent-green)) 28%, var(--bg-primary));
  box-shadow: inset 0 0 0 2px color-mix(in srgb, var(--bs-poison, var(--accent-green)) 55%, transparent);
  color: var(--bs-poison, var(--accent-green));
}

.cell-destroyed {
  background: color-mix(in srgb, var(--bs-hit, var(--accent-red)) 16%, var(--bg-inset));
  background-image: radial-gradient(circle at 30% 30%, color-mix(in srgb, var(--bs-hit, var(--accent-red)) 22%, transparent) 0%, transparent 60%),
                     radial-gradient(circle at 70% 70%, color-mix(in srgb, var(--bs-burn, var(--accent-orange)) 14%, transparent) 0%, transparent 50%);
  color: var(--text-muted);
}
.cell-frozen {
  background: color-mix(in srgb, var(--bs-freeze, var(--accent-blue)) 26%, var(--bg-primary));
  color: color-mix(in srgb, var(--bs-freeze, var(--accent-blue)) 75%, white);
  box-shadow: inset 0 0 6px color-mix(in srgb, var(--bs-freeze, var(--accent-blue)) 35%, transparent);
}
/* BurnResist ship survived fire/explosion — dark green */
.cell-burn-resist {
  background: color-mix(in srgb, var(--bs-poison, var(--accent-green)) 28%, var(--bg-primary));
  color: color-mix(in srgb, var(--bs-poison, var(--accent-green)) 75%, white);
  box-shadow: inset 0 0 6px color-mix(in srgb, var(--bs-poison, var(--accent-green)) 35%, transparent);
}
.cell-devastated {
  background: color-mix(in srgb, var(--bs-cursed, var(--accent-purple)) 20%, var(--bg-inset));
  color: var(--bs-cursed, var(--accent-purple));
}
.cell-captured {
  background: color-mix(in srgb, var(--bs-cursed, var(--accent-purple)) 45%, var(--bg-primary));
  color: var(--text-primary);
  outline: 2px solid var(--bs-cursed, var(--accent-purple));
  outline-offset: -2px;
}
.cell-fire-permanent {
  background: color-mix(in srgb, var(--bs-burn, var(--accent-orange)) 55%, var(--bs-hit, var(--accent-red)));
  color: var(--text-primary);
  box-shadow: inset 0 0 8px color-mix(in srgb, var(--bs-burn, var(--accent-orange)) 55%, transparent);
}

/* -- Overlays ----------------------------------------------------- */
.cell-blocked {
  cursor: not-allowed !important;
  background: color-mix(in srgb, var(--accent-red) 14%, transparent) !important;
  outline: 1px solid color-mix(in srgb, var(--accent-red) 50%, transparent);
  outline-offset: -1px;
}
.cell-blocked::after {
  content: '\2717';
  color: color-mix(in srgb, var(--accent-red) 60%, transparent);
  font-size: 0.7rem;
  position: absolute;
}

.cell-clickable { cursor: pointer; }
@media (hover: hover) {
  .cell-clickable:hover {
    outline: 2px solid var(--bs-gold, var(--accent-gold));
    outline-offset: -2px;
    z-index: 1;
  }
}

.cell-highlighted {
  outline: 2px solid var(--bs-gold, var(--accent-gold));
  outline-offset: -2px;
  z-index: 1;
}

.cell-zone {
  background: color-mix(in srgb, var(--accent-blue) 10%, transparent) !important;
  outline: 1px dashed color-mix(in srgb, var(--accent-blue) 40%, transparent);
  outline-offset: -1px;
}

.cell-space {
  background: color-mix(in srgb, var(--accent-red) 8%, transparent) !important;
  outline: 1px solid color-mix(in srgb, var(--accent-red) 45%, transparent);
  outline-offset: -1px;
}

.cell-preview {
  background: color-mix(in srgb, var(--accent-blue) 45%, var(--bg-card)) !important;
  opacity: 0.6;
  outline: 2px solid var(--bs-gold, var(--accent-gold));
  outline-offset: -2px;
  z-index: 1;
}

@keyframes burn-pulse {
  from { opacity: 0.8; }
  to { opacity: 1; }
}

.cell-icon {
  pointer-events: none;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* -- Shot impact animations --------------------------------------- */
.anim-hit {
  animation: cell-hit-flash 0.4s ease-out forwards;
  z-index: 2;
}
.anim-miss {
  animation: cell-miss-ripple 0.4s ease-out forwards;
}
.anim-scratch {
  animation: cell-scratch-bounce 0.5s ease-out forwards;
  z-index: 2;
}
.anim-destroy {
  animation: cell-destroy-shake 0.5s ease-out forwards;
  z-index: 2;
}
.anim-sunk {
  animation: cell-sunk-collapse 0.8s ease-out forwards;
  z-index: 2;
}
.anim-burn-ignite {
  animation: cell-burn-ignite 0.7s ease-out forwards;
  z-index: 2;
}
.anim-dodge {
  animation: cell-dodge-flash 0.5s ease-out forwards;
  z-index: 2;
}
.anim-freeze {
  animation: cell-freeze 0.6s ease-out forwards;
  z-index: 2;
}
.anim-devastate {
  animation: cell-devastate 0.6s ease-out forwards;
  z-index: 2;
}
.anim-capture {
  animation: cell-capture 0.6s ease-out forwards;
  z-index: 2;
}
.anim-explode {
  animation: cell-explode 0.6s ease-out forwards;
  z-index: 2;
}

@keyframes cell-hit-flash {
  0% { background: var(--text-primary); transform: scale(1.3); }
  30% { background: var(--accent-red); }
  100% { transform: scale(1); }
}

@keyframes cell-miss-ripple {
  0% { box-shadow: inset 0 0 0 12px color-mix(in srgb, var(--accent-blue) 50%, transparent); transform: scale(0.85); }
  100% { box-shadow: inset 0 0 0 0 transparent; transform: scale(1); }
}

@keyframes cell-scratch-bounce {
  0% { transform: scale(1.2); background: var(--accent-gold); }
  40% { transform: scale(0.9); }
  70% { transform: scale(1.05); }
  100% { transform: scale(1); }
}

@keyframes cell-destroy-shake {
  0% { transform: translateX(0); background: var(--text-primary); }
  15% { transform: translateX(-3px); }
  30% { transform: translateX(3px); background: var(--accent-red); }
  45% { transform: translateX(-2px); }
  60% { transform: translateX(2px); background: color-mix(in srgb, var(--accent-red) 30%, var(--bg-inset)); }
  75% { transform: translateX(-1px); }
  100% { transform: translateX(0); }
}

@keyframes cell-sunk-collapse {
  0% { transform: scale(1.15); background: var(--text-primary); }
  20% { background: var(--accent-red); }
  50% { transform: scale(0.9); opacity: 0.6; background: color-mix(in srgb, var(--accent-red) 35%, var(--bg-inset)); }
  70% { transform: scale(1.02); opacity: 0.8; }
  100% { transform: scale(1); opacity: 1; }
}

@keyframes cell-burn-ignite {
  0% { background: var(--text-primary); transform: scale(1.4); }
  25% { background: var(--accent-orange); }
  50% { transform: scale(1.1); background: var(--accent-red); }
  100% { transform: scale(1); }
}

@keyframes cell-dodge-flash {
  0% { background: var(--accent-green); transform: scale(1.25); box-shadow: 0 0 12px color-mix(in srgb, var(--accent-green) 60%, transparent); }
  50% { background: color-mix(in srgb, var(--accent-green) 30%, transparent); }
  100% { transform: scale(1); box-shadow: none; }
}

@keyframes cell-freeze {
  0% { background: var(--text-primary); transform: scale(1.2); }
  30% { background: var(--accent-blue); }
  60% { transform: scale(0.95); }
  100% { transform: scale(1); }
}

@keyframes cell-devastate {
  0% { background: var(--text-primary); transform: scale(1.3); }
  30% { background: var(--accent-purple); }
  60% { transform: scale(0.95); }
  100% { transform: scale(1); }
}

@keyframes cell-capture {
  0% { transform: scale(1.2); box-shadow: 0 0 16px color-mix(in srgb, var(--accent-purple) 80%, transparent); }
  50% { transform: scale(0.95); }
  100% { transform: scale(1); box-shadow: none; }
}

@keyframes cell-explode {
  0% { background: var(--text-primary); transform: scale(1.5); box-shadow: 0 0 20px color-mix(in srgb, var(--accent-orange) 80%, transparent); }
  25% { background: var(--accent-orange); }
  50% { background: var(--accent-red); transform: scale(1.1); }
  100% { transform: scale(1); box-shadow: none; }
}

/* -- Last shot marker --------------------------------------------- */
.cell-last-shot::before {
  content: '';
  position: absolute;
  inset: -2px;
  border: 2px solid var(--bs-gold, var(--accent-gold));
  border-radius: 3px;
  animation: last-shot-pulse 1.5s ease-in-out infinite;
  z-index: 3;
  pointer-events: none;
}
@keyframes last-shot-pulse {
  0%, 100% { opacity: 1; border-color: var(--bs-gold, var(--accent-gold)); }
  50% { opacity: 0.3; border-color: transparent; }
}

/* -- Marked cell overlay ------------------------------------------ */
.cell-marked::after {
  content: '\2691';
  position: absolute;
  top: 0;
  right: 1px;
  font-size: 0.5rem;
  color: var(--bs-gold, var(--accent-gold));
  opacity: 0.8;
  pointer-events: none;
  z-index: 3;
}

/* -- Ship silhouette borders -------------------------------------- */
.cell-ship-outline {
  border: none;
}
.ship-edge-top { border-top: 2px solid color-mix(in srgb, var(--accent-blue) 60%, transparent); }
.ship-edge-right { border-right: 2px solid color-mix(in srgb, var(--accent-blue) 60%, transparent); }
.ship-edge-bottom { border-bottom: 2px solid color-mix(in srgb, var(--accent-blue) 60%, transparent); }
.ship-edge-left { border-left: 2px solid color-mix(in srgb, var(--accent-blue) 60%, transparent); }

/* -- Summon trail ------------------------------------------------- */
.cell-summon-trail::before {
  content: '';
  position: absolute;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--bs-gold, var(--accent-gold));
  opacity: 0.2;
  pointer-events: none;
}
.trail-ram::before {
  background: var(--accent-red);
  opacity: 0.3;
  box-shadow: 0 0 4px color-mix(in srgb, var(--accent-red) 40%, transparent);
}
.trail-scout::before {
  background: var(--accent-blue);
  opacity: 0.25;
  width: 8px;
  height: 4px;
  border-radius: 4px;
}
.trail-brander::before {
  background: var(--accent-orange);
  opacity: 0.3;
  animation: trail-smolder 1.5s ease-in-out infinite alternate;
}
@keyframes trail-smolder {
  0% { opacity: 0.15; box-shadow: none; }
  100% { opacity: 0.35; box-shadow: 0 0 4px color-mix(in srgb, var(--accent-orange) 40%, transparent); }
}
.trail-cursedboat::before {
  background: var(--accent-purple);
  opacity: 0.25;
  box-shadow: 0 0 5px color-mix(in srgb, var(--accent-purple) 30%, transparent);
}
.trail-pirateboat::before {
  background: var(--accent-gold);
  opacity: 0.2;
}

/* -- Range overlays ----------------------------------------------- */
.cell-range-overlay {
  position: relative;
}
.cell-range-overlay::after {
  content: '';
  position: absolute;
  inset: 1px;
  pointer-events: none;
  z-index: 1;
  border-radius: 2px;
}
.range-poison::after {
  background: color-mix(in srgb, var(--accent-green) 16%, transparent);
  border: 1px solid color-mix(in srgb, var(--accent-green) 45%, transparent);
  box-shadow: inset 0 0 6px color-mix(in srgb, var(--accent-green) 15%, transparent);
}
.range-explosion::after {
  background: color-mix(in srgb, var(--accent-orange) 16%, transparent);
  border: 1px solid color-mix(in srgb, var(--accent-orange) 45%, transparent);
  box-shadow: inset 0 0 6px color-mix(in srgb, var(--accent-orange) 15%, transparent);
}
.range-freeze::after {
  background: color-mix(in srgb, var(--accent-blue) 16%, transparent);
  border: 1px solid color-mix(in srgb, var(--accent-blue) 45%, transparent);
  box-shadow: inset 0 0 6px color-mix(in srgb, var(--accent-blue) 15%, transparent);
}
.range-brander::after {
  background: color-mix(in srgb, var(--accent-orange) 13%, transparent);
  border: 1px dashed color-mix(in srgb, var(--accent-orange) 50%, transparent);
}
.range-penalty-zone::after {
  background: color-mix(in srgb, var(--accent-red) 10%, transparent);
  border: 1px solid color-mix(in srgb, var(--accent-red) 35%, transparent);
}
.range-ownboard-target::after {
  background: color-mix(in srgb, var(--accent-red) 13%, transparent);
  border: 1px solid color-mix(in srgb, var(--accent-red) 50%, transparent);
  animation: own-board-pulse 1.5s ease-in-out infinite;
}
@keyframes own-board-pulse {
  0%, 100% { opacity: 0.6; }
  50% { opacity: 1; }
}


/* -- Ship idle bob ------------------------------------------------ */
@keyframes ship-bob {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-1px); }
}

/* -- Fog reveal --------------------------------------------------- */
.anim-reveal {
  animation: cell-reveal 0.4s ease-out forwards;
  z-index: 2;
}
@keyframes cell-reveal {
  0% { clip-path: circle(0% at 50% 50%); }
  100% { clip-path: circle(100% at 50% 50%); }
}

/* -- Mobile ------------------------------------------------------- */
@media (max-width: 480px) {
  .cell {
    width: 24px;
    height: 24px;
    font-size: 0.65rem;
  }
}
</style>
