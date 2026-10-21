import type { Farm } from '../types/Farm'

/** Phaser → React: the scene has finished create and installed farm:sync listeners. */
export type FarmSceneReady = { farmId: string; isVisiting: boolean }

/** Retains accepted React state for replay when a matching scene becomes ready. */
export class FarmSceneSync {
  private farm: Farm | null = null
  private viewKey: string | null = null
  private acceptedFarm: Farm | null = null
  private readonly dispatch: (farm: Farm) => void

  constructor(dispatch: (farm: Farm) => void) {
    this.dispatch = dispatch
  }

  update(farm: Farm, isVisiting: boolean) {
    const viewKey = this.key(farm.id, isVisiting)
    if (viewKey !== this.viewKey) {
      this.viewKey = viewKey
      this.acceptedFarm = null
    }

    // useFarm dispatches farm:sync before React renders. Do not let a render
    // with older props replace a confirmed snapshot received in that gap.
    if (farm === this.acceptedFarm) this.acceptedFarm = null
    this.farm = this.acceptedFarm ?? farm
  }

  /** Records useFarm's normal event without dispatching a second farm:sync. */
  recordAccepted(farm: Farm) {
    if (farm.id !== this.farm?.id) return
    this.farm = farm
    this.acceptedFarm = farm
  }

  sceneReady(farmId: string, isVisiting: boolean) {
    const readyViewKey = this.key(farmId, isVisiting)
    if (readyViewKey !== this.viewKey) return
    if (this.farm) this.dispatch(this.farm)
  }

  private key(farmId: string, isVisiting: boolean) {
    return `${farmId}:${isVisiting ? 'visitor' : 'owner'}`
  }
}
