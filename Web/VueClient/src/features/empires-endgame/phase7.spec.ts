import { describe, expect, it } from 'vitest'
import bundledConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, migrateEmpiresConfig, validateEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
import { importEmpiresCampaign } from './persistence'
import { evaluateQuestTriggerStarts } from './quests'
import type {
  EmpiresCampaignState,
  EmpiresEndgameConfig,
  EmpiresQuestDefinition,
  EmpiresQuestState,
} from './types'

function config(): EmpiresEndgameConfig {
  const value = cloneEmpiresConfig(bundledConfigJson)
  value.empire.eventChance = 0
  return value
}

function startPalach(value = config()): EmpiresEndgameEngine {
  const engine = new EmpiresEndgameEngine(value)
  engine.state.con = 2
  ;(engine as unknown as { startEmpirePhase(): void }).startEmpirePhase()
  return engine
}

function choose(engine: EmpiresEndgameEngine, choiceId: string) {
  const result = engine.advanceDialogue('quest-palach', choiceId)
  if (!result.ok) throw new Error(result.message)
}

function eventState(value: EmpiresEndgameConfig, eventId: string): EmpiresCampaignState {
  const state = new EmpiresEndgameEngine(value).snapshot()
  state.phase = 'event'
  state.event = {
    instanceId: `phase7:${eventId}`,
    eventId,
    empireSettlementPending: false,
  }
  state.empire.daysRemaining = 0
  state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  return state
}

describe('Empire\'s Endgame Phase 7 quest and dialogue engine', () => {
  it('ships the complete canonical Палач graph and validates broken graph contracts', () => {
    const value = config()
    const palach = value.quests.definitions.find(quest => quest.id === 'quest-palach')!
    expect(palach.stages.flatMap(stage => stage.nodes)).toHaveLength(43)
    expect(() => validateEmpiresConfig(value)).not.toThrow()

    const orphan = structuredClone(value)
    orphan.quests.definitions[0].stages[0].nodes.push({
      id: 'orphan', speaker: 'X', text: 'X', choices: [], terminal: 'fail',
    })
    expect(() => validateEmpiresConfig(orphan)).toThrow(/orphan node/)

    const brokenGoto = structuredClone(value)
    brokenGoto.quests.definitions[0].stages[0].nodes[0].choices[0].goto = {
      kind: 'node', nodeId: 'missing-node',
    }
    expect(() => validateEmpiresConfig(brokenGoto)).toThrow(/missing node/)

    const cycle = structuredClone(value)
    const nodes = cycle.quests.definitions[0].stages[0].nodes
    const terminal = nodes.find(node => node.id === 'palach-p13')!
    delete terminal.terminal
    terminal.choices = [
      { id: 'loop', label: 'Назад', goto: { kind: 'node', nodeId: 'palach-p12' } },
      { id: 'leave-loop', label: 'Закончить', goto: { kind: 'complete' } },
    ]
    expect(() => validateEmpiresConfig(cycle)).toThrow(/undeclared cycle/)
    cycle.quests.definitions[0].allowedCycles = [{
      id: 'authored-loop', nodeIds: ['palach-p12', 'palach-p13'],
    }]
    expect(() => validateEmpiresConfig(cycle)).not.toThrow()

    const duplicate = structuredClone(value)
    duplicate.quests.definitions.push(structuredClone(duplicate.quests.definitions[0]))
    expect(() => validateEmpiresConfig(duplicate)).toThrow(/unique non-empty ids/)

    const disabledBridge = structuredClone(value)
    disabledBridge.quests.enabled = false
    expect(() => validateEmpiresConfig(disabledBridge)).toThrow(/invalid questResolution bridge/)
  })

  it('matches every supported trigger kind and keeps stable definition order', () => {
    const value = config()
    const base = value.quests.definitions[0]
    const definitions: EmpiresQuestDefinition[] = [
      { ...base, id: 'q-con', trigger: { kind: 'conReached', con: 2 } },
      { ...base, id: 'q-flag', trigger: { kind: 'flag', flagId: 'known', minimum: 1 } },
      { ...base, id: 'q-event', trigger: { kind: 'event', eventId: 'event-golden-idol' }, mandatory: false },
      { ...base, id: 'q-building', trigger: { kind: 'building', buildingId: 'building-farm', level: 1 } },
      { ...base, id: 'q-minigame', trigger: { kind: 'minigameResult', minigameKind: 'td', outcome: 'victory' } },
      { ...base, id: 'q-manual', trigger: { kind: 'manual' } },
    ]
    const readers = { flagValue: () => 1, buildingLevel: () => 1 }
    const contexts = [
      { kind: 'empireStart', con: 2 } as const,
      { kind: 'event', eventId: 'event-golden-idol', eventInstanceId: 'e1' } as const,
      { kind: 'building', buildingId: 'building-farm', cityId: 'c1', level: 1, con: 2 } as const,
      { kind: 'minigameResult', sessionId: 'm1', minigameKind: 'td', outcome: 'victory', con: 2 } as const,
      { kind: 'manual', questId: 'q-manual', sourceId: 'qa', con: 2 } as const,
    ]
    const starts = contexts.flatMap(context => evaluateQuestTriggerStarts(definitions, {}, context, readers))
    expect(starts.map(start => start.definition.id)).toEqual([
      'q-con', 'q-flag', 'q-building', 'q-event', 'q-building', 'q-minigame', 'q-manual',
    ])

    const consumed: EmpiresQuestState = {
      questId: 'q-con', status: 'completed', stageId: base.entryStageId,
      nodeId: base.stages[0].entryNodeId, memory: {}, run: 1, nodeVisit: 1,
      lastAppliedChoiceIdentity: null,
      consumedTriggerIds: [starts[0].triggerIdentity], compactedTriggerCount: 0,
      compactedTriggerDigest: '', compactedTriggerWatermarks: {}, sealedTriggerKinds: [],
      startedAtCon: 2, finishedAtCon: 2,
    }
    expect(evaluateQuestTriggerStarts(definitions, { 'q-con': consumed }, contexts[0], readers)
      .some(start => start.definition.id === 'q-con')).toBe(false)

    const flagConfig = config()
    flagConfig.quests.definitions[0].stages[0].nodes[0].choices[0].effects = [{
      kind: 'flag', flagId: 'questAuthoredTrigger', amount: 1,
    }]
    flagConfig.quests.definitions.push({
      ...structuredClone(base),
      id: 'q-authored-flag',
      trigger: { kind: 'flag', flagId: 'questAuthoredTrigger', minimum: 1 },
    })
    expect(() => validateEmpiresConfig(flagConfig)).not.toThrow()
  })

  it('executes stage, complete, fail, and repeat-after-terminal transitions exactly once', () => {
    const value = config()
    value.empire.events = value.empire.events.filter(event => (
      event.id !== 'event-golden-idol' && event.id !== 'event-witch-apprenticeship'
    ))
    const flow: EmpiresQuestDefinition = {
      id: 'quest-flow',
      name: 'Проверка переходов',
      journalDescription: 'Проверяет полный контракт переходов.',
      trigger: { kind: 'manual', repeatable: true },
      entryStageId: 'opening',
      mandatory: false,
      restartPolicy: 'afterTerminal',
      stages: [
        {
          id: 'opening', name: 'Начало', entryNodeId: 'opening-node', nodes: [{
            id: 'opening-node', speaker: 'Тест', text: 'Начало', choices: [{
              id: 'to-ending', label: 'Дальше', goto: { kind: 'stage', stageId: 'ending' },
            }],
          }],
        },
        {
          id: 'ending', name: 'Финал', entryNodeId: 'ending-node', nodes: [{
            id: 'ending-node', speaker: 'Тест', text: 'Финал', choices: [
              { id: 'complete', label: 'Успех', goto: { kind: 'complete' } },
              { id: 'fail', label: 'Провал', goto: { kind: 'fail' } },
            ],
          }],
        },
      ],
    }
    const expeditionComplaint = value.quests.definitions.find(
      quest => quest.id === 'quest-expedition-south-complaint',
    )!
    value.quests.definitions = [flow, expeditionComplaint]
    value.quests.historyRetention = 2
    value.quests.triggerHistoryRetention = 2
    expect(() => validateEmpiresConfig(value)).not.toThrow()

    const completed = new EmpiresEndgameEngine(value)
    expect(completed.startManualQuest(flow.id, 'source-1').ok).toBe(true)
    expect(completed.advanceDialogue(flow.id, 'to-ending').ok).toBe(true)
    expect(completed.state.quests[flow.id]).toMatchObject({ stageId: 'ending', nodeId: 'ending-node' })
    expect(completed.advanceDialogue(flow.id, 'complete').ok).toBe(true)
    expect(completed.state.quests[flow.id].status).toBe('completed')
    const afterComplete = completed.snapshot()
    expect(completed.advanceDialogue(flow.id, 'complete').ok).toBe(true)
    expect(completed.snapshot()).toEqual(afterComplete)
    expect(completed.startManualQuest(flow.id, 'source-1').ok).toBe(false)
    expect(completed.startManualQuest(flow.id, 'source-2').ok).toBe(true)
    expect(completed.state.quests[flow.id].run).toBe(2)

    expect(completed.advanceDialogue(flow.id, 'to-ending').ok).toBe(true)
    expect(completed.advanceDialogue(flow.id, 'fail').ok).toBe(true)
    expect(completed.state.quests[flow.id].status).toBe('failed')
    expect(completed.state.questRuntime.history).toHaveLength(2)
    expect(completed.state.questRuntime.compactedHistoryCount).toBe(2)
  })

  it('ports scripted Палач paths, explicit time, memory, save-at-node, and mandatory blocking', () => {
    const value = config()
    const engine = startPalach(value)
    const initialDays = engine.state.empire.daysRemaining
    expect(engine.state.questRuntime.activeMandatoryQuestId).toBe('quest-palach')
    expect(engine.finishEmpire().ok).toBe(false)

    choose(engine, 'palach-p28-bed')
    expect(engine.state.empire.daysRemaining).toBe(initialDays)
    choose(engine, 'palach-p30-start')
    choose(engine, 'palach-p01-prisoner')
    expect(engine.state.empire.daysRemaining).toBe(initialDays - 1)

    const restored = new EmpiresEndgameEngine(value, engine.snapshot())
    expect(restored.state.quests['quest-palach'].nodeId).toBe('palach-p02')
    choose(restored, 'palach-p02-point')
    choose(restored, 'palach-p03-personal')
    choose(restored, 'palach-p25-homeland')
    choose(restored, 'palach-p05-west')
    choose(restored, 'palach-p06-executioner')
    choose(restored, 'palach-p33-innocent')
    choose(restored, 'palach-p36-bored')
    choose(restored, 'palach-p31-seriously')
    expect(restored.state.quests['quest-palach'].memory.cardsKnown).toBe(true)
    choose(restored, 'palach-p09-family')
    choose(restored, 'palach-p20-injustice')
    choose(restored, 'palach-p22-brother')
    choose(restored, 'palach-p10-innocent')
    expect(restored.questChoiceBlockedReason('quest-palach', 'palach-p11-cards')).toBeNull()
    choose(restored, 'palach-p11-expedition')
    choose(restored, 'palach-p12-complete')
    expect(restored.state.quests['quest-palach'].status).toBe('completed')
    expect(restored.dismissDialogue('quest-palach').ok).toBe(true)
    expect(restored.state.questRuntime.activeMandatoryQuestId).toBeNull()
  })

  it('rechecks a choice atomically and never charges an unavailable branch', () => {
    const value = config()
    const opening = value.quests.definitions[0].stages[0].nodes
      .find(node => node.id === 'palach-p28')!.choices[0]
    opening.costs = [{ resourceId: 'gold', amount: 9_000_000_000 }]
    const engine = startPalach(value)
    const before = engine.snapshot()
    const result = engine.advanceDialogue('quest-palach', opening.id)
    expect(result.ok).toBe(false)
    expect(engine.state.revision).toBe(before.revision)
    expect(engine.state.quests['quest-palach']).toEqual(before.quests['quest-palach'])
    expect(engine.state.empire.resources.gold).toBe(before.empire.resources.gold)
  })

  it('suspends incompatible saved nodes without resetting quest memory', () => {
    const value = config()
    const engine = startPalach(value)
    engine.state.quests['quest-palach'].nodeId = 'removed-node'
    engine.state.quests['quest-palach'].memory.cardsKnown = true
    const restored = new EmpiresEndgameEngine(value, engine.snapshot())
    expect(restored.state.quests['quest-palach']).toMatchObject({
      status: 'suspended',
      suspendedStatus: 'active',
      memory: { cardsKnown: true },
    })
    expect(restored.state.questRuntime.activeMandatoryQuestId).toBeNull()

    const disabled = config()
    disabled.empire.events = disabled.empire.events.filter(event => (
      event.id !== 'event-golden-idol' && event.id !== 'event-witch-apprenticeship'
    ))
    disabled.quests.definitions = disabled.quests.definitions.filter(quest => quest.id === 'quest-palach')
    disabled.quests.enabled = false
    const disabledRestore = new EmpiresEndgameEngine(disabled, engine.snapshot())
    expect(disabledRestore.state.quests['quest-palach']).toMatchObject({
      status: 'suspended',
      suspendedStatus: 'active',
      compatibilityReason: 'Quests are disabled in the active configuration.',
      memory: { cardsKnown: true },
    })
    expect(disabledRestore.state.questRuntime.activeMandatoryQuestId).toBeNull()
  })

  it('bridges Golden Idol and Witch Apprenticeship through consumed quest resolutions', () => {
    const value = config()
    const idol = new EmpiresEndgameEngine(value, eventState(value, 'event-golden-idol'))
    const beforeDeferred = idol.snapshot()
    expect(idol.eventChoiceBlockedReason('idol-monument')).toMatch(/deferred/i)
    expect(idol.chooseEvent('idol-monument').ok).toBe(false)
    expect(idol.snapshot()).toEqual(beforeDeferred)
    const beforeGold = idol.state.empire.resources.gold
    const beforeWood = idol.state.empire.resources.wood
    const beforeIron = idol.state.empire.resources.iron
    expect(idol.eventChoiceBlockedReason('idol-sell')).toBeNull()
    expect(idol.chooseEvent('idol-sell').ok).toBe(true)
    expect(idol.state.empire.resources.gold).toBe(beforeGold + 6000)
    expect(idol.state.empire.resources.wood).toBe(beforeWood + 300000)
    expect(idol.state.empire.resources.iron).toBe(beforeIron + 200000)
    expect(idol.state.quests['quest-golden-idol']).toMatchObject({
      status: 'completed', memory: { enemyIdolRisk: true },
    })
    expect(idol.state.empire.flags.enemyIdolRisk).toBeUndefined()

    const destroy = new EmpiresEndgameEngine(value, eventState(value, 'event-golden-idol'))
    expect(destroy.chooseEvent('idol-destroy').ok).toBe(true)
    expect(destroy.state.quests['quest-golden-idol']).toMatchObject({
      status: 'completed',
      memory: {
        idolDestroyed: true,
        nationalUnityStrengthened: true,
        churchUnityStrengthened: true,
      },
    })
    expect(destroy.state.empire.flags.nationalUnity).toBeUndefined()
    expect(destroy.state.empire.flags.churchUnity).toBeUndefined()

    const send = new EmpiresEndgameEngine(value, eventState(value, 'event-witch-apprenticeship'))
    const east = send.state.empire.cities
      .filter(city => city.regionId === 'east' && send.isCityAccessible(city.id))
      .sort((left, right) => left.population - right.population || left.id.localeCompare(right.id))[0]
    const beforePopulation = east.population
    const beforeKnowledge = send.state.empire.resources.knowledge
    expect(send.chooseEvent('send-apprentices').ok).toBe(true)
    expect(send.state.empire.resources.knowledge).toBe(beforeKnowledge + 1200)
    expect(send.state.empire.cities.find(city => city.id === east.id)?.population).toBe(beforePopulation - 1000)
    expect(send.state.quests['quest-witch-apprenticeship'].memory).toMatchObject({
      swampAlchemy: true, apprenticeCityId: east.id,
    })

    const refuse = new EmpiresEndgameEngine(value, eventState(value, 'event-witch-apprenticeship'))
    const beforeLoyalty = refuse.state.empire.loyalty.regions.east.value
    expect(refuse.chooseEvent('refuse-witches').ok).toBe(true)
    expect(refuse.state.empire.loyalty.regions.east.value).toBe(beforeLoyalty - 1)
  })

  it('migrates config v11 and save v9 clone-first without losing valid custom quest data', () => {
    const value = config()
    const legacyConfig = structuredClone(value) as unknown as Record<string, unknown>
    legacyConfig.schemaVersion = 11
    ;(legacyConfig.quests as Record<string, unknown>).dialogueGraphs = [{ id: 'legacy-shell' }]
    const untouchedConfig = structuredClone(legacyConfig)
    const migrated = migrateEmpiresConfig(legacyConfig) as EmpiresEndgameConfig
    expect(legacyConfig).toEqual(untouchedConfig)
    expect(migrated.schemaVersion).toBe(17)
    expect(migrated.quests.definitions.find(quest => quest.id === 'quest-palach')
      ?.stages.flatMap(stage => stage.nodes)).toHaveLength(43)
    expect((migrated.quests as unknown as Record<string, unknown>).dialogueGraphs).toBeUndefined()
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)

    const legacyState = new EmpiresEndgameEngine(value).snapshot() as unknown as Record<string, unknown>
    legacyState.schemaVersion = 9
    legacyState.quests = {}
    delete legacyState.questRuntime
    const envelope = {
      schemaVersion: 9,
      savedAt: '2026-07-18T00:00:00.000Z',
      state: legacyState,
    }
    const untouchedEnvelope = structuredClone(envelope)
    const restored = new EmpiresEndgameEngine(value, importEmpiresCampaign(envelope, value.id))
    expect(envelope).toEqual(untouchedEnvelope)
    expect(restored.state).toMatchObject({
      schemaVersion: 16,
      quests: {},
      questRuntime: {
        activeMandatoryQuestId: null,
        mandatoryQueue: [],
        history: [],
        nextHistorySequence: 1,
        compactedHistoryCount: 0,
      },
    })
  })
})
