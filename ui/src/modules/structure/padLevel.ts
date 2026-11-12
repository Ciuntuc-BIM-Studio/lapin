/**
 * A pad footing's level and its columns (decision 507): the points the Pad footing tool snaps to (the columns of the plan's
 * storey and of the storey above, so a foundation plan puts a pad right under a column), and what Pad footings under
 * columns asks for (the selected columns, else the plan's storey).
 */

import type { Vec2 } from '../../platform/module';
import { relatingOf, storeys, type ModelState } from '../../engine/model';

const point = (v: unknown): Vec2 | null =>
  Array.isArray(v) && typeof v[0] === 'number' && typeof v[1] === 'number' ? { x: v[0], y: v[1] } : null;

/** The insertion points of the columns standing on the storey and on the one above it. */
export function columnSnapPoints(s: ModelState, storeyId: string | null): Vec2[] {
  if (!storeyId) return [];
  const all = storeys(s);
  const i = all.findIndex((x) => x.id === storeyId);
  const on = new Set([storeyId, ...(i >= 0 && i + 1 < all.length ? [all[i + 1].id] : [])]);
  const o: Vec2[] = [];
  for (const n of s.nodes.values()) {
    if (n.type !== 'IfcColumn') continue;
    const at = point(n.props.at);
    const storey = relatingOf(s, n.id, 'IfcRelContainedInSpatialStructure');
    if (at && storey && on.has(storey.id)) o.push(at);
  }
  return o;
}

/** Pad footings under columns: the selected columns, else every column of the plan's storey (the engine takes the storey above when it has none). */
export function underColumnsArgs(s: ModelState, selected: string[], storeyId: string | null): Record<string, unknown> | null {
  const ids = selected.filter((id) => s.nodes.get(id)?.type === 'IfcColumn');
  if (ids.length) return { ids };
  return storeyId ? { storeyId } : null;
}

/** A pad's level as the engine works it out (structure.padLevel). */
export interface PadLevel {
  top: number;
  bottom: number;
  /** column: its top on the base of the column over it; own: its own top; storey: its storey's elevation, its type's bottom under it. */
  rule: 'column' | 'own' | 'storey';
  follows: boolean;
  column?: { id: string; name: string; storey: string | null };
}
