/** A plan's picking and selection box, on hand-made plan lines. */

import { describe, expect, it } from 'vitest';
import type { NodeDto, PlanItemDto, RenderItem } from '../engine/protocol';
import { boxIds, pickAt, type Mark } from './planPick';

const node = (id: string, type = 'IfcWall'): NodeDto => ({ id, type, name: id } as unknown as NodeDto);
const item = (nodeId: string, plan: PlanItemDto[]): RenderItem => ({ nodeId, plan } as unknown as RenderItem);
/** A closed rectangle's outline as a plan item. */
const rect = (kind: string, x0: number, y0: number, x1: number, y1: number): PlanItemDto => ({ kind, closed: true, points: [x0, y0, x1, y0, x1, y1, x0, y1] });
const nodesOf = (...n: NodeDto[]) => new Map(n.map((x) => [x.id, x]));

// A wall cut 0..4 × 0..0.25, a slab outline 0..4 × 0..3 under it, a grid with its axis A at x = 2.
const wall = item('w', [rect('cut', 0, 0, 4, 0.25)]);
const slab = item('s', [rect('outline', 0, 0, 4, 3)]);
const nodes = nodesOf(node('w'), node('s', 'IfcSlab'), node('g', 'IfcGrid'));
const marks: Mark[] = [{ id: 'g#A', segments: [2, -1, 2, 4] }];
const P = (x: number, y: number) => ({ x, y });

describe('pickAt', () => {
  it('takes the cut before the outline where both lie', () => {
    expect(pickAt(P(1, 0.1), 0.05, [slab, wall], nodes, undefined)?.id).toBe('w');
    expect(pickAt(P(1, 0.1), 0.05, [wall, slab], nodes, undefined)?.id).toBe('w');
  });

  it('takes a face inside its closed outline, and nothing outside every one', () => {
    expect(pickAt(P(1, 2), 0.05, [wall, slab], nodes, undefined)?.id).toBe('s');
    expect(pickAt(P(9, 9), 0.05, [wall, slab], nodes, undefined)).toBeNull();
  });

  it('picks an annotation first, a grid axis on its own as grid#tag', () => {
    const hit = pickAt(P(2.01, 2), 0.05, [wall, slab], nodes, marks);
    expect(hit?.id).toBe('g#A');
    expect(hit?.name).toBe('Axis A');
    expect(hit?.type).toBe('IfcGrid');
  });

  it('with classes asked for, skips annotations and the other classes', () => {
    expect(pickAt(P(2.01, 0.1), 0.05, [wall, slab], nodes, marks, ['IfcSlab'])?.id).toBe('s');
  });

  it('takes a room inside its outline before the floor under it, and a wall on its edge before the room', () => {
    const room = item('r', [rect('room', 0, 0.25, 4, 3)]);
    const withRoom = nodesOf(node('w'), node('s', 'IfcSlab'), node('r', 'IfcSpace'));
    expect(pickAt(P(1, 2), 0.05, [slab, room, wall], withRoom, undefined)?.id).toBe('r');
    expect(pickAt(P(1, 2), 0.05, [room, slab, wall], withRoom, undefined)?.id).toBe('r');
    expect(pickAt(P(1, 0.1), 0.05, [room, slab, wall], withRoom, undefined)?.id).toBe('w');
  });

  it('ignores lines of an element no longer in the model', () => {
    expect(pickAt(P(1, 0.1), 0.05, [wall], nodesOf(node('s', 'IfcSlab')), undefined)).toBeNull();
  });
});

describe('boxIds', () => {
  it('a window takes what lies wholly inside it', () => {
    expect(boxIds(P(-1, -1), P(5, 1), true, [wall, slab], nodes, marks)).toEqual(['w']);
  });

  it('a crossing box takes what it touches, annotations included', () => {
    expect(boxIds(P(1.5, 0.1), P(2.5, 0.3), false, [wall, slab], nodes, marks).sort()).toEqual(['g#A', 'w']);
  });

  it('skips an annotation whose owner went', () => {
    expect(boxIds(P(-1, -2), P(5, 5), true, [wall, slab], nodesOf(node('w'), node('s', 'IfcSlab')), marks).sort()).toEqual(['s', 'w']);
  });
});
