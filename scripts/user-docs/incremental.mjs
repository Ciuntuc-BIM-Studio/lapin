// The guide's incremental runs (run.mjs): a scene is taken again in a language
// only when something its pictures depend on changed since its last good run.
// What a scene depends on, worked out cheaply from the files' git hashes:
//   - its own code in scenes.mjs, with the helpers it calls (house, select…), by name, transitively;
//   - the guide's pages that show its pictures (English and the language's own);
//   - the modules it drives: a module id before a dot in its text ('architecture.wall', t.cmd('steel.…')),
//     its name or one of its ribbon tabs' in quotes ('Steel', 'Site'), or its own `touches: ['wood']`; with what
//     those modules require;
//   - the shared code, which every scene drives: ui/ outside the modules (shell, platform, viewport, i18n,
//     theme, the core module), engine/ outside its modules (platform, model, geometry, host), run.mjs,
//     languages.mjs, the product and its resolution. A change there takes every scene again.
// Tests and Markdown never count. The state (each scene × language: its fingerprint's parts, the files it
// wrote, how long it took) is kept in build/user-docs-cache/, outside the published folder, and the
// pictures stay where they are taken (build/user-docs/shots).
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, realpathSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, relative, sep } from 'node:path';
import { pathToFileURL } from 'node:url';
import { knownModules } from '../product.mjs';

/** A full run at least this often, whatever changed: what the coarse dependencies miss is caught within a week. */
export const FULL_EVERY_DAYS = 7;

const sha = (s) => createHash('sha1').update(s).digest('hex');
const ignored = (rel) => /\.test\.[cm]?[jt]sx?$/.test(rel) || /(^|\/)__tests__\//.test(rel) || rel.endsWith('.md');

/** Every file git knows in the repository holding `dir` (absolute path → content hash): the index's blob ids, files changed or new in the working tree hashed as they are. */
const repos = new Map();
function repoFiles(dir) {
  const top = execFileSync('git', ['-C', dir, 'rev-parse', '--show-toplevel'], { encoding: 'utf8' }).trim();
  if (repos.has(top)) return repos.get(top);
  const files = new Map();
  const git = (...a) => execFileSync('git', ['-C', top, ...a], { encoding: 'utf8', maxBuffer: 1 << 28 }).split('\0').filter(Boolean);
  for (const line of git('ls-files', '-s', '-z')) {
    const tab = line.indexOf('\t');
    files.set(join(top, line.slice(tab + 1)), line.slice(0, tab).split(' ')[1]);
  }
  for (const f of git('ls-files', '-m', '-o', '--exclude-standard', '-z')) {
    const p = join(top, f);
    if (existsSync(p) && statSync(p).isFile()) files.set(p, sha(readFileSync(p))); else files.delete(p);
  }
  repos.set(top, files);
  return files;
}

/** One hash for the files under some folders (absolute), less some others; paths relative to `base`, so another checkout of the same commit hashes the same. */
function areaHash(base, include, exclude = []) {
  const asDir = (d) => (existsSync(d) && statSync(d).isDirectory() ? d + sep : d);
  const inc = include.filter((d) => existsSync(d)).map(asDir), exc = exclude.map(asDir);
  const rows = [];
  for (const files of new Set(inc.map((d) => repoFiles(d.endsWith(sep) ? d : join(d, '..'))))) {
    for (const [p, h] of files) {
      if (!inc.some((d) => p === d || (d.endsWith(sep) && p.startsWith(d))) || exc.some((d) => p === d || (d.endsWith(sep) && p.startsWith(d)))) continue;
      const rel = relative(base, p).split(sep).join('/');
      if (!ignored(rel)) rows.push(`${rel}\0${h}`);
    }
  }
  return sha(rows.sort().join('\n'));
}

/** The top-level helpers of a scenes file (functions and constants before and after its `scenes` list), by name. */
function helpersOf(source) {
  const lines = source.split('\n');
  const decl = /^(?:export\s+)?(?:async\s+)?(?:function\*?\s+(\w+)|const\s+(\w+)\s*=)/;
  const starts = [];
  lines.forEach((l, i) => { const m = l.match(decl); if (m) starts.push({ i, name: m[1] ?? m[2] }); });
  const out = new Map();
  starts.forEach((s, k) => { if (s.name !== 'scenes') out.set(s.name, lines.slice(s.i, starts[k + 1]?.i ?? lines.length).join('\n')); });
  return out;
}

/** A scene's own text: its fields and its code, with every helper it uses, transitively. */
function sceneText(scene, helpers) {
  const own = JSON.stringify(scene, (k, v) => (typeof v === 'function' ? v.toString() : v));
  const seen = new Set();
  const visit = (text) => {
    for (const [name, body] of helpers) if (!seen.has(name) && new RegExp(`\\b${name}\\b`).test(text)) { seen.add(name); visit(body); }
  };
  visit(own);
  return [own, ...[...seen].sort().map((n) => helpers.get(n))].join('\n');
}

const esc = (s) => s.replace(/[.*+?^${}()|[\]\\-]/g, '\\$&');

/**
 * What each scene depends on, ready to fingerprint: `inputs(scene, lang, files)` gives the parts
 * ({ scene, shared, modules: {id: hash}, pages }) for the files it wrote last time.
 */
export function dependencies(root, product, productFile, sceneFiles, docsDirs) {
  const known = knownModules(root, existsSync(productFile) ? JSON.parse(readFileSync(productFile, 'utf8')).external ?? {} : {});
  const modules = product.modules.filter((m) => m !== 'core' && known[m]);
  const dirsOf = (m) => [known[m].engineDir, known[m].uiDir].filter(Boolean);
  const moduleHash = new Map(modules.map((m) => [m, areaHash(root, dirsOf(m))]));
  const shared = areaHash(root,
    [join(root, 'ui'), join(root, 'engine'), ...['run.mjs', 'languages.mjs', 'incremental.mjs'].map((f) => join(root, 'scripts/user-docs', f)), join(root, 'scripts/product.mjs'), join(root, 'package.json'), join(root, 'pnpm-lock.yaml'), productFile],
    [join(root, 'engine/tests'), join(root, 'engine/src/Host/Lapin.Browser'), ...modules.flatMap(dirsOf)]);
  // Each scene's file's helpers, and each scene's text with them.
  const texts = new Map();
  for (const { file, scenes } of sceneFiles) {
    const helpers = helpersOf(readFileSync(file, 'utf8'));
    for (const s of scenes) texts.set(s.id, sceneText(s, helpers));
  }
  const touched = new Map();
  const requires = (m, into) => { if (m === 'core' || into.has(m) || !known[m]) return; into.add(m); for (const r of known[m].requires) requires(r, into); };
  // A module's ribbon tabs by their labels (a tab has an order: `id: 'site', label: 'Site', order: 40`), clicked by name in a scene.
  const tabsOf = (m) => {
    const f = known[m].uiDir && join(known[m].uiDir, 'index.tsx');
    return f && existsSync(f) ? [...readFileSync(f, 'utf8').matchAll(/label: '([^']+)', order:/g)].map((x) => x[1]) : [];
  };
  const patterns = modules.map((m) => {
    const names = [known[m].name, ...tabsOf(m)].map((n) => `['"\`]${esc(n)}['"\`]`).join('|');
    return { m, re: new RegExp(`['"\`/]${esc(m)}\\.[A-Za-z]|/modules/${esc(m)}/|${names}`) };
  });
  const modulesOf = (scene) => {
    if (touched.has(scene.id)) return touched.get(scene.id);
    const text = texts.get(scene.id);
    const set = new Set();
    for (const { m, re } of patterns) if (re.test(text)) requires(m, set);
    for (const m of scene.touches ?? []) requires(m, set);
    const list = [...set].sort();
    touched.set(scene.id, list);
    return list;
  };
  // The pages, read once: which pictures each shows.
  const pages = [];
  for (const dir of docsDirs) for (const lang of ['', ...readdirSync(dir).filter((d) => /^[a-z]{2}$/.test(d) && statSync(join(dir, d)).isDirectory())]) {
    const folder = join(dir, lang);
    for (const f of readdirSync(folder).filter((f) => f.endsWith('.md'))) {
      const text = readFileSync(join(folder, f), 'utf8');
      pages.push({ lang: lang || 'en', file: join(folder, f), hash: sha(text), shows: new Set([...text.matchAll(/shots\/([\w.-]+\.(?:webp|json))/g)].map((m) => m[1])) });
    }
  }
  return {
    modulesOf,
    inputs(scene, lang, files = []) {
      const its = pages.filter((p) => (p.lang === 'en' || p.lang === lang) && files.some((f) => p.shows.has(f)));
      return {
        scene: sha(texts.get(scene.id)),
        shared,
        modules: Object.fromEntries(modulesOf(scene).map((m) => [m, moduleHash.get(m)])),
        pages: sha(its.map((p) => `${relative(root, p.file)}\0${p.hash}`).sort().join('\n')),
      };
    },
  };
}

/** Why a scene must be taken again (null when its pictures still hold), from its stored parts and its current ones. */
export function reasonToRun(entry, now, folderOf) {
  if (!entry) return 'never taken';
  if (!entry.files?.every((f) => existsSync(join(folderOf(), f)))) return 'pictures missing';
  const why = [];
  if (entry.parts.scene !== now.scene) why.push('its scene changed');
  if (entry.parts.shared !== now.shared) why.push('shared code changed');
  const mods = new Set([...Object.keys(entry.parts.modules), ...Object.keys(now.modules)]);
  for (const m of [...mods].sort()) if (entry.parts.modules[m] !== now.modules[m]) why.push(`module ${m} changed`);
  if (entry.parts.pages !== now.pages) why.push('its pages changed');
  return why.length ? why.join(', ') : null;
}

/** The state of a product's pictures: build/user-docs-cache/<product>.json. */
export function stateStore(root, productId) {
  const dir = join(root, 'build', 'user-docs-cache');
  const file = join(dir, `${productId}.json`);
  let state = null;
  try { state = JSON.parse(readFileSync(file, 'utf8')); } catch { state = null; }
  const fresh = state && typeof state.lastFull === 'object' && state.lastFull ? state : { lastFull: {}, scenes: state?.scenes ?? {} };
  return {
    /** Whether there was a state to go by. */
    found: state !== null,
    state: fresh,
    key: (id, lang) => `${id}|${lang}`,
    save() { mkdirSync(dir, { recursive: true }); writeFileSync(file, JSON.stringify(fresh, null, 1)); },
    /** Days since a language's last full run (Infinity when none). */
    ageDays(lang) { const at = fresh.lastFull[lang]; return at ? (Date.now() - Date.parse(at)) / 86_400_000 : Infinity; },
  };
}

/** The scenes a run's log says were taken: its `✓ <id>[ (<lang>)] (<pictures>) <secs> s` lines (English has no language). */
export function takenInLog(text) {
  const taken = [];
  for (const line of text.split('\n')) {
    const m = line.match(/^\s*✓ (\S+) (?:\(([a-z]{2})\) )?\(([^()]*)\) ([\d.]+) s\s*$/);
    if (m) taken.push({ id: m[1], lang: m[2] ?? 'en', names: m[3].split(', ').filter(Boolean), secs: Number(m[4]) });
  }
  return taken;
}

/**
 * The state seeded from pictures already in build/user-docs/shots, taken by a run at `ref` before this
 * state existed (run.mjs --seed-from <ref> --seed-log <log>[,<log>…]): each scene × language its logs
 * say was taken is recorded with the inputs it had at `ref` (its scene, modules and pages, worked out
 * from a checkout of `ref`), so the next run takes again only what changed since. The shared code is
 * counted as it is now: a seed starts the incremental runs from those pictures, and the weekly full run
 * catches what a change there alters. Pictures missing, scenes not in the logs or failed, are not
 * seeded and are taken next time; the languages the logs cover count as run whole when the log was written.
 */
export async function seedState({ root, ref, logs, product, productId, langs, shotsOf, store, sharedNow }) {
  // Its real path: git names the checkout's files by it (macOS's temporary folder is a link).
  const at = realpathSync(mkdtempSync(join(tmpdir(), 'lapin-user-docs-seed-')));
  execFileSync('git', ['-C', root, 'worktree', 'add', '-q', '--detach', at, ref]);
  try {
    const old = (await import(pathToFileURL(join(at, 'scripts/user-docs/scenes.mjs')).href)).scenes;
    const deps = dependencies(at, product, join(at, 'products', `${productId}.json`),
      [{ file: join(at, 'scripts/user-docs/scenes.mjs'), scenes: old }], [join(at, 'docs/user')]);
    const date = new Date(Number(execFileSync('git', ['-C', root, 'log', '-1', '--format=%ct', ref], { encoding: 'utf8' })) * 1000).toISOString();
    let seeded = 0, skipped = 0;
    const covered = new Map();
    for (const log of logs) {
      const written = statSync(log).mtime.toISOString();
      for (const t of takenInLog(readFileSync(log, 'utf8'))) {
        const scene = old.find((s) => s.id === t.id);
        const files = t.names.map((n) => `${n}.webp`);
        if (!scene || !langs.includes(t.lang) || !files.every((f) => existsSync(join(shotsOf(t.lang), f)))) { skipped++; continue; }
        store.state.scenes[store.key(t.id, t.lang)] = { parts: { ...deps.inputs(scene, t.lang, files), shared: sharedNow }, files, secs: t.secs, at: date, seeded: ref };
        seeded++;
        if (!covered.has(t.lang) || covered.get(t.lang) < written) covered.set(t.lang, written);
      }
    }
    for (const [lang, when] of covered) store.state.lastFull[lang] = when;
    store.save();
    return { seeded, skipped, langs: [...covered.keys()] };
  } finally {
    execFileSync('git', ['-C', root, 'worktree', 'remove', '--force', at]);
  }
}
