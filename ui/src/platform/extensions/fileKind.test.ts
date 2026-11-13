import { describe, expect, it } from 'vitest';
import { fileKindInstaller } from './fileKind';
import { readRulePack } from './packs';
import { furniturePack } from './fixtures';

const bytes = (o: unknown) => new TextEncoder().encode(JSON.stringify(o));

describe('a one-file library kind', () => {
  const kept = new Map<string, string>();
  const brought: string[] = [];
  const inst = fileKindInstaller({
    kind: 'rules', ext: 'lapinrules', read: (b) => { const r = readRulePack(b); return { text: r.text, names: [r.name] }; },
    files: { keep: async (ext, name, text) => { kept.set(`${ext}:${name}`, text); }, forget: async (ext, name) => { kept.delete(`${ext}:${name}`); }, get: async (ext, name) => kept.get(`${ext}:${name}`) ?? null },
    bring: async (text, file) => { brought.push(file); return text; },
  });
  const m = furniturePack({ kind: 'rules', name: 'RO rules', id: 'x.ro-rules', contents: { file: 'ro.lapinrules' }, indicative: true });
  const pack = { format: 'lapin-rules', version: 1, name: 'RO', indicative: true, stairs: { a: 1 } };

  it('keeps the checked file under the extension\'s name, brings it on request, forgets it on uninstall', async () => {
    const receipt = await inst.install({ name: 'ro.lapinrules', bytes: bytes(pack) }, m, { confirm: () => true });
    expect(receipt).toMatchObject({ file: 'RO rules', names: ['RO'] });
    expect([...kept.keys()]).toEqual(['lapinrules:RO rules']);
    inst.open!.run({ receipt } as never);
    await new Promise((r) => setTimeout(r, 0));
    expect(brought).toEqual(['RO rules.lapinrules']);
    await inst.uninstall(receipt, {} as never);
    expect(kept.size).toBe(0);
  });

  it('refuses a file that is not its kind and keeps nothing', async () => {
    await expect(inst.install({ name: 'x', bytes: bytes({ format: 'lapin-symbols', version: 1 }) }, m, { confirm: () => true })).rejects.toThrow('The file is not a Lapin rule pack.');
    expect(kept.size).toBe(0);
  });
});
