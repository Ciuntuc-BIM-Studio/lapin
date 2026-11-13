import { describe, expect, it } from 'vitest';
import { addLeg, addParam, cleanDef, fitCorners, newShape, nextKey, removeLeg, setParam, shapeProblem } from './shapeModel';
import { diameterChoices, steelChoices } from './rebarRules';

describe('bar shapes', () => {
  it('adds parameters with free keys and renames them in the legs', () => {
    expect(nextKey([{ key: 'A', label: '', default: 1 }, { key: 'B', label: '', default: 1 }])).toBe('C');
    const d = setParam(newShape(), 0, { key: 'X' });
    expect(d.legs[0].length).toBe('X');
    expect(addParam(newShape()).params.map((p) => p.key)).toEqual(['A', 'B', 'C']);
  });
  it('keeps no turn on the last leg', () => {
    const d = addLeg(newShape());
    expect(d.legs.map((l) => l.turn)).toEqual(['90', '90', undefined]);
    const r = removeLeg(d, 2);
    expect(r.legs[r.legs.length - 1].turn).toBeUndefined();
    expect(cleanDef(d).legs[2]).toEqual({ length: 'B' });
  });
  it('finds what the engine would refuse', () => {
    expect(shapeProblem({ ...newShape(), code: '' })).toMatch(/code/);
    expect(shapeProblem({ ...newShape(), params: [{ key: '1x', label: '', default: 1 }] })).toMatch(/key/);
    expect(shapeProblem(newShape())).toBeNull();
  });
  it('fits corners into a box, y up', () => {
    const f = fitCorners([[0, 0], [1, 0], [1, 1]], 100, 100, 10)!;
    expect(f.scale).toBe(80);
    expect(f.points[0]).toEqual([10, 90]);
    expect(f.points[2]).toEqual([90, 10]);
    expect(fitCorners([[0, 0]], 10, 10)).toBeNull();
  });
  it('offers the rules\' diameters and steels over the catalog\'s, keeping the current', () => {
    expect(diameterChoices({ diameters: [8, 10, 12.5] }, [0.006], null)).toEqual([0.008, 0.01, 0.0125]);
    expect(diameterChoices({ diameters: [8] }, [], 0.01)).toEqual([0.008, 0.01]);
    expect(diameterChoices(null, [0.006], null)).toEqual([0.006]);
    expect(steelChoices({ steels: ['B500'] }, ['X'], 'PC52')).toEqual(['B500', 'PC52']);
  });
});
