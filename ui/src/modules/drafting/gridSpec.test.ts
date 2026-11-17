import { describe, expect, it } from 'vitest';
import { useStandards } from '../../engine/unitsStore';
import { firstForCounting, gridLines, labelSample, nextLabel, readSpacings, runUpThePlan } from './gridSpec';

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
    // Issue #83: single spans and N*span mixed.
    expect(readSpacings('100 2*300 200')).toEqual([0.1, 0.3, 0.3, 0.2]);
  });
  it('shows each family\'s labels between its prefix and suffix (issue #83)', () => {
    expect(labelSample('1', 5, 'X-', '\'')).toBe('X-1\' X-2\' … X-5\'');
    expect(labelSample('A', 3, '', '1')).toBe('A1 B1 C1');
    expect(labelSample('Z', 2)).toBe('Z AA');
    expect(labelSample('10', 3, 'N', '', true)).toBe('N10 …');
    expect(labelSample('', 1)).toBe('1');
  });
  it('lays the axes as the engine does', () => {
    const lines = gridLines({ x: [6, 6], y: [5], extend: 1, angle: 0, origin: { x: 0, y: 0 } });
    expect(lines.filter((l) => l.across).map((l) => l.a.x)).toEqual([0, 6, 12]);
    expect(lines.filter((l) => !l.across)).toHaveLength(2);
    expect(lines[0].a.y).toBe(-1);
    expect(lines[0].b.y).toBe(6);
  });
});

describe('Renumber axes (forum #75)', () => {
  it('follows the selected axes to their new tags and reads which axes are selected', async () => {
    const { renumberedSelection, selectedAxes } = await import('./gridSpec');
    expect(renumberedSelection(['g#7', 'g#3', 'wall'], 'g', { '7': '1', '3': '2' })).toEqual(['g#1', 'g#2', 'wall']);
    expect(selectedAxes(['g#1', 'g#B'])).toEqual({ gridId: 'g', tags: ['1', 'B'] });
    expect(selectedAxes(['wall'])).toBeNull();
    expect(selectedAxes(['g#1', 'h#1'])).toBeNull();
  });
});

describe('axes labelled either way, counting up or down (issue #64)', () => {
  it('counts labels as the engine does', () => {
    expect(['1', '9', 'A', 'Z', 'AZ', 'A1', 'b'].map(nextLabel)).toEqual(['2', '10', 'B', 'AA', 'BA', 'A2', 'c']);
  });
  it('turns a default first label into the one that ends the family on it, and back', () => {
    expect(firstForCounting('1', '1', 4, true)).toBe('4');
    expect(firstForCounting('A', 'A', 3, true)).toBe('C');
    expect(firstForCounting('4', '1', 4, false)).toBe('1');
    expect(firstForCounting('12', '1', 4, true)).toBe('12');
    expect(firstForCounting('12', '1', 4, false)).toBe('12');
  });
  it('tells the selected axes\' direction', () => {
    const axes = [{ tag: '1', a: { x: 0, y: 0 }, b: { x: 0, y: 10 } }, { tag: 'A', a: { x: 0, y: 0 }, b: { x: 10, y: 0 } }];
    expect(runUpThePlan(axes, ['1'])).toBe(true);
    expect(runUpThePlan(axes, ['A'])).toBe(false);
    expect(runUpThePlan(axes, ['Q'])).toBeNull();
  });
});
