import assert from 'node:assert/strict'
import test from 'node:test'
import { FarmSceneSync } from '../src/farm/farmSceneSync.ts'
import type { Farm } from '../src/types/Farm.ts'

const farm = (id: string): Farm => ({ id, name: id, ownerUserId: id, ownerUsername: id,
  nextPestCheckAt: null, landOffer: null, plots: [], animalState: {
    farmId: id, revision: 4, observedAt: '2026-10-05T00:00:00Z',
    enclosure: { id: 'enclosure', slot: 'animal-enclosure', animals: [] },
  } })

function purchasedPeacock(current: Farm): Farm {
  return { ...current, animalState: { ...current.animalState!, revision: 5,
    enclosure: { ...current.animalState!.enclosure!, animals: [{
      id: 'peacock', animalTypeId: 'peacock', displayName: 'Pavão', stage: 'peachick',
      purchasedAt: '2026-10-05T00:00:00Z', maturesAt: '2026-10-05T08:00:00Z',
      cycle: null, nextStateChangeAt: '2026-10-05T08:00:00Z',
      permissions: { canHelp: false, canCollect: false, canSteal: false,
        helpReason: 'NOT_PRODUCING', collectReason: 'NOT_READY', stealReason: 'NOT_READY',
        rewardAvailable: false, coinsReward: 0, xpReward: 0, nextRewardAt: null },
    }] },
  } }
}

test('purchase accepted during preload replays the latest peacock snapshot when the scene is ready', () => {
  const sent: Farm[] = []
  const bridge = new FarmSceneSync(snapshot => sent.push(snapshot))
  const before = farm('own')
  const bought = purchasedPeacock(before)
  bridge.update(before, false)
  bridge.recordAccepted(bought)
  bridge.update(before, false) // React has not rendered the accepted snapshot yet.
  assert.equal(sent.length, 0)
  bridge.sceneReady('own', false)
  assert.deepEqual(sent, [bought])
  assert.equal(sent[0].animalState?.enclosure?.animals[0].stage, 'peachick')
  bridge.update(bought, false)
  assert.deepEqual(sent, [bought])
})

test('a normal confirmed purchase has one useFarm event and no additional bridge event', () => {
  const events = new EventTarget()
  const snapshots: Farm[] = []
  events.addEventListener('farm:sync', event => snapshots.push((event as CustomEvent<Farm>).detail))
  const bridge = new FarmSceneSync(snapshot => events.dispatchEvent(new CustomEvent('farm:sync', { detail: snapshot })))
  events.addEventListener('farm:sync', event => bridge.recordAccepted((event as CustomEvent<Farm>).detail))
  const before = farm('own')
  bridge.update(before, false)
  bridge.sceneReady('own', false)
  const bought = purchasedPeacock(before)
  events.dispatchEvent(new CustomEvent('farm:sync', { detail: bought }))
  bridge.update(bought, false)
  bridge.update(bought, false)
  assert.deepEqual(snapshots, [before, bought])
})

test('scene restart reapplies the latest state and retains updates received during restart', () => {
  const sent: Farm[] = []
  const bridge = new FarmSceneSync(snapshot => sent.push(snapshot))
  const before = farm('own')
  bridge.update(before, false)
  bridge.sceneReady('own', false)
  const bought = purchasedPeacock(before)
  bridge.recordAccepted(bought)
  bridge.update(bought, false)
  assert.deepEqual(sent, [before])
  bridge.sceneReady('own', false)
  bridge.sceneReady('own', false)
  assert.deepEqual(sent, [before, bought, bought])
})

test('an old ready event cannot synchronize another farm or owner state into a visiting scene', () => {
  const sent: Farm[] = []
  const bridge = new FarmSceneSync(snapshot => sent.push(snapshot))
  bridge.update(farm('own'), false)
  const friend = farm('friend')
  bridge.update(friend, true)
  bridge.sceneReady('own', false)
  bridge.sceneReady('friend', false)
  assert.equal(sent.length, 0)
  bridge.sceneReady('friend', true)
  assert.deepEqual(sent, [friend])
  bridge.sceneReady('own', false)
  const refreshedFriend = { ...friend, name: 'Nome atualizado' }
  bridge.recordAccepted(refreshedFriend)
  bridge.update(refreshedFriend, true)
  assert.deepEqual(sent, [friend])
  bridge.sceneReady('friend', true)
  assert.deepEqual(sent, [friend, refreshedFriend])
})
