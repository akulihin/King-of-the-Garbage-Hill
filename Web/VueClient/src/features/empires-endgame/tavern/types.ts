export interface TavernRulesIdentity {
  configSchemaVersion: number
  rulesDigest: string
}

export interface TavernMercenaryOfferPlan {
  id: string
  name: string
  unitId: string
  count: number
  goldCost: number
}

export interface TavernRumorPlan {
  goldCost: number
  text: string
  deckHint: {
    position: number
    suit: string
    rank: string
  } | null
}

export interface TavernMariaPlan {
  present: boolean
  title: string
  description: string
  deferredReason: string | null
}

export interface TavernPlan {
  id: string
  sessionId: string
  rulesIdentity: TavernRulesIdentity
  con: number
  cityId: string
  goldAvailable: number
  sections: readonly ['tables', 'bar']
  mercenaryOffers: TavernMercenaryOfferPlan[]
  drinks: {
    goldCost: number
    readyAtCon: number
    expiresAfterCon: number
  }
  rumor: TavernRumorPlan
  maria: TavernMariaPlan
  maxCommands: number
}

export type TavernCommand =
  | { turn: number, kind: 'hire', offerId: string }
  | { turn: number, kind: 'buy-drinks' }
  | { turn: number, kind: 'buy-rumor' }
  | { turn: number, kind: 'finish' }

export interface TavernReplayState {
  turn: number
  commandLog: TavernCommand[]
  hiredOfferId: string | null
  drinksPurchased: boolean
  rumorPurchased: boolean
  goldSpent: number
  finished: boolean
  error: string | null
}

export interface TavernResult {
  kind: 'tavern'
  sessionId: string
  planId: string
  planDigest: string
  rulesIdentity: TavernRulesIdentity
  seed: string | number
  commandLog: TavernCommand[]
  commandDigest: string
  hiredOfferId: string | null
  drinksPurchased: boolean
  rumorPurchased: boolean
  goldSpent: number
  mariaPresent: boolean
  outcome: 'completed'
  error: string | null
}
