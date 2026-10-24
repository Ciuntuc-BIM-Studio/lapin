/** Drafting: CAD drawing on views (lines, polylines, circles, arcs, hatches on layers, and their edits), dimensions that follow the elements, texts, grid axes. */

import {
  ArrowRightToLine, Circle, Copy, CopyPlus, CornerUpRight, Eraser, FileText, FlipHorizontal2, Grid3x3, Hash, Layers, Move, MoveHorizontal,
  RectangleHorizontal, RotateCw, Scaling, Scissors, Slash, Spline, Type, TrendingUp, Redo2, PenLine, Tag,
} from 'lucide-react';
import type { Grip, LapinModule, ToolDef, Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { useUi } from '../../app/store';
import { execute } from '../../engine/session';
import { cadHost, registerCommand, startCommand, type Step } from '../../viewport/cad/session';
import { DimensionSection } from './DimensionSection';
import { fromJson, keyPoints, toJson, type Shape } from '../../viewport/cad/shapes';
import { LayersDialog } from './LayersDialog';
import { frontView } from '../../viewport/frontView';

/** An aligned dimension: its two points, then where its line goes; then on as a string. Ends on an element's cut edge, a grid axis or a wall's centre line follow it. */
const dimension: ToolDef = { id: 'drafting.dimension', label: 'Aligned', icon: MoveHorizontal, shortcut: 'DI', run: () => void startCommand('DIMALIGNED') };
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
  id: 'drafting.axis', label: 'Grid axis', icon: Grid3x3, shortcut: 'GR',
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

/** A drafted shape's grips: its ends and corners, a circle's centre and radius; dragging one changes the shape. */
function draftGrips(node: NodeDto): Grip[] {
  if (node.props.predefinedType !== 'DRAFT' || !node.props.shape) return [];
  const s = fromJson(node.props.shape as Record<string, unknown>);
  if (!s) return [];
  const save = (next: Shape) => execute('drafting.updateDraft', { id: node.id, shape: toJson(next) });
  switch (s.type) {
    case 'line': return [
      { key: 'a', at: s.a, move: (p: Vec2) => save({ ...s, a: p }) },
      { key: 'b', at: s.b, move: (p: Vec2) => save({ ...s, b: p }) },
    ];
    case 'polyline': return s.pts.map((q, i) => ({ key: `p${i}`, at: q, move: (p: Vec2) => save({ ...s, pts: s.pts.map((x, j) => (j === i ? p : x)) }) }));
    case 'circle': return [
      { key: 'c', at: s.c, move: (p: Vec2) => save({ ...s, c: p }) },
      { key: 'r', at: keyPoints(s).quadrants[0], move: (p: Vec2) => save({ ...s, r: Math.max(1e-3, Math.hypot(p.x - s.c.x, p.y - s.c.y)) }) },
    ];
    case 'arc': return [{ key: 'c', at: s.c, move: (p: Vec2) => save({ ...s, c: p }) }];
  }
}

const drafting: LapinModule = {
  id: 'drafting',
  name: 'Drafting',
  summary: 'CAD drawing on views, annotations, grid axes, levels, hatches and sheets.',
  tabs: [
    {
      id: 'draft', label: 'Draft', order: 25,
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
      id: 'annotate', label: 'Annotate', order: 30,
      groups: [
        { label: 'Dimension', tools: [dimension, cad('dimLinear', 'Linear', MoveHorizontal, 'DIMLINEAR')] },
        { label: 'Text', tools: [text, cad('tag', 'Tag', Tag, 'TAG')] },
        { label: 'Datum', tools: [
          axis,
          { id: 'level', label: 'Level', icon: TrendingUp, shortcut: 'LL' },
        ] },
        { label: 'Sheets', tools: [{ id: 'sheet', label: 'Sheet', icon: FileText }] },
      ],
    },
  ],
  hotbar: [dimension],
  dialogs: { draftLayers: LayersDialog },
  propertySections: [{ id: 'dimension', label: 'Dimension', classes: ['IfcAnnotation'], component: DimensionSection }],
  grips: { IfcAnnotation: draftGrips },
};

export default drafting;
