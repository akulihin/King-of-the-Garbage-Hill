<script setup lang="ts">
const props = withDefaults(defineProps<{
  type: 'victory' | 'defeat' | 'tie'
  size?: number
  animate?: boolean
}>(), {
  size: 28,
  animate: true,
})
</script>

<template>
  <div
    class="wax-seal"
    :class="[props.type, { 'seal-animate': props.animate }]"
    :style="{ width: props.size + 'px', height: props.size + 'px', '--seal-size': props.size }"
  >
    <div class="seal-inner">
      <span v-if="props.type === 'victory'" class="seal-icon">&#x2713;</span>
      <span v-else-if="props.type === 'defeat'" class="seal-icon">&#x2717;</span>
      <span v-else class="seal-icon">=</span>
    </div>
  </div>
</template>

<style scoped>
.wax-seal {
  /* 20-point scalloped circle */
  clip-path: polygon(
    50% 0%, 59% 3%, 65% 9%, 72% 5%, 76% 12%,
    84% 10%, 86% 18%, 93% 19%, 92% 27%, 98% 31%,
    95% 39%, 100% 45%, 96% 52%, 100% 58%, 95% 64%,
    98% 72%, 92% 75%, 93% 83%, 86% 83%, 84% 91%,
    76% 89%, 72% 96%, 65% 92%, 59% 97%, 50% 100%,
    41% 97%, 35% 92%, 28% 96%, 24% 89%, 16% 91%,
    14% 83%, 7% 83%, 8% 75%, 2% 72%, 5% 64%,
    0% 58%, 4% 52%, 0% 45%, 5% 39%, 2% 31%,
    8% 27%, 7% 19%, 14% 18%, 16% 10%, 24% 12%,
    28% 5%, 35% 9%, 41% 3%
  );
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.wax-seal.victory {
  background: radial-gradient(circle at 40% 35%, var(--seal-green-light), var(--seal-green));
}
.wax-seal.defeat {
  background: radial-gradient(circle at 40% 35%, var(--seal-red-light), var(--seal-red));
}
.wax-seal.tie {
  background: radial-gradient(circle at 40% 35%, #a0884c, var(--seal-orange));
}

.seal-inner {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 60%;
  height: 60%;
}

.seal-icon {
  font-weight: 900;
  line-height: 1;
  color: rgba(255, 255, 255, 0.85);
  font-size: 0.5em;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.4);
}

/* Use the parent's font-size for the seal icon scaling */
.wax-seal { font-size: calc(var(--seal-size, 28) * 1px); }
.seal-icon { font-size: calc(var(--seal-size, 28) * 0.45px); }

.seal-animate {
  animation: seal-stamp 0.35s cubic-bezier(0.34, 1.56, 0.64, 1);
}

@keyframes seal-stamp {
  0% { transform: scale(2.5) rotate(-15deg); opacity: 0; }
  50% { transform: scale(0.9) rotate(3deg); opacity: 1; }
  100% { transform: scale(1) rotate(0deg); opacity: 1; }
}
</style>
