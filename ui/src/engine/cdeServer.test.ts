import { beforeEach, describe, expect, it, vi } from 'vitest';

// This tab's storage and origin, as the browser has them.
const tab = new Map<string, string>();
vi.stubGlobal('window', {
  sessionStorage: { getItem: (k: string) => tab.get(k) ?? null, setItem: (k: string, v: string) => void tab.set(k, v), removeItem: (k: string) => void tab.delete(k) },
  location: { origin: 'https://cde.example.org' },
});
const fetch = vi.fn();
vi.stubGlobal('fetch', (...a: unknown[]) => fetch(...a));
vi.mock('./session', () => ({ engineInBrowser: true }));
const cde = await import('./cdeServer');

const answer = (status: number, body: unknown, type = 'application/json') =>
  ({ ok: status < 300, status, headers: new Headers({ 'content-type': type }), json: async () => body, arrayBuffer: async () => new TextEncoder().encode(JSON.stringify(body)).buffer }) as unknown as Response;
const auth = (init: RequestInit | undefined) => (init?.headers as Record<string, string> | undefined)?.authorization ?? null;
const form = (init: RequestInit | undefined) => Object.fromEntries(new URLSearchParams(String(init?.body)));

beforeEach(() => {
  tab.clear();
  fetch.mockReset();
  cde.resetCdeForTests();
});

describe('the sign-in', () => {
  it('asks for a token with the password grant as lapin-web, keeps the refresh token in this tab only, and reads who signed in', async () => {
    fetch.mockImplementation(async (url: string, init?: RequestInit) => {
      if (url === '/foundation/oauth2/token') {
        expect(init?.method).toBe('POST');
        expect(form(init)).toEqual({ grant_type: 'password', username: 'ana@example.com', password: 'secret-pw', client_id: 'lapin-web' });
        return answer(200, { access_token: 'A1', refresh_token: 'R1', token_type: 'bearer', expires_in: 3600 });
      }
      expect(url).toBe('/foundation/1.1/current-user');
      expect(auth(init)).toBe('Bearer A1');
      return answer(200, { id: 'ana@example.com', name: 'Ana Popescu' });
    });
    await cde.signIn(' ana@example.com ', 'secret-pw');
    expect(cde.useCde.getState().user).toEqual({ id: 'ana@example.com', name: 'Ana Popescu' });
    expect([...tab.entries()]).toEqual([['lapin.cde.refresh', 'R1']]);
  });

  it("says the server's refusal", async () => {
    fetch.mockResolvedValue(answer(400, { error: 'invalid_grant', error_description: 'The e-mail address or the password is wrong.' }));
    await expect(cde.signIn('ana@example.com', 'nope')).rejects.toMatchObject({ status: 400, code: 'invalid_grant', message: 'The e-mail address or the password is wrong.' });
    expect(tab.size).toBe(0);
  });

  it('signs out: the tokens forgotten', async () => {
    tab.set('lapin.cde.refresh', 'R1');
    cde.useCde.setState({ user: { id: 'a', name: 'A' } });
    cde.signOut();
    expect(tab.size).toBe(0);
    expect(cde.useCde.getState().user).toBeNull();
  });
});

describe('a request', () => {
  it('after a reload refreshes first with the kept token, rotating it', async () => {
    tab.set('lapin.cde.refresh', 'R1');
    fetch.mockImplementation(async (url: string, init?: RequestInit) => {
      if (url === '/foundation/oauth2/token') {
        expect(form(init)).toEqual({ grant_type: 'refresh_token', refresh_token: 'R1', client_id: 'lapin-web' });
        return answer(200, { access_token: 'A2', refresh_token: 'R2' });
      }
      expect(auth(init)).toBe('Bearer A2');
      return answer(200, { projects: [{ id: 'p', name: 'Bloc', role: 'owner' }], more: false });
    });
    expect(await cde.listProjects()).toEqual([{ id: 'p', name: 'Bloc', role: 'owner' }]);
    expect(tab.get('lapin.cde.refresh')).toBe('R2');
  });

  it('refreshes once on a 401, for requests made together, and repeats them with the new token', async () => {
    tab.set('lapin.cde.refresh', 'R1');
    let refreshes = 0;
    let token = 'A1';
    fetch.mockImplementation(async (url: string, init?: RequestInit) => {
      if (url === '/foundation/oauth2/token') {
        refreshes++;
        token = `A${refreshes + 1}`;
        return answer(200, { access_token: token, refresh_token: `R${refreshes + 1}` });
      }
      return auth(init) === `Bearer ${token}` ? answer(200, { refs: [] }) : answer(401, { code: 'unauthorized', message: 'Sign in.' });
    });
    await cde.listRefs('p'); // the first refresh (no access token yet)
    token = 'expired-on-the-server';
    await Promise.all([cde.listRefs('p'), cde.listRefs('p'), cde.listRefs('p')]);
    expect(refreshes).toBe(2);
    expect(tab.get('lapin.cde.refresh')).toBe('R3');
  });

  it('a refresh token that is dead signs the interface out', async () => {
    tab.set('lapin.cde.refresh', 'R1');
    cde.useCde.setState({ user: { id: 'a', name: 'A' } });
    fetch.mockImplementation(async (url: string) => url === '/foundation/oauth2/token'
      ? answer(400, { error: 'invalid_grant', error_description: 'expired' })
      : answer(401, { code: 'unauthorized', message: 'Sign in first.' }));
    await expect(cde.listRefs('p')).rejects.toMatchObject({ status: 401, code: 'unauthorized' });
    expect(cde.useCde.getState().user).toBeNull();
    expect(tab.size).toBe(0);
  });

  it("changing the password keeps the fresh pair the server hands back", async () => {
    tab.set('lapin.cde.refresh', 'R1');
    fetch.mockImplementation(async (url: string, init?: RequestInit) => {
      if (url === '/foundation/oauth2/token') return answer(200, { access_token: 'A2', refresh_token: 'R2' });
      expect(url).toBe('/atrium/1.0/account/password');
      expect(JSON.parse(String(init?.body))).toEqual({ currentPassword: 'old-pass', newPassword: 'new-password' });
      return answer(200, { access_token: 'A3', refresh_token: 'R3' });
    });
    await cde.changePassword('old-pass', 'new-password');
    expect(tab.get('lapin.cde.refresh')).toBe('R3');
  });
});

describe('the server', () => {
  it('is the build\'s own origin when it says so, else the origin that answers lapin-remote\'s capabilities', async () => {
    fetch.mockResolvedValue(answer(200, { protocol: 'lapin-remote', version: '1.0' }));
    expect(await cde.detectCde()).toBe('https://cde.example.org');
    expect(fetch.mock.calls[0][0]).toBe('/lapin-remote/1.0/capabilities');
    expect(cde.cdeActive()).toBe(true);
  });

  it('is none where the origin answers a page (a web build served without a server)', async () => {
    fetch.mockResolvedValue(answer(200, '<!doctype html>', 'text/html'));
    expect(await cde.detectCde()).toBeNull();
    expect(cde.cdeActive()).toBe(false);
  });
});

describe('a project read', () => {
  const ref = (name: string, at = '2026-11-23T10:00:00Z') => ({ name, target: name.length.toString(16).padStart(64, '0'), updatedAt: at, updatedBy: { id: 'x', name: 'X' } });

  it('sorts its refs: main, branches in progress newest first, versions and issues by number', () => {
    const r = cde.readRefs([
      ref('refs/tags/issue/2-P01'), ref('refs/heads/wip/ana/2026-11-22-0900', '2026-11-22T09:00:00Z'), ref('refs/tags/v/10'),
      ref('refs/heads/main'), ref('refs/tags/v/2'), ref('refs/tags/issue/1-C01'), ref('refs/heads/wip/mihai/x', '2026-11-23T11:00:00Z'), ref('refs/other'),
    ]);
    expect(r.main?.name).toBe('refs/heads/main');
    expect(r.wips.map((w) => [w.who, w.label])).toEqual([['mihai', 'x'], ['ana', '2026-11-22-0900']]);
    expect(r.versions.map((v) => v.number)).toEqual([2, 10]);
    expect(r.issues.map((i) => [i.number, i.revision])).toEqual([[1, 'C01'], [2, 'P01']]);
  });

  it('names refs shortly', () => {
    expect(['refs/heads/main', 'refs/heads/wip/ana/b1', 'refs/tags/v/3', 'refs/tags/issue/4-P02', 'x'].map(cde.refLabel)).toEqual(['main', 'ana/b1', 'v3', '#4 P02', 'x']);
  });

  it("gives each commit its versions' and issues' chips", () => {
    const chips = cde.chipsByCommit([
      { commit: 'c1', kind: 'version', number: 3, name: 'For the client' },
      { commit: 'c1', kind: 'issue', number: 1, revision: 'P01', status: 'S2' },
      { commit: 'c2', kind: 'version', number: 4 },
      { commit: 'c3', kind: 'other', number: 1 },
    ]);
    expect([...chips.entries()]).toEqual([['c1', ['v3 For the client', '#1 P01 S2']], ['c2', ['v4']]]);
  });

  it('lists the activity newest first, the last entries only', async () => {
    tab.set('lapin.cde.refresh', 'R1');
    const e = (n: number) => ({ name: 'refs/heads/main', old: null, new: 'x', by: { id: 'a', name: 'A' }, at: `2026-11-23T10:0${n}:00Z`, force: false });
    fetch.mockImplementation(async (url: string) => {
      if (url === '/foundation/oauth2/token') return answer(200, { access_token: 'A2', refresh_token: 'R2' });
      return url.includes('cursor=') ? answer(200, { entries: [e(3), e(4)], more: false }) : answer(200, { entries: [e(1), e(2)], more: true, cursor: 'Mg' });
    });
    expect((await cde.activity('p', 3)).map((x) => x.at)).toEqual(['2026-11-23T10:04:00Z', '2026-11-23T10:03:00Z', '2026-11-23T10:02:00Z']);
  });

  it("reads a branch's commits from the server's walk", async () => {
    tab.set('lapin.cde.refresh', 'R1');
    fetch.mockImplementation(async (url: string) => {
      if (url === '/foundation/oauth2/token') return answer(200, { access_token: 'A2', refresh_token: 'R2' });
      expect(url).toBe('/lapin-remote/1.0/projects/p/commits?from=h&limit=50');
      return answer(200, { commits: [{ id: 'h', commit: { message: 'Walls', at: '2026-11-23T10:00:00Z', author: { id: 'atrium:1', name: 'Ana' }, parents: ['g'], entities: 12, tree: 't' } }], more: false });
    });
    expect(await cde.listCommits('p', 'h', 50)).toEqual([{ id: 'h', message: 'Walls', at: '2026-11-23T10:00:00Z', author: { id: 'atrium:1', name: 'Ana' }, parents: ['g'], entities: 12 }]);
  });
});
