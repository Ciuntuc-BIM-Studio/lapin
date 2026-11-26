/**
 * The reference documents' tables a document shows (docs/plan-cde-references.md §6, stage R5): `references:permits` (the urbanism
 * certificate's checklist with the documents answering it) and `references:documents`, whose filter follows a `?` in its source —
 * `type` (one or more ids, by commas), `folder` (with its subfolders) and `status` (`received`, `requested`, `expired`, or `all`;
 * none: the live ones). The engine reads the source as it is; this is what the editor's settings make of it.
 */

export const REFERENCE_PERMITS = 'references:permits';
export const REFERENCE_LIST = 'references:documents';

export interface ReferenceFilter { type: string; folder: string; status: string }
export const NO_REFERENCE_FILTER: ReferenceFilter = { type: '', folder: '', status: '' };

/** A table's source without its filter: what the editor's list of tables names. */
export const tableBase = (source: string) => {
  const at = source.indexOf('?');
  return at < 0 ? source : source.slice(0, at);
};

/** The filter a `references:documents` source carries. */
export function readReferenceFilter(source: string): ReferenceFilter {
  const at = source.indexOf('?');
  const out = { ...NO_REFERENCE_FILTER };
  if (at < 0) return out;
  for (const pair of source.slice(at + 1).split('&')) {
    const eq = pair.indexOf('=');
    if (eq <= 0) continue;
    const key = decodeURIComponent(pair.slice(0, eq)).trim();
    const value = decodeURIComponent(pair.slice(eq + 1)).trim();
    if (key === 'type' || key === 'folder' || key === 'status') out[key] = value;
  }
  return out;
}

/** The source for a filter: the base alone when it filters nothing, else its non-empty keys in a fixed order. */
export function withReferenceFilter(base: string, f: ReferenceFilter): string {
  const parts = (['type', 'folder', 'status'] as const).filter((k) => f[k].trim()).map((k) => `${k}=${encodeURIComponent(f[k].trim()).replace(/%2C/gi, ',')}`);
  return parts.length ? `${base}?${parts.join('&')}` : base;
}
