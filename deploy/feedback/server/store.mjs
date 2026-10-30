/**
 * The kept reports, as files: DATA_DIR/open/<n>.json and DATA_DIR/closed/<n>.json,
 * a screenshot beside each (<n>.png or <n>.jpg). Shared by the server and the
 * CLI, which run as two processes on the same folder: every change is made
 * under one lock (a directory, DATA_DIR/.lock) and written atomically (a
 * temporary file renamed over the old one).
 *
 * A report whose sender asked for a reply carries a thread:
 *   thread: { nickname?, keyId, messages: [{ from: 'lapin' | 'user', text, at }],
 *             userSeen, lapinSeen, lastActivity }
 * keyId is the SHA-256 (hex) of the sender's secret, which stays on their
 * device; userSeen / lapinSeen count the messages each side has read. A
 * thread forgotten (by its sender, or idle past the retention) keeps its
 * messages but loses nickname and keyId: { forgotten: <date>, messages }.
 *
 * No dependencies: Node 22's fs and crypto.
 */

import { createHash, randomBytes } from 'node:crypto';
import { mkdir, readdir, readFile, rename, rm, stat, writeFile } from 'node:fs/promises';
import { join } from 'node:path';

export const DIRS = ['open', 'closed'];

/** SHA-256 of a text's UTF-8 bytes, in hex: a key's id is the hash of its secret (the base64url text). */
export const sha256 = (text) => createHash('sha256').update(text, 'utf8').digest('hex');

/** A secret as the app makes it: 32 random bytes in base64url (43 characters); a little room either way. */
export const validSecret = (s) => typeof s === 'string' && /^[A-Za-z0-9_-]{32,128}$/.test(s);
export const validKeyId = (s) => typeof s === 'string' && /^[0-9a-f]{64}$/.test(s);

/** Write a file whole or not at all: a temporary file beside it, renamed over it. */
export async function writeAtomic(path, data) {
  const tmp = `${path}.tmp-${randomBytes(6).toString('hex')}`;
  await writeFile(tmp, data);
  await rename(tmp, path);
}

/** Run `fn` holding the data folder's lock (a directory: mkdir is atomic); a lock older than 30 s is a crashed holder's. */
export async function withLock(dataDir, fn) {
  const lock = join(dataDir, '.lock');
  await mkdir(dataDir, { recursive: true });
  for (let i = 0; ; i++) {
    try { await mkdir(lock); break; } catch (e) {
      if (e.code !== 'EEXIST') throw e;
      const info = await stat(lock).catch(() => null);
      if (info && Date.now() - info.mtimeMs > 30_000) { await rm(lock, { recursive: true, force: true }); continue; }
      if (i > 400) throw new Error('the data folder stays locked');
      await new Promise((r) => setTimeout(r, 25));
    }
  }
  try { return await fn(); } finally { await rm(lock, { recursive: true, force: true }); }
}

/** Every report in a folder (open or closed), with `dir` set, by number. */
export async function reportsIn(dataDir, dir) {
  const names = await readdir(join(dataDir, dir)).catch(() => []);
  const out = [];
  for (const name of names.filter((n) => /^\d+\.json$/.test(n))) {
    try { out.push({ ...JSON.parse(await readFile(join(dataDir, dir, name), 'utf8')), dir }); } catch { /* unreadable: left out */ }
  }
  return out.sort((a, b) => a.number - b.number);
}

export const allReports = async (dataDir) => (await Promise.all(DIRS.map((d) => reportsIn(dataDir, d)))).flat();

/** Report n, wherever it is (open or closed), with `dir` set; null when there is none. */
export async function findReport(dataDir, n) {
  if (!Number.isInteger(Number(n)) || Number(n) < 1) return null;
  for (const dir of DIRS) {
    try { return { ...JSON.parse(await readFile(join(dataDir, dir, `${Number(n)}.json`), 'utf8')), dir }; } catch { /* not there */ }
  }
  return null;
}

/** Write a report back where its `dir` says (`dir` itself is not kept in the file). */
export async function saveReport(dataDir, report) {
  const { dir, ...kept } = report;
  await writeAtomic(join(dataDir, dir, `${report.number}.json`), JSON.stringify(kept, null, 2));
}

/** Move a report (and its screenshot) between open/ and closed/; the report with its new `dir`. */
export async function moveReport(dataDir, report, to) {
  if (report.dir === to) return report;
  await mkdir(join(dataDir, to), { recursive: true });
  await rename(join(dataDir, report.dir, `${report.number}.json`), join(dataDir, to, `${report.number}.json`));
  if (report.screenshot) await rename(join(dataDir, report.dir, report.screenshot), join(dataDir, to, report.screenshot)).catch(() => {});
  return { ...report, dir: to };
}

/** A thread made anonymous: its messages stay, nothing ties them to a person any more. */
export const forgetThread = (thread, now = new Date()) => ({ forgotten: now.toISOString(), messages: thread.messages ?? [] });

/** Whether a report can be answered: it has a thread with a key. */
export const repliable = (r) => Boolean(r.thread && validKeyId(r.thread.keyId));

/** The user's messages Lapin has not read yet. */
export const unreadByLapin = (r) => (repliable(r) ? r.thread.messages.slice(r.thread.lapinSeen ?? 0).filter((m) => m.from === 'user').length : 0);
/** Lapin's messages the user has not read yet. */
export const unreadByUser = (r) => (repliable(r) ? r.thread.messages.slice(r.thread.userSeen ?? 0).filter((m) => m.from === 'lapin').length : 0);

/** The e-mail an older report kept (as `contact`, a string) or a newer one keeps (as `email`). */
export const emailOf = (r) => r.email ?? (typeof r.contact === 'string' ? r.contact : undefined);

/**
 * Anonymise the threads idle for longer than `months` (no message, and no
 * report, since then): the retention the report form promises. How many.
 */
export async function pruneThreads(dataDir, months = 12, now = new Date()) {
  const cutoff = new Date(now);
  cutoff.setMonth(cutoff.getMonth() - months);
  return withLock(dataDir, async () => {
    let n = 0;
    for (const r of await allReports(dataDir)) {
      if (!repliable(r)) continue;
      const last = Date.parse(r.thread.lastActivity ?? r.receivedAt);
      if (Number.isFinite(last) && last >= cutoff.getTime()) continue;
      await saveReport(dataDir, { ...r, thread: forgetThread(r.thread, now) });
      n++;
    }
    return n;
  });
}
