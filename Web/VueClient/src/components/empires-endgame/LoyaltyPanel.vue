<script setup lang="ts">
import { BookOpen, Landmark, Scale, ShieldAlert, Users } from 'lucide-vue-next'
import type { EmpiresChronicleEntry } from '../../features/empires-endgame/types'

interface RegionView {
  id: string
  name: string
  value: number
  status: 'controlled' | 'rebellious'
  destroyed: boolean
  negativeStreak: number
  recoveryStreak: number
}

interface CityView {
  id: string
  name: string
  regionName: string
  cityLoyalty: number
  regionLoyalty: number
  effectiveLoyalty: number
  baseWorkforce: number
  effectiveWorkforce: number
  workforceDivisor: number
  classes: Array<{ id: string, name: string, value: number, gates: string[] }>
}

defineProps<{
  minimum: number
  maximum: number
  reputation: number
  rebellionThreshold: number
  rebellionApplications: number
  recoveryThreshold: number
  recoveryApplications: number
  regions: RegionView[]
  cities: CityView[]
  chronicle: EmpiresChronicleEntry[]
}>()

function signed(value: number) {
  return `${value > 0 ? '+' : ''}${Number.isInteger(value) ? value : value.toFixed(1)}`
}
</script>

<template>
  <section class="loyalty-panel" data-testid="loyalty-panel" aria-labelledby="loyalty-title">
    <header>
      <div><span>Политическое состояние</span><h2 id="loyalty-title">Лояльность и летопись</h2></div>
      <div class="reputation" data-testid="reputation-value">
        <Scale :size="18" /><small>Репутация</small><b>{{ signed(reputation) }}</b>
        <span>{{ minimum }}…{{ maximum }}</span>
      </div>
    </header>

    <p class="rule-note">
      <ShieldAlert :size="17" /> Регион восстаёт после {{ rebellionApplications }} изменений на уровне
      {{ signed(rebellionThreshold) }} или ниже; возвращается после {{ recoveryApplications }} изменений
      на уровне {{ signed(recoveryThreshold) }} или выше.
    </p>

    <div class="politics-grid">
      <section>
        <h3><Landmark :size="16" /> Регионы</h3>
        <div class="region-list">
          <article
            v-for="region in regions"
            :key="region.id"
            :class="{ rebellious: region.status === 'rebellious', destroyed: region.destroyed }"
            :data-testid="`region-status-${region.id}`"
          >
            <div><b>{{ region.name }}</b><span>{{ signed(region.value) }}</span></div>
            <small v-if="region.destroyed">Уничтожен</small>
            <small v-else-if="region.status === 'rebellious'">Восстание · восстановление {{ region.recoveryStreak }}/{{ recoveryApplications }}</small>
            <small v-else>Под контролем · риск {{ region.negativeStreak }}/{{ rebellionApplications }}</small>
          </article>
        </div>
      </section>

      <section>
        <h3><Users :size="16" /> Города и сословия</h3>
        <div class="city-list">
          <article v-for="city in cities" :key="city.id" :data-testid="`city-loyalty-${city.id}`">
            <div class="city-title"><b>{{ city.name }}</b><small>{{ city.regionName }}</small><strong>{{ signed(city.effectiveLoyalty) }}</strong></div>
            <p>Город {{ signed(city.cityLoyalty) }} + регион {{ signed(city.regionLoyalty) }} · рабочие {{ city.effectiveWorkforce }} / {{ city.baseWorkforce }} (÷{{ city.workforceDivisor }})</p>
            <ul>
              <li v-for="populationClass in city.classes" :key="populationClass.id">
                <span>{{ populationClass.name }}</span><b>{{ signed(populationClass.value) }}</b>
                <small v-if="populationClass.gates.length">{{ populationClass.gates.join(' · ') }}</small>
              </li>
            </ul>
          </article>
        </div>
      </section>
    </div>

    <section class="chronicle">
      <h3><BookOpen :size="16" /> Летопись</h3>
      <ol v-if="chronicle.length">
        <li
          v-for="entry in chronicle"
          :key="entry.id"
          :data-testid="`chronicle-entry-${entry.kind}`"
        >
          <div><b>{{ entry.title }}</b><time>кон {{ entry.con }} · #{{ entry.sequence }}</time></div>
          <p>{{ entry.description }}</p><small>{{ entry.sourceId }}</small>
        </li>
      </ol>
      <p v-else class="empty">Политическая летопись пока пуста.</p>
    </section>
  </section>
</template>

<style scoped>
.loyalty-panel { display: grid; gap: 18px; padding: clamp(16px, 2vw, 26px); border: 1px solid rgba(209,178,100,.24); border-radius: 18px; background: rgba(12,19,15,.82); }
header { display: flex; align-items: center; justify-content: space-between; gap: 18px; }
header span, h3, small { color: #a9b3a8; } h2, h3, p { margin: 0; } h2 { font-family: Georgia, serif; }
.reputation { display: grid; grid-template-columns: auto auto auto; align-items: center; gap: 3px 8px; padding: 10px 14px; border-radius: 12px; background: rgba(209,178,100,.1); }
.reputation svg { grid-row: 1 / 3; color: #d1b264; }.reputation b { font-size: 1.35rem; color: #ecd58e; }.reputation span { grid-column: 2 / 4; font-size: .7rem; }
.rule-note { display: flex; gap: 9px; align-items: center; padding: 10px 12px; border-left: 3px solid #a87443; background: rgba(168,116,67,.09); color: #d4cab7; }
.politics-grid { display: grid; grid-template-columns: minmax(220px,.75fr) minmax(0,1.6fr); gap: 18px; }.politics-grid section { min-width: 0; }
h3 { display: flex; align-items: center; gap: 7px; margin-bottom: 9px; color: #d9c994; }
.region-list,.city-list { display: grid; gap: 8px; }.region-list article,.city-list article,.chronicle li { padding: 11px 12px; border: 1px solid rgba(255,255,255,.08); border-radius: 11px; background: rgba(255,255,255,.025); }
.region-list article > div,.city-title,.chronicle li > div { display: flex; align-items: center; justify-content: space-between; gap: 8px; }.region-list article span,.city-title strong { color: #ddd09b; }.region-list article.rebellious { border-color: rgba(198,80,65,.55); background: rgba(127,40,32,.15); }.region-list article.destroyed { opacity: .56; filter: grayscale(.55); }
.city-list { grid-template-columns: repeat(auto-fit,minmax(260px,1fr)); }.city-title small { margin-right: auto; }.city-list article > p { margin: 6px 0 9px; font-size: .78rem; color: #aeb6aa; }
ul,ol { list-style: none; margin: 0; padding: 0; }li { min-width: 0; }.city-list li { display: grid; grid-template-columns: 1fr auto; gap: 2px 8px; padding: 4px 0; border-top: 1px solid rgba(255,255,255,.05); }.city-list li small { grid-column: 1 / 3; font-size: .68rem; }
.chronicle ol { display: grid; gap: 7px; max-height: 360px; overflow: auto; }.chronicle li p { margin: 4px 0; color: #d0c8b7; }.chronicle time,.chronicle li > small { color: #7f8c80; font-size: .68rem; }.empty { padding: 18px; text-align: center; color: #849086; }
@media (max-width: 860px) { .politics-grid { grid-template-columns: 1fr; } header { align-items: stretch; flex-direction: column; }.reputation { align-self: start; } }
</style>
