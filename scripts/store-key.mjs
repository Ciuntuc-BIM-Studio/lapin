#!/usr/bin/env node
// The Lapin Extensions store key (docs/plan-extensions.md §9.7, stage 1): an Ed25519 key of its own, separate from the
// updater's, which signs the catalogue's index and each of its packages (lapin-extensions: tools/publish.mjs). Kept like
// the updater's key: an encrypted file outside the repository, its password in the macOS keychain, never in a chat or a log.
//
//   node scripts/store-key.mjs                 makes ~/.lapin/store-key.pem (PKCS#8, AES-256 with a random password kept as
//                                              the keychain item lapin-store-key, account lapin) unless it exists, and prints
//                                              the public key's line for ui/src/platform/extensions/keys.ts
//   node scripts/store-key.mjs --public        prints that line for the existing key (its password from the keychain)
//   node scripts/store-key.mjs --out <file> --plain
//                                              a throwaway key, unencrypted, nothing in the keychain (tests, a local catalogue)
//
// The private key and its password are never printed. Node 22, no dependencies (macOS's `security` for the keychain).
import { spawnSync } from 'node:child_process';
import { createHash, createPrivateKey, createPublicKey, generateKeyPairSync, randomBytes } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { dirname, join } from 'node:path';
import { pathToFileURL } from 'node:url';

export const KEYCHAIN_ITEM = 'lapin-store-key';
export const DEFAULT_FILE = join(homedir(), '.lapin', 'store-key.pem');

/** A store key's public half as keys.ts lists it: its id (16 hex digits of the SHA-256 of the raw bytes) and the raw bytes in base64. */
export function publicEntry(key) {
  const raw = Buffer.from(createPublicKey(key).export({ format: 'jwk' }).x, 'base64url');
  return { id: createHash('sha256').update(raw).digest('hex').slice(0, 16), publicKey: raw.toString('base64') };
}

/** A new key as PEM: encrypted with `password`, or plain when none is given. */
export function makeKey(password) {
  const { privateKey } = generateKeyPairSync('ed25519');
  const pem = privateKey.export(password ? { format: 'pem', type: 'pkcs8', cipher: 'aes-256-cbc', passphrase: password } : { format: 'pem', type: 'pkcs8' });
  return { pem, entry: publicEntry(privateKey) };
}

const line = (e) => `  { id: '${e.id}', publicKey: '${e.publicKey}' },`;

function keychainPassword() {
  const r = spawnSync('security', ['find-generic-password', '-s', KEYCHAIN_ITEM, '-a', 'lapin', '-w'], { encoding: 'utf8' });
  return r.status === 0 ? r.stdout.trim() : null;
}

function main(argv) {
  const opt = (n) => { const i = argv.indexOf(n); return i >= 0 ? argv[i + 1] : undefined; };
  const file = opt('--out') ?? DEFAULT_FILE;

  if (argv.includes('--plain')) {
    if (!opt('--out')) { console.error('--plain needs --out <file>: a throwaway key never goes where the real one lives.'); return 1; }
    const { pem, entry } = makeKey(null);
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, pem, { mode: 0o600 });
    console.log(`A throwaway store key (unencrypted) → ${file}\n${line(entry)}`);
    return 0;
  }

  if (argv.includes('--public')) {
    if (!existsSync(file)) { console.error(`No store key at ${file}.`); return 1; }
    const password = process.env.LAPIN_STORE_KEY_PASSWORD ?? keychainPassword();
    if (!password) { console.error(`No password for it: the keychain item ${KEYCHAIN_ITEM} is missing (or set LAPIN_STORE_KEY_PASSWORD).`); return 1; }
    const key = createPrivateKey({ key: readFileSync(file, 'utf8'), format: 'pem', passphrase: password });
    console.log(line(publicEntry(key)));
    return 0;
  }

  if (existsSync(file)) { console.error(`${file} exists: the store key is made once. --public prints its line for keys.ts.`); return 1; }
  if (process.platform !== 'darwin') { console.error('The store key is made on the maintainer\'s Mac (its password goes in the keychain).'); return 1; }
  if (keychainPassword()) { console.error(`The keychain already has ${KEYCHAIN_ITEM} but there is no ${file}: restore the file from its backup rather than make a new key.`); return 1; }
  const password = randomBytes(32).toString('base64url');
  // The password goes to the keychain as an argument of `security`, on this computer only; it is never printed.
  const r = spawnSync('security', ['add-generic-password', '-s', KEYCHAIN_ITEM, '-a', 'lapin', '-l', 'Lapin Extensions store key', '-w', password], { stdio: ['ignore', 'ignore', 'inherit'] });
  if (r.status !== 0) { console.error('The password could not be put in the keychain: nothing was made.'); return 1; }
  const { pem, entry } = makeKey(password);
  mkdirSync(dirname(file), { recursive: true, mode: 0o700 });
  writeFileSync(file, pem, { mode: 0o600 });
  console.log(`The store key → ${file} (encrypted; its password is the keychain item ${KEYCHAIN_ITEM}).`);
  console.log('Back the file up with the updater key (both are needed to publish). Then add this line to STORE_KEYS in ui/src/platform/extensions/keys.ts:');
  console.log(line(entry));
  return 0;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) process.exit(main(process.argv.slice(2)));
