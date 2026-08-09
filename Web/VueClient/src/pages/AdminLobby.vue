<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import {
  Bot,
  ChevronDown,
  ChevronRight,
  Globe2,
  Search,
  UserPlus,
  Users,
  X,
} from 'lucide-vue-next'
import { useGameStore } from 'src/store/game'
import { currentLocale } from 'src/i18n'
import { message } from 'src/platform/localization/messages'
import type { AdminLobbyGuild, AdminLobbyUser } from 'src/services/signalr'

const store = useGameStore()
const router = useRouter()

const inviteSlot = ref<number | null>(null)
const directoryLoading = ref(false)
const searchText = ref('')
const statusFilter = ref<'site' | 'discord' | 'all'>('site')
const serverFilter = ref('all')
const collapsedGuilds = ref<Set<string>>(new Set())
const botChoiceSlot = ref<number | null>(null)
const busyAction = ref('')
let presencePoll: ReturnType<typeof setInterval> | null = null
let presenceRequestInFlight = false
let initialized = false

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

const onlineIds = computed(() => new Set(store.adminLobbyPresence?.onlineIds ?? []))
const busyIds = computed(() => new Set(store.adminLobbyPresence?.busyIds ?? []))
const reservedIds = computed(() => new Set(store.adminLobbyPresence?.reservedIds ?? []))
const selectedHumanIds = computed(() => new Set(
  store.adminLobbyState?.slots
    .filter(slot => slot.kind === 'human')
    .map(slot => slot.discordId) ?? [],
))

function isBrowserOnline(user: AdminLobbyUser): boolean {
  return store.adminLobbyPresence ? onlineIds.value.has(user.discordId) : user.browserOnline
}

function isBusy(user: AdminLobbyUser): boolean {
  return store.adminLobbyPresence ? busyIds.value.has(user.discordId) : user.isBusy
}

function isReserved(user: AdminLobbyUser): boolean {
  return store.adminLobbyPresence ? reservedIds.value.has(user.discordId) : user.isReserved
}

function canInvite(user: AdminLobbyUser): boolean {
  return !isBusy(user)
    && !isReserved(user)
    && !selectedHumanIds.value.has(user.discordId)
}

const visibleGuilds = computed<AdminLobbyGuild[]>(() => {
  const query = searchText.value.trim().toLocaleLowerCase()
  return (store.adminLobbyDirectory?.guilds ?? [])
    .filter(guild => serverFilter.value === 'all' || guild.guildId === serverFilter.value)
    .map(guild => ({
      ...guild,
      members: guild.members.filter((user) => {
        if (query && !user.username.toLocaleLowerCase().includes(query)) return false
        if (statusFilter.value === 'site' && !isBrowserOnline(user)) return false
        if (statusFilter.value === 'discord' && !user.discordOnline) return false
        return true
      }),
    }))
    .filter(guild => guild.members.length > 0)
})

async function initializeLobby() {
  if (initialized || !store.isAuthenticated) return
  if (!store.isGodAdmin) {
    await router.replace('/games')
    return
  }
  initialized = true
  await store.requestAdminLobbyState()
  await nextTick()
  if (!store.adminLobbyState) await store.createAdminLobby()
}

onMounted(() => {
  void initializeLobby()
})

watch(
  () => [store.isAuthenticated, store.isGodAdmin],
  () => { void initializeLobby() },
)

onUnmounted(() => {
  stopPresencePoll()
})

function startPresencePoll() {
  stopPresencePoll()
  void pollPresence()
  presencePoll = setInterval(() => {
    if (inviteSlot.value !== null && store.isConnected) {
      void pollPresence()
    }
  }, 1000)
}

async function pollPresence() {
  if (presenceRequestInFlight) return
  presenceRequestInFlight = true
  try {
    await store.requestAdminLobbyPresence()
  }
  finally {
    presenceRequestInFlight = false
  }
}

function stopPresencePoll() {
  if (presencePoll) clearInterval(presencePoll)
  presencePoll = null
}

async function openInvitePanel(slotIndex: number) {
  if (inviteSlot.value !== null) return
  inviteSlot.value = slotIndex
  directoryLoading.value = true
  searchText.value = ''
  statusFilter.value = 'site'
  serverFilter.value = 'all'
  collapsedGuilds.value = new Set()
  startPresencePoll()
  try {
    await store.requestAdminLobbyDirectory()
  }
  finally {
    directoryLoading.value = false
  }
}

function closeInvitePanel() {
  inviteSlot.value = null
  directoryLoading.value = false
  stopPresencePoll()
}

async function invite(user: AdminLobbyUser) {
  if (inviteSlot.value === null || !canInvite(user)) return
  const slotIndex = inviteSlot.value
  busyAction.value = `invite:${user.discordId}`
  try {
    await store.adminLobbyInvitePlayer(slotIndex, user.discordId)
    closeInvitePanel()
  }
  finally {
    busyAction.value = ''
  }
}

async function addBot(slotIndex: number, difficulty: number) {
  busyAction.value = `bot:${slotIndex}`
  try {
    await store.adminLobbyAddBot(slotIndex, difficulty)
    botChoiceSlot.value = null
  }
  finally {
    busyAction.value = ''
  }
}

async function setCharacter(slotIndex: number, event: Event) {
  const characterName = (event.target as HTMLSelectElement).value
  busyAction.value = `character:${slotIndex}`
  try {
    await store.adminLobbySetCharacter(slotIndex, characterName)
  }
  finally {
    busyAction.value = ''
  }
}

async function removeSlot(slotIndex: number) {
  busyAction.value = `remove:${slotIndex}`
  try {
    await store.adminLobbyRemoveSlot(slotIndex)
  }
  finally {
    busyAction.value = ''
  }
}

async function startGame() {
  if (busyAction.value) return
  busyAction.value = 'start'
  try {
    await store.adminLobbyStart()
  }
  finally {
    busyAction.value = ''
  }
}

async function cancelLobby() {
  if (busyAction.value) return
  busyAction.value = 'cancel'
  try {
    await store.adminLobbyCancel()
    await router.push('/games')
  }
  finally {
    busyAction.value = ''
  }
}

function toggleGuild(guildId: string) {
  const next = new Set(collapsedGuilds.value)
  if (next.has(guildId)) next.delete(guildId)
  else next.add(guildId)
  collapsedGuilds.value = next
}

function aiLabel(difficulty: number): string {
  if (difficulty === 1) return 'Legacy'
  if (difficulty === 4) return 'Legacy+'
  if (difficulty === 2) return 'V2'
  if (difficulty === 3) return 'V3'
  return message('kotgh.adminLobby.defaultLegacyPlus')
}
</script>

<template>
  <div class="admin-lobby-page">
    <header class="admin-header">
      <div>
        <span class="eyebrow">{{ t('Curated match', 'Кураторский матч') }}</span>
        <h1>{{ t('Admin Match', 'Админская игра') }}</h1>
        <p>
          {{ t(
            'Compose six seats, choose bot intelligence and pin characters before the game exists.',
            'Соберите шесть мест, выберите интеллект ботов и закрепите персонажей до создания игры.',
          ) }}
        </p>
      </div>
      <div class="admin-mark" aria-hidden="true">✦</div>
    </header>

    <div v-if="!store.adminLobbyState" class="loading-card">
      {{ t('Preparing the lobby…', 'Подготавливаем лобби…') }}
    </div>

    <template v-else>
      <section class="slots-grid" aria-label="Admin lobby seats">
        <article
          v-for="(slot, slotIndex) in store.adminLobbyState.slots"
          :key="slotIndex"
          class="slot-card"
          :class="`slot-${slot.kind}`"
        >
          <div class="slot-topline">
            <span class="seat-number">{{ t('Seat', 'Место') }} {{ slotIndex + 1 }}</span>
            <button
              v-if="slotIndex > 0 && slot.kind !== 'empty'"
              class="icon-button"
              :aria-label="t('Remove seat', 'Освободить место')"
              :disabled="Boolean(busyAction)"
              @click="removeSlot(slotIndex)"
            >
              <X :size="17" />
            </button>
          </div>

          <template v-if="slot.kind === 'empty'">
            <div class="empty-icon"><Users :size="34" /></div>
            <h2>{{ t('Empty seat', 'Свободное место') }}</h2>
            <div class="empty-actions">
              <button class="choice-button" @click="botChoiceSlot = slotIndex">
                <Bot :size="18" /> {{ t('Add bot', 'Добавить бота') }}
              </button>
              <button class="choice-button" @click="openInvitePanel(slotIndex)">
                <UserPlus :size="18" /> {{ t('Invite player', 'Пригласить игрока') }}
              </button>
            </div>
            <div v-if="botChoiceSlot === slotIndex" class="difficulty-picker">
              <button :disabled="Boolean(busyAction)" @click="addBot(slotIndex, 1)">Legacy</button>
              <button :disabled="Boolean(busyAction)" @click="addBot(slotIndex, 4)">Legacy+</button>
              <button :disabled="Boolean(busyAction)" @click="addBot(slotIndex, 2)">V2</button>
              <button :disabled="Boolean(busyAction)" @click="addBot(slotIndex, 3)">V3</button>
            </div>
          </template>

          <template v-else>
            <div class="occupant">
              <div class="occupant-icon">
                <Bot v-if="slot.kind === 'bot'" :size="26" />
                <Users v-else :size="26" />
              </div>
              <div>
                <h2>{{ slot.username }}</h2>
                <p v-if="slot.kind === 'bot'">
                  {{ t('Bot intelligence', 'Интеллект бота') }} · {{ aiLabel(slot.aiDifficulty) }}
                </p>
                <p v-else-if="slotIndex === 0">{{ t('Lobby owner', 'Владелец лобби') }}</p>
                <p v-else-if="slot.isUnreachable" class="warning">
                  {{ t('unreachable for notification', 'недоступен для уведомления') }}
                </p>
                <p v-else-if="slot.notifiedByDm">{{ t('Discord DM sent', 'Отправлено ЛС в Discord') }}</p>
                <p v-else>{{ t('Browser invitation sent', 'Приглашение отправлено в браузер') }}</p>
              </div>
            </div>

            <label class="character-field">
              <span>{{ t('Character', 'Персонаж') }}</span>
              <select
                :value="slot.characterName"
                :disabled="Boolean(busyAction)"
                @change="setCharacter(slotIndex, $event)"
              >
                <option value="">{{ t('random', 'случайный') }}</option>
                <option
                  v-for="character in store.adminLobbyState.characters"
                  :key="character.name"
                  :value="character.name"
                >
                  {{ character.name }}
                </option>
              </select>
            </label>
          </template>
        </article>
      </section>

      <footer class="admin-footer">
        <p>
          {{ message('kotgh.adminLobby.emptySeatsLegacyPlus') }}
        </p>
        <div>
          <button class="cancel-button" :disabled="Boolean(busyAction)" @click="cancelLobby">
            {{ t('cancel', 'отмена') }}
          </button>
          <button class="start-button" :disabled="Boolean(busyAction)" @click="startGame">
            {{ busyAction === 'start' ? t('Starting…', 'Запускаем…') : t('Start game', 'Начать игру') }}
          </button>
        </div>
      </footer>
    </template>

    <div v-if="inviteSlot !== null" class="directory-backdrop" @click.self="closeInvitePanel">
      <section class="directory-panel" role="dialog" aria-modal="true" aria-labelledby="directory-title">
        <header class="directory-header">
          <div>
            <span class="eyebrow">{{ t('Seat', 'Место') }} {{ inviteSlot + 1 }}</span>
            <h2 id="directory-title">{{ t('Player directory', 'Каталог игроков') }}</h2>
          </div>
          <button class="icon-button" :aria-label="t('Close', 'Закрыть')" @click="closeInvitePanel">
            <X :size="20" />
          </button>
        </header>

        <div class="directory-controls">
          <label class="search-field">
            <Search :size="18" aria-hidden="true" />
            <input
              v-model="searchText"
              type="search"
              :placeholder="t('Search every server', 'Поиск по всем серверам')"
            />
          </label>

          <div class="filter-row">
            <div class="filter-chips" role="group" :aria-label="t('Status filter', 'Фильтр статуса')">
              <button :class="{ active: statusFilter === 'site' }" @click="statusFilter = 'site'">
                {{ t('Online on the site', 'Онлайн на сайте') }}
              </button>
              <button :class="{ active: statusFilter === 'discord' }" @click="statusFilter = 'discord'">
                Discord
              </button>
              <button :class="{ active: statusFilter === 'all' }" @click="statusFilter = 'all'">
                {{ t('All', 'Все') }}
              </button>
            </div>
            <select v-model="serverFilter" :aria-label="t('Server filter', 'Фильтр сервера')">
              <option value="all">{{ t('All servers', 'Все серверы') }}</option>
              <option
                v-for="guild in store.adminLobbyDirectory?.guilds ?? []"
                :key="guild.guildId"
                :value="guild.guildId"
              >
                {{ guild.guildName }}
              </option>
            </select>
          </div>
        </div>

        <div v-if="directoryLoading" class="directory-loading">
          <span class="spinner" />
          {{ t('Loading Discord server members…', 'Загружаем участников Discord-серверов…') }}
        </div>

        <div v-else class="guild-list">
          <section v-for="guild in visibleGuilds" :key="guild.guildId" class="guild-section">
            <button class="guild-heading" @click="toggleGuild(guild.guildId)">
              <ChevronRight v-if="collapsedGuilds.has(guild.guildId)" :size="17" />
              <ChevronDown v-else :size="17" />
              <Globe2 v-if="guild.guildId === 'web-only'" :size="17" />
              <Users v-else :size="17" />
              <span>{{ guild.guildName }}</span>
              <small>{{ guild.members.length }}</small>
            </button>

            <div v-if="!collapsedGuilds.has(guild.guildId)" class="member-list">
              <button
                v-for="user in guild.members"
                :key="`${guild.guildId}:${user.discordId}`"
                class="member-row"
                :disabled="!canInvite(user) || Boolean(busyAction)"
                @click="invite(user)"
              >
                <span class="identity-icon" :class="{ web: !user.hasDiscord }">
                  <Globe2 v-if="!user.hasDiscord" :size="18" />
                  <Users v-else :size="18" />
                </span>
                <span class="member-name">
                  <strong>{{ user.username }}</strong>
                  <small v-if="isBusy(user)">{{ t('already playing', 'уже играет') }}</small>
                  <small v-else-if="isReserved(user)">{{ t('reserved', 'зарезервирован') }}</small>
                  <small v-else-if="selectedHumanIds.has(user.discordId)">
                    {{ t('already seated', 'уже в лобби') }}
                  </small>
                  <small v-else-if="!user.hasDiscord">{{ t('No Discord', 'Без Discord') }}</small>
                </span>
                <span class="status-pair">
                  <span
                    class="status-dot browser"
                    :class="{ online: isBrowserOnline(user) }"
                    :title="t('Browser online', 'Онлайн на сайте')"
                  />
                  <span
                    v-if="user.hasDiscord"
                    class="status-dot discord"
                    :class="{ online: user.discordOnline }"
                    :title="t('Discord online', 'Онлайн в Discord')"
                  />
                  <span v-else class="status-dot unavailable" />
                </span>
              </button>
            </div>
          </section>

          <div v-if="visibleGuilds.length === 0" class="directory-empty">
            {{ t('No players match these filters.', 'Нет игроков, подходящих под фильтры.') }}
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.admin-lobby-page {
  min-height: calc(100vh - 76px);
  padding: 36px clamp(18px, 4vw, 64px) 54px;
  background:
    radial-gradient(circle at 80% 5%, rgba(242, 185, 69, 0.13), transparent 28rem),
    radial-gradient(circle at 5% 55%, rgba(82, 179, 159, 0.09), transparent 26rem);
}

.admin-header,
.admin-footer {
  max-width: 1180px;
  margin: 0 auto;
}

.admin-header {
  display: flex;
  justify-content: space-between;
  gap: 24px;
  align-items: center;
  margin-bottom: 28px;
}

.eyebrow {
  color: var(--accent-gold);
  font-size: 0.72rem;
  font-weight: 800;
  letter-spacing: 0.16em;
  text-transform: uppercase;
}

.admin-header h1 {
  margin: 6px 0 8px;
  font-size: clamp(2rem, 5vw, 3.5rem);
  line-height: 1;
}

.admin-header p,
.admin-footer p {
  max-width: 720px;
  color: var(--text-muted);
}

.admin-mark {
  display: grid;
  width: 86px;
  height: 86px;
  flex: 0 0 auto;
  place-items: center;
  border: 1px solid rgba(242, 185, 69, 0.35);
  border-radius: 50%;
  color: var(--accent-gold);
  font-size: 2.4rem;
  background: rgba(242, 185, 69, 0.07);
  box-shadow: 0 0 44px rgba(242, 185, 69, 0.12);
}

.slots-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
  max-width: 1180px;
  margin: 0 auto;
}

.slot-card,
.loading-card {
  border: 1px solid var(--glass-border);
  border-radius: 18px;
  background: linear-gradient(145deg, var(--glass-bg-heavy), var(--bg-inset));
  box-shadow: 0 18px 44px rgba(0, 0, 0, 0.18);
}

.slot-card {
  min-height: 250px;
  padding: 18px;
}

.slot-human {
  border-color: rgba(82, 179, 159, 0.35);
}

.slot-bot {
  border-color: rgba(242, 185, 69, 0.3);
}

.slot-topline {
  display: flex;
  min-height: 32px;
  justify-content: space-between;
  align-items: center;
}

.seat-number {
  color: var(--text-dim);
  font-size: 0.74rem;
  font-weight: 800;
  letter-spacing: 0.12em;
  text-transform: uppercase;
}

.icon-button {
  display: inline-grid;
  width: 34px;
  height: 34px;
  padding: 0;
  place-items: center;
  border: 1px solid var(--glass-border);
  border-radius: 10px;
  color: var(--text-secondary);
  background: var(--bg-inset);
  cursor: pointer;
}

.empty-icon {
  display: grid;
  width: 62px;
  height: 62px;
  margin: 12px auto 6px;
  place-items: center;
  border: 1px dashed var(--glass-border);
  border-radius: 50%;
  color: var(--text-dim);
}

.slot-card h2 {
  margin: 10px 0;
  font-size: 1.08rem;
}

.slot-empty h2 {
  text-align: center;
}

.empty-actions {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
  margin-top: 20px;
}

.choice-button,
.difficulty-picker button,
.cancel-button,
.start-button {
  border: 1px solid var(--glass-border);
  border-radius: 11px;
  font-weight: 750;
  cursor: pointer;
}

.choice-button {
  display: flex;
  min-height: 48px;
  padding: 10px;
  align-items: center;
  justify-content: center;
  gap: 7px;
  color: var(--text-secondary);
  background: var(--bg-surface);
}

.difficulty-picker {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 6px;
  margin-top: 8px;
}

.difficulty-picker button {
  padding: 8px;
  color: var(--accent-gold);
  background: rgba(242, 185, 69, 0.08);
}

.occupant {
  display: flex;
  min-height: 105px;
  align-items: center;
  gap: 13px;
}

.occupant-icon {
  display: grid;
  width: 50px;
  height: 50px;
  flex: 0 0 auto;
  place-items: center;
  border-radius: 14px;
  color: var(--accent-teal);
  background: rgba(82, 179, 159, 0.1);
}

.occupant h2 {
  overflow: hidden;
  margin-bottom: 3px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.occupant p {
  margin: 0;
  color: var(--text-muted);
  font-size: 0.82rem;
}

.occupant .warning {
  color: #ef9b7d;
}

.character-field {
  display: grid;
  gap: 7px;
  margin-top: 28px;
}

.character-field span {
  color: var(--text-muted);
  font-size: 0.75rem;
  font-weight: 700;
}

.character-field select,
.filter-row select {
  width: 100%;
  border: 1px solid var(--glass-border);
  border-radius: 10px;
  color: var(--text-primary);
  background: var(--bg-inset);
}

.character-field select {
  padding: 11px;
}

.admin-footer {
  display: flex;
  margin-top: 22px;
  align-items: center;
  justify-content: space-between;
  gap: 18px;
}

.admin-footer > div {
  display: flex;
  gap: 10px;
}

.cancel-button,
.start-button {
  min-width: 130px;
  padding: 12px 18px;
}

.cancel-button {
  color: var(--text-secondary);
  background: var(--bg-surface);
}

.start-button {
  border-color: rgba(242, 185, 69, 0.45);
  color: #16120a;
  background: var(--accent-gold);
}

.loading-card {
  max-width: 1180px;
  margin: 0 auto;
  padding: 80px 20px;
  color: var(--text-muted);
  text-align: center;
}

.directory-backdrop {
  position: fixed;
  z-index: 2000;
  inset: 0;
  display: flex;
  justify-content: flex-end;
  background: rgba(4, 7, 10, 0.72);
  backdrop-filter: blur(7px);
}

.directory-panel {
  display: flex;
  width: min(640px, 100%);
  height: 100%;
  flex-direction: column;
  border-left: 1px solid var(--glass-border);
  background: var(--bg-primary);
  box-shadow: -22px 0 70px rgba(0, 0, 0, 0.42);
}

.directory-header {
  display: flex;
  padding: 24px;
  align-items: flex-start;
  justify-content: space-between;
  border-bottom: 1px solid var(--glass-border);
}

.directory-header h2 {
  margin: 5px 0 0;
}

.directory-controls {
  display: grid;
  gap: 12px;
  padding: 18px 24px;
  border-bottom: 1px solid var(--glass-border);
}

.search-field {
  display: flex;
  align-items: center;
  gap: 9px;
  padding: 0 12px;
  border: 1px solid var(--glass-border);
  border-radius: 11px;
  color: var(--text-muted);
  background: var(--bg-inset);
}

.search-field input {
  width: 100%;
  padding: 11px 0;
  border: 0;
  outline: 0;
  color: var(--text-primary);
  background: transparent;
}

.filter-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.filter-chips {
  display: flex;
  flex: 1;
  gap: 6px;
}

.filter-chips button {
  padding: 8px 10px;
  border: 1px solid var(--glass-border);
  border-radius: 999px;
  color: var(--text-muted);
  background: transparent;
  cursor: pointer;
}

.filter-chips button.active {
  border-color: rgba(82, 179, 159, 0.5);
  color: var(--accent-teal);
  background: rgba(82, 179, 159, 0.1);
}

.filter-row select {
  max-width: 180px;
  padding: 8px;
}

.directory-loading,
.directory-empty {
  display: flex;
  min-height: 180px;
  align-items: center;
  justify-content: center;
  gap: 10px;
  color: var(--text-muted);
}

.spinner {
  width: 19px;
  height: 19px;
  border: 2px solid var(--glass-border);
  border-top-color: var(--accent-gold);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.guild-list {
  overflow-y: auto;
  padding: 14px 18px 28px;
}

.guild-section {
  margin-bottom: 10px;
  overflow: hidden;
  border: 1px solid var(--glass-border);
  border-radius: 13px;
  background: var(--bg-secondary);
}

.guild-heading {
  display: flex;
  width: 100%;
  padding: 12px 14px;
  align-items: center;
  gap: 8px;
  border: 0;
  color: var(--text-primary);
  background: transparent;
  cursor: pointer;
}

.guild-heading span {
  flex: 1;
  text-align: left;
}

.guild-heading small {
  color: var(--text-dim);
}

.member-list {
  border-top: 1px solid var(--glass-border);
}

.member-row {
  display: grid;
  width: 100%;
  grid-template-columns: 38px 1fr auto;
  padding: 10px 14px;
  align-items: center;
  gap: 9px;
  border: 0;
  border-bottom: 1px solid rgba(255, 255, 255, 0.035);
  color: var(--text-secondary);
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.member-row:hover:not(:disabled) {
  background: var(--bg-card-hover);
}

.member-row:disabled {
  cursor: not-allowed;
  opacity: 0.48;
}

.identity-icon {
  display: grid;
  width: 34px;
  height: 34px;
  place-items: center;
  border-radius: 10px;
  color: #8897ff;
  background: rgba(88, 101, 242, 0.12);
}

.identity-icon.web {
  color: var(--accent-teal);
  background: rgba(82, 179, 159, 0.11);
}

.member-name {
  display: grid;
  gap: 2px;
}

.member-name small {
  color: var(--text-dim);
}

.status-pair {
  display: flex;
  gap: 7px;
}

.status-dot {
  width: 10px;
  height: 10px;
  border: 2px solid rgba(255, 255, 255, 0.12);
  border-radius: 50%;
  background: #59616d;
}

.status-dot.browser.online {
  background: #4fd4a5;
  box-shadow: 0 0 9px rgba(79, 212, 165, 0.55);
}

.status-dot.discord.online {
  background: #7c8cff;
  box-shadow: 0 0 9px rgba(124, 140, 255, 0.55);
}

.status-dot.unavailable {
  opacity: 0.2;
}

button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

@media (max-width: 900px) {
  .slots-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 620px) {
  .admin-lobby-page {
    padding-inline: 12px;
  }

  .admin-mark {
    display: none;
  }

  .slots-grid {
    grid-template-columns: 1fr;
  }

  .admin-footer,
  .filter-row {
    align-items: stretch;
    flex-direction: column;
  }

  .admin-footer > div,
  .filter-chips {
    display: grid;
    grid-template-columns: 1fr 1fr;
  }

  .filter-chips button:first-child {
    grid-column: span 2;
  }

  .filter-row select {
    max-width: none;
  }
}
</style>
