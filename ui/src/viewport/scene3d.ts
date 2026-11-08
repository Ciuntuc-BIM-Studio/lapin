/**
 * What the 3D view's overlays ask of the scene under the cursor
 * (docs/plan-3d-modelling.md): the cursor's ray, a model point on the screen,
 * the body the ray meets first (not what the storeys' cut, a section or the
 * section box takes away), the edges near the cursor for the snaps, and the
 * point on the active level. The move, the placing tools and the grips in 3D
 * share them with snap3d.ts.
 */

import * as THREE from 'three';
import type { Vec3 } from './camera';
import { useClip3d } from './section3d';
import { edgesOf } from './sketch/edges';
import { snap3, type Axis3, type Lock3, type Pt, type Ray3, type Seg3, type Snapped3 } from './snap3d';
import type { PointCloudLayer } from './pointClouds3d';

export interface ViewRefs {
  camera: React.RefObject<THREE.Camera | null>;
  canvas: React.RefObject<HTMLElement | null>;
  scene: React.RefObject<THREE.Scene | null>;
}

/** A client point inside the view's canvas. */
export function insideView(canvas: HTMLElement | null, x: number, y: number): boolean {
  const r = canvas?.getBoundingClientRect();
  return !!r && x >= r.left && x <= r.right && y >= r.top && y <= r.bottom;
}

/** The ray under a client point. */
export function rayAt(v: ViewRefs, x: number, y: number): Ray3 | null {
  const c = v.camera.current, r = v.canvas.current?.getBoundingClientRect();
  if (!c || !r) return null;
  c.updateMatrixWorld();
  const ray = new THREE.Raycaster();
  ray.setFromCamera(new THREE.Vector2(((x - r.left) / r.width) * 2 - 1, -((y - r.top) / r.height) * 2 + 1), c);
  const o = ray.ray.origin, d = ray.ray.direction;
  return { origin: { x: o.x, y: o.y, z: o.z }, dir: { x: d.x, y: d.y, z: d.z } };
}

/** A model point on the view (px from its top left); null behind the camera. */
export function projector(v: ViewRefs): (p: Vec3) => Pt | null {
  const c = v.camera.current, r = v.canvas.current?.getBoundingClientRect();
  const q = new THREE.Vector3();
  if (c) c.updateMatrixWorld();
  return (p) => {
    if (!c || !r) return null;
    q.set(p.x, p.y, p.z).project(c);
    if (q.z > 1 || q.z < -1) return null;
    return { x: ((q.x + 1) / 2) * r.width, y: ((1 - q.y) / 2) * r.height };
  };
}

/** Whether a point is kept by the clipping planes the view cuts by (three's: kept where the distance is positive). */
const kept = (p: THREE.Vector3) => useClip3d.getState().planes.every((c) => c.normal[0] * p.x + c.normal[1] * p.y + c.normal[2] * p.z + c.constant >= -1e-4);

/** The element meshes drawn (their own, not instances), but those of `skip`. */
function meshes(scene: THREE.Scene | null, skip?: ReadonlySet<string>): THREE.Mesh[] {
  const out: THREE.Mesh[] = [];
  scene?.traverseVisible((o) => {
    const m = o as THREE.Mesh;
    const id = m.userData?.nodeId as string | undefined;
    if (!m.isMesh || !id || (m as THREE.InstancedMesh).isInstancedMesh || skip?.has(id)) return;
    out.push(m);
  });
  return out;
}

export interface Hit3 { point: Vec3; normal: Vec3 | null; nodeId: string }

/** The body the ray meets first, kept by the view's cuts; null over empty space. */
export function hitAt(scene: THREE.Scene | null, ray: Ray3, skip?: ReadonlySet<string>): Hit3 | null {
  const rc = new THREE.Raycaster(new THREE.Vector3(ray.origin.x, ray.origin.y, ray.origin.z), new THREE.Vector3(ray.dir.x, ray.dir.y, ray.dir.z));
  const h = rc.intersectObjects(meshes(scene, skip), false).find((x) => kept(x.point));
  if (!h) return null;
  const n = h.face ? h.face.normal.clone().transformDirection(h.object.matrixWorld) : null;
  return { point: { x: h.point.x, y: h.point.y, z: h.point.z }, normal: n ? { x: n.x, y: n.y, z: n.z } : null, nodeId: h.object.userData.nodeId as string };
}

/** Where the ray meets the horizontal plane at `z`, in front of the camera; null when it runs away from it. */
export function onLevel(ray: Ray3, z: number): Vec3 | null {
  if (Math.abs(ray.dir.z) < 1e-9) return null;
  const t = (z - ray.origin.z) / ray.dir.z;
  return t > 0 ? { x: ray.origin.x + ray.dir.x * t, y: ray.origin.y + ray.dir.y * t, z } : null;
}

/** The sharp edges of the bodies whose box reaches the cursor on the screen (within `tol` px), but those of `skip`. */
export function segmentsNear(v: ViewRefs, cursor: Pt, skip?: ReadonlySet<string>, tol = 12, max = 20000): Seg3[] {
  const out: Seg3[] = [];
  const project = projector(v);
  const box = new THREE.Box3(), c = new THREE.Vector3(), a = new THREE.Vector3(), b = new THREE.Vector3();
  for (const m of meshes(v.scene.current, skip)) {
    if (!m.geometry) continue;
    if (!m.geometry.boundingBox) m.geometry.computeBoundingBox();
    box.copy(m.geometry.boundingBox!).applyMatrix4(m.matrixWorld);
    // The box's corners on the screen: the cursor within their bounds (a corner behind the camera: kept to be sure).
    let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity, behind = false;
    for (let i = 0; i < 8; i++) {
      c.set(i & 1 ? box.max.x : box.min.x, i & 2 ? box.max.y : box.min.y, i & 4 ? box.max.z : box.min.z);
      const s = project({ x: c.x, y: c.y, z: c.z });
      if (!s) { behind = true; break; }
      x0 = Math.min(x0, s.x); y0 = Math.min(y0, s.y); x1 = Math.max(x1, s.x); y1 = Math.max(y1, s.y);
    }
    if (!behind && (cursor.x < x0 - tol || cursor.x > x1 + tol || cursor.y < y0 - tol || cursor.y > y1 + tol)) continue;
    const e = edgesOf(m.geometry);
    for (let k = 0; k + 5 < e.length && out.length < max; k += 6) {
      a.set(e[k], e[k + 1], e[k + 2]).applyMatrix4(m.matrixWorld);
      b.set(e[k + 3], e[k + 4], e[k + 5]).applyMatrix4(m.matrixWorld);
      if (!kept(a) && !kept(b)) continue;
      out.push({ a: { x: a.x, y: a.y, z: a.z }, b: { x: b.x, y: b.y, z: b.z } });
    }
    if (out.length >= max) break;
  }
  out.push(...underlaysNear(v, cursor, tol, max - out.length));
  return out;
}

/**
 * An imported DXF's lines in 3D (decision 382, `underlays3d.ts`) passing within reach of the cursor on the screen, as
 * they lie in the model: walls traced over a DXF in 3D snap to its ends and lines, and a move to them.
 */
export function underlaysNear(v: ViewRefs, cursor: Pt, tol = 12, max = 4000): Seg3[] {
  const out: Seg3[] = [];
  const group = v.scene.current?.getObjectByName('cad-underlays');
  if (!group || !group.visible || max <= 0) return out;
  const project = projector(v);
  const reach = tol * 1.5;
  for (const c of group.children) {
    const ls = c as THREE.LineSegments;
    if (!ls.isLineSegments || !ls.visible) continue;
    const p = ls.geometry.getAttribute('position').array as Float32Array;
    for (let k = 0; k + 5 < p.length && out.length < max; k += 6) {
      const a = { x: p[k], y: p[k + 1], z: p[k + 2] }, b = { x: p[k + 3], y: p[k + 4], z: p[k + 5] };
      const sa = project(a), sb = project(b);
      if (!sa || !sb) continue;
      // The cursor's distance to the segment on the screen.
      const dx = sb.x - sa.x, dy = sb.y - sa.y, l2 = dx * dx + dy * dy;
      const t = l2 > 1e-9 ? Math.max(0, Math.min(1, ((cursor.x - sa.x) * dx + (cursor.y - sa.y) * dy) / l2)) : 0;
      if (Math.hypot(sa.x + dx * t - cursor.x, sa.y + dy * t - cursor.y) <= reach) out.push({ a, b });
    }
  }
  return out;
}

/**
 * The points of the clouds drawn near the cursor's ray (decision 396): within `tol` px of it on the screen, nearest the eye
 * first, kept by the view's cuts. The 3D's point snaps take them as they take an edge's ends.
 */
export function cloudPointsNear(v: ViewRefs, ray: Ray3, tol = 10, max = 6): Vec3[] {
  const layer = v.scene.current?.userData.pointClouds as PointCloudLayer | undefined;
  const c = v.camera.current, r = v.canvas.current?.getBoundingClientRect();
  if (!layer || !c || !r) return [];
  const persp = c as THREE.PerspectiveCamera, ortho = c as THREE.OrthographicCamera;
  // A pixel at a distance t along the ray, in metres.
  const reach = persp.isPerspectiveCamera
    ? ((k: number) => (t: number) => t * k)((2 * Math.tan((persp.fov * Math.PI) / 360) / r.height) * tol)
    : ((m: number) => () => m)(((ortho.top - ortho.bottom) / ortho.zoom / r.height) * tol);
  return layer.pointsNear(ray, reach, max, (p) => kept(new THREE.Vector3(p.x, p.y, p.z)));
}

/** The cursor in the 3D view, snapped (snap3d.ts): the edges near it, the body under it, the active level, the clouds' points. */
export function snapAt(v: ViewRefs, clientX: number, clientY: number, o: { level: number; base?: Vec3 | null; lock?: Lock3; skip?: ReadonlySet<string>; points?: Vec3[]; axes?: readonly Axis3[] }): Snapped3 | null {
  const r = v.canvas.current?.getBoundingClientRect();
  const ray = rayAt(v, clientX, clientY);
  if (!r || !ray) return null;
  const cursor = { x: clientX - r.left, y: clientY - r.top };
  const hit = hitAt(v.scene.current, ray, o.skip);
  return snap3({
    cursor, ray, project: projector(v), segs: segmentsNear(v, cursor, o.skip), points: [...(o.points ?? []), ...cloudPointsNear(v, ray)],
    face: hit?.point ?? null, level: onLevel(ray, o.level), base: o.base ?? null, lock: o.lock ?? null, axes: o.axes,
  });
}
