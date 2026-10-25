import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { DEFAULT_SETS, disciplineOf, groupItems, implicitPlan, itemOf, matches, setsOf, THREE_D, type BrowserItem } from './browserSets';

const node = (id: string, type: string, name: string, props: Record<string, unknown> = {}): NodeDto =>
  ({ id, type, name, parentId: null, px: 0, py: 0, props, editable: true }) as unknown as NodeDto;
const item = (over: Partial<BrowserItem>): BrowserItem =>
  ({ id: 'x', name: 'View', kind: 'plan', discipline: 'Architecture', storeyId: null, scale: 100, phaseId: null, params: {}, node: null, ...over });

describe('the browser sets', () => {
  it('match on every field given, case-free for disciplines', () => {
    const permit = item({ name: 'Ground floor – permit', kind: 'plan', scale: 100, storeyId: 's1', params: { Stage: 'DTAC' } });
    expect(matches({}, permit)).toBe(true);
    expect(matches({ disciplines: ['architecture'] }, permit)).toBe(true);
    expect(matches({ disciplines: ['Structure'] }, permit)).toBe(false);
    expect(matches({ kinds: ['plan', 'section'], scales: ['100'], nameContains: 'PERMIT', storeys: ['s1'], params: { Stage: 'DTAC' } }, permit)).toBe(true);
    expect(matches({ scales: ['50'] }, permit)).toBe(false);
    expect(matches({ params: { Stage: 'DTAC', Client: 'X' } }, permit)).toBe(false);
    expect(matches({ phases: ['p1'] }, permit)).toBe(false);
  });

  it('read views, sheets and schedules as items, with their discipline, scale and parameters', () => {
    const section = itemOf(node('v1', 'LapinView', 'A-A', { kind: 'section', scale: 50, discipline: 'Structure', params: [{ name: 'Stage', kind: 'text', value: 'DTAC' }] }))!;
    expect(section).toMatchObject({ kind: 'section', scale: 50, discipline: 'Structure', params: { Stage: 'DTAC' } });
    expect(itemOf(node('s1', 'LapinSheet', 'Plans', { number: 'A-101' }))!.kind).toBe('sheet');
    expect(itemOf(node('q1', 'LapinSchedule', 'Doors'))!.discipline).toBe('Architecture');
    expect(itemOf(node('w1', 'IfcWall', 'Wall'))).toBeNull();
    expect(implicitPlan(node('st', 'IfcBuildingStorey', 'Ground floor')).id).toBe('plan:st');
  });

  it('group by the defaults when the project has no sets, and put what none takes under Other', () => {
    const nodes = new Map([['p', node('p', 'IfcProject', 'P')]]);
    expect(setsOf(nodes)).toBe(DEFAULT_SETS);
    const items = [THREE_D, item({ id: 'a', discipline: 'Architecture' }), item({ id: 'b', discipline: 'Structure', kind: 'section' }), item({ id: 'c', discipline: 'Landscape' })];
    const groups = groupItems(DEFAULT_SETS, items);
    expect(groups.map((g) => [g.set?.name ?? null, g.items.map((i) => i.id)])).toEqual([
      ['Architecture', ['3d', 'a']], ['Structure', ['b']], ['MEP', []], [null, ['c']],
    ]);
  });

  it('read the project\'s own sets, where an item may sit in several, and name the discipline new views take', () => {
    const nodes = new Map([['p', node('p', 'IfcProject', 'P', { browserSets: [
      { id: 'all', name: 'Everything', filter: {} }, { id: 'str', name: 'Structure', filter: { disciplines: ['Structure'] } }, { bad: true },
    ] })]]);
    const sets = setsOf(nodes);
    expect(sets.map((s) => s.name)).toEqual(['Everything', 'Structure']);
    const groups = groupItems(sets, [item({ id: 'b', discipline: 'Structure' })]);
    expect(groups.map((g) => g.items.length)).toEqual([1, 1]);
    expect(disciplineOf(sets[0])).toBe('Architecture');
    expect(disciplineOf(sets[1])).toBe('Structure');
    expect(disciplineOf(null)).toBe('Architecture');
  });
});
