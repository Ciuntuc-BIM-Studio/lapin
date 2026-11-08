/**
 * The 3D camera as the key plan sees it: where it stands, where it looks
 * (its azimuth in plan, its pitch), how far its target is, and what can be
 * asked of it. The 3D view publishes the state on every move and lends the
 * api while it is mounted. Angles in radians; azimuth 0 looks along +X
 * (east), π/2 along +Y (north); pitch below 0 looks down.
 */

import { create } from 'zustand';

export interface Vec3 { x: number; y: number; z: number }

export interface CameraPose { azimuth: number; pitch: number; distance: number; target: Vec3 }

export interface CameraState extends CameraPose {
  position: Vec3;
  /** Vertical field of view, radians. */
  fov: number;
  ortho: boolean;
  /** The orthogonal camera's zoom (1 in perspective): its view is 2·distance·tan(fov/2)/zoom high. */
  zoom: number;
  /** Width over height of the view. */
  aspect: number;
}

export interface CameraApi {
  /** Part of the pose changed, the rest kept; animated unless told not to. */
  orbitTo(pose: Partial<CameraPose>, animate?: boolean): void;
  /** Look at a point from where the camera stands (the target moves there, the distance follows). */
  lookAt(p: Vec3): void;
  /** Stand at a place in plan, keeping the look (camera and target move together). */
  moveTo(p: { x: number; y: number }): void;
  /** The camera's height (its target's follows). */
  setHeight(z: number): void;
  /** Frame everything. */
  fit(animate?: boolean): void;
  setOrtho(on: boolean): void;
  /** The view as drawn now, a PNG data URL at most `maxWidth` wide (a remark's snapshot); null when it cannot be read. */
  snapshot(maxWidth?: number): string | null;
  /** The view drawn now, on its canvas, to be copied at once (the 4D simulation's video frames); null while there is none. */
  canvas?(): HTMLCanvasElement | null;
  /** The box round the elements given as they are drawn now (what a move's handles stand on); null when none is shown. */
  boxOf?(ids: string[]): { min: Vec3; max: Vec3 } | null;
  /** Where a model point is on the screen (client px), as drawn now; null behind the camera (the guide's scenes aim by it). */
  screenOf?(p: Vec3): { x: number; y: number } | null;
}

export const useCamera = create<CameraState & { api: CameraApi | null }>(() => ({
  azimuth: Math.PI / 4, pitch: -0.5, distance: 30, target: { x: 0, y: 0, z: 0 }, position: { x: -20, y: -20, z: 15 }, fov: (45 * Math.PI) / 180, ortho: false, zoom: 1, aspect: 16 / 9, api: null,
}));

/** The look direction of a pose (unit). */
export const lookOf = (p: { azimuth: number; pitch: number }): Vec3 => ({ x: Math.cos(p.azimuth) * Math.cos(p.pitch), y: Math.sin(p.azimuth) * Math.cos(p.pitch), z: Math.sin(p.pitch) });

/** Where the camera stands for a pose. */
export const positionOf = (p: CameraPose): Vec3 => {
  const d = lookOf(p);
  return { x: p.target.x - d.x * p.distance, y: p.target.y - d.y * p.distance, z: p.target.z - d.z * p.distance };
};

/** The pose that stands at a position looking at a target. */
export function poseOf(position: Vec3, target: Vec3): CameraPose {
  const dx = target.x - position.x, dy = target.y - position.y, dz = target.z - position.z;
  const h = Math.hypot(dx, dy);
  return { azimuth: h > 1e-9 ? Math.atan2(dy, dx) : 0, pitch: Math.atan2(dz, h), distance: Math.hypot(h, dz), target };
}

/** The standard views: from the compass points (elevations), the four isometrics and the plan. */
export const STANDARD = {
  /** Seen from the north: looking south. */
  N: { azimuth: -Math.PI / 2, pitch: 0 }, E: { azimuth: Math.PI, pitch: 0 }, S: { azimuth: Math.PI / 2, pitch: 0 }, W: { azimuth: 0, pitch: 0 },
  NE: { azimuth: (-3 * Math.PI) / 4, pitch: -Math.PI / 6 }, SE: { azimuth: (3 * Math.PI) / 4, pitch: -Math.PI / 6 }, SW: { azimuth: Math.PI / 4, pitch: -Math.PI / 6 }, NW: { azimuth: -Math.PI / 4, pitch: -Math.PI / 6 },
  plan: { azimuth: Math.PI / 2, pitch: -Math.PI / 2 + 0.01 },
} as const;

/** The camera's pose as a key (millimetre, milliradian), to tell whether it moved. */
export function poseKey(s: CameraPose = useCamera.getState()): string {
  return [s.azimuth, s.pitch, s.distance, s.target.x, s.target.y, s.target.z].map((v) => v.toFixed(3)).join(',');
}

/**
 * After an import (IFC, Fragments, BIM Open Schema, a mesh): the model framed once its elements are drawn, so the camera
 * does not start very close to a part of it; never when the user moved the camera while it came in (`before`: the pose
 * when it began). Framed once the new elements are on screen (two frames), and again a moment later for those drawn after
 * (a big diff arrives in parts), unless the camera was moved in between.
 */
export function fitAfterImport(before: string, later = 900): void {
  const raf = (f: () => void) => (typeof requestAnimationFrame === 'function' ? requestAnimationFrame(() => requestAnimationFrame(f)) : setTimeout(f, 32));
  raf(() => {
    const api = useCamera.getState().api;
    if (!api || poseKey() !== before) return;
    api.fit(false);
    const framed = poseKey();
    setTimeout(() => { if (poseKey() === framed) useCamera.getState().api?.fit(false); }, later);
  });
}
