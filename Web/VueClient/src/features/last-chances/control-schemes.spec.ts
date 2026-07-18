import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/99lc/game-config.json'
import {
  DualSenseControlRecognizer,
  MylorikControlRecognizer,
  physicalClusterToRuntimeHand,
  type LastChancesControlDispatchResult,
  type LastChancesSemanticInputEvent,
} from './control-schemes'
import type {
  LastChancesAttackSetControlDefinition,
  LastChancesConfig,
  LastChancesDualSenseInputDefinition,
  LastChancesMylorikInputDefinition,
  LastChancesTactileProfile,
} from './types'

const defaultConfig = defaultConfigJson as unknown as LastChancesConfig

const keyboard = {
  leftTechniqueKeys: ['KeyQ'],
  rightTechniqueKeys: ['KeyE'],
  mobilityKeys: ['Space'],
  interactKeys: ['KeyF'],
  leftStrikeMouseButton: 0,
  rightStrikeMouseButton: 2,
}

const mylorikConfig: LastChancesMylorikInputDefinition = {
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
  keyboard,
}

function profile(): Record<LastChancesTactileProfile, {
  startPosition: number
  endPosition: number
  resistance: number
  force: number
  transitionMs: number
  effectMs: number
  magnitude: number
}> {
  const value = {
    startPosition: 0.2,
    endPosition: 0.8,
    resistance: 0.4,
    force: 0.4,
    transitionMs: 40,
    effectMs: 80,
    magnitude: 0.4,
  }
  return {
    click: { ...value },
    ramp: { ...value },
    bandLight: { ...value },
    bandMedium: { ...value },
    bandStrong: { ...value },
    gate: { ...value },
    followUp: { ...value },
    blocked: { ...value },
    impact: { ...value },
    tension: { ...value },
  }
}

const dualSenseConfig: LastChancesDualSenseInputDefinition = {
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
  keyboard,
  gatePositions: { shallow: 0.22, medium: 0.48, deep: 0.72, final: 0.9 },
  feedback: {
    maxMagnitude: 0.8,
    maxDurationMs: 500,
    blockedRepeatMs: 180,
    profiles: profile(),
  },
}

const controls: LastChancesAttackSetControlDefinition = {
  role: 'Test route',
  mylorik: {
    activations: [{
      gesture: 'tap',
      intent: 'strike',
      phase: 'press',
      priority: 100,
    }],
  },
  dualsense: {
    instantGesture: 'tap',
    triggerRole: 'Test trigger',
    startNodeId: 'shallow',
    nodes: [
      {
        id: 'shallow',
        gesture: 'doubleTap',
        entryContext: 'neutral',
        activationThreshold: 0.22,
        dispatch: 'release',
        holdBehavior: 'none',
        releaseBehavior: 'dispatch',
        next: ['deep'],
        cancel: 'release',
        expiryMs: 800,
        tactileProfile: 'click',
      },
      {
        id: 'deep',
        gesture: 'hold',
        entryContext: 'neutral',
        activationThreshold: 0.48,
        dispatch: 'release',
        holdBehavior: 'charge',
        releaseBehavior: 'dispatch',
        next: [],
        cancel: 'release',
        expiryMs: 1600,
        tactileProfile: 'ramp',
      },
    ],
  },
}

describe('mylorik control recognizer', () => {
  it('dispatches strike on down and technique tap on release without a classifier wait', () => {
    const events: LastChancesSemanticInputEvent[] = []
    const recognizer = new MylorikControlRecognizer(mylorikConfig, (event) => {
      events.push(event)
      return 'handled'
    })

    recognizer.pressStrike('left', 10, 'gamepad')
    recognizer.pressTechnique('right', 20, 'keyboard')
    recognizer.releaseTechnique('right', 120, 'keyboard')

    expect(events.map(event => [event.intent, event.phase, event.atMs])).toEqual([
      ['strike', 'press', 10],
      ['technique', 'tap', 120],
    ])
    expect(events[0].hand).toBe(physicalClusterToRuntimeHand('left'))
  })

  it('arms at the inclusive hold threshold and resolves immediately on release', () => {
    const events: LastChancesSemanticInputEvent[] = []
    const recognizer = new MylorikControlRecognizer(mylorikConfig, (event) => {
      events.push(event)
      return 'handled'
    })

    recognizer.pressTechnique('left', 100, 'keyboard')
    expect(recognizer.snapshot('left', 749).techniqueArmed).toBe(false)
    expect(recognizer.snapshot('left', 750).techniqueArmed).toBe(true)
    recognizer.releaseTechnique('left', 750, 'keyboard')

    expect(events).toHaveLength(1)
    expect(events[0]).toMatchObject({ phase: 'hold', heldMs: 650, atMs: 750 })
  })

  it('routes a bumper during an armed technique as its held continuation', () => {
    const events: LastChancesSemanticInputEvent[] = []
    const recognizer = new MylorikControlRecognizer(mylorikConfig, (event) => {
      events.push(event)
      return 'handled'
    })

    recognizer.pressTechnique('right', 100, 'gamepad')
    recognizer.pressStrike('right', 750, 'gamepad')
    recognizer.releaseTechnique('right', 900, 'gamepad')

    expect(events).toEqual([
      expect.objectContaining({
        intent: 'strike',
        phase: 'press',
        context: 'continuation',
        heldMs: 650,
        commit: false,
      }),
      expect.objectContaining({
        intent: 'strike',
        phase: 'press',
        context: 'continuation',
        heldMs: 800,
        commit: true,
      }),
    ])
  })

  it('resolves a held mobility input as hold and does not repeat a press continuation', () => {
    const events: LastChancesSemanticInputEvent[] = []
    const recognizer = new MylorikControlRecognizer(mylorikConfig, (event) => {
      events.push(event)
      return event.phase === 'press' && event.context === 'continuation' ? 'handled' : 'observe'
    })

    recognizer.pressMobility('left', 0, 'keyboard')
    recognizer.releaseMobility('left', 650, 'keyboard')
    recognizer.pressTechnique('right', 1_000, 'keyboard')
    recognizer.pressMobility('right', 1_650, 'keyboard')
    recognizer.releaseMobility('right', 1_700, 'keyboard')
    recognizer.releaseTechnique('right', 1_750, 'keyboard')

    expect(events.filter(event => !event.probe)
      .map(event => [event.physicalHand, event.phase, event.context])).toEqual([
      ['left', 'press', undefined],
      ['left', 'hold', undefined],
      ['right', 'press', 'continuation'],
    ])
  })

  it('arms the Knife-spider throw with trigger plus Circle and commits it on trigger release', () => {
    const events: LastChancesSemanticInputEvent[] = []
    const committedGestures: string[] = []
    const activations = defaultConfig.weapons
      .find(weapon => weapon.id === 'secondary-spider-knife')!
      .controls!.primary.mylorik.activations
    const recognizer = new MylorikControlRecognizer(mylorikConfig, (event) => {
      events.push(event)
      const activation = activations.find(candidate => (
        candidate.intent === event.intent
        && candidate.phase === event.phase
        && candidate.context === event.context
      ))
      if (event.probe) return activation ? 'handled' : 'observe'
      if (!activation) return 'observe'
      if (event.commit) committedGestures.push(activation.gesture)
      return 'handled'
    })

    recognizer.pressTechnique('left', 0, 'gamepad')
    recognizer.pressMobility('left', 650, 'gamepad')
    recognizer.releaseMobility('left', 700, 'gamepad')
    recognizer.releaseTechnique('left', 900, 'gamepad')

    expect(events.filter(event => event.probe)).toEqual([
      expect.objectContaining({
        intent: 'mobility',
        phase: 'release',
        context: 'continuation',
        heldMs: 650,
        commit: false,
      }),
    ])
    expect(events.filter(event => event.commit)).toEqual([
      expect.objectContaining({
        intent: 'mobility',
        phase: 'release',
        context: 'continuation',
        atMs: 900,
        heldMs: 900,
      }),
    ])
    expect(committedGestures).toEqual(['doubleTapHold'])
  })

  it('keeps only the latest one-slot buffered intent and reset clears every source', () => {
    const attempts: LastChancesSemanticInputEvent[] = []
    let result: LastChancesControlDispatchResult = 'buffer'
    const recognizer = new MylorikControlRecognizer(mylorikConfig, (event) => {
      attempts.push(event)
      return result
    })
    recognizer.pressStrike('left', 0, 'gamepad')
    recognizer.pressStrike('right', 20, 'keyboard')
    result = 'handled'
    recognizer.update(30)

    expect(attempts[attempts.length - 1]?.physicalHand).toBe('right')
    recognizer.pressTechnique('left', 40, 'keyboard')
    recognizer.reset()
    expect(recognizer.snapshot('left', 700)).toMatchObject({
      techniquePressed: false,
      mobilityPressed: false,
      buffered: false,
    })
  })
})

describe('DualSense control recognizer', () => {
  it('emits each threshold transition once and commits the deepest node on release', () => {
    const events: LastChancesSemanticInputEvent[] = []
    const recognizer = new DualSenseControlRecognizer(dualSenseConfig, (event) => {
      events.push(event)
      return 'handled'
    })

    recognizer.updateTrigger('right', 0.22, 100, controls, 'gamepad')
    recognizer.updateTrigger('right', 0.3, 130, controls, 'gamepad')
    recognizer.updateTrigger('right', 0.55, 180, controls, 'gamepad')
    recognizer.updateTrigger('right', 0.5, 210, controls, 'gamepad')
    recognizer.updateTrigger('right', 0.1, 410, controls, 'gamepad')

    expect(events.filter(event => !event.probe && !event.commit).map(event => event.nodeId)).toEqual([
      'shallow',
      'deep',
    ])
    expect(events.filter(event => event.commit)).toEqual([
      expect.objectContaining({ nodeId: 'deep', gesture: 'hold', phase: 'release', heldMs: 310 }),
    ])
  })

  it('uses release hysteresis and keeps bumpers independent and immediate', () => {
    const events: LastChancesSemanticInputEvent[] = []
    const recognizer = new DualSenseControlRecognizer(dualSenseConfig, (event) => {
      events.push(event)
      return 'handled'
    })
    recognizer.updateTrigger('left', 0.23, 0, controls, 'gamepad')
    recognizer.updateTrigger('left', 0.18, 10, controls, 'gamepad')
    recognizer.updateTrigger('left', 0.14, 20, controls, 'gamepad')
    recognizer.pressBumper('left', 25, 'gamepad')
    recognizer.pressBumper('right', 25, 'gamepad')
    recognizer.updateTrigger('left', 0.23, 30, controls, 'gamepad')
    recognizer.updateTrigger('left', 0.05, 40, controls, 'gamepad')
    recognizer.updateTrigger('left', 0.23, 50, controls, 'gamepad')

    expect(events.filter(event => (
      event.nodeId === 'shallow' && !event.probe && !event.commit
    ))).toHaveLength(2)
    expect(events.filter(event => event.intent === 'strike')).toEqual([
      expect.objectContaining({ physicalHand: 'left', atMs: 25, commit: true }),
      expect.objectContaining({ physicalHand: 'right', atMs: 25, commit: true }),
    ])
    expect(events.filter(event => event.phase === 'release')).toEqual([
      expect.objectContaining({ atMs: 20, value: 0.14 }),
    ])
  })

  it('consumes the authored hysteresis as the neutral distance required before re-arm', () => {
    const transitions = (hysteresis: number): LastChancesSemanticInputEvent[] => {
      const events: LastChancesSemanticInputEvent[] = []
      const recognizer = new DualSenseControlRecognizer(
        { ...dualSenseConfig, hysteresis },
        (event) => {
          events.push(event)
          return 'handled'
        },
      )
      recognizer.updateTrigger('right', 0.23, 0, controls, 'gamepad')
      recognizer.updateTrigger('right', 0.1, 10, controls, 'gamepad')
      recognizer.updateTrigger('right', 0.23, 20, controls, 'gamepad')
      return events.filter(event => !event.probe && !event.commit)
    }

    expect(transitions(0.03)).toHaveLength(2)
    expect(transitions(0.08)).toHaveLength(1)
  })

  it('emits one committed blocked request when no probed graph node is legal', () => {
    const events: LastChancesSemanticInputEvent[] = []
    const recognizer = new DualSenseControlRecognizer(dualSenseConfig, (event) => {
      events.push(event)
      return event.nodeId === 'deep' ? 'blocked' : 'observe'
    })

    recognizer.updateTrigger('right', 0.55, 0, controls, 'gamepad')
    recognizer.updateTrigger('right', 0.1, 300, controls, 'gamepad')

    expect(events.map(event => [event.nodeId, event.commit])).toEqual([
      ['shallow', false],
      ['deep', false],
      ['deep', true],
    ])
    expect(recognizer.snapshot('right', 300)).toMatchObject({ active: false, nodeId: null })
  })

  it('requires a physical release before an expired route can re-arm', () => {
    const events: LastChancesSemanticInputEvent[] = []
    const expiring: LastChancesAttackSetControlDefinition = {
      ...controls,
      dualsense: {
        ...controls.dualsense,
        nodes: [{
          ...controls.dualsense.nodes[0],
          next: [],
          cancel: 'expiry',
          expiryMs: 50,
        }],
      },
    }
    const recognizer = new DualSenseControlRecognizer(dualSenseConfig, (event) => {
      events.push(event)
      return 'handled'
    })

    recognizer.updateTrigger('right', 0.22, 0, expiring, 'gamepad')
    recognizer.update(60, () => expiring)
    recognizer.updateTrigger('right', 0.22, 70, expiring, 'gamepad')
    recognizer.updateTrigger('right', 0.05, 80, expiring, 'gamepad')
    recognizer.updateTrigger('right', 0.22, 90, expiring, 'gamepad')

    expect(events.filter(event => !event.probe && event.nodeId === 'shallow')).toHaveLength(2)
  })

  it('locks a gradual Knife-spider pull to twist while a direct final pull throws', () => {
    const spiderControls = defaultConfig.weapons
      .find(weapon => weapon.id === 'secondary-spider-knife')!.controls!.primary
    const runPull = (peak: 0.72 | 0.9, gradual = true): LastChancesSemanticInputEvent[] => {
      const events: LastChancesSemanticInputEvent[] = []
      let flurryActive = false
      const recognizer = new DualSenseControlRecognizer(dualSenseConfig, (event) => {
        events.push(event)
        if (event.probe) {
          if (event.context === 'neutral') return flurryActive ? 'observe' : 'handled'
          if (event.context === 'flurry') return flurryActive ? 'handled' : 'observe'
        }
        if (event.context === 'neutral' && flurryActive && !event.armed) return 'observe'
        if (event.nodeId === 'hold' && event.commit) flurryActive = true
        return 'handled'
      })
      if (gradual) {
        recognizer.updateTrigger('left', 0.22, 0, spiderControls, 'gamepad')
        recognizer.updateTrigger('left', 0.48, 100, spiderControls, 'gamepad')
        recognizer.updateTrigger('left', 0.72, 200, spiderControls, 'gamepad')
        if (peak === 0.9) recognizer.updateTrigger('left', 0.9, 300, spiderControls, 'gamepad')
      } else {
        recognizer.updateTrigger('left', peak, 0, spiderControls, 'gamepad')
      }
      recognizer.updateTrigger('left', 0, 400, spiderControls, 'gamepad')
      return events
    }

    expect(runPull(0.72).filter(event => event.commit && event.phase === 'release')).toEqual([
      expect.objectContaining({ nodeId: 'holdThenDoubleTap', gesture: 'holdThenDoubleTap' }),
    ])
    expect(runPull(0.9).filter(event => event.commit && event.phase === 'release')).toEqual([
      expect.objectContaining({ nodeId: 'holdThenDoubleTap', gesture: 'holdThenDoubleTap' }),
    ])
    const directThrow = runPull(0.9, false)
    expect(directThrow.filter(event => event.commit && event.phase === 'hold')).toHaveLength(0)
    expect(directThrow.filter(event => event.commit && event.phase === 'release')).toEqual([
      expect.objectContaining({ nodeId: 'doubleTapHold', gesture: 'doubleTapHold' }),
    ])
  })
})
