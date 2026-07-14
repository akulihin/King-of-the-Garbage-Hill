<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  ArrowLeft, Dices, FlaskConical, Plus, Play, RotateCcw, Save, Settings2,
  ShieldCheck, Swords, Trash2, UsersRound,
} from 'lucide-vue-next'
import BalanceDialog from 'src/components/fight-calculator/BalanceDialog.vue'
import CollisionDetailsDialog from 'src/components/fight-calculator/CollisionDetailsDialog.vue'
import UnitEditorDialog from 'src/components/fight-calculator/UnitEditorDialog.vue'
import { currentLocale } from 'src/i18n'
import {
  BASE_ARMORS, BASE_TALENTS, BASE_WEAPONS, DEFAULT_BALANCE, cloneValue, createDefaultProfile,
} from 'src/features/fight-calculator/catalog'
import { getUnitPreview, runFight } from 'src/features/fight-calculator/engine'
import type {
  ArmorDefinition, BattleResult, CalculatorProfile, CollisionSummary, FightBalance,
  TalentDefinition, TeamId, UnitConfig, WeaponDefinition,
} from 'src/features/fight-calculator/types'

const STORAGE_KEY = 'kotgh_fight_calculator_profiles_v1'
const profiles = ref<CalculatorProfile[]>([])
const activeProfileId = ref('')
const profile = ref(createDefaultProfile())
const battleResult = ref<BattleResult | null>(null)
const seed = ref(Math.abs(Date.now() % 2147483647))
const editorTarget = ref<{ team: TeamId; index: number } | null>(null)
const showBalance = ref(false)
const selectedCollision = ref<CollisionSummary | null>(null)
const statusMessage = ref('')

const editingUnit = computed(() => {
  if (!editorTarget.value) return null
  return editorTarget.value.team === 1
    ? profile.value.team1[editorTarget.value.index]
    : profile.value.team2[editorTarget.value.index]
})
const activeTeam1 = computed(() => profile.value.team1.filter(unit => unit.enabled).length)
const activeTeam2 = computed(() => profile.value.team2.filter(unit => unit.enabled).length)

function t(ru: string, en: string): string {
  return currentLocale.value === 'ru' ? ru : en
}

function flash(message: string): void {
  statusMessage.value = message
  window.setTimeout(() => {
    if (statusMessage.value === message) statusMessage.value = ''
  }, 2400)
}

function persistProfiles(): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(profiles.value))
}

function loadProfiles(): void {
  const raw = localStorage.getItem(STORAGE_KEY)
  if (raw) {
    try {
      const saved = JSON.parse(raw) as CalculatorProfile[]
      if (Array.isArray(saved) && saved.length > 0) profiles.value = saved
    }
    catch {
      localStorage.removeItem(STORAGE_KEY)
    }
  }
  if (profiles.value.length === 0) profiles.value = [cloneValue(profile.value)]
  activeProfileId.value = profiles.value[0].id
  profile.value = cloneValue(profiles.value[0])
  persistProfiles()
}

onMounted(loadProfiles)

function loadProfile(): void {
  const saved = profiles.value.find(item => item.id === activeProfileId.value)
  if (!saved) return
  profile.value = cloneValue(saved)
  battleResult.value = null
}

function saveProfile(): void {
  profile.value.name = profile.value.name.trim() || t('Профиль боя', 'Fight profile')
  const index = profiles.value.findIndex(item => item.id === profile.value.id)
  if (index >= 0) profiles.value[index] = cloneValue(profile.value)
  else profiles.value.push(cloneValue(profile.value))
  activeProfileId.value = profile.value.id
  persistProfiles()
  flash(t('Профиль сохранён локально', 'Profile saved locally'))
}

function newProfile(): void {
  const created = createDefaultProfile(t('Новый профиль', 'New profile'))
  profiles.value.push(cloneValue(created))
  profile.value = created
  activeProfileId.value = created.id
  battleResult.value = null
  persistProfiles()
}

function deleteProfile(): void {
  profiles.value = profiles.value.filter(item => item.id !== profile.value.id)
  if (profiles.value.length === 0) profiles.value.push(createDefaultProfile())
  activeProfileId.value = profiles.value[0].id
  profile.value = cloneValue(profiles.value[0])
  battleResult.value = null
  persistProfiles()
}

function resetTeams(): void {
  const reset = createDefaultProfile(profile.value.name)
  reset.id = profile.value.id
  profile.value.team1 = reset.team1
  profile.value.team2 = reset.team2
  battleResult.value = null
}

function resetBalanceCatalog(): void {
  profile.value.balance = cloneValue(DEFAULT_BALANCE)
  profile.value.weapons = cloneValue(BASE_WEAPONS)
  profile.value.armors = cloneValue(BASE_ARMORS)
  profile.value.talents = cloneValue(BASE_TALENTS)
  showBalance.value = false
  battleResult.value = null
  flash(t('Баланс возвращён к данным таблицы', 'Balance reset to spreadsheet data'))
}

function applyBalance(value: {
  balance: FightBalance
  weapons: WeaponDefinition[]
  armors: ArmorDefinition[]
  talents: TalentDefinition[]
}): void {
  profile.value.balance = value.balance
  profile.value.weapons = value.weapons
  profile.value.armors = value.armors
  profile.value.talents = value.talents
  showBalance.value = false
  battleResult.value = null
}

function openEditor(team: TeamId, index: number): void {
  editorTarget.value = { team, index }
}

function saveUnit(unit: UnitConfig): void {
  if (!editorTarget.value) return
  const collection = editorTarget.value.team === 1 ? profile.value.team1 : profile.value.team2
  collection[editorTarget.value.index] = unit
  editorTarget.value = null
  battleResult.value = null
}

function unitPreview(unit: UnitConfig, team: TeamId) {
  return getUnitPreview(unit, team, profile.value)
}

function rollSeed(): void {
  const values = new Uint32Array(1)
  crypto.getRandomValues(values)
  seed.value = values[0] || 1
}

function conductFight(): void {
  battleResult.value = runFight(profile.value, seed.value)
  window.requestAnimationFrame(() => document.querySelector('.battle-result')?.scrollIntoView({ behavior: 'smooth', block: 'start' }))
}

function phaseLabel(phase: CollisionSummary['phase']): string {
  if (phase === 'mirror') return t('Зеркальные слоты', 'Mirrored slots')
  if (phase === 'fallback') return t('Свободные соперники', 'Free opponents')
  return t('Бой выживших', 'Survivor fight')
}

function strikeCount(collision: CollisionSummary): number {
  return collision.steps.filter(step => step.kind === 'block' || step.kind === 'damage').length
}

function resultSummary(): string {
  if (!battleResult.value) return ''
  if (!battleResult.value.winnerTeam) return t('Обе команды выбыли — ничья.', 'Both teams were eliminated — draw.')
  return t(
    `Команда ${battleResult.value.winnerTeam} победила. Выживших: ${battleResult.value.survivors.length}.`,
    `Team ${battleResult.value.winnerTeam} wins. Survivors: ${battleResult.value.survivors.length}.`,
  )
}
</script>

<template>
  <div class="fight-calculator-page">
    <RouterLink class="back-link" to="/games"><ArrowLeft :size="16" /> {{ t('Назад в лобби', 'Back to Lobby') }}</RouterLink>

    <header class="calculator-hero">
      <div>
        <span class="hero-kicker"><FlaskConical :size="15" /> KOTGH FIGHT LAB</span>
        <h1>{{ t('Калькулятор боя', 'Fight calculator') }}</h1>
        <p>{{ t('Соберите две команды, настройте экипировку и получите воспроизводимый поэтапный разбор каждого столкновения.', 'Build two teams, tune their equipment, and get a reproducible step-by-step breakdown of every collision.') }}</p>
      </div>
      <div class="hero-stats"><span><UsersRound :size="16" /> {{ activeTeam1 }} : {{ activeTeam2 }}</span><span><Swords :size="16" /> {{ profile.weapons.length }}</span><span><ShieldCheck :size="16" /> {{ profile.armors.length }}</span></div>
    </header>

    <section class="profile-toolbar" aria-label="Fight calculator profiles">
      <label class="profile-select"><span>{{ t('Профиль', 'Profile') }}</span><select v-model="activeProfileId" @change="loadProfile"><option v-for="item in profiles" :key="item.id" :value="item.id">{{ item.name }}</option></select></label>
      <label class="profile-name"><span>{{ t('Название', 'Name') }}</span><input v-model="profile.name" maxlength="48" type="text" /></label>
      <button class="tool-button" type="button" :title="t('Сохранить профиль', 'Save profile')" @click="saveProfile"><Save :size="17" /><span>{{ t('Сохранить', 'Save') }}</span></button>
      <button class="tool-button" type="button" :title="t('Новый профиль', 'New profile')" @click="newProfile"><Plus :size="17" /><span>{{ t('Новый', 'New') }}</span></button>
      <button class="tool-button tool-button--danger" type="button" :title="t('Удалить профиль', 'Delete profile')" @click="deleteProfile"><Trash2 :size="17" /></button>
      <button class="tool-button" type="button" @click="showBalance = true"><Settings2 :size="17" /><span>{{ t('Баланс', 'Balance') }}</span></button>
      <Transition name="status"><span v-if="statusMessage" class="status-message">{{ statusMessage }}</span></Transition>
    </section>

    <section class="teams-board">
      <article v-for="teamNumber in ([1, 2] as const)" :key="teamNumber" class="team-panel" :class="`team-panel--${teamNumber}`">
        <header><div><span>{{ t('КОМАНДА', 'TEAM') }}</span><h2>{{ teamNumber }}</h2></div><small>{{ (teamNumber === 1 ? activeTeam1 : activeTeam2) }} / 6 {{ t('юнитов', 'units') }}</small></header>
        <div class="unit-slots">
          <button
            v-for="(unit, index) in (teamNumber === 1 ? profile.team1 : profile.team2)"
            :key="unit.id"
            class="unit-slot"
            :class="{ 'unit-slot--active': unit.enabled }"
            type="button"
            @click="openEditor(teamNumber, index)"
          >
            <span class="slot-number">{{ index + 1 }}</span>
            <template v-if="unit.enabled">
              <strong>{{ unit.name }}</strong>
              <span class="loadout-name">{{ unitPreview(unit, teamNumber).weaponName ?? t('Без оружия', 'Unarmed') }}</span>
              <div class="unit-metrics"><span>HP {{ unitPreview(unit, teamNumber).hp }}</span><span>{{ unitPreview(unit, teamNumber).moveSpeed }} m/s</span><span>{{ unitPreview(unit, teamNumber).armorCount }} ARM</span></div>
            </template>
            <template v-else><Plus :size="21" /><strong>{{ t('Пустой слот', 'Empty slot') }}</strong><span>{{ t('Нажмите для редактирования', 'Click to edit') }}</span></template>
          </button>
        </div>
      </article>
    </section>

    <section class="fight-controls">
      <div class="seed-control"><label><span>Seed</span><input v-model.number="seed" min="1" step="1" type="number" /></label><button type="button" :title="t('Новый seed', 'New seed')" @click="rollSeed"><Dices :size="18" /></button></div>
      <button class="reset-button" type="button" @click="resetTeams"><RotateCcw :size="17" /> {{ t('Сбросить команды', 'Reset teams') }}</button>
      <button class="fight-button" type="button" :disabled="activeTeam1 === 0 || activeTeam2 === 0" @click="conductFight"><Play :size="22" fill="currentColor" /> {{ t('ПРОВЕСТИ БОЙ', 'RUN FIGHT') }}</button>
    </section>

    <section v-if="battleResult" class="battle-result" :class="battleResult.winnerTeam ? `battle-result--team-${battleResult.winnerTeam}` : 'battle-result--draw'">
      <header class="result-heading"><div><span>{{ t('РЕЗУЛЬТАТ БОЯ', 'BATTLE RESULT') }}</span><h2>{{ battleResult.winnerTeam ? t(`Победа команды ${battleResult.winnerTeam}`, `Team ${battleResult.winnerTeam} wins`) : t('Ничья', 'Draw') }}</h2><p>{{ resultSummary() }} Seed: {{ battleResult.seed }}</p></div><ShieldCheck :size="42" /></header>

      <div class="survivors-section"><h3>{{ t('Выжившие', 'Survivors') }}</h3><div v-if="battleResult.survivors.length" class="survivors-grid"><article v-for="unit in battleResult.survivors" :key="unit.unitId" :class="`survivor--team-${unit.team}`"><strong>{{ unit.name }}</strong><span>{{ t('Команда', 'Team') }} {{ unit.team }}</span><div><b>{{ unit.hp }} / {{ unit.maxHp }} HP</b><span>{{ t('Усталость', 'Fatigue') }} {{ unit.fatigue }}</span><span>{{ unit.weaponName ?? t('Без оружия', 'Unarmed') }}</span></div></article></div><p v-else class="empty-copy">{{ t('Никто не выжил.', 'No survivors.') }}</p></div>

      <div class="collisions-section"><h3>{{ t('Столкновения', 'Collisions') }} · {{ battleResult.collisions.length }}</h3><div class="collision-list"><button v-for="collision in battleResult.collisions" :key="collision.id" type="button" @click="selectedCollision = collision"><span class="collision-icon"><Swords :size="19" /></span><span class="collision-copy"><small>#{{ collision.order }} · {{ phaseLabel(collision.phase) }}</small><strong>{{ collision.team1Name }} <em>vs</em> {{ collision.team2Name }}</strong><span>{{ collision.winnerName ? t(`Победил: ${collision.winnerName}`, `Winner: ${collision.winnerName}`) : t('Без победителя', 'No winner') }} · {{ collision.duration.toFixed(2) }}s</span></span><span class="step-count">{{ strikeCount(collision) }} {{ t('ударов', 'strikes') }}</span></button></div></div>
    </section>

    <section v-else class="result-placeholder"><Swords :size="34" /><h2>{{ t('Команды готовы?', 'Teams ready?') }}</h2><p>{{ t('Нажмите «ПРОВЕСТИ БОЙ», чтобы построить цепочку зеркальных, свободных и финальных столкновений.', 'Press “RUN FIGHT” to build the mirrored, fallback, and survivor collision chain.') }}</p></section>

    <UnitEditorDialog v-if="editorTarget && editingUnit" :key="`${editorTarget.team}-${editorTarget.index}`" :unit="editingUnit" :slot-number="editorTarget.index + 1" :team-number="editorTarget.team" :weapons="profile.weapons" :armors="profile.armors" :talents="profile.talents" @save="saveUnit" @cancel="editorTarget = null" />
    <BalanceDialog v-if="showBalance" :balance="profile.balance" :weapons="profile.weapons" :armors="profile.armors" :talents="profile.talents" @save="applyBalance" @reset="resetBalanceCatalog" @cancel="showBalance = false" />
    <CollisionDetailsDialog v-if="selectedCollision" :collision="selectedCollision" @close="selectedCollision = null" />
  </div>
</template>

<style scoped>
.fight-calculator-page { width: min(1480px, 100%); margin: 0 auto; padding-bottom: 50px; }.back-link { display: inline-flex; min-height: 36px; align-items: center; gap: 6px; margin-bottom: 10px; padding: 4px 9px; border-radius: 7px; color: var(--text-muted); text-decoration: none; font-size: 11px; font-weight: 750; }.back-link:hover { color: var(--text-primary); background: rgba(255, 255, 255, .04); }
.calculator-hero { position: relative; display: flex; align-items: flex-end; justify-content: space-between; gap: 22px; overflow: hidden; padding: 25px 28px; border: 1px solid var(--glass-border); border-radius: 18px; background: radial-gradient(circle at 80% 10%, color-mix(in srgb, var(--accent-red) 14%, transparent), transparent 32%), radial-gradient(circle at 5% 80%, color-mix(in srgb, var(--accent-blue) 16%, transparent), transparent 40%), var(--glass-bg-heavy); box-shadow: var(--shadow-lg); }.hero-kicker { display: inline-flex; align-items: center; gap: 6px; color: var(--accent-gold); font: 800 .7rem/1 var(--font-mono); letter-spacing: .1em; }.calculator-hero h1 { margin: 7px 0 8px; font-size: clamp(1.8rem, 4vw, 3.15rem); line-height: 1; }.calculator-hero p { max-width: 720px; margin: 0; color: var(--text-muted); font-size: .84rem; line-height: 1.55; }.hero-stats { display: flex; gap: 8px; flex-wrap: wrap; justify-content: flex-end; }.hero-stats span { display: inline-flex; min-height: 36px; align-items: center; gap: 6px; padding: 7px 10px; border: 1px solid var(--border-subtle); border-radius: 9px; color: var(--text-secondary); background: var(--bg-inset); font: 750 .7rem/1 var(--font-mono); }
.profile-toolbar { position: relative; display: grid; grid-template-columns: minmax(150px, 230px) minmax(150px, 1fr) repeat(4, auto); gap: 8px; align-items: end; margin: 12px 0; padding: 11px; border: 1px solid var(--border-subtle); border-radius: 13px; background: var(--glass-bg); }.profile-toolbar label span { display: block; margin: 0 0 4px; color: var(--text-muted); font-size: .63rem; font-weight: 800; text-transform: uppercase; }.profile-toolbar select, .profile-toolbar input, .seed-control input { width: 100%; min-height: 38px; padding: 7px 9px; border: 1px solid var(--border-color); border-radius: 8px; color: var(--text-primary); background: var(--bg-inset); }.tool-button, .reset-button { display: inline-flex; min-height: 38px; align-items: center; justify-content: center; gap: 6px; padding: 7px 10px; border: 1px solid var(--border-color); border-radius: 8px; color: var(--text-primary); background: var(--bg-inset); cursor: pointer; font-size: .72rem; font-weight: 800; }.tool-button:hover, .reset-button:hover { border-color: var(--accent-gold-dim); }.tool-button--danger { color: var(--accent-red); }.status-message { position: absolute; right: 12px; bottom: -27px; z-index: 2; padding: 5px 8px; border-radius: 6px; color: var(--accent-green); background: var(--bg-inset); font-size: .68rem; }
.teams-board { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }.team-panel { overflow: hidden; border: 1px solid var(--border-subtle); border-radius: 15px; background: var(--glass-bg); box-shadow: var(--shadow); }.team-panel > header { display: flex; align-items: center; justify-content: space-between; padding: 13px 16px; border-bottom: 1px solid var(--border-subtle); }.team-panel--1 > header { background: linear-gradient(90deg, color-mix(in srgb, var(--accent-blue) 22%, var(--bg-card)), var(--bg-card)); }.team-panel--2 > header { background: linear-gradient(90deg, color-mix(in srgb, var(--accent-red) 20%, var(--bg-card)), var(--bg-card)); }.team-panel header div { display: flex; align-items: baseline; gap: 7px; }.team-panel header span { color: var(--text-muted); font: 850 .64rem/1 var(--font-mono); letter-spacing: .09em; }.team-panel header h2 { margin: 0; font-size: 1.45rem; }.team-panel header small { color: var(--text-muted); font: 700 .66rem/1 var(--font-mono); }.unit-slots { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; padding: 10px; }.unit-slot { position: relative; display: flex; min-height: 122px; flex-direction: column; align-items: flex-start; justify-content: center; gap: 5px; padding: 13px 13px 11px 43px; border: 1px dashed var(--border-color); border-radius: 11px; color: var(--text-muted); background: var(--bg-inset); cursor: pointer; text-align: left; transition: transform .16s ease, border-color .16s ease, background .16s ease; }.unit-slot:hover { transform: translateY(-2px); border-color: var(--accent-gold-dim); background: var(--bg-card-hover); }.unit-slot--active { border-style: solid; color: var(--text-primary); }.team-panel--1 .unit-slot--active { border-color: color-mix(in srgb, var(--accent-blue) 42%, var(--border-color)); }.team-panel--2 .unit-slot--active { border-color: color-mix(in srgb, var(--accent-red) 42%, var(--border-color)); }.slot-number { position: absolute; top: 10px; left: 10px; display: grid; width: 24px; height: 24px; place-items: center; border-radius: 7px; color: var(--text-muted); background: var(--bg-card); font: 850 .65rem/1 var(--font-mono); }.unit-slot strong { font-size: .84rem; }.unit-slot > span:not(.slot-number):not(.loadout-name) { font-size: .68rem; }.loadout-name { overflow: hidden; width: 100%; color: var(--accent-gold); font-size: .7rem; text-overflow: ellipsis; white-space: nowrap; }.unit-metrics { display: flex; gap: 5px; flex-wrap: wrap; margin-top: 5px; }.unit-metrics span { padding: 3px 5px; border-radius: 5px; color: var(--text-muted); background: var(--bg-card); font: 700 .58rem/1 var(--font-mono); }
.fight-controls { display: grid; grid-template-columns: auto 1fr auto; gap: 9px; align-items: end; margin: 12px 0 18px; }.seed-control { display: flex; align-items: end; gap: 5px; }.seed-control label span { display: block; margin-bottom: 4px; color: var(--text-muted); font: 750 .62rem/1 var(--font-mono); }.seed-control input { width: 150px; }.seed-control button { display: grid; width: 38px; height: 38px; place-items: center; border: 1px solid var(--border-color); border-radius: 8px; color: var(--text-primary); background: var(--bg-inset); cursor: pointer; }.reset-button { justify-self: start; }.fight-button { display: inline-flex; min-height: 52px; align-items: center; justify-content: center; gap: 9px; padding: 10px 24px; border: 1px solid color-mix(in srgb, var(--accent-red) 64%, var(--border-color)); border-radius: 12px; color: #fff; background: linear-gradient(135deg, color-mix(in srgb, var(--accent-red) 78%, #471010), color-mix(in srgb, var(--accent-orange) 45%, #451414)); box-shadow: 0 8px 26px color-mix(in srgb, var(--accent-red) 20%, transparent); cursor: pointer; font-weight: 950; letter-spacing: .04em; }.fight-button:hover:not(:disabled) { filter: brightness(1.12); transform: translateY(-1px); }.fight-button:disabled { opacity: .4; cursor: not-allowed; }
.battle-result, .result-placeholder { scroll-margin-top: 74px; border: 1px solid var(--border-subtle); border-radius: 16px; background: var(--glass-bg-heavy); box-shadow: var(--shadow-lg); }.result-heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 20px 22px; border-bottom: 1px solid var(--border-subtle); }.battle-result--team-1 .result-heading { background: linear-gradient(100deg, color-mix(in srgb, var(--accent-blue) 20%, transparent), transparent); }.battle-result--team-2 .result-heading { background: linear-gradient(100deg, color-mix(in srgb, var(--accent-red) 18%, transparent), transparent); }.result-heading span { color: var(--accent-gold); font: 850 .65rem/1 var(--font-mono); letter-spacing: .1em; }.result-heading h2 { margin: 5px 0 4px; font-size: 1.45rem; }.result-heading p { margin: 0; color: var(--text-muted); font-size: .75rem; }.survivors-section, .collisions-section { padding: 16px 18px; }.survivors-section { border-bottom: 1px solid var(--border-subtle); }.survivors-section h3, .collisions-section h3 { margin: 0 0 10px; font-size: .83rem; }.survivors-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 8px; }.survivors-grid article { padding: 10px; border: 1px solid var(--border-subtle); border-left: 3px solid var(--accent-blue); border-radius: 9px; background: var(--bg-inset); }.survivors-grid article.survivor--team-2 { border-left-color: var(--accent-red); }.survivors-grid article > span { display: block; margin-top: 2px; color: var(--text-muted); font-size: .64rem; }.survivors-grid article div { display: flex; gap: 6px; flex-wrap: wrap; margin-top: 7px; }.survivors-grid article div span, .survivors-grid article div b { padding: 3px 5px; border-radius: 5px; background: var(--bg-card); font: 700 .58rem/1 var(--font-mono); }.empty-copy { color: var(--text-muted); font-size: .76rem; }.collision-list { display: grid; gap: 7px; }.collision-list button { display: grid; grid-template-columns: 40px 1fr auto; gap: 10px; align-items: center; width: 100%; padding: 9px 11px; border: 1px solid var(--border-subtle); border-radius: 9px; color: var(--text-primary); background: var(--bg-inset); cursor: pointer; text-align: left; }.collision-list button:hover { border-color: var(--accent-gold-dim); background: var(--bg-card-hover); }.collision-icon { display: grid; width: 36px; height: 36px; place-items: center; border-radius: 9px; color: var(--accent-gold); background: var(--bg-card); }.collision-copy { min-width: 0; }.collision-copy small, .collision-copy > span { display: block; color: var(--text-muted); font-size: .62rem; }.collision-copy strong { display: block; overflow: hidden; margin: 3px 0; font-size: .76rem; text-overflow: ellipsis; white-space: nowrap; }.collision-copy em { color: var(--text-dim); font-style: normal; }.step-count { color: var(--text-muted); font: 700 .63rem/1 var(--font-mono); }.result-placeholder { display: grid; min-height: 210px; place-items: center; align-content: center; padding: 24px; color: var(--text-muted); text-align: center; }.result-placeholder h2 { margin: 8px 0 3px; color: var(--text-secondary); font-size: 1rem; }.result-placeholder p { max-width: 610px; margin: 0; font-size: .75rem; line-height: 1.45; }
button:focus-visible, input:focus-visible, select:focus-visible, .back-link:focus-visible { outline: 2px solid var(--accent-blue); outline-offset: 2px; }.status-enter-active, .status-leave-active { transition: opacity .18s ease; }.status-enter-from, .status-leave-to { opacity: 0; }
@media (max-width: 1050px) { .profile-toolbar { grid-template-columns: repeat(4, minmax(0, 1fr)); }.profile-select, .profile-name { grid-column: span 2; }.teams-board { grid-template-columns: 1fr; } }
@media (max-width: 650px) { .calculator-hero { align-items: flex-start; flex-direction: column; padding: 20px 17px; }.hero-stats { justify-content: flex-start; }.profile-toolbar { grid-template-columns: repeat(2, 1fr); }.profile-select, .profile-name { grid-column: 1 / -1; }.tool-button span { display: none; }.unit-slots { grid-template-columns: 1fr; }.fight-controls { grid-template-columns: 1fr auto; }.seed-control { grid-column: 1 / -1; }.reset-button { justify-self: stretch; }.fight-button { padding: 9px 14px; }.collision-list button { grid-template-columns: 38px 1fr; }.step-count { display: none; } }
</style>
