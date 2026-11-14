/**
 * Inset views (decision 574): how the host view of an inset maps to its source. The engine draws each inset's source
 * at its point (`at`), its middle (`centre`) there, by `factor` host metres per source metre; what the source has of its
 * own (a bar, a drafted line, a dimension) comes through as its own mark with the mapping it came by (`via`). So:
 *
 * - what a tool draws over an inset goes on the source view, in its own coordinates (`insetUnder`);
 * - an element seen through an inset shows its grips where the inset shows it, and a drag moves it on its source
 *   (`throughInset`), whatever module made the grips; a module that maps its grips itself (rebar's bars) gives them
 *   the host's view, and they are left as they are.
 */

import type { Grip, MarkDto, Vec2 } from '../platform/module';
import type { Shape } from './cad/shapes';

/** The tools that place an inset: clicked over one, they stay on the host (the others draw on the inset's source). */
export const PLACES_INSETS: ReadonlySet<string> = new Set(['view.inset', 'rebar.inset']);

/**
 * Whether a plan's tool draws on an inset's source when clicked over the inset: the annotations' and rebar's (a dimension, a
 * text, a bar); the model's (a wall, a slab) stay on the plan. In a section or an elevation every tool does, as rebar's always have.
 */
export const drawsThroughInsets = (toolId: string): boolean => !PLACES_INSETS.has(toolId) && /^(drafting|rebar|annotate)\./.test(toolId);

/** How a mark seen through an inset view maps between its source's coordinates and the host's. */
export interface Via { inset: string; host: string; source: string; at: number[]; centre: number[]; factor: number }

/** The mapping a mark came by, when it is seen through an inset. */
export function viaOf(mark?: MarkDto): Via | null {
  const v = (mark?.data as { via?: Via } | undefined)?.via;
  return v && v.host && v.source && Array.isArray(v.at) && Array.isArray(v.centre) && typeof v.factor === 'number' && v.factor > 0 ? v : null;
}

/** Source → host and host → source points of a mapping. */
export function mapper(via: Pick<Via, 'at' | 'centre' | 'factor'>): { show: (p: Vec2) => Vec2; back: (p: Vec2) => Vec2 } {
  const { at, centre, factor } = via;
  return {
    show: (p) => ({ x: at[0] + (p.x - centre[0]) * factor, y: at[1] + (p.y - centre[1]) * factor }),
    back: (p) => ({ x: centre[0] + (p.x - at[0]) / factor, y: centre[1] + (p.y - at[1]) / factor }),
  };
}

/** The inset all the points fall in (the host's marks), with its source and the host → source mapping; null when none. */
export function insetUnder(marks: MarkDto[] | undefined, points: Vec2[]): { inset: string; source: string; map: (p: Vec2) => Vec2 } | null {
  for (const m of marks ?? []) {
    const d = m.data as { source?: string; at?: number[]; centre?: number[]; factor?: number; bounds?: number[] } | undefined;
    if (!d?.source || !d.bounds || !d.at || !d.centre || !d.factor) continue;
    const [x0, y0, x1, y1] = d.bounds;
    if (points.length > 0 && points.every((p) => p.x >= x0 && p.x <= x1 && p.y >= y0 && p.y <= y1)) {
      const { back } = mapper({ at: d.at, centre: d.centre, factor: d.factor });
      return { inset: m.id, source: d.source, map: back };
    }
  }
  return null;
}

/**
 * A grip made for the source view, shown in the host through an inset: at the point the inset shows, its drag taken back
 * to the source's coordinates; a grip already on the host (its module mapped it) is kept, one on any other view (or on
 * every view: a wall's in plan) dropped.
 */
export function throughInset(g: Grip, via: Via): Grip | null {
  if (g.view !== via.source) return g.view === via.host ? g : null;
  const { show, back } = mapper(via);
  return {
    ...g,
    at: show(g.at),
    view: via.host,
    step: g.step ? (to) => g.step!(back(to)) : undefined,
    move: g.move ? (to) => g.move!(back(to)) : undefined,
    insertAt: g.insertAt ? (to) => g.insertAt!(back(to)) : undefined,
    constrain: g.constrain ? (to, free) => show(g.constrain!(back(to), free)) : undefined,
    // A constrained sketch's live drag is the source view's own: through an inset, a plain drag.
    sketch: undefined,
  };
}

/** A drafted shape taken through a mapping (host → source): its points mapped, its radius by the mapping's scale. */
export function mapShape(s: Shape, map: (p: Vec2) => Vec2): Shape {
  const o = map({ x: 0, y: 0 }), u = map({ x: 1, y: 0 });
  const k = Math.hypot(u.x - o.x, u.y - o.y);
  switch (s.type) {
    case 'line': return { type: 'line', a: map(s.a), b: map(s.b) };
    case 'polyline': return { type: 'polyline', pts: s.pts.map(map), closed: s.closed };
    case 'circle': return { type: 'circle', c: map(s.c), r: s.r * k };
    case 'arc': return { type: 'arc', c: map(s.c), r: s.r * k, start: s.start, end: s.end };
  }
}
