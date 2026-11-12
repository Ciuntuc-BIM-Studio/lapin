import { describe, expect, it } from 'vitest';
import { installExtension, sha256Hex, uninstallExtension, type ContentInstaller, type InstalledExtension } from './install';
import type { ExtensionKind } from './manifest';
import { furniturePack } from './fixtures';
import { readPriceList, readTypePack, writePriceList } from './packs';
import { typesInstaller, type TypePackRow } from './typesKind';

const b = (v: unknown) => new TextEncoder().encode(typeof v === 'string' ? v : JSON.stringify(v));

const pack = {
  format: 'lapin-type-pack', version: 1, name: 'Pereți RO',
  types: [
    { ref: 'w1', class: 'IfcWallType', name: 'WT1 BCA 30', attrs: { mark: 'WT1' }, material: 's1' },
    { ref: 'd1', class: 'IfcDoorType', name: 'D1 900×2100', attrs: { spec: { name: 'D1 900×2100' } } },
  ],
  resources: [{ ref: 's1', class: 'IfcMaterialLayerSet', name: 'WT1 BCA 30', attrs: { layers: [{ material: 'm1', thickness: 0.3 }] } }, { ref: 'm1', class: 'IfcMaterial', name: 'BCA', attrs: {} }],
};

describe('a type pack\'s file', () => {
  it('lists its types, and is refused when it is not one', () => {
    const r = readTypePack(b(pack));
    expect(r.name).toBe('Pereți RO');
    expect(r.types).toEqual([{ ref: 'w1', class: 'IfcWallType', name: 'WT1 BCA 30' }, { ref: 'd1', class: 'IfcDoorType', name: 'D1 900×2100' }]);
    expect(JSON.parse(r.text).format).toBe('lapin-type-pack');
    expect(() => readTypePack(b({ ...pack, format: 'lapin-types' }))).toThrow('The file is not a Lapin type pack (.lapintypes).');
    expect(() => readTypePack(b({ ...pack, version: 2 }))).toThrow(/newer Lapin/);
    expect(() => readTypePack(b({ ...pack, types: [] }))).toThrow('The type pack has no type.');
    expect(() => readTypePack(b({ ...pack, types: [{ ref: 'x', class: 'IfcWall', name: 'A wall', attrs: {} }] }))).toThrow('A type pack holds types only.');
    expect(() => readTypePack(b({ ...pack, types: [{ ref: 'x', class: 'IfcWallType', name: 'No attrs' }] }))).toThrow(/a ref, a class, a name and its attrs/);
  });
});

describe('a price list written as .lapinprices', () => {
  it('is the file the price list kind reads, always indicative', () => {
    const text = writePriceList({ name: 'Prețurile mele', currency: 'RON', rows: [{ c: 'CA01', n: 'Zidărie BCA', u: 'm3', m: 410, l: 120 }] });
    expect(JSON.parse(text)).toMatchObject({ format: 'lapin-price-list', version: 1, indicative: true });
    expect(readPriceList(b(text))).toEqual({ name: 'Prețurile mele', currency: 'RON', rows: [{ c: 'CA01', n: 'Zidărie BCA', u: 'm3', m: 410, l: 120 }] });
  });
});

describe('the types kind', () => {
  function setup(open = true) {
    const kept = new Map<string, TypePackRow>();
    const brought: string[] = [];
    const installer = typesInstaller({
      rows: { all: async () => [...kept.values()], put: async (r) => { kept.set(r.id, r); }, remove: async (id) => { kept.delete(id); } },
      bring: async (text) => { if (!open) return null; brought.push(text); return { created: 2, reused: 0 }; },
      now: () => '2026-11-12T10:00:00Z',
    });
    const store = new Map<string, InstalledExtension>();
    const deps = {
      app: { version: '0.1.900', browser: true, hasModule: (id: string) => id === 'core' },
      installers: new Map<ExtensionKind, ContentInstaller>([['types', installer]]),
      store: { all: () => [...store.values()], put: (x: InstalledExtension) => { store.set(x.id, x); }, remove: (id: string) => { store.delete(id); } },
      confirm: () => true,
    };
    return { kept, brought, installer, deps };
  }

  async function manifestFor(bytes: Uint8Array) {
    return furniturePack({ id: 'ro.ciuntucbimstudio.pereti-ro', name: 'Pereți RO', kind: 'types', contents: { file: 'pereti-ro.lapintypes' }, package: { url: 'https://example.com/pereti-ro.lapintypes', sha256: await sha256Hex(bytes), size: bytes.length } });
  }

  it('keeps the pack, brings its types into the open project, and forgets the pack when uninstalled', async () => {
    const s = setup();
    const bytes = b(pack);
    const m = await manifestFor(bytes);
    const x = await installExtension(m, { ...s.deps, fetchBytes: async () => bytes });
    expect(x.receipt).toEqual({ pack: m.id, types: ['WT1 BCA 30', 'D1 900×2100'], project: { created: 2, reused: 0 } });
    expect(s.kept.get(m.id)?.types).toEqual(['WT1 BCA 30', 'D1 900×2100']);
    expect(s.brought).toHaveLength(1);
    // Installed → Add its types to this project: the kept pack again.
    s.installer.open!.run(x);
    await new Promise((r) => setTimeout(r, 0));
    expect(s.brought).toHaveLength(2);
    await uninstallExtension(m.id, s.deps);
    expect(s.kept.size).toBe(0);
    expect(s.deps.store.all()).toEqual([]);
  });

  it('is kept with no project open, and refuses a file that is not a type pack', async () => {
    const s = setup(false);
    const bytes = b(pack);
    const x = await installExtension(await manifestFor(bytes), { ...s.deps, fetchBytes: async () => bytes });
    expect(x.receipt.project).toBeNull();
    expect(s.kept.size).toBe(1);
    const bad = b({ format: 'lapin-furniture-library', version: 1, items: [] });
    await expect(installExtension(await manifestFor(bad), { ...setup().deps, fetchBytes: async () => bad })).rejects.toThrow('The file is not a Lapin type pack (.lapintypes).');
  });

  it('needs the core module, which every edition has', async () => {
    const s = setup();
    const bytes = b(pack);
    await expect(installExtension(await manifestFor(bytes), { ...s.deps, app: { ...s.deps.app, hasModule: () => false }, fetchBytes: async () => bytes })).rejects.toThrow('This edition of Lapin has no core module.');
  });
});
