<script setup lang="ts">
import { computed } from 'vue'
import type { BattleshipCell } from 'src/services/signalr'
import type { BattleshipBowDirection } from './battleship-geometry'
import { renderIcon } from './battleship-icons'
import { message } from 'src/platform/localization'
import {
  boardingShipIconKey,
  boardingShipName,
  summonIconKey,
  summonMarkerClass,
  summonMarkerName,
  summonTypeName,
} from './battleship-summon-presentation'

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
  revealShipName?: boolean
  lastShot?: boolean
  marked?: boolean
  shipEdges?: { top: boolean; right: boolean; bottom: boolean; left: boolean }
  rangeOverlay?: string
  deckSymbols?: string[]
  bowDirection?: BattleshipBowDirection
  maneuverActive?: boolean
  maneuverShipCell?: boolean
  maneuverTarget?: boolean
  replacementOptionA?: boolean
  replacementOptionB?: boolean
  captureFocus?: boolean
  captureShipCell?: boolean
}>()

const cellStyle = computed(() => {
  if (!props.cell) return {}
  return { '--cell-row': props.cell.row, '--cell-col': props.cell.col } as Record<string, string | number>
})

const hasVisibleCurrentShip = computed(() =>
  props.cell?.hasShip === true && (!props.isEnemy || props.cell.isRevealed))

const cellClass = computed(() => {
  if (!props.cell) return ['cell', 'cell-unknown']
  const classes = ['cell']

  // Priority order per spec section 11
  if (props.cell.isBurnResistMarked) classes.push('cell-burn-resist')
  else if (props.cell.isScratched) classes.push('cell-scratched')
  else if (props.cell.isFrozen) classes.push('cell-frozen')
  else if (props.cell.isDevastated) classes.push('cell-devastated')
  else if (props.cell.isShipSunk) classes.push('cell-ship-sunk')
  else if (props.cell.isDestroyed) classes.push('cell-destroyed')
  else if (props.cell.isFirePermanent) classes.push('cell-fire-permanent')
  else if (props.cell.isBurning) classes.push('cell-burning')
  else if (props.cell.isManeuverDodgeMarked) classes.push('cell-maneuver-dodge')
  else if (props.cell.isHit && props.cell.hasShip) classes.push('cell-hit')
  else if (props.cell.isHit) classes.push('cell-hit-empty')
  else if (props.cell.isCaptured) classes.push('cell-captured')
  else if (props.cell.isDodgeMarked) classes.push('cell-dodge-mark')
  else if (hasVisibleCurrentShip.value) {
    classes.push(props.isEnemy ? 'cell-revealed-ship' : 'cell-ship')
  }
  else if (props.cell.isMiss) classes.push('cell-miss')
  else if (!props.cell.isRevealed && props.isEnemy) classes.push('cell-fog')
  else classes.push('cell-empty')

  // ТЗ #17: creatures render as an orange icon overlay — the base class above keeps
  // showing the cell's own status (hit/miss/fire/…) underneath
  if (props.cell.hasSummon) classes.push('cell-has-summon')
  if (props.cell.isGhostSummon) classes.push('cell-ghost-summon')
  if (props.cell.isPhantomSummon) {
    classes.push('cell-phantom-summon')
    classes.push(`phantom-direction-${(props.cell.summonMoveDirection ?? 'Down').toLowerCase()}`)
  }
  if (props.cell.isBoardingSummon) {
    classes.push('cell-boarding-ship')
    classes.push(`boarding-direction-${(props.cell.summonMoveDirection ?? 'Down').toLowerCase()}`)
  }

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
  if (props.maneuverActive) classes.push('cell-maneuver-muted')
  if (props.maneuverShipCell) classes.push('cell-maneuver-ship')
  if (props.replacementOptionA) classes.push('cell-replacement-option-a')
  if (props.replacementOptionB) classes.push('cell-replacement-option-b')
  if (props.maneuverTarget) classes.push('cell-maneuver-target')
  if (props.captureFocus) classes.push('cell-capture-muted')
  if (props.captureShipCell) classes.push('cell-capture-focused')

  // Ship silhouette borders
  if (props.shipEdges) {
    classes.push('cell-ship-outline')
    if (props.shipEdges.top) classes.push('ship-edge-top')
    if (props.shipEdges.right) classes.push('ship-edge-right')
    if (props.shipEdges.bottom) classes.push('ship-edge-bottom')
    if (props.shipEdges.left) classes.push('ship-edge-left')
  }

  // Summon trail (type-specific)
  if (props.cell.summonTrails?.length) {
    classes.push('cell-summon-trail')
    for (const marker of props.cell.summonTrails)
      classes.push('trail-' + summonMarkerClass(marker))
    if (props.cell.summonTrails.length > 1) classes.push('trail-multiple')
  }

  // Range overlay (poison, explosion, freeze, brander)
  if (props.rangeOverlay) {
    classes.push('cell-range-overlay', 'range-' + props.rangeOverlay)
  }
  if (props.cell.isGrabCell) classes.push('cell-grab')

  return classes
})

const cellIconHtml = computed(() => {
  if (!props.cell) return ''
  // ТЗ #17: the creature icon always wins — the cell status shows through the background
  if (props.cell.hasSummon) {
    if (props.cell.isBoardingSummon) {
      return renderIcon(
        boardingShipIconKey(props.cell.boardingShipDeckCount),
        26,
      )
    }
    return renderIcon(
      summonIconKey(props.cell.summonType ?? ''),
      14,
    )
  }
  if (props.cell.isBurnResistMarked) return renderIcon('scratched', 14)
  if (props.cell.isScratched) return renderIcon('scratched', 14)
  if (props.cell.isFrozen) return renderIcon('frozen', 14)
  if (props.cell.isDevastated) return renderIcon('devastated', 16)
  if (props.cell.isShipSunk) return renderIcon('destroyed', 16)
  if (props.cell.isDestroyed) return renderIcon('destroyed', 16)
  if (props.cell.isFirePermanent) return renderIcon('firePermanent', 14)
  if (props.cell.isBurning) return renderIcon('burning', 14)
  if (props.cell.isHit && props.cell.hasShip) return renderIcon('hit', 14)
  if (props.cell.isCaptured) return renderIcon('captured', 14)
  if (hasVisibleCurrentShip.value)
    return props.isEnemy ? renderIcon('ship1', 13) : ''
  if (props.cell.isMiss) return renderIcon('miss', 10)
  if (props.blocked) return ''
  return ''
})

const colLabels = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J']

const deckSymbolNames: Record<string, string> = {
  ballista: 'Баллиста',
  catapult: 'Тетракамнемёт',
  electricCharge: message('battleship.weapon.neptune.name'),
  mast: 'Мачта',
  boiler: 'Котельная',
  incendiary: 'Горючка',
  cannon: message('battleship.weapon.cannon.name'),
  fortuna: message('battleship.weapon.fortuna.name'),
  warming: message('battleship.weapon.warming.name'),
  armor: 'Усиленная броня',
}

const deckSymbolHtml = computed(() => (props.deckSymbols ?? [])
  .map(symbol => ({ symbol, html: renderIcon(symbol, 10) })))
const electricChargeHtml = computed(() => props.cell?.hasElectricCharge
  ? renderIcon('electricCharge', 16)
  : '')
const bowHtml = computed(() => props.bowDirection ? renderIcon('bow', 10) : '')
const summonDeathHtml = computed(() => (props.cell?.summonDeaths ?? []).map((marker, index) => ({
  marker,
  markerClass: summonMarkerClass(marker),
  frozen: props.cell?.frozenSummonDeathIndices?.includes(index) ?? false,
  html: renderIcon(summonIconKey(marker.type, marker.isBoardingShip), 9),
})))
const frozenDeathBadgeHtml = renderIcon('frozen', 7)
const parrotMotionHtml = computed(() => props.anim === 'anim-parrot-death'
  || props.anim?.startsWith('anim-parrot-flight-')
  ? renderIcon('parrot', 16)
  : '')

defineEmits<{
  (e: 'tipShow', ev: MouseEvent, text: string): void
  (e: 'tipMove', ev: MouseEvent): void
  (e: 'tipHide'): void
}>()

const cellTooltip = computed(() => {
  if (!props.cell) return ''
  const coord = ` (${colLabels[props.cell.col] ?? props.cell.col}${props.cell.row + 1})`
  const ship = props.shipName ? ` — ${props.shipName}` : ''
  const sunkShip = props.revealShipName === false
    ? undefined
    : props.cell.sunkShipName ?? props.shipName
  const sunkShipSuffix = sunkShip ? ` - ${sunkShip}` : ''

  let base = ''
  if (props.cell.hasSummon) {
    base = props.cell.isBoardingSummon
      ? boardingShipName(props.cell.summonName)
      : props.cell.summonName
        ?? (props.cell.summonType ? summonTypeName(props.cell.summonType) : 'Призыв')
    if (props.cell.isGhostSummon) {
      base = `${message('battleship.summon.ghost')} | ${base}`
    }
    if (props.cell.isPhantomSummon) {
      base = `${message('battleship.summon.parrot.phantom')} | ${base}`
    }
    // ТЗ #1: material non-boarding enemy creature in the penalty zone (rows 1-3 of the own board)
    if (!props.isEnemy && !props.cell.isGhostSummon && !props.cell.isPhantomSummon
      && props.cell.summonType !== 'Parrot'
      && !props.cell.isBoardingSummon && props.cell.row <= 2) {
      base = `Штраф за убийство суммона в этой зоне (кроме убийства сразу после появления) | ${base}`
    }
  }
  else if (props.cell.isBurnResistMarked) base = `Корабль устоял против огня`
  else if (props.cell.isScratched) base = `Поцарапано`
  else if (props.cell.isFrozen) base = `Заморожено`
  else if (props.cell.isShipSunk) base = `Корабль полностью потоплен${sunkShipSuffix}`
  else if (props.cell.isDestroyed) base = `Палуба уничтожена${ship}`
  else if (props.cell.isDevastated) base = `Опустошено`
  else if (props.cell.isBurning || props.cell.isFirePermanent) base = `Горит${ship}`
  else if (props.cell.isCaptured) base = `Захвачено`
  else if (props.cell.isManeuverDodgeMarked) base = `Лёгкая тройка увернулась — прежняя клетка`
  else if (props.cell.isHit && props.cell.hasShip) base = `Попадание${ship}`
  else if (props.cell.isDodgeMarked) base = `Юркая единичка увернулась — баллиста бессильна`
  else if (hasVisibleCurrentShip.value)
    base = props.isEnemy ? `Обнаружен корабль` : `Корабль${ship}`
  else if (props.cell.isMiss) base = `Промах`
  else if (props.isEnemy && !props.cell.isRevealed) base = `Неизведано`

  const extras: string[] = []
  const addState = (active: boolean, label: string) => {
    if (active && !base.startsWith(label) && !extras.includes(label)) extras.push(label)
  }
  addState(props.cell.isShipSunk ?? false, 'Корабль полностью потоплен')
  addState((props.cell.isDestroyed ?? false) && !props.cell.isShipSunk, 'Палуба уничтожена')
  addState(props.cell.isDevastated, 'Опустошено')
  addState(props.cell.isFirePermanent || props.cell.isBurning, 'Горит')
  addState(props.cell.isFrozen, 'Заморожено')
  addState(props.cell.isBurnResistMarked, 'Корабль устоял против огня')
  addState(props.cell.isScratched, 'Поцарапано')
  addState(props.cell.isCaptured, 'Захвачено')
  addState(props.cell.isDodgeMarked, 'Уклонение')
  addState(props.cell.isManeuverDodgeMarked, 'Манёвренное уклонение')
  addState(props.cell.hasElectricCharge, message('battleship.status.electricCharge'))
  if (props.cell.isGrabCell || props.rangeOverlay === 'grab')
    extras.push(message('battleship.grab.tooltip'))
  for (const marker of props.cell.summonTrails ?? [])
    extras.push(`След: ${summonMarkerName(marker)}`)
  for (const [index, marker] of (props.cell.summonDeaths ?? []).entries()) {
    const frozen = props.cell.frozenSummonDeathIndices?.includes(index) ?? false
    extras.push(`${frozen ? 'Заморожен' : 'Погиб'}: ${summonMarkerName(marker)}`)
  }
  if (props.lastShot) extras.push('Последний выстрел')
  if (props.marked) extras.push('Метка')
  if (props.bowDirection) extras.push('Нос корабля')
  for (const symbol of props.deckSymbols ?? [])
    extras.push(`Палуба: ${deckSymbolNames[symbol] ?? symbol}`)

  const parts = [base, ...extras].filter(Boolean)
  if (parts.length === 0) return coord.trim()
  return parts.join(' | ') + coord
})
</script>

<template>
  <div :class="cellClass" :style="cellStyle" :data-row="cell?.row" :data-col="cell?.col"
    @mouseenter="$emit('tipShow', $event, cellTooltip)"
    @mousemove="$emit('tipMove', $event)"
    @mouseleave="$emit('tipHide')"
  >
    <span
      v-if="cellIconHtml"
      class="cell-icon"
      :class="{ 'cell-icon--boarding': cell?.isBoardingSummon }"
      v-html="cellIconHtml"
    ></span>
    <span
      v-if="parrotMotionHtml"
      class="parrot-motion-icon"
      v-html="parrotMotionHtml"
    ></span>
    <span
      v-if="electricChargeHtml"
      class="electric-charge"
      v-html="electricChargeHtml"
    ></span>
    <span v-if="bowHtml" class="deck-bow" :class="'deck-bow--' + bowDirection" v-html="bowHtml"></span>
    <span v-if="deckSymbolHtml.length" class="deck-symbols">
      <span v-for="entry in deckSymbolHtml" :key="entry.symbol" class="deck-symbol" :class="'deck-symbol--' + entry.symbol" v-html="entry.html"></span>
    </span>
    <span v-if="summonDeathHtml.length" class="summon-deaths">
      <span
        v-for="(death, deathIndex) in summonDeathHtml"
        :key="`${death.marker.summonId}-${deathIndex}`"
        class="summon-death"
        :class="[
          'summon-death--' + death.markerClass,
          { 'summon-death--frozen': death.frozen },
        ]"
      >
        <span class="summon-death-icon" v-html="death.html"></span>
        <span
          v-if="death.frozen"
          class="summon-death-freeze-badge"
          v-html="frozenDeathBadgeHtml"
        ></span>
      </span>
    </span>
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

.electric-charge {
  position: absolute;
  top: -2px;
  right: -2px;
  z-index: 12;
  display: inline-flex;
  width: 16px;
  height: 16px;
  pointer-events: none;
  filter:
    drop-shadow(0 0 3px rgba(45, 212, 191, 0.95))
    drop-shadow(0 1px 1px rgba(0, 0, 0, 0.9));
  animation: electric-charge-pulse 1.2s ease-in-out infinite alternate;
}
.electric-charge :deep(img) {
  width: 100%;
  height: 100%;
  object-fit: contain;
}
@keyframes electric-charge-pulse {
  from { transform: scale(0.9); opacity: 0.82; }
  to { transform: scale(1.08); opacity: 1; }
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
  background: color-mix(in srgb, white 88%, var(--bg-card));
  color: color-mix(in srgb, var(--accent-blue) 60%, #111827);
  box-shadow: inset 0 0 0 1px color-mix(in srgb, white 75%, var(--accent-blue));
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
.cell-ghost-summon .cell-icon {
  opacity: 0.46;
  filter: saturate(0.35) drop-shadow(0 0 5px #c4f1ff);
  animation: ghost-summon-pulse 1.15s ease-in-out infinite alternate;
}

.cell-phantom-summon {
  overflow: visible;
}
.cell-phantom-summon::after {
  content: '';
  position: absolute;
  width: 70%;
  height: 2px;
  border-radius: 999px;
  background: linear-gradient(90deg, transparent, rgba(74, 222, 128, 0.8));
  filter: blur(1px);
  opacity: 0.8;
  pointer-events: none;
}
.cell-phantom-summon .cell-icon {
  opacity: 0.62;
  filter: blur(1.4px) saturate(1.4) drop-shadow(0 0 7px rgba(74, 222, 128, 0.85));
  animation: parrot-phantom-right 0.34s ease-in-out infinite alternate;
}
.phantom-direction-right::after { top: 50%; right: 52%; }
.phantom-direction-left::after {
  top: 50%;
  left: 52%;
  transform: rotate(180deg);
}
.phantom-direction-down::after {
  left: 15%;
  bottom: 52%;
  transform: rotate(90deg);
}
.phantom-direction-up::after {
  left: 15%;
  top: 52%;
  transform: rotate(-90deg);
}
.phantom-direction-left .cell-icon { animation-name: parrot-phantom-left; }
.phantom-direction-up .cell-icon { animation-name: parrot-phantom-up; }
.phantom-direction-down .cell-icon { animation-name: parrot-phantom-down; }
@keyframes parrot-phantom-right {
  from { transform: translateX(-3px) scaleX(1.22); }
  to { transform: translateX(3px) scaleX(0.88); }
}
@keyframes parrot-phantom-left {
  from { transform: translateX(3px) scaleX(-1.22); }
  to { transform: translateX(-3px) scaleX(-0.88); }
}
@keyframes parrot-phantom-up {
  from { transform: translateY(3px) rotate(-90deg) scaleX(1.22); }
  to { transform: translateY(-3px) rotate(-90deg) scaleX(0.88); }
}
@keyframes parrot-phantom-down {
  from { transform: translateY(-3px) rotate(90deg) scaleX(1.22); }
  to { transform: translateY(3px) rotate(90deg) scaleX(0.88); }
}
@keyframes ghost-summon-pulse {
  from { transform: scale(0.88); opacity: 0.35; }
  to { transform: scale(1.05); opacity: 0.68; }
}

/* A nimble ship dodged a ballista here — static green mark */
.cell-dodge-mark {
  background: color-mix(in srgb, var(--bs-poison, var(--accent-green)) 28%, var(--bg-primary));
  box-shadow: inset 0 0 0 2px color-mix(in srgb, var(--bs-poison, var(--accent-green)) 55%, transparent);
  color: var(--bs-poison, var(--accent-green));
}

/* Light Wood Triple auto-maneuver: server-projected latest origin for that ship. */
.cell-maneuver-dodge {
  background: color-mix(in srgb, #ec4899 32%, var(--bg-primary));
  box-shadow: inset 0 0 0 2px color-mix(in srgb, #f472b6 72%, transparent);
  color: #f9a8d4;
}

.cell-destroyed {
  background: color-mix(in srgb, var(--bs-hit, var(--accent-red)) 16%, var(--bg-inset));
  background-image: radial-gradient(circle at 30% 30%, color-mix(in srgb, var(--bs-hit, var(--accent-red)) 22%, transparent) 0%, transparent 60%),
                     radial-gradient(circle at 70% 70%, color-mix(in srgb, var(--bs-burn, var(--accent-orange)) 14%, transparent) 0%, transparent 50%);
  color: var(--text-muted);
}
.cell-ship-sunk {
  background-color: color-mix(in srgb, #020617 88%, var(--accent-blue));
  background-image:
    linear-gradient(45deg, transparent 43%, rgba(226, 232, 240, 0.72) 44% 48%, transparent 49%),
    linear-gradient(-45deg, transparent 43%, rgba(226, 232, 240, 0.72) 44% 48%, transparent 49%),
    radial-gradient(circle at 50% 50%, rgba(15, 23, 42, 0.2), rgba(2, 6, 23, 0.92));
  color: #cbd5e1;
  box-shadow:
    inset 0 0 0 2px rgba(71, 85, 105, 0.72),
    inset 0 0 10px rgba(0, 0, 0, 0.9);
  filter: saturate(0.35) brightness(0.78);
}
.cell-frozen {
  background: color-mix(in srgb, var(--bs-freeze, var(--accent-blue)) 26%, var(--bg-primary));
  color: color-mix(in srgb, var(--bs-freeze, var(--accent-blue)) 75%, white);
  box-shadow: inset 0 0 6px color-mix(in srgb, var(--bs-freeze, var(--accent-blue)) 35%, transparent);
}
/* BurnResist survived fire/explosion — black for both players, ahead of red hit states. */
.cell-burn-resist {
  background: #020204 !important;
  color: #e2e8f0;
  box-shadow:
    inset 0 0 0 2px rgba(15, 23, 42, 0.95),
    inset 0 0 9px rgba(0, 0, 0, 0.98);
}
.cell-devastated {
  background: color-mix(in srgb, #02040a 92%, var(--accent-blue));
  color: #d6d8df;
  box-shadow: inset 0 0 10px rgba(0, 0, 0, 0.95);
}
.cell-captured {
  background: color-mix(in srgb, var(--bs-cursed, var(--accent-purple)) 45%, var(--bg-primary));
  color: var(--text-primary);
  outline: 2px solid var(--bs-cursed, var(--accent-purple));
  outline-offset: -2px;
}
.cell-grab {
  background: color-mix(in srgb, #800020 58%, var(--bg-primary)) !important;
  color: #fff1f2;
  outline: 2px solid #be123c;
  outline-offset: -2px;
  box-shadow: inset 0 0 9px rgba(76, 5, 25, 0.9);
}
.cell-fire-permanent {
  background: color-mix(in srgb, var(--bs-burn, var(--accent-orange)) 55%, var(--bs-hit, var(--accent-red)));
  color: var(--text-primary);
  box-shadow: inset 0 0 8px color-mix(in srgb, var(--bs-burn, var(--accent-orange)) 55%, transparent);
}

/* Converted Close ships remain real hulls during Boarding: a full red enemy
   ship tile, not the former tiny summon dot. */
.cell-boarding-ship {
  background:
    linear-gradient(
      90deg,
      rgba(255, 255, 255, 0.09) 25%,
      transparent 25% 50%,
      rgba(255, 255, 255, 0.09) 50% 75%,
      transparent 75%
    ),
    color-mix(in srgb, var(--accent-red) 56%, var(--bg-card)) !important;
  background-size: 8px 8px;
  color: #fecaca !important;
  outline: 2px solid color-mix(in srgb, var(--accent-red) 82%, white);
  outline-offset: -2px;
  box-shadow:
    inset 0 0 10px color-mix(in srgb, var(--accent-red) 42%, transparent),
    0 0 9px color-mix(in srgb, var(--accent-red) 48%, transparent);
  opacity: 1;
  z-index: 4;
  animation: boarding-hull-bob 1.8s ease-in-out infinite;
}
.cell-boarding-ship .cell-icon--boarding {
  width: calc(100% - 4px);
  height: calc(100% - 4px);
  color: #fff1f2;
  filter:
    drop-shadow(0 1px 1px rgba(0, 0, 0, 0.95))
    drop-shadow(0 0 5px color-mix(in srgb, var(--accent-red) 88%, transparent));
}
.cell-boarding-ship .cell-icon--boarding :deep(svg) {
  width: 100%;
  height: 100%;
}
.boarding-direction-right .cell-icon--boarding { transform: rotate(0deg); }
.boarding-direction-down .cell-icon--boarding { transform: rotate(90deg); }
.boarding-direction-left .cell-icon--boarding { transform: rotate(180deg); }
.boarding-direction-up .cell-icon--boarding { transform: rotate(-90deg); }
@keyframes boarding-hull-bob {
  0%, 100% { translate: 0 0; }
  50% { translate: 0 -1px; }
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

.cell-maneuver-muted {
  filter: grayscale(1) brightness(0.38);
  opacity: 0.48;
  pointer-events: none;
}
.cell-maneuver-ship {
  filter: none;
  opacity: 1;
  outline: 3px solid #facc15;
  outline-offset: -3px;
  z-index: 5;
}
.cell-replacement-option-a {
  filter: none;
  opacity: 1;
  background-image: linear-gradient(rgba(56, 189, 248, 0.34), rgba(56, 189, 248, 0.34)) !important;
  box-shadow: inset 0 0 0 3px #38bdf8;
  z-index: 5;
}
.cell-replacement-option-b {
  filter: none;
  opacity: 1;
  background-image: linear-gradient(rgba(192, 132, 252, 0.34), rgba(192, 132, 252, 0.34)) !important;
  box-shadow: inset 0 0 0 3px #c084fc;
  z-index: 5;
}
.cell-replacement-option-a.cell-replacement-option-b {
  background-image: linear-gradient(
    135deg,
    rgba(56, 189, 248, 0.42) 0 50%,
    rgba(192, 132, 252, 0.42) 50% 100%
  ) !important;
  box-shadow:
    inset 3px 0 0 #38bdf8,
    inset -3px 0 0 #c084fc;
}
.cell-maneuver-target {
  filter: none;
  opacity: 1;
  pointer-events: auto;
  cursor: pointer;
  background: color-mix(in srgb, #22c55e 62%, var(--bg-primary)) !important;
  outline: 3px solid #86efac;
  outline-offset: -3px;
  box-shadow: 0 0 16px rgba(34, 197, 94, 0.9);
  z-index: 6;
  animation: maneuver-target-pulse 0.75s ease-in-out infinite alternate;
}
.cell-maneuver-target.cell-replacement-option-a:not(.cell-replacement-option-b)::after,
.cell-maneuver-target.cell-replacement-option-b:not(.cell-replacement-option-a)::after {
  position: absolute;
  right: 2px;
  top: 1px;
  color: #052e16;
  font-size: 0.58rem;
  font-weight: 950;
  line-height: 1;
}
.cell-maneuver-target.cell-replacement-option-a:not(.cell-replacement-option-b)::after {
  content: 'I';
}
.cell-maneuver-target.cell-replacement-option-b:not(.cell-replacement-option-a)::after {
  content: 'II';
}
.cell-capture-muted {
  filter: grayscale(1) brightness(0.52);
  opacity: 0.46;
  pointer-events: none;
}
.cell-capture-focused {
  filter: none;
  opacity: 1;
  pointer-events: auto;
  z-index: 5;
  box-shadow:
    inset 0 0 0 1px color-mix(in srgb, var(--bs-cursed, var(--accent-purple)) 62%, transparent),
    0 0 8px color-mix(in srgb, var(--bs-cursed, var(--accent-purple)) 28%, transparent);
}
@keyframes maneuver-target-pulse {
  from { box-shadow: 0 0 7px rgba(34, 197, 94, 0.62); }
  to { box-shadow: 0 0 19px rgba(34, 197, 94, 1); }
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

.deck-bow {
  position: absolute;
  top: 1px;
  left: 1px;
  display: flex;
  color: #fff;
  filter: drop-shadow(0 0 3px rgba(255, 255, 255, 0.9));
  z-index: 4;
  pointer-events: none;
}
.deck-bow--left { transform: rotate(-90deg); }
.deck-bow--right { transform: rotate(90deg); }
.deck-bow--down { transform: rotate(180deg); }
.deck-bow--up-left { transform: rotate(-45deg); }
.deck-bow--up-right { transform: rotate(45deg); }
.deck-bow--down-right { transform: rotate(135deg); }
.deck-bow--down-left { transform: rotate(-135deg); }
.deck-symbols {
  position: absolute;
  right: 1px;
  bottom: 1px;
  display: flex;
  flex-wrap: wrap-reverse;
  justify-content: flex-end;
  gap: 1px;
  max-width: calc(100% - 2px);
  z-index: 4;
  pointer-events: none;
}
.deck-symbol {
  width: 12px;
  height: 12px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  border-radius: 3px;
  background: rgba(5, 15, 30, 0.82);
  box-shadow: 0 0 4px rgba(255, 255, 255, 0.35);
}
.deck-symbol--armor { color: #facc15; }
.deck-symbol--catapult { color: #f8fafc; }
.deck-symbol--boiler,
.deck-symbol--incendiary,
.deck-symbol--warming { color: #fb923c; }
.deck-symbol--cannon { color: #cbd5e1; }
.deck-symbol--fortuna { color: var(--accent-gold); }

.summon-deaths {
  position: absolute;
  left: 1px;
  bottom: 1px;
  display: flex;
  flex-wrap: wrap-reverse;
  gap: 1px;
  max-width: calc(100% - 2px);
  z-index: 5;
  pointer-events: none;
}
.summon-death {
  position: relative;
  width: 11px;
  height: 11px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: #fca5a5;
  border: 1px solid rgba(248, 113, 113, 0.9);
  border-radius: 50%;
  background: rgba(69, 10, 10, 0.88);
  filter: grayscale(0.3);
}
.summon-death::after {
  content: '×';
  position: absolute;
  color: #fff;
  font-size: 9px;
  font-weight: 900;
  text-shadow: 0 0 2px #000;
}
.summon-death--frozen {
  color: #bae6fd;
  border-color: #7dd3fc;
  background: rgba(7, 45, 74, 0.94);
  box-shadow: 0 0 5px rgba(125, 211, 252, 0.9);
  filter: none;
}
.summon-death-icon {
  width: 9px;
  height: 9px;
  display: inline-flex;
}
.summon-death-freeze-badge {
  position: absolute;
  right: -4px;
  top: -5px;
  width: 8px;
  height: 8px;
  display: inline-flex;
  color: #e0f2fe;
  filter: drop-shadow(0 0 2px #0284c7);
  z-index: 2;
}
.summon-death-icon :deep(svg),
.summon-death-freeze-badge :deep(svg) {
  width: 100%;
  height: 100%;
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
.anim-summon-spawn {
  animation: cell-summon-spawn 1s ease-out forwards;
  z-index: 4;
}
.anim-parrot-arrive-right .cell-icon { animation: parrot-arrive-right 0.72s cubic-bezier(.2,.8,.2,1); }
.anim-parrot-arrive-left .cell-icon { animation: parrot-arrive-left 0.72s cubic-bezier(.2,.8,.2,1); }
.anim-parrot-arrive-down .cell-icon { animation: parrot-arrive-down 0.72s cubic-bezier(.2,.8,.2,1); }
.anim-parrot-arrive-up .cell-icon { animation: parrot-arrive-up 0.72s cubic-bezier(.2,.8,.2,1); }
.anim-parrot-pending .cell-icon { opacity: 0; }
.anim-parrot-settle .cell-icon { animation: parrot-settle 0.36s ease-out; }
[class*='anim-parrot-flight-'] { overflow: visible; }
.parrot-motion-icon {
  position: absolute;
  top: 50%;
  left: 50%;
  width: 16px;
  height: 16px;
  margin: -8px 0 0 -8px;
  z-index: 8;
  color: #4ade80;
  pointer-events: none;
  filter: drop-shadow(0 0 7px rgba(74, 222, 128, 0.9));
}
.anim-parrot-death .parrot-motion-icon {
  animation: parrot-death 0.82s ease-out forwards;
}
[class*='anim-parrot-flight-'] .parrot-motion-icon {
  animation-duration: 0.52s;
  animation-timing-function: linear;
  animation-fill-mode: forwards;
}
[class*='anim-parrot-flight-'][class*='-long'] .parrot-motion-icon { animation-duration: 3.2s; }
[class*='anim-parrot-flight-'][class*='-arrow'] .parrot-motion-icon { animation-duration: 0.43s; }
[class*='anim-parrot-flight-right'] .parrot-motion-icon { animation-name: parrot-flight-right; }
[class*='anim-parrot-flight-left'] .parrot-motion-icon { animation-name: parrot-flight-left; }
[class*='anim-parrot-flight-up'] .parrot-motion-icon { animation-name: parrot-flight-up; }
[class*='anim-parrot-flight-down'] .parrot-motion-icon { animation-name: parrot-flight-down; }
@keyframes parrot-arrive-right {
  from { transform: translateX(-115%) scaleX(1.5); opacity: 0.25; filter: blur(2px); }
  to { transform: translateX(0) scaleX(1); opacity: 1; filter: blur(0); }
}
@keyframes parrot-arrive-left {
  from { transform: translateX(115%) scaleX(1.5); opacity: 0.25; filter: blur(2px); }
  to { transform: translateX(0) scaleX(1); opacity: 1; filter: blur(0); }
}
@keyframes parrot-arrive-down {
  from { transform: translateY(-115%) scaleY(1.5); opacity: 0.25; filter: blur(2px); }
  to { transform: translateY(0) scaleY(1); opacity: 1; filter: blur(0); }
}
@keyframes parrot-arrive-up {
  from { transform: translateY(115%) scaleY(1.5); opacity: 0.25; filter: blur(2px); }
  to { transform: translateY(0) scaleY(1); opacity: 1; filter: blur(0); }
}
@keyframes parrot-settle {
  from { transform: scale(1.35); opacity: 0.4; filter: blur(2px); }
  to { transform: scale(1); opacity: 1; filter: blur(0); }
}
@keyframes parrot-flight-right {
  from { transform: translateX(0) scaleX(1.25); opacity: 0.62; filter: blur(1.4px); }
  to { transform: translateX(calc(var(--cell-size, 32px) + 1px)) scaleX(0.9); opacity: 0.82; filter: blur(0.8px); }
}
@keyframes parrot-flight-left {
  from { transform: translateX(0) scaleX(-1.25); opacity: 0.62; filter: blur(1.4px); }
  to { transform: translateX(calc(0px - var(--cell-size, 32px) - 1px)) scaleX(-0.9); opacity: 0.82; filter: blur(0.8px); }
}
@keyframes parrot-flight-up {
  from { transform: translateY(0) rotate(-90deg) scaleX(1.25); opacity: 0.62; filter: blur(1.4px); }
  to { transform: translateY(calc(0px - var(--cell-size, 32px) - 1px)) rotate(-90deg) scaleX(0.9); opacity: 0.82; filter: blur(0.8px); }
}
@keyframes parrot-flight-down {
  from { transform: translateY(0) rotate(90deg) scaleX(1.25); opacity: 0.62; filter: blur(1.4px); }
  to { transform: translateY(calc(var(--cell-size, 32px) + 1px)) rotate(90deg) scaleX(0.9); opacity: 0.82; filter: blur(0.8px); }
}
@keyframes parrot-death {
  0% { transform: scale(1.15) rotate(0); opacity: 1; }
  35% { transform: scale(1.5) rotate(-18deg); color: #fef08a; opacity: 1; }
  100% { transform: translateY(14px) scale(0.25) rotate(115deg); color: #ef4444; opacity: 0; filter: blur(2px); }
}

@keyframes cell-summon-spawn {
  0% {
    transform: scale(0.35);
    background: white;
    box-shadow: 0 0 0 0 rgba(255, 255, 255, 1), 0 0 28px 12px rgba(255, 255, 255, 0.95);
  }
  35% {
    transform: scale(1.35);
    box-shadow: 0 0 0 9px rgba(255, 255, 255, 0.45), 0 0 22px 9px var(--accent-gold);
  }
  100% {
    transform: scale(1);
    box-shadow: 0 0 0 18px transparent, 0 0 0 transparent;
  }
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
  0% { transform: translateY(0) scale(1.15); background: var(--text-primary); filter: brightness(1.25); }
  22% { background: var(--accent-red); }
  55% { transform: translateY(4px) scale(0.88); opacity: 0.7; background: #101a28; filter: brightness(.7); }
  100% { transform: translateY(13px) scale(0.62); opacity: 0.12; background: #01030a; filter: blur(1px) brightness(.35); box-shadow: inset 0 0 14px #000; }
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
.trail-parrot::before {
  width: 9px;
  height: 3px;
  border-radius: 999px;
  background: #4ade80;
  opacity: 0.3;
  filter: blur(0.5px);
  box-shadow: 0 0 5px rgba(74, 222, 128, 0.55);
}
.trail-boarding::before {
  width: 8px;
  height: 5px;
  border-radius: 2px;
  background: #86efac;
  opacity: 0.35;
  box-shadow: 0 0 5px rgba(134, 239, 172, 0.55);
}
.trail-multiple::before {
  width: 9px;
  height: 9px;
  opacity: 0.5;
  background: conic-gradient(
    var(--accent-red),
    var(--accent-blue),
    var(--accent-orange),
    var(--accent-purple),
    var(--accent-gold),
    var(--accent-red)
  );
  box-shadow: 0 0 5px color-mix(in srgb, var(--accent-blue) 35%, transparent);
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
.range-grab::after {
  background: color-mix(in srgb, #800020 58%, transparent);
  border: 2px solid #be123c;
  box-shadow: inset 0 0 8px rgba(76, 5, 25, 0.82), 0 0 6px rgba(159, 18, 57, 0.38);
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
