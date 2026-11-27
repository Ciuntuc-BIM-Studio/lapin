/**
 * The Converge IFC connector's interface side (docs/plan-converge-ifc.md): the account, the projects, their IFC files and
 * versions, a send and a fetch. The IFC is made and read by Lapin's own export and import; the engine's `convergeifc.*`
 * queries only carry files.
 *
 * The token never goes into a file Lapin writes: on the desktop the shell keeps it in the system's secure store
 * (desktop/src-tauri, `secret_*`), under `convergeifc:<server>`; the interface reads it from there when a dialog opens and
 * hands it to the engine, which holds it in memory for the session (`convergeifc.setAccount`). Only the server's address,
 * the last project and file chosen, and where each file was last saved are remembered as settings. Outside the desktop app
 * (the dev server in a browser) the token is asked each session.
 */

import { create } from 'zustand';
import { engine } from '../../engine/session';
import { isDesktop, pickPath } from '../../engine/files';
import { LONG_TIMEOUT_MS } from '../../engine/client';
import { begin, end, surveyIfc } from '../../engine/ifcImport';
import { notify, useUi } from '../../app/store';

export interface ConvergeAccount { server: string; name?: string | null; email?: string | null }
export interface ConvergeProject { id: string; code: string; name: string; canUpload: boolean; roles: string[] }
export interface ConvergeFile { id: string; fileName: string; versionNo: number; revisionCode: string; status: string; sha256: string; sizeBytes: number; updatedAt?: string | null }
export interface ConvergeVersion { versionNo: number; revisionCode: string; sha256: string; sizeBytes: number; uploadedAt?: string | null; uploadedBy?: string | null; isCurrent: boolean }
export interface ConvergeSent { id: string; fileName: string; versionNo: number; revisionCode: string; sha256: string; created: boolean; unchanged: boolean }
export interface ConvergeFetched { path: string; fileName: string; sha256: string; versionNo: number; revisionCode: string }

/** The choice of a new file in the Send dialog's list. */
export const NEW_FILE = '__new__';

const KEY = { server: 'lapin.convergeifc.server', project: 'lapin.convergeifc.project', file: 'lapin.convergeifc.file', place: 'lapin.convergeifc.place:' };
const read = (k: string): string | null => { try { return localStorage.getItem(k); } catch { return null; } };
const write = (k: string, v: string) => { try { localStorage.setItem(k, v); } catch { /* this session only */ } };

/** A server's address (the engine's ConvergeServer.ServerOf): https when none is said, no path, no trailing slash. There is no default. */
export function serverOf(text: string | null | undefined): string {
  let s = (text ?? '').trim();
  if (!s) return '';
  if (!s.includes('://')) s = `https://${s}`;
  try {
    const u = new URL(s);
    if (u.protocol !== 'http:' && u.protocol !== 'https:') return s;
    return `${u.protocol}//${u.host}`;
  } catch { return s; }
}

/** The secure store's key for a server's token. */
export const secretKey = (server: string) => `convergeifc:${serverOf(server)}`;

/** What Send may add a version to: the .ifc files (what it sends is one). */
export const sendable = (files: ConvergeFile[]) => files.filter((f) => /\.ifc$/i.test(f.fileName));
/** What Get can open: .ifc, and .ifczip (unpacked on the way). */
export const gettable = (files: ConvergeFile[]) => files.filter((f) => /\.ifc(zip)?$/i.test(f.fileName));

/** A new file's name as it is sent: what was typed, ending in .ifc. */
export function ifcName(text: string): string {
  const s = text.trim();
  if (!s) return '';
  return /\.ifc$/i.test(s) ? s : `${s.replace(/\.ifczip$/i, '')}.ifc`;
}

/**
 * The file a Send starts on: the one sent to last in this project, else the one already holding the new file's name
 * (a second file of that name would be refused), else a new one.
 */
export function sendTarget(files: ConvergeFile[], last: string | null, name: string): string {
  if (last && files.some((f) => f.id === last)) return last;
  const same = files.find((f) => f.fileName.toLowerCase() === ifcName(name).toLowerCase());
  return same?.id ?? NEW_FILE;
}

type Invoke = <T>(cmd: string, args?: Record<string, unknown>) => Promise<T>;
let invoker: Invoke | null = null;
async function shell(): Promise<Invoke | null> {
  if (!isDesktop()) return null;
  if (!invoker) invoker = (await import('@tauri-apps/api/core')).invoke as Invoke;
  return invoker;
}

/** Whether the token can be remembered here (the desktop app's secure store). */
export const canRemember = () => isDesktop();

export async function readToken(server: string): Promise<string | null> {
  const invoke = await shell();
  if (!invoke) return null;
  try { return await invoke<string | null>('secret_read', { key: secretKey(server) }); } catch { return null; }
}

async function keepToken(server: string, token: string): Promise<void> {
  const invoke = await shell();
  if (invoke) await invoke('secret_write', { key: secretKey(server), value: token });
}

async function forgetToken(server: string): Promise<void> {
  const invoke = await shell();
  if (invoke) await invoke('secret_delete', { key: secretKey(server) }).catch(() => undefined);
}

const ask = <T>(name: string, args: Record<string, unknown> = {}, long = false) =>
  engine.call<T>('doc.query', { name, args }, long ? { timeoutMs: LONG_TIMEOUT_MS } : undefined);

interface ConvergeState {
  account: ConvergeAccount | null;
  /** The server the dialog shows: the last one signed in to, else none. */
  server: string;
  /** The last project and file sent to or fetched from, chosen again next time. */
  project: string | null;
  file: string | null;
}

export const useConverge = create<ConvergeState>(() => ({
  account: null,
  server: read(KEY.server) ?? '',
  project: read(KEY.project),
  file: read(KEY.file),
}));

function chose(project: string, file: string) {
  write(KEY.project, project);
  write(KEY.file, file);
  useConverge.setState({ project, file });
}

/** The account the engine holds, else signs in with the token kept for the server shown (desktop): null when there is none. */
export async function restore(): Promise<ConvergeAccount | null> {
  const held = await ask<ConvergeAccount[]>('convergeifc.accounts').catch(() => []);
  if (held.length > 0) { useConverge.setState({ account: held[0], server: held[0].server }); return held[0]; }
  const server = useConverge.getState().server;
  if (!server) return null;
  const token = await readToken(server);
  if (!token) return null;
  try { return await signIn(server, token, false); } catch { return null; }
}

/** Signs in (the token checked by the server); kept in the secure store when `remember` and on the desktop. */
export async function signIn(server: string, token: string, remember: boolean): Promise<ConvergeAccount> {
  const account = await ask<ConvergeAccount>('convergeifc.setAccount', { server: serverOf(server), token: token.trim() });
  if (remember) await keepToken(account.server, token.trim());
  write(KEY.server, account.server);
  useConverge.setState({ account, server: account.server });
  return account;
}

/** Signs out: the engine forgets the account, the secure store its token. */
export async function signOut(): Promise<void> {
  const server = useConverge.getState().account?.server ?? useConverge.getState().server;
  await ask('convergeifc.forgetAccount').catch(() => undefined);
  await forgetToken(server);
  useConverge.setState({ account: null });
}

export const listProjects = () => ask<ConvergeProject[]>('convergeifc.projects');
export const listFiles = (projectId: string) => ask<ConvergeFile[]>('convergeifc.files', { projectId });
export const listVersions = (containerId: string) => ask<ConvergeVersion[]>('convergeifc.versions', { containerId });

const cancel = () => { void engine.call('cancel.long', {}).catch(() => undefined); };

export interface SendArgs { projectId: string; /** The file to add a version to, or NEW_FILE. */ fileId: string; /** The new file's name. */ name: string; schema: string }

/** The arguments of convergeifc.send for the IFC made at `path`; the engine removes it once it is done with it. */
export function sendArgs(a: SendArgs, path: string): Record<string, unknown> {
  return { projectId: a.projectId, path, discard: true, ...(a.fileId === NEW_FILE ? { fileName: ifcName(a.name) } : { containerId: a.fileId }) };
}

/** The project exported as IFC to a file of the connector's own, then sent: a new file, or a new version of one. */
export async function sendToServer(a: SendArgs): Promise<ConvergeSent> {
  begin('Sending to Converge IFC', 'Making the IFC', cancel);
  let path: string | null = null;
  try {
    path = (await ask<{ path: string }>('convergeifc.tempPath', { name: ifcName(a.name) || 'model.ifc' })).path;
    await engine.call('project.exportIfc', { schema: a.schema, entourage: true, path }, { timeoutMs: LONG_TIMEOUT_MS });
    const sent = await ask<ConvergeSent>('convergeifc.send', sendArgs(a, path), true);
    path = null;
    chose(a.projectId, sent.id);
    return sent;
  } finally {
    // An export or a send that failed leaves no file behind.
    if (path) await ask('convergeifc.discard', { path }).catch(() => undefined);
    end();
  }
}

export interface GetArgs { projectId: string; file: ConvergeFile; versionNo: number }

/** A link of the project (link.list), as far as a fetch needs it. */
export interface LinkInfo { id: string; name: string; path?: string | null }

/** The link that reads the file at `path`, if the project has one (Windows names a file whatever the slashes and the case). */
export function linkOf(links: LinkInfo[], path: string): LinkInfo | undefined {
  const plain = (p: string) => (/^[a-z]:|^\\\\/i.test(p) ? p.replace(/\\/g, '/').toLowerCase() : p);
  return links.find((l) => !!l.path && plain(l.path) === plain(path));
}

/** Where a fetched file is proposed to be saved: where it was saved last time (a link then follows the new version), else its name. */
export const proposedPlace = (file: ConvergeFile) => read(KEY.place + file.id) ?? file.fileName.replace(/\.ifczip$/i, '.ifc');

/**
 * A version fetched and handed to the IFC import, where the user chooses how it comes in. On the desktop it is saved where
 * the user says, since a link keeps reading that file; elsewhere the engine keeps it in a file of its own. Saved over a
 * file the project already links, that link is read again instead (one step to undo), so the model is not linked twice.
 * False when the user saved nowhere.
 */
export async function getFromServer(a: GetArgs): Promise<boolean> {
  let target: string | null = null;
  if (isDesktop()) {
    target = await pickPath('save', proposedPlace(a.file), [{ name: 'IFC', extensions: ['ifc'] }]);
    if (!target) return false;
  }
  begin('Getting from Converge IFC', 'Downloading from the server', cancel);
  let got: ConvergeFetched;
  try {
    got = await ask<ConvergeFetched>('convergeifc.fetch', { containerId: a.file.id, versionNo: a.versionNo, ...(target ? { path: target } : {}) }, true);
  } finally { end(); }
  if (target) write(KEY.place + a.file.id, got.path);
  chose(a.projectId, a.file.id);
  const linked = target ? linkOf(await ask<LinkInfo[]>('link.list'), got.path) : undefined;
  if (linked) {
    begin(`Reading ${linked.name}`, 'Reading the file');
    try {
      await engine.call('project.reloadLink', { id: linked.id }, { timeoutMs: LONG_TIMEOUT_MS });
      notify(`${linked.name} read again.`);
      useUi.setState({ dialog: null });
    } finally { end(); }
    return true;
  }
  await surveyIfc({ name: got.path.split(/[\\/]/).pop() ?? got.fileName, path: got.path }, 'auto');
  return true;
}
