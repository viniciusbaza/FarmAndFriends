import { productPresentation } from '../animals/animalPresentation.ts'

type CycleVisualState = { id: string; isReady: boolean; ownerCollectible: number; helperCount: number }
type AnimalVisualState = { animalTypeId?: string; stage: 'chick' | 'calf' | 'lamb' | 'piglet' | 'adult'; cycle: CycleVisualState | null }

/** No local readiness calculation: only the accepted server projection. */
export function resolveAnimalVisual(animal: AnimalVisualState) {
  const sheep = animal.animalTypeId === 'sheep'
  const pig = animal.animalTypeId === 'pig'
  return {
    age: animal.stage,
    nest: sheep || pig ? null : animal.cycle?.isReady ? 'nest-eggs' : 'nest-empty',
    coat: sheep && animal.stage === 'adult' && animal.cycle?.isReady ? 'full' : 'shorn',
    productFrame: sheep || pig ? null : animal.animalTypeId === 'cow'
      ? animal.cycle?.isReady ? 'milk-full' : 'milk-empty'
      : animal.cycle?.isReady ? 'nest-eggs' : 'nest-empty',
    readyIndicator: pig && animal.stage === 'adult' && animal.cycle?.isReady === true,
    label: animal.stage !== 'adult' ? 'Crescendo' : animal.cycle?.isReady
      ? pig ? 'Adubo pronto' : sheep ? 'Lã pronta' : animal.animalTypeId === 'cow' ? 'Leite pronto' : 'Ovos prontos' : 'Produzindo',
    helpers: animal.cycle?.helperCount ?? 0,
  }
}

/** Slower, independent presentation patrol inside the cow's reserved sector. */
export function resolveCowMotion(time: number, reduceMotion: boolean) {
  if (reduceMotion || !Number.isFinite(time)) return { pose: 'idle', offset: 0, flipX: false }
  const phase = (((time % 14_000) + 14_000) % 14_000) / 1_000
  if (phase < 3 || phase >= 10) return { pose: 'idle', offset: 0, flipX: false }
  if (phase >= 7) return { pose: 'graze', offset: 0, flipX: false }
  const progress = (phase - 3) / 4
  return { pose: Math.floor(time / 320) % 2 ? 'walk-a' : 'walk-b',
    offset: Math.sin(progress * Math.PI) * 5, flipX: progress >= 0.5 }
}

/** A short patrol and grazing pose, independent from the other species. */
export function resolveSheepMotion(time: number, reduceMotion: boolean) {
  if (reduceMotion || !Number.isFinite(time)) return { pose: 'idle', offset: 0, flipX: false }
  const phase = (((time % 16_000) + 16_000) % 16_000) / 1_000
  if (phase < 4 || phase >= 11) return { pose: 'idle', offset: 0, flipX: false }
  if (phase >= 8) return { pose: 'graze', offset: 0, flipX: false }
  const progress = (phase - 4) / 4
  return { pose: Math.floor(time / 350) % 2 ? 'walk-a' : 'walk-b',
    offset: Math.sin(progress * Math.PI) * 4, flipX: progress >= 0.5 }
}

export type PigMotion = {
  pose: 'idle' | 'walk-a' | 'walk-b'
  offset: number
  flipX: boolean
}

/** A short patrol with alternating hoof poses in the fourth sector. */
export function resolvePigMotion(time: number, reduceMotion: boolean): PigMotion {
  if (reduceMotion || !Number.isFinite(time)) return { pose: 'idle', offset: 0, flipX: false }
  const phase = (((time % 18_000) + 18_000) % 18_000) / 1_000
  if (phase < 5 || phase >= 9) return { pose: 'idle', offset: 0, flipX: false }
  const progress = (phase - 5) / 4
  return { pose: Math.floor(time / 320) % 2 ? 'walk-a' : 'walk-b',
    offset: Math.sin(progress * Math.PI) * 3, flipX: progress >= 0.5 }
}

/** Walking keeps the young silhouette or the accepted ready indication. */
export function resolvePigFrame(animal: AnimalVisualState, pose: PigMotion['pose']) {
  if (animal.stage === 'piglet') return `piglet-${pose}`
  return `${resolveAnimalVisual(animal).readyIndicator ? 'adult-ready' : 'adult'}-${pose}`
}

export function animalProductCaption(productId: string | null, amount?: number) {
  if (amount === undefined) {
    return productId === 'egg' || productId === 'milk' || productId === 'wool' || productId === 'fertilizer'
      ? productPresentation(productId).actionName
      : 'produtos'
  }
  if (productId === 'egg') return amount === 1 ? 'ovo' : 'ovos'
  if (productId === 'fertilizer') return amount === 1 ? 'adubo' : 'adubos'
  if (productId === 'milk') return amount === 1 ? 'leite' : 'leites'
  if (productId === 'wool') return amount === 1 ? 'lã' : 'lãs'
  return amount === 1 ? 'produto' : 'produtos'
}

export function animalActionCaption(
  action: 'collect' | 'help' | 'steal',
  productId: string | null,
  amount: number,
) {
  if (action === 'help') return ''

  const product = animalProductCaption(productId, amount)
  const actionLabel = action === 'steal' ? '😈' : '🧺'
  return `+${amount} ${product} ${actionLabel}`
}

const MIN_ANIMAL_SCREEN_SCALE = 0.9
const MAX_ANIMAL_SEMANTIC_SCALE = 1.4

/**
 * Presentation-only compensation for overview cameras. It keeps the animal
 * readable on narrow screens without changing its world position or hit area.
 */
export function resolveAnimalSemanticScale(cameraZoom: number) {
  if (!Number.isFinite(cameraZoom) || cameraZoom <= 0) return 1

  return Math.min(
    MAX_ANIMAL_SEMANTIC_SCALE,
    Math.max(1, MIN_ANIMAL_SCREEN_SCALE / cameraZoom),
  )
}

export type ChickenMotion = {
  pose: 'idle' | 'walk-a' | 'walk-b' | 'peck'
  offset: number
  flipX: boolean
}

/** One visual patrol: left-to-right, right-to-left, then normal-facing idle. */
export function resolveChickenMotion(
  time: number,
  reduceMotion: boolean,
): ChickenMotion {
  if (reduceMotion || !Number.isFinite(time)) {
    return { pose: 'idle', offset: 0, flipX: false }
  }

  const phase = (((time % 9_000) + 9_000) % 9_000) / 1_000
  if (phase < 2 || phase >= 7) {
    return { pose: 'idle', offset: 0, flipX: false }
  }
  if (phase >= 5) {
    return { pose: 'peck', offset: 0, flipX: false }
  }

  const progress = (phase - 2) / 3
  return {
    pose: Math.floor(time / 220) % 2 ? 'walk-a' : 'walk-b',
    offset: Math.sin(progress * Math.PI) * 10,
    flipX: progress >= 0.5,
  }
}

export type AnimalFeedbackIdentity = {
  operationId: string
  farmId: string
  animalId: string
  cycleId: string
}

/** Collection refers to the cycle just replaced by the accepted snapshot. */
export function matchesAnimalFeedback(
  effect: AnimalFeedbackIdentity,
  farmId: string,
  currentAnimalId: string | undefined,
  currentCycleId: string | undefined,
  collectedCycleId: string | undefined,
  action: 'collect' | 'help' | 'steal',
) {
  return effect.farmId === farmId && effect.animalId === currentAnimalId &&
    effect.cycleId === (action === 'collect' ? collectedCycleId : currentCycleId)
}
