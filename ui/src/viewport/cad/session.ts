/**
 * The CAD command line (the old Lapin's drafting session): a command is a
 * chain of steps, each asking for a point, a picked shape, a selection, a
 * value or a text, with keywords. Clicks in a 2D view and typed input feed the
 * running step; Enter or Space ends it (or repeats the last command), Escape
 * cancels. Each finished edit is one engine command, so one undo step.
 */

import type { ComponentType } from 'react';
import { create } from 'zustand';
import type { Vec2 } from '../../platform/module';
import { notify, select, useUi } from '../../app/store';
import { execute, undo, useSession } from '../../engine/session';
import { beginMove3d } from '../moveIn3d';
import { useWorkPlane } from '../sketch/sketch3d';
import { deleteElements } from '../../engine/dependents';
import { ucsOf, useLocation } from '../../app/location';
import { directionFrom, directionOf, normalizeTurn, rotatedShape, rotationFromReference } from './rotate';
import { applyOrtho, parseCoordinateInput, parseScalar, pointAlong } from './input';
import { lengthText, parseLength } from '../../engine/unitsStore';
import { arcThrough, arcTangent, dist, fromJson, toJson, type Shape } from './shapes';
import { liveBegin, liveEnd } from '../liveInput';
import { traceBegin, traceEnd } from '../moveTrace';
import { lassoTest, type RegionTest } from '../selectBox';
import { xy } from '../../platform/vec';
import { usePrefs } from '../../app/prefs';
import type { Aimed } from './aim';
import { drawConstrainedArgs, inferConstraints, newShapeId, newShapeIndex, sketchViews, type ConstraintSpec, type DrawHints, type Drawn, type PlacedPoint } from './recorded';
import { viewHasConstraints } from './sketchStore';
import type { ViewFrame } from '../viewFrame';

/** What a 2D view lends the command line: its view, and what lies under a point. */
export interface CadHost {
  viewId: string;
  /** A plan (typed coordinates are in the UCS), not a section or an elevation (in its own frame). */
  isPlan?(): boolean;
  /** Pixels per metre of the view, so a step can tell a click on the same spot from one near it. */
  pxPerMetre?(): number;
  /** The drafted shape under a point, with its exact geometry. */
  pickDraft(p: Vec2): { id: string; shape: Shape } | null;
  /** Every drafted shape within a click's reach of a point, nearest first (a corner two shapes share has both). */
  pickDrafts?(p: Vec2): { id: string; shape: Shape; d: number }[];
  /** Anything selectable under a point (a drafted shape, an annotation, an element). */
  pickAny(p: Vec2): string | null;
  /** A drafted shape's geometry, for previews. */
  shapeOf(id: string): Shape | null;
  /** What anything selectable looks like in this view (a drafted shape, an annotation's lines, an element's outline in plan), for previews and the move's ghost. */
  outlinesOf?(id: string): Shape[];
  /** Everything selectable a region takes (a window or crossing polygon). */
  inRegion?(takes: RegionTest): string[];
  /** Where a section's or an elevation's point lies in the model (decision 453: element.transform carries the model's elements by it); none in a plan. */
  frameAt?(at: Vec2): ViewFrame | null;
}

type Next = Step | null | void | Promise<Step | null | void>;
export interface Step {
  prompt: string;
  want: 'point' | 'pick' | 'select' | 'text' | 'none';
  /** The point typed coordinates and distances are relative to, and ortho squares to. */
  base?: Vec2 | null;
  preview?: (cursor: Vec2) => Shape[];
  /** Texts the preview carries (a dimension's values), standing at their points. */
  labels?: (cursor: Vec2) => { at: Vec2; text: string }[];
  /** The step moves these shapes from its base (MOVE, COPY): the base marker, the line to the cursor and this ghost where they were show until the step ends. */
  trace?: Shape[];
  point?: (p: Vec2) => Next;
  pick?: (hit: { id: string; shape: Shape }, at: Vec2) => Next;
  /** A typed length, read in the project's unit unless it carries its own (2000 in a project in millimetres is 2 m); metres. */
  value?: (v: number) => Next;
  /** A typed plain number (an angle in degrees, a factor, a count, a pen), never converted by the project's unit. */
  scalar?: (n: number) => Next;
  text?: (s: string) => Next;
  /** Keywords (upper case) and what they do. */
  keywords?: Record<string, () => Next>;
  /** Enter with nothing typed. */
  enter?: () => Next;
  /** Escape (or another command starting): what the step keeps of the work so far, as a dimension string keeps its points. */
  escape?: () => void | Promise<void>;
  /** An element clicked in the 3D view (where a click has no point on a drawing): the step takes it, rather than the selection. */
  element?: (id: string) => Next;
  /** The step is a rectangle's other corner: the box at the cursor offers width and height as well as length and angle (the mode remembered by this key). */
  rect?: string;
  /** The element under the cursor is lit while the step waits for one (Match properties' targets, decision 490). */
  hover?: boolean;
}

interface CadState {
  buffer: string;
  /** The command line takes all typing (the ` key, decision 473): no letter shortcuts, no quick tools, until Enter has run it or Esc. */
  takeAll: boolean;
  history: string[];
  command: string | null;
  step: Step | null;
  last: string | null;
  cursor: Vec2 | null;
  /** What the view's aim made of the cursor (the snap, the guides, the lock): how a clicked point was found. */
  aim: Aimed | null;
  lastPoint: Vec2 | null;
  osnap: boolean;
  ortho: boolean;
  polar: boolean;
  layer: string;
  /** The role new shapes take (profile, path…; empty: none). */
  role: string;
  host: CadHost | null;
  /** Remembered values: offset distance, fillet radius, chamfer distances, hatch pattern and scale. */
  offset: number;
  radius: number;
  chamfer: number;
  pattern: string;
  hatchScale: number;
  /** OFFSET through a point picked rather than by the distance. */
  offsetThrough: boolean;
}

export const useCad = create<CadState>(() => ({
  buffer: '', takeAll: false, history: [], command: null, step: null, last: null, cursor: null, aim: null, lastPoint: null,
  osnap: true, ortho: false, polar: true, layer: '0', role: '', host: null,
  offset: 0.5, offsetThrough: false, radius: 0, chamfer: 0.1, pattern: 'LINES45', hatchScale: 1,
}));

const log = (line: string) => useCad.setState((s) => ({ history: [...s.history, line].slice(-50) }));
const fmt = (v: number) => String(Math.round(v * 1000) / 1000);
/** A length as the prompt shows it, in the project's unit with its label ("500 mm"). */
const len = (m: number) => lengthText(m);

/** Bumped when a command is cancelled or another starts: a step that answers after that is dropped. */
let generation = 0;

async function advance(next: Next): Promise<void> {
  const g = generation;
  const step = await next;
  if (g !== generation) return;
  if (step) useCad.setState({ step });
  else useCad.setState({ step: null, command: null });
  // A point asked for from a base: its length and angle can be typed at the cursor.
  if (step && step.want === 'point' && step.base) liveBegin('cad', step.base, (p, typed, angle) => void feedPoint(p, { length: typed, angle }), { delta: !!step.trace, ...(step.rect ? { rect: step.rect } : {}) });
  else liveEnd('cad');
  if (step?.trace && step.base) traceBegin('cad', step.base, step.trace);
  else traceEnd('cad');
}

function host(): CadHost | null {
  const h = useCad.getState().host;
  if (!h) log('Open a plan, section or elevation to draft on.');
  return h;
}

/**
 * Shapes drawn on the host's view. In a sketch, with the constraints recorded while drawing them, as one command and so one undo
 * step (`studio.drawConstrained`); else, or where that cannot run (no Studio), as `drafting.draw`.
 */
async function draw(shapes: Shape[], hints?: DrawHints): Promise<string[]> {
  const h = host();
  if (!h || !shapes.length) return [];
  const { layer, role } = useCad.getState();
  const body = { viewId: h.viewId, layer, shapes: shapes.map(toJson), ...(role ? { role } : {}) };
  const specs = recordWhileDrawing(h.viewId, role, shapes, hints);
  let r: { ids?: string[]; refused?: { kind?: string; reason?: string }[] } | null = null;
  if (specs.length) {
    r = await execute('studio.drawConstrained', drawConstrainedArgs(body, specs)) as { ids?: string[]; refused?: { kind?: string; reason?: string }[] } | null;
    reportRefused(r);
  }
  if (!r) r = await execute('drafting.draw', body) as { ids?: string[] } | null;
  const ids = r?.ids ?? [];
  if (ids.length) lastDrawn = { id: ids[ids.length - 1], cw: !!hints?.cw };
  // The shape drawn last is named now: the next one of a chain is joined to it by its id.
  const i = prevDrawn ? newShapeIndex(prevDrawn.id) : null;
  if (prevDrawn && i != null) prevDrawn = ids[i] ? { id: ids[i], shape: prevDrawn.shape } : null;
  return ids;
}

// ─── Constraints recorded while drawing (decision 520, C2) ─────────────────

/** The points put down in the running command, how each was found: what `draw` turns into constraints. */
let trail: PlacedPoint[] = [];
/** The shape drawn just before in the same command: a chain of lines is joined as it is drawn. */
let prevDrawn: Drawn | null = null;
const resetRecording = () => { trail = []; prevDrawn = null; };

/** What the last command drew, for a tangent arc to continue from: the shape's id, and whether it is an arc travelled clockwise (its far end is then its `start`). Kept across commands. */
let lastDrawn: { id: string; cw: boolean } | null = null;

/** Constraints the solver refused while drawing (`refused`): left out of the drawing, and said so with why. */
function reportRefused(result: { refused?: { kind?: string; reason?: string }[] } | null): void {
  const refused = result?.refused;
  if (!Array.isArray(refused) || !refused.length) return;
  const why = `${refused[0].kind ?? 'constraint'} — ${refused[0].reason ?? 'it conflicts with the others'}`;
  notify(refused.length === 1 ? `Constraint left out: ${why}` : `${refused.length} constraints left out: ${why}`);
}

/** Where a tangent arc continues from: the last drawn line's, polyline's or arc's end, the direction it arrives there, and the part that ends it. */
function continuation(viewId: string): { p: Vec2; dir: Vec2; ref: { shape: string; part: string } } | null {
  if (!lastDrawn) return null;
  const s = draftsOf(viewId).get(lastDrawn.id);
  if (!s) return null;
  const ref = (part: string) => ({ shape: lastDrawn!.id, part });
  if (s.type === 'line') return dist(s.a, s.b) < 1e-9 ? null : { p: s.b, dir: { x: s.b.x - s.a.x, y: s.b.y - s.a.y }, ref: ref('end') };
  if (s.type === 'polyline' && !s.closed && s.pts.length >= 2) {
    const n = s.pts.length, a = s.pts[n - 2], b = s.pts[n - 1];
    return dist(a, b) < 1e-9 ? null : { p: b, dir: { x: b.x - a.x, y: b.y - a.y }, ref: ref(`vertex:${n - 1}`) };
  }
  if (s.type === 'arc') {
    // Counter-clockwise it ends at `end` heading along the perpendicular; clockwise it ends at `start` heading the other way.
    const a = lastDrawn.cw ? s.start : s.end, k = lastDrawn.cw ? -1 : 1;
    return { p: { x: s.c.x + s.r * Math.cos(a), y: s.c.y + s.r * Math.sin(a) }, dir: { x: -k * Math.sin(a), y: k * Math.cos(a) }, ref: ref(lastDrawn.cw ? 'start' : 'end') };
  }
  return null;
}

/** Ctrl (or ⌘) held at the press: the click places its point without constraints. Read when the click is fed, not when it is run. */
let ctrlHeld = false;
if (typeof window !== 'undefined') window.addEventListener('pointerdown', (e) => { ctrlHeld = e.ctrlKey || e.metaKey; }, true);

/** What was typed for a point put down (a length in metres, an angle in degrees), as `draw` will want it. */
export const typedAt = (p: Vec2): { length?: number; angle?: number } | undefined => trail.find((x) => dist(x.p, p) < 1e-7)?.typed;

function noteFed(p: Vec2, o: { typed?: PlacedPoint['typed']; free?: boolean; aim?: Aimed | null } = {}): void {
  const a = o.aim;
  const typed = o.typed && (o.typed.length != null || o.typed.angle != null || o.typed.size) ? o.typed : undefined;
  // A typed point lies where the typing put it, off the aim's own; the line that held the direction still holds it.
  const inference = !a ? null : typed ? (a.lock ? { lock: a.lock, ...(a.to ? { to: a.to } : {}) } : null) : dist(a.point, p) < 1e-9 && (a.mode || a.lock) ? { mode: a.mode, shapeId: a.shapeId, lock: a.lock, ...(a.to ? { to: a.to } : {}) } : null;
  trail = [...trail, { p, inference, free: !!o.free, ...(typed ? { typed } : {}) }].slice(-64);
}

/** The drafted shapes of a view as the model has them now. */
function draftsOf(viewId: string): Map<string, Shape> {
  const out = new Map<string, Shape>();
  for (const n of useSession.getState().nodes.values()) {
    if (n.props.predefinedType !== 'DRAFT' || n.props.viewId !== viewId || !n.props.shape) continue;
    const sh = fromJson(n.props.shape as Record<string, unknown>);
    if (sh) out.set(n.id, sh);
  }
  return out;
}

/**
 * What shapes about to be drawn in a sketch (a view whose shapes build a Studio solid, one that has constraints, or drawing with
 * a role) keep of how their points were placed: constraints whose refs name the new shapes by `newShapeId`, sent with them. The
 * engine adds and solves them with the drawing; a batch the solver refuses is tried one by one there, so what holds stays.
 */
function recordWhileDrawing(viewId: string, role: string, shapes: Shape[], hints?: DrawHints): ConstraintSpec[] {
  if (!usePrefs.getState().constrainWhileDrawing) { prevDrawn = null; return []; }
  if (!(viewHasConstraints(viewId) || role || sketchViews(useSession.getState().nodes.values()).has(viewId))) { prevDrawn = null; return []; }
  const world = draftsOf(viewId);
  if (prevDrawn) world.set(prevDrawn.id, prevDrawn.shape);
  return shapes.flatMap((shape, i) => {
    const drawn = { id: newShapeId(i), shape };
    const list = inferConstraints(drawn, trail, world, { prev: prevDrawn, hints });
    prevDrawn = drawn;
    return list;
  });
}

// ─── Commands ──────────────────────────────────────────────────────────────

function line(): Step {
  const pts: Vec2[] = [];
  const from = (a: Vec2): Step => ({
    prompt: pts.length >= 2 ? 'Next point [Close/Undo]' : 'Next point [Undo]', want: 'point', base: a,
    preview: (c) => [{ type: 'line', a, b: c }],
    // A segment refused (a locked layer, no view) is not one: the line goes on from where it was, and U never undoes an edit of before.
    point: async (b) => { if (!(await draw([{ type: 'line', a, b }])).length) return from(a); pts.push(b); return from(b); },
    keywords: {
      C: async () => { if (pts.length >= 3) await draw([{ type: 'line', a, b: pts[0] }]); return null; },
      U: async () => { if (pts.length < 2) return from(a); await undo(); pts.pop(); return from(pts[pts.length - 1]); },
    },
    enter: () => null,
  });
  return { prompt: 'First point', want: 'point', point: (a) => { pts.push(a); return from(a); } };
}

function pline(): Step {
  const pts: Vec2[] = [];
  const finish = async (closed: boolean) => { if (pts.length >= (closed ? 3 : 2)) await draw([{ type: 'polyline', pts: [...pts], closed }]); return null; };
  const next = (): Step => ({
    prompt: 'Next point [Close/Undo], Enter to finish', want: 'point', base: pts[pts.length - 1],
    preview: (c) => [{ type: 'polyline', pts: [...pts, c], closed: false }],
    point: (p) => { pts.push(p); return next(); },
    keywords: { C: () => finish(true), U: () => { pts.pop(); return pts.length ? next() : pline(); } },
    enter: () => finish(false),
  });
  return { prompt: 'First point', want: 'point', point: (p) => { pts.push(p); return next(); } };
}

const rectOf = (a: Vec2, b: Vec2): Shape => ({ type: 'polyline', pts: [a, { x: b.x, y: a.y }, b, { x: a.x, y: b.y }], closed: true });
function rectangle(): Step {
  return {
    prompt: 'First corner', want: 'point',
    point: (a) => ({ prompt: 'Other corner (or @width,height)', want: 'point', base: a, rect: 'RECTANGLE', preview: (c) => [rectOf(a, c)], point: async (b) => { await draw([rectOf(a, b)], { rect: true }); return null; } }),
  };
}

function circle(): Step {
  return {
    prompt: 'Centre', want: 'point',
    point: (c) => ({
      prompt: 'Radius (a point or a value)', want: 'point', base: c,
      preview: (p) => [{ type: 'circle', c, r: Math.max(dist(c, p), 1e-6) }],
      point: async (p) => { if (dist(c, p) > 1e-9) await draw([{ type: 'circle', c, r: dist(c, p) }], { radius: typedAt(p)?.length }); return null; },
      value: async (r) => { if (r > 0) await draw([{ type: 'circle', c, r }], { radius: r }); return null; },
    }),
  };
}

/** The arc that goes on from the last line's or arc's end, tangent to it: only its end point is picked. */
function arcContinued(): Step | null {
  const h = host();
  const k = h ? continuation(h.viewId) : null;
  if (!h || !k) { log('There is no line or arc to continue from.'); return null; }
  const unit = Math.hypot(k.dir.x, k.dir.y);
  return {
    prompt: 'End point (tangent to the last line or arc)', want: 'point', base: k.p,
    // The arc, with the tangent it leaves along drawn as a line, as the hint of what holds it.
    preview: (c) => {
      const hint: Shape = { type: 'line', a: k.p, b: { x: k.p.x + (k.dir.x / unit) * Math.max(dist(k.p, c) * 0.5, 0.05), y: k.p.y + (k.dir.y / unit) * Math.max(dist(k.p, c) * 0.5, 0.05) } };
      const t = arcTangent(k.p, k.dir, c);
      return t ? [{ type: 'arc', c: t.c, r: t.r, start: t.start, end: t.end }, hint] : [hint, { type: 'line', a: k.p, b: c }];
    },
    point: async (e) => {
      const t = arcTangent(k.p, k.dir, e);
      if (!t) { log('The end point lies on the tangent line.'); return null; }
      await draw([{ type: 'arc', c: t.c, r: t.r, start: t.start, end: t.end }], { tangentFrom: { ref: k.ref, p: k.p }, cw: t.cw });
      return null;
    },
  };
}

function arc(): Step {
  return {
    prompt: 'Start point [Tangent]', want: 'point',
    keywords: { T: () => arcContinued() },
    enter: () => arcContinued(),
    point: (a) => ({
      prompt: 'Point on the arc', want: 'point', base: a, preview: (c) => [{ type: 'line', a, b: c }],
      point: (m) => ({
        prompt: 'End point', want: 'point', base: m,
        preview: (c) => { const s = arcThrough(a, m, c); return s ? [s] : [{ type: 'line', a, b: c }]; },
        point: async (b) => { const s = arcThrough(a, m, b); if (s) await draw([s]); else log('The three points lie on a line.'); return null; },
      }),
    }),
  };
}

/** The selection a modify command works on: what is selected, else picked now (Enter when done). */
/** The selection, or the objects clicked one by one until Enter; then what to do with them. */
export function withSelection(then: (ids: string[]) => Next): Step | Promise<Step | null | void> | null | void {
  const sel = useUi.getState().selected;
  if (sel.length) return then(sel);
  const collect = (): Step => ({
    prompt: `Select objects (${useUi.getState().selected.length} selected) [WPolygon/CPolygon], Enter when done`, want: 'select',
    point: (p) => { const id = useCad.getState().host?.pickAny(p); if (id) select(id, true); return collect(); },
    keywords: { WP: () => selectPolygon(true, collect), CP: () => selectPolygon(false, collect) },
    enter: () => { const ids = useUi.getState().selected; if (!ids.length) return null; return then(ids); },
  });
  return collect();
}

/**
 * AutoCAD's WP and CP: the corners of a polygon clicked one by one, Enter
 * closes it and adds what it takes to the selection: wholly inside (window)
 * or touching it (crossing); then back to selecting.
 */
function selectPolygon(windowMode: boolean, back: () => Step): Step {
  const pts: Vec2[] = [];
  const name = windowMode ? 'Window polygon' : 'Crossing polygon';
  const next = (): Step => ({
    prompt: `${name}: next corner [Undo], Enter to close`, want: 'point', base: pts[pts.length - 1],
    preview: (c) => [{ type: 'polyline', pts: [...pts, c], closed: pts.length >= 2 }],
    point: (p) => { pts.push(p); return next(); },
    keywords: { U: () => { pts.pop(); return pts.length ? next() : back(); } },
    enter: () => {
      if (pts.length < 3) { log('A polygon needs three corners.'); return back(); }
      const ids = useCad.getState().host?.inRegion?.(lassoTest(pts, windowMode)) ?? [];
      const before = useUi.getState().selected;
      const all = [...new Set([...before, ...ids])];
      useUi.setState({ selected: all, selection: all[all.length - 1] ?? null });
      log(`${all.length - before.length} added.`);
      return back();
    },
  });
  return { prompt: `${name}: first corner`, want: 'point', point: (p) => { pts.push(p); return next(); } };
}

/** What the ids look like in the view: every shape of each (an element's outline in plan), else a drafted shape's first. */
const shapesOf = (ids: string[]): Shape[] => {
  const h = useCad.getState().host;
  if (!h) return [];
  return ids.flatMap((id) => { const o = h.outlinesOf?.(id); if (o?.length) return o; const s = h.shapeOf(id); return s ? [s] : []; });
};
const moved = (s: Shape, d: Vec2): Shape => {
  const m = (p: Vec2) => ({ x: p.x + d.x, y: p.y + d.y });
  switch (s.type) {
    case 'line': return { ...s, a: m(s.a), b: m(s.b) };
    case 'polyline': return { ...s, pts: s.pts.map(m) };
    default: return { ...s, c: m(s.c) };
  }
};

/** In a section or an elevation, the view's frame for element.transform: its points are the view's, the model's elements move in the model (decision 453). */
export function frameArg(at: Vec2): { frame?: ViewFrame } {
  const f = useCad.getState().host?.frameAt?.(at);
  return f ? { frame: f } : {};
}

function moveOrCopy(copy: boolean): Step | null | void | Promise<Step | null | void> {
  // In the 3D view, a move in 3D snapped to the bodies (docs/plan-3d-modelling.md, report #104): Move3D.tsx takes it.
  // A sketch's work plane open, or only drafted shapes selected: the plane's own move (decision 375).
  if (in3dModel()) return withSelection((ids) => { if (!draftsOnly(ids)) { beginMove3d(ids, copy); return null; } return moveOnPlane(copy, ids); });
  return withSelection((ids) => moveOnPlane(copy, ids));
}

/** Whether a move is the 3D view's own: in 3D, no sketch's work plane open. */
const in3dModel = () => useUi.getState().view === '3d' && !useWorkPlane.getState().plane;
/** Drafted shapes and annotations only: they lie on their view's plane. */
const draftsOnly = (ids: readonly string[]) => { const nodes = useSession.getState().nodes; return ids.every((id) => nodes.get(id)?.type === 'IfcAnnotation'); };

function moveOnPlane(copy: boolean, ids: string[]): Step {
  return {
    prompt: 'Base point', want: 'point',
    point: (base) => {
      // What is moved, where it is now: the ghost left at the start, and what the preview carries.
      const ghost = shapesOf(ids);
      const second = (): Step => ({
        prompt: copy ? 'Second point (Enter to finish)' : 'Second point', want: 'point', base, trace: ghost,
        preview: (c) => ghost.map((s) => moved(s, { x: c.x - base.x, y: c.y - base.y })),
        point: async (p) => {
          await execute('element.transform', { ids, op: copy ? 'copy' : 'move', by: [p.x - base.x, p.y - base.y], ...frameArg(base) });
          return copy ? second() : null;
        },
        enter: () => null,
      });
      return second();
    },
  };
}

/** The UCS's X axis in the plan (degrees), for a typed direction; 0 outside a plan. */
const ucsDegrees = (): number => (useCad.getState().host?.isPlan?.() ? ucsOf(useLocation.getState().loc)?.angle ?? 0 : 0);

/**
 * ROTATE: the base point (the hinge), then the angle (typed, or a point: measured from X), with the keywords
 * [Reference/Copy] as AutoCAD's. Reference: the hinge, a point (or a typed angle) giving the reference direction,
 * then a point (or a typed angle) giving the new one; the turn is the difference. Copy turns a copy and leaves the
 * originals. Either way the selection turns in the preview and the angle shows at the cursor.
 */
function rotate(startWith: 'angle' | 'reference' = 'angle'): Step | null | void | Promise<Step | null | void> {
  let copy = false;
  return withSelection((ids) => ({
    prompt: 'Base point', want: 'point',
    point: (c) => {
      const ghost = shapesOf(ids);
      const go = async (deg: number) => {
        await execute('element.transform', { ids, op: 'rotate', center: xy(c), angle: deg, ...(copy ? { copy: true } : {}), ...frameArg(c) });
        return null;
      };
      const turnPreview = (deg: number): Shape[] => ghost.map((g) => rotatedShape(g, c, deg));
      const readout = (deg: number) => (cursor: Vec2) => [{ at: cursor, text: `∠ ${fmt(normalizeTurn(deg))}°` }];
      const flag = () => `Copy: ${copy ? 'yes' : 'no'}`;
      const keywords = (): Record<string, () => Next> => ({ R: () => reference(), C: () => { copy = !copy; return angle(); } });
      // The new direction, after a reference direction (an angle in degrees from +X in the plan).
      const target = (ref: number): Step => ({
        prompt: 'New direction (a point or an angle)', want: 'point', base: c,
        preview: (p) => [{ type: 'line', a: c, b: p }, ...turnPreview(rotationFromReference(c, ref, p))],
        labels: (p) => readout(rotationFromReference(c, ref, p))(p),
        scalar: (n) => go(rotationFromReference(c, ref, n, ucsDegrees())),
        point: (p) => go(rotationFromReference(c, ref, p)),
      });
      const reference = (): Step => ({
        prompt: 'Reference direction (a point, or an angle)', want: 'point', base: c,
        preview: (p) => [{ type: 'line', a: c, b: p }],
        scalar: (n) => target(directionFrom(c, n, ucsDegrees())),
        point: (p) => target(directionOf(c, p)),
      });
      // The ribbon's Rotate: hinge → reference point → target point; an angle typed at the reference step is the turn itself.
      const hingeFirst = (): Step => ({
        prompt: `Reference point (or a rotation angle) [Copy] (${flag()})`, want: 'point', base: c,
        preview: (p) => [{ type: 'line', a: c, b: p }],
        scalar: go, point: (p) => target(directionOf(c, p)),
        keywords: { C: () => { copy = !copy; return hingeFirst(); } },
      });
      if (startWith === 'reference') return hingeFirst();
      const angle = (): Step => ({
        prompt: `Angle (degrees, or a point) [Reference/Copy] (${flag()})`, want: 'point', base: c,
        preview: (p) => [{ type: 'line', a: c, b: p }, ...turnPreview(directionOf(c, p))],
        labels: (p) => readout(directionOf(c, p))(p),
        scalar: go, point: (p) => go(directionOf(c, p)), keywords: keywords(),
      });
      return angle();
    },
  }));
}

function mirror(): Step | null | void | Promise<Step | null | void> {
  let erase = false;
  return withSelection((ids) => ({
    prompt: 'First point of the mirror line', want: 'point',
    point: (a) => {
      const second = (): Step => ({
        prompt: `Second point [Erase source: ${erase ? 'yes' : 'no'}]`, want: 'point', base: a, preview: (p) => [{ type: 'line', a, b: p }],
        point: async (b) => { await execute('element.transform', { ids, op: 'mirror', a: xy(a), b: xy(b), copy: !erase, ...frameArg(a) }); return null; },
        keywords: { E: () => { erase = !erase; return second(); } },
      });
      return second();
    },
  }));
}

function scale(): Step | null | void | Promise<Step | null | void> {
  return withSelection((ids) => ({
    prompt: 'Base point', want: 'point',
    point: (c) => ({ prompt: 'Scale factor', want: 'none', base: c, scalar: async (k) => { await execute('drafting.transform', { ids, op: 'scale', center: xy(c), factor: k }); return null; } }),
  }));
}

function erase(): Step | null | void | Promise<Step | null | void> {
  return withSelection(async (ids) => { await deleteElements(ids); select(null); return null; });
}

/**
 * OFFSET (O): a distance typed (the project's unit) or Through a point; then, again and again until Enter or Escape,
 * the shape to offset and the side it goes (lines, polylines and rectangles with mitred corners, circles, arcs).
 */
function offset(): Step {
  const through = () => useCad.getState().offsetThrough;
  const pickStep = (): Step => ({
    prompt: through()
      ? 'Pick what to offset (through a point; type a distance to give one)'
      : `Pick what to offset (distance ${len(useCad.getState().offset)}; type a distance to change) [Through]`,
    want: 'pick',
    value: (d) => { if (d > 0) useCad.setState({ offset: d, offsetThrough: false }); return pickStep(); },
    keywords: { T: () => { useCad.setState({ offsetThrough: !through() }); return pickStep(); }, THROUGH: () => { useCad.setState({ offsetThrough: !through() }); return pickStep(); } },
    pick: (hit) => ({
      prompt: through() ? 'Through which point?' : 'Which side?', want: 'point',
      point: async (side) => {
        await execute('drafting.offset', { id: hit.id, side: xy(side), ...(through() ? { through: true } : { distance: useCad.getState().offset }) });
        return pickStep();
      },
    }),
    enter: () => null,
  });
  return pickStep();
}

function trimOrExtend(kind: 'trim' | 'extend'): Step {
  const step = (): Step => ({
    prompt: kind === 'trim' ? 'Pick the piece to cut away (Enter to finish)' : 'Pick the end to extend (Enter to finish)', want: 'pick',
    pick: async (hit, at) => { await execute(`drafting.${kind}`, { id: hit.id, at: xy(at) }); return step(); },
    enter: () => null,
  });
  return step();
}

function corner(kind: 'fillet' | 'chamfer'): Step {
  const first = (): Step => ({
    prompt: kind === 'fillet' ? `First line (radius ${len(useCad.getState().radius)}; type a value to change)` : `First line (distance ${len(useCad.getState().chamfer)}; type a value to change)`,
    want: 'pick',
    value: (v) => { if (v >= 0) useCad.setState(kind === 'fillet' ? { radius: v } : { chamfer: v }); return first(); },
    pick: (a, atA) => ({
      prompt: 'Second line', want: 'pick',
      pick: async (b, atB) => {
        const s = useCad.getState();
        await execute(`drafting.${kind}`, kind === 'fillet' ? { a: a.id, atA: xy(atA), b: b.id, atB: xy(atB), radius: s.radius } : { a: a.id, atA: xy(atA), b: b.id, atB: xy(atB), d1: s.chamfer, d2: s.chamfer });
        return null;
      },
    }),
  });
  return first();
}

function hatch(): Step {
  const step = (): Step => ({
    prompt: `Click inside an area [pattern ${useCad.getState().pattern}, scale ${fmt(useCad.getState().hatchScale)}: type a pattern name or a scale]`, want: 'point',
    point: async (p) => {
      const h = host(); if (!h) return null;
      const s = useCad.getState();
      await execute('drafting.hatch', { viewId: h.viewId, at: xy(p), pattern: s.pattern, scale: s.hatchScale, layer: s.layer });
      return step();
    },
    scalar: (v) => { if (v > 0) useCad.setState({ hatchScale: v }); return step(); },
    text: (t) => { useCad.setState({ pattern: t.trim().toUpperCase() }); return step(); },
    enter: () => null,
  });
  return step();
}

function text(): Step {
  return {
    prompt: 'Where?', want: 'point',
    point: (p) => ({
      prompt: 'Text (Enter to place)', want: 'text',
      text: async (t) => { const h = host(); if (h && t.trim()) await execute('drafting.addText', { viewId: h.viewId, at: xy(p), text: t.trim() }); return null; },
    }),
  };
}

function layer(): Step {
  return {
    prompt: `Current layer ${useCad.getState().layer}: type a layer's name (a new one is made)`, want: 'text',
    text: async (t) => {
      const name = t.trim();
      if (!name) return null;
      await execute('drafting.setLayer', { name });
      useCad.setState({ layer: name });
      log(`Layer ${name}.`);
      return null;
    },
  };
}

/** What a drafted shape is for (Studio's operations find their parts by it); draft: construction lines, never printed. */
export const ROLES = ['draft', 'profile', 'hole', 'path', 'rail', 'axis', 'endProfile'] as const;
export const roleLabel = (r: string) => ({ draft: 'construction', endProfile: 'end profile' } as Record<string, string>)[r] ?? r;

export type Starter = () => Step | null | void | Promise<Step | null | void>;
/** A command: its words, its label, how it starts, and what it lets one choose while it runs (TAG's field), shown in the command line's bar. */
export interface CommandDef { names: string[]; label: string; start: Starter; options?: ComponentType }
export const COMMANDS: CommandDef[] = [
  { names: ['LINE', 'L'], label: 'Line', start: line },
  { names: ['PLINE', 'PL'], label: 'Polyline', start: pline },
  { names: ['RECTANGLE', 'REC', 'RECT'], label: 'Rectangle', start: rectangle },
  { names: ['CIRCLE', 'C'], label: 'Circle', start: circle },
  { names: ['ARC', 'A'], label: 'Arc', start: arc },
  { names: ['MOVE', 'M', 'MV'], label: 'Move', start: () => moveOrCopy(false) },
  { names: ['COPY', 'CO', 'CP'], label: 'Copy', start: () => moveOrCopy(true) },
  { names: ['ROTATE', 'RO'], label: 'Rotate', start: () => rotate() },
  // The ribbon's Rotate: the same command, starting in the Reference flow (hinge, reference point, target point).
  { names: ['ROTATEREF'], label: 'Rotate', start: () => rotate('reference') },
  { names: ['MIRROR', 'MI', 'MM'], label: 'Mirror', start: mirror },
  { names: ['SCALE', 'SC'], label: 'Scale', start: scale },
  { names: ['ERASE', 'E', 'DELETE'], label: 'Erase', start: erase },
  { names: ['OFFSET', 'O', 'OF'], label: 'Offset', start: offset },
  { names: ['TRIM', 'TR'], label: 'Trim', start: () => trimOrExtend('trim') },
  { names: ['EXTEND', 'EX'], label: 'Extend', start: () => trimOrExtend('extend') },
  { names: ['FILLET', 'F'], label: 'Fillet', start: () => corner('fillet') },
  { names: ['CHAMFER', 'CHA'], label: 'Chamfer', start: () => corner('chamfer') },
  { names: ['HATCH', 'H', 'BHATCH'], label: 'Hatch', start: hatch },
  { names: ['TEXT', 'T', 'DT', 'MTEXT'], label: 'Text', start: text },
  { names: ['LAYER', 'LA'], label: 'Layer', start: layer },
];

/** Words the command line also takes for a tool of the ribbon (MEASURE, DIST: the Measure tool, as CAD users type it). */
export const TOOL_WORDS: Record<string, string> = { MEASURE: 'measure', MEA: 'measure', DIST: 'measure', DISTANCE: 'measure' };

/** What a word no command knows may still be (a keyboard shortcut's letters, ZF ↵): true when it ran something. */
let fallback: (text: string) => boolean = () => false;
export const setCommandFallback = (f: (text: string) => boolean) => { fallback = f; };

/** The user's own alias for a typed word (decision 473): a command-line command to start, or `done` when a registry command ran. */
let aliasHook: (text: string) => { cad: string } | { done: true } | null = () => null;
export const setAliasHook = (f: typeof aliasHook) => { aliasHook = f; };

export const findCommand = (name: string) => COMMANDS.find((c) => c.names.includes(name.trim().toUpperCase()));

/** A module's own command for the command line (Studio's EXTRUDE…). */
export function registerCommand(c: CommandDef): void {
  if (!COMMANDS.some((x) => x.names[0] === c.names[0])) COMMANDS.push(c);
}

/** For a module's commands: the log, the host, ending or chaining steps. */
export const cadLog = log;
export const cadHost = host;
export { moved as movedShape };

// ─── Input ─────────────────────────────────────────────────────────────────

/**
 * Input reaches the steps one at a time: a step answers (often after an
 * engine round trip) before the next point or word is fed, so a double-click
 * goes to the step the first click made, not twice to the same one.
 */
let queue: Promise<unknown> = Promise.resolve();
function serial<A extends unknown[], R>(fn: (...a: A) => Promise<R>): (...a: A) => Promise<R> {
  return (...a) => {
    const r = queue.then(() => fn(...a));
    queue = r.catch(() => undefined);
    return r;
  };
}

export const startCommand = serial(runCommand);

async function runCommand(name: string): Promise<boolean> {
  const c = findCommand(name);
  if (!c) return false;
  cancel(true);
  resetRecording();
  useUi.setState({ tool: null });
  log(`> ${c.names[0]}`);
  useCad.setState({ command: c.names[0], last: c.names[0], buffer: '' });
  await advance(c.start());
  return true;
}

export function cancel(quiet = false): void {
  const s = useCad.getState();
  if (s.command && !quiet) log('*Cancel*');
  if (s.step?.escape) void Promise.resolve(s.step.escape()).catch(() => undefined);
  generation++;
  resetRecording();
  useCad.setState({ command: null, step: null, buffer: '' });
  liveEnd('cad');
  traceEnd('cad');
}

/** A point clicked in a view (already snapped). */
const feed = serial(async (p: Vec2, typed: { length?: number; angle?: number } | undefined, free: boolean, aim: Aimed | null): Promise<void> => {
  const s = useCad.getState();
  if (!s.step) return;
  useCad.setState({ lastPoint: p });
  if (s.step.want === 'pick' && s.host) {
    const hit = s.host.pickDraft(p);
    if (!hit) { log('Nothing drafted there.'); return; }
    await advance(s.step.pick?.(hit, p));
    return;
  }
  if (s.step.point) { noteFed(p, { typed, free, aim }); await advance(s.step.point(p)); }
});
/** A point clicked in a view (already snapped), or typed at its cursor (`typed`: the length and angle). How it was found, and whether Ctrl placed it free, is read now. */
export const feedPoint = (p: Vec2, typed?: { length?: number; angle?: number }): Promise<void> => feed(p, typed, ctrlHeld, useCad.getState().aim);

/** An element clicked in the 3D view while a step takes elements (`element`): true when it took it. */
export function feedElement(id: string): boolean {
  if (!useCad.getState().step?.element) return false;
  void serial(async () => { const step = useCad.getState().step; if (step?.element) await advance(step.element(id)); })();
  return true;
}

/** A right-click in a view while a step runs: what Enter does (finish, accept); false when no step takes it. */
export const finishStep = serial(async (): Promise<boolean> => {
  const s = useCad.getState();
  if (!s.step?.enter) return false;
  log('');
  await advance(s.step.enter());
  return true;
});

/** What was typed, on Enter or Space. */
export const submit = serial(async (raw: string): Promise<void> => {
  const s = useCad.getState();
  const text = raw.trim();
  useCad.setState({ buffer: '' });
  if (!s.step) {
    if (!text) { if (s.last) await runCommand(s.last); return; }
    const alias = aliasHook(text);
    if (alias) { if ('cad' in alias) await runCommand(alias.cad); return; }
    if (!(await runCommand(text)) && !fallback(text)) log(`Unknown command „${text}”.`);
    return;
  }
  const step = s.step;
  if (!text) { log(''); await advance(step.enter ? step.enter() : null); return; }
  log(text);
  if (step.want === 'text' && step.text) { await advance(step.text(raw)); return; }
  const kw = text.toUpperCase();
  if (step.keywords?.[kw]) { await advance(step.keywords[kw]()); return; }
  const n = step.scalar ? parseScalar(text, step.want !== 'point') : null;
  if (n != null && step.scalar) { await advance(step.scalar(n)); return; }
  if (step.want === 'point' || step.want === 'select' || step.want === 'pick' || step.want === 'none') {
    const frame = s.host?.isPlan?.() ? ucsOf(useLocation.getState().loc) : null;
    const parsed = parseCoordinateInput(text, { last: step.base ?? s.lastPoint, frame });
    if (parsed?.kind === 'point' && step.point) {
      useCad.setState({ lastPoint: parsed.pt });
      // A rectangle's corner typed as @width,height: its size is what was typed.
      const base = step.base, size = step.rect && base && /^@/.test(text) && text.includes(',') ? { w: Math.abs(parsed.pt.x - base.x), h: Math.abs(parsed.pt.y - base.y) } : undefined;
      noteFed(parsed.pt, size ? { typed: { size } } : {});
      await advance(step.point(parsed.pt));
      return;
    }
    if (parsed?.kind === 'distance') {
      if (step.value) { await advance(step.value(parsed.d)); return; }
      // A distance from the base toward the cursor (squared by ortho).
      if (step.base && step.point) {
        const toward = s.cursor ? (s.ortho ? applyOrtho(step.base, s.cursor, frame) : s.cursor) : { x: step.base.x + 1, y: step.base.y };
        const p = pointAlong(step.base, toward, parsed.d);
        useCad.setState({ lastPoint: p });
        noteFed(p, { typed: { length: parsed.d }, aim: s.aim });
        await advance(step.point(p));
        return;
      }
    }
    // A length with its own unit or a sum (2.4m, 240cm, 3'6", 3000+250).
    const l = step.value && !parsed ? parseLength(text) : null;
    if (l != null && step.value) { await advance(step.value(l)); return; }
  }
  if (/^[a-z][a-z0-9_.-]*$/i.test(text) && step.text) { await advance(step.text(text)); return; }
  log(`„${text}”? ${step.prompt}`);
});
