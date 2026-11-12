#!/usr/bin/env node
/**
 * The Python package's tests (python/tests, stdlib unittest; docs/python.md): on the oldest Python it supports (3.11) and
 * the newest found, each against an engine of its own (python/tests/engine.py: the built Debug host on a free port).
 * No Python 3.11 or newer here: the tests are skipped, saying so. Needs the engine built (pnpm build:engine).
 *
 *   node scripts/test-python.mjs [--all]       (--all: every Python found, not only the oldest and newest)
 */

import { spawnSync } from 'node:child_process';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const MIN = [3, 11];

/** Each Python here (by its version, the first found of each), oldest first. */
function pythons() {
  const names = ['python3.11', 'python3.12', 'python3.13', 'python3.14', 'python3.15', 'python3', 'python'];
  const found = new Map();
  const tryOne = (cmd, args = []) => {
    const r = spawnSync(cmd, [...args, '-c', 'import sys; print("%d.%d" % sys.version_info[:2])'], { encoding: 'utf8' });
    if (r.status !== 0) return;
    const v = r.stdout.trim();
    const [a, b] = v.split('.').map(Number);
    if (a > MIN[0] || (a === MIN[0] && b >= MIN[1])) if (!found.has(v)) found.set(v, { cmd, args, version: v });
  };
  for (const n of names) tryOne(n);
  if (process.platform === 'win32') for (const v of ['3.11', '3.12', '3.13', '3.14']) tryOne('py', [`-${v}`]);
  return [...found.values()].sort((x, y) => x.version.localeCompare(y.version, undefined, { numeric: true }));
}

const all = pythons();
if (!all.length) {
  console.log('Python 3.11 or newer was not found: the Python tests (python/tests) are skipped.');
  process.exit(0);
}
const chosen = process.argv.includes('--all') || all.length <= 2 ? all : [all[0], all.at(-1)];
let failed = 0;
for (const py of chosen) {
  console.log(`\n— Python ${py.version} (${[py.cmd, ...py.args].join(' ')})`);
  const r = spawnSync(py.cmd, [...py.args, '-m', 'unittest', 'discover', '-s', 'python/tests', '-t', 'python'], {
    cwd: root, stdio: 'inherit', env: { ...process.env, PYTHONDONTWRITEBYTECODE: '1' },
  });
  if (r.status !== 0) failed++;
}
process.exit(failed ? 1 : 0);
