/**
 * Final shipped safety ceilings for Empire's Endgame.
 *
 * These are deliberately above the bundled baselines, but finite so a custom
 * config, imported plan, or long-running campaign cannot create unbounded
 * frame catch-up, replay work, or localStorage growth.
 */
export const EMPIRES_STABILIZATION_BUDGETS = Object.freeze({
  qaActions: 10_000,
  maxTicks: 10_000,
  maxCommands: 512,
  maxCatchUpTicksPerFrame: 16,
  maxLogicalReplayDurationMs: 300_000,
  maxResultRetention: 256,
  maxHistoryRetention: 256,
  maxBoardCells: 4_096,
  maxPlanItems: 256,
  maxRosterUnitInstances: 2_048,
  maxTavernOffers: 64,
  maxSettledMinigames: 4_096,
  legacySettledSessionRetention: 64,
  recentBattleLossIdentityRetention: 64,
  recentExpeditionComplaintRetention: 64,
  endedEpidemicRetention: 32,
  longCampaignSaveUtf8Bytes: 512 * 1_024,
})

export const EMPIRES_SAVE_SCHEMA_VERSION = 16 as const

export function empiresUtf8ByteLength(value: string): number {
  return new TextEncoder().encode(value).byteLength
}
