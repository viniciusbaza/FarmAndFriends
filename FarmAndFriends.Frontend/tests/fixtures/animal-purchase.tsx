import { StrictMode, useEffect, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { FarmProvider } from '../../src/farm/FarmContext'
import { useFarm } from '../../src/farm/useFarmContext'
import { UserContext } from '../../src/user/UserContext'
import { AnimalProvider } from '../../src/animals/AnimalProvider'
import { AnimalShopContent } from '../../src/animals/AnimalShopContent'
import { useAnimals } from '../../src/animals/useAnimals'
import { PhaserGame } from '../../src/game/PhaserGame'
import type { Farm, Plot } from '../../src/types/Farm'
import type { AnimalDto, AnimalOperationResponse } from '../../src/types/animal'

// Deterministic HTTP fixture, using the production providers, shop cards and scene.
const names = ['Galinha', 'Vaca', 'Ovelha', 'Porco', 'Pavão']
const species = ['chicken', 'cow', 'sheep', 'pig', 'peacock']
const products = ['egg', 'milk', 'wool', 'fertilizer', 'feather']
const date = new Date().toISOString()
const later = new Date(Date.now() + 100 * 60 * 60 * 1000).toISOString()
const permissions = { canCollect: false, canHelp: false, canSteal: false,
  collectReason: 'NOT_READY', helpReason: 'SELF_ACTION', stealReason: 'SELF_ACTION',
  rewardAvailable: false, coinsReward: 0, xpReward: 0, nextRewardAt: null }
const animals: AnimalDto[] = species.slice(0, 4).map((animalTypeId, i) => ({
  id: animalTypeId, animalTypeId, displayName: names[i], purchasedAt: date, maturesAt: date,
  stage: 'adult', nextStateChangeAt: null, permissions,
  cycle: { id: `${animalTypeId}-cycle`, productId: products[i], startsAt: date,
    readyAt: later, isReady: false, baseYield: 6, protectedHelpBonus: 0,
    remainingBase: 6, ownerCollectible: 6, helperCount: 0, viewerHelped: false,
    helpers: [], helpersNextCursor: null },
}))
let farm: Farm = { id: 'purchase-farm', name: 'Fazenda de teste', ownerUserId: 'owner', ownerUsername: 'Teste',
  nextPestCheckAt: null, landOffer: null,
  plots: Array.from({ length: 28 }, (_, i): Plot => ({ id: `plot-${i}`, x: i % 7, y: Math.floor(i / 7),
    unlocked: true, currentHarvestCycle: null, currentHarvestCycleStartedAt: null,
    isReady: false, remainingYield: null, state: 'empty', protectedUntil: null, pest: null })),
  animalState: { farmId: 'purchase-farm', revision: 4, observedAt: date,
    enclosure: { id: 'enclosure', slot: 'animal-enclosure', animals } },
}
const nativeFetch = window.fetch.bind(window)
window.fetch = async (input, init) => {
  const path = new URL(String(input), window.location.href).pathname
  if (path === '/farms/my') return Response.json(farm)
  if (path === '/shop/animal-enclosure') return Response.json({ priceCoins: 1000, minLevel: 2,
    currentLevel: 10, firstExpansionReached: true, initialPlotsUnlocked: 9, requiredInitialPlots: 9,
    owned: true, canBuy: false, reason: 'ALREADY_OWNED' })
  if (path === '/shop/animals') return Response.json(species.map((animalTypeId, i) => ({
    animalTypeId, displayName: names[i], priceCoins: 2500, minLevel: 8,
    growthDuration: 28800, productionDuration: 259200,
    owned: farm.animalState!.enclosure!.animals.some(animal => animal.animalTypeId === animalTypeId),
    canBuy: animalTypeId === 'peacock' && farm.animalState!.revision === 4, reason: null,
  })))
  if (path === '/shop/animal-products') return Response.json([{ itemId: 'feather', displayName: 'Penas', sellPriceCoins: 150 }])
  if (path === '/shop/buy-animal' && init?.method === 'POST') {
    const { animalTypeId } = JSON.parse(String(init.body)) as { animalTypeId: string }
    if (animalTypeId !== 'peacock') return Response.json({ detail: 'Somente pavão disponível na fixture.' }, { status: 409 })
    const peacock: AnimalDto = { id: 'peacock', animalTypeId: 'peacock', displayName: 'Pavão',
      purchasedAt: date, maturesAt: later, stage: 'peachick', cycle: null, permissions, nextStateChangeAt: later }
    farm = { ...farm, animalState: { ...farm.animalState!, revision: 5,
      enclosure: { ...farm.animalState!.enclosure!, animals: [...animals, peacock] } } }
    const result: AnimalOperationResponse = { operationId: 'purchase-operation', replayed: false,
      current: farm.animalState!, effect: { action: 'buy-animal', completedAt: date, farmId: farm.id,
        animalId: 'peacock', cycleId: null, animalTypeId: 'peacock', productId: null,
        quantity: 0, coinsSpent: 2500, coinsGained: 0, xpGained: 0, ownerBonusAdded: 0 } }
    return Response.json(result)
  }
  return nativeFetch(input, init)
}
localStorage.setItem('access_token', 'fixture-session')

export function PurchaseFixture() {
  const { farm: currentFarm } = useFarm()
  const { selected, close } = useAnimals()
  const [shop, setShop] = useState(true)
  const [syncCount, setSyncCount] = useState(0)
  const [ready, setReady] = useState(0)
  useEffect(() => {
    const synchronized = () => setSyncCount(count => count + 1)
    const sceneReady = () => setReady(count => count + 1)
    window.addEventListener('farm:sync', synchronized)
    window.addEventListener('farm:scene:ready', sceneReady)
    return () => {
      window.removeEventListener('farm:sync', synchronized)
      window.removeEventListener('farm:scene:ready', sceneReady)
    }
  }, [])
  useEffect(() => {
    window.dispatchEvent(new CustomEvent('ui:modal', { detail: { source: 'purchase-fixture', open: shop } }))
    return () => window.dispatchEvent(new CustomEvent('ui:modal', { detail: { source: 'purchase-fixture', open: false } }))
  }, [shop])
  return <div style={{ position: 'relative', width: '100vw', height: '100vh' }}>
    {currentFarm && <PhaserGame farm={currentFarm} cameraMode="overview" isVisiting={false} />}
    <aside style={{ position: 'fixed', zIndex: 100, background: 'white', maxHeight: '80vh', overflow: 'auto' }}>
      <output id="react-animals">{currentFarm?.animalState?.enclosure?.animals.length ?? 0}</output>{' animais React; '}
      <output id="sync-count">{syncCount}</output>{' sync; '}
      <output id="scene-ready">{ready}</output>{' ready; '}
      <output id="selected-animal">{selected?.animalId ?? 'nenhum'}</output>
      <button onClick={() => { setShop(value => !value); close() }}>{shop ? 'Fechar loja' : 'Abrir loja'}</button>
      {shop && <AnimalShopContent />}
    </aside>
  </div>
}

createRoot(document.getElementById('root')!).render(<StrictMode>
  <UserContext.Provider value={{ user: { id: 'owner', username: 'Teste', farmId: farm.id,
    farmName: farm.name, level: 10, currentXp: 0, xpToNextLevel: 100 }, loading: false,
    economyPending: false, refreshUser: async () => undefined,
    saveProfile: async () => ({ avatarId: 'cow', farmId: farm.id, farmName: farm.name }) }}>
    <FarmProvider><AnimalProvider><PurchaseFixture /></AnimalProvider></FarmProvider>
  </UserContext.Provider>
</StrictMode>)
