import { describe, expect, it } from 'vitest';
import { addFolder, addOption, addTypeField, draftOf, moveFolder, moveOption, newFolderId, plainOptions, removeFolder, renameFolder, renameOption, renameType, sameTree, typeIdOf } from './folderEdit';
import type { RefFolder, RefType } from './references';

const node = (id: string, en: string, children: RefFolder[] = [], derived?: string): RefFolder => ({ id, name: { ro: `r-${en}`, en }, text: en, count: 3, total: 5, children, ...(derived ? { derived } : {}) });
const tree = [node('a', 'A', [node('a1', 'A1'), node('a2', 'A2')]), node('b', 'B'), node('d', 'D', [], 'sheets')];

describe('the folder tree draft', () => {
  it('keeps ids, names, children and derived marks, and leaves the counts out', () => {
    const d = draftOf(tree);
    expect(d[0]).toEqual({ id: 'a', name: { ro: 'r-A', en: 'A' }, children: [{ id: 'a1', name: { ro: 'r-A1', en: 'A1' }, children: [] }, { id: 'a2', name: { ro: 'r-A2', en: 'A2' }, children: [] }] });
    expect(d[2].derived).toBe('sheets');
    expect(JSON.stringify(d)).not.toContain('count');
  });

  it('renames in a language and keeps the folder id', () => {
    const d = renameFolder(draftOf(tree), 'a1', 'ro', 'Nou');
    expect(d[0].children[0]).toEqual({ id: 'a1', name: { ro: 'Nou', en: 'A1' }, children: [] });
  });

  it('adds a folder at the top or under another, with a new id', () => {
    const d = draftOf(tree);
    const top = addFolder(d, null, 'Z', 'z');
    expect(top.map((f) => f.id)).toEqual(['a', 'b', 'd', 'z']);
    const under = addFolder(d, 'b', 'B1', 'b1');
    expect(under[1].children[0]).toEqual({ id: 'b1', name: { en: 'B1', ro: 'B1' }, children: [] });
    expect(d[1].children).toEqual([]);
    expect(newFolderId(d, 1)).toMatch(/^f-/);
  });

  it('removes a folder with its children, and moves one among its siblings', () => {
    const d = draftOf(tree);
    expect(removeFolder(d, 'a')[0].id).toBe('b');
    expect(removeFolder(d, 'a2')[0].children.map((c) => c.id)).toEqual(['a1']);
    expect(moveFolder(d, 'a2', -1)[0].children.map((c) => c.id)).toEqual(['a2', 'a1']);
    expect(moveFolder(d, 'a1', -1)[0].children.map((c) => c.id)).toEqual(['a1', 'a2']);
    expect(sameTree(d, draftOf(tree))).toBe(true);
    expect(sameTree(d, moveFolder(d, 'b', -1))).toBe(false);
  });
});

describe('the type list', () => {
  const types: RefType[] = [{ id: 'cu', name: { ro: 'Certificat', en: 'Certificate' }, fields: [] }];
  it('makes ids from names', () => {
    expect(typeIdOf('Aviz de mediu', [])).toBe('aviz-de-mediu');
    expect(typeIdOf('Ștefan', ['stefan'])).toBe('stefan-2');
    expect(typeIdOf('???', [])).toBe('type');
  });
  it('renames and adds fields', () => {
    expect(renameType(types, 'cu', 'ro', 'CU')[0].name).toEqual({ ro: 'CU', en: 'Certificate' });
    const t = addTypeField(types, 'cu', 'Cadastral number', 'en')[0];
    expect(t.fields).toEqual([{ key: 'cadastralNumber', label: { en: 'Cadastral number', ro: 'Cadastral number' }, kind: 'text' }]);
  });
});

describe('the checklist offered', () => {
  const items = [{ key: 'water', group: 'utilities', label: { ro: 'Apă', en: 'Water supply' }, text: 'Water supply', folder: 'permits' }, { key: 'gas', label: { ro: 'Gaz', en: 'Gas' } }];

  it('adds an item with a key of its own from the label, in every language it lacks', () => {
    const out = addOption(addOption(items, 'Aviz drumuri', 'ro'), 'Aviz drumuri', 'ro');
    expect(out.map((o) => o.key)).toEqual(['water', 'gas', 'aviz-drumuri', 'aviz-drumuri-2']);
    expect(out[2].label).toEqual({ en: 'Aviz drumuri', ro: 'Aviz drumuri' });
  });

  it('renames in a language and keeps the key the certificates name', () => {
    const out = renameOption(items, 'water', 'ro', 'Alimentare cu apă');
    expect(out[0]).toMatchObject({ key: 'water', label: { ro: 'Alimentare cu apă', en: 'Water supply' }, folder: 'permits' });
  });

  it('moves an item and stops at the ends', () => {
    expect(moveOption(items, 'gas', -1).map((o) => o.key)).toEqual(['gas', 'water']);
    expect(moveOption(items, 'gas', 1)).toBe(items);
  });

  it('sends back the key, label, group and folder only', () => {
    expect(plainOptions(items)).toEqual([{ key: 'water', group: 'utilities', label: { ro: 'Apă', en: 'Water supply' }, folder: 'permits' }, { key: 'gas', label: { ro: 'Gaz', en: 'Gas' } }]);
  });
});
