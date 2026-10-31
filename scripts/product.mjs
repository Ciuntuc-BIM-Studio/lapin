// What a product is made of. A product (products/<id>.json) names the modules
// a client gets; each module may need others (`requires` in its module.json),
// and those come with it, so a product lists only what it is for: "civil"
// brings what civil needs, nothing more. The result is the manifest the
// engine loads and the UI build bundles.
//
// A module has an engine half (engine/src/Modules/*/*/module.json), a UI half
// (ui/src/modules/<id>/index.tsx, with an optional module.json beside it), or
// both. "core" is the platform's and comes with every product.
import { existsSync, readFileSync, readdirSync, realpathSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const readJson = (f) => JSON.parse(readFileSync(f, 'utf8'));

/**
 * Every module the repository can build: id → { id, name, requires, engine, ui,
 * engineDir, uiDir, external }. A product may name modules that live in a
 * sibling repository ("external": { id: "../lapin-mep" }, a private module):
 * those roots are scanned the same way (the same engine/src/Modules and
 * ui/src/modules layout), and the paths say where each half is.
 */
export function knownModules(root, externals = {}) {
  const o = { core: { id: 'core', name: 'Core', requires: [], engine: true, ui: existsSync(join(root, 'ui/src/modules/core/index.tsx')), external: false } };
  const scan = (base, external) => {
    const engineDir = join(base, 'engine/src/Modules');
    if (existsSync(engineDir))
      for (const group of readdirSync(engineDir))
        for (const project of readdirSync(join(engineDir, group))) {
          const f = join(engineDir, group, project, 'module.json');
          if (!existsSync(f)) continue;
          const m = readJson(f);
          o[m.id] = { id: m.id, name: m.name, requires: m.requires ?? [], engine: true, ui: false, engineDir: join(engineDir, group, project), external };
        }
    const uiDir = join(base, 'ui/src/modules');
    if (existsSync(uiDir))
      for (const id of readdirSync(uiDir)) {
        if (!existsSync(join(uiDir, id, 'index.tsx'))) continue;
        const meta = existsSync(join(uiDir, id, 'module.json')) ? readJson(join(uiDir, id, 'module.json')) : {};
        const m = (o[id] ??= { id, name: meta.name ?? id, requires: [], engine: false, ui: false, external });
        m.ui = true;
        m.uiDir = join(uiDir, id);
        m.requires = [...new Set([...m.requires, ...(meta.requires ?? [])])];
      }
  };
  scan(root, false);
  for (const [id, rel] of Object.entries(externals)) {
    const base = join(root, rel);
    if (!existsSync(base)) throw new Error(`Module "${id}" is external, in ${rel}, but that folder is not there (beside this repository?).`);
    scan(base, true);
    if (!o[id]) throw new Error(`Module "${id}" is not in ${rel} (no engine/src/Modules/*/*/module.json with that id, no ui/src/modules/${id}/index.tsx).`);
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
  const known = knownModules(root, product.external ?? {});
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
    /** How the product presents itself: its name, the short name that stands for "Lapin" in the interface, its theme, its app identifier. */
    brand: {
      name: product.brand?.name ?? product.name,
      short: product.brand?.short ?? 'Lapin',
      identifier: product.brand?.identifier ?? `ro.ciuntucbimstudio.${product.id}`,
      // What an edition under another name says of itself (none for Lapin): its mark's letter, its accent colour
      // (light and dark), the line under its name, the start screen's first hint, its app icons (a folder, from this repo).
      ...(product.brand?.mark ? { mark: product.brand.mark } : {}),
      ...(product.brand?.accent ? { accent: product.brand.accent } : {}),
      ...(product.brand?.tagline ? { tagline: product.brand.tagline } : {}),
      ...(product.brand?.hint ? { hint: product.brand.hint } : {}),
      ...(product.brand?.icons ? { icons: product.brand.icons } : {}),
    },
    /** The ribbon's tabs placed otherwise (tab id → order, lower first): the product's own work first. */
    ...(product.tabs ? { tabs: product.tabs } : {}),
    modules: order,
    /** What a new project starts with and the disciplines the browser groups by, when the product says (else the platform's). */
    ...(product.storeys ? { storeys: product.storeys } : {}),
    ...(product.disciplines ? { disciplines: product.disciplines } : {}),
    /** What came only because another module needs it. */
    required: added,
    engine: order.filter((id) => known[id].engine),
    ui: order.filter((id) => known[id].ui),
    /** Where the interface half of each module outside this repository is (id → folder): the build imports those by path. */
    uiPaths: Object.fromEntries(order.filter((id) => known[id].ui && known[id].external).map((id) => [id, known[id].uiDir])),
    /** The engine projects outside this repository, built one by one (they are not in Lapin.slnx). */
    engineProjects: order.filter((id) => known[id].engine && known[id].external).map((id) => {
      const dir = known[id].engineDir;
      return join(dir, readdirSync(dir).find((f) => f.endsWith('.csproj')));
    }),
  };
}

/**
 * The engine modules of a product as a host that builds them in needs them
 * (the browser's, which loads nothing from a folder): each one's project and
 * the class that is its ILapinModule, core left out (it is the platform's).
 */
export function engineModules(root, productId) {
  const p = resolveProduct(root, productId);
  const known = knownModules(root, readJson(join(root, 'products', `${productId}.json`)).external ?? {});
  const o = [];
  for (const m of Object.values(known)) {
      if (!m.engine || !p.engine.includes(m.id) || m.id === 'core') continue;
      const dir = m.engineDir;
      const project = readdirSync(dir).find((f) => f.endsWith('.csproj')).slice(0, -7);
      let cls = null;
      for (const file of readdirSync(dir).filter((x) => x.endsWith('.cs'))) {
        const text = readFileSync(join(dir, file), 'utf8');
        const c = /public\s+sealed\s+(?:partial\s+)?class\s+(\w+)\s*:\s*ILapinModule\b/.exec(text);
        const ns = /^namespace\s+([\w.]+)\s*;/m.exec(text);
        if (c && ns) { cls = `${ns[1]}.${c[1]}`; break; }
      }
      if (!cls) throw new Error(`Module "${m.id}": no class implementing ILapinModule in ${dir}.`);
      o.push({ id: m.id, project: join(dir, `${project}.csproj`), cls, external: m.external });
  }
  // In the product's order (a module after those it requires).
  return o.sort((a, b) => p.engine.indexOf(a.id) - p.engine.indexOf(b.id));
}

/** Every product of the repository. */
export function productIds(root) {
  return readdirSync(join(root, 'products')).filter((f) => f.endsWith('.json')).map((f) => f.slice(0, -5)).sort();
}

// `node scripts/product.mjs [id]`: what a product resolves to (all of them without an id).
// Compared as file paths: the URL percent-encodes a space, and the module's is the real path.
if (process.argv[1] && existsSync(process.argv[1]) && fileURLToPath(import.meta.url) === realpathSync(process.argv[1])) {
  const root = join(dirname(fileURLToPath(import.meta.url)), '..');
  for (const id of process.argv[2] ? [process.argv[2]] : productIds(root)) {
    const p = resolveProduct(root, id);
    console.log(`${p.id} (${p.name}): ${p.modules.join(' + ')}${p.required.length ? `   [required: ${p.required.join(', ')}]` : ''}`);
  }
}
