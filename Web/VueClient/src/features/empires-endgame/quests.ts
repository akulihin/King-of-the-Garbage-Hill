import type {
  EmpiresCampaignState,
  EmpiresDependency,
  EmpiresEffect,
  EmpiresEndgameConfig,
  EmpiresQuestChoiceDefinition,
  EmpiresQuestDefinition,
  EmpiresQuestMemoryRequirement,
  EmpiresQuestMemoryValue,
  EmpiresQuestNodeDefinition,
  EmpiresQuestState,
  EmpiresResourceAmount,
  TdBattleOutcome,
} from './types'

export type EmpiresQuestTriggerContext =
  | { kind: 'empireStart', con: number }
  | { kind: 'event', eventId: string, eventInstanceId: string }
  | { kind: 'building', buildingId: string, cityId: string, level: number, con: number }
  | {
    kind: 'minigameResult'
    sessionId: string
    minigameKind: 'td'
    outcome: TdBattleOutcome
    con: number
  }
  | { kind: 'manual', questId: string, sourceId: string, con: number }

export interface EmpiresQuestTriggerReaders {
  flagValue(flagId: string): number
  buildingLevel(buildingId: string, cityId?: string): number
}

export interface EmpiresQuestStartRequest {
  definition: EmpiresQuestDefinition
  triggerIdentity: string
}

function stableStringCompare(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function nonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0
}

function finiteNonNegative(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0
}

function unique(values: readonly string[]): boolean {
  return new Set(values).size === values.length
}

function memoryValueMatchesType(
  value: EmpiresQuestMemoryValue,
  type: 'string' | 'number' | 'boolean',
): boolean {
  return typeof value === type && (type !== 'number' || Number.isFinite(value as number))
}

function dependencyError(
  dependency: EmpiresDependency,
  config: EmpiresEndgameConfig,
): string | null {
  if (dependency.kind === 'building') {
    if (!config.empire.buildings.some(item => item.id === dependency.buildingId)) {
      return `unknown building ${dependency.buildingId}`
    }
    if (!Number.isInteger(dependency.level) || dependency.level < 1) return 'invalid building level'
    if (dependency.scope !== undefined && !['sameCity', 'anyCity'].includes(dependency.scope)) {
      return `unknown building scope ${dependency.scope}`
    }
    return null
  }
  if (dependency.kind === 'technology') {
    return config.empire.technologies.some(item => item.id === dependency.technologyId)
      ? null
      : `unknown technology ${dependency.technologyId}`
  }
  if (dependency.kind === 'flag') {
    return nonEmptyString(dependency.flagId) && Number.isFinite(dependency.minimum)
      ? null
      : 'invalid flag dependency'
  }
  if (dependency.kind === 'reputation') {
    return Number.isFinite(dependency.minimum) ? null : 'invalid reputation dependency'
  }
  if (dependency.kind === 'advisor') {
    return config.governance.advisors.some(item => item.id === dependency.advisorId)
      ? null
      : `unknown advisor ${dependency.advisorId}`
  }
  return 'unknown dependency kind'
}

function effectError(effect: EmpiresEffect, config: EmpiresEndgameConfig): string | null {
  const resourceIds = new Set(config.empire.resources.map(item => item.id))
  if (effect.kind === 'resource') {
    if (!resourceIds.has(effect.resourceId)) return `unknown resource ${effect.resourceId}`
    return Number.isFinite(effect.amount) && Number.isFinite(effect.amountPerLevel ?? 0)
      ? null
      : 'non-finite resource effect'
  }
  if (effect.kind === 'resourceMultiplier') {
    if (!resourceIds.has(effect.resourceId)) return `unknown resource ${effect.resourceId}`
    return Number.isFinite(effect.multiplier) && Number.isFinite(effect.multiplierPerLevel ?? 0)
      ? null
      : 'non-finite resource effect'
  }
  if (effect.kind === 'time') {
    return Number.isFinite(effect.days) && Number.isFinite(effect.daysPerLevel ?? 0)
      ? null
      : 'non-finite time effect'
  }
  if (effect.kind === 'foodProduction' || effect.kind === 'population') {
    if (effect.cityId && !config.empire.cities.some(item => item.id === effect.cityId)) {
      return `unknown city ${effect.cityId}`
    }
    return Number.isFinite(effect.amount) && Number.isFinite(effect.amountPerLevel ?? 0)
      ? null
      : 'non-finite city effect'
  }
  if (effect.kind === 'loyalty') {
    if (!Number.isFinite(effect.amount) || !Number.isFinite(effect.amountPerLevel ?? 0)) {
      return 'non-finite loyalty effect'
    }
    if (effect.target.kind === 'region') {
      return config.empire.map.regions.some(item => item.id === effect.target.regionId)
        ? null
        : `unknown region ${effect.target.regionId}`
    }
    if (effect.target.kind === 'city') {
      return config.empire.cities.some(item => item.id === effect.target.cityId)
        ? null
        : `unknown city ${effect.target.cityId}`
    }
    return config.empire.cities.some(item => item.id === effect.target.cityId)
      && config.empire.populationClasses.some(item => item.id === effect.target.populationClassId)
      ? null
      : 'unknown class-loyalty target'
  }
  if (effect.kind === 'loyaltyAllCities' || effect.kind === 'reputation') {
    return Number.isFinite(effect.amount) && Number.isFinite(effect.amountPerLevel ?? 0)
      ? null
      : 'non-finite political effect'
  }
  if (effect.kind === 'classLoyalty') {
    if (!config.empire.populationClasses.some(item => item.id === effect.populationClassId)) {
      return `unknown population class ${effect.populationClassId}`
    }
    return Number.isFinite(effect.amount) && Number.isFinite(effect.amountPerLevel ?? 0)
      ? null
      : 'non-finite class-loyalty effect'
  }
  if (effect.kind === 'flag') {
    return nonEmptyString(effect.flagId)
      && Number.isFinite(effect.amount)
      && Number.isFinite(effect.amountPerLevel ?? 0)
      ? null
      : 'invalid flag effect'
  }
  if (effect.kind === 'epidemicStart') {
    if (!config.empire.epidemics.definitions.some(item => item.id === effect.definitionId)) {
      return `unknown epidemic ${effect.definitionId}`
    }
    if (effect.origin.kind === 'city') {
      return config.empire.cities.some(item => item.id === effect.origin.cityId)
        ? null
        : `unknown epidemic city ${effect.origin.cityId}`
    }
    if (effect.origin.kind === 'lowest-operational-building-city') {
      return config.empire.buildings.some(item => item.id === effect.origin.buildingId)
        ? null
        : `unknown epidemic building ${effect.origin.buildingId}`
    }
    return ['effect-target-city', 'lowest-accessible-city'].includes(effect.origin.kind)
      ? null
      : 'unknown epidemic origin selector'
  }
  return 'unknown effect kind'
}

function costError(cost: EmpiresResourceAmount, config: EmpiresEndgameConfig): string | null {
  if (!config.empire.resources.some(item => item.id === cost.resourceId)) {
    return `unknown resource ${cost.resourceId}`
  }
  return finiteNonNegative(cost.amount) ? null : 'cost must be finite and non-negative'
}

function nodeKey(stageId: string, nodeId: string): string {
  return `${stageId}/${nodeId}`
}

function stronglyConnectedComponents(
  nodes: readonly string[],
  edges: ReadonlyMap<string, readonly string[]>,
): string[][] {
  let index = 0
  const indexes = new Map<string, number>()
  const low = new Map<string, number>()
  const stack: string[] = []
  const onStack = new Set<string>()
  const result: string[][] = []

  const visit = (node: string) => {
    indexes.set(node, index)
    low.set(node, index)
    index += 1
    stack.push(node)
    onStack.add(node)
    for (const target of edges.get(node) ?? []) {
      if (!indexes.has(target)) {
        visit(target)
        low.set(node, Math.min(low.get(node)!, low.get(target)!))
      } else if (onStack.has(target)) {
        low.set(node, Math.min(low.get(node)!, indexes.get(target)!))
      }
    }
    if (low.get(node) !== indexes.get(node)) return
    const component: string[] = []
    while (stack.length > 0) {
      const member = stack.pop()!
      onStack.delete(member)
      component.push(member)
      if (member === node) break
    }
    result.push(component.sort(stableStringCompare))
  }

  for (const node of nodes) if (!indexes.has(node)) visit(node)
  return result
}

export function validateEmpiresQuestsConfig(config: EmpiresEndgameConfig): string[] {
  const errors: string[] = []
  const quests = config.quests
  if (!isRecord(quests) || typeof quests.enabled !== 'boolean') {
    return ['quests must be an object with an enabled flag']
  }
  if (!Number.isInteger(quests.historyRetention) || quests.historyRetention < 1) {
    errors.push('quests.historyRetention must be a positive integer')
  }
  if (!Number.isInteger(quests.triggerHistoryRetention) || quests.triggerHistoryRetention < 1) {
    errors.push('quests.triggerHistoryRetention must be a positive integer')
  }
  if (!Array.isArray(quests.definitions)) return [...errors, 'quests.definitions must be an array']
  const questIds = quests.definitions.map(item => item?.id)
  if (questIds.some(id => !nonEmptyString(id)) || !unique(questIds)) {
    errors.push('quest definitions need unique non-empty ids')
  }
  if (quests.enabled && quests.definitions.length === 0) {
    errors.push('quests.definitions must not be empty when quests.enabled is true')
  }

  const eventIds = new Set(config.empire.events.map(item => item.id))
  const buildingIds = new Set(config.empire.buildings.map(item => item.id))
  const cityIds = new Set(config.empire.cities.map(item => item.id))
  const regionIds = new Set(config.empire.map.regions.map(item => item.id))
  const knownFlagIds = new Set<string>(Object.keys(config.empire.initialFlags ?? {}))
  const collectEffectFlags = (effects: readonly EmpiresEffect[]) => {
    for (const effect of effects) if (effect.kind === 'flag') knownFlagIds.add(effect.flagId)
  }
  for (const card of config.cards) {
    collectEffectFlags(card.normal.effects)
    collectEffectFlags(card.inverted.effects)
  }
  for (const technology of config.empire.technologies) collectEffectFlags(technology.effects)
  for (const gift of config.gifts.definitions) {
    collectEffectFlags(gift.effects)
  }
  for (const building of config.empire.buildings) {
    for (const level of building.levels) collectEffectFlags(level.effects ?? [])
  }
  for (const event of config.empire.events) {
    for (const choice of event.choices) collectEffectFlags(choice.effects)
  }
  for (const quest of config.quests.definitions) {
    for (const stage of quest.stages ?? []) {
      for (const node of stage.nodes ?? []) {
        for (const choice of node.choices ?? []) collectEffectFlags(choice.effects ?? [])
      }
    }
  }

  for (const quest of quests.definitions) {
    const label = `quest ${quest.id}`
    if (!nonEmptyString(quest.name) || !nonEmptyString(quest.journalDescription)) {
      errors.push(`${label} needs name and journalDescription`)
    }
    if (quest.deferredReason !== undefined && !nonEmptyString(quest.deferredReason)) {
      errors.push(`${label} deferredReason must be a non-empty string`)
    }
    if (typeof quest.mandatory !== 'undefined' && typeof quest.mandatory !== 'boolean') {
      errors.push(`${label} mandatory must be a boolean`)
    }
    const trigger = quest.trigger
    if (!isRecord(trigger) || !nonEmptyString(trigger.kind)) {
      errors.push(`${label} needs a typed trigger`)
    } else {
      if (trigger.repeatable !== undefined && typeof trigger.repeatable !== 'boolean') {
        errors.push(`${label} trigger.repeatable must be a boolean`)
      }
      if (trigger.kind === 'conReached') {
        if (!Number.isInteger(trigger.con) || (trigger.con as number) < 1) errors.push(`${label} has an invalid conReached trigger`)
      } else if (trigger.kind === 'flag') {
        if (!nonEmptyString(trigger.flagId) || !Number.isFinite(trigger.minimum)) {
          errors.push(`${label} has an invalid flag trigger`)
        } else if (!knownFlagIds.has(trigger.flagId as string)) {
          errors.push(`${label} trigger references unknown flag ${String(trigger.flagId)}`)
        }
      } else if (trigger.kind === 'event') {
        if (!nonEmptyString(trigger.eventId) || !eventIds.has(trigger.eventId as string)) {
          errors.push(`${label} trigger references unknown event ${String(trigger.eventId)}`)
        }
      } else if (trigger.kind === 'building') {
        if (!nonEmptyString(trigger.buildingId) || !buildingIds.has(trigger.buildingId as string)
          || !Number.isInteger(trigger.level) || (trigger.level as number) < 1
          || (trigger.cityId !== undefined && (!nonEmptyString(trigger.cityId) || !cityIds.has(trigger.cityId)))) {
          errors.push(`${label} has an invalid building trigger`)
        }
      } else if (trigger.kind === 'minigameResult') {
        if (trigger.minigameKind !== 'td'
          || (trigger.outcome !== undefined && !['victory', 'defeat', 'aborted'].includes(trigger.outcome as string))) {
          errors.push(`${label} has an invalid minigameResult trigger`)
        }
      } else if (trigger.kind !== 'manual') {
        errors.push(`${label} has unknown trigger kind ${String(trigger.kind)}`)
      }
    }
    const repeatable = quest.trigger?.repeatable === true
    const restartPolicy = quest.restartPolicy ?? 'never'
    if (!['never', 'afterTerminal'].includes(restartPolicy)
      || (repeatable && restartPolicy !== 'afterTerminal')
      || (!repeatable && restartPolicy !== 'never')) {
      errors.push(`${label} repeatability and restartPolicy disagree`)
    }

    const memoryDefinitions = quest.memory ?? []
    const memoryKeys = memoryDefinitions.map(item => item?.key)
    if (memoryKeys.some(key => !nonEmptyString(key)) || !unique(memoryKeys)) {
      errors.push(`${label} memory definitions need unique non-empty keys`)
    }
    const memoryByKey = new Map(memoryDefinitions.map(item => [item.key, item]))
    for (const memory of memoryDefinitions) {
      if (!['string', 'number', 'boolean'].includes(memory.type)
        || !memoryValueMatchesType(memory.initial, memory.type)
        || (memory.journalVisible && !nonEmptyString(memory.label))) {
        errors.push(`${label} memory ${memory.key} has an invalid type, initial value, or journal label`)
      }
    }

    if (!Array.isArray(quest.stages) || quest.stages.length === 0) {
      errors.push(`${label} needs at least one stage`)
      continue
    }
    const stageIds = quest.stages.map(stage => stage?.id)
    if (stageIds.some(id => !nonEmptyString(id)) || !unique(stageIds)) {
      errors.push(`${label} stages need unique non-empty ids`)
    }
    const stageById = new Map(quest.stages.map(stage => [stage.id, stage]))
    if (!nonEmptyString(quest.entryStageId) || !stageById.has(quest.entryStageId)) {
      errors.push(`${label} references missing entry stage ${quest.entryStageId}`)
    }
    const nodeById = new Map<string, { stageId: string, node: EmpiresQuestNodeDefinition }>()
    for (const stage of quest.stages) {
      if (!nonEmptyString(stage.name) || !Array.isArray(stage.nodes) || stage.nodes.length === 0) {
        errors.push(`${label} stage ${stage.id} needs a name and nodes`)
        continue
      }
      const localIds = stage.nodes.map(node => node?.id)
      if (localIds.some(id => !nonEmptyString(id)) || !unique(localIds)) {
        errors.push(`${label} stage ${stage.id} nodes need unique non-empty ids`)
      }
      if (!localIds.includes(stage.entryNodeId)) {
        errors.push(`${label} stage ${stage.id} references missing entry node ${stage.entryNodeId}`)
      }
      for (const node of stage.nodes) {
        if (nodeById.has(node.id)) errors.push(`${label} repeats node id ${node.id} across stages`)
        nodeById.set(node.id, { stageId: stage.id, node })
      }
    }

    const allEdges = new Map<string, string[]>()
    const liveEdges = new Map<string, string[]>()
    const liveTerminalSources = new Set<string>()
    for (const stage of quest.stages) {
      for (const node of stage.nodes) {
        const source = nodeKey(stage.id, node.id)
        allEdges.set(source, [])
        liveEdges.set(source, [])
        if (!nonEmptyString(node.speaker) || !nonEmptyString(node.text) || !Array.isArray(node.choices)) {
          errors.push(`${label} node ${node.id} needs speaker, text, and choices`)
          continue
        }
        if (node.terminal && !['complete', 'fail'].includes(node.terminal)) {
          errors.push(`${label} node ${node.id} has an invalid terminal kind`)
        }
        if (node.terminal) {
          liveTerminalSources.add(source)
        }
        if (node.terminal && node.choices.length > 0) {
          errors.push(`${label} terminal node ${node.id} cannot have choices`)
        }
        const choiceIds = node.choices.map(choice => choice?.id)
        if (choiceIds.some(id => !nonEmptyString(id)) || !unique(choiceIds)) {
          errors.push(`${label} node ${node.id} choices need unique non-empty ids`)
        }
        for (const choice of node.choices) {
          const choiceLabel = `${label} node ${node.id} choice ${choice.id}`
          if (!nonEmptyString(choice.label)) errors.push(`${choiceLabel} needs a label`)
          if (choice.deferredReason !== undefined && !nonEmptyString(choice.deferredReason)) {
            errors.push(`${choiceLabel} deferredReason must be a non-empty string`)
          }
          if (choice.deferredVisibility !== undefined && !['visible', 'hidden'].includes(choice.deferredVisibility)) {
            errors.push(`${choiceLabel} has an invalid deferredVisibility`)
          }
          if (choice.deferredVisibility !== undefined && !choice.deferredReason) {
            errors.push(`${choiceLabel} deferredVisibility requires deferredReason`)
          }
          for (const dependency of choice.requirements ?? []) {
            const error = dependencyError(dependency, config)
            if (error) errors.push(`${choiceLabel} has ${error}`)
          }
          for (const cost of choice.costs ?? []) {
            const error = costError(cost, config)
            if (error) errors.push(`${choiceLabel} has ${error}`)
          }
          for (const effect of choice.effects ?? []) {
            const error = effectError(effect, config)
            if (error) errors.push(`${choiceLabel} has ${error}`)
          }
          for (const requirement of choice.visibilityRequirements ?? []) {
            const memory = memoryByKey.get(requirement.key)
            if (!memory || !['eq', 'gte', 'lte'].includes(requirement.comparison)
              || !memoryValueMatchesType(requirement.value, memory.type)
              || (requirement.comparison !== 'eq' && memory.type !== 'number')) {
              errors.push(`${choiceLabel} has an invalid memory visibility requirement`)
            }
          }
          for (const write of choice.memoryWrites ?? []) {
            const memory = memoryByKey.get(write.key)
            if (!memory || !['set', 'add'].includes(write.operation)
              || !memoryValueMatchesType(write.value, memory.type)
              || (write.operation === 'add' && memory.type !== 'number')) {
              errors.push(`${choiceLabel} has an invalid memory write`)
            }
          }
          if (choice.target) {
            if (choice.target.kind !== 'city'
              || !['eventTarget', 'lowestAccessible', 'lowestAccessibleInRegion'].includes(choice.target.selector)
              || (choice.target.selector === 'lowestAccessibleInRegion'
                && (!choice.target.regionId || !regionIds.has(choice.target.regionId)))
              || (choice.target.memoryKey && memoryByKey.get(choice.target.memoryKey)?.type !== 'string')) {
              errors.push(`${choiceLabel} has an invalid typed target`)
            }
          }
          if (!isRecord(choice.goto) || !nonEmptyString(choice.goto.kind)) {
            errors.push(`${choiceLabel} needs a typed goto`)
            continue
          }
          let target: string | null = null
          if (choice.goto.kind === 'node') {
            const destination = nodeById.get(choice.goto.nodeId as string)
            if (!destination) errors.push(`${choiceLabel} references missing node ${String(choice.goto.nodeId)}`)
            else target = nodeKey(destination.stageId, destination.node.id)
          } else if (choice.goto.kind === 'stage') {
            const destination = stageById.get(choice.goto.stageId as string)
            if (!destination) errors.push(`${choiceLabel} references missing stage ${String(choice.goto.stageId)}`)
            else target = nodeKey(destination.id, destination.entryNodeId)
          } else if (choice.goto.kind === 'complete' || choice.goto.kind === 'fail') {
            if (!choice.deferredReason) liveTerminalSources.add(source)
          } else {
            errors.push(`${choiceLabel} has unknown goto kind ${String(choice.goto.kind)}`)
          }
          if (target) {
            allEdges.get(source)!.push(target)
            if (!choice.deferredReason) liveEdges.get(source)!.push(target)
          }
        }
      }
    }

    const entryStage = stageById.get(quest.entryStageId)
    const root = entryStage ? nodeKey(entryStage.id, entryStage.entryNodeId) : null
    if (!root || !allEdges.has(root)) continue
    const reachable = new Set<string>()
    const visit = (key: string) => {
      if (reachable.has(key)) return
      reachable.add(key)
      for (const target of allEdges.get(key) ?? []) visit(target)
    }
    visit(root)
    for (const key of allEdges.keys()) {
      if (!reachable.has(key)) errors.push(`${label} has orphan node ${key}`)
    }

    const cycleComponents = stronglyConnectedComponents([...allEdges.keys()], allEdges)
      .filter(component => component.length > 1 || (allEdges.get(component[0]) ?? []).includes(component[0]))
    const allowedCycles = quest.allowedCycles ?? []
    const allowedIds = allowedCycles.map(item => item.id)
    if (allowedIds.some(id => !nonEmptyString(id)) || !unique(allowedIds)) {
      errors.push(`${label} allowed cycles need unique non-empty ids`)
    }
    const allowedSets = allowedCycles.map(item => item.nodeIds.map((nodeId) => {
      const destination = nodeById.get(nodeId)
      return destination ? nodeKey(destination.stageId, nodeId) : nodeId
    }).sort(stableStringCompare))
    for (const [index, allowed] of allowedSets.entries()) {
      const authoredNodeIds = allowedCycles[index].nodeIds
      if (allowed.length === 0 || !unique(authoredNodeIds)
        || authoredNodeIds.some(nodeId => !nodeById.has(nodeId))) {
        errors.push(`${label} has an invalid allowed cycle declaration`)
      }
    }
    for (const component of cycleComponents) {
      if (!allowedSets.some(allowed => JSON.stringify(allowed) === JSON.stringify(component))) {
        errors.push(`${label} has an undeclared cycle: ${component.join(', ')}`)
      }
    }
    for (const allowed of allowedSets) {
      if (!cycleComponents.some(component => JSON.stringify(allowed) === JSON.stringify(component))) {
        errors.push(`${label} declares a cycle that is not present: ${allowed.join(', ')}`)
      }
    }

    const liveReachable = new Set<string>()
    const visitLive = (key: string) => {
      if (liveReachable.has(key)) return
      liveReachable.add(key)
      for (const target of liveEdges.get(key) ?? []) visitLive(target)
    }
    visitLive(root)
    const reverse = new Map<string, string[]>()
    for (const key of liveEdges.keys()) reverse.set(key, [])
    for (const [source, targets] of liveEdges) {
      for (const target of targets) reverse.get(target)?.push(source)
    }
    const canReachTerminal = new Set<string>()
    const markTerminal = (key: string) => {
      if (canReachTerminal.has(key)) return
      canReachTerminal.add(key)
      for (const source of reverse.get(key) ?? []) markTerminal(source)
    }
    for (const key of liveTerminalSources) markTerminal(key)
    if (!quest.deferredReason) {
      if (!canReachTerminal.has(root)) errors.push(`${label} has no reachable non-deferred terminal path`)
      for (const key of liveReachable) {
        if (!canReachTerminal.has(key)) errors.push(`${label} live node ${key} cannot reach a terminal`)
      }
    }
  }

  const definitionById = new Map(quests.definitions.map(item => [item.id, item]))
  for (const event of config.empire.events) {
    for (const choice of event.choices) {
      if (!choice.questResolution) continue
      const quest = definitionById.get(choice.questResolution.questId)
      const entryStage = quest?.stages.find(stage => stage.id === quest.entryStageId)
      const entryNode = entryStage?.nodes.find(node => node.id === entryStage.entryNodeId)
      if (!quests.enabled || !quest || quest.deferredReason || quest.trigger.kind !== 'event'
        || quest.trigger.eventId !== event.id || quest.mandatory !== false
        || !entryNode?.choices.some(item => item.id === choice.questResolution!.choiceId && !item.deferredReason)) {
        errors.push(`event ${event.id} choice ${choice.id} has an invalid questResolution bridge`)
      }
      if ((choice.effects.length > 0 || (choice.resourceCosts?.length ?? 0) > 0)) {
        errors.push(`event ${event.id} choice ${choice.id} questResolution must own all effects and costs`)
      }
    }
  }
  return errors
}

export function questCurrentNode(
  definition: EmpiresQuestDefinition,
  state: Pick<EmpiresQuestState, 'stageId' | 'nodeId'>,
): EmpiresQuestNodeDefinition | null {
  return definition.stages.find(stage => stage.id === state.stageId)
    ?.nodes.find(node => node.id === state.nodeId) ?? null
}

function compareMemory(
  actual: EmpiresQuestMemoryValue | undefined,
  requirement: EmpiresQuestMemoryRequirement,
): boolean {
  if (requirement.comparison === 'eq') return actual === requirement.value
  if (typeof actual !== 'number' || typeof requirement.value !== 'number') return false
  return requirement.comparison === 'gte'
    ? actual >= requirement.value
    : actual <= requirement.value
}

export function questMemoryRequirementsMet(
  memory: Readonly<Record<string, EmpiresQuestMemoryValue>>,
  requirements: readonly EmpiresQuestMemoryRequirement[] = [],
): boolean {
  return requirements.every(requirement => compareMemory(memory[requirement.key], requirement))
}

export function questChoiceIsVisible(
  choice: EmpiresQuestChoiceDefinition,
  memory: Readonly<Record<string, EmpiresQuestMemoryValue>>,
): boolean {
  if (choice.deferredReason && choice.deferredVisibility === 'hidden') return false
  return questMemoryRequirementsMet(memory, choice.visibilityRequirements)
}

export function questTriggerIdentity(
  definition: EmpiresQuestDefinition,
  context: EmpiresQuestTriggerContext,
): string | null {
  const trigger = definition.trigger
  const base = `quest:${definition.id}:trigger:${trigger.kind}`
  if (trigger.kind === 'conReached' && context.kind === 'empireStart') {
    return trigger.repeatable ? `${base}:con:${context.con}` : `${base}:con:${trigger.con}`
  }
  if (trigger.kind === 'flag' && context.kind === 'empireStart') {
    return trigger.repeatable ? `${base}:${trigger.flagId}:con:${context.con}` : `${base}:${trigger.flagId}`
  }
  if (trigger.kind === 'event' && context.kind === 'event') return `${base}:${context.eventInstanceId}`
  if (trigger.kind === 'building' && (context.kind === 'building' || context.kind === 'empireStart')) {
    return context.kind === 'building'
      ? `${base}:${context.cityId}:${context.level}:con:${context.con}`
      : trigger.repeatable ? `${base}:con:${context.con}` : `${base}:${trigger.buildingId}:${trigger.level}`
  }
  if (trigger.kind === 'minigameResult' && context.kind === 'minigameResult') {
    return `${base}:${context.sessionId}`
  }
  if (trigger.kind === 'manual' && context.kind === 'manual' && context.questId === definition.id) {
    return `${base}:${context.sourceId}`
  }
  return null
}

export function questTriggerMatches(
  definition: EmpiresQuestDefinition,
  context: EmpiresQuestTriggerContext,
  readers: EmpiresQuestTriggerReaders,
): boolean {
  const trigger = definition.trigger
  if (trigger.kind === 'conReached') return context.kind === 'empireStart' && context.con >= trigger.con
  if (trigger.kind === 'flag') {
    return context.kind === 'empireStart' && readers.flagValue(trigger.flagId) >= trigger.minimum
  }
  if (trigger.kind === 'event') return context.kind === 'event' && context.eventId === trigger.eventId
  if (trigger.kind === 'building') {
    if (context.kind === 'building') {
      return context.buildingId === trigger.buildingId
        && context.level >= trigger.level
        && (!trigger.cityId || trigger.cityId === context.cityId)
    }
    return context.kind === 'empireStart'
      && readers.buildingLevel(trigger.buildingId, trigger.cityId) >= trigger.level
  }
  if (trigger.kind === 'minigameResult') {
    return context.kind === 'minigameResult'
      && context.minigameKind === trigger.minigameKind
      && (!trigger.outcome || context.outcome === trigger.outcome)
  }
  return context.kind === 'manual' && context.questId === definition.id
}

export function evaluateQuestTriggerStarts(
  definitions: readonly EmpiresQuestDefinition[],
  states: Readonly<Record<string, EmpiresQuestState>>,
  context: EmpiresQuestTriggerContext,
  readers: EmpiresQuestTriggerReaders,
): EmpiresQuestStartRequest[] {
  return definitions.flatMap((definition) => {
    if (definition.deferredReason || !questTriggerMatches(definition, context, readers)) return []
    const identity = questTriggerIdentity(definition, context)
    if (!identity) return []
    const state = states[definition.id]
    if (state?.consumedTriggerIds.includes(identity)) return []
    if (state?.status === 'active' || state?.status === 'suspended') return []
    if (state && (!definition.trigger.repeatable || (definition.restartPolicy ?? 'never') !== 'afterTerminal')) {
      return []
    }
    return [{ definition, triggerIdentity: identity }]
  })
}

export function initialQuestMemory(
  definition: EmpiresQuestDefinition,
): Record<string, EmpiresQuestMemoryValue> {
  return Object.fromEntries((definition.memory ?? []).map(item => [item.key, item.initial]))
}

export function compactQuestTriggerIdentity(
  previousDigest: string,
  identity: string,
): string {
  let hash = 0x811c9dc5
  for (const character of `${previousDigest}\u0000${identity}`) {
    hash ^= character.charCodeAt(0)
    hash = Math.imul(hash, 0x01000193) >>> 0
  }
  return hash.toString(16).padStart(8, '0')
}

export function questStateIsTerminal(state: Pick<EmpiresQuestState, 'status'>): boolean {
  return state.status === 'completed' || state.status === 'failed'
}

export function questStateBlocksUnrelatedActions(
  state: Pick<EmpiresCampaignState, 'quests' | 'questRuntime'>,
): boolean {
  const questId = state.questRuntime.activeMandatoryQuestId
  return Boolean(questId && state.quests[questId])
}
