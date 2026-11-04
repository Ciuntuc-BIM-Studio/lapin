/**
 * A roof window's and a dormer's grips on the selected roof (decisions 251 and 280): the middle moves it; a window's edges
 * and corners and a dormer's sides resize it on its slope, the opposite edge staying where it was. The roof tells each
 * opening's frame (`openingFrames`: the eave's direction `d`, the up-slope one `n` in plan, and `k`, what a length up the
 * slope measures in plan); the size is kept as the model keeps it, the width along the eave and the length up the slope.
 */

import type { Grip, GripStep, RefLine, Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';

export interface RoofOpeningProps { id: string; name?: string; kind?: string; x?: number; y?: number; width?: number; length?: number }
export interface OpeningFrame { d: [number, number]; n: [number, number]; k: number }

/** The smallest a window or a dormer is made, m (the engine's rule too). */
export const MIN_SIZE = 0.2;

export const widthOf = (o: RoofOpeningProps) => o.width ?? (o.kind === 'dormer' ? 1.6 : 0.78);
export const lengthOf = (o: RoofOpeningProps) => o.length ?? 1.18;
export const openingName = (o: RoofOpeningProps) => o.name ?? (o.kind === 'dormer' ? 'Dormer' : 'Roof window');

const round = (v: number) => Math.round(v * 1e4) / 1e4;

/**
 * The opening resized by a grip at (sx, sy) on it (−1, 0 or 1 along the eave and up the slope) dragged to `to`: its new
 * middle and size, the opposite edge kept; null when it would be thinner than MIN_SIZE. A dormer's middle is its front
 * wall's, so only its sides (sy 0) resize it.
 */
export function resizeOpening(o: RoofOpeningProps, f: OpeningFrame, sx: -1 | 0 | 1, sy: -1 | 0 | 1, to: Vec2): { x: number; y: number; width: number; length: number } | null {
  const c = { x: o.x ?? 0, y: o.y ?? 0 };
  const [dx, dy] = f.d, [nx, ny] = f.n;
  const a = widthOf(o) / 2, b = (lengthOf(o) / 2) * f.k;
  const u = (to.x - c.x) * dx + (to.y - c.y) * dy;
  const v = (to.x - c.x) * nx + (to.y - c.y) * ny;
  let cu = 0, cv = 0, width = widthOf(o), length = lengthOf(o);
  if (sx) { width = sx * u + a; cu = (u - sx * a) / 2; }
  if (sy) { const plan = sy * v + b; length = plan / f.k; cv = (v - sy * b) / 2; }
  if (width < MIN_SIZE - 1e-9 || length < MIN_SIZE - 1e-9) return null;
  return { x: round(c.x + dx * cu + nx * cv), y: round(c.y + dy * cu + ny * cv), width: round(width), length: round(length) };
}

/** Where the grip at (sx, sy) stands: a window's edge middle or corner; a dormer's front corner. */
export function gripPoint(o: RoofOpeningProps, f: OpeningFrame, sx: number, sy: number): Vec2 {
  const a = widthOf(o) / 2, b = (lengthOf(o) / 2) * f.k;
  return { x: (o.x ?? 0) + f.d[0] * a * sx + f.n[0] * b * sy, y: (o.y ?? 0) + f.d[1] * a * sx + f.n[1] * b * sy };
}

/** A window's outline in plan, drawn over the selected roof. */
export function windowOutline(o: RoofOpeningProps, f: OpeningFrame): Vec2[] {
  return [[-1, -1], [1, -1], [1, 1], [-1, 1]].map(([sx, sy]) => gripPoint(o, f, sx, sy));
}

const SIDES: [-1 | 0 | 1, -1 | 0 | 1, Grip['kind']][] = [
  [1, 0, 'edgeMid'], [-1, 0, 'edgeMid'], [0, 1, 'edgeMid'], [0, -1, 'edgeMid'], [1, 1, 'vertex'], [-1, 1, 'vertex'], [1, -1, 'vertex'], [-1, -1, 'vertex'],
];

const openingsOf = (node: NodeDto) => (Array.isArray(node.props.openings) ? node.props.openings : []) as unknown as RoofOpeningProps[];
const framesOf = (node: NodeDto) => (node.props.openingFrames ?? {}) as unknown as Record<string, OpeningFrame | undefined>;

/** Every roof window's and dormer's grips on the roof: the middle, then the resizing ones. */
export function roofOpeningGrips(node: NodeDto): Grip[] {
  const frames = framesOf(node);
  return openingsOf(node).filter((o) => typeof o.x === 'number' && typeof o.y === 'number').flatMap((o): Grip[] => {
    const name = openingName(o);
    const remove = (): GripStep => ({ command: 'architecture.removeRoofOpening', args: { roofId: node.id, id: o.id } });
    const removeLabel = o.kind === 'dormer' ? 'Remove dormer' : 'Remove roof window';
    const middle: Grip = {
      key: `opening:${o.id}`, kind: 'centre', at: { x: o.x!, y: o.y! },
      title: `${name} · Drag: move it on the roof · Ctrl/⌘-click, Delete or a right-click: remove it`,
      step: (to) => ({ command: 'architecture.updateRoofOpening', args: { roofId: node.id, id: o.id, x: to.x, y: to.y } }),
      remove, removeLabel,
    };
    const f = frames[o.id];
    if (!f) return [middle];
    const sides = o.kind === 'dormer' ? SIDES.filter(([, sy]) => sy === 0).map(([sx]): [-1 | 0 | 1, -1 | 0 | 1, Grip['kind']] => [sx, 0, 'end']) : SIDES;
    return [middle, ...sides.map(([sx, sy, kind]): Grip => ({
      key: `opening:${o.id}:${sx}:${sy}`, kind, at: gripPoint(o, f, sx, sy),
      title: `${name} · Drag: resize it on the roof · Delete or a right-click: remove it`,
      step: (to) => {
        const r = resizeOpening(o, f, sx, sy, to);
        if (!r) return null;
        // A dormer's depth follows its heights: no length of its own.
        const size = o.kind === 'dormer' ? { x: r.x, y: r.y, width: r.width } : r;
        return { command: 'architecture.updateRoofOpening', args: { roofId: node.id, id: o.id, ...size } };
      },
      remove, removeLabel,
    }))];
  });
}

/** The roof windows' outlines over the selected roof. */
export function roofOpeningRefs(node: NodeDto): RefLine[] {
  const frames = framesOf(node);
  return openingsOf(node).flatMap((o) => {
    const f = frames[o.id];
    return f && o.kind !== 'dormer' && typeof o.x === 'number' ? [{ points: windowOutline(o, f), closed: true, kind: 'outline' as const }] : [];
  });
}
