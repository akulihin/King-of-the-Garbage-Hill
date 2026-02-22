<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useGameStore } from 'src/store/game'
import type { BlackjackPlayerState } from 'src/services/signalr'

defineProps<{ gameId: number }>()
const store = useGameStore()

// ── Dark Souls Message Composer ─────────────────────────────────────
const selectedWords = ref<string[]>([])
const messageSent = ref(false)

function toggleWord(word: string) {
  const idx = selectedWords.value.indexOf(word)
  if (idx >= 0) {
    selectedWords.value.splice(idx, 1)
  } else if (selectedWords.value.length < 10) {
    selectedWords.value.push(word)
  }
}

function isWordSelected(word: string): boolean {
  return selectedWords.value.includes(word)
}

async function sendMessage() {
  if (selectedWords.value.length === 0) return
  await store.blackjackSendMessage(selectedWords.value)
  messageSent.value = true
  selectedWords.value = []
}

// ── Computed ────────────────────────────────────────────────────────
const bjState = computed(() => store.blackjackState)
const mePlayer = computed(() => bjState.value?.players.find((p: BlackjackPlayerState) => p.isMe) ?? null)
const canSendMessage = computed(() => mePlayer.value?.canSendMessage && !messageSent.value)
const isWaiting = computed(() => bjState.value?.phase === 'waiting')
const isFinished = computed(() => bjState.value?.phase === 'finished')
const isMyTurn = computed(() => {
  if (!bjState.value || bjState.value.phase !== 'playerturns') return false
  const me = mePlayer.value
  return me?.isCurrentTurn ?? false
})

// Reset messageSent when a new round starts
watch(() => bjState.value?.phase, (newPhase, oldPhase) => {
  if (newPhase === 'playerturns' && oldPhase === 'finished') {
    messageSent.value = false
  }
})

function isRed(suit: string | null): boolean {
  return suit === 'hearts' || suit === 'diamonds'
}

function suitSymbol(suit: string | null): string {
  switch (suit) {
    case 'spades': return '♠'
    case 'clubs': return '♣'
    case 'hearts': return '♥'
    case 'diamonds': return '♦'
    default: return ''
  }
}

function playerResultText(player: BlackjackPlayerState): string {
  switch (player.result) {
    case 'win': return 'Победа!'
    case 'loss': return 'Проигрыш'
    case 'push': return 'Ничья'
    case 'blackjack': return 'Блэкджек!'
    default: return ''
  }
}

function playerStatusText(player: BlackjackPlayerState): string {
  switch (player.status) {
    case 'busted': return 'Перебор!'
    case 'stood': return 'Стоит'
    case 'playing': return player.isCurrentTurn ? 'Ходит...' : 'Ждёт'
    default: return ''
  }
}
</script>

<template>
  <div class="bj-panel card" v-if="bjState">
    <!-- Header -->
    <div class="bj-header">
      <div class="bj-title">Мир Шинигами — Игра 21</div>
      <div class="bj-player-count">{{ bjState.players.length }} / 5</div>
    </div>

    <!-- Waiting / No round yet -->
    <template v-if="isWaiting">
      <div class="bj-waiting-msg">Ожидание начала раунда...</div>
      <div class="bj-controls">
        <button class="bj-btn bj-btn-start" @click="store.blackjackNewRound()">Начать раунд</button>
      </div>
    </template>

    <!-- Active round or finished -->
    <template v-else>
      <!-- Dealer Hand -->
      <div class="bj-hand-area">
        <div class="bj-hand-label">
          {{ bjState.dealerName }}
          <span class="bj-hand-total">{{ bjState.dealerTotal }}</span>
        </div>
        <TransitionGroup name="card-deal" tag="div" class="bj-cards">
          <div
            v-for="(card, i) in bjState.dealerHand"
            :key="'d' + i"
            class="bj-card"
            :class="{ 'bj-card-hidden': !card.faceUp, 'bj-card-red': card.faceUp && isRed(card.suit) }"
          >
            <template v-if="card.faceUp">
              <span class="bj-card-rank">{{ card.rank }}</span>
              <span class="bj-card-suit">{{ suitSymbol(card.suit) }}</span>
            </template>
            <template v-else>
              <span class="bj-card-back">?</span>
            </template>
          </div>
        </TransitionGroup>
      </div>

      <!-- All Players' Hands -->
      <div
        v-for="player in bjState.players"
        :key="player.discordId"
        class="bj-hand-area"
        :class="{ 'bj-hand-me': player.isMe, 'bj-hand-active': player.isCurrentTurn }"
      >
        <div class="bj-hand-label">
          <span :class="{ 'bj-name-me': player.isMe }">{{ player.username }}</span>
          <span class="bj-hand-total">{{ player.total }}</span>
          <span class="bj-wins-badge" v-if="player.wins > 0">W: {{ player.wins }}</span>
          <span
            v-if="isFinished && player.result"
            class="bj-player-result"
            :class="'bj-result-' + player.result"
          >{{ playerResultText(player) }}</span>
          <span
            v-else-if="bjState.phase === 'playerturns'"
            class="bj-player-status"
            :class="{ 'bj-status-active': player.isCurrentTurn }"
          >{{ playerStatusText(player) }}</span>
        </div>
        <TransitionGroup name="card-deal" tag="div" class="bj-cards">
          <div
            v-for="(card, i) in player.hand"
            :key="'p' + player.discordId + i"
            class="bj-card"
            :class="{ 'bj-card-red': isRed(card.suit) }"
          >
            <span class="bj-card-rank">{{ card.rank }}</span>
            <span class="bj-card-suit">{{ suitSymbol(card.suit) }}</span>
          </div>
        </TransitionGroup>
      </div>

      <!-- Controls -->
      <div class="bj-controls">
        <template v-if="bjState.phase === 'playerturns' && isMyTurn">
          <button class="bj-btn bj-btn-hit" @click="store.blackjackHit()">Ещё</button>
          <button class="bj-btn bj-btn-stand" @click="store.blackjackStand()">Хватит</button>
        </template>
        <template v-else-if="bjState.phase === 'playerturns' && !isMyTurn">
          <div class="bj-dealer-msg">Ожидание хода другого игрока...</div>
        </template>
        <template v-else-if="bjState.phase === 'dealerturn'">
          <div class="bj-dealer-msg">Дилер берёт карты...</div>
        </template>
        <template v-else-if="isFinished">
          <button class="bj-btn bj-btn-start" @click="store.blackjackNewRound()">Новый раунд</button>
        </template>
      </div>

      <!-- Dark Souls Message Panel (for winners) -->
      <div v-if="canSendMessage" class="bj-message-panel">
        <div class="bj-message-title">Оставить послание</div>
        <div class="bj-message-preview" v-if="selectedWords.length > 0">
          {{ selectedWords.join(' ') }}
        </div>
        <div class="bj-message-preview bj-message-empty" v-else>
          Выберите слова...
        </div>

        <div
          v-for="category in bjState.wordCategories"
          :key="category.name"
          class="bj-word-category"
        >
          <div class="bj-category-name">{{ category.name }}</div>
          <div class="bj-word-list">
            <button
              v-for="word in category.words"
              :key="word"
              class="bj-word-btn"
              :class="{ 'bj-word-selected': isWordSelected(word) }"
              @click="toggleWord(word)"
            >{{ word }}</button>
          </div>
        </div>

        <div class="bj-message-actions">
          <span class="bj-word-count">{{ selectedWords.length }} / 10</span>
          <button
            class="bj-btn bj-btn-send"
            :disabled="selectedWords.length === 0"
            @click="sendMessage"
          >Отправить</button>
        </div>
      </div>

      <!-- Last Message Display -->
      <div v-if="bjState.lastMessage" class="bj-last-message">
        <span class="bj-msg-author">{{ bjState.lastMessage.author }}:</span>
        "{{ bjState.lastMessage.text }}"
      </div>
    </template>
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

.bj-player-count {
  font-size: 0.85rem;
  color: #999;
  font-weight: 600;
}

.bj-hand-area {
  margin-bottom: 12px;
  padding: 6px 8px;
  border-radius: 8px;
}

.bj-hand-me {
  background: rgba(120, 60, 200, 0.08);
  border: 1px solid rgba(120, 60, 200, 0.2);
}

.bj-hand-active {
  border-color: rgba(120, 60, 200, 0.5);
}

.bj-hand-label {
  font-size: 0.85rem;
  color: #ccc;
  margin-bottom: 6px;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.bj-name-me {
  color: #d4b8ff;
  font-weight: 700;
}

.bj-hand-total {
  background: rgba(120, 60, 200, 0.25);
  color: #d4b8ff;
  padding: 1px 8px;
  border-radius: 8px;
  font-size: 0.8rem;
  font-weight: 700;
}

.bj-wins-badge {
  background: rgba(76, 175, 80, 0.2);
  color: #4caf50;
  padding: 1px 6px;
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
}

.bj-player-result {
  font-weight: 700;
  font-size: 0.85rem;
}

.bj-player-status {
  font-size: 0.8rem;
  color: #888;
}

.bj-status-active {
  color: #b47aff;
  animation: pulse-msg 1s ease-in-out infinite;
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

.bj-btn-send {
  background: #7b2ff2;
  color: #fff;
}
.bj-btn-send:hover:not(:disabled) {
  background: #8e44ff;
}

.bj-dealer-msg {
  color: #b47aff;
  font-weight: 600;
  animation: pulse-msg 1s ease-in-out infinite;
}

.bj-waiting-msg {
  text-align: center;
  color: #888;
  padding: 16px;
}

@keyframes pulse-msg {
  0%, 100% { opacity: 0.6; }
  50% { opacity: 1; }
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

/* ── Message Panel ──────────────────────────────────────────────── */

.bj-message-panel {
  margin-top: 16px;
  padding: 12px;
  background: rgba(30, 20, 50, 0.6);
  border: 1px solid rgba(120, 60, 200, 0.25);
  border-radius: 10px;
}

.bj-message-title {
  font-size: 0.95rem;
  font-weight: 700;
  color: #d4b8ff;
  margin-bottom: 8px;
}

.bj-message-preview {
  background: rgba(0, 0, 0, 0.3);
  padding: 8px 12px;
  border-radius: 6px;
  color: #e0d0ff;
  font-style: italic;
  font-size: 0.9rem;
  margin-bottom: 10px;
  min-height: 36px;
  display: flex;
  align-items: center;
}

.bj-message-empty {
  color: #666;
}

.bj-word-category {
  margin-bottom: 8px;
}

.bj-category-name {
  font-size: 0.75rem;
  color: #888;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 4px;
}

.bj-word-list {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.bj-word-btn {
  padding: 3px 10px;
  border: 1px solid rgba(120, 60, 200, 0.25);
  border-radius: 14px;
  background: rgba(120, 60, 200, 0.08);
  color: #bbb;
  font-size: 0.8rem;
  cursor: pointer;
  transition: all 0.15s;
}

.bj-word-btn:hover {
  background: rgba(120, 60, 200, 0.2);
  color: #d4b8ff;
}

.bj-word-selected {
  background: rgba(120, 60, 200, 0.4);
  color: #fff;
  border-color: rgba(120, 60, 200, 0.6);
}

.bj-message-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 10px;
}

.bj-word-count {
  font-size: 0.8rem;
  color: #888;
}

/* ── Last Message Display ───────────────────────────────────────── */

.bj-last-message {
  margin-top: 12px;
  padding: 8px 12px;
  background: rgba(255, 215, 0, 0.05);
  border: 1px solid rgba(255, 215, 0, 0.15);
  border-radius: 8px;
  color: #d4b8ff;
  font-size: 0.85rem;
  font-style: italic;
}

.bj-msg-author {
  color: #ffd700;
  font-weight: 700;
  font-style: normal;
}
</style>
