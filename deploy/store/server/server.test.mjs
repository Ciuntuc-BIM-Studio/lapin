// The store service on a temporary data folder, the forum and GitHub faked: node --test deploy/store/server/

import { after, before, describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { existsSync } from 'node:fs';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { cleanFileName, compareVersions, imageKind, storeService } from './server.mjs';
import { forumAnswer, loginRequest, readLogin } from './discourse.mjs';
import { githubClient } from './github.mjs';
import { run } from './cli.mjs';
import { sha256 } from './data.mjs';

const SECRET = 'a-test-secret-of-the-forum-and-the-store';
const STORE = 'https://store.lapinbim.com';
let dataDir, server, base, service;

/** GitHub as the store uses it, in memory. */
const gh = {
  prs: new Map(), next: 1, files: {},
  async openPullRequest({ branch, files, title }) { const n = this.next++; this.prs.set(n, { state: 'open', checks: 'running', branch, title }); Object.assign(this.files, files); return { number: n, url: `https://github.com/x/y/pull/${n}`, branch }; },
  async pullRequestState(n) { const p = this.prs.get(n); return { state: p.state, checks: p.checks, url: '' }; },
  async closePullRequest(n) { this.prs.get(n).state = 'closed'; },
};

const furniture = { format: 'lapin-furniture-library', version: 1, items: [{ name: 'Sofa', type: { class: 'IfcFurnitureType', spec: { kind: 'sofa' } } }] };
const listed = {
  schema: 1, id: 'ro.other.living', name: 'Living', summary: 'x', version: '1.0.0', author: { name: 'Other', github: 'other' }, license: 'MIT', lapin: '*', kind: 'furniture',
  contents: { file: 'l.lapinlib' }, package: { url: 'https://example.org/l.lapinlib', sha256: 'a'.repeat(64), size: 3 },
};

before(async () => {
  dataDir = await mkdtemp(join(tmpdir(), 'lapin-store-'));
  const pub = join(dataDir, 'public');
  await mkdir(join(pub, 'site', 'extension', 'ro.other.living'), { recursive: true });
  await writeFile(join(pub, 'site', 'index.html'), '<!doctype html><title>Lapin Extensions</title>');
  await writeFile(join(pub, 'site', 'extension', 'ro.other.living', 'index.html'), '<p>Living</p>');
  await writeFile(join(pub, 'site', '404.html'), '<p>Not here</p>');
  await mkdir(join(pub, 'extensions', 'files', 'ro.other.living', '1.0.0'), { recursive: true });
  await writeFile(join(pub, 'extensions', 'index.json'), JSON.stringify({ format: 'lapin-extensions-index', version: 1, extensions: [listed] }));
  await writeFile(join(pub, 'extensions', 'index.json.sig'), '{"format":"lapin-signature"}');
  await writeFile(join(pub, 'extensions', 'files', 'ro.other.living', '1.0.0', 'l.lapinlib'), 'abc');
  service = storeService({ dataDir, publicDir: pub, storeUrl: STORE, ssoSecret: SECRET, github: gh, salt: 'test', submissionsPerDay: 50, uploadsPerHour: 100, countedPerHour: 2, prCacheMs: 0, maintainers: new Set(['ionut']) });
  server = createServer(service.listener);
  await new Promise((r) => server.listen(0, '127.0.0.1', r));
  base = `http://127.0.0.1:${server.address().port}`;
});
after(async () => {
  await new Promise((r) => server.close(r));
  await rm(dataDir, { recursive: true, force: true });
});

const get = (path, headers = {}) => fetch(`${base}${path}`, { redirect: 'manual', headers });
/** A visitor signed in through the (faked) forum: their cookie. */
async function signIn(user) {
  const res = await get('/auth/login?return=/dashboard/');
  assert.equal(res.status, 302);
  const to = new URL(res.headers.get('location'));
  assert.equal(to.origin + to.pathname, 'https://forum.lapinbim.com/session/sso_provider');
  const payload = new URLSearchParams(Buffer.from(to.searchParams.get('sso'), 'base64').toString());
  assert.equal(payload.get('return_sso_url'), `${STORE}/auth/callback`);
  const answer = forumAnswer({ secret: SECRET, nonce: payload.get('nonce'), user });
  const back = await get(`/auth/callback?${new URLSearchParams(answer)}`);
  assert.equal(back.status, 302);
  assert.equal(back.headers.get('location'), '/dashboard/');
  const cookie = /lapin_store=([^;]+)/.exec(back.headers.get('set-cookie'))[1];
  assert.match(back.headers.get('set-cookie'), /HttpOnly; Secure; SameSite=Lax/);
  return `lapin_store=${cookie}`;
}
const as = (cookie) => ({
  get: (p) => fetch(`${base}${p}`, { headers: { cookie } }).then(async (r) => ({ status: r.status, body: await r.json() })),
  post: (p, b) => fetch(`${base}${p}`, { method: 'POST', headers: { cookie, origin: STORE, 'content-type': 'application/json' }, body: JSON.stringify(b ?? {}) }).then(async (r) => ({ status: r.status, body: await r.json() })),
  put: (p, bytes, headers = {}) => fetch(`${base}${p}`, { method: 'PUT', headers: { cookie, origin: STORE, ...headers }, body: bytes }).then(async (r) => ({ status: r.status, body: await r.json() })),
});
const fields = (over = {}) => ({ id: 'ana.sofas', kind: 'furniture', name: 'Ana’s sofas', version: '1.0.0', summary: 'Two sofas.', license: 'CC-BY-4.0', lapin: '>=0.1.900', tags: 'living, sofa', languages: 'ro', acceptTerms: true, ...over });

describe('the store: pages and the catalogue', () => {
  it('serves the site, its pages by folder, a 404 page, never a path outside it', async () => {
    assert.equal((await get('/')).status, 200);
    const page = await get('/extension/ro.other.living');
    assert.equal(page.status, 302, 'a folder without its slash');
    assert.equal(page.headers.get('location'), '/extension/ro.other.living/');
    assert.match(await (await get('/extension/ro.other.living/')).text(), /Living/);
    const html = await get('/');
    assert.match(html.headers.get('content-security-policy'), /default-src 'self'/);
    const missing = await get('/nothing');
    assert.equal(missing.status, 404);
    assert.match(await missing.text(), /Not here/);
    for (const p of ['/../server.mjs', '/%2e%2e/server.mjs', '/extensions/..%2F..%2Fsubmissions']) assert.notEqual((await get(p)).status, 200, p);
  });

  it('serves the index and its signature briefly cached, the files for ever, to any origin', async () => {
    const index = await get('/extensions/index.json');
    assert.equal(index.status, 200);
    assert.equal(index.headers.get('access-control-allow-origin'), '*');
    assert.match(index.headers.get('cache-control'), /max-age=300/);
    assert.equal((await get('/extensions/index.json.sig')).status, 200);
    const file = await get('/extensions/files/ro.other.living/1.0.0/l.lapinlib');
    assert.match(file.headers.get('cache-control'), /immutable/);
    assert.equal(await file.text(), 'abc');
  });

  it('counts a download and redirects to the copy; past the visitor’s limit it still redirects, uncounted; stats say how many', async () => {
    for (let i = 0; i < 4; i++) {
      const r = await get('/api/download/ro.other.living/1.0.0');
      assert.equal(r.status, 302);
      assert.equal(r.headers.get('location'), 'https://lapinbim.com/extensions/files/ro.other.living/1.0.0/l.lapinlib');
    }
    assert.equal((await get('/api/download/ro.other.living/9.9.9')).status, 404);
    const stats = await (await get('/api/stats/ro.other.living')).json();
    assert.deepEqual(stats, { total: 2, versions: { '1.0.0': 2 } });
    await service.flush();
    const kept = JSON.parse(await readFile(join(dataDir, 'stats.json'), 'utf8'));
    assert.deepEqual(Object.values(kept['ro.other.living']['1.0.0']), [2]);
    assert.ok(!JSON.stringify(kept).includes('127.0.0.1'), 'nothing about who');
    assert.deepEqual(await (await get('/api/stats/ro.other.living')).json(), { total: 2, versions: { '1.0.0': 2 } });
  });
});

describe('the store: signing in through the forum', () => {
  it('refuses an answer with a wrong signature, an unknown nonce, or one used twice', async () => {
    const forged = forumAnswer({ secret: 'another secret', nonce: 'n', user: { forumId: '1', username: 'eve' } });
    assert.equal((await get(`/auth/callback?${new URLSearchParams(forged)}`)).status, 400);
    const unknown = forumAnswer({ secret: SECRET, nonce: 'never-asked', user: { forumId: '1', username: 'eve' } });
    assert.equal((await get(`/auth/callback?${new URLSearchParams(unknown)}`)).status, 400);
    const res = await get('/auth/login');
    const nonce = new URLSearchParams(Buffer.from(new URL(res.headers.get('location')).searchParams.get('sso'), 'base64').toString()).get('nonce');
    const ok = new URLSearchParams(forumAnswer({ secret: SECRET, nonce, user: { forumId: '1', username: 'eve' } }));
    assert.equal((await get(`/auth/callback?${ok}`)).status, 302);
    assert.equal((await get(`/auth/callback?${ok}`)).status, 400, 'a nonce is used once');
  });

  it('keeps no e-mail address; the API wants a session, and a change from the store’s own pages only', async () => {
    const read = readLogin({ secret: SECRET, ...forumAnswer({ secret: SECRET, nonce: 'n', user: { forumId: '7', username: 'ana', name: 'Ana', email: 'ana@example.org' } }) });
    assert.equal(JSON.stringify(read).includes('ana@example.org'), false);
    assert.equal((await fetch(`${base}/api/me`)).status, 401);
    const cookie = await signIn({ forumId: '7', username: 'ana', name: 'Ana Pop' });
    assert.deepEqual((await as(cookie).get('/api/me')).body, { user: { username: 'ana', name: 'Ana Pop', maintainer: false } });
    const foreign = await fetch(`${base}/api/submissions`, { method: 'POST', headers: { cookie, origin: 'https://evil.example', 'content-type': 'application/json' }, body: JSON.stringify(fields()) });
    assert.equal(foreign.status, 403);
    assert.ok(!(await readFile(join(dataDir, 'submissions', 'x'), 'utf8').catch(() => '')).includes('ana@'));
  });

  it('builds the forum’s address with the payload signed', () => {
    const { url, nonce } = loginRequest({ forum: 'https://forum.lapinbim.com/', secret: SECRET, returnUrl: `${STORE}/auth/callback`, nonce: 'abc' });
    const u = new URL(url);
    assert.equal(nonce, 'abc');
    assert.equal(u.searchParams.get('sig').length, 64);
    assert.equal(readLogin({ secret: SECRET, sso: u.searchParams.get('sso'), sig: u.searchParams.get('sig') }), null, 'a request is no user');
  });
});

describe('the store: submitting', () => {
  let ana, bob, sid;
  before(async () => {
    ana = as(await signIn({ forumId: '7', username: 'ana', name: 'Ana Pop' }));
    bob = as(await signIn({ forumId: '8', username: 'bob', name: 'Bob' }));
  });

  it('refuses fields the catalogue would refuse, and without the author terms', async () => {
    assert.match((await ana.post('/api/submissions', fields({ license: 'not a licence!' }))).body.error, /SPDX/);
    assert.match((await ana.post('/api/submissions', fields({ id: 'Ana Sofas' }))).body.error, /^id must/);
    assert.match((await ana.post('/api/submissions', fields({ acceptTerms: false }))).body.error, /author terms/);
  });

  it('refuses an id listed by another author, and a version not higher than the published one', async () => {
    assert.match((await ana.post('/api/submissions', fields({ id: 'ro.other.living' }))).body.error, /listed by another author/);
    const maintainer = as(await signIn({ forumId: '1', username: 'ionut', name: 'Ionuț' }));
    assert.match((await maintainer.post('/api/submissions', fields({ id: 'ro.other.living', version: '1.0.0' }))).body.error, /Version 1.0.0 is published/);
  });

  it('takes a submission: the file checked as Lapin will, kept, screenshots, then a pull request with the manifest', async () => {
    const r = await ana.post('/api/submissions', fields());
    assert.equal(r.status, 201);
    sid = r.body.sid;
    assert.equal((await ana.put(`/api/submissions/${sid}/package`, Buffer.from('{"not":"furniture"}'), { 'x-file-name': 'sofas.lapinlib' })).status, 400);
    assert.equal((await ana.put(`/api/submissions/${sid}/package`, Buffer.from('x'), { 'x-file-name': 'sofas.zip' })).status, 400);
    const bytes = Buffer.from(JSON.stringify(furniture));
    const up = await ana.put(`/api/submissions/${sid}/package`, bytes, { 'x-file-name': 'Ana sofas.lapinlib' });
    assert.equal(up.status, 200);
    assert.deepEqual(up.body, { file: 'Ana-sofas.lapinlib', size: bytes.length, sha256: sha256(bytes), items: ['Sofa'] });
    const png = Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), Buffer.alloc(20)]);
    assert.equal((await ana.put(`/api/submissions/${sid}/screenshots/1`, png)).status, 200);
    assert.equal((await ana.put(`/api/submissions/${sid}/screenshots/2`, Buffer.from('<svg/>'))).status, 400);
    assert.equal((await bob.post(`/api/submissions/${sid}/send`)).status, 404, 'not Bob’s');
    const sent = await ana.post(`/api/submissions/${sid}/send`);
    assert.equal(sent.status, 200);
    const manifest = JSON.parse(gh.files['extensions/ana.sofas/manifest.json'].toString());
    assert.deepEqual(manifest.author, { name: 'Ana Pop', forum: 'ana' });
    assert.deepEqual(manifest.screenshots, ['screenshot.png']);
    assert.deepEqual(manifest.package, { url: `${STORE}/packages/ana.sofas/1.0.0/Ana-sofas.lapinlib`, sha256: sha256(bytes), size: bytes.length });
    assert.deepEqual(manifest.contents, { file: 'Ana-sofas.lapinlib', items: ['Sofa'] });
    assert.deepEqual(manifest.tags, ['living', 'sofa']);
    assert.ok(gh.files['extensions/ana.sofas/screenshot.png']);
    // The file is served where the manifest says, as a download.
    const file = await get('/packages/ana.sofas/1.0.0/Ana-sofas.lapinlib');
    assert.equal(file.status, 200);
    assert.match(file.headers.get('content-disposition'), /attachment/);
    assert.equal(file.headers.get('content-type'), 'application/octet-stream');
    assert.equal(sha256(Buffer.from(await file.arrayBuffer())), sha256(bytes));
  });

  it('while it is in review: Bob cannot take its id, nor Ana send its version twice', async () => {
    assert.match((await bob.post('/api/submissions', fields())).body.error, /Another author/);
    assert.match((await ana.post('/api/submissions', fields())).body.error, /already submitted/);
  });

  it('the dashboard shows its review from the pull request; merged, Ana owns the id and Bob never can', async () => {
    let mine = (await ana.get('/api/me/submissions')).body;
    assert.equal(mine.submissions[0].status, 'open');
    assert.equal(mine.submissions[0].checks, 'running');
    gh.prs.get(1).state = 'merged';
    gh.prs.get(1).checks = 'passing';
    mine = (await ana.get('/api/me/submissions')).body;
    assert.equal(mine.submissions[0].status, 'merged');
    assert.deepEqual(JSON.parse(await readFile(join(dataDir, 'owners.json'), 'utf8'))['ana.sofas'], { forumId: '7', username: 'ana' });
    assert.match((await bob.post('/api/submissions', fields({ version: '2.0.0' }))).body.error, /Another author publishes/);
    assert.match((await ana.post('/api/submissions', fields())).body.error, /already submitted/);
  });

  it('a second submission of a new version, withdrawn: its pull request closed and its file deleted', async () => {
    const r = await ana.post('/api/submissions', fields({ version: '1.1.0' }));
    assert.equal(r.status, 201);
    await ana.put(`/api/submissions/${r.body.sid}/package`, Buffer.from(JSON.stringify(furniture)), { 'x-file-name': 's.lapinlib' });
    const sent = await ana.post(`/api/submissions/${r.body.sid}/send`);
    assert.equal((await ana.post(`/api/submissions/${r.body.sid}/withdraw`)).status, 200);
    assert.equal(gh.prs.get(sent.body.pr.number).state, 'closed');
    assert.equal(existsSync(join(dataDir, 'packages', 'ana.sofas', '1.1.0', 's.lapinlib')), false);
  });

  it('the CLI lists, counts and forgets', async () => {
    const lines = [];
    assert.equal(await run(['list', 'all'], { dataDir, log: (l) => lines.push(l) }), 0);
    assert.ok(lines.some((l) => l.includes('ana.sofas 1.0.0') && l.includes('ana')));
    lines.length = 0;
    assert.equal(await run(['forget', 'ana'], { dataDir, log: (l) => lines.push(l) }), 0);
    const left = await readFile(join(dataDir, 'submissions', `${sid}.json`), 'utf8');
    assert.ok(!left.includes('"ana"') && !left.includes('Ana Pop'), 'her name is gone from what stays');
  });
});

describe('the helpers', () => {
  it('versions, file names, pictures', () => {
    assert.ok(compareVersions('1.10.0', '1.9.0') > 0);
    assert.ok(compareVersions('1.0.0-beta', '1.0.0') < 0);
    assert.equal(cleanFileName('../../etc/pass wd.pat'), 'pass-wd.pat');
    assert.equal(cleanFileName('.hidden.pat'), 'hidden.pat');
    assert.equal(imageKind(Buffer.from('RIFF0000WEBPVP8 ')), 'webp');
    assert.equal(imageKind(Buffer.from([0xff, 0xd8, 0xff, 0xe0])), 'jpg');
    assert.equal(imageKind(Buffer.from('GIF89a')), null);
  });

  it('the GitHub client: a branch from main, the files, the pull request; its state with its checks', async () => {
    const calls = [];
    const fakeFetch = async (url, init) => {
      const path = url.replace('https://api.github.com/repos/o/r', '');
      calls.push(`${init.method} ${path}`);
      assert.equal(init.headers.authorization, 'Bearer t0k');
      const reply = (status, body) => new Response(JSON.stringify(body), { status });
      if (path === '/git/ref/heads/main') return reply(200, { object: { sha: 'abc' } });
      if (path === '/git/refs') return reply(201, {});
      if (path.startsWith('/contents/') && init.method === 'GET') return reply(path.includes('manifest') ? 200 : 404, { sha: 'old' });
      if (path.startsWith('/contents/')) { const b = JSON.parse(init.body); assert.equal(b.branch, 'store/x'); return reply(201, {}); }
      if (path === '/pulls' && init.method === 'POST') return reply(201, { number: 5, html_url: 'https://github.com/o/r/pull/5' });
      if (path === '/pulls/5') return reply(200, { state: 'open', merged_at: null, head: { sha: 'h' }, html_url: 'u' });
      if (path === '/commits/h/check-runs') return reply(200, { check_runs: [{ status: 'completed', conclusion: 'failure' }] });
      return reply(404, { message: 'Not Found' });
    };
    const c = githubClient({ token: 't0k', repo: 'o/r', fetch: fakeFetch });
    const pr = await c.openPullRequest({ branch: 'store/x', files: { 'extensions/x/manifest.json': Buffer.from('{}'), 'extensions/x/screenshot.png': Buffer.from('p') }, title: 'Add x', body: 'b', message: 'm' });
    assert.deepEqual(pr, { number: 5, url: 'https://github.com/o/r/pull/5', branch: 'store/x' });
    assert.deepEqual(await c.pullRequestState(5), { state: 'open', checks: 'failing', url: 'u' });
    assert.deepEqual(calls.slice(0, 2), ['GET /git/ref/heads/main', 'POST /git/refs']);
  });

  it('the catalogue’s rules are the catalogue’s own (a copy of lapin-extensions/tools/lib.mjs)', async () => {
    const sibling = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', 'lapin-extensions', 'tools', 'lib.mjs');
    if (!existsSync(sibling)) return; // not beside this checkout: nothing to compare
    assert.equal(await readFile(join(dirname(fileURLToPath(import.meta.url)), 'catalogue-lib.mjs'), 'utf8'), await readFile(sibling, 'utf8'), 'copy it again: cp ../lapin-extensions/tools/lib.mjs deploy/store/server/catalogue-lib.mjs');
  });
});
