<script setup lang="ts">
import { computed, ref, watch, onUnmounted } from 'vue'
import type { Player, PortalGun, ExploitState, TsukuyomiState, PassiveAbilityStates } from 'src/services/signalr'
import { useGameStore } from 'src/store/game'
import {
  playComboHype,
  playComboPluck,
  playPointsIncreaseSound,
} from 'src/services/sound'

interface ScoreEntry {
  raw: string
  html: string
  type: string
  comboCount: number
}

const props = withDefaults(defineProps<{
  player: Player
  isMe: boolean
  resistFlash?: string[]
  justiceReset?: boolean
  scoreEntries?: ScoreEntry[]
  scoreAnimReady?: boolean
}>(), {
  resistFlash: () => [],
  justiceReset: false,
  scoreEntries: () => [],
  scoreAnimReady: false,
})

const store = useGameStore()
type LevelUpStatIndex = 1 | 2 | 3 | 4

const hasLvlUpPoints = computed(() => props.isMe && (props.player?.status.lvlUpPoints ?? 0) > 0)
const lvlUpPoints = computed(() => props.player?.status.lvlUpPoints ?? 0)

const moral = computed(() => {
  if (!props.player) return 0
  return Number.parseFloat(props.player.character.moralDisplay) || 0
})

const hasMoral = computed(() => props.isMe && moral.value >= 1)

const roundNo = computed(() => store.gameState?.roundNo ?? 0)
const isLastRound = computed(() => roundNo.value === 10)

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

const isGoblin = computed(() => props.player?.character.name === 'Стая Гоблинов')
const goblin = computed(() => passiveStates.value?.goblinSwarm ?? null)

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

/** A single "hit" — one combo source within a score entry */
interface ComboHit {
  label: string        // e.g. "Победа", "Заводить друзей"
  pointsDelta: number  // points this hit adds
  hitIndex: number     // global sequential index across all entries
  comboNum: number     // 1-based combo counter within the entry (1 = first, 2+ = combo)
  totalInEntry: number // how many hits in this entry
}

/** Expand all score entries into individual hits */
const comboHits = computed<ComboHit[]>(() => {
  const hits: ComboHit[] = []
  let globalIdx = 0
  for (const entryR of props.scoreEntries) {
    // Remove Discord markdown formatting
    const entry = entryR.raw.replaceAll("**", "").replaceAll("__", "").replaceAll("~~", "")
    // Parse total points: "+8 обычных очков ..." or "Блок: -1 бонусных очков"
    const ptsMatch = entry.match(/([+-]?\d+)\s*(?:обычных|бонусных)?\s*очков/i)
    const totalPts = ptsMatch ? parseInt(ptsMatch[1]) : 0
    // Parse combo sources from parentheses: "(Победа+Заводить друзей+Победа)"
    const parenMatch = entry.match(/\(([^)]+)\)/)
    let sources: string[]
    if (parenMatch) {
      sources = parenMatch[1].split('+').map((s: string) => s.trim()).filter((s: string) => s)
    } else {
      // Use text before the number as label, e.g. "Блок:" → "Блок"
      const prefixMatch = entry.match(/^([^0-9+-]+)/)
      const prefix = prefixMatch ? prefixMatch[1].replace(/[:\s]+$/, '').trim() : ''
      sources = [prefix || 'Очки']
    }
    const perHit = sources.length > 0 ? Math.round(totalPts / sources.length) : totalPts
    for (let i = 0; i < sources.length; i++) {
      hits.push({
        label: sources[i],
        pointsDelta: perHit,
        hitIndex: globalIdx++,
        comboNum: i + 1,
        totalInEntry: sources.length,
      })
    }
  }
  return hits
})

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
  const entries = props.scoreEntries
  if (!entries.length) {
    hitVisibleCount.value = 0; hitActiveIdx.value = -1; animatedScoreDelta.value = 0
    return
  }
  hitVisibleCount.value = 0
  hitActiveIdx.value = -1
  animatedScoreDelta.value = 0
  setTimeout(() => {
    let i = 0
    const allHits = comboHits.value
    comboTimer = setInterval(() => {
      if (i >= allHits.length) { clearComboTimer(); setTimeout(() => { hitActiveIdx.value = -1 }, 600); return }
      hitActiveIdx.value = i
      const hit = allHits[i]
      if (hit.pointsDelta > 0) {
        playPointsIncreaseSound(hit.pointsDelta)
        if (hit.comboNum === 1) {
          playComboPluck(hit.totalInEntry)
          playComboHype(hit.totalInEntry)
        }
      }
      animatedScoreDelta.value += hit.pointsDelta
      i++
      hitVisibleCount.value = i
    }, 350)
  }, 100)
}

// Reset when score entries change (new round)
watch(() => props.scoreEntries, (entries: ScoreEntry[]) => {
  const snap = entries.map((e: ScoreEntry) => e.raw).join('|')
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
  if (ready && props.scoreEntries.length > 0 && hitVisibleCount.value === 0) {
    startComboAnimation()
  }
})

onUnmounted(() => { clearComboTimer() })

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
const tipText = ref('')
const tipVisible = ref(false)
const tipPos = ref({ x: 0, y: 0 })
let tipTimer: ReturnType<typeof setTimeout> | null = null

function showTip(e: MouseEvent, text: string) {
  if (tipTimer) clearTimeout(tipTimer)
  tipText.value = text
  tipPos.value = { x: e.clientX, y: e.clientY }
  tipTimer = setTimeout(() => { tipVisible.value = true }, 120)
}
function moveTip(e: MouseEvent) {
  tipPos.value = { x: e.clientX, y: e.clientY }
}
function hideTip() {
  if (tipTimer) clearTimeout(tipTimer)
  tipVisible.value = false
}

function handleLevelUp(statIndex: LevelUpStatIndex) {
  void store.levelUp(statIndex)
}

function handleMoralToPoints() {
  void store.moralToPoints()
}

function handleMoralToSkill() {
  void store.moralToSkill()
}
</script>

<template>
  <div class="player-card" :class="{ 'is-me': isMe, 'is-bug': isBug, 'is-dragon': passiveStates?.dragon, 'is-awakened': passiveStates?.dragon?.isAwakened }"
    :style="passiveStates?.privilege && passiveStates.privilege.markedCount > 0 ? { borderColor: 'rgba(205, 127, 50, 0.5)', boxShadow: '0 0 12px rgba(205, 127, 50, 0.2)' } : {}"
  >
    <!-- Large avatar -->
    <div class="pc-avatar-wrap">
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

    <!-- Name & character -->
    <div class="pc-identity">
      <div class="pc-name">{{ player.character.name }}</div>
      <div class="pc-username">{{ player.discordUsername }}</div>
    </div>

    <!-- Stats with bars + resist/quality -->
    <div class="pc-stats">
      <div v-if="hasLvlUpPoints" class="lvl-up-badge">
        +{{ lvlUpPoints }} очков
      </div>

      <!-- Goblin level-up upgrades (replaces stat +buttons) -->
      <div v-if="isGoblin && hasLvlUpPoints && goblin" class="goblin-lvlup">
        <button class="goblin-lvlup-btn" data-sfx-skip-default="true" @click="handleLevelUp(1)">
          <span class="goblin-lvlup-name">Правильное питание</span>
          <span class="goblin-lvlup-desc">Больше Хобгоблинов (сейчас каждый {{ goblin.hobRate }}й)</span>
        </button>
        <button class="goblin-lvlup-btn" data-sfx-skip-default="true" @click="handleLevelUp(2)">
          <span class="goblin-lvlup-name">Контрактная армия</span>
          <span class="goblin-lvlup-desc">Больше Воинов (сейчас каждый {{ goblin.warriorRate }}й)</span>
        </button>
        <button class="goblin-lvlup-btn" data-sfx-skip-default="true" @click="handleLevelUp(3)">
          <span class="goblin-lvlup-name">Трудовые условия</span>
          <span class="goblin-lvlup-desc">Больше Трудяг (сейчас каждый {{ goblin.workerRate }}й)</span>
        </button>
        <button class="goblin-lvlup-btn" :class="{ 'goblin-lvlup-disabled': goblin.festivalUsed }" :disabled="goblin.festivalUsed" data-sfx-skip-default="true" @click="handleLevelUp(4)">
          <span class="goblin-lvlup-name">Праздник Гоблинов</span>
          <span v-if="goblin.festivalUsed" class="goblin-lvlup-desc">Уже использовано</span>
          <span v-else class="goblin-lvlup-desc">Удвоить гоблинов ({{ goblin.totalGoblins }} &rarr; {{ goblin.totalGoblins * 2 }})</span>
        </button>
      </div>

      <template v-else>
      <!-- Intelligence -->
      <div class="stat-block" :class="{ 'resist-hit': resistFlash.includes('intelligence'), 'lvl-up-available': hasLvlUpPoints }">
        <div class="stat-row">
          <span class="gi gi-lg gi-int">INT</span>
          <div class="stat-bar-bg">
            <div class="stat-bar intelligence" :style="{ width: `${player.character.intelligence * 10}%` }" />
          </div>
          <span class="stat-val stat-intelligence">{{ player.character.intelligence }}</span>
          <button v-if="hasLvlUpPoints" class="lvl-btn" data-sfx-skip-default="true" title="+1 Intelligence" @click="handleLevelUp(1)">+</button>
        </div>
        <div v-if="isMe" class="resist-row">
          <span class="resist-badge"><span class="gi gi-def">DEF</span> {{ player.character.intelligenceResist }}</span>
          <span v-if="player.character.intelligenceBonusText" class="resist-bonus">{{ player.character.intelligenceBonusText }}</span>
        </div>
      </div>
      <!-- Strength -->
      <div class="stat-block" :class="{ 'resist-hit': resistFlash.includes('strength'), 'lvl-up-available': hasLvlUpPoints }">
        <div class="stat-row">
          <span class="gi gi-lg gi-str">STR</span>
          <div class="stat-bar-bg">
            <div class="stat-bar strength" :style="{ width: `${player.character.strength * 10}%` }" />
          </div>
          <span class="stat-val stat-strength">{{ player.character.strength }}</span>
          <button v-if="hasLvlUpPoints" class="lvl-btn" data-sfx-skip-default="true" title="+1 Strength" @click="handleLevelUp(2)">+</button>
        </div>
        <div v-if="isMe" class="resist-row">
          <span class="resist-badge"><span class="gi gi-def">DEF</span> {{ player.character.strengthResist }}</span>
          <span v-if="player.character.strengthBonusText" class="resist-bonus">{{ player.character.strengthBonusText }}</span>
        </div>
      </div>
      <!-- Speed -->
      <div class="stat-block" :class="{ 'lvl-up-available': hasLvlUpPoints }">
        <div class="stat-row">
          <span class="gi gi-lg gi-spd">SPD</span>
          <div class="stat-bar-bg">
            <div class="stat-bar speed" :style="{ width: `${player.character.speed * 10}%` }" />
          </div>
          <span class="stat-val stat-speed">{{ player.character.speed }}</span>
          <button v-if="hasLvlUpPoints" class="lvl-btn" data-sfx-skip-default="true" title="+1 Speed" @click="handleLevelUp(3)">+</button>
        </div>
        <div v-if="isMe" class="resist-row">
          <span class="resist-badge"><span class="gi gi-def">DEF</span> {{ player.character.speedResist }}</span>
          <span v-if="player.character.speedBonusText" class="resist-bonus">{{ player.character.speedBonusText }}</span>
        </div>
      </div>
      </template>
    </div>

    <!-- Psyche (separated — different stat type, hidden during goblin lvl-up) -->
    <div v-if="!(isGoblin && hasLvlUpPoints && goblin)" class="pc-psyche-box">
      <div class="stat-block" :class="{ 'resist-hit': resistFlash.includes('psyche'), 'lvl-up-available': hasLvlUpPoints }">
        <div class="stat-row">
          <span class="gi gi-lg gi-psy">PSY</span>
          <div class="stat-bar-bg">
            <div class="stat-bar psyche" :style="{ width: `${player.character.psyche * 10}%` }" />
          </div>
          <span class="stat-val stat-psyche">{{ player.character.psyche }}</span>
          <button v-if="hasLvlUpPoints" class="lvl-btn" data-sfx-skip-default="true" title="+1 Psyche" @click="handleLevelUp(4)">+</button>
        </div>
        <div v-if="isMe" class="resist-row">
          <span class="resist-badge"><span class="gi gi-def">DEF</span> {{ player.character.psycheResist }}</span>
          <span v-if="player.character.psycheBonusText" class="resist-bonus">{{ player.character.psycheBonusText }}</span>
        </div>
      </div>
    </div>

    <!-- Justice: highlighted, own row -->
    <div class="pc-justice-row" :class="{ 'justice-reset-flash': justiceReset }"
      @mouseenter="showTip($event, 'Justice allows you to win Round 2 and influences Round 1. You gain it when you\'re defeated and it is fully reset on victory')"
      @mousemove="moveTip" @mouseleave="hideTip">
      <span class="justice-icon">⚖</span>
      <span class="justice-label">Justice</span>
      <span class="justice-value">{{ player.character.justice }}</span>
      <span v-if="justiceReset" class="justice-reset-label">RESET</span>
    </div>

    <!-- Moral / Skill / Class / Target -->
    <div class="pc-meta">
      <div class="meta-box"
        @mouseenter="showTip($event, 'You can exchange moral for Skill or Points. Gain it by winning and lose it when you\'re defeated')"
        @mousemove="moveTip" @mouseleave="hideTip">
        <span class="meta-label">Moral</span>
        <span class="meta-value stat-moral">{{ player.character.moralDisplay }}</span>
      </div>

      <div class="meta-box"
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

      <div v-if="isMe && player.character.skillTarget" class="meta-box"
        @mouseenter="showTip($event, skillTargetTooltip(player.character.skillTarget))" @mousemove="moveTip" @mouseleave="hideTip">
        <span class="meta-label">Target</span>
        <span class="meta-value"><span :class="skillTargetBadge(player.character.skillTarget).cls" style="font-size:14px">{{ skillTargetBadge(player.character.skillTarget).label }}</span></span>
      </div>
    </div>

    <!-- Moral exchange -->
    <div v-if="hasMoral" class="pc-moral-actions">
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
        Shinigami Eyes (25)
      </button>
    </div>

    <!-- Portal Gun (Rick special ability) -->
    <div v-if="portalGun" class="pc-special-ability">
      <div class="sa-header">Портальная пушка</div>
      <div v-if="!portalGun.invented" class="sa-status sa-not-invented">
        Не изобретена (INT 30)
      </div>
      <div v-else class="sa-status sa-invented">
        <span class="sa-charge-count">{{ portalGun.charges }}</span>
        <span class="sa-charge-label">charges</span>
      </div>
    </div>

    <!-- Exploit state (Баг special ability) -->
    <div v-if="exploitState" class="pc-exploit-state">
      <div class="exploit-header">
        <span class="exploit-title">EXPLOIT</span>
        <span class="exploit-progress">{{ exploitState.fixedCount }}/{{ exploitState.totalPlayers }}</span>
      </div>
      <div class="exploit-accumulated">
        <span class="exploit-value">{{ exploitState.totalExploit }}</span>
        <span class="exploit-label">pending</span>
      </div>
      <div class="exploit-bar-bg">
        <div class="exploit-bar-fill" :style="{ width: `${exploitState.totalPlayers > 0 ? (exploitState.fixedCount / exploitState.totalPlayers) * 100 : 0}%` }" />
      </div>
    </div>

    <!-- Tsukuyomi state (Itachi special ability) -->
    <div v-if="tsukuyomiState" class="pc-tsukuyomi-state">
      <div class="tsukuyomi-header">
        <span class="tsukuyomi-title">TSUKUYOMI</span>
        <span class="tsukuyomi-charge" :class="{ 'tsukuyomi-ready': tsukuyomiState.isReady }">
          {{ tsukuyomiState.isReady ? 'READY' : `${tsukuyomiState.chargeCounter}/2` }}
        </span>
      </div>
      <div class="tsukuyomi-stolen">
        <span class="tsukuyomi-value">{{ tsukuyomiState.totalStolenPoints }}</span>
        <span class="tsukuyomi-label">stolen</span>
      </div>
      <div class="tsukuyomi-bar-bg">
        <div class="tsukuyomi-bar-fill" :style="{ width: `${(tsukuyomiState.chargeCounter / 2) * 100}%` }" />
      </div>
    </div>

    <!-- ── Passive Ability Widgets ── -->

    <!-- 1. Буль (Drowning) -->
    <div v-if="passiveStates?.bulk" class="pc-passive-widget bulk-widget">
      <div class="pw-header">
        <span class="pw-title bulk-title">DROWNING</span>
      </div>
      <div class="pw-body">
        <div class="bulk-chance-wrap">
          <span class="bulk-chance-value">{{ passiveStates.bulk.drownChance }}%</span>
          <span class="bulk-chance-label">drown chance</span>
          <div class="bulk-wave-bar">
            <div class="bulk-wave-fill" :style="{ width: `${passiveStates.bulk.drownChance}%` }" />
          </div>
        </div>
        <span v-if="passiveStates.bulk.isBuffed" class="bulk-buffed">BUFFED</span>
      </div>
    </div>

    <!-- 2. Я за чаем (Tea Time) -->
    <div v-if="passiveStates?.tea" class="pc-passive-widget tea-widget">
      <div class="pw-header">
        <span class="pw-title tea-title">TEA TIME</span>
        <span class="pw-status" :class="passiveStates.tea.isReady ? 'tea-ready' : 'tea-brewing'">
          {{ passiveStates.tea.isReady ? 'READY' : 'BREWING' }}
        </span>
      </div>
    </div>

    <!-- 3. Еврей (Profit) -->
    <div v-if="passiveStates?.jew" class="pc-passive-widget jew-widget">
      <div class="pw-header">
        <span class="pw-title jew-title">PROFIT</span>
      </div>
      <div class="pw-body">
        <span class="pw-value">{{ passiveStates.jew.stolenPsyche }}</span>
        <span class="pw-label">stolen PSY</span>
      </div>
    </div>

    <!-- 4. HardKitty (Friends) -->
    <div v-if="passiveStates?.hardKitty" class="pc-passive-widget hardkitty-widget">
      <div class="pw-header">
        <span class="pw-title hardkitty-title">FRIENDS</span>
      </div>
      <div class="pw-body">
        <span class="pw-value">{{ passiveStates.hardKitty.friendsCount }}</span>
        <span class="pw-label">friends made</span>
      </div>
    </div>

    <!-- 5. Обучение (Training) -->
    <div v-if="passiveStates?.training" class="pc-passive-widget training-widget">
      <div class="pw-header">
        <span class="pw-title training-title">TRAINING</span>
        <span class="pw-status training-stat">{{ passiveStates.training.statName }}</span>
      </div>
      <div v-if="passiveStates.training.targetStatValue > 0" class="pw-body">
        <span class="pw-value">{{ passiveStates.training.targetStatValue }}</span>
        <span class="pw-label">target</span>
      </div>
      <div v-else class="pw-body">
        <span class="pw-label">waiting for defeat...</span>
      </div>
    </div>

    <!-- 6. Дракон (Dragon) -->
    <div v-if="passiveStates?.dragon" class="pc-passive-widget dragon-widget">
      <div class="pw-header">
        <span class="pw-title dragon-title">DRAGON</span>
        <span class="pw-status" :class="passiveStates.dragon.isAwakened ? 'dragon-awakened' : 'dragon-sleeping'">
          {{ passiveStates.dragon.isAwakened ? 'AWAKENED' : `${passiveStates.dragon.roundsUntilAwaken} rounds` }}
        </span>
      </div>
      <div v-if="!passiveStates.dragon.isAwakened" class="dragon-bar-bg">
        <div class="dragon-bar-fill" :style="{ width: `${((10 - passiveStates.dragon.roundsUntilAwaken) / 10) * 100}%` }" />
      </div>
    </div>

    <!-- 7. Запах мусора (Garbage) -->
    <div v-if="passiveStates?.garbage" class="pc-passive-widget garbage-widget">
      <div class="pw-header">
        <span class="pw-title garbage-title">GARBAGE</span>
        <span class="pw-status garbage-count">{{ passiveStates.garbage.markedCount }}/{{ passiveStates.garbage.totalTracked }}</span>
      </div>
      <div class="garbage-bar-bg">
        <div class="garbage-bar-fill" :style="{ width: `${passiveStates.garbage.totalTracked > 0 ? (passiveStates.garbage.markedCount / passiveStates.garbage.totalTracked) * 100 : 0}%` }" />
      </div>
    </div>

    <!-- 8. Научите играть (Copycat) -->
    <div v-if="passiveStates?.copycat" class="pc-passive-widget copycat-widget">
      <div class="pw-header">
        <span class="pw-title copycat-title">COPYCAT</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.copycat.copiedStatName }}</span>
          <span class="pw-label">current stat</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.copycat.historyCount }}</span>
          <span class="pw-label">copied</span>
        </div>
      </div>
    </div>

    <!-- 9. Чернильная завеса (Ink Screen) -->
    <div v-if="passiveStates?.inkScreen" class="pc-passive-widget ink-widget">
      <div class="pw-header">
        <span class="pw-title ink-title">INK SCREEN</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.inkScreen.fakeDefeatCount }}</span>
          <span class="pw-label">fake defeats</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.inkScreen.totalDeferredScore }}</span>
          <span class="pw-label">deferred pts</span>
        </div>
      </div>
    </div>

    <!-- 10. Тигр топ (Tiger Top) -->
    <div v-if="passiveStates?.tigerTop" class="pc-passive-widget tigertop-widget" :class="{ 'tigertop-active': passiveStates.tigerTop.isActive }">
      <div class="pw-header">
        <span class="pw-title tigertop-title">TIGER TOP</span>
        <span class="pw-status" :class="passiveStates.tigerTop.isActive ? 'tigertop-on' : 'tigertop-off'">
          {{ passiveStates.tigerTop.isActive ? 'ACTIVE' : 'INACTIVE' }}
        </span>
      </div>
      <div class="pw-body">
        <span class="pw-value">{{ passiveStates.tigerTop.swapsRemaining }}</span>
        <span class="pw-label">swaps left</span>
      </div>
    </div>

    <!-- 11. Челюсти (Jaws) -->
    <div v-if="passiveStates?.jaws" class="pc-passive-widget jaws-widget">
      <div class="pw-header">
        <span class="pw-title jaws-title">JAWS</span>
        <svg class="jaws-shark" viewBox="0 0 40 20" :style="{ animationDuration: `${Math.max(0.3, 3 - passiveStates.jaws.currentSpeed * 0.2)}s` }">
          <path d="M2 10 L10 4 L18 8 L22 3 L28 8 L35 6 L38 10 L35 14 L28 12 L22 17 L18 12 L10 16 L2 10 Z" fill="currentColor" />
          <circle cx="32" cy="9" r="1.5" fill="var(--bg-card)" />
        </svg>
      </div>
      <div class="pw-body jaws-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.jaws.currentSpeed }}</span>
          <span class="pw-label">speed</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.jaws.uniqueDefeated }}</span>
          <span class="pw-label">defeated</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.jaws.uniquePositions }}</span>
          <span class="pw-label">positions</span>
        </div>
      </div>
    </div>

    <!-- 12. Привилегия (Privilege) -->
    <div v-if="passiveStates?.privilege" class="pc-passive-widget privilege-widget" :class="{ 'privilege-active': passiveStates.privilege.markedCount > 0 }">
      <div class="pw-header">
        <span class="pw-title privilege-title">PRIVILEGE</span>
      </div>
      <div class="pw-body">
        <span class="pw-value">{{ passiveStates.privilege.markedCount }}</span>
        <span class="pw-label">marked</span>
      </div>
    </div>

    <!-- 13. Вампуризм (Vampirism) -->
    <div v-if="passiveStates?.vampirism" class="pc-passive-widget vampirism-widget">
      <div class="pw-header">
        <span class="pw-title vampirism-title">VAMPIRISM</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.vampirism.activeFeeds }}</span>
          <span class="pw-label">feeds</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.vampirism.ignoredJustice }}</span>
          <span class="pw-label">ignored</span>
        </div>
      </div>
    </div>

    <!-- 14. Weedwick (Weed) -->
    <div v-if="passiveStates?.weed" class="pc-passive-widget weed-widget">
      <div class="pw-header">
        <span class="pw-title weed-title">WEED</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.weed.totalWeedAvailable }}</span>
          <span class="pw-label">available</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.weed.lastHarvestRound }}</span>
          <span class="pw-label">last harvest</span>
        </div>
      </div>
    </div>

    <!-- 15. Сайтама (One Punch) -->
    <div v-if="passiveStates?.saitama" class="pc-passive-widget saitama-widget">
      <div class="pw-header">
        <span class="pw-title saitama-title">ONE PUNCH</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.saitama.deferredPoints }}</span>
          <span class="pw-label">deferred pts</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.saitama.deferredMoral }}</span>
          <span class="pw-label">deferred moral</span>
        </div>
      </div>
    </div>

    <!-- 16. Глаза бога смерти (Shinigami Eyes) -->
    <div v-if="passiveStates?.shinigamiEyes" class="pc-passive-widget shinigami-widget">
      <div class="pw-header">
        <span class="pw-title shinigami-title">SHINIGAMI EYES</span>
        <span class="pw-status" :class="passiveStates.shinigamiEyes.isActive ? 'shinigami-on' : 'shinigami-off'">
          {{ passiveStates.shinigamiEyes.isActive ? 'ACTIVE' : 'INACTIVE' }}
        </span>
      </div>
    </div>

    <!-- 17. Продавец (Seller) -->
    <div v-if="passiveStates?.seller" class="pc-passive-widget seller-widget">
      <div class="pw-header">
        <span class="pw-title seller-title">SELLER</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.seller.cooldown }}</span>
          <span class="pw-label">CD</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.seller.markedCount }}</span>
          <span class="pw-label">marked</span>
        </div>
        <div class="pw-stat-pair">
          <span class="pw-value">{{ Math.round(passiveStates.seller.secretBuildSkill) }}</span>
          <span class="pw-label">secret sk</span>
        </div>
      </div>
    </div>

    <!-- 19. Dopa -->
    <div v-if="passiveStates?.dopa" class="pc-passive-widget dopa-widget">
      <div class="pw-header">
        <span class="pw-title dopa-title">DOPA</span>
        <span v-if="passiveStates.dopa.chosenTactic" class="pw-status dopa-tactic">{{ passiveStates.dopa.chosenTactic }}</span>
      </div>
      <div class="pw-body">
        <div class="pw-stat-pair">
          <span class="pw-value" :class="{ 'dopa-ready': passiveStates.dopa.visionReady }">{{ passiveStates.dopa.visionReady ? 'RDY' : passiveStates.dopa.visionCooldown }}</span>
          <span class="pw-label">vision</span>
        </div>
        <div v-if="passiveStates.dopa.needSecondAttack" class="pw-stat-pair">
          <span class="pw-value dopa-need-atk">2nd</span>
          <span class="pw-label">attack</span>
        </div>
      </div>
    </div>

    <!-- 18. Впарили говна (Seller Mark on marked player) -->
    <div v-if="passiveStates?.sellerMark" class="pc-passive-widget seller-mark-widget">
      <div class="pw-header">
        <span class="pw-title seller-mark-title">ВПАРИЛИ</span>
      </div>
      <div class="pw-body">
        <div v-if="passiveStates.sellerMark.roundsRemaining > 0" class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.sellerMark.roundsRemaining }}</span>
          <span class="pw-label">rounds left</span>
        </div>
        <div v-if="passiveStates.sellerMark.debt" class="pw-stat-pair">
          <span class="pw-value">{{ passiveStates.sellerMark.debt }}</span>
          <span class="pw-label">debt</span>
        </div>
      </div>
    </div>

    <!-- 20. Стая Гоблинов (Goblin Swarm) -->
    <div v-if="passiveStates?.goblinSwarm" class="pc-passive-widget goblin-widget">
      <div class="pw-header">
        <span class="pw-title goblin-title">СТАЯ ГОБЛИНОВ</span>
        <span v-if="passiveStates.goblinSwarm.isInZiggurat" class="pw-status goblin-zig-active">🏛️ ZIGGURAT</span>
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
        <span v-if="passiveStates.goblinSwarm.festivalUsed" class="goblin-festival-used">🎉 Праздник был</span>
      </div>
    </div>

    <!-- 21. Котики (owner widget) -->
    <div v-if="passiveStates?.kotiki" class="pc-passive-widget kotiki-widget">
      <div class="pw-header">
        <span class="pw-title kotiki-title">КОТИКИ</span>
      </div>
      <div class="kotiki-info">
        <div class="kotiki-row">
          <span class="kotiki-label">Провокации:</span>
          <span class="kotiki-val">{{ passiveStates.kotiki.tauntedCount }}/{{ passiveStates.kotiki.tauntedMax }}</span>
        </div>
        <!-- Deployed cats -->
        <div v-if="passiveStates.kotiki.minkaOnPlayerName" class="kotiki-cat-card kotiki-cat-minka">
          <div class="kotiki-cat-header">
            <span class="kotiki-cat-icon">🐱</span>
            <span class="kotiki-cat-name">Минька</span>
            <span class="kotiki-cat-rounds">{{ passiveStates.kotiki.minkaRoundsOnEnemy }} р.</span>
          </div>
          <div class="kotiki-cat-target">на {{ passiveStates.kotiki.minkaOnPlayerName }}</div>
        </div>
        <div v-if="passiveStates.kotiki.stormOnPlayerName" class="kotiki-cat-card kotiki-cat-storm">
          <div class="kotiki-cat-header">
            <span class="kotiki-cat-icon">🐱</span>
            <span class="kotiki-cat-name">Штормяк</span>
          </div>
          <div class="kotiki-cat-target">на {{ passiveStates.kotiki.stormOnPlayerName }}</div>
        </div>
        <!-- Cooldowns -->
        <div v-if="passiveStates.kotiki.minkaCooldown > 0" class="kotiki-row kotiki-cooldown">
          <span class="kotiki-label">Минька откат:</span>
          <span class="kotiki-val">{{ passiveStates.kotiki.minkaCooldown }}</span>
        </div>
        <div v-if="passiveStates.kotiki.stormCooldown > 0" class="kotiki-row kotiki-cooldown">
          <span class="kotiki-label">Штормяк откат:</span>
          <span class="kotiki-val">{{ passiveStates.kotiki.stormCooldown }}</span>
        </div>
      </div>
    </div>

    <!-- 21b. Котики cat-on-me (shown to ANY player who has a cat sitting on them) -->
    <div v-if="passiveStates?.kotikiCatOnMe" class="pc-passive-widget kotiki-cat-on-me-widget">
      <div class="kotiki-cat-card" :class="passiveStates.kotikiCatOnMe.catType === 'Минька' ? 'kotiki-cat-minka' : 'kotiki-cat-storm'">
        <div class="kotiki-cat-header">
          <span class="kotiki-cat-icon">🐱</span>
          <span class="kotiki-cat-name">{{ passiveStates.kotikiCatOnMe.catType }}</span>
          <span v-if="passiveStates.kotikiCatOnMe.roundsDeployed > 0" class="kotiki-cat-rounds">{{ passiveStates.kotikiCatOnMe.roundsDeployed }} р.</span>
        </div>
        <div class="kotiki-cat-target">от {{ passiveStates.kotikiCatOnMe.catOwnerName }}</div>
      </div>
    </div>

    <!-- 22. Монстр без имени (owner widget) -->
    <div v-if="passiveStates?.monster" class="pc-passive-widget monster-widget">
      <div class="pw-header">
        <span class="pw-title monster-title">МОНСТР</span>
      </div>
      <div class="monster-info">
        <div class="monster-row">
          <span class="monster-label">Пешки:</span>
          <span class="monster-val">{{ passiveStates.monster.pawnCount }}</span>
        </div>
      </div>
    </div>

    <!-- 22b. Monster pawn indicator (shown to ANY player who is a Johan pawn) -->
    <div v-if="passiveStates?.monsterPawnOnMe" class="pc-passive-widget monster-pawn-on-me-widget">
      <div class="monster-pawn-card">
        <div class="monster-pawn-header">
          <span class="monster-pawn-icon">♟️</span>
          <span class="monster-pawn-name">Пешка Йохана</span>
        </div>
        <div class="monster-pawn-target">от {{ passiveStates.monsterPawnOnMe.pawnOwnerName }}</div>
      </div>
    </div>

    <!-- Score + animated delta -->
    <div class="pc-score-row">
      <span class="pc-score">{{ player.status.score }}</span>
      <span class="pc-score-label">pts</span>
      <span v-if="animatedScoreDelta !== 0" class="pc-score-delta" :class="{ 'delta-big': comboHits.length >= 4, 'delta-huge': comboHits.length >= 6, 'delta-negative': animatedScoreDelta < 0 }" :key="animatedScoreDelta">
        {{ animatedScoreDelta > 0 ? '+' : '' }}{{ animatedScoreDelta }}
      </span>
    </div>

    <!-- Score combo hits (each + source animated individually) -->
    <div v-if="isMe && comboHits.length > 0" class="pc-combo-feed">
      <div
        v-for="(hit, idx) in comboHits"
        :key="idx"
        class="combo-entry"
        :class="{
          'combo-visible': idx < hitVisibleCount,
          'combo-active': idx === hitActiveIdx,
          'combo-big': hit.comboNum >= 3 && hit.pointsDelta > 0,
          'combo-huge': hit.comboNum >= 5 && hit.pointsDelta > 0,
          'combo-negative': hit.pointsDelta < 0,
        }"
      >
        <span class="combo-hit-pts" :class="{ 'combo-hit-negative': hit.pointsDelta < 0 }">{{ hit.pointsDelta > 0 ? '+' : '' }}{{ hit.pointsDelta }}</span>
        <span class="combo-hit-label">{{ hit.label }}</span>
        <span v-if="hit.comboNum >= 2" class="combo-badge" :class="{ 'combo-badge-big': hit.comboNum >= 3, 'combo-badge-huge': hit.comboNum >= 5 }">
          x{{ hit.comboNum }}
        </span>
      </div>
      <div v-if="comboHits.length >= 3 && hitVisibleCount >= comboHits.length" class="combo-total" :class="{ 'combo-total-big': comboHits.length >= 5, 'combo-total-huge': comboHits.length >= 8 }">
        {{ comboHits.length }} Combo!
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
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  padding: 14px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.player-card.is-me {
  border-color: var(--accent-gold-dim);
  box-shadow: var(--glow-gold);
}

/* Avatar */
.pc-avatar-wrap {
  width: 100%;
  aspect-ratio: 1;
  border-radius: var(--radius);
  overflow: hidden;
  background: var(--bg-inset);
  border: 1px solid var(--border-subtle);
}

.pc-avatar-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
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
}

.pc-name {
  font-weight: 800;
  font-size: 16px;
  color: var(--accent-gold);
  letter-spacing: 0.3px;
}

.pc-username {
  font-size: 11px;
  color: var(--text-muted);
  font-family: var(--font-mono);
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
  height: 5px;
  background: var(--bg-inset);
  border-radius: 3px;
  overflow: hidden;
}

.stat-bar {
  height: 100%;
  border-radius: 3px;
  transition: width 0.5s ease;
}

.stat-bar.intelligence { background: linear-gradient(90deg, var(--kh-c-secondary-info-400), var(--kh-c-secondary-info-200)); }
.stat-bar.strength { background: linear-gradient(90deg, var(--kh-c-secondary-danger-400), var(--kh-c-secondary-danger-200)); }
.stat-bar.speed { background: linear-gradient(90deg, var(--kh-c-secondary-warning-400), var(--kh-c-secondary-warning-200)); }
.stat-bar.psyche { background: linear-gradient(90deg, var(--kh-c-secondary-purple-400), var(--kh-c-secondary-purple-200)); }

.stat-val {
  width: 20px;
  text-align: right;
  font-family: var(--font-mono);
  font-weight: 700;
  font-size: 12px;
  color: var(--text-primary);
}

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

@keyframes resist-hit-flash {
  0% { background: rgba(239, 128, 128, 0.3); box-shadow: inset 0 0 12px rgba(239, 128, 128, 0.4); }
  30% { background: rgba(239, 128, 128, 0.15); box-shadow: inset 0 0 6px rgba(239, 128, 128, 0.2); }
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

.lvl-btn {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  border: 1.5px solid var(--accent-green);
  background: rgba(63, 167, 61, 0.08);
  color: var(--accent-green);
  font-size: 13px;
  font-weight: 900;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  line-height: 1;
  transition: all 0.15s;
  flex-shrink: 0;
}
.lvl-btn:hover {
  background: var(--accent-green);
  color: var(--bg-primary);
  box-shadow: var(--glow-green);
}

/* Justice highlight row */
.pc-justice-row {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 6px 8px;
  background: linear-gradient(135deg, rgba(139, 92, 246, 0.08), rgba(180, 150, 255, 0.04));
  border: 1.5px solid rgba(139, 92, 246, 0.25);
  border-radius: var(--radius);
  position: relative;
  transition: border-color 0.3s, box-shadow 0.3s;
}
.pc-justice-row:hover {
  border-color: rgba(139, 92, 246, 0.4);
  box-shadow: 0 0 8px rgba(139, 92, 246, 0.15);
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
  padding: 5px 3px;
  background: var(--bg-inset);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius);
  cursor: default;
  transition: border-color 0.15s;
}

.meta-box:hover {
  border-color: var(--border-color);
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
  background: rgba(230, 148, 74, 0.06);
  color: var(--accent-orange);
  font-size: 10px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.15s;
  text-align: center;
  min-width: 0;
  white-space: nowrap;
}
.moral-btn:hover:not(:disabled) {
  background: var(--accent-orange);
  color: var(--bg-primary);
  border-color: var(--accent-orange);
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
}

.pc-score {
  font-size: 26px;
  font-weight: 900;
  color: var(--accent-gold);
  font-family: var(--font-mono);
}

.pc-score-label {
  font-size: 11px;
  color: var(--text-muted);
  font-weight: 600;
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
  0% { transform: scale(0.6); opacity: 0.3; }
  50% { transform: scale(1.3); }
  100% { transform: scale(1); opacity: 1; }
}

.pc-place {
  font-size: 13px;
  font-weight: 700;
  color: var(--text-muted);
  margin-left: 6px;
  font-family: var(--font-mono);
}

/* ── Score combo feed ── */
.pc-combo-feed {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 2px 0;
}

.combo-entry {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  font-size: 10px;
  font-weight: 700;
  color: var(--accent-gold);
  padding: 2px 6px;
  border-radius: 3px;
  background: rgba(233, 219, 61, 0.04);
  border: 1px solid rgba(233, 219, 61, 0.1);
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
  background: rgba(233, 219, 61, 0.1);
  border-color: rgba(233, 219, 61, 0.3);
}

.combo-entry.combo-big.combo-active {
  animation: combo-pop-big 0.5s ease;
  background: rgba(230, 148, 74, 0.12);
  border-color: rgba(230, 148, 74, 0.4);
  color: var(--accent-orange);
}

.combo-entry.combo-huge.combo-active {
  animation: combo-pop-huge 0.6s ease;
  background: rgba(239, 128, 128, 0.12);
  border-color: rgba(239, 128, 128, 0.4);
  color: var(--accent-red);
  font-size: 11px;
}

.combo-hit-pts {
  font-weight: 900;
  font-family: var(--font-mono);
  color: var(--accent-green);
  font-size: 11px;
  min-width: 24px;
}
.combo-entry.combo-big .combo-hit-pts { color: var(--accent-orange); }
.combo-entry.combo-huge .combo-hit-pts { color: var(--accent-red); }
.combo-hit-negative { color: var(--accent-red) !important; }

.combo-entry.combo-negative {
  background: rgba(239, 128, 128, 0.04);
  border-color: rgba(239, 128, 128, 0.15);
}
.combo-entry.combo-negative.combo-active {
  background: rgba(239, 128, 128, 0.1);
  border-color: rgba(239, 128, 128, 0.3);
}

.combo-hit-label {
  flex: 1;
  text-align: center;
  font-size: 10px;
  color: var(--text-secondary);
}

.combo-badge {
  font-size: 9px;
  font-weight: 900;
  padding: 1px 5px;
  border-radius: 3px;
  background: rgba(233, 219, 61, 0.15);
  color: var(--accent-gold);
  letter-spacing: 0.3px;
}
.combo-badge-big {
  background: rgba(230, 148, 74, 0.2);
  color: var(--accent-orange);
}
.combo-badge-huge {
  background: rgba(239, 128, 128, 0.2);
  color: var(--accent-red);
  font-size: 10px;
}

.combo-total {
  text-align: center;
  font-size: 10px;
  font-weight: 900;
  color: var(--accent-gold);
  padding: 2px 0;
  letter-spacing: 0.5px;
  text-transform: uppercase;
  animation: combo-total-in 0.5s ease;
}
.combo-total-big {
  color: var(--accent-orange);
  font-size: 11px;
  text-shadow: 0 0 8px rgba(230, 148, 74, 0.3);
}
.combo-total-huge {
  color: var(--accent-red);
  font-size: 12px;
  text-shadow: 0 0 12px rgba(239, 128, 128, 0.4);
  animation: combo-total-in 0.5s ease, combo-glow 1s ease-in-out 0.5s 2;
}

@keyframes combo-pop {
  0% { transform: translateY(6px) scale(0.8); opacity: 0; }
  50% { transform: translateY(-2px) scale(1.05); }
  100% { transform: translateY(0) scale(1); opacity: 1; }
}
@keyframes combo-pop-big {
  0% { transform: translateY(8px) scale(0.7); opacity: 0; }
  40% { transform: translateY(-4px) scale(1.12); }
  70% { transform: translateY(1px) scale(0.98); }
  100% { transform: translateY(0) scale(1); opacity: 1; }
}
@keyframes combo-pop-huge {
  0% { transform: translateY(10px) scale(0.6); opacity: 0; }
  30% { transform: translateY(-6px) scale(1.2); }
  60% { transform: translateY(2px) scale(0.95); }
  100% { transform: translateY(0) scale(1); opacity: 1; }
}
@keyframes combo-total-in {
  0% { opacity: 0; transform: scale(0.5); }
  60% { transform: scale(1.15); }
  100% { opacity: 1; transform: scale(1); }
}
@keyframes combo-glow {
  0%, 100% { text-shadow: 0 0 12px rgba(239, 128, 128, 0.4); }
  50% { text-shadow: 0 0 20px rgba(239, 128, 128, 0.8), 0 0 40px rgba(239, 128, 128, 0.3); }
}

/* PSY separated box */
.pc-psyche-box {
  border: 1px solid rgba(232, 121, 249, 0.12);
  border-radius: var(--radius);
  padding: 4px 6px;
  background: rgba(232, 121, 249, 0.03);
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

/* 18. Впарили говна (Seller Mark) */
.seller-mark-widget {
  background: linear-gradient(135deg, rgba(139, 69, 19, 0.08), rgba(139, 69, 19, 0.02));
  border-color: rgba(139, 69, 19, 0.25);
}
.seller-mark-title { color: #8b4513; text-shadow: 0 0 4px rgba(139, 69, 19, 0.4); }
.seller-mark-widget .pw-value { color: #8b4513; text-shadow: 0 0 8px rgba(139, 69, 19, 0.4); }
.seller-mark-widget .pw-label { color: rgba(139, 69, 19, 0.5); }

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

/* Goblin level-up panel */
.goblin-lvlup {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 4px 0;
}
.goblin-lvlup-btn {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 1px;
  padding: 6px 10px;
  background: linear-gradient(135deg, rgba(76, 153, 0, 0.12), rgba(76, 153, 0, 0.04));
  border: 1px solid rgba(76, 153, 0, 0.3);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  text-align: left;
  color: inherit;
  font-family: inherit;
}
.goblin-lvlup-btn:hover {
  background: linear-gradient(135deg, rgba(76, 153, 0, 0.25), rgba(76, 153, 0, 0.1));
  border-color: rgba(76, 153, 0, 0.6);
  box-shadow: 0 0 8px rgba(76, 153, 0, 0.3);
}
.goblin-lvlup-btn:active {
  transform: scale(0.98);
}
.goblin-lvlup-name {
  font-size: 11px;
  font-weight: 700;
  color: #4c9900;
  text-shadow: 0 0 4px rgba(76, 153, 0, 0.3);
}
.goblin-lvlup-desc {
  font-size: 10px;
  color: rgba(255, 255, 255, 0.6);
}
.goblin-lvlup-disabled {
  opacity: 0.4;
  cursor: not-allowed;
  pointer-events: none;
}
.goblin-lvlup-disabled .goblin-lvlup-name {
  color: rgba(76, 153, 0, 0.4);
}

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

/* 21b. Cat-on-me widget (non-Котики players) */
.kotiki-cat-on-me-widget {
  background: linear-gradient(135deg, rgba(255, 165, 0, 0.06), rgba(255, 165, 0, 0.01));
  border-color: rgba(255, 165, 0, 0.2);
}

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

/* 22b. Monster pawn-on-me widget */
.monster-pawn-on-me-widget {
  background: linear-gradient(135deg, rgba(100, 0, 0, 0.08), rgba(100, 0, 0, 0.02));
  border-color: rgba(180, 0, 0, 0.2);
}
.monster-pawn-card {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 4px 8px;
  border-radius: 6px;
  border: 1px solid rgba(180, 0, 0, 0.3);
  background: linear-gradient(135deg, rgba(180, 0, 0, 0.1), rgba(180, 0, 0, 0.03));
}
.monster-pawn-header { display: flex; align-items: center; gap: 4px; }
.monster-pawn-icon { font-size: 14px; }
.monster-pawn-name { font-size: 11px; font-weight: 700; color: #cc3333; }
.monster-pawn-target { font-size: 10px; color: rgba(255, 255, 255, 0.6); }
</style>

<!-- Tooltip needs to be unscoped to work with Teleport to body -->
<style>
.pc-tooltip {
  position: fixed;
  z-index: 9999;
  pointer-events: none;
  transform: translate(12px, -100%);
  max-width: 240px;
  padding: 6px 10px;
  font-size: 11px;
  font-weight: 600;
  line-height: 1.4;
  color: var(--text-primary);
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: 5px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.5);
  animation: pc-tip-in 0.1s ease-out;
}
@keyframes pc-tip-in {
  from { opacity: 0; transform: translate(12px, -100%) scale(0.95); }
  to { opacity: 1; transform: translate(12px, -100%) scale(1); }
}
</style>
