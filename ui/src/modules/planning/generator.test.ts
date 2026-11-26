import { describe, expect, it } from 'vitest';
import { bars, freshKey, moveStep, normLabel, quantityText, stepForLeftovers, toggleClass, translateNames, withStep, type GenSpec, type GenStep, type PreviewTask } from './generator';

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
