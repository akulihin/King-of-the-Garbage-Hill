<script setup lang="ts">
import { computed, ref, watch, onUnmounted } from 'vue'
import type { Player, PortalGun, ExploitState, TsukuyomiState, PassiveAbilityStates, ScoreBreakdown } from 'src/services/signalr'
import { useTip } from 'src/composables/useTip'
import ScoreOdometer from 'src/components/ScoreOdometer.vue'
import SpecialLevelUpPanel from 'src/components/SpecialLevelUpPanel.vue'
import { useGameStore } from 'src/store/game'
import { currentLocale } from 'src/i18n'
import {
  playComboHype,
  playComboPluck,
  playPointsIncreaseSound,
  playComboStack,
  playPointsSummary,
} from 'src/services/sound'

const props = withDefaults(defineProps<{
  player: Player
  isMe: boolean
  resistFlash?: string[]
  justiceReset?: boolean
  justiceUp?: boolean
  scoreBreakdown?: ScoreBreakdown | null
  scoreAnimReady?: boolean
  fightBonuses?: { label: string; value: string; cssClass: string }[]
}>(), {
  resistFlash: () => [],
  justiceReset: false,
  justiceUp: false,
  scoreBreakdown: null,
  scoreAnimReady: false,
  fightBonuses: () => [],
})

const store = useGameStore()
type LevelUpStatIndex = 1 | 2 | 3 | 4

const hasLvlUpPoints = computed(() => props.isMe && (props.player?.status.lvlUpPoints ?? 0) > 0)
const lvlUpPoints = computed(() => props.player?.status.lvlUpPoints ?? 0)

/** Level-up flavor quips per character */
const levelUpQuotes: Record<string, string[]> = {
  'Глеб': ['*зевает* ...ну ладно, прокачаюсь...', '*бормочет сквозь сон*', '...пять минуточек...'],
  'mylorik': ['ЭТО СПАРТА!!!', 'ДААА! ЕЩЕ СИЛЬНЕЕ!', 'КАМОН, БОЛЬШЕ МОЩИ!'],
  'Продавец': ['Хех, выгодное вложение...'],
  'Рик': ['Wubba lubba dub dub!', 'Science, b*tch!', '*отрыжка* ...умнее стал...'],
  'Сайтама': ['Ок.', '...серьезно?', 'Скучно.'],
  'Кратос': ['BOI!', 'Мы должны стать сильнее.', 'Боги заплатят.'],
  'Тигр': ['Тигр на вершине!', 'Слабаки, все слабаки!', 'Я ЛУЧШИЙ!'],
  'Кира': ['Я - бог нового мира.'],
  'LeCrisp': ['GG EZ'],
  'Дракон': ['ROAR', 'Огонь сильнее!', 'Пламя растёт...'],
  'Котики': ['Мяу~', '*мурчит*', 'Котик стал сильнее!'],
  'HardKitty': ['...', '*тишина*', '...никто не ответил...'],
  'Стая Гоблинов': ['ВААААГХ!', 'Больше гоблинов!', 'МЯЯЯСО!'],
  'Weedwick': ['420...', 'АУФ'],
  'DeepList': ['гек?'],
  'Братишка': ['Буль!'],
  'Дезморалист': ['Всё бесполезно...', 'Сдавайся...', 'Нет смысла...'],
  'Баг': ['> sudo levelup', '0x1337', 'exploit.exe'],
  'Допа': ['Тактика решает.', 'По плану.', 'Рассчитано.'],
  'Geralt': ['Ветер воет...', 'Место Силы...'],
  'Молодой Глеб': ['Опять нерфят...', 'Сколько можно?!', 'Риоты совсем...', 'Да как так?!'],
  'TheBoys': ['Diabolical.', 'Работаем, пацаны.', 'Oi! Кого качаем?', 'Voilà!'],
  'DooM Guy': ['Rip and tear.', 'Until it is done.'],
}

const levelUpQuip = computed(() => {
  const name = props.player?.character.name
  if (!name || !hasLvlUpPoints.value) return ''
  const quotes = levelUpQuotes[name]
  if (!quotes) return ''
  // Deterministic pick based on round number
  const roundNo = store.gameState?.roundNo ?? 1
  return quotes[(roundNo - 1) % quotes.length]
})

/** Character accent color for level-up tint */
const levelUpTintColors: Record<string, string> = {
  'Глеб': 'rgba(155,89,182,0.05)',
  'mylorik': 'rgba(231,76,60,0.05)',
  'Продавец': 'rgba(243,156,18,0.05)',
  'Рик': 'rgba(46,204,113,0.05)',
  'Сайтама': 'rgba(241,196,15,0.05)',
  'Кратос': 'rgba(192,57,43,0.05)',
  'Тигр': 'rgba(230,126,34,0.05)',
  'Кира': 'rgba(142,68,173,0.05)',
  'Братишка': 'rgba(52,152,219,0.05)',
  'DeepList': 'rgba(231,76,60,0.05)',
  'Баг': 'rgba(0,255,65,0.05)',
  'Молодой Глеб': 'rgba(239,80,80,0.05)',
  'TheBoys': 'rgba(220,40,40,0.06)',
  'DooM Guy': 'rgba(214,54,30,0.08)',
}

const levelUpTint = computed(() => {
  const name = props.player?.character.name
  if (!name || !hasLvlUpPoints.value) return ''
  return levelUpTintColors[name] || ''
})

const moral = computed(() => {
  if (!props.player) return 0
  return Number.parseFloat(props.player.character.moralDisplay) || 0
})

const isMadara = computed(() => props.player?.character.name === 'Мадара')
const hasMoral = computed(() => props.isMe && !isMadara.value && moral.value >= 1)

const roundNo = computed(() => store.gameState?.roundNo ?? 0)
const isLastRound = computed(() => roundNo.value === 10)
const roundMultiplier = computed(() => props.scoreBreakdown?.roundMultiplier ?? 1)

const isMultiplierModified = computed(() => {
  if (!props.scoreBreakdown) return false
  return props.scoreBreakdown.roundMultiplier !== props.scoreBreakdown.expectedRoundMultiplier
})

const expectedMultiplier = computed(() => props.scoreBreakdown?.expectedRoundMultiplier ?? 1)

const hasBulkaet = computed(() => {
  if (!props.player) return false
  return props.player.character.passives.some((p: { name: string }) => p.name === 'Булькает')
})

const isDeepList = computed(() => {
  if (!props.player) return false
  return props.player.character.name === 'DeepList'
})

const portalGun = computed<PortalGun | null>(() => {
  if (!props.isMe) return null
  return props.player?.portalGun ?? null
})

const isBug = computed(() => props.player?.isBug ?? false)

// Position-based avatar glow tier (1=top, 6=bottom)
const placeTier = computed(() => {
  const place = props.player?.status?.place ?? 3
  if (place <= 1) return 'place-1'
  if (place <= 2) return 'place-2'
  if (place <= 3) return 'place-3'
  if (place <= 5) return 'place-mid'
  return 'place-last'
})

const exploitState = computed<ExploitState | null>(() => {
  if (!props.isMe) return null
  return props.player?.exploitState ?? null
})

const tsukuyomiState = computed<TsukuyomiState | null>(() => {
  if (!props.isMe) return null
  return props.player?.tsukuyomiState ?? null
})

const passiveStates = computed<PassiveAbilityStates | null>(() => {
  if (!props.isMe) return null
  return props.player?.passiveAbilityStates ?? null
})

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

const widgetHelpCopy = {
  portal: ['At 30 INT the Portal Gun is invented. A charge turns a winning attack into a position swap.', 'При 30 INT пушка изобретается. Заряд тратится на победную атаку с обменом местами.'],
  exploit: ['Stored EXPLOIT is waiting for the cycle to finish. The bar shows how many players have been processed.', 'Накопленный EXPLOIT ждёт завершения цикла. Шкала показывает, сколько игроков уже обработано.'],
  tsukuyomi: ['Two charge points ready Tsukuyomi. The value below is the total score already stolen.', 'Две единицы заряда открывают Цукуёми. Ниже показана сумма уже украденных очков.'],
  doom: ['The four slots are your current combat loadout. Card colors show active, completed, and failed modules.', 'Четыре слота — твой текущий боевой комплект. Цвет карточки показывает активный, выполненный или проваленный модуль.'],
  pickleRick: ['Shows the turns left as Pickle Rick and the length of the penalty that follows.', 'Показывает, сколько ходов осталось в форме Огурчика и сколько длится последующий штраф.'],
  giantBeans: ['Stacks empower the Beans. COOKING means ingredients are already assigned to the shown number of targets.', 'Заряды усиливают Бобы. ГОТОВЯТСЯ означает, что ингредиенты уже разложены на указанных целях.'],
  eren: ['Rumbling only checks losses in round 10. The left counter shows Attack Titan readiness; fire marks show accumulated hatred.', 'RUMBLING проверяет только поражения в 10-м раунде. Счётчик слева показывает готовность Атакующего Титана, а метки 🔥 — накопленную ненависть.'],
  naruto: ['A ready Harem replaces Block. After use it recharges for two turns; Block remains ordinary while cooling down.', 'Готовый Гарем заменяет Блок. После использования он перезаряжается два хода; во время отката Блок остаётся обычным.'],
  bulk: ['The current chance for Boole to lose his turn. BUFFED means his zero-Psyche stat boost is active.', 'Текущий шанс Буля пропустить ход. BUFFED означает усиление характеристик при нулевой Психике.'],
  tea: ['When tea is ready, the next attack spends it for one point and makes the target skip their next turn.', 'Когда чай готов, следующая атака потратит его: даст очко и заставит цель пропустить следующий ход.'],
  jew: ['Tracks the Psyche accumulated by the PROFIT mechanic.', 'Счётчик показывает, сколько Психики уже накоплено механикой PROFIT.'],
  hardKitty: ['Tracks accumulated friends. A full bar represents five friends.', 'Количество накопленных друзей. Полная шкала соответствует пяти друзьям.'],
  training: ['After losing an attack, Sirinoks trains the shown stat until she reaches the target value.', 'После атакующего поражения Sirinoks тренирует указанную характеристику до показанной цели.'],
  dragon: ['Countdown to the round-10 awakening and the Dragon final recalculation.', 'Отсчёт до пробуждения в 10-м раунде, когда Дракон получает финальный перерасчёт.'],
  garbage: ['Shows how many enemies carry the smell after attacking Harry.', 'Сколько соперников уже оставили на себе запах, атаковав Гарри.'],
  copycat: ['Shows the currently copied stat and the total number of copies made.', 'Показывает скопированную характеристику и общее число сделанных копий.'],
  inkScreen: ['Fake defeats temporarily reverse winners. Deferred score is restored during the finale.', 'Ложные поражения временно меняют победителя. Отложенные очки будут восстановлены в финале.'],
  tigerTop: ['ACTIVE means the Tiger window is open. The counter is the number of first-place swaps remaining.', 'АКТИВНО означает, что окно Тигра открыто. Счётчик показывает оставшиеся обмены с первым местом.'],
  jaws: ['Speed rises for every unique enemy defeated and every new leaderboard place visited.', 'Скорость растёт за каждого уникального побеждённого врага и каждое новое посещённое место.'],
  privilege: ['The listed players are currently marked by Privilege.', 'Отмеченные игроки участвуют в механике Привилегии; их имена собраны здесь.'],
  vampirism: ['Bites pay Moral on even rounds. The second counter tracks Justice copied through the bypass.', 'Укусы приносят Мораль по чётным раундам; второй счётчик показывает скопированную через обход Справедливость.'],
  weed: ['Wins grow Weed on players; a Weedwick victory harvests the target supply as Moral.', 'Победы выращивают Weed у игроков; победа Weedwick собирает запас цели в Мораль.'],
  saitama: ['Score and Moral stored by ONE PUNCH are waiting for the final payout.', 'Очки и Мораль отложены способностью ONE PUNCH и ждут финального возврата.'],
  shinigamiEyes: ['Once activated, the next eligible attack reveals the target’s true name.', 'После активации следующая подходящая атака раскроет настоящее имя цели.'],
  seller: ['Cooldown, marked customers, and the hidden Skill reserve from Procurement.', 'Откат, количество отмеченных клиентов и скрытый запас Скилла от Закупа.'],
  dopa: ['Shows the current tactic, Vision readiness, and whether a second attack is required.', 'Текущая тактика, готовность обзора и напоминание о необходимой второй атаке.'],
  goblinSwarm: ['The bar splits the swarm by type; 1/N is the current spawn rate for each type. Badges show built Ziggurats.', 'Полоса делит стаю по типам; 1/N — текущий шанс появления типа. Значки внизу — построенные Зиккураты.'],
  kotiki: ['Tracks taunts, current Minka and Storm carriers, and both cat cooldowns.', 'Здесь видны провокации, текущие носители Миньки и Штормяка и оставшиеся откаты котиков.'],
  monster: ['The number of players who have become Johan pawns.', 'Количество игроков, превращённых в пешек Монстра.'],
  tolyaCount: ['When Count is ready, the next attack can mark a target. The number is its remaining cooldown.', 'Когда Подсчёт готов, следующая атака может отметить цель. Число — оставшийся откат.'],
  impact: ['The streak grows each round without a defensive loss and increases victory rewards.', 'Серия растёт за раунды без защитного поражения и усиливает награду за победы.'],
  darksci: ['Shows the chosen luck style and how many unique enemies remain to be tested.', 'Выбранный стиль удачи и число уникальных соперников, которых ещё нужно проверить.'],
  deepList: ['Tracks enemies learned and the number of Mockery triggers.', 'Сколько соперников уже изучено и сколько раз сработал Стёб.'],
  craboRack: ['Shell charges remaining from the initial five.', 'Оставшиеся заряды Панциря из пяти начальных.'],
  napoleon: ['Shows Napoleon’s current ally and the number of treaties made.', 'Текущий союзник Наполеона и число заключённых договоров.'],
  support: ['The Carry currently empowered by Support as a PREMADE.', 'Имя выбранного керри, которого Суппорт усиливает как PREMADE.'],
  toxicMate: ['Shows the infection carrier and transfer count. Returning to Toxic Mate converts the chain into a bonus.', 'Показывает носителя инфекции и число передач. Возврат к Toxic Mate превращает цепочку в бонус.'],
  yongGleb: ['Young Gleb’s tea readiness; the number is the remaining cooldown.', 'Готовность чая Молодого Глеба; число показывает оставшийся откат.'],
  theBoys: ['Each card is one team member with their level, active job, and progress. Bright badges are unlocked ultimates.', 'Каждая карточка — отдельный член команды: уровень, активная задача и прогресс. Яркие жетоны внизу — открытые ультимейты.'],
  salldorum: ['Shen charges are spent automatically by the next attack. Cola shows its cache and pickup readiness. Rewrite shows history availability.', 'Заряд Шэна автоматически тратится следующей атакой. Cola показывает место тайника и готовность к подбору, Rewrite — доступность переписывания истории.'],
  geralt: ['Each row shows contracts and oil tier. Fighting that monster type spends every matching contract on extra bouts.', 'Каждая строка показывает число заказов типа и уровень масла. Бой с целью тратит все её заказы на дополнительные схватки.'],
} as const

type WidgetHelpKey = keyof typeof widgetHelpCopy

function widgetHelp(key: WidgetHelpKey): string {
  const copy = widgetHelpCopy[key]
  return t(copy[0], copy[1])
}

// Mastery badge
const masteryPoints = computed(() => props.player?.characterMasteryPoints ?? 0)
const masteryLevel = computed(() => Math.floor(Math.sqrt(masteryPoints.value / 5)))
const masteryTier = computed(() => {
  const lvl = masteryLevel.value
  if (lvl >= 20) return 'diamond'
  if (lvl >= 15) return 'platinum'
  if (lvl >= 10) return 'gold'
  if (lvl >= 5) return 'silver'
  if (lvl >= 1) return 'bronze'
  return 'none'
})

// Character rarity tier (1 = rarest, 6 = most common)
const charTier = computed(() => props.player?.character.tier ?? 0)
const rarityLabel = computed(() => {
  switch (charTier.value) {
    case 1: return 'Legendary'
    case 2: return 'Epic'
    case 3: return 'Rare'
    case 4: return 'Uncommon'
    case 5: return 'Common'
    case 6: return 'Common'
    default: return ''
  }
})
const rarityClass = computed(() => {
  switch (charTier.value) {
    case 1: return 'rarity-legendary'
    case 2: return 'rarity-epic'
    case 3: return 'rarity-rare'
    case 4: return 'rarity-uncommon'
    default: return 'rarity-common'
  }
})

const isGeralt = computed(() => props.player?.character.name === 'Геральт')
const isEren = computed(() => props.player?.character.name === 'Эрен Йегер')
const isIrelia = computed(() => props.player?.character.passives.some((p: { name: string }) => p.name === 'Main Ирелия') ?? false)
const goblin = computed(() => passiveStates.value?.goblinSwarm ?? null)
const geralt = computed(() => passiveStates.value?.geralt ?? null)
const theBoys = computed(() => passiveStates.value?.theBoys ?? null)
const doomGuy = computed(() => passiveStates.value?.doomGuy ?? null)

const hasPassive = (name: string) => props.player?.character.passives.some((passive: { name: string }) => passive.name === name) ?? false

// These mechanics replace the ordinary +1 stat choice entirely. Keeping this list explicit
// prevents a new special branch from accidentally exposing misleading + buttons.
const usesSpecialLevelUpPanel = computed(() => {
  const name = props.player?.character.name
  return name === 'DooM Guy'
    || name === 'Стая Гоблинов'
    || name === 'Геральт'
    || name === 'TheBoys'
    || name === 'Котики'
    || name === 'Вампур'
    || hasPassive('Vampyr Позорный')
    || hasPassive('Main Ирелия')
    || hasPassive('Закуп')
})

// These characters still choose a normal stat, but level-up also triggers a load-bearing
// passive consequence. Surface it at the decision point instead of burying it in the log.
const levelUpConsequences = computed(() => {
  if (!hasLvlUpPoints.value || usesSpecialLevelUpPanel.value) return []
  const notes: string[] = []
  const name = props.player?.character.name
  if (name === 'Итачи') notes.push('Любой выбор подготовит Ворона: он сядет на цель следующей атаки, даже при поражении.')
  if (name === 'Salldorum') notes.push('Любой выбор также даст +1 заряд Шэн.')
  if (name === 'Рик Санчез' && hasPassive('Гигантские бобы')) notes.push('Любой выбор разложит ингредиенты на новых врагов; INT повышает базу Бобов и не ограничен 10.')
  if (name === 'Рик Санчез' && hasPassive('Портальная пушка')) notes.push('При INT 30 Портальная пушка будет изобретена; после изобретения каждый выбор даёт +1 заряд.')
  if (name === 'Darksci' && roundNo.value === 9) notes.push('Дизмораль после выбора отнимет 5 Психики. Если она упадёт до 0, этот ход сразу станет пропуском.')
  if (name === 'Darksci' && roundNo.value !== 9 && props.player.character.psyche <= 0) notes.push('После обязательной прокачки Дизмораль подтвердит пропуск этого хода.')
  const training = passiveStates.value?.training
  if (name === 'Sirinoks' && training?.currentStatIndex) notes.push(`Обучение: цель — ${training.statName} ${training.targetStatValue}. Достижение цели даст +3 Морали и +10% Скилла.`)
  return notes
})

// Geralt demand progressive color helpers
function geraltSegColor(displeasure: number): string {
  if (displeasure <= 3) return '#C8A050'
  if (displeasure <= 6) return '#D4882A'
  if (displeasure <= 8) return '#D05030'
  return '#E02020'
}

function geraltSegStyle(i: number): Record<string, string> {
  const d = geralt.value?.displeasure ?? 0
  if (i > d) return { background: 'rgba(200, 160, 80, 0.15)' }
  return { background: geraltSegColor(d) }
}

const geraltDemandContainerClass = computed(() => {
  const d = geralt.value?.displeasure ?? 0
  if (d >= 10) return 'geralt-demand-critical'
  if (d >= 7) return 'geralt-demand-hot'
  if (d >= 4) return 'geralt-demand-warm'
  return ''
})

const geraltHeaderStyle = computed(() => {
  const d = geralt.value?.displeasure ?? 0
  if (d >= 9) return { color: '#E02020' }
  if (d >= 7) return { color: '#D05030' }
  if (d >= 4) return { color: '#D4882A' }
  return {}
})

const geraltDispleasureTextStyle = computed(() => {
  const d = geralt.value?.displeasure ?? 0
  if (d >= 9) return { color: '#E02020' }
  if (d >= 7) return { color: 'rgba(208, 80, 48, 0.7)' }
  if (d >= 4) return { color: 'rgba(212, 136, 42, 0.7)' }
  return {}
})

const invoiceTotalClass = computed(() => {
  const t = geralt.value?.invoiceTotal ?? 0
  if (t >= 12) return 'inv-tier-great'
  if (t >= 8) return 'inv-tier-good'
  if (t >= 4) return 'inv-tier-mid'
  return 'inv-tier-bad'
})

const demandPreviousBtnText = computed(() => {
  return t('Previous round (+1)', 'За прошлый (+1)')
})

// Goblin population bar segment percentages
const warriorPct = computed(() => {
  const g = goblin.value
  if (!g || g.totalGoblins === 0) return 0
  return Math.round((g.warriors / g.totalGoblins) * 100)
})
const hobPct = computed(() => {
  const g = goblin.value
  if (!g || g.totalGoblins === 0) return 0
  return Math.round((g.hobs / g.totalGoblins) * 100)
})
const workerPct = computed(() => {
  const g = goblin.value
  if (!g || g.totalGoblins === 0) return 0
  return Math.round((g.workers / g.totalGoblins) * 100)
})

/** Moral → Points exchange rate (matching backend GameUpdateMess.cs) */
const moralToPointsRate = computed(() => {
  if (isDeepList.value) return null
  const m = moral.value
  if (m >= 20) return { cost: 20, gain: 10 }
  if (m >= 13) return { cost: 13, gain: 5 }
  if (m >= 8)  return { cost: 8,  gain: 2 }
  if (m >= 5)  return { cost: 5,  gain: 1 }
  return null
})

const hasEvreiPassive = computed(() => {
  if (!props.player) return false
  return props.player.character.passives.some((p: { name: string }) => p.name === 'Еврей')
})

/** Moral → Skill exchange rate (matching backend GameUpdateMess.cs) */
const moralToSkillRate = computed(() => {
  if (hasBulkaet.value) return null
  const m = moral.value
  if (m >= 20) return { cost: 20, gain: 100 }
  if (m >= 13) return { cost: 13, gain: 50 }
  if (hasEvreiPassive.value && m >= 7) return { cost: 7, gain: 40 }
  if (m >= 8)  return { cost: 8,  gain: 30 }
  if (m >= 5)  return { cost: 5,  gain: 18 }
  if (m >= 3)  return { cost: 3,  gain: 10 }
  if (m >= 2)  return { cost: 2,  gain: 6 }
  if (m >= 1)  return { cost: 1,  gain: 2 }
  return null
})

// ── Score combo animation ──────────────────────────────────────────

/** A single source entry (one row in the combo feed) */
interface SourceEntry {
  name: string
  basePts: number       // pre-multiplied point value to display
  pointsEarned: number  // actual points earned (basePts × multiplier for regular)
}

/** A group of score sources (regular or bonus) */
interface ScoreGroup {
  type: 'regular' | 'bonus'
  multiplier: number
  entries: SourceEntry[]
  totalPoints: number
}

/** A flattened animation hit for staggered reveal */
interface AnimHit {
  name: string
  basePts: number
  pointsEarned: number
  comboIndex: number    // running index within the group (0-based)
  groupType: 'regular' | 'bonus'
  groupMultiplier: number
}

/** Build Regular and Bonus groups directly from structured ScoreBreakdown data */
const scoreGroups = computed<ScoreGroup[]>(() => {
  if (!props.scoreBreakdown) return []

  const mult = roundMultiplier.value
  const regularEntries: SourceEntry[] = []
  const bonusEntries: SourceEntry[] = []
  let bonusTotal = 0

  for (const entry of props.scoreBreakdown.entries) {
    if (entry.isBonus) {
      bonusEntries.push({ name: entry.source || 'Бонус', basePts: entry.points, pointsEarned: entry.points })
      bonusTotal += entry.points
    } else {
      regularEntries.push({ name: entry.source || 'Очки', basePts: entry.points, pointsEarned: Math.round(entry.points * mult) })
    }
  }

  const regularTotal = Math.round(regularEntries.reduce((sum, e) => sum + e.basePts, 0) * mult)

  const groups: ScoreGroup[] = []
  if (regularEntries.length > 0) {
    groups.push({ type: 'regular', multiplier: mult, entries: regularEntries, totalPoints: regularTotal })
  }
  if (bonusEntries.length > 0) {
    groups.push({ type: 'bonus', multiplier: 1, entries: bonusEntries, totalPoints: bonusTotal })
  }
  return groups
})

/** Flat list of all animation hits across groups (each source = separate hit) */
const allAnimHits = computed<AnimHit[]>(() => {
  const hits: AnimHit[] = []
  for (const group of scoreGroups.value) {
    let comboIdx = 0
    for (const entry of group.entries) {
      hits.push({
        name: entry.name,
        basePts: entry.basePts,
        pointsEarned: entry.pointsEarned,
        comboIndex: comboIdx++,
        groupType: group.type,
        groupMultiplier: group.multiplier,
      })
    }
  }
  return hits
})

function comboHeatStyle(combo: number) {
  const hue = Math.max(0, 52 - Math.max(0, combo - 2) * 7)
  return {
    color: `hsl(${hue} 92% 64%)`,
    backgroundColor: `hsl(${hue} 82% 48% / 0.16)`,
    borderColor: `hsl(${hue} 82% 58% / 0.32)`,
    textShadow: `0 0 9px hsl(${hue} 90% 55% / 0.45)`,
  }
}

const hitVisibleCount = ref(0)
const hitActiveIdx = ref(-1)
const animatedScoreDelta = ref(0)
let comboTimer: ReturnType<typeof setInterval> | null = null
let comboAnimStarted = false
let lastScoreSnapshot = ''

function clearComboTimer() {
  if (comboTimer !== null) { clearInterval(comboTimer); comboTimer = null }
}

function startComboAnimation() {
  if (comboAnimStarted) return
  comboAnimStarted = true
  clearComboTimer()
  const entryCount = props.scoreBreakdown?.entries.length ?? 0
  if (!entryCount) {
    hitVisibleCount.value = 0; hitActiveIdx.value = -1; animatedScoreDelta.value = 0
    return
  }
  hitVisibleCount.value = 0
  hitActiveIdx.value = -1
  animatedScoreDelta.value = 0
  setTimeout(() => {
    let i = 0
    let pluckSeq = 0
    let lastGroupType: string | null = null
    const hits = allAnimHits.value
    comboTimer = setInterval(() => {
      if (i >= hits.length) {
        clearComboTimer()
        const totalEarned = hits.reduce((sum: number, h: AnimHit) => sum + h.pointsEarned, 0)
        playPointsSummary(totalEarned)
        setTimeout(() => { hitActiveIdx.value = -1 }, 600)
        return
      }
      hitActiveIdx.value = i
      const hit = hits[i]
      if (hit.pointsEarned > 0) {
        playPointsIncreaseSound(hit.pointsEarned, hit.name)
        pluckSeq++
        playComboPluck(Math.min(7, pluckSeq))
        // Play hype sound when first hit of a new group type appears
        if (hit.groupType !== lastGroupType) {
          const groupHits = hits.filter((h: AnimHit) => h.groupType === hit.groupType)
          playComboHype(groupHits.length)
        }
      }
      lastGroupType = hit.groupType
      // Combo stack sounds for large combos
      if (hits.length > 7) {
        playComboStack(i + 1)
      }
      animatedScoreDelta.value += hit.pointsEarned
      i++
      hitVisibleCount.value = i
    }, 350)
  }, 100)
}

// Reset when score breakdown changes (new round)
watch(() => props.scoreBreakdown, (breakdown) => {
  const snap = breakdown ? JSON.stringify(breakdown) : ''
  if (snap === lastScoreSnapshot) return
  lastScoreSnapshot = snap
  comboAnimStarted = false
  clearComboTimer()
  hitVisibleCount.value = 0; hitActiveIdx.value = -1; animatedScoreDelta.value = 0
  // If replay already ended (e.g. no fights), start immediately
  if (props.scoreAnimReady) startComboAnimation()
}, { immediate: true, deep: true })

// Start combo when fight replay ends
watch(() => props.scoreAnimReady, (ready: boolean) => {
  if (ready && (props.scoreBreakdown?.entries.length ?? 0) > 0 && hitVisibleCount.value === 0) {
    startComboAnimation()
  }
})

// Trigger confetti on big score gains (delta > 8)
watch(animatedScoreDelta, (val: number) => {
  if (val > 8) {
    showConfetti.value = true
    if (confettiTimer) clearTimeout(confettiTimer)
    confettiTimer = setTimeout(() => { showConfetti.value = false }, 1500)
  }
})

onUnmounted(() => { clearComboTimer(); if (confettiTimer) clearTimeout(confettiTimer) })

// ── Stat change pulse animation + floating numbers ──────────────────
const prevStatValues = ref<{ int: number; str: number; spd: number; psy: number } | null>(null)
const pulsingStats = ref<Set<string>>(new Set())

// Ghost stat bars (show previous value on change)
const ghostStats = ref<{ int: number; str: number; spd: number; psy: number } | null>(null)
const showGhost = ref<Set<string>>(new Set())

// Confetti burst on big score gains
const showConfetti = ref(false)
let confettiTimer: ReturnType<typeof setTimeout> | null = null

interface FloatingNumber {
  id: number
  stat: string
  delta: number
}
let floatIdCounter = 0
const floatingNumbers = ref<FloatingNumber[]>([])

watch(
  () => [
    props.player?.character.intelligence,
    props.player?.character.strength,
    props.player?.character.speed,
    props.player?.character.psyche,
  ],
  (newVals) => {
    if (!newVals || !newVals[0]) return
    const [newInt, newStr, newSpd, newPsy] = newVals as number[]
    const prev = prevStatValues.value
    if (prev) {
      const changed: string[] = []
      const deltas: Record<string, number> = {}
      if (newInt !== prev.int) { changed.push('intelligence'); deltas.intelligence = newInt - prev.int }
      if (newStr !== prev.str) { changed.push('strength'); deltas.strength = newStr - prev.str }
      if (newSpd !== prev.spd) { changed.push('speed'); deltas.speed = newSpd - prev.spd }
      if (newPsy !== prev.psy) { changed.push('psyche'); deltas.psyche = newPsy - prev.psy }
      if (changed.length > 0) {
        for (const s of changed) pulsingStats.value.add(s)
        pulsingStats.value = new Set(pulsingStats.value)
        setTimeout(() => {
          for (const s of changed) pulsingStats.value.delete(s)
          pulsingStats.value = new Set(pulsingStats.value)
        }, 1500)

        // Ghost stat bars — show old values fading out
        ghostStats.value = { ...prev }
        for (const s of changed) showGhost.value.add(s)
        showGhost.value = new Set(showGhost.value)
        setTimeout(() => {
          for (const s of changed) showGhost.value.delete(s)
          showGhost.value = new Set(showGhost.value)
        }, 1500)

        // Spawn floating numbers
        for (const s of changed) {
          const id = ++floatIdCounter
          const d = deltas[s]
          floatingNumbers.value.push({ id, stat: s, delta: d })
          setTimeout(() => {
            floatingNumbers.value = floatingNumbers.value.filter(f => f.id !== id)
          }, Math.abs(d) >= 3 ? 1500 : 1200)
        }
      }
    }
    prevStatValues.value = { int: newInt, str: newStr, spd: newSpd, psy: newPsy }
  },
  { deep: true },
)

/** Geralt oil tier label */
function geraltOilLabel(tier: number): string {
  if (tier === 0) return '—'
  if (tier === 1) return t('Oil', 'Масло')
  if (tier === 2) return t('Improved', 'Улучш.')
  return t('Superior', 'Отличн.')
}

/** Parse "ClassName || description" from classStatDisplayText */
const classLabel = computed(() => {
  const raw = props.player?.character.classStatDisplayText ?? ''
  if (!raw) return ''
  const parts = raw.split('||')
  return parts[0].trim()
})
const classTooltip = computed(() => {
  const raw = props.player?.character.classStatDisplayText ?? ''
  if (!raw) return ''
  const parts = raw.split('||')
  // Strip Discord markdown (*word*) from tooltip
  return parts.length > 1 ? parts[1].trim().replace(/\*/g, '') : ''
})

/** Translate skill target class to a badge label + class */
function skillTargetBadge(target: string): { label: string; cls: string } {
  switch (target) {
    case 'Интеллект': return { label: 'INT', cls: 'gi gi-lg gi-int' }
    case 'Сила': return { label: 'STR', cls: 'gi gi-lg gi-str' }
    case 'Скорость': return { label: 'SPD', cls: 'gi gi-lg gi-spd' }
    default: return { label: '?', cls: 'gi' }
  }
}

function skillTargetTooltip(target: string): string {
  switch (target) {
    case 'Интеллект': return 'Мишень: Intelligence. Attack INT-class enemies for bonus skill'
    case 'Сила': return 'Мишень: Strength. Attack STR-class enemies for bonus skill'
    case 'Скорость': return 'Мишень: Speed. Attack SPD-class enemies for bonus skill'
    default: return 'Мишень'
  }
}

// ── Tooltip system ──────────────────────────────────────────────────
const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

function handleLevelUp(statIndex: LevelUpStatIndex) {
  void store.levelUp(statIndex)
}

function handleMoralToPoints() {
  void store.moralToPoints()
}

function handleMoralToSkill() {
  void store.moralToSkill()
}

function handleDoomChainsaw(passiveName: string) {
  void store.doomChainsaw(passiveName)
}

const doomStages = computed(() => [
  { key: 'Rune', icon: 'ᛟ', label: t('RUNE', 'РУНА') },
  { key: 'Shield', icon: '⬡', label: t('SHIELD', 'ЩИТ') },
  { key: 'Mission', icon: '⌖', label: t('MISSION', 'МИССИЯ') },
  { key: 'Gun', icon: '▰', label: t('WEAPON', 'ОРУЖИЕ') },
] as const)

function doomModuleStatus(module: string): { text: string; state: 'live' | 'done' | 'failed' | 'idle' } {
  const d = doomGuy.value
  if (!d || !module) return { text: t('Waiting for selection', 'Ожидает выбора'), state: 'idle' }
  if (d.copiedPassiveNames.length && module === d.copiedPassiveName)
    return { text: t(`Stolen: ${d.copiedPassiveNames.join(', ')}`, `Украдено: ${d.copiedPassiveNames.join(', ')}`), state: 'done' }
  switch (module) {
    case 'Вознесение': return { text: t(`${d.ascensionIntelligenceRemaining}/8 protected INT remaining`, `${d.ascensionIntelligenceRemaining}/8 защищённого INT осталось`), state: d.ascensionIntelligenceRemaining > 0 ? 'live' : 'done' }
    case 'Маневры': return { text: t(`${d.maneuversSpeedRemaining}/5 protected SPD remaining`, `${d.maneuversSpeedRemaining}/5 защищённой SPD осталось`), state: d.maneuversSpeedRemaining > 0 ? 'live' : 'done' }
    case 'Истребление': return { text: d.exterminationAwarded ? t('All five eliminated', 'Все пятеро уничтожены') : t(`${d.exterminationVictories}/5 unique victories`, `${d.exterminationVictories}/5 уникальных побед`), state: d.exterminationAwarded ? 'done' : 'live' }
    case 'Glory kill': return { text: t('Double Skill against adjacent enemies · wins grant stats', 'Двойной Скилл против соседних врагов · победы дают статы'), state: 'live' }
    case 'Щит-пила': return { text: t('Blocked attackers lose 3 score', 'Блок отнимает у атакующих −3 очка'), state: 'live' }
    case 'Шоковый щит': return { text: d.shockShieldUsed ? t('Discharge already spent', 'Разряд уже потрачен') : t('Discharge ready: the next enemy turn will be skipped', 'Разряд готов: следующий ход врага будет пропущен'), state: d.shockShieldUsed ? 'done' : 'live' }
    case 'Адский блок': return { text: d.hellBlockUsed ? t('666 Skill collected', '666 Скилла получено') : t(`${Math.min(2, d.blocksThisRound)}/2 attacks into this block`, `${Math.min(2, d.blocksThisRound)}/2 атак в текущий блок`), state: d.hellBlockUsed ? 'done' : 'live' }
    case 'Контр-атака': return { text: d.counterAttackMarkedNames.length ? t(`Vulnerable: ${d.counterAttackMarkedNames.join(', ')}`, `Уязвимы: ${d.counterAttackMarkedNames.join(', ')}`) : t('Waiting for an enemy to hit the block', 'Ждёт врага, ударившего в блок'), state: 'live' }
    case 'Щит-акула': return { text: d.sharkShieldActive ? t('Shark stance active', 'Стойка акулы активна') : t('A block will become shark stance', 'Блок станет стойкой акулы'), state: 'live' }
    case 'Адеские гнезда': return { text: t(`${d.demonNestNames.length}/3 active nests`, `${d.demonNestNames.length}/3 активных гнезда`), state: d.demonNestNames.length > 3 ? 'failed' : 'live' }
    case 'Навести беспорядок': return { text: t('+1 score for every real fight', '+1 очко за каждую реальную битву'), state: 'live' }
    case 'Стань богом':
      if (d.becomeGodAwarded) return { text: t('Trial complete: +20', 'Испытание завершено: +20'), state: 'done' }
      if (d.everBlocked || d.everLost) return { text: t(`Trial failed${d.everBlocked ? ': blocked' : ': lost a fight'}`, `Испытание провалено${d.everBlocked ? ': был блок' : ': было поражение'}`), state: 'failed' }
      return { text: t('Clean run: no blocks or losses', 'Чистый забег: без блоков и поражений'), state: 'live' }
    case 'Ближник': return { text: t('Adjacent melee bonuses are doubled', 'Melee-бонусы против соседей удвоены'), state: 'live' }
    case 'BFG': return { text: d.bfgCharged ? t('CHARGED — waiting for a random attack', 'ЗАРЯЖЕНА — ждёт атаки с рандомом') : t('Charge spent', 'Заряд потрачен'), state: d.bfgCharged ? 'live' : 'done' }
    case 'Рельса': return { text: d.railgunCharged ? t('CHARGED — the next attack hits one whole side', 'ЗАРЯЖЕНА — следующая атака бьёт всю сторону') : t('Charge spent', 'Заряд потрачен'), state: d.railgunCharged ? 'live' : 'done' }
    case 'Приручить дракона': return { text: t('Dragon transformation awaits round 10', 'Превращение в дракона ждёт 10-й ход'), state: 'live' }
    case 'Кулаки': return { text: t('STR = 0 · every victory gives +2 score', 'STR = 0 · каждая победа +2 очка'), state: 'live' }
    case 'Бензопила':
      if (d.copiedPassiveName) return { text: t(`Stolen: ${d.copiedPassiveName}`, `Украдено: ${d.copiedPassiveName}`), state: 'done' }
      return { text: d.chainsawSpent ? t(`Target sawed apart — ${d.chainsawSelectionsRemaining} choice(s) left`, `Жертва распилена — осталось выборов: ${d.chainsawSelectionsRemaining}`) : t('Ready for the next victory', 'Готова к следующей победе'), state: 'live' }
    default: return { text: t('Module active', 'Модуль активен'), state: 'live' }
  }
}
</script>

<template>
  <div class="player-card" :class="{ 'is-me': isMe, 'is-bug': isBug, 'is-dragon': passiveStates?.dragon, 'is-awakened': passiveStates?.dragon?.isAwakened, 'is-last-place': isMe && (player?.status?.place ?? 0) >= 6 }"
    :style="passiveStates?.privilege && passiveStates.privilege.markedCount > 0 ? { borderColor: 'rgba(205, 127, 50, 0.5)', boxShadow: '0 0 12px rgba(205, 127, 50, 0.2)' } : {}"
  >
    <div v-if="player.isTheBoysSupTarget" class="theboys-sup-target-badge" title="Супер — цель Бучера">
      <span aria-hidden="true">🦸</span>
      <span>СУПЕР</span>
    </div>

    <!-- Top grid: if not isMe, show avatar on right; if isMe, avatar lives in game-right -->
    <div class="pc-top-grid" :class="{ 'pc-top-no-avatar': isMe }">
    <div class="pc-top-left">

    <!-- Stats with bars + resist/quality -->
    <div class="pc-stats">
      <div v-if="hasLvlUpPoints" class="lvl-up-badge" :class="{ 'nerf-badge': isIrelia }" :style="levelUpTint ? { background: levelUpTint } : {}">
        {{ usesSpecialLevelUpPanel ? `${lvlUpPoints} особый выбор` : `+${lvlUpPoints} очков` }}
        <span v-if="levelUpQuip" class="lvl-up-quip">{{ levelUpQuip }}</span>
      </div>

      <SpecialLevelUpPanel
        v-if="hasLvlUpPoints && usesSpecialLevelUpPanel"
        :player="player"
        :round-no="roundNo"
        :submitting="store.isLevelingUp"
        @choose="handleLevelUp"
      />

      <div v-if="levelUpConsequences.length" class="special-levelup-notes" aria-live="polite">
        <strong>Особый эффект этой прокачки</strong>
        <span v-for="note in levelUpConsequences" :key="note">{{ note }}</span>
      </div>

      <!-- Intelligence -->
      <div class="stat-block" :class="{ 'resist-hit': resistFlash.includes('intelligence'), 'lvl-up-available': hasLvlUpPoints, 'stat-pulse': pulsingStats.has('intelligence') }">
        <div class="stat-row">
          <span class="gi gi-lg gi-int">{{ isEren ? 'Злость' : 'INT' }}</span>
          <div class="stat-bar-bg">
            <div v-if="showGhost.has('intelligence')" class="stat-bar-ghost intelligence" :style="{ width: `${(ghostStats?.int ?? 0) * 10}%` }" :key="'ghost-int-' + (ghostStats?.int ?? 0)" />
            <div class="stat-bar intelligence" :style="{ width: `${player.character.intelligence * 10}%` }" />
          </div>
          <span class="stat-val stat-intelligence">{{ player.character.intelligence }}</span>
          <button v-if="hasLvlUpPoints && !usesSpecialLevelUpPanel" class="lvl-btn" data-sfx-skip-default="true" :disabled="store.isLevelingUp" :title="isEren ? '+1 Злость' : '+1 Intelligence'" @click="handleLevelUp(1)">+</button>
        </div>
        <div v-if="isMe && !isMadara" class="resist-row">
          <span class="resist-badge"><span class="gi gi-def">DEF</span> {{ player.character.intelligenceResist }}</span>
          <span v-if="player.character.intelligenceBonusText" class="resist-bonus">{{ player.character.intelligenceBonusText }}</span>
        </div>
      </div>
      <!-- Strength -->
      <div class="stat-block" :class="{ 'resist-hit': resistFlash.includes('strength'), 'lvl-up-available': hasLvlUpPoints, 'stat-pulse': pulsingStats.has('strength') }">
        <div class="stat-row">
          <span class="gi gi-lg gi-str">STR</span>
          <div class="stat-bar-bg">
            <div v-if="showGhost.has('strength')" class="stat-bar-ghost strength" :style="{ width: `${(ghostStats?.str ?? 0) * 10}%` }" :key="'ghost-str-' + (ghostStats?.str ?? 0)" />
            <div class="stat-bar strength" :style="{ width: `${player.character.strength * 10}%` }" />
          </div>
          <span class="stat-val stat-strength">{{ player.character.strength }}</span>
          <button v-if="hasLvlUpPoints && !usesSpecialLevelUpPanel" class="lvl-btn" data-sfx-skip-default="true" :disabled="store.isLevelingUp" title="+1 Strength" @click="handleLevelUp(2)">+</button>
        </div>
        <div v-if="isMe && !isMadara" class="resist-row">
          <span class="resist-badge"><span class="gi gi-def">DEF</span> {{ player.character.strengthResist }}</span>
          <span v-if="player.character.strengthBonusText" class="resist-bonus">{{ player.character.strengthBonusText }}</span>
        </div>
      </div>
      <!-- Speed -->
      <div class="stat-block" :class="{ 'lvl-up-available': hasLvlUpPoints, 'stat-pulse': pulsingStats.has('speed') }">
        <div class="stat-row">
          <span class="gi gi-lg gi-spd">SPD</span>
          <div class="stat-bar-bg">
            <div v-if="showGhost.has('speed')" class="stat-bar-ghost speed" :style="{ width: `${(ghostStats?.spd ?? 0) * 10}%` }" :key="'ghost-spd-' + (ghostStats?.spd ?? 0)" />
            <div class="stat-bar speed" :style="{ width: `${player.character.speed * 10}%` }" />
          </div>
          <span class="stat-val stat-speed">{{ player.character.speed }}</span>
          <button v-if="hasLvlUpPoints && !usesSpecialLevelUpPanel" class="lvl-btn" data-sfx-skip-default="true" :disabled="store.isLevelingUp" title="+1 Speed" @click="handleLevelUp(3)">+</button>
        </div>
        <div v-if="isMe && !isMadara" class="resist-row">
          <span class="resist-badge"><span class="gi gi-def">DEF</span> {{ player.character.speedResist }}</span>
          <span v-if="player.character.speedBonusText" class="resist-bonus">{{ player.character.speedBonusText }}</span>
        </div>
      </div>
      <!-- Floating stat change numbers -->
      <TransitionGroup name="float-num" tag="div" class="floating-numbers-container">
        <span
          v-for="fn in floatingNumbers.filter(f => f.stat !== 'psyche')"
          :key="fn.id"
          class="floating-number"
          :class="[
            fn.delta > 0 ? 'float-positive' : 'float-negative',
            `float-${fn.stat}`,
            Math.abs(fn.delta) >= 3 ? 'float-big' : '',
          ]"
        >
          {{ fn.delta > 0 ? '+' : '' }}{{ fn.delta }} <span class="float-stat-label">{{ isEren && fn.stat === 'intelligence' ? 'Злость' : { intelligence: 'INT', strength: 'STR', speed: 'SPD', psyche: 'PSY' }[fn.stat] }}</span>
        </span>
      </TransitionGroup>
    </div>

    <!-- Psyche (separated — different stat type, hidden during kotiki lvl-up) -->
    <div class="pc-psyche-box">
      <div class="stat-block" :class="{ 'resist-hit': resistFlash.includes('psyche'), 'lvl-up-available': hasLvlUpPoints, 'stat-pulse': pulsingStats.has('psyche') }">
        <div class="stat-row">
          <span class="gi gi-lg gi-psy">{{ isEren ? 'Самоуверенность' : 'PSY' }}</span>
          <div class="stat-bar-bg">
            <div v-if="showGhost.has('psyche')" class="stat-bar-ghost psyche" :style="{ width: `${(ghostStats?.psy ?? 0) * 10}%` }" :key="'ghost-psy-' + (ghostStats?.psy ?? 0)" />
            <div class="stat-bar psyche" :style="{ width: `${player.character.psyche * 10}%` }" />
          </div>
          <span class="stat-val stat-psyche">{{ player.character.psyche }}</span>
          <button v-if="hasLvlUpPoints && !usesSpecialLevelUpPanel" class="lvl-btn" data-sfx-skip-default="true" :disabled="store.isLevelingUp" :title="isEren ? '+1 Самоуверенность' : '+1 Psyche'" @click="handleLevelUp(4)">+</button>
        </div>
        <div v-if="isMe && !isMadara" class="resist-row">
          <span class="resist-badge"><span class="gi gi-def">DEF</span> {{ player.character.psycheResist }}</span>
          <span v-if="player.character.psycheBonusText" class="resist-bonus">{{ player.character.psycheBonusText }}</span>
        </div>
      </div>
      <!-- Psyche floating number -->
      <TransitionGroup name="float-num" tag="div" class="floating-numbers-container">
        <span
          v-for="fn in floatingNumbers.filter(f => f.stat === 'psyche')"
          :key="fn.id"
          class="floating-number"
          :class="[fn.delta > 0 ? 'float-positive' : 'float-negative', 'float-psyche', Math.abs(fn.delta) >= 3 ? 'float-big' : '']"
        >
          {{ fn.delta > 0 ? '+' : '' }}{{ fn.delta }} <span class="float-stat-label">{{ isEren ? 'Самоуверенность' : 'PSY' }}</span>
        </span>
      </TransitionGroup>
    </div>

    <!-- Justice: highlighted, own row -->
    <div class="pc-justice-row" data-justice-target :class="{ 'justice-reset-flash': justiceReset, 'justice-up-sparkle': justiceUp }"
      @mouseenter="showTip($event, 'Justice allows you to win Round 2 and influences Round 1. You gain it when you\'re defeated and it is fully reset on victory')"
      @mousemove="moveTip" @mouseleave="hideTip">
      <span class="justice-icon">⚖</span>
      <span class="justice-label">Justice</span>
      <ScoreOdometer :value="player.character.justice" size="sm" class="justice-value" />
      <span v-if="justiceReset" class="justice-reset-label">RESET</span>
    </div>

    <!-- Moral / Skill / Class / Target -->
    <div class="pc-meta">
      <div v-if="!isMadara" class="meta-box"
        @mouseenter="showTip($event, 'You can exchange moral for Skill or Points. Gain it by winning and lose it when you\'re defeated')"
        @mousemove="moveTip" @mouseleave="hideTip">
        <span class="meta-label">Moral</span>
        <span class="meta-value stat-moral">{{ player.character.moralDisplay }}</span>
      </div>

      <div v-if="!isMadara" class="meta-box"
        @mouseenter="showTip($event, 'Skill influences your fighting power. Gain it by attacking your TARGET')"
        @mousemove="moveTip" @mouseleave="hideTip">
        <span class="meta-label">Skill</span>
        <span class="meta-value stat-skill">{{ player.character.skillDisplay }}</span>
      </div>

      <div v-if="classLabel" class="meta-box"
        @mouseenter="showTip($event, classTooltip)" @mousemove="moveTip" @mouseleave="hideTip">
        <span class="meta-label">Class</span>
        <span class="meta-value stat-class">{{ classLabel }}</span>
      </div>

      <div v-if="isMe && !isMadara && player.character.skillTarget" class="meta-box"
        @mouseenter="showTip($event, skillTargetTooltip(player.character.skillTarget))" @mousemove="moveTip" @mouseleave="hideTip">
        <span class="meta-label">Target</span>
        <span class="meta-value"><span :class="skillTargetBadge(player.character.skillTarget).cls" style="font-size:14px">{{ skillTargetBadge(player.character.skillTarget).label }}</span></span>
      </div>
    </div>

    </div><!-- /pc-top-left -->

    <!-- Right: avatar and identity (only for non-me, e.g. Replay enemy) -->
    <div v-if="!isMe" class="pc-top-right">
      <div class="pc-avatar-wrap" :class="[placeTier]">
        <img
          v-if="player.character.avatarCurrent"
          :src="player.character.avatarCurrent"
          :alt="player.character.name"
          class="pc-avatar-img"
        >
        <div v-else class="pc-avatar-fallback">
          {{ player.character.name.charAt(0) }}
        </div>
      </div>
      <div class="pc-identity">
        <div class="pc-name">
          {{ player.character.name }}
        </div>
        <div class="pc-username">{{ player.discordUsername }}</div>
      </div>
    </div><!-- /pc-top-right -->

    </div><!-- /pc-top-grid -->

    <!-- Moral exchange -->
    <div v-if="hasMoral && !doomGuy?.rollMode" class="pc-moral-actions">
      <div v-if="isLastRound" class="moral-last-round">Последний шанс!</div>
      <!-- Булькает: both disabled -->
      <template v-if="hasBulkaet">
        <button class="moral-btn moral-btn-disabled" disabled>Ничего не понимает, но Булькает!</button>
      </template>
      <template v-else>
        <!-- Points button (DeepList can't use) -->
        <button v-if="isDeepList" class="moral-btn moral-btn-disabled" disabled>Только скилл</button>
        <button v-else-if="moralToPointsRate" class="moral-btn" data-sfx-skip-default="true" @click="handleMoralToPoints()"
          :title="`Обменять ${moralToPointsRate.cost} Морали на ${moralToPointsRate.gain} бонусных очков`">
          {{ moralToPointsRate.cost }} Moral → {{ moralToPointsRate.gain }} pts
        </button>
        <button v-else class="moral-btn moral-btn-disabled" disabled>Мало морали</button>
        <!-- Skill button -->
        <button v-if="moralToSkillRate" class="moral-btn" data-sfx-skip-default="true" @click="handleMoralToSkill()"
          :title="`Обменять ${moralToSkillRate.cost} Морали на ${moralToSkillRate.gain} Cкилла`">
          {{ moralToSkillRate.cost }} Moral → {{ moralToSkillRate.gain }} skill
        </button>
        <button v-else class="moral-btn moral-btn-disabled" disabled>Мало морали</button>
      </template>
      <!-- Shinigami Eyes button for Kira -->
      <button
        v-if="store.isKira && moral >= 25"
        class="moral-btn shinigami-btn"
        @click="store.shinigamiEyes()"
        title="Глаза бога смерти: потратить 25 морали, чтобы увидеть имя следующего противника"
      >
        {{ t('Shinigami Eyes (25)', 'Глаза бога смерти (25)') }}
      </button>
    </div>

    <!-- Geralt contract demand (replaces moral area for Geralt) -->
    <div v-if="isMe && isGeralt && geralt" class="pc-geralt-demand" :class="geraltDemandContainerClass">
      <div class="geralt-demand-header" :style="geraltHeaderStyle">{{ t('Demand more coin for the contract', 'Потребовать больше монет за заказ') }}</div>
      <!-- Invoice breakdown (admin only) -->
      <div v-if="store.isAdmin && geralt.invoiceItems && geralt.invoiceItems.length > 0 && !geralt.demandedThisPhase" class="geralt-invoice">
        <div v-for="(item, idx) in geralt.invoiceItems" :key="idx" class="geralt-invoice-line">
          <span class="geralt-invoice-label">{{ item.label }}</span>
          <span class="geralt-invoice-pts" :class="item.points >= 0 ? 'pts-pos' : 'pts-neg'">{{ item.points >= 0 ? '+' : '' }}{{ item.points }}</span>
        </div>
        <div class="geralt-invoice-total" :class="invoiceTotalClass">
          <span>{{ t('Total:', 'Итого:') }}</span>
          <span class="geralt-invoice-total-val">{{ geralt.invoiceTotal }}</span>
        </div>
        <div class="geralt-invoice-prediction">
          <span v-if="(geralt.invoicePredictedCoins ?? 0) > 0" class="geralt-inv-coins">+{{ geralt.invoicePredictedCoins }} {{ t(geralt.invoicePredictedCoins === 1 ? 'coin' : 'coins', geralt.invoicePredictedCoins === 1 ? 'очко' : 'очка') }}</span>
          <span v-if="(geralt.invoicePredictedDispleasure ?? 0) > 0" class="geralt-inv-displ">+{{ geralt.invoicePredictedDispleasure }} {{ t('Displeasure', 'недовольство') }}</span>
          <span v-if="(geralt.invoicePredictedCoins ?? 0) === 0 && (geralt.invoicePredictedDispleasure ?? 0) === 0" class="geralt-inv-nothing">{{ t('Nothing', 'Ничего') }}</span>
        </div>
      </div>
      <div class="geralt-demand-btns">
        <button
          class="geralt-demand-btn"
          :disabled="!geralt.canDemandPrevious || geralt.demandedThisPhase"
          @click="store.demandContractReward('previous')"
        >
          {{ geralt.demandedThisPhase ? t('Already demanded', 'Уже потребовал') : geralt.canDemandPrevious ? demandPreviousBtnText : t('No contracts', 'Нет заказов') }}
        </button>
        <button
          class="geralt-demand-btn geralt-demand-next"
          :disabled="!geralt.canDemandNext"
          @click="store.demandContractReward('next')"
        >
          {{ geralt.advancePending ? t('Advance pending', 'Аванс в обработке') : t('Next round (+2)', 'За следующий (+2)') }}
        </button>
      </div>
      <div class="geralt-displeasure">
        <div class="geralt-displeasure-bar">
          <div
            v-for="i in 10" :key="i"
            class="geralt-displeasure-seg"
            :style="geraltSegStyle(i)"
          />
        </div>
        <span class="geralt-displeasure-text" :style="geraltDispleasureTextStyle">{{ geralt.displeasure }}/10</span>
      </div>
    </div>

    <!-- Portal Gun (Rick special ability) -->
    <div v-if="portalGun" class="pc-special-ability" :data-widget-help="widgetHelp('portal')" :aria-description="widgetHelp('portal')" tabindex="0" :class="{ 'sa-charged': portalGun.invented && portalGun.charges > 0 }">
      <div class="sa-header">{{ t('Portal Gun', 'Портальная пушка') }}</div>
      <div v-if="!portalGun.invented" class="sa-status sa-not-invented">
        {{ t('Not invented (INT 30)', 'Не изобретена (INT 30)') }}
      </div>
      <div v-else class="sa-status sa-invented">
        <span class="sa-charge-count">{{ portalGun.charges }}</span>
        <span class="sa-charge-label">{{ t('charges', 'зарядов') }}</span>
      </div>
    </div>

    <!-- Exploit state (Баг special ability) -->
    <div v-if="exploitState" class="pc-exploit-state" :data-widget-help="widgetHelp('exploit')" :aria-description="widgetHelp('exploit')" tabindex="0">
      <div class="exploit-header">
        <span class="exploit-title">EXPLOIT</span>
        <span class="exploit-progress">{{ exploitState.fixedCount }}/{{ exploitState.totalPlayers }}</span>
      </div>
      <div class="exploit-accumulated">
        <span class="exploit-value">{{ exploitState.totalExploit }}</span>
        <span class="exploit-label">{{ t('pending', 'в ожидании') }}</span>
      </div>
      <div class="exploit-bar-bg">
        <div class="exploit-bar-fill" :style="{ width: `${exploitState.totalPlayers > 0 ? (exploitState.fixedCount / exploitState.totalPlayers) * 100 : 0}%` }" />
      </div>
    </div>

    <!-- Tsukuyomi state (Itachi special ability) -->
    <div v-if="tsukuyomiState" class="pc-tsukuyomi-state" :data-widget-help="widgetHelp('tsukuyomi')" :aria-description="widgetHelp('tsukuyomi')" tabindex="0">
      <div class="tsukuyomi-header">
        <span class="tsukuyomi-title">TSUKUYOMI</span>
        <span class="tsukuyomi-charge" :class="{ 'tsukuyomi-ready': tsukuyomiState.isReady }">
          {{ tsukuyomiState.isReady ? t('READY', 'ГОТОВО') : `${tsukuyomiState.chargeCounter}/2` }}
        </span>
      </div>
      <div class="tsukuyomi-stolen">
        <span class="tsukuyomi-value">{{ tsukuyomiState.totalStolenPoints }}</span>
        <span class="tsukuyomi-label">{{ t('stolen', 'украдено') }}</span>
      </div>
      <div class="tsukuyomi-bar-bg">
        <div class="tsukuyomi-bar-fill" :style="{ width: `${(tsukuyomiState.chargeCounter / 2) * 100}%` }" />
      </div>
    </div>

    <!-- DooM Guy module controller -->
    <div v-if="doomGuy" class="pc-passive-widget doom-widget" :data-widget-help="widgetHelp('doom')" :aria-description="widgetHelp('doom')" tabindex="0">
      <div class="pw-header">
        <div>
          <span class="pw-title doom-title">FORTRESS OF DOOM</span>
          <span class="doom-subtitle">{{ t('COMBAT LOADOUT', 'БОЕВОЙ КОМПЛЕКТ') }}</span>
        </div>
        <span class="pw-status doom-mode" :class="doomGuy.rollMode ? 'doom-roll-active' : ''">{{ doomGuy.rollMode ? "LET'S ROLL" : t('MANUAL', 'ВРУЧНУЮ') }}</span>
      </div>
      <div class="doom-module-list" :class="{ 'doom-module-list--selecting': hasLvlUpPoints }">
        <div v-for="stage in doomStages" :key="stage.key" class="doom-module-card" :class="`doom-module-card--${doomModuleStatus(doomGuy.activeModules[stage.key] || '').state}`">
          <span class="doom-module-icon">{{ stage.icon }}</span>
          <span class="doom-module-copy">
            <small>{{ stage.label }}</small>
            <strong>{{ doomGuy.activeModules[stage.key] || t('Not selected', 'Не выбран') }}</strong>
            <span v-if="doomGuy.activeModules[stage.key]">{{ doomModuleStatus(doomGuy.activeModules[stage.key]).text }}</span>
            <span v-else>{{ t('Unlocks on turn', 'Откроется на ходу') }} {{ { Rune: 3, Shield: 5, Mission: 7, Gun: 9 }[stage.key] }}</span>
          </span>
        </div>
      </div>
      <div v-if="doomGuy.demonNestNames.length" class="doom-nests">
        <strong>🔥 {{ t('DEMON NESTS', 'ГНЕЗДА ДЕМОНОВ') }}</strong>
        <span v-for="name in doomGuy.demonNestNames" :key="name">{{ name }}</span>
      </div>
      <div v-if="doomGuy.bfgCharged" class="doom-bfg"><span>●</span> {{ t('BFG CHARGED', 'BFG ЗАРЯЖЕНА') }}</div>
      <div v-if="doomGuy.railgunCharged" class="doom-bfg"><span>●</span> {{ t('RAILGUN CHARGED', 'РЕЛЬСА ЗАРЯЖЕНА') }}</div>
      <div v-if="doomGuy.chainsawChoices.length" class="doom-chainsaw-choice">
        <strong>{{ t('CHAINSAW: CHOOSE A TROPHY', 'БЕНЗОПИЛА: ВЫБЕРИ ТРОФЕЙ') }}</strong>
        <span>{{ t('The Gun slot will be permanently replaced by the chosen passive.', 'Gun будет навсегда заменён выбранной пассивкой.') }}</span>
        <button v-for="choice in doomGuy.chainsawChoices" :key="choice.name" :title="choice.description" @click="handleDoomChainsaw(choice.name)">
          <b>{{ choice.name }}</b>
          <small>{{ choice.description }}</small>
        </button>
      </div>
    </div>

    <!-- Pickle Rick (Rick passive) -->
    <div v-if="passiveStates?.pickleRick" class="pc-passive-widget pickle-widget" :data-widget-help="widgetHelp('pickleRick')" :aria-description="widgetHelp('pickleRick')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title pickle-title">{{ t('PICKLE RICK', 'ОГУРЧИК РИК') }}</span>
        <span class="pw-status" :class="passiveStates.pickleRick.pickleTurnsRemaining > 0 ? 'pickle-active' : (passiveStates.pickleRick.penaltyTurnsRemaining > 0 ? 'pickle-penalty' : 'pickle-off')">
          {{ passiveStates.pickleRick.pickleTurnsRemaining > 0 ? t('PICKLE', 'ОГУРЧИК') : (passiveStates.pickleRick.penaltyTurnsRemaining > 0 ? t('PENALTY', 'ШТРАФ') : t('NORMAL', 'НОРМА')) }}
        </span>
      </div>
      <div class="pw-body">
        <div v-if="passiveStates.pickleRick.pickleTurnsRemaining > 0" class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.pickleRick.pickleTurnsRemaining }}</span>
          <span class="pw-label">{{ t('turns left', 'ходов осталось') }}</span>
        </div>
        <div v-if="passiveStates.pickleRick.penaltyTurnsRemaining > 0" class="pw-stat-pair">
          <span class="pw-value pickle-penalty-val">{{ passiveStates.pickleRick.penaltyTurnsRemaining }}</span>
          <span class="pw-label">{{ t('penalty', 'штраф') }}</span>
        </div>
        <div v-if="passiveStates.pickleRick.wasAttackedAsPickle" class="pw-stat-pair">
          <span class="pw-value pickle-attacked">!</span>
          <span class="pw-label">{{ t('was attacked', 'был атакован') }}</span>
        </div>
      </div>
    </div>

    <!-- Giant Beans (Rick passive) -->
    <div v-if="passiveStates?.giantBeans" class="pc-passive-widget beans-widget" :data-widget-help="widgetHelp('giantBeans')" :aria-description="widgetHelp('giantBeans')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title beans-title">{{ t('GIANT BEANS', 'ГИГАНТСКИЕ БОБЫ') }}</span>
        <span v-if="passiveStates.giantBeans.ingredientsActive" class="pw-status beans-cooking">{{ t('COOKING', 'ГОТОВЯТСЯ') }}</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.giantBeans.beanStacks }}</span>
          <span class="pw-label">{{ t('stacks', 'зарядов') }}</span>
        </div>
        <div v-if="passiveStates.giantBeans.ingredientsActive" class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.giantBeans.ingredientTargetCount }}</span>
          <span class="pw-label">{{ t('targets', 'целей') }}</span>
        </div>
      </div>
    </div>

    <!-- ── Passive Ability Widgets ── -->

    <!-- Эрен Йегер -->
    <div v-if="passiveStates?.eren" class="pc-passive-widget eren-widget" :data-widget-help="widgetHelp('eren')" :aria-description="widgetHelp('eren')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title eren-title">{{ t('RUMBLING', 'РОКОТ ЗЕМЛИ') }}</span>
        <span class="pw-status" :class="passiveStates.eren.losses < 2 ? 'eren-ready' : 'eren-failed'">
          {{ passiveStates.eren.losses }}/2 {{ t('losses', 'поражений') }}
        </span>
      </div>
      <div class="pw-body eren-stats">
        <span class="eren-titan-cooldown" :class="{ 'eren-titan-ready': passiveStates.eren.attackTitanCooldown === 0 }">
          ⚡ {{ t('Attack Titan', 'Атакующий Титан') }}:
          {{ passiveStates.eren.attackTitanActive
            ? t('ACTIVE', 'АКТИВЕН')
            : passiveStates.eren.attackTitanCooldown === 0
              ? t('READY', 'ГОТОВ')
              : passiveStates.eren.attackTitanCooldown }}
        </span>
        <span>{{ t('Rage', 'Злость') }} +{{ passiveStates.eren.rageGained }}</span>
        <span v-if="passiveStates.eren.rumblingTriggered">🌋 {{ t('place', 'место') }} {{ passiveStates.eren.rumblingPlace }}</span>
      </div>
      <div v-if="passiveStates.eren.hatredMarks.length" class="eren-marks">
        <span v-for="mark in passiveStates.eren.hatredMarks" :key="mark.playerName" class="eren-mark">
          🔥 {{ mark.playerName }} ×{{ mark.marks }}
        </span>
      </div>
    </div>

    <!-- Наруто -->
    <div v-if="passiveStates?.naruto" class="pc-passive-widget naruto-widget" :data-widget-help="widgetHelp('naruto')" :aria-description="widgetHelp('naruto')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title naruto-title">{{ t('HAREM NO JUTSU', 'ГАРЕМ НО ДЖУТСУ') }}</span>
        <span class="pw-status" :class="passiveStates.naruto.haremCooldown === 0 ? 'naruto-ready' : 'naruto-cooldown'">
          {{ passiveStates.naruto.haremActive
            ? t('ACTIVE', 'АКТИВЕН')
            : passiveStates.naruto.haremCooldown === 0
              ? t('READY', 'ГОТОВ')
              : `${t('COOLDOWN', 'ОТКАТ')}: ${passiveStates.naruto.haremCooldown}` }}
        </span>
      </div>
    </div>

    <!-- 1. Буль (Drowning) -->
    <div v-if="passiveStates?.bulk" class="pc-passive-widget bulk-widget" :data-widget-help="widgetHelp('bulk')" :aria-description="widgetHelp('bulk')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title bulk-title">{{ t('DROWNING', 'БУЛЬ') }}</span>
      </div>
      <div class="pw-body">
        <div class="bulk-chance-wrap">
          <span class="bulk-chance-value">{{ passiveStates.bulk.drownChance }}%</span>
          <span class="bulk-chance-label">{{ t('skip chance', 'шанс пропустить ход') }}</span>
          <div class="bulk-wave-bar">
            <div class="bulk-wave-fill" :style="{ width: `${passiveStates.bulk.drownChance}%` }" />
          </div>
        </div>
        <span v-if="passiveStates.bulk.isBuffed" class="bulk-buffed">BUFFED</span>
      </div>
    </div>

    <!-- 2. Я за чаем (Tea Time) -->
    <div v-if="passiveStates?.tea" class="pc-passive-widget tea-widget" :data-widget-help="widgetHelp('tea')" :aria-description="widgetHelp('tea')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title tea-title">{{ t('TEA TIME', 'Я ЗА ЧАЕМ') }}</span>
        <span class="pw-status" :class="passiveStates.tea.isReady ? 'tea-ready' : 'tea-brewing'">
          {{ passiveStates.tea.isReady ? t('READY', 'ГОТОВО') : t('BREWING', 'ЗАВАРИВАЕТСЯ') }}
        </span>
      </div>
    </div>

    <!-- 3. Еврей (Profit) -->
    <div v-if="passiveStates?.jew" class="pc-passive-widget jew-widget" :data-widget-help="widgetHelp('jew')" :aria-description="widgetHelp('jew')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title jew-title">{{ t('PROFIT', 'ПРОФИТ') }}</span>
      </div>
      <div class="pw-body">
        <span class="pw-value">{{ passiveStates.jew.stolenPsyche }}</span>
        <span class="pw-label">{{ t('Psyche stolen', 'украдено Психики') }}</span>
      </div>
    </div>

    <!-- 4. HardKitty (Friends) -->
    <div v-if="passiveStates?.hardKitty" class="pc-passive-widget hardkitty-widget" :data-widget-help="widgetHelp('hardKitty')" :aria-description="widgetHelp('hardKitty')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title hardkitty-title">{{ t('FRIENDS', 'ДРУЗЬЯ') }}</span>
        <span class="pw-status hardkitty-count">{{ passiveStates.hardKitty.friendsCount }}</span>
      </div>
      <div class="hardkitty-bar-bg">
        <div class="hardkitty-bar-fill" :style="{ width: `${Math.min(100, (passiveStates.hardKitty.friendsCount / 5) * 100)}%` }" />
      </div>
    </div>

    <!-- 5. Обучение (Training) -->
    <div v-if="passiveStates?.training" class="pc-passive-widget training-widget" :data-widget-help="widgetHelp('training')" :aria-description="widgetHelp('training')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title training-title">{{ t('TRAINING', 'ОБУЧЕНИЕ') }}</span>
        <span v-if="passiveStates.training.statName !== '—'" class="pw-status training-stat">{{ passiveStates.training.statName }}</span>
      </div>
      <div v-if="passiveStates.training.targetStatValue > 0" class="pw-body">
        <span class="pw-value">{{ passiveStates.training.targetStatValue }}</span>
        <span class="pw-label">{{ t('training target', 'цель тренировки') }}</span>
      </div>
      <div v-else class="pw-body">
        <span class="pw-label training-waiting">{{ t('Lose an attack to begin', 'Нужно атакующее поражение, чтобы начать') }}</span>
      </div>
    </div>

    <!-- 6. Дракон (Dragon) -->
    <div v-if="passiveStates?.dragon" class="pc-passive-widget dragon-widget" :data-widget-help="widgetHelp('dragon')" :aria-description="widgetHelp('dragon')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title dragon-title">{{ t('DRAGON', 'ДРАКОН') }}</span>
        <span class="pw-status" :class="passiveStates.dragon.isAwakened ? 'dragon-awakened' : 'dragon-sleeping'">
          {{ passiveStates.dragon.isAwakened ? t('AWAKENED', 'ПРОБУДИЛАСЬ') : t(`${passiveStates.dragon.roundsUntilAwaken} rounds`, `${passiveStates.dragon.roundsUntilAwaken} раунд.`) }}
        </span>
      </div>
      <div v-if="!passiveStates.dragon.isAwakened" class="dragon-bar-bg">
        <div class="dragon-bar-fill" :style="{ width: `${((10 - passiveStates.dragon.roundsUntilAwaken) / 10) * 100}%` }" />
      </div>
    </div>

    <!-- 7. Запах мусора (Garbage) -->
    <div v-if="passiveStates?.garbage" class="pc-passive-widget garbage-widget" :data-widget-help="widgetHelp('garbage')" :aria-description="widgetHelp('garbage')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title garbage-title">{{ t('GARBAGE SCENT', 'ЗАПАХ МУСОРА') }}</span>
        <span class="pw-status garbage-count">{{ passiveStates.garbage.markedCount }}/{{ passiveStates.garbage.totalTracked }}</span>
      </div>
      <div class="garbage-bar-bg">
        <div class="garbage-bar-fill" :style="{ width: `${passiveStates.garbage.totalTracked > 0 ? (passiveStates.garbage.markedCount / passiveStates.garbage.totalTracked) * 100 : 0}%` }" />
      </div>
    </div>

    <!-- 8. Научите играть (Copycat) -->
    <div v-if="passiveStates?.copycat" class="pc-passive-widget copycat-widget" :data-widget-help="widgetHelp('copycat')" :aria-description="widgetHelp('copycat')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title copycat-title">{{ t('COPYCAT', 'НАУЧИТЕ ИГРАТЬ') }}</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.copycat.copiedStatName }}</span>
          <span class="pw-label">{{ t('copied now', 'сейчас скопировано') }}</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.copycat.historyCount }}</span>
          <span class="pw-label">{{ t('copies', 'копирований') }}</span>
        </div>
      </div>
    </div>

    <!-- 9. Чернильная завеса (Ink Screen) -->
    <div v-if="passiveStates?.inkScreen" class="pc-passive-widget ink-widget" :data-widget-help="widgetHelp('inkScreen')" :aria-description="widgetHelp('inkScreen')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title ink-title">{{ t('INK SCREEN', 'ЧЕРНИЛЬНАЯ ЗАВЕСА') }}</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.inkScreen.fakeDefeatCount }}</span>
          <span class="pw-label">{{ t('fake defeats', 'ложных поражений') }}</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.inkScreen.totalDeferredScore }}</span>
          <span class="pw-label">{{ t('deferred score', 'отложено очков') }}</span>
        </div>
      </div>
    </div>

    <!-- 10. Тигр топ (Tiger Top) -->
    <div v-if="passiveStates?.tigerTop" class="pc-passive-widget tigertop-widget" :data-widget-help="widgetHelp('tigerTop')" :aria-description="widgetHelp('tigerTop')" tabindex="0" :class="{ 'tigertop-active': passiveStates.tigerTop.isActive }">
      <div class="pw-header">
        <span class="pw-title tigertop-title">{{ t('TIGER TOP', 'ТИГР ТОП') }}</span>
        <span class="pw-status" :class="passiveStates.tigerTop.isActive ? 'tigertop-on' : 'tigertop-off'">
          {{ passiveStates.tigerTop.isActive ? t('ACTIVE', 'АКТИВНО') : t('INACTIVE', 'НЕАКТИВНО') }}
        </span>
      </div>
      <div class="pw-body">
        <span class="pw-value">{{ passiveStates.tigerTop.swapsRemaining }}</span>
        <span class="pw-label">{{ t('swaps left', 'обменов осталось') }}</span>
      </div>
    </div>

    <!-- 11. Челюсти (Jaws) -->
    <div v-if="passiveStates?.jaws" class="pc-passive-widget jaws-widget" :data-widget-help="widgetHelp('jaws')" :aria-description="widgetHelp('jaws')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title jaws-title">{{ t('JAWS', 'ЧЕЛЮСТИ') }}</span>
        <svg class="jaws-shark" viewBox="0 0 40 20" :style="{ animationDuration: `${Math.max(0.3, 3 - passiveStates.jaws.currentSpeed * 0.2)}s` }">
          <path d="M2 10 L10 4 L18 8 L22 3 L28 8 L35 6 L38 10 L35 14 L28 12 L22 17 L18 12 L10 16 L2 10 Z" fill="currentColor" />
          <circle cx="32" cy="9" r="1.5" fill="var(--bg-card)" />
        </svg>
      </div>
      <div class="pw-body jaws-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.jaws.currentSpeed }}</span>
          <span class="pw-label">{{ t('Speed', 'Скорость') }}</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.jaws.uniqueDefeated }}</span>
          <span class="pw-label">{{ t('defeated', 'побеждено') }}</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.jaws.uniquePositions }}</span>
          <span class="pw-label">{{ t('places visited', 'мест посещено') }}</span>
        </div>
      </div>
    </div>

    <!-- 12. Привилегия (Privilege) -->
    <div v-if="passiveStates?.privilege" class="pc-passive-widget privilege-widget" :data-widget-help="widgetHelp('privilege')" :aria-description="widgetHelp('privilege')" tabindex="0" :class="{ 'privilege-active': passiveStates.privilege.markedCount > 0 }">
      <div class="pw-header">
        <span class="pw-title privilege-title">{{ t('PRIVILEGE', 'ПРИВИЛЕГИЯ') }}</span>
        <span class="pw-status privilege-count">{{ passiveStates.privilege.markedCount }}</span>
      </div>
      <div v-if="passiveStates.privilege.markedNames?.length" class="pw-body privilege-names">
        <span v-for="name in passiveStates.privilege.markedNames" :key="name" class="privilege-name-tag">{{ name }}</span>
      </div>
    </div>

    <!-- 13. Вампуризм (Vampirism) -->
    <div v-if="passiveStates?.vampirism" class="pc-passive-widget vampirism-widget" :data-widget-help="widgetHelp('vampirism')" :aria-description="widgetHelp('vampirism')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title vampirism-title">{{ t('VAMPIRISM', 'ВАМПУРИЗМ') }}</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.vampirism.activeFeeds }}</span>
          <span class="pw-label">{{ t('active bites', 'активных укусов') }}</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.vampirism.ignoredJustice }}</span>
          <span class="pw-label">{{ t('Justice bypassed', 'обход Справедливости') }}</span>
        </div>
      </div>
    </div>

    <!-- 14. Weedwick (Weed) -->
    <div v-if="passiveStates?.weed" class="pc-passive-widget weed-widget" :data-widget-help="widgetHelp('weed')" :aria-description="widgetHelp('weed')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title weed-title">WEED</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.weed.totalWeedAvailable }}</span>
          <span class="pw-label">{{ t('available', 'доступно') }}</span>
        </div>
        <div v-if="passiveStates.weed.lastHarvestRound > 0" class="pw-stat-pair">
          <span class="pw-value">{{ roundNo - passiveStates.weed.lastHarvestRound }}</span>
          <span class="pw-label">{{ t('rounds since harvest', 'раундов без сбора') }}</span>
        </div>
      </div>
    </div>

    <!-- 15. Сайтама (One Punch) -->
    <div v-if="passiveStates?.saitama" class="pc-passive-widget saitama-widget" :data-widget-help="widgetHelp('saitama')" :aria-description="widgetHelp('saitama')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title saitama-title">ONE PUNCH</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.saitama.deferredPoints }}</span>
          <span class="pw-label">{{ t('deferred score', 'отложено очков') }}</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ Number(passiveStates.saitama.deferredMoral).toFixed(1) }}</span>
          <span class="pw-label">{{ t('deferred Moral', 'отложено Морали') }}</span>
        </div>
      </div>
    </div>

    <!-- 16. Глаза бога смерти (Shinigami Eyes) -->
    <div v-if="passiveStates?.shinigamiEyes" class="pc-passive-widget shinigami-widget" :data-widget-help="widgetHelp('shinigamiEyes')" :aria-description="widgetHelp('shinigamiEyes')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title shinigami-title">{{ t('SHINIGAMI EYES', 'ГЛАЗА БОГА СМЕРТИ') }}</span>
        <span class="pw-status" :class="passiveStates.shinigamiEyes.isActive ? 'shinigami-on' : 'shinigami-off'">
          {{ passiveStates.shinigamiEyes.isActive ? t('ACTIVE', 'АКТИВНЫ') : t('INACTIVE', 'НЕАКТИВНЫ') }}
        </span>
      </div>
    </div>

    <!-- 17. Продавец (Seller) -->
    <div v-if="passiveStates?.seller" class="pc-passive-widget seller-widget" :data-widget-help="widgetHelp('seller')" :aria-description="widgetHelp('seller')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title seller-title">{{ t('SELLER', 'ПРОДАВЕЦ') }}</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.seller.cooldown }}</span>
          <span class="pw-label">{{ t('cooldown', 'откат') }}</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.seller.markedCount }}</span>
          <span class="pw-label">{{ t('marked', 'отмечено') }}</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ Math.round(passiveStates.seller.secretBuildSkill) }}</span>
          <span class="pw-label">{{ t('hidden Skill', 'скрытый Скилл') }}</span>
        </div>
      </div>
    </div>

    <!-- 19. Dopa -->
    <div v-if="passiveStates?.dopa" class="pc-passive-widget dopa-widget" :data-widget-help="widgetHelp('dopa')" :aria-description="widgetHelp('dopa')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title dopa-title">DOPA</span>
        <span v-if="passiveStates.dopa.chosenTactic" class="pw-status dopa-tactic">{{ passiveStates.dopa.chosenTactic }}</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value" :class="{ 'dopa-ready': passiveStates.dopa.visionReady }">{{ passiveStates.dopa.visionReady ? t('READY', 'ГОТОВО') : passiveStates.dopa.visionCooldown }}</span>
          <span class="pw-label">{{ t('Vision', 'обзор') }}</span>
        </div>
        <div v-if="passiveStates.dopa.needSecondAttack" class="pw-stat-pair">
          <span class="pw-value dopa-need-atk">{{ t('2nd', '2-я') }}</span>
          <span class="pw-label">{{ t('attack', 'атака') }}</span>
        </div>
      </div>
    </div>

    <!-- 20. Стая Гоблинов (Goblin Swarm) -->
    <div v-if="passiveStates?.goblinSwarm" class="pc-passive-widget goblin-widget" :data-widget-help="widgetHelp('goblinSwarm')" :aria-description="widgetHelp('goblinSwarm')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title goblin-title">{{ t('GOBLIN SWARM', 'СТАЯ ГОБЛИНОВ') }}</span>
        <span v-if="passiveStates.goblinSwarm.isInZiggurat" class="pw-status goblin-zig-active">🏛️ {{ t('ZIGGURAT', 'ЗИККУРАТ') }}</span>
      </div>
      <!-- Population bar -->
      <div class="goblin-pop-bar">
        <div class="goblin-pop-total">{{ passiveStates.goblinSwarm.totalGoblins }}</div>
        <div class="goblin-pop-track">
          <div class="goblin-seg goblin-seg-warrior" :style="{ width: warriorPct + '%' }" />
          <div class="goblin-seg goblin-seg-hob" :style="{ width: hobPct + '%' }" />
          <div class="goblin-seg goblin-seg-worker" :style="{ width: workerPct + '%' }" />
        </div>
      </div>
      <!-- Type breakdown -->
      <div class="goblin-types">
        <div class="goblin-type">
          <span class="goblin-type-icon">⚔️</span>
          <span class="goblin-type-val">{{ passiveStates.goblinSwarm.warriors }}</span>
          <span class="goblin-type-rate">1/{{ passiveStates.goblinSwarm.warriorRate }}</span>
        </div>
        <div class="goblin-type">
          <span class="goblin-type-icon">🧙</span>
          <span class="goblin-type-val">{{ passiveStates.goblinSwarm.hobs }}</span>
          <span class="goblin-type-rate">1/{{ passiveStates.goblinSwarm.hobRate }}</span>
        </div>
        <div class="goblin-type">
          <span class="goblin-type-icon">⛏️</span>
          <span class="goblin-type-val">{{ passiveStates.goblinSwarm.workers }}</span>
          <span class="goblin-type-rate">1/{{ passiveStates.goblinSwarm.workerRate }}</span>
        </div>
      </div>
      <!-- Ziggurat positions + Festival status -->
      <div class="goblin-footer" v-if="passiveStates.goblinSwarm.zigguratPositions.length || passiveStates.goblinSwarm.festivalUsed">
        <span v-for="pos in passiveStates.goblinSwarm.zigguratPositions" :key="pos" class="goblin-zig-badge">🏛️{{ pos }}</span>
        <span v-if="passiveStates.goblinSwarm.festivalUsed" class="goblin-festival-used">🎉 {{ t('Festival used', 'Праздник был') }}</span>
      </div>
    </div>

    <!-- 21. Котики (owner widget) -->
    <div v-if="passiveStates?.kotiki" class="pc-passive-widget kotiki-widget" :data-widget-help="widgetHelp('kotiki')" :aria-description="widgetHelp('kotiki')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title kotiki-title">{{ t('CATS', 'КОТИКИ') }}</span>
      </div>
      <div class="kotiki-info">
        <div class="kotiki-row">
          <span class="kotiki-label">{{ t('Taunts:', 'Провокации:') }}</span>
          <span class="kotiki-val">{{ passiveStates.kotiki.tauntedCount }}/{{ passiveStates.kotiki.tauntedMax }}</span>
        </div>
        <!-- Deployed cats -->
        <div v-if="passiveStates.kotiki.minkaOnPlayerName" class="kotiki-cat-card kotiki-cat-minka">
          <div class="kotiki-cat-header">
            <span class="kotiki-cat-icon">🐱</span>
            <span class="kotiki-cat-name">{{ t('Minka', 'Минька') }}</span>
            <span class="kotiki-cat-rounds">{{ passiveStates.kotiki.minkaRoundsOnEnemy }} {{ t('r.', 'р.') }}</span>
          </div>
          <div class="kotiki-cat-target">{{ t('on', 'на') }} {{ passiveStates.kotiki.minkaOnPlayerName }}</div>
        </div>
        <div v-if="passiveStates.kotiki.stormOnPlayerName" class="kotiki-cat-card kotiki-cat-storm">
          <div class="kotiki-cat-header">
            <span class="kotiki-cat-icon">🐱</span>
            <span class="kotiki-cat-name">{{ t('Storm', 'Штормяк') }}</span>
          </div>
          <div class="kotiki-cat-target">{{ t('on', 'на') }} {{ passiveStates.kotiki.stormOnPlayerName }}</div>
        </div>
        <!-- Cooldowns -->
        <div v-if="passiveStates.kotiki.minkaCooldown > 0" class="kotiki-row kotiki-cooldown">
          <span class="kotiki-label">{{ t('Minka cooldown:', 'Минька откат:') }}</span>
          <span class="kotiki-val">{{ passiveStates.kotiki.minkaCooldown }}</span>
        </div>
        <div v-if="passiveStates.kotiki.stormCooldown > 0" class="kotiki-row kotiki-cooldown">
          <span class="kotiki-label">{{ t('Storm cooldown:', 'Штормяк откат:') }}</span>
          <span class="kotiki-val">{{ passiveStates.kotiki.stormCooldown }}</span>
        </div>
      </div>
    </div>

    <!-- 22. Монстр без имени (owner widget) -->
    <div v-if="passiveStates?.monster" class="pc-passive-widget monster-widget" :data-widget-help="widgetHelp('monster')" :aria-description="widgetHelp('monster')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title monster-title">{{ t('MONSTER', 'МОНСТР') }}</span>
      </div>
      <div class="monster-info">
        <div class="monster-row">
          <span class="monster-label">{{ t('Pawns:', 'Пешки:') }}</span>
          <span class="monster-val">{{ passiveStates.monster.pawnCount }}</span>
        </div>
      </div>
    </div>

    <!-- 23. Подсчет (Tolya Count) -->
    <div v-if="passiveStates?.tolyaCount" class="pc-passive-widget tolya-widget" :data-widget-help="widgetHelp('tolyaCount')" :aria-description="widgetHelp('tolyaCount')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title tolya-title">{{ t('COUNT', 'ПОДСЧЕТ') }}</span>
        <span class="pw-status" :class="passiveStates.tolyaCount.isReady ? 'tolya-ready' : 'tolya-cooldown'">
          {{ passiveStates.tolyaCount.isReady ? t('READY', 'ГОТОВО') : passiveStates.tolyaCount.cooldown }}
        </span>
      </div>
    </div>

    <!-- 24. Импакт (LeCrisp) -->
    <div v-if="passiveStates?.impact" class="pc-passive-widget impact-widget" :data-widget-help="widgetHelp('impact')" :aria-description="widgetHelp('impact')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title impact-title">{{ t('IMPACT', 'ИМПАКТ') }}</span>
        <span class="pw-status impact-streak" :class="{ 'impact-low': passiveStates.impact.streak <= 1, 'impact-mid': passiveStates.impact.streak >= 2 && passiveStates.impact.streak <= 3, 'impact-high': passiveStates.impact.streak >= 4 }">
          {{ passiveStates.impact.streak }}x
        </span>
      </div>
    </div>

    <!-- 25. Darksci (Luck) -->
    <div v-if="passiveStates?.darksci" class="pc-passive-widget darksci-widget" :data-widget-help="widgetHelp('darksci')" :aria-description="widgetHelp('darksci')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title darksci-title">{{ passiveStates.darksci.typeChosen ? (passiveStates.darksci.isStableType ? t('STABLE', 'СТАБИЛЬНЫЙ') : t('RISKY', 'РИСКОВЫЙ')) : t('LUCKY', 'ПОВЕЗЛО') }}</span>
        <span class="pw-status darksci-left">{{ t(`${passiveStates.darksci.uniqueEnemiesLeft} left`, `осталось ${passiveStates.darksci.uniqueEnemiesLeft}`) }}</span>
      </div>
    </div>

    <!-- 26. DeepList -->
    <div v-if="passiveStates?.deepList" class="pc-passive-widget deeplist-widget" :data-widget-help="widgetHelp('deepList')" :aria-description="widgetHelp('deepList')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title deeplist-title">DEEPLIST</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.deepList.knownCount }}</span>
          <span class="pw-label">{{ t('known', 'изучено') }}</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.deepList.mockeryTriggered }}</span>
          <span class="pw-label">{{ t('Mockery triggers', 'срабатываний Стёба') }}</span>
        </div>
      </div>
    </div>

    <!-- 27. Краборак (Shell) -->
    <div v-if="passiveStates?.craboRack" class="pc-passive-widget craborack-widget" :data-widget-help="widgetHelp('craboRack')" :aria-description="widgetHelp('craboRack')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title craborack-title">{{ t('SHELL', 'ПАНЦИРЬ') }}</span>
        <span class="pw-status craborack-count">{{ 5 - passiveStates.craboRack.shellsUsed }}/5</span>
      </div>
    </div>

    <!-- 28. Napoleon (Alliance) -->
    <div v-if="passiveStates?.napoleon" class="pc-passive-widget napoleon-widget" :data-widget-help="widgetHelp('napoleon')" :aria-description="widgetHelp('napoleon')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title napoleon-title">{{ t('NAPOLEON', 'НАПОЛЕОН') }}</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair" v-if="passiveStates.napoleon.allyName">
          <span class="pw-value">{{ passiveStates.napoleon.allyName }}</span>
          <span class="pw-label">{{ t('ally', 'союзник') }}</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.napoleon.treatyCount }}</span>
          <span class="pw-label">{{ t('treaties', 'договоров') }}</span>
        </div>
      </div>
    </div>

    <!-- 29. Суппорт (Carry) -->
    <div v-if="passiveStates?.support" class="pc-passive-widget support-widget" :data-widget-help="widgetHelp('support')" :aria-description="widgetHelp('support')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title support-title">PREMADE</span>
        <span class="pw-status support-carry">{{ passiveStates.support.carryName || '—' }}</span>
      </div>
    </div>

    <!-- 30. Toxic Mate (Cancer owner) -->
    <div v-if="passiveStates?.toxicMate" class="pc-passive-widget toxic-widget" :data-widget-help="widgetHelp('toxicMate')" :aria-description="widgetHelp('toxicMate')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title toxic-title">CANCER</span>
        <span class="pw-status" :class="passiveStates.toxicMate.cancerActive ? 'toxic-active' : 'toxic-inactive'">
          {{ passiveStates.toxicMate.cancerActive ? t('ACTIVE', 'АКТИВНО') : t('DORMANT', 'СПИТ') }}
        </span>
      </div>
      <div class="pw-body" v-if="passiveStates.toxicMate.cancerActive">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.toxicMate.transferCount }}</span>
          <span class="pw-label">{{ t('transfers', 'передач') }}</span>
        </div>
        <div v-if="passiveStates.toxicMate.currentHolderName" class="pw-stat-pair">
          <span class="pw-value toxic-holder">{{ passiveStates.toxicMate.currentHolderName }}</span>
          <span class="pw-label">{{ t('carrier', 'носитель') }}</span>
        </div>
      </div>
    </div>

    <!-- 31. Молодой Глеб (Tea) -->
    <div v-if="passiveStates?.yongGleb" class="pc-passive-widget yonggleb-widget" :data-widget-help="widgetHelp('yongGleb')" :aria-description="widgetHelp('yongGleb')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title yonggleb-title">{{ t('CALM', 'СПОКОЙСТВИЕ') }}</span>
        <span class="pw-status" :class="passiveStates.yongGleb.teaReady ? 'yonggleb-ready' : 'yonggleb-cooldown'">
          {{ passiveStates.yongGleb.teaReady ? t('READY', 'ГОТОВО') : passiveStates.yongGleb.teaCooldown }}
        </span>
      </div>
    </div>

    <!-- 32. TheBoys -->
    <div v-if="passiveStates?.theBoys" class="pc-passive-widget theboys-widget" :data-widget-help="widgetHelp('theBoys')" :aria-description="widgetHelp('theBoys')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title theboys-title">THE BOYS</span>
      </div>
      <div class="theboys-grid">
        <!-- Francie -->
        <div class="theboys-member">
          <div class="theboys-member-header">
            <span class="theboys-icon">🧪</span>
            <span class="theboys-name">{{ t('Frenchie', 'Француз') }}</span>
            <span class="theboys-val">Lv{{ passiveStates.theBoys.chemWeaponLevel }}</span>
          </div>
          <div v-if="passiveStates.theBoys.orderTargetName" class="theboys-order">
            🎯 {{ passiveStates.theBoys.orderTargetName }}
            <span class="theboys-order-rounds">({{ passiveStates.theBoys.orderRoundsLeft }})</span>
          </div>
          <div class="theboys-stats">
            <span class="theboys-stat-ok">✅{{ passiveStates.theBoys.ordersCompleted }}</span>
            <span class="theboys-stat-fail">❌{{ passiveStates.theBoys.ordersFailed }}</span>
          </div>
        </div>
        <!-- Butcher -->
        <div class="theboys-member theboys-butcher" :style="{ boxShadow: passiveStates.theBoys.pokerCount > 0 ? `0 0 ${4 + passiveStates.theBoys.pokerCount * 4}px rgba(255,50,50,${Math.min(0.2 + passiveStates.theBoys.pokerCount * 0.2, 1)})` : 'none' }">
          <div class="theboys-member-header">
            <span class="theboys-icon">🔪</span>
            <span class="theboys-name">{{ t('Butcher', 'Бучер') }}</span>
            <span class="theboys-val theboys-poker-val">{{ passiveStates.theBoys.pokerCount }}</span>
          </div>
        </div>
        <!-- Kimiko -->
        <div class="theboys-member" :class="{ 'theboys-disabled': passiveStates.theBoys.kimikoDisabled }">
          <div class="theboys-member-header">
            <span class="theboys-icon">💚</span>
            <span class="theboys-name">Kimiko</span>
            <span class="theboys-val">Lv{{ passiveStates.theBoys.regenLevel }}</span>
          </div>
          <div v-if="passiveStates.theBoys.kimikoDisabled" class="theboys-kimiko-status">{{ t('DISABLED', 'ОТКЛЮЧЕНА') }}</div>
          <div v-if="passiveStates.theBoys.totalJusticeBlocked > 0" class="theboys-kimiko-blocked">
            ⚖️ {{ t(`${passiveStates.theBoys.totalJusticeBlocked} blocked`, `заблокировано ${passiveStates.theBoys.totalJusticeBlocked}`) }}
          </div>
        </div>
        <!-- M.M. -->
        <div class="theboys-member" :class="{ 'theboys-calm-member': passiveStates.theBoys.isCalm }">
          <div class="theboys-member-header">
            <span class="theboys-icon">📋</span>
            <span class="theboys-name">М.М.</span>
            <span class="theboys-val">Lv{{ passiveStates.theBoys.mmUpgradeLevel }} <span class="theboys-kompromat-count">📁{{ passiveStates.theBoys.kompromatCount }}</span></span>
          </div>
          <div v-if="passiveStates.theBoys.isCalm" class="theboys-mm-active theboys-calm">🧘 {{ t('CALM', 'СПОКОЕН') }}</div>
          <div v-else-if="passiveStates.theBoys.nextAttackGathersKompromat" class="theboys-mm-active">📡 {{ t('GATHERING ACTIVE', 'СБОР АКТИВЕН') }}</div>
          <div v-if="passiveStates.theBoys.kompromatEntries?.length" class="theboys-kompromat-list">
            <div v-for="entry in passiveStates.theBoys.kompromatEntries" :key="entry.targetName" class="theboys-kompromat-entry">
              <span class="theboys-kompromat-name">{{ entry.targetName }}</span>
              <span class="theboys-kompromat-hint">{{ entry.hint }}</span>
            </div>
          </div>
        </div>
      </div>
      <!-- Ultimate / status indicators -->
      <div v-if="passiveStates.theBoys.superDickActive || passiveStates.theBoys.livingWeapon || passiveStates.theBoys.virusArmed" class="theboys-ultimates">
        <span v-if="passiveStates.theBoys.superDickActive" class="theboys-ult-badge theboys-ult-superdick">💀 {{ t('SuperDick', 'СуперМудень') }}</span>
        <span v-if="passiveStates.theBoys.livingWeapon" class="theboys-ult-badge theboys-ult-livingweapon">⚔️ {{ t('Living Weapon', 'Живое Оружие') }}</span>
        <span v-if="passiveStates.theBoys.virusArmed" class="theboys-ult-badge theboys-ult-virus">☣️ {{ t('Virus ready', 'Вирус готов') }}</span>
      </div>
      <!-- Infected players (owner view) -->
      <div v-if="passiveStates.theBoys.virusNames?.length" class="theboys-marks-row">
        <span class="theboys-marks-label">☣️</span>
        <span v-for="v in passiveStates.theBoys.virusNames" :key="v" class="theboys-mark-chip theboys-mark-virus">{{ v }}</span>
      </div>
    </div>

    <!-- 33. Salldorum -->
    <div v-if="passiveStates?.salldorum" class="pc-passive-widget salldorum-widget" :data-widget-help="widgetHelp('salldorum')" :aria-description="widgetHelp('salldorum')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title salldorum-title">SALLDORUM</span>
      </div>
      <div class="salldorum-body">
        <!-- Shen -->
        <div class="salldorum-row">
          <span class="salldorum-label">Shen</span>
          <span class="salldorum-val">{{ passiveStates.salldorum.shenCharges }}</span>
          <span v-if="passiveStates.salldorum.shenCharges > 0" class="salldorum-active">{{ t('NEXT ATTACK', 'СЛЕДУЮЩАЯ АТАКА') }}</span>
        </div>
        <!-- Cola -->
        <div class="salldorum-row">
          <span class="salldorum-label">Cola</span>
          <span v-if="passiveStates.salldorum.colaBuried" class="salldorum-val">
            {{ t('place', 'место') }} {{ passiveStates.salldorum.colaBuriedPosition }} ·
            <span v-if="passiveStates.salldorum.colaReady" class="salldorum-available">{{ t('READY TO PICK UP', 'ГОТОВА К ПОДБОРУ') }}</span>
            <span v-else>{{ t('ready in round', 'готова в раунде') }} {{ passiveStates.salldorum.colaReadyRound }}</span>
          </span>
          <span v-else-if="passiveStates.salldorum.colaDrinks > 0" class="salldorum-used">{{ t('Spent', 'Потрачено') }}</span>
          <span v-else class="salldorum-used">{{ t('NOT BURIED', 'НЕ ЗАКОПАНА') }}</span>
        </div>
        <!-- Chronicler -->
        <div class="salldorum-row">
          <span class="salldorum-label">Rewrite</span>
          <span v-if="passiveStates.salldorum.historyRewritten" class="salldorum-used">
            {{ t('USED', 'ИСПОЛЬЗОВАНО') }}<template v-if="passiveStates.salldorum.rewrittenRound > 0"> · {{ t('round', 'раунд') }} {{ passiveStates.salldorum.rewrittenRound }}</template>
          </span>
          <span v-else class="salldorum-available">{{ t('READY', 'ГОТОВО') }}</span>
        </div>
      </div>
    </div>

    <!-- 25. Геральт (owner widget) -->
    <div v-if="passiveStates?.geralt" class="pc-passive-widget geralt-widget" :data-widget-help="widgetHelp('geralt')" :aria-description="widgetHelp('geralt')" tabindex="0">
      <div class="pw-header">
        <span class="pw-title geralt-title">{{ t('CONTRACT BOARD', 'ДОСКА ЗАКАЗОВ') }}</span>

      </div>
      <div class="geralt-body">
        <div class="geralt-row" style="border-left-color: #3B82F6; background: #3B82F612;">
          <span style="color: #3B82F6">💀 {{ t('Drowners', 'Утопцы') }}</span>
          <span style="color: #3B82F6">x{{ passiveStates.geralt.drownersContracts }}</span>
          <span class="geralt-oil-tier">{{ geraltOilLabel(passiveStates.geralt.drownersOilTier) }}</span>
        </div>
        <div class="geralt-row" style="border-left-color: #22C55E; background: #22C55E12;">
          <span style="color: #22C55E">🐺 {{ t('Werewolves', 'Волколаки') }}</span>
          <span style="color: #22C55E">x{{ passiveStates.geralt.werewolvesContracts }}</span>
          <span class="geralt-oil-tier">{{ geraltOilLabel(passiveStates.geralt.werewolvesOilTier) }}</span>
        </div>
        <div class="geralt-row" style="border-left-color: #A855F7; background: #A855F712;">
          <span style="color: #A855F7">🦇 {{ t('Vampires', 'Вампиры') }}</span>
          <span style="color: #A855F7">x{{ passiveStates.geralt.vampiresContracts }}</span>
          <span class="geralt-oil-tier">{{ geraltOilLabel(passiveStates.geralt.vampiresOilTier) }}</span>
        </div>
        <div class="geralt-row" style="border-left-color: #EF4444; background: #EF444412;">
          <span style="color: #EF4444">🐉 {{ t('Dragons', 'Драконы') }}</span>
          <span style="color: #EF4444">x{{ passiveStates.geralt.dragonsContracts }}</span>
          <span class="geralt-oil-tier">{{ geraltOilLabel(passiveStates.geralt.dragonsOilTier) }}</span>
        </div>
        <div class="geralt-status-row">
          <span v-if="passiveStates.geralt.revealedCount > 0">{{ t('Senses:', 'Чутьё:') }} {{ passiveStates.geralt.revealedCount }}/4</span>
        </div>
      </div>
    </div>

    <!-- Score + animated delta -->
    <div class="pc-score-row" :class="{ 'confetti-burst': showConfetti }">
      <ScoreOdometer :value="player.status.score" size="lg" :flash-color="animatedScoreDelta > 0 ? '#5ba85b' : animatedScoreDelta < 0 ? '#e05545' : null" class="pc-score" />
      <span class="pc-score-label" :class="{ 'pc-score-label-geralt': isGeralt }">{{ isGeralt ? t('minted\ncoins', 'чеканные\nмонеты') : 'pts' }}</span>
      <span v-if="animatedScoreDelta !== 0" class="pc-score-delta" :class="{ 'delta-big': allAnimHits.length >= 4, 'delta-huge': allAnimHits.length >= 6, 'delta-negative': animatedScoreDelta < 0 }" :key="animatedScoreDelta">
        {{ animatedScoreDelta > 0 ? '+' : '' }}{{ animatedScoreDelta }}
      </span>
      <span v-if="hitActiveIdx >= 0 && allAnimHits[hitActiveIdx]?.comboIndex > 0" class="combo-multiplier" :key="hitActiveIdx"
        :style="comboHeatStyle(allAnimHits[hitActiveIdx].comboIndex + 1)">
        COMBO {{ allAnimHits[hitActiveIdx].comboIndex + 1 }}
      </span>
      <!-- Confetti particles for big score gains -->
      <div v-if="showConfetti" class="confetti-container">
        <span v-for="n in 12" :key="n" class="confetti-particle" />
      </div>
    </div>

    <!-- Score combo feed: each source as separate row, Regular / Bonus sections -->
    <div v-if="isMe && allAnimHits.length > 0" class="pc-combo-feed">
      <template v-for="(group, gIdx) in scoreGroups" :key="gIdx">
        <div
          class="combo-section"
          :class="[
            group.type === 'regular' ? 'combo-section-regular' : 'combo-section-bonus',
            { 'combo-visible': hitVisibleCount > allAnimHits.findIndex(h => h.groupType === group.type) }
          ]"
        >
          <!-- Section header -->
          <div class="combo-section-header">
            <span class="combo-section-label">{{ group.type === 'regular' ? 'Regular' : 'Bonus' }}</span>
            <span v-if="group.type === 'regular' && (group.multiplier > 1 || isMultiplierModified)" class="combo-mult-badge" :class="{ 'combo-mult-modified': isMultiplierModified }">
              <span v-if="isMultiplierModified" class="combo-mult-expected">x{{ expectedMultiplier }}</span>
              x{{ group.multiplier }} point multiplier
            </span>
          </div>

          <!-- Source rows (each source = separate row) -->
          <div
            v-for="(hit, hIdx) in allAnimHits.filter(h => h.groupType === group.type)"
            :key="hIdx"
            class="combo-entry"
            :class="[
              group.type === 'regular' ? 'combo-type-regular' : 'combo-type-bonus',
              {
                'combo-visible': (() => {
                  const globalIdx = allAnimHits.indexOf(hit)
                  return globalIdx >= 0 && globalIdx < hitVisibleCount
                })(),
                'combo-active': allAnimHits.indexOf(hit) === hitActiveIdx,
                'combo-negative': hit.pointsEarned < 0,
              }
            ]"
          >
            <span class="combo-hit-pts" :class="{ 'combo-hit-negative': hit.pointsEarned < 0 }">
              {{ hit.pointsEarned > 0 ? '+' : '' }}{{ hit.pointsEarned }}
            </span>
            <span class="combo-hit-label">{{ hit.name }}</span>
            <span v-if="hit.comboIndex > 0" class="combo-badge" :style="comboHeatStyle(hit.comboIndex + 1)">COMBO {{ hit.comboIndex + 1 }}</span>
          </div>

          <!-- Section total -->
          <div
            v-if="(() => {
              const groupHits = allAnimHits.filter(h => h.groupType === group.type)
              const lastGlobalIdx = allAnimHits.indexOf(groupHits[groupHits.length - 1])
              return lastGlobalIdx >= 0 && lastGlobalIdx < hitVisibleCount
            })()"
            class="combo-section-total"
            :class="group.type === 'regular' ? 'combo-total-regular' : 'combo-total-bonus'"
          >
            Total: {{ group.totalPoints > 0 ? '+' : '' }}{{ group.totalPoints }} pts
          </div>
        </div>
      </template>
    </div>

    <!-- Fight bonuses (Skill, Justice, Moral gains from fights) -->
    <div v-if="isMe && fightBonuses.length > 0" class="pc-fight-bonuses">
      <div class="fight-bonus-header">Fight Gains</div>
      <div v-for="(bonus, bIdx) in fightBonuses" :key="bIdx" class="fight-bonus-entry" :class="bonus.cssClass">
        <span class="fight-bonus-value">{{ bonus.value }}</span>
        <span class="fight-bonus-label">{{ bonus.label }}</span>
      </div>
    </div>

    <!-- In-game tooltip (teleported to body for correct positioning) -->
    <Teleport to="body">
      <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
        {{ tipText }}
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.player-card {
  position: relative;
  background: var(--glass-bg);
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-lg);
  padding: 14px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  box-shadow: var(--shadow-glow), inset 0 1px 0 var(--glass-highlight);
  transition: border-color 0.3s, box-shadow 0.3s;
}

.player-card.is-me {
  border-color: rgba(240, 200, 80, 0.2);
  box-shadow: var(--glow-gold), var(--shadow-glow), inset 0 1px 0 var(--glass-highlight);
}

/* Phase 9b: Last-place heartbeat — double-beat pulse on card border */
.player-card.is-last-place {
  animation: heartbeat 1.5s ease-in-out infinite;
}

@keyframes heartbeat {
  0%, 100% { border-color: rgba(240, 200, 80, 0.2); }
  14% { border-color: rgba(224, 85, 69, 0.4); }
  28% { border-color: rgba(240, 200, 80, 0.2); }
  42% { border-color: rgba(224, 85, 69, 0.5); }
  56% { border-color: rgba(240, 200, 80, 0.2); }
}

/* ── Top grid: stats left, avatar right ── */
.pc-top-grid {
  display: grid;
  grid-template-columns: 1fr 140px;
  gap: 10px;
  align-items: start;
}
.pc-top-grid.pc-top-no-avatar {
  grid-template-columns: 1fr;
}

.pc-top-left {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0; /* prevent overflow */
}

.pc-top-right {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}

/* Avatar */
.pc-avatar-wrap {
  width: 140px;
  height: 140px;
  border-radius: var(--radius-lg);
  overflow: hidden;
  background: var(--bg-inset);
  border: 2px solid var(--border-subtle);
  transition: border-color 0.8s ease, box-shadow 0.8s ease, filter 0.8s ease;
  position: relative;
}

/* ── LoL-style rank frames ─────────────────────────────────────────── */

/* 1st place — Challenger: animated iridescent border */
.pc-avatar-wrap.place-1 {
  border-width: 3px;
  border-color: rgba(240, 200, 80, 0.7);
  box-shadow:
    0 0 16px rgba(240, 200, 80, 0.35),
    0 0 40px rgba(240, 200, 80, 0.12),
    inset 0 0 12px rgba(240, 200, 80, 0.08);
  animation: frame-challenger 3s ease-in-out infinite;
}
.pc-avatar-wrap.place-1::after {
  content: '';
  position: absolute;
  inset: -3px;
  border-radius: inherit;
  background: conic-gradient(
    from 0deg,
    rgba(240,200,80,0.3),
    rgba(255,160,60,0.2),
    rgba(240,200,80,0.3),
    rgba(255,220,120,0.2),
    rgba(240,200,80,0.3)
  );
  mask: linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0);
  mask-composite: exclude;
  -webkit-mask: linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0);
  -webkit-mask-composite: xor;
  padding: 3px;
  animation: frame-rotate 4s linear infinite;
  pointer-events: none;
  z-index: 1;
}
@keyframes frame-challenger {
  0%, 100% { box-shadow: 0 0 16px rgba(240,200,80,0.35), 0 0 40px rgba(240,200,80,0.12), inset 0 0 12px rgba(240,200,80,0.08); }
  50% { box-shadow: 0 0 22px rgba(240,200,80,0.5), 0 0 50px rgba(240,200,80,0.18), inset 0 0 16px rgba(240,200,80,0.12); }
}
@keyframes frame-rotate {
  from { filter: hue-rotate(0deg); }
  to { filter: hue-rotate(360deg); }
}

/* 2nd place — Diamond: ice blue shimmer */
.pc-avatar-wrap.place-2 {
  border-width: 3px;
  border-color: rgba(140, 200, 255, 0.5);
  box-shadow:
    0 0 14px rgba(140, 200, 255, 0.2),
    0 0 30px rgba(140, 200, 255, 0.08),
    inset 0 0 8px rgba(140, 200, 255, 0.06);
  animation: frame-diamond 2.5s ease-in-out infinite;
}
.pc-avatar-wrap.place-2::after {
  content: '';
  position: absolute;
  inset: -3px;
  border-radius: inherit;
  background: linear-gradient(135deg, rgba(140,200,255,0.25), transparent 40%, transparent 60%, rgba(185,230,255,0.2));
  pointer-events: none;
  z-index: 1;
  animation: frame-diamond-shine 3s ease-in-out infinite;
}
@keyframes frame-diamond {
  0%, 100% { box-shadow: 0 0 14px rgba(140,200,255,0.2), 0 0 30px rgba(140,200,255,0.08), inset 0 0 8px rgba(140,200,255,0.06); }
  50% { box-shadow: 0 0 18px rgba(140,200,255,0.3), 0 0 36px rgba(140,200,255,0.12), inset 0 0 10px rgba(140,200,255,0.08); }
}
@keyframes frame-diamond-shine {
  0%, 100% { opacity: 0.6; }
  50% { opacity: 1; }
}

/* 3rd place — Gold: warm metallic glow */
.pc-avatar-wrap.place-3 {
  border-width: 2.5px;
  border-color: rgba(205, 160, 80, 0.5);
  box-shadow:
    0 0 10px rgba(205, 160, 80, 0.18),
    0 0 24px rgba(205, 160, 80, 0.06);
}
.pc-avatar-wrap.place-3::after {
  content: '';
  position: absolute;
  inset: -2px;
  border-radius: inherit;
  background: linear-gradient(135deg, rgba(205,160,80,0.15), transparent 50%);
  pointer-events: none;
  z-index: 1;
}

/* 4th-5th place — Silver: subtle metallic */
.pc-avatar-wrap.place-mid {
  border-color: rgba(160, 165, 180, 0.3);
  box-shadow: 0 0 6px rgba(160, 165, 180, 0.08);
}

/* 6th place — Iron: desaturated, dull */
.pc-avatar-wrap.place-last {
  border-color: rgba(120, 80, 80, 0.4);
  box-shadow: inset 0 0 16px rgba(100, 40, 40, 0.12);
  filter: saturate(0.65);
}

.pc-avatar-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  animation: avatar-breathe 4s ease-in-out infinite;
  transition: filter 0.5s ease;
}

/* Avatar reactivity by position */
.place-1 .pc-avatar-img,
.place-2 .pc-avatar-img {
  filter: contrast(1.05) brightness(1.05);
  animation-duration: 5s;
}
.place-last .pc-avatar-img {
  filter: saturate(0.7) brightness(0.9);
  animation-duration: 2.5s; 
}

@keyframes avatar-breathe {
  0%, 100% { transform: scale(1); }
  50% { transform: scale(1.015); }
}

.pc-avatar-fallback {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 48px;
  font-weight: 800;
  color: var(--text-dim);
}

/* Identity */
.pc-identity {
  text-align: center;
  max-width: 140px;
}

.pc-name {
  font-weight: 800;
  font-size: 13px;
  color: var(--accent-gold);
  letter-spacing: 0.3px;
  text-shadow: 0 0 10px rgba(240, 200, 80, 0.25);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-wrap: wrap;
  gap: 4px;
}

/* ── Character rarity badge ── */
.rarity-badge {
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.5px;
  text-transform: uppercase;
  padding: 1px 5px;
  border-radius: 3px;
  border: 1px solid;
  line-height: 1.4;
  text-shadow: none;
}
.rarity-legendary {
  color: #f0c850;
  border-color: rgba(240, 200, 80, 0.4);
  background: rgba(240, 200, 80, 0.1);
  box-shadow: 0 0 8px rgba(240, 200, 80, 0.15);
}
.rarity-epic {
  color: #c084fc;
  border-color: rgba(192, 132, 252, 0.4);
  background: rgba(192, 132, 252, 0.1);
  box-shadow: 0 0 8px rgba(192, 132, 252, 0.15);
}
.rarity-rare {
  color: #60a5fa;
  border-color: rgba(96, 165, 250, 0.4);
  background: rgba(96, 165, 250, 0.1);
}
.rarity-uncommon {
  color: #4ade80;
  border-color: rgba(74, 222, 128, 0.3);
  background: rgba(74, 222, 128, 0.08);
}
.rarity-common {
  color: var(--text-muted);
  border-color: rgba(255, 255, 255, 0.1);
  background: rgba(255, 255, 255, 0.03);
}

.pc-username {
  font-size: 10px;
  color: var(--text-muted);
  font-family: var(--font-mono);
}

/* Mastery badge */
.mastery-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 1px 8px;
  border-radius: 8px;
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}
.mastery-level {
  font-size: 11px;
  font-weight: 800;
}
.mastery-bronze {
  background: linear-gradient(135deg, rgba(184, 115, 51, 0.25), rgba(205, 127, 50, 0.15));
  color: #cd7f32;
  border: 1px solid rgba(205, 127, 50, 0.35);
  text-shadow: 0 0 4px rgba(205, 127, 50, 0.3);
}
.mastery-silver {
  background: linear-gradient(135deg, rgba(192, 192, 192, 0.25), rgba(169, 169, 169, 0.15));
  color: #c0c0c0;
  border: 1px solid rgba(192, 192, 192, 0.35);
  text-shadow: 0 0 4px rgba(192, 192, 192, 0.3);
}
.mastery-gold {
  background: linear-gradient(135deg, rgba(255, 215, 0, 0.25), rgba(255, 193, 37, 0.15));
  color: #ffd700;
  border: 1px solid rgba(255, 215, 0, 0.4);
  text-shadow: 0 0 6px rgba(255, 215, 0, 0.4);
  box-shadow: 0 0 8px rgba(255, 215, 0, 0.1);
}
.mastery-platinum {
  background: linear-gradient(135deg, rgba(180, 220, 255, 0.25), rgba(200, 230, 255, 0.15));
  color: #b4dcff;
  border: 1px solid rgba(180, 220, 255, 0.4);
  text-shadow: 0 0 8px rgba(180, 220, 255, 0.5);
  box-shadow: 0 0 12px rgba(180, 220, 255, 0.12);
}
.mastery-diamond {
  background: linear-gradient(135deg, rgba(185, 242, 255, 0.3), rgba(255, 255, 255, 0.15));
  color: #e0f7ff;
  border: 1px solid rgba(185, 242, 255, 0.5);
  text-shadow: 0 0 10px rgba(185, 242, 255, 0.6);
  box-shadow: 0 0 16px rgba(185, 242, 255, 0.15);
  animation: mastery-shimmer 2s ease-in-out infinite;
}
@keyframes mastery-shimmer {
  0%, 100% { opacity: 1; box-shadow: 0 0 16px rgba(185, 242, 255, 0.15); }
  50% { opacity: 0.85; box-shadow: 0 0 24px rgba(185, 242, 255, 0.3); }
}

/* Stats */
.pc-stats {
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.stat-row {
  display: flex;
  align-items: center;
  gap: 5px;
}

.stat-bar-bg {
  flex: 1;
  height: 10px;
  background: var(--bg-inset);
  border-radius: 5px;
  overflow: hidden;
  box-shadow: inset 0 1px 3px rgba(0, 0, 0, 0.3);
  position: relative;
}

.stat-bar {
  height: 100%;
  border-radius: 5px;
  transition: width 0.6s var(--ease-spring);
  position: relative;
}

/* Inner shine on stat bars */
.stat-bar::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 5px;
  background: linear-gradient(180deg, rgba(255,255,255,0.15) 0%, transparent 60%);
  pointer-events: none;
}

.stat-bar.intelligence { background: linear-gradient(90deg, var(--kh-c-secondary-info-400), var(--kh-c-secondary-info-200)); box-shadow: 0 0 6px rgba(110, 170, 240, 0.2); }
.stat-bar.strength { background: linear-gradient(90deg, var(--kh-c-secondary-danger-400), var(--kh-c-secondary-danger-200)); box-shadow: 0 0 6px rgba(239, 128, 128, 0.2); }
.stat-bar.speed { background: linear-gradient(90deg, var(--kh-c-secondary-warning-400), var(--kh-c-secondary-warning-200)); box-shadow: 0 0 6px rgba(233, 219, 61, 0.15); }
.stat-bar.psyche { background: linear-gradient(90deg, var(--kh-c-secondary-purple-400), var(--kh-c-secondary-purple-200)); box-shadow: 0 0 6px rgba(180, 150, 255, 0.2); }

.stat-val {
  width: 22px;
  text-align: right;
  font-family: var(--font-mono);
  font-weight: 800;
  font-size: 13px;
  color: var(--text-primary);
  transition: color 0.3s, text-shadow 0.3s;
}

/* 2A. Per-stat-color text glows */
.stat-val.stat-intelligence { color: var(--kh-c-secondary-info-200); text-shadow: 0 0 6px rgba(110, 170, 240, 0.35); }
.stat-val.stat-strength { color: var(--kh-c-secondary-danger-200); text-shadow: 0 0 6px rgba(239, 128, 128, 0.35); }
.stat-val.stat-speed { color: var(--kh-c-secondary-warning-200); text-shadow: 0 0 6px rgba(233, 219, 61, 0.3); }
.stat-val.stat-psyche { color: var(--kh-c-secondary-purple-200); text-shadow: 0 0 6px rgba(180, 150, 255, 0.35); }

.stat-block {
  display: flex;
  flex-direction: column;
  gap: 1px;
  border-radius: 4px;
  transition: background 0.3s, box-shadow 0.3s;
}

.stat-block.resist-hit {
  animation: resist-hit-flash 1.5s ease-out;
}

.stat-block.stat-pulse {
  animation: stat-change-pulse 1.5s ease-out;
}

@keyframes resist-hit-flash {
  0% { background: rgba(239, 128, 128, 0.3); box-shadow: inset 0 0 12px rgba(239, 128, 128, 0.4); }
  30% { background: rgba(239, 128, 128, 0.15); box-shadow: inset 0 0 6px rgba(239, 128, 128, 0.2); }
  100% { background: transparent; box-shadow: none; }
}

@keyframes stat-change-pulse {
  0% { background: rgba(110, 170, 240, 0.25); box-shadow: inset 0 0 10px rgba(110, 170, 240, 0.3); }
  40% { background: rgba(110, 170, 240, 0.1); box-shadow: inset 0 0 4px rgba(110, 170, 240, 0.15); }
  100% { background: transparent; box-shadow: none; }
}

.stat-block.lvl-up-available {
  animation: lvl-glow 2s ease-in-out infinite;
  border: 1px solid rgba(63, 167, 61, 0.3);
  border-radius: 6px;
}

@keyframes lvl-glow {
  0%, 100% { box-shadow: 0 0 4px rgba(63, 167, 61, 0.15); }
  50% { box-shadow: 0 0 12px rgba(63, 167, 61, 0.35); }
}

.resist-row {
  display: flex;
  align-items: center;
  gap: 4px;
  padding-left: 23px;
}

.resist-badge {
  font-size: 10px;
  font-weight: 600;
  color: var(--accent-blue);
}

.resist-bonus {
  font-size: 9px;
  color: var(--accent-gold-dim);
  font-weight: 700;
}

.lvl-up-badge {
  text-align: center;
  font-size: 10px;
  font-weight: 800;
  color: var(--accent-gold);
  padding: 3px 8px;
  background: rgba(233, 219, 61, 0.08);
  border: 1px solid rgba(233, 219, 61, 0.15);
  border-radius: var(--radius);
  margin-bottom: 2px;
  letter-spacing: 0.3px;
  animation: badge-pulse 1.5s ease-in-out infinite;
}

@keyframes badge-pulse {
  0%, 100% { transform: scale(1); }
  50% { transform: scale(1.05); }
}

.lvl-up-quip {
  display: block;
  font-size: 9px;
  font-weight: 500;
  font-style: italic;
  color: var(--text-muted);
  opacity: 0.7;
  margin-top: 1px;
  letter-spacing: 0;
  animation: quip-fade-in 0.5s ease-out;
}

@keyframes quip-fade-in {
  0% { opacity: 0; transform: translateY(-4px); }
  100% { opacity: 0.7; transform: translateY(0); }
}

.lvl-btn {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  border: 1.5px solid var(--accent-green);
  background: rgba(63, 167, 61, 0.1);
  color: var(--accent-green);
  font-size: 14px;
  font-weight: 900;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  line-height: 1;
  transition: all 0.2s var(--ease-spring);
  flex-shrink: 0;
  box-shadow: 0 0 6px rgba(63, 167, 61, 0.15);
  position: relative;
}

/* 2B. Pulsing ring around lvl-up button */
.lvl-btn::before {
  content: '';
  position: absolute;
  inset: -4px;
  border-radius: 50%;
  border: 1.5px solid rgba(63, 167, 61, 0.4);
  animation: lvl-pulse-ring 1.5s ease-in-out infinite;
  pointer-events: none;
}
@keyframes lvl-pulse-ring {
  0%, 100% { transform: scale(1); opacity: 0.6; }
  50% { transform: scale(1.15); opacity: 0; }
}

.lvl-btn:hover {
  background: var(--accent-green);
  color: var(--bg-primary);
  box-shadow: 0 0 14px rgba(63, 167, 61, 0.4);
  transform: scale(1.15);
}
.lvl-btn:hover::before {
  animation: none;
  opacity: 0;
}
.lvl-btn:active {
  box-shadow: 0 0 20px rgba(63, 167, 61, 0.6);
}

/* Irelia nerf variant — red instead of green */
.lvl-btn.nerf-btn {
  border-color: var(--accent-red);
  background: rgba(239, 80, 80, 0.1);
  color: var(--accent-red);
  box-shadow: 0 0 6px rgba(239, 80, 80, 0.15);
}
.lvl-btn.nerf-btn::before {
  border-color: rgba(239, 80, 80, 0.4);
  animation: nerf-pulse-ring 1.5s ease-in-out infinite;
}
@keyframes nerf-pulse-ring {
  0%, 100% { transform: scale(1); opacity: 0.6; }
  50% { transform: scale(1.15); opacity: 0; }
}
.lvl-btn.nerf-btn:hover {
  background: var(--accent-red);
  color: var(--bg-primary);
  box-shadow: 0 0 14px rgba(239, 80, 80, 0.4);
}
.lvl-btn.nerf-btn:active {
  box-shadow: 0 0 20px rgba(239, 80, 80, 0.6);
}

.nerf-badge {
  color: var(--accent-red) !important;
  border-color: rgba(239, 80, 80, 0.2) !important;
}

/* Justice highlight row */
.pc-justice-row {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 7px 10px;
  background: linear-gradient(135deg, rgba(139, 92, 246, 0.1), rgba(180, 150, 255, 0.04));
  border: 1.5px solid rgba(139, 92, 246, 0.25);
  border-radius: var(--radius-lg);
  position: relative;
  transition: border-color 0.3s, box-shadow 0.3s;
  backdrop-filter: blur(4px);
}
.pc-justice-row:hover {
  border-color: rgba(139, 92, 246, 0.45);
  box-shadow: 0 0 12px rgba(139, 92, 246, 0.2);
}
.justice-icon {
  font-size: 16px;
  opacity: 0.8;
}
.justice-label {
  font-size: 9px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: rgba(180, 150, 255, 0.8);
}
.justice-value {
  font-size: 22px;
  font-weight: 900;
  color: var(--accent-purple);
  font-family: var(--font-mono);
  text-shadow: 0 0 10px rgba(139, 92, 246, 0.25);
}

/* Meta row */
.pc-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.meta-box {
  flex: 1 1 auto;
  min-width: 44px;
  text-align: center;
  padding: 5px 4px;
  background: rgba(28, 26, 33, 0.6);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius);
  cursor: default;
  transition: border-color 0.2s, box-shadow 0.2s, transform 0.2s;
}

.meta-box:hover {
  border-color: var(--border-color);
  box-shadow: 0 0 6px rgba(255, 255, 255, 0.04);
  transform: translateY(-1px);
}

.meta-label {
  display: block;
  font-size: 8px;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-weight: 700;
}

.meta-value {
  font-size: 16px;
  font-weight: 800;
}

.stat-class { font-size: 12px; color: var(--accent-gold); }

/* Justice up sparkle animation */
.pc-justice-row.justice-up-sparkle {
  position: relative;
}
.pc-justice-row.justice-up-sparkle::after {
  content: '';
  position: absolute;
  inset: -4px 0;
  pointer-events: none;
  background-image:
    radial-gradient(circle 2px, #fbbf24 100%, transparent 100%),
    radial-gradient(circle 1.5px, #f59e0b 100%, transparent 100%),
    radial-gradient(circle 2px, #fcd34d 100%, transparent 100%),
    radial-gradient(circle 1.5px, #fbbf24 100%, transparent 100%),
    radial-gradient(circle 2px, #f59e0b 100%, transparent 100%),
    radial-gradient(circle 1.5px, #fcd34d 100%, transparent 100%),
    radial-gradient(circle 2px, #fbbf24 100%, transparent 100%),
    radial-gradient(circle 1.5px, #f59e0b 100%, transparent 100%);
  background-position:
    8% 80%, 20% 60%, 35% 90%, 48% 50%,
    62% 85%, 75% 55%, 85% 75%, 95% 65%;
  background-size: 4px 4px;
  background-repeat: no-repeat;
  animation: justice-sparkle-up 2s ease-out forwards;
}

@keyframes justice-sparkle-up {
  0%   { opacity: 0.9; transform: translateY(0); }
  50%  { opacity: 0.7; }
  100% { opacity: 0; transform: translateY(-30px); }
}

/* Justice reset animation */
.pc-justice-row.justice-reset-flash {
  animation: justice-reset 2s ease-out;
}

.justice-reset-label {
  position: absolute;
  top: -8px;
  right: 6px;
  font-size: 8px;
  font-weight: 900;
  color: #e879f9;
  background: rgba(232, 121, 249, 0.2);
  padding: 1px 6px;
  border-radius: 3px;
  letter-spacing: 0.5px;
  animation: justice-reset-label 2s ease-out forwards;
}

@keyframes justice-reset {
  0% { border-color: #e879f9; box-shadow: 0 0 16px rgba(232, 121, 249, 0.6); }
  30% { border-color: #e879f9; box-shadow: 0 0 10px rgba(232, 121, 249, 0.3); }
  100% { border-color: rgba(139, 92, 246, 0.25); box-shadow: none; }
}

@keyframes justice-reset-label {
  0% { opacity: 1; transform: translateY(0); }
  70% { opacity: 1; transform: translateY(0); }
  100% { opacity: 0; transform: translateY(-8px); }
}

/* Moral exchange */
.pc-moral-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  position: relative;
}

.moral-last-round {
  width: 100%;
  text-align: center;
  font-size: 10px;
  font-weight: 800;
  color: #ff4444;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  animation: lastChancePulse 1.2s ease-in-out infinite;
}
@keyframes lastChancePulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.moral-btn {
  flex: 1;
  padding: 5px 4px;
  border: 1px solid rgba(230, 148, 74, 0.3);
  border-radius: var(--radius);
  background: linear-gradient(180deg, rgba(230, 148, 74, 0.08), rgba(230, 148, 74, 0.03));
  color: var(--accent-orange);
  font-size: 10px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s var(--ease-in-out);
  text-align: center;
  min-width: 0;
  white-space: nowrap;
}
.moral-btn:hover:not(:disabled) {
  background: linear-gradient(180deg, var(--accent-orange), rgba(230, 148, 74, 0.85));
  color: var(--bg-primary);
  border-color: var(--accent-orange);
  transform: translateY(-1px);
  box-shadow: 0 3px 10px rgba(230, 148, 74, 0.25);
}
.moral-btn:active:not(:disabled) {
  transform: translateY(0) scale(0.97);
  box-shadow: 0 1px 4px rgba(230, 148, 74, 0.15);
}
.moral-btn-disabled {
  opacity: 0.45;
  cursor: not-allowed;
  border-color: rgba(100, 100, 100, 0.3);
  color: rgba(180, 180, 180, 0.7);
  background: rgba(60, 60, 60, 0.15);
}

.shinigami-btn {
  border-color: rgba(200, 50, 50, 0.4);
  color: #ef5050;
  background: rgba(200, 50, 50, 0.08);
}
.shinigami-btn:hover {
  background: rgba(200, 50, 50, 0.15);
  border-color: rgba(200, 50, 50, 0.6);
}

/* Score */
.pc-score-row {
  display: flex;
  align-items: baseline;
  justify-content: center;
  gap: 4px;
  padding-top: 4px;
  border-top: 1px solid var(--border-subtle);
  position: relative;
}

.pc-score {
  font-size: 28px;
  font-weight: 900;
  color: var(--accent-gold);
  font-family: var(--font-mono);
  filter: drop-shadow(0 0 12px rgba(240, 200, 80, 0.3));
  transition: filter 0.3s;
}

/* 2E. Brighter glow when score delta is visible */
.pc-score-row:has(.pc-score-delta) .pc-score {
  filter: drop-shadow(0 0 16px rgba(240, 200, 80, 0.5)) drop-shadow(0 0 30px rgba(240, 200, 80, 0.2));
}

.pc-score-label {
  font-size: 11px;
  color: var(--text-muted);
  font-weight: 600;
}

.pc-score-label-geralt {
  white-space: pre-line;
  font-size: 9px;
  line-height: 1.2;
}

.pc-round-multiplier {
  font-size: 11px;
  font-weight: 700;
  color: #d4a017;
  background: rgba(212, 160, 23, 0.15);
  padding: 1px 5px;
  border-radius: 4px;
  margin-left: 4px;
  font-family: var(--font-mono);
}

.pc-score-delta {
  font-size: 16px;
  font-weight: 900;
  color: var(--accent-green);
  font-family: var(--font-mono);
  animation: score-delta-pop 0.35s ease;
}
.pc-score-delta.delta-big {
  color: var(--accent-orange);
  font-size: 18px;
}
.pc-score-delta.delta-huge {
  color: var(--accent-red);
  font-size: 20px;
  text-shadow: 0 0 8px rgba(239, 128, 128, 0.5);
}
.pc-score-delta.delta-negative {
  color: var(--accent-red);
}
@keyframes score-delta-pop {
  0% { transform: scale(0.5) translateY(4px); opacity: 0; }
  40% { transform: scale(1.35) translateY(-2px); }
  70% { transform: scale(0.95); }
  100% { transform: scale(1) translateY(0); opacity: 1; }
}

.pc-place {
  font-size: 13px;
  font-weight: 700;
  color: var(--text-muted);
  margin-left: 6px;
  font-family: var(--font-mono);
}

/* ── Score combo feed (grouped sections) ── */
.pc-combo-feed {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 2px 0;
}

.combo-section {
  border-radius: 4px;
  padding: 3px 4px;
  opacity: 0;
  transform: translateY(6px);
  transition: opacity 0.3s, transform 0.3s;
}
.combo-section.combo-visible {
  opacity: 1;
  transform: translateY(0);
}

.combo-section-regular {
  background: rgba(212, 160, 23, 0.04);
  border: 1px solid rgba(212, 160, 23, 0.12);
}
.combo-section-bonus {
  background: rgba(96, 165, 250, 0.04);
  border: 1px solid rgba(96, 165, 250, 0.12);
}

.combo-section-header {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 1px 4px 2px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
  margin-bottom: 2px;
}
.combo-section-label {
  font-size: 9px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}
.combo-section-regular .combo-section-label {
  color: var(--accent-gold);
}
.combo-section-bonus .combo-section-label {
  color: #60a5fa;
}
.combo-mult-badge {
  font-size: 8px;
  font-weight: 800;
  padding: 1px 5px;
  border-radius: 3px;
  background: rgba(212, 160, 23, 0.18);
  color: var(--accent-gold);
  letter-spacing: 0.3px;
  text-transform: lowercase;
}

.combo-mult-modified {
  color: #f59e0b;
  border-color: rgba(245, 158, 11, 0.3);
  background: rgba(245, 158, 11, 0.08);
}

.combo-mult-expected {
  text-decoration: line-through;
  opacity: 0.6;
  margin-right: 2px;
}

.combo-entry {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 10px;
  font-weight: 700;
  padding: 1px 6px;
  border-radius: 3px;
  opacity: 0;
  transform: translateY(6px) scale(0.9);
  transition: opacity 0.3s, transform 0.3s;
}
.combo-entry.combo-visible {
  opacity: 1;
  transform: translateY(0) scale(1);
}
.combo-entry.combo-active {
  animation: combo-pop 0.4s ease;
}

.combo-type-regular {
  color: var(--accent-gold);
}
.combo-type-regular.combo-active {
  background: rgba(212, 160, 23, 0.1);
}
.combo-type-bonus {
  color: #60a5fa;
}
.combo-type-bonus.combo-active {
  background: rgba(96, 165, 250, 0.1);
}

.combo-entry.combo-negative {
  color: var(--accent-red);
}
.combo-entry.combo-negative.combo-active {
  background: rgba(239, 128, 128, 0.1);
}

.combo-hit-pts {
  font-weight: 900;
  font-family: var(--font-mono);
  font-size: 11px;
  min-width: 22px;
}
.combo-type-regular .combo-hit-pts { color: var(--accent-green); }
.combo-type-bonus .combo-hit-pts { color: #60a5fa; }
.combo-hit-negative { color: var(--accent-red) !important; }

.combo-hit-label {
  flex: 1;
  text-align: left;
  font-size: 10px;
  color: var(--text-secondary);
}

.combo-badge {
  font-size: 9px;
  font-weight: 900;
  padding: 1px 5px;
  border-radius: 3px;
  background: rgba(233, 219, 61, 0.15);
  border: 1px solid transparent;
  color: var(--accent-gold);
  letter-spacing: 0.5px;
  margin-left: auto;
  font-family: var(--font-mono);
}
.combo-type-bonus .combo-badge {
  background: rgba(96, 165, 250, 0.15);
  color: #60a5fa;
}

.combo-section-total {
  text-align: right;
  font-size: 10px;
  font-weight: 900;
  padding: 2px 6px 1px;
  border-top: 1px solid rgba(255, 255, 255, 0.06);
  margin-top: 2px;
  letter-spacing: 0.3px;
  animation: combo-total-in 0.5s ease;
}
.combo-total-regular {
  color: var(--accent-gold);
}
.combo-total-bonus {
  color: #60a5fa;
}

@keyframes combo-pop {
  0% { transform: translateY(6px) scale(0.8); opacity: 0; }
  50% { transform: translateY(-2px) scale(1.05); }
  100% { transform: translateY(0) scale(1); opacity: 1; }
}
@keyframes combo-total-in {
  0% { opacity: 0; transform: scale(0.5); }
  60% { transform: scale(1.15); }
  100% { opacity: 1; transform: scale(1); }
}

/* ── Fight Bonuses Section ── */
.pc-fight-bonuses {
  margin-top: 6px;
  padding: 6px 8px;
  border-radius: var(--radius);
  background: rgba(0, 0, 0, 0.15);
  border: 1px solid var(--border-subtle);
}

.fight-bonus-header {
  font-size: 9px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: var(--text-dim);
  margin-bottom: 4px;
}

.fight-bonus-entry {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 2px 0;
  font-size: 12px;
  font-weight: 700;
}

.fight-bonus-value {
  min-width: 28px;
  text-align: right;
  font-family: var(--font-mono);
}

.fight-bonus-label {
  color: var(--text-secondary);
  font-weight: 500;
}

/* Color coding */
.bonus-skill { color: var(--accent-green); }
.bonus-justice { color: var(--accent-blue); }
.bonus-moral-up { color: var(--accent-purple); }
.bonus-moral-down { color: var(--accent-red); }

/* PSY separated box */
.pc-psyche-box {
  border: 1px solid rgba(232, 121, 249, 0.15);
  border-radius: var(--radius-lg);
  padding: 5px 8px;
  background: rgba(232, 121, 249, 0.04);
  transition: border-color 0.3s, box-shadow 0.3s;
}
.pc-psyche-box:hover {
  border-color: rgba(232, 121, 249, 0.25);
  box-shadow: 0 0 8px rgba(232, 121, 249, 0.08);
}

/* Portal Gun special ability box */
.pc-special-ability {
  padding: 6px 8px;
  background: linear-gradient(135deg, rgba(0, 200, 100, 0.06), rgba(0, 200, 100, 0.02));
  border: 1px solid rgba(0, 200, 100, 0.2);
  border-radius: var(--radius);
}
.sa-header {
  font-size: 10px; font-weight: 800; color: var(--accent-green);
  text-transform: uppercase; letter-spacing: 0.3px;
}
.sa-status { margin-top: 2px; font-size: 11px; }
.sa-not-invented { color: var(--text-muted); }
.sa-invented { display: flex; align-items: baseline; gap: 4px; }
.sa-charge-count {
  font-size: 18px; font-weight: 900; font-family: var(--font-mono);
  color: var(--accent-green); text-shadow: 0 0 8px rgba(0, 200, 100, 0.3);
}
.sa-charge-label { font-size: 10px; color: var(--text-muted); }
/* Charged: pulse a green glow so it's obvious the gun is loaded */
.sa-charged {
  border-color: rgba(0, 200, 100, 0.7);
  animation: portal-gun-glow 1.4s ease-in-out infinite;
}
@keyframes portal-gun-glow {
  0%, 100% {
    box-shadow: 0 0 4px rgba(0, 200, 100, 0.25);
    border-color: rgba(0, 200, 100, 0.35);
  }
  50% {
    box-shadow: 0 0 16px rgba(0, 200, 100, 0.75);
    border-color: rgba(0, 200, 100, 0.9);
  }
}

/* ── Matrix theme for Баг ── */
.player-card.is-bug {
  border-color: rgba(0, 255, 65, 0.25);
  box-shadow: 0 0 12px rgba(0, 255, 65, 0.08);
  background:
    repeating-linear-gradient(
      0deg,
      transparent,
      transparent 2px,
      rgba(0, 255, 65, 0.015) 2px,
      rgba(0, 255, 65, 0.015) 4px
    ),
    var(--bg-card);
}
.player-card.is-bug.is-me {
  border-color: rgba(0, 255, 65, 0.35);
  box-shadow: 0 0 16px rgba(0, 255, 65, 0.12), 0 0 40px rgba(0, 255, 65, 0.04);
}
.player-card.is-bug .pc-name {
  color: #00ff41;
  text-shadow: 0 0 6px rgba(0, 255, 65, 0.4);
  font-family: var(--font-mono);
}
.player-card.is-bug .pc-username {
  color: rgba(0, 255, 65, 0.5);
}
.player-card.is-bug .pc-score {
  color: #00ff41;
  text-shadow: 0 0 8px rgba(0, 255, 65, 0.3);
}
.player-card.is-bug .pc-score-row {
  border-top-color: rgba(0, 255, 65, 0.15);
}
.player-card.is-bug .justice-value {
  color: #00ff41;
  text-shadow: 0 0 10px rgba(0, 255, 65, 0.3);
}
.player-card.is-bug .pc-justice-row {
  background: linear-gradient(135deg, rgba(0, 255, 65, 0.06), rgba(0, 255, 65, 0.02));
  border-color: rgba(0, 255, 65, 0.2);
}
.player-card.is-bug .justice-label {
  color: rgba(0, 255, 65, 0.6);
}

/* Exploit state box */
.pc-exploit-state {
  padding: 6px 8px;
  background: linear-gradient(135deg, rgba(0, 255, 65, 0.08), rgba(0, 255, 65, 0.02));
  border: 1px solid rgba(0, 255, 65, 0.25);
  border-radius: var(--radius);
}
.exploit-header {
  display: flex; justify-content: space-between; align-items: center;
}
.exploit-title {
  font-size: 10px; font-weight: 900; color: #00ff41;
  text-transform: uppercase; letter-spacing: 1px;
  font-family: var(--font-mono);
  text-shadow: 0 0 4px rgba(0, 255, 65, 0.4);
}
.exploit-progress {
  font-size: 10px; font-weight: 700; color: rgba(0, 255, 65, 0.6);
  font-family: var(--font-mono);
}
.exploit-accumulated {
  display: flex; align-items: baseline; gap: 4px;
  margin-top: 2px;
}
.exploit-value {
  font-size: 20px; font-weight: 900; font-family: var(--font-mono);
  color: #00ff41; text-shadow: 0 0 10px rgba(0, 255, 65, 0.4);
}
.exploit-label {
  font-size: 10px; color: rgba(0, 255, 65, 0.5);
  font-family: var(--font-mono);
}
.exploit-bar-bg {
  height: 3px; margin-top: 4px;
  background: rgba(0, 255, 65, 0.1);
  border-radius: 2px; overflow: hidden;
}
.exploit-bar-fill {
  height: 100%;
  background: linear-gradient(90deg, #00ff41, #00cc33);
  border-radius: 2px;
  transition: width 0.5s ease;
  box-shadow: 0 0 4px rgba(0, 255, 65, 0.3);
}

/* Tsukuyomi state box */
.pc-tsukuyomi-state {
  padding: 6px 8px;
  background: linear-gradient(135deg, rgba(220, 20, 60, 0.08), rgba(139, 0, 0, 0.02));
  border: 1px solid rgba(220, 20, 60, 0.25);
  border-radius: var(--radius);
}
.tsukuyomi-header {
  display: flex; justify-content: space-between; align-items: center;
}
.tsukuyomi-title {
  font-size: 10px; font-weight: 900; color: #dc143c;
  text-transform: uppercase; letter-spacing: 1px;
  font-family: var(--font-mono);
  text-shadow: 0 0 4px rgba(220, 20, 60, 0.4);
}
.tsukuyomi-charge {
  font-size: 10px; font-weight: 700; color: rgba(220, 20, 60, 0.6);
  font-family: var(--font-mono);
}
.tsukuyomi-charge.tsukuyomi-ready {
  color: #dc143c;
  text-shadow: 0 0 6px rgba(220, 20, 60, 0.5);
  animation: tsukuyomi-pulse 1.5s ease-in-out infinite;
}
@keyframes tsukuyomi-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}
.tsukuyomi-stolen {
  display: flex; align-items: baseline; gap: 4px;
  margin-top: 2px;
}
.tsukuyomi-value {
  font-size: 20px; font-weight: 900; font-family: var(--font-mono);
  color: #dc143c; text-shadow: 0 0 10px rgba(220, 20, 60, 0.4);
}
.tsukuyomi-label {
  font-size: 10px; color: rgba(220, 20, 60, 0.5);
  font-family: var(--font-mono);
}
.tsukuyomi-bar-bg {
  height: 3px; margin-top: 4px;
  background: rgba(220, 20, 60, 0.1);
  border-radius: 2px; overflow: hidden;
}
.tsukuyomi-bar-fill {
  height: 100%;
  background: linear-gradient(90deg, #dc143c, #8b0000);
  border-radius: 2px;
  transition: width 0.5s ease;
  box-shadow: 0 0 4px rgba(220, 20, 60, 0.3);
}

/* ── Passive Ability Widgets (shared base) ── */
.pc-passive-widget {
  padding: 6px 8px;
  border-radius: var(--radius);
  border: 1px solid;
}
.pw-header {
  display: flex; justify-content: space-between; align-items: center;
}
.pw-title {
  font-size: 10px; font-weight: 900;
  text-transform: uppercase; letter-spacing: 1px;
  font-family: var(--font-mono);
}
.pw-status {
  font-size: 10px; font-weight: 700;
  font-family: var(--font-mono);
}
.pw-body {
  display: flex; align-items: baseline; gap: 6px;
  margin-top: 2px;
}
.pw-stat-pair {
  display: flex; align-items: baseline; gap: 3px;
}
.pw-value {
  font-size: 18px; font-weight: 900; font-family: var(--font-mono);
}
.pw-label {
  font-size: 10px; font-family: var(--font-mono);
}

/* 1. Буль (Drowning) */
.bulk-widget {
  background: linear-gradient(135deg, rgba(30, 144, 255, 0.08), rgba(30, 144, 255, 0.02));
  border-color: rgba(30, 144, 255, 0.25);
}
.bulk-title { color: #1e90ff; text-shadow: 0 0 4px rgba(30, 144, 255, 0.4); }
.bulk-chance-wrap { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px; flex: 1; }
.bulk-chance-value { font-size: 18px; font-weight: 900; font-family: var(--font-mono); color: #1e90ff; text-shadow: 0 0 8px rgba(30, 144, 255, 0.4); }
.bulk-chance-label { font-size: 10px; color: rgba(30, 144, 255, 0.5); font-family: var(--font-mono); }
.bulk-wave-bar { width: 100%; height: 3px; background: rgba(30, 144, 255, 0.1); border-radius: 2px; overflow: hidden; }
.bulk-wave-fill { height: 100%; background: linear-gradient(90deg, #1e90ff, #00bfff); border-radius: 2px; transition: width 0.5s ease; animation: wave 2s ease-in-out infinite; }
.bulk-buffed { font-size: 9px; font-weight: 900; color: #1e90ff; background: rgba(30, 144, 255, 0.15); padding: 1px 6px; border-radius: 3px; animation: wave 1.5s ease-in-out infinite; }
@keyframes wave {
  0%, 100% { opacity: 0.7; }
  50% { opacity: 1; }
}

/* 2. Я за чаем (Tea Time) */
.tea-widget {
  background: linear-gradient(135deg, rgba(212, 165, 116, 0.08), rgba(212, 165, 116, 0.02));
  border-color: rgba(212, 165, 116, 0.25);
}
.tea-title { color: #d4a574; text-shadow: 0 0 4px rgba(212, 165, 116, 0.4); }
.tea-widget .pw-value { color: #d4a574; text-shadow: 0 0 8px rgba(212, 165, 116, 0.4); }
.tea-widget .pw-label { color: rgba(212, 165, 116, 0.5); }
.tea-ready { color: #d4a574; text-shadow: 0 0 6px rgba(212, 165, 116, 0.5); animation: tsukuyomi-pulse 1.5s ease-in-out infinite; }
.tea-brewing { color: rgba(212, 165, 116, 0.5); }

/* 3. Еврей (Profit) */
.jew-widget {
  background: linear-gradient(135deg, rgba(255, 215, 0, 0.08), rgba(255, 215, 0, 0.02));
  border-color: rgba(255, 215, 0, 0.25);
}
.jew-title { color: #ffd700; text-shadow: 0 0 4px rgba(255, 215, 0, 0.4); }
.jew-widget .pw-value { color: #ffd700; text-shadow: 0 0 8px rgba(255, 215, 0, 0.4); }
.jew-widget .pw-label { color: rgba(255, 215, 0, 0.5); }

/* 4. HardKitty (Friends) */
.hardkitty-widget {
  background: linear-gradient(135deg, rgba(255, 105, 180, 0.08), rgba(255, 105, 180, 0.02));
  border-color: rgba(255, 105, 180, 0.25);
}
.hardkitty-title { color: #ff69b4; text-shadow: 0 0 4px rgba(255, 105, 180, 0.4); }
.hardkitty-widget .pw-value { color: #ff69b4; text-shadow: 0 0 8px rgba(255, 105, 180, 0.4); }
.hardkitty-widget .pw-label { color: rgba(255, 105, 180, 0.5); }

/* 5. Обучение (Training) */
.training-widget {
  background: linear-gradient(135deg, rgba(106, 90, 205, 0.08), rgba(106, 90, 205, 0.02));
  border-color: rgba(106, 90, 205, 0.25);
}
.training-title { color: #6a5acd; text-shadow: 0 0 4px rgba(106, 90, 205, 0.4); }
.training-stat { color: #6a5acd; font-weight: 900; }
.training-widget .pw-value { color: #6a5acd; text-shadow: 0 0 8px rgba(106, 90, 205, 0.4); }
.training-widget .pw-label { color: rgba(106, 90, 205, 0.5); }

/* 6. Дракон (Dragon) */
.dragon-widget {
  background: linear-gradient(135deg, rgba(255, 69, 0, 0.08), rgba(255, 69, 0, 0.02));
  border-color: rgba(255, 69, 0, 0.25);
}
.dragon-title { color: #ff4500; text-shadow: 0 0 4px rgba(255, 69, 0, 0.4); }
.dragon-sleeping { color: rgba(255, 69, 0, 0.5); }
.dragon-awakened { color: #ff4500; font-weight: 900; text-shadow: 0 0 8px rgba(255, 69, 0, 0.6); animation: tsukuyomi-pulse 1s ease-in-out infinite; }
.dragon-bar-bg {
  height: 3px; margin-top: 4px;
  background: rgba(255, 69, 0, 0.1); border-radius: 2px; overflow: hidden;
}
.dragon-bar-fill {
  height: 100%;
  background: linear-gradient(90deg, #ff4500, #ff8c00);
  border-radius: 2px; transition: width 0.5s ease;
  box-shadow: 0 0 4px rgba(255, 69, 0, 0.3);
}

/* Dragon fire effect on card border */
.player-card.is-dragon {
  border-color: rgba(255, 69, 0, 0.3);
  box-shadow: 0 0 8px rgba(255, 69, 0, 0.1);
}
.player-card.is-dragon.is-awakened {
  animation: dragon-fire 2s ease-in-out infinite;
}
@keyframes dragon-fire {
  0%, 100% { box-shadow: 0 0 8px rgba(255, 69, 0, 0.2), 0 0 20px rgba(255, 140, 0, 0.1); border-color: rgba(255, 69, 0, 0.4); }
  50% { box-shadow: 0 0 16px rgba(255, 69, 0, 0.4), 0 0 40px rgba(255, 140, 0, 0.15); border-color: rgba(255, 69, 0, 0.6); }
}

/* 7. Запах мусора (Garbage) */
.garbage-widget {
  background: linear-gradient(135deg, rgba(139, 115, 85, 0.08), rgba(139, 115, 85, 0.02));
  border-color: rgba(139, 115, 85, 0.25);
}
.garbage-title { color: #8b7355; text-shadow: 0 0 4px rgba(139, 115, 85, 0.4); }
.garbage-count { color: rgba(139, 115, 85, 0.7); }
.garbage-bar-bg {
  height: 3px; margin-top: 4px;
  background: rgba(139, 115, 85, 0.1); border-radius: 2px; overflow: hidden;
}
.garbage-bar-fill {
  height: 100%;
  background: linear-gradient(90deg, #8b7355, #a0875e);
  border-radius: 2px; transition: width 0.5s ease;
}

/* 8. Научите играть (Copycat) */
.copycat-widget {
  background: linear-gradient(135deg, rgba(32, 178, 170, 0.08), rgba(32, 178, 170, 0.02));
  border-color: rgba(32, 178, 170, 0.25);
}
.copycat-title { color: #20b2aa; text-shadow: 0 0 4px rgba(32, 178, 170, 0.4); }
.copycat-stat { color: #20b2aa; font-weight: 900; }
.copycat-widget .pw-value { color: #20b2aa; text-shadow: 0 0 8px rgba(32, 178, 170, 0.4); }
.copycat-widget .pw-label { color: rgba(32, 178, 170, 0.5); }

/* 9. Чернильная завеса (Ink Screen) */
.ink-widget {
  background: linear-gradient(135deg, rgba(75, 0, 130, 0.08), rgba(75, 0, 130, 0.02));
  border-color: rgba(75, 0, 130, 0.25);
}
.ink-title { color: #7b68ee; text-shadow: 0 0 4px rgba(75, 0, 130, 0.4); }
.ink-widget .pw-value { color: #7b68ee; text-shadow: 0 0 8px rgba(75, 0, 130, 0.4); }
.ink-widget .pw-label { color: rgba(123, 104, 238, 0.5); }

/* 10. Тигр топ (Tiger Top) */
.tigertop-widget {
  background: linear-gradient(135deg, rgba(255, 140, 0, 0.08), rgba(255, 140, 0, 0.02));
  border-color: rgba(255, 140, 0, 0.25);
}
.tigertop-title { color: #ff8c00; text-shadow: 0 0 4px rgba(255, 140, 0, 0.4); }
.tigertop-widget .pw-value { color: #ff8c00; text-shadow: 0 0 8px rgba(255, 140, 0, 0.4); }
.tigertop-widget .pw-label { color: rgba(255, 140, 0, 0.5); }
.tigertop-on { color: #ff8c00; text-shadow: 0 0 6px rgba(255, 140, 0, 0.5); }
.tigertop-off { color: rgba(255, 140, 0, 0.4); }
.tigertop-widget.tigertop-active {
  animation: tigertop-pulse 2s ease-in-out infinite;
}
@keyframes tigertop-pulse {
  0%, 100% { box-shadow: 0 0 4px rgba(255, 140, 0, 0.1); }
  50% { box-shadow: 0 0 12px rgba(255, 140, 0, 0.3); }
}

/* 11. Челюсти (Jaws) */
.jaws-widget {
  background: linear-gradient(135deg, rgba(70, 130, 180, 0.08), rgba(70, 130, 180, 0.02));
  border-color: rgba(70, 130, 180, 0.25);
}
.jaws-title { color: #4682b4; text-shadow: 0 0 4px rgba(70, 130, 180, 0.4); }
.jaws-shark {
  width: 28px; height: 14px; color: #4682b4;
  animation: jaws-swim 3s ease-in-out infinite;
}
@keyframes jaws-swim {
  0%, 100% { transform: translateX(0); }
  50% { transform: translateX(4px); }
}
.jaws-body { gap: 8px; }
.jaws-widget .pw-value { color: #4682b4; text-shadow: 0 0 8px rgba(70, 130, 180, 0.4); font-size: 16px; }
.jaws-widget .pw-label { color: rgba(70, 130, 180, 0.5); }

/* 12. Привилегия (Privilege) */
.privilege-widget {
  background: linear-gradient(135deg, rgba(205, 127, 50, 0.08), rgba(205, 127, 50, 0.02));
  border-color: rgba(205, 127, 50, 0.25);
}
.privilege-title { color: #cd7f32; text-shadow: 0 0 4px rgba(205, 127, 50, 0.4); }
.privilege-widget .pw-value { color: #cd7f32; text-shadow: 0 0 8px rgba(205, 127, 50, 0.4); }
.privilege-widget .pw-label { color: rgba(205, 127, 50, 0.5); }
.privilege-widget.privilege-active {
  animation: privilege-pulse 2s ease-in-out infinite;
}
@keyframes privilege-pulse {
  0%, 100% { border-color: rgba(205, 127, 50, 0.25); box-shadow: 0 0 4px rgba(205, 127, 50, 0.1); }
  50% { border-color: rgba(205, 127, 50, 0.5); box-shadow: 0 0 12px rgba(205, 127, 50, 0.3); }
}

/* 13. Вампуризм (Vampirism) */
.vampirism-widget {
  background: linear-gradient(135deg, rgba(139, 0, 0, 0.08), rgba(139, 0, 0, 0.02));
  border-color: rgba(139, 0, 0, 0.25);
}
.vampirism-title { color: #b22222; text-shadow: 0 0 4px rgba(139, 0, 0, 0.4); }
.vampirism-widget .pw-value { color: #b22222; text-shadow: 0 0 8px rgba(139, 0, 0, 0.4); }
.vampirism-widget .pw-label { color: rgba(178, 34, 34, 0.5); }

/* 14. Weedwick (Weed) */
.weed-widget {
  background: linear-gradient(135deg, rgba(50, 205, 50, 0.08), rgba(50, 205, 50, 0.02));
  border-color: rgba(50, 205, 50, 0.25);
}
.weed-title { color: #32cd32; text-shadow: 0 0 4px rgba(50, 205, 50, 0.4); }
.weed-widget .pw-value { color: #32cd32; text-shadow: 0 0 8px rgba(50, 205, 50, 0.4); }
.weed-widget .pw-label { color: rgba(50, 205, 50, 0.5); }

/* 15. Сайтама (One Punch) */
.saitama-widget {
  background: linear-gradient(135deg, rgba(255, 193, 7, 0.08), rgba(255, 193, 7, 0.02));
  border-color: rgba(255, 193, 7, 0.25);
}
.saitama-title { color: #ffc107; text-shadow: 0 0 4px rgba(255, 193, 7, 0.4); }
.saitama-widget .pw-value { color: #ffc107; text-shadow: 0 0 8px rgba(255, 193, 7, 0.4); }
.saitama-widget .pw-label { color: rgba(255, 193, 7, 0.5); }

/* 16. Глаза бога смерти (Shinigami Eyes) */
.shinigami-widget {
  background: linear-gradient(135deg, rgba(255, 0, 0, 0.08), rgba(255, 0, 0, 0.02));
  border-color: rgba(255, 0, 0, 0.25);
}
.shinigami-title { color: #ff0000; text-shadow: 0 0 4px rgba(255, 0, 0, 0.4); }
.shinigami-on { color: #ff0000; text-shadow: 0 0 6px rgba(255, 0, 0, 0.5); animation: tsukuyomi-pulse 1.5s ease-in-out infinite; }
.shinigami-off { color: rgba(255, 0, 0, 0.4); }

/* 17. Продавец (Seller) */
.seller-widget {
  background: linear-gradient(135deg, rgba(218, 165, 32, 0.08), rgba(218, 165, 32, 0.02));
  border-color: rgba(218, 165, 32, 0.25);
}
.seller-title { color: #daa520; text-shadow: 0 0 4px rgba(218, 165, 32, 0.4); }
.seller-widget .pw-value { color: #daa520; text-shadow: 0 0 8px rgba(218, 165, 32, 0.4); }
.seller-widget .pw-label { color: rgba(218, 165, 32, 0.5); }

/* 19. Dopa */
.dopa-widget {
  background: linear-gradient(135deg, rgba(74, 144, 217, 0.08), rgba(74, 144, 217, 0.02));
  border-color: rgba(74, 144, 217, 0.25);
}
.dopa-title { color: #4a90d9; text-shadow: 0 0 4px rgba(74, 144, 217, 0.4); }
.dopa-widget .pw-value { color: #4a90d9; text-shadow: 0 0 8px rgba(74, 144, 217, 0.4); }
.dopa-widget .pw-label { color: rgba(74, 144, 217, 0.5); }
.dopa-tactic { color: #4a90d9; font-size: 9px; font-weight: 700; opacity: 0.8; }
.dopa-ready { color: #6ecc6e !important; text-shadow: 0 0 6px rgba(110, 204, 110, 0.5) !important; }
.dopa-need-atk { color: #ffb428 !important; animation: dopa-atk-pulse 1s ease-in-out infinite; }
@keyframes dopa-atk-pulse {
  0%, 100% { opacity: 0.6; }
  50% { opacity: 1; }
}

/* 20. Goblin Swarm */
.goblin-widget {
  background: linear-gradient(135deg, rgba(76, 153, 0, 0.08), rgba(76, 153, 0, 0.02));
  border-color: rgba(76, 153, 0, 0.25);
}
.goblin-title { color: #4c9900; text-shadow: 0 0 4px rgba(76, 153, 0, 0.4); }
.goblin-zig-active { color: #daa520; font-size: 9px; font-weight: 700; text-shadow: 0 0 4px rgba(218, 165, 32, 0.5); }

/* Goblin population bar */
.goblin-pop-bar { display: flex; align-items: center; gap: 6px; padding: 2px 0; }
.goblin-pop-total { font-size: 18px; font-weight: 800; color: #4c9900; text-shadow: 0 0 8px rgba(76, 153, 0, 0.4); min-width: 32px; }
.goblin-pop-track { flex: 1; height: 6px; border-radius: 3px; background: rgba(255, 255, 255, 0.08); display: flex; overflow: hidden; }
.goblin-seg { height: 100%; transition: width 0.4s ease; }
.goblin-seg-warrior { background: #c0392b; }
.goblin-seg-hob { background: #8e44ad; }
.goblin-seg-worker { background: #d4a017; }

/* Goblin type breakdown */
.goblin-types { display: flex; gap: 8px; justify-content: space-around; padding: 2px 0; }
.goblin-type { display: flex; align-items: center; gap: 3px; }
.goblin-type-icon { font-size: 11px; }
.goblin-type-val { font-size: 12px; font-weight: 700; color: #4c9900; }
.goblin-type-rate { font-size: 9px; color: rgba(255, 255, 255, 0.4); }

/* Goblin footer (ziggurat badges + festival) */
.goblin-footer { display: flex; gap: 6px; align-items: center; flex-wrap: wrap; padding-top: 2px; }
.goblin-zig-badge { font-size: 10px; font-weight: 700; color: #daa520; background: rgba(218, 165, 32, 0.12); border: 1px solid rgba(218, 165, 32, 0.3); border-radius: 4px; padding: 1px 4px; }
.goblin-festival-used { font-size: 9px; color: rgba(255, 255, 255, 0.4); font-style: italic; }

.special-levelup-notes {
  display: grid;
  gap: 4px;
  margin: 5px 0 9px;
  padding: 9px 10px;
  border: 1px solid rgba(99, 184, 255, .28);
  border-left: 3px solid #63b8ff;
  border-radius: 8px;
  background: linear-gradient(110deg, rgba(45, 119, 181, .13), rgba(17, 22, 29, .25));
  color: rgba(226, 241, 255, .72);
  font-size: 9px;
  line-height: 1.4;
}
.special-levelup-notes strong {
  color: #83c7ff;
  font-size: 9px;
  letter-spacing: .05em;
  text-transform: uppercase;
}
.special-levelup-notes span::before { content: '◆'; margin-right: 6px; color: #63b8ff; font-size: 7px; }

/* 21. Котики */
.kotiki-widget {
  background: linear-gradient(135deg, rgba(255, 165, 0, 0.08), rgba(255, 165, 0, 0.02));
  border-color: rgba(255, 165, 0, 0.25);
}
.kotiki-title { color: #ffa500; text-shadow: 0 0 4px rgba(255, 165, 0, 0.4); }
.kotiki-info { display: flex; flex-direction: column; gap: 4px; padding: 2px 0; }
.kotiki-row { display: flex; align-items: center; gap: 6px; }
.kotiki-label { font-size: 10px; color: rgba(255, 255, 255, 0.5); min-width: 80px; }
.kotiki-val { font-size: 11px; font-weight: 700; color: #ffa500; }
.kotiki-cooldown { opacity: 0.6; }

/* Cat deployment cards (shared between owner & target views) */
.kotiki-cat-card {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 4px 8px;
  border-radius: 6px;
  border: 1px solid rgba(255, 165, 0, 0.3);
}
.kotiki-cat-minka {
  background: linear-gradient(135deg, rgba(100, 200, 255, 0.12), rgba(100, 200, 255, 0.04));
  border-color: rgba(100, 200, 255, 0.35);
}
.kotiki-cat-storm {
  background: linear-gradient(135deg, rgba(255, 80, 80, 0.12), rgba(255, 80, 80, 0.04));
  border-color: rgba(255, 80, 80, 0.35);
}
.kotiki-cat-header { display: flex; align-items: center; gap: 4px; }
.kotiki-cat-icon { font-size: 14px; }
.kotiki-cat-name { font-size: 11px; font-weight: 700; color: #ffa500; }
.kotiki-cat-rounds {
  font-size: 9px;
  font-weight: 700;
  color: rgba(255, 255, 255, 0.7);
  background: rgba(255, 255, 255, 0.1);
  border-radius: 3px;
  padding: 1px 4px;
  margin-left: auto;
}
.kotiki-cat-target { font-size: 10px; color: rgba(255, 255, 255, 0.6); }

/* 22. Монстр без имени */
.monster-widget {
  background: linear-gradient(135deg, rgba(100, 0, 0, 0.12), rgba(100, 0, 0, 0.04));
  border-color: rgba(180, 0, 0, 0.3);
}
.monster-title { color: #cc3333; text-shadow: 0 0 4px rgba(180, 0, 0, 0.5); }
.monster-info { display: flex; flex-direction: column; gap: 4px; padding: 2px 0; }
.monster-row { display: flex; align-items: center; gap: 6px; }
.monster-label { font-size: 10px; color: rgba(255, 255, 255, 0.5); min-width: 50px; }
.monster-val { font-size: 11px; font-weight: 700; color: #cc3333; }

/* 23. Подсчет (Tolya Count) */
.tolya-widget {
  background: linear-gradient(135deg, rgba(138, 92, 200, 0.08), rgba(138, 92, 200, 0.02));
  border-color: rgba(138, 92, 200, 0.25);
}
.tolya-title { color: #a06cd5; text-shadow: 0 0 4px rgba(138, 92, 200, 0.4); }
.tolya-ready { color: #a06cd5; text-shadow: 0 0 6px rgba(138, 92, 200, 0.5); animation: tsukuyomi-pulse 1.5s ease-in-out infinite; }
.tolya-cooldown { color: rgba(138, 92, 200, 0.5); }

/* 24. Импакт (LeCrisp) */
.impact-widget {
  background: linear-gradient(135deg, rgba(76, 153, 76, 0.08), rgba(76, 153, 76, 0.02));
  border-color: rgba(76, 153, 76, 0.25);
}
.impact-title { color: #4c994c; text-shadow: 0 0 4px rgba(76, 153, 76, 0.4); }
.impact-streak { color: #4c994c; font-weight: 900; text-shadow: 0 0 6px rgba(76, 153, 76, 0.5); }

/* 25. Darksci (Luck) */
.darksci-widget {
  background: linear-gradient(135deg, rgba(0, 200, 150, 0.08), rgba(0, 200, 150, 0.02));
  border-color: rgba(0, 200, 150, 0.25);
}
.darksci-title { color: #00c896; text-shadow: 0 0 4px rgba(0, 200, 150, 0.4); }
.darksci-left { color: #00c896; font-weight: 900; }

/* 26. DeepList */
.deeplist-widget {
  background: linear-gradient(135deg, rgba(100, 180, 255, 0.08), rgba(100, 180, 255, 0.02));
  border-color: rgba(100, 180, 255, 0.25);
}
.deeplist-title { color: #64b4ff; text-shadow: 0 0 4px rgba(100, 180, 255, 0.4); }
.deeplist-widget .pw-value { color: #64b4ff; text-shadow: 0 0 8px rgba(100, 180, 255, 0.4); }
.deeplist-widget .pw-label { color: rgba(100, 180, 255, 0.5); }

/* 27. Краборак (Shell) */
.craborack-widget {
  background: linear-gradient(135deg, rgba(210, 105, 30, 0.08), rgba(210, 105, 30, 0.02));
  border-color: rgba(210, 105, 30, 0.25);
}
.craborack-title { color: #d2691e; text-shadow: 0 0 4px rgba(210, 105, 30, 0.4); }
.craborack-count { color: #d2691e; font-weight: 900; }

/* 28. Napoleon (Alliance) */
.napoleon-widget {
  background: linear-gradient(135deg, rgba(200, 170, 50, 0.08), rgba(200, 170, 50, 0.02));
  border-color: rgba(200, 170, 50, 0.25);
}
.napoleon-title { color: #c8aa32; text-shadow: 0 0 4px rgba(200, 170, 50, 0.4); }
.napoleon-widget .pw-value { color: #c8aa32; text-shadow: 0 0 8px rgba(200, 170, 50, 0.4); }
.napoleon-widget .pw-label { color: rgba(200, 170, 50, 0.5); }

/* 29. Суппорт (Carry) */
.support-widget {
  background: linear-gradient(135deg, rgba(110, 170, 240, 0.08), rgba(110, 170, 240, 0.02));
  border-color: rgba(110, 170, 240, 0.25);
}
.support-title { color: #6eaaf0; text-shadow: 0 0 4px rgba(110, 170, 240, 0.4); }
.support-carry { color: #6eaaf0; }

/* 30. Toxic Mate (Cancer) */
.toxic-widget {
  background: linear-gradient(135deg, rgba(0, 255, 0, 0.06), rgba(0, 255, 0, 0.01));
  border-color: rgba(0, 255, 0, 0.2);
}
.toxic-title { color: #00dd00; text-shadow: 0 0 4px rgba(0, 255, 0, 0.4); }
.toxic-widget .pw-value { color: #00dd00; text-shadow: 0 0 8px rgba(0, 255, 0, 0.4); }
.toxic-widget .pw-label { color: rgba(0, 255, 0, 0.5); }
.toxic-active { color: #00dd00; text-shadow: 0 0 6px rgba(0, 255, 0, 0.5); animation: tsukuyomi-pulse 1.5s ease-in-out infinite; }
.toxic-inactive { color: rgba(0, 255, 0, 0.4); }

/* 31. Молодой Глеб (Tea/Calm) */
.yonggleb-widget {
  background: linear-gradient(135deg, rgba(150, 210, 180, 0.08), rgba(150, 210, 180, 0.02));
  border-color: rgba(150, 210, 180, 0.25);
}
.yonggleb-title { color: #96d2b4; text-shadow: 0 0 4px rgba(150, 210, 180, 0.4); }
.yonggleb-ready { color: #96d2b4; text-shadow: 0 0 6px rgba(150, 210, 180, 0.5); animation: tsukuyomi-pulse 1.5s ease-in-out infinite; }
.yonggleb-cooldown { color: rgba(150, 210, 180, 0.5); }

/* ── Pickle Rick ── */
.pickle-widget { border-color: rgba(80, 200, 80, 0.25); }
.pickle-title { color: #50c850; text-shadow: 0 0 4px rgba(80, 200, 80, 0.4); }
.pickle-active { color: #50c850; text-shadow: 0 0 6px rgba(80, 200, 80, 0.5); }
.pickle-penalty { color: #e05050; text-shadow: 0 0 6px rgba(224, 80, 80, 0.4); }
.pickle-off { color: rgba(180, 180, 180, 0.5); }
.pickle-penalty-val { color: #e05050; }
.pickle-attacked { color: #e05050; font-weight: 800; }

/* ── Giant Beans ── */
.beans-widget { border-color: rgba(160, 120, 60, 0.25); }
.beans-title { color: #c8a050; text-shadow: 0 0 4px rgba(200, 160, 80, 0.4); }
.beans-cooking { color: #e0a030; text-shadow: 0 0 6px rgba(224, 160, 48, 0.5); animation: tsukuyomi-pulse 1.5s ease-in-out infinite; }

/* ── HardKitty progress bar ── */
.hardkitty-bar-bg {
  width: 100%;
  height: 4px;
  border-radius: 2px;
  background: rgba(255, 255, 255, 0.08);
  margin-top: 4px;
  overflow: hidden;
}
.hardkitty-bar-fill {
  height: 100%;
  border-radius: 2px;
  background: linear-gradient(90deg, #e0c060, #f0d070);
  transition: width 0.4s ease;
}
.hardkitty-count { color: #e0c060; }

/* ── Privilege names ── */
.privilege-names {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.privilege-name-tag {
  font-size: 10px;
  padding: 1px 6px;
  border-radius: 3px;
  background: rgba(205, 127, 50, 0.15);
  color: #cd7f32;
  border: 1px solid rgba(205, 127, 50, 0.3);
}
.privilege-count { color: #cd7f32; font-weight: 700; }

/* ── Impact streak colors ── */
.impact-low { color: rgba(180, 180, 180, 0.6); }
.impact-mid { color: #e0c060; text-shadow: 0 0 4px rgba(224, 192, 96, 0.3); }
.impact-high { color: #e05050; text-shadow: 0 0 6px rgba(224, 80, 80, 0.5); font-weight: 800; }

/* ── Training waiting ── */
.training-waiting { color: rgba(180, 180, 180, 0.4); font-style: italic; }

/* ── Toxic Mate holder ── */
.toxic-holder { font-size: 11px; color: #a0e050; max-width: 60px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

/* ── Geralt ── */
.geralt-widget { border-color: rgba(100, 80, 40, 0.5); }
.geralt-title { color: #C8A050; text-shadow: 0 0 6px rgba(200, 160, 80, 0.25); }
.geralt-body { display: flex; flex-direction: column; gap: 3px; font-size: 11px; }
.geralt-row { display: flex; justify-content: space-between; gap: 6px; padding: 3px 6px; border-left: 3px solid transparent; border-radius: 3px; }
.geralt-oil-tier { color: rgba(180, 180, 180, 0.6); font-size: 10px; min-width: 50px; text-align: right; }
.geralt-status-row { display: flex; gap: 8px; font-size: 10px; color: rgba(180, 180, 180, 0.5); margin-top: 2px; }
/* ── Geralt demand mechanic ── */
.pc-geralt-demand {
  padding: 6px 10px;
  background: rgba(200, 160, 80, 0.06);
  border: 1px solid rgba(200, 160, 80, 0.2);
  border-radius: 6px;
  margin-top: 4px;
  transition: background 0.4s, border-color 0.4s;
}
.pc-geralt-demand.geralt-demand-warm {
  background: rgba(212, 136, 42, 0.08);
  border-color: rgba(212, 136, 42, 0.3);
}
.pc-geralt-demand.geralt-demand-hot {
  background: rgba(208, 80, 48, 0.08);
  border-color: rgba(208, 80, 48, 0.35);
}
.pc-geralt-demand.geralt-demand-critical {
  background: rgba(224, 32, 32, 0.1);
  border-color: rgba(224, 32, 32, 0.5);
  animation: geralt-pulse 1.5s ease-in-out infinite;
}
@keyframes geralt-pulse {
  0%, 100% { box-shadow: 0 0 4px rgba(224, 32, 32, 0.2); }
  50% { box-shadow: 0 0 12px rgba(224, 32, 32, 0.5); }
}
.geralt-demand-header {
  font-size: 11px;
  color: #C8A050;
  font-weight: 600;
  margin-bottom: 5px;
  text-align: center;
  transition: color 0.4s;
}
.geralt-demand-btns {
  display: flex;
  gap: 6px;
  margin-bottom: 6px;
}
.geralt-demand-btn {
  flex: 1;
  padding: 5px 8px;
  font-size: 11px;
  font-weight: 600;
  border: 1px solid rgba(200, 160, 80, 0.4);
  border-radius: 4px;
  background: rgba(200, 160, 80, 0.12);
  color: #C8A050;
  cursor: pointer;
  transition: all 0.15s;
}
.geralt-demand-btn:hover:not(:disabled) {
  background: rgba(200, 160, 80, 0.25);
  border-color: #C8A050;
}
.geralt-demand-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}
.geralt-demand-next {
  flex: 0 0 auto;
  min-width: 90px;
}
.geralt-displeasure {
  display: flex;
  align-items: center;
  gap: 8px;
}
.geralt-displeasure-bar {
  display: flex;
  gap: 2px;
  flex: 1;
}
.geralt-displeasure-seg {
  flex: 1;
  height: 6px;
  border-radius: 2px;
  background: rgba(200, 160, 80, 0.15);
  transition: background 0.3s;
}
.geralt-displeasure-text {
  font-size: 10px;
  color: rgba(200, 160, 80, 0.6);
  min-width: 28px;
  text-align: right;
  transition: color 0.4s;
}
.geralt-invoice {
  margin-bottom: 6px;
  padding: 4px 6px;
  background: rgba(200, 160, 80, 0.04);
  border-radius: 4px;
  font-size: 10px;
}
.geralt-invoice-line {
  display: flex;
  justify-content: space-between;
  padding: 1px 0;
}
.geralt-invoice-label {
  color: rgba(200, 200, 200, 0.7);
}
.geralt-invoice-pts {
  font-weight: 600;
  min-width: 24px;
  text-align: right;
}
.geralt-invoice-pts.pts-pos { color: #4ADE80; }
.geralt-invoice-pts.pts-neg { color: #F87171; }
.geralt-invoice-total {
  display: flex;
  justify-content: space-between;
  padding: 3px 0 2px;
  margin-top: 2px;
  border-top: 1px solid rgba(200, 160, 80, 0.15);
  font-weight: 700;
  font-size: 11px;
}
.geralt-invoice-total.inv-tier-great { color: #4ADE80; }
.geralt-invoice-total.inv-tier-good { color: #C8A050; }
.geralt-invoice-total.inv-tier-mid { color: #FACC15; }
.geralt-invoice-total.inv-tier-bad { color: #F87171; }
.geralt-invoice-prediction {
  display: flex;
  gap: 8px;
  justify-content: center;
  padding-top: 2px;
  font-size: 10px;
  font-weight: 600;
}
.geralt-inv-coins { color: #4ADE80; }
.geralt-inv-displ { color: #F87171; }
.geralt-inv-nothing { color: rgba(200, 200, 200, 0.4); }

/* ── Floating damage numbers ── */
.floating-numbers-container {
  position: relative;
  height: 0;
  overflow: visible;
  pointer-events: none;
}

.floating-number {
  position: absolute;
  right: 8px;
  top: -4px;
  font-size: 16px;
  font-weight: 900;
  font-family: var(--font-mono);
  pointer-events: none;
  z-index: 10;
  animation: float-up-stat 1.2s ease-out forwards;
  white-space: nowrap;
}

.float-positive { color: var(--accent-green); }
.float-negative { color: var(--accent-red); }
.float-intelligence { text-shadow: 0 0 6px rgba(91, 155, 213, 0.5); }
.float-strength { text-shadow: 0 0 6px rgba(224, 85, 69, 0.5); }
.float-speed { text-shadow: 0 0 6px rgba(220, 195, 50, 0.5); }
.float-psyche { text-shadow: 0 0 6px rgba(176, 122, 216, 0.5); }

@keyframes float-up-stat {
  0% { opacity: 1; transform: translateY(0) scale(0.5); }
  15% { opacity: 1; transform: translateY(-4px) scale(1.2); }
  30% { opacity: 1; transform: translateY(-10px) scale(1); }
  70% { opacity: 0.8; transform: translateY(-30px) scale(1.1); }
  100% { opacity: 0; transform: translateY(-45px) scale(0.9); }
}

.float-stat-label {
  font-size: 10px;
  font-weight: 700;
  opacity: 0.7;
  letter-spacing: 0.5px;
}

.float-big {
  font-size: 20px;
  animation-duration: 1.5s !important;
}
.float-big.float-intelligence { text-shadow: 0 0 10px rgba(91, 155, 213, 0.7); }
.float-big.float-strength { text-shadow: 0 0 10px rgba(224, 85, 69, 0.7); }
.float-big.float-speed { text-shadow: 0 0 10px rgba(220, 195, 50, 0.7); }
.float-big.float-psyche { text-shadow: 0 0 10px rgba(176, 122, 216, 0.7); }

.float-num-enter-active { animation: float-up-stat 1.2s ease-out forwards; }
.float-num-leave-active { display: none; }


/* ── Ghost stat bars (before/after) ── */
.stat-bar-ghost {
  position: absolute;
  top: 0;
  left: 0;
  height: 100%;
  border-radius: inherit;
  opacity: 0.4;
  animation: ghost-fade 1.5s ease-out forwards;
  z-index: 0;
}
.stat-bar-ghost.intelligence { background: var(--kh-c-secondary-info-200); }
.stat-bar-ghost.strength { background: var(--kh-c-secondary-danger-200); }
.stat-bar-ghost.speed { background: var(--kh-c-secondary-success-200); }
.stat-bar-ghost.psyche { background: var(--kh-c-secondary-purple-200); }
@keyframes ghost-fade {
  0% { opacity: 0.4; }
  100% { opacity: 0; }
}

/* ── Score combo multiplier ── */
.combo-multiplier {
  position: absolute;
  right: -10px;
  top: -8px;
  font-size: 12px;
  font-weight: 900;
  color: var(--accent-gold);
  text-shadow: 0 0 12px rgba(240, 200, 80, 0.6);
  animation: combo-pop-scale 0.3s var(--ease-spring);
  pointer-events: none;
  letter-spacing: 0.5px;
  font-family: var(--font-mono);
  padding: 1px 5px;
  border: 1px solid transparent;
  border-radius: 4px;
}
@keyframes combo-pop-scale {
  0% { transform: scale(0.5); opacity: 0; }
  100% { transform: scale(1); opacity: 1; }
}

/* ── Confetti burst on big score gains ── */
.confetti-container {
  position: absolute;
  inset: 0;
  pointer-events: none;
  overflow: visible;
  z-index: 5;
}
.confetti-particle {
  position: absolute;
  width: 6px;
  height: 6px;
  border-radius: 1px;
  top: 50%;
  left: 50%;
  animation: confetti-fly 1.5s ease-out forwards;
}
.confetti-particle:nth-child(1)  { background: #f0c850; --confetti-x: -40px; --confetti-y: -50px; --confetti-r: 120deg; animation-delay: 0ms; }
.confetti-particle:nth-child(2)  { background: #ff7f6e; --confetti-x: 35px;  --confetti-y: -55px; --confetti-r: -90deg; animation-delay: 30ms; }
.confetti-particle:nth-child(3)  { background: #64b4f0; --confetti-x: -50px; --confetti-y: -20px; --confetti-r: 200deg; animation-delay: 60ms; }
.confetti-particle:nth-child(4)  { background: #a082dc; --confetti-x: 55px;  --confetti-y: -30px; --confetti-r: -150deg; animation-delay: 90ms; }
.confetti-particle:nth-child(5)  { background: #48cab4; --confetti-x: -30px; --confetti-y: -60px; --confetti-r: 80deg; animation-delay: 50ms; }
.confetti-particle:nth-child(6)  { background: #f0d250; --confetti-x: 45px;  --confetti-y: -45px; --confetti-r: -200deg; animation-delay: 70ms; }
.confetti-particle:nth-child(7)  { background: #ff7f6e; --confetti-x: -55px; --confetti-y: -35px; --confetti-r: 160deg; animation-delay: 40ms; }
.confetti-particle:nth-child(8)  { background: #64b4f0; --confetti-x: 25px;  --confetti-y: -65px; --confetti-r: -60deg; animation-delay: 100ms; }
.confetti-particle:nth-child(9)  { background: #a082dc; --confetti-x: -45px; --confetti-y: -45px; --confetti-r: 240deg; animation-delay: 20ms; }
.confetti-particle:nth-child(10) { background: #48cab4; --confetti-x: 50px;  --confetti-y: -25px; --confetti-r: -120deg; animation-delay: 80ms; }
.confetti-particle:nth-child(11) { background: #f0c850; --confetti-x: -20px; --confetti-y: -55px; --confetti-r: 300deg; animation-delay: 110ms; }
.confetti-particle:nth-child(12) { background: #ff7f6e; --confetti-x: 40px;  --confetti-y: -60px; --confetti-r: -280deg; animation-delay: 60ms; }
@keyframes confetti-fly {
  0% {
    transform: translate(0, 0) rotate(0deg) scale(1);
    opacity: 1;
  }
  70% {
    opacity: 0.8;
  }
  100% {
    transform: translate(var(--confetti-x), var(--confetti-y)) rotate(var(--confetti-r)) scale(0.3);
    opacity: 0;
  }
}

/* ── Widget experience layer ─────────────────────────────────────── */
.pc-passive-widget button:focus-visible,
.pc-geralt-demand button:focus-visible,
.pc-moral-actions button:focus-visible {
  outline: 2px solid #f0c850;
  outline-offset: 2px;
}

.pc-passive-widget,
.pc-special-ability,
.pc-exploit-state,
.pc-tsukuyomi-state {
  position: relative;
  min-width: 0;
  border-radius: 10px;
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.035), 0 4px 12px rgba(0, 0, 0, 0.12);
  transition: transform 0.18s ease, border-color 0.22s ease, box-shadow 0.22s ease;
}
[data-widget-help] {
  cursor: help;
  outline: none;
}
[data-widget-help]:focus-visible {
  outline: 2px solid rgba(240, 200, 80, 0.88);
  outline-offset: 2px;
}
.pc-passive-widget {
  padding: 10px 11px;
}
.pc-passive-widget:hover,
.pc-special-ability:hover,
.pc-exploit-state:hover,
.pc-tsukuyomi-state:hover {
  transform: translateY(-1px);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.06), 0 7px 18px rgba(0, 0, 0, 0.18);
}
.pw-header {
  min-height: 24px;
  gap: 8px;
}
.pw-title {
  font-size: 11px;
  line-height: 1.25;
  letter-spacing: 0.09em;
}
.pw-status {
  display: inline-flex;
  min-height: 22px;
  max-width: 62%;
  align-items: center;
  justify-content: center;
  padding: 3px 7px;
  border: 1px solid rgba(255, 255, 255, 0.09);
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.045);
  font-size: 10px;
  line-height: 1.2;
  text-align: center;
}
.pw-body {
  flex-wrap: wrap;
  gap: 7px;
  margin-top: 7px;
}
.pw-stat-pair {
  min-width: 0;
  flex: 1 1 74px;
  flex-direction: column;
  align-items: flex-start;
  gap: 1px;
  padding: 5px 7px;
  border-radius: 6px;
  background: rgba(255, 255, 255, 0.035);
}
.pw-value {
  max-width: 100%;
  overflow: hidden;
  font-size: 18px;
  line-height: 1.1;
  text-overflow: ellipsis;
}
.pw-label,
.bulk-chance-label {
  color: rgba(255, 255, 255, 0.6) !important;
  font-size: 10px;
  line-height: 1.25;
}
[data-widget-help]::after { content: none; }
.pw-title::after,
.sa-header::after,
.exploit-title::after,
.tsukuyomi-title::after {
  content: '?';
  display: inline-grid;
  width: 15px;
  height: 15px;
  margin-left: 6px;
  place-items: center;
  border: 1px solid currentColor;
  border-radius: 50%;
  opacity: 0.38;
  font: 800 9px/1 var(--font-mono);
  vertical-align: 1px;
  transition: opacity 0.16s ease, transform 0.16s ease;
}
[data-widget-help]:hover :is(.pw-title, .sa-header, .exploit-title, .tsukuyomi-title)::after,
[data-widget-help]:focus :is(.pw-title, .sa-header, .exploit-title, .tsukuyomi-title)::after,
[data-widget-help]:focus-within :is(.pw-title, .sa-header, .exploit-title, .tsukuyomi-title)::after {
  opacity: 0.9;
  transform: rotate(12deg) scale(1.06);
}
[data-widget-help]:hover::after,
[data-widget-help]:focus::after,
[data-widget-help]:focus-within::after {
  content: attr(data-widget-help);
  position: relative;
  right: auto;
  bottom: auto;
  display: block;
  width: auto;
  height: auto;
  margin-top: 9px;
  padding: 8px 9px;
  border: 1px solid rgba(255, 255, 255, 0.07);
  border-radius: 7px;
  background: rgba(0, 0, 0, 0.16);
  color: rgba(244, 246, 250, 0.76);
  font: 500 11px/1.45 var(--font-body);
  animation: widget-help-in 0.18s ease-out both;
}
@keyframes widget-help-in {
  from { opacity: 0; transform: translateY(-3px); }
  to { opacity: 1; transform: translateY(0); }
}

/* Character-flavored response without obscuring the state itself. */
.pc-passive-widget .pw-title,
.geralt-row,
.goblin-pop-total,
.kotiki-cat-icon,
.impact-streak {
  transition: transform 0.18s ease, letter-spacing 0.18s ease, text-shadow 0.18s ease;
}
.pc-passive-widget:hover .pw-title { letter-spacing: 0.12em; }
.bulk-widget:hover .bulk-wave-fill { animation-duration: 0.7s; }
.tea-widget:hover .tea-title { text-shadow: 0 0 12px rgba(212, 165, 116, 0.75); }
.beans-widget:hover .beans-cooking { transform: rotate(-2deg) scale(1.04); }
.jaws-widget:hover .jaws-shark { animation-duration: 0.55s !important; }
.goblin-widget:hover .goblin-pop-total { transform: rotate(-3deg) scale(1.08); }
.kotiki-widget:hover .kotiki-cat-icon { animation: widget-cat-hop 0.55s ease; }
.impact-widget:hover .impact-streak { transform: scale(1.12); }
.ink-widget:hover { box-shadow: inset 0 0 22px rgba(123, 104, 238, 0.1), 0 7px 18px rgba(0, 0, 0, 0.18); }
.shinigami-widget:hover { box-shadow: inset 0 0 22px rgba(255, 0, 0, 0.09), 0 7px 18px rgba(0, 0, 0, 0.18); }
.geralt-widget:hover .geralt-row:nth-child(odd) { transform: translateX(2px); }
@keyframes widget-cat-hop {
  0%, 100% { transform: translateY(0) rotate(0); }
  45% { transform: translateY(-3px) rotate(-8deg); }
}

.hardkitty-bar-bg,
.dragon-bar-bg,
.garbage-bar-bg,
.goblin-pop-track,
.bulk-wave-bar,
.exploit-bar-bg,
.tsukuyomi-bar-bg {
  min-height: 6px;
}
.moral-btn,
.geralt-demand-btn,
.doom-chainsaw-choice button {
  min-height: 40px;
}
.geralt-demand-btn:active:not(:disabled),
.doom-chainsaw-choice button:active {
  transform: translateY(1px) scale(0.99);
}

@media (max-width: 600px) {
  .player-card { padding: 11px; gap: 9px; }
  .pc-passive-widget { padding: 11px; }
  .pw-title { font-size: 12px; }
  .pw-status { max-width: 58%; font-size: 10px; }
  .pw-label,
  .bulk-chance-label { font-size: 11px; }
  .moral-btn,
  .geralt-demand-btn,
  .doom-chainsaw-choice button { min-height: 44px; }
  .geralt-demand-btns,
  .doom-chainsaw-choice { grid-template-columns: 1fr; }
  .geralt-demand-btns { flex-direction: column; }
  .geralt-demand-next { width: 100%; }
  .doom-module-list { grid-template-columns: 1fr; }
  .doom-module-card { min-height: 62px; }
  .doom-module-copy strong { white-space: normal; }
}

@media (prefers-reduced-motion: reduce) {
  .pc-passive-widget,
  .pc-passive-widget *,
  .pc-special-ability,
  .pc-exploit-state,
  .pc-tsukuyomi-state {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    scroll-behavior: auto !important;
    transition-duration: 0.01ms !important;
  }
  .pc-passive-widget:hover,
  .pc-special-ability:hover,
  .pc-exploit-state:hover,
  .pc-tsukuyomi-state:hover { transform: none; }
}
</style>

<!-- Unscoped styles for widgets that use Teleport to body -->
<style>
/* ── TheBoys Widget ── */
.theboys-widget {
  border-color: #444;
}
.theboys-title {
  color: #e63946;
  font-weight: 700;
  letter-spacing: 1px;
}
.theboys-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 4px;
  margin-top: 4px;
}
.theboys-member {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 4px;
  padding: 4px 6px;
  transition: box-shadow 0.3s ease;
}
.theboys-member-header {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 0.8em;
}
.theboys-icon { font-size: 1em; }
.theboys-name {
  flex: 1;
  color: #ccc;
  font-weight: 600;
}
.theboys-val {
  color: #fff;
  font-weight: 700;
  font-size: 0.9em;
}
.theboys-poker-val {
  color: #ff4444;
  text-shadow: 0 0 6px rgba(255, 68, 68, 0.6);
}
.theboys-butcher {
  transition: box-shadow 0.4s ease;
}
.theboys-disabled {
  opacity: 0.5;
  border-color: rgba(255, 0, 0, 0.3);
}
.theboys-order {
  font-size: 0.75em;
  color: #ffb347;
  margin-top: 2px;
}
.theboys-order-rounds { color: #999; }
.theboys-stats {
  display: flex;
  gap: 6px;
  font-size: 0.75em;
  margin-top: 2px;
}
.theboys-stat-ok { color: #4caf50; }
.theboys-stat-fail { color: #f44336; }
.theboys-kimiko-status {
  color: #f44336;
  font-size: 0.7em;
  font-weight: 700;
  margin-top: 2px;
}
.theboys-kimiko-blocked {
  font-size: 0.7em;
  color: #90caf9;
  margin-top: 2px;
}
.theboys-mm-active {
  color: #4fc3f7;
  font-size: 0.7em;
  font-weight: 700;
  margin-top: 2px;
  animation: theboys-pulse 1s ease-in-out infinite;
}
@keyframes theboys-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}
.theboys-kompromat-list {
  margin-top: 3px;
}
.theboys-kompromat-entry {
  display: flex;
  flex-direction: column;
  font-size: 0.7em;
  padding: 2px 0;
  border-top: 1px solid rgba(255, 255, 255, 0.05);
}
.theboys-kompromat-name {
  color: #ffb347;
  font-weight: 600;
}
.theboys-kompromat-hint {
  color: #888;
  font-style: italic;
}

/* TheBoys — widget extras */
.theboys-kompromat-count { color: #ffb347; font-size: 0.85em; }
.theboys-calm { color: #9ccc65 !important; animation: none !important; }
.theboys-calm-member { box-shadow: inset 0 0 8px rgba(156, 204, 101, 0.25); }
.theboys-ultimates {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 6px;
}
.theboys-ult-badge {
  font-size: 0.68em;
  font-weight: 800;
  padding: 2px 6px;
  border-radius: 4px;
  letter-spacing: 0.3px;
}
.theboys-ult-superdick { background: rgba(150, 0, 0, 0.3); color: #ff5252; border: 1px solid rgba(255, 60, 60, 0.5); animation: theboys-pulse 1.2s ease-in-out infinite; }
.theboys-ult-livingweapon { background: rgba(120, 40, 160, 0.28); color: #ce93d8; border: 1px solid rgba(206, 147, 216, 0.5); }
.theboys-ult-virus { background: rgba(60, 140, 40, 0.28); color: #aed581; border: 1px solid rgba(174, 213, 129, 0.5); }
.theboys-sup-target-badge {
  position: absolute;
  top: -9px;
  right: 12px;
  z-index: 4;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 3px 8px;
  border: 1px solid rgba(255, 205, 64, 0.8);
  border-radius: 999px;
  background: linear-gradient(135deg, rgba(104, 22, 148, 0.97), rgba(198, 42, 64, 0.97));
  color: #fff2a8;
  box-shadow: 0 0 12px rgba(255, 80, 100, 0.55), inset 0 0 8px rgba(255, 220, 90, 0.18);
  font-size: 0.68rem;
  font-weight: 900;
  letter-spacing: 0.08em;
  pointer-events: none;
}
.theboys-marks-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px;
  margin-top: 5px;
  font-size: 0.7em;
}
.theboys-marks-label { opacity: 0.9; }
.theboys-mark-chip {
  background: rgba(255, 80, 80, 0.18);
  color: #ffab91;
  padding: 1px 6px;
  border-radius: 3px;
  font-weight: 600;
}
.theboys-mark-virus { background: rgba(120, 200, 90, 0.2); color: #c5e1a5; }

/* DooM Guy */
.eren-widget { border-color: rgba(185, 63, 45, .62) !important; background: linear-gradient(135deg, rgba(88, 28, 20, .3), rgba(17, 14, 13, .96)) !important; }
.eren-title { color: #ff8068; letter-spacing: .13em; }
.eren-ready { color: #ffb36b; }
.eren-failed { color: #8f8f8f; }
.eren-stats { display: flex; flex-wrap: wrap; gap: 5px 10px; color: #f0c3aa; font-size: .72em; }
.eren-titan-cooldown { order: -1; color: rgba(240, 195, 170, .62); }
.eren-titan-ready { color: #ffb36b; }
.eren-marks { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 7px; }
.eren-mark { padding: 2px 6px; border: 1px solid rgba(255, 91, 55, .35); border-radius: 4px; background: rgba(160, 38, 18, .2); color: #ffad8e; font-size: .68em; }

.naruto-widget { border-color: rgba(245, 130, 52, .55) !important; background: linear-gradient(135deg, rgba(62, 30, 12, .35), rgba(17, 14, 13, .96)) !important; }
.naruto-title { color: #ff9b4a; letter-spacing: .08em; }
.naruto-ready { color: #ffc36b; text-shadow: 0 0 6px rgba(255, 155, 74, .45); }
.naruto-cooldown { color: rgba(255, 195, 107, .58); }

.doom-widget {
  position: relative;
  overflow: hidden;
  padding: 10px !important;
  border-color: rgba(226, 74, 42, .62) !important;
  background:
    linear-gradient(115deg, rgba(82, 19, 10, .34), transparent 52%),
    repeating-linear-gradient(135deg, rgba(255,255,255,.018) 0 1px, transparent 1px 7px),
    #0d0d0f !important;
  box-shadow: inset 0 0 28px rgba(143, 28, 12, .12);
}
.doom-widget::before { content: ''; position: absolute; top: 0; right: 0; width: 42px; height: 2px; background: #ef6545; box-shadow: 0 0 12px #ef6545; }
.doom-title { display: block; color: #ff8264; letter-spacing: .1em; }
.doom-subtitle { display: block; margin-top: 2px; color: #74483e; font-size: 7px; font-weight: 900; letter-spacing: .16em; }
.doom-mode { padding: 3px 6px; border: 1px solid rgba(239, 101, 69, .25); border-radius: 3px; background: rgba(239, 101, 69, .07); color: #a66b5e; font-size: 8px; }
.doom-roll-active { color: #ffb36b; text-shadow: 0 0 8px rgba(255, 76, 30, .7); }
.doom-module-list { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 5px; margin-top: 8px; }
.doom-module-list--selecting { opacity: .58; }
.doom-module-card { display: grid; grid-template-columns: 25px minmax(0, 1fr); gap: 6px; min-height: 55px; padding: 7px; border: 1px solid rgba(255,255,255,.07); border-radius: 6px; background: rgba(255,255,255,.025); }
.doom-module-card--live { border-color: rgba(239, 101, 69, .24); background: linear-gradient(135deg, rgba(130, 34, 19, .13), rgba(255,255,255,.02)); }
.doom-module-card--done { border-color: rgba(102, 203, 89, .22); }
.doom-module-card--failed { border-color: rgba(150, 150, 150, .18); filter: grayscale(.62); opacity: .62; }
.doom-module-icon { display: grid; place-items: center; width: 25px; height: 25px; border-radius: 4px; color: #ef7954; background: rgba(239, 101, 69, .09); font-size: 13px; }
.doom-module-card--done .doom-module-icon { color: #8fda7e; background: rgba(102, 203, 89, .08); }
.doom-module-copy { display: flex; min-width: 0; flex-direction: column; gap: 1px; }
.doom-module-copy small { color: #7f5045; font-size: 7px; font-weight: 900; letter-spacing: .08em; }
.doom-module-copy strong { overflow: hidden; color: #f0c9ba; font-size: 9px; text-overflow: ellipsis; white-space: nowrap; }
.doom-module-copy > span { color: rgba(255,255,255,.48); font-size: 8px; line-height: 1.3; }
.doom-nests { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; margin-top: 7px; padding: 6px; border: 1px solid rgba(239, 101, 69, .16); border-radius: 5px; background: rgba(108, 24, 12, .1); color: #d78c72; font-size: 8px; }
.doom-nests strong { margin-right: 2px; color: #f18a55; }
.doom-nests span { padding: 2px 5px; border-radius: 3px; background: rgba(239, 101, 69, .1); }
.doom-bfg { display: flex; align-items: center; gap: 6px; margin-top: 7px; padding: 5px 7px; border: 1px solid rgba(123, 255, 92, .25); border-radius: 4px; color: #9cff78; background: rgba(69, 135, 39, .08); font-size: 9px; font-weight: 900; letter-spacing: .12em; animation: doom-charge 1s ease-in-out infinite alternate; }
.doom-bfg span { color: #70ff4d; font-size: 8px; }
.doom-chainsaw-choice { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 5px; margin-top: 8px; padding-top: 8px; border-top: 1px solid rgba(239, 101, 69, .2); color: #efb094; font-size: 9px; }
.doom-chainsaw-choice > strong, .doom-chainsaw-choice > span { grid-column: 1 / -1; }
.doom-chainsaw-choice > span { color: rgba(255,255,255,.46); font-size: 8px; }
.doom-chainsaw-choice button { display: flex; min-width: 0; flex-direction: column; gap: 2px; padding: 7px; color: #f5d7c7; text-align: left; border: 1px solid #743324; border-radius: 5px; background: #24120e; cursor: pointer; transition: border-color .15s, transform .15s; }
.doom-chainsaw-choice button:hover { transform: translateY(-1px); border-color: #e86543; }
.doom-chainsaw-choice button b { font-size: 9px; }
.doom-chainsaw-choice button small { overflow: hidden; color: rgba(255,255,255,.45); font-size: 7px; text-overflow: ellipsis; white-space: nowrap; }
@keyframes doom-charge { to { text-shadow: 0 0 9px #59ff37; } }

/* Dense widgets stay readable without losing their character styling. */
.theboys-grid { gap: 7px; margin-top: 8px; }
.theboys-member { padding: 7px 8px; border-radius: 7px; }
.theboys-member-header { font-size: 11px; }
.theboys-stats,
.theboys-order,
.theboys-kimiko-status,
.theboys-kimiko-blocked,
.theboys-mm-active,
.theboys-kompromat-entry,
.theboys-marks-row { font-size: 10px; line-height: 1.35; }
.theboys-ult-badge { padding: 4px 7px; font-size: 10px; }
.eren-stats { font-size: 11px; line-height: 1.4; }
.eren-mark { padding: 3px 7px; font-size: 10px; }

.doom-subtitle,
.doom-module-copy small { font-size: 9px; }
.doom-mode,
.doom-module-copy > span,
.doom-nests,
.doom-chainsaw-choice > span { font-size: 10px; }
.doom-module-copy strong,
.doom-bfg,
.doom-chainsaw-choice,
.doom-chainsaw-choice button b { font-size: 11px; }
.doom-chainsaw-choice button small { font-size: 10px; line-height: 1.35; white-space: normal; }
.doom-module-card { min-height: 68px; padding: 9px; }
.doom-module-copy { gap: 3px; }

.salldorum-widget {
  border-color: rgba(112, 174, 255, 0.32);
  background: linear-gradient(135deg, rgba(46, 102, 176, 0.13), rgba(13, 25, 44, 0.28));
}
.salldorum-title { color: #75b9ff; text-shadow: 0 0 7px rgba(70, 150, 255, 0.38); }
.salldorum-body { display: grid; gap: 5px; margin-top: 7px; }
.salldorum-row {
  display: grid;
  grid-template-columns: minmax(54px, auto) minmax(0, 1fr) auto;
  align-items: center;
  gap: 7px;
  padding: 6px 7px;
  border: 1px solid rgba(117, 185, 255, 0.1);
  border-radius: 6px;
  background: rgba(255, 255, 255, 0.03);
  font-size: 11px;
}
.salldorum-label { color: rgba(196, 224, 255, 0.62); font-weight: 700; }
.salldorum-val { overflow: hidden; color: #d6eaff; font-weight: 800; text-overflow: ellipsis; }
.salldorum-active,
.salldorum-available { color: #77e6aa; font-size: 10px; font-weight: 900; }
.salldorum-used { color: rgba(255, 255, 255, 0.38); font-size: 10px; font-weight: 800; }

@media (max-width: 600px) {
  .theboys-grid { grid-template-columns: 1fr; }
  .doom-module-list { grid-template-columns: 1fr; }
  .salldorum-row { grid-template-columns: 54px minmax(0, 1fr); }
  .salldorum-active,
  .salldorum-available,
  .salldorum-used { grid-column: 1 / -1; }
}
</style>
