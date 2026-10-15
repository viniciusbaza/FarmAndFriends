import type { FarmPlotLayout } from './farmLayout.ts'

export type WorldBounds = FarmPlotLayout['bounds']

export function worldBounds(left: number, top: number, right: number, bottom: number): WorldBounds {
  return { left, top, right, bottom, width: right - left, height: bottom - top,
    centerX: (left + right) / 2, centerY: (top + bottom) / 2 }
}

export function unionWorldBounds(a: WorldBounds, b: WorldBounds): WorldBounds {
  return worldBounds(Math.min(a.left, b.left), Math.min(a.top, b.top),
    Math.max(a.right, b.right), Math.max(a.bottom, b.bottom))
}

/** Presentation reservation only: no species ownership or unlock decisions. */
export function calculateAnimalLayout(plots: WorldBounds) {
  const width = 250
  const height = 122

  // The enclosure shares the two isometric axes used by the plots. Its broad
  // front edge sits above the grid while the centered texture anchor keeps it
  // beside the barn and inside the clearing reserved by the environment art.
  const centerX = plots.right - 35
  const centerY = plots.top + 20
  const ground = worldBounds(centerX - width / 2, centerY - height / 2,
    centerX + width / 2, centerY + height / 2)
  const structure = {
    x: centerX,
    y: centerY,
    width: 288,
    height: 144,
    originX: 0.5,
    originY: 0.5,
  }
  const bounds = worldBounds(
    structure.x - structure.width * structure.originX,
    structure.y - structure.height * structure.originY,
    structure.x + structure.width * (1 - structure.originX),
    structure.y + structure.height * (1 - structure.originY),
  )
  // Five nonoverlapping volumes within the shared diamond. Only species
  // present in the authoritative snapshot are instantiated.
  const reserved = [
    { x: centerX - 65, y: centerY + 12, width: 44, height: 34 }, // Galinha
    { x: centerX - 10, y: centerY - 28, width: 50, height: 38 }, // Vaca
    { x: centerX + 10, y: centerY + 44, width: 36, height: 29 }, // Ovelha
    { x: centerX + 56, y: centerY + 18, width: 32, height: 34 }, // Porco
    { x: centerX - 6, y: centerY + 10, width: 38, height: 28 }, // Pavão
  ]
  // Current disjoint hit rectangles; the peacock reaches its upper body.
  const targets = reserved.map((sector, index) => {
    const top = [-28, -65, 12, -15, -21][index]
    const targetHeight = sector.height
    return { x: sector.x, y: centerY + top + targetHeight / 2,
      width: sector.width, height: targetHeight }
  })
  return { centerX, centerY, ground, structure, bounds, reserved, targets,
    chicken: { x: centerX - 58, y: centerY - 7 },
    cow: { x: centerX - 9, y: centerY - 28 },
    sheep: { x: centerX + 11, y: centerY + 39 },
    pig: { x: centerX + 56, y: centerY + 18 },
    peacock: { x: centerX - 4, y: centerY + 7 },
    milk: { x: centerX - 30, y: centerY - 25 },
    nest: { x: centerX - 75, y: centerY + 6 } }
}

export function animalOverviewZoom(bounds: WorldBounds, width: number, height: number, hud: number) {
  // Do not clamp upward: even the narrowest viewport must fit all world bounds.
  return Math.max(0.01, Math.min((width - 24) / bounds.width,
    (height - hud - 24) / bounds.height, 1.6))
}
