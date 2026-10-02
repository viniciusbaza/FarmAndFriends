import assert from 'node:assert/strict'
import test from 'node:test'
import { acceptAnimalState, nextAnimalStateAt, patchAnimalFarm, shouldRefreshAnimalFarm } from '../src/animals/animalState.ts'
import { attemptStorageKey, parseAnimalAttempt, prepareAnimalAttempt } from '../src/animals/animalAttempt.ts'
import type { AnimalStateEnvelope } from '../src/types/animal.ts'
import type { Farm } from '../src/types/Farm.ts'
import { AuthoritativeReadGate } from '../src/user/authoritativeRead.ts'

const envelope = (revision: number, observedAt: string): AnimalStateEnvelope => ({ farmId: 'farm-a', revision, observedAt, enclosure: null })
test('animal snapshots prioritize farm, revision, then server observation; same revision can become ready', () => {
  const old = envelope(4, '2026-09-12T12:00:00Z')
  assert.equal(acceptAnimalState(old, envelope(4, '2026-09-12T12:01:00Z')), true)
  assert.equal(acceptAnimalState(old, envelope(3, '2026-09-12T13:00:00Z')), false)
  assert.equal(acceptAnimalState(old, envelope(4, '2026-09-12T11:59:00Z')), false)
  assert.equal(acceptAnimalState(old, { ...envelope(9, old.observedAt), farmId: 'farm-b' }), false)
  assert.equal(acceptAnimalState(null, old), true)
})

test('unknown command result persists exact sale fingerprint across reload and blocks a new intention', () => {
  const attempt = prepareAnimalAttempt(null, { action: 'sell-animal-product', farmId: 'farm-a', itemId: 'egg', quantity: 3 }, () => '11111111-1111-4111-8111-111111111111')
  const recovered = parseAnimalAttempt(JSON.stringify(attempt))
  assert.deepEqual(recovered, attempt)
  assert.throws(() => prepareAnimalAttempt(recovered, { action: 'sell-animal-product', farmId: 'farm-a', itemId: 'egg', quantity: 4 }, () => 'new'), /Recupere/)
  assert.notEqual(attemptStorageKey('alice'), attemptStorageKey('bob'))
  assert.equal(parseAnimalAttempt('invalid'), null)
  assert.equal(parseAnimalAttempt(JSON.stringify({ ...attempt, quantity: -1 })), null)
})

test('no animal timer grants state and an empty enclosure has no deadline', () => {
  assert.equal(nextAnimalStateAt(null), null)
  assert.equal(nextAnimalStateAt(envelope(0, '2026-09-12T12:00:00Z')), null)
})

test('legacy reward deadline only schedules an authoritative refresh during a mixed rollout', () => {
  const state = {
    ...envelope(1, '2026-09-12T12:00:00Z'),
    enclosure: {
      id: 'enclosure-a', slot: 'animal-enclosure', animals: [{
        id: 'animal-a', animalTypeId: 'chicken', displayName: 'Galinha',
        purchasedAt: '2026-09-12T10:00:00Z', maturesAt: '2026-09-12T11:00:00Z', stage: 'adult',
        cycle: null, nextStateChangeAt: null,
        permissions: {
          canCollect: false, canHelp: false, canSteal: false,
          collectReason: 'NOT_READY', helpReason: 'HELP_ALREADY_REGISTERED', stealReason: 'NOT_READY',
          rewardAvailable: false, coinsReward: 2, xpReward: 5,
          nextRewardAt: '2026-09-13T12:00:00Z',
        },
      }],
    },
  } as AnimalStateEnvelope
  assert.equal(nextAnimalStateAt(state), Date.parse('2026-09-13T12:00:00Z'))
})

test('delayed command from A preserves active B GET and can update only the own-farm A cache', () => {
  const makeFarm = (id: string): Farm => ({ id, name: id, ownerUserId: id, ownerUsername: id, nextPestCheckAt: null, landOffer: null, plots: [], animalState: { ...envelope(0, '2026-09-12T12:00:00Z'), farmId: id } })
  const farmA = makeFarm('farm-a')
  const farmB = makeFarm('farm-b')
  const read = new AuthoritativeReadGate()
  const inFlightB = read.begin(1)
  const confirmedA = envelope(3, '2026-09-12T12:01:00Z')
  const nextB = patchAnimalFarm(farmB, confirmedA)
  if (nextB !== farmB) read.invalidate()
  assert.equal(nextB, farmB)
  assert.equal(read.accepts(inFlightB, 1), true)
  assert.equal(shouldRefreshAnimalFarm('farm-b', confirmedA.farmId), false)
  assert.equal(shouldRefreshAnimalFarm(undefined, confirmedA.farmId), false)
  assert.equal(patchAnimalFarm(farmA, confirmedA)?.animalState?.revision, 3)
  assert.equal(patchAnimalFarm(null, confirmedA), null)
})
