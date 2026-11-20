import { beforeEach, describe, expect, it, vi } from 'vitest';

const stored = new Map<string, string>();
let quota = Infinity;
vi.stubGlobal('localStorage', {
  getItem: (k: string) => stored.get(k) ?? null,
  setItem: (k: string, v: string) => {
    if (v.length > quota) throw new Error('QuotaExceededError');
    stored.set(k, v);
  },
  removeItem: (k: string) => void stored.delete(k),
});

const {
  applyRoofTemplateArgs,
  exportRoofTemplatesJson,
  forgetRoofTemplate,
  importRoofTemplates,
  isUserRoofTemplateId,
  libraryRoofTemplate,
  libraryRoofTemplates,
  parseRoofTemplatesJson,
  renameRoofTemplate,
  ROOF_TEMPLATE_FIELDS,
  saveRoofTemplate,
  subscribeRoofTemplates,
} = await import('./roofTemplateLibrary');

beforeEach(() => {
  stored.clear();
  quota = Infinity;
});

describe('roofTemplateLibrary', () => {
  it('saves a roof configuration as a reusable user template without location fields', () => {
    const res = saveRoofTemplate('Alpine Gable', {
      shape: 'gable',
      pitch: 38,
      overhang: 0.6,
      gableOverhang: 0.4,
      covering: 'clay-tiles',
      coveringMaterial: 'Clay tile red',
      structure: 'rafters',
      rafterSection: '80x160 mm',
      rafterSpacing: 0.75,
      gutterProfile: 'half-round',
      gutterColor: '#445566',
      // Location-specific or non-template fields that must NOT be captured:
      contour: [[0, 0], [10, 0], [10, 8], [0, 8]],
      eaveOffset: 0.25,
      edges: [{ kind: 'gable' }],
      style: 'own:1',
      openings: [{ id: 'op1' }],
    });

    expect(res.status).toBe('kept');
    if (res.status !== 'kept') return;

    expect(isUserRoofTemplateId(res.template.id)).toBe(true);
    expect(res.template.label).toBe('Alpine Gable');
    expect(res.template.shape).toBe('gable');
    expect(res.template.pitch).toBe(38);
    expect(res.template.structure).toBe('rafters');
    expect(res.template.added).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(res.template.attrs.coveringMaterial).toBe('Clay tile red');
    expect(res.template.attrs.gutterColor).toBe('#445566');
    expect(res.template.attrs.contour).toBeUndefined();
    expect(res.template.attrs.eaveOffset).toBeUndefined();
    expect(res.template.attrs.edges).toBeUndefined();
    expect(res.template.attrs.style).toBeUndefined();
    expect(res.template.attrs.openings).toBeUndefined();

    expect(libraryRoofTemplates()).toHaveLength(1);
    expect(libraryRoofTemplate(res.template.id)?.label).toBe('Alpine Gable');
  });

  it('detects duplicate names unless overwrite is true', () => {
    const first = saveRoofTemplate('Modern Flat', { shape: 'flat', pitch: 2, deckThickness: 0.2 });
    expect(first.status).toBe('kept');

    const dup = saveRoofTemplate('modern flat', { shape: 'flat', pitch: 3, deckThickness: 0.25 });
    expect(dup.status).toBe('exists');
    expect(libraryRoofTemplates()).toHaveLength(1);

    const replaced = saveRoofTemplate('Modern Flat', { shape: 'flat', pitch: 3, deckThickness: 0.25 }, { overwrite: true });
    expect(replaced.status).toBe('kept');
    expect(libraryRoofTemplates()).toHaveLength(1);
    expect(libraryRoofTemplates()[0].attrs.deckThickness).toBe(0.25);
  });

  it('renames, deletes, and notifies subscribers', () => {
    const listener = vi.fn();
    const unsub = subscribeRoofTemplates(listener);

    const saved = saveRoofTemplate('Shed Metal', { shape: 'shed', pitch: 15, covering: 'metal-sheet' });
    expect(saved.status).toBe('kept');
    if (saved.status !== 'kept') return;
    expect(listener).toHaveBeenCalledTimes(1);

    expect(renameRoofTemplate(saved.template.id, 'Shed Standing Seam')).toBe(true);
    expect(libraryRoofTemplate(saved.template.id)?.label).toBe('Shed Standing Seam');
    expect(listener).toHaveBeenCalledTimes(2);

    forgetRoofTemplate(saved.template.id);
    expect(libraryRoofTemplates()).toHaveLength(0);
    expect(listener).toHaveBeenCalledTimes(3);

    unsub();
  });

  it('computes applyRoofTemplateArgs with null for absent fields and scales barrel riseShare', () => {
    const barrelContour: [number, number][] = [[0, 0], [10, 0], [10, 6], [0, 6]]; // span = 6m
    const saved = saveRoofTemplate(
      'Vaulted Hall',
      { shape: 'barrel', rise: 1.5, covering: 'metal-sheet', deckMaterial: 'Timber CLT', deckThickness: 0.14 },
      { contour: barrelContour },
    );
    expect(saved.status).toBe('kept');
    if (saved.status !== 'kept') return;

    expect(saved.template.attrs.riseShare).toBe(0.25);

    // Apply to a larger 12x8m barrel roof -> rise should scale to 8 * 0.25 = 2m
    const targetContour: [number, number][] = [[0, 0], [12, 0], [12, 8], [0, 8]];
    const args = applyRoofTemplateArgs('roof-42', saved.template, targetContour);
    expect(args.id).toBe('roof-42');
    expect(args.edges).toEqual([]);
    expect(args.attrs.shape).toBe('barrel');
    expect(args.attrs.rise).toBe(2);
    expect(args.attrs.riseShare).toBeUndefined();
    expect(args.attrs.deckMaterial).toBe('Timber CLT');
    // Unset fields must be explicitly null so RoofDraft clears stale attributes
    expect(args.attrs.upperPitch).toBeNull();
    expect(args.attrs.rafterSection).toBeNull();
    for (const k of ROOF_TEMPLATE_FIELDS) {
      expect(k in args.attrs).toBe(true);
    }
  });

  it('exports and imports roof templates safely, filtering unknown keys', () => {
    const saved = saveRoofTemplate('Mansard Slate', {
      shape: 'mansard',
      pitch: 70,
      upperPitch: 25,
      mansardInset: 1.2,
      covering: 'slate',
    });
    expect(saved.status).toBe('kept');
    if (saved.status !== 'kept') return;

    const jsonText = exportRoofTemplatesJson([saved.template]);
    const parsed = parseRoofTemplatesJson(JSON.parse(jsonText));
    expect(parsed).toHaveLength(1);
    expect(parsed[0].label).toBe('Mansard Slate');
    expect(parsed[0].attrs.upperPitch).toBe(25);

    // Unknown keys in imported payload are stripped
    const dirtyParsed = parseRoofTemplatesJson({
      label: 'Imported Hip',
      attrs: { shape: 'hip', pitch: 30, bogusKey: 'evil', contour: [[0, 0]] },
    });
    expect(dirtyParsed).toHaveLength(1);
    expect(dirtyParsed[0].attrs.bogusKey).toBeUndefined();
    expect(dirtyParsed[0].attrs.contour).toBeUndefined();

    // Importing into a clean library adds both
    stored.clear();
    const res1 = importRoofTemplates([...parsed, ...dirtyParsed]);
    expect(res1).toEqual({ added: 2, updated: 0, skipped: 0, full: false });
    expect(libraryRoofTemplates()).toHaveLength(2);

    // Re-importing without overwrite skips duplicates; with overwrite updates them
    const res2 = importRoofTemplates(parsed, false);
    expect(res2).toEqual({ added: 0, updated: 0, skipped: 1, full: false });
    const res3 = importRoofTemplates(parsed, true);
    expect(res3).toEqual({ added: 0, updated: 1, skipped: 0, full: false });
  });

  it('handles storage quota errors and malformed localStorage gracefully', () => {
    stored.set('lapin.roofTemplates', '{broken json');
    expect(libraryRoofTemplates()).toEqual([]);

    quota = 10;
    const res = saveRoofTemplate('Too Big', { shape: 'hip', pitch: 30 });
    expect(res.status).toBe('full');
  });
  it('keeps the roof\'s layers, extents, sections and edges, and puts the edges on a roof with as many corners', () => {
    const contour: [number, number][] = [[0, 0], [10, 0], [10, 5.5], [8.5, 7], [0, 7]];
    const layers = [{ material: 'Mineral wool', thickness: 0.16, role: 'insulation' }, { material: 'OSB', thickness: 0.022 }];
    const layerExtents = [{ layer: 0, edge: 'outline', offset: 0.1 }, { layer: 1, edge: 'edge', index: 2, offset: -0.2 }];
    const saved = saveRoofTemplate('Curved hip roof', {
      shape: 'hip', pitch: 35, overhang: 0.8, layers, layerExtents,
      plateSection: '120x120', ridgeSection: '140x200', purlinSection: '120x160', collarSection: '60x160',
      contour, arcs: [-1.5, 0, 0.62, 0, 0], edges: [null, { pitch: 40 }, null, { kind: 'gable', overhang: 0.3 }, { gutter: false, bogus: 1 }],
    }, { contour });
    expect(saved.status).toBe('kept');
    if (saved.status !== 'kept') return;
    const tpl = saved.template;
    expect(tpl.version).toBe(2);
    expect(tpl.attrs.layers).toEqual(layers);
    expect(tpl.attrs.layerExtents).toEqual(layerExtents);
    expect(tpl.attrs.plateSection).toBe('120x120');
    expect(tpl.attrs.ridgeSection).toBe('140x200');
    expect(tpl.attrs.purlinSection).toBe('120x160');
    expect(tpl.attrs.collarSection).toBe('60x160');
    expect(tpl.outline).toEqual({
      corners: 5,
      edges: [null, { pitch: 40 }, null, { kind: 'gable', overhang: 0.3 }, { gutter: false }],
      arcs: [-1.5, 0, 0.62, 0, 0],
    });

    // A roof with five corners takes its edges and arcs too, in the same step.
    const five: [number, number][] = [[0, 0], [12, 0], [12, 6], [10, 8], [0, 8]];
    const onFive = applyRoofTemplateArgs('r5', tpl, five);
    expect(onFive.arcs).toEqual([-1.5, 0, 0.62, 0, 0]);
    expect(onFive.edges).toEqual(tpl.outline!.edges);
    expect(onFive.attrs.layers).toEqual(layers);
    expect(onFive.attrs.layerExtents).toEqual(layerExtents);

    // A rectangle: the roof-wide fields, its own edges cleared, its arcs as they are, no extent named by an edge.
    const onFour = applyRoofTemplateArgs('r4', tpl, [[0, 0], [8, 0], [8, 6], [0, 6]]);
    expect(onFour.arcs).toBeUndefined();
    expect(onFour.edges).toEqual([]);
    expect(onFour.attrs.layers).toEqual(layers);
    expect(onFour.attrs.layerExtents).toEqual([{ layer: 0, edge: 'outline', offset: 0.1 }]);
    expect(onFour.attrs.pitch).toBe(35);

    // Exported and imported, it keeps all of it.
    const back = parseRoofTemplatesJson(JSON.parse(exportRoofTemplatesJson([tpl])))[0];
    expect(back.version).toBe(2);
    expect(back.outline).toEqual(tpl.outline);
    expect(back.attrs.layers).toEqual(layers);
  });

  it('applies a template saved before version 2 as it always did: the roof keeps its layers and sections', () => {
    // As an older build kept it in this computer's library: no version, no outline.
    stored.set('lapin.roofTemplates', JSON.stringify([{ id: 'tpl:old', label: 'Old hip', shape: 'hip', pitch: 30, attrs: { shape: 'hip', pitch: 30, covering: 'clay-tiles' }, added: '2026-11-01' }]));
    const old = libraryRoofTemplate('tpl:old')!;
    expect(old).toBeTruthy();
    const args = applyRoofTemplateArgs('r', old, [[0, 0], [8, 0], [8, 6], [0, 6]]);
    expect(args.attrs.covering).toBe('clay-tiles');
    expect(args.attrs.rafterSection).toBeNull();
    for (const k of ['layers', 'layerExtents', 'plateSection', 'ridgeSection', 'purlinSection', 'collarSection']) expect(k in args.attrs).toBe(false);
    expect(args.edges).toEqual([]);
    expect(args.arcs).toBeUndefined();
    // An old exported file (no version) imports, and applies the same way.
    const imported = parseRoofTemplatesJson({ format: 'lapin-roof-templates', version: 1, templates: [{ label: 'Old file', attrs: { shape: 'gable', pitch: 40 } }] });
    expect(imported).toHaveLength(1);
    expect('layers' in applyRoofTemplateArgs('r', imported[0], [[0, 0], [8, 0], [8, 6], [0, 6]]).attrs).toBe(false);
  });
});
