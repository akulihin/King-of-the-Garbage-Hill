import { describe, expect, it } from 'vitest'
import {
  abortChess,
  applyChessCommand,
  chooseDeterministicChessAiCommand,
  createChessCommand,
  createChessRulesIdentity,
  createChessState,
  createRecommendedChessPlan,
  isWhiteKingInCheck,
  legalChessMoves,
  replayChess,
  resolveChess,
  validateChessPlan,
} from './engine'
import type {
  ChessPieceSetup,
  ChessPlan,
  ChessState,
  ChessVariantRules,
} from './types'

function customPlan(
  setup: ChessPieceSetup[],
  options: { drawPlyLimit?: number, repetitionCount?: number, antonCadence?: number } = {},
): ChessPlan {
  const base = createRecommendedChessPlan({ id: 'chess-spec', sessionId: 'chess-spec-session' })
  const rules: ChessVariantRules = {
    ...base.rules,
    drawPlyLimit: options.drawPlyLimit ?? base.rules.drawPlyLimit,
    repetitionCount: options.repetitionCount ?? base.rules.repetitionCount,
  }
  const anton = {
    ...base.anton,
    extraEveryPlayerTurns: options.antonCadence ?? base.anton.extraEveryPlayerTurns,
  }
  const maxCommands = 256
  return {
    ...base,
    rules,
    anton,
    setup,
    maxCommands,
    rulesIdentity: createChessRulesIdentity(
      base.rulesIdentity.configSchemaVersion,
      rules,
      anton,
      setup,
      maxCommands,
      base.settlement,
    ),
  }
}

function move(plan: ChessPlan, state: ChessState, from: string, to: string): ChessState {
  const command = createChessCommand(plan, state, { kind: 'move', from, to })
  const next = applyChessCommand(plan, state, command)
  expect(next.error, `${from}-${to}`).toBeNull()
  return next
}

function skipAnton(plan: ChessPlan, state: ChessState): ChessState {
  const next = applyChessCommand(plan, state, createChessCommand(plan, state, { kind: 'skip-anton' }))
  expect(next.error).toBeNull()
  return next
}

describe('Empire\'s Endgame Chess variant', () => {
  it('builds the recommended 8x8 kingless-black setup and ordinary opening moves', () => {
    const plan = createRecommendedChessPlan()
    const state = createChessState(plan)

    expect(validateChessPlan(plan)).toEqual([])
    expect(plan.setup).toHaveLength(31)
    expect(plan.setup.filter(piece => piece.side === 'black' && piece.role === 'king')).toEqual([])
    expect(plan.setup.find(piece => piece.id === plan.anton.pieceId)).toMatchObject({
      square: 'g8',
      side: 'black',
      role: 'knight',
      anton: true,
      sourceDefinitionId: 'card-spades-jack',
    })
    expect(legalChessMoves(plan, state)).toEqual(expect.arrayContaining([
      expect.objectContaining({ from: 'e2', to: 'e4' }),
      expect.objectContaining({ from: 'b1', to: 'a3' }),
    ]))
    expect(legalChessMoves(plan, state).some(candidate => (
      candidate.from === 'e1' && candidate.to === 'g1'
    ))).toBe(false)
  })

  it('uses standard sliding attacks and permits only moves that resolve white check', () => {
    const plan = customPlan([
      { id: 'white-king', side: 'white', role: 'king', square: 'e1' },
      { id: 'white-rook', side: 'white', role: 'rook', square: 'a1' },
      { id: 'black-rook', side: 'black', role: 'rook', square: 'e8' },
      { id: 'black-anton', side: 'black', role: 'knight', square: 'g8', anton: true },
    ])
    const state = createChessState(plan)

    expect(isWhiteKingInCheck(state)).toBe(true)
    expect(legalChessMoves(plan, state).some(candidate => candidate.pieceId === 'white-rook')).toBe(false)
    expect(legalChessMoves(plan, state).map(candidate => candidate.to)).toEqual(
      expect.arrayContaining(['d1', 'f1']),
    )
  })

  it('promotes pawns only to queens and has no en-passant or castling command', () => {
    const plan = customPlan([
      { id: 'white-king', side: 'white', role: 'king', square: 'e1' },
      { id: 'white-pawn', side: 'white', role: 'pawn', square: 'a7' },
      { id: 'black-anton', side: 'black', role: 'knight', square: 'g8', anton: true },
      { id: 'black-rook', side: 'black', role: 'rook', square: 'h8' },
    ])
    const state = createChessState(plan)
    const promotion = legalChessMoves(plan, state).find(candidate => (
      candidate.from === 'a7' && candidate.to === 'a8'
    ))

    expect(promotion?.promotion).toBe('queen')
    const promoted = move(plan, state, 'a7', 'a8')
    expect(promoted.pieces['white-pawn']).toMatchObject({ role: 'queen', square: 'a8' })
    expect(plan.rules).toMatchObject({ castling: false, enPassant: false, promotion: 'queen-only' })
  })

  it('wins by capturing every black piece and loses when the white king is captured', () => {
    const captureAllPlan = customPlan([
      { id: 'white-king', side: 'white', role: 'king', square: 'e1' },
      { id: 'white-rook', side: 'white', role: 'rook', square: 'g1' },
      { id: 'black-anton', side: 'black', role: 'knight', square: 'g8', anton: true },
    ])
    const won = move(captureAllPlan, createChessState(captureAllPlan), 'g1', 'g8')
    expect(won).toMatchObject({
      outcome: 'white-win',
      terminalReason: 'black-army-captured',
      capturedPieceIds: ['black-anton'],
    })

    const kingCapturePlan = customPlan([
      { id: 'white-king', side: 'white', role: 'king', square: 'e1' },
      { id: 'black-rook', side: 'black', role: 'rook', square: 'e2' },
      { id: 'black-anton', side: 'black', role: 'knight', square: 'g8', anton: true },
    ])
    const beforeCapture = createChessState(kingCapturePlan)
    beforeCapture.sideToAct = 'black'
    const lost = move(kingCapturePlan, beforeCapture, 'e2', 'e1')
    expect(lost).toMatchObject({ outcome: 'black-win', terminalReason: 'white-king-captured' })
  })

  it('gives white an optional shared-Anton move after every two regular white turns', () => {
    const plan = customPlan([
      { id: 'white-king', side: 'white', role: 'king', square: 'e1' },
      { id: 'black-pawn-a', side: 'black', role: 'pawn', square: 'a7' },
      { id: 'black-pawn-e', side: 'black', role: 'pawn', square: 'e7' },
      { id: 'black-anton', side: 'black', role: 'knight', square: 'g8', anton: true },
    ])
    let state = createChessState(plan)
    state = move(plan, state, 'e1', 'e2')
    state = move(plan, state, 'a7', 'a6')
    state = move(plan, state, 'e2', 'e3')

    expect(state).toMatchObject({
      sideToAct: 'white',
      phase: 'anton-extra',
      playerTurnsCompleted: 2,
      playerTurnsSinceAntonExtra: 2,
    })
    expect(legalChessMoves(plan, state)).toContainEqual(expect.objectContaining({
      pieceId: 'black-anton',
      from: 'g8',
      to: 'e7',
      capturedPieceId: 'black-pawn-e',
    }))
    state = move(plan, state, 'g8', 'e7')
    expect(state).toMatchObject({ sideToAct: 'black', phase: 'regular', playerTurnsSinceAntonExtra: 0 })
    expect(state.pieces['black-anton']).toMatchObject({ side: 'black', square: 'e7' })
    expect(state.pieces['black-pawn-e']).toBeUndefined()
    expect(legalChessMoves(plan, state).some(candidate => candidate.pieceId === 'black-anton')).toBe(true)
  })

  it('draws on the literal ply cap and on the third repeated full position', () => {
    const setup: ChessPieceSetup[] = [
      { id: 'white-king', side: 'white', role: 'king', square: 'e1' },
      { id: 'white-knight', side: 'white', role: 'knight', square: 'b1' },
      { id: 'black-knight', side: 'black', role: 'knight', square: 'b8' },
      { id: 'black-anton', side: 'black', role: 'knight', square: 'g8', anton: true },
    ]
    const cappedPlan = customPlan(setup, { drawPlyLimit: 2 })
    let capped = createChessState(cappedPlan)
    capped = move(cappedPlan, capped, 'b1', 'a3')
    capped = move(cappedPlan, capped, 'b8', 'a6')
    expect(capped).toMatchObject({ outcome: 'draw', terminalReason: 'ply-cap', ply: 2 })

    const repeatedPlan = customPlan(setup)
    let repeated = createChessState(repeatedPlan)
    for (let cycle = 0; cycle < 2; cycle += 1) {
      repeated = move(repeatedPlan, repeated, 'b1', 'a3')
      repeated = move(repeatedPlan, repeated, 'b8', 'a6')
      repeated = move(repeatedPlan, repeated, 'a3', 'b1')
      expect(repeated.phase).toBe('anton-extra')
      repeated = skipAnton(repeatedPlan, repeated)
      repeated = move(repeatedPlan, repeated, 'a6', 'b8')
    }
    expect(repeated).toMatchObject({
      outcome: 'draw',
      terminalReason: 'threefold-repetition',
      ply: 8,
    })
  })

  it('selects deterministic winning AI moves and replays authenticated results byte-for-byte', () => {
    const plan = customPlan([
      { id: 'white-king', side: 'white', role: 'king', square: 'e1' },
      { id: 'white-queen', side: 'white', role: 'queen', square: 'd1' },
      { id: 'black-rook', side: 'black', role: 'rook', square: 'e8' },
      { id: 'black-anton', side: 'black', role: 'knight', square: 'g8', anton: true },
    ])
    const state = createChessState(plan)
    state.sideToAct = 'black'
    const first = chooseDeterministicChessAiCommand(plan, state)
    const second = chooseDeterministicChessAiCommand(plan, state)

    expect(second).toEqual(first)
    expect(first).toMatchObject({ kind: 'move', from: 'e8', to: 'e1' })
    expect(applyChessCommand(plan, state, first!)).toMatchObject({
      outcome: 'black-win',
      terminalReason: 'white-king-captured',
    })

    const capturePlan = customPlan([
      { id: 'white-king', side: 'white', role: 'king', square: 'e1' },
      { id: 'white-rook', side: 'white', role: 'rook', square: 'g1' },
      { id: 'black-anton', side: 'black', role: 'knight', square: 'g8', anton: true },
    ])
    const initial = createChessState(capturePlan)
    const command = createChessCommand(capturePlan, initial, { kind: 'move', from: 'g1', to: 'g8' })
    const one = resolveChess(capturePlan, 'seed', [command])
    const two = resolveChess(capturePlan, 'seed', [command])
    expect(two).toEqual(one)
    expect(one).toMatchObject({ outcome: 'white-win', winner: 'white', completedPly: 1, error: null })
    expect(replayChess(capturePlan, [command])).toEqual(applyChessCommand(capturePlan, initial, command))
    expect(abortChess(capturePlan, 'seed', [])).toMatchObject({ outcome: 'aborted', terminalReason: 'aborted' })
  })

  it('rejects stale plan identity without mutating the supplied state', () => {
    const plan = createRecommendedChessPlan()
    const state = createChessState(plan)
    const before = structuredClone(state)
    const command = createChessCommand(plan, state, { kind: 'move', from: 'e2', to: 'e4' })
    command.sessionId = 'stale-session'
    const rejected = applyChessCommand(plan, state, command)

    expect(state).toEqual(before)
    expect(rejected).toMatchObject({ outcome: 'invalid', terminalReason: 'invalid-command' })
    expect(rejected.error).toMatch(/another plan or session/i)
  })
})
