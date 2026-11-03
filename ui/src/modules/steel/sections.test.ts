import { describe, expect, it } from 'vitest';
import { centreline, kgPerMetre, sectionArea, sectionLabel, sectionPath, sectionShape, signedArea } from './sections';

describe('steel sections drawn small', () => {
  it('a hot-rolled I has its flanges and web, as the engine builds it', () => {
    const ipe300 = { kind: 'I', width: 0.15, depth: 0.3, web: 0.0071, flange: 0.0107 };
    expect(sectionArea(ipe300)).toBeCloseTo(2 * 0.15 * 0.0107 + 0.0071 * (0.3 - 2 * 0.0107), 12);
    expect(signedArea(sectionShape(ipe300).outline)).toBeGreaterThan(0);
  });

  it('hollow sections are open inside, a CHS measured exactly', () => {
    const rhs = { kind: 'RHS', width: 0.1, depth: 0.15, web: 0.006 };
    expect(sectionArea(rhs)).toBeCloseTo(0.1 * 0.15 - 0.088 * 0.138, 12);
    expect(signedArea(sectionShape(rhs).holes[0])).toBeLessThan(0);
    const chs = { kind: 'CHS', width: 0.1143, depth: 0.1143, web: 0.005 };
    expect(sectionArea(chs)).toBeCloseTo((Math.PI / 4) * (0.1143 ** 2 - 0.1043 ** 2), 12);
  });

  it('a cold-formed sheet is its middle line\'s length times its thickness', () => {
    for (const kind of ['C', 'Z', 'SIGMA']) {
      const s = { kind, width: kind === 'Z' ? 0.13 : 0.065, depth: 0.2, web: 0.002, flange: 0.002, lip: 0.02 };
      const line = centreline(s);
      const length = line.slice(1).reduce((a, p, i) => a + Math.hypot(p.x - line[i].x, p.y - line[i].y), 0);
      expect(sectionArea(s)).toBeCloseTo(length * 0.002, 12);
    }
    // A C 200×65×2 with 20 mm lips: 5.7 kg/m.
    expect(kgPerMetre({ kind: 'C', width: 0.065, depth: 0.2, web: 0.002, lip: 0.02 })).toBeCloseTo(5.68, 2);
  });

  it('says a section by its name or its size, and draws it in its box', () => {
    expect(sectionLabel({ kind: 'I', width: 0.15, depth: 0.3, name: 'IPE 300' })).toBe('IPE 300');
    expect(sectionLabel({ kind: 'RHS', width: 0.1, depth: 0.15, web: 0.006 })).toBe('RHS 150×100×6');
    expect(sectionLabel({ kind: 'SIGMA', width: 0.062, depth: 0.2, web: 0.002 })).toBe('Σ 200×62×2');
    expect(sectionLabel(undefined)).toBe('—');
    const path = sectionPath({ kind: 'RHS', width: 0.1, depth: 0.1, web: 0.005 }, 40);
    expect(path.match(/M/g)).toHaveLength(2);
    for (const n of path.match(/-?\d+\.\d+/g)!.map(Number)) { expect(n).toBeGreaterThanOrEqual(2 - 1e-9); expect(n).toBeLessThanOrEqual(38 + 1e-9); }
  });
});
