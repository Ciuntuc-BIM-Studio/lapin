import { describe, expect, it } from 'vitest';
import {
  barReinforcementText, filterRows, formatArea, formatUtilisation, groupBySource, ownDesign, parseDesignValue, projectText, projectValue, storeysOf, summary,
  surfaceRows, utilisationBySource, utilisationColour, valuesText, wallRows, wallSums,
  type CheckedDto, type ChecksDto, type SurfaceCheckedDto, type WallStoreyDto,
} from './checks';

const gov = (u: number, name = 'Bending', clause = 'EN 1993-1-1 6.2.5') => ({ key: 'b', name, clause, combination: 'c1', combinationName: 'ULS 1', utilisation: u });
const member = (id: string, sourceId: string, u: number | null, extra: Partial<CheckedDto> = {}): CheckedDto => ({
  id, sourceId, name: `M ${sourceId}`, material: 'steel', grade: 'S275', kind: 'beam', storeyId: 's1', utilisation: u, ok: u === null || u <= 1, done: u !== null, governing: u === null ? null : gov(u), ...extra,
});

describe('the utilisation as text and colour', () => {
  it('writes two decimals, a dash for nothing, and caps the absurd', () => {
    expect(formatUtilisation(0.847)).toBe('0.85');
    expect(formatUtilisation(1.2)).toBe('1.20');
    expect(formatUtilisation(null)).toBe('–');
    expect(formatUtilisation(Number.NaN)).toBe('–');
    expect(formatUtilisation(120)).toBe('>10');
  });
  it('goes from green at 0 toward orange at 1, red over 1, grey unchecked', () => {
    expect(utilisationColour(0)).toBe('hsl(120, 70%, 42%)');
    expect(utilisationColour(1)).toBe('hsl(35, 70%, 42%)');
    expect(utilisationColour(0.5)).toBe('hsl(78, 70%, 42%)');
    expect(utilisationColour(1.0001)).toBe('#d9202c');
    expect(utilisationColour(null)).toBe('#9aa0a6');
    expect(utilisationColour(-0.2)).toBe('hsl(120, 70%, 42%)');
  });
});

describe('the members by element', () => {
  it('takes the worst piece of an element and counts the pieces', () => {
    const rows = groupBySource([member('a#1', 'a', 0.4), member('a#2', 'a', 0.9, { governing: gov(0.9, 'Shear', '6.2.6') }), member('b#1', 'b', 1.2), member('c#1', 'c', null)]);
    expect(rows.map((r) => [r.sourceId, r.utilisation, r.pieces])).toEqual([['b', 1.2, 1], ['a', 0.9, 2], ['c', null, 1]]);
    expect(rows[1]!.governing?.name).toBe('Shear');
    expect(rows[2]!.governing).toBeNull();
  });
  it('is not ok when any piece is not', () => {
    const rows = groupBySource([member('a#1', 'a', 0.4), member('a#2', 'a', 1.3)]);
    expect(rows[0]!.ok).toBe(false);
  });
  it('filters by material and storey, and lists the storeys', () => {
    const rows = groupBySource([member('a#1', 'a', 0.4), member('b#1', 'b', 0.5, { material: 'timber', storeyId: 's2' })]);
    expect(filterRows(rows, 'timber', '').map((r) => r.sourceId)).toEqual(['b']);
    expect(filterRows(rows, '', 's1').map((r) => r.sourceId)).toEqual(['a']);
    expect(filterRows(rows, '', '')).toHaveLength(2);
    expect(storeysOf(rows, new Map([['s1', 'Ground']]))).toEqual([{ id: 's2', name: 's2' }, { id: 's1', name: 'Ground' }]);
  });
  it('counts the checked, the failing and the worst', () => {
    expect(summary([{ utilisation: 0.5 }, { utilisation: 1.1 }, { utilisation: null }])).toEqual({ checked: 2, over: 1, worst: 1.1 });
    expect(summary([])).toEqual({ checked: 0, over: 0, worst: null });
  });
  it('gives each element its worst utilisation for the colours, slabs and walls too', () => {
    const c: ChecksDto = { ok: true, indicative: true, members: [member('a#1', 'a', 0.3), member('a#2', 'a', 0.7)], surfaces: [{ ...member('s#1', 's', null) } as SurfaceCheckedDto], walls: [member('w#1', 'w', 1.4, { material: 'masonry' })], notes: [] };
    const by = utilisationBySource(c);
    expect(by.get('a')).toBe(0.7);
    expect(by.get('s')).toBeNull();
    expect(by.get('w')).toBe(1.4);
  });
});

describe('the slabs\' and walls\' reinforcement', () => {
  it('keeps the largest of each layer over an element\'s pieces', () => {
    const s = (id: string, r: number): SurfaceCheckedDto => ({ ...member(id, 'slab', 0.5, { kind: 'slab', material: 'concrete' }), reinforcement: { asxBottom: r, asyBottom: r / 2, asxTop: 0, asyTop: 1 } });
    const [row] = surfaceRows([s('a', 4), s('b', 6.5)]);
    expect(row!.layers).toEqual({ asxBottom: 6.5, asyBottom: 3.25, asxTop: 0, asyTop: 1 });
    expect(row!.surfaceKind).toBe('slab');
  });
  it('has no layers when the engine gave none', () => {
    expect(surfaceRows([member('a', 'w', 0.2, { kind: 'wall' }) as SurfaceCheckedDto])[0]!.layers).toBeNull();
  });
  it('writes areas with two decimals up to 10, then one', () => {
    expect(formatArea(3.456)).toBe('3.46');
    expect(formatArea(12.34)).toBe('12.3');
    expect(formatArea(undefined)).toBe('–');
  });
  it('lists a bar\'s required areas, the stirrups per metre', () => {
    expect(barReinforcementText({ bottom: 8.4, top: 2, stirrups: 3.1, unit: 'cm²' })).toEqual([
      { label: 'Bottom', value: '8.40 cm²' }, { label: 'Top', value: '2.00 cm²' }, { label: 'Stirrups', value: '3.10 cm²/m' },
    ]);
  });
});

const wall = (id: string, u: number, d = 'X') => ({
  id, sourceId: id, name: `Wall ${id}`, direction: d, length: 3, thickness: 0.25, height: 2.8, utilisation: u, governing: gov(u, 'Shear', 'CR 6 6.6'),
  N: { value: -250.46, combinationName: 'ULS 2' }, V: { value: 12.34, combination: 'c3' }, M: { value: 4.5, combinationName: 'ULS 1' }, NRd: 600, VRd: 80.25, MRd: 30, mOut: 1,
});
describe('the masonry walls\' table', () => {
  const storey: WallStoreyDto = {
    id: 's1', name: 'Ground', walls: [wall('a', 0.4), wall('b', 0.9, 'Y')],
    sums: { X: { walls: 1, V: 12.34, VRd: 80.25, area: 0.75 }, Y: { walls: 0, V: 0, VRd: 0, area: 0 } },
  };
  it('writes each wall in kN and m with the combinations for the hover, the worst first', () => {
    const rows = wallRows(storey);
    expect(rows.map((r) => r.id)).toEqual(['b', 'a']);
    expect(rows[1]).toMatchObject({ length: '3.00', thickness: '0.25', N: '-250.5', V: '12.3', M: '4.5', nTitle: 'ULS 2', vTitle: 'c3', mTitle: 'ULS 1', NRd: '600.0', VRd: '80.3', MRd: '30.0', governing: 'Shear (CR 6 6.6)' });
  });
  it('sums only the directions that have walls', () => {
    expect(wallSums(storey)).toEqual([{ direction: 'X', walls: 1, V: '12.3', VRd: '80.3', area: '0.75' }]);
  });
});

describe('an element\'s checks and design values', () => {
  it('writes a check\'s values on a line', () => {
    expect(valuesText([{ name: 'NEd', value: 120.456789, unit: 'kN' }, { name: 'λ', value: 0.82, unit: '' }])).toBe('NEd = 120.5 kN · λ = 0.82');
    expect(valuesText(undefined)).toBe('');
  });
  it('reads the own design values and drops what is not a number', () => {
    expect(ownDesign({ ky: 0.7, lcrZ: 'x', serviceClass: 2, system: 'end', junk: 1 })).toEqual({ ky: 0.7, kz: undefined, lcrY: undefined, lcrZ: undefined, llt: undefined, serviceClass: 2, system: 'end' });
    expect(ownDesign(null)).toEqual({});
  });
  it('reads a typed value: empty is the default, a comma is a point, words are refused', () => {
    expect(parseDesignValue('')).toBeNull();
    expect(parseDesignValue(' 0,7 ')).toBe(0.7);
    expect(parseDesignValue('abc')).toBeUndefined();
  });
  it('turns the project\'s values into the fields\' units and back', () => {
    expect(projectText(0.03, 1000)).toBe('30');
    expect(projectText(500e6, 1e-6)).toBe('500');
    expect(projectValue('30', 1000)).toBe(0.03);
    expect(projectValue('500', 1e-6)).toBe(500e6);
    expect(projectValue('', 1000)).toBeNull();
    expect(projectValue('x', 1000)).toBeUndefined();
    expect(projectText(undefined, 1000)).toBe('');
  });
});
