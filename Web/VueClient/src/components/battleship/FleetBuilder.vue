<script setup lang="ts">
import { ref, computed } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'
import type { BattleshipShipCatalogEntry, BattleshipFleetSelection } from 'src/services/signalr'
import { useTip } from 'src/composables/useTip'
import { renderIcon } from './battleship-icons'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const store = useBattleshipStore()

const selectedShips = ref<BattleshipFleetSelection[]>([
  { definitionId: 'single', shipName: 'Single', cost: 0, upgrades: [] },
  { definitionId: 'double', shipName: 'Double', cost: 0, upgrades: [] },
  { definitionId: 'triple', shipName: 'Triple', cost: 0, upgrades: [] },
  { definitionId: 'tetranavis', shipName: 'Tetranavis', cost: 0, upgrades: [] },
])

// Boiler weapon choice: 'GreekFire' or 'Brander'
const boilerWeaponChoice = ref<'GreekFire' | 'Brander'>('GreekFire')

const catalog = computed(() => store.shipCatalog)

const totalCost = computed(() => {
  let cost = 0
  for (const sel of selectedShips.value) {
    const def = catalog.value.find(s => s.id === sel.definitionId)
    if (def) {
      cost += def.cost
      for (const uid of sel.upgrades) {
        const upg = def.availableUpgrades.find(u => u.id === uid)
        if (upg) cost += upg.cost
      }
    }
  }
  return cost
})

const coinsLeft = computed(() => 40 - totalCost.value)
const buyableShips = computed(() => catalog.value.filter(s => !s.isFree))

const usedRegions = computed(() => {
  const regions = new Set<string>()
  for (const sel of selectedShips.value) {
    const def = catalog.value.find(s => s.id === sel.definitionId)
    if (def?.region) regions.add(def.region)
  }
  return regions
})

const regionCount = computed(() => usedRegions.value.size)
const overRegionLimit = computed(() => regionCount.value > 3)

function addShip(def: BattleshipShipCatalogEntry) {
  if (def.cost > coinsLeft.value) return
  selectedShips.value.push({
    definitionId: def.id,
    shipName: def.name,
    cost: def.cost,
    upgrades: [],
  })
}

function removeShip(index: number) {
  const ship = selectedShips.value[index]
  if (ship && catalog.value.find(s => s.id === ship.definitionId)?.isFree) return
  selectedShips.value.splice(index, 1)
}

function toggleUpgrade(shipIndex: number, upgradeId: string) {
  const sel = selectedShips.value[shipIndex]
  if (!sel) return
  const idx = sel.upgrades.indexOf(upgradeId)
  if (idx >= 0) {
    sel.upgrades.splice(idx, 1)
  } else {
    sel.upgrades.push(upgradeId)
  }
}

const BOILER_UPGRADE_IDS = ['tetra_boiler_fire', 'tetra_boiler_brander']

function isBoilerUpgrade(upgradeId: string): boolean {
  return BOILER_UPGRADE_IDS.includes(upgradeId)
}

function hasBoilerUpgrade(shipIndex: number): boolean {
  const sel = selectedShips.value[shipIndex]
  if (!sel) return false
  return sel.upgrades.some(u => isBoilerUpgrade(u))
}

function setBoilerChoice(shipIndex: number, choice: 'GreekFire' | 'Brander') {
  const sel = selectedShips.value[shipIndex]
  if (!sel) return
  boilerWeaponChoice.value = choice
  // Remove both boiler upgrade IDs, then add the chosen one
  sel.upgrades = sel.upgrades.filter(u => !isBoilerUpgrade(u))
  sel.upgrades.push(choice === 'GreekFire' ? 'tetra_boiler_fire' : 'tetra_boiler_brander')
}

async function confirmFleet() {
  await store.selectFleet(selectedShips.value)
}

function getShipDef(id: string) {
  return catalog.value.find(s => s.id === id)
}

function abilityLabel(a: string): string {
  switch (a) {
    case 'nimble': return 'Юркий — уклоняется от баллисты'
    case 'ballista_immune': return 'Иммунитет к баллисте'
    case 'burn_resist': return 'Огнеупорность — не горит'
    case 'auto_dodge_bow_stern': return 'Авто-уклонение при попадании в нос/корму'
    case 'manual_move_after_hit': return 'Маневр — двигается после потери палубы'
    case 'explode_on_hit': return 'Взрывается при любом попадании'
    case 'spawn_pirate_boat': return 'Выпускает пиратский корабль при гибели'
    case 'spawn_cursed_boat': return 'Выпускает проклятый корабль при гибели'
    case 'poison_cone': return 'Ядовитый конус — убивает в зоне'
    case 'auto_win_boarding': return 'Авто-победа при абордаже'
    case 'stationary': return 'Неподвижный — не двигается'
    case 'freeze_nearby': return 'Аура заморозки — убивает в Space'
    default: return a
  }
}

function getRegionColor(region: string): string {
  switch (region?.toLowerCase()) {
    case 'south': case 'юг': return 'var(--accent-coral, #f87171)'
    case 'west': case 'запад': return 'var(--accent-blue)'
    case 'north': case 'север': return 'var(--accent-teal, #2dd4bf)'
    case 'east': case 'восток': return '#fb923c'
    default: return 'var(--text-muted)'
  }
}
</script>

<template>
  <div class="fleet-builder bs-pirate">
    <!-- Header -->
    <div class="builder-header">
      <h3 class="builder-title bs-font-title">Сборка флота</h3>
      <div class="header-stats">
        <div
          class="budget bs-font-data"
          :class="{ 'over-budget': coinsLeft < 0 }"
          @mouseenter="showTip($event, 'Оставшийся бюджет для покупки кораблей')"
          @mousemove="moveTip"
          @mouseleave="hideTip"
        >
          <span class="coin-icon" v-html="renderIcon('anchor', 14)"></span>
          {{ coinsLeft }} / 40 монет
        </div>
        <div
          class="region-counter bs-font-data"
          :class="{ 'over-budget': overRegionLimit }"
          @mouseenter="showTip($event, 'Используемые регионы (максимум 3)')"
          @mousemove="moveTip"
          @mouseleave="hideTip"
        >
          Регионы: {{ regionCount }}/3
        </div>
      </div>
      <div v-if="overRegionLimit" class="region-warning">Макс. 3 региона! Уберите корабль из лишнего региона.</div>
    </div>

    <!-- Selected Fleet Section -->
    <div class="section">
      <div class="section-title bs-font-body">Ваш флот</div>
      <div class="selected-ships">
        <div v-for="(sel, idx) in selectedShips" :key="idx" class="card-wood ship-entry">
          <div class="ship-entry-header">
            <span class="ship-name bs-font-body">{{ sel.shipName }}</span>
            <span class="ship-cost bs-font-data">{{ getShipDef(sel.definitionId)?.cost ?? 0 }}c</span>
            <button
              v-if="!getShipDef(sel.definitionId)?.isFree"
              class="remove-btn"
              @click="removeShip(idx)"
            >
              X
            </button>
          </div>

          <!-- Upgrades -->
          <div v-if="getShipDef(sel.definitionId)?.availableUpgrades?.length" class="upgrades">
            <template v-for="upg in getShipDef(sel.definitionId)!.availableUpgrades" :key="upg.id">
              <!-- Skip boiler sub-upgrades from general toggle list -->
              <template v-if="isBoilerUpgrade(upg.id)"><!-- handled below --></template>
              <button
                v-else-if="upg.name === 'Diskomety' || upg.name === 'Дискометы'"
                class="upgrade-btn upgrade-disabled"
                disabled
                @mouseenter="showTip($event, 'WIP')" @mousemove="moveTip" @mouseleave="hideTip"
              >
                {{ upg.nameRu || upg.name }} ({{ upg.cost }}c) <span class="wip-badge">WIP</span>
              </button>
              <button
                v-else
                class="upgrade-btn"
                :class="sel.upgrades.includes(upg.id) ? 'upgrade-active' : 'upgrade-inactive'"
                @mouseenter="showTip($event, upg.description || upg.name)"
                @mousemove="moveTip" @mouseleave="hideTip"
                @click="toggleUpgrade(idx, upg.id)"
              >
                {{ upg.nameRu || upg.name }} ({{ upg.cost }}c)
              </button>
            </template>

            <!-- Boiler weapon choice (radio) — only show if ship has boiler upgrades available -->
            <div v-if="getShipDef(sel.definitionId)!.availableUpgrades.some(u => isBoilerUpgrade(u.id))" class="boiler-choice">
              <span class="boiler-label bs-font-data">Котельная ({{ getShipDef(sel.definitionId)!.availableUpgrades.find(u => isBoilerUpgrade(u.id))?.cost ?? 0 }}c):</span>
              <button
                class="upgrade-btn"
                :class="{ 'upgrade-inactive': hasBoilerUpgrade(idx) }"
                @click="sel.upgrades = sel.upgrades.filter(u => !isBoilerUpgrade(u))"
                :disabled="!hasBoilerUpgrade(idx)"
              >
                Нет
              </button>
              <button
                class="upgrade-btn"
                :class="hasBoilerUpgrade(idx) && boilerWeaponChoice === 'GreekFire' ? 'upgrade-active' : 'upgrade-inactive'"
                @click="setBoilerChoice(idx, 'GreekFire')"
              >
                Греческий огонь
              </button>
              <button
                class="upgrade-btn"
                :class="hasBoilerUpgrade(idx) && boilerWeaponChoice === 'Brander' ? 'upgrade-active' : 'upgrade-inactive'"
                @click="setBoilerChoice(idx, 'Brander')"
              >
                Брандер
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Ship Catalog Section -->
    <div class="section">
      <div class="section-title bs-font-body">Доступные корабли</div>
      <div class="ship-catalog">
        <div v-for="def in buyableShips" :key="def.id" class="card-parchment catalog-card">
          <div class="catalog-header">
            <div class="catalog-name-row">
              <span class="catalog-ship-name bs-font-body">{{ def.nameRu || def.name }}</span>
              <span
                v-if="def.region"
                class="region-badge"
                :style="{ color: getRegionColor(def.region) }"
              >
                {{ def.region }}
              </span>
            </div>
            <span class="ship-stats bs-font-data">{{ def.deckCount }}П | HP {{ def.deckHpOverrides ? def.deckHpOverrides.join('/') : def.defaultArmor }} | Скор. {{ def.speed }} | Зона {{ def.space }} | {{ def.range }} | {{ def.cost }}м</span>
          </div>
          <div v-if="def.description" class="ship-desc bs-font-body">{{ def.description }}</div>
          <div v-if="def.abilities.length" class="ship-abilities">
            <span v-for="a in def.abilities" :key="a" class="ability-tag" @mouseenter="showTip($event, abilityLabel(a))" @mousemove="moveTip" @mouseleave="hideTip">{{ a }}</span>
          </div>
          <button
            class="catalog-add-btn"
            :disabled="def.cost > coinsLeft"
            @mouseenter="showTip($event, def.cost > coinsLeft ? `Нужно ещё ${def.cost - coinsLeft} монет` : 'Добавить в флот')"
            @mousemove="moveTip" @mouseleave="hideTip"
            @click="addShip(def)"
          >
            Добавить
          </button>
        </div>
      </div>
    </div>

    <!-- Confirm Button -->
    <button
      class="btn-pirate confirm-btn"
      :disabled="coinsLeft < 0 || overRegionLimit"
      @mouseenter="showTip($event, coinsLeft < 0 ? 'Превышен бюджет' : overRegionLimit ? 'Слишком много регионов (макс. 3)' : 'Подтвердить выбор флота')"
      @mousemove="moveTip" @mouseleave="hideTip"
      @click="confirmFleet"
    >
      <span class="confirm-icon" v-html="renderIcon('flag', 18)"></span>
      Подтвердить флот
    </button>

    <!-- Tooltip -->
    <Teleport to="body">
      <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
        {{ tipText }}
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
@import './battleship-theme.css';

.fleet-builder {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  background-color: var(--bs-sea-deep);
}

/* ── Header ────────────────────────────────────────────── */
.builder-header {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
}
.builder-title {
  margin: 0;
  color: var(--bs-gold);
  font-size: 1.3rem;
  text-shadow: 0 2px 4px rgba(0, 0, 0, 0.5);
}
.header-stats {
  display: flex;
  gap: 1rem;
  align-items: center;
}
.budget {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 0.9rem;
  font-weight: 700;
  color: var(--bs-gold);
}
.coin-icon {
  display: inline-flex;
  color: var(--bs-gold-bright);
}
.region-counter {
  font-size: 0.9rem;
  font-weight: 700;
  color: var(--bs-parchment-dim);
}
.budget.over-budget,
.region-counter.over-budget {
  color: var(--bs-fire-red);
}
.region-warning {
  width: 100%;
  font-size: 0.75rem;
  color: var(--bs-fire-red);
  font-weight: 600;
  text-align: right;
  margin-top: 0.25rem;
  font-family: 'Crimson Text', 'Georgia', serif;
}

/* ── Sections ──────────────────────────────────────────── */
.section-title {
  color: var(--bs-parchment);
  font-size: 0.9rem;
  font-weight: 700;
  margin-bottom: 0.5rem;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

/* ── Selected ships ────────────────────────────────────── */
.selected-ships {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
.ship-entry {
  padding: 0.5rem 0.75rem;
}
.ship-entry-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.ship-name {
  font-weight: 700;
  color: var(--bs-parchment);
  font-size: 0.9rem;
}
.ship-cost {
  color: var(--bs-gold);
  font-size: 0.8rem;
}
.remove-btn {
  margin-left: auto;
  background: transparent;
  border: 1px solid var(--bs-fire-red);
  color: var(--bs-fire-red);
  border-radius: 3px;
  padding: 2px 8px;
  cursor: pointer;
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  font-size: 0.7rem;
  font-weight: 700;
  transition: background 0.15s ease, color 0.15s ease;
}
.remove-btn:hover {
  background: var(--bs-fire-red);
  color: var(--bs-parchment);
}

/* ── Upgrades ──────────────────────────────────────────── */
.upgrades {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 0.375rem;
}
.upgrade-btn {
  font-family: 'Crimson Text', 'Georgia', serif;
  font-size: 0.75rem;
  font-weight: 600;
  padding: 3px 10px;
  border-radius: 3px;
  border: 1px solid var(--bs-wood-light);
  cursor: pointer;
  transition: background 0.15s ease, color 0.15s ease, border-color 0.15s ease;
}
.upgrade-active {
  background: var(--bs-gold);
  color: var(--bs-wood-dark);
  border-color: var(--bs-gold-bright);
  text-shadow: none;
}
.upgrade-inactive {
  background: var(--bs-wood-dark);
  color: var(--bs-parchment-dim);
  border-color: var(--bs-wood-mid);
}
.upgrade-inactive:hover {
  border-color: var(--bs-wood-light);
  color: var(--bs-parchment);
}
.upgrade-disabled {
  background: var(--bs-wood-dark);
  color: var(--bs-parchment-dim);
  border-color: var(--bs-wood-mid);
  opacity: 0.5;
  cursor: not-allowed;
}
.wip-badge {
  font-size: 0.55rem;
  color: var(--bs-parchment-dim);
  opacity: 0.5;
  margin-left: 2px;
  text-transform: uppercase;
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
}
.boiler-choice {
  display: flex;
  align-items: center;
  gap: 4px;
  width: 100%;
  margin-top: 4px;
  padding-top: 4px;
  border-top: 1px solid var(--bs-wood-mid);
}
.boiler-label {
  font-size: 0.75rem;
  color: var(--bs-parchment-dim);
  font-weight: 600;
  white-space: nowrap;
}

/* ── Ship Catalog ──────────────────────────────────────── */
.ship-catalog {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 0.5rem;
}
.catalog-card {
  padding: 0.5rem 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}
.catalog-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}
.catalog-name-row {
  display: flex;
  align-items: center;
  gap: 0.375rem;
}
.catalog-ship-name {
  font-weight: 700;
  color: var(--bs-wood-dark);
  font-size: 0.9rem;
}
.region-badge {
  font-size: 0.6rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.3px;
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
}
.ship-stats {
  font-size: 0.7rem;
  color: var(--bs-parchment-dim);
  white-space: nowrap;
}
.ship-desc {
  font-size: 0.75rem;
  color: var(--bs-wood-mid);
  line-height: 1.3;
}
.ship-abilities {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.ability-tag {
  font-size: 0.65rem;
  font-family: 'Crimson Text', 'Georgia', serif;
  padding: 1px 6px;
  border-radius: 3px;
  background: rgba(41, 128, 185, 0.12);
  color: var(--bs-ocean-blue);
  border: 1px solid rgba(41, 128, 185, 0.25);
}
.catalog-add-btn {
  align-self: flex-start;
  margin-top: 0.25rem;
  background: var(--bs-poison-green);
  color: var(--bs-gold-bright);
  border: 1px solid rgba(39, 174, 96, 0.6);
  border-radius: 3px;
  padding: 3px 12px;
  font-family: 'Crimson Text', 'Georgia', serif;
  font-size: 0.8rem;
  font-weight: 700;
  cursor: pointer;
  transition: background 0.15s ease, transform 0.1s ease;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.4);
}
.catalog-add-btn:hover:not(:disabled) {
  background: #2ecc71;
  transform: translateY(-1px);
}
.catalog-add-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
  transform: none;
}

/* ── Confirm ───────────────────────────────────────────── */
.confirm-btn {
  align-self: center;
  margin-top: 0.5rem;
  font-size: 1.05rem;
  padding: 10px 32px;
}
.confirm-icon {
  display: inline-flex;
  align-items: center;
}
</style>
