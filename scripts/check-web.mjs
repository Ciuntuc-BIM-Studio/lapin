#!/usr/bin/env node
// The web build, checked as a tester uses it (docs/plan-browser-engine.md,
// stage 3): build/<id>/web served as static files, opened in headless Chrome;
// the engine must start in the worker within the budget, a new project must
// open, and a wall drawn with WA must show in the plan.
//
//   node scripts/check-web.mjs [id]       (default: lapin)
// CHROME: the browser to drive (default: Chrome where it is usually installed).
// LAPIN_WEB_START_BUDGET_MS: the start budget (default 4000; slow CI machines set more).
import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { existsSync, mkdtempSync, readFileSync, rmSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const id = process.argv[2] ?? 'lapin';
const web = join(root, 'build', id, 'web');
const budget = Number(process.env.LAPIN_WEB_START_BUDGET_MS ?? 4000);
if (!existsSync(join(web, 'index.html'))) { console.error(`check-web: no build/${id}/web (run pnpm build:web ${id}).`); process.exit(1); }
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// The folder, served as a static host would (wasm with its type).
const TYPES = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.wasm': 'application/wasm', '.png': 'image/png', '.svg': 'image/svg+xml', '.woff2': 'font/woff2' };
const server = createServer((req, res) => {
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
  };
  const send = (method, params = {}) => new Promise((r) => { const i = ++seq; pending.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); });
  const ev = async (expression) => (await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true })).result?.result?.value;
  const mouse = (type, x, y) => send('Input.dispatchMouseEvent', { type, x, y, button: 'left', clickCount: 1, buttons: type === 'mouseReleased' ? 0 : 1 });
  const click = async (x, y) => { await mouse('mouseMoved', x, y); await mouse('mousePressed', x, y); await mouse('mouseReleased', x, y); await sleep(150); };
  const key = async (k) => { await send('Input.dispatchKeyEvent', { type: 'keyDown', key: k, code: k.length === 1 ? `Key${k.toUpperCase()}` : k, text: k.length === 1 ? k : undefined }); await send('Input.dispatchKeyEvent', { type: 'keyUp', key: k, code: k.length === 1 ? `Key${k.toUpperCase()}` : k }); };
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
    }
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
