export const CLASH_FIELD_LIMITS = {
  minWidth: 3,
  maxWidth: 10,
  minLength: 3,
  maxLength: 5,
  defaultWidth: 5,
  defaultLength: 5,
} as const

export type ClashPhase =
  | 'Lobby'
  | 'InitialFrontPlacement'
  | 'GuestSecondRowPlacement'
  | 'HostSecondRowPlacement'
  | 'GuestThirdRowPlacement'
  | 'HostThirdRowPlacement'
  | 'ResolvingClash'
  | 'GuestReinforcement'
  | 'HostReinforcement'
  | 'ActiveExchange'
  | 'Finished'

export type ClashSide = 'Host' | 'Guest'

export interface ClashLobbyState {
  games: ClashLobbyGame[]
}

export interface ClashLobbyGame {
  gameId: string
  phase: ClashPhase | string
  width: number
  length: number
  hostName: string
  guestName: string | null
  vsBot: boolean
  canJoin: boolean
  createdAt: string
}

export interface ClashCatalog {
  units: ClashUnitDefinition[]
  minWidth: number
  maxWidth: number
  minLength: number
  maxLength: number
  defaultWidth: number
  defaultLength: number
  startingMorale: number
}

export interface ClashUnitDefinition {
  id: string
  name: string
  faction: string
  attack: number
  maxHp: number
  speed: number
  isRanged: boolean
  reloadClashes: number
  shieldCharges: number
  dodgeCharges: number
  appliesBleed: boolean
  diesToAoe: boolean
  tags: string[]
  passives: ClashPassiveDefinition[]
  abilities: ClashAbilityDefinition[]
}

export interface ClashPassiveDefinition {
  id?: string
  name: string
  description?: string
}

export interface ClashAbilityDefinition {
  id: string
  name: string
  description?: string
  target?: 'self' | 'ally' | 'enemy' | 'unit' | 'cell' | 'none' | string
  value?: number
  isAoe: boolean
}

export interface ClashGameState {
  gameId: string
  revision: number
  phase: ClashPhase | string
  width: number
  length: number
  clashNumber: number
  vsBot: boolean
  isFinished: boolean
  winnerId: string | null
  isDraw: boolean
  terminalReason: string | null
  currentTurnPlayerId: string | null
  isMyTurn: boolean
  myPlayerId: string | null
  requiredPlacementRow: number | null
  placementActionLabel: string | null
  host: ClashPlayerState | null
  guest: ClashPlayerState | null
  boardCells: ClashBoardCell[]
  latestResolution: ClashResolution | null
  canConfigure: boolean
  canSetArmy: boolean
  canConfirmReady: boolean
  canPlace: boolean
  canRemove: boolean
  canConfirmPlacement: boolean
  canPlaceReinforcement: boolean
  canUseActive: boolean
  canContinue: boolean
  canForfeit: boolean
}

export interface ClashPlayerState {
  playerId: string
  username: string
  isBot: boolean
  isHost: boolean
  isMe: boolean
  isReady: boolean
  initialFrontConfirmed: boolean
  morale: number
  armySize: number
  selectedArmyDefinitionIds: string[]
  hand: ClashUnitState[]
  handCount: number
  usedActiveIds: string[]
  activeSelectionsUsed: number
  activeSelectionLimit: number
  canRepeatActive: boolean
  activeEffectsDoubled: boolean
  hasContinued: boolean
}

/**
 * The server normally sends a full cell with `unit`, while early Clash builds
 * sent occupied unit DTOs directly in `boardCells`. The normalizer below
 * accepts both without ever reconstructing hidden enemy units client-side.
 */
export interface ClashBoardCell {
  boardRow: number
  column: number
  territorySide: ClashSide | string
  unit: ClashUnitState | null
  isHidden: boolean
}

export interface ClashUnitState {
  instanceId: string
  definitionId: string
  name?: string
  ownerId: string
  ownerSide: ClashSide | string
  boardRow: number
  column: number
  hp: number
  maxHp: number
  attack: number
  speed: number
  shieldCharges: number
  dodgeCharges: number
  bleedStacks: number
  rangedReadyClash: number
  alive: boolean
  deployed: boolean
  isHidden: boolean
  diesToAoe: boolean
}

export interface ClashResolution {
  gameId: string
  revision: number
  clashNumber: number
  startedAtUtc: string
  durationMs: number
  events: ClashResolutionEvent[]
  finalUnits: ClashUnitState[]
  winnerId: string | null
  isDraw: boolean
  terminalReason: string | null
}

export type ClashResolutionEventType =
  | 'Attack'
  | 'Active'
  | 'Passive'
  | 'Wait'
  | 'Damage'
  | 'Death'
  | 'Advance'
  | string

export interface ClashResolutionEvent {
  sequence: number
  type: ClashResolutionEventType
  actorUnitInstanceId: string | null
  targetUnitInstanceId: string | null
  speed: number
  startOffsetMs: number
  impactOffsetMs: number
  amount: number
  fromBoardRow: number | null
  toBoardRow: number | null
  column: number | null
  message: string
}

export interface ClashVisualUnitOverride {
  snapshot: ClashUnitState
  hp: number
  alive: boolean
  boardRow: number
  column: number
  shieldCharges: number
  dodgeCharges: number
  bleedStacks: number
  animation: 'idle' | 'attack' | 'active' | 'passive' | 'hit' | 'death' | 'advance' | 'wait'
  animationSequence: number
}

export interface ClashCommandMeta {
  expectedRevision: number | null
  commandId: string
}

export type ClashPhaseKind =
  | 'lobby'
  | 'army'
  | 'deployment'
  | 'combat'
  | 'reinforcement'
  | 'actives'
  | 'finished'

export function clashPhaseKind(state: Pick<ClashGameState, 'phase'>): ClashPhaseKind {
  switch (state.phase) {
    case 'Lobby': return 'lobby'
    case 'InitialFrontPlacement':
    case 'GuestSecondRowPlacement':
    case 'HostSecondRowPlacement':
    case 'GuestThirdRowPlacement':
    case 'HostThirdRowPlacement':
      return 'deployment'
    case 'ResolvingClash': return 'combat'
    case 'GuestReinforcement':
    case 'HostReinforcement':
      return 'reinforcement'
    case 'ActiveExchange': return 'actives'
    case 'Finished': return 'finished'
    default: return 'army'
  }
}

export function placementActionLabel(phase: string): string {
  if (phase === 'InitialFrontPlacement') return 'Сблизиться!'
  if (phase === 'GuestSecondRowPlacement' || phase === 'HostSecondRowPlacement') return 'Становись!'
  if (phase === 'GuestThirdRowPlacement' || phase === 'HostThirdRowPlacement') return 'Вступить в бой!'
  return 'Подтвердить'
}

export function localRowToGlobalRow(localRow: number, length: number, isHost: boolean): number {
  return isHost ? length - 1 - localRow : length + localRow
}

export function globalRowToLocalRow(boardRow: number, length: number, isHost: boolean): number {
  return isHost ? length - 1 - boardRow : boardRow - length
}

export function fieldCellKey(boardRow: number, column: number): string {
  return `${boardRow}:${column}`
}

export function clampClashDimension(value: number, minimum: number, maximum: number): number {
  if (!Number.isFinite(value)) return minimum
  return Math.min(maximum, Math.max(minimum, Math.round(value)))
}

function record(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null ? value as Record<string, unknown> : {}
}

function numberValue(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
}

function stringValue(value: unknown, fallback = ''): string {
  return typeof value === 'string' ? value : fallback
}

function nullableString(value: unknown): string | null {
  return typeof value === 'string' ? value : null
}

function booleanValue(value: unknown, fallback = false): boolean {
  return typeof value === 'boolean' ? value : fallback
}

function arrayValue(value: unknown): unknown[] {
  return Array.isArray(value) ? value : []
}

export function normalizeClashUnit(value: unknown): ClashUnitState {
  const source = record(value)
  return {
    instanceId: stringValue(source.instanceId ?? source.unitInstanceId ?? source.id),
    definitionId: stringValue(source.definitionId ?? source.unitDefinitionId),
    name: stringValue(source.name) || undefined,
    ownerId: stringValue(source.ownerId),
    ownerSide: stringValue(source.ownerSide),
    boardRow: numberValue(source.boardRow, -1),
    column: numberValue(source.column, -1),
    hp: numberValue(source.hp),
    maxHp: numberValue(source.maxHp),
    attack: numberValue(source.attack),
    speed: numberValue(source.speed),
    shieldCharges: numberValue(source.shieldCharges),
    dodgeCharges: numberValue(source.dodgeCharges),
    bleedStacks: numberValue(source.bleedStacks),
    rangedReadyClash: numberValue(source.rangedReadyClash),
    alive: booleanValue(source.alive, true),
    deployed: booleanValue(source.deployed),
    isHidden: booleanValue(source.isHidden),
    diesToAoe: booleanValue(source.diesToAoe),
  }
}

export function normalizeClashBoardCells(value: unknown): ClashBoardCell[] {
  return arrayValue(value).map((entry) => {
    const source = record(entry)
    const nestedUnit = source.unit
    const looksLikeUnit = 'instanceId' in source || 'unitInstanceId' in source || 'definitionId' in source
    const unit = nestedUnit == null && !looksLikeUnit
      ? null
      : normalizeClashUnit(nestedUnit ?? source)
    return {
      boardRow: numberValue(source.boardRow, unit?.boardRow ?? -1),
      column: numberValue(source.column, unit?.column ?? -1),
      territorySide: stringValue(source.territorySide),
      unit,
      isHidden: booleanValue(source.isHidden, unit?.isHidden ?? false),
    }
  }).filter(cell => cell.boardRow >= 0 && cell.column >= 0)
}

export function normalizeClashResolution(value: unknown): ClashResolution | null {
  if (value == null) return null
  const source = record(value)
  const events = arrayValue(source.events).map((entry) => {
    const event = record(entry)
    return {
      sequence: numberValue(event.sequence),
      type: stringValue(event.type),
      actorUnitInstanceId: nullableString(event.actorUnitInstanceId),
      targetUnitInstanceId: nullableString(event.targetUnitInstanceId),
      speed: numberValue(event.speed),
      startOffsetMs: Math.max(0, numberValue(event.startOffsetMs)),
      impactOffsetMs: Math.max(0, numberValue(event.impactOffsetMs)),
      amount: numberValue(event.amount),
      fromBoardRow: typeof event.fromBoardRow === 'number' ? event.fromBoardRow : null,
      toBoardRow: typeof event.toBoardRow === 'number' ? event.toBoardRow : null,
      column: typeof event.column === 'number' ? event.column : null,
      message: stringValue(event.message),
    } satisfies ClashResolutionEvent
  }).sort((a, b) => a.sequence - b.sequence)
  return {
    gameId: stringValue(source.gameId),
    revision: numberValue(source.revision),
    clashNumber: numberValue(source.clashNumber),
    startedAtUtc: stringValue(source.startedAtUtc),
    durationMs: Math.max(0, numberValue(source.durationMs)),
    events,
    finalUnits: arrayValue(source.finalUnits).map(normalizeClashUnit),
    winnerId: nullableString(source.winnerId),
    isDraw: booleanValue(source.isDraw),
    terminalReason: nullableString(source.terminalReason),
  }
}

function normalizePlayer(value: unknown): ClashPlayerState | null {
  if (value == null) return null
  const source = record(value)
  return {
    playerId: stringValue(source.playerId),
    username: stringValue(source.username, 'Игрок'),
    isBot: booleanValue(source.isBot),
    isHost: booleanValue(source.isHost),
    isMe: booleanValue(source.isMe),
    isReady: booleanValue(source.isReady),
    initialFrontConfirmed: booleanValue(source.initialFrontConfirmed),
    morale: clampClashDimension(numberValue(source.morale), 0, 5),
    armySize: numberValue(source.armySize),
    selectedArmyDefinitionIds: arrayValue(source.selectedArmyDefinitionIds)
      .filter((entry): entry is string => typeof entry === 'string'),
    hand: arrayValue(source.hand).map(normalizeClashUnit),
    handCount: numberValue(source.handCount, arrayValue(source.hand).length),
    usedActiveIds: arrayValue(source.usedActiveIds).filter((entry): entry is string => typeof entry === 'string'),
    activeSelectionsUsed: numberValue(source.activeSelectionsUsed),
    activeSelectionLimit: numberValue(source.activeSelectionLimit),
    canRepeatActive: booleanValue(source.canRepeatActive),
    activeEffectsDoubled: booleanValue(source.activeEffectsDoubled),
    hasContinued: booleanValue(source.hasContinued),
  }
}

export function normalizeClashGameState(value: unknown): ClashGameState {
  const source = record(value)
  const width = clampClashDimension(
    numberValue(source.width, CLASH_FIELD_LIMITS.defaultWidth),
    CLASH_FIELD_LIMITS.minWidth,
    CLASH_FIELD_LIMITS.maxWidth,
  )
  const length = clampClashDimension(
    numberValue(source.length, CLASH_FIELD_LIMITS.defaultLength),
    CLASH_FIELD_LIMITS.minLength,
    CLASH_FIELD_LIMITS.maxLength,
  )
  return {
    gameId: stringValue(source.gameId),
    revision: numberValue(source.revision),
    phase: stringValue(source.phase, 'Lobby'),
    width,
    length,
    clashNumber: numberValue(source.clashNumber),
    vsBot: booleanValue(source.vsBot),
    isFinished: booleanValue(source.isFinished),
    winnerId: nullableString(source.winnerId),
    isDraw: booleanValue(source.isDraw),
    terminalReason: nullableString(source.terminalReason),
    currentTurnPlayerId: nullableString(source.currentTurnPlayerId),
    isMyTurn: booleanValue(source.isMyTurn),
    myPlayerId: nullableString(source.myPlayerId),
    requiredPlacementRow: typeof source.requiredPlacementRow === 'number' ? source.requiredPlacementRow : null,
    placementActionLabel: nullableString(source.placementActionLabel),
    host: normalizePlayer(source.host),
    guest: normalizePlayer(source.guest),
    boardCells: normalizeClashBoardCells(source.boardCells),
    latestResolution: normalizeClashResolution(source.latestResolution),
    canConfigure: booleanValue(source.canConfigure),
    canSetArmy: booleanValue(source.canSetArmy),
    canConfirmReady: booleanValue(source.canConfirmReady),
    canPlace: booleanValue(source.canPlace),
    canRemove: booleanValue(source.canRemove),
    canConfirmPlacement: booleanValue(source.canConfirmPlacement),
    canPlaceReinforcement: booleanValue(source.canPlaceReinforcement),
    canUseActive: booleanValue(source.canUseActive),
    canContinue: booleanValue(source.canContinue),
    canForfeit: booleanValue(source.canForfeit),
  }
}

export function normalizeClashCatalog(value: unknown): ClashCatalog {
  const source = record(value)
  const units = arrayValue(source.units).map((entry) => {
    const unit = record(entry)
    return {
      id: stringValue(unit.id),
      name: stringValue(unit.name, 'Неизвестный юнит'),
      faction: stringValue(unit.faction, 'Нейтральный'),
      attack: numberValue(unit.attack),
      maxHp: numberValue(unit.maxHp),
      speed: numberValue(unit.speed),
      isRanged: booleanValue(unit.isRanged),
      reloadClashes: numberValue(unit.reloadClashes),
      shieldCharges: numberValue(unit.shieldCharges),
      dodgeCharges: numberValue(unit.dodgeCharges),
      appliesBleed: booleanValue(unit.appliesBleed),
      diesToAoe: booleanValue(unit.diesToAoe),
      tags: arrayValue(unit.tags).filter((tag): tag is string => typeof tag === 'string'),
      passives: arrayValue(unit.passives).map((passiveValue) => {
        const passive = record(passiveValue)
        return {
          id: stringValue(passive.id) || undefined,
          name: stringValue(passive.name),
          description: stringValue(passive.description) || undefined,
        }
      }),
      abilities: arrayValue(unit.abilities).map((abilityValue) => {
        const ability = record(abilityValue)
        return {
          id: stringValue(ability.id),
          name: stringValue(ability.name),
          description: stringValue(ability.description) || undefined,
          target: stringValue(ability.target) || undefined,
          value: typeof ability.value === 'number' ? ability.value : undefined,
          isAoe: booleanValue(ability.isAoe),
        }
      }),
    } satisfies ClashUnitDefinition
  }).filter(unit => unit.id !== '')
  return {
    units,
    minWidth: numberValue(source.minWidth, CLASH_FIELD_LIMITS.minWidth),
    maxWidth: numberValue(source.maxWidth, CLASH_FIELD_LIMITS.maxWidth),
    minLength: numberValue(source.minLength, CLASH_FIELD_LIMITS.minLength),
    maxLength: numberValue(source.maxLength, CLASH_FIELD_LIMITS.maxLength),
    defaultWidth: numberValue(source.defaultWidth, CLASH_FIELD_LIMITS.defaultWidth),
    defaultLength: numberValue(source.defaultLength, CLASH_FIELD_LIMITS.defaultLength),
    startingMorale: clampClashDimension(numberValue(source.startingMorale), 0, 5),
  }
}
