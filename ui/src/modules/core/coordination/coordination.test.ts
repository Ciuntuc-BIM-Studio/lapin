import { describe, expect, it } from 'vitest';
import { clashCount, metresToMm, mmToMetres, pairOf, shortBy, shown, sizeOf, toggleKind, voidsOfKinds, type ClashCheck, type ClashResult, type VoidDto } from './coordination';

const r = (status: ClashResult['status'], by = 0, more: Partial<ClashResult> = {}): ClashResult =>
  ({ key: `${status}-${by}-${more.mepId ?? ''}`, status, by, hostId: 'wall', mepId: 'pipe', centre: [0, 0, 0], message: '', ...more });

describe('openings vs MEP', () => {
  it('reads millimetres typed as metres, with a comma too, and refuses what is not a number or out of range', () => {
    expect(mmToMetres('25')).toBe(0.025);
    expect(mmToMetres('12,5')).toBe(0.0125);
    expect(mmToMetres('-10')).toBe(-0.01);
    expect(mmToMetres('-10', 0)).toBeNull();
    expect(mmToMetres('abc')).toBeNull();
    expect(mmToMetres('')).toBeNull();
    expect(mmToMetres('600')).toBeNull();
    expect(metresToMm(0.025)).toBe('25');
    expect(metresToMm(0)).toBe('0');
  });

  it('says how much too small an opening is in whole millimetres, rounded up, and nothing when it fits', () => {
    expect(shortBy(r('tooSmall', 0.025))).toBe('25');
    expect(shortBy(r('tooSmall', 0.0100000001))).toBe('10');
    expect(shortBy(r('tooSmall', 0.0101))).toBe('11');
    expect(shortBy(r('tooSmall', 0.0002))).toBe('1');
    expect(shortBy(r('ok'))).toBe('');
    expect(shortBy(r('missing'))).toBe('');
  });

  it('lists what needs an opening first, what fits only when asked', () => {
    const all = [r('ok', 0, { mepId: 'a' }), r('tooSmall', 0.01, { mepId: 'b' }), r('empty', 0, { mepId: null }), r('missing', 0, { mepId: 'c' }), r('missing', 0, { mepId: 'd' })];
    expect(shown(all, false).map((x) => x.status)).toEqual(['missing', 'missing', 'tooSmall', 'empty']);
    // The engine's order kept within a status.
    expect(shown(all, false).slice(0, 2).map((x) => x.mepId)).toEqual(['c', 'd']);
    expect(shown(all, true).map((x) => x.status)).toEqual(['missing', 'missing', 'tooSmall', 'empty', 'ok']);
    const check: ClashCheck = { results: all, counts: { missing: 2, tooSmall: 1, ok: 1, empty: 1 }, available: true };
    expect(clashCount(check)).toBe(3);
    expect(clashCount(null)).toBe(0);
  });

  it('selects the element and its host, and the opening it goes through', () => {
    expect(pairOf(r('missing', 0, { openingId: null }))).toEqual(['pipe', 'wall']);
    expect(pairOf(r('tooSmall', 0.01, { openingId: 'op' }))).toEqual(['pipe', 'wall', 'op']);
    expect(pairOf(r('empty', 0, { mepId: null, openingId: 'op' }))).toEqual(['wall', 'op']);
  });

  it('reads a void as width × height, or its diameter, in millimetres', () => {
    expect(sizeOf({ width: 0.4, height: 0.3, depth: 0.25 })).toBe('400 × 300');
    expect(sizeOf({ width: 0.2, height: 0.2, depth: 0.2, diameter: 0.2 })).toBe('Ø200');
  });

  it('toggles a kind keeping the kinds in their order, and filters the voids by kind', () => {
    expect(toggleKind(['wall', 'slab'], 'door')).toEqual(['wall', 'door', 'slab']);
    expect(toggleKind(['wall', 'door', 'slab'], 'door')).toEqual(['wall', 'slab']);
    const v = (kind: VoidDto['kind']): VoidDto => ({ key: kind, mark: 'V1', kind, shape: 'Rectangle', hostId: 'h', selectId: 'h', globalId: 'g', width: 1, height: 1, depth: 1 });
    expect(voidsOfKinds([v('wall'), v('door'), v('roof')], ['door', 'roof']).map((x) => x.kind)).toEqual(['door', 'roof']);
  });
});
