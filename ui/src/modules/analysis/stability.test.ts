import { describe, expect, it } from 'vitest';
import {
  actsIn, classTone, formatAlpha, formatAmplifier, formatPhi, hasPurlinMode, imperfectionsText, variantLabel, variantsTitle, formatSoilRange, formatStiffness, globalText, modeCells, onlyArg, ownCompressionOnly, ownSprings, ownWinkler,
  parseSpring, parseWinkler, slackNames, withSpring, type BucklingComboDto,
} from './stability';
import { orderNotes } from './results';

const combo = (over: Partial<BucklingComboDto> = {}): BucklingComboDto => ({
  key: 'c1', name: 'ULS 1', ok: true, alpha: [4.2, 6.125, 12.4], modes: [
    { alpha: 4.2, local: { sourceId: 's1', name: 'Column 3', share: 0.8 } }, { alpha: 6.125, local: null }, { alpha: 12.4, local: null },
  ], global: 6.125, globalAbove: null, class: 'amplify', amplifier: 1.3, compressed: true, converged: true, message: null, ...over,
});

describe('the stability section\'s helpers', () => {
  it('colours the class as EN 1993-1-1 5.2.1(3) reads it', () => {
    expect(classTone('first-order')).toBe('ok');
    expect(classTone('amplify')).toBe('warn');
    expect(classTone('second-order')).toBe('bad');
    expect(classTone('unstable')).toBe('bad');
    expect(classTone('none')).toBe('none');
    expect(classTone(null)).toBe('none');
  });
  it('writes αcr with few digits', () => {
    expect(formatAlpha(6.12345)).toBe('6.12');
    expect(formatAlpha(23.456)).toBe('23.5');
    expect(formatAlpha(1234.5)).toBe('1235');
    expect(formatAlpha(null)).toBe('–');
    expect(formatAmplifier(1.2987)).toBe('× 1.3');
  });
  it('gives the global αcr, or a lower bound when every mode is one member\'s own', () => {
    expect(globalText(combo())).toBe('6.13');
    expect(globalText(combo({ global: null, globalAbove: 14.2 }))).toBe('≥ 14.2');
    expect(globalText(combo({ global: null, globalAbove: null }))).toBe('–');
  });
  it('marks the local modes with their member', () => {
    const cells = modeCells(combo());
    expect(cells[0]).toEqual({ alpha: '4.2', local: 'Column 3', share: 0.8 });
    expect(cells[1]!.local).toBeNull();
    expect(modeCells(combo({ alpha: [3], modes: [{ alpha: 3, local: null }] }))[2]!.alpha).toBe('–');
  });
  it('lists the slack members once', () => {
    expect(slackNames({ slack: [{ id: 'a', sourceId: 'x', name: 'Brace 1' }, { id: 'b', sourceId: 'x', name: 'Brace 1' }, { id: 'c', sourceId: 'y', name: '' }] })).toEqual(['Brace 1', 'y']);
  });
});

describe('the supports\' properties', () => {
  it('reads the attribute and the typed values', () => {
    expect(ownSprings({ springs: [1, null, 3, null, null, 0] })).toEqual([1, null, 3, null, null, 0]);
    expect(ownSprings(undefined)).toEqual([null, null, null, null, null, null]);
    expect(ownWinkler({ winkler: 25000 })).toBe(25000);
    expect(ownWinkler({})).toBeNull();
    expect(ownCompressionOnly({ compressionOnly: true })).toBe(true);
    expect(parseSpring('')).toBeNull();
    expect(parseSpring('0')).toBe(0);
    expect(parseSpring('1,5e4')).toBe(15000);
    expect(parseSpring('-1')).toBeUndefined();
    expect(parseSpring('abc')).toBeUndefined();
    expect(parseWinkler('0')).toBeUndefined();
    expect(parseWinkler('30000')).toBe(30000);
    expect(parseWinkler(' ')).toBeNull();
  });
  it('removes the springs when none is left', () => {
    expect(withSpring([null, null, 5, null, null, null], 2, null)).toBeNull();
    expect(withSpring([null, null, null, null, null, null], 0, 100)).toEqual([100, null, null, null, null, null]);
  });
  it('formats stiffnesses and soils', () => {
    expect(formatStiffness(12345.678)).toBe('12345.68');
    expect(formatStiffness(2.5e9)).toBe('2.50e9');
    expect(formatStiffness(0)).toBe('0');
    expect(formatSoilRange(5000, 15000)).toBe('5–15 MN/m³');
  });
  it('maps the bar\'s direction', () => {
    expect(actsIn('tension')).toBe('tension');
    expect(actsIn(undefined)).toBe('both');
    expect(onlyArg('both')).toBeNull();
    expect(onlyArg('compression')).toBe('compression');
  });
});

describe('the S5 notes', () => {
  it('flag the ones to check', () => {
    const { warnings } = orderNotes([
      '„ULS 1”: Second order: unstable, the loads are past the critical ones (αcr < 1). Left out of the envelopes and the checks.',
      '„ULS 2”: Not settled in 30 iterations. Its last iteration is taken.',
      'EN 1993-1-1 5.2.1(3): αcr = 4.2 under „ULS 1”, below 10: second-order effects must be taken (analysis.setOptions secondOrder, or the sway effects × 1/(1 − 1/αcr) when αcr ≥ 3); analysis.buckling gives every combination\'s.',
      'Second order amplifies the sway most in „ULS 1”: × 1.4.',
      'Supports lifted in 1 combination(s): 2 node(s).',
    ]);
    expect(warnings).toBe(3);
  });
});

describe('the global imperfections', () => {
  it('writes φ as 1/N, and a dash where there is no column', () => {
    expect(formatPhi(1 / 231.4)).toBe('1/231');
    expect(formatPhi(0.005)).toBe('1/200');
    expect(formatPhi(0)).toBe('–');
    expect(formatPhi(null)).toBe('–');
  });
  it('gives φ and m along X and Y with h and αh, with no m for timber', () => {
    expect(imperfectionsText({ x: { phi: 1 / 231, m: 4 }, y: { phi: 1 / 200, m: 0 }, h: 6.5, alphaH: 0.7845 })).toBe('φ X 1/231 (m = 4) · Y 1/200 · h = 6.5 m · αh = 0.785');
    expect(imperfectionsText({ x: { phi: 0, m: 0 }, y: { phi: 0.004, m: 2 }, h: 3, alphaH: 1 })).toBe('φ X – · Y 1/250 (m = 2) · h = 3 m · αh = 1');
  });
  it('names the governing variant with the typographic minus, and lists the four amplifications', () => {
    expect(variantLabel('+X')).toBe('φ(+X)');
    expect(variantLabel('-Y')).toBe('φ(−Y)');
    expect(variantLabel(undefined)).toBe('–');
    const v = (variant: '+X' | '-X', amplification: number | null) => ({ variant, key: '', name: '', ok: true, iterations: 1, converged: true, amplification, phi: 0, m: 0, force: 0, neglectable: false, message: null });
    expect(variantsTitle({ variants: [v('+X', 1.2), v('-X', 1.1)] })).toBe('φ(+X) × 1.2 · φ(−X) × 1.1');
    expect(variantsTitle({ variants: [v('+X', null)] })).toBe('φ(+X)');
    expect(variantsTitle({})).toBeUndefined();
  });
});

describe('the sheeting', () => {
  it('offers itself when a mode is a roof purlin\'s own', () => {
    expect(hasPurlinMode([combo()])).toBe(false);
    expect(hasPurlinMode([combo({ modes: [{ alpha: 3, local: { sourceId: 'p1', name: 'Purlin', share: 0.9, purlin: true } }] })])).toBe(true);
  });
});
