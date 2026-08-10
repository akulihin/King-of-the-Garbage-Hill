<script setup lang="ts">
import { computed, ref } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'
import { message } from 'src/platform/localization'
import { renderIcon } from './battleship-icons'
import BsIcon from './BsIcon.vue'

const store = useBattleshipStore()
const submittingId = ref<string | null>(null)

const flagshipOptions = computed(() => [
  {
    id: 'flint_fortune',
    name: message('battleship.flint.fortune.name'),
    description: message('battleship.flint.fortune.description'),
    accent: 'fortune',
  },
  {
    id: 'flint_freedom',
    name: message('battleship.flint.freedom.name'),
    description: message('battleship.flint.freedom.description'),
    accent: 'freedom',
  },
])

async function chooseFlagship(definitionId: string) {
  if (submittingId.value) return
  const option = flagshipOptions.value.find(value => value.id === definitionId)
  if (!option) return
  const definition = store.shipCatalog.find(value => value.id === definitionId)
  submittingId.value = definitionId
  try {
    await store.selectFleet([{
      definitionId,
      shipName: definition?.nameRu || definition?.name || option.name,
      cost: definition?.cost ?? 0,
      upgrades: [],
    }])
  }
  finally {
    submittingId.value = null
  }
}
</script>

<template>
  <div class="flint-builder">
    <div class="flint-heading">
      <span class="bs-kicker">
        <BsIcon icon="flag" :size="13" />
        {{ message('battleship.flint.fleet.kicker') }}
      </span>
      <h3 class="bs-title">{{ message('battleship.flint.fleet.title') }}</h3>
      <p>{{ message('battleship.flint.fleet.hint') }}</p>
    </div>

    <div class="flagship-options">
      <article
        v-for="option in flagshipOptions"
        :key="option.id"
        class="bs-card flagship-card"
        :class="`flagship-card--${option.accent}`"
      >
        <span class="flagship-cannon" v-html="renderIcon('cannon', 38)" />
        <h4>{{ option.name }}</h4>
        <p>{{ option.description }}</p>
        <button
          type="button"
          class="bs-btn bs-btn--primary bs-btn--lg"
          :disabled="!!submittingId"
          @click="chooseFlagship(option.id)"
        >
          {{ message('battleship.flint.fleet.choose', { ship: option.name }) }}
        </button>
      </article>
    </div>
  </div>
</template>

<style scoped>
.flint-builder {
  max-width: 860px;
  margin: 1rem auto;
}
.flint-heading {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.45rem;
  margin-bottom: 1rem;
  text-align: center;
}
.flint-heading h3,
.flint-heading p {
  margin: 0;
}
.flint-heading p {
  max-width: 650px;
  color: var(--text-muted);
  font-size: 0.78rem;
}
.flagship-options {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}
.flagship-card {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  min-height: 285px;
  padding: 1.25rem;
  overflow: hidden;
  text-align: center;
}
.flagship-card::before {
  content: '';
  position: absolute;
  inset: 0;
  pointer-events: none;
  opacity: 0.13;
}
.flagship-card--fortune::before {
  background: radial-gradient(circle at 50% 0%, var(--accent-gold), transparent 64%);
}
.flagship-card--freedom::before {
  background: radial-gradient(circle at 50% 0%, var(--accent-blue), transparent 64%);
}
.flagship-cannon {
  position: relative;
  display: inline-flex;
  margin-bottom: 0.55rem;
  color: var(--accent-gold);
  filter: drop-shadow(0 0 9px color-mix(in srgb, var(--accent-gold) 48%, transparent));
}
.flagship-card--freedom .flagship-cannon {
  color: var(--accent-blue);
  filter: drop-shadow(0 0 9px color-mix(in srgb, var(--accent-blue) 48%, transparent));
}
.flagship-card h4 {
  position: relative;
  margin: 0 0 0.55rem;
  color: var(--text-primary);
  font-size: 1.35rem;
}
.flagship-card p {
  position: relative;
  flex: 1;
  margin: 0 0 1rem;
  color: var(--text-secondary);
  font-size: 0.78rem;
  line-height: 1.55;
}
.flagship-card button {
  position: relative;
  width: 100%;
}
@media (max-width: 680px) {
  .flagship-options { grid-template-columns: 1fr; }
}
</style>
