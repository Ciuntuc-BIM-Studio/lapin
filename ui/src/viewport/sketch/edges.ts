/**
 * Other elements' edges as inference lines (stage B, decision 375): the edges
 * of the bodies drawn in the 3D view that lie in the sketch's plane (a wall's
 * corner on a slab's top, the next block's side flush with this one), and the
 * points where edges pass through it, in the plane's (x, y). Both ways of
 * sketching snap to them.
 */

import * as THREE from 'three';
import type { Vec2 } from '../../platform/module';
import type { Vec3 } from '../camera';
import { toPlane, type WorkFrame } from '../workPlane';
import type { Seg2 } from './inference';

const off = (f: WorkFrame, p: Vec3) => (p.x - f.origin.x) * f.normal.x + (p.y - f.origin.y) * f.normal.y + (p.z - f.origin.z) * f.normal.z;

/** Of segments in the model, those lying in the plane (both ends within tol of it), and the points where the others pass through it. */
export function edgesInPlane(segs: readonly [Vec3, Vec3][], f: WorkFrame, tol = 1e-3): { edges: Seg2[]; points: Vec2[] } {
  const edges: Seg2[] = [], points: Vec2[] = [];
  for (const [a, b] of segs) {
    const da = off(f, a), db = off(f, b);
    if (Math.abs(da) <= tol && Math.abs(db) <= tol) edges.push({ a: toPlane(f, a), b: toPlane(f, b) });
    else if (Math.abs(da) <= tol) points.push(toPlane(f, a));
    else if (Math.abs(db) <= tol) points.push(toPlane(f, b));
    else if (da * db < 0) {
      const t = da / (da - db);
      points.push(toPlane(f, { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t, z: a.z + (b.z - a.z) * t }));
    }
  }
  return { edges, points };
}

/** A mesh's sharp edges (20°), once per geometry. */
const cache = new WeakMap<THREE.BufferGeometry, Float32Array>();
function edgesOf(g: THREE.BufferGeometry): Float32Array {
  let e = cache.get(g);
  if (!e) { const eg = new THREE.EdgesGeometry(g, 20); e = eg.getAttribute('position').array as Float32Array; eg.dispose(); cache.set(g, e); }
  return e;
}

/** The edges of the bodies drawn whose bounds reach the plane, as model segments (at most `max`). */
export function sceneSegments(scene: THREE.Scene | null, f: WorkFrame, max = 6000): [Vec3, Vec3][] {
  const out: [Vec3, Vec3][] = [];
  if (!scene) return out;
  const n = new THREE.Vector3(f.normal.x, f.normal.y, f.normal.z), o = new THREE.Vector3(f.origin.x, f.origin.y, f.origin.z);
  const plane = new THREE.Plane().setFromNormalAndCoplanarPoint(n, o);
  const box = new THREE.Box3(), a = new THREE.Vector3(), b = new THREE.Vector3();
  scene.traverse((obj) => {
    if (out.length >= max) return;
    const m = obj as THREE.Mesh;
    if (!m.isMesh || !m.visible || !m.userData.nodeId || (m as THREE.InstancedMesh).isInstancedMesh || !m.geometry) return;
    if (!m.geometry.boundingBox) m.geometry.computeBoundingBox();
    box.copy(m.geometry.boundingBox!).applyMatrix4(m.matrixWorld).expandByScalar(1e-3);
    if (!box.intersectsPlane(plane)) return;
    const e = edgesOf(m.geometry);
    for (let k = 0; k + 5 < e.length && out.length < max; k += 6) {
      a.set(e[k], e[k + 1], e[k + 2]).applyMatrix4(m.matrixWorld);
      b.set(e[k + 3], e[k + 4], e[k + 5]).applyMatrix4(m.matrixWorld);
      out.push([{ x: a.x, y: a.y, z: a.z }, { x: b.x, y: b.y, z: b.z }]);
    }
  });
  return out;
}
