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
});
