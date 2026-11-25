/**
 * The folder tree and the type list edited in the dialog's settings panel (docs/plan-cde-references.md §3.2): pure functions over
 * a copy of the tree, so the panel keeps its draft and sends the whole of it with `references.setFolders` / `references.setTypes`.
 * The engine refuses what it must (removing a folder that holds documents), and its words are shown as it says them.
 */

import type { Named, RefFolder, RefType } from './references';

/** A folder as the engine takes it: its id, names and children, without the counts the query adds. */
export interface FolderDraft { id: string; name: Record<string, string>; derived?: string; children: FolderDraft[] }

const asNames = (n: Named): Record<string, string> => (typeof n === 'string' ? { en: n } : Object.fromEntries(Object.entries(n).filter((e): e is [string, string] => !!e[1])));

/** The tree from the query, stripped to what is kept. */
export function draftOf(tree: RefFolder[]): FolderDraft[] {
  return tree.map((f) => ({ id: f.id, name: asNames(f.name), ...(f.derived ? { derived: f.derived } : {}), children: draftOf(f.children ?? []) }));
}

const walk = (tree: FolderDraft[], f: (n: FolderDraft, siblings: FolderDraft[], i: number) => boolean | void): boolean => {
  for (let i = 0; i < tree.length; i++) {
    if (f(tree[i], tree, i) === true) return true;
    if (walk(tree[i].children, f)) return true;
  }
  return false;
};

/** A folder id not yet in the tree. */
export function newFolderId(tree: FolderDraft[], seed = Date.now()): string {
  const ids = new Set<string>();
  walk(tree, (n) => { ids.add(n.id); });
  let id = `f-${seed.toString(36)}`;
  while (ids.has(id)) id += 'x';
  return id;
}

/** A folder renamed in `lang` (its other names stay; one with none yet takes the new name too, so no language shows it empty). */
export function renameFolder(tree: FolderDraft[], id: string, lang: string, name: string): FolderDraft[] {
  const copy = structuredClone(tree);
  walk(copy, (n) => {
    if (n.id !== id) return;
    n.name = { ...n.name, [lang]: name };
    for (const l of ['en', 'ro']) if (!n.name[l]) n.name[l] = name;
    return true;
  });
  return copy;
}

/** A folder added under `parent` (the top when null), named the same in both template languages. */
export function addFolder(tree: FolderDraft[], parent: string | null, name: string, id = newFolderId(tree)): FolderDraft[] {
  const copy = structuredClone(tree);
  const fresh: FolderDraft = { id, name: { en: name, ro: name }, children: [] };
  if (parent === null) copy.push(fresh);
  else walk(copy, (n) => { if (n.id === parent) { n.children.push(fresh); return true; } });
  return copy;
}

/** A folder taken out with what it holds (the engine refuses when a document is in it). */
export function removeFolder(tree: FolderDraft[], id: string): FolderDraft[] {
  const copy = structuredClone(tree);
  walk(copy, (_n, siblings, i) => { if (siblings[i].id === id) { siblings.splice(i, 1); return true; } });
  return copy;
}

/** A folder one place up or down among its siblings. */
export function moveFolder(tree: FolderDraft[], id: string, by: -1 | 1): FolderDraft[] {
  const copy = structuredClone(tree);
  walk(copy, (_n, siblings, i) => {
    if (siblings[i].id !== id) return;
    const j = i + by;
    if (j >= 0 && j < siblings.length) [siblings[i], siblings[j]] = [siblings[j], siblings[i]];
    return true;
  });
  return copy;
}

/** Whether two drafts are the same tree (what has changed since it was read). */
export const sameTree = (a: FolderDraft[], b: FolderDraft[]) => JSON.stringify(a) === JSON.stringify(b);

// ---------------------------------------------------------------- the types

/** A type id from a name: letters and digits, hyphens between, not one of the ids taken. */
export function typeIdOf(name: string, taken: string[]): string {
  const base = name.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'type';
  let id = base;
  for (let n = 2; taken.includes(id); n++) id = `${base}-${n}`;
  return id;
}

/** A type's name set in `lang` (the others stay; a missing English one takes it too). */
export function renameType(types: RefType[], id: string, lang: string, name: string): RefType[] {
  return types.map((t) => {
    if (t.id !== id) return t;
    const names = { ...asNames(t.name), [lang]: name };
    for (const l of ['en', 'ro']) if (!names[l]) names[l] = name;
    return { ...t, name: names };
  });
}

/** A type's extra field added (label in `lang`), its key from the label. */
export function addTypeField(types: RefType[], id: string, label: string, lang: string): RefType[] {
  return types.map((t) => {
    if (t.id !== id) return t;
    const key = typeIdOf(label, t.fields.map((f) => f.key)).replace(/-(\w)/g, (_m, c: string) => c.toUpperCase());
    return { ...t, fields: [...t.fields, { key, label: { en: label, ro: label, [lang]: label }, kind: 'text' }] };
  });
}
