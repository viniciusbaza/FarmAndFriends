import assert from 'node:assert/strict'
import test from 'node:test'
import { fertilizerStock, parseFertilizerAttempt, sellableAnimalProducts } from '../src/fertilizer/fertilizerState.ts'

const key = '11111111-1111-4111-8111-111111111111'

test('adubo stays in inventory but never enters animal sale rows', () => {
  const items = [
    { itemType: 'AnimalProduct' as const, itemId: 'fertilizer', quantity: 4 },
    { itemType: 'AnimalProduct' as const, itemId: 'egg', quantity: 2 },
    { itemType: 'Crop' as const, itemId: 'carrot', quantity: 3 },
  ]
  assert.equal(fertilizerStock(items), 4)
  assert.deepEqual(sellableAnimalProducts(items).map(item => item.itemId), ['egg'])
})

test('a pending application retains its original cycle and key for recovery', () => {
  const attempt = { action: 'fertilize', farmId: 'farm', plotId: 'plot',
    expectedHarvestCycle: 2, expectedCycleStartedAt: '2026-09-24T12:00:00Z', key }
  assert.deepEqual(parseFertilizerAttempt(JSON.stringify(attempt)), attempt)
  assert.equal(parseFertilizerAttempt(JSON.stringify({ ...attempt, expectedHarvestCycle: null })), null)
  assert.equal(parseFertilizerAttempt('{bad'), null)
})
