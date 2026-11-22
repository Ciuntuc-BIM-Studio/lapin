#!/usr/bin/env node
// Intel Open Image Denoise (github.com/RenderKit/oidn, Apache-2.0) for the path tracer's still images on the desktop
// (docs/plan-render.md, S4; answer Î4: a separate program beside the engine). Fetches OIDN's official release for a
// platform, checks its SHA-256 against engine/native/oidn.json, and keeps only what the denoiser needs in
// engine/native/<rid>/oidn/: bin/oidnDenoise, the core library (its trained weights inside), the CPU device (and Metal's
// on macOS), oneTBB (Apache-2.0), and the licence files. The GPU devices of other vendors (SYCL, CUDA, HIP) and their
// runtimes are left out: the CPU device needs nothing else. The folder is not committed (≈ 55 MB); build-product fetches
// it and ships it as resources/oidn; the engine finds it there (Lapin.Host/Denoiser.cs) or here in a checkout.
//
//   node scripts/fetch-oidn.mjs                  (this machine's platform)
//   node scripts/fetch-oidn.mjs --rid win-x64    (osx-arm64, osx-x64, linux-x64, win-x64)
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { arch, platform, tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const register = JSON.parse(readFileSync(join(root, 'engine/native/oidn.json'), 'utf8'));
const arg = (name) => { const i = process.argv.indexOf(name); return i >= 0 ? process.argv[i + 1] : undefined; };
const rid = arg('--rid') ?? `${platform() === 'darwin' ? 'osx' : platform() === 'win32' ? 'win' : 'linux'}-${arch() === 'arm64' ? 'arm64' : 'x64'}`;
const asset = register.assets[rid];
if (!asset) { console.error(`fetch-oidn: no OIDN release for ${rid} (${Object.keys(register.assets).join(', ')}).`); process.exit(1); }

const out = join(root, 'engine/native', rid, 'oidn');
const stamp = join(out, 'VERSION');
if (existsSync(stamp) && readFileSync(stamp, 'utf8').trim() === `${register.version} ${asset.sha256}`) {
  console.log(`▸ OIDN ${register.version} for ${rid} is already in ${out.slice(root.length + 1)}`);
  process.exit(0);
}

const work = join(tmpdir(), `lapin-oidn-${register.version}-${rid}`);
mkdirSync(work, { recursive: true });
const archive = join(work, asset.file);
const sha = (f) => createHash('sha256').update(readFileSync(f)).digest('hex');
if (!existsSync(archive) || sha(archive) !== asset.sha256) {
  const url = `https://github.com/RenderKit/oidn/releases/download/v${register.version}/${asset.file}`;
  console.log(`▸ fetching ${url}`);
  const res = await fetch(url);
  if (!res.ok) { console.error(`fetch-oidn: ${url}: ${res.status}`); process.exit(1); }
  writeFileSync(archive, Buffer.from(await res.arrayBuffer()));
}
const got = sha(archive);
if (got !== asset.sha256) { console.error(`fetch-oidn: ${asset.file} is not the release registered (SHA-256 ${got}, expected ${asset.sha256}).`); process.exit(1); }

const unpacked = join(work, 'x');
rmSync(unpacked, { recursive: true, force: true });
mkdirSync(unpacked, { recursive: true });
// tar unpacks .tar.gz everywhere, and .zip too where it is bsdtar (macOS, Windows 10+).
execFileSync('tar', ['-xf', archive, '-C', unpacked], { stdio: 'inherit' });
const top = join(unpacked, readdirSync(unpacked).find((d) => d.startsWith('oidn-')));

rmSync(out, { recursive: true, force: true });
for (const d of ['bin', 'lib', 'doc']) mkdirSync(join(out, d), { recursive: true });
// What the CPU (and Metal) device needs; nothing of the other vendors' GPU runtimes.
const keep = (name) => register.keep.some((p) => new RegExp(p).test(name)) && !register.drop.some((p) => new RegExp(p).test(name));
let kept = 0;
for (const d of ['bin', 'lib']) {
  const dir = join(top, d);
  if (!existsSync(dir)) continue;
  for (const f of readdirSync(dir)) {
    if (!keep(f)) continue;
    cpSync(join(dir, f), join(out, d, f), { verbatimSymlinks: true });
    kept++;
  }
}
for (const f of ['LICENSE.txt', 'third-party-programs.txt', 'third-party-programs-oneTBB.txt']) if (existsSync(join(top, 'doc', f))) cpSync(join(top, 'doc', f), join(out, 'doc', f));
writeFileSync(stamp, `${register.version} ${asset.sha256}\n`);
console.log(`▸ OIDN ${register.version} for ${rid}: ${kept} files in ${out.slice(root.length + 1)}`);
