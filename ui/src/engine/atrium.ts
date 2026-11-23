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

export class AtriumError extends Error {
  constructor(public status: number, public code: string, message: string) { super(message); }
}

interface Answer<T> { status: number; body: T | null; etag: string | null }

async function request<T>(server: string, method: string, path: string, body?: unknown): Promise<Answer<T>> {
  if (isCdeServer(server)) {
    const r = await api(`/${path}`, {
      method,
      headers: body === undefined ? {} : { 'content-type': 'application/json' },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    let json: T | null = null;
    try { json = await r.json() as T; } catch { json = null; }
    return { status: r.status, body: json, etag: r.headers.get('etag') };
  }
  return remoteCall<Answer<T>>('remote.atrium', { url: server, method, path, ...(body === undefined ? {} : { body }) }, server);
}

/** A request whose answer must be a success: its JSON, or the refusal thrown with its code and the message said here. */
async function need<T>(server: string, method: string, path: string, body?: unknown): Promise<T> {
  const a = await request<T>(server, method, path, body);
  if (a.status >= 200 && a.status < 300) return (a.body ?? {}) as T;
  const b = (a.body ?? {}) as { code?: string; message?: string };
  const code = b.code ?? (a.status === 404 || a.status === 405 ? 'not_offered' : a.status === 401 ? 'unauthorized' : `http_${a.status}`);
  throw new AtriumError(a.status, code, refusalText(code, b.message ?? `The server answered ${a.status}.`));
}

const enc = encodeURIComponent;

// ------------------------------------------------------------ the account

export interface Account { id: string; publicId: string; authorId: string; name: string; email: string; isAdmin: boolean; projects: { id: string; name: string; role: Role }[] }
export const account = (server: string) => need<Account>(server, 'GET', 'atrium/1.0/account');

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

// ------------------------------------------------------------ a project's rules

export interface Rules { main: 'editor' | 'owner'; issueTags: 'editor' | 'owner'; comments: 'viewer' | 'editor'; close: 'author' | 'editor'; resolveNeedsComment: boolean; role: Role }
export const getRules = (server: string, project: string) => need<Rules>(server, 'GET', `atrium/1.0/projects/${enc(project)}/rules`);
export const putRules = (server: string, project: string, change: Partial<Omit<Rules, 'role'>>) => need<Rules>(server, 'PUT', `atrium/1.0/projects/${enc(project)}/rules`, change);

// ------------------------------------------------------------ activity

export interface RefItem { kind: 'ref'; at: string; by: { id: string; name: string }; name: string; old: string | null; new: string | null; force: boolean }
export interface IssueChange { type: string; commentGuid: string | null; viewpointGuid: string | null; old: string | null; new: string | null }
export interface IssueItem { kind: 'issue'; at: string; by: { id: string; name: string; email: string }; topicGuid: string; title: string | null; changes: IssueChange[] }
export type ActivityItem = RefItem | IssueItem;

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
