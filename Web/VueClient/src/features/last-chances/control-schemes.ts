import type {
  LastChancesAttackSetControlDefinition,
  LastChancesControlContext,
  LastChancesControlIntent,
  LastChancesControlPhase,
  LastChancesDualSenseInputDefinition,
  LastChancesGesture,
  LastChancesHand,
  LastChancesMylorikInputDefinition,
  LastChancesTactileProfile,
} from './types'

export type LastChancesControlSource = 'keyboard' | 'pointer' | 'gamepad'
export type LastChancesControlDispatchResult = 'handled' | 'buffer' | 'blocked' | 'observe'

export interface LastChancesSemanticInputEvent {
  scheme: 'mylorik' | 'dualsense'
  /** Physical shoulder/hand cluster. New schemes map it to the opposite legacy action-set hand. */
  physicalHand: LastChancesHand
  /** Stable runtime hand/action-set identifier consumed by existing gameplay executors. */
  hand: LastChancesHand
  intent: LastChancesControlIntent
  phase: LastChancesControlPhase
  context?: LastChancesControlContext
  source: LastChancesControlSource
  atMs: number
  heldMs: number
  value: number
  gesture?: LastChancesGesture
  nodeId?: string
  tactileProfile?: LastChancesTactileProfile
  /** False events teach/update feedback but never authorize gameplay. */
  commit: boolean
  /** Availability probe used to choose among authored graph branches; it has no side effects. */
  probe?: boolean
  /** A node that was accepted earlier in this same trigger pull. */
  armed?: boolean
  /** Quick release before any combo node was reached: dispatch the set's preGateGesture. */
  preGate?: boolean
}

export type LastChancesSemanticInputHandler = (
  event: LastChancesSemanticInputEvent,
) => LastChancesControlDispatchResult

/**
 * DeepList names its catalog action sets left(primary)/right(secondary). The two new
 * schemes deliberately put primary on the physical right cluster and support on left.
 */
export function physicalClusterToRuntimeHand(hand: LastChancesHand): LastChancesHand {
  return hand === 'left' ? 'right' : 'left'
}

export function runtimeHandToPhysicalCluster(hand: LastChancesHand): LastChancesHand {
  return hand === 'left' ? 'right' : 'left'
}

interface MylorikHandState {
  techniqueDown: boolean
  techniqueStartedAt: number
  techniqueArmed: boolean
  techniqueCommitted: boolean
  techniqueContinuationSource: LastChancesControlSource | null
  pendingMobilityContinuationSource: LastChancesControlSource | null
  mobilityDown: boolean
  mobilityStartedAt: number
  mobilityCommitted: boolean
}

interface BufferedMylorikEvent {
  event: LastChancesSemanticInputEvent
  expiresAt: number
}

function mylorikHandState(): MylorikHandState {
  return {
    techniqueDown: false,
    techniqueStartedAt: 0,
    techniqueArmed: false,
    techniqueCommitted: false,
    techniqueContinuationSource: null,
    pendingMobilityContinuationSource: null,
    mobilityDown: false,
    mobilityStartedAt: 0,
    mobilityCommitted: false,
  }
}

export interface MylorikControlSnapshot {
  physicalHand: LastChancesHand
  hand: LastChancesHand
  techniquePressed: boolean
  techniqueArmed: boolean
  techniqueHeldMs: number
  mobilityPressed: boolean
  mobilityHeldMs: number
  buffered: boolean
}

export class MylorikControlRecognizer {
  private readonly states: Record<LastChancesHand, MylorikHandState> = {
    left: mylorikHandState(),
    right: mylorikHandState(),
  }
  private buffered: BufferedMylorikEvent | null = null

  constructor(
    private timings: LastChancesMylorikInputDefinition,
    private readonly emit: LastChancesSemanticInputHandler,
  ) {}

  updateConfig(timings: LastChancesMylorikInputDefinition): void {
    this.timings = timings
  }

  pressStrike(
    physicalHand: LastChancesHand,
    atMs: number,
    source: LastChancesControlSource,
  ): void {
    const state = this.states[physicalHand]
    if (state.techniqueDown) this.armTechnique(state, atMs)
    if (state.techniqueDown && state.techniqueArmed && !state.techniqueCommitted) {
      state.techniqueCommitted = true
      const continuation = this.event(
        physicalHand,
        'strike',
        'press',
        source,
        atMs,
        Math.max(0, atMs - state.techniqueStartedAt),
        'continuation',
      )
      continuation.commit = false
      const result = this.dispatch(continuation)
      state.techniqueContinuationSource = result === 'blocked' ? null : source
      return
    }
    this.dispatch(this.event(physicalHand, 'strike', 'press', source, atMs, 0))
  }

  pressTechnique(
    physicalHand: LastChancesHand,
    atMs: number,
    _source: LastChancesControlSource,
  ): void {
    const state = this.states[physicalHand]
    if (state.techniqueDown) return
    state.techniqueDown = true
    state.techniqueStartedAt = atMs
    state.techniqueArmed = false
    state.techniqueCommitted = false
    state.techniqueContinuationSource = null
    state.pendingMobilityContinuationSource = null
  }

  releaseTechnique(
    physicalHand: LastChancesHand,
    atMs: number,
    source: LastChancesControlSource,
  ): void {
    const state = this.states[physicalHand]
    if (!state.techniqueDown) return
    this.armTechnique(state, atMs)
    const heldMs = Math.max(0, atMs - state.techniqueStartedAt)
    const committed = state.techniqueCommitted
    const continuationSource = state.techniqueContinuationSource
    const pendingMobilityContinuationSource = state.pendingMobilityContinuationSource
    const armed = state.techniqueArmed
    state.techniqueDown = false
    state.techniqueStartedAt = 0
    state.techniqueArmed = false
    state.techniqueCommitted = false
    state.techniqueContinuationSource = null
    state.pendingMobilityContinuationSource = null
    if (pendingMobilityContinuationSource) {
      this.dispatch(this.event(
        physicalHand,
        'mobility',
        'release',
        pendingMobilityContinuationSource,
        atMs,
        heldMs,
        'continuation',
      ))
      return
    }
    if (continuationSource) {
      this.dispatch(this.event(
        physicalHand,
        'strike',
        'press',
        continuationSource,
        atMs,
        heldMs,
        'continuation',
      ))
      return
    }
    if (committed) return
    this.dispatch(this.event(
      physicalHand,
      'technique',
      armed ? 'hold' : 'tap',
      source,
      atMs,
      heldMs,
    ))
  }

  pressMobility(
    physicalHand: LastChancesHand,
    atMs: number,
    source: LastChancesControlSource,
  ): void {
    const state = this.states[physicalHand]
    if (state.mobilityDown) return
    state.mobilityDown = true
    state.mobilityStartedAt = atMs
    state.mobilityCommitted = false
    if (state.techniqueDown) this.armTechnique(state, atMs)
    const context = state.techniqueDown && state.techniqueArmed ? 'continuation' : undefined
    if (context) {
      const deferredRelease = this.event(
        physicalHand,
        'mobility',
        'release',
        source,
        atMs,
        Math.max(0, atMs - state.techniqueStartedAt),
        context,
      )
      deferredRelease.commit = false
      deferredRelease.probe = true
      if (this.emit(deferredRelease) === 'handled') {
        state.techniqueCommitted = true
        state.pendingMobilityContinuationSource = source
        state.mobilityCommitted = true
        return
      }
      state.techniqueCommitted = true
    }
    const result = this.dispatch(this.event(
      physicalHand,
      'mobility',
      'press',
      source,
      atMs,
      context ? Math.max(0, atMs - state.techniqueStartedAt) : 0,
      context,
    ))
    state.mobilityCommitted = result === 'handled'
  }

  releaseMobility(
    physicalHand: LastChancesHand,
    atMs: number,
    source: LastChancesControlSource,
  ): void {
    const state = this.states[physicalHand]
    if (!state.mobilityDown) return
    const heldMs = Math.max(0, atMs - state.mobilityStartedAt)
    const committed = state.mobilityCommitted
    state.mobilityDown = false
    state.mobilityStartedAt = 0
    state.mobilityCommitted = false
    if (committed) return
    this.dispatch(this.event(
      physicalHand,
      'mobility',
      heldMs >= this.timings.techniqueHoldMs ? 'hold' : 'release',
      source,
      atMs,
      heldMs,
    ))
  }

  update(atMs: number): void {
    for (const hand of ['left', 'right'] as const) this.armTechnique(this.states[hand], atMs)
    if (!this.buffered) return
    if (atMs > this.buffered.expiresAt) {
      this.buffered = null
      return
    }
    const result = this.emit(this.buffered.event)
    if (result === 'handled' || result === 'blocked') this.buffered = null
  }

  snapshot(physicalHand: LastChancesHand, atMs: number): MylorikControlSnapshot {
    const state = this.states[physicalHand]
    this.armTechnique(state, atMs)
    return {
      physicalHand,
      hand: physicalClusterToRuntimeHand(physicalHand),
      techniquePressed: state.techniqueDown,
      techniqueArmed: state.techniqueArmed,
      techniqueHeldMs: state.techniqueDown ? Math.max(0, atMs - state.techniqueStartedAt) : 0,
      mobilityPressed: state.mobilityDown,
      mobilityHeldMs: state.mobilityDown ? Math.max(0, atMs - state.mobilityStartedAt) : 0,
      buffered: this.buffered?.event.physicalHand === physicalHand,
    }
  }

  reset(): void {
    this.states.left = mylorikHandState()
    this.states.right = mylorikHandState()
    this.buffered = null
  }

  private armTechnique(state: MylorikHandState, atMs: number): void {
    if (state.techniqueDown
      && atMs - state.techniqueStartedAt >= this.timings.techniqueHoldMs) {
      state.techniqueArmed = true
    }
  }

  private event(
    physicalHand: LastChancesHand,
    intent: LastChancesControlIntent,
    phase: LastChancesControlPhase,
    source: LastChancesControlSource,
    atMs: number,
    heldMs: number,
    context?: LastChancesControlContext,
  ): LastChancesSemanticInputEvent {
    return {
      scheme: 'mylorik',
      physicalHand,
      hand: physicalClusterToRuntimeHand(physicalHand),
      intent,
      phase,
      ...(context ? { context } : {}),
      source,
      atMs,
      heldMs,
      value: phase === 'release' ? 0 : 1,
      commit: true,
    }
  }

  private dispatch(event: LastChancesSemanticInputEvent): LastChancesControlDispatchResult {
    const result = this.emit(event)
    if (result === 'buffer') {
      this.buffered = {
        event,
        expiresAt: event.atMs + this.timings.bufferMs,
      }
    }
    return result
  }
}

interface DualSenseTriggerState {
  active: boolean
  needsRelease: boolean
  startedAt: number
  value: number
  maxValue: number
  activeNodeId: string | null
  routeLocked: boolean
  entryLegalNodeIds: Set<string>
  source: LastChancesControlSource
}

function dualSenseTriggerState(needsRelease = false): DualSenseTriggerState {
  return {
    active: false,
    needsRelease,
    startedAt: 0,
    value: 0,
    maxValue: 0,
    activeNodeId: null,
    routeLocked: false,
    entryLegalNodeIds: new Set<string>(),
    source: 'gamepad',
  }
}

export interface DualSenseTriggerSnapshot {
  physicalHand: LastChancesHand
  hand: LastChancesHand
  active: boolean
  value: number
  maxValue: number
  heldMs: number
  nodeId: string | null
}

function clampTrigger(value: number): number {
  if (!Number.isFinite(value)) return 0
  return Math.max(0, Math.min(1, value))
}

export class DualSenseControlRecognizer {
  private readonly states: Record<LastChancesHand, DualSenseTriggerState> = {
    left: dualSenseTriggerState(),
    right: dualSenseTriggerState(),
  }

  constructor(
    private config: LastChancesDualSenseInputDefinition,
    private readonly emit: LastChancesSemanticInputHandler,
  ) {}

  updateConfig(config: LastChancesDualSenseInputDefinition): void {
    this.config = config
  }

  pressBumper(
    physicalHand: LastChancesHand,
    atMs: number,
    source: LastChancesControlSource,
  ): void {
    this.emit({
      scheme: 'dualsense',
      physicalHand,
      hand: physicalClusterToRuntimeHand(physicalHand),
      intent: 'strike',
      phase: 'press',
      source,
      atMs,
      heldMs: 0,
      value: 1,
      commit: true,
    })
  }

  updateTrigger(
    physicalHand: LastChancesHand,
    value: number,
    atMs: number,
    controls: LastChancesAttackSetControlDefinition | undefined,
    source: LastChancesControlSource,
  ): void {
    const state = this.states[physicalHand]
    const normalized = clampTrigger(value)
    state.value = normalized
    const releaseGate = this.config.releaseThreshold
    const neutralRearmGate = Math.max(0, releaseGate - this.config.hysteresis)
    if (state.needsRelease) {
      if (normalized <= neutralRearmGate) this.states[physicalHand] = dualSenseTriggerState()
      return
    }
    const nodes = [...(controls?.dualsense.nodes ?? [])]
      .sort((left, right) => left.activationThreshold - right.activationThreshold)
    if (!state.active && normalized >= this.config.activationThreshold) {
      state.active = true
      state.startedAt = atMs
      state.maxValue = normalized
      state.source = source
      state.entryLegalNodeIds = new Set(nodes
        .filter(node => node.entryContext === 'neutral')
        .filter((node) => this.emit({
          scheme: 'dualsense',
          physicalHand,
          hand: physicalClusterToRuntimeHand(physicalHand),
          intent: 'technique',
          phase: 'hold',
          context: node.entryContext,
          source,
          atMs,
          heldMs: 0,
          value: normalized,
          gesture: node.gesture,
          nodeId: node.id,
          tactileProfile: node.tactileProfile,
          commit: false,
          probe: true,
        }) === 'handled')
        .map(node => node.id))
    }
    if (!state.active) return

    state.maxValue = Math.max(state.maxValue, normalized)
    if (normalized > releaseGate) {
      const byId = new Map(nodes.map(node => [node.id, node]))
      const reachable: typeof nodes = []
      const visited = new Set<string>()
      const visit = (nodeId: string): void => {
        if (visited.has(nodeId)) return
        visited.add(nodeId)
        const node = byId.get(nodeId)
        if (!node || state.maxValue < node.activationThreshold) return
        reachable.push(node)
        node.next.forEach(visit)
      }
      const activeNode = state.activeNodeId ? byId.get(state.activeNodeId) : undefined
      const routeHeads = state.routeLocked && activeNode
        ? activeNode.next
        : (controls?.dualsense.startNodeId ? [controls.dualsense.startNodeId] : [])
      routeHeads.forEach(visit)

      const eligible = reachable.filter((node) => node.entryContext === 'neutral'
        ? state.entryLegalNodeIds.has(node.id)
        : this.emit({
            scheme: 'dualsense',
            physicalHand,
            hand: physicalClusterToRuntimeHand(physicalHand),
            intent: 'technique',
            phase: 'hold',
            context: node.entryContext,
            source,
            atMs,
            heldMs: Math.max(0, atMs - state.startedAt),
            value: normalized,
            gesture: node.gesture,
            nodeId: node.id,
            tactileProfile: node.tactileProfile,
            commit: false,
            probe: true,
          }) === 'handled')
        .sort((left, right) => right.activationThreshold - left.activationThreshold)
      const selected = eligible[0]
      const advances = selected && selected.id !== activeNode?.id
        && (!activeNode || selected.activationThreshold >= activeNode.activationThreshold)
      if (selected && advances) {
        const result = this.emit({
          scheme: 'dualsense',
          physicalHand,
          hand: physicalClusterToRuntimeHand(physicalHand),
          intent: 'technique',
          phase: 'hold',
          context: selected.entryContext,
          source,
          atMs,
          heldMs: Math.max(0, atMs - state.startedAt),
          value: normalized,
          gesture: selected.gesture,
          nodeId: selected.id,
          tactileProfile: selected.tactileProfile,
          commit: selected.dispatch === 'press',
          armed: state.entryLegalNodeIds.has(selected.id),
        })
        if (result === 'handled') {
          state.activeNodeId = selected.id
          if (selected.dispatch === 'press') state.routeLocked = true
        }
        if (result === 'blocked') {
          this.states[physicalHand] = dualSenseTriggerState(true)
          return
        }
      } else if (!selected && !activeNode && reachable.length > 0) {
        // Reachable nodes exist but none is eligible: an illegal pull. A pull
        // still below every authored gate (reachable empty — the pre-gate
        // zone) instead stays active, waiting to reach the first pocket or to
        // release into the set's preGateGesture.
        const blockedNode = reachable.reduce<typeof reachable[number] | undefined>(
          (deepest, node) => !deepest || node.activationThreshold > deepest.activationThreshold
            ? node
            : deepest,
          undefined,
        )
        if (blockedNode) {
          this.emit({
            scheme: 'dualsense',
            physicalHand,
            hand: physicalClusterToRuntimeHand(physicalHand),
            intent: 'technique',
            phase: 'hold',
            context: blockedNode.entryContext,
            source,
            atMs,
            heldMs: Math.max(0, atMs - state.startedAt),
            value: normalized,
            gesture: blockedNode.gesture,
            nodeId: blockedNode.id,
            tactileProfile: blockedNode.tactileProfile,
            commit: true,
          })
        }
        this.states[physicalHand] = dualSenseTriggerState(true)
        return
      }
    }

    if (normalized > releaseGate) return
    const node = nodes.find(candidate => candidate.id === state.activeNodeId)
    if (node && node.releaseBehavior === 'dispatch') {
      this.emit({
        scheme: 'dualsense',
        physicalHand,
        hand: physicalClusterToRuntimeHand(physicalHand),
        intent: 'technique',
        phase: 'release',
        context: node.entryContext,
        source: state.source,
        atMs,
        heldMs: Math.max(0, atMs - state.startedAt),
        value: normalized,
        gesture: node.gesture,
        nodeId: node.id,
        tactileProfile: node.tactileProfile,
        commit: true,
        armed: true,
      })
    } else if (!state.activeNodeId && controls?.dualsense.preGateGesture) {
      // The pull never reached a combo node: the "click before the gate"
      // dispatches the authored quick action (e.g. the spear's distance poke).
      this.emit({
        scheme: 'dualsense',
        physicalHand,
        hand: physicalClusterToRuntimeHand(physicalHand),
        intent: 'technique',
        phase: 'release',
        source: state.source,
        atMs,
        heldMs: Math.max(0, atMs - state.startedAt),
        value: normalized,
        gesture: controls.dualsense.preGateGesture,
        commit: true,
        preGate: true,
      })
    }
    this.states[physicalHand] = dualSenseTriggerState(normalized > neutralRearmGate)
  }

  update(
    atMs: number,
    controls: (physicalHand: LastChancesHand) => LastChancesAttackSetControlDefinition | undefined,
  ): void {
    for (const physicalHand of ['left', 'right'] as const) {
      const state = this.states[physicalHand]
      if (!state.active || !state.activeNodeId) continue
      const node = controls(physicalHand)?.dualsense.nodes
        .find(candidate => candidate.id === state.activeNodeId)
      if (!node || node.cancel !== 'expiry' || atMs - state.startedAt < node.expiryMs) continue
      this.states[physicalHand] = dualSenseTriggerState(true)
    }
  }

  snapshot(physicalHand: LastChancesHand, atMs: number): DualSenseTriggerSnapshot {
    const state = this.states[physicalHand]
    return {
      physicalHand,
      hand: physicalClusterToRuntimeHand(physicalHand),
      active: state.active,
      value: state.value,
      maxValue: state.maxValue,
      heldMs: state.active ? Math.max(0, atMs - state.startedAt) : 0,
      nodeId: state.activeNodeId,
    }
  }

  reset(): void {
    this.states.left = dualSenseTriggerState()
    this.states.right = dualSenseTriggerState()
  }
}
