import { beforeEach, describe, expect, it, vi } from 'vitest';

// The browser in front of a CDE server (Atrium docs/plan-cde-web.md, W2): the engine's remote calls with the start screen's one sign-in.
const tab = new Map<string, string>();
const local = new Map<string, string>();
const store = (m: Map<string, string>) => ({ getItem: (k: string) => m.get(k) ?? null, setItem: (k: string, v: string) => void m.set(k, v), removeItem: (k: string) => void m.delete(k) });
vi.stubGlobal('window', { sessionStorage: store(tab), localStorage: store(local), location: { origin: 'https://cde.example.org' } });
const fetch = vi.fn();
vi.stubGlobal('fetch', (...a: unknown[]) => fetch(...a));
const call = vi.fn();
vi.mock('./session', () => ({ engineInBrowser: true, engine: { call: (...a: unknown[]) => call(...a) }, useSession: { getState: () => ({ doc: null }) } }));
const keepProjectFile = vi.fn(async () => true);
vi.mock('./files', () => ({ isDesktop: () => false, keepProjectFile: (...a: unknown[]) => keepProjectFile(...(a as [])), reportWarnings: () => undefined, saveProject: async () => true }));
vi.mock('../app/store', () => ({ notify: () => undefined, useUi: { setState: () => undefined } }));

const cde = await import('./cdeServer');
const remote = await import('./remote');

const SERVER = 'https://cde.example.org';
const answer = (status: number, body: unknown) => ({ ok: status < 300, status, json: async () => body }) as unknown as Response;
const status = (url: string, ahead: number, uncommitted = false) => ({
  branch: 'refs/heads/main', head: 'c1', uncommitted, saved: true, problems: [], unreadable: null,
  remotes: [{ name: 'origin', kind: 'server', url, project: 'p1', ahead, behind: 0, fetchedAt: null, remoteHead: null }],
});

beforeEach(() => {
  tab.clear(); local.clear(); fetch.mockReset(); call.mockReset(); keepProjectFile.mockClear();
  cde.resetCdeForTests();
  cde.useCde.setState({ server: SERVER, user: { id: 'mihai@example.com', name: 'Mihai Ionescu' } });
});

describe('one sign-in for the interface and the engine', () => {
  it('hands the engine a live access token (refreshed first after a reload) and keeps the pair a refresh in the engine replaced', async () => {
    tab.set('lapin.cde.refresh', 'R1');
    fetch.mockResolvedValue(answer(200, { access_token: 'A2', refresh_token: 'R2' }));
    call.mockResolvedValue({ pushed: true, tokens: { accessToken: 'A3', refreshToken: 'R3' }, status: status(SERVER, 0) });
    await remote.remoteCall('remote.push', {}, SERVER);
    const [method, params] = call.mock.calls[0] as [string, Record<string, unknown>];
    expect(method).toBe('remote.push');
    expect(params).toMatchObject({ token: 'A2', refreshToken: 'R2' });
    expect(tab.get('lapin.cde.refresh')).toBe('R3');
    // The interface's own requests go on with the engine's new token.
    fetch.mockResolvedValue(answer(200, { projects: [], more: false }));
    await cde.listProjects();
    expect((fetch.mock.calls.at(-1)![1] as RequestInit & { headers: Record<string, string> }).headers.authorization).toBe('Bearer A3');
  });

  it('signs commits with the account’s name and this installation’s id, never the e-mail', () => {
    const a = remote.authorFor(SERVER);
    expect(a.name).toBe('Mihai Ionescu');
    expect(a.id).toMatch(/^local:[0-9a-f]{16}$/);
  });

  it('is signed in to the CDE server as the start screen is', async () => {
    expect(await remote.signedIn(SERVER)).toBe(true);
    expect(await remote.signedIn(`${SERVER}/`)).toBe(true);
    cde.useCde.setState({ user: null });
    expect(await remote.signedIn(SERVER)).toBe(false);
  });
});

describe('the copy in the browser', () => {
  it('keeps a commit in the engine, with no download, when the project’s remote is the CDE server', async () => {
    call.mockResolvedValue({ name: 'p1.lapin', data: 'UEsDBA==', commit: {}, status: status(SERVER, 1) });
    expect(await remote.writingCall('history.commit', { message: 'Walls' }, SERVER)).not.toBeNull();
    expect((call.mock.calls[0] as [string, Record<string, unknown>])[1]).toMatchObject({ bytes: true, message: 'Walls' });
    expect(keepProjectFile).not.toHaveBeenCalled();
  });

  it('still keeps the file of a project whose remote is another', async () => {
    call.mockResolvedValue({ name: 'p1.lapin', data: 'UEsDBA==', status: status('http://127.0.0.1:5280', 1) });
    await remote.writingCall('history.commit', { message: 'Walls' }, 'http://127.0.0.1:5280');
    expect(keepProjectFile).toHaveBeenCalledOnce();
  });

  it('knows what the server has not got: commits not pushed or changes not committed, cleared by a push', () => {
    remote.noteStatus(status(SERVER, 0, true));
    expect(cde.useCde.getState().unpushed).toEqual({ ahead: 0, uncommitted: true });
    remote.noteStatus({ status: status(SERVER, 2) });
    expect(cde.useCde.getState().unpushed).toEqual({ ahead: 2, uncommitted: false });
    remote.noteStatus({ pushed: true, status: status(SERVER, 0) });
    expect(cde.useCde.getState().unpushed).toBeNull();
    expect(cde.useCde.getState().linked).toBe(true);
    // A project on another server is not the CDE's to watch.
    remote.noteStatus(status('http://127.0.0.1:5280', 3));
    expect(cde.useCde.getState().unpushed).toBeNull();
    expect(cde.useCde.getState().linked).toBe(false);
    // Connected in Collaborate: remote.add answers the status itself.
    remote.noteStatus({ ...status(SERVER, 0), name: 'p1.lapin', data: 'UEsDBA==' });
    expect(cde.useCde.getState().linked).toBe(true);
  });
});
