export interface LastChancesGamepadButtonLike {
  readonly pressed: boolean
  readonly value: number
}

export interface LastChancesGamepadLike {
  readonly axes: readonly number[]
  readonly buttons: readonly LastChancesGamepadButtonLike[]
  readonly connected: boolean
  readonly id: string
  readonly index: number
  readonly mapping: string
}

export interface LastChancesGamepadAdapterConfig {
  deadZone: number
  leftButton: number
  rightButton: number
  buttonThreshold?: number
  analogTriggerThreshold?: number
}

export interface LastChancesGamepadVector {
  x: number
  y: number
}

export type LastChancesGamepadProfile = 'standard' | 'sony-raw' | 'generic'
export type LastChancesGamepadStatus = 'disconnected' | 'idle' | 'active'

export interface LastChancesGamepadReading {
  status: LastChancesGamepadStatus
  activeIndex: number | null
  connectedCount: number
  id: string | null
  mapping: string | null
  profile: LastChancesGamepadProfile | null
  meaningfulInput: boolean
  axes: readonly [number, number, number, number]
  move: LastChancesGamepadVector
  aim: LastChancesGamepadVector
  buttons: {
    /** DeepList-configured inputs. */
    left: boolean
    right: boolean
    l1: boolean
    r1: boolean
    circle: boolean
    cross: boolean
    options: boolean
    dpadUp: boolean
    dpadDown: boolean
    dpadLeft: boolean
    dpadRight: boolean
  }
  triggers: {
    left: number
    right: number
  }
  /** Canonicalized button values; Builder bindings index this array. */
  canonicalButtons: readonly LastChancesGamepadButtonLike[]
  sourceButtonIndexes: {
    left: number
    right: number
  } | null
}

type GamepadListLike = readonly (LastChancesGamepadLike | null | undefined)[]

interface NormalizedGamepad {
  gamepad: LastChancesGamepadLike
  profile: LastChancesGamepadProfile
  meaningfulInput: boolean
  axes: [number, number, number, number]
}

const DEFAULT_BUTTON_THRESHOLD = 0.5
const SONY_PRODUCTS = /\b(?:0268|05c4|09cc|0ce6|0df2)\b/i
const SONY_NAME = /\b(?:dualsense|dualshock)\b|sony interactive entertainment/i
const RAW_SONY_AXIS_INDEXES = [0, 1, 2, 5] as const
const STANDARD_AXIS_INDEXES = [0, 1, 2, 3] as const
const RAW_SONY_FACE_BUTTONS: Readonly<Record<number, number>> = {
  0: 1,
  1: 2,
  2: 0,
  3: 3,
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.max(minimum, Math.min(maximum, value))
}

function normalizedThreshold(value: number | undefined, fallback: number): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) return fallback
  return clamp(value, 0, 1)
}

function isRawSonyGamepad(gamepad: LastChancesGamepadLike): boolean {
  if (gamepad.mapping === 'standard') return false
  const id = gamepad.id.trim()
  return (id.toLowerCase().includes('054c') && SONY_PRODUCTS.test(id)) || SONY_NAME.test(id)
}

function profileFor(gamepad: LastChancesGamepadLike): LastChancesGamepadProfile {
  if (gamepad.mapping === 'standard') return 'standard'
  return isRawSonyGamepad(gamepad) ? 'sony-raw' : 'generic'
}

function axisValue(gamepad: LastChancesGamepadLike, index: number, deadZone: number): number {
  const raw = gamepad.axes[index]
  if (typeof raw !== 'number' || !Number.isFinite(raw)) return 0
  const value = clamp(raw, -1, 1)
  return Math.abs(value) >= deadZone ? value : 0
}

function normalizeVector(x: number, y: number): LastChancesGamepadVector {
  const length = Math.hypot(x, y)
  if (length <= 1) return { x, y }
  return { x: x / length, y: y / length }
}

function sourceButtonIndex(profile: LastChancesGamepadProfile, canonicalIndex: number): number {
  if (profile !== 'sony-raw') return canonicalIndex
  return RAW_SONY_FACE_BUTTONS[canonicalIndex] ?? canonicalIndex
}

function buttonPressed(
  gamepad: LastChancesGamepadLike,
  index: number,
  threshold: number,
): boolean {
  const button = gamepad.buttons[index]
  if (!button) return false
  return button.pressed || (Number.isFinite(button.value) && button.value >= threshold)
}

function buttonValue(gamepad: LastChancesGamepadLike, index: number): number {
  const button = gamepad.buttons[index]
  if (!button) return 0
  if (Number.isFinite(button.value)) return clamp(button.value, 0, 1)
  return button.pressed ? 1 : 0
}

function normalizeGamepad(
  gamepad: LastChancesGamepadLike,
  deadZone: number,
  buttonThreshold: number,
  analogTriggerThreshold: number,
): NormalizedGamepad {
  const profile = profileFor(gamepad)
  const indexes = profile === 'sony-raw' ? RAW_SONY_AXIS_INDEXES : STANDARD_AXIS_INDEXES
  const axes: [number, number, number, number] = [
    axisValue(gamepad, indexes[0], deadZone),
    axisValue(gamepad, indexes[1], deadZone),
    axisValue(gamepad, indexes[2], deadZone),
    axisValue(gamepad, indexes[3], deadZone),
  ]
  const meaningfulInput = axes.some(value => value !== 0)
    || gamepad.buttons.some((_, index) => buttonPressed(gamepad, index, buttonThreshold))
    || [6, 7].some(canonicalIndex => (
      buttonValue(gamepad, sourceButtonIndex(profile, canonicalIndex)) >= analogTriggerThreshold
    ))
  return { gamepad, profile, meaningfulInput, axes }
}

function selectGamepad(
  gamepads: NormalizedGamepad[],
  previousActiveIndex: number | null,
): NormalizedGamepad {
  const previous = previousActiveIndex === null
    ? undefined
    : gamepads.find(candidate => candidate.gamepad.index === previousActiveIndex)
  if (previous?.meaningfulInput) return previous

  const active = gamepads.find(candidate => candidate.meaningfulInput)
  if (active) return active
  return previous ?? gamepads[0]
}

function disconnectedReading(): LastChancesGamepadReading {
  return {
    status: 'disconnected',
    activeIndex: null,
    connectedCount: 0,
    id: null,
    mapping: null,
    profile: null,
    meaningfulInput: false,
    axes: [0, 0, 0, 0],
    move: { x: 0, y: 0 },
    aim: { x: 0, y: 0 },
    buttons: {
      left: false,
      right: false,
      l1: false,
      r1: false,
      circle: false,
      cross: false,
      options: false,
      dpadUp: false,
      dpadDown: false,
      dpadLeft: false,
      dpadRight: false,
    },
    triggers: { left: 0, right: 0 },
    canonicalButtons: [],
    sourceButtonIndexes: null,
  }
}

export function readLastChancesGamepads(
  gamepads: GamepadListLike,
  config: LastChancesGamepadAdapterConfig,
  previousActiveIndex: number | null = null,
): LastChancesGamepadReading {
  const deadZone = normalizedThreshold(config.deadZone, 0)
  const buttonThreshold = normalizedThreshold(config.buttonThreshold, DEFAULT_BUTTON_THRESHOLD)
  const analogTriggerThreshold = normalizedThreshold(
    config.analogTriggerThreshold,
    buttonThreshold,
  )
  const connected = gamepads
    .filter((gamepad): gamepad is LastChancesGamepadLike => Boolean(gamepad?.connected))
    .map(gamepad => normalizeGamepad(
      gamepad,
      deadZone,
      buttonThreshold,
      analogTriggerThreshold,
    ))

  if (connected.length === 0) return disconnectedReading()

  const selected = selectGamepad(connected, previousActiveIndex)
  const leftIndex = sourceButtonIndex(selected.profile, config.leftButton)
  const rightIndex = sourceButtonIndex(selected.profile, config.rightButton)
  const crossIndex = sourceButtonIndex(selected.profile, 0)
  const circleIndex = sourceButtonIndex(selected.profile, 1)
  const l1Index = sourceButtonIndex(selected.profile, 4)
  const r1Index = sourceButtonIndex(selected.profile, 5)
  const l2Index = sourceButtonIndex(selected.profile, 6)
  const r2Index = sourceButtonIndex(selected.profile, 7)
  const optionsIndex = sourceButtonIndex(selected.profile, 9)
  const dpadUpIndex = sourceButtonIndex(selected.profile, 12)
  const dpadDownIndex = sourceButtonIndex(selected.profile, 13)
  const dpadLeftIndex = sourceButtonIndex(selected.profile, 14)
  const dpadRightIndex = sourceButtonIndex(selected.profile, 15)
  const [moveX, moveY, aimX, aimY] = selected.axes
  const canonicalButtons = selected.gamepad.buttons.map((_, canonicalIndex) => {
    const sourceIndex = sourceButtonIndex(selected.profile, canonicalIndex)
    return {
      pressed: buttonPressed(selected.gamepad, sourceIndex, buttonThreshold),
      value: buttonValue(selected.gamepad, sourceIndex),
    }
  })

  return {
    status: selected.meaningfulInput ? 'active' : 'idle',
    activeIndex: selected.gamepad.index,
    connectedCount: connected.length,
    id: selected.gamepad.id,
    mapping: selected.gamepad.mapping,
    profile: selected.profile,
    meaningfulInput: selected.meaningfulInput,
    axes: selected.axes,
    move: normalizeVector(moveX, moveY),
    aim: normalizeVector(aimX, aimY),
    buttons: {
      left: buttonPressed(selected.gamepad, leftIndex, buttonThreshold),
      right: buttonPressed(selected.gamepad, rightIndex, buttonThreshold),
      l1: buttonPressed(selected.gamepad, l1Index, buttonThreshold),
      r1: buttonPressed(selected.gamepad, r1Index, buttonThreshold),
      circle: buttonPressed(selected.gamepad, circleIndex, buttonThreshold),
      cross: buttonPressed(selected.gamepad, crossIndex, buttonThreshold),
      options: buttonPressed(selected.gamepad, optionsIndex, buttonThreshold),
      dpadUp: buttonPressed(selected.gamepad, dpadUpIndex, buttonThreshold),
      dpadDown: buttonPressed(selected.gamepad, dpadDownIndex, buttonThreshold),
      dpadLeft: buttonPressed(selected.gamepad, dpadLeftIndex, buttonThreshold),
      dpadRight: buttonPressed(selected.gamepad, dpadRightIndex, buttonThreshold),
    },
    triggers: {
      left: buttonValue(selected.gamepad, l2Index),
      right: buttonValue(selected.gamepad, r2Index),
    },
    canonicalButtons,
    sourceButtonIndexes: { left: leftIndex, right: rightIndex },
  }
}

export class LastChancesGamepadAdapter {
  private activeGamepadIndex: number | null = null

  constructor(private readonly config: LastChancesGamepadAdapterConfig) {}

  get activeIndex(): number | null {
    return this.activeGamepadIndex
  }

  poll(gamepads: GamepadListLike): LastChancesGamepadReading {
    const reading = readLastChancesGamepads(gamepads, this.config, this.activeGamepadIndex)
    this.activeGamepadIndex = reading.activeIndex
    return reading
  }

  reset(): void {
    this.activeGamepadIndex = null
  }
}

export function createLastChancesGamepadAdapter(
  config: LastChancesGamepadAdapterConfig,
): LastChancesGamepadAdapter {
  return new LastChancesGamepadAdapter(config)
}
