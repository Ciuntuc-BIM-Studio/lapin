/**
 * The bars in 3D on request (docs/plan-rebar.md, decision 221): with View →
 * Bars in 3D on, the 3D view asks the engine for `rebar.bodies` (hexagonal
 * tubes along every bar in space, grouped by colour) after it mounts and after
 * every diff that touches beams, columns, bars or their types, a pause of 300
 * ms after the last, and draws one mesh per colour in the group `rebar-3d`.
 * They are a drawing for the screen only: not picked, not selected, not cut,
 * never exported (the exports read the engine's scene); the group is taken
 * out and its GPU memory freed when the setting goes off or the bars change.
 */

import { useEffect, type MutableRefObject } from 'react';
import * as THREE from 'three';
import type { NodeDto } from '../engine/protocol';
import { query, useSession } from '../engine/session';
import { notify, useUi } from '../app/store';

export interface RebarBodyGroup { color: string; positions: Float32Array | number[]; indices: Int32Array | number[] }
export interface RebarBodies { groups: RebarBodyGroup[]; triangles: number; truncated: boolean }

export const REBAR_GROUP = 'rebar-3d';
/** The engine draws this many triangles at most (RebarBodies.MaxTriangles); past it a notice says the rest is left out. */
export const MAX_TRIANGLES = 2_000_000;
export const TRUNCATED_NOTICE = 'The bars in 3D are too many: only the first 2,000,000 triangles are drawn.';

/** The nodes whose change moves a bar in space: the elements with bars, the bars drawn, and the types the stirrups and column bars are set on. */
const WATCHED = new Set(['IfcBeam', 'IfcColumn', 'IfcBeamType', 'IfcColumnType', 'LapinRebar', 'LapinRebarRun']);

/** What the bars in space depend on, as one string: the same string, nothing to ask again. */
export function rebarKey(nodes: ReadonlyMap<string, NodeDto>): string {
  const o: unknown[] = [];
  for (const n of nodes.values()) if (WATCHED.has(n.type)) o.push([n.id, n.props]);
  return JSON.stringify(o);
}

const floats = (a: Float32Array | number[]) => a instanceof Float32Array ? a : Float32Array.from(a);
const indices = (a: Int32Array | number[]) => a instanceof Int32Array ? new Uint32Array(a.buffer, a.byteOffset, a.length) : Uint32Array.from(a);

/** One mesh per colour, flat shaded steel, under a group named `rebar-3d`; nothing in it carries a nodeId, so no click lands on it. */
export function rebarGroup(bodies: RebarBodies): THREE.Group {
  const group = new THREE.Group();
  group.name = REBAR_GROUP;
  for (const g of bodies.groups) {
    const positions = floats(g.positions);
    if (positions.length < 9) continue;
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geometry.setIndex(new THREE.BufferAttribute(indices(g.indices), 1));
    geometry.computeVertexNormals();
    const material = new THREE.MeshStandardMaterial({ color: g.color, roughness: 0.55, metalness: 0.25 });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.userData.rebar = true;
    // Never under the cursor: the pick walks the scene's meshes, and this one answers with nothing.
    mesh.raycast = () => undefined;
    group.add(mesh);
  }
  return group;
}

/** Free the group's geometries and materials (its own, shared with nothing). */
export function disposeRebar(group: THREE.Object3D): void {
  group.traverse((c) => {
    const m = c as THREE.Mesh;
    m.geometry?.dispose();
    const mat = m.material as THREE.Material | THREE.Material[] | undefined;
    if (Array.isArray(mat)) mat.forEach((x) => x.dispose()); else mat?.dispose();
  });
}

/** Take the bars out of a scene, freeing them. */
export function removeRebar(scene: THREE.Scene): void {
  const old = scene.getObjectByName(REBAR_GROUP);
  if (old) { scene.remove(old); disposeRebar(old); }
}

/**
 * The 3D view's side: while the setting is on, ask for the bodies 300 ms after
 * the bars last changed and swap the group in; off, take it out.
 */
export function useRebar3d(scene: MutableRefObject<THREE.Scene | null>, invalidate: MutableRefObject<() => void>): void {
  const on = useUi((s) => s.rebar3d);
  const key = useSession((s) => (on ? rebarKey(s.nodes) : ''));
  useEffect(() => {
    if (!on) {
      const s = scene.current;
      if (s) { removeRebar(s); invalidate.current(); }
      return;
    }
    let cancelled = false;
    const timer = setTimeout(() => {
      void query<RebarBodies>('rebar.bodies').then((bodies) => {
        const s = scene.current;
        if (cancelled || !bodies || !s) return;
        removeRebar(s);
        s.add(rebarGroup(bodies));
        invalidate.current();
        if (bodies.truncated) notify(TRUNCATED_NOTICE);
      });
    }, 300);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [on, key, scene, invalidate]);
  // The view going away takes its bars with it.
  useEffect(() => () => { const s = scene.current; if (s) removeRebar(s); }, [scene]);
}
