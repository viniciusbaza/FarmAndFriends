import type { AnimalDto, AnimalOperationEffect } from '../types/animal'

/** Names and icons only; prices, yields, deadlines and permissions come from the API. */
export function animalPresentation(animalTypeId: string) {
  switch (animalTypeId) {
    case 'chicken': return { icon: '🐔', youngIcon: '🐥', youngName: 'Pintinho', buyLabel: 'Comprar pintinho', arrival: 'Seu pintinho chegou' }
    case 'cow': return { icon: '🐄', youngIcon: '🐮', youngName: 'Bezerra', buyLabel: 'Comprar bezerra', arrival: 'Sua bezerra chegou' }
    case 'sheep': return { icon: '🐑', youngIcon: '🐑', youngName: 'Cordeira', buyLabel: 'Comprar cordeira', arrival: 'Sua cordeira chegou' }
    case 'pig': return { icon: '🐷', youngIcon: '🐽', youngName: 'Leitão', buyLabel: 'Comprar leitão', arrival: 'Seu leitão chegou' }
    default: return { icon: '🐾', youngIcon: '🐾', youngName: 'Filhote', buyLabel: 'Comprar filhote', arrival: 'Seu filhote chegou' }
  }
}

export function productPresentation(productId: string | null) {
  switch (productId) {
    case 'egg': return { icon: '🥚', name: 'Ovos', actionName: 'ovos' }
    case 'milk': return { icon: '🥛', name: 'Leite', actionName: 'leite' }
    case 'wool': return { icon: '🧶', name: 'Lã', actionName: 'lã' }
    case 'fertilizer': return { icon: '🌿', name: 'Adubo', actionName: 'adubo' }
    default: return { icon: '🧺', name: 'Produto animal', actionName: 'produção' }
  }
}

export function productQuantity(productId: string | null, quantity: number) {
  if (productId === 'egg') return `${quantity} ${quantity === 1 ? 'ovo' : 'ovos'}`
  if (productId === 'milk') return `${quantity} ${quantity === 1 ? 'leite' : 'leites'}`
  if (productId === 'wool') return `${quantity} ${quantity === 1 ? 'lã' : 'lãs'}`
  if (productId === 'fertilizer') return `${quantity} ${quantity === 1 ? 'adubo' : 'adubos'}`
  return `${quantity} ${quantity === 1 ? 'produto animal' : 'produtos animais'}`
}

export function animalGrowthDeadline(animal: Pick<AnimalDto, 'stage' | 'maturesAt' | 'cycle'>) {
  return animal.stage !== 'adult' ? animal.maturesAt : !animal.cycle?.isReady ? animal.cycle?.readyAt : null
}

export function animalSuccessMessage(effect: AnimalOperationEffect) {
  const amount = productQuantity(effect.productId, effect.quantity)
  switch (effect.action) {
    case 'buy-animal-enclosure': return 'Seu cercado está pronto! Agora você pode comprar animais.'
    case 'buy-animal': return `${animalPresentation(effect.animalTypeId ?? '').arrival} e crescerá na fazenda.`
    case 'collect': return effect.productId === 'wool'
      ? `Você tosquiou a ovelha e obteve ${amount}. Um novo ciclo iniciou.`
      : `Você coletou ${amount}. Um novo ciclo iniciou.`
    case 'help': return `Cuidado registrado! Você recebeu ${effect.coinsGained} moedas e ${effect.xpGained} XP.`
    case 'steal': return `Você roubou ${amount}.`
    case 'sell-animal-product': return `Você vendeu ${amount} e recebeu ${effect.coinsGained} moedas.`
  }
}
