/** Drafting: CAD drawing on views (lines, polylines, circles, arcs, hatches on layers, and their edits), dimensions that follow the elements, texts, grid axes. */

import { ArrowRightToLine, ListPlus, ListMinus, Circle, Copy, CopyPlus, CornerUpRight, Eraser, FileInput, FileText, FlipHorizontal2, Hash, Layers, Link2, Move, MoveHorizontal, RectangleHorizontal, RotateCw, Scaling, Scissors, Slash, Spline, Type, Redo2, PenLine, Tag, Tags } from 'lucide-react';
import { AngleIcon, ArcLengthIcon, DiameterIcon, DimensionIcon, GridAxisIcon, LevelIcon, OrdinateIcon, RadiusIcon, SlopeIcon } from '../../icons';
import type { Grip, GripStep, GripView, LapinModule, RefLine, ToolDef, Vec2 } from '../../platform/module';
import { axisGrips, axisRef, outlineGrips, pt } from '../../viewport/gripKit';
import type { NodeDto } from '../../engine/protocol';
import { notify, select, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { cadHost, cadLog, registerCommand, startCommand, withSelection, type Step } from '../../viewport/cad/session';
import { importDxf } from '../../engine/files';
import { ImportDxfDialog } from './ImportDxfDialog';
import { DimensionSection } from './DimensionSection';
import { DraftSection } from './DraftSection';
import { fromJson, keyPoints, toJson, type Shape } from '../../viewport/cad/shapes';
import { LayersDialog } from './LayersDialog';
import { frontView } from '../../viewport/frontView';

/** An aligned dimension: its two points, then where its line goes; then on as a string, one object. Points on an element's cut edge, a grid axis or a wall's centre line follow it. */
const DRAFTED = new Set(['DRAFT', 'TEXT', 'DIMENSION', 'TAG']);
const draftsFields = (node: NodeDto) => DRAFTED.has(String(node.props.predefinedType));

const dimension: ToolDef = { id: 'drafting.dimension', label: 'Aligned', icon: DimensionIcon, shortcut: 'DI', run: () => void startCommand('DIMALIGNED') };
const text: ToolDef = {
  id: 'drafting.text', label: 'Text', icon: Type, shortcut: 'TX',
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => {
    const body = window.prompt('Text');
    if (body?.trim()) await ctx.execute('drafting.addText', { viewId: ctx.viewId, at: [p.x, p.y], text: body.trim() });
  },
};
/** A grid axis: its two ends; it takes the next label of its direction. */
const axis: ToolDef = {
  id: 'drafting.axis', label: 'Grid axis', icon: GridAxisIcon, shortcut: 'GR',
  gesture: { kind: 'twoPoints' },
  complete: async ({ points: [a, b] }, ctx) => { await ctx.execute('drafting.addAxis', { x1: a.x, y1: a.y, x2: b.x, y2: b.y }); },
};

/**
 * DIM (aligned) and DLI (linear: horizontal or vertical, by where its line
 * goes): two points, then the line; then it goes on as a string, each next
 * point measured from the last on the same line, previewed until Enter makes
 * the whole string one dimension (one object, one undo step; U takes the last
 * point back; S first for a single one). Points on elements, grid axes or
 * wall centre lines follow them.
 */
function dimCommand(linear: boolean): Step {
  let single = false;
  const dirOf = (a: Vec2, b: Vec2, at: Vec2): Vec2 | null => {
    if (!linear) return null;
    const m = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
    return Math.abs(at.y - m.y) >= Math.abs(at.x - m.x) ? { x: 1, y: 0 } : { x: 0, y: 1 };
  };
  const first = (): Step => ({
    prompt: `First point${single ? '' : ' [Single]'}`, want: 'point',
    keywords: { S: () => { single = true; return first(); } },
    point: (a) => ({
      prompt: 'Second point', want: 'point', base: a, preview: (c) => [{ type: 'line', a, b: c }],
      point: (b) => ({
        prompt: 'Where the dimension line goes', want: 'point', base: b,
        preview: (c) => stringPreview([a, b], c, dirOf(a, b, c) ?? unit(a, b)),
        point: async (at) => {
          const dir = dirOf(a, b, at);
          if (single) { await addString([a, b], at, dir); return null; }
          const pts = [a, b];
          // An aligned string measures along its first segment.
          const along = dir ?? unit(a, b);
          const next = (): Step => ({
            prompt: 'Next point of the string [Undo] (Enter to finish)', want: 'point', base: pts[pts.length - 1],
            preview: (c) => stringPreview([...pts, c], at, along),
            point: (p) => { pts.push(p); return next(); },
            keywords: { U: () => { if (pts.length > 2) pts.pop(); return next(); } },
            enter: async () => { await addString(pts, at, dir ?? (collinear(pts) ? null : along)); return null; },
            // Escape keeps the string drawn so far, as AutoCAD keeps a continued dimension.
            escape: () => addString(pts, at, dir ?? (collinear(pts) ? null : along)),
          });
          return next();
        },
      }),
    }),
  });
  return first();
}

const unit = (a: Vec2, b: Vec2): Vec2 => { const l = Math.hypot(b.x - a.x, b.y - a.y) || 1; return { x: (b.x - a.x) / l, y: (b.y - a.y) / l }; };
/** Whether the points lie on the line through the first two. */
const collinear = (pts: Vec2[]): boolean => {
  const u = unit(pts[0], pts[1]);
  return pts.every((p) => Math.abs((p.x - pts[0].x) * u.y - (p.y - pts[0].y) * u.x) < 1e-6);
};

/** A string of points made as one dimension (one engine command). */
async function addString(pts: Vec2[], at: Vec2, dir: Vec2 | null): Promise<void> {
  const h = cadHost();
  if (!h) return;
  const V = (p: Vec2) => [p.x, p.y];
  await execute('drafting.addDimension', { viewId: h.viewId, pts: pts.map(V), at: V(at), ...(dir ? { dir: V(dir) } : {}) });
}

/** How a string will look: its dimension line through `at` along `u`, and each point's extension line onto it. */
function stringPreview(pts: Vec2[], at: Vec2, u: Vec2): Shape[] {
  const n = { x: -u.y, y: u.x };
  const onLine = pts.map((p) => { const o = (at.x - p.x) * n.x + (at.y - p.y) * n.y; return { x: p.x + n.x * o, y: p.y + n.y * o }; });
  const t = onLine.map((q) => (q.x - onLine[0].x) * u.x + (q.y - onLine[0].y) * u.y);
  const lo = Math.min(...t), hi = Math.max(...t);
  const shapes: Shape[] = [{ type: 'line', a: { x: onLine[0].x + u.x * lo, y: onLine[0].y + u.y * lo }, b: { x: onLine[0].x + u.x * hi, y: onLine[0].y + u.y * hi } }];
  pts.forEach((p, i) => shapes.push({ type: 'line', a: p, b: onLine[i] }));
  return shapes;
}
/** Whether an id is a dimension string (a linear or aligned dimension, not an angle, a radius…). */
const isString = (id: string | null | undefined): id is string => {
  const n = id ? useSession.getState().nodes.get(id) : undefined;
  return n?.props.predefinedType === 'DIMENSION' && !n.props.dimKind;
};

/**
 * DIMADD, as ArchiCAD inserts a dimension point: a dimension string (the one
 * selected, else one clicked), then each point clicked goes into it where it
 * falls along its line (inside it, or beyond an end as the new end), holding
 * on the element, axis or wall centre line it lands on; one undo step each,
 * Enter or Esc to finish.
 */
function dimAddCommand(): Step {
  const add = (id: string): Step => ({
    prompt: 'Point to add to the dimension string (Enter to finish)', want: 'point',
    point: async (p) => { await execute('drafting.insertDimensionPoint', { id, point: [p.x, p.y] }); return add(id); },
    enter: () => null,
  });
  const sel = useUi.getState().selected.filter(isString);
  if (sel.length === 1) return add(sel[0]);
  const pick = (): Step => ({
    prompt: 'Select the dimension string', want: 'select',
    point: (p) => { const id = cadHost()?.pickAny(p); if (!isString(id)) return pick(); select(id); return add(id); },
    enter: () => null,
  });
  return pick();
}
registerCommand({ names: ['DIMADD', 'DAP'], label: 'Add dimension points', start: dimAddCommand });

/**
 * DIMDEL: a dimension string (the one selected, else one clicked), then each
 * click takes out the string's point nearest it (the segments either side
 * become one); a string keeps two points. Enter or Esc to finish.
 */
function dimDelCommand(): Step {
  const del = (id: string): Step => ({
    prompt: 'Click near a point to take it out of the string (Enter to finish)', want: 'point',
    point: async (p) => {
      const n = useSession.getState().nodes.get(id);
      const pts = (Array.isArray(n?.props.pts) ? (n!.props.pts as unknown[]) : []).map(pt).filter((q): q is Vec2 => !!q);
      if (pts.length <= 2) { notify('A dimension keeps two points at least.'); return null; }
      let best = 0;
      pts.forEach((q, i) => { if (Math.hypot(q.x - p.x, q.y - p.y) < Math.hypot(pts[best].x - p.x, pts[best].y - p.y)) best = i; });
      await execute('drafting.removeDimensionPoint', { id, index: best });
      return del(id);
    },
    enter: () => null,
  });
  const sel = useUi.getState().selected.filter(isString);
  if (sel.length === 1) return del(sel[0]);
  const pick = (): Step => ({
    prompt: 'Select the dimension string', want: 'select',
    point: (p) => { const id = cadHost()?.pickAny(p); if (!isString(id)) return pick(); select(id); return del(id); },
    enter: () => null,
  });
  return pick();
}
registerCommand({ names: ['DIMDEL', 'DRP'], label: 'Remove dimension points', start: dimDelCommand });

/** TAG: click an element, then where its tag goes [MARK/NAME/CLASS/TYPE]. */
let tagField = 'mark';
function tagCommand(): Step {
  const pick = (): Step => ({
    prompt: `Click the element to tag [${tagField.toUpperCase()}: MARK/NAME/CLASS/TYPE]`, want: 'select',
    keywords: Object.fromEntries(['MARK', 'NAME', 'CLASS', 'TYPE'].map((k) => [k, () => { tagField = k.toLowerCase(); return pick(); }])),
    point: (p) => {
      const id = cadHost()?.pickAny(p);
      if (!id || id.includes('#')) return pick();
      return {
        prompt: 'Where the tag goes', want: 'point', base: p, preview: (c) => [{ type: 'line', a: p, b: c }],
        point: async (at) => { const h = cadHost(); if (h) await execute('drafting.addTag', { viewId: h.viewId, element: id, field: tagField, at: [at.x, at.y] }); return pick(); },
      };
    },
    enter: () => null,
  });
  return pick();
}
registerCommand({ names: ['TAG', 'TG'], label: 'Tag', start: tagCommand });
registerCommand({ names: ['DIMALIGNED', 'DAL', 'DIM'], label: 'Aligned dimension', start: () => dimCommand(false) });
registerCommand({ names: ['DIMLINEAR', 'DLI'], label: 'Linear dimension', start: () => dimCommand(true) });

// ─── The other dimensions: angle, radius, diameter, arc length, slope, level, ordinate ───

const pair = (p: Vec2) => [p.x, p.y];
async function addKind(kind: string, args: Record<string, unknown>): Promise<null> {
  const h = cadHost();
  if (!h) return null;
  await execute('drafting.addDimension', { viewId: h.viewId, dimKind: kind, ...args });
  return null;
}
/** DIMANGULAR: the vertex, a point on each side, then where the arc goes (the side it lies on picks the angle or its reflex). */
function angularCommand(): Step {
  return {
    prompt: 'The angle\'s vertex', want: 'point',
    point: (c) => ({
      prompt: 'A point on the first side', want: 'point', base: c, preview: (q) => [{ type: 'line', a: c, b: q }],
      point: (a) => ({
        prompt: 'A point on the second side', want: 'point', base: c, preview: (q) => [{ type: 'line', a: c, b: a }, { type: 'line', a: c, b: q }],
        point: (b) => ({
          prompt: 'Where the arc goes', want: 'point', base: c,
          preview: (q) => [{ type: 'line', a: c, b: a }, { type: 'line', a: c, b }, { type: 'circle', c, r: Math.max(1e-3, Math.hypot(q.x - c.x, q.y - c.y)) }],
          point: (at) => addKind('angular', { c: pair(c), a: pair(a), b: pair(b), at: pair(at) }),
        }),
      }),
    }),
  };
}
/** DIMRADIUS / DIMDIAMETER: a drafted circle or arc (it follows and drives it), or its centre and a point on it [Points]; then where the text goes. */
function roundCommand(kind: 'radial' | 'diameter'): Step {
  const place = (args: Record<string, unknown>, centre: Vec2): Step => ({
    prompt: 'Where the text goes', want: 'point', base: centre,
    preview: (q) => [{ type: 'line', a: centre, b: q }],
    point: (at) => addKind(kind, { ...args, at: pair(at) }),
  });
  return {
    prompt: `Pick a drafted circle or arc [Points]`, want: 'pick',
    pick: (hit) => (hit.shape.type === 'circle' || hit.shape.type === 'arc' ? place({ shape: hit.id }, hit.shape.c) : (cadLog('Pick a circle or an arc.'), roundCommand(kind))),
    keywords: {
      P: () => ({
        prompt: 'The centre', want: 'point',
        point: (c) => ({ prompt: 'A point on the circle', want: 'point', base: c, preview: (q) => [{ type: 'circle', c, r: Math.max(1e-3, Math.hypot(q.x - c.x, q.y - c.y)) }], point: (a) => place({ c: pair(c), a: pair(a) }, c) }),
      }),
    },
  };
}
/** DIMARC: a drafted arc, then where the dimension arc goes. */
function arcLengthCommand(): Step {
  return {
    prompt: 'Pick a drafted arc', want: 'pick',
    pick: (hit) => (hit.shape.type === 'arc'
      ? { prompt: 'Where the dimension goes', want: 'point', base: hit.shape.c, point: (at: Vec2) => addKind('arc', { shape: hit.id, at: pair(at) }) }
      : (cadLog('Pick an arc.'), arcLengthCommand())),
  };
}
/** DIMSLOPE: two points on the sloped line [Percent/Degrees/Ratio/Auto], then a point beside it. */
function slopeCommand(): Step {
  let format = 'auto';
  const kw = (next: () => Step) => ({ P: () => { format = '%'; return next(); }, D: () => { format = 'deg'; return next(); }, R: () => { format = 'ratio'; return next(); }, A: () => { format = 'auto'; return next(); } });
  const first = (): Step => ({
    prompt: `First point on the slope [Percent/Degrees/Ratio/Auto: ${format}]`, want: 'point', keywords: kw(() => first()),
    point: (a) => ({
      prompt: 'Second point on it', want: 'point', base: a, preview: (q) => [{ type: 'line', a, b: q }],
      point: (b) => addKind('slope', { a: pair(a), b: pair(b), at: pair({ x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 }), format }),
    }),
  });
  return first();
}
/** DIMLEVEL: the point, then the side the text goes; a typed value is the level itself (in a plan: else the storey's). */
function levelCommand(): Step {
  return {
    prompt: 'The point whose level is written', want: 'point',
    point: (a) => {
      let z: number | null = null;
      const side = (): Step => ({
        prompt: `Which side the text goes${z != null ? ` (level ${z} m)` : ' (type a level to write it)'}`, want: 'point', base: a,
        value: (v) => { z = v; return side(); },
        point: (at) => addKind('level', { a: pair(a), at: pair(at), ...(z != null ? { z } : {}) }),
      });
      return side();
    },
  };
}
/** DIMORDINATE: the point, then where its leader ends [X/Y: which coordinate; Origin: set it]. */
const ordinate = { origin: { x: 0, y: 0 } as Vec2 };
function ordinateCommand(): Step {
  let axis: 'x' | 'y' = 'x';
  const step = (): Step => ({
    prompt: `The point [X/Y: ${axis}; Origin at ${ordinate.origin.x}, ${ordinate.origin.y}]`, want: 'point',
    keywords: { X: () => { axis = 'x'; return step(); }, Y: () => { axis = 'y'; return step(); }, O: () => ({ prompt: 'The origin', want: 'point', point: (o) => { ordinate.origin = o; return step(); } }) },
    point: (a) => ({ prompt: 'Where the leader ends', want: 'point', base: a, preview: (q) => [{ type: 'line', a, b: q }], point: (at) => addKind('ordinate', { a: pair(a), at: pair(at), axis, o: pair(ordinate.origin) }) }),
  });
  return step();
}
registerCommand({ names: ['DIMANGULAR', 'DAN'], label: 'Angular dimension', start: angularCommand });
registerCommand({ names: ['DIMRADIUS', 'DRA'], label: 'Radius', start: () => roundCommand('radial') });
registerCommand({ names: ['DIMDIAMETER', 'DDI'], label: 'Diameter', start: () => roundCommand('diameter') });
registerCommand({ names: ['DIMARC', 'DAR'], label: 'Arc length', start: arcLengthCommand });
registerCommand({ names: ['DIMSLOPE', 'DSL'], label: 'Slope', start: slopeCommand });
registerCommand({ names: ['DIMLEVEL', 'DLV'], label: 'Level', start: levelCommand });
registerCommand({ names: ['DIMORDINATE', 'DOR'], label: 'Ordinate', start: ordinateCommand });

// ─── Linework (LW…): the view's generated lines hidden, shown or restyled, joined at corners ───

type LwMode = { action: 'hide' | 'show' | 'style'; style?: { pen?: number; color?: string; pattern?: string } };
const LW_STYLES: Record<string, LwMode> = {
  INVISIBLE: { action: 'hide' }, SHOW: { action: 'show' },
  THIN: { action: 'style', style: { pen: 1, pattern: 'solid' } }, MEDIUM: { action: 'style', style: { pen: 3, pattern: 'solid' } }, WIDE: { action: 'style', style: { pen: 5, pattern: 'solid' } },
  HIDDEN: { action: 'style', style: { pattern: 'hidden' } }, OVERHEAD: { action: 'style', style: { pattern: 'dashed' } },
  BEYOND: { action: 'style', style: { pen: 1, color: '#9ca3af' } }, CENTER: { action: 'style', style: { pattern: 'center' } }, DOTTED: { action: 'style', style: { pattern: 'dotted' } },
};
let lwMode = 'INVISIBLE';
let lwPen: number | null = null;

function lineworkStep(): Step {
  const mode = (): LwMode => (lwPen != null ? { action: 'style', style: { pen: lwPen } } : LW_STYLES[lwMode]);
  const step = (): Step => ({
    prompt: `Click the edges [${lwPen != null ? `pen ${lwPen}` : lwMode}: ${Object.keys(LW_STYLES).join('/')}; a number: a pen]`, want: 'select',
    keywords: Object.fromEntries(Object.keys(LW_STYLES).map((k) => [k, () => { lwMode = k; lwPen = null; return step(); }])),
    value: (n) => { if (n >= 1 && n <= 16) lwPen = Math.round(n); return step(); },
    point: async (p) => {
      const id = await frontView();
      if (id && id !== '3d') await execute('view.linework', { id, at: [p.x, p.y], ...mode() });
      return step();
    },
    enter: () => null,
  });
  return step();
}

function lineworkCorner(mode: 'join' | 'trim' | 'extend'): Step {
  const first = (): Step => ({
    prompt: mode === 'join' ? 'First edge' : mode === 'trim' ? 'The edge to cut at' : 'The edge to extend to', want: 'select',
    point: (a) => ({
      prompt: mode === 'join' ? 'Second edge' : mode === 'trim' ? 'The edge to cut (the side clicked goes)' : 'The edge to extend', want: 'select',
      point: async (b) => {
        const id = await frontView();
        if (id && id !== '3d') await execute('view.lineworkCorner', { id, first: [a.x, a.y], second: [b.x, b.y], mode });
        return first();
      },
    }),
    enter: () => null,
  });
  return first();
}

function lineworkReset(): Step {
  const step = (): Step => ({
    prompt: 'Click an edge to give it back its own lines [ALL: the whole view; ORPHANS: the edits that lost their edge]', want: 'select',
    keywords: {
      ALL: async () => { const id = await frontView(); if (id && id !== '3d') await execute('view.lineworkReset', { id, all: true }); return null; },
      ORPHANS: async () => { const id = await frontView(); if (id && id !== '3d') await execute('view.lineworkReset', { id, orphans: true }); return null; },
    },
    point: async (p) => { const id = await frontView(); if (id && id !== '3d') await execute('view.lineworkReset', { id, at: [p.x, p.y] }); return step(); },
    enter: () => null,
  });
  return step();
}

/** JOIN: lines and open polylines welded end to end into polylines (ends within 0.5 mm meet); a chain that comes back closes. */
registerCommand({ names: ['JOIN', 'J'], label: 'Join', start: () => withSelection(async (ids) => {
  const r = await execute('drafting.join', { ids }) as { joined?: number; pieces?: number } | null;
  cadLog(r?.joined ? `${r.joined} polyline${r.joined === 1 ? '' : 's'} from ${r.pieces} pieces.` : 'Nothing met end to end.');
  return null;
}) });

registerCommand({ names: ['LINEWORK', 'LW'], label: 'Linework', start: lineworkStep });
registerCommand({ names: ['LWJOIN', 'LWJ'], label: 'Linework join', start: () => lineworkCorner('join') });
registerCommand({ names: ['LWTRIM', 'LWTR'], label: 'Linework trim', start: () => lineworkCorner('trim') });
registerCommand({ names: ['LWEXTEND', 'LWEX'], label: 'Linework extend', start: () => lineworkCorner('extend') });
registerCommand({ names: ['LWRESET', 'LWR'], label: 'Linework reset', start: lineworkReset });

/** A CAD command started from the ribbon, as if typed. */
const cad = (id: string, label: string, icon: ToolDef['icon'], command: string): ToolDef => ({ id: `cad.${id}`, label, icon, run: () => void startCommand(command) });

/**
 * A drafted shape's grips, drawn in the view it is on: a line's ends and
 * middle; a polyline's vertices and edge middles (Ctrl/⌘ inserts or removes a
 * vertex); a circle's centre and quadrants; an arc's centre, ends and middle.
 */
function draftGrips(node: NodeDto): Grip[] {
  if (node.props.predefinedType !== 'DRAFT' || !node.props.shape) return [];
  const s = fromJson(node.props.shape as Record<string, unknown>);
  if (!s) return [];
  const view = typeof node.props.viewId === 'string' ? node.props.viewId : undefined;
  const save = (next: Shape): GripStep => ({ command: 'drafting.updateDraft', args: { id: node.id, shape: toJson(next) } });
  const on = (r: number, t: number): Vec2 => (s.type === 'arc' || s.type === 'circle' ? { x: s.c.x + Math.cos(t) * r, y: s.c.y + Math.sin(t) * r } : { x: 0, y: 0 });
  const grips: Grip[] = (() => {
    switch (s.type) {
      case 'line': return axisGrips(s.a, s.b, (a, b) => save({ ...s, a, b }));
      case 'polyline': {
        if (s.closed && s.pts.length >= 3) return outlineGrips({ pts: s.pts }, (o) => save({ ...s, pts: o.pts }));
        const out: Grip[] = s.pts.map((q, i) => ({
          key: `p${i}`, kind: 'vertex' as const, at: q,
          step: (p: Vec2) => save({ ...s, pts: s.pts.map((x, j) => (j === i ? p : x)) }),
          remove: () => (s.pts.length > 2 ? save({ ...s, pts: s.pts.filter((_, j) => j !== i) }) : null),
        }));
        for (let i = 0; i + 1 < s.pts.length; i++) {
          const a = s.pts[i], b = s.pts[i + 1], m = { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
          out.push({
            key: `e${i}`, kind: 'edgeMid', at: m,
            // The segment moved parallel to itself.
            step: (p: Vec2) => {
              const len = Math.hypot(b.x - a.x, b.y - a.y) || 1, n = { x: -(b.y - a.y) / len, y: (b.x - a.x) / len };
              const d = (p.x - m.x) * n.x + (p.y - m.y) * n.y;
              return save({ ...s, pts: s.pts.map((x, j) => (j === i || j === i + 1 ? { x: x.x + n.x * d, y: x.y + n.y * d } : x)) });
            },
            insert: () => save({ ...s, pts: [...s.pts.slice(0, i + 1), m, ...s.pts.slice(i + 1)] }),
          });
        }
        return out;
      }
      case 'circle': return [
        { key: 'c', kind: 'centre', at: s.c, step: (p: Vec2) => save({ ...s, c: p }) },
        ...keyPoints(s).quadrants.map((q, i) => ({ key: `q${i}`, kind: 'radius' as const, at: q, step: (p: Vec2) => save({ ...s, r: Math.max(1e-3, Math.hypot(p.x - s.c.x, p.y - s.c.y)) }) })),
      ];
      case 'arc': {
        const mid = s.start + (((s.end - s.start) % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI) / 2;
        const angle = (p: Vec2) => Math.atan2(p.y - s.c.y, p.x - s.c.x);
        return [
          { key: 'c', kind: 'centre', at: s.c, step: (p: Vec2) => save({ ...s, c: p }) },
          { key: 's', kind: 'end', at: on(s.r, s.start), step: (p: Vec2) => save({ ...s, start: angle(p) }) },
          { key: 'e', kind: 'end', at: on(s.r, s.end), step: (p: Vec2) => save({ ...s, end: angle(p) }) },
          { key: 'm', kind: 'radius', at: on(s.r, mid), step: (p: Vec2) => save({ ...s, r: Math.max(1e-3, Math.hypot(p.x - s.c.x, p.y - s.c.y)) }) },
        ];
      }
    }
  })();
  return grips.map((g) => ({ ...g, view }));
}

/**
 * A dimension string's grips, like a polyline's: a grip on each measured
 * point (dragged: the point goes there and holds on what it lands on; Ctrl/⌘:
 * taken out, two stay), one in each segment's middle on the dimension line
 * (dragged: a new point where it is dropped; Ctrl/⌘: one halfway), and the
 * dimension line's ends (dragged: the line goes through the point). They stand
 * where the view draws the points (on the elements they hold on), else where
 * they were placed.
 */
function dimensionGrips(node: NodeDto, view?: GripView): Grip[] {
  if (node.props.predefinedType !== 'DIMENSION' || node.props.dimKind) return [];
  const data = view?.mark?.data as { points?: unknown[]; line?: unknown[] } | undefined;
  const list = (raw: unknown): Vec2[] => (Array.isArray(raw) ? raw.map(pt).filter((p): p is Vec2 => !!p) : []);
  const stored = Array.isArray(node.props.pts) ? list(node.props.pts) : list([node.props.a, node.props.b]);
  const pts = data?.points && list(data.points).length === stored.length ? list(data.points) : stored;
  if (pts.length < 2) return [];
  const at = pt(node.props.at) ?? pts[0];
  const dir = pt(node.props.dir);
  const u = dir ? unit({ x: 0, y: 0 }, dir) : unit(pts[0], pts[pts.length - 1]);
  const n = { x: -u.y, y: u.x };
  const line = data?.line && list(data.line).length === pts.length ? list(data.line)
    : pts.map((p) => { const o = (at.x - p.x) * n.x + (at.y - p.y) * n.y; return { x: p.x + n.x * o, y: p.y + n.y * o }; });
  const id = node.id;
  const grips: Grip[] = pts.map((p, i) => ({
    key: `p${i}`, kind: 'vertex' as const, at: p, title: 'Drag: the point moves (and holds on what it lands on); Ctrl/⌘-click: take it out',
    step: (to: Vec2) => ({ command: 'drafting.moveDimensionPoint', args: { id, index: i, point: [to.x, to.y] } }),
    remove: () => (pts.length > 2 ? { command: 'drafting.removeDimensionPoint', args: { id, index: i } } : null),
  }));
  for (let i = 0; i + 1 < pts.length; i++) {
    const m = { x: (line[i].x + line[i + 1].x) / 2, y: (line[i].y + line[i + 1].y) / 2 };
    const half = { x: (pts[i].x + pts[i + 1].x) / 2, y: (pts[i].y + pts[i + 1].y) / 2 };
    grips.push({
      key: `e${i}`, kind: 'edgeMid', at: m, title: 'Drag: a new point where it is dropped (in its place along the string); Ctrl/⌘-click: one halfway',
      step: (to: Vec2) => ({ command: 'drafting.insertDimensionPoint', args: { id, point: [to.x, to.y] } }),
      insert: () => ({ command: 'drafting.insertDimensionPoint', args: { id, index: i + 1, point: [half.x, half.y] } }),
    });
  }
  // Each segment's value: dragged, it moves off its place (to stand clear of a neighbour's); Ctrl/⌘-click: back on its line.
  const texts = Array.isArray((data as { texts?: unknown[] } | undefined)?.texts) ? ((data as { texts: unknown[] }).texts) : [];
  texts.forEach((raw, i) => {
    const at = pt(raw);
    if (!at) return;
    grips.push({
      key: `t${i}`, kind: 'move', at, title: 'Drag: move the value · Ctrl/⌘-click: back on its line',
      step: (to: Vec2) => ({ command: 'drafting.moveDimensionText', args: { id, segment: i, by: [to.x - at.x, to.y - at.y] } }),
      remove: () => ({ command: 'drafting.moveDimensionText', args: { id, segment: i, reset: true } }),
    });
  });
  // The dimension line's ends: dragged, the line goes through the point.
  const t = line.map((q) => (q.x - line[0].x) * u.x + (q.y - line[0].y) * u.y);
  for (const [key, s] of [['l0', Math.min(...t)], ['l1', Math.max(...t)]] as const) {
    grips.push({
      key, kind: 'end', at: { x: line[0].x + u.x * s, y: line[0].y + u.y * s }, title: 'Drag: the dimension line goes through the point',
      step: (to: Vec2) => ({ command: 'drafting.moveDimension', args: { id, at: [to.x, to.y] } }),
    });
  }
  const viewId = typeof node.props.viewId === 'string' ? node.props.viewId : undefined;
  return grips.map((g) => ({ ...g, view: viewId }));
}

/** An annotation's grips: a drafted shape's, a dimension string's. */
const annotationGrips = (node: NodeDto, view?: GripView): Grip[] => [...draftGrips(node), ...dimensionGrips(node, view)];

/** A grid's axes: each axis's ends and middle (keys "<tag>:start"…), shown when that axis is selected (grid#tag). */
function gridGrips(node: NodeDto): Grip[] {
  const axes = (node.props.axes as { tag: string; a: number[]; b: number[] }[] | undefined) ?? [];
  return axes.flatMap((ax) => {
    const a = pt(ax.a), b = pt(ax.b);
    if (!a || !b) return [];
    return axisGrips(a, b, (s, e) => ({ command: 'drafting.updateAxis', args: { gridId: node.id, tag: ax.tag, x1: s.x, y1: s.y, x2: e.x, y2: e.y } })).map((g) => ({ ...g, key: `${ax.tag}:${g.key}` }));
  });
}
const gridRefs = (node: NodeDto): RefLine[] => ((node.props.axes as { tag: string; a: number[]; b: number[] }[] | undefined) ?? [])
  .flatMap((ax) => { const a = pt(ax.a), b = pt(ax.b); return a && b ? [{ ...axisRef(a, b), tag: ax.tag }] : []; });

const drafting: LapinModule = {
  id: 'drafting',
  name: 'Drafting',
  summary: 'CAD drawing on views, annotations, grid axes, levels, hatches and sheets.',
  tabs: [
    {
      id: 'draft', label: 'Draft', order: 10,
      groups: [
        { label: 'Draw', tools: [cad('line', 'Line', Slash, 'LINE'), cad('pline', 'Polyline', Spline, 'PLINE'), cad('rect', 'Rectangle', RectangleHorizontal, 'RECTANGLE'), cad('circle', 'Circle', Circle, 'CIRCLE'), cad('arc', 'Arc', Redo2, 'ARC')] },
        { label: 'Modify', tools: [cad('move', 'Move', Move, 'MOVE'), cad('copy', 'Copy', Copy, 'COPY'), cad('rotate', 'Rotate', RotateCw, 'ROTATE'), cad('mirror', 'Mirror', FlipHorizontal2, 'MIRROR')], small: [cad('scale', 'Scale', Scaling, 'SCALE'), cad('erase', 'Erase', Eraser, 'ERASE')] },
        { label: 'Edit', tools: [cad('offset', 'Offset', CopyPlus, 'OFFSET'), cad('trim', 'Trim', Scissors, 'TRIM'), cad('extend', 'Extend', ArrowRightToLine, 'EXTEND'), cad('fillet', 'Fillet', CornerUpRight, 'FILLET')], small: [cad('chamfer', 'Chamfer', CornerUpRight, 'CHAMFER'), cad('join', 'Join', Link2, 'JOIN')] },
        { label: 'Fill', tools: [cad('hatch', 'Hatch', Hash, 'HATCH')] },
        { label: 'Linework', tools: [cad('linework', 'Linework', PenLine, 'LINEWORK')], small: [cad('lwJoin', 'Join', CornerUpRight, 'LWJOIN'), cad('lwTrim', 'Trim', Scissors, 'LWTRIM'), cad('lwExtend', 'Extend', ArrowRightToLine, 'LWEXTEND'), cad('lwReset', 'Reset', Eraser, 'LWRESET')] },
        { label: 'Layers', tools: [{ id: 'drafting.layers', label: 'Layers', icon: Layers, run: () => useUi.setState({ dialog: 'draftLayers' }) }] },
        { label: 'Import', tools: [{ id: 'drafting.importDxf', label: 'Import DXF', icon: FileInput, run: () => void importDxf() }] },
      ],
    },
    {
      id: 'annotate', label: 'Annotate', order: 60,
      groups: [
        { label: 'Dimension', tools: [dimension, cad('dimLinear', 'Linear', MoveHorizontal, 'DIMLINEAR'), cad('dimAngular', 'Angle', AngleIcon, 'DIMANGULAR')],
          // Points clicked into a selected string, as ArchiCAD's Insert dimension point.
          small: [cad('dimAdd', 'Add points', ListPlus, 'DIMADD'), cad('dimDel', 'Remove points', ListMinus, 'DIMDEL'), cad('dimRadius', 'Radius', RadiusIcon, 'DIMRADIUS'), cad('dimDiameter', 'Diameter', DiameterIcon, 'DIMDIAMETER'), cad('dimArc', 'Arc length', ArcLengthIcon, 'DIMARC'), cad('dimSlope', 'Slope', SlopeIcon, 'DIMSLOPE'), cad('dimOrdinate', 'Ordinate', OrdinateIcon, 'DIMORDINATE')] },
        { label: 'Text', tools: [text, cad('tag', 'Tag', Tag, 'TAG'), {
          id: 'tagOpenings', label: 'Door & window tags', icon: Tags,
          run: () => void (async () => {
            // Every door and window of the plan's storey not tagged yet: a marker across its wall (Drafting standards → Tags).
            if (useUi.getState().view !== 'plan') { notify('Doors and windows are tagged on a plan: open one.'); return; }
            const viewId = await frontView();
            if (!viewId) return;
            const r = await execute('architecture.tagOpenings', { viewId }) as { tagged?: number } | null;
            if (r) notify(r.tagged ? `${r.tagged} doors and windows tagged.` : 'Every door and window here has its tag already.');
          })(),
        }] },
        { label: 'Datum', tools: [
          axis,
          { id: 'level', label: 'Level', icon: LevelIcon, shortcut: 'LL', run: () => void startCommand('DIMLEVEL') },
        ] },
        { label: 'Sheets', tools: [{ id: 'sheet', label: 'Sheet', icon: FileText }] },
      ],
    },
  ],
  hotbar: [dimension],
  dialogs: { draftLayers: LayersDialog, importDxf: ImportDxfDialog },
  propertySections: [
    // The drafted kinds show their own fields (a line its ends, a circle its radius), so the class's generic ones stay out.
    { id: 'draft', label: 'Drafting', classes: ['IfcAnnotation'], component: DraftSection, replacesFields: draftsFields },
    { id: 'dimension', label: 'Dimension', classes: ['IfcAnnotation'], component: DimensionSection },
  ],
  grips: { IfcAnnotation: annotationGrips, IfcGrid: gridGrips },
  references: { IfcGrid: gridRefs },
};

export default drafting;
