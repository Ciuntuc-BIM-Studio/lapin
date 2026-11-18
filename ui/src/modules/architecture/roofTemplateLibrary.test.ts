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
});
