/**
 * Lapin's "Report an issue" form, server side (docs/decisions-pending.md 177,
 * deploy/feedback/server/README.md). Nothing leaves the server: reports are
 * read, answered and closed from the team's machine through `cli.mjs`
 * (deploy/feedback/reports.sh).
 *
 *   POST /api/feedback           a report → open/<n>.json (its screenshot beside it); { number }.
 *                                With `contact: { nickname?, keyId }` the sender may be answered:
 *                                the report gets a thread tied to keyId, the SHA-256 of a secret
 *                                that never leaves their device.
 *   POST /api/feedback/inbox     { secret } → that key's reports with their threads and unread counts.
 *   POST /api/feedback/seen      { secret, report, upTo } → the first upTo messages are read.
 *   POST /api/feedback/message   { secret, report, text } → the sender's answer (reopens a closed report).
 *   POST /api/feedback/nickname  { secret, nickname } → the name shown on all that key's reports.
 *   POST /api/feedback/forget    { secret } → nickname and key removed from all that key's reports.
 *   GET  /api/feedback/health
 *
 * Secrets come only in POST bodies (never in a URL, so never in a log) and
 * are only hashed, never kept. Guards: body limits (and nginx's rate limits
 * before them), a honeypot field, a per-visitor limit on reports, a per-key
 * limit on messages, a cap on the reports kept open, a screenshot that must
 * really be a PNG or JPEG.
 *
 * Privacy: the visitor is told apart by a salted hash of the address
 * Cloudflare reports (CF-Connecting-IP), never the address itself; an e-mail
 * is kept only with the sender's consent; closed reports are deleted after
 * RETENTION_DAYS (default 365); a thread idle for REPLY_MONTHS (default 12)
 * loses its nickname and key (`cli.mjs prune`, also run daily here).
 *
 * No dependencies: Node 22's http, fs and crypto.
 */

import { createServer } from 'node:http';
import { createHash, randomBytes } from 'node:crypto';
import { mkdir, readdir, rm, stat, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import {
  allReports, findReport, forgetThread, moveReport, pruneThreads, repliable, saveReport, sha256, unreadByUser,
  validKeyId, validSecret, withLock, writeAtomic,
} from './store.mjs';

const KINDS = new Set(['bug', 'idea', 'question']);
const MAX_BODY = 4 * 1024 * 1024; // a screenshot included
const MAX_SMALL_BODY = 16 * 1024; // inbox, seen, message, nickname, forget
const MAX_SHOT = 3 * 1024 * 1024;
export const MAX_TEXT = 4000; // one message
export const MAX_MESSAGES = 200; // one thread
export const MAX_NICKNAME = 40;

const clip = (v, n) => (typeof v === 'string' ? v.trim().slice(0, n) : '');
/** A nickname: one line, no control characters, at most MAX_NICKNAME characters; '' when none. */
export const cleanNickname = (v) => clip(typeof v === 'string' ? v.replace(/[\p{Cc}\p{Cf}]/gu, ' ').replace(/\s+/g, ' ') : '', MAX_NICKNAME);

/** Whether the bytes are what the data URL claims: a PNG or a JPEG. */
const isImage = (bytes, type) => (type === 'png'
  ? bytes.length > 8 && bytes.subarray(0, 8).equals(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]))
  : bytes.length > 3 && bytes[0] === 0xff && bytes[1] === 0xd8 && bytes[2] === 0xff);

/** At most `limit` hits per key in the last hour: true when this one is over. */
function limiter(limit) {
  const hits = new Map();
  return (key) => {
    const now = Date.now();
    const recent = (hits.get(key) ?? []).filter((t) => now - t < 3600_000);
    if (recent.length >= limit) return true;
    hits.set(key, [...recent, now]);
    if (hits.size > 5000) for (const [k, v] of hits) if (!v.some((t) => now - t < 3600_000)) hits.delete(k);
    return false;
  };
}

/** The settings, from the environment unless given. */
export function settingsFrom(env = process.env) {
  return {
    port: Number(env.PORT ?? 8080),
    dataDir: env.DATA_DIR ?? '/data',
    maxOpen: Number(env.MAX_OPEN ?? 2000),
    apps: new Set((env.APPS ?? 'lapin').split(',').map((s) => s.trim()).filter(Boolean)),
    rateLimit: Number(env.RATE_LIMIT ?? 6), // reports per visitor per hour
    messageLimit: Number(env.MESSAGE_LIMIT ?? 30), // messages and nickname changes per key per hour
    retentionDays: Number(env.RETENTION_DAYS ?? 365),
    replyMonths: Number(env.REPLY_MONTHS ?? 12),
    // A fixed salt keeps one visitor's reports together across restarts; without one, per process.
    salt: env.FEEDBACK_SALT ?? randomBytes(16).toString('hex'),
  };
}

/** The feedback service on a data folder: its request handler and its daily housekeeping. */
export function feedbackService(options = {}) {
  const s = { ...settingsFrom({}), ...options };
  const DATA_DIR = s.dataDir;
  const reportLimited = limiter(s.rateLimit);
  const messageLimited = limiter(s.messageLimit);

  /** Who sent it, as far as the limit and the triage need: a salted hash of Cloudflare's visitor address. */
  const visitorOf = (req) => {
    const ip = String(req.headers['cf-connecting-ip'] ?? '').trim();
    return ip ? createHash('sha256').update(`${s.salt}:${ip}`).digest('hex').slice(0, 16) : 'local';
  };

  /** Delete closed reports (and their screenshots) older than retentionDays; anonymise idle threads. */
  async function housekeeping() {
    const dir = join(DATA_DIR, 'closed');
    const cutoff = Date.now() - s.retentionDays * 86400_000;
    for (const name of await readdir(dir).catch(() => [])) {
      const path = join(dir, name);
      const info = await stat(path).catch(() => null);
      if (info && info.mtimeMs < cutoff) await rm(path, { force: true });
    }
    await pruneThreads(DATA_DIR, s.replyMonths);
  }

  /** The next report number (one past the highest kept, open or closed). */
  async function nextNumber() {
    let max = 0;
    for (const dir of ['open', 'closed']) {
      for (const name of await readdir(join(DATA_DIR, dir)).catch(() => [])) {
        const n = Number(/^(\d+)\.json$/.exec(name)?.[1] ?? 0);
        if (n > max) max = n;
      }
    }
    return max + 1;
  }

  /** Keep report `r` as open/<n>.json (and its screenshot as open/<n>.<ext>); its number. Under the lock, so numbers never clash. */
  const keep = (r, app, kind, title, visitor, contact) => withLock(DATA_DIR, async () => {
    await mkdir(join(DATA_DIR, 'open'), { recursive: true });
    await mkdir(join(DATA_DIR, 'closed'), { recursive: true });
    if ((await readdir(join(DATA_DIR, 'open'))).filter((n) => n.endsWith('.json')).length >= s.maxOpen) throw new Error('too many open reports');
    const number = await nextNumber();
    const ctx = r.context && typeof r.context === 'object' ? r.context : {};
    let screenshot = null;
    const m = /^data:image\/(png|jpeg);base64,([A-Za-z0-9+/=]+)$/.exec(typeof r.screenshot === 'string' ? r.screenshot : '');
    const shot = m && m[2].length * 0.75 <= MAX_SHOT ? Buffer.from(m[2], 'base64') : null;
    if (m && shot && isImage(shot, m[1])) {
      screenshot = `${number}.${m[1] === 'jpeg' ? 'jpg' : 'png'}`;
      await writeFile(join(DATA_DIR, 'open', screenshot), shot);
    }
    const now = new Date().toISOString();
    const report = {
      number, receivedAt: now, app, kind, title,
      description: clip(r.description, 8000), expected: clip(r.expected, 4000) || undefined,
      email: clip(r.email, 200) && r.consent === true ? clip(r.email, 200) : undefined,
      screenshot: screenshot ?? undefined,
      context: {
        version: clip(ctx.version, 80), url: clip(ctx.url, 300), userAgent: clip(ctx.userAgent, 300), screen: clip(ctx.screen, 40),
        errors: Array.isArray(ctx.errors) ? ctx.errors.slice(-20).map((e) => clip(e, 400)).filter(Boolean) : [],
      },
      visitor,
      thread: contact
        ? { nickname: contact.nickname || undefined, keyId: contact.keyId, messages: [], userSeen: 0, lapinSeen: 0, lastActivity: now }
        : undefined,
    };
    await writeAtomic(join(DATA_DIR, 'open', `${number}.json`), JSON.stringify(report, null, 2));
    return number;
  });

  /** The reports a key may read: theirs, with a thread still tied to the key. */
  const reportsOf = async (keyId) => (await allReports(DATA_DIR)).filter((r) => repliable(r) && r.thread.keyId === keyId);

  /** Report n if it belongs to keyId, else null. */
  async function ownReport(keyId, n) {
    const r = await findReport(DATA_DIR, n);
    return r && repliable(r) && r.thread.keyId === keyId ? r : null;
  }

  async function submit(r, req, send) {
    // The honeypot: people never fill it in.
    if (clip(r.website, 200)) return send(200, { ok: true });
    const app = clip(r.app, 40);
    const kind = KINDS.has(r.kind) ? r.kind : 'bug';
    const title = clip(r.title, 140);
    if (!s.apps.has(app)) return send(400, { error: 'Unknown app.' });
    if (title.length < 4 || clip(r.description, 8000).length < 10) return send(400, { error: 'Please give a title and describe what happened.' });
    let contact = null;
    if (r.contact != null) {
      if (typeof r.contact !== 'object' || !validKeyId(r.contact.keyId)) return send(400, { error: 'The reply key is not valid.' });
      contact = { keyId: r.contact.keyId, nickname: cleanNickname(r.contact.nickname) };
    }
    // Counted once the report is a real one: a rejected request does not use up the sender's reports.
    const visitor = visitorOf(req);
    if (reportLimited(visitor)) return send(429, { error: 'Too many reports from here; try again in an hour.' });
    try {
      const number = await keep(r, app, kind, title, visitor, contact);
      console.log(new Date().toISOString(), `report #${number} (${app}, ${kind}${contact ? ', replies' : ''})`);
      return send(201, { ok: true, number });
    } catch (err) {
      console.error(new Date().toISOString(), err instanceof Error ? err.message : err);
      return send(503, { error: 'The report could not be kept; please try again later.' });
    }
  }

  /** The endpoints a sender's key reaches; each is given the key's id and the body. */
  const keyed = {
    async inbox(keyId) {
      const reports = (await reportsOf(keyId)).sort((a, b) => b.number - a.number).map((r) => ({
        number: r.number, title: r.title, kind: r.kind, receivedAt: r.receivedAt, status: r.dir,
        description: r.description, messages: r.thread.messages, seen: r.thread.userSeen ?? 0, unread: unreadByUser(r),
      }));
      const latest = reports.length ? (await ownReport(keyId, reports[0].number))?.thread.nickname : undefined;
      return [200, { reports, unread: reports.reduce((n, r) => n + r.unread, 0), nickname: latest ?? '' }];
    },
    async seen(keyId, b) {
      const upTo = Number(b.upTo);
      if (!Number.isInteger(upTo) || upTo < 0) return [400, { error: 'Which messages were read?' }];
      return withLock(DATA_DIR, async () => {
        const r = await ownReport(keyId, b.report);
        if (!r) return [404, { error: 'There is no such report.' }];
        const seen = Math.max(r.thread.userSeen ?? 0, Math.min(upTo, r.thread.messages.length));
        if (seen !== r.thread.userSeen) await saveReport(DATA_DIR, { ...r, thread: { ...r.thread, userSeen: seen } });
        return [200, { ok: true, seen }];
      });
    },
    async message(keyId, b) {
      const text = typeof b.text === 'string' ? b.text.trim() : '';
      if (!text) return [400, { error: 'The message is empty.' }];
      if (text.length > MAX_TEXT) return [413, { error: `A message is at most ${MAX_TEXT} characters.` }];
      if (messageLimited(keyId)) return [429, { error: 'Too many messages; try again in an hour.' }];
      return withLock(DATA_DIR, async () => {
        let r = await ownReport(keyId, b.report);
        if (!r) return [404, { error: 'There is no such report.' }];
        if (r.thread.messages.length >= MAX_MESSAGES) return [409, { error: 'This conversation is full; please send a new report.' }];
        const at = new Date().toISOString();
        const messages = [...r.thread.messages, { from: 'user', text, at }];
        // A message answers everything before it.
        r = { ...r, thread: { ...r.thread, messages, userSeen: messages.length, lastActivity: at } };
        if (r.dir === 'closed') { r = await moveReport(DATA_DIR, r, 'open'); r.reopenedAt = at; }
        await saveReport(DATA_DIR, r);
        console.log(at, `message on #${r.number}`);
        return [200, { ok: true, status: r.dir }];
      });
    },
    async nickname(keyId, b) {
      if (messageLimited(keyId)) return [429, { error: 'Too many changes; try again in an hour.' }];
      const nickname = cleanNickname(b.nickname);
      return withLock(DATA_DIR, async () => {
        const mine = await reportsOf(keyId);
        for (const r of mine) await saveReport(DATA_DIR, { ...r, thread: { ...r.thread, nickname: nickname || undefined } });
        return [200, { ok: true, updated: mine.length, nickname }];
      });
    },
    async forget(keyId) {
      return withLock(DATA_DIR, async () => {
        const mine = await reportsOf(keyId);
        for (const r of mine) await saveReport(DATA_DIR, { ...r, thread: forgetThread(r.thread) });
        console.log(new Date().toISOString(), `a key forgotten (${mine.length} report(s))`);
        return [200, { ok: true, forgotten: mine.length }];
      });
    },
  };

  async function handle(req, res) {
    const send = (status, body) => {
      res.writeHead(status, { 'content-type': 'application/json', 'cache-control': 'no-store' });
      res.end(JSON.stringify(body));
    };
    const path = (req.url ?? '').split('?')[0];
    if (req.method === 'GET' && path === '/api/feedback/health') return send(200, { ok: true });
    const endpoint = path === '/api/feedback' ? 'submit' : /^\/api\/feedback\/([a-z]+)$/.exec(path)?.[1];
    if (req.method !== 'POST' || !endpoint || (endpoint !== 'submit' && !Object.hasOwn(keyed, endpoint))) return send(404, { error: 'not found' });

    const limit = endpoint === 'submit' ? MAX_BODY : MAX_SMALL_BODY;
    let size = 0;
    const chunks = [];
    // Past the limit the rest is read and dropped, so the client gets the answer rather than a broken connection (nginx caps it before).
    for await (const chunk of req) {
      size += chunk.length;
      if (size <= limit) chunks.push(chunk);
    }
    if (size > limit) return send(413, { error: endpoint === 'submit' ? 'The report is too large (the screenshot, most likely).' : 'The request is too large.' });
    let body;
    try {
      body = JSON.parse(Buffer.concat(chunks).toString('utf8'));
    } catch {
      return send(400, { error: 'Not a valid request.' });
    }
    if (!body || typeof body !== 'object' || Array.isArray(body)) return send(400, { error: 'Not a valid request.' });
    if (endpoint === 'submit') return submit(body, req, send);
    if (!validSecret(body.secret)) return send(400, { error: 'The key is not valid.' });
    const [status, answer] = await keyed[endpoint](sha256(body.secret), body);
    return send(status, answer);
  }

  const listener = (req, res) => {
    handle(req, res).catch((err) => {
      console.error(err);
      if (!res.headersSent) res.writeHead(500).end();
    });
  };
  return { listener, housekeeping, settings: s };
}

// Run as the service (node server.mjs); imported (the tests), it only exports.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const service = feedbackService(settingsFrom());
  const { port, dataDir } = service.settings;
  createServer(service.listener).listen(port, () => console.log(`feedback on :${port} → ${dataDir}`));
  const daily = () => service.housekeeping().catch((err) => console.error('housekeeping failed', err));
  void daily();
  setInterval(daily, 86400_000).unref();
}
