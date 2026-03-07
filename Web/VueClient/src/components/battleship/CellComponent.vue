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
  else if (props.cell.isCaptured) classes.push('cell-captured')
  else if (props.cell.isScratched) classes.push('cell-scratched')
  else if (props.cell.isHit && props.cell.hasShip) classes.push('cell-hit')
  else if (props.cell.isHit) classes.push('cell-hit-empty')
  else if (props.cell.hasSummon) {
    classes.push(props.cell.summonOwnerId && props.isEnemy ? 'cell-summon-enemy' : 'cell-summon')
  }
  else if (props.cell.isMiss) classes.push('cell-miss')
  else if (props.cell.hasShip && !props.isEnemy) classes.push('cell-ship')
  else if (!props.cell.isRevealed && props.isEnemy) classes.push('cell-fog')
  else classes.push('cell-empty')

  if (props.clickable) {
    const alreadyShot = props.isEnemy && props.cell && (props.cell.isHit || props.cell.isMiss || props.cell.isRevealed)
    const incendiaryRetarget = alreadyShot && props.shotType === 'Incendiary' && props.cell?.isHit && props.cell?.hasShip
    const scratchedRetarget = props.cell?.isScratched
    if (!alreadyShot || incendiaryRetarget || scratchedRetarget) classes.push('cell-clickable')
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
  if (props.cell.isDestroyed) return renderIcon('destroyed', 16)
  if (props.cell.isDevastated) return renderIcon('devastated', 16)
  if (props.cell.isFirePermanent) return renderIcon('firePermanent', 14)
  if (props.cell.isBurning) return renderIcon('burning', 14)
  if (props.cell.isFrozen) return renderIcon('frozen', 14)
  if (props.cell.isCaptured) return renderIcon('captured', 14)
  if (props.cell.isScratched) return renderIcon('scratched', 14)
  if (props.cell.isHit && props.cell.hasShip) return renderIcon('hit', 14)
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
  if (props.cell.isMiss) return renderIcon('miss', 10)
  if (props.blocked) return ''
  return ''
})

const colLabels = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J']

const summonNames: Record<string, string> = {
  Ram: 'Таран', Scout: 'Разведчик', Brander: 'Брандер',
  CursedBoat: 'Проклятый', PirateBoat: 'Пират',
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
  if (props.cell.isDestroyed) base = `Уничтожено${ship}`
  else if (props.cell.isDevastated) base = `Опустошено`
  else if (props.cell.isBurning || props.cell.isFirePermanent) base = `Горит${ship}`
  else if (props.cell.isFrozen) base = `Заморожено`
  else if (props.cell.isCaptured) base = `Захвачено`
  else if (props.cell.isScratched) base = `Поцарапано — можно стрелять повторно`
  else if (props.cell.isHit && props.cell.hasShip) base = `Попадание${ship}`
  else if (props.cell.hasSummon) base = (props.cell.summonType && summonNames[props.cell.summonType]) ?? 'Призыв'
  else if (props.cell.isMiss) base = `Промах`
  else if (props.cell.hasShip && !props.isEnemy) base = `Корабль${ship}`
  else if (props.isEnemy && !props.cell.isRevealed) base = `Неизведано`

  const extras: string[] = []
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
.cell-empty { background: var(--bs-sea-dark, #0e1f3d); }
.cell-fog {
  background: var(--bs-sea-mid, #142c54);
  background-image: linear-gradient(
    160deg,
    transparent 30%,
    rgba(255, 255, 255, 0.03) 50%,
    transparent 70%
  );
}
.cell-unknown { background: var(--bs-sea-dark, #0e1f3d); }

.cell-ship {
  background: var(--bs-ocean-blue, #2980b9);
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
  opacity: 0.7;
  animation: ship-bob 3s ease-in-out infinite;
  animation-delay: calc(var(--cell-row, 0) * 0.15s + var(--cell-col, 0) * 0.1s);
}

.cell-hit {
  background: linear-gradient(135deg, #4a1010 0%, #6b1a1a 50%, #4a1010 100%);
  color: var(--bs-fire-red, #c0392b);
  transform: rotate(0.5deg);
}
.cell-hit-empty {
  background: var(--bs-sea-dark, #0e1f3d);
  color: var(--bs-parchment-dim, #b09a78);
}
.cell-miss {
  background: var(--bs-sea-dark, #0e1f3d);
  color: var(--bs-ocean-light, #5dade2);
}

.cell-scratched {
  background: var(--bs-wood-mid, #4a2f1a);
  background-image: linear-gradient(
    135deg,
    transparent 40%,
    rgba(232, 213, 176, 0.12) 45%,
    rgba(232, 213, 176, 0.12) 55%,
    transparent 60%
  );
  color: var(--bs-parchment, #e8d5b0);
}

.cell-burning {
  background: var(--bs-fire-orange, #e67e22);
  color: white;
  animation: burn-pulse 0.8s ease-in-out infinite alternate;
}

.cell-summon {
  background: rgba(212, 168, 71, 0.3);
  color: var(--bs-gold, #d4a847);
  border: 1px solid var(--bs-gold, #d4a847);
  font-size: 0.7rem;
}
.cell-summon-enemy {
  background: rgba(192, 57, 43, 0.3);
  color: var(--bs-fire-red, #c0392b);
  border: 1px solid var(--bs-fire-red, #c0392b);
  font-size: 0.7rem;
}

.cell-destroyed {
  background: #1a0a0a;
  background-image: radial-gradient(circle at 30% 30%, rgba(80, 20, 0, 0.5) 0%, transparent 60%),
                     radial-gradient(circle at 70% 70%, rgba(60, 10, 0, 0.4) 0%, transparent 50%);
  color: rgba(255, 255, 255, 0.6);
}
.cell-frozen {
  background: linear-gradient(135deg, #0a2a4a, #1a3a5c, #0a2a4a);
  color: var(--bs-ice-blue, #74b9ff);
  box-shadow: inset 0 0 6px rgba(116, 185, 255, 0.3);
}
.cell-devastated {
  background: #1a0a1e;
  color: var(--bs-cursed-purple, #8e44ad);
}
.cell-captured {
  background: var(--bs-cursed-purple, #8e44ad);
  color: white;
  outline: 2px solid var(--bs-cursed-purple, #8e44ad);
  outline-offset: -2px;
}
.cell-fire-permanent {
  background: #cc3300;
  color: white;
  box-shadow: inset 0 0 8px rgba(255, 100, 0, 0.4);
}

/* -- Overlays ----------------------------------------------------- */
.cell-blocked {
  cursor: not-allowed !important;
  background: rgba(239, 68, 68, 0.15) !important;
  outline: 1px solid rgba(239, 68, 68, 0.5);
  outline-offset: -1px;
}
.cell-blocked::after {
  content: '\2717';
  color: rgba(239, 68, 68, 0.6);
  font-size: 0.7rem;
  position: absolute;
}

.cell-clickable { cursor: pointer; }
@media (hover: hover) {
  .cell-clickable:hover {
    outline: 2px solid var(--bs-gold-bright, #f0c850);
    outline-offset: -2px;
    z-index: 1;
  }
}

.cell-highlighted {
  outline: 2px solid var(--bs-poison-green, #27ae60);
  outline-offset: -2px;
  z-index: 1;
}

.cell-zone {
  background: rgba(41, 128, 185, 0.1) !important;
  outline: 1px dashed rgba(41, 128, 185, 0.4);
  outline-offset: -1px;
}

.cell-space {
  background: rgba(192, 57, 43, 0.08) !important;
  outline: 1px solid rgba(192, 57, 43, 0.45);
  outline-offset: -1px;
}

.cell-preview {
  background: var(--bs-ocean-blue, #2980b9) !important;
  opacity: 0.5;
  outline: 2px solid var(--bs-gold, #d4a847);
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
  0% { background: #ffffff; transform: scale(1.3); }
  30% { background: var(--bs-fire-red, #c0392b); }
  100% { transform: scale(1); }
}

@keyframes cell-miss-ripple {
  0% { box-shadow: inset 0 0 0 12px rgba(93, 173, 226, 0.5); transform: scale(0.85); }
  100% { box-shadow: inset 0 0 0 0 rgba(93, 173, 226, 0); transform: scale(1); }
}

@keyframes cell-scratch-bounce {
  0% { transform: scale(1.2); background: var(--bs-gold, #d4a847); }
  40% { transform: scale(0.9); }
  70% { transform: scale(1.05); }
  100% { transform: scale(1); }
}

@keyframes cell-destroy-shake {
  0% { transform: translateX(0); background: #ffffff; }
  15% { transform: translateX(-3px); }
  30% { transform: translateX(3px); background: var(--bs-fire-red, #c0392b); }
  45% { transform: translateX(-2px); }
  60% { transform: translateX(2px); background: #2a0505; }
  75% { transform: translateX(-1px); }
  100% { transform: translateX(0); }
}

@keyframes cell-sunk-collapse {
  0% { transform: scale(1.15); background: #ffffff; }
  20% { background: #8b0000; }
  50% { transform: scale(0.9); opacity: 0.6; background: #3a0808; }
  70% { transform: scale(1.02); opacity: 0.8; }
  100% { transform: scale(1); opacity: 1; }
}

@keyframes cell-burn-ignite {
  0% { background: #ffffff; transform: scale(1.4); }
  25% { background: var(--bs-fire-orange, #e67e22); }
  50% { transform: scale(1.1); background: var(--bs-fire-red, #c0392b); }
  100% { transform: scale(1); }
}

@keyframes cell-dodge-flash {
  0% { background: var(--bs-poison-green, #27ae60); transform: scale(1.25); box-shadow: 0 0 12px rgba(39, 174, 96, 0.6); }
  50% { background: rgba(39, 174, 96, 0.3); }
  100% { transform: scale(1); box-shadow: none; }
}

@keyframes cell-freeze {
  0% { background: #ffffff; transform: scale(1.2); }
  30% { background: var(--bs-ice-blue, #74b9ff); }
  60% { transform: scale(0.95); }
  100% { transform: scale(1); }
}

@keyframes cell-devastate {
  0% { background: #ffffff; transform: scale(1.3); }
  30% { background: var(--bs-cursed-purple, #8e44ad); }
  60% { transform: scale(0.95); }
  100% { transform: scale(1); }
}

@keyframes cell-capture {
  0% { transform: scale(1.2); box-shadow: 0 0 16px rgba(142, 68, 173, 0.8); }
  50% { transform: scale(0.95); }
  100% { transform: scale(1); box-shadow: none; }
}

@keyframes cell-explode {
  0% { background: #ffffff; transform: scale(1.5); box-shadow: 0 0 20px rgba(255, 100, 0, 0.8); }
  25% { background: var(--bs-fire-orange, #e67e22); }
  50% { background: var(--bs-fire-red, #c0392b); transform: scale(1.1); }
  100% { transform: scale(1); box-shadow: none; }
}

/* -- Last shot marker --------------------------------------------- */
.cell-last-shot::before {
  content: '';
  position: absolute;
  inset: -2px;
  border: 2px solid var(--bs-gold-bright, #f0c850);
  border-radius: 3px;
  animation: last-shot-pulse 1.5s ease-in-out infinite;
  z-index: 3;
  pointer-events: none;
}
@keyframes last-shot-pulse {
  0%, 100% { opacity: 1; border-color: var(--bs-gold-bright, #f0c850); }
  50% { opacity: 0.3; border-color: transparent; }
}

/* -- Marked cell overlay ------------------------------------------ */
.cell-marked::after {
  content: '\2691';
  position: absolute;
  top: 0;
  right: 1px;
  font-size: 0.5rem;
  color: var(--bs-gold, #d4a847);
  opacity: 0.8;
  pointer-events: none;
  z-index: 3;
}

/* -- Ship silhouette borders -------------------------------------- */
.cell-ship-outline {
  border: none;
}
.ship-edge-top { border-top: 2px solid rgba(41, 128, 185, 0.6); }
.ship-edge-right { border-right: 2px solid rgba(41, 128, 185, 0.6); }
.ship-edge-bottom { border-bottom: 2px solid rgba(41, 128, 185, 0.6); }
.ship-edge-left { border-left: 2px solid rgba(41, 128, 185, 0.6); }

/* -- Summon trail ------------------------------------------------- */
.cell-summon-trail::before {
  content: '';
  position: absolute;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--bs-gold, #d4a847);
  opacity: 0.2;
  pointer-events: none;
}
.trail-ram::before {
  background: var(--bs-fire-red, #c0392b);
  opacity: 0.3;
  box-shadow: 0 0 4px rgba(192, 57, 43, 0.4);
}
.trail-scout::before {
  background: var(--bs-ocean-blue, #2980b9);
  opacity: 0.25;
  width: 8px;
  height: 4px;
  border-radius: 4px;
}
.trail-brander::before {
  background: var(--bs-fire-orange, #e67e22);
  opacity: 0.3;
  animation: trail-smolder 1.5s ease-in-out infinite alternate;
}
@keyframes trail-smolder {
  0% { opacity: 0.15; box-shadow: none; }
  100% { opacity: 0.35; box-shadow: 0 0 4px rgba(230, 126, 34, 0.4); }
}
.trail-cursedboat::before {
  background: var(--bs-cursed-purple, #8e44ad);
  opacity: 0.25;
  box-shadow: 0 0 5px rgba(142, 68, 173, 0.3);
}
.trail-pirateboat::before {
  background: var(--bs-gold, #d4a847);
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
  background: rgba(39, 174, 96, 0.18);
  border: 1px solid rgba(39, 174, 96, 0.45);
  box-shadow: inset 0 0 6px rgba(39, 174, 96, 0.15);
}
.range-explosion::after {
  background: rgba(230, 126, 34, 0.18);
  border: 1px solid rgba(230, 126, 34, 0.45);
  box-shadow: inset 0 0 6px rgba(230, 126, 34, 0.15);
}
.range-freeze::after {
  background: rgba(116, 185, 255, 0.18);
  border: 1px solid rgba(116, 185, 255, 0.45);
  box-shadow: inset 0 0 6px rgba(116, 185, 255, 0.15);
}
.range-brander::after {
  background: rgba(230, 126, 34, 0.15);
  border: 1px dashed rgba(230, 126, 34, 0.5);
}
.range-ownboard-target::after {
  background: rgba(192, 57, 43, 0.15);
  border: 1px solid rgba(192, 57, 43, 0.5);
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
