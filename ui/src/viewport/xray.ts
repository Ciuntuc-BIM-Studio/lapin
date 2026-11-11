/**
 * The X-ray visual style (report #142): every element drawn see-through, edges kept, the selection solid. It is a look
 * laid over the elements' own (a phase's, the 4D simulation's), so the plain meshes and the instanced ones, picking
 * and the selection's highlight all go through the paths they already have. The looks it makes are kept, so an element
 * whose look did not change is not painted again.
 */

import type { Look } from '../platform/looks';
import { XRAY_OPACITY } from '../app/visualStyle';

const seeThrough = (l: Look): Look => {
  const out: Look = { ...l, opacity: Math.min(l.opacity ?? 1, XRAY_OPACITY) };
  if (l.parts) out.parts = Object.fromEntries(Object.entries(l.parts).map(([k, v]) => [k, xrayLook(v)]));
  return out;
};

const NONE: Look = { opacity: XRAY_OPACITY };
const made = new WeakMap<Look, Look>();

/** A look made see-through (none: the plain see-through look). */
export function xrayLook(l: Look | undefined): Look {
  if (!l) return NONE;
  let x = made.get(l);
  if (!x) { x = seeThrough(l); made.set(l, x); }
  return x;
}

/** The looks of `ids` with X-ray laid over all but `except` (the selection, drawn solid). */
export function xrayLooks(looks: Record<string, Look | undefined>, ids: Iterable<string>, except: ReadonlySet<string>): Record<string, Look | undefined> {
  const out: Record<string, Look | undefined> = { ...looks };
  for (const id of ids) if (!except.has(id) && !looks[id]?.hidden) out[id] = xrayLook(looks[id]);
  return out;
}
