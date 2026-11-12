/**
 * What a plan's click and selection box take: annotations first (a grid's
 * axis on its own, as grid#tag), then the element whose line or face lies
 * under the point, by the line's kind (a hit area or a symbol before a cut,
 * a cut before an outline, overhead lines last). The drawing's pick lines
 * (an element's cut and seen lines, for sections) count only for what has no
 * plan lines of its own (a room's finishes), after them: a wall's pick lines
 * never take a click from the door in it. A tag gives way to the element it
 * marks where that element is under the point (a door's marker across its
 * opening). Drafting sent behind the model gives way to the element over it.
 */

import { hitsLabel } from '../platform/labelParts';
import type { NodeDto, RenderItem } from '../engine/protocol';
import type { Vec2 } from '../platform/module';
import { boxTest, segsOf, type RegionTest, type Seg } from './selectBox';
import { distToSegment, pairs, pointInPolygon } from './snap';

/** A drawing's marks: their ids and segments (x1, y1, x2, y2, …); pick: an element's own pick lines from the drawing (never drawn); data.of: the element a tag marks. */
export type Mark = { id: string; segments: number[]; pick?: boolean; behind?: boolean; data?: Record<string, unknown>; hit?: MarkHit };
/** The area a click inside takes (a hatch): even-odd loops (x, y, …); solid: opaque, so it wins over the model drawn under it. */
export type MarkHit = { solid: boolean; loops: number[][] };

/** Whether a point lies inside a mark's area (even-odd across its loops) and that area's size, to take the smallest of several. */
function insideHit(p: Vec2, hit: MarkHit): number | null {
  let inside = false, area = 0;
  for (const l of hit.loops) {
    const poly = pairs(l);
    if (pointInPolygon(p, poly)) inside = !inside;
    let a = 0;
    for (let i = 0; i < poly.length; i++) { const q = poly[(i + 1) % poly.length]; a += poly[i].x * q.y - q.x * poly[i].y; }
    area = Math.max(area, Math.abs(a) / 2);
  }
  return inside ? area : null;
}

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
/** A drawing's pick line, for an element with no plan lines of its own: as a cut, after the plan lines on a tie. */
const PICK_MARK_PRIORITY = 1;

const near = (p: Vec2, s: number[], tol: number): boolean => {
  for (let k = 0; k + 3 < s.length; k += 4) if (distToSegment(p, { x: s[k], y: s[k + 1] }, { x: s[k + 2], y: s[k + 3] }) < tol) return true;
  return false;
};
const drawsInPlan = (r: RenderItem): boolean => r.plan.some((it) => it.kind !== 'label' && it.kind !== 'grid');

/** What lies under `p` within `tol` (metres); with `types`, only elements of those classes (annotations then are not picked). */
/** The nearest element of `types` within `tol` of the point, by its plan lines (inside a closed outline: nearest of all). */
export function nearestOf(p: Vec2, tol: number, items: readonly RenderItem[], nodes: Map<string, NodeDto>, types: string[]): NodeDto | null {
  let best: NodeDto | null = null, least = tol;
  for (const r of items) {
    const e = nodes.get(r.nodeId);
    if (!e || !(types.includes('*') || types.includes(e.type))) continue;
    for (const it of r.plan) {
      if (it.kind === 'label' || it.kind === 'grid' || it.kind === 'reach') continue;
      const poly = pairs(it.points);
      let d = it.closed && pointInPolygon(p, poly) ? 0 : Infinity;
      const n = it.closed ? poly.length : poly.length - 1;
      for (let i = 0; i < n && d > 0; i++) d = Math.min(d, distToSegment(p, poly[i], poly[(i + 1) % poly.length]));
      if (d < least || (d === 0 && !best)) { least = d; best = e; }
    }
  }
  return best;
}

export function pickAt(p: Vec2, tol: number, items: readonly RenderItem[], nodes: Map<string, NodeDto>, marks: Mark[] | undefined, types?: string[]): NodeDto | null {
  const takes = (e: NodeDto) => !types || types.includes('*') || types.includes(e.type);
  let best: { e: NodeDto; prio: number } | null = null;
  for (const r of items) {
    const e = nodes.get(r.nodeId);
    if (!e || !takes(e)) continue;
    for (const it of r.plan) {
      // A device's reach (a detector's coverage, a hydrant's hose and jet) is drawn round it, never picked.
      if (it.kind === 'label' || it.kind === 'grid' || it.kind === 'reach') continue;
      const poly = pairs(it.points);
      let hit = it.closed && pointInPolygon(p, poly);
      const n = it.closed ? poly.length : poly.length - 1;
      for (let i = 0; i < n && !hit; i++) if (distToSegment(p, poly[i], poly[(i + 1) % poly.length]) < tol) hit = true;
      if (!hit) continue;
      const prio = PICK_PRIORITY[it.kind] ?? 9;
      if (!best || prio <= best.prio) best = { e, prio };
    }
  }
  // Annotations (dimensions, texts, grids, section lines) lie over the model: they are picked first, a tag but where its element is under the point.
  if (!types && marks) {
    // Drafting sent behind the model (decision 339) only where the model has nothing but a room, an outline or overhead lines.
    const under = !best || best.prio >= PICK_PRIORITY.room;
    for (const m of marks) {
      // A label's texts pick it too (a marker's, a void's: decision 292).
      if (m.pick || (m.behind && !under) || !(near(p, m.segments, tol) || hitsLabel(p, m.data, tol))) continue;
      const hit = markOwner(m.id, nodes);
      if (!hit) continue;
      if (typeof m.data?.of === 'string' && m.data.of === best?.e.id) continue;
      if (hit.pick === hit.owner.id) return hit.owner;
      // One axis of a grid (grid#tag): selected on its own.
      return { ...hit.owner, id: m.id, name: `Axis ${m.id.slice(m.id.indexOf('#') + 1)}` };
    }
  }
  // A click inside a hatch takes it, after every line and outline (report #122): the smallest area first; a solid one lies over the model,
  // a pattern's lines leave what is under them to be clicked, so it is taken only where nothing else is.
  if (!types && marks) {
    const under = !best || best.prio >= PICK_PRIORITY.room;
    let found: { e: NodeDto; area: number } | null = null;
    for (const m of marks) {
      if (!m.hit || m.pick || !(m.hit.solid ? !m.behind || under : !best)) continue;
      const area = insideHit(p, m.hit);
      const owner = area != null ? nodes.get(m.id) : undefined;
      if (area != null && owner && (!found || area < found.area)) found = { e: owner, area };
    }
    if (found) return found.e;
  }
  // The drawing's pick lines of what draws nothing of its own in the plan.
  if (marks && (!best || best.prio > PICK_MARK_PRIORITY)) {
    const drawn = new Set(items.filter(drawsInPlan).map((r) => r.nodeId));
    for (const m of marks) {
      if (!m.pick || !near(p, m.segments, tol)) continue;
      const e = markOwner(m.id, nodes)?.owner;
      if (e && !drawn.has(e.id) && takes(e)) return e;
    }
  }
  return best?.e ?? null;
}

/** What a selection box from world corner `p` to `q` takes (see boxTest): elements by their lines, then annotations. */
export function boxIds(p: Vec2, q: Vec2, windowMode: boolean, items: readonly RenderItem[], nodes: Map<string, NodeDto>, marks: Mark[] | undefined): string[] {
  return regionIds(boxTest(p, q, windowMode), items, nodes, marks);
}

/** What a selection region (a box or a lasso) takes: elements by their lines, then annotations. */
export function regionIds(takes: RegionTest, items: readonly RenderItem[], nodes: Map<string, NodeDto>, marks: Mark[] | undefined): string[] {
  const ids: string[] = [];
  for (const r of items) {
    const segs: Seg[] = [];
    for (const it of r.plan) {
      if (it.kind === 'label' || it.kind === 'hit' || it.kind === 'grid' || it.kind === 'reach') continue;
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
