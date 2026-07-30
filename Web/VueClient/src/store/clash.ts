import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { signalrService } from 'src/services/signalr'
import {
  clashPhaseKind,
  fieldCellKey,
  normalizeClashCatalog,
  normalizeClashGameState,
  normalizeClashResolution,
  type ClashAbilityDefinition,
  type ClashBoardCell,
  type ClashCatalog,
  type ClashCommandMeta,
  type ClashGameState,
  type ClashLobbyState,
  type ClashPlayerState,
  type ClashResolution,
  type ClashResolutionEvent,
  type ClashUnitDefinition,
  type ClashUnitState,
  type ClashVisualUnitOverride,
} from 'src/features/clash/types'
import {
  clashEventAnimation,
  clashResolutionElapsedMs,
  clashResolutionIdentity,
  prefersReducedClashMotion,
  reconstructClashResolutionStartUnit,
} from 'src/features/clash/visuals'

export const useClashStore = defineStore('clash', () => {
  const lobbyState = ref<ClashLobbyState | null>(null)
  const gameState = ref<ClashGameState | null>(null)
  const catalog = ref<ClashCatalog | null>(null)
  const errorMessage = ref<string | null>(null)
  const isCreating = ref(false)
  const pendingCommands = ref(0)
  const navigationGameId = ref<string | null>(null)
  const myActiveGameId = ref<string | null>(null)

  const activeResolution = ref<ClashResolution | null>(null)
  const resolutionEvents = ref<ClashResolutionEvent[]>([])
  const timelinePlaying = ref(false)
  const visualOverrides = ref(new Map<string, ClashVisualUnitOverride>())
  const seenResolutionIds = new Set<string>()
  let errorTimer: ReturnType<typeof setTimeout> | null = null
  let createSucceeded = false

  const gameId = computed(() => gameState.value?.gameId ?? null)
  const revision = computed(() => gameState.value?.revision ?? null)
  const phase = computed(() => gameState.value?.phase ?? 'Lobby')
  const phaseKind = computed(() => gameState.value ? clashPhaseKind(gameState.value) : 'lobby')
  const isMyTurn = computed(() => gameState.value?.isMyTurn ?? false)
  const isBusy = computed(() => pendingCommands.value > 0)

  const myPlayer = computed<ClashPlayerState | null>(() => {
    if (gameState.value?.host?.isMe) return gameState.value.host
    if (gameState.value?.guest?.isMe) return gameState.value.guest
    return null
  })

  const opponent = computed<ClashPlayerState | null>(() => {
    if (!gameState.value) return null
    if (gameState.value.host?.isMe) return gameState.value.guest
    if (gameState.value.guest?.isMe) return gameState.value.host
    return gameState.value.guest
  })

  const catalogById = computed(() => {
    const entries = (catalog.value?.units ?? []).map(unit => [unit.id, unit] as const)
    return new Map<string, ClashUnitDefinition>(entries)
  })

  const boardCells = computed<ClashBoardCell[]>(() => gameState.value?.boardCells ?? [])
  const boardUnits = computed<ClashUnitState[]>(() =>
    boardCells.value.flatMap(cell => cell.unit ? [cell.unit] : []))

  function setError(error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : String(error)
    if (errorTimer) clearTimeout(errorTimer)
    errorTimer = setTimeout(() => {
      errorMessage.value = null
      errorTimer = null
    }, 5000)
  }

  function makeCommandMeta(): ClashCommandMeta {
    const randomId = typeof globalThis.crypto?.randomUUID === 'function'
      ? globalThis.crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(36).slice(2)}`
    return {
      expectedRevision: revision.value,
      commandId: randomId,
    }
  }

  async function runCommand(command: () => Promise<void>) {
    pendingCommands.value++
    try {
      await command()
    }
    catch (error) {
      setError(error)
      throw error
    }
    finally {
      pendingCommands.value--
    }
  }

  function snapshotVisualUnits(state: ClashGameState | null, resolution: ClashResolution) {
    const next = new Map<string, ClashVisualUnitOverride>()
    for (const cell of state?.boardCells ?? []) {
      const unit = cell.unit
      if (!unit || unit.isHidden) continue
      next.set(unit.instanceId, {
        snapshot: { ...unit },
        hp: unit.hp,
        alive: unit.alive,
        boardRow: unit.boardRow,
        column: unit.column,
        shieldCharges: unit.shieldCharges,
        dodgeCharges: unit.dodgeCharges,
        bleedStacks: unit.bleedStacks,
        animation: 'idle',
        animationSequence: 0,
      })
    }
    // The host's final setup row becomes public in the same mutation that
    // starts the first clash. It may therefore be absent from the viewer's
    // previous personalized DTO. Reconstruct only those newly-public units
    // backwards from the authoritative trace so final HP/deaths/moves are
    // still revealed at their impact offsets, never at event receipt.
    for (const finalUnit of resolution.finalUnits) {
      if (next.has(finalUnit.instanceId) || finalUnit.isHidden) continue
      const unit = reconstructClashResolutionStartUnit(
        finalUnit,
        resolution.events,
        catalogById.value.get(finalUnit.definitionId),
      )
      next.set(unit.instanceId, {
        snapshot: unit,
        hp: unit.hp,
        alive: unit.alive,
        boardRow: unit.boardRow,
        column: unit.column,
        shieldCharges: unit.shieldCharges,
        dodgeCharges: unit.dodgeCharges,
        bleedStacks: unit.bleedStacks,
        animation: 'idle',
        animationSequence: 0,
      })
    }
    visualOverrides.value = next
  }

  function beginResolution(resolution: ClashResolution, snapshot: ClashGameState | null) {
    const identity = clashResolutionIdentity(
      resolution.gameId,
      resolution.revision,
      resolution.clashNumber,
    )
    if (seenResolutionIds.has(identity)) return
    seenResolutionIds.add(identity)
    activeResolution.value = resolution
    resolutionEvents.value = resolution.events
    if (
      prefersReducedClashMotion()
      || resolution.events.length === 0
      || clashResolutionElapsedMs(resolution.startedAtUtc) >= resolution.durationMs
    ) {
      visualOverrides.value = new Map()
      timelinePlaying.value = false
      return
    }
    snapshotVisualUnits(snapshot, resolution)
    timelinePlaying.value = true
  }

  function receiveState(rawState: ClashGameState) {
    const nextState = normalizeClashGameState(rawState)
    if (gameState.value?.gameId && gameState.value.gameId !== nextState.gameId) {
      visualOverrides.value = new Map()
      resolutionEvents.value = []
      activeResolution.value = null
      timelinePlaying.value = false
      seenResolutionIds.clear()
    }
    // Live resolutions arrive as their own event before the post-clash state.
    // A state-only reconnect resumes any still-running trace. Terminal clashes
    // enter Finished immediately on the server, so they also remain eligible
    // until beginResolution observes that their authoritative duration elapsed.
    if (
      nextState.latestResolution
      && (nextState.phase === 'ResolvingClash' || nextState.phase === 'Finished')
    ) {
      beginResolution(nextState.latestResolution, gameState.value)
    }
    if (
      timelinePlaying.value
      && activeResolution.value
      && nextState.phase !== 'ResolvingClash'
      && clashResolutionElapsedMs(activeResolution.value.startedAtUtc)
        >= activeResolution.value.durationMs
    ) {
      finishTimeline()
    }
    gameState.value = nextState
  }

  function receiveResolution(rawResolution: ClashResolution) {
    const resolution = normalizeClashResolution(rawResolution)
    if (!resolution || (gameId.value && resolution.gameId !== gameId.value)) return
    beginResolution(resolution, gameState.value)
  }

  function mutateVisualOverride(
    unitId: string | null,
    mutate: (current: ClashVisualUnitOverride) => ClashVisualUnitOverride,
  ) {
    if (!unitId) return
    const current = visualOverrides.value.get(unitId)
    if (!current) return
    const next = new Map(visualOverrides.value)
    next.set(unitId, mutate(current))
    visualOverrides.value = next
  }

  function startTimelineEvent(event: ClashResolutionEvent) {
    const type = event.type.toLowerCase()
    const animatedUnitId = ['damage', 'bleeddamage', 'death'].includes(type)
      ? null
      : ['block', 'dodge', 'bleedapplied'].includes(type)
        ? event.targetUnitInstanceId
        : event.actorUnitInstanceId
    mutateVisualOverride(animatedUnitId, current => ({
      ...current,
      animation: clashEventAnimation(event.type),
      animationSequence: event.sequence,
    }))
  }

  function impactTimelineEvent(event: ClashResolutionEvent) {
    const eventType = event.type.toLowerCase()
    if (eventType === 'damage' || eventType === 'bleeddamage') {
      mutateVisualOverride(event.targetUnitInstanceId, current => ({
        ...current,
        hp: Math.max(0, current.hp - Math.abs(event.amount)),
        animation: 'hit',
        animationSequence: event.sequence,
      }))
    }
    else if (eventType === 'death') {
      mutateVisualOverride(event.targetUnitInstanceId ?? event.actorUnitInstanceId, current => ({
        ...current,
        hp: 0,
        alive: false,
        animation: 'death',
        animationSequence: event.sequence,
      }))
    }
    else if (eventType === 'advance') {
      mutateVisualOverride(event.actorUnitInstanceId, current => ({
        ...current,
        boardRow: event.toBoardRow ?? current.boardRow,
        column: event.column ?? current.column,
        animation: 'advance',
        animationSequence: event.sequence,
      }))
    }
    else if (eventType === 'block') {
      mutateVisualOverride(event.targetUnitInstanceId, current => ({
        ...current,
        shieldCharges: Math.max(0, current.shieldCharges - 1),
        animation: 'passive',
        animationSequence: event.sequence,
      }))
    }
    else if (eventType === 'dodge') {
      mutateVisualOverride(event.targetUnitInstanceId, current => ({
        ...current,
        dodgeCharges: Math.max(0, current.dodgeCharges - 1),
        animation: 'passive',
        animationSequence: event.sequence,
      }))
    }
    else if (eventType === 'bleedapplied') {
      mutateVisualOverride(event.targetUnitInstanceId, current => ({
        ...current,
        bleedStacks: current.bleedStacks + Math.max(1, Math.abs(event.amount)),
        animation: 'passive',
        animationSequence: event.sequence,
      }))
    }
  }

  function finishTimeline() {
    timelinePlaying.value = false
    visualOverrides.value = new Map()
  }

  function initCallbacks() {
    signalrService.onClashLobby = (state) => {
      lobbyState.value = {
        games: Array.isArray(state.games) ? state.games : [],
      }
    }
    signalrService.onClashState = receiveState
    signalrService.onClashCatalog = (state) => {
      catalog.value = normalizeClashCatalog(state)
    }
    signalrService.onClashMyActiveGame = (data) => {
      myActiveGameId.value = typeof data.gameId === 'string' ? data.gameId : null
    }
    signalrService.onClashResolution = receiveResolution
    signalrService.onClashGameCreated = (data) => {
      createSucceeded = true
      isCreating.value = false
      navigationGameId.value = data.gameId
    }
    signalrService.onClashGameJoined = (data) => {
      navigationGameId.value = data.gameId
    }
    signalrService.onClashActionResult = (result) => {
      if (!result.success) {
        if (result.error) setError(result.error)
        // A failed optimistic command (most often two parallel front-row
        // placements crossing in flight) does not trigger a server push.
        // Refresh so the next command always carries the current revision.
        void requestState().catch(() => undefined)
      }
    }
    signalrService.onClashError = (error) => {
      createSucceeded = false
      isCreating.value = false
      setError(error)
    }
  }

  function cleanupCallbacks() {
    signalrService.onClashLobby = null
    signalrService.onClashState = null
    signalrService.onClashCatalog = null
    signalrService.onClashResolution = null
    signalrService.onClashGameCreated = null
    signalrService.onClashGameJoined = null
    signalrService.onClashActionResult = null
    signalrService.onClashError = null
    signalrService.onClashMyActiveGame = null
  }

  function consumeNavigationGameId() {
    const id = navigationGameId.value
    navigationGameId.value = null
    return id
  }

  async function refreshLobby() {
    await runCommand(() => signalrService.requestClashLobby())
  }

  async function requestCatalog() {
    await runCommand(() => signalrService.requestClashCatalog())
  }

  async function createGame(vsBot: boolean, width: number, length: number) {
    if (isCreating.value) return false
    createSucceeded = false
    isCreating.value = true
    try {
      await runCommand(() => signalrService.createClashGame(vsBot, width, length))
      if (!createSucceeded) isCreating.value = false
      return createSucceeded
    }
    catch {
      isCreating.value = false
      return false
    }
  }

  async function joinWebGame(id: string) {
    await runCommand(() => signalrService.joinClashWebGame(id))
  }

  async function leaveWebGame(id: string) {
    await runCommand(() => signalrService.leaveClashWebGame(id))
  }

  async function joinGame(id: string) {
    await runCommand(() => signalrService.joinClashGame(id))
  }

  async function leaveGame(id: string) {
    await runCommand(() => signalrService.leaveClashGame(id))
  }

  async function requestState(id = gameId.value) {
    if (!id) return
    await runCommand(() => signalrService.requestClashState(id))
  }

  async function setConfiguration(width: number, length: number) {
    if (!gameId.value) return
    const meta = makeCommandMeta()
    await runCommand(() => signalrService.clashSetConfiguration(
      gameId.value!,
      width,
      length,
      meta.expectedRevision,
      meta.commandId,
    ))
  }

  async function setArmy(unitDefinitionIds: string[]) {
    if (!gameId.value) return
    const meta = makeCommandMeta()
    await runCommand(() => signalrService.clashSetArmy(
      gameId.value!,
      unitDefinitionIds,
      meta.expectedRevision,
      meta.commandId,
    ))
  }

  async function confirmLobbyReady() {
    if (!gameId.value) return
    const meta = makeCommandMeta()
    await runCommand(() => signalrService.clashConfirmLobbyReady(
      gameId.value!,
      meta.expectedRevision,
      meta.commandId,
    ))
  }

  async function placeUnit(unitInstanceId: string, row: number, column: number) {
    if (!gameId.value) return
    const meta = makeCommandMeta()
    await runCommand(() => signalrService.clashPlaceUnit(
      gameId.value!,
      unitInstanceId,
      row,
      column,
      meta.expectedRevision,
      meta.commandId,
    ))
  }

  async function removeUnit(unitInstanceId: string) {
    if (!gameId.value) return
    const meta = makeCommandMeta()
    await runCommand(() => signalrService.clashRemoveUnit(
      gameId.value!,
      unitInstanceId,
      meta.expectedRevision,
      meta.commandId,
    ))
  }

  async function confirmPlacement() {
    if (!gameId.value) return
    const meta = makeCommandMeta()
    await runCommand(() => signalrService.clashConfirmPlacement(
      gameId.value!,
      meta.expectedRevision,
      meta.commandId,
    ))
  }

  async function placeReinforcement(unitInstanceId: string, row: number, column: number) {
    if (!gameId.value) return
    const meta = makeCommandMeta()
    await runCommand(() => signalrService.clashPlaceReinforcement(
      gameId.value!,
      unitInstanceId,
      row,
      column,
      meta.expectedRevision,
      meta.commandId,
    ))
  }

  async function useActive(
    sourceUnitInstanceId: string,
    ability: Pick<ClashAbilityDefinition, 'id'>,
    targetUnitInstanceId: string | null,
    targetRow: number | null,
    targetColumn: number | null,
  ) {
    if (!gameId.value) return
    const meta = makeCommandMeta()
    await runCommand(() => signalrService.clashUseActive(
      gameId.value!,
      sourceUnitInstanceId,
      ability.id,
      targetUnitInstanceId,
      targetRow,
      targetColumn,
      meta.expectedRevision,
      meta.commandId,
    ))
  }

  async function continuePhase() {
    if (!gameId.value) return
    const meta = makeCommandMeta()
    await runCommand(() => signalrService.clashContinue(
      gameId.value!,
      meta.expectedRevision,
      meta.commandId,
    ))
  }

  async function forfeit() {
    if (!gameId.value) return
    const meta = makeCommandMeta()
    await runCommand(() => signalrService.clashForfeit(
      gameId.value!,
      meta.expectedRevision,
      meta.commandId,
    ))
  }

  return {
    lobbyState,
    gameState,
    catalog,
    errorMessage,
    isCreating,
    navigationGameId,
    myActiveGameId,
    activeResolution,
    resolutionEvents,
    timelinePlaying,
    visualOverrides,
    gameId,
    revision,
    phase,
    phaseKind,
    isMyTurn,
    isBusy,
    myPlayer,
    opponent,
    catalogById,
    boardCells,
    boardUnits,
    initCallbacks,
    cleanupCallbacks,
    consumeNavigationGameId,
    refreshLobby,
    requestCatalog,
    createGame,
    joinWebGame,
    leaveWebGame,
    joinGame,
    leaveGame,
    requestState,
    setConfiguration,
    setArmy,
    confirmLobbyReady,
    placeUnit,
    removeUnit,
    confirmPlacement,
    placeReinforcement,
    useActive,
    continuePhase,
    forfeit,
    startTimelineEvent,
    impactTimelineEvent,
    finishTimeline,
    fieldCellKey,
  }
})
