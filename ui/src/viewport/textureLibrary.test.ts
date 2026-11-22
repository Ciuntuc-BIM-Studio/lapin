import { existsSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { mapUrl, setArgs, setBytes, setKey, setOf, TEXTURE_SETS, toBase64 } from './textureLibrary';
import { hasRelief } from './textures';
import { LOOK_RANGES, lookOf, PLAIN } from './look';
import type { NodeDto } from '../engine/protocol';

const root = join(__dirname, '../../..');

describe('the texture sets Lapin ships (docs/plan-render.md, S1)', () => {
  it('are the starter set: brick, concrete, plaster, wood floor, tiles, stone, metal, fabric, each with its colour and normal maps', () => {
    expect(TEXTURE_SETS.map((s) => s.kind)).toEqual(['brick', 'concrete', 'plaster', 'wood', 'tiles', 'stone', 'metal', 'fabric']);
    for (const s of TEXTURE_SETS) {
      expect(s.files.color, s.id).toBeDefined();
      expect(s.files.normal, s.id).toBeDefined();
      expect(s.files.roughness, s.id).toBeDefined();
      expect(s.size.every((x) => x > 0), s.id).toBe(true);
      for (const f of Object.values(s.files)) expect(existsSync(join(root, f!.file)) && statSync(join(root, f!.file)).size === f!.bytes, f!.file).toBe(true);
    }
  });

  it('stay small: a few megabytes all together, at most half a megabyte a set', () => {
    const total = TEXTURE_SETS.reduce((n, s) => n + setBytes(s), 0);
    expect(total).toBeLessThan(4 * 1024 * 1024);
    for (const s of TEXTURE_SETS) expect(setBytes(s), s.id).toBeLessThan(512 * 1024);
  });

  it('are served under the interface base, and named on a material by source/id', () => {
    const brick = TEXTURE_SETS.find((s) => s.id === 'Bricks085')!;
    expect(mapUrl(brick, 'color', '/')).toBe('/textures/ambientcg/Bricks085/Color.jpg');
    expect(mapUrl(brick, 'normal', '/web')).toBe('/web/textures/ambientcg/Bricks085/Normal.jpg');
    expect(setKey(brick)).toBe('ambientcg/Bricks085');
    expect(setOf('ambientcg/Bricks085')).toBe(brick);
    expect(setOf('ambientcg/Nothing')).toBeUndefined();
    expect(setOf(undefined)).toBeUndefined();
  });

  it('give material.textures its maps, the height as the bump map, with the set, its tile and factors', async () => {
    const brick = TEXTURE_SETS.find((s) => s.id === 'Bricks085')!;
    const seen: string[] = [];
    const args = await setArgs(brick, ['m1'], async (url) => { seen.push(url); return new Uint8Array([0xff, 0xd8, 0xff, seen.length]); }, '/');
    expect(args).toMatchObject({ id: 'm1', set: 'ambientcg/Bricks085', size: [2.4, 1.2], roughness: 1, metalness: 0 });
    const maps = args.maps as Record<string, { mime: string; data: string }>;
    expect(Object.keys(maps).sort()).toEqual(['ao', 'bump', 'color', 'normal', 'roughness']);
    expect(maps.color.mime).toBe('image/jpeg');
    expect(maps.color.data).toBe(toBase64(new Uint8Array([0xff, 0xd8, 0xff, 1])));
    expect(seen).toContain('/textures/ambientcg/Bricks085/Height.jpg');
    const many = await setArgs(brick, ['a', 'b'], async () => new Uint8Array([1]), '/');
    expect(many).toMatchObject({ ids: ['a', 'b'] });
    expect(many).not.toHaveProperty('id');
  });
});

describe('a material\'s surface maps', () => {
  const mat = (props: Record<string, unknown>): NodeDto => ({ id: 'm', type: 'IfcMaterial', name: 'Brick', parentId: null, props } as unknown as NodeDto);

  it('take an occlusion strength from 0 to 1, 1 by default', () => {
    expect(PLAIN.aoStrength).toBe(1);
    expect(LOOK_RANGES.aoStrength).toEqual([0, 1]);
    expect(lookOf(mat({ aoStrength: 0.4 })).aoStrength).toBe(0.4);
    expect(lookOf(mat({})).aoStrength).toBe(1);
  });

  it('need texture coordinates when any map is there', () => {
    expect(hasRelief({ normal: null, bump: null })).toBe(false);
    expect(hasRelief({ normal: null, bump: null, roughness: null, ao: null })).toBe(false);
    expect(hasRelief({ normal: null, bump: null, roughness: { id: 'r', url: '', width: 1, height: 1 } })).toBe(true);
    expect(hasRelief({ normal: null, bump: null, ao: { id: 'a', url: '', width: 1, height: 1 } })).toBe(true);
  });
});
