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
  // Four nonoverlapping volumes within the shared diamond. Only species
  // present in the authoritative snapshot are instantiated.
  const reserved = [
    { x: centerX - 55, y: centerY + 15, width: 50, height: 30 }, //Galinha
    { x: centerX - 14, y: centerY - 24, width: 50, height: 36 }, //Vaca
    { x: centerX + 6, y: centerY + 40, width: 44, height: 32 },  //Ovelha
    { x: centerX + 48, y: centerY + 20, width: 38, height: 26 }, //Porco
  ]
  return { centerX, centerY, ground, structure, bounds, reserved,
    chicken: { x: centerX - 50, y: centerY + 10 },
    cow: { x: centerX - 5, y: centerY - 24 },
    sheep: { x: centerX + 6, y: centerY + 40 },
    pig: { x: centerX + 48, y: centerY + 20 },
    milk: { x: centerX - 32, y: centerY - 21 },
    nest: { x: centerX - 73, y: centerY + 6 } }
}

export function animalOverviewZoom(bounds: WorldBounds, width: number, height: number, hud: number) {
  // Do not clamp upward: even the narrowest viewport must fit all world bounds.
  return Math.max(0.01, Math.min((width - 24) / bounds.width,
    (height - hud - 24) / bounds.height, 1.6))
}
