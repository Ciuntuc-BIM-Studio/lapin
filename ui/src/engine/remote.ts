/**
 * The project's history and its remote (lapin-remote 1.0, docs/spec-lapin-remote.md; docs/remote.md): the engine's history.* and
 * remote.* calls with what the interface adds to each, the author (this installation's `local:` id, or the signed-in account's) and a
 * server's tokens. Tokens are never written into a project: the access token lives in memory only; the refresh token, on the desktop,
 * in the shell's store of sign-ins (remote_secret_get/set, readable by the user alone; the system's keychain from S2); in the browser,
 * in this tab's memory only, so a reload signs out; in front of a CDE server (engine/cdeServer.ts, W2) the server's sign-in is the
 * start screen's, one for the interface and the engine (access token in memory, refresh token in sessionStorage).
 */

import { engine, engineInBrowser } from './session';
import { isDesktop, keepProjectFile, reportWarnings, saveProject } from './files';
import { LONG_TIMEOUT_MS } from './client';
import { useSession } from './session';
import { notify, useUi } from '../app/store';
import { cdeTokens, isCdeServer, keepCdeTokens, signIn as cdeSignIn, signOut as cdeSignOut, useCde } from './cdeServer';

export interface RemoteInfo { name: string; kind: 'folder' | 'server'; url: string; project: string; ahead: number; behind: number; fetchedAt: string | null; remoteHead: string | null }
export interface CommitInfo { id: string; message: string; at: string; author: { id: string; name: string }; entities: number | null; parents: string[]; tags: string[]; pushed?: boolean }
export interface HistoryStatus {
  branch: string; head: string | null; uncommitted: boolean; saved: boolean; problems: string[]; unreadable: string | null;
  commit?: CommitInfo; remotes: RemoteInfo[];
}
export interface Author { id: string; name: string }

const INSTALL_KEY = 'lapin.remote.installation';
const NAME_KEY = 'lapin.remote.name';
const ACCOUNT_KEY = 'lapin.remote.account.';

function storage(): Storage | null {
  try { return window.localStorage; } catch { return null; }
}

/** This installation's id (16 hex digits, made once, kept in the user's settings, never in projects): the author of commits made offline or on a folder. */
export function installationId(): string {
  const s = storage();
  let id = s?.getItem(INSTALL_KEY) ?? '';
  if (!/^[0-9a-f]{16}$/.test(id)) {
    const bytes = new Uint8Array(8);
    crypto.getRandomValues(bytes);
    id = [...bytes].map((b) => b.toString(16).padStart(2, '0')).join('');
    s?.setItem(INSTALL_KEY, id);
  }
  return id;
}

/** The name commits carry: the signed-in account's, else the one the user gave, else "Lapin". */
export const authorName = () => storage()?.getItem(NAME_KEY) || 'Lapin';
export const setAuthorName = (name: string) => storage()?.setItem(NAME_KEY, name.trim());

/** The author of what this installation does on a remote: the account's public id when signed in to that server, else local. */
export function authorFor(server: string | null): Author {
  // The CDE's sign-in: the account's name (Foundation names the account by its e-mail, which never goes into a commit).
  const cde = isCdeServer(server) ? useCde.getState().user : null;
  if (cde?.name) return { id: `local:${installationId()}`, name: cde.name };
  if (server) {
    const raw = storage()?.getItem(ACCOUNT_KEY + server);
    if (raw) {
      try { const a = JSON.parse(raw) as Partial<Author>; if (a.id && a.name) return { id: a.id, name: a.name }; } catch { /* a damaged entry: local */ }
    }
  }
  return { id: `local:${installationId()}`, name: authorName() };
}

// ------------------------------------------------------------ tokens

interface Tokens { access: string | null; refresh: string | null }
const tokens = new Map<string, Tokens>();

async function invoke<T>(cmd: string, args: Record<string, unknown>): Promise<T> {
  const { invoke } = await import('@tauri-apps/api/core');
  return invoke<T>(cmd, args);
}

async function keepRefresh(server: string, refresh: string | null): Promise<void> {
  if (!isDesktop()) return;
  try { await invoke('remote_secret_set', { key: server, value: refresh }); } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
}

/** A server's tokens: those of this session, else the refresh token the desktop kept (a call will refresh it). */
async function tokensOf(server: string): Promise<Tokens> {
  const t = tokens.get(server);
  if (t) return t;
  let refresh: string | null = null;
  if (isDesktop()) { try { refresh = await invoke<string | null>('remote_secret_get', { key: server }); } catch { refresh = null; } }
  const fresh = { access: null, refresh };
  tokens.set(server, fresh);
  return fresh;
}

export async function signedIn(server: string): Promise<boolean> {
  if (isCdeServer(server)) return Boolean(useCde.getState().user);
  const t = await tokensOf(server);
  return Boolean(t.access || t.refresh);
}

/** A sign-in with the account's e-mail and password (OAuth2 password grant, client lapin-desktop or lapin-web); the password goes no further. */
export async function signIn(server: string, username: string, password: string): Promise<Author | null> {
  if (isCdeServer(server)) {
    await cdeSignIn(username, password);
    return authorFor(server);
  }
  const r = await engine.call<{ accessToken: string; refreshToken: string | null; user?: { id?: string; name?: string } }>('remote.signIn', {
    url: server, username, password, clientId: engineInBrowser ? 'lapin-web' : 'lapin-desktop',
  }, { timeoutMs: 60_000 });
  tokens.set(server, { access: r.accessToken, refresh: r.refreshToken });
  await keepRefresh(server, r.refreshToken);
  const name = r.user?.name?.trim();
  if (name) {
    // The account's public id when the server says it; else this installation's id with the account's name.
    const author = r.user?.id ? { id: r.user.id, name } : { id: `local:${installationId()}`, name };
    storage()?.setItem(ACCOUNT_KEY + server, JSON.stringify(author));
    return author;
  }
  return null;
}

export async function signOut(server: string): Promise<void> {
  if (isCdeServer(server)) { cdeSignOut(); return; }
  tokens.delete(server);
  storage()?.removeItem(ACCOUNT_KEY + server);
  await keepRefresh(server, null);
}

/**
 * The signed-in account's password changed on the server (Atrium Server's POST /atrium/1.0/account/password, through the engine as the
 * sign-in goes). The server ends every sign-in of the account and hands back a fresh pair, kept as a refresh's are; the passwords go no
 * further than the request and are never kept. A refusal comes back with its code (wrong_password, weak_password, too_many_attempts,
 * not_supported, unauthorized) and a message to show.
 */
export async function changePassword(server: string, currentPassword: string, newPassword: string): Promise<{ changed: boolean; code?: string; message?: string }> {
  return remoteCall<{ changed: boolean; code?: string; message?: string }>('remote.changePassword', { url: server, currentPassword, newPassword }, server);
}

/** One call of the history or the remote: the author and, for a server, its tokens added; tokens a refresh replaced kept at once. */
export async function remoteCall<T>(method: string, args: Record<string, unknown> = {}, server: string | null = null): Promise<T> {
  const extra: Record<string, unknown> = { author: authorFor(server) };
  const cde = isCdeServer(server);
  if (server) {
    const t = cde ? await cdeTokens() : await tokensOf(server);
    if (t.access) extra.token = t.access;
    if (t.refresh) extra.refreshToken = t.refresh;
  }
  const r = await engine.call<T & { tokens?: { accessToken: string | null; refreshToken: string | null } }>(method, { ...args, ...extra }, { timeoutMs: LONG_TIMEOUT_MS });
  if (server && r && typeof r === 'object' && r.tokens) {
    if (cde) keepCdeTokens(r.tokens.accessToken, r.tokens.refreshToken);
    else {
      tokens.set(server, { access: r.tokens.accessToken, refresh: r.tokens.refreshToken });
      await keepRefresh(server, r.tokens.refreshToken);
    }
  }
  noteStatus(r);
  reportWarnings(r);
  return r;
}

/**
 * What the browser holds that the CDE server does not, from the status a history or remote call answers (W2): commits not pushed,
 * changes not committed. Only for a project whose remote is the CDE server; any other project clears it.
 */
export function noteStatus(r: unknown): void {
  if (!useCde.getState().server || !r || typeof r !== 'object') return;
  // A status as the answer itself (history.status, remote.add, remote.remove) or beside it (a commit, a push, a pull, a clone).
  const s = ((r as { status?: unknown }).status ?? (Array.isArray((r as HistoryStatus).remotes) ? r : undefined)) as HistoryStatus | undefined;
  if (!s || !Array.isArray(s.remotes)) return;
  const remote = s.remotes[0];
  const linked = Boolean(remote && isCdeServer(remote.url));
  useCde.setState({ linked, unpushed: linked && (remote.ahead > 0 || s.uncommitted) ? { ahead: remote.ahead, uncommitted: s.uncommitted } : null });
}

/** The remote's server address, when the project's remote is a server. */
export const serverOf = (s: HistoryStatus | null) => s?.remotes[0]?.kind === 'server' ? s.remotes[0].url : null;

export const historyStatus = () => remoteCall<HistoryStatus>('history.status');
export const historyLog = (limit = 50) => remoteCall<CommitInfo[]>('history.log', { limit });

/**
 * A call that writes the project's file (a commit, a pull, a remote added): in the browser the engine hands the file back, kept as a
 * save keeps it; on the desktop a project without a file is saved first. Null: the user stepped back.
 */
export async function writingCall<T extends object>(method: string, args: Record<string, unknown>, server: string | null): Promise<T | null> {
  if (engineInBrowser) {
    const r = await remoteCall<T & { name?: string; data?: string }>(method, { ...args, bytes: true }, server);
    // A project whose remote is the CDE server keeps its file in the browser's engine (its history with it): the server is where it
    // is saved (Save to server), so no download each time; Save still keeps a copy on this computer.
    const status = (r as { status?: HistoryStatus }).status ?? (r as unknown as HistoryStatus);
    if (isCdeServer(status?.remotes?.[0]?.url)) return r;
    if (r.data && r.name && !await keepProjectFile({ name: r.name, data: r.data })) return null;
    return r;
  }
  if (!useSession.getState().doc?.path) {
    notify('Save the project first: its history lives in its file.');
    if (!await saveProject(true)) return null;
  }
  return remoteCall<T>(method, args, server);
}

/**
 * File → Get latest from server: the remote's new commits brought into the project. Without a remote, the Collaborate dialog opens to
 * connect one; with changes not saved to the server yet, or a history that went another way, it opens too, where the choices are.
 */
export async function getLatest(): Promise<void> {
  const open = (why: string) => { notify(why); useUi.setState({ dialog: 'remote' }); };
  try {
    const s = await historyStatus();
    const remote = s.remotes[0];
    if (!remote) return open('Connect a server or folder first.');
    const server = serverOf(s);
    if (server && !await signedIn(server)) return open('Sign in to the server first.');
    if (s.uncommitted) return open('Save your changes to the server first (or keep them as a version), then get the latest.');
    const fetched = await remoteCall<{ notes: string[] }>('remote.fetch', {}, server);
    for (const n of fetched.notes ?? []) notify(n);
    const o = await writingCall<{ pulled?: boolean; kind?: string; message?: string; notes?: string[] }>('remote.pull', {}, server);
    if (!o) return;
    for (const n of o.notes ?? []) notify(n);
    if (o.pulled === false) return open(o.message ?? 'Someone saved before you: choose what to keep.');
    notify('Up to date with the server.');
  } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
}
