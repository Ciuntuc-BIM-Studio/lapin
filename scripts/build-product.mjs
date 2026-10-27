#!/usr/bin/env node
// Build one product as a desktop app, with only its modules, engine and UI
// alike: a module the product does not have is not in the package at all.
//   pnpm build:product <product>      (lapin | lapin-architecture | lapin-architecture-structure | …)
// Output: build/<product>/bundle. Each product is its own app (name and identifier).
//
// On macOS the app is signed with the Developer ID Application certificate in
// the keychain (or LAPIN_SIGNING_IDENTITY), put in a DMG, and the DMG notarized
// and stapled with the notarytool keychain profile LAPIN_NOTARY_PROFILE
// (default "lapin-notary"; docs/release-macos.md). Without a certificate the
// app is built unsigned, as before; without the profile it is signed, not notarized.
//
// The version is 0.1.<the commit count of HEAD> (or LAPIN_VERSION), the same on
// every machine for one commit, so the updater can tell a newer build. The app
// looks for updates at ciuntucbimstudio.ro/downloads/<product>-update/; on macOS
// the signed, stapled app is also packed as <name>_<version>_<arch>.app.tar.gz,
// what the updater installs. scripts/publish-update.mjs signs the packages and
// puts them there.
import { spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readdirSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { dirname, join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { resolveProduct } from './product.mjs';
import { checkDmg } from './check-dmg.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const productId = process.argv[2];
if (!productId) { console.error('Usage: pnpm build:product <product>'); process.exit(1); }
let product;
try { product = resolveProduct(root, productId); } catch (e) { console.error(e.message); process.exit(1); }
const out = join(root, 'build', productId);
const tauriDir = join(root, 'desktop/src-tauri');

// pnpm is pnpm.cmd on Windows, which Node starts only through the shell; there
// an argument with a space (a path) is quoted, the shell would split it otherwise.
const win = process.platform === 'win32';
const shellArgs = (cmd, args) => (win && cmd === 'pnpm' ? args.map((a) => (/\s/.test(a) ? `"${a}"` : a)) : args);
const sh = (cmd, args, opts = {}) => {
  console.log(`▸ ${cmd} ${args.join(' ')}`);
  const r = spawnSync(cmd, shellArgs(cmd, args), { cwd: root, stdio: 'inherit', shell: win && cmd === 'pnpm', ...opts, env: { ...process.env, ...opts.env } });
  if (r.error) { console.error(`Cannot run ${cmd}: ${r.error.message}`); process.exit(1); }
  if (r.status !== 0) process.exit(r.status ?? 1);
};

// The target triple of this machine, from the Rust toolchain the desktop shell is built with.
const rustc = spawnSync('rustc', ['-vV'], { encoding: 'utf8' });
const triple = rustc.stdout?.match(/^host: (.+)$/m)?.[1].trim();
if (!triple) {
  console.error(`Cannot read the target from rustc${rustc.error ? ` (${rustc.error.message})` : ''}: install Rust (https://rustup.rs) and make sure rustc is on the PATH.`);
  process.exit(1);
}
const rid = { 'aarch64-apple-darwin': 'osx-arm64', 'x86_64-apple-darwin': 'osx-x64', 'x86_64-pc-windows-msvc': 'win-x64', 'x86_64-unknown-linux-gnu': 'linux-x64' }[triple];
if (!rid) { console.error(`No .NET runtime id for ${triple}`); process.exit(1); }
const exe = triple.includes('windows') ? '.exe' : '';

const commits = spawnSync('git', ['rev-list', '--count', 'HEAD'], { cwd: root, encoding: 'utf8' }).stdout?.trim();
const version = process.env.LAPIN_VERSION ?? (commits ? `0.1.${commits}` : null);
if (!version || !/^\d+\.\d+\.\d+$/.test(version)) { console.error(`No version: set LAPIN_VERSION (x.y.z), or build from a git checkout (${version ?? 'no commit count'}).`); process.exit(1); }
console.log(`▸ ${product.name} ${version}`);

rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });

// 1. Engine: the modules (Release), then the host as one self-contained file.
sh('dotnet', ['build', 'engine/Lapin.slnx', '-c', 'Release', '-v', 'q', '-nologo']);
for (const csproj of product.engineProjects ?? []) sh('dotnet', ['build', csproj, '-c', 'Release', '-v', 'q', '-nologo']);
sh('dotnet', ['publish', 'engine/src/Host/Lapin.Host', '-c', 'Release', '-r', rid, '--self-contained', 'true',
  `-p:Version=${version}`, '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=none', '-o', join(out, 'host'), '-v', 'q', '-nologo']);
mkdirSync(join(tauriDir, 'binaries'), { recursive: true });
cpSync(join(out, 'host', `Lapin.Host${exe}`), join(tauriDir, 'binaries', `lapin-host-${triple}${exe}`));

// 2. Resources: the manifest and ONLY this product's engine modules.
const res = join(tauriDir, 'resources');
rmSync(res, { recursive: true, force: true });
mkdirSync(join(res, 'modules'), { recursive: true });
const manifest = join(res, 'product.json');
writeFileSync(manifest, JSON.stringify(product, null, 2));
for (const m of product.engine.filter((id) => id !== 'core')) {
  const src = join(root, 'engine/artifacts/modules', m);
  if (!existsSync(src)) { console.error(`Module ${m} was not built (${src}).`); process.exit(1); }
  mkdirSync(join(res, 'modules', m), { recursive: true });
  for (const f of readdirSync(src).filter((f) => !f.endsWith('.pdb'))) cpSync(join(src, f), join(res, 'modules', m, f));
}

// 3. UI: only this product's UI modules are bundled.
sh('pnpm', ['--filter', 'ui', 'build'], { env: { LAPIN_PRODUCT_FILE: manifest, LAPIN_UI_OUT: join(out, 'ui') } });

// 4. The desktop bundle, named and identified per product.
const mac = process.platform === 'darwin';
// The Developer ID Application identity: named, else the first one in the keychain.
const identity = mac ? process.env.LAPIN_SIGNING_IDENTITY
  ?? spawnSync('security', ['find-identity', '-v', '-p', 'codesigning'], { encoding: 'utf8' }).stdout?.match(/"(Developer ID Application: [^"]+)"/)?.[1] : undefined;
if (mac) console.log(identity ? `▸ signing as ${identity}` : '▸ no Developer ID Application certificate in the keychain: the app is built unsigned');
writeFileSync(join(tauriDir, 'tauri.product.json'), JSON.stringify({
  productName: product.name,
  version,
  identifier: `ro.ciuntucbimstudio.${productId}`,
  plugins: { updater: { endpoints: [`https://ciuntucbimstudio.ro/downloads/${productId}-update/{{target}}-{{arch}}-{{bundle_type}}.json`] } },
  // Relative to desktop/src-tauri, with forward slashes: Tauri reads an absolute Windows path ("D:\\a\\…") as a URL with the
  // scheme "d:", embeds nothing and the installed app opens on "File not found".
  build: { frontendDist: relative(tauriDir, join(out, 'ui')).split(sep).join('/') },
  app: { windows: [{ label: 'main', title: product.name, width: 1440, height: 900, minWidth: 1000, minHeight: 640, center: true }] },
  bundle: {
    externalBin: ['binaries/lapin-host'], resources: ['resources/**/*'],
    ...(process.platform === 'linux' ? { targets: ['deb', 'appimage'] } : {}),
    // Windows: an NSIS installer for the current user (no admin rights), which fetches WebView2 when it is missing.
    ...(win ? { targets: ['nsis'], windows: { nsis: { installMode: 'currentUser' } } } : {}),
    ...(mac ? { targets: ['app'], macOS: { minimumSystemVersion: '13.0', hardenedRuntime: true, entitlements: 'entitlements.plist', ...(identity ? { signingIdentity: identity } : {}) } } : {}),
  },
}, null, 2));
const bundleDir = join(tauriDir, 'target/release/bundle');
rmSync(bundleDir, { recursive: true, force: true }); // never ship another product's leftovers
// Notarization is done below with the keychain profile, never by Tauri from passwords in the environment.
if (mac) for (const k of ['APPLE_ID', 'APPLE_PASSWORD', 'APPLE_TEAM_ID', 'APPLE_API_KEY', 'APPLE_API_ISSUER', 'APPLE_API_KEY_PATH']) delete process.env[k];
sh('pnpm', ['tauri', 'build', '--config', join(tauriDir, 'tauri.product.json')], { cwd: join(root, 'desktop') });

// 5. macOS: the DMG (the app and a link to Applications; made here with hdiutil,
// not Tauri's Finder script, which fails when the image is busy), signed;
// its engine started from it and stopped before it is detached; the signature
// checked; the DMG notarized and stapled, and the app too.
if (mac) {
  const macos = join(bundleDir, 'macos');
  const apps = existsSync(macos) ? readdirSync(macos).filter((f) => f.endsWith('.app')) : [];
  const dmgDir = join(bundleDir, 'dmg');
  mkdirSync(dmgDir, { recursive: true });
  const dmgs = [];
  for (const app of apps) {
    const stage = join(bundleDir, 'dmg-stage');
    rmSync(stage, { recursive: true, force: true });
    mkdirSync(stage);
    sh('ditto', [join(macos, app), join(stage, app)]);
    symlinkSync('/Applications', join(stage, 'Applications'));
    const dmg = join(dmgDir, `${app.replace(/\.app$/, '')}_${version}_${triple.split('-')[0]}.dmg`);
    sh('hdiutil', ['create', '-volname', app.replace(/\.app$/, ''), '-srcfolder', stage, '-ov', '-format', 'UDZO', dmg]);
    rmSync(stage, { recursive: true, force: true });
    if (identity) sh('codesign', ['--sign', identity, '--timestamp', dmg]);
    dmgs.push(dmg);
  }
  // Each DMG opened and its engine started from it, then stopped before the image is detached.
  for (const dmg of dmgs) {
    console.log(`▸ checking ${dmg}`);
    try { await checkDmg(dmg); } catch (e) { console.error(`The DMG does not work: ${e.message}`); process.exit(1); }
  }
  if (identity) {
    for (const app of apps) sh('codesign', ['--verify', '--deep', '--strict', '--verbose=2', join(macos, app)]);
    const profile = process.env.LAPIN_NOTARY_PROFILE ?? 'lapin-notary';
    const known = spawnSync('xcrun', ['notarytool', 'history', '--keychain-profile', profile], { encoding: 'utf8' }).status === 0;
    if (!identity.startsWith('Developer ID Application')) console.log('▸ not a Developer ID certificate: signed for this Mac only, not notarized');
    else if (!known) console.log(`▸ no notarytool profile "${profile}" in the keychain: signed, not notarized (docs/release-macos.md)`);
    else {
      for (const dmg of dmgs) {
        sh('xcrun', ['notarytool', 'submit', dmg, '--keychain-profile', profile, '--wait']);
        sh('xcrun', ['stapler', 'staple', dmg]);
      }
      for (const app of apps) sh('xcrun', ['stapler', 'staple', join(macos, app)]);
      for (const app of apps) sh('spctl', ['--assess', '--type', 'execute', '--verbose=2', join(macos, app)]);
    }
  }
  // What the updater installs: the app as it is now (signed, stapled), packed on its own.
  for (const app of apps) sh('tar', ['-czf', join(macos, `${app.replace(/\.app$/, '')}_${version}_${triple.split('-')[0]}.app.tar.gz`), '-C', macos, app], { env: { COPYFILE_DISABLE: '1' } });
}

if (existsSync(bundleDir)) cpSync(bundleDir, join(out, 'bundle'), { recursive: true });
console.log(`\n✓ ${product.name} (${product.modules.join(' + ')}): ${join(out, 'bundle')}`);
