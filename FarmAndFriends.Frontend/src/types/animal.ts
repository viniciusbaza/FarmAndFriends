/** Mirrors Contracts/Animals/AnimalContracts.cs; timestamps are authoritative UTC. */
export type AnimalAction = 'buy-animal-enclosure' | 'buy-animal' | 'collect' | 'help' | 'steal' | 'sell-animal-product'
export type AnimalOperationEffect = {
  action: AnimalAction; completedAt: string; farmId: string; animalId: string | null
  cycleId: string | null; animalTypeId: string | null; productId: string | null
  quantity: number; coinsSpent: number; coinsGained: number; xpGained: number; ownerBonusAdded: number
}
export type AnimalOperationResponse = {
  operationId: string; replayed: boolean; effect: AnimalOperationEffect; current: AnimalStateEnvelope | null
}
export type AnimalStateEnvelope = {
  farmId: string; enclosure: AnimalEnclosureDto | null; revision: number; observedAt: string
}
export type AnimalEnclosureDto = { id: string; slot: string; animals: AnimalDto[] }
export type AnimalDto = {
  id: string; animalTypeId: string; displayName: string; purchasedAt: string; maturesAt: string
  stage: 'chick' | 'calf' | 'lamb' | 'piglet' | 'peachick' | 'adult'; cycle: AnimalCycleDto | null; permissions: AnimalPermissions
  nextStateChangeAt: string | null
}
export type AnimalCycleDto = {
  id: string; productId: string; startsAt: string; readyAt: string; isReady: boolean
  baseYield: number; protectedHelpBonus: number; remainingBase: number; ownerCollectible: number
  helperCount: number; viewerHelped: boolean; helpers: AnimalHelperDto[] | null; helpersNextCursor: string | null
}
export type AnimalPermissions = {
  canCollect: boolean; canHelp: boolean; canSteal: boolean
  collectReason: string | null; helpReason: string | null; stealReason: string | null
  rewardAvailable: boolean; coinsReward: number; xpReward: number; nextRewardAt: string | null
}
export type AnimalHelperDto = { userId: string; displayNameAtHelp: string; helpedAt: string }
export type AnimalHelpersPage = { items: AnimalHelperDto[]; nextCursor: string | null; total: number }
export type AnimalEnclosureOffer = {
  priceCoins: number; minLevel: number; currentLevel: number; firstExpansionReached: boolean
  initialPlotsUnlocked: number; requiredInitialPlots: number; owned: boolean; canBuy: boolean; reason: string | null
}
export type AnimalOffer = {
  animalTypeId: string; displayName: string; priceCoins: number; minLevel: number
  growthDuration: number; productionDuration: number; owned: boolean; canBuy: boolean; reason: string | null
}
export type AnimalProductOffer = { itemId: string; displayName: string; sellPriceCoins: number }
