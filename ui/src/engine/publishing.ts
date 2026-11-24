/**
 * Publishing on a CDE server (Atrium docs/plan-cde-web.md, stage W4; the server's contract in its docs/atrium-server.md § Publishing):
 * who may act on a submission, the containers' rows, an issue tag's label, and the steps an issue takes from Lapin's Issue dialog to
 * the server (saved there first, then submitted for approval or tagged directly). The screens are modules/core/CdePublish.tsx.
 */

import type { Containers, IssueTagItem, Rules, Submission, WipItem } from './atrium';
import type { Role } from './cdeServer';

const rank: Record<Role, number> = { viewer: 0, editor: 1, owner: 2 };
export const atLeast = (role: Role | null | undefined, need: 'viewer' | 'editor' | 'owner') => role != null && rank[role] >= rank[need];

export interface Me { email: string; role: Role; isAdmin?: boolean }

/** What the account may do with a submission, as the server's rules say (403s greyed rather than met), each with why not. */
export interface SubmissionRights { approve: boolean; reject: boolean; withdraw: boolean; override: boolean; why: string | null }

export function submissionRights(s: Submission, me: Me | null, rules: Pick<Rules, 'approve'> | null, owners = 2): SubmissionRights {
  const none = (why: string): SubmissionRights => ({ approve: false, reject: false, withdraw: false, override: false, why });
  if (!me) return none('Sign in to the server first.');
  if (s.status !== 'pending') return none('Only a pending submission can be approved, rejected or withdrawn.');
  const role: Role = me.isAdmin ? 'owner' : me.role;
  const mine = Boolean(s.author.email && s.author.email.toLowerCase() === me.email.toLowerCase());
  const approver = atLeast(role, rules?.approve ?? 'owner');
  // The author approves their own only when they are the project's only owner (the server's own_submission).
  const ownBlocked = mine && !(role === 'owner' && owners <= 1);
  const decide = approver && !ownBlocked;
  const why = decide ? null
    : !approver ? (rules?.approve === 'editor' ? 'Editors and owners approve in this project.' : 'Only the project\'s owners approve in this project.')
    : 'You cannot approve or reject your own submission.';
  return { approve: decide, reject: decide, withdraw: mine, override: decide && role === 'owner', why };
}

/** "#4 P02": an issue tag's (or a submission's) number and revision; "next" while the server is to number it. */
export const issueLabel = (issue: number | null, revision: string) => `${issue == null ? '#?' : `#${issue}`} ${revision}`;

/** The short name of an issue tag (refs/tags/issue/4-P02 → 4-P02), as the handover diff takes it. */
export const tagShort = (name: string) => name.replace(/^refs\/tags\/issue\//, '');

export type ContainerId = 'wip' | 'shared' | 'published' | 'archived';
export const CONTAINERS: { id: ContainerId; label: string }[] = [
  { id: 'wip', label: 'WIP' }, { id: 'shared', label: 'Shared' }, { id: 'published', label: 'Published' }, { id: 'archived', label: 'Archived' },
];

/** One row of the containers view: what it is, its revision, when and by whom, the commit it opens. */
export interface ContainerRow { key: string; title: string; revision: string | null; suitability: string | null; at: string; by: string; commit: string; tag: IssueTagItem | null; note: string | null; files: number; supersededBy: string | null; fromSubmission: boolean }

export function containerRows(c: Containers | null, id: ContainerId): ContainerRow[] {
  if (!c) return [];
  return (c[id] ?? []).map((x: WipItem | IssueTagItem): ContainerRow => x.kind === 'branch'
    ? { key: x.name, title: x.name.replace(/^refs\/heads\/wip\//, ''), revision: null, suitability: null, at: x.updatedAt, by: x.updatedBy?.name ?? '', commit: x.commit, tag: null, note: null, files: 0, supersededBy: null, fromSubmission: false }
    : { key: x.name, title: issueLabel(x.issue, x.revision), revision: x.revision, suitability: x.suitability, at: x.issuedAt || x.at, by: x.issuedBy?.name || x.author?.name || '', commit: x.commit, tag: x, note: x.note, files: x.files?.length ?? 0, supersededBy: x.supersededBy, fromSubmission: Boolean(x.submission) });
}

/** The issue tags a handover diff can compare: the Shared, Published and Archived revisions, oldest first. */
export function comparable(c: Containers | null): IssueTagItem[] {
  if (!c) return [];
  const all = [...c.shared, ...c.published, ...c.archived];
  return all.sort((a, b) => (a.issuedAt || a.at).localeCompare(b.issuedAt || b.at) || a.name.localeCompare(b.name));
}

/** The two revisions proposed for a diff: the last two issued (older first), or nothing. */
export function proposedPair(list: IssueTagItem[]): [string, string] | null {
  if (list.length < 2) return null;
  return [list[list.length - 2].name, list[list.length - 1].name];
}

/** Whether the account may make an issue tag directly (the project's issueTags rule; owners by default). */
export const mayTagDirectly = (role: Role | null | undefined, rules: Pick<Rules, 'issueTags'> | null | undefined) => atLeast(role, rules?.issueTags ?? 'owner');

/** The ISO 19650 container an issue's status puts it in, as the server names a submission's purpose. */
export const purposeOf = (container: string): 'shared' | 'published' | null =>
  /^shared$/i.test(container) ? 'shared' : /^published$/i.test(container) ? 'published' : null;

/** A submission's status in words (the interface translates them). */
export const SUBMISSION_STATUS: Record<Submission['status'], string> = { pending: 'Waiting for approval', approved: 'Approved', rejected: 'Rejected', withdrawn: 'Withdrawn' };

/** What a submission item of the activity did, in a few words each (translated by the interface). */
export function submissionWords(changes: { type: string }[]): string[] {
  const words: string[] = [];
  for (const c of changes) {
    const w = c.type === 'submission_created' ? 'submitted for approval' : c.type === 'submission_approved' ? 'approved'
      : c.type === 'submission_rejected' ? 'rejected' : c.type === 'submission_withdrawn' ? 'withdrew it' : c.type === 'gate_overridden' ? 'passed the gate by override'
      : c.type === 'checks_done' ? 'checked the documents' : 'changed it';
    if (!words.includes(w)) words.push(w);
  }
  return words;
}
