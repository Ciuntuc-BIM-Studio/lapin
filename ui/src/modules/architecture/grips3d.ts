/**
 * The 3D-only grips of walls, floors and roofs (decision 610; viewport/Grips3D.tsx drags them):
 *
 * - a wall's top, at its axis' middle: up and down changes its top's offset when the top goes to a storey (WallTop: the
 *   storey above, or one named), else its own height (architecture.setWallTop);
 * - a wall's two faces, at their middles half way up: across the wall moves it sideways (its joined neighbours' ends go
 *   with it, as the wall's update does), a curved wall's radius about its centre (architecture.updateWall);
 * - a floor's top: its offset from its storey (the thickness is its type's, as Properties has it; architecture.updateSlab);
 * - a roof's eave: its wall plate over the storey's top (eaveOffset, as its properties; architecture.updateRoof).
 *
 * Each drag is one engine command, one undo step.
 */

import type { Grip3D, Vec2, Vec3 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { num, storeys, typeLayers, typeOf, usageOf } from '../../engine/model';
import { useSession } from '../../engine/session';
import { arcLength, arcOf, isArc, type Arc } from '../../viewport/arc';
import { frameAt } from '../../viewport/gripKit';
import { fromPairs } from '../../viewport/contour';

const UP: Vec3 = { x: 0, y: 0, z: 1 };
/** The least a wall stands over its base (WallTop.MinHeight). */
const MIN_HEIGHT = 0.01;

const storeyOf = (n: NodeDto) => (n.storeyId ? useSession.getState().nodes.get(n.storeyId) : undefined);

/** A wall's base, its top and how its top is held, as the engine works them out (WallTop.Base, Level, Height). */
export function wallHeights(n: NodeDto): { base: number; top: number; connected: boolean; offset: number; height: number } {
  const s = useSession.getState();
  const own = storeyOf(n);
  const base = num(own, 'elevation') + num(n, 'baseOffset');
  const stored = num(n, 'height', 3);
  const offset = num(n, 'topOffset');
  const top = typeof n.props.top === 'string' && n.props.top ? n.props.top : null;
  let level: number | null = null;
  if (top === 'above') {
    const above = own ? storeys(s).find((st) => num(st, 'elevation') > num(own, 'elevation') + 1e-6) : undefined;
    if (above) level = num(above, 'elevation');
    else if (own && num(own, 'height') > 0) level = num(own, 'elevation') + num(own, 'height');
  } else if (top) {
    const named = s.nodes.get(top);
    if (named?.type === 'IfcBuildingStorey') level = num(named, 'elevation');
  }
  const connected = level != null && level + offset - base >= MIN_HEIGHT;
  const height = connected ? level! + offset - base : stored;
  return { base, top: base + height, connected, offset, height };
}

/** A wall's faces across its axis (left of it positive): its layers from the usage's offset, else its thickness about the axis. */
export function wallFaces(n: NodeDto): { left: number; right: number } {
  const s = useSession.getState();
  const type = typeOf(s, n.id);
  const layers = type ? typeLayers(s, type.id) : [];
  const total = layers.reduce((a, l) => a + l.thickness, 0);
  const usage = usageOf(s, n.id);
  if (usage && total > 0) {
    const far = usage.offset + (usage.negative ? -total : total);
    return { left: Math.max(usage.offset, far), right: Math.min(usage.offset, far) };
  }
  const t = num(n, 'thickness', total > 0 ? total : 0.2);
  return { left: t / 2, right: -t / 2 };
}

/** The arc's centre and radius (its rise to the left positive, as a wall's `arc`). */
function circleOf(c: Arc): { centre: Vec2; radius: number } {
  const dx = c.b.x - c.a.x, dy = c.b.y - c.a.y, chord = Math.hypot(dx, dy);
  const radius = (chord * chord / 4 + c.rise * c.rise) / (2 * Math.abs(c.rise));
  const left = { x: -dy / chord, y: dx / chord };
  const k = c.rise - Math.sign(c.rise) * radius;
  return { centre: { x: (c.a.x + c.b.x) / 2 + left.x * k, y: (c.a.y + c.b.y) / 2 + left.y * k }, radius };
}

/** A wall moved `by` across it along `dir` (a straight one), or its radius changed by it about its centre (a curved one). */
export function wallAcross(c: Arc, dir: Vec2, by: number): Arc | null {
  if (!isArc(c)) return { a: { x: c.a.x + dir.x * by, y: c.a.y + dir.y * by }, b: { x: c.b.x + dir.x * by, y: c.b.y + dir.y * by }, rise: 0 };
  const { centre, radius } = circleOf(c);
  const mid = frameAt(c, arcLength(c) / 2).at;
  // Away from the centre the radius grows.
  const away = Math.sign((mid.x - centre.x) * dir.x + (mid.y - centre.y) * dir.y) || 1;
  const r = radius + away * by;
  if (!(r > 0.05)) return null;
  const k = r / radius;
  const scale = (p: Vec2) => ({ x: centre.x + (p.x - centre.x) * k, y: centre.y + (p.y - centre.y) * k });
  return { a: scale(c.a), b: scale(c.b), rise: c.rise * k };
}

function wallGrips3d(n: NodeDto): Grip3D[] {
  const c = arcOf(n.props);
  if (!c || arcLength(c) < 1e-6 || n.props.reference) return [];
  const h = wallHeights(n);
  const f = frameAt(c, arcLength(c) / 2);
  const faces = wallFaces(n);
  const mid = (h.base + h.top) / 2;
  const at = (y: number, z: number): Vec3 => ({ x: f.at.x + f.left.x * y, y: f.at.y + f.left.y * y, z });
  const across = (key: string, y: number, side: 1 | -1): Grip3D => {
    const dir = { x: f.left.x * side, y: f.left.y * side };
    return {
      key, kind: 'face', at: at(y, mid), dir: { ...dir, z: 0 },
      title: isArc(c) ? 'Drag across: the curved wall\'s radius about its centre' : 'Drag across: move the wall sideways (its joined walls follow)',
      step: (by) => {
        const next = wallAcross(c, dir, by);
        return next ? { command: 'architecture.updateWall', args: { id: n.id, x1: next.a.x, y1: next.a.y, x2: next.b.x, y2: next.b.y, arc: next.rise } } : null;
      },
    };
  };
  return [
    {
      key: 'top', kind: 'height', at: at((faces.left + faces.right) / 2, h.top), dir: UP,
      title: h.connected ? 'Drag up or down: the top\'s offset from the storey it goes to' : 'Drag up or down: the wall\'s height',
      step: (by) => {
        if (h.height + by < MIN_HEIGHT) return null;
        return h.connected
          ? { command: 'architecture.setWallTop', args: { ids: [n.id], topOffset: Math.round((h.offset + by) * 1e6) / 1e6 } }
          : { command: 'architecture.setWallTop', args: { ids: [n.id], height: Math.round((h.height + by) * 1e6) / 1e6 } };
      },
    },
    across('face:left', faces.left, 1),
    across('face:right', faces.right, -1),
  ];
}

/** A point inside an outline to stand a grip on: its centroid, else (an L, a U) its first edge's middle a little inside. */
export function insidePoint(pts: Vec2[]): Vec2 {
  let a = 0, cx = 0, cy = 0;
  for (let i = 0; i < pts.length; i++) {
    const p = pts[i], q = pts[(i + 1) % pts.length], k = p.x * q.y - q.x * p.y;
    a += k; cx += (p.x + q.x) * k; cy += (p.y + q.y) * k;
  }
  const c = Math.abs(a) > 1e-12 ? { x: cx / (3 * a), y: cy / (3 * a) } : pts[0];
  if (inside(pts, c)) return c;
  const [p, q] = [pts[0], pts[1]];
  const len = Math.hypot(q.x - p.x, q.y - p.y) || 1, s = a >= 0 ? 1 : -1;
  return { x: (p.x + q.x) / 2 - ((q.y - p.y) / len) * 0.3 * s, y: (p.y + q.y) / 2 + ((q.x - p.x) / len) * 0.3 * s };
}

function inside(pts: Vec2[], p: Vec2): boolean {
  let o = false;
  for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
    const a = pts[i], b = pts[j];
    if ((a.y > p.y) !== (b.y > p.y) && p.x < ((b.x - a.x) * (p.y - a.y)) / (b.y - a.y) + a.x) o = !o;
  }
  return o;
}

function slabGrips3d(n: NodeDto): Grip3D[] {
  const pts = fromPairs(n.props.contour);
  if (pts.length < 3 || n.props.reference) return [];
  const offset = num(n, 'offset');
  const p = insidePoint(pts);
  return [{
    key: 'top', kind: 'height', at: { ...p, z: num(storeyOf(n), 'elevation') + offset }, dir: UP,
    title: 'Drag up or down: the floor\'s offset from its storey (its thickness is its type\'s)',
    step: (by) => ({ command: 'architecture.updateSlab', args: { id: n.id, offset: Math.round((offset + by) * 1e6) / 1e6 } }),
  }];
}

function roofGrips3d(n: NodeDto): Grip3D[] {
  const pts = fromPairs(n.props.contour);
  if (pts.length < 3 || n.props.reference) return [];
  const st = storeyOf(n);
  const eave = num(n, 'eaveOffset');
  const [a, b] = [pts[0], pts[1]];
  return [{
    // A quarter along its first edge: its middle has the edge's own grip.
    key: 'eave', kind: 'height', at: { x: a.x + (b.x - a.x) / 4, y: a.y + (b.y - a.y) / 4, z: num(st, 'elevation') + num(st, 'height', 3) + eave }, dir: UP,
    title: 'Drag up or down: the roof\'s wall plate over the storey\'s top',
    step: (by) => ({ command: 'architecture.updateRoof', args: { id: n.id, attrs: { eaveOffset: Math.round((eave + by) * 1e6) / 1e6 } } }),
  }];
}

export const architectureGrips3d: Record<string, (n: NodeDto) => Grip3D[]> = { IfcWall: wallGrips3d, IfcSlab: slabGrips3d, IfcRoof: roofGrips3d };
