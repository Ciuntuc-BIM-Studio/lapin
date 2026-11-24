import { describe, expect, it, vi } from 'vitest';

vi.mock('../../../engine/remote', () => ({ remoteCall: vi.fn() }));
const { personName, rightsOf, statusesFor } = await import('./issues');
import type { IssueInfo } from './issues';

const info = (role: 'viewer' | 'editor' | 'owner', rules: Partial<NonNullable<IssueInfo['rules']>> = {}): IssueInfo => ({
  linked: true, account: 'ana@example.com', role, known: ['t1'],
  rules: { comments: 'viewer', close: 'author', resolveNeedsComment: true, ...rules },
  people: { 'ana@example.com': 'Ana Popescu', 'mihai@example.com': 'Mihai Ionescu' },
  extensions: { statuses: ['Open', 'In progress', 'Resolved', 'Closed'] },
});
const topic = (author: string, assigned?: string, guid = 't1') => ({ guid, creationAuthor: author, assignedTo: assigned });

describe("what the server's rules let a user do with an issue", () => {
  it('lets everything without a server, or before the first sync', () => {
    expect(rightsOf(null, topic('x'))).toMatchObject({ create: true, edit: true, del: true, close: true });
    expect(rightsOf({ linked: true }, topic('x'))).toMatchObject({ edit: true });
  });

  it('owners do everything, deleting included', () => {
    expect(rightsOf(info('owner'), topic('mihai@example.com'))).toEqual({ create: true, edit: true, statusOnly: false, comment: true, close: true, del: true });
  });

  it('editors change what they made or are assigned to, close only what they made, delete nothing', () => {
    expect(rightsOf(info('editor'), topic('ANA@example.com'))).toMatchObject({ edit: true, close: true, del: false });
    expect(rightsOf(info('editor'), topic('mihai@example.com', 'ana@example.com'))).toMatchObject({ edit: true, close: false, del: false });
    expect(rightsOf(info('editor', { close: 'editor' }), topic('mihai@example.com', 'ana@example.com'))).toMatchObject({ close: true });
    expect(rightsOf(info('editor'), topic('mihai@example.com'))).toMatchObject({ edit: false, comment: true });
  });

  it('viewers read, comment while the rules let them, and change the status of what is assigned to them', () => {
    expect(rightsOf(info('viewer'), null)).toMatchObject({ create: false, comment: true });
    expect(rightsOf(info('viewer', { comments: 'editor' }), topic('m'))).toMatchObject({ edit: false, comment: false });
    expect(rightsOf(info('viewer'), topic('m', 'ana@example.com'))).toMatchObject({ edit: true, statusOnly: true, close: false });
  });

  it("a remark not on the server yet is the user's own", () => {
    expect(rightsOf(info('editor'), topic('Ana', undefined, 'new'))).toMatchObject({ edit: true, del: true });
  });

  it('offers Closed only to who may close, keeping a remark already closed', () => {
    const r = rightsOf(info('editor'), topic('mihai@example.com', 'ana@example.com'));
    expect(statusesFor(info('editor'), [], r)).toEqual(['Open', 'In progress', 'Resolved']);
    expect(statusesFor(info('editor'), [], r, 'Closed')).toContain('Closed');
  });

  it("names people by the members' names", () => {
    expect(personName(info('owner'), 'Mihai@example.com')).toBe('Mihai Ionescu');
    expect(personName(info('owner'), 'Lapin user')).toBe('Lapin user');
  });
});
