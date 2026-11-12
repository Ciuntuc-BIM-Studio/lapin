import { describe, expect, it } from 'vitest';
import { countByKind, filterCatalogue, installExtension, resolveUrl, sha256Hex, uninstallExtension, updatesOf, type ContentInstaller, type InstalledExtension, type InstallDeps } from './install';
import type { ExtensionKind, Manifest } from './manifest';
import { furniturePack } from './fixtures';

const bytesOf = (s: string) => new TextEncoder().encode(s);

/** A fake library of the furniture kind: what it holds, by id. */
function fakeFurniture() {
  const library = new Map<string, string>();
  let n = 0;
  const installer: ContentInstaller = {
    kind: 'furniture',
    install: async (file) => {
      const items = JSON.parse(new TextDecoder().decode(file.bytes)).items as string[];
      const ids = items.map((name) => { const id = `f${++n}`; library.set(id, name); return id; });
      return { ids };
    },
    uninstall: async (r) => { for (const id of r.ids as string[]) library.delete(id); },
  };
  return { library, installer };
}

function memoryStore() {
  const rows = new Map<string, InstalledExtension>();
  return { all: () => [...rows.values()], put: (x: InstalledExtension) => { rows.set(x.id, x); }, remove: (id: string) => { rows.delete(id); } };
}

async function packed(items: string[], over: Partial<Manifest> = {}) {
  const bytes = bytesOf(JSON.stringify({ items }));
  return { bytes, manifest: furniturePack({ ...over, package: { url: 'packs/mobilier.lapinlib', sha256: await sha256Hex(bytes), size: bytes.length } }) };
}

function deps(files: Record<string, Uint8Array>, installers: ContentInstaller[], store = memoryStore(), over: Partial<InstallDeps> = {}): InstallDeps & { fetched: string[] } {
  const fetched: string[] = [];
  return {
    app: { version: '0.1.870', browser: false, hasModule: () => true },
    fetchBytes: async (url) => { fetched.push(url); const b = files[url]; if (!b) throw new Error(`404 ${url}`); return b; },
    installers: new Map<ExtensionKind, ContentInstaller>(installers.map((i) => [i.kind, i])),
    store, confirm: () => true, base: '/Users/me/catalogue/index.json', now: () => '2026-11-12T10:00:00Z', fetched,
    ...over,
  };
}

describe('installing an extension', () => {
  it('downloads the package beside the index, checks its SHA-256 and takes it into its library', async () => {
    const { bytes, manifest } = await packed(['Oak bookcase', 'TV unit']);
    const f = fakeFurniture();
    const d = deps({ '/Users/me/catalogue/packs/mobilier.lapinlib': bytes }, [f.installer]);
    const x = await installExtension(manifest, d);
    expect(d.fetched).toEqual(['/Users/me/catalogue/packs/mobilier.lapinlib']);
    expect([...f.library.values()]).toEqual(['Oak bookcase', 'TV unit']);
    expect(x).toMatchObject({ id: manifest.id, version: '1.0.0', kind: 'furniture', receipt: { ids: ['f1', 'f2'] }, at: '2026-11-12T10:00:00Z' });
    expect(d.store.all()).toHaveLength(1);
  });

  it('installs nothing from a package that is not the one its manifest names', async () => {
    const { manifest } = await packed(['Oak bookcase']);
    const f = fakeFurniture();
    const tampered = bytesOf(JSON.stringify({ items: ['Oak bookcasf'] }));
    const d = deps({ '/Users/me/catalogue/packs/mobilier.lapinlib': tampered }, [f.installer]);
    await expect(installExtension(manifest, d)).rejects.toThrow(/SHA-256 differs/);
    const short = deps({ '/Users/me/catalogue/packs/mobilier.lapinlib': bytesOf('{}') }, [f.installer]);
    await expect(installExtension(manifest, short)).rejects.toThrow(/its manifest says/);
    expect(f.library.size).toBe(0);
    expect(d.store.all()).toEqual([]);
  });

  it('refuses what this Lapin cannot take, before downloading', async () => {
    const { bytes, manifest } = await packed(['Oak bookcase'], { lapin: '>=0.2.0' });
    const f = fakeFurniture();
    const d = deps({ '/Users/me/catalogue/packs/mobilier.lapinlib': bytes }, [f.installer]);
    await expect(installExtension(manifest, d)).rejects.toThrow('It works with Lapin >=0.2.0; this is Lapin 0.1.870.');
    const none = deps({}, []);
    await expect(installExtension((await packed(['x'])).manifest, none)).rejects.toThrow('This edition of Lapin cannot install furniture.');
    expect(d.fetched).toEqual([]);
  });

  it('reads the browser\'s copy from lapinbim.com when there is one', async () => {
    const { bytes, manifest } = await packed(['Oak bookcase']);
    const mirrored = { ...manifest, package: { ...manifest.package, url: 'https://github.com/a/b/releases/download/v1/m.lapinlib', mirror: 'https://lapinbim.com/extensions/files/m.lapinlib' } };
    const f = fakeFurniture();
    const d = deps({ 'https://lapinbim.com/extensions/files/m.lapinlib': bytes }, [f.installer], memoryStore(), { app: { version: '0.1.870', browser: true, hasModule: () => true } });
    await installExtension(mirrored, d);
    expect(d.fetched).toEqual(['https://lapinbim.com/extensions/files/m.lapinlib']);
  });

  it('uninstalls what it added only, and updates by taking the old version away first', async () => {
    const f = fakeFurniture();
    f.library.set('mine', 'My own chair');
    const v1 = await packed(['Oak bookcase', 'TV unit']);
    const v2 = await packed(['Oak bookcase', 'TV unit', 'Sideboard'], { version: '1.1.0' });
    const store = memoryStore();
    const d = deps({ '/Users/me/catalogue/packs/mobilier.lapinlib': v1.bytes }, [f.installer], store);
    await installExtension(v1.manifest, d);
    expect(updatesOf(store.all(), [v2.manifest], d.app).map((m) => m.version)).toEqual(['1.1.0']);
    expect(updatesOf(store.all(), [v1.manifest], d.app)).toEqual([]);
    const d2 = deps({ '/Users/me/catalogue/packs/mobilier.lapinlib': v2.bytes }, [f.installer], store);
    await installExtension(v2.manifest, d2);
    expect([...f.library.values()].sort()).toEqual(['My own chair', 'Oak bookcase', 'Sideboard', 'TV unit']);
    expect(store.all().map((x) => x.version)).toEqual(['1.1.0']);
    await uninstallExtension(v2.manifest.id, d2);
    expect([...f.library.values()]).toEqual(['My own chair']);
    expect(store.all()).toEqual([]);
  });

  it('reads a package path against the index', () => {
    expect(resolveUrl('https://lapinbim.com/extensions/index.json', 'files/a.lapinlib')).toBe('https://lapinbim.com/extensions/files/a.lapinlib');
    expect(resolveUrl('C:\\cat\\index.json', 'dist\\a.pat')).toBe('C:\\cat\\dist\\a.pat');
    expect(resolveUrl('/cat/index.json', 'https://github.com/x')).toBe('https://github.com/x');
    expect(resolveUrl('/cat/index.json', '/abs/a.pat')).toBe('/abs/a.pat');
  });
});

describe('Browse', () => {
  const list = [
    furniturePack(),
    furniturePack({ id: 'ro.x.sablon', name: 'Șablon România', summary: 'Niveluri și planșe', kind: 'template', contents: { file: 'ro.lapin' } }),
    furniturePack({ id: 'ro.x.script', name: 'Perimeter walls', summary: 'Walls round a rectangle', kind: 'script', contents: { file: 'p.py' }, tags: ['walls'] }),
  ];
  it('searches names, summaries, tags and items, in a category', () => {
    expect(filterCatalogue(list, { search: 'walls' }).map((m) => m.kind)).toEqual(['script']);
    expect(filterCatalogue(list, { search: 'bibliotecă' }).map((m) => m.kind)).toEqual(['furniture']);
    expect(filterCatalogue(list, { kind: 'template' }).map((m) => m.name)).toEqual(['Șablon România']);
    expect(filterCatalogue(list, {}).map((m) => m.name)).toEqual(['Mobilier living', 'Perimeter walls', 'Șablon România']);
    expect(countByKind(list)).toEqual({ furniture: 1, template: 1, script: 1 });
  });
});
