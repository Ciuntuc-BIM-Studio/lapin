#!/usr/bin/env node
// Development: build the BubbleBIM Pro engine (vendor/bubblebim-pro), start it
// on Lapin's own port with Lapin's product (products/lapin.json), start the UI.
//   pnpm dev           → engine on ws://127.0.0.1:47810/ws, UI on http://localhost:5180
//   pnpm dev:desktop   → the same inside the Lapin window (Tauri)
import { spawn, spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const engine = join(root, 'vendor/bubblebim-pro/engine');
const PORT = Number(process.env.LAPIN_ENGINE_PORT ?? 47810);
const desktop = process.argv.includes('--desktop');

console.log('▸ building the engine…');
const build = spawnSync('dotnet', ['build', join(engine, 'BubbleBim.slnx'), '-v', 'q', '-nologo'], { cwd: root, stdio: 'inherit' });
if (build.status !== 0) process.exit(build.status ?? 1);

const product = JSON.parse(readFileSync(join(root, 'products/lapin.json'), 'utf8'));
mkdirSync(join(root, '.artifacts'), { recursive: true });
const manifest = join(root, '.artifacts/product.json');
writeFileSync(manifest, JSON.stringify({ ...product, extensions: [] }, null, 2));

const children = [];
let down = false;
const shutdown = () => {
  if (down) return;
  down = true;
  for (const c of children) c.kill('SIGTERM');
  setTimeout(() => process.exit(0), 300);
};
const run = (name, cmd, args, opts = {}) => {
  const c = spawn(cmd, args, { cwd: root, stdio: 'inherit', ...opts, env: { ...process.env, ...opts.env } });
  c.on('exit', (code) => { console.log(`▸ ${name} stopped (${code})`); shutdown(); });
  children.push(c);
};
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);

const hostDll = join(engine, 'src/Host/BubbleBim.Host/bin/Debug/net10.0/BubbleBim.Host.dll');
console.log(`▸ engine: ${product.modules.join(' + ')} on ws://127.0.0.1:${PORT}/ws`);
run('engine', 'dotnet', [hostDll, '--product', manifest, '--modules-dir', join(engine, 'artifacts/modules'), '--port', String(PORT), '--parent-pid', String(process.pid)]);
run('ui', 'pnpm', ['--filter', 'ui', 'dev'], { env: { VITE_ENGINE_URL: `ws://127.0.0.1:${PORT}/ws` } });
if (desktop) run('desktop', 'pnpm', ['tauri', 'dev'], { cwd: join(root, 'desktop'), env: { BUBBLEBIM_ENGINE_URL: `ws://127.0.0.1:${PORT}/ws` } });
