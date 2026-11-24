/**
 * The remarks of a project whose remote is a CDE server are its issues (Atrium docs/plan-cde-web.md W3): what the engine knows of the
 * server from the last sync (remote.issueInfo: the account, its role, the project's rules, the members' names, the BCF choices) and
 * what the interface lets each user do with each remark, after the server's rules (Atrium docs/atrium-server.md, "The rules"), so that
 * what the server would refuse is greyed out before it is tried.
 */

import { remoteCall } from '../../../engine/remote';
import type { Remark } from './remarks';

export type Role = 'viewer' | 'editor' | 'owner';

export interface IssueInfo {
  linked: boolean; server?: string; project?: string;
  account?: string; name?: string; role?: Role; isAdmin?: boolean;
  rules?: { comments: 'viewer' | 'editor'; close: 'author' | 'editor'; resolveNeedsComment: boolean; main?: string; issueTags?: string };
  people?: Record<string, string>;
  extensions?: { types?: string[]; statuses?: string[]; priorities?: string[]; labels?: string[]; stages?: string[]; users?: string[] };
  syncedAt?: string; last?: number; known?: string[]; pending?: number;
}

export const issueInfo = () => remoteCall<IssueInfo>('remote.issueInfo');

/** What the user may do: make issues, and with one remark change it (all of it, or its status only), comment, close it, delete it. */
export interface Rights { create: boolean; edit: boolean; statusOnly: boolean; comment: boolean; close: boolean; del: boolean }

const ALL: Rights = { create: true, edit: true, statusOnly: false, comment: true, close: true, del: true };

const same = (a: string | undefined | null, b: string | undefined | null) => Boolean(a && b && a.trim().toLowerCase() === b.trim().toLowerCase());

/**
 * The server's rules for this user and this remark. Without a server (or before the first sync) everything is allowed: the remarks are
 * the project's own. A remark not on the server yet is the user's own until it is sent.
 */
export function rightsOf(info: IssueInfo | null, remark: Pick<Remark, 'guid' | 'creationAuthor' | 'assignedTo'> | null): Rights {
  if (!info?.linked || !info.role) return ALL;
  const role = info.role;
  const create = role !== 'viewer';
  const comments = role !== 'viewer' || (info.rules?.comments ?? 'viewer') === 'viewer';
  if (!remark) return { ...ALL, create, comment: comments, del: role === 'owner' };
  if (!(info.known ?? []).includes(remark.guid)) return { ...ALL, create, edit: create, close: create, del: true, comment: comments };
  const me = info.account;
  const author = same(remark.creationAuthor, me);
  const assigned = same(remark.assignedTo, me);
  const closeRule = info.rules?.close ?? 'author';
  if (role === 'owner') return { create, edit: true, statusOnly: false, comment: true, close: true, del: true };
  if (role === 'editor') {
    const edit = author || assigned;
    return { create, edit, statusOnly: false, comment: true, close: edit && (author || closeRule === 'editor'), del: false };
  }
  // A viewer: reads, comments when the project lets viewers comment, and changes the status of a topic assigned to them.
  return { create: false, edit: assigned, statusOnly: assigned, comment: comments, close: assigned && closeRule === 'editor', del: false };
}

/** A person as the interface names them: the member's name for an e-mail address the server uses, else as written. */
export function personName(info: IssueInfo | null, who: string | undefined | null): string {
  if (!who) return '';
  return info?.people?.[who.toLowerCase()] ?? who;
}

/** The statuses offered: the server's (else Lapin's), without "Closed" for someone who may not close (keeping the remark's own). */
export function statusesFor(info: IssueInfo | null, own: string[], rights: Rights, current?: string): string[] {
  const all = info?.linked && info.extensions?.statuses?.length ? info.extensions.statuses : own;
  return all.filter((s) => rights.close || s.toLowerCase() !== 'closed' || same(s, current));
}

/** The choices of a field: the server project's when it has them, else Lapin's. */
export const choicesFor = (info: IssueInfo | null, server: string[] | undefined, own: string[]) => (info?.linked && server?.length ? server : own);
