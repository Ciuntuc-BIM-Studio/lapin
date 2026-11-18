import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import type { InstalledExtension } from '../../platform/extensions/install';
import type { Manifest } from '../../platform/extensions/manifest';
import { readStandards } from '../../platform/extensions/packs';
import { fieldPatch, groupPatch, stepsPatch } from './materialGraphicsModel';
import {
  addPatterns, addRule, applyArgs, applyGraphicsPatch, blankStandard, buildManifest, countryOf, crc32, duplicateRule, filterChoices, filterPacks, moveRule, moveRuleTo, nameRows, normOf,
  packId, packStandards, packUpdates, parseNames, patchRuleKind, publishFiles, removeLanguage, removePattern, removeRuleAt, ruleLabel, setMeta, setNames, setPen, shownRows, sheetArgs,
  slug, splitPatterns, standardMeta, standardProblem, updateRule, zipStore, type ApplyPreview, type MaterialStandard, type Rule,
} from './materialStandardsModel';

const std = (rules: Rule[] = []): MaterialStandard => ({ name: 'Mine', rules });
const rule = (...names: string[]): Rule => ({ match: { names: { en: names } }, section: { hatch: 'ANSI31' } });

describe('the rule table', () => {
  it('adds, removes, duplicates and moves rules (the order is the priority)', () => {
    let s = std([rule('brick'), rule('concrete')]);
    s = addRule(s);
    expect(s.rules).toHaveLength(3);
    s = moveRule(s, 1, -1);
    expect(s.rules.map((r) => ruleLabel(r))).toEqual(['concrete', 'brick', '']);
    expect(moveRule(s, 0, -1)).toBe(s);
    s = duplicateRule(s, 0);
    expect(s.rules[1]).toEqual(s.rules[0]);
    expect(s.rules[1]).not.toBe(s.rules[0]);
    s = moveRuleTo(s, 0, 3);
    expect(ruleLabel(s.rules[3])).toBe('concrete');
    expect(removeRuleAt(s, 3).rules).toHaveLength(3);
  });

  it('keeps names per language, and the plain list when there is one language for all', () => {
    let r = rule('brick');
    r = setNames(r, 'ro', ['zidarie', 'caramida']);
    expect(nameRows(r.match)).toEqual([{ lang: 'en', names: ['brick'] }, { lang: 'ro', names: ['zidarie', 'caramida'] }]);
    r = removeLanguage(r, 'en');
    expect(r.match.names).toEqual({ ro: ['zidarie', 'caramida'] });
    const plain: Rule = { match: { names: ['wood'] } };
    expect(nameRows(plain.match)).toEqual([{ lang: '', names: ['wood'] }]);
    expect(setNames(plain, '', ['wood', 'timber']).match.names).toEqual(['wood', 'timber']);
    expect(setNames({ match: {} }, 'de', []).match.names).toBeUndefined();
  });

  it('parses names typed as chips without repeats', () => {
    expect(parseNames(' brick, Brick; block\n ,')).toEqual(['brick', 'block']);
  });

  it('applies the material editor’s patches to a rule’s graphics', () => {
    let g = applyGraphicsPatch(undefined, fieldPatch('hatch', 'ANSI31'));
    g = applyGraphicsPatch(g, fieldPatch('hatchScale', 2));
    g = applyGraphicsPatch(g, groupPatch(g, 'cutLine', 'weight', 0.5));
    expect(g).toEqual({ hatch: 'ANSI31', hatchScale: 2, cutLine: { weight: 0.5 } });
    g = applyGraphicsPatch(g, fieldPatch('hatchScale', undefined));
    expect(g).toEqual({ hatch: 'ANSI31', cutLine: { weight: 0.5 } });
    g = applyGraphicsPatch(g, stepsPatch([{ from: 100, hatch: null }]));
    expect((g as { byScale?: unknown[] }).byScale).toEqual([{ from: 100, hatch: null }]);
    expect(applyGraphicsPatch(g, stepsPatch([]))).not.toHaveProperty('byScale');
    expect(applyGraphicsPatch(g, null)).toBeUndefined();
  });

  it('sets a kind of a rule and takes it away when nothing is left', () => {
    let r: Rule = { match: { names: ['x'] } };
    r = patchRuleKind(r, 'plan', fieldPatch('hatch', 'ISO_135'));
    expect(r.plan).toEqual({ hatch: 'ISO_135' });
    r = patchRuleKind(r, 'plan', { reset: ['hatch'] });
    expect(r.plan).toBeUndefined();
    expect(patchRuleKind({ ...r, elevation: { hatch: null } }, 'elevation', null)).not.toHaveProperty('elevation');
    expect(updateRule(std([r]), 0, (x) => ({ ...x, category: 'Finish' })).rules[0].category).toBe('Finish');
  });

  it('says what is wrong before sending', () => {
    expect(standardProblem(std())).toMatch(/at least one rule/);
    expect(standardProblem({ name: ' ', rules: [rule('a')] })).toMatch(/name/);
    expect(standardProblem(std([rule('a'), { match: {} }]))).toMatch(/Rule 2/);
    expect(standardProblem(std([rule('a'), { match: { category: 'Finish' } }]))).toBeNull();
    expect(standardProblem(blankStandard('New'))).toMatch(/Rule 1/);
  });

  it('edits the details and the pens', () => {
    let s = setMeta(std([rule('a')]), 'country', 'pl-x');
    expect(s.country).toBe('PL');
    s = setMeta(s, 'norm', 'PN-B-01030');
    expect(setMeta(s, 'norm', ' ').norm).toBeUndefined();
    s = setPen(s, 'hatch', 'weight', 0.13);
    expect(s.pens).toEqual({ hatch: { weight: 0.13 } });
    expect(setPen(s, 'hatch', 'weight', undefined).pens).toBeUndefined();
  });

  it('keeps the patterns of a .pat text by name', () => {
    const a = '*A, first\n45, 0,0, 0,3\n\n*B, second\n90, 0,0, 0,2\n';
    expect(splitPatterns(a).map((p) => p.name)).toEqual(['A', 'B']);
    const more = addPatterns(a, '*B, newer\n0, 0,0, 0,5\n*C, third\n0, 0,0, 0,1');
    expect(splitPatterns(more).map((p) => p.name)).toEqual(['A', 'B', 'C']);
    expect(splitPatterns(more)[1].text).toContain('newer');
    expect(removePattern(a, 'A')).not.toContain('*A');
    expect(removePattern('*A, x\n0,0,0,0,1\n', 'A')).toBeUndefined();
  });
});

describe('the engine’s questions', () => {
  it('asks the sheet of a saved or an inline standard, and the apply with the same ticks as its preview', () => {
    expect(sheetArgs({ id: 'iso-128-50' }, 50)).toEqual({ id: 'iso-128-50', scale: 50, drawings: true });
    expect(sheetArgs({ standard: std() }, 20, ['wall'])).toMatchObject({ scale: 20, details: ['wall'], standard: { name: 'Mine' } });
    expect(applyArgs('din-1356', true)).toEqual({ id: 'din-1356', overwrite: true });
    expect(applyArgs('din-1356', false, 100)).toEqual({ id: 'din-1356', overwrite: false, scale: 100 });
  });

  it('shows the changed rows only when asked', () => {
    const row = (name: string, changed: boolean) => ({ id: name, name, changed, before: {}, after: {} });
    const p: ApplyPreview = { standard: 'x', from: 'lapin', overwrite: false, changed: 1, materials: [row('a', true), row('b', false)] };
    expect(shownRows(p, true).map((m) => m.name)).toEqual(['a']);
    expect(shownRows(p, false)).toHaveLength(2);
  });

  it('describes a standard in the list', () => {
    expect(standardMeta({ id: 'a', name: 'A', country: 'PL', norm: 'PN-B', version: '1.2.0', builtin: false, rules: 12, patterns: [] })).toBe('PL · PN-B · v1.2.0 · 12 rules');
  });
});

const manifest = (over: Partial<Manifest> & { id: string }): Manifest => ({
  schema: 1, name: 'Material standard: X', summary: 's', version: '1.0.0', author: { name: 'A' }, license: 'MIT', lapin: '*', kind: 'standards', contents: { file: 'x.lapinstandards' },
  package: { url: 'https://x/y', sha256: '0'.repeat(64), size: 1 }, ...over,
} as Manifest);

describe('the store’s standards', () => {
  const pl = manifest({ id: 'pl', name: 'Material standard: Polska', tags: ['standard', 'country-pl'], description: 'Zasady. Country: PL; norm: PN-B-01030.' });
  const de = manifest({ id: 'de', name: 'Material standard: Deutschland', tags: ['country-de'], description: 'Country: DE; norm: DIN 1356-1.' });
  const fi = manifest({ id: 'fi', name: 'Material standard: Suomi', description: 'Pelkkä kuvaus.' });
  const other = manifest({ id: 'f', kind: 'furniture', tags: ['country-pl'] });
  const all = [pl, de, fi, other];

  it('reads the country from the tag, the field or the description, and the norm from the description or the field', () => {
    expect(countryOf(pl)).toBe('PL');
    expect(countryOf(manifest({ id: 'a', description: 'Country: ro; norm: SR ISO 128-50.' }))).toBe('RO');
    expect(countryOf({ ...fi, country: 'fi' } as Manifest)).toBe('FI');
    expect(countryOf(fi)).toBeNull();
    expect(normOf(pl)).toBe('PN-B-01030');
    expect(normOf(de)).toBe('DIN 1356-1');
    expect(normOf({ ...fi, norm: 'SFS 1' } as Manifest)).toBe('SFS 1');
    expect(normOf(fi)).toBeNull();
  });

  it('filters by country, norm and search, standards only', () => {
    expect(filterPacks(all, {}).map((m) => m.id)).toEqual(['de', 'pl', 'fi'].sort((a, b) => ({ de: 'Deutschland', pl: 'Polska', fi: 'Suomi' }[a]!).localeCompare({ de: 'Deutschland', pl: 'Polska', fi: 'Suomi' }[b]!)));
    expect(filterPacks(all, { country: 'PL' }).map((m) => m.id)).toEqual(['pl']);
    expect(filterPacks(all, { norm: 'DIN 1356-1' }).map((m) => m.id)).toEqual(['de']);
    expect(filterPacks(all, { search: 'suomi' }).map((m) => m.id)).toEqual(['fi']);
    expect(filterPacks(all, { country: 'DE', norm: 'PN-B-01030' })).toEqual([]);
    expect(filterChoices(all)).toEqual({ countries: ['DE', 'PL'], norms: ['DIN 1356-1', 'PN-B-01030'] });
  });

  it('offers an update only for an installed standard whose store version is newer', () => {
    const inst = (id: string, version: string, kind: Manifest['kind'] = 'standards'): InstalledExtension => ({ id, version, kind, name: id, at: '', manifest: manifest({ id }), receipt: {} });
    const store = [manifest({ id: 'a', version: '1.2.0' }), manifest({ id: 'b', version: '1.0.0' }), manifest({ id: 'c', version: '2.0.0', kind: 'furniture' })];
    const got = packUpdates([inst('a', '1.1.0'), inst('b', '1.0.0'), inst('c', '1.0.0', 'furniture'), inst('gone', '1.0.0')], store);
    expect(got.map((u) => u.manifest.id)).toEqual(['a']);
    expect(got[0].installed.version).toBe('1.1.0');
  });
});

describe('publishing', () => {
  const s: MaterialStandard = { name: 'Polska PN-B', author: 'Biuro Ć', country: 'PL', norm: 'PN-B-01030', version: '1.0.0', license: 'CC-BY-4.0', languages: ['pl', 'en'], indicative: true, description: 'Hatches after the Polish norm.', patterns: '*PL_A, x\n0,0,0,0,1\n', rules: [rule('cegła')] };
  const bytes = new TextEncoder().encode('{"format":"lapin-standards"}');

  it('slugs and ids', () => {
    expect(slug('Biuro Ć & Co.')).toBe('biuro-c-co');
    expect(packId(s)).toBe('biuro-c.standard-polska-pn-b');
    expect(packId(s, 'Ciuntuc-BIM-Studio')).toBe('ciuntuc-bim-studio.standard-polska-pn-b');
    expect(packId({ name: 'A', author: '' })).toBe('author.standard-a');
  });

  it('writes the manifest the catalogue takes: country tag, norm in the description, licence, size and SHA-256 of the file', async () => {
    const m = await buildManifest(s, { account: 'biuro', fileName: 'polska.lapinstandards', bytes });
    expect(m).toMatchObject({
      schema: 1, id: 'biuro.standard-polska-pn-b', name: 'Material standard: Polska PN-B', version: '1.0.0', kind: 'standards', license: 'CC-BY-4.0', lapin: '*',
      indicative: true, languages: ['pl', 'en'], author: { name: 'Biuro Ć', github: 'biuro' }, permissions: [], screenshots: ['screenshot.png'], contents: { file: 'polska.lapinstandards' },
    });
    expect(m.tags).toEqual(['standard', 'hatches', 'materials', 'pn-b-01030', 'country-pl']);
    expect(m.description).toBe('Hatches after the Polish norm. Country: PL; norm: PN-B-01030.');
    const pkg = m.package as { url: string; sha256: string; size: number };
    expect(pkg.size).toBe(bytes.length);
    expect(pkg.sha256).toMatch(/^[0-9a-f]{64}$/);
    expect(pkg.url).toBe('https://store.lapinbim.com/packages/biuro.standard-polska-pn-b/1.0.0/polska.lapinstandards');
    // what is written reads back as the filters read it
    expect(countryOf(m as unknown as Manifest)).toBe('PL');
    expect(normOf(m as unknown as Manifest)).toBe('PN-B-01030');
  });

  it('leaves out what the author did not say', async () => {
    const m = await buildManifest({ name: 'Plain', rules: [rule('a')] }, { fileName: 'p.lapinstandards', bytes });
    expect(m).not.toHaveProperty('indicative');
    expect(m).not.toHaveProperty('languages');
    expect(m.license).toBe('CC-BY-4.0');
    expect((m.author as { name: string }).name).toBe('Author');
  });

  it('zips the files stored, readable back (signatures, names, sizes and CRCs)', () => {
    const files = publishFiles({ id: 'x' }, bytes, 'x.lapinstandards', new Uint8Array([1, 2, 3]));
    expect(files.map((f) => f.name)).toEqual(['manifest.json', 'x.lapinstandards', 'screenshot.png']);
    const zip = zipStore(files, new Date(2026, 10, 18, 10, 0, 0));
    const v = new DataView(zip.buffer);
    expect(v.getUint32(0, true)).toBe(0x04034b50);
    expect(v.getUint32(zip.length - 22, true)).toBe(0x06054b50);
    expect(v.getUint16(zip.length - 22 + 10, true)).toBe(3);
    // the first entry: its CRC and size
    expect(v.getUint32(14, true)).toBe(crc32(files[0].bytes));
    expect(v.getUint32(22, true)).toBe(files[0].bytes.length);
    expect(new TextDecoder().decode(zip.subarray(30, 30 + 13))).toBe('manifest.json');
    expect(crc32(new TextEncoder().encode('123456789'))).toBe(0xcbf43926);
  });
});

describe('the store packs in this repository', () => {
  const dir = join(__dirname, '../../../../extensions-packs/standards');
  let ids: string[] = [];
  try { ids = readdirSync(dir); } catch { ids = []; }
  it.skipIf(ids.length === 0)('reads each pack: the manifest’s country and norm, and the file as the installer reads it', () => {
    for (const id of ids) {
      const m = JSON.parse(readFileSync(join(dir, id, 'manifest.json'), 'utf8')) as Manifest;
      const file = readFileSync(join(dir, id, m.contents.file));
      const read = readStandards(new Uint8Array(file));
      const standards = packStandards(read.text);
      expect(standards.length).toBeGreaterThan(0);
      if ((m.tags ?? []).some((t) => t.startsWith('country-'))) expect(countryOf(m)).toMatch(/^[A-Z]{2}$/);
      if (id === 'lapin') expect(normOf(m)).toBeNull(); else expect(normOf(m)).toBeTruthy();
    }
  });
});
