#!/usr/bin/env node
// Lapin stays MIT: every production dependency of the UI must carry a
// permissive licence. Fails (exit 1) on anything else — copyleft (GPL, LGPL,
// AGPL, MPL, EPL, CC-BY-SA…) or unknown — so it is caught before it ships.
// Dev-only tools (TypeScript, Vite, test runners) are not shipped and not checked.
import { execFileSync } from 'node:child_process';

const ALLOWED = new Set(['MIT', 'MIT-0', 'ISC', 'BSD-2-Clause', 'BSD-3-Clause', '0BSD', 'Apache-2.0', 'Zlib', 'CC0-1.0', 'Unlicense', 'BlueOak-1.0.0', 'Python-2.0']);
// An SPDX expression passes when one side of an OR is allowed and every side of an AND is.
const ok = (expr) => expr.split(/\s+OR\s+/i).some((alt) => alt.replace(/[()]/g, '').split(/\s+AND\s+/i).every((p) => ALLOWED.has(p.trim())));

const out = execFileSync('pnpm', ['licenses', 'list', '--prod', '--json'], { encoding: 'utf8', cwd: new URL('..', import.meta.url) });
const byLicence = JSON.parse(out);
const bad = [];
let count = 0;
for (const [licence, pkgs] of Object.entries(byLicence)) {
  for (const p of pkgs) {
    count++;
    if (!ok(licence)) bad.push(`${p.name}@${(p.versions ?? [p.version]).join(',')}: ${licence}`);
  }
}
if (bad.length) {
  console.error(`✗ ${bad.length} production dependencies are not under a permissive licence:\n  ${bad.join('\n  ')}`);
  process.exit(1);
}
console.log(`✓ ${count} production dependencies, all permissive (${Object.keys(byLicence).join(', ')}).`);
