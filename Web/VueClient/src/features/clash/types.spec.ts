import { describe, expect, it } from 'vitest'
import {
  CLASH_FIELD_LIMITS,
  globalRowToLocalRow,
  localRowToGlobalRow,
  normalizeClashCatalog,
  normalizeClashGameState,
} from './types'
import {
  clashResolutionElapsedMs,
  clashSpeedDelayMs,
  clashUnitArtUrl,
  reconstructClashResolutionStartUnit,
} from './visuals'

describe('production Clash DTO boundary', () => {
  it('keeps a viewer-private empty enemy cell empty', () => {
    const state = normalizeClashGameState({
      gameId: 'privacy',
      revision: 4,
      phase: 'InitialFrontPlacement',
      width: 5,
      length: 5,
      boardCells: [
        { boardRow: 5, column: 0, territorySide: 'Guest', unit: null },
        {
          boardRow: 4,
          column: 0,
          territorySide: 'Host',
          unit: {
            instanceId: 'mine-1',
            definitionId: 'shield-bearer',
            ownerId: 'host',
            ownerSide: 'Host',
            boardRow: 4,
            column: 0,
            hp: 5,
            maxHp: 5,
            attack: 1,
            speed: 1,
            alive: true,
            deployed: true,
            diesToAoe: true,
          },
        },
      ],
    })

    expect(state.boardCells[0]?.unit).toBeNull()
    expect(state.boardCells[1]?.unit?.definitionId).toBe('shield-bearer')
    expect(state.boardCells[1]?.unit?.diesToAoe).toBe(true)
    expect(state.boardCells[0]).not.toHaveProperty('definitionId')
  })

  it('normalizes the AoE-death contract in both catalog and deployed DTOs', () => {
    const catalog = normalizeClashCatalog({
      units: [{
        id: 'dancer',
        name: 'Танцор',
        diesToAoe: true,
      }],
    })

    expect(catalog.units[0]?.diesToAoe).toBe(true)
  })

  it('clamps hostile field dimensions to the supported production envelope', () => {
    const state = normalizeClashGameState({
      gameId: 'bounds',
      width: 99,
      length: -4,
    })

    expect(state.width).toBe(CLASH_FIELD_LIMITS.maxWidth)
    expect(state.length).toBe(CLASH_FIELD_LIMITS.minLength)
  })

  it('maps local rows symmetrically onto the shared battlefield', () => {
    expect(localRowToGlobalRow(0, 5, true)).toBe(4)
    expect(localRowToGlobalRow(0, 5, false)).toBe(5)
    expect(localRowToGlobalRow(4, 5, true)).toBe(0)
    expect(localRowToGlobalRow(4, 5, false)).toBe(9)
    expect(globalRowToLocalRow(1, 5, true)).toBe(3)
    expect(globalRowToLocalRow(8, 5, false)).toBe(3)
  })

  it('uses the optimized strict live-unit WebP path', () => {
    expect(clashUnitArtUrl('Shield-Bearer')).toBe('/clash/art/units/shield-bearer.webp')
    expect(clashUnitArtUrl('../unsafe')).toBe('/clash/art/units/unsafe.webp')
  })

  it('keeps speed 9 instant and delays speed 1 by four seconds', () => {
    expect(clashSpeedDelayMs(9)).toBe(0)
    expect(clashSpeedDelayMs(1)).toBe(4000)
  })

  it('anchors resolution playback to the shared server timestamp', () => {
    expect(clashResolutionElapsedMs('2026-07-29T12:00:00.000Z', Date.parse('2026-07-29T12:00:01.750Z')))
      .toBe(1750)
    expect(clashResolutionElapsedMs('not-a-date', 1234)).toBe(0)
  })

  it('reconstructs damage, death, and advance before playing a resolution', () => {
    const finalUnit = {
      instanceId: 'revealed-1',
      definitionId: 'legionary',
      ownerId: 'guest',
      ownerSide: 'Guest',
      boardRow: 6,
      column: 2,
      hp: 0,
      maxHp: 5,
      attack: 2,
      speed: 4,
      shieldCharges: 0,
      dodgeCharges: 0,
      bleedStacks: 0,
      rangedReadyClash: 0,
      alive: false,
      deployed: true,
      isHidden: false,
      diesToAoe: false,
    }
    const restored = reconstructClashResolutionStartUnit(finalUnit, [
      {
        sequence: 0,
        type: 'Block',
        actorUnitInstanceId: 'enemy',
        targetUnitInstanceId: finalUnit.instanceId,
        speed: 5,
        startOffsetMs: 0,
        impactOffsetMs: 100,
        amount: 0,
        fromBoardRow: null,
        toBoardRow: null,
        column: 2,
        message: '',
      },
      {
        sequence: 0,
        type: 'Block',
        actorUnitInstanceId: 'enemy-2',
        targetUnitInstanceId: finalUnit.instanceId,
        speed: 5,
        startOffsetMs: 0,
        impactOffsetMs: 100,
        amount: 0,
        fromBoardRow: null,
        toBoardRow: null,
        column: 2,
        message: '',
      },
      {
        sequence: 1,
        type: 'Damage',
        actorUnitInstanceId: 'enemy',
        targetUnitInstanceId: finalUnit.instanceId,
        speed: 5,
        startOffsetMs: 0,
        impactOffsetMs: 250,
        amount: 3,
        fromBoardRow: null,
        toBoardRow: null,
        column: 2,
        message: '',
      },
      {
        sequence: 2,
        type: 'Death',
        actorUnitInstanceId: null,
        targetUnitInstanceId: finalUnit.instanceId,
        speed: 4,
        startOffsetMs: 250,
        impactOffsetMs: 300,
        amount: 0,
        fromBoardRow: null,
        toBoardRow: null,
        column: 2,
        message: '',
      },
      {
        sequence: 3,
        type: 'Advance',
        actorUnitInstanceId: finalUnit.instanceId,
        targetUnitInstanceId: null,
        speed: 4,
        startOffsetMs: 300,
        impactOffsetMs: 500,
        amount: 0,
        fromBoardRow: 5,
        toBoardRow: 6,
        column: 2,
        message: '',
      },
    ], {
      shieldCharges: 1,
      dodgeCharges: 0,
    })

    expect(restored).toMatchObject({
      hp: 3,
      alive: true,
      boardRow: 5,
      column: 2,
      shieldCharges: 1,
    })
  })
})
