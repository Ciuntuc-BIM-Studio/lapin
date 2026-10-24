/** Drafting: CAD drawing on views (lines, polylines, circles, arcs, hatches on layers, and their edits), dimensions that follow the elements, texts, grid axes. */

import {
  ArrowRightToLine, Circle, Copy, CopyPlus, CornerUpRight, Eraser, FileText, FlipHorizontal2, Grid3x3, Hash, Layers, Move, MoveHorizontal,
  RectangleHorizontal, RotateCw, Scaling, Scissors, Slash, Spline, Type, TrendingUp, Redo2,
} from 'lucide-react';
import type { Grip, LapinModule, ToolDef, Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { useUi } from '../../app/store';
import { execute } from '../../engine/session';
import { startCommand } from '../../viewport/cad/session';
import { fromJson, keyPoints, toJson, type Shape } from '../../viewport/cad/shapes';
import { LayersDialog } from './LayersDialog';

/** An aligned dimension: its two points, then where its line goes. Ends on an element's cut edge follow it. */
const dimension: ToolDef = {
  id: 'drafting.dimension', label: 'Aligned', icon: MoveHorizontal, shortcut: 'DI',
  gesture: { kind: 'threePoints' },
  complete: async ({ points: [a, b, at] }, ctx) => {
    await ctx.execute('drafting.addDimension', { viewId: ctx.viewId, a: [a.x, a.y], b: [b.x, b.y], at: [at.x, at.y] });
  },
};
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
        { label: 'Layers', tools: [{ id: 'drafting.layers', label: 'Layers', icon: Layers, run: () => useUi.setState({ dialog: 'draftLayers' }) }] },
      ],
    },
    {
      id: 'annotate', label: 'Annotate', order: 30,
      groups: [
        { label: 'Dimension', tools: [dimension] },
        { label: 'Text', tools: [text] },
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
  grips: { IfcAnnotation: draftGrips },
};

export default drafting;
