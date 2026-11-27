import { describe, expect, it } from 'vitest';
import cost from '../cost/index';
import planning from './index';
import { cleanSpec, type GenSpec } from './generator';
import { addGeneratorSection, generatorSections, sectionValue, withSectionValue, type GeneratorSection } from './generatorSections';

const spec: GenSpec = { template: 'masonry', steps: [], byStorey: true, demolition: true, formwork: true, scaffolding: true };
const Nothing = () => null;

describe('the generator\'s sections other modules add (docs/plan-extension-modules.md §6.3)', () => {
  it('adds a section once by its id, in order, and takes it away again', () => {
    const before = generatorSections().length;
    const off = addGeneratorSection({ id: 'chain', label: 'Chain', component: Nothing });
    const again = addGeneratorSection({ id: 'chain', label: 'Chain 2', component: Nothing });
    expect(generatorSections().length).toBe(before + 1);
    expect(generatorSections().at(-1)?.label).toBe('Chain 2');
    off();
    expect(generatorSections().length).toBe(before + 1);
    again();
    expect(generatorSections().length).toBe(before);
  });

  it('reads and writes its part of the spec under its id, which cleaning the spec keeps as it came', () => {
    const s = { id: 'chain', label: 'Chain', component: Nothing } as GeneratorSection;
    expect(sectionValue(s, spec)).toBeUndefined();
    const on = withSectionValue(s, spec, { on: true, days: 4 });
    expect(sectionValue(s, on)).toEqual({ on: true, days: 4 });
    expect(cleanSpec(on).chain).toEqual({ on: true, days: 4 });
    expect('chain' in withSectionValue(s, on, undefined)).toBe(false);
    expect(spec).not.toHaveProperty('chain');
    // Its own read and write when its part is not one key.
    const own: GeneratorSection = { ...s, read: (x) => x.byStorey, write: (x, v) => ({ ...x, byStorey: !!v }) };
    expect(withSectionValue(own, spec, false).byStorey).toBe(false);
    expect(sectionValue(own, spec)).toBe(true);
  });
});

describe('what the open modules offer extension modules', () => {
  it('planning offers the Gantt\'s helpers, the Gantt and the generator\'s sections; cost the money and the estimate; at api 1', () => {
    expect(planning.api).toBe(1);
    const p = planning.exports as Record<string, unknown>;
    expect(Object.keys(p.gantt as object).sort()).toEqual(['dateOf', 'dayOf', 'dragDays', 'namesOf', 'showDate', 'ticks']);
    expect(p.addGeneratorSection).toBe(addGeneratorSection);
    expect(typeof p.openGantt).toBe('function');
    expect(cost.api).toBe(1);
    expect(Object.keys(cost.exports ?? {}).sort()).toEqual(['axisOf', 'formOnSheet', 'money', 'niceStep', 'openCostTime', 'shortMoney', 'useEstimateData']);
  });
});
