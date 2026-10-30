import { beforeEach, describe, expect, it, vi } from 'vitest';

// This device's storage, as localStorage is in the app.
const stored = new Map<string, string>();
vi.stubGlobal('localStorage', {
  getItem: (k: string) => stored.get(k) ?? null,
  setItem: (k: string, v: string) => void stored.set(k, v),
  removeItem: (k: string) => void stored.delete(k),
});
const call = vi.fn();
const notify = vi.fn();
vi.mock('../engine/session', () => ({ engine: { call: (...a: unknown[]) => call(...a) }, engineInBrowser: true }));
vi.mock('../engine/share', () => ({ isGuest: () => false }));
vi.mock('../app/store', () => ({ notify: (...a: unknown[]) => notify(...a), useUi: { getState: () => ({ windowRole: 'main' }) } }));
const replies = await import('./replies');
const { reportParams } = await import('./support');
const { contactFor, forgetMe, keyIdOf, newSecret, poll, refresh, shouldPoll, unreadCount, unreadOf, useReplies, validSecret, loadSettings } = replies;

const answer = (status: number, body: unknown) => ({ ok: status < 300, status, json: async () => body }) as Response;
const form = { kind: 'bug' as const, title: 'Roofs', description: 'Gaps at the ridge', expected: '', email: '', screenshot: null };
const report = (messages: { from: 'lapin' | 'user'; text: string; at: string }[], seen: number) =>
  ({ number: 3, title: 'Roofs', kind: 'bug', receivedAt: '2026-10-30T10:00:00Z', status: 'open' as const, description: 'Gaps', messages, seen });
const m = (from: 'lapin' | 'user') => ({ from, text: 'x', at: '2026-10-30T11:00:00Z' });

beforeEach(() => {
  stored.clear();
  useReplies.setState({ settings: null, reports: [], unread: 0, loaded: false });
  call.mockReset();
  notify.mockReset();
});

describe('a key without an account', () => {
  it('is 32 random bytes in base64url, a new one each time', () => {
    const a = newSecret();
    expect(a).toMatch(/^[A-Za-z0-9_-]{43}$/);
    expect(validSecret(a)).toBe(true);
    expect(newSecret()).not.toBe(a);
    expect(validSecret('too short')).toBe(false);
  });

  it("is told by its id, the SHA-256 of the secret's text in hex, as the server computes it", async () => {
    expect(await keyIdOf('abc')).toBe('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
  });
});

describe('the report form', () => {
  it('sends an anonymous report as before, and makes no key, when no reply is asked for', async () => {
    const contact = await contactFor(false, 'Ana');
    expect(contact).toBeUndefined();
    const p = reportParams(form, ['e1'], '1440×900 @2', contact);
    expect(p).not.toHaveProperty('contact');
    expect(p).toMatchObject({ kind: 'bug', title: 'Roofs', errors: ['e1'], consent: false, website: '' });
    expect(stored.size).toBe(0);
  });

  it('sends the nickname and the key id, never the secret, when a reply is asked for; the key is kept and reused', async () => {
    const contact = await contactFor(true, '  Ana  ');
    const secret = loadSettings()!.secret;
    expect(contact).toEqual({ nickname: 'Ana', keyId: await keyIdOf(secret) });
    expect(JSON.stringify(reportParams(form, [], 's', contact))).not.toContain(secret);
    expect((await contactFor(true, ''))!.keyId).toBe(contact!.keyId);
    expect(await contactFor(true, '')).not.toHaveProperty('nickname');
  });
});

describe('asking for news', () => {
  const env = { online: true, guest: false, main: true };
  const settings = { secret: 'x'.repeat(43), nickname: '', active: true, notified: 0 };

  it('only once opted in and not stopped, online, from the main window of the computer\'s own user', () => {
    expect(shouldPoll(null, env)).toBe(false);
    expect(shouldPoll(settings, env)).toBe(true);
    expect(shouldPoll({ ...settings, active: false }, env)).toBe(false);
    expect(shouldPoll(settings, { ...env, online: false })).toBe(false);
    expect(shouldPoll(settings, { ...env, guest: true })).toBe(false);
    expect(shouldPoll(settings, { ...env, main: false })).toBe(false);
  });

  it('makes no call before the opt-in, and fails quietly', async () => {
    const post = vi.fn();
    await poll(post as unknown as typeof fetch);
    expect(call).not.toHaveBeenCalled();
    await contactFor(true, '');
    call.mockRejectedValueOnce(new Error('offline'));
    await expect(poll(post as unknown as typeof fetch)).resolves.toBeUndefined();
    expect(notify).not.toHaveBeenCalled();
  });

  it('counts the unread replies from Lapin only, after the read marker', () => {
    expect(unreadOf(report([m('lapin'), m('user'), m('lapin')], 0))).toBe(2);
    expect(unreadOf(report([m('lapin'), m('user'), m('lapin')], 2))).toBe(1);
    expect(unreadOf(report([m('user')], 0))).toBe(0);
    expect(unreadCount([report([m('lapin')], 0), report([m('lapin'), m('lapin')], 1)])).toBe(2);
  });

  it('reads the inbox through the page in the browser, sets the badge and announces a new reply once', async () => {
    await contactFor(true, 'Ana');
    const secret = loadSettings()!.secret;
    call.mockImplementation(async (_m: string, p: Record<string, unknown>) => ({ endpoint: p.endpoint, send: { secret: p.secret } }));
    const post = vi.fn().mockResolvedValue(answer(200, { reports: [report([m('lapin')], 0)], unread: 1 }));
    await refresh(post as unknown as typeof fetch);
    expect(call.mock.calls[0]).toEqual(['support.messages', { endpoint: 'inbox', secret }]);
    expect(post.mock.calls[0][0]).toBe('/api/feedback/inbox');
    expect(JSON.parse(post.mock.calls[0][1].body)).toEqual({ secret });
    expect(useReplies.getState().unread).toBe(1);
    expect(notify).toHaveBeenCalledTimes(1);
    await refresh(post as unknown as typeof fetch);
    expect(notify).toHaveBeenCalledTimes(1);
  });
});

describe('forget me', () => {
  it('asks the server, then deletes the key here', async () => {
    await contactFor(true, 'Ana');
    call.mockImplementation(async (_m: string, p: Record<string, unknown>) => ({ endpoint: p.endpoint, send: { secret: p.secret } }));
    await forgetMe(vi.fn().mockResolvedValue(answer(200, { ok: true, forgotten: 2 })) as unknown as typeof fetch);
    expect(call.mock.calls[0][1]).toMatchObject({ endpoint: 'forget' });
    expect(stored.size).toBe(0);
    expect(useReplies.getState().settings).toBeNull();
  });

  it('keeps the key when the server could not be reached, to try again', async () => {
    await contactFor(true, 'Ana');
    call.mockImplementation(async (_m: string, p: Record<string, unknown>) => ({ endpoint: p.endpoint, send: { secret: p.secret } }));
    await expect(forgetMe(vi.fn().mockResolvedValue(answer(503, { error: 'Down.' })) as unknown as typeof fetch)).rejects.toThrow('Down.');
    expect(loadSettings()).not.toBeNull();
  });
});
