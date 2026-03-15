<script setup lang="ts">
import { computed, ref, watch, nextTick } from 'vue'
import type { FightEntry, ForOneFightMod } from 'src/services/signalr'
import WaxSeal from './WaxSeal.vue'
import './parchment.css'

const props = defineProps<{
  fight: FightEntry
  isMyFight: boolean
  isFlipped: boolean
  leftName: string
  leftCharName: string
  leftAvatar: string
  rightName: string
  rightCharName: string
  rightAvatar: string
  leftWon: boolean
  attackedRight: boolean
  sign: number
  // Visibility state
  currentStep: number
  skippedToEnd: boolean
  showR1Result: boolean
  showR2: boolean
  showR3: boolean
  showFinalResult: boolean
  showDetails: boolean
  // Phase results
  phase1Result: number
  phase2Result: number
  phase3Result: number
  phase2Revealed: boolean
  phase3Revealed: boolean
  // Weighing
  barPosition: number
  animatedWeighingValue: number
  // Justice (Slam)
  ourJustice: number
  enemyJustice: number
  slamPhase: 'idle' | 'rush' | 'impact' | 'resolved'
  justiceWinner: 'left' | 'right' | 'tie'
  // R3 (Needle)
  r3OurChance: number
  r3DisplayChance: number
  r3RollPct: number
  r3WeWon: boolean
  r3Overflow: boolean
  r3Underflow: boolean
  r3JusticePct: number
  r3NemesisPct: number
  r3NeedlePos: number
  r3NeedleSettled: boolean
  // Factors
  round1Factors: { label: string; detail: string; value: number; highlight: 'good' | 'bad' | 'neutral'; badge?: string; showValue?: boolean; tier?: number }[]
  // Badges
  ourSkillMultiplier: number
  ourForOneFightMods: ForOneFightMod[]
  enemyModsOnUs: ForOneFightMod[]
  classChangeBadge: string
  ourMoralChange: number
  // Result glow
  fightResult: 'win' | 'loss' | null
  fightShake: boolean
  isPortalSwap: boolean
  // Display helpers
  getDisplayAvatar: (orig: string, u: string) => string
  getDisplayCharName: (orig: string, u: string) => string
  fofBadgeText: (mod: ForOneFightMod) => string
  isFofBuff: (mod: ForOneFightMod) => boolean
  enemyFofBadgeText: (mod: ForOneFightMod) => string
  phaseClass: (result: number, revealed: boolean) => string
}>()

const isSpecialOutcome = computed(() =>
  props.fight.outcome === 'block' || props.fight.outcome === 'skip'
)

// Class color for character card border
function classColor(charClass: string): string {
  switch (charClass) {
    case 'Интеллект': return 'class-int'
    case 'Сила': return 'class-str'
    case 'Скорость': return 'class-spd'
    default: return ''
  }
}

const leftClass = computed(() => {
  if (!props.fight) return ''
  return props.isFlipped ? props.fight.defenderClass : props.fight.attackerClass
})
const rightClass = computed(() => {
  if (!props.fight) return ''
  return props.isFlipped ? props.fight.attackerClass : props.fight.defenderClass
})

function showR1Factor(idx: number): boolean {
  if (props.skippedToEnd || !props.isMyFight) return true
  return props.currentStep > idx
}

function fmtPct(v: number): string {
  return (v > 0 ? '+' : '') + v.toFixed(2) + '%'
}
function r3ModClass(v: number): string {
  return v > 0 ? 'pct-good' : v < 0 ? 'pct-bad' : ''
}

// Dynamic drop distance: card falls to wheel or justice level
const arenaRef = ref<HTMLElement | null>(null)
const dropDistance = ref(200)

watch(() => props.showFinalResult, async (show) => {
  if (!show || props.fight.drops === 0) return
  await nextTick()
  const arena = arenaRef.value
  if (!arena) return

  const loserSide = props.leftWon ? '.char-card-right' : '.char-card-left'
  const card = arena.querySelector(loserSide) as HTMLElement | null
  if (!card) return

  const r3Section = arena.querySelector('.fa-r3-details') as HTMLElement | null
  const justice = arena.querySelector('.justice-slam') as HTMLElement | null
  const target = r3Section || justice

  const cardRect = card.getBoundingClientRect()
  if (target) {
    const targetRect = target.getBoundingClientRect()
    // Align card bottom with target bottom
    dropDistance.value = Math.max(50, targetRect.bottom - cardRect.bottom)
  } else {
    const arenaRect = arena.getBoundingClientRect()
    dropDistance.value = Math.max(50, arenaRect.bottom - cardRect.top)
  }
})

function outcomeLabel(f: FightEntry): string {
  switch (f.outcome) {
    case 'block': return 'BLOCK'
    case 'skip': return 'SKIP'
    case 'win': case 'loss': return `${f.winnerName} WINS`
    default: return ''
  }
}
function outcomeClass(f: FightEntry): string {
  if (f.outcome === 'block' || f.outcome === 'skip') return 'outcome-neutral'
  return f.outcome === 'win' ? 'outcome-attacker' : 'outcome-defender'
}
</script>

<template>
  <div ref="arenaRef" class="arena" :class="{
    'arena-shake': props.fightShake,
    'arena-result-win': props.fightResult === 'win',
    'arena-result-loss': props.fightResult === 'loss',
  }" :style="{ '--drop-distance': dropDistance + 'px' }">
    <!-- ═══ BLOCK/SKIP ═══ -->
    <div v-if="isSpecialOutcome" class="arena-special">
      <div class="arena-grid">
        <!-- Left character card -->
        <div class="char-card char-card-left" :class="classColor(leftClass)">
          <img :src="props.getDisplayAvatar(props.leftAvatar, props.leftName)" class="char-avatar"
            @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
          <div class="char-info">
            <span class="char-player-name">{{ props.leftName }}</span>
          </div>
        </div>

        <!-- Center: outcome -->
        <div class="arena-center">
          <div class="phase-tracker-center">
            <div class="phase-role-pip" :class="props.attackedRight ? 'role-atk' : 'role-def'">
              <span class="phase-role-text">{{ props.attackedRight ? 'ATK' : 'DEF' }}</span>
            </div>
            <div class="phase-connector revealed"></div>
            <div class="phase-outcome-pip" :class="outcomeClass(props.fight)">
              <span class="phase-outcome-text">{{ outcomeLabel(props.fight) }}</span>
            </div>
          </div>
          <!-- Shield overlay for block -->
          <div v-if="props.fight.outcome === 'block'" class="block-shield">&#x1F6E1;</div>
        </div>

        <!-- Right character card -->
        <div class="char-card char-card-right" :class="classColor(rightClass)">
          <img :src="props.getDisplayAvatar(props.rightAvatar, props.rightName)" class="char-avatar"
            @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
          <div class="char-info">
            <span class="char-player-name">{{ props.rightName }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══ NORMAL FIGHT ═══ -->
    <template v-else>
      <div class="arena-grid">
        <!-- Left character card -->
        <div class="char-card char-card-left" :class="[
          classColor(leftClass),
          {
            winner: props.showFinalResult && props.leftWon,
            loser: props.showFinalResult && !props.leftWon,
            dropped: props.showFinalResult && !props.leftWon && props.fight.drops > 0,
            'entrance-active': props.currentStep === 0 && !props.skippedToEnd,
          }
        ]">
          <div class="char-class-badge" :class="classColor(leftClass)">
            {{ leftClass === 'Интеллект' ? 'INT' : leftClass === 'Сила' ? 'STR' : leftClass === 'Скорость' ? 'SPD' : '?' }}
          </div>
          <img :src="props.getDisplayAvatar(props.leftAvatar, props.leftName)" class="char-avatar"
            @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
          <div class="char-info">
            <span class="char-name">{{ props.getDisplayCharName(props.leftCharName, props.leftName) }}</span>
            <span class="char-player-name">{{ props.leftName }}</span>
          </div>
          <!-- Our results -->
          <div v-if="props.showFinalResult && props.isMyFight" class="card-results">
            <span v-if="!props.isFlipped && props.fight.skillGainedFromTarget > 0" class="card-result-item detail-skill">+{{ props.fight.skillGainedFromTarget }} Skill</span>
            <span v-if="!props.isFlipped && props.fight.skillGainedFromClassAttacker > 0" class="card-result-item detail-skill">+{{ props.fight.skillGainedFromClassAttacker }} Skill</span>
            <span v-if="props.isFlipped && props.fight.skillGainedFromClassDefender > 0" class="card-result-item detail-skill">+{{ props.fight.skillGainedFromClassDefender }} Skill</span>
            <span v-if="props.ourMoralChange !== 0" class="card-result-item detail-moral">{{ props.ourMoralChange > 0 ? '+' : '' }}{{ props.ourMoralChange }} Moral</span>
            <span v-if="!props.leftWon && props.fight.justiceChange > 0" class="card-result-item detail-justice">+{{ props.fight.justiceChange }} Justice</span>
            <template v-if="!props.leftWon && props.fight.qualityDamageApplied">
              <span v-if="props.fight.resistIntelDamage > 0" class="card-result-item detail-resist">INT -{{ props.fight.resistIntelDamage }}</span>
              <span v-if="props.fight.resistStrDamage > 0" class="card-result-item detail-resist">STR -{{ props.fight.resistStrDamage }}</span>
              <span v-if="props.fight.resistPsycheDamage > 0" class="card-result-item detail-resist">PSY -{{ props.fight.resistPsycheDamage }}</span>
            </template>
            <span v-if="!props.leftWon && props.fight.intellectualDamage" class="card-result-item detail-dmg-us">INT Damage!</span>
            <span v-if="!props.leftWon && props.fight.emotionalDamage" class="card-result-item detail-dmg-us">PSY Damage!</span>
            <span v-if="!props.leftWon && props.fight.drops > 0" class="card-result-item detail-drop">DROP x{{ props.fight.drops }}!</span>
          </div>
        </div>

        <!-- Center zone -->
        <div class="arena-center">
          <!-- Phase tracker -->
          <div v-if="props.isMyFight" class="phase-tracker-center">
            <div class="phase-role-pip" :class="props.attackedRight ? 'role-atk' : 'role-def'">
              <span class="phase-role-text">{{ props.attackedRight ? 'ATK' : 'DEF' }}</span>
            </div>
            <div class="phase-connector revealed"></div>
            <div class="phase-pip" :class="props.phaseClass(props.phase1Result, props.showR1Result)">
              <span v-if="!props.showR1Result" class="phase-icon phase-icon-pending">?</span>
              <span v-else-if="props.phase1Result > 0" class="phase-icon phase-icon-win">&#x2713;</span>
              <span v-else-if="props.phase1Result < 0" class="phase-icon phase-icon-lose">&#x2717;</span>
              <span v-else class="phase-icon phase-icon-draw">&mdash;</span>
            </div>
            <div class="phase-connector" :class="{ revealed: props.showR2 }"></div>
            <div class="phase-pip" :class="props.phaseClass(props.phase2Result, props.phase2Revealed)">
              <span v-if="!props.phase2Revealed" class="phase-icon phase-icon-pending">?</span>
              <span v-else-if="props.phase2Result > 0" class="phase-icon phase-icon-win">&#x2713;</span>
              <span v-else-if="props.phase2Result < 0" class="phase-icon phase-icon-lose">&#x2717;</span>
              <span v-else class="phase-icon phase-icon-draw">&mdash;</span>
            </div>
            <div class="phase-connector" :class="{ revealed: props.fight.usedRandomRoll && props.showR3, broken: !props.fight.usedRandomRoll }"></div>
            <div class="phase-pip" :class="[props.phaseClass(props.phase3Result, props.fight.usedRandomRoll ? props.phase3Revealed : false), { 'phase-skipped': !props.fight.usedRandomRoll }]">
              <template v-if="!props.fight.usedRandomRoll">
                <span class="phase-icon phase-icon-skip">&mdash;</span>
              </template>
              <template v-else>
                <span v-if="!props.phase3Revealed" class="phase-icon phase-icon-pending">?</span>
                <span v-else-if="props.phase3Result > 0" class="phase-icon phase-icon-win">&#x2713;</span>
                <span v-else class="phase-icon phase-icon-lose">&#x2717;</span>
              </template>
            </div>
            <div class="phase-connector phase-connector-outcome" :class="{ revealed: props.showFinalResult }"></div>
            <div v-if="props.showFinalResult" class="phase-outcome-pip" :class="props.leftWon ? 'phase-ours' : 'phase-theirs'">
              <span class="phase-outcome-text">{{ props.leftWon ? 'Victory' : 'Defeat' }}</span>
            </div>
            <div v-else class="phase-outcome-pip phase-outcome-pending">
              <span class="phase-outcome-text">?</span>
            </div>
          </div>

          <!-- Tug-of-war bar (fixed at top, never shifts) -->
          <div class="tug-of-war">
            <div class="tug-track">
              <div class="tug-fill" :style="{ width: props.barPosition + '%' }" :class="{
                'tug-good': props.animatedWeighingValue > 0,
                'tug-bad': props.animatedWeighingValue < 0,
                'tug-even': props.animatedWeighingValue === 0,
              }">
                <div class="tug-particles"></div>
              </div>
            </div>
          </div>

          <!-- R1 Factor cards (horizontal) -->
          <div v-if="props.isMyFight" class="modifier-zone">
            <div class="mod-cards-grid">
              <div
                v-for="(factor, idx) in props.round1Factors"
                :key="'r1-' + idx"
                class="mod-card"
                :class="[
                  factor.highlight,
                  { visible: showR1Factor(idx) },
                  factor.tier != null ? 'tier-' + factor.tier : '',
                ]"
                :style="{
                  '--tilt': Math.sin(idx * 2.7 + 0.5) * 2.5 + 'deg',
                  '--nudge': Math.cos(idx * 3.1 + 0.8) * 2 + 'px',
                }"
              >
                <div class="mod-card-header">
                  <span class="mod-card-label">{{ factor.label }}</span>
                  <span
                    v-if="factor.value !== 0 && props.showDetails"
                    class="mod-card-value"
                  >{{ (factor.value > 0 ? '+' : '') + factor.value.toFixed(1) }}</span>
                </div>
                <div class="mod-card-detail" v-html="factor.detail"></div>
              </div>
            </div>

            <!-- Enchantment badges -->
            <div v-if="(props.fight.isTooGoodMe || props.fight.isTooGoodEnemy) || (props.fight.isTooStronkMe || props.fight.isTooStronkEnemy) || props.ourSkillMultiplier > 1 || props.ourForOneFightMods.length > 0 || props.enemyModsOnUs.length > 0 || props.classChangeBadge" class="enchantment-row" :class="{ visible: props.showR1Result }">
              <span v-if="props.fight.isTooGoodMe || props.fight.isTooGoodEnemy" class="enchant-badge" :class="(props.fight.isTooGoodMe ? !props.isFlipped : props.isFlipped) ? 'badge-buff-ours' : 'badge-buff-theirs'">
                <span class="badge-arrow">{{ (props.fight.isTooGoodMe ? !props.isFlipped : props.isFlipped) ? '&#x2B06;' : '&#x2B07;' }}</span>
                TOO GOOD
              </span>
              <span v-if="props.fight.isTooStronkMe || props.fight.isTooStronkEnemy" class="enchant-badge" :class="(props.fight.isTooStronkMe ? !props.isFlipped : props.isFlipped) ? 'badge-buff-ours' : 'badge-buff-theirs'">
                <span class="badge-arrow">{{ (props.fight.isTooStronkMe ? !props.isFlipped : props.isFlipped) ? '&#x2B06;' : '&#x2B07;' }}</span>
                TOO STRONK
              </span>
              <span v-if="props.ourSkillMultiplier > 1" class="enchant-badge badge-buff-ours">
                <span class="badge-arrow">&#x2B06;</span>
                SKILL x{{ props.ourSkillMultiplier }}
              </span>
              <span v-for="(mod, i) in props.ourForOneFightMods" :key="'fof-'+i"
                class="enchant-badge" :class="props.isFofBuff(mod) ? 'badge-buff-ours' : 'badge-buff-theirs'">
                <span class="badge-arrow">{{ props.isFofBuff(mod) ? '&#x2B06;' : '&#x2B07;' }}</span>
                {{ props.fofBadgeText(mod) }}
              </span>
              <span v-for="(mod, i) in props.enemyModsOnUs" :key="'fof-enemy-'+i"
                class="enchant-badge badge-buff-theirs">
                <span class="badge-arrow">&#x2B07;</span>
                {{ props.enemyFofBadgeText(mod) }}
              </span>
              <span v-if="props.classChangeBadge" class="enchant-badge badge-class-change">{{ props.classChangeBadge }}</span>
            </div>
          </div>
        </div>

        <!-- Right character card -->
        <div class="char-card char-card-right" :class="[
          classColor(rightClass),
          {
            winner: props.showFinalResult && !props.leftWon && (props.fight.outcome === 'win' || props.fight.outcome === 'loss'),
            loser: props.showFinalResult && props.leftWon,
            dropped: props.showFinalResult && props.leftWon && props.fight.drops > 0,
            'entrance-active': props.currentStep === 0 && !props.skippedToEnd,
          }
        ]">
          <div class="char-class-badge" :class="classColor(rightClass)">
            {{ rightClass === 'Интеллект' ? 'INT' : rightClass === 'Сила' ? 'STR' : rightClass === 'Скорость' ? 'SPD' : '?' }}
          </div>
          <img :src="props.getDisplayAvatar(props.rightAvatar, props.rightName)" class="char-avatar"
            @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
          <div class="char-info">
            <span class="char-name">{{ props.getDisplayCharName(props.rightCharName, props.rightName) }}</span>
            <span class="char-player-name">{{ props.rightName }}</span>
          </div>
          <!-- Enemy results -->
          <div v-if="props.showFinalResult && props.isMyFight" class="card-results">
            <span v-if="props.leftWon && props.fight.intellectualDamage" class="card-result-item detail-dmg-enemy">INT Damage!</span>
            <span v-if="props.leftWon && props.fight.emotionalDamage" class="card-result-item detail-dmg-enemy">PSY Damage!</span>
            <span v-if="props.leftWon && props.fight.drops > 0" class="card-result-item detail-drop-enemy">DROP x{{ props.fight.drops }}!</span>
          </div>
        </div>
      </div>

      <!-- Justice Slam (Phase 2) -->
      <div v-if="props.isMyFight && props.showR2" class="justice-slam" :class="{ 'slam-impact': props.slamPhase === 'impact' }">
        <div class="justice-card justice-left" :class="{
          'j-rush': props.slamPhase === 'rush',
          'j-winner': props.slamPhase === 'resolved' && props.justiceWinner === 'left',
          'j-loser': props.slamPhase === 'resolved' && props.justiceWinner === 'right',
          'j-tied': props.slamPhase === 'resolved' && props.justiceWinner === 'tie',
        }">
          <span class="justice-label">Justice</span>
          <span class="justice-value">{{ props.ourJustice }}</span>
          <WaxSeal v-if="props.slamPhase === 'resolved' && props.justiceWinner === 'left'" type="victory" :size="20" class="justice-seal" />
        </div>
        <div class="justice-vs" :class="{ visible: props.slamPhase === 'impact' || props.slamPhase === 'resolved' }">
          <span v-if="props.slamPhase === 'impact'" class="justice-spark">&#x2696;</span>
          <span v-else>vs</span>
        </div>
        <div class="justice-card justice-right" :class="{
          'j-rush': props.slamPhase === 'rush',
          'j-winner': props.slamPhase === 'resolved' && props.justiceWinner === 'right',
          'j-loser': props.slamPhase === 'resolved' && props.justiceWinner === 'left',
          'j-tied': props.slamPhase === 'resolved' && props.justiceWinner === 'tie',
        }">
          <span class="justice-label">Justice</span>
          <span class="justice-value">{{ props.enemyJustice }}</span>
          <WaxSeal v-if="props.slamPhase === 'resolved' && props.justiceWinner === 'right'" type="victory" :size="20" class="justice-seal" />
        </div>
        <!-- Impact particles -->
        <div v-if="props.slamPhase === 'impact' || props.slamPhase === 'resolved'" class="slam-particles">
          <span v-for="i in 6" :key="i" class="slam-dust" :style="{
            '--dx': (i % 2 === 0 ? 1 : -1) * (6 + i * 3) + 'px',
            '--delay': (i * 25) + 'ms',
          }"></span>
        </div>
      </div>

      <!-- R3 Random Roll (Needle) -->
      <template v-if="props.isMyFight && props.fight.usedRandomRoll">
        <div v-if="props.showR3" class="fa-r3-details">
          <!-- R3 modifier cards (horizontal grid, same style as R1) -->
          <div class="mod-cards-grid">
            <div v-if="props.fight.tooGoodRandomChange !== 0"
              class="mod-card visible"
              :class="props.fight.tooGoodRandomChange * props.sign > 0 ? 'good' : 'bad'">
              <div class="mod-card-header">
                <span class="mod-card-label">TooGood</span>
              </div>
              <div class="mod-card-detail" :class="r3ModClass(props.fight.tooGoodRandomChange * props.sign)">
                {{ fmtPct(props.fight.tooGoodRandomChange * props.sign) }}
              </div>
            </div>
            <div v-if="props.fight.tooStronkRandomChange !== 0"
              class="mod-card visible"
              :class="props.fight.tooStronkRandomChange * props.sign > 0 ? 'good' : 'bad'">
              <div class="mod-card-header">
                <span class="mod-card-label">TooStronk</span>
              </div>
              <div class="mod-card-detail" :class="r3ModClass(props.fight.tooStronkRandomChange * props.sign)">
                {{ fmtPct(props.fight.tooStronkRandomChange * props.sign) }}
              </div>
            </div>
            <div v-if="props.fight.justiceRandomChange !== 0"
              class="mod-card visible"
              :class="props.r3JusticePct > 0 ? 'good' : props.r3JusticePct < 0 ? 'bad' : 'neutral'">
              <div class="mod-card-header">
                <span class="mod-card-label">Justice</span>
              </div>
              <div class="mod-card-detail" :class="r3ModClass(props.r3JusticePct)">
                {{ fmtPct(props.r3JusticePct) }}
              </div>
            </div>
            <div v-if="props.fight.nemesisRandomChange !== 0"
              class="mod-card visible"
              :class="props.r3NemesisPct > 0 ? 'good' : props.r3NemesisPct < 0 ? 'bad' : 'neutral'">
              <div class="mod-card-header">
                <span class="mod-card-label">Nemesis</span>
              </div>
              <div class="mod-card-detail" :class="r3ModClass(props.r3NemesisPct)">
                {{ fmtPct(props.r3NemesisPct) }}
              </div>
            </div>
            <div v-if="props.fight.skillDifferenceRandomModifier !== 0"
              class="mod-card visible"
              :class="props.fight.skillDifferenceRandomModifier * props.sign > 0 ? 'good' : 'bad'">
              <div class="mod-card-header">
                <span class="mod-card-label">Skill Diff</span>
              </div>
              <div class="mod-card-detail" :class="r3ModClass(props.fight.skillDifferenceRandomModifier * props.sign)">
                {{ fmtPct(props.fight.skillDifferenceRandomModifier * props.sign) }}
              </div>
            </div>
          </div>
          <!-- Roll bar with needle -->
          <div class="fa-roll-bar-wrap" :class="{ 'roll-overflow': props.r3Overflow }">
            <div class="fa-roll-bar-track" :class="{ 'track-overflow': props.r3Overflow }">
              <div class="fa-roll-threshold" :style="{ left: props.r3DisplayChance + '%' }">
                <span class="fa-roll-threshold-label">{{ props.r3Overflow ? '>100' : props.r3DisplayChance.toFixed(0) }}%</span>
              </div>
              <div class="fa-roll-zone-win"
                   :style="{ width: props.r3DisplayChance + '%' }"
                   :class="{ 'zone-overflow': props.r3Overflow }"></div>
              <div class="fa-roll-needle" :style="{ left: props.r3NeedlePos + '%' }" :class="[props.r3WeWon ? 'needle-win' : 'needle-lose', { 'needle-settled': props.r3NeedleSettled }]">
                <span class="fa-roll-needle-val">{{ props.r3RollPct.toFixed(1) }}%</span>
              </div>
            </div>
          </div>
        </div>
      </template>

      <!-- Enemy fight: minimal view -->
      <div v-if="!props.isMyFight && props.showFinalResult" class="enemy-summary">
        <span v-if="props.fight.isTooGoodMe || props.fight.isTooGoodEnemy" class="enchant-badge badge-toogood">TOO GOOD</span>
        <span v-if="props.fight.isTooStronkMe || props.fight.isTooStronkEnemy" class="enchant-badge badge-toostronk">TOO STRONK</span>
      </div>

      <!-- Result badge (corner) -->
      <Transition name="result-badge">
        <span v-if="props.fightResult === 'win'" class="result-badge result-badge-win">&#x2713;</span>
        <span v-else-if="props.fightResult === 'loss'" class="result-badge result-badge-loss">&#x2717;</span>
      </Transition>

      <!-- Enemy fight drops (non-own fights) -->
      <div v-if="props.showFinalResult && !props.isMyFight && props.fight.drops > 0" class="enemy-drop-banner">
        <span class="detail-item detail-drop">DROP {{ props.fight.droppedPlayerName }}! (-{{ props.fight.drops }})</span>
      </div>

      <!-- Portal swap overlay -->
      <div v-if="props.showFinalResult && props.isPortalSwap" class="portal-swap-overlay">
        <div class="portal-ring portal-ring-left"></div>
        <div class="portal-swap-text">PORTAL!</div>
        <div class="portal-ring portal-ring-right"></div>
      </div>

      <!-- Storm cat overlay -->
      <div v-if="props.fight.stormAppeared && props.showR1Result" class="storm-overlay">
        <div class="storm-cat">🐱</div>
        <div class="storm-label">
          Штормяк {{ props.fight.stormWeighingDelta > 0 ? '+' : '' }}{{ props.fight.stormWeighingDelta }}
          <span v-if="props.fight.stormFlipped" class="storm-flipped">ПЕРЕВЕРНУЛ!</span>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
/* ── Arena Container ── */
.arena {
  background: var(--bg-inset);
  border: 1px solid var(--parch-border, var(--border-subtle));
  border-radius: var(--radius);
  padding: 6px 8px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  position: relative;
  overflow: clip;
}
/* Allow dropped card to escape arena bounds */
.arena:has(.dropped) {
  overflow: visible;
}
/* Subtle parchment texture overlay */
.arena::before {
  content: '';
  position: absolute;
  inset: 0;
  background:
    radial-gradient(ellipse at 20% 50%, rgba(201, 168, 76, 0.03) 0%, transparent 50%),
    radial-gradient(ellipse at 80% 50%, rgba(176, 141, 87, 0.02) 0%, transparent 50%);
  pointer-events: none;
  z-index: 0;
}
.arena > * { position: relative; z-index: 1; }

.arena-result-win {
  border-color: rgba(63, 167, 61, 0.5);
  box-shadow: 0 0 12px rgba(63, 167, 61, 0.25), 0 0 24px rgba(63, 167, 61, 0.1), inset 0 0 8px rgba(63, 167, 61, 0.05);
  transition: border-color 0.4s, box-shadow 0.4s;
}
.arena-result-loss {
  border-color: rgba(239, 128, 128, 0.5);
  box-shadow: 0 0 12px rgba(239, 128, 128, 0.25), 0 0 24px rgba(239, 128, 128, 0.1), inset 0 0 8px rgba(239, 128, 128, 0.05);
  transition: border-color 0.4s, box-shadow 0.4s;
}

.arena-shake {
  animation: arena-shake 0.5s ease-in-out;
}
@keyframes arena-shake {
  0%, 100% { transform: translateX(0); }
  10% { transform: translateX(-3px); }
  20% { transform: translateX(3px); }
  30% { transform: translateX(-2px); }
  40% { transform: translateX(2px); }
  50% { transform: translateX(-1px); }
  60% { transform: translateX(1px); }
}

/* ── Arena Grid (3-column: card | center | card) ── */
.arena-grid {
  display: grid;
  grid-template-columns: var(--card-width, 90px) 1fr var(--card-width, 90px);
  gap: 8px;
  align-items: start;
}

/* ── Character Cards ── */
.char-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0;
  padding: 0;
  border-radius: 8px;
  background: var(--bg-surface);
  border: 1.5px solid var(--border-subtle);
  position: relative;
  transition: all 0.4s ease;
  overflow: hidden;
}

.char-card.class-int { border-color: rgba(110, 170, 240, 0.4); }
.char-card.class-str { border-color: rgba(239, 128, 128, 0.4); }
.char-card.class-spd { border-color: rgba(200, 185, 50, 0.4); }

.char-card.winner {
  border-color: rgba(63, 167, 61, 0.6);
  box-shadow: 0 0 12px rgba(63, 167, 61, 0.2);
  transform: translateY(-4px);
}
.char-card.loser {
  filter: brightness(0.7) saturate(0.7);
  transform: rotate(2deg);
  opacity: 0.8;
}
.char-card.dropped {
  animation: avatar-drop 0.9s cubic-bezier(0.22, 1, 0.36, 1) 0.3s both;
  z-index: 20;
}
@keyframes avatar-drop {
  0% { transform: rotate(2deg) translateY(0); opacity: 0.8; filter: brightness(0.7) saturate(0.7); }
  12% { transform: rotate(-1deg) translateY(-10px); opacity: 0.8; }
  40% { transform: rotate(4deg) translateY(var(--drop-distance, 200px)); opacity: 0.7; filter: brightness(0.5) saturate(0.3); }
  55% { transform: rotate(-2deg) translateY(calc(var(--drop-distance, 200px) * 0.92)); opacity: 0.6; }
  70% { transform: rotate(2deg) translateY(calc(var(--drop-distance, 200px) * 0.98)); opacity: 0.5; }
  85% { transform: rotate(-1deg) translateY(calc(var(--drop-distance, 200px) * 0.95)); opacity: 0.4; }
  100% { transform: rotate(1deg) translateY(calc(var(--drop-distance, 200px) * 0.96)); opacity: 0.3; filter: brightness(0.4) saturate(0.2); }
}

.char-class-badge {
  position: absolute;
  top: -1px;
  right: -1px;
  font-size: 7px;
  font-weight: 900;
  padding: 1px 5px;
  border-radius: 0 7px 0 5px;
  letter-spacing: 0.5px;
  background: var(--bg-inset);
  border-bottom: 1px solid var(--border-subtle);
  border-left: 1px solid var(--border-subtle);
}
.char-class-badge.class-int { color: var(--kh-c-secondary-info-200); }
.char-class-badge.class-str { color: var(--kh-c-secondary-danger-200); }
.char-class-badge.class-spd { color: var(--kh-c-text-highlight-dim); }

.char-avatar {
  width: 100%;
  aspect-ratio: 1;
  border-radius: 0;
  object-fit: cover;
  border: none;
  display: block;
}

.char-card.winner .char-avatar {
  box-shadow: inset 0 0 12px rgba(63, 167, 61, 0.4);
}

.char-info {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  line-height: 1.2;
  padding: 12px 4px 4px;
  background: linear-gradient(to top, rgba(0, 0, 0, 0.75) 0%, transparent 100%);
  opacity: 0;
  transition: opacity 0.2s;
  pointer-events: none;
}
.char-card:hover .char-info {
  opacity: 1;
}

.char-name {
  font-size: 9px;
  color: rgba(255, 255, 255, 0.7);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 80px;
  text-align: center;
}

.char-player-name {
  font-size: 10px;
  font-weight: 700;
  color: rgba(255, 255, 255, 0.95);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 80px;
  text-align: center;
}

.char-card.winner .char-player-name {
  color: #5fe35f;
}

/* Character card entrance */
.char-card-left.entrance-active {
  animation: entrance-left 400ms cubic-bezier(0.34, 1.56, 0.64, 1) both;
}
.char-card-right.entrance-active {
  animation: entrance-right 400ms cubic-bezier(0.34, 1.56, 0.64, 1) both;
}

@keyframes entrance-left {
  0% { transform: translateX(-40px) scale(0.6); opacity: 0; }
  60% { transform: translateX(6px) scale(1.05); opacity: 1; }
  80% { transform: translateX(-2px) scale(0.98); }
  100% { transform: translateX(0) scale(1); opacity: 1; }
}
@keyframes entrance-right {
  0% { transform: translateX(40px) scale(0.6); opacity: 0; }
  60% { transform: translateX(-6px) scale(1.05); opacity: 1; }
  80% { transform: translateX(2px) scale(0.98); }
  100% { transform: translateX(0) scale(1); opacity: 1; }
}

/* Entrance impact flash */
.char-card.entrance-active .char-avatar::after {
  content: '';
  position: absolute;
  inset: -8px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(240, 200, 80, 0.6) 0%, transparent 70%);
  opacity: 0;
  animation: entrance-impact-flash 500ms ease-out 300ms forwards;
  pointer-events: none;
}
@keyframes entrance-impact-flash {
  0% { opacity: 0; transform: scale(0.4); }
  30% { opacity: 0.9; transform: scale(1.2); }
  100% { opacity: 0; transform: scale(1.8); }
}

/* ── Arena Center ── */
.arena-center {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
  align-self: stretch;
}

.arena-special .arena-center {
  justify-content: center;
}


/* ── Phase Tracker ── */
.phase-tracker-center {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0;
  padding: 2px 0;
  flex-wrap: nowrap;
}
.phase-role-pip { padding: 2px 5px; border-radius: 4px; border: 1.5px solid var(--border-subtle); background: var(--bg-inset); flex-shrink: 0; }
.phase-role-text { font-size: 8px; font-weight: 900; letter-spacing: 0.5px; }
.phase-role-pip.role-atk { border-color: rgba(239, 128, 128, 0.4); background: rgba(239, 128, 128, 0.08); }
.phase-role-pip.role-atk .phase-role-text { color: var(--accent-red); }
.phase-role-pip.role-def { border-color: rgba(100, 160, 255, 0.4); background: rgba(100, 160, 255, 0.08); }
.phase-role-pip.role-def .phase-role-text { color: var(--accent-blue); }

.phase-pip { display: flex; flex-direction: column; align-items: center; gap: 1px; padding: 3px 6px; border-radius: 6px; border: 1.5px solid var(--border-subtle); background: var(--bg-inset); transition: all 0.4s ease; min-width: 28px; }
.phase-pip.phase-ours { border-color: rgba(63, 167, 61, 0.5); background: rgba(63, 167, 61, 0.08); animation: phase-pop 0.4s ease; }
.phase-pip.phase-theirs { border-color: rgba(239, 128, 128, 0.4); background: rgba(239, 128, 128, 0.06); animation: phase-pop 0.4s ease; }
.phase-pip.phase-draw { border-color: rgba(230, 148, 74, 0.4); background: rgba(230, 148, 74, 0.06); animation: phase-pop 0.4s ease; }
.phase-pip.phase-skipped { opacity: 0.3; border-style: dashed; }

.phase-connector { width: 12px; height: 2px; background: var(--border-subtle); transition: background 0.4s; flex-shrink: 0; }
.phase-connector.revealed { background: var(--text-muted); }
.phase-connector.broken { background: none; border-top: 2px dashed var(--border-subtle); height: 0; }
.phase-connector-outcome { width: 10px; }

.phase-outcome-pip { padding: 2px 6px; border-radius: 4px; border: 1.5px solid var(--border-subtle); background: var(--bg-inset); transition: all 0.4s ease; animation: phase-pop 0.4s ease; }
.phase-outcome-pending { opacity: 0.3; }
.phase-outcome-pip.phase-ours, .phase-outcome-pip.outcome-attacker { border-color: rgba(63, 167, 61, 0.5); background: rgba(63, 167, 61, 0.1); }
.phase-outcome-pip.phase-theirs, .phase-outcome-pip.outcome-defender { border-color: rgba(239, 128, 128, 0.4); background: rgba(239, 128, 128, 0.08); }
.phase-outcome-pip.outcome-neutral { border-color: rgba(230, 148, 74, 0.4); background: rgba(230, 148, 74, 0.08); }
.phase-outcome-text { font-size: 7px; font-weight: 900; letter-spacing: 0.5px; text-transform: uppercase; white-space: nowrap; }
.phase-outcome-pip.phase-ours .phase-outcome-text { color: var(--accent-green); }
.phase-outcome-pip.phase-theirs .phase-outcome-text { color: var(--accent-red); }
.phase-outcome-pip.outcome-neutral .phase-outcome-text { color: var(--accent-orange); }
.phase-outcome-pending .phase-outcome-text { color: var(--text-dim); }

.phase-icon { font-size: 12px; font-weight: 900; line-height: 1; transition: all 0.3s; }
.phase-icon-pending { color: var(--text-dim); opacity: 0.4; }
.phase-icon-win { color: var(--accent-green); animation: phase-icon-in 0.4s ease; }
.phase-icon-lose { color: var(--accent-red); animation: phase-icon-in 0.4s ease; }
.phase-icon-draw { color: var(--accent-orange); animation: phase-icon-in 0.4s ease; }
.phase-icon-skip { color: var(--text-dim); opacity: 0.3; }

@keyframes phase-pop { 0% { transform: scale(1); } 50% { transform: scale(1.15); } 100% { transform: scale(1); } }
@keyframes phase-icon-in { 0% { transform: scale(0); opacity: 0; } 50% { transform: scale(1.3); } 100% { transform: scale(1); opacity: 1; } }


/* ── Block shield ── */
.block-shield {
  font-size: 32px;
  text-align: center;
  opacity: 0.6;
  animation: shield-pop 0.5s ease;
}
@keyframes shield-pop {
  0% { transform: scale(0); opacity: 0; }
  50% { transform: scale(1.2); }
  100% { transform: scale(1); opacity: 0.6; }
}

/* ── Enchantment badges (enemy summary) ── */
.enchant-badge {
  font-size: 8px;
  font-weight: 900;
  padding: 2px 8px;
  border-radius: 4px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}
.badge-toogood { background: rgba(63, 167, 61, 0.1); color: var(--accent-green); border: 1px solid rgba(63, 167, 61, 0.3); }
.badge-toostronk { background: rgba(180, 150, 255, 0.1); color: var(--accent-purple); border: 1px solid rgba(180, 150, 255, 0.3); }

/* ── Enemy summary ── */
.enemy-summary { display: flex; gap: 6px; justify-content: center; padding: 2px 0; }

/* ── Result Badge ── */
.result-badge {
  position: absolute;
  bottom: 8px;
  right: 10px;
  font-size: 18px;
  font-weight: 900;
  z-index: 10;
  pointer-events: none;
  line-height: 1;
}
.result-badge-win { color: var(--accent-green); text-shadow: 0 0 8px rgba(63, 167, 61, 0.6); }
.result-badge-loss { color: var(--accent-red); text-shadow: 0 0 8px rgba(239, 128, 128, 0.6); }

.result-badge-enter-active { transition: opacity 0.3s, transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1); }
.result-badge-leave-active { transition: opacity 0.4s; }
.result-badge-enter-from { opacity: 0; transform: scale(0.5); }
.result-badge-leave-to { opacity: 0; }

/* ── Result Panel ── */
@keyframes result-slide-up {
  0% { opacity: 0; transform: translateY(8px); }
  100% { opacity: 1; transform: translateY(0); }
}

.detail-item {
  padding: 2px 8px;
  border-radius: 4px;
  background: var(--bg-secondary);
  border: 1px solid var(--border-subtle);
}
.detail-skill { color: var(--accent-green); font-weight: 700; }
.detail-moral { color: var(--accent-purple); }
.detail-justice { color: var(--accent-blue); }
.detail-resist { color: var(--accent-orange); }
.detail-dmg-us { color: var(--accent-red); font-weight: 800; font-style: italic; animation: dmg-pulse 0.6s ease-in-out 2; }
.detail-dmg-enemy { color: var(--text-muted); font-weight: 600; font-style: italic; }
@keyframes dmg-pulse { 0%,100% { opacity: 1; } 50% { opacity: 0.4; } }
.detail-drop { color: white; background: var(--accent-red-dim) !important; border-color: var(--accent-red) !important; font-weight: 800; animation: dmg-pulse 0.6s ease-in-out 2; }
.detail-drop-enemy { color: var(--text-muted); font-weight: 600; }

/* v-html stat badges */
.detail-item :deep(.gi) { display: inline-block; font-size: 9px; font-weight: 800; padding: 1px 4px; border-radius: 3px; letter-spacing: 0.5px; vertical-align: middle; }
.detail-item :deep(.gi-int) { background: rgba(110, 170, 240, 0.12); color: var(--kh-c-secondary-info-200); }
.detail-item :deep(.gi-str) { background: rgba(239, 128, 128, 0.12); color: var(--kh-c-secondary-danger-200); }
.detail-item :deep(.gi-psy) { background: rgba(232, 121, 249, 0.12); color: #e879f9; }
.detail-item :deep(.gi-def) { background: rgba(230, 148, 74, 0.12); color: var(--accent-orange); }

/* ── Card Results (under avatars) ── */
.card-results {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 4px 4px 3px;
  width: 100%;
  animation: result-slide-up 0.3s ease;
}
.card-result-item {
  font-size: 9px;
  font-weight: 700;
  text-align: center;
  padding: 1px 3px;
  border-radius: 3px;
  line-height: 1.3;
}
.card-result-item.detail-skill { color: var(--accent-green); }
.card-result-item.detail-moral { color: var(--accent-purple); }
.card-result-item.detail-justice { color: var(--accent-blue); }
.card-result-item.detail-resist { color: var(--accent-orange); font-size: 8px; }
.card-result-item.detail-dmg-us { color: var(--accent-red); font-weight: 800; animation: dmg-pulse 0.6s ease-in-out 2; }
.card-result-item.detail-dmg-enemy { color: var(--text-muted); font-weight: 600; }
.card-result-item.detail-drop { color: white; background: var(--accent-red-dim); font-weight: 800; animation: dmg-pulse 0.6s ease-in-out 2; }
.card-result-item.detail-drop-enemy { color: var(--text-muted); font-weight: 600; }

.enemy-drop-banner {
  display: flex;
  justify-content: center;
  padding: 2px 0;
  animation: result-slide-up 0.3s ease;
}

/* ── Portal Swap ── */
.portal-swap-overlay {
  display: flex; align-items: center; justify-content: center;
  gap: 8px; padding: 4px 0;
  animation: portal-fade-in 0.3s ease-out;
}
.portal-ring {
  width: 24px; height: 24px; border-radius: 50%;
  border: 3px solid transparent;
  border-top-color: #00ff88; border-right-color: #00cc66;
  animation: portal-spin 1s linear infinite;
  box-shadow: 0 0 10px rgba(0, 255, 136, 0.4), inset 0 0 6px rgba(0, 255, 136, 0.2);
}
.portal-ring-right {
  animation-direction: reverse;
  border-top-color: #ff8800; border-right-color: #cc6600;
  box-shadow: 0 0 10px rgba(255, 136, 0, 0.4), inset 0 0 6px rgba(255, 136, 0, 0.2);
}
@keyframes portal-spin { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }
.portal-swap-text {
  font-size: 13px; font-weight: 900; color: #00ff88;
  text-transform: uppercase; letter-spacing: 2px;
  text-shadow: 0 0 12px rgba(0, 255, 136, 0.6);
  animation: portal-text-pulse 0.8s ease-in-out infinite alternate;
}
@keyframes portal-text-pulse {
  0% { opacity: 0.7; transform: scale(0.95); }
  100% { opacity: 1; transform: scale(1.05); }
}
@keyframes portal-fade-in {
  0% { opacity: 0; transform: scale(0.5); }
  60% { transform: scale(1.1); }
  100% { opacity: 1; transform: scale(1); }
}

/* ── Storm Cat Overlay ── */
.storm-overlay {
  position: absolute; inset: 0;
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  pointer-events: none; z-index: 20;
  animation: storm-bounce 0.5s ease-out;
  background: radial-gradient(ellipse at center, rgba(255,165,0,0.12) 0%, transparent 70%);
}
.storm-cat {
  font-size: 48px;
  filter: drop-shadow(0 0 12px rgba(255,165,0,0.6));
  animation: storm-wiggle 0.6s ease-in-out infinite alternate;
}
.storm-label {
  font-size: 13px; font-weight: 700; color: #ffa500;
  text-shadow: 0 0 8px rgba(255,165,0,0.5);
  margin-top: 2px;
}
.storm-flipped {
  color: #ff4444; font-weight: 900; margin-left: 4px;
  animation: storm-flash 0.4s ease-in-out infinite alternate;
}
@keyframes storm-bounce {
  0% { opacity: 0; transform: translateY(-30px) scale(0.5); }
  60% { transform: translateY(5px) scale(1.05); }
  100% { opacity: 1; transform: translateY(0) scale(1); }
}
@keyframes storm-wiggle {
  0% { transform: rotate(-8deg); }
  100% { transform: rotate(8deg); }
}
@keyframes storm-flash {
  0% { opacity: 0.6; }
  100% { opacity: 1; }
}

/* ── Tug-of-War Bar ── */
.tug-of-war {
  padding: 2px 0;
}
.tug-track {
  height: 6px;
  border-radius: 3px;
  background: var(--bg-inset);
  border: 1px solid var(--parch-border, var(--border-subtle));
  overflow: hidden;
  position: relative;
}
.tug-fill {
  height: 100%;
  border-radius: 2px;
  transition: width 0.4s cubic-bezier(0.22, 1, 0.36, 1), background 0.3s;
  position: relative;
}
.tug-good { background: linear-gradient(90deg, rgba(63, 167, 61, 0.6), rgba(63, 167, 61, 0.9)); }
.tug-bad { background: linear-gradient(90deg, rgba(239, 128, 128, 0.6), rgba(239, 128, 128, 0.9)); }
.tug-even { background: linear-gradient(90deg, rgba(230, 148, 74, 0.5), rgba(230, 148, 74, 0.8)); }
.tug-particles {
  position: absolute;
  right: 0;
  top: -2px;
  bottom: -2px;
  width: 8px;
  background: radial-gradient(circle at center, rgba(255, 255, 255, 0.4) 0%, transparent 70%);
  animation: tug-glow 0.6s ease-in-out infinite alternate;
}
@keyframes tug-glow {
  0% { opacity: 0.3; }
  100% { opacity: 0.8; }
}

/* ── Modifier Cards (3-column grid) ── */
.modifier-zone {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.mod-cards-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 4px;
}
.mod-card {
  background: var(--parch-surface, var(--bg-surface));
  border: 1.5px solid var(--parch-border, var(--border-subtle));
  border-radius: 5px;
  padding: 5px 7px;
  font-size: 10px;
  opacity: 0;
  transform: translateY(10px) scale(0.85);
  transition: opacity 0.3s, transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1), filter 0.4s, border-color 0.3s;
  position: relative;
  overflow: hidden;
}
.mod-card.visible {
  opacity: 1;
  transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1);
}

/* ── Base highlight colors ── */
.mod-card.good { border-color: rgba(63, 167, 61, 0.5); background: rgba(63, 167, 61, 0.08); }
.mod-card.bad { border-color: rgba(239, 128, 128, 0.5); background: rgba(239, 128, 128, 0.08); }
.mod-card.neutral { border-color: rgba(230, 148, 74, 0.5); background: rgba(230, 148, 74, 0.08); }

/* ── GOOD tier effects ── */
/* Tier 0 (▲): subtle glow + gentle lift */
.mod-card.good.tier-0.visible {
  animation: card-forge-0 0.35s ease-out 0.1s both;
}
@keyframes card-forge-0 {
  0% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1); }
  40% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1.03) translateY(-1px); }
  100% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1.01); }
}
.mod-card.good.tier-0.visible::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 5px;
  background: linear-gradient(
    105deg,
    transparent 40%,
    rgba(255, 255, 255, 0.15) 48%,
    rgba(255, 255, 255, 0.05) 52%,
    transparent 58%
  );
  opacity: 0;
  animation: shine-sweep 0.7s ease-out 0.3s forwards;
  pointer-events: none;
}

/* Tier 1 (▲▲): Forge stamp — bounce slam + shine sweep */
.mod-card.good.tier-1.visible {
  border-color: rgba(63, 167, 61, 0.6);
  animation: card-forge-1 0.45s cubic-bezier(0.34, 1.56, 0.64, 1) 0.1s both;
}
@keyframes card-forge-1 {
  0% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1) translateY(0); }
  35% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1.06) translateY(-3px); }
  55% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(0.98) translateY(1px); }
  100% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1.02) translateY(0); }
}
/* Shine sweep for tier 1 */
.mod-card.good.tier-1.visible::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 5px;
  background: linear-gradient(
    105deg,
    transparent 35%,
    rgba(255, 255, 255, 0.25) 45%,
    rgba(255, 255, 255, 0.08) 50%,
    transparent 55%
  );
  opacity: 0;
  animation: shine-sweep 0.6s ease-out 0.35s forwards;
  pointer-events: none;
}

/* Tier 2: Heavy forge — slam + shine + spark particles + embossed border */
.mod-card.good.tier-2.visible {
  border-color: rgba(63, 167, 61, 0.7);
  box-shadow: 0 0 8px rgba(63, 167, 61, 0.25), 0 2px 6px rgba(0, 0, 0, 0.12);
  animation: card-forge-2 0.5s cubic-bezier(0.34, 1.56, 0.64, 1) 0.1s both;
}
@keyframes card-forge-2 {
  0% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1) translateY(0); }
  25% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1.08) translateY(-4px); }
  45% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(0.96) translateY(2px); box-shadow: 0 0 12px rgba(63, 167, 61, 0.4); }
  65% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1.04) translateY(-1px); }
  100% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1.03) translateY(0); box-shadow: 0 0 8px rgba(63, 167, 61, 0.25), 0 2px 6px rgba(0, 0, 0, 0.12); }
}
/* Shine sweep for tier 2 */
.mod-card.good.tier-2.visible::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 5px;
  background: linear-gradient(
    105deg,
    transparent 30%,
    rgba(255, 255, 255, 0.35) 42%,
    rgba(255, 255, 255, 0.12) 50%,
    transparent 55%
  );
  opacity: 0;
  animation: shine-sweep 0.5s ease-out 0.3s forwards;
  pointer-events: none;
}
/* Spark particles for tier 2 */
.mod-card.good.tier-2.visible::before {
  content: '';
  position: absolute;
  top: 50%;
  left: 50%;
  width: 5px;
  height: 5px;
  background: rgba(63, 167, 61, 0.7);
  border-radius: 50%;
  opacity: 0;
  animation: forge-sparks 0.5s ease-out 0.25s forwards;
  box-shadow:
    -6px -12px 0 rgba(63, 167, 61, 0.6),
    4px -14px 0 rgba(201, 168, 76, 0.5),
    10px -8px 0 rgba(63, 167, 61, 0.5),
    -10px -6px 0 rgba(201, 168, 76, 0.4),
    0px -16px 0 rgba(63, 167, 61, 0.4);
  pointer-events: none;
}
@keyframes forge-sparks {
  0% { opacity: 0.9; transform: translate(-50%, -50%) scale(0.5); }
  100% { opacity: 0; transform: translate(-50%, -50%) scale(2.2) translateY(-6px); }
}
@keyframes shine-sweep {
  0% { opacity: 0; transform: translateX(-100%); }
  40% { opacity: 1; }
  100% { opacity: 0; transform: translateX(100%); }
}

/* ── BAD tier effects ── */
/* Shared shatter animations */
@keyframes card-crack-in {
  0% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1); filter: brightness(1); }
  30% { transform: translateX(calc(var(--nudge, 0px) + 1px)) rotate(calc(var(--tilt, 0deg) + 0.5deg)) scale(1.01); }
  100% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(0.96); filter: brightness(0.7) saturate(0.7); }
}
@keyframes card-shatter {
  0% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1); filter: brightness(1); }
  15% { transform: translateX(calc(var(--nudge, 0px) + 3px)) rotate(calc(var(--tilt, 0deg) + 2deg)) scale(1.03); }
  30% { transform: translateX(calc(var(--nudge, 0px) - 3px)) rotate(calc(var(--tilt, 0deg) - 2deg)) scale(1.02); }
  45% { transform: translateX(calc(var(--nudge, 0px) + 2px)) rotate(calc(var(--tilt, 0deg) + 1deg)) scale(0.98); filter: brightness(0.6); }
  60% { transform: translateX(calc(var(--nudge, 0px) - 1px)) rotate(calc(var(--tilt, 0deg) - 0.5deg)) scale(0.92); }
  100% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(0.85); filter: brightness(0.55) saturate(0.4); }
}
@keyframes card-explode {
  0% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(1); filter: brightness(1); }
  10% { transform: translateX(calc(var(--nudge, 0px) + 4px)) rotate(calc(var(--tilt, 0deg) + 3deg)) scale(1.06); filter: brightness(1.3); }
  20% { transform: translateX(calc(var(--nudge, 0px) - 4px)) rotate(calc(var(--tilt, 0deg) - 3deg)) scale(1.04); }
  30% { transform: translateX(calc(var(--nudge, 0px) + 3px)) rotate(calc(var(--tilt, 0deg) + 2deg)) scale(1.02); filter: brightness(0.5); }
  45% { transform: translateX(calc(var(--nudge, 0px) - 2px)) rotate(calc(var(--tilt, 0deg) - 1deg)) scale(0.9); }
  60% { transform: translateX(calc(var(--nudge, 0px) + 1px)) rotate(calc(var(--tilt, 0deg) + 0.5deg)) scale(0.82); filter: brightness(0.4) saturate(0.2); }
  100% { transform: translateX(var(--nudge, 0px)) rotate(var(--tilt, 0deg)) scale(0.75); filter: brightness(0.4) saturate(0.15); }
}
@keyframes shard-burst {
  0% { opacity: 0.8; transform: translate(-50%, -50%) scale(0.5); }
  100% { opacity: 0; transform: translate(-50%, -50%) scale(2.5); }
}
@keyframes shard-explode {
  0% { opacity: 1; transform: translate(-50%, -50%) scale(0.6); }
  40% { opacity: 0.9; }
  100% { opacity: 0; transform: translate(-50%, -50%) scale(4); }
}

/* Tier 0 (▼): single slash crack + slight shake */
.mod-card.bad.tier-0.visible {
  animation: card-crack-in 0.4s ease-out 0.15s both;
}
.mod-card.bad.tier-0.visible::before {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 5px;
  background:
    linear-gradient(35deg, transparent 42%, rgba(239, 128, 128, 0.25) 42%, rgba(239, 128, 128, 0.25) 44%, transparent 44%);
  pointer-events: none;
  opacity: 0;
  animation: crack-appear 0.3s ease-out 0.35s forwards;
}
.mod-card.bad.tier-0.visible::after {
  content: '';
  position: absolute;
  top: 50%;
  left: 50%;
  width: 4px;
  height: 4px;
  background: rgba(239, 128, 128, 0.5);
  border-radius: 2px;
  opacity: 0;
  animation: shard-burst 0.4s ease-out 0.3s forwards;
  box-shadow:
    6px -7px 0 rgba(239, 128, 128, 0.3),
    -7px 5px 0 rgba(239, 128, 128, 0.25);
  pointer-events: none;
}

/* Tier 1 (▼▼): X cross + shatter */
.mod-card.bad.tier-1.visible {
  animation: card-shatter 0.6s ease-out 0.15s both;
}
.mod-card.bad.tier-1.visible::before {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 5px;
  background:
    linear-gradient(45deg, transparent 40%, rgba(239, 128, 128, 0.4) 40%, rgba(239, 128, 128, 0.4) 43%, transparent 43%),
    linear-gradient(-45deg, transparent 40%, rgba(239, 128, 128, 0.4) 40%, rgba(239, 128, 128, 0.4) 43%, transparent 43%);
  pointer-events: none;
  opacity: 0;
  animation: crack-appear 0.2s ease-out 0.4s forwards;
}
.mod-card.bad.tier-1.visible::after {
  content: '';
  position: absolute;
  top: 50%;
  left: 50%;
  width: 6px;
  height: 6px;
  background: rgba(239, 128, 128, 0.6);
  border-radius: 2px;
  opacity: 0;
  animation: shard-burst 0.5s ease-out 0.3s forwards;
  box-shadow:
    8px -10px 0 rgba(239, 128, 128, 0.5),
    -10px -6px 0 rgba(239, 128, 128, 0.4),
    12px 4px 0 rgba(239, 128, 128, 0.3),
    -6px 8px 0 rgba(239, 128, 128, 0.4),
    4px 12px 0 rgba(239, 128, 128, 0.3);
  pointer-events: none;
}

/* Tier 2 (▼▼▼): multiple cracks + blow-up explosion */
.mod-card.bad.tier-2.visible {
  animation: card-explode 0.7s ease-out 0.15s both;
}
.mod-card.bad.tier-2.visible::before {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 5px;
  background:
    linear-gradient(30deg, transparent 38%, rgba(239, 128, 128, 0.45) 38%, rgba(239, 128, 128, 0.45) 41%, transparent 41%),
    linear-gradient(-40deg, transparent 36%, rgba(239, 128, 128, 0.4) 36%, rgba(239, 128, 128, 0.4) 39%, transparent 39%),
    linear-gradient(70deg, transparent 44%, rgba(239, 128, 128, 0.35) 44%, rgba(239, 128, 128, 0.35) 47%, transparent 47%),
    linear-gradient(-15deg, transparent 50%, rgba(239, 128, 128, 0.3) 50%, rgba(239, 128, 128, 0.3) 52%, transparent 52%);
  pointer-events: none;
  opacity: 0;
  animation: crack-appear 0.15s ease-out 0.3s forwards;
}
.mod-card.bad.tier-2.visible::after {
  content: '';
  position: absolute;
  top: 50%;
  left: 50%;
  width: 8px;
  height: 8px;
  background: rgba(239, 128, 128, 0.8);
  border-radius: 2px;
  opacity: 0;
  animation: shard-explode 0.6s ease-out 0.25s forwards;
  box-shadow:
    10px -12px 0 rgba(239, 128, 128, 0.7),
    -12px -8px 0 rgba(239, 128, 128, 0.6),
    14px 5px 0 rgba(239, 128, 128, 0.5),
    -8px 10px 0 rgba(239, 128, 128, 0.6),
    5px 14px 0 rgba(239, 128, 128, 0.5),
    -14px 2px 0 rgba(239, 128, 128, 0.4),
    3px -16px 0 rgba(239, 128, 128, 0.35),
    16px -3px 0 rgba(239, 128, 128, 0.3);
  pointer-events: none;
}

@keyframes crack-appear {
  0% { opacity: 0; }
  100% { opacity: 1; }
}

.mod-card-header {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 4px;
}
.mod-card-label {
  font-weight: 700;
  font-size: 10px;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.mod-card-value {
  font-weight: 900;
  font-size: 10px;
  flex-shrink: 0;
}
.mod-card.good .mod-card-value { color: var(--accent-green); }
.mod-card.bad .mod-card-value { color: var(--accent-red); }
.mod-card.neutral .mod-card-value { color: var(--accent-orange); }

.mod-card-detail {
  font-size: 9px;
  color: var(--text-muted);
  line-height: 1.3;
  margin-top: 2px;
  font-style: italic;
}
.mod-card-detail :deep(b),
.mod-card-detail :deep(strong) {
  font-weight: 800;
  font-style: normal;
  color: var(--text-primary);
}
.mod-card.good .mod-card-detail { color: rgba(63, 167, 61, 0.7); }
.mod-card.bad .mod-card-detail { color: rgba(239, 128, 128, 0.7); }

/* Nemesis verbs and stat detail badges */
.mod-card-detail :deep(.dom-good) { color: var(--accent-green); font-weight: 800; font-style: normal; }
.mod-card-detail :deep(.dom-bad) { color: var(--accent-red); font-weight: 800; font-style: normal; }
.mod-card-detail :deep(.gi-ok) { color: var(--accent-green); font-weight: 800; }
.mod-card-detail :deep(.gi-fail) { color: var(--accent-red); font-weight: 800; }
.mod-card-detail :deep(.gi-tie) { color: var(--text-dim); }
.mod-card-detail :deep(.gi) { display: inline-block; font-size: 8px; font-weight: 800; padding: 1px 3px; border-radius: 2px; letter-spacing: 0.3px; vertical-align: middle; }
.mod-card-detail :deep(.gi-int) { background: rgba(110, 170, 240, 0.15); color: var(--kh-c-secondary-info-200); }
.mod-card-detail :deep(.gi-str) { background: rgba(239, 128, 128, 0.15); color: var(--kh-c-secondary-danger-200); }
.mod-card-detail :deep(.gi-spd) { background: rgba(200, 185, 50, 0.15); color: var(--kh-c-text-highlight-dim); }
.mod-card-detail :deep(.admin-extra) { font-size: 8px; color: var(--text-dim); font-style: normal; }

/* ── Enchantment Badges ── */
.enchantment-row {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  justify-content: center;
  opacity: 0;
  transform: translateY(4px);
  transition: opacity 0.3s, transform 0.3s;
}
.enchantment-row.visible {
  opacity: 1;
  transform: translateY(0);
}
.badge-arrow {
  font-size: 9px;
  line-height: 1;
  margin-right: 2px;
}
/* Green = benefits us, Red = hurts us */
.badge-buff-ours {
  background: rgba(63, 167, 61, 0.12);
  color: var(--accent-green);
  border: 1px solid rgba(63, 167, 61, 0.4);
  border-left: 3px solid var(--accent-green);
}
.badge-buff-theirs {
  background: rgba(239, 128, 128, 0.12);
  color: var(--accent-red);
  border: 1px solid rgba(239, 128, 128, 0.4);
  border-left: 3px solid var(--accent-red);
}
.badge-class-change { background: rgba(100, 160, 255, 0.1); color: var(--accent-blue); border: 1px solid rgba(100, 160, 255, 0.3); }

/* ── Justice Slam (Phase 2) ── */
.justice-slam {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 6px 0;
  position: relative;
}
.justice-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  padding: 6px 12px;
  border-radius: 6px;
  background: var(--parch-surface, var(--bg-surface));
  border: 1.5px solid var(--parch-border, var(--border-subtle));
  position: relative;
  transition: transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1), opacity 0.3s, box-shadow 0.3s;
}
.justice-left.j-rush {
  animation: justice-rush-left 0.2s cubic-bezier(0.22, 1, 0.36, 1) both;
}
.justice-right.j-rush {
  animation: justice-rush-right 0.2s cubic-bezier(0.22, 1, 0.36, 1) both;
}
@keyframes justice-rush-left {
  0% { transform: translateX(-60px) scale(0.7); opacity: 0; }
  100% { transform: translateX(0) scale(1); opacity: 1; }
}
@keyframes justice-rush-right {
  0% { transform: translateX(60px) scale(0.7); opacity: 0; }
  100% { transform: translateX(0) scale(1); opacity: 1; }
}

.justice-card.j-winner {
  border-color: rgba(63, 167, 61, 0.6);
  box-shadow: 0 0 10px rgba(63, 167, 61, 0.3);
  transform: scale(1.1);
}
.justice-card.j-loser {
  opacity: 0.5;
  transform: scale(0.9);
  filter: grayscale(0.4);
}
.justice-card.j-loser::before {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 6px;
  background:
    linear-gradient(45deg, transparent 45%, rgba(239, 128, 128, 0.3) 45%, rgba(239, 128, 128, 0.3) 55%, transparent 55%),
    linear-gradient(-45deg, transparent 45%, rgba(239, 128, 128, 0.2) 45%, rgba(239, 128, 128, 0.2) 55%, transparent 55%);
  pointer-events: none;
}
.justice-card.j-tied {
  border-color: rgba(230, 148, 74, 0.5);
  box-shadow: 0 0 6px rgba(230, 148, 74, 0.2);
}

.justice-label {
  font-size: 8px;
  font-weight: 700;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}
.justice-value {
  font-size: 18px;
  font-weight: 900;
  color: rgba(180, 150, 255, 0.95);
  line-height: 1;
}
.justice-card.j-winner .justice-value { color: rgba(180, 150, 255, 0.95); text-shadow: 0 0 10px rgba(63, 167, 61, 0.5); }
.justice-card.j-loser .justice-value { color: rgba(180, 150, 255, 0.95); text-shadow: 0 0 10px rgba(239, 128, 128, 0.5); }
.justice-card.j-tied .justice-value { color: rgba(180, 150, 255, 0.95); text-shadow: 0 0 10px rgba(240, 180, 60, 0.5); }

.justice-seal {
  position: absolute;
  top: -6px;
  right: -6px;
}

.justice-vs {
  font-size: 11px;
  font-weight: 900;
  color: var(--text-dim);
  opacity: 0;
  transition: opacity 0.2s;
}
.justice-vs.visible { opacity: 1; }
.justice-spark {
  font-size: 16px;
  animation: spark-flash 0.3s ease-out;
}
@keyframes spark-flash {
  0% { transform: scale(2); opacity: 0; }
  50% { opacity: 1; }
  100% { transform: scale(1); opacity: 1; }
}

.slam-impact {
  animation: slam-shake 0.3s ease-in-out;
}
@keyframes slam-shake {
  0%, 100% { transform: translateX(0); }
  15% { transform: translateX(-3px); }
  30% { transform: translateX(3px); }
  45% { transform: translateX(-2px); }
  60% { transform: translateX(2px); }
  75% { transform: translateX(-1px); }
}

.slam-particles {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  pointer-events: none;
}
.slam-dust {
  position: absolute;
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: var(--parch-gold, #c9a84c);
  opacity: 0;
  animation: dust-rise 0.5s ease-out var(--delay, 0ms) forwards;
}
@keyframes dust-rise {
  0% { transform: translate(0, 0) scale(1); opacity: 0.8; }
  100% { transform: translate(var(--dx, 10px), -14px) scale(0.3); opacity: 0; }
}

/* ── R3 details ── */
.fa-r3-details { display: flex; flex-direction: column; gap: 4px; }
.pct-good { color: var(--accent-green); font-weight: 700; }
.pct-bad { color: var(--accent-red); font-weight: 700; }

/* ── Roll bar (needle) ── */
.fa-roll-bar-wrap { margin-top: 3px; padding-top: 3px; }
.fa-roll-bar-track { position: relative; height: 22px; background: rgba(239, 128, 128, 0.12); border-radius: 6px; overflow: visible; border: 1px solid var(--border-subtle); }
.fa-roll-zone-win { position: absolute; left: 0; top: 0; height: 100%; background: rgba(63, 167, 61, 0.15); border-radius: 6px 0 0 6px; transition: width 0.6s ease; }
.fa-roll-threshold { position: absolute; top: -2px; bottom: -2px; width: 2px; background: var(--accent-gold); z-index: 2; transform: translateX(-1px); }
.fa-roll-threshold-label { position: absolute; top: -14px; left: 50%; transform: translateX(-50%); font-size: 8px; font-weight: 800; color: var(--accent-gold); white-space: nowrap; font-family: var(--font-mono); }
.fa-roll-needle { position: absolute; top: -2px; bottom: -2px; width: 3px; z-index: 3; transform: translateX(-1.5px); border-radius: 2px; transition: none; }
.fa-roll-needle.needle-win { background: var(--accent-green); box-shadow: 0 0 8px rgba(63, 167, 61, 0.6); }
.fa-roll-needle.needle-lose { background: var(--accent-red); box-shadow: 0 0 8px rgba(239, 128, 128, 0.6); }
.fa-roll-needle.needle-settled.needle-win { box-shadow: 0 0 14px rgba(63, 167, 61, 0.8), 0 0 28px rgba(63, 167, 61, 0.3); width: 4px; }
.fa-roll-needle.needle-settled.needle-lose { box-shadow: 0 0 14px rgba(239, 128, 128, 0.8), 0 0 28px rgba(239, 128, 128, 0.3); width: 4px; }
.fa-roll-needle-val { position: absolute; bottom: -14px; left: 50%; transform: translateX(-50%); font-size: 8px; font-weight: 800; white-space: nowrap; font-family: var(--font-mono); }
.needle-win .fa-roll-needle-val { color: var(--accent-green); }
.needle-lose .fa-roll-needle-val { color: var(--accent-red); }
/* Overflow: chance > 100% */
.fa-roll-bar-track.track-overflow { border-right: none; border-radius: 6px 0 0 6px; }
.fa-roll-zone-win.zone-overflow { width: 100% !important; border-radius: 6px 0 0 6px; box-shadow: 4px 0 12px rgba(63, 167, 61, 0.4), 8px 0 24px rgba(63, 167, 61, 0.2); }
.fa-roll-bar-wrap.roll-overflow { position: relative; }
.fa-roll-bar-wrap.roll-overflow::after { content: ''; position: absolute; right: -8px; top: 3px; bottom: 0; width: 16px; background: linear-gradient(90deg, rgba(63, 167, 61, 0.3), transparent); border-radius: 0 6px 6px 0; pointer-events: none; }

/* ── Responsive ── */
@media (max-width: 400px) {
  .arena-grid {
    --card-width: 70px;
  }
  .char-avatar {
    width: 40px;
    height: 40px;
  }
  .char-name, .char-player-name {
    max-width: 60px;
    font-size: 8px;
  }
  .phase-connector { width: 8px; }
  .phase-pip { padding: 2px 4px; min-width: 22px; }
  .phase-icon { font-size: 10px; }
  .mod-cards-grid { grid-template-columns: repeat(2, 1fr); }
  .mod-card { font-size: 9px; padding: 3px 5px; }
  .mod-card-label { font-size: 9px; }
}
</style>
