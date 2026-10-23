/** Drafting: dimensions that follow the elements, texts, grid axes; levels, hatches and sheets to come. */

import { FileText, Grid3x3, Hash, MoveHorizontal, Type, TrendingUp } from 'lucide-react';
import type { LapinModule, ToolDef } from '../../platform/module';

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

const drafting: LapinModule = {
  id: 'drafting',
  name: 'Drafting',
  summary: 'Annotations, grid axes, levels, hatches and sheets.',
  tabs: [{
    id: 'annotate', label: 'Annotate', order: 30,
    groups: [
      { label: 'Dimension', tools: [dimension] },
      { label: 'Text', tools: [text] },
      { label: 'Datum', tools: [
        axis,
        { id: 'level', label: 'Level', icon: TrendingUp, shortcut: 'LL' },
      ] },
      { label: 'Detail', tools: [{ id: 'hatch', label: 'Hatch', icon: Hash }] },
      { label: 'Sheets', tools: [{ id: 'sheet', label: 'Sheet', icon: FileText }] },
    ],
  }],
  hotbar: [dimension],
};

export default drafting;
