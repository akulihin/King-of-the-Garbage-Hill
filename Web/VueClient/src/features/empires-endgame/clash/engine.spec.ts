import { describe, expect, it } from 'vitest'
import {
  applyClashCommand,
  createClashPlan,
  createClashRulesIdentity,
  createInitialClashState,
  resolveClash,
  validateClashConfig,
  validateClashPlan,
} from './engine'
import { CLASH_SCAFFOLD } from './catalog'
import type {
  ClashAbilityDefinition,
  ClashCommand,
  ClashPlan,
  ClashSide,
  ClashUnitDefinition,
  EmpiresClashConfig,
} from './types'

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function fighter(
  id: string,
  attack: number,
  maxHp: number,
  speed: number,
  extras: Partial<ClashUnitDefinition> = {},
): ClashUnitDefinition {
  return {
    id,
    name: id,
    faction: 'fixture',
    regions: ['common'],
    ranks: [],
    cost: null,
    acquisitionTags: [],
    attack,
    maxHp,
    speed,
    tags: [],
    passives: [],
    abilities: [],
    sourceMessageIds: ['fixture'],
    ...extras,
  }
}

function fixtureConfig(units: ClashUnitDefinition[], overrides: Partial<EmpiresClashConfig> = {}): EmpiresClashConfig {
  return {
    ...clone(CLASH_SCAFFOLD),
    enabled: true,
    roster: units,
    fieldVariants: [{
      id: 'fixture',
      name: 'fixture',
      columns: 1,
      rowsPerSide: 2,
      reinforcementRows: 1,
      unitCountMultiplier: 1,
      terrainCellIds: [],
    }],
    defaultFieldVariantId: 'fixture',
    deferredSubfeatures: [],
    ...overrides,
  }
}

function planFor(
  attacker: ClashUnitDefinition[],
  defender: ClashUnitDefinition[],
  overrides: Partial<EmpiresClashConfig> = {},
): ClashPlan {
  const config = fixtureConfig([...attacker, ...defender], overrides)
  const rulesIdentity = createClashRulesIdentity(18, config)
  return createClashPlan({
    id: 'fixture-plan',
    sessionId: 'fixture-session',
    rulesIdentity,
    config,
    field: config.fieldVariants[0],
    region: config.regions.find(region => region.id === 'common')!,
    roster: [
      ...attacker.map((unit, index) => ({
        instanceId: `a-${index}`,
        definitionId: unit.id,
        side: 'attacker' as const,
      })),
      ...defender.map((unit, index) => ({
        instanceId: `d-${index}`,
        definitionId: unit.id,
        side: 'defender' as const,
      })),
    ],
  })
}

function placementCommands(plan: ClashPlan): ClashCommand[] {
  const state = createInitialClashState(plan, 'fixture-seed')
  const queues = {
    attacker: plan.roster.filter(unit => unit.side === 'attacker'),
    defender: plan.roster.filter(unit => unit.side === 'defender'),
  }
  const commands: ClashCommand[] = []
  let turn = 0
  let side = state.expectedSide!
  const placed = { attacker: 0, defender: 0 }
  while (placed.attacker < 1 || placed.defender < 1) {
    const unit = queues[side][placed[side]]
    turn += 1
    commands.push({ turn, kind: 'place', side, unitInstanceId: unit.instanceId, row: 0, column: 0 })
    placed[side] += 1
    side = side === 'attacker' ? 'defender' : 'attacker'
  }
  return commands
}

function placeConfiguredFront(plan: ClashPlan, seed: string | number = 'fixture-seed') {
  let state = createInitialClashState(plan, seed)
  const queues = {
    attacker: plan.roster.filter(unit => unit.side === 'attacker'),
    defender: plan.roster.filter(unit => unit.side === 'defender'),
  }
  const placed = { attacker: 0, defender: 0 }
  while (state.phase === 'placement') {
    const side = state.expectedSide!
    const item = queues[side][placed[side]]
    state = applyClashCommand(plan, state, {
      turn: state.turn + 1,
      kind: 'place',
      side,
      unitInstanceId: item.instanceId,
      row: Math.min(placed[side], plan.field.rowsPerSide - 1),
      column: 0,
    })
    placed[side] += 1
  }
  return state
}

function closeBetweenClashWindows(plan: ClashPlan, initial: ReturnType<typeof createInitialClashState>) {
  let state = initial
  while (state.phase === 'between-clashes') {
    const side = state.expectedSide!
    state = applyClashCommand(plan, state, {
      turn: state.turn + 1,
      kind: 'end-between-clash',
      side,
    })
  }
  return state
}

function resolveToEnd(plan: ClashPlan, seed = 'fixture-seed'): ReturnType<typeof resolveClash> {
  let state = createInitialClashState(plan, seed)
  const queues = {
    attacker: plan.roster.filter(unit => unit.side === 'attacker'),
    defender: plan.roster.filter(unit => unit.side === 'defender'),
  }
  const placed = { attacker: 0, defender: 0 }
  while (state.phase === 'placement') {
    const side = state.expectedSide!
    const row = Math.min(placed[side], plan.field.rowsPerSide - 1)
    const item = queues[side][placed[side]]
    if (!item) throw new Error(`Fixture ${side} ran out of placement units`)
    state = applyClashCommand(plan, state, {
      turn: state.turn + 1,
      kind: 'place',
      side,
      unitInstanceId: item.instanceId,
      row,
      column: 0,
    })
    placed[side] += 1
  }
  while (state.phase !== 'finished') {
    if (state.phase === 'clash-ready') {
      state = applyClashCommand(plan, state, { turn: state.turn + 1, kind: 'resolve-clash' })
    } else {
      const side = state.expectedSide!
      const reserve = queues[side][placed[side]]
      if (reserve && !state.betweenClashes[side].placementUsed) {
        const empty = state.cells.find(cell => cell.side === side && cell.unitInstanceId === null)!
        state = applyClashCommand(plan, state, {
          turn: state.turn + 1,
          kind: 'place',
          side,
          unitInstanceId: reserve.instanceId,
          row: empty.row,
          column: empty.column,
        })
        placed[side] += 1
      }
      state = applyClashCommand(plan, state, {
        turn: state.turn + 1,
        kind: 'end-between-clash',
        side,
      })
    }
    if (state.error) throw new Error(state.error)
  }
  return resolveClash(plan, seed, state.commandLog)
}

describe('Clash pure engine', () => {
  it('keeps the disabled bundled catalog structurally valid', () => {
    expect(validateClashConfig(CLASH_SCAFFOLD)).toEqual([])
    expect(CLASH_SCAFFOLD.enabled).toBe(false)
    expect(CLASH_SCAFFOLD.roster.some(unit => unit.name === 'Палач')).toBe(true)
    expect(CLASH_SCAFFOLD.roster.find(unit => unit.name === 'Кобра')?.attack).toBeNull()
    const enabledErrors = validateClashConfig({ ...clone(CLASH_SCAFFOLD), enabled: true })
    expect(enabledErrors).toContain('enabled clash requires a live Clash assault route')
    expect(enabledErrors).toContain(
      'enabled clash requires an authored campaign roster mapping and launch contract',
    )
  })

  it('rejects malformed and over-budget imported plans at the restore boundary', () => {
    const plan = planFor([fighter('a', 1, 3, 1)], [fighter('d', 1, 3, 1)])
    expect(validateClashPlan(plan)).toEqual([])

    const overBudget = clone(plan)
    overBudget.maxCommands = 513
    expect(validateClashPlan(overBudget)).toContain('plan budget exceeds the shipped safety ceiling')

    const duplicateCatalogIds = clone(plan)
    duplicateCatalogIds.statuses.push(clone(duplicateCatalogIds.statuses[0]))
    duplicateCatalogIds.terrain.push(clone(duplicateCatalogIds.terrain[0]))
    expect(validateClashPlan(duplicateCatalogIds)).toEqual(expect.arrayContaining([
      expect.stringMatching(/duplicate or empty status/),
      expect.stringMatching(/duplicate or empty terrain/),
    ]))

    const invalidActor = clone(plan)
    invalidActor.roster[0].side = 'spectator' as ClashSide
    invalidActor.units[0].attack = Number.POSITIVE_INFINITY
    expect(validateClashPlan(invalidActor)).toEqual(expect.arrayContaining([
      expect.stringMatching(/invalid side/),
      expect.stringMatching(/invalid stats/),
    ]))
  })

  it.each([
    [[3, 2, 2], [1, 6, 1]],
    [[1, 6, 1], [4, 1, 3]],
    [[4, 1, 3], [3, 2, 2]],
  ])('preserves the golden rock-paper-scissors fixture %j > %j', (left, right) => {
    const plan = planFor(
      [fighter('left', left[0], left[1], left[2])],
      [fighter('right', right[0], right[1], right[2])],
    )
    expect(resolveToEnd(plan).winner).toBe('attacker')
  })

  it('preserves the two-unit golden formula with defender-first speed ties', () => {
    const plan = planFor(
      [fighter('left-front', 5, 1, 1), fighter('left-back', 3, 2, 2)],
      [fighter('right-front', 1, 4, 1), fighter('right-back', 1, 1, 3)],
    )
    expect(resolveToEnd(plan).winner).toBe('defender')
  })

  it.each([
    ['attacker-first', 'attacker'],
    ['defender-first', 'defender'],
  ] as const)('uses the configured %s speed-tie rule', (speedTieRule, winner) => {
    const plan = planFor(
      [fighter('attacker-tie', 2, 1, 2)],
      [fighter('defender-tie', 2, 1, 2)],
      { speedTieRule },
    )
    expect(resolveToEnd(plan).winner).toBe(winner)
  })

  it('applies high-ground speed before first-strike ordering', () => {
    const plain = planFor(
      [fighter('attacker-high', 2, 1, 2)],
      [fighter('defender-high', 2, 1, 2)],
    )
    const elevated = clone(plain)
    elevated.field.terrainCellIds = [{
      side: 'attacker', row: 0, column: 0, terrainId: 'high-ground',
    }]
    expect(resolveToEnd(plain).winner).toBe('defender')
    expect(resolveToEnd(elevated).winner).toBe('attacker')
  })

  it('lets an ordinary-arrow shield absorb ranged support without cancelling melee damage', () => {
    const archer = fighter('support-archer', 3, 2, 2, {
      passives: [{
        id: 'support-shot', name: 'Лучник', description: 'Стреляет из заднего ряда.',
        kind: 'ranged', category: 'weapon',
      }],
    })
    const shield = fighter('arrow-shield', 1, 6, 1, {
      passives: [{
        id: 'arrow-shield-passive', name: 'Щит', description: 'Поглощает обычные стрелы.',
        kind: 'shield', category: 'shield', charges: 0, targetTag: 'ordinary-arrows',
      }],
    })
    const plan = planFor([fighter('front', 1, 6, 3), archer], [shield])
    plan.field.reinforcementRows = 0
    const result = resolveToEnd(plan)
    expect(result.log.some(entry => entry.message.includes('поглотил обычную стрелу'))).toBe(true)
    expect(result.log.some(entry => entry.message === 'a-0 → d-0: 1.')).toBe(true)
  })

  it('reveals a hidden passive only after its first obvious proc', () => {
    const hidden = fighter('hidden', 1, 3, 3, {
      passives: [{
        id: 'hidden-bonus', name: 'Скрытый натиск', description: 'Получает +1 урон.',
        kind: 'damage-modifier', category: 'unit', hidden: true, value: 1,
      }],
    })
    const result = resolveToEnd(planFor([hidden], [fighter('target-hidden', 1, 3, 1)]))
    expect(result.revealedPassiveIds).toContain('hidden-bonus')
    expect(result.log.filter(entry => entry.message.includes('Раскрыта скрытая пассивка'))).toHaveLength(1)
  })

  it('applies bleeding on the following clash and creates a first-class corpse', () => {
    const bleeder = fighter('bleeder', 1, 4, 3, {
      passives: [{
        id: 'bleed',
        name: 'Кровотечение',
        description: 'Наносит кровотечение.',
        kind: 'status-on-hit',
        category: 'weapon',
        statusId: 'bleeding',
      }],
    })
    const plan = planFor([bleeder], [fighter('tank', 1, 3, 1)])
    const result = resolveToEnd(plan)
    expect(result.log.some(entry => entry.message.includes('кровотечение'))).toBe(true)
    expect(result.terminalReason).toBe('elimination')
  })

  it('uses side morale to grant exactly one extra activation charge', () => {
    const poke: ClashAbilityDefinition = {
      id: 'poke', name: 'Тычка', kind: 'damage', charges: 1, reloadTurns: 0,
      target: 'enemy', value: 1,
    }
    const plan = planFor(
      [fighter('caster', 1, 8, 2, { abilities: [poke] })],
      [fighter('target', 1, 8, 1)],
    )
    plan.initialMorale.attacker = 1
    let state = createInitialClashState(plan, 5)
    for (const command of placementCommands(plan)) state = applyClashCommand(plan, state, command)
    state = applyClashCommand(plan, state, { turn: state.turn + 1, kind: 'resolve-clash' })
    state = applyClashCommand(plan, state, {
      turn: state.turn + 1, kind: 'activate', side: 'attacker', unitInstanceId: 'a-0',
      abilityId: 'poke', targetUnitInstanceId: 'd-0',
    })
    state = applyClashCommand(plan, state, {
      turn: state.turn + 1, kind: 'activate', side: 'attacker', unitInstanceId: 'a-0',
      abilityId: 'poke', targetUnitInstanceId: 'd-0',
    })
    expect(state.error).toBeNull()
    const rejected = applyClashCommand(plan, state, {
      turn: state.turn + 1, kind: 'activate', side: 'attacker', unitInstanceId: 'a-0',
      abilityId: 'poke', targetUnitInstanceId: 'd-0',
    })
    expect(rejected.error).toContain('morale')
  })

  it('permits negative-morale activations only every second clash', () => {
    const poke: ClashAbilityDefinition = {
      id: 'slow-poke', name: 'Редкая тычка', kind: 'damage', charges: 2, reloadTurns: 0,
      target: 'enemy', value: 1,
    }
    const plan = planFor(
      [fighter('negative-caster', 0, 9, 2, { abilities: [poke] })],
      [fighter('negative-target', 0, 9, 1)],
    )
    plan.initialMorale.attacker = -1
    let state = placeConfiguredFront(plan)
    state = applyClashCommand(plan, state, { turn: state.turn + 1, kind: 'resolve-clash' })
    const firstClash = applyClashCommand(plan, state, {
      turn: state.turn + 1,
      kind: 'activate',
      side: 'attacker',
      unitInstanceId: 'a-0',
      abilityId: poke.id,
      targetUnitInstanceId: 'd-0',
    })
    expect(firstClash.error).toContain('morale')
    state = closeBetweenClashWindows(plan, state)
    state = applyClashCommand(plan, state, { turn: state.turn + 1, kind: 'resolve-clash' })
    state = applyClashCommand(plan, state, {
      turn: state.turn + 1,
      kind: 'activate',
      side: 'attacker',
      unitInstanceId: 'a-0',
      abilityId: poke.id,
      targetUnitInstanceId: 'd-0',
    })
    expect(state.error).toBeNull()
    expect(state.units['d-0'].hp).toBe(8)
  })

  it('measures ranged reloads in clashes instead of unrelated commands', () => {
    const crossbow = fighter('crossbow', 2, 9, 1, {
      passives: [{
        id: 'crossbow-shot', name: 'Лучник', description: 'Перезарядка два хода.',
        kind: 'ranged', category: 'weapon', reloadTurns: 2,
      }],
    })
    const plan = planFor(
      [fighter('front-zero', 0, 9, 1), crossbow],
      [fighter('target-nine', 0, 9, 1)],
    )
    plan.field.reinforcementRows = 0
    let state = placeConfiguredFront(plan)
    state = applyClashCommand(plan, state, { turn: state.turn + 1, kind: 'resolve-clash' })
    expect(state.units['d-0'].hp).toBe(7)
    for (const expectedHp of [7, 7, 5]) {
      state = closeBetweenClashWindows(plan, state)
      state = applyClashCommand(plan, state, { turn: state.turn + 1, kind: 'resolve-clash' })
      expect(state.units['d-0'].hp).toBe(expectedHp)
    }
  })

  it('keeps a one-clash status active through the following clash', () => {
    const stun: ClashAbilityDefinition = {
      id: 'fixture-stun', name: 'Оглушение', kind: 'status', charges: 1, reloadTurns: 0,
      target: 'enemy', statusId: 'stun', durationTurns: 1,
    }
    const plan = planFor(
      [fighter('stunner', 1, 8, 2, { abilities: [stun] })],
      [fighter('stunned', 1, 8, 1)],
    )
    plan.statuses.find(status => status.id === 'stun')!.deferredReason = undefined
    let state = placeConfiguredFront(plan)
    state = applyClashCommand(plan, state, { turn: state.turn + 1, kind: 'resolve-clash' })
    expect(state.units['a-0'].hp).toBe(7)
    state = applyClashCommand(plan, state, {
      turn: state.turn + 1,
      kind: 'activate',
      side: 'attacker',
      unitInstanceId: 'a-0',
      abilityId: stun.id,
      targetUnitInstanceId: 'd-0',
    })
    state = closeBetweenClashWindows(plan, state)
    state = applyClashCommand(plan, state, { turn: state.turn + 1, kind: 'resolve-clash' })
    expect(state.units['a-0'].hp).toBe(7)
    expect(state.units['d-0'].statuses.some(status => status.statusId === 'stun')).toBe(false)
    state = closeBetweenClashWindows(plan, state)
    state = applyClashCommand(plan, state, { turn: state.turn + 1, kind: 'resolve-clash' })
    expect(state.units['a-0'].hp).toBe(6)
  })

  it('consumes a one-use status-on-hit passive exactly once', () => {
    const dancer = fighter('one-use-bleed', 1, 9, 2, {
      passives: [{
        id: 'one-use-bleed-passive', name: 'Кровотечение', description: 'Один раз.',
        kind: 'status-on-hit', category: 'weapon', statusId: 'bleeding', charges: 1,
      }],
    })
    const plan = planFor([dancer], [fighter('bleed-target', 0, 9, 1)])
    let state = placeConfiguredFront(plan)
    state = applyClashCommand(plan, state, { turn: state.turn + 1, kind: 'resolve-clash' })
    expect(state.units['a-0'].passiveCharges['one-use-bleed-passive']).toBe(0)
    expect(state.units['d-0'].statuses.find(status => status.statusId === 'bleeding')?.stacks).toBe(1)
    state = closeBetweenClashWindows(plan, state)
    state = applyClashCommand(plan, state, { turn: state.turn + 1, kind: 'resolve-clash' })
    expect(state.units['d-0'].statuses.find(status => status.statusId === 'bleeding')?.stacks).toBe(1)
  })

  it('terminates immediately after the final permitted command', () => {
    const plan = planFor([fighter('cap-a', 1, 3, 1)], [fighter('cap-d', 1, 3, 1)], {
      maxCommands: 2,
    })
    let state = createInitialClashState(plan, 'command-cap')
    for (const command of placementCommands(plan)) state = applyClashCommand(plan, state, command)
    expect(state.commandLog).toHaveLength(2)
    expect(state.phase).toBe('finished')
    expect(state.terminalReason).toBe('turn-cap')
  })

  it('blocks reinforcement placement into a corpse-occupied cell when configured', () => {
    const plan = planFor(
      [fighter('corpse-maker', 9, 9, 9)],
      [fighter('front-corpse', 0, 1, 1), fighter('reserve-after-corpse', 1, 3, 1)],
      { corpseBlocksAdvance: true },
    )
    let state = placeConfiguredFront(plan)
    state = applyClashCommand(plan, state, { turn: state.turn + 1, kind: 'resolve-clash' })
    expect(state.cells.find(cell => cell.side === 'defender' && cell.row === 0)?.corpseIds).toHaveLength(1)
    state = applyClashCommand(plan, state, {
      turn: state.turn + 1,
      kind: 'end-between-clash',
      side: 'attacker',
    })
    const rejected = applyClashCommand(plan, state, {
      turn: state.turn + 1,
      kind: 'place',
      side: 'defender',
      unitInstanceId: 'd-1',
      row: 0,
      column: 0,
    })
    expect(rejected.error).toContain('unavailable')
  })

  it('rejects unknown command kinds instead of accepting a no-op journal entry', () => {
    const plan = planFor([fighter('a', 1, 3, 1)], [fighter('d', 1, 3, 1)])
    const initial = createInitialClashState(plan, 'unknown-command')
    const rejected = applyClashCommand(plan, initial, {
      turn: 1,
      kind: 'unknown-command',
    } as unknown as ClashCommand)

    expect(rejected.error).toContain('Unknown Clash command')
    expect(rejected.turn).toBe(0)
    expect(rejected.commandLog).toEqual([])
    expect(initial.error).toBeNull()
  })

  it('does not mutate the immutable plan or prior state and replay is deterministic', () => {
    const plan = planFor([fighter('a', 3, 2, 2)], [fighter('d', 1, 6, 1)])
    const planBefore = JSON.stringify(plan)
    const initial = createInitialClashState(plan, 'repeat')
    const initialBefore = JSON.stringify(initial)
    const placed = applyClashCommand(plan, initial, {
      turn: 1, kind: 'place', side: 'attacker', unitInstanceId: 'a-0', row: 0, column: 0,
    })
    expect(JSON.stringify(plan)).toBe(planBefore)
    expect(JSON.stringify(initial)).toBe(initialBefore)
    expect(placed.commandLog).toHaveLength(1)
    const left = resolveToEnd(plan, 'repeat')
    const right = resolveClash(plan, 'repeat', left.turnLog)
    expect(right).toEqual(left)
  })

  it('terminates at the configured logical turn cap without a wall clock', () => {
    const plan = planFor(
      [fighter('a', 0, 9, 0)],
      [fighter('d', 0, 9, 0)],
      { maxTurns: 4 },
    )
    const result = resolveToEnd(plan)
    expect(result.terminalReason).toBe('turn-cap')
    expect(result.turns).toBe(4)
  })
})
