/** The mode shape's display (docs/plan-fem.md §11.10): its swing sized to the model, the tubes of its bars, the model at rest see-through or hidden, Stop bringing back the diagrams' card, and the notes with those to check first. */

import { beforeEach, describe, expect, it, vi } from 'vitest';

const engine = vi.hoisted(() => ({
  execute: vi.fn(), query: vi.fn(), engine: { call: vi.fn() },
  useSession: Object.assign(() => null, { getState: () => ({ nodes: new Map(), render: new Map() }), setState: () => undefined }),
}));
vi.mock('../../engine/session', () => engine);

import { GHOST_OPACITY, ghostLooks, largestDisplacement, modeAmplitude, MODE_FRACTION, ringAxes, shapeDiagonal, tubeIndex, tubeRings, type ModeShapeDto } from './seismic';
import { animateMode, stopMode, useMode } from './useMode';
import { useResults } from './useResults';
import { orderNotes } from './results';

/** A 6 × 4 × 3 m box: one column, and a wall's mesh whose top moves 2 mm along X at the engine's scale. */
const shape: ModeShapeDto = {
  mode: 1, T: 0.2, f: 5, ratio: [0.8, 0, 0], scale: 1, max: 0.002,
  polylines: [{ frame: 'c1', sourceId: 'C1', base: [[0, 0, 0], [0, 0, 3]], points: [[0, 0, 0], [0.0005, 0, 1.5], [0.001, 0, 3]] }],
  shells: [{ id: 'w', sourceId: 'W1', positions: [0, 0, 0, 6, 0, 0, 6, 4, 3, 0, 4, 3], displaced: [0, 0, 0, 6, 0, 0, 6.002, 4, 3, 0.002, 4, 3], faces: [0, 1, 2, 3] }],
};

describe('the mode shape\'s swing', () => {
  it('is sized so its largest displacement is a share of the model\'s diagonal, times the card\'s scale', () => {
    const diag = Math.hypot(6, 4, 3);
    expect(shapeDiagonal(shape)).toBeCloseTo(diag, 9);
    expect(largestDisplacement(shape)).toBeCloseTo(0.002, 9);
    expect(modeAmplitude(shape) * 0.002).toBeCloseTo(MODE_FRACTION * diag, 9);
    // 7 % of 7.81 m: 0.55 m at the top, where the engine's shape moved 2 mm.
    expect(modeAmplitude(shape) * 0.002).toBeCloseTo(0.547, 3);
    expect(modeAmplitude(shape, 2)).toBeCloseTo(2 * modeAmplitude(shape), 9);
    expect(MODE_FRACTION).toBeGreaterThanOrEqual(0.05);
    expect(MODE_FRACTION).toBeLessThanOrEqual(0.1);
  });

  it('leaves a shape that does not move as it is', () => {
    const still = { ...shape, polylines: [], shells: [{ ...shape.shells[0]!, displaced: shape.shells[0]!.positions }] };
    expect(modeAmplitude(still, 1.5)).toBe(1.5);
  });
});

describe('a bar\'s tube', () => {
  it('has two triangles a quad between rings, its rings round the points at the radius', () => {
    expect(tubeIndex(3, 8)).toHaveLength(2 * 8 * 2 * 3);
    expect(Math.max(...tubeIndex(3, 8))).toBe(3 * 8 - 1);
    const [u, v] = ringAxes(0, 0, 3);
    expect(Math.abs(u[2]!) + Math.abs(v[2]!)).toBeLessThan(1e-12);
    const out = tubeRings([0, 0, 0, 0, 0, 3], u, v, 0.05, 8, new Float32Array(2 * 8 * 3));
    for (let i = 0; i < 16; i++) expect(Math.hypot(out[i * 3]!, out[i * 3 + 1]!)).toBeCloseTo(0.05, 6);
    expect(out[8 * 3 + 2]).toBeCloseTo(3, 6);
  });
});

describe('the model at rest', () => {
  it('is see-through behind the shape, or hidden', () => {
    expect(ghostLooks(['a', 'b'], true)).toEqual({ a: { opacity: GHOST_OPACITY }, b: { opacity: GHOST_OPACITY } });
    expect(ghostLooks(['a'], false)).toEqual({ a: { hidden: true } });
  });
});

describe('Stop', () => {
  beforeEach(() => { stopMode(); engine.query.mockReset(); });

  it('brings back the diagrams\' card the animation took off', async () => {
    useResults.setState({ show: true });
    engine.query.mockResolvedValue(shape);
    await animateMode(1);
    expect(useMode.getState().shape?.mode).toBe(1);
    expect(useResults.getState().show).toBe(false);
    // Another mode while one is shown: still back to the card at the end.
    await animateMode(2);
    stopMode();
    expect(useMode.getState().mode).toBeNull();
    expect(useResults.getState().show).toBe(true);
  });

  it('leaves the card off when it was not up', async () => {
    useResults.setState({ show: false });
    engine.query.mockResolvedValue(shape);
    await animateMode(1);
    stopMode();
    expect(useResults.getState().show).toBe(false);
  });

  it('brings it back when the engine has no shape to give', async () => {
    useResults.setState({ show: true });
    engine.query.mockResolvedValue(null);
    await animateMode(9);
    expect(useMode.getState().mode).toBeNull();
    expect(useResults.getState().show).toBe(true);
  });
});

describe('the notes', () => {
  it('come once each, those to check first', () => {
    const o = orderNotes([
      'Bars as Timoshenko beams, their shear areas from the section (EN 1993-1-1 §6.2.6(3) for steel, 5/6·A for a rectangle).',
      '„Q1”: 2 kN of 1 load find nothing under them (no bar, slab or wall within reach) and are left out: Load 1.',
      'Bars as Timoshenko beams, their shear areas from the section (EN 1993-1-1 §6.2.6(3) for steel, 5/6·A for a rectangle).',
      'Walls that say nothing of their load taken as load-bearing (masonry or concrete, a core of 10 cm or more): Wall 1. Set Pset_WallCommon.LoadBearing to false on a partition to leave it out.',
    ]);
    expect(o.notes).toHaveLength(3);
    expect(o.warnings).toBe(2);
    expect(o.notes.map((n) => n.warning)).toEqual([true, true, false]);
    expect(o.notes[2]!.text.startsWith('Bars as Timoshenko')).toBe(true);
  });
});
