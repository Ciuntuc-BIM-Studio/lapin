import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import type { TaskDto } from './gantt';
import { dropOf, elementIds, frameDistance, partLabel, ruleSummary, tasksHolding, tasksOf, toggleRule } from './links';

const task = (id: string, extra: Partial<TaskDto> = {}): TaskDto => ({
  id, row: 0, name: id, wbs: '', level: 0, parent: null, summary: false, milestone: false, duration: 1, start: '2026-11-16', finish: '2026-11-16',
  lateStart: '2026-11-16', lateFinish: '2026-11-16', totalFloat: 0, freeFloat: 0, critical: false, predecessors: '', count: extra.elements?.length ?? 0, ...extra,
});

const node = (id: string, type: string) => ({ id, type, name: id } as unknown as NodeDto);

describe('the tasks and the elements', () => {
  const tasks = [
    task('structure', { summary: true, elements: ['w1', 'w2', 's1'] }),
    task('masonry', { parent: 'structure', elements: ['w1', 'w2'] }),
    task('slab', { parent: 'structure', elements: ['s1'] }),
    task('plaster', { elements: ['w1'] }),
    task('handover'),
  ];

  it('marks the rows of the tasks holding what is selected, a summary with its tasks', () => {
    expect([...tasksHolding(tasks, ['w1'])].sort()).toEqual(['masonry', 'plaster', 'structure']);
    expect([...tasksHolding(tasks, ['s1'])].sort()).toEqual(['slab', 'structure']);
    expect(tasksHolding(tasks, []).size).toBe(0);
  });

  it('lists an element\'s tasks without the summaries over them', () => {
    expect(tasksOf(tasks, 'w1').map((t) => t.id)).toEqual(['masonry', 'plaster']);
    expect(tasksOf(tasks, 'nothing')).toEqual([]);
  });

  it('takes the elements of a selection, not its tasks or what the model no longer has', () => {
    const nodes = new Map([['w1', node('w1', 'IfcWall')], ['t', node('t', 'IfcTask')]]);
    expect(elementIds(['w1', 't', 'gone'], nodes)).toEqual(['w1']);
  });

  it('switches a rule\'s values on and off, an empty condition dropped', () => {
    const r1 = toggleRule(undefined, 'classes', 'IfcWall');
    expect(r1).toEqual({ classes: ['IfcWall'] });
    const r2 = toggleRule(r1, 'storeys', 'gf');
    expect(r2).toEqual({ classes: ['IfcWall'], storeys: ['gf'] });
    expect(toggleRule(r2, 'classes', 'IfcWall')).toEqual({ storeys: ['gf'] });
    expect(r1).toEqual({ classes: ['IfcWall'] });   // not changed in place
  });

  it('says a rule in a few words', () => {
    const names: Record<string, string> = { gf: 'Ground floor', m1: 'Gypsum plaster' };
    const nameOf = (id: string) => names[id];
    const label = (c: string) => c.replace(/^Ifc/, '');
    expect(ruleSummary({ rule: { classes: ['IfcWall', 'IfcColumn'], storeys: ['gf'] }, part: 'core', added: 2, removed: 1 }, nameOf, label))
      .toBe('Wall, Column · Ground floor · core · +2 −1');
    expect(ruleSummary({ rule: {} }, nameOf, label)).toBe('every element');
    expect(ruleSummary({ added: 3 }, nameOf, label)).toBe('+3');
    expect(partLabel('material:m1', nameOf)).toBe('Gypsum plaster');
    expect(partLabel('category:Finish', nameOf)).toBe('Finish');
    expect(partLabel(undefined, nameOf)).toBeNull();
  });

  it('reads a drop of the selection: its ids, Shift taking them out', () => {
    expect(dropOf('["a","b"]', false)).toEqual({ ids: ['a', 'b'], remove: false });
    expect(dropOf('["a"]', true)).toEqual({ ids: ['a'], remove: true });
    expect(dropOf('[]', false)).toBeNull();
    expect(dropOf('not json', false)).toBeNull();
    expect(dropOf('{"a":1}', false)).toBeNull();
  });

  it('frames a box from far enough to see it whole', () => {
    const d = frameDistance({ x: 0, y: 0, z: 0 }, { x: 10, y: 0, z: 0 }, Math.PI / 4);
    expect(d).toBeCloseTo(6 / Math.tan(Math.PI / 8), 6);
    expect(frameDistance({ x: 0, y: 0, z: 0 }, { x: 0.1, y: 0, z: 0 }, Math.PI / 4)).toBe(3);
  });
});
