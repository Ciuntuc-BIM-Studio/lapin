/** Drafting: CAD drawing on views (lines, polylines, circles, arcs, hatches on layers, and their edits), dimensions that follow the elements, texts, grid axes. */

import { ArrowRightToLine, Circle, Copy, CopyPlus, CornerUpRight, Eraser, FileText, FlipHorizontal2, Hash, Layers, Move, MoveHorizontal, RectangleHorizontal, RotateCw, Scaling, Scissors, Slash, Spline, Type, Redo2, PenLine, Tag } from 'lucide-react';
import { DimensionIcon, GridAxisIcon, LevelIcon } from '../../icons';
import type { Grip, GripStep, LapinModule, RefLine, ToolDef, Vec2 } from '../../platform/module';
import { axisGrips, axisRef, outlineGrips, pt } from '../../viewport/gripKit';
import type { NodeDto } from '../../engine/protocol';
import { useUi } from '../../app/store';
import { execute } from '../../engine/session';
import { cadHost, registerCommand, startCommand, type Step } from '../../viewport/cad/session';
import { DimensionSection } from './DimensionSection';
import { DraftSection } from './DraftSection';
import { fromJson, keyPoints, toJson, type Shape } from '../../viewport/cad/shapes';
import { LayersDialog } from './LayersDialog';
import { frontView } from '../../viewport/frontView';

/** An aligned dimension: its two points, then where its line goes; then on as a string. Ends on an element's cut edge, a grid axis or a wall's centre line follow it. */
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
 * point measured from the last on the same line (Enter to finish; S first for
 * a single one). Ends on elements, grid axes or wall centre lines follow them.
 */
function dimCommand(linear: boolean): Step {
  let single = false;
  const V = (p: Vec2) => [p.x, p.y];
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
        preview: (c) => [{ type: 'line', a, b }, { type: 'line', a: c, b: { x: c.x + b.x - a.x, y: c.y + b.y - a.y } }],
        point: async (at) => {
          const h = cadHost(); if (!h) return null;
          const dir = dirOf(a, b, at);
          const r = await execute('drafting.addDimension', { viewId: h.viewId, a: V(a), b: V(b), at: V(at), ...(dir ? { dir: V(dir) } : {}) }) as { id?: string } | null;
          if (!r?.id || single) return null;
          const next = (prev: string, from: Vec2): Step => ({
            prompt: 'Next point of the string (Enter to finish)', want: 'point', base: from,
            preview: (c) => [{ type: 'line', a: from, b: c }],
            point: async (p) => {
              const hh = cadHost(); if (!hh) return null;
              const rr = await execute('drafting.addDimension', { viewId: hh.viewId, a: V(from), b: V(p), at: V(at), after: prev, ...(dir ? { dir: V(dir) } : {}) }) as { id?: string } | null;
              return rr?.id ? next(rr.id, p) : null;
            },
            enter: () => null,
          });
          return next(r.id, b);
        },
      }),
    }),
  });
  return first();
}
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
        { label: 'Edit', tools: [cad('offset', 'Offset', CopyPlus, 'OFFSET'), cad('trim', 'Trim', Scissors, 'TRIM'), cad('extend', 'Extend', ArrowRightToLine, 'EXTEND'), cad('fillet', 'Fillet', CornerUpRight, 'FILLET')], small: [cad('chamfer', 'Chamfer', CornerUpRight, 'CHAMFER')] },
        { label: 'Fill', tools: [cad('hatch', 'Hatch', Hash, 'HATCH')] },
        { label: 'Linework', tools: [cad('linework', 'Linework', PenLine, 'LINEWORK')], small: [cad('lwJoin', 'Join', CornerUpRight, 'LWJOIN'), cad('lwTrim', 'Trim', Scissors, 'LWTRIM'), cad('lwExtend', 'Extend', ArrowRightToLine, 'LWEXTEND'), cad('lwReset', 'Reset', Eraser, 'LWRESET')] },
        { label: 'Layers', tools: [{ id: 'drafting.layers', label: 'Layers', icon: Layers, run: () => useUi.setState({ dialog: 'draftLayers' }) }] },
      ],
    },
    {
      id: 'annotate', label: 'Annotate', order: 60,
      groups: [
        { label: 'Dimension', tools: [dimension, cad('dimLinear', 'Linear', MoveHorizontal, 'DIMLINEAR')] },
        { label: 'Text', tools: [text, cad('tag', 'Tag', Tag, 'TAG')] },
        { label: 'Datum', tools: [
          axis,
          { id: 'level', label: 'Level', icon: LevelIcon, shortcut: 'LL' },
        ] },
        { label: 'Sheets', tools: [{ id: 'sheet', label: 'Sheet', icon: FileText }] },
      ],
    },
  ],
  hotbar: [dimension],
  dialogs: { draftLayers: LayersDialog },
  propertySections: [
    // The drafted kinds show their own fields (a line its ends, a circle its radius), so the class's generic ones stay out.
    { id: 'draft', label: 'Drafting', classes: ['IfcAnnotation'], component: DraftSection, replacesFields: draftsFields },
    { id: 'dimension', label: 'Dimension', classes: ['IfcAnnotation'], component: DimensionSection },
  ],
  grips: { IfcAnnotation: draftGrips, IfcGrid: gridGrips },
  references: { IfcGrid: gridRefs },
};

export default drafting;
