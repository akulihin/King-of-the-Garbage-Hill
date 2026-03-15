<script setup lang="ts">
import { computed } from 'vue'
import type { FightEntry, ForOneFightMod } from 'src/services/signalr'
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
  // Clash phase for KO impact animation
  clashPhase: 'idle' | 'hit' | 'recoil' | 'settled'
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

const leftFactors = computed(() =>
  props.round1Factors.filter(f => f.highlight === 'good' || (f.highlight === 'neutral' && f.value >= 0))
)

const rightFactors = computed(() =>
  props.round1Factors.filter(f => f.highlight === 'bad' || (f.highlight === 'neutral' && f.value < 0))
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

function phaseIcon(result: number, revealed: boolean): string {
  if (!revealed) return '?'
  if (result > 0) return '\u2713'
  if (result < 0) return '\u2717'
  return '\u2014'
}

function phaseIconClass(result: number, revealed: boolean): string {
  if (!revealed) return 'phase-icon-pending'
  if (result > 0) return 'phase-icon-win'
  if (result < 0) return 'phase-icon-lose'
  return 'phase-icon-draw'
}

function classAbbrev(charClass: string): string {
  switch (charClass) {
    case 'Интеллект': return 'INT'
    case 'Сила': return 'STR'
    case 'Скорость': return 'SPD'
    default: return '?'
  }
}

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

/** Enchantment badges that benefit or affect US (shown on our card) */
const ourEnchantBadges = computed(() => {
  const badges: { text: string; isGood: boolean; arrow: string }[] = []
  const f = props.fight

  // TOO GOOD — check if it benefits us
  if (f.isTooGoodMe || f.isTooGoodEnemy) {
    const ours = f.isTooGoodMe ? !props.isFlipped : props.isFlipped
    badges.push({ text: 'TOO GOOD', isGood: ours, arrow: ours ? '\u2B06' : '\u2B07' })
  }
  // TOO STRONK
  if (f.isTooStronkMe || f.isTooStronkEnemy) {
    const ours = f.isTooStronkMe ? !props.isFlipped : props.isFlipped
    badges.push({ text: 'TOO STRONK', isGood: ours, arrow: ours ? '\u2B06' : '\u2B07' })
  }
  // Skill multiplier (always ours)
  if (props.ourSkillMultiplier > 1) {
    badges.push({ text: `SKILL x${props.ourSkillMultiplier}`, isGood: true, arrow: '\u2B06' })
  }
  // ForOneFight mods (on us)
  for (const mod of props.ourForOneFightMods) {
    badges.push({ text: props.fofBadgeText(mod), isGood: props.isFofBuff(mod), arrow: props.isFofBuff(mod) ? '\u2B06' : '\u2B07' })
  }
  // Enemy mods affecting us (debuffs)
  for (const mod of props.enemyModsOnUs) {
    badges.push({ text: props.enemyFofBadgeText(mod), isGood: false, arrow: '\u2B07' })
  }
  // Class change
  if (props.classChangeBadge) {
    badges.push({ text: props.classChangeBadge, isGood: true, arrow: '' })
  }

  return badges
})

/** Positive badges → our card (left, bottom-right) */
const ourBuffBadges = computed(() => ourEnchantBadges.value.filter(b => b.isGood))
/** Negative badges → enemy card (right, bottom-left) */
const enemyDebuffBadges = computed(() => ourEnchantBadges.value.filter(b => !b.isGood))
const hasOurBadges = computed(() => ourBuffBadges.value.length > 0)
const hasEnemyBadges = computed(() => enemyDebuffBadges.value.length > 0)
</script>

<template>
  <div class="arena" :class="{
    'arena-shake': props.fightShake,
    'arena-result-win': props.fightResult === 'win',
    'arena-result-loss': props.fightResult === 'loss',
  }">
    <!-- ═══ BLOCK/SKIP ═══ -->
    <div v-if="isSpecialOutcome" class="arena-special">
      <div class="arena-grid">
        <!-- Left character card -->
        <div class="char-card char-card-left" :class="classColor(leftClass)"
          :style="{ '--char-bg': `url(${props.getDisplayAvatar(props.leftAvatar, props.leftName)})` }">
          <div class="char-class-corner corner-right" :class="classColor(leftClass)">
            {{ classAbbrev(leftClass) }}
          </div>
          <img :src="props.getDisplayAvatar(props.leftAvatar, props.leftName)" class="char-avatar"
            @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
          <div class="char-info">
            <span class="char-name">{{ props.getDisplayCharName(props.leftCharName, props.leftName) }}</span>
            <span class="char-player-name">{{ props.leftName }}</span>
          </div>
        </div>

        <!-- Center: outcome -->
        <div class="arena-center">
          <div class="phase-tracker-vertical">
            <!-- Role medallion -->
            <div class="medallion role-badge" :class="props.attackedRight ? 'role-atk' : 'role-def'">
              <span class="role-icon">{{ props.attackedRight ? '\u2694' : '\uD83D\uDEE1' }}</span>
              <span class="phase-role-text">{{ props.attackedRight ? 'ATK' : 'DEF' }}</span>
            </div>
            <div class="medallion-connector revealed"></div>
            <!-- Outcome medallion -->
            <div class="medallion outcome-medallion" :class="outcomeClass(props.fight)">
              <span class="outcome-text">{{ outcomeLabel(props.fight) }}</span>
            </div>
          </div>
          <!-- Shield overlay for block -->
          <div v-if="props.fight.outcome === 'block'" class="block-shield">&#x1F6E1;</div>
        </div>

        <!-- Right character card -->
        <div class="char-card char-card-right" :class="classColor(rightClass)"
          :style="{ '--char-bg': `url(${props.getDisplayAvatar(props.rightAvatar, props.rightName)})` }">
          <div class="char-class-corner corner-left" :class="classColor(rightClass)">
            {{ classAbbrev(rightClass) }}
          </div>
          <img :src="props.getDisplayAvatar(props.rightAvatar, props.rightName)" class="char-avatar"
            @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
          <div class="char-info">
            <span class="char-name">{{ props.getDisplayCharName(props.rightCharName, props.rightName) }}</span>
            <span class="char-player-name">{{ props.rightName }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- ═══ NORMAL FIGHT ═══ -->
    <template v-else>
      <div class="arena-grid" :class="{
        clashed: props.isMyFight && props.showFinalResult && !isSpecialOutcome,
        'skip-clash': props.skippedToEnd,
        'clash-hit': props.clashPhase === 'hit',
        'clash-recoil': props.clashPhase === 'recoil' || props.clashPhase === 'settled',
      }">
        <!-- Left character card -->
        <div class="char-card char-card-left" :class="[
          classColor(leftClass),
          {
            winner: props.showFinalResult && props.leftWon,
            loser: props.showFinalResult && !props.leftWon,
            dropped: props.showFinalResult && !props.leftWon && props.fight.drops > 0,
            'entrance-active': props.currentStep === 0 && !props.skippedToEnd,
            'recoil-loser': (props.clashPhase === 'recoil' || props.clashPhase === 'settled') && props.showFinalResult && !props.leftWon,
            'recoil-winner': (props.clashPhase === 'recoil' || props.clashPhase === 'settled') && props.showFinalResult && props.leftWon,
          }
        ]" :style="{ '--char-bg': `url(${props.getDisplayAvatar(props.leftAvatar, props.leftName)})` }">
          <div class="char-class-corner corner-right" :class="classColor(leftClass)">
            {{ classAbbrev(leftClass) }}
          </div>
          <img :src="props.getDisplayAvatar(props.leftAvatar, props.leftName)" class="char-avatar"
            @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
          <div class="char-info">
            <span class="char-name">{{ props.getDisplayCharName(props.leftCharName, props.leftName) }}</span>
            <span class="char-player-name">{{ props.leftName }}</span>
          </div>
          <!-- Factor tags (good / neutral-positive) -->
          <div class="factor-tags factor-tags-left" v-if="isMyFight">
            <div
              v-for="(factor, idx) in leftFactors"
              :key="'lf-' + idx"
              class="factor-tag factor-good"
              :class="{
                visible: showR1Factor(props.round1Factors.indexOf(factor)),
                'skip-anim': skippedToEnd
              }"
              :title="factor.detail"
            >
              <span class="factor-tag-label">{{ factor.label }}</span>
              <span class="factor-tag-arrows">{{ '\u25B2'.repeat(Math.max(1, factor.tier || 1)) }}</span>
            </div>
          </div>
          <!-- Positive enchant badges (bottom-right of our card) -->
          <div class="enchant-overlay enchant-overlay-right" v-if="isMyFight && hasOurBadges" :class="{ visible: props.showR1Result }">
            <div
              v-for="(badge, bIdx) in ourBuffBadges"
              :key="'eb-' + bIdx"
              class="enchant-pill enchant-buff"
            >
              <span v-if="badge.arrow" class="enchant-arrow">{{ badge.arrow }}</span>
              {{ badge.text }}
            </div>
          </div>
        </div>

        <!-- Center zone -->
        <div class="arena-center">
          <!-- Phase tracker (vertical medallion stack) -->
          <div v-if="props.isMyFight" class="phase-tracker-vertical">
            <!-- Role medallion -->
            <div class="medallion role-badge" :class="props.attackedRight ? 'role-atk' : 'role-def'">
              <span class="role-icon">{{ props.attackedRight ? '\u2694' : '\uD83D\uDEE1' }}</span>
              <span class="phase-role-text">{{ props.attackedRight ? 'ATK' : 'DEF' }}</span>
            </div>
            <div class="medallion-connector revealed"></div>

            <!-- Phase 1 medallion -->
            <div class="medallion phase-medallion" :class="props.phaseClass(props.phase1Result, props.showR1Result)">
              <span class="medallion-label">R1</span>
              <span class="medallion-icon phase-icon" :class="phaseIconClass(props.phase1Result, props.showR1Result)">{{ phaseIcon(props.phase1Result, props.showR1Result) }}</span>
            </div>
            <!-- Tug-of-war integrated into phase tracker -->
            <div class="tug-of-war-vertical">
              <div class="tug-track-v">
                <div class="tug-fill-v" :style="{ height: props.barPosition + '%' }" :class="{
                  'tug-good': props.animatedWeighingValue > 0,
                  'tug-bad': props.animatedWeighingValue < 0,
                  'tug-even': props.animatedWeighingValue === 0,
                }">
                  <div class="tug-particles-v"></div>
                </div>
              </div>
            </div>
            <div class="medallion-connector" :class="{ revealed: props.showR2 }"></div>

            <!-- Phase 2 medallion -->
            <div class="medallion phase-medallion" :class="props.phaseClass(props.phase2Result, props.phase2Revealed)">
              <span class="medallion-label">R2</span>
              <span class="medallion-icon phase-icon" :class="phaseIconClass(props.phase2Result, props.phase2Revealed)">{{ phaseIcon(props.phase2Result, props.phase2Revealed) }}</span>
            </div>
            <!-- Compact justice display (R2) -->
            <div class="justice-display" v-if="showR2" :class="'slam-' + slamPhase">
              <div class="justice-value-side" :class="{ 'j-winner': justiceWinner === 'left', 'j-loser': justiceWinner === 'right' }">
                <span class="justice-icon">⚖</span>
                <span class="justice-num">{{ ourJustice }}</span>
              </div>
              <span class="justice-vs">vs</span>
              <div class="justice-value-side" :class="{ 'j-winner': justiceWinner === 'right', 'j-loser': justiceWinner === 'left' }">
                <span class="justice-num">{{ enemyJustice }}</span>
                <span class="justice-icon">⚖</span>
              </div>
            </div>
            <div class="medallion-connector" :class="{ revealed: props.fight.usedRandomRoll && props.showR3, broken: !props.fight.usedRandomRoll }"></div>

            <!-- Phase 3 medallion -->
            <div class="medallion phase-medallion" :class="[props.phaseClass(props.phase3Result, props.fight.usedRandomRoll ? props.phase3Revealed : false), { 'phase-skipped': !props.fight.usedRandomRoll }]">
              <span class="medallion-label">R3</span>
              <template v-if="!props.fight.usedRandomRoll">
                <span class="medallion-icon phase-icon phase-icon-skip">&mdash;</span>
              </template>
              <template v-else>
                <span class="medallion-icon phase-icon" :class="phaseIconClass(props.phase3Result, props.phase3Revealed)">{{ phaseIcon(props.phase3Result, props.phase3Revealed) }}</span>
              </template>
            </div>
            <!-- R3 modifier chips -->
            <div class="r3-modifier-chips" v-if="showR3">
              <div v-if="fight.tooGoodRandomChange" class="r3-chip" :class="r3ModClass(fight.tooGoodRandomChange)">
                TG {{ fmtPct(fight.tooGoodRandomChange) }}
              </div>
              <div v-if="fight.tooStronkRandomChange" class="r3-chip" :class="r3ModClass(fight.tooStronkRandomChange)">
                TS {{ fmtPct(fight.tooStronkRandomChange) }}
              </div>
              <div v-if="r3JusticePct" class="r3-chip" :class="r3ModClass(r3JusticePct)">
                ⚖ {{ fmtPct(r3JusticePct) }}
              </div>
              <div v-if="r3NemesisPct" class="r3-chip" :class="r3ModClass(r3NemesisPct)">
                ◆ {{ fmtPct(r3NemesisPct) }}
              </div>
            </div>
            <div class="medallion-connector" :class="{ revealed: props.showFinalResult }"></div>

            <!-- Outcome medallion -->
            <div v-if="props.showFinalResult" class="medallion outcome-medallion" :class="props.leftWon ? 'outcome-win' : 'outcome-loss'">
              <span class="outcome-star">{{ props.leftWon ? '\u2605' : '\u2716' }}</span>
              <span class="outcome-text">{{ props.leftWon ? 'VICTORY' : 'DEFEAT' }}</span>
            </div>
            <div v-else class="medallion outcome-medallion phase-outcome-pending">
              <span class="outcome-star">?</span>
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
            'recoil-loser': (props.clashPhase === 'recoil' || props.clashPhase === 'settled') && props.showFinalResult && props.leftWon,
            'recoil-winner': (props.clashPhase === 'recoil' || props.clashPhase === 'settled') && props.showFinalResult && !props.leftWon,
          }
        ]" :style="{ '--char-bg': `url(${props.getDisplayAvatar(props.rightAvatar, props.rightName)})` }">
          <div class="char-class-corner corner-left" :class="classColor(rightClass)">
            {{ classAbbrev(rightClass) }}
          </div>
          <img :src="props.getDisplayAvatar(props.rightAvatar, props.rightName)" class="char-avatar"
            @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
          <div class="char-info">
            <span class="char-name">{{ props.getDisplayCharName(props.rightCharName, props.rightName) }}</span>
            <span class="char-player-name">{{ props.rightName }}</span>
          </div>
          <!-- Factor tags (bad / neutral-negative) -->
          <div class="factor-tags factor-tags-right" v-if="isMyFight">
            <div
              v-for="(factor, idx) in rightFactors"
              :key="'rf-' + idx"
              class="factor-tag factor-bad"
              :class="{
                visible: showR1Factor(props.round1Factors.indexOf(factor)),
                'skip-anim': skippedToEnd
              }"
              :title="factor.detail"
            >
              <span class="factor-tag-label">{{ factor.label }}</span>
              <span class="factor-tag-arrows">{{ '\u25BC'.repeat(Math.max(1, factor.tier || 1)) }}</span>
            </div>
          </div>
          <!-- Negative enchant badges (bottom-left of enemy card) -->
          <div class="enchant-overlay enchant-overlay-left" v-if="isMyFight && hasEnemyBadges" :class="{ visible: props.showR1Result }">
            <div
              v-for="(badge, bIdx) in enemyDebuffBadges"
              :key="'ebd-' + bIdx"
              class="enchant-pill enchant-debuff"
            >
              <span v-if="badge.arrow" class="enchant-arrow">{{ badge.arrow }}</span>
              {{ badge.text }}
            </div>
          </div>
        </div>

        <!-- KO Impact VFX -->
        <div v-if="props.isMyFight && (props.clashPhase === 'hit' || props.clashPhase === 'recoil')" class="clash-sparks">
          <div v-for="i in 8" :key="'spark-' + i" class="clash-spark"
            :style="{
              '--dx': (Math.cos((i - 1) * Math.PI / 4) * (40 + (i % 3) * 20)) + 'px',
              '--dy': (Math.sin((i - 1) * Math.PI / 4) * (30 + (i % 2) * 25) - 10) + 'px',
              '--delay': ((i % 3) * 0.03) + 's',
            }"
          ></div>
        </div>

        <!-- Speed lines -->
        <div v-if="props.isMyFight && props.clashPhase === 'hit'" class="clash-speed-lines">
          <div v-for="i in 5" :key="'line-' + i" class="clash-speed-line"
            :style="{ '--rot': ((i - 1) * 36 - 72) + 'deg' }"
          ></div>
        </div>
      </div>

      <!-- R3 Random Roll (Needle) -->
      <template v-if="props.isMyFight && props.fight.usedRandomRoll">
        <div v-if="props.showR3" class="fa-r3-details">
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
  --accent-gold: #b8860b;
  --accent-gold-light: #d4a839;
  --accent-gold-dim: rgba(184, 134, 11, 0.3);

  background: var(--bg-inset);
  border: 2px solid var(--accent-gold);
  border-radius: 10px;
  padding: 6px 8px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  position: relative;
  overflow: clip;
  flex: 1;
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
/* Corner accent glow */
.arena::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 10px;
  box-shadow:
    inset 3px 3px 0 -1px rgba(184,134,11,0.3),
    inset -3px -3px 0 -1px rgba(184,134,11,0.3);
  pointer-events: none;
  z-index: 10;
}

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
  0%, 100% { transform: translate(0, 0); }
  10% { transform: translate(-4px, 2px); }
  20% { transform: translate(4px, -2px); }
  30% { transform: translate(-3px, -1px); }
  40% { transform: translate(3px, 1px); }
  50% { transform: translate(-2px, 2px); }
  60% { transform: translate(2px, -1px); }
  70% { transform: translate(-1px, 1px); }
}

/* ── Arena Grid (3-column: card | center | card) ── */
.arena-grid {
  display: grid;
  grid-template-columns: 1fr 140px 1fr;
  gap: 0;
  align-items: stretch;
  flex: 1;
  position: relative;
}

/* ── Clash Animation (KO Impact) ── */
.arena-grid {
  --clash-offset: 70px;
}

/* Phase 1: Slam — cards translate inward (doubled class for specificity over removed media queries) */
.arena-grid.clashed .char-card.char-card-left {
  transform: translateX(var(--clash-offset));
  transition: transform 0.4s ease-in;
  z-index: 5;
}
.arena-grid.clashed .char-card.char-card-right {
  transform: translateX(calc(-1 * var(--clash-offset)));
  transition: transform 0.4s ease-in;
  z-index: 5;
}
.arena-grid.clashed .arena-center {
  z-index: 1;
}

/* Phase 2: Hit-stop — freeze in place, impact flash triggers */

/* Impact flash overlay */
.arena-grid.clash-hit::before {
  content: '';
  position: absolute;
  top: 0;
  left: 50%;
  transform: translateX(-50%);
  width: 140px;
  height: 100%;
  background: radial-gradient(ellipse at center, rgba(255, 240, 200, 0.8) 0%, rgba(255, 215, 0, 0.4) 40%, transparent 70%);
  z-index: 25;
  pointer-events: none;
  animation: impact-flash 0.15s ease-out forwards;
}
@keyframes impact-flash {
  0% { opacity: 1; }
  100% { opacity: 0; }
}

/* Phase 3: Recoil — loser flies back with overshoot, winner holds position */
/* No static transform needed — the keyframe animation controls transform throughout */
.arena-grid.clashed .char-card-left.recoil-loser {
  animation: recoil-left 0.4s ease-out forwards, loser-red-flash 0.3s ease-out;
}
.arena-grid.clashed .char-card-right.recoil-loser {
  animation: recoil-right 0.4s ease-out forwards, loser-red-flash 0.3s ease-out;
}

@keyframes recoil-left {
  0% { transform: translateX(var(--clash-offset)); }
  40% { transform: translateX(-15px); }
  70% { transform: translateX(6px); }
  90% { transform: translateX(-2px); }
  100% { transform: translateX(0); }
}
@keyframes recoil-right {
  0% { transform: translateX(calc(-1 * var(--clash-offset))); }
  40% { transform: translateX(15px); }
  70% { transform: translateX(-6px); }
  90% { transform: translateX(2px); }
  100% { transform: translateX(0); }
}
/* Winner stays at clashed position — holds ground */

/* Loser red flash overlay */
@keyframes loser-red-flash {
  0% { box-shadow: inset 0 0 40px rgba(239, 68, 68, 0.4); }
  100% { box-shadow: inset 0 0 0 rgba(239, 68, 68, 0); }
}

/* Skip clash: instant position, no animation (5 classes beats slam's 4) */
.arena-grid.clashed.skip-clash .char-card.char-card-left,
.arena-grid.clashed.skip-clash .char-card.char-card-right {
  transition: none;
  transform: none;
  animation: none;
}
.arena-grid.clashed.skip-clash::before {
  animation: none;
  display: none;
}

/* Hover to reveal phase tracker (:hover pseudo-class adds specificity, beats slam + recoil) */
.arena-grid.clashed:hover .char-card.char-card-left,
.arena-grid.clashed:hover .char-card.char-card-right {
  transform: translateX(0);
  transition: transform 0.4s cubic-bezier(0.34, 1.56, 0.64, 1);
  animation: none;
}
.arena-grid.clashed:hover::before {
  animation: none;
  display: none;
}

/* Note: prefers-reduced-motion intentionally not used here —
   this game's visual feedback is core to the experience */

/* ── KO Spark Particles ── */
.clash-sparks {
  position: absolute;
  top: 50%;
  left: 50%;
  z-index: 30;
  pointer-events: none;
}

.clash-spark {
  position: absolute;
  width: 5px;
  height: 5px;
  background: radial-gradient(circle, #fff 0%, #ffd700 60%, transparent 100%);
  border-radius: 1px;
  animation: spark-fly 0.4s var(--delay, 0s) ease-out forwards;
}

@keyframes spark-fly {
  0% {
    transform: translate(0, 0) scale(1);
    opacity: 1;
  }
  100% {
    transform: translate(var(--dx), var(--dy)) scale(0);
    opacity: 0;
  }
}

/* ── KO Speed Lines ── */
.clash-speed-lines {
  position: absolute;
  top: 50%;
  left: 50%;
  z-index: 15;
  pointer-events: none;
}

.clash-speed-line {
  position: absolute;
  width: 60px;
  height: 2px;
  background: linear-gradient(90deg, rgba(255, 255, 255, 0.5) 0%, transparent 100%);
  transform-origin: left center;
  transform: rotate(var(--rot));
  animation: speed-line-flash 0.3s ease-out forwards;
}

@keyframes speed-line-flash {
  0% {
    opacity: 0;
    width: 0;
  }
  30% {
    opacity: 0.6;
    width: 60px;
  }
  100% {
    opacity: 0;
    width: 80px;
  }
}

/* ── Character Cards (art-filled halves) ── */
.char-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0;
  padding: 0;
  position: relative;
  overflow: hidden;
  min-height: 300px;
  transition: border-color 0.4s ease, box-shadow 0.4s ease, filter 0.4s ease;
}
/* Art background via CSS variable */
.char-card::before {
  content: '';
  position: absolute;
  inset: 0;
  background-image: var(--char-bg);
  background-size: cover;
  background-position: center top;
  background-repeat: no-repeat;
  z-index: 0;
}
/* Vignette overlay */
.char-card::after {
  content: '';
  position: absolute;
  inset: 0;
  background:
    radial-gradient(ellipse at center, transparent 40%, rgba(0, 0, 0, 0.45) 100%),
    linear-gradient(to top, rgba(0, 0, 0, 0.6) 0%, transparent 40%);
  pointer-events: none;
  z-index: 1;
}
/* Ensure children sit above the background and vignette */
.char-card > * {
  position: relative;
  z-index: 2;
}

/* Left/right border styling */
.char-card-left {
  border-left: 2px solid #b8860b;
  border-top: 2px solid #b8860b;
  border-bottom: 2px solid #b8860b;
  border-right: none;
  border-radius: 8px 0 0 8px;
}
.char-card-right {
  border-right: 2px solid #b8860b;
  border-top: 2px solid #b8860b;
  border-bottom: 2px solid #b8860b;
  border-left: none;
  border-radius: 0 8px 8px 0;
}

.char-card.class-int { border-color: rgba(110, 170, 240, 0.4); }
.char-card.class-str { border-color: rgba(239, 128, 128, 0.4); }
.char-card.class-spd { border-color: rgba(200, 185, 50, 0.4); }

.char-card.winner {
  border-color: #22c55e;
  box-shadow: inset 0 0 30px rgba(34, 197, 94, 0.15), 0 0 12px rgba(34, 197, 94, 0.3);
}
.char-card.loser {
  filter: brightness(0.7) saturate(0.6);
}
.char-card.loser::before {
  filter: brightness(0.7) saturate(0.5);
}
.char-card.dropped {
  animation: half-fade-out 0.9s cubic-bezier(0.55, 0, 1, 0.45) forwards;
}
@keyframes half-fade-out {
  0% { opacity: 1; filter: none; }
  100% { opacity: 0.3; filter: brightness(0.4) saturate(0); }
}

/* ── Character Class Corner Badge ── */
.char-class-corner {
  position: absolute;
  z-index: 5;
  font-size: 9px;
  font-weight: 900;
  padding: 2px 6px;
  letter-spacing: 0.5px;
  text-transform: uppercase;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(4px);
}

.corner-right {
  top: 0;
  right: 0;
  border-radius: 0 0 0 6px;
  border-left: 1px solid rgba(184, 134, 11, 0.4);
  border-bottom: 1px solid rgba(184, 134, 11, 0.4);
}

.corner-left {
  top: 0;
  left: 0;
  border-radius: 0 0 6px 0;
  border-right: 1px solid rgba(184, 134, 11, 0.4);
  border-bottom: 1px solid rgba(184, 134, 11, 0.4);
}

.char-class-corner.class-int { color: var(--kh-c-secondary-info-200); }
.char-class-corner.class-str { color: var(--kh-c-secondary-danger-200); }
.char-class-corner.class-spd { color: var(--kh-c-text-highlight-dim); }

.char-avatar {
  position: absolute;
  width: 1px;
  height: 1px;
  opacity: 0;
  pointer-events: none;
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

/* Character card entrance (fade + scale for full-width halves) */
.char-card.entrance-active {
  animation: half-entrance 0.4s cubic-bezier(0.34, 1.56, 0.64, 1) both;
}

@keyframes half-entrance {
  0% { opacity: 0; transform: scale(1.1); }
  100% { opacity: 1; transform: scale(1); }
}

/* ── Arena Center ── */
.arena-center {
  background: linear-gradient(180deg, rgba(30,25,15,0.95), rgba(20,18,12,0.9));
  border-left: 1px solid rgba(184, 134, 11, 0.3);
  border-right: 1px solid rgba(184, 134, 11, 0.3);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 8px 4px;
  z-index: 2;
  gap: 6px;
  min-width: 0;
  align-self: stretch;
}

.arena-special .arena-center {
  justify-content: center;
}


/* ── Phase Tracker (Vertical Medallion Stack) ── */
.phase-tracker-vertical {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0;
  padding: 12px 0;
  z-index: 5;
}

.medallion {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 60px;
  height: 60px;
  border-radius: 50%;
  border: 2px solid #b8860b;
  background: linear-gradient(145deg, rgba(45,35,20,0.95), rgba(30,25,15,0.95));
  position: relative;
  transition: all 0.3s ease;
}

/* Role badge medallion */
.role-badge {
  width: 48px;
  height: 48px;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 1px;
  text-transform: uppercase;
}
.phase-role-text { font-size: 8px; font-weight: 900; letter-spacing: 0.5px; }
.role-atk {
  color: #ef4444;
  border-color: #dc2626;
  background: linear-gradient(145deg, rgba(60,20,20,0.95), rgba(40,15,15,0.95));
}
.role-atk .phase-role-text { color: #ef4444; }
.role-def {
  color: #60a5fa;
  border-color: #3b82f6;
  background: linear-gradient(145deg, rgba(20,30,60,0.95), rgba(15,20,40,0.95));
}
.role-def .phase-role-text { color: #60a5fa; }
.role-icon {
  font-size: 14px;
  margin-top: 2px;
}

/* Phase medallion labels and icons */
.phase-medallion .medallion-label {
  font-size: 8px;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: rgba(255,255,255,0.5);
}
.phase-medallion .medallion-icon {
  font-size: 20px;
  font-weight: 900;
}

/* Vertical connectors */
.medallion-connector {
  width: 2px;
  height: 16px;
  background: rgba(184,134,11,0.3);
  transition: background 0.4s ease;
  flex-shrink: 0;
}
.medallion-connector.revealed {
  background: #b8860b;
}
.medallion-connector.broken {
  background: none;
  border-left: 2px dashed rgba(184,134,11,0.3);
  width: 0;
}

/* Outcome medallion */
.outcome-medallion {
  width: 72px;
  height: 72px;
  border-width: 3px;
}
.outcome-medallion .outcome-text {
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 1px;
  text-transform: uppercase;
  white-space: nowrap;
}
.outcome-medallion .outcome-star {
  font-size: 18px;
  margin-top: 2px;
}

/* Phase result colors on medallions */
.medallion.phase-ours { border-color: #22c55e; box-shadow: inset 0 0 12px rgba(34,197,94,0.3), 0 0 8px rgba(34,197,94,0.2); animation: phase-pop 0.4s ease; }
.medallion.phase-theirs { border-color: #ef4444; box-shadow: inset 0 0 12px rgba(239,68,68,0.3), 0 0 8px rgba(239,68,68,0.2); animation: phase-pop 0.4s ease; }
.medallion.phase-draw { border-color: #f59e0b; box-shadow: inset 0 0 8px rgba(245,158,11,0.2); animation: phase-pop 0.4s ease; }
.medallion.phase-skipped { opacity: 0.3; border-style: dashed; }

/* Outcome result states */
.outcome-win { border-color: #22c55e; color: #22c55e; box-shadow: 0 0 16px rgba(34,197,94,0.4), inset 0 0 12px rgba(34,197,94,0.2); animation: phase-pop 0.4s ease; }
.outcome-loss { border-color: #ef4444; color: #ef4444; box-shadow: 0 0 12px rgba(239,68,68,0.3), inset 0 0 8px rgba(239,68,68,0.2); animation: phase-pop 0.4s ease; }
.phase-outcome-pending { opacity: 0.3; }
.phase-outcome-pending .outcome-star { color: var(--text-dim); }

/* Block/skip outcome classes on medallions */
.medallion.outcome-attacker { border-color: rgba(63, 167, 61, 0.5); box-shadow: inset 0 0 12px rgba(63, 167, 61, 0.2); }
.medallion.outcome-defender { border-color: rgba(239, 128, 128, 0.4); box-shadow: inset 0 0 12px rgba(239, 128, 128, 0.2); }
.medallion.outcome-neutral { border-color: rgba(230, 148, 74, 0.4); box-shadow: inset 0 0 8px rgba(230, 148, 74, 0.2); }
.outcome-neutral .outcome-text { color: var(--accent-orange); }
.outcome-attacker .outcome-text { color: var(--accent-green); }
.outcome-defender .outcome-text { color: var(--accent-red); }

/* Phase icons */
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
  z-index: 11;
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

/* ── Tug-of-War Bar (vertical, inside phase tracker) ── */
.tug-of-war-vertical {
  width: 8px;
  height: 40px;
  flex-shrink: 0;
}

.tug-track-v {
  width: 100%;
  height: 100%;
  border-radius: 4px;
  background: var(--bg-inset);
  border: 1px solid rgba(184, 134, 11, 0.3);
  overflow: hidden;
  position: relative;
  display: flex;
  flex-direction: column-reverse; /* fill from bottom to top */
}

.tug-fill-v {
  width: 100%;
  border-radius: 3px;
  transition: height 0.4s cubic-bezier(0.22, 1, 0.36, 1), background 0.3s;
  position: relative;
}

.tug-fill-v.tug-good { background: linear-gradient(0deg, rgba(63, 167, 61, 0.6), rgba(63, 167, 61, 0.9)); }
.tug-fill-v.tug-bad { background: linear-gradient(0deg, rgba(239, 128, 128, 0.6), rgba(239, 128, 128, 0.9)); }
.tug-fill-v.tug-even { background: linear-gradient(0deg, rgba(230, 148, 74, 0.5), rgba(230, 148, 74, 0.8)); }

.tug-particles-v {
  position: absolute;
  top: 0;
  left: -2px;
  right: -2px;
  height: 6px;
  background: radial-gradient(circle at center, rgba(255, 255, 255, 0.4) 0%, transparent 70%);
  animation: tug-glow 0.6s ease-in-out infinite alternate;
}
@keyframes tug-glow {
  0% { opacity: 0.3; }
  100% { opacity: 0.8; }
}

/* ── Enchantment Pill Badges (overlaid on char-cards) ── */
.enchant-overlay {
  position: absolute;
  bottom: 8px;
  display: flex;
  flex-direction: column;
  gap: 2px;
  z-index: 6;
  opacity: 0;
  transition: opacity 0.3s ease;
}

.enchant-overlay.visible {
  opacity: 1;
}

.enchant-overlay-right {
  right: 6px;
  align-items: flex-end;
}

.enchant-overlay-left {
  left: 6px;
  align-items: flex-start;
}

.enchant-pill {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 8px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  white-space: nowrap;
  backdrop-filter: blur(4px);
}

.enchant-buff {
  background: rgba(63, 167, 61, 0.2);
  color: #4ade80;
  border: 1px solid rgba(63, 167, 61, 0.4);
}

.enchant-debuff {
  background: rgba(239, 68, 68, 0.2);
  color: #f87171;
  border: 1px solid rgba(239, 68, 68, 0.4);
}

.enchant-arrow {
  font-size: 8px;
  line-height: 1;
}

/* ── Justice Icon (compact display) ── */
.justice-icon {
  font-size: 16px;
  opacity: 0.8;
}

/* Slam impact shake (applied via dynamic class on justice-display) */
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

/* ── R3 details ── */
.fa-r3-details { display: flex; flex-direction: column; gap: 4px; }
.pct-good { color: var(--accent-green); font-weight: 700; }
.pct-bad { color: var(--accent-red); font-weight: 700; }

/* ── Roll bar (needle) ── */
.fa-roll-bar-wrap { margin-top: 3px; padding-top: 3px; padding-bottom: 14px; }
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

/* ── Factor Tags (overlaid on char-cards) ── */
.factor-tags {
  position: absolute;
  bottom: 8px;
  display: flex;
  flex-direction: column;
  gap: 3px;
  z-index: 5;
  max-width: 85%;
}

.factor-tags-left {
  left: 6px;
}

.factor-tags-right {
  right: 6px;
  align-items: flex-end;
}

.factor-tag {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(4px);
  border: 1px solid rgba(184, 134, 11, 0.4);
  opacity: 0;
  transform: translateX(-20px);
  transition: opacity 0.3s ease, transform 0.3s ease;
  cursor: pointer;
  white-space: nowrap;
}

.factor-tags-right .factor-tag {
  transform: translateX(20px);
}

.factor-tag.visible {
  opacity: 1;
  transform: translateX(0);
}

.factor-tag.skip-anim {
  opacity: 1;
  transform: translateX(0);
  transition: none;
}

.factor-tag-label {
  color: rgba(255, 255, 255, 0.9);
}

.factor-tag-arrows {
  font-size: 10px;
  letter-spacing: -1px;
}

.factor-good .factor-tag-arrows {
  color: #4ade80;
}

.factor-bad .factor-tag-arrows {
  color: #f87171;
}

.factor-tag:hover {
  background: rgba(0, 0, 0, 0.8);
  z-index: 10;
}

/* ── Compact Justice Display (inside phase tracker) ── */
.justice-display {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  margin: 4px 0;
}

.justice-value-side {
  display: flex;
  align-items: center;
  gap: 3px;
  padding: 3px 8px;
  border-radius: 12px;
  font-size: 14px;
  font-weight: 800;
  background: rgba(139, 92, 246, 0.15);
  border: 1px solid rgba(139, 92, 246, 0.3);
  transition: all 0.3s ease;
}

.justice-display .justice-vs {
  font-size: 10px;
  font-weight: 900;
  color: rgba(255,255,255,0.4);
}

.j-winner {
  border-color: #22c55e;
  background: rgba(34, 197, 94, 0.15);
  color: #4ade80;
  box-shadow: 0 0 8px rgba(34, 197, 94, 0.3);
}

.j-loser {
  opacity: 0.5;
  filter: saturate(0.5);
}

/* ── R3 Modifier Chips (inside phase tracker) ── */
.r3-modifier-chips {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  margin-top: 4px;
}

.r3-chip {
  padding: 2px 8px;
  border-radius: 8px;
  font-size: 10px;
  font-weight: 600;
  background: rgba(0, 0, 0, 0.5);
  border: 1px solid rgba(184, 134, 11, 0.3);
  white-space: nowrap;
}

.r3-chip.pct-good {
  color: #4ade80;
  border-color: rgba(34, 197, 94, 0.4);
}

.r3-chip.pct-bad {
  color: #f87171;
  border-color: rgba(239, 68, 68, 0.4);
}

/* ── Responsive ── */
@media (max-width: 600px) {
  .arena-grid {
    grid-template-columns: 1fr 120px 1fr;
  }
  .arena-grid { --clash-offset: 60px; }
  .char-card { min-height: 260px; }
  .medallion { width: 52px; height: 52px; }
  .outcome-medallion { width: 64px; height: 64px; }
  .factor-tag { font-size: 10px; padding: 3px 8px; }
  .justice-display { gap: 3px; }
  .justice-value-side { font-size: 12px; padding: 2px 6px; }
  .r3-chip { font-size: 9px; padding: 2px 6px; }
  .enchant-pill { font-size: 7px; padding: 2px 6px; }
}
@media (max-width: 400px) {
  .arena-grid {
    grid-template-columns: 1fr 100px 1fr;
  }
  .arena-grid { --clash-offset: 50px; }
  .char-card { min-height: 220px; }
  .char-name, .char-player-name {
    max-width: 60px;
    font-size: 8px;
  }
  .medallion { width: 44px; height: 44px; }
  .role-badge { width: 36px; height: 36px; }
  .outcome-medallion { width: 56px; height: 56px; }
  .medallion-connector { height: 10px; }
  .phase-medallion .medallion-icon { font-size: 16px; }
  .phase-medallion .medallion-label { font-size: 7px; }
  .outcome-medallion .outcome-text { font-size: 7px; }
  .outcome-medallion .outcome-star { font-size: 14px; }
  .phase-icon { font-size: 10px; }
  .factor-tag { font-size: 9px; padding: 3px 7px; }
  .justice-display { gap: 2px; }
  .justice-value-side { font-size: 11px; padding: 2px 5px; }
  .justice-display .justice-vs { font-size: 8px; }
  .r3-chip { font-size: 9px; padding: 2px 5px; }
  .enchant-pill { font-size: 7px; padding: 1px 5px; }
}
</style>
