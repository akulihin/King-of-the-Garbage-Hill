import { describe, expect, it } from 'vitest'
import rawConfig from '../../../../public/empires-endgame/game-config.json'
import { validateEmpiresConfig } from '../config'
import {
  alchemyCommandDisabledReason,
  consumeAlchemyFrameTime,
  createAlchemyRulesIdentity,
  createAlchemySimulation,
  replayAlchemy,
  stepAlchemySimulation,
} from './engine'
import { ALCHEMY_QA_POLICIES, createAlchemyPolicyCommandLog } from './qa'
import type { AlchemyCommand, AlchemyPlan, AlchemySimulationState } from './types'

function plan(overrides: Partial<AlchemyPlan> = {}): AlchemyPlan {
  const value: AlchemyPlan = {
    id: 'plan:test',
    sessionId: 'session:test',
    rulesIdentity: { configSchemaVersion: 15, rulesDigest: 'rules:test' },
    originCityId: 'city:test',
    buildingId: 'building-alchemy',
    recipe: {
      id: 'recipe:test',
      name: 'Test assembly',
      description: 'test',
      mode: 'assembly',
      family: 'experiment',
      initialCells: [{ x: 6, y: 6, color: 'gray' }],
      targetCells: [{ x: 6, y: 6 }, { x: 0, y: 0 }],
      pieceDefinitionIds: ['single'],
      prerequisites: [],
      rewards: [],
    },
    tickMs: 50,
    maxTicks: 500,
    maxCommands: 100,
    maxCatchUpTicksPerFrame: 4,
    board: { width: 13, height: 13, centerX: 6, centerY: 6 },
    spawn: { minDelayTicks: 50, maxDelayTicks: 50, baseMoveIntervalTicks: 100, inwardSpeedMultiplier: 3 },
    acceleration: {
      baseSpeedPercent: 100,
      stepPercent: 1,
      piecesPerStep: 1,
      explosionThresholdPercent: 400,
      explosionBoundary: 'above',
    },
    reagents: { removeColorCharges: 1, addGrayCharges: 1, resetAccelerationCharges: 1 },
    explosion: {
      epidemicDefinitionId: 'epidemic-plague',
      severityMultiplier: 1.5,
      lockBuildingForCon: true,
    },
    colors: ['red', 'yellow', 'blue', 'green'],
    pieces: [{ id: 'single', name: 'Single', cells: [{ x: 0, y: 0 }] }],
  }
  return { ...value, ...overrides }
}

function command(
  value: AlchemyPlan,
  state: AlchemySimulationState,
  body: Omit<AlchemyCommand, 'tick' | 'sequence' | 'sessionId' | 'planId'>,
): AlchemyCommand {
  return {
    tick: state.tick,
    sequence: state.commandLog.length,
    sessionId: value.sessionId,
    planId: value.id,
    ...body,
  } as AlchemyCommand
}

function replacePieces(state: AlchemySimulationState, pieces: AlchemySimulationState['activePieces']) {
  state.activePieces = pieces
  state.controlledPieceId = pieces[0]?.id ?? null
  state.nextSpawnTick = Number.MAX_SAFE_INTEGER
}

describe('Tetris-alchemy fixed-step engine', () => {
  it('spawns deterministically from all four sides and selects nearest pieces with a stable tie order', () => {
    const seen = new Set<string>()
    for (let seed = 1; seed <= 64; seed += 1) {
      seen.add(createAlchemySimulation(plan(), seed).activePieces[0].side)
    }
    expect([...seen].sort()).toEqual(['bottom', 'left', 'right', 'top'])

    const value = plan()
    const state = createAlchemySimulation(value, 1)
    replacePieces(state, [
      { id: 'right', definitionId: 'single', side: 'right', color: 'blue', anchor: { x: 9, y: 6 }, rotation: 0, nextMoveTick: 999 },
      { id: 'top', definitionId: 'single', side: 'top', color: 'red', anchor: { x: 6, y: 3 }, rotation: 0, nextMoveTick: 999 },
      { id: 'far', definitionId: 'single', side: 'bottom', color: 'green', anchor: { x: 6, y: 11 }, rotation: 0, nextMoveTick: 999 },
    ])
    stepAlchemySimulation(value, state, [command(value, state, { kind: 'remove-color', color: 'green' })])
    expect(state.controlledPieceId).toBe('top')

    state.reagentCharges.removeColor = 1
    stepAlchemySimulation(value, state, [command(value, state, { kind: 'remove-color', color: 'red' })])
    expect(state.controlledPieceId).toBe('right')
  })

  it('forbids backward movement, applies the sourced inward x3 movement, and locks on construction collision', () => {
    const value = plan()
    const state = createAlchemySimulation(value, 2)
    replacePieces(state, [{
      id: 'top', definitionId: 'single', side: 'top', color: 'red', anchor: { x: 6, y: 1 }, rotation: 0, nextMoveTick: 999,
    }])
    const backward = command(value, state, { kind: 'move', direction: 'up' })
    expect(alchemyCommandDisabledReason(value, state, backward)).toContain('cannot move back')
    stepAlchemySimulation(value, state, [command(value, state, { kind: 'move', direction: 'down' })])
    expect(state.activePieces[0].anchor.y).toBe(4)

    state.activePieces[0].anchor.y = 5
    stepAlchemySimulation(value, state, [command(value, state, { kind: 'move', direction: 'down' })])
    expect(state.activePieces).toHaveLength(0)
    expect(state.construction).toContainEqual({ x: 6, y: 5, color: 'red' })
    expect(state.settledPieces).toBe(1)

    const rotationPlan = plan({
      pieces: [{ id: 'bar', name: 'Bar', cells: [{ x: -1, y: 0 }, { x: 0, y: 0 }, { x: 1, y: 0 }] }],
      recipe: { ...plan().recipe, pieceDefinitionIds: ['bar'] },
    })
    const rotationState = createAlchemySimulation(rotationPlan, 2)
    replacePieces(rotationState, [{
      id: 'top-bar', definitionId: 'bar', side: 'top', color: 'red', anchor: { x: 2, y: 6 }, rotation: 0, nextMoveTick: 999,
    }])
    expect(alchemyCommandDisabledReason(
      rotationPlan,
      rotationState,
      command(rotationPlan, rotationState, { kind: 'rotate' }),
    )).toContain('cannot rotate')
  })

  it('applies remove-color, add-gray, and acceleration-reset reagents through logged commands', () => {
    const value = plan()
    const state = createAlchemySimulation(value, 3)
    state.construction.push({ x: 5, y: 6, color: 'red' })
    replacePieces(state, [
      { id: 'red', definitionId: 'single', side: 'top', color: 'red', anchor: { x: 6, y: 1 }, rotation: 0, nextMoveTick: 999 },
      { id: 'blue', definitionId: 'single', side: 'right', color: 'blue', anchor: { x: 11, y: 6 }, rotation: 0, nextMoveTick: 999 },
    ])
    stepAlchemySimulation(value, state, [command(value, state, { kind: 'remove-color', color: 'red' })])
    expect(state.construction.some(cell => cell.color === 'red')).toBe(false)
    expect(state.activePieces.map(piece => piece.id)).toEqual(['blue'])
    stepAlchemySimulation(value, state, [command(value, state, { kind: 'add-gray', pieceId: 'blue' })])
    expect(state.activePieces[0].color).toBe('gray')
    state.accelerationPieces = 5
    state.speedPercent = 115
    stepAlchemySimulation(value, state, [command(value, state, { kind: 'reset-acceleration' })])
    expect(state.speedPercent).toBe(100)
    expect(state.reagentCharges).toEqual({ removeColor: 0, addGray: 0, resetAcceleration: 0 })
  })

  it('uses an explicit above-400 boundary and supports the alternative at-or-above boundary', () => {
    const abovePlan = plan({
      spawn: { minDelayTicks: 50, maxDelayTicks: 50, baseMoveIntervalTicks: 100, inwardSpeedMultiplier: 1 },
      acceleration: {
        baseSpeedPercent: 100,
        stepPercent: 300,
        piecesPerStep: 1,
        explosionThresholdPercent: 400,
        explosionBoundary: 'above',
      },
    })
    const exactly = createAlchemySimulation(abovePlan, 4)
    replacePieces(exactly, [{ id: 'top', definitionId: 'single', side: 'top', color: 'red', anchor: { x: 6, y: 5 }, rotation: 0, nextMoveTick: 999 }])
    stepAlchemySimulation(abovePlan, exactly, [command(abovePlan, exactly, { kind: 'move', direction: 'down' })])
    expect(exactly.speedPercent).toBe(400)
    expect(exactly.terminalReason).toBeNull()

    const inclusivePlan = plan({
      ...abovePlan,
      acceleration: { ...abovePlan.acceleration, explosionBoundary: 'at-or-above' },
    })
    const inclusive = createAlchemySimulation(inclusivePlan, 4)
    replacePieces(inclusive, [{ id: 'top', definitionId: 'single', side: 'top', color: 'red', anchor: { x: 6, y: 5 }, rotation: 0, nextMoveTick: 999 }])
    stepAlchemySimulation(inclusivePlan, inclusive, [command(inclusivePlan, inclusive, { kind: 'move', direction: 'down' })])
    expect(inclusive.terminalReason).toBe('explosion')
  })

  it('runs complementary Disassembly in the pure engine while the bundled recipe remains honestly deferred', () => {
    const value = plan({
      recipe: {
        ...plan().recipe,
        mode: 'disassembly',
        initialCells: [{ x: 6, y: 6, color: 'gray' }, { x: 6, y: 5, color: 'red' }],
        targetCells: [{ x: 6, y: 6 }],
      },
      spawn: { minDelayTicks: 50, maxDelayTicks: 50, baseMoveIntervalTicks: 100, inwardSpeedMultiplier: 1 },
    })
    const state = createAlchemySimulation(value, 5)
    replacePieces(state, [{ id: 'top', definitionId: 'single', side: 'top', color: 'blue', anchor: { x: 6, y: 4 }, rotation: 0, nextMoveTick: 999 }])
    stepAlchemySimulation(value, state, [command(value, state, { kind: 'move', direction: 'down' })])
    expect(state.terminalReason).toBe('recipe-complete')
    expect(state.construction).toEqual([{ x: 6, y: 6, color: 'gray' }])
  })

  it('caps catch-up and replays identical commands independently of frame cadence', () => {
    expect(consumeAlchemyFrameTime(0, 10_000, 50, 4)).toEqual({ ticks: 4, accumulatorMs: 0, droppedMs: 9_800 })
    const value = plan()
    const first = replayAlchemy(value, 'cadence', [])
    const second = replayAlchemy(value, 'cadence', [])
    expect(second).toEqual(first)
  })

  it('terminates three seeds by three bounded QA policies using immutable bundled rules', () => {
    const config = structuredClone(rawConfig)
    expect(() => validateEmpiresConfig(config)).not.toThrow()
    const recipe = config.alchemy.recipes.find(candidate => !candidate.deferredReason)!
    const rulesIdentity = createAlchemyRulesIdentity(config.schemaVersion, config.alchemy, {
      buildings: config.empire.buildings,
      technologies: config.empire.technologies,
      epidemics: config.empire.epidemics,
    })
    for (const seed of ['alchemy:a', 'alchemy:b', 'alchemy:c']) {
      for (const policy of ALCHEMY_QA_POLICIES) {
        const value: AlchemyPlan = {
          id: `plan:${seed}:${policy}`,
          sessionId: `session:${seed}:${policy}`,
          rulesIdentity,
          originCityId: config.empire.cities[0].id,
          buildingId: config.alchemy.buildingId,
          recipe: structuredClone(recipe),
          tickMs: config.alchemy.tickMs,
          maxTicks: config.alchemy.maxTicks,
          maxCommands: config.alchemy.maxCommands,
          maxCatchUpTicksPerFrame: config.alchemy.maxCatchUpTicksPerFrame,
          board: structuredClone(config.alchemy.board),
          spawn: structuredClone(config.alchemy.spawn),
          acceleration: structuredClone(config.alchemy.acceleration),
          reagents: structuredClone(config.alchemy.reagents),
          explosion: structuredClone(config.alchemy.explosion),
          colors: structuredClone(config.alchemy.colors),
          pieces: structuredClone(config.alchemy.pieces),
        }
        const log = createAlchemyPolicyCommandLog(value, seed, policy)
        const result = replayAlchemy(value, seed, log)
        expect(result.completedTick).toBeLessThanOrEqual(value.maxTicks)
        expect(result.commandLog.length).toBeLessThanOrEqual(value.maxCommands)
        expect(result.terminalReason).not.toBe('invalid-command')
        if (policy === 'greedy') expect(result.outcome).toBe('success')
      }
    }
  })
})
