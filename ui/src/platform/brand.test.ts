import { describe, expect, it } from 'vitest';
import { resolve } from 'node:path';
import { rebrandAs } from './brand';
import { resolveProduct } from '../../../scripts/product.mjs';

const repo = resolve(__dirname, '../../..');

describe('a product under its own brand', () => {
  it('Lapin says nothing of a brand and keeps its own', () => {
    const p = resolveProduct(repo, 'lapin');
    expect(p.brand.name).toBe('Lapin');
    expect(p.brand.short).toBe('Lapin');
    expect(p.brand.mark).toBeUndefined();
  });

  it('puts the short name where the interface says Lapin, not in class names', () => {
    expect(rebrandAs('Open separately, in another Lapin window', 'Acme')).toBe('Open separately, in another Acme window');
    expect(rebrandAs("Lapin's default", 'Acme')).toBe("Acme's default");
    expect(rebrandAs('Construction (Lapin)', 'Acme')).toBe('Construction (Acme)');
    expect(rebrandAs('LapinTrack · Lapin_Stationing', 'Acme')).toBe('LapinTrack · Lapin_Stationing');
    expect(rebrandAs('Deschide în altă fereastră Lapin', 'Acme')).toBe('Deschide în altă fereastră Acme');
    expect(rebrandAs('Open separately, in another Lapin window', 'Lapin')).toBe('Open separately, in another Lapin window');
  });
});
