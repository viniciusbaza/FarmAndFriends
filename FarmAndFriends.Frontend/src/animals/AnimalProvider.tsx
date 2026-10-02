import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../api/http'
import { getSessionScope } from '../api/sessionScope'
import { useFarm } from '../farm/useFarmContext'
import { useUser } from '../user/useUser'
import type { AnimalDone, AnimalScreenPosition, AnimalSelect } from '../types/animalEvents'
import { AnimalContext, type AnimalFeedback, type AnimalModalAnchor } from './AnimalContext'
import { attemptStorageKey, parseAnimalAttempt, prepareAnimalAttempt, type AnimalAttempt, type AnimalIntent } from './animalAttempt'
import { getAnimalCatalog, sendAnimalAttempt } from './animalApi'
import { shouldRefreshAnimalFarm } from './animalState'
import { animalSuccessMessage } from './animalPresentation'

export function AnimalProvider({ children }: { children: React.ReactNode }) {
  const { farm, patchAnimals, refreshFarm } = useFarm()
  const { user } = useUser()
  const [catalog, setCatalog] = useState<Awaited<ReturnType<typeof getAnimalCatalog>> | null>(null)
  const [catalogLoading, setCatalogLoading] = useState(true)
  const [catalogError, setCatalogError] = useState<string | null>(null)
  const [selection, setSelection] = useState<{
    farmId: string
    animalId: string
    anchor: AnimalModalAnchor | null
  } | null>(null)
  const [busy, setBusy] = useState(false)
  const busyRef = useRef(false)
  const [pending, setPending] = useState<AnimalAttempt | null>(null)
  const [feedback, setFeedback] = useState<AnimalFeedback | null>(null)
  const catalogRequest = useRef(0)
  const mounted = useRef(true)
  const seenEffects = useRef(new Set<string>())
  const userId = user?.id
  const farmId = farm?.id
  const activeFarm = useRef({ farmId, refreshFarm })
  activeFarm.current = { farmId, refreshFarm }
  const selected = selection && selection.farmId === farmId
    && farm?.animalState?.enclosure?.animals.some(animal => animal.id === selection.animalId)
    ? selection : null

  const refreshCatalog = useCallback(async () => {
    if (!userId) return
    const scope = getSessionScope()
    const request = ++catalogRequest.current
    const current = () => mounted.current && scope === getSessionScope() && request === catalogRequest.current
    try {
      const data = await getAnimalCatalog()
      if (!current()) return
      setCatalog(data)
      setCatalogError(null)
    } catch {
      if (!current()) return
      setCatalogError('Não foi possível carregar os animais e preços. Tente novamente.')
    } finally { if (current()) setCatalogLoading(false) }
  }, [userId])

  useEffect(() => {
    mounted.current = true
    return () => { mounted.current = false; catalogRequest.current += 1 }
  }, [])
  useEffect(() => {
    if (feedback?.type !== 'success') return
    const timer = window.setTimeout(() => {
      setFeedback(current => current === feedback ? null : current)
    }, 2_500)
    return () => window.clearTimeout(timer)
  }, [feedback])
  useEffect(() => {
    setCatalog(null)
    setCatalogLoading(true)
    setFeedback(null)
    setSelection(null)
    setBusy(false)
    busyRef.current = false
    setPending(userId ? parseAnimalAttempt(localStorage.getItem(attemptStorageKey(userId))) : null)
    void refreshCatalog()
  }, [userId, refreshCatalog])
  useEffect(() => {
    const refresh = () => { void refreshCatalog() }
    window.addEventListener('economy:confirmed', refresh)
    window.addEventListener('focus', refresh)
    const timer = window.setInterval(refresh, 30_000)
    return () => {
      window.removeEventListener('economy:confirmed', refresh)
      window.removeEventListener('focus', refresh)
      window.clearInterval(timer)
    }
  }, [refreshCatalog])

  const open = useCallback((animalId: string, anchor?: AnimalModalAnchor | null) => {
    if (!farmId || !farm?.animalState?.enclosure?.animals.some(animal => animal.id === animalId)) return
    setSelection({ farmId, animalId, anchor: anchor ?? null })
    setFeedback(null)
  }, [farmId, farm?.animalState])
  const close = useCallback(() => { setSelection(null) }, [])
  useEffect(() => {
    const animalSelect = (event: Event) => {
      const detail = (event as CustomEvent<AnimalSelect>).detail
      if (detail.farmId === farmId && farm?.animalState?.enclosure?.animals.some(animal => animal.id === detail.animalId)) {
        open(detail.animalId, eventAnchor(detail))
      }
    }
    window.addEventListener('animal:select', animalSelect)
    return () => {
      window.removeEventListener('animal:select', animalSelect)
    }
  }, [farmId, farm?.animalState, open])

  async function run(attempt: AnimalAttempt) {
    if (!userId || busyRef.current) return null
    const scope = getSessionScope()
    const current = () => mounted.current && scope === getSessionScope()
    busyRef.current = true
    setBusy(true)
    setFeedback(null)
    try {
      // Persist before HTTP: a crash or lost response must retain the original fingerprint.
      localStorage.setItem(attemptStorageKey(userId), JSON.stringify(attempt))
      setPending(attempt)
    } catch {
      busyRef.current = false
      setBusy(false)
      setFeedback({ type: 'error', message: 'Não foi possível guardar a tentativa neste navegador. Libere o armazenamento e tente novamente.' })
      return null
    }
    try {
      const result = await sendAnimalAttempt(attempt)
      if (!current()) return null
      localStorage.removeItem(attemptStorageKey(userId))
      setPending(null)
      if (result.current) patchAnimals(result.current)
      if (result.effect.action === 'collect' || result.effect.action === 'help' || result.effect.action === 'steal') {
        setSelection(selected => selected?.farmId === result.effect.farmId
          && selected.animalId === result.effect.animalId
          ? null : selected)
      }
      if (activeFarm.current.farmId === attempt.farmId || attempt.action.startsWith('buy-') || attempt.action === 'sell-animal-product') {
        setFeedback({ type: 'success', message: result.replayed ? 'Resultado recuperado. A ação já havia sido concluída.' : animalSuccessMessage(result.effect) })
      }
      if (activeFarm.current.farmId === result.effect.farmId && !result.replayed && !seenEffects.current.has(result.operationId)) {
        seenEffects.current.add(result.operationId)
        const event = result.effect.action === 'buy-animal-enclosure' ? 'enclosure:buy:done'
          : result.effect.action === 'buy-animal' ? 'animal:buy:done'
          : `animal:${result.effect.action}:done`
        const dispatchDone = () => {
          if (!current() || activeFarm.current.farmId !== result.effect.farmId) return
          window.dispatchEvent(new CustomEvent<AnimalDone>(event, { detail: { ...result.effect, operationId: result.operationId } }))
        }
        if (result.effect.action === 'collect' || result.effect.action === 'help' || result.effect.action === 'steal') {
          // Let the modal unmount before playing the confirmed world feedback.
          window.requestAnimationFrame(dispatchDone)
        } else {
          dispatchDone()
        }
      }
      await Promise.allSettled([
        shouldRefreshAnimalFarm(activeFarm.current.farmId, attempt.farmId) ? activeFarm.current.refreshFarm() : Promise.resolve(),
        refreshCatalog(),
      ])
      return result
    } catch (error) {
      if (!current()) return null
      const definitive = error instanceof ApiError && error.status >= 400 && error.status < 500 && error.status !== 408 && error.status !== 429
      if (definitive) {
        localStorage.removeItem(attemptStorageKey(userId))
        setPending(null)
      }
      setFeedback({ type: definitive ? 'error' : 'pending', message: definitive
        ? error.message
        : 'A resposta não chegou. Sua ação pode ter sido concluída. Recupere o resultado antes de tentar outra ação.' })
      await Promise.allSettled([
        shouldRefreshAnimalFarm(activeFarm.current.farmId, attempt.farmId) ? activeFarm.current.refreshFarm() : Promise.resolve(),
        refreshCatalog(),
      ])
      return null
    } finally {
      if (current()) { busyRef.current = false; setBusy(false) }
    }
  }
  function execute(intent: AnimalIntent) {
    if (!userId || busyRef.current) return Promise.resolve(null)
    const saved = parseAnimalAttempt(localStorage.getItem(attemptStorageKey(userId)))
    if (saved) { setPending(saved); return Promise.resolve(null) }
    return run(prepareAnimalAttempt(null, intent, () => crypto.randomUUID()))
  }
  function recover() {
    if (!userId) return Promise.resolve(null)
    const attempt = parseAnimalAttempt(localStorage.getItem(attemptStorageKey(userId)))
    return attempt ? run(attempt) : Promise.resolve(null)
  }
  return <AnimalContext.Provider value={{ catalog, catalogLoading, catalogError, refreshCatalog,
    selected, open, close, busy, pending, feedback, execute, recover }}>{children}</AnimalContext.Provider>
}

function eventAnchor(detail: AnimalScreenPosition): AnimalModalAnchor | null {
  return Number.isFinite(detail.screenX) && Number.isFinite(detail.screenY)
    ? { x: detail.screenX!, y: detail.screenY! }
    : null
}
