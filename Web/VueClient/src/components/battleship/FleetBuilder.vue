<script setup lang="ts">
import { ref, computed } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'
import type { BattleshipShipCatalogEntry, BattleshipFleetSelection } from 'src/services/signalr'
import { useTip } from 'src/composables/useTip'
import { currentLocale } from 'src/i18n'
import BsIcon from './BsIcon.vue'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const store = useBattleshipStore()

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

// Template: deckCount → slot count
const TEMPLATE: Record<number, number> = { 1: 4, 2: 3, 3: 2, 4: 1 }
const DEFAULT_IDS: Record<number, string> = { 1: 'single', 2: 'double', 3: 'triple', 4: 'tetranavis' }
const DECK_LABELS: Record<number, string> = { 1: '1-палубные', 2: '2-палубные', 3: '3-палубные', 4: '4-палубные' }

interface FleetSlot {
  definitionId: string
  shipName: string
  cost: number
  upgrades: string[]
  isDefault: boolean
}

// Initialize all 10 slots with defaults
function createDefaultSlots(): FleetSlot[] {
  const slots: FleetSlot[] = []
  for (const dc of [1, 2, 3, 4]) {
    const defId = DEFAULT_IDS[dc]
    for (let i = 0; i < TEMPLATE[dc]; i++) {
      slots.push({ definitionId: defId, shipName: defId.charAt(0).toUpperCase() + defId.slice(1), cost: 0, upgrades: [], isDefault: true })
    }
  }
  return slots
}

const slots = ref<FleetSlot[]>(createDefaultSlots())

// Boiler weapon choice: 'GreekFire' or 'Brander'
const boilerWeaponChoice = ref<'GreekFire' | 'Brander'>('GreekFire')

const catalog = computed(() => store.shipCatalog)

// Slots grouped by deck count
function slotsForDeck(dc: number): { slot: FleetSlot; globalIndex: number }[] {
  const result: { slot: FleetSlot; globalIndex: number }[] = []
  let idx = 0
  for (const d of [1, 2, 3, 4]) {
    for (let i = 0; i < TEMPLATE[d]; i++) {
      if (d === dc) result.push({ slot: slots.value[idx], globalIndex: idx })
      idx++
    }
  }
  return result
}

const totalCost = computed(() => {
  let cost = 0
  for (const slot of slots.value) {
    const def = catalog.value.find(s => s.id === slot.definitionId)
    if (def) {
      cost += def.cost
      for (const uid of slot.upgrades) {
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
  for (const slot of slots.value) {
    const def = catalog.value.find(s => s.id === slot.definitionId)
    for (const region of def?.regions ?? (def?.region ? [def.region] : [])) regions.add(region)
  }
  return regions
})

const regionCount = computed(() => usedRegions.value.size)
const overRegionLimit = computed(() => regionCount.value > 3)

// How many default slots remain for a given deck count
function defaultSlotsLeft(dc: number): number {
  return slotsForDeck(dc).filter(s => s.slot.isDefault).length
}

// Get the def of a ship from catalog
function getShipDef(id: string) {
  return catalog.value.find(s => s.id === id)
}

// Add purchased ship → replaces first default slot of matching deck count
function addShip(def: BattleshipShipCatalogEntry) {
  if (def.cost > coinsLeft.value) return
  const deckSlots = slotsForDeck(def.deckCount)
  const defaultSlot = deckSlots.find(s => s.slot.isDefault)
  if (!defaultSlot) return // no empty slot
  const s = slots.value[defaultSlot.globalIndex]
  s.definitionId = def.id
  s.shipName = def.nameRu || def.name
  s.cost = def.cost
  s.upgrades = []
  s.isDefault = false
}

// Remove purchased ship → restore to default
function removeShip(globalIndex: number) {
  const slot = slots.value[globalIndex]
  if (slot.isDefault) return
  const def = getShipDef(slot.definitionId)
  const dc = def?.deckCount ?? 1
  const defaultId = DEFAULT_IDS[dc]
  slot.definitionId = defaultId
  slot.shipName = defaultId.charAt(0).toUpperCase() + defaultId.slice(1)
  slot.cost = 0
  slot.upgrades = []
  slot.isDefault = true
}

function toggleUpgrade(globalIndex: number, upgradeId: string) {
  const slot = slots.value[globalIndex]
  if (!slot) return
  const idx = slot.upgrades.indexOf(upgradeId)
  if (idx >= 0) {
    slot.upgrades.splice(idx, 1)
  } else {
    slot.upgrades.push(upgradeId)
  }
}

const BOILER_UPGRADE_IDS = ['tetra_boiler_fire', 'tetra_boiler_brander']

function isBoilerUpgrade(upgradeId: string): boolean {
  return BOILER_UPGRADE_IDS.includes(upgradeId)
}

function hasBoilerUpgrade(globalIndex: number): boolean {
  const slot = slots.value[globalIndex]
  if (!slot) return false
  return slot.upgrades.some(u => isBoilerUpgrade(u))
}

function setBoilerChoice(globalIndex: number, choice: 'GreekFire' | 'Brander') {
  const slot = slots.value[globalIndex]
  if (!slot) return
  boilerWeaponChoice.value = choice
  slot.upgrades = slot.upgrades.filter(u => !isBoilerUpgrade(u))
  slot.upgrades.push(choice === 'GreekFire' ? 'tetra_boiler_fire' : 'tetra_boiler_brander')
}

async function confirmFleet() {
  // Send only non-default ships + free ships with upgrades
  const selections: BattleshipFleetSelection[] = []
  for (const slot of slots.value) {
    if (!slot.isDefault || slot.upgrades.length > 0) {
      selections.push({
        definitionId: slot.definitionId,
        shipName: slot.shipName,
        cost: slot.cost,
        upgrades: [...slot.upgrades],
      })
    }
  }
  await store.selectFleet(selections)
}

// Full descriptions (tooltips) — pre-existing strings, kept verbatim
function abilityLabel(a: string): string {
  switch (a) {
    case 'ballista_immune': return 'Иммунитет к баллисте'
    case 'burn_resist': return 'Огнеупорность — не горит'
    case 'auto_dodge_bow_stern': return 'Авто-уклонение при попадании в нос/корму'
    case 'manual_move_after_hit': return 'Маневр — двигается после потери палубы'
    case 'explode_on_hit': return 'Взрывается при любом попадании'
    case 'spawn_pirate_boat': return 'Пираты — выпускают Пиратскую лодку при гибели'
    case 'spawn_cursed_boat': return 'Выпускает проклятый корабль при гибели'
    case 'poison_cone': return 'Ядовитый конус — убивает в зоне'
    case 'auto_win_boarding': return 'Авто-победа при абордаже'
    case 'freeze_nearby': return 'Аура заморозки — убивает в Space'
    default: return a
  }
}

// Short human-readable chip labels (instead of raw keys like explode_on_hit)
function abilityShortLabel(a: string): string {
  switch (a) {
    case 'ballista_immune': return t('Ballista immune', 'Иммунитет к баллисте')
    case 'burn_resist': return t('Fireproof', 'Огнеупорный')
    case 'auto_dodge_bow_stern': return t('Auto-dodge', 'Авто-уклонение')
    case 'manual_move_after_hit': return t('Maneuver', 'Маневр')
    case 'explode_on_hit': return t('Explosive', 'Взрывной')
    case 'spawn_pirate_boat': return t('Pirates', 'Пираты')
    case 'spawn_cursed_boat': return t('Cursed boat', 'Проклятый кораблик')
    case 'poison_cone': return t('Poison cone', 'Ядовитый конус')
    case 'auto_win_boarding': return t('Boarding master', 'Мастер абордажа')
    case 'freeze_nearby': return t('Freeze aura', 'Аура заморозки')
    default: return a
  }
}

function getRegionColor(region: string): string {
  switch (region?.toLowerCase()) {
    case 'south': case 'юг': return 'var(--accent-coral, #f87171)'
    case 'west': case 'запад': return 'var(--accent-blue)'
    case 'north': case 'север': return 'var(--accent-teal, #2dd4bf)'
    case 'east': case 'восток': return 'var(--accent-orange)'
    default: return 'var(--text-muted)'
  }
}

// Ships in catalog available for a given deck count (has free slot + affordable)
function catalogForDeck(dc: number) {
  return buyableShips.value.filter(s => s.deckCount === dc)
}
</script>

<template>
  <div class="fleet-builder">
    <!-- Header -->
    <div class="builder-header bs-card">
      <div class="builder-heading">
        <span class="bs-kicker"><BsIcon icon="sailboat" :size="13" /> {{ t('Fleet dock', 'Верфь') }}</span>
        <h3 class="bs-title">Сборка флота</h3>
      </div>
      <div class="header-stats">
        <div
          class="bs-chip bs-chip--gold budget bs-mono"
          :class="{ 'over-budget': coinsLeft < 0 }"
          @mouseenter="showTip($event, 'Оставшийся бюджет для покупки кораблей')"
          @mousemove="moveTip"
          @mouseleave="hideTip"
        >
          <BsIcon icon="coins" :size="14" />
          {{ coinsLeft }} / 40 монет
        </div>
        <div
          class="bs-chip region-counter bs-mono"
          :class="{ 'over-budget': overRegionLimit }"
          @mouseenter="showTip($event, 'Используемые регионы (максимум 3)')"
          @mousemove="moveTip"
          @mouseleave="hideTip"
        >
          <BsIcon icon="map" :size="14" />
          Регионы: {{ regionCount }}/3
        </div>
      </div>
      <div v-if="overRegionLimit" class="region-warning">Макс. 3 региона! Уберите корабль из лишнего региона.</div>
    </div>

    <!-- Fleet Slots by Deck Count -->
    <div v-for="dc in [1, 2, 3, 4]" :key="dc" class="section">
      <div class="section-title">{{ DECK_LABELS[dc] }} ({{ TEMPLATE[dc] }} шт.)</div>

      <div class="slot-group">
        <div v-for="{ slot, globalIndex } in slotsForDeck(dc)" :key="globalIndex" class="bs-card ship-entry" :class="{ 'slot-default': slot.isDefault }">
          <div class="ship-entry-header">
            <span class="slot-badge bs-mono" :class="slot.isDefault ? 'badge-default' : 'badge-purchased'">
              {{ slot.isDefault ? 'стд' : 'куп' }}
            </span>
            <span class="ship-name">{{ slot.isDefault ? (getShipDef(slot.definitionId)?.nameRu || slot.shipName) : slot.shipName }}</span>
            <span v-if="!slot.isDefault" class="ship-cost bs-mono">{{ getShipDef(slot.definitionId)?.cost ?? 0 }}c</span>
            <button
              v-if="!slot.isDefault"
              class="remove-btn"
              @mouseenter="showTip($event, 'Убрать и вернуть стандартный корабль')"
              @mousemove="moveTip" @mouseleave="hideTip"
              @click="removeShip(globalIndex)"
            >
              X
            </button>
          </div>

          <!-- Upgrades for this slot's ship -->
          <div v-if="getShipDef(slot.definitionId)?.availableUpgrades?.length" class="upgrades">
            <template v-for="upg in getShipDef(slot.definitionId)!.availableUpgrades" :key="upg.id">
              <template v-if="isBoilerUpgrade(upg.id)"><!-- handled below --></template>
              <button
                v-else-if="upg.name === 'Diskomety' || upg.name === 'Дискометы'"
                class="upgrade-btn upgrade-disabled"
                disabled
                @mouseenter="showTip($event, 'WIP')" @mousemove="moveTip" @mouseleave="hideTip"
              >
                {{ upg.nameRu || upg.name }} ({{ upg.cost }}c) <span class="wip-badge bs-mono">WIP</span>
              </button>
              <button
                v-else
                class="upgrade-btn"
                :aria-pressed="slot.upgrades.includes(upg.id)"
                :class="slot.upgrades.includes(upg.id) ? 'upgrade-active' : 'upgrade-inactive'"
                @mouseenter="showTip($event, upg.description || upg.name)"
                @mousemove="moveTip" @mouseleave="hideTip"
                @click="toggleUpgrade(globalIndex, upg.id)"
              >
                {{ upg.nameRu || upg.name }} ({{ upg.cost }}c)
              </button>
            </template>

            <!-- Boiler weapon choice -->
            <div v-if="getShipDef(slot.definitionId)!.availableUpgrades.some(u => isBoilerUpgrade(u.id))" class="boiler-choice">
              <span class="boiler-label bs-mono">Котельная ({{ getShipDef(slot.definitionId)!.availableUpgrades.find(u => isBoilerUpgrade(u.id))?.cost ?? 0 }}c):</span>
              <button class="upgrade-btn" :class="{ 'upgrade-inactive': hasBoilerUpgrade(globalIndex) }" @click="slot.upgrades = slot.upgrades.filter(u => !isBoilerUpgrade(u))" :disabled="!hasBoilerUpgrade(globalIndex)">Нет</button>
              <button class="upgrade-btn" :class="hasBoilerUpgrade(globalIndex) && boilerWeaponChoice === 'GreekFire' ? 'upgrade-active' : 'upgrade-inactive'" @click="setBoilerChoice(globalIndex, 'GreekFire')">Греческий огонь</button>
              <button class="upgrade-btn" :class="hasBoilerUpgrade(globalIndex) && boilerWeaponChoice === 'Brander' ? 'upgrade-active' : 'upgrade-inactive'" @click="setBoilerChoice(globalIndex, 'Brander')">Брандер</button>
            </div>
          </div>
        </div>
      </div>

      <!-- Catalog for this deck count -->
      <div v-if="catalogForDeck(dc).length" class="deck-catalog">
        <div class="catalog-label">Доступные замены:</div>
        <div class="ship-catalog">
          <div v-for="def in catalogForDeck(dc)" :key="def.id" class="bs-card catalog-card">
            <div class="catalog-header">
              <div class="catalog-name-row">
                <span class="catalog-ship-name">{{ def.nameRu || def.name }}</span>
                <span
                  v-for="region in def.regions ?? (def.region ? [def.region] : [])"
                  :key="region"
                  class="region-badge bs-mono"
                  :style="{ color: getRegionColor(region) }"
                >{{ region }}</span>
              </div>
              <span class="ship-stats bs-mono">HP {{ def.deckHpOverrides ? def.deckHpOverrides.join('/') : def.defaultArmor }} | Скор. {{ def.speed }} | Зона {{ def.space }} | {{ def.range }} | {{ def.cost }}м</span>
            </div>
            <div v-if="def.description" class="ship-desc">{{ def.description }}</div>
            <div v-if="def.abilities.length" class="ship-abilities">
              <span v-for="a in def.abilities" :key="a" class="ability-tag" @mouseenter="showTip($event, abilityLabel(a))" @mousemove="moveTip" @mouseleave="hideTip">{{ abilityShortLabel(a) }}</span>
            </div>
            <button
              class="bs-btn bs-btn--primary bs-btn--sm catalog-add-btn"
              :disabled="def.cost > coinsLeft || defaultSlotsLeft(dc) === 0"
              @mouseenter="showTip($event, def.cost > coinsLeft ? `Нужно ещё ${def.cost - coinsLeft} монет` : defaultSlotsLeft(dc) === 0 ? 'Нет свободных слотов — уберите купленный корабль' : `Заменить стандартный ${DECK_LABELS[dc]} корабль`)"
              @mousemove="moveTip" @mouseleave="hideTip"
              @click="addShip(def)"
            >
              Заменить
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Confirm Button -->
    <button
      class="bs-btn bs-btn--primary bs-btn--lg confirm-btn"
      :disabled="coinsLeft < 0 || overRegionLimit"
      @mouseenter="showTip($event, coinsLeft < 0 ? 'Превышен бюджет' : overRegionLimit ? 'Слишком много регионов (макс. 3)' : 'Подтвердить выбор флота')"
      @mousemove="moveTip" @mouseleave="hideTip"
      @click="confirmFleet"
    >
      <BsIcon icon="flag" :size="18" />
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
.fleet-builder {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

/* ── Header ────────────────────────────────────────────── */
.builder-header {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
}
.builder-heading {
  display: flex;
  flex-direction: column;
  gap: 3px;
}
.header-stats {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  flex-wrap: wrap;
}
.budget {
  font-size: 0.78rem;
}
.region-counter {
  font-size: 0.78rem;
}
.budget.over-budget,
.region-counter.over-budget {
  --bs-chip-color: var(--accent-red);
}
.region-warning {
  width: 100%;
  font-size: 0.75rem;
  color: var(--accent-red);
  font-weight: 600;
  text-align: right;
  margin-top: 0.25rem;
}

/* ── Sections ──────────────────────────────────────────── */
.section {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
.section-title {
  color: var(--text-muted);
  font-size: 0.68rem;
  font-weight: 900;
  margin-bottom: 0.25rem;
  text-transform: uppercase;
  letter-spacing: 1.5px;
}

/* ── Slot group ───────────────────────────────────────── */
.slot-group {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.ship-entry {
  padding: 0.5rem 0.75rem;
  border-radius: 10px;
}
.slot-default {
  opacity: 0.65;
  border-left: 3px solid var(--glass-border);
}
.ship-entry:not(.slot-default) {
  border-left: 3px solid var(--accent-gold);
}
.ship-entry-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.slot-badge {
  font-size: 0.6rem;
  font-weight: 700;
  text-transform: uppercase;
  padding: 1px 5px;
  border-radius: 4px;
  letter-spacing: 0.5px;
}
.badge-default {
  background: var(--bg-inset);
  color: var(--text-dim);
}
.badge-purchased {
  background: var(--accent-gold);
  color: var(--bg-primary);
}
.ship-name {
  font-weight: 700;
  color: var(--text-primary);
  font-size: 0.85rem;
}
.ship-cost {
  color: var(--accent-gold);
  font-size: 0.75rem;
}
.remove-btn {
  margin-left: auto;
  background: transparent;
  border: 1px solid color-mix(in srgb, var(--accent-red) 45%, transparent);
  color: var(--accent-red);
  border-radius: 6px;
  padding: 2px 8px;
  cursor: pointer;
  font-family: var(--font-mono);
  font-size: 0.7rem;
  font-weight: 700;
  transition: background 0.15s ease, color 0.15s ease;
}
.remove-btn:hover {
  background: var(--accent-red);
  color: var(--bg-primary);
}

/* ── Upgrades ──────────────────────────────────────────── */
.upgrades {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 0.375rem;
}
.upgrade-btn {
  font-size: 0.72rem;
  font-weight: 600;
  padding: 3px 10px;
  border-radius: 7px;
  border: 1px solid var(--glass-border);
  cursor: pointer;
  transition: background 0.15s ease, color 0.15s ease, border-color 0.15s ease;
}
.upgrade-active {
  background: var(--accent-gold);
  color: var(--bg-primary);
  border-color: color-mix(in srgb, var(--accent-gold) 70%, white);
}
.upgrade-inactive {
  background: rgba(255, 255, 255, 0.03);
  color: var(--text-muted);
}
.upgrade-inactive:hover {
  border-color: var(--border-color);
  color: var(--text-primary);
}
.upgrade-disabled {
  background: rgba(255, 255, 255, 0.02);
  color: var(--text-dim);
  border-color: var(--glass-border);
  opacity: 0.5;
  cursor: not-allowed;
}
.wip-badge {
  font-size: 0.55rem;
  color: var(--text-dim);
  opacity: 0.6;
  margin-left: 2px;
  text-transform: uppercase;
}
.boiler-choice {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px;
  width: 100%;
  margin-top: 4px;
  padding-top: 4px;
  border-top: 1px solid var(--glass-border);
}
.boiler-label {
  font-size: 0.72rem;
  color: var(--text-dim);
  font-weight: 600;
  white-space: nowrap;
}

/* ── Deck catalog ─────────────────────────────────────── */
.deck-catalog {
  margin-top: 0.25rem;
}
.catalog-label {
  font-size: 0.68rem;
  color: var(--text-dim);
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 1px;
  margin-bottom: 0.25rem;
}
.ship-catalog {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(230px, 1fr));
  gap: 0.5rem;
}
.catalog-card {
  --bs-accent: var(--accent-gold);
  padding: 0.6rem 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  transition: transform 0.15s var(--ease-spring), box-shadow 0.15s ease;
}
.catalog-card:hover {
  transform: translateY(-2px);
  box-shadow:
    0 10px 28px rgba(0, 0, 0, 0.32),
    0 0 26px color-mix(in srgb, var(--accent-gold) 10%, transparent),
    inset 0 1px 0 var(--glass-highlight);
}
.catalog-header {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.catalog-name-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.375rem;
}
.catalog-ship-name {
  font-weight: 800;
  color: var(--text-primary);
  font-size: 0.88rem;
}
.region-badge {
  font-size: 0.6rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}
.ship-stats {
  font-size: 0.62rem;
  color: var(--text-dim);
}
.ship-desc {
  font-size: 0.73rem;
  color: var(--text-muted);
  line-height: 1.4;
}
.ship-abilities {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.ability-tag {
  font-size: 0.63rem;
  font-weight: 600;
  padding: 1px 7px;
  border-radius: 7px;
  background: color-mix(in srgb, var(--accent-blue) 10%, transparent);
  color: var(--accent-blue);
  border: 1px solid color-mix(in srgb, var(--accent-blue) 25%, transparent);
  cursor: help;
}
.catalog-add-btn {
  align-self: flex-start;
  margin-top: 0.25rem;
}

/* ── Confirm ───────────────────────────────────────────── */
.confirm-btn {
  align-self: center;
  margin-top: 0.5rem;
}
</style>
