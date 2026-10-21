import assert from 'node:assert/strict'
import test from 'node:test'
import { AuthoritativeReadGate } from '../src/user/authoritativeRead.ts'

test('wallet mutation invalidates in-flight GET even before replacement GET starts', () => {
  const gate = new AuthoritativeReadGate()
  const old = gate.begin(1)
  gate.invalidate()
  assert.equal(gate.accepts(old, 1), false)
  const fresh = gate.begin(1)
  assert.equal(gate.accepts(fresh, 1), true)
  assert.equal(gate.accepts(fresh, 2), false)
})

test('two reversed confirmations converge to absolute XP without reapplying historical rewards', () => {
  const gate = new AuthoritativeReadGate()
  let xp = 10
  // Plant commits +2, animal action +5, animal response arrives first.
  gate.invalidate()
  const animalRead = gate.begin(1)
  if (gate.accepts(animalRead, 1)) xp = 17
  // Delayed plant response also refreshes; never adds +2 to already current 17.
  gate.invalidate()
  const plantRead = gate.begin(1)
  assert.equal(gate.accepts(animalRead, 1), false)
  if (gate.accepts(plantRead, 1)) xp = 17
  assert.equal(xp, 17)
  // A replay is another absolute read, not another +5.
  gate.invalidate()
  const replayRead = gate.begin(1)
  if (gate.accepts(replayRead, 1)) xp = 17
  assert.equal(xp, 17)
})

test('only latest request in the current mutation epoch may replace shared state', () => {
  const gate = new AuthoritativeReadGate()
  const first = gate.begin(1)
  const second = gate.begin(1)
  assert.equal(gate.accepts(first, 1), false)
  assert.equal(gate.accepts(second, 1), true)
  gate.invalidate()
  assert.equal(gate.accepts(second, 1), false)
})
