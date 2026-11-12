/**
 * Installing an extension (docs/plan-extensions.md, stage 0): the package downloaded, its size and SHA-256 checked against
 * the manifest, then handed to the installer of its kind, which takes it in through Lapin's own importer (my furniture, the
 * project templates, the profile libraries, my patterns, the title blocks' library, the price lists' library) and returns
 * a receipt: what it added, so Uninstall takes away that and nothing else. Nothing is ever run: a script is kept as a file
 * until the Python console exists. Pure: the download, the stores and the questions are given (tests give fakes).
 */

import { compatibility, compareVersions, KIND_INFO, MAX_PACKAGE, type AppFacts, type ExtensionKind, type Manifest } from './manifest';

/** What an installer added, kept to take it away again (ids, names); JSON. */
export type Receipt = Record<string, unknown>;

export interface InstallerContext {
  /** A yes/no question to the user (a library item of the same name already there: replace it?). */
  confirm: (question: string) => boolean | Promise<boolean>;
}

/** How one kind's packages go in and come out, through the library that kind belongs to. */
export interface ContentInstaller {
  kind: ExtensionKind;
  /** Takes the checked file in; throws a sentence when the file is not what the kind takes (nothing kept then). */
  install: (file: { name: string; bytes: Uint8Array }, m: Manifest, ctx: InstallerContext) => Promise<Receipt>;
  /** Takes away what `install` added (what is already gone is no error). */
  uninstall: (receipt: Receipt, m: InstalledExtension) => Promise<void>;
  /** Where the user finds it once installed (My furniture…): a button in the Installed list. */
  open?: { label: string; run: (x: InstalledExtension) => void };
}

/** An extension installed on this computer: its manifest as installed, and the receipt of what it added. */
export interface InstalledExtension { id: string; version: string; kind: ExtensionKind; name: string; at: string; manifest: Manifest; receipt: Receipt }

export interface InstalledStore {
  all: () => InstalledExtension[];
  put: (x: InstalledExtension) => void;
  remove: (id: string) => void;
}

export interface InstallDeps {
  app: AppFacts;
  /** The bytes at an address (an https address, or a file of a local catalogue). */
  fetchBytes: (url: string) => Promise<Uint8Array>;
  installers: ReadonlyMap<ExtensionKind, ContentInstaller>;
  store: InstalledStore;
  confirm: InstallerContext['confirm'];
  /** The index's own address, against which a relative package path is read. */
  base?: string;
  now?: () => string;
}

/** A package's address read against the index's: an absolute URL or path as it is; a relative one beside the index. */
export function resolveUrl(base: string | undefined, rel: string): string {
  if (/^[a-z][a-z0-9+.-]*:/i.test(rel) || rel.startsWith('/') || /^[A-Za-z]:[\\/]/.test(rel)) return rel;
  if (!base) return rel;
  if (/^[a-z][a-z0-9+.-]*:\/\//i.test(base)) return new URL(rel, base).toString();
  const cut = Math.max(base.lastIndexOf('/'), base.lastIndexOf('\\'));
  return `${cut >= 0 ? base.slice(0, cut + 1) : ''}${rel}`;
}

/** The SHA-256 of bytes, as 64 lowercase hex digits (the browser's own digest). */
export async function sha256Hex(bytes: Uint8Array): Promise<string> {
  const digest = await globalThis.crypto.subtle.digest('SHA-256', bytes as BufferSource);
  return [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, '0')).join('');
}

/** The package's bytes, from the address the app can read (the browser: lapinbim.com's copy when there is one), checked. */
export async function download(m: Manifest, deps: Pick<InstallDeps, 'fetchBytes' | 'base' | 'app'>): Promise<Uint8Array> {
  const from = resolveUrl(deps.base, deps.app.browser && m.package.mirror ? m.package.mirror : m.package.url);
  const bytes = await deps.fetchBytes(from);
  if (bytes.length > MAX_PACKAGE) throw new Error('The package is bigger than an extension may be.');
  if (bytes.length !== m.package.size) throw new Error(`The package is ${bytes.length} bytes, its manifest says ${m.package.size}: it was not installed.`);
  const sum = await sha256Hex(bytes);
  if (sum !== m.package.sha256) throw new Error('The package is not the one its manifest names (its SHA-256 differs): it was not installed.');
  return bytes;
}

/** Downloads, checks and installs an extension; its record. An extension already installed is updated (the old one taken away first). */
export async function installExtension(m: Manifest, deps: InstallDeps): Promise<InstalledExtension> {
  const can = compatibility(m, deps.app);
  if (!can.ok) throw new Error(can.reason);
  const installer = deps.installers.get(m.kind);
  if (!installer) throw new Error(`This edition of Lapin cannot install ${KIND_INFO[m.kind].label.toLowerCase()}.`);
  // Downloaded and checked before anything is touched: a bad package leaves the installed one as it was.
  const bytes = await download(m, deps);
  const old = deps.store.all().find((x) => x.id === m.id);
  if (old) await uninstallExtension(old.id, deps);
  const receipt = await installer.install({ name: m.contents.file, bytes }, m, { confirm: deps.confirm });
  const record: InstalledExtension = { id: m.id, version: m.version, kind: m.kind, name: m.name, at: deps.now?.() ?? new Date().toISOString(), manifest: m, receipt };
  deps.store.put(record);
  return record;
}

/** Takes away what an extension added, and its record. */
export async function uninstallExtension(id: string, deps: Pick<InstallDeps, 'installers' | 'store'>): Promise<void> {
  const x = deps.store.all().find((r) => r.id === id);
  if (!x) return;
  const installer = deps.installers.get(x.kind);
  if (installer) await installer.uninstall(x.receipt, x);
  deps.store.remove(id);
}

/** The installed extensions the catalogue has a newer version of, which this app can take. */
export function updatesOf(installed: InstalledExtension[], catalogue: Manifest[], app: AppFacts): Manifest[] {
  const byId = new Map(catalogue.map((m) => [m.id, m]));
  return installed.map((x) => byId.get(x.id)).filter((m, i): m is Manifest => !!m && compareVersions(m.version, installed[i].version) > 0 && compatibility(m, app).ok);
}

// ------------------------------------------------------------ the catalogue as Browse shows it

/** Browse's categories: the kinds, in the order the list shows them. */
export const CATEGORY_ORDER: ExtensionKind[] = ['furniture', 'template', 'types', 'profiles', 'hatches', 'titleBlock', 'priceList', 'script'];

/** The extensions matching the search (name, summary, author, tags, items) and the kind chosen, by name. */
export function filterCatalogue(list: Manifest[], f: { search?: string; kind?: ExtensionKind | null }): Manifest[] {
  const words = (f.search ?? '').toLowerCase().split(/\s+/).filter(Boolean);
  return list
    .filter((m) => !f.kind || m.kind === f.kind)
    .filter((m) => {
      const text = [m.name, m.summary, m.description ?? '', m.author.name, ...(m.tags ?? []), ...(m.contents.items ?? []), m.id].join(' ').toLowerCase();
      return words.every((w) => text.includes(w));
    })
    .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
}

/** How many of each kind the catalogue has (the category chips' counts). */
export function countByKind(list: Manifest[]): Partial<Record<ExtensionKind, number>> {
  const out: Partial<Record<ExtensionKind, number>> = {};
  for (const m of list) out[m.kind] = (out[m.kind] ?? 0) + 1;
  return out;
}

/** A size as people read it. */
export const sizeText = (bytes: number) => (bytes < 1024 ? `${bytes} B` : bytes < 1024 * 1024 ? `${Math.round(bytes / 1024)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`);
