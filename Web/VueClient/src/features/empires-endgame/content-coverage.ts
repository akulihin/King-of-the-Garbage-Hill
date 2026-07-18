import type { EmpiresEndgameConfig } from './types'

export const EMPIRES_CONTENT_DISPOSITIONS = [
  'live',
  'ready-now',
  'blocked-semantic',
  'blocked-substrate',
  'review',
  'out',
] as const

export type EmpiresContentDisposition = typeof EMPIRES_CONTENT_DISPOSITIONS[number]
export type EmpiresConfigCarrierAvailability = 'configured' | 'deferred' | 'review'

export interface EmpiresConfigCarrierCoverageGroup {
  id: string
  disposition: EmpiresContentDisposition
  expectedAvailability: EmpiresConfigCarrierAvailability
  rawSources: string[]
  owner: string
  consumer: string
  testEvidence: string[]
  configCarrierKeys: string[]
  designerQuestion?: string
}

export interface EmpiresRawCatalogCoverageGroup {
  id: string
  disposition: EmpiresContentDisposition
  rawSources: string[]
  owner: string
  consumer: string
  testEvidence: string[]
  stableIdentities: string[]
  /** Frozen link count: zero means this raw group is explicitly config-absent. */
  expectedLinkedConfigCarrierCount: number
  linkedConfigCarrierKeys: string[]
  designerQuestion?: string
}

export interface EmpiresRawSourceInventoryEntry {
  id: string
  path: string
  messageCount: number | null
  role: 'authoritative' | 'mixed' | 'review' | 'out' | 'background'
  evidence: string
  /** Frozen source-message spine; null is reserved for non-JSON/directory/image sources. */
  messageIds: string[] | null
  residualDisposition: 'review' | 'out'
  residualOwner: string
  residualConsumer: string
  residualTestEvidence: string[]
  residualDesignerQuestion?: string
}

export interface EmpiresContentCoverageManifest {
  schemaVersion: 1
  configSchemaVersion: number
  auditedAt: string
  sourceInventory: EmpiresRawSourceInventoryEntry[]
  configGroups: EmpiresConfigCarrierCoverageGroup[]
  rawCatalogGroups: EmpiresRawCatalogCoverageGroup[]
}

export interface EmpiresConfigCarrier {
  key: string
  availability: EmpiresConfigCarrierAvailability
}

function carrierAvailability(deferredReason?: string): EmpiresConfigCarrierAvailability {
  return deferredReason ? 'deferred' : 'configured'
}

export function collectEmpiresConfigCarriers(config: EmpiresEndgameConfig): EmpiresConfigCarrier[] {
  const carriers: EmpiresConfigCarrier[] = []
  const add = (key: string, availability: EmpiresConfigCarrierAvailability = 'configured') => {
    carriers.push({ key, availability })
  }
  const addSubfeatures = (
    prefix: string,
    subfeatures: readonly { id: string }[] | undefined,
  ) => {
    for (const subfeature of subfeatures ?? []) add(`${prefix}:${subfeature.id}`, 'deferred')
  }

  add(`config:${config.id}`)

  for (const card of config.cards) {
    add(`card-face:${card.id}:normal`, carrierAvailability(card.normal.deferredReason))
    add(`card-face:${card.id}:inverted`, carrierAvailability(card.inverted.deferredReason))
  }
  for (const card of config.mysticCards) {
    const definitionAvailability = carrierAvailability(card.deferredReason)
    add(`mystic-card:${card.id}`, definitionAvailability)
    // Face carriers own their own marker. Inheriting the parent definition marker here would
    // hide an accidental deletion of one of the retained normal/inverted face blockers.
    add(`mystic-card-face:${card.id}:normal`, carrierAvailability(card.normal.deferredReason))
    add(`mystic-card-face:${card.id}:inverted`, carrierAvailability(card.inverted.deferredReason))
  }

  for (const rule of config.durak.scoringRules) add(`durak-scoring:${rule.id}`)
  for (const gift of config.gifts.definitions) {
    add(`gift:${gift.id}`, carrierAvailability(gift.deferredReason))
  }
  for (const resource of config.empire.resources) {
    add(`resource:${resource.id}`, carrierAvailability(resource.deferredReason))
  }
  for (const building of config.empire.buildings) {
    const availability = carrierAvailability(building.deferredReason)
    add(`building:${building.id}`, availability)
    for (const level of building.levels) {
      add(`building-level:${building.id}:${level.level}`, availability)
    }
    addSubfeatures(`building-subfeature:${building.id}`, building.deferredSubfeatures)
  }
  for (const unit of config.empire.units ?? []) {
    const availability = carrierAvailability(unit.deferredReason)
    add(`unit:${unit.id}`, availability)
    for (const loadout of unit.loadouts ?? []) {
      add(`unit-loadout:${unit.id}:${loadout.id}`, availability)
    }
  }
  for (const technology of config.empire.technologies) {
    const availability = carrierAvailability(technology.deferredReason)
    add(`technology:${technology.id}`, availability)
    for (const side of technology.sides?.definitions ?? []) {
      add(`technology-side:${technology.id}:${side.id}`, availability)
    }
    addSubfeatures(`technology-subfeature:${technology.id}`, technology.deferredSubfeatures)
  }
  for (const event of config.empire.events) {
    const eventAvailability = carrierAvailability(event.deferredReason)
    add(`event:${event.id}`, eventAvailability)
    for (const choice of event.choices) {
      add(
        `event-choice:${event.id}:${choice.id}`,
        choice.deferredReason || eventAvailability === 'deferred' ? 'deferred' : 'configured',
      )
    }
  }
  for (const quest of config.quests.definitions) {
    const questAvailability = carrierAvailability(quest.deferredReason)
    add(`quest:${quest.id}`, questAvailability)
    for (const stage of quest.stages) {
      add(`quest-stage:${quest.id}:${stage.id}`, questAvailability)
      for (const node of stage.nodes) {
        add(`quest-node:${quest.id}:${stage.id}:${node.id}`, questAvailability)
        for (const choice of node.choices) {
          add(
            `quest-choice:${quest.id}:${choice.id}`,
            choice.deferredReason || questAvailability === 'deferred' ? 'deferred' : 'configured',
          )
        }
      }
    }
    for (const cycle of quest.allowedCycles ?? []) {
      add(`quest-allowed-cycle:${quest.id}:${cycle.id}`, questAvailability)
    }
  }

  for (const type of config.combat.damageTypes) add(`combat-damage-type:${type.id}`)
  for (const armor of config.combat.armorClasses) add(`combat-armor-class:${armor.id}`)
  for (const rule of config.combat.counterRules) add(`combat-counter-rule:${rule.id}`)
  for (const equipment of config.combat.equipment) {
    add(`combat-equipment:${equipment.id}`, carrierAvailability(equipment.deferredReason))
  }
  for (const base of config.td.towerBases ?? []) {
    add(`td-tower-base:${base.id}`)
    for (const loadout of base.loadouts ?? []) {
      add(`td-tower-loadout:${base.id}:${loadout.id}`)
    }
  }
  for (const battlefield of config.td.battlefields) {
    add(`td-battlefield:${battlefield.id}`)
    for (const node of battlefield.laneGraph.nodes) {
      add(`td-lane-node:${battlefield.id}:${node.id}`)
    }
    for (const edge of battlefield.laneGraph.edges) {
      add(`td-lane-edge:${battlefield.id}:${edge.id}`)
    }
    for (const spot of battlefield.buildSpots) {
      add(`td-build-spot:${battlefield.id}:${spot.id}`)
    }
    for (const modifier of battlefield.modifiers) {
      add(`td-battlefield-modifier:${battlefield.id}:${modifier.id}`)
    }
  }
  for (const tower of config.td.towers) add(`td-tower-choice:${tower.id}`)
  for (const set of config.td.gradeChoices ?? []) {
    add(`td-grade-choice:${set.id}`, carrierAvailability(set.deferredReason))
  }
  for (const wave of config.td.waves) {
    add(`td-wave:${wave.id}`)
    for (const group of wave.groups) add(`td-wave-group:${wave.id}:${group.id}`)
  }
  for (const variant of config.td.planVariants ?? []) {
    const availability = carrierAvailability(variant.deferredReason)
    add(`td-plan-variant:${variant.id}`, availability)
    add(`td-objective:${variant.id}:${variant.objective.id}`, availability)
  }
  for (const line of config.td.equipmentProductionLines ?? []) {
    add(`td-production-line:${line.id}`)
  }
  for (const recipe of config.td.equipmentProduction ?? []) add(`td-production:${recipe.id}`)

  for (const offer of config.tavern.mercenaries.offers) add(`tavern-offer:${offer.id}`)
  addSubfeatures('tavern-subfeature', config.tavern.deferredSubfeatures)
  add('tavern-maria-game', carrierAvailability(config.tavern.maria.encounterDeferredReason))
  for (const piece of config.alchemy.pieces) add(`alchemy-piece:${piece.id}`)
  for (const recipe of config.alchemy.recipes) {
    add(`alchemy-recipe:${recipe.id}`, carrierAvailability(recipe.deferredReason))
  }
  addSubfeatures('alchemy-subfeature', config.alchemy.deferredSubfeatures)
  for (const item of config.inventory.itemDefinitions) {
    add(`inventory-item:${item.id}`, carrierAvailability(item.deferredReason))
  }
  addSubfeatures('inventory-subfeature', config.inventory.deferredSubfeatures)
  for (const zone of config.expeditions.zones) {
    add(`expedition-zone:${zone.id}`)
    addSubfeatures(`expedition-zone-subfeature:${zone.id}`, zone.deferredSubfeatures)
  }
  for (const profile of config.expeditions.enemyProfiles) {
    add(`expedition-enemy-profile:${profile.id}`)
  }
  for (const expedition of config.expeditions.definitions) {
    add(`expedition:${expedition.id}`, carrierAvailability(expedition.deferredReason))
  }
  add(
    'expedition-veteran-later-bonus',
    carrierAvailability(config.expeditions.veteran.laterBattleBonusDeferredReason),
  )

  for (const region of config.empire.map.regions) add(`map-region:${region.id}`)
  for (const subregion of config.empire.map.subregions) add(`map-subregion:${subregion.id}`)
  for (const object of config.empire.map.objects) {
    add(
      `map-object:${object.id}`,
      object.kind === 'fortress' ? carrierAvailability(object.payload.deferredReason) : 'configured',
    )
    for (const [property] of Object.entries(object.properties ?? {})) {
      if (property !== 'cityId' && property !== 'visualKind') {
        add(`map-object-property:${object.id}:${property}`, 'review')
      }
    }
  }
  for (const city of config.empire.cities) {
    add(`city:${city.id}`)
    for (const slot of city.slots) add(`city-slot:${city.id}:${slot.id}`)
  }
  for (const populationClass of config.empire.populationClasses) {
    add(`population-class:${populationClass.id}`)
  }
  for (const season of config.empire.seasons.definitions) add(`season:${season.id}`)
  for (const combination of config.empire.hiddenCombinations.definitions) {
    add(`hidden-combination:${combination.id}`, carrierAvailability(combination.deferredReason))
  }
  for (const epidemic of config.empire.epidemics.definitions) {
    add(`epidemic:${epidemic.id}`)
    for (const stage of epidemic.stages) add(`epidemic-stage:${epidemic.id}:${stage.id}`)
  }
  for (const protection of config.empire.epidemics.protections) {
    add(`epidemic-protection:${protection.id}`)
  }
  for (const gate of config.empire.loyalty.classGates) {
    add(`loyalty-class-gate:${gate.id}`)
  }
  for (const action of config.empire.domesticEconomy.fair.actions) add(`fair-action:${action.id}`)
  for (const actor of config.empire.externalEconomy.actors) add(`external-actor:${actor.id}`)
  for (const union of config.empire.externalEconomy.unions) add(`external-union:${union.id}`)
  for (const offer of config.empire.externalEconomy.offers) add(`external-offer:${offer.id}`)
  for (const building of config.empire.externalEconomy.reviewedAbsentBuildings) {
    add(`reviewed-absent-building:${building.id}`, 'review')
  }
  for (const [incident, reason] of Object.entries(
    config.empire.domesticEconomy.insurance.unsupportedIncidentReasons,
  )) {
    if (reason) add(`insurance-unsupported-incident:${incident}`, 'deferred')
  }

  for (const advisor of config.governance.advisors) {
    add(`governance-advisor:${advisor.id}`)
    if (advisor.accessDeferredReason) add(`governance-advisor-access:${advisor.id}`, 'deferred')
  }
  for (const decision of config.governance.advisorDecisions) {
    add(`governance-advisor-decision:${decision.id}`)
  }
  for (const perst of config.governance.persts) add(`governance-perst:${perst.id}`)
  for (const site of config.governance.governor.citySites) {
    add(`governance-city-site:${site.cityId}`)
  }
  for (const site of config.governance.capital.sites) {
    add(`governance-capital-site:${site.id}`, carrierAvailability(site.deferredReason))
  }
  for (const line of config.god.lines) add(`god-line:${line.id}`)

  for (const lifecycle of [
    'durak',
    'combat',
    'td',
    'steel-research',
    'seasons',
    'hidden-combinations',
    'loyalty',
    'governance',
    'epidemics',
    'medical',
    'domestic-economy',
    'external-economy',
    'economy-content',
    'quests',
    'god',
    'tavern',
    'alchemy',
    'expeditions',
    'inventory',
  ]) add(`lifecycle:${lifecycle}`)

  return carriers
}

function hasText(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0
}

function validateCoverageFields(
  label: string,
  value: Pick<
    EmpiresConfigCarrierCoverageGroup,
    'id' | 'rawSources' | 'owner' | 'consumer' | 'testEvidence' | 'disposition' | 'designerQuestion'
  >,
): string[] {
  const errors: string[] = []
  if (!hasText(value.id)) errors.push(`${label} needs a stable id`)
  if (!EMPIRES_CONTENT_DISPOSITIONS.includes(value.disposition)) {
    errors.push(`${label} has unknown disposition ${String(value.disposition)}`)
  }
  if (!Array.isArray(value.rawSources) || value.rawSources.length === 0
    || value.rawSources.some(source => !hasText(source))) {
    errors.push(`${label} needs at least one raw source`)
  }
  if (!hasText(value.owner)) errors.push(`${label} needs an owner`)
  if (!hasText(value.consumer)) errors.push(`${label} needs a consumer or explicit none/blocker`)
  if (!Array.isArray(value.testEvidence) || value.testEvidence.length === 0
    || value.testEvidence.some(evidence => !hasText(evidence))) {
    errors.push(`${label} needs test evidence`)
  }
  if (['blocked-semantic', 'blocked-substrate', 'review'].includes(value.disposition)
    && !hasText(value.designerQuestion)) {
    errors.push(`${label} needs an exact designer question`)
  }
  return errors
}

export function validateEmpiresContentCoverage(
  config: EmpiresEndgameConfig,
  manifest: EmpiresContentCoverageManifest,
): string[] {
  const errors: string[] = []
  if (manifest.schemaVersion !== 1) errors.push('content manifest schemaVersion must be 1')
  if (manifest.configSchemaVersion !== config.schemaVersion) {
    errors.push('content manifest configSchemaVersion does not match the bundled config')
  }
  if (!/^\d{4}-\d{2}-\d{2}$/.test(manifest.auditedAt)) {
    errors.push('content manifest auditedAt must be YYYY-MM-DD')
  }

  const actualCarriers = collectEmpiresConfigCarriers(config)
  const actualByKey = new Map<string, EmpiresConfigCarrier>()
  for (const carrier of actualCarriers) {
    if (actualByKey.has(carrier.key)) errors.push(`collector repeats config carrier ${carrier.key}`)
    actualByKey.set(carrier.key, carrier)
  }

  const coveredKeys = new Set<string>()
  const groupIds = new Set<string>()
  for (const group of manifest.configGroups) {
    const label = `config coverage group ${group.id || '<missing>'}`
    errors.push(...validateCoverageFields(label, group))
    if (!['configured', 'deferred', 'review'].includes(group.expectedAvailability)) {
      errors.push(`${label} has unknown expectedAvailability ${String(group.expectedAvailability)}`)
    }
    if (groupIds.has(group.id)) errors.push(`content manifest repeats group ${group.id}`)
    groupIds.add(group.id)
    if (!Array.isArray(group.configCarrierKeys) || group.configCarrierKeys.length === 0) {
      errors.push(`${label} needs configCarrierKeys`)
      continue
    }
    for (const key of group.configCarrierKeys) {
      if (!hasText(key)) {
        errors.push(`${label} contains an empty config carrier key`)
        continue
      }
      if (coveredKeys.has(key)) errors.push(`content manifest covers config carrier ${key} twice`)
      coveredKeys.add(key)
      const carrier = actualByKey.get(key)
      if (!carrier) {
        errors.push(`content manifest references unknown config carrier ${key}`)
        continue
      }
      if (carrier.availability !== group.expectedAvailability) {
        errors.push(
          `config carrier ${key} changed availability from ${group.expectedAvailability} to ${carrier.availability}`,
        )
      }
      if (carrier.availability === 'deferred'
        && ['live', 'ready-now', 'out'].includes(group.disposition)) {
        errors.push(`deferred config carrier ${key} cannot be ${group.disposition}`)
      }
      if (carrier.availability === 'review' && group.disposition !== 'review') {
        errors.push(`review config carrier ${key} must keep review disposition`)
      }
      if (carrier.availability === 'configured' && group.disposition === 'out') {
        errors.push(`configured carrier ${key} cannot be out`)
      }
    }
  }
  for (const key of actualByKey.keys()) {
    if (!coveredKeys.has(key)) errors.push(`unowned config carrier ${key}`)
  }

  const rawIdentities = new Set<string>()
  const rawGroupIds = new Set<string>()
  for (const group of manifest.rawCatalogGroups) {
    const label = `raw coverage group ${group.id || '<missing>'}`
    errors.push(...validateCoverageFields(label, group))
    if (rawGroupIds.has(group.id)) errors.push(`content manifest repeats raw group ${group.id}`)
    rawGroupIds.add(group.id)
    if (!Array.isArray(group.stableIdentities) || group.stableIdentities.length === 0) {
      errors.push(`${label} needs stableIdentities`)
    } else {
      for (const identity of group.stableIdentities) {
        if (!hasText(identity)) errors.push(`${label} contains an empty raw identity`)
        if (rawIdentities.has(identity)) errors.push(`content manifest repeats raw identity ${identity}`)
        rawIdentities.add(identity)
      }
    }
    if (!Number.isInteger(group.expectedLinkedConfigCarrierCount)
      || group.expectedLinkedConfigCarrierCount < 0) {
      errors.push(`${label} needs a non-negative expectedLinkedConfigCarrierCount`)
    }
    if (!Array.isArray(group.linkedConfigCarrierKeys)) {
      errors.push(`${label} needs explicit linkedConfigCarrierKeys (use [] when config-absent)`)
    } else {
      if (group.linkedConfigCarrierKeys.length !== group.expectedLinkedConfigCarrierCount) {
        errors.push(
          `${label} expected ${group.expectedLinkedConfigCarrierCount} config links but has ${group.linkedConfigCarrierKeys.length}`,
        )
      }
      const linkedKeys = new Set<string>()
      for (const key of group.linkedConfigCarrierKeys) {
        if (!hasText(key)) {
          errors.push(`${label} contains an empty config link`)
          continue
        }
        if (linkedKeys.has(key)) errors.push(`${label} links config carrier ${key} twice`)
        linkedKeys.add(key)
        if (!actualByKey.has(key)) errors.push(`${label} links unknown config carrier ${key}`)
      }
    }
  }

  const sourceIds = new Set<string>()
  const sourceMessageIds = new Set<string>()
  for (const source of manifest.sourceInventory) {
    if (!hasText(source.id) || sourceIds.has(source.id)) {
      errors.push(`raw source inventory needs unique non-empty ids; got ${source.id}`)
    }
    sourceIds.add(source.id)
    if (!hasText(source.path) || !hasText(source.evidence)) {
      errors.push(`raw source ${source.id} needs a path and evidence`)
    }
    if (source.messageCount !== null
      && (!Number.isInteger(source.messageCount) || source.messageCount < 0)) {
      errors.push(`raw source ${source.id} has invalid messageCount`)
    }
    if (source.messageCount === null) {
      if (source.messageIds !== null) {
        errors.push(`raw source ${source.id} with null messageCount must keep null messageIds`)
      }
    } else if (!Array.isArray(source.messageIds)) {
      errors.push(`raw source ${source.id} needs an explicit messageIds snapshot`)
    } else {
      if (source.messageIds.length !== source.messageCount) {
        errors.push(
          `raw source ${source.id} expected ${source.messageCount} messageIds but has ${source.messageIds.length}`,
        )
      }
      const localMessageIds = new Set<string>()
      for (const messageId of source.messageIds) {
        if (!hasText(messageId) || !/^\d+$/.test(messageId)) {
          errors.push(`raw source ${source.id} has invalid message id ${String(messageId)}`)
          continue
        }
        if (localMessageIds.has(messageId)) {
          errors.push(`raw source ${source.id} repeats message id ${messageId}`)
        }
        localMessageIds.add(messageId)
        if (sourceMessageIds.has(messageId)) {
          errors.push(`raw source inventory repeats message id ${messageId} across sources`)
        }
        sourceMessageIds.add(messageId)
      }
    }
    if (!['review', 'out'].includes(source.residualDisposition)) {
      errors.push(`raw source ${source.id} needs a review/out residual disposition`)
    }
    if (source.role === 'out' && source.residualDisposition !== 'out') {
      errors.push(`out-of-scope raw source ${source.id} must keep an out residual disposition`)
    }
    if (source.role !== 'out' && source.residualDisposition === 'out') {
      errors.push(`raw source ${source.id} cannot exclude residual messages without an out role`)
    }
    if (!hasText(source.residualOwner) || !hasText(source.residualConsumer)
      || !Array.isArray(source.residualTestEvidence)
      || source.residualTestEvidence.length === 0
      || source.residualTestEvidence.some(evidence => !hasText(evidence))) {
      errors.push(`raw source ${source.id} needs residual owner, consumer and test evidence`)
    }
    if (source.residualDisposition === 'review' && !hasText(source.residualDesignerQuestion)) {
      errors.push(`raw source ${source.id} needs a residual designer question`)
    }
  }
  const referencedSourceIds = new Set<string>()
  for (const group of [...manifest.configGroups, ...manifest.rawCatalogGroups]) {
    for (const sourceId of group.rawSources) {
      referencedSourceIds.add(sourceId)
      if (!sourceIds.has(sourceId)) {
        errors.push(`coverage group ${group.id} references unknown raw source ${sourceId}`)
      }
    }
  }
  for (const sourceId of sourceIds) {
    if (!referencedSourceIds.has(sourceId)) {
      errors.push(`raw source ${sourceId} is not referenced by any coverage group`)
    }
  }

  return errors
}

export function summarizeEmpiresContentCoverage(manifest: EmpiresContentCoverageManifest) {
  const config: Record<EmpiresContentDisposition, number> = {
    live: 0,
    'ready-now': 0,
    'blocked-semantic': 0,
    'blocked-substrate': 0,
    review: 0,
    out: 0,
  }
  const raw = { ...config }
  for (const group of manifest.configGroups) {
    config[group.disposition] += group.configCarrierKeys.length
  }
  for (const group of manifest.rawCatalogGroups) {
    raw[group.disposition] += group.stableIdentities.length
  }
  return {
    config,
    raw,
    configTotal: Object.values(config).reduce((sum, count) => sum + count, 0),
    rawTotal: Object.values(raw).reduce((sum, count) => sum + count, 0),
    sourceTotal: manifest.sourceInventory.length,
    sourceMessageTotal: manifest.sourceInventory.reduce(
      (sum, source) => sum + (source.messageIds?.length ?? 0),
      0,
    ),
  }
}
