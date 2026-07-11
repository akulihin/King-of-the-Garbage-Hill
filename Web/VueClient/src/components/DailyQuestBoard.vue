<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch, type Component } from 'vue'
import {
  Award,
  ArrowLeftRight,
  BarChart3,
  BrainCircuit,
  CalendarClock,
  Check,
  CheckCircle2,
  CircleAlert,
  Clock3,
  Crown,
  Dices,
  Flame,
  Gamepad2,
  Gift,
  HandFist,
  Hourglass,
  LoaderCircle,
  Medal,
  Mountain,
  PackageOpen,
  RefreshCw,
  Route,
  Scale,
  ShieldCheck,
  Sparkles,
  Swords,
  Target,
  Trophy,
  Users,
  Zap,
} from 'lucide-vue-next'
import { currentLocale } from 'src/i18n'
import type { QuestProgress, QuestState } from 'src/services/signalr'

const props = defineProps<{
  state: QuestState | null
  loading: boolean
  error: string | null
  rerollingQuestId: string | null
}>()

const emit = defineEmits<{
  retry: []
  reroll: [questId: string]
  reset: []
}>()

const now = ref(Date.now())
const boardRoot = ref<HTMLElement | null>(null)
const serverClockOffset = ref(0)
const resetRefreshEmittedFor = ref('')
const justCompletedIds = ref<Set<string>>(new Set())
const completionAnnouncement = ref('')
let clockTimer: ReturnType<typeof setInterval> | null = null
let completionTimer: ReturnType<typeof setTimeout> | null = null
let hasCompletionBaseline = false
let baselineDate = ''
let previousCompletion = new Map<string, boolean>()

const quests = computed(() => props.state?.quests ?? [])
const totalQuestCount = computed(() => quests.value.length)
const completedQuestCount = computed(() => Math.min(
  totalQuestCount.value,
  Math.max(0, props.state?.completedQuestCount ?? quests.value.filter(quest => quest.isCompleted).length),
))
const completionPercent = computed(() => {
  if (totalQuestCount.value <= 0) return 0
  return Math.round((completedQuestCount.value / totalQuestCount.value) * 100)
})
const correctedNow = computed(() => now.value + serverClockOffset.value)
const resetRemainingMs = computed(() => remainingUntil(props.state?.resetsAt))
const weekRemainingMs = computed(() => remainingUntil(props.state?.weekEndsAt))
const dailyRequirement = computed(() => Math.max(0, props.state?.dailyQuestRequirement ?? 0))
const weeklyTarget = computed(() => Math.max(1, props.state?.weeklyTargetDays ?? 5))
const weeklyCompleted = computed(() => Math.max(0, props.state?.weeklyCompletedDays ?? 0))
const weeklyProgressPercent = computed(() => Math.min(100, (weeklyCompleted.value / weeklyTarget.value) * 100))
const weekStampCount = 7

const questIcons: Record<string, Component> = {
  arrows: ArrowLeftRight,
  award: Award,
  balance: Scale,
  brain: BrainCircuit,
  chart: BarChart3,
  crown: Crown,
  dice: Dices,
  fist: HandFist,
  fight: Swords,
  flame: Flame,
  game: Gamepad2,
  games: Gamepad2,
  hourglass: Hourglass,
  lightning: Zap,
  medal: Medal,
  mountain: Mountain,
  play: Gamepad2,
  prediction: BrainCircuit,
  route: Route,
  score: BarChart3,
  shield: ShieldCheck,
  social: Users,
  streak: Flame,
  sword: Swords,
  swords: Swords,
  target: Target,
  trophy: Trophy,
  win: Trophy,
  zap: Zap,
}

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

function normalizedKey(value: string): string {
  return value.trim().toLocaleLowerCase().replace(/[_\s-]+/g, '')
}

function iconFor(quest: QuestProgress): Component {
  const iconKey = normalizedKey(quest.icon)
  const laneKey = normalizedKey(quest.lane)
  return questIcons[iconKey] ?? questIcons[laneKey] ?? Sparkles
}

function localizedName(quest: QuestProgress): string {
  if (currentLocale.value === 'ru') return quest.nameRu || quest.name || quest.descriptionRu || quest.description
  return quest.name || quest.description
}

function localizedDescription(quest: QuestProgress): string {
  if (currentLocale.value === 'ru') return quest.descriptionRu || quest.description
  return quest.description
}

function laneLabel(lane: string): string {
  const labels: Record<string, [string, string]> = {
    ambition: ['Ambition', 'Амбиция'],
    anchor: ['Anchor', 'Основа'],
    combat: ['Combat', 'Бой'],
    consistency: ['Consistency', 'Стабильность'],
    participation: ['Participation', 'Участие'],
    placement: ['Placement', 'Место'],
    prediction: ['Prediction', 'Предсказания'],
    progress: ['Progress', 'Прогресс'],
    score: ['Score', 'Очки'],
    skirmish: ['Skirmish', 'Схватка'],
    strategy: ['Strategy', 'Стратегия'],
    survival: ['Survival', 'Выживание'],
    universal: ['Universal', 'Общее'],
  }
  const label = labels[normalizedKey(lane)] ?? ['Daily objective', 'Ежедневная цель']
  return t(label[0], label[1])
}

function aggregationLabel(aggregation: string): string {
  const key = normalizedKey(aggregation)
  if (['best', 'bestmatch', 'bestsinglegame', 'singlematch', 'singlegame'].includes(key)) {
    return t('Best in one match', 'Лучший результат за матч')
  }
  if (['cumulative', 'daily', 'dailysum', 'sum', 'total'].includes(key)) {
    return t('Across today', 'За весь день')
  }
  return t('Daily progress', 'Прогресс за день')
}

function questProgress(quest: QuestProgress): number {
  if (quest.target <= 0) return quest.isCompleted ? 100 : 0
  return Math.max(0, Math.min(100, (quest.current / quest.target) * 100))
}

function boundedQuestCurrent(quest: QuestProgress): number {
  if (quest.target <= 0) return quest.isCompleted ? 1 : 0
  return Math.max(0, Math.min(quest.current, quest.target))
}

function questAriaMaximum(quest: QuestProgress): number {
  return Math.max(1, quest.target)
}

function remainingUntil(value: string | undefined): number {
  if (!value) return 0
  const target = Date.parse(value)
  if (!Number.isFinite(target)) return 0
  return Math.max(0, target - correctedNow.value)
}

function formatCountdown(milliseconds: number): string {
  const totalSeconds = Math.max(0, Math.floor(milliseconds / 1000))
  const days = Math.floor(totalSeconds / 86_400)
  const hours = Math.floor((totalSeconds % 86_400) / 3_600)
  const minutes = Math.floor((totalSeconds % 3_600) / 60)
  const seconds = totalSeconds % 60
  if (days > 0) return t(`${days}d ${hours}h`, `${days}д ${hours}ч`)
  return [hours, minutes, seconds].map(value => String(value).padStart(2, '0')).join(':')
}

function formatWeekEnd(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return new Intl.DateTimeFormat(currentLocale.value === 'ru' ? 'ru-RU' : 'en-CA', {
    weekday: 'short',
    hour: '2-digit',
    minute: '2-digit',
    timeZoneName: 'short',
  }).format(date)
}

function formatCompletedAt(value: string | null): string {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return new Intl.DateTimeFormat(currentLocale.value === 'ru' ? 'ru-RU' : 'en-CA', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(date)
}

function calibrateServerClock(): void {
  const serverNow = Date.parse(props.state?.serverNow ?? '')
  serverClockOffset.value = Number.isFinite(serverNow) ? serverNow - Date.now() : 0
  now.value = Date.now()
}

function updateClock(): void {
  now.value = Date.now()
  const resetKey = props.state?.resetsAt ?? ''
  if (resetKey && resetRemainingMs.value <= 0 && resetRefreshEmittedFor.value !== resetKey) {
    resetRefreshEmittedFor.value = resetKey
    emit('reset')
  }
}

function reroll(quest: QuestProgress): void {
  if (!quest.canReroll || quest.isCompleted || props.rerollingQuestId || (props.state?.rerollsRemaining ?? 0) <= 0) return
  emit('reroll', quest.id)
}

watch(() => props.state?.serverNow, calibrateServerClock, { immediate: true })

watch(() => props.state?.resetsAt, (next, previous) => {
  if (next !== previous) resetRefreshEmittedFor.value = ''
  updateClock()
})

watch(() => props.state?.quests, (nextQuests) => {
  if (!nextQuests) return
  const activeDate = props.state?.activeDate ?? ''
  const nextCompletion = new Map(nextQuests.map(quest => [quest.id, quest.isCompleted]))

  if (!hasCompletionBaseline || activeDate !== baselineDate) {
    hasCompletionBaseline = true
    baselineDate = activeDate
    previousCompletion = nextCompletion
    justCompletedIds.value = new Set()
    return
  }

  const removedQuestId = [...previousCompletion.keys()].find(id => !nextCompletion.has(id))
  const replacementQuest = removedQuestId
    ? nextQuests.find(quest => !previousCompletion.has(quest.id))
    : undefined
  const newlyCompleted = nextQuests.filter(quest => (
    quest.isCompleted
    && (previousCompletion.get(quest.id) === false || replacementQuest?.id === quest.id)
  ))
  previousCompletion = nextCompletion

  if (replacementQuest) {
    const replacementName = localizedName(replacementQuest)
    completionAnnouncement.value = replacementQuest.isCompleted
      ? t(
        `Quest swapped to ${replacementName} and completed. Reward credited.`,
        `Задание заменено на «${replacementName}» и выполнено. Награда начислена.`,
      )
      : t(
        `Quest swapped to ${replacementName}. Progress ${replacementQuest.current} of ${replacementQuest.target}.`,
        `Задание заменено на «${replacementName}». Прогресс: ${replacementQuest.current} из ${replacementQuest.target}.`,
      )
    void nextTick(() => {
      boardRoot.value
        ?.querySelector<HTMLElement>(`[data-quest-id="${replacementQuest.id}"]`)
        ?.focus({ preventScroll: true })
    })
  }

  if (newlyCompleted.length === 0) return

  const completedIds = new Set(newlyCompleted.map(quest => quest.id))
  justCompletedIds.value = completedIds
  const names = newlyCompleted.map(localizedName).join(', ')
  if (!replacementQuest) {
    completionAnnouncement.value = newlyCompleted.length === 1
      ? t(`Quest completed: ${names}. Reward credited.`, `Задание выполнено: ${names}. Награда начислена.`)
      : t(`Quests completed: ${names}. Rewards credited.`, `Задания выполнены: ${names}. Награды начислены.`)
  }
  if (completionTimer) clearTimeout(completionTimer)
  completionTimer = setTimeout(() => {
    justCompletedIds.value = new Set()
  }, 1_600)
}, { deep: true, immediate: true })

onMounted(() => {
  updateClock()
  clockTimer = setInterval(updateClock, 1_000)
})

onUnmounted(() => {
  if (clockTimer) clearInterval(clockTimer)
  if (completionTimer) clearTimeout(completionTimer)
})
</script>

<template>
  <section
    ref="boardRoot"
    class="daily-board"
    :class="{ 'is-complete': state?.allCompletedToday }"
    :aria-busy="loading"
    aria-labelledby="daily-quests-title"
  >
    <div class="daily-ambient" aria-hidden="true" />
    <div class="daily-grid-pattern" aria-hidden="true" />

    <header class="daily-hero">
      <div class="daily-heading">
        <span class="daily-kicker"><Sparkles :size="14" aria-hidden="true" /> {{ t('Today’s run', 'Маршрут на сегодня') }}</span>
        <h2 id="daily-quests-title">{{ t('Daily Quests', 'Ежедневные задания') }}</h2>
        <p>
          {{ t(
            'Three quick goals turn every match into progress. Rewards are credited automatically.',
            'Три быстрые цели превращают каждый матч в прогресс. Награды начисляются автоматически.',
          ) }}
        </p>
      </div>

      <div v-if="state" class="daily-hero-status">
        <div
          class="daily-ring"
          :style="{ '--daily-progress': `${completionPercent * 3.6}deg` }"
          role="progressbar"
          :aria-label="t('Daily quest completion', 'Выполнение ежедневных заданий')"
          :aria-valuenow="completedQuestCount"
          aria-valuemin="0"
          :aria-valuemax="Math.max(1, totalQuestCount)"
          :aria-valuetext="t(`${completedQuestCount} of ${totalQuestCount} quests completed`, `Выполнено заданий: ${completedQuestCount} из ${totalQuestCount}`)"
        >
          <span class="daily-ring-inner">
            <CheckCircle2 v-if="state.allCompletedToday" :size="22" aria-hidden="true" />
            <strong v-else>{{ completedQuestCount }}/{{ totalQuestCount }}</strong>
            <small>{{ state.allCompletedToday ? t('Done', 'Готово') : t('Today', 'Сегодня') }}</small>
          </span>
        </div>

        <div class="reset-clock">
          <Clock3 :size="17" aria-hidden="true" />
          <span>
            <small>{{ t('UTC reset in', 'Сброс UTC через') }}</small>
            <time :datetime="state.resetsAt">{{ formatCountdown(resetRemainingMs) }}</time>
          </span>
        </div>
      </div>
    </header>

    <div v-if="state" class="fairness-note">
      <ShieldCheck :size="18" aria-hidden="true" />
      <span>
        <strong>{{ t('Fair for random picks', 'Честно при случайном выборе') }}</strong>
        {{ t(
          'Every objective works with any randomly assigned character—none require a specific hero.',
          'Каждую цель можно выполнить за любого случайно выданного персонажа — конкретный герой не требуется.',
        ) }}
      </span>
    </div>

    <div v-if="state" class="daily-summary-grid">
      <article class="summary-card milestone-card" :class="{ achieved: state.dailyCompleted, claimed: state.dailyBonusGranted }">
        <span class="summary-icon daily-reward-icon"><Gift :size="21" aria-hidden="true" /></span>
        <span class="summary-copy">
          <small>{{ t('Daily reward', 'Награда дня') }} · {{ dailyRequirement }}/{{ totalQuestCount }}</small>
          <strong>+{{ state.dailyBonusZbs }} ZBS</strong>
          <span>{{ state.dailyBonusGranted ? t('Credited', 'Начислено') : t(`${Math.max(0, dailyRequirement - completedQuestCount)} quests to go`, `Осталось заданий: ${Math.max(0, dailyRequirement - completedQuestCount)}`) }}</span>
        </span>
        <Check v-if="state.dailyBonusGranted" :size="17" :stroke-width="3" aria-hidden="true" />
      </article>

      <article class="summary-card milestone-card mastery-card" :class="{ achieved: state.allCompletedToday, claimed: state.masteryBonusGranted }">
        <span class="summary-icon mastery-icon"><PackageOpen :size="21" aria-hidden="true" /></span>
        <span class="summary-copy">
          <small>{{ t('Daily mastery', 'Мастерство дня') }} · {{ totalQuestCount }}/{{ totalQuestCount }}</small>
          <strong>+{{ state.masteryBonusLootBoxes }} {{ t(state.masteryBonusLootBoxes === 1 ? 'loot box' : 'loot boxes', state.masteryBonusLootBoxes === 1 ? 'лутбокс' : 'лутбокса') }}</strong>
          <span>{{ state.masteryBonusGranted ? t('Added to inventory', 'Добавлено в инвентарь') : t('Complete the full set', 'Выполните весь набор') }}</span>
        </span>
        <Check v-if="state.masteryBonusGranted" :size="17" :stroke-width="3" aria-hidden="true" />
      </article>

      <article class="summary-card streak-card">
        <span class="summary-icon streak-icon"><Flame :size="21" aria-hidden="true" /></span>
        <span class="summary-copy">
          <small>{{ t('Current streak', 'Текущая серия') }}</small>
          <strong>{{ state.streakDays }} {{ t(state.streakDays === 1 ? 'day' : 'days', state.streakDays === 1 ? 'день' : 'дней') }}</strong>
          <span>{{ t(`Personal best: ${state.bestStreakDays}`, `Личный рекорд: ${state.bestStreakDays}`) }}</span>
        </span>
        <Award :size="19" aria-hidden="true" />
      </article>
    </div>

    <section v-if="state" class="weekly-panel" aria-labelledby="weekly-title">
      <div class="weekly-copy">
        <span class="weekly-icon"><CalendarClock :size="22" aria-hidden="true" /></span>
        <span>
          <small>{{ t('Weekly momentum', 'Недельный ритм') }}</small>
          <h3 id="weekly-title">{{ t(`Reach the daily reward on any ${weeklyTarget} days`, `Получите награду дня в любые ${weeklyTarget} дней`) }}</h3>
          <p v-if="state.weekEndsAt">
            {{ t('Week ends', 'Неделя завершится') }} {{ formatWeekEnd(state.weekEndsAt) }}
            <span aria-hidden="true">·</span>
            {{ formatCountdown(weekRemainingMs) }}
          </p>
        </span>
      </div>

      <div
        class="weekly-stamps-wrap"
        role="progressbar"
        :aria-label="t('Weekly quest progress', 'Недельный прогресс заданий')"
        :aria-valuenow="Math.min(weeklyCompleted, weeklyTarget)"
        aria-valuemin="0"
        :aria-valuemax="weeklyTarget"
        :aria-valuetext="t(`${weeklyCompleted} of ${weeklyTarget} required days completed`, `Выполнено дней: ${weeklyCompleted} из ${weeklyTarget}`)"
      >
        <div class="weekly-stamps" aria-hidden="true">
          <span
            v-for="day in weekStampCount"
            :key="day"
            class="weekly-stamp"
            :class="{ filled: day <= weeklyCompleted, goal: day === weeklyTarget }"
          >
            <Check v-if="day <= weeklyCompleted" :size="14" :stroke-width="3" />
            <span v-else>{{ day }}</span>
          </span>
        </div>
        <span class="weekly-track" aria-hidden="true"><span :style="{ width: `${weeklyProgressPercent}%` }" /></span>
      </div>

      <div class="weekly-reward" :class="{ claimed: state.weeklyRewardGranted }">
        <CheckCircle2 v-if="state.weeklyRewardGranted" :size="20" aria-hidden="true" />
        <Gift v-else :size="20" aria-hidden="true" />
        <span>
          <small>{{ state.weeklyRewardGranted ? t('Weekly reward credited', 'Недельная награда начислена') : t('Weekly reward', 'Награда недели') }}</small>
          <strong>+{{ state.weeklyRewardZbs }} ZBS</strong>
        </span>
      </div>
    </section>

    <div v-if="loading && !state" class="quest-loading" role="status">
      <span class="sr-only">{{ t('Loading daily quests', 'Загружаем ежедневные задания') }}</span>
      <div v-for="index in 3" :key="index" class="quest-skeleton">
        <span class="skeleton-icon" />
        <span class="skeleton-lines"><i /><i /><i /></span>
      </div>
    </div>

    <div v-else-if="error && !state" class="quest-state-message quest-error" role="alert">
      <CircleAlert :size="28" aria-hidden="true" />
      <strong>{{ t('Daily quests could not be loaded', 'Не удалось загрузить ежедневные задания') }}</strong>
      <span>{{ error }}</span>
      <button class="btn retry-button" type="button" @click="emit('retry')">
        <RefreshCw :size="16" aria-hidden="true" /> {{ t('Try again', 'Повторить') }}
      </button>
    </div>

    <div v-else-if="!state" class="quest-state-message quest-empty">
      <Route :size="29" aria-hidden="true" />
      <strong>{{ t('Your daily route is not loaded yet', 'Ваш маршрут на день ещё не загружен') }}</strong>
      <span>{{ t('Refresh to collect today’s universal objectives.', 'Обновите задания, чтобы получить общие цели на сегодня.') }}</span>
      <button class="btn retry-button" type="button" @click="emit('retry')">
        <RefreshCw :size="16" aria-hidden="true" /> {{ t('Load quests', 'Загрузить задания') }}
      </button>
    </div>

    <div v-else-if="state && quests.length === 0" class="quest-state-message quest-empty">
      <Route :size="29" aria-hidden="true" />
      <strong>{{ t('Today’s route is being prepared', 'Маршрут на сегодня готовится') }}</strong>
      <span>{{ t('Refresh to receive a new set of universal objectives.', 'Обновите задания, чтобы получить новый набор общих целей.') }}</span>
      <button class="btn retry-button" type="button" :disabled="loading" @click="emit('retry')">
        <LoaderCircle v-if="loading" :size="16" aria-hidden="true" />
        <RefreshCw v-else :size="16" aria-hidden="true" />
        {{ loading ? t('Refreshing…', 'Обновляем…') : t('Refresh quests', 'Обновить задания') }}
      </button>
    </div>

    <template v-else-if="state">
      <div v-if="error" class="inline-quest-error" role="alert">
        <CircleAlert :size="17" aria-hidden="true" />
        <span><strong>{{ t('Quest update failed', 'Не удалось обновить задания') }}</strong><small>{{ error }}</small></span>
        <button type="button" @click="emit('retry')">{{ t('Retry', 'Повторить') }}</button>
      </div>

      <div class="quest-list-heading">
        <div>
          <span class="section-kicker">{{ t('Your objectives', 'Ваши цели') }}</span>
          <h3>{{ t('Pick a match and make progress', 'Выберите матч и двигайтесь вперёд') }}</h3>
        </div>
        <span v-if="state.rerollsRemaining > 0" class="swap-allowance">
          <Dices :size="15" aria-hidden="true" />
          {{ t(`${state.rerollsRemaining} free swap`, `${state.rerollsRemaining} бесплатная замена`) }}
        </span>
        <span v-else class="swap-allowance spent">
          <Check :size="15" aria-hidden="true" /> {{ t('Free swap used', 'Бесплатная замена использована') }}
        </span>
      </div>

      <div class="quests-grid" role="list">
        <article
          v-for="quest in quests"
          :key="quest.id"
          :data-quest-id="quest.id"
          class="quest-card"
          :class="{
            completed: quest.isCompleted,
            claimed: quest.rewardGranted,
            'just-completed': justCompletedIds.has(quest.id),
            rerolling: rerollingQuestId === quest.id,
          }"
          role="listitem"
          tabindex="-1"
        >
          <div class="quest-card-glow" aria-hidden="true" />
          <header class="quest-card-header">
            <span class="quest-icon" aria-hidden="true">
              <component :is="iconFor(quest)" :size="25" :stroke-width="1.7" />
              <span v-if="quest.isCompleted" class="quest-check"><Check :size="11" :stroke-width="3.4" /></span>
            </span>
            <span class="quest-heading-copy">
              <span class="quest-labels">
                <span>{{ laneLabel(quest.lane) }}</span>
                <span>{{ aggregationLabel(quest.aggregation) }}</span>
              </span>
              <h4>{{ localizedName(quest) }}</h4>
            </span>
          </header>

          <p class="quest-description">{{ localizedDescription(quest) }}</p>

          <div class="quest-rewards" :aria-label="t('Quest rewards', 'Награды за задание')">
            <span v-if="quest.zbsReward > 0" class="reward-chip reward-zbs">
              <img :src="'/art/emojis/zbs.png'" alt="ZBS">
              <strong>+{{ quest.zbsReward }}</strong>
              <span>ZBS</span>
            </span>
            <span v-if="quest.rewardLootBoxes > 0" class="reward-chip reward-box">
              <PackageOpen :size="16" aria-hidden="true" />
              <strong>+{{ quest.rewardLootBoxes }}</strong>
              <span>{{ t(quest.rewardLootBoxes === 1 ? 'box' : 'boxes', quest.rewardLootBoxes === 1 ? 'лутбокс' : 'лутбокса') }}</span>
            </span>
            <span v-if="quest.rewardGranted" class="reward-status"><Check :size="13" :stroke-width="3" aria-hidden="true" /> {{ t('Credited', 'Начислено') }}</span>
          </div>

          <div class="quest-progress-block">
            <div class="quest-progress-label">
              <span>{{ quest.isCompleted ? t('Completed', 'Выполнено') : t('Progress', 'Прогресс') }}</span>
              <strong>{{ Math.min(quest.current, quest.target) }} / {{ quest.target }}</strong>
            </div>
            <div
              class="quest-progress-track"
              role="progressbar"
              :aria-label="t(`Progress for ${localizedName(quest)}`, `Прогресс задания «${localizedName(quest)}»`)"
              :aria-valuenow="boundedQuestCurrent(quest)"
              aria-valuemin="0"
              :aria-valuemax="questAriaMaximum(quest)"
              :aria-valuetext="t(`${boundedQuestCurrent(quest)} of ${questAriaMaximum(quest)}`, `${boundedQuestCurrent(quest)} из ${questAriaMaximum(quest)}`)"
            >
              <span :style="{ width: `${questProgress(quest)}%` }" />
            </div>
          </div>

          <footer class="quest-card-footer">
            <span v-if="quest.completedAt" class="completed-time">
              <CheckCircle2 :size="14" aria-hidden="true" />
              {{ t('Completed at', 'Выполнено в') }} {{ formatCompletedAt(quest.completedAt) }}
            </span>
            <span v-else class="settlement-hint">
              <Clock3 :size="14" aria-hidden="true" /> {{ t('Updates after a match ends', 'Обновится после завершения матча') }}
            </span>

            <button
              v-if="quest.canReroll && !quest.isCompleted && state.rerollsRemaining > 0"
              class="swap-button"
              type="button"
              :disabled="Boolean(rerollingQuestId) || loading"
              @click="reroll(quest)"
            >
              <LoaderCircle v-if="rerollingQuestId === quest.id" :size="15" aria-hidden="true" />
              <RefreshCw v-else :size="15" aria-hidden="true" />
              {{ rerollingQuestId === quest.id ? t('Swapping…', 'Заменяем…') : t('Free swap', 'Заменить бесплатно') }}
            </button>
          </footer>
        </article>
      </div>
    </template>

    <div class="sr-only" aria-live="polite" aria-atomic="true">{{ completionAnnouncement }}</div>
    <div v-if="loading && state" class="refresh-indicator" role="status" aria-live="polite">
      <LoaderCircle :size="14" aria-hidden="true" /> {{ t('Refreshing quests…', 'Обновляем задания…') }}
    </div>
  </section>
</template>

<style scoped>
.daily-board {
  --quest-accent: var(--accent-gold);
  position: relative;
  isolation: isolate;
  overflow: hidden;
  margin-bottom: 36px;
  padding: 22px;
  border: 1px solid var(--glass-border);
  border-radius: 18px;
  background:
    linear-gradient(145deg, rgba(240, 200, 80, 0.035), transparent 34%),
    var(--glass-bg);
  box-shadow: var(--shadow), inset 0 1px 0 var(--glass-highlight);
}

.daily-board.is-complete {
  border-color: rgba(63, 167, 61, 0.28);
  box-shadow: var(--shadow), 0 0 34px rgba(63, 167, 61, 0.08), inset 0 1px 0 var(--glass-highlight);
}

.daily-ambient {
  position: absolute;
  z-index: -2;
  width: 340px;
  height: 340px;
  top: -230px;
  right: -100px;
  border-radius: 50%;
  background: var(--accent-gold);
  filter: blur(68px);
  opacity: 0.12;
  pointer-events: none;
}

.daily-grid-pattern {
  position: absolute;
  z-index: -1;
  inset: 0;
  opacity: 0.1;
  pointer-events: none;
  background-image:
    linear-gradient(rgba(255, 255, 255, 0.06) 1px, transparent 1px),
    linear-gradient(90deg, rgba(255, 255, 255, 0.06) 1px, transparent 1px);
  background-size: 38px 38px;
  mask-image: linear-gradient(to bottom, black, transparent 46%);
}

.daily-hero {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;
}

.daily-heading {
  max-width: 590px;
}

.daily-kicker,
.section-kicker {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--accent-gold);
  font-size: 9px;
  font-weight: 850;
  letter-spacing: 1.5px;
  text-transform: uppercase;
}

.daily-heading h2 {
  margin-top: 3px;
  color: var(--text-primary);
  font-size: clamp(22px, 3vw, 30px);
  font-weight: 900;
  letter-spacing: -0.7px;
}

.daily-heading p {
  max-width: 540px;
  margin-top: 5px;
  color: var(--text-muted);
  font-size: 11px;
  line-height: 1.55;
}

.daily-hero-status {
  display: flex;
  align-items: center;
  gap: 14px;
  flex: 0 0 auto;
}

.daily-ring {
  --daily-progress: 0deg;
  width: 78px;
  height: 78px;
  display: grid;
  place-items: center;
  border-radius: 50%;
  background:
    radial-gradient(circle at center, var(--bg-card) 58%, transparent 60%),
    conic-gradient(var(--accent-gold) var(--daily-progress), rgba(255, 255, 255, 0.07) 0deg);
  box-shadow: 0 0 24px rgba(240, 200, 80, 0.1);
}

.is-complete .daily-ring {
  color: var(--accent-green);
  background:
    radial-gradient(circle at center, var(--bg-card) 58%, transparent 60%),
    conic-gradient(var(--accent-green) 360deg, rgba(255, 255, 255, 0.07) 0deg);
  box-shadow: 0 0 24px rgba(63, 167, 61, 0.15);
}

.daily-ring-inner {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}

.daily-ring strong {
  color: var(--text-primary);
  font: 900 17px/1 var(--font-mono);
}

.daily-ring small {
  margin-top: 4px;
  color: var(--text-dim);
  font-size: 8px;
  font-weight: 800;
  text-transform: uppercase;
}

.reset-clock {
  min-width: 124px;
  display: flex;
  align-items: center;
  gap: 9px;
  padding: 9px 11px;
  color: var(--accent-blue);
  border: 1px solid rgba(100, 180, 240, 0.17);
  border-radius: 11px;
  background: rgba(100, 180, 240, 0.06);
}

.reset-clock span {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.reset-clock small {
  color: var(--text-dim);
  font-size: 8px;
  font-weight: 750;
}

.reset-clock time {
  color: var(--text-primary);
  font: 850 12px/1 var(--font-mono);
  font-variant-numeric: tabular-nums;
}

.fairness-note {
  display: flex;
  align-items: flex-start;
  gap: 9px;
  margin-top: 17px;
  padding: 10px 12px;
  color: var(--accent-blue);
  border: 1px solid rgba(100, 180, 240, 0.14);
  border-radius: 10px;
  background: rgba(100, 180, 240, 0.045);
}

.fairness-note > span {
  color: var(--text-muted);
  font-size: 10px;
  line-height: 1.45;
}

.fairness-note strong {
  margin-right: 4px;
  color: var(--text-secondary);
}

.daily-summary-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  margin-top: 12px;
}

.summary-card {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 9px;
  padding: 10px;
  border: 1px solid var(--glass-border);
  border-radius: 11px;
  background: rgba(0, 0, 0, 0.13);
}

.summary-card > svg:last-child {
  margin-left: auto;
  color: var(--text-dim);
}

.summary-icon {
  width: 36px;
  height: 36px;
  display: grid;
  place-items: center;
  flex: 0 0 36px;
  border-radius: 9px;
}

.daily-reward-icon {
  color: var(--accent-green);
  background: rgba(63, 167, 61, 0.09);
}

.mastery-icon {
  color: var(--accent-purple);
  background: rgba(180, 150, 255, 0.09);
}

.streak-icon {
  color: var(--accent-gold);
  background: rgba(240, 200, 80, 0.09);
}

.summary-copy {
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.summary-copy small,
.summary-copy span {
  overflow: hidden;
  color: var(--text-dim);
  font-size: 8px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.summary-copy strong {
  overflow: hidden;
  margin: 2px 0;
  color: var(--text-primary);
  font: 850 11px/1.2 var(--font-mono);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.milestone-card.achieved {
  border-color: rgba(240, 200, 80, 0.22);
}

.milestone-card.claimed {
  border-color: rgba(63, 167, 61, 0.3);
  background: linear-gradient(135deg, rgba(63, 167, 61, 0.11), rgba(63, 167, 61, 0.035));
  box-shadow: inset 0 1px 0 rgba(110, 225, 109, 0.08);
}

.milestone-card.claimed > svg:last-child,
.milestone-card.claimed .summary-copy strong {
  color: var(--accent-green);
}

.weekly-panel {
  display: grid;
  grid-template-columns: minmax(190px, 0.9fr) minmax(250px, 1.3fr) minmax(145px, 0.65fr);
  align-items: center;
  gap: 16px;
  margin-top: 12px;
  padding: 13px 14px;
  border: 1px solid rgba(240, 200, 80, 0.15);
  border-radius: 12px;
  background: linear-gradient(100deg, rgba(240, 200, 80, 0.055), rgba(0, 0, 0, 0.13));
}

.weekly-copy {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 10px;
}

.weekly-icon {
  width: 39px;
  height: 39px;
  display: grid;
  place-items: center;
  flex: 0 0 39px;
  color: var(--accent-gold);
  border: 1px solid rgba(240, 200, 80, 0.17);
  border-radius: 10px;
  background: rgba(240, 200, 80, 0.07);
}

.weekly-copy small {
  color: var(--accent-gold);
  font-size: 8px;
  font-weight: 800;
  letter-spacing: 0.8px;
  text-transform: uppercase;
}

.weekly-copy h3 {
  margin-top: 1px;
  color: var(--text-primary);
  font-size: 12px;
  font-weight: 850;
}

.weekly-copy p {
  margin-top: 2px;
  color: var(--text-dim);
  font-size: 8px;
}

.weekly-stamps-wrap {
  min-width: 0;
}

.weekly-stamps {
  display: grid;
  grid-template-columns: repeat(7, minmax(23px, 1fr));
  gap: 5px;
}

.weekly-stamp {
  aspect-ratio: 1;
  display: grid;
  place-items: center;
  color: var(--text-dim);
  border: 1px dashed var(--border-subtle);
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.14);
  font: 750 8px/1 var(--font-mono);
}

.weekly-stamp.goal {
  border-style: solid;
  border-color: rgba(240, 200, 80, 0.4);
  box-shadow: 0 0 0 2px rgba(240, 200, 80, 0.05);
}

.weekly-stamp.filled {
  color: #151711;
  border-style: solid;
  border-color: var(--accent-green);
  background: var(--accent-green);
  box-shadow: 0 0 10px rgba(63, 167, 61, 0.18);
}

.weekly-track {
  display: block;
  height: 3px;
  margin-top: 7px;
  overflow: hidden;
  border-radius: 2px;
  background: var(--bg-inset);
}

.weekly-track > span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, var(--accent-green), var(--accent-gold));
  transition: width 0.45s ease;
}

.weekly-reward {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  color: var(--accent-gold);
}

.weekly-reward span {
  display: flex;
  flex-direction: column;
}

.weekly-reward small {
  color: var(--text-dim);
  font-size: 8px;
}

.weekly-reward strong {
  margin-top: 2px;
  font: 850 13px/1 var(--font-mono);
}

.weekly-reward.claimed {
  color: var(--accent-green);
}

.quest-list-heading {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 14px;
  margin: 22px 0 10px;
}

.quest-list-heading h3 {
  margin-top: 2px;
  color: var(--text-primary);
  font-size: 15px;
  font-weight: 850;
}

.swap-allowance {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 5px 8px;
  color: var(--accent-purple);
  border: 1px solid rgba(180, 150, 255, 0.18);
  border-radius: 9px;
  background: rgba(180, 150, 255, 0.07);
  font-size: 9px;
  font-weight: 750;
  white-space: nowrap;
}

.swap-allowance.spent {
  color: var(--text-dim);
  border-color: var(--glass-border);
  background: rgba(255, 255, 255, 0.025);
}

.quests-grid,
.quest-loading {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
}

.quest-card {
  position: relative;
  isolation: isolate;
  min-width: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding: 15px;
  border: 1px solid var(--glass-border);
  border-radius: 13px;
  background: linear-gradient(150deg, rgba(255, 255, 255, 0.025), transparent 42%), rgba(0, 0, 0, 0.14);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.035);
  transition: border-color 0.25s ease, box-shadow 0.25s ease, transform 0.25s ease;
}

.quest-card:focus-visible {
  outline: 2px solid var(--quest-accent);
  outline-offset: 3px;
}

.quest-card-glow {
  position: absolute;
  z-index: -1;
  width: 150px;
  height: 150px;
  top: -105px;
  right: -70px;
  border-radius: 50%;
  background: var(--accent-gold);
  filter: blur(35px);
  opacity: 0.08;
}

.quest-card.completed {
  border-color: rgba(63, 167, 61, 0.3);
  background: linear-gradient(145deg, rgba(63, 167, 61, 0.1), rgba(0, 0, 0, 0.13) 68%);
}

.quest-card.completed .quest-card-glow {
  background: var(--accent-green);
  opacity: 0.15;
}

.quest-card.claimed {
  box-shadow: 0 0 22px rgba(63, 167, 61, 0.07), inset 0 1px 0 rgba(110, 225, 109, 0.08);
}

.quest-card.rerolling {
  pointer-events: none;
}

.quest-card.rerolling::after {
  content: '';
  position: absolute;
  z-index: 4;
  inset: 0;
  background: rgba(10, 10, 15, 0.22);
  backdrop-filter: blur(1px);
}

.quest-card.just-completed {
  animation: quest-completed 0.75s var(--ease-spring);
}

.quest-card-header {
  display: flex;
  align-items: center;
  gap: 10px;
}

.quest-icon {
  position: relative;
  width: 43px;
  height: 43px;
  display: grid;
  place-items: center;
  flex: 0 0 43px;
  color: var(--accent-gold);
  border: 1px solid rgba(240, 200, 80, 0.17);
  border-radius: 11px;
  background: rgba(240, 200, 80, 0.07);
}

.completed .quest-icon {
  color: var(--accent-green);
  border-color: rgba(63, 167, 61, 0.24);
  background: rgba(63, 167, 61, 0.1);
}

.quest-check {
  position: absolute;
  right: -4px;
  bottom: -4px;
  width: 18px;
  height: 18px;
  display: grid;
  place-items: center;
  color: #151711;
  border: 2px solid var(--bg-card);
  border-radius: 50%;
  background: var(--accent-green);
}

.quest-heading-copy {
  min-width: 0;
}

.quest-labels {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.quest-labels span {
  padding: 2px 5px;
  color: var(--text-dim);
  border: 1px solid var(--glass-border);
  border-radius: 5px;
  background: rgba(255, 255, 255, 0.025);
  font-size: 7px;
  font-weight: 750;
  text-transform: uppercase;
}

.quest-heading-copy h4 {
  margin-top: 5px;
  color: var(--text-primary);
  font-size: 13px;
  font-weight: 850;
  line-height: 1.2;
}

.quest-description {
  min-height: 45px;
  margin-top: 11px;
  color: var(--text-muted);
  font-size: 10px;
  line-height: 1.5;
}

.quest-rewards {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 5px;
  margin-top: 9px;
}

.reward-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  min-height: 25px;
  padding: 3px 6px;
  border: 1px solid var(--glass-border);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.13);
}

.reward-chip img {
  width: 16px;
  height: 16px;
  object-fit: contain;
}

.reward-zbs {
  color: var(--accent-green);
}

.reward-box {
  color: var(--accent-purple);
}

.reward-chip strong {
  font: 850 9px/1 var(--font-mono);
}

.reward-chip span,
.reward-status {
  color: var(--text-dim);
  font-size: 7px;
  font-weight: 750;
}

.reward-status {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  margin-left: auto;
  color: var(--accent-green);
}

.quest-progress-block {
  margin-top: 13px;
}

.quest-progress-label {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 6px;
  color: var(--text-dim);
  font-size: 8px;
  font-weight: 750;
}

.quest-progress-label strong {
  color: var(--text-secondary);
  font: 800 9px/1 var(--font-mono);
}

.completed .quest-progress-label strong {
  color: var(--accent-green);
}

.quest-progress-track {
  height: 7px;
  overflow: hidden;
  border-radius: 5px;
  background: var(--bg-inset);
  box-shadow: inset 0 1px 2px rgba(0, 0, 0, 0.28);
}

.quest-progress-track > span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, var(--accent-gold), #f5df83);
  box-shadow: 0 0 9px rgba(240, 200, 80, 0.16);
  transition: width 0.48s ease;
}

.completed .quest-progress-track > span {
  background: linear-gradient(90deg, var(--accent-green), #79d878);
  box-shadow: 0 0 10px rgba(63, 167, 61, 0.2);
}

.quest-card-footer {
  min-height: 35px;
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 7px;
  margin-top: auto;
  padding-top: 11px;
}

.settlement-hint,
.completed-time {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  color: var(--text-dim);
  font-size: 7px;
  line-height: 1.3;
}

.completed-time {
  color: var(--accent-green);
}

.swap-button,
.retry-button {
  min-height: 31px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 5px;
  padding: 6px 8px;
  color: var(--accent-purple);
  border: 1px solid rgba(180, 150, 255, 0.22);
  border-radius: 8px;
  background: rgba(180, 150, 255, 0.07);
  font-size: 8px;
  font-weight: 800;
  cursor: pointer;
}

.swap-button:hover:not(:disabled),
.retry-button:hover:not(:disabled) {
  background: rgba(180, 150, 255, 0.14);
  box-shadow: 0 0 14px rgba(180, 150, 255, 0.1);
}

.swap-button:disabled,
.retry-button:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.quest-loading {
  margin-top: 20px;
}

.quest-skeleton {
  min-height: 210px;
  display: flex;
  gap: 11px;
  padding: 15px;
  border: 1px solid var(--glass-border);
  border-radius: 13px;
  background: rgba(0, 0, 0, 0.13);
}

.skeleton-icon,
.skeleton-lines i {
  display: block;
  border-radius: 8px;
  background: linear-gradient(90deg, var(--bg-inset), var(--bg-card), var(--bg-inset));
  background-size: 200% 100%;
  animation: quest-skeleton 1.4s linear infinite;
}

.skeleton-icon {
  width: 43px;
  height: 43px;
  flex: 0 0 43px;
}

.skeleton-lines {
  flex: 1;
}

.skeleton-lines i {
  height: 12px;
  margin-bottom: 10px;
}

.skeleton-lines i:nth-child(2) {
  width: 78%;
}

.skeleton-lines i:nth-child(3) {
  width: 56%;
  margin-top: 38px;
}

.quest-state-message {
  min-height: 190px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  margin-top: 20px;
  padding: 24px;
  text-align: center;
  color: var(--text-muted);
  border: 1px dashed var(--border-subtle);
  border-radius: 13px;
  background: rgba(0, 0, 0, 0.11);
}

.quest-state-message strong {
  color: var(--text-primary);
  font-size: 13px;
}

.quest-state-message span {
  max-width: 480px;
  font-size: 10px;
}

.quest-error > svg,
.inline-quest-error > svg {
  color: var(--accent-red);
}

.retry-button {
  margin-top: 5px;
}

.inline-quest-error {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 15px;
  padding: 9px 10px;
  color: var(--text-muted);
  border: 1px solid rgba(220, 80, 80, 0.2);
  border-radius: 9px;
  background: rgba(220, 80, 80, 0.06);
}

.inline-quest-error span {
  min-width: 0;
  display: flex;
  flex: 1;
  flex-direction: column;
  font-size: 9px;
}

.inline-quest-error strong {
  color: var(--text-secondary);
}

.inline-quest-error small {
  overflow: hidden;
  color: var(--text-dim);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.inline-quest-error button {
  color: var(--accent-red);
  border: 0;
  background: transparent;
  font-size: 9px;
  font-weight: 800;
  cursor: pointer;
}

.refresh-indicator {
  position: absolute;
  top: 10px;
  right: 12px;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 4px 7px;
  color: var(--text-dim);
  border: 1px solid var(--glass-border);
  border-radius: 8px;
  background: var(--bg-card);
  font-size: 8px;
}

.refresh-indicator svg,
.swap-button svg:first-child,
.retry-button svg:first-child {
  animation: quest-spin 0.9s linear infinite;
}

.swap-button svg:not(.lucide-loader-circle),
.retry-button svg:not(.lucide-loader-circle) {
  animation: none;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

@keyframes quest-completed {
  0% { transform: scale(1); }
  35% { transform: scale(1.035); border-color: var(--accent-green); box-shadow: 0 0 30px rgba(63, 167, 61, 0.23); }
  100% { transform: scale(1); }
}

@keyframes quest-skeleton {
  to { background-position: -200% 0; }
}

@keyframes quest-spin {
  to { transform: rotate(360deg); }
}

@media (max-width: 850px) {
  .daily-summary-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .streak-card {
    grid-column: 1 / -1;
  }

  .weekly-panel {
    grid-template-columns: 1fr 1.35fr;
  }

  .weekly-reward {
    grid-column: 1 / -1;
    justify-content: center;
    padding-top: 9px;
    border-top: 1px solid var(--glass-border);
  }
}

@media (max-width: 720px) {
  .daily-board {
    padding: 17px;
  }

  .daily-hero {
    align-items: flex-start;
    flex-direction: column;
  }

  .daily-hero-status {
    width: 100%;
    justify-content: space-between;
  }

  .quests-grid,
  .quest-loading {
    grid-template-columns: 1fr;
  }

  .quest-description {
    min-height: 0;
  }

  .quest-card-footer {
    min-height: 44px;
  }

  .swap-button {
    min-height: 44px;
    padding: 8px 11px;
  }
}

@media (max-width: 540px) {
  .daily-summary-grid,
  .weekly-panel {
    grid-template-columns: 1fr;
  }

  .streak-card,
  .weekly-reward {
    grid-column: auto;
  }

  .weekly-reward {
    justify-content: flex-start;
  }

  .quest-list-heading {
    align-items: flex-start;
    flex-direction: column;
  }

  .fairness-note {
    line-height: 1.5;
  }

  .weekly-stamps {
    gap: 7px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .quest-card,
  .quest-progress-track > span,
  .weekly-track > span {
    transition: none;
  }

  .quest-card.just-completed,
  .skeleton-icon,
  .skeleton-lines i,
  .refresh-indicator svg,
  .swap-button svg,
  .retry-button svg {
    animation: none;
  }
}
</style>
