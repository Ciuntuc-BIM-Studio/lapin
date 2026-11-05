/**
 * Navigating the 3D view (report #89): the wheel zooms to the point under the
 * cursor, and a drag orbits round the selection (else round the point of the
 * model under the cursor), as CAD and BIM viewers do. Both keep their point
 * where it is on the screen: a zoom is a scaling of the camera's position and
 * target about the point, an orbit a rotation of both about it, so the view
 * never jumps. View → Zoom to cursor and View → Orbit around selection turn
 * each off (app/display.ts), back to three's orbit controls about the view's centre.
 */

export interface V3 { x: number; y: number; z: number }
/** The camera as the orbit controls hold it: where it stands and what it turns about. */
export interface Rig { position: V3; target: V3 }

const sub = (a: V3, b: V3): V3 => ({ x: a.x - b.x, y: a.y - b.y, z: a.z - b.z });
const add = (a: V3, b: V3): V3 => ({ x: a.x + b.x, y: a.y + b.y, z: a.z + b.z });
const mul = (a: V3, k: number): V3 => ({ x: a.x * k, y: a.y * k, z: a.z * k });
const dot = (a: V3, b: V3) => a.x * b.x + a.y * b.y + a.z * b.z;
const len = (a: V3) => Math.sqrt(dot(a, a));

/** The closest a zoom comes to its point (m), and the farthest it goes (within the camera's far plane). */
export const ZOOM_NEAR = 0.25;
export const ZOOM_FAR = 4000;

/**
 * A perspective zoom by `k` (the factor the view is enlarged by: over 1 in,
 * under 1 out) about `point`: the camera and its target move along their lines
 * to the point, so the point stays under the cursor. Never closer to the point
 * than ZOOM_NEAR nor through it; never farther than ZOOM_FAR.
 */
export function zoomAbout(rig: Rig, point: V3, k: number, near = ZOOM_NEAR, far = ZOOM_FAR): Rig {
  const d = len(sub(rig.position, point));
  if (!(k > 0) || d < 1e-9) return rig;
  let s = 1 / k;
  if (d * s < near) s = Math.min(1, near / d);
  if (d * s > far) s = Math.max(1, far / d);
  const scale = (p: V3) => add(point, mul(sub(p, point), s));
  return { position: scale(rig.position), target: scale(rig.target) };
}

/**
 * An orthographic zoom from `zoom` to `next` about `point`: the camera and its
 * target slide across the view (not along it) by what keeps the point where it
 * is on the screen. `dir` is the way the camera looks.
 */
export function orthoZoomShift(rig: Rig, point: V3, dir: V3, zoom: number, next: number): Rig {
  if (!(zoom > 0) || !(next > 0)) return rig;
  const n = mul(dir, 1 / (len(dir) || 1));
  const off = sub(point, rig.target);
  const across = sub(off, mul(n, dot(off, n)));
  const shift = mul(across, 1 - zoom / next);
  return { position: add(rig.position, shift), target: add(rig.target, shift) };
}

/** How far from straight up and down an orbit stops (radians), as the orbit controls keep off the poles. */
const POLE = 1e-3;

/**
 * An orbit about `pivot`: `dAzimuth` round the vertical (Z) and `dPolar` away
 * from it (the camera's polar angle grows: it looks more level), the camera
 * and its target turned together so the camera keeps its aim relative to the
 * pivot and the pivot stays where it is on the screen. The polar angle is kept
 * off the poles, so the view never flips.
 */
export function orbitAbout(rig: Rig, pivot: V3, dAzimuth: number, dPolar: number): Rig {
  const o = sub(rig.position, rig.target);
  const r = len(o);
  if (r < 1e-9) return rig;
  const polar = Math.acos(Math.max(-1, Math.min(1, o.z / r)));
  const next = Math.max(POLE, Math.min(Math.PI - POLE, polar + dPolar));
  const dp = next - polar;
  const az = Math.atan2(o.y, o.x);
  // The pitch: about the horizontal axis across the view (k × o grows the polar angle), through the pivot.
  const k: V3 = { x: -Math.sin(az), y: Math.cos(az), z: 0 };
  const turn = (p: V3) => rotateZ(rotateAxis(sub(p, pivot), k, dp), dAzimuth);
  return { position: add(pivot, turn(rig.position)), target: add(pivot, turn(rig.target)) };
}

function rotateAxis(v: V3, k: V3, a: number): V3 {
  if (!a) return v;
  // Rodrigues: v cos a + (k × v) sin a + k (k·v)(1 − cos a).
  const c = Math.cos(a), s = Math.sin(a), kv = dot(k, v);
  const cross = { x: k.y * v.z - k.z * v.y, y: k.z * v.x - k.x * v.z, z: k.x * v.y - k.y * v.x };
  return { x: v.x * c + cross.x * s + k.x * kv * (1 - c), y: v.y * c + cross.y * s + k.y * kv * (1 - c), z: v.z * c + cross.z * s + k.z * kv * (1 - c) };
}

function rotateZ(v: V3, a: number): V3 {
  if (!a) return v;
  const c = Math.cos(a), s = Math.sin(a);
  return { x: v.x * c - v.y * s, y: v.x * s + v.y * c, z: v.z };
}

/**
 * What an orbit turns about: the selection's middle while something shown is
 * selected, else the point of the model under the cursor where the drag
 * started, else nothing (the view's own centre, as the orbit controls do).
 */
export function orbitPivot(selection: { min: V3; max: V3 } | null, hit: V3 | null): V3 | null {
  if (selection) return mul(add(selection.min, selection.max), 0.5);
  return hit;
}

/** A drag of `dx`, `dy` pixels as the orbit controls turn the view: a full turn for the pane's height, the model following the mouse. */
export const orbitAngles = (dx: number, dy: number, height: number) => {
  const h = Math.max(1, height);
  return { dAzimuth: (-2 * Math.PI * dx) / h, dPolar: (-2 * Math.PI * dy) / h };
};
