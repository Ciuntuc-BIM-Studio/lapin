import { describe, expect, it } from 'vitest';
import { DEFAULT_RULE, effectiveBearing, legacyProducts, withBeamType, type LintelType } from './lintels';

const kp7: LintelType = { id: 't1', name: 'KP 7', width: 0.07, height: 0.238, lengths: [1.25, 1.5], code: 'KP7', manufacturer: 'Heluz', bearing: 0.125 };

describe('a beam type as a lintel (report #135)', () => {
  it('keeps the type and a copy of its section as the product', () => {
    const r = withBeamType(DEFAULT_RULE, kp7);
    expect(r.beamType).toBe('t1');
    expect(r.product).toEqual({ code: 'KP 7', width: 0.07, height: 0.238, lengths: [1.25, 1.5], manufacturer: 'Heluz' });
    expect(withBeamType(r, null)).toMatchObject({ beamType: null, product: null, ownBearing: null });
  });
  it('bears as the type says unless the rule has its own bearing', () => {
    const r = withBeamType(DEFAULT_RULE, kp7);
    expect(effectiveBearing(r, [kp7])).toBe(0.125);
    expect(effectiveBearing({ ...r, ownBearing: true, bearing: 0.2 }, [kp7])).toBe(0.2);
    expect(effectiveBearing(DEFAULT_RULE, [kp7])).toBe(0.25);
    expect(effectiveBearing(r, [])).toBe(0.25);
  });
  it('lists an older product only when no beam type has its code', () => {
    const products = [{ code: 'kp7', width: 0.07, height: 0.238, lengths: [1] }, { code: 'L2', width: 0.1, height: 0.1, lengths: [1] }];
    expect(legacyProducts(products, [kp7]).map((p) => p.code)).toEqual(['L2']);
  });
});
