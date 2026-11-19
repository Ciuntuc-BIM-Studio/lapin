import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { reach, toggleFace, wrapsArg, wrapsOf, type Wrap } from './columnLayers';

const node = (id: string, name: string, props: Record<string, unknown> = {}) => ({ id, name, type: 'IfcMaterial', props, storeyId: null }) as unknown as NodeDto;

describe('a column type\'s finish layers', () => {
  it('read with their materials by name, all round when no faces are kept', () => {
    const nodes = new Map([['m1', node('m1', 'Plaster')], ['m2', node('m2', 'Fire board')]]);
    const type = node('t', 'C30', { finishLayers: [{ material: 'm1', thickness: 0.015 }, { material: 'm2', thickness: 0.025, faces: ['-x', '+x'] }] });
    expect(wrapsOf(type, nodes)).toEqual([
      { material: 'Plaster', thickness: 0.015, faces: ['+x', '-x', '+y', '-y'] },
      { material: 'Fire board', thickness: 0.025, faces: ['+x', '-x'] },
    ]);
    expect(wrapsOf(node('t2', 'C', {}), nodes)).toEqual([]);
  });

  it('sent without faces when all round, and without the empty ones', () => {
    const w: Wrap[] = [{ material: 'Plaster', thickness: 0.015, faces: ['+x', '-x', '+y', '-y'] }, { material: ' Board ', thickness: 0.02, faces: ['+y'] }, { material: '', thickness: 0.01, faces: ['+x'] }];
    expect(wrapsArg(w)).toEqual([{ material: 'Plaster', thickness: 0.015 }, { material: 'Board', thickness: 0.02, faces: ['+y'] }]);
  });

  it('turn a face on and off, never the last one, and add up what they reach out', () => {
    const w: Wrap = { material: 'Board', thickness: 0.02, faces: ['+x'] };
    expect(toggleFace(w, '+x')).toBe(w);
    expect(toggleFace(w, '-y').faces).toEqual(['+x', '-y']);
    expect(reach([{ material: 'P', thickness: 0.015, faces: ['+x', '-x', '+y', '-y'] }, w])).toEqual({ '+x': 0.035, '-x': 0.015, '+y': 0.015, '-y': 0.015 });
  });
});
