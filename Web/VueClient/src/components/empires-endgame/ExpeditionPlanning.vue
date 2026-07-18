<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { AlertTriangle, Castle, Map, PackageOpen, Shield, Swords, X } from 'lucide-vue-next'
import type { EmpiresExpeditionPlanningView } from '../../features/empires-endgame/types'

const props = defineProps<{
  view: EmpiresExpeditionPlanningView
}>()

const emit = defineEmits<{
  close: []
  launch: [unitInstanceIds: string[], provisionAmount: number, installmentCount: number]
  payInstallment: []
  assault: []
  abort: []
}>()

const selectedIds = ref<string[]>([])
const provisionAmount = ref(0)
const installmentCount = ref(1)

watch(() => [props.view.definitionId, props.view.status] as const, () => {
  selectedIds.value = [...props.view.selectedUnitInstanceIds]
  provisionAmount.value = props.view.provisionRequested
  installmentCount.value = Math.min(props.view.maxInstallments, Math.max(1, installmentCount.value))
}, { immediate: true })

const selectedUnits = computed(() => props.view.roster.filter(unit => (
  selectedIds.value.includes(unit.unitInstanceId)
)))
const selectedRequired = computed(() => selectedUnits.value.reduce((total, unit) => (
  total + unit.foodPerCon * props.view.effectiveDurationCons
), 0))
const canLaunch = computed(() => props.view.status === 'planning'
  && selectedIds.value.length > 0
  && Number.isFinite(provisionAmount.value)
  && provisionAmount.value >= 0)

function toggleUnit(unitInstanceId: string) {
  if (selectedIds.value.includes(unitInstanceId)) {
    selectedIds.value = selectedIds.value.filter(id => id !== unitInstanceId)
  } else {
    selectedIds.value = [...selectedIds.value, unitInstanceId]
  }
  provisionAmount.value = selectedRequired.value
}

const statusLabel: Record<EmpiresExpeditionPlanningView['status'], string> = {
  available: 'доступна',
  planning: 'планирование',
  provisioning: 'снабжение',
  ready: 'готова к штурму',
  fighting: 'идёт штурм',
  won: 'крепость взята',
  lost: 'поражение',
  aborted: 'прервана',
}
</script>

<template>
  <div class="expedition-backdrop" role="presentation" @click.self="emit('close')">
    <section class="expedition-panel" role="dialog" aria-modal="true" :aria-label="view.name" data-testid="expedition-planning">
      <header>
        <div class="fort-seal"><Castle :size="28" /></div>
        <div>
          <span>Экспедиция · {{ statusLabel[view.status] }}</span>
          <h2>{{ view.name }}</h2>
          <p><Map :size="14" /> {{ view.zoneName }} · {{ view.plannedDurationCons }} кона по маршруту</p>
        </div>
        <button type="button" aria-label="Закрыть" data-testid="expedition-close" @click="emit('close')"><X :size="18" /></button>
      </header>

      <div class="expedition-grid">
        <article class="expedition-card intelligence">
          <h3><Shield :size="17" /> Противник</h3>
          <strong>{{ view.enemyProfileName }}</strong>
          <p>{{ view.enemyDescription }}</p>
          <div v-if="view.enemyIntel === 'exact'" class="enemy-groups" data-testid="expedition-exact-intel">
            <span v-for="group in view.enemyGroups" :key="group.id">
              <b>{{ group.count }}×</b> {{ group.id }}<small>{{ group.armorClassId || 'без брони' }}</small>
            </span>
          </div>
          <small v-else class="intel-note">Точные числа откроют «Подробнейшие карты мира».</small>
        </article>

        <article class="expedition-card logistics">
          <h3><PackageOpen :size="17" /> Маршрут и провизия</h3>
          <dl>
            <div><dt>Подготовка</dt><dd>{{ view.preparationDays }} дней</dd></div>
            <div><dt>Длительность</dt><dd>{{ view.effectiveDurationCons }} кон.</dd></div>
            <div><dt>Логистика</dt><dd>+{{ view.speedPercent }}%</dd></div>
            <div><dt>Бонус карт</dt><dd>+{{ view.mapBonusPercent }}%</dd></div>
            <div><dt>В регионе</dt><dd>{{ view.provisionAvailable }}</dd></div>
            <div><dt>Списано</dt><dd>{{ view.provisionWithdrawn }}</dd></div>
          </dl>
          <p v-if="view.installmentBlockedReason" class="warning"><AlertTriangle :size="14" /> {{ view.installmentBlockedReason }}</p>
        </article>
      </div>

      <article v-if="view.status === 'planning'" class="roster-card">
        <header>
          <div><h3>Состав экспедиции</h3><small>У каждого бойца сохраняются собственные здоровье, раны и статус ветерана.</small></div>
          <b>{{ selectedIds.length }} выбрано</b>
        </header>
        <div class="roster-list">
          <label v-for="unit in view.roster" :key="unit.unitInstanceId" :class="{ disabled: !unit.eligible }">
            <input
              type="checkbox"
              :checked="selectedIds.includes(unit.unitInstanceId)"
              :disabled="!unit.eligible"
              :data-testid="`expedition-unit-${unit.unitInstanceId}`"
              @change="toggleUnit(unit.unitInstanceId)"
            >
            <span><strong>{{ unit.unitName }}</strong><small>{{ unit.cityName }} · {{ unit.unitInstanceId }}</small></span>
            <em v-if="unit.veteran">Ветеран · ран {{ unit.wounds }}</em>
            <em v-else>{{ Math.round(unit.healthRatio * 100) }}% здоровья</em>
            <small v-if="unit.disabledReason" class="disabled-reason">{{ unit.disabledReason }}</small>
          </label>
          <p v-if="view.roster.length === 0" class="empty-roster">В исходном регионе нет набранных отрядов.</p>
        </div>
        <div class="provision-controls">
          <label>Провизия
            <input v-model.number="provisionAmount" data-testid="expedition-provision" type="number" min="0" step="1">
          </label>
          <label>Платежей
            <select v-model.number="installmentCount" data-testid="expedition-installments">
              <option v-for="count in view.maxInstallments" :key="count" :value="count">{{ count }}</option>
            </select>
          </label>
          <span>Расчётная потребность: <b>{{ selectedRequired }}</b></span>
        </div>
      </article>

      <footer>
        <button v-if="view.status === 'planning'" class="secondary" type="button" @click="emit('abort')">Отменить без расходов</button>
        <button v-if="view.status === 'provisioning'" class="secondary" type="button" @click="emit('abort')">Прервать без возврата</button>
        <button v-if="view.status === 'provisioning'" type="button" data-testid="expedition-pay" @click="emit('payInstallment')">Внести платёж</button>
        <button
          v-if="view.status === 'planning'"
          type="button"
          data-testid="expedition-launch"
          :disabled="!canLaunch"
          @click="emit('launch', selectedIds, provisionAmount, installmentCount)"
        >Снарядить экспедицию</button>
        <button v-if="view.status === 'ready'" class="secondary" type="button" @click="emit('abort')">Отступить без возврата</button>
        <button v-if="view.status === 'ready'" type="button" data-testid="expedition-assault" @click="emit('assault')"><Swords :size="16" /> Начать штурм</button>
        <button v-if="view.status === 'won' || view.opened" type="button" @click="emit('close')">Зона открыта</button>
      </footer>
    </section>
  </div>
</template>

<style scoped>
.expedition-backdrop { position:fixed; z-index:70; inset:0; display:grid; place-items:center; padding:20px; background:rgba(5,8,7,.78); backdrop-filter:blur(7px); }
.expedition-panel { width:min(980px,96vw); max-height:92vh; overflow:auto; border:1px solid rgba(211,178,96,.35); border-radius:18px; color:#eee3cc; background:radial-gradient(circle at 12% 0,rgba(190,151,72,.13),transparent 30%),#111713; box-shadow:0 30px 100px rgba(0,0,0,.65); }
.expedition-panel > header { display:grid; grid-template-columns:auto 1fr auto; align-items:center; gap:14px; padding:20px 22px; border-bottom:1px solid rgba(220,192,128,.14); }
.fort-seal { display:grid; width:58px; height:58px; place-items:center; border:1px solid rgba(214,180,101,.32); border-radius:50%; color:#d5b565; background:rgba(206,169,83,.08); }
.expedition-panel > header span { color:#b89c5d; font:800 .58rem/1 monospace; letter-spacing:.12em; text-transform:uppercase; }
.expedition-panel h2 { margin:5px 0 4px; font:700 1.7rem/1 Georgia,serif; }
.expedition-panel > header p { display:flex; align-items:center; gap:5px; margin:0; color:rgba(238,227,204,.52); font-size:.7rem; }
.expedition-panel button { display:inline-flex; min-height:36px; align-items:center; justify-content:center; gap:6px; padding:0 13px; border:1px solid #9f8349; border-radius:7px; color:#211b11; background:#cfaf5f; font-weight:800; cursor:pointer; }
.expedition-panel button:disabled { opacity:.4; cursor:not-allowed; }.expedition-panel > header > button { width:36px; padding:0; border-color:rgba(230,211,167,.15); color:#cfc2a7; background:rgba(255,255,255,.03); }
.expedition-grid { display:grid; grid-template-columns:1.2fr .8fr; gap:10px; padding:14px 16px 0; }.expedition-card,.roster-card { padding:15px; border:1px solid rgba(219,190,126,.13); border-radius:11px; background:rgba(255,255,255,.025); }
.expedition-card h3,.roster-card h3 { display:flex; align-items:center; gap:6px; margin:0 0 9px; color:#d9c083; font:700 .9rem/1 Georgia,serif; }.expedition-card > strong { font:700 1.08rem/1.1 Georgia,serif; }.expedition-card > p { color:rgba(238,227,204,.55); font-size:.7rem; line-height:1.45; }
.enemy-groups { display:grid; gap:5px; margin-top:10px; }.enemy-groups span { display:grid; grid-template-columns:auto 1fr auto; gap:6px; padding:7px 9px; border-radius:6px; background:rgba(0,0,0,.18); font-size:.65rem; }.enemy-groups b { color:#ddc477; }.enemy-groups small,.intel-note { color:rgba(238,227,204,.42); }
.logistics dl { display:grid; grid-template-columns:1fr 1fr; gap:6px; margin:0; }.logistics dl div { padding:7px; border-radius:6px; background:rgba(0,0,0,.16); }.logistics dt { color:rgba(238,227,204,.42); font-size:.56rem; }.logistics dd { margin:3px 0 0; color:#dec579; font:700 .82rem/1 Georgia,serif; }.warning { display:flex; gap:5px; color:#e4b08b!important; }
.roster-card { margin:10px 16px 0; }.roster-card > header { display:flex; align-items:start; justify-content:space-between; gap:10px; }.roster-card h3 { margin-bottom:4px; }.roster-card header small { color:rgba(238,227,204,.42); font-size:.6rem; }.roster-card header > b { color:#d7bc76; font-size:.68rem; }
.roster-list { display:grid; max-height:260px; grid-template-columns:1fr 1fr; gap:5px; overflow:auto; margin-top:10px; }.roster-list label { display:grid; grid-template-columns:auto 1fr auto; align-items:center; gap:8px; padding:8px; border:1px solid rgba(220,190,125,.12); border-radius:7px; background:rgba(0,0,0,.13); cursor:pointer; }.roster-list label.disabled { opacity:.45; cursor:not-allowed; }.roster-list label span { display:grid; }.roster-list label strong { font-size:.7rem; }.roster-list label small,.roster-list label em { color:rgba(238,227,204,.45); font-size:.55rem; font-style:normal; }.disabled-reason { grid-column:2/4; color:#d99b79!important; }.empty-roster { color:rgba(238,227,204,.45); font-size:.7rem; }
.provision-controls { display:flex; align-items:end; gap:10px; margin-top:12px; }.provision-controls label { display:grid; gap:4px; color:rgba(238,227,204,.55); font-size:.58rem; }.provision-controls input,.provision-controls select { width:115px; height:34px; padding:0 8px; border:1px solid rgba(219,189,123,.2); border-radius:6px; color:#eee3cc; background:#182019; }.provision-controls > span { margin-left:auto; color:rgba(238,227,204,.48); font-size:.65rem; }.provision-controls b { color:#dcc276; }
.expedition-panel > footer { display:flex; justify-content:flex-end; gap:7px; padding:16px; }.expedition-panel button.secondary { border-color:rgba(219,190,126,.22); color:#d8c9aa; background:rgba(255,255,255,.035); }
@media(max-width:720px){.expedition-grid,.roster-list{grid-template-columns:1fr}.provision-controls{align-items:stretch;flex-direction:column}.provision-controls>span{margin-left:0}.expedition-panel>footer{flex-wrap:wrap}}
</style>
