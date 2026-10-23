// What a product is made of. A product (products/<id>.json) names the modules
// a client gets; each module may need others (`requires` in its module.json),
// and those come with it, so a product lists only what it is for: "civil"
// brings what civil needs, nothing more. The result is the manifest the
// engine loads and the UI build bundles.
//
// A module has an engine half (engine/src/Modules/*/*/module.json), a UI half
// (ui/src/modules/<id>/index.tsx, with an optional module.json beside it), or
// both. "core" is the platform's and comes with every product.
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const readJson = (f) => JSON.parse(readFileSync(f, 'utf8'));

/** Every module the repository can build: id → { id, name, requires, engine, ui }. */
export function knownModules(root) {
  const o = { core: { id: 'core', name: 'Core', requires: [], engine: true, ui: existsSync(join(root, 'ui/src/modules/core/index.tsx')) } };
  const engineDir = join(root, 'engine/src/Modules');
  if (existsSync(engineDir))
    for (const group of readdirSync(engineDir))
      for (const project of readdirSync(join(engineDir, group))) {
        const f = join(engineDir, group, project, 'module.json');
        if (!existsSync(f)) continue;
        const m = readJson(f);
        o[m.id] = { id: m.id, name: m.name, requires: m.requires ?? [], engine: true, ui: false };
      }
  const uiDir = join(root, 'ui/src/modules');
  if (existsSync(uiDir))
    for (const id of readdirSync(uiDir)) {
      if (!existsSync(join(uiDir, id, 'index.tsx'))) continue;
      const meta = existsSync(join(uiDir, id, 'module.json')) ? readJson(join(uiDir, id, 'module.json')) : {};
      const m = (o[id] ??= { id, name: meta.name ?? id, requires: [], engine: false, ui: false });
      m.ui = true;
      m.requires = [...new Set([...m.requires, ...(meta.requires ?? [])])];
    }
  return o;
}

/**
 * The product with its modules completed: every module it names, what they
 * require (transitively), core first, in a stable order. Throws on a module
 * the repository does not have.
 */
export function resolveProduct(root, productId) {
  const file = join(root, 'products', `${productId}.json`);
  if (!existsSync(file)) throw new Error(`No product "${productId}" in products/.`);
  const product = readJson(file);
  const known = knownModules(root);
  const order = [];
  const visit = (id, via) => {
    if (!known[id]) throw new Error(`Product "${productId}": no module "${id}"${via ? ` (required by "${via}")` : ''}.`);
    if (order.includes(id)) return;
    for (const r of known[id].requires) visit(r, id);
    order.push(id);
  };
  visit('core');
  for (const id of product.modules) visit(id);
  const added = order.filter((id) => !product.modules.includes(id) && id !== 'core');
  return {
    id: product.id,
    name: product.name,
    modules: order,
    /** What came only because another module needs it. */
    required: added,
    engine: order.filter((id) => known[id].engine),
    ui: order.filter((id) => known[id].ui),
  };
}

/** Every product of the repository. */
export function productIds(root) {
  return readdirSync(join(root, 'products')).filter((f) => f.endsWith('.json')).map((f) => f.slice(0, -5)).sort();
}

// `node scripts/product.mjs [id]`: what a product resolves to (all of them without an id).
if (import.meta.url === `file://${process.argv[1]}`) {
  const root = join(new URL('.', import.meta.url).pathname, '..');
  for (const id of process.argv[2] ? [process.argv[2]] : productIds(root)) {
    const p = resolveProduct(root, id);
    console.log(`${p.id} (${p.name}): ${p.modules.join(' + ')}${p.required.length ? `   [required: ${p.required.join(', ')}]` : ''}`);
  }
}
