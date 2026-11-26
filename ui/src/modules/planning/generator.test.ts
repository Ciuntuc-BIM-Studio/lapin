import { describe, expect, it } from 'vitest';
import {
  addChainStep, addPackage, bars, chainDays, cleanSpec, freshKey, freshPackageKey, indicativeChain, moveStep, normLabel, packageDays, procurementOf, procures, procureStep,
  quantityText, removeChainStep, setPackageDays, stepForLeftovers, toggleClass, translateNames, withChainStep, withPackage, withProcurement, withStep,
  type GenSpec, type GenStep, type PreviewTask,
} from './generator';

const step = (key: string, extra: Partial<GenStep> = {}): GenStep =>
  ({ key, name: key, classes: ['IfcWall'], split: 'storey', days: 5, crew: 0, link: 'FS', lag: 0, on: true, ...extra });

const spec = (...steps: GenStep[]): GenSpec => ({ template: 'masonry', steps, byStorey: true, demolition: true, formwork: true, scaffolding: true });

describe('the generator\'s sequence', () => {
  it('moves a step up or down, the others keeping their order, and clamps at the ends', () => {
    const s = [step('a'), step('b'), step('c')];
    expect(moveStep(s, 2, 0).map((x) => x.key)).toEqual(['c', 'a', 'b']);
    expect(moveStep(s, 0, 1).map((x) => x.key)).toEqual(['b', 'a', 'c']);
    expect(moveStep(s, 1, 9).map((x) => x.key)).toEqual(['a', 'c', 'b']);
    expect(moveStep(s, 5, 0)).toEqual(s);
  });

  it('changes one step by its key and toggles the kinds it builds', () => {
    const s = withStep(spec(step('a'), step('b')), 'b', { crew: 3, on: false });
    expect(s.steps[1]).toMatchObject({ crew: 3, on: false });
    expect(s.steps[0].crew).toBe(0);
    expect(toggleClass(step('a'), 'IfcSlab').classes).toEqual(['IfcWall', 'IfcSlab']);
    expect(toggleClass(step('a'), 'IfcWall').classes).toEqual([]);
  });

  it('gives the elements left without a task a step of their own, before the external works', () => {
    const s = spec(step('walls'), step('external', { split: 'once' }));
    const next = stepForLeftovers(s, ['IfcRailing', 'IfcFurniture'], 'Other elements')!;
    expect(next.steps.map((x) => x.key)).toEqual(['walls', 'other', 'external']);
    expect(next.steps[1]).toMatchObject({ name: 'Other elements', classes: ['IfcRailing', 'IfcFurniture'], split: 'storey', on: true });
    expect(freshKey(next, 'other')).toBe('other-2');
    expect(stepForLeftovers(s, [], 'x')).toBeNull();
    expect(stepForLeftovers(spec(step('walls')), ['IfcSlab'], 'Rest')!.steps.map((x) => x.key)).toEqual(['walls', 'other']);
  });

  it('names the catalogue\'s steps in the interface\'s language', () => {
    const s = translateNames(spec(step('a', { name: 'Masonry' })), (x) => (x === 'Masonry' ? 'Zidărie' : x));
    expect(s.steps[0].name).toBe('Zidărie');
  });
});

describe('the generator\'s preview', () => {
  it('names a norm by its code, text and hours a unit', () => {
    expect(normLabel({ code: 'LAP-MASON', name: 'Masonry', unit: 'm3', hours: 5 })).toBe('LAP-MASON · Masonry · 5 h/m3');
    expect(normLabel({ code: 'X', name: '', unit: 'kg', hours: 0.03333 })).toBe('X · 0.033 h/kg');
  });

  it('shows a quantity with two decimals at most and its unit', () => {
    expect(quantityText(4.5, 'm3')).toBe('4.5 m3');
    expect(quantityText(1234.567, 'kg')).toBe('1 234.57 kg');
    expect(quantityText(undefined, 'm3')).toBe('');
  });

  it('places the bars over the schedule\'s span, a day the narrowest', () => {
    const t = (start: string, finish: string): PreviewTask => ({ id: start, name: '', level: 0, summary: false, start, finish, duration: 1, critical: false, count: 0 });
    // 1–10 March is ten days; the first task its first five, the second its last five.
    const b = bars([t('2027-03-01', '2027-03-05'), t('2027-03-06', '2027-03-10')]);
    expect(b.days).toBe(10);
    expect(b.rows[0]).toEqual({ left: 0, width: 0.5 });
    expect(b.rows[1]).toEqual({ left: 0.5, width: 0.5 });
    expect(bars([]).rows).toEqual([]);
  });
});

describe('the generator\'s procurement', () => {
  const on = (s: GenSpec) => withProcurement(s, { on: true });

  it('is off and Lapin\'s indicative chain until switched on, and a spec without it is cleaned as before', () => {
    const s = spec(step('structure'));
    const p = procurementOf(s);
    expect(p.on).toBe(false);
    expect(p.chain.map((c) => c.key)).toEqual(['design', 'approval', 'manufacturing', 'transport']);
    expect(p.chain.map((c) => c.days)).toEqual([5, 5, 5, 5]);
    expect(p.chain[3].kind).toBe('LOGISTIC');
    expect(indicativeChain(p)).toBe(true);
    expect('procurement' in cleanSpec(s)).toBe(false);
    expect(cleanSpec(on(s)).procurement?.on).toBe(true);
  });

  it('gives a step its package and takes it off again, keeping the package\'s days', () => {
    let p = procurementOf(on(spec(step('structure'), step('masonry'))));
    p = procureStep(p, 'structure', true);
    expect(procures(p, 'structure')).toBe(true);
    expect(p.packages).toEqual([{ key: 'structure', name: '', before: 'structure', on: true }]);
    p = withPackage(p, 'structure', setPackageDays(p.packages[0], p.chain[2], 15));
    expect(chainDays(p, p.packages[0])).toBe(30);
    p = procureStep(p, 'structure', false);
    expect(procures(p, 'structure')).toBe(false);
    expect(p.packages[0].days).toEqual({ manufacturing: 15 });
    expect(procureStep(procurementOf(spec()), 'x', false).packages).toEqual([]);
  });

  it('overrides a chain step\'s days for one package, the chain\'s value taking it away', () => {
    const p = procurementOf(spec());
    const pkg = { key: 'k', name: 'Kitchens', before: 'interior', on: true };
    const longer = setPackageDays(pkg, p.chain[2], 20);
    expect(packageDays(longer, p.chain[2])).toBe(20);
    expect(packageDays(longer, p.chain[0])).toBe(5);
    expect(setPackageDays(longer, p.chain[2], 5).days).toBeUndefined();
    expect(setPackageDays(longer, p.chain[2], null).days).toBeUndefined();
  });

  it('adds named packages with keys of their own and edits the user\'s chain', () => {
    let p = procurementOf(spec());
    p = addPackage(addPackage(p, 'Doors & windows', 'openings'), 'Doors & windows', 'openings');
    expect(p.packages.map((x) => x.key)).toEqual(['doors-windows', 'doors-windows-2']);
    expect(p.packages[0]).toMatchObject({ name: 'Doors & windows', before: 'openings', on: true });
    expect(freshPackageKey(['a'], '')).toBe('package');
    p = addChainStep(p, 'Customs');
    expect(p.chain.at(-1)).toEqual({ key: 'customs', name: 'Customs', days: 5 });
    expect(indicativeChain(p)).toBe(false);
    p = removeChainStep(withChainStep(p, 'approval', { days: 10 }), 'customs');
    expect(p.chain.find((c) => c.key === 'approval')?.days).toBe(10);
    expect(indicativeChain(p)).toBe(false);
    // Cleaned for the engine: a chain step with no name and a package before no step go.
    const dirty = { ...p, chain: [...p.chain, { key: 'x', name: '  ', days: 1 }], packages: [...p.packages, { key: 'y', name: ' Lifts ', before: '', on: true }] };
    const clean = cleanSpec({ ...spec(), procurement: dirty }).procurement!;
    expect(clean.chain.map((c) => c.key)).not.toContain('x');
    expect(clean.packages.map((x) => x.key)).not.toContain('y');
  });
});
