<script setup lang="ts">
import { computed, watch, ref } from 'vue'

const props = withDefaults(defineProps<{
  value: number
  size?: 'sm' | 'lg'
  flashColor?: string | null
}>(), {
  size: 'lg',
  flashColor: null,
})

const digits = computed(() => {
  const str = String(Math.abs(Math.round(props.value)))
  return str.split('').map(Number)
})

const isNegative = computed(() => props.value < 0)

// Track previous digits to animate transitions
const prevDigits = ref<number[]>([])
const animating = ref(false)

watch(() => props.value, (newVal, oldVal) => {
  if (oldVal !== undefined && newVal !== oldVal) {
    prevDigits.value = String(Math.abs(Math.round(oldVal))).split('').map(Number)
    animating.value = true
    setTimeout(() => { animating.value = false }, 700)
  }
}, { immediate: false })

const flashActive = ref(false)
watch(() => props.flashColor, (color) => {
  if (color) {
    flashActive.value = true
    setTimeout(() => { flashActive.value = false }, 800)
  }
})
</script>

<template>
  <span
    class="odometer"
    :class="[`odo-${size}`, { 'odo-flash': flashActive }]"
    :style="flashActive && flashColor ? { '--odo-flash-color': flashColor } : {}"
  >
    <span v-if="isNegative" class="odo-sign">-</span>
    <span
      v-for="(d, i) in digits"
      :key="i"
      class="odo-digit-wrap"
    >
      <span
        class="odo-digit"
        :class="{ 'odo-rolling': animating }"
        :style="{
          transform: `translateY(${-d}em)`,
          transitionDelay: `${(digits.length - 1 - i) * 30}ms`,
        }"
      >
        <span v-for="n in 10" :key="n - 1" class="odo-num">{{ n - 1 }}</span>
      </span>
    </span>
  </span>
</template>

<style scoped>
.odometer {
  display: inline-flex;
  align-items: center;
  overflow: hidden;
  font-variant-numeric: tabular-nums;
  font-weight: 900;
  line-height: 1;
}

.odo-lg {
  font-size: 28px;
  height: 1.15em;
}

.odo-sm {
  font-size: 14px;
  height: 1.15em;
}

.odo-sign {
  margin-right: 1px;
}

.odo-digit-wrap {
  display: inline-block;
  width: 0.65em;
  height: 1em;
  overflow: hidden;
  position: relative;
}

.odo-digit {
  display: flex;
  flex-direction: column;
  transition: transform 0.6s cubic-bezier(0.23, 1, 0.32, 1);
  will-change: transform;
}

.odo-digit.odo-rolling {
  transition: transform 0.6s cubic-bezier(0.23, 1, 0.32, 1);
}

.odo-num {
  display: block;
  height: 1em;
  text-align: center;
}

.odo-flash {
  animation: odo-color-flash 0.8s ease-out;
}

@keyframes odo-color-flash {
  0% { color: var(--odo-flash-color, inherit); text-shadow: 0 0 8px var(--odo-flash-color, transparent); }
  100% { color: inherit; text-shadow: none; }
}
</style>
