#!/usr/bin/env node
// The texture sets Lapin ships (docs/plan-render.md, S1): writes each set's files (the maps found under
// ui/public/textures/<source>/<id>/) with their sizes and SHA-256 into ui/src/viewport/texture-library.json, which
// pnpm check:licenses checks. --check: only says whether the register matches the files (exit 1 if not).
//
// A new set: download its 1K-JPG zip from its source, bring each map down to 512 px on its longer side
// (sips -s format jpeg -s formatOptions 78 -Z 512 in.jpg --out Color.jpg; NormalGL → Normal, AmbientOcclusion → AO,
// Displacement → Height), add the set to the register (id, source, name, kind, size in metres), then run this.
import { createHash } from 'node:crypto';
import { existsSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const registerPath = join(root, 'ui/src/viewport/texture-library.json');
const MAPS = ['Color', 'Normal', 'Roughness', 'AO', 'Height'];
const check = process.argv.includes('--check');

const reg = JSON.parse(readFileSync(registerPath, 'utf8'));
let differs = false;
for (const set of reg.sets) {
  const files = {};
  for (const map of MAPS) {
    const rel = `ui/public/textures/${set.source}/${set.id}/${map}.jpg`;
    const p = join(root, rel);
    if (!existsSync(p)) continue;
    files[map.toLowerCase()] = { file: rel, bytes: statSync(p).size, sha256: createHash('sha256').update(readFileSync(p)).digest('hex') };
  }
  if (!files.color) { console.error(`✗ ${set.id}: no Color.jpg`); differs = true; }
  if (JSON.stringify(files) !== JSON.stringify(set.files)) differs = true;
  set.files = files;
}
const dir = join(root, 'ui/public/textures');
const total = existsSync(dir) ? readdirSync(dir, { recursive: true }).map(String).filter((f) => f.endsWith('.jpg')).reduce((s, f) => s + statSync(join(dir, f)).size, 0) : 0;
if (check) {
  if (differs) { console.error('✗ ui/src/viewport/texture-library.json does not match the files: run node scripts/texture-assets.mjs'); process.exit(1); }
  console.log(`✓ ${reg.sets.length} texture sets, ${(total / 1024 / 1024).toFixed(2)} MB`);
} else {
  writeFileSync(registerPath, JSON.stringify(reg, null, 2) + '\n');
  console.log(`✓ ${reg.sets.length} texture sets written, ${(total / 1024 / 1024).toFixed(2)} MB`);
}
