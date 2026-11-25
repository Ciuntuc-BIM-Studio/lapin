/**
 * The hatch origin's grip (docs/plan-material-graphics.md, stage 3): on a selected element in a plan, an elevation or a section,
 * a grip where its hatch starts (`element.hatchOriginPoint`, asked once per element, view and origin); dragged and dropped, the
 * engine converts the drop to the element's own frame (`element.hatchOriginAt`) and `element.setHatchOrigin` stores it, one
 * undo step. The panel row (HatchOriginSection) stays for typing the two numbers.
 */

import type { NodeDto } from '../../engine/protocol';
import { execute, query } from '../../engine/session';
import type { Grip, GripView, Vec2 } from '../../platform/module';
import { bumpGrips } from '../../viewport/grips';
import { HATCH_ORIGIN_TYPES, hatchOriginOf, setOriginArgs } from './hatchOrigin';

export interface HatchOriginPoint { point: [number, number]; origin: [number, number]; own: boolean }

/** What the drop needs of the engine (tests give fakes). */
export interface OriginDeps {
  point: (id: string, view: string) => Promise<HatchOriginPoint | null>;
  at: (id: string, view: string, point: [number, number]) => Promise<{ origin: [number, number] } | null>;
  set: (args: Record<string, unknown>) => Promise<unknown>;
}

const engineDeps: OriginDeps = {
  point: (id, view) => query<HatchOriginPoint>('element.hatchOriginPoint', { id, view }),
  at: (id, view, point) => query<{ origin: [number, number] }>('element.hatchOriginAt', { id, view, point }),
  set: (args) => execute('element.setHatchOrigin', args),
};

/** A drop: where it lands as the element's origin, then stored. Null when the engine could not read the point. */
export async function dropHatchOrigin(id: string, view: string, to: Vec2, deps: OriginDeps = engineDeps): Promise<[number, number] | null> {
  const r = await deps.at(id, view, [to.x, to.y]);
  if (!r) return null;
  await deps.set(setOriginArgs([id], r.origin));
  return r.origin;
}

// The points asked for: by element, view and the origin it had (a changed origin asks again).
const points = new Map<string, HatchOriginPoint | null>();
const asking = new Set<string>();
const keyOf = (n: NodeDto, view: string) => `${n.id}|${view}|${JSON.stringify(hatchOriginOf(n))}|${String(n.props.hatchOrigin === undefined)}`;

/** Forgets what was asked (the model changed under it). */
export const forgetOriginPoints = (): void => { points.clear(); };

/** The grip of an element in a view: none until its point has come (it is asked, and the views take their grips again). */
export function hatchOriginGrips(n: NodeDto, v?: GripView, deps: OriginDeps = engineDeps): Grip[] {
  const view = v?.view;
  if (!view || !HATCH_ORIGIN_TYPES.has(n.type)) return [];
  const key = keyOf(n, view);
  const known = points.get(key);
  if (known === undefined) {
    if (!asking.has(key)) {
      asking.add(key);
      void deps.point(n.id, view).then((p) => { points.set(key, p); }).catch(() => { points.set(key, null); }).finally(() => { asking.delete(key); bumpGrips(); });
    }
    return [];
  }
  if (!known) return [];
  return [{
    key: 'hatchOrigin', at: { x: known.point[0], y: known.point[1] }, kind: 'anchor', view,
    title: 'Hatch origin: drag to where the hatch should start',
    // On an outline's first corner by default: the corner's grip comes first.
    yields: true,
    move: async (to) => { await dropHatchOrigin(n.id, view, to, deps); },
  }];
}
