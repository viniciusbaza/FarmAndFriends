export const FARM_CAMERA_MODE_EVENT = 'farm:camera:mode'

export type FarmCameraMode = 'focus' | 'overview'

export type FarmCameraModeDetail = {
  mode: FarmCameraMode
}

/** Phaser BaseCamera.centerOn stores scroll relative to the unzoomed viewport. */
export function getFarmCameraCenter(camera: { scrollX: number; scrollY: number; width: number; height: number }) {
  return { x: camera.scrollX + camera.width / 2, y: camera.scrollY + camera.height / 2 }
}

export function dispatchFarmCameraMode(mode: FarmCameraMode) {
  window.dispatchEvent(
    new CustomEvent<FarmCameraModeDetail>(FARM_CAMERA_MODE_EVENT, {
      detail: { mode },
    }),
  )
}
