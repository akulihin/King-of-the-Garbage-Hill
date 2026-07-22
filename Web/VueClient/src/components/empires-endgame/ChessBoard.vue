<script setup lang="ts">
import { computed, nextTick, onMounted, ref, shallowRef } from 'vue'
import { Crown, RotateCcw, Swords } from 'lucide-vue-next'
import {
  applyChessCommand,
  chooseDeterministicChessAiCommand,
  createChessCommand,
  createChessState,
  isWhiteKingInCheck,
  legalChessMoves,
  resolveChess,
} from '../../features/empires-endgame/chess/engine'
import type {
  ChessCommand,
  ChessPieceState,
  ChessResult,
  ChessRole,
  ChessSquare,
  ChessState,
} from '../../features/empires-endgame/chess/types'
import type { EmpiresChessMinigameSession } from '../../features/empires-endgame/types'
import MinigameAbortDialog from './MinigameAbortDialog.vue'

const props = defineProps<{
  session: EmpiresChessMinigameSession
  qaMode?: boolean
}>()

const emit = defineEmits<{
  resolved: [result: ChessResult]
  abort: [commandLog: ChessCommand[]]
}>()

const files = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'] as const
const ranks = [8, 7, 6, 5, 4, 3, 2, 1] as const
const state = shallowRef<ChessState>(createChessState(props.session.plan))
const selectedSquare = ref<ChessSquare | null>(null)
const abortOpen = ref(false)
const aiBusy = ref(false)
const emitted = ref(false)

const glyphs: Record<ChessPieceState['side'], Record<ChessRole, string>> = {
  white: { king: '♔', queen: '♕', rook: '♖', bishop: '♗', knight: '♘', pawn: '♙' },
  black: { king: '♚', queen: '♛', rook: '♜', bishop: '♝', knight: '♞', pawn: '♟' },
}

const roleNames: Record<ChessRole, string> = {
  king: 'король',
  queen: 'ферзь',
  rook: 'ладья',
  bishop: 'слон',
  knight: 'конь',
  pawn: 'пешка',
}

const boardSquares = computed(() => ranks.flatMap(rank => files.map(file => `${file}${rank}`)))
const pieceBySquare = computed<Record<string, ChessPieceState>>(() => Object.fromEntries(
  Object.values(state.value.pieces).map(piece => [piece.square, piece]),
))
const legalMoves = computed(() => legalChessMoves(props.session.plan, state.value))
const selectedTargets = computed(() => new Set(
  legalMoves.value.filter(move => move.from === selectedSquare.value).map(move => move.to),
))
const playerCanAct = computed(() => (
  !state.value.terminalReason && !aiBusy.value && state.value.sideToAct === 'white'
))
const capturedWhite = computed(() => state.value.capturedPieceIds.filter(id => (
  props.session.plan.setup.find(piece => piece.id === id)?.side === 'white'
)).length)
const capturedBlack = computed(() => state.value.capturedPieceIds.filter(id => (
  props.session.plan.setup.find(piece => piece.id === id)?.side === 'black'
)).length)
const statusText = computed(() => {
  if (state.value.outcome === 'white-win') return 'Победа: все чёрные фигуры взяты.'
  if (state.value.outcome === 'black-win') return 'Поражение: белый король потерян.'
  if (state.value.outcome === 'draw') return 'Ничья: партия завершена правилами варианта.'
  if (state.value.outcome === 'invalid') return state.value.error ?? 'Партия недействительна.'
  if (state.value.phase === 'anton-extra') return 'Ваш дополнительный ход: переместите Антона или пропустите.'
  if (state.value.sideToAct === 'black' || aiBusy.value) return 'Двор Бога Азарта обдумывает ход…'
  return isWhiteKingInCheck(state.value)
    ? 'Ваш ход. Белый король под шахом.'
    : 'Ваш ход белыми.'
})

function pieceLabel(piece: ChessPieceState): string {
  if (piece.anton) return 'Антон, общий чёрный конь'
  const authored: Record<string, string> = {
    'white-hearts-ace': 'Червовый туз, белый слон',
    'white-hearts-king': 'Червовый король, белый ферзь',
    'white-hearts-queen': 'Червовая дама, белый король',
    'white-hearts-jack': 'Червовый валет, белый слон',
    'white-clean-streets': 'Чистые улицы, белая ладья',
  }
  return authored[piece.id] ?? `${piece.side === 'white' ? 'Белая' : 'Чёрная'} ${roleNames[piece.role]}`
}

function squareLabel(square: ChessSquare): string {
  const piece = pieceBySquare.value[square]
  return piece ? `${square}: ${pieceLabel(piece)}` : `${square}: пусто`
}

function isSelectable(square: ChessSquare): boolean {
  return playerCanAct.value && legalMoves.value.some(move => move.from === square)
}

function settleIfTerminal(): void {
  if (!state.value.terminalReason || emitted.value) return
  emitted.value = true
  emit('resolved', resolveChess(props.session.plan, props.session.seed, state.value.commandLog))
}

function apply(command: ChessCommand): void {
  state.value = applyChessCommand(props.session.plan, state.value, command)
  selectedSquare.value = null
  settleIfTerminal()
}

async function runBlackTurn(): Promise<void> {
  if (emitted.value || state.value.terminalReason || state.value.sideToAct !== 'black') return
  aiBusy.value = true
  await nextTick()
  const command = chooseDeterministicChessAiCommand(props.session.plan, state.value)
  if (command) apply(command)
  aiBusy.value = false
}

function clickSquare(square: ChessSquare): void {
  if (!playerCanAct.value) return
  if (selectedSquare.value && selectedTargets.value.has(square)) {
    const move = legalMoves.value.find(candidate => (
      candidate.from === selectedSquare.value && candidate.to === square
    ))!
    apply(createChessCommand(props.session.plan, state.value, {
      kind: 'move',
      from: move.from,
      to: move.to,
      ...(move.promotion ? { promotion: move.promotion } : {}),
    }))
    if (!state.value.terminalReason && state.value.sideToAct === 'black') void runBlackTurn()
    return
  }
  selectedSquare.value = isSelectable(square) ? square : null
}

function skipAnton(): void {
  if (!playerCanAct.value || state.value.phase !== 'anton-extra') return
  apply(createChessCommand(props.session.plan, state.value, { kind: 'skip-anton' }))
  if (!state.value.terminalReason) void runBlackTurn()
}

function confirmAbort(): void {
  abortOpen.value = false
  if (emitted.value) return
  emit('abort', structuredClone(state.value.commandLog))
}

function qaResolve(): void {
  if (!props.qaMode || emitted.value) return
  let next = state.value
  while (!next.terminalReason && next.commandLog.length < props.session.plan.maxCommands) {
    const command = chooseDeterministicChessAiCommand(props.session.plan, next)
    if (!command) break
    next = applyChessCommand(props.session.plan, next, command)
  }
  state.value = next
  settleIfTerminal()
}

onMounted(() => settleIfTerminal())
</script>

<template>
  <section class="chess" data-testid="chess-board" aria-labelledby="chess-title">
    <header class="chess__header">
      <div>
        <span>Колизей · шахматы Бога Азарта</span>
        <h2 id="chess-title"><Crown :size="25" /> Последняя партия двора</h2>
        <p>Белые побеждают, взяв все чёрные фигуры. У чёрных нет короля; каждые два ваших хода Антоном можно сходить дополнительно.</p>
      </div>
      <dl aria-live="polite">
        <div><dt>Ходы</dt><dd data-testid="chess-ply">{{ state.ply }}/{{ session.plan.rules.drawPlyLimit }}</dd></div>
        <div><dt>Взято белых</dt><dd>{{ capturedWhite }}</dd></div>
        <div><dt>Взято чёрных</dt><dd>{{ capturedBlack }}</dd></div>
      </dl>
    </header>

    <div class="chess__status" :class="{ 'chess__status--check': isWhiteKingInCheck(state) }" role="status" aria-live="polite">
      <Swords :size="19" /> {{ statusText }}
    </div>

    <div class="chess__layout">
      <div class="chess__board-shell">
        <div class="chess__ranks" aria-hidden="true">
          <span v-for="rank in ranks" :key="rank">{{ rank }}</span>
        </div>
        <div class="chess__board" role="grid" aria-label="Шахматная доска, белые снизу">
          <button
            v-for="(square, index) in boardSquares"
            :key="square"
            type="button"
            role="gridcell"
            class="chess__square"
            :class="{
              'chess__square--dark': (Math.floor(index / 8) + index % 8) % 2 === 1,
              'chess__square--selected': selectedSquare === square,
              'chess__square--target': selectedTargets.has(square),
              'chess__square--last': state.lastMove?.from === square || state.lastMove?.to === square,
            }"
            :data-testid="`chess-square-${square}`"
            :aria-label="squareLabel(square)"
            :aria-pressed="selectedSquare === square"
            :disabled="!playerCanAct"
            @click="clickSquare(square)"
          >
            <span v-if="pieceBySquare[square]" class="chess__piece" :class="`chess__piece--${pieceBySquare[square].side}`" aria-hidden="true">
              {{ glyphs[pieceBySquare[square].side][pieceBySquare[square].role] }}
            </span>
            <span v-if="selectedTargets.has(square)" class="chess__target-dot" aria-hidden="true"></span>
          </button>
        </div>
        <div class="chess__files" aria-hidden="true">
          <span v-for="file in files" :key="file">{{ file }}</span>
        </div>
      </div>

      <aside class="chess__aside">
        <h3>Правила ставки</h3>
        <ul>
          <li>Победа: +{{ session.plan.settlement.victoryGold }} золота и +{{ session.plan.settlement.victoryKnowledge }} знаний.</li>
          <li>Поражение или выход: {{ session.plan.settlement.defeatAllCityLoyaltyDelta }} к лояльности каждого города.</li>
          <li>Рокировка и взятие на проходе отключены; пешка превращается только в ферзя.</li>
        </ul>
        <button
          v-if="state.phase === 'anton-extra' && !state.terminalReason"
          type="button"
          class="chess__anton"
          data-testid="chess-skip-anton"
          :disabled="!playerCanAct"
          @click="skipAnton"
        >
          Пропустить ход Антоном
        </button>
        <button v-if="qaMode" type="button" data-testid="chess-qa-resolve" @click="qaResolve">
          <RotateCcw :size="17" /> QA: доиграть
        </button>
        <button
          type="button"
          class="chess__abort"
          data-testid="chess-open-abort"
          :disabled="emitted"
          @click="abortOpen = true"
        >
          Покинуть партию
        </button>
      </aside>
    </div>

    <MinigameAbortDialog
      v-if="abortOpen"
      id-prefix="chess-abort"
      title="Покинуть шахматную партию?"
      description="Выход считается поражением: лояльность каждого города снизится на 1."
      confirm-label="Покинуть и принять штраф"
      continue-label="Продолжить партию"
      confirm-test-id="chess-confirm-abort"
      continue-test-id="chess-continue"
      @confirm="confirmAbort"
      @cancel="abortOpen = false"
    />
  </section>
</template>

<style scoped>
.chess {
  display: grid;
  gap: 18px;
  width: min(1100px, 100%);
  margin: 0 auto;
  color: #f5eadf;
}

.chess__header {
  display: flex;
  justify-content: space-between;
  gap: 18px;
  padding: 18px;
  border: 1px solid rgba(224, 184, 99, .45);
  background: linear-gradient(135deg, rgba(72, 54, 31, .92), rgba(23, 28, 23, .96));
}

.chess__header span { color: #e0b863; font-size: .75rem; letter-spacing: .12em; text-transform: uppercase; }
.chess__header h2 { display: flex; gap: 9px; align-items: center; margin: 5px 0; }
.chess__header p { max-width: 680px; margin: 0; color: rgba(245, 234, 223, .72); line-height: 1.45; }
.chess__header dl { display: flex; flex-wrap: wrap; gap: 14px; margin: 0; }
.chess__header dl div { min-width: 82px; text-align: center; }
.chess__header dt { color: rgba(245, 234, 223, .62); font-size: .72rem; }
.chess__header dd { margin: 3px 0 0; color: #ffe5a1; font-size: 1.05rem; font-weight: 700; }

.chess__status {
  display: flex;
  gap: 8px;
  align-items: center;
  min-height: 42px;
  padding: 9px 14px;
  border-left: 3px solid #d5b368;
  background: rgba(213, 179, 104, .1);
}
.chess__status--check { border-color: #e86f52; background: rgba(232, 111, 82, .13); }

.chess__layout { display: grid; grid-template-columns: minmax(320px, 680px) minmax(220px, 1fr); gap: 22px; align-items: start; }
.chess__board-shell { display: grid; grid-template-columns: 20px minmax(0, 1fr); grid-template-rows: minmax(0, 1fr) 20px; }
.chess__board { grid-column: 2; display: grid; grid-template-columns: repeat(8, 1fr); aspect-ratio: 1; border: 5px solid #6f4f2c; box-shadow: 0 18px 45px rgba(0, 0, 0, .5); }
.chess__ranks { grid-row: 1; display: grid; grid-template-rows: repeat(8, 1fr); align-items: center; color: rgba(245, 234, 223, .62); font-size: .72rem; }
.chess__files { grid-column: 2; display: grid; grid-template-columns: repeat(8, 1fr); justify-items: center; align-items: end; color: rgba(245, 234, 223, .62); font-size: .72rem; }
.chess__square { position: relative; display: grid; place-items: center; min-width: 0; min-height: 0; padding: 0; border: 0; background: #d9c49e; color: #181a17; cursor: pointer; }
.chess__square--dark { background: #75684c; }
.chess__square--last { box-shadow: inset 0 0 0 999px rgba(255, 225, 111, .22); }
.chess__square--selected { box-shadow: inset 0 0 0 4px #fff2a5; z-index: 1; }
.chess__square--target::after { content: ''; position: absolute; inset: 8%; border: 2px solid rgba(92, 35, 24, .68); border-radius: 50%; }
.chess__square:focus-visible { z-index: 2; outline: 3px solid #fff2a5; outline-offset: -3px; }
.chess__square:disabled { cursor: wait; opacity: 1; }
.chess__piece { position: relative; z-index: 1; font-family: Georgia, 'Times New Roman', serif; font-size: clamp(1.7rem, 6.2vw, 4.2rem); line-height: 1; filter: drop-shadow(0 2px 1px rgba(0, 0, 0, .38)); }
.chess__piece--white { color: #fff8df; text-shadow: 0 0 2px #191b18, 0 1px 0 #191b18; }
.chess__piece--black { color: #171915; text-shadow: 0 1px 0 rgba(255, 255, 255, .18); }
.chess__target-dot { position: absolute; width: 20%; aspect-ratio: 1; border-radius: 50%; background: rgba(45, 37, 22, .5); }

.chess__aside { display: grid; gap: 12px; padding: 18px; border: 1px solid rgba(224, 184, 99, .28); background: rgba(22, 28, 22, .9); }
.chess__aside h3 { margin: 0; color: #e7c675; }
.chess__aside ul { display: grid; gap: 9px; margin: 0; padding-left: 19px; color: rgba(245, 234, 223, .75); line-height: 1.4; }
.chess__aside button { display: inline-flex; justify-content: center; gap: 7px; align-items: center; min-height: 42px; padding: 8px 12px; border: 1px solid rgba(224, 184, 99, .55); background: rgba(92, 79, 45, .82); color: inherit; cursor: pointer; }
.chess__aside button:focus-visible { outline: 2px solid #fff0a8; outline-offset: 2px; }
.chess__aside button:disabled { opacity: .52; cursor: default; }
.chess__anton { border-color: #88b6d5 !important; background: rgba(38, 78, 104, .78) !important; }
.chess__abort { margin-top: 6px; border-color: rgba(213, 104, 83, .65) !important; background: rgba(86, 37, 29, .72) !important; }

@media (max-width: 780px) {
  .chess__header { display: grid; }
  .chess__layout { grid-template-columns: 1fr; }
  .chess__piece { font-size: clamp(1.65rem, 10vw, 3.8rem); }
}
</style>
