/**
 * Getting quantities (decision 287, the tester's report #75: the Quantities tab's buttons did nothing): every ribbon
 * button of every module does something when pressed, and the takeoff's groupings are the engine's dimensions.
 */

import { describe, expect, it } from 'vitest';
import { modules } from '../../platform/modules';
import cost from './index';
import { TAKEOFF_GROUPINGS, groupingOf } from './schedules';

describe('the Cost tab (Quantities until stage 6 of docs/plan-4d5d.md)', () => {
  it('has no button that does nothing: each tool draws (a gesture) or runs', () => {
    const dead = modules.flatMap((m) => (m.tabs ?? []).flatMap((t) => t.groups.flatMap((g) => [...g.tools, ...(g.small ?? [])])))
      .filter((t) => !t.gesture && !t.run).map((t) => t.id);
    expect(dead).toEqual([]);
  });

  it('makes the quantity takeoff, lists every schedule, exports the workbook, and prices the model', () => {
    expect(cost.tabs![0].label).toBe('Cost');
    const tools = cost.tabs![0].groups.flatMap((g) => [...g.tools, ...(g.small ?? [])]).map((t) => t.id);
    expect(tools).toEqual(['cost.takeoff', 'cost.schedules', 'cost.xlsx', 'cost.prices', 'cost.map', 'cost.estimate', 'cost.overTime', 'cost.formSheet', 'cost.estimateXlsx', 'cost.estimatePdf']);
    expect(cost.dialogs?.schedules).toBeTruthy();
    expect(cost.dialogs?.['cost.prices']).toBeTruthy();
    expect(cost.dialogs?.['cost.map']).toBeTruthy();
    expect(cost.views?.estimate).toBeTruthy();
    expect(cost.views?.costTime).toBeTruthy();
  });
});

describe('the takeoff groupings', () => {
  it('group by the engine\'s dimensions, kind and type by default', () => {
    for (const g of TAKEOFF_GROUPINGS) {
      expect(g.groupBy.every((d) => ['kind', 'type', 'storey', 'material'].includes(d))).toBe(true);
      expect(g.id).toBe(g.groupBy.join(','));
    }
    expect(groupingOf(undefined)).toBe('kind,type');
    expect(groupingOf(['storey', 'material'])).toBe('storey,material');
    expect(groupingOf(['material', 'storey'])).toBe('kind,type');
  });
});
