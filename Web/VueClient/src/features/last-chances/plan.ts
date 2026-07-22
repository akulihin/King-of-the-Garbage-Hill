import {
  createLastChancesRng,
  hashLastChancesSeed,
  lastChancesRandomInt,
  lastChancesShuffle,
  pickLastChancesWeighted,
} from './rng'
import { LAST_CHANCES_ARENA_EDGES } from './types'
import type {
  LastChancesArenaEdge,
  LastChancesConfig,
  LastChancesGamePlan,
  LastChancesPlanEnemy,
  LastChancesPlanNode,
  LastChancesPlanSwarm,
  LastChancesRoomTemplate,
  LastChancesSpawnLayoutDefinition,
  LastChancesTierDefinition,
  LastChancesVector,
} from './types'

function copyVector(value: LastChancesVector): LastChancesVector {
  return { x: value.x, y: value.y }
}

function distanceSquared(a: LastChancesVector, b: LastChancesVector): number {
  return (a.x - b.x) ** 2 + (a.y - b.y) ** 2
}

interface EnemyPlanRoll {
  enemies: LastChancesPlanEnemy[]
  swarm: LastChancesPlanSwarm | null
}

interface TierDiversityState {
  usedRoomTemplateIds: Set<string>
  usedSpawnLayouts: Set<string>
  usedSpecialEnemyIds: Set<string>
  usedSwarmEnemyIds: Set<string>
}

function makeEnemyPlan(
  config: LastChancesConfig,
  tier: LastChancesTierDefinition,
  enemySpawns: LastChancesVector[],
  playerSpawn: LastChancesVector,
  nodeId: string,
  rng: () => number,
  guaranteedEnemyIds: string[],
  diversity: TierDiversityState,
): EnemyPlanRoll {
  const count = lastChancesRandomInt(rng, tier.enemyCount[0], tier.enemyCount[1])
  const available = lastChancesShuffle(enemySpawns, rng)
  const enemies: LastChancesPlanEnemy[] = []
  const nextEnemy = (definitionId: string, spawn: LastChancesVector): void => {
    enemies.push({
      id: `${nodeId}-enemy-${enemies.length + 1}`,
      definitionId,
      position: copyVector(spawn),
    })
  }
  for (const definitionId of guaranteedEnemyIds) {
    if (available.length === 0) break
    let farthestIndex = 0
    for (let index = 1; index < available.length; index += 1) {
      if (distanceSquared(available[index], playerSpawn)
        > distanceSquared(available[farthestIndex], playerSpawn)) farthestIndex = index
    }
    nextEnemy(definitionId, available.splice(farthestIndex, 1)[0])
    diversity.usedSpecialEnemyIds.add(definitionId)
  }
  let swarmDefinitionId: string | null = null
  for (let index = 0; index < count; index += 1) {
    const diversePool = tier.enemyPool.filter((entry) => {
      const definition = config.enemies.find(candidate => candidate.id === entry.enemyId)
      if (definition?.swarm) return !diversity.usedSwarmEnemyIds.has(entry.enemyId)
      if (definition?.role === 'elite' || definition?.role === 'boss') {
        return !diversity.usedSpecialEnemyIds.has(entry.enemyId)
      }
      return true
    })
    const ordinaryPool = tier.enemyPool.filter((entry) => {
      const definition = config.enemies.find(candidate => candidate.id === entry.enemyId)
      return !definition?.swarm && definition?.role !== 'elite' && definition?.role !== 'boss'
    })
    const poolEntry = pickLastChancesWeighted(
      diversePool.length > 0 ? diversePool : ordinaryPool.length > 0 ? ordinaryPool : tier.enemyPool,
      rng,
    )
    const definition = config.enemies.find(candidate => candidate.id === poolEntry.enemyId)
    // A rolled swarm-type slot becomes the room's swarm event; extra rolls collapse into it.
    if (definition?.swarm) {
      swarmDefinitionId = poolEntry.enemyId
      diversity.usedSwarmEnemyIds.add(poolEntry.enemyId)
      continue
    }
    if (available.length === 0) break
    nextEnemy(poolEntry.enemyId, available.shift() as LastChancesVector)
    if (definition?.role === 'elite' || definition?.role === 'boss') {
      diversity.usedSpecialEnemyIds.add(poolEntry.enemyId)
    }
  }
  if (!swarmDefinitionId) return { enemies, swarm: null }
  const edges = lastChancesShuffle([...LAST_CHANCES_ARENA_EDGES], rng)
    .slice(0, 2) as [LastChancesArenaEdge, LastChancesArenaEdge]
  return { enemies, swarm: { definitionId: swarmDefinitionId, edges } }
}

function makeFixedEncounterPlan(
  room: LastChancesRoomTemplate,
  enemySpawns: LastChancesVector[],
  playerSpawn: LastChancesVector,
  nodeId: string,
  rng: () => number,
): EnemyPlanRoll {
  const encounter = room.encounter
  if (!encounter) return { enemies: [], swarm: null }
  const available = lastChancesShuffle(enemySpawns, rng)
  const enemies: LastChancesPlanEnemy[] = []
  for (const definitionId of encounter.enemyIds) {
    if (available.length === 0) break
    let farthestIndex = 0
    for (let index = 1; index < available.length; index += 1) {
      if (distanceSquared(available[index], playerSpawn)
        > distanceSquared(available[farthestIndex], playerSpawn)) farthestIndex = index
    }
    enemies.push({
      id: `${nodeId}-enemy-${enemies.length + 1}`,
      definitionId,
      position: copyVector(available.splice(farthestIndex, 1)[0]),
    })
  }
  if (!encounter.swarmEnemyId) return { enemies, swarm: null }
  const edges = lastChancesShuffle([...LAST_CHANCES_ARENA_EDGES], rng)
    .slice(0, 2) as [LastChancesArenaEdge, LastChancesArenaEdge]
  return {
    enemies,
    swarm: {
      definitionId: encounter.swarmEnemyId,
      edges,
      infinite: encounter.infiniteSwarm === true,
    },
  }
}

function roomSpawnLayouts(room: LastChancesRoomTemplate): LastChancesSpawnLayoutDefinition[] {
  if (room.spawnLayouts?.length) return room.spawnLayouts
  return [{ id: 'legacy', name: 'Legacy layout', enemySpawns: room.enemySpawns ?? [] }]
}

function makeNode(
  config: LastChancesConfig,
  planSeed: string,
  tier: LastChancesTierDefinition,
  tierIndex: number,
  nodeIndex: number,
  guaranteedEnemyIds: string[],
  diversity: TierDiversityState,
  forcedRoomId?: string,
): LastChancesPlanNode {
  const id = `${tier.id}-${nodeIndex + 1}`
  const seed = hashLastChancesSeed(`${planSeed}:${id}`)
  const rng = createLastChancesRng(seed)
  const ordinaryRoomIds = tier.roomTemplateIds.filter(id => (
    !(tier.guaranteedRoomTemplateIds ?? []).includes(id)
  ))
  const randomRoomIds = ordinaryRoomIds.length > 0 ? ordinaryRoomIds : tier.roomTemplateIds
  const unusedRoomIds = randomRoomIds.filter(id => !diversity.usedRoomTemplateIds.has(id))
  const roomCandidates = unusedRoomIds.length > 0 ? unusedRoomIds : randomRoomIds
  const roomId = forcedRoomId ?? roomCandidates[Math.floor(rng() * roomCandidates.length)]
  diversity.usedRoomTemplateIds.add(roomId)
  const room = config.rooms.find(candidate => candidate.id === roomId) as LastChancesRoomTemplate
  const layouts = roomSpawnLayouts(room)
  const unusedLayouts = layouts.filter(layout => (
    !diversity.usedSpawnLayouts.has(`${room.id}:${layout.id}`)
  ))
  const layoutCandidates = unusedLayouts.length > 0 ? unusedLayouts : layouts
  const spawnLayout = layoutCandidates[Math.floor(rng() * layoutCandidates.length)]
  diversity.usedSpawnLayouts.add(`${room.id}:${spawnLayout.id}`)
  const roll = room.encounter
    ? makeFixedEncounterPlan(room, spawnLayout.enemySpawns, room.playerSpawn, id, rng)
    : makeEnemyPlan(
        config,
        tier,
        spawnLayout.enemySpawns,
        room.playerSpawn,
        id,
        rng,
        guaranteedEnemyIds,
        diversity,
      )
  return {
    id,
    tierIndex,
    tierId: tier.id,
    tierKind: tier.kind,
    label: `${tier.label} · ${room.name}`,
    accent: tier.accent,
    roomTemplateId: room.id,
    spawnLayoutId: spawnLayout.id,
    roomName: room.name,
    roomArchetype: room.archetype,
    seed,
    arena: {
      width: room.width,
      height: room.height,
      playerSpawn: copyVector(room.playerSpawn),
      obstacles: room.obstacles.map(obstacle => ({ ...obstacle })),
      hazards: (room.hazards ?? []).map(hazard => ({ ...hazard })),
    },
    interaction: room.interaction
      ? JSON.parse(JSON.stringify(room.interaction)) as LastChancesPlanNode['interaction']
      : null,
    turrets: (room.turrets ?? []).map(turret => JSON.parse(JSON.stringify(turret))),
    bossHoles: (room.bossHoles ?? []).map(hole => JSON.parse(JSON.stringify(hole))),
    altar: room.altar ? JSON.parse(JSON.stringify(room.altar)) : null,
    ouroborosPickup: room.ouroborosPickup
      ? JSON.parse(JSON.stringify(room.ouroborosPickup))
      : null,
    enemies: roll.enemies,
    swarm: roll.swarm,
    nextNodeIds: [],
  }
}

function connectTiers(config: LastChancesConfig, planSeed: string, tiers: LastChancesPlanNode[][]): void {
  for (let tierIndex = 0; tierIndex < tiers.length - 1; tierIndex += 1) {
    const currentTier = tiers[tierIndex]
    const nextTier = tiers[tierIndex + 1]
    const choiceCount = Math.min(config.graph.choicesPerNode, nextTier.length)

    // Cover every next-tier node first without exceeding the authored per-node cap.
    // Config validation rejects graphs whose total outgoing capacity is insufficient.
    const coverageRng = createLastChancesRng(`${planSeed}:coverage:${tierIndex}`)
    const coverage = lastChancesShuffle(nextTier.map(node => node.id), coverageRng)
    coverage.forEach((nextNodeId, nextIndex) => {
      currentTier[nextIndex % currentTier.length].nextNodeIds.push(nextNodeId)
    })

    for (const node of currentTier) {
      const rng = createLastChancesRng(`${planSeed}:connections:${node.id}`)
      const remaining = lastChancesShuffle(nextTier.map(next => next.id), rng)
        .filter(nextNodeId => !node.nextNodeIds.includes(nextNodeId))
      node.nextNodeIds.push(...remaining.slice(0, choiceCount - node.nextNodeIds.length))
    }
  }
}

export function buildLastChancesPlan(
  config: LastChancesConfig,
  generation = 1,
  seedOverride?: string | number,
): LastChancesGamePlan {
  const seed = seedOverride === undefined
    ? `${config.seed}:${generation * config.graph.generationSeedStep}`
    : String(seedOverride)
  const tiers = config.progression.tiers.map((tier, tierIndex) => {
    const diversity: TierDiversityState = {
      usedRoomTemplateIds: new Set(),
      usedSpawnLayouts: new Set(),
      usedSpecialEnemyIds: new Set(),
      usedSwarmEnemyIds: new Set(),
    }
    const guaranteedRoomByNode = Array.from({ length: tier.nodeCount }, () => undefined as string | undefined)
    const assignmentRng = createLastChancesRng(`${seed}:${tier.id}:guaranteed`)
    const guaranteedRoomNodeOrder = lastChancesShuffle(
      Array.from({ length: tier.nodeCount }, (_, index) => index),
      assignmentRng,
    )
    ;(tier.guaranteedRoomTemplateIds ?? []).forEach((roomId, index) => {
      guaranteedRoomByNode[guaranteedRoomNodeOrder[index % guaranteedRoomNodeOrder.length]] = roomId
    })
    const guaranteedByNode = Array.from({ length: tier.nodeCount }, () => [] as string[])
    const ordinaryEnemyNodeIndexes = Array.from({ length: tier.nodeCount }, (_, index) => index)
      .filter((index) => {
        const forcedRoomId = guaranteedRoomByNode[index]
        return !forcedRoomId
          || !config.rooms.find(room => room.id === forcedRoomId)?.encounter
      })
    const guaranteedNodeOrder = lastChancesShuffle(
      ordinaryEnemyNodeIndexes.length > 0
        ? ordinaryEnemyNodeIndexes
        : Array.from({ length: tier.nodeCount }, (_, index) => index),
      assignmentRng,
    )
    const guaranteedIds = tier.guaranteedEnemyIds ?? []
    guaranteedIds.forEach((guaranteedId, index) => {
      guaranteedByNode[guaranteedNodeOrder[index % guaranteedNodeOrder.length]].push(guaranteedId)
    })
    return Array.from({ length: tier.nodeCount }, (_, nodeIndex) => (
      makeNode(
        config,
        seed,
        tier,
        tierIndex,
        nodeIndex,
        guaranteedByNode[nodeIndex],
        diversity,
        guaranteedRoomByNode[nodeIndex],
      )
    ))
  })
  connectTiers(config, seed, tiers)
  return {
    generation,
    seed,
    tiers,
    nodes: tiers.flat(),
  }
}
