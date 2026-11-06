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

describe('pickAt: a door in its wall (tester report, Lapin 0.1.592)', () => {
  // A wall along y = 0, 0.25 thick, with a 0.9 m door at x = 2.55..3.45: the wall cut on both sides of the opening.
  const wallCut = item('w', [rect('cut', -0.125, -0.125, 2.55, 0.125), rect('cut', 3.45, -0.125, 6, 0.125)]);
  const door = item('d', [
    rect('hit', 2.55, -0.125, 3.45, 0.125),
    { kind: 'symbol', closed: false, points: [2.55, -0.125, 2.55, 0.125] },
    { kind: 'symbol', closed: false, points: [3.45, -0.125, 3.45, 0.125] },
    rect('heavy', 2.6, 0.05, 2.64, 0.85),
  ]);
  const doorNodes = nodesOf(node('w'), node('d', 'IfcDoor'), node('f', 'LapinRoomFinish'), node('t', 'IfcAnnotation'));
  // The drawing's pick lines (for sections): the wall's cut outline, beside the jamb; a floor finish's threshold across the opening (it has no plan lines).
  const pickLines: Mark[] = [
    { id: 'w', pick: true, segments: [2.53, -0.125, 2.53, 0.125, 2.53, 0.125, 2.55, 0.125] },
    { id: 'f', pick: true, segments: [2.55, 0, 3.45, 0, 3.45, 0, 3.45, 0.125] },
  ];
  // The door's marker (decision 143): a line across the wall through the opening's middle.
  const tag: Mark = { id: 't', segments: [3, -0.6, 3, 0.6], data: { of: 'd' } };

  it('takes the door at its jamb and its leaf, not the wall by the drawing\'s pick lines', () => {
    expect(pickAt(P(2.55, 0), 0.05, [wallCut, door], doorNodes, pickLines)?.id).toBe('d');
    expect(pickAt(P(2.62, 0.1), 0.05, [wallCut, door], doorNodes, pickLines)?.id).toBe('d');
    expect(pickAt(P(1, 0), 0.05, [wallCut, door], doorNodes, pickLines)?.id).toBe('w');
  });

  it('takes the door in its opening over a room finish drawn across it, the finish elsewhere', () => {
    expect(pickAt(P(3, 0), 0.05, [wallCut, door], doorNodes, pickLines)?.id).toBe('d');
    expect(pickAt(P(3, 0), 0.05, [wallCut], doorNodes, pickLines)?.id).toBe('f');
  });

  it('takes the door where its marker crosses it, the marker outside the wall', () => {
    expect(pickAt(P(3, 0.02), 0.05, [wallCut, door], doorNodes, [tag, ...pickLines])?.id).toBe('d');
    expect(pickAt(P(3, -0.5), 0.05, [wallCut, door], doorNodes, [tag, ...pickLines])?.id).toBe('t');
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

describe('drafting sent behind the model (decision 339)', () => {
  const draft = { ...node('h', 'IfcAnnotation'), props: { viewId: 'v' } } as unknown as NodeDto;
  const all = nodesOf(node('w'), node('s', 'IfcSlab'), draft);
  const over: Mark[] = [{ id: 'h', segments: [0, 0.1, 4, 0.1] }];
  const behind: Mark[] = [{ id: 'h', segments: [0, 0.1, 4, 0.1], behind: true }];

  it('gives way to the wall over it, and is taken where only an outline is', () => {
    expect(pickAt(P(1, 0.1), 0.05, [wall, slab], all, over)?.id).toBe('h');
    expect(pickAt(P(1, 0.1), 0.05, [wall, slab], all, behind)?.id).toBe('w');
    const low: Mark[] = [{ id: 'h', segments: [0, 2, 4, 2], behind: true }];
    expect(pickAt(P(1, 2), 0.05, [wall, slab], all, low)?.id).toBe('h');
  });
});
