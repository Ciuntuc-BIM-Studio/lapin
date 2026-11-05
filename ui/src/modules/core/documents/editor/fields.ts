/**
 * What a document can read (`document.fields`), for the editor: the fields with their values now, the collections a repeat goes
 * over with their items' fields, the project's tables; and the search Insert → Field and the pickers use: the fields of the
 * project grouped by their key's first part, then the fields of each repeat the cursor stands in (`room.name`), with the first
 * item's values; a query matches a label, a key or a value, letters with or without their diacritics, every word somewhere.
 */

export interface DocFieldInfo { key: string; label: string; value?: string | null }
export interface DocCollectionInfo { key: string; label: string; itemPrefix: string; fields: string[]; count: number }
export interface DocTableInfo { source: string; label: string }
export interface DocFieldsInfo { fields: DocFieldInfo[]; collections: DocCollectionInfo[]; tables: DocTableInfo[] }

/** A repeat around the cursor: its collection and the prefix its items are read by. */
export interface ScopeInfo { collection: string; prefix: string }

export interface FieldGroup { id: string; title: string; item: boolean; fields: DocFieldInfo[] }

/** Lower case without diacritics (ș → s, ă → a). */
export const fold = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

/** The names the project's groups of fields are shown under, by their key's first part. */
const GROUP_TITLES: Record<string, string> = {
  project: 'Project', document: 'Document', site: 'Site', date: 'Date', cde: 'Issue', areas: 'Areas', counts: 'Counts', height: 'Heights', levels: 'Storeys', param: 'Parameters',
};

/** A field's label, else its key's last part. */
export function labelOf(info: DocFieldsInfo | null, key: string, scopes: ScopeInfo[] = []): string {
  const f = info?.fields.find((x) => x.key === key);
  if (f) return f.label;
  for (const s of scopes) if (key.startsWith(`${s.prefix}.`)) return key;
  return key;
}

/**
 * The fields a document may use where the cursor is, grouped: those of each repeat it stands in first (the innermost first,
 * their values from `scopeValues`), then the project's by group, each filtered by the query.
 */
export function searchFields(info: DocFieldsInfo | null, scopes: ScopeInfo[], query: string, scopeValues: Record<string, string> = {}): FieldGroup[] {
  const words = fold(query).split(/\s+/).filter(Boolean);
  const hit = (f: DocFieldInfo) => {
    const hay = fold(`${f.label} ${f.key} ${f.value ?? ''}`);
    return words.every((w) => hay.includes(w));
  };
  const out: FieldGroup[] = [];
  for (const s of [...scopes].reverse()) {
    const c = info?.collections.find((x) => x.key === s.collection);
    if (!c) continue;
    const keys = ['index', ...c.fields.filter((k) => k !== 'index')];
    const fields = keys.map((k) => ({ key: `${s.prefix}.${k}`, label: `${s.prefix}.${k}`, value: scopeValues[`${s.prefix}.${k}`] ?? null })).filter(hit);
    if (fields.length) out.push({ id: `item:${s.prefix}`, title: c.label, item: true, fields });
  }
  const groups = new Map<string, DocFieldInfo[]>();
  for (const f of info?.fields ?? []) {
    if (!hit(f)) continue;
    const g = f.key.split('.')[0];
    if (!groups.has(g)) groups.set(g, []);
    groups.get(g)!.push(f);
  }
  for (const [g, fields] of groups) out.push({ id: g, title: GROUP_TITLES[g] ?? g, item: false, fields });
  return out;
}
