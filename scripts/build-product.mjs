#!/usr/bin/env node
// Build one product as a desktop app, with only its modules, engine and UI
// alike: a module the product does not have is not in the package at all.
//   pnpm build:product <product>      (lapin | lapin-architecture | lapin-architecture-structure | …)
// Output: build/<product>/bundle. Each product is its own app (name and identifier).
import { spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { resolveProduct } from './product.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const productId = process.argv[2];
if (!productId) { console.error('Usage: pnpm build:product <product>'); process.exit(1); }
let product;
try { product = resolveProduct(root, productId); } catch (e) { console.error(e.message); process.exit(1); }
const out = join(root, 'build', productId);
const tauriDir = join(root, 'desktop/src-tauri');

// pnpm is pnpm.cmd on Windows, which Node starts only through the shell; there
// an argument with a space (a path) is quoted, the shell would split it otherwise.
const win = process.platform === 'win32';
const shellArgs = (cmd, args) => (win && cmd === 'pnpm' ? args.map((a) => (/\s/.test(a) ? `"${a}"` : a)) : args);
const sh = (cmd, args, opts = {}) => {
  console.log(`▸ ${cmd} ${args.join(' ')}`);
  const r = spawnSync(cmd, shellArgs(cmd, args), { cwd: root, stdio: 'inherit', shell: win && cmd === 'pnpm', ...opts, env: { ...process.env, ...opts.env } });
  if (r.error) { console.error(`Cannot run ${cmd}: ${r.error.message}`); process.exit(1); }
  if (r.status !== 0) process.exit(r.status ?? 1);
};

// The target triple of this machine, from the Rust toolchain the desktop shell is built with.
const rustc = spawnSync('rustc', ['-vV'], { encoding: 'utf8' });
const triple = rustc.stdout?.match(/^host: (.+)$/m)?.[1].trim();
if (!triple) {
  console.error(`Cannot read the target from rustc${rustc.error ? ` (${rustc.error.message})` : ''}: install Rust (https://rustup.rs) and make sure rustc is on the PATH.`);
  process.exit(1);
}
const rid = { 'aarch64-apple-darwin': 'osx-arm64', 'x86_64-apple-darwin': 'osx-x64', 'x86_64-pc-windows-msvc': 'win-x64', 'x86_64-unknown-linux-gnu': 'linux-x64' }[triple];
if (!rid) { console.error(`No .NET runtime id for ${triple}`); process.exit(1); }
const exe = triple.includes('windows') ? '.exe' : '';

rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });

// 1. Engine: the modules (Release), then the host as one self-contained file.
sh('dotnet', ['build', 'engine/Lapin.slnx', '-c', 'Release', '-v', 'q', '-nologo']);
sh('dotnet', ['publish', 'engine/src/Host/Lapin.Host', '-c', 'Release', '-r', rid, '--self-contained', 'true',
  '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=none', '-o', join(out, 'host'), '-v', 'q', '-nologo']);
mkdirSync(join(tauriDir, 'binaries'), { recursive: true });
cpSync(join(out, 'host', `Lapin.Host${exe}`), join(tauriDir, 'binaries', `lapin-host-${triple}${exe}`));

// 2. Resources: the manifest and ONLY this product's engine modules.
const res = join(tauriDir, 'resources');
rmSync(res, { recursive: true, force: true });
mkdirSync(join(res, 'modules'), { recursive: true });
const manifest = join(res, 'product.json');
writeFileSync(manifest, JSON.stringify(product, null, 2));
for (const m of product.engine.filter((id) => id !== 'core')) {
  const src = join(root, 'engine/artifacts/modules', m);
  if (!existsSync(src)) { console.error(`Module ${m} was not built (${src}).`); process.exit(1); }
  mkdirSync(join(res, 'modules', m), { recursive: true });
  for (const f of readdirSync(src).filter((f) => !f.endsWith('.pdb'))) cpSync(join(src, f), join(res, 'modules', m, f));
}

// 3. UI: only this product's UI modules are bundled.
sh('pnpm', ['--filter', 'ui', 'build'], { env: { LAPIN_PRODUCT_FILE: manifest, LAPIN_UI_OUT: join(out, 'ui') } });

// 4. The desktop bundle, named and identified per product.
writeFileSync(join(tauriDir, 'tauri.product.json'), JSON.stringify({
  productName: product.name,
  identifier: `ro.ciuntucbimstudio.${productId}`,
  build: { frontendDist: join(out, 'ui') },
  app: { windows: [{ label: 'main', title: product.name, width: 1440, height: 900, minWidth: 1000, minHeight: 640, center: true }] },
  bundle: { externalBin: ['binaries/lapin-host'], resources: ['resources/**/*'] },
}, null, 2));
const bundleDir = join(tauriDir, 'target/release/bundle');
rmSync(bundleDir, { recursive: true, force: true }); // never ship another product's leftovers
sh('pnpm', ['tauri', 'build', '--config', join(tauriDir, 'tauri.product.json')], { cwd: join(root, 'desktop') });

if (existsSync(bundleDir)) cpSync(bundleDir, join(out, 'bundle'), { recursive: true });
console.log(`\n✓ ${product.name} (${product.modules.join(' + ')}): ${join(out, 'bundle')}`);
