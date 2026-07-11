<script setup lang="ts">
import { computed, onMounted, ref, type Component } from 'vue'
import {
  Check,
  ChevronRight,
  CircleAlert,
  Gift,
  Globe2,
  LockKeyhole,
  PackageOpen,
  Search,
  Sparkles,
  Trophy,
  UserRound,
  UsersRound,
} from 'lucide-vue-next'
import AchievementIcon from 'src/components/achievements/AchievementIcon.vue'
import { currentLocale } from 'src/i18n'
import type { AchievementEntry, CharacterListEntry } from 'src/services/signalr'
import { useGameStore } from 'src/store/game'

type CategoryFilter = 'all' | 'global' | 'character' | 'interaction'
type StatusFilter = 'all' | 'in-progress' | 'unlocked' | 'locked'

const store = useGameStore()
const searchQuery = ref('')
const selectedCategory = ref<CategoryFilter>('all')
const selectedStatus = ref<StatusFilter>('all')
const selectedRarity = ref('all')

const categoryTabs: Array<{
  key: CategoryFilter
  label: [string, string]
  icon: Component
}> = [
  { key: 'all', label: ['All', 'Все'], icon: Trophy },
  { key: 'global', label: ['Global', 'Общие'], icon: Globe2 },
  { key: 'character', label: ['Characters', 'Персонажи'], icon: UserRound },
  { key: 'interaction', label: ['Interactions', 'Взаимодействия'], icon: UsersRound },
]

const rarityOptions = ['all', 'common', 'uncommon', 'rare', 'epic', 'legendary']
const board = computed(() => store.achievementBoard)
const achievements = computed(() => board.value?.achievements ?? [])

const characterByName = computed(() => {
  const result = new Map<string, CharacterListEntry>()
  for (const character of store.characterList) result.set(character.name, character)
  return result
})

const progressPercent = computed(() => {
  const total = board.value?.totalAchievements ?? 0
  if (total <= 0) return 0
  return Math.round(((board.value?.totalUnlocked ?? 0) / total) * 100)
})

const totalRemaining = computed(() => Math.max(0,
  (board.value?.totalAchievements ?? 0) - (board.value?.totalUnlocked ?? 0),
))

const filteredAchievements = computed(() => {
  const query = searchQuery.value.trim().toLocaleLowerCase()
  return achievements.value.filter((achievement) => {
    const category = achievement.category.toLocaleLowerCase()
    if (selectedCategory.value !== 'all' && category !== selectedCategory.value) return false
    if (selectedRarity.value !== 'all' && rarityKey(achievement.rarity) !== selectedRarity.value) return false
    if (selectedStatus.value === 'unlocked' && !achievement.isUnlocked) return false
    if (selectedStatus.value === 'in-progress' && (achievement.isUnlocked || achievement.current <= 0)) return false
    if (selectedStatus.value === 'locked' && (achievement.isUnlocked || achievement.current > 0)) return false
    if (!query) return true
    const haystack = [
      achievement.name,
      achievement.nameRu,
      achievement.description,
      achievement.descriptionRu,
      achievement.secretHint,
      achievement.secretHintRu,
      ...achievement.characterNames,
    ].join(' ').toLocaleLowerCase()
    return haystack.includes(query)
  })
})

const nearestAchievements = computed(() => achievements.value
  .filter(achievement => !achievement.isUnlocked && achievement.target > 0 && !achievement.isSecret)
  .sort((a, b) => {
    const progressDelta = progressRatio(b) - progressRatio(a)
    if (progressDelta !== 0) return progressDelta
    return a.target - b.target
  })
  .slice(0, 3))

const categoryCounts = computed(() => {
  const result: Record<CategoryFilter, number> = {
    all: achievements.value.length,
    global: 0,
    character: 0,
    interaction: 0,
  }
  for (const achievement of achievements.value) {
    const key = achievement.category.toLocaleLowerCase() as CategoryFilter
    if (key in result) result[key]++
  }
  return result
})

onMounted(async () => {
  const requests: Promise<void>[] = [store.requestAchievements()]
  if (store.characterList.length === 0) requests.push(store.fetchCharacterList())
  await Promise.all(requests)
})

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

function localizedName(achievement: AchievementEntry): string {
  if (currentLocale.value === 'ru') return achievement.nameRu || achievement.name
  return achievement.name
}

function localizedDescription(achievement: AchievementEntry): string {
  if (achievement.isSecret && !achievement.isUnlocked) {
    if (currentLocale.value === 'ru') {
      return achievement.secretHintRu || achievement.descriptionRu || achievement.secretHint || achievement.description
    }
    return achievement.secretHint || achievement.description
  }
  if (currentLocale.value === 'ru') return achievement.descriptionRu || achievement.description
  return achievement.description
}

function rarityKey(rarity: string): string {
  const key = rarity.toLocaleLowerCase()
  return rarityOptions.includes(key) ? key : 'common'
}

function rarityLabel(rarity: string): string {
  const labels: Record<string, [string, string]> = {
    common: ['Common', 'Обычное'],
    uncommon: ['Uncommon', 'Необычное'],
    rare: ['Rare', 'Редкое'],
    epic: ['Epic', 'Эпическое'],
    legendary: ['Legendary', 'Легендарное'],
  }
  const label = labels[rarityKey(rarity)] ?? labels.common
  return t(label[0], label[1])
}

function categoryLabel(category: string): string {
  const labels: Record<string, [string, string]> = {
    global: ['Global', 'Общее'],
    character: ['Character', 'Персонаж'],
    interaction: ['Interaction', 'Взаимодействие'],
  }
  const label = labels[category.toLocaleLowerCase()] ?? [category, category]
  return t(label[0], label[1])
}

function progressRatio(achievement: AchievementEntry): number {
  if (achievement.target <= 0) return achievement.isUnlocked ? 100 : 0
  return Math.max(0, Math.min(100, (achievement.current / achievement.target) * 100))
}

function characterInfo(name: string): { name: string; avatar: string } {
  const character = characterByName.value.get(name)
  return { name, avatar: character?.avatar ?? '' }
}

function characterInitial(name: string): string {
  return name.trim().slice(0, 1).toLocaleUpperCase() || '?'
}

function unlockedDate(value: string | null): string {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return new Intl.DateTimeFormat(currentLocale.value === 'ru' ? 'ru-RU' : 'en-CA', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(date)
}

function chooseNearest(achievement: AchievementEntry): void {
  const category = achievement.category.toLocaleLowerCase() as CategoryFilter
  selectedCategory.value = category === 'global' || category === 'character' || category === 'interaction'
    ? category
    : 'all'
  selectedStatus.value = achievement.current > 0 ? 'in-progress' : 'all'
  searchQuery.value = localizedName(achievement)
  document.querySelector('.achievement-filters')?.scrollIntoView({
    behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
    block: 'start',
  })
}
</script>

<template>
  <section class="achievement-center" aria-labelledby="achievements-title">
    <header class="achievement-hero">
      <div class="hero-copy">
        <div class="eyebrow"><Sparkles :size="15" aria-hidden="true" /> {{ t('Hall of Feats', 'Зал подвигов') }}</div>
        <h1 id="achievements-title">{{ t('Achievements', 'Достижения') }}</h1>
        <p>
          {{ t(
            'Master the rules, uncover character stories, and create impossible matchups.',
            'Осваивайте правила, раскрывайте истории персонажей и создавайте невозможные противостояния.',
          ) }}
        </p>
      </div>
      <div class="hero-progress" :aria-label="t('Achievement completion', 'Прогресс достижений')">
        <div class="hero-ring" :style="{ '--achievement-progress': `${progressPercent * 3.6}deg` }">
          <div class="hero-ring-inner">
            <strong>{{ progressPercent }}%</strong>
            <span>{{ board?.totalUnlocked ?? 0 }}/{{ board?.totalAchievements ?? 0 }}</span>
          </div>
        </div>
      </div>
    </header>

    <div v-if="board" class="achievement-stats" aria-label="Achievement rewards summary">
      <div class="overview-stat">
        <span class="overview-icon overview-icon-trophy"><Trophy :size="20" aria-hidden="true" /></span>
        <span class="overview-copy">
          <small>{{ t('Unlocked', 'Открыто') }}</small>
          <strong>{{ board.totalUnlocked }}</strong>
          <span>{{ t(`${totalRemaining} left`, `Осталось: ${totalRemaining}`) }}</span>
        </span>
      </div>
      <div class="overview-stat">
        <span class="overview-icon overview-icon-zbs"><img :src="'/art/emojis/zbs.png'" alt=""></span>
        <span class="overview-copy">
          <small>{{ t('ZBS rewards', 'Награды ZBS') }}</small>
          <strong>{{ board.earnedRewardZbs }}</strong>
          <span>{{ t(`of ${board.totalRewardZbs} earned`, `получено из ${board.totalRewardZbs}`) }}</span>
        </span>
      </div>
      <div class="overview-stat">
        <span class="overview-icon overview-icon-box"><PackageOpen :size="21" aria-hidden="true" /></span>
        <span class="overview-copy">
          <small>{{ t('Loot-box rewards', 'Награды-лутбоксы') }}</small>
          <strong>{{ board.earnedRewardLootBoxes }}</strong>
          <span>{{ t(`of ${board.totalRewardLootBoxes} earned`, `получено из ${board.totalRewardLootBoxes}`) }}</span>
        </span>
      </div>
    </div>

    <section v-if="nearestAchievements.length" class="nearest-section" aria-labelledby="nearest-title">
      <div class="section-heading">
        <div>
          <span class="section-kicker">{{ t('Within reach', 'Уже близко') }}</span>
          <h2 id="nearest-title">{{ t('Nearest completions', 'Ближайшие достижения') }}</h2>
        </div>
      </div>
      <div class="nearest-grid">
        <button
          v-for="achievement in nearestAchievements"
          :key="achievement.id"
          class="nearest-card"
          type="button"
          @click="chooseNearest(achievement)"
        >
          <span class="nearest-icon" :class="`rarity-${rarityKey(achievement.rarity)}`">
            <AchievementIcon :icon="achievement.icon" :size="21" />
          </span>
          <span class="nearest-copy">
            <strong>{{ localizedName(achievement) }}</strong>
            <span>{{ achievement.current }} / {{ achievement.target }}</span>
            <span class="nearest-track" aria-hidden="true">
              <span :style="{ width: `${progressRatio(achievement)}%` }" />
            </span>
          </span>
          <ChevronRight :size="18" aria-hidden="true" />
        </button>
      </div>
    </section>

    <div class="achievement-filters">
      <div class="category-tabs" role="group" :aria-label="t('Achievement groups', 'Группы достижений')">
        <button
          v-for="tab in categoryTabs"
          :key="tab.key"
          class="category-tab"
          :class="{ active: selectedCategory === tab.key }"
          type="button"
          :aria-pressed="selectedCategory === tab.key"
          @click="selectedCategory = tab.key"
        >
          <component :is="tab.icon" :size="16" aria-hidden="true" />
          <span>{{ t(tab.label[0], tab.label[1]) }}</span>
          <span class="tab-count">{{ categoryCounts[tab.key] }}</span>
        </button>
      </div>

      <div class="filter-row">
        <label class="search-field">
          <span class="sr-only">{{ t('Search achievements', 'Поиск достижений') }}</span>
          <Search :size="17" aria-hidden="true" />
          <input
            v-model="searchQuery"
            type="search"
            :placeholder="t('Search achievements or characters…', 'Найти достижение или персонажа…')"
          >
        </label>
        <label class="select-field">
          <span>{{ t('Status', 'Статус') }}</span>
          <select v-model="selectedStatus">
            <option value="all">{{ t('Any status', 'Любой статус') }}</option>
            <option value="in-progress">{{ t('In progress', 'В процессе') }}</option>
            <option value="unlocked">{{ t('Unlocked', 'Открыто') }}</option>
            <option value="locked">{{ t('Not started', 'Не начато') }}</option>
          </select>
        </label>
        <label class="select-field">
          <span>{{ t('Rarity', 'Редкость') }}</span>
          <select v-model="selectedRarity">
            <option v-for="rarity in rarityOptions" :key="rarity" :value="rarity">
              {{ rarity === 'all' ? t('Any rarity', 'Любая редкость') : rarityLabel(rarity) }}
            </option>
          </select>
        </label>
      </div>
    </div>

    <div v-if="store.isAchievementsLoading && !board" class="achievement-loading" role="status">
      <span class="loading-orbit" aria-hidden="true"><Trophy :size="26" /></span>
      <strong>{{ t('Opening the Hall of Feats…', 'Открываем Зал подвигов…') }}</strong>
      <span>{{ t('Collecting your progress and rewards.', 'Собираем ваш прогресс и награды.') }}</span>
    </div>

    <div v-else-if="store.achievementsError && !board" class="achievement-error" role="alert">
      <CircleAlert :size="28" aria-hidden="true" />
      <strong>{{ t('Achievements could not be loaded', 'Не удалось загрузить достижения') }}</strong>
      <span>{{ store.achievementsError }}</span>
      <button class="btn btn-primary" type="button" @click="store.requestAchievements()">
        {{ t('Try again', 'Повторить') }}
      </button>
    </div>

    <div v-else-if="board && filteredAchievements.length === 0" class="achievement-empty">
      <Search :size="30" aria-hidden="true" />
      <strong>{{ t('Nothing matches these filters', 'По этим фильтрам ничего нет') }}</strong>
      <span>{{ t('Try another group, rarity, or search.', 'Измените группу, редкость или запрос.') }}</span>
      <button
        class="btn btn-ghost"
        type="button"
        @click="searchQuery = ''; selectedStatus = 'all'; selectedRarity = 'all'; selectedCategory = 'all'"
      >
        {{ t('Clear filters', 'Сбросить фильтры') }}
      </button>
    </div>

    <div v-else-if="board" class="achievements-grid" role="list" :aria-busy="store.isAchievementsLoading">
      <article
        v-for="achievement in filteredAchievements"
        :key="achievement.id"
        class="achievement-card"
        :class="[
          `rarity-${rarityKey(achievement.rarity)}`,
          { unlocked: achievement.isUnlocked, secret: achievement.isSecret && !achievement.isUnlocked },
        ]"
        role="listitem"
      >
        <div class="card-accent" aria-hidden="true" />
        <header class="achievement-card-header">
          <div class="achievement-icon-wrap" aria-hidden="true">
            <LockKeyhole v-if="achievement.isSecret && !achievement.isUnlocked" :size="25" />
            <AchievementIcon v-else :icon="achievement.icon" :size="27" />
            <span v-if="achievement.isUnlocked" class="unlock-check"><Check :size="11" :stroke-width="3" /></span>
          </div>
          <div class="card-heading-copy">
            <div class="card-labels">
              <span class="category-pill">{{ categoryLabel(achievement.category) }}</span>
              <span class="rarity-pill">{{ rarityLabel(achievement.rarity) }}</span>
            </div>
            <h3>{{ localizedName(achievement) }}</h3>
          </div>
        </header>

        <div v-if="achievement.characterNames.length" class="character-chips" :aria-label="t('Characters', 'Персонажи')">
          <span
            v-for="name in achievement.characterNames"
            :key="name"
            class="character-chip"
          >
            <span class="character-avatar">
              <img v-if="characterInfo(name).avatar" :src="characterInfo(name).avatar" :alt="name">
              <span v-else>{{ characterInitial(name) }}</span>
            </span>
            <span>{{ name }}</span>
          </span>
        </div>

        <p class="achievement-description">{{ localizedDescription(achievement) }}</p>

        <div class="card-progress">
          <div class="progress-label">
            <span>{{ achievement.isUnlocked ? t('Completed', 'Выполнено') : t('Progress', 'Прогресс') }}</span>
            <strong>{{ achievement.current }} / {{ achievement.target }}</strong>
          </div>
          <div
            class="progress-track"
            role="progressbar"
            :aria-label="t(`Progress for ${localizedName(achievement)}`, `Прогресс: ${localizedName(achievement)}`)"
            :aria-valuemin="0"
            :aria-valuemax="achievement.target"
            :aria-valuenow="Math.min(achievement.current, achievement.target)"
          >
            <span :style="{ width: `${progressRatio(achievement)}%` }" />
          </div>
        </div>

        <footer class="achievement-card-footer">
          <div class="achievement-rewards" :aria-label="t('Rewards', 'Награды')">
            <span v-if="achievement.rewardZbs > 0" class="reward-chip reward-zbs">
              <img :src="'/art/emojis/zbs.png'" alt="ZBS">
              <strong>+{{ achievement.rewardZbs }}</strong>
            </span>
            <span v-if="achievement.rewardLootBoxes > 0" class="reward-chip reward-box">
              <Gift :size="15" aria-hidden="true" />
              <strong>+{{ achievement.rewardLootBoxes }}</strong>
            </span>
            <span v-if="achievement.rewardZbs <= 0 && achievement.rewardLootBoxes <= 0" class="reward-none">
              {{ t('Feat only', 'Только подвиг') }}
            </span>
          </div>
          <span v-if="achievement.isUnlocked && unlockedDate(achievement.unlockedAt)" class="unlock-date">
            {{ unlockedDate(achievement.unlockedAt) }}
          </span>
          <span v-else-if="achievement.isUnlocked" class="unlock-state">
            <Check :size="13" aria-hidden="true" /> {{ t('Unlocked', 'Открыто') }}
          </span>
          <span v-else class="locked-state">
            <LockKeyhole :size="13" aria-hidden="true" /> {{ t('Locked', 'Закрыто') }}
          </span>
        </footer>
      </article>
    </div>
  </section>
</template>

<style scoped>
.achievement-center {
  --ach-common: #b5bcc4;
  --ach-uncommon: #67d391;
  --ach-rare: #69adff;
  --ach-epic: #c68cff;
  --ach-legendary: #f3c85b;
  width: min(1180px, 100%);
  margin: 0 auto;
  padding-bottom: 56px;
}

.achievement-hero {
  position: relative;
  isolation: isolate;
  min-height: 224px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 32px;
  overflow: hidden;
  padding: 36px 42px;
  border: 1px solid var(--glass-border);
  border-radius: 20px;
  background:
    radial-gradient(circle at 83% 48%, rgba(240, 200, 80, 0.18), transparent 24%),
    radial-gradient(circle at 8% 5%, rgba(180, 150, 255, 0.12), transparent 35%),
    linear-gradient(135deg, rgba(42, 39, 49, 0.98), rgba(22, 21, 28, 0.96));
  box-shadow: var(--shadow-lg), inset 0 1px 0 rgba(255, 255, 255, 0.05);
}

.achievement-hero::before,
.achievement-hero::after {
  content: '';
  position: absolute;
  z-index: -1;
  border: 1px solid rgba(240, 200, 80, 0.08);
  border-radius: 50%;
}

.achievement-hero::before { width: 280px; height: 280px; right: -56px; top: -80px; }
.achievement-hero::after { width: 190px; height: 190px; right: -12px; top: -36px; }

.hero-copy { max-width: 680px; }
.eyebrow,
.section-kicker {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  color: var(--accent-gold);
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 2px;
  text-transform: uppercase;
}

.hero-copy h1 {
  margin: 6px 0 8px;
  color: var(--text-primary);
  font-size: clamp(34px, 5vw, 54px);
  font-weight: 900;
  line-height: 1;
  letter-spacing: -1.8px;
}

.hero-copy p {
  max-width: 620px;
  color: var(--text-muted);
  font-size: 14px;
  line-height: 1.65;
}

.hero-ring {
  --achievement-progress: 0deg;
  width: 142px;
  height: 142px;
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  border-radius: 50%;
  background: conic-gradient(var(--accent-gold) var(--achievement-progress), rgba(255, 255, 255, 0.08) 0deg);
  box-shadow: 0 0 42px rgba(240, 200, 80, 0.12);
  animation: ring-arrive 0.7s var(--ease-spring) both;
}

.hero-ring-inner {
  width: 116px;
  height: 116px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-direction: column;
  border: 1px solid rgba(255, 255, 255, 0.06);
  border-radius: 50%;
  background: var(--bg-inset);
}

.hero-ring strong { color: var(--text-primary); font: 900 30px/1 var(--font-mono); }
.hero-ring span { margin-top: 7px; color: var(--text-muted); font: 700 11px/1 var(--font-mono); }

.achievement-stats {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 10px;
  margin: 12px 0 34px;
}

.overview-stat {
  display: flex;
  align-items: center;
  gap: 13px;
  min-width: 0;
  padding: 14px 16px;
  border: 1px solid var(--glass-border);
  border-radius: 12px;
  background: var(--glass-bg);
  box-shadow: inset 0 1px 0 var(--glass-highlight);
}

.overview-icon {
  width: 42px;
  height: 42px;
  display: grid;
  place-items: center;
  flex: 0 0 42px;
  border-radius: 11px;
}

.overview-icon-trophy { color: var(--accent-gold); background: rgba(240, 200, 80, 0.11); }
.overview-icon-zbs { background: rgba(63, 167, 61, 0.12); }
.overview-icon-zbs img { width: 25px; height: 25px; object-fit: contain; }
.overview-icon-box { color: var(--accent-purple); background: rgba(180, 150, 255, 0.12); }
.overview-copy { min-width: 0; display: grid; grid-template-columns: auto 1fr; column-gap: 8px; align-items: baseline; }
.overview-copy small { grid-column: 1 / -1; color: var(--text-muted); font-size: 10px; font-weight: 800; letter-spacing: 0.7px; text-transform: uppercase; }
.overview-copy strong { color: var(--text-primary); font: 900 22px/1.3 var(--font-mono); }
.overview-copy > span { overflow: hidden; color: var(--text-dim); font-size: 10px; text-overflow: ellipsis; white-space: nowrap; }

.nearest-section { margin-bottom: 32px; }
.section-heading { display: flex; align-items: flex-end; justify-content: space-between; margin-bottom: 11px; }
.section-heading h2 { margin-top: 2px; color: var(--text-primary); font-size: 18px; font-weight: 850; }
.nearest-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 9px; }
.nearest-card {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 11px;
  padding: 11px 12px;
  color: var(--text-muted);
  border: 1px solid var(--glass-border);
  border-radius: 11px;
  background: var(--glass-bg);
  text-align: left;
}
.nearest-card:hover { border-color: var(--border-color); background: var(--bg-card); transform: translateY(-2px); }
.nearest-icon { width: 38px; height: 38px; display: grid; place-items: center; flex: 0 0 38px; border-radius: 10px; color: var(--rarity); background: color-mix(in srgb, var(--rarity) 12%, transparent); }
.nearest-copy { min-width: 0; flex: 1; display: flex; flex-direction: column; }
.nearest-copy strong { overflow: hidden; color: var(--text-primary); font-size: 12px; font-weight: 800; text-overflow: ellipsis; white-space: nowrap; }
.nearest-copy > span:not(.nearest-track) { color: var(--text-muted); font: 700 10px/1.6 var(--font-mono); }
.nearest-track { height: 3px; overflow: hidden; border-radius: 2px; background: var(--bg-inset); }
.nearest-track span { display: block; height: 100%; border-radius: inherit; background: var(--rarity); }

.achievement-filters {
  position: sticky;
  z-index: 10;
  top: 8px;
  margin-bottom: 14px;
  padding: 10px;
  border: 1px solid var(--glass-border);
  border-radius: 14px;
  background: var(--glass-bg-heavy);
  box-shadow: var(--shadow);
  backdrop-filter: blur(18px);
}

.category-tabs { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 6px; }
.category-tab {
  min-width: 0;
  min-height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 7px;
  padding: 7px 10px;
  color: var(--text-muted);
  border: 1px solid transparent;
  border-radius: 9px;
  background: transparent;
  font-size: 11px;
  font-weight: 750;
}
.category-tab:hover { color: var(--text-primary); background: rgba(255, 255, 255, 0.035); }
.category-tab.active { color: var(--text-primary); border-color: rgba(240, 200, 80, 0.24); background: rgba(240, 200, 80, 0.1); box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.04); }
.tab-count { min-width: 19px; padding: 1px 5px; border-radius: 9px; color: var(--text-dim); background: var(--bg-inset); font: 700 9px/1.4 var(--font-mono); }
.category-tab.active .tab-count { color: var(--accent-gold); }

.filter-row { display: grid; grid-template-columns: minmax(230px, 1fr) auto auto; gap: 8px; margin-top: 8px; }
.search-field,
.select-field {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 39px;
  padding: 0 11px;
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  color: var(--text-dim);
  background: var(--bg-inset);
}
.search-field:focus-within,
.select-field:focus-within { border-color: var(--accent-gold-dim); box-shadow: 0 0 0 2px rgba(240, 200, 80, 0.08); }
.search-field input { width: 100%; min-width: 0; border: 0; outline: 0; color: var(--text-primary); background: transparent; font-size: 12px; }
.search-field input::placeholder { color: var(--text-dim); }
.select-field > span { color: var(--text-dim); font-size: 9px; font-weight: 800; letter-spacing: 0.5px; text-transform: uppercase; }
.select-field select { border: 0; outline: 0; color: var(--text-secondary); background: transparent; font-size: 11px; font-weight: 700; }
.select-field option { color: var(--text-primary); background: var(--bg-card); }

.achievements-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
.achievement-card {
  --rarity: var(--ach-common);
  position: relative;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
  overflow: hidden;
  padding: 16px;
  border: 1px solid var(--glass-border);
  border-radius: 13px;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--rarity) 5%, transparent), transparent 42%),
    var(--glass-bg);
  box-shadow: var(--shadow), inset 0 1px 0 var(--glass-highlight);
  transition: transform 0.22s var(--ease-out), border-color 0.22s, box-shadow 0.22s;
}
.achievement-card:hover { transform: translateY(-2px); border-color: color-mix(in srgb, var(--rarity) 38%, var(--border-subtle)); box-shadow: var(--shadow-lg), 0 0 22px color-mix(in srgb, var(--rarity) 8%, transparent); }
.achievement-card:not(.unlocked) { filter: saturate(0.7); }
.achievement-card.secret { background: linear-gradient(135deg, rgba(148, 156, 164, 0.04), transparent 45%), var(--glass-bg); }
.card-accent { position: absolute; inset: 0 auto 0 0; width: 3px; background: linear-gradient(180deg, var(--rarity), transparent 90%); opacity: 0.85; }

.rarity-common { --rarity: var(--ach-common); }
.rarity-uncommon { --rarity: var(--ach-uncommon); }
.rarity-rare { --rarity: var(--ach-rare); }
.rarity-epic { --rarity: var(--ach-epic); }
.rarity-legendary { --rarity: var(--ach-legendary); }

.achievement-card-header { display: flex; align-items: flex-start; gap: 12px; }
.achievement-icon-wrap {
  position: relative;
  width: 49px;
  height: 49px;
  display: grid;
  place-items: center;
  flex: 0 0 49px;
  color: var(--rarity);
  border: 1px solid color-mix(in srgb, var(--rarity) 30%, transparent);
  border-radius: 12px;
  background: color-mix(in srgb, var(--rarity) 9%, var(--bg-inset));
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.04);
}
.unlock-check { position: absolute; right: -4px; bottom: -4px; width: 19px; height: 19px; display: grid; place-items: center; color: #102417; border: 2px solid var(--bg-card); border-radius: 50%; background: var(--accent-green); }
.card-heading-copy { min-width: 0; flex: 1; }
.card-labels { display: flex; align-items: center; gap: 5px; margin-bottom: 5px; }
.category-pill,
.rarity-pill { padding: 2px 6px; border-radius: 5px; font-size: 8px; font-weight: 850; letter-spacing: 0.55px; text-transform: uppercase; }
.category-pill { color: var(--text-muted); background: rgba(255, 255, 255, 0.04); }
.rarity-pill { color: var(--rarity); border: 1px solid color-mix(in srgb, var(--rarity) 22%, transparent); background: color-mix(in srgb, var(--rarity) 8%, transparent); }
.card-heading-copy h3 { color: var(--text-primary); font-size: 15px; font-weight: 850; line-height: 1.25; }

.character-chips { display: flex; flex-wrap: wrap; gap: 6px; }
.character-chip { display: inline-flex; align-items: center; gap: 6px; min-width: 0; padding: 3px 8px 3px 3px; color: var(--text-secondary); border: 1px solid var(--glass-border); border-radius: 15px; background: rgba(255, 255, 255, 0.035); font-size: 10px; font-weight: 700; }
.character-avatar { width: 24px; height: 24px; display: grid; place-items: center; overflow: hidden; flex: 0 0 24px; color: var(--accent-gold); border-radius: 50%; background: var(--bg-inset); font-size: 10px; font-weight: 900; }
.character-avatar img { width: 100%; height: 100%; object-fit: cover; }

.achievement-description { flex: 1; color: var(--text-muted); font-size: 11px; line-height: 1.55; }
.progress-label { display: flex; justify-content: space-between; margin-bottom: 5px; color: var(--text-dim); font-size: 9px; font-weight: 750; text-transform: uppercase; }
.progress-label strong { color: var(--text-muted); font: 750 9px/1 var(--font-mono); }
.progress-track { height: 5px; overflow: hidden; border-radius: 3px; background: var(--bg-inset); box-shadow: inset 0 1px 2px rgba(0, 0, 0, 0.35); }
.progress-track > span { display: block; height: 100%; border-radius: inherit; background: linear-gradient(90deg, color-mix(in srgb, var(--rarity) 70%, #fff), var(--rarity)); box-shadow: 0 0 9px color-mix(in srgb, var(--rarity) 40%, transparent); transition: width 0.6s var(--ease-out); }

.achievement-card-footer { min-height: 27px; display: flex; align-items: center; justify-content: space-between; gap: 10px; padding-top: 10px; border-top: 1px solid var(--glass-border); }
.achievement-rewards { display: flex; align-items: center; gap: 5px; }
.reward-chip { display: inline-flex; align-items: center; gap: 4px; padding: 3px 7px; border-radius: 7px; font: 800 10px/1.2 var(--font-mono); }
.reward-chip img { width: 15px; height: 15px; object-fit: contain; }
.reward-zbs { color: var(--accent-green); background: rgba(63, 167, 61, 0.1); }
.reward-box { color: var(--accent-purple); background: rgba(180, 150, 255, 0.1); }
.reward-none { color: var(--text-dim); font-size: 9px; font-style: italic; }
.unlock-date,
.unlock-state,
.locked-state { display: inline-flex; align-items: center; gap: 4px; flex: 0 0 auto; color: var(--text-dim); font-size: 9px; font-weight: 700; }
.unlock-state { color: var(--accent-green); }

.achievement-loading,
.achievement-error,
.achievement-empty {
  min-height: 280px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-direction: column;
  gap: 8px;
  padding: 30px;
  color: var(--text-muted);
  border: 1px dashed var(--border-subtle);
  border-radius: 14px;
  text-align: center;
}
.achievement-loading strong,
.achievement-error strong,
.achievement-empty strong { color: var(--text-primary); font-size: 14px; font-weight: 800; }
.achievement-loading > span:not(.loading-orbit),
.achievement-error > span,
.achievement-empty > span { font-size: 11px; }
.achievement-error { color: var(--accent-red); }
.achievement-error .btn,
.achievement-empty .btn { margin-top: 8px; }
.loading-orbit { width: 54px; height: 54px; display: grid; place-items: center; color: var(--accent-gold); border: 1px solid rgba(240, 200, 80, 0.22); border-radius: 50%; animation: loading-pulse 1.4s ease-in-out infinite; }

.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; margin: -1px; padding: 0; clip: rect(0, 0, 0, 0); border: 0; white-space: nowrap; }

@keyframes ring-arrive { from { opacity: 0; transform: scale(0.65) rotate(-30deg); } to { opacity: 1; transform: scale(1) rotate(0); } }
@keyframes loading-pulse { 0%, 100% { transform: scale(0.94); opacity: 0.6; } 50% { transform: scale(1.04); opacity: 1; box-shadow: var(--glow-gold); } }

@media (max-width: 820px) {
  .achievement-hero { min-height: 190px; padding: 28px; }
  .hero-ring { width: 116px; height: 116px; }
  .hero-ring-inner { width: 94px; height: 94px; }
  .hero-ring strong { font-size: 24px; }
  .achievement-stats { grid-template-columns: 1fr; }
  .nearest-grid { grid-template-columns: 1fr; }
  .achievements-grid { grid-template-columns: 1fr; }
}

@media (max-width: 620px) {
  .achievement-center { padding-bottom: 28px; }
  .achievement-hero { min-height: 0; align-items: flex-start; padding: 23px 20px; border-radius: 14px; }
  .hero-copy h1 { font-size: 34px; }
  .hero-copy p { font-size: 12px; }
  .hero-ring { width: 86px; height: 86px; }
  .hero-ring-inner { width: 70px; height: 70px; }
  .hero-ring strong { font-size: 18px; }
  .hero-ring span { margin-top: 4px; font-size: 8px; }
  .achievement-stats { margin-bottom: 26px; }
  .achievement-filters { top: 4px; padding: 7px; border-radius: 11px; }
  .category-tabs { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .category-tab { min-height: 44px; }
  .filter-row { grid-template-columns: 1fr 1fr; }
  .search-field { grid-column: 1 / -1; min-height: 44px; }
  .select-field { min-height: 44px; flex-direction: column; align-items: flex-start; justify-content: center; gap: 0; }
  .select-field select { width: 100%; }
  .achievement-card { padding: 14px; }
}

@media (max-width: 410px) {
  .achievement-hero { gap: 12px; }
  .hero-ring { width: 74px; height: 74px; }
  .hero-ring-inner { width: 60px; height: 60px; }
  .hero-copy h1 { font-size: 29px; }
  .hero-copy p { display: none; }
  .overview-stat { padding: 11px; }
  .achievement-card-footer { align-items: flex-start; flex-direction: column; }
}

@media (prefers-reduced-motion: reduce) {
  .hero-ring,
  .loading-orbit { animation: none; }
  .achievement-card,
  .nearest-card,
  .progress-track > span { transition: none; }
  .achievement-card:hover,
  .nearest-card:hover { transform: none; }
}
</style>
