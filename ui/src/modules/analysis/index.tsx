/** Structural analysis, stage S0 (docs/plan-fem.md §8): load cases, loads on a plan, the combinations, the IFC Structural Analysis View. */

import { ArrowDown, ArrowDownToLine, ListChecks, Weight } from 'lucide-react';
import type { LapinModule, ToolDef } from '../../platform/module';
import { notify, useUi } from '../../app/store';
import { lazyPart } from '../../platform/lazyPart';
import { areaLoadArgs, lineLoadArgs, pointLoadArgs } from './loads';
import { currentLoad, LoadOption } from './prefs';
import { OccupancySection } from './OccupancySection';

// Fetched the first time it is opened (platform/lazyPart.tsx).
const LoadsDialog = lazyPart(() => import('./LoadsDialog').then((m) => m.LoadsDialog), { title: 'Loads' });

const AreaOption = () => <LoadOption kind="area" />;
const LineOption = () => <LoadOption kind="line" />;
const PointOption = () => <LoadOption kind="point" />;

/** The value typed in the bar and the case chosen; a message and null when the value does not read. */
function chosen(kind: 'area' | 'line' | 'point') {
  const l = currentLoad(kind);
  if (l.value == null) { notify('Type the load\'s value (more than 0) in the bar first.'); return null; }
  return { value: l.value, caseId: l.caseId };
}

const cases: ToolDef = { id: 'analysis.cases', label: 'Load cases…', icon: ListChecks, run: () => useUi.setState({ dialog: 'analysisLoads' }) };
const area: ToolDef = {
  id: 'analysis.areaLoad', label: 'Area load', icon: ArrowDownToLine, options: AreaOption,
  gesture: { kind: 'polygon' },
  complete: async ({ points }, ctx) => { const c = chosen('area'); if (c) await ctx.execute('analysis.addAreaLoad', areaLoadArgs(ctx.storeyId, points, c.value, c.caseId)); },
};
const line: ToolDef = {
  id: 'analysis.lineLoad', label: 'Line load', icon: Weight, options: LineOption,
  gesture: { kind: 'twoPoints', repeat: true },
  complete: async ({ points: [a, b] }, ctx) => { const c = chosen('line'); if (c) await ctx.execute('analysis.addLineLoad', lineLoadArgs(ctx.storeyId, a, b, c.value, c.caseId)); },
};
const point: ToolDef = {
  id: 'analysis.pointLoad', label: 'Point load', icon: ArrowDown, options: PointOption,
  gesture: { kind: 'point' },
  complete: async ({ points: [p] }, ctx) => { const c = chosen('point'); if (c) await ctx.execute('analysis.addPointLoad', pointLoadArgs(ctx.storeyId, p, c.value, c.caseId)); },
};

const analysis: LapinModule = {
  id: 'analysis',
  name: 'Structural analysis',
  summary: 'Load cases, loads, EN 1990 combinations and the IFC Structural Analysis View.',
  tabs: [{
    // The Structure tab's group.
    id: 'structure', label: 'Structure', order: 30, include: ['modify'],
    groups: [{ label: 'Loads', tools: [cases, area, line, point] }],
  }],
  dialogs: { analysisLoads: LoadsDialog },
  propertySections: [{ id: 'occupancy', label: 'Imposed load', classes: ['IfcSpace'], component: OccupancySection }],
};

export default analysis;
