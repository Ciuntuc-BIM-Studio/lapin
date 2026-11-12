/**
 * Lapin Extensions' store, store.lapinbim.com (docs/plan-extensions.md, stage 1; deploy/store/README.md). One small
 * service, no dependencies (Node 22's http, fs and crypto):
 *
 *   GET  /…                         the store's pages (lapin-extensions' site/build.mjs, published to PUBLIC_DIR/site)
 *   GET  /extensions/…              the catalogue: index.json, index.json.sig, files/<id>/<version>/<file> (PUBLIC_DIR/
 *                                   extensions, published by lapin-extensions' tools/publish.mjs); lapinbim.com/extensions/*
 *                                   is routed here too
 *   GET  /packages/<id>/<v>/<file>  the files authors uploaded (a store submission's package.url)
 *   GET  /dashboard/                the author dashboard (its script: /dashboard/dashboard.js)
 *   GET  /auth/login, /auth/callback, POST /auth/logout     sign-in with a forum.lapinbim.com account (DiscourseConnect)
 *   GET  /api/me                    who is signed in
 *   GET  /api/me/submissions        their submissions, each with its review state (from its pull request) and downloads
 *   POST /api/submissions           a new extension or version: its manifest fields → { sid }
 *   PUT  /api/submissions/<sid>/package          the package file (the raw body; its name in X-File-Name)
 *   PUT  /api/submissions/<sid>/screenshots/<n>  a screenshot (PNG, WebP or JPEG, ≤ 2 MB), n = 1…6
 *   POST /api/submissions/<sid>/send             → a pull request in Ciuntuc-BIM-Studio/lapin-extensions
 *   POST /api/submissions/<sid>/withdraw         its pull request closed, its files deleted
 *   GET  /api/submissions/<sid>                  one of theirs, to start a new version from it
 *   GET  /api/download/<id>/<version>            counts a download (no personal data) and redirects to the copy
 *   GET  /api/stats/<id>                         { total, versions } downloads
 *   GET  /api/health
 *
 * Privacy (legal/privacy.md in lapin-extensions): from the forum the store keeps the user's id, name and display name with
 * what they submit, never their e-mail; sessions live in memory only (14 days); downloads are counted per extension, version
 * and month, nothing about who; a visitor is told apart for the rate limits by a salted hash of the address Cloudflare
 * reports, in memory, for an hour at most.
 */

import { createServer } from 'node:http';
import { createHash, randomBytes } from 'node:crypto';
import { mkdir, readdir, readFile, rm, stat, writeFile } from 'node:fs/promises';
import { createReadStream } from 'node:fs';
import { basename, extname, join, normalize, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { checkPackage, KINDS, MAX_PACKAGE, validateManifest } from './catalogue-lib.mjs';
import { downloadCounter, newSid, sha256, storeData, validSid, withLock } from './data.mjs';
import { loginRequest, readLogin } from './discourse.mjs';
import { githubClient } from './github.mjs';

const HERE = fileURLToPath(new URL('.', import.meta.url));
export const MAX_SCREENSHOT = 2 * 1024 * 1024;
export const MAX_SCREENSHOTS = 6;
const MAX_JSON = 64 * 1024;
const SESSION_DAYS = 14;
const COOKIE = 'lapin_store';

const TYPES = {
  '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.json': 'application/json',
  '.svg': 'image/svg+xml', '.png': 'image/png', '.webp': 'image/webp', '.jpg': 'image/jpeg', '.ico': 'image/x-icon', '.txt': 'text/plain; charset=utf-8',
  '.sig': 'application/json',
};
const HTML_HEADERS = {
  'content-security-policy': "default-src 'self'; img-src 'self' https: data:; style-src 'self'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'",
  'x-content-type-options': 'nosniff', 'referrer-policy': 'strict-origin-when-cross-origin',
};

/** At most `limit` hits per key per hour: true when this one is over. */
export function limiter(limit, windowMs = 3600_000) {
  const hits = new Map();
  return (key) => {
    const now = Date.now();
    const recent = (hits.get(key) ?? []).filter((t) => now - t < windowMs);
    if (recent.length >= limit) { hits.set(key, recent); return true; }
    recent.push(now);
    hits.set(key, recent);
    if (hits.size > 10000) for (const [k, v] of hits) if (!v.some((t) => now - t < windowMs)) hits.delete(k);
    return false;
  };
}

/** Semver's order on x.y.z (a pre-release before its release); 0 for what is not one. */
export function compareVersions(a, b) {
  const p = (v) => /^(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?/.exec(v ?? '');
  const x = p(a), y = p(b);
  if (!x || !y) return 0;
  for (let i = 1; i <= 3; i++) if (+x[i] !== +y[i]) return +x[i] - +y[i];
  if (!x[4] !== !y[4]) return x[4] ? -1 : 1;
  return (x[4] ?? '').localeCompare(y[4] ?? '');
}

/** What a screenshot's bytes are: png, webp or jpg; null for anything else. */
export function imageKind(b) {
  if (b.length > 8 && b.subarray(0, 8).equals(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]))) return 'png';
  if (b.length > 3 && b[0] === 0xff && b[1] === 0xd8 && b[2] === 0xff) return 'jpg';
  if (b.length > 12 && b.subarray(0, 4).toString('latin1') === 'RIFF' && b.subarray(8, 12).toString('latin1') === 'WEBP') return 'webp';
  return null;
}

/** A package file's name as the store keeps it: one segment, letters, digits, dots, dashes, underscores. */
export const cleanFileName = (name) => basename(String(name ?? '')).replace(/\s+/g, '-').replace(/[^A-Za-z0-9._-]/g, '').replace(/^\.+/, '').slice(0, 100);

export function settingsFrom(env = process.env) {
  return {
    port: Number(env.PORT ?? 8080),
    dataDir: env.DATA_DIR ?? '/data',
    publicDir: env.PUBLIC_DIR ?? join(env.DATA_DIR ?? '/data', 'public'),
    storeUrl: (env.STORE_URL ?? 'https://store.lapinbim.com').replace(/\/$/, ''),
    forumUrl: (env.FORUM_URL ?? 'https://forum.lapinbim.com').replace(/\/$/, ''),
    mirrorUrl: (env.MIRROR_URL ?? 'https://lapinbim.com/extensions/files/').replace(/\/?$/, '/'),
    ssoSecret: env.STORE_SSO_SECRET ?? '',
    githubToken: env.STORE_GITHUB_TOKEN ?? '',
    githubRepo: env.STORE_GITHUB_REPO ?? 'Ciuntuc-BIM-Studio/lapin-extensions',
    // Forum names who may do what a maintainer does here (publish any extension's versions); the forum's admins too.
    maintainers: new Set((env.STORE_MAINTAINERS ?? '').split(',').map((s) => s.trim().toLowerCase()).filter(Boolean)),
    salt: env.STORE_SALT ?? randomBytes(16).toString('hex'),
    submissionsPerDay: Number(env.SUBMISSIONS_PER_DAY ?? 10),
    uploadsPerHour: Number(env.UPLOADS_PER_HOUR ?? 40),
    countedPerHour: Number(env.COUNTED_PER_HOUR ?? 60), // downloads counted per visitor per hour; past that, served but not counted
    requestsPerMinute: Number(env.REQUESTS_PER_MINUTE ?? 240), // any API call per visitor
    keepDeclinedDays: Number(env.KEEP_DECLINED_DAYS ?? 90),
    prCacheMs: 60_000, // a pull request's state read again after a minute
  };
}

/** The store service on a data folder: its request handler, its housekeeping and its stop. */
export function storeService(options = {}) {
  const s = { ...settingsFrom({}), ...options };
  const data = storeData(s.dataDir);
  const counter = downloadCounter(data, { now: s.now });
  const github = s.github ?? (s.githubToken ? githubClient({ token: s.githubToken, repo: s.githubRepo, fetch: s.fetch }) : null);
  const sessions = new Map(); // sha256(cookie) → { user, expires }
  const nonces = new Map(); // nonce → { expires, back }
  const prCache = new Map(); // number → { at, state }
  const limits = {
    api: limiter(s.requestsPerMinute, 60_000),
    login: limiter(30),
    submit: limiter(s.submissionsPerDay, 86400_000),
    upload: limiter(s.uploadsPerHour),
    counted: limiter(s.countedPerHour),
  };

  const visitorOf = (req) => {
    const ip = String(req.headers['cf-connecting-ip'] ?? '').trim();
    return ip ? createHash('sha256').update(`${s.salt}:${ip}`).digest('hex').slice(0, 16) : 'local';
  };

  // ---------------------------------------------------------------- the catalogue as published (its index on disk)
  let indexCache = { mtime: 0, list: [] };
  async function catalogue() {
    const path = join(s.publicDir, 'extensions', 'index.json');
    const info = await stat(path).catch(() => null);
    if (!info) return [];
    if (info.mtimeMs !== indexCache.mtime) {
      try { indexCache = { mtime: info.mtimeMs, list: JSON.parse(await readFile(path, 'utf8')).extensions ?? [] }; } catch { indexCache = { mtime: info.mtimeMs, list: [] }; }
    }
    return indexCache.list;
  }

  // ---------------------------------------------------------------- sessions
  const cookieOf = (req) => /(?:^|;\s*)lapin_store=([A-Za-z0-9_-]{43})/.exec(req.headers.cookie ?? '')?.[1] ?? null;
  function userOf(req) {
    const c = cookieOf(req);
    if (!c) return null;
    const key = sha256(c);
    const sess = sessions.get(key);
    if (!sess || sess.expires < Date.now()) { sessions.delete(key); return null; }
    return sess.user;
  }
  const isMaintainer = (u) => !!u && (u.admin || s.maintainers.has(u.username.toLowerCase()));

  // ---------------------------------------------------------------- answers
  const json = (res, status, body, extra = {}) => {
    res.writeHead(status, { 'content-type': 'application/json', 'cache-control': 'no-store', ...extra });
    res.end(JSON.stringify(body));
  };
  const redirect = (res, to, extra = {}) => { res.writeHead(302, { location: to, 'cache-control': 'no-store', ...extra }); res.end(); };

  async function body(req, limit) {
    const declared = Number(req.headers['content-length'] ?? 0);
    if (declared > limit) { req.resume(); return null; }
    let size = 0;
    const chunks = [];
    for await (const chunk of req) {
      size += chunk.length;
      if (size <= limit) chunks.push(chunk);
    }
    return size > limit ? null : Buffer.concat(chunks);
  }
  async function jsonBody(req) {
    const b = await body(req, MAX_JSON);
    if (!b) return { error: [413, 'The request is too large.'] };
    try {
      const o = JSON.parse(b.toString('utf8'));
      return o && typeof o === 'object' && !Array.isArray(o) ? { value: o } : { error: [400, 'Not a valid request.'] };
    } catch { return { error: [400, 'Not a valid request.'] }; }
  }

  /** A file under `root` for a URL path, never outside it; null when there is none. */
  async function fileUnder(root, urlPath) {
    let rel;
    try { rel = decodeURIComponent(urlPath); } catch { return null; }
    if (rel.includes('\0') || rel.split('/').some((p) => p === '..')) return null;
    const full = normalize(join(root, rel));
    if (full !== root && !full.startsWith(root.endsWith(sep) ? root : root + sep)) return null;
    let info = await stat(full).catch(() => null);
    if (info?.isDirectory()) {
      const index = join(full, 'index.html');
      info = await stat(index).catch(() => null);
      return info?.isFile() ? { path: index, size: info.size, dirSlash: !urlPath.endsWith('/') } : null;
    }
    return info?.isFile() ? { path: full, size: info.size } : null;
  }
  function sendFile(res, file, headers = {}, status = 200) {
    const type = TYPES[extname(file.path).toLowerCase()] ?? 'application/octet-stream';
    res.writeHead(status, { 'content-type': type, 'content-length': file.size, 'x-content-type-options': 'nosniff', ...(type.startsWith('text/html') ? HTML_HEADERS : {}), ...headers });
    createReadStream(file.path).pipe(res);
  }

  // ---------------------------------------------------------------- submissions
  /** The manifest a submission's fields make (its package filled in once uploaded). */
  function manifestOf(sub, user = sub.owner) {
    const f = sub.fields;
    const m = {
      schema: 1, id: f.id, name: f.name, summary: f.summary, ...(f.description ? { description: f.description } : {}),
      ...(sub.screenshots?.length ? { screenshots: sub.screenshots.map((x) => x.name) } : {}),
      version: f.version,
      author: { name: f.authorName || user.name || user.username, ...(f.github ? { github: f.github } : {}), ...(f.authorUrl ? { url: f.authorUrl } : {}), forum: user.username },
      license: f.license, lapin: f.lapin, kind: f.kind,
      ...(f.tags?.length ? { tags: f.tags } : {}), ...(f.languages?.length ? { languages: f.languages } : {}),
      ...(f.permissions?.length ? { permissions: f.permissions } : {}), ...(f.requires?.length ? { requires: f.requires } : {}),
      contents: { file: sub.package?.file ?? `package.${KINDS[f.kind]?.ext[0] ?? 'bin'}`, ...(f.items?.length ? { items: f.items } : sub.package?.items?.length ? { items: sub.package.items } : {}) },
      package: sub.package
        ? { url: `${s.storeUrl}/packages/${f.id}/${f.version}/${sub.package.file}`, sha256: sub.package.sha256, size: sub.package.size }
        : { url: `${s.storeUrl}/packages/${f.id}/${f.version}/pending`, sha256: '0'.repeat(64), size: 1 },
      ...(f.homepage ? { homepage: f.homepage } : {}), ...(f.forum ? { forum: f.forum } : {}),
      ...(f.kind === 'priceList' ? { indicative: true } : {}),
    };
    return m;
  }

  const list = (v, n) => (Array.isArray(v) ? v : typeof v === 'string' ? v.split(',') : []).map((x) => String(x).trim()).filter(Boolean).slice(0, n);
  const text = (v, n) => (typeof v === 'string' ? v.trim().slice(0, n) : '');
  function fieldsFrom(b) {
    return {
      id: text(b.id, 64).toLowerCase(), name: text(b.name, 80), summary: text(b.summary, 200), description: text(b.description, 8000),
      version: text(b.version, 40), license: text(b.license, 200), lapin: text(b.lapin, 80), kind: text(b.kind, 20),
      authorName: text(b.authorName, 80), github: text(b.github, 39), authorUrl: text(b.authorUrl, 300),
      tags: list(b.tags, 12), languages: list(b.languages, 12).map((l) => l.toLowerCase()), permissions: list(b.permissions, 6), requires: list(b.requires, 12),
      items: list(b.items, 200).map((i) => i.slice(0, 120)), homepage: text(b.homepage, 300), forum: text(b.forum, 300),
    };
  }

  /** Whether `user` may publish this id's versions here, and why not. */
  async function ownership(user, id, sid = null) {
    if (isMaintainer(user)) return null;
    const owner = (await data.owners())[id];
    if (owner) return owner.forumId === user.forumId ? null : 'Another author publishes this extension. Choose another id.';
    const listed = (await catalogue()).find((m) => m.id === id);
    if (listed && listed.author?.forum?.toLowerCase() !== user.username.toLowerCase()) return 'This extension is listed by another author (through GitHub). Choose another id, or ask hello@lapinbim.com.';
    const others = (await data.submissions()).filter((x) => x.sid !== sid && x.fields.id === id && x.owner.forumId !== user.forumId && ['draft', 'ready', 'open'].includes(x.status));
    return others.length ? 'Another author has a submission with this id in review. Choose another id.' : null;
  }

  async function versionProblem(id, version, sid = null) {
    const listed = (await catalogue()).find((m) => m.id === id);
    if (listed && compareVersions(version, listed.version) <= 0) return `Version ${listed.version} is published: a new version must be higher.`;
    const same = (await data.submissions()).find((x) => x.sid !== sid && x.fields.id === id && x.fields.version === version && ['draft', 'ready', 'open', 'merged'].includes(x.status));
    return same ? `Version ${version} of this extension is already submitted.` : null;
  }

  /** A submission's review state, refreshed from its pull request (at most once a minute); a merged one gives its id to its author. */
  async function refreshed(sub) {
    if (sub.status !== 'open' || !sub.pr || !github) return sub;
    const cached = prCache.get(sub.pr.number);
    let st = cached && Date.now() - cached.at < s.prCacheMs ? cached.state : null;
    if (!st) {
      try { st = await github.pullRequestState(sub.pr.number); prCache.set(sub.pr.number, { at: Date.now(), state: st }); } catch { return sub; }
    }
    if (st.state === 'open') return { ...sub, checks: st.checks };
    return withLock(s.dataDir, async () => {
      const fresh = (await data.submission(sub.sid)) ?? sub;
      if (fresh.status !== 'open') return fresh;
      const next = { ...fresh, status: st.state === 'merged' ? 'merged' : 'closed', checks: st.checks, closedAt: new Date().toISOString() };
      await data.saveSubmission(next);
      if (next.status === 'merged' && !(await data.owners())[next.fields.id]) await data.setOwner(next.fields.id, { forumId: next.owner.forumId, username: next.owner.username });
      return next;
    });
  }

  const publicView = async (sub) => ({
    sid: sub.sid, id: sub.fields.id, name: sub.fields.name, version: sub.fields.version, kind: sub.fields.kind, status: sub.status, checks: sub.checks ?? null,
    createdAt: sub.createdAt, pr: sub.pr ? { number: sub.pr.number, url: sub.pr.url } : null, package: sub.package ? { file: sub.package.file, size: sub.package.size, sha256: sub.package.sha256 } : null,
    screenshots: (sub.screenshots ?? []).map((x) => x.name), downloads: (await counter.of(sub.fields.id)).versions[sub.fields.version] ?? 0,
  });

  async function deleteFiles(sub) {
    if (sub.package) await rm(data.packagePath(sub.fields.id, sub.fields.version, sub.package.file), { force: true });
    await rm(data.screenshotDir(sub.sid), { recursive: true, force: true });
  }

  const api = {
    async me(req, res, user) { return json(res, 200, { user: { username: user.username, name: user.name, maintainer: isMaintainer(user) } }); },

    async mySubmissions(req, res, user) {
      const mine = (await data.submissions()).filter((x) => x.owner.forumId === user.forumId && x.status !== 'withdrawn');
      const views = [];
      for (const sub of mine) views.push(await publicView(await refreshed(sub)));
      const owned = Object.entries(await data.owners()).filter(([, o]) => o.forumId === user.forumId).map(([id]) => id);
      const downloads = {};
      for (const id of new Set([...owned, ...mine.map((x) => x.fields.id)])) downloads[id] = await counter.of(id);
      return json(res, 200, { submissions: views, downloads });
    },

    async create(req, res, user, b) {
      const fields = fieldsFrom(b);
      if (b.acceptTerms !== true) return json(res, 400, { error: 'Accept the author terms to publish.' });
      const errors = validateManifest(manifestOf({ fields, owner: user }, user));
      if (errors.length) return json(res, 400, { error: errors[0], errors });
      // An unsent draft of the same version (an upload that failed) is replaced by this one.
      for (const old of (await data.submissions()).filter((x) => x.owner.forumId === user.forumId && x.fields.id === fields.id && x.fields.version === fields.version && ['draft', 'ready'].includes(x.status))) {
        await withLock(s.dataDir, async () => { await deleteFiles(old); await data.removeSubmission(old.sid); });
      }
      const why = (await ownership(user, fields.id)) ?? (await versionProblem(fields.id, fields.version));
      if (why) return json(res, 409, { error: why });
      if (limits.submit(user.forumId)) return json(res, 429, { error: 'Too many submissions today; try again tomorrow.' });
      const sub = { sid: newSid(), status: 'draft', createdAt: new Date().toISOString(), owner: { forumId: user.forumId, username: user.username, name: user.name }, fields, acceptedTermsAt: new Date().toISOString() };
      await data.saveSubmission(sub);
      return json(res, 201, { sid: sub.sid });
    },

    async package(req, res, user, sub) {
      if (!['draft', 'ready'].includes(sub.status)) { req.resume(); return json(res, 409, { error: 'This submission was sent already: start a new version.' }); }
      if (limits.upload(user.forumId)) { req.resume(); return json(res, 429, { error: 'Too many uploads; try again in an hour.' }); }
      const name = cleanFileName(req.headers['x-file-name']);
      const kind = KINDS[sub.fields.kind];
      if (!name || !kind.ext.some((x) => name.toLowerCase().endsWith(`.${x}`))) { req.resume(); return json(res, 400, { error: `A ${sub.fields.kind} package is a .${kind.ext.join(' or .')} file.` }); }
      const bytes = await body(req, MAX_PACKAGE);
      if (!bytes) return json(res, 413, { error: 'An extension is at most 64 MB.' });
      if (!bytes.length) return json(res, 400, { error: 'The file is empty.' });
      let items;
      try { items = checkPackage(sub.fields.kind, bytes); } catch (e) { return json(res, 400, { error: `The file is not a ${sub.fields.kind} package Lapin takes: ${e.message}.` }); }
      return withLock(s.dataDir, async () => {
        const fresh = await data.submission(sub.sid);
        if (fresh.package) await rm(data.packagePath(fresh.fields.id, fresh.fields.version, fresh.package.file), { force: true });
        const path = data.packagePath(fresh.fields.id, fresh.fields.version, name);
        if (await stat(path).catch(() => null)) return json(res, 409, { error: 'A file of this version is already kept: raise the version.' });
        await mkdir(join(path, '..'), { recursive: true });
        await writeFile(path, bytes);
        const next = { ...fresh, status: 'ready', package: { file: name, size: bytes.length, sha256: sha256(bytes), items: items.slice(0, 200) } };
        await data.saveSubmission(next);
        return json(res, 200, { file: name, size: bytes.length, sha256: next.package.sha256, items: next.package.items });
      });
    },

    async screenshot(req, res, user, sub, n) {
      if (!['draft', 'ready'].includes(sub.status)) { req.resume(); return json(res, 409, { error: 'This submission was sent already.' }); }
      if (!(n >= 1 && n <= MAX_SCREENSHOTS)) { req.resume(); return json(res, 400, { error: `At most ${MAX_SCREENSHOTS} screenshots.` }); }
      if (limits.upload(user.forumId)) { req.resume(); return json(res, 429, { error: 'Too many uploads; try again in an hour.' }); }
      const bytes = await body(req, MAX_SCREENSHOT);
      if (!bytes) return json(res, 413, { error: 'A screenshot is at most 2 MB.' });
      const kind = imageKind(bytes);
      if (!kind) return json(res, 400, { error: 'A screenshot is a PNG, WebP or JPEG picture.' });
      const name = n === 1 ? `screenshot.${kind}` : `screenshot-${n}.${kind}`;
      return withLock(s.dataDir, async () => {
        const fresh = await data.submission(sub.sid);
        const dir = data.screenshotDir(sub.sid);
        await mkdir(dir, { recursive: true });
        for (const old of (fresh.screenshots ?? []).filter((x) => x.n === n)) await rm(join(dir, old.name), { force: true });
        await writeFile(join(dir, name), bytes);
        const screenshots = [...(fresh.screenshots ?? []).filter((x) => x.n !== n), { n, name, size: bytes.length }].sort((a, b) => a.n - b.n);
        await data.saveSubmission({ ...fresh, screenshots });
        return json(res, 200, { name });
      });
    },

    async send(req, res, user, sub) {
      if (sub.status !== 'ready') return json(res, 409, { error: sub.status === 'draft' ? 'Upload the package file first.' : 'This submission was sent already.' });
      if (!github) return json(res, 503, { error: 'The store cannot open pull requests yet (no GitHub token): try again later.' });
      const m = manifestOf(sub);
      const errors = validateManifest(m);
      if (errors.length) return json(res, 400, { error: errors[0], errors });
      const why = (await ownership(user, m.id, sub.sid)) ?? (await versionProblem(m.id, m.version, sub.sid));
      if (why) return json(res, 409, { error: why });
      const files = { [`extensions/${m.id}/manifest.json`]: Buffer.from(JSON.stringify(m, null, 2) + '\n') };
      for (const x of sub.screenshots ?? []) files[`extensions/${m.id}/${x.name}`] = await readFile(join(data.screenshotDir(sub.sid), x.name));
      const listed = (await catalogue()).some((e) => e.id === m.id);
      const title = `${listed ? 'Update' : 'Add'} ${m.id} ${m.version}`;
      const prBody = [
        `${listed ? 'A new version' : 'A new extension'}, submitted through store.lapinbim.com by forum user **${user.username}**.`,
        '', '| | |', '|---|---|', `| Name | ${m.name.replace(/\|/g, '/')} |`, `| Kind | ${m.kind} |`, `| Version | ${m.version} |`, `| Licence | ${m.license} |`,
        `| Lapin | ${m.lapin} |`, `| File | ${m.contents.file}, ${m.package.size} bytes |`, `| SHA-256 | \`${m.package.sha256}\` |`,
        '', `The package is kept by the store at ${m.package.url}. The author accepted the author terms (legal/author-terms.md) on ${sub.acceptedTermsAt.slice(0, 10)}.`,
        KINDS[m.kind].code ? '\n**A script**: merged after its code review and the maintainer\'s approval.' : '',
      ].join('\n');
      let pr;
      try {
        pr = await github.openPullRequest({ branch: `store/${m.id}-${m.version}-${sub.sid.slice(0, 6)}`, files, title, body: prBody, message: `${title} (store.lapinbim.com)` });
      } catch (e) {
        console.error(new Date().toISOString(), `pull request for ${sub.sid} failed: ${e.message}`);
        return json(res, 502, { error: 'The pull request could not be opened; please try again later.' });
      }
      const next = { ...sub, status: 'open', sentAt: new Date().toISOString(), pr };
      await withLock(s.dataDir, () => data.saveSubmission(next));
      console.log(next.sentAt, `submission ${sub.sid}: ${m.id} ${m.version} → pull request #${pr.number}`);
      return json(res, 200, { pr: { number: pr.number, url: pr.url } });
    },

    async withdraw(req, res, user, sub) {
      if (['merged', 'withdrawn'].includes(sub.status)) return json(res, 409, { error: sub.status === 'merged' ? 'It is published: to remove it, write to hello@lapinbim.com.' : 'It was withdrawn already.' });
      if (sub.status === 'open' && sub.pr && github) {
        try { await github.closePullRequest(sub.pr.number, 'Withdrawn by its author on store.lapinbim.com.'); } catch (e) { console.error(`closing #${sub.pr.number}: ${e.message}`); }
      }
      await withLock(s.dataDir, async () => { await deleteFiles(sub); await data.saveSubmission({ ...sub, status: 'withdrawn', closedAt: new Date().toISOString() }); });
      return json(res, 200, { ok: true });
    },

    async one(req, res, user, sub) {
      return json(res, 200, { ...(await publicView(sub)), fields: sub.fields });
    },
  };

  // ---------------------------------------------------------------- downloads
  async function downloadTarget(id, version) {
    if (!/^[a-z0-9]+(?:[.-][a-z0-9]+)*$/.test(id) || !/^[0-9A-Za-z.+-]{1,40}$/.test(version)) return null;
    const copy = (await readdir(join(s.publicDir, 'extensions', 'files', id, version)).catch(() => []))[0];
    if (copy) return `${s.mirrorUrl}${id}/${version}/${encodeURIComponent(copy)}`;
    const own = (await readdir(join(s.dataDir, 'packages', id, version)).catch(() => []))[0];
    return own ? `${s.storeUrl}/packages/${id}/${version}/${encodeURIComponent(own)}` : null;
  }

  // ---------------------------------------------------------------- the dashboard's page
  let dashboardHtml = null;
  const dashboard = async () => (dashboardHtml ??= await readFile(join(HERE, 'public', 'dashboard.html'), 'utf8'));

  async function handle(req, res) {
    const url = new URL(req.url ?? '/', 'http://store');
    const path = url.pathname;
    const method = req.method ?? 'GET';
    const cors = { 'access-control-allow-origin': '*' };

    // The catalogue: files for anyone (the browser edition reads them from another origin).
    if (path.startsWith('/extensions/') && (method === 'GET' || method === 'HEAD')) {
      const file = await fileUnder(join(s.publicDir, 'extensions'), path.slice('/extensions'.length));
      if (!file) return json(res, 404, { error: 'not found' }, cors);
      const index = /\/index\.json(\.sig)?$/.test(path);
      return sendFile(res, file, { ...cors, 'cache-control': index ? 'public, max-age=300' : 'public, max-age=31536000, immutable', ...(index ? {} : { 'content-disposition': 'attachment' }) });
    }
    if (path.startsWith('/packages/') && (method === 'GET' || method === 'HEAD')) {
      const file = await fileUnder(join(s.dataDir, 'packages'), path.slice('/packages'.length));
      if (!file || file.path.endsWith('index.html')) return json(res, 404, { error: 'not found' }, cors);
      // Served as a download, never as a page (a file under review is the author's, not the store's).
      return sendFile(res, { ...file }, { ...cors, 'content-type': 'application/octet-stream', 'content-disposition': `attachment; filename="${basename(file.path)}"`, 'cache-control': 'public, max-age=31536000, immutable', 'content-security-policy': "default-src 'none'; sandbox" });
    }

    if (path === '/api/health') return json(res, 200, { ok: true });

    if (path.startsWith('/api/download/') && method === 'GET') {
      const [, , , id, version] = path.split('/');
      const to = await downloadTarget(id ?? '', decodeURIComponent(version ?? ''));
      if (!to) return json(res, 404, { error: 'There is no such extension version.' }, cors);
      if (!limits.counted(`${visitorOf(req)}:${id}`)) counter.add(id, decodeURIComponent(version));
      return redirect(res, to, cors);
    }
    if (path.startsWith('/api/stats/') && method === 'GET') {
      if (limits.api(visitorOf(req))) return json(res, 429, { error: 'Too many requests.' }, cors);
      const id = path.slice('/api/stats/'.length);
      if (!/^[a-z0-9]+(?:[.-][a-z0-9]+)*$/.test(id)) return json(res, 404, { error: 'not found' }, cors);
      return json(res, 200, await counter.of(id), { ...cors, 'cache-control': 'public, max-age=300' });
    }

    // Sign-in through the forum.
    if (path === '/auth/login' && method === 'GET') {
      if (!s.ssoSecret) return json(res, 503, { error: 'Sign-in is not set up yet.' });
      if (limits.login(visitorOf(req))) return json(res, 429, { error: 'Too many sign-ins; try again in an hour.' });
      const back = /^\/[A-Za-z0-9/_.-]*$/.test(url.searchParams.get('return') ?? '') ? url.searchParams.get('return') : '/dashboard/';
      const { url: to, nonce } = loginRequest({ forum: s.forumUrl, secret: s.ssoSecret, returnUrl: `${s.storeUrl}/auth/callback` });
      const now = Date.now();
      for (const [k, v] of nonces) if (v.expires < now) nonces.delete(k);
      if (nonces.size > 5000) return json(res, 503, { error: 'Too many sign-ins at once; try again in a minute.' });
      nonces.set(nonce, { expires: now + 10 * 60_000, back });
      return redirect(res, to);
    }
    if (path === '/auth/callback' && method === 'GET') {
      const answer = s.ssoSecret ? readLogin({ secret: s.ssoSecret, sso: url.searchParams.get('sso'), sig: url.searchParams.get('sig') }) : null;
      const pending = answer && nonces.get(answer.nonce);
      if (!answer || !pending || pending.expires < Date.now()) return json(res, 400, { error: 'The sign-in could not be checked. Please try again.' });
      nonces.delete(answer.nonce);
      const cookie = randomBytes(32).toString('base64url');
      sessions.set(sha256(cookie), { user: answer.user, expires: Date.now() + SESSION_DAYS * 86400_000 });
      console.log(new Date().toISOString(), `signed in: ${answer.user.username}`);
      return redirect(res, pending.back, { 'set-cookie': `${COOKIE}=${cookie}; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age=${SESSION_DAYS * 86400}` });
    }
    if (path === '/auth/logout' && method === 'POST') {
      const c = cookieOf(req);
      if (c) sessions.delete(sha256(c));
      return json(res, 200, { ok: true }, { 'set-cookie': `${COOKIE}=; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age=0` });
    }

    if (path === '/dashboard' ) return redirect(res, '/dashboard/');
    if (path === '/dashboard/' && method === 'GET') {
      res.writeHead(200, { 'content-type': 'text/html; charset=utf-8', 'cache-control': 'no-store', ...HTML_HEADERS });
      return res.end(await dashboard());
    }
    if (path === '/dashboard/dashboard.js' && method === 'GET') {
      return sendFile(res, { path: join(HERE, 'public', 'dashboard.js'), size: (await stat(join(HERE, 'public', 'dashboard.js'))).size }, { 'cache-control': 'no-cache' });
    }

    // The signed-in author's API.
    if (path.startsWith('/api/')) {
      if (limits.api(visitorOf(req))) { req.resume(); return json(res, 429, { error: 'Too many requests; slow down.' }); }
      const user = userOf(req);
      if (!user) { req.resume(); return json(res, 401, { error: 'Sign in with your forum account.', login: '/auth/login?return=/dashboard/' }); }
      // A change comes from the store's own pages only (the cookie is SameSite=Lax; this closes the rest).
      if (method !== 'GET' && req.headers.origin !== s.storeUrl) { req.resume(); return json(res, 403, { error: 'Not from the store.' }); }
      if (path === '/api/me' && method === 'GET') return api.me(req, res, user);
      if (path === '/api/me/submissions' && method === 'GET') return api.mySubmissions(req, res, user);
      if (path === '/api/submissions' && method === 'POST') {
        const b = await jsonBody(req);
        if (b.error) return json(res, b.error[0], { error: b.error[1] });
        return api.create(req, res, user, b.value);
      }
      const m = /^\/api\/submissions\/([0-9a-f]{16})(?:\/(package|send|withdraw|screenshots\/([1-9])))?$/.exec(path);
      if (m && validSid(m[1])) {
        const sub = await data.submission(m[1]);
        if (!sub || (sub.owner.forumId !== user.forumId && !isMaintainer(user))) { req.resume(); return json(res, 404, { error: 'There is no such submission.' }); }
        if (!m[2] && method === 'GET') return api.one(req, res, user, sub);
        if (m[2] === 'package' && method === 'PUT') return api.package(req, res, user, sub);
        if (m[3] && method === 'PUT') return api.screenshot(req, res, user, sub, Number(m[3]));
        if (m[2] === 'send' && method === 'POST') { req.resume(); return api.send(req, res, user, sub); }
        if (m[2] === 'withdraw' && method === 'POST') { req.resume(); return api.withdraw(req, res, user, sub); }
      }
      req.resume();
      return json(res, 404, { error: 'not found' });
    }

    // The store's pages.
    if (method === 'GET' || method === 'HEAD') {
      const root = join(s.publicDir, 'site');
      const file = await fileUnder(root, path);
      if (file?.dirSlash) return redirect(res, `${path}/`);
      if (file) {
        const asset = path.startsWith('/assets/');
        return sendFile(res, file, { 'cache-control': asset ? 'public, max-age=3600' : 'public, max-age=300' });
      }
      const missing = await fileUnder(root, '/404.html');
      if (missing) return sendFile(res, missing, { 'cache-control': 'no-store' }, 404);
    }
    return json(res, 404, { error: 'not found' });
  }

  /** Daily: declined or withdrawn submissions forgotten after keepDeclinedDays, their files with them; open ones' state read. */
  async function housekeeping() {
    const cutoff = Date.now() - s.keepDeclinedDays * 86400_000;
    for (const sub of await data.submissions()) {
      if (sub.status === 'open') { await refreshed(sub); continue; }
      const ended = Date.parse(sub.closedAt ?? sub.createdAt ?? '');
      const stale = (['closed', 'withdrawn'].includes(sub.status) && ended < cutoff) || (['draft', 'ready'].includes(sub.status) && Date.parse(sub.createdAt) < cutoff);
      if (stale) await withLock(s.dataDir, async () => { await deleteFiles(sub); await data.removeSubmission(sub.sid); });
    }
    const now = Date.now();
    for (const [k, v] of sessions) if (v.expires < now) sessions.delete(k);
    await counter.flush();
  }

  const listener = (req, res) => {
    handle(req, res).catch((err) => {
      console.error(err);
      if (!res.headersSent) res.writeHead(500, { 'content-type': 'application/json' }).end('{"error":"The store failed; please try again."}');
    });
  };
  return { listener, housekeeping, flush: () => counter.flush(), settings: s, data, sessions };
}

// Run as the service (node server.mjs); imported (the tests), it only exports.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const service = storeService(settingsFrom());
  const { port, dataDir, ssoSecret, githubToken } = service.settings;
  if (!ssoSecret) console.warn('STORE_SSO_SECRET is not set: sign-in is off.');
  if (!githubToken) console.warn('STORE_GITHUB_TOKEN is not set: submissions cannot become pull requests.');
  const server = createServer(service.listener);
  server.requestTimeout = 10 * 60_000; // a 64 MB upload on a slow line
  server.listen(port, () => console.log(`store on :${port} → ${dataDir}`));
  setInterval(() => service.flush().catch((e) => console.error('flush failed', e)), 60_000).unref();
  const daily = () => service.housekeeping().catch((e) => console.error('housekeeping failed', e));
  void daily();
  setInterval(daily, 86400_000).unref();
  for (const sig of ['SIGTERM', 'SIGINT']) process.on(sig, () => { server.close(); service.flush().finally(() => process.exit(0)); });
}
