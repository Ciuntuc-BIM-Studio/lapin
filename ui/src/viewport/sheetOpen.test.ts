import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { openPlaced } from './sheetOpen';

const node = (id: string, type: string, props: Record<string, unknown>): NodeDto =>
  ({ id, type, name: id, parentId: null, px: 0, py: 0, props, editable: true, module: null, storeyId: null }) as unknown as NodeDto;

describe('opening a view placed on a sheet', () => {
  it('opens a 3D view in the 3D pane with its own camera, not the drawing pane (forum topic 91)', () => {
    expect(openPlaced('v3', node('v3', 'LapinView', { kind: '3d' }))).toEqual({ view: '3d', view3dId: 'v3', selection: 'v3', selected: ['v3'] });
  });
  it('opens plans, schedules and drawings where they live', () => {
    expect(openPlaced('plan:s1', undefined)).toEqual({ view: 'plan', storeyId: 's1', planViewId: null });
    expect(openPlaced('p', node('p', 'LapinView', { kind: 'plan', storeyId: 's2' }))).toEqual({ view: 'plan', planViewId: 'p', storeyId: 's2' });
    expect(openPlaced('t', node('t', 'LapinSchedule', {}))).toEqual({ view: 'schedule', scheduleId: 't' });
    expect(openPlaced('e', node('e', 'LapinView', { kind: 'elevation' }))).toEqual({ view: 'drawing', drawingViewId: 'e' });
    expect(openPlaced('gone', undefined)).toBeNull();
  });
});
