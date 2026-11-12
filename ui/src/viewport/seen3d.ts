/**
 * What a 3D view's click, hover and region may take (decision 498): only what it shows. A ray meets hidden objects too
 * (three.js does not ask whether they are visible), so a hit counts only when its object and every group above it are
 * shown (a look's hidden element or layer, a wireframe's faces, a storey or a link put away) and the point is on the kept
 * side of every clipping plane (a section box, a section shown in 3D, the cut above the storeys shown). A coarser copy
 * standing in for a far part (lod.ts) leaves its finest copy hidden but picked: that copy is still what is clicked.
 */

import * as THREE from 'three';

/** A clipping plane as the renderer keeps it: three.js keeps what has a distance ≥ 0. */
export type ClipPlane = Pick<THREE.Plane, 'distanceToPoint'>;

const TOLERANCE = 1e-3;

/** Whether `o` and every group above it are shown (an LOD level hidden for a coarser copy counts as shown). */
export function shownObject(o: THREE.Object3D | null): boolean {
  for (let x = o; x; x = x.parent) if (!x.visible && !x.userData.lodLevel) return false;
  return true;
}

/** Whether a ray's hit lies on what the view shows: an object shown, at a point no clipping plane takes away. */
export function seenHit(h: Pick<THREE.Intersection, 'object' | 'point'>, planes: readonly ClipPlane[]): boolean {
  return shownObject(h.object) && planes.every((p) => p.distanceToPoint(h.point) >= -TOLERANCE);
}

/** Whether a box (x0, y0, z0, x1, y1, z1) lies wholly on the side a clipping plane takes away: nothing of it is seen. */
export function clippedAway(box: ArrayLike<number>, planes: readonly ClipPlane[]): boolean {
  const v = new THREE.Vector3();
  return planes.some((p) => {
    for (let k = 0; k < 8; k++) {
      v.set(box[k & 1 ? 3 : 0], box[k & 2 ? 4 : 1], box[k & 4 ? 5 : 2]);
      if (p.distanceToPoint(v) >= -TOLERANCE) return false;
    }
    return true;
  });
}
