import { useEffect, useRef, useState } from 'react'
import type { CSSProperties } from 'react'
import { getSessionScope } from '../api/sessionScope'
import { useFarm } from '../farm/useFarmContext'
import type { AnimalDto, AnimalHelperDto } from '../types/animal'
import { formatCountdown } from '../utils/time'
import { useAnimals } from './useAnimals'
import { AnimalFeedback } from './AnimalFeedback'
import { animalReason } from './animalMessages'
import { getAnimalHelpers } from './animalApi'
import { animalGrowthDeadline, animalPresentation, productPresentation, productQuantity } from './animalPresentation'

export function AnimalPanel({ maxHeight }: { maxHeight: number }) {
  const { farm, isVisiting } = useFarm()
  const { selected, close, busy, pending, feedback } = useAnimals()
  const dialog = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const prior = document.activeElement instanceof HTMLElement ? document.activeElement : null
    dialog.current?.querySelector<HTMLButtonElement>('button')?.focus()
    window.dispatchEvent(new CustomEvent('ui:modal', { detail: { source: 'animals', open: true } }))
    return () => {
      window.dispatchEvent(new CustomEvent('ui:modal', { detail: { source: 'animals', open: false } }))
      prior?.focus()
    }
  }, [])
  if (!farm || !selected) return null
  const animal = farm.animalState?.enclosure?.animals.find(animal => animal.id === selected.animalId)
  if (!animal) return null
  const presentation = animalPresentation(animal.animalTypeId)
  const title = animal.stage === 'adult' ? animal.displayName : presentation.youngName
  return <div ref={dialog} role="dialog" aria-modal="true" aria-labelledby="animal-panel-title"
      aria-busy={busy}
      className="animal-modal plot-modal w-72 max-w-[calc(100vw-2rem)]"
      style={{ '--animal-modal-max-height': `${maxHeight}px` } as CSSProperties}
      onPointerDown={event => event.stopPropagation()}
      onKeyDown={event => {
        if (event.key === 'Escape') { event.stopPropagation(); close() }
        if (event.key !== 'Tab') return
        const focusable = [...(dialog.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), summary, [tabindex="0"]') ?? [])]
          .filter(element => {
            const closedDetails = element.closest('details:not([open])')
            return !element.hidden && element.getClientRects().length > 0
              && (!closedDetails || element === closedDetails.querySelector('summary'))
          })
        if (!focusable.length) return
        const first = focusable[0], last = focusable[focusable.length - 1]
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
        if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
      }}>
      <header className="mb-2 flex items-start justify-between gap-3">
        <h2 id="animal-panel-title" className="font-bold text-emerald-900">Cercado de animais</h2>
        <button type="button" onClick={close} aria-label={`Fechar ${title.toLocaleLowerCase('pt-BR')}`}
          className="-mr-1 -mt-1 rounded-full px-2 py-1 text-lg leading-none text-slate-600 hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700">×</button>
      </header>
      <div className="space-y-2">
        {((feedback && feedback.type !== 'success') || (pending && !busy)) && <AnimalFeedback />}
        <AnimalCard key={`${animal.id}:${animal.cycle?.id ?? animal.stage}`} animal={animal} farmId={farm.id} isVisiting={isVisiting} />
      </div>
    </div>
}

function AnimalCard({ animal, farmId, isVisiting }: { animal: AnimalDto; farmId: string; isVisiting: boolean }) {
  const { execute, busy, pending } = useAnimals()
  const [now, setNow] = useState(Date.now)
  const cycle = animal.cycle
  const growing = animal.stage !== 'adult'
  const presentation = animalPresentation(animal.animalTypeId)
  const product = productPresentation(cycle?.productId ?? null)
  const [extraHelpers, setExtraHelpers] = useState<AnimalHelperDto[]>([])
  const [cursor, setCursor] = useState<string | null | undefined>(undefined)
  const [helpersBusy, setHelpersBusy] = useState(false)
  const [helpersError, setHelpersError] = useState<string | null>(null)
  const mounted = useRef(true)
  const helperBusy = useRef(false)
  useEffect(() => {
    mounted.current = true
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => { mounted.current = false; window.clearInterval(timer) }
  }, [])
  const nextCursor = cursor === undefined ? cycle?.helpersNextCursor : cursor
  const helpers = [...new Map([...(cycle?.helpers ?? []), ...extraHelpers].map(helper => [helper.userId, helper])).values()]
  const visibleHelpers = helpers.slice(0, 3)
  const permission = animal.permissions
  const deadline = animalGrowthDeadline(animal)
  async function loadHelpers() {
    if (!cycle || !nextCursor || helperBusy.current) return
    helperBusy.current = true
    setHelpersBusy(true)
    setHelpersError(null)
    const scope = getSessionScope()
    try {
      const page = await getAnimalHelpers(farmId, animal.id, cycle.id, nextCursor)
      if (!mounted.current || scope !== getSessionScope()) return
      setExtraHelpers(current => [...current, ...page.items])
      setCursor(page.nextCursor)
    } catch { if (mounted.current) setHelpersError('Não foi possível carregar mais ajudantes. Tente novamente.') }
    finally { helperBusy.current = false; if (mounted.current) setHelpersBusy(false) }
  }
  const countdown = deadline
    ? Date.parse(deadline) > now
      ? `⏳ ${growing ? 'Cresce' : 'Pronto'} em ${formatCountdown(deadline, now)}`
      : '⏳ Sincronizando…'
    : null
  const productAction = isVisiting ? 'steal' : 'collect'
  const productEnabled = isVisiting ? permission.canSteal : permission.canCollect
  const productReason = isVisiting ? permission.stealReason : permission.collectReason
  const isFertilizer = cycle?.productId === 'fertilizer'
  return <article className="space-y-2">
    <h3 className="text-sm">{growing ? presentation.youngIcon : presentation.icon} {growing ? `${presentation.youngName} · ${animal.displayName}` : animal.displayName}</h3>
    {cycle && cycle.helperCount > 0 && <p className="text-xs font-semibold text-sky-700">
      💛 {!isVisiting && visibleHelpers.length > 0
        ? `Cuidada por ${visibleHelpers.map(helper => helper.displayNameAtHelp).join(', ')}${cycle.helperCount > visibleHelpers.length ? ` e +${cycle.helperCount - visibleHelpers.length}` : ''}`
        : cycle.viewerHelped ? 'Você cuidou neste ciclo' : `${cycle.helperCount} cuidado(s) neste ciclo`}
    </p>}
    <p className="text-xs font-semibold text-emerald-700">{growing ? 'Crescendo' : cycle?.isReady ? 'Pronto' : 'Produzindo'}</p>
    {isVisiting && isFertilizer && cycle?.isReady && <p className="text-xs text-amber-800">🌿 O adubo está pronto, mas só o dono pode coletá-lo. Você ainda pode ajudar o porco no próximo ciclo.</p>}
    {growing && <div className="plot-actions"><button type="button" disabled className="plot-action disabled">{countdown}</button></div>}
    {cycle && <>
      {cycle.isReady && !isVisiting && <p className="text-sm">
        {product.icon} Disponível: {productQuantity(cycle.productId, cycle.ownerCollectible)}
      </p>}
      <div className="plot-actions">
        {isVisiting && permission.canHelp && <button type="button" disabled={busy || !!pending}
          onClick={() => void execute({ action: 'help', farmId, animalId: animal.id, expectedCycleId: cycle.id })}
          className="plot-action care">{busy ? 'Aguarde…' : '💛 Ajudar'}</button>}
        {!isFertilizer || !isVisiting ? <button type="button" disabled={busy || !!pending || !productEnabled || !cycle.isReady}
          onClick={() => void execute({ action: productAction, farmId, animalId: animal.id, expectedCycleId: cycle.id })}
          className={`plot-action ${!cycle.isReady ? 'disabled' : isVisiting ? 'steal' : 'harvest'}`}>
          {busy ? 'Aguarde…' : !cycle.isReady ? countdown : isVisiting ? `😈 Roubar ${product.actionName}` : cycle.productId === 'wool' ? '✂️ Tosquiar' : `${product.icon} Coletar ${product.actionName}`}
        </button> : null}
      </div>
      {cycle.isReady && !productEnabled && (!isFertilizer || !isVisiting) && <p className="text-xs text-amber-800">{animalReason(productReason)}</p>}
      {isVisiting && !cycle.isReady && !permission.canHelp && !cycle.viewerHelped
        && <p className="text-xs text-amber-800">{animalReason(permission.helpReason)}</p>}
      {!isVisiting && (nextCursor || helpers.length > visibleHelpers.length) && <details className="text-xs text-sky-700">
        <summary className="cursor-pointer">Ver cuidados</summary>
        <ul className="mt-1 space-y-1">{helpers.map(helper => <li key={helper.userId}>{helper.displayNameAtHelp}</li>)}</ul>
        {helpersError && <p role="alert" className="mt-2 text-red-800">{helpersError}</p>}
        {nextCursor && <button type="button" disabled={helpersBusy} onClick={() => void loadHelpers()} className="plot-action care w-full">{helpersBusy ? 'Carregando…' : 'Ver mais ajudantes'}</button>}
      </details>}
    </>}
  </article>
}
