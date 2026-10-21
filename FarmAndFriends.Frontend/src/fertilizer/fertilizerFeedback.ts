import type { FertilizerOffer } from './fertilizerApi'

export function fertilizerEffectDescription(offer: FertilizerOffer) {
  const minutes = Math.round(offer.maxReductionSeconds / 60)
  const limit = minutes % 60 === 0 ? `${minutes / 60} h` : `${minutes} min`
  return `Antecipa o crescimento em até ${limit}. Se o tempo restante for menor ou igual a ${limit}, a plantação fica pronta imediatamente.`
}

/** Only refreshed snapshots confirm the current state; mutation replays may describe an older cycle. */
export async function refreshFertilizerState(
  refreshFarm: () => Promise<unknown>,
  refreshInventory: () => Promise<unknown>,
) {
  const [farm, inventory] = await Promise.allSettled([refreshFarm(), refreshInventory()])
  return {
    farmUpdated: farm.status === 'fulfilled' && Boolean(farm.value),
    inventoryUpdated: inventory.status === 'fulfilled' && Boolean(inventory.value),
  }
}
