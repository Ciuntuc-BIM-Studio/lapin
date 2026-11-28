import type { Vec2 } from '../../platform/module';
import type { PreviewDto } from './furniture';

type Item = { closed: boolean; points: number[] };

/**
 * The plan outline of a piece as the Place tool's ghost: the preview's items (x, y pairs in the piece's own frame, centred,
 * y up, its front along local -y) turned by `rotation` degrees counter-clockwise (the front then looks along (sin, -cos), as
 * the engine's placement and the grips do) and moved to `at`. A closed item gets its first point repeated, an open one stays open.
 */
export function ghostLines(items: Item[], at: Vec2, rotation = 0): Vec2[][] {
  const a = (rotation * Math.PI) / 180, cos = Math.cos(a), sin = Math.sin(a);
  const lines: Vec2[][] = [];
  for (const it of items) {
    const line: Vec2[] = [];
    for (let k = 0; k + 1 < it.points.length; k += 2) {
      const x = it.points[k], y = it.points[k + 1];
      line.push({ x: at.x + x * cos - y * sin, y: at.y + x * sin + y * cos });
    }
    if (line.length < 2) continue;
    if (it.closed) line.push(line[0]);
    lines.push(line);
  }
  return lines;
}

/** The items to draw for a preview: its plan, or, for a piece without plan items (a model), a rectangle width x depth. */
export function outlineOf(p: Pick<PreviewDto, 'plan' | 'width' | 'depth'>): Item[] {
  if (p.plan?.some((i) => i.points.length >= 4)) return p.plan;
  const w = Number(p.width) / 2, d = Number(p.depth) / 2;
  if (!(w > 0) || !(d > 0)) return [];
  return [{ closed: true, points: [-w, -d, w, -d, w, d, -w, d] }];
}
