<script setup lang="ts">
import { computed, ref } from 'vue'
import { Crown, Gavel, Landmark, ShieldCheck, UserRoundCog } from 'lucide-vue-next'
import type { EmpiresEndgameEngine } from '../../features/empires-endgame/engine'
import type {
  EmpiresAdvisorTransitionAction,
  EmpiresCampaignState,
  EmpiresEndgameConfig,
} from '../../features/empires-endgame/types'

const props = defineProps<{
  config: EmpiresEndgameConfig
  state: EmpiresCampaignState
  engine: EmpiresEndgameEngine
}>()

const emit = defineEmits<{
  advisor: [advisorId: string, action: EmpiresAdvisorTransitionAction]
  assignGovernor: [perstId: string, regionId: string]
}>()

const selectedRegions = ref<Record<string, string>>({})

const standardAdvisors = computed(() => props.config.governance.advisors.filter(advisor => !advisor.grandAdvisor))
const grandAdvisor = computed(() => props.config.governance.advisors.find(advisor => advisor.grandAdvisor) ?? null)
const regionName = (regionId: string) => props.config.empire.map.regions.find(region => region.id === regionId)?.name ?? regionId
const advisorStatus = (advisorId: string) => props.state.governance.advisors[advisorId]?.status ?? 'locked'
const advisorStatusLabel = (advisorId: string) => ({
  locked: 'Закрыт',
  'awaiting-judgment': 'Ждёт приговора',
  active: 'Помилован · активен',
  executed: 'Казнён',
}[advisorStatus(advisorId)])
const advisorBlocked = (advisorId: string, action: 'pardon' | 'execute') => (
  props.engine.advisorTransitionBlockedReason(advisorId, action) ?? ''
)
const suitLabel = (suit: string) => ({ clubs: '♣ Народ', diamonds: '♦ Торговля', hearts: '♥ Война', spades: '♠ Наука' }[suit] ?? suit)
const alignmentLabel = (advisorId: string) => ({
  locked: 'Не участвует',
  balanced: 'Сбалансированный курс',
  specialized: 'Специализация: масть совпала с козырем',
}[props.engine.advisorAlignment(advisorId)])
const perstAssignment = (perstId: string) => Object.values(props.state.governance.governorAssignments)
  .find(assignment => assignment.perstId === perstId)
const selectedRegion = (perstId: string) => selectedRegions.value[perstId] ?? props.config.governance.governor.regionIds[0] ?? ''
const assignmentBlocked = (perstId: string) => props.engine.governorAssignmentBlockedReason(perstId, selectedRegion(perstId)) ?? ''
const regionSites = (regionId: string) => props.config.governance.governor.citySites
  .filter(site => site.regionId === regionId)
  .sort((left, right) => left.order - right.order)
const accessibleSiteCount = (regionId: string) => regionSites(regionId)
  .filter(site => props.engine.isCityAccessible(site.cityId)).length
</script>

<template>
  <section class="governance-panel" data-testid="governance-panel">
    <header>
      <div>
        <span>Император → Персты → Советники</span>
        <h2><Crown :size="24" /> Управление империей</h2>
      </div>
      <p data-testid="governance-trump">
        Козырь: <b>{{ suitLabel(state.durak.trumpSuit) }}</b>
        · критический эффект ×{{ config.governance.trump.criticalEffectMultiplier }}
      </p>
    </header>

    <section aria-labelledby="advisor-heading">
      <h3 id="advisor-heading"><Gavel :size="18" /> Суд над советниками</h3>
      <p class="section-copy">В начале правления можно помиловать одного из трёх советников; двое остальных должны быть казнены.</p>
      <div class="advisor-grid">
        <article v-for="advisor in standardAdvisors" :key="advisor.id" :data-testid="`advisor-${advisor.id}`">
          <div><b>{{ advisor.name }}</b><small>{{ suitLabel(advisor.suit) }}</small></div>
          <strong>{{ advisorStatusLabel(advisor.id) }}</strong>
          <p>{{ alignmentLabel(advisor.id) }}</p>
          <div class="actions">
            <button
              :data-testid="`advisor-pardon-${advisor.id}`"
              type="button"
              :disabled="Boolean(advisorBlocked(advisor.id, 'pardon'))"
              :title="advisorBlocked(advisor.id, 'pardon')"
              @click="emit('advisor', advisor.id, 'pardon')"
            >Помиловать</button>
            <button
              :data-testid="`advisor-execute-${advisor.id}`"
              class="danger"
              type="button"
              :disabled="Boolean(advisorBlocked(advisor.id, 'execute'))"
              :title="advisorBlocked(advisor.id, 'execute')"
              @click="emit('advisor', advisor.id, 'execute')"
            >Казнить</button>
          </div>
        </article>
      </div>
      <article v-if="grandAdvisor" class="grand-advisor" data-testid="advisor-grand-status">
        <Crown :size="22" />
        <div><b>{{ grandAdvisor.name }}</b><p>{{ advisorStatusLabel(grandAdvisor.id) }} · {{ suitLabel(grandAdvisor.suit) }}</p></div>
        <small v-if="advisorStatus(grandAdvisor.id) === 'locked'">{{ grandAdvisor.accessDeferredReason }}</small>
        <strong v-else>{{ alignmentLabel(grandAdvisor.id) }}</strong>
      </article>
    </section>

    <section aria-labelledby="perst-heading">
      <h3 id="perst-heading"><UserRoundCog :size="18" /> Персты-губернаторы</h3>
      <p class="section-copy">Назначение окончательно. Перст открывает три новых города: два во втором слое обороны и один в третьем.</p>
      <div class="perst-grid">
        <article v-for="perst in config.governance.persts" :key="perst.id" :data-testid="`perst-${perst.id}`">
          <div><b>{{ perst.title }} · {{ perst.name }}</b><small>{{ perst.description }}</small></div>
          <template v-if="perstAssignment(perst.id)">
            <strong>Губернатор: {{ regionName(perstAssignment(perst.id)!.regionId) }}</strong>
          </template>
          <template v-else>
            <label :for="`perst-region-${perst.id}`">Регион</label>
            <select
              :id="`perst-region-${perst.id}`"
              v-model="selectedRegions[perst.id]"
              :data-testid="`perst-region-${perst.id}`"
            >
              <option v-for="regionId in config.governance.governor.regionIds" :key="regionId" :value="regionId">{{ regionName(regionId) }}</option>
            </select>
            <button
              :data-testid="`perst-assign-${perst.id}`"
              type="button"
              :disabled="Boolean(assignmentBlocked(perst.id))"
              :title="assignmentBlocked(perst.id)"
              @click="emit('assignGovernor', perst.id, selectedRegion(perst.id))"
            >Назначить навсегда</button>
          </template>
        </article>
      </div>
      <div class="region-grid">
        <article v-for="regionId in config.governance.governor.regionIds" :key="regionId" :data-testid="`governance-region-${regionId}`">
          <ShieldCheck :size="18" />
          <div><b>{{ regionName(regionId) }}</b><small>Доступно {{ accessibleSiteCount(regionId) }} / {{ regionSites(regionId).length }} городов</small></div>
          <ol>
            <li v-for="site in regionSites(regionId)" :key="site.cityId" :class="{ locked: !engine.isCityAccessible(site.cityId) }">
              Слой {{ site.defenseLayer }} · {{ config.empire.cities.find(city => city.id === site.cityId)?.name }}
            </li>
          </ol>
        </article>
      </div>
    </section>

    <section aria-labelledby="capital-heading">
      <h3 id="capital-heading"><Landmark :size="18" /> Столичный реестр</h3>
      <div class="capital-grid">
        <article v-for="site in config.governance.capital.sites" :key="site.id" :data-testid="`capital-site-${site.id}`">
          <div><b>{{ site.name }}</b><small>{{ site.owner }}</small></div>
          <p>{{ site.deferredReason }}</p>
        </article>
      </div>
    </section>
  </section>
</template>

<style scoped>
.governance-panel{display:grid;gap:22px;min-height:550px;padding:22px;border:1px solid rgba(216,190,133,.15);border-radius:16px;background:rgba(18,22,18,.93);color:#e9dfca}.governance-panel>header{display:flex;align-items:end;justify-content:space-between;gap:20px}.governance-panel header span{color:#a9935f;font:800 .58rem/1 monospace;text-transform:uppercase}.governance-panel h2{display:flex;align-items:center;gap:8px;margin:6px 0 0;font:700 1.8rem/1 Georgia,serif}.governance-panel header p{margin:0;color:#b9aa88;font-size:.76rem}.governance-panel h3{display:flex;align-items:center;gap:7px;margin:0 0 5px;color:#dfc783;font:700 1rem/1.2 Georgia,serif}.section-copy{margin:0 0 12px;color:#958b76;font-size:.72rem}.advisor-grid,.perst-grid,.region-grid,.capital-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:10px}.advisor-grid article,.perst-grid article,.region-grid article,.capital-grid article,.grand-advisor{padding:13px;border:1px solid rgba(216,190,133,.16);border-radius:9px;background:rgba(255,255,255,.025)}article div{display:grid;gap:3px}article small{color:#91866e;font-size:.65rem}article strong{display:block;margin:9px 0 4px;color:#d5bd79;font-size:.72rem}article p{margin:4px 0;color:#aca18b;font-size:.68rem;line-height:1.45}.actions{display:grid!important;grid-template-columns:1fr 1fr;gap:6px!important;margin-top:10px}button,select{min-height:34px;border:1px solid rgba(212,180,100,.3);border-radius:6px;color:#e2cf9c;background:#24271f;font-size:.68rem}button{cursor:pointer}button.danger{color:#dca49b;border-color:rgba(190,95,78,.35)}button:disabled{opacity:.38;cursor:not-allowed}.grand-advisor{display:grid;grid-template-columns:auto auto 1fr;align-items:center;gap:12px;margin-top:10px}.grand-advisor small{justify-self:end;max-width:620px}.perst-grid label{margin-top:8px;color:#91866e;font-size:.62rem}.perst-grid select{width:100%;padding:0 8px}.perst-grid button{margin-top:6px;width:100%}.region-grid{margin-top:12px}.region-grid article{display:grid;grid-template-columns:auto 1fr;align-items:start;gap:8px}.region-grid ol{grid-column:1/-1;margin:5px 0 0;padding-left:19px;color:#aaa087;font-size:.64rem;line-height:1.55}.region-grid li.locked{color:#695f50}.capital-grid article div{display:flex;justify-content:space-between;gap:8px}.capital-grid article p{margin-bottom:0}@media(max-width:760px){.governance-panel>header{align-items:start;flex-direction:column}.grand-advisor{grid-template-columns:auto 1fr}.grand-advisor small{grid-column:1/-1;justify-self:start}}
</style>
