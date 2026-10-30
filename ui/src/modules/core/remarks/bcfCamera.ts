/**
 * BCF viewpoints against Lapin's 3D view (docs/plan-cde.md, stage 2). A BCF
 * camera stands at a point in metres, looks along a direction with an up
 * vector, and is perspective (a vertical field of view in degrees) or
 * orthogonal (the view's height in metres, ViewToWorldScale). Lapin's camera
 * is a pose (azimuth, pitch, distance, target) with Z always up, so a roll
 * cannot be kept and a straight-down look takes its azimuth from the up
 * vector. The section box's six faces are BCF clipping planes, each normal
 * pointing at the part clipped away; axis-aligned planes come back as a box.
 */

import { lookOf, poseOf, positionOf, type CameraPose, type Vec3 } from '../../../viewport/camera';
import { boxAxes, boxPlanes, type SectionBox, type Triple } from '../../../viewport/sectionBox';

export type Triplet = [number, number, number];
export interface BcfCamera { perspective: boolean; viewPoint: Triplet; direction: Triplet; up: Triplet; fieldOfView: number; viewToWorldScale: number; aspectRatio?: number }
export interface BcfPlane { location: Triplet; direction: Triplet }

/** A pose as the 3D view publishes it, with its lens. */
export interface Lens { azimuth: number; pitch: number; distance: number; target: Vec3; fov: number; ortho: boolean; zoom: number; aspect: number }

const EPS = 1e-9;
/** The pitch the 3D view can hold (OrbitControls never looks exactly along Z). */
const MAX_PITCH = Math.PI / 2 - 0.01;

const norm = (v: Triplet): Triplet => { const l = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / l, v[1] / l, v[2] / l]; };
const round = (v: number) => Math.round(v * 1e6) / 1e6;
const tidy = (v: Triplet): Triplet => [round(v[0]), round(v[1]), round(v[2])];

/** The up vector of a look direction with Z up: Z projected off the direction (the direction itself in plan when it looks straight down). */
export function upOf(direction: Triplet): Triplet {
  const d = norm(direction);
  const h = Math.hypot(d[0], d[1]);
  if (h < EPS) return [0, 1, 0];
  // up = Z − (Z·d)d, normalised
  return norm([-d[2] * d[0], -d[2] * d[1], 1 - d[2] * d[2]]);
}

/** The 3D view's camera as a BCF camera. */
export function toBcfCamera(l: Lens): BcfCamera {
  const d = lookOf(l);
  const at = positionOf(l);
  const direction: Triplet = [d.x, d.y, d.z];
  const halfH = l.distance * Math.tan(l.fov / 2);
  return {
    perspective: !l.ortho,
    viewPoint: tidy([at.x, at.y, at.z]),
    direction: tidy(direction),
    up: tidy(upOf(direction)),
    fieldOfView: l.ortho ? 0 : round((l.fov * 180) / Math.PI),
    viewToWorldScale: l.ortho ? round((2 * halfH) / (l.zoom || 1)) : 0,
    aspectRatio: round(l.aspect || 16 / 9),
  };
}

/**
 * A BCF camera as a pose for the 3D view. `fov` is the view's own (radians;
 * an orthogonal camera's distance comes from its height through it);
 * `distance` the way to the target for a perspective camera (what the
 * remark looked at; 10 m when unknown).
 */
export function fromBcfCamera(c: BcfCamera, fov: number, distance = 10): { pose: CameraPose; ortho: boolean } {
  const d = norm(c.direction);
  const h = Math.hypot(d[0], d[1]);
  let azimuth: number;
  if (h < EPS) {
    // Straight up or down: the screen's up is the up vector's heading.
    const u = c.up ?? [0, 1, 0];
    azimuth = Math.hypot(u[0], u[1]) < EPS ? Math.PI / 2 : Math.atan2(u[1], u[0]);
  } else azimuth = Math.atan2(d[1], d[0]);
  const pitch = Math.max(-MAX_PITCH, Math.min(MAX_PITCH, Math.atan2(d[2], h)));
  const far = c.perspective ? Math.max(0.5, distance) : Math.max(0.5, (c.viewToWorldScale || 10) / (2 * Math.tan(fov / 2)));
  const look = lookOf({ azimuth, pitch });
  const target = { x: c.viewPoint[0] + look.x * far, y: c.viewPoint[1] + look.y * far, z: c.viewPoint[2] + look.z * far };
  return { pose: { azimuth, pitch, distance: far, target }, ortho: !c.perspective };
}

/** How far along the look direction a point lies from the camera (the distance to give a target); null when behind it. */
export function distanceTo(c: BcfCamera, p: Triplet): number | null {
  const d = norm(c.direction);
  const t = (p[0] - c.viewPoint[0]) * d[0] + (p[1] - c.viewPoint[1]) * d[1] + (p[2] - c.viewPoint[2]) * d[2];
  return t > 0.1 ? t : null;
}

/** The section box's faces as clipping planes: each a point on the face and the outward normal (what lies beyond is clipped). */
export function planesOfBox(b: SectionBox): BcfPlane[] {
  // boxPlanes keeps the inside where normal·p + constant ≥ 0: the clipped side is −normal, and −constant·normal is on the plane.
  return boxPlanes(b).map((p) => ({
    location: tidy([-p.constant * p.normal[0], -p.constant * p.normal[1], -p.constant * p.normal[2]]),
    direction: tidy([-p.normal[0], -p.normal[1], -p.normal[2]]),
  }));
}

/**
 * A section box from clipping planes whose normals lie along the box's axes
 * (the world's, or turned by `rot`): each sets the face it clips at; faces no
 * plane sets take the fallback box's (the model's bounds). Null without a
 * usable plane, or without a fallback.
 */
export function boxOfPlanes(planes: BcfPlane[], fallback: SectionBox | null, rot = 0): SectionBox | null {
  if (!fallback || planes.length === 0) return null;
  const base: SectionBox = { ...fallback, min: [...fallback.min] as Triple, max: [...fallback.max] as Triple, rot };
  const axes = boxAxes(base);
  let used = 0;
  for (const p of planes) {
    const n = norm(p.direction);
    for (let k = 0; k < 3; k++) {
      const e = axes[k];
      const dot = n[0] * e.x + n[1] * e.y + n[2] * e.z;
      if (Math.abs(Math.abs(dot) - 1) > 1e-3) continue;
      // The location along this axis, in the box's frame.
      const along = p.location[0] * e.x + p.location[1] * e.y + p.location[2] * e.z;
      if (dot > 0) base.max[k] = Math.min(base.max[k], along); else base.min[k] = Math.max(base.min[k], along);
      used++;
    }
  }
  if (!used) return null;
  for (let k = 0; k < 3; k++) if (base.max[k] - base.min[k] < 0.05) return null;
  if (!rot) delete base.rot;
  return base;
}

/** The pose that stands where a BCF camera does, looking at a point (for the key plan's sake, when the direction is to be kept exactly). */
export const poseAt = (viewPoint: Triplet, target: Triplet): CameraPose => poseOf({ x: viewPoint[0], y: viewPoint[1], z: viewPoint[2] }, { x: target[0], y: target[1], z: target[2] });
