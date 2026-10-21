import { createContext } from 'react'
import type { AnimalEnclosureOffer, AnimalOffer, AnimalProductOffer, AnimalOperationResponse } from '../types/animal'
import type { AnimalAttempt, AnimalIntent } from './animalAttempt'

export type AnimalFeedback = { type: 'success' | 'error' | 'pending'; message: string }
export type AnimalModalAnchor = { x: number; y: number }
export const AnimalContext = createContext<{
  catalog: { enclosure: AnimalEnclosureOffer; animals: AnimalOffer[]; products: AnimalProductOffer[] } | null
  catalogLoading: boolean; catalogError: string | null; refreshCatalog: () => Promise<void>
  selected: { farmId: string; animalId: string; anchor: AnimalModalAnchor | null } | null
  open: (animalId: string, anchor?: AnimalModalAnchor | null) => void; close: () => void
  busy: boolean; pending: AnimalAttempt | null; feedback: AnimalFeedback | null
  execute: (intent: AnimalIntent) => Promise<AnimalOperationResponse | null>
  recover: () => Promise<AnimalOperationResponse | null>
} | null>(null)
