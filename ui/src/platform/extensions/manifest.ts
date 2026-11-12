/**
 * Lapin Extensions (docs/plan-extensions.md): what an extension says of itself, its manifest, as the catalogue's index
 * (`lapin-extensions` on GitHub, published at lapinbim.com/extensions/index.json) carries it, and the pure checks made on it
 * before anything is downloaded: the manifest's schema, the versions (semver) and the range of Lapin versions it works with.
 * The same rules are written for the catalogue's CI (`tools/validate.mjs` in lapin-extensions); a manifest either accepts is
 * accepted by the other.
 */

/** The kinds of extension: content packs (no code) first; Python scripts, which run once the Python console exists. */
export const KINDS = ['furniture', 'template', 'types', 'profiles', 'hatches', 'titleBlock', 'priceList', 'script'] as const;
export type ExtensionKind = (typeof KINDS)[number];

export interface KindInfo {
  label: string;
  /** The file the package is. */
  extensions: string[];
  /** Code (a script): installed on the desktop only, and shown with its permissions. */
  code: boolean;
  /** The module whose library it goes into (none: the shell's own). */
  module?: string;
}

export const KIND_INFO: Record<ExtensionKind, KindInfo> = {
  furniture: { label: 'Furniture', extensions: ['lapinlib'], code: false, module: 'furniture' },
  template: { label: 'Project templates', extensions: ['lapin'], code: false },
  // Wall, floor, door, window, column, beam, stair… types with their materials and layers (a .lapintypes, types.importFrom).
  types: { label: 'Types', extensions: ['lapintypes'], code: false, module: 'core' },
  profiles: { label: 'Profiles', extensions: ['lapinprofiles'], code: false, module: 'core' },
  hatches: { label: 'Hatch patterns', extensions: ['pat'], code: false, module: 'core' },
  titleBlock: { label: 'Title blocks', extensions: ['json'], code: false, module: 'core' },
  priceList: { label: 'Price lists', extensions: ['lapinprices'], code: false, module: 'cost' },
  script: { label: 'Python scripts', extensions: ['py'], code: true },
};

/** What a script may ask for, said to the user before it is installed (enforced for JS plug-ins later; a script on the desktop can do what the user can). */
export const PERMISSIONS = ['model.read', 'model.write', 'files.read', 'files.write', 'network', 'process'] as const;
export type Permission = (typeof PERMISSIONS)[number];
export const PERMISSION_LABELS: Record<Permission, string> = {
  'model.read': 'Read the open project',
  'model.write': 'Change the open project',
  'files.read': 'Read files on this computer',
  'files.write': 'Write files on this computer',
  network: 'Reach the internet',
  process: 'Start other programs',
};

export interface Author { name: string; github?: string; url?: string; email?: string; /** Their user name on forum.lapinbim.com (set by the store for what is submitted there). */ forum?: string }

export interface ExtensionPackage {
  /** Where the file is: an https address (the author's GitHub release), or, in a local catalogue, a path beside the index. */
  url: string;
  sha256: string;
  size: number;
  /** A copy kept by lapinbim.com (the browser cannot read GitHub's releases); the same bytes, the same SHA-256. */
  mirror?: string;
  /** The store's counting address (store.lapinbim.com/api/download/<id>/<version>): counts the download, then redirects to the copy. Tried first. */
  download?: string;
  /** The store key's signature of `packageMessage` (signature.ts): its id, version, SHA-256 and size. */
  signature?: { keyId: string; value: string };
}

export interface Manifest {
  /** The manifest's own format. */
  schema: 1;
  /** Lowercase, dots and dashes: `ro.ciuntucbimstudio.mobilier-living`; the folder in the catalogue. */
  id: string;
  name: string;
  /** One line for the list. */
  summary: string;
  description?: string;
  /** Pictures for the store's page: files of its folder in the catalogue, or https addresses (Lapin does not show them). */
  screenshots?: string[];
  /** Semver: 1.2.0. */
  version: string;
  author: Author;
  /** An SPDX expression (MIT, CC-BY-4.0, Apache-2.0 OR MIT): required, shown to the user. */
  license: string;
  /** The Lapin versions it works with: `>=0.1.860`, `>=0.1.860 <0.3.0`, `^0.1.900`, `*`. */
  lapin: string;
  kind: ExtensionKind;
  tags?: string[];
  /** The languages of its content (ISO 639-1). */
  languages?: string[];
  /** What a script asks for (none for a content pack). */
  permissions?: Permission[];
  /** The modules it needs beyond its kind's (a script: `scripting`). */
  requires?: string[];
  /** What is in it: the file's name and, to show before installing, its items. */
  contents: { file: string; items?: string[] };
  package: ExtensionPackage;
  homepage?: string;
  /** Its topic on forum.lapinbim.com. */
  forum?: string;
  /** A price list's prices (and any norm) are the author's and indicative: required true for a price list. */
  indicative?: boolean;
}

export interface CatalogueIndex {
  format: 'lapin-extensions-index';
  version: 1;
  generated?: string;
  extensions: Manifest[];
}

const ID = /^[a-z0-9]+(?:[.-][a-z0-9]+)*$/;
const SEMVER = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$/;
const SHA256 = /^[0-9a-f]{64}$/;
const LANG = /^[a-z]{2}$/;
/** An SPDX expression's shape: identifiers (or LicenseRef-…), WITH exceptions, AND / OR, brackets. */
const SPDX_TOKEN = /^(?:[A-Za-z0-9][A-Za-z0-9.+-]*|LicenseRef-[A-Za-z0-9.-]+)$/;
/** At most this big (bytes): a pack, a template with its images. */
export const MAX_PACKAGE = 64 * 1024 * 1024;

/** The SPDX licences Lapin calls permissive (shown so): others are accepted and shown as they are. */
export const PERMISSIVE = new Set(['MIT', 'MIT-0', 'Apache-2.0', 'BSD-2-Clause', 'BSD-3-Clause', 'ISC', '0BSD', 'Zlib', 'Unlicense', 'CC0-1.0', 'CC-BY-4.0', 'PDDL-1.0', 'ODC-By-1.0', 'BSL-1.0', 'PSF-2.0']);

/** Whether a text is an SPDX licence expression (its grammar, not the list of identifiers). */
export function isSpdx(expr: string): boolean {
  const tokens = expr.replace(/[()]/g, ' $& ').trim().split(/\s+/).filter(Boolean);
  if (!tokens.length) return false;
  let depth = 0, wantId = true;
  for (let i = 0; i < tokens.length; i++) {
    const t = tokens[i];
    if (t === '(') { if (!wantId) return false; depth++; continue; }
    if (t === ')') { if (wantId || --depth < 0) return false; continue; }
    if (t === 'AND' || t === 'OR') { if (wantId) return false; wantId = true; continue; }
    if (t === 'WITH') { if (wantId || !SPDX_TOKEN.test(tokens[i + 1] ?? '')) return false; i++; continue; }
    if (!wantId || !SPDX_TOKEN.test(t.replace(/\+$/, ''))) return false;
    wantId = false;
  }
  return depth === 0 && !wantId;
}

/**
 * An SPDX expression evaluated: whether it can be taken under licences for which `ok` holds (OR: either side is enough;
 * AND: both are needed; a WITH exception goes with its licence). False for what is not an SPDX expression.
 */
function holds(expr: string, ok: (id: string) => boolean): boolean {
  if (!isSpdx(expr)) return false;
  const tokens = expr.replace(/[()]/g, ' $& ').trim().split(/\s+/);
  let i = 0;
  // or := and ('OR' and)*; and := atom ('AND' atom)*; atom := '(' or ')' | id ['WITH' id]
  const atom = (): boolean => {
    if (tokens[i] === '(') { i++; const v = or(); i++; return v; }
    const id = tokens[i++].replace(/\+$/, '');
    if (tokens[i] === 'WITH') i += 2;
    return ok(id);
  };
  const and = (): boolean => { let v = atom(); while (tokens[i] === 'AND') { i++; v = atom() && v; } return v; };
  const or = (): boolean => { let v = and(); while (tokens[i] === 'OR') { i++; v = and() || v; } return v; };
  return or();
}

/** Whether the expression can be taken under licences Lapin calls permissive only. */
export const isPermissive = (expr: string): boolean => holds(expr, (id) => PERMISSIVE.has(id));

/** Licences that forbid commercial use, besides the Creative Commons NC family (any SPDX id with -NC: CC-BY-NC-4.0, CC-BY-NC-SA-4.0, CC-BY-NC-ND-4.0…). */
export const NON_COMMERCIAL = new Set(['PolyForm-Noncommercial-1.0.0', 'NCGL-UK-2.0']);

/** Whether a licence id forbids commercial use: -NC in it (Creative Commons), one of the known others, a LicenseRef saying non-commercial. */
export function isNonCommercialId(id: string): boolean {
  const bare = id.replace(/\+$/, '');
  return /-NC(?:-|$)/i.test(bare) || NON_COMMERCIAL.has(bare) || /^LicenseRef-.*non-?commercial/i.test(bare);
}

/**
 * Whether what the extension holds may not be used commercially: no way to take it but under a non-commercial licence
 * (`MIT OR CC-BY-NC-4.0` can be taken under MIT; `CC-BY-NC-4.0 AND MIT` cannot). Shown as „Non-commercial” in Browse
 * and the details (decision 499, §9.2); accepted in the catalogue.
 */
export const isNonCommercial = (expr: string): boolean => isSpdx(expr) && !holds(expr, (id) => !isNonCommercialId(id));

const str = (v: unknown): v is string => typeof v === 'string' && v.trim().length > 0;
const isHttps = (u: string) => /^https:\/\/[^\s/$.?#].[^\s]*$/i.test(u);

/** What is wrong with a manifest, in sentences; none: it is a good one. `local`: a catalogue on this computer, whose files may be paths. */
export function validateManifest(m: unknown, opts: { local?: boolean } = {}): string[] {
  const e: string[] = [];
  if (!m || typeof m !== 'object' || Array.isArray(m)) return ['The manifest is not a JSON object.'];
  const o = m as Record<string, unknown>;
  if (o.schema !== 1) e.push('schema must be 1.');
  if (!str(o.id) || !ID.test(o.id) || o.id.length > 64) e.push('id must be lowercase letters and digits, joined by dots or dashes, at most 64 characters.');
  if (!str(o.name) || o.name.length > 80) e.push('name is required, at most 80 characters.');
  if (!str(o.summary) || o.summary.length > 200) e.push('summary is required, at most 200 characters.');
  if (o.description !== undefined && typeof o.description !== 'string') e.push('description must be text.');
  if (!str(o.version) || !SEMVER.test(o.version)) e.push('version must be a semantic version (1.0.0).');
  const a = o.author as Record<string, unknown> | undefined;
  if (!a || typeof a !== 'object' || !str(a.name)) e.push('author.name is required.');
  else {
    if (a.github !== undefined && (!str(a.github) || !/^[A-Za-z0-9-]{1,39}$/.test(a.github))) e.push('author.github must be a GitHub user name.');
    if (a.url !== undefined && (!str(a.url) || !isHttps(a.url))) e.push('author.url must be an https address.');
    if (a.forum !== undefined && (!str(a.forum) || !/^[A-Za-z0-9_.-]{1,60}$/.test(a.forum))) e.push('author.forum must be a user name on forum.lapinbim.com.');
  }
  if (!str(o.license)) e.push('license is required: an SPDX expression (MIT, CC-BY-4.0…).');
  else if (!isSpdx(o.license)) e.push(`license „${o.license}” is not an SPDX expression.`);
  if (!str(o.lapin) || parseRange(o.lapin) === null) e.push('lapin must be a range of Lapin versions (>=0.1.860).');
  const kind = o.kind as ExtensionKind;
  if (!KINDS.includes(kind)) e.push(`kind must be one of ${KINDS.join(', ')}.`);
  if (o.tags !== undefined && (!Array.isArray(o.tags) || o.tags.some((t) => !str(t) || t.length > 30) || o.tags.length > 12)) e.push('tags must be at most 12 short texts.');
  if (o.languages !== undefined && (!Array.isArray(o.languages) || o.languages.some((l) => typeof l !== 'string' || !LANG.test(l)))) e.push('languages must be two-letter codes (ro, en).');
  const perms = o.permissions;
  if (perms !== undefined && (!Array.isArray(perms) || perms.some((p) => !PERMISSIONS.includes(p as Permission)))) e.push(`permissions must be among ${PERMISSIONS.join(', ')}.`);
  if (KINDS.includes(kind) && !KIND_INFO[kind].code && Array.isArray(perms) && perms.length) e.push('A content pack asks for no permission: it holds no code.');
  if (o.requires !== undefined && (!Array.isArray(o.requires) || o.requires.some((r) => !str(r) || !ID.test(r)))) e.push('requires must be module ids.');
  const c = o.contents as Record<string, unknown> | undefined;
  if (!c || typeof c !== 'object' || !str(c.file) || /[\\/]/.test(c.file)) e.push('contents.file is required: the package file\'s name.');
  else {
    if (KINDS.includes(kind) && !KIND_INFO[kind].extensions.some((x) => (c.file as string).toLowerCase().endsWith(`.${x}`))) e.push(`A ${kind} package is a .${KIND_INFO[kind].extensions.join(' or .')} file.`);
    if (c.items !== undefined && (!Array.isArray(c.items) || c.items.some((i) => typeof i !== 'string'))) e.push('contents.items must be a list of names.');
  }
  const p = o.package as Record<string, unknown> | undefined;
  if (!p || typeof p !== 'object') e.push('package is required: url, sha256 and size.');
  else {
    if (!str(p.url) || (!isHttps(p.url) && !opts.local)) e.push('package.url must be an https address (the release of the package).');
    if (!str(p.sha256) || !SHA256.test(p.sha256)) e.push('package.sha256 must be the file\'s SHA-256, 64 lowercase hex digits.');
    if (typeof p.size !== 'number' || !Number.isInteger(p.size) || p.size <= 0 || p.size > MAX_PACKAGE) e.push(`package.size must be the file's size in bytes, at most ${MAX_PACKAGE}.`);
    if (p.mirror !== undefined && (!str(p.mirror) || !isHttps(p.mirror))) e.push('package.mirror must be an https address.');
    if (p.download !== undefined && (!str(p.download) || !isHttps(p.download))) e.push('package.download must be an https address.');
    const sig = p.signature as { keyId?: unknown; value?: unknown } | undefined;
    if (sig !== undefined && (!sig || typeof sig !== 'object' || !/^[0-9a-f]{16}$/.test(String(sig.keyId ?? '')) || !/^[A-Za-z0-9+/]{86}==$/.test(String(sig.value ?? '')))) e.push('package.signature must be { keyId, value }: a store key id and an Ed25519 signature in base64.');
  }
  const shots = o.screenshots;
  if (shots !== undefined && (!Array.isArray(shots) || shots.length > 6 || shots.some((x) => typeof x !== 'string' || !(/^screenshot(?:-[1-9])?\.(?:png|webp|jpg)$/.test(x) || /^https:\/\/[^\s"'<>]+$/i.test(x))))) e.push('screenshots must be at most 6 https addresses or files of its folder (screenshot.webp, screenshot-2.png).');
  for (const k of ['homepage', 'forum'] as const) if (o[k] !== undefined && (!str(o[k]) || !isHttps(o[k] as string))) e.push(`${k} must be an https address.`);
  if (kind === 'priceList' && o.indicative !== true) e.push('A price list says its prices are indicative: indicative must be true.');
  if (o.indicative !== undefined && typeof o.indicative !== 'boolean') e.push('indicative must be true or false.');
  return e;
}

/** An index read: its good manifests, and what was wrong with the others (an entry left out, never the whole index). */
export function readIndex(json: unknown, opts: { local?: boolean } = {}): { extensions: Manifest[]; problems: string[] } {
  if (!json || typeof json !== 'object' || (json as CatalogueIndex).format !== 'lapin-extensions-index') throw new Error('This is not a catalogue of Lapin extensions.');
  if (typeof (json as CatalogueIndex).version !== 'number' || (json as CatalogueIndex).version > 1) throw new Error('The catalogue was written for a newer Lapin: update Lapin to read it.');
  const list = (json as CatalogueIndex).extensions;
  if (!Array.isArray(list)) throw new Error('The catalogue lists no extension.');
  const extensions: Manifest[] = [], problems: string[] = [];
  const seen = new Set<string>();
  for (const m of list) {
    const errs = validateManifest(m, opts);
    const id = (m as Manifest | null)?.id ?? '?';
    if (errs.length) { problems.push(`${id}: ${errs[0]}`); continue; }
    if (seen.has(id)) { problems.push(`${id}: listed twice.`); continue; }
    seen.add(id);
    extensions.push(m as Manifest);
  }
  return { extensions, problems };
}

// ------------------------------------------------------------ versions

export interface Version { major: number; minor: number; patch: number; pre: string[] }

export function parseVersion(v: string): Version | null {
  const m = SEMVER.exec(v.trim());
  if (!m) return null;
  return { major: +m[1], minor: +m[2], patch: +m[3], pre: m[4] ? m[4].split('.') : [] };
}

/** Semver's order: <0 when a comes first; a pre-release before its release. */
export function compareVersions(a: string, b: string): number {
  const x = parseVersion(a), y = parseVersion(b);
  if (!x || !y) return 0;
  for (const k of ['major', 'minor', 'patch'] as const) if (x[k] !== y[k]) return x[k] - y[k];
  if (!x.pre.length || !y.pre.length) return y.pre.length - x.pre.length;
  for (let i = 0; i < Math.max(x.pre.length, y.pre.length); i++) {
    const p = x.pre[i], q = y.pre[i];
    if (p === undefined) return -1;
    if (q === undefined) return 1;
    const pn = /^\d+$/.test(p), qn = /^\d+$/.test(q);
    if (pn && qn && +p !== +q) return +p - +q;
    if (pn !== qn) return pn ? -1 : 1;
    if (p !== q) return p < q ? -1 : 1;
  }
  return 0;
}

type Comparator = { op: '>=' | '>' | '<=' | '<' | '='; v: string };

/** A range as sets of comparators (any set may hold; each of its comparators must): `>=0.1.860 <0.3.0 || ^1.0.0`; null: not a range. */
export function parseRange(range: string): Comparator[][] | null {
  const sets: Comparator[][] = [];
  for (const part of range.split('||')) {
    const words = part.trim().split(/\s+/).filter(Boolean);
    const set: Comparator[] = [];
    for (const w of words.length ? words : ['*']) {
      if (w === '*' || w === 'x') continue;
      const m = /^(\^|~|>=|<=|>|<|=)?v?(.+)$/.exec(w);
      if (!m) return null;
      const op = m[1] ?? '=';
      // x-ranges: 0.1.x, 1.x
      const xr = /^(\d+)(?:\.(\d+|x|\*))?(?:\.(\d+|x|\*))?$/.exec(m[2]);
      if (xr && (xr[2] === undefined || xr[3] === undefined || /[x*]/.test(xr[2] + xr[3])) && op === '=') {
        const maj = +xr[1];
        if (xr[2] === undefined || /[x*]/.test(xr[2])) { set.push({ op: '>=', v: `${maj}.0.0` }, { op: '<', v: `${maj + 1}.0.0` }); continue; }
        const min = +xr[2];
        set.push({ op: '>=', v: `${maj}.${min}.0` }, { op: '<', v: `${maj}.${min + 1}.0` });
        continue;
      }
      const v = parseVersion(m[2]);
      if (!v) return null;
      if (op === '^') {
        const upper = v.major > 0 ? `${v.major + 1}.0.0` : v.minor > 0 ? `0.${v.minor + 1}.0` : `0.0.${v.patch + 1}`;
        set.push({ op: '>=', v: m[2] }, { op: '<', v: upper });
      } else if (op === '~') set.push({ op: '>=', v: m[2] }, { op: '<', v: `${v.major}.${v.minor + 1}.0` });
      else set.push({ op: op as Comparator['op'], v: m[2] });
    }
    sets.push(set);
  }
  return sets;
}

/** Whether a version is in a range; a version that is not semver (a development build's) is in none but `*`. */
export function satisfies(version: string, range: string): boolean {
  const sets = parseRange(range);
  if (!sets) return false;
  if (sets.some((s) => s.length === 0)) return true;
  if (!parseVersion(version)) return false;
  return sets.some((set) => set.every(({ op, v }) => {
    const c = compareVersions(version, v);
    return op === '>=' ? c >= 0 : op === '>' ? c > 0 : op === '<=' ? c <= 0 : op === '<' ? c < 0 : c === 0;
  }));
}

/** What the app is, for the checks: its version, whether its engine runs in the browser, the modules it has. */
export interface AppFacts { version: string; browser: boolean; hasModule: (id: string) => boolean }

/** Whether an extension can be installed here, and why not (a sentence) when it cannot. */
export function compatibility(m: Manifest, app: AppFacts): { ok: true } | { ok: false; reason: string } {
  if (!satisfies(app.version, m.lapin)) return { ok: false, reason: `It works with Lapin ${m.lapin}; this is Lapin ${app.version}.` };
  const info = KIND_INFO[m.kind];
  if (info.code && app.browser) return { ok: false, reason: 'Scripts are installed in the desktop app; the browser takes content packs only.' };
  if (info.module && !app.hasModule(info.module)) return { ok: false, reason: `This edition of Lapin has no ${info.module} module.` };
  return { ok: true };
}

/** Whether a script can run yet: it needs the Python console (the scripting module, docs/plan-python-scripting.md). */
export const runnable = (m: Manifest, app: Pick<AppFacts, 'hasModule'>): boolean => (m.requires ?? []).every((r) => app.hasModule(r)) && (!KIND_INFO[m.kind].code || app.hasModule('scripting'));
