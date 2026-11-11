import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { dataOf } from './JoineryData';

const type = (params: unknown[]): NodeDto => ({ id: 't', type: 'IfcWindowType', name: 'W', props: { params } } as unknown as NodeDto);

describe('a door or window type’s data (report #76)', () => {
  it('reads the fixed fields and the extras from the type’s parameters', () => {
    const t = type([
      { name: 'U-value', kind: 'text', value: '0,9 W/m²K', pset: 'Lapin_Joinery' },
      { name: 'FireRating', kind: 'text', value: 'EI 30', pset: 'Pset_WindowCommon' },
      { name: 'Sauna window', kind: 'text', value: 'yes', pset: 'Lapin_Joinery' },
      { name: 'Manufacturer', kind: 'text', value: 'ACME' },
    ]);
    const { fixed, extras } = dataOf(t);
    expect(fixed.uValue).toBe('0,9 W/m²K');
    expect(fixed.fireRating).toBe('EI 30');
    expect(fixed.gValue).toBe('');
    // Only the data’s own set; the type’s other parameters are not data.
    expect(extras).toEqual([{ name: 'Sauna window', value: 'yes' }]);
  });

  it('is empty for nothing', () => {
    expect(dataOf(undefined)).toEqual({ fixed: { uValue: '', gValue: '', safetyGlass: '', fireRating: '', fittings: '' }, extras: [] });
  });
});
