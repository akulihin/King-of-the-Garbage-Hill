<script setup lang="ts">
import { computed, reactive } from 'vue'
import { Save, Shield, Swords, UserRound, X } from 'lucide-vue-next'
import { useFocusTrapDialog } from 'src/composables/useFocusTrapDialog'
import { currentLocale } from 'src/i18n'
import { cloneValue } from 'src/features/fight-calculator/catalog'
import type {
  ArmorDefinition,
  TalentDefinition,
  UnitConfig,
  WeaponDefinition,
} from 'src/features/fight-calculator/types'

const props = defineProps<{
  unit: UnitConfig
  slotNumber: number
  teamNumber: 1 | 2
  weapons: WeaponDefinition[]
  armors: ArmorDefinition[]
  talents: TalentDefinition[]
}>()

const emit = defineEmits<{
  save: [unit: UnitConfig]
  cancel: []
}>()

const draft = reactive<UnitConfig>(cloneValue(props.unit))
const { overlayRef, dialogRef, trapTabKey } = useFocusTrapDialog()
const helmets = computed(() => props.armors.filter(item => item.slot === 'helmet'))
const mails = computed(() => props.armors.filter(item => item.slot === 'mail'))
const paddings = computed(() => props.armors.filter(item => item.slot === 'padding'))
const plates = computed(() => props.armors.filter(item => item.slot === 'plate'))

function t(ru: string, en: string): string {
  return currentLocale.value === 'ru' ? ru : en
}

function toggleTalent(id: string): void {
  const index = draft.talentIds.indexOf(id)
  if (index >= 0) draft.talentIds.splice(index, 1)
  else draft.talentIds.push(id)
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
  draft.name = draft.name.trim() || t(`Боец ${props.slotNumber}`, `Fighter ${props.slotNumber}`)
  draft.baseHp = Math.max(1, Number(draft.baseHp) || 1)
  draft.baseMoveSpeed = Math.max(0.1, Number(draft.baseMoveSpeed) || 0.1)
  emit('save', cloneValue(draft))
}
</script>

<template>
  <Teleport to="body">
    <Transition name="fc-modal" appear>
      <div ref="overlayRef" class="editor-overlay" @click.self="emit('cancel')">
        <form
          ref="dialogRef"
          class="unit-editor"
          role="dialog"
          aria-modal="true"
          aria-labelledby="unit-editor-title"
          tabindex="-1"
          @submit.prevent="save"
          @keydown="onKeydown"
        >
          <header class="editor-header">
            <div>
              <span class="eyebrow">{{ t(`Команда ${teamNumber} · слот ${slotNumber}`, `Team ${teamNumber} · slot ${slotNumber}`) }}</span>
              <h2 id="unit-editor-title"><UserRound :size="20" /> {{ t('Редактор юнита', 'Unit editor') }}</h2>
            </div>
            <button class="icon-button" type="button" :aria-label="t('Закрыть', 'Close')" @click="emit('cancel')"><X :size="19" /></button>
          </header>

          <div class="editor-scroll">
            <label class="presence-toggle">
              <input v-model="draft.enabled" type="checkbox" />
              <span>{{ t('Юнит присутствует в бою', 'Unit participates in the fight') }}</span>
            </label>

            <fieldset :disabled="!draft.enabled">
              <legend><UserRound :size="16" /> {{ t('Основное', 'Core') }}</legend>
              <div class="form-grid form-grid--core">
                <label class="wide-field">
                  <span>{{ t('Имя', 'Name') }}</span>
                  <input v-model="draft.name" maxlength="40" type="text" />
                </label>
                <label>
                  <span>{{ t('Базовое ХП', 'Base HP') }}</span>
                  <input v-model.number="draft.baseHp" min="1" step="1" type="number" />
                </label>
                <label>
                  <span>{{ t('Скорость, м/с', 'Speed, m/s') }}</span>
                  <input v-model.number="draft.baseMoveSpeed" min="0.1" step="0.1" type="number" />
                </label>
              </div>
            </fieldset>

            <fieldset :disabled="!draft.enabled">
              <legend><Swords :size="16" /> {{ t('Оружие', 'Weapons') }}</legend>
              <div class="form-grid">
                <label>
                  <span>{{ t('Основное', 'Primary') }}</span>
                  <select v-model="draft.primaryWeaponId">
                    <option value="">— {{ t('нет', 'none') }} —</option>
                    <option v-for="item in weapons" :key="item.id" :value="item.id">{{ item.name }}</option>
                  </select>
                </label>
                <label>
                  <span>{{ t('Вторичное', 'Secondary') }}</span>
                  <select v-model="draft.secondaryWeaponId">
                    <option value="">— {{ t('нет', 'none') }} —</option>
                    <option v-for="item in weapons" :key="item.id" :value="item.id">{{ item.name }}</option>
                  </select>
                </label>
                <label>
                  <span>{{ t('Кинжал (не ломается)', 'Dagger (unbreakable)') }}</span>
                  <select v-model="draft.daggerWeaponId">
                    <option value="">— {{ t('нет', 'none') }} —</option>
                    <option v-for="item in weapons" :key="item.id" :value="item.id">{{ item.name }}</option>
                  </select>
                </label>
                <label class="mastery-toggle">
                  <input v-model="draft.mastery" type="checkbox" />
                  <span>{{ t('Максимальное мастерство', 'Maximum mastery') }}</span>
                </label>
              </div>
            </fieldset>

            <fieldset :disabled="!draft.enabled">
              <legend><Shield :size="16" /> {{ t('Броня', 'Armor') }}</legend>
              <div class="form-grid">
                <label>
                  <span>{{ t('Шлем', 'Helmet') }}</span>
                  <select v-model="draft.helmetId"><option value="">— {{ t('нет', 'none') }} —</option><option v-for="item in helmets" :key="item.id" :value="item.id">{{ item.name }}</option></select>
                </label>
                <label>
                  <span>{{ t('Кольчуга', 'Mail') }}</span>
                  <select v-model="draft.mailId"><option value="">— {{ t('нет', 'none') }} —</option><option v-for="item in mails" :key="item.id" :value="item.id">{{ item.name }}</option></select>
                </label>
                <label>
                  <span>{{ t('Поддоспешник', 'Padding') }}</span>
                  <select v-model="draft.paddingId"><option value="">— {{ t('нет', 'none') }} —</option><option v-for="item in paddings" :key="item.id" :value="item.id">{{ item.name }}</option></select>
                </label>
                <label>
                  <span>{{ t('Латы', 'Plate') }}</span>
                  <select v-model="draft.plateId">
                    <option value="">— {{ plates.length ? t('нет', 'none') : t('нет в исходных данных', 'not in source data') }} —</option>
                    <option v-for="item in plates" :key="item.id" :value="item.id">{{ item.name }}</option>
                  </select>
                </label>
              </div>
            </fieldset>

            <fieldset :disabled="!draft.enabled">
              <legend>{{ t('Таланты', 'Talents') }}</legend>
              <div class="talent-grid">
                <label v-for="talent in talents" :key="talent.id" class="talent-option">
                  <input :checked="draft.talentIds.includes(talent.id)" type="checkbox" @change="toggleTalent(talent.id)" />
                  <span><strong>{{ talent.name }}</strong><small>Сила +{{ talent.strength }} · ХП +{{ talent.hp }} · Скорость +{{ talent.speed }}</small></span>
                </label>
              </div>
            </fieldset>
          </div>

          <footer class="editor-actions">
            <button class="fc-button fc-button--ghost" type="button" @click="emit('cancel')">{{ t('Отмена', 'Cancel') }}</button>
            <button class="fc-button fc-button--primary" type="submit"><Save :size="17" /> {{ t('Сохранить юнита', 'Save unit') }}</button>
          </footer>
        </form>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.editor-overlay { position: fixed; z-index: 4100; inset: 0; display: grid; place-items: center; padding: 18px; background: rgba(5, 5, 8, 0.82); backdrop-filter: blur(8px); }
.unit-editor { display: flex; flex-direction: column; width: min(860px, 100%); max-height: min(900px, 94vh); overflow: hidden; border: 1px solid var(--border-color); border-radius: 18px; outline: none; background: linear-gradient(155deg, var(--bg-card), var(--bg-secondary)); box-shadow: 0 24px 90px rgba(0, 0, 0, 0.65); }
.editor-header, .editor-actions { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 16px 18px; border-bottom: 1px solid var(--border-subtle); }
.editor-actions { justify-content: flex-end; border-top: 1px solid var(--border-subtle); border-bottom: 0; }
.eyebrow { color: var(--accent-gold); font: 800 0.68rem/1.2 var(--font-mono); letter-spacing: 0.08em; text-transform: uppercase; }
h2 { display: flex; align-items: center; gap: 8px; margin: 3px 0 0; color: var(--text-primary); font-size: 1.18rem; }
.icon-button { display: grid; width: 38px; height: 38px; place-items: center; border: 1px solid var(--border-subtle); border-radius: 10px; color: var(--text-muted); background: var(--bg-inset); cursor: pointer; }
.icon-button:hover { color: var(--text-primary); border-color: var(--accent-gold-dim); }
.editor-scroll { overflow-y: auto; padding: 16px 18px 22px; }
.presence-toggle, .mastery-toggle { display: flex; align-items: center; gap: 10px; }
.presence-toggle { margin-bottom: 14px; padding: 12px 14px; border: 1px solid color-mix(in srgb, var(--accent-green) 35%, var(--border-subtle)); border-radius: 12px; color: var(--text-primary); background: color-mix(in srgb, var(--accent-green) 7%, var(--bg-inset)); font-weight: 800; }
fieldset { margin: 0 0 14px; padding: 13px; border: 1px solid var(--border-subtle); border-radius: 13px; }
fieldset:disabled { opacity: 0.45; }
legend { display: inline-flex; align-items: center; gap: 6px; padding: 0 7px; color: var(--text-primary); font-weight: 850; }
.form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 11px; }
.form-grid--core { grid-template-columns: 2fr 1fr 1fr; }
label > span:not(.eyebrow) { display: block; margin-bottom: 5px; color: var(--text-muted); font-size: 0.74rem; font-weight: 750; }
input[type='text'], input[type='number'], select { width: 100%; min-height: 40px; padding: 8px 10px; border: 1px solid var(--border-color); border-radius: 9px; color: var(--text-primary); background: var(--bg-inset); font: inherit; }
input:focus-visible, select:focus-visible, button:focus-visible { outline: 2px solid var(--accent-blue); outline-offset: 2px; }
input[type='checkbox'] { width: 18px; height: 18px; accent-color: var(--accent-green); }
.mastery-toggle { min-height: 40px; align-self: end; padding: 8px 10px; border-radius: 9px; background: var(--bg-inset); }
.mastery-toggle span { margin: 0 !important; }
.talent-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; }
.talent-option { display: flex; align-items: center; gap: 9px; padding: 9px; border: 1px solid var(--border-subtle); border-radius: 9px; background: var(--bg-inset); }
.talent-option span { margin: 0 !important; color: var(--text-primary) !important; }
.talent-option small { display: block; margin-top: 2px; color: var(--text-muted); font-size: 0.68rem; }
.fc-button { display: inline-flex; min-height: 40px; align-items: center; justify-content: center; gap: 7px; padding: 8px 14px; border: 1px solid var(--border-color); border-radius: 10px; color: var(--text-primary); background: var(--bg-inset); cursor: pointer; font-weight: 800; }
.fc-button--primary { border-color: color-mix(in srgb, var(--accent-green) 55%, var(--border-color)); background: color-mix(in srgb, var(--accent-green) 18%, var(--bg-card)); }
.fc-button:hover { filter: brightness(1.12); }
.fc-modal-enter-active, .fc-modal-leave-active { transition: opacity 0.18s ease; }
.fc-modal-enter-from, .fc-modal-leave-to { opacity: 0; }
@media (max-width: 700px) { .form-grid, .form-grid--core, .talent-grid { grid-template-columns: 1fr; } .editor-overlay { padding: 7px; } .unit-editor { max-height: 98vh; border-radius: 12px; } }
</style>
