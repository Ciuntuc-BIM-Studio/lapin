/**
 * The store's data, as files under DATA_DIR (deploy/store/README.md):
 *   submissions/<sid>.json        a submission: its owner (forum id and name), its manifest fields, its file, its pull request
 *   submissions/<sid>/            its screenshots until they are sent
 *   packages/<id>/<version>/<file>   the files authors uploaded, served at /packages/… (a manifest's package.url)
 *   owners.json                   { <extension id>: { forumId, username } }: who may publish its new versions here
 *   stats.json                    { <extension id>: { <version>: { <yyyy-mm>: n } } }: downloads, nothing about who
 * Every change happens under one lock (a directory, DATA_DIR/.lock) and is written atomically (a temporary file renamed
 * over the old one), so the server and the CLI never overwrite each other. No dependencies: Node 22's fs and crypto.
 */

import { createHash, randomBytes } from 'node:crypto';
import { mkdir, readdir, readFile, rename, rm, stat, writeFile } from 'node:fs/promises';
import { join } from 'node:path';

export const sha256 = (bytes) => createHash('sha256').update(bytes).digest('hex');

/** Write a file whole or not at all. */
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

const readJson = async (path, fallback) => { try { return JSON.parse(await readFile(path, 'utf8')); } catch { return fallback; } };

/** A submission's id: 16 random hex digits. */
export const newSid = () => randomBytes(8).toString('hex');
export const validSid = (s) => typeof s === 'string' && /^[0-9a-f]{16}$/.test(s);

export function storeData(dataDir) {
  const sub = (sid) => join(dataDir, 'submissions', `${sid}.json`);
  return {
    dataDir,
    async submission(sid) { return validSid(sid) ? readJson(sub(sid), null) : null; },
    async saveSubmission(s) {
      await mkdir(join(dataDir, 'submissions'), { recursive: true });
      await writeAtomic(sub(s.sid), JSON.stringify(s, null, 2));
    },
    async removeSubmission(sid) {
      await rm(sub(sid), { force: true });
      await rm(join(dataDir, 'submissions', sid), { recursive: true, force: true });
    },
    async submissions() {
      const out = [];
      for (const name of await readdir(join(dataDir, 'submissions')).catch(() => [])) {
        const m = /^([0-9a-f]{16})\.json$/.exec(name);
        if (m) { const s = await readJson(sub(m[1]), null); if (s) out.push(s); }
      }
      return out.sort((a, b) => (b.createdAt ?? '').localeCompare(a.createdAt ?? ''));
    },
    screenshotDir: (sid) => join(dataDir, 'submissions', sid),
    packagePath: (id, version, file) => join(dataDir, 'packages', id, version, file),
    async owners() { return readJson(join(dataDir, 'owners.json'), {}); },
    async setOwner(id, owner) {
      const all = await readJson(join(dataDir, 'owners.json'), {});
      all[id] = owner;
      await writeAtomic(join(dataDir, 'owners.json'), JSON.stringify(all, null, 2));
    },
    async saveOwners(all) { await writeAtomic(join(dataDir, 'owners.json'), JSON.stringify(all, null, 2)); },
    async stats() { return readJson(join(dataDir, 'stats.json'), {}); },
    async saveStats(all) { await mkdir(dataDir, { recursive: true }); await writeAtomic(join(dataDir, 'stats.json'), JSON.stringify(all)); },
  };
}

/** The downloads counted in memory, added to stats.json now and then (flush) and when the service stops. */
export function downloadCounter(data, { now = () => new Date() } = {}) {
  let pending = {};
  let count = 0;
  return {
    add(id, version) {
      const month = now().toISOString().slice(0, 7);
      ((pending[id] ??= {})[version] ??= {})[month] = (pending[id][version][month] ?? 0) + 1;
      count++;
    },
    get pending() { return count; },
    /** The counts kept plus those not yet written: { total, versions: { <version>: n } }. */
    async of(id) {
      const kept = (await data.stats())[id] ?? {};
      const versions = {};
      for (const src of [kept, pending[id] ?? {}]) for (const [v, months] of Object.entries(src)) versions[v] = (versions[v] ?? 0) + Object.values(months).reduce((a, b) => a + b, 0);
      return { total: Object.values(versions).reduce((a, b) => a + b, 0), versions };
    },
    async flush() {
      if (!count) return;
      const add = pending;
      pending = {}; count = 0;
      await withLock(data.dataDir, async () => {
        const all = await data.stats();
        for (const [id, vs] of Object.entries(add)) for (const [v, months] of Object.entries(vs)) for (const [m, n] of Object.entries(months)) ((all[id] ??= {})[v] ??= {})[m] = (all[id][v][m] ?? 0) + n;
        await data.saveStats(all);
      });
    },
  };
}
