#!/usr/bin/env node
// Lapin stays MIT: every production dependency, of the UI (npm) and of the
// engine (NuGet, everything the host ships with), must carry a permissive
// licence. Fails (exit 1) on anything else — copyleft (GPL, LGPL, AGPL, MPL,
// EPL, CC-BY-SA…) or unknown — so it is caught before it ships. Dev-only
// tools (TypeScript, Vite, test runners, xUnit) are not shipped and not checked.
import { execFileSync } from 'node:child_process';
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

if (bad.length) {
  console.error(`✗ ${bad.length} production dependencies are not under a permissive licence:\n  ${bad.join('\n  ')}`);
  process.exit(1);
}
console.log(`✓ ${count} production dependencies, all permissive (${[...licences].join(', ')}).`);
