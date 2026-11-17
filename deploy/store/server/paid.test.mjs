// Paid extensions in the store (docs/plan-extensions.md §12, stage 2a), the providers faked: node --test deploy/store/server/
import { after, before, describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { storeService } from './server.mjs';
import { checkLicence, isKeyShaped, PROVIDER_URLS, readGumroad } from './providers.mjs';
import { forumAnswer } from './discourse.mjs';
import { sha256 } from './data.mjs';

const STORE = 'https://store.lapinbim.com';
const SECRET = 'a-test-secret-of-the-forum-and-the-store';
const pat = Buffer.from('*BRICK, brick\n0, 0,0, 0,6.3\n');
const KEYS = { two: 'GR-TWO-0001', gr: 'GR-GOOD-0001', grRefunded: 'GR-REFUNDED', grOff: 'GR-DISABLED' };
/** Which product each key was sold for (Gumroad scopes a key to its product). */
const PRODUCT_OF = { [KEYS.two]: 'prod-two', [KEYS.gr]: 'prod-gr', [KEYS.grRefunded]: 'prod-gr', [KEYS.grOff]: 'prod-gr' };
const paidManifest = (id, purchase, over = {}) => ({
  schema: 1, id, name: id, summary: 'x', version: '1.0.0', author: { name: 'Ana', forum: 'ana' }, license: 'LicenseRef-Ana-EULA', lapin: '*', kind: 'hatches',
  contents: { file: 'brick.pat' }, package: { url: `${STORE}/api/licensed/${id}/1.0.0`, sha256: sha256(pat), size: pat.length }, purchase, ...over,
});
const two = paidManifest('ana.two', { provider: 'gumroad', productId: 'prod-two', checkoutUrl: 'https://ana.gumroad.com/l/two', terms: 'https://ana.example/terms' });
const gr = paidManifest('ana.gr', { provider: 'gumroad', productId: 'prod-gr', checkoutUrl: 'https://ana.gumroad.com/l/x', terms: 'https://ana.example/terms' });
const missing = paidManifest('ana.nofile', { provider: 'gumroad', productId: 'prod-gr', checkoutUrl: 'https://ana.gumroad.com/l/x', terms: 'https://ana.example/terms' });
const free = { schema: 1, id: 'ana.free', name: 'Free', summary: 'x', version: '1.0.0', author: { name: 'Ana' }, license: 'MIT', lapin: '*', kind: 'hatches', contents: { file: 'f.pat' }, package: { url: 'https://example.org/f.pat', sha256: 'a'.repeat(64), size: 3 } };

/** The providers as their public docs describe them; `down` makes them answer 503, `slow` never answer. */
const providers = { calls: [], down: false, slow: false };
async function fakeProviders(url, init) {
  const form = new URLSearchParams(init.body);
  providers.calls.push({ url, form: Object.fromEntries(form) });
  if (providers.slow) return new Promise((_, reject) => init.signal.addEventListener('abort', () => reject(init.signal.reason)));
  if (providers.down) return new Response('busy', { status: 503 });
  const reply = (status, body) => new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });
  if (url === 'https://api.gumroad.com/v2/licenses/verify') {
    assert.equal(form.get('increment_uses_count'), 'false', 'a check never counts a use');
    const key = form.get('license_key');
    if (PRODUCT_OF[key] && PRODUCT_OF[key] !== form.get('product_id')) return reply(404, { success: false, message: 'That license does not exist for the provided product.' });
    const purchase = (o) => ({ product_id: form.get('product_id'), refunded: false, chargebacked: false, disputed: false, dispute_won: false, subscription_ended_at: null, subscription_cancelled_at: null, subscription_failed_at: null, license_key: key, ...o });
    if (key === KEYS.gr || key === KEYS.two) return reply(200, { success: true, uses: 0, purchase: purchase({}) });
    if (key === KEYS.grRefunded) return reply(200, { success: true, uses: 0, purchase: purchase({ refunded: true }) });
    if (key === KEYS.grOff) return reply(404, { success: false, message: 'This license key has been disabled.' });
    return reply(404, { success: false, message: 'That license does not exist for the provided product.' });
  }
  return reply(404, {});
}

let dataDir, server, base, service;
const lines = [];
const origLog = console.log, origErr = console.error;

before(async () => {
  dataDir = await mkdtemp(join(tmpdir(), 'lapin-store-paid-'));
  const pub = join(dataDir, 'public');
  await mkdir(join(pub, 'extensions'), { recursive: true });
  await writeFile(join(pub, 'extensions', 'index.json'), JSON.stringify({ format: 'lapin-extensions-index', version: 1, extensions: [two, gr, missing, free] }));
  for (const m of [two, gr]) {
    await mkdir(join(dataDir, 'paid', m.id, '1.0.0'), { recursive: true });
    await writeFile(join(dataDir, 'paid', m.id, '1.0.0', 'brick.pat'), pat);
  }
  service = storeService({ dataDir, publicDir: pub, storeUrl: STORE, ssoSecret: SECRET, salt: 'test', providerFetch: fakeProviders, licenceChecksPerHour: 1000, github: { async openPullRequest() { return { number: 1, url: 'u', branch: 'b' }; }, async pullRequestState() { return { state: 'open' }; } } });
  server = createServer(service.listener);
  await new Promise((r) => server.listen(0, '127.0.0.1', r));
  base = `http://127.0.0.1:${server.address().port}`;
  console.log = (...a) => lines.push(a.join(' '));
  console.error = (...a) => lines.push(a.join(' '));
});
after(async () => {
  console.log = origLog; console.error = origErr;
  await new Promise((r) => server.close(r));
  await rm(dataDir, { recursive: true, force: true });
});

const post = (path, body, headers = {}) => fetch(`${base}${path}`, { method: 'POST', headers: { 'content-type': 'application/json', 'cf-connecting-ip': '203.0.113.7', ...headers }, body: JSON.stringify(body) });
const check = async (id, key) => { const r = await post('/api/licence/check', { id, key }); return { http: r.status, ...(await r.json()) }; };

describe('a licence key checked with the seller’s provider', () => {
  it('a key is good only for the product it was sold for', async () => {
    assert.equal((await check('ana.two', KEYS.two)).status, 'active');
    assert.equal((await check('ana.two', KEYS.gr)).status, 'invalid', 'another product’s key');
    assert.equal((await check('ana.gr', KEYS.two)).status, 'invalid');
  });

  it('Gumroad: good, refunded, disabled, unknown; never counted as a use', async () => {
    assert.equal((await check('ana.gr', KEYS.gr)).status, 'active');
    assert.equal((await check('ana.gr', KEYS.grRefunded)).status, 'revoked');
    assert.equal((await check('ana.gr', KEYS.grOff)).status, 'revoked');
    assert.equal((await check('ana.gr', 'NOPE')).status, 'invalid');
    assert.equal(providers.calls.at(-1).form.product_id, 'prod-gr');
  });

  it('a provider down says unavailable, concluding nothing', async () => {
    providers.down = true;
    try { assert.equal((await check('ana.gr', KEYS.gr)).status, 'unavailable'); } finally { providers.down = false; }
  });

  it('refuses what is not a paid extension of the catalogue, or not a key, before asking anyone', async () => {
    const n = providers.calls.length;
    assert.equal((await check('ana.free', KEYS.gr)).http, 404);
    assert.equal((await check('nobody.here', KEYS.gr)).http, 404);
    assert.equal((await check('ana.gr', 'two words')).http, 400);
    assert.equal((await check('ana.gr', 'x'.repeat(201))).http, 400);
    assert.equal(providers.calls.length, n);
    assert.equal((await fetch(`${base}/api/licence/check?key=${KEYS.gr}`)).status, 405, 'never a key in an address');
  });
});

describe('the paid package', () => {
  it('is given to a good key only, the very file the index names', async () => {
    const r = await post('/api/licensed/ana.two/1.0.0', { key: KEYS.two });
    assert.equal(r.status, 200);
    assert.equal(r.headers.get('content-type'), 'application/octet-stream');
    assert.equal(r.headers.get('cache-control'), 'no-store');
    assert.equal(r.headers.get('access-control-allow-origin'), '*');
    assert.deepEqual(Buffer.from(await r.arrayBuffer()), pat);
    assert.equal((await post('/api/licensed/ana.two/1.0.0', { key: KEYS.gr })).status, 403);
    const revoked = await post('/api/licensed/ana.gr/1.0.0', { key: KEYS.grRefunded });
    assert.equal(revoked.status, 403);
    assert.equal((await revoked.json()).status, 'revoked');
    assert.equal((await post('/api/licensed/ana.gr/0.9.0', { key: KEYS.gr })).status, 404, 'the version the catalogue gives only');
    assert.equal((await post('/api/licensed/ana.nofile/1.0.0', { key: KEYS.gr })).status, 404, 'a good key, but no file kept yet');
  });

  it('never gives a kept file that is not the index’s', async () => {
    await writeFile(join(dataDir, 'paid', 'ana.gr', '1.0.0', 'brick.pat'), 'changed');
    try { assert.equal((await post('/api/licensed/ana.gr/1.0.0', { key: KEYS.gr })).status, 404); } finally { await writeFile(join(dataDir, 'paid', 'ana.gr', '1.0.0', 'brick.pat'), pat); }
    assert.equal((await post('/api/licensed/ana.gr/1.0.0', { key: KEYS.gr })).status, 200);
  });

  it('has no public path: not under /packages, /extensions/files or the download counter', async () => {
    for (const p of ['/packages/ana.two/1.0.0/brick.pat', '/extensions/files/ana.two/1.0.0/brick.pat', '/paid/ana.two/1.0.0/brick.pat', '/api/download/ana.two/1.0.0']) {
      assert.notEqual((await fetch(`${base}${p}`, { redirect: 'manual' })).status, 200, p);
      assert.notEqual((await fetch(`${base}${p}`, { redirect: 'manual' })).status, 302, p);
    }
  });

  it('answers the browser edition’s preflight', async () => {
    const r = await fetch(`${base}/api/licensed/ana.two/1.0.0`, { method: 'OPTIONS' });
    assert.equal(r.status, 204);
    assert.match(r.headers.get('access-control-allow-methods'), /POST/);
    assert.match(r.headers.get('access-control-allow-headers'), /content-type/);
  });

  it('logs the extension, the provider and the answer: never a key, never an address', () => {
    const log = lines.join('\n');
    assert.match(log, /licence ana\.two gumroad: active/);
    assert.match(log, /licence ana\.gr gumroad: revoked/);
    for (const k of [...Object.values(KEYS), 'NOPE']) assert.ok(!log.includes(k), `the key ${k} is not in the log`);
    assert.ok(!log.includes('203.0.113.7'), 'no address');
    assert.ok(!existsSync(join(dataDir, 'licences.json')));
  });
});

describe('the limits', () => {
  it('a visitor checks so many keys an hour; the providers are asked so many times a minute', async () => {
    const dir = await mkdtemp(join(tmpdir(), 'lapin-store-limits-'));
    await mkdir(join(dir, 'public', 'extensions'), { recursive: true });
    await writeFile(join(dir, 'public', 'extensions', 'index.json'), JSON.stringify({ format: 'lapin-extensions-index', version: 1, extensions: [gr] }));
    const svc = storeService({ dataDir: dir, publicDir: join(dir, 'public'), storeUrl: STORE, salt: 't', providerFetch: fakeProviders, licenceChecksPerHour: 2, providerCallsPerMinute: 3 });
    const srv = createServer(svc.listener);
    await new Promise((r) => srv.listen(0, '127.0.0.1', r));
    const at = `http://127.0.0.1:${srv.address().port}`;
    const ask = (ip) => fetch(`${at}/api/licence/check`, { method: 'POST', headers: { 'content-type': 'application/json', 'cf-connecting-ip': ip }, body: JSON.stringify({ id: 'ana.gr', key: KEYS.gr }) });
    try {
      assert.equal((await ask('198.51.100.1')).status, 200);
      assert.equal((await ask('198.51.100.1')).status, 200);
      assert.equal((await ask('198.51.100.1')).status, 429, 'this visitor’s hour is used up');
      assert.equal((await ask('198.51.100.2')).status, 200);
      const over = await ask('198.51.100.3');
      assert.equal(over.status, 503, 'the providers’ minute is used up');
      assert.equal((await over.json()).status, 'unavailable');
    } finally { await new Promise((r) => srv.close(r)); await rm(dir, { recursive: true, force: true }); }
  });
});

describe('a paid submission through the dashboard', () => {
  it('keeps its file in paid/, never under /packages, and its manifest names the licensed address', async () => {
    const res = await fetch(`${base}/auth/login`, { redirect: 'manual' });
    const nonce = new URLSearchParams(Buffer.from(new URL(res.headers.get('location')).searchParams.get('sso'), 'base64').toString()).get('nonce');
    const back = await fetch(`${base}/auth/callback?${new URLSearchParams(forumAnswer({ secret: SECRET, nonce, user: { forumId: '7', username: 'ana', name: 'Ana' } }))}`, { redirect: 'manual' });
    const cookie = /lapin_store=([^;]+)/.exec(back.headers.get('set-cookie'))[0];
    const h = { cookie, origin: STORE, 'content-type': 'application/json' };
    const purchase = { provider: 'gumroad', productId: 'p-1', checkoutUrl: 'https://ana.gumroad.com/l/b', terms: 'https://ana.example/terms', price: '9 EUR' };
    const bad = await fetch(`${base}/api/submissions`, { method: 'POST', headers: h, body: JSON.stringify({ id: 'ana.bricks', kind: 'hatches', name: 'B', version: '1.0.0', summary: 's', license: 'LicenseRef-Ana-EULA', lapin: '*', acceptTerms: true, purchase: { ...purchase, provider: 'paypal' } }) });
    assert.match((await bad.json()).error, /purchase.provider/);
    const r = await fetch(`${base}/api/submissions`, { method: 'POST', headers: h, body: JSON.stringify({ id: 'ana.bricks', kind: 'hatches', name: 'B', version: '1.0.0', summary: 's', license: 'LicenseRef-Ana-EULA', lapin: '*', acceptTerms: true, purchase }) });
    assert.equal(r.status, 201);
    const { sid } = await r.json();
    const up = await fetch(`${base}/api/submissions/${sid}/package`, { method: 'PUT', headers: { cookie, origin: STORE, 'x-file-name': 'bricks.pat' }, body: pat });
    assert.equal(up.status, 200);
    assert.ok(existsSync(join(dataDir, 'paid', 'ana.bricks', '1.0.0', 'bricks.pat')));
    assert.ok(!existsSync(join(dataDir, 'packages', 'ana.bricks')));
    assert.equal((await fetch(`${base}/packages/ana.bricks/1.0.0/bricks.pat`)).status, 404);
    const one = await (await fetch(`${base}/api/submissions/${sid}`, { headers: { cookie } })).json();
    assert.deepEqual(one.fields.purchase, purchase);
  });
});

describe('the providers’ answers read', () => {
  it('Gumroad only: Lemon Squeezy is closing (decision 660)', async () => {
    assert.deepEqual(Object.keys(PROVIDER_URLS), ['gumroad']);
    assert.equal((await checkLicence({ provider: 'lemonsqueezy', productId: '42' }, KEYS.gr, { fetch: fakeProviders })).status, 'unavailable');
  });

  it('Gumroad', () => {
    const ok = (p) => readGumroad({ status: 200, body: { success: true, purchase: { product_id: 'p', ...p } } }, 'p', Date.parse('2026-11-17T00:00:00Z')).status;
    assert.equal(ok({}), 'active');
    assert.equal(ok({ chargebacked: true }), 'revoked');
    assert.equal(ok({ disputed: true }), 'revoked');
    assert.equal(ok({ disputed: true, dispute_won: true }), 'active');
    assert.equal(ok({ subscription_cancelled_at: '2026-11-01T00:00:00Z' }), 'active', 'cancelled, running to its end');
    assert.equal(ok({ subscription_ended_at: '2026-11-01T00:00:00Z' }), 'revoked');
    assert.equal(ok({ subscription_ended_at: '2026-12-01T00:00:00Z' }), 'active', 'ends later');
    assert.equal(ok({ subscription_failed_at: '2026-11-01T00:00:00Z' }), 'revoked');
    assert.equal(ok({ product_id: 'other' }), 'invalid');
  });

  it('a provider that does not answer in time is unavailable; a key with spaces is no key', async () => {
    providers.slow = true;
    try { assert.equal((await checkLicence({ provider: 'gumroad', productId: 'p' }, KEYS.gr, { fetch: fakeProviders, timeoutMs: 50 })).status, 'unavailable'); } finally { providers.slow = false; }
    assert.equal((await checkLicence({ provider: 'gumroad', productId: 'p' }, KEYS.gr, { fetch: async () => { throw new TypeError('fetch failed'); } })).status, 'unavailable');
    assert.equal(isKeyShaped('A1B2C3D4-E5F60718-9ABCDEF0-1234ABCD'), true);
    assert.equal(isKeyShaped('two words'), false);
    assert.equal(isKeyShaped(''), false);
  });
});
