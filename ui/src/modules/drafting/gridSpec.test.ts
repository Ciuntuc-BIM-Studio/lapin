import { describe, expect, it } from 'vitest';
import { useStandards } from '../../engine/unitsStore';
import { gridLines, readSpacings } from './gridSpec';

describe('the Grid dialog (report #68)', () => {
  it('reads spacings in the project\'s units, a count times a length repeating it', () => {
    useStandards.setState({ units: 'mm' });
    expect(readSpacings('6000 6000 4500')).toEqual([6, 6, 4.5]);
    expect(readSpacings('3*6000; 4500')).toEqual([6, 6, 6, 4.5]);
    expect(readSpacings('')).toEqual([]);
    expect(readSpacings('6000 abc')).toBeNull();
    useStandards.setState({ units: 'ft-in' });
    const ft = readSpacings('20\'; 2x18\'')!;
    expect(ft.map((v) => Math.round(v * 10000) / 10000)).toEqual([6.096, 5.4864, 5.4864]);
    useStandards.setState({ units: 'mm' });
  });
  it('lays the axes as the engine does', () => {
    const lines = gridLines({ x: [6, 6], y: [5], extend: 1, angle: 0, origin: { x: 0, y: 0 } });
    expect(lines.filter((l) => l.across).map((l) => l.a.x)).toEqual([0, 6, 12]);
    expect(lines.filter((l) => !l.across)).toHaveLength(2);
    expect(lines[0].a.y).toBe(-1);
    expect(lines[0].b.y).toBe(6);
  });
});
