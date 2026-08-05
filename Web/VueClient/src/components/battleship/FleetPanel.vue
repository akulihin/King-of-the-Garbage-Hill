<script setup lang="ts">
import { useTip } from 'src/composables/useTip'
import { renderIcon } from './battleship-icons'

export interface FleetShip {
  id: string
  name: string
  range: string
  speed: number
  space: number
  deckCount: number
  isDestroyed: boolean
  isPlaced: boolean
  orientation: string
  row: number
  col: number
  decks: Array<{
    index: number
    currentHp: number
    maxHp: number
    isDestroyed: boolean
    module?: string | null
    moduleDestroyed?: boolean
  }>
  weapons: Array<{
    id?: string
    type: string
    ammo: number
    hasAmmo: boolean
    aimSpeed: number
    deckIndex?: number
  }>
  upgrades: string[]
  abilities: string[]
  definitionId: string
}

const props = withDefaults(defineProps<{
  fleet: FleetShip[]
  isEnemy?: boolean
  shotCount?: number
  title?: string
}>(), {
  isEnemy: false,
  shotCount: 0,
  title: '',
})

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

function resolvedTitle(): string {
  if (props.title) return props.title
  return props.isEnemy ? 'Разведка противника' : 'Ваш флот'
}

function shipHpPercent(ship: { decks: { currentHp: number; maxHp: number }[] }): number {
  let cur = 0, max = 0
  for (const d of ship.decks) { cur += d.currentHp; max += d.maxHp }
  return max > 0 ? (cur / max) * 100 : 0
}

function shipTooltip(ship: FleetShip): string {
  let text = `${ship.range} | Скор. ${ship.speed} | Зона ${ship.space}`
  if (ship.upgrades.length) {
    text += ' | Улучшения: ' + ship.upgrades.join(', ')
  }
  return text
}

// Expose skull icon HTML for destroyed ships
const skullHtml = renderIcon('skull', 12)
</script>

<template>
  <div v-if="fleet.length" class="fleet-panel bs-card" :class="{ 'enemy-panel': isEnemy }">
    <div class="fleet-header" :class="{ 'header-enemy': isEnemy }">{{ resolvedTitle() }}</div>
    <div class="fleet-status-list">
      <div
        v-for="ship in fleet"
        :key="ship.id"
        class="fleet-ship-entry"
        :class="{ 'ship-sunk': ship.isDestroyed }"
        @mouseenter="!isEnemy && showTip($event, shipTooltip(ship))"
        @mousemove="moveTip"
        @mouseleave="hideTip"
      >
        <span class="fleet-ship-name">{{ ship.name }}</span>

        <!-- Range + speed meta (own fleet only) -->
        <span v-if="!isEnemy" class="fleet-ship-meta">{{ ship.range }} | s{{ ship.speed }}</span>

        <!-- Deck pips -->
        <span class="fleet-ship-decks">
          <span
            v-for="d in ship.decks"
            :key="d.index"
            class="deck-pip"
            :class="{
              'deck-destroyed': d.isDestroyed,
              'deck-damaged': !d.isDestroyed && d.currentHp < d.maxHp,
              'deck-module-destroyed': d.module && d.moduleDestroyed
            }"
            @mouseenter="showTip($event, (d.module ? `[${d.module}${d.moduleDestroyed ? ' X' : ''}] ` : '') + `${d.currentHp}/${d.maxHp} HP`)"
            @mousemove="moveTip"
            @mouseleave="hideTip"
          >
            <span v-if="d.module && !d.moduleDestroyed" class="deck-module-dot"></span>
          </span>
        </span>

        <!-- Ammo pips (own fleet only) -->
        <span v-if="!isEnemy && ship.weapons.length" class="fleet-ship-ammo">
          <span
            v-for="(w, weaponIndex) in ship.weapons"
            :key="w.id ?? `${w.type}-${w.deckIndex ?? weaponIndex}-${weaponIndex}`"
            class="ammo-pip"
            :class="{ 'ammo-pip--empty': w.ammo <= 0 }"
            @mouseenter="showTip($event, `${w.type}: ${w.ammo} выстр.`)"
            @mousemove="moveTip"
            @mouseleave="hideTip"
          >
            {{ w.ammo }}
          </span>
        </span>

        <!-- Sunk label with skull -->
        <span v-if="ship.isDestroyed" class="fleet-ship-status sunk">
          <span class="skull-icon" v-html="skullHtml"></span>
          Потоплен
        </span>

        <!-- HP bar for alive ships -->
        <div v-if="!ship.isDestroyed" class="ship-hp-bar">
          <div
            class="ship-hp-fill"
            :style="{ width: shipHpPercent(ship) + '%' }"
            :class="{
              'hp-low': shipHpPercent(ship) < 30,
              'hp-mid': shipHpPercent(ship) >= 30 && shipHpPercent(ship) < 60
            }"
          ></div>
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
/* ── Fleet panel (glass card) ─────────────────────────────── */
.fleet-panel {
  margin-top: 0.5rem;
  padding: 10px 12px;
}

.fleet-header {
  color: var(--text-muted);
  font-size: 0.62rem;
  font-weight: 900;
  letter-spacing: 1.5px;
  text-transform: uppercase;
  margin-bottom: 0.25rem;
  user-select: none;
}

.fleet-header.header-enemy {
  color: var(--accent-red);
}

/* ── Ship list ────────────────────────────────────────────── */
.fleet-status-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 0.375rem;
}

.fleet-ship-entry {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.8rem;
}

.fleet-ship-entry.ship-sunk {
  opacity: 0.35;
}

.fleet-ship-entry.ship-sunk .fleet-ship-name {
  text-decoration: line-through;
}

/* ── Ship name ────────────────────────────────────────────── */
.fleet-ship-name {
  font-weight: 700;
  color: var(--text-secondary);
  font-size: 0.75rem;
}

/* ── Range/speed meta ─────────────────────────────────────── */
.fleet-ship-meta {
  font-size: 0.6rem;
  color: var(--text-dim);
  font-family: var(--font-mono);
}

/* ── Deck pips ────────────────────────────────────────────── */
.fleet-ship-decks {
  display: flex;
  gap: 2px;
}

.deck-pip {
  position: relative;
  width: 10px;
  height: 10px;
  border-radius: 2px;
  background: var(--accent-green);
}

.deck-pip.deck-damaged {
  background: var(--accent-gold);
}

.deck-pip.deck-destroyed {
  background: var(--accent-red);
  opacity: 0.6;
}

.deck-pip.deck-module-destroyed {
  background: var(--accent-red);
  opacity: 0.4;
  outline: 1px solid var(--accent-red);
}

.deck-module-dot {
  position: absolute;
  top: 1px;
  right: 1px;
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: var(--text-primary);
  opacity: 0.8;
}

/* ── Ammo pips ────────────────────────────────────────────── */
.fleet-ship-ammo {
  display: flex;
  gap: 2px;
  margin-left: auto;
}

.ammo-pip {
  font-size: 0.55rem;
  padding: 0 3px;
  border-radius: 2px;
  background: color-mix(in srgb, var(--accent-gold) 14%, transparent);
  color: var(--accent-gold);
  font-family: var(--font-mono);
  font-weight: 700;
}
.ammo-pip--empty {
  color: var(--text-dim);
  background: color-mix(in srgb, var(--text-dim) 9%, transparent);
  opacity: 0.66;
}

/* ── Sunk status ──────────────────────────────────────────── */
.fleet-ship-status.sunk {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 0.65rem;
  color: var(--accent-red);
  font-weight: 700;
  text-transform: uppercase;
}

.skull-icon {
  display: inline-flex;
  align-items: center;
  color: var(--accent-red);
  line-height: 0;
}

/* ── HP bar ───────────────────────────────────────────────── */
.ship-hp-bar {
  width: 40px;
  height: 4px;
  background: var(--bg-inset);
  border-radius: 2px;
  overflow: hidden;
  margin-left: auto;
}

.ship-hp-fill {
  height: 100%;
  background: var(--accent-green);
  transition: width 0.3s ease;
  border-radius: 2px;
}

.ship-hp-fill.hp-mid {
  background: var(--accent-gold);
}

.ship-hp-fill.hp-low {
  background: var(--accent-red);
}

/* ── Enemy panel accent ───────────────────────────────────── */
.enemy-panel {
  border-color: color-mix(in srgb, var(--accent-red) 30%, var(--glass-border));
}
</style>
