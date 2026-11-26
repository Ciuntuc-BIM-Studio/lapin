import { describe, expect, it } from 'vitest';
import type { RefDoc, RefFolder } from '../references/references';
import { chosenRefs, folderState, inFolder, notHeld, toggleFolder } from './issueReferences';

const folder = (id: string, children: RefFolder[] = []): RefFolder => ({ id, name: id, text: id, count: 0, total: 0, children });
const tree = [folder('input', [folder('input-cu')]), folder('permits', [folder('permits-utilities'), folder('permits-other')])];
const file = (name: string, held = true) => ({ name, sha256: name.padEnd(64, '0'), size: 10, held });
const doc = (id: string, folderId: string, status = 'received', files = [file(`${id}.pdf`)]): RefDoc => ({ id, title: id, folder: folderId, status, expired: status === 'expired', files });

const docs = [
  doc('cu', 'input-cu'),
  doc('water', 'permits-utilities', 'received', [file('aviz.pdf'), file('plan.png', false)]),
  doc('fire', 'permits-other', 'requested', []),
  doc('old', 'permits-other', 'superseded'),
  doc('gone', 'permits-other', 'withdrawn'),
  doc('late', 'permits-other', 'expired'),
];

describe('the reference documents an issue sends', () => {
  it('takes a folder with its subfolders: the live documents with a file', () => {
    expect(inFolder(docs, tree, 'permits').map((d) => d.id)).toEqual(['water', 'late']);
    expect(inFolder(docs, tree, 'input').map((d) => d.id)).toEqual(['cu']);
  });

  it('ticks and unticks a folder, and shows it on, off or mixed', () => {
    let chosen = toggleFolder(new Set(), docs, tree, 'permits', true);
    expect([...chosen]).toEqual(['water', 'late']);
    expect(folderState(chosen, docs, tree, 'permits')).toBe('on');
    expect(folderState(chosen, docs, tree, 'input')).toBe('off');
    chosen.delete('late');
    expect(folderState(chosen, docs, tree, 'permits')).toBe('mixed');
    chosen = toggleFolder(chosen, docs, tree, 'permits-utilities', false);
    expect([...chosen]).toEqual([]);
  });

  it('sends what was picked one by one too (a superseded one when named), never a document without a file', () => {
    expect(chosenRefs(docs, new Set(['old', 'fire', 'cu'])).map((d) => d.id)).toEqual(['cu', 'old']);
  });

  it('lists the files not held here, to fetch from the server', () => {
    expect(notHeld(chosenRefs(docs, new Set(['cu', 'water'])))).toEqual([{ id: 'water', name: 'plan.png', sha256: 'plan.png'.padEnd(64, '0') }]);
  });
});
