import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  addSources, change, cleanSuffix, copyName, DEFAULT_CHOICE, optionsOf, rememberChoice, rememberedChoice, sizeText, toleranceOf, useOptimize,
} from './ifcOptimize';

// This computer's storage, as localStorage is in the app.
const store = new Map<string, string>();
vi.stubGlobal('localStorage', { getItem: (k: string) => store.get(k) ?? null, setItem: (k: string, v: string) => void store.set(k, v), removeItem: (k: string) => void store.delete(k) });

describe('File → Optimize IFC…', () => {
  beforeEach(() => { store.clear(); useOptimize.setState({ items: [], folder: null, running: false }); });

  it('names the copy as the engine does: the ending before the extension, never the original', () => {
    expect(copyName('Bloc-P-4.ifc', '.optimized', false)).toBe('Bloc-P-4.optimized.ifc');
    expect(copyName('Bloc-P-4.IFC', '-lite', true)).toBe('Bloc-P-4-lite.ifczip');
    expect(copyName('house.ifczip', '_v2.ifc', false)).toBe('house_v2.ifc');
    // An ending of nothing (or of a folder) is not one: the default comes back.
    expect(cleanSuffix('  ')).toBeNull();
    expect(cleanSuffix('../x')).toBe('..x');
    expect(copyName('a.ifc', '', false)).toBe('a.optimized.ifc');
  });

  it('takes the tolerance from the preset, or the number typed', () => {
    expect(toleranceOf(DEFAULT_CHOICE)).toBe(1);
    expect(toleranceOf({ ...DEFAULT_CHOICE, preset: 'fast' })).toBe(5);
    expect(toleranceOf({ ...DEFAULT_CHOICE, preset: 'precise' })).toBe(0.1);
    expect(toleranceOf({ ...DEFAULT_CHOICE, preset: 'custom', toleranceMm: '0,5' })).toBe(0.5);
    expect(toleranceOf({ ...DEFAULT_CHOICE, preset: 'custom', toleranceMm: 'x' })).toBeNull();
    expect(toleranceOf({ ...DEFAULT_CHOICE, preset: 'custom', toleranceMm: '500' })).toBeNull();
  });

  it('sends the safe defaults: simplify, clean, share and orphans on; rounding, face sets and zip off', () => {
    expect(optionsOf(DEFAULT_CHOICE)).toEqual({ simplify: true, toleranceMm: 1, clean: true, share: true, orphans: true, roundMm: 0, brepsToFaceSets: false, zip: false });
    expect(optionsOf({ ...DEFAULT_CHOICE, round: true, zip: true }).roundMm).toBe(0.01);
  });

  it('remembers the choice and the ending on this computer', () => {
    rememberChoice({ ...DEFAULT_CHOICE, suffix: '-light', preset: 'precise' });
    expect(rememberedChoice().suffix).toBe('-light');
    expect(rememberedChoice().preset).toBe('precise');
  });

  it('adds each file once', () => {
    addSources([{ name: 'a.ifc', path: '/x/a.ifc' }, { name: 'b.ifc', path: '/x/b.ifc' }]);
    addSources([{ name: 'a.ifc', path: '/x/a.ifc' }, { name: 'c.ifc', data: 'AAAA' }]);
    expect(useOptimize.getState().items.map((i) => i.source.name)).toEqual(['a.ifc', 'b.ifc', 'c.ifc']);
  });

  it('says what changed, as sizes and shares', () => {
    expect(sizeText(512)).toBe('512 B');
    expect(sizeText(2915375)).toBe('2.8 MB');
    expect(change(2915375, 2344760, sizeText)).toBe('2.8 MB → 2.2 MB (−20 %)');
    expect(change(4944, 1032)).toBe('4,944 → 1,032 (−79 %)');
    expect(change(10, 10)).toBe('10 → 10 (±0 %)');
  });
});
