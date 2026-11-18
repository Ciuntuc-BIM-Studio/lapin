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
 *
 * Stage 1: the catalogue's signature (`<index>.sig`) is read with it and checked against the store keys Lapin carries
 * (keys.ts). Lapin's own catalogue must be signed once Lapin carries a key; another may be unsigned (the dialog says so),
 * never wrongly signed. A package of Lapin's own catalogue must then be signed too (install.ts, signature.ts).
 *
 * Stage 2a (§12): a paid extension's licence key, kept here (`lapin.extensions.licences`, with the other `lapin.*`
 * settings: the page's storage, and on the desktop the user's settings.json too) with what its last conclusive check said;
 * sent to the store by POST (the desktop through its engine, `extensions.post`; the browser with fetch). It never goes into
 * a project, a report or an export.
 */

import { confirmAction } from '../../shell/prompts';
import { create } from 'zustand';
import { engine, engineInBrowser, useSession } from '../../engine/session';
import { deleteTemplate, importTemplate, indexedRows } from '../../engine/templates';
import { toBase64 } from '../../engine/files';
import { hasModule } from '../brand';
import { download, installExtension, uninstallExtension, type ContentInstaller, type InstalledExtension, type InstalledStore } from './install';
import { readIndex, type AppFacts, type ExtensionKind, type Manifest } from './manifest';
import { STORE_KEYS } from './keys';
import { afterCheck, checkLicenceKey, dueForCheck, LicenceError, type LicenceRecord, type StoreAnswer } from './licence';
import { isPaid } from './manifest';
import { catalogueTrust, trustProblem, type CatalogueTrust } from './signature';

export const DEFAULT_INDEX = 'https://lapinbim.com/extensions/index.json';
const INDEX_KEY = 'lapin.extensions.index';
const INSTALLED_KEY = 'lapin.extensions';
const LICENCES_KEY = 'lapin.extensions.licences';

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

// ------------------------------------------------------------ paid extensions' licence keys (stage 2a)

function readLicences(): Record<string, LicenceRecord> {
  try {
    const v = JSON.parse(localStorage.getItem(LICENCES_KEY) ?? '{}') as unknown;
    if (!v || typeof v !== 'object' || Array.isArray(v)) return {};
    return Object.fromEntries(Object.entries(v as Record<string, LicenceRecord>).filter(([, r]) => r && typeof r.key === 'string' && (r.status === 'active' || r.status === 'revoked')));
  } catch { return {}; }
}

function writeLicences(all: Record<string, LicenceRecord>): void {
  try { localStorage.setItem(LICENCES_KEY, JSON.stringify(all)); } catch { /* kept for this session in the store */ }
  useExtensions.setState({ licences: all });
}

const putLicence = (id: string, r: LicenceRecord) => writeLicences({ ...useExtensions.getState().licences, [id]: r });

/** The licence key kept for an extension forgotten (Forget licence key); what is installed stays. */
export function forgetLicence(id: string): void {
  const { [id]: _gone, ...rest } = useExtensions.getState().licences;
  writeLicences(rest);
}

/** A JSON body POSTed to the store: through the desktop's engine, or the browser's fetch (the store answers CORS). */
export async function postJson(url: string, body: string): Promise<StoreAnswer> {
  if (!engineInBrowser) {
    const r = await engine.call<{ status: number; type?: string | null; data: string }>('extensions.post', { url, body }, { timeoutMs: 120_000 });
    return { status: r.status, type: r.type ?? null, bytes: fromBase64(r.data) };
  }
  const res = await fetch(url, { method: 'POST', headers: { 'content-type': 'application/json' }, body, cache: 'no-store', credentials: 'omit' });
  return { status: res.status, type: res.headers.get('content-type'), bytes: new Uint8Array(await res.arrayBuffer()) };
}

/** The installed paid extensions due a check (an update offered, or a week since), checked; their records updated. Offline: as they were. */
export async function recheckLicences(now = Date.now()): Promise<void> {
  const { installed, list, licences } = useExtensions.getState();
  for (const m of dueForCheck(installed, list, licences, now)) {
    const rec = useExtensions.getState().licences[m.id];
    if (!rec) continue;
    const status = await checkLicenceKey(m, rec.key, postJson);
    const next = afterCheck(rec, status, new Date(now).toISOString());
    if (next !== rec) putLicence(m.id, next);
  }
}

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
  /** How the catalogue read is signed: by a store key Lapin trusts, or not at all (null: not read yet). */
  trust: CatalogueTrust | null;
  /** The paid extensions' licence keys kept on this computer, by extension id (stage 2a). */
  licences: Record<string, LicenceRecord>;
}

const storedIndex = (): string | null => { try { return localStorage.getItem(INDEX_KEY) || null; } catch { return null; } };

export const useExtensions = create<CatalogueState>(() => ({ chosen: storedIndex(), from: null, list: [], problems: [], loading: false, error: null, installed: readInstalled(), busy: null, trust: null, licences: readLicences() }));

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
    // Its signature beside it: none (a 404, an unsigned catalogue of one's own) reads as unsigned.
    const sig = await fetchBytes(`${from}.sig`).catch(() => null);
    const trust = await catalogueTrust(STORE_KEYS, bytes, sig);
    const problem = trustProblem(trust, { isDefault: from === DEFAULT_INDEX, keys: STORE_KEYS });
    if (problem) throw new Error(problem);
    const { extensions, problems } = readIndex(JSON.parse(new TextDecoder().decode(bytes)), { local: isLocalAddress(from) });
    if (mine !== reading) return;
    useExtensions.setState({ from, list: extensions, problems, loading: false, trust });
    if (problems.length) console.info('Extensions left out of the catalogue:', problems.join(' '));
    // Paid extensions installed: their keys checked again when due (never blocking, never removing anything).
    void recheckLicences().catch(() => undefined);
  } catch (e) {
    if (mine !== reading) return;
    useExtensions.setState({ loading: false, error: e instanceof Error ? e.message : String(e), list: [], trust: null });
  }
}

/** What the checks know of this app. */
export function appFacts(): AppFacts {
  return { version: useSession.getState().info?.engineVersion ?? '0.0.0', browser: engineInBrowser, hasModule };
}

/**
 * An extension installed (or updated) from the catalogue. A paid one takes the key typed (`key`) or the one kept; a good
 * key is kept, one its seller revoked is remembered as revoked (no more updates; nothing removed).
 */
export async function install(m: Manifest, key?: string): Promise<InstalledExtension> {
  useExtensions.setState({ busy: m.id });
  const kept = useExtensions.getState().licences[m.id];
  const useKey = key ?? kept?.key ?? null;
  try {
    const base = useExtensions.getState().from ?? undefined;
    const signature = { keys: STORE_KEYS, required: base === DEFAULT_INDEX && STORE_KEYS.length > 0 };
    const x = await installExtension(m, { app: appFacts(), fetchBytes, installers, store: installedStore, confirm: (q) => confirmAction(q), base, signature, licence: { key: useKey, post: postJson } });
    if (isPaid(m) && useKey) putLicence(m.id, { key: useKey, provider: m.purchase.provider, status: 'active', checkedAt: new Date().toISOString() });
    return x;
  } catch (e) {
    // The key kept, now refunded or disabled: remembered so (Installed says it, Update is no longer offered).
    if (e instanceof LicenceError && (e.reason === 'revoked' || e.reason === 'invalid') && kept && useKey === kept.key) putLicence(m.id, { ...kept, status: 'revoked', checkedAt: new Date().toISOString() });
    throw e;
  } finally { useExtensions.setState({ busy: null }); }
}

/** A package's bytes, downloaded and checked (size, SHA-256, signature) like an install's, and nothing installed: for a preview before Install. */
export async function fetchPackage(m: Manifest, key?: string): Promise<Uint8Array> {
  const base = useExtensions.getState().from ?? undefined;
  const signature = { keys: STORE_KEYS, required: base === DEFAULT_INDEX && STORE_KEYS.length > 0 };
  const useKey = key ?? useExtensions.getState().licences[m.id]?.key ?? null;
  return download(m, { app: appFacts(), fetchBytes, base, signature, licence: { key: useKey, post: postJson } });
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
