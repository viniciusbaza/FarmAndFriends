import { useCallback, useEffect, useRef, useState } from 'react'
import { authFetch } from '../api/http'
import { getSessionScope } from '../api/sessionScope'
import { useAuth } from '../auth/useAuth'
import { AuthoritativeReadGate } from '../user/authoritativeRead'
import type { Inventory } from '../types/Inventory'
import { InventoryContext } from './InventoryContext'

export function InventoryProvider({ children }: { children: React.ReactNode }) {
  const { token } = useAuth()
  const [snapshot, setSnapshot] = useState<{ scope: number; inventory: Inventory } | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [economyPending, setEconomyPending] = useState(false)
  const gate = useRef(new AuthoritativeReadGate())
  const mounted = useRef(true)

  const refreshInventory = useCallback(async () => {
    const scope = getSessionScope()
    const ticket = gate.current.begin(scope)
    if (!token) { setSnapshot(null); setLoading(false); return }
    const current = () => mounted.current && gate.current.accepts(ticket, getSessionScope())
    try {
      const inventory = await authFetch<Inventory>('/inventory')
      if (!current()) return
      setSnapshot({ scope, inventory })
      setEconomyPending(false)
      setError(null)
      return inventory
    } catch {
      if (!current()) return
      setEconomyPending(true)
      setError('Seu saldo e inventário estão sendo sincronizados. Tentaremos novamente.')
    } finally {
      if (current()) setLoading(false)
    }
  }, [token])

  useEffect(() => {
    mounted.current = true
    const currentGate = gate.current
    void refreshInventory()
    return () => { mounted.current = false; currentGate.invalidate() }
  }, [refreshInventory])
  useEffect(() => {
    const confirmed = () => {
      gate.current.invalidate()
      setEconomyPending(true)
      void refreshInventory()
    }
    const refresh = () => { void refreshInventory() }
    window.addEventListener('economy:confirmed', confirmed)
    window.addEventListener('inventory:changed', confirmed)
    window.addEventListener('focus', refresh)
    const timer = window.setInterval(refresh, economyPending ? 5_000 : 30_000)
    return () => {
      window.removeEventListener('economy:confirmed', confirmed)
      window.removeEventListener('inventory:changed', confirmed)
      window.removeEventListener('focus', refresh)
      window.clearInterval(timer)
    }
  }, [refreshInventory, economyPending])
  return <InventoryContext.Provider value={{
    inventory: token && snapshot?.scope === getSessionScope() ? snapshot.inventory : null,
    loading, economyPending, error, refreshInventory,
  }}>{children}</InventoryContext.Provider>
}
