import { describe, expect, it } from 'vitest';
import { base64OfBytes, bytesOfBase64, mergeFiles, normalizeDoc, normalizeTree, serverPerms, sha256Hex } from './serverRefs';
import type { RefDoc, RefFile } from './references';

const file = (name: string, sha = 'a'): RefFile => ({ name, sha256: sha, size: 3, mime: 'application/pdf', held: true });
const doc = (over: Partial<RefDoc> = {}): RefDoc => ({ id: 'x', title: 'x', status: 'received', expired: false, files: [], ...over });

describe('the server adapter\'s pure parts', () => {
  it('hashes bytes as the object store names them', async () => {
    expect(await sha256Hex(new TextEncoder().encode('abc'))).toBe('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
  });

  it('round-trips base64', () => {
    const bytes = Uint8Array.from({ length: 70_000 }, (_, i) => i % 251);
    expect([...bytesOfBase64(base64OfBytes(bytes))]).toEqual([...bytes]);
  });

  it('gives viewers the reading, editors their own documents, owners everything', () => {
    const mine = doc({ addedBy: 'atrium:me' }), theirs = doc({ addedBy: 'atrium:other' });
    const viewer = serverPerms('viewer', 'atrium:me'), editor = serverPerms('editor', 'atrium:me'), owner = serverPerms('owner', 'atrium:me');
    expect([viewer.add, viewer.canChange(mine), viewer.remove, viewer.admin]).toEqual([false, false, false, false]);
    expect([editor.add, editor.canChange(mine), editor.canChange(theirs), editor.remove, editor.admin]).toEqual([true, true, false, false, false]);
    expect([owner.add, owner.canChange(theirs), owner.remove, owner.admin]).toEqual([true, true, true, true]);
    expect(serverPerms('editor', null).canChange(mine)).toBe(false);
  });

  it('adds files by name (replacing the same name, ignoring case) and takes one off', () => {
    const current = [file('a.pdf', '1'), file('b.pdf', '2')];
    const added = mergeFiles(current, [{ name: 'A.PDF', sha256: '3', size: 9 }]);
    expect(added.map((f) => `${f.name}:${f.sha256}`)).toEqual(['b.pdf:2', 'A.PDF:3']);
    expect(mergeFiles(current, [], 'B.pdf').map((f) => f.name)).toEqual(['a.pdf']);
    expect(mergeFiles(current, [])[0]).toEqual({ name: 'a.pdf', sha256: '1', size: 3, mime: 'application/pdf' });
  });

  it('shows a server document\'s files as held and a derived folder\'s null count as none', () => {
    expect(normalizeDoc(doc({ files: [{ name: 'a', sha256: 's', size: 1 } as RefFile] })).files[0].held).toBe(true);
    const t = normalizeTree({ folders: [{ id: 'issued', name: 'Issued', text: 'Issued', count: null as unknown as number, total: null as unknown as number, derived: 'sheets', children: [] }], roleFolders: { A: 'issued' } });
    expect(t.folders[0].count).toBe(0);
    expect(t.roleFolders.A).toBe('issued');
    expect(t.keepLimit).toBe(0);
  });
});
