/**
 * What a section's or an elevation's click takes: the first mark whose line
 * lies under the point (annotations and drafted shapes before the elements'
 * pick lines, in the order the engine drew them), else the element whose
 * smallest cut or filled loop holds the point, as a plan picks inside a cut.
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

/** The id a click at `p` (view coordinates) picks, within `tol` metres of a line; null when nothing is there. */
export function pickDrawing(p: Vec2, tol: number, marks: MarkDto[] | undefined, nodes: Map<string, NodeDto>): string | null {
  for (const m of marks ?? []) {
    const s = m.segments;
    for (let k = 0; k + 3 < s.length; k += 4) {
      if (distToSegment(p, { x: s[k], y: s[k + 1] }, { x: s[k + 2], y: s[k + 3] }) >= tol) continue;
      const hit = markOwner(m.id, nodes);
      if (hit) return hit.pick;
    }
  }
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
      if (hit) best = { pick: hit.pick, area: a };
    }
  }
  return best?.pick ?? null;
}
