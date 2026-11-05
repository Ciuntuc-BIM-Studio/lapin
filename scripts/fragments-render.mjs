#!/usr/bin/env node
/**
 * The cross-check of a .frag in That Open's own viewer, repeatable (docs/fragments.md):
 *
 *   node scripts/fragments-render.mjs <out folder> <a.frag | a.glb>... [--modules <node_modules>] [--views iso,top,front,back]
 *
 * Serves the files and the official library (@thatopen/fragments, three, web-ifc, from BubbleBIM
 * Standalone's node_modules by default) to headless Chrome, loads each .frag as BubbleBIM Standalone's
 * viewer does (FragmentsModels with its worker, `core.load(bytes, {modelId, raw})`, the model's object in
 * the scene, `core.update(true)`), each .glb with three's GLTFLoader (Lapin's own geometry), and writes,
 * per view, one picture with the files side by side (`<out>/<view>.png`) and what the viewer saw
 * (`<out>/report.json`: items with geometry, categories, the box, triangles drawn, materials).
 * Files are framed by the box of all of them, so a misplaced one shows where it is.
 */

import { createServer } from 'node:http';
import { readFileSync, writeFileSync, mkdirSync, existsSync, statSync } from 'node:fs';
import { resolve, extname, basename } from 'node:path';
import { chromium } from 'playwright-core';

const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf(name); if (i < 0) return def; const v = args[i + 1]; args.splice(i, 2); return v; };
const modules = resolve(opt('--modules', resolve(process.cwd(), '../BubbleBIM-Standalone/node_modules')));
const views = opt('--views', 'iso,top,front,back').split(',');
const [out, ...files] = args;
if (!out || files.length === 0) { console.error('usage: fragments-render.mjs <out folder> <a.frag | a.glb>... [--modules <node_modules>] [--views iso,top,front,back]'); process.exit(2); }
mkdirSync(out, { recursive: true });
const version = JSON.parse(readFileSync(resolve(modules, '@thatopen/fragments/package.json'), 'utf8')).version;

const page = `<!doctype html><html><head><meta charset="utf-8">
<script type="importmap">{"imports":{"three":"/m/three/build/three.module.js","three/examples/jsm/":"/m/three/examples/jsm/","three/addons/":"/m/three/examples/jsm/","web-ifc":"/m/web-ifc/web-ifc-api.js"}}</script>
<style>html,body{margin:0;background:#fff}canvas{display:block}</style></head><body>
<script type="module">
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import * as FRAGS from '/m/@thatopen/fragments/dist/index.mjs';
const files = ${JSON.stringify(files.map((f) => basename(f)))};
const W = 480, H = 400;
const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
renderer.setSize(W * files.length, H);
renderer.setScissorTest(true);
document.body.appendChild(renderer.domElement);
const fragments = new FRAGS.FragmentsModels('/m/@thatopen/fragments/dist/Worker/worker.mjs');
// Each file in its own frame: the viewer would move a second model by the difference of their coordinates (autoCoordinate); report.json gives each file's coordinates.
fragments.settings.autoCoordinate = false;
const panes = [];
const report = [];
for (const [i, name] of files.entries()) {
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0xf2f2f2);
  scene.add(new THREE.HemisphereLight(0xffffff, 0x888888, 2.2));
  const sun = new THREE.DirectionalLight(0xffffff, 1.6); sun.position.set(3, 5, 2); scene.add(sun);
  const camera = new THREE.PerspectiveCamera(40, W / H, 0.05, 1e7);
  const bytes = new Uint8Array(await (await fetch('/f/' + i)).arrayBuffer());
  const r = { file: name, bytes: bytes.length };
  try {
    if (name.endsWith('.glb')) {
      const gltf = await new GLTFLoader().parseAsync(bytes.buffer, '');
      scene.add(gltf.scene);
      let tris = 0; const mats = new Set();
      gltf.scene.traverse((o) => { if (o.isMesh) { tris += (o.geometry.index ? o.geometry.index.count : o.geometry.attributes.position.count) / 3; mats.add(o.material); } });
      const box = new THREE.Box3().setFromObject(gltf.scene);
      Object.assign(r, { kind: 'glb (three GLTFLoader)', triangles: tris, materials: mats.size, box: [box.min.toArray(), box.max.toArray()] });
      panes.push({ scene, camera, box });
    } else {
      const raw = bytes[0] !== 0x78;
      const model = await fragments.load(bytes, { modelId: 'm' + i, camera, raw });
      scene.add(model.object);
      await fragments.update(true);
      const ids = await model.getItemsIdsWithGeometry();
      // In the scene's frame, the library's own move included (a model after the first is moved by their coordinates' difference, autoCoordinate, as in the viewer).
      const box = model.box.clone();
      const geoms = ids.length ? await model.getItemsGeometry(ids) : [];
      let tris = 0; for (const g of geoms) for (const m of g) tris += (m.indices?.length ?? 0) / 3;
      const mats = ids.length ? await model.getItemsMaterialDefinition(ids) : [];
      const boxes = ids.length ? await model.getBoxes(ids.slice(0, 5)) : [];
      Object.assign(r, { kind: 'frag (FragmentsModels ${version})', raw, itemsWithGeometry: ids.length, categories: await model.getCategories(),
        triangles: tris, materials: mats.length, materialDefinitions: mats.slice(0, 12).map((m) => m.definition ?? m),
        box: [box.min.toArray(), box.max.toArray()], firstBoxes: boxes.map((b) => [b.min.toArray(), b.max.toArray()]),
        coordinates: await model.getCoordinates(), shift: model.object.position.toArray(), metadata: await model.getMetadata?.() });
      panes.push({ scene, camera, box, model });
    }
  } catch (e) { r.error = String(e?.stack ?? e); panes.push({ scene, camera, box: new THREE.Box3(new THREE.Vector3(-1, -1, -1), new THREE.Vector3(1, 1, 1)) }); }
  report.push(r);
}
const all = new THREE.Box3(); for (const p of panes) if (!p.box.isEmpty()) all.union(p.box);
const dirs = { iso: [1, 0.8, 1], top: [0, 1, 0.0001], front: [0, 0.15, 1], back: [-0.6, 0.5, -1] };
window.render = async (view) => {
  const d = new THREE.Vector3(...dirs[view]).normalize();
  for (const [i, p] of panes.entries()) {
    const box = p.box.isEmpty() ? all : p.box;
    const c = box.getCenter(new THREE.Vector3()), size = box.getSize(new THREE.Vector3()).length();
    p.camera.position.copy(c).addScaledVector(d, size * 1.15);
    p.camera.up.set(0, 1, 0); p.camera.lookAt(c); p.camera.updateProjectionMatrix();
  }
  // Until every model's tiles are in (a big model streams them over many updates), at least six frames.
  for (let k = 0; k < 400 && (k < 6 || panes.some((p) => p.model?.isBusy)); k++) {
    await fragments.update(true);
    for (const [i, p] of panes.entries()) { renderer.setViewport(i * W, 0, W, H); renderer.setScissor(i * W, 0, W, H); renderer.render(p.scene, p.camera); }
    await new Promise((r) => setTimeout(r, 120));
  }
  return renderer.domElement.toDataURL('image/png');
};
window.report = report;
window.ready = true;
</script></body></html>`;

const types = { '.mjs': 'text/javascript', '.js': 'text/javascript', '.wasm': 'application/wasm', '.json': 'application/json' };
const server = createServer((req, res) => {
  const url = decodeURIComponent(req.url.split('?')[0]);
  let path = null;
  if (url === '/') { res.writeHead(200, { 'content-type': 'text/html' }); res.end(page); return; }
  if (url.startsWith('/m/')) path = resolve(modules, url.slice(3));
  else if (url.startsWith('/f/')) path = resolve(files[Number(url.slice(3))]);
  if (!path || !path.startsWith(url.startsWith('/m/') ? modules : '/') || !existsSync(path) || !statSync(path).isFile()) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { 'content-type': types[extname(path)] ?? 'application/octet-stream' });
  res.end(readFileSync(path));
});
await new Promise((r) => server.listen(0, '127.0.0.1', r));
const browser = await chromium.launch(process.env.CHROME ? { executablePath: process.env.CHROME } : { channel: 'chrome', args: ['--use-angle=metal', '--enable-unsafe-swiftshader'] });
try {
  const tab = await browser.newPage();
  const logs = [];
  tab.on('console', (m) => logs.push(`${m.type()}: ${m.text()}`));
  tab.on('pageerror', (e) => logs.push(`pageerror: ${e.message}`));
  await tab.goto(`http://127.0.0.1:${server.address().port}/`);
  await tab.waitForFunction(() => window.ready === true, null, { timeout: 120_000 });
  for (const v of views) {
    const data = await tab.evaluate((view) => window.render(view), v);
    writeFileSync(resolve(out, `${v}.png`), Buffer.from(data.split(',')[1], 'base64'));
  }
  const report = await tab.evaluate(() => window.report);
  writeFileSync(resolve(out, 'report.json'), JSON.stringify({ library: version, report, logs: logs.slice(0, 50) }, null, 1));
  for (const r of report) console.log(`${r.file}: ${r.error ? 'ERROR ' + r.error.split('\n')[0] : `${r.itemsWithGeometry ?? '-'} items, ${r.triangles} triangles, ${r.materials} materials, box ${JSON.stringify(r.box.map((p) => p.map((x) => +x.toFixed(2))))}`}`);
  if (logs.some((l) => l.startsWith('pageerror') || l.startsWith('error'))) console.log(logs.filter((l) => l.startsWith('pageerror') || l.startsWith('error')).slice(0, 10).join('\n'));
  console.log(`pictures in ${out}: ${views.map((v) => v + '.png').join(', ')}`);
} finally {
  await browser.close();
  server.close();
}
