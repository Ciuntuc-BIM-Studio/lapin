/**
 * Move and Copy in the 3D view (docs/plan-3d-modelling.md, stage A; testers'
 * report #104): the selection's base point, then where it goes, both snapped
 * by snap3d.ts; the selection carried on the screen as the cursor moves, a
 * value typed at any time (a length along the way the cursor points, or the
 * displacement dx,dy[,dz]), one element.transform with the vertical part as
 * `up`. Move3D.tsx draws it and takes the mouse; this is its state and rules.
 */

import { create } from 'zustand';
import type { Vec3 } from './camera';
import type { Lock3, Snapped3 } from './snap3d';

export interface Move3dState {
  /** What moves (the selection); empty: no move. */
  ids: string[];
  /** Copy instead: the copies keep coming from the same base until Esc or Enter. */
  copy: boolean;
  /** The point it goes from; null while it is being picked. */
  base: Vec3 | null;
  /** Where the cursor is now, inferred. */
  at: Snapped3 | null;
  lock: Lock3;
  /** The value typed so far (SketchUp's Measurements). */
  text: string;
  /** Begun by dragging the selection's body: letting go applies. */
  drag: boolean;
}

const idle = { ids: [] as string[], copy: false, base: null, at: null, lock: null, text: '', drag: false };
export const useMove3d = create<Move3dState>(() => ({ ...idle }));

export function beginMove3d(ids: string[], copy = false, base: Vec3 | null = null, drag = false): void {
  useMove3d.setState({ ...idle, ids: [...ids], copy, base, drag });
}
export const endMove3d = () => useMove3d.setState({ ...idle });
export const moving3d = () => useMove3d.getState().ids.length > 0;

const sub = (a: Vec3, b: Vec3): Vec3 => ({ x: a.x - b.x, y: a.y - b.y, z: a.z - b.z });
const r6 = (v: number) => Math.round(v * 1e6) / 1e6;

/**
 * The target a typed value gives: one length goes from the base along the way the cursor points (none while the cursor
 * is on the base), two or three values (`dx,dy` or `dx,dy,dz`, also with ; between them) are the displacement itself.
 * Each value in the project's unit through `parse`; null while the text means nothing.
 */
export function typedTarget(text: string, base: Vec3, toward: Vec3 | null, parse: (s: string) => number | null): Vec3 | null {
  const parts = text.trim().split(/\s*[,;]\s*/).filter((s) => s.length > 0);
  if (!parts.length || parts.length > 3) return null;
  const v = parts.map(parse);
  if (v.some((x) => x == null)) return null;
  if (v.length === 1) {
    if (!toward) return null;
    const d = sub(toward, base), l = Math.hypot(d.x, d.y, d.z);
    if (l < 1e-9 || v[0] === 0) return null;
    const k = v[0]! / l;
    return { x: base.x + d.x * k, y: base.y + d.y * k, z: base.z + d.z * k };
  }
  return { x: base.x + v[0]!, y: base.y + v[1]!, z: base.z + (v[2] ?? 0) };
}

/** The engine command a displacement makes: element.transform in plan, its vertical part as `up`. */
export function moveArgs(ids: readonly string[], copy: boolean, by: Vec3): Record<string, unknown> {
  const o: Record<string, unknown> = { ids: [...ids], op: copy ? 'copy' : 'move', by: [r6(by.x), r6(by.y)] };
  if (Math.abs(by.z) > 1e-9) o.up = r6(by.z);
  return o;
}

/** What a displacement is worth applying: more than a tenth of a millimetre. */
export const moves = (by: Vec3) => Math.hypot(by.x, by.y, by.z) > 1e-4;

interface Rel { class: string; relating: string; related: string[] }

/**
 * What is carried on the screen with the elements moved: they, their openings and the doors and windows in them (the
 * engine moves those with their host).
 */
export function carriedWith(ids: Iterable<string>, relations: Iterable<Rel>): Set<string> {
  const out = new Set(ids);
  const rels = [...relations];
  const hosted = (cls: string, of: Set<string>) => {
    const add: string[] = [];
    for (const r of rels) if (r.class === cls && of.has(r.relating)) add.push(...r.related);
    return add;
  };
  const openings = hosted('IfcRelVoidsElement', out);
  for (const id of openings) out.add(id);
  for (const id of hosted('IfcRelFillsElement', new Set(openings))) out.add(id);
  return out;
}
