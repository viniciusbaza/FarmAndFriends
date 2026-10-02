import assert from 'node:assert/strict'
import test from 'node:test'
import { animalGrowthDeadline, animalPresentation, animalSuccessMessage, productPresentation, productQuantity } from '../src/animals/animalPresentation.ts'
import { animalReason } from '../src/animals/animalMessages.ts'
import type { AnimalOperationEffect } from '../src/types/animal.ts'

const effect: AnimalOperationEffect = {
  action: 'collect', completedAt: '2026-09-17T12:00:00Z', farmId: 'farm', animalId: 'cow',
  cycleId: 'milk-cycle', animalTypeId: 'cow', productId: 'milk', quantity: 8,
  coinsSpent: 0, coinsGained: 0, xpGained: 25, ownerBonusAdded: 0,
}

test('cow and chicken purchases retain their distinct young names and arrival feedback', () => {
  assert.equal(animalPresentation('cow').youngName, 'Bezerra')
  assert.equal(animalPresentation('cow').buyLabel, 'Comprar bezerra')
  assert.equal(animalSuccessMessage({ ...effect, action: 'buy-animal' }),
    'Sua bezerra chegou e crescerá na fazenda.')
  assert.equal(animalPresentation('chicken').buyLabel, 'Comprar pintinho')
  assert.equal(animalSuccessMessage({ ...effect, action: 'buy-animal', animalTypeId: 'chicken' }),
    'Seu pintinho chegou e crescerá na fazenda.')
})

test('sheep arrives as a lamb, grows into a sheep, and wool has distinct feedback', () => {
  assert.equal(animalPresentation('sheep').youngName, 'Cordeira')
  assert.equal(animalPresentation('sheep').buyLabel, 'Comprar cordeira')
  assert.match(animalSuccessMessage({ ...effect, action: 'buy-animal', animalTypeId: 'sheep' })!, /Sua cordeira chegou/)
  assert.equal(productPresentation('wool').name, 'Lã')
  assert.equal(productPresentation('wool').actionName, 'lã')
  assert.equal(productQuantity('wool', 1), '1 lã')
  assert.equal(productQuantity('wool', 6), '6 lãs')
  assert.equal(animalSuccessMessage({ ...effect, productId: 'wool', quantity: 6 }),
    'Você tosquiou a ovelha e obteve 6 lãs. Um novo ciclo iniciou.')
  assert.equal(animalSuccessMessage({ ...effect, action: 'steal', productId: 'wool', quantity: 1 }),
    'Você roubou 1 lã.')
  assert.equal(animalSuccessMessage({ ...effect, action: 'sell-animal-product', productId: 'wool', quantity: 6, coinsGained: 540 }),
    'Você vendeu 6 lãs e recebeu 540 moedas.')
})

test('calf and chick countdowns use growth deadlines; adult countdown uses its species cycle', () => {
  const maturesAt = '2026-09-17T20:00:00Z'
  const readyAt = '2026-09-18T20:00:00Z'
  for (const stage of ['calf', 'chick', 'lamb'] as const) {
    assert.equal(animalGrowthDeadline({ stage, maturesAt, cycle: null }), maturesAt)
  }
  const cycle = { id: 'milk-cycle', productId: 'milk', startsAt: maturesAt, readyAt, isReady: false,
    baseYield: 8, protectedHelpBonus: 0, remainingBase: 8, ownerCollectible: 8,
    helperCount: 0, viewerHelped: false, helpers: null, helpersNextCursor: null }
  assert.equal(animalGrowthDeadline({ stage: 'adult', maturesAt, cycle }), readyAt)
  assert.equal(animalGrowthDeadline({ stage: 'adult', maturesAt, cycle: { ...cycle, isReady: true } }), null)
})

test('milk collection, theft and sale feedback uses milk quantities and server outcomes', () => {
  assert.equal(productPresentation('milk').name, 'Leite')
  assert.equal(productPresentation('milk').actionName, 'leite')
  assert.equal(productQuantity('milk', 1), '1 leite')
  assert.equal(productQuantity('milk', 8), '8 leites')
  assert.equal(animalSuccessMessage(effect), 'Você coletou 8 leites. Um novo ciclo iniciou.')
  assert.equal(animalSuccessMessage({ ...effect, action: 'steal', quantity: 1 }), 'Você roubou 1 leite.')
  assert.equal(animalSuccessMessage({ ...effect, action: 'sell-animal-product', coinsGained: 320 }), 'Você vendeu 8 leites e recebeu 320 moedas.')
  assert.equal(animalSuccessMessage({ ...effect, action: 'help', coinsGained: 2, xpGained: 5 }), 'Cuidado registrado! Você recebeu 2 moedas e 5 XP.')
})

test('animal theft reasons describe the exhausted limit and repeated theft', () => {
  assert.equal(animalReason('THEFT_LIMIT_REACHED'), 'A parte disponível para roubo já acabou.')
  assert.equal(animalReason('THEFT_ALREADY_REGISTERED'), 'Você já roubou deste animal neste ciclo.')
})

test('egg presentation remains distinct and an unknown product is never labelled eggs or milk', () => {
  assert.equal(productPresentation('egg').name, 'Ovos')
  assert.equal(productQuantity('egg', 1), '1 ovo')
  assert.equal(productQuantity('egg', 8), '8 ovos')
  assert.equal(productPresentation('unknown').name, 'Produto animal')
  assert.equal(productQuantity(null, 2), '2 produtos animais')
})

test('pig fertilizer is a collectable utility with its own names', () => {
  assert.equal(animalPresentation('pig').youngName, 'Leitão')
  assert.equal(animalPresentation('pig').buyLabel, 'Comprar leitão')
  assert.equal(productPresentation('fertilizer').name, 'Adubo')
  assert.equal(productQuantity('fertilizer', 2), '2 adubos')
  assert.equal(animalSuccessMessage({ ...effect, productId: 'fertilizer', quantity: 4 }),
    'Você coletou 4 adubos. Um novo ciclo iniciou.')
})
