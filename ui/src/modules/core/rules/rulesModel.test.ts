import { describe, expect, it } from 'vitest';
import {
  addStairSet, addStep, addSteel, parseDiameters, removeStairSet, removeStep, removeSteel, setPitch, setStairValue, setStep, stairProblem, stepsProblem, findingsByModule,
} from './rulesModel';

describe('rule tables', () => {
  it('sets a stair value in one set only', () => {
    const s = { residential: { riserMax: 0.18 }, public: { riserMax: 0.16 } };
    expect(setStairValue(s, 'residential', 'riserMax', 0.19)).toEqual({ residential: { riserMax: 0.19 }, public: { riserMax: 0.16 } });
  });
  it('starts a set from another, and keeps Lapin\'s sets', () => {
    const s = { residential: { a: 1 } };
    expect(addStairSet(s, 'loft', 'residential')).toEqual({ residential: { a: 1 }, loft: { a: 1 } });
    expect(addStairSet(s, 'residential', 'residential')).toBeNull();
    expect(removeStairSet({ residential: {}, loft: {} }, 'loft')).toEqual({ residential: {} });
    expect(removeStairSet({ residential: {} }, 'residential')).toEqual({ residential: {} });
  });
  it('finds a least above a most', () => {
    expect(stairProblem({ riserMin: 0.2, riserMax: 0.18, treadMin: 0.2, treadMax: 0.3, stepMin: 0.5, stepMax: 0.7 })).toMatch(/riser/);
    expect(stairProblem({ riserMin: 0.1, riserMax: 0.18, treadMin: 0.2, treadMax: 0.3, stepMin: 0.5, stepMax: 0.7 })).toBeNull();
  });
  it('edits steps, the last having no "up to"', () => {
    const m = [{ upTo: 16, factor: 4 }, { factor: 7 }];
    expect(addStep(m, 4)).toEqual([{ upTo: 16, factor: 4 }, { upTo: 20, factor: 7 }, { factor: 7 }]);
    expect(removeStep([{ upTo: 16, factor: 4 }, { factor: 7 }], 1)).toEqual([{ factor: 4 }]);
    expect(setStep(m, 0, { factor: 5 })[0].factor).toBe(5);
    expect(stepsProblem([{ upTo: 20, factor: 4 }, { upTo: 10, factor: 4 }, { factor: 7 }], 'mandrel')).toMatch(/go up/);
    expect(stepsProblem(m, 'mandrel')).toBeNull();
  });
  it('reads diameters and keeps one steel at least', () => {
    expect(parseDiameters('12, 8 10;8')).toEqual([8, 10, 12]);
    expect(parseDiameters('2')).toBeNull();
    expect(addSteel(['B500'], ' PC52 ')).toEqual(['B500', 'PC52']);
    expect(addSteel(['B500'], 'B500')).toEqual(['B500']);
    expect(removeSteel({ steels: ['B500', 'PC52'], defaultSteel: 'B500' }, 'B500')).toEqual({ steels: ['PC52'], defaultSteel: 'PC52' });
    expect(removeSteel({ steels: ['B500'], defaultSteel: 'B500' }, 'B500')).toEqual({ steels: ['B500'], defaultSteel: 'B500' });
  });
  it('keeps a pitch within 0 and 89 and sorts findings', () => {
    expect(setPitch({ minPitch: { tile: 22 } }, 'tile', 95)).toEqual({ minPitch: { tile: 89 } });
    const f = findingsByModule([{ module: 'a', element: null, severity: 'info', message: 'i' }, { module: 'a', element: null, severity: 'warning', message: 'w' }]);
    expect(f[0][1].map((x) => x.severity)).toEqual(['warning', 'info']);
  });
});

describe('bolt grades', () => {
  it('adds, renames and removes grades, keeping one at least and no duplicates', async () => {
    const { addGrade, removeGrade, renameGrade } = await import('./rulesModel');
    expect(addGrade(['8.8'], ' A325 ')).toEqual(['8.8', 'A325']);
    expect(addGrade(['8.8'], '8.8')).toEqual(['8.8']);
    expect(renameGrade(['8.8', '10.9'], 1, 'A490')).toEqual(['8.8', 'A490']);
    expect(renameGrade(['8.8', '10.9'], 1, '8.8')).toEqual(['8.8', '10.9']);
    expect(removeGrade(['8.8'], 0)).toEqual(['8.8']);
    expect(removeGrade(['8.8', '10.9'], 0)).toEqual(['10.9']);
  });
});
