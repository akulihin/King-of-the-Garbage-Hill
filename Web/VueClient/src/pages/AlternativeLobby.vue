<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { Bot, Check, Clock3, Dices, LogOut, Plus, ShieldCheck, Users } from 'lucide-vue-next'
import { useGameStore } from 'src/store/game'
import { signalrService, type AlternativeLobbySeat } from 'src/services/signalr'
import { message } from 'src/platform/localization/messages'

const props = defineProps<{ lobbyId: string | number }>()
const lobbyId = Number(props.lobbyId)
const store = useGameStore()
const router = useRouter()
const now = ref(Date.now())
const busy = ref(false)
const openPassiveSlot = ref<number | null>(null)
let timer: ReturnType<typeof setInterval> | null = null

const state = computed(() => store.alternativeLobbyState?.lobbyId === lobbyId
  ? store.alternativeLobbyState
  : null)
const ownSeat = computed(() => state.value?.seats.find(seat => seat.discordId === store.discordId) ?? null)
const teams = computed(() => {
  if (!state.value || state.value.stage !== 'Team') return []
  const count = state.value.teamSize === 2 ? 3 : 2
  return Array.from({ length: count }, (_, index) => ({
    teamId: index + 1,
    seats: state.value!.seats.filter(seat => seat.teamId === index + 1),
  }))
})
const confirmedAllies = computed(() => {
  if (!state.value || !ownSeat.value || state.value.mode === 'Aram') return []
  return state.value.seats.filter(seat =>
    seat.teamId === ownSeat.value!.teamId
    && seat.discordId !== ownSeat.value!.discordId
    && seat.selectedCharacter)
})
const secondsLeft = computed(() => {
  if (!state.value?.deadlineUtc) return 0
  return Math.max(0, Math.ceil((Date.parse(state.value.deadlineUtc) - now.value) / 1000))
})
const countdown = computed(() => {
  const minutes = Math.floor(secondsLeft.value / 60)
  const seconds = secondsLeft.value % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
})
const modeTitle = computed(() => {
  if (state.value?.mode === 'Aram') return message('kotgh.alternative.aram')
  if (state.value?.mode === 'TeamAram') return message('kotgh.alternative.teamAram')
  return message('kotgh.alternative.team')
})
const stageTitle = computed(() => {
  if (state.value?.stage === 'Character') return message('kotgh.alternative.characterStage')
  if (state.value?.stage === 'Aram') return message('kotgh.alternative.aramStage')
  return message('kotgh.alternative.teamStage')
})
const allPassivesSelected = computed(() =>
  state.value?.aramBuild?.passiveSlots.every(slot => slot.selectedIndex >= 0) ?? false)

function seatCanMove(seat: AlternativeLobbySeat): boolean {
  return state.value?.stage === 'Team' && seat.kind === 'empty' && !busy.value
}

async function run(action: () => Promise<void>) {
  if (busy.value) return
  busy.value = true
  try { await action() }
  finally { busy.value = false }
}

async function moveTo(slotIndex: number) {
  if (!seatCanMove(state.value!.seats[slotIndex])) return
  await run(() => store.alternativeLobbyMove(lobbyId, slotIndex))
}

async function toggleTeamReady() {
  if (!ownSeat.value) return
  await run(() => store.alternativeLobbySetTeamReady(lobbyId, !ownSeat.value!.ready))
}

async function selectCharacter(name: string) {
  await run(() => store.alternativeLobbySelectCharacter(lobbyId, name))
}

async function selectPassive(slotIndex: number, passiveName: string) {
  await run(() => store.alternativeLobbySelectPassive(lobbyId, slotIndex, passiveName))
}

async function unlockPassive(slotIndex: number) {
  await run(() => store.alternativeLobbyUnlockPassive(lobbyId, slotIndex))
}

async function toggleAramReady() {
  if (!ownSeat.value) return
  await run(() => store.alternativeLobbySetAramReady(lobbyId, !ownSeat.value!.ready))
}

async function leaveLobby() {
  await run(() => store.leaveAlternativeLobby(lobbyId))
  await router.push('/games')
}

onMounted(async () => {
  signalrService.onAlternativeLobbyGameStarted = ({ gameId }) => {
    store.alternativeLobbyState = null
    void router.push(`/game/${gameId}`)
  }
  await store.joinAlternativeLobby(lobbyId)
  await store.requestAlternativeLobbyState(lobbyId)
  timer = setInterval(() => {
    now.value = Date.now()
    if (store.isConnected) void store.requestAlternativeLobbyState(lobbyId)
  }, 1000)
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
  signalrService.onAlternativeLobbyGameStarted = null
})
</script>

<template>
  <main class="alt-lobby">
    <header class="alt-header">
      <div>
        <span class="eyebrow">{{ message('kotgh.alternative.preparation') }} #{{ lobbyId }}</span>
        <h1>{{ modeTitle }}</h1>
        <p>{{ stageTitle }}</p>
      </div>
      <div class="timer-card" :class="{ urgent: secondsLeft <= 60 }">
        <Clock3 :size="20" />
        <div>
          <small>{{ message('kotgh.alternative.sharedTimer') }}</small>
          <strong>{{ countdown }}</strong>
        </div>
      </div>
    </header>

    <section v-if="!state" class="loading-card card">
      <Dices class="loading-die" :size="28" />
      <span>{{ message('kotgh.alternative.preparing') }}…</span>
    </section>

    <template v-else>
      <section class="settings-strip card">
        <div v-if="state.mode !== 'Aram'">
          <span>{{ message('kotgh.alternative.teamFormat') }}</span>
          <div class="segmented">
            <button
              v-for="size in ([2, 3] as const)"
              :key="size"
              type="button"
              :class="{ active: state.teamSize === size }"
              :disabled="!state.isOwner || state.stage !== 'Team' || busy"
              @click="store.alternativeLobbySetTeamSize(lobbyId, size)"
            >{{ size === 2 ? '2v2v2' : '3v3' }}</button>
          </div>
        </div>
        <div>
          <span>{{ message('kotgh.alternative.botDifficulty') }}</span>
          <div class="segmented difficulty">
            <button
              v-for="difficulty in [1, 2, 3, 4]"
              :key="difficulty"
              type="button"
              :class="{ active: state.aiDifficulty === difficulty }"
              :disabled="!state.isOwner || (state.stage !== 'Team' && state.mode !== 'Aram') || busy"
              @click="store.alternativeLobbySetAiDifficulty(lobbyId, difficulty)"
            >L{{ difficulty }}</button>
          </div>
        </div>
      </section>

      <section v-if="state.stage === 'Team'" class="stage-panel">
        <p class="stage-hint"><Users :size="17" /> {{ message('kotgh.alternative.moveHint') }}</p>
        <div class="teams-grid" :class="`teams-${teams.length}`">
          <article v-for="team in teams" :key="team.teamId" class="team-card card">
            <header>
              <span>TEAM</span>
              <strong>{{ team.teamId }}</strong>
              <small>{{ team.seats.filter(seat => seat.kind !== 'empty').length }} / {{ state.teamSize }}</small>
            </header>
            <button
              v-for="seat in team.seats"
              :key="seat.slotIndex"
              type="button"
              class="team-seat"
              :class="{
                empty: seat.kind === 'empty',
                mine: seat.discordId === store.discordId,
                ready: seat.ready,
              }"
              :disabled="!seatCanMove(seat)"
              @click="moveTo(seat.slotIndex)"
            >
              <span class="seat-index">{{ seat.slotIndex + 1 }}</span>
              <template v-if="seat.kind === 'empty'">
                <Plus :size="18" />
                <span>{{ message('kotgh.alternative.emptySeat') }}</span>
              </template>
              <template v-else>
                <Bot v-if="seat.kind === 'bot'" :size="18" />
                <Users v-else :size="18" />
                <strong>{{ seat.username || message('kotgh.alternative.bot') }}</strong>
                <small>{{ seat.ready ? message('kotgh.alternative.ready') : message('kotgh.alternative.notReady') }}</small>
              </template>
            </button>
          </article>
        </div>
        <button class="primary-ready" type="button" :disabled="busy" @click="toggleTeamReady">
          <Check :size="19" />
          {{ ownSeat?.ready ? message('kotgh.alternative.notReady') : message('kotgh.alternative.ready') }}
        </button>
      </section>

      <section v-else-if="state.stage === 'Character'" class="stage-panel">
        <div v-if="confirmedAllies.length" class="allied-lineup card">
          <h2><ShieldCheck :size="18" /> {{ message('kotgh.alternative.alliedLineup') }}</h2>
          <div>
            <article v-for="ally in confirmedAllies" :key="ally.discordId">
              <img :src="ally.selectedCharacter!.avatar" :alt="ally.selectedCharacter!.name" />
              <span>{{ ally.username }}</span>
              <strong>{{ ally.selectedCharacter!.name }}</strong>
            </article>
          </div>
        </div>
        <p class="stage-hint"><Dices :size="17" /> {{ message('kotgh.alternative.chooseCharacter') }}</p>
        <div v-if="state.characterOptions.length" class="character-options">
          <button
            v-for="(character, index) in state.characterOptions"
            :key="character.name"
            type="button"
            class="character-choice card"
            :disabled="busy"
            @click="selectCharacter(character.name)"
          >
            <span class="roll-kind">{{ index === 0 ? message('kotgh.alternative.mainRoll') : message('kotgh.alternative.switchOption') }}</span>
            <img :src="character.avatar" :alt="character.name" />
            <h2>{{ character.name }}</h2>
            <span class="tier">{{ character.tier === 0 ? 'PRO' : `T${character.tier}` }}</span>
            <div class="mini-stats">
              <span>INT {{ character.intelligence }}</span><span>STR {{ character.strength }}</span>
              <span>SPD {{ character.speed }}</span><span>PSY {{ character.psyche }}</span>
            </div>
          </button>
        </div>
        <div v-else class="waiting-card card"><Check :size="26" /><strong>{{ message('kotgh.alternative.waiting') }}</strong></div>
      </section>

      <section v-else class="stage-panel aram-panel">
        <p class="stage-hint"><Dices :size="17" /> {{ message('kotgh.alternative.chooseFour') }}</p>
        <template v-if="state.aramBuild">
          <div class="aram-stats card">
            <span>{{ message('kotgh.alternative.stats') }}</span>
            <strong>INT <b>{{ state.aramBuild.intelligence }}</b></strong>
            <strong>STR <b>{{ state.aramBuild.strength }}</b></strong>
            <strong>SPD <b>{{ state.aramBuild.speed }}</b></strong>
            <strong>PSY <b>{{ state.aramBuild.psyche }}</b></strong>
          </div>
          <div class="passive-slots">
            <article v-for="slot in state.aramBuild.passiveSlots" :key="slot.slotIndex" class="passive-slot card">
              <button
                type="button"
                class="passive-slot-header"
                :class="{ open: openPassiveSlot === slot.slotIndex }"
                @click="openPassiveSlot = openPassiveSlot === slot.slotIndex ? null : slot.slotIndex"
              >
                <span>{{ message('kotgh.alternative.passiveSlot', { slot: slot.slotIndex + 1 }) }}</span>
                <Check v-if="slot.selectedIndex >= 0" :size="17" />
                <Plus v-else :size="17" />
              </button>
              <div v-if="openPassiveSlot === slot.slotIndex" class="passive-cells">
                <button
                  v-for="(passive, optionIndex) in slot.candidates"
                  :key="passive.name"
                  type="button"
                  class="passive-option"
                  :class="{ selected: slot.selectedIndex === optionIndex }"
                  :disabled="state.aramBuild.locked || busy"
                  @click="selectPassive(slot.slotIndex, passive.name)"
                >
                  <strong>{{ passive.name }}</strong>
                  <span>{{ passive.description }}</span>
                </button>
                <button
                  v-for="lockedIndex in Math.max(0, 4 - slot.candidates.length)"
                  :key="`locked-${lockedIndex}`"
                  type="button"
                  class="passive-option locked"
                  :disabled="state.aramBuild.locked || busy"
                  :title="message('kotgh.alternative.unlockCost')"
                  @click="unlockPassive(slot.slotIndex)"
                >
                  <Plus :size="23" />
                  <strong>{{ message('kotgh.alternative.unlockPassive') }}</strong>
                  <span>{{ message('kotgh.alternative.unlockCost') }}</span>
                </button>
              </div>
            </article>
          </div>
          <button
            class="primary-ready"
            type="button"
            :disabled="busy || (!ownSeat?.ready && !allPassivesSelected)"
            @click="toggleAramReady"
          >
            <Check :size="19" />
            {{ ownSeat?.ready ? message('kotgh.alternative.buildLocked') : message('kotgh.alternative.lockBuild') }}
          </button>
        </template>
      </section>

      <footer class="lobby-footer">
        <button type="button" class="leave-button" :disabled="busy" @click="leaveLobby">
          <LogOut :size="17" />
          {{ state.isOwner ? message('kotgh.alternative.cancel') : message('kotgh.alternative.leave') }}
        </button>
      </footer>
    </template>
  </main>
</template>

<style scoped>
.alt-lobby { width: min(1120px, calc(100% - 24px)); margin: 0 auto 50px; }
.alt-header { display: flex; align-items: flex-end; justify-content: space-between; gap: 20px; margin: 20px 0 16px; }
.eyebrow { color: var(--accent-gold); font: 800 .68rem/1 var(--font-mono); letter-spacing: .11em; text-transform: uppercase; }
.alt-header h1 { margin: 6px 0 2px; font-size: clamp(1.55rem, 4vw, 2.4rem); }
.alt-header p { margin: 0; color: var(--text-muted); font-size: .82rem; }
.timer-card { display: flex; align-items: center; gap: 10px; min-width: 150px; padding: 11px 14px; border: 1px solid var(--border-subtle); border-radius: 12px; background: var(--glass-bg-heavy); box-shadow: var(--shadow); }
.timer-card small, .timer-card strong { display: block; }.timer-card small { color: var(--text-muted); font-size: .58rem; }.timer-card strong { margin-top: 3px; font: 900 1.16rem/1 var(--font-mono); }.timer-card.urgent { border-color: var(--accent-red); color: var(--accent-red); }
.card { border: 1px solid var(--border-subtle); border-radius: 14px; background: var(--glass-bg); box-shadow: var(--shadow); }
.loading-card, .waiting-card { display: flex; min-height: 180px; align-items: center; justify-content: center; gap: 12px; color: var(--text-muted); }.loading-die { animation: spin 1.2s linear infinite; }
.settings-strip { display: flex; flex-wrap: wrap; gap: 24px; align-items: flex-end; justify-content: space-between; padding: 13px 15px; }
.settings-strip > div > span { display: block; margin-bottom: 7px; color: var(--text-muted); font: 750 .62rem/1 var(--font-mono); text-transform: uppercase; }
.segmented { display: inline-flex; padding: 3px; border-radius: 9px; background: var(--bg-inset); }.segmented button { min-width: 68px; padding: 7px 10px; border: 0; border-radius: 7px; color: var(--text-muted); background: transparent; cursor: pointer; font-weight: 800; }.segmented.difficulty button { min-width: 40px; }.segmented button.active { color: var(--bg-primary); background: var(--accent-gold); }.segmented button:disabled { cursor: default; }
.stage-panel { margin-top: 17px; }.stage-hint { display: flex; align-items: center; gap: 7px; margin: 0 0 12px; color: var(--text-muted); font-size: .76rem; }
.teams-grid { display: grid; gap: 12px; }.teams-3 { grid-template-columns: repeat(3, 1fr); }.teams-2 { grid-template-columns: repeat(2, 1fr); }
.team-card { overflow: hidden; }.team-card > header { display: flex; align-items: baseline; gap: 7px; padding: 12px 14px; border-bottom: 1px solid var(--border-subtle); background: linear-gradient(90deg, color-mix(in srgb, var(--accent-blue) 16%, transparent), transparent); }.team-card > header span { color: var(--text-muted); font: 850 .6rem/1 var(--font-mono); }.team-card > header strong { font-size: 1.25rem; }.team-card > header small { margin-left: auto; color: var(--text-muted); }
.team-seat { position: relative; display: flex; width: calc(100% - 16px); min-height: 73px; align-items: center; gap: 9px; margin: 8px; padding: 12px 12px 12px 42px; border: 1px solid var(--border-subtle); border-radius: 10px; color: var(--text-primary); background: var(--bg-inset); text-align: left; }.team-seat:not(:disabled) { cursor: pointer; border-style: dashed; }.team-seat:not(:disabled):hover { border-color: var(--accent-gold); }.team-seat.mine { border-color: var(--accent-blue); box-shadow: inset 0 0 20px color-mix(in srgb, var(--accent-blue) 9%, transparent); }.team-seat.ready { border-color: var(--accent-green); }.team-seat.empty { justify-content: center; padding-left: 12px; color: var(--text-muted); }.team-seat strong, .team-seat small { display: block; }.team-seat small { color: var(--text-muted); font-size: .62rem; }.seat-index { position: absolute; left: 11px; display: grid; width: 22px; height: 22px; place-items: center; border-radius: 6px; background: var(--bg-card); color: var(--text-muted); font: 800 .6rem/1 var(--font-mono); }
.primary-ready { display: flex; min-width: 220px; align-items: center; justify-content: center; gap: 8px; margin: 16px auto 0; padding: 12px 20px; border: 1px solid var(--accent-green); border-radius: 10px; color: #07150c; background: var(--accent-green); cursor: pointer; font-weight: 900; }.primary-ready:disabled { opacity: .45; cursor: not-allowed; }
.allied-lineup { margin-bottom: 12px; padding: 12px; }.allied-lineup h2 { display: flex; align-items: center; gap: 7px; margin: 0 0 9px; font-size: .78rem; }.allied-lineup > div { display: flex; gap: 8px; flex-wrap: wrap; }.allied-lineup article { display: grid; grid-template-columns: 38px 1fr; column-gap: 8px; align-items: center; min-width: 180px; padding: 7px; border-radius: 9px; background: var(--bg-inset); }.allied-lineup img { grid-row: span 2; width: 38px; height: 38px; border-radius: 9px; object-fit: cover; }.allied-lineup span { color: var(--text-muted); font-size: .6rem; }.allied-lineup strong { font-size: .7rem; }
.character-options { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }.character-choice { position: relative; overflow: hidden; padding: 0 0 13px; color: var(--text-primary); cursor: pointer; text-align: center; }.character-choice:hover { transform: translateY(-2px); border-color: var(--accent-gold); }.character-choice img { width: 100%; height: 190px; object-fit: cover; }.character-choice h2 { margin: 10px 8px 3px; font-size: 1rem; }.roll-kind { position: absolute; z-index: 1; top: 9px; left: 9px; padding: 5px 7px; border-radius: 6px; color: var(--text-primary); background: rgba(8, 9, 14, .78); font: 800 .58rem/1 var(--font-mono); }.tier { color: var(--accent-gold); font: 800 .64rem/1 var(--font-mono); }.mini-stats { display: grid; grid-template-columns: repeat(4, 1fr); gap: 4px; margin: 10px 9px 0; }.mini-stats span { padding: 5px 2px; border-radius: 5px; background: var(--bg-inset); font: 700 .56rem/1 var(--font-mono); }
.aram-stats { display: flex; align-items: center; gap: 10px; padding: 12px 14px; }.aram-stats > span { margin-right: auto; color: var(--text-muted); font-weight: 800; }.aram-stats strong { padding: 7px 9px; border-radius: 7px; background: var(--bg-inset); font: 800 .65rem/1 var(--font-mono); }.aram-stats b { color: var(--accent-gold); }
.passive-slots { display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; margin-top: 12px; }.passive-slot { padding: 12px; }.passive-slot-header { display: flex; width: 100%; align-items: center; justify-content: space-between; padding: 4px; border: 0; color: var(--accent-gold); background: transparent; cursor: pointer; font-weight: 850; text-align: left; }.passive-slot-header.open { margin-bottom: 9px; }.passive-cells { display: grid; grid-template-columns: repeat(2, 1fr); gap: 7px; }.passive-option { display: flex; min-height: 112px; flex-direction: column; align-items: flex-start; gap: 5px; padding: 10px; border: 1px solid var(--border-subtle); border-radius: 9px; color: var(--text-primary); background: var(--bg-inset); cursor: pointer; text-align: left; }.passive-option strong { font-size: .72rem; }.passive-option span { display: -webkit-box; overflow: hidden; color: var(--text-muted); font-size: .6rem; line-height: 1.35; -webkit-box-orient: vertical; -webkit-line-clamp: 4; }.passive-option.selected { border-color: var(--accent-green); box-shadow: inset 0 0 18px color-mix(in srgb, var(--accent-green) 10%, transparent); }.passive-option.locked { align-items: center; justify-content: center; border-style: dashed; color: var(--text-muted); text-align: center; }.passive-option:disabled { cursor: default; }
.lobby-footer { display: flex; justify-content: center; margin-top: 22px; }.leave-button { display: flex; align-items: center; gap: 7px; padding: 8px 12px; border: 1px solid var(--border-subtle); border-radius: 8px; color: var(--text-muted); background: transparent; cursor: pointer; }.leave-button:hover { color: var(--accent-red); border-color: var(--accent-red); }
@keyframes spin { to { transform: rotate(360deg); } }
@media (max-width: 760px) { .alt-header { align-items: flex-start; flex-direction: column; }.timer-card { width: 100%; }.teams-3, .teams-2, .character-options, .passive-slots { grid-template-columns: 1fr; }.character-choice img { height: 160px; }.aram-stats { flex-wrap: wrap; }.aram-stats > span { width: 100%; }.passive-cells { grid-template-columns: 1fr; } }
</style>
