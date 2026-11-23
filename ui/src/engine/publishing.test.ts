import { describe, expect, it } from 'vitest';
import type { Containers, IssueTagItem, Submission } from './atrium';
import { comparable, containerRows, issueLabel, mayTagDirectly, proposedPair, purposeOf, submissionRights, submissionWords, tagShort } from './publishing';

const sub = (over: Partial<Submission> = {}): Submission => ({
  id: 's1', number: 1, status: 'pending', commit: 'c'.repeat(64), issue: 4, issueId: null, revision: 'P02', purpose: 'shared', suitability: 'S2', note: '',
  documents: [], files: [], author: { id: 'atrium:ana', name: 'Ana Popescu', email: 'ana@example.com' }, created: '2026-11-23T10:00:00Z',
  decidedBy: null, decided: null, decisionNote: null, reason: null, override: false, tag: null, tagId: null, closedTopics: [], gate: { passed: true, topics: [] }, ...over,
});

const tag = (n: number, rev: string, at: string, container = 'Shared'): IssueTagItem => ({
  kind: 'issue', name: `refs/tags/issue/${n}-${rev}`, issue: n, revision: rev, suitability: 'S2', container, tag: 't', commit: 'c', at, author: { id: 'a', name: 'Ana' },
  issuedAt: at, issuedBy: { id: 'm', name: 'Mihai' }, note: null, files: [{ name: 'x.pdf', sha256: 's', size: 1 }], submission: n === 2 ? 's1' : null, supersededBy: null,
});

describe('who may act on a submission', () => {
  it('lets owners approve, reject and override; the author withdraw; nobody act on one decided', () => {
    const owner = submissionRights(sub(), { email: 'mihai@example.com', role: 'owner' }, { approve: 'owner' });
    expect(owner).toMatchObject({ approve: true, reject: true, withdraw: false, override: true, why: null });
    const author = submissionRights(sub(), { email: 'ANA@example.com', role: 'editor' }, { approve: 'owner' });
    expect(author).toMatchObject({ approve: false, reject: false, withdraw: true, override: false });
    expect(author.why).toContain('owners');
    expect(submissionRights(sub({ status: 'approved' }), { email: 'mihai@example.com', role: 'owner' }, null)).toMatchObject({ approve: false, withdraw: false });
  });

  it('follows the approve rule for editors, never their own, and lets the only owner approve their own', () => {
    expect(submissionRights(sub(), { email: 'eva@example.com', role: 'editor' }, { approve: 'editor' })).toMatchObject({ approve: true, override: false });
    expect(submissionRights(sub(), { email: 'ana@example.com', role: 'editor' }, { approve: 'editor' })).toMatchObject({ approve: false, withdraw: true });
    const ownOnly = sub({ author: { id: 'm', name: 'Mihai', email: 'mihai@example.com' } });
    expect(submissionRights(ownOnly, { email: 'mihai@example.com', role: 'owner' }, null, 1).approve).toBe(true);
    expect(submissionRights(ownOnly, { email: 'mihai@example.com', role: 'owner' }, null, 2).approve).toBe(false);
    expect(submissionRights(sub(), { email: 'vio@example.com', role: 'viewer' }, { approve: 'editor' })).toMatchObject({ approve: false, withdraw: false });
    expect(submissionRights(sub(), { email: 'admin@example.com', role: 'viewer', isAdmin: true }, null).approve).toBe(true);
  });
});

describe('containers', () => {
  const c: Containers = {
    wip: [{ kind: 'branch', name: 'refs/heads/wip/ana-popescu/2026-11-23-1000', commit: 'w', updatedAt: '2026-11-23T10:00:00Z', updatedBy: { id: 'a', name: 'Ana' } }],
    shared: [tag(2, 'P02', '2026-11-22T10:00:00Z')],
    published: [tag(3, 'C01', '2026-11-23T09:00:00Z', 'Published')],
    archived: [tag(1, 'P01', '2026-11-20T10:00:00Z', 'Published')],
  };
  it('lists each container with its revision, date and who issued it', () => {
    expect(containerRows(c, 'wip')[0]).toMatchObject({ title: 'ana-popescu/2026-11-23-1000', revision: null, by: 'Ana', commit: 'w' });
    expect(containerRows(c, 'shared')[0]).toMatchObject({ title: '#2 P02', revision: 'P02', by: 'Mihai', files: 1, fromSubmission: true });
    expect(containerRows(null, 'published')).toEqual([]);
  });
  it('offers the issued revisions oldest first, the last two proposed for the diff', () => {
    const list = comparable(c);
    expect(list.map((x) => tagShort(x.name))).toEqual(['1-P01', '2-P02', '3-C01']);
    expect(proposedPair(list)).toEqual(['refs/tags/issue/2-P02', 'refs/tags/issue/3-C01']);
    expect(proposedPair(list.slice(0, 1))).toBeNull();
  });
});

describe('words', () => {
  it('labels, purposes, direct tags and the activity of a submission', () => {
    expect(issueLabel(4, 'P02')).toBe('#4 P02');
    expect(issueLabel(null, 'C01')).toBe('#? C01');
    expect([purposeOf('Shared'), purposeOf('PUBLISHED'), purposeOf('WIP')]).toEqual(['shared', 'published', null]);
    expect([mayTagDirectly('editor', { issueTags: 'owner' }), mayTagDirectly('editor', { issueTags: 'editor' }), mayTagDirectly('owner', null), mayTagDirectly(null, null)]).toEqual([false, true, true, false]);
    expect(submissionWords([{ type: 'submission_approved' }, { type: 'gate_overridden' }, { type: 'submission_approved' }])).toEqual(['approved', 'passed the gate by override']);
  });
});
