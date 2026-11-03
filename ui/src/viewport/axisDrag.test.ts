import { afterEach, describe, expect, it, vi } from 'vitest';
import { alongLine, beginAxisDrag, boxEdges3, commitAxisDrag, endAxisDrag, middleOf, snapDistance, useAxisDrag, XYZ } from './axisDrag';

const z = { x: 0, y: 0, z: 1 };

describe('axisDrag: a distance set along an axis on the 3D view', () => {
  afterEach(() => endAxisDrag());

  it('reads the distance where the cursor\'s ray passes nearest the axis', () => {
    // Looking along +y at the height 2.5 from 10 m away: the point of the vertical axis through the origin nearest the ray is at z = 2.5.
    expect(alongLine({ x: 0, y: 0, z: 0 }, z, { x: 0, y: -10, z: 2.5 }, { x: 0, y: 1, z: 0 })).toBeCloseTo(2.5, 9);
    // From an axis that starts at z = 1: 1.5 along it.
    expect(alongLine({ x: 0, y: 0, z: 1 }, z, { x: 0, y: -10, z: 2.5 }, { x: 0, y: 1, z: 0 })).toBeCloseTo(1.5, 9);
    // A slanted ray, down onto the axis: the nearest point is where it passes it.
    const r = alongLine({ x: 0, y: 0, z: 0 }, z, { x: -4, y: 0, z: 7 }, { x: 4 / 5, y: 0, z: -3 / 5 });
    expect(r).toBeCloseTo(4, 9);
    // Seen end on, the axis cannot be dragged.
    expect(alongLine({ x: 0, y: 0, z: 0 }, z, { x: 0, y: 0, z: 9 }, { x: 0, y: 0, z: -1 })).toBeNull();
  });

  it('keeps a drag to the centimetre, to 10 cm with Shift, and above its least', () => {
    expect(snapDistance(0.2349)).toBe(0.23);
    expect(snapDistance(-0.2351)).toBe(-0.24);
    expect(snapDistance(0.26, true)).toBe(0.3);
    expect(snapDistance(-0.4, false, 0)).toBe(0);
  });

  it('a face\'s middle and a box\'s edges', () => {
    expect(middleOf([{ x: 0, y: 0, z: 0 }, { x: 2, y: 0, z: 0 }, { x: 2, y: 4, z: 0 }, { x: 0, y: 4, z: 0 }])).toEqual({ x: 1, y: 2, z: 0 });
    const edges = boxEdges3({ x: 0, y: 0, z: 0 }, { x: 1, y: 2, z: 3 }, { x: 0, y: 0, z: 1 });
    expect(edges).toHaveLength(12);
    expect(Math.min(...edges.flat().map((p) => p.z))).toBe(1);
    expect(Math.max(...edges.flat().map((p) => p.z))).toBe(4);
  });

  it('applies the typed distance, else the dragged one, else the last one applied; nothing for none', async () => {
    const commit = vi.fn();
    const spec = { title: 'Push/Pull', at: { x: 0, y: 0, z: 0 }, axes: [{ dir: z }], preview: { kind: 'prism' as const, outline: [] }, commit };
    beginAxisDrag(spec);
    expect(useAxisDrag.getState().axis).toBe(0);
    useAxisDrag.setState({ distance: 0.3 });
    await commitAxisDrag(null);
    expect(commit).toHaveBeenLastCalledWith(0.3, 0, undefined);
    expect(useAxisDrag.getState().spec).toBeNull();
    // Enter without a drag or a value: the last distance again (SketchUp's repeat).
    beginAxisDrag(spec);
    await commitAxisDrag(null);
    expect(commit).toHaveBeenLastCalledWith(0.3, 0, undefined);
    beginAxisDrag(spec);
    useAxisDrag.setState({ distance: 0.3 });
    await commitAxisDrag(-0.25);
    expect(commit).toHaveBeenLastCalledWith(-0.25, 0, undefined);
    // A move starts on z, its option and axis go with the value; a split's plane may stay in the face (zero).
    const move = vi.fn();
    beginAxisDrag({ title: 'Move 3D', at: { x: 0, y: 0, z: 0 }, axes: XYZ, preview: { kind: 'box', min: { x: 0, y: 0, z: 0 }, max: { x: 1, y: 1, z: 1 } }, commit: move });
    expect(useAxisDrag.getState().axis).toBe(2);
    await commitAxisDrag(0);
    expect(move).not.toHaveBeenCalled();
    const split = vi.fn();
    beginAxisDrag({ title: 'Split', at: { x: 0, y: 0, z: 0 }, axes: [{ dir: z }], preview: { kind: 'plane', outline: [] }, zero: true, options: { label: 'Keep', value: 'back', values: [] }, commit: split });
    await commitAxisDrag(0);
    expect(split).toHaveBeenLastCalledWith(0, 0, 'back');
  });
});
