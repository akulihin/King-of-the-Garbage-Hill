<script setup lang="ts">
import { computed } from 'vue'
import type { DoomGuyState, DopaState, GeraltState, GoblinSwarmState, Player, TheBoysState } from 'src/services/signalr'
import { formatPassiveDescription } from 'src/services/textFormatting'

type LevelUpIndex = 1 | 2 | 3 | 4

type Choice = {
  index: LevelUpIndex
  icon: string
  title: string
  kicker: string
  description: string
  outcome: string
  badge?: string
  disabled?: boolean
}

const props = defineProps<{
  player: Player
  roundNo: number
  submitting: boolean
}>()

const emit = defineEmits<{
  choose: [index: LevelUpIndex]
}>()

const states = computed(() => props.player.passiveAbilityStates)
const goblin = computed<GoblinSwarmState | null>(() => states.value?.goblinSwarm ?? null)
const geralt = computed<GeraltState | null>(() => states.value?.geralt ?? null)
const theBoys = computed<TheBoysState | null>(() => states.value?.theBoys ?? null)
const doom = computed<DoomGuyState | null>(() => states.value?.doomGuy ?? null)
const dopa = computed<DopaState | null>(() => states.value?.dopa ?? null)

const hasPassive = (name: string) => props.player.character.passives.some(passive => passive.name === name)

const kind = computed(() => {
  const name = props.player.character.name
  if (name === 'DooM Guy' && doom.value) return 'doom'
  if (name === 'Стая Гоблинов' && goblin.value) return 'goblin'
  if (name === 'Геральт' && geralt.value) return 'geralt'
  if (name === 'TheBoys' && theBoys.value) return 'theboys'
  if (name === 'Dopa' && dopa.value?.metaChoiceReady) return 'dopa'
  if (name === 'Котики' && states.value?.kotiki) return 'kotiki'
  if (hasPassive('Vampyr Позорный')) return 'vampyr'
  if (hasPassive('Main Ирелия')) return 'irelia'
  if (hasPassive('Закуп')) return 'seller'
  return ''
})

const stageOrder = ['Rune', 'Shield', 'Mission', 'Gun'] as const
const stageMeta: Record<string, { icon: string; ru: string; round: number }> = {
  Rune: { icon: 'ᛟ', ru: 'Руна', round: 3 },
  Shield: { icon: '⬡', ru: 'Щит', round: 5 },
  Mission: { icon: '⌖', ru: 'Миссия', round: 7 },
  Gun: { icon: '▰', ru: 'Оружие', round: 9 },
}

const inferredDoomStage = computed(() => doom.value?.currentStage || ({ 3: 'Rune', 5: 'Shield', 7: 'Mission', 9: 'Gun' } as Record<number, string>)[props.roundNo] || '')

const header = computed(() => {
  switch (kind.value) {
    case 'doom': {
      const meta = stageMeta[inferredDoomStage.value]
      return { eyebrow: `FORTRESS // ROUND ${props.roundNo}`, title: meta ? `${meta.icon} ${meta.ru}: выбери модуль` : 'Выбери модуль', hint: 'Один выбор навсегда определит этот этап матча.' }
    }
    case 'goblin': return { eyebrow: 'СОВЕТ СТАИ', title: 'Куда пустить новых гоблинов?', hint: 'Сравни текущее производство с результатом следующего уровня.' }
    case 'geralt': return { eyebrow: 'ВЕДЬМАЧЬЯ АЛХИМИЯ', title: 'Подготовить масло', hint: 'Масло срабатывает после Медитации против соответствующего типа чудовищ.' }
    case 'theboys': return { eyebrow: 'VOUGHT FIELD TEAM', title: theBoys.value?.superDickActive ? 'Butcher работает один' : 'Кого прокачать?', hint: theBoys.value?.superDickActive ? 'СуперМудень отключил остальные ветки. Следующее очко будет потрачено без усиления.' : 'Каждый выбор даёт +2 к связанному стату. Первые четыре выбора могут открыть скрытую комбинацию, а x4 одной ветки — ультимейт.' }
    case 'dopa': return { eyebrow: 'DOPA // ВТОРОЕ УЛУЧШЕНИЕ', title: 'Выбрать мету', hint: 'Этот выбор заменяет улучшение характеристики: стат не повысится.' }
    case 'kotiki': return { eyebrow: 'LVL-МЯК', title: 'Справедливости много не бывает', hint: 'Котики не качают статы — это очко сразу превращается в живую Справедливость.' }
    case 'vampyr': return { eyebrow: 'VAMPYR ПОЗОРНЫЙ', title: 'Никаких статов для тебя', hint: 'Выбор характеристики — обманка: очко тратится, но ни один стат не растёт.' }
    case 'irelia': return { eyebrow: 'RIOT BALANCE TEAM', title: 'Нерфа не избежать', hint: 'Выбери характеристику, которая потеряет 1. Очко прокачки будет потрачено.' }
    case 'seller': return { eyebrow: 'ЗАКУП', title: 'Оптовая прокачка', hint: 'Продавец вкладывается по-крупному: выбранный стат получает +10 вместо +1.' }
    default: return { eyebrow: '', title: '', hint: '' }
  }
})

function cappedStat(value: number, delta: number) {
  return Math.max(0, Math.min(10, value + delta))
}

function statChoices(delta: number, label: string): Choice[] {
  const c = props.player.character
  const stats = [
    { index: 1 as const, icon: 'INT', title: 'Интеллект', value: c.intelligence },
    { index: 2 as const, icon: 'STR', title: 'Сила', value: c.strength },
    { index: 3 as const, icon: 'SPD', title: 'Скорость', value: c.speed },
    { index: 4 as const, icon: 'PSY', title: 'Психика', value: c.psyche },
  ]
  return stats.map((stat) => {
    const capped = delta > 0 && stat.value >= 10 && stats.some(other => other.index !== stat.index && other.value <= 9)
    return {
      index: stat.index,
      icon: stat.icon,
      title: stat.title,
      kicker: label,
      description: capped ? 'Этот стат уже максимален — вложись в другой.' : delta > 0 ? 'Мгновенное крупное усиление.' : 'Риоты уже подготовили патчноут.',
      outcome: capped ? 'Максимальный уровень' : `${stat.value} → ${cappedStat(stat.value, delta)}`,
      badge: capped ? 'MAX' : delta > 0 ? '+10' : '−1',
      disabled: capped,
    }
  })
}

function geraltTierName(tier: number) {
  return ['Нет', 'Масло', 'Улучшенное', 'Отличное'][tier] ?? 'Максимум'
}

function geraltTierEffect(tier: number) {
  if (tier === 1) return 'Открывает: −1 Справедливость врагу'
  if (tier === 2) return 'Добавляет: +2 Силы в атаке'
  return 'Добавляет: ×3 Скилл в атаке'
}

function boysChoice(index: LevelUpIndex, icon: string, title: string, level: number, stat: string, statValue: number, skill: string, ultimate: string): Choice {
  const next = Math.min(4, level + 1)
  return {
    index,
    icon,
    title,
    kicker: `${stat} ${statValue} → ${cappedStat(statValue, 2)} · ${skill} x${level}`,
    description: next === 4 ? `Финальный уровень открывает «${ultimate}».` : `Усилить «${skill}» до x${next}.`,
    outcome: level >= 4 ? 'Ветка уже завершена' : `+2 ${stat} · ${skill} x${next}`,
    badge: level >= 4 ? 'MAX' : `x${next}`,
    disabled: level >= 4,
  }
}

const choices = computed<Choice[]>(() => {
  const c = props.player.character
  switch (kind.value) {
    case 'doom':
      return (doom.value?.currentOptions ?? []).map((module, idx) => ({
        index: (idx + 1) as LevelUpIndex,
        icon: module.reward ? '◆' : '◈',
        title: module.name,
        kicker: module.reward ? 'МОДУЛЬ-НАГРАДА' : 'БАЗОВЫЙ МОДУЛЬ',
        description: module.description,
        outcome: 'Установить в активный слот',
        badge: stageMeta[module.stage]?.ru ?? module.stage,
      }))
    case 'goblin': {
      const g = goblin.value
      if (!g) return []
      const warriorNext = [5, 4, 3, 2, 2][Math.min(4, g.warriorUpgradeLevel + 1)]
      return [
        { index: 1, icon: '🧙', title: 'Правильное питание', kicker: `Хобгоблины · уровень ${g.hobUpgradeLevel}/4`, description: 'Хобгоблины появляются чаще.', outcome: g.hobUpgradeLevel >= 4 ? 'Минимальный интервал достигнут' : `каждый ${g.hobRate}й → каждый ${g.hobRate - 1}й`, badge: g.hobUpgradeLevel >= 4 ? 'MAX' : `${g.hobUpgradeLevel + 1}/4`, disabled: g.hobUpgradeLevel >= 4 },
        { index: 2, icon: '⚔️', title: 'Контрактная армия', kicker: `Воины · уровень ${g.warriorUpgradeLevel}/4`, description: 'Больше воинов в общей популяции.', outcome: g.warriorUpgradeLevel >= 4 ? 'Контракты уже оптимизированы' : `каждый ${g.warriorRate}й → каждый ${warriorNext}й`, badge: g.warriorUpgradeLevel >= 4 ? 'MAX' : `${g.warriorUpgradeLevel + 1}/4`, disabled: g.warriorUpgradeLevel >= 4 },
        { index: 3, icon: '⛏️', title: 'Трудовые условия', kicker: `Трудяги · уровень ${g.workerUpgradeLevel}/4`, description: 'Трудяги появляются чаще.', outcome: g.workerUpgradeLevel >= 4 ? 'Минимальный интервал достигнут' : `каждый ${g.workerRate}й → каждый ${g.workerRate - 1}й`, badge: g.workerUpgradeLevel >= 4 ? 'MAX' : `${g.workerUpgradeLevel + 1}/4`, disabled: g.workerUpgradeLevel >= 4 },
        { index: 4, icon: '🎉', title: 'Праздник Гоблинов', kicker: 'ОДИН РАЗ ЗА МАТЧ', description: 'Никаких процентов: стая удваивается прямо сейчас.', outcome: g.festivalUsed ? 'Праздник уже состоялся' : `${g.totalGoblins} → ${g.totalGoblins * 2} гоблинов`, badge: g.festivalUsed ? 'USED' : '×2', disabled: g.festivalUsed },
      ]
    }
    case 'geralt': {
      const g = geralt.value
      if (!g) return []
      const tiers = [g.drownersOilTier, g.werewolvesOilTier, g.vampiresOilTier, g.dragonsOilTier]
      if (tiers.every(tier => tier === 0)) {
        return [{ index: 1, icon: '🧪', title: 'Масло от любой заразы', kicker: 'ПЕРВАЯ ВАРКА', description: 'Сразу приготовит Масло I против всех четырёх типов чудовищ.', outcome: 'Утопцы · Волколаки · Вампиры · Драконы', badge: '4× I' }]
      }
      const data = [
        { icon: '💀', title: 'Утопцы', tier: tiers[0] },
        { icon: '🐺', title: 'Волколаки', tier: tiers[1] },
        { icon: '🦇', title: 'Вампиры', tier: tiers[2] },
        { icon: '🐉', title: 'Драконы', tier: tiers[3] },
      ]
      return data.map((oil, idx) => ({
        index: (idx + 1) as LevelUpIndex,
        icon: oil.icon,
        title: oil.title,
        kicker: `${geraltTierName(oil.tier)} · уровень ${oil.tier}/3`,
        description: oil.tier >= 3 ? 'Рецепт уже доведён до совершенства.' : geraltTierEffect(oil.tier + 1),
        outcome: oil.tier >= 3 ? 'Максимальный уровень' : `${geraltTierName(oil.tier)} → ${geraltTierName(oil.tier + 1)}`,
        badge: oil.tier >= 3 ? 'MAX' : `${oil.tier + 1}/3`,
        disabled: oil.tier >= 3,
      }))
    }
    case 'theboys': {
      const b = theBoys.value
      if (!b) return []
      if (b.superDickActive) return [{ index: 2, icon: '🔪', title: 'СуперМудень', kicker: 'НЕОБРАТИМО', description: 'Butcher больше не делится прокачкой с командой.', outcome: 'Потратить очко без усиления', badge: 'SOLO' }]
      const choices = [
        boysChoice(1, '🧪', 'Francie', b.chemWeaponLevel, 'INT', c.intelligence, 'Хим.оружие', 'Вирус V'),
        boysChoice(2, '🔪', 'Butcher', b.pokerCount, 'STR', c.strength, 'Кочерга', 'СуперМудень'),
        boysChoice(3, '💚', 'Kimiko', b.regenLevel, 'SPD', c.speed, 'Регенирация', 'Живое Оружие'),
        boysChoice(4, '📋', 'M.M.', b.mmUpgradeLevel, 'PSY', c.psyche, 'Компромат', 'Оковы Правосудия'),
      ]
      if (b.butcherLeft) {
        choices[1] = {
          ...choices[1],
          kicker: 'Бучер покинул команду',
          description: 'Эта ветка больше недоступна.',
          outcome: 'Нахер Бучера',
          badge: 'УШЁЛ',
          disabled: true,
        }
      }
      return choices
    }
    case 'dopa':
      return [
        { index: 1, icon: '⚔️', title: 'Стомп', kicker: 'СИЛОВАЯ МЕТА', description: '+9 Силы и 99 *Скилла*.', outcome: 'Выбрать Стомп', badge: '+9 STR' },
        { index: 2, icon: '👁️', title: 'Фарм', kicker: 'ЭКОНОМИЧЕСКАЯ МЕТА', description: '"Взгляд в будущее" приносит вдвое больше очков.', outcome: 'Выбрать Фарм', badge: '×2' },
        { index: 3, icon: '👑', title: 'Доминация', kicker: 'ПОБЕДНАЯ МЕТА', description: 'Победы приносят Допе +20 *Скилла*, а цель теряет **бонусное** очко и иногда психику. (шанс 33%)', outcome: 'Выбрать Доминацию', badge: '33%' },
        { index: 4, icon: '🧭', title: 'Роум', kicker: 'МЕТА ТАБЛИЦЫ', description: 'При победе над врагами, не стоящими по соседству в таблице, **Крадет** у них **бонусное** очко и 3 *Морали*.', outcome: 'Выбрать Роум', badge: 'STEAL' },
      ]
    case 'kotiki':
      return [{ index: 1, icon: '⚖️', title: 'Получить Справедливость', kicker: `СЕЙЧАС ${c.justice}`, description: 'Срабатывает сразу и не меняет характеристики.', outcome: `${c.justice} → ${Math.min(5, c.justice + 1)} Справедливости`, badge: '+1' }]
    case 'vampyr':
      return [{ index: 1, icon: '🧄', title: 'Принять позор', kicker: 'СТАТЫ НЕ ИЗМЕНЯТСЯ', description: 'Vampyr Позорный съедает обязательное очко прокачки.', outcome: 'Потратить очко и продолжить ход', badge: '+0' }]
    case 'irelia': return statChoices(-1, 'ОБЯЗАТЕЛЬНЫЙ НЕРФ')
    case 'seller': return statChoices(10, 'ОПТОВАЯ ПОКУПКА')
    default: return []
  }
})

const allDisabled = computed(() => choices.value.length > 0 && choices.value.every(choice => choice.disabled))

function choose(choice: Choice) {
  if (choice.disabled || props.submitting) return
  emit('choose', choice.index)
}
</script>

<template>
  <section class="special-levelup" :class="[`special-levelup--${kind}`, { 'is-submitting': submitting }]" aria-labelledby="special-levelup-title">
    <header class="special-levelup__header">
      <span class="special-levelup__eyebrow">{{ header.eyebrow }}</span>
      <div class="special-levelup__title-row">
        <h3 id="special-levelup-title">{{ header.title }}</h3>
        <span class="special-levelup__points">{{ player.status.lvlUpPoints }} {{ player.status.lvlUpPoints === 1 ? 'выбор' : 'выбора' }}</span>
      </div>
      <p>{{ header.hint }}</p>
    </header>

    <div v-if="kind === 'doom'" class="doom-stage-rail" aria-label="Этапы Fortress of Doom">
      <div
        v-for="stage in stageOrder"
        :key="stage"
        class="doom-stage"
        :class="{
          active: inferredDoomStage === stage,
          complete: Boolean(doom?.activeModules[stage]),
        }"
      >
        <span class="doom-stage__icon">{{ stageMeta[stage].icon }}</span>
        <span>{{ stageMeta[stage].ru }}</span>
        <small>{{ doom?.activeModules[stage] || `ход ${stageMeta[stage].round}` }}</small>
      </div>
    </div>

    <div v-if="choices.length" class="special-levelup__choices">
      <button
        v-for="choice in choices"
        :key="`${kind}-${choice.index}-${choice.title}`"
        class="levelup-choice"
        :disabled="choice.disabled || submitting"
        data-sfx-skip-default="true"
        @click="choose(choice)"
      >
        <span class="levelup-choice__icon" aria-hidden="true">{{ choice.icon }}</span>
        <span class="levelup-choice__body">
          <span class="levelup-choice__kicker">{{ choice.kicker }}</span>
          <strong>{{ choice.title }}</strong>
          <span class="levelup-choice__description" v-html="formatPassiveDescription(choice.description)" />
          <span class="levelup-choice__outcome">{{ choice.outcome }}</span>
        </span>
        <span v-if="choice.badge" class="levelup-choice__badge">{{ choice.badge }}</span>
        <span class="levelup-choice__cta">{{ submitting ? '…' : choice.disabled ? 'Готово' : 'Выбрать' }}</span>
      </button>
    </div>

    <div v-else class="special-levelup__empty" role="status">
      <strong>Модули не загрузились</strong>
      <span>Очко не потеряно. Переподключись к матчу — базовые модули Fortress восстановятся автоматически.</span>
    </div>

    <div v-if="allDisabled" class="special-levelup__warning">
      <span>Все ветки заполнены, но осталось обязательное очко. Оно не даст нового эффекта.</span>
      <button :disabled="submitting" data-sfx-skip-default="true" @click="emit('choose', choices[0].index)">Потратить без усиления</button>
    </div>
  </section>
</template>

<style scoped>
.special-levelup {
  --accent: #f4b942;
  --accent-rgb: 244, 185, 66;
  position: relative;
  margin: 4px 0 10px;
  padding: 12px;
  overflow: hidden;
  border: 1px solid rgba(var(--accent-rgb), .42);
  border-radius: 12px;
  background:
    radial-gradient(circle at 92% 0%, rgba(var(--accent-rgb), .17), transparent 42%),
    linear-gradient(145deg, rgba(var(--accent-rgb), .09), rgba(8, 10, 13, .92) 60%);
  box-shadow: inset 0 1px rgba(255, 255, 255, .04), 0 8px 26px rgba(0, 0, 0, .2);
}
.special-levelup::before {
  content: '';
  position: absolute;
  inset: 0 auto 0 0;
  width: 3px;
  background: var(--accent);
  box-shadow: 0 0 16px rgba(var(--accent-rgb), .75);
}
.special-levelup--doom { --accent: #ef6545; --accent-rgb: 239, 101, 69; }
.special-levelup--goblin { --accent: #83c341; --accent-rgb: 131, 195, 65; }
.special-levelup--geralt { --accent: #d8af63; --accent-rgb: 216, 175, 99; }
.special-levelup--theboys { --accent: #ef5350; --accent-rgb: 239, 83, 80; }
.special-levelup--dopa { --accent: #4a90d9; --accent-rgb: 74, 144, 217; }
.special-levelup--kotiki { --accent: #ffb74d; --accent-rgb: 255, 183, 77; }
.special-levelup--vampyr { --accent: #b46cff; --accent-rgb: 180, 108, 255; }
.special-levelup--irelia { --accent: #ff5c6c; --accent-rgb: 255, 92, 108; }
.special-levelup--seller { --accent: #42d69b; --accent-rgb: 66, 214, 155; }
.special-levelup__header { position: relative; z-index: 1; }
.special-levelup__eyebrow { display: block; color: var(--accent); font-size: 9px; font-weight: 900; letter-spacing: .14em; }
.special-levelup__title-row { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-top: 3px; }
.special-levelup h3 { margin: 0; color: #fff; font-size: 15px; line-height: 1.15; }
.special-levelup__header p { margin: 5px 0 0; color: rgba(255, 255, 255, .64); font-size: 10px; line-height: 1.4; }
.special-levelup__points { flex: none; padding: 3px 7px; border: 1px solid rgba(var(--accent-rgb), .35); border-radius: 999px; color: var(--accent); background: rgba(var(--accent-rgb), .09); font-size: 9px; font-weight: 800; }
.special-levelup__choices { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 7px; margin-top: 10px; }
.levelup-choice {
  position: relative;
  display: grid;
  grid-template-columns: 32px minmax(0, 1fr) auto;
  gap: 8px;
  min-height: 94px;
  padding: 10px 9px 24px;
  color: inherit;
  text-align: left;
  font: inherit;
  border: 1px solid rgba(255, 255, 255, .1);
  border-radius: 9px;
  background: rgba(255, 255, 255, .035);
  cursor: pointer;
  transition: transform .16s ease, border-color .16s ease, background .16s ease, box-shadow .16s ease;
}
.levelup-choice:hover:not(:disabled), .levelup-choice:focus-visible {
  transform: translateY(-2px);
  border-color: rgba(var(--accent-rgb), .72);
  outline: none;
  background: rgba(var(--accent-rgb), .1);
  box-shadow: 0 7px 20px rgba(0, 0, 0, .25), inset 0 0 18px rgba(var(--accent-rgb), .05);
}
.levelup-choice:active:not(:disabled) { transform: translateY(0) scale(.99); }
.levelup-choice:disabled { opacity: .46; cursor: not-allowed; filter: saturate(.55); }
.levelup-choice__icon { display: grid; place-items: center; width: 32px; height: 32px; color: var(--accent); border: 1px solid rgba(var(--accent-rgb), .28); border-radius: 8px; background: rgba(var(--accent-rgb), .1); font-size: 14px; font-weight: 900; }
.levelup-choice__body { display: flex; min-width: 0; flex-direction: column; gap: 2px; }
.levelup-choice__kicker { color: var(--accent); font-size: 8px; font-weight: 900; letter-spacing: .07em; }
.levelup-choice strong { color: #f7f3ed; font-size: 12px; line-height: 1.2; }
.levelup-choice__description { color: rgba(255, 255, 255, .58); font-size: 9px; line-height: 1.35; }
.levelup-choice__outcome { margin-top: 3px; color: rgba(255, 255, 255, .87); font-size: 9px; font-weight: 700; }
.levelup-choice__badge { align-self: start; padding: 2px 5px; color: var(--accent); border-radius: 4px; background: rgba(var(--accent-rgb), .11); font-size: 8px; font-weight: 900; }
.levelup-choice__cta { position: absolute; right: 9px; bottom: 7px; color: rgba(var(--accent-rgb), .9); font-size: 8px; font-weight: 900; letter-spacing: .08em; text-transform: uppercase; }
.is-submitting .levelup-choice:not(:disabled) { opacity: .62; }
.doom-stage-rail { display: grid; grid-template-columns: repeat(4, 1fr); gap: 4px; margin-top: 10px; }
.doom-stage { display: flex; min-width: 0; flex-direction: column; gap: 1px; padding: 6px; color: rgba(255, 255, 255, .35); border: 1px solid rgba(255, 255, 255, .07); border-radius: 6px; background: rgba(0, 0, 0, .2); font-size: 8px; }
.doom-stage__icon { color: inherit; font-size: 12px; }
.doom-stage small { overflow: hidden; color: rgba(255, 255, 255, .28); font-size: 7px; text-overflow: ellipsis; white-space: nowrap; }
.doom-stage.complete { color: #91d67c; border-color: rgba(105, 210, 89, .2); }
.doom-stage.active { color: var(--accent); border-color: rgba(var(--accent-rgb), .65); background: rgba(var(--accent-rgb), .1); box-shadow: 0 0 12px rgba(var(--accent-rgb), .12); }
.special-levelup__empty, .special-levelup__warning { display: flex; flex-direction: column; gap: 3px; margin-top: 10px; padding: 9px; border: 1px solid rgba(255, 120, 80, .35); border-radius: 7px; color: #ffb49f; background: rgba(150, 40, 20, .12); font-size: 9px; line-height: 1.35; }
.special-levelup__warning { color: #ffd08a; }
.special-levelup__warning button { align-self: flex-start; margin-top: 4px; padding: 5px 8px; border: 1px solid rgba(255, 208, 138, .35); border-radius: 4px; color: #ffd08a; background: rgba(255, 208, 138, .08); font: inherit; font-weight: 800; cursor: pointer; }
@media (max-width: 420px) {
  .special-levelup__choices { grid-template-columns: 1fr; }
  .doom-stage { padding: 5px 3px; text-align: center; }
  .doom-stage small { display: none; }
}
@media (prefers-reduced-motion: reduce) {
  .levelup-choice { transition: none; }
}
</style>
