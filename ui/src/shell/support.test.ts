import { describe, expect, it, vi } from 'vitest';

const call = vi.fn();
vi.mock('../engine/session', () => ({ engine: { call: (...a: unknown[]) => call(...a) }, engineInBrowser: true }));
vi.mock('../app/store', () => ({ notify: vi.fn() }));
const { sendReport, keepRecentError, RECENT, forumTopicUrl, forumQuestion } = await import('./support');

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

describe('the user guide', () => {
  it('opens in each interface language: /docs/ for English, /docs/<lang>/ for the others, by the names the engine knows', async () => {
    const { guideUrl, guideLink } = await import('./support');
    const { LANGUAGES } = await import('../i18n');
    expect(LANGUAGES.map((l) => guideUrl(l.id))).toEqual(['', 'ro/', 'es/', 'de/', 'fr/', 'da/', 'it/', 'pl/', 'pt/', '', ''].map((s) => `https://lapin.ciuntucbimstudio.ro/docs/${s}`));
    expect(LANGUAGES.map((l) => guideLink(l.id))).toEqual(['guide', 'guideRo', 'guideEs', 'guideDe', 'guideFr', 'guideDa', 'guideIt', 'guidePl', 'guidePt', 'guide', 'guide']);
  });
});

describe('a new topic on the forum', () => {
  it('is the same address as the engine makes: only the forum, the title and text escaped in its query, in Lapin\'s category', () => {
    expect(forumTopicUrl(' Walls & roofs ', 'Lapin 0.1.9\nmacos')).toBe('https://forum.lapinbim.com/new-topic?title=Walls%20%26%20roofs&body=Lapin%200.1.9%0Amacos&category=lapin');
    expect(forumTopicUrl()).toBe('https://forum.lapinbim.com/new-topic?category=lapin');
    expect(forumTopicUrl('https://example.com/?x=1#y')).toBe('https://forum.lapinbim.com/new-topic?title=https%3A%2F%2Fexample.com%2F%3Fx%3D1%23y&category=lapin');
  });
  it('takes a short question as its title and a long one whole in its text, with the version and where Lapin runs', () => {
    expect(forumQuestion('How do I copy a wall?', '0.1.500', 'desktop')).toEqual({ title: 'How do I copy a wall?', body: '---\nLapin 0.1.500 · desktop' });
    const long = forumQuestion('x'.repeat(250), '0.1.500', 'web');
    expect(long.title).toHaveLength(198);
    expect(long.body).toBe(`${'x'.repeat(250)}\n\n\n---\nLapin 0.1.500 · web`);
  });
});
