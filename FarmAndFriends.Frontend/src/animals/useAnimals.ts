import { useContext } from 'react'
import { AnimalContext } from './AnimalContext'
export function useAnimals() {
  const context = useContext(AnimalContext)
  if (!context) throw new Error('useAnimals requires AnimalProvider')
  return context
}
