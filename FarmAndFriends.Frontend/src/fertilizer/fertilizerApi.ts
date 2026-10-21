import { authFetch } from '../api/http'
import { type FertilizerAttempt } from './fertilizerState'

export type FertilizerOffer = {
  itemId: string
  displayName: string
  pricePremiumCoins: number
  maxReductionSeconds: number
  minRemainingSeconds: number
}

export type BuyFertilizerResponse = {
  operationId: string
  replayed: boolean
  itemId: string
  quantity: number
  remainingQuantity: number
  premiumCoins: number
}

export type FertilizeResponse = {
  operationId: string
  replayed: boolean
  previousReadyAt: string
  readyAt: string
  remainingQuantity: number
}

export function getFertilizerOffer() {
  return authFetch<FertilizerOffer>('/shop/fertilizer')
}

export function sendFertilizerAttempt(attempt: FertilizerAttempt): Promise<BuyFertilizerResponse | FertilizeResponse> {
  return attempt.action === 'buy'
    ? authFetch<BuyFertilizerResponse>('/shop/buy-fertilizer', {
      method: 'POST', headers: { 'Idempotency-Key': attempt.key }, body: JSON.stringify({ quantity: attempt.quantity }),
    })
    : authFetch<FertilizeResponse>(`/farms/${encodeURIComponent(attempt.farmId)}/plots/${encodeURIComponent(attempt.plotId)}/fertilize`, {
      method: 'POST', headers: { 'Idempotency-Key': attempt.key },
      body: JSON.stringify({ expectedHarvestCycle: attempt.expectedHarvestCycle, expectedCycleStartedAt: attempt.expectedCycleStartedAt }),
    })
}
