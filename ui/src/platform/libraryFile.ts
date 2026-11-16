/**
 * The library files' one envelope and one import preview, as the interface sees them (audit D13; the engine's LibraryFile and
 * LibraryMerge). Every library file Lapin writes (.lapinsymbols, .lapintypes, .lapinstandards, .lapinrules, .lapinparams,
 * .lapinaliases, title blocks, .lapinbarshapes, .lapinpart, .lapinfamily, .lapinschedule, .lapinlib, .lapinprofiles,
 * .lapinprices, .lapinmapping) keeps its `format` and `version` and carries the header `lapin: {envelope, app, created, source}`;
 * one written before the envelope (no header) is envelope 0 and reads as it always did. An import shows, item by item, what it
 * will do: new, the same, the same name (keep both, „Name (2)”, by default) or the same thing by its id (updated by default),
 * with the same three choices everywhere: keep both, replace (update in place), skip.
 */

/** The envelope's own version: 0 none, 1 the header. */
export const ENVELOPE = 1;
export const HEADER_KEY = 'lapin';

export interface LibraryHeader { format: string; version: number; envelope: number; app: string | null; created: string | null; source: string | null }

const str = (v: unknown): string | null => (typeof v === 'string' && v.trim() ? v.trim() : null);

/** A file's header: envelope 0 when it has none; a version not given is 1. */
export function headerOf(o: Record<string, unknown>): LibraryHeader {
  const format = typeof o.format === 'string' ? o.format : '';
  const version = typeof o.version === 'number' && Number.isFinite(o.version) ? Math.floor(o.version) : 1;
  const h = o[HEADER_KEY];
  if (!h || typeof h !== 'object' || Array.isArray(h)) return { format, version, envelope: 0, app: null, created: null, source: null };
  const r = h as Record<string, unknown>;
  const envelope = typeof r.envelope === 'number' && Number.isFinite(r.envelope) ? Math.floor(r.envelope) : 1;
  return { format, version, envelope, app: str(r.app), created: str(r.created), source: str(r.source) };
}

/** Refused, with a sentence, when the file's envelope is a newer Lapin's (its payload's version each reader checks). */
export function checkEnvelope(o: Record<string, unknown>, what: string): void {
  const h = headerOf(o);
  if (h.envelope > ENVELOPE) throw new Error(`This ${what} was written by a newer Lapin${h.app ? ` (${h.app})` : ''}: update Lapin to read it.`);
}

/** The file's object without its header (what a library keeps). */
export function withoutHeader<T extends Record<string, unknown>>(o: T): T {
  const rest: Record<string, unknown> = { ...o };
  delete rest[HEADER_KEY];
  return rest as T;
}

let app: string | null = null;
/** The app version files are written by (the engine's, set when the session starts): kept here so this module stays pure. */
export const setLibraryApp = (version: string | null): void => { app = version; };

/** A payload as a file: its format and version first, then the header (this app, now, from `source`), then the payload's own keys. */
export function withEnvelope(format: string, version: number, payload: Record<string, unknown>, source?: string | null, now: Date = new Date()): Record<string, unknown> {
  const out: Record<string, unknown> = {
    format, version,
    [HEADER_KEY]: { envelope: ENVELOPE, app, created: now.toISOString().replace(/\.\d{3}Z$/, 'Z'), source: source?.trim() || 'Lapin' },
  };
  for (const [k, v] of Object.entries(payload)) if (k !== 'format' && k !== 'version' && k !== HEADER_KEY) out[k] = v;
  return out;
}

// ------------------------------------------------------------ the import preview

export type RowStatus = 'new' | 'same' | 'name' | 'id';
export type Choice = 'both' | 'update' | 'skip' | 'keep';

/** One item of a file as the preview shows it (the engine's LibraryMerge.Row). */
export interface LibraryRow { key: string; kind: string; name: string; status: RowStatus; differs: string[]; choices: Choice[]; choice: Choice | null }

export interface LibraryPreview {
  format: string; extension: string; what: string; header: LibraryHeader | null;
  rows: LibraryRow[]; new: number; same: number; conflicts: number;
}

/** What an import did, counted the same way by every kind. */
export interface LibraryTally { added?: number; updated?: number; kept?: number; skipped?: number; renamed?: string[] }

export const isConflict = (r: LibraryRow): boolean => r.status === 'name' || r.status === 'id';

/** Every conflict at its default. */
export const defaultChoices = (rows: LibraryRow[]): Record<string, Choice> =>
  Object.fromEntries(rows.filter(isConflict).map((r) => [r.key, r.choice ?? (r.status === 'id' ? 'update' : 'both')]));

/** The same choice for every conflict that offers it (the others keep theirs). */
export const chooseAll = (rows: LibraryRow[], value: Record<string, Choice>, c: Choice): Record<string, Choice> =>
  ({ ...value, ...Object.fromEntries(rows.filter((r) => isConflict(r) && r.choices.includes(c)).map((r) => [r.key, c])) });

/** The words of a choice; a skip that keeps the project's own (a material's) says so. */
export const CHOICE_LABELS: Record<Choice, string> = { both: 'Keep both', update: 'Replace', skip: 'Skip', keep: "Keep the project's" };

export const STATUS_LABELS: Record<RowStatus, string> = { new: 'New', same: 'The same', name: 'Same name', id: 'Same item, changed' };

/** The counts of a preview, said as one phrase's values. */
export const previewCounts = (p: Pick<LibraryPreview, 'rows'>) => ({
  added: p.rows.filter((r) => r.status === 'new').length,
  same: p.rows.filter((r) => r.status === 'same').length,
  choose: p.rows.filter(isConflict).length,
});

/** The rows of a library kept by the interface (a price list, a profile library), each by its name: new, the same, or the same name (keep both, replace, skip). */
export function rowsByName<T>(incoming: { name: string; item: T }[], have: (name: string) => T | undefined, same: (a: T, b: T) => boolean, kind: string): LibraryRow[] {
  return incoming.map(({ name, item }) => {
    const mine = have(name);
    const status: RowStatus = mine === undefined ? 'new' : same(mine, item) ? 'same' : 'name';
    return { key: name, kind, name, status, differs: [], choices: status === 'name' ? ['both', 'update', 'skip'] : [], choice: status === 'name' ? 'both' : null };
  });
}

/** „Name (2)”, „Name (3)”…: the first `taken` refuses not (an import's copy kept beside, as the engine names it). */
export function freeName(taken: (n: string) => boolean, name: string): string {
  let n = 2;
  while (taken(`${name} (${n})`)) n++;
  return `${name} (${n})`;
}
