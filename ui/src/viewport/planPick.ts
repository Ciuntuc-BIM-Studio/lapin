/**
 * What a plan's click and selection box take: annotations first (a grid's
 * axis on its own, as grid#tag), then the element whose line or face lies
 * under the point, by the line's kind (a hit area or a symbol before a cut,
 * a cut before an outline, overhead lines last).
 */

import type { NodeDto, RenderItem } from '../engine/protocol';
import type { Vec2 } from '../platform/module';
import { boxTest, segsOf, type RegionTest, type Seg } from './selectBox';
import { distToSegment, pairs, pointInPolygon } from './snap';

/** A drawing's annotations: their ids and segments (x1, y1, x2, y2, …). */
export type Mark = { id: string; segments: number[] };

/**
 * The element a mark belongs to when its id is not an element's: a grid's axis is `grid#tag` (selected on its own, as that id),
 * a part drawn for an element is `${owner}:key` (a type's stirrups on a beam, a bar seen in another view: a click takes the owner).
 */
export function markOwner(id: string, nodes: Map<string, NodeDto>): { owner: NodeDto; pick: string } | null {
  const own = nodes.get(id);
  if (own) return { owner: own, pick: id };
  const hash = id.indexOf('#');
  if (hash > 0) { const o = nodes.get(id.slice(0, hash)); return o ? { owner: o, pick: id } : null; }
  const colon = id.indexOf(':');
  if (colon > 0) { const o = nodes.get(id.slice(0, colon)); return o ? { owner: o, pick: o.id } : null; }
  return null;
}

/** Whether a mark is drawn as selected: its own id is, or its owner's (a part's). */
export const markSelected = (id: string, selected: string[]): boolean => selected.includes(id) || (id.includes(':') && !id.includes('#') && selected.includes(id.slice(0, id.indexOf(':'))));

const PICK_PRIORITY: Record<string, number> = { hit: 0, symbol: 0, heavy: 0, cut: 1, room: 1.5, outline: 2, overhead: 8, roomIsland: 9 };

/** What lies under `p` within `tol` (metres); with `types`, only elements of those classes (annotations then are not picked). */
export function pickAt(p: Vec2, tol: number, items: RenderItem[], nodes: Map<string, NodeDto>, marks: Mark[] | undefined, types?: string[]): NodeDto | null {
  // Annotations (dimensions, texts, grids, section lines) lie over the model: they are picked first.
  if (!types && marks) {
    for (const m of marks) {
      const s = m.segments;
      for (let k = 0; k + 3 < s.length; k += 4) {
        if (distToSegment(p, { x: s[k], y: s[k + 1] }, { x: s[k + 2], y: s[k + 3] }) >= tol) continue;
        const hit = markOwner(m.id, nodes);
        if (!hit) continue;
        if (hit.pick === hit.owner.id) return hit.owner;
        // One axis of a grid (grid#tag): selected on its own.
        return { ...hit.owner, id: m.id, name: `Axis ${m.id.slice(m.id.indexOf('#') + 1)}` };
      }
    }
  }
  let best: { e: NodeDto; prio: number } | null = null;
  for (const r of items) {
    const e = nodes.get(r.nodeId);
    if (!e || (types && !types.includes('*') && !types.includes(e.type))) continue;
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
  return regionIds(boxTest(p, q, windowMode), items, nodes, marks);
}

/** What a selection region (a box or a lasso) takes: elements by their lines, then annotations. */
export function regionIds(takes: RegionTest, items: RenderItem[], nodes: Map<string, NodeDto>, marks: Mark[] | undefined): string[] {
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
  return markIds(takes, nodes, marks, ids);
}

/** The annotations and drafted shapes a region takes (a grid's axis on its own), added to `ids`. */
export function markIds(takes: RegionTest, nodes: Map<string, NodeDto>, marks: Mark[] | undefined, ids: string[] = []): string[] {
  for (const m of marks ?? []) {
    const hit = markOwner(m.id, nodes);
    if (hit && takes(segsOf(m.segments)) && !ids.includes(hit.pick)) ids.push(hit.pick);
  }
  return ids;
}
