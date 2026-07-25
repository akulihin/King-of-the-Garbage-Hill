<script setup lang="ts">
import { onMounted, ref } from 'vue'
import ClashBattle from '../components/empires-endgame/ClashBattle.vue'
import { loadBundledEmpiresConfig } from '../features/empires-endgame/config'
import {
  abortClash,
  createClashRulesIdentity,
} from '../features/empires-endgame/clash/engine'
import {
  CLASH_QA_POLICIES,
  createClashQaPlan,
  digestClashQaResult,
  type ClashQaPolicy,
} from '../features/empires-endgame/clash/qa'
import type {
  ClashCommand,
  ClashResult,
  EmpiresClashMinigameSession,
  EmpiresEndgameConfig,
} from '../features/empires-endgame/types'

const DEFAULT_SEED = 'clash-standalone'

const config = ref<EmpiresEndgameConfig | null>(null)
const session = ref<EmpiresClashMinigameSession | null>(null)
const result = ref<ClashResult | null>(null)
const seedInput = ref(DEFAULT_SEED)
const initialPolicy = ref<ClashQaPolicy>('balanced')
const loading = ref(true)
const error = ref('')
const battleKey = ref(0)

function isClashQaPolicy(value: string | null): value is ClashQaPolicy {
  return value !== null && CLASH_QA_POLICIES.some(policy => policy === value)
}

function normalizedSeed(): string {
  return seedInput.value.trim() || DEFAULT_SEED
}

function startBattle(): void {
  if (!config.value) return
  const seed = normalizedSeed()
  seedInput.value = seed
  const sessionId = 'clash-standalone-session'
  const planId = 'clash-standalone-plan'
  const rulesIdentity = createClashRulesIdentity(
    config.value.schemaVersion,
    config.value.clash,
    { standaloneLab: true },
  )
  const plan = createClashQaPlan(
    config.value.clash,
    seed,
    sessionId,
    rulesIdentity,
    planId,
  )
  session.value = {
    id: sessionId,
    sequence: 1,
    kind: 'clash',
    plan,
    rulesIdentity,
    seed,
    turnLog: [],
    attempt: 0,
    origin: {
      returnPhase: 'cards',
      context: { kind: 'manual', sourceId: 'standalone-clash-lab' },
    },
  }
  result.value = null
  error.value = ''
  battleKey.value += 1
}

function recordProgress(turnLog: ClashCommand[]): void {
  if (!session.value) return
  session.value = { ...session.value, turnLog: [...turnLog] }
}

function finishBattle(nextResult: ClashResult): void {
  if (session.value) {
    session.value = { ...session.value, turnLog: [...nextResult.turnLog] }
  }
  result.value = nextResult
}

function abortBattle(turnLog: ClashCommand[], turn: number): void {
  if (!session.value) return
  finishBattle(abortClash(session.value.plan, session.value.seed, turnLog, turn))
}

function sideLabel(side: ClashResult['winner']): string {
  if (side === 'attacker') return 'Атакующие'
  if (side === 'defender') return 'Защитники'
  return 'Нет победителя'
}

async function boot(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    const query = new URLSearchParams(window.location.search)
    seedInput.value = query.get('seed')?.trim() || DEFAULT_SEED
    const requestedPolicy = query.get('policy')
    if (isClashQaPolicy(requestedPolicy)) initialPolicy.value = requestedPolicy
    config.value = await loadBundledEmpiresConfig()
    startBattle()
  }
  catch (cause) {
    error.value = cause instanceof Error ? cause.message : 'Клэш не удалось запустить.'
  }
  finally {
    loading.value = false
  }
}

onMounted(() => {
  void boot()
})
</script>

<template>
  <main class="clash-lab" data-testid="clash-standalone">
    <header class="clash-lab__hero">
      <div>
        <span class="clash-lab__eyebrow">Standalone battle lab</span>
        <h1>Клэш — отдельный полигон</h1>
        <p>
          Полноценный движок Клэша без кампании Empire's Endgame. Управляйте обеими
          сторонами вручную или завершите бой одной из детерминированных QA-стратегий.
          Сохранения кампании здесь не читаются и не изменяются.
        </p>
      </div>
      <form class="clash-lab__controls" data-testid="clash-standalone-controls" @submit.prevent="startBattle">
        <label>
          Seed боя
          <input v-model="seedInput" data-testid="clash-standalone-seed" autocomplete="off">
        </label>
        <button type="submit" :disabled="loading || !config">Новый бой</button>
      </form>
    </header>

    <p v-if="loading" class="clash-lab__notice" role="status">Загрузка правил Клэша…</p>
    <p v-else-if="error" class="clash-lab__notice clash-lab__notice--error" role="alert">{{ error }}</p>

    <section
      v-else-if="result"
      class="clash-lab__result"
      data-testid="clash-standalone-result"
      aria-labelledby="clash-result-title"
    >
      <span>{{ result.terminalReason }}</span>
      <h2 id="clash-result-title">
        {{ result.outcome === 'aborted' ? 'Бой прерван' : `${sideLabel(result.winner)} победили` }}
      </h2>
      <dl>
        <div><dt>Ходов</dt><dd>{{ result.turns }}</dd></div>
        <div><dt>Столкновений</dt><dd>{{ result.clashes }}</dd></div>
        <div><dt>Команд</dt><dd>{{ result.turnLog.length }}</dd></div>
        <div><dt>Digest</dt><dd data-testid="clash-standalone-digest">{{ digestClashQaResult(result) }}</dd></div>
      </dl>
      <button type="button" data-testid="clash-standalone-restart" @click="startBattle">Повторить бой</button>
    </section>

    <ClashBattle
      v-else-if="session"
      :key="battleKey"
      :session="session"
      qa-mode
      :qa-policy="initialPolicy"
      @progress="recordProgress"
      @resolve="finishBattle"
      @abort="abortBattle"
    />
  </main>
</template>

<style scoped>
.clash-lab {
  display: grid;
  gap: 18px;
  min-height: 100%;
  padding: 24px;
  color: #f3e7cf;
  background:
    radial-gradient(circle at 18% 0%, rgba(170, 91, 42, .18), transparent 38%),
    linear-gradient(155deg, #17120f, #0b1110 60%, #111814);
}

.clash-lab__hero {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 24px;
  width: min(1380px, 100%);
  margin: 0 auto;
  padding: 20px 22px;
  border: 1px solid rgba(210, 158, 80, .32);
  border-radius: 16px;
  background: rgba(13, 16, 14, .82);
}

.clash-lab__hero h1 {
  margin: 5px 0 8px;
  font: 700 clamp(1.65rem, 3vw, 2.45rem)/1.05 Georgia, serif;
}

.clash-lab__hero p {
  max-width: 760px;
  margin: 0;
  color: #b9aa90;
}

.clash-lab__eyebrow {
  color: #efc56d;
  font-size: .72rem;
  font-weight: 800;
  letter-spacing: .14em;
  text-transform: uppercase;
}

.clash-lab__controls {
  display: flex;
  align-items: end;
  gap: 9px;
}

.clash-lab__controls label {
  display: grid;
  gap: 5px;
  color: #baaa8e;
  font-size: .76rem;
}

.clash-lab input,
.clash-lab button {
  min-height: 42px;
  padding: 8px 12px;
  border: 1px solid rgba(218, 171, 91, .38);
  border-radius: 6px;
  color: #f3e7cf;
  background: rgba(78, 48, 28, .72);
}

.clash-lab input {
  width: min(260px, 45vw);
  background: rgba(8, 12, 10, .9);
}

.clash-lab button {
  cursor: pointer;
}

.clash-lab button:disabled {
  cursor: not-allowed;
  opacity: .48;
}

.clash-lab input:focus-visible,
.clash-lab button:focus-visible {
  outline: 3px solid #ffcf72;
  outline-offset: 3px;
}

.clash-lab__notice,
.clash-lab__result {
  width: min(720px, 100%);
  margin: 30px auto;
  padding: 22px;
  border: 1px solid rgba(210, 158, 80, .32);
  border-radius: 14px;
  background: rgba(13, 16, 14, .88);
}

.clash-lab__notice--error {
  color: #f0a18b;
  border-color: rgba(240, 161, 139, .48);
}

.clash-lab__result {
  display: grid;
  gap: 14px;
  text-align: center;
}

.clash-lab__result > span {
  color: #efc56d;
  font-size: .72rem;
  letter-spacing: .12em;
  text-transform: uppercase;
}

.clash-lab__result h2 {
  margin: 0;
  font: 700 2rem/1.1 Georgia, serif;
}

.clash-lab__result dl {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 8px;
  margin: 0;
}

.clash-lab__result dl div {
  display: grid;
  gap: 5px;
  padding: 10px;
  border: 1px solid rgba(210, 158, 80, .2);
  background: rgba(255, 255, 255, .025);
}

.clash-lab__result dt {
  color: #a99b83;
  font-size: .68rem;
  text-transform: uppercase;
}

.clash-lab__result dd {
  min-width: 0;
  margin: 0;
  overflow-wrap: anywhere;
}

@media (max-width: 760px) {
  .clash-lab {
    padding: 10px;
  }

  .clash-lab__hero,
  .clash-lab__controls {
    align-items: stretch;
    flex-direction: column;
  }

  .clash-lab input {
    width: 100%;
  }

  .clash-lab__result dl {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
