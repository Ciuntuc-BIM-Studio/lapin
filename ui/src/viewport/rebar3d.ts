/**
 * The bars in 3D (docs/plan-rebar.md, decisions 221 and 402): the 3D view asks
 * the engine for `rebar.bodies` with its id (hexagonal tubes along every bar in
 * space, grouped by colour) after it mounts and after every diff that touches
 * beams, columns, bars, their types or the view's graphics, a pause of 300 ms
 * after the last, and draws one mesh per colour in the group `rebar-3d`. They
 * show under the view's Rebar category (Visibility / Graphics): off in an
 * architectural 3D until shown, on in a structural one; View → Bars in 3D and
 * the Rebar 2D tab's button switch that category. They are a drawing for the
 * screen only: not picked, not selected, not cut, never exported (the exports
 * read the engine's scene); the group is taken out and its GPU memory freed
 * when the category hides them or the bars change.
 */

import { useEffect, type MutableRefObject } from 'react';
import * as THREE from 'three';
import type { NodeDto } from '../engine/protocol';
import { execute, query, useSession } from '../engine/session';
import { notify, useUi } from '../app/store';

export interface RebarBodyGroup { color: string; positions: Float32Array | number[]; indices: Int32Array | number[] }
export interface RebarBodies { groups: RebarBodyGroup[]; triangles: number; truncated: boolean; hidden?: boolean }

export const REBAR_GROUP = 'rebar-3d';
/** The engine draws this many triangles at most (RebarBodies.MaxTriangles); past it a notice says the rest is left out. */
export const MAX_TRIANGLES = 2_000_000;
export const TRUNCATED_NOTICE = 'The bars in 3D are too many: only the first 2,000,000 triangles are drawn.';

/** The nodes whose change moves a bar in space: the elements with bars, the bars drawn, and the types the stirrups and column bars are set on. */
const WATCHED = new Set(['IfcBeam', 'IfcColumn', 'IfcBeamType', 'IfcColumnType', 'LapinRebar', 'LapinRebarRun']);

/** What the bars in space depend on, as one string: the same string, nothing to ask again. The 3D view's graphics too (the project's {3D}, its styles and sets, or a saved 3D view). */
export function rebarKey(nodes: ReadonlyMap<string, NodeDto>, view3dId: string | null = null): string {
  const o: unknown[] = [];
  for (const n of nodes.values()) {
    if (WATCHED.has(n.type)) o.push([n.id, n.props]);
    else if (n.type === 'IfcProject') o.push(['project', n.props.view3d ?? null, n.props.viewStyles ?? null, n.props.graphicsSets ?? null]);
  }
  if (view3dId) o.push(['view', nodes.get(view3dId)?.props ?? null]);
  return JSON.stringify(o);
}

/** The key worked out once for a map of nodes (the store calls its selector on every change, the nodes' map changes only with a diff). */
let lastKey: { nodes: ReadonlyMap<string, NodeDto>; view: string | null; key: string } | null = null;
function cachedKey(nodes: ReadonlyMap<string, NodeDto>, view: string | null): string {
  if (lastKey?.nodes !== nodes || lastKey.view !== view) lastKey = { nodes, view, key: rebarKey(nodes, view) };
  return lastKey.key;
}

/** The categories row the dialog and this button share: shown when it says so, else as the view hides by default. */
interface Visibility { graphics?: { categories?: Record<string, Record<string, unknown>> } | null; hiddenByDefault?: string[] }

/**
 * View → Bars in 3D and the Rebar 2D tab's button (decision 402): the 3D view's Rebar category switched, its other
 * settings (halftone, transparency, subcategories) kept; returns whether the bars now show.
 */
export async function toggleRebar3d(view3dId: string | null = useUi.getState().view3dId): Promise<boolean> {
  const id = view3dId ?? '3d';
  const v = await query<Visibility>('view.visibility', { id });
  const row = { ...(v?.graphics?.categories?.rebar ?? {}) };
  const byDefault = (v?.hiddenByDefault ?? []).includes('rebar');
  const hidden = typeof row.hidden === 'boolean' ? row.hidden : byDefault;
  // Shown again where they show by default: no row needed; hidden where they hide by default: none either.
  if (!hidden === byDefault) delete row.hidden; else row.hidden = !hidden;
  await execute('view.graphics', { id, category: 'rebar', value: Object.keys(row).length ? row : null });
  return hidden;
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
 * The 3D view's side: ask for the bodies as the view shows them 300 ms after the
 * bars or the view's graphics last changed and swap the group in; hidden by the
 * view's Rebar category (or none at all), take it out.
 */
export function useRebar3d(scene: MutableRefObject<THREE.Scene | null>, invalidate: MutableRefObject<() => void>): void {
  const view3dId = useUi((s) => s.view3dId);
  const key = useSession((s) => cachedKey(s.nodes, view3dId));
  useEffect(() => {
    let cancelled = false;
    const timer = setTimeout(() => {
      void query<RebarBodies>('rebar.bodies', { view: view3dId ?? '3d' }).then((bodies) => {
        const s = scene.current;
        if (cancelled || !s) return;
        removeRebar(s);
        if (bodies && !bodies.hidden && bodies.groups.length > 0) s.add(rebarGroup(bodies));
        invalidate.current();
        if (bodies?.truncated) notify(TRUNCATED_NOTICE);
      }).catch(() => { /* no rebar module in this edition: nothing to draw */ });
    }, 300);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [key, view3dId, scene, invalidate]);
  // The view going away takes its bars with it.
  useEffect(() => () => { const s = scene.current; if (s) removeRebar(s); }, [scene]);
}
