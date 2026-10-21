import assert from 'node:assert/strict'
import test from 'node:test'
import {
  fertilizerEffectDescription,
  refreshFertilizerState,
} from '../src/fertilizer/fertilizerFeedback.ts'

const offer = {
  itemId: 'fertilizer', displayName: 'Adubo', pricePremiumCoins: 2,
  maxReductionSeconds: 3600, minRemainingSeconds: 0,
}

test('offer describes the configured reduction and immediate readiness', () => {
  const text = fertilizerEffectDescription(offer)
  assert.match(text, /Antecipa o crescimento em até 1 h/)
  assert.match(text, /tempo restante for menor ou igual a 1 h.*pronta imediatamente/)
  assert.doesNotMatch(text, /25%|pelo menos|0 s/)
  assert.match(fertilizerEffectDescription({ ...offer, maxReductionSeconds: 1800 }), /até 30 min/)
})

test('refresh confirms snapshots without modifying server readiness', async () => {
  const farm = { plots: [{ isReady: true, readyAt: '2026-09-26T12:00:00Z' }] }
  const inventory = { items: [{ itemId: 'fertilizer', quantity: 3 }] }
  assert.deepEqual(await refreshFertilizerState(async () => farm, async () => inventory), {
    farmUpdated: true, inventoryUpdated: true,
  })
  assert.equal(farm.plots[0].readyAt, '2026-09-26T12:00:00Z')
})

test('swallowed farm read failure is reported without inventing a ready state', async () => {
  assert.deepEqual(await refreshFertilizerState(async () => undefined, async () => ({ items: [] })), {
    farmUpdated: false, inventoryUpdated: true,
  })
})

test('an inventory read failure retains a retry even when the farm updated', async () => {
  assert.deepEqual(await refreshFertilizerState(async () => ({ plots: [] }), async () => {
    throw new Error('network unavailable')
  }), { farmUpdated: true, inventoryUpdated: false })
})

test('retry repeats only reads and confirms snapshots after a transient failure', async () => {
  let farmReads = 0
  let inventoryReads = 0
  const refreshFarm = async () => ++farmReads === 1 ? undefined : { plots: [] }
  const refreshInventory = async () => { inventoryReads++; return { items: [] } }
  assert.equal((await refreshFertilizerState(refreshFarm, refreshInventory)).farmUpdated, false)
  assert.equal((await refreshFertilizerState(refreshFarm, refreshInventory)).farmUpdated, true)
  assert.equal(farmReads, 2)
  assert.equal(inventoryReads, 2)
})
