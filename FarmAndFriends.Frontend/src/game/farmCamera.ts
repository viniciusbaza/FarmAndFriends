export const FARM_CAMERA_MODE_EVENT = 'farm:camera:mode'

export type FarmCameraMode = 'focus' | 'overview'

export type FarmCameraModeDetail = {
  mode: FarmCameraMode
}

/** Camera events can arrive between scene shutdown and the next layout. */
export function canMoveFarmCamera(
  sceneActive: boolean,
  layoutReady: boolean,
  camera: { width: number; height: number } | null | undefined,
) {
  return sceneActive && layoutReady && Boolean(camera && camera.width > 0 && camera.height > 0)
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
