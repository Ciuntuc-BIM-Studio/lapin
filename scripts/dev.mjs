#!/usr/bin/env node
// Development: build the Lapin engine (engine/Lapin.slnx), start it with a
// product (products/<id>.json, LAPIN_PRODUCT, default lapin) on Lapin's own
// port, start the UI with the same product's modules.
//   pnpm dev           → engine on ws://127.0.0.1:47810/ws, UI on http://localhost:5180
//   pnpm dev:desktop   → the same inside the Lapin window (Tauri)
import { spawn, spawnSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { resolveProduct } from './product.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const engine = join(root, 'engine');
const PORT = Number(process.env.LAPIN_ENGINE_PORT ?? 47810);
const desktop = process.argv.includes('--desktop');

const productId = process.env.LAPIN_PRODUCT ?? 'lapin';
let product;
try { product = resolveProduct(root, productId); } catch (e) { console.error(e.message); process.exit(1); }

console.log('▸ building the engine…');
const build = spawnSync('dotnet', ['build', join(engine, 'Lapin.slnx'), '-v', 'q', '-nologo'], { cwd: root, stdio: 'inherit' });
if (build.status !== 0) process.exit(build.status ?? 1);
// A module from a sibling repository (a private one) is not in the solution: built on its own, it lands in artifacts/modules like the others.
for (const csproj of product.engineProjects ?? []) {
  const b = spawnSync('dotnet', ['build', csproj, '-v', 'q', '-nologo'], { cwd: root, stdio: 'inherit' });
  if (b.status !== 0) process.exit(b.status ?? 1);
}

mkdirSync(join(root, '.artifacts'), { recursive: true });
const manifest = join(root, '.artifacts/product.json');
writeFileSync(manifest, JSON.stringify(product, null, 2));

const children = [];
let down = false;
const shutdown = () => {
  if (down) return;
  down = true;
  for (const c of children) c.kill('SIGTERM');
  setTimeout(() => process.exit(0), 300);
};
// pnpm is pnpm.cmd on Windows, which Node starts only through the shell; there
// an argument with a space (a path) is quoted, the shell would split it otherwise.
const win = process.platform === 'win32';
const shellArgs = (cmd, args) => (win && cmd === 'pnpm' ? args.map((a) => (/\s/.test(a) ? `"${a}"` : a)) : args);
const run = (name, cmd, args, opts = {}) => {
  const c = spawn(cmd, shellArgs(cmd, args), { cwd: root, stdio: 'inherit', shell: win && cmd === 'pnpm', ...opts, env: { ...process.env, ...opts.env } });
  c.on('error', (e) => { console.error(`▸ ${name}: cannot run ${cmd}: ${e.message}`); shutdown(); });
  c.on('exit', (code) => { console.log(`▸ ${name} stopped (${code})`); shutdown(); });
  children.push(c);
};
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);

const hostDll = join(engine, 'src/Host/Lapin.Host/bin/Debug/net10.0/Lapin.Host.dll');
console.log(`▸ ${product.name}: ${product.modules.join(' + ')} on ws://127.0.0.1:${PORT}/ws`);
// The dev engine keeps its user data (recovery journals) in the repository, apart from an installed Lapin's.
run('engine', 'dotnet', [hostDll, '--product', manifest, '--modules-dir', join(engine, 'artifacts/modules'), '--user-dir', join(root, '.artifacts/user'), '--port', String(PORT), '--parent-pid', String(process.pid)]);
run('ui', 'pnpm', ['--filter', 'ui', 'dev'], { env: { VITE_ENGINE_URL: `ws://127.0.0.1:${PORT}/ws`, LAPIN_PRODUCT_FILE: manifest } });
if (desktop) run('desktop', 'pnpm', ['tauri', 'dev'], { cwd: join(root, 'desktop'), env: { LAPIN_ENGINE_URL: `ws://127.0.0.1:${PORT}/ws` } });
