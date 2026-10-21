import { authFetch } from '../api/http.ts'
import type { AnimalAttempt } from './animalAttempt'
import type { AnimalEnclosureOffer, AnimalOffer, AnimalProductOffer, AnimalOperationResponse, AnimalHelpersPage } from '../types/animal'

export async function getAnimalCatalog() {
  const [enclosure, animals, products] = await Promise.all([
    authFetch<AnimalEnclosureOffer>('/shop/animal-enclosure'),
    authFetch<AnimalOffer[]>('/shop/animals'),
    authFetch<AnimalProductOffer[]>('/shop/animal-products'),
  ])
  return { enclosure, animals, products }
}

export async function sendAnimalAttempt(attempt: AnimalAttempt) {
  let path: string
  let body: object
  switch (attempt.action) {
    case 'buy-animal-enclosure': path = '/shop/buy-animal-enclosure'; body = {}; break
    case 'buy-animal': path = '/shop/buy-animal'; body = { animalTypeId: attempt.animalTypeId }; break
    case 'sell-animal-product': path = '/shop/sell-animal-product'; body = { itemId: attempt.itemId, quantity: attempt.quantity }; break
    default:
      path = `/farms/${encodeURIComponent(attempt.farmId)}/animals/${encodeURIComponent(attempt.animalId!)}/${attempt.action}`
      body = { expectedCycleId: attempt.expectedCycleId }
  }
  const abort = new AbortController()
  const timeout = setTimeout(() => abort.abort(), 20_000)
  try {
    return await authFetch<AnimalOperationResponse>(path, {
      method: 'POST', headers: { 'Idempotency-Key': attempt.key }, body: JSON.stringify(body), signal: abort.signal,
    })
  } finally { clearTimeout(timeout) }
}

export function getAnimalHelpers(farmId: string, animalId: string, cycleId: string, cursor: string) {
  return authFetch<AnimalHelpersPage>(`/farms/${encodeURIComponent(farmId)}/animals/${encodeURIComponent(animalId)}/cycles/${encodeURIComponent(cycleId)}/helpers?take=20&cursor=${encodeURIComponent(cursor)}`)
}
