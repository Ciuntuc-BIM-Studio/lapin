/**
 * Material standards, the interface's pure side (docs/plan-material-graphics.md, stage 3): what the engine's `standards.material*`
 * commands give and take, the rule table's editing (rows, order, names per language, each kind's graphics), the apply preview,
 * the store's standards (filters by country and norm, updates), the manifest and files a standard is published with, and the
 * zip that carries them. No React, no engine: the dialogs call these.
 */

import { compareVersions, type Manifest } from '../../platform/extensions/manifest';
import { sha256Hex, type InstalledExtension } from '../../platform/extensions/install';
import type { OwnGraphics } from './materialGraphicsModel';

// ---- what the engine says ------------------------------------------------------------------------------------------------

export interface StandardSummary {
  id: string; name: string; description?: string; author?: string; country?: string; norm?: string; version?: string; license?: string;
  languages?: string[]; indicative?: boolean; basedOn?: string; fold?: 'all' | 'ro'; builtin: boolean; rules: number; patterns: string[];
}
export interface StandardList { current: string; standards: StandardSummary[] }

/** What a rule says for one kind of view: the material.update fields (and `byScale`). */
export type RuleGraphics = OwnGraphics & { byScale?: unknown[]; membrane?: boolean };
export type Names = Record<string, string[]> | string[];
export interface Rule {
  match: { names?: Names; category?: string };
  section?: RuleGraphics; plan?: RuleGraphics; elevation?: RuleGraphics; membrane?: boolean; category?: string;
}
export interface Pen { color?: string; weight?: number; lineType?: string }
export interface MaterialStandard {
  id?: string; name: string; description?: string; author?: string; country?: string; norm?: string; version?: string; license?: string;
  languages?: string[]; indicative?: boolean; basedOn?: string; fold?: 'all' | 'ro';
  pens?: { hatch?: Pen; cut?: Pen }; patterns?: string; rules: Rule[];
}
export interface StandardGet { standard: MaterialStandard; builtin: boolean; current: boolean }

export const RULE_CATEGORIES = ['Structure', 'Finish', 'Insulation', 'Membrane', 'Other'] as const;
export type RuleKind = 'section' | 'plan' | 'elevation';
export const RULE_KINDS: readonly RuleKind[] = ['section', 'plan', 'elevation'];

// ---- the rule table ------------------------------------------------------------------------------------------------------

/** A rule with nothing yet but a name to look for. */
export const emptyRule = (): Rule => ({ match: { names: { en: [] } }, section: { hatch: null } });

/** A standard to start a new one from: empty, or a copy of another's rules (a new standard has no id: the engine gives it). */
export function blankStandard(name: string, author?: string): MaterialStandard {
  return { name, ...(author ? { author } : {}), indicative: false, rules: [emptyRule()] };
}

/** The rules as rows of language and names: an array of names is one row for every language (`lang` ''). */
export function nameRows(match: Rule['match']): { lang: string; names: string[] }[] {
  const n = match.names;
  if (!n) return [{ lang: '', names: [] }];
  if (Array.isArray(n)) return [{ lang: '', names: [...n] }];
  return Object.entries(n).map(([lang, names]) => ({ lang, names: [...names] }));
}

/** The names of one language set; `''` (every language) is the plain list when it is the only one. */
export function setNames(rule: Rule, lang: string, names: string[]): Rule {
  const rows = nameRows(rule.match).filter((r) => r.lang !== lang);
  const all = [...rows, { lang, names }].filter((r) => r.names.length > 0 || r.lang === lang);
  const next: Rule['match'] = { ...rule.match };
  const only = all.length === 1 && all[0].lang === '';
  if (only) next.names = all[0].names;
  else next.names = Object.fromEntries(all.filter((r) => r.lang !== '').map((r) => [r.lang, r.names]));
  if (all.every((r) => r.names.length === 0)) delete next.names;
  return { ...rule, match: next };
}

/** A language's row taken away. */
export function removeLanguage(rule: Rule, lang: string): Rule {
  const rows = nameRows(rule.match).filter((r) => r.lang !== lang);
  const next: Rule['match'] = { ...rule.match };
  if (rows.length === 0 || rows.every((r) => r.names.length === 0 && r.lang === '')) delete next.names;
  else if (rows.length === 1 && rows[0].lang === '') next.names = rows[0].names;
  else next.names = Object.fromEntries(rows.filter((r) => r.lang !== '').map((r) => [r.lang, r.names]));
  return { ...rule, match: next };
}

/** Names typed as chips: split on commas and semicolons, trimmed, no empty and no repeat. */
export function parseNames(text: string): string[] {
  const out: string[] = [];
  for (const part of text.split(/[,;\n]+/)) { const s = part.trim(); if (s && !out.some((o) => o.toLowerCase() === s.toLowerCase())) out.push(s); }
  return out;
}

/** Every name of every language of a rule, to show it in a list. */
export const allNames = (rule: Rule): string[] => nameRows(rule.match).flatMap((r) => r.names);

/** How a rule reads in the list: its names, else its category match. */
export function ruleLabel(rule: Rule): string {
  const names = allNames(rule);
  if (names.length) return names.slice(0, 4).join(', ') + (names.length > 4 ? ` +${names.length - 4}` : '');
  return rule.match.category ? `[${rule.match.category}]` : '';
}

export function addRule(s: MaterialStandard, at?: number): MaterialStandard {
  const rules = [...s.rules];
  rules.splice(at ?? rules.length, 0, emptyRule());
  return { ...s, rules };
}
export const removeRuleAt = (s: MaterialStandard, i: number): MaterialStandard => ({ ...s, rules: s.rules.filter((_, k) => k !== i) });
export function duplicateRule(s: MaterialStandard, i: number): MaterialStandard {
  if (i < 0 || i >= s.rules.length) return s;
  const rules = [...s.rules];
  rules.splice(i + 1, 0, structuredClone(s.rules[i]));
  return { ...s, rules };
}
/** A rule moved by `by` places (the first rule that matches wins, so the order matters); outside the list: as it is. */
export function moveRule(s: MaterialStandard, i: number, by: number): MaterialStandard {
  const j = i + by;
  if (i < 0 || i >= s.rules.length || j < 0 || j >= s.rules.length) return s;
  const rules = [...s.rules];
  [rules[i], rules[j]] = [rules[j], rules[i]];
  return { ...s, rules };
}
/** A rule moved to a place (drag and drop). */
export function moveRuleTo(s: MaterialStandard, from: number, to: number): MaterialStandard {
  if (from === to || from < 0 || to < 0 || from >= s.rules.length || to >= s.rules.length) return s;
  const rules = [...s.rules];
  const [r] = rules.splice(from, 1);
  rules.splice(to, 0, r);
  return { ...s, rules };
}
export const updateRule = (s: MaterialStandard, i: number, f: (r: Rule) => Rule): MaterialStandard => ({ ...s, rules: s.rules.map((r, k) => (k === i ? f(r) : r)) });

/**
 * A graphics patch of the material editor (`fieldPatch`, `groupPatch`, `stepsPatch`: `{field: value}` and `{reset: [fields]}`)
 * applied to a rule's graphics for a kind; a kind left with nothing is taken away.
 */
export function applyGraphicsPatch(own: RuleGraphics | undefined, patch: Record<string, unknown> | null): RuleGraphics | undefined {
  if (patch === null) return undefined;
  const next: Record<string, unknown> = { ...(own ?? {}) };
  for (const [k, v] of Object.entries(patch)) {
    if (k === 'reset') { for (const f of v as string[]) delete next[f]; } else if (v === undefined) delete next[k]; else next[k] = v;
  }
  return Object.keys(next).length ? (next as RuleGraphics) : undefined;
}

/** A rule's kind (section, plan, elevation) changed by a patch of the material editor. */
export function patchRuleKind(rule: Rule, kind: RuleKind, patch: Record<string, unknown> | null): Rule {
  const g = applyGraphicsPatch(rule[kind], patch);
  const { [kind]: _old, ...rest } = rule;
  return g ? { ...rest, [kind]: g } : rest;
}

/** What is wrong with a standard before it is sent (the engine checks it too), or null. */
export function standardProblem(s: MaterialStandard): string | null {
  if (!s.name.trim()) return 'A standard needs a name.';
  if (s.rules.length === 0) return 'A standard needs at least one rule.';
  const empty = s.rules.findIndex((r) => allNames(r).length === 0 && !r.match.category);
  if (empty >= 0) return `Rule ${empty + 1} matches nothing: give it a name or a category.`;
  return null;
}

/** The metadata fields the form edits; empty text takes the field away. */
export function setMeta(s: MaterialStandard, key: 'name' | 'description' | 'author' | 'country' | 'norm' | 'version' | 'license', value: string): MaterialStandard {
  const v = key === 'country' ? value.trim().toUpperCase().slice(0, 2) : value;
  const next: MaterialStandard = { ...s };
  if (key === 'name') next.name = v; else if (v.trim() === '') delete next[key]; else next[key] = v;
  return next;
}
export const setLanguages = (s: MaterialStandard, text: string): MaterialStandard => {
  const list = text.split(/[,;\s]+/).map((x) => x.trim().toLowerCase()).filter(Boolean);
  const { languages: _l, ...rest } = s;
  return list.length ? { ...rest, languages: list } : rest;
};

/** A reference sheet's detail names, scales. */
export const SHEET_SCALES = [20, 50, 100] as const;

/** `standards.previewSheet`'s arguments for a saved standard, or one not saved yet (inline). */
export function sheetArgs(target: { id: string } | { standard: MaterialStandard }, scale: number, details?: string[]): Record<string, unknown> {
  return { ...target, scale, drawings: true, ...(details?.length ? { details } : {}) };
}

// ---- the list ------------------------------------------------------------------------------------------------------------

/** A standard's line in the list: country, norm, version, rules. */
export function standardMeta(s: StandardSummary): string {
  return [s.country, s.norm, s.version ? `v${s.version}` : '', `${s.rules} rules`].filter(Boolean).join(' · ');
}

// ---- applying ------------------------------------------------------------------------------------------------------------

export interface LookBefore { section?: unknown; plan?: unknown; elevation?: unknown }
export interface ApplyRow { id: string; name: string; changed: boolean; before: LookBefore; after: LookBefore; cleared?: unknown; category?: string }
export interface ApplyPreview { standard: string; from: string; overwrite: boolean; changed: number; materials: ApplyRow[] }

/** The rows to show: the changed ones only, or all of them. */
export const shownRows = (p: ApplyPreview, changedOnly: boolean): ApplyRow[] => (changedOnly ? p.materials.filter((m) => m.changed) : p.materials);

/** `standards.applyMaterials`'s arguments: a preview asks the same question as the apply (the same ticks). */
export const applyArgs = (id: string, overwrite: boolean, scale?: number): Record<string, unknown> => ({ id, overwrite, ...(scale ? { scale } : {}) });

/** What the apply did, in a sentence. */
export function appliedSentence(r: { standard: string; materials: number; patternsAdded: number; patternsUpdated: number }): string {
  const pats = r.patternsAdded + r.patternsUpdated;
  return `Applied „${r.standard}”: ${r.materials} materials${pats ? `, ${pats} patterns` : ''}.`;
}

// ---- the store's standards -----------------------------------------------------------------------------------------------

type WithFacts = Manifest & { country?: string; norm?: string };

/** A pack's country (ISO 3166 alpha-2, upper case): its `country` field when the schema has one, else the tag `country-xx`. */
export function countryOf(m: Manifest): string | null {
  const c = (m as WithFacts).country;
  if (typeof c === 'string' && /^[A-Za-z]{2}$/.test(c.trim())) return c.trim().toUpperCase();
  const tag = (m.tags ?? []).find((t) => /^country-[a-z]{2}$/i.test(t));
  if (tag) return tag.slice(8).toUpperCase();
  const d = /\bcountry:\s*([A-Za-z]{2})\b/i.exec(`${m.description ?? ''} ${m.summary}`);
  return d ? d[1].toUpperCase() : null;
}

/** The norm a pack follows: its `norm` field, else „norm: X” in the description (to the next full stop followed by a space, or a semicolon). */
export function normOf(m: Manifest): string | null {
  const n = (m as WithFacts).norm;
  if (typeof n === 'string' && n.trim()) return n.trim();
  const d = /\bnorm:\s*([^;]+?)\.?\s*(?:;|$|\.\s)/i.exec(m.description ?? '');
  const v = d ? d[1].trim() : '';
  return v && !/^(none|n\/a|-)$/i.test(v) ? v : null;
}

/** The standards of a catalogue, by name. */
export const standardPacks = (list: readonly Manifest[]): Manifest[] => list.filter((m) => m.kind === 'standards').sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));

export interface StoreFilter { search?: string; country?: string | null; norm?: string | null }

/** The packs matching the search, the country and the norm. */
export function filterPacks(list: readonly Manifest[], f: StoreFilter): Manifest[] {
  const words = (f.search ?? '').toLowerCase().split(/\s+/).filter(Boolean);
  return list.filter((m) => m.kind === 'standards')
    .filter((m) => !f.country || countryOf(m) === f.country)
    .filter((m) => !f.norm || normOf(m) === f.norm)
    .filter((m) => { const t = [m.name, m.summary, m.author.name, normOf(m) ?? '', ...(m.tags ?? [])].join(' ').toLowerCase(); return words.every((w) => t.includes(w)); })
    .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
}

/** The countries and norms the packs have, to fill the two filters (each once, sorted). */
export function filterChoices(list: readonly Manifest[]): { countries: string[]; norms: string[] } {
  const packs = list.filter((m) => m.kind === 'standards');
  const uniq = (xs: (string | null)[]) => [...new Set(xs.filter((x): x is string => !!x))].sort((a, b) => a.localeCompare(b));
  return { countries: uniq(packs.map(countryOf)), norms: uniq(packs.map(normOf)) };
}

/** The installed standards a newer version of is in the store (never installed by itself). */
export function packUpdates(installed: readonly InstalledExtension[], catalogue: readonly Manifest[]): { installed: InstalledExtension; manifest: Manifest }[] {
  const byId = new Map(catalogue.map((m) => [m.id, m]));
  const out: { installed: InstalledExtension; manifest: Manifest }[] = [];
  for (const x of installed) {
    if (x.kind !== 'standards') continue;
    const m = byId.get(x.id);
    if (m && compareVersions(m.version, x.version) > 0) out.push({ installed: x, manifest: m });
  }
  return out;
}

// ---- publishing ----------------------------------------------------------------------------------------------------------

/** The store's author page, where a new extension or a new version is sent (docs/plan-extensions.md §10). */
export const AUTHOR_PAGE = 'https://store.lapinbim.com/dashboard/';

/** Lower case, letters, digits and dashes. */
export const slug = (text: string): string => text.normalize('NFKD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');

/** The extension's id: the author's name (or GitHub account) first, then the standard's. */
export function packId(s: Pick<MaterialStandard, 'name' | 'author'>, account?: string): string {
  const who = slug(account || s.author || '') || 'author';
  return `${who}.standard-${slug(s.name) || 'material'}`.slice(0, 64).replace(/[-.]+$/, '');
}

export interface PublishOptions {
  /** The GitHub account (or domain) the id starts with. */
  account?: string; summary?: string; version?: string; lapin?: string; fileName: string; bytes: Uint8Array;
}

/** The manifest of a standard as the store takes it (schema 1; the package's address is where the store puts it once accepted). */
export async function buildManifest(s: MaterialStandard, o: PublishOptions): Promise<Record<string, unknown>> {
  const id = packId(s, o.account);
  const version = o.version ?? (s.version && /^\d+\.\d+\.\d+$/.test(s.version) ? s.version : '1.0.0');
  const tags = ['standard', 'hatches', 'materials', ...(s.norm ? [slug(s.norm)] : []), ...(s.country ? [`country-${s.country.toLowerCase()}`] : [])].filter(Boolean).slice(0, 12);
  const facts = [s.country ? `Country: ${s.country}` : '', s.norm ? `norm: ${s.norm}` : ''].filter(Boolean).join('; ');
  const description = [s.description?.trim(), facts ? `${facts}.` : ''].filter(Boolean).join(' ');
  const summary = (o.summary ?? s.description ?? `How materials draw in cuts, plans and facades: ${s.name}.`).replace(/\s+/g, ' ').trim().slice(0, 200);
  return {
    schema: 1, id, name: `Material standard: ${s.name}`.slice(0, 80), summary,
    ...(description ? { description } : {}),
    screenshots: ['screenshot.png'],
    version,
    author: { name: s.author?.trim() || 'Author', ...(o.account ? { github: o.account } : {}) },
    license: s.license?.trim() || 'CC-BY-4.0',
    lapin: o.lapin ?? '*',
    kind: 'standards',
    tags,
    ...(s.languages?.length ? { languages: s.languages } : {}),
    permissions: [],
    ...(s.indicative ? { indicative: true } : {}),
    contents: { file: o.fileName, items: [`${s.rules.length} rules (cut, plan and facade graphics by the material's name)`, ...(s.patterns ? ['Patterns: ' + patternNames(s.patterns).join(', ')] : [])] },
    package: { url: `https://store.lapinbim.com/packages/${id}/${version}/${o.fileName}`, sha256: await sha256Hex(o.bytes), size: o.bytes.length },
  };
}

/** The names of the patterns in a .pat text (`*NAME, description`). */
export const patternNames = (pat: string): string[] => [...pat.matchAll(/^\*([^,\r\n]+)/gm)].map((m) => m[1].trim());

/** The files a publication holds, named as the catalogue's folder has them. */
export interface PublishFile { name: string; bytes: Uint8Array }

export function publishFiles(manifest: Record<string, unknown>, pack: Uint8Array, packName: string, screenshot: Uint8Array | null): PublishFile[] {
  return [
    { name: 'manifest.json', bytes: new TextEncoder().encode(`${JSON.stringify(manifest, null, 2)}\n`) },
    { name: packName, bytes: pack },
    ...(screenshot ? [{ name: 'screenshot.png', bytes: screenshot }] : []),
  ];
}

// ---- a zip (stored, no compression) ------------------------------------------------------------------------------------

const CRC_TABLE = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; }
  return t;
})();
export function crc32(bytes: Uint8Array): number {
  let c = 0xffffffff;
  for (const b of bytes) c = CRC_TABLE[(c ^ b) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

/** A zip holding the files as they are (stored): the author's page takes the folder from it. */
export function zipStore(files: readonly PublishFile[], at = new Date()): Uint8Array {
  const enc = new TextEncoder();
  const time = (at.getHours() << 11) | (at.getMinutes() << 5) | (at.getSeconds() >> 1);
  const date = ((Math.max(1980, at.getFullYear()) - 1980) << 9) | ((at.getMonth() + 1) << 5) | at.getDate();
  const locals: Uint8Array[] = [];
  const centrals: Uint8Array[] = [];
  let offset = 0;
  for (const f of files) {
    const name = enc.encode(f.name);
    const crc = crc32(f.bytes);
    const local = new Uint8Array(30 + name.length);
    const lv = new DataView(local.buffer);
    lv.setUint32(0, 0x04034b50, true); lv.setUint16(4, 20, true); lv.setUint16(6, 0x0800, true); lv.setUint16(8, 0, true);
    lv.setUint16(10, time, true); lv.setUint16(12, date, true); lv.setUint32(14, crc, true); lv.setUint32(18, f.bytes.length, true); lv.setUint32(22, f.bytes.length, true);
    lv.setUint16(26, name.length, true); lv.setUint16(28, 0, true);
    local.set(name, 30);
    const central = new Uint8Array(46 + name.length);
    const cv = new DataView(central.buffer);
    cv.setUint32(0, 0x02014b50, true); cv.setUint16(4, 20, true); cv.setUint16(6, 20, true); cv.setUint16(8, 0x0800, true); cv.setUint16(10, 0, true);
    cv.setUint16(12, time, true); cv.setUint16(14, date, true); cv.setUint32(16, crc, true); cv.setUint32(20, f.bytes.length, true); cv.setUint32(24, f.bytes.length, true);
    cv.setUint16(28, name.length, true); cv.setUint32(42, offset, true);
    central.set(name, 46);
    locals.push(local, f.bytes);
    centrals.push(central);
    offset += local.length + f.bytes.length;
  }
  const size = centrals.reduce((n, c) => n + c.length, 0);
  const end = new Uint8Array(22);
  const ev = new DataView(end.buffer);
  ev.setUint32(0, 0x06054b50, true); ev.setUint16(8, files.length, true); ev.setUint16(10, files.length, true); ev.setUint32(12, size, true); ev.setUint32(16, offset, true);
  const out = new Uint8Array(offset + size + 22);
  let p = 0;
  for (const part of [...locals, ...centrals, end]) { out.set(part, p); p += part.length; }
  return out;
}

/** The instructions beside the saved files, one phrase each (translated as such). */
export const PUBLISH_STEPS = [
  'Sign in with your forum account on the store’s author page.',
  'Choose “New extension”, attach the .lapinstandards file and the screenshot, and fill in what the manifest says.',
  'The store opens a pull request in the catalogue; it is reviewed and, once accepted, your pack appears in Lapin’s store.',
] as const;

// ---- a standard's patterns (.pat text) ---------------------------------------------------------------------------------

/** The patterns of a .pat text, each with its own lines (from its `*NAME` line to the next one). */
export function splitPatterns(pat: string | undefined): { name: string; text: string }[] {
  if (!pat) return [];
  const out: { name: string; text: string }[] = [];
  for (const line of pat.split(/\r?\n/)) {
    const m = /^\*([^,\s]+)/.exec(line);
    if (m) out.push({ name: m[1], text: line });
    else if (out.length && line.trim()) out[out.length - 1].text += `\n${line}`;
  }
  return out;
}
const joinPatterns = (list: { text: string }[]): string | undefined => (list.length ? `${list.map((p) => p.text).join('\n\n')}\n` : undefined);

/** Patterns added (one of the same name is replaced); `pat` is a whole .pat file's text. */
export function addPatterns(current: string | undefined, pat: string): string | undefined {
  const add = splitPatterns(pat);
  const keep = splitPatterns(current).filter((p) => !add.some((a) => a.name.toLowerCase() === p.name.toLowerCase()));
  return joinPatterns([...keep, ...add]);
}
export const removePattern = (current: string | undefined, name: string): string | undefined => joinPatterns(splitPatterns(current).filter((p) => p.name !== name));

/** The standard with its patterns set (undefined: none). */
export function withPatterns(s: MaterialStandard, pat: string | undefined): MaterialStandard {
  const { patterns: _p, ...rest } = s;
  return pat ? { ...rest, patterns: pat } : rest;
}

/** The pens set or taken back (an empty pen goes away). */
export function setPen(s: MaterialStandard, which: 'hatch' | 'cut', key: keyof Pen, value: string | number | undefined): MaterialStandard {
  const pen: Record<string, unknown> = { ...(s.pens?.[which] ?? {}) };
  if (value === undefined || value === '') delete pen[key]; else pen[key] = value;
  const pens: Record<string, unknown> = { ...(s.pens ?? {}) };
  if (Object.keys(pen).length) pens[which] = pen; else delete pens[which];
  const { pens: _old, ...rest } = s;
  return Object.keys(pens).length ? { ...rest, pens: pens as MaterialStandard['pens'] } : rest;
}

/** The material standards a .lapinstandards file carries (as the engine writes them), to know which to apply after an import. */
export function packStandards(text: string): MaterialStandard[] {
  try {
    const o = JSON.parse(text) as { materialGraphics?: { standards?: MaterialStandard[] } };
    return Array.isArray(o.materialGraphics?.standards) ? o.materialGraphics.standards : [];
  } catch { return []; }
}
