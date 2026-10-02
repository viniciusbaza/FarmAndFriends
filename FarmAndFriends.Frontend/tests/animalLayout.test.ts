import assert from 'node:assert/strict'
import test from 'node:test'
import {
  calculateFarmPlotLayout,
  FARM_PLOT_HEIGHT,
  FARM_PLOT_WIDTH,
  FARM_TILE_HEIGHT,
  FARM_TILE_WIDTH,
} from '../src/game/farmLayout.ts'
import { gridToIso } from '../src/game/iso/isoUtils.ts'
import { animalOverviewZoom, calculateAnimalLayout, unionWorldBounds } from '../src/game/animalLayout.ts'
import { getFarmCameraCenter } from '../src/game/farmCamera.ts'

const plots = Array.from({ length: 28 }, (_, index) => ({ x: index % 7, y: Math.floor(index / 7) }))

test('camera center inverts Phaser centerOn at any zoom before resizing or dragging', () => {
  for (const [width, height] of [[1440, 900], [390, 844]]) {
    const expected = { x: 120, y: -40 }
    // BaseCamera.centerOnX/Y subtract half the viewport, without dividing by zoom.
    assert.deepEqual(getFarmCameraCenter({ width, height,
      scrollX: expected.x - width / 2, scrollY: expected.y - height / 2 }), expected)
  }
})

function diamondsOverlap(
  a: { x: number, y: number, width: number, height: number },
  b: { x: number, y: number, width: number, height: number },
) {
  return Math.abs(a.x - b.x) / ((a.width + b.width) / 2) +
    Math.abs(a.y - b.y) / ((a.height + b.height) / 2) < 1
}

test('aligned enclosure stays beside the barn-side plots without entering the right orchard', () => {
  for (let unlocked = 9; unlocked <= 28; unlocked++) {
    let extra = unlocked - 9
    const layout = calculateFarmPlotLayout(plots.map(p => ({ ...p,
      unlocked: p.x < 3 && p.y < 3 || extra-- > 0 })))
    const enclosure = calculateAnimalLayout(layout.bounds)
    assert.equal(enclosure.centerX - layout.bounds.right, -35)
    assert.equal(enclosure.centerY - layout.bounds.top, 20)
    assert.ok(enclosure.centerX > layout.bounds.centerX)
    assert.ok(enclosure.centerY < layout.bounds.centerY)
    assert.ok(enclosure.bounds.right < layout.bounds.right + 160)
    assert.equal(enclosure.ground.width, 250)
    assert.equal(enclosure.ground.height, 122)
    assert.equal(enclosure.structure.width / enclosure.structure.height, 2)
    assert.equal(enclosure.structure.originX, 0.5)
    assert.equal(enclosure.structure.originY, 0.5)
    assert.equal(enclosure.reserved.length, 4)
    const chickenSector = enclosure.reserved[0]
    const leftSectorPoints = [
      enclosure.chicken,
      enclosure.nest,
      { x: enclosure.chicken.x + 10, y: enclosure.chicken.y + 5 },
    ]
    for (const point of leftSectorPoints) {
      assert.ok(Math.abs(point.x - chickenSector.x) <= chickenSector.width / 2)
      assert.ok(Math.abs(point.y - chickenSector.y) <= chickenSector.height / 2)
      assert.ok(Math.abs(point.x - enclosure.centerX) / (enclosure.ground.width / 2) +
        Math.abs(point.y - enclosure.centerY) / (enclosure.ground.height / 2) < 1)
    }
    const cowSector = enclosure.reserved[1]
    for (const point of [enclosure.cow, enclosure.milk,
      { x: enclosure.cow.x + 5, y: enclosure.cow.y + 2.5 }]) {
      assert.ok(Math.abs(point.x - cowSector.x) <= cowSector.width / 2)
      assert.ok(Math.abs(point.y - cowSector.y) <= cowSector.height / 2)
      assert.ok(Math.abs(point.x - enclosure.centerX) / 125 +
        Math.abs(point.y - enclosure.centerY) / 61 < 1)
    }
    const sheepSector = enclosure.reserved[2]
    assert.ok(Math.abs(enclosure.sheep.x - sheepSector.x) <= sheepSector.width / 2)
    assert.ok(Math.abs(enclosure.sheep.y - sheepSector.y) <= sheepSector.height / 2)
    assert.ok(Math.abs(enclosure.sheep.x - enclosure.centerX) / 125 +
      Math.abs(enclosure.sheep.y - enclosure.centerY) / 61 < 1)
    assert.ok(cowSector.y - 12 + 44 / 2 < sheepSector.y - sheepSector.height / 2)
    assert.ok(chickenSector.x + chickenSector.width / 2 < sheepSector.x - sheepSector.width / 2)
    assert.ok(sheepSector.x + sheepSector.width / 2 <
      enclosure.reserved[3].x - enclosure.reserved[3].width / 2)
    const pigSector = enclosure.reserved[3]
    assert.ok(Math.abs(enclosure.pig.x - pigSector.x) <= pigSector.width / 2)
    assert.ok(Math.abs(enclosure.pig.y - pigSector.y) <= pigSector.height / 2)
    assert.ok(Math.abs(enclosure.pig.x - enclosure.centerX) / 125 +
      Math.abs(enclosure.pig.y - enclosure.centerY) / 61 < 1)
    assert.ok(chickenSector.x + chickenSector.width / 2 < cowSector.x - cowSector.width / 2
      || cowSector.y - 12 + 44 / 2 < chickenSector.y - 34 / 2,
      'species interaction targets remain disjoint')

    for (const plot of plots) {
      const point = gridToIso(plot.x, plot.y, FARM_TILE_WIDTH, FARM_TILE_HEIGHT)
      assert.equal(diamondsOverlap({
        x: layout.originX + point.isoX,
        y: layout.originY + point.isoY - FARM_PLOT_HEIGHT / 2,
        width: FARM_PLOT_WIDTH,
        height: FARM_PLOT_HEIGHT,
      }, {
        x: enclosure.centerX,
        y: enclosure.centerY,
        width: enclosure.ground.width,
        height: enclosure.ground.height,
      }), false, `enclosure overlaps plot ${plot.x},${plot.y}`)
    }

    for (const volume of enclosure.reserved) {
      // Ground-contact rectangle fits inside 2:1 diamond, with margin.
      for (const dx of [-volume.width / 2, volume.width / 2]) {
        for (const dy of [-6, 6]) {
          assert.ok(Math.abs(volume.x + dx - enclosure.centerX) / 125 +
            Math.abs(volume.y + dy - enclosure.centerY) / 61 < 1)
        }
      }
    }
  }
})

test('overview fits plots, tall crops and enclosure in portrait and landscape', () => {
  const layout = calculateFarmPlotLayout(plots)
  const enclosure = calculateAnimalLayout(layout.bounds)
  const bounds = unionWorldBounds({ ...layout.bounds, top: layout.bounds.top - 84 }, enclosure.bounds)
  for (const [width, height] of [[320, 568], [390, 844], [844, 390], [1440, 900]]) {
    const zoom = animalOverviewZoom(bounds, width, height, 96)
    assert.ok(bounds.width * zoom <= width - 24 + 0.001)
    assert.ok(bounds.height * zoom <= height - 120 + 0.001)
  }
})
