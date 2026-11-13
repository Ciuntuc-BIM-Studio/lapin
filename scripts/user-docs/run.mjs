#!/usr/bin/env node
// The user guide (docs/user, published at https://lapin.ciuntucbimstudio.ro/docs/):
// the pages are Markdown written by hand; their pictures are taken here, by
// scenes (scenes.mjs) that drive the real app the way a user does, in Chrome,
// and check what they see on the way. A scene that no longer finds its button
// or its result fails the run: the guide never shows an interface that is gone.
// The guide's languages are languages.mjs's (English in docs/user, the others
// in docs/user/<id>): each scene runs once per language, the pictures taken
// with the interface in it. A page a language has not translated yet is
// rendered from English, with English pictures, and the run says which.
//
//   pnpm docs:user                 the engine and the UI started for the run, every scene, then the pages → build/user-docs
//   pnpm docs:user --only walls    the scenes whose id starts with "walls" (or one of several: --only walls,roofs; the other pictures stay as they were), those whose inputs changed (with --full: whatever changed)
//   pnpm docs:user --lang ro       one language's pictures (or several: --lang ro,de; the others' stay as they were)
//   pnpm docs:user --pages         the pages alone, from the pictures already taken
//   pnpm docs:user --theme graphite the pictures in the dark theme, for a look only (build/user-docs/shots-graphite; no pages)
//   pnpm docs:user --full          every scene again, whatever changed (else only the scenes whose inputs changed: below)
//   pnpm docs:user --jobs 4        how many scenes run at once (default: by this computer's cores and memory, at most 4)
//   pnpm docs:user --product <id|file> --scenes <file> --docs <folder> [--alone] [--out <folder>]
//                                  an edition's own scenes and pages with the guide's (--alone: its own only, a guide of its own),
//                                  written to --out instead of build/user-docs (its state beside it, <folder>-cache)
//   pnpm docs:user --seed-from <ref> --seed-log <log>[,<log>…]
//                                  no pictures: the state seeded from those already taken by a run at <ref> (its logs say which), so the next run takes only what changed since (incremental.mjs, seedState)
//
// Incremental by default (incremental.mjs): a scene × language is taken again only when its code, the
// pages showing its pictures, the modules it drives or the shared code (ui/ and engine/ outside the
// modules, this runner) changed since its last good run; the others keep their pictures. The run says
// which scenes it takes and why. A language is taken whole by itself when it has no state yet
// (build/user-docs-cache/<product>.json) and when its last full run is older than a week (not with --only).
// A failing scene fails the run and is taken again next time.
//
// In parallel: each of the --jobs workers starts its own engine, relay, Vite (its dependency cache in
// ui/node_modules/.vite-docs-<n>) and Chrome on free ports, and takes the next scene × language from one
// queue, the longest first (by last run's times).
//
// CHROME: the browser to drive (default: the installed Google Chrome).
import { spawn, spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, mkdtempSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:net';
import { availableParallelism, freemem, tmpdir, totalmem } from 'node:os';
import { dirname, join, resolve as resolvePath } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { chromium } from 'playwright-core';
import { resolveProduct } from '../product.mjs';
import { scenes as ownScenes } from './scenes.mjs';
import { render } from './render.mjs';
import { LANGS, checkInterfaceLanguages } from './languages.mjs';
import { FULL_EVERY_DAYS, dependencies, reasonToRun, seedState, stateStore } from './incremental.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '../..');
const args = process.argv.slice(2);
// Where the guide goes: build/user-docs, or another folder (--out: an edition's guide kept apart from the published one, its state in <folder>-cache).
const out = args.includes('--out') ? resolvePath(args[args.indexOf('--out') + 1]) : join(root, 'build', 'user-docs');
const outName = args.includes('--out') ? out : 'build/user-docs';
const shots = join(out, 'shots');
const only = args.includes('--only') ? String(args[args.indexOf('--only') + 1]).split(',') : null;
checkInterfaceLanguages(root);
// Another edition (a premium module's pictures, taken with its own scenes from its repository): --product lapin-urban --scenes ../lapin-urban/docs/scenes.mjs
// (and its pages: --docs ../lapin-planning/docs/user)
const productId = args.includes('--product') ? args[args.indexOf('--product') + 1] : 'lapin';
const extraFile = args.includes('--scenes') ? resolvePath(args[args.indexOf('--scenes') + 1]) : null;
const extra = extraFile ? (await import(pathToFileURL(extraFile).href)).scenes ?? [] : [];
// --alone: the edition's scenes and pages only (a guide of its own, none of the guide's pages or pictures in it).
const alone = args.includes('--alone');
if (alone && (!extraFile || !args.includes('--docs'))) { console.error('✗ --alone takes --scenes <file> and --docs <folder>: the edition\'s own'); process.exit(1); }
const scenes = alone ? extra : [...ownScenes, ...extra];
// How many at once: a worker (an engine, Vite and Chrome) takes about two cores and a few GB while a scene runs.
const jobs = args.includes('--jobs') ? Math.max(1, Number(args[args.indexOf('--jobs') + 1]) || 1)
  : Math.max(1, Math.min(4, Math.floor(availableParallelism() / 2.5), Math.floor(totalmem() / 2 ** 30 / 6)));
// Its pages too (--docs ../lapin-planning/docs/user: English *.md and <lang>/*.md), rendered with the guide's from a merged copy.
const extraDocs = args.includes('--docs') ? resolvePath(args[args.indexOf('--docs') + 1]) : null;
const langs = args.includes('--lang') ? String(args[args.indexOf('--lang') + 1]).split(',') : LANGS;
if (!langs.every((l) => LANGS.includes(l))) { console.error(`✗ --lang: one or more of ${LANGS.join(', ')}`); process.exit(1); }
// Another theme (report #110): its pictures beside the guide's, never in it, to look at.
const theme = args.includes('--theme') ? args[args.indexOf('--theme') + 1] : null;
if (theme && !['paper', 'graphite'].includes(theme)) { console.error('✗ --theme: paper or graphite'); process.exit(1); }
/** A language's pictures: shots/ for English, shots/<id>/ for the others (shots-<theme>/ for another theme's). */
const shotsOf = (lang) => { const base = theme ? join(out, `shots-${theme}`) : shots; return lang === 'en' ? base : join(base, lang); };
const store = theme ? null : stateStore(root, productId.endsWith('.json') ? resolveProduct(root, productId).id : productId, args.includes('--out') ? `${out}-cache` : undefined);
// A seed (--seed-from): the state of pictures taken before it existed, then nothing else.
if (args.includes('--seed-from')) {
  if (!store || extraFile || extraDocs || args.includes('--out') || !args.includes('--seed-log')) { console.error('✗ --seed-from <ref> takes --seed-log <log>[,<log>…], for this repository\'s scenes in the paper theme'); process.exit(1); }
  const product = resolveProduct(root, productId);
  const productFile = productId.endsWith('.json') ? resolvePath(root, productId) : join(root, 'products', `${productId}.json`);
  const sharedNow = dependencies(root, product, productFile, [{ file: join(root, 'scripts/user-docs/scenes.mjs'), scenes: ownScenes }], [join(root, 'docs/user')]).inputs(ownScenes[0], 'en').shared;
  const r = await seedState({ root, ref: args[args.indexOf('--seed-from') + 1], logs: String(args[args.indexOf('--seed-log') + 1]).split(',').map((l) => resolvePath(l)),
    product, productId: product.id ?? productId, langs, shotsOf, store, sharedNow });
  console.log(`✓ seeded ${r.seeded} scene(s) × language from ${args[args.indexOf('--seed-from') + 1]} (${r.langs.join(', ')}); ${r.skipped} not seeded (pictures missing or no such scene), taken next time`);
  process.exit(0);
}
// The languages taken whole: all with --full (and another theme), else each whose last full run is missing or over a week old (not with --only).
const fullLangs = new Set(args.includes('--pages') ? [] : langs.filter((l) => theme || args.includes('--full') || (!only && store.ageDays(l) > FULL_EVERY_DAYS)));
if (fullLangs.size) console.log(`▸ a full run of ${[...fullLangs].join(', ')}: ${theme ? 'another theme' : args.includes('--full') ? '--full' : [...fullLangs].map((l) => (store.ageDays(l) === Infinity ? `${l} never ran whole here (${args.includes('--out') ? `${out}-cache` : 'build/user-docs-cache'})` : `${l} last ran whole ${store.ageDays(l).toFixed(1)} days ago`)).join('; ')}`);
// A full run takes every scene again: the pictures start from nothing, so none outlives the scene that took it.
for (const lang of langs) {
  if (fullLangs.has(lang) && !only) for (const f of existsSync(shotsOf(lang)) ? readdirSync(shotsOf(lang)) : []) if (f.endsWith('.webp')) rmSync(join(shotsOf(lang), f));
  mkdirSync(shotsOf(lang), { recursive: true });
}

if (!args.includes('--pages')) await takePictures();
if (theme) { console.log(`✓ the pictures in the ${theme} theme → ${outName}/shots-${theme}`); process.exit(0); }
let failed = false;
const docs = alone ? extraDocs : extraDocs ? mergedDocs(extraDocs) : join(root, 'docs/user');
for (const lang of LANGS) {
  let r;
  try { r = render(root, out, lang, docs); } catch (e) { console.error(`✗ ${lang}: ${e instanceof Error ? e.message : e}`); failed = true; continue; }
  const { pages, missing, unused, english } = r;
  for (const u of unused) console.warn(`▸ ${lang}: a picture no page shows: ${u}`);
  if (missing.length) { console.error(`✗ ${lang}: pictures the pages show but no scene takes: ${missing.join(', ')}`); failed = true; }
  if (english.length) console.log(`▸ ${lang}: ${english.length} page(s) not translated yet, rendered from English: ${english.length === pages ? 'all' : english.join(', ')}`);
  console.log(`✓ ${lang}: ${pages} pages → ${outName}${lang === 'en' ? '' : `/${lang}`}`);
}
if (failed) process.exit(1);

/** The guide's pages with an edition's own laid over them, in a folder of their own (the repository's stay as they are). */
function mergedDocs(extra) {
  const merged = mkdtempSync(join(tmpdir(), 'lapin-user-docs-pages-'));
  cpSync(join(root, 'docs/user'), merged, { recursive: true });
  cpSync(extra, merged, { recursive: true });
  return merged;
}

async function takePictures() {
  const product = resolveProduct(root, productId);
  // What runs: every scene × language, or (incremental) those whose inputs changed, each with its reason.
  const chosen = scenes.filter((s) => !only || only.some((o) => s.id.startsWith(o)));
  const productFile = productId.endsWith('.json') ? resolvePath(root, productId) : join(root, 'products', `${productId}.json`);
  const deps = store && dependencies(root, product, productFile,
    [...(alone ? [] : [{ file: join(root, 'scripts/user-docs/scenes.mjs'), scenes: ownScenes }]), ...(extraFile ? [{ file: extraFile, scenes: extra }] : [])],
    [...(alone ? [] : [join(root, 'docs/user')]), ...(extraDocs ? [extraDocs] : [])]);
  const queue = [];
  const kept = [];
  for (const lang of langs) for (const scene of chosen) {
    const entry = store?.state.scenes[store.key(scene.id, lang)];
    const full = fullLangs.has(lang);
    const why = full ? null : reasonToRun(entry, deps.inputs(scene, lang, entry?.files), () => shotsOf(lang));
    if (full || why) queue.push({ scene, lang, why: why ?? 'full run', secs: entry?.secs ?? 30 });
    else kept.push(`${scene.id}${lang === 'en' ? '' : ` (${lang})`}`);
  }
  // A scene gone from the list takes its pictures with it.
  if (store && !theme) for (const [key, entry] of Object.entries(store.state.scenes)) {
    const [id, lang] = key.split('|');
    if (scenes.some((s) => s.id === id) || !langs.includes(lang)) continue;
    for (const f of entry.files ?? []) rmSync(join(shotsOf(lang), f), { force: true });
    delete store.state.scenes[key];
    console.log(`▸ ${id} (${lang}) is no longer a scene: its pictures removed`);
  }
  if (queue.some((q) => q.why !== 'full run') || kept.length) {
    for (const q of queue) console.log(`  ▸ ${q.scene.id}${q.lang === 'en' ? '' : ` (${q.lang})`}: ${q.why}`);
    console.log(`▸ ${queue.length} scene(s) to take, ${kept.length} unchanged kept their pictures`);
  }
  if (!queue.length) { store?.save(); return; }
  // The longest first (last run's times), so the workers finish together.
  queue.sort((a, b) => b.secs - a.secs);

  const free = () => new Promise((r) => { const s = createServer(); s.listen(0, '127.0.0.1', () => { const p = s.address().port; s.close(() => r(p)); }); });
  console.log('▸ building the engine…');
  if (spawnSync('dotnet', ['build', join(root, 'engine/Lapin.slnx'), '-v', 'q', '-nologo'], { stdio: 'inherit' }).status !== 0) process.exit(1);
  // A module outside this repository is not in the solution: built on its own.
  for (const csproj of product.engineProjects ?? []) if (spawnSync('dotnet', ['build', csproj, '-v', 'q', '-nologo', `-p:LapinProDir=${root}/`], { stdio: 'inherit' }).status !== 0) process.exit(1);
  const temp = mkdtempSync(join(tmpdir(), 'lapin-user-docs-'));
  const manifest = join(temp, 'product.json');
  writeFileSync(manifest, JSON.stringify(product, null, 2));
  const children = [];
  // Vite runs under pnpm in a process group of its own: the whole group goes.
  const stop = () => { for (const c of children) { try { if (c.group) process.kill(-c.pid, 'SIGTERM'); else c.kill('SIGTERM'); } catch { /* gone already */ } } };
  process.on('exit', stop);
  for (const sig of ['SIGINT', 'SIGTERM']) process.on(sig, () => { stop(); process.exit(130); });
  const ready = (child, text, name) => new Promise((resolve, reject) => {
    let seen = '';
    const look = (d) => { seen += d; if (seen.includes(text)) resolve(); };
    child.stdout.on('data', look); child.stderr.on('data', look);
    child.on('exit', (code) => reject(new Error(`${name} stopped (${code}): ${seen.slice(-800)}`)));
    setTimeout(() => reject(new Error(`${name} did not start: ${seen.slice(-800)}`)), 120_000);
  });
  const hostDll = join(root, 'engine/src/Host/Lapin.Host/bin/Debug/net10.0/Lapin.Host.dll');
  const n = Math.min(jobs, queue.length);
  console.log(`▸ ${queue.length} scene(s) in ${n} worker(s) (${availableParallelism()} cores, ${(freemem() / 2 ** 30).toFixed(0)} of ${(totalmem() / 2 ** 30).toFixed(0)} GB free)`);
  const failures = [];
  const t0all = Date.now();

  /** One worker: its own relay, engine, Vite and Chrome, taking scenes from the queue until it is empty. */
  async function worker(w) {
    const [enginePort, uiPort, relayPort] = [await free(), await free(), await free()];
    // A relay of its own for the shared session's pictures (docs/plan-multiplayer.md): the engine shares through it, the invitation opens this UI.
    const relay = spawn('dotnet', [join(root, 'engine/src/Relay/Lapin.Relay/bin/Debug/net10.0/Lapin.Relay.dll'), '--urls', `http://127.0.0.1:${relayPort}`]);
    children.push(relay);
    const engine = spawn('dotnet', [hostDll, '--product', manifest, '--modules-dir', join(root, 'engine/artifacts/modules'), '--user-dir', join(temp, `user-${w}`), '--port', String(enginePort), '--parent-pid', String(process.pid)], {
      env: { ...process.env, LAPIN_RELAY: `ws://127.0.0.1:${relayPort}/relay`, LAPIN_JOIN_PAGE: `http://localhost:${uiPort}/` },
    });
    children.push(engine);
    const ui = spawn('pnpm', ['--filter', 'ui', 'exec', 'vite', '--port', String(uiPort), '--strictPort'], {
      cwd: root, detached: true,
      env: { ...process.env, VITE_ENGINE_URL: `ws://127.0.0.1:${enginePort}/ws`, LAPIN_PRODUCT_FILE: manifest, LAPIN_VITE_CACHE_DIR: join(root, 'ui', 'node_modules', `.vite-docs-${w}`) },
    });
    ui.group = true;
    children.push(ui);
    await Promise.all([ready(engine, 'LAPIN_HOST_READY', `the engine (worker ${w})`), ready(ui, 'Local:', `the UI (worker ${w})`)]);
    console.log(`▸ worker ${w}: engine on ${enginePort}, UI on ${uiPort} (ready after ${((Date.now() - t0all) / 1000).toFixed(0)} s)`);
    const browser = await chromium.launch(process.env.CHROME ? { executablePath: process.env.CHROME } : { channel: 'chrome' });
    try {
      for (let job = queue.shift(); job; job = queue.shift()) {
        const { scene, lang } = job;
        const t0 = Date.now();
        const id = lang === 'en' ? scene.id : `${scene.id} (${lang})`;
        const key = store?.key(scene.id, lang);
        // What it wrote last time goes first: a picture it no longer takes does not outlive it.
        for (const f of store?.state.scenes[key]?.files ?? []) rmSync(join(shotsOf(lang), f), { force: true });
        const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 2 });
        const errors = [];
        page.on('pageerror', (e) => errors.push(e.message));
        page.on('dialog', (d) => void d.accept());
        // Exports and saves ask for a file name first (decision 509): no native picker here, and the Save as box (in any language: its input holds a name with an extension) answered with OK, so the download follows.
        await page.addInitScript(() => {
          try { Object.defineProperty(window, 'showSaveFilePicker', { value: undefined, configurable: true, writable: true }); } catch { /* kept */ }
          setInterval(() => {
            const box = document.querySelector('.ask-box');
            const input = box?.querySelector('input[type=text], input:not([type])');
            if (input && /\.[A-Za-z0-9]{2,6}$/.test(input.value)) box.querySelector('.btn.primary')?.click();
          }, 150);
        });
        // Reports a scene might open never leave the machine.
        await page.route('**/api/feedback**', (r) => r.abort());
        try {
          const t = tools(page, `http://localhost:${uiPort}/`, shotsOf(lang), lang);
          await scene.run(t);
          if (errors.length) throw new Error(`errors in the page: ${errors.join(' | ')}`);
          const secs = (Date.now() - t0) / 1000;
          console.log(`  ✓ ${id} (${t.taken.join(', ')}) ${secs.toFixed(1)} s${n > 1 ? ` [${w}]` : ''}${process.env.LAPIN_DOCS_DEBUG ? ` at ${((Date.now() - t0all) / 1000).toFixed(0)} s` : ''}`);
          if (store) {
            // Its inputs as they were when the run began (deps hashes the tree once), and the files it wrote this time.
            store.state.scenes[key] = { parts: deps.inputs(scene, lang, t.files), files: t.files, secs: Math.round(secs * 10) / 10, at: new Date().toISOString() };
            store.save();
          }
        } catch (e) {
          failures.push(id);
          if (store) { delete store.state.scenes[key]; store.save(); }
          const where = join(temp, `${scene.id}-${lang}-failed.png`);
          await page.screenshot({ path: where }).catch(() => {});
          console.error(`  ✗ ${id}: ${e instanceof Error ? e.message : e}\n    the page as it was: ${where}`);
        } finally {
          await page.close();
        }
      }
    } finally {
      // Several Chromes closing at once may take minutes to say so: not waited for beyond a few seconds (Playwright ends them with the run).
      await Promise.race([browser.close().catch(() => {}), new Promise((r) => setTimeout(r, 5000).unref())]);
    }
  }

  try {
    await Promise.all(Array.from({ length: n }, (_, w) => worker(w + 1)));
  } catch (e) {
    console.error(`✗ ${e instanceof Error ? e.message : e}`);
    stop();
    process.exit(1);
  } finally {
    stop();
  }
  console.log(`▸ the pictures took ${((Date.now() - t0all) / 1000).toFixed(0)} s`);
  if (failures.length) { console.error(`✗ ${failures.length} scene(s) failed: ${failures.join(', ')}`); process.exit(1); }
  if (store && !only && fullLangs.size) { for (const l of fullLangs) store.state.lastFull[l] = new Date().toISOString(); store.save(); }
  rmSync(temp, { recursive: true, force: true });
}

/**
 * What a scene drives the app with. A scene always drives the interface in
 * English (its buttons are found by their English names); for another
 * language, the interface turns to it for each picture and back after.
 */
function tools(page, url, folder, lang) {
  const taken = [];
  /** Every file it writes (pictures and data), for the incremental runs. */
  const files = [];
  let marks = [];
  const setLang = (l) => page.evaluate(async (l) => (await import('/src/i18n/index.ts')).setLang(l), l);
  const unplace = () => page.evaluate(() => document.querySelectorAll('.user-docs-mark').forEach((d) => d.remove()));
  async function place(list) {
    for (const m of list) {
      const b = await page.locator(m.of).first().boundingBox().catch(() => null);
      if (!b) throw new Error(`nothing to mark for ${m.n} (${m.of})`);
      const at = m.at ?? 'center';
      const x = at === 'right' ? b.x + b.width - 18 : at === 'left' ? b.x + 18 : at === 'before' ? b.x - 16 : at === 'after' ? b.x + b.width + 18 : b.x + b.width / 2;
      const y = at === 'below' ? b.y + b.height + 14 : at === 'bottom' ? b.y + b.height - 16 : at === 'center' || at === 'before' || at === 'after' ? b.y + b.height / 2 : b.y + Math.min(b.height / 2, 14);
      await page.evaluate(([x, y, n]) => {
        const d = document.createElement('div');
        d.className = 'user-docs-mark';
        d.textContent = String(n);
        Object.assign(d.style, { position: 'fixed', left: `${x - 13}px`, top: `${y - 13}px`, width: '26px', height: '26px', borderRadius: '50%', background: '#b5532d', color: '#fff',
          font: '700 14px/26px system-ui, sans-serif', textAlign: 'center', zIndex: '99999', boxShadow: '0 0 0 3px #fff, 0 2px 8px rgba(0,0,0,.35)', pointerEvents: 'none' });
        document.body.appendChild(d);
      }, [x, y, m.n]);
    }
  }
  /** The interface in the picture's language, and the marks placed again over the words as wide as they now are. */
  async function speak(l) {
    await setLang(l);
    await page.waitForTimeout(250);
    if (marks.length) { await unplace(); await place(marks); }
  }
  const t = {
    page, taken, files, lang,
    /** A command of the engine, as the interface sends it. */
    cmd: (command, args = {}) => page.evaluate(async ([c, a]) => (await import('/src/engine/session.ts')).execute(c, a), [command, args]),
    /** A call of the engine's protocol (doc.snapshot, session.info…). */
    call: (method, params = {}) => page.evaluate(async ([m, p]) => (await import('/src/engine/session.ts')).engine.call(m, p), [method, params]),
    /** The interface's state, changed as its buttons change it. */
    ui: (patch) => page.evaluate(async (p) => (await import('/src/app/store.ts')).useUi.setState(p), patch),
    /** The project's elements as the interface holds them: [{ id, type, name, props }]. */
    nodes: () => page.evaluate(async () => [...(await import('/src/engine/session.ts')).useSession.getState().nodes.values()].map((n) => ({ id: n.id, type: n.type, name: n.name, parent: n.parent, storeyId: n.storeyId, props: n.props }))),
    /** The start screen, fresh. */
    async start() {
      await page.goto(url);
      await page.waitForFunction(() => { const b = [...document.querySelectorAll('button')].find((x) => /new project/i.test(x.textContent ?? '')); return b && !b.disabled; }, null, { timeout: 60_000 });
      if (theme) await t.ui({ theme });
    },
    /** A new project, on its ground floor's plan. */
    async fresh() {
      await t.start();
      await page.getByRole('button', { name: /new project/i }).first().click();
      await page.waitForSelector('.plan-svg', { timeout: 30_000 });
      await t.wait(600);
    },
    /** The id of a storey by its name. */
    async storey(name = 'Ground floor') {
      const s = (await t.nodes()).find((n) => n.type === 'IfcBuildingStorey' && n.name === name);
      if (!s) throw new Error(`no storey "${name}"`);
      return s.id;
    },
    wait: (ms) => page.waitForTimeout(ms),
    /** Types keys as a user does (a tool's letters, Enter, Escape). */
    async keys(...keys) { for (const k of keys) await page.keyboard.press(k); await t.wait(150); },
    /** Clicks the button with this name (its text or its title). */
    async button(name, opts = {}) {
      const exact = opts.exact ?? false;
      // A tab the row has no room for is under More ▾ (decision 367): opened, and picked from its menu.
      if (await page.getByRole('button', { name, exact }).count() === 0 && await page.locator('[data-testid=tabs-more]').count() > 0) {
        await page.locator('[data-testid=tabs-more]').click();
        const item = page.getByRole('menuitem', { name, exact });
        if (await item.count() > 0) { await item.first().click(); await t.wait(opts.wait ?? 400); return; }
        await page.keyboard.press('Escape');
      }
      await page.getByRole('button', { name, exact }).first().click({ timeout: 10_000 });
      await t.wait(opts.wait ?? 400);
    },
    /** A ribbon tool by its id (architecture.wall, manage.materials…), as its button starts it. */
    async tool(id) {
      const ok = await page.evaluate(async (id) => { const m = await import('/src/platform/modules.ts'); const tool = m.findTool(id); if (tool) m.activate(tool); return Boolean(tool); }, id);
      if (!ok) throw new Error(`no tool ${id}`);
      await t.wait(600);
    },
    /** A point on the active storey's plan, metres in the model → pixels on the screen (the plan's own view, viewport/zoom.ts). */
    async planPoint(x, y) {
      const p = await page.evaluate(async ([x, y]) => {
        const svg = document.querySelector('[data-testid=plan]');
        const storey = (await import('/src/engine/session.ts')).activeStorey();
        const view = (await import('/src/viewport/zoom.ts')).recall(`plan:${storey?.id}`);
        if (!svg || !view) return null;
        const r = svg.getBoundingClientRect();
        return [r.left + (x - view.cx) * view.scale + r.width / 2, r.top + r.height / 2 - (y - view.cy) * view.scale];
      }, [x, y]);
      if (!p) throw new Error('no plan on the screen');
      return p;
    },
    /**
     * Numbered marks over the interface, for a picture that names its parts:
     * [{ of: selector, n: 1, at: 'center' | 'right' | 'left' | 'below' | 'bottom' | 'before' | 'after' }]
     * (right and left: inside the element's top corner; below: under it; bottom: inside its bottom edge; before and after: left and right of it).
     */
    async callouts(list) { marks = list; await place(list); },
    async clearCallouts() { marks = []; await unplace(); },
    /** Shows everything in the view on the screen. */
    async fit() { await page.locator('button[title^="Fit"]').first().click().catch(() => t.keys('z', 'e', 'Enter')); await t.wait(500); },
    /** Data a page shows (shots/<name>.json, in the scene's language): the default shortcuts' table. */
    data(name, value) { writeFileSync(join(folder, `${name}.json`), JSON.stringify(value, null, 2)); files.push(`${name}.json`); },
    /** Waits for something the engine sends in its own time (a preview), failing the scene as expect does if it never comes: a fixed wait is too short when several scenes run at once. */
    async until(ok, what, ms = 20_000) {
      const end = Date.now() + ms;
      for (;;) { if (await ok()) return; if (Date.now() > end) throw new Error(`expected: ${what}`); await page.waitForTimeout(200); }
    },
    /** Fails the scene when something is not as the guide says. */
    expect(ok, what) { if (!ok) throw new Error(`expected: ${what}`); },
    /**
     * A picture for the guide, shots/<name>.webp: the window, or a part of it
     * (a CSS selector or a locator, with `pad` pixels around it). `page`: another
     * page of the browser's (a page Lapin exported, opened from its file), taken
     * as it is: it speaks the language it was written in.
     */
    async shot(name, opts = {}) {
      if (lang === 'en' || opts.page) { await photograph(name, opts); return; }
      // The part to photograph is found while the interface is in English (its selector may name an English label or title, which the
      // language turns), and marked so it is found again in the picture's language.
      const target = typeof opts.of === 'string' ? page.locator(opts.of).first() : opts.of;
      if (target) await target.evaluate((e) => e.setAttribute('data-user-docs-shot', '')).catch(() => {});
      await speak(lang);
      try { await photograph(name, target ? { ...opts, of: '[data-user-docs-shot]' } : opts); } finally {
        await page.evaluate(() => document.querySelectorAll('[data-user-docs-shot]').forEach((e) => e.removeAttribute('data-user-docs-shot')));
        await speak('en');
      }
    },
  };
  /** The picture itself: the window, or a part of it. */
  async function photograph(name, opts) {
    await t.wait(opts.settle ?? 500);
    let clip;
    const from = opts.page ?? page;
    const target = typeof opts.of === 'string' ? from.locator(opts.of).first() : opts.of;
    if (target) {
      const b = await target.boundingBox();
      if (!b) throw new Error(`nothing to photograph for ${name} (${opts.of})`);
      const pad = opts.pad ?? 8;
      const vp = from.viewportSize();
      const x = Math.max(0, b.x - pad), y = Math.max(0, b.y - pad);
      clip = { x, y, width: Math.min(vp.width - x, b.width + 2 * pad), height: Math.min(vp.height - y, b.height + 2 * pad, opts.maxHeight ?? Infinity) };
    } else if (opts.clip) clip = opts.clip;
    const png = await from.screenshot({ clip, animations: 'disabled', caret: 'hide' });
    // WebP, made by the browser itself: a third of a PNG's size, the text as sharp.
    const webp = await page.evaluate(async (b64) => {
      const img = new Image(); img.src = `data:image/png;base64,${b64}`; await img.decode();
      const c = document.createElement('canvas'); c.width = img.naturalWidth; c.height = img.naturalHeight;
      c.getContext('2d').drawImage(img, 0, 0);
      const blob = await new Promise((r) => c.toBlob(r, 'image/webp', 0.9));
      const bytes = new Uint8Array(await blob.arrayBuffer());
      let s = ''; for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
      return btoa(s);
    }, png.toString('base64'));
    writeFileSync(join(folder, `${name}.webp`), Buffer.from(webp, 'base64'));
    taken.push(name);
    files.push(`${name}.webp`);
  }
  return t;
}
