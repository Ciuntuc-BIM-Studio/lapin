/**
 * What a plan's click and selection box take: annotations first (a grid's
 * axis on its own, as grid#tag), then the element whose line or face lies
 * under the point, by the line's kind (a hit area or a symbol before a cut,
 * a cut before an outline, overhead lines last).
 */

import type { NodeDto, RenderItem } from '../engine/protocol';
import type { Vec2 } from '../platform/module';
import { boxTest, segsOf, type Seg } from './selectBox';
import { distToSegment, pairs, pointInPolygon } from './snap';

/** A drawing's annotations: their ids and segments (x1, y1, x2, y2, …). */
export type Mark = { id: string; segments: number[] };

const PICK_PRIORITY: Record<string, number> = { hit: 0, symbol: 0, heavy: 0, cut: 1, room: 1.5, outline: 2, overhead: 8, roomIsland: 9 };

/** What lies under `p` within `tol` (metres); with `types`, only elements of those classes (annotations then are not picked). */
export function pickAt(p: Vec2, tol: number, items: RenderItem[], nodes: Map<string, NodeDto>, marks: Mark[] | undefined, types?: string[]): NodeDto | null {
  // Annotations (dimensions, texts, grids, section lines) lie over the model: they are picked first.
  if (!types && marks) {
    for (const m of marks) {
      const s = m.segments;
      for (let k = 0; k + 3 < s.length; k += 4) {
        if (distToSegment(p, { x: s[k], y: s[k + 1] }, { x: s[k + 2], y: s[k + 3] }) >= tol) continue;
        if (nodes.get(m.id)) return nodes.get(m.id)!;
        // One axis of a grid (grid#tag): selected on its own.
        const owner = m.id.includes('#') ? nodes.get(m.id.slice(0, m.id.indexOf('#'))) : undefined;
        if (owner) return { ...owner, id: m.id, name: `Axis ${m.id.slice(m.id.indexOf('#') + 1)}` };
      }
    }
  }
  let best: { e: NodeDto; prio: number } | null = null;
  for (const r of items) {
    const e = nodes.get(r.nodeId);
    if (!e || (types && !types.includes(e.type))) continue;
    for (const it of r.plan) {
      if (it.kind === 'label' || it.kind === 'grid') continue;
      const poly = pairs(it.points);
      let hit = it.closed && pointInPolygon(p, poly);
      const n = it.closed ? poly.length : poly.length - 1;
      for (let i = 0; i < n && !hit; i++) if (distToSegment(p, poly[i], poly[(i + 1) % poly.length]) < tol) hit = true;
      if (!hit) continue;
      const prio = PICK_PRIORITY[it.kind] ?? 9;
      if (!best || prio <= best.prio) best = { e, prio };
    }
  }
  return best?.e ?? null;
}

/** What a selection box from world corner `p` to `q` takes (see boxTest): elements by their lines, then annotations. */
export function boxIds(p: Vec2, q: Vec2, windowMode: boolean, items: RenderItem[], nodes: Map<string, NodeDto>, marks: Mark[] | undefined): string[] {
  const takes = boxTest(p, q, windowMode);
  const ids: string[] = [];
  for (const r of items) {
    const segs: Seg[] = [];
    for (const it of r.plan) {
      if (it.kind === 'label' || it.kind === 'hit' || it.kind === 'grid') continue;
      const ps = pairs(it.points);
      for (let i = 0; i + 1 < ps.length + (it.closed ? 1 : 0); i++) segs.push([ps[i], ps[(i + 1) % ps.length]]);
    }
    if (nodes.get(r.nodeId) && takes(segs)) ids.push(r.nodeId);
  }
  for (const m of marks ?? []) {
    const segs = segsOf(m.segments);
    const owner = m.id.includes('#') ? m.id.slice(0, m.id.indexOf('#')) : m.id;
    if (nodes.get(owner) && takes(segs) && !ids.includes(m.id)) ids.push(m.id);
  }
  return ids;
}
