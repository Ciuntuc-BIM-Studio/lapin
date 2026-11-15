/** Tab cycles through what lies under the pointer (forum topic 105), Delete on a sheet (topic 91), symbols on a sheet (topic 106). */

import { beforeEach, describe, expect, it } from 'vitest';
import type { NodeDto, PlanItemDto, RenderItem } from '../engine/protocol';
import { pickAllAt, pickAt, type Mark } from './planPick';
import { pickDrawing, pickDrawingAll } from './drawingPick';
import { cycleLabel, cycledId, describeNode, pickCycleTab, setCycleSource, stepCycle, usePickCycle, withFirst } from './pickCycle';
import { deleteTakesViewports, removeSteps } from './sheetDelete';
import { boxOnPaper, pickSymbol, symbolsOn } from './sheetSymbols';

const node = (id: string, type = 'IfcWall', name = id, props: Record<string, unknown> = {}): NodeDto => ({ id, type, name, props } as unknown as NodeDto);
const item = (nodeId: string, plan: PlanItemDto[]): RenderItem => ({ nodeId, plan } as unknown as RenderItem);
const rect = (kind: string, x0: number, y0: number, x1: number, y1: number): PlanItemDto => ({ kind, closed: true, points: [x0, y0, x1, y0, x1, y1, x0, y1] });
const nodesOf = (...n: NodeDto[]) => new Map(n.map((x) => [x.id, x]));
const P = (x: number, y: number) => ({ x, y });

const wall = item('w', [rect('cut', 0, 0, 4, 0.25)]);
const slab = item('s', [rect('outline', 0, 0, 4, 3)]);
const nodes = nodesOf(node('w'), node('s', 'IfcSlab'), node('g', 'IfcGrid'));

describe('pickAllAt: every element under a point, best first', () => {
  it('lists the cut, then the outline under it, and the first is what a click takes', () => {
    const all = pickAllAt(P(1, 0.1), 0.05, [slab, wall], nodes, undefined);
    expect(all).toEqual(['w', 's']);
    expect(all[0]).toBe(pickAt(P(1, 0.1), 0.05, [slab, wall], nodes, undefined)?.id);
  });
  it('lists one element once and only those under the point', () => {
    expect(pickAllAt(P(1, 2), 0.05, [wall, slab], nodes, undefined)).toEqual(['s']);
    expect(pickAllAt(P(9, 9), 0.05, [wall, slab], nodes, undefined)).toEqual([]);
  });
  it('puts an annotation first, a grid axis as grid#tag', () => {
    const marks: Mark[] = [{ id: 'g#A', segments: [2, -1, 2, 4] }];
    expect(pickAllAt(P(2.01, 0.1), 0.05, [wall, slab], nodes, marks)).toEqual(['g#A', 'w', 's']);
  });
  it('takes the later of two equal lines first, as a click does', () => {
    const a = item('a', [rect('cut', 0, 0, 1, 1)]), b = item('b', [rect('cut', 0, 0, 1, 1)]);
    const n = nodesOf(node('a'), node('b'));
    expect(pickAllAt(P(0.5, 0.5), 0.05, [a, b], n, undefined)[0]).toBe(pickAt(P(0.5, 0.5), 0.05, [a, b], n, undefined)?.id);
  });
});

describe('pickDrawingAll', () => {
  const marks = [
    { id: 'w', segments: [0, 0, 4, 0], data: { loops: [[0, 0, 4, 0, 4, 1, 0, 1]] } },
    { id: 's', segments: [0, 1, 4, 1], data: { loops: [[0, -1, 4, -1, 4, 2, 0, 2]] } },
  ] as never;
  const n = nodesOf(node('w'), node('s', 'IfcSlab'));
  it('lists the lines near the point, then the loops holding it, smallest first', () => {
    expect(pickDrawingAll(P(2, 0.01), 0.05, marks, n)).toEqual(['w', 's']);
    expect(pickDrawingAll(P(2, 0.5), 0.05, marks, n)).toEqual(['w', 's']);
    expect(pickDrawingAll(P(2, 0.01), 0.05, marks, n)[0]).toBe(pickDrawing(P(2, 0.01), 0.05, marks, n));
  });
});

describe('the Tab cycle', () => {
  it('keeps the click\'s choice first', () => {
    expect(withFirst('b', ['a', 'b', 'c'])).toEqual(['b', 'a', 'c']);
    expect(withFirst(null, ['a'])).toEqual(['a']);
  });
  it('does nothing with fewer than two candidates, so Tab keeps its meaning', () => {
    expect(stepCycle(null, 'v', P(0, 0), [], 1, 0.1)).toBeNull();
    expect(stepCycle(null, 'v', P(0, 0), ['a'], 1, 0.1)).toBeNull();
  });
  it('starts on the second, goes round, and Shift goes back', () => {
    let c = stepCycle(null, 'v', P(0, 0), ['a', 'b', 'c'], 1, 0.1)!;
    expect(c.ids[c.index]).toBe('b');
    c = stepCycle(c, 'v', P(0.01, 0), ['a', 'b', 'c'], 1, 0.1)!;
    expect(c.ids[c.index]).toBe('c');
    c = stepCycle(c, 'v', P(0, 0), ['a', 'b', 'c'], 1, 0.1)!;
    expect(c.ids[c.index]).toBe('a');
    c = stepCycle(c, 'v', P(0, 0), ['a', 'b', 'c'], -1, 0.1)!;
    expect(c.ids[c.index]).toBe('c');
    expect(stepCycle(null, 'v', P(0, 0), ['a', 'b', 'c'], -1, 0.1)!.index).toBe(2);
  });
  it('starts again when the pointer has moved or what lies there changed', () => {
    const c = stepCycle(null, 'v', P(0, 0), ['a', 'b', 'c'], 1, 0.1)!;
    expect(stepCycle(c, 'v', P(1, 0), ['a', 'b', 'c'], 1, 0.1)!.index).toBe(1);
    expect(stepCycle(c, 'v', P(0, 0), ['a', 'b'], 1, 0.1)!.index).toBe(1);
    expect(stepCycle(c, 'other', P(0, 0), ['a', 'b', 'c'], 1, 0.1)!.index).toBe(1);
  });
  it('lights its candidate only at the same place of the same view', () => {
    const c = stepCycle(null, 'v', P(0, 0), ['a', 'b'], 1, 0.1)!;
    expect(cycledId(c, 'v', P(0.05, 0), 0.1)).toBe('b');
    expect(cycledId(c, 'v', P(1, 0), 0.1)).toBeNull();
    expect(cycledId(c, 'w', P(0, 0), 0.1)).toBeNull();
    expect(cycledId(null, 'v', P(0, 0), 0.1)).toBeNull();
  });
  it('says which of how many, and what', () => {
    const c = stepCycle(null, 'v', P(0, 0), ['a', 'b', 'c'], 1, 0.1)!;
    expect(cycleLabel(c, (id) => `Slab ${id}`)).toBe('2 of 3: Slab b');
    expect(describeNode(node('s', 'IfcSlab', 'Floor 1'), 's')).toBe('Slab Floor 1');
    expect(describeNode(node('g', 'IfcGrid', 'Grid'), 'g#A')).toBe('Axis A');
  });
});

describe('Tab through the source the pointer is over', () => {
  beforeEach(() => { setCycleSource(null); usePickCycle.setState({ cycle: null }); });
  const src = (ids: string[]) => ({ key: 'v', at: () => P(0, 0), ids: () => ids, tol: () => 0.1 });
  it('is not taken with no view, nothing or one element under the pointer', () => {
    expect(pickCycleTab(false, new Map())).toBe(false);
    setCycleSource(src(['a']));
    expect(pickCycleTab(false, new Map())).toBe(false);
  });
  it('is taken with several, and goes round; leaving the view gives Tab back', () => {
    setCycleSource(src(['a', 'b', 'c']));
    expect(pickCycleTab(false, nodesOf(node('b', 'IfcSlab', 'Floor')))).toBe(true);
    expect(usePickCycle.getState().cycle?.index).toBe(1);
    expect(pickCycleTab(true, new Map())).toBe(true);
    expect(usePickCycle.getState().cycle?.index).toBe(0);
    setCycleSource(null, 'v');
    expect(pickCycleTab(false, new Map())).toBe(false);
  });
});

describe('Delete on a sheet', () => {
  it('belongs to the active views when they are active and nothing else is selected', () => {
    expect(deleteTakesViewports(['vp1'], ['sheet'], 'sheet')).toBe(true);
    expect(deleteTakesViewports(['vp1', 'vp2'], [], 'sheet')).toBe(true);
  });
  it('stays with the selection otherwise: a selected symbol, or no active view', () => {
    expect(deleteTakesViewports(['vp1'], ['sym'], 'sheet')).toBe(false);
    expect(deleteTakesViewports([], ['sheet'], 'sheet')).toBe(false);
  });
  it('removes the views only, one edit for all of them', () => {
    expect(removeSteps('sheet', ['a', 'b', 'a'])).toEqual([
      { command: 'sheet.removeViewport', args: { id: 'sheet', viewportId: 'a' } },
      { command: 'sheet.removeViewport', args: { id: 'sheet', viewportId: 'b' } },
    ]);
  });
});

describe('symbols placed on a sheet', () => {
  const sym = (id: string, at: [number, number], scale = 1) => node(id, 'IfcAnnotation', 'North', { sheetId: 'sh', symbolId: 'north', at, scale });
  it('are the elements that name the sheet and a symbol', () => {
    const n = nodesOf(sym('a', [10, 10]), node('b', 'IfcAnnotation', 'x', { viewId: 'v', symbolId: 'north', at: [1, 1] }), node('sh', 'LapinSheet'));
    expect(symbolsOn(n, 'sh').map((x) => x.id)).toEqual(['a']);
    expect(symbolsOn(n, 'other')).toEqual([]);
  });
  it('have a box from the preview, at their scale, and a click takes the smallest under it', () => {
    const big = boxOnPaper(sym('big', [50, 50], 2), [-10, -10, 10, 10]);
    const small = boxOnPaper(sym('small', [50, 50]), [-2, -2, 2, 2]);
    expect(big.box).toEqual([30, 30, 70, 70]);
    expect(pickSymbol([big, small], P(51, 51))?.id).toBe('small');
    expect(pickSymbol([big, small], P(65, 50))?.id).toBe('big');
    expect(pickSymbol([big, small], P(100, 100))).toBeNull();
    expect(pickSymbol([small], P(52.5, 50))?.id).toBe('small');
  });
});
