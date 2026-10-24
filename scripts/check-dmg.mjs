// Checking a macOS DMG the safe way: attach it (read-only, out of sight),
// start the engine of each app inside and wait for it to be ready, stop it
// and wait until it has exited, and only then detach the image — never by
// force while something still runs from it (an engine whose image is pulled
// from under it cannot run its code any more; see ExitGuard.cs).
//   node scripts/check-dmg.mjs <file.dmg>

import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync, readdirSync, realpathSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const alive = (pid) => { try { process.kill(pid, 0); return true; } catch (e) { return e.code === 'EPERM'; } };

/** Start an engine and wait for its ready line; resolves to the child. */
async function startEngine(bin, resources, userDir) {
  const child = spawn(bin, ['--product', join(resources, 'product.json'), '--modules-dir', join(resources, 'modules'), '--user-dir', userDir, '--port', '0', '--parent-pid', String(process.pid)], { stdio: ['ignore', 'pipe', 'pipe'] });
  let out = '';
  child.stdout.on('data', (d) => { out += d; });
  child.stderr.on('data', (d) => { out += d; });
  for (let i = 0; i < 120 && !/LAPIN_HOST_READY/.test(out) && child.exitCode === null; i++) await sleep(250);
  if (!/LAPIN_HOST_READY/.test(out)) throw Object.assign(new Error(`the engine did not start:\n${out.slice(-2000)}`), { child });
  return child;
}

/** Stop an engine and wait until it is gone (SIGTERM, then SIGKILL after 10 s). */
async function stopEngine(child) {
  if (!child || !alive(child.pid)) return;
  child.kill('SIGTERM');
  for (let i = 0; i < 40 && alive(child.pid); i++) await sleep(250);
  if (alive(child.pid)) { child.kill('SIGKILL'); for (let i = 0; i < 20 && alive(child.pid); i++) await sleep(250); }
}

export async function checkDmg(dmg) {
  // The real path: hdiutil prints /private/var/… for /var/….
  const mountRoot = realpathSync(mkdtempSync(join(tmpdir(), 'lapin-dmg-')));
  const userDir = mkdtempSync(join(tmpdir(), 'lapin-dmg-user-'));
  const attach = spawnSync('hdiutil', ['attach', '-nobrowse', '-readonly', '-noverify', '-mountrandom', mountRoot, dmg], { encoding: 'utf8' });
  if (attach.status !== 0) throw new Error(`hdiutil attach failed: ${attach.stderr}`);
  const mount = attach.stdout.split('\n').map((l) => l.split('\t').pop()?.trim()).find((p) => p?.startsWith(mountRoot));
  if (!mount) {
    // Attached somewhere unexpected: detach every volume it made.
    for (const dev of attach.stdout.split('\n').map((l) => l.split('\t')[0].trim()).filter((d) => /^\/dev\/disk\d+$/.test(d))) spawnSync('hdiutil', ['detach', dev], { stdio: 'ignore' });
    throw new Error(`no mount point in:\n${attach.stdout}`);
  }
  let engine = null;
  try {
    const apps = readdirSync(mount).filter((f) => f.endsWith('.app'));
    if (!apps.length) throw new Error('no app in the DMG');
    for (const app of apps) {
      const bin = join(mount, app, 'Contents', 'MacOS', 'lapin-host');
      if (!existsSync(bin)) throw new Error(`${app} has no lapin-host`);
      engine = await startEngine(bin, join(mount, app, 'Contents', 'Resources', 'resources'), userDir);
      console.log(`▸ ${app}: the engine starts from the DMG`);
      await stopEngine(engine);
      engine = null;
    }
  } finally {
    // Nothing may run from the image when it is detached.
    await stopEngine(engine);
    let detached = false;
    for (let i = 0; i < 5 && !detached; i++) {
      detached = spawnSync('hdiutil', ['detach', mount], { stdio: 'ignore' }).status === 0;
      if (!detached) await sleep(1000);
    }
    if (!detached) console.error(`▸ ${mount} is still busy: not detached by force; detach it once nothing runs from it`);
    rmSync(userDir, { recursive: true, force: true });
    if (detached) rmSync(mountRoot, { recursive: true, force: true });
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const dmg = process.argv[2];
  if (!dmg) { console.error('usage: node scripts/check-dmg.mjs <file.dmg>'); process.exit(2); }
  checkDmg(dmg).then(() => console.log('✓ the DMG works'), (e) => { console.error(`✗ ${e.message}`); process.exit(1); });
}
