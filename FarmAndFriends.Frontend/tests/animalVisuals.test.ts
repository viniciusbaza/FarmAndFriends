import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import {
  matchesAnimalFeedback,
  resolveAnimalSemanticScale,
  resolveChickenMotion,
  resolveCowMotion,
  resolveSheepMotion,
  resolvePigMotion,
  resolvePigFrame,
  animalProductCaption,
  animalActionCaption,
  resolveAnimalVisual,
} from '../src/game/animalVisuals.ts'

test('chick, producing and ready presentation follows only supplied stage and cycle', () => {
  assert.equal(resolveAnimalVisual({ stage: 'chick', cycle: null }).label, 'Crescendo')
  const cycle = { id: 'cycle', isReady: false, ownerCollectible: 10, helperCount: 2 }
  assert.equal(resolveAnimalVisual({ stage: 'adult', cycle }).nest, 'nest-empty')
  assert.equal(resolveAnimalVisual({ stage: 'adult', cycle: { ...cycle, isReady: true } }).nest, 'nest-eggs')
  assert.equal(resolveAnimalVisual({ stage: 'adult', cycle }).helpers, 2)
})

test('semantic scale preserves desktop size and caps overview compensation', () => {
  assert.equal(resolveAnimalSemanticScale(1.4), 1)
  assert.equal(resolveAnimalSemanticScale(0.9), 1)
  assert.equal(resolveAnimalSemanticScale(0.75), 1.2)
  assert.equal(resolveAnimalSemanticScale(0.5), 1.4)
  assert.equal(resolveAnimalSemanticScale(0.01), 1.4)
  assert.equal(resolveAnimalSemanticScale(0), 1)
  assert.equal(resolveAnimalSemanticScale(Number.NaN), 1)
})

test('chicken faces each patrol direction and resets before idle or pecking', () => {
  assert.deepEqual(resolveChickenMotion(1_999, false), {
    pose: 'idle', offset: 0, flipX: false,
  })

  const movingRight = resolveChickenMotion(3_000, false)
  assert.match(movingRight.pose, /^walk-/)
  assert.ok(movingRight.offset > 0)
  assert.equal(movingRight.flipX, false)

  const turningLeft = resolveChickenMotion(3_500, false)
  assert.match(turningLeft.pose, /^walk-/)
  assert.equal(turningLeft.offset, 10)
  assert.equal(turningLeft.flipX, true)

  const movingLeft = resolveChickenMotion(4_000, false)
  assert.match(movingLeft.pose, /^walk-/)
  assert.ok(movingLeft.offset > 0 && movingLeft.offset < turningLeft.offset)
  assert.equal(movingLeft.flipX, true)

  assert.deepEqual(resolveChickenMotion(5_000, false), {
    pose: 'peck', offset: 0, flipX: false,
  })
  assert.deepEqual(resolveChickenMotion(7_000, false), {
    pose: 'idle', offset: 0, flipX: false,
  })
  assert.deepEqual(resolveChickenMotion(4_000, true), {
    pose: 'idle', offset: 0, flipX: false,
  })
})

test('runtime atlases contain every stage, nest and transparent enclosure frame', () => {
  for (const [name, required] of [
    ['chicken-poses', ['adult', 'chick'].flatMap(age => ['idle', 'walk-a', 'walk-b', 'peck'].map(pose => `${age}-${pose}`))],
    ['cow-poses', ['adult', 'calf'].flatMap(age => ['idle', 'walk-a', 'walk-b', 'graze'].map(pose => `${age}-${pose}`))],
    ['sheep-poses', ['full', 'shorn'].flatMap(coat => ['idle', 'walk-a', 'walk-b', 'graze'].map(pose => `${coat}-${pose}`))],
    ['lamb-poses', ['idle', 'walk-a', 'walk-b', 'graze'].map(pose => `lamb-${pose}`)],
    ['pig-cycle-poses', ['piglet', 'adult', 'adult-ready'].flatMap(stage => ['idle', 'walk-a', 'walk-b'].map(pose => `${stage}-${pose}`))],
    ['milk-pails', ['milk-empty', 'milk-full']],
    ['chicken-nests', ['nest-empty', 'nest-eggs']],
    ['enclosure-wood-transparent', ['enclosure-wood-transparent']],
  ] as const) {
    const atlas = JSON.parse(readFileSync(new URL(`../src/game/assets/animals/${name}.json`, import.meta.url), 'utf8'))
    for (const key of required) {
      const frame = atlas.frames[key]?.frame
      assert.ok(frame, `missing ${name}/${key}`)
      assert.ok(frame.x >= 0 && frame.y >= 0 && frame.w > 0 && frame.h > 0)
      assert.ok(frame.x + frame.w <= atlas.meta.size.w && frame.y + frame.h <= atlas.meta.size.h)
      if (name === 'chicken-poses' || name === 'cow-poses' || name === 'sheep-poses' || name === 'lamb-poses' || name.startsWith('pig-')) {
        assert.equal(atlas.frames[key].sourceSize.w, name.startsWith('pig-') ? 418 : name === 'lamb-poses' ? 543 : 384)
        assert.equal(atlas.frames[key].sourceSize.h, name.startsWith('pig-') ? 418 : name === 'lamb-poses' ? 724 : 512)
        if (!name.startsWith('pig-')) assert.equal(atlas.frames[key].pivot.x, 0.5)
        else assert.ok(atlas.frames[key].pivot.x > 0.4 && atlas.frames[key].pivot.x < 0.6)
        assert.ok(atlas.frames[key].pivot.y > 0.7 && atlas.frames[key].pivot.y < 1)
      }
    }
  }
})

test('chicken nest atlas contains only the two runtime states', () => {
  const path = new URL('../src/game/assets/animals/chicken-nests.json', import.meta.url)
  const atlas = JSON.parse(readFileSync(path, 'utf8'))

  assert.deepEqual(Object.keys(atlas.frames), ['nest-empty', 'nest-eggs'])
  assert.equal(atlas.meta.image, 'chicken-nests.png')
  assert.deepEqual(atlas.meta.size, { w: 755, h: 275 })
  assert.deepEqual(atlas.frames['nest-empty'].frame, { x: 0, y: 0, w: 380, h: 275 })
  assert.deepEqual(atlas.frames['nest-eggs'].frame, { x: 380, y: 0, w: 375, h: 275 })
})

test('cow uses calf growth and milk readiness without changing chicken presentation', () => {
  assert.equal(resolveAnimalVisual({ animalTypeId: 'cow', stage: 'calf', cycle: null }).label, 'Crescendo')
  assert.equal(resolveAnimalVisual({ animalTypeId: 'cow', stage: 'calf', cycle: null }).productFrame, 'milk-empty')
  const cycle = { id: 'milk-cycle', isReady: true, ownerCollectible: 8, helperCount: 2 }
  assert.equal(resolveAnimalVisual({ animalTypeId: 'cow', stage: 'adult', cycle }).productFrame, 'milk-full')
  assert.equal(resolveAnimalVisual({ animalTypeId: 'cow', stage: 'adult', cycle }).label, 'Leite pronto')
  assert.equal(resolveAnimalVisual({ animalTypeId: 'chicken', stage: 'adult', cycle }).productFrame, 'nest-eggs')
  assert.equal(animalProductCaption('milk'), 'leite')
  assert.equal(animalProductCaption('egg'), 'ovos')
  assert.equal(animalProductCaption('unknown'), 'produtos')
})

test('cow patrol remains bounded and independent, with no motion when reduced', () => {
  for (let time = 0; time < 28_000; time += 100) {
    const motion = resolveCowMotion(time, false)
    assert.ok(motion.offset >= 0 && motion.offset <= 5)
    assert.deepEqual(resolveCowMotion(time, true), { pose: 'idle', offset: 0, flipX: false })
  }
  assert.match(resolveCowMotion(4_000, false).pose, /^walk-/)
  assert.equal(resolveCowMotion(5_000, false).flipX, true)
  assert.equal(resolveCowMotion(8_000, false).pose, 'graze')
  assert.equal(resolveCowMotion(11_000, false).pose, 'idle')
  assert.deepEqual(resolveCowMotion(Number.NaN, false), { pose: 'idle', offset: 0, flipX: false })
})

test('sheep coat represents only the confirmed ready snapshot, without a product prop', () => {
  const producing = { animalTypeId: 'sheep', stage: 'adult' as const,
    cycle: { id: 'wool-cycle', isReady: false, ownerCollectible: 6, helperCount: 1 } }
  const ready = { ...producing, cycle: { ...producing.cycle, isReady: true } }
  assert.equal(resolveAnimalVisual({ ...producing, stage: 'lamb' }).coat, 'shorn')
  assert.equal(resolveAnimalVisual(producing).coat, 'shorn')
  assert.equal(resolveAnimalVisual(ready).coat, 'full')
  assert.equal(resolveAnimalVisual(ready).productFrame, null)
  assert.equal(resolveAnimalVisual(ready).nest, null)
  assert.equal(resolveAnimalVisual(ready).label, 'Lã pronta')
  // Partial theft leaves the same cycle ready, so its full coat stays visible.
  assert.equal(resolveAnimalVisual({ ...ready, cycle: { ...ready.cycle, ownerCollectible: 5 } }).coat, 'full')
  // The next confirmed cycle after shearing returns the short coat.
  assert.equal(resolveAnimalVisual({ ...producing, cycle: { ...producing.cycle, id: 'next' } }).coat, 'shorn')
  assert.equal(animalProductCaption('wool'), 'lã')
  assert.equal(animalActionCaption('collect', 'wool', 6), '+6 lãs 🧺')
  assert.equal(animalActionCaption('collect', 'milk', 8), '+8 leites 🧺')
  assert.equal(animalActionCaption('steal', 'wool', 1), '+1 lã 😈')
  assert.equal(animalActionCaption('help', 'wool', 1), '')
  assert.equal(animalActionCaption('steal', 'egg', 1), '+1 ovo 😈')
})

test('sheep patrol stays within its sector and reduced motion fixes idle', () => {
  for (let time = 0; time < 32_000; time += 100) {
    const motion = resolveSheepMotion(time, false)
    assert.ok(motion.offset >= 0 && motion.offset <= 4)
    assert.deepEqual(resolveSheepMotion(time, true), { pose: 'idle', offset: 0, flipX: false })
  }
  assert.match(resolveSheepMotion(5_000, false).pose, /^walk-/)
  assert.equal(resolveSheepMotion(6_000, false).flipX, true)
  assert.equal(resolveSheepMotion(9_000, false).pose, 'graze')
  assert.deepEqual(resolveSheepMotion(Number.NaN, false), { pose: 'idle', offset: 0, flipX: false })
})

test('pig readiness indicator follows the accepted cycle and does not add a product prop', () => {
  const growing = { animalTypeId: 'pig', stage: 'piglet' as const, cycle: null }
  const producing = { animalTypeId: 'pig', stage: 'adult' as const,
    cycle: { id: 'fertilizer-cycle', isReady: false, ownerCollectible: 4, helperCount: 0 } }
  const ready = { ...producing, cycle: { ...producing.cycle, isReady: true } }
  assert.equal(resolveAnimalVisual(growing).readyIndicator, false)
  assert.equal(resolveAnimalVisual(producing).readyIndicator, false)
  assert.equal(resolveAnimalVisual(ready).readyIndicator, true)
  assert.equal(resolveAnimalVisual(ready).label, 'Adubo pronto')
  assert.equal(resolveAnimalVisual(ready).productFrame, null)
  assert.equal(resolveAnimalVisual(ready).nest, null)
  assert.equal(resolveAnimalVisual({ ...producing, cycle: { ...producing.cycle, id: 'next' } }).readyIndicator, false)
  assert.equal(animalProductCaption('fertilizer'), 'adubo')
  assert.equal(animalActionCaption('collect', 'fertilizer', 4), '+4 adubos 🧺')
})

test('pig patrol remains bounded and reduced motion fixes idle', () => {
  for (let time = 0; time < 36_000; time += 100) {
    const motion = resolvePigMotion(time, false)
    assert.ok(motion.offset >= 0 && motion.offset <= 3)
    assert.deepEqual(resolvePigMotion(time, true), { pose: 'idle', offset: 0, flipX: false })
  }
  assert.equal(resolvePigMotion(6_000, false).pose, 'walk-b')
  assert.equal(resolvePigMotion(6_320, false).pose, 'walk-a')
  assert.equal(resolvePigMotion(7_000, false).flipX, true)
  assert.equal(resolvePigMotion(10_000, false).pose, 'idle')
  assert.deepEqual(resolvePigMotion(Number.NaN, false), { pose: 'idle', offset: 0, flipX: false })
})

test('pig strides retain the young silhouette and ready leaf until a confirmed new cycle', () => {
  const young = { animalTypeId: 'pig', stage: 'piglet' as const, cycle: null }
  const ready = { animalTypeId: 'pig', stage: 'adult' as const,
    cycle: { id: 'ready-cycle', isReady: true, ownerCollectible: 4, helperCount: 1 } }
  const producing = { ...ready, cycle: { ...ready.cycle, id: 'next-cycle', isReady: false } }
  for (const pose of ['walk-a', 'walk-b'] as const) {
    assert.equal(resolvePigFrame(young, pose), `piglet-${pose}`)
    assert.equal(resolvePigFrame(ready, pose), `adult-ready-${pose}`)
    assert.equal(resolvePigFrame(producing, pose), `adult-${pose}`)
  }
  assert.equal(resolvePigFrame(young, 'idle'), 'piglet-idle')
  assert.equal(resolvePigFrame(ready, 'idle'), 'adult-ready-idle')
  assert.equal(resolvePigFrame(producing, 'idle'), 'adult-idle')
})

test('pig idle and strides share one texture and stable torso anchors per state', () => {
  const atlas = JSON.parse(readFileSync(new URL('../src/game/assets/animals/pig-cycle-poses.json', import.meta.url), 'utf8'))
  for (const state of ['piglet', 'adult', 'adult-ready']) {
    const idle = atlas.frames[`${state}-idle`]
    for (const pose of ['walk-a', 'walk-b']) {
      const stride = atlas.frames[`${state}-${pose}`]
      assert.deepEqual(stride.sourceSize, idle.sourceSize)
      assert.deepEqual(stride.pivot, idle.pivot)
    }
  }
})

test('historical collection targets only the immediately replaced cycle of the same animal and farm', () => {
  const effect = { operationId: 'operation', farmId: 'farm', animalId: 'hen', cycleId: 'old' }
  assert.equal(matchesAnimalFeedback(effect, 'farm', 'hen', 'new', 'old', 'collect'), true)
  assert.equal(matchesAnimalFeedback(effect, 'farm', 'hen', 'new', 'old', 'help'), false)
  assert.equal(matchesAnimalFeedback(effect, 'farm', 'hen', 'new', 'older', 'collect'), false)
  assert.equal(matchesAnimalFeedback(effect, 'other', 'hen', 'new', 'old', 'collect'), false)
  assert.equal(matchesAnimalFeedback(effect, 'farm', 'other', 'new', 'old', 'collect'), false)
})
