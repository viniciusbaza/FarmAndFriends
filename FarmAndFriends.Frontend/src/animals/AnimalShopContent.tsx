import { useAnimals } from './useAnimals'
import { useUser } from '../user/useUser'
import { animalReason } from './animalMessages'
import { animalAcquiredLabel, animalPresentation } from './animalPresentation'

const numberFormatter = new Intl.NumberFormat('pt-BR')

export function AnimalShopContent() {
  const { catalog, catalogLoading, catalogError, refreshCatalog, busy, pending, execute } = useAnimals()
  const { user } = useUser()
  if (catalogLoading) return <p role="status" className="p-4">Carregando cercado e animais…</p>
  if (catalogError || !catalog) return <div role="alert" className="p-4">
    <p>{catalogError ?? 'O catálogo ainda não está disponível.'}</p>
    <button type="button" onClick={() => void refreshCatalog()} className="mt-2 min-h-11 rounded-lg bg-emerald-700 px-4 text-white">Tentar novamente</button>
  </div>
  return <div className="grid gap-2 md:grid-cols-2">
    <article className={`flex min-w-0 flex-col rounded-xl border bg-white p-3 shadow-sm transition ${catalog.enclosure.currentLevel < catalog.enclosure.minLevel ? 'border-slate-200 opacity-75' : 'border-transparent hover:border-emerald-200'}`}>
      <div className="flex items-start gap-3">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-2xl" aria-hidden="true">🌿</span>
        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-2">
            <h3 className="font-semibold text-emerald-950">Cercado de animais</h3>
            <span className="shrink-0 rounded-full bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-800">Nível {catalog.enclosure.minLevel}</span>
          </div>
          <p className="mt-1 text-xs leading-relaxed text-slate-600">Um espaço aberto na sua fazenda para receber seus animais.</p>
          <p className="mt-1 text-sm font-semibold text-amber-700">🪙 {numberFormatter.format(catalog.enclosure.priceCoins)} moedas</p>
        </div>
      </div>
      <dl className="mt-3 rounded-lg bg-emerald-50 px-3 py-2 text-xs">
        <dt className="text-emerald-600">Primeira expansão</dt>
        <dd className="font-semibold text-emerald-900">{catalog.enclosure.initialPlotsUnlocked}/{catalog.enclosure.requiredInitialPlots} lotes iniciais</dd>
      </dl>
      {!catalog.enclosure.canBuy && !catalog.enclosure.owned && <p className="mt-2 text-sm">{animalReason(catalog.enclosure.reason)}</p>}
      <div className="mt-auto pt-3">
        <button type="button" disabled={!catalog.enclosure.canBuy || busy || !!pending || !user}
          onClick={() => user && void execute({ action: 'buy-animal-enclosure', farmId: user.farmId })}
          className="min-h-11 w-full rounded-lg bg-emerald-600 px-3 py-2 text-sm font-semibold text-white transition hover:bg-emerald-700 disabled:cursor-not-allowed disabled:bg-slate-300 disabled:text-slate-600">
          {catalog.enclosure.owned ? 'Cercado adquirido' : busy ? 'Aguarde…' : 'Comprar cercado'}
        </button>
      </div>
    </article>
    {catalog.animals.map(animal => <article key={animal.animalTypeId}
      className={`flex min-w-0 flex-col rounded-xl border bg-white p-3 shadow-sm transition ${catalog.enclosure.currentLevel < animal.minLevel ? 'border-slate-200 opacity-75' : 'border-transparent hover:border-emerald-200'}`}>
      <div className="flex items-start gap-3">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-2xl" aria-hidden="true">
          {animalPresentation(animal.animalTypeId).youngIcon}
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-2">
            <h3 className="font-semibold text-emerald-950">{animal.displayName}</h3>
            <span className="shrink-0 rounded-full bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-800">Nível {animal.minLevel}</span>
          </div>
          <p className="mt-1 text-xs leading-relaxed text-slate-600">Chega filhote, cresce uma vez e permanece na fazenda. Um animal de cada espécie por fazenda.</p>
          <p className="mt-1 text-sm font-semibold text-amber-700">🪙 {numberFormatter.format(animal.priceCoins)} moedas</p>
        </div>
      </div>
      <dl className="mt-3 grid grid-cols-2 gap-x-3 rounded-lg bg-emerald-50 px-3 py-2 text-xs">
        <div>
          <dt className="text-emerald-600">Crescimento</dt>
          <dd className="font-semibold text-emerald-900">{duration(animal.growthDuration)}</dd>
        </div>
        <div>
          <dt className="text-emerald-600">Produção por ciclo</dt>
          <dd className="font-semibold text-emerald-900">{duration(animal.productionDuration)}</dd>
        </div>
      </dl>
      {animal.animalTypeId === 'pig' && <p className="mt-2 text-xs leading-relaxed text-emerald-800">Produz adubo para acelerar plantações próprias ou de amigos. O adubo não é vendido por moedas.</p>}
      {!animal.canBuy && !animal.owned && <p className="mt-2 text-sm">{animalReason(animal.reason)}</p>}
      <div className="mt-auto pt-3">
        <button type="button" disabled={!animal.canBuy || busy || !!pending || !user}
          onClick={() => user && void execute({ action: 'buy-animal', farmId: user.farmId, animalTypeId: animal.animalTypeId })}
          className="min-h-11 w-full rounded-lg bg-emerald-600 px-3 py-2 text-sm font-semibold text-white transition hover:bg-emerald-700 disabled:cursor-not-allowed disabled:bg-slate-300 disabled:text-slate-600">
          {animal.owned ? `${animal.displayName} ${animalAcquiredLabel(animal.animalTypeId)}` : busy ? 'Aguarde…' : animalPresentation(animal.animalTypeId).buyLabel}
        </button>
      </div>
    </article>)}
  </div>
}

function duration(seconds: number) {
  return seconds >= 3600 ? `${Math.ceil(seconds / 3600)}h` : seconds >= 60 ? `${Math.ceil(seconds / 60)}min` : `${seconds}s`
}
