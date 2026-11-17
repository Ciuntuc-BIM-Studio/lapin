/**
 * Paid extensions in Lapin (docs/plan-extensions.md §12, stage 2a). A paid extension is sold by its author through their
 * own shop; the buyer gets a licence key, which Lapin sends to the store (POST, the key in the body) and the store checks
 * with the shop's provider: `POST <store>/api/licensed/<id>/<version> {key}` gives the package to a good key,
 * `POST <store>/api/licence/check {id, key}` only says what the key is now.
 *
 * The key stays on this computer (runtime.ts keeps it with the `lapin.*` settings); a good check is remembered, so nothing
 * is ever blocked offline once installed. Lapin checks again when the catalogue has a newer version or a week has passed;
 * a key refunded or disabled by its seller is remembered as revoked: no more updates, a notice, nothing installed removed.
 * Pure: the POST is given (the desktop's engine, the browser's fetch; tests give a fake).
 */

import { compareVersions, isPaid, type Manifest } from './manifest';
import type { InstalledExtension } from './install';

/** What the store says of a key: good, refunded / disabled, not one of this extension, or nothing concluded. */
export type LicenceStatus = 'active' | 'revoked' | 'invalid' | 'unavailable';

/** A paid extension's key as this computer keeps it, with what the last conclusive check said. */
export interface LicenceRecord { key: string; provider: string; status: 'active' | 'revoked'; checkedAt: string }

/** The store's answer to a POST: its HTTP status, its content type and its bytes. */
export interface StoreAnswer { status: number; type?: string | null; bytes: Uint8Array }
export type PostJson = (url: string, body: string) => Promise<StoreAnswer>;

/** How long a good check is trusted before Lapin asks again (it never blocks anything: it only decides updates). */
export const RECHECK_MS = 7 * 86400_000;

export const LICENCE_MESSAGES = {
  missing: 'Enter the licence key you received from the seller.',
  invalid: "This is not a licence key of this extension: check the key in the seller's e-mail.",
  revoked: 'This licence key is no longer valid: its purchase was refunded, or its seller disabled it.',
  unavailable: 'The store could not check the licence key just now: try again later.',
  file: 'The store does not have this file yet: try again later.',
} as const;

/** A licence problem, its kind kept so the dialog can remember a revoked key or ask for another. */
export class LicenceError extends Error {
  constructor(readonly reason: keyof typeof LICENCE_MESSAGES) { super(LICENCE_MESSAGES[reason]); }
}

/** The store's check address, beside the licensed package's: <origin>/api/licence/check. */
export const checkUrlOf = (m: Manifest): string => new URL('/api/licence/check', m.package.url).toString();

/** A key as typed: the spaces around it (a copy from an e-mail) taken off; null when it cannot be one. */
export function cleanKey(typed: string): string | null {
  const k = typed.trim();
  return /^[\x21-\x7e]{1,200}$/.test(k) ? k : null;
}

function answerJson(a: StoreAnswer): { status?: string; error?: string } {
  try {
    const o = JSON.parse(new TextDecoder().decode(a.bytes)) as unknown;
    return o && typeof o === 'object' ? (o as { status?: string; error?: string }) : {};
  } catch { return {}; }
}

/** A paid extension's package, from the store, for its key; a LicenceError when the key is not good now. */
export async function fetchLicensedPackage(m: Manifest, key: string, post: PostJson): Promise<Uint8Array> {
  const a = await post(m.package.url, JSON.stringify({ key }));
  const isJson = /json/i.test(a.type ?? '');
  if (a.status === 200 && !isJson) return a.bytes;
  const j = answerJson(a);
  if (a.status === 403 || a.status === 400) throw new LicenceError(j.status === 'revoked' ? 'revoked' : 'invalid');
  if (a.status === 404 && j.status === 'active') throw new LicenceError('file');
  if (a.status === 429 || a.status === 503 || a.status >= 500) throw new LicenceError('unavailable');
  throw new Error(j.error ?? `The store answered ${a.status}.`);
}

/** What the store says of a key now; `unavailable` for any failure (offline, the store or the shop down). */
export async function checkLicenceKey(m: Manifest, key: string, post: PostJson): Promise<LicenceStatus> {
  try {
    const a = await post(checkUrlOf(m), JSON.stringify({ id: m.id, key }));
    if (a.status === 400) return 'invalid';
    if (a.status !== 200) return 'unavailable';
    const s = answerJson(a).status;
    return s === 'active' || s === 'revoked' || s === 'invalid' ? s : 'unavailable';
  } catch { return 'unavailable'; }
}

/** A record after a check: good → active, refunded / disabled / no longer this product's → revoked; nothing concluded → as it was. */
export function afterCheck(rec: LicenceRecord, status: LicenceStatus, now: string): LicenceRecord {
  if (status === 'active') return { ...rec, status: 'active', checkedAt: now };
  if (status === 'revoked' || status === 'invalid') return { ...rec, status: 'revoked', checkedAt: now };
  return rec;
}

/**
 * The installed paid extensions to check again now: those with a key kept, not revoked, whose catalogue entry is newer
 * (an update would be offered) or whose last good check is older than RECHECK_MS. The catalogue's manifest of each.
 */
export function dueForCheck(installed: InstalledExtension[], catalogue: Manifest[], licences: Record<string, LicenceRecord>, now: number): Manifest[] {
  const byId = new Map(catalogue.map((m) => [m.id, m]));
  const out: Manifest[] = [];
  for (const x of installed) {
    const m = byId.get(x.id);
    const rec = licences[x.id];
    if (!m || !isPaid(m) || !rec || rec.status === 'revoked') continue;
    const newer = compareVersions(m.version, x.version) > 0;
    const stale = !(Date.parse(rec.checkedAt) > now - RECHECK_MS);
    if (newer || stale) out.push(m);
  }
  return out;
}

/** Whether an installed paid extension's key was revoked by its seller (it gets no more updates). */
export const isRevoked = (licences: Record<string, LicenceRecord>, id: string): boolean => licences[id]?.status === 'revoked';
