/**
 * Floor slopes and falls (docs/plan-floor-slopes.md, forum topic 157): the tools that put a drain, a point, a line, an edge slope,
 * a slope arrow and a region on a floor in a plan, and "Slope this room"; the helpers they and the properties share (which
 * floor a click is on, which edge is nearest, a grade read and written). Every tool is one engine command, one undo step.
 */

import { ArrowDownRight, CircleDot, Dot, Minus, PanelBottomOpen, SquareDashed, SquareDot } from 'lucide-react';
import type { ToolDef, Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { formatLength, parseLength, unitName } from '../../engine/unitsStore';
import { promptText } from '../../shell/prompts';

/** The corners of an outline as stored ([[x, y]…]). */
export function cornersOf(raw: unknown): Vec2[] {
  return Array.isArray(raw) ? raw.filter((p): p is number[] => Array.isArray(p) && typeof p[0] === 'number' && typeof p[1] === 'number').map(([x, y]) => ({ x, y })) : [];
}

/** Whether a point lies in a polygon (even-odd). */
export function inside(poly: Vec2[], p: Vec2): boolean {
  let o = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const a = poly[i], b = poly[j];
    if ((a.y > p.y) !== (b.y > p.y) && p.x < ((b.x - a.x) * (p.y - a.y)) / (b.y - a.y) + a.x) o = !o;
  }
  return o;
}

/** The floor a point lies on: the selected one first, else the last drawn (on the storey given, when one is). */
export function floorAt(nodes: Iterable<NodeDto>, p: Vec2, storeyId?: string | null, selected: string[] = []): NodeDto | undefined {
  const all = [...nodes].filter((n) => n.type === 'IfcSlab' && (!storeyId || n.storeyId === storeyId) && inside(cornersOf(n.props.contour), p));
  return all.find((n) => selected.includes(n.id)) ?? all[all.length - 1];
}

/** The room a point lies in (on the storey given, when one is). */
export function roomAt(nodes: Iterable<NodeDto>, p: Vec2, storeyId?: string | null): NodeDto | undefined {
  return [...nodes].find((n) => n.type === 'IfcSpace' && (!storeyId || n.storeyId === storeyId) && inside(cornersOf(n.props.contour), p));
}

/** The edge of an outline nearest a point: its index (from corner i to the next) and how far it is. */
export function nearestEdge(corners: Vec2[], p: Vec2): { index: number; distance: number } | null {
  let best: { index: number; distance: number } | null = null;
  for (let i = 0; i < corners.length; i++) {
    const a = corners[i], b = corners[(i + 1) % corners.length];
    const dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy;
    const t = l2 > 0 ? Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l2)) : 0;
    const d = Math.hypot(p.x - (a.x + dx * t), p.y - (a.y + dy * t));
    if (!best || d < best.distance) best = { index: i, distance: d };
  }
  return best;
}

/** A grade as typed: "1.5 %", "1:50", "0.015"; null when it does not read. */
export function parseGrade(text: string): number | null {
  const s = text.trim().replace(',', '.');
  if (!s) return null;
  const n = (t: string) => { const v = Number(t.trim()); return t.trim() !== '' && Number.isFinite(v) ? v : null; };
  if (s.endsWith('%')) { const v = n(s.slice(0, -1)); return v == null ? null : v / 100; }
  const k = s.indexOf(':');
  if (k > 0) { const a = n(s.slice(0, k)), b = n(s.slice(k + 1)); return a != null && b != null && b !== 0 ? a / b : null; }
  return n(s);
}

/** A grade as plans write it: 1.5 %, under one percent with two decimals. */
export const gradeText = (g: number): string => `${(g * 100).toFixed(Math.abs(g * 100) < 1 ? 2 : 1)} %`;

/** What a floor's `surface` holds, read for the properties and the tools. */
export interface EdgeSlope { edge: number; grade: number; fall: 'in' | 'out'; offset?: number; until?: { offset?: number; distance?: number; along?: 'plan' | 'slope' } }
export interface SlopePoint { at: [number, number]; offset?: number; level?: number }
export interface SlopeLine { a: [number, number]; b: [number, number]; offset?: [number, number]; level?: [number, number] }
export interface SlopeDrain { at?: [number, number]; to?: [number, number]; depth: number; grate?: number; centre?: boolean }
export interface SlopeTop {
  how?: 'bend' | 'layer'; layer?: number; minThickness?: number;
  topMode?: 'level' | 'slope' | 'corners'; elevation?: number; grade?: number; slopeDirection?: number;
  edges?: EdgeSlope[]; points?: SlopePoint[]; lines?: SlopeLine[]; drains?: SlopeDrain[];
}
export interface SlopeRegion extends SlopeTop { outline?: [number, number][]; roomId?: string }
export interface Surface extends SlopeTop { regions?: SlopeRegion[] }

export const surfaceOf = (n: NodeDto): Surface | null => (n.props.surface && typeof n.props.surface === 'object' ? (n.props.surface as Surface) : null);

/** How many things a top holds (its edge slopes, points, lines and drains). */
export const itemCount = (t: SlopeTop): number => (t.edges?.length ?? 0) + (t.points?.length ?? 0) + (t.lines?.length ?? 0) + (t.drains?.length ?? 0);

/** The region of a floor a point lies in (its index), or null for the floor's own top. */
export function regionAt(s: Surface | null, p: Vec2, rooms: ReadonlyMap<string, NodeDto>): number | null {
  const regs = s?.regions ?? [];
  for (let i = 0; i < regs.length; i++) {
    const r = regs[i];
    const loop = r.roomId ? cornersOf(rooms.get(r.roomId)?.props.contour) : cornersOf(r.outline);
    if (loop.length >= 3 && inside(loop, p)) return i;
  }
  return null;
}

/** The floor a click is on and the region of it there, or a notice and nothing. */
function picked(p: Vec2, storeyId: string): { floor: NodeDto; region: number | null } | null {
  const nodes = useSession.getState().nodes;
  const floor = floorAt(nodes.values(), p, storeyId, useUi.getState().selected);
  if (!floor) { notify('Click on a floor.'); return null; }
  return { floor, region: regionAt(surfaceOf(floor), p, nodes) };
}

const withRegion = (region: number | null) => (region == null ? {} : { region });

/** A drain on a floor (the outline held, falls from every side to it); its depth asked. */
export const floorDrain: ToolDef = {
  id: 'architecture.floorDrain', label: 'Floor drain', icon: CircleDot,
  gesture: { kind: 'point' },
  complete: async ({ points }, ctx) => {
    const p = points[0];
    const at = p && picked(p, ctx.storeyId);
    if (!at) return;
    const answer = promptText('How far below the floor round it ({unit})?', formatLength(0.02), { unit: unitName() });
    const depth = answer == null ? null : parseLength(answer);
    if (depth == null || !(depth > 0)) return;
    await ctx.execute('architecture.addFloorSlope', { id: at.floor.id, ...withRegion(at.region), kind: 'drain', at: [p.x, p.y], depth });
  },
};

/** A channel drain: two clicks along it; the floor falls to it from both sides. */
export const floorChannel: ToolDef = {
  id: 'architecture.floorChannel', label: 'Channel drain', icon: Minus,
  gesture: { kind: 'twoPoints', hint: 'click one end of the channel, then the other, on a floor · Esc cancels' },
  complete: async ({ points }, ctx) => {
    if (points.length < 2) return;
    const [a, b] = points;
    const at = picked(a, ctx.storeyId);
    if (!at) return;
    const answer = promptText('How far below the floor round it ({unit})?', formatLength(0.02), { unit: unitName() });
    const depth = answer == null ? null : parseLength(answer);
    if (depth == null || !(depth > 0)) return;
    await ctx.execute('architecture.addFloorSlope', { id: at.floor.id, ...withRegion(at.region), kind: 'drain', at: [a.x, a.y], to: [b.x, b.y], depth });
  },
};

/** A point the floor's top passes through: an offset from the floor's top (-20) or a level (=+3.18). */
export const floorSlopePoint: ToolDef = {
  id: 'architecture.floorSlopePoint', label: 'Level point', icon: Dot,
  gesture: { kind: 'point' },
  complete: async ({ points }, ctx) => {
    const p = points[0];
    const at = p && picked(p, ctx.storeyId);
    if (!at) return;
    const answer = promptText('Its height: an offset from the floor\'s top ({unit}, -20), or a level after = (=3.18 m):', formatLength(-0.02), { unit: unitName() });
    if (answer == null) return;
    const t = answer.trim();
    const level = t.startsWith('=') ? parseLength(t.slice(1), 'm') : null;
    const offset = level == null ? parseLength(t) : null;
    if (level == null && offset == null) { notify('That height does not read.'); return; }
    await ctx.execute('architecture.addFloorSlope', { id: at.floor.id, ...withRegion(at.region), kind: 'point', at: [p.x, p.y], ...(level != null ? { level } : { offset }) });
  },
};

/** A line the floor's top passes through (a ridge or a valley): two clicks, an offset at each end. */
export const floorSlopeLine: ToolDef = {
  id: 'architecture.floorSlopeLine', label: 'Level line', icon: SquareDashed,
  gesture: { kind: 'twoPoints', hint: 'click the line\'s start, then its end, on a floor · Esc cancels' },
  complete: async ({ points }, ctx) => {
    if (points.length < 2) return;
    const [a, b] = points;
    const at = picked(a, ctx.storeyId);
    if (!at) return;
    const answer = promptText('Its offsets from the floor\'s top at the start and the end ({unit}; one value for both):', '0; 0', { unit: unitName() });
    if (answer == null) return;
    const parts = answer.split(/[;\s]+/).filter(Boolean).map((x) => parseLength(x));
    if (!parts.length || parts.some((x) => x == null)) { notify('Those heights do not read.'); return; }
    const offset = [parts[0]!, (parts[1] ?? parts[0])!];
    await ctx.execute('architecture.addFloorSlope', { id: at.floor.id, ...withRegion(at.region), kind: 'line', a: [a.x, a.y], b: [b.x, b.y], offset });
  },
};

/** A slope from an edge of a floor: a click near the edge, then its grade; it falls away from the edge (towards it with a minus). */
export const floorEdgeSlope: ToolDef = {
  id: 'architecture.floorEdgeSlope', label: 'Edge slope', icon: PanelBottomOpen,
  gesture: { kind: 'point' },
  complete: async ({ points }, ctx) => {
    const p = points[0];
    const at = p && picked(p, ctx.storeyId);
    if (!at) return;
    const s = surfaceOf(at.floor);
    const r = at.region != null ? s?.regions?.[at.region] : null;
    const loop = r ? (r.roomId ? cornersOf(useSession.getState().nodes.get(r.roomId)?.props.contour) : cornersOf(r.outline)) : cornersOf(at.floor.props.contour);
    const edge = nearestEdge(loop, p);
    if (!edge) return;
    const answer = promptText('Its slope from this edge (1.5 %, 1:50); a minus to fall towards the edge instead:', '1.5 %');
    if (answer == null) return;
    const out = answer.trim().startsWith('-');
    const grade = parseGrade(answer.trim().replace(/^-/, ''));
    if (grade == null || !(grade > 0)) { notify('That grade does not read: type 1.5 %, 1:50 or 0.015.'); return; }
    await ctx.execute('architecture.addFloorSlope', { id: at.floor.id, ...withRegion(at.region), kind: 'edge', edge: edge.index, grade, fall: out ? 'out' : 'in' });
  },
};

/** A floor's top as one plane, drawn as an arrow from its high end to its low end; the grade asked. */
export const floorSlopeArrow: ToolDef = {
  id: 'architecture.floorSlopeArrow', label: 'Floor slope arrow', icon: ArrowDownRight,
  gesture: { kind: 'twoPoints', hint: 'click the high end, then the low end, over a floor · Esc cancels' },
  complete: async ({ points }, ctx) => {
    if (points.length < 2) return;
    const [a, b] = points;
    const at = picked(a, ctx.storeyId);
    if (!at) return;
    const answer = promptText('Its grade (1.5 %, 1:50):', '1.5 %');
    if (answer == null) return;
    const grade = parseGrade(answer);
    if (grade == null || !(grade > 0)) { notify('That grade does not read: type 1.5 %, 1:50 or 0.015.'); return; }
    await ctx.execute('architecture.setFloorSlope', { id: at.floor.id, ...withRegion(at.region), arrow: { from: [a.x, a.y], to: [b.x, b.y], grade } });
  },
};

/** A region of a floor with its own top (a sunk shower tray): its outline drawn inside the floor; its offset asked. */
export const floorSlopeRegion: ToolDef = {
  id: 'architecture.floorSlopeRegion', label: 'Floor region', icon: SquareDot,
  gesture: { kind: 'polygon' },
  complete: async ({ points }, ctx) => {
    if (points.length < 3) return;
    const mid = { x: points.reduce((s, p) => s + p.x, 0) / points.length, y: points.reduce((s, p) => s + p.y, 0) / points.length };
    const floor = floorAt(useSession.getState().nodes.values(), mid, ctx.storeyId, useUi.getState().selected);
    if (!floor) { notify('Draw the region inside a floor.'); return; }
    const answer = promptText('Its offset from the floor\'s top ({unit}):', formatLength(-0.02), { unit: unitName() });
    const elevation = answer == null ? null : parseLength(answer);
    if (elevation == null) return;
    await ctx.execute('architecture.addFloorSlopeRegion', { id: floor.id, points: points.map((p) => [p.x, p.y]), elevation });
  },
};

/** A room's floor falling to a drain at its middle: a click in the room; the floor under it takes a region on the room's outline. */
export const slopeRoom: ToolDef = {
  id: 'architecture.slopeRoom', label: 'Slope this room', icon: CircleDot,
  gesture: { kind: 'point' },
  complete: async ({ points }, ctx) => {
    const p = points[0];
    const room = p && roomAt(useSession.getState().nodes.values(), p, ctx.storeyId);
    if (!room) { notify('Click in a room.'); return; }
    const answer = promptText('The drain at its middle, how far below the floor round it ({unit})?', formatLength(0.02), { unit: unitName() });
    const depth = answer == null ? null : parseLength(answer);
    if (depth == null || !(depth > 0)) return;
    await ctx.execute('architecture.slopeRoom', { roomId: room.id, drain: { depth } });
  },
};

export const floorSlopeTools = [floorDrain, floorEdgeSlope, floorChannel, floorSlopePoint, floorSlopeLine, floorSlopeArrow, floorSlopeRegion, slopeRoom];
