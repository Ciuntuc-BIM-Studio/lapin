#!/usr/bin/env node
// The GitHub Release that mirrors a published build (decision 663: `lapin` only), printed, never run: the user runs it
// (creating releases is refused to Claude in auto mode). docs/release-linux-and-github.md.
//   node scripts/github-release.mjs [product] [file or folder…] [--target <commit>]
// e.g. node scripts/github-release.mjs lapin build/lapin/bundle build/lapin-linux ~/Downloads/lapin-windows
// Default folders: build/<product>/bundle and build/<product>-linux. The packages are found by their names (Tauri's,
// as publish-update.mjs finds them): .dmg (macOS), -setup.exe and -portable.zip (Windows), .AppImage and .deb (Linux);
// all must be of one version (<name>_<version>_…). What is printed: the packages copied under the site's fixed names
// into build/github-release/<version>/, their SHA256SUMS.txt, the notes (the commit subjects since the release before),
// and `gh release create v<version>` on the build's commit (--target, else HEAD, checked against the version's
// commit count).
import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync, statSync, readFileSync } from 'node:fs';
import { basename, dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const targetAt = args.indexOf('--target');
const targetArg = targetAt >= 0 ? args.splice(targetAt, 2)[1] : null;
const [productId = 'lapin', ...given] = args;
const productFile = join(root, 'products', `${productId}.json`);
if (!existsSync(productFile)) { console.error(`No ${relative(root, productFile)}.`); process.exit(1); }
const product = JSON.parse(readFileSync(productFile, 'utf8'));
const inputs = given.length ? given.map((p) => resolve(p)) : [join(root, 'build', productId, 'bundle'), join(root, 'build', `${productId}-linux`)].filter(existsSync);

// The download names, the same as on the site (publish-update.mjs).
const prefix = productId === 'lapin' ? 'Lapin' : (product.name ?? productId).replace(/\s+/g, '-');
const KINDS = [
  { test: /\.dmg$/, name: `${prefix}-macOS-AppleSilicon.dmg`, platform: 'macOS (Apple Silicon)' },
  { test: /-setup\.exe$/, name: `${prefix}-Windows-x64-setup.exe`, platform: 'Windows' },
  { test: /-portable\.zip$/, name: `${prefix}-Windows-x64-portable.zip`, platform: 'Windows' },
  { test: /\.AppImage$/, name: `${prefix}-Linux-x64.AppImage`, platform: 'Linux' },
  { test: /\.deb$/, name: `${prefix}-Linux-x64.deb`, platform: 'Linux' },
];
const walk = (p) => (statSync(p).isDirectory() ? readdirSync(p).flatMap((f) => walk(join(p, f))) : [p]);
const found = inputs.flatMap((i) => (existsSync(i) ? walk(i) : [])).filter((f) => !basename(f).startsWith('.'))
  .map((f) => ({ file: f, kind: KINDS.find((k) => k.test.test(f)) })).filter((x) => x.kind);
if (found.length === 0) { console.error(`No package in ${inputs.map((i) => relative(root, i) || '.').join(', ') || '(nothing given)'}.`); process.exit(1); }
for (const k of KINDS) {
  const of = found.filter((x) => x.kind === k);
  if (of.length > 1) { console.error(`Two packages for ${k.name}: ${of.map((x) => x.file).join(', ')}`); process.exit(1); }
}

const versions = [...new Set(found.map((x) => basename(x.file).match(/_(\d+\.\d+\.\d+)_/)?.[1] ?? '?'))];
if (versions.length !== 1 || versions[0] === '?') { console.error(`The packages are not of one version: ${found.map((x) => `${basename(x.file)}`).join(', ')}`); process.exit(1); }
const version = versions[0];
const tag = `v${version}`;

const git = (...a) => spawnSync('git', a, { cwd: root, encoding: 'utf8' }).stdout?.trim() ?? '';
const commit = git('rev-parse', targetArg ?? 'HEAD');
if (!commit) { console.error(`No commit ${targetArg ?? 'HEAD'}.`); process.exit(1); }
const count = git('rev-list', '--count', commit);
const warnings = [];
if (`0.1.${count}` !== version) warnings.push(`${commit.slice(0, 8)} is build 0.1.${count}, not ${version}: pass --target <the build's commit> (its BUILD.txt in the server's archive names it).`);
if (git('tag', '--list', tag)) warnings.push(`The tag ${tag} exists already: gh release create uses it as it is.`);
const missing = [...new Set(KINDS.map((k) => k.platform))].filter((p) => !found.some((x) => x.kind.platform === p));
if (missing.length) warnings.push(`No package for ${missing.join(', ')}: the release would lack it (gh release upload ${tag} <file> adds one later).`);

// The notes: as the releases before (0.1.1355), the commit subjects since the last release tag, newest first.
const previous = git('describe', '--tags', '--abbrev=0', '--match', 'v*', `${commit}^`);
const subjects = (previous ? git('log', '--no-merges', '--format=%s', `${previous}..${commit}`) : '').split('\n').filter(Boolean)
  .map((s) => s.replace(/^\w+(\([^)]*\))?!?:\s*/, '')).map((s) => (s.length > 100 ? `${s.slice(0, 99)}…` : s));
const list = (p) => found.filter((x) => x.kind.platform === p).map((x) => `\`${x.kind.name}\``).join(', ');
const notes = [
  `${product.name ?? 'Lapin'} ${version}, the build published on lapinbim.com.`,
  '',
  'Official downloads, release notes and update feeds: https://lapinbim.com/download/ (Romanian: https://lapinbim.com/ro/download/). These files are the same ones, mirrored here with their SHA-256 sums (`SHA256SUMS.txt`). The macOS build is signed and notarized.',
  '',
  ...[['macOS (Apple Silicon)', ''], ['Windows', ' (unsigned)'], ['Linux', ' (unsigned)']].filter(([p]) => list(p)).map(([p, s]) => `- ${p}: ${list(p)}${s}`),
  '',
  'Installed apps update themselves from lapinbim.com.',
  ...(subjects.length ? ['', `What changed since ${previous.replace(/^v/, '')}:`, '', ...subjects.map((s) => `- ${s}`)] : []),
].join('\n');

const q = (s) => `'${s.replace(/'/g, `'\\''`)}'`;
const dir = join('build', 'github-release', version);
const out = [
  `# ${product.name ?? productId} ${version} as a GitHub Release (${commit.slice(0, 8)}${previous ? `, notes since ${previous}` : ''}). Nothing below has been run.`,
  ...warnings.map((w) => `# ! ${w}`),
  `cd ${q(root)}`,
  `rm -rf ${dir} && mkdir -p ${dir}`,
  ...found.map((x) => `cp ${q(x.file)} ${dir}/${x.kind.name}`),
  `(cd ${dir} && shasum -a 256 ${found.map((x) => x.kind.name).join(' ')} > SHA256SUMS.txt)`,
  `cat > ${dir}/NOTES.md <<'NOTES'`,
  notes,
  'NOTES',
  `gh release create ${tag} --target ${commit} --title ${q(`${product.name ?? 'Lapin'} ${version}`)} --notes-file ${dir}/NOTES.md \\`,
  `  ${[...found.map((x) => `${dir}/${x.kind.name}`), `${dir}/SHA256SUMS.txt`].join(' \\\n  ')}`,
];
console.log(out.join('\n'));
