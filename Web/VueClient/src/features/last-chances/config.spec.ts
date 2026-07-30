import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../../public/99lc/game-config.json'
import {
  clearLastChancesConfig,
  cloneLastChancesConfig,
  loadLastChancesConfig,
  migrateLastChancesConfig,
  saveLastChancesConfig,
  validateLastChancesConfig,
} from './config'
import { resolveLastChancesLoadout } from './equipment'
import { buildLastChancesPlan } from './plan'
import type {
  LastChancesConfig,
  LastChancesEquipMode,
  LastChancesWeaponDefinition,
} from './types'

const defaultConfig = defaultConfigJson as unknown as LastChancesConfig

interface ExpectedShippedControlRoute {
  mylorik: string[]
  dualsense: {
    instant: string
    start: string | null
    starts?: string[]
    preGate?: string
    nodes: string[]
  }
}

/** Binding control-scheme plan, kept independent from the shipped JSON it verifies. */
const EXPECTED_SHIPPED_CONTROL_ROUTES: Record<string, ExpectedShippedControlRoute> = {
  'twohand-spear:primary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|tap|*|100',
      'doubleTapHold|strike|press|continuation|80',
      'hold|technique|hold|*|100',
      'holdThenDoubleTap|mobility|press|continuation|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'hold-early',
      preGate: 'doubleTap',
      nodes: [
        'hold-early|hold|neutral|0.25|release|charge|dispatch|hold-middle|release|2300|ramp|*|early|plain|*|1|0|*',
        'hold-middle|hold|neutral|0.5|release|charge|dispatch|ram-short,spin-finisher|release|2300|ramp|*|middle|plain|*|2|0|soft',
        'ram-short|doubleTapHold|continuation|0.75|release|charge|dispatch|ram-strong,spin-finisher|release|2300|gate|*|ram-short|plain|*|0|0|soft',
        'ram-strong|doubleTapHold|continuation|0.95|release|charge|dispatch||release|2300|gate|*|ram-strong|plain|*|0|0|*',
        'spin-finisher|holdThenDoubleTap|continuation|0.95|press|none|cancel||release|2300|followUp|*|spin-middle|armed|*|0|0|*',
      ],
    },
  },
  'twohand-spear:secondary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|tap|*|100',
      'doubleTapHold|strike|press|continuation|80',
      'hold|technique|hold|*|100',
      'holdThenDoubleTap|mobility|press|stance|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'stance',
      preGate: 'doubleTap',
      nodes: [
        'stance|hold|neutral|0.25|press|channel|dispatch|kick-brace,vault-finisher|release|2300|tension|*|*|plain|*|0|0|soft',
        'kick-brace|doubleTapHold|continuation|0.5|release|charge|dispatch|kick-strong|release|2300|gate|*|brace|plain|*|1|0|*',
        'kick-strong|doubleTapHold|continuation|0.75|release|charge|dispatch|kick-final|release|2300|gate|*|kick|plain|*|2|0|*',
        'kick-final|doubleTapHold|continuation|0.95|release|charge|dispatch||release|2300|gate|*|kick|plain|*|0|0|*',
        'vault-finisher|holdThenDoubleTap|stance|0.95|press|none|cancel||release|2300|followUp|*|*|armed|*|0|0|*',
      ],
    },
  },
  'secondary-chain:primary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|tap|*|100',
      'doubleTapHold|mobility|press|spin|80',
      'hold|technique|hold|*|100',
      'holdThenDoubleTap|strike|press|tether|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'hook-near',
      nodes: [
        'hook-near|hold|neutral|0.25|press|channel|dispatch|spin,bind-finisher|release|2300|tension|*|hook-near|plain|*|0|0|soft',
        'spin|doubleTap|neutral|0.5|press|none|cancel|throw-wrap|release|2300|gate|*|*|plain|*|0|0|*',
        'throw-wrap|doubleTapHold|spin|0.75|release|charge|dispatch|throw-heave|release|2300|gate|*|wrap|plain|*|0|0|*',
        'throw-heave|doubleTapHold|spin|0.95|release|charge|dispatch||release|2300|gate|*|heave|plain|*|0|0|*',
        'bind-finisher|holdThenDoubleTap|tether|0.95|press|none|cancel||release|2300|gate|*|*|armed|*|0|0|*',
      ],
    },
  },
  'either-claws:primary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|tap|*|100',
      'doubleTapHold|strike|press|continuation|80',
      'hold|mobility|hold|*|100',
      'holdThenDoubleTap|strike|press|dash|90',
    ],
    dualsense: {
      instant: 'tap',
      start: 'rend',
      nodes: [
        'rend|doubleTap|neutral|0.25|release|none|dispatch|dash-short|release|2300|click|*|*|plain|*|1|0|*',
        'dash-short|hold|neutral|0.5|release|charge|dispatch|disarm,deep-strike-finisher|release|2300|ramp|*|claw-dash-short|plain|*|1|0|soft',
        'disarm|doubleTapHold|neutral|0.75|release|none|dispatch|dash-long,deep-strike-finisher|release|2300|gate|*|*|plain|*|1|0|soft',
        'dash-long|hold|neutral|0.95|release|charge|dispatch||release|2300|ramp|*|claw-dash-long|plain|*|1|0|*',
        'deep-strike-finisher|holdThenDoubleTap|dash|0.95|press|none|cancel||release|2300|followUp|*|*|armed|*|0|0|*',
      ],
    },
  },
  'secondary-spider-knife:primary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|tap|*|100',
      'doubleTapHold|mobility|release|continuation|80',
      'hold|technique|hold|*|100',
      'holdThenDoubleTap|strike|press|flurry|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'impale',
      nodes: [
        'impale|doubleTap|neutral|0.25|release|none|dispatch|flurry|release|2300|click|*|*|plain|*|0|0|*',
        'flurry|hold|neutral|0.5|press|channel|dispatch|twist|release|2300|tension|*|*|plain|*|0|0|*',
        'twist|holdThenDoubleTap|flurry|0.75|release|none|dispatch|throw-finisher|release|2300|gate|*|*|plain|*|0|3|soft',
        'throw-finisher|doubleTapHold|continuation|0.95|press|charge|cancel||release|2300|gate|*|spider-throw-ready|armed|*|0|0|*',
      ],
    },
  },
  'twohand-axe:primary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|tap|*|100',
      'doubleTapHold|technique|hold|grapple|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'grapple',
      nodes: [
        'grapple|doubleTap|neutral|0.25|press|none|cancel|throw-aim|release|2300|gate|*|*|plain|*|0|0|*',
        'throw-aim|doubleTapHold|grapple|0.5|release|charge|dispatch|throw-heave|release|2300|tension|*|axe-aim|plain|*|1|0|*',
        'throw-heave|doubleTapHold|grapple|0.75|release|charge|dispatch|throw-final,throw-max-finisher|release|2300|gate|*|axe-heave|plain|*|2|0|soft',
        'throw-final|doubleTapHold|grapple|0.95|release|charge|dispatch||release|2300|gate|*|axe-heave|plain|*|0|0|*',
        'throw-max-finisher|doubleTapHold|grapple|0.95|press|charge|cancel||release|2300|impact|*|axe-max|armed|*|0|0|*',
      ],
    },
  },
  'twohand-axe:secondary': {
    mylorik: [
      'tap|strike|press|*|100',
      'hold|technique|hold|*|100',
      'holdThenDoubleTap|mobility|press|spin|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'spin',
      nodes: [
        'spin|hold|neutral|0.25|press|channel|dispatch|leap-near,leap-max-finisher|release|2300|ramp|*|*|plain|*|0|0|soft',
        'leap-near|holdThenDoubleTap|spin|0.5|release|none|dispatch|leap-far|release|2300|followUp|*|axe-leap-near|plain|*|1|0|*',
        'leap-far|holdThenDoubleTap|spin|0.75|release|none|dispatch|leap-final|release|2300|followUp|*|axe-leap-far|plain|*|2|0|*',
        'leap-final|holdThenDoubleTap|spin|0.95|release|none|dispatch||release|2300|followUp|*|axe-leap-far|plain|*|0|0|*',
        'leap-max-finisher|holdThenDoubleTap|spin|0.95|press|none|cancel||release|2300|impact|*|axe-leap-max|armed|*|0|0|*',
      ],
    },
  },
  'twohand-katana:primary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|tap|*|100',
      'doubleTapHold|strike|press|continuation|80',
      'hold|technique|hold|*|100',
      'holdThenDoubleTap|mobility|press|continuation|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'overhead',
      nodes: [
        'overhead|doubleTap|neutral|0.25|release|none|dispatch|charge|release|2300|click|*|*|plain|*|0|0|*',
        'charge|hold|neutral|0.5|release|charge|dispatch|flurry,dance-finisher|release|2300|ramp|*|katana-charge|plain|*|1|2|soft',
        'flurry|doubleTapHold|continuation|0.75|release|none|dispatch|full-charge|release|2300|gate|*|*|plain|*|0|0|*',
        'full-charge|hold|neutral|0.95|release|charge|dispatch||release|2300|ramp|*|katana-full|plain|*|0|0|*',
        'dance-finisher|holdThenDoubleTap|continuation|0.95|press|none|cancel||release|2300|followUp|katana-charge|*|armed|*|0|0|*',
      ],
    },
  },
  'twohand-katana:secondary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|tap|*|100',
      'doubleTapHold|strike|press|continuation|80',
      'hold|technique|hold|*|100',
      'holdThenDoubleTap|mobility|press|continuation|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'hop',
      nodes: [
        'hop|doubleTap|neutral|0.25|release|none|dispatch|iaido|release|2300|click|*|*|plain|*|0|0|*',
        'iaido|hold|neutral|0.5|release|charge|dispatch|hop-slash,flash-finisher|release|2300|ramp|*|iaido-ready|plain|*|1|0|soft',
        'hop-slash|doubleTapHold|continuation|0.75|release|none|dispatch|iaido-full|release|2300|gate|*|*|plain|*|0|0|*',
        'iaido-full|hold|neutral|0.95|release|charge|dispatch||release|2300|ramp|*|iaido-full|plain|*|0|0|*',
        'flash-finisher|holdThenDoubleTap|continuation|0.95|press|none|cancel||release|2300|followUp|*|*|armed|*|0|0|*',
      ],
    },
  },
  'hybrid-sword:primary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|tap|*|80',
      'doubleTapHold|technique|hold|*|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'oberhau',
      preGate: undefined,
      nodes: [
        'oberhau|doubleTap|neutral|0.25|press|none|cancel|unterhau|release|1000|followUp|*|*|plain|*|0|0|*',
        'unterhau|doubleTapHold|continuation|0.75|release|charge|dispatch||release|1000|gate|*|*|plain|*|0|0|*',
      ],
    },
  },
  'hybrid-sword:secondary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|tap|*|80',
      'doubleTapHold|technique|hold|*|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'oberhau',
      preGate: undefined,
      nodes: [
        'oberhau|doubleTap|neutral|0.25|press|none|cancel|unterhau|release|1000|followUp|*|*|plain|*|0|0|*',
        'unterhau|doubleTapHold|continuation|0.75|release|charge|dispatch||release|1000|gate|*|*|plain|*|0|0|*',
      ],
    },
  },
  'secondary-ouroboros-fang:primary': {
    mylorik: [
      'tap|strike|press|*|100',
    ],
    dualsense: {
      instant: 'tap',
      start: 'fang-thrust',
      nodes: [
        'fang-thrust|tap|neutral|0.25|press|none|cancel||release|2300|click|*|*|plain|*|0|0|*',
      ],
    },
  },
  // Двуручное копьё v2 keeps the lance grammar, but R3 is the press-dispatched
  // Прорыв channel and its armed spin still combines the early-band requirement
  // with the new middle-pocket dwell gate.
  'twohand-spear-v2:primary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|tap|*|100',
      'doubleTapHold|strike|press|continuation|80',
      'hold|technique|hold|*|100',
      'holdThenDoubleTap|mobility|press|continuation|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'hold-early',
      preGate: 'doubleTap',
      nodes: [
        'hold-early|hold|neutral|0.25|release|charge|dispatch|hold-middle|release|2300|ramp|*|early|plain|*|1|0|*',
        'hold-middle|hold|neutral|0.5|release|charge|dispatch|breakthrough,spin-finisher|release|2300|ramp|*|middle|plain|*|2|0|soft',
        'breakthrough|doubleTapHold|continuation|0.75|press|channel|cancel|hold-late|release|2300|gate|*|*|plain|*|0|0|*',
        'hold-late|hold|neutral|0.95|release|charge|dispatch||release|2300|ramp|*|late|plain|*|0|0|*',
        'spin-finisher|holdThenDoubleTap|continuation|0.95|press|none|cancel||release|2300|followUp|early|spin-middle|armed|*|0|0|*',
      ],
    },
  },
  'twohand-spear-v2:secondary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|tap|*|100',
      'doubleTapHold|strike|press|continuation|80',
      'hold|technique|hold|*|100',
      'holdThenDoubleTap|mobility|press|stance|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'stance',
      preGate: 'doubleTap',
      nodes: [
        'stance|hold|neutral|0.25|press|channel|dispatch|kick-brace,vault-finisher|release|2300|tension|*|*|plain|*|0|0|soft',
        'kick-brace|doubleTapHold|continuation|0.5|release|charge|dispatch|kick-strong|release|2300|gate|*|shove|plain|*|1|0|*',
        'kick-strong|doubleTapHold|continuation|0.75|release|charge|dispatch|kick-final|release|2300|gate|*|kick|plain|*|2|0|*',
        'kick-final|doubleTapHold|continuation|0.95|release|charge|dispatch||release|2300|gate|*|strong-kick|plain|*|0|0|*',
        'vault-finisher|holdThenDoubleTap|stance|0.95|press|none|cancel||release|2300|followUp|*|*|armed|*|0|0|*',
      ],
    },
  },
  'twohand-bow:primary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|tap|*|100',
      'doubleTapHold|strike|press|continuation|80|press',
      'hold|technique|hold|*|100',
      'holdThenDoubleTap|mobility|press|continuation|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'double-shot',
      starts: ['double-shot', 'draw'],
      nodes: [
        'double-shot|doubleTap|neutral|0.25|release|none|dispatch|draw|release|2300|click|*|*|plain|*|0|0|*',
        'draw|hold|neutral|0.5|release|charge|dispatch|rapid-fire,scatter-finisher|release|2300|tension|*|*|plain|670|0|2|soft',
        'rapid-fire|doubleTapHold|continuation|0.75|press|channel|cancel||release|2300|ramp|*|*|plain|*|0|0|*',
        'scatter-finisher|holdThenDoubleTap|continuation|0.95|press|none|cancel||release|2300|followUp|draw-middle|*|armed|*|0|0|*',
      ],
    },
  },
  'twohand-bow:secondary': {
    mylorik: [
      'tap|strike|press|*|100',
      'doubleTap|technique|press|*|100',
      'doubleTapHold|strike|release|continuation|80|release',
      'hold|technique|hold|*|100',
      'holdThenDoubleTap|mobility|press|continuation|80',
    ],
    dualsense: {
      instant: 'tap',
      start: 'jump',
      starts: ['jump', 'rain'],
      nodes: [
        'jump|doubleTap|dash|0.25|press|none|cancel|riposte|release|2300|click|*|*|plain|*|0|0|*',
        'rain|hold|neutral|0.5|press|channel|cancel|ignite-finisher|release|2300|tension|*|*|plain|*|0|0|*',
        'riposte|doubleTapHold|dash|0.75|release|charge|dispatch||release|2300|gate|*|*|plain|470|2|0|*',
        'ignite-finisher|holdThenDoubleTap|continuation|0.95|press|none|cancel||release|2300|impact|*|*|plain|*|0|0|*',
      ],
    },
  },
}

function makeWeapon(
  id: string,
  equipMode: LastChancesEquipMode,
  sourceIndex = 0,
): LastChancesWeaponDefinition {
  const weapon = cloneLastChancesConfig(defaultConfig).weapons[sourceIndex] as LastChancesWeaponDefinition
  weapon.id = id
  weapon.name = id
  weapon.equipMode = equipMode
  delete weapon.hand
  delete weapon.secondaryAttacks
  if (weapon.controls) delete weapon.controls.secondary
  return weapon
}

function secondaryAttacks(prefix: string) {
  const attacks = cloneLastChancesConfig(defaultConfig).weapons[1].attacks
  for (const [gesture, attack] of Object.entries(attacks)) attack.name = `${prefix}:${gesture}`
  return attacks
}

function removeRoomInteractions(config: LastChancesConfig): void {
  config.rooms.forEach(room => { delete room.interaction })
}

function removeOuroborosSet(config: LastChancesConfig): void {
  delete config.ouroborosSet
}

function previousShippedSchemaV1Config(): LastChancesConfig {
  const config = cloneLastChancesConfig(defaultConfig)
  config.schemaVersion = 1
  delete config.input.tapComboWindowMs
  delete config.input.mylorik
  delete config.input.dualsense
  delete config.loadout
  removeRoomInteractions(config)
  const previousSpawns: Record<string, Array<{ x: number, y: number }>> = {
    'combat-hall': [
      { x: 700, y: 170 }, { x: 805, y: 335 }, { x: 690, y: 520 }, { x: 510, y: 120 },
      { x: 520, y: 560 }, { x: 370, y: 325 }, { x: 835, y: 95 }, { x: 850, y: 580 },
    ],
    'chest-gallery': [
      { x: 730, y: 160 }, { x: 790, y: 345 }, { x: 720, y: 550 }, { x: 540, y: 145 },
      { x: 555, y: 545 }, { x: 425, y: 345 }, { x: 820, y: 85 }, { x: 825, y: 620 },
    ],
    'rest-conservatory': [
      { x: 770, y: 170 }, { x: 855, y: 355 }, { x: 770, y: 555 }, { x: 580, y: 120 },
      { x: 590, y: 605 }, { x: 430, y: 360 }, { x: 875, y: 95 }, { x: 875, y: 625 },
    ],
    'wrong-shadow-event': [
      { x: 755, y: 145 }, { x: 840, y: 345 }, { x: 755, y: 560 }, { x: 560, y: 105 },
      { x: 570, y: 595 }, { x: 420, y: 345 }, { x: 875, y: 85 }, { x: 875, y: 620 },
    ],
    'curator-threshold': [
      { x: 820, y: 380 }, { x: 900, y: 200 }, { x: 900, y: 560 },
      { x: 690, y: 140 }, { x: 690, y: 620 }, { x: 530, y: 380 },
    ],
  }
  config.rooms.forEach((room) => {
    room.enemySpawns = previousSpawns[room.id] ?? room.spawnLayouts?.[0].enemySpawns
    delete room.spawnLayouts
  })
  config.enemies.forEach((enemy) => {
    delete enemy.idleTurnRadiansPerSecond
    delete enemy.preferredAttackRangeRatio
  })
  config.weapons = config.weapons.slice(0, 2)
  config.weapons.forEach((weapon, index) => {
    weapon.hand = index === 0 ? 'left' : 'right'
    delete weapon.equipMode
    delete weapon.trait
    delete weapon.controls
    delete weapon.tapCombo
    delete weapon.secondaryAttacks
    delete weapon.secondaryTapCombo
    Object.values(weapon.attacks).forEach((attack) => {
      delete attack.behavior
      delete attack.collider
    })
  })
  config.weapons[0].attacks.tap.cooldownMs = 280
  config.weapons[1].attacks.tap.cooldownMs = 210
  return config
}

function schema3Config(): LastChancesConfig {
  const config = cloneLastChancesConfig(defaultConfig)
  config.schemaVersion = 3
  config.weapons.forEach((weapon) => {
    weapon.trait ??= 'spearDistance'
    const attackCollections = [
      Object.values(weapon.attacks),
      ...(weapon.tapCombo ? [weapon.tapCombo] : []),
      ...(weapon.secondaryAttacks ? [Object.values(weapon.secondaryAttacks)] : []),
      ...(weapon.secondaryTapCombo ? [weapon.secondaryTapCombo] : []),
    ]
    attackCollections.flat().forEach((attack) => {
      attack.behavior ??= 'standard'
      attack.collider ??= { shape: 'sector', traceMs: 180 }
    })
  })
  return config
}

function stubCurrentConfigFetch() {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    status: 200,
    statusText: 'OK',
    json: async () => defaultConfig,
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('99LC config and deterministic plan', () => {
  beforeEach(() => clearLastChancesConfig())
  afterEach(() => { vi.unstubAllGlobals() })

  it('accepts the shipped builder config and its terminal boss tier', () => {
    const result = validateLastChancesConfig(defaultConfig)

    expect(result.errors).toEqual([])
    expect(defaultConfig.schemaVersion).toBe(11)
    expect(defaultConfig.player).toMatchObject({
      accelerationMs: 100,
      decelerationMs: 50,
    })
    expect(defaultConfig.combat).toMatchObject({
      attackStopsMovement: true,
      minimumPlayerParryMs: 180,
      enemyRevealOnParryMs: 1200,
      enemyRevealOnHitMs: 900,
      minimumPlayerDamageTaken: 1,
    })
    expect(defaultConfig.progression).toMatchObject({
      moveQuestKillsRequired: 2,
      sameTierSacrificeRatio: 0.5,
      staminaCostIncreasePerRoom: 0.1,
      maxStaminaCostStacks: 10,
      chanceErosionStep: 5,
    })
    expect(defaultConfig.chances).toBe(99)
    expect(defaultConfig.rooms.every(room => (room.spawnLayouts?.length ?? 0) >= 2)).toBe(true)
    expect(defaultConfig.progression.tiers).toHaveLength(8)
    expect(defaultConfig.progression.tiers.slice(0, 7).every(tier => tier.kind === 'normal')).toBe(true)
    expect(defaultConfig.progression.tiers[defaultConfig.progression.tiers.length - 1]?.kind).toBe('boss')
    expect(defaultConfig.progression.tiers.every(tier => tier.deathCost === 1)).toBe(true)
    const loadout = resolveLastChancesLoadout(defaultConfig)
    const shippedSpear = defaultConfig.weapons.find(weapon => weapon.id === 'twohand-spear')
    expect(shippedSpear?.equipMode).toBe('twoHanded')
    expect(shippedSpear?.trait).toBe('spearDistance')
    expect(shippedSpear?.secondaryAttacks).toBeDefined()
    expect(shippedSpear?.secondaryTapCombo?.length).toBeGreaterThanOrEqual(1)
    // The shipped loadout is Двуручное копьё v2. Being two-handed, it fills both inputs from
    // itself: the primary set is the Охота chain, the second input is its own secondary set.
    expect(loadout.left?.id).toBe('twohand-spear-v2')
    expect(loadout.left?.tapCombo).toHaveLength(3)
    expect(loadout.left?.tapCombo.map(attack => attack.behavior))
      .toEqual(['spearHunt', 'spearHunt', 'standard'])
    expect(loadout.right?.id).toBe('twohand-spear-v2')
    expect(loadout.right?.attacks.hold.behavior).toBe('spearStance')
    expect(loadout.left?.tapCombo.map(attack => attack.name))
      .toEqual(['Охота', 'Охота', 'Охота'])
    // An empty secondary slot changes nothing for a two-handed primary.
    const unsupplemented = cloneLastChancesConfig(defaultConfig)
    unsupplemented.loadout!.secondaryWeaponId = null
    expect(resolveLastChancesLoadout(unsupplemented)).toMatchObject({
      left: { id: 'twohand-spear-v2', hand: 'left', augment: 'none' },
      right: { id: 'twohand-spear-v2', hand: 'right', augment: 'none' },
    })
    const sword = defaultConfig.weapons.find(weapon => weapon.id === 'hybrid-sword')
    expect(sword?.name).toBe('Меч наемника')
    expect(sword?.primaryHandOnly).toBe(true)
    expect(sword?.staggerEnabled).toBe(true)
    expect(sword?.attacks.tap.description).not.toContain('Движения прицелом')
    expect(sword?.tuning).toMatchObject({
      rhythmMissesPerRoomBeforeFatigue: 3,
      rhythmConsecutiveMissesBeforeFatigue: 2,
      unterhauCooldownMultiplier: 3,
    })
    expect(sword?.attacks.doubleTap.collider?.width).toBe(48)
    const axe = defaultConfig.weapons.find(weapon => weapon.id === 'twohand-axe')
    expect(axe?.attacks.tap.description).toContain('Движения прицелом в сторону взмаха')
    expect(axe?.tuning).toMatchObject({
      mouseDamageBonusMax: 0.25,
      mouseMotionForMaxBonusPx: 160,
    })
    expect(axe?.secondaryAttacks?.hold.tuning).toMatchObject({
      assistMovementThreshold: 0.2,
      assistFacingTurnRadians: 0.012,
      assistMultiplier: 2,
      reflectedProjectileMinimumRange: 260,
      reflectedProjectilePierce: 40,
    })
    expect(axe?.secondaryAttacks?.holdThenDoubleTap.tuning).toMatchObject({
      chemicalTrailDamage: 0,
      chemicalTrailPierce: 20,
      chemicalTrailKnockback: 0,
    })
    const chain = defaultConfig.weapons.find(weapon => weapon.id === 'secondary-chain')
    expect(chain?.attacks.doubleTap.tuning).toMatchObject({
      assistMovementThreshold: 0.2,
      assistFacingTurnRadians: 0.012,
      assistMultiplier: 2,
    })
    const claws = defaultConfig.weapons.find(weapon => weapon.id === 'either-claws')
    expect(claws?.tuning).toMatchObject({
      bleedEveryHits: 2,
      parityBleedDurationMs: 5000,
      parityBleedStacks: 1,
      parityBleedTickDamage: 1.1,
      parityBleedTickMs: 500,
    })
    expect(claws?.attacks.hold.tuning).toMatchObject({
      chemicalTrailDamage: 0,
      chemicalTrailPierce: 20,
      chemicalTrailKnockback: 0,
    })
    const katana = defaultConfig.weapons.find(weapon => weapon.id === 'twohand-katana')
    expect(katana?.attacks.doubleTapHold.tuning).toMatchObject({
      dodgeThreshold: 0.25,
      dodgeEveryHits: 2,
    })
    const spearV2 = defaultConfig.weapons.find(weapon => weapon.id === 'twohand-spear-v2')
    expect(spearV2?.attacks.hold).toMatchObject({
      name: 'Замах',
      charge: {
        maxMs: 1100,
        bands: [
          { id: 'early', minMs: 325 },
          { id: 'middle', minMs: 563 },
          { id: 'late', minMs: 825 },
        ],
      },
    })
    expect(spearV2?.attacks.hold.recoveryMs).toBeUndefined()
    expect(spearV2?.attacks.doubleTapHold.color).toBe('#55c7ff')
    expect(spearV2?.secondaryAttacks?.tap).toMatchObject({
      name: 'Парирование',
      behavior: 'parry',
      range: 90,
      collider: { shape: 'capsule', width: 28 },
    })
    expect(spearV2?.secondaryAttacks?.tap.tuning).toMatchObject({
      reflectedProjectileMinimumRange: 220,
      reflectedProjectilePierce: 0,
      sidewaysForwardOffset: 36,
      sidewaysHalfWidth: 90,
    })
    expect(spearV2?.secondaryAttacks?.doubleTap).toMatchObject({
      name: 'Толчок',
      hitEffects: [{ status: 'stun', durationMs: 1000 }],
      tuning: { windupMs: 250, shoveTargetDistance: 176 },
    })
    expect(spearV2?.secondaryAttacks?.doubleTapHold).toMatchObject({
      name: 'Пинок',
      charge: {
        bands: [
          { id: 'shove', minMs: 550 },
          { id: 'kick', minMs: 850 },
          { id: 'strong-kick', minMs: 1200 },
        ],
      },
      tuning: {
        armorMultiplier: 2,
        shoveTargetDistance: 188,
        kickTargetDistance: 432,
        strongKickTargetDistance: 528,
        strongKickImmobilizeMs: 1500,
      },
    })
    expect(spearV2?.secondaryAttacks?.hold).toMatchObject({
      name: 'Строй',
      behavior: 'spearStance',
      tuning: {
        staminaPerTickMs: 100,
        staminaPerTick: 5,
        pierceReferenceSpeed: 420,
        cutReferenceSpeed: 900,
      },
    })
    expect(spearV2?.secondaryAttacks?.holdThenDoubleTap).toMatchObject({
      name: 'Олимпийский прыжок',
      behavior: 'poleVault',
      tuning: {
        runMs: 180,
        plantMs: 120,
        riseMs: 180,
        flightMs: 420,
        landMs: 150,
        trajectoryFlashMs: 180,
      },
    })
    expect(defaultConfig.enemies.find(enemy => enemy.id === 'spider-knife')).toMatchObject({
      maxHp: 48,
      armor: 2,
      tuning: {
        behaviorVersion: 2,
        reflectedDamageMultiplier: 4,
        quickCaptureWindowMs: 170,
        embeddedCaptureWindowMs: 2200,
        reflectionSelfDamageRatio: 0.1,
        impactSelfDamageRatio: 0.1,
      },
    })
    expect(defaultConfig.enemies.find(enemy => enemy.id === 'invisible-wolf')).toMatchObject({
      invisibleUntilAlerted: true,
      tuning: {
        behaviorVersion: 2,
        stalkRadius: 260,
        stalkPatienceMs: 900,
        rearDotMaximum: -0.15,
        frontDotAbort: 0.25,
        rehideDelayMs: 1400,
      },
    })
    expect(defaultConfig.progression.moveQuestsEnabled).toBe(true)
    expect(defaultConfig.artifacts).toHaveLength(5)
    expect(defaultConfig.outfits).toHaveLength(4)
    expect(defaultConfig.enemies.find(enemy => enemy.id === 'swarm-cockroach')?.swarm?.spawnIntervalMs)
      .toBe(200)
    expect(defaultConfig.enemies.find(enemy => enemy.id === 'swarm-cockroach')).toMatchObject({
      name: 'Таракан',
      role: 'cockroach',
      maxHp: 1,
      attackDamage: 1,
    })
    const turretRoom = defaultConfig.rooms.find(room => room.id === 'turret-crossfire')
    expect(turretRoom?.turretAlarmHoldMs).toBe(650)
    expect(turretRoom?.turrets).toHaveLength(4)
    expect(turretRoom?.turrets?.[0]).toMatchObject({
      projectileSpawnOffset: 18,
      projectileKnockback: 0,
    })
    const motherRoom = defaultConfig.rooms.find(room => room.id === 'cockroach-mother-lair')!
    expect(motherRoom).toMatchObject({
      width: 840,
      height: 840,
      encounter: { enemyIds: ['cockroach-mother'], infiniteSwarm: true },
      altar: { chanceCost: 5 },
    })
    expect(new Set(motherRoom.bossHoles?.map(hole => hole.shape)).size).toBe(4)
    expect(new Set(motherRoom.bossHoles?.map(hole => hole.color.toLowerCase())).size).toBe(4)
    for (const source of motherRoom.bossHoles ?? []) {
      const distances = (motherRoom.bossHoles ?? [])
        .filter(hole => hole.id !== source.id)
        .map(hole => Math.hypot(
          hole.position.x - source.position.x,
          hole.position.y - source.position.y,
        ))
        .sort((left, right) => left - right)
      expect(distances.slice(0, 2).every(distance => distance < 900)).toBe(true)
      expect(distances[2]).toBeGreaterThan(900 + defaultConfig.player.radius)
    }
    expect(defaultConfig.enemies.find(enemy => enemy.id === 'running-stapler')).toMatchObject({
      projectileKnockback: 0,
    })
    expect(defaultConfig.enemies.find(enemy => enemy.id === 'curator-shadow')
      ?.bossPhases?.find(phase => phase.attackKind === 'projectile')).toMatchObject({
        projectileKnockback: 0,
      })
    expect(defaultConfig.enemies.find(enemy => enemy.id === 'cockroach-mother')?.cockroachMother)
      .toMatchObject({
        entranceRadiusRatio: 0.55,
        exitRecoveryMs: 650,
        blastRadius: 900,
      })
    expect(defaultConfig.enemies.map(enemy => enemy.name)).toEqual(expect.arrayContaining([
      'Слуга',
      'Бегущий степлер',
      'Стражник',
      'Химера',
      'Нож-паук',
      'Невидимый волк',
      'Тень Куратора',
    ]))
    expect(defaultConfig.rooms.filter(room => room.interaction)).toHaveLength(5)
    expect(defaultConfig.rooms.flatMap(room => room.hazards ?? [])).toHaveLength(4)
    expect(defaultConfig.weapons.map(weapon => weapon.id)).toEqual([
      'twohand-spear',
      'secondary-chain',
      'either-claws',
      'secondary-spider-knife',
      'twohand-axe',
      'twohand-katana',
      'hybrid-sword',
      'secondary-ouroboros-fang',
      'twohand-spear-v2',
      'twohand-bow',
    ])
  })

  it('re-pairs the boss holes reciprocally and rerolls the pairing across generations', () => {
    const pairings = new Set<string>()
    for (let generation = 1; generation <= 24; generation += 1) {
      const plan = buildLastChancesPlan(defaultConfig, generation)
      for (const node of plan.nodes) {
        if (node.bossHoles.length === 0) continue
        expect(node.bossHoles).toHaveLength(4)
        const byId = new Map(node.bossHoles.map(hole => [hole.id, hole]))
        for (const hole of node.bossHoles) {
          const partner = byId.get(hole.linkedHoleId)
          expect(partner).toBeTruthy()
          expect(partner!.id).not.toBe(hole.id)
          expect(partner!.linkedHoleId).toBe(hole.id)
        }
        pairings.add([...node.bossHoles]
          .map(hole => [hole.id, hole.linkedHoleId].sort().join('~'))
          .sort()
          .join('|'))
      }
    }
    // Four holes admit exactly three perfect matchings; the roll must not be stuck on one.
    expect(pairings.size).toBeGreaterThan(1)
  })

  it('authors the persistent longbow catalog without changing the shipped spear loadout', () => {
    const bow = defaultConfig.weapons.find(weapon => weapon.id === 'twohand-bow')!
    const arrowBehaviors = [
      'bowShot',
      'bowDoubleShot',
      'bowRapidFire',
      'bowDraw',
      'bowScatter',
      'bowRiposte',
      'bowRain',
    ]

    expect(defaultConfig.loadout).toMatchObject({
      primaryWeaponId: 'twohand-spear-v2',
      secondaryWeaponId: null,
    })
    expect(bow).toMatchObject({
      name: 'Длинный лук',
      equipMode: 'twoHanded',
      chanceCost: 3,
      trait: 'longbowPersistence',
      defaultAugment: 'none',
    })
    expect(Object.values(bow.attacks).map(attack => attack.name)).toEqual([
      'Шот',
      'Шот-шот',
      'Чреда',
      'Натяг',
      'Множественный залп',
    ])
    expect(Object.values(bow.secondaryAttacks!).map(attack => attack.name)).toEqual([
      'Уворот',
      'Прыжок',
      'Ответ',
      'Обстрел',
      'Огонь!',
    ])
    expect(bow.attacks.tap).toMatchObject({
      behavior: 'bowShot',
      damage: 15,
      range: 520,
      projectileSpeed: 720,
      cooldownMs: 320,
      rootMs: 75,
      staminaCost: 3,
    })
    expect(bow.tapCombo?.[0]).toMatchObject({
      name: 'Шот',
      behavior: 'bowShot',
      cooldownMs: 320,
    })
    expect(bow.attacks.doubleTap).toMatchObject({
      behavior: 'bowDoubleShot',
      damage: 15,
      cooldownMs: 900,
      staminaCost: 4,
    })
    expect(bow.attacks.doubleTapHold).toMatchObject({
      behavior: 'bowRapidFire',
      tuning: {
        channelStartMs: 220,
        channelMaxMs: 2000,
        shotIntervalMs: 120,
        staminaTickMs: 100,
        staminaPerTick: 5,
      },
    })
    expect(bow.attacks.hold).toMatchObject({
      behavior: 'bowDraw',
      damage: 18,
      range: 420,
      projectileSpeed: 660,
      cooldownMs: 2600,
      staminaCost: 0,
      charge: {
        maxMs: 1000,
        bands: [
          { id: 'draw-early', minMs: 325 },
          { id: 'draw-middle', minMs: 650 },
          {
            id: 'draw-max',
            minMs: 1000,
            damageMultiplier: 2.2,
            rangeMultiplier: 1.75,
          },
        ],
      },
      tuning: {
        goldStartMs: 670,
        goldEndMs: 760,
        drawMaxHoldMs: 2000,
        staminaPerMs: 0.04,
      },
    })
    expect(bow.attacks.holdThenDoubleTap).toMatchObject({
      behavior: 'bowScatter',
      cooldownMs: 7600,
      staminaCost: 14,
      tuning: { scatterCount: 7, fanDegrees: 52, ricochets: 1 },
    })
    expect(bow.secondaryAttacks?.tap).toMatchObject({
      behavior: 'bowDodge',
      range: 110,
      durationMs: 180,
      cooldownMs: 500,
      invulnerabilityMs: 220,
    })
    expect(bow.secondaryTapCombo?.[0]).toMatchObject({
      name: 'Уворот',
      behavior: 'bowDodge',
      cooldownMs: 500,
    })
    expect(bow.secondaryAttacks?.doubleTap).toMatchObject({
      behavior: 'bowJump',
      range: 230,
      durationMs: 520,
      cooldownMs: 4200,
      invulnerabilityMs: 620,
    })
    expect(bow.secondaryAttacks?.doubleTapHold).toMatchObject({
      behavior: 'bowRiposte',
      cooldownMs: 6200,
      tuning: {
        goldStartMs: 470,
        goldEndMs: 530,
        lateShotMs: 530,
        goldDamageMultiplier: 1.22,
        goldRangeMultiplier: 1.12,
      },
    })
    expect(bow.secondaryAttacks?.hold).toMatchObject({
      behavior: 'bowRain',
      range: 520,
      tuning: {
        channelMaxMs: 2000,
        zoneRadius: 105,
        arrowImpactRadius: 10,
        arrowIntervalMs: 145,
        staminaTickMs: 100,
        staminaPerTick: 6,
      },
    })
    expect(bow.secondaryAttacks?.holdThenDoubleTap).toMatchObject({
      behavior: 'bowIgnite',
      cooldownMs: 6800,
      tuning: {
        ordinaryRadius: 54,
        ordinaryDamage: 18,
        chemicalRadius: 126,
        chemicalDamage: 48,
        chemicalSelfDamage: 34,
      },
    })
    expect(Object.keys(bow.augmentHooks ?? {}).sort()).toEqual([
      'bleed',
      'chemical',
      'fire',
      'poison',
    ])
    expect(Object.values(bow.augmentHooks ?? {})
      .every(hook => arrowBehaviors.every(behavior => hook.behaviors?.includes(behavior as never))))
      .toBe(true)
    expect(bow.controls?.primary.dualsense.nodes).toHaveLength(4)
    expect(bow.controls?.secondary?.dualsense.nodes).toHaveLength(4)
    expect(bow.controls?.primary.dualsense).toMatchObject({
      startNodeId: 'double-shot',
      startNodeIds: ['double-shot', 'draw'],
      nodes: expect.arrayContaining([
        expect.objectContaining({
          id: 'draw',
          armMs: 670,
          armClock: 'input',
        }),
      ]),
    })
    expect(bow.controls?.primary.mylorik.activations
      .find(activation => activation.gesture === 'doubleTapHold'))
      .toMatchObject({
        phase: 'press',
        context: 'continuation',
        continuationDispatch: 'press',
      })
    expect(bow.controls?.secondary?.mylorik.activations
      .find(activation => activation.gesture === 'doubleTapHold'))
      .toMatchObject({
        phase: 'release',
        context: 'continuation',
        continuationDispatch: 'release',
      })
    expect(bow.controls?.secondary?.dualsense).toMatchObject({
      startNodeId: 'jump',
      startNodeIds: ['jump', 'rain'],
      nodes: expect.arrayContaining([
        expect.objectContaining({
          id: 'jump',
          entryContext: 'dash',
          dispatch: 'press',
          next: ['riposte'],
        }),
        expect.objectContaining({
          id: 'rain',
          entryContext: 'neutral',
          dispatch: 'press',
          next: ['ignite-finisher'],
        }),
        expect.objectContaining({
          id: 'riposte',
          entryContext: 'dash',
          dispatch: 'release',
        }),
      ]),
    })
  })

  it('requires positive authored player acceleration and braking times', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.player.accelerationMs = 0
    invalid.player.decelerationMs = -1

    expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
      'player.accelerationMs must be a finite number > 0',
      'player.decelerationMs must be a finite number > 0',
    ]))
  })

  it('validates optional projectile, reflection, turret-alarm, and Cockroach Mother tuning', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    const roomIndex = invalid.rooms.findIndex(room => room.id === 'turret-crossfire')
    const turretRoom = invalid.rooms[roomIndex]!
    turretRoom.turretAlarmHoldMs = 0
    turretRoom.turrets![0]!.projectileSpawnOffset = -1
    turretRoom.turrets![0]!.projectileKnockback = -1

    const staplerIndex = invalid.enemies.findIndex(enemy => enemy.id === 'running-stapler')
    invalid.enemies[staplerIndex]!.projectileSpeed = 0
    invalid.enemies[staplerIndex]!.projectileKnockback = -1

    const curatorIndex = invalid.enemies.findIndex(enemy => enemy.id === 'curator-shadow')
    const projectilePhaseIndex = invalid.enemies[curatorIndex]!.bossPhases!
      .findIndex(phase => phase.attackKind === 'projectile')
    const projectilePhase = invalid.enemies[curatorIndex]!.bossPhases![projectilePhaseIndex]!
    projectilePhase.projectileSpeed = 0
    projectilePhase.projectileKnockback = -1

    const spear = invalid.weapons.find(weapon => weapon.id === 'twohand-spear')!
    spear.secondaryAttacks!.tap.tuning = {
      ...spear.secondaryAttacks!.tap.tuning,
      reflectedProjectileMinimumRange: -1,
      reflectedProjectilePierce: 1.5,
    }

    const motherIndex = invalid.enemies.findIndex(enemy => enemy.id === 'cockroach-mother')
    const mother = invalid.enemies[motherIndex]!.cockroachMother!
    mother.entranceRadiusRatio = 1.1
    mother.exitRecoveryMs = -1

    const wolfIndex = invalid.enemies.findIndex(enemy => enemy.id === 'invisible-wolf')
    invalid.enemies[wolfIndex]!.tuning = {
      ...invalid.enemies[wolfIndex]!.tuning,
      behaviorVersion: 3,
      frontDotAbort: 1.4,
      stalkRadius: 0,
    }

    expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
      `rooms[${roomIndex}].turretAlarmHoldMs must be a finite number > 0`,
      `rooms[${roomIndex}].turrets[0].projectileSpawnOffset must be a finite number >= 0`,
      `rooms[${roomIndex}].turrets[0].projectileKnockback must be a finite number >= 0`,
      `enemies[${staplerIndex}].projectileSpeed must be a finite number > 0`,
      `enemies[${staplerIndex}].projectileKnockback must be a finite number >= 0`,
      `enemies[${curatorIndex}].bossPhases[${projectilePhaseIndex}].projectileSpeed must be a finite number > 0`,
      `enemies[${curatorIndex}].bossPhases[${projectilePhaseIndex}].projectileKnockback must be a finite number >= 0`,
      'weapons[0].secondaryAttacks.tap.tuning.reflectedProjectileMinimumRange must be a finite number >= 0',
      'weapons[0].secondaryAttacks.tap.tuning.reflectedProjectilePierce must be an integer >= 0',
      `enemies[${motherIndex}].cockroachMother.entranceRadiusRatio must be <= 1`,
      `enemies[${motherIndex}].cockroachMother.exitRecoveryMs must be a finite number >= 0`,
      `enemies[${wolfIndex}].tuning.behaviorVersion must be 1 or 2`,
      `enemies[${wolfIndex}].tuning.frontDotAbort must be between -1 and 1`,
      `enemies[${wolfIndex}].tuning.stalkRadius must be > 0`,
    ]))
  })

  it('rebuilds identical rooms, enemies, and links for the same generation', () => {
    const first = buildLastChancesPlan(defaultConfig, 3)
    const retry = buildLastChancesPlan(defaultConfig, 3)
    const nextGeneration = buildLastChancesPlan(defaultConfig, 4)

    expect(retry).toEqual(first)
    expect(nextGeneration).not.toEqual(first)
    expect(first.tiers).toHaveLength(defaultConfig.progression.tiers.length)
    expect(first.tiers[first.tiers.length - 1]).toHaveLength(1)

    for (let tierIndex = 0; tierIndex < first.tiers.length - 1; tierIndex += 1) {
      const currentTier = first.tiers[tierIndex]
      const nextTier = first.tiers[tierIndex + 1]
      expect(currentTier.every(node => node.nextNodeIds.length <= defaultConfig.graph.choicesPerNode)).toBe(true)
      expect(currentTier.every(node => new Set(node.nextNodeIds).size === node.nextNodeIds.length)).toBe(true)
      expect(new Set(currentTier.flatMap(node => node.nextNodeIds))).toEqual(new Set(nextTier.map(node => node.id)))
    }
    for (const node of first.nodes) {
      const room = defaultConfig.rooms.find(candidate => candidate.id === node.roomTemplateId)
      const layout = room?.spawnLayouts?.find(candidate => candidate.id === node.spawnLayoutId)
      expect(layout).toBeTruthy()
      expect(new Set(node.enemies.map(enemy => `${enemy.position.x}:${enemy.position.y}`)).size)
        .toBe(node.enemies.length)
      for (const enemy of node.enemies) {
        expect(layout?.enemySpawns).toContainEqual(enemy.position)
      }
    }
  })

  it('starts every generation with the authored apartment and solo Knife-spider encounter', () => {
    for (let generation = 1; generation <= 12; generation += 1) {
      const plan = buildLastChancesPlan(defaultConfig, generation)
      const opening = plan.tiers[0]

      expect(opening).toHaveLength(1)
      expect(opening[0]).toMatchObject({
        tierId: 'opening',
        roomTemplateId: 'false-apartment',
      })
      expect(opening[0].enemies.map(enemy => enemy.definitionId)).toEqual(['spider-knife'])
      expect(new Set(opening[0].nextNodeIds))
        .toEqual(new Set(plan.tiers[1].map(node => node.id)))
      expect(plan.tiers[1].every(node => node.roomTemplateId !== 'false-apartment')).toBe(true)
    }
  })

  it('counts guaranteed enemies inside the authored room population target', () => {
    const config = cloneLastChancesConfig(defaultConfig)
    const tierIndex = config.progression.tiers.findIndex(tier => tier.id === 'tier-3')
    const tier = config.progression.tiers[tierIndex]
    tier.nodeCount = 1
    tier.enemyCount = [6, 6]
    tier.enemyPool = [{ enemyId: 'servant', weight: 1 }]
    tier.guaranteedEnemyIds = ['colossus']
    tier.guaranteedRoomTemplateIds = undefined
    tier.roomTemplateIds = ['combat-hall']

    const node = buildLastChancesPlan(config, 5).tiers[tierIndex][0]

    expect(node.enemies).toHaveLength(6)
    expect(node.enemies.filter(enemy => enemy.definitionId === 'colossus')).toHaveLength(1)
    expect(node.enemies.filter(enemy => enemy.definitionId === 'servant')).toHaveLength(5)
  })

  it('rejects a graph whose choice cap cannot cover the next tier', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.progression.tiers[1].nodeCount = 1
    invalid.graph.choicesPerNode = 2

    expect(validateLastChancesConfig(invalid).errors).toContain(
      'graph.choicesPerNode cannot connect every progression.tiers[2] node',
    )
  })

  it('selects more than the first authored spawn layout across generations', () => {
    const seenByRoom = new Map<string, Set<string>>()
    for (let generation = 1; generation <= 24; generation += 1) {
      for (const node of buildLastChancesPlan(defaultConfig, generation).nodes) {
        const seen = seenByRoom.get(node.roomTemplateId) ?? new Set<string>()
        seen.add(node.spawnLayoutId)
        seenByRoom.set(node.roomTemplateId, seen)
      }
    }

    expect(defaultConfig.rooms.some((room) => {
      const expected = new Set(room.spawnLayouts?.map(layout => layout.id))
      const seen = seenByRoom.get(room.id) ?? new Set<string>()
      return expected.size > 1 && [...expected].every(layoutId => seen.has(layoutId))
    })).toBe(true)
  })

  it('rejects authored player and enemy spawns that overlap room geometry', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    const roomIndex = invalid.rooms.findIndex(room => room.id === 'chest-gallery')
    invalid.rooms[roomIndex].spawnLayouts![0].enemySpawns[0] = { x: 929, y: 160 }
    invalid.rooms[roomIndex].playerSpawn = { x: 662, y: 351 }

    expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
      expect.stringContaining(`rooms[${roomIndex}].spawnLayouts[0].enemySpawns[0] overlaps room obstacle 1`),
      expect.stringContaining(`rooms[${roomIndex}].playerSpawn overlaps room obstacle 0`),
    ]))
  })

  it('rejects enemy layout points that overlap the player spawn circles', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.rooms[0].spawnLayouts![0].enemySpawns[0] = { ...invalid.rooms[0].playerSpawn }

    expect(validateLastChancesConfig(invalid).errors).toContain(
      'rooms[0].spawnLayouts[0].enemySpawns[0] overlaps playerSpawn with combined radius 38',
    )
  })

  it('rejects a named spawn layout that cannot hold its tier maximum', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    const roomIndex = invalid.rooms.findIndex(room => room.id === 'rest-conservatory')
    invalid.rooms[roomIndex].spawnLayouts![0].enemySpawns.length = 6

    expect(validateLastChancesConfig(invalid).errors).toContain(
      `rooms[${roomIndex}].spawnLayouts[0].enemySpawns needs at least 7 points for eligible tiers`,
    )
  })

  it('requires enough points when guarantees exceed the room population target', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    const tier = invalid.progression.tiers.find(candidate => candidate.id === 'tier-3')!
    tier.nodeCount = 1
    tier.enemyCount = [2, 2]
    tier.enemyPool = [{ enemyId: 'servant', weight: 1 }]
    tier.guaranteedEnemyIds = ['colossus', 'guard', 'running-stapler']
    tier.guaranteedRoomTemplateIds = undefined
    const isolatedRoom = JSON.parse(JSON.stringify(
      invalid.rooms.find(room => room.id === 'combat-hall'),
    )) as LastChancesConfig['rooms'][number]
    isolatedRoom.id = 'guarantee-capacity-test'
    isolatedRoom.name = 'Guarantee capacity test'
    invalid.rooms.push(isolatedRoom)
    tier.roomTemplateIds = [isolatedRoom.id]
    const roomIndex = invalid.rooms.length - 1
    invalid.rooms[roomIndex].spawnLayouts![0].enemySpawns.length = 2
    invalid.rooms[roomIndex].spawnLayouts![1].enemySpawns.length = 2

    expect(validateLastChancesConfig(invalid).errors).toContain(
      `rooms[${roomIndex}].spawnLayouts[0].enemySpawns needs at least 3 points for eligible tiers`,
    )
  })

  it('clones deeply and loads a validated browser builder override', async () => {
    const override = cloneLastChancesConfig(defaultConfig)
    override.seed = 'builder-override'
    override.progression.tiers[0].deathCost = 2
    saveLastChancesConfig(override)

    const loaded = await loadLastChancesConfig({ url: '/fetch-must-not-run.json' })

    expect(loaded.seed).toBe('builder-override')
    expect(loaded.progression.tiers[0].deathCost).toBe(2)
    loaded.progression.tiers[0].deathCost = 7
    expect(override.progression.tiers[0].deathCost).toBe(2)
  })

  it('rejects a progression without a terminal boss component', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.progression.tiers[invalid.progression.tiers.length - 1].kind = 'normal'

    expect(validateLastChancesConfig(invalid).errors).toContain(
      'progression.tiers must end with a boss tier',
    )
  })

  it('rejects more than ten accumulated stamina-cost icons', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.progression.maxStaminaCostStacks = 11

    expect(validateLastChancesConfig(invalid).errors).toContain(
      'progression.maxStaminaCostStacks must be <= 10',
    )
  })

  it('fetches runtime JSON without using the HTTP cache', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => defaultConfig,
    })
    vi.stubGlobal('fetch', fetchMock)

    await loadLastChancesConfig({ url: '/99lc/test-config.json', useBrowserOverride: false })

    expect(fetchMock).toHaveBeenCalledWith('/99lc/test-config.json', {
      cache: 'no-store',
      signal: undefined,
    })
  })

  it('keeps schema-v1 hand slots, enemySpawns, and single basic taps backward-compatible', () => {
    const legacy = cloneLastChancesConfig(defaultConfig)
    legacy.schemaVersion = 1
    delete legacy.loadout
    delete legacy.input.tapComboWindowMs
    for (const room of legacy.rooms) {
      room.enemySpawns = room.spawnLayouts![0].enemySpawns
      delete room.spawnLayouts
    }
    for (const enemy of legacy.enemies) {
      delete enemy.idleTurnRadiansPerSecond
      delete enemy.preferredAttackRangeRatio
    }
    removeRoomInteractions(legacy)
    legacy.weapons = legacy.weapons.slice(0, 2)
    legacy.weapons.forEach((weapon, index) => {
      weapon.hand = index === 0 ? 'left' : 'right'
      delete weapon.equipMode
      delete weapon.tapCombo
      delete weapon.secondaryAttacks
      delete weapon.secondaryTapCombo
    })

    expect(validateLastChancesConfig(legacy).errors).toEqual([])
    expect(resolveLastChancesLoadout(legacy)).toMatchObject({
      left: { id: legacy.weapons[0].id, hand: 'left' },
      right: { id: legacy.weapons[1].id, hand: 'right' },
    })
  })

  it('migrates the previously shipped schema-v1 browser override before strict validation', async () => {
    const previous = previousShippedSchemaV1Config()
    expect(validateLastChancesConfig(previous).valid).toBe(false)
    window.localStorage.setItem('99lc:game-config', JSON.stringify(previous))
    stubCurrentConfigFetch()

    const migrated = await loadLastChancesConfig({ url: '/99lc/schema-v4.json' })

    expect(migrated.schemaVersion).toBe(11)
    expect(migrated.weapons.filter(weapon => (
      weapon.id !== 'secondary-ouroboros-fang' && weapon.id !== 'twohand-bow'
    ))
      .every(weapon => weapon.attacks.tap.cooldownMs === 0)).toBe(true)
    expect(migrated.weapons.find(weapon => weapon.id === 'secondary-ouroboros-fang')
      ?.attacks.tap.cooldownMs).toBe(5000)
    expect(migrated.weapons.find(weapon => weapon.id === 'twohand-bow')
      ?.attacks.tap.cooldownMs).toBe(320)
    expect(migrated.rooms.find(room => room.id === 'combat-hall')?.enemySpawns).toContainEqual({ x: 1020, y: 337 })
    expect(migrated.rooms.find(room => room.id === 'wrong-shadow-event')?.enemySpawns).toContainEqual({ x: 990, y: 580 })
    expect(validateLastChancesConfig(migrated).errors).toEqual([])
  })

  it('repairs guaranteed-enemy clearance without applying an unsafe legacy target', async () => {
    const previous = previousShippedSchemaV1Config()
    const chestGallery = previous.rooms.find(room => room.id === 'chest-gallery')!
    Object.assign(chestGallery.obstacles[1], { x: 765, y: 130, width: 30, height: 60 })
    window.localStorage.setItem('99lc:game-config', JSON.stringify(previous))
    stubCurrentConfigFetch()

    const migrated = await loadLastChancesConfig({ url: '/99lc/schema-v4.json' })
    const spawns = migrated.rooms.find(room => room.id === 'chest-gallery')?.enemySpawns

    expect(spawns).toContainEqual({ x: 700, y: 160 })
    expect(spawns).not.toContainEqual({ x: 730, y: 160 })
    expect(spawns).not.toContainEqual({ x: 780, y: 160 })
    expect(spawns).toContainEqual({ x: 720, y: 550 })
    expect(spawns).toContainEqual({ x: 425, y: 345 })
    expect(validateLastChancesConfig(migrated).errors).toEqual([])
  })

  it('upgrades a saved schema-v2 override while preserving run tuning and adopting the schema-v4 arsenal', async () => {
    const legacy = cloneLastChancesConfig(defaultConfig)
    legacy.schemaVersion = 2
    legacy.weapons = legacy.weapons.filter(weapon => (
      weapon.id !== 'secondary-ouroboros-fang' && weapon.id !== 'twohand-bow'
    ))
    legacy.seed = 'saved-schema-v2-run'
    legacy.player.baseStats.attackPower = 137
    legacy.enemies[0].moveSpeed = 73
    const renamedWeapons = new Map<string, string>()
    legacy.weapons.forEach((weapon) => {
      const previousId = weapon.id
      weapon.id = `legacy-${previousId}`
      renamedWeapons.set(previousId, weapon.id)
      delete weapon.trait
    })
    legacy.loadout!.primaryWeaponId = renamedWeapons.get(legacy.loadout!.primaryWeaponId)!
    legacy.loadout!.secondaryWeaponId = legacy.loadout!.secondaryWeaponId
      ? renamedWeapons.get(legacy.loadout!.secondaryWeaponId) ?? null
      : null
    legacy.rooms.forEach((room) => {
      room.interaction?.choices.forEach((choice) => {
        if (choice.effect.primaryWeaponId) {
          choice.effect.primaryWeaponId = renamedWeapons.get(choice.effect.primaryWeaponId)
        }
        if (choice.effect.secondaryWeaponId) {
          choice.effect.secondaryWeaponId = renamedWeapons.get(choice.effect.secondaryWeaponId)
        }
      })
    })
    expect(validateLastChancesConfig(legacy).errors).toEqual([])
    window.localStorage.setItem('99lc:game-config', JSON.stringify(legacy))
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => defaultConfig,
    })
    vi.stubGlobal('fetch', fetchMock)

    const migrated = await loadLastChancesConfig({ url: '/99lc/schema-v3.json' })

    expect(fetchMock).toHaveBeenCalledWith('/99lc/schema-v3.json', {
      cache: 'no-store',
      signal: undefined,
    })
    expect(migrated).toMatchObject({
      schemaVersion: 11,
      seed: 'saved-schema-v2-run',
      player: { baseStats: { attackPower: 137 } },
      loadout: defaultConfig.loadout,
    })
    expect(migrated.enemies[0].moveSpeed).toBe(73)
    expect(migrated.weapons.map(weapon => weapon.id)).toEqual(
      defaultConfig.weapons.map(weapon => weapon.id),
    )
    expect(migrated.rooms.find(room => room.id === 'merchant-crossing')?.interaction?.choices)
      .toEqual(expect.arrayContaining([
        expect.objectContaining({
          id: 'buy-chain',
          effect: expect.objectContaining({ secondaryWeaponId: 'secondary-chain' }),
        }),
      ]))
    expect(validateLastChancesConfig(migrated).errors).toEqual([])
  })

  it('requires loadouts, named layouts, and authored tap sequences in schema v2', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.schemaVersion = 2
    delete invalid.loadout
    delete invalid.input.tapComboWindowMs
    invalid.rooms[0].enemySpawns = invalid.rooms[0].spawnLayouts![0].enemySpawns
    delete invalid.rooms[0].spawnLayouts
    delete invalid.weapons[0].tapCombo

    expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
      'loadout is required by schemaVersion 2',
      'input.tapComboWindowMs must be a finite number > 0',
      'rooms[0].spawnLayouts is required by schemaVersion 2 or newer',
      'weapons[0].tapCombo must contain at least one basic-combo follow-up',
    ]))
  })

  describe('schema-v11 longbow, schema-v10 attrition, schema-v9 run tuning, and legacy migration', () => {
    it('adds the longbow to schema v10 while preserving a custom catalog and loadout', () => {
      const v10 = cloneLastChancesConfig(defaultConfig)
      v10.schemaVersion = 10
      v10.weapons = v10.weapons.filter(weapon => weapon.id !== 'twohand-bow')
      const customWeapon = cloneLastChancesConfig(defaultConfig)
        .weapons.find(weapon => weapon.id === 'twohand-katana')!
      customWeapon.id = 'custom-kept'
      customWeapon.name = 'Custom kept weapon'
      v10.weapons.push(customWeapon)
      v10.loadout = {
        ...v10.loadout!,
        primaryWeaponId: customWeapon.id,
        secondaryWeaponId: null,
      }
      v10.weapons[0].attacks.doubleTap.damage = 137
      const before = JSON.stringify(v10)

      const migrated = migrateLastChancesConfig(v10, defaultConfig) as LastChancesConfig

      expect(migrated.schemaVersion).toBe(11)
      expect(migrated.loadout).toEqual(v10.loadout)
      expect(migrated.weapons.map(weapon => weapon.id)).toEqual([
        ...v10.weapons.map(weapon => weapon.id),
        'twohand-bow',
      ])
      expect(migrated.weapons.find(weapon => weapon.id === 'custom-kept'))
        .toEqual(customWeapon)
      expect(migrated.weapons[0].attacks.doubleTap.damage).toBe(137)
      expect(migrated.weapons.find(weapon => weapon.id === 'twohand-bow'))
        .toEqual(defaultConfig.weapons.find(weapon => weapon.id === 'twohand-bow'))
      expect(validateLastChancesConfig(migrated).errors).toEqual([])
      expect(JSON.stringify(v10)).toBe(before)
    })

    it('updates a retired twohand-bow record in place without changing its saved loadout', () => {
      const v10 = cloneLastChancesConfig(defaultConfig)
      v10.schemaVersion = 10
      const bowIndex = v10.weapons.findIndex(weapon => weapon.id === 'twohand-bow')
      v10.weapons[bowIndex].name = 'Лук памяти'
      v10.weapons[bowIndex].attacks.tap.damage = 1
      v10.loadout = {
        ...v10.loadout!,
        primaryWeaponId: 'twohand-bow',
        secondaryWeaponId: null,
      }
      const before = JSON.stringify(v10)

      const migrated = migrateLastChancesConfig(v10, defaultConfig) as LastChancesConfig

      expect(migrated.schemaVersion).toBe(11)
      expect(migrated.loadout).toEqual(v10.loadout)
      expect(migrated.weapons.filter(weapon => weapon.id === 'twohand-bow')).toHaveLength(1)
      expect(migrated.weapons[bowIndex])
        .toEqual(defaultConfig.weapons.find(weapon => weapon.id === 'twohand-bow'))
      expect(validateLastChancesConfig(migrated).errors).toEqual([])
      expect(JSON.stringify(v10)).toBe(before)
    })

    it('backfills current-attempt attrition and removes entrance-hole attacks from schema v9', () => {
      const v9 = cloneLastChancesConfig(defaultConfig) as LastChancesConfig & {
        progression: LastChancesConfig['progression'] & {
          staminaCostIncreasePerRoom?: number
          maxStaminaCostStacks?: number
          chanceErosionStep?: number
        }
      }
      v9.schemaVersion = 9
      delete v9.progression.staminaCostIncreasePerRoom
      delete v9.progression.maxStaminaCostStacks
      delete v9.progression.chanceErosionStep
      const mother = v9.enemies.find(enemy => enemy.id === 'cockroach-mother')!
        .cockroachMother as unknown as Record<string, unknown>
      mother.sameHoleChance = 1

      const migrated = migrateLastChancesConfig(v9, defaultConfig) as LastChancesConfig

      expect(migrated.schemaVersion).toBe(11)
      expect(migrated.progression).toMatchObject({
        staminaCostIncreasePerRoom: 0.1,
        maxStaminaCostStacks: 10,
        chanceErosionStep: 5,
      })
      expect(migrated.enemies.find(enemy => enemy.id === 'cockroach-mother')?.cockroachMother)
        .not.toHaveProperty('sameHoleChance')
      expect(validateLastChancesConfig(migrated).errors).toEqual([])
    })

    it('standalone-migrates an actual v1-shaped Builder definition through v2 and v3 fields', () => {
      const v1 = previousShippedSchemaV1Config()
      const before = JSON.stringify(v1)

      const migrated = migrateLastChancesConfig(v1) as LastChancesConfig

      expect(migrated).toMatchObject({
        schemaVersion: 11,
        input: {
          tapComboWindowMs: 900,
          mylorik: defaultConfig.input.mylorik,
          dualsense: defaultConfig.input.dualsense,
        },
      })
      expect(migrated.loadout).toEqual({
        primaryWeaponId: v1.weapons[0].id,
        secondaryWeaponId: v1.weapons[1].id,
      })
      expect(migrated.rooms.every(room => (room.spawnLayouts?.length ?? 0) === 2)).toBe(true)
      expect(migrated.enemies.every(enemy => (
        enemy.idleTurnRadiansPerSecond !== undefined
        && enemy.preferredAttackRangeRatio !== undefined
      ))).toBe(true)
      expect(migrated.weapons.every(weapon => (
        weapon.equipMode !== undefined
        && weapon.trait !== undefined
        && weapon.tapCombo?.length === 1
        && weapon.controls !== undefined
        && Object.values(weapon.attacks).every(attack => (
          attack.behavior !== undefined && attack.collider !== undefined
        ))
      ))).toBe(true)
      expect(migrated.weapons.map(weapon => weapon.trait)).toEqual([
        'spearDistance',
        'chainDotCarrier',
      ])
      expect(validateLastChancesConfig(migrated).errors).toEqual([])
      expect(JSON.stringify(v1)).toBe(before)
      expect(migrateLastChancesConfig(migrated)).toEqual(migrated)
    })

    it('clone-first migrates v1, v2, and v3 to v11 and keeps v11 idempotent', () => {
      const v1 = previousShippedSchemaV1Config()
      const v2 = cloneLastChancesConfig(defaultConfig)
      v2.schemaVersion = 2
      delete v2.input.mylorik
      delete v2.input.dualsense
      v2.weapons.forEach(weapon => delete weapon.controls)
      const v3 = cloneLastChancesConfig(defaultConfig)
      v3.schemaVersion = 3
      delete v3.input.mylorik
      delete v3.input.dualsense
      v3.weapons.forEach(weapon => delete weapon.controls)

      for (const legacy of [v1, v2, v3]) {
        const before = JSON.stringify(legacy)
        const migrated = migrateLastChancesConfig(legacy, defaultConfig) as LastChancesConfig
        expect(migrated.schemaVersion).toBe(11)
        expect(validateLastChancesConfig(migrated).errors).toEqual([])
        expect(JSON.stringify(legacy)).toBe(before)
        expect(migrated.input.mylorik).toEqual(defaultConfig.input.mylorik)
        expect(migrated.input.dualsense).toEqual(defaultConfig.input.dualsense)
        expect(migrated.weapons.map(weapon => weapon.controls)).toEqual(
          defaultConfig.weapons.map(weapon => weapon.controls),
        )
      }

      const before = JSON.stringify(defaultConfig)
      const migratedV4 = migrateLastChancesConfig(defaultConfig) as LastChancesConfig
      expect(migratedV4).toEqual(defaultConfig)
      expect(migratedV4).not.toBe(defaultConfig)
      expect(JSON.stringify(defaultConfig)).toBe(before)
    })

    it('backfills stamina into a saved schema-v6 override instead of rejecting it', () => {
      // Everything stamina-related is now required, so a v6 override saved by the previously
      // deployed build would otherwise fail validation and become unloadable.
      const v6 = cloneLastChancesConfig(defaultConfig) as LastChancesConfig & {
        stamina?: unknown
        renderer: Record<string, unknown>
      }
      v6.schemaVersion = 6
      v6.seed = 'v6-stamina-migration'
      delete v6.stamina
      delete (v6.player.baseStats as Record<string, unknown>).maxStamina
      v6.progression.tiers.forEach((tier) => {
        delete (tier.erosion as unknown as Record<string, unknown>).maxStamina
      })
      delete v6.renderer.stamina
      expect(validateLastChancesConfig(v6).valid).toBe(false)

      const before = JSON.stringify(v6)
      const migrated = migrateLastChancesConfig(v6, defaultConfig) as LastChancesConfig
      expect(validateLastChancesConfig(migrated).errors).toEqual([])
      expect(migrated.schemaVersion).toBe(11)
      expect(migrated.seed).toBe('v6-stamina-migration')
      expect(migrated.stamina).toEqual(defaultConfig.stamina)
      expect(migrated.player.baseStats.maxStamina).toBe(100)
      expect(migrated.progression.tiers.every(tier => tier.erosion.maxStamina === 0)).toBe(true)
      expect(migrated.renderer.stamina).toBe('#e0b64a')
      expect(JSON.stringify(v6)).toBe(before)
    })

    it('keeps stamina values a designer already authored in an override', () => {
      const v6 = cloneLastChancesConfig(defaultConfig)
      v6.schemaVersion = 6
      v6.stamina.attackCost = 9
      v6.player.baseStats.maxStamina = 250

      const migrated = migrateLastChancesConfig(v6, defaultConfig) as LastChancesConfig
      expect(migrated.stamina.attackCost).toBe(9)
      expect(migrated.player.baseStats.maxStamina).toBe(250)
    })

    it('backfills the authored movement ramps into a schema-v7 override', () => {
      const v7 = cloneLastChancesConfig(defaultConfig) as LastChancesConfig & {
        player: LastChancesConfig['player'] & {
          accelerationMs?: number
          decelerationMs?: number
        }
      }
      v7.schemaVersion = 7
      v7.seed = 'v7-movement-migration'
      delete v7.player.accelerationMs
      delete v7.player.decelerationMs

      const before = JSON.stringify(v7)
      const migrated = migrateLastChancesConfig(v7, defaultConfig) as LastChancesConfig

      expect(migrated.schemaVersion).toBe(11)
      expect(migrated.seed).toBe('v7-movement-migration')
      expect(migrated.player).toMatchObject({
        accelerationMs: 100,
        decelerationMs: 50,
      })
      expect(validateLastChancesConfig(migrated).errors).toEqual([])
      expect(JSON.stringify(v7)).toBe(before)
    })

    it('upgrades the real shipped-v7 topology to the fixed apartment opening', () => {
      const v7 = cloneLastChancesConfig(defaultConfig)
      v7.schemaVersion = 7
      v7.seed = 'real-v7-shape'
      v7.progression.roomHpRecovery = 17
      v7.progression.tiers = v7.progression.tiers.filter(tier => tier.id !== 'opening')
      v7.progression.tiers[0].roomTemplateIds.unshift('false-apartment')
      delete v7.rooms.find(room => room.id === 'false-apartment')!.encounter
      v7.narrative!.prologue.at(-1)!.text = 'На пороге сгибается украденный нож. Из рукояти вырастают ноги, и Нож-паук прыгает туда, где герой стоял мгновение назад.'
      delete (v7.player as Partial<LastChancesConfig['player']>).accelerationMs
      delete (v7.player as Partial<LastChancesConfig['player']>).decelerationMs
      delete (v7.input as Partial<LastChancesConfig['input']>).actionDirectionDeadZone
      delete (v7.progression as Partial<LastChancesConfig['progression']>).moveQuestKillsRequired
      delete (v7.progression as Partial<LastChancesConfig['progression']>).sameTierSacrificeRatio
      delete (v7 as unknown as { combat?: LastChancesConfig['combat'] }).combat

      const migrated = migrateLastChancesConfig(v7, defaultConfig) as LastChancesConfig

      expect(migrated.schemaVersion).toBe(11)
      expect(migrated.seed).toBe('real-v7-shape')
      expect(migrated.progression.roomHpRecovery).toBe(17)
      expect(migrated.progression.tiers[0]).toMatchObject({
        id: 'opening',
        nodeCount: 1,
        roomTemplateIds: ['false-apartment'],
        enemyPool: [{ enemyId: 'spider-knife', weight: 1 }],
      })
      expect(migrated.progression.tiers[1].roomTemplateIds).not.toContain('false-apartment')
      expect(migrated.rooms.find(room => room.id === 'false-apartment')?.encounter)
        .toEqual({ enemyIds: ['spider-knife'] })
      expect(migrated.narrative?.prologue.at(-1)?.text).toContain('готовясь к первому прыжку')
      expect(migrated.combat).toEqual(defaultConfig.combat)
      expect(validateLastChancesConfig(migrated).errors).toEqual([])
    })

    it('fails clearly for an unknown future schema', () => {
      const future = cloneLastChancesConfig(defaultConfig) as LastChancesConfig & { schemaVersion: number }
      future.schemaVersion = 12

      expect(() => migrateLastChancesConfig(future)).toThrow(
        'Unsupported 99LC schemaVersion: schemaVersion 12 is newer than supported 11',
      )
    })

    it('migrates a v3 browser override while preserving unrelated tuning', async () => {
      const legacy = cloneLastChancesConfig(defaultConfig)
      legacy.schemaVersion = 3
      legacy.seed = 'v3-control-migration'
      legacy.input.holdMs = 777
      legacy.input.mylorik!.techniqueHoldMs = 999
      legacy.player.baseStats.attackPower = 143
      legacy.enemies[0].moveSpeed = 71
      legacy.rooms[0].name = 'Saved room tuning'
      legacy.weapons[0].attacks.doubleTap.damage = 91
      legacy.weapons[0].controls!.primary.role = 'stale activation table'
      window.localStorage.setItem('99lc:game-config', JSON.stringify(legacy))
      const fetchMock = stubCurrentConfigFetch()

      const migrated = await loadLastChancesConfig({ url: '/99lc/schema-v4.json' })

      expect(fetchMock).toHaveBeenCalledOnce()
      expect(migrated).toMatchObject({
        schemaVersion: 11,
        seed: 'v3-control-migration',
        input: { holdMs: 777 },
        player: { baseStats: { attackPower: 143 } },
      })
      expect(migrated.input.mylorik).toEqual(defaultConfig.input.mylorik)
      expect(migrated.input.dualsense).toEqual(defaultConfig.input.dualsense)
      expect(migrated.enemies[0].moveSpeed).toBe(71)
      expect(migrated.rooms[0].name).toBe('Saved room tuning')
      expect(migrated.weapons[0].attacks.doubleTap.damage).toBe(91)
      expect(migrated.weapons[0].controls).toEqual(defaultConfig.weapons[0].controls)
      expect(JSON.parse(window.localStorage.getItem('99lc:game-config')!).schemaVersion).toBe(11)
    })

    it('validates bindings, hysteresis, ordered gates, and bounded feedback', () => {
      const invalid = cloneLastChancesConfig(defaultConfig)
      invalid.input.mylorik!.gamepad.interactButton = invalid.input.mylorik!.gamepad.mobilityButton
      invalid.input.mylorik!.keyboard.rightTechniqueKeys = ['KeyQ']
      invalid.input.dualsense!.releaseThreshold = 0.2
      invalid.input.dualsense!.hysteresis = 0.08
      invalid.input.dualsense!.gatePositions.medium = 0.2
      invalid.input.dualsense!.feedback.maxDurationMs = 2500
      invalid.input.dualsense!.feedback.profiles.click.magnitude = 1.1

      expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
        'input.mylorik.gamepad.interactButton duplicates input.mylorik.gamepad.mobilityButton',
        'input.mylorik.keyboard.rightTechniqueKeys duplicates key KeyQ from input.mylorik.keyboard.leftTechniqueKeys',
        'input.dualsense.activationThreshold - releaseThreshold must be >= hysteresis',
        'input.dualsense.gatePositions must be strictly increasing',
        'input.dualsense.feedback.maxDurationMs must be <= 2000',
        'input.dualsense.feedback.profiles.click.magnitude must be <= 1',
      ]))

      const unreachableNeutralGate = cloneLastChancesConfig(defaultConfig)
      unreachableNeutralGate.input.dualsense!.activationThreshold = 0.5
      unreachableNeutralGate.input.dualsense!.releaseThreshold = 0.1
      unreachableNeutralGate.input.dualsense!.hysteresis = 0.11
      expect(validateLastChancesConfig(unreachableNeutralGate).errors).toContain(
        'input.dualsense.releaseThreshold must be >= hysteresis so the neutral re-arm gate is reachable',
      )
    })

    it('re-seeds every shipped haptics personality and trigger ladder from a controls-less config', () => {
      const stripped = cloneLastChancesConfig(defaultConfig)
      stripped.schemaVersion = 2
      delete stripped.input.mylorik
      delete stripped.input.dualsense
      stripped.weapons.forEach(weapon => delete weapon.controls)

      const migrated = migrateLastChancesConfig(stripped) as LastChancesConfig
      migrated.weapons.forEach((weapon, index) => {
        expect(weapon.controls).toEqual(defaultConfig.weapons[index].controls)
      })
      const spider = migrated.weapons.find(weapon => weapon.id === 'secondary-spider-knife')
      expect(spider?.controls?.primary.dualsense.haptics?.wriggle).toMatchObject({
        calmIntervalMs: [2800, 4600],
        panicIntervalMs: [320, 720],
      })
      const spearControls = migrated.weapons[0].controls!.primary.dualsense
      expect(spearControls.preGateGesture).toBe('doubleTap')
      const forces = spearControls.nodes.map(node => node.adaptiveOverride?.force)
      expect(forces).toEqual([0.42, 0.58, 0.8, 1, 1])
    })

    it('accepts controls without any haptics block or node ticks', () => {
      const bare = cloneLastChancesConfig(defaultConfig)
      const dualsense = bare.weapons[0].controls!.primary.dualsense
      delete dualsense.haptics
      dualsense.nodes.forEach((node) => {
        delete node.entryTick
        delete node.adaptiveOverride
      })
      expect(validateLastChancesConfig(bare).errors).toEqual([])
    })

    it('validates haptics blocks, commit patterns, wriggle ranges, and entry ticks', () => {
      const invalid = cloneLastChancesConfig(defaultConfig)
      const spear = invalid.weapons[0].controls!.primary.dualsense
      spear.haptics!.gateTick!.durationMs = -5
      spear.haptics!.commitPattern = Array.from({ length: 9 }, () => (
        { delayMs: 0, durationMs: 40, magnitude: 0.5 }
      ))
      spear.nodes[1].entryTick = { durationMs: 30, magnitude: 1.4 }
      const chain = invalid.weapons[1].controls!.primary.dualsense
      chain.haptics!.commitPattern = [{ delayMs: 1990, durationMs: 40, magnitude: 0.5 }]
      const spider = invalid.weapons[3].controls!.primary.dualsense
      spider.haptics!.wriggle!.calmIntervalMs = [4600, 2800]
      spider.haptics!.wriggle!.pulsesPerBurst = [3, 1]

      expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
        'weapons[0].controls.primary.dualsense.haptics.gateTick.durationMs must be a finite number > 0',
        'weapons[0].controls.primary.dualsense.haptics.commitPattern must contain 1-8 pulses',
        'weapons[0].controls.primary.dualsense.nodes[1].entryTick.magnitude must be <= 1',
        'weapons[1].controls.primary.dualsense.haptics.commitPattern[0] must end within 2000ms of pattern start',
        'weapons[3].controls.primary.dualsense.haptics.wriggle.calmIntervalMs minimum must be <= maximum',
        'weapons[3].controls.primary.dualsense.haptics.wriggle.pulsesPerBurst must be an integer [min, max] pair with 1 <= min <= max',
      ]))
    })

    it('pins every authored request while covering all 68 enabled slots with no extras', () => {
      const sets = defaultConfig.weapons.flatMap((weapon) => [
        { key: `${weapon.id}:primary`, attacks: weapon.attacks, controls: weapon.controls!.primary },
        ...(weapon.secondaryAttacks
          ? [{
              key: `${weapon.id}:secondary`,
              attacks: weapon.secondaryAttacks,
              controls: weapon.controls!.secondary!,
            }]
          : []),
      ])
      expect(sets.map(set => set.key)).toEqual(Object.keys(EXPECTED_SHIPPED_CONTROL_ROUTES))

      const mylorikRequest = (
        activation: (typeof sets)[number]['controls']['mylorik']['activations'][number],
      ): string => [
        activation.gesture,
        activation.intent,
        activation.phase,
        activation.context ?? '*',
        activation.priority,
        ...(activation.continuationDispatch ? [activation.continuationDispatch] : []),
      ].join('|')
      const dualSenseRequest = (
        node: (typeof sets)[number]['controls']['dualsense']['nodes'][number],
      ): string => [
        node.id,
        node.gesture,
        node.entryContext,
        node.activationThreshold,
        node.dispatch,
        node.holdBehavior,
        node.releaseBehavior,
        node.next.join(','),
        node.cancel,
        node.expiryMs,
        node.tactileProfile,
        node.requiredChargeBandId ?? '*',
        node.chargeBandOverrideId ?? '*',
        node.entryRequiresArmed ? 'armed' : 'plain',
        node.armMs ?? '*',
        node.telegraph?.length ?? 0,
        node.armedCue?.length ?? 0,
        node.armedTriggerOverride ? 'soft' : '*',
      ].join('|')

      let enabledCount = 0
      let disabledCount = 0
      for (const { key, attacks, controls } of sets) {
        const authored = EXPECTED_SHIPPED_CONTROL_ROUTES[key]
        expect(controls.mylorik.activations.map(mylorikRequest), key).toEqual(authored.mylorik)
        expect({
          instant: controls.dualsense.instantGesture,
          start: controls.dualsense.startNodeId,
          ...(controls.dualsense.startNodeIds
            ? { starts: controls.dualsense.startNodeIds }
            : {}),
          preGate: controls.dualsense.preGateGesture,
          nodes: controls.dualsense.nodes.map(dualSenseRequest),
        }, key).toEqual(authored.dualsense)

        const enabled = Object.entries(attacks)
          .filter(([, attack]) => attack.enabled !== false && attack.behavior !== 'disabled')
          .map(([gesture]) => gesture)
        const disabled = Object.entries(attacks)
          .filter(([, attack]) => attack.enabled === false || attack.behavior === 'disabled')
          .map(([gesture]) => gesture)
        enabledCount += enabled.length
        disabledCount += disabled.length
        expect(controls.mylorik.activations.map(activation => activation.gesture).sort())
          .toEqual([...enabled].sort())
        expect([...new Set([
          controls.dualsense.instantGesture,
          ...(controls.dualsense.preGateGesture ? [controls.dualsense.preGateGesture] : []),
          ...controls.dualsense.nodes.map(node => node.gesture),
        ])].sort()).toEqual([...enabled].sort())
        expect(disabled).not.toContain(controls.dualsense.instantGesture)
        expect(controls.mylorik.activations.filter(activation => (
          activation.gesture === 'tap'
          && activation.intent === 'strike'
          && activation.phase === 'press'
          && activation.context === undefined
        ))).toHaveLength(1)

        const nodes = new Map(controls.dualsense.nodes.map(node => [node.id, node]))
        const visiting = new Set<string>()
        const visited = new Set<string>()
        const walk = (id: string): void => {
          expect(visiting.has(id)).toBe(false)
          if (visited.has(id)) return
          visiting.add(id)
          nodes.get(id)?.next.forEach(walk)
          visiting.delete(id)
          visited.add(id)
        }
        const roots = controls.dualsense.startNodeIds
          ?? (controls.dualsense.startNodeId ? [controls.dualsense.startNodeId] : [])
        roots.forEach(walk)
        expect(visited.size).toBe(nodes.size)
      }

      expect(sets).toHaveLength(16)
      expect(enabledCount).toBe(68)
      expect(disabledCount).toBe(12)

      const migrated = cloneLastChancesConfig(defaultConfig)
      migrated.schemaVersion = 3
      delete migrated.input.mylorik
      delete migrated.input.dualsense
      migrated.weapons.forEach(weapon => delete weapon.controls)
      const rebuilt = migrateLastChancesConfig(migrated) as LastChancesConfig
      expect(rebuilt.weapons.map(weapon => weapon.controls)).toEqual(
        defaultConfig.weapons.map(weapon => weapon.controls),
      )
    })

    it('rejects duplicate activations, graph cycles, unreachable nodes, and unmatched gates', () => {
      const duplicate = cloneLastChancesConfig(defaultConfig)
      const duplicateActivations = duplicate.weapons[0].controls!.primary.mylorik.activations
      Object.assign(duplicateActivations[1], {
        intent: duplicateActivations[0].intent,
        phase: duplicateActivations[0].phase,
        context: duplicateActivations[0].context,
        priority: duplicateActivations[0].priority,
      })
      expect(validateLastChancesConfig(duplicate).errors).toContain(
        'weapons[0].controls.primary.mylorik.activations[1] duplicates activation strike|press|*|100',
      )

      const cycle = cloneLastChancesConfig(defaultConfig)
      cycle.weapons[0].controls!.primary.dualsense.nodes.at(-1)!.next = ['hold-early']
      expect(validateLastChancesConfig(cycle).errors).toContain(
        'weapons[0].controls.primary.dualsense combo graph must be acyclic',
      )

      const unreachable = cloneLastChancesConfig(defaultConfig)
      unreachable.weapons[0].controls!.primary.dualsense.nodes[0].next = ['spin-finisher']
      expect(validateLastChancesConfig(unreachable).errors).toContain(
        'weapons[0].controls.primary.dualsense combo node hold-middle is unreachable',
      )

      const unmatchedGate = cloneLastChancesConfig(defaultConfig)
      unmatchedGate.weapons[0].controls!.primary.dualsense.nodes[0].activationThreshold = 0.33
      expect(validateLastChancesConfig(unmatchedGate).errors).toContain(
        'weapons[0].controls.primary.dualsense.nodes[0].activationThreshold must match an input.dualsense gate position',
      )

      const unsupportedLifecycle = cloneLastChancesConfig(defaultConfig)
      const unsupportedNode = unsupportedLifecycle.weapons[0].controls!.primary.dualsense.nodes[0]
      ;(unsupportedNode as unknown as { releaseBehavior: string }).releaseBehavior = 'maintain'
      ;(unsupportedNode as unknown as { cancel: string }).cancel = 'explicit'
      expect(validateLastChancesConfig(unsupportedLifecycle).errors).toEqual(expect.arrayContaining([
        'weapons[0].controls.primary.dualsense.nodes[0].releaseBehavior must be dispatch or cancel',
        'weapons[0].controls.primary.dualsense.nodes[0].cancel must be release or expiry',
      ]))

      const unknownBand = cloneLastChancesConfig(defaultConfig)
      unknownBand.weapons[0].controls!.primary.dualsense.nodes.at(-1)!.requiredChargeBandId = 'missing'
      expect(validateLastChancesConfig(unknownBand).errors).toContain(
        'weapons[0].controls.primary.dualsense.nodes[4].requiredChargeBandId must reference an existing hold charge band',
      )
    })

    it('validates explicit Mylorik continuation edges and multi-root DualSense graphs', () => {
      const invalidContinuation = cloneLastChancesConfig(defaultConfig)
      const bow = invalidContinuation.weapons.find(weapon => weapon.id === 'twohand-bow')!
      const rapid = bow.controls!.primary.mylorik.activations
        .find(activation => activation.gesture === 'doubleTapHold')!
      ;(rapid as unknown as { continuationDispatch: string }).continuationDispatch = 'later'
      expect(validateLastChancesConfig(invalidContinuation).errors).toEqual(expect.arrayContaining([
        expect.stringContaining('continuationDispatch must be press or release'),
      ]))

      const mismatchedContinuation = cloneLastChancesConfig(defaultConfig)
      const answer = mismatchedContinuation.weapons
        .find(weapon => weapon.id === 'twohand-bow')!
        .controls!.secondary!.mylorik.activations
        .find(activation => activation.gesture === 'doubleTapHold')!
      answer.phase = 'press'
      expect(validateLastChancesConfig(mismatchedContinuation).errors).toEqual(expect.arrayContaining([
        expect.stringContaining('phase must match continuationDispatch'),
      ]))

      const invalidRoots = cloneLastChancesConfig(defaultConfig)
      const roots = invalidRoots.weapons
        .find(weapon => weapon.id === 'twohand-bow')!
        .controls!.secondary!.dualsense
      roots.startNodeIds = ['rain', 'rain', 'missing']
      expect(validateLastChancesConfig(invalidRoots).errors).toEqual(expect.arrayContaining([
        expect.stringContaining('startNodeIds must not contain duplicates'),
        expect.stringContaining('startNodeIds must begin with startNodeId'),
        expect.stringContaining('startNodeIds references unknown node missing'),
      ]))
    })

    it('carries the authored primary and secondary controls onto resolved weapons', () => {
      const spearConfig = cloneLastChancesConfig(defaultConfig)
      spearConfig.loadout!.primaryWeaponId = 'twohand-spear'
      spearConfig.loadout!.secondaryWeaponId = null
      const spear = spearConfig.weapons.find(weapon => weapon.id === 'twohand-spear')!
      const resolvedSpear = resolveLastChancesLoadout(spearConfig)
      expect(resolvedSpear.left?.controls).toEqual(spear.controls!.primary)
      expect(resolvedSpear.right?.controls).toEqual(spear.controls!.secondary)
      expect(resolvedSpear.left?.controls).not.toBe(spear.controls!.primary)

      const paired = cloneLastChancesConfig(defaultConfig)
      paired.loadout!.primaryWeaponId = 'either-claws'
      paired.loadout!.secondaryWeaponId = 'secondary-chain'
      const chain = paired.weapons.find(weapon => weapon.id === 'secondary-chain')!
      expect(resolveLastChancesLoadout(paired).right?.controls).toEqual(chain.controls!.primary)
    })
  })

  it('rejects cooldowns on basic taps and authored combo steps', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.weapons[0].attacks.tap.cooldownMs = 1
    invalid.weapons[1].tapCombo![0].cooldownMs = 25

    expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
      'weapons[0].attacks.tap.cooldownMs must be 0 because basic taps have no cooldown',
      'weapons[1].tapCombo[0].cooldownMs must be 0 because basic taps have no cooldown',
    ]))
  })

  it('validates authored enemy idle turning and preferred attack distance', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.enemies[0].idleTurnRadiansPerSecond = -0.1
    invalid.enemies[1].preferredAttackRangeRatio = 1.01

    expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
      'enemies[0].idleTurnRadiansPerSecond must be a finite number >= 0',
      'enemies[1].preferredAttackRangeRatio must be <= 1',
    ]))
  })

  it('validates zone attacks, swarm blocks, and guaranteed tier enemies', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    const colossus = invalid.enemies.find(enemy => enemy.id === 'colossus')!
    const colossusIndex = invalid.enemies.indexOf(colossus)
    delete colossus.zone
    const cockroach = invalid.enemies.find(enemy => enemy.id === 'swarm-cockroach')!
    const cockroachIndex = invalid.enemies.indexOf(cockroach)
    cockroach.swarm = { total: 5, initialBurst: 9, spawnIntervalMs: 600 }
    invalid.progression.tiers[2].guaranteedEnemyIds = ['no-such-enemy']

    expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
      `enemies[${colossusIndex}].zone is required when attackKind is zone`,
      `enemies[${cockroachIndex}].swarm.initialBurst must be <= total`,
      'progression.tiers[2].guaranteedEnemyIds references unknown enemy no-such-enemy',
    ]))
  })

  it('uses guaranteed enemies for room geometry and boss-infrastructure validation', () => {
    const invalidGeometry = cloneLastChancesConfig(defaultConfig)
    invalidGeometry.enemies.find(enemy => enemy.id === 'colossus')!.radius = 140
    const shadowRoomIndex = invalidGeometry.rooms.findIndex(room => room.id === 'wrong-shadow-event')

    expect(validateLastChancesConfig(invalidGeometry).errors).toEqual(expect.arrayContaining([
      expect.stringContaining(
        `rooms[${shadowRoomIndex}].spawnLayouts[0].enemySpawns`,
      ),
      expect.stringContaining('must fit inside its room with radius 140'),
    ]))

    const invalidBossRoute = cloneLastChancesConfig(defaultConfig)
    invalidBossRoute.progression.tiers[1].guaranteedEnemyIds = ['cockroach-mother']
    const combatHallIndex = invalidBossRoute.rooms.findIndex(room => room.id === 'combat-hall')

    expect(validateLastChancesConfig(invalidBossRoute).errors).toEqual(expect.arrayContaining([
      `rooms[${combatHallIndex}].altar is required for every boss room`,
      `rooms[${combatHallIndex}].bossHoles is required for a Cockroach Mother encounter`,
    ]))
  })

  it('requires altars in every boss room and four holes for the Cockroach Mother', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    const motherRoom = invalid.rooms.find(room => room.id === 'cockroach-mother-lair')!
    const motherRoomIndex = invalid.rooms.indexOf(motherRoom)
    const curatorRoom = invalid.rooms.find(room => room.id === 'curator-threshold')!
    const curatorRoomIndex = invalid.rooms.indexOf(curatorRoom)
    delete motherRoom.altar
    delete motherRoom.bossHoles
    delete curatorRoom.altar

    expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
      `rooms[${motherRoomIndex}].altar is required for every boss room`,
      `rooms[${motherRoomIndex}].bossHoles is required for a Cockroach Mother encounter`,
      `rooms[${curatorRoomIndex}].altar is required for every boss room`,
    ]))
  })

  it('requires every Cockroach Mother hole to have a memorable color', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    const motherRoom = invalid.rooms.find(room => room.id === 'cockroach-mother-lair')!
    const motherRoomIndex = invalid.rooms.indexOf(motherRoom)
    motherRoom.bossHoles![1].color = motherRoom.bossHoles![0].color

    expect(validateLastChancesConfig(invalid).errors).toContain(
      `rooms[${motherRoomIndex}].bossHoles must use four different colors`,
    )
  })

  it('rejects unknown zone shapes', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    const colossus = invalid.enemies.find(enemy => enemy.id === 'colossus')!
    const colossusIndex = invalid.enemies.indexOf(colossus)
    colossus.zone!.shapes = ['circle', 'hexagon' as never]

    expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
      `enemies[${colossusIndex}].zone.shapes must be a non-empty array of circle, square, triangle`,
    ]))
  })

  describe('schema-v3 combat actions', () => {
    it('accepts typed colliders, charge bands, statuses, resources, and augments', () => {
      const config = schema3Config()
      const weapon = config.weapons[0]
      config.loadout!.primaryWeaponId = weapon.id
      config.loadout!.secondaryWeaponId = null
      const attack = weapon.attacks.hold
      weapon.resource = { kind: 'chain', max: 1, initial: 0, label: 'Stored DOT', color: '#86d981' }
      weapon.defaultAugment = 'poison'
      weapon.augmentHooks = {
        poison: {
          behaviors: ['spearRelease'],
          damageMultiplier: 1.2,
          hitEffects: [{
            status: 'poison',
            durationMs: 4200,
            stacks: 2,
            chance: 0.75,
            tickDamage: 3,
            tickMs: 700,
            refresh: 'stack',
          }],
        },
      }
      attack.collider = {
        shape: 'sweep',
        innerRange: 35,
        width: 22,
        traceMs: 360,
        passesThroughWalls: true,
        followsPlayer: true,
        tickMs: 120,
        rotationDegrees: -110,
      }
      attack.charge = {
        maxMs: 1800,
        bands: [
          { id: 'early', label: 'Early', minMs: 0, color: '#9bdc7b', damageMultiplier: 0.9 },
          {
            id: 'late',
            label: 'Late',
            minMs: 900,
            color: '#ff9e57',
            rangeMultiplier: 1.5,
            overrides: { pierce: 4, recoveryMs: 250 },
          },
        ],
      }
      attack.hitEffects = [{ status: 'microstun', durationMs: 160, refresh: 'refresh' }]
      attack.repeatHits = 3
      attack.repeatIntervalMs = 140
      attack.sweetSpot = {
        minRange: 90,
        maxRange: 170,
        damageMultiplier: 1.6,
        knockbackMultiplier: 1.4,
        criticalMultiplier: 2,
      }
      config.loadout!.primaryAugment = 'poison'

      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config).left).toMatchObject({
        trait: weapon.trait,
        resource: weapon.resource,
        augment: 'poison',
      })
    })

    it('accepts an explicitly disabled un-authored gesture', () => {
      const config = schema3Config()
      const attack = config.weapons[0].attacks.holdThenDoubleTap
      attack.enabled = false
      attack.behavior = 'disabled'
      delete attack.collider

      expect(validateLastChancesConfig(config).errors).toEqual([])
    })

    it('rejects malformed disabled actions, colliders, charge bands, and statuses', () => {
      const config = schema3Config()
      const attack = config.weapons[0].attacks.hold
      attack.enabled = false
      attack.behavior = 'standard'
      attack.collider = {
        shape: 'capsule',
        width: 0,
        traceMs: -1,
        tickMs: 0,
      }
      ;(attack.collider as unknown as { passesThroughWalls: string }).passesThroughWalls = 'yes'
      attack.charge = {
        maxMs: 800,
        bands: [
          { id: 'late', label: 'Late', minMs: 600, color: '#fff' },
          { id: 'early', label: 'Early', minMs: 300, color: '#fff' },
          { id: 'too-late', label: 'Too late', minMs: 900, color: '#fff' },
        ],
      }
      attack.hitEffects = [{
        status: 'bleed',
        durationMs: 0,
        chance: 1.1,
        tickMs: 0,
      }]
      attack.repeatHits = 2
      delete attack.repeatIntervalMs

      expect(validateLastChancesConfig(config).errors).toEqual(expect.arrayContaining([
        'weapons[0].attacks.hold.behavior must be disabled when enabled is false',
        'weapons[0].attacks.hold.collider.traceMs must be a finite number >= 0',
        'weapons[0].attacks.hold.collider.width must be a finite number > 0',
        'weapons[0].attacks.hold.collider.tickMs must be a finite number > 0',
        'weapons[0].attacks.hold.collider.passesThroughWalls must be a boolean',
        'weapons[0].attacks.hold.charge.bands[1].minMs must be strictly greater than the previous charge band',
        'weapons[0].attacks.hold.charge.bands[2].minMs must be <= weapons[0].attacks.hold.charge.maxMs',
        'weapons[0].attacks.hold.hitEffects[0].durationMs must be a finite number > 0',
        'weapons[0].attacks.hold.hitEffects[0].chance must be <= 1',
        'weapons[0].attacks.hold.hitEffects[0].tickMs must be a finite number > 0',
        'weapons[0].attacks.hold.repeatIntervalMs is required when repeatHits is greater than 1',
      ]))
    })

    it('rejects malformed weapon resources, traits, and augment hooks', () => {
      const config = schema3Config()
      const weapon = config.weapons[0]
      ;(weapon as unknown as { trait: string }).trait = 'unknownTrait'
      weapon.resource = { kind: 'durability', max: 10, initial: 11 }
      ;(config.loadout as unknown as { primaryAugment: string }).primaryAugment = 'ice'
      weapon.augmentHooks = {
        poison: {
          behaviors: ['not-a-behavior' as never],
          damageMultiplier: -0.1,
          hitEffects: [{ status: 'poison', durationMs: 0 }],
        },
      }

      expect(validateLastChancesConfig(config).errors).toEqual(expect.arrayContaining([
        `weapons[0].trait must be one of spearDistance, chainDotCarrier, clawParity, spiderDurability, axeHookRecovery, katanaFlow, swordRhythm, ouroborosFang, longbowPersistence`,
        'weapons[0].resource.initial must be <= max',
        'weapons[0].augmentHooks.poison.behaviors[0] uses unknown behavior not-a-behavior',
        'weapons[0].augmentHooks.poison.damageMultiplier must be a finite number >= 0',
        'weapons[0].augmentHooks.poison.hitEffects[0].durationMs must be a finite number > 0',
        'loadout.primaryAugment must be one of none, bleed, poison, fire, chemical, ouroborosAcid',
      ]))
    })
  })

  describe('catalog equipment modes', () => {
    it('resolves a two-handed weapon into ten attacks and rejects a supplemental weapon', () => {
      const config = cloneLastChancesConfig(defaultConfig)
      removeOuroborosSet(config)
      const weapon = makeWeapon('greatblade', 'twoHanded')
      weapon.secondaryAttacks = secondaryAttacks('greatblade-secondary')
      weapon.controls!.secondary = cloneLastChancesConfig(defaultConfig).weapons[0].controls!.secondary
      weapon.controls.secondary.dualsense.nodes.forEach((node) => {
        delete node.chargeBandOverrideId
      })
      weapon.augmentHooks = { fire: { damageMultiplier: 1.1 } }
      const supplemental = makeWeapon('sidearm', 'secondaryOnly', 1)
      removeRoomInteractions(config)
      config.weapons = [weapon, supplemental]
      config.loadout = {
        primaryWeaponId: weapon.id,
        secondaryWeaponId: null,
        primaryAugment: 'fire',
        secondaryAugment: 'fire',
      }

      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config)).toMatchObject({
        left: {
          id: weapon.id,
          hand: 'left',
          augment: 'fire',
          attacks: { tap: { name: weapon.attacks.tap.name } },
        },
        right: {
          id: weapon.id,
          hand: 'right',
          augment: 'fire',
          attacks: { tap: { name: 'greatblade-secondary:tap' } },
        },
      })

      config.loadout.secondaryWeaponId = supplemental.id
      expect(validateLastChancesConfig(config).errors).toContain(
        'loadout.secondaryWeaponId must be null while a twoHanded weapon is equipped',
      )
    })

    it('allows an either-hand weapon in one slot or duplicated into both slots', () => {
      const config = cloneLastChancesConfig(defaultConfig)
      removeOuroborosSet(config)
      const weapon = makeWeapon('shortsword', 'eitherHand')
      removeRoomInteractions(config)
      config.weapons = [weapon]
      config.loadout = { primaryWeaponId: weapon.id, secondaryWeaponId: weapon.id }

      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config)).toMatchObject({
        left: { id: weapon.id, hand: 'left' },
        right: { id: weapon.id, hand: 'right' },
      })

      config.loadout.secondaryWeaponId = null
      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config).right).toBeNull()
    })

    it('pairs primary-only and secondary-only weapons while permitting an intentionally empty slot', () => {
      const config = cloneLastChancesConfig(defaultConfig)
      removeOuroborosSet(config)
      const primary = makeWeapon('spear', 'primaryOnly')
      const secondary = makeWeapon('knife', 'secondaryOnly', 1)
      removeRoomInteractions(config)
      config.weapons = [primary, secondary]
      config.loadout = { primaryWeaponId: primary.id, secondaryWeaponId: secondary.id }

      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config)).toMatchObject({
        left: { id: primary.id, hand: 'left' },
        right: { id: secondary.id, hand: 'right' },
      })

      config.loadout.secondaryWeaponId = null
      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config).right).toBeNull()
    })

    it('switches a hybrid between its own second attack set and a supplemental weapon', () => {
      const config = cloneLastChancesConfig(defaultConfig)
      removeOuroborosSet(config)
      const hybrid = makeWeapon('wand-blade', 'hybrid')
      hybrid.secondaryAttacks = secondaryAttacks('hybrid-secondary')
      hybrid.controls!.secondary = cloneLastChancesConfig(defaultConfig).weapons[0].controls!.secondary
      hybrid.controls.secondary.dualsense.nodes.forEach((node) => {
        delete node.chargeBandOverrideId
      })
      hybrid.augmentHooks = { chemical: { damageMultiplier: 1.1 } }
      const supplemental = makeWeapon('shield', 'secondaryOnly', 1)
      supplemental.attacks.tap.name = 'shield-tap'
      supplemental.augmentHooks = { bleed: { damageMultiplier: 1.05 } }
      removeRoomInteractions(config)
      config.weapons = [hybrid, supplemental]
      config.loadout = {
        primaryWeaponId: hybrid.id,
        secondaryWeaponId: null,
        primaryAugment: 'chemical',
        secondaryAugment: 'bleed',
      }

      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config).right).toMatchObject({
        id: hybrid.id,
        hand: 'right',
        augment: 'chemical',
        attacks: { tap: { name: 'hybrid-secondary:tap' } },
      })

      config.loadout.secondaryWeaponId = supplemental.id
      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config).right).toMatchObject({
        id: supplemental.id,
        hand: 'right',
        attacks: { tap: { name: 'shield-tap' } },
      })
    })
  })
})
