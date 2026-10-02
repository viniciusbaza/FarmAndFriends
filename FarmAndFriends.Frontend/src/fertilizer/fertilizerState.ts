import type { InventoryItem } from '../types/Inventory'

export const FERTILIZER_ITEM_ID = 'fertilizer'

export type FertilizerIntent =
  | { action: 'buy'; quantity: number }
  | { action: 'fertilize'; farmId: string; plotId: string; expectedHarvestCycle: number; expectedCycleStartedAt: string }

export type FertilizerAttempt = FertilizerIntent & { key: string }

export const fertilizerAttemptKey = (userId: string) => `farm-friends:fertilizer-attempt:${userId}`

export function parseFertilizerAttempt(raw: string | null): FertilizerAttempt | null {
  if (!raw) return null
  try {
    const value: unknown = JSON.parse(raw)
    if (!value || typeof value !== 'object') return null
    const attempt = value as Partial<FertilizerAttempt>
    if (typeof attempt.key !== 'string' || !/^[0-9a-f-]{36}$/i.test(attempt.key)) return null
    if (attempt.action === 'buy' && Number.isInteger(attempt.quantity) && attempt.quantity! > 0) return attempt as FertilizerAttempt
    if (attempt.action === 'fertilize' && typeof attempt.farmId === 'string' && typeof attempt.plotId === 'string'
      && Number.isInteger(attempt.expectedHarvestCycle) && typeof attempt.expectedCycleStartedAt === 'string') return attempt as FertilizerAttempt
    return null
  } catch { return null }
}

export function sellableAnimalProducts(items: InventoryItem[]) {
  return items.filter(item => item.itemType === 'AnimalProduct'
    && item.itemId !== FERTILIZER_ITEM_ID && item.quantity > 0)
}

export function fertilizerStock(items: InventoryItem[] | undefined) {
  return items?.find(item => item.itemType === 'AnimalProduct'
    && item.itemId === FERTILIZER_ITEM_ID)?.quantity ?? 0
}
