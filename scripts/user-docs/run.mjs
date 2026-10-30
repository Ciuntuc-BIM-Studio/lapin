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
//   pnpm docs:user --only walls    the scenes whose id starts with "walls" (the other pictures stay as they were)
//   pnpm docs:user --lang ro       one language's pictures (or several: --lang ro,de; the others' stay as they were)
//   pnpm docs:user --pages         the pages alone, from the pictures already taken
//
// CHROME: the browser to drive (default: the installed Google Chrome).
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import { dirname, join, resolve as resolvePath } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { chromium } from 'playwright-core';
import { resolveProduct } from '../product.mjs';
import { scenes as ownScenes } from './scenes.mjs';
import { render } from './render.mjs';
import { LANGS, checkInterfaceLanguages } from './languages.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '../..');
const out = join(root, 'build', 'user-docs');
const shots = join(out, 'shots');
const args = process.argv.slice(2);
const only = args.includes('--only') ? args[args.indexOf('--only') + 1] : null;
checkInterfaceLanguages(root);
// Another edition (a premium module's pictures, taken with its own scenes from its repository): --product lapin-urban --scenes ../lapin-urban/docs/scenes.mjs
const productId = args.includes('--product') ? args[args.indexOf('--product') + 1] : 'lapin';
const extra = args.includes('--scenes') ? (await import(pathToFileURL(resolvePath(args[args.indexOf('--scenes') + 1])).href)).scenes ?? [] : [];
const scenes = [...ownScenes, ...extra];
const langs = args.includes('--lang') ? String(args[args.indexOf('--lang') + 1]).split(',') : LANGS;
if (!langs.every((l) => LANGS.includes(l))) { console.error(`✗ --lang: one or more of ${LANGS.join(', ')}`); process.exit(1); }
/** A language's pictures: shots/ for English, shots/<id>/ for the others. */
const shotsOf = (lang) => (lang === 'en' ? shots : join(shots, lang));
// Every scene again: the pictures start from nothing, so none outlives the scene that took it.
for (const lang of langs) {
  if (!only && !args.includes('--pages')) for (const f of existsSync(shotsOf(lang)) ? readdirSync(shotsOf(lang)) : []) if (f.endsWith('.webp')) rmSync(join(shotsOf(lang), f));
  mkdirSync(shotsOf(lang), { recursive: true });
}

if (!args.includes('--pages')) await takePictures();
let failed = false;
for (const lang of LANGS) {
  let r;
  try { r = render(root, out, lang); } catch (e) { console.error(`✗ ${lang}: ${e instanceof Error ? e.message : e}`); failed = true; continue; }
  const { pages, missing, unused, english } = r;
  for (const u of unused) console.warn(`▸ ${lang}: a picture no page shows: ${u}`);
  if (missing.length) { console.error(`✗ ${lang}: pictures the pages show but no scene takes: ${missing.join(', ')}`); failed = true; }
  if (english.length) console.log(`▸ ${lang}: ${english.length} page(s) not translated yet, rendered from English: ${english.length === pages ? 'all' : english.join(', ')}`);
  console.log(`✓ ${lang}: ${pages} pages → build/user-docs${lang === 'en' ? '' : `/${lang}`}`);
}
if (failed) process.exit(1);

async function takePictures() {
  const free = () => new Promise((r) => { const s = createServer(); s.listen(0, '127.0.0.1', () => { const p = s.address().port; s.close(() => r(p)); }); });
  console.log('▸ building the engine…');
  if (spawnSync('dotnet', ['build', join(root, 'engine/Lapin.slnx'), '-v', 'q', '-nologo'], { stdio: 'inherit' }).status !== 0) process.exit(1);
  const product = resolveProduct(root, productId);
  // A module outside this repository is not in the solution: built on its own.
  for (const csproj of product.engineProjects ?? []) if (spawnSync('dotnet', ['build', csproj, '-v', 'q', '-nologo'], { stdio: 'inherit' }).status !== 0) process.exit(1);
  const temp = mkdtempSync(join(tmpdir(), 'lapin-user-docs-'));
  const manifest = join(temp, 'product.json');
  writeFileSync(manifest, JSON.stringify(product, null, 2));
  const [enginePort, uiPort, relayPort] = [await free(), await free(), await free()];
  const children = [];
  const stop = () => { for (const c of children) c.kill('SIGTERM'); };
  process.on('exit', stop);
  const ready = (child, text, name) => new Promise((resolve, reject) => {
    let seen = '';
    const look = (d) => { seen += d; if (seen.includes(text)) resolve(); };
    child.stdout.on('data', look); child.stderr.on('data', look);
    child.on('exit', (code) => reject(new Error(`${name} stopped (${code}): ${seen.slice(-800)}`)));
    setTimeout(() => reject(new Error(`${name} did not start: ${seen.slice(-800)}`)), 90_000);
  });
  const hostDll = join(root, 'engine/src/Host/Lapin.Host/bin/Debug/net10.0/Lapin.Host.dll');
  // A relay of its own for the shared session's pictures (docs/plan-multiplayer.md): the engine shares through it, the invitation opens this UI.
  const relay = spawn('dotnet', [join(root, 'engine/src/Relay/Lapin.Relay/bin/Debug/net10.0/Lapin.Relay.dll'), '--urls', `http://127.0.0.1:${relayPort}`]);
  children.push(relay);
  const engine = spawn('dotnet', [hostDll, '--product', manifest, '--modules-dir', join(root, 'engine/artifacts/modules'), '--user-dir', join(temp, 'user'), '--port', String(enginePort), '--parent-pid', String(process.pid)], {
    env: { ...process.env, LAPIN_RELAY: `ws://127.0.0.1:${relayPort}/relay`, LAPIN_JOIN_PAGE: `http://localhost:${uiPort}/` },
  });
  children.push(engine);
  const ui = spawn('pnpm', ['--filter', 'ui', 'exec', 'vite', '--port', String(uiPort), '--strictPort'], {
    cwd: root, env: { ...process.env, VITE_ENGINE_URL: `ws://127.0.0.1:${enginePort}/ws`, LAPIN_PRODUCT_FILE: manifest },
  });
  children.push(ui);
  await Promise.all([ready(engine, 'LAPIN_HOST_READY', 'the engine'), ready(ui, 'Local:', 'the UI')]);
  console.log(`▸ engine on ${enginePort}, UI on ${uiPort}`);

  const browser = await chromium.launch(process.env.CHROME ? { executablePath: process.env.CHROME } : { channel: 'chrome' });
  const failures = [];
  try {
    for (const lang of langs) for (const scene of scenes.filter((s) => !only || s.id.startsWith(only))) {
      const t0 = Date.now();
      const id = lang === 'en' ? scene.id : `${scene.id} (${lang})`;
      const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 2 });
      const errors = [];
      page.on('pageerror', (e) => errors.push(e.message));
      page.on('dialog', (d) => void d.accept());
      // Reports a scene might open never leave the machine.
      await page.route('**/api/feedback**', (r) => r.abort());
      try {
        const t = tools(page, `http://localhost:${uiPort}/`, shotsOf(lang), lang);
        await scene.run(t);
        if (errors.length) throw new Error(`errors in the page: ${errors.join(' | ')}`);
        console.log(`  ✓ ${id} (${t.taken.join(', ')}) ${((Date.now() - t0) / 1000).toFixed(1)} s`);
      } catch (e) {
        failures.push(id);
        const where = join(temp, `${scene.id}-${lang}-failed.png`);
        await page.screenshot({ path: where }).catch(() => {});
        console.error(`  ✗ ${id}: ${e instanceof Error ? e.message : e}\n    the page as it was: ${where}`);
      } finally {
        await page.close();
      }
    }
  } finally {
    await browser.close();
    stop();
  }
  if (failures.length) { console.error(`✗ ${failures.length} scene(s) failed: ${failures.join(', ')}`); process.exit(1); }
  rmSync(temp, { recursive: true, force: true });
}

/**
 * What a scene drives the app with. A scene always drives the interface in
 * English (its buttons are found by their English names); for another
 * language, the interface turns to it for each picture and back after.
 */
function tools(page, url, folder, lang) {
  const taken = [];
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
    page, taken, lang,
    /** A command of the engine, as the interface sends it. */
    cmd: (command, args = {}) => page.evaluate(async ([c, a]) => (await import('/src/engine/session.ts')).execute(c, a), [command, args]),
    /** A call of the engine's protocol (doc.snapshot, session.info…). */
    call: (method, params = {}) => page.evaluate(async ([m, p]) => (await import('/src/engine/session.ts')).engine.call(m, p), [method, params]),
    /** The interface's state, changed as its buttons change it. */
    ui: (patch) => page.evaluate(async (p) => (await import('/src/app/store.ts')).useUi.setState(p), patch),
    /** The project's elements as the interface holds them: [{ id, type, name, props }]. */
    nodes: () => page.evaluate(async () => [...(await import('/src/engine/session.ts')).useSession.getState().nodes.values()].map((n) => ({ id: n.id, type: n.type, name: n.name, parent: n.parent, props: n.props }))),
    /** The start screen, fresh. */
    async start() {
      await page.goto(url);
      await page.waitForFunction(() => { const b = [...document.querySelectorAll('button')].find((x) => /new project/i.test(x.textContent ?? '')); return b && !b.disabled; }, null, { timeout: 60_000 });
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
    async button(name, opts = {}) { await page.getByRole('button', { name, exact: opts.exact ?? false }).first().click({ timeout: 10_000 }); await t.wait(opts.wait ?? 400); },
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
    data(name, value) { writeFileSync(join(folder, `${name}.json`), JSON.stringify(value, null, 2)); },
    /** Fails the scene when something is not as the guide says. */
    expect(ok, what) { if (!ok) throw new Error(`expected: ${what}`); },
    /**
     * A picture for the guide, shots/<name>.webp: the window, or a part of it
     * (a CSS selector or a locator, with `pad` pixels around it).
     */
    async shot(name, opts = {}) {
      if (lang === 'en') { await photograph(name, opts); return; }
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
    const target = typeof opts.of === 'string' ? page.locator(opts.of).first() : opts.of;
    if (target) {
      const b = await target.boundingBox();
      if (!b) throw new Error(`nothing to photograph for ${name} (${opts.of})`);
      const pad = opts.pad ?? 8;
      const vp = page.viewportSize();
      const x = Math.max(0, b.x - pad), y = Math.max(0, b.y - pad);
      clip = { x, y, width: Math.min(vp.width - x, b.width + 2 * pad), height: Math.min(vp.height - y, b.height + 2 * pad, opts.maxHeight ?? Infinity) };
    } else if (opts.clip) clip = opts.clip;
    const png = await page.screenshot({ clip, animations: 'disabled', caret: 'hide' });
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
  }
  return t;
}
