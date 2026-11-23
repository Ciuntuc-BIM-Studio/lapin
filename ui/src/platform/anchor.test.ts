import { beforeEach, describe, expect, it } from 'vitest';
import { ANCHORS, alongShift, anchorFor, anchorsOf, insertionAt, readAnchors, screenSense, setAnchor, shiftedAlong, snapAlong, stepAnchor, useAnchors } from './anchor';
import { requestFor, placedAt } from './placement';
import type { NodeDto } from '../engine/protocol';

const box = { x0: -0.15, x1: 0.15, y0: -0.2, y1: 0.2 }; // a 300 × 400 column
const at = { x: 10, y: 5 };
const wallOf = (a: [number, number], b: [number, number]) => ({ id: 'w', type: 'IfcWall', name: 'W', props: { start: a, end: b } }) as unknown as NodeDto;
const arc = (a: [number, number], b: [number, number]) => ({ a: { x: a[0], y: a[1] }, b: { x: b[0], y: b[1] }, rise: 0 });

describe('the insertion anchor of a box (a column)', () => {
  it('puts the insertion point where each of the nine anchors puts the box about the cursor', () => {
    // The anchor point of the box lies at the cursor: the insertion point is the cursor less the anchor's offset in the box.
    const want: Record<string, [number, number]> = {
      '-1,1': [10.15, 4.8], '0,1': [10, 4.8], '1,1': [9.85, 4.8],
      '-1,0': [10.15, 5], '0,0': [10, 5], '1,0': [9.85, 5],
      '-1,-1': [10.15, 5.2], '0,-1': [10, 5.2], '1,-1': [9.85, 5.2],
    };
    expect(ANCHORS).toHaveLength(9);
    for (const a of ANCHORS) {
      const p = insertionAt(at, a, box);
      const [x, y] = want[`${a.h},${a.v}`];
      expect(p.x).toBeCloseTo(x, 9);
      expect(p.y).toBeCloseTo(y, 9);
    }
  });

  it('leaves the insertion point where it was for the centre anchor, whatever the box (an off-centre section is placed as ever)', () => {
    expect(insertionAt(at, { h: 0, v: 0 }, { x0: 0, x1: 0.4, y0: 0, y1: 0.2 })).toEqual(at);
  });

  it('turns the box with the element', () => {
    const p = insertionAt(at, { h: 1, v: 0 }, box, 90); // its right edge (x1 = 0.15) now points up the screen
    expect(p.x).toBeCloseTo(10, 9);
    expect(p.y).toBeCloseTo(4.85, 9);
  });
});

describe('the insertion anchor along a wall (a door, a window)', () => {
  it('offers three anchors along the wall, the nine for a box', () => {
    expect(anchorsOf('along').map((a) => a.h)).toEqual([-1, 0, 1]);
    expect(anchorsOf('along').every((a) => a.v === 0)).toBe(true);
    expect(anchorsOf('box')).toHaveLength(9);
  });

  it('puts the centre half the width from the cursor: left edge at the cursor, centre to its right', () => {
    const a = arc([0, 0], [6, 0]);
    expect(alongShift(a, 2, 0.9, -1)).toBeCloseTo(0.45, 9);
    expect(alongShift(a, 2, 0.9, 0)).toBeCloseTo(0, 9);
    expect(alongShift(a, 2, 0.9, 1)).toBeCloseTo(-0.45, 9);
  });

  it('reads left and right as the screen has them, whichever way the wall was drawn', () => {
    const back = arc([6, 0], [0, 0]); // drawn right to left
    expect(screenSense(back, 1)).toBe(-1);
    expect(alongShift(back, 1, 0.9, -1)).toBeCloseTo(-0.45, 9); // along runs the other way: the centre is at a smaller along
    // A vertical wall: up is the way it reads "right"
    const up = arc([0, 0], [0, 6]);
    const down = arc([0, 6], [0, 0]);
    expect(screenSense(up, 1)).toBe(1);
    expect(screenSense(down, 1)).toBe(-1);
  });

  it('moves the cursor along the wall to the opening\'s centre, its side of the wall kept', () => {
    const a = arc([0, 0], [6, 0]);
    expect(shiftedAlong(a, { x: 2, y: 0.3 }, 0.45)).toEqual({ x: 2.45, y: 0.3 });
    expect(shiftedAlong(a, { x: 2, y: 0.3 }, 0)).toEqual({ x: 2, y: 0.3 });
  });

  it('makes the module\'s question about the opening as it will stand, and takes the place it offers as the centre', () => {
    const w = wallOf([0, 0], [6, 0]);
    const r = requestFor(w, { x: 2, y: 0.1 }, { kind: 'door', width: 0.9, typeId: 't', shift: 0.45 });
    expect(r?.req.along).toBeCloseTo(2.45, 9);
    expect((r?.req as unknown as { shift?: number }).shift).toBeUndefined();
    // The module offers a centre at 3: the cursor is put where the left edge is for that centre (2.55), within a metre.
    const put = placedAt(arc([0, 0], [6, 0]), { x: 2.5, y: 0.2 }, { ok: false, snapAlong: 3 }, 0.45);
    expect(put.snapped).toBe(true);
    expect(put.point.x).toBeCloseTo(2.55, 9);
  });
});

describe('the grid along a wall', () => {
  it('moves a point along the wall to the step, from the wall\'s start', () => {
    const a = arc([0, 0], [6, 0]);
    expect(snapAlong(a, { x: 2.37, y: 0.2 }, 0.05).x).toBeCloseTo(2.35, 9);
    expect(snapAlong(a, { x: 2.37, y: 0.2 }, 0.5).x).toBeCloseTo(2.5, 9);
    expect(snapAlong(a, { x: 2.37, y: 0.2 }, 0.05).y).toBe(0.2);
    expect(snapAlong(a, { x: 2.37, y: 0.2 }, 0)).toEqual({ x: 2.37, y: 0.2 });
    // an angled wall: along its axis
    const d = arc([0, 0], [3, 4]);
    const p = snapAlong(d, { x: 1.5, y: 2 }, 1); // 2.5 along → 3 (or 2) m along
    expect(Math.hypot(p.x, p.y)).toBeCloseTo(3, 9);
  });
});

describe('the anchor kept per tool', () => {
  beforeEach(() => { useAnchors.setState({ byTool: {} }); });

  it('is the centre until one is chosen, then remembered for that tool only', () => {
    expect(anchorFor('structure.column')).toEqual({ h: 0, v: 0 });
    setAnchor('structure.column', { h: -1, v: -1 }, 'box');
    setAnchor('architecture.door', { h: 1, v: 1 }, 'along'); // a door has no vertical anchor
    expect(anchorFor('structure.column')).toEqual({ h: -1, v: -1 });
    expect(anchorFor('architecture.door')).toEqual({ h: 1, v: 0 });
    expect(anchorFor('architecture.window')).toEqual({ h: 0, v: 0 });
  });

  it('steps through the nine, and the three, round', () => {
    let a = { h: 0, v: 0 } as ReturnType<typeof anchorFor>;
    const seen = new Set<string>();
    for (let i = 0; i < 9; i++) { a = stepAnchor(a, 'box'); seen.add(`${a.h},${a.v}`); }
    expect(seen.size).toBe(9);
    expect(a).toEqual({ h: 0, v: 0 });
    expect(stepAnchor({ h: 0, v: 0 }, 'along')).toEqual({ h: 1, v: 0 });
    expect(stepAnchor({ h: 1, v: 0 }, 'along')).toEqual({ h: -1, v: 0 });
    expect(stepAnchor({ h: -1, v: 0 }, 'along', -1)).toEqual({ h: 1, v: 0 });
  });

  it('reads what was kept, anything unreadable at the centre', () => {
    expect(readAnchors({ a: { h: 1, v: -1 }, b: { h: 5, v: 'x' }, c: 7 })).toEqual({ a: { h: 1, v: -1 }, b: { h: 0, v: 0 } });
    expect(readAnchors(null)).toEqual({});
  });
});
