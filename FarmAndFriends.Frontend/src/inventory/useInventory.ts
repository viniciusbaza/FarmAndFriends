import { useContext } from 'react'
import { InventoryContext } from './InventoryContext'

export function useInventory() {
  const value = useContext(InventoryContext)
  if (!value) throw new Error('useInventory requires InventoryProvider')
  return value
}
