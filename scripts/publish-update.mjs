#!/usr/bin/env node
// Publish built packages of one product on the site: as the downloads the site
// links to (fixed names, ciuntucbimstudio.ro/downloads/<product>/), and as an
// update the installed apps find (ciuntucbimstudio.ro/downloads/<product>-update/).
//   node scripts/publish-update.mjs <product> <file or folder>… [--notes "what changed"]
// e.g. node scripts/publish-update.mjs lapin build/lapin/bundle build/lapin-linux "windows builds"
// What a folder holds is found by its names (Tauri's): <name>_<version>_….dmg and
// .app.tar.gz (macOS), -setup.exe and -portable.zip (Windows), .AppImage and .deb (Linux).
//
// Each update package is signed here with the update key (~/.tauri/lapin-updater.key,
// or LAPIN_UPDATER_KEY; its password from the macOS keychain item "lapin-updater",
// or TAURI_SIGNING_PRIVATE_KEY_PASSWORD), the signature bound to the version, so
// the key never leaves this machine and no build needs it. One manifest per
// platform and kind of package (<target>-<arch>-<kind>.json, what the app asks
// for), uploaded after its package. The notes default to the commit subjects
// since the version before (from the git history), shortened.
// Uploads go through ciuntucbimstudio-site/deploy/upload-downloads.sh (LAPIN_SITE_DIR).
import { spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { homedir, tmpdir } from 'node:os';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const notesAt = args.indexOf('--notes');
const notesArg = notesAt >= 0 ? args.splice(notesAt, 2)[1] : null;
const [productId, ...inputs] = args;
if (!productId || inputs.length === 0) { console.error('Usage: node scripts/publish-update.mjs <product> <file or folder>… [--notes "…"]'); process.exit(1); }
const product = JSON.parse(readFileSync(join(root, 'products', `${productId}.json`), 'utf8'));
const site = resolve(process.env.LAPIN_SITE_DIR ?? join(root, '../ciuntucbimstudio-site'));
const upload = join(site, 'deploy/upload-downloads.sh');
if (!existsSync(upload)) { console.error(`No ${upload} (set LAPIN_SITE_DIR to the site's checkout).`); process.exit(1); }

// The kinds of package: how a file is recognised, its download name, the updater's target.
const prefix = productId === 'lapin' ? 'Lapin' : (product.name ?? productId).replace(/\s+/g, '-');
const KINDS = [
  { test: /\.app\.tar\.gz$/, target: 'darwin-aarch64-app' },
  { test: /\.dmg$/, download: `${prefix}-macOS-AppleSilicon.dmg` },
  { test: /-setup\.exe$/, download: `${prefix}-Windows-x64-setup.exe`, target: 'windows-x86_64-nsis' },
  { test: /-portable\.zip$/, download: `${prefix}-Windows-x64-portable.zip` },
  { test: /\.AppImage$/, download: `${prefix}-Linux-x64.AppImage`, target: 'linux-x86_64-appimage' },
  { test: /\.deb$/, download: `${prefix}-Linux-x64.deb`, target: 'linux-x86_64-deb' },
];
const walk = (p) => (statSync(p).isDirectory() ? readdirSync(p).flatMap((f) => walk(join(p, f))) : [p]);
const files = inputs.flatMap((i) => walk(resolve(i))).filter((f) => KINDS.some((k) => k.test.test(f)) && !basename(f).startsWith('.'));
if (files.length === 0) { console.error('No package found in what was given.'); process.exit(1); }

const sh = (cmd, a, opts = {}) => {
  const r = spawnSync(cmd, a, { stdio: 'inherit', ...opts });
  if (r.status !== 0) { console.error(`${cmd} failed`); process.exit(r.status ?? 1); }
};
const key = process.env.LAPIN_UPDATER_KEY ?? join(homedir(), '.tauri/lapin-updater.key');
const password = process.env.TAURI_SIGNING_PRIVATE_KEY_PASSWORD
  ?? spawnSync('security', ['find-generic-password', '-s', 'lapin-updater', '-a', 'lapin', '-w'], { encoding: 'utf8' }).stdout?.trim();
const work = mkdtempSync(join(tmpdir(), 'lapin-publish-'));

// The notes: the commit subjects (features and fixes) since the version the
// site offers now, up to this one; a version is the commit count of its commit.
const git = (a) => spawnSync('git', a, { cwd: root, encoding: 'utf8' }).stdout?.trim() ?? '';
const head = Number(git(['rev-list', '--count', 'HEAD']));
const commitOf = (version) => { const n = head - Number(version.split('.')[2]); return n >= 0 ? `HEAD~${n}` : null; };
const notesFor = async (version, target) => {
  if (notesArg) return notesArg;
  const at = commitOf(version);
  if (!at) return `${product.name ?? 'Lapin'} ${version}`;
  const before = await fetch(`https://ciuntucbimstudio.ro/downloads/${productId}-update/${target}.json`).then((r) => (r.ok ? r.json() : null)).catch(() => null);
  const since = before?.version ? commitOf(before.version) : null;
  const subjects = git(['log', '--format=%s', '--no-merges', since ? `${since}..${at}` : `${at}~10..${at}`]).split('\n')
    .filter((t) => /^(feat|fix)/.test(t)).slice(0, 15)
    .map((t) => t.replace(/^\w+(\([^)]*\))?:\s*/, '').split(/[;:(]/)[0].trim())
    .map((t) => `• ${t.length > 100 ? `${t.slice(0, 99).replace(/\s+\S*$/, '')}…` : t}`);
  return subjects.length ? subjects.join('\n') : `${product.name ?? 'Lapin'} ${version}`;
};

const downloads = [];
const updates = [];
for (const file of files) {
  const kind = KINDS.find((k) => k.test.test(file));
  const version = basename(file).match(/_(\d+\.\d+\.\d+)_/)?.[1];
  if (kind.download) downloads.push(`${file}=${kind.download}`);
  if (!kind.target) continue;
  if (!version) { console.error(`${basename(file)}: no version in its name.`); process.exit(1); }
  if (!existsSync(key) || !password) { console.error(`No update key (${key}) or no password for it: the updates cannot be signed.`); process.exit(1); }
  console.log(`▸ signing ${basename(file)} (${version})`);
  sh('pnpm', ['-s', 'tauri', 'signer', 'sign', '-f', key, '--app-version', version, file], { cwd: root, env: { ...process.env, TAURI_SIGNING_PRIVATE_KEY_PASSWORD: password }, stdio: ['ignore', 'ignore', 'inherit'] });
  const published = basename(file).replace(/\s+/g, '-');
  const manifest = join(work, `${kind.target}.json`);
  writeFileSync(manifest, JSON.stringify({
    version,
    notes: await notesFor(version, kind.target),
    pub_date: new Date().toISOString(),
    url: `https://ciuntucbimstudio.ro/downloads/${productId}-update/${encodeURIComponent(published)}`,
    signature: readFileSync(`${file}.sig`, 'utf8').trim(),
  }, null, 2));
  updates.push(`${file}=${published}`, `${manifest}=${kind.target}.json`);
}

if (updates.length) sh(upload, [`${productId}-update`, ...updates]);
if (downloads.length) sh(upload, [productId, ...downloads]);
console.log(`✓ ${downloads.length} download(s), ${updates.length / 2} update(s) of ${productId}`);
