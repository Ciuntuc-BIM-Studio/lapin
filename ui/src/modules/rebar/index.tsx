/**
 * Rebar 2D (docs/plan-rebar.md): reinforcement drawn on views as parametric bar shapes bent at the Eurocode radius,
 * stirrups distributed along a path with zones, marks and labels, the bar bending schedule; a section across a beam
 * or a column at a click, inset beside it in the same view, with the context round it.
 */

import { Crosshair, Minus, PictureInPicture2, Scissors, Settings2, Table2 } from 'lucide-react';
import { SectionIcon } from '../../icons';
import type { Grip, GripView, LapinModule, ToolDef, Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { axisGrips, pt } from '../../viewport/gripKit';
import { notify, useUi } from '../../app/store';
import { execute } from '../../engine/session';
import { RebarRunSection, RebarSection } from './RebarSection';
import { InsetPickDialog, RebarSettingsDialog } from './RebarDialogs';
import { BarDotIcon, LBarIcon, PolylineBarIcon, StirrupIcon, StirrupsIcon, TieIcon, UBarIcon } from './icons';
import { structureHooks } from '../structure/hooks';
import { StirrupsFields } from './StirrupsFields';

// The stirrups of a beam's or a column's type are set in the Structure types dialog (decision 215).
structureHooks.typeExtras.push(StirrupsFields);

/** What a section can be cut across: anything with an axis, or standing at a point. */
export const ACROSS = ['IfcBeam', 'IfcColumn', 'IfcWall', 'IfcFooting', 'IfcMember', 'IfcPile'];

async function open(r: unknown) {
  const id = (r as { id?: string; viewId?: string } | null)?.viewId ?? (r as { id?: string } | null)?.id;
  if (id) useUi.setState({ view: 'drawing', drawingViewId: id, tool: null });
}

/** A bar placed with a click, of a shape of the catalogue. */
const bar = (shape: string, label: string, icon: ToolDef['icon'], shortcut?: string): ToolDef => ({
  id: `rebar.${shape}`, label, icon, shortcut,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { await ctx.execute('rebar.add', { viewId: ctx.viewId, shape, at: [p.x, p.y] }); },
});
const straight = bar('straight', 'Straight bar', Minus, 'RB');
const lBar = bar('L', 'L bar', LBarIcon);
const uBar = bar('U', 'U bar', UBarIcon);
const stirrup = bar('stirrup', 'Stirrup', StirrupIcon, 'RS');
const tie = bar('tie', 'Tie', TieIcon);
const dot = bar('dot', 'Bar in section', BarDotIcon, 'RD');
/** A bar bent as drawn: its vertices, the first one its point. */
const polylineBar: ToolDef = {
  id: 'rebar.polyline', label: 'Polyline bar', icon: PolylineBarIcon,
  gesture: { kind: 'polyline' },
  complete: async ({ points }, ctx) => {
    if (points.length < 2) return;
    const [o] = points;
    await ctx.execute('rebar.add', { viewId: ctx.viewId, shape: 'polyline', at: [o.x, o.y], vertices: points.map((p) => [p.x - o.x, p.y - o.y]) });
  },
};
/** Stirrups (or any bar) along a path: its two ends. */
const run: ToolDef = {
  id: 'rebar.run', label: 'Stirrups along', icon: StirrupsIcon, shortcut: 'RR',
  gesture: { kind: 'twoPoints' },
  complete: async ({ points: [a, b] }, ctx) => { await ctx.execute('rebar.addRun', { viewId: ctx.viewId, start: [a.x, a.y], end: [b.x, b.y] }); },
};
/** A section across the element clicked, where it was clicked, inset beside the click in this view. */
const sectionHere: ToolDef = {
  id: 'rebar.sectionHere', label: 'Section here', icon: Scissors, shortcut: 'SH',
  gesture: { kind: 'pick', types: ACROSS },
  complete: async ({ points: [p], entity }, ctx) => {
    if (!entity) return;
    const r = await ctx.execute('rebar.sectionHere', { viewId: ctx.viewId, id: entity.id, point: [p.x, p.y] }) as { insetId?: string } | null;
    if (r?.insetId) useUi.setState({ selected: [r.insetId] });
  },
};
/** A section across the element clicked, opened on its own. */
const sectionAcross: ToolDef = {
  id: 'rebar.sectionAcross', label: 'Section across', icon: SectionIcon, shortcut: 'SX',
  gesture: { kind: 'pick', types: ACROSS },
  complete: async ({ points: [p], entity }, ctx) => {
    if (!entity) return;
    await open(await ctx.execute('rebar.sectionHere', { viewId: ctx.viewId, id: entity.id, point: [p.x, p.y], inset: false }));
  },
};
/** Another view drawn in this one: click where it goes, then choose it. */
const insetView: ToolDef = {
  id: 'rebar.inset', label: 'Inset view', icon: PictureInPicture2, shortcut: 'IV',
  gesture: { kind: 'point' },
  complete: ({ points: [p] }, ctx) => { useUi.setState({ dialog: 'insetPick', insetAt: { viewId: ctx.viewId, x: p.x, y: p.y } }); },
};
const schedule: ToolDef = {
  id: 'rebar.schedule', label: 'Bar schedule', icon: Table2,
  run: () => void execute('architecture.createSchedule', { kind: 'rebar' }).then((r) => { if (r) notify('Bar bending schedule made: it is in the project browser, under Schedules.'); }),
};
const settings: ToolDef = { id: 'rebar.settings', label: 'Rebar settings', icon: Settings2, run: () => useUi.setState({ dialog: 'rebarSettings' }) };
/** The selected bar seen in section (a dot), placed with a click; it stays out of the schedule. */
const asSection: ToolDef = {
  id: 'rebar.asSection', label: 'Same bar in section', icon: Crosshair,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => {
    const id = useUi.getState().selected[0];
    if (!id) { notify('Select the bar to show in section first.'); return; }
    await ctx.execute('rebar.duplicateAs', { id, view: 'section', at: [p.x, p.y], viewId: ctx.viewId });
  },
};

/** How a mark seen through an inset view maps between its source's coordinates and the host's (`via` in the mark's data). */
interface Via { inset: string; host: string; source: string; at: number[]; centre: number[]; factor: number }
function mapping(view?: GripView): { show: (p: Vec2) => Vec2; back: (p: Vec2) => Vec2; host?: string } {
  const via = (view?.mark?.data as { via?: Via } | undefined)?.via;
  if (!via) return { show: (p) => p, back: (p) => p };
  return {
    show: (p) => ({ x: via.at[0] + (p.x - via.centre[0]) * via.factor, y: via.at[1] + (p.y - via.centre[1]) * via.factor }),
    back: (p) => ({ x: via.centre[0] + (p.x - via.at[0]) / via.factor, y: via.centre[1] + (p.y - via.at[1]) / via.factor }),
    host: via.host,
  };
}

/** A bar's grips as the engine drew them: its shape's (each drives a parameter), its point, its label; through an inset, where the inset shows them. */
function barGrips(node: NodeDto, view?: GripView): Grip[] {
  const data = view?.mark?.data as { grips?: { key: string; at: number[] }[]; at?: number[]; label?: number[] } | undefined;
  const { show, back, host } = mapping(view);
  const viewId = host ?? (typeof node.props.viewId === 'string' ? node.props.viewId : undefined);
  const step = (key: string) => (to: Vec2) => { const p = back(to); return { command: 'rebar.grip', args: { id: node.id, key, point: [p.x, p.y] } }; };
  const grips: Grip[] = [];
  const at = pt(data?.at) ?? pt(node.props.at);
  if (at) grips.push({ key: 'at', kind: 'move', at: show(at), title: 'Drag: move the bar', step: step('at') });
  for (const g of data?.grips ?? []) {
    const p = pt(g.at);
    if (p) grips.push({ key: g.key, kind: g.key.startsWith('v') ? 'vertex' : 'end', at: show(p), title: 'Drag: the bar follows', step: step(g.key) });
  }
  const label = pt(data?.label);
  if (label) grips.push({ key: 'label', kind: 'move', at: show(label), title: 'Drag: move the mark', step: step('label') });
  return grips.map((g) => ({ ...g, view: viewId }));
}

/** A distribution's path ends and its label. */
function runGrips(node: NodeDto, view?: GripView): Grip[] {
  const a = pt(node.props.start), b = pt(node.props.end);
  if (!a || !b) return [];
  const { show, back, host } = mapping(view);
  const viewId = host ?? (typeof node.props.viewId === 'string' ? node.props.viewId : undefined);
  const grips = axisGrips(show(a), show(b), (s, e) => { const p = back(s), q = back(e); return { command: 'rebar.updateRun', args: { id: node.id, start: [p.x, p.y], end: [q.x, q.y] } }; });
  const label = pt((view?.mark?.data as { label?: number[] } | undefined)?.label);
  if (label) grips.push({ key: 'label', kind: 'move', at: show(label), title: 'Drag: move the mark', step: (to: Vec2) => { const p = back(to); return { command: 'rebar.updateRun', args: { id: node.id, label: { at: [p.x, p.y] } } }; } });
  return grips.map((g) => ({ ...g, view: viewId }));
}

/** An inset view's point. */
function insetGrips(node: NodeDto): Grip[] {
  const at = pt(node.props.at);
  if (!at) return [];
  const viewId = typeof node.props.viewId === 'string' ? node.props.viewId : undefined;
  return [{ key: 'at', kind: 'move', at, view: viewId, title: 'Drag: move the inset', step: (to: Vec2) => ({ command: 'view.updateInset', args: { id: node.id, at: [to.x, to.y] } }) }];
}

export const rebarModule: LapinModule = {
  id: 'rebar',
  name: 'Rebar 2D',
  summary: 'Reinforcement drawn on views: bars bent at the Eurocode radius, stirrups along a path, marks and the bar bending schedule; sections across an element inset where you work.',
  tabs: [{
    id: 'rebar', label: 'Rebar 2D', order: 35, include: ['modify'],
    groups: [
      { label: 'Sections', tools: [sectionHere, sectionAcross, insetView] },
      { label: 'Bars', tools: [straight, lBar, uBar, stirrup, polylineBar], small: [tie, dot, asSection] },
      { label: 'Distribution', tools: [run] },
      { label: 'Schedule', tools: [schedule, settings] },
    ],
  }],
  hotbar: [straight, stirrup, run, sectionHere],
  grips: { LapinRebar: barGrips, LapinRebarRun: runGrips, LapinInset: insetGrips },
  propertySections: [
    { id: 'rebar.bar', label: 'Bar', classes: ['LapinRebar'], component: RebarSection },
    { id: 'rebar.run', label: 'Distribution', classes: ['LapinRebarRun'], component: RebarRunSection },
  ],
  dialogs: { rebarSettings: RebarSettingsDialog, insetPick: InsetPickDialog },
};

export default rebarModule;
