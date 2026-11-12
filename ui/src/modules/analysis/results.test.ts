import { describe, expect, it } from 'vitest';
import {
  balanceText, defaultPick, footOnBase, formatMm, formatValue, hatchIndices, labelIndices, ownReleases, pickArgs, placeLabels, pickChoices, pickFrom, pickValue,
  reactionLabel, reactionVector, releasePreset, releaseValue, signColour, spanRatio, toggleRelease, validPick, FIXED, PINNED, SIGN_COLOUR,
  type RunDto,
} from './results';

const run = (combos: { key: string; kind: string; name: string }[] = [], loaded = true): RunDto => ({
  ok: true, dofs: 12, elements: 1, nodes: 2, solveMs: 1, totalMs: 2, profile: 3, notes: [], combinations: combos,
  cases: [
    { id: 'g', code: 'G1', sumLoads: [0, 0, loaded ? -10 : 0], sumReactions: [0, 0, 10], balance: 0, balanced: true, unmapped: 0 },
    { id: 'q', code: 'Q-A', sumLoads: [0, 0, -5], sumReactions: null, balance: null, balanced: null, unmapped: 0 },
  ],
});

describe('the picker', () => {
  it('names what to ask for', () => {
    expect(pickArgs({ kind: 'case', key: 'c1' })).toEqual({ case: 'c1' });
    expect(pickArgs({ kind: 'combination', key: 'uls-1' })).toEqual({ combination: 'uls-1' });
    expect(pickArgs({ kind: 'envelope', key: 'sls' })).toEqual({ envelope: 'sls' });
  });
  it('round-trips its value', () => {
    for (const p of [{ kind: 'case', key: 'a:b' }, { kind: 'envelope', key: 'uls' }] as const) expect(pickFrom(pickValue(p))).toEqual(p);
    expect(pickFrom('envelope:other')).toBeNull();
    expect(pickFrom('nothing')).toBeNull();
  });
  it('starts on the ultimate envelope, else the first loaded case', () => {
    expect(defaultPick(run([{ key: 'u1', kind: 'uls', name: '1.35 G' }]))).toEqual({ kind: 'envelope', key: 'uls' });
    expect(defaultPick(run([{ key: 's1', kind: 'sls-characteristic', name: 'G + Q' }]))).toEqual({ kind: 'case', key: 'g' });
    expect(defaultPick({ ...run(), cases: [{ ...run().cases[0]!, sumLoads: [0, 0, 0] }, run().cases[1]!] })).toEqual({ kind: 'case', key: 'q' });
  });
  it('lists cases, combinations and the envelopes there are', () => {
    const c = pickChoices(run([{ key: 'u1', kind: 'uls', name: '1.35 G' }, { key: 's1', kind: 'sls-quasi', name: 'G + 0.3 Q' }]));
    expect(c.map((x) => x.group)).toEqual(['Load cases', 'Load cases', 'Combinations', 'Combinations', 'Envelopes', 'Envelopes']);
    expect(pickChoices(run()).filter((x) => x.group === 'Envelopes')).toEqual([]);
  });
  it('keeps a pick that still exists and replaces one that does not', () => {
    expect(validPick(run(), { kind: 'case', key: 'q' })).toEqual({ kind: 'case', key: 'q' });
    expect(validPick(run(), { kind: 'case', key: 'gone' })).toEqual({ kind: 'case', key: 'g' });
  });
});

describe('numbers', () => {
  it('writes forces to two decimals and small ones to two figures', () => {
    expect(formatValue(12.3456)).toBe('12.35');
    expect(formatValue(-0.01234)).toBe('-0.012');
    expect(formatValue(0)).toBe('0');
    expect(formatValue(-0)).toBe('0');
    expect(formatValue(NaN)).toBe('–');
  });
  it('writes displacements in millimetres', () => {
    expect(formatMm(0.0123)).toBe('12.3 mm');
    expect(formatMm(0.00042)).toBe('0.42 mm');
  });
  it('gives the span ratio of a deflection', () => {
    expect(spanRatio(6, 0.02)).toBe('L/300');
    expect(spanRatio(6, 0)).toBeNull();
  });
  it('says whether a case balances', () => {
    const r = run();
    expect(balanceText(r.cases[0]!)).toBe('balanced');
    expect(balanceText(r.cases[1]!)).toBe('–');
    expect(balanceText({ ...r.cases[0]!, balanced: false })).toBe('does not balance');
  });
});

describe('the diagram', () => {
  it('colours by sign', () => {
    expect(signColour(3)).toBe(SIGN_COLOUR.positive);
    expect(signColour(-3)).toBe(SIGN_COLOUR.negative);
    expect(signColour(1e-12)).toBe(SIGN_COLOUR.neutral);
  });
  it('drops a point onto its bar', () => {
    expect(footOnBase([[0, 0, 0], [4, 0, 0]], [1, 3, 2])).toEqual([1, 0, 0]);
    expect(footOnBase([[0, 0, 0], [4, 0, 0]], [9, 1, 1])).toEqual([4, 0, 0]);
    expect(footOnBase([[1, 1, 1], [1, 1, 1]], [3, 3, 3])).toEqual([1, 1, 1]);
  });
  it('puts a few hatch ticks between the ends, ends included', () => {
    expect(hatchIndices([0, 1])).toEqual([]);
    const t = hatchIndices(Array.from({ length: 21 }, (_, i) => i));
    expect(t[0]).toBe(0);
    expect(t[t.length - 1]).toBe(20);
    expect(t.length).toBeLessThanOrEqual(9);
  });
  it('labels the greatest value, and the least when it is not nothing', () => {
    expect(labelIndices([0, 5, 10, 5, 0])).toEqual([2]);
    expect(labelIndices([-8, 0, 10, 0, -8])).toEqual([2, 0]);
    expect(labelIndices([0, 0, 0])).toEqual([]);
  });
});

describe('reactions', () => {
  it('takes the larger of an envelope\'s two vectors', () => {
    expect(reactionVector({ node: 'n', at: [0, 0, 0], f: [1, 2, 3, 0, 0, 0] })).toEqual([1, 2, 3, 0, 0, 0]);
    expect(reactionVector({ node: 'n', at: [0, 0, 0], min: [0, 0, 5, 0, 0, 0], max: [0, 0, 9, 0, 0, 0] })).toEqual([0, 0, 9, 0, 0, 0]);
    expect(reactionVector({ node: 'n', at: [0, 0, 0] })).toBeNull();
  });
  it('labels the components that count', () => {
    expect(reactionLabel([0.1, 0, 34.5, 0, 0, 0], 34.5)).toBe('Fz 34.5');
    expect(reactionLabel([5, 0, 34.5, 0, 0, 0], 34.5)).toBe('Fx 5, Fz 34.5');
  });
});

describe('releases', () => {
  it('reads an end\'s preset', () => {
    expect(releasePreset(null)).toBe('default');
    expect(releasePreset(FIXED)).toBe('fixed');
    expect(releasePreset(PINNED)).toBe('pinned');
    expect(releasePreset([false, false, false, true, false, false])).toBe('custom');
  });
  it('gives setReleases what it takes', () => {
    expect(releaseValue('default', FIXED)).toBeNull();
    expect(releaseValue('fixed', null)).toBe('fixed');
    expect(releaseValue('pinned', null)).toBe('pinned');
    expect(releaseValue('custom', PINNED)).toEqual(PINNED);
    expect(releaseValue('custom', null)).toEqual([false, false, false, false, false, true]);
  });
  it('toggles one freedom', () => {
    expect(toggleRelease(FIXED, 3)).toEqual([false, false, false, true, false, false]);
  });
  it('reads what the element carries', () => {
    expect(ownReleases({ i: PINNED })).toEqual({ i: PINNED, j: null });
    expect(ownReleases({ i: [true] })).toEqual({ i: null, j: null });
    expect(ownReleases(undefined)).toEqual({ i: null, j: null });
  });
});

describe('placeLabels', () => {
  it('keeps the biggest of labels that overlap and every one that stands clear', () => {
    const kept = placeLabels([
      { x: 100, y: 100, text: '-11.08', weight: 11.08 },
      { x: 102, y: 106, text: '-39.03', weight: 39.03 },
      { x: 300, y: 100, text: '91.56', weight: 91.56 },
      { x: 100, y: 140, text: '21.1', weight: 21.1 },
    ]);
    expect(kept).toEqual([1, 2, 3]);
  });
  it('places a reaction (infinite weight) before any value', () => {
    expect(placeLabels([{ x: 0, y: 20, text: '999', weight: 999 }, { x: 2, y: 22, text: 'Fz 10', weight: Infinity }])).toEqual([1]);
  });
});
