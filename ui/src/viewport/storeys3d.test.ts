import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { ALL_STOREYS, chipText, cutZ, hiddenByStoreys, settingOf, shownStoreys, storeyOf, storeys3dOf, type Storeys3d } from './storeys3d';

const node = (id: string, type: string, props: Record<string, unknown> = {}, storeyId: string | null = null, parentId: string | null = null): NodeDto =>
  ({ id, type, name: id, parentId, px: 0, py: 0, props: props as NodeDto['props'], editable: true, module: null, storeyId });

// Foundations −1.50, Ground floor ±0.00, First floor +3.00; a wall on each, a curtain wall's panel on the ground floor, the terrain on none.
const fnd = node('fnd', 'IfcBuildingStorey', { elevation: -1.5 }, null, 'b');
const gf = node('gf', 'IfcBuildingStorey', { elevation: 0 }, null, 'b');
const ff = node('ff', 'IfcBuildingStorey', { elevation: 3 }, null, 'b');
const storeys = [fnd, gf, ff];
const nodes = new Map<string, NodeDto>([
  ['p', node('p', 'IfcProject', { view3d: { storeys: { mode: 'below', ids: [], unplaced: true, cut: 1.2 } } })],
  ['b', node('b', 'IfcBuilding')],
  ...storeys.map((s) => [s.id, s] as [string, NodeDto]),
  ['wF', node('wF', 'IfcWall', {}, 'fnd', 'fnd')],
  ['wG', node('wG', 'IfcWall', {}, 'gf', 'gf')],
  ['wU', node('wU', 'IfcWall', {}, 'ff', 'ff')],
  ['cw', node('cw', 'IfcCurtainWall', {}, 'gf', 'gf')],
  ['panel', node('panel', 'IfcPlate', {}, null, 'cw')],
  ['space', node('space', 'IfcSpace', {}, null, 'ff')],
  ['terrain', node('terrain', 'IfcGeographicElement', {}, null, 'site')],
  ['saved', node('saved', 'LapinView', { kind: '3d', storeys: { mode: 'some', ids: ['fnd', 'ff', 'gone'] } })],
]);
const elements = ['wF', 'wG', 'wU', 'cw', 'panel', 'space', 'terrain'];
const hidden = (s: Partial<Storeys3d>, active = 'gf') => hiddenByStoreys(nodes, elements, { ...ALL_STOREYS, ...s }, storeys, active).sort();

describe('the storeys a 3D view shows (report #29)', () => {
  it('shows every storey by default, and what stands on none', () => {
    expect(storeys3dOf(undefined)).toEqual(ALL_STOREYS);
    expect(storeys3dOf({ storeys: { mode: 'odd' } })).toEqual(ALL_STOREYS);
    expect(hidden({})).toEqual([]);
    expect(shownStoreys(ALL_STOREYS, storeys, 'gf')).toBeNull();
  });

  it('reads {3D}\'s setting from the project and a saved view\'s from the view', () => {
    expect(settingOf(nodes, '3d')).toEqual({ mode: 'below', ids: [], unplaced: true, cut: 1.2 });
    expect(settingOf(nodes, null).mode).toBe('below');
    expect(settingOf(nodes, 'saved')).toEqual({ mode: 'some', ids: ['fnd', 'ff', 'gone'], unplaced: true, cut: null });
  });

  it('finds an element\'s storey through its containment or its aggregate', () => {
    expect(storeyOf(nodes, nodes.get('wG')!)).toBe('gf');
    expect(storeyOf(nodes, nodes.get('panel')!)).toBe('gf');
    expect(storeyOf(nodes, nodes.get('space')!)).toBe('ff');
    expect(storeyOf(nodes, nodes.get('terrain')!)).toBeNull();
  });

  it('shows the active storey alone, and follows it', () => {
    expect(hidden({ mode: 'active' })).toEqual(['space', 'wF', 'wU']);
    expect(hidden({ mode: 'active' }, 'ff')).toEqual(['cw', 'panel', 'wF', 'wG']);
  });

  it('shows the active storey and those below: the building cut at a level', () => {
    expect(hidden({ mode: 'below' })).toEqual(['space', 'wU']);
    expect(hidden({ mode: 'below' }, 'fnd')).toEqual(['cw', 'panel', 'space', 'wG', 'wU']);
    expect(hidden({ mode: 'below' }, 'ff')).toEqual([]);
  });

  it('shows the storeys ticked, forgetting those that are gone', () => {
    const s = settingOf(nodes, 'saved');
    expect([...shownStoreys(s, storeys, 'gf')!].sort()).toEqual(['ff', 'fnd']);
    expect(hiddenByStoreys(nodes, elements, s, storeys, 'gf').sort()).toEqual(['cw', 'panel', 'wG']);
  });

  it('hides what stands on no storey only when asked', () => {
    expect(hidden({ unplaced: false })).toEqual(['terrain']);
    expect(hidden({ mode: 'active', unplaced: false })).toEqual(['space', 'terrain', 'wF', 'wU']);
  });

  it('cuts above the highest storey shown, at its level plus the cut', () => {
    expect(cutZ({ ...ALL_STOREYS, mode: 'below', cut: 1.2 }, storeys, 'gf')).toBeCloseTo(1.2);
    expect(cutZ({ ...ALL_STOREYS, mode: 'active', cut: 1.2 }, storeys, 'ff')).toBeCloseTo(4.2);
    expect(cutZ({ ...ALL_STOREYS, cut: 1 }, storeys, 'gf')).toBeCloseTo(4);
    expect(cutZ({ ...ALL_STOREYS, mode: 'below' }, storeys, 'gf')).toBeNull();
  });

  it('says on the chip what is shown', () => {
    expect(chipText(ALL_STOREYS, storeys, 'gf')).toBeNull();
    expect(chipText({ ...ALL_STOREYS, mode: 'active' }, storeys, 'gf')).toEqual({ name: 'gf', text: 'only', cut: null });
    expect(chipText({ ...ALL_STOREYS, mode: 'below', cut: 1.2 }, storeys, 'ff')).toEqual({ name: 'ff', text: 'and below', cut: 'cut at 1.20 m' });
    expect(chipText({ ...ALL_STOREYS, mode: 'some', ids: ['gf', 'ff'] }, storeys, 'gf')?.text).toBe('2 of 3 storeys');
    expect(chipText({ ...ALL_STOREYS, mode: 'some', ids: ['ff'] }, storeys, 'gf')).toEqual({ name: 'ff', text: 'only', cut: null });
  });
});
