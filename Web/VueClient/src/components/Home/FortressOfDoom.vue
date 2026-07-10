<script setup lang="ts">
import { computed } from 'vue'
import { useGameStore } from 'src/store/game'
import type { DoomFortressStage } from 'src/services/signalr'

const store = useGameStore()
const stages = computed(() => store.doomFortressState?.stages ?? [])

function moduleDescription(stage: DoomFortressStage, moduleName: string): string {
  return stage.unlockedModules.find(m => m.name === moduleName)?.description ?? 'Слот заполнится автоматически после получения нового модуля.'
}

function equip(stage: DoomFortressStage, slotIndex: number, event: Event) {
  const moduleName = (event.target as HTMLSelectElement).value
  if (moduleName) void store.equipDoomModule(stage.name, slotIndex, moduleName)
}
</script>

<template>
  <section class="fortress">
    <header class="fortress-header">
      <div>
        <span class="eyebrow">DooM Guy loadout</span>
        <h1>Fortress of Doom</h1>
      </div>
      <span class="fortress-status">4 combat stages</span>
    </header>

    <div v-if="stages.length === 0" class="fortress-loading">Загрузка модулей...</div>
    <div v-else class="stage-grid">
      <article v-for="(stage, stageIndex) in stages" :key="stage.name" class="stage-card">
        <div class="stage-heading">
          <span class="stage-number">0{{ stageIndex + 1 }}</span>
          <div>
            <h2>{{ stage.name }}</h2>
            <p>Осталось наградных: {{ stage.rewardModulesRemaining }} · шанс {{ stage.currentDropChance }}%</p>
          </div>
        </div>

        <div class="slot-list">
          <label v-for="(slot, slotIndex) in stage.slots" :key="slotIndex" class="module-slot" :class="{ empty: !slot }">
            <span class="slot-index">SLOT {{ slotIndex + 1 }}</span>
            <select :value="slot" @change="equip(stage, slotIndex, $event)">
              <option value="" disabled>Ожидает модуль</option>
              <option v-for="module in stage.unlockedModules" :key="module.name" :value="module.name">
                {{ module.reward ? '◆ ' : '' }}{{ module.name }}
              </option>
            </select>
            <span class="slot-description">{{ moduleDescription(stage, slot) }}</span>
          </label>
        </div>
      </article>
    </div>
  </section>
</template>

<style scoped>
.fortress {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  min-width: 0;
}
.fortress-header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  padding: 1.25rem 1.4rem;
  border: 1px solid rgba(220, 58, 35, .45);
  border-radius: 12px;
  background: linear-gradient(135deg, rgba(70, 12, 8, .78), rgba(18, 18, 18, .96));
  box-shadow: inset 0 0 36px rgba(255, 55, 20, .08);
}
.eyebrow, .slot-index { color: #d76b43; font-size: .66rem; font-weight: 900; letter-spacing: .16em; text-transform: uppercase; }
h1 { margin: .2rem 0 0; color: #f2e7d3; font-size: clamp(1.55rem, 4vw, 2.4rem); letter-spacing: .04em; text-transform: uppercase; }
.fortress-status { color: #e2a467; font-family: 'JetBrains Mono', monospace; font-size: .72rem; }
.stage-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .8rem; }
.stage-card { padding: 1rem; border: 1px solid rgba(151, 74, 48, .42); border-radius: 10px; background: rgba(19, 20, 20, .93); }
.stage-heading { display: flex; gap: .75rem; align-items: center; margin-bottom: .85rem; }
.stage-number { color: rgba(220, 74, 42, .42); font-size: 2rem; font-weight: 950; line-height: 1; }
h2 { margin: 0; color: #efc59d; font-size: 1rem; text-transform: uppercase; }
.stage-heading p { margin: .15rem 0 0; color: #8e8a82; font-size: .67rem; }
.slot-list { display: grid; gap: .5rem; }
.module-slot { display: flex; flex-direction: column; gap: .3rem; padding: .6rem; border-left: 3px solid #9d351f; background: rgba(255, 255, 255, .025); }
.module-slot.empty { border-left-color: #414141; opacity: .75; }
select { width: 100%; padding: .45rem; color: #f2e7d3; border: 1px solid #4e3a32; border-radius: 4px; background: #171717; font-weight: 800; }
.slot-description { color: #9f9a91; font-size: .68rem; line-height: 1.35; }
.fortress-loading { padding: 2rem; color: #b5a999; text-align: center; }
@media (max-width: 900px) { .stage-grid { grid-template-columns: 1fr; } .fortress-header { align-items: flex-start; flex-direction: column; gap: .5rem; } }
</style>
