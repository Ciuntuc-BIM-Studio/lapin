import { describe, expect, it } from 'vitest';
import {
  addCarcass, addZone, cutListRows, fitWidth, formatRatios, laidWidths, moveCarcass, paramApplies, parseRatios, removeCarcass, removeZone, setCarcassField,
  setZoneField, splitCarcass, zoneFieldsFor, type BoardDto, type Carcass,
} from './casework';
import type { UnitField } from './kitchenUnits';

const fields: UnitField[] = [
  { key: 'front', label: 'Front', kind: 'choice', choices: ['open', 'doors', 'drawers', 'flap'] },
  { key: 'height', label: 'Height', kind: 'length', min: 0.05, max: 3 },
  { key: 'doors', label: 'Doors', kind: 'number', min: 1, max: 4, when: { key: 'front', values: ['doors'] } },
  { key: 'hand', label: 'Hinged on the', kind: 'choice', choices: ['left', 'right'], when: { key: 'front', values: ['doors'] } },
  { key: 'drawers', label: 'Drawers', kind: 'number', min: 1, max: 6, when: { key: 'front', values: ['drawers'] } },
  { key: 'ratios', label: 'Heights by ratio', kind: 'ratios' as UnitField['kind'], when: { key: 'front', values: ['drawers'] } },
  { key: 'shelves', label: 'Shelves', kind: 'number', min: 0, max: 12, when: { key: 'front', values: ['open', 'doors', 'flap'] } },
];

const three = (): Carcass[] => [{ zones: [{ front: 'doors' }] }, { width: 0.4, zones: [{ front: 'drawers', drawers: 3 }] }, { zones: [{ front: 'open' }] }];

describe('casework carcasses', () => {
  it('lay as the engine does: the given widths, the others sharing the rest', () => {
    expect(laidWidths(three(), 1.8)).toEqual([0.7, 0.4, 0.7]);
    expect(laidWidths([{ width: 1 }, {}], 1.05)).toEqual([1, 0.1]);
  });

  it('fit the width to their own when they set it', () => {
    expect(fitWidth(three(), 1.8)).toBeNull();
    expect(fitWidth([{ width: 0.6 }, { width: 0.45 }], 1.8)).toBe(1.05);
    expect(fitWidth([{ width: 0.6 }, { width: 0.45 }], 1.05)).toBeNull();
  });

  it('are added, taken out, moved and split', () => {
    expect(addCarcass(three()).length).toBe(4);
    expect(addCarcass(three(), 0)[1]).toEqual({ zones: [{ front: 'doors' }] });
    expect(removeCarcass(three(), 1).map((c) => c.width)).toEqual([undefined, undefined]);
    expect(moveCarcass(three(), 1, -1)[0].width).toBe(0.4);
    expect(moveCarcass(three(), 0, -1)).toEqual(three());
    const split = splitCarcass(three(), 1);
    expect(split.length).toBe(4);
    expect(split[1].width).toBe(0.2);
    expect(split[2].zones).toEqual([{ front: 'drawers', drawers: 3 }]);
    expect(split[1].zones).not.toBe(split[2].zones);
  });

  it('becomes a knee space without zones, and a box again with a door', () => {
    const gap = setCarcassField(three(), 0, 'kind', 'gap');
    expect(gap[0]).toEqual({ kind: 'gap' });
    expect(setCarcassField(gap, 0, 'kind', 'box')[0]).toEqual({ zones: [{ front: 'doors' }] });
    expect(setCarcassField(three(), 1, 'width', undefined)[1].width).toBeUndefined();
  });
});

describe('casework zones', () => {
  it('show the fields of their front', () => {
    expect(zoneFieldsFor({ front: 'drawers' }, fields).map((f) => f.key)).toEqual(['front', 'height', 'drawers', 'ratios']);
    expect(zoneFieldsFor({ front: 'doors' }, fields).map((f) => f.key)).toEqual(['front', 'height', 'doors', 'hand', 'shelves']);
  });

  it('drop what no longer applies when the front changes, and ratios that no longer match', () => {
    const doors = setZoneField(three(), 0, 0, 'hand', 'right', fields);
    expect(doors[0].zones![0]).toEqual({ front: 'doors', hand: 'right' });
    expect(setZoneField(doors, 0, 0, 'front', 'drawers', fields)[0].zones![0]).toEqual({ front: 'drawers' });
    const ratios = setZoneField(three(), 1, 0, 'ratios', [1, 1, 2], fields);
    expect(ratios[1].zones![0].ratios).toEqual([1, 1, 2]);
    expect(setZoneField(ratios, 1, 0, 'drawers', 2, fields)[1].zones![0].ratios).toBeUndefined();
  });

  it('are added over the top one and taken out down to one', () => {
    const two = addZone(three(), 0);
    expect(two[0].zones).toEqual([{ front: 'doors' }, { front: 'open', shelves: 1 }]);
    expect(removeZone(two, 0, 0)[0].zones).toEqual([{ front: 'open', shelves: 1 }]);
    expect(removeZone(three(), 0, 0)).toEqual(three());
    expect(addZone([{ kind: 'gap' }], 0)).toEqual([{ kind: 'gap' }]);
  });

  it('read drawer ratios typed as 1:1:2', () => {
    expect(parseRatios('1:1:2')).toEqual([1, 1, 2]);
    expect(parseRatios('1, 1,5 2')).toEqual([1, 1, 5, 2]);
    expect(parseRatios('1:0')).toBeNull();
    expect(parseRatios('')).toBeNull();
    expect(formatRatios([1, 1.5])).toBe('1:1.5');
  });
});

describe('casework parameters and the cut list', () => {
  it('show a parameter only where it applies', () => {
    expect(paramApplies('legHeight', { base: 'legs' }, {})).toBe(true);
    expect(paramApplies('legHeight', { base: 'plinth' }, {})).toBe(false);
    expect(paramApplies('mount', {}, { base: 'floating' })).toBe(true);
    expect(paramApplies('overhang', { top: 'none' }, {})).toBe(false);
  });

  it('groups the boards by part and size, the fronts last', () => {
    const b = (part: string, carcass: number, front = false): BoardDto => ({ part, carcass, role: front ? 'fronts' : 'body', length: 0.7, width: 0.432, thickness: 0.018, grain: 'z', front });
    const rows = cutListRows([b('Door', 1, true), b('Side', 1), b('Side', 1), b('Side', 2)]);
    expect(rows.map((r) => [r.part, r.carcass, r.count])).toEqual([['Side', 1, 2], ['Side', 2, 1], ['Door', 1, 1]]);
    expect(rows[0].size).toBe('700×432×18');
  });
});
