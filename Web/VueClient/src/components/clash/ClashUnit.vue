<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type {
  ClashUnitDefinition,
  ClashUnitState,
  ClashVisualUnitOverride,
} from 'src/features/clash/types'
import { clashUnitArtUrl, clashUnitInitials } from 'src/features/clash/visuals'

const props = withDefaults(defineProps<{
  unit?: ClashUnitState | null
  definition?: ClashUnitDefinition | null
  visualOverride?: ClashVisualUnitOverride | null
  compact?: boolean
  selected?: boolean
  concealed?: boolean
}>(), {
  unit: null,
  definition: null,
  visualOverride: null,
  compact: false,
  selected: false,
  concealed: false,
})

const imageFailed = ref(false)
const definitionId = computed(() => props.unit?.definitionId ?? props.definition?.id ?? 'unknown')
const name = computed(() => props.unit?.name ?? props.definition?.name ?? definitionId.value)
const hp = computed(() => props.visualOverride?.hp ?? props.unit?.hp ?? props.definition?.maxHp ?? 0)
const maxHp = computed(() => props.unit?.maxHp ?? props.definition?.maxHp ?? 0)
const attack = computed(() => props.unit?.attack ?? props.definition?.attack ?? 0)
const speed = computed(() => props.unit?.speed ?? props.definition?.speed ?? 0)
const alive = computed(() => props.visualOverride?.alive ?? props.unit?.alive ?? true)
const animation = computed(() => props.visualOverride?.animation ?? 'idle')
const hpPercent = computed(() => maxHp.value > 0 ? Math.max(0, Math.min(100, hp.value / maxHp.value * 100)) : 0)
const artUrl = computed(() => clashUnitArtUrl(definitionId.value))
const unitClass = computed(() => `unit-${definitionId.value.toLowerCase().replace(/[^a-z0-9-]/g, '')}`)
const initials = computed(() => clashUnitInitials({
  name: name.value,
}))

watch(definitionId, () => {
  imageFailed.value = false
})
</script>

<template>
  <div
    class="clash-unit"
    :class="[
      `is-${animation}`,
      unitClass,
      {
        'is-compact': compact,
        'is-selected': selected,
        'is-dead': !alive,
        'is-concealed': concealed,
      },
    ]"
    :aria-label="concealed ? 'Скрытый вражеский юнит' : `${name}: атака ${attack}, здоровье ${hp} из ${maxHp}, скорость ${speed}`"
  >
    <template v-if="concealed">
      <div class="clash-unit__silhouette" aria-hidden="true">?</div>
      <span class="clash-unit__name">Скрыто</span>
    </template>
    <template v-else>
      <div class="clash-unit__portrait">
        <img
          v-if="!imageFailed"
          :src="artUrl"
          :alt="name"
          draggable="false"
          @error="imageFailed = true"
        />
        <span v-else class="clash-unit__fallback" aria-hidden="true">{{ initials }}</span>
        <span v-if="unit?.shieldCharges" class="clash-unit__ward" title="Щит">
          ◇{{ unit.shieldCharges }}
        </span>
        <span
          v-if="unit?.dodgeCharges"
          class="clash-unit__ward clash-unit__ward--dodge"
          title="Увороты"
        >
          〽{{ unit.dodgeCharges }}
        </span>
      </div>
      <span class="clash-unit__name" :title="name">{{ name }}</span>
      <div class="clash-unit__stats" aria-hidden="true">
        <span class="is-attack">⚔ {{ attack }}</span>
        <span class="is-hp">♥ {{ hp }}</span>
        <span class="is-speed">➤ {{ speed }}</span>
      </div>
      <div class="clash-unit__health" aria-hidden="true">
        <span :style="{ width: `${hpPercent}%` }" />
      </div>
      <div v-if="unit?.bleedStacks" class="clash-unit__status" title="Кровотечение">
        Кровь ×{{ unit.bleedStacks }}
      </div>
    </template>
  </div>
</template>
