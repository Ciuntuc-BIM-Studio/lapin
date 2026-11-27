/**
 * The catalogue's revocation list (docs/plan-extension-modules.md §4.3; lapin-extensions: tools/revoked.mjs): extension versions
 * withdrawn after they were published, by id and version (or "*", every version) or by the package's SHA-256, each with its
 * reason. Published beside the index as `revoked.json` with `revoked.json.sig`, signed by the store key like the index.
 *
 * The interface reads it with the catalogue and checks it with the catalogue's trust (signature.ts, the keys of keys.ts):
 * only a list signed by a store key Lapin trusts counts. It keeps the newest it verified (by `generated`; an older one is never
 * taken) in this computer's settings, checked again when read, and on the desktop hands it to the engine (`extensions.revoked`),
 * which verifies it again itself and refuses a withdrawn extension module at its next start. Offline, the kept copies are used.
 * Pure but for `loadRevoked`'s callers (runtime.ts).
 */

import { catalogueTrust, type StoreKey } from './signature';

export const REVOKED_FORMAT = 'lapin-extensions-revoked';

export interface Revocation { id: string; versions?: string[]; sha256?: string[]; reason: string; date: string }
export interface RevokedList { generated: string; revoked: Revocation[] }

const ID = /^[a-z0-9]+(?:[.-][a-z0-9]+)*$/;
const SEMVER = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$/;
const SHA256 = /^[0-9a-f]{64}$/;
const DATE = /^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$/;

const strings = (v: unknown, ok: (s: string) => boolean): string[] | null | undefined =>
  v === undefined ? undefined : Array.isArray(v) && v.length && v.every((s) => typeof s === 'string' && ok(s)) ? (v as string[]) : null;

/** A list's bytes read as tools/revoked.mjs checks it, `generated` required; null when it is not one. */
export function readRevokedList(bytes: Uint8Array): RevokedList | null {
  let o: Record<string, unknown>;
  try { o = JSON.parse(new TextDecoder().decode(bytes)) as Record<string, unknown>; } catch { return null; }
  if (!o || typeof o !== 'object' || Array.isArray(o) || o.format !== REVOKED_FORMAT || o.version !== 1) return null;
  if (typeof o.generated !== 'string' || Number.isNaN(Date.parse(o.generated)) || !Array.isArray(o.revoked)) return null;
  const revoked: Revocation[] = [];
  for (const r of o.revoked as Record<string, unknown>[]) {
    if (!r || typeof r !== 'object' || typeof r.id !== 'string' || !ID.test(r.id)) return null;
    const versions = strings(r.versions, (v) => v === '*' || SEMVER.test(v));
    const sha256 = strings(r.sha256, (h) => SHA256.test(h));
    if (versions === null || sha256 === null || (!versions && !sha256)) return null;
    if (typeof r.reason !== 'string' || !r.reason.trim() || r.reason.length > 500 || typeof r.date !== 'string' || !DATE.test(r.date)) return null;
    revoked.push({ id: r.id, ...(versions ? { versions } : {}), ...(sha256 ? { sha256 } : {}), reason: r.reason.trim(), date: r.date });
  }
  return { generated: o.generated, revoked };
}

/** A list checked against its signature file by a store key, then read; null when either does not hold (unsigned counts as not holding). */
export async function verifyRevoked(keys: readonly StoreKey[], bytes: Uint8Array, sigFile: Uint8Array | null): Promise<RevokedList | null> {
  if ((await catalogueTrust(keys, bytes, sigFile)) !== 'signed') return null;
  return readRevokedList(bytes);
}

/** Whether `next` is newer than `kept` (by `generated`): only a newer list replaces the one kept. */
export const isNewer = (next: RevokedList, kept: RevokedList | null): boolean => !kept || Date.parse(next.generated) > Date.parse(kept.generated);

/** The entry withdrawing this version or package, or null: its id, and a version it names (or "*"), or its package's SHA-256. */
export function revocationOf(list: RevokedList | null | undefined, m: { id: string; version: string; package?: { sha256?: string } }): Revocation | null {
  const sha = m.package?.sha256;
  return list?.revoked.find((r) => r.id === m.id && ((r.versions ?? []).some((v) => v === '*' || v === m.version) || (!!sha && (r.sha256 ?? []).includes(sha)))) ?? null;
}

/** Where the list is published: beside the catalogue's index (its folder's revoked.json). */
export const revokedAddress = (index: string): string => (/[/\\]/.test(index) ? index.replace(/[^/\\]*$/, 'revoked.json') : 'revoked.json');

/** The sentence an install that is refused shows. */
export const revokedProblem = (name: string, version: string, r: Revocation): string =>
  `„${name}” ${version} is not installed: Lapin Extensions withdrew this version on ${r.date}: ${r.reason}`;
