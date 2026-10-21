import type Phaser from 'phaser'
import chickenImage from './assets/animals/chicken-poses.png'
import chickenAtlas from './assets/animals/chicken-poses.json'
import cowImage from './assets/animals/cow-poses.png'
import cowAtlas from './assets/animals/cow-poses.json'
import sheepImage from './assets/animals/sheep-poses.png'
import sheepAtlas from './assets/animals/sheep-poses.json'
import lambImage from './assets/animals/lamb-poses.png'
import lambAtlas from './assets/animals/lamb-poses.json'
import pigImage from './assets/animals/pig-cycle-poses.png'
import pigAtlas from './assets/animals/pig-cycle-poses.json'
import peacockImage from './assets/animals/peacock-adult-poses.png'
import peacockAtlas from './assets/animals/peacock-adult-poses.json'
import peachickImage from './assets/animals/peachick-poses.png'
import peachickAtlas from './assets/animals/peachick-poses.json'
import milkImage from './assets/animals/milk-pails.png'
import milkAtlas from './assets/animals/milk-pails.json'
import chickenNestsImage from './assets/animals/chicken-nests.png'
import chickenNestsAtlas from './assets/animals/chicken-nests.json'
import enclosureWoodImage from './assets/animals/enclosure-wood-transparent.png'
import enclosureWoodAtlas from './assets/animals/enclosure-wood-transparent.json'

export const CHICKEN_ATLAS = 'chicken-poses'
export const COW_ATLAS = 'cow-poses'
export const SHEEP_ATLAS = 'sheep-poses'
export const LAMB_ATLAS = 'lamb-poses'
export const PIG_ATLAS = 'pig-cycle-poses'
export const PEACOCK_ATLAS = 'peacock-adult-poses'
export const PEACHICK_ATLAS = 'peachick-poses'
export const MILK_ATLAS = 'milk-pails'
export const CHICKEN_NESTS_ATLAS = 'chicken-nests'
export const ENCLOSURE_WOOD_ATLAS = 'enclosure-wood-transparent'

export const ENCLOSURE_STYLES = {
  wood: {
    atlas: ENCLOSURE_WOOD_ATLAS,
    frame: 'enclosure-wood-transparent',
  },
} as const

export type AnimalEnclosureStyle = keyof typeof ENCLOSURE_STYLES
export const DEFAULT_ENCLOSURE_STYLE: AnimalEnclosureStyle = 'wood'

const assets = [
  { key: CHICKEN_ATLAS, image: chickenImage, atlas: chickenAtlas },
  { key: COW_ATLAS, image: cowImage, atlas: cowAtlas },
  { key: SHEEP_ATLAS, image: sheepImage, atlas: sheepAtlas },
  { key: LAMB_ATLAS, image: lambImage, atlas: lambAtlas },
  { key: PIG_ATLAS, image: pigImage, atlas: pigAtlas },
  { key: PEACOCK_ATLAS, image: peacockImage, atlas: peacockAtlas },
  { key: PEACHICK_ATLAS, image: peachickImage, atlas: peachickAtlas },
  { key: MILK_ATLAS, image: milkImage, atlas: milkAtlas },
  { key: CHICKEN_NESTS_ATLAS, image: chickenNestsImage, atlas: chickenNestsAtlas },
  { key: ENCLOSURE_WOOD_ATLAS, image: enclosureWoodImage, atlas: enclosureWoodAtlas },
] as const

export function preloadAnimalAssets(scene: Phaser.Scene) {
  for (const asset of assets) {
    scene.load.atlas(asset.key, asset.image, asset.atlas)
  }
}

export const CHICKEN_METRICS = {
  adult: { scale: 24 / 392, originX: 0.5, originY: 494 / 512 },
  chick: { scale: 15 / 264, originX: 0.5, originY: 382 / 512 },
} as const

/** Measured idle height; each pose keeps one age scale and its hoof pivot. */
export const COW_METRICS = {
  adult: { scale: 36 / 346, originX: 0.5 },
  calf: { scale: 24 / 263, originX: 0.5 },
} as const

export function cowFootOrigin(frame: string) {
  return cowAtlas.frames[frame as keyof typeof cowAtlas.frames]?.pivot.y ?? 474 / 512
}

/** Adult coat states share a scale; the lamb uses its own sheet and foot pivot. */
export const SHEEP_METRICS = {
  adult: { scale: 34 / 370, originX: 0.5 },
  lamb: { scale: 23 / 426, originX: 0.5 },
} as const

export function sheepFootOrigin(frame: string, lamb: boolean) {
  if (lamb) return lambAtlas.frames[frame as keyof typeof lambAtlas.frames]?.pivot.y ?? 576 / 724
  return sheepAtlas.frames[frame as keyof typeof sheepAtlas.frames]?.pivot.y ?? 485 / 512
}

/** One scale per age across idle and both strides in the unified atlas. */
export const PIG_METRICS = {
  adult: { scale: 32 / 330 },
  piglet: { scale: 23 / 283 },
} as const

export function pigFootOrigin(frame: string) {
  return pigAtlas.frames[frame as keyof typeof pigAtlas.frames]?.pivot ?? pigAtlas.frames['adult-idle'].pivot
}

export const MILK_METRICS = { scale: 18 / 593, originX: 0.54, originY: 950 / 1254 }

/** Approved v6 uses original v1 art and one fixed scale per age/open fan. */
export const PEACOCK_METRICS = {
  adult: { scale: 36 / 375 },
  peachick: { scale: 26 / 505 },
} as const

export function peacockFootOrigin(frame: string, young: boolean) {
  return young
    ? peachickAtlas.frames[frame as keyof typeof peachickAtlas.frames]?.pivot ?? peachickAtlas.frames['peachick-idle'].pivot
    : peacockAtlas.frames[frame as keyof typeof peacockAtlas.frames]?.pivot ?? peacockAtlas.frames['producing-idle'].pivot
}
