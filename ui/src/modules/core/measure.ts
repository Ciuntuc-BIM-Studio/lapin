/**
 * Measure (ME, the second quick tool; a tester's report, 03.11.2026: the button was there and did nothing): click points in a
 * plan, a section or an elevation, Enter to finish; while it runs the live input shows each leg's length and angle, and at the
 * end the notice gives each leg, the total and, from three points on, the area they enclose. It draws nothing.
 */

import { Ruler } from 'lucide-react';
import type { ToolDef, Vec2 } from '../../platform/module';
import { notify } from '../../app/store';
import { lengthText } from '../../engine/unitsStore';

/** The legs' lengths, their total, and the area the points enclose (from three on; m²). */
export function measured(points: Vec2[]): { legs: number[]; total: number; area: number | null } {
  const legs = points.slice(1).map((p, i) => Math.hypot(p.x - points[i].x, p.y - points[i].y));
  let a = 0;
  for (let i = 0; i < points.length; i++) {
    const p = points[i], q = points[(i + 1) % points.length];
    a += p.x * q.y - q.x * p.y;
  }
  return { legs, total: legs.reduce((s, l) => s + l, 0), area: points.length >= 3 ? Math.abs(a) / 2 : null };
}

/**
 * The same in space, for points clicked in the 3D view with their heights: each leg's true length (a ridge to an eave is
 * longer than its plan), the total, and the plan area they enclose.
 */
export function measured3d(points: Vec2[], heights: number[]): { legs: number[]; total: number; area: number | null; rise: number } {
  const flat = measured(points);
  const legs = points.slice(1).map((p, i) => Math.hypot(p.x - points[i].x, p.y - points[i].y, (heights[i + 1] ?? 0) - (heights[i] ?? 0)));
  return { legs, total: legs.reduce((s, l) => s + l, 0), area: flat.area, rise: (heights[heights.length - 1] ?? 0) - (heights[0] ?? 0) };
}

export const measure: ToolDef = {
  id: 'measure', label: 'Measure', icon: Ruler, shortcut: 'ME',
  gesture: { kind: 'polyline' },
  complete: ({ points, heights }) => {
    if (points.length < 2) return;
    // Clicked in the 3D view on points at different heights: the distance in space.
    if (heights && heights.length === points.length && heights.some((h) => Math.abs(h - heights[0]) > 1e-3)) {
      const m = measured3d(points, heights);
      const parts = [`Length ${lengthText(m.total)}`];
      if (m.legs.length > 1) parts.push(`legs ${m.legs.map((l) => lengthText(l)).join(' + ')}`);
      else {
        const [a, b] = points;
        parts.push(`Δx ${lengthText(Math.abs(b.x - a.x))}`, `Δy ${lengthText(Math.abs(b.y - a.y))}`, `Δz ${lengthText(Math.abs(m.rise))}`);
      }
      notify(parts.join(' · '));
      return;
    }
    const { legs, total, area } = measured(points);
    const parts = [`Length ${lengthText(total)}`];
    if (legs.length > 1) parts.push(`legs ${legs.map((l) => lengthText(l)).join(' + ')}`);
    else {
      const [a, b] = points;
      parts.push(`Δx ${lengthText(Math.abs(b.x - a.x))}`, `Δy ${lengthText(Math.abs(b.y - a.y))}`, `angle ${(Math.atan2(b.y - a.y, b.x - a.x) * 180 / Math.PI).toFixed(1)}°`);
    }
    if (area != null && area > 1e-9) parts.push(`area ${area.toFixed(2)} m²`);
    notify(parts.join(' · '));
  },
};
