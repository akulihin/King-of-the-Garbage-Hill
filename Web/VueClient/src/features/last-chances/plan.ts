import {
  createLastChancesRng,
  hashLastChancesSeed,
  lastChancesRandomInt,
  lastChancesShuffle,
  pickLastChancesWeighted,
} from './rng'
import type {
  LastChancesConfig,
  LastChancesGamePlan,
  LastChancesPlanEnemy,
  LastChancesPlanNode,
  LastChancesRoomTemplate,
  LastChancesSpawnLayoutDefinition,
  LastChancesTierDefinition,
  LastChancesVector,
} from './types'

function copyVector(value: LastChancesVector): LastChancesVector {
  return { x: value.x, y: value.y }
}

function makeEnemyPlan(
  tier: LastChancesTierDefinition,
  enemySpawns: LastChancesVector[],
  nodeId: string,
  rng: () => number,
): LastChancesPlanEnemy[] {
  const count = lastChancesRandomInt(rng, tier.enemyCount[0], tier.enemyCount[1])
  const spawns = lastChancesShuffle(enemySpawns, rng)
  return Array.from({ length: count }, (_, index) => {
    const spawn = spawns[index]
    const poolEntry = pickLastChancesWeighted(tier.enemyPool, rng)
    return {
      id: `${nodeId}-enemy-${index + 1}`,
      definitionId: poolEntry.enemyId,
      position: copyVector(spawn),
    }
  })
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
    enemies: makeEnemyPlan(tier, spawnLayout.enemySpawns, id, rng),
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
