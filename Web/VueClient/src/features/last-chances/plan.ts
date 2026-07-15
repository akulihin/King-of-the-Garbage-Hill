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
  LastChancesTierDefinition,
  LastChancesVector,
} from './types'

function copyVector(value: LastChancesVector): LastChancesVector {
  return { x: value.x, y: value.y }
}

function makeEnemyPlan(
  tier: LastChancesTierDefinition,
  room: LastChancesRoomTemplate,
  nodeId: string,
  rng: () => number,
): LastChancesPlanEnemy[] {
  const count = lastChancesRandomInt(rng, tier.enemyCount[0], tier.enemyCount[1])
  const spawns = lastChancesShuffle(room.enemySpawns, rng)
  return Array.from({ length: count }, (_, index) => {
    const base = spawns[index % spawns.length]
    const cycle = Math.floor(index / spawns.length)
    const angle = rng() * Math.PI * 2
    const jitter = cycle * 26
    const poolEntry = pickLastChancesWeighted(tier.enemyPool, rng)
    return {
      id: `${nodeId}-enemy-${index + 1}`,
      definitionId: poolEntry.enemyId,
      position: {
        x: Math.max(0, Math.min(room.width, base.x + Math.cos(angle) * jitter)),
        y: Math.max(0, Math.min(room.height, base.y + Math.sin(angle) * jitter)),
      },
    }
  })
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
  return {
    id,
    tierIndex,
    tierId: tier.id,
    tierKind: tier.kind,
    label: `${tier.label} · ${room.name}`,
    accent: tier.accent,
    roomTemplateId: room.id,
    roomName: room.name,
    roomArchetype: room.archetype,
    seed,
    arena: {
      width: room.width,
      height: room.height,
      playerSpawn: copyVector(room.playerSpawn),
      obstacles: room.obstacles.map(obstacle => ({ ...obstacle })),
    },
    enemies: makeEnemyPlan(tier, room, id, rng),
    nextNodeIds: [],
  }
}

function connectTiers(config: LastChancesConfig, planSeed: string, tiers: LastChancesPlanNode[][]): void {
  for (let tierIndex = 0; tierIndex < tiers.length - 1; tierIndex += 1) {
    const currentTier = tiers[tierIndex]
    const nextTier = tiers[tierIndex + 1]
    const choiceCount = Math.min(config.graph.choicesPerNode, nextTier.length)
    for (const node of currentTier) {
      const rng = createLastChancesRng(`${planSeed}:connections:${node.id}`)
      node.nextNodeIds = lastChancesShuffle(nextTier.map(next => next.id), rng).slice(0, choiceCount)
    }

    for (let nextIndex = 0; nextIndex < nextTier.length; nextIndex += 1) {
      const nextNode = nextTier[nextIndex]
      if (currentTier.some(node => node.nextNodeIds.includes(nextNode.id))) continue
      const parent = currentTier[nextIndex % currentTier.length]
      parent.nextNodeIds.push(nextNode.id)
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
