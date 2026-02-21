<script setup lang="ts">
import { ref, computed } from 'vue'

// ── Types ──────────────────────────────────────────────────────────────
type Suit = '♠' | '♣' | '♥' | '♦'
type Rank = 'A' | '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9' | '10' | 'J' | 'Q' | 'K'
interface Card { suit: Suit; rank: Rank; faceUp: boolean }
type Phase = 'betting' | 'playing' | 'dealer-turn' | 'finished'

// ── State ──────────────────────────────────────────────────────────────
const phase = ref<Phase>('betting')
const deck = ref<Card[]>([])
const playerHand = ref<Card[]>([])
const dealerHand = ref<Card[]>([])
const resultText = ref('')
const resultClass = ref('')
const wins = ref(0)
const losses = ref(0)
const draws = ref(0)
const dealing = ref(false)

// ── Helpers ────────────────────────────────────────────────────────────
function makeDeck(): Card[] {
  const suits: Suit[] = ['♠', '♣', '♥', '♦']
  const ranks: Rank[] = ['A', '2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K']
  const d: Card[] = []
  for (const suit of suits)
    for (const rank of ranks)
      d.push({ suit, rank, faceUp: true })
  // Fisher-Yates shuffle
  for (let i = d.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[d[i], d[j]] = [d[j], d[i]]
  }
  return d
}

function cardValue(card: Card): number {
  if (['J', 'Q', 'K'].includes(card.rank)) return 10
  if (card.rank === 'A') return 11
  return parseInt(card.rank)
}

function handTotal(hand: Card[]): number {
  let total = 0
  let aces = 0
  for (const c of hand) {
    total += cardValue(c)
    if (c.rank === 'A') aces++
  }
  while (total > 21 && aces > 0) {
    total -= 10
    aces--
  }
  return total
}

function isRed(suit: Suit): boolean {
  return suit === '♥' || suit === '♦'
}

function drawCard(faceUp = true): Card {
  const card = deck.value.pop()!
  card.faceUp = faceUp
  return card
}

function sleep(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms))
}

// ── Computed ───────────────────────────────────────────────────────────
const playerTotal = computed(() => handTotal(playerHand.value))
const dealerTotal = computed(() => handTotal(dealerHand.value.filter(c => c.faceUp)))
const dealerTrueTotal = computed(() => handTotal(dealerHand.value))

// ── Game Flow ──────────────────────────────────────────────────────────
async function startGame() {
  deck.value = makeDeck()
  playerHand.value = []
  dealerHand.value = []
  resultText.value = ''
  resultClass.value = ''
  phase.value = 'playing'
  dealing.value = true

  // Staggered deal: player, dealer, player, dealer(hidden)
  await sleep(100)
  playerHand.value.push(drawCard())
  await sleep(200)
  dealerHand.value.push(drawCard())
  await sleep(200)
  playerHand.value.push(drawCard())
  await sleep(200)
  dealerHand.value.push(drawCard(false))

  dealing.value = false

  // Natural blackjack check
  if (playerTotal.value === 21) {
    await stand()
  }
}

function hit() {
  if (phase.value !== 'playing' || dealing.value) return
  playerHand.value.push(drawCard())
  if (playerTotal.value > 21) {
    finishGame('Перебор! Вы проиграли.', 'loss')
    losses.value++
  } else if (playerTotal.value === 21) {
    stand()
  }
}

async function stand() {
  if (phase.value !== 'playing' || dealing.value) return
  phase.value = 'dealer-turn'

  // Reveal hidden card
  if (dealerHand.value.length > 1) {
    dealerHand.value[1].faceUp = true
  }

  await sleep(400)

  // Dealer draws until 17+
  while (handTotal(dealerHand.value) < 17) {
    await sleep(500)
    dealerHand.value.push(drawCard())
  }

  await sleep(300)
  resolveGame()
}

function resolveGame() {
  const pTotal = playerTotal.value
  const dTotal = handTotal(dealerHand.value)

  if (dTotal > 21) {
    finishGame('Дилер перебрал! Вы выиграли!', 'win')
    wins.value++
  } else if (pTotal === 21 && playerHand.value.length === 2 && !(dTotal === 21 && dealerHand.value.length === 2)) {
    finishGame('Блэкджек! Вы выиграли!', 'blackjack')
    wins.value++
  } else if (dTotal === 21 && dealerHand.value.length === 2 && !(pTotal === 21 && playerHand.value.length === 2)) {
    finishGame('Блэкджек у дилера!', 'loss')
    losses.value++
  } else if (pTotal > dTotal) {
    finishGame('Вы выиграли!', 'win')
    wins.value++
  } else if (dTotal > pTotal) {
    finishGame('Дилер выиграл.', 'loss')
    losses.value++
  } else {
    finishGame('Ничья!', 'push')
    draws.value++
  }
}

function finishGame(text: string, cls: string) {
  resultText.value = text
  resultClass.value = cls
  phase.value = 'finished'
}
</script>

<template>
  <div class="bj-panel card">
    <!-- Header -->
    <div class="bj-header">
      <div class="bj-title">Мир Шинигами — Игра 21</div>
      <div class="bj-stats">
        <span class="bj-stat bj-stat-win">W: {{ wins }}</span>
        <span class="bj-stat bj-stat-loss">L: {{ losses }}</span>
        <span class="bj-stat bj-stat-draw">D: {{ draws }}</span>
      </div>
    </div>

    <!-- Dealer Hand -->
    <div class="bj-hand-area">
      <div class="bj-hand-label">
        Дилер
        <span class="bj-hand-total">{{ phase === 'dealer-turn' || phase === 'finished' ? dealerTrueTotal : dealerTotal }}</span>
      </div>
      <TransitionGroup name="card-deal" tag="div" class="bj-cards">
        <div
          v-for="(card, i) in dealerHand"
          :key="'d' + i"
          class="bj-card"
          :class="{ 'bj-card-hidden': !card.faceUp, 'bj-card-red': card.faceUp && isRed(card.suit) }"
        >
          <template v-if="card.faceUp">
            <span class="bj-card-rank">{{ card.rank }}</span>
            <span class="bj-card-suit">{{ card.suit }}</span>
          </template>
          <template v-else>
            <span class="bj-card-back">?</span>
          </template>
        </div>
      </TransitionGroup>
    </div>

    <!-- Player Hand -->
    <div class="bj-hand-area">
      <div class="bj-hand-label">
        Игрок
        <span class="bj-hand-total">{{ playerTotal }}</span>
      </div>
      <TransitionGroup name="card-deal" tag="div" class="bj-cards">
        <div
          v-for="(card, i) in playerHand"
          :key="'p' + i"
          class="bj-card"
          :class="{ 'bj-card-red': isRed(card.suit) }"
        >
          <span class="bj-card-rank">{{ card.rank }}</span>
          <span class="bj-card-suit">{{ card.suit }}</span>
        </div>
      </TransitionGroup>
    </div>

    <!-- Controls -->
    <div class="bj-controls">
      <template v-if="phase === 'betting'">
        <button class="bj-btn bj-btn-start" @click="startGame">Начать игру</button>
      </template>
      <template v-else-if="phase === 'playing'">
        <button class="bj-btn bj-btn-hit" :disabled="dealing" @click="hit">Ещё</button>
        <button class="bj-btn bj-btn-stand" :disabled="dealing" @click="stand">Хватит</button>
      </template>
      <template v-else-if="phase === 'dealer-turn'">
        <div class="bj-dealer-msg">Дилер берёт карты...</div>
      </template>
      <template v-else-if="phase === 'finished'">
        <div class="bj-result" :class="'bj-result-' + resultClass">{{ resultText }}</div>
        <button class="bj-btn bj-btn-start" @click="startGame">Новая игра</button>
      </template>
    </div>
  </div>
</template>

<style scoped>
.bj-panel {
  background: rgba(15, 10, 25, 0.85);
  border: 1px solid rgba(120, 60, 200, 0.35);
  border-radius: 12px;
  padding: 16px 20px;
  margin-bottom: 12px;
}

.bj-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 14px;
}

.bj-title {
  font-size: 1.1rem;
  font-weight: 700;
  color: #b47aff;
  text-shadow: 0 0 12px rgba(140, 80, 220, 0.5);
}

.bj-stats {
  display: flex;
  gap: 10px;
  font-size: 0.85rem;
  font-weight: 600;
}
.bj-stat-win { color: #4caf50; }
.bj-stat-loss { color: #ef5350; }
.bj-stat-draw { color: #9e9e9e; }

.bj-hand-area {
  margin-bottom: 12px;
}

.bj-hand-label {
  font-size: 0.85rem;
  color: #ccc;
  margin-bottom: 6px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.bj-hand-total {
  background: rgba(120, 60, 200, 0.25);
  color: #d4b8ff;
  padding: 1px 8px;
  border-radius: 8px;
  font-size: 0.8rem;
  font-weight: 700;
}

.bj-cards {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  min-height: 85px;
}

.bj-card {
  width: 60px;
  height: 85px;
  border-radius: 6px;
  background: #f0ede5;
  color: #222;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.4);
  position: relative;
  user-select: none;
}

.bj-card-rank {
  font-size: 1.15rem;
  line-height: 1;
}

.bj-card-suit {
  font-size: 1.3rem;
  line-height: 1;
  margin-top: 2px;
}

.bj-card-red {
  color: #c41e3a;
}

.bj-card-hidden {
  background: linear-gradient(135deg, #2a1545, #1a0d30);
  border: 1px solid rgba(120, 60, 200, 0.4);
  color: rgba(180, 122, 255, 0.5);
}

.bj-card-back {
  font-size: 2rem;
}

/* Card deal animation */
.card-deal-enter-active {
  transition: all 0.3s ease-out;
}
.card-deal-enter-from {
  opacity: 0;
  transform: translateY(-20px) scale(0.8);
}
.card-deal-enter-to {
  opacity: 1;
  transform: translateY(0) scale(1);
}

.bj-controls {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-top: 14px;
  min-height: 40px;
  flex-wrap: wrap;
}

.bj-btn {
  padding: 8px 22px;
  border: none;
  border-radius: 8px;
  font-size: 0.95rem;
  font-weight: 700;
  cursor: pointer;
  transition: background 0.2s, transform 0.1s;
}

.bj-btn:hover {
  transform: translateY(-1px);
}

.bj-btn:active {
  transform: translateY(0);
}

.bj-btn:disabled {
  opacity: 0.5;
  cursor: default;
  transform: none;
}

.bj-btn-hit {
  background: #7b2ff2;
  color: #fff;
}
.bj-btn-hit:hover:not(:disabled) {
  background: #8e44ff;
}

.bj-btn-stand {
  background: #c41e3a;
  color: #fff;
}
.bj-btn-stand:hover:not(:disabled) {
  background: #d93050;
}

.bj-btn-start {
  background: #388e3c;
  color: #fff;
}
.bj-btn-start:hover {
  background: #43a047;
}

.bj-dealer-msg {
  color: #b47aff;
  font-weight: 600;
  animation: pulse-msg 1s ease-in-out infinite;
}

@keyframes pulse-msg {
  0%, 100% { opacity: 0.6; }
  50% { opacity: 1; }
}

.bj-result {
  font-size: 1rem;
  font-weight: 700;
}

.bj-result-win {
  color: #4caf50;
}

.bj-result-loss {
  color: #ef5350;
}

.bj-result-push {
  color: #9e9e9e;
}

.bj-result-blackjack {
  color: #ffd700;
  text-shadow: 0 0 10px rgba(255, 215, 0, 0.6);
  animation: gold-glow 1.5s ease-in-out infinite;
}

@keyframes gold-glow {
  0%, 100% { text-shadow: 0 0 10px rgba(255, 215, 0, 0.4); }
  50% { text-shadow: 0 0 20px rgba(255, 215, 0, 0.8), 0 0 30px rgba(255, 215, 0, 0.3); }
}
</style>
