import Phaser from 'phaser'
import FarmScene from '../../src/game/scenes/FarmScene'
import type { Farm } from '../../src/types/Farm'
import type { AnimalDto } from '../../src/types/animal'
import { calculateFarmPlotLayout } from '../../src/game/farmLayout'
import { calculateAnimalLayout } from '../../src/game/animalLayout'

let allUnlocked = false
let stage = 'ready'
let cowStage = 'ready'
let sheepStage = 'ready'
let pigStage = 'ready'
let pigAnimation = 'live'
let selectedSpecies = 'pig'
let visiting = false
const nativeMatchMedia = window.matchMedia.bind(window)
let reducedMotion = nativeMatchMedia('(prefers-reduced-motion: reduce)').matches
// Fixture-only media emulation; the real scene still reads its normal media query.
window.matchMedia = query => {
  const result = nativeMatchMedia(query)
  if (query === '(prefers-reduced-motion: reduce)') Object.defineProperty(result, 'matches', { value: reducedMotion })
  return result
}
let revision = 1
let cycleId = 'cycle-1'
let cowCycleId = 'cow-cycle-1'
let sheepCycleId = 'sheep-cycle-1'
let pigCycleId = 'pig-cycle-1'
let overview = true
let volumes: Phaser.GameObjects.GameObject[] = []
const permissions = { canCollect: true, canHelp: true, canSteal: true, collectReason: null,
  helpReason: null, stealReason: null, rewardAvailable: true, coinsReward: 2, xpReward: 1, nextRewardAt: null }
function snapshot(): Farm {
  const animal: AnimalDto = { id: 'hen', animalTypeId: 'chicken', displayName: 'Galinha',
    purchasedAt: '2026-09-01T00:00:00Z', maturesAt: '2026-09-01T01:00:00Z', stage: stage === 'chick' ? 'chick' : 'adult',
    nextStateChangeAt: null, permissions,
    cycle: stage === 'chick' ? null : { id: cycleId, productId: 'egg', startsAt: '2026-09-01T01:00:00Z',
      readyAt: '2026-09-01T02:00:00Z', isReady: stage === 'ready', baseYield: 10,
      protectedHelpBonus: 2, remainingBase: 8, ownerCollectible: 10, helperCount: 2,
      viewerHelped: false, helpers: [], helpersNextCursor: null } }
  const cow: AnimalDto = { ...animal, id: 'cow', animalTypeId: 'cow', displayName: 'Vaca',
    stage: cowStage === 'calf' ? 'calf' : 'adult',
    cycle: cowStage === 'calf' ? null : { ...animal.cycle!, id: cowCycleId, productId: 'milk',
      startsAt: '2026-09-01T01:00:00Z', readyAt: '2026-09-02T01:00:00Z',
      baseYield: 8, protectedHelpBonus: 2, remainingBase: 6, ownerCollectible: 8,
      helperCount: 2, viewerHelped: false, helpers: [], helpersNextCursor: null,
      isReady: cowStage === 'ready' } }
  const sheep: AnimalDto = { ...animal, id: 'sheep', animalTypeId: 'sheep', displayName: 'Ovelha',
    stage: sheepStage === 'lamb' ? 'lamb' : 'adult',
    cycle: sheepStage === 'lamb' ? null : { ...animal.cycle!, id: sheepCycleId, productId: 'wool',
      startsAt: '2026-09-01T01:00:00Z', readyAt: '2026-09-03T01:00:00Z',
      baseYield: 6, protectedHelpBonus: 2, remainingBase: 4, ownerCollectible: 6,
      helperCount: 2, viewerHelped: false, helpers: [], helpersNextCursor: null,
      isReady: sheepStage === 'ready' } }
  const pig: AnimalDto = { ...animal, id: 'pig', animalTypeId: 'pig', displayName: 'Porco',
    stage: pigStage === 'piglet' ? 'piglet' : 'adult',
    permissions: { ...permissions, canSteal: false, stealReason: 'Adubo não pode ser roubado' },
    cycle: pigStage === 'piglet' ? null : { ...animal.cycle!, id: pigCycleId, productId: 'fertilizer',
      startsAt: '2026-09-01T01:00:00Z', readyAt: '2026-09-04T01:00:00Z',
      baseYield: 4, protectedHelpBonus: 1, remainingBase: 4, ownerCollectible: 4,
      helperCount: 0, viewerHelped: false, helpers: [], helpersNextCursor: null,
      isReady: pigStage === 'ready' } }
  return { id: 'fixture-farm', name: 'Prova visual', ownerUserId: 'fixture', ownerUsername: 'Fazendeiro',
    nextPestCheckAt: null, landOffer: allUnlocked ? null : { plotId: 'plot-3-0', plotNumber: 10, maxPlots: 28,
      minLevel: 1, prices: { coins: 100, premiumCoins: 1 }, expandsTo: null },
    animalState: { farmId: 'fixture-farm', revision, observedAt: new Date().toISOString(),
      enclosure: stage === 'none' ? null : { id: 'enclosure', slot: 'upper-right',
        animals: stage === 'empty' ? [] : [animal, ...(cowStage === 'none' ? [] : [cow]),
          ...(sheepStage === 'none' ? [] : [sheep]), ...(pigStage === 'none' ? [] : [pig])] } },
    plots: Array.from({ length: 28 }, (_, i) => {
      const x = i % 7; const y = Math.floor(i / 7); const unlocked = allUnlocked || x < 3 && y < 3
      return { id: `plot-${x}-${y}`, x, y, unlocked, seedId: unlocked ? (x === 0 ? 'apple_tree' : 'carrot') : null,
        plantedAt: unlocked ? '2026-09-01T00:00:00Z' : null, currentHarvestCycle: unlocked ? 1 : null,
        isReady: unlocked, readyAt: unlocked ? '2026-09-01T01:00:00Z' : null, remainingYield: unlocked ? 10 : null,
        state: unlocked ? 'ready' : 'empty', protectedUntil: null, pest: null }
    }) }
}
const game = new Phaser.Game({ type: Phaser.AUTO, parent: 'world', backgroundColor: '#dff5ff',
  scale: { mode: Phaser.Scale.RESIZE, width: '100%', height: '100%' }, scene: [] })
game.registry.set('farmCameraMode', 'overview')
game.scene.add('FarmScene', FarmScene, true, { farm: snapshot(), isVisiting: false })
function sceneData() {
  return { farm: snapshot(), isVisiting: visiting,
    modalSources: document.getElementById('modal')!.hidden ? [] : ['fixture'] }
}
document.getElementById('motion')!.textContent = reducedMotion ? 'Movimento reduzido' : 'Movimento normal'
// Public Phaser loop control is restricted to this QA fixture. Zero delta
// freezes timers/tweens; game.step also renders the selected animation frame.
// A/B have the same patrol offset and orientation despite opposite stride parity.
function renderSelectedPigFrame() {
  if (pigAnimation === 'live') return
  game.loop.sleep()
  game.step(pigAnimation === 'idle' ? 0 : pigAnimation === 'walk-a' ? 24_000 : 6_000, 0)
}
function sync() {
  revision++
  window.dispatchEvent(new CustomEvent('farm:sync', { detail: snapshot() }))
  renderSelectedPigFrame()
}
function button(id: string, action: () => void) { document.getElementById(id)!.addEventListener('click', action) }
document.getElementById('stage')!.addEventListener('change', event => { stage = (event.target as HTMLSelectElement).value; sync() })
document.getElementById('cow-stage')!.addEventListener('change', event => { cowStage = (event.target as HTMLSelectElement).value; sync() })
document.getElementById('sheep-stage')!.addEventListener('change', event => { sheepStage = (event.target as HTMLSelectElement).value; sync() })
document.getElementById('pig-stage')!.addEventListener('change', event => { pigStage = (event.target as HTMLSelectElement).value; sync() })
document.getElementById('pig-animation')!.addEventListener('change', event => {
  pigAnimation = (event.target as HTMLSelectElement).value
  if (pigAnimation === 'live') game.loop.wake()
  else renderSelectedPigFrame()
})
document.getElementById('species')!.addEventListener('change', event => { selectedSpecies = (event.target as HTMLSelectElement).value })
button('visit', () => { visiting = !visiting; document.getElementById('visit')!.textContent = visiting ? 'Visita' : 'Própria';
  volumes = []; game.scene.start('FarmScene', sceneData()); renderSelectedPigFrame() })
button('motion', () => { reducedMotion = !reducedMotion;
  document.getElementById('motion')!.textContent = reducedMotion ? 'Movimento reduzido' : 'Movimento normal';
  volumes = []; game.scene.remove('FarmScene');
  game.scene.add('FarmScene', FarmScene, true, sceneData()); renderSelectedPigFrame() })
button('plots', () => { allUnlocked = !allUnlocked; document.getElementById('plots')!.textContent = allUnlocked ? '28 lotes' : '9 lotes'; sync() })
button('view', () => { overview = !overview; const mode = overview ? 'overview' : 'focus'; game.registry.set('farmCameraMode', mode);
  document.getElementById('view')!.textContent = mode; window.dispatchEvent(new CustomEvent('farm:camera:mode', { detail: { mode } })) })
button('sync', () => {
  const scene = game.scene.getScene('FarmScene')
  const before = scene.children.length
  for (let i = 0; i < 5; i++) sync()
  document.getElementById('status')!.textContent = `Objetos: ${before} → ${scene.children.length}`
})
button('restart', () => { volumes = []; game.scene.start('FarmScene', sceneData()); renderSelectedPigFrame() })
button('volumes', () => {
  if (volumes.length) { volumes.forEach(object => object.destroy()); volumes = []; return }
  const scene = game.scene.getScene('FarmScene')
  const layout = calculateAnimalLayout(calculateFarmPlotLayout(snapshot().plots).bounds)
  for (const [i, volume] of layout.reserved.entries()) {
    volumes.push(scene.add.rectangle(volume.x, volume.y - volume.height / 2, volume.width, volume.height, 0x55bbff, 0.3).setDepth(800))
    volumes.push(scene.add.text(volume.x, volume.y - volume.height, ['Galinha', 'Vaca', 'Ovelha', 'Porco'][i], {
      fontSize: '9px', color: '#142a31', backgroundColor: '#ffffff',
    }).setOrigin(0.5, 1).setDepth(801))
  }
})
function modal(open: boolean) { document.getElementById('modal')!.hidden = !open;
  window.dispatchEvent(new CustomEvent('ui:modal', { detail: { source: 'fixture', open } })) }
button('overlay', () => modal(true)); button('close', () => modal(false))
function done(action: 'help' | 'collect' | 'steal') {
  if (action === 'steal' && selectedSpecies === 'pig') {
    document.getElementById('status')!.textContent = 'Adubo não pode ser roubado'
    return
  }
  const cow = selectedSpecies === 'cow'
  const sheep = selectedSpecies === 'sheep'
  const pig = selectedSpecies === 'pig'
  const historical = pig ? pigCycleId : sheep ? sheepCycleId : cow ? cowCycleId : cycleId
  if (action === 'collect') {
    if (pig) { pigCycleId = `pig-cycle-${++revision}`; pigStage = 'producing';
      (document.getElementById('pig-stage') as HTMLSelectElement).value = pigStage }
    else if (sheep) { sheepCycleId = `sheep-cycle-${++revision}`; sheepStage = 'producing';
      (document.getElementById('sheep-stage') as HTMLSelectElement).value = sheepStage }
    else if (cow) { cowCycleId = `cow-cycle-${++revision}`; cowStage = 'producing';
      (document.getElementById('cow-stage') as HTMLSelectElement).value = cowStage }
    else { cycleId = `cycle-${++revision}`; stage = 'producing';
      (document.getElementById('stage') as HTMLSelectElement).value = stage }
    sync()
  }
  window.dispatchEvent(new CustomEvent(`animal:${action}:done`, { detail: { operationId: crypto.randomUUID(), action,
    completedAt: new Date().toISOString(), farmId: 'fixture-farm', animalId: pig ? 'pig' : sheep ? 'sheep' : cow ? 'cow' : 'hen', cycleId: historical,
    animalTypeId: pig ? 'pig' : sheep ? 'sheep' : cow ? 'cow' : 'chicken', productId: pig ? 'fertilizer' : sheep ? 'wool' : cow ? 'milk' : 'egg', quantity: action === 'steal' ? 1 : pig ? 4 : 8,
    coinsSpent: 0, coinsGained: action === 'help' ? 2 : 0, xpGained: action === 'steal' ? 0 : 5, ownerBonusAdded: 1 } }))
}
button('help', () => done('help')); button('collect', () => done('collect'))
button('steal', () => done('steal'))
for (const name of ['animal:select', 'plot:click']) window.addEventListener(name, event => {
  const detail = (event as CustomEvent<{ animalId?: string }>).detail
  document.getElementById('status')!.textContent = `${name}${detail?.animalId ? `: ${detail.animalId}` : ''}`
})
