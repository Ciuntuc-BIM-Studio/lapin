import { describe, expect, it } from 'vitest';
import {
  addStairSet, addStep, addSteel, parseDiameters, removeStairSet, removeStep, removeSteel, setPitch, setStairValue, setStep, stairProblem, stepsProblem, findingsByModule,
} from './rulesModel';
import {
  addLoadSet, addSnowZone, languageFrom, loadSetProblem, removeLoadSet, removeSafetyClass, removeSnowZone, setLoadSet, setPsi, setSafetyClass, setSnowZone, sourcesFrom,
  type LoadSet, type LoadsSection,
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

describe('loads in a rule pack (N1)', () => {
  const en: LoadSet = {
    label: 'EN 1990 Annex A1, recommended values', gammaGSup: 1.35, gammaGInf: 1, gammaQ: 1.5, expression: '6.10', xi: 0.85,
    psi: { A: { '0': 0.7, '1': 0.5, '2': 0.3 }, snow: { '0': 0.5, '1': 0.2, '2': 0 } }, snowPsi: [], safetyClasses: {},
  };
  const loads: LoadsSection = { sets: { en, cr0: { ...en, label: 'CR 0-2012' } }, imposed: { A: 2 } };

  it('starts a set of one\'s own from another and keeps Lapin\'s', () => {
    const next = addLoadSet(loads, 'se', 'en')!;
    expect(next.sets.se).toEqual({ ...en, label: 'se' });
    expect(addLoadSet(loads, 'en', 'en')).toBeNull();
    expect(addLoadSet(loads, ' ', 'en')).toBeNull();
    expect(Object.keys(removeLoadSet(next, 'se').sets)).toEqual(['en', 'cr0']);
    expect(removeLoadSet(next, 'en')).toBe(next);
  });
  it('changes one value of one set', () => {
    const next = setLoadSet(loads, 'cr0', { gammaQ: 1.4 });
    expect(next.sets.cr0!.gammaQ).toBe(1.4);
    expect(next.sets.en!.gammaQ).toBe(1.5);
    expect(setPsi(en, 'A', 2, 0.6).psi.A).toEqual({ '0': 0.7, '1': 0.5, '2': 0.6 });
  });
  it('gives γd by safety class and refuses one out of range', () => {
    const s = setSafetyClass(en, '2', 0.91)!;
    expect(s.safetyClasses).toEqual({ '2': 0.91 });
    expect(setSafetyClass(en, '2', 3)).toBeNull();
    expect(setSafetyClass(en, '', 1)).toBeNull();
    expect(removeSafetyClass(s, '2').safetyClasses).toEqual({});
  });
  it('adds snow zones in order and finds two from the same s0,k', () => {
    const one = addSnowZone(en);
    expect(one.snowPsi).toEqual([{ from: 0, psi: { '0': 0.5, '1': 0.2, '2': 0 } }]);
    const two = addSnowZone(one);
    expect(two.snowPsi.map((z) => z.from)).toEqual([0, 1]);
    expect(loadSetProblem(two)).toBeNull();
    expect(loadSetProblem(setSnowZone(two, 1, { from: 0 }))).toMatch(/same s0,k/);
    expect(setSnowZone(two, 1, { psi: { '0': 0.7, '1': 0.4, '2': 0.2 } }).snowPsi[1]!.psi['0']).toBe(0.7);
    expect(removeSnowZone(two, 0).snowPsi).toHaveLength(1);
    expect(loadSetProblem({ ...en, gammaQ: 5 })).toMatch(/γQ/);
    expect(loadSetProblem(setPsi(en, 'A', 0, 1.2))).toMatch(/ψ0/);
  });
  it('reads the pack\'s sources one a line and its language as a code', () => {
    expect(sourcesFrom('Boverket\n\n  A link  \n')).toEqual(['Boverket', 'A link']);
    expect(languageFrom('sv')).toBe('sv');
    expect(languageFrom(' ')).toBe('');
    expect(languageFrom('Swedish')).toBeNull();
  });
});

import {
  ACCESS_LIMITS, addRegion, checksAccessibility, functionsFrom, mergeRegions, parseRegions, qbFromVb0, regionProblem, regionsProblem, removeRegion, setAccessLimit, setRegion,
  type AccessibilitySection, type ClimateSection,
} from './rulesModel';

describe('climate regions (N2)', () => {
  const c: ClimateSection = { regionLabel: 'Kommun', regions: [{ name: 'Kiruna', sk: 5.5, vb0: 22 }] };
  it('works out qb from vb,0 as the engine does', () => {
    expect(qbFromVb0(22)).toBe(0.3025);
    expect(qbFromVb0(0)).toBe(0);
  });
  it('adds, changes and removes regions, each name once', () => {
    const a = addRegion(c, ' Malmö ')!;
    expect(a.regions.map((r) => r.name)).toEqual(['Kiruna', 'Malmö']);
    expect(addRegion(c, 'kiruna')).toBeNull();
    expect(addRegion(c, '')).toBeNull();
    expect(setRegion(a, 1, { sk: 1, vb0: 26 }).regions[1]).toEqual({ name: 'Malmö', sk: 1, vb0: 26 });
    expect(setRegion(c, 0, { vb0: null }).regions[0]).toEqual({ name: 'Kiruna', sk: 5.5 });
    expect(removeRegion(a, 0).regions.map((r) => r.name)).toEqual(['Malmö']);
  });
  it('finds what the engine would refuse', () => {
    expect(regionProblem({ name: 'A', sk: 25 })).toMatch(/sk/);
    expect(regionProblem({ name: 'A', vb0: 22, qb: 0.4 })).toMatch(/not both/);
    expect(regionProblem({ name: 'A' })).toMatch(/gives/);
    expect(regionProblem({ name: 'A', qb: 0.4 })).toBeNull();
    expect(regionsProblem({ regionLabel: 'R', regions: [{ name: 'A', sk: 1 }, { name: 'a', sk: 2 }] })).toMatch(/twice/);
    expect(regionsProblem(c)).toBeNull();
  });
  it('reads regions pasted from a table, a header left out, a decimal comma read', () => {
    const r = parseRegions('Kommun\tsk\tvb0\nKiruna\t5,5\t22\nMalmö;1.0;26;coast\nGotland;2\n');
    expect(r.problem).toBeNull();
    expect(r.regions).toEqual([{ name: 'Kiruna', sk: 5.5, vb0: 22 }, { name: 'Malmö', sk: 1, vb0: 26, note: 'coast' }, { name: 'Gotland', sk: 2 }]);
    expect(parseRegions('Kiruna\t5.5\t22\nUmeå\tlots\t24').problem).toMatch(/line 2/);
    expect(parseRegions('Kiruna\t25\t22').problem).toMatch(/sk/);
    const merged = mergeRegions(c, r.regions);
    expect(merged.regions.map((x) => x.name)).toEqual(['Kiruna', 'Malmö', 'Gotland']);
    expect(merged.regions[0]!.sk).toBe(5.5);
  });
});

describe('accessibility (N3)', () => {
  const none: AccessibilitySection = { doorClearWidth: null, corridorWidth: null, turningDiameter: null, checkRamps: false, corridorFunctions: ['Circulation'], turningFunctions: ['Bath'] };
  it('checks nothing by default, and a limit within its range', () => {
    expect(checksAccessibility(none)).toBe(false);
    const a = setAccessLimit(none, 'doorClearWidth', 0.8)!;
    expect(a.doorClearWidth).toBe(0.8);
    expect(checksAccessibility(a)).toBe(true);
    expect(setAccessLimit(none, 'turningDiameter', 5)).toBeNull();
    expect(setAccessLimit(a, 'doorClearWidth', null)!.doorClearWidth).toBeNull();
    expect(checksAccessibility({ ...none, checkRamps: true })).toBe(true);
    expect(ACCESS_LIMITS.map((l) => l.key)).toEqual(['doorClearWidth', 'corridorWidth', 'turningDiameter']);
  });
  it('reads room functions typed as a list', () => {
    expect(functionsFrom('Bath, WC; bath\nBedroom ,')).toEqual(['Bath', 'WC', 'Bedroom']);
    expect(functionsFrom('')).toEqual([]);
  });
});
