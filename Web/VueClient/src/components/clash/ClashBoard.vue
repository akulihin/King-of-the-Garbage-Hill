<script setup lang="ts">
import { computed } from 'vue'
import ClashUnit from './ClashUnit.vue'
import {
  fieldCellKey,
  type ClashBoardCell,
  type ClashPlayerState,
  type ClashUnitDefinition,
  type ClashUnitState,
  type ClashVisualUnitOverride,
} from 'src/features/clash/types'

const props = withDefaults(defineProps<{
  width: number
  length: number
  cells: ClashBoardCell[]
  catalogById: Map<string, ClashUnitDefinition>
  viewer?: ClashPlayerState | null
  selectableKeys?: Set<string>
  visualOverrides?: Map<string, ClashVisualUnitOverride>
  selectedUnitId?: string | null
  label?: string
}>(), {
  viewer: null,
  selectableKeys: () => new Set<string>(),
  visualOverrides: () => new Map<string, ClashVisualUnitOverride>(),
  selectedUnitId: null,
  label: 'Поле боя',
})

const emit = defineEmits<{
  'cell-click': [payload: {
    boardRow: number
    column: number
    unit: ClashUnitState | null
  }]
}>()

const cellStateByKey = computed(() => {
  const result = new Map<string, ClashBoardCell>()
  for (const cell of props.cells) result.set(fieldCellKey(cell.boardRow, cell.column), cell)
  return result
})

const displayedUnits = computed(() => {
  const result = new Map<string, ClashUnitState>()
  const finalById = new Map<string, ClashUnitState>()
  for (const cell of props.cells) {
    if (cell.unit) finalById.set(cell.unit.instanceId, cell.unit)
  }

  if (props.visualOverrides.size > 0) {
    for (const [unitId, visual] of props.visualOverrides) {
      const source = finalById.get(unitId) ?? visual.snapshot
      const key = fieldCellKey(visual.boardRow, visual.column)
      const presented = {
        ...source,
        hp: visual.hp,
        alive: visual.alive,
        boardRow: visual.boardRow,
        column: visual.column,
        // Effective speed can change after casualties (for example, Легион).
        // Keep the pre-clash value until the authoritative timeline completes.
        speed: visual.snapshot.speed,
        shieldCharges: visual.shieldCharges,
        dodgeCharges: visual.dodgeCharges,
        bleedStacks: visual.bleedStacks,
      }
      const occupant = result.get(key)
      if (!occupant || presented.alive || !occupant.alive) result.set(key, presented)
    }
    for (const [unitId, unit] of finalById) {
      if (!props.visualOverrides.has(unitId)) {
        result.set(fieldCellKey(unit.boardRow, unit.column), unit)
      }
    }
    return result
  }

  for (const unit of finalById.values()) {
    result.set(fieldCellKey(unit.boardRow, unit.column), unit)
  }
  return result
})

const orderedRows = computed(() => {
  const rows = Array.from({ length: props.length * 2 }, (_, index) => index)
  return props.viewer?.isHost ? rows.reverse() : rows
})

function rowSide(boardRow: number): 'host' | 'guest' {
  return boardRow < props.length ? 'host' : 'guest'
}

function isOwnRow(boardRow: number) {
  if (!props.viewer) return false
  return props.viewer.isHost ? boardRow < props.length : boardRow >= props.length
}

function cellAria(boardRow: number, column: number, unit: ClashUnitState | null, hidden: boolean) {
  const coordinate = `ряд ${boardRow + 1}, колонка ${column + 1}`
  if (hidden) return `${coordinate}: скрыто`
  if (unit) {
    const definition = props.catalogById.get(unit.definitionId)
    return `${coordinate}: ${definition?.name ?? unit.name ?? unit.definitionId}`
  }
  return `${coordinate}: пусто`
}
</script>

<template>
  <section class="clash-board-shell" :aria-label="label">
    <div class="clash-board__legend">
      <span class="is-enemy">Вражеский тыл</span>
      <span class="is-front">Линия столкновения</span>
      <span class="is-own">Ваш тыл</span>
    </div>
    <div class="clash-board__scroll">
      <div
        class="clash-board"
        :style="{ '--clash-columns': width }"
      >
        <div class="clash-board__columns" aria-hidden="true">
          <span v-for="column in width" :key="column">{{ column }}</span>
        </div>
        <div class="clash-board__grid">
          <template v-for="boardRow in orderedRows" :key="boardRow">
            <button
              v-for="columnIndex in width"
              :key="fieldCellKey(boardRow, columnIndex - 1)"
              type="button"
              class="clash-cell"
              :class="[
                `is-${rowSide(boardRow)}`,
                {
                  'is-own': isOwnRow(boardRow),
                  'is-front-edge': boardRow === length - 1 || boardRow === length,
                  'is-selectable': selectableKeys.has(fieldCellKey(boardRow, columnIndex - 1)),
                  'is-hidden': cellStateByKey.get(fieldCellKey(boardRow, columnIndex - 1))?.isHidden,
                },
              ]"
              :disabled="!selectableKeys.has(fieldCellKey(boardRow, columnIndex - 1))"
              :aria-label="cellAria(
                boardRow,
                columnIndex - 1,
                displayedUnits.get(fieldCellKey(boardRow, columnIndex - 1)) ?? null,
                cellStateByKey.get(fieldCellKey(boardRow, columnIndex - 1))?.isHidden ?? false,
              )"
              :data-board-row="boardRow"
              :data-column="columnIndex - 1"
              @click="emit('cell-click', {
                boardRow,
                column: columnIndex - 1,
                unit: displayedUnits.get(fieldCellKey(boardRow, columnIndex - 1)) ?? null,
              })"
            >
              <span
                v-if="cellStateByKey.get(fieldCellKey(boardRow, columnIndex - 1))?.isHidden"
                class="clash-cell__fog"
                aria-hidden="true"
              >?</span>
              <ClashUnit
                :key="`${displayedUnits.get(fieldCellKey(boardRow, columnIndex - 1))!.instanceId}:${visualOverrides.get(displayedUnits.get(fieldCellKey(boardRow, columnIndex - 1))!.instanceId)?.animationSequence ?? 0}`"
                v-else-if="displayedUnits.get(fieldCellKey(boardRow, columnIndex - 1))"
                :unit="displayedUnits.get(fieldCellKey(boardRow, columnIndex - 1))"
                :definition="catalogById.get(displayedUnits.get(fieldCellKey(boardRow, columnIndex - 1))!.definitionId)"
                :visual-override="visualOverrides.get(displayedUnits.get(fieldCellKey(boardRow, columnIndex - 1))!.instanceId)"
                :selected="selectedUnitId === displayedUnits.get(fieldCellKey(boardRow, columnIndex - 1))!.instanceId"
                compact
              />
              <span v-else class="clash-cell__empty" aria-hidden="true">·</span>
            </button>
          </template>
        </div>
      </div>
    </div>
  </section>
</template>
