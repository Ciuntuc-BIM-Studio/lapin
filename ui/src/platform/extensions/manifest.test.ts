import { describe, expect, it } from 'vitest';
import { compareVersions, compatibility, isPermissive, isSpdx, readIndex, runnable, satisfies, validateManifest } from './manifest';
import { furniturePack } from './fixtures';

const app = (over: Partial<{ version: string; browser: boolean; modules: string[] }> = {}) => {
  const modules = over.modules ?? ['core', 'architecture', 'furniture', 'cost'];
  return { version: over.version ?? '0.1.870', browser: over.browser ?? false, hasModule: (id: string) => modules.includes(id) };
};

describe('an extension\'s manifest', () => {
  it('takes a good one', () => {
    expect(validateManifest(furniturePack())).toEqual([]);
  });

  it('says what is wrong, field by field', () => {
    const bad = validateManifest({ ...furniturePack(), id: 'Mobilier Living', version: '1.0', license: '', lapin: 'soon', kind: 'plugin' });
    expect(bad.some((e) => e.startsWith('id '))).toBe(true);
    expect(bad.some((e) => e.startsWith('version '))).toBe(true);
    expect(bad.some((e) => e.startsWith('license is required'))).toBe(true);
    expect(bad.some((e) => e.startsWith('lapin '))).toBe(true);
    expect(bad.some((e) => e.startsWith('kind '))).toBe(true);
    expect(validateManifest(null)).toEqual(['The manifest is not a JSON object.']);
  });

  it('requires a licence, an https package with its SHA-256 and size, and a file of its kind', () => {
    expect(validateManifest(furniturePack({ license: 'any licence you like' }))).toContain('license „any licence you like” is not an SPDX expression.');
    expect(validateManifest(furniturePack({ package: { url: 'http://example.com/x.lapinlib', sha256: 'A'.repeat(64), size: 0 } })).length).toBe(3);
    expect(validateManifest(furniturePack({ contents: { file: 'mobilier.zip' } }))).toContain('A furniture package is a .lapinlib file.');
    // A local catalogue's packages may be paths beside its index.
    expect(validateManifest(furniturePack({ package: { url: 'dist/mobilier-living.lapinlib', sha256: 'a'.repeat(64), size: 10 } }), { local: true })).toEqual([]);
  });

  it('lets only code ask for permissions, and a price list must say it is indicative', () => {
    expect(validateManifest(furniturePack({ permissions: ['network'] }))).toContain('A content pack asks for no permission: it holds no code.');
    expect(validateManifest(furniturePack({ kind: 'script', permissions: ['model.write'], contents: { file: 'perimeter.py' } }))).toEqual([]);
    expect(validateManifest(furniturePack({ kind: 'priceList', contents: { file: 'preturi.lapinprices' } }))).toContain('A price list says its prices are indicative: indicative must be true.');
    expect(validateManifest(furniturePack({ kind: 'priceList', indicative: true, contents: { file: 'preturi.lapinprices' } }))).toEqual([]);
  });
});

describe('licences', () => {
  it('reads SPDX expressions', () => {
    for (const ok of ['MIT', 'Apache-2.0 OR MIT', '(MIT AND CC-BY-4.0)', 'GPL-2.0-or-later WITH Classpath-exception-2.0', 'LicenseRef-Ciuntuc-1', 'GPL-2.0+']) expect(isSpdx(ok), ok).toBe(true);
    for (const bad of ['', 'MIT OR', 'AND MIT', '(MIT', 'MIT)', 'MIT licence']) expect(isSpdx(bad), bad).toBe(false);
  });

  it('calls permissive what may be taken under permissive licences only', () => {
    expect(isPermissive('MIT')).toBe(true);
    expect(isPermissive('GPL-3.0-only OR MIT')).toBe(true);
    expect(isPermissive('GPL-3.0-only AND MIT')).toBe(false);
    expect(isPermissive('(MIT OR GPL-3.0-only) AND CC-BY-4.0')).toBe(true);
    expect(isPermissive('CC-BY-NC-4.0')).toBe(false);
  });
});

describe('versions', () => {
  it('orders semver, a pre-release before its release', () => {
    expect(compareVersions('1.0.0', '1.0.1')).toBeLessThan(0);
    expect(compareVersions('0.10.0', '0.9.9')).toBeGreaterThan(0);
    expect(compareVersions('1.0.0-beta.2', '1.0.0')).toBeLessThan(0);
    expect(compareVersions('1.0.0-beta.2', '1.0.0-beta.10')).toBeLessThan(0);
    expect(compareVersions('2.1.3', '2.1.3')).toBe(0);
  });

  it('checks a version against a range', () => {
    expect(satisfies('0.1.870', '>=0.1.860')).toBe(true);
    expect(satisfies('0.1.850', '>=0.1.860')).toBe(false);
    expect(satisfies('0.3.0', '>=0.1.860 <0.3.0')).toBe(false);
    expect(satisfies('0.1.999', '^0.1.900')).toBe(true);
    expect(satisfies('0.2.0', '^0.1.900')).toBe(false);
    expect(satisfies('1.4.0', '~1.3.0 || ^1.4.0')).toBe(true);
    expect(satisfies('0.1.5', '0.1.x')).toBe(true);
    expect(satisfies('0.2.5', '0.1.x')).toBe(false);
    expect(satisfies('anything', '*')).toBe(true);
    expect(satisfies('dev', '>=0.1.0')).toBe(false);
  });
});

describe('compatibility', () => {
  it('needs the version, the module of its kind, and the desktop for code', () => {
    expect(compatibility(furniturePack(), app())).toEqual({ ok: true });
    expect(compatibility(furniturePack(), app({ version: '0.1.800' }))).toEqual({ ok: false, reason: 'It works with Lapin >=0.1.860; this is Lapin 0.1.800.' });
    expect(compatibility(furniturePack(), app({ modules: ['core'] }))).toEqual({ ok: false, reason: 'This edition of Lapin has no furniture module.' });
    const script = furniturePack({ kind: 'script', contents: { file: 'x.py' }, requires: ['scripting'] });
    expect(compatibility(script, app({ browser: true })).ok).toBe(false);
    expect(compatibility(script, app()).ok).toBe(true);
    // Installed, but it runs only once the Python console (the scripting module) is there.
    expect(runnable(script, app())).toBe(false);
    expect(runnable(script, app({ modules: ['core', 'scripting'] }))).toBe(true);
  });
});

describe('the catalogue\'s index', () => {
  it('keeps the good entries and says which were left out', () => {
    const r = readIndex({ format: 'lapin-extensions-index', version: 1, extensions: [furniturePack(), { ...furniturePack(), id: 'Bad Id' }, furniturePack()] });
    expect(r.extensions.map((m) => m.id)).toEqual(['ro.ciuntucbimstudio.mobilier-living']);
    expect(r.problems).toHaveLength(2);
    expect(() => readIndex({ format: 'something else' })).toThrow('This is not a catalogue of Lapin extensions.');
    expect(() => readIndex({ format: 'lapin-extensions-index', version: 2, extensions: [] })).toThrow(/newer Lapin/);
  });
});
