/**
 * The catalogue's signatures (docs/plan-extensions.md §9.7, stage 1): the index's detached signature (`index.json.sig`,
 * `{format: "lapin-signature", version: 1, keyId, value}` over the index's exact bytes) and each package's
 * (`package.signature`, over `packageMessage`: its id, version, SHA-256 and size), both Ed25519 by a store key of
 * `keys.ts`. The same texts are signed by lapin-extensions' tools/sign.mjs.
 *
 * Verification uses the platform's WebCrypto Ed25519 where it has it, else a small verifier of its own (RFC 8032 §5.1.7,
 * with BigInt): only public data goes through it, so it need not be constant-time. Pure: the keys are given.
 */

import type { Manifest } from './manifest';

export interface StoreKey { id: string; publicKey: string }
export interface Signature { keyId: string; value: string }

/** The text a package's signature is over. */
export const packageMessage = (m: Pick<Manifest, 'id' | 'version' | 'package'>): string =>
  `lapin-extension-package\n1\n${m.id}\n${m.version}\n${m.package.sha256}\n${m.package.size}\n`;

const fromBase64 = (s: string): Uint8Array => Uint8Array.from(atob(s), (c) => c.charCodeAt(0));
const isSignature = (s: unknown): s is Signature => !!s && typeof s === 'object' && /^[0-9a-f]{16}$/.test((s as Signature).keyId ?? '') && /^[A-Za-z0-9+/]{86}==$/.test((s as Signature).value ?? '');

/** An index's signature file read; null when it is not one. */
export function readSignatureFile(bytes: Uint8Array | null): Signature | null {
  if (!bytes) return null;
  try {
    const o = JSON.parse(new TextDecoder().decode(bytes)) as { format?: string; version?: number } & Signature;
    return o.format === 'lapin-signature' && o.version === 1 && isSignature(o) ? { keyId: o.keyId, value: o.value } : null;
  } catch { return null; }
}

// ------------------------------------------------------------ Ed25519 verification

const P = 2n ** 255n - 19n;
const L = 2n ** 252n + 27742317777372353535851937790883648493n;
const mod = (a: bigint, m = P) => { const r = a % m; return r >= 0n ? r : r + m; };
function pow(b: bigint, e: bigint): bigint {
  let r = 1n;
  b = mod(b);
  while (e > 0n) { if (e & 1n) r = (r * b) % P; b = (b * b) % P; e >>= 1n; }
  return r;
}
const inv = (a: bigint) => pow(a, P - 2n);
const D = mod(-121665n * inv(121666n));
const D2 = mod(2n * D);
const SQRT_M1 = pow(2n, (P - 1n) / 4n);
/** A point in extended coordinates (X, Y, Z, T), x = X/Z, y = Y/Z, xy = T/Z. */
type Point = [bigint, bigint, bigint, bigint];
const IDENTITY: Point = [0n, 1n, 1n, 0n];

/** Addition on the twisted Edwards curve (a = -1), complete: it doubles too (RFC 8032 §5.1.4). */
function add([X1, Y1, Z1, T1]: Point, [X2, Y2, Z2, T2]: Point): Point {
  const a = mod((Y1 - X1) * (Y2 - X2)), b = mod((Y1 + X1) * (Y2 + X2)), c = mod(T1 * D2 * T2), d = mod(Z1 * 2n * Z2);
  const e = b - a, f = d - c, g = d + c, h = b + a;
  return [mod(e * f), mod(g * h), mod(f * g), mod(e * h)];
}
function times(p: Point, n: bigint): Point {
  let r = IDENTITY, q = p;
  while (n > 0n) { if (n & 1n) r = add(r, q); q = add(q, q); n >>= 1n; }
  return r;
}
const littleEndian = (b: Uint8Array) => { let n = 0n; for (let i = b.length - 1; i >= 0; i--) n = (n << 8n) | BigInt(b[i]); return n; };

/** A point from its 32 bytes (RFC 8032 §5.1.3); null when the bytes are no point. */
function decodePoint(bytes: Uint8Array): Point | null {
  if (bytes.length !== 32) return null;
  // A copy: a Node Buffer's slice() is a view, and the sign bit cleared here must not touch the caller's bytes.
  const b = new Uint8Array(bytes);
  const sign = (b[31] >> 7) & 1;
  b[31] &= 0x7f;
  const y = littleEndian(b);
  if (y >= P) return null;
  const y2 = (y * y) % P;
  const u = mod(y2 - 1n), v = mod(D * y2 + 1n);
  const v3 = (((v * v) % P) * v) % P, v7 = (((v3 * v3) % P) * v) % P;
  let x = mod(((u * v3) % P) * pow((u * v7) % P, (P - 5n) / 8n));
  const vx2 = mod(((v * x) % P) * x);
  if (vx2 !== u) {
    if (vx2 !== mod(-u)) return null;
    x = (x * SQRT_M1) % P;
  }
  if (x === 0n && sign === 1) return null;
  if (Number(x & 1n) !== sign) x = P - x;
  return [x, y, 1n, (x * y) % P];
}

const BASE = (() => {
  const y = (4n * inv(5n)) % P;
  const b = new Uint8Array(32);
  let n = y;
  for (let i = 0; i < 32; i++) { b[i] = Number(n & 255n); n >>= 8n; }
  return decodePoint(b)!;
})();

const samePoint = ([X1, Y1, Z1]: Point, [X2, Y2, Z2]: Point) => mod(X1 * Z2 - X2 * Z1) === 0n && mod(Y1 * Z2 - Y2 * Z1) === 0n;

/** Ed25519 verification without WebCrypto's: [S]B = R + [k]A, k = SHA-512(R ‖ A ‖ M) mod L. */
export async function verifyEd25519Own(publicKey: Uint8Array, signature: Uint8Array, message: Uint8Array): Promise<boolean> {
  if (publicKey.length !== 32 || signature.length !== 64) return false;
  const A = decodePoint(publicKey);
  const R = decodePoint(signature.subarray(0, 32));
  const S = littleEndian(signature.subarray(32));
  if (!A || !R || S >= L) return false;
  const joined = new Uint8Array(64 + message.length);
  joined.set(signature.subarray(0, 32), 0);
  joined.set(publicKey, 32);
  joined.set(message, 64);
  const k = mod(littleEndian(new Uint8Array(await globalThis.crypto.subtle.digest('SHA-512', joined))), L);
  return samePoint(times(BASE, S), add(R, times(A, k)));
}

/** Ed25519 verification: the platform's when it has it (WebView2, WKWebView, Node), else Lapin's own. */
export async function verifyEd25519(publicKey: Uint8Array, signature: Uint8Array, message: Uint8Array): Promise<boolean> {
  let key: CryptoKey | null = null;
  try { key = await globalThis.crypto.subtle.importKey('raw', publicKey as BufferSource, { name: 'Ed25519' }, false, ['verify']); } catch { /* no Ed25519 in this WebCrypto */ }
  if (key) {
    try { return await globalThis.crypto.subtle.verify({ name: 'Ed25519' }, key, signature as BufferSource, message as BufferSource); } catch { return false; }
  }
  return verifyEd25519Own(publicKey, signature, message);
}

/** Whether a signature is by one of the keys, over the bytes. */
export async function verifySignature(keys: readonly StoreKey[], sig: Signature | null | undefined, bytes: Uint8Array): Promise<boolean> {
  if (!isSignature(sig)) return false;
  const key = keys.find((k) => k.id === sig.keyId);
  if (!key) return false;
  try { return await verifyEd25519(fromBase64(key.publicKey), fromBase64(sig.value), bytes); } catch { return false; }
}

/** How a catalogue is signed: by a key Lapin trusts, not at all, or wrongly (a signature that does not hold: tampered, or an unknown key). */
export type CatalogueTrust = 'signed' | 'unsigned' | 'bad';

export async function catalogueTrust(keys: readonly StoreKey[], index: Uint8Array, sigFile: Uint8Array | null): Promise<CatalogueTrust> {
  if (!sigFile) return 'unsigned';
  const sig = readSignatureFile(sigFile);
  return sig && (await verifySignature(keys, sig, index)) ? 'signed' : 'bad';
}

/**
 * Whether a catalogue may be read: Lapin's own must be signed once Lapin carries a store key; any other is read signed or
 * unsigned (the dialog warns), never wrongly signed. A sentence when it may not.
 */
export function trustProblem(trust: CatalogueTrust, o: { isDefault: boolean; keys: readonly StoreKey[] }): string | null {
  if (trust === 'bad') return 'The catalogue’s signature does not hold: it may have been changed. It was not read.';
  if (trust === 'unsigned' && o.isDefault && o.keys.length) return 'Lapin’s catalogue is not signed as it should be. It was not read; try again later.';
  return null;
}

/**
 * A package's signature checked before it is downloaded: a signature that is there must hold; `required` (Lapin's own
 * catalogue, once it carries a key) wants one. A sentence when the package may not be installed.
 */
export async function packageProblem(m: Manifest, o: { keys: readonly StoreKey[]; required: boolean }): Promise<string | null> {
  const sig = m.package.signature;
  if (!sig) return o.required ? 'The package is not signed by the catalogue: it was not installed.' : null;
  return (await verifySignature(o.keys, sig, new TextEncoder().encode(packageMessage(m)))) ? null : 'The package’s signature does not hold: it was not installed.';
}
