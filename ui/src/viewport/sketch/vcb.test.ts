import { describe, expect, it } from 'vitest';
import { parseVcb } from './vcb';

// The project in millimetres: a plain number is mm.
const mm = (s: string) => { const m = /^(-?\d+(?:\.\d+)?)\s*(mm|m)?$/.exec(s.trim()); return m ? Number(m[1]) / (m[2] === 'm' ? 1 : 1000) : null; };

describe('the value typed while sketching (decision 374)', () => {
  it('reads a length, a pair, a radius and angles', () => {
    expect(parseVcb('1200', mm)).toEqual({ kind: 'length', value: 1.2 });
    expect(parseVcb('2m', mm)).toEqual({ kind: 'length', value: 2 });
    expect(parseVcb('1200,600', mm)).toEqual({ kind: 'pair', a: 1.2, b: 0.6 });
    expect(parseVcb('1200, 600', mm)).toEqual({ kind: 'pair', a: 1.2, b: 0.6 });
    expect(parseVcb('1200x600', mm)).toEqual({ kind: 'pair', a: 1.2, b: 0.6 });
    expect(parseVcb('1200;600', mm)).toEqual({ kind: 'pair', a: 1.2, b: 0.6 });
    expect(parseVcb('r300', mm)).toEqual({ kind: 'radius', value: 0.3 });
    expect(parseVcb('R 300', mm)).toEqual({ kind: 'radius', value: 0.3 });
    expect(parseVcb('<30', mm)).toEqual({ kind: 'angle', deg: 30 });
    expect(parseVcb('1000<45', mm)).toEqual({ kind: 'lengthAngle', value: 1, deg: 45 });
  });
  it('means nothing while empty or half typed', () => {
    expect(parseVcb('', mm)).toBeNull();
    expect(parseVcb('r', mm)).toBeNull();
    expect(parseVcb('1200,', mm)).toEqual({ kind: 'length', value: 1.2 });
    expect(parseVcb('abc', mm)).toBeNull();
    expect(parseVcb('0', mm)).toBeNull();
    expect(parseVcb('1,2,3', mm)).toBeNull();
  });
});
