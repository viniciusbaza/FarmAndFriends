import { createContext } from 'react'
import type { Inventory } from '../types/Inventory'
export const InventoryContext = createContext<{
  inventory: Inventory | null
  loading: boolean
  economyPending: boolean
  error: string | null
  refreshInventory: () => Promise<Inventory | undefined>
} | null>(null)
