<script setup lang="ts">
import { computed, ref } from 'vue'
import { Plus, RotateCcw, Save, Settings2, Trash2, X } from 'lucide-vue-next'
import { useFocusTrapDialog } from 'src/composables/useFocusTrapDialog'
import { currentLocale } from 'src/i18n'
import { cloneValue } from 'src/features/fight-calculator/catalog'
import { DAMAGE_TYPES } from 'src/features/fight-calculator/types'
import type {
  ArmorDefinition,
  ArmorSlot,
  DamageValues,
  FightBalance,
  TalentDefinition,
  WeaponDefinition,
} from 'src/features/fight-calculator/types'

const props = defineProps<{
  balance: FightBalance
  weapons: WeaponDefinition[]
  armors: ArmorDefinition[]
  talents: TalentDefinition[]
}>()

const emit = defineEmits<{
  save: [value: { balance: FightBalance; weapons: WeaponDefinition[]; armors: ArmorDefinition[]; talents: TalentDefinition[] }]
  reset: []
  cancel: []
}>()

const draftBalance = ref(cloneValue(props.balance))
const draftWeapons = ref(cloneValue(props.weapons))
const draftArmors = ref(cloneValue(props.armors))
const draftTalents = ref(cloneValue(props.talents))
const activeTab = ref<'global' | 'weapons' | 'armors' | 'talents'>('global')
const selectedWeaponId = ref(draftWeapons.value[0]?.id ?? '')
const selectedArmorId = ref(draftArmors.value[0]?.id ?? '')
const selectedTalentId = ref(draftTalents.value[0]?.id ?? '')
const { overlayRef, dialogRef, trapTabKey } = useFocusTrapDialog()

const selectedWeapon = computed(() => draftWeapons.value.find(item => item.id === selectedWeaponId.value) ?? null)
const selectedArmor = computed(() => draftArmors.value.find(item => item.id === selectedArmorId.value) ?? null)
const selectedTalent = computed(() => draftTalents.value.find(item => item.id === selectedTalentId.value) ?? null)

function t(ru: string, en: string): string {
  return currentLocale.value === 'ru' ? ru : en
}

function emptyDamage(): DamageValues {
  return { Ударное: 0, Дробящее: 0, Рубящее: 0, Режущее: 0, Колющее: 0 }
}

function customId(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`
}

function addWeapon(): void {
  const item: WeaponDefinition = {
    id: customId('custom-weapon'), name: t('Новое оружие', 'New weapon'), category: t('Кастомное', 'Custom'),
    attacks: emptyDamage(), defense: 0, disarm: 0, antiShield: 0, speed: 1, rangeMin: 1, rangeMax: 1,
    handsMin: 1, handsMax: 1, durability: 5, fatigue: 0.1, isCustom: true,
  }
  draftWeapons.value.push(item)
  selectedWeaponId.value = item.id
}

function addArmor(): void {
  const item: ArmorDefinition = {
    id: customId('custom-armor'), name: t('Новые латы', 'New plate armor'), category: t('Кастомное', 'Custom'),
    slot: 'plate', resists: emptyDamage(), hp: 0, weight: 0, ergonomics: 100, heaviness: 0, isCustom: true,
  }
  draftArmors.value.push(item)
  selectedArmorId.value = item.id
}

function addTalent(): void {
  const item: TalentDefinition = {
    id: customId('custom-talent'), name: t('Новый талант', 'New talent'), strength: 0, hp: 0, speed: 0, isCustom: true,
  }
  draftTalents.value.push(item)
  selectedTalentId.value = item.id
}

function removeSelected(kind: 'weapon' | 'armor' | 'talent'): void {
  if (kind === 'weapon' && selectedWeapon.value?.isCustom) {
    draftWeapons.value = draftWeapons.value.filter(item => item.id !== selectedWeaponId.value)
    selectedWeaponId.value = draftWeapons.value[0]?.id ?? ''
  }
  if (kind === 'armor' && selectedArmor.value?.isCustom) {
    draftArmors.value = draftArmors.value.filter(item => item.id !== selectedArmorId.value)
    selectedArmorId.value = draftArmors.value[0]?.id ?? ''
  }
  if (kind === 'talent' && selectedTalent.value?.isCustom) {
    draftTalents.value = draftTalents.value.filter(item => item.id !== selectedTalentId.value)
    selectedTalentId.value = draftTalents.value[0]?.id ?? ''
  }
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    emit('cancel')
    return
  }
  trapTabKey(event)
}

function save(): void {
  emit('save', {
    balance: cloneValue(draftBalance.value),
    weapons: cloneValue(draftWeapons.value),
    armors: cloneValue(draftArmors.value),
    talents: cloneValue(draftTalents.value),
  })
}

const armorSlots: Array<{ value: ArmorSlot; label: string }> = [
  { value: 'helmet', label: 'Шлем' },
  { value: 'mail', label: 'Кольчуга' },
  { value: 'padding', label: 'Поддоспешник' },
  { value: 'plate', label: 'Латы' },
]
</script>

<template>
  <Teleport to="body">
    <Transition name="fc-modal" appear>
      <div ref="overlayRef" class="balance-overlay" @click.self="emit('cancel')">
        <section ref="dialogRef" class="balance-dialog" role="dialog" aria-modal="true" aria-labelledby="balance-title" tabindex="-1" @keydown="onKeydown">
          <header class="dialog-header">
            <div><span class="eyebrow">FIGHT LAB</span><h2 id="balance-title"><Settings2 :size="20" /> {{ t('Баланс и каталог', 'Balance & catalog') }}</h2></div>
            <button class="icon-button" type="button" :aria-label="t('Закрыть', 'Close')" @click="emit('cancel')"><X :size="19" /></button>
          </header>

          <nav class="balance-tabs" :aria-label="t('Раздел баланса', 'Balance section')">
            <button v-for="tab in (['global', 'weapons', 'armors', 'talents'] as const)" :key="tab" type="button" :class="{ active: activeTab === tab }" @click="activeTab = tab">
              {{ tab === 'global' ? t('Механики', 'Mechanics') : tab === 'weapons' ? t('Оружие', 'Weapons') : tab === 'armors' ? t('Броня', 'Armor') : t('Таланты', 'Talents') }}
            </button>
          </nav>

          <div class="dialog-scroll">
            <div v-if="activeTab === 'global'" class="settings-grid">
              <label><span>{{ t('Базовый урон', 'Base damage') }}</span><input v-model.number="draftBalance.baseDamage" min="0.1" step="0.1" type="number" /></label>
              <label><span>{{ t('Начальная усталость', 'Initial fatigue') }}</span><input v-model.number="draftBalance.initialFatigue" min="0.01" step="0.1" type="number" /></label>
              <label><span>{{ t('Тяжесть → усталость', 'Heaviness → fatigue') }}</span><input v-model.number="draftBalance.armorFatigueFactor" min="0" step="0.01" type="number" /></label>
              <label><span>{{ t('Тяжесть → скорость', 'Heaviness → speed') }}</span><input v-model.number="draftBalance.heavinessSpeedPenalty" min="0" step="0.01" type="number" /></label>
              <label><span>{{ t('Минимальная скорость', 'Minimum speed') }}</span><input v-model.number="draftBalance.minMoveSpeed" min="0.01" step="0.05" type="number" /></label>
              <label><span>{{ t('Замедление после урона', 'Post-damage delay') }}</span><input v-model.number="draftBalance.postDamageDelayMultiplier" min="1" step="0.1" type="number" /></label>
              <label><span>{{ t('Оглушение, сек', 'Stun, seconds') }}</span><input v-model.number="draftBalance.stunSeconds" min="0" step="0.1" type="number" /></label>
              <label><span>{{ t('Шанс дробящего', 'Crushing chance') }}</span><input v-model.number="draftBalance.crushKnockoutChance" max="1" min="0" step="0.05" type="number" /></label>
              <label><span>{{ t('Шанс дизарма', 'Disarm chance') }}</span><input v-model.number="draftBalance.disarmChance" max="1" min="0" step="0.05" type="number" /></label>
              <label><span>{{ t('Урон кровотечения', 'Bleed damage') }}</span><input v-model.number="draftBalance.bleedDamage" min="0" step="0.1" type="number" /></label>
              <label><span>{{ t('Период крови, сек', 'Bleed interval') }}</span><input v-model.number="draftBalance.bleedIntervalSeconds" min="0.1" step="0.1" type="number" /></label>
              <label><span>{{ t('Мин. потеря прочности', 'Min durability loss') }}</span><input v-model.number="draftBalance.durabilityLossMin" min="0" step="0.1" type="number" /></label>
              <label><span>{{ t('Макс. потеря прочности', 'Max durability loss') }}</span><input v-model.number="draftBalance.durabilityLossMax" min="0" step="0.1" type="number" /></label>
              <label><span>{{ t('Лимит боя, сек', 'Collision timeout') }}</span><input v-model.number="draftBalance.maxCollisionSeconds" min="10" step="10" type="number" /></label>
              <label><span>{{ t('Лимит событий', 'Event limit') }}</span><input v-model.number="draftBalance.maxCollisionEvents" min="20" step="10" type="number" /></label>
            </div>

            <div v-else-if="activeTab === 'weapons'" class="catalog-editor">
              <div class="catalog-toolbar"><select v-model="selectedWeaponId"><option v-for="item in draftWeapons" :key="item.id" :value="item.id">{{ item.name }}</option></select><button type="button" @click="addWeapon"><Plus :size="16" /> {{ t('Добавить', 'Add') }}</button><button :disabled="!selectedWeapon?.isCustom" type="button" @click="removeSelected('weapon')"><Trash2 :size="16" /></button></div>
              <template v-if="selectedWeapon">
                <div class="settings-grid"><label><span>{{ t('Название', 'Name') }}</span><input v-model="selectedWeapon.name" type="text" /></label><label><span>{{ t('Категория', 'Category') }}</span><input v-model="selectedWeapon.category" type="text" /></label></div>
                <h3>{{ t('Техники', 'Techniques') }}</h3><div class="damage-grid"><label v-for="type in DAMAGE_TYPES" :key="type"><span>{{ type }}</span><input v-model.number="selectedWeapon.attacks[type]" min="0" step="1" type="number" /></label></div>
                <div class="settings-grid compact-grid">
                  <label><span>{{ t('Защита', 'Defense') }}</span><input v-model.number="selectedWeapon.defense" min="0" step="1" type="number" /></label><label><span>Дизарм</span><input v-model.number="selectedWeapon.disarm" min="0" step="1" type="number" /></label><label><span>Анти-щит</span><input v-model.number="selectedWeapon.antiShield" min="0" step="1" type="number" /></label><label><span>{{ t('Ударов/сек', 'Hits/sec') }}</span><input v-model.number="selectedWeapon.speed" min="0.1" step="0.1" type="number" /></label><label><span>{{ t('Дальность min', 'Range min') }}</span><input v-model.number="selectedWeapon.rangeMin" min="0" step="0.1" type="number" /></label><label><span>{{ t('Дальность max', 'Range max') }}</span><input v-model.number="selectedWeapon.rangeMax" min="0" step="0.1" type="number" /></label><label><span>{{ t('Прочность', 'Durability') }}</span><input v-model.number="selectedWeapon.durability" min="0.1" step="0.1" type="number" /></label><label><span>{{ t('Усталость/удар', 'Fatigue/hit') }}</span><input v-model.number="selectedWeapon.fatigue" min="0" step="0.01" type="number" /></label><label><span>{{ t('Руки min', 'Hands min') }}</span><input v-model.number="selectedWeapon.handsMin" min="1" step="1" type="number" /></label><label><span>{{ t('Руки max', 'Hands max') }}</span><input v-model.number="selectedWeapon.handsMax" min="1" step="1" type="number" /></label>
                </div>
              </template>
            </div>

            <div v-else-if="activeTab === 'armors'" class="catalog-editor">
              <div class="catalog-toolbar"><select v-model="selectedArmorId"><option v-for="item in draftArmors" :key="item.id" :value="item.id">{{ item.name }}</option></select><button type="button" @click="addArmor"><Plus :size="16" /> {{ t('Добавить', 'Add') }}</button><button :disabled="!selectedArmor?.isCustom" type="button" @click="removeSelected('armor')"><Trash2 :size="16" /></button></div>
              <template v-if="selectedArmor">
                <div class="settings-grid"><label><span>{{ t('Название', 'Name') }}</span><input v-model="selectedArmor.name" type="text" /></label><label><span>{{ t('Слот', 'Slot') }}</span><select v-model="selectedArmor.slot"><option v-for="slot in armorSlots" :key="slot.value" :value="slot.value">{{ slot.label }}</option></select></label></div>
                <h3>{{ t('Резисты', 'Resists') }}</h3><div class="damage-grid"><label v-for="type in DAMAGE_TYPES" :key="type"><span>{{ type }}</span><input v-model.number="selectedArmor.resists[type]" min="0" step="1" type="number" /></label></div>
                <div class="settings-grid compact-grid"><label><span>ХП</span><input v-model.number="selectedArmor.hp" min="0" step="1" type="number" /></label><label><span>{{ t('Вес, кг', 'Weight, kg') }}</span><input v-model.number="selectedArmor.weight" min="0" step="0.1" type="number" /></label><label><span>{{ t('Эргономика, %', 'Ergonomics, %') }}</span><input v-model.number="selectedArmor.ergonomics" min="0" step="1" type="number" /></label><label><span>{{ t('Тяжесть', 'Heaviness') }}</span><input v-model.number="selectedArmor.heaviness" min="0" step="0.1" type="number" /></label></div>
              </template>
            </div>

            <div v-else class="catalog-editor">
              <div class="catalog-toolbar"><select v-model="selectedTalentId"><option v-for="item in draftTalents" :key="item.id" :value="item.id">{{ item.name }}</option></select><button type="button" @click="addTalent"><Plus :size="16" /> {{ t('Добавить', 'Add') }}</button><button :disabled="!selectedTalent?.isCustom" type="button" @click="removeSelected('talent')"><Trash2 :size="16" /></button></div>
              <div v-if="selectedTalent" class="settings-grid"><label><span>{{ t('Название', 'Name') }}</span><input v-model="selectedTalent.name" type="text" /></label><label><span>{{ t('Сила', 'Strength') }}</span><input v-model.number="selectedTalent.strength" step="1" type="number" /></label><label><span>ХП</span><input v-model.number="selectedTalent.hp" step="1" type="number" /></label><label><span>{{ t('Скорость', 'Speed') }}</span><input v-model.number="selectedTalent.speed" step="0.1" type="number" /></label></div>
            </div>
          </div>

          <footer class="dialog-actions"><button class="fc-button" type="button" @click="emit('reset')"><RotateCcw :size="16" /> {{ t('Сбросить базу', 'Reset defaults') }}</button><span class="action-spacer" /><button class="fc-button" type="button" @click="emit('cancel')">{{ t('Отмена', 'Cancel') }}</button><button class="fc-button fc-button--primary" type="button" @click="save"><Save :size="16" /> {{ t('Применить', 'Apply') }}</button></footer>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.balance-overlay { position: fixed; z-index: 4100; inset: 0; display: grid; place-items: center; padding: 16px; background: rgba(5, 5, 8, 0.84); backdrop-filter: blur(9px); }
.balance-dialog { display: flex; flex-direction: column; width: min(980px, 100%); max-height: 94vh; overflow: hidden; border: 1px solid var(--border-color); border-radius: 18px; outline: none; background: linear-gradient(155deg, var(--bg-card), var(--bg-secondary)); box-shadow: 0 25px 90px rgba(0, 0, 0, 0.68); }
.dialog-header, .dialog-actions { display: flex; align-items: center; gap: 10px; padding: 15px 18px; border-bottom: 1px solid var(--border-subtle); }
.dialog-header { justify-content: space-between; }.dialog-actions { border-top: 1px solid var(--border-subtle); border-bottom: 0; }.action-spacer { flex: 1; }
.eyebrow { color: var(--accent-gold); font: 800 0.67rem/1 var(--font-mono); letter-spacing: .1em; } h2 { display: flex; align-items: center; gap: 8px; margin: 5px 0 0; font-size: 1.2rem; }
.icon-button, .catalog-toolbar button { display: inline-grid; min-width: 38px; min-height: 38px; place-items: center; border: 1px solid var(--border-color); border-radius: 9px; color: var(--text-primary); background: var(--bg-inset); cursor: pointer; }.icon-button:hover, .catalog-toolbar button:hover { border-color: var(--accent-gold-dim); }.catalog-toolbar button:disabled { opacity: .35; cursor: not-allowed; }
.balance-tabs { display: grid; grid-template-columns: repeat(4, 1fr); gap: 4px; padding: 8px 12px; border-bottom: 1px solid var(--border-subtle); background: var(--bg-inset); }.balance-tabs button { min-height: 38px; border: 0; border-radius: 8px; color: var(--text-muted); background: transparent; cursor: pointer; font-weight: 850; }.balance-tabs button.active { color: var(--text-primary); background: var(--bg-card-hover); box-shadow: inset 0 -2px var(--accent-gold); }
.dialog-scroll { overflow-y: auto; padding: 17px 18px 24px; }.settings-grid, .damage-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 11px; }.compact-grid { margin-top: 15px; }.damage-grid { grid-template-columns: repeat(5, minmax(0, 1fr)); }.catalog-toolbar { display: grid; grid-template-columns: 1fr auto auto; gap: 8px; margin-bottom: 17px; }.catalog-toolbar button { display: inline-flex; align-items: center; gap: 5px; padding: 0 10px; }
h3 { margin: 17px 0 9px; color: var(--text-primary); font-size: .88rem; } label span { display: block; margin-bottom: 5px; color: var(--text-muted); font-size: .72rem; font-weight: 750; } input, select { width: 100%; min-height: 40px; padding: 8px 10px; border: 1px solid var(--border-color); border-radius: 9px; color: var(--text-primary); background: var(--bg-inset); font: inherit; }
.fc-button { display: inline-flex; min-height: 40px; align-items: center; justify-content: center; gap: 7px; padding: 8px 13px; border: 1px solid var(--border-color); border-radius: 9px; color: var(--text-primary); background: var(--bg-inset); cursor: pointer; font-weight: 800; }.fc-button--primary { border-color: color-mix(in srgb, var(--accent-green) 55%, var(--border-color)); background: color-mix(in srgb, var(--accent-green) 18%, var(--bg-card)); }
button:focus-visible, input:focus-visible, select:focus-visible { outline: 2px solid var(--accent-blue); outline-offset: 2px; }.fc-modal-enter-active, .fc-modal-leave-active { transition: opacity .18s ease; }.fc-modal-enter-from, .fc-modal-leave-to { opacity: 0; }
@media (max-width: 720px) { .balance-overlay { padding: 6px; }.balance-dialog { max-height: 98vh; border-radius: 12px; }.settings-grid, .damage-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }.balance-tabs { grid-template-columns: repeat(2, 1fr); }.dialog-actions { flex-wrap: wrap; }.action-spacer { display: none; } }
</style>
