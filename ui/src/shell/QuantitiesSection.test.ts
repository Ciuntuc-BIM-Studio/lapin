import { describe, expect, it } from 'vitest';
import { shown, type Quantity } from './QuantitiesSection';
import { translate } from '../i18n';

const q = (name: string, value: number, label: string): Quantity => ({ name, value, kind: 'VOLUME', label, unit: 'm³' });

describe("a terrain's quantities (filming the terrain tutorial, 19.11.2026)", () => {
  // As the engine lists them: a 1.4 m platform with 2:1 slopes, its cut and fill beside the net.
  const terrain = [q('PlanArea', 1600, 'Plan area'), q('CutVolume', 12.3, 'Cut'), q('FillVolume', 29.7, 'Fill'), q('NetVolume', -17.4, 'Net cut − fill')];

  it('shows the cut and the fill, each beside the net', () => {
    expect(terrain.filter((x) => shown(x, terrain)).map((x) => x.name)).toEqual(['CutVolume', 'FillVolume', 'NetVolume']);
  });

  it("shows each platform's own cut and fill", () => {
    const pad = [q('CutVolume', 12.3, 'Cut'), q('FillVolume', 29.7, 'Fill')];
    expect(pad.filter((x) => shown(x, pad)).map((x) => x.name)).toEqual(['CutVolume', 'FillVolume']);
  });

  it("names them as the ground's, not a section's cut or a hatch's fill", async () => {
    const { loadLanguage } = await import('../i18n');
    await loadLanguage('ro');
    expect(translate('Cut', 'ro', 'earthwork')).toBe('Săpătură');
    expect(translate('Fill', 'ro', 'earthwork')).toBe('Umplutură');
    expect(translate('Cut', 'ro')).toBe('Tăietură');
    expect(translate('Net cut − fill', 'ro')).toBe('Săpătură netă − umplutură');
  });
});
