<script setup lang="ts">
import { computed } from 'vue'
import type { FightEntry, ForOneFightMod } from 'src/services/signalr'

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

// ── Local helpers (were inline in old FightAnimation.vue) ──
function outcomeLabel(f: FightEntry): string {
  switch (f.outcome) {
    case 'block': return 'БЛОК'
    case 'skip': return 'СКИП'
    case 'win': case 'loss': return `${f.winnerName} ПОБЕДИЛ`
    default: return ''
  }
}
function outcomeClass(f: FightEntry): string {
  if (f.outcome === 'block' || f.outcome === 'skip') return 'outcome-neutral'
  return f.outcome === 'win' ? 'outcome-attacker' : 'outcome-defender'
}

const showR1Factor = (idx: number) => {
  if (props.skippedToEnd || !props.isMyFight) return true
  return props.currentStep > idx
}

function fmtVal(v: number): string {
  return (v > 0 ? '+' : '') + v.toFixed(1)
}
function fmtPct(v: number): string {
  return (v > 0 ? '+' : '') + v.toFixed(2) + '%'
}
function r3ModClass(v: number): string {
  return v > 0 ? 'pct-good' : v < 0 ? 'pct-bad' : ''
}

// Justice block layout (unused since justiceUseBlocks is false, kept for completeness)
function justiceBlockLayout(j: number): { front: number; back: number; top?: number } {
  if (j >= 10) return { front: 3, back: 2, top: 1 }
  return { front: j <= 2 ? 1 : j <= 4 ? 2 : 3, back: 0, top: 0 }
}
const ourJusticeLayout = computed(() => justiceBlockLayout(props.ourJustice))
const enemyJusticeLayout = computed(() => justiceBlockLayout(props.enemyJustice))
const justiceUseBlocks = false
</script>

<template>
  <div class="fa-card" :class="{ 'fa-shake': fightShake, 'fa-result-win': fightResult === 'win', 'fa-result-loss': fightResult === 'loss' }">
    <!-- Block/Skip -->
    <div v-if="isSpecialOutcome" class="fa-special">
      <div class="fa-bar-container">
        <div class="fa-id-left">
          <img :src="getDisplayAvatar(leftAvatar, leftName)" class="fa-ava-sm" @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
          <div class="fa-id-info">
            <span class="fa-id-name">{{ leftName }}</span>
          </div>
        </div>
        <div class="phase-tracker phase-tracker-inline">
          <div class="phase-role-pip" :class="attackedRight ? 'role-atk' : 'role-def'">
            <span class="phase-role-text">{{ attackedRight ? 'ATK' : 'DEF' }}</span>
          </div>
          <div class="phase-connector revealed"></div>
          <div class="phase-outcome-pip" :class="outcomeClass(fight)">
            <span class="phase-outcome-text">{{ outcomeLabel(fight) }}</span>
          </div>
        </div>
        <div class="fa-id-right">
          <div class="fa-id-info" style="text-align:right">
            <span class="fa-id-name">{{ rightName }}</span>
          </div>
          <img :src="getDisplayAvatar(rightAvatar, rightName)" class="fa-ava-sm" @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
        </div>
      </div>
    </div>

    <!-- Normal fight -->
    <template v-else>
      <!-- Compact scale row: avatar+name | bar | avatar+name -->
      <div class="fa-bar-container">
        <div class="fa-id-left" :class="{ winner: leftWon, 'entrance-active': currentStep === 0 && !skippedToEnd }">
          <img :src="getDisplayAvatar(leftAvatar, leftName)" class="fa-ava-sm" @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
          <div class="fa-id-info">
            <span class="fa-id-name">{{ leftName }}</span>
            <span class="fa-id-char">{{ getDisplayCharName(leftCharName, leftName) }}</span>
          </div>
        </div>
        <!-- Phase tracker between avatars -->
        <div v-if="isMyFight" class="phase-tracker phase-tracker-inline">
          <div class="phase-role-pip" :class="attackedRight ? 'role-atk' : 'role-def'">
            <span class="phase-role-text">{{ attackedRight ? 'ATK' : 'DEF' }}</span>
          </div>
          <div class="phase-connector revealed"></div>
          <div class="phase-pip" :class="phaseClass(phase1Result, showR1Result)">
            <span v-if="!showR1Result" class="phase-icon phase-icon-pending">?</span>
            <span v-else-if="phase1Result > 0" class="phase-icon phase-icon-win">✓</span>
            <span v-else-if="phase1Result < 0" class="phase-icon phase-icon-lose">✗</span>
            <span v-else class="phase-icon phase-icon-draw">—</span>
          </div>
          <div class="phase-connector" :class="{ revealed: showR2 }"></div>
          <div class="phase-pip" :class="phaseClass(phase2Result, phase2Revealed)">
            <span v-if="!phase2Revealed" class="phase-icon phase-icon-pending">?</span>
            <span v-else-if="phase2Result > 0" class="phase-icon phase-icon-win">✓</span>
            <span v-else-if="phase2Result < 0" class="phase-icon phase-icon-lose">✗</span>
            <span v-else class="phase-icon phase-icon-draw">—</span>
          </div>
          <div class="phase-connector" :class="{ revealed: fight?.usedRandomRoll && showR3, broken: !fight?.usedRandomRoll }"></div>
          <div class="phase-pip" :class="[phaseClass(phase3Result, fight?.usedRandomRoll ? phase3Revealed : false), { 'phase-skipped': !fight?.usedRandomRoll }]">
            <template v-if="!fight?.usedRandomRoll">
              <span class="phase-icon phase-icon-skip">—</span>
            </template>
            <template v-else>
              <span v-if="!phase3Revealed" class="phase-icon phase-icon-pending">?</span>
              <span v-else-if="phase3Result > 0" class="phase-icon phase-icon-win">✓</span>
              <span v-else class="phase-icon phase-icon-lose">✗</span>
            </template>
          </div>
          <div class="phase-connector phase-connector-outcome" :class="{ revealed: showFinalResult }"></div>
          <div v-if="showFinalResult" class="phase-outcome-pip" :class="leftWon ? 'phase-ours' : 'phase-theirs'">
            <span class="phase-outcome-text">{{ leftWon ? 'Victory' : 'Defeat' }}</span>
          </div>
          <div v-else class="phase-outcome-pip phase-outcome-pending">
            <span class="phase-outcome-text">?</span>
          </div>
        </div>
        <!-- Fallback bar for enemy fights -->
        <div v-else class="fa-bar-track">
          <div class="fa-bar-fill" :style="{ width: barPosition + '%' }" :class="{ 'bar-attacker': animatedWeighingValue > 0, 'bar-defender': animatedWeighingValue < 0, 'bar-even': animatedWeighingValue === 0 }">
          </div>
        </div>
        <div class="fa-id-right" :class="{ winner: !leftWon && (fight.outcome === 'win' || fight.outcome === 'loss'), 'entrance-active': currentStep === 0 && !skippedToEnd }">
          <div class="fa-id-info" style="text-align:right">
            <span class="fa-id-name">{{ rightName }}</span>
            <span class="fa-id-char">{{ getDisplayCharName(rightCharName, rightName) }}</span>
          </div>
          <img :src="getDisplayAvatar(rightAvatar, rightName)" class="fa-ava-sm" @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
        </div>
      </div>

      <!-- MY FIGHT: Full 3-round detail -->
      <template v-if="isMyFight">
        <!-- Weighing bar -->
        <div class="fa-bar-container fa-bar-compact">
          <div class="fa-bar-track">
            <div class="fa-bar-fill" :style="{ width: barPosition + '%' }" :class="{ 'bar-attacker': animatedWeighingValue > 0, 'bar-defender': animatedWeighingValue < 0, 'bar-even': animatedWeighingValue === 0, 'bar-particles-gold': barPosition > 50, 'bar-particles-red': barPosition < 50 }">
            </div>
          </div>
        </div>
        <div class="fa-factors">
          <div v-for="(fac, idx) in round1Factors" :key="'r1-'+idx"
            class="fa-factor" :class="[fac.highlight, { visible: showR1Factor(idx) }, fac.tier != null ? 'tier-' + fac.tier : '']">
            <span class="fa-factor-label">{{ fac.label }}</span>
            <span class="fa-factor-detail" v-html="fac.detail"></span>
            <span class="fa-factor-value" v-if="fac.value !== 0 && fac.showValue !== false">{{ fmtVal(fac.value) }}</span>
          </div>

          <!-- TooGood / TooStronk / Skill Multiplier / ForOneFight badges -->
          <div v-if="(fight.isTooGoodMe || fight.isTooGoodEnemy) || (fight.isTooStronkMe || fight.isTooStronkEnemy) || ourSkillMultiplier > 1 || ourForOneFightMods.length > 0 || enemyModsOnUs.length > 0 || classChangeBadge" class="fa-badge-row" :class="{ visible: showR1Result }">
            <span v-if="fight.isTooGoodMe || fight.isTooGoodEnemy" class="fa-badge badge-toogood">TOO GOOD: {{ (fight.isTooGoodMe ? !isFlipped : isFlipped) ? 'МЫ' : 'ВРАГ' }}</span>
            <span v-if="fight.isTooStronkMe || fight.isTooStronkEnemy" class="fa-badge badge-toostronk">TOO STRONK: {{ (fight.isTooStronkMe ? !isFlipped : isFlipped) ? 'МЫ' : 'ВРАГ' }}</span>
            <span v-if="ourSkillMultiplier > 1" class="fa-badge badge-skill">SKILL MULTIPLIER x{{ ourSkillMultiplier }}</span>
            <span v-for="(mod, i) in ourForOneFightMods" :key="'fof-'+i"
              class="fa-badge" :class="isFofBuff(mod) ? 'badge-fof-buff' : 'badge-fof-debuff'">
              {{ fofBadgeText(mod) }}
            </span>
            <span v-for="(mod, i) in enemyModsOnUs" :key="'fof-enemy-'+i"
              class="fa-badge badge-fof-enemy">
              {{ enemyFofBadgeText(mod) }}
            </span>
            <span v-if="classChangeBadge" class="fa-badge badge-class-change">{{ classChangeBadge }}</span>
          </div>
        </div>

        <div v-if="showR2" class="fj-slam-wrap" :class="{ 'fj-slam-impact': slamPhase === 'impact' }">
          <template v-if="justiceUseBlocks">
            <!-- Block pyramid mode -->
            <div class="fj-slam-tower fj-slam-left" :class="{
              winner: slamPhase === 'resolved' && ourJustice > enemyJustice,
              loser: slamPhase === 'resolved' && ourJustice < enemyJustice,
              tied: slamPhase === 'resolved' && ourJustice === enemyJustice,
            }">
              <div v-if="ourJusticeLayout.top" class="fj-slam-row fj-slam-top">
                <div v-for="b in ourJusticeLayout.top" :key="'ot'+b" class="fj-block fj-block-ours"></div>
              </div>
              <div v-if="ourJusticeLayout.back" class="fj-slam-row fj-slam-back">
                <div v-for="b in ourJusticeLayout.back" :key="'ob'+b" class="fj-block fj-block-ours"></div>
              </div>
              <div class="fj-slam-row">
                <div v-for="b in ourJusticeLayout.front" :key="'of'+b" class="fj-block fj-block-ours"></div>
              </div>
            </div>
            <div class="fj-slam-vs" :class="{ visible: slamPhase === 'impact' || slamPhase === 'resolved' }">
              <span v-if="slamPhase === 'impact'" class="fj-slam-spark">⚖</span>
              <span v-else>vs</span>
            </div>
            <div class="fj-slam-tower fj-slam-right" :class="{
              winner: slamPhase === 'resolved' && enemyJustice > ourJustice,
              loser: slamPhase === 'resolved' && enemyJustice < ourJustice,
              tied: slamPhase === 'resolved' && ourJustice === enemyJustice,
            }">
              <div v-if="enemyJusticeLayout.top" class="fj-slam-row fj-slam-top">
                <div v-for="b in enemyJusticeLayout.top" :key="'et'+b" class="fj-block fj-block-enemy"></div>
              </div>
              <div v-if="enemyJusticeLayout.back" class="fj-slam-row fj-slam-back">
                <div v-for="b in enemyJusticeLayout.back" :key="'eb'+b" class="fj-block fj-block-enemy"></div>
              </div>
              <div class="fj-slam-row">
                <div v-for="b in enemyJusticeLayout.front" :key="'ef'+b" class="fj-block fj-block-enemy"></div>
              </div>
            </div>
          </template>
          <template v-else>
            <!-- Number mode -->
            <div class="fj-slam-num fj-slam-left" :class="{
              winner: slamPhase === 'resolved' && ourJustice > enemyJustice,
              loser: slamPhase === 'resolved' && ourJustice < enemyJustice,
              tied: slamPhase === 'resolved' && ourJustice === enemyJustice,
              'justice-winner': slamPhase === 'resolved' && justiceWinner === 'left',
              'justice-loser': slamPhase === 'resolved' && justiceWinner === 'right',
              'justice-tied-pulse': slamPhase === 'resolved' && justiceWinner === 'tie',
            }">{{ ourJustice }}</div>
            <div class="fj-slam-vs" :class="{ visible: slamPhase === 'impact' || slamPhase === 'resolved' }">
              <span v-if="slamPhase === 'impact'" class="fj-slam-spark">⚖</span>
              <span v-else>vs</span>
            </div>
            <div class="fj-slam-num fj-slam-right" :class="{
              winner: slamPhase === 'resolved' && enemyJustice > ourJustice,
              loser: slamPhase === 'resolved' && enemyJustice < ourJustice,
              tied: slamPhase === 'resolved' && ourJustice === enemyJustice,
              'justice-winner': slamPhase === 'resolved' && justiceWinner === 'right',
              'justice-loser': slamPhase === 'resolved' && justiceWinner === 'left',
              'justice-tied-pulse': slamPhase === 'resolved' && justiceWinner === 'tie',
            }">{{ enemyJustice }}</div>
            <div v-if="slamPhase === 'impact' || slamPhase === 'resolved'" class="impact-cracks">
              <div class="crack crack-1"></div>
              <div class="crack crack-2"></div>
              <div class="crack crack-3"></div>
              <div class="crack crack-4"></div>
            </div>
          </template>
        </div>

        <template v-if="fight.usedRandomRoll">
          <div v-if="showR3" class="fa-r3-details">
            <!-- Modifier: TooGood -->
            <div v-if="fight.tooGoodRandomChange !== 0" class="fa-factor random visible">
              <span class="fa-factor-label">TooGood</span>
              <span class="fa-factor-detail" :class="r3ModClass(fight.tooGoodRandomChange * sign)">
                {{ fmtPct(fight.tooGoodRandomChange * sign) }}
              </span>
            </div>
            <!-- Modifier: TooStronk -->
            <div v-if="fight.tooStronkRandomChange !== 0" class="fa-factor random visible">
              <span class="fa-factor-label">TooStronk</span>
              <span class="fa-factor-detail" :class="r3ModClass(fight.tooStronkRandomChange * sign)">
                {{ fmtPct(fight.tooStronkRandomChange * sign) }}
              </span>
            </div>
            <!-- Modifier: Justice -->
            <div v-if="fight.justiceRandomChange !== 0" class="fa-factor random visible">
              <span class="fa-factor-label">Justice</span>
              <span class="fa-factor-detail" :class="r3ModClass(r3JusticePct)">
                {{ fmtPct(r3JusticePct) }}
              </span>
            </div>
            <!-- Modifier: Nemesis -->
            <div v-if="fight.nemesisRandomChange !== 0" class="fa-factor random visible">
              <span class="fa-factor-label">Nemesis</span>
              <span class="fa-factor-detail" :class="r3ModClass(r3NemesisPct)">
                {{ fmtPct(r3NemesisPct) }}
              </span>
            </div>
            <!-- Modifier: Skill Difference -->
            <div v-if="fight.skillDifferenceRandomModifier !== 0" class="fa-factor random visible">
              <span class="fa-factor-label">Skill Difference</span>
              <span class="fa-factor-detail" :class="r3ModClass(fight.skillDifferenceRandomModifier * sign)">
                {{ fmtPct(fight.skillDifferenceRandomModifier * sign) }}
              </span>
            </div>
            <!-- Roll result: animated bar -->
            <div v-if="showR3" class="fa-roll-bar-wrap" :class="{ 'roll-overflow': r3Overflow }">
              <div class="fa-roll-bar-track" :class="{ 'track-overflow': r3Overflow }">
                <!-- Threshold marker at our win chance -->
                <div class="fa-roll-threshold" :style="{ left: r3DisplayChance + '%' }">
                  <span class="fa-roll-threshold-label">{{ r3Overflow ? '>100' : r3DisplayChance.toFixed(0) }}%</span>
                </div>
                <!-- Win zone (0 to threshold) -->
                <div class="fa-roll-zone-win"
                     :style="{ width: r3DisplayChance + '%' }"
                     :class="{ 'zone-overflow': r3Overflow }"></div>
                <!-- Roll needle animates in -->
                <div class="fa-roll-needle" :style="{ left: r3NeedlePos + '%' }" :class="[r3WeWon ? 'needle-win' : 'needle-lose', { 'needle-settled': r3NeedleSettled }]">
                  <span class="fa-roll-needle-val">{{ r3RollPct.toFixed(1) }}%</span>
                </div>
              </div>
            </div>
          </div>
        </template>
      </template>

      <!-- ENEMY FIGHT: Minimal view -->
      <template v-else>
        <div v-if="showFinalResult" class="fa-enemy-summary">
          <span v-if="fight.isTooGoodMe || fight.isTooGoodEnemy" class="fa-badge badge-toogood">TOO GOOD</span>
          <span v-if="fight.isTooStronkMe || fight.isTooStronkEnemy" class="fa-badge badge-toostronk">TOO STRONK</span>
        </div>
      </template>

      <!-- Result badge (corner icon) -->
      <Transition name="result-badge">
        <span v-if="fightResult === 'win'" class="fa-result-badge fa-result-badge-win">✓</span>
        <span v-else-if="fightResult === 'loss'" class="fa-result-badge fa-result-badge-loss">✗</span>
      </Transition>

      <!-- Final result details -->
      <div v-if="showFinalResult" class="fa-result">
        <div v-if="isMyFight" class="fa-result-details">
          <!-- Skill gained: only show when WE are the attacker -->
          <span v-if="!isFlipped && fight.skillGainedFromTarget > 0" class="fa-detail-item fa-skill-gain">
            +{{ fight.skillGainedFromTarget }} Скилл (Мишень)
          </span>
          <span v-if="!isFlipped && fight.skillGainedFromClassAttacker > 0" class="fa-detail-item fa-skill-gain">
            +{{ fight.skillGainedFromClassAttacker }} Скилл (Класс)
          </span>
          <span v-if="isFlipped && fight.skillGainedFromClassDefender > 0" class="fa-detail-item fa-skill-gain">
            +{{ fight.skillGainedFromClassDefender }} Скилл (Класс)
          </span>
          <!-- Moral -->
          <span v-if="ourMoralChange !== 0" class="fa-detail-item fa-moral">
            {{ ourMoralChange > 0 ? '+' : '' }}{{ ourMoralChange }} Мораль
          </span>
          <!-- Justice -->
          <span v-if="!leftWon && fight.justiceChange > 0" class="fa-detail-item fa-justice">
            +{{ fight.justiceChange }} Справедливость
          </span>
          <!-- Resist damage -->
          <template v-if="!leftWon && fight.qualityDamageApplied">
            <span v-if="fight.resistIntelDamage > 0" class="fa-detail-item fa-resist"><span class="gi gi-int">INT</span> <span class="gi gi-def">DEF</span> -{{ fight.resistIntelDamage }}</span>
            <span v-if="fight.resistStrDamage > 0" class="fa-detail-item fa-resist"><span class="gi gi-str">STR</span> <span class="gi gi-def">DEF</span> -{{ fight.resistStrDamage }}</span>
            <span v-if="fight.resistPsycheDamage > 0" class="fa-detail-item fa-resist"><span class="gi gi-psy">PSY</span> <span class="gi gi-def">DEF</span> -{{ fight.resistPsycheDamage }}</span>
          </template>
          <!-- Intellectual / Emotional damage -->
          <span v-if="!leftWon && fight.intellectualDamage" class="fa-detail-item fa-dmg-us">
            <span class="gi gi-int">INT</span> Intellectual Damage!
          </span>
          <span v-if="leftWon && fight.intellectualDamage" class="fa-detail-item fa-dmg-enemy">
            <span class="gi gi-int">INT</span> {{ rightName }}: Intellectual Damage!
          </span>
          <span v-if="!leftWon && fight.emotionalDamage" class="fa-detail-item fa-dmg-us">
            <span class="gi gi-psy">PSY</span> Emotional Damage!
          </span>
          <span v-if="leftWon && fight.emotionalDamage" class="fa-detail-item fa-dmg-enemy">
            <span class="gi gi-psy">PSY</span> {{ rightName }}: Emotional Damage!
          </span>
          <!-- Drop (us losing) -->
          <span v-if="!leftWon && fight.drops > 0" class="fa-detail-item fa-drop">
             DROP x{{ fight.drops }}!
          </span>
          <!-- Drop (enemy losing) -->
          <span v-if="leftWon && fight.drops > 0" class="fa-detail-item fa-drop-enemy">
            {{ rightName }}: DROP x{{ fight.drops }}!
          </span>
        </div>
        <!-- Drops visible to ALL players -->
        <div v-else-if="fight.drops > 0" class="fa-result-details">
          <span class="fa-detail-item fa-drop">
            DROP {{ fight.droppedPlayerName }}! (-{{ fight.drops }})
          </span>
        </div>
      </div>
      <!-- Portal Gun swap indicator -->
      <div v-if="showFinalResult && isPortalSwap" class="portal-swap-overlay">
        <div class="portal-ring portal-ring-left"></div>
        <div class="portal-swap-text">PORTAL!</div>
        <div class="portal-ring portal-ring-right"></div>
      </div>

      <!-- Storm cat overlay -->
      <div v-if="fight.stormAppeared && showR1Result" class="storm-overlay">
        <div class="storm-cat">🐱</div>
        <div class="storm-label">
          Штормяк {{ fight.stormWeighingDelta > 0 ? '+' : '' }}{{ fight.stormWeighingDelta }}
          <span v-if="fight.stormFlipped" class="storm-flipped">ПЕРЕВЕРНУЛ!</span>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
/* ── Card ── */
.fa-card { background: var(--bg-inset); border: 1px solid var(--border-subtle); border-radius: var(--radius); padding: 4px 6px; display: flex; flex-direction: column; gap: 3px; position: relative; overflow: hidden; }

/* ── v-html icon badges (bypass scoped CSS) ── */
.fa-factor-detail :deep(.gi) { display: inline-block; font-size: 9px; font-weight: 800; padding: 1px 4px; border-radius: 3px; letter-spacing: 0.5px; vertical-align: middle; }
.fa-factor-detail :deep(.gi-int) { background: rgba(110, 170, 240, 0.12); color: var(--kh-c-secondary-info-200); }
.fa-factor-detail :deep(.gi-str) { background: rgba(239, 128, 128, 0.12); color: var(--kh-c-secondary-danger-200); }
.fa-factor-detail :deep(.gi-spd) { background: rgba(200, 185, 50, 0.12); color: var(--kh-c-text-highlight-dim); }
.fa-factor-detail :deep(.gi-psy) { background: rgba(232, 121, 249, 0.12); color: #e879f9; }
.fa-factor-detail :deep(.gi-ok) { color: var(--accent-green); font-weight: 800; }
.fa-factor-detail :deep(.gi-fail) { color: var(--accent-red); font-weight: 800; }
.fa-factor-detail :deep(.gi-tie) { color: var(--text-muted); }
.fa-factor-detail :deep(.dom-good) { color: var(--accent-green); font-weight: 900; text-transform: uppercase; font-size: 9px; letter-spacing: 0.5px; }
.fa-factor-detail :deep(.dom-bad) { color: var(--accent-red); font-weight: 900; text-transform: uppercase; font-size: 9px; letter-spacing: 0.5px; }

/* ── Compact identity + scale row ── */
.fa-id-left, .fa-id-right { display: flex; align-items: center; gap: 4px; flex-shrink: 0; }
.fa-id-left.winner .fa-id-name, .fa-id-right.winner .fa-id-name { color: var(--accent-green); font-weight: 800; }
.fa-id-left.winner .fa-ava-sm, .fa-id-right.winner .fa-ava-sm { border-color: var(--accent-green); box-shadow: var(--glow-green); }
.fa-ava-sm { width: 28px; height: 28px; border-radius: 50%; object-fit: cover; border: 2px solid var(--border-subtle); flex-shrink: 0; }
.fa-id-info { display: flex; flex-direction: column; line-height: 1.2; }
.fa-id-name { font-size: 10px; font-weight: 700; color: var(--text-primary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 80px; }
.fa-id-char { font-size: 8px; color: var(--text-muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 80px; }

/* ── Bar ── */
.fa-bar-container { display: flex; align-items: center; gap: 6px; padding: 2px 0; }
.fa-bar-compact { padding: 2px 0; }
.fa-bar-compact .fa-bar-track { height: 16px; }
.fa-bar-track { flex: 1; height: 20px; background: var(--bg-secondary); border-radius: 10px; overflow: hidden; position: relative; border: 1px solid var(--border-subtle); }
.fa-bar-fill { height: 100%; border-radius: 10px; transition: width 0.5s ease; display: flex; align-items: center; justify-content: flex-end; padding-right: 6px; min-width: 40px; }
.fa-bar-fill.bar-attacker { background: linear-gradient(90deg, var(--kh-c-secondary-success-500), var(--accent-green)); }
.fa-bar-fill.bar-defender { background: linear-gradient(90deg, var(--accent-red-dim), var(--accent-red)); }
.fa-bar-fill.bar-even { background: linear-gradient(90deg, rgba(230, 148, 74, 0.6), var(--accent-orange)); }

/* ── Weighing bar particles ── */
.fa-bar-fill.bar-particles-gold,
.fa-bar-fill.bar-particles-red { position: relative; }
.fa-bar-fill.bar-particles-gold::after,
.fa-bar-fill.bar-particles-red::after {
  content: ''; position: absolute; right: 0; top: -2px; width: 8px; height: calc(100% + 4px); pointer-events: none; z-index: 2;
}
.fa-bar-fill.bar-particles-gold::after {
  background-image:
    radial-gradient(1.5px 1.5px at 2px 2px, rgba(240, 200, 80, 0.9) 50%, transparent 50%),
    radial-gradient(1px 1px at 5px 7px, rgba(240, 200, 80, 0.7) 50%, transparent 50%),
    radial-gradient(1.5px 1.5px at 1px 12px, rgba(255, 220, 100, 0.85) 50%, transparent 50%),
    radial-gradient(1px 1px at 6px 17px, rgba(240, 200, 80, 0.6) 50%, transparent 50%),
    radial-gradient(1px 1px at 3px 22px, rgba(255, 230, 120, 0.75) 50%, transparent 50%);
  background-size: 8px 24px;
  animation: bar-sparkle-up 1.2s linear infinite;
}
.fa-bar-fill.bar-particles-red::after {
  background-image:
    radial-gradient(1.5px 1.5px at 2px 3px, rgba(239, 128, 128, 0.9) 50%, transparent 50%),
    radial-gradient(1px 1px at 5px 8px, rgba(239, 128, 128, 0.7) 50%, transparent 50%),
    radial-gradient(1.5px 1.5px at 1px 13px, rgba(255, 100, 90, 0.85) 50%, transparent 50%),
    radial-gradient(1px 1px at 6px 18px, rgba(239, 128, 128, 0.6) 50%, transparent 50%),
    radial-gradient(1px 1px at 3px 23px, rgba(255, 110, 100, 0.75) 50%, transparent 50%);
  background-size: 8px 24px;
  animation: bar-sparkle-up 1.5s linear infinite;
}
@keyframes bar-sparkle-up {
  0% { background-position: 0 0; opacity: 0.7; }
  50% { opacity: 1; }
  100% { background-position: 0 -24px; opacity: 0.4; }
}

/* ── Justice: Number Slam ── */
.fj-slam-wrap { display: flex; align-items: center; justify-content: center; gap: 0; padding: 1px 0; position: relative; }
.fj-slam-wrap.fj-slam-impact { animation: fj-slam-shake 0.4s ease-out; }
@keyframes fj-slam-shake {
  0%, 100% { transform: translateX(0); }
  15% { transform: translateX(-4px); }
  30% { transform: translateX(4px); }
  45% { transform: translateX(-3px); }
  60% { transform: translateX(2px); }
  75% { transform: translateX(-1px); }
}
.fj-slam-num { font-size: 16px; font-weight: 900; font-family: var(--font-mono); color: var(--text-muted); min-width: 24px; text-align: center; transition: all 0.5s cubic-bezier(0.2, 0.8, 0.2, 1.2); }
.fj-slam-num.winner { font-size: 20px; color: var(--accent-purple); text-shadow: 0 0 8px rgba(139, 92, 246, 0.5); transform: scale(1.1); }
.fj-slam-num.winner.fj-slam-right { color: var(--accent-red); text-shadow: 0 0 8px rgba(239, 128, 128, 0.5); }
.fj-slam-num.loser { font-size: 11px; color: var(--text-dim); opacity: 0.35; transform: scale(0.7); }
.fj-slam-num.tied { color: var(--accent-orange); animation: fj-tied-tremble 0.3s ease-in-out infinite; }

/* Block towers */
.fj-slam-tower { display: flex; flex-direction: column; align-items: center; gap: 1px; min-width: 24px; transition: all 0.5s cubic-bezier(0.2, 0.8, 0.2, 1.2); }
.fj-slam-row { display: flex; gap: 2px; }
.fj-slam-top { opacity: 0.35; transform: scale(0.6); margin-bottom: -3px; }
.fj-slam-back { opacity: 0.5; transform: scale(0.8); margin-bottom: -2px; }
.fj-block { width: 16px; height: 10px; border-radius: 2px; border: 1.5px solid var(--border-subtle); background: var(--bg-secondary); transition: all 0.4s ease; }
.fj-block-ours { border-color: rgba(139, 92, 246, 0.4); background: rgba(139, 92, 246, 0.12); }
.fj-block-enemy { border-color: rgba(239, 128, 128, 0.4); background: rgba(239, 128, 128, 0.12); }

.fj-slam-left { animation: fj-fly-left 0.4s ease-out both; }
.fj-slam-right { animation: fj-fly-right 0.4s ease-out both; }
@keyframes fj-fly-left {
  0% { transform: translateX(-24px) scale(0.5); opacity: 0; }
  70% { transform: translateX(3px) scale(1.05); opacity: 1; }
  100% { transform: translateX(0) scale(1); }
}
@keyframes fj-fly-right {
  0% { transform: translateX(24px) scale(0.5); opacity: 0; }
  70% { transform: translateX(-3px) scale(1.05); opacity: 1; }
  100% { transform: translateX(0) scale(1); }
}
.fj-slam-tower.winner .fj-block { transform: scale(1.15); }
.fj-slam-tower.winner .fj-block-ours { background: rgba(139, 92, 246, 0.35); border-color: rgba(139, 92, 246, 0.7); box-shadow: 0 0 8px rgba(139, 92, 246, 0.4); }
.fj-slam-tower.winner .fj-block-enemy { background: rgba(239, 128, 128, 0.35); border-color: rgba(239, 128, 128, 0.7); box-shadow: 0 0 8px rgba(239, 128, 128, 0.4); }
.fj-slam-tower.winner .fj-slam-top { opacity: 0.65; }
.fj-slam-tower.winner .fj-slam-back { opacity: 0.8; }
.fj-slam-tower.loser { transform: scale(0.6); opacity: 0.3; }
.fj-slam-tower.loser .fj-block { border-style: dashed; }
.fj-slam-tower.tied .fj-block { border-color: rgba(230, 148, 74, 0.6); background: rgba(230, 148, 74, 0.15); }
.fj-slam-tower.tied { animation: fj-tied-tremble 0.3s ease-in-out infinite; }
@keyframes fj-tied-tremble {
  0%, 100% { transform: translateX(0); }
  25% { transform: translateX(-1px); }
  75% { transform: translateX(1px); }
}
.fj-slam-vs { font-size: 9px; font-weight: 900; color: var(--text-dim); text-transform: uppercase; letter-spacing: 0.5px; min-width: 20px; text-align: center; opacity: 0; transition: opacity 0.3s; }
.fj-slam-vs.visible { opacity: 1; }
.fj-slam-spark { font-size: 14px; animation: fj-spark-pop 0.5s ease-out both; display: inline-block; }
@keyframes fj-spark-pop {
  0% { transform: scale(0.3); opacity: 0; }
  40% { transform: scale(1.8); opacity: 1; }
  100% { transform: scale(1); opacity: 0.7; }
}

/* ── Phase tracker ── */
.phase-tracker { display: flex; align-items: center; justify-content: center; gap: 0; padding: 2px 0 1px; }
.phase-tracker-inline { flex: 1; min-width: 0; justify-content: center; }
.phase-role-pip { padding: 2px 5px; border-radius: 4px; border: 1.5px solid var(--border-subtle); background: var(--bg-inset); flex-shrink: 0; }
.phase-role-text { font-size: 8px; font-weight: 900; letter-spacing: 0.5px; }
.phase-role-pip.role-atk { border-color: rgba(239, 128, 128, 0.4); background: rgba(239, 128, 128, 0.08); }
.phase-role-pip.role-atk .phase-role-text { color: var(--accent-red); }
.phase-role-pip.role-def { border-color: rgba(100, 160, 255, 0.4); background: rgba(100, 160, 255, 0.08); }
.phase-role-pip.role-def .phase-role-text { color: var(--accent-blue); }
.phase-pip { display: flex; flex-direction: column; align-items: center; gap: 1px; padding: 3px 6px; border-radius: 6px; border: 1.5px solid var(--border-subtle); background: var(--bg-inset); transition: all 0.4s ease; min-width: 28px; }
.phase-connector-outcome { width: 12px; }
.phase-outcome-pip { padding: 2px 8px; border-radius: 4px; border: 1.5px solid var(--border-subtle); background: var(--bg-inset); transition: all 0.4s ease; animation: phase-pop 0.4s ease; }
.phase-outcome-pending { opacity: 0.3; }
.phase-outcome-pip.phase-ours, .phase-outcome-pip.outcome-attacker { border-color: rgba(63, 167, 61, 0.5); background: rgba(63, 167, 61, 0.1); }
.phase-outcome-pip.phase-theirs, .phase-outcome-pip.outcome-defender { border-color: rgba(239, 128, 128, 0.4); background: rgba(239, 128, 128, 0.08); }
.phase-outcome-pip.outcome-neutral { border-color: rgba(230, 148, 74, 0.4); background: rgba(230, 148, 74, 0.08); }
.phase-outcome-text { font-size: 8px; font-weight: 900; letter-spacing: 0.5px; text-transform: uppercase; white-space: nowrap; }
.phase-outcome-pip.phase-ours .phase-outcome-text, .phase-outcome-pip.outcome-attacker .phase-outcome-text { color: var(--accent-green); }
.phase-outcome-pip.phase-theirs .phase-outcome-text, .phase-outcome-pip.outcome-defender .phase-outcome-text { color: var(--accent-red); }
.phase-outcome-pip.outcome-neutral .phase-outcome-text { color: var(--accent-orange); }
.phase-outcome-pending .phase-outcome-text { color: var(--text-dim); }
.phase-icon { font-size: 13px; font-weight: 900; line-height: 1; transition: all 0.3s; }
.phase-icon-pending { color: var(--text-dim); opacity: 0.4; }
.phase-icon-win { color: var(--accent-green); animation: phase-icon-in 0.4s ease; }
.phase-icon-lose { color: var(--accent-red); animation: phase-icon-in 0.4s ease; }
.phase-icon-draw { color: var(--accent-orange); animation: phase-icon-in 0.4s ease; }
.phase-icon-skip { color: var(--text-dim); opacity: 0.3; }
.phase-connector { width: 16px; height: 2px; background: var(--border-subtle); transition: background 0.4s; flex-shrink: 0; }
.phase-connector.revealed { background: var(--text-muted); }
.phase-connector.broken { background: none; border-top: 2px dashed var(--border-subtle); height: 0; }
.phase-pip.phase-ours { border-color: rgba(63, 167, 61, 0.5); background: rgba(63, 167, 61, 0.08); animation: phase-pop 0.4s ease; }
.phase-pip.phase-ours .phase-label { color: var(--accent-green); }
.phase-pip.phase-theirs { border-color: rgba(239, 128, 128, 0.4); background: rgba(239, 128, 128, 0.06); animation: phase-pop 0.4s ease; }
.phase-pip.phase-theirs .phase-label { color: var(--accent-red); }
.phase-pip.phase-draw { border-color: rgba(230, 148, 74, 0.4); background: rgba(230, 148, 74, 0.06); animation: phase-pop 0.4s ease; }
.phase-pip.phase-draw .phase-label { color: var(--accent-orange); }
.phase-pip.phase-skipped { opacity: 0.3; border-style: dashed; }
@keyframes phase-icon-in {
  0% { transform: scale(0); opacity: 0; }
  50% { transform: scale(1.3); }
  100% { transform: scale(1); opacity: 1; }
}
@keyframes phase-pop {
  0% { transform: scale(1); }
  50% { transform: scale(1.15); }
  100% { transform: scale(1); }
}

/* ── Factors ── */
.fa-factors { display: flex; flex-direction: column; gap: 2px; }
.fa-factor { display: flex; align-items: center; gap: 6px; padding: 3px 8px; border-radius: 4px; font-size: 11px; background: var(--bg-secondary); opacity: 0; transform: translateY(4px); transition: opacity 0.3s, transform 0.3s; border: 1px solid transparent; }
.fa-factor.visible { opacity: 1; transform: translateY(0); }
.fa-factor.good { border-left: 2px solid var(--accent-green); }
.fa-factor.bad { border-left: 2px solid var(--accent-red); }
.fa-factor.neutral { border-left: 2px solid var(--accent-orange); }
.fa-factor.justice { border-left: 2px solid var(--accent-purple); }
.fa-factor.random { border-left: 2px solid var(--accent-blue); }
.fa-factor-label { font-weight: 700; color: var(--text-primary); min-width: 100px; font-size: 10px; }
.fa-factor-detail { color: var(--text-secondary); flex: 1; font-size: 10px; display: flex; align-items: center; gap: 3px; flex-wrap: wrap; }
.fa-factor-value { font-weight: 800; color: var(--accent-gold); margin-left: auto; font-family: var(--font-mono); font-size: 11px; flex-shrink: 0; }
.fa-factor-detail :deep(.admin-extra) { color: var(--text-dim); font-size: 9px; font-family: var(--font-mono); }

/* ── Tier intensity ── */
.fa-factor.good.tier-0 { border-left-width: 2px; }
.fa-factor.good.tier-1 { border-left-width: 3px; }
.fa-factor.good.tier-2 { border-left-width: 4px; background: rgba(63, 167, 61, 0.06); }
.fa-factor.bad.tier-0 { border-left-width: 2px; }
.fa-factor.bad.tier-1 { border-left-width: 3px; }
.fa-factor.bad.tier-2 { border-left-width: 4px; background: rgba(239, 128, 128, 0.06); }
.fa-factor.tier-0 :deep(.gi-ok),
.fa-factor.tier-0 :deep(.gi-fail) { opacity: 0.65; }
.fa-factor.tier-2 :deep(.gi-ok) { font-weight: 900; text-shadow: 0 0 6px rgba(63, 167, 61, 0.5); }
.fa-factor.tier-2 :deep(.gi-fail) { font-weight: 900; text-shadow: 0 0 6px rgba(239, 128, 128, 0.5); }
.fa-factor.tier-2.visible { animation: tier2-pulse 1.8s ease-in-out 1; }
@keyframes tier2-pulse {
  0%, 100% { filter: brightness(1); }
  40% { filter: brightness(1.25); }
}

/* ── Badges ── */
.fa-badge-row { display: flex; justify-content: center; gap: 6px; padding: 2px 0; opacity: 0; transition: opacity 0.3s; flex-wrap: wrap; }
.fa-badge-row.visible { opacity: 1; }
.fa-badge { font-size: 9px; font-weight: 900; padding: 2px 10px; border-radius: 4px; text-transform: uppercase; letter-spacing: 0.5px; }
.badge-toogood { background: rgba(63, 167, 61, 0.1); color: var(--accent-green); border: 1px solid rgba(63, 167, 61, 0.3); }
.badge-toostronk { background: rgba(180, 150, 255, 0.1); color: var(--accent-purple); border: 1px solid rgba(180, 150, 255, 0.3); }
.badge-skill { background: rgba(233, 219, 61, 0.1); color: var(--accent-gold); border: 1px solid rgba(233, 219, 61, 0.3); }
.badge-fof-buff { background: rgba(56, 189, 248, 0.1); color: #38bdf8; border: 1px solid rgba(56, 189, 248, 0.3); }
.badge-fof-debuff { background: rgba(239, 68, 68, 0.1); color: #ef4444; border: 1px solid rgba(239, 68, 68, 0.3); }
.badge-fof-enemy { background: rgba(245, 158, 11, 0.1); color: #f59e0b; border: 1px solid rgba(245, 158, 11, 0.3); }
.badge-class-change { background: rgba(236, 72, 153, 0.1); color: #ec4899; border: 1px solid rgba(236, 72, 153, 0.3); }

/* ── R3 details ── */
.fa-r3-details { display: flex; flex-direction: column; gap: 2px; }
.pct-good { color: var(--accent-green); font-weight: 700; }
.pct-bad { color: var(--accent-red); font-weight: 700; }

/* ── Roll bar ── */
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
.fa-roll-bar-track.track-overflow { border-right: none; border-radius: 6px 0 0 6px; }
.fa-roll-zone-win.zone-overflow { width: 100% !important; border-radius: 6px 0 0 6px; box-shadow: 4px 0 12px rgba(63, 167, 61, 0.4), 8px 0 24px rgba(63, 167, 61, 0.2); }
.fa-roll-bar-wrap.roll-overflow { position: relative; }
.fa-roll-bar-wrap.roll-overflow::after {
  content: ''; position: absolute; right: -8px; top: 3px; bottom: 0; width: 16px;
  background: linear-gradient(90deg, rgba(63, 167, 61, 0.3), transparent); border-radius: 0 6px 6px 0; pointer-events: none;
}

/* ── Enemy summary ── */
.fa-enemy-summary { display: flex; gap: 6px; justify-content: center; padding: 2px 0; }

/* ── Result ── */
.fa-result { display: flex; flex-direction: column; align-items: center; gap: 3px; padding: 3px 0 2px; }
.fa-result-details { display: flex; flex-wrap: wrap; gap: 4px; justify-content: center; font-size: 10px; padding-top: 2px; }
.fa-detail-item { padding: 1px 8px; border-radius: 4px; background: var(--bg-secondary); border: 1px solid var(--border-subtle); }
.fa-skill-gain { color: var(--accent-green); font-weight: 700; }
.fa-moral { color: var(--accent-purple); }
.fa-justice { color: var(--accent-blue); }
.fa-resist { color: var(--accent-orange); }
.fa-dmg-us { color: var(--accent-red); font-weight: 800; font-style: italic; animation: dmg-pulse 0.6s ease-in-out 2; }
.fa-dmg-enemy { color: var(--text-muted); font-weight: 600; font-style: italic; }
@keyframes dmg-pulse { 0%,100% { opacity: 1; } 50% { opacity: 0.4; } }
.fa-drop { color: white; background: var(--accent-red-dim) !important; border-color: var(--accent-red) !important; font-weight: 800; animation: dmg-pulse 0.6s ease-in-out 2; }
.fa-drop-enemy { color: var(--text-muted); font-weight: 600; }

/* ── Special (block/skip) ── */
.fa-special { display: flex; justify-content: center; padding: 4px 0; }

/* ── Fight result glow ── */
.fa-card.fa-result-win {
  border-color: rgba(63, 167, 61, 0.5);
  box-shadow: 0 0 12px rgba(63, 167, 61, 0.25), 0 0 24px rgba(63, 167, 61, 0.1), inset 0 0 8px rgba(63, 167, 61, 0.05);
  transition: border-color 0.4s, box-shadow 0.4s;
}
.fa-card.fa-result-loss {
  border-color: rgba(239, 128, 128, 0.5);
  box-shadow: 0 0 12px rgba(239, 128, 128, 0.25), 0 0 24px rgba(239, 128, 128, 0.1), inset 0 0 8px rgba(239, 128, 128, 0.05);
  transition: border-color 0.4s, box-shadow 0.4s;
}

/* ── Result badge ── */
.fa-result-badge {
  position: absolute; bottom: 8px; right: 10px; font-size: 18px; font-weight: 900; z-index: 10; pointer-events: none; line-height: 1;
}
.fa-result-badge-win { color: var(--accent-green); text-shadow: 0 0 8px rgba(63, 167, 61, 0.6); }
.fa-result-badge-loss { color: var(--accent-red); text-shadow: 0 0 8px rgba(239, 128, 128, 0.6); }
.result-badge-enter-active { transition: opacity 0.3s, transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1); }
.result-badge-leave-active { transition: opacity 0.4s; }
.result-badge-enter-from { opacity: 0; transform: scale(0.5); }
.result-badge-leave-to { opacity: 0; }

/* ── Screen shake ── */
.fa-card.fa-shake { animation: fight-shake 0.5s ease-in-out; }
@keyframes fight-shake {
  0%, 100% { transform: translateX(0); }
  10% { transform: translateX(-3px); }
  20% { transform: translateX(3px); }
  30% { transform: translateX(-2px); }
  40% { transform: translateX(2px); }
  50% { transform: translateX(-1px); }
  60% { transform: translateX(1px); }
}

/* ── Winner emphasis ── */
.fa-id-left.winner .fa-id-char, .fa-id-right.winner .fa-id-char {
  color: var(--accent-green); text-shadow: 0 0 4px rgba(63, 167, 61, 0.3);
}

/* ── Avatar clash intro ── */
.fa-id-left .fa-ava-sm { animation: avatar-clash-left 0.5s cubic-bezier(0.34, 1.56, 0.64, 1); }
.fa-id-right .fa-ava-sm { animation: avatar-clash-right 0.5s cubic-bezier(0.34, 1.56, 0.64, 1); }
@keyframes avatar-clash-left {
  0% { transform: translateX(-20px) scale(0.8); opacity: 0.5; }
  50% { transform: translateX(3px) scale(1.05); opacity: 1; }
  100% { transform: translateX(0) scale(1); opacity: 1; }
}
@keyframes avatar-clash-right {
  0% { transform: translateX(20px) scale(0.8); opacity: 0.5; }
  50% { transform: translateX(-3px) scale(1.05); opacity: 1; }
  100% { transform: translateX(0) scale(1); opacity: 1; }
}

/* ── Entrance animation ── */
.fa-id-left.entrance-active .fa-ava-sm {
  animation: entrance-left 400ms cubic-bezier(0.34, 1.56, 0.64, 1) both; position: relative;
}
.fa-id-right.entrance-active .fa-ava-sm {
  animation: entrance-right 400ms cubic-bezier(0.34, 1.56, 0.64, 1) both; position: relative;
}
@keyframes entrance-left {
  0% { transform: translateX(-40px) scale(0.6); opacity: 0; }
  60% { transform: translateX(6px) scale(1.08); opacity: 1; }
  80% { transform: translateX(-2px) scale(0.98); }
  100% { transform: translateX(0) scale(1); opacity: 1; }
}
@keyframes entrance-right {
  0% { transform: translateX(40px) scale(0.6); opacity: 0; }
  60% { transform: translateX(-6px) scale(1.08); opacity: 1; }
  80% { transform: translateX(2px) scale(0.98); }
  100% { transform: translateX(0) scale(1); opacity: 1; }
}
.fa-id-left.entrance-active .fa-ava-sm::after,
.fa-id-right.entrance-active .fa-ava-sm::after {
  content: ''; position: absolute; inset: -8px; border-radius: 50%;
  background: radial-gradient(circle, rgba(240, 200, 80, 0.6) 0%, rgba(240, 200, 80, 0) 70%);
  opacity: 0; animation: entrance-impact-flash 500ms ease-out 300ms forwards; pointer-events: none; z-index: 1;
}
@keyframes entrance-impact-flash {
  0% { opacity: 0; transform: scale(0.4); }
  30% { opacity: 0.9; transform: scale(1.2); }
  100% { opacity: 0; transform: scale(1.8); }
}

/* ── Factor reveal bounce ── */
.fa-factor.visible { animation: factor-slide-in 0.35s cubic-bezier(0.34, 1.56, 0.64, 1); }
@keyframes factor-slide-in {
  0% { opacity: 0; transform: translateX(-15px); }
  60% { opacity: 1; transform: translateX(3px); }
  100% { opacity: 1; transform: translateX(0); }
}
.fa-factor.tier-2.visible::before {
  content: ''; position: absolute; inset: 0; border-radius: inherit; opacity: 0;
  animation: factor-glow-burst 0.8s ease-out 0.2s; pointer-events: none;
}
.fa-factor.tier-2.good.visible::before { background: radial-gradient(ellipse at 10% 50%, rgba(91, 168, 91, 0.2), transparent 70%); }
.fa-factor.tier-2.bad.visible::before { background: radial-gradient(ellipse at 10% 50%, rgba(224, 85, 69, 0.2), transparent 70%); }
@keyframes factor-glow-burst { 0% { opacity: 0; } 30% { opacity: 1; } 100% { opacity: 0; } }

/* ── Roll bar needle trail ── */
.fa-roll-needle::before {
  content: ''; position: absolute; top: 2px; bottom: 2px; left: -1px; width: 5px;
  border-radius: 3px; opacity: 0.3; filter: blur(2px); pointer-events: none; transition: none;
}
.fa-roll-needle.needle-win::before { background: var(--accent-green); }
.fa-roll-needle.needle-lose::before { background: var(--accent-red); }

/* ── Impact cracks ── */
.impact-cracks { position: absolute; left: 50%; top: 50%; transform: translate(-50%, -50%); pointer-events: none; z-index: 5; }
.crack { position: absolute; width: 2px; height: 0; background: linear-gradient(to bottom, rgba(255, 255, 255, 0.6), transparent); transform-origin: top center; animation: crack-grow 0.3s ease-out forwards; }
.crack-1 { transform: rotate(-30deg); }
.crack-2 { transform: rotate(25deg); }
.crack-3 { transform: rotate(-60deg); }
.crack-4 { transform: rotate(50deg); }
@keyframes crack-grow { 0% { height: 0; opacity: 1; } 60% { height: 30px; opacity: 0.8; } 100% { height: 40px; opacity: 0; } }

/* ── Justice collision physics ── */
.justice-winner { transform: scale(1.3) !important; text-shadow: 0 0 20px rgba(240, 200, 80, 0.8); transition: all 0.3s cubic-bezier(0.2, 0.8, 0.2, 1.2); }
.justice-loser { transform: scale(0.7) !important; opacity: 0.5; transition: all 0.3s ease-out; }
.justice-tied-pulse { animation: justice-tie-pulse 0.4s ease-in-out 1; }
@keyframes justice-tie-pulse { 0%, 100% { transform: scale(1); } 50% { transform: scale(1.15); } }
.fj-slam-left.justice-loser { transform: scale(0.7) translateX(-8px) !important; }
.fj-slam-right.justice-loser { transform: scale(0.7) translateX(8px) !important; }

/* ── Winner justice number ring ── */
.fj-slam-num.winner::after {
  content: ''; position: absolute; inset: -4px; border-radius: 50%; border: 2px solid var(--accent-gold);
  opacity: 0; animation: justice-ring 0.6s ease-out 0.4s forwards; pointer-events: none;
}
@keyframes justice-ring { 0% { transform: scale(0.5); opacity: 0.8; } 100% { transform: scale(1.5); opacity: 0; } }

/* ── Portal Gun swap ── */
.portal-swap-overlay { display: flex; align-items: center; justify-content: center; gap: 8px; padding: 4px 0; animation: portal-fade-in 0.3s ease-out; }
.portal-ring { width: 24px; height: 24px; border-radius: 50%; border: 3px solid transparent; border-top-color: #00ff88; border-right-color: #00cc66; animation: portal-spin 1s linear infinite; box-shadow: 0 0 10px rgba(0, 255, 136, 0.4), inset 0 0 6px rgba(0, 255, 136, 0.2); }
.portal-ring-right { animation-direction: reverse; border-top-color: #ff8800; border-right-color: #cc6600; box-shadow: 0 0 10px rgba(255, 136, 0, 0.4), inset 0 0 6px rgba(255, 136, 0, 0.2); }
@keyframes portal-spin { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }
.portal-swap-text { font-size: 13px; font-weight: 900; color: #00ff88; text-transform: uppercase; letter-spacing: 2px; text-shadow: 0 0 12px rgba(0, 255, 136, 0.6); animation: portal-text-pulse 0.8s ease-in-out infinite alternate; }
@keyframes portal-text-pulse { 0% { opacity: 0.7; transform: scale(0.95); } 100% { opacity: 1; transform: scale(1.05); } }
@keyframes portal-fade-in { 0% { opacity: 0; transform: scale(0.5); } 60% { transform: scale(1.1); } 100% { opacity: 1; transform: scale(1); } }

/* ── Storm Cat Overlay ── */
.storm-overlay { position: absolute; inset: 0; display: flex; flex-direction: column; align-items: center; justify-content: center; pointer-events: none; z-index: 20; animation: storm-bounce 0.5s ease-out; background: radial-gradient(ellipse at center, rgba(255,165,0,0.12) 0%, transparent 70%); }
.storm-cat { font-size: 48px; filter: drop-shadow(0 0 12px rgba(255,165,0,0.6)); animation: storm-wiggle 0.6s ease-in-out infinite alternate; }
.storm-label { font-size: 13px; font-weight: 700; color: #ffa500; text-shadow: 0 0 8px rgba(255,165,0,0.5); margin-top: 2px; }
.storm-flipped { color: #ff4444; font-weight: 900; margin-left: 4px; animation: storm-flash 0.4s ease-in-out infinite alternate; }
@keyframes storm-bounce { 0% { opacity: 0; transform: translateY(-30px) scale(0.5); } 60% { transform: translateY(5px) scale(1.05); } 100% { opacity: 1; transform: translateY(0) scale(1); } }
@keyframes storm-wiggle { 0% { transform: rotate(-8deg); } 100% { transform: rotate(8deg); } }
@keyframes storm-flash { 0% { opacity: 0.6; } 100% { opacity: 1; } }
</style>
