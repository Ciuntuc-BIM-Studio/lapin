#!/usr/bin/env node
// The web build, checked as a tester uses it (docs/plan-browser-engine.md,
// stage 3): build/<id>/web served as static files, opened in headless Chrome;
// the engine must start in the worker within the budget, a new project must
// open, a wall drawn with WA must show in the plan, File → Export 3D… must
// download that wall as GLB, USDZ and DAE (docs/plan-mesh-export.md), and File →
// Export IFC or Fragments… as IFC (the browser's .NET lacks some of the
// platform's, MD5 among them: decision 258), and File → Link point cloud… must
// link a LAS picked as a user picks it, index it in the page and draw it in 3D
// (docs/point-clouds.md, decision 395). The lazy parts (docs/plan-browser-engine.md, Lazy parts): none is fetched
// to open a project (LAPIN_WEB_PROJECT, else the user's testLapin.lapin when it is there) or to draw in one, the
// 3D exchange is fetched by Export 3D, a BIM Open Schema file exported comes into a new project, and a part or a
// dialog that cannot be fetched says so, then comes when it can.
//
//   node scripts/check-web.mjs [id]       (default: lapin)
// CHROME: the browser to drive (default: Chrome where it is usually installed).
// LAPIN_WEB_START_BUDGET_MS: the start budget (default 4000; slow CI machines set more).
import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { homedir, tmpdir } from 'node:os';
import { dirname, extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const id = process.argv[2] ?? 'lapin';
const web = join(root, 'build', id, 'web');
const budget = Number(process.env.LAPIN_WEB_START_BUDGET_MS ?? 4000);
const project = process.env.LAPIN_WEB_PROJECT ?? join(homedir(), 'Documents/GitHub/Lapin/modele/testLapin.lapin');
if (!existsSync(join(web, 'index.html'))) { console.error(`check-web: no build/${id}/web (run pnpm build:web ${id}).`); process.exit(1); }
// Every command and query the interface calls by name must exist in the engine (this host runs the same modules): decision 434.
{
  const { unanswered } = await import('./check-commands.mjs');
  const bad = unanswered();
  if (bad.length) { for (const b of bad) console.error(`check-web: ${b.file}:${b.line} ${b.mod}.${b.name}: ${b.why}`); process.exit(1); }
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** A LAS 1.2 of 5 000 points (format 2, coloured): a floor 10 × 10 m and a wall on it. */
function smallLas() {
  const n = 5000, len = 26, head = 227;
  const b = Buffer.alloc(head + n * len);
  b.write('LASF', 0, 'ascii'); b[24] = 1; b[25] = 2;
  b.writeUInt16LE(head, 94); b.writeUInt32LE(head, 96); b[104] = 2; b.writeUInt16LE(len, 105); b.writeUInt32LE(n, 107);
  for (let k = 0; k < 3; k++) { b.writeDoubleLE(0.001, 131 + 8 * k); b.writeDoubleLE(0, 155 + 8 * k); }
  b.writeDoubleLE(10, 179); b.writeDoubleLE(0, 187); b.writeDoubleLE(10, 195); b.writeDoubleLE(0, 203); b.writeDoubleLE(3, 211); b.writeDoubleLE(0, 219);
  for (let i = 0; i < n; i++) {
    const o = head + i * len, wall = i % 3 === 0;
    const x = (i * 7919 % 10000) / 1000, y = wall ? 0 : (i * 104729 % 10000) / 1000, z = wall ? (i * 31 % 3000) / 1000 : 0;
    b.writeInt32LE(Math.round(x * 1000), o); b.writeInt32LE(Math.round(y * 1000), o + 4); b.writeInt32LE(Math.round(z * 1000), o + 8);
    b[o + 15] = wall ? 6 : 2;
    b.writeUInt16LE(wall ? 200 * 257 : 90 * 257, o + 20); b.writeUInt16LE(wall ? 190 * 257 : 140 * 257, o + 22); b.writeUInt16LE(wall ? 180 * 257 : 60 * 257, o + 24);
  }
  return b;
}

// The folder, served as a static host would (wasm with its type).
const TYPES = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.wasm': 'application/wasm', '.png': 'image/png', '.svg': 'image/svg+xml', '.woff2': 'font/woff2' };
/** Files the server refuses for now, as an offline moment would (a lazy part that cannot be fetched). */
let refused = null;
const server = createServer((req, res) => {
  if (refused?.test(req.url)) { res.writeHead(503); res.end(); return; }
  let p = normalize(decodeURIComponent(new URL(req.url, 'http://x').pathname)).replace(/^[/\\]+/, '') || 'index.html';
  const f = join(web, p);
  if (!f.startsWith(web) || !existsSync(f) || statSync(f).isDirectory()) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { 'content-type': TYPES[extname(f)] ?? 'application/octet-stream' });
  res.end(readFileSync(f));
});
await new Promise((r) => server.listen(0, '127.0.0.1', r));
const url = `http://127.0.0.1:${server.address().port}/`;

const chromePath = process.env.CHROME ?? (process.platform === 'darwin' ? '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
  : process.platform === 'win32' ? 'C:/Program Files/Google/Chrome/Application/chrome.exe' : 'google-chrome');
const profile = mkdtempSync(join(tmpdir(), 'lapin-check-web-'));
const port = 9300 + Math.floor(Math.random() * 500);
const chrome = spawn(chromePath, ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`, '--no-first-run', '--no-sandbox', 'about:blank'], { stdio: 'ignore' });
let failed = false;
const fail = (m) => { console.error(`check-web: ${m}`); failed = true; };
try {
  let tabs;
  for (let k = 0; k < 60 && !tabs; k++) { await sleep(250); try { tabs = await (await fetch(`http://127.0.0.1:${port}/json`)).json(); } catch { /* not yet */ } }
  if (!tabs) throw new Error(`Chrome did not start (${chromePath}).`);
  const ws = new WebSocket(tabs.find((t) => t.type === 'page').webSocketDebuggerUrl);
  await new Promise((r, j) => { ws.onopen = r; ws.onerror = j; });
  let seq = 0;
  const pending = new Map();
  const errors = [];
  ws.onmessage = (e) => {
    const m = JSON.parse(e.data);
    if (pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); }
    if (m.method === 'Runtime.exceptionThrown') errors.push(JSON.stringify(m.params.exceptionDetails).slice(0, 300));
    // Leaving with unsaved work asks first (the page's beforeunload): the check says leave.
    if (m.method === 'Page.javascriptDialogOpening') ws.send(JSON.stringify({ id: ++seq, method: 'Page.handleJavaScriptDialog', params: { accept: true } }));
  };
  const send = (method, params = {}) => new Promise((r) => { const i = ++seq; pending.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); });
  const ev = async (expression) => (await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true })).result?.result?.value;
  const mouse = (type, x, y) => send('Input.dispatchMouseEvent', { type, x, y, button: 'left', clickCount: 1, buttons: type === 'mouseReleased' ? 0 : 1 });
  const click = async (x, y) => { await mouse('mouseMoved', x, y); await mouse('mousePressed', x, y); await mouse('mouseReleased', x, y); await sleep(150); };
  const key = async (k) => { await send('Input.dispatchKeyEvent', { type: 'keyDown', key: k, code: k.length === 1 ? `Key${k.toUpperCase()}` : k, text: k.length === 1 ? k : undefined }); await send('Input.dispatchKeyEvent', { type: 'keyUp', key: k, code: k.length === 1 ? `Key${k.toUpperCase()}` : k }); };
  /** The lazy parts the engine has fetched so far (session.ts writes them on the page). */
  const parts = async () => String(await ev(`document.documentElement.dataset.lapinParts ?? ''`)).split(' ').filter(Boolean);
  /** A File menu item clicked as a user does (a file input opens only on a real click). */
  const fileItem = async (re) => {
    await key('Escape'); await key('Escape');
    await sleep(300);
    for (let k = 0; k < 10 && !(await ev(`!![...document.querySelectorAll('[role=menuitem]')].find(x => ${re}.test(x.textContent))`)); k++) {
      await ev(`[...document.querySelectorAll('button')].find(x => x.textContent.trim() === 'File')?.click()`);
      await sleep(300);
    }
    const item = JSON.parse(await ev(`JSON.stringify([...document.querySelectorAll('[role=menuitem]')].find(x => ${re}.test(x.textContent))?.getBoundingClientRect() ?? null)`) ?? 'null');
    if (item) await click(item.x + item.width / 2, item.y + item.height / 2);
    return Boolean(item);
  };
  let chooser = null;
  /** A file given to the page's file input when the item asks for one; false when none was asked for. */
  const pick = async (re, file, clickIt = () => fileItem(re)) => {
    if (!chooser) {
      await send('Page.enable');
      await send('Page.setInterceptFileChooserDialog', { enabled: true });
      // The page's own picker is put aside so the file input (which the test can fill) is used, as in browsers without one.
      await ev(`Object.defineProperty(window, "showOpenFilePicker", { value: undefined, configurable: true })`);
      chooser = { waiting: null };
      const old = ws.onmessage;
      ws.onmessage = (e) => { old(e); const m = JSON.parse(e.data); if (m.method === 'Page.fileChooserOpened') chooser.waiting?.(m.params.backendNodeId); };
    }
    const chosen = new Promise((r) => { chooser.waiting = r; });
    await clickIt();
    const node = await Promise.race([chosen, sleep(5000).then(() => null)]);
    if (!node) return false;
    await send('DOM.setFileInputFiles', { files: [file], backendNodeId: node });
    return true;
  };
  await send('Runtime.enable');
  await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 950, deviceScaleFactor: 1, mobile: false });

  // 1. The engine starts in its worker.
  const t0 = Date.now();
  await send('Page.navigate', { url });
  const newButton = `[...document.querySelectorAll('button')].find(x => /new project/i.test(x.textContent))`;
  let started = false;
  for (let i = 0; i < 240 && !started; i++) { await sleep(250); started = Boolean(await ev(`(() => { const b = ${newButton}; return b && !b.disabled && !document.body.innerText.includes('Starting the engine'); })()`)); }
  const startMs = Date.now() - t0;
  if (!started) fail(`the engine did not start in 60 s. ${errors.join(' | ')}`);
  else console.log(`▸ engine started in ${startMs} ms (budget ${budget} ms)`);
  if (started && startMs > budget) fail(`the start took ${startMs} ms, over the ${budget} ms budget.`);

  // 1b. A project opened as a user opens it, its plan drawn: nothing lazy fetched for it.
  if (started && existsSync(project)) {
    // The start screen's Open project…, clicked as a user does.
    const openButton = async () => {
      const b = JSON.parse(await ev(`JSON.stringify([...document.querySelectorAll('button')].find(x => /Open project/.test(x.textContent))?.getBoundingClientRect() ?? null)`) ?? 'null');
      if (b) await click(b.x + b.width / 2, b.y + b.height / 2);
    };
    if (!(await pick(null, project, openButton))) fail('Open project… did not ask for a file.');
    else {
      let cut = 0;
      for (let k = 0; k < 60 && !(cut > 0); k++) {
        await sleep(250);
        await ev(`[...document.querySelectorAll('.view-tab')].find(x => /^Plan/.test(x.textContent))?.click()`);
        cut = Number(await ev(`document.querySelectorAll('.plan-item.cut').length`));
      }
      if (!(cut > 0)) fail(`${project} opened, but its plan shows nothing cut. ${errors.join(' | ')}`);
      else console.log(`▸ ${project.split('/').pop()} opened: ${cut} cut band(s) in its plan`);
      const p = await parts();
      if (p.length) fail(`starting and opening a project fetched lazy parts: ${p.join(', ')}.`);
    }
    // The page again, for the start screen's New project (the project opened stays where it was).
    chooser = null;
    await send('Page.setInterceptFileChooserDialog', { enabled: false });
    await send('Page.navigate', { url });
    let again = false;
    for (let i = 0; i < 240 && !again; i++) { await sleep(250); again = Boolean(await ev(`(() => { const b = ${newButton}; return b && !b.disabled && !document.body.innerText.includes('Starting the engine'); })()`)); }
    if (!again) fail('the page did not start again after the project was opened.');
  } else if (started) console.log(`▸ (no ${project}: the project's opening is not checked; LAPIN_WEB_PROJECT names one)`);

  // 2. A new project, a wall drawn with WA on its plan.
  if (started) {
    await ev(`${newButton}.click()`);
    await sleep(1500);
    await ev(`[...document.querySelectorAll('.view-tab')].find(x => /^Plan/.test(x.textContent))?.click()`);
    await sleep(800);
    const box = await ev(`JSON.stringify(document.querySelector('.plan-svg')?.getBoundingClientRect() ?? null)`);
    const r = box && JSON.parse(box);
    if (!r) fail('no plan after a new project.');
    else {
      for (const k of 'wa') await key(k);
      await click(r.x + r.width / 2 - 150, r.y + r.height / 2);
      await click(r.x + r.width / 2 + 150, r.y + r.height / 2);
      await key('Escape'); await key('Escape');
      await sleep(1500);
      const cut = await ev(`document.querySelectorAll('.plan-item.cut').length`);
      if (!(cut > 0)) fail(`the wall drawn did not show in the plan. ${errors.join(' | ')}`);
      else console.log(`▸ a wall drawn with WA: ${cut} cut band(s) in the plan`);
      const p = await parts();
      if (p.length) fail(`drawing a wall fetched lazy parts: ${p.join(', ')}.`);
    }
  }
  // 3. File → Export 3D…, as a tester does it: each format downloaded, and what it starts with.
  if (started && !failed) {
    const downloads = mkdtempSync(join(tmpdir(), 'lapin-check-web-dl-'));
    await send('Browser.setDownloadBehavior', { behavior: 'allow', downloadPath: downloads });
    // Exports ask for a file name first (decision 509): no native picker here, the Save as… box answered with OK.
    await ev(`(() => { window.showSaveFilePicker = undefined; setInterval(() => { document.querySelector('.ask-box[aria-label="Save as…"] .btn.primary')?.click(); }, 150); })()`);
    const MAGIC = { glb: (b) => b.readUInt32LE(0) === 0x46546c67, usdz: (b) => b.readUInt32LE(0) === 0x04034b50 && b.toString('utf8', 30, 40) === 'model.usda', dae: (b) => b.toString('utf8', 0, 200).includes('<COLLADA') };
    for (const format of ['glb', 'usdz', 'dae']) {
      await ev(`[...document.querySelectorAll('button')].find(x => x.textContent.trim() === 'File')?.click()`);
      await sleep(200);
      await ev(`[...document.querySelectorAll('[role=menuitem]')].find(x => /Export 3D/.test(x.textContent))?.click()`);
      await sleep(400);
      const chosen = await ev(`(() => { const s = document.querySelector('[aria-label="Export 3D"] select'); if (!s) return false;
        Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(s, '${format}'); s.dispatchEvent(new Event('change', { bubbles: true })); return true; })()`);
      if (!chosen) { fail('File → Export 3D… did not open its dialog.'); break; }
      await sleep(200);
      await ev(`[...document.querySelectorAll('[aria-label="Export 3D"] button')].find(x => x.textContent.trim() === 'Export')?.click()`);
      let file = null;
      for (let k = 0; k < 80 && !file; k++) { await sleep(250); file = readdirSync(downloads).find((f) => f.endsWith(`.${format}`)) ?? null; }
      const bytes = file ? readFileSync(join(downloads, file)) : null;
      if (!bytes || bytes.length < 100) fail(`Export 3D as ${format.toUpperCase()} downloaded nothing. ${errors.join(' | ')}`);
      else if (!MAGIC[format](bytes)) fail(`Export 3D as ${format.toUpperCase()} is not a ${format} file.`);
      else console.log(`▸ Export 3D as ${format.toUpperCase()}: ${file}, ${bytes.length} bytes`);
    }
    if (!failed) {
      const p = await parts();
      if (!p.includes('Lapin.Mesh')) fail(`Export 3D worked without fetching the 3D exchange (Lapin.Mesh): is it still a lazy part? Fetched: ${p.join(', ') || 'none'}.`);
      else console.log(`▸ lazy parts fetched by Export 3D: ${p.join(', ')}`);
    }
    // File → Export IFC, Fragments or BOS…, IFC chosen as it is by default.
    if (!failed) {
      await ev(`[...document.querySelectorAll('button')].find(x => x.textContent.trim() === 'File')?.click()`);
      await sleep(200);
      await ev(`[...document.querySelectorAll('[role=menuitem]')].find(x => /Export IFC/.test(x.textContent))?.click()`);
      await sleep(600);
      const clicked = await ev(`(() => { const b = [...document.querySelectorAll('[aria-label^="Export IFC"] button')].find(x => x.textContent.trim() === 'Export'); if (!b || b.disabled) return false; b.click(); return true; })()`);
      if (!clicked) fail('File → Export IFC, Fragments or BOS… did not open its dialog, or it has nothing to export.');
      else {
        let file = null;
        for (let k = 0; k < 120 && !file; k++) { await sleep(250); file = readdirSync(downloads).find((f) => f.endsWith('.ifc')) ?? null; }
        const bytes = file ? readFileSync(join(downloads, file)) : null;
        if (!bytes || bytes.length < 100) fail(`Export IFC downloaded nothing. ${errors.join(' | ')}`);
        else if (!bytes.toString('utf8', 0, 20).startsWith('ISO-10303-21')) fail('Export IFC is not an IFC (STEP) file.');
        else console.log(`▸ Export IFC: ${file}, ${bytes.length} bytes`);
      }
    }
    // File → Lapin for the browser (.html)…: the page with its 3D model and the project after it (decision 482).
    if (!failed) {
      await ev(`[...document.querySelectorAll('button')].find(x => x.textContent.trim() === 'File')?.click()`);
      await sleep(200);
      await ev(`[...document.querySelectorAll('[role=menuitem]')].find(x => /Lapin for the browser/.test(x.textContent))?.click()`);
      let clicked = false;
      for (let k = 0; k < 120 && !clicked; k++) {
        await sleep(250);
        clicked = Boolean(await ev(`(() => { const b = [...document.querySelectorAll('[aria-label="Lapin for the browser"] button')].find(x => x.textContent.trim() === 'Export'); if (!b || b.disabled) return false; b.click(); return true; })()`));
      }
      if (!clicked) fail('File → Lapin for the browser (.html)… did not open its dialog, or it has nothing to export.');
      else {
        let file = null;
        for (let k = 0; k < 120 && !file; k++) { await sleep(250); file = readdirSync(downloads).find((f) => f.endsWith('.html')) ?? null; }
        const text = file ? readFileSync(join(downloads, file), 'latin1') : '';
        if (text.length < 1000) fail(`Lapin for the browser downloaded nothing. ${errors.join(' | ')}`);
        else if (!/<html/i.test(text.slice(0, 2000)) || !text.includes('<plaintext')) fail('Lapin for the browser is not a page with its project.');
        else console.log(`▸ Lapin for the browser: ${file}, ${text.length} bytes`);
      }
    }
    // A BIM Open Schema file: exported, then imported into a new project.
    if (!failed) {
      await ev(`[...document.querySelectorAll('button')].find(x => x.textContent.trim() === 'File')?.click()`);
      await sleep(200);
      await ev(`[...document.querySelectorAll('[role=menuitem]')].find(x => /Export IFC/.test(x.textContent))?.click()`);
      await sleep(600);
      await ev(`[...document.querySelectorAll('[aria-label="Export IFC, Fragments or BOS"] input[type=radio]')][2]?.click()`);
      await sleep(400);
      await ev(`[...document.querySelectorAll('[aria-label="Export IFC, Fragments or BOS"] button')].find(x => x.textContent.trim() === 'Export')?.click()`);
      let file = null;
      for (let k = 0; k < 120 && !file; k++) { await sleep(250); file = readdirSync(downloads).find((f) => f.endsWith('.bos')) ?? null; }
      if (!file) fail(`Export as BIM Open Schema downloaded nothing. ${errors.join(' | ')}`);
      else {
        // Another session: the file comes into a new project, the 3D exchange fetched again for it.
        chooser = null;
        await send('Page.setInterceptFileChooserDialog', { enabled: false });
        await send('Page.navigate', { url });
        let again = false;
        for (let i = 0; i < 240 && !again; i++) { await sleep(250); again = Boolean(await ev(`(() => { const b = ${newButton}; return b && !b.disabled && !document.body.innerText.includes('Starting the engine'); })()`)); }
        if (!again) fail('the page did not start again before the import.');
        await ev(`${newButton}.click()`);
        await sleep(1500);
        if (!(await pick('/Import BIM Open Schema/', join(downloads, file)))) fail('File → Import BIM Open Schema… did not ask for a file.');
        else {
          let clicked = false;
          for (let k = 0; k < 80 && !clicked; k++) { await sleep(250); clicked = Boolean(await ev(`(() => { const b = [...document.querySelectorAll('.import-ifc button')].find(x => x.textContent.trim() === 'Import'); if (!b || b.disabled) return false; b.click(); return true; })()`)); }
          // What the import says of itself (fragOutcome): the file by its name, and what came in.
          let said = '';
          for (let k = 0; k < 80 && !said.includes(file); k++) { await sleep(250); said = String(await ev(`document.querySelector('.notice-status')?.textContent ?? ''`)); }
          if (!clicked) fail(`the BIM Open Schema survey did not offer Import. ${errors.join(' | ')}`);
          else if (!said.includes(file) || said.startsWith('Nothing new')) fail(`the BIM Open Schema import brought nothing from ${file} (last: ${said}). ${errors.join(' | ')}`);
          else console.log(`▸ BIM Open Schema exported and imported into a new project: ${said} (parts: ${(await parts()).join(', ')})`);
        }
      }
    }
    rmSync(downloads, { recursive: true, force: true });
  }
  // 4. File → Link point cloud…: a small LAS picked through the browser's file input, indexed in the worker, drawn in 3D.
  if (started && !failed) {
    const dir = mkdtempSync(join(tmpdir(), 'lapin-check-web-cloud-'));
    const las = join(dir, 'scan.las');
    writeFileSync(las, smallLas());
    // A dialog's chunk that cannot be fetched says so and offers to try again.
    refused = /JgExportDialog/;
    await fileItem('/Export JG Structures/');
    let told = false;
    for (let k = 0; k < 40 && !told; k++) { await sleep(150); told = Boolean(await ev(`/could not be downloaded/.test(document.querySelector('[role=dialog] .error-text')?.textContent ?? '')`)); }
    if (!told) fail(`a dialog whose chunk cannot be fetched does not say so (dialogs: ${await ev(`[...document.querySelectorAll("[role=dialog]")].map(x => x.textContent.slice(0, 80)).join(" | ")`)}).`);
    refused = null;
    await ev(`[...document.querySelectorAll('[role=dialog] button')].find(x => x.textContent.trim() === 'Try again')?.click()`);
    let opened = false;
    for (let k = 0; k < 40 && !opened; k++) { await sleep(150); opened = Boolean(await ev(`!!document.querySelector('.jg-export')`)); }
    if (told && !opened) fail('Try again did not bring the dialog when its chunk could be fetched.');
    else if (told) console.log('▸ a dialog that cannot be fetched says so, and Try again brings it');
    await ev(`document.querySelector('[role=dialog] button[title=Close]')?.click()`);
    await sleep(200);
    // A part of the engine that cannot be fetched says so, each time it is reached for (the runtime keeps the refusal);
    // the engine goes on working, and a new page, the part back, links.
    refused = /Lapin\.PointClouds/;
    for (const time of ['first', 'second']) {
      if (!(await pick('/Link point cloud/', las))) { fail('File → Link point cloud… did not ask for a file.'); break; }
      for (let k = 0; k < 40 && !(await ev(`!!document.querySelector('.ask-box')`)); k++) await sleep(150);
      await ev(`[...document.querySelectorAll('.ask-box button')].find(x => x.textContent.trim() === 'OK')?.click()`);
      let said = '';
      for (let k = 0; k < 60 && !/reload the page/.test(said); k++) { await sleep(150); said = String(await ev(`document.querySelector('.notice-status')?.textContent ?? ''`)); }
      if (!/reload the page/.test(said)) fail(`the ${time} time, a part of the engine that cannot be fetched does not say so (last: ${said}). ${errors.join(' | ')}`);
      else console.log(`▸ a part of the engine that cannot be fetched says so (${time} time)`);
      await sleep(300);
    }
    refused = null;
    chooser = null;
    await send('Page.setInterceptFileChooserDialog', { enabled: false });
    await send('Page.navigate', { url });
    let back = false;
    for (let i = 0; i < 240 && !back; i++) { await sleep(250); back = Boolean(await ev(`(() => { const b = ${newButton}; return b && !b.disabled && !document.body.innerText.includes('Starting the engine'); })()`)); }
    if (!back) fail('the page did not start again after the part was refused.');
    await ev(`${newButton}.click()`);
    await sleep(1500);
    const asked = await pick('/Link point cloud/', las);
    if (!asked) fail(`File → Link point cloud… did not ask for a file (dialogs: ${await ev(`[...document.querySelectorAll("[role=dialog]")].map(x => x.getAttribute("aria-label")).join(", ")`)}; buttons: ${await ev(`[...document.querySelectorAll("button")].slice(0, 12).map(x => x.textContent.trim()).join(", ")`)}).`);
    else {
      let box = false;
      for (let k = 0; k < 40 && !box; k++) { await sleep(150); box = Boolean(await ev(`!!document.querySelector('.ask-box')`)); }
      await ev(`[...document.querySelectorAll('.ask-box button')].find(x => x.textContent.trim() === 'OK')?.click()`);
      await ev(`[...document.querySelectorAll('.view-tab')].find(x => /^3D/.test(x.textContent))?.click()`);
      let drawn = 0;
      for (let k = 0; k < 80 && !(drawn > 0); k++) { await sleep(250); drawn = Number(await ev(`document.querySelector('[data-testid=view3d]')?.dataset.cloudPoints ?? 0`)); }
      if (!(drawn > 0)) fail(`the point cloud linked in the browser is not drawn in 3D. ${errors.join(' | ')}`);
      else console.log(`▸ File → Link point cloud…: a LAS of 5 000 points indexed in the page, ${drawn} drawn in 3D`);
      if (!(await parts()).includes('Lapin.PointClouds')) fail('the point cloud was linked without fetching Lapin.PointClouds: is it still a lazy part?');
    }
    rmSync(dir, { recursive: true, force: true });
  }
  if (errors.length) fail(`exceptions in the page: ${errors.join(' | ')}`);
  ws.close();
} catch (e) {
  fail(e instanceof Error ? e.message : String(e));
} finally {
  chrome.kill();
  server.close();
  await sleep(300);
  rmSync(profile, { recursive: true, force: true });
}
if (failed) process.exit(1);
console.log(`▸ build/${id}/web works in the browser`);
