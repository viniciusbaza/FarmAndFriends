import type { AnimalStateEnvelope } from '../types/animal'
import type { Farm } from '../types/Farm'

export function acceptAnimalState(current: AnimalStateEnvelope | null | undefined, incoming: AnimalStateEnvelope) {
  if (!current) return true
  if (current.farmId !== incoming.farmId) return false
  if (incoming.revision !== current.revision) return incoming.revision > current.revision
  return Date.parse(incoming.observedAt) >= Date.parse(current.observedAt)
}

/** Return identity unchanged for another farm: its response must not invalidate the active GET. */
export function patchAnimalFarm(farm: Farm | null, incoming: AnimalStateEnvelope): Farm | null {
  if (!farm || farm.id !== incoming.farmId || !acceptAnimalState(farm.animalState, incoming)) return farm
  return { ...farm, animalState: incoming }
}

export function shouldRefreshAnimalFarm(activeFarmId: string | undefined, commandFarmId: string) {
  return activeFarmId === commandFarmId
}

/** Deadlines request server state; they never grant readiness or permission. */
export function nextAnimalStateAt(state: AnimalStateEnvelope | null | undefined) {
  const deadlines = (state?.enclosure?.animals ?? []).flatMap(animal => [
    animal.nextStateChangeAt,
    // Compatibility with an older backend during a mixed rollout or rollback.
    animal.permissions.nextRewardAt,
  ])
    .filter((value): value is string => value !== null)
    .map(Date.parse).filter(Number.isFinite)
  return deadlines.length ? Math.min(...deadlines) : null
}
