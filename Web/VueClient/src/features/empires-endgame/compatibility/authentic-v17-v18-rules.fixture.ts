/**
 * Immutable bundled minigame identities derived independently from repository
 * history, rather than from the current config:
 *
 * - v17: production config + identity builders at commit 69230b07.
 * - v18: that exact v17 artifact after the Phase-14 v17 -> v18 migration.
 *
 * Keeping this test fixture separate from engine.ts ensures a compatibility
 * test cannot pass by deriving both its input and recognizer from today's
 * mutable bundled catalog.
 */
export const AUTHENTIC_V17_V18_RULES_IDENTITIES = {
  17: {
    td: { configSchemaVersion: 17, rulesDigest: '9d06db5f8e787590' },
    tavern: { configSchemaVersion: 17, rulesDigest: 'bda17289c466fd64' },
    alchemy: { configSchemaVersion: 17, rulesDigest: '811cbf29fbff5088' },
    inventory: { configSchemaVersion: 17, rulesDigest: '7072b88e10cf43d4' },
  },
  18: {
    td: { configSchemaVersion: 18, rulesDigest: 'b90b7e94b057fb65' },
    tavern: { configSchemaVersion: 18, rulesDigest: '2871f17de5462196' },
    alchemy: { configSchemaVersion: 18, rulesDigest: 'ea7d4b6ba97401c4' },
    inventory: { configSchemaVersion: 18, rulesDigest: '93f9d6bb12721fdb' },
    clash: { configSchemaVersion: 18, rulesDigest: 'd8a3fdd15b5e8ab8' },
  },
} as const
