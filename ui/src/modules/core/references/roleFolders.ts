/**
 * Group by folder in the CDE web's Containers tab (docs/plan-cde-references.md §5): an issued item's role is read from its ISO 19650
 * name (Project-Originator-Volume-Level-Type-Role-Number, the project's separator) and mapped through the folders' `roleFolders`
 * to a folder's name; a role no folder takes goes under "Other". No imports: the Containers tab loads this with the page.
 */

export interface FolderNode { id: string; name?: string | Record<string, string | undefined>; text?: string; children?: FolderNode[] }

/** The role code of an ISO 19650 name: its sixth field (Project, Originator, Volume, Level, Type, Role, Number), upper case; null when it has fewer. */
export function roleOfName(name: string, separator = '-'): string | null {
  const base = name.replace(/\.[A-Za-z0-9]{1,5}$/, '').replace(/[_ ]P\d{2,3}$|[_ ]C\d{2,3}$/, '');
  const parts = base.split(separator);
  if (parts.length < 6) return null;
  const role = parts[5].trim().toUpperCase();
  return /^[A-Z]{1,2}$/.test(role) ? role : null;
}

/** The first role found among an item's names (its files', then its own), null when none has one. */
export function roleOfItem(names: string[], separator = '-'): string | null {
  for (const n of names) { const r = roleOfName(n, separator); if (r) return r; }
  return null;
}

/** Every folder's id with its name in the language, the derived ones too (an issued folder is where roles usually go). */
export function folderNames(tree: FolderNode[], lang: string): Map<string, string> {
  const out = new Map<string, string>();
  const walk = (nodes: FolderNode[]) => {
    for (const n of nodes) {
      const name = typeof n.name === 'string' ? n.name : n.name?.[lang] ?? n.name?.en ?? n.name?.ro ?? Object.values(n.name ?? {}).find(Boolean);
      out.set(n.id, n.text ?? name ?? n.id);
      walk(n.children ?? []);
    }
  };
  walk(tree);
  return out;
}

export interface Group<T> { folder: string | null; name: string | null; rows: T[] }

/**
 * The rows in groups by the folder their role maps to, in the order the folders first appear in the rows, "Other" (name null) last.
 * `namesOf` gives the names an item's role is read from.
 */
export function groupByFolder<T>(rows: T[], namesOf: (r: T) => string[], roleFolders: Record<string, string>, names: Map<string, string>, separator = '-'): Group<T>[] {
  const groups = new Map<string, Group<T>>();
  const other: Group<T> = { folder: null, name: null, rows: [] };
  for (const r of rows) {
    const role = roleOfItem(namesOf(r), separator);
    const id = role ? roleFolders[role] : undefined;
    if (!id || !names.has(id)) { other.rows.push(r); continue; }
    const g = groups.get(id) ?? { folder: id, name: names.get(id)!, rows: [] };
    g.rows.push(r);
    groups.set(id, g);
  }
  return [...groups.values(), ...(other.rows.length ? [other] : [])];
}
