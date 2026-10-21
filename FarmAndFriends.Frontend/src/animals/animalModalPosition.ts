import type { AnimalModalAnchor } from './AnimalContext'

const VIEWPORT_MARGIN = 16
const PANEL_GAP = 12
const PANEL_HALF_WIDTH = 144 + VIEWPORT_MARGIN

function clamp(value: number, min: number, max: number) {
  return Math.max(min, Math.min(value, max))
}

export type AnimalModalPlacement = {
  x: number
  y: number
  side: 'above' | 'below'
  maxHeight: number
}

export function resolveAnimalModalPlacement(
  anchor: AnimalModalAnchor | null,
  viewportWidth: number,
  viewportHeight: number,
): AnimalModalPlacement {
  const horizontalSafeMargin = Math.min(viewportWidth / 2, PANEL_HALF_WIDTH)
  const x = clamp(
    anchor?.x ?? viewportWidth / 2,
    horizontalSafeMargin,
    Math.max(horizontalSafeMargin, viewportWidth - horizontalSafeMargin),
  )
  const y = clamp(
    anchor?.y ?? viewportHeight - VIEWPORT_MARGIN,
    VIEWPORT_MARGIN + 40,
    viewportHeight - VIEWPORT_MARGIN,
  )
  const roomAbove = Math.max(0, y - PANEL_GAP - VIEWPORT_MARGIN)
  const roomBelow = Math.max(0, viewportHeight - y - PANEL_GAP - VIEWPORT_MARGIN)
  const side = roomAbove >= roomBelow ? 'above' : 'below'

  return {
    x,
    y,
    side,
    maxHeight: Math.max(0, side === 'above' ? roomAbove : roomBelow),
  }
}
