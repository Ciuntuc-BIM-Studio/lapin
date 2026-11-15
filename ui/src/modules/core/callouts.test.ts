import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { boundaryArgs, calloutGrips, calloutTool, calloutCircleTool, calloutPolygonTool, referableViews } from './callouts';
import { detailsOf } from '../../engine/dependents';

const node = (id: string, type: string, props: Record<string, unknown> = {}, name = id) => ({ id, type, name, parentId: null, props }) as unknown as NodeDto;
const close = (a: number[] | undefined, b: number[]) => { expect(a).toHaveLength(b.length); a!.forEach((x, i) => expect(x).toBeCloseTo(b[i], 9)); };

describe('the Detail callout tool (forum topic 107)', () => {
  it('draws a rectangle by default (CO), a circle or a polygon from its bar', () => {
    expect(calloutTool.shortcut).toBe('CO');
    expect(calloutTool.gesture).toEqual({ kind: 'rectangle' });
    expect(calloutCircleTool.gesture).toEqual({ kind: 'twoPoints' });
    expect(calloutPolygonTool.gesture).toEqual({ kind: 'polygon' });
  });

  it('gives the engine the boundary as drawn: corners sorted, a circle by its centre and radius, a polygon by its corners', () => {
    expect(boundaryArgs('rect', [{ x: 3, y: 1 }, { x: 1, y: 2 }])).toEqual({ shape: 'rect', min: [1, 1], max: [3, 2] });
    expect(boundaryArgs('circle', [{ x: 1, y: 1 }, { x: 4, y: 5 }])).toEqual({ shape: 'circle', centre: [1, 1], radius: 5 });
    expect(boundaryArgs('polygon', [{ x: 0, y: 0 }, { x: 2, y: 0 }, { x: 1, y: 1 }])).toEqual({ shape: 'polygon', pts: [[0, 0], [2, 0], [1, 1]] });
    // Not enough to make one.
    expect(boundaryArgs('rect', [{ x: 0, y: 0 }])).toBeNull();
    expect(boundaryArgs('circle', [{ x: 0, y: 0 }, { x: 0, y: 0 }])).toBeNull();
    expect(boundaryArgs('polygon', [{ x: 0, y: 0 }, { x: 1, y: 0 }])).toBeNull();
  });

  it("keeps a turned plan's rectangle square to the screen: its corners in the view's turned frame, as a crop", () => {
    // A plan turned a quarter: the corners (0, 0) and (2, 1) drawn are (0, 0) and (-1, 2) in its frame.
    const a = boundaryArgs('rect', [{ x: 0, y: 0 }, { x: 2, y: 1 }], Math.PI / 2)!;
    close(a.min as number[], [-1, 0]);
    close(a.max as number[], [0, 2]);
  });
});

describe("a callout's grips", () => {
  const rect = node('co', 'LapinCallout', { viewId: 'v1', shape: 'rect', min: [0, 0], max: [2, 1], target: 'd1', made: true });
  const mark = { id: 'co', segments: [], data: { head: [2.8, 1.8], anchor: [2, 1], turn: 0 } };

  it("drag a rectangle's corners, edges and middle, each one updateCallout, and its head as an offset", () => {
    const grips = calloutGrips(rect, { mark }, 0);
    expect(grips.map((g) => g.key)).toEqual(['sw', 'se', 'ne', 'nw', 's', 'e', 'n', 'w', 'mid', 'head']);
    expect(grips.every((g) => g.view === 'v1')).toBe(true);
    const ne = grips.find((g) => g.key === 'ne')!;
    expect(ne.step!({ x: 3, y: 2 })).toEqual({ command: 'view.updateCallout', args: { id: 'co', min: [0, 0], max: [3, 2] } });
    const mid = grips.find((g) => g.key === 'mid')!;
    expect(mid.at).toEqual({ x: 1, y: 0.5 });
    expect(mid.step!({ x: 2, y: 0.5 })).toEqual({ command: 'view.updateCallout', args: { id: 'co', min: [1, 0], max: [3, 1] } });
    const head = grips.find((g) => g.key === 'head')!;
    expect(head.step!({ x: 3, y: 2 })).toEqual({ command: 'view.updateCallout', args: { id: 'co', head: [1, 1] } });
    // Without the engine's mark (not drawn yet), no head grip.
    expect(calloutGrips(rect, undefined, 0).some((g) => g.key === 'head')).toBe(false);
  });

  it("drag a circle's centre and radius, and a polygon's corners", () => {
    const circle = node('cc', 'LapinCallout', { viewId: 'v1', shape: 'circle', centre: [1, 1], radius: 0.5 });
    const [c, r] = calloutGrips(circle, undefined, 0);
    expect(c.step!({ x: 2, y: 2 })).toEqual({ command: 'view.updateCallout', args: { id: 'cc', centre: [2, 2] } });
    expect(r.at).toEqual({ x: 1.5, y: 1 });
    expect(r.step!({ x: 1, y: 3 })).toEqual({ command: 'view.updateCallout', args: { id: 'cc', radius: 2 } });
    const poly = node('cp', 'LapinCallout', { viewId: 'v1', shape: 'polygon', pts: [[0, 0], [2, 0], [1, 1]] });
    const grips = calloutGrips(poly, undefined, 0);
    expect(grips.map((g) => g.key)).toEqual(['pt-0', 'pt-1', 'pt-2', 'mid']);
    expect(grips[1].step!({ x: 3, y: 0 })).toEqual({ command: 'view.updateCallout', args: { id: 'cp', pts: [[0, 0], [3, 0], [1, 1]] } });
  });
});

describe('around a callout', () => {
  const nodes = new Map<string, NodeDto>([
    ['v1', node('v1', 'LapinView', { kind: 'plan', storeyId: 's1' }, 'Ground floor')],
    ['d1', node('d1', 'LapinView', { kind: 'plan', storeyId: 's1', calloutOf: 'v1' }, 'Detail 1')],
    ['s1', node('s1', 'LapinView', { kind: 'section' }, 'Section 1')],
    ['3d', node('3d', 'LapinView', { kind: '3d' }, 'Camera')],
    ['co', node('co', 'LapinCallout', { viewId: 'v1', target: 'd1', made: true })],
    ['cr', node('cr', 'LapinCallout', { viewId: 'v1', target: 's1', made: false })],
  ]);

  it('refers to any 2D view but the one it is drawn on', () => {
    expect(referableViews(nodes, 'v1').map((v) => v.id)).toEqual(['d1', 's1']);
  });

  it('asks only of the detail views its callouts made, and not of one already going', () => {
    expect(detailsOf(['co', 'cr'], nodes)).toEqual(['d1']);
    expect(detailsOf(['co', 'd1'], nodes)).toEqual([]);
    expect(detailsOf(['cr'], nodes)).toEqual([]);
  });
});
