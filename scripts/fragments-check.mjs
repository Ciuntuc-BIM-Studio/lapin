#!/usr/bin/env node
/**
 * The decisive check of a .frag Lapin wrote: the official library reads it.
 *
 *   node scripts/fragments-check.mjs <file.frag> [<path to a node_modules holding @thatopen/fragments>]
 *
 * The library is not a dependency of Lapin (it brings three.js and web-ifc);
 * it is taken from another checkout's node_modules (BubbleBIM Standalone's,
 * ../BubbleBIM-Standalone/node_modules, by default). Prints what the library
 * sees: the items, their categories, the geometry, the spatial tree, the
 * metadata; exits 1 when it cannot read the file.
 */

import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const [, , file, modules = resolve(process.cwd(), '../BubbleBIM-Standalone/node_modules')] = process.argv;
if (!file) { console.error('usage: fragments-check.mjs <file.frag> [node_modules]'); process.exit(2); }
// The library keeps handles open (its workers, timers): the process ends by itself, and never hangs on a call that waits for a worker.
const watchdog = setTimeout(() => { console.error('gave up after 60 s'); process.exit(3); }, 60_000);
const settle = (p) => Promise.race([p, new Promise((_, reject) => setTimeout(() => reject(new Error('no answer in 10 s')), 10_000))]);
const lib = await import(pathToFileURL(resolve(modules, '@thatopen/fragments/dist/index.mjs')).href);
const bytes = new Uint8Array(readFileSync(file));
const raw = bytes[0] !== 0x78;
const model = new lib.SingleThreadedFragmentsModel('check', bytes, raw);
const categories = model.getCategories();
const withGeometry = model.getItemsWithGeometry();
const metadata = model.getMetadata();
const tree = model.getSpatialStructure();
const guids = model.getGuidsByLocalIds(withGeometry);
const walk = (n, d = 0, out = []) => { if (d > 8) return out; out.push('  '.repeat(d) + `${n.category ?? '·'} #${n.localId ?? '·'}`); for (const c of n.children ?? []) walk(c, d + 1, out); return out; };
console.log(`${file}: ${raw ? 'raw' : 'zlib'}, ${bytes.length} bytes`);
console.log(`categories: ${categories.join(', ')}`);
console.log(`items with geometry: ${withGeometry.length} → local ids ${JSON.stringify(withGeometry.slice(0, 8))}${withGeometry.length > 8 ? '…' : ''}`);
console.log(`their guids: ${JSON.stringify(guids.slice(0, 4))}`);
console.log(`max local id: ${model.getMaxLocalId()}; metadata: ${JSON.stringify(metadata)}`);
console.log('spatial structure:\n' + walk(tree).slice(0, 24).join('\n'));
// The geometry, as the library gives it to a renderer: a box and positions per item.
for (const name of ['getItemsData', 'getBoxes', 'getPositions', 'getItemsGeometry', 'getItemsAttributes']) {
  if (typeof model[name] !== 'function') continue;
  try {
    const r = await settle(model[name](withGeometry.slice(0, 3)));
    const s = JSON.stringify(r, (k, v) => (ArrayBuffer.isView(v) ? `[${v.constructor.name} ×${v.length}]` : v));
    console.log(`${name}: ${s.slice(0, 400)}${s.length > 400 ? '…' : ''}`);
  } catch (e) { console.log(`${name}: ${e.message}`); }
}
const again = new Uint8Array(model.getBuffer(true));
console.log(`buffer back from the library: ${again.length} bytes`);
model.dispose();
clearTimeout(watchdog);
process.exit(0);
