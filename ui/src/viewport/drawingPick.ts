/**
 * What a section's or an elevation's click takes: the mark whose line lies
 * nearest under the point (on a tie the first, annotations and drafted shapes
 * before the elements' pick lines, in the order the engine drew them; a bar
 * against its stirrup is had by clicking its own side), else the element whose
 * smallest cut or filled loop holds the point, as a plan picks inside a cut.
 * An inset view's lines give way to what is drawn through it (a bar seen in
 * the cross-section): the inset is taken only when nothing else is there.
 */

import type { NodeDto } from '../engine/protocol';
import type { MarkDto, Vec2 } from '../platform/module';
import { markOwner } from './planPick';
import { distToSegment, pairs, pointInPolygon } from './snap';

const area = (poly: Vec2[]): number => {
  let a = 0;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) a += (poly[j].x + poly[i].x) * (poly[j].y - poly[i].y);
  return Math.abs(a / 2);
};

/** The id a click at `p` (view coordinates) picks, within `tol` metres of a line; null when nothing is there. `accept`: only owners a tool works on (a split takes bars, not the stirrups beside them). */
export function pickDrawing(p: Vec2, tol: number, marks: MarkDto[] | undefined, nodes: Map<string, NodeDto>, accept?: (owner: NodeDto) => boolean): string | null {
  let inset: string | null = null;
  let near: { pick: string; d: number } | null = null;
  let behind: string | null = null;
  for (const m of marks ?? []) {
    const s = m.segments;
    let d = Infinity;
    for (let k = 0; k + 3 < s.length; k += 4) d = Math.min(d, distToSegment(p, { x: s[k], y: s[k + 1] }, { x: s[k + 2], y: s[k + 3] }));
    if (d >= tol || (near && d >= near.d)) continue;
    const hit = markOwner(m.id, nodes);
    if (!hit || (accept && !accept(hit.owner))) continue;
    if (hit.owner.type === 'LapinInset') inset ??= hit.pick;
    // Drafting sent behind the model (decision 339): taken only when nothing over it is there.
    else if (m.behind) behind ??= hit.pick;
    else near = { pick: hit.pick, d };
  }
  if (near) return near.pick;
  if (inset) return inset;
  let best: { pick: string; area: number } | null = null;
  for (const m of marks ?? []) {
    const loops = (m.data as { loops?: number[][] } | undefined)?.loops;
    if (!loops) continue;
    for (const raw of loops) {
      const poly = pairs(raw);
      if (poly.length < 3 || !pointInPolygon(p, poly)) continue;
      const a = area(poly);
      if (best && a >= best.area) continue;
      const hit = markOwner(m.id, nodes);
      if (hit && (!accept || accept(hit.owner))) best = { pick: hit.pick, area: a };
    }
  }
  return best?.pick ?? behind;
}
