import type { AnimalAction } from '../types/animal'

export type AnimalIntent = {
  action: AnimalAction; farmId: string; animalId?: string; expectedCycleId?: string
  animalTypeId?: string; itemId?: string; quantity?: number
}
export type AnimalAttempt = AnimalIntent & { key: string }
const actions: AnimalAction[] = ['buy-animal-enclosure', 'buy-animal', 'collect', 'help', 'steal', 'sell-animal-product']
export const attemptStorageKey = (userId: string) => `farm-friends:animal-attempt:${userId}`

export function parseAnimalAttempt(raw: string | null): AnimalAttempt | null {
  if (!raw) return null
  try {
    const attempt = JSON.parse(raw) as Partial<AnimalAttempt>
    if (typeof attempt.key !== 'string' || !/^[0-9a-f-]{36}$/i.test(attempt.key) ||
        typeof attempt.farmId !== 'string' || !actions.includes(attempt.action as AnimalAction)) return null
    if (['collect', 'help', 'steal'].includes(attempt.action!) &&
        (typeof attempt.animalId !== 'string' || typeof attempt.expectedCycleId !== 'string')) return null
    if (attempt.action === 'buy-animal' && typeof attempt.animalTypeId !== 'string') return null
    if (attempt.action === 'sell-animal-product' && (typeof attempt.itemId !== 'string' ||
        !Number.isInteger(attempt.quantity) || attempt.quantity! <= 0)) return null
    return attempt as AnimalAttempt
  } catch { return null }
}

/** Immutable semantic request survives timeouts, remounts and reloads. */
export function prepareAnimalAttempt(pending: AnimalAttempt | null, intent: AnimalIntent, key: () => string) {
  if (pending) throw new Error('Recupere a ação pendente antes de iniciar outra.')
  return { ...intent, key: key() }
}
