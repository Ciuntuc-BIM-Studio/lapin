import { beforeEach, describe, expect, it, vi } from 'vitest';

const tab = new Map<string, string>();
vi.stubGlobal('window', {
  sessionStorage: { getItem: (k: string) => tab.get(k) ?? null, setItem: (k: string, v: string) => void tab.set(k, v), removeItem: (k: string) => void tab.delete(k) },
  location: { origin: 'https://cde.example.org' },
});
const fetch = vi.fn();
vi.stubGlobal('fetch', (...a: unknown[]) => fetch(...a));
vi.mock('./session', () => ({ engineInBrowser: true }));
const remoteCall = vi.fn();
vi.mock('./remote', () => ({ remoteCall: (...a: unknown[]) => remoteCall(...a) }));
const cde = await import('./cdeServer');
const atrium = await import('./atrium');

const answer = (status: number, body: unknown) => ({ ok: status < 300, status, headers: new Headers({ 'content-type': 'application/json', etag: '"e1"' }), json: async () => body }) as unknown as Response;

beforeEach(() => {
  fetch.mockReset(); remoteCall.mockReset(); tab.clear();
  cde.resetCdeForTests();
  cde.keepCdeTokens('A1', 'R1');
});

describe("Atrium's own API", () => {
  it('in front of the server is the page\'s own request; a refusal comes with its code, said here', async () => {
    cde.useCde.setState({ server: 'https://cde.example.org' });
    fetch.mockImplementation(async (url: string, init?: RequestInit) => {
      expect(url).toBe('/atrium/1.0/admin/users/ana%40example.com');
      expect(init?.method).toBe('PATCH');
      expect(JSON.parse(String(init?.body))).toEqual({ disabled: true });
      return answer(409, { code: 'self', message: "You can't disable your own account." });
    });
    await expect(atrium.changeUser('https://cde.example.org', 'ana@example.com', { disabled: true }))
      .rejects.toMatchObject({ status: 409, code: 'self', message: 'You cannot disable your own account or take away your own administration.' });
  });

  it('on the desktop goes through the engine (remote.atrium), on the project\'s sign-in', async () => {
    remoteCall.mockResolvedValue({ status: 201, body: { user: { email: 'x@y.z' }, password: 'p4ssw0rd-once' }, etag: null });
    const r = await atrium.createUser('https://other.example.org', { email: ' x@y.z ', name: 'X' });
    expect(r.password).toBe('p4ssw0rd-once');
    expect(remoteCall).toHaveBeenCalledWith('remote.atrium', { url: 'https://other.example.org', method: 'POST', path: 'atrium/1.0/admin/users', body: { email: 'x@y.z', name: 'X', isAdmin: false } }, 'https://other.example.org');
  });

  it('a server without the endpoint says so', async () => {
    remoteCall.mockResolvedValue({ status: 404, body: null, etag: null });
    await expect(atrium.account('https://old.example.org')).rejects.toMatchObject({ code: 'not_offered' });
  });

  it("says an issue's changes in a few words, each kind once", () => {
    const words = atrium.issueWords({
      kind: 'issue', at: '', by: { id: 'a', name: 'Ana', email: 'ana@example.com' }, topicGuid: 'g', title: 'T',
      changes: [
        { type: 'comment_created', commentGuid: 'c', viewpointGuid: null, old: null, new: null },
        { type: 'status_updated', commentGuid: null, viewpointGuid: null, old: 'Open', new: 'Resolved' },
        { type: 'comment_created', commentGuid: 'd', viewpointGuid: null, old: null, new: null },
        { type: 'assigned_to_updated', commentGuid: null, viewpointGuid: null, old: null, new: 'mihai@example.com' },
      ],
    }, (e) => (e === 'mihai@example.com' ? 'Mihai' : e));
    expect(words).toEqual(['commented', 'status: Resolved', 'assigned to Mihai']);
  });

  it('W5: a register goes up as its file, in the browser and through the engine; a refusal keeps the server\'s words and body', async () => {
    cde.useCde.setState({ server: 'https://cde.example.org' });
    const csv = new TextEncoder().encode('cod;titlu\nA-1;Plan\n');
    fetch.mockImplementationOnce(async (url: string, init?: RequestInit) => {
      expect(url).toBe('/atrium/1.0/projects/p%201/register?name=registru%20v2.csv');
      expect(init?.method).toBe('PUT');
      expect((init?.headers as Record<string, string>)['content-type']).toBe('text/csv');
      expect(init?.body).toBe(csv);
      return answer(200, { name: 'registru v2.csv', count: 1, entries: [], warnings: [] });
    });
    expect((await atrium.putRegister('https://cde.example.org', 'p 1', csv, 'registru v2.csv')).count).toBe(1);
    fetch.mockImplementationOnce(async () => answer(400, { code: 'bad_register', message: 'No code column.' }));
    await expect(atrium.putRegister('https://cde.example.org', 'p1', csv, 'r.xlsx')).rejects.toMatchObject({ status: 400, code: 'bad_register', serverMessage: 'No code column.' });
    remoteCall.mockResolvedValue({ status: 200, body: { name: 'r.xlsx', count: 2 }, etag: null });
    expect((await atrium.putRegister('https://other.example.org', 'p1', csv, 'r.xlsx')).count).toBe(2);
    expect(remoteCall).toHaveBeenCalledWith('remote.atrium', {
      url: 'https://other.example.org', method: 'PUT', path: 'atrium/1.0/projects/p1/register?name=r.xlsx',
      data: btoa('cod;titlu\nA-1;Plan\n'), contentType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    }, 'https://other.example.org');
  });

  it('W5: no register is null, not an error; the checks run again with a POST; a gate refusal carries its checks', async () => {
    remoteCall.mockResolvedValueOnce({ status: 404, body: { code: 'no_register', message: 'None.' }, etag: null });
    expect(await atrium.getRegister('https://other.example.org', 'p1')).toBeNull();
    remoteCall.mockResolvedValueOnce({ status: 202, body: { id: 's1', checks: { status: 'pending', run: 2 } }, etag: null });
    expect((await atrium.rerunChecks('https://other.example.org', 'p1', 's1')).checks?.run).toBe(2);
    expect(remoteCall).toHaveBeenLastCalledWith('remote.atrium', { url: 'https://other.example.org', method: 'POST', path: 'atrium/1.0/projects/p1/submissions/s1/checks' }, 'https://other.example.org');
    remoteCall.mockResolvedValueOnce({ status: 422, body: { code: 'gate_failed', message: 'x', topics: [], checks: { status: 'done', errors: 2, warnings: 0, infos: 0 } }, etag: null });
    const e = await atrium.approveSubmission('https://other.example.org', 'p1', 's1', '').catch((x) => x);
    expect(e).toBeInstanceOf(atrium.AtriumError);
    expect(e.body.checks.errors).toBe(2);
  });
  it('makes, lists and revokes personal access tokens (R6), the secret returned once', async () => {
    remoteCall.mockResolvedValueOnce({ status: 201, body: { id: 't1', name: 'Claude', scope: 'read+write', created: 'c', expires: 'e', lastUsed: null, expired: false, token: 'atrium_pat_x' }, etag: null });
    const made = await atrium.createAccessToken('https://other.example.org', { name: ' Claude ', scope: 'read+write', expiresInDays: 90 });
    expect(made.token).toBe('atrium_pat_x');
    expect(remoteCall).toHaveBeenLastCalledWith('remote.atrium', { url: 'https://other.example.org', method: 'POST', path: 'atrium/1.0/account/tokens', body: { name: 'Claude', scope: 'read+write', expiresInDays: 90 } }, 'https://other.example.org');
    remoteCall.mockResolvedValueOnce({ status: 200, body: { tokens: [{ id: 't1', name: 'Claude' }] }, etag: null });
    expect((await atrium.accessTokens('https://other.example.org')).map((t) => t.id)).toEqual(['t1']);
    remoteCall.mockResolvedValueOnce({ status: 204, body: null, etag: null });
    await atrium.revokeAccessToken('https://other.example.org', 't/1');
    expect(remoteCall).toHaveBeenLastCalledWith('remote.atrium', { url: 'https://other.example.org', method: 'DELETE', path: 'atrium/1.0/account/tokens/t%2F1' }, 'https://other.example.org');
    const { claudeCommand } = await import('../modules/core/AccessTokens');
    expect(claudeCommand('https://cde.example.org/')).toBe('claude mcp add --transport http atrium https://cde.example.org/mcp --header "Authorization: Bearer <token>"');
  });
});
