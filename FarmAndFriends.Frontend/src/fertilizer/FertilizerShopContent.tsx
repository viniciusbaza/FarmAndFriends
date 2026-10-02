import { useEffect, useState } from 'react'
import { getSessionScope } from '../api/sessionScope'
import { useFarm } from '../farm/useFarmContext'
import { useInventory } from '../inventory/useInventory'
import { MAX_SHOP_QUANTITY } from '../shop/shopLimits'
import { useUser } from '../user/useUser'
import { getFertilizerOffer, type FertilizerOffer } from './fertilizerApi'
import { useFertilizerAttempt } from './useFertilizerAttempt'
import { fertilizerStock } from './fertilizerState'
import { fertilizerEffectDescription, refreshFertilizerState } from './fertilizerFeedback'

type Props = { onSuccess: (message: string) => void; onError: (message: string) => void }
const numberFormatter = new Intl.NumberFormat('pt-BR')

export function FertilizerShopContent({ onSuccess, onError }: Props) {
  const { user } = useUser()
  const { inventory, economyPending, refreshInventory } = useInventory()
  const { refreshFarm } = useFarm()
  const { busy, pending, execute, recover } = useFertilizerAttempt(user?.id)
  const [offer, setOffer] = useState<FertilizerOffer | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)
  const [quantity, setQuantity] = useState(1)
  const [syncPending, setSyncPending] = useState(false)
  const [syncing, setSyncing] = useState(false)

  async function loadOffer() {
    const scope = getSessionScope()
    setLoading(true)
    try {
      const result = await getFertilizerOffer()
      if (scope !== getSessionScope()) return
      setOffer(result)
      setLoadError(false)
    } catch {
      if (scope === getSessionScope()) setLoadError(true)
    } finally {
      if (scope === getSessionScope()) setLoading(false)
    }
  }

  useEffect(() => { void loadOffer() }, [])

  async function buy() {
    if (!offer || busy || pending || economyPending) return
    try {
      const result = await execute({ action: 'buy', quantity })
      if (!result || !('quantity' in result)) return
      window.dispatchEvent(new Event('inventory:changed'))
      const refreshed = await refreshInventory()
      setSyncPending(!refreshed)
      setQuantity(1)
      onSuccess(!refreshed ? 'Compra de adubo confirmada. Seu estoque ainda está sendo atualizado.'
        : result.replayed ? 'Compra de adubo recuperada. Seu estoque foi atualizado.'
        : `🌿 Você comprou ${result.quantity} ${result.quantity === 1 ? 'adubo' : 'adubos'} com notas.`)
    } catch (error) {
      onError(error instanceof Error ? error.message : 'Não foi possível comprar adubo. Confira o resultado antes de tentar novamente.')
      await refreshInventory()
    }
  }

  async function recoverPending() {
    try {
      const result = await recover()
      if (!result) return
      window.dispatchEvent(new Event('inventory:changed'))
      const refreshed = await refreshFertilizerState(refreshFarm, refreshInventory)
      const synchronized = refreshed.farmUpdated && refreshed.inventoryUpdated
      setSyncPending(!synchronized)
      onSuccess(!synchronized ? 'Ação de adubo confirmada. A plantação e o estoque ainda precisam ser atualizados.'
        : result.replayed ? 'Ação de adubo recuperada e estoque atualizado.' : 'Ação de adubo confirmada e estoque atualizado.')
    } catch (error) {
      onError(error instanceof Error ? error.message : 'Não foi possível recuperar a ação de adubo.')
    }
  }

  async function retrySync() {
    if (busy || syncing) return
    setSyncing(true)
    try {
      const refreshed = await refreshFertilizerState(refreshFarm, refreshInventory)
      const synchronized = refreshed.farmUpdated && refreshed.inventoryUpdated
      setSyncPending(!synchronized)
      if (synchronized) onSuccess('Plantação e estoque atualizados.')
      else onError('Ainda não foi possível atualizar a plantação e o estoque. Tente novamente.')
    } finally {
      setSyncing(false)
    }
  }

  const total = offer ? offer.pricePremiumCoins * quantity : 0
  const available = inventory?.premiumCoins ?? null
  const stock = fertilizerStock(inventory?.items)

  return <article aria-label="Comprar adubo com notas" className="flex min-w-0 flex-col rounded-xl border border-transparent bg-white p-3 shadow-sm transition hover:border-emerald-200">
    <div className="flex items-start gap-3">
      <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-2xl" aria-hidden="true">🌿</span>
      <div className="min-w-0 flex-1">
        <div className="flex items-start justify-between gap-2">
          <h3 className="font-semibold text-emerald-950">{offer?.displayName ?? 'Adubo'}</h3>
          <span className="shrink-0 rounded-full bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-800">Em estoque: {stock}</span>
        </div>
        <p className="mt-1 text-xs leading-relaxed text-slate-600">Acelera uma plantação em crescimento. Cada ciclo aceita uma aplicação; a produção pronta não aceita adubo. O porco também produz adubo.</p>
        {offer && <p className="mt-1 text-sm font-semibold text-amber-700">💵 {numberFormatter.format(offer.pricePremiumCoins)} {offer.pricePremiumCoins === 1 ? 'nota' : 'notas'} por unidade</p>}
      </div>
    </div>
    {loading && <p role="status" className="mt-2 text-sm">Carregando preço do adubo…</p>}
    {loadError && <p role="alert" className="mt-2 text-sm text-red-700">Não foi possível carregar o preço. <button type="button" className="underline" onClick={() => void loadOffer()}>Tentar novamente</button></p>}
    {offer && <>
      <p className="mt-3 rounded-lg bg-emerald-50 px-3 py-2 text-xs leading-relaxed text-emerald-900">{fertilizerEffectDescription(offer)}</p>
      <div className="mt-auto flex items-end gap-2 pt-4">
        <label className="min-w-0 flex-1 text-xs font-semibold text-emerald-700" htmlFor="fertilizer-buy-quantity">
          Quantidade
          <input id="fertilizer-buy-quantity" type="number" min={1} max={MAX_SHOP_QUANTITY} step={1} value={quantity}
            onChange={event => { const value = Number(event.target.value); setQuantity(Number.isFinite(value) ? Math.max(1, Math.min(MAX_SHOP_QUANTITY, Math.floor(value))) : 1) }}
            disabled={busy || !!pending}
            className="mt-1 w-full rounded-lg border border-emerald-300 bg-white px-3 py-2 text-base text-emerald-950 outline-none transition focus:border-emerald-600 focus:ring-2 focus:ring-emerald-200 disabled:opacity-60" />
        </label>
        <div className="pb-2 text-right text-xs text-emerald-600">
          <span className="block">Total</span>
          <strong className="text-sm text-amber-800">{numberFormatter.format(total)} 💵</strong>
        </div>
      </div>
      <button type="button" onClick={() => void buy()}
        disabled={busy || !!pending || economyPending || available === null || available < total}
        className="mt-3 min-h-11 rounded-lg bg-emerald-600 px-3 py-2 text-sm font-semibold text-white transition hover:bg-emerald-700 disabled:cursor-not-allowed disabled:bg-slate-300 disabled:text-slate-600">
        {busy ? 'Comprando…' : available !== null && available < total ? 'Notas insuficientes' : `Comprar · ${numberFormatter.format(total)} 💵`}
      </button>
    </>}
    {pending && <div role="status" className="mt-3 rounded-lg border border-amber-300 bg-amber-50 p-2 text-sm text-amber-900">
      A resposta da última ação de adubo ainda precisa ser confirmada.
      <button type="button" disabled={busy} onClick={() => void recoverPending()}
        className="mt-2 block min-h-10 w-full rounded-lg bg-amber-700 px-3 font-semibold text-white disabled:opacity-50">Recuperar resultado</button>
    </div>}
    {syncPending && <button type="button" disabled={busy || syncing} onClick={() => void retrySync()}
      className="mt-3 min-h-10 w-full rounded-lg border border-amber-300 bg-amber-50 px-3 text-sm font-semibold text-amber-900 disabled:opacity-50">
      {syncing ? 'Atualizando…' : 'Atualizar plantação e estoque'}
    </button>}
  </article>
}
