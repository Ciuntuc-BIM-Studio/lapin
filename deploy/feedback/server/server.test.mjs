// The feedback service and its CLI on a temporary data folder: node --test deploy/feedback/server/

import { after, before, describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { randomBytes } from 'node:crypto';
import { feedbackService } from './server.mjs';
import { run } from './cli.mjs';
import { findReport, pruneThreads, saveReport, sha256 } from './store.mjs';

let dataDir, server, base;
const secret = randomBytes(32).toString('base64url');
const keyId = sha256(secret);
const other = randomBytes(32).toString('base64url');

before(async () => {
  dataDir = await mkdtemp(join(tmpdir(), 'lapin-feedback-'));
  const service = feedbackService({ dataDir, salt: 'test', rateLimit: 100, messageLimit: 5 });
  server = createServer(service.listener);
  await new Promise((r) => server.listen(0, '127.0.0.1', r));
  base = `http://127.0.0.1:${server.address().port}/api/feedback`;
});
after(async () => {
  await new Promise((r) => server.close(r));
  await rm(dataDir, { recursive: true, force: true });
});

const post = async (path, body) => {
  const res = await fetch(`${base}${path}`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: typeof body === 'string' ? body : JSON.stringify(body) });
  return { status: res.status, body: await res.json().catch(() => null) };
};
const report = (extra = {}) => ({ app: 'lapin', kind: 'bug', title: 'Walls vanish', description: 'They vanish when I zoom.', context: { version: 'lapin-pro 1' }, ...extra });
const cli = async (...args) => {
  const lines = [];
  const code = await run(args, { dataDir, log: (l) => lines.push(l) });
  return { code, out: lines.join('\n') };
};

describe('the feedback service', () => {
  let anonymous, mine, second;

  it('keeps an anonymous report as before, with no thread', async () => {
    const r = await post('', report({ email: 'a@b.ro', consent: true }));
    assert.equal(r.status, 201);
    anonymous = r.body.number;
    const kept = await findReport(dataDir, anonymous);
    assert.equal(kept.thread, undefined);
    assert.equal(kept.email, 'a@b.ro');
    assert.equal(kept.visitor, 'local');
  });

  it('keeps a report with a contact: its nickname, its key id and an empty thread; never the secret', async () => {
    const r = await post('', report({ title: 'Roof gaps', contact: { nickname: ' Ana\u0007 B ', keyId } }));
    assert.equal(r.status, 201);
    mine = r.body.number;
    const kept = await findReport(dataDir, mine);
    assert.deepEqual({ nickname: kept.thread.nickname, keyId: kept.thread.keyId, messages: kept.thread.messages }, { nickname: 'Ana B', keyId, messages: [] });
    assert.ok(!(await readFile(join(dataDir, 'open', `${mine}.json`), 'utf8')).includes(secret));
    second = (await post('', report({ title: 'Door tags', contact: { keyId } }))).body.number;
  });

  it('refuses a contact whose key id is not a SHA-256', async () => {
    assert.equal((await post('', report({ contact: { keyId: 'abc' } }))).status, 400);
  });

  it('gives a key its own reports only; a wrong secret gets none', async () => {
    const r = await post('/inbox', { secret });
    assert.equal(r.status, 200);
    assert.deepEqual(r.body.reports.map((x) => x.number), [second, mine]);
    assert.equal(r.body.unread, 0);
    assert.equal(r.body.nickname, '');
    assert.deepEqual((await post('/inbox', { secret: other })).body.reports, []);
    assert.equal((await post('/inbox', { secret: 'short' })).status, 400);
    assert.equal((await post('/inbox', {})).status, 400);
  });

  it('answers from the CLI only a report that asked for it; the sender sees it unread, then read', async () => {
    assert.equal((await cli('reply', String(anonymous), 'Thanks')).code, 1);
    const sent = await cli('reply', String(mine), 'Fixed', 'in', '0.1.900');
    assert.equal(sent.code, 0, sent.out);
    let inbox = (await post('/inbox', { secret })).body;
    const r = inbox.reports.find((x) => x.number === mine);
    assert.deepEqual(r.messages.map((m) => [m.from, m.text]), [['lapin', 'Fixed in 0.1.900']]);
    assert.equal(r.unread, 1);
    assert.equal(inbox.unread, 1);
    assert.equal((await post('/seen', { secret, report: mine, upTo: 1 })).status, 200);
    assert.equal((await post('/seen', { secret: other, report: mine, upTo: 1 })).status, 404);
    inbox = (await post('/inbox', { secret })).body;
    assert.equal(inbox.unread, 0);
  });

  it("takes the sender's answer on their own report, reopening it when closed; not on another's", async () => {
    await cli('close', String(mine));
    assert.equal((await findReport(dataDir, mine)).dir, 'closed');
    const r = await post('/message', { secret, report: mine, text: '  Still there on the first floor.  ' });
    assert.equal(r.status, 200);
    assert.equal(r.body.status, 'open');
    const kept = await findReport(dataDir, mine);
    assert.equal(kept.dir, 'open');
    assert.deepEqual(kept.thread.messages.map((m) => m.from), ['lapin', 'user']);
    assert.equal(kept.thread.messages[1].text, 'Still there on the first floor.');
    assert.equal((await post('/message', { secret: other, report: mine, text: 'hi' })).status, 404);
    assert.equal((await post('/message', { secret, report: anonymous, text: 'hi' })).status, 404);
    assert.equal((await post('/message', { secret, report: mine, text: '   ' })).status, 400);
  });

  it('marks the unread answers in the list, and reads them with show', async () => {
    let list = await cli('list');
    assert.match(list.out, new RegExp(`#${mine}\\t.*✉ Ana B ●\\tRoof gaps`));
    assert.match(list.out, new RegExp(`#${second}\\t.*✉ \\(no name\\)\\tDoor tags`));
    const shown = await cli('show', String(mine));
    assert.match(shown.out, /lapin {2}Fixed in 0\.1\.900/);
    assert.match(shown.out, /user  ● {2}Still there/);
    list = await cli('list');
    assert.doesNotMatch(list.out, /●/);
  });

  it('limits sizes: a message, a small request, a report', async () => {
    assert.equal((await post('/message', { secret, report: mine, text: 'x'.repeat(4001) })).status, 413);
    assert.equal((await post('/inbox', { secret, pad: 'x'.repeat(20_000) })).status, 413);
    assert.equal((await post('', report({ description: 'x'.repeat(5 * 1024 * 1024) }))).status, 413);
    assert.equal((await post('/inbox', 'not json')).status, 400);
    assert.equal((await post('/nothing', { secret })).status, 404);
  });

  it('limits the messages per key', async () => {
    // Five per hour in this test (the message and the refused one above used two).
    const statuses = [];
    for (let i = 0; i < 4; i++) statuses.push((await post('/message', { secret, report: second, text: `m${i}` })).status);
    assert.deepEqual(statuses, [200, 200, 200, 429]);
  });

  it('renames the sender on all their reports', async () => {
    // A fresh key: the other is at its hourly limit.
    const s2 = randomBytes(32).toString('base64url');
    const n = (await post('', report({ contact: { keyId: sha256(s2), nickname: 'Old' } }))).body.number;
    const r = await post('/nickname', { secret: s2, nickname: 'New name' });
    assert.equal(r.body.updated, 1);
    assert.equal((await findReport(dataDir, n)).thread.nickname, 'New name');
  });

  it('forgets a key: nickname and key gone from all its reports, the reports and messages kept', async () => {
    const r = await post('/forget', { secret });
    assert.equal(r.body.forgotten, 2);
    const kept = await findReport(dataDir, mine);
    assert.equal(kept.title, 'Roof gaps');
    assert.equal(kept.thread.keyId, undefined);
    assert.equal(kept.thread.nickname, undefined);
    assert.ok(kept.thread.forgotten);
    assert.equal(kept.thread.messages.length, 2);
    assert.deepEqual((await post('/inbox', { secret })).body.reports, []);
    assert.equal((await cli('reply', String(mine), 'Hello?')).code, 1);
    assert.doesNotMatch((await cli('list')).out, new RegExp(`#${mine}\\t.*✉`));
  });

  it('prunes the threads idle for more than twelve months, and only those', async () => {
    const s3 = randomBytes(32).toString('base64url');
    const idle = (await post('', report({ contact: { keyId: sha256(s3), nickname: 'Idle' } }))).body.number;
    const fresh = (await post('', report({ contact: { keyId: sha256(s3), nickname: 'Fresh' } }))).body.number;
    const r = await findReport(dataDir, idle);
    await saveReport(dataDir, { ...r, thread: { ...r.thread, lastActivity: '2025-01-01T00:00:00.000Z' } });
    const out = await cli('prune');
    assert.equal(out.code, 0);
    assert.match(out.out, /^1 thread/);
    assert.equal((await findReport(dataDir, idle)).thread.nickname, undefined);
    assert.equal((await findReport(dataDir, fresh)).thread.nickname, 'Fresh');
    assert.equal(await pruneThreads(dataDir, 12, new Date(Date.now() + 400 * 86400_000)), 2);
  });

  it('reads an older report that kept its e-mail as contact', async () => {
    await writeFile(join(dataDir, 'open', '900.json'), JSON.stringify({ number: 900, receivedAt: '2026-01-01T00:00:00Z', app: 'lapin', kind: 'bug', title: 'Old', description: 'Old one', contact: 'x@y.ro', context: {} }));
    const shown = await cli('show', '900');
    assert.match(shown.out, /"email": "x@y.ro"/);
    const refused = await cli('reply', '900', 'hi');
    assert.equal(refused.code, 1);
    assert.match(refused.out, /x@y\.ro/);
  });

  it('answers its health check', async () => {
    const res = await fetch(`${base}/health`);
    assert.equal(res.status, 200);
  });
});
