import { describe, expect, it } from 'vitest';
import { guessLook, lookOf, LOOK_RANGES, PLAIN, rotateUv } from './look';
import { textureOf } from './textures';
import { freeName, matches, PRESETS, presetArgs } from '../modules/core/materialLibrary';
import type { NodeDto } from '../engine/protocol';

const mat = (name: string, props: Record<string, unknown> = {}): NodeDto => ({ id: 'm', type: 'IfcMaterial', name, props } as unknown as NodeDto);

describe('a material’s look (report #76)', () => {
  it('comes from its name as the engine guesses it', () => {
    expect(guessLook('Oak parquet').roughness).toBe(0.45);
    expect(guessLook('Laminate').roughness).toBe(0.45);
    expect(guessLook('Glass blue')).toMatchObject({ transparency: 0.6, transmission: 0.9, ior: 1.52 });
    expect(guessLook('Glass wool').transmission).toBe(0);
    expect(guessLook('Aluminium anodised')).toMatchObject({ metalness: 1, roughness: 0.3 });
    expect(guessLook('Aluminium RAL 7016')).toMatchObject({ metalness: 0.1, roughness: 0.45 });
    expect(guessLook('Sticlă').transmission).toBe(0.9);
    expect(guessLook('Something else')).toEqual(PLAIN);
  });

  it('keeps what the material sets and takes the rest from the name', () => {
    const l = lookOf(mat('Glass blue', { roughness: 0.2, emissive: '' }));
    expect(l.roughness).toBe(0.2);
    expect(l.transparency).toBe(0.6);
    expect(l.emissive).toBeNull();
    expect(lookOf(undefined)).toEqual(PLAIN);
  });

  it('turns texture coordinates as the exports do', () => {
    const [u, v] = rotateUv(1, 0, 90);
    expect(u).toBeCloseTo(0);
    expect(v).toBeCloseTo(-1);
    expect(rotateUv(2, 3, 0)).toEqual([2, 3]);
  });

  it('sees parquet and laminate as wood', () => {
    expect(textureOf(mat('Parquet'))).toBe('wood');
    expect(textureOf(mat('Laminate flooring'))).toBe('wood');
    expect(textureOf(mat('Parchet stejar'))).toBe('wood');
  });
});

describe('the material library', () => {
  it('has unique names and values in the engine’s ranges', () => {
    expect(new Set(PRESETS.map((p) => p.name)).size).toBe(PRESETS.length);
    for (const p of PRESETS) {
      expect(p.color).toMatch(/^#[0-9a-f]{6}$/);
      for (const k of ['roughness', 'metalness', 'transparency', 'transmission', 'ior'] as const)
        if (p[k] !== undefined) { expect(p[k]).toBeGreaterThanOrEqual(LOOK_RANGES[k][0]); expect(p[k]).toBeLessThanOrEqual(LOOK_RANGES[k][1]); }
    }
    expect(PRESETS.some((p) => p.name === 'Aluminium RAL 7016 anthracite')).toBe(true);
    expect(PRESETS.filter((p) => p.group === 'Glass').every((p) => (p.transmission ?? 0) > 0)).toBe(true);
  });

  it('adds a preset under a name the project does not have', () => {
    const glass = PRESETS.find((p) => p.name === 'Glass blue tinted')!;
    expect(presetArgs(glass)).toMatchObject({ name: 'Glass blue tinted', transparency: 0.6, transmission: 0.9 });
    expect(freeName('Glass blue tinted', ['glass blue tinted'])).toBe('Glass blue tinted 2');
    expect(freeName('Oak', ['Walnut'])).toBe('Oak');
  });

  it('finds materials by every word searched', () => {
    expect(matches('Aluminium RAL 7016 anthracite', 'ral alu')).toBe(true);
    expect(matches('Sticlă albastră', 'sticla')).toBe(true);
    expect(matches('Brick', 'glass')).toBe(false);
  });
});
