import { describe, expect, it } from 'vitest';
import { groupsOf, withPreset, type PortalCatalog } from './portal';
import { axisSourceOf, fullTunnelSpec, isRings } from './tunnel';

const catalog: PortalCatalog = {
  params: [
    { key: 'clearWidth', label: 'Clear width', kind: 'length', group: 'Section', default: 10.6, formula: true, showIf: { preset: ['horseshoe', 'box', 'vault'] } },
    { key: 'innerRadius', label: 'Inner radius', kind: 'length', group: 'Section', default: 4.75, formula: true, showIf: { preset: ['circular'] } },
    { key: 'thickness', label: 'Lining thickness', kind: 'length', group: 'Lining', default: 0.35, formula: true },
    { key: 'wallThickness', label: 'Wall thickness', kind: 'length', group: 'Lining', default: '{= thickness}', formula: true, showIf: { preset: ['horseshoe', 'box', 'vault'] } },
    { key: 'blockLength', label: 'Casting block length', kind: 'length', group: 'Blocks', default: 12.5, formula: true, showIf: { preset: ['horseshoe', 'box', 'vault'] } },
    { key: 'ringWidth', label: 'Ring width', kind: 'length', group: 'Blocks', default: 1.8, formula: true, showIf: { preset: ['circular'] } },
    { key: 'ifc', label: 'IFC', kind: 'choice', group: 'Output', default: 'assembly', formula: false, choices: ['assembly', 'body'] },
  ],
  presets: [
    { key: 'horseshoe', set: { primary: 0.25 } },
    { key: 'circular', set: { thickness: 0.4 } },
    { key: 'box', set: { clearWidth: 10, thickness: 0.8 } },
    { key: 'vault', set: { clearWidth: 9 } },
  ],
};

describe('the tunnel lining configurator', () => {
  it('fills a spec from the defaults under its preset, the horseshoe when it names none', () => {
    expect(fullTunnelSpec(catalog, {}).preset).toBe('horseshoe');
    expect(fullTunnelSpec(catalog, { preset: 'nonsense' }).preset).toBe('horseshoe');
    const box = fullTunnelSpec(catalog, { preset: 'box', clearWidth: 12 });
    expect(box.thickness).toBe(0.8);
    expect(box.clearWidth).toBe(12);
    expect(box.wallThickness).toBe('{= thickness}');
  });

  it('shows the numbers its preset has: a ring\'s radius and width, a horseshoe\'s width and blocks', () => {
    const keys = (preset: string) => groupsOf(catalog, fullTunnelSpec(catalog, { preset })).flatMap((g) => g.params.map((p) => p.key));
    expect(keys('circular')).toEqual(['innerRadius', 'thickness', 'ringWidth', 'ifc']);
    expect(keys('horseshoe')).toEqual(['clearWidth', 'thickness', 'wallThickness', 'blockLength', 'ifc']);
  });

  it('keeps its numbers and formulas when the preset changes, what the preset sets taken over', () => {
    const s = withPreset(catalog, { preset: 'horseshoe', thickness: 0.5, wallThickness: '{= thickness * 1.2}' }, 'circular');
    expect(s).toEqual({ preset: 'circular', wallThickness: '{= thickness * 1.2}' });
    expect(fullTunnelSpec(catalog, s).thickness).toBe(0.4);
  });

  it('is in rings only as a TBM ring of its own section; says where its axis came from', () => {
    expect(isRings({ preset: 'circular' })).toBe(true);
    expect(isRings({ preset: 'circular', profileId: 'p1' })).toBe(false);
    expect(isRings({ preset: 'box' })).toBe(false);
    expect(axisSourceOf({ axisSource: { id: 'r1', from: 120, to: 290 } })).toEqual({ id: 'r1', from: 120, to: 290 });
    expect(axisSourceOf({})).toBeNull();
  });
});
