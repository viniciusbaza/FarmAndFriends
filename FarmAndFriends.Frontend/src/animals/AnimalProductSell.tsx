import { useState } from 'react'
import { getSessionScope } from '../api/sessionScope'
import { useInventory } from '../inventory/useInventory'
import { MAX_SHOP_QUANTITY } from '../shop/shopLimits'
import {
  getDefaultSellQuantity,
  getSellActionLabel,
  isSellQuantityLimited,
  parseSellQuantity,
} from '../shop/sellQuantity'
import { useUser } from '../user/useUser'
import { useAnimals } from './useAnimals'
import { productPresentation } from './animalPresentation'
import { sellableAnimalProducts } from '../fertilizer/fertilizerState'

const numberFormatter = new Intl.NumberFormat('pt-BR')

export function AnimalProductSell() {
  const { inventory, loading, economyPending } = useInventory()
  const { user } = useUser()
  const { catalog, busy, pending, execute, catalogError, refreshCatalog } = useAnimals()
  const [quantities, setQuantities] = useState<Record<string, { value: string; available: number }>>({})
  const products = sellableAnimalProducts(inventory?.items ?? [])

  async function handleSell(itemId: string, quantity: number) {
    if (!user || busy || pending || economyPending) return
    const scope = getSessionScope()
    const result = await execute({ action: 'sell-animal-product', farmId: user.farmId, itemId, quantity })
    if (result && scope === getSessionScope()) {
      setQuantities(current => {
        const next = { ...current }
        delete next[itemId]
        return next
      })
    }
  }

  if (loading || !products.length) return null

  return (
    <>
      {catalogError && (
        <p role="alert" className="col-span-full text-sm text-red-700">
          {catalogError}{' '}
          <button type="button" onClick={() => void refreshCatalog()} className="underline">
            Tentar novamente
          </button>
        </p>
      )}
        {products.map(product => {
          const offer = catalog?.products.find(offer => offer.itemId === product.itemId)
          const presentation = productPresentation(product.itemId)
          const name = offer?.displayName ?? presentation.name
          const override = quantities[product.itemId]
          const inputValue = override?.available === product.quantity
            ? override.value : String(getDefaultSellQuantity(product.quantity))
          const quantity = parseSellQuantity(inputValue, product.quantity)
          const totalCoins = (offer?.sellPriceCoins ?? 0) * (quantity ?? 0)
          const isBusy = busy && pending?.action === 'sell-animal-product' && pending.itemId === product.itemId
          const blocked = busy || !!pending || economyPending
          const quantityLimited = isSellQuantityLimited(product.quantity)
          const actionLabel = getSellActionLabel(quantity, product.quantity)

          return (
            <article
              key={product.itemId}
              className="flex flex-col rounded-xl border border-transparent bg-white p-3 shadow-sm transition hover:border-amber-200"
            >
              <div className="flex items-start gap-3">
                <span
                  className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-amber-100 text-2xl"
                  aria-hidden="true"
                >
                  {presentation.icon}
                </span>
                <div className="min-w-0 flex-1">
                  <div className="flex items-start justify-between gap-2">
                    <h3 className="truncate font-semibold">{name}</h3>
                    <span className="shrink-0 rounded-full bg-amber-100 px-2 py-1 text-xs font-bold text-amber-900">
                      ×{numberFormatter.format(product.quantity)}
                    </span>
                  </div>
                  <p className="mt-1 text-sm font-semibold text-amber-700">
                    {offer ? `🪙 ${numberFormatter.format(offer.sellPriceCoins)} por unidade` : 'Carregando preço…'}
                  </p>
                </div>
              </div>

              <div className="mt-4 flex items-end gap-2">
                <label className="min-w-0 flex-1 text-xs font-semibold text-emerald-700">
                  Quantidade
                  <input
                    type="number"
                    min={1}
                    max={Math.min(product.quantity, MAX_SHOP_QUANTITY)}
                    step={1}
                    value={inputValue}
                    onChange={event => setQuantities(current => ({
                      ...current,
                      [product.itemId]: { value: event.target.value, available: product.quantity },
                    }))}
                    disabled={blocked}
                    aria-invalid={quantity === null}
                    aria-label={`Quantidade de ${name} para vender`}
                    className="mt-1 w-full rounded-lg border border-emerald-300 bg-white px-3 py-2 text-base text-emerald-950 outline-none transition focus:border-emerald-600 focus:ring-2 focus:ring-emerald-200 disabled:opacity-60"
                  />
                </label>
                <div className="pb-2 text-right text-xs text-emerald-600">
                  <span className="block">Você recebe</span>
                  <strong className="text-sm text-amber-800">
                    {offer ? `${numberFormatter.format(totalCoins)} 🪙` : '—'}
                  </strong>
                </div>
              </div>

              {quantity === null && (
                <p className="mt-1 text-xs font-semibold text-red-700">
                  Informe uma quantidade entre 1 e {numberFormatter.format(getDefaultSellQuantity(product.quantity))}.
                </p>
              )}

              {quantityLimited && (
                <p className="mt-2 rounded-lg bg-amber-50 px-2 py-1.5 text-xs text-amber-800">
                  Limite de {numberFormatter.format(MAX_SHOP_QUANTITY)} unidades por venda.
                  O restante continuará no estoque.
                </p>
              )}

              <button
                type="button"
                onClick={() => {
                  if (quantity !== null) void handleSell(product.itemId, quantity)
                }}
                disabled={!user || !offer || quantity === null || blocked}
                className="mt-3 rounded-lg bg-amber-500 px-3 py-2 text-sm font-semibold text-amber-950 transition hover:bg-amber-400 disabled:cursor-not-allowed disabled:bg-slate-300 disabled:text-slate-600"
              >
                {isBusy ? 'Vendendo...'
                  : economyPending ? 'Atualizando estoque...'
                    : `${actionLabel} · ${offer ? `${numberFormatter.format(totalCoins)} 🪙` : '—'}`}
              </button>
            </article>
          )
        })}
    </>
  )
}
