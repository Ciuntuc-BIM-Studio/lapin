/**
 * Lapin on the web in front of a CDE server (Atrium Server, its plan: Atrium docs/plan-cde-web.md, stage W1): the interface
 * served from the server's own origin talks to its APIs on that origin (/foundation, /lapin-remote/1.0, /atrium/1.0), so no
 * token ever crosses origins. W1 is read-only: sign in, the projects, a project's history, branches, versions and issues,
 * activity and members, the account's password; a commit opens through its package (`…/commits/{c}/package`, an ordinary
 * .lapin) in the browser's engine, which never writes back. Saving to the server from the browser waits for the engine's
 * asynchronous remote path (W2): the engine's HttpRemote is synchronous, which the browser's WebAssembly cannot run.
 *
 * Tokens: the access token lives in this tab's memory only; the refresh token (single use, rotated at each refresh) in
 * sessionStorage, so a reload keeps the sign-in and closing the tab ends it. Never localStorage, never a cookie.
 */

import { create } from 'zustand';
import { engineInBrowser } from './session';

/** A CDE server's origin when the interface is served by one; null otherwise. */
export interface CdeState {
  /** The server's origin ('' while not known, null when there is none). */
  server: string | null | '';
  user: { id: string; name: string } | null;
  /** The user chose to work on local projects (the ordinary start screen). */
  offline: boolean;
  /** The commit last opened from the server (read-only). */
  opened: { project: string; commit: string } | null;
}

export const useCde = create<CdeState>(() => ({ server: '', user: null, offline: false, opened: null }));

/** True when the interface is in front of a CDE server. */
export const cdeActive = () => Boolean(useCde.getState().server);

/** An answer of the server that is not a success: its HTTP status, its code (`not_found`, `invalid_grant`…) and its message. */
export class CdeError extends Error {
  constructor(public status: number, public code: string, message: string) { super(message); }
}

const REFRESH_KEY = 'lapin.cde.refresh';
const CLIENT_ID = 'lapin-web';

let access: string | null = null;
let refreshing: Promise<boolean> | null = null;

function tabStorage(): Storage | null {
  try { return window.sessionStorage; } catch { return null; }
}

/** The tokens kept: the access token in memory, the refresh token in this tab's sessionStorage. */
function keep(pair: { access_token: string; refresh_token?: string | null }): void {
  access = pair.access_token;
  try {
    if (pair.refresh_token) tabStorage()?.setItem(REFRESH_KEY, pair.refresh_token);
    else tabStorage()?.removeItem(REFRESH_KEY);
  } catch { /* no storage: the sign-in lasts until a reload */ }
}

function forget(): void {
  access = null;
  try { tabStorage()?.removeItem(REFRESH_KEY); } catch { /* nothing kept */ }
}

/** A server's error body read into a CdeError (lapin-remote's and Atrium's {code, message}; OAuth2's {error, error_description}). */
async function failure(r: Response): Promise<CdeError> {
  let code = `http_${r.status}`;
  let message = `The server answered ${r.status}.`;
  try {
    const body = await r.json() as { code?: string; error?: string; message?: string; error_description?: string };
    code = body.code ?? body.error ?? code;
    message = body.message ?? body.error_description ?? message;
  } catch { /* not JSON */ }
  return new CdeError(r.status, code, message);
}

async function tokenRequest(form: Record<string, string>): Promise<{ access_token: string; refresh_token?: string | null }> {
  const r = await fetch('/foundation/oauth2/token', {
    method: 'POST',
    headers: { 'content-type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams(form).toString(),
    credentials: 'omit',
  });
  if (!r.ok) throw await failure(r);
  return r.json();
}

/** A refresh with the kept refresh token, one at a time (the token is single use): false when there is none or it is dead. */
function refresh(): Promise<boolean> {
  refreshing ??= (async () => {
    const token = tabStorage()?.getItem(REFRESH_KEY);
    if (!token) return false;
    try {
      keep(await tokenRequest({ grant_type: 'refresh_token', refresh_token: token, client_id: CLIENT_ID }));
      return true;
    } catch {
      forget();
      return false;
    }
  })().finally(() => { refreshing = null; });
  return refreshing;
}

/**
 * A request to the server with the access token, refreshed once on a 401 (or first, when only a refresh token is kept). A 401
 * that stays signs the user out of the interface.
 */
export async function api(path: string, init: RequestInit = {}): Promise<Response> {
  if (!access) await refresh();
  const send = () => fetch(path, { ...init, credentials: 'omit', headers: { ...(init.headers as Record<string, string> | undefined), ...(access ? { authorization: `Bearer ${access}` } : {}) } });
  const used = access;
  let r = await send();
  if (r.status === 401) {
    // Another request may have refreshed meanwhile: use its token, else refresh now.
    if ((access && access !== used) || await refresh()) r = await send();
    if (r.status === 401) { forget(); useCde.setState({ user: null }); }
  }
  return r;
}

async function json<T>(path: string, init?: RequestInit): Promise<T> {
  const r = await api(path, init);
  if (!r.ok) throw await failure(r);
  return r.json() as Promise<T>;
}

async function bytes(path: string): Promise<Uint8Array> {
  const r = await api(path);
  if (!r.ok) throw await failure(r);
  return new Uint8Array(await r.arrayBuffer());
}

// ------------------------------------------------------------ the server, the account

/**
 * Whether the interface is in front of a CDE server: the build says so (VITE_CDE_SERVER=same-origin, the build for
 * cde.lapinbim.com), else, for the browser build only, the origin answers lapin-remote's capabilities (a stock web build
 * served by a self-hosted server). Asked once; the answer is kept in useCde.
 */
let detecting: Promise<string | null> | null = null;
export function detectCde(): Promise<string | null> {
  detecting ??= (async () => {
    const flag = import.meta.env.VITE_CDE_SERVER as string | undefined;
    let found: string | null = null;
    if (flag === 'same-origin') found = window.location.origin;
    else if (engineInBrowser && flag !== 'off') {
      try {
        const ctl = new AbortController();
        const t = setTimeout(() => ctl.abort(), 4000);
        const r = await fetch('/lapin-remote/1.0/capabilities', { signal: ctl.signal, credentials: 'omit' });
        clearTimeout(t);
        const c = r.ok && (r.headers.get('content-type') ?? '').includes('json') ? await r.json() as { protocol?: string } : null;
        if (c?.protocol === 'lapin-remote') found = window.location.origin;
      } catch { /* not a CDE server */ }
    }
    useCde.setState({ server: found });
    if (found && tabStorage()?.getItem(REFRESH_KEY)) {
      try { useCde.setState({ user: await currentUser() }); } catch { /* signed out */ }
    }
    return found;
  })();
  return detecting;
}

/** The signed-in account: {id (the e-mail), name}. */
export const currentUser = () => json<{ id: string; name: string }>('/foundation/1.1/current-user');

/** A sign-in with the account's e-mail and password (OAuth2 password grant, client lapin-web); the password goes no further. */
export async function signIn(email: string, password: string): Promise<void> {
  keep(await tokenRequest({ grant_type: 'password', username: email.trim(), password, client_id: CLIENT_ID }));
  useCde.setState({ user: await currentUser() });
}

export function signOut(): void {
  forget();
  useCde.setState({ user: null });
}

/**
 * The account's password changed (Atrium Server's POST /atrium/1.0/account/password). The server ends every sign-in of the
 * account and hands back a fresh pair, kept at once. A refusal is thrown with its code (wrong_password, weak_password,
 * too_many_attempts) and the server's message.
 */
export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  const r = await api('/atrium/1.0/account/password', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ currentPassword, newPassword }) });
  if (!r.ok) throw await failure(r);
  keep(await r.json() as { access_token: string; refresh_token: string });
}

// ------------------------------------------------------------ projects

export type Role = 'viewer' | 'editor' | 'owner';
export interface ServerProject { id: string; name: string; role: Role }
export interface Ref { name: string; target: string; updatedAt: string; updatedBy: { id: string; name: string } }
export interface Commit { id: string; message: string; at: string; author: { id: string; name: string }; parents: string[]; entities: number | null }
export interface LogEntry { name: string; old: string | null; new: string | null; by: { id: string; name: string }; at: string; force: boolean }
export interface Member { email: string; name: string; authorId: string | null; role: Role }
export interface Viewer { commit: string; ifc: string; schema: string; size: number }

const P = (project: string) => `/lapin-remote/1.0/projects/${encodeURIComponent(project)}`;

/** The projects the account sees, every page of them. */
export async function listProjects(): Promise<ServerProject[]> {
  const all: ServerProject[] = [];
  let cursor: string | undefined;
  for (let page = 0; page < 20; page++) {
    const r = await json<{ projects: ServerProject[]; more: boolean; cursor?: string }>(`/lapin-remote/1.0/projects?limit=1000${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''}`);
    all.push(...r.projects);
    if (!r.more || !r.cursor) break;
    cursor = r.cursor;
  }
  return all;
}

export const getProject = (project: string) => json<ServerProject>(P(project));
export const listRefs = async (project: string) => (await json<{ refs: Ref[] }>(`${P(project)}/refs`)).refs;

/** A branch's commits from its head, newest first (the server's walk), at most `limit`. */
export async function listCommits(project: string, from: string, limit = 100): Promise<Commit[]> {
  const r = await json<{ commits: { id: string; commit: Partial<Commit> & { author?: { id: string; name: string } } }[] }>(`${P(project)}/commits?from=${from}&limit=${limit}`);
  return r.commits.map(({ id, commit }) => ({
    id, message: commit.message ?? '', at: commit.at ?? '', author: commit.author ?? { id: '', name: '' }, parents: commit.parents ?? [], entities: commit.entities ?? null,
  }));
}

/** An object's JSON (a tag, a commit). */
export async function getObjectJson<T>(project: string, id: string): Promise<T> {
  return JSON.parse(new TextDecoder().decode(await bytes(`${P(project)}/objects/${id}`))) as T;
}

/** The ref log, newest first, at most `limit` entries (the server pages it oldest first). */
export async function activity(project: string, limit = 30): Promise<LogEntry[]> {
  const all: LogEntry[] = [];
  let cursor: string | undefined;
  for (let page = 0; page < 10; page++) {
    const r = await json<{ entries: LogEntry[]; more: boolean; cursor?: string }>(`${P(project)}/refs/log?limit=1000${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''}`);
    all.push(...r.entries);
    if (!r.more || !r.cursor) break;
    cursor = r.cursor;
  }
  return all.slice(-limit).reverse();
}

/** The commit as a .lapin (the server assembles it: lapin-remote §12.1), without the viewer IFC. */
export const commitPackage = (project: string, commit: string) => bytes(`${P(project)}/commits/${commit}/package?viewer=false`);

/** The commit's viewer copy (an IFC export made at push), or null when it has none. */
export async function viewerOf(project: string, commit: string): Promise<Viewer | null> {
  const r = await api(`${P(project)}/commits/${commit}/viewer`);
  if (r.status === 404) return null;
  if (!r.ok) throw await failure(r);
  return r.json() as Promise<Viewer>;
}

export const objectBytes = (project: string, id: string) => bytes(`${P(project)}/objects/${id}`);

// ------------------------------------------------------------ members (Atrium's /atrium/1.0/projects/{id}/members)

const M = (project: string) => `/atrium/1.0/projects/${encodeURIComponent(project)}/members`;

export const listMembers = (project: string) => json<{ role: Role; members: Member[] }>(M(project));

export async function setMember(project: string, email: string, role: Role): Promise<Member> {
  return json<Member>(`${M(project)}/${encodeURIComponent(email.trim())}`, { method: 'PUT', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ role }) });
}

export async function removeMember(project: string, email: string): Promise<void> {
  const r = await api(`${M(project)}/${encodeURIComponent(email)}`, { method: 'DELETE' });
  if (!r.ok) throw await failure(r);
}

// ------------------------------------------------------------ refs read

export interface RefsRead {
  main: Ref | null;
  wips: (Ref & { who: string; label: string })[];
  versions: (Ref & { number: number })[];
  issues: (Ref & { number: number; revision: string })[];
}

/** The refs sorted by kind (lapin-remote §5.1): main, the work-in-progress branches newest first, versions and issues by number. */
export function readRefs(refs: Ref[]): RefsRead {
  const out: RefsRead = { main: null, wips: [], versions: [], issues: [] };
  for (const r of refs) {
    if (r.name === 'refs/heads/main') { out.main = r; continue; }
    const wip = /^refs\/heads\/wip\/([^/]+)\/([^/]+)$/.exec(r.name);
    if (wip) { out.wips.push({ ...r, who: wip[1], label: wip[2] }); continue; }
    const v = /^refs\/tags\/v\/(\d+)$/.exec(r.name);
    if (v) { out.versions.push({ ...r, number: Number(v[1]) }); continue; }
    const issue = /^refs\/tags\/issue\/(\d+)-([PC]\d{2,3})$/.exec(r.name);
    if (issue) out.issues.push({ ...r, number: Number(issue[1]), revision: issue[2] });
  }
  out.wips.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
  out.versions.sort((a, b) => a.number - b.number);
  out.issues.sort((a, b) => a.number - b.number || a.revision.localeCompare(b.revision));
  return out;
}

/** A ref's short name for the activity list: main, a branch's owner/name, v3, issue 2 P01. */
export function refLabel(name: string): string {
  if (name === 'refs/heads/main') return 'main';
  const wip = /^refs\/heads\/wip\/(.+)$/.exec(name);
  if (wip) return wip[1];
  const v = /^refs\/tags\/v\/(\d+)$/.exec(name);
  if (v) return `v${v[1]}`;
  const issue = /^refs\/tags\/issue\/(\d+)-([PC]\d{2,3})$/.exec(name);
  if (issue) return `#${issue[1]} ${issue[2]}`;
  return name;
}

/** A tag object (lapin-remote §4.4), as far as the history list shows it. */
export interface TagObject { commit: string; kind: 'version' | 'issue' | string; number: number; name?: string; revision?: string; status?: string }

/** The chips each commit carries: its versions (v3 Name) and issues (#1 P01 S2), from the tag objects. */
export function chipsByCommit(tags: TagObject[]): Map<string, string[]> {
  const out = new Map<string, string[]>();
  for (const t of tags) {
    const chip = t.kind === 'version' ? `v${t.number}${t.name ? ` ${t.name}` : ''}` : t.kind === 'issue' ? `#${t.number} ${t.revision ?? ''}${t.status ? ` ${t.status}` : ''}`.trim() : null;
    if (!chip || !t.commit) continue;
    out.set(t.commit, [...(out.get(t.commit) ?? []), chip]);
  }
  return out;
}

/** The test hook: the module's memory as it is at a page load. */
export function resetCdeForTests(): void {
  access = null; refreshing = null; detecting = null;
  useCde.setState({ server: '', user: null, offline: false, opened: null });
}
