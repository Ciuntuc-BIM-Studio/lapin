#!/usr/bin/env node
/**
 * The Python package lapinbim (python/, docs/python.md), built for PyPI and, with --upload, uploaded.
 *
 * 1. A staging copy of python/ in build/python/stage, its version Lapin's build version, 0.1.<commit count> (as the
 *    desktop app's): the package and the app it was made with carry the same number.
 * 2. The catalogue of the Lapin it was made with (commands with their arguments, queries, classes with their fields),
 *    read from an engine of its own (the built Debug host, product lapin, on a free port; never `pnpm dev`'s), and the
 *    stubs written from it (lapinbim/catalogue.json, lapinbim/__init__.pyi).
 * 3. The sdist and the wheel, by the package's own backend (python/build_backend, standard library only), into
 *    build/python/dist, each with its SHA-256.
 * 4. --upload: `python3 -m twine upload` of those two files. Twine (a developer tool, not a dependency of anything
 *    shipped) asks for the PyPI token, or reads it from ~/.pypirc or the keyring; it never goes through this script.
 *    --test uploads to TestPyPI instead.
 *
 *   pnpm build:engine && node scripts/publish-python.mjs [--upload [--test]] [--product <id>] [--python <cmd>]
 */

import { spawn, spawnSync } from 'node:child_process';
import { cpSync, createReadStream, existsSync, mkdirSync, mkdtempSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { resolveProduct } from './product.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const arg = (name, fallback) => { const i = process.argv.indexOf(name); return i >= 0 ? process.argv[i + 1] : fallback; };
const python = arg('--python', process.platform === 'win32' ? 'python' : 'python3');
const productId = arg('--product', 'lapin');
const out = join(root, 'build/python');
const stage = join(out, 'stage');
const dist = join(out, 'dist');
const fail = (m) => { console.error(m); process.exit(1); };

const v = spawnSync(python, ['-c', 'import sys; print(sys.version_info >= (3, 11))'], { encoding: 'utf8' });
if (v.stdout?.trim() !== 'True') fail(`${python} is not Python 3.11 or newer: pass --python <cmd>.`);

const commits = spawnSync('git', ['rev-list', '--count', 'HEAD'], { cwd: root, encoding: 'utf8' }).stdout?.trim();
const version = process.env.LAPIN_VERSION ?? (commits ? `0.1.${commits}` : null);
if (!version || !/^\d+\.\d+\.\d+$/.test(version)) fail(`No version: set LAPIN_VERSION (x.y.z), or build from a git checkout (${version ?? 'no commit count'}).`);
console.log(`▸ lapinbim ${version}`);

// 1. The staging copy.
rmSync(out, { recursive: true, force: true });
mkdirSync(stage, { recursive: true });
for (const part of ['pyproject.toml', 'README.md', 'build_backend', 'lapinbim'])
  cpSync(join(root, 'python', part), join(stage, part), { recursive: true, filter: (f) => !f.includes('__pycache__') && !f.endsWith('.pyc') && !f.endsWith('__init__.pyi') && !f.endsWith('catalogue.json') });
cpSync(join(root, 'LICENSE'), join(stage, 'LICENSE'));
writeFileSync(join(stage, 'lapinbim/_version.py'), `# Lapin's build version (0.1.<commit count>), written by scripts/publish-python.mjs.\n__version__ = "${version}"\n`);

// 2. The catalogue and the stubs, from an engine of the staging copy's own.
const host = join(root, 'engine/src/Host/Lapin.Host/bin/Debug/net10.0/Lapin.Host.dll');
if (!existsSync(host)) fail(`The engine is not built (${host}): pnpm build:engine.`);
const temp = mkdtempSync(join(tmpdir(), 'lapinbim-publish-'));
const manifest = join(temp, 'product.json');
writeFileSync(manifest, JSON.stringify(resolveProduct(root, productId), null, 2));
const port = await new Promise((done) => { const s = createServer(); s.listen(0, '127.0.0.1', () => { const p = s.address().port; s.close(() => done(p)); }); });
const engine = spawn('dotnet', [host, '--product', manifest, '--modules-dir', join(root, 'engine/artifacts/modules'), '--user-dir', join(temp, 'user'),
  '--port', String(port), '--parent-pid', String(process.pid)]);
process.on('exit', () => { engine.kill('SIGTERM'); rmSync(temp, { recursive: true, force: true }); });
await new Promise((done, reject) => {
  let seen = '';
  const look = (d) => { seen += d; if (seen.includes('LAPIN_HOST_READY')) done(); };
  engine.stdout.on('data', look); engine.stderr.on('data', look);
  engine.on('exit', (code) => reject(new Error(`the engine stopped (${code}): ${seen.slice(-800)}`)));
  setTimeout(() => reject(new Error(`the engine did not start: ${seen.slice(-800)}`)), 90_000);
});
const py = (args) => {
  const r = spawnSync(python, args, { cwd: stage, stdio: 'inherit', env: { ...process.env, PYTHONPATH: stage, PYTHONDONTWRITEBYTECODE: '1' } });
  if (r.status !== 0) fail(`${python} ${args.join(' ')} failed.`);
};
py(['-m', 'lapinbim', 'catalogue', '--port', String(port), '--out', join(stage, 'lapinbim/catalogue.json')]);
py(['-m', 'lapinbim', 'stubs', '--catalogue', join(stage, 'lapinbim/catalogue.json'), '--out', join(stage, 'lapinbim')]);
engine.kill('SIGTERM');

// 3. The sdist and the wheel, by the package's own backend.
mkdirSync(dist, { recursive: true });
py(['-c', `import sys; sys.path.insert(0, "build_backend"); import lapin_build as b; print(b.build_sdist(${JSON.stringify(dist)})); print(b.build_wheel(${JSON.stringify(dist)}))`]);
// The wheel installs and imports in a clean environment: its own version, its stubs and its catalogue.
py(['-c', [
  'import sys, zipfile, glob',
  `w = glob.glob(${JSON.stringify(join(dist, '*.whl'))})[0]`,
  'names = zipfile.ZipFile(w).namelist()',
  `assert "lapinbim/__init__.pyi" in names and "lapinbim/catalogue.json" in names and "lapinbim/py.typed" in names, names`,
  'sys.path.insert(0, w)',
  'import lapinbim',
  `assert lapinbim.__version__ == ${JSON.stringify(version)}, lapinbim.__version__`,
  'print("wheel imports:", lapinbim.__version__, len(names), "files")',
].join('\n')]);
const sha = (f) => new Promise((done) => { const h = createHash('sha256'); createReadStream(f).on('data', (d) => h.update(d)).on('end', () => done(h.digest('hex'))); });
const files = readdirSync(dist).sort().map((f) => join(dist, f));
for (const f of files) console.log(`  ${await sha(f)}  ${f}`);

// 4. The upload, by twine, with the user's own token.
if (process.argv.includes('--upload')) {
  const twine = spawnSync(python, ['-m', 'twine', '--version'], { encoding: 'utf8' });
  if (twine.status !== 0) fail(`twine is not installed for ${python}: ${python} -m pip install --user twine`);
  const repo = process.argv.includes('--test') ? ['--repository', 'testpypi'] : [];
  const r = spawnSync(python, ['-m', 'twine', 'upload', ...repo, ...files], { stdio: 'inherit' });
  if (r.status !== 0) fail('The upload failed.');
  console.log(`lapinbim ${version} is on ${repo.length ? 'TestPyPI' : 'PyPI'}: pip install lapinbim==${version}`);
} else {
  console.log(`Built, not uploaded. To publish: node scripts/publish-python.mjs --upload  (twine asks for the PyPI token)`);
}
process.exit(0);
