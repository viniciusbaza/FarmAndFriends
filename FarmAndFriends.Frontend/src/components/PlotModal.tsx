import { useCallback, useEffect, useRef, useState } from 'react'
import type { KeyboardEvent as ReactKeyboardEvent } from 'react'
import { ApiError, authFetch } from '../api/http'
import { hasFarmExpansion, getLandPurchaseSuccessMessage } from '../land/landPurchase'
import type { LandPurchaseAttemptStore } from '../land/useLandPurchase'
import { useFarm } from '../farm/useFarmContext'
import {
  confirmedHarvestPatch,
  confirmedPestRemovalPatch,
  confirmedTheftPatch,
  getHarvestCyclePrecondition,
  getPestRemovalAttempt,
} from '../farm/farmState'
import type { PestRemovalAttempt } from '../farm/farmState'
import { useInventory } from '../inventory/useInventory'
import { useFertilizerAttempt } from '../fertilizer/useFertilizerAttempt'
import { fertilizerStock } from '../fertilizer/fertilizerState'
import { refreshFertilizerState } from '../fertilizer/fertilizerFeedback'
import { useSeeds } from '../seeds/useSeeds'
import { useShopItems } from '../shop/useShopItems'
import type {
  ApplyPestProtectionResponse,
  CareResponse,
  HarvestResponse,
  LandPurchaseResponse,
  PestActionResponse,
  PlantResponse,
  PlotPestRemoveDone,
  StealResponse,
} from '../types/Farm'
import { useUser } from '../user/useUser'
import { hasServerPestProtection } from '../utils/pests'
import { formatCountdown, formatTimeRemaining } from '../utils/time'
import { LandPurchasePanel } from './LandPurchasePanel'

const NATURAL_REPELLENT_ID = 'natural_repellent'

type Props = {
  plotId: string
  onClose: () => void
  landPurchaseAttemptStore: LandPurchaseAttemptStore
  onBusyChange?: (busy: boolean) => void
  onLandPurchaseFeedback?: (feedback: LandPurchaseFeedback) => void
}

export type LandPurchaseFeedback = {
  type: 'pending' | 'success' | 'error'
  message: string
}

type PendingAction =
  | 'plant'
  | 'harvest'
  | 'steal'
  | 'care'
  | 'remove-pest'
  | 'protect'
  | 'fertilize'

type Feedback = {
  type: 'success' | 'error'
  message: string
}

type HarvestCycleProgressProps = {
  currentCycle: number
  totalCycles: number
}

function HarvestCycleProgress({
  currentCycle,
  totalCycles,
}: HarvestCycleProgressProps) {
  const radius = 10
  const circumference = 2 * Math.PI * radius
  const completedPortion = currentCycle / totalCycles

  return (
    <span
      role="progressbar"
      aria-label={`Progresso das colheitas: ${currentCycle} de ${totalCycles}`}
      aria-valuemin={1}
      aria-valuemax={totalCycles}
      aria-valuenow={currentCycle}
      className="relative inline-flex h-7 w-7 shrink-0 items-center justify-center"
    >
      <svg
        viewBox="0 0 36 36"
        className="h-7 w-7"
        aria-hidden="true"
      >
        <circle
          cx="18"
          cy="18"
          r={radius}
          fill="none"
          strokeWidth="4"
          className="stroke-emerald-100"
        />
        <circle
          cx="18"
          cy="18"
          r={radius}
          fill="none"
          strokeWidth="4"
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={circumference * (1 - completedPortion)}
          transform="rotate(-90 18 18)"
          className="stroke-emerald-600 transition-[stroke-dashoffset] duration-300"
        />
      </svg>
      <span
        aria-hidden="true"
        className="absolute text-[9px] font-bold tabular-nums text-emerald-800"
      >
        {currentCycle}
      </span>
    </span>
  )
}

function errorMessage(error: unknown, fallback: string) {
  return error instanceof Error ? error.message : fallback
}

export function PlotModal({
  plotId,
  onClose,
  landPurchaseAttemptStore,
  onBusyChange,
  onLandPurchaseFeedback,
}: Props) {
  const {
    farm,
    refreshFarm,
    patchPlot,
    isVisiting,
    canInteract,
  } = useFarm()
  const { getSeed } = useSeeds()
  const { inventory, economyPending, refreshInventory } = useInventory()
  const refreshLandInventory = useCallback(async () => { await refreshInventory() }, [refreshInventory])
  const { getItem } = useShopItems()
  const { user, refreshUser } = useUser()
  const fertilizerAttempt = useFertilizerAttempt(user?.id)
  const dialogRef = useRef<HTMLDivElement>(null)
  const closeButtonRef = useRef<HTMLButtonElement>(null)
  const pestRemovalAttemptRef = useRef<PestRemovalAttempt | null>(null)
  const actionBusyRef = useRef(false)
  const fertilizerBusyRef = useRef(false)

  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null)
  const [feedback, setFeedback] = useState<Feedback | null>(null)
  const [confirmProtection, setConfirmProtection] = useState(false)
  const [confirmFertilizer, setConfirmFertilizer] = useState(false)
  const [fertilizedCycle, setFertilizedCycle] = useState<string | null>(null)
  const [fertilizerSyncPending, setFertilizerSyncPending] = useState(false)
  const [landPurchasePending, setLandPurchasePending] = useState(false)
  const [currentTime, setCurrentTime] = useState(Date.now)
  const handleLandPurchasePending = useCallback((pending: boolean) => {
    setLandPurchasePending(pending)
  }, [])

  const plot = farm?.plots.find(candidate => candidate.id === plotId)
  const farmId = farm?.id
  const landOffer = farm?.landOffer ?? null
  const care = plot?.care
  const nextCareAt = care?.nextCareAt
  const careCycleEndsAt = care?.careCycleEndsAt
  const canCareNow = Boolean(care?.canCare)
  const careOpportunityId = canCareNow
    ? care?.opportunityId ?? null
    : null
  const seedCatalog = getSeed(plot?.seedId ?? undefined)
  const currentHarvestCycle = plot?.currentHarvestCycle ?? 1
  const harvestCycles = seedCatalog?.harvestCycles ?? 1
  const isRegrowing = currentHarvestCycle > 1
  const repellent = getItem(NATURAL_REPELLENT_ID)
  const repellentQuantity = inventory?.items.find(
    item => item.itemType === 'Item' && item.itemId === NATURAL_REPELLENT_ID,
  )?.quantity ?? 0
  const fertilizerQuantity = fertilizerStock(inventory?.items)
  const canOfferFertilizer = Boolean(plot?.unlocked && plot.seedId && !plot.isReady
    && plot.currentHarvestCycle !== null && plot.currentHarvestCycleStartedAt
    && fertilizerQuantity > 0
    && !economyPending && !fertilizerSyncPending
    && !plot.fertilizedAt
    && fertilizedCycle !== `${farmId}:${plotId}:${plot.currentHarvestCycle}:${plot.currentHarvestCycleStartedAt}`)

  const readyAt = plot?.readyAt
  const protectedUntil = plot?.protectedUntil
  const isProtected = hasServerPestProtection(protectedUntil ?? null)
  const pestConsumesAt = plot?.pest?.status === 'active'
    ? plot.pest.consumesAt
    : null
  const timeLeft = readyAt
    ? formatTimeRemaining(readyAt, currentTime)
    : null
  const pestTimeLeft = pestConsumesAt
    ? formatCountdown(pestConsumesAt, currentTime)
    : null
  const protectionTimeLeft = isProtected && protectedUntil
    ? formatCountdown(protectedUntil, currentTime)
    : null
  const nextCareAtTime = nextCareAt
    ? new Date(nextCareAt).getTime()
    : Number.NaN
  const careTimeLeft = Number.isFinite(nextCareAtTime)
    && nextCareAtTime > currentTime
    ? formatTimeRemaining(nextCareAt!, currentTime)
    : null
  const careCycleEndsAtTime = careCycleEndsAt
    ? new Date(careCycleEndsAt).getTime()
    : Number.NaN
  const careCycleTimeLeft = Number.isFinite(careCycleEndsAtTime)
    && careCycleEndsAtTime > currentTime
    ? formatTimeRemaining(careCycleEndsAt!, currentTime)
    : null
  const seeds = canInteract
    ? inventory?.items.filter(
        item => item.itemType === 'Seed' && item.quantity > 0,
      ) ?? []
    : []
  const canOfferProtection = Boolean(
    plot?.unlocked
      && !isProtected
      && repellent !== undefined
      && repellentQuantity > 0,
  )
  const actionBusy = pendingAction !== null || landPurchasePending
  actionBusyRef.current = actionBusy

  useEffect(() => {
    onBusyChange?.(actionBusy)
    return () => onBusyChange?.(false)
  }, [actionBusy, onBusyChange])

  useEffect(() => {
    if (
      !readyAt
      && !nextCareAt
      && !careCycleEndsAt
      && !pestConsumesAt
      && !protectedUntil
    ) {
      return
    }

    const interval = setInterval(() => setCurrentTime(Date.now()), 1000)
    return () => clearInterval(interval)
  }, [
    careCycleEndsAt,
    nextCareAt,
    pestConsumesAt,
    protectedUntil,
    readyAt,
  ])

  useEffect(() => {
    const previouslyFocused = document.activeElement
    closeButtonRef.current?.focus()

    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape' && !actionBusyRef.current) onClose()
    }

    window.addEventListener('keydown', closeOnEscape)
    return () => {
      window.removeEventListener('keydown', closeOnEscape)
      if (previouslyFocused instanceof HTMLElement) {
        previouslyFocused.focus()
      }
    }
  }, [onClose])

  if (!plot) return null

  function keepFocusInside(event: ReactKeyboardEvent<HTMLDivElement>) {
    if (event.key !== 'Tab') return

    const focusable = dialogRef.current?.querySelectorAll<HTMLElement>(
      'button:not(:disabled), input:not(:disabled), [tabindex]:not([tabindex="-1"])',
    )
    if (!focusable || focusable.length === 0) return

    const first = focusable[0]
    const last = focusable[focusable.length - 1]
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault()
      last.focus()
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault()
      first.focus()
    }
  }

  async function handlePlant(seedId: string) {
    if (plot?.unlocked !== true || isVisiting || pendingAction) return

    setFeedback(null)
    setPendingAction('plant')

    try {
      const data = await authFetch<PlantResponse>(`/plots/${plotId}/plant`, {
        method: 'POST',
        body: JSON.stringify({ seedId }),
      })

      window.dispatchEvent(
        new CustomEvent('plot:plant:done', {
          detail: { plotId, xpGained: data.xpGained },
        }),
      )
      await Promise.all([refreshFarm(), refreshInventory()])
      onClose()
    } catch (error) {
      setFeedback({
        type: 'error',
        message: errorMessage(error, 'Não foi possível plantar agora.'),
      })
    } finally {
      setPendingAction(null)
    }
  }

  async function handleHarvest() {
    if (plot?.unlocked !== true || isVisiting || pendingAction) return

    setFeedback(null)
    setPendingAction('harvest')

    try {
      const harvestRequest = getHarvestCyclePrecondition(plot)
      const data = await authFetch<HarvestResponse>(
        `/plots/${plotId}/harvest`,
        {
          method: 'POST',
          ...(harvestRequest
            ? { body: JSON.stringify(harvestRequest) }
            : {}),
        },
      )

      window.dispatchEvent(
        new CustomEvent('plot:harvest:done', {
          detail: { plotId, xpGained: data.xpGained },
        }),
      )
      patchPlot(plotId, confirmedHarvestPatch(data))
      window.dispatchEvent(new Event('inventory:changed'))
      await Promise.all([refreshFarm(), refreshInventory()])
      onClose()
    } catch (error) {
      setFeedback({
        type: 'error',
        message: errorMessage(error, 'Não foi possível colher agora.'),
      })
      await refreshFarm()
    } finally {
      setPendingAction(null)
    }
  }

  async function handleSteal() {
    if (
      plot?.unlocked !== true
      || !isVisiting
      || !farmId
      || pendingAction
    ) {
      return
    }

    setFeedback(null)
    setPendingAction('steal')

    try {
      const data = await authFetch<StealResponse>(
        `/farms/${farmId}/plots/${plotId}/steal`,
        { method: 'POST' },
      )

      window.dispatchEvent(
        new CustomEvent('plot:steal:done', {
          detail: {
            plotId,
            stolen: data.stolen,
            remainingYield: data.ownerWillReceive,
            xpGained: data.xpGained,
            pestCancelled: data.pestCancelled,
          },
        }),
      )
      patchPlot(
        plotId,
        currentPlot => confirmedTheftPatch(currentPlot, data),
      )
      await refreshFarm()
      onClose()
    } catch (error) {
      const message = errorMessage(error, 'Erro ao roubar')
      setFeedback({ type: 'error', message })
      window.dispatchEvent(
        new CustomEvent('plot:steal:failed', {
          detail: { plotId, reason: message },
        }),
      )
      await refreshFarm()
    } finally {
      setPendingAction(null)
    }
  }

  async function handleCare() {
    if (
      !isVisiting
      || plot?.unlocked !== true
      || !farmId
      || !careOpportunityId
      || pendingAction
    ) {
      return
    }

    setFeedback(null)
    setPendingAction('care')

    try {
      const data = await authFetch<CareResponse>(
        `/farms/${farmId}/plots/${plotId}/care`,
        {
          method: 'POST',
          headers: { 'Idempotency-Key': crypto.randomUUID() },
          body: JSON.stringify({ careOpportunityId }),
        },
      )

      window.dispatchEvent(
        new CustomEvent('plot:care:done', {
          detail: {
            plotId,
            coinsGained: data.coinsGained,
            xpGained: data.xpGained,
            caredByUsername: data.caredByUsername,
          },
        }),
      )
      window.dispatchEvent(new Event('inventory:changed'))
      await refreshFarm()
      onClose()
    } catch (error) {
      setFeedback({
        type: 'error',
        message: errorMessage(error, 'Não foi possível deixar o cuidado.'),
      })
      await refreshFarm()
    } finally {
      setPendingAction(null)
    }
  }

  async function handleRemovePest() {
    const pest = plot?.pest
    if (plot?.unlocked !== true || !farmId || pendingAction || !pest?.canRemove) {
      return
    }

    setFeedback(null)
    setPendingAction('remove-pest')
    const pestOccurrenceId = pest.occurrenceId

    if (pestOccurrenceId === null) {
      setFeedback({
        type: 'success',
        message: '🐛 Só um instante: estamos sincronizando esta lagarta.',
      })
      await refreshFarm()
      setPendingAction(null)
      return
    }

    const attempt = getPestRemovalAttempt(
      pestRemovalAttemptRef.current,
      pestOccurrenceId,
      () => crypto.randomUUID(),
    )
    pestRemovalAttemptRef.current = attempt

    try {
      const data = await authFetch<PestActionResponse>(
        `/farms/${farmId}/plots/${plotId}/pest/remove`,
        {
          method: 'POST',
          headers: { 'Idempotency-Key': attempt.key },
          body: JSON.stringify({ pestOccurrenceId }),
        },
      )

      if (pestRemovalAttemptRef.current?.key === attempt.key) {
        pestRemovalAttemptRef.current = null
      }
      patchPlot(
        plotId,
        currentPlot => confirmedPestRemovalPatch(currentPlot, data),
      )

      window.dispatchEvent(new Event('inventory:changed'))
      await Promise.allSettled([
        refreshFarm(),
        refreshInventory(),
        refreshUser(),
      ])

      if (data.replayed) {
        setFeedback({
          type: 'success',
          message: '🐛 Remoção já confirmada. Resultado recuperado.',
        })
        return
      }

      const removeDone: PlotPestRemoveDone = {
        plotId,
        pestOccurrenceId: data.pestOccurrenceId,
        coinsGained: data.coinsGained,
        xpGained: data.xpGained,
      }
      onClose()
      window.requestAnimationFrame(() => {
        window.dispatchEvent(
          new CustomEvent<PlotPestRemoveDone>('plot:pest:remove:done', {
            detail: removeDone,
          }),
        )
      })
    } catch (error) {
      setFeedback({
        type: 'error',
        message: errorMessage(error, 'Não foi possível remover a lagarta.'),
      })
      window.dispatchEvent(new Event('inventory:changed'))
      await Promise.allSettled([
        refreshFarm(),
        refreshInventory(),
        refreshUser(),
      ])
    } finally {
      setPendingAction(null)
    }
  }

  async function handleApplyProtection() {
    if (!farmId || pendingAction || !canOfferProtection) return

    setFeedback(null)
    setPendingAction('protect')

    try {
      const data = await authFetch<ApplyPestProtectionResponse>(
        `/farms/${farmId}/plots/${plotId}/pest/protection`,
        { method: 'POST' },
      )
      patchPlot(plotId, {
        pest: data.pest,
        protectedUntil: data.protectedUntil,
        remainingYield: data.remainingYield,
      })
      await Promise.all([refreshFarm(), refreshInventory()])
      window.dispatchEvent(new Event('inventory:changed'))
      setConfirmProtection(false)
      setFeedback({
        type: 'success',
        message: data.pest?.status === 'cancelledByProtection'
          ? `🛡️ ${repellent?.name ?? 'Repelente Natural'} aplicado: a lagarta foi espantada.`
          : `🛡️ ${repellent?.name ?? 'Repelente Natural'} aplicado ao lote.`,
      })
      setCurrentTime(Date.now())
    } catch (error) {
      setFeedback({
        type: 'error',
        message: errorMessage(error, 'Não foi possível proteger este lote.'),
      })
      await Promise.all([refreshFarm(), refreshInventory()])
    } finally {
      setPendingAction(null)
    }
  }

  async function handleFertilize(recover = false) {
    if (pendingAction || fertilizerBusyRef.current || !farmId || (!recover && !canOfferFertilizer)) return
    const cycle = plot?.currentHarvestCycle
    const cycleStartedAt = plot?.currentHarvestCycleStartedAt
    if (!recover && (cycle === null || cycle === undefined || !cycleStartedAt)) return
    setFeedback(null)
    fertilizerBusyRef.current = true
    setPendingAction('fertilize')
    try {
      const response = recover
        ? await fertilizerAttempt.recover()
        : await fertilizerAttempt.execute({ action: 'fertilize', farmId, plotId,
          expectedHarvestCycle: cycle!, expectedCycleStartedAt: cycleStartedAt! })
      if (!response) return
      const recoveredPlot = fertilizerAttempt.pending?.action === 'fertilize'
        && fertilizerAttempt.pending.farmId === farmId
        && fertilizerAttempt.pending.plotId === plotId
      if ('readyAt' in response && (!recover || recoveredPlot)) {
        // A fresh farm projection confirms the current cycle before Phaser receives a new deadline.
        const completedCycle = recover && fertilizerAttempt.pending?.action === 'fertilize'
          ? fertilizerAttempt.pending.expectedHarvestCycle : cycle
        const completedStart = recover && fertilizerAttempt.pending?.action === 'fertilize'
          ? fertilizerAttempt.pending.expectedCycleStartedAt : cycleStartedAt
        if (completedCycle !== null && completedCycle !== undefined && completedStart) {
          setFertilizedCycle(`${farmId}:${plotId}:${completedCycle}:${completedStart}`)
        }
      }
      window.dispatchEvent(new Event('inventory:changed'))
      const refreshed = await refreshFertilizerState(refreshFarm, refreshInventory)
      const synchronized = refreshed.farmUpdated && refreshed.inventoryUpdated
      setFertilizerSyncPending(!synchronized)
      setConfirmFertilizer(false)
      setCurrentTime(Date.now())
      const secondsGained = 'readyAt' in response
        ? Math.max(0, Math.round((Date.parse(response.previousReadyAt) - Date.parse(response.readyAt)) / 1000))
        : 0
      const timeGained = secondsGained >= 60
        ? `${Math.floor(secondsGained / 60)} min${secondsGained % 60 ? ` ${secondsGained % 60} s` : ''}`
        : `${secondsGained} s`
      setFeedback({ type: 'success', message: !synchronized
        ? '🌿 Ação de adubo confirmada. Ainda não foi possível atualizar a plantação e o estoque. Atualize para conferir o resultado; o adubo não será usado novamente.'
        : !('readyAt' in response)
        ? '🌿 Compra de adubo recuperada. Estoque atualizado.'
        : response.replayed ? '🌿 Adubação já confirmada. Estoque e plantação atualizados.'
          : isVisiting ? `🌿 Você adubou a plantação do seu amigo e adiantou a colheita em ${timeGained}.`
            : `🌿 Plantação adubada! A colheita foi adiantada em ${timeGained}.` })
    } catch (error) {
      if (error instanceof ApiError && error.code === 'ALREADY_FERTILIZED'
        && cycle !== null && cycle !== undefined && cycleStartedAt) {
        setFertilizedCycle(`${farmId}:${plotId}:${cycle}:${cycleStartedAt}`)
      }
      setFeedback({ type: 'error', message: errorMessage(error, 'Não foi possível adubar agora. Atualize e confira seu estoque.') })
      window.dispatchEvent(new Event('inventory:changed'))
      await Promise.allSettled([refreshFarm(), refreshInventory()])
    } finally {
      fertilizerBusyRef.current = false
      setPendingAction(null)
    }
  }

  async function retryFertilizerSync() {
    if (pendingAction || fertilizerBusyRef.current) return
    fertilizerBusyRef.current = true
    setPendingAction('fertilize')
    try {
      const refreshed = await refreshFertilizerState(refreshFarm, refreshInventory)
      const synchronized = refreshed.farmUpdated && refreshed.inventoryUpdated
      setFertilizerSyncPending(!synchronized)
      setFeedback({ type: synchronized ? 'success' : 'error', message: synchronized
        ? '🌿 Plantação e estoque atualizados. Confira as ações disponíveis no lote.'
        : 'Ainda não foi possível atualizar a plantação e o estoque. Tente novamente.' })
    } finally {
      fertilizerBusyRef.current = false
      setPendingAction(null)
    }
  }

  function handleLandPurchaseConfirmed(response: LandPurchaseResponse) {
    const message = getLandPurchaseSuccessMessage(response)

    if (hasFarmExpansion(response)) {
      onLandPurchaseFeedback?.({ type: 'success', message })
      return
    }

    setFeedback({ type: 'success', message })
  }

  function handleLandPurchaseReconciliationStarted(
    response: LandPurchaseResponse,
  ) {
    if (!hasFarmExpansion(response)) return

    onLandPurchaseFeedback?.({
      type: 'pending',
      message: `Atualizando a fazenda com o lote ${response.plotNumber}...`,
    })
    onClose()
  }

  function handleLandPurchaseReconciliationFailed(
    response: LandPurchaseResponse,
    message: string,
  ) {
    if (!hasFarmExpansion(response)) return
    onLandPurchaseFeedback?.({ type: 'error', message })
  }

  async function reconcileLandPurchase(
    response: LandPurchaseResponse,
  ) {
    let reconciled = false

    function onFarmSynchronized(event: Event) {
      if (!(event instanceof CustomEvent)) return

      const detail: unknown = event.detail
      if (!detail || typeof detail !== 'object' || !('plots' in detail)) return
      if (!Array.isArray(detail.plots)) return

      reconciled = detail.plots.some((candidate: unknown) => (
        candidate !== null
        && typeof candidate === 'object'
        && 'id' in candidate
        && candidate.id === response.plotId
        && 'unlocked' in candidate
        && candidate.unlocked === true
      ))
    }

    window.addEventListener('farm:sync', onFarmSynchronized)
    window.addEventListener('farm:change', onFarmSynchronized)

    try {
      await refreshFarm()
      if (!reconciled) {
        throw new Error(
          'A compra foi recebida, mas n\u00e3o foi poss\u00edvel atualizar a fazenda. Tente novamente pela placa.',
        )
      }
    } finally {
      window.removeEventListener('farm:sync', onFarmSynchronized)
      window.removeEventListener('farm:change', onFarmSynchronized)
    }
  }

  const activePest = plot.pest?.status === 'active'

  return (
    <div
      ref={dialogRef}
      role="dialog"
      aria-modal="true"
      aria-labelledby="plot-modal-title"
      aria-busy={actionBusy}
      className="plot-modal w-72 max-w-[calc(100vw-2rem)]"
      onPointerDown={event => event.stopPropagation()}
      onKeyDown={keepFocusInside}
    >
      <div className="mb-2 flex items-start justify-between gap-3">
        <h2 id="plot-modal-title" className="font-bold text-emerald-900">
          Detalhes do lote ({plot.x + 1},{plot.y + 1})
        </h2>
        <button
          ref={closeButtonRef}
          type="button"
          onClick={() => {
            if (!actionBusy) onClose()
          }}
          disabled={actionBusy}
          className="-mr-1 -mt-1 rounded-full px-2 py-1 text-lg leading-none text-slate-600 hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700"
          aria-label="Fechar detalhes do lote"
        >
          ×
        </button>
      </div>

      {!plot.unlocked && (
        <p className="text-sm text-red-600">🔒 Terreno bloqueado</p>
      )}

      {!isVisiting && landOffer?.plotId === plotId && (
        <LandPurchasePanel
          key={landOffer.plotId}
          offer={landOffer}
          coins={inventory?.coins ?? null}
          premiumCoins={inventory?.premiumCoins ?? null}
          playerLevel={user?.level ?? null}
          attemptStore={landPurchaseAttemptStore}
          refreshFarm={reconcileLandPurchase}
          refreshInventory={refreshLandInventory}
          onReconciliationStarted={handleLandPurchaseReconciliationStarted}
          onReconciliationFailed={handleLandPurchaseReconciliationFailed}
          onConfirmed={handleLandPurchaseConfirmed}
          onPendingChange={handleLandPurchasePending}
        />
      )}

      {plot.unlocked && isProtected && protectionTimeLeft && (
        <div className="mb-2 rounded-lg border border-sky-200 bg-sky-50 px-3 py-2 text-sm font-semibold text-sky-800">
          🛡️ Protegido contra pragas por {protectionTimeLeft}
        </div>
      )}

      {canInteract && plot.unlocked && !plot.seedId && (
        <>
          <p className="mb-2 font-semibold">🌱 Plantar:</p>
          {seeds.length === 0 && (
            <p className="text-sm text-gray-500">Você não tem sementes.</p>
          )}
          <div className="flex flex-wrap gap-2">
            {seeds.map(seed => (
              <button
                key={seed.itemId}
                type="button"
                className="rounded-lg border bg-green-100 px-3 py-1 text-sm hover:bg-green-200 disabled:cursor-wait disabled:opacity-60"
                onClick={() => void handlePlant(seed.itemId)}
                disabled={actionBusy}
              >
                🌱 {getSeed(seed.itemId)?.name} ×{seed.quantity}
              </button>
            ))}
          </div>
        </>
      )}

      {isVisiting && plot.unlocked && !plot.seedId && !canOfferProtection && (
        <p className="text-sm text-gray-500">Este lote está vazio.</p>
      )}

      {plot.unlocked && plot.seedId && seedCatalog && (
        <>
          <p className="mb-2 flex items-center gap-1.5 text-sm">
            <span>{seedCatalog.icon}</span>
            <span>{seedCatalog.name}</span>
            {harvestCycles > 1 && (
              <HarvestCycleProgress
                currentCycle={currentHarvestCycle}
                totalCycles={harvestCycles}
              />
            )}
          </p>
          {activePest && (
            <section
              aria-label="Lagarta ativa"
              className="mb-2 rounded-lg border border-amber-300 bg-amber-50 px-3 py-2"
            >
              <p className="font-bold text-amber-900">🐛 Lagarta encontrada!</p>
              <p className="mt-1 text-sm text-amber-800">
                Ela comerá a produção
                {pestTimeLeft ? ` em ${pestTimeLeft}` : ''}.
              </p>
              {plot.pest?.canRemove && (
                <button
                  type="button"
                  className="mt-2 w-full rounded-lg border border-amber-400 bg-white px-3 py-2 text-sm font-semibold text-amber-900 hover:bg-amber-100 disabled:cursor-wait disabled:opacity-60"
                  onClick={() => void handleRemovePest()}
                  disabled={actionBusy}
                >
                  {pendingAction === 'remove-pest'
                    ? 'Removendo...'
                    : 'Remover lagarta'}
                </button>
              )}
            </section>
          )}

          {plot.pest?.status === 'consumed' && (
            <p className="mb-2 rounded-lg border border-orange-200 bg-orange-50 px-3 py-2 text-sm text-orange-800">
              Uma lagarta comeu {plot.pest.consumedAmount ?? 1} {' '}
              {(seedCatalog?.cropName ?? 'sua cultura').toLocaleLowerCase('pt-BR')}.
            </p>
          )}

          {!isVisiting && (plot.care?.caregiverCount ?? 0) > 0 && (
            <p className="mt-1 text-xs font-semibold text-sky-700">
              💧 Regada por{' '}
              {plot.care!.caregivers
                .map(caregiver => caregiver.username)
                .join(', ')}
              {plot.care!.caregiverCount > plot.care!.caregivers.length
                ? ` e +${plot.care!.caregiverCount
                    - plot.care!.caregivers.length}`
                : ''}
            </p>
          )}
          {isVisiting
            && plot.care?.viewerCared
            && !canCareNow
            && careTimeLeft && (
              <p className="mt-1 text-xs font-semibold text-sky-700">
                💧 Regue novamente em {careTimeLeft}.
              </p>
            )}
          {isVisiting
            && canCareNow
            && plot.care?.rewardAvailable === false
            && careCycleTimeLeft && (
              <p className="mt-1 max-w-52 text-[11px] leading-snug text-amber-700">
                💧 Você regou recentemente. Regue novamente em {careCycleTimeLeft}.
              </p>
            )}

          <div className="mt-2 flex items-center gap-2 text-xs font-semibold text-emerald-700">
            <p>
              {plot.isReady
                ? 'Pronto'
                : isRegrowing
                  ? 'Produzindo'
                  : 'Crescendo'}
            </p>
          </div>

          <div className="plot-actions">
            {plot.isReady ? (
              isVisiting ? (
                <button
                  type="button"
                  className="plot-action steal"
                  onClick={() => void handleSteal()}
                  disabled={actionBusy}
                >
                  {pendingAction === 'steal' ? 'Roubando...' : '😈 Roubar'}
                </button>
              ) : (
                <button
                  type="button"
                  className="plot-action harvest"
                  onClick={() => void handleHarvest()}
                  disabled={actionBusy}
                >
                  {pendingAction === 'harvest' ? 'Colhendo...' : '✂️ Colher'}
                </button>
              )
            ) : (
              <>
                {isVisiting
                  && canCareNow
                  && plot.care?.rewardAvailable === true && (
                    <button
                      type="button"
                      className="plot-action care"
                      onClick={() => void handleCare()}
                      disabled={actionBusy}
                    >
                      {pendingAction === 'care' ? '💧 Regando...' : '💧 Regar'}
                    </button>
                  )}
                <div className="plot-action disabled">
                  ⏳ Pronto em {timeLeft}
                </div>
              </>
            )}
          </div>
        </>
      )}

      {canOfferProtection && (
        <section className="mt-3 border-t border-slate-200 pt-3">
          {!confirmProtection ? (
            <button
              type="button"
              className="w-full rounded-lg border border-sky-300 bg-sky-50 px-3 py-2 text-sm font-semibold text-sky-800 hover:bg-sky-100 disabled:opacity-60"
              onClick={() => setConfirmProtection(true)}
              disabled={actionBusy}
            >
              🛡️ Aplicar {repellent?.name ?? 'Repelente Natural'} ({repellentQuantity})
            </button>
          ) : (
            <div className="rounded-lg border border-sky-300 bg-sky-50 p-2">
              <p className="text-sm text-sky-900">
                Usar 1 {repellent?.name ?? 'Repelente Natural'} neste lote?
              </p>
              {isVisiting && (
                <p className="mt-1 text-xs text-sky-700">
                  O item sairá do seu inventário e protegerá a fazenda visitada.
                </p>
              )}
              <div className="mt-2 flex gap-2">
                <button
                  type="button"
                  className="flex-1 rounded-md bg-sky-700 px-2 py-1.5 text-sm font-semibold text-white hover:bg-sky-800 disabled:cursor-wait disabled:opacity-60"
                  onClick={() => void handleApplyProtection()}
                  disabled={actionBusy}
                >
                  {pendingAction === 'protect' ? 'Aplicando...' : 'Confirmar'}
                </button>
                <button
                  type="button"
                  className="rounded-md border border-sky-300 bg-white px-2 py-1.5 text-sm text-sky-800 hover:bg-sky-100 disabled:opacity-60"
                  onClick={() => setConfirmProtection(false)}
                  disabled={actionBusy}
                >
                  Cancelar
                </button>
              </div>
            </div>
          )}
        </section>
      )}

      {plot.unlocked && plot.seedId && !plot.isReady && fertilizerQuantity > 0 && <section className="mt-3 border-t border-lime-200 pt-3">
        <p className="text-xs text-emerald-700">🌿 Seu adubo: {fertilizerQuantity}</p>
        {(plot.fertilizedAt || fertilizedCycle === `${farmId}:${plotId}:${plot.currentHarvestCycle}:${plot.currentHarvestCycleStartedAt}`) && <p className="mt-1 text-xs text-amber-800">Este ciclo já foi adubado.</p>}
        {!confirmFertilizer ? <button type="button" disabled={!canOfferFertilizer || actionBusy || !!fertilizerAttempt.pending}
          onClick={() => setConfirmFertilizer(true)}
          className="mt-2 min-h-10 w-full rounded-lg border border-lime-400 bg-lime-50 px-3 py-2 text-sm font-semibold text-emerald-900 disabled:opacity-50">
          🌿 Adubar plantação
        </button> : <div className="mt-2 rounded-lg border border-lime-300 bg-lime-50 p-2">
          <p className="text-sm text-emerald-900">Usar 1 adubo {isVisiting ? 'na plantação do seu amigo' : 'neste lote'}?</p>
          <div className="mt-2 flex gap-2">
            <button type="button" disabled={actionBusy} onClick={() => void handleFertilize()}
              className="min-h-10 flex-1 rounded-lg bg-emerald-700 px-2 text-sm font-semibold text-white disabled:opacity-50">
              {pendingAction === 'fertilize' ? 'Adubando…' : 'Confirmar'}
            </button>
            <button type="button" disabled={actionBusy} onClick={() => setConfirmFertilizer(false)}
              className="min-h-10 rounded-lg border border-lime-300 bg-white px-2 text-sm text-emerald-900">Cancelar</button>
          </div>
        </div>}
      </section>}

      {fertilizerAttempt.pending && <div role="status" className="mt-2 rounded-lg border border-amber-300 bg-amber-50 p-2 text-sm text-amber-900">
        A última ação de adubo ainda precisa ser confirmada.
        <button type="button" disabled={actionBusy || fertilizerAttempt.busy} onClick={() => void handleFertilize(true)}
          className="mt-2 min-h-10 w-full rounded-lg bg-amber-700 px-2 font-semibold text-white disabled:opacity-50">Recuperar resultado</button>
      </div>}

      {fertilizerSyncPending && <button type="button" disabled={actionBusy}
        onClick={() => void retryFertilizerSync()}
        className="mt-2 min-h-10 w-full rounded-lg border border-amber-300 bg-amber-50 px-2 text-sm font-semibold text-amber-900 disabled:opacity-50">
        {pendingAction === 'fertilize' ? 'Atualizando…' : 'Atualizar plantação e estoque'}
      </button>}

      {feedback && (
        <div
          role={feedback.type === 'error' ? 'alert' : 'status'}
          aria-live="polite"
          className={`mt-2 rounded-lg border px-3 py-2 text-sm ${
            feedback.type === 'error'
              ? 'border-red-300 bg-red-100 text-red-700'
              : 'border-emerald-300 bg-emerald-50 text-emerald-800'
          }`}
        >
          {feedback.message}
        </div>
      )}
    </div>
  )
}
