/**
 * Lapin Extensions in the app (docs/plan-extensions.md, stage 0): where the catalogue is read from, how its files are
 * downloaded, the installers of each kind (each module registers its own, `addContentInstaller`, as it registers what Save
 * as template… may add), and what is installed on this computer (`lapin.extensions`, kept with the other `lapin.*`
 * settings, so the desktop's copy keeps it too).
 *
 * The catalogue's address: the one typed in Extensions → Catalogue, else LAPIN_EXTENSIONS_INDEX given to the desktop's
 * engine (a local index while developing), else Lapin's own, https://lapinbim.com/extensions/index.json. The desktop reads
 * through its engine (`extensions.fetch`: its webview reaches no other site, and GitHub's release files answer no browser);
 * the browser reads with fetch, the packages from lapinbim.com's copies.
 */

import { create } from 'zustand';
import { engine, engineInBrowser, useSession } from '../../engine/session';
import { deleteTemplate, importTemplate, indexedRows } from '../../engine/templates';
import { toBase64 } from '../../engine/files';
import { hasModule } from '../brand';
import { installExtension, uninstallExtension, type ContentInstaller, type InstalledExtension, type InstalledStore } from './install';
import { readIndex, type AppFacts, type ExtensionKind, type Manifest } from './manifest';

export const DEFAULT_INDEX = 'https://lapinbim.com/extensions/index.json';
const INDEX_KEY = 'lapin.extensions.index';
const INSTALLED_KEY = 'lapin.extensions';

// ------------------------------------------------------------ installers, by kind

const installers = new Map<ExtensionKind, ContentInstaller>();

/** A kind's installer (one per kind; the last registered stands). */
export function addContentInstaller(i: ContentInstaller): void { installers.set(i.kind, i); }
export const contentInstallers = (): ReadonlyMap<ExtensionKind, ContentInstaller> => installers;

// ------------------------------------------------------------ what is installed

function readInstalled(): InstalledExtension[] {
  try {
    const v = JSON.parse(localStorage.getItem(INSTALLED_KEY) ?? '[]') as unknown;
    return Array.isArray(v) ? v.filter((x): x is InstalledExtension => !!x && typeof x === 'object' && typeof (x as InstalledExtension).id === 'string') : [];
  } catch { return []; }
}

function writeInstalled(all: InstalledExtension[]): void {
  try { localStorage.setItem(INSTALLED_KEY, JSON.stringify(all)); } catch { /* kept for this session in the store */ }
  useExtensions.setState({ installed: all });
}

export const installedStore: InstalledStore = {
  all: () => useExtensions.getState().installed,
  put: (x) => writeInstalled([...useExtensions.getState().installed.filter((r) => r.id !== x.id), x]),
  remove: (id) => writeInstalled(useExtensions.getState().installed.filter((r) => r.id !== id)),
};

/** A script installed (kept until the Python console runs it: docs/plan-python-scripting.md); the page's IndexedDB, the desktop's too. */
export interface ScriptRow { id: string; name: string; file: string; text: string; at: string }
export const scriptRows = indexedRows<ScriptRow>('lapin-extensions', 'scripts', 'script');

// ------------------------------------------------------------ the catalogue

export interface CatalogueState {
  /** The address typed in the dialog (null: the engine's or Lapin's own). */
  chosen: string | null;
  /** Where the catalogue was read from. */
  from: string | null;
  list: Manifest[];
  problems: string[];
  loading: boolean;
  error: string | null;
  installed: InstalledExtension[];
  /** The extension being installed or removed. */
  busy: string | null;
}

const storedIndex = (): string | null => { try { return localStorage.getItem(INDEX_KEY) || null; } catch { return null; } };

export const useExtensions = create<CatalogueState>(() => ({ chosen: storedIndex(), from: null, list: [], problems: [], loading: false, error: null, installed: readInstalled(), busy: null }));

/** The catalogue's address set (empty: back to the engine's or Lapin's own). */
export function setCatalogueAddress(address: string): void {
  const v = address.trim() || null;
  try { if (v) localStorage.setItem(INDEX_KEY, v); else localStorage.removeItem(INDEX_KEY); } catch { /* this session only */ }
  useExtensions.setState({ chosen: v });
}

const fromBase64 = (data: string) => Uint8Array.from(atob(data), (c) => c.charCodeAt(0));

/** The bytes at an address: through the desktop's engine, or the browser's fetch. */
export async function fetchBytes(url: string): Promise<Uint8Array> {
  if (!engineInBrowser) {
    const r = await engine.call<{ data: string }>('extensions.fetch', { url }, { timeoutMs: 120_000 });
    return fromBase64(r.data);
  }
  const res = await fetch(url, { cache: 'no-cache' });
  if (!res.ok) throw new Error(`${new URL(url, location.href).host} answered ${res.status}.`);
  return new Uint8Array(await res.arrayBuffer());
}

/** Where the catalogue is read from now. */
export async function catalogueAddress(): Promise<string> {
  const chosen = useExtensions.getState().chosen;
  if (chosen) return chosen;
  if (!engineInBrowser) {
    const r = await engine.call<{ index?: string | null }>('extensions.source').catch(() => null);
    if (r?.index) return r.index;
  }
  return DEFAULT_INDEX;
}

/** A catalogue on this computer (a path, a file:// or a localhost address) may name its packages by paths beside it. */
export const isLocalAddress = (a: string) => !/^https:\/\//i.test(a) || /^https?:\/\/(localhost|127\.0\.0\.1)[:/]/i.test(a);

/** The catalogue read (again). */
let reading = 0;

export async function loadCatalogue(): Promise<void> {
  // Only the latest read counts: an earlier one answering late (Lapin's own catalogue, unreachable, after another was chosen) is dropped.
  const mine = ++reading;
  useExtensions.setState({ loading: true, error: null });
  try {
    const from = await catalogueAddress();
    const bytes = await fetchBytes(from);
    const { extensions, problems } = readIndex(JSON.parse(new TextDecoder().decode(bytes)), { local: isLocalAddress(from) });
    if (mine !== reading) return;
    useExtensions.setState({ from, list: extensions, problems, loading: false });
    if (problems.length) console.info('Extensions left out of the catalogue:', problems.join(' '));
  } catch (e) {
    if (mine !== reading) return;
    useExtensions.setState({ loading: false, error: e instanceof Error ? e.message : String(e), list: [] });
  }
}

/** What the checks know of this app. */
export function appFacts(): AppFacts {
  return { version: useSession.getState().info?.engineVersion ?? '0.0.0', browser: engineInBrowser, hasModule };
}

/** An extension installed (or updated) from the catalogue. */
export async function install(m: Manifest): Promise<InstalledExtension> {
  useExtensions.setState({ busy: m.id });
  try {
    const base = useExtensions.getState().from ?? undefined;
    return await installExtension(m, { app: appFacts(), fetchBytes, installers, store: installedStore, confirm: (q) => window.confirm(q), base });
  } finally { useExtensions.setState({ busy: null }); }
}

export async function uninstall(id: string): Promise<void> {
  useExtensions.setState({ busy: id });
  try { await uninstallExtension(id, { installers, store: installedStore }); } finally { useExtensions.setState({ busy: null }); }
}

// ------------------------------------------------------------ the shell's own kinds: project templates, scripts

const text = (bytes: Uint8Array) => new TextDecoder('utf-8', { fatal: true }).decode(bytes);

/** A Python script: kept as it is, never run here; the console (scripting module) will list it under My scripts. */
addContentInstaller({
  kind: 'script',
  install: async (file, m) => {
    let code: string;
    try { code = text(file.bytes); } catch { throw new Error('The script is not UTF-8 text.'); }
    if (code.includes('\0')) throw new Error('The script is not text.');
    await scriptRows.put({ id: m.id, name: m.name, file: file.name, text: code, at: new Date().toISOString() });
    return { script: m.id };
  },
  uninstall: async (r) => { if (typeof r.script === 'string') await scriptRows.remove(r.script); },
});

/** A project template (.lapin): into the templates on this computer (File → New from template…), named after the extension. */
addContentInstaller({
  kind: 'template',
  install: async (file, m, ctx) => {
    if (file.bytes[0] !== 0x50 || file.bytes[1] !== 0x4b) throw new Error('The template is not a .lapin file.');
    const data = toBase64(file.bytes);
    let id = await importTemplate({ data }, m.name);
    if (id === 'exists') {
      if (!(await ctx.confirm(`A template is already called „${m.name}” on this computer. Replace it?`))) throw new Error('Not installed: yours was kept.');
      id = await importTemplate({ data }, m.name, true);
    }
    return { template: id };
  },
  uninstall: async (r) => { if (typeof r.template === 'string') await deleteTemplate(r.template).catch(() => undefined); },
  open: { label: 'Project templates…', run: () => { void import('../../app/store').then(({ useUi }) => useUi.setState({ dialog: 'projectTemplates' })); } },
});
