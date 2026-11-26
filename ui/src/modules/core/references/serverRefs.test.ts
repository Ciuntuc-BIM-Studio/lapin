import { describe, expect, it, vi } from 'vitest';
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

describe('the server adapter\'s calls', () => {
  it('asks the server for suggestions, says when it has none to give, and puts the checklist offered with the types', async () => {
    const calls: { path: string; init?: RequestInit }[] = [];
    let answer: Response = new Response('{}');
    vi.doMock('../../../engine/cdeServer', () => ({ api: async (path: string, init?: RequestInit) => { calls.push({ path, init }); return answer.clone(); } }));
    vi.resetModules();
    const { serverAdapter } = await import('./serverRefs');
    const a = serverAdapter('p1', 'en');
    const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });

    answer = json({ number: '123', date: '2026-03-12', evidence: { number: 'Nr. 123 din 12.03.2026' } });
    expect(await a.suggest!('2fQ0vD7r1Eb9Kc3xY8mN$w', 'cu 1.pdf')).toMatchObject({ number: '123', date: '2026-03-12' });
    expect(calls.at(-1)!.path).toBe('/atrium/1.0/projects/p1/references/2fQ0vD7r1Eb9Kc3xY8mN%24w/suggest?name=cu%201.pdf');

    answer = new Response('', { status: 404 });
    expect((await a.suggest!('x'))?.warning).toMatch(/newer Atrium Server/);

    answer = json({ types: [], checklist: [] });
    await a.setChecklist([{ key: 'water', label: { en: 'Water supply' } }]);
    expect(calls.at(-1)!.path).toBe('/atrium/1.0/projects/p1/references/types');
    expect(calls.at(-1)!.init?.method).toBe('PUT');
    expect(JSON.parse(String(calls.at(-1)!.init?.body))).toEqual({ checklist: [{ key: 'water', label: { en: 'Water supply' } }] });
    const before = calls.length;
    expect(await a.setChecklist(null)).toBeNull();
    expect(calls.length).toBe(before);
    vi.doUnmock('../../../engine/cdeServer');
  });
});
