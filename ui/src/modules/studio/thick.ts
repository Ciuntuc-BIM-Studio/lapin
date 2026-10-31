/**
 * Studio's thick solids (docs/plan-studio-thick.md): RING, BOX and TWALL give a drafted shape a thickness and a height.
 * On a plan they make native walls of the current wall type by default (architecture.wallsAlong), or a Studio solid;
 * on a section, an elevation or a reference plane, a Studio solid classed IfcWall (studio.ring, studio.box, studio.wall).
 * What the commands decide, apart from the command line, so it can be tested.
 */

import type { NodeDto } from '../../engine/protocol';

export type ThickOp = 'ring' | 'box' | 'wall';

/** Where the thickness lies, by the command line's keyword. */
export const JUSTIFY: Record<ThickOp, Record<string, string>> = {
  ring: { I: 'inside', O: 'outside', C: 'centre' },
  box: { I: 'inside', O: 'outside', C: 'centre' },
  wall: { L: 'left', R: 'right', C: 'centre' },
};

export const DEFAULT_JUSTIFY: Record<ThickOp, string> = { ring: 'inside', box: 'inside', wall: 'centre' };

/** The engine's commands. */
export const COMMAND: Record<ThickOp, string> = { ring: 'studio.ring', box: 'studio.box', wall: 'studio.wall' };

interface ShapeJson { type?: string; closed?: boolean }

/** Whether a drafted shape can be the solid's (the engine says why not when it cannot). */
export function shapeFits(op: ThickOp, shape: ShapeJson | undefined | null): boolean {
  const t = shape?.type;
  const closed = t === 'circle' || (t === 'polyline' && !!shape?.closed);
  if (op === 'ring') return closed || t === 'arc';
  if (op === 'box') return closed;
  return t === 'line' || t === 'polyline' || t === 'arc' || t === 'circle';
}

/** The kind of view a drafted shape lies on and its storey: a storey's own plan is `plan:<storey id>`. */
export function viewOf(viewId: string | undefined, nodes: Map<string, NodeDto>): { kind: string; storeyId: string | null } {
  if (!viewId) return { kind: '', storeyId: null };
  if (viewId.startsWith('plan:')) return { kind: 'plan', storeyId: viewId.slice(5) };
  const v = nodes.get(viewId);
  const kind = typeof v?.props.kind === 'string' ? v.props.kind : 'plan';
  return { kind, storeyId: typeof v?.props.storeyId === 'string' ? v.props.storeyId : null };
}

/** Native walls only on a plan, and there unless the user asked for a solid. */
export const makesNative = (viewKind: string, solid: boolean): boolean => viewKind === 'plan' && !solid;

export interface ThickChoice { op: ThickOp; justify: string; thickness: number; height: number; bottom: boolean; top: boolean }

/** The arguments of the Studio command for a shape. */
export function solidArgs(profileId: string, c: ThickChoice): Record<string, unknown> {
  const a: Record<string, unknown> = { profileId, thickness: c.thickness, height: c.height, justify: c.justify };
  if (c.op === 'box') Object.assign(a, { bottom: c.bottom, top: c.top });
  return a;
}

/** The arguments of architecture.wallsAlong for a shape on a plan: the walls of the wall type, the bottom and top as floors. */
export function nativeArgs(shape: unknown, storeyId: string | null, typeId: string | null, c: ThickChoice): Record<string, unknown> {
  const a: Record<string, unknown> = { shape, height: c.height, justify: c.justify };
  if (storeyId) a.storeyId = storeyId;
  if (typeId) a.typeId = typeId;
  if (c.op === 'box') Object.assign(a, { bottom: c.bottom, top: c.top });
  return a;
}

/** The fields a Studio solid shows in the properties panel, by what it is. */
export function solidFieldKeys(node: NodeDto): string[] | null {
  const op = node.props.operation;
  if (typeof op !== 'string') return null;
  if (op === 'boxSlab') return ['cutPriority'];
  if (op === 'ring' || op === 'wall') return ['thickness', 'height', 'justify', 'offset', 'cutPriority'];
  if (op === 'box') return ['thickness', 'height', 'justify', 'bottom', 'bottomThickness', 'top', 'topThickness', 'offset', 'cutPriority'];
  return ['depth', 'angle', 'offset', 'place', 'placeOffset', 'cutPriority', 'exportAs'];
}

/** The plane through three picked points ([x, y, z] each), or why not: none when two are the same or the three lie on a line. */
export function threePoints(pts: number[][]): string | null {
  if (pts.length !== 3) return 'Pick three points.';
  const [a, b, c] = pts;
  const u = [b[0] - a[0], b[1] - a[1], b[2] - a[2]];
  const v = [c[0] - a[0], c[1] - a[1], c[2] - a[2]];
  const n = [u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]];
  return Math.hypot(...n) < 1e-9 ? 'The three points lie on a line: pick three that make a plane.' : null;
}

/** The view in front: the plan (its own, or the storey's), the section, elevation or reference plane; none in 3D. */
export function frontViewId(ui: { view: string; planViewId?: string | null; drawingViewId?: string | null; storeyId?: string | null }, storeyId: string | null): string | null {
  if (ui.view === 'plan') return ui.planViewId ?? ((ui.storeyId ?? storeyId) ? `plan:${ui.storeyId ?? storeyId}` : null);
  if (ui.view === 'drawing') return ui.drawingViewId ?? null;
  return null;
}
