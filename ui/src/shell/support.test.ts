import { describe, expect, it, vi } from 'vitest';

const call = vi.fn();
vi.mock('../engine/session', () => ({ engine: { call: (...a: unknown[]) => call(...a) }, engineInBrowser: true }));
vi.mock('../app/store', () => ({ notify: vi.fn() }));
const { sendReport, keepRecentError, RECENT } = await import('./support');

const reply = (status: number, body: unknown) => ({ ok: status < 300, status, json: async () => body }) as Response;

describe('a report from the browser', () => {
  it('is posted by the page, the body the engine made, and its number comes back', async () => {
    call.mockResolvedValueOnce({ send: { app: 'lapin', title: 'Roofs' } });
    const post = vi.fn(async () => reply(200, { number: 42 }));
    expect(await sendReport({ kind: 'idea', title: 'Roofs', description: 'Hip' }, post as unknown as typeof fetch)).toBe(42);
    expect(call.mock.calls[0][0]).toBe('support.report');
    expect(call.mock.calls[0][1]).toHaveProperty('userAgent');
    const [url, init] = post.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe('/api/feedback');
    expect(JSON.parse(init.body as string)).toEqual({ app: 'lapin', title: 'Roofs' });
  });

  it('says why the server refused it', async () => {
    call.mockResolvedValueOnce({ send: {} });
    const post = vi.fn(async () => reply(429, { error: 'Too many reports.' }));
    await expect(sendReport({}, post as unknown as typeof fetch)).rejects.toThrow('Too many reports.');
  });

  it('is sent by the engine itself on the desktop', async () => {
    call.mockResolvedValueOnce({ number: 7 });
    const post = vi.fn();
    expect(await sendReport({}, post as unknown as typeof fetch)).toBe(7);
    expect(post).not.toHaveBeenCalled();
  });
});

describe('the recent errors a report carries', () => {
  it('leave out the benign ResizeObserver messages and keep the others, with their time', () => {
    RECENT.length = 0;
    const at = new Date('2026-10-29T08:15:30Z');
    keepRecentError('error: ResizeObserver loop completed with undelivered notifications. @ :0', at);
    keepRecentError('error: ResizeObserver loop limit exceeded @ :0', at);
    keepRecentError('console: ResizeObserver loop completed with undelivered notifications.', at);
    keepRecentError('rejection: The engine did not answer.', at);
    expect(RECENT).toEqual(['08:15:30 rejection: The engine did not answer.']);
  });

  it('keep the last twenty', () => {
    RECENT.length = 0;
    for (let i = 0; i < 25; i++) keepRecentError(`console: ${i}`);
    expect(RECENT).toHaveLength(20);
    expect(RECENT[0]).toMatch(/console: 5$/);
  });
});
