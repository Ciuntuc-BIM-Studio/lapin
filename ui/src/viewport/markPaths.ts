/**
 * A drawing's marks lit over a plan, section or elevation (hover, selection, a tag's target) as SVG paths in screen pixels.
 * One element can own several marks (its own and an inset's copy of it, mapped into the host; a window's opening lines and
 * its pick outline in an elevation), so each path is keyed by its place in the list too: keyed by the owner alone, React met
 * the same key twice and left stale paths behind, which then stood still on the screen as the view was zoomed or panned and
 * stayed lit after the pointer left (forum topic 252).
 */

import type { Vec2 } from '../platform/module';

export interface MarkPath { key: string; d: string }

export function markPaths(marks: readonly { id: string; segments: number[] }[], prefix: string, toScreen: (p: Vec2) => Vec2): MarkPath[] {
  return marks.map((m, i) => ({
    key: `${prefix}:${m.id}:${i}`,
    d: Array.from({ length: Math.floor(m.segments.length / 4) }, (_, k) => {
      const a = toScreen({ x: m.segments[4 * k], y: m.segments[4 * k + 1] }), b = toScreen({ x: m.segments[4 * k + 2], y: m.segments[4 * k + 3] });
      return `M${a.x} ${a.y}L${b.x} ${b.y}`;
    }).join(''),
  }));
}
