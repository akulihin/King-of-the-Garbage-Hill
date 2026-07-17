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

function makeEnemyPlan(
  config: LastChancesConfig,
  tier: LastChancesTierDefinition,
  enemySpawns: LastChancesVector[],
  playerSpawn: LastChancesVector,
  nodeId: string,
  rng: () => number,
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
  for (const definitionId of tier.guaranteedEnemyIds ?? []) {
    if (available.length === 0) break
    let farthestIndex = 0
    for (let index = 1; index < available.length; index += 1) {
      if (distanceSquared(available[index], playerSpawn)
        > distanceSquared(available[farthestIndex], playerSpawn)) farthestIndex = index
    }
    nextEnemy(definitionId, available.splice(farthestIndex, 1)[0])
  }
  let swarmDefinitionId: string | null = null
  for (let index = 0; index < count; index += 1) {
    const poolEntry = pickLastChancesWeighted(tier.enemyPool, rng)
    const definition = config.enemies.find(candidate => candidate.id === poolEntry.enemyId)
    // A rolled swarm-type slot becomes the room's swarm event; extra rolls collapse into it.
    if (definition?.swarm) {
      swarmDefinitionId = poolEntry.enemyId
      continue
    }
    if (available.length === 0) break
    nextEnemy(poolEntry.enemyId, available.shift() as LastChancesVector)
  }
  if (!swarmDefinitionId) return { enemies, swarm: null }
  const edges = lastChancesShuffle([...LAST_CHANCES_ARENA_EDGES], rng)
    .slice(0, 2) as [LastChancesArenaEdge, LastChancesArenaEdge]
  return { enemies, swarm: { definitionId: swarmDefinitionId, edges } }
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
): LastChancesPlanNode {
  const id = `${tier.id}-${nodeIndex + 1}`
  const seed = hashLastChancesSeed(`${planSeed}:${id}`)
  const rng = createLastChancesRng(seed)
  const roomId = tier.roomTemplateIds[Math.floor(rng() * tier.roomTemplateIds.length)]
  const room = config.rooms.find(candidate => candidate.id === roomId) as LastChancesRoomTemplate
  const layouts = roomSpawnLayouts(room)
  const spawnLayout = layouts[Math.floor(rng() * layouts.length)]
  const roll = makeEnemyPlan(config, tier, spawnLayout.enemySpawns, room.playerSpawn, id, rng)
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
  const tiers = config.progression.tiers.map((tier, tierIndex) => (
    Array.from({ length: tier.nodeCount }, (_, nodeIndex) => (
      makeNode(config, seed, tier, tierIndex, nodeIndex)
    ))
  ))
  connectTiers(config, seed, tiers)
  return {
    generation,
    seed,
    tiers,
    nodes: tiers.flat(),
  }
}
