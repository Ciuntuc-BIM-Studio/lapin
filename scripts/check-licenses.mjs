#!/usr/bin/env node
// Lapin stays MIT: every production dependency, of the UI (npm) and of the
// engine (NuGet, everything the host ships with), must carry a permissive
// licence. Fails (exit 1) on anything else — copyleft (GPL, LGPL, AGPL, MPL,
// EPL, CC-BY-SA…) or unknown — so it is caught before it ships. Dev-only
// tools (TypeScript, Vite, test runners, xUnit) are not shipped and not checked.
// The furniture models shipped (CC0 meshes and their previews) are checked against their register.
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ALLOWED = new Set(['MIT', 'MIT-0', 'ISC', 'BSD-2-Clause', 'BSD-3-Clause', '0BSD', 'Apache-2.0', 'Zlib', 'CC0-1.0', 'Unlicense', 'BlueOak-1.0.0', 'Python-2.0']);
// An SPDX expression passes when one side of an OR is allowed and every side of an AND is.
const ok = (expr) => expr.split(/\s+OR\s+/i).some((alt) => alt.replace(/[()]/g, '').split(/\s+AND\s+/i).every((p) => ALLOWED.has(p.trim())));

// A file path, not a URL's pathname: that one is percent-encoded (a space is %20).
const root = join(dirname(fileURLToPath(import.meta.url)), '..');
// pnpm is pnpm.cmd on Windows, which runs only through the shell.
const out = execFileSync('pnpm', ['licenses', 'list', '--prod', '--json'], { encoding: 'utf8', cwd: root, shell: process.platform === 'win32' });
const byLicence = JSON.parse(out);
const bad = [];
let count = 0;
for (const [licence, pkgs] of Object.entries(byLicence)) {
  for (const p of pkgs) {
    count++;
    if (!ok(licence)) bad.push(`${p.name}@${(p.versions ?? [p.version]).join(',')}: ${licence}`);
  }
}
const licences = new Set(Object.keys(byLicence));

// The engine: what the host and the modules reference, transitively (the
// modules ride on the host's dependencies or bring their own).
// Found in Node rather than with `find`, which Windows does not have.
const modulesDir = join(root, 'engine/src/Modules');
const moduleProjects = existsSync(modulesDir)
  ? readdirSync(modulesDir, { recursive: true }).map(String).filter((f) => f.endsWith('.csproj')).sort().map((f) => join('engine/src/Modules', f))
  : [];
const projects = ['engine/src/Host/Lapin.Host', ...moduleProjects];
const nuget = process.env.NUGET_PACKAGES ?? join(homedir(), '.nuget/packages');
// A licence shipped as a file: read its title.
const FILE_LICENCES = [[/Apache License\s+Version 2\.0/i, 'Apache-2.0'], [/^\s*MIT License/im, 'MIT'], [/Permission is hereby granted, free of charge/i, 'MIT']];
const seen = new Set();
for (const project of projects) {
  let listed;
  try {
    listed = JSON.parse(execFileSync('dotnet', ['list', project, 'package', '--include-transitive', '--format', 'json'], { encoding: 'utf8', cwd: root }));
  } catch {
    bad.push(`${project}: cannot list its packages (run pnpm build:engine first)`);
    continue;
  }
  for (const fw of listed.projects?.flatMap((p) => p.frameworks ?? []) ?? [])
    for (const p of [...(fw.topLevelPackages ?? []), ...(fw.transitivePackages ?? [])]) {
      const key = `${p.id}@${p.resolvedVersion}`;
      if (seen.has(key)) continue;
      seen.add(key);
      count++;
      const dir = join(nuget, p.id.toLowerCase(), p.resolvedVersion);
      const nuspec = join(dir, `${p.id.toLowerCase()}.nuspec`);
      const text = existsSync(nuspec) ? readFileSync(nuspec, 'utf8') : '';
      const expr = text.match(/<license type="expression">([^<]+)<\/license>/)?.[1];
      const file = text.match(/<license type="file">([^<]+)<\/license>/)?.[1];
      const licence = expr ?? (file && existsSync(join(dir, file)) ? FILE_LICENCES.find(([re]) => re.test(readFileSync(join(dir, file), 'utf8')))?.[1] : undefined);
      if (!licence || !ok(licence)) bad.push(`${key} (NuGet): ${licence ?? 'unknown licence'}`);
      else licences.add(licence);
    }
}

// Shipped assets (decision 477): the furniture models and their previews, each in the register with its source, a
// licence of ASSET_ALLOWED (no attribution, no share-alike) and its hash as shipped; no file shipped that it does not list.
const ASSET_ALLOWED = new Set(['CC0-1.0', 'MIT-0']);
let assets = 0;
const registerDir = join(root, 'engine/src/Modules/Interior/Lapin.Furniture.Models');
const registerPath = join(registerDir, 'furniture-assets.json');
if (existsSync(registerPath)) {
  const reg = JSON.parse(readFileSync(registerPath, 'utf8'));
  const sha = (p) => createHash('sha256').update(readFileSync(p)).digest('hex');
  const listed = new Set();
  const checkFile = (where, rel, hash, what) => {
    const p = join(where === 'engine' ? registerDir : root, rel);
    listed.add(p);
    if (!existsSync(p)) bad.push(`${what}: ${rel} is listed but missing`);
    else if (sha(p) !== hash) bad.push(`${what}: ${rel} is not the file registered (its hash differs)`);
  };
  for (const [id, src] of Object.entries(reg.sources ?? {}))
    if (!ASSET_ALLOWED.has(src.licence) || !src.verified) bad.push(`asset source ${id}: ${src.licence ?? 'no licence'}${src.verified ? '' : ', not verified'}`);
  for (const m of reg.models ?? []) {
    assets++;
    const src = reg.sources?.[m.source];
    if (!src) bad.push(`model ${m.id}: its source ${m.source} is not in the register`);
    if (!ASSET_ALLOWED.has(m.licence) || (src && src.licence !== m.licence)) bad.push(`model ${m.id}: ${m.licence ?? 'no licence'}`);
    checkFile('engine', m.file, m.sha256, `model ${m.id}`);
    if (m.thumbnail) checkFile('root', m.thumbnail.file, m.thumbnail.sha256, `model ${m.id}'s preview`);
  }
  for (const [dir, base] of [[join(registerDir, 'models'), registerDir], [join(root, 'ui/public/furniture-models'), root]])
    if (existsSync(dir)) for (const f of readdirSync(dir)) if (!listed.has(join(dir, f))) bad.push(`${join(dir, f).slice(base.length + 1)}: shipped but not in the furniture models' register`);
  licences.add('CC0-1.0 (assets)');
}

if (bad.length) {
  console.error(`✗ ${bad.length} production dependencies are not under a permissive licence:\n  ${bad.join('\n  ')}`);
  process.exit(1);
}
console.log(`✓ ${count} production dependencies and ${assets} assets, all permissive (${[...licences].join(', ')}).`);
