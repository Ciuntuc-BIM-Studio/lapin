import { describe, expect, it } from 'vitest';
import { catalogueOf, exportedFile, inCatalogue, MATERIAL_IMPORT, measureOf, paramKey, parseThicknesses, PARAM_PRESETS, unitOf } from './materialData';
import type { ModelState } from '../../engine/model';

const model = (nodes: { id: string; type: string; name: string; props: Record<string, unknown> }[]): ModelState =>
  ({ nodes: new Map(nodes.map((n) => [n.id, n])), relations: new Map() }) as unknown as ModelState;

describe('a material’s data', () => {
  it('reads a typed list of catalogue thicknesses, refusing what is not one', () => {
    expect(parseThicknesses('200, 50 100;150, 50')).toEqual([50, 100, 150, 200]);
    expect(parseThicknesses('12,5')).toEqual([5, 12]);
    expect(parseThicknesses('')).toEqual([]);
    expect(parseThicknesses('50, abc')).toBeNull();
    expect(parseThicknesses('0')).toBeNull();
  });

  it('finds a material’s catalogue by its name and flags a thickness not in it', () => {
    const m = model([
      { id: 'w', type: 'IfcMaterial', name: 'Mineral wool', props: { measure: { by: 'area', thicknesses: [50, 100, 150, 200] } } },
      { id: 'b', type: 'IfcMaterial', name: 'Brick', props: {} },
    ]);
    expect(catalogueOf(m, 'mineral wool ')).toEqual([50, 100, 150, 200]);
    expect(catalogueOf(m, 'Brick')).toEqual([]);
    expect(inCatalogue([50, 100, 150, 200], 0.15)).toBe(true);
    expect(inCatalogue([50, 100, 150, 200], 0.13)).toBe(false);
    expect(inCatalogue([], 0.13)).toBe(true);
    expect(inCatalogue([50], 0)).toBe(true);
  });

  it('reads only a measure the engine knows', () => {
    expect(measureOf({ measure: { by: 'volume' } })?.by).toBe('volume');
    expect(measureOf({ measure: { by: 'weight' } })).toBeNull();
    expect(measureOf(undefined)).toBeNull();
  });

  it('offers λ, μ and density in IFC’s own sets, under their exact names', () => {
    const keys = PARAM_PRESETS.map((p) => `${p.param.pset ?? ''}.${p.param.name}`);
    expect(keys).toContain('Pset_MaterialThermal.ThermalConductivity');
    expect(keys).toContain('Pset_MaterialHygroscopic.UpperVaporResistanceFactor');
    expect(keys).toContain('Pset_MaterialCommon.MassDensity');
    expect(PARAM_PRESETS.filter((p) => !p.param.pset).every((p) => !p.param.name.startsWith('Pset_'))).toBe(true);
    expect(unitOf({ pset: 'Pset_MaterialThermal', name: 'ThermalConductivity', kind: 'number', value: 0.035 })).toBe('W/(m·K)');
    expect(paramKey({ name: 'Reaction to fire', kind: 'text', value: 'A1' })).toBe('.reaction to fire');
  });

  it('offers Young’s modulus E in MPa, the name the structural analysis reads (report #189)', () => {
    const e = PARAM_PRESETS.find((p) => p.param.name === "Young's modulus");
    expect(e?.param.kind).toBe('number');
    expect(unitOf({ name: "Young's modulus", kind: 'number', value: 30000 })).toBe('MPa');
    expect(unitOf({ name: 'Yield strength', kind: 'number', value: 355 })).toBe('MPa');
    expect(unitOf({ name: 'Compressive strength', kind: 'number', value: 30 })).toBe('MPa');
  });
});

describe('the materials data table (Import data… / Export data…)', () => {
  it('turns the engine\'s export into the file to save: a workbook from base64, a CSV from text', () => {
    const csv = exportedFile({ text: 'Material;Id\r\nSteel;m1\r\n', file: 'materials-data.csv' })!;
    expect(csv.name).toBe('materials-data.csv');
    expect(csv.type).toBe('text/csv');
    // A byte-order mark first, so Excel reads the names' accents right.
    expect(Array.from(csv.bytes.slice(0, 3))).toEqual([0xef, 0xbb, 0xbf]);
    expect(new TextDecoder().decode(csv.bytes)).toBe('Material;Id\r\nSteel;m1\r\n');
    const xlsx = exportedFile({ data: btoa('PK\u0003\u0004'), file: 'materials-data.xlsx' })!;
    expect(Array.from(xlsx.bytes)).toEqual([0x50, 0x4b, 3, 4]);
    expect(xlsx.type).toBe('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
    expect(exportedFile(null)).toBeNull();
  });

  it('opens the import on the materials target, unmatched rows skipped unless chosen otherwise', () => {
    expect(MATERIAL_IMPORT).toEqual({ target: 'core.materials', title: 'Import material data', defaults: { ifMissing: 'Skip' } });
  });
});
