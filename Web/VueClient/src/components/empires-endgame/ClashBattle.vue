<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  applyClashCommand,
  clashResultFromState,
  replayClashState,
} from '../../features/empires-endgame/clash/engine'
import {
  CLASH_QA_POLICIES,
  resolveClashWithPolicy,
  type ClashQaPolicy,
} from '../../features/empires-endgame/clash/qa'
import type {
  ClashAbilityDefinition,
  ClashCellState,
  ClashCommand,
  ClashResult,
  ClashSide,
  ClashUnitDefinition,
  ClashUnitState,
  EmpiresClashMinigameSession,
} from '../../features/empires-endgame/types'

const props = withDefaults(defineProps<{
  session: EmpiresClashMinigameSession
  qaMode?: boolean
  qaPolicy?: ClashQaPolicy
}>(), {
  qaMode: false,
  qaPolicy: 'balanced',
})

const emit = defineEmits<{
  resolve: [result: ClashResult]
  abort: [turnLog: ClashCommand[], turn: number]
  progress: [turnLog: ClashCommand[]]
}>()

const SIDES: readonly ClashSide[] = ['defender', 'attacker']
const state = ref(replayClashState(props.session.plan, props.session.seed, props.session.turnLog))
const selectedReserveId = ref<string | null>(null)
const selectedActorId = ref<string | null>(null)
const selectedAbilityId = ref<string | null>(null)
const qaPolicy = ref<ClashQaPolicy>(props.qaPolicy)
const emitted = ref(false)

onMounted(() => {
  const result = clashResultFromState(props.session.plan, state.value)
  if (!result || emitted.value) return
  emitted.value = true
  emit('resolve', result)
})

const plan = computed(() => props.session.plan)
const definitions = computed(() => new Map(plan.value.units.map(unit => [unit.id, unit])))
const phaseLabel = computed(() => ({
  placement: 'Расстановка',
  'between-clashes': 'Между клэшами',
  'clash-ready': 'Готово к столкновению',
  finished: 'Бой завершён',
}[state.value.phase]))
const expectedSideLabel = computed(() => state.value.expectedSide
  ? sideLabel(state.value.expectedSide)
  : '—')
const commandCapacity = computed(() => Math.max(0, plan.value.maxCommands - state.value.commandLog.length))

const reserveBySide = computed<Record<ClashSide, ClashUnitState[]>>(() => ({
  attacker: availableReserve('attacker'),
  defender: availableReserve('defender'),
}))

const activeReserve = computed(() => {
  const side = state.value.expectedSide
  if (!side) return null
  const candidates = reserveBySide.value[side]
  return candidates.find(unit => unit.instanceId === selectedReserveId.value) ?? candidates[0] ?? null
})

const selectedActor = computed(() => {
  if (!selectedActorId.value) return null
  return state.value.units[selectedActorId.value] ?? null
})

const selectedActorDefinition = computed(() => selectedActor.value
  ? definitions.value.get(selectedActor.value.definitionId) ?? null
  : null)

const selectedAbilities = computed(() => selectedActorDefinition.value?.abilities ?? [])

const revealedPassives = computed(() => {
  const revealed = new Map<string, { id: string, name: string, unitName: string }>()
  for (const unit of Object.values(state.value.units)) {
    const definition = definitions.value.get(unit.definitionId)
    if (!definition) continue
    for (const passive of definition.passives) {
      if (!passive.hidden || !unit.hiddenPassiveIdsRevealed.includes(passive.id)) continue
      revealed.set(`${unit.instanceId}:${passive.id}`, {
        id: `${unit.instanceId}:${passive.id}`,
        name: passive.name,
        unitName: definition.name,
      })
    }
  }
  return [...revealed.values()]
})

function sideLabel(side: ClashSide): string {
  return side === 'attacker' ? 'Атакующие' : 'Защитники'
}

function stableCompare(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0
}

function definitionFor(unit: ClashUnitState | null | undefined): ClashUnitDefinition | null {
  return unit ? definitions.value.get(unit.definitionId) ?? null : null
}

function cellUnitIds(cell: ClashCellState): string[] {
  return cell.unitInstanceId ? [cell.unitInstanceId] : []
}

function cellUnits(cell: ClashCellState): ClashUnitState[] {
  return cellUnitIds(cell).flatMap(id => state.value.units[id] ? [state.value.units[id]] : [])
}

function availableReserve(side: ClashSide): ClashUnitState[] {
  return Object.values(state.value.units)
    .filter(unit => unit.side === side && unit.alive && !unit.deployed)
    .sort((left, right) => stableCompare(left.instanceId, right.instanceId))
}

function rowsForSide(side: ClashSide): number[] {
  const rows = Array.from({ length: plan.value.field.rowsPerSide }, (_, row) => row)
  return side === 'defender' ? rows.reverse() : rows
}

function cell(side: ClashSide, row: number, column: number): ClashCellState {
  const found = state.value.cells.find(candidate => (
    candidate.side === side && candidate.row === row && candidate.column === column
  ))
  if (!found) throw new Error(`Missing Clash cell ${side}:${row}:${column}.`)
  return found
}

function firstPlacementRow(side: ClashSide): number | null {
  const deployableRows = plan.value.field.rowsPerSide - plan.value.field.reinforcementRows
  for (let row = 0; row < deployableRows; row += 1) {
    if (state.value.cells.some(candidate => candidate.side === side
      && candidate.row === row && cellUnitIds(candidate).length === 0)) return row
  }
  return null
}

function canPlaceAt(target: ClashCellState): boolean {
  if (!activeReserve.value || target.side !== state.value.expectedSide
    || cellUnitIds(target).length > 0
    || (plan.value.corpseBlocksAdvance && target.corpseIds.length > 0)) return false
  if (state.value.phase === 'placement') return target.row === firstPlacementRow(target.side)
  if (state.value.phase !== 'between-clashes') return false
  const window = state.value.betweenClashes[target.side]
  return !window.ended && (!plan.value.onePlacementPerSideBetweenClashes || !window.placementUsed)
}

function cellCanTargetAbility(target: ClashCellState): boolean {
  const actor = selectedActor.value
  const ability = selectedAbilities.value.find(candidate => candidate.id === selectedAbilityId.value)
  if (state.value.phase !== 'between-clashes' || !actor || !ability
    || actor.side !== state.value.expectedSide) return false
  const occupants = cellUnits(target)
  if (ability.target === 'enemy') return occupants.some(unit => unit.side !== actor.side)
  if (ability.target === 'ally') return occupants.some(unit => unit.side === actor.side)
  return ['cell', 'row', 'column'].includes(ability.target)
}

function cellInteractive(target: ClashCellState): boolean {
  return canPlaceAt(target) || cellCanTargetAbility(target) || cellUnitIds(target).length > 0
}

function unitLabel(unit: ClashUnitState): string {
  const definition = definitionFor(unit)
  return `${definition?.name ?? unit.definitionId}: ${Math.max(0, unit.hp)}/${unit.maxHp} ХП`
}

function cellLabel(target: ClashCellState): string {
  const contents = cellUnits(target)
  const location = `${sideLabel(target.side)}, ряд ${target.row + 1}, колонна ${target.column + 1}`
  const units = contents.length > 0 ? contents.map(unitLabel).join('; ') : 'пусто'
  const corpses = target.corpseIds.length > 0 ? `, останков: ${target.corpseIds.length}` : ''
  const terrain = target.terrainId ? `, местность: ${target.terrainId}` : ''
  const placement = canPlaceAt(target) && activeReserve.value
    ? `. Выставить: ${unitLabel(activeReserve.value)}`
    : ''
  return `${location}: ${units}${corpses}${terrain}${placement}`
}

function issue(command: ClashCommand): void {
  const next = applyClashCommand(plan.value, state.value, command)
  state.value = next
  if (next.error) return
  emit('progress', [...next.commandLog])
  selectedReserveId.value = null
  selectedAbilityId.value = null
  const result = clashResultFromState(plan.value, next)
  if (result && !emitted.value) {
    emitted.value = true
    emit('resolve', result)
  }
}

function activationCommand(
  actor: ClashUnitState,
  ability: ClashAbilityDefinition,
  target?: ClashCellState,
): Extract<ClashCommand, { kind: 'activate' }> {
  const targetUnit = target
    ? cellUnits(target).find(unit => ability.target === 'enemy'
      ? unit.side !== actor.side
      : ability.target === 'ally'
        ? unit.side === actor.side
        : true) ?? null
    : null
  return {
    turn: state.value.turn + 1,
    kind: 'activate',
    side: actor.side,
    unitInstanceId: actor.instanceId,
    abilityId: ability.id,
    ...(targetUnit ? { targetUnitInstanceId: targetUnit.instanceId } : {}),
    ...(target ? {
      targetSide: target.side,
      targetRow: target.row,
      targetColumn: target.column,
    } : ability.kind === 'morale' ? { targetSide: actor.side } : {}),
  }
}

function chooseAbility(ability: ClashAbilityDefinition): void {
  const actor = selectedActor.value
  if (!actor || actor.side !== state.value.expectedSide || state.value.phase !== 'between-clashes') return
  if (['self', 'all-enemies'].includes(ability.target) || ability.kind === 'morale') {
    issue(activationCommand(actor, ability))
    return
  }
  selectedAbilityId.value = ability.id
}

function activateCell(target: ClashCellState): void {
  const actor = selectedActor.value
  const ability = selectedAbilities.value.find(candidate => candidate.id === selectedAbilityId.value)
  if (actor && ability && cellCanTargetAbility(target)) {
    issue(activationCommand(actor, ability, target))
    return
  }
  if (canPlaceAt(target) && activeReserve.value) {
    issue({
      turn: state.value.turn + 1,
      kind: 'place',
      side: target.side,
      unitInstanceId: activeReserve.value.instanceId,
      row: target.row,
      column: target.column,
    })
    return
  }
  const selectable = cellUnits(target).find(unit => unit.side === state.value.expectedSide)
    ?? cellUnits(target)[0]
  if (selectable) {
    selectedActorId.value = selectable.instanceId
    selectedAbilityId.value = null
  }
}

function endBetweenClash(): void {
  const side = state.value.expectedSide
  if (!side || state.value.phase !== 'between-clashes') return
  issue({ turn: state.value.turn + 1, kind: 'end-between-clash', side })
}

function resolveRound(): void {
  if (state.value.phase !== 'clash-ready') return
  issue({ turn: state.value.turn + 1, kind: 'resolve-clash' })
}

function fastResolve(): void {
  if (!props.qaMode || emitted.value) return
  const result = resolveClashWithPolicy(
    plan.value,
    props.session.seed,
    qaPolicy.value,
    state.value.commandLog,
  )
  emitted.value = true
  emit('resolve', result)
}

function requestAbort(): void {
  emit('abort', [...state.value.commandLog], state.value.turn)
}
</script>

<template>
  <section class="clash" data-testid="clash-minigame" aria-labelledby="clash-title">
    <header class="clash__header">
      <div>
        <span>Оффенсив-лейн · {{ session.plan.field.name }}</span>
        <h2 id="clash-title">Клэш</h2>
        <p>{{ session.plan.region.name }} · ход {{ state.turn }} · столкновение {{ state.clashNumber }}</p>
      </div>
      <dl class="clash__status" aria-label="Состояние боя">
        <div><dt>Фаза</dt><dd data-testid="clash-phase">{{ phaseLabel }}</dd></div>
        <div><dt>Действует</dt><dd>{{ expectedSideLabel }}</dd></div>
        <div><dt>Команд осталось</dt><dd>{{ commandCapacity }}</dd></div>
      </dl>
    </header>

    <div class="clash__morale" aria-label="Мораль сторон">
      <article v-for="side in ['attacker', 'defender'] as const" :key="side">
        <b>{{ sideLabel(side) }}</b>
        <span :data-testid="`clash-morale-${side}`">{{ state.morale[side] }}</span>
      </article>
    </div>

    <div class="clash__layout">
      <aside class="clash__reserves" aria-labelledby="clash-reserves-title">
        <h3 id="clash-reserves-title">Резерв</h3>
        <section v-for="side in ['attacker', 'defender'] as const" :key="side">
          <h4>{{ sideLabel(side) }}</h4>
          <p v-if="reserveBySide[side].length === 0">Резерв пуст.</p>
          <button
            v-for="unit in reserveBySide[side]"
            :key="unit.instanceId"
            type="button"
            :data-testid="`clash-reserve-${unit.instanceId}`"
            :aria-pressed="activeReserve?.instanceId === unit.instanceId"
            :disabled="state.expectedSide !== side || !['placement', 'between-clashes'].includes(state.phase)"
            @click="selectedReserveId = unit.instanceId"
          >
            <b>{{ definitionFor(unit)?.name ?? unit.definitionId }}</b>
            <small>{{ unit.hp }}/{{ unit.maxHp }} ХП</small>
          </button>
        </section>
      </aside>

      <div
        class="clash__board"
        role="grid"
        aria-label="Поле боя Клэша"
        :aria-rowcount="session.plan.field.rowsPerSide * 2"
        :aria-colcount="session.plan.field.columns"
      >
        <section v-for="side in SIDES" :key="side" class="clash__side" role="rowgroup" :aria-label="sideLabel(side)">
          <h3 aria-hidden="true">{{ sideLabel(side) }}</h3>
          <div
            v-for="row in rowsForSide(side)"
            :key="`${side}-${row}`"
            class="clash__row"
            role="row"
            :aria-label="`${sideLabel(side)}, ряд ${row + 1}`"
            :style="{ gridTemplateColumns: `repeat(${session.plan.field.columns}, minmax(72px, 1fr))` }"
          >
            <button
              v-for="column in session.plan.field.columns"
              :key="`${side}-${row}-${column - 1}`"
              type="button"
              class="clash__cell"
              role="gridcell"
              :class="{
                'clash__cell--placeable': canPlaceAt(cell(side, row, column - 1)),
                'clash__cell--targeted': cellUnitIds(cell(side, row, column - 1)).includes(selectedActorId ?? ''),
              }"
              :data-testid="`clash-cell-${side}-${row}-${column - 1}`"
              :aria-label="cellLabel(cell(side, row, column - 1))"
              :aria-selected="cellUnitIds(cell(side, row, column - 1)).includes(selectedActorId ?? '')"
              :disabled="!cellInteractive(cell(side, row, column - 1))"
              @click="activateCell(cell(side, row, column - 1))"
              @keydown.enter.prevent="activateCell(cell(side, row, column - 1))"
              @keydown.space.prevent="activateCell(cell(side, row, column - 1))"
            >
              <span v-if="cell(side, row, column - 1).terrainId" class="clash__terrain">
                {{ cell(side, row, column - 1).terrainId }}
              </span>
              <span
                v-for="unit in cellUnits(cell(side, row, column - 1))"
                :key="unit.instanceId"
                class="clash__unit"
              >
                <b>{{ definitionFor(unit)?.name ?? unit.definitionId }}</b>
                <small>{{ Math.max(0, unit.hp) }}/{{ unit.maxHp }} ХП</small>
              </span>
              <span
                v-for="corpseId in cell(side, row, column - 1).corpseIds"
                :key="corpseId"
                class="clash__corpse"
                :aria-label="`Останки ${corpseId}`"
              >☠</span>
              <span v-if="cellUnitIds(cell(side, row, column - 1)).length === 0" class="clash__empty" aria-hidden="true">·</span>
            </button>
          </div>
        </section>
      </div>

      <aside class="clash__actions" aria-labelledby="clash-actions-title">
        <h3 id="clash-actions-title">Приказы</h3>
        <template v-if="selectedActor">
          <p><b>{{ definitionFor(selectedActor)?.name ?? selectedActor.definitionId }}</b></p>
          <p v-if="selectedAbilities.length === 0">У этого бойца нет активных умений.</p>
          <button
            v-for="ability in selectedAbilities"
            :key="ability.id"
            type="button"
            :aria-pressed="selectedAbilityId === ability.id"
            :disabled="state.phase !== 'between-clashes' || state.expectedSide !== selectedActor.side"
            @click="chooseAbility(ability)"
          >{{ ability.name }}</button>
          <small v-if="selectedAbilityId">Выберите цель на поле.</small>
        </template>
        <p v-else>Выберите выставленного бойца, чтобы увидеть активные умения.</p>

        <button
          v-if="state.phase === 'clash-ready'"
          type="button"
          data-testid="clash-resolve-round"
          @click="resolveRound"
        >Начать столкновение</button>
        <button
          v-if="state.phase === 'between-clashes'"
          type="button"
          :data-testid="`clash-end-between-${state.expectedSide}`"
          @click="endBetweenClash"
        >Завершить окно: {{ expectedSideLabel }}</button>

        <label v-if="qaMode" class="clash__qa-policy">
          QA-стратегия
          <select v-model="qaPolicy" data-testid="clash-qa-policy">
            <option v-for="policy in CLASH_QA_POLICIES" :key="policy" :value="policy">{{ policy }}</option>
          </select>
        </label>
        <button
          v-if="qaMode"
          type="button"
          data-testid="clash-qa-resolve"
          :disabled="emitted"
          @click="fastResolve"
        >QA: быстро завершить</button>
        <button type="button" data-testid="clash-abort" :disabled="emitted" @click="requestAbort">Отступить</button>
        <p v-if="state.error" class="clash__error" role="alert">{{ state.error }}</p>
      </aside>
    </div>

    <div class="clash__reports">
      <section aria-labelledby="clash-passives-title">
        <h3 id="clash-passives-title">Раскрытые скрытые пассивки</h3>
        <p v-if="revealedPassives.length === 0">Пока ничего не раскрыто.</p>
        <ul v-else>
          <li v-for="passive in revealedPassives" :key="passive.id">
            {{ passive.unitName }} — {{ passive.name }}
          </li>
        </ul>
      </section>
      <section aria-labelledby="clash-log-title">
        <h3 id="clash-log-title">Журнал ходов</h3>
        <p v-if="state.log.length === 0">Команд ещё не было.</p>
        <ol v-else data-testid="clash-turn-log">
          <li v-for="entry in state.log" :key="entry.sequence">
            <b>{{ entry.turn }}</b> · {{ entry.message }}
          </li>
        </ol>
      </section>
    </div>

    <p class="clash__text-state" data-testid="clash-text-state" aria-live="polite">
      {{ phaseLabel }}. Ход {{ state.turn }}. {{ sideLabel('attacker') }}: мораль {{ state.morale.attacker }},
      живых {{ Object.values(state.units).filter(unit => unit.side === 'attacker' && unit.alive).length }}.
      {{ sideLabel('defender') }}: мораль {{ state.morale.defender }},
      живых {{ Object.values(state.units).filter(unit => unit.side === 'defender' && unit.alive).length }}.
      Останков на поле: {{ state.corpses.length }}.
    </p>
  </section>
</template>

<style scoped>
.clash { display:grid; gap:14px; max-width:1380px; margin:0 auto; padding:18px; color:#f3e7cf; background:radial-gradient(circle at 50% 40%,rgba(126,52,35,.18),transparent 35%),linear-gradient(145deg,#201713,#101514); border:1px solid rgba(210,158,80,.32); border-radius:18px; box-shadow:0 24px 70px rgba(0,0,0,.38); }
.clash__header { display:flex; align-items:start; justify-content:space-between; gap:18px; }.clash__header span,.clash__header p { color:#b9aa90; }.clash__header h2 { margin:4px 0; font:700 2rem/1 Georgia,serif; }.clash__header p { margin:0; }
.clash__status { display:flex; gap:14px; margin:0; }.clash__status div { display:grid; gap:3px; min-width:90px; }.clash__status dt { color:#a99b83; font-size:.66rem; text-transform:uppercase; }.clash__status dd { margin:0; color:#efc56d; }
.clash__morale { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:8px; }.clash__morale article { display:flex; justify-content:space-between; padding:10px 13px; border:1px solid rgba(208,159,81,.23); background:rgba(255,255,255,.025); }.clash__morale span { color:#efc56d; font-weight:800; }
.clash__layout { display:grid; grid-template-columns:minmax(180px,240px) minmax(420px,1fr) minmax(190px,260px); gap:12px; align-items:start; }.clash__reserves,.clash__actions,.clash__reports section { display:grid; align-content:start; gap:8px; padding:12px; border:1px solid rgba(208,159,81,.2); background:rgba(12,17,15,.72); }.clash h3,.clash h4,.clash p { margin:0; }.clash__reserves section { display:grid; gap:6px; }.clash__reserves button { display:flex; justify-content:space-between; gap:8px; text-align:left; }
.clash button,.clash select { min-height:38px; padding:7px 10px; border:1px solid rgba(218,171,91,.35); border-radius:5px; color:inherit; background:rgba(78,48,28,.65); }.clash button { cursor:pointer; }.clash button:disabled { cursor:not-allowed; opacity:.42; }.clash button:focus-visible,.clash select:focus-visible { outline:3px solid #ffcf72; outline-offset:3px; }.clash button[aria-pressed="true"] { border-color:#ffd27d; background:#744622; }
.clash__board { display:grid; gap:11px; padding:12px; border:1px solid rgba(211,167,95,.28); background:linear-gradient(#24261f,#181e1a); }.clash__side { display:grid; gap:5px; }.clash__side h3 { color:#cfb37e; font-size:.72rem; letter-spacing:.08em; text-transform:uppercase; }.clash__row { display:grid; gap:5px; }.clash__cell { position:relative; display:grid; align-content:center; gap:3px; min-height:74px; overflow:hidden; text-align:center; background:rgba(41,46,37,.86) !important; }.clash__cell--placeable { border-color:#80bd73 !important; box-shadow:inset 0 0 0 1px rgba(128,189,115,.35); }.clash__cell--targeted { border-color:#f4c56b !important; }.clash__cell:disabled { opacity:.68 !important; }.clash__unit { display:grid; gap:2px; }.clash__unit b { font-size:.72rem; }.clash__unit small { color:#d6bd8d; }.clash__terrain { position:absolute; top:3px; left:4px; color:#91b987; font-size:.55rem; }.clash__corpse { color:#b88e77; }.clash__empty { color:rgba(243,231,207,.3); font-size:1.3rem; }
.clash__actions { min-height:220px; }.clash__actions > small { color:#d7bd8e; }.clash__qa-policy { display:grid; gap:4px; margin-top:6px; color:#baaa8e; font-size:.72rem; }.clash__error { color:#f0a18b; }
.clash__reports { display:grid; grid-template-columns:minmax(240px,.7fr) minmax(300px,1.3fr); gap:12px; }.clash__reports ul,.clash__reports ol { max-height:180px; margin:0; padding-left:22px; overflow:auto; }.clash__reports li { margin:4px 0; color:#cbbca2; }.clash__text-state { padding:10px 12px; color:#bcae96; background:rgba(0,0,0,.18); }
@media (max-width:980px) { .clash__layout { grid-template-columns:1fr; }.clash__reserves { grid-template-columns:repeat(2,minmax(0,1fr)); }.clash__reserves > h3 { grid-column:1/-1; } }
@media (max-width:620px) { .clash { padding:9px; }.clash__header,.clash__status { flex-direction:column; }.clash__status { display:grid; grid-template-columns:repeat(3,1fr); }.clash__reserves,.clash__reports { grid-template-columns:1fr; }.clash__row { overflow:auto; }.clash__cell { min-width:76px; } }
</style>
