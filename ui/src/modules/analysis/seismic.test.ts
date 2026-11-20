import { describe, expect, it } from 'vitest';
import {
  blend, formatDriftMm, formatHz, formatPercent, formatPeriod, modeRows, niceStep, oscillation, readNumber, reachedMass, restPoints, spectrumChart, storeyRows, ticks,
  type ModalDto, type SpectrumDto, type StoreyDto,
} from './seismic';
import { pickChoices, pickFrom, pickValue, pickArgs, validPick, type RunDto } from './results';

const modal: ModalDto = {
  ok: true, ms: 12, dofs: 100, supportMass: 0, reached: true, notes: [], totalMass: [100, 100, 100],
  modes: [
    { n: 1, omega: 12.5, f: 1.99, T: 0.5026, gamma: [1, 0, 0], mass: [60, 0, 0], ratio: [0.6, 0.001, 0], cumulative: [0.6, 0.001, 0], rayleigh: 1, residual: 0 },
    { n: 2, omega: 20, f: 3.18, T: 1.9876, gamma: [0, 1, 0], mass: [0, 35, 0], ratio: [0.05, 0.351, 0], cumulative: [0.65, 0.352, 0], rayleigh: 1, residual: 0 },
  ],
};

describe('the modes table', () => {
  it('writes each mode\'s period, frequency and effective masses', () => {
    expect(modeRows(modal)).toEqual([
      { n: 1, T: '0.503', f: '1.99', x: '60.0 %', y: '0.1 %', cumX: '60.0 %', cumY: '0.1 %' },
      { n: 2, T: '1.99', f: '3.18', x: '5.0 %', y: '35.1 %', cumX: '65.0 %', cumY: '35.2 %' },
    ]);
  });
  it('says where the modes stopped', () => {
    expect(reachedMass(modal)).toEqual([0.65, 0.352]);
    expect(reachedMass({ ...modal, modes: [] })).toEqual([0, 0]);
  });
  it('formats numbers, and a missing one as a dash', () => {
    expect(formatPercent(0.1234)).toBe('12.3 %');
    expect(formatPercent(NaN)).toBe('–');
    expect(formatPeriod(0.25)).toBe('0.250');
    expect(formatPeriod(2)).toBe('2.00');
    expect(formatHz(4)).toBe('4.00');
    expect(formatDriftMm(0.0123)).toBe('12.3');
    expect(formatDriftMm(0.00042)).toBe('0.42');
  });
});

const storey = (over: Partial<StoreyDto> = {}): StoreyDto => ({
  id: 's1', name: 'Ground floor', z: 0, h: 3, mass: 250.4, shear: [120.26, 80], force: [0, 0], drift: [0.002, 0.0015], driftMax: [0.0021, 0.0016],
  sls: [0.0015, 0.001], uls: [0.0045, 0.003], slsLimit: 0.015, ulsLimit: 0.075, slsOk: true, ulsOk: true, ...over,
});

describe('the storeys table', () => {
  it('writes shears, drifts and the checks against their limits', () => {
    const [r] = storeyRows([storey()]);
    expect(r).toMatchObject({ name: 'Ground floor', h: '3.00', mass: '250', shearX: '120', shearY: '80.0', driftX: '2.0', driftY: '1.5', sls: '1.5 · 1.0', slsOk: true, slsTitle: '15.0 mm', uls: '4.5 · 3.0', ulsOk: true, ulsTitle: '75.0 mm' });
  });
  it('has no ultimate check with EN 1998-1 (no limit), and keeps a failed one failed', () => {
    const [r] = storeyRows([storey({ ulsLimit: null, ulsOk: null, slsOk: false })]);
    expect(r!.uls).toBe('–');
    expect(r!.ulsOk).toBeNull();
    expect(r!.slsOk).toBe(false);
  });
});

describe('a number typed in a parameter field', () => {
  it('reads a comma or a dot within the range, else nothing', () => {
    expect(readNumber('0,25', 0.01, 1.5)).toBe(0.25);
    expect(readNumber(' 1.5 ', 0.01, 1.5)).toBe(1.5);
    expect(readNumber('2', 0.01, 1.5)).toBeNull();
    expect(readNumber('', 0.01, 1.5)).toBeNull();
    expect(readNumber('abc', 0.01, 1.5)).toBeNull();
  });
});

describe('the spectrum chart', () => {
  const spectrum: SpectrumDto = {
    code: 'P100-1/2013', ag: 2.94, S: 1, TB: 0.2, TC: 1, TD: 3, q: 1.5, gamma: 1, given: true, unit: 'm/s²',
    points: [{ T: 0, Sd: 2.94, Se: 2.94 }, { T: 0.2, Sd: 4.9, Se: 7.35 }, { T: 1, Sd: 4.9, Se: 7.35 }, { T: 3, Sd: 1.63, Se: 2.45 }, { T: 4, Sd: 0.6, Se: 1.4 }],
    modes: [{ n: 1, T: 0.5, Sd: 4.9 }, { n: 2, T: 0.12, Sd: 4 }],
  };
  it('nice steps and ticks reach the maximum', () => {
    expect(niceStep(7.35)).toBe(2);
    expect(ticks(7.35)).toEqual([0, 2, 4, 6, 8]);
    expect(ticks(4)).toEqual([0, 1, 2, 3, 4].slice(0, 5));
    expect(niceStep(0)).toBe(1);
  });
  it('puts the curves and the modes inside the plot, the higher the larger', () => {
    const c = spectrumChart(spectrum, 520, 240);
    expect(c.x(0)).toBe(c.left);
    expect(c.x(c.xMax)).toBe(520 - c.right);
    expect(c.y(0)).toBe(240 - c.bottom);
    expect(c.y(c.yMax)).toBe(c.top);
    expect(c.y(7.35)).toBeLessThan(c.y(2.94));
    expect(c.sd.startsWith('M')).toBe(true);
    expect(c.sd.match(/L/g)).toHaveLength(4);
    expect(c.modes.map((m) => m.n)).toEqual([1, 2]);
    for (const m of c.modes) { expect(m.cx).toBeGreaterThan(c.left); expect(m.cy).toBeLessThan(240 - c.bottom); }
    expect(c.corners.map((k) => k.name)).toEqual(['TB', 'TC', 'TD']);
    // The elastic curve passes above the design one at TC.
    expect(c.y(7.35)).toBeLessThan(c.y(4.9));
  });
  it('still draws when there is nothing but a point', () => {
    const c = spectrumChart({ ...spectrum, points: [{ T: 0, Sd: 0, Se: 0 }], modes: [] });
    expect(Number.isFinite(c.x(1))).toBe(true);
    expect(Number.isFinite(c.y(0))).toBe(true);
  });
});

describe('the mode shape\'s animation', () => {
  it('cuts the bar into equal pieces at rest', () => {
    expect(restPoints([[0, 0, 0], [4, 0, 2]], 3)).toEqual([0, 0, 0, 2, 0, 1, 4, 0, 2]);
  });
  it('swings between rest and displaced, and past it the other way', () => {
    const rest = [0, 0, 0, 1, 0, 0];
    const disp = [0, 0, 0, 1, 2, 0];
    expect(Array.from(blend(rest, disp, 0))).toEqual(rest);
    expect(Array.from(blend(rest, disp, 1))).toEqual(disp);
    expect(Array.from(blend(rest, disp, -1))).toEqual([0, 0, 0, 1, -2, 0]);
  });
  it('oscillates as sin(2πt/period)', () => {
    expect(oscillation(0)).toBe(0);
    expect(oscillation(400, 1600)).toBeCloseTo(1, 9);
    expect(oscillation(1200, 1600)).toBeCloseTo(-1, 9);
    expect(oscillation(1600, 1600)).toBeCloseTo(0, 9);
  });
});

const run = (): RunDto => ({
  ok: true, dofs: 12, elements: 1, nodes: 2, solveMs: 1, totalMs: 2, profile: 3, notes: [],
  combinations: [{ key: 'u1', kind: 'uls', name: '1.35 G' }],
  cases: [{ id: 'g', code: 'G1', sumLoads: [0, 0, -10], sumReactions: [0, 0, 10], balance: 0, balanced: true, unmapped: 0 }],
});

describe('the seismic envelope in the picker', () => {
  it('is offered only when the seismic parameters are given', () => {
    const labels = (s: boolean) => pickChoices(run(), s).filter((c) => c.group === 'Envelopes').map((c) => c.label);
    expect(labels(false)).toEqual(['Ultimate limit state (envelope)']);
    expect(labels(true)).toEqual(['Ultimate limit state (envelope)', 'Seismic combination']);
  });
  it('names it to the engine and round-trips its value', () => {
    const p = { kind: 'envelope', key: 'seismic' } as const;
    expect(pickArgs(p)).toEqual({ envelope: 'seismic' });
    expect(pickFrom(pickValue(p))).toEqual(p);
  });
  it('keeps the seismic pick while it is given, drops it when it is not', () => {
    const p = { kind: 'envelope', key: 'seismic' } as const;
    expect(validPick(run(), p, true)).toEqual(p);
    expect(validPick(run(), p, false)).toEqual({ kind: 'envelope', key: 'uls' });
  });
});
