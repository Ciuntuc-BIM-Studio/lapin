/**
 * The CAD command line (the old Lapin's drafting session): a command is a
 * chain of steps, each asking for a point, a picked shape, a selection, a
 * value or a text, with keywords. Clicks in a 2D view and typed input feed the
 * running step; Enter or Space ends it (or repeats the last command), Escape
 * cancels. Each finished edit is one engine command, so one undo step.
 */

import { create } from 'zustand';
import type { Vec2 } from '../../platform/module';
import { select, useUi } from '../../app/store';
import { execute, undo } from '../../engine/session';
import { applyOrtho, parseCoordinateInput, pointAlong } from './input';
import { arcThrough, dist, toJson, type Shape } from './shapes';
import { liveBegin, liveEnd } from '../liveInput';

/** What a 2D view lends the command line: its view, and what lies under a point. */
export interface CadHost {
  viewId: string;
  /** The drafted shape under a point, with its exact geometry. */
  pickDraft(p: Vec2): { id: string; shape: Shape } | null;
  /** Anything selectable under a point (a drafted shape, an annotation, an element). */
  pickAny(p: Vec2): string | null;
  /** A drafted shape's geometry, for previews. */
  shapeOf(id: string): Shape | null;
}

type Next = Step | null | void | Promise<Step | null | void>;
export interface Step {
  prompt: string;
  want: 'point' | 'pick' | 'select' | 'text' | 'none';
  /** The point typed coordinates and distances are relative to, and ortho squares to. */
  base?: Vec2 | null;
  preview?: (cursor: Vec2) => Shape[];
  point?: (p: Vec2) => Next;
  pick?: (hit: { id: string; shape: Shape }, at: Vec2) => Next;
  value?: (v: number) => Next;
  text?: (s: string) => Next;
  /** Keywords (upper case) and what they do. */
  keywords?: Record<string, () => Next>;
  /** Enter with nothing typed. */
  enter?: () => Next;
}

interface CadState {
  buffer: string;
  history: string[];
  command: string | null;
  step: Step | null;
  last: string | null;
  cursor: Vec2 | null;
  lastPoint: Vec2 | null;
  osnap: boolean;
  ortho: boolean;
  polar: boolean;
  layer: string;
  host: CadHost | null;
  /** Remembered values: offset distance, fillet radius, chamfer distances, hatch pattern and scale. */
  offset: number;
  radius: number;
  chamfer: number;
  pattern: string;
  hatchScale: number;
}

export const useCad = create<CadState>(() => ({
  buffer: '', history: [], command: null, step: null, last: null, cursor: null, lastPoint: null,
  osnap: true, ortho: false, polar: true, layer: '0', host: null,
  offset: 0.5, radius: 0, chamfer: 0.1, pattern: 'LINES45', hatchScale: 1,
}));

const log = (line: string) => useCad.setState((s) => ({ history: [...s.history, line].slice(-50) }));
const fmt = (v: number) => String(Math.round(v * 1000) / 1000);
const V = (p: Vec2) => [p.x, p.y];

/** Bumped when a command is cancelled or another starts: a step that answers after that is dropped. */
let generation = 0;

async function advance(next: Next): Promise<void> {
  const g = generation;
  const step = await next;
  if (g !== generation) return;
  if (step) useCad.setState({ step });
  else useCad.setState({ step: null, command: null });
  // A point asked for from a base: its length and angle can be typed at the cursor.
  if (step && step.want === 'point' && step.base) liveBegin('cad', step.base, (p) => void feedPoint(p));
  else liveEnd('cad');
}

function host(): CadHost | null {
  const h = useCad.getState().host;
  if (!h) log('Open a plan, section or elevation to draft on.');
  return h;
}

async function draw(shapes: Shape[]): Promise<string[]> {
  const h = host();
  if (!h || !shapes.length) return [];
  const r = await execute('drafting.draw', { viewId: h.viewId, layer: useCad.getState().layer, shapes: shapes.map(toJson) }) as { ids?: string[] } | null;
  return r?.ids ?? [];
}

// ─── Commands ──────────────────────────────────────────────────────────────

function line(): Step {
  const pts: Vec2[] = [];
  const from = (a: Vec2): Step => ({
    prompt: pts.length >= 2 ? 'Next point [Close/Undo]' : 'Next point [Undo]', want: 'point', base: a,
    preview: (c) => [{ type: 'line', a, b: c }],
    point: async (b) => { await draw([{ type: 'line', a, b }]); pts.push(b); return from(b); },
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
    point: (a) => ({ prompt: 'Other corner (or @width,height)', want: 'point', base: a, preview: (c) => [rectOf(a, c)], point: async (b) => { await draw([rectOf(a, b)]); return null; } }),
  };
}

function circle(): Step {
  return {
    prompt: 'Centre', want: 'point',
    point: (c) => ({
      prompt: 'Radius (a point or a value)', want: 'point', base: c,
      preview: (p) => [{ type: 'circle', c, r: Math.max(dist(c, p), 1e-6) }],
      point: async (p) => { if (dist(c, p) > 1e-9) await draw([{ type: 'circle', c, r: dist(c, p) }]); return null; },
      value: async (r) => { if (r > 0) await draw([{ type: 'circle', c, r }]); return null; },
    }),
  };
}

function arc(): Step {
  return {
    prompt: 'Start point', want: 'point',
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
function withSelection(then: (ids: string[]) => Next): Step | Promise<Step | null | void> | null | void {
  const sel = useUi.getState().selected;
  if (sel.length) return then(sel);
  const collect = (): Step => ({
    prompt: `Select objects (${useUi.getState().selected.length} selected), Enter when done`, want: 'select',
    point: (p) => { const id = useCad.getState().host?.pickAny(p); if (id) select(id, true); return collect(); },
    enter: () => { const ids = useUi.getState().selected; if (!ids.length) return null; return then(ids); },
  });
  return collect();
}

const shapesOf = (ids: string[]) => ids.map((id) => useCad.getState().host?.shapeOf(id)).filter((s): s is Shape => !!s);
const moved = (s: Shape, d: Vec2): Shape => {
  const m = (p: Vec2) => ({ x: p.x + d.x, y: p.y + d.y });
  switch (s.type) {
    case 'line': return { ...s, a: m(s.a), b: m(s.b) };
    case 'polyline': return { ...s, pts: s.pts.map(m) };
    default: return { ...s, c: m(s.c) };
  }
};

function moveOrCopy(copy: boolean): Step | null | void | Promise<Step | null | void> {
  return withSelection((ids) => ({
    prompt: 'Base point', want: 'point',
    point: (base) => {
      const second = (): Step => ({
        prompt: copy ? 'Second point (Enter to finish)' : 'Second point', want: 'point', base,
        preview: (c) => shapesOf(ids).map((s) => moved(s, { x: c.x - base.x, y: c.y - base.y })),
        point: async (p) => {
          await execute('element.transform', { ids, op: copy ? 'copy' : 'move', by: [p.x - base.x, p.y - base.y] });
          return copy ? second() : null;
        },
        enter: () => null,
      });
      return second();
    },
  }));
}

function rotate(): Step | null | void | Promise<Step | null | void> {
  return withSelection((ids) => ({
    prompt: 'Base point', want: 'point',
    point: (c) => {
      const go = async (deg: number) => { await execute('element.transform', { ids, op: 'rotate', center: V(c), angle: deg }); return null; };
      return { prompt: 'Angle (degrees, or a point)', want: 'point', base: c, preview: (p) => [{ type: 'line', a: c, b: p }], value: go, point: (p) => go((Math.atan2(p.y - c.y, p.x - c.x) * 180) / Math.PI) };
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
        point: async (b) => { await execute('element.transform', { ids, op: 'mirror', a: V(a), b: V(b), copy: !erase }); return null; },
        keywords: { E: () => { erase = !erase; return second(); } },
      });
      return second();
    },
  }));
}

function scale(): Step | null | void | Promise<Step | null | void> {
  return withSelection((ids) => ({
    prompt: 'Base point', want: 'point',
    point: (c) => ({ prompt: 'Scale factor', want: 'none', base: c, value: async (k) => { await execute('drafting.transform', { ids, op: 'scale', center: V(c), factor: k }); return null; } }),
  }));
}

function erase(): Step | null | void | Promise<Step | null | void> {
  return withSelection(async (ids) => { await execute('element.delete', { ids }); select(null); return null; });
}

function offset(): Step {
  const pickStep = (): Step => ({
    prompt: `Pick what to offset (distance ${fmt(useCad.getState().offset)}; type a value to change)`, want: 'pick',
    value: (d) => { if (d > 0) useCad.setState({ offset: d }); return pickStep(); },
    pick: (hit) => ({
      prompt: 'Which side?', want: 'point',
      point: async (side) => { await execute('drafting.offset', { id: hit.id, distance: useCad.getState().offset, side: V(side) }); return pickStep(); },
    }),
    enter: () => null,
  });
  return pickStep();
}

function trimOrExtend(kind: 'trim' | 'extend'): Step {
  const step = (): Step => ({
    prompt: kind === 'trim' ? 'Pick the piece to cut away (Enter to finish)' : 'Pick the end to extend (Enter to finish)', want: 'pick',
    pick: async (hit, at) => { await execute(`drafting.${kind}`, { id: hit.id, at: V(at) }); return step(); },
    enter: () => null,
  });
  return step();
}

function corner(kind: 'fillet' | 'chamfer'): Step {
  const first = (): Step => ({
    prompt: kind === 'fillet' ? `First line (radius ${fmt(useCad.getState().radius)}; type a value to change)` : `First line (distance ${fmt(useCad.getState().chamfer)}; type a value to change)`,
    want: 'pick',
    value: (v) => { if (v >= 0) useCad.setState(kind === 'fillet' ? { radius: v } : { chamfer: v }); return first(); },
    pick: (a, atA) => ({
      prompt: 'Second line', want: 'pick',
      pick: async (b, atB) => {
        const s = useCad.getState();
        await execute(`drafting.${kind}`, kind === 'fillet' ? { a: a.id, atA: V(atA), b: b.id, atB: V(atB), radius: s.radius } : { a: a.id, atA: V(atA), b: b.id, atB: V(atB), d1: s.chamfer, d2: s.chamfer });
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
      await execute('drafting.hatch', { viewId: h.viewId, at: V(p), pattern: s.pattern, scale: s.hatchScale, layer: s.layer });
      return step();
    },
    value: (v) => { if (v > 0) useCad.setState({ hatchScale: v }); return step(); },
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
      text: async (t) => { const h = host(); if (h && t.trim()) await execute('drafting.addText', { viewId: h.viewId, at: V(p), text: t.trim() }); return null; },
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

export type Starter = () => Step | null | void | Promise<Step | null | void>;
export const COMMANDS: { names: string[]; label: string; start: Starter }[] = [
  { names: ['LINE', 'L'], label: 'Line', start: line },
  { names: ['PLINE', 'PL'], label: 'Polyline', start: pline },
  { names: ['RECTANGLE', 'REC', 'RECT'], label: 'Rectangle', start: rectangle },
  { names: ['CIRCLE', 'C'], label: 'Circle', start: circle },
  { names: ['ARC', 'A'], label: 'Arc', start: arc },
  { names: ['MOVE', 'M', 'MV'], label: 'Move', start: () => moveOrCopy(false) },
  { names: ['COPY', 'CO', 'CP'], label: 'Copy', start: () => moveOrCopy(true) },
  { names: ['ROTATE', 'RO'], label: 'Rotate', start: rotate },
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

export const findCommand = (name: string) => COMMANDS.find((c) => c.names.includes(name.trim().toUpperCase()));

/** A module's own command for the command line (Studio's EXTRUDE…). */
export function registerCommand(c: { names: string[]; label: string; start: Starter }): void {
  if (!COMMANDS.some((x) => x.names[0] === c.names[0])) COMMANDS.push(c);
}

/** For a module's commands: the log, the host, ending or chaining steps. */
export const cadLog = log;
export const cadHost = host;

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
  useUi.setState({ tool: null });
  log(`> ${c.names[0]}`);
  useCad.setState({ command: c.names[0], last: c.names[0], buffer: '' });
  await advance(c.start());
  return true;
}

export function cancel(quiet = false): void {
  const s = useCad.getState();
  if (s.command && !quiet) log('*Cancel*');
  generation++;
  useCad.setState({ command: null, step: null, buffer: '' });
  liveEnd('cad');
}

/** A point clicked in a view (already snapped). */
export const feedPoint = serial(async (p: Vec2): Promise<void> => {
  const s = useCad.getState();
  if (!s.step) return;
  useCad.setState({ lastPoint: p });
  if (s.step.want === 'pick' && s.host) {
    const hit = s.host.pickDraft(p);
    if (!hit) { log('Nothing drafted there.'); return; }
    await advance(s.step.pick?.(hit, p));
    return;
  }
  if (s.step.point) await advance(s.step.point(p));
});

/** What was typed, on Enter or Space. */
export const submit = serial(async (raw: string): Promise<void> => {
  const s = useCad.getState();
  const text = raw.trim();
  useCad.setState({ buffer: '' });
  if (!s.step) {
    if (!text) { if (s.last) await runCommand(s.last); return; }
    if (!(await runCommand(text))) log(`Unknown command „${text}”.`);
    return;
  }
  const step = s.step;
  if (!text) { log(''); await advance(step.enter ? step.enter() : null); return; }
  log(text);
  if (step.want === 'text' && step.text) { await advance(step.text(raw)); return; }
  const kw = text.toUpperCase();
  if (step.keywords?.[kw]) { await advance(step.keywords[kw]()); return; }
  if (step.want === 'point' || step.want === 'select' || step.want === 'pick' || step.want === 'none') {
    const parsed = parseCoordinateInput(text, { last: step.base ?? s.lastPoint });
    if (parsed?.kind === 'point' && step.point) { useCad.setState({ lastPoint: parsed.pt }); await advance(step.point(parsed.pt)); return; }
    if (parsed?.kind === 'distance') {
      if (step.value) { await advance(step.value(parsed.d)); return; }
      // A distance from the base toward the cursor (squared by ortho).
      if (step.base && step.point) {
        const toward = s.cursor ? (s.ortho ? applyOrtho(step.base, s.cursor) : s.cursor) : { x: step.base.x + 1, y: step.base.y };
        const p = pointAlong(step.base, toward, parsed.d);
        useCad.setState({ lastPoint: p });
        await advance(step.point(p));
        return;
      }
    }
  }
  if (/^[a-z][a-z0-9_.-]*$/i.test(text) && step.text) { await advance(step.text(text)); return; }
  log(`„${text}”? ${step.prompt}`);
});
