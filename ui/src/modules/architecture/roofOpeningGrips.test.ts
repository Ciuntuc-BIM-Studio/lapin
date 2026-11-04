import { describe, expect, it, vi } from 'vitest';

vi.mock('../../engine/session', () => ({ execute: vi.fn() }));

import { execute } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { gripPoint, resizeOpening, roofOpeningGrips, roofOpeningRefs, type OpeningFrame } from './roofOpeningGrips';
import { setHotRemove, takeHotRemove } from '../../viewport/grips';

// A south slope at 30°: the eave along +X, up the slope +Y; a metre up the slope is cos 30° in plan.
const k = Math.cos(Math.PI / 6);
const frame: OpeningFrame = { d: [1, 0], n: [0, 1], k };
const win = { id: 'w', kind: 'window', name: 'Roof window 1', x: 3, y: 1.5, width: 0.78, length: 1.18 };
const roof = (openings: unknown[], frames: Record<string, OpeningFrame> = { w: frame, dm: frame }) =>
  ({ id: 'r', type: 'IfcRoof', name: 'Roof 1', parentId: null, px: 0, py: 0, props: { openings, openingFrames: frames }, editable: true, module: 'architecture', storeyId: 's' }) as unknown as NodeDto;

describe('a roof window resized by its grips', () => {
  it('moves the edge dragged, the opposite one staying where it was', () => {
    // The right edge from 3.39 to 3.60: 0.99 wide, its middle 0.105 to the right.
    const r = resizeOpening(win, frame, 1, 0, { x: 3.6, y: 9 })!;
    expect(r.width).toBeCloseTo(0.99, 9);
    expect(r.length).toBeCloseTo(1.18, 9);
    expect(r.x).toBeCloseTo(3.105, 9);
    expect(r.y).toBeCloseTo(1.5, 9);
    expect(r.x - r.width / 2).toBeCloseTo(3 - 0.39, 9);
  });

  it('measures a length up the slope on the slope, not in plan', () => {
    // The top edge 0.2 m further up in plan: 0.2 / cos 30° longer on the slope; the bottom edge stays.
    const top = gripPoint(win, frame, 0, 1);
    const r = resizeOpening(win, frame, 0, 1, { x: top.x, y: top.y + 0.2 })!;
    expect(r.length).toBeCloseTo(1.18 + 0.2 / k, 3);
    expect(r.y - (r.length / 2) * k).toBeCloseTo(1.5 - (1.18 / 2) * k, 3);
    expect(r.width).toBeCloseTo(0.78, 9);
  });

  it('a corner resizes both ways; past the opposite edge, or thinner than 200 mm, it is refused', () => {
    const r = resizeOpening(win, frame, -1, -1, { x: 2.5, y: 0.9 })!;
    expect(r.width).toBeCloseTo(3.39 - 2.5, 9);
    expect(r.length).toBeCloseTo((1.5 + (1.18 / 2) * k - 0.9) / k, 3);
    expect(resizeOpening(win, frame, 1, 0, { x: 2.5, y: 1.5 })).toBeNull();
    expect(resizeOpening(win, frame, 0, 1, { x: 3, y: 1.5 - 0.5 })).toBeNull();
  });
});

describe('the roof\'s grips for its openings', () => {
  it('a window has its middle, four edges and four corners; a dormer its middle and its two sides', () => {
    const grips = roofOpeningGrips(roof([win, { id: 'dm', kind: 'dormer', x: 7, y: 0.6, width: 1.4 }]));
    expect(grips.filter((g) => g.key.startsWith('opening:w')).map((g) => g.kind)).toEqual(['centre', 'edgeMid', 'edgeMid', 'edgeMid', 'edgeMid', 'vertex', 'vertex', 'vertex', 'vertex']);
    const dormer = grips.filter((g) => g.key.startsWith('opening:dm'));
    expect(dormer.map((g) => g.at)).toEqual([{ x: 7, y: 0.6 }, { x: 7.7, y: 0.6 }, { x: 6.3, y: 0.6 }]);
    expect(dormer[1].step!({ x: 8, y: 3 })).toEqual({ command: 'architecture.updateRoofOpening', args: { roofId: 'r', id: 'dm', x: 7.15, y: 0.6, width: 1.7 } });
    expect(grips.every((g) => g.removeLabel === (g.key.startsWith('opening:dm') ? 'Remove dormer' : 'Remove roof window'))).toBe(true);
  });

  it('without its frame (an old engine) only the middle, as before', () => {
    expect(roofOpeningGrips(roof([win], {})).map((g) => g.key)).toEqual(['opening:w']);
    expect(roofOpeningRefs(roof([win], {}))).toEqual([]);
  });

  it('draws a window\'s outline over the roof', () => {
    const [ref] = roofOpeningRefs(roof([win]));
    expect(ref.closed).toBe(true);
    expect(ref.points[0].x).toBeCloseTo(2.61, 9);
    expect(ref.points[2].y).toBeCloseTo(1.5 + 0.59 * k, 9);
  });

  it('Delete over a grip removes its window, once, and nothing when the cursor left it', () => {
    const [middle] = roofOpeningGrips(roof([win]));
    setHotRemove(middle);
    expect(takeHotRemove()).toBe(true);
    expect(execute).toHaveBeenCalledWith('architecture.removeRoofOpening', { roofId: 'r', id: 'w' });
    expect(takeHotRemove()).toBe(false);
    setHotRemove(middle);
    setHotRemove(null);
    expect(takeHotRemove()).toBe(false);
    // A grip whose remove is a vertex's (no label of its own) never takes Delete.
    setHotRemove({ key: 'v', at: { x: 0, y: 0 }, remove: () => ({ command: 'x', args: {} }) });
    expect(takeHotRemove()).toBe(false);
  });
});
