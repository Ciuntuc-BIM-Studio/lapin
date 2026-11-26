/**
 * Atrium Server's own API (its docs/atrium-server.md; Atrium docs/plan-cde-web.md W2 and W3): the account, the server's
 * administration (accounts, new projects), a project's rules and its activity. In the browser in front of the server (cdeServer.ts) a
 * request is the page's own fetch, on the same origin; on the desktop the interface cannot reach a server itself, so the engine makes
 * it on the project's sign-in (remote.atrium, only for these paths). Either way a refusal comes back as an AtriumError with the
 * server's stable code, which the interface says in the reader's language (remote.ts REFUSALS).
 */

import { api, isCdeServer, type Role } from './cdeServer';
import { remoteCall } from './remote';
import { refusalText } from './refusals';
import type { CheckSettings, ChecksReport, GateChecks } from './checks';

/** A refusal: its status and stable code, the message said here, and the server's own words and body (a 422's gate, a 400's member). */
export class AtriumError extends Error {
  constructor(public status: number, public code: string, message: string, public serverMessage: string | null = null, public body: Record<string, unknown> | null = null) { super(message); }
}

export interface Answer<T> { status: number; body: T | null; etag: string | null }

async function request<T>(server: string, method: string, path: string, body?: unknown, ifMatch?: string | null): Promise<Answer<T>> {
  if (isCdeServer(server)) {
    const r = await api(`/${path}`, {
      method,
      headers: { ...(body === undefined ? {} : { 'content-type': 'application/json' }), ...(ifMatch ? { 'if-match': ifMatch } : {}) },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    let json: T | null;
    try { json = await r.json() as T; } catch { json = null; }
    return { status: r.status, body: json, etag: r.headers.get('etag') };
  }
  return remoteCall<Answer<T>>('remote.atrium', { url: server, method, path, ...(body === undefined ? {} : { body }), ...(ifMatch ? { ifMatch } : {}) }, server);
}

/**
 * A request with its whole answer (status, JSON, ETag), `ifMatch` sent as If-Match: for a record changed with its version (the project
 * information, A1b), whose 412 the caller reads again. A refusal other than 412 is thrown as by need().
 */
export async function send<T>(server: string, method: string, path: string, body?: unknown, ifMatch?: string | null): Promise<Answer<T>> {
  const a = await request<T>(server, method, path, body, ifMatch);
  if ((a.status >= 200 && a.status < 300) || a.status === 412) return a;
  const b = (a.body ?? {}) as { code?: string; message?: string };
  const code = b.code ?? (a.status === 404 || a.status === 405 ? 'not_offered' : a.status === 401 ? 'unauthorized' : `http_${a.status}`);
  throw new AtriumError(a.status, code, refusalText(code, b.message ?? `The server answered ${a.status}.`), b.message ?? null, (a.body ?? null) as Record<string, unknown> | null);
}

/** A request whose answer must be a success: its JSON, or the refusal thrown with its code and the message said here. */
export async function need<T>(server: string, method: string, path: string, body?: unknown): Promise<T> {
  const a = await request<T>(server, method, path, body);
  if (a.status >= 200 && a.status < 300) return (a.body ?? {}) as T;
  const b = (a.body ?? {}) as { code?: string; message?: string };
  const code = b.code ?? (a.status === 404 || a.status === 405 ? 'not_offered' : a.status === 401 ? 'unauthorized' : `http_${a.status}`);
  throw new AtriumError(a.status, code, refusalText(code, b.message ?? `The server answered ${a.status}.`), b.message ?? null, (a.body ?? null) as Record<string, unknown> | null);
}

const enc = encodeURIComponent;

// ------------------------------------------------------------ what the server offers

const offered = new Map<string, Promise<string[]>>();

/**
 * The features of Atrium's own API (`GET /atrium/1.0/`, no sign-in: `{api, version, features}`), asked once per server; an older
 * server answers 404 there, which is no feature. W4's screens show when it offers `submissions`.
 */
export function features(server: string): Promise<string[]> {
  let f = offered.get(server);
  if (!f) {
    f = (async () => {
      try {
        if (isCdeServer(server)) {
          const r = await fetch('/atrium/1.0/', { credentials: 'omit' });
          return r.ok ? ((await r.json()) as { features?: string[] }).features ?? [] : [];
        }
        const a = await request<{ features?: string[] }>(server, 'GET', 'atrium/1.0/');
        return a.status === 200 ? a.body?.features ?? [] : [];
      } catch { return []; }
    })();
    offered.set(server, f);
  }
  return f;
}

/** Forgets what servers offer (tests). */
export const forgetFeatures = () => offered.clear();

// ------------------------------------------------------------ the account

export interface Account { id: string; publicId: string; authorId: string; name: string; email: string; isAdmin: boolean; projects: { id: string; name: string; role: Role }[] }
export const account = (server: string) => need<Account>(server, 'GET', 'atrium/1.0/account');

// ------------------------------------------------------------ personal access tokens, for AI clients over MCP (plan-cde-references R6)

/** A personal access token as the server lists it (never its secret). */
export interface AccessToken { id: string; name: string; scope: 'read' | 'read+write'; created: string; expires: string; lastUsed: string | null; expired: boolean }

/** The account's tokens, newest first (feature `account.tokens`). */
export const accessTokens = async (server: string) => (await need<{ tokens: AccessToken[] }>(server, 'GET', 'atrium/1.0/account/tokens')).tokens;

/** A new token: its listing and `token`, the secret, which the server shows this once. */
export const createAccessToken = (server: string, t: { name: string; scope: AccessToken['scope']; expiresInDays: number }) =>
  need<AccessToken & { token: string }>(server, 'POST', 'atrium/1.0/account/tokens', { name: t.name.trim(), scope: t.scope, expiresInDays: t.expiresInDays });

/** Revokes a token at once. */
export const revokeAccessToken = (server: string, id: string) => need<unknown>(server, 'DELETE', `atrium/1.0/account/tokens/${enc(id)}`);

// ------------------------------------------------------------ apps connected over OAuth (plan-cde-references R6b)

/**
 * An app the account connected by signing in with the browser (an OAuth client of the server's MCP endpoint: claude.ai's connectors,
 * other MCP clients that sign in by themselves). `verified`: its name is published at its own address (a client ID metadata document);
 * otherwise the app registered itself and chose its name.
 */
export interface ConnectedApp {
  id: string; name: string; clientId: string; kind: 'registered' | 'metadata'; verified: boolean; redirectHost: string;
  scopes: string[]; scope: 'read' | 'read+write'; created: string; firstUsed: string | null; lastUsed: string | null;
}

/** The account's connected apps, the most recently used first (feature `account.apps`). */
export const connectedApps = async (server: string) => (await need<{ apps: ConnectedApp[] }>(server, 'GET', 'atrium/1.0/account/apps')).apps;

/** Revokes an app: its consent and every token it holds, at once. */
export const revokeConnectedApp = (server: string, id: string) => need<unknown>(server, 'DELETE', `atrium/1.0/account/apps/${enc(id)}`);

// ------------------------------------------------------------ administration (server administrators)

export interface AdminUser { email: string; name: string; publicId: string; authorId: string; isAdmin: boolean; disabled: boolean; created: string | null; projects: { id: string; name: string; role: Role }[] }

export const adminUsers = async (server: string) => (await need<{ users: AdminUser[] }>(server, 'GET', 'atrium/1.0/admin/users')).users;

/** A new account; without a password the server makes one and returns it this once. */
export const createUser = (server: string, user: { email: string; name?: string; isAdmin?: boolean }) =>
  need<{ user: AdminUser; password?: string }>(server, 'POST', 'atrium/1.0/admin/users', { email: user.email.trim(), ...(user.name?.trim() ? { name: user.name.trim() } : {}), isAdmin: Boolean(user.isAdmin) });

/** An account's name, administration or being disabled (which ends its sign-ins). */
export const changeUser = async (server: string, email: string, change: { name?: string; isAdmin?: boolean; disabled?: boolean }) =>
  (await need<{ user: AdminUser }>(server, 'PATCH', `atrium/1.0/admin/users/${enc(email)}`, change)).user;

/** A new password made by the server, shown once; the account's sign-ins end. */
export const resetPassword = async (server: string, email: string) => (await need<{ password: string }>(server, 'POST', `atrium/1.0/admin/users/${enc(email)}/password`, {})).password;

/** A new project (administrators): the caller becomes its owner and adds the members. */
export const createProject = (server: string, name: string) => need<{ id: string; name: string; role?: Role }>(server, 'POST', 'lapin-remote/1.0/projects', { name: name.trim() });

// ------------------------------------------------------------ a project's members (read by any member)

export const members = (server: string, project: string) =>
  need<{ role: Role; members: { email: string; name: string; role: Role }[] }>(server, 'GET', `atrium/1.0/projects/${enc(project)}/members`);

// ------------------------------------------------------------ a project's rules

export interface Rules {
  main: 'editor' | 'owner'; issueTags: 'editor' | 'owner'; comments: 'viewer' | 'editor'; close: 'author' | 'editor'; resolveNeedsComment: boolean; approve?: 'editor' | 'owner'; role: Role;
  /** W5: the naming rule (a regex every file's code must match whole), and the checks' settings (every member, defaults filled). */
  naming?: string | null; checks?: CheckSettings;
  /** W6c: the project's IDS files run on every submission (`enabled`), a failing specification an error (`blocking`). */
  ids?: { enabled: boolean; blocking: boolean };
}
export const getRules = (server: string, project: string) => need<Rules>(server, 'GET', `atrium/1.0/projects/${enc(project)}/rules`);
/** Changes the rules given; `checks` replaces the settings whole (members left out take their defaults). */
export const putRules = (server: string, project: string, change: Partial<Omit<Rules, 'role' | 'checks'>> & { checks?: CheckSettings | null }) => need<Rules>(server, 'PUT', `atrium/1.0/projects/${enc(project)}/rules`, change);

/** The change `PUT rules` takes for the IDS switches: the whole `ids` object (members left out would take their defaults). */
export const idsRule = (rules: Pick<Rules, 'ids'>, change: Partial<NonNullable<Rules['ids']>>) =>
  ({ ids: { enabled: rules.ids?.enabled ?? true, blocking: rules.ids?.blocking ?? true, ...change } });

// ------------------------------------------------------------ activity

export interface RefItem { kind: 'ref'; at: string; by: { id: string; name: string }; name: string; old: string | null; new: string | null; force: boolean }
export interface IssueChange { type: string; commentGuid: string | null; viewpointGuid: string | null; old: string | null; new: string | null }
export interface IssueItem { kind: 'issue'; at: string; by: { id: string; name: string; email: string }; topicGuid: string; title: string | null; changes: IssueChange[] }
export interface SubmissionItem { kind: 'submission'; at: string; by: { id: string; name: string; email: string }; submissionId: string; number: number; issue: number | null; revision: string; purpose: string; changes: { type: string; old: string | null; new: string | null }[] }
export type ActivityItem = RefItem | IssueItem | SubmissionItem;

/** The ref log and the issue changes together, newest first, a page at a time. */
export const activity = (server: string, project: string, cursor?: string, limit = 50) =>
  need<{ items: ActivityItem[]; more: boolean; cursor?: string }>(server, 'GET', `atrium/1.0/projects/${enc(project)}/activity?limit=${limit}${cursor ? `&cursor=${enc(cursor)}` : ''}`);

/** What an issue item did, in a few words each (the interface translates them), the same kind said once. */
export function issueWords(item: IssueItem, nameOf: (email: string) => string = (x) => x): string[] {
  const words: string[] = [];
  const add = (w: string) => { if (!words.includes(w)) words.push(w); };
  const value = (v: string | null) => (v ?? '').replace(/^"|"$/g, '');
  for (const c of item.changes) {
    switch (c.type) {
      case 'topic_created': add('created the issue'); break;
      case 'topic_deleted': add('deleted the issue'); break;
      case 'status_updated': add(`status: ${value(c.new)}`); break;
      case 'title_updated': add('renamed it'); break;
      case 'assigned_to_updated': add(`assigned to ${nameOf(value(c.new))}`); break;
      case 'assigned_to_removed': add('unassigned it'); break;
      case 'priority_updated': add(`priority: ${value(c.new)}`); break;
      case 'comment_created': add('commented'); break;
      case 'comment_text_updated': add('edited a comment'); break;
      case 'comment_deleted': add('deleted a comment'); break;
      case 'viewpoint_added': case 'viewpoint_updated': add('added a viewpoint'); break;
      case 'viewpoint_deleted': case 'viewpoint_removed': add('removed a viewpoint'); break;
      default: add('edited it');
    }
  }
  return words;
}

// ------------------------------------------------------------ publishing (W4: the publish queue, containers, the handover diff, the outbox)

export interface Person { id: string; name: string; email?: string }
export interface ServerFile { name: string; sha256: string; size: number }
export interface GateTopic { guid: string; serverAssignedId: string | null; title: string; status: string | null; priority: string | null; document: string | null }
export type SubmissionStatus = 'pending' | 'approved' | 'rejected' | 'withdrawn';
export interface Submission {
  id: string; number: number; status: SubmissionStatus; commit: string; issue: number | null; issueId: string | null; revision: string;
  purpose: 'shared' | 'published'; suitability: string; note: string; documents: string[]; files: ServerFile[];
  author: Person; created: string; decidedBy: Person | null; decided: string | null; decisionNote: string | null; reason: string | null; override: boolean;
  tag: string | null; tagId: string | null; closedTopics: string[]; gate: { passed: boolean; topics: GateTopic[]; checks?: GateChecks | null } | null;
  /** W5: the checks on its PDF and DXF files (the list carries them without `files`). */
  checks?: ChecksReport | null;
  /** R7: files submitted from disk (Publishing → Submit files…), `commit` being main's at the time. */
  filesOnly?: boolean;
}

const S = (project: string) => `atrium/1.0/projects/${enc(project)}/submissions`;
export const listSubmissions = async (server: string, project: string, status?: SubmissionStatus) =>
  (await need<{ submissions: Submission[] }>(server, 'GET', `${S(project)}${status ? `?status=${status}` : ''}`)).submissions;
export const getSubmission = (server: string, project: string, id: string) => need<Submission>(server, 'GET', `${S(project)}/${enc(id)}`);
export const approveSubmission = (server: string, project: string, id: string, note: string, override = false) =>
  need<Submission>(server, 'POST', `${S(project)}/${enc(id)}/approve`, { note, ...(override ? { override: true } : {}) });
export const rejectSubmission = (server: string, project: string, id: string, reason: string) => need<Submission>(server, 'POST', `${S(project)}/${enc(id)}/reject`, { reason });
export const withdrawSubmission = (server: string, project: string, id: string) => need<Submission>(server, 'POST', `${S(project)}/${enc(id)}/withdraw`, {});
/**
 * R7: another tool's files submitted from disk (a server offering `submissions.files`): no commit, so the server takes main's and the
 * tag names it. The files are put as objects when the server says they are missing (content-addressed: a file sent twice is stored once).
 */
export async function submitFiles(server: string, project: string, body: Record<string, unknown>, blobs: Map<string, Uint8Array>): Promise<Submission> {
  const post = () => need<Submission>(server, 'POST', S(project), { ...body, commit: null });
  try { return await post(); } catch (e) {
    if (!(e instanceof AtriumError) || e.code !== 'object_missing') throw e;
    const missing = (e.body?.missing as string[] | undefined) ?? [...blobs.keys()];
    for (const sha of missing) {
      const bytes = blobs.get(sha);
      if (bytes) await putObject(server, project, sha, bytes);
    }
    return post();
  }
}

/** An object of the project's store put as its bytes (its id their SHA-256): the page's fetch in the browser, the engine on the desktop. */
export async function putObject(server: string, project: string, sha256: string, bytes: Uint8Array): Promise<void> {
  const path = `lapin-remote/1.0/projects/${enc(project)}/objects/${sha256}`;
  let status: number;
  if (isCdeServer(server)) status = (await api(`/${path}`, { method: 'PUT', headers: { 'content-type': 'application/octet-stream' }, body: bytes as BodyInit })).status;
  else {
    let bin = '';
    for (let i = 0; i < bytes.length; i += 0x8000) bin += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
    status = (await remoteCall<{ status: number }>('remote.atrium', { url: server, method: 'PUT', path, data: btoa(bin) }, server)).status;
  }
  if (status < 200 || status >= 300) throw new AtriumError(status, `http_${status}`, refusalText(`http_${status}`, `The server did not take the file (${status}).`));
}

/** W5: the checks run again (202: the submission, its checks back to pending). */
export const rerunChecks = (server: string, project: string, id: string) => need<Submission>(server, 'POST', `${S(project)}/${enc(id)}/checks`);

// ------------------------------------------------------------ the document register (W5)

export interface RegisterEntry { code: string; title: string | null; revision: string | null; fields: Record<string, string> }
export interface Register {
  name: string | null; imported: string; importedBy: Person | null;
  columns: { code: string; title: string | null; revision: string | null; fields: string[] };
  count: number; entries: RegisterEntry[]; warnings: string[];
}
const R = (project: string) => `atrium/1.0/projects/${enc(project)}/register`;
/** The project's register, or null when it has none. */
export async function getRegister(server: string, project: string): Promise<Register | null> {
  try { return await need<Register>(server, 'GET', R(project)); } catch (e) { if (e instanceof AtriumError && e.code === 'no_register') return null; throw e; }
}
export const registerType = (name: string) => (/\.xlsx$/i.test(name) ? 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' : 'text/csv');
/** A register imported from a CSV or XLSX file (owners): it replaces the one there. The file goes as it is, not as JSON. */
export const putRegister = (server: string, project: string, bytes: Uint8Array, name: string) => putFile<Register>(server, `${R(project)}?name=${enc(name)}`, bytes, registerType(name));

/**
 * A file put as its own bytes (the register, W5; an IDS file, W6c): the page's fetch in front of the server, else the engine
 * (remote.atrium sends bytes to those two paths only). A refusal is thrown with its body (a 400's problems).
 */
export async function putFile<T>(server: string, path: string, bytes: Uint8Array, type: string): Promise<T> {
  let a: Answer<T>;
  if (isCdeServer(server)) {
    const r = await api(`/${path}`, { method: 'PUT', headers: { 'content-type': type }, body: bytes as BodyInit });
    let body: T | null;
    try { body = await r.json() as T; } catch { body = null; }
    a = { status: r.status, body, etag: null };
  } else {
    let bin = '';
    for (let i = 0; i < bytes.length; i += 0x8000) bin += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
    a = await remoteCall<Answer<T>>('remote.atrium', { url: server, method: 'PUT', path, data: btoa(bin), contentType: type }, server);
  }
  if (a.status >= 200 && a.status < 300 && a.body) return a.body;
  const b = (a.body ?? {}) as { code?: string; message?: string };
  const code = b.code ?? `http_${a.status}`;
  throw new AtriumError(a.status, code, refusalText(code, b.message ?? `The server answered ${a.status}.`), b.message ?? null, b as Record<string, unknown>);
}
export const deleteRegister = (server: string, project: string) => need<unknown>(server, 'DELETE', R(project));

export interface IssueTagItem {
  kind: 'issue'; name: string; issue: number; revision: string; suitability: string; container: string; tag: string; commit: string; at: string;
  author: Person; issuedAt: string; issuedBy: Person; note: string | null; files: ServerFile[]; submission: string | null; supersededBy: string | null;
  /** Files submitted from disk (R7): the commit is main's at the time, not a model of this issue's own. Left out by an older server. */
  filesOnly?: boolean;
}
export interface WipItem { kind: 'branch'; name: string; commit: string; updatedAt: string; updatedBy: Person }
export interface Containers { wip: (WipItem | IssueTagItem)[]; shared: IssueTagItem[]; published: IssueTagItem[]; archived: IssueTagItem[] }
export const containers = (server: string, project: string) => need<Containers>(server, 'GET', `atrium/1.0/projects/${enc(project)}/containers`);

export interface DiffSide { name: string; commit: string; revision: string; at: string }
export interface DiffDocument { document: string; name: string; sha256: string; size: number }
export interface HandoverDiff {
  from: DiffSide; to: DiffSide;
  documents: { added: DiffDocument[]; removed: DiffDocument[]; changed: { document: string; from: ServerFile; to: ServerFile }[]; unchanged: string[] };
  parts: { added: string[]; removed: string[]; changed: string[] };
}
export const handoverDiff = (server: string, project: string, from: string, to: string) =>
  need<HandoverDiff>(server, 'GET', `atrium/1.0/projects/${enc(project)}/handover-diff?from=${enc(from)}&to=${enc(to)}`);

export interface OutboxItem { id: string | number; kind: string; projectId: string | null; to: string; subject: string; body: string; status: 'pending' | 'sent' | 'failed'; attempts: number; created: string; nextAttempt: string | null; lastError: string | null; sent: string | null }
export const outbox = (server: string, status?: OutboxItem['status'], limit = 100) =>
  need<{ smtp: boolean; items: OutboxItem[] }>(server, 'GET', `atrium/1.0/admin/outbox?limit=${limit}${status ? `&status=${status}` : ''}`);

/**
 * A commit's package or an object (an issued file, an IFC copy) of a server project, as bytes: the page's fetch in the browser, the
 * engine on the desktop (remote.atrium reads these paths, and nothing else of lapin-remote).
 */
export async function serverBytes(server: string, path: string): Promise<Uint8Array> {
  if (isCdeServer(server)) {
    const r = await api(`/${path}`);
    if (!r.ok) {
      let b: { code?: string; message?: string } = {};
      try { b = await r.json() as typeof b; } catch { b = {}; }
      throw new AtriumError(r.status, b.code ?? `http_${r.status}`, refusalText(b.code ?? '', b.message ?? `The server answered ${r.status}.`));
    }
    return new Uint8Array(await r.arrayBuffer());
  }
  const a = await remoteCall<{ status: number; body: { code?: string; message?: string } | null; data?: string }>('remote.atrium', { url: server, method: 'GET', path, bytes: true }, server);
  if (a.status < 200 || a.status >= 300 || !a.data) throw new AtriumError(a.status, a.body?.code ?? `http_${a.status}`, refusalText(a.body?.code ?? '', a.body?.message ?? `The server answered ${a.status}.`));
  const bin = atob(a.data);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

/** The viewer record of a commit (its IFC copy's object), or null when it has none. */
export async function commitViewer(server: string, project: string, commit: string): Promise<{ ifc: string; schema: string; size: number } | null> {
  const a = await request<{ ifc: string; schema: string; size: number }>(server, 'GET', `lapin-remote/1.0/projects/${enc(project)}/commits/${commit}/viewer`);
  return a.status === 200 && a.body ? a.body : null;
}
