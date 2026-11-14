import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../../engine/protocol';
import { carrierEnd, fillText, sizeLabel, step45, typeArg } from './carriers';

const carrier = (id: string, points: number[][], storeyId = 's1'): NodeDto =>
  ({ id, type: 'IfcCableCarrierSegment', name: id, parentId: null, px: 0, py: 0, props: { points }, editable: true, module: 'mep', storeyId }) as unknown as NodeDto;

describe('a cable carrier drawn', () => {
  it('lays each leg at the nearest 45° step from the last point', () => {
    expect(step45([{ x: 0, y: 0 }], { x: 3, y: 0.4 })).toEqual({ x: 3, y: 0 });
    const d = step45([{ x: 1, y: 1 }], { x: 3, y: 3.3 });
    expect(d.x - 1).toBeCloseTo(d.y - 1, 6);
    expect(step45([{ x: 0, y: 0 }], { x: -0.2, y: 2 })).toEqual({ x: 0, y: 2 });
    expect(step45([], { x: 2, y: 1 })).toEqual({ x: 2, y: 1 });
  });

  it('joins the carrier its end lands on, on its own storey', () => {
    const cs = [carrier('a', [[0, 0], [4, 0]]), carrier('b', [[0, 1], [4, 1]], 's2')];
    expect(carrierEnd(cs, { x: 2, y: 0.1 }, 0.3, 's1')).toEqual({ carrierId: 'a', at: [2, 0.1] });
    expect(carrierEnd(cs, { x: 2, y: 0.9 }, 0.3, 's1')).toBeUndefined();
    expect(carrierEnd(cs, { x: 2, y: 0.9 }, 0.3, 's2')?.carrierId).toBe('b');
  });

  it('names its type, size and fill', () => {
    expect(typeArg('preset:trunking40x40')).toEqual({ preset: 'trunking40x40' });
    expect(typeArg('abc')).toEqual({ typeId: 'abc' });
    expect(sizeLabel('perforated', 0.2, 0.06, 0)).toBe('200×60');
    expect(sizeLabel('conduit', 0, 0, 0.02)).toBe('Ø20');
    expect(fillText(null)).toBe('no cables');
    expect(fillText({ percent: 38.24, limit: 45, over: false, count: 3, cableIds: [], size: '40×40', kind: 'trunking' })).toBe('38.2 % of 45 % · 3 cables');
  });
});
