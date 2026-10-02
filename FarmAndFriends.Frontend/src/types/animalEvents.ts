import type { AnimalOperationEffect } from './animal'

/** Phaser → React intent. React validates the active farm before opening the panel. */
export type AnimalScreenPosition = { screenX?: number; screenY?: number }
export type AnimalSelect = AnimalScreenPosition & { farmId: string; animalId: string }
/** React → Phaser confirmed result, after farm:sync, once per operationId; never for replay.
 * effect.cycleId identifies the historical cycle, not necessarily the current nest.
 * Both sides unregister listeners on unmount/shutdown. No Phaser objects cross the bridge.
 */
export type AnimalDone = AnimalOperationEffect & { operationId: string }
export type AnimalCollectDone = AnimalDone
export type AnimalHelpDone = AnimalDone
export type AnimalStealDone = AnimalDone
export type AnimalBuyDone = AnimalDone
