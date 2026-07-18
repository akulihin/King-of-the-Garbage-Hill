import {
  LAST_CHANCES_ATTACK_BEHAVIORS,
  LAST_CHANCES_ATTACK_KINDS,
  LAST_CHANCES_AUGMENTS,
  LAST_CHANCES_COLLIDER_SHAPES,
  LAST_CHANCES_CONTROL_CONTEXTS,
  LAST_CHANCES_CONTROL_INTENTS,
  LAST_CHANCES_CONTROL_PHASES,
  LAST_CHANCES_DAMAGE_TYPES,
  LAST_CHANCES_ENEMY_ATTACK_KINDS,
  LAST_CHANCES_ENEMY_ROLES,
  LAST_CHANCES_EQUIP_MODES,
  LAST_CHANCES_GESTURES,
  LAST_CHANCES_HAZARD_KINDS,
  LAST_CHANCES_HANDS,
  LAST_CHANCES_STATUS_KINDS,
  LAST_CHANCES_STATUS_REFRESH_MODES,
  LAST_CHANCES_TACTILE_PROFILES,
  LAST_CHANCES_WEAPON_RESOURCE_KINDS,
  LAST_CHANCES_WEAPON_TRAITS,
  LAST_CHANCES_ZONE_SHAPES,
} from './types'
import type {
  LastChancesConfig,
  LastChancesConfigValidation,
  LastChancesAttackDefinition,
  LastChancesAttackSetControlDefinition,
  LastChancesDualSenseInputDefinition,
  LastChancesMylorikActivationDefinition,
  LastChancesMylorikInputDefinition,
  LastChancesGesture,
  LastChancesTactileProfile,
  LastChancesWeaponDefinition,
  LoadLastChancesConfigOptions,
} from './types'

export const LAST_CHANCES_CONFIG_URL = '/99lc/game-config.json'
export const LAST_CHANCES_CONFIG_STORAGE_KEY = '99lc:game-config'

type UnknownRecord = Record<string, unknown>

const CURRENT_LAST_CHANCES_SCHEMA_VERSION = 4
const MAX_GAMEPAD_BUTTON_INDEX = 31
const MAX_FEEDBACK_DURATION_MS = 2_000
const MAX_CONTROL_EXPIRY_MS = 10_000

const LEGACY_SHIPPED_WEAPON_TRAITS: Record<
  string,
  typeof LAST_CHANCES_WEAPON_TRAITS[number]
> = {
  'twohand-spear': 'spearDistance',
  'secondary-chain': 'chainDotCarrier',
  'either-claws': 'clawParity',
  'secondary-spider-knife': 'spiderDurability',
  'twohand-axe': 'axeHookRecovery',
  'twohand-katana': 'katanaFlow',
  'hybrid-sword': 'swordRhythm',
}

const DEFAULT_MYLORIK_INPUT: LastChancesMylorikInputDefinition = {
  techniqueHoldMs: 650,
  bufferMs: 150,
  continuationWindowMs: 480,
  gamepad: {
    leftBumper: 4,
    rightBumper: 5,
    leftTrigger: 6,
    rightTrigger: 7,
    mobilityButton: 1,
    interactButton: 0,
  },
  keyboard: {
    leftTechniqueKeys: ['KeyQ'],
    rightTechniqueKeys: ['KeyE'],
    mobilityKeys: ['Space'],
    interactKeys: ['KeyF'],
    leftStrikeMouseButton: 0,
    rightStrikeMouseButton: 2,
  },
}

const DEFAULT_ADAPTIVE_PROFILES: LastChancesDualSenseInputDefinition['feedback']['profiles'] = {
  click: {
    startPosition: 0.18,
    endPosition: 0.3,
    resistance: 0.24,
    force: 0.28,
    transitionMs: 35,
    effectMs: 90,
    magnitude: 0.22,
  },
  ramp: {
    startPosition: 0.22,
    endPosition: 0.86,
    resistance: 0.3,
    force: 0.55,
    transitionMs: 140,
    effectMs: 600,
    magnitude: 0.38,
  },
  bandLight: {
    startPosition: 0.2,
    endPosition: 0.38,
    resistance: 0.22,
    force: 0.25,
    transitionMs: 30,
    effectMs: 80,
    magnitude: 0.2,
  },
  bandMedium: {
    startPosition: 0.4,
    endPosition: 0.62,
    resistance: 0.34,
    force: 0.42,
    transitionMs: 35,
    effectMs: 95,
    magnitude: 0.34,
  },
  bandStrong: {
    startPosition: 0.64,
    endPosition: 0.84,
    resistance: 0.48,
    force: 0.58,
    transitionMs: 40,
    effectMs: 110,
    magnitude: 0.48,
  },
  gate: {
    startPosition: 0.48,
    endPosition: 0.78,
    resistance: 0.52,
    force: 0.62,
    transitionMs: 70,
    effectMs: 320,
    magnitude: 0.46,
  },
  followUp: {
    startPosition: 0.3,
    endPosition: 0.56,
    resistance: 0.3,
    force: 0.36,
    transitionMs: 45,
    effectMs: 140,
    magnitude: 0.3,
  },
  blocked: {
    startPosition: 0.12,
    endPosition: 0.34,
    resistance: 0.46,
    force: 0.42,
    transitionMs: 45,
    effectMs: 160,
    magnitude: 0.32,
  },
  impact: {
    startPosition: 0.16,
    endPosition: 0.44,
    resistance: 0.38,
    force: 0.5,
    transitionMs: 25,
    effectMs: 120,
    magnitude: 0.55,
  },
  tension: {
    startPosition: 0.26,
    endPosition: 0.82,
    resistance: 0.44,
    force: 0.58,
    transitionMs: 100,
    effectMs: 520,
    magnitude: 0.4,
  },
}

const DEFAULT_DUALSENSE_INPUT: LastChancesDualSenseInputDefinition = {
  activationThreshold: 0.22,
  releaseThreshold: 0.14,
  hysteresis: 0.08,
  gamepad: {
    leftBumper: 4,
    rightBumper: 5,
    leftTrigger: 6,
    rightTrigger: 7,
    circle: 1,
    cross: 0,
    options: 9,
  },
  keyboard: JSON.parse(JSON.stringify(DEFAULT_MYLORIK_INPUT.keyboard)) as LastChancesMylorikInputDefinition['keyboard'],
  gatePositions: {
    shallow: 0.22,
    medium: 0.48,
    deep: 0.72,
    final: 0.9,
  },
  feedback: {
    maxMagnitude: 0.7,
    maxDurationMs: 900,
    blockedRepeatMs: 240,
    profiles: DEFAULT_ADAPTIVE_PROFILES,
  },
}

type MylorikActivationWithoutGesture = Omit<LastChancesMylorikActivationDefinition, 'gesture'>
type DualSenseNodeSeed = Omit<
  LastChancesAttackSetControlDefinition['dualsense']['nodes'][number],
  'id'
>

interface AttackSetControlSeed {
  role: string
  triggerRole: string
  mylorik: Partial<Record<LastChancesGesture, MylorikActivationWithoutGesture>>
  dualsense: DualSenseNodeSeed[]
}

function mylorikActivation(
  intent: MylorikActivationWithoutGesture['intent'],
  phase: MylorikActivationWithoutGesture['phase'],
  context?: MylorikActivationWithoutGesture['context'],
  priority = context ? 80 : 100,
): MylorikActivationWithoutGesture {
  return { intent, phase, ...(context ? { context } : {}), priority }
}

function dualSenseNode(
  gesture: LastChancesGesture,
  entryContext: DualSenseNodeSeed['entryContext'],
  activationThreshold: number,
  options: Partial<Omit<DualSenseNodeSeed, 'gesture' | 'entryContext' | 'activationThreshold'>> = {},
): DualSenseNodeSeed {
  return {
    gesture,
    entryContext,
    activationThreshold,
    dispatch: options.dispatch ?? 'release',
    holdBehavior: options.holdBehavior ?? 'none',
    releaseBehavior: options.releaseBehavior ?? (options.dispatch === 'press' ? 'cancel' : 'dispatch'),
    next: options.next ?? [],
    cancel: options.cancel ?? 'release',
    expiryMs: options.expiryMs ?? 480,
    tactileProfile: options.tactileProfile ?? 'click',
    ...(options.requiredChargeBandId ? { requiredChargeBandId: options.requiredChargeBandId } : {}),
    ...(options.adaptiveOverride ? { adaptiveOverride: options.adaptiveOverride } : {}),
  }
}

const ATTACK_SET_CONTROL_SEEDS: Record<string, AttackSetControlSeed> = {
  'twohand-spear:primary': {
    role: 'Right cluster — lance gearbox',
    triggerRole: 'R2 thrust, overhead release, ram and spin',
    mylorik: {
      tap: mylorikActivation('strike', 'press'),
      doubleTap: mylorikActivation('technique', 'tap'),
      doubleTapHold: mylorikActivation('strike', 'press', 'continuation'),
      hold: mylorikActivation('technique', 'hold'),
      holdThenDoubleTap: mylorikActivation('mobility', 'press', 'continuation'),
    },
    dualsense: [
      dualSenseNode('doubleTap', 'neutral', 0.22, { next: ['hold', 'doubleTapHold'] }),
      dualSenseNode('hold', 'neutral', 0.48, {
        holdBehavior: 'charge',
        next: ['holdThenDoubleTap'],
        tactileProfile: 'ramp',
      }),
      dualSenseNode('doubleTapHold', 'continuation', 0.72, {
        holdBehavior: 'charge',
        tactileProfile: 'gate',
      }),
      dualSenseNode('holdThenDoubleTap', 'continuation', 0.9, {
        dispatch: 'press',
        requiredChargeBandId: 'middle',
        tactileProfile: 'followUp',
      }),
    ],
  },
  'twohand-spear:secondary': {
    role: 'Left cluster — parry, brace, stance and vault',
    triggerRole: 'L2 shove, brace, cutting stance and pole vault',
    mylorik: {
      tap: mylorikActivation('strike', 'press'),
      doubleTap: mylorikActivation('technique', 'tap'),
      doubleTapHold: mylorikActivation('strike', 'press', 'continuation'),
      hold: mylorikActivation('technique', 'hold'),
      holdThenDoubleTap: mylorikActivation('mobility', 'press', 'stance'),
    },
    dualsense: [
      dualSenseNode('doubleTap', 'neutral', 0.22, { next: ['hold', 'doubleTapHold'] }),
      dualSenseNode('hold', 'neutral', 0.48, {
        dispatch: 'press',
        holdBehavior: 'channel',
        releaseBehavior: 'dispatch',
        next: ['holdThenDoubleTap'],
        tactileProfile: 'tension',
      }),
      dualSenseNode('doubleTapHold', 'continuation', 0.72, {
        holdBehavior: 'charge',
        tactileProfile: 'gate',
      }),
      dualSenseNode('holdThenDoubleTap', 'stance', 0.9, {
        dispatch: 'release',
        tactileProfile: 'followUp',
      }),
    ],
  },
  'secondary-chain:primary': {
    role: 'Matching hand — lash and tension spool',
    triggerRole: 'Trigger cast, spin, drag, throw and bind',
    mylorik: {
      tap: mylorikActivation('strike', 'press'),
      doubleTap: mylorikActivation('technique', 'tap'),
      doubleTapHold: mylorikActivation('mobility', 'press', 'spin'),
      hold: mylorikActivation('technique', 'hold'),
      holdThenDoubleTap: mylorikActivation('strike', 'press', 'tether'),
    },
    dualsense: [
      dualSenseNode('hold', 'neutral', 0.22, {
        holdBehavior: 'charge',
        next: ['doubleTap', 'holdThenDoubleTap'],
        tactileProfile: 'tension',
      }),
      dualSenseNode('doubleTap', 'neutral', 0.48, {
        dispatch: 'press',
        next: ['doubleTapHold'],
        tactileProfile: 'gate',
      }),
      dualSenseNode('doubleTapHold', 'spin', 0.72, {
        holdBehavior: 'charge',
        tactileProfile: 'gate',
      }),
      dualSenseNode('holdThenDoubleTap', 'tether', 0.9, {
        dispatch: 'press',
        tactileProfile: 'gate',
      }),
    ],
  },
  'either-claws:primary': {
    role: 'Matching hand — slash and predator spring',
    triggerRole: 'Trigger rend, traversal, disarm and deep critical',
    mylorik: {
      tap: mylorikActivation('strike', 'press'),
      doubleTap: mylorikActivation('technique', 'tap'),
      doubleTapHold: mylorikActivation('strike', 'press', 'continuation'),
      hold: mylorikActivation('mobility', 'hold'),
      holdThenDoubleTap: mylorikActivation('strike', 'press', 'dash', 90),
    },
    dualsense: [
      dualSenseNode('doubleTap', 'neutral', 0.22, { next: ['hold'] }),
      dualSenseNode('hold', 'neutral', 0.48, {
        holdBehavior: 'charge',
        next: ['doubleTapHold', 'holdThenDoubleTap'],
        tactileProfile: 'ramp',
      }),
      dualSenseNode('doubleTapHold', 'neutral', 0.72, {
        tactileProfile: 'gate',
      }),
      dualSenseNode('holdThenDoubleTap', 'dash', 0.9, {
        dispatch: 'press',
        tactileProfile: 'followUp',
      }),
    ],
  },
  'secondary-spider-knife:primary': {
    role: 'Matching hand — slash and ratcheting impale',
    triggerRole: 'Trigger impale, flurry, twist and committed throw',
    mylorik: {
      tap: mylorikActivation('strike', 'press'),
      doubleTap: mylorikActivation('technique', 'tap'),
      doubleTapHold: mylorikActivation('mobility', 'release', 'continuation'),
      hold: mylorikActivation('technique', 'hold'),
      holdThenDoubleTap: mylorikActivation('strike', 'press', 'flurry'),
    },
    dualsense: [
      dualSenseNode('doubleTap', 'neutral', 0.22, { next: ['hold', 'doubleTapHold'] }),
      dualSenseNode('hold', 'neutral', 0.48, {
        dispatch: 'press',
        holdBehavior: 'channel',
        releaseBehavior: 'dispatch',
        next: ['holdThenDoubleTap'],
        tactileProfile: 'tension',
      }),
      dualSenseNode('holdThenDoubleTap', 'flurry', 0.72, {
        tactileProfile: 'gate',
      }),
      dualSenseNode('doubleTapHold', 'neutral', 0.9, {
        holdBehavior: 'charge',
        tactileProfile: 'gate',
      }),
    ],
  },
  'twohand-axe:primary': {
    role: 'Right cluster — chop and grapple lever',
    triggerRole: 'R2 grapple, maintain, aim and throw',
    mylorik: {
      tap: mylorikActivation('strike', 'press'),
      doubleTap: mylorikActivation('technique', 'tap'),
      doubleTapHold: mylorikActivation('technique', 'hold', 'grapple'),
    },
    dualsense: [
      dualSenseNode('doubleTap', 'neutral', 0.22, {
        dispatch: 'press',
        next: ['doubleTapHold'],
        tactileProfile: 'gate',
      }),
      dualSenseNode('doubleTapHold', 'grapple', 0.72, {
        holdBehavior: 'charge',
        tactileProfile: 'tension',
      }),
    ],
  },
  'twohand-axe:secondary': {
    role: 'Left cluster — long parry and flywheel',
    triggerRole: 'L2 reflecting spin and momentum leap',
    mylorik: {
      tap: mylorikActivation('strike', 'press'),
      hold: mylorikActivation('technique', 'hold'),
      holdThenDoubleTap: mylorikActivation('mobility', 'press', 'spin'),
    },
    dualsense: [
      dualSenseNode('hold', 'neutral', 0.22, {
        dispatch: 'press',
        holdBehavior: 'channel',
        releaseBehavior: 'dispatch',
        next: ['holdThenDoubleTap'],
        tactileProfile: 'ramp',
      }),
      dualSenseNode('holdThenDoubleTap', 'spin', 0.72, {
        dispatch: 'release',
        tactileProfile: 'followUp',
      }),
    ],
  },
  'twohand-katana:primary': {
    role: 'Right cluster — cut and draw-and-flow rail',
    triggerRole: 'R2 overhead, charge, flurry and dance',
    mylorik: {
      tap: mylorikActivation('strike', 'press'),
      doubleTap: mylorikActivation('technique', 'tap'),
      doubleTapHold: mylorikActivation('strike', 'press', 'continuation'),
      hold: mylorikActivation('technique', 'hold'),
      holdThenDoubleTap: mylorikActivation('mobility', 'press', 'continuation'),
    },
    dualsense: [
      dualSenseNode('doubleTap', 'neutral', 0.22, { next: ['hold', 'doubleTapHold'] }),
      dualSenseNode('hold', 'neutral', 0.48, {
        holdBehavior: 'charge',
        next: ['holdThenDoubleTap'],
        tactileProfile: 'ramp',
      }),
      dualSenseNode('doubleTapHold', 'continuation', 0.72, {
        tactileProfile: 'gate',
      }),
      dualSenseNode('holdThenDoubleTap', 'continuation', 0.9, {
        dispatch: 'press',
        requiredChargeBandId: 'katana-charge',
        tactileProfile: 'followUp',
      }),
    ],
  },
  'twohand-katana:secondary': {
    role: 'Left cluster — parry and movement/sheath rail',
    triggerRole: 'L2 hop, hop-slash, Iaido and Flash',
    mylorik: {
      tap: mylorikActivation('strike', 'press'),
      doubleTap: mylorikActivation('technique', 'tap'),
      doubleTapHold: mylorikActivation('strike', 'press', 'continuation'),
      hold: mylorikActivation('technique', 'hold'),
      holdThenDoubleTap: mylorikActivation('mobility', 'press', 'continuation'),
    },
    dualsense: [
      dualSenseNode('doubleTap', 'neutral', 0.22, { next: ['hold', 'doubleTapHold'] }),
      dualSenseNode('hold', 'neutral', 0.48, {
        holdBehavior: 'charge',
        next: ['holdThenDoubleTap'],
        tactileProfile: 'ramp',
      }),
      dualSenseNode('doubleTapHold', 'continuation', 0.72, {
        tactileProfile: 'gate',
      }),
      dualSenseNode('holdThenDoubleTap', 'continuation', 0.9, {
        dispatch: 'press',
        requiredChargeBandId: 'iaido-ready',
        tactileProfile: 'followUp',
      }),
    ],
  },
  'hybrid-sword:primary': {
    role: 'Matching hand — Zornhau and opening breaker',
    triggerRole: 'Trigger opening, Oberhau and delayed Unterhau',
    mylorik: {
      tap: mylorikActivation('strike', 'press'),
      doubleTap: mylorikActivation('technique', 'tap', 'opening'),
      doubleTapHold: mylorikActivation('technique', 'hold', 'opening'),
    },
    dualsense: [
      dualSenseNode('doubleTap', 'opening', 0.22, { next: ['doubleTapHold'], tactileProfile: 'followUp' }),
      dualSenseNode('doubleTapHold', 'opening', 0.72, {
        holdBehavior: 'charge',
        tactileProfile: 'gate',
      }),
    ],
  },
  'hybrid-sword:secondary': {
    role: 'Matching hand — Zornhau and opening breaker',
    triggerRole: 'Trigger opening, Oberhau and delayed Unterhau',
    mylorik: {
      tap: mylorikActivation('strike', 'press'),
      doubleTap: mylorikActivation('technique', 'tap', 'opening'),
      doubleTapHold: mylorikActivation('technique', 'hold', 'opening'),
    },
    dualsense: [
      dualSenseNode('doubleTap', 'opening', 0.22, { next: ['doubleTapHold'], tactileProfile: 'followUp' }),
      dualSenseNode('doubleTapHold', 'opening', 0.72, {
        holdBehavior: 'charge',
        tactileProfile: 'gate',
      }),
    ],
  },
}

function buildAttackSetControls(
  seed: AttackSetControlSeed,
  attacks: Record<LastChancesGesture, LastChancesAttackDefinition>,
): LastChancesAttackSetControlDefinition {
  const enabledGestures = new Set(LAST_CHANCES_GESTURES.filter((gesture) => {
    const attack = attacks[gesture]
    return attack.enabled !== false && attack.behavior !== 'disabled'
  }))
  const activations = LAST_CHANCES_GESTURES.flatMap((gesture) => {
    const activation = seed.mylorik[gesture]
    return enabledGestures.has(gesture) && activation ? [{ gesture, ...activation }] : []
  })
  const nodes = seed.dualsense
    .filter(node => enabledGestures.has(node.gesture))
    .map(node => ({
      ...node,
      id: node.gesture,
      next: node.next.filter(nextId => enabledGestures.has(nextId as LastChancesGesture)),
    }))
  return {
    role: seed.role,
    mylorik: { activations },
    dualsense: {
      instantGesture: 'tap',
      triggerRole: seed.triggerRole,
      startNodeId: nodes[0]?.id ?? null,
      nodes,
    },
  }
}

function migratedWeaponControls(weapon: LastChancesWeaponDefinition): LastChancesWeaponDefinition['controls'] {
  const primarySeed = ATTACK_SET_CONTROL_SEEDS[`${weapon.id}:primary`]
  const primary = primarySeed
    ? buildAttackSetControls(primarySeed, weapon.attacks)
    : buildLegacyAttackSetControls(weapon.attacks, `${weapon.name} legacy controls`)
  if (!weapon.secondaryAttacks) return { primary }
  const secondarySeed = ATTACK_SET_CONTROL_SEEDS[`${weapon.id}:secondary`]
  return {
    primary,
    secondary: secondarySeed
      ? buildAttackSetControls(secondarySeed, weapon.secondaryAttacks)
      : buildLegacyAttackSetControls(weapon.secondaryAttacks, `${weapon.name} legacy support controls`),
  }
}

function buildLegacyAttackSetControls(
  attacks: Record<LastChancesGesture, LastChancesAttackDefinition>,
  role: string,
): LastChancesAttackSetControlDefinition {
  const enabled = LAST_CHANCES_GESTURES.filter((gesture) => {
    const attack = attacks[gesture]
    return attack.enabled !== false && attack.behavior !== 'disabled'
  })
  const activationByGesture: Record<LastChancesGesture, MylorikActivationWithoutGesture> = {
    tap: mylorikActivation('strike', 'press'),
    doubleTap: mylorikActivation('technique', 'tap'),
    doubleTapHold: mylorikActivation('strike', 'press', 'continuation'),
    hold: mylorikActivation('technique', 'hold'),
    holdThenDoubleTap: mylorikActivation('mobility', 'press', 'continuation'),
  }
  const routeGestures = (['doubleTap', 'hold', 'doubleTapHold', 'holdThenDoubleTap'] as const)
    .filter(gesture => enabled.includes(gesture))
  const gatePositions = [
    DEFAULT_DUALSENSE_INPUT.gatePositions.shallow,
    DEFAULT_DUALSENSE_INPUT.gatePositions.medium,
    DEFAULT_DUALSENSE_INPUT.gatePositions.deep,
    DEFAULT_DUALSENSE_INPUT.gatePositions.final,
  ]
  const nodes = routeGestures.map((gesture, index) => dualSenseNode(
    gesture,
    index === 0 ? 'neutral' : 'continuation',
    gatePositions[index],
    { next: routeGestures[index + 1] ? [routeGestures[index + 1]] : [] },
  )).map((node, index) => ({ ...node, id: routeGestures[index] }))
  return {
    role,
    mylorik: {
      activations: enabled.map(gesture => ({ gesture, ...activationByGesture[gesture] })),
    },
    dualsense: {
      instantGesture: 'tap',
      triggerRole: `${role} trigger route`,
      startNodeId: nodes[0]?.id ?? null,
      nodes,
    },
  }
}

const LEGACY_SPAWN_RELOCATIONS: Record<string, Record<string, { x: number, y: number }>> = {
  'chest-gallery': {
    '730:160': { x: 780, y: 160 },
    '720:550': { x: 785, y: 550 },
    '425:345': { x: 350, y: 345 },
  },
  'wrong-shadow-event': {
    '570:595': { x: 650, y: 595 },
    '420:345': { x: 420, y: 370 },
  },
}

export class LastChancesConfigError extends Error {
  readonly errors: string[]

  constructor(message: string, errors: string[]) {
    super(`${message}: ${errors.join('; ')}`)
    this.name = 'LastChancesConfigError'
    this.errors = errors
  }
}

function cloneUnknown<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

/** Repairs the concrete schema-v1 definition that shipped before strict validation. */
function repairSchemaV1(migrated: UnknownRecord): void {
  if (Array.isArray(migrated.weapons)) {
    migrated.weapons.forEach((weaponValue) => {
      if (typeof weaponValue !== 'object' || weaponValue === null || Array.isArray(weaponValue)) return
      const weapon = weaponValue as UnknownRecord
      for (const attackSetKey of ['attacks', 'secondaryAttacks'] as const) {
        const attackSet = weapon[attackSetKey]
        if (typeof attackSet !== 'object' || attackSet === null || Array.isArray(attackSet)) continue
        const tap = (attackSet as UnknownRecord).tap
        if (typeof tap === 'object' && tap !== null && !Array.isArray(tap)) {
          (tap as UnknownRecord).cooldownMs = 0
        }
      }
      for (const comboKey of ['tapCombo', 'secondaryTapCombo'] as const) {
        const combo = weapon[comboKey]
        if (!Array.isArray(combo)) continue
        combo.forEach((attack) => {
          if (typeof attack === 'object' && attack !== null && !Array.isArray(attack)) {
            (attack as UnknownRecord).cooldownMs = 0
          }
        })
      }
    })
  }

  const { globalEnemyRadius, roomEnemyRadii } = eligibleEnemySpawnRadii(migrated)
  const player = typeof migrated.player === 'object' && migrated.player !== null
    && !Array.isArray(migrated.player) ? migrated.player as UnknownRecord : null
  const playerRadius = player ? Math.max(0, finiteRecordNumber(player, 'radius') ?? 0) : 0

  if (Array.isArray(migrated.rooms)) {
    migrated.rooms.forEach((roomValue) => {
      if (typeof roomValue !== 'object' || roomValue === null || Array.isArray(roomValue)) return
      const room = roomValue as UnknownRecord
      if (typeof room.id !== 'string' || !Array.isArray(room.enemySpawns)) return
      const relocations = LEGACY_SPAWN_RELOCATIONS[room.id]
      if (!relocations) return
      const enemyRadius = roomEnemyRadii.get(room.id) ?? globalEnemyRadius
      room.enemySpawns = room.enemySpawns.map((spawnValue) => {
        if (typeof spawnValue !== 'object' || spawnValue === null || Array.isArray(spawnValue)) return spawnValue
        const spawn = spawnValue as UnknownRecord
        const replacement = relocations[`${spawn.x}:${spawn.y}`]
        if (!replacement
          || spawnFitsRoomGeometry(spawn, enemyRadius, room, playerRadius)
          || !spawnFitsRoomGeometry(replacement, enemyRadius, room, playerRadius)) return spawnValue
        return { ...replacement }
      })
    })
  }

}

function migrateSchemaV1ToV2(migrated: UnknownRecord): void {
  const input = typeof migrated.input === 'object' && migrated.input !== null
    && !Array.isArray(migrated.input) ? migrated.input as UnknownRecord : null
  if (input && input.tapComboWindowMs === undefined) input.tapComboWindowMs = 900

  if (Array.isArray(migrated.rooms)) {
    migrated.rooms.forEach((roomValue, index) => {
      if (typeof roomValue !== 'object' || roomValue === null || Array.isArray(roomValue)) return
      const room = roomValue as UnknownRecord
      if (room.spawnLayouts !== undefined || !Array.isArray(room.enemySpawns)) return
      const id = typeof room.id === 'string' ? room.id : `room-${index + 1}`
      const name = typeof room.name === 'string' ? room.name : id
      room.spawnLayouts = [
        { id: `${id}-legacy-a`, name: `${name} A`, enemySpawns: cloneUnknown(room.enemySpawns) },
        { id: `${id}-legacy-b`, name: `${name} B`, enemySpawns: cloneUnknown(room.enemySpawns) },
      ]
      delete room.enemySpawns
    })
  }

  if (Array.isArray(migrated.enemies)) {
    migrated.enemies.forEach((enemyValue) => {
      if (typeof enemyValue !== 'object' || enemyValue === null || Array.isArray(enemyValue)) return
      const enemy = enemyValue as UnknownRecord
      if (enemy.idleTurnRadiansPerSecond === undefined) enemy.idleTurnRadiansPerSecond = 0
      if (enemy.preferredAttackRangeRatio === undefined) enemy.preferredAttackRangeRatio = 0.75
    })
  }

  let primaryWeaponId: string | null = null
  let secondaryWeaponId: string | null = null
  if (Array.isArray(migrated.weapons)) {
    migrated.weapons.forEach((weaponValue) => {
      if (typeof weaponValue !== 'object' || weaponValue === null || Array.isArray(weaponValue)) return
      const weapon = weaponValue as UnknownRecord
      const hand = weapon.hand
      if (hand === 'left' && typeof weapon.id === 'string') primaryWeaponId ??= weapon.id
      if (hand === 'right' && typeof weapon.id === 'string') secondaryWeaponId ??= weapon.id
      if (weapon.equipMode === undefined) {
        weapon.equipMode = hand === 'right' ? 'secondaryOnly' : 'primaryOnly'
      }
      delete weapon.hand
      if (weapon.tapCombo === undefined && typeof weapon.attacks === 'object'
        && weapon.attacks !== null && !Array.isArray(weapon.attacks)) {
        const tap = (weapon.attacks as UnknownRecord).tap
        if (typeof tap === 'object' && tap !== null && !Array.isArray(tap)) {
          const comboTap = cloneUnknown(tap) as UnknownRecord
          comboTap.cooldownMs = 0
          comboTap.name = `${String(comboTap.name)} · combo`
          weapon.tapCombo = [comboTap]
        }
      }
    })
  }
  if (migrated.loadout === undefined && primaryWeaponId) {
    migrated.loadout = { primaryWeaponId, secondaryWeaponId }
  }
  migrated.schemaVersion = 2
}

function migrateSchemaV2ToV3(migrated: UnknownRecord): void {
  const visitAttack = (attackValue: unknown): void => {
    if (typeof attackValue !== 'object' || attackValue === null || Array.isArray(attackValue)) return
    const attack = attackValue as UnknownRecord
    if (attack.behavior === undefined) attack.behavior = attack.enabled === false ? 'disabled' : 'standard'
    if (attack.enabled !== false && attack.collider === undefined) {
      const shape = attack.kind === 'burst'
        ? 'circle'
        : attack.kind === 'projectile' || attack.kind === 'dash' ? 'capsule' : 'sector'
      attack.collider = { shape, traceMs: 180 }
    }
  }
  const visitAttackSet = (attackSetValue: unknown): void => {
    if (typeof attackSetValue !== 'object' || attackSetValue === null || Array.isArray(attackSetValue)) return
    Object.values(attackSetValue as UnknownRecord).forEach(visitAttack)
  }

  if (Array.isArray(migrated.weapons)) {
    migrated.weapons.forEach((weaponValue) => {
      if (typeof weaponValue !== 'object' || weaponValue === null || Array.isArray(weaponValue)) return
      const weapon = weaponValue as UnknownRecord
      if (weapon.trait === undefined) {
        weapon.trait = typeof weapon.id === 'string'
          ? LEGACY_SHIPPED_WEAPON_TRAITS[weapon.id] ?? 'spearDistance'
          : 'spearDistance'
      }
      visitAttackSet(weapon.attacks)
      visitAttackSet(weapon.secondaryAttacks)
      if (Array.isArray(weapon.tapCombo)) weapon.tapCombo.forEach(visitAttack)
      if (Array.isArray(weapon.secondaryTapCombo)) weapon.secondaryTapCombo.forEach(visitAttack)
    })
  }
  migrated.schemaVersion = 3
}

function attachCurrentControlCatalog(
  weaponsValue: unknown,
  currentWeapons?: LastChancesWeaponDefinition[],
): void {
  if (!Array.isArray(weaponsValue)) return
  const currentById = new Map((currentWeapons ?? []).map(weapon => [weapon.id, weapon]))
  weaponsValue.forEach((weaponValue) => {
    if (typeof weaponValue !== 'object' || weaponValue === null || Array.isArray(weaponValue)) return
    const weapon = weaponValue as UnknownRecord
    const currentWeapon = typeof weapon.id === 'string' ? currentById.get(weapon.id) : undefined
    const controls = currentWeapon?.controls
      ?? migratedWeaponControls(weapon as unknown as LastChancesWeaponDefinition)
    if (controls) weapon.controls = cloneUnknown(controls)
  })
}

function legacyCatalogMatchesCurrent(
  legacyWeapons: unknown,
  currentWeapons: LastChancesWeaponDefinition[],
): legacyWeapons is LastChancesWeaponDefinition[] {
  if (!Array.isArray(legacyWeapons) || legacyWeapons.length !== currentWeapons.length) return false
  const currentIds = new Set(currentWeapons.map(weapon => weapon.id))
  return legacyWeapons.every((weapon) => (
    typeof weapon === 'object' && weapon !== null && !Array.isArray(weapon)
    && typeof (weapon as UnknownRecord).id === 'string'
    && currentIds.has((weapon as UnknownRecord).id as string)
  ))
}

function mergeCurrentShape(currentValue: unknown, legacyValue: unknown): unknown {
  if (legacyValue === undefined) return cloneUnknown(currentValue)
  if (Array.isArray(currentValue) || Array.isArray(legacyValue)) return cloneUnknown(legacyValue)
  if (typeof currentValue !== 'object' || currentValue === null
    || typeof legacyValue !== 'object' || legacyValue === null) {
    return cloneUnknown(legacyValue)
  }
  const merged = cloneUnknown(currentValue) as UnknownRecord
  Object.entries(legacyValue as UnknownRecord).forEach(([key, value]) => {
    merged[key] = mergeCurrentShape(merged[key], value)
  })
  return merged
}

function mergeLegacyDefinitionWithCurrent(
  legacyValue: UnknownRecord,
  current: LastChancesConfig,
): UnknownRecord {
  const legacyValidation = validateLastChancesConfig(legacyValue)
  if (!legacyValidation.valid) {
    throw new LastChancesConfigError('Invalid 99LC browser override', legacyValidation.errors)
  }

  const legacy = legacyValue as unknown as LastChancesConfig
  const merged = cloneLastChancesConfig(current)
  const copiedKeys = [
    'title',
    'seed',
    'chances',
    'graph',
    'player',
    'mentalHealth',
    'progression',
    'narrative',
    'renderer',
  ] as const
  copiedKeys.forEach((key) => {
    const legacyField = legacy[key]
    if (legacyField !== undefined) {
      ;(merged as unknown as UnknownRecord)[key] = cloneUnknown(legacyField)
    }
  })

  merged.input = {
    ...cloneUnknown(current.input),
    ...cloneUnknown(legacy.input),
    mylorik: cloneUnknown(current.input.mylorik ?? DEFAULT_MYLORIK_INPUT),
    dualsense: cloneUnknown(current.input.dualsense ?? DEFAULT_DUALSENSE_INPUT),
  }

  const legacyEnemies = new Map(legacy.enemies.map(enemy => [enemy.id, enemy]))
  merged.enemies = current.enemies.map((currentEnemy) => (
    mergeCurrentShape(currentEnemy, legacyEnemies.get(currentEnemy.id))
  )) as LastChancesConfig['enemies']

  const legacyRooms = new Map(legacy.rooms.map(room => [room.id, room]))
  merged.rooms = current.rooms.map((currentRoom) => {
    const legacyRoom = legacyRooms.get(currentRoom.id)
    const mergedRoom = mergeCurrentShape(
      currentRoom,
      legacyRoom,
    ) as LastChancesConfig['rooms'][number]
    if (legacyRoom?.enemySpawns && !legacyRoom.spawnLayouts && currentRoom.spawnLayouts) {
      mergedRoom.spawnLayouts = currentRoom.spawnLayouts.map(layout => ({
        ...cloneUnknown(layout),
        enemySpawns: cloneUnknown(legacyRoom.enemySpawns!),
      }))
    }
    if (currentRoom.interaction) mergedRoom.interaction = cloneUnknown(currentRoom.interaction)
    else delete mergedRoom.interaction
    return mergedRoom
  })

  if (legacyCatalogMatchesCurrent(legacy.weapons, current.weapons)) {
    const legacyWeapons = new Map(legacy.weapons.map(weapon => [weapon.id, weapon]))
    merged.weapons = current.weapons.map((currentWeapon) => (
      mergeCurrentShape(currentWeapon, legacyWeapons.get(currentWeapon.id))
    )) as LastChancesConfig['weapons']
    attachCurrentControlCatalog(merged.weapons, current.weapons)
    merged.loadout = legacy.loadout ? cloneUnknown(legacy.loadout) : cloneUnknown(current.loadout)
  } else {
    merged.weapons = cloneUnknown(current.weapons)
    merged.loadout = cloneUnknown(current.loadout)
  }
  merged.schemaVersion = CURRENT_LAST_CHANCES_SCHEMA_VERSION
  return merged as unknown as UnknownRecord
}

/**
 * Clone-first sequential migration for the real schema-v1/v2/v3 definitions.
 * A current definition lets old browser overrides retain run tuning while taking
 * the shipped v4 catalog records that did not exist when the override was saved.
 */
export function migrateLastChancesConfig(
  value: unknown,
  currentDefinition?: LastChancesConfig,
): unknown {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return value
  const migrated = cloneUnknown(value) as UnknownRecord
  const version = migrated.schemaVersion
  if (!Number.isInteger(version)) return migrated
  if ((version as number) > CURRENT_LAST_CHANCES_SCHEMA_VERSION) {
    throw new LastChancesConfigError('Unsupported 99LC schemaVersion', [
      `schemaVersion ${String(version)} is newer than supported ${CURRENT_LAST_CHANCES_SCHEMA_VERSION}`,
    ])
  }
  if ((version as number) < 1) return migrated
  if (version === CURRENT_LAST_CHANCES_SCHEMA_VERSION) return migrated

  if (version === 1) repairSchemaV1(migrated)
  if (currentDefinition) return mergeLegacyDefinitionWithCurrent(migrated, currentDefinition)

  if (migrated.schemaVersion === 1) migrateSchemaV1ToV2(migrated)
  if (migrated.schemaVersion === 2) migrateSchemaV2ToV3(migrated)

  // Standalone legacy Builder imports retain their own catalog. Inject only the
  // mechanically derived prior-schema fields and the new v4 control records;
  // browser migrations use the current shipped baseline above.
  const input = typeof migrated.input === 'object' && migrated.input !== null
    && !Array.isArray(migrated.input) ? migrated.input as UnknownRecord : null
  if (input) {
    input.mylorik = cloneUnknown(DEFAULT_MYLORIK_INPUT)
    input.dualsense = cloneUnknown(DEFAULT_DUALSENSE_INPUT)
  }
  attachCurrentControlCatalog(migrated.weapons)
  migrated.schemaVersion = CURRENT_LAST_CHANCES_SCHEMA_VERSION
  return migrated
}

function asRecord(value: unknown, path: string, errors: string[]): UnknownRecord | null {
  if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
    return value as UnknownRecord
  }
  errors.push(`${path} must be an object`)
  return null
}

function requireString(record: UnknownRecord, key: string, path: string, errors: string[]): void {
  if (typeof record[key] !== 'string' || (record[key] as string).trim().length === 0) {
    errors.push(`${path}.${key} must be a non-empty string`)
  }
}

function requireNumber(
  record: UnknownRecord,
  key: string,
  path: string,
  errors: string[],
  minimum = 0,
): void {
  const value = record[key]
  if (typeof value !== 'number' || !Number.isFinite(value) || value < minimum) {
    errors.push(`${path}.${key} must be a finite number >= ${minimum}`)
  }
}

function requirePositiveNumber(
  record: UnknownRecord,
  key: string,
  path: string,
  errors: string[],
): void {
  const value = record[key]
  if (typeof value !== 'number' || !Number.isFinite(value) || value <= 0) {
    errors.push(`${path}.${key} must be a finite number > 0`)
  }
}

function requireInteger(
  record: UnknownRecord,
  key: string,
  path: string,
  errors: string[],
  minimum = 0,
): void {
  const value = record[key]
  if (!Number.isInteger(value) || (value as number) < minimum) {
    errors.push(`${path}.${key} must be an integer >= ${minimum}`)
  }
}

function requireStringArray(record: UnknownRecord, key: string, path: string, errors: string[]): void {
  const value = record[key]
  if (!Array.isArray(value) || value.length === 0
    || value.some(item => typeof item !== 'string' || item.trim().length === 0)) {
    errors.push(`${path}.${key} must be a non-empty string array`)
  }
}

function validateIntegerRange(
  record: UnknownRecord,
  key: string,
  path: string,
  errors: string[],
  maximum: number,
): void {
  requireInteger(record, key, path, errors)
  const value = record[key]
  if (Number.isInteger(value) && (value as number) > maximum) {
    errors.push(`${path}.${key} must be <= ${maximum}`)
  }
}

function validateUnitNumber(
  record: UnknownRecord,
  key: string,
  path: string,
  errors: string[],
): void {
  requireNumber(record, key, path, errors)
  const value = record[key]
  if (typeof value === 'number' && Number.isFinite(value) && value > 1) {
    errors.push(`${path}.${key} must be <= 1`)
  }
}

function validateUniqueBindings(
  record: UnknownRecord,
  keys: readonly string[],
  path: string,
  errors: string[],
): void {
  const used = new Map<number, string>()
  keys.forEach((key) => {
    validateIntegerRange(record, key, path, errors, MAX_GAMEPAD_BUTTON_INDEX)
    const value = record[key]
    if (!Number.isInteger(value)) return
    const previous = used.get(value as number)
    if (previous) errors.push(`${path}.${key} duplicates ${path}.${previous}`)
    else used.set(value as number, key)
  })
}

function validateSchemeKeyboard(value: unknown, path: string, errors: string[]): void {
  const keyboard = asRecord(value, path, errors)
  if (!keyboard) return
  const keyGroups = [
    'leftTechniqueKeys',
    'rightTechniqueKeys',
    'mobilityKeys',
    'interactKeys',
  ] as const
  const usedKeys = new Map<string, string>()
  keyGroups.forEach((key) => {
    requireStringArray(keyboard, key, path, errors)
    const bindings = keyboard[key]
    if (!Array.isArray(bindings)) return
    bindings.forEach((binding) => {
      if (typeof binding !== 'string' || binding.trim().length === 0) return
      const previous = usedKeys.get(binding)
      if (previous) errors.push(`${path}.${key} duplicates key ${binding} from ${path}.${previous}`)
      else usedKeys.set(binding, key)
    })
  })
  validateIntegerRange(keyboard, 'leftStrikeMouseButton', path, errors, MAX_GAMEPAD_BUTTON_INDEX)
  validateIntegerRange(keyboard, 'rightStrikeMouseButton', path, errors, MAX_GAMEPAD_BUTTON_INDEX)
  if (keyboard.leftStrikeMouseButton === keyboard.rightStrikeMouseButton
    && Number.isInteger(keyboard.leftStrikeMouseButton)) {
    errors.push(`${path}.rightStrikeMouseButton duplicates ${path}.leftStrikeMouseButton`)
  }
}

function validateMylorikInput(value: unknown, path: string, errors: string[]): void {
  const input = asRecord(value, path, errors)
  if (!input) return
  requirePositiveNumber(input, 'techniqueHoldMs', path, errors)
  requirePositiveNumber(input, 'bufferMs', path, errors)
  requirePositiveNumber(input, 'continuationWindowMs', path, errors)
  const gamepad = asRecord(input.gamepad, `${path}.gamepad`, errors)
  if (gamepad) {
    validateUniqueBindings(gamepad, [
      'leftBumper',
      'rightBumper',
      'leftTrigger',
      'rightTrigger',
      'mobilityButton',
      'interactButton',
    ], `${path}.gamepad`, errors)
  }
  validateSchemeKeyboard(input.keyboard, `${path}.keyboard`, errors)
}

function validateAdaptiveProfile(
  value: unknown,
  path: string,
  maxDurationMs: number | null,
  errors: string[],
  partial = false,
): void {
  const profile = asRecord(value, path, errors)
  if (!profile) return
  const unitFields = [
    'startPosition',
    'endPosition',
    'resistance',
    'force',
    'magnitude',
  ] as const
  unitFields.forEach((key) => {
    if (!partial || profile[key] !== undefined) validateUnitNumber(profile, key, path, errors)
  })
  for (const key of ['transitionMs', 'effectMs'] as const) {
    if (!partial || profile[key] !== undefined) {
      requireNumber(profile, key, path, errors)
      if (typeof profile[key] === 'number' && maxDurationMs !== null
        && profile[key] > maxDurationMs) {
        errors.push(`${path}.${key} must be <= feedback.maxDurationMs (${maxDurationMs})`)
      }
    }
  }
  if (typeof profile.startPosition === 'number' && typeof profile.endPosition === 'number'
    && profile.startPosition > profile.endPosition) {
    errors.push(`${path}.startPosition must be <= endPosition`)
  }
}

function validateDualSenseInput(value: unknown, path: string, errors: string[]): number[] {
  const input = asRecord(value, path, errors)
  if (!input) return []
  for (const key of ['activationThreshold', 'releaseThreshold', 'hysteresis'] as const) {
    validateUnitNumber(input, key, path, errors)
  }
  if (typeof input.releaseThreshold === 'number' && typeof input.activationThreshold === 'number'
    && input.releaseThreshold >= input.activationThreshold) {
    errors.push(`${path}.releaseThreshold must be less than activationThreshold`)
  }
  if (typeof input.hysteresis === 'number' && input.hysteresis <= 0) {
    errors.push(`${path}.hysteresis must be > 0`)
  }
  if (typeof input.releaseThreshold === 'number' && typeof input.activationThreshold === 'number'
    && typeof input.hysteresis === 'number'
    && input.activationThreshold - input.releaseThreshold + Number.EPSILON < input.hysteresis) {
    errors.push(`${path}.activationThreshold - releaseThreshold must be >= hysteresis`)
  }
  if (typeof input.releaseThreshold === 'number' && typeof input.hysteresis === 'number'
    && input.releaseThreshold + Number.EPSILON < input.hysteresis) {
    errors.push(`${path}.releaseThreshold must be >= hysteresis so the neutral re-arm gate is reachable`)
  }

  const gamepad = asRecord(input.gamepad, `${path}.gamepad`, errors)
  if (gamepad) {
    validateUniqueBindings(gamepad, [
      'leftBumper',
      'rightBumper',
      'leftTrigger',
      'rightTrigger',
      'circle',
      'cross',
      'options',
    ], `${path}.gamepad`, errors)
  }
  validateSchemeKeyboard(input.keyboard, `${path}.keyboard`, errors)

  const gatePositions = asRecord(input.gatePositions, `${path}.gatePositions`, errors)
  const gates: number[] = []
  if (gatePositions) {
    for (const key of ['shallow', 'medium', 'deep', 'final'] as const) {
      validateUnitNumber(gatePositions, key, `${path}.gatePositions`, errors)
      if (typeof gatePositions[key] === 'number' && Number.isFinite(gatePositions[key])) {
        gates.push(gatePositions[key] as number)
      }
    }
    if (gates.length === 4 && !gates.every((gate, index) => index === 0 || gate > gates[index - 1])) {
      errors.push(`${path}.gatePositions must be strictly increasing`)
    }
    if (typeof gatePositions.shallow === 'number' && typeof input.activationThreshold === 'number'
      && gatePositions.shallow < input.activationThreshold) {
      errors.push(`${path}.gatePositions.shallow must be >= activationThreshold`)
    }
  }

  const feedback = asRecord(input.feedback, `${path}.feedback`, errors)
  if (feedback) {
    validateUnitNumber(feedback, 'maxMagnitude', `${path}.feedback`, errors)
    requirePositiveNumber(feedback, 'maxDurationMs', `${path}.feedback`, errors)
    requirePositiveNumber(feedback, 'blockedRepeatMs', `${path}.feedback`, errors)
    if (typeof feedback.maxDurationMs === 'number'
      && feedback.maxDurationMs > MAX_FEEDBACK_DURATION_MS) {
      errors.push(`${path}.feedback.maxDurationMs must be <= ${MAX_FEEDBACK_DURATION_MS}`)
    }
    if (typeof feedback.blockedRepeatMs === 'number'
      && feedback.blockedRepeatMs > MAX_CONTROL_EXPIRY_MS) {
      errors.push(`${path}.feedback.blockedRepeatMs must be <= ${MAX_CONTROL_EXPIRY_MS}`)
    }
    const profiles = asRecord(feedback.profiles, `${path}.feedback.profiles`, errors)
    if (profiles) {
      LAST_CHANCES_TACTILE_PROFILES.forEach((profile) => {
        if (profiles[profile] === undefined) {
          errors.push(`${path}.feedback.profiles.${profile} is required`)
          return
        }
        validateAdaptiveProfile(
          profiles[profile],
          `${path}.feedback.profiles.${profile}`,
          typeof feedback.maxDurationMs === 'number' ? feedback.maxDurationMs : null,
          errors,
        )
        if (typeof feedback.maxMagnitude === 'number'
          && typeof (profiles[profile] as UnknownRecord)?.magnitude === 'number'
          && ((profiles[profile] as UnknownRecord).magnitude as number) > feedback.maxMagnitude) {
          errors.push(`${path}.feedback.profiles.${profile}.magnitude must be <= feedback.maxMagnitude (${feedback.maxMagnitude})`)
        }
      })
    }
  }
  return gates
}

function validateVector(value: unknown, path: string, errors: string[]): void {
  const record = asRecord(value, path, errors)
  if (!record) return
  requireNumber(record, 'x', path, errors)
  requireNumber(record, 'y', path, errors)
}

function validateStats(value: unknown, path: string, errors: string[], allowZero: boolean): void {
  const record = asRecord(value, path, errors)
  if (!record) return
  if (allowZero) {
    for (const key of ['maxHp', 'maxMentalHealth', 'attackPower', 'moveSpeed', 'armor'] as const) {
      requireNumber(record, key, path, errors)
    }
  } else {
    for (const key of ['maxHp', 'maxMentalHealth', 'attackPower', 'moveSpeed'] as const) {
      requirePositiveNumber(record, key, path, errors)
    }
    requireNumber(record, 'armor', path, errors)
  }
}

function validateHitEffects(value: unknown, path: string, errors: string[]): void {
  if (value === undefined) return
  if (!Array.isArray(value)) {
    errors.push(`${path} must be an array`)
    return
  }
  value.forEach((effectValue, index) => {
    const effectPath = `${path}[${index}]`
    const effect = asRecord(effectValue, effectPath, errors)
    if (!effect) return
    if (!LAST_CHANCES_STATUS_KINDS.includes(
      effect.status as typeof LAST_CHANCES_STATUS_KINDS[number],
    )) {
      errors.push(`${effectPath}.status must be one of ${LAST_CHANCES_STATUS_KINDS.join(', ')}`)
    }
    requirePositiveNumber(effect, 'durationMs', effectPath, errors)
    if (effect.stacks !== undefined) requireInteger(effect, 'stacks', effectPath, errors, 1)
    if (effect.chance !== undefined) {
      requireNumber(effect, 'chance', effectPath, errors)
      if (typeof effect.chance === 'number' && effect.chance > 1) {
        errors.push(`${effectPath}.chance must be <= 1`)
      }
    }
    for (const key of ['magnitude', 'tickDamage'] as const) {
      if (effect[key] !== undefined) requireNumber(effect, key, effectPath, errors)
    }
    if (effect.tickMs !== undefined) requirePositiveNumber(effect, 'tickMs', effectPath, errors)
    if (effect.refresh !== undefined && !LAST_CHANCES_STATUS_REFRESH_MODES.includes(
      effect.refresh as typeof LAST_CHANCES_STATUS_REFRESH_MODES[number],
    )) {
      errors.push(`${effectPath}.refresh must be one of ${LAST_CHANCES_STATUS_REFRESH_MODES.join(', ')}`)
    }
  })
}

function validateAttackOverrides(value: unknown, path: string, errors: string[]): void {
  if (value === undefined) return
  const overrides = asRecord(value, path, errors)
  if (!overrides) return
  for (const key of [
    'damage',
    'cooldownMs',
    'range',
    'radius',
    'arcDegrees',
    'durationMs',
    'lingerMs',
    'projectileSpeed',
    'knockback',
    'recoveryMs',
    'rootMs',
    'invulnerabilityMs',
    'repeatIntervalMs',
  ] as const) {
    if (overrides[key] !== undefined) requireNumber(overrides, key, path, errors)
  }
  if (overrides.pierce !== undefined) requireInteger(overrides, 'pierce', path, errors)
  if (overrides.repeatHits !== undefined) requireInteger(overrides, 'repeatHits', path, errors, 1)
}

function validateCollider(value: unknown, path: string, errors: string[]): void {
  if (value === undefined) return
  const collider = asRecord(value, path, errors)
  if (!collider) return
  if (!LAST_CHANCES_COLLIDER_SHAPES.includes(
    collider.shape as typeof LAST_CHANCES_COLLIDER_SHAPES[number],
  )) {
    errors.push(`${path}.shape must be one of ${LAST_CHANCES_COLLIDER_SHAPES.join(', ')}`)
  }
  requireNumber(collider, 'traceMs', path, errors)
  if (collider.innerRange !== undefined) requireNumber(collider, 'innerRange', path, errors)
  if (collider.strictInnerRange !== undefined && typeof collider.strictInnerRange !== 'boolean') {
    errors.push(`${path}.strictInnerRange must be a boolean`)
  }
  if (collider.width !== undefined) requirePositiveNumber(collider, 'width', path, errors)
  if (collider.tickMs !== undefined) requirePositiveNumber(collider, 'tickMs', path, errors)
  if (collider.followsPlayer !== undefined && typeof collider.followsPlayer !== 'boolean') {
    errors.push(`${path}.followsPlayer must be a boolean`)
  }
  if (collider.rotationDegrees !== undefined
    && (typeof collider.rotationDegrees !== 'number' || !Number.isFinite(collider.rotationDegrees))) {
    errors.push(`${path}.rotationDegrees must be a finite number`)
  }
}

function validateCharge(value: unknown, path: string, errors: string[]): void {
  if (value === undefined) return
  const charge = asRecord(value, path, errors)
  if (!charge) return
  requirePositiveNumber(charge, 'maxMs', path, errors)
  if (!Array.isArray(charge.bands) || charge.bands.length === 0) {
    errors.push(`${path}.bands must be a non-empty array`)
    return
  }
  let previousMinimum = Number.NEGATIVE_INFINITY
  const bandIds = new Set<string>()
  charge.bands.forEach((bandValue, index) => {
    const bandPath = `${path}.bands[${index}]`
    const band = asRecord(bandValue, bandPath, errors)
    if (!band) return
    requireString(band, 'id', bandPath, errors)
    requireString(band, 'label', bandPath, errors)
    requireNumber(band, 'minMs', bandPath, errors)
    requireString(band, 'color', bandPath, errors)
    if (typeof band.id === 'string') {
      if (bandIds.has(band.id)) errors.push(`${bandPath}.id duplicates ${band.id}`)
      bandIds.add(band.id)
    }
    if (typeof band.minMs === 'number' && Number.isFinite(band.minMs)) {
      if (band.minMs <= previousMinimum) {
        errors.push(`${bandPath}.minMs must be strictly greater than the previous charge band`)
      }
      if (typeof charge.maxMs === 'number' && band.minMs > charge.maxMs) {
        errors.push(`${bandPath}.minMs must be <= ${path}.maxMs`)
      }
      previousMinimum = band.minMs
    }
    for (const key of [
      'damageMultiplier',
      'rangeMultiplier',
      'knockbackMultiplier',
      'durationMultiplier',
      'speedMultiplier',
    ] as const) {
      if (band[key] !== undefined) requireNumber(band, key, bandPath, errors)
    }
    validateAttackOverrides(band.overrides, `${bandPath}.overrides`, errors)
  })
}

function validateSweetSpot(value: unknown, path: string, errors: string[]): void {
  if (value === undefined) return
  const sweetSpot = asRecord(value, path, errors)
  if (!sweetSpot) return
  requireNumber(sweetSpot, 'minRange', path, errors)
  requirePositiveNumber(sweetSpot, 'damageMultiplier', path, errors)
  if (sweetSpot.maxRange !== undefined) requirePositiveNumber(sweetSpot, 'maxRange', path, errors)
  if (typeof sweetSpot.minRange === 'number' && typeof sweetSpot.maxRange === 'number'
    && sweetSpot.maxRange <= sweetSpot.minRange) {
    errors.push(`${path}.maxRange must be greater than minRange`)
  }
  if (sweetSpot.knockbackMultiplier !== undefined) {
    requireNumber(sweetSpot, 'knockbackMultiplier', path, errors)
  }
  if (sweetSpot.criticalMultiplier !== undefined) {
    requirePositiveNumber(sweetSpot, 'criticalMultiplier', path, errors)
  }
}

function validateTuning(value: unknown, path: string, errors: string[]): void {
  if (value === undefined) return
  const tuning = asRecord(value, path, errors)
  if (!tuning) return
  for (const [key, amount] of Object.entries(tuning)) {
    if (typeof amount !== 'number' || !Number.isFinite(amount)) {
      errors.push(`${path}.${key} must be a finite number`)
    }
  }
}

function validateAttack(
  value: unknown,
  path: string,
  errors: string[],
  schemaVersion: number,
): void {
  const record = asRecord(value, path, errors)
  if (!record) return
  requireString(record, 'name', path, errors)
  if (!LAST_CHANCES_ATTACK_KINDS.includes(record.kind as typeof LAST_CHANCES_ATTACK_KINDS[number])) {
    errors.push(`${path}.kind must be one of ${LAST_CHANCES_ATTACK_KINDS.join(', ')}`)
  }
  if (record.enabled !== undefined && typeof record.enabled !== 'boolean') {
    errors.push(`${path}.enabled must be a boolean`)
  }
  if (record.behavior !== undefined && !LAST_CHANCES_ATTACK_BEHAVIORS.includes(
    record.behavior as typeof LAST_CHANCES_ATTACK_BEHAVIORS[number],
  )) {
    errors.push(`${path}.behavior must be one of ${LAST_CHANCES_ATTACK_BEHAVIORS.join(', ')}`)
  }
  if (schemaVersion >= 3 && record.behavior === undefined) {
    errors.push(`${path}.behavior is required by schemaVersion ${schemaVersion}`)
  }
  if (schemaVersion >= 3 && record.enabled === false && record.behavior !== 'disabled') {
    errors.push(`${path}.behavior must be disabled when enabled is false`)
  }
  if (schemaVersion >= 3 && record.behavior === 'disabled' && record.enabled !== false) {
    errors.push(`${path}.enabled must be false when behavior is disabled`)
  }
  if (schemaVersion >= 3 && record.enabled !== false
    && record.behavior !== 'disabled' && record.collider === undefined) {
    errors.push(`${path}.collider is required for enabled schemaVersion ${schemaVersion} attacks`)
  }
  requireNumber(record, 'damage', path, errors)
  if (record.damageType !== undefined && !LAST_CHANCES_DAMAGE_TYPES.includes(
    record.damageType as typeof LAST_CHANCES_DAMAGE_TYPES[number],
  )) {
    errors.push(`${path}.damageType must be one of ${LAST_CHANCES_DAMAGE_TYPES.join(', ')}`)
  }
  requireNumber(record, 'cooldownMs', path, errors)
  requireNumber(record, 'range', path, errors)
  requireNumber(record, 'radius', path, errors)
  requireNumber(record, 'arcDegrees', path, errors)
  requireNumber(record, 'durationMs', path, errors)
  if (record.lingerMs !== undefined) requireNumber(record, 'lingerMs', path, errors)
  requireNumber(record, 'projectileSpeed', path, errors)
  requireInteger(record, 'pierce', path, errors)
  requireNumber(record, 'knockback', path, errors)
  requireString(record, 'color', path, errors)
  validateCollider(record.collider, `${path}.collider`, errors)
  validateCharge(record.charge, `${path}.charge`, errors)
  validateHitEffects(record.hitEffects, `${path}.hitEffects`, errors)
  for (const key of [
    'recoveryMs',
    'rootMs',
    'invulnerabilityMs',
    'repeatIntervalMs',
    'cooldownRefundMs',
  ] as const) {
    if (record[key] !== undefined) requireNumber(record, key, path, errors)
  }
  if (record.repeatHits !== undefined) requireInteger(record, 'repeatHits', path, errors, 1)
  if (typeof record.repeatHits === 'number' && record.repeatHits > 1
    && record.repeatIntervalMs === undefined) {
    errors.push(`${path}.repeatIntervalMs is required when repeatHits is greater than 1`)
  }
  if (record.resetCooldownOnKill !== undefined && typeof record.resetCooldownOnKill !== 'boolean') {
    errors.push(`${path}.resetCooldownOnKill must be a boolean`)
  }
  if (record.resourceCost !== undefined) requireNumber(record, 'resourceCost', path, errors)
  if (record.consumeAllResource !== undefined && typeof record.consumeAllResource !== 'boolean') {
    errors.push(`${path}.consumeAllResource must be a boolean`)
  }
  validateTuning(record.tuning, `${path}.tuning`, errors)
  validateSweetSpot(record.sweetSpot, `${path}.sweetSpot`, errors)
}

function validateAttackSet(
  value: unknown,
  path: string,
  errors: string[],
  schemaVersion: number,
): void {
  const attacks = asRecord(value, path, errors)
  if (!attacks) return
  for (const gesture of LAST_CHANCES_GESTURES) {
    validateAttack(attacks[gesture], `${path}.${gesture}`, errors, schemaVersion)
  }
  const tap = attacks.tap
  if (typeof tap === 'object' && tap !== null && !Array.isArray(tap)
    && (tap as UnknownRecord).cooldownMs !== 0) {
    errors.push(`${path}.tap.cooldownMs must be 0 because basic taps have no cooldown`)
  }
}

function validateTapCombo(
  value: unknown,
  path: string,
  errors: string[],
  required: boolean,
  schemaVersion: number,
): void {
  if (value === undefined && !required) return
  if (!Array.isArray(value) || value.length < 1) {
    errors.push(`${path} must contain at least one basic-combo follow-up`)
    return
  }
  value.forEach((attack, index) => {
    const attackPath = `${path}[${index}]`
    validateAttack(attack, attackPath, errors, schemaVersion)
    if (typeof attack === 'object' && attack !== null && !Array.isArray(attack)
      && (attack as UnknownRecord).cooldownMs !== 0) {
      errors.push(`${attackPath}.cooldownMs must be 0 because basic taps have no cooldown`)
    }
  })
}

function validateHazards(value: unknown, path: string, errors: string[]): void {
  if (value === undefined) return
  if (!Array.isArray(value)) {
    errors.push(`${path} must be an array`)
    return
  }
  const ids = new Set<string>()
  value.forEach((hazardValue, index) => {
    const hazardPath = `${path}[${index}]`
    const hazard = asRecord(hazardValue, hazardPath, errors)
    if (!hazard) return
    requireString(hazard, 'id', hazardPath, errors)
    requireString(hazard, 'name', hazardPath, errors)
    if (!LAST_CHANCES_HAZARD_KINDS.includes(
      hazard.kind as typeof LAST_CHANCES_HAZARD_KINDS[number],
    )) {
      errors.push(`${hazardPath}.kind must be one of ${LAST_CHANCES_HAZARD_KINDS.join(', ')}`)
    }
    for (const key of ['x', 'y', 'damage', 'mentalDamagePerSecond', 'phaseOffsetMs'] as const) {
      requireNumber(hazard, key, hazardPath, errors)
    }
    for (const key of ['width', 'height', 'cycleMs', 'activeMs'] as const) {
      requirePositiveNumber(hazard, key, hazardPath, errors)
    }
    requireString(hazard, 'color', hazardPath, errors)
    if (typeof hazard.activeMs === 'number' && typeof hazard.cycleMs === 'number'
      && hazard.activeMs > hazard.cycleMs) {
      errors.push(`${hazardPath}.activeMs must be <= cycleMs`)
    }
    if (typeof hazard.id === 'string') {
      if (ids.has(hazard.id)) errors.push(`${hazardPath}.id duplicates ${hazard.id}`)
      ids.add(hazard.id)
    }
  })
}

function validateInteraction(value: unknown, path: string, errors: string[]): void {
  if (value === undefined) return
  const interaction = asRecord(value, path, errors)
  if (!interaction) return
  requireString(interaction, 'title', path, errors)
  requireString(interaction, 'body', path, errors)
  if (!Array.isArray(interaction.choices) || interaction.choices.length === 0) {
    errors.push(`${path}.choices must be a non-empty array`)
    return
  }
  const ids = new Set<string>()
  interaction.choices.forEach((choiceValue, index) => {
    const choicePath = `${path}.choices[${index}]`
    const choice = asRecord(choiceValue, choicePath, errors)
    if (!choice) return
    requireString(choice, 'id', choicePath, errors)
    requireString(choice, 'label', choicePath, errors)
    requireString(choice, 'description', choicePath, errors)
    const effect = asRecord(choice.effect, `${choicePath}.effect`, errors)
    if (effect) {
      if (effect.chanceCost !== undefined) requireInteger(effect, 'chanceCost', `${choicePath}.effect`, errors)
      if (effect.hp !== undefined) {
        const hp = effect.hp
        if (typeof hp !== 'number' || !Number.isFinite(hp)) {
          errors.push(`${choicePath}.effect.hp must be a finite number`)
        }
      }
      if (effect.mentalHealth !== undefined) {
        const mental = effect.mentalHealth
        if (typeof mental !== 'number' || !Number.isFinite(mental)) {
          errors.push(`${choicePath}.effect.mentalHealth must be a finite number`)
        }
      }
      if (effect.stats !== undefined) {
        const stats = asRecord(effect.stats, `${choicePath}.effect.stats`, errors)
        if (stats) {
          for (const key of ['maxHp', 'maxMentalHealth', 'attackPower', 'moveSpeed', 'armor'] as const) {
            if (stats[key] !== undefined
              && (typeof stats[key] !== 'number' || !Number.isFinite(stats[key]))) {
              errors.push(`${choicePath}.effect.stats.${key} must be a finite number`)
            }
          }
        }
      }
      if (effect.primaryWeaponId !== undefined) {
        requireString(effect, 'primaryWeaponId', `${choicePath}.effect`, errors)
      }
      if (effect.secondaryWeaponId !== undefined && effect.secondaryWeaponId !== null) {
        requireString(effect, 'secondaryWeaponId', `${choicePath}.effect`, errors)
      }
    }
    if (typeof choice.id === 'string') {
      if (ids.has(choice.id)) errors.push(`${choicePath}.id duplicates ${choice.id}`)
      ids.add(choice.id)
    }
  })
}

function validateRooms(value: unknown, errors: string[], requireSpawnLayouts: boolean): Set<string> {
  const ids = new Set<string>()
  if (!Array.isArray(value) || value.length === 0) {
    errors.push('rooms must be a non-empty array')
    return ids
  }
  value.forEach((item, index) => {
    const path = `rooms[${index}]`
    const room = asRecord(item, path, errors)
    if (!room) return
    requireString(room, 'id', path, errors)
    requireString(room, 'name', path, errors)
    if (!['combat', 'chest', 'rest', 'event', 'merchant', 'trap', 'puzzle'].includes(String(room.archetype))) {
      errors.push(`${path}.archetype must be combat, chest, rest, event, merchant, trap, or puzzle`)
    }
    requirePositiveNumber(room, 'width', path, errors)
    requirePositiveNumber(room, 'height', path, errors)
    validateVector(room.playerSpawn, `${path}.playerSpawn`, errors)
    if (room.enemySpawns !== undefined && (!Array.isArray(room.enemySpawns) || room.enemySpawns.length === 0)) {
      errors.push(`${path}.enemySpawns must be a non-empty array when provided`)
    } else if (Array.isArray(room.enemySpawns)) {
      room.enemySpawns.forEach((spawn, spawnIndex) => {
        validateVector(spawn, `${path}.enemySpawns[${spawnIndex}]`, errors)
      })
    }
    if (requireSpawnLayouts && room.spawnLayouts === undefined) {
      errors.push(`${path}.spawnLayouts is required by schemaVersion 2 or newer`)
    }
    if (room.spawnLayouts !== undefined && (!Array.isArray(room.spawnLayouts) || room.spawnLayouts.length < 2)) {
      errors.push(`${path}.spawnLayouts must contain at least two named layouts when provided`)
    } else if (Array.isArray(room.spawnLayouts)) {
      const layoutIds = new Set<string>()
      room.spawnLayouts.forEach((layoutValue, layoutIndex) => {
        const layoutPath = `${path}.spawnLayouts[${layoutIndex}]`
        const layout = asRecord(layoutValue, layoutPath, errors)
        if (!layout) return
        requireString(layout, 'id', layoutPath, errors)
        requireString(layout, 'name', layoutPath, errors)
        if (!Array.isArray(layout.enemySpawns) || layout.enemySpawns.length === 0) {
          errors.push(`${layoutPath}.enemySpawns must be a non-empty array`)
        } else {
          layout.enemySpawns.forEach((spawn, spawnIndex) => {
            validateVector(spawn, `${layoutPath}.enemySpawns[${spawnIndex}]`, errors)
          })
        }
        if (typeof layout.id === 'string') {
          if (layoutIds.has(layout.id)) errors.push(`${layoutPath}.id duplicates ${layout.id}`)
          layoutIds.add(layout.id)
        }
      })
    }
    if (room.enemySpawns === undefined && room.spawnLayouts === undefined) {
      errors.push(`${path} must define enemySpawns or spawnLayouts`)
    }
    if (!Array.isArray(room.obstacles)) {
      errors.push(`${path}.obstacles must be an array`)
    } else {
      room.obstacles.forEach((itemObstacle, obstacleIndex) => {
        const obstaclePath = `${path}.obstacles[${obstacleIndex}]`
        const obstacle = asRecord(itemObstacle, obstaclePath, errors)
        if (!obstacle) return
        requireNumber(obstacle, 'x', obstaclePath, errors)
        requireNumber(obstacle, 'y', obstaclePath, errors)
        requirePositiveNumber(obstacle, 'width', obstaclePath, errors)
        requirePositiveNumber(obstacle, 'height', obstaclePath, errors)
        requireNumber(obstacle, 'elevation', obstaclePath, errors)
      })
    }
    validateHazards(room.hazards, `${path}.hazards`, errors)
    validateInteraction(room.interaction, `${path}.interaction`, errors)
    if (typeof room.id === 'string') {
      if (ids.has(room.id)) errors.push(`${path}.id duplicates ${room.id}`)
      ids.add(room.id)
    }
  })
  return ids
}

function validateEnemies(value: unknown, errors: string[], schemaVersion: number): Set<string> {
  const ids = new Set<string>()
  if (!Array.isArray(value) || value.length === 0) {
    errors.push('enemies must be a non-empty array')
    return ids
  }
  value.forEach((item, index) => {
    const path = `enemies[${index}]`
    const enemy = asRecord(item, path, errors)
    if (!enemy) return
    requireString(enemy, 'id', path, errors)
    requireString(enemy, 'name', path, errors)
    for (const key of ['maxHp', 'radius', 'moveSpeed', 'visionRange', 'noticeMs', 'alertPauseMs', 'attackRange',
      'attackCooldownMs', 'attackWindupMs'] as const) {
      requirePositiveNumber(enemy, key, path, errors)
    }
    if (enemy.armor !== undefined) requireNumber(enemy, 'armor', path, errors)
    if (enemy.dodge !== undefined) {
      requireNumber(enemy, 'dodge', path, errors)
      if (typeof enemy.dodge === 'number' && enemy.dodge > 1) {
        errors.push(`${path}.dodge must be <= 1`)
      }
    }
    if (schemaVersion >= 2 || enemy.idleTurnRadiansPerSecond !== undefined) {
      requireNumber(enemy, 'idleTurnRadiansPerSecond', path, errors)
    }
    if (schemaVersion >= 2 || enemy.preferredAttackRangeRatio !== undefined) {
      requirePositiveNumber(enemy, 'preferredAttackRangeRatio', path, errors)
      if (typeof enemy.preferredAttackRangeRatio === 'number'
        && enemy.preferredAttackRangeRatio > 1) {
        errors.push(`${path}.preferredAttackRangeRatio must be <= 1`)
      }
    }
    if (enemy.role !== undefined
      && !LAST_CHANCES_ENEMY_ROLES.includes(enemy.role as typeof LAST_CHANCES_ENEMY_ROLES[number])) {
      errors.push(`${path}.role must be one of ${LAST_CHANCES_ENEMY_ROLES.join(', ')}`)
    }
    if (enemy.attackKind !== undefined
      && !LAST_CHANCES_ENEMY_ATTACK_KINDS.includes(
        enemy.attackKind as typeof LAST_CHANCES_ENEMY_ATTACK_KINDS[number],
      )) {
      errors.push(`${path}.attackKind must be one of ${LAST_CHANCES_ENEMY_ATTACK_KINDS.join(', ')}`)
    }
    for (const key of ['attackRadius', 'projectileSpeed', 'leapDistance', 'leapDurationMs',
      'targetLockMs', 'parryWindowMs'] as const) {
      if (enemy[key] !== undefined) requireNumber(enemy, key, path, errors)
    }
    if (enemy.invisibleUntilAlerted !== undefined && typeof enemy.invisibleUntilAlerted !== 'boolean') {
      errors.push(`${path}.invisibleUntilAlerted must be a boolean`)
    }
    if (enemy.bossPhases !== undefined) {
      if (!Array.isArray(enemy.bossPhases) || enemy.bossPhases.length === 0) {
        errors.push(`${path}.bossPhases must be a non-empty array`)
      } else {
        enemy.bossPhases.forEach((phaseValue, phaseIndex) => {
          const phasePath = `${path}.bossPhases[${phaseIndex}]`
          const phase = asRecord(phaseValue, phasePath, errors)
          if (!phase) return
          requireString(phase, 'name', phasePath, errors)
          requireNumber(phase, 'minimumHealthRatio', phasePath, errors)
          if (typeof phase.minimumHealthRatio === 'number' && phase.minimumHealthRatio > 1) {
            errors.push(`${phasePath}.minimumHealthRatio must be <= 1`)
          }
          if (!LAST_CHANCES_ENEMY_ATTACK_KINDS.includes(
            phase.attackKind as typeof LAST_CHANCES_ENEMY_ATTACK_KINDS[number],
          )) {
            errors.push(`${phasePath}.attackKind must be one of ${LAST_CHANCES_ENEMY_ATTACK_KINDS.join(', ')}`)
          }
          for (const key of ['attackRange', 'attackRadius', 'attackDamage', 'attackCooldownMs',
            'attackWindupMs'] as const) {
            requireNumber(phase, key, phasePath, errors)
          }
          for (const key of ['projectileSpeed', 'leapDistance', 'leapDurationMs',
            'targetLockMs', 'parryWindowMs'] as const) {
            if (phase[key] !== undefined) requireNumber(phase, key, phasePath, errors)
          }
        })
      }
    }
    if (enemy.attackKind === 'zone' && enemy.zone === undefined) {
      errors.push(`${path}.zone is required when attackKind is zone`)
    }
    if (enemy.zone !== undefined) {
      const zonePath = `${path}.zone`
      const zone = asRecord(enemy.zone, zonePath, errors)
      if (zone) {
        if (!Array.isArray(zone.shapes) || zone.shapes.length === 0
          || !zone.shapes.every(shape => LAST_CHANCES_ZONE_SHAPES.includes(
            shape as typeof LAST_CHANCES_ZONE_SHAPES[number],
          ))) {
          errors.push(`${zonePath}.shapes must be a non-empty array of ${LAST_CHANCES_ZONE_SHAPES.join(', ')}`)
        }
        requirePositiveNumber(zone, 'escapeMs', zonePath, errors)
        requirePositiveNumber(zone, 'size', zonePath, errors)
        requirePositiveNumber(zone, 'damageMaxHpRatio', zonePath, errors)
        if (typeof zone.damageMaxHpRatio === 'number' && zone.damageMaxHpRatio > 1) {
          errors.push(`${zonePath}.damageMaxHpRatio must be <= 1`)
        }
      }
    }
    if (enemy.swarm !== undefined) {
      const swarmPath = `${path}.swarm`
      const swarm = asRecord(enemy.swarm, swarmPath, errors)
      if (swarm) {
        requireInteger(swarm, 'total', swarmPath, errors, 1)
        requireInteger(swarm, 'initialBurst', swarmPath, errors, 1)
        requirePositiveNumber(swarm, 'spawnIntervalMs', swarmPath, errors)
        if (typeof swarm.total === 'number' && typeof swarm.initialBurst === 'number'
          && swarm.initialBurst > swarm.total) {
          errors.push(`${swarmPath}.initialBurst must be <= total`)
        }
      }
    }
    requireNumber(enemy, 'visionAngleDegrees', path, errors)
    requireNumber(enemy, 'attackDamage', path, errors)
    requireNumber(enemy, 'mentalPressurePerSecond', path, errors)
    requireString(enemy, 'color', path, errors)
    validateTuning(enemy.tuning, `${path}.tuning`, errors)
    if (typeof enemy.id === 'string') {
      if (ids.has(enemy.id)) errors.push(`${path}.id duplicates ${enemy.id}`)
      ids.add(enemy.id)
    }
  })
  return ids
}

function validateTiers(
  value: unknown,
  roomIds: Set<string>,
  enemyIds: Set<string>,
  errors: string[],
): void {
  if (!Array.isArray(value) || value.length === 0) {
    errors.push('progression.tiers must be a non-empty ordered array')
    return
  }
  const ids = new Set<string>()
  value.forEach((item, index) => {
    const path = `progression.tiers[${index}]`
    const tier = asRecord(item, path, errors)
    if (!tier) return
    requireString(tier, 'id', path, errors)
    requireString(tier, 'label', path, errors)
    if (tier.kind !== 'normal' && tier.kind !== 'boss') {
      errors.push(`${path}.kind must be normal or boss`)
    }
    requireInteger(tier, 'nodeCount', path, errors, 1)
    requireInteger(tier, 'deathCost', path, errors, 1)
    requireString(tier, 'accent', path, errors)
    validateStats(tier.erosion, `${path}.erosion`, errors, true)
    if (!Array.isArray(tier.enemyCount) || tier.enemyCount.length !== 2
      || !tier.enemyCount.every(count => Number.isInteger(count) && count >= 0)
      || (tier.enemyCount[0] as number) > (tier.enemyCount[1] as number)) {
      errors.push(`${path}.enemyCount must be [minimum, maximum] non-negative integers`)
    }
    if (!Array.isArray(tier.enemyPool) || tier.enemyPool.length === 0) {
      errors.push(`${path}.enemyPool must be a non-empty array`)
    } else {
      tier.enemyPool.forEach((poolItem, poolIndex) => {
        const poolPath = `${path}.enemyPool[${poolIndex}]`
        const pool = asRecord(poolItem, poolPath, errors)
        if (!pool) return
        requireString(pool, 'enemyId', poolPath, errors)
        requirePositiveNumber(pool, 'weight', poolPath, errors)
        if (typeof pool.enemyId === 'string' && !enemyIds.has(pool.enemyId)) {
          errors.push(`${poolPath}.enemyId references unknown enemy ${pool.enemyId}`)
        }
      })
    }
    if (tier.guaranteedEnemyIds !== undefined) {
      requireStringArray(tier, 'guaranteedEnemyIds', path, errors)
      if (Array.isArray(tier.guaranteedEnemyIds)) {
        tier.guaranteedEnemyIds.forEach((enemyId) => {
          if (typeof enemyId === 'string' && !enemyIds.has(enemyId)) {
            errors.push(`${path}.guaranteedEnemyIds references unknown enemy ${enemyId}`)
          }
        })
      }
    }
    requireStringArray(tier, 'roomTemplateIds', path, errors)
    if (Array.isArray(tier.roomTemplateIds)) {
      tier.roomTemplateIds.forEach((roomId) => {
        if (typeof roomId === 'string' && !roomIds.has(roomId)) {
          errors.push(`${path}.roomTemplateIds references unknown room ${roomId}`)
        }
      })
    }
    if (typeof tier.id === 'string') {
      if (ids.has(tier.id)) errors.push(`${path}.id duplicates ${tier.id}`)
      ids.add(tier.id)
    }
  })
  const terminal = value[value.length - 1]
  if (typeof terminal !== 'object' || terminal === null || Array.isArray(terminal)
    || (terminal as UnknownRecord).kind !== 'boss') {
    errors.push('progression.tiers must end with a boss tier')
  }
}

function inferredEquipMode(weapon: UnknownRecord): string {
  if (typeof weapon.equipMode === 'string') return weapon.equipMode
  return weapon.hand === 'right' ? 'secondaryOnly' : 'primaryOnly'
}

function validateWeaponResource(value: unknown, path: string, errors: string[]): void {
  if (value === undefined) return
  const resource = asRecord(value, path, errors)
  if (!resource) return
  if (!LAST_CHANCES_WEAPON_RESOURCE_KINDS.includes(
    resource.kind as typeof LAST_CHANCES_WEAPON_RESOURCE_KINDS[number],
  )) {
    errors.push(`${path}.kind must be one of ${LAST_CHANCES_WEAPON_RESOURCE_KINDS.join(', ')}`)
  }
  requirePositiveNumber(resource, 'max', path, errors)
  requireNumber(resource, 'initial', path, errors)
  if (typeof resource.max === 'number' && typeof resource.initial === 'number'
    && resource.initial > resource.max) {
    errors.push(`${path}.initial must be <= max`)
  }
  if (resource.label !== undefined) requireString(resource, 'label', path, errors)
  if (resource.color !== undefined) requireString(resource, 'color', path, errors)
}

function validateAugmentHooks(value: unknown, path: string, errors: string[]): void {
  if (value === undefined) return
  const hooks = asRecord(value, path, errors)
  if (!hooks) return
  for (const [augment, hookValue] of Object.entries(hooks)) {
    const hookPath = `${path}.${augment}`
    if (!LAST_CHANCES_AUGMENTS.includes(augment as typeof LAST_CHANCES_AUGMENTS[number])) {
      errors.push(`${hookPath} uses unknown augment ${augment}`)
      continue
    }
    const hook = asRecord(hookValue, hookPath, errors)
    if (!hook) continue
    if (hook.behaviors !== undefined) {
      if (!Array.isArray(hook.behaviors) || hook.behaviors.length === 0) {
        errors.push(`${hookPath}.behaviors must be a non-empty array`)
      } else {
        for (const [behaviorIndex, behavior] of hook.behaviors.entries()) {
          if (!LAST_CHANCES_ATTACK_BEHAVIORS.includes(
            behavior as typeof LAST_CHANCES_ATTACK_BEHAVIORS[number],
          )) {
            errors.push(`${hookPath}.behaviors[${behaviorIndex}] uses unknown behavior ${String(behavior)}`)
          }
        }
      }
    }
    if (hook.damageMultiplier !== undefined) {
      requireNumber(hook, 'damageMultiplier', hookPath, errors)
    }
    validateHitEffects(hook.hitEffects, `${hookPath}.hitEffects`, errors)
  }
}

function enabledAttackGestures(value: unknown): Set<LastChancesGesture> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return new Set()
  const attacks = value as UnknownRecord
  return new Set(LAST_CHANCES_GESTURES.filter((gesture) => {
    const attackValue = attacks[gesture]
    if (typeof attackValue !== 'object' || attackValue === null || Array.isArray(attackValue)) return false
    const attack = attackValue as UnknownRecord
    return attack.enabled !== false && attack.behavior !== 'disabled'
  }))
}

function validateAttackSetControls(
  value: unknown,
  attacksValue: unknown,
  path: string,
  gatePositions: number[],
  errors: string[],
): void {
  const controls = asRecord(value, path, errors)
  if (!controls) return
  requireString(controls, 'role', path, errors)
  const enabledGestures = enabledAttackGestures(attacksValue)
  const attacks = typeof attacksValue === 'object' && attacksValue !== null && !Array.isArray(attacksValue)
    ? attacksValue as UnknownRecord
    : null
  const holdAttack = attacks && typeof attacks.hold === 'object'
    && attacks.hold !== null && !Array.isArray(attacks.hold)
    ? attacks.hold as UnknownRecord
    : null
  const holdCharge = holdAttack && typeof holdAttack.charge === 'object'
    && holdAttack.charge !== null && !Array.isArray(holdAttack.charge)
    ? holdAttack.charge as UnknownRecord
    : null
  const holdChargeBandIds = new Set(
    Array.isArray(holdCharge?.bands)
      ? holdCharge.bands.flatMap((band) => (
          typeof band === 'object' && band !== null && !Array.isArray(band)
            && typeof (band as UnknownRecord).id === 'string'
            ? [(band as UnknownRecord).id as string]
            : []
        ))
      : [],
  )

  const mylorik = asRecord(controls.mylorik, `${path}.mylorik`, errors)
  if (mylorik) {
    if (!Array.isArray(mylorik.activations)) {
      errors.push(`${path}.mylorik.activations must be an array`)
    } else {
      const slotCounts = new Map<LastChancesGesture, number>()
      const activationKeys = new Set<string>()
      let unconditionalStrikeCount = 0
      mylorik.activations.forEach((activationValue, index) => {
        const activationPath = `${path}.mylorik.activations[${index}]`
        const activation = asRecord(activationValue, activationPath, errors)
        if (!activation) return
        const gesture = activation.gesture as LastChancesGesture
        if (!LAST_CHANCES_GESTURES.includes(gesture)) {
          errors.push(`${activationPath}.gesture must be one of ${LAST_CHANCES_GESTURES.join(', ')}`)
        } else {
          slotCounts.set(gesture, (slotCounts.get(gesture) ?? 0) + 1)
          if (!enabledGestures.has(gesture)) {
            errors.push(`${activationPath}.gesture routes disabled slot ${gesture}`)
          }
        }
        if (!LAST_CHANCES_CONTROL_INTENTS.includes(
          activation.intent as typeof LAST_CHANCES_CONTROL_INTENTS[number],
        )) {
          errors.push(`${activationPath}.intent must be one of ${LAST_CHANCES_CONTROL_INTENTS.join(', ')}`)
        }
        if (!LAST_CHANCES_CONTROL_PHASES.includes(
          activation.phase as typeof LAST_CHANCES_CONTROL_PHASES[number],
        )) {
          errors.push(`${activationPath}.phase must be one of ${LAST_CHANCES_CONTROL_PHASES.join(', ')}`)
        }
        if (activation.context !== undefined && !LAST_CHANCES_CONTROL_CONTEXTS.includes(
          activation.context as typeof LAST_CHANCES_CONTROL_CONTEXTS[number],
        )) {
          errors.push(`${activationPath}.context must be one of ${LAST_CHANCES_CONTROL_CONTEXTS.join(', ')}`)
        }
        requireInteger(activation, 'priority', activationPath, errors)
        if (typeof activation.priority === 'number' && activation.priority > MAX_CONTROL_EXPIRY_MS) {
          errors.push(`${activationPath}.priority must be <= ${MAX_CONTROL_EXPIRY_MS}`)
        }
        if (activation.legacyOnlyReason !== undefined) {
          requireString(activation, 'legacyOnlyReason', activationPath, errors)
        }
        const activationKey = [
          String(activation.intent),
          String(activation.phase),
          activation.context === undefined ? '*' : String(activation.context),
          String(activation.priority),
        ].join('|')
        if (activationKeys.has(activationKey)) {
          errors.push(`${activationPath} duplicates activation ${activationKey}`)
        }
        activationKeys.add(activationKey)
        if (gesture === 'tap' && activation.intent === 'strike' && activation.phase === 'press'
          && activation.context === undefined) {
          unconditionalStrikeCount += 1
        }
      })
      LAST_CHANCES_GESTURES.forEach((gesture) => {
        const expected = enabledGestures.has(gesture) ? 1 : 0
        const actual = slotCounts.get(gesture) ?? 0
        if (actual !== expected) {
          errors.push(`${path}.mylorik must route ${gesture} exactly ${expected} time(s); found ${actual}`)
        }
      })
      if (unconditionalStrikeCount !== 1) {
        errors.push(`${path}.mylorik must define exactly one unconditional instant tap strike`)
      }
    }
  }

  const dualsense = asRecord(controls.dualsense, `${path}.dualsense`, errors)
  if (!dualsense) return
  requireString(dualsense, 'triggerRole', `${path}.dualsense`, errors)
  if (dualsense.instantGesture !== 'tap') {
    errors.push(`${path}.dualsense.instantGesture must be tap`)
  }
  if (!enabledGestures.has(dualsense.instantGesture as LastChancesGesture)) {
    errors.push(`${path}.dualsense.instantGesture must reference an enabled action`)
  }
  if (!Array.isArray(dualsense.nodes)) {
    errors.push(`${path}.dualsense.nodes must be an array`)
    return
  }

  const nodes = new Map<string, UnknownRecord>()
  const dualSenseSlotCounts = new Map<LastChancesGesture, number>()
  if (dualsense.instantGesture === 'tap') dualSenseSlotCounts.set('tap', 1)
  dualsense.nodes.forEach((nodeValue, index) => {
    const nodePath = `${path}.dualsense.nodes[${index}]`
    const node = asRecord(nodeValue, nodePath, errors)
    if (!node) return
    requireString(node, 'id', nodePath, errors)
    if (typeof node.id === 'string') {
      if (nodes.has(node.id)) errors.push(`${nodePath}.id duplicates ${node.id}`)
      else nodes.set(node.id, node)
    }
    const gesture = node.gesture as LastChancesGesture
    if (!LAST_CHANCES_GESTURES.includes(gesture)) {
      errors.push(`${nodePath}.gesture must be one of ${LAST_CHANCES_GESTURES.join(', ')}`)
    } else {
      dualSenseSlotCounts.set(gesture, (dualSenseSlotCounts.get(gesture) ?? 0) + 1)
      if (!enabledGestures.has(gesture)) errors.push(`${nodePath}.gesture routes disabled slot ${gesture}`)
    }
    if (!LAST_CHANCES_CONTROL_CONTEXTS.includes(
      node.entryContext as typeof LAST_CHANCES_CONTROL_CONTEXTS[number],
    )) {
      errors.push(`${nodePath}.entryContext must be one of ${LAST_CHANCES_CONTROL_CONTEXTS.join(', ')}`)
    }
    validateUnitNumber(node, 'activationThreshold', nodePath, errors)
    if (typeof node.activationThreshold === 'number'
      && !gatePositions.some(gate => Math.abs(gate - (node.activationThreshold as number)) < 1e-9)) {
      errors.push(`${nodePath}.activationThreshold must match an input.dualsense gate position`)
    }
    if (node.dispatch !== 'press' && node.dispatch !== 'release') {
      errors.push(`${nodePath}.dispatch must be press or release`)
    }
    if (node.holdBehavior !== 'none' && node.holdBehavior !== 'charge'
      && node.holdBehavior !== 'channel') {
      errors.push(`${nodePath}.holdBehavior must be none, charge, or channel`)
    }
    if (node.releaseBehavior !== 'dispatch' && node.releaseBehavior !== 'cancel') {
      errors.push(`${nodePath}.releaseBehavior must be dispatch or cancel`)
    }
    if (node.cancel !== 'release' && node.cancel !== 'expiry') {
      errors.push(`${nodePath}.cancel must be release or expiry`)
    }
    requirePositiveNumber(node, 'expiryMs', nodePath, errors)
    if (typeof node.expiryMs === 'number' && node.expiryMs > MAX_CONTROL_EXPIRY_MS) {
      errors.push(`${nodePath}.expiryMs must be <= ${MAX_CONTROL_EXPIRY_MS}`)
    }
    if (!LAST_CHANCES_TACTILE_PROFILES.includes(
      node.tactileProfile as LastChancesTactileProfile,
    )) {
      errors.push(`${nodePath}.tactileProfile must be one of ${LAST_CHANCES_TACTILE_PROFILES.join(', ')}`)
    }
    if (node.requiredChargeBandId !== undefined
      && (typeof node.requiredChargeBandId !== 'string'
        || !holdChargeBandIds.has(node.requiredChargeBandId))) {
      errors.push(`${nodePath}.requiredChargeBandId must reference an existing hold charge band`)
    }
    if (!Array.isArray(node.next) || node.next.some(next => typeof next !== 'string')) {
      errors.push(`${nodePath}.next must be a string array`)
    } else if (new Set(node.next).size !== node.next.length) {
      errors.push(`${nodePath}.next must not contain duplicates`)
    }
    if (node.adaptiveOverride !== undefined) {
      validateAdaptiveProfile(
        node.adaptiveOverride,
        `${nodePath}.adaptiveOverride`,
        MAX_FEEDBACK_DURATION_MS,
        errors,
        true,
      )
    }
  })

  LAST_CHANCES_GESTURES.forEach((gesture) => {
    const expected = enabledGestures.has(gesture) ? 1 : 0
    const actual = dualSenseSlotCounts.get(gesture) ?? 0
    if (actual !== expected) {
      errors.push(`${path}.dualsense must route ${gesture} exactly ${expected} time(s); found ${actual}`)
    }
  })

  const startNodeId = dualsense.startNodeId
  if (nodes.size === 0) {
    if (startNodeId !== null) errors.push(`${path}.dualsense.startNodeId must be null when nodes is empty`)
    return
  }
  if (typeof startNodeId !== 'string' || !nodes.has(startNodeId)) {
    errors.push(`${path}.dualsense.startNodeId must reference a combo node`)
    return
  }
  nodes.forEach((node, id) => {
    if (!Array.isArray(node.next)) return
    const branchKeys = new Set<string>()
    node.next.forEach((nextId) => {
      if (typeof nextId === 'string' && !nodes.has(nextId)) {
        errors.push(`${path}.dualsense node ${id} references unknown next node ${nextId}`)
        return
      }
      if (typeof nextId === 'string') {
        const nextNode = nodes.get(nextId)
        if (!nextNode) return
        const branchKey = `${String(nextNode.entryContext)}|${String(nextNode.activationThreshold)}`
        if (branchKeys.has(branchKey)) {
          errors.push(`${path}.dualsense node ${id} has ambiguous branch ${branchKey}`)
        }
        branchKeys.add(branchKey)
      }
    })
  })

  const reachable = new Set<string>()
  const states = new Map<string, 0 | 1 | 2>()
  let foundCycle = false
  const visit = (nodeId: string): void => {
    if (states.get(nodeId) === 1) {
      foundCycle = true
      return
    }
    if (states.get(nodeId) === 2) return
    states.set(nodeId, 1)
    reachable.add(nodeId)
    const node = nodes.get(nodeId)
    if (node && Array.isArray(node.next)) {
      node.next.forEach((nextId) => {
        if (typeof nextId === 'string' && nodes.has(nextId)) visit(nextId)
      })
    }
    states.set(nodeId, 2)
  }
  visit(startNodeId)
  if (foundCycle) errors.push(`${path}.dualsense combo graph must be acyclic`)
  nodes.forEach((_node, nodeId) => {
    if (!reachable.has(nodeId)) errors.push(`${path}.dualsense combo node ${nodeId} is unreachable`)
  })
}

function validateWeapons(
  value: unknown,
  loadoutValue: unknown,
  errors: string[],
  schemaVersion: number,
  dualSenseGatePositions: number[] = [],
): void {
  if (!Array.isArray(value) || value.length === 0) {
    errors.push('weapons must be a non-empty array')
    return
  }
  const hasLoadout = loadoutValue !== undefined
  const ids = new Set<string>()
  const hands = new Set<string>()
  const catalog = new Map<string, UnknownRecord>()
  value.forEach((item, index) => {
    const path = `weapons[${index}]`
    const weapon = asRecord(item, path, errors)
    if (!weapon) return
    requireString(weapon, 'id', path, errors)
    requireString(weapon, 'name', path, errors)
    if (weapon.description !== undefined) requireString(weapon, 'description', path, errors)
    if (weapon.chanceCost !== undefined) requireInteger(weapon, 'chanceCost', path, errors)
    if (weapon.corpseBound !== undefined && typeof weapon.corpseBound !== 'boolean') {
      errors.push(`${path}.corpseBound must be a boolean`)
    }
    if (weapon.trait !== undefined && !LAST_CHANCES_WEAPON_TRAITS.includes(
      weapon.trait as typeof LAST_CHANCES_WEAPON_TRAITS[number],
    )) {
      errors.push(`${path}.trait must be one of ${LAST_CHANCES_WEAPON_TRAITS.join(', ')}`)
    }
    if (schemaVersion >= 3 && weapon.trait === undefined) {
      errors.push(`${path}.trait is required by schemaVersion ${schemaVersion}`)
    }
    validateTuning(weapon.tuning, `${path}.tuning`, errors)
    validateWeaponResource(weapon.resource, `${path}.resource`, errors)
    if (weapon.defaultAugment !== undefined && !LAST_CHANCES_AUGMENTS.includes(
      weapon.defaultAugment as typeof LAST_CHANCES_AUGMENTS[number],
    )) {
      errors.push(`${path}.defaultAugment must be one of ${LAST_CHANCES_AUGMENTS.join(', ')}`)
    }
    validateAugmentHooks(weapon.augmentHooks, `${path}.augmentHooks`, errors)
    if (weapon.hand !== undefined
      && !LAST_CHANCES_HANDS.includes(weapon.hand as typeof LAST_CHANCES_HANDS[number])) {
      errors.push(`${path}.hand must be left or right`)
    }
    if (schemaVersion >= 2 && weapon.hand !== undefined) {
      errors.push(`${path}.hand is legacy-only; schemaVersion ${schemaVersion} uses loadout and equipMode`)
    }
    if (weapon.equipMode !== undefined
      && !LAST_CHANCES_EQUIP_MODES.includes(weapon.equipMode as typeof LAST_CHANCES_EQUIP_MODES[number])) {
      errors.push(`${path}.equipMode must be one of ${LAST_CHANCES_EQUIP_MODES.join(', ')}`)
    }
    if (schemaVersion >= 2 && weapon.equipMode === undefined) {
      errors.push(`${path}.equipMode is required by schemaVersion ${schemaVersion}`)
    }
    validateAttackSet(weapon.attacks, `${path}.attacks`, errors, schemaVersion)
    if (schemaVersion >= 4) {
      const controls = asRecord(weapon.controls, `${path}.controls`, errors)
      if (controls) {
        validateAttackSetControls(
          controls.primary,
          weapon.attacks,
          `${path}.controls.primary`,
          dualSenseGatePositions,
          errors,
        )
        if (weapon.secondaryAttacks !== undefined) {
          validateAttackSetControls(
            controls.secondary,
            weapon.secondaryAttacks,
            `${path}.controls.secondary`,
            dualSenseGatePositions,
            errors,
          )
        } else if (controls.secondary !== undefined) {
          errors.push(`${path}.controls.secondary requires secondaryAttacks`)
        }
      }
    }
    validateTapCombo(weapon.tapCombo, `${path}.tapCombo`, errors, schemaVersion >= 2, schemaVersion)
    if (weapon.secondaryAttacks !== undefined) {
      validateAttackSet(weapon.secondaryAttacks, `${path}.secondaryAttacks`, errors, schemaVersion)
    }
    const equipMode = inferredEquipMode(weapon)
    if ((equipMode === 'twoHanded' || equipMode === 'hybrid') && weapon.secondaryAttacks === undefined) {
      errors.push(`${path}.secondaryAttacks is required for ${equipMode} equipment`)
    }
    validateTapCombo(
      weapon.secondaryTapCombo,
      `${path}.secondaryTapCombo`,
      errors,
      schemaVersion >= 2 && (equipMode === 'twoHanded' || equipMode === 'hybrid'),
      schemaVersion,
    )
    if (typeof weapon.id === 'string') {
      if (ids.has(weapon.id)) errors.push(`${path}.id duplicates ${weapon.id}`)
      ids.add(weapon.id)
      catalog.set(weapon.id, weapon)
    }
    if (!hasLoadout && typeof weapon.hand === 'string') {
      if (hands.has(weapon.hand)) errors.push(`${path}.hand duplicates ${weapon.hand}`)
      hands.add(weapon.hand)
    }
  })

  if (!hasLoadout) {
    if (schemaVersion >= 2) {
      errors.push(`loadout is required by schemaVersion ${schemaVersion}`)
    } else {
      if (value.length !== 2) errors.push('legacy weapons without loadout must contain exactly two definitions')
      for (const hand of LAST_CHANCES_HANDS) {
        if (!hands.has(hand)) errors.push(`weapons must define the ${hand} hand`)
      }
    }
    return
  }

  const loadout = asRecord(loadoutValue, 'loadout', errors)
  if (!loadout) return
  requireString(loadout, 'primaryWeaponId', 'loadout', errors)
  if (loadout.secondaryWeaponId !== null
    && (typeof loadout.secondaryWeaponId !== 'string' || loadout.secondaryWeaponId.trim().length === 0)) {
    errors.push('loadout.secondaryWeaponId must be a non-empty string or null')
  }
  for (const key of ['primaryAugment', 'secondaryAugment'] as const) {
    if (loadout[key] !== undefined && !LAST_CHANCES_AUGMENTS.includes(
      loadout[key] as typeof LAST_CHANCES_AUGMENTS[number],
    )) {
      errors.push(`loadout.${key} must be one of ${LAST_CHANCES_AUGMENTS.join(', ')}`)
    }
  }
  if (typeof loadout.primaryWeaponId !== 'string') return
  const primary = catalog.get(loadout.primaryWeaponId)
  if (!primary) {
    errors.push(`loadout.primaryWeaponId references unknown weapon ${loadout.primaryWeaponId}`)
    return
  }
  const primaryMode = inferredEquipMode(primary)
  if (primaryMode === 'secondaryOnly') {
    errors.push('loadout.primaryWeaponId cannot equip a secondaryOnly weapon')
  }
  const primaryAugment = typeof loadout.primaryAugment === 'string'
    ? loadout.primaryAugment
    : 'none'
  const primaryHooks = typeof primary.augmentHooks === 'object'
    && primary.augmentHooks !== null
    && !Array.isArray(primary.augmentHooks)
    ? primary.augmentHooks as UnknownRecord
    : null
  if (primaryAugment !== 'none' && !primaryHooks?.[primaryAugment]) {
    errors.push(`loadout.primaryAugment ${primaryAugment} is not supported by ${loadout.primaryWeaponId}`)
  }

  const secondaryId = typeof loadout.secondaryWeaponId === 'string'
    ? loadout.secondaryWeaponId
    : null
  if (primaryMode === 'twoHanded' && secondaryId) {
    errors.push('loadout.secondaryWeaponId must be null while a twoHanded weapon is equipped')
  }
  if (!secondaryId) {
    return
  }

  const secondary = catalog.get(secondaryId)
  if (!secondary) {
    errors.push(`loadout.secondaryWeaponId references unknown weapon ${secondaryId}`)
    return
  }
  const secondaryMode = inferredEquipMode(secondary)
  if (secondaryMode !== 'secondaryOnly' && secondaryMode !== 'eitherHand') {
    errors.push('loadout.secondaryWeaponId must equip a secondaryOnly or eitherHand weapon')
  }
  if (secondaryId === loadout.primaryWeaponId && primaryMode !== 'eitherHand') {
    errors.push('only eitherHand weapons may be equipped in both hands')
  }
  const secondaryAugment = typeof loadout.secondaryAugment === 'string'
    ? loadout.secondaryAugment
    : 'none'
  const secondaryHooks = typeof secondary.augmentHooks === 'object'
    && secondary.augmentHooks !== null
    && !Array.isArray(secondary.augmentHooks)
    ? secondary.augmentHooks as UnknownRecord
    : null
  if (secondaryAugment !== 'none' && !secondaryHooks?.[secondaryAugment]) {
    errors.push(`loadout.secondaryAugment ${secondaryAugment} is not supported by ${secondaryId}`)
  }
}

function finiteRecordNumber(record: UnknownRecord, key: string): number | null {
  const value = record[key]
  return typeof value === 'number' && Number.isFinite(value) ? value : null
}

function pointOverlapsObstacle(
  point: UnknownRecord,
  radius: number,
  obstacle: UnknownRecord,
): boolean {
  const x = finiteRecordNumber(point, 'x')
  const y = finiteRecordNumber(point, 'y')
  const obstacleX = finiteRecordNumber(obstacle, 'x')
  const obstacleY = finiteRecordNumber(obstacle, 'y')
  const width = finiteRecordNumber(obstacle, 'width')
  const height = finiteRecordNumber(obstacle, 'height')
  if (x === null || y === null || obstacleX === null || obstacleY === null
    || width === null || height === null) return false
  const nearestX = Math.max(obstacleX, Math.min(obstacleX + width, x))
  const nearestY = Math.max(obstacleY, Math.min(obstacleY + height, y))
  return Math.hypot(x - nearestX, y - nearestY) < radius
}

function eligibleEnemySpawnRadii(root: UnknownRecord): {
  globalEnemyRadius: number
  roomEnemyRadii: Map<string, number>
} {
  const enemyRadii = new Map<string, number>()
  if (Array.isArray(root.enemies)) {
    root.enemies.forEach((enemyValue) => {
      if (typeof enemyValue !== 'object' || enemyValue === null || Array.isArray(enemyValue)) return
      const enemy = enemyValue as UnknownRecord
      const radius = finiteRecordNumber(enemy, 'radius')
      if (typeof enemy.id === 'string' && radius !== null) enemyRadii.set(enemy.id, radius)
    })
  }

  const roomEnemyRadii = new Map<string, number>()
  const progression = typeof root.progression === 'object' && root.progression !== null
    && !Array.isArray(root.progression) ? root.progression as UnknownRecord : null
  if (progression && Array.isArray(progression.tiers)) {
    progression.tiers.forEach((tierValue) => {
      if (typeof tierValue !== 'object' || tierValue === null || Array.isArray(tierValue)) return
      const tier = tierValue as UnknownRecord
      if (!Array.isArray(tier.roomTemplateIds) || !Array.isArray(tier.enemyPool)) return
      const radius = Math.max(0, ...tier.enemyPool.flatMap((poolValue) => {
        if (typeof poolValue !== 'object' || poolValue === null || Array.isArray(poolValue)) return []
        const enemyId = (poolValue as UnknownRecord).enemyId
        return typeof enemyId === 'string' && enemyRadii.has(enemyId)
          ? [enemyRadii.get(enemyId) as number]
          : []
      }))
      tier.roomTemplateIds.forEach((roomId) => {
        if (typeof roomId !== 'string') return
        roomEnemyRadii.set(roomId, Math.max(roomEnemyRadii.get(roomId) ?? 0, radius))
      })
    })
  }

  return {
    globalEnemyRadius: Math.max(0, ...enemyRadii.values()),
    roomEnemyRadii,
  }
}

function spawnFitsRoomGeometry(
  point: UnknownRecord,
  radius: number,
  room: UnknownRecord,
  playerRadius: number,
): boolean {
  const x = finiteRecordNumber(point, 'x')
  const y = finiteRecordNumber(point, 'y')
  const width = finiteRecordNumber(room, 'width')
  const height = finiteRecordNumber(room, 'height')
  if (x === null || y === null || width === null || height === null
    || x < radius || x > width - radius || y < radius || y > height - radius) return false
  if (Array.isArray(room.obstacles) && room.obstacles.some((obstacleValue) => {
    return typeof obstacleValue === 'object' && obstacleValue !== null && !Array.isArray(obstacleValue)
      && pointOverlapsObstacle(point, radius, obstacleValue as UnknownRecord)
  })) return false
  if (typeof room.playerSpawn !== 'object' || room.playerSpawn === null
    || Array.isArray(room.playerSpawn)) return true
  const playerSpawn = room.playerSpawn as UnknownRecord
  const playerX = finiteRecordNumber(playerSpawn, 'x')
  const playerY = finiteRecordNumber(playerSpawn, 'y')
  return playerX === null || playerY === null
    || Math.hypot(x - playerX, y - playerY) >= radius + playerRadius
}

function validateSpawnPoint(
  value: unknown,
  path: string,
  radius: number,
  room: UnknownRecord,
  errors: string[],
): void {
  const point = typeof value === 'object' && value !== null && !Array.isArray(value)
    ? value as UnknownRecord
    : null
  if (!point) return
  const x = finiteRecordNumber(point, 'x')
  const y = finiteRecordNumber(point, 'y')
  const width = finiteRecordNumber(room, 'width')
  const height = finiteRecordNumber(room, 'height')
  if (x === null || y === null || width === null || height === null) return
  if (x < radius || x > width - radius || y < radius || y > height - radius) {
    errors.push(`${path} must fit inside its room with radius ${radius}`)
  }
  if (!Array.isArray(room.obstacles)) return
  room.obstacles.forEach((obstacleValue, obstacleIndex) => {
    if (typeof obstacleValue !== 'object' || obstacleValue === null || Array.isArray(obstacleValue)) return
    if (pointOverlapsObstacle(point, radius, obstacleValue as UnknownRecord)) {
      errors.push(`${path} overlaps room obstacle ${obstacleIndex} with radius ${radius}`)
    }
  })
}

function roomSpawnCollections(
  room: UnknownRecord,
  roomPath: string,
): Array<{ path: string, spawns: unknown[] }> {
  if (Array.isArray(room.spawnLayouts) && room.spawnLayouts.length > 0) {
    return room.spawnLayouts.flatMap((layoutValue, layoutIndex) => {
      if (typeof layoutValue !== 'object' || layoutValue === null || Array.isArray(layoutValue)) return []
      const layout = layoutValue as UnknownRecord
      return Array.isArray(layout.enemySpawns)
        ? [{ path: `${roomPath}.spawnLayouts[${layoutIndex}].enemySpawns`, spawns: layout.enemySpawns }]
        : []
    })
  }
  return Array.isArray(room.enemySpawns)
    ? [{ path: `${roomPath}.enemySpawns`, spawns: room.enemySpawns }]
    : []
}

function validateSpawnSpacing(
  spawns: unknown[],
  path: string,
  radius: number,
  errors: string[],
): void {
  for (let firstIndex = 0; firstIndex < spawns.length; firstIndex += 1) {
    const first = spawns[firstIndex]
    if (typeof first !== 'object' || first === null || Array.isArray(first)) continue
    const firstX = finiteRecordNumber(first as UnknownRecord, 'x')
    const firstY = finiteRecordNumber(first as UnknownRecord, 'y')
    if (firstX === null || firstY === null) continue
    for (let secondIndex = firstIndex + 1; secondIndex < spawns.length; secondIndex += 1) {
      const second = spawns[secondIndex]
      if (typeof second !== 'object' || second === null || Array.isArray(second)) continue
      const secondX = finiteRecordNumber(second as UnknownRecord, 'x')
      const secondY = finiteRecordNumber(second as UnknownRecord, 'y')
      if (secondX === null || secondY === null) continue
      if (Math.hypot(firstX - secondX, firstY - secondY) < radius * 2) {
        errors.push(`${path}[${firstIndex}] overlaps spawn ${secondIndex} with radius ${radius}`)
      }
    }
  }
}

function validateSpawnGeometry(root: UnknownRecord, errors: string[]): void {
  if (!Array.isArray(root.rooms) || !Array.isArray(root.enemies)) return
  const { globalEnemyRadius, roomEnemyRadii } = eligibleEnemySpawnRadii(root)
  const roomEnemyCounts = new Map<string, number>()
  const progression = typeof root.progression === 'object' && root.progression !== null
    && !Array.isArray(root.progression) ? root.progression as UnknownRecord : null
  if (progression && Array.isArray(progression.tiers)) {
    progression.tiers.forEach((tierValue) => {
      if (typeof tierValue !== 'object' || tierValue === null || Array.isArray(tierValue)) return
      const tier = tierValue as UnknownRecord
      if (!Array.isArray(tier.roomTemplateIds) || !Array.isArray(tier.enemyPool)) return
      tier.roomTemplateIds.forEach((roomId) => {
        if (typeof roomId !== 'string') return
        const maximumCount = Array.isArray(tier.enemyCount) && Number.isInteger(tier.enemyCount[1])
          ? tier.enemyCount[1] as number
          : 0
        roomEnemyCounts.set(roomId, Math.max(roomEnemyCounts.get(roomId) ?? 0, maximumCount))
      })
    })
  }
  const player = typeof root.player === 'object' && root.player !== null && !Array.isArray(root.player)
    ? root.player as UnknownRecord : null
  const playerRadius = player && typeof player.radius === 'number' && Number.isFinite(player.radius)
    ? player.radius
    : 0

  root.rooms.forEach((roomValue, roomIndex) => {
    if (typeof roomValue !== 'object' || roomValue === null || Array.isArray(roomValue)) return
    const room = roomValue as UnknownRecord
    const roomPath = `rooms[${roomIndex}]`
    const roomWidth = finiteRecordNumber(room, 'width')
    const roomHeight = finiteRecordNumber(room, 'height')
    if (roomWidth !== null && roomHeight !== null && Array.isArray(room.obstacles)) {
      room.obstacles.forEach((obstacleValue, obstacleIndex) => {
        if (typeof obstacleValue !== 'object' || obstacleValue === null || Array.isArray(obstacleValue)) return
        const obstacle = obstacleValue as UnknownRecord
        const x = finiteRecordNumber(obstacle, 'x')
        const y = finiteRecordNumber(obstacle, 'y')
        const width = finiteRecordNumber(obstacle, 'width')
        const height = finiteRecordNumber(obstacle, 'height')
        if (x !== null && y !== null && width !== null && height !== null
          && (x + width > roomWidth || y + height > roomHeight)) {
          errors.push(`${roomPath}.obstacles[${obstacleIndex}] must fit inside its room`)
        }
      })
    }
    if (roomWidth !== null && roomHeight !== null && Array.isArray(room.hazards)) {
      room.hazards.forEach((hazardValue, hazardIndex) => {
        if (typeof hazardValue !== 'object' || hazardValue === null || Array.isArray(hazardValue)) return
        const hazard = hazardValue as UnknownRecord
        const x = finiteRecordNumber(hazard, 'x')
        const y = finiteRecordNumber(hazard, 'y')
        const width = finiteRecordNumber(hazard, 'width')
        const height = finiteRecordNumber(hazard, 'height')
        if (x !== null && y !== null && width !== null && height !== null
          && (x + width > roomWidth || y + height > roomHeight)) {
          errors.push(`${roomPath}.hazards[${hazardIndex}] must fit inside its room`)
        }
      })
    }
    validateSpawnPoint(room.playerSpawn, `${roomPath}.playerSpawn`, playerRadius, room, errors)
    const enemyRadius = typeof room.id === 'string'
      ? roomEnemyRadii.get(room.id) ?? globalEnemyRadius
      : globalEnemyRadius
    const requiredCount = typeof room.id === 'string' ? roomEnemyCounts.get(room.id) ?? 0 : 0
    roomSpawnCollections(room, roomPath).forEach((collection) => {
      if (collection.spawns.length < requiredCount) {
        errors.push(`${collection.path} needs at least ${requiredCount} points for eligible tiers`)
      }
      collection.spawns.forEach((spawn, spawnIndex) => {
        validateSpawnPoint(spawn, `${collection.path}[${spawnIndex}]`, enemyRadius, room, errors)
        if (typeof spawn !== 'object' || spawn === null || Array.isArray(spawn)
          || typeof room.playerSpawn !== 'object' || room.playerSpawn === null
          || Array.isArray(room.playerSpawn)) return
        const spawnX = finiteRecordNumber(spawn as UnknownRecord, 'x')
        const spawnY = finiteRecordNumber(spawn as UnknownRecord, 'y')
        const playerX = finiteRecordNumber(room.playerSpawn as UnknownRecord, 'x')
        const playerY = finiteRecordNumber(room.playerSpawn as UnknownRecord, 'y')
        if (spawnX !== null && spawnY !== null && playerX !== null && playerY !== null
          && Math.hypot(spawnX - playerX, spawnY - playerY) < playerRadius + enemyRadius) {
          errors.push(
            `${collection.path}[${spawnIndex}] overlaps playerSpawn with combined radius ${playerRadius + enemyRadius}`,
          )
        }
      })
      validateSpawnSpacing(collection.spawns, collection.path, enemyRadius, errors)
    })
  })
}

function validateGraphCapacity(root: UnknownRecord, errors: string[]): void {
  const graph = typeof root.graph === 'object' && root.graph !== null && !Array.isArray(root.graph)
    ? root.graph as UnknownRecord : null
  const progression = typeof root.progression === 'object' && root.progression !== null
    && !Array.isArray(root.progression) ? root.progression as UnknownRecord : null
  if (!graph || !progression || !Array.isArray(progression.tiers)
    || !Number.isInteger(graph.choicesPerNode) || (graph.choicesPerNode as number) < 1) return
  for (let index = 0; index < progression.tiers.length - 1; index += 1) {
    const currentValue = progression.tiers[index]
    const nextValue = progression.tiers[index + 1]
    if (typeof currentValue !== 'object' || currentValue === null || Array.isArray(currentValue)
      || typeof nextValue !== 'object' || nextValue === null || Array.isArray(nextValue)) continue
    const currentCount = (currentValue as UnknownRecord).nodeCount
    const nextCount = (nextValue as UnknownRecord).nodeCount
    if (!Number.isInteger(currentCount) || !Number.isInteger(nextCount)) continue
    if ((currentCount as number) * (graph.choicesPerNode as number) < (nextCount as number)) {
      errors.push(`graph.choicesPerNode cannot connect every progression.tiers[${index + 1}] node`)
    }
  }
}

function validateNarrative(value: unknown, errors: string[]): void {
  if (value === undefined) return
  const narrative = asRecord(value, 'narrative', errors)
  if (!narrative) return
  for (const key of ['prologue', 'victory', 'exhaustedVictory'] as const) {
    const pages = narrative[key]
    if (!Array.isArray(pages) || pages.length === 0) {
      errors.push(`narrative.${key} must be a non-empty array`)
      continue
    }
    pages.forEach((pageValue, index) => {
      const pagePath = `narrative.${key}[${index}]`
      const page = asRecord(pageValue, pagePath, errors)
      if (!page) return
      requireString(page, 'text', pagePath, errors)
      if (page.speaker !== undefined) requireString(page, 'speaker', pagePath, errors)
    })
  }
  requireInteger(narrative, 'exhaustedDeathThreshold', 'narrative', errors, 1)
}

function validateContentReferences(root: UnknownRecord, errors: string[]): void {
  if (!Array.isArray(root.weapons) || !Array.isArray(root.rooms)) return
  const weaponIds = new Set(root.weapons.flatMap((weaponValue) => {
    if (typeof weaponValue !== 'object' || weaponValue === null || Array.isArray(weaponValue)) return []
    const id = (weaponValue as UnknownRecord).id
    return typeof id === 'string' ? [id] : []
  }))
  root.rooms.forEach((roomValue, roomIndex) => {
    if (typeof roomValue !== 'object' || roomValue === null || Array.isArray(roomValue)) return
    const interaction = (roomValue as UnknownRecord).interaction
    if (typeof interaction !== 'object' || interaction === null || Array.isArray(interaction)
      || !Array.isArray((interaction as UnknownRecord).choices)) return
    ((interaction as UnknownRecord).choices as unknown[]).forEach((choiceValue, choiceIndex) => {
      if (typeof choiceValue !== 'object' || choiceValue === null || Array.isArray(choiceValue)) return
      const effect = (choiceValue as UnknownRecord).effect
      if (typeof effect !== 'object' || effect === null || Array.isArray(effect)) return
      for (const key of ['primaryWeaponId', 'secondaryWeaponId'] as const) {
        const weaponId = (effect as UnknownRecord)[key]
        if (typeof weaponId === 'string' && !weaponIds.has(weaponId)) {
          errors.push(`rooms[${roomIndex}].interaction.choices[${choiceIndex}].effect.${key} references unknown weapon ${weaponId}`)
        }
      }
    })
  })
}

export function validateLastChancesConfig(value: unknown): LastChancesConfigValidation {
  const errors: string[] = []
  const root = asRecord(value, 'config', errors)
  if (!root) return { valid: false, errors }

  if (root.schemaVersion !== 1 && root.schemaVersion !== 2
    && root.schemaVersion !== 3 && root.schemaVersion !== 4) {
    errors.push('schemaVersion must be 1, 2, 3, or 4')
  }
  const schemaVersion = root.schemaVersion === 4
    ? 4
    : root.schemaVersion === 3
      ? 3
      : root.schemaVersion === 2
        ? 2
        : 1
  requireString(root, 'title', 'config', errors)
  requireString(root, 'seed', 'config', errors)
  requireInteger(root, 'chances', 'config', errors, 1)

  const graph = asRecord(root.graph, 'graph', errors)
  if (graph) {
    requireInteger(graph, 'choicesPerNode', 'graph', errors, 1)
    requireInteger(graph, 'generationSeedStep', 'graph', errors, 1)
  }

  let dualSenseGatePositions: number[] = []
  const input = asRecord(root.input, 'input', errors)
  if (input) {
    requirePositiveNumber(input, 'doubleTapMs', 'input', errors)
    if (schemaVersion >= 2 || input.tapComboWindowMs !== undefined) {
      requirePositiveNumber(input, 'tapComboWindowMs', 'input', errors)
    }
    requirePositiveNumber(input, 'holdMs', 'input', errors)
    requirePositiveNumber(input, 'holdMaxMs', 'input', errors)
    requirePositiveNumber(input, 'holdThenDoubleTapWindowMs', 'input', errors)
    validateUnitNumber(input, 'aimDeadZone', 'input', errors)
    validateUnitNumber(input, 'gamepadDeadZone', 'input', errors)
    validateIntegerRange(input, 'gamepadLeftButton', 'input', errors, MAX_GAMEPAD_BUTTON_INDEX)
    validateIntegerRange(input, 'gamepadRightButton', 'input', errors, MAX_GAMEPAD_BUTTON_INDEX)
    if (input.gamepadLeftButton === input.gamepadRightButton
      && Number.isInteger(input.gamepadLeftButton)) {
      errors.push('input.gamepadRightButton duplicates input.gamepadLeftButton')
    }
    requireStringArray(input, 'leftKeys', 'input', errors)
    requireStringArray(input, 'rightKeys', 'input', errors)
    if (Array.isArray(input.leftKeys) && Array.isArray(input.rightKeys)) {
      const leftKeys = new Set(input.leftKeys.filter(key => typeof key === 'string'))
      input.rightKeys.forEach((key) => {
        if (typeof key === 'string' && leftKeys.has(key)) {
          errors.push(`input.rightKeys duplicates key ${key} from input.leftKeys`)
        }
      })
    }
    if (typeof input.holdMs === 'number' && typeof input.holdMaxMs === 'number'
      && input.holdMaxMs < input.holdMs) {
      errors.push('input.holdMaxMs must be >= input.holdMs')
    }
    if (schemaVersion >= 4) {
      validateMylorikInput(input.mylorik, 'input.mylorik', errors)
      dualSenseGatePositions = validateDualSenseInput(input.dualsense, 'input.dualsense', errors)
    }
  }

  const player = asRecord(root.player, 'player', errors)
  if (player) {
    requirePositiveNumber(player, 'radius', 'player', errors)
    requireNumber(player, 'invulnerabilityMs', 'player', errors)
    validateStats(player.baseStats, 'player.baseStats', errors, false)
  }

  const mentalHealth = asRecord(root.mentalHealth, 'mentalHealth', errors)
  if (mentalHealth) {
    requireNumber(mentalHealth, 'calmRecoveryPerSecond', 'mentalHealth', errors)
    requireNumber(mentalHealth, 'restoreOnKill', 'mentalHealth', errors)
    requireNumber(mentalHealth, 'maxPressurePerSecond', 'mentalHealth', errors)
  }

  const progression = asRecord(root.progression, 'progression', errors)
  const roomIds = validateRooms(root.rooms, errors, schemaVersion >= 2)
  const enemyIds = validateEnemies(root.enemies, errors, schemaVersion)
  if (progression) {
    requireNumber(progression, 'roomHpRecovery', 'progression', errors)
    requireNumber(progression, 'roomMentalRecovery', 'progression', errors)
    validateTiers(progression.tiers, roomIds, enemyIds, errors)
  }
  validateGraphCapacity(root, errors)
  validateSpawnGeometry(root, errors)
  validateWeapons(root.weapons, root.loadout, errors, schemaVersion, dualSenseGatePositions)
  validateContentReferences(root, errors)
  validateNarrative(root.narrative, errors)

  const renderer = asRecord(root.renderer, 'renderer', errors)
  if (renderer) {
    requirePositiveNumber(renderer, 'maxDpr', 'renderer', errors)
    requirePositiveNumber(renderer, 'snapshotHz', 'renderer', errors)
    requirePositiveNumber(renderer, 'floorGridSize', 'renderer', errors)
    for (const key of ['background', 'floor', 'floorGrid', 'obstacleTop', 'obstacleSide',
      'player', 'playerAccent', 'mental'] as const) {
      requireString(renderer, key, 'renderer', errors)
    }
  }

  return { valid: errors.length === 0, errors }
}

function assertValidConfig(value: unknown, source: string): LastChancesConfig {
  const migrated = migrateLastChancesConfig(value)
  const validation = validateLastChancesConfig(migrated)
  if (!validation.valid) throw new LastChancesConfigError(`Invalid 99LC config from ${source}`, validation.errors)
  return cloneLastChancesConfig(migrated as LastChancesConfig)
}

function getBrowserStorage(): Storage | null {
  if (typeof window === 'undefined') return null
  try {
    return window.localStorage
  } catch {
    return null
  }
}

function configSchemaVersion(value: unknown): number {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return 0
  const version = (value as UnknownRecord).schemaVersion
  return typeof version === 'number' ? version : 0
}

export function cloneLastChancesConfig(config: LastChancesConfig): LastChancesConfig {
  return JSON.parse(JSON.stringify(config)) as LastChancesConfig
}

export function saveLastChancesConfig(config: LastChancesConfig): void {
  const validated = assertValidConfig(config, 'builder')
  const storage = getBrowserStorage()
  if (!storage) throw new Error('Browser storage is unavailable')
  storage.setItem(LAST_CHANCES_CONFIG_STORAGE_KEY, JSON.stringify(validated))
}

export function clearLastChancesConfig(): void {
  getBrowserStorage()?.removeItem(LAST_CHANCES_CONFIG_STORAGE_KEY)
}

export async function loadLastChancesConfig(
  options: LoadLastChancesConfigOptions = {},
): Promise<LastChancesConfig> {
  const useBrowserOverride = options.useBrowserOverride ?? true
  const storage = getBrowserStorage()
  const override = useBrowserOverride ? storage?.getItem(LAST_CHANCES_CONFIG_STORAGE_KEY) : null
  if (override) {
    let value: unknown
    try {
      value = JSON.parse(override) as unknown
    } catch {
      throw new LastChancesConfigError('Invalid 99LC browser override', ['stored value is not valid JSON'])
    }
    const overrideVersion = configSchemaVersion(value)
    if (overrideVersion === CURRENT_LAST_CHANCES_SCHEMA_VERSION) {
      return assertValidConfig(value, 'browser override')
    }
    if (overrideVersion > CURRENT_LAST_CHANCES_SCHEMA_VERSION || overrideVersion < 1) {
      return assertValidConfig(value, 'browser override')
    }

    const url = options.url ?? LAST_CHANCES_CONFIG_URL
    const response = await fetch(url, { cache: 'no-store', signal: options.signal })
    if (!response.ok) throw new Error(`Unable to load 99LC config (${response.status} ${response.statusText})`)
    const current = assertValidConfig(await response.json() as unknown, url)
    if (current.schemaVersion !== CURRENT_LAST_CHANCES_SCHEMA_VERSION) {
      throw new LastChancesConfigError(`Invalid 99LC config from ${url}`, [
        `current definition must use schemaVersion ${CURRENT_LAST_CHANCES_SCHEMA_VERSION}`,
      ])
    }
    const migrated = assertValidConfig(
      migrateLastChancesConfig(value, current),
      'migrated browser override',
    )
    storage?.setItem(LAST_CHANCES_CONFIG_STORAGE_KEY, JSON.stringify(migrated))
    return migrated
  }

  const url = options.url ?? LAST_CHANCES_CONFIG_URL
  const response = await fetch(url, { cache: 'no-store', signal: options.signal })
  if (!response.ok) throw new Error(`Unable to load 99LC config (${response.status} ${response.statusText})`)
  return assertValidConfig(await response.json() as unknown, url)
}
