import { describe, expect, it } from 'vitest';
import {
  breakdownSummary, byProductText, crossTasks, defaultLocations, locationGroups, productCodes, productLabel, productOptions, setProduct, toggleLocation, togglePicked,
  type LocationOption, type PbsItem,
} from './breakdown';
import { ruleSummary } from './links';

const items: PbsItem[] = [
  { code: '40', title: 'Interior', depth: 0, count: 6 },
  { code: '40.10', title: 'Partition walls', parent: '40', depth: 1, count: 3 },
  { code: '40.20', title: 'Doors', parent: '40', depth: 1, count: 3 },
];

const locations: LocationOption[] = [
  { id: 'b', name: 'Building', level: 'building', count: 7 },
  { id: 's1', name: 'Ground floor', level: 'storey', count: 5 },
  { id: 'r1', name: '1.01 Hall', level: 'room', storey: 's1' },
  { id: 's2', name: 'Floor 2', level: 'storey', count: 2 },
];

describe('product × location', () => {
  it('sets one product and toggles locations, each key gone when empty', () => {
    const r = setProduct({ classes: ['IfcDoor'] }, '40.20');
    expect(r).toEqual({ classes: ['IfcDoor'], products: ['40.20'] });
    expect(productCodes(r)).toEqual(['40.20']);
    expect(setProduct(r, null)).toEqual({ classes: ['IfcDoor'] });
    const on = toggleLocation(r, 's1');
    expect(on.locations).toEqual(['s1']);
    expect(toggleLocation(on, 's1').locations).toBeUndefined();
    expect(productCodes({ products: [{ system: 'Uniclass', code: 'Pr_30' }] })).toEqual(['Pr_30']);
  });

  it('lists the PBS indented and the locations by storey with their rooms', () => {
    expect(productOptions(items).map((o) => o.label)).toEqual(['40 Interior', ' 40.10 Partition walls', ' 40.20 Doors']);
    expect(productLabel(items, '40.20')).toBe('40.20 Doors');
    expect(productLabel(items, '99')).toBe('99');
    const groups = locationGroups(locations);
    expect(groups.map((g) => g.head.id)).toEqual(['b', 's1', 's2']);
    expect(groups[1].rooms.map((r) => r.id)).toEqual(['r1']);
  });

  it('says the pair in words, and the task row says it too', () => {
    expect(breakdownSummary({ products: ['40.20'], locations: ['s1', 's2'] }, items, locations)).toBe('40.20 Doors × Ground floor, Floor 2');
    expect(breakdownSummary({ locations: ['r1'] }, items, locations)).toBe('any product × 1.01 Hall');
    expect(breakdownSummary({ classes: ['IfcWall'] }, items, locations)).toBeNull();
    // The rows' tooltip (links.ruleSummary) names the products and locations too, the other keys as before.
    expect(ruleSummary({ rule: { classes: ['IfcWall'], products: ['40.20'], locations: ['s1'] } }, (id) => ({ s1: 'Ground floor' } as Record<string, string>)[id], (c) => c.replace('Ifc', '')))
      .toBe('Wall · 40.20 · Ground floor');
    expect(ruleSummary({ rule: { classes: ['IfcWall'] } }, () => undefined, (c) => c.replace('Ifc', ''))).toBe('Wall');
  });

  it('makes one task a location, every storey by default, in the model order', () => {
    expect(defaultLocations(locations)).toEqual(['s1', 's2']);
    expect(crossTasks(' Install doors ', locations, ['s1', 's2']).map((t) => t.name)).toEqual(['Install doors · Ground floor', 'Install doors · Floor 2']);
    expect(togglePicked(locations, ['s2'], 's1')).toEqual(['s1', 's2']);
    expect(togglePicked(locations, ['s1', 's2'], 's1')).toEqual(['s2']);
  });

  it('counts the elements without a task by product', () => {
    expect(byProductText([{ code: '', title: '', count: 2 }, { code: '40.20', title: 'Doors', count: 3 }])).toBe('40.20 Doors 3, no code 2');
    expect(byProductText(undefined)).toBe('');
  });
});
