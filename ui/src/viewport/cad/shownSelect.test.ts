/** A plan's selection takes only what the view shows (decision 498), as its snaps do (decision 491). */

import { describe, expect, it } from 'vitest';
import type { NodeDto, PlanItemDto, RenderItem } from '../../engine/protocol';
import type { DrawingDto } from '../drawing';
import { pickAt, regionIds } from '../planPick';
import { boxTest } from '../selectBox';
import { clearShown, hiddenHere, hiddenInDrawing, isShown, publishShown, shownItems, useShownHere } from './shown';

const node = (id: string, type = 'IfcWall'): NodeDto => ({ id, type, name: id } as unknown as NodeDto);
const item = (nodeId: string, plan: PlanItemDto[]): RenderItem => ({ nodeId, plan } as unknown as RenderItem);
const cut = (x0: number, y0: number, x1: number, y1: number): PlanItemDto => ({ kind: 'cut', closed: true, points: [x0, y0, x1, y0, x1, y1, x0, y1] });
const P = (x: number, y: number) => ({ x, y });

// Wall A at y = 0, wall B at y = 3 (hidden by the view's Visibility / Graphics: no mark), beam C overhead (a mark with no lines),
// a hall H drawn as one whose member M has its own plan lines, and a part P of the assembly Q (the drawing names P).
const items = [
  item('A', [cut(0, 0, 5, 0.2)]), item('B', [cut(0, 3, 5, 3.2)]), item('C', [{ kind: 'overhead', closed: false, points: [0, 6, 5, 6] }]),
  item('M', [cut(10, 0, 10.2, 5)]), item('P', [cut(20, 0, 20.2, 5)]),
];
const nodes = new Map(['A', 'B', 'C', 'M', 'P', 'H', 'Q'].map((id) => [id, node(id)]));
const drawing: DrawingDto = {
  kind: 'plan', scale: 100, pens: [],
  marks: [{ id: 'A', segments: [0, 0, 5, 0] }, { id: 'C', segments: [] }, { id: 'H', segments: [10, 0, 10, 5] }, { id: 'P', segments: [20, 0, 20, 5] }],
};
const whole: Record<string, string> = { M: 'H', P: 'Q' };
const wholeOf = (id: string) => whole[id];

describe('what a plan selects', () => {
  const shown = shownItems(items, drawing, wholeOf) as RenderItem[];

  it('keeps what the drawing shows: an element drawn only overhead, a part of a whole drawn as one', () => {
    expect(shown.map((r) => r.nodeId)).toEqual(['A', 'C', 'M', 'P']);
    expect(isShown('M', new Set(['H']), wholeOf)).toBe(true);
    expect(isShown('Q', new Set(['P']), wholeOf)).toBe(false);
  });

  it('never takes a hidden element by a click', () => {
    expect(pickAt(P(1, 3.1), 0.05, shown, nodes, drawing.marks)).toBeNull();
    expect(pickAt(P(1, 0.1), 0.05, shown, nodes, drawing.marks)?.id).toBe('A');
    // Overhead, by its own modelling line.
    expect(pickAt(P(1, 6.01), 0.05, shown, nodes, drawing.marks)?.id).toBe('C');
    // A member of a hall drawn as one keeps its own lines (drilling goes on from it).
    expect(pickAt(P(10.1, 2), 0.05, shown, nodes, undefined)?.id).toBe('M');
  });

  it('never takes a hidden element by a window or a crossing box', () => {
    const all = regionIds(boxTest(P(-1, -1), P(6, 7), true), shown, nodes, drawing.marks);
    expect(all).toContain('A');
    expect(all).toContain('C');
    expect(all).not.toContain('B');
    expect(regionIds(boxTest(P(6, 3.1), P(1, 3.15), false), shown, nodes, drawing.marks)).toEqual([]);
  });

  it('takes everything on screen over modelling lines (no drawing)', () => {
    expect(shownItems(items, null).map((r) => r.nodeId)).toEqual(['A', 'B', 'C', 'M', 'P']);
    expect(pickAt(P(1, 3.1), 0.05, items, nodes, undefined)?.id).toBe('B');
  });

  it('says when a selected element is not shown here, only for what is modelled', () => {
    const modelled = (id: string) => items.some((r) => r.nodeId === id);
    expect(hiddenInDrawing('B', drawing, modelled, wholeOf)).toBe(true);
    expect(hiddenInDrawing('A', drawing, modelled, wholeOf)).toBe(false);
    expect(hiddenInDrawing('M', drawing, modelled, wholeOf)).toBe(false);
    // A type or a view: no view draws it, nothing to say.
    expect(hiddenInDrawing('T', drawing, modelled, wholeOf)).toBe(false);
    // No drawing to ask: nothing to say.
    expect(hiddenInDrawing('B', null, modelled, wholeOf)).toBe(false);
  });

  it('asks the view in front, and forgets a view put away', () => {
    publishShown('plan-1', (id) => id === 'B');
    expect(hiddenHere('B')).toBe(true);
    expect(hiddenHere('A')).toBe(false);
    // Another pane came to the front before the first one let go: the first one's clearing leaves the other's.
    publishShown('3d', () => false);
    clearShown('plan-1');
    expect(useShownHere.getState().owner).toBe('3d');
    expect(hiddenHere('B')).toBe(false);
    clearShown('3d');
    expect(useShownHere.getState().hides).toBeNull();
    expect(hiddenHere('B')).toBe(false);
  });
});
