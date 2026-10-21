import assert from 'node:assert/strict'
import test from 'node:test'
import { resolveAnimalModalPlacement } from '../src/animals/animalModalPosition.ts'

test('animal modal uses the larger vertical side and remains inside the desktop viewport', () => {
  const high = resolveAnimalModalPlacement({ x: 640, y: 100 }, 1280, 720)
  assert.equal(high.side, 'below')
  assert.ok(high.y + 12 + high.maxHeight <= 720 - 16)

  const middle = resolveAnimalModalPlacement({ x: 640, y: 360 }, 1280, 720)
  assert.equal(middle.side, 'above')
  assert.ok(middle.y - 12 - middle.maxHeight >= 16)

  const low = resolveAnimalModalPlacement({ x: 640, y: 690 }, 1280, 720)
  assert.equal(low.side, 'above')
  assert.ok(low.y - 12 - low.maxHeight >= 16)
})

test('animal modal keeps its full width inside narrow desktop bounds', () => {
  const left = resolveAnimalModalPlacement({ x: 0, y: 400 }, 900, 800)
  const right = resolveAnimalModalPlacement({ x: 900, y: 400 }, 900, 800)
  assert.equal(left.x, 160)
  assert.equal(right.x, 740)
})
