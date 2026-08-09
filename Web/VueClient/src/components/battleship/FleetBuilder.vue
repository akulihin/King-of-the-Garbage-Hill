<script setup lang="ts">
import { ref, computed } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'
import type { BattleshipShipCatalogEntry, BattleshipFleetSelection } from 'src/services/signalr'
import { useTip } from 'src/composables/useTip'
import { currentLocale } from 'src/i18n'
import { message } from 'src/platform/localization'
import BsIcon from './BsIcon.vue'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const store = useBattleshipStore()

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

// Template: deckCount → slot count
const TEMPLATE: Record<number, number> = { 1: 4, 2: 3, 3: 2, 4: 1 }
const DECK_LABELS: Record<number, string> = { 1: '1-палубные', 2: '2-палубные', 3: '3-палубные', 4: '4-палубные' }

interface FleetSlot {
  definitionId: string
  shipName: string
  cost: number
  upgrades: string[]
  isDefault: boolean
}

// Initialize all 10 slots with defaults
function defaultId(deckCount: number, faction: string | undefined): string {
  if (deckCount === 4)
    return faction === 'Alliance' ? 'alliance_flagship' : 'tetranavis'
  return ({ 1: 'single', 2: 'double', 3: 'triple' } as Record<number, string>)[deckCount]
}

function createDefaultSlots(): FleetSlot[] {
  const slots: FleetSlot[] = []
  for (const dc of [1, 2, 3, 4]) {
    const defId = defaultId(dc, store.myPlayer?.faction)
    for (let i = 0; i < TEMPLATE[dc]; i++) {
      slots.push({ definitionId: defId, shipName: defId.charAt(0).toUpperCase() + defId.slice(1), cost: 0, upgrades: [], isDefault: true })
    }
  }
  return slots
}

const slots = ref<FleetSlot[]>(createDefaultSlots())

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

const fleetBudget = computed(() => store.myPlayer?.faction === 'Alliance' ? 50 : 40)
const coinsLeft = computed(() => fleetBudget.value - totalCost.value)

const buyableShips = computed(() => catalog.value.filter(s => !s.isFree))

const usedRegions = computed(() => {
  const regions = new Set<string>()
  for (const slot of slots.value) {
    const def = catalog.value.find(s => s.id === slot.definitionId)
    for (const region of def?.regions ?? (def?.region ? [def.region] : [])) {
      if (region !== 'Tetracor') regions.add(region)
    }
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
  const replacementId = defaultId(dc, store.myPlayer?.faction)
  slot.definitionId = replacementId
  slot.shipName = replacementId.charAt(0).toUpperCase() + replacementId.slice(1)
  slot.cost = 0
  slot.upgrades = []
  slot.isDefault = true
}

function toggleUpgrade(globalIndex: number, upgradeId: string) {
  const slot = slots.value[globalIndex]
  if (!slot) return
  const upgrade = getShipDef(slot.definitionId)?.availableUpgrades.find(u => u.id === upgradeId)
  if (!upgrade || upgrade.isPreinstalled) return
  const idx = slot.upgrades.indexOf(upgradeId)
  if (idx >= 0) {
    slot.upgrades.splice(idx, 1)
  } else {
    if (upgradeId === 'double_mast' && slots.value.some((candidate, candidateIndex) =>
      candidateIndex !== globalIndex && candidate.upgrades.includes('double_mast'))) return
    if (upgrade.cost > coinsLeft.value) return
    slot.upgrades.push(upgradeId)
  }
}

function canToggleUpgrade(globalIndex: number, upgradeId: string): boolean {
  const slot = slots.value[globalIndex]
  if (!slot) return false
  const upgrade = getShipDef(slot.definitionId)?.availableUpgrades.find(u => u.id === upgradeId)
  if (!upgrade || upgrade.isPreinstalled) return false
  if (slot.upgrades.includes(upgradeId)) return true
  if (upgradeId === 'double_mast' && slots.value.some((candidate, candidateIndex) =>
    candidateIndex !== globalIndex && candidate.upgrades.includes('double_mast'))) return false
  return upgrade.cost <= coinsLeft.value
}

function upgradeDescription(upgrade: { name: string; description: string | null; descriptionKey: string | null; cost: number }): string {
  if (upgrade.descriptionKey)
    return message(upgrade.descriptionKey, { cost: String(upgrade.cost) })
  const description = upgrade.description?.trim() || upgrade.name
  return /цена\s*:/i.test(description)
    ? description
    : `${description} Цена: ${upgrade.cost} монет.`
}

function catalogDescription(definition: BattleshipShipCatalogEntry): string {
  if (definition.descriptionKey)
    return message(definition.descriptionKey)
  if (definition.abilities.includes('matryoshka_stage_4'))
    return message('battleship.ability.matryoshka.description')
  return definition.description ?? ''
}

type BoilerChoice = 'GreekFire' | 'Brander' | 'EvilGreekFire'

const BOILER_UPGRADE_BY_CHOICE: Record<BoilerChoice, string> = {
  GreekFire: 'tetra_boiler_fire',
  Brander: 'tetra_boiler_brander',
  EvilGreekFire: 'tetra_boiler_evil_fire',
}
const BOILER_UPGRADE_IDS = Object.values(BOILER_UPGRADE_BY_CHOICE)

function isBoilerUpgrade(upgradeId: string): boolean {
  return BOILER_UPGRADE_IDS.includes(upgradeId)
}

function hasBoilerUpgrade(globalIndex: number): boolean {
  const slot = slots.value[globalIndex]
  if (!slot) return false
  return slot.upgrades.some(u => isBoilerUpgrade(u))
}

function selectedBoilerChoice(globalIndex: number): BoilerChoice | null {
  const slot = slots.value[globalIndex]
  if (!slot) return null
  const entry = Object.entries(BOILER_UPGRADE_BY_CHOICE)
    .find(([, upgradeId]) => slot.upgrades.includes(upgradeId))
  return entry ? entry[0] as BoilerChoice : null
}

function setBoilerChoice(globalIndex: number, choice: BoilerChoice) {
  const slot = slots.value[globalIndex]
  if (!slot) return
  if (!canSetBoilerChoice(globalIndex, choice)) return
  slot.upgrades = slot.upgrades.filter(u => !isBoilerUpgrade(u))
  slot.upgrades.push(BOILER_UPGRADE_BY_CHOICE[choice])
}

function canSetBoilerChoice(globalIndex: number, choice: BoilerChoice): boolean {
  const slot = slots.value[globalIndex]
  if (!slot) return false
  const targetId = BOILER_UPGRADE_BY_CHOICE[choice]
  if (slot.upgrades.includes(targetId)) return true
  const definition = getShipDef(slot.definitionId)
  const targetCost = definition?.availableUpgrades.find(u => u.id === targetId)?.cost ?? Number.POSITIVE_INFINITY
  const selectedCost = definition?.availableUpgrades
    .filter(u => slot.upgrades.includes(u.id) && isBoilerUpgrade(u.id))
    .reduce((sum, u) => sum + u.cost, 0) ?? 0
  return targetCost - selectedCost <= coinsLeft.value
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
  if (a.startsWith('matryoshka_stage_'))
    return message('battleship.ability.matryoshka.description')
  switch (a) {
    case 'ballista_immune': return 'Иммунитет к баллисте'
    case 'burn_resist': return 'Огнеупорность — не горит'
    case 'auto_dodge_bow_stern': return 'Авто-уклонение при попадании в нос/корму'
    case 'manual_move_after_hit': return 'Маневр — двигается после потери палубы'
    case 'ramming_maneuver': return 'Таранный маневр — может войти в Space союзника и уничтожить перекрытые палубы'
    case 'diagonal_shape': return 'Диагональный корпус из четырёх палуб'
    case 'merge_maneuver': return message('battleship.ability.mergeManeuver.description')
    case 'merge_maneuver_after_hit': return message('battleship.ability.mergeAfterHit.description')
    case 'double_shot_while_alive': return message('battleship.ability.doubleShot.description')
    case 'overheat_after_20_shots': return message('battleship.ability.overheat.description')
    case 'warming_chain_until_two_misses': return message('battleship.ability.warmingChain.description')
    case 'capturing_shape': return message('battleship.ability.capturingShape.description')
    case 'grab_summon': return message('battleship.ability.grabSummon.description')
    case 'capture_reward': return message('battleship.ability.captureReward.description')
    case 'crew_boarding_pirate': return message('battleship.ability.boardingCrew.description')
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
  if (a.startsWith('matryoshka_stage_'))
    return message('battleship.ability.matryoshka.label')
  switch (a) {
    case 'ballista_immune': return t('Ballista immune', 'Иммунитет к баллисте')
    case 'burn_resist': return t('Fireproof', 'Огнеупорный')
    case 'auto_dodge_bow_stern': return t('Auto-dodge', 'Авто-уклонение')
    case 'manual_move_after_hit': return t('Maneuver', 'Маневр')
    case 'ramming_maneuver': return t('Ramming maneuver', 'Таранный маневр')
    case 'diagonal_shape': return t('Diagonal hull', 'Диагональный корпус')
    case 'merge_maneuver': return message('battleship.ability.mergeManeuver.label')
    case 'merge_maneuver_after_hit': return message('battleship.ability.mergeAfterHit.label')
    case 'double_shot_while_alive': return message('battleship.ability.doubleShot.label')
    case 'overheat_after_20_shots': return message('battleship.ability.overheat.label')
    case 'warming_chain_until_two_misses': return message('battleship.ability.warmingChain.label')
    case 'capturing_shape': return message('battleship.ability.capturingShape.label')
    case 'grab_summon': return message('battleship.ability.grabSummon.label')
    case 'capture_reward': return message('battleship.ability.captureReward.label')
    case 'crew_boarding_pirate': return message('battleship.ability.boardingCrew.label')
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
          {{ coinsLeft }} / {{ fleetBudget }} монет
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
                v-else-if="upg.isPreinstalled"
                class="upgrade-btn upgrade-disabled"
                disabled
              >
                {{ upg.nameRu || upg.name }}
              </button>
              <button
                v-else-if="upg.id === 'tetra_discus'"
                class="upgrade-btn upgrade-disabled"
                disabled
                @mouseenter="showTip($event, upgradeDescription(upg))" @mousemove="moveTip" @mouseleave="hideTip"
              >
                {{ upg.nameRu || upg.name }} ({{ upg.cost }}c) <span class="wip-badge bs-mono">WIP</span>
              </button>
              <button
                v-else
                class="upgrade-btn"
                :aria-pressed="slot.upgrades.includes(upg.id)"
                :class="[
                  slot.upgrades.includes(upg.id) ? 'upgrade-active' : 'upgrade-inactive',
                  !canToggleUpgrade(globalIndex, upg.id) ? 'upgrade-unavailable' : ''
                ]"
                :disabled="!canToggleUpgrade(globalIndex, upg.id)"
                @mouseenter="showTip($event, canToggleUpgrade(globalIndex, upg.id) ? upgradeDescription(upg) : `${upgradeDescription(upg)} Не хватает ${upg.cost - coinsLeft} монет.`)"
                @mousemove="moveTip" @mouseleave="hideTip"
                @click="toggleUpgrade(globalIndex, upg.id)"
              >
                {{ upg.nameRu || upg.name }} ({{ upg.cost }}c)
              </button>
            </template>

            <!-- Boiler weapon choice -->
            <div v-if="getShipDef(slot.definitionId)!.availableUpgrades.some(u => isBoilerUpgrade(u.id))" class="boiler-choice">
              <span class="boiler-label bs-mono">Котельная:</span>
              <button class="upgrade-btn" :class="{ 'upgrade-inactive': hasBoilerUpgrade(globalIndex) }" @click="slot.upgrades = slot.upgrades.filter(u => !isBoilerUpgrade(u))" :disabled="!hasBoilerUpgrade(globalIndex)">Нет</button>
              <button class="upgrade-btn" :class="[selectedBoilerChoice(globalIndex) === 'GreekFire' ? 'upgrade-active' : 'upgrade-inactive', !canSetBoilerChoice(globalIndex, 'GreekFire') ? 'upgrade-unavailable' : '']" :disabled="!canSetBoilerChoice(globalIndex, 'GreekFire')" @mouseenter="showTip($event, upgradeDescription(getShipDef(slot.definitionId)!.availableUpgrades.find(u => u.id === 'tetra_boiler_fire')!))" @mousemove="moveTip" @mouseleave="hideTip" @click="setBoilerChoice(globalIndex, 'GreekFire')">Греческий огонь (4c)</button>
              <button class="upgrade-btn" :class="[selectedBoilerChoice(globalIndex) === 'Brander' ? 'upgrade-active' : 'upgrade-inactive', !canSetBoilerChoice(globalIndex, 'Brander') ? 'upgrade-unavailable' : '']" :disabled="!canSetBoilerChoice(globalIndex, 'Brander')" @mouseenter="showTip($event, upgradeDescription(getShipDef(slot.definitionId)!.availableUpgrades.find(u => u.id === 'tetra_boiler_brander')!))" @mousemove="moveTip" @mouseleave="hideTip" @click="setBoilerChoice(globalIndex, 'Brander')">Брандер (4c)</button>
              <button
                v-if="getShipDef(slot.definitionId)!.availableUpgrades.some(u => u.id === 'tetra_boiler_evil_fire')"
                class="upgrade-btn"
                :class="[selectedBoilerChoice(globalIndex) === 'EvilGreekFire' ? 'upgrade-active' : 'upgrade-inactive', !canSetBoilerChoice(globalIndex, 'EvilGreekFire') ? 'upgrade-unavailable' : '']"
                :disabled="!canSetBoilerChoice(globalIndex, 'EvilGreekFire')"
                @mouseenter="showTip($event, upgradeDescription(getShipDef(slot.definitionId)!.availableUpgrades.find(u => u.id === 'tetra_boiler_evil_fire')!))"
                @mousemove="moveTip"
                @mouseleave="hideTip"
                @click="setBoilerChoice(globalIndex, 'EvilGreekFire')"
              >Злой Греческий огонь (6c)</button>
            </div>
          </div>
        </div>
      </div>

      <!-- Catalog for this deck count -->
      <div v-if="catalogForDeck(dc).length" class="deck-catalog">
        <div class="catalog-label">Доступные замены:</div>
        <div class="ship-catalog">
          <div v-for="def in catalogForDeck(dc)" :key="def.id" class="bs-card catalog-card" :class="{ 'catalog-card-unavailable': def.cost > coinsLeft || defaultSlotsLeft(dc) === 0 }">
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
              <span class="ship-stats bs-mono">
                HP {{ def.deckHpOverrides ? def.deckHpOverrides.join('/') : def.defaultArmor }}
                | Скор. {{ def.speed }} | Зона {{ def.space }} |
                <span class="range-class" lang="en" translate="no">{{ def.range }}</span>
                | {{ def.cost }}м
              </span>
            </div>
            <div v-if="catalogDescription(def)" class="ship-desc">{{ catalogDescription(def) }}</div>
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
.upgrade-unavailable,
.upgrade-btn:disabled {
  background: color-mix(in srgb, var(--text-dim) 10%, var(--bg-inset));
  color: var(--text-dim);
  border-color: color-mix(in srgb, var(--text-dim) 25%, transparent);
  opacity: 0.48;
  cursor: not-allowed;
  filter: grayscale(1);
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
.catalog-card-unavailable {
  filter: grayscale(0.85);
  opacity: 0.58;
}
.catalog-card-unavailable:hover {
  transform: none;
  box-shadow: inset 0 1px 0 var(--glass-highlight);
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
