import { describe, expect, it } from 'vitest';
import { installExtension, sha256Hex, updatesOf, type ContentInstaller, type InstalledExtension } from './install';
import { afterCheck, checkLicenceKey, checkUrlOf, cleanKey, dueForCheck, LicenceError, LICENCE_MESSAGES, RECHECK_MS, type LicenceRecord, type PostJson, type StoreAnswer } from './licence';
import { isPaid, validateManifest, type ExtensionKind, type Manifest } from './manifest';
import { furniturePack } from './fixtures';

const bytesOf = (s: string) => new TextEncoder().encode(s);
const json = (status: number, o: unknown): StoreAnswer => ({ status, type: 'application/json', bytes: bytesOf(JSON.stringify(o)) });

const STORE = 'https://store.lapinbim.com';
const paidPack = (bytes: Uint8Array, sha: string, over: Partial<Manifest> = {}): Manifest => furniturePack({
  id: 'ana.sofas', version: '1.0.0', license: 'LicenseRef-Ana-EULA',
  package: { url: `${STORE}/api/licensed/ana.sofas/1.0.0`, sha256: sha, size: bytes.length },
  purchase: { provider: 'gumroad', productId: 'p-1', checkoutUrl: 'https://ana.gumroad.com/l/sofas', terms: 'https://ana.example/terms', price: '29 EUR' },
  ...over,
});

/** The store as Lapin sees it: good keys get the file, a refunded one 403 revoked, others 403 invalid; the posts recorded. */
function fakeStore(file: Uint8Array, keys: Record<string, 'active' | 'revoked'>) {
  const posts: { url: string; body: Record<string, string> }[] = [];
  let down = false;
  const post: PostJson = async (url, body) => {
    const b = JSON.parse(body) as Record<string, string>;
    posts.push({ url, body: b });
    if (down) throw new Error('offline');
    const st = keys[b.key] ?? 'invalid';
    if (url.endsWith('/api/licence/check')) return json(200, { status: st });
    if (st === 'active') return { status: 200, type: 'application/octet-stream', bytes: file };
    return json(403, { status: st, error: 'no' });
  };
  return { post, posts, goOffline: () => { down = true; } };
}

const furnitureInstaller: ContentInstaller = { kind: 'furniture', install: async () => ({ ids: ['f1'] }), uninstall: async () => undefined };
function memory() {
  const rows = new Map<string, InstalledExtension>();
  return { all: () => [...rows.values()], put: (x: InstalledExtension) => { rows.set(x.id, x); }, remove: (id: string) => { rows.delete(id); } };
}

describe('a paid extension’s manifest', () => {
  it('names its provider, product, checkout and terms; its package only at the store’s licensed address', () => {
    const m = paidPack(bytesOf('x'), 'a'.repeat(64));
    expect(validateManifest(m)).toEqual([]);
    expect(isPaid(m)).toBe(true);
    expect(isPaid(furniturePack())).toBe(false);
    expect(validateManifest({ ...m, purchase: { ...m.purchase!, provider: 'paypal' } }).join(' ')).toMatch(/purchase.provider must be one of gumroad/);
    expect(validateManifest({ ...m, purchase: { ...m.purchase!, checkoutUrl: 'http://x.ro' } }).join(' ')).toMatch(/checkoutUrl/);
    expect(validateManifest({ ...m, package: { ...m.package, url: 'https://github.com/a/b/x.lapinlib' } }).join(' ')).toMatch(/api\/licensed\/ana.sofas\/1.0.0/);
    expect(validateManifest({ ...m, package: { ...m.package, mirror: 'https://lapinbim.com/extensions/files/x' } }).join(' ')).toMatch(/no public copy/);
    expect(checkUrlOf(m)).toBe(`${STORE}/api/licence/check`);
  });
});

describe('installing a paid extension', () => {
  it('asks for a key; with a good one the store gives the file, checked like any other', async () => {
    const file = bytesOf(JSON.stringify({ items: ['Sofa'] }));
    const m = paidPack(file, await sha256Hex(file));
    const s = fakeStore(file, { 'GOOD-1': 'active', 'REFUNDED-1': 'revoked' });
    const deps = (key: string | null) => ({
      app: { version: '0.1.900', browser: false, hasModule: () => true }, fetchBytes: async () => { throw new Error('never a public address'); },
      installers: new Map<ExtensionKind, ContentInstaller>([['furniture', furnitureInstaller]]), store: memory(), confirm: () => true,
      licence: { key, post: s.post },
    });
    await expect(installExtension(m, deps(null))).rejects.toThrow(LICENCE_MESSAGES.missing);
    await expect(installExtension(m, deps('NOT-A-KEY'))).rejects.toThrow(LICENCE_MESSAGES.invalid);
    const refused = await installExtension(m, deps('REFUNDED-1')).catch((e: unknown) => e);
    expect(refused).toBeInstanceOf(LicenceError);
    expect((refused as LicenceError).reason).toBe('revoked');
    const d = deps('GOOD-1');
    const x = await installExtension(m, d);
    expect(x.version).toBe('1.0.0');
    expect(d.store.all()).toHaveLength(1);
    expect(s.posts.at(-1)).toEqual({ url: `${STORE}/api/licensed/ana.sofas/1.0.0`, body: { key: 'GOOD-1' } });
  });

  it('refuses a file from the store that is not the one its manifest names', async () => {
    const file = bytesOf('{"items":["Sofa"]}');
    const m = paidPack(file, 'b'.repeat(64));
    const s = fakeStore(file, { K: 'active' });
    await expect(installExtension(m, { app: { version: '0.1.900', browser: true, hasModule: () => true }, fetchBytes: async () => file, installers: new Map([['furniture', furnitureInstaller]]), store: memory(), confirm: () => true, licence: { key: 'K', post: s.post } }))
      .rejects.toThrow(/SHA-256 differs/);
  });

  it('the store unreachable or busy says so, and nothing is installed', async () => {
    const m = paidPack(bytesOf('x'), 'a'.repeat(64));
    const busy: PostJson = async () => json(503, { status: 'unavailable' });
    await expect(installExtension(m, { app: { version: '0.1.900', browser: false, hasModule: () => true }, fetchBytes: async () => bytesOf('x'), installers: new Map([['furniture', furnitureInstaller]]), store: memory(), confirm: () => true, licence: { key: 'K', post: busy } }))
      .rejects.toThrow(LICENCE_MESSAGES.unavailable);
  });
});

describe('a key kept, checked again, revoked', () => {
  const at = Date.parse('2026-11-17T12:00:00Z');
  const rec = (over: Partial<LicenceRecord> = {}): LicenceRecord => ({ key: 'GOOD-1', provider: 'gumroad', status: 'active', checkedAt: '2026-11-16T12:00:00Z', ...over });
  const installedOf = (m: Manifest, version = m.version): InstalledExtension => ({ id: m.id, version, kind: m.kind, name: m.name, at: '', manifest: m, receipt: {} });

  it('is checked when the catalogue has a newer version, or a week has passed; not when revoked or without a key', () => {
    const m = paidPack(bytesOf('x'), 'a'.repeat(64), { version: '1.1.0', package: { url: `${STORE}/api/licensed/ana.sofas/1.1.0`, sha256: 'a'.repeat(64), size: 1 } });
    expect(dueForCheck([installedOf(m, '1.0.0')], [m], { 'ana.sofas': rec() }, at).map((x) => x.id)).toEqual(['ana.sofas']);
    expect(dueForCheck([installedOf(m)], [m], { 'ana.sofas': rec() }, at)).toEqual([]);
    expect(dueForCheck([installedOf(m)], [m], { 'ana.sofas': rec({ checkedAt: new Date(at - RECHECK_MS - 1).toISOString() }) }, at)).toHaveLength(1);
    expect(dueForCheck([installedOf(m, '1.0.0')], [m], { 'ana.sofas': rec({ status: 'revoked' }) }, at)).toEqual([]);
    expect(dueForCheck([installedOf(m, '1.0.0')], [m], {}, at)).toEqual([]);
    expect(dueForCheck([installedOf(furniturePack(), '0.9.0')], [furniturePack()], { [furniturePack().id]: rec() }, at)).toEqual([]); // a free one has no key to check
  });

  it('a check: good keeps it active, refunded or disabled makes it revoked, offline leaves it as it was', async () => {
    const m = paidPack(bytesOf('x'), 'a'.repeat(64));
    const s = fakeStore(bytesOf('x'), { 'GOOD-1': 'active', 'REFUNDED-1': 'revoked' });
    const now = '2026-11-17T12:00:00Z';
    expect(afterCheck(rec(), await checkLicenceKey(m, 'GOOD-1', s.post), now)).toEqual(rec({ checkedAt: now }));
    expect(afterCheck(rec({ key: 'REFUNDED-1' }), await checkLicenceKey(m, 'REFUNDED-1', s.post), now).status).toBe('revoked');
    expect(afterCheck(rec({ key: 'GONE' }), await checkLicenceKey(m, 'GONE', s.post), now).status).toBe('revoked'); // no longer a key of this product
    expect(s.posts.at(-1)).toEqual({ url: `${STORE}/api/licence/check`, body: { id: 'ana.sofas', key: 'GONE' } });
    s.goOffline();
    const kept = rec();
    expect(await checkLicenceKey(m, 'GOOD-1', s.post)).toBe('unavailable');
    expect(afterCheck(kept, 'unavailable', now)).toBe(kept);
  });

  it('a revoked paid extension gets no update; what is installed stays; a free one is unaffected', () => {
    const app = { version: '0.1.900', browser: false, hasModule: () => true };
    const m = paidPack(bytesOf('x'), 'a'.repeat(64), { version: '1.1.0', package: { url: `${STORE}/api/licensed/ana.sofas/1.1.0`, sha256: 'a'.repeat(64), size: 1 } });
    const installed = [installedOf(m, '1.0.0')];
    expect(updatesOf(installed, [m], app).map((x) => x.id)).toEqual(['ana.sofas']);
    expect(updatesOf(installed, [m], app, (id) => id === 'ana.sofas')).toEqual([]);
    expect(installed).toHaveLength(1);
    const free = furniturePack({ version: '2.0.0' });
    expect(updatesOf([installedOf(free, '1.0.0')], [free], app, () => true)).toHaveLength(1);
  });

  it('a key as typed: spaces around it taken off; what cannot be a key refused', () => {
    expect(cleanKey('  A1B2C3D4-E5F60718-9ABCDEF0-1234ABCD \n')).toBe('A1B2C3D4-E5F60718-9ABCDEF0-1234ABCD');
    expect(cleanKey('two words')).toBeNull();
    expect(cleanKey('')).toBeNull();
  });
});
