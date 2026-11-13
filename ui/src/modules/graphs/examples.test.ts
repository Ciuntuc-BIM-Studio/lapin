import { describe, expect, it } from 'vitest';
import { scriptIcon } from '../../shell/toolIcons';
import { EXAMPLES, exampleFiles, loadExample, uniqueName } from './examples';

describe('the example graphs', () => {
  it('lists exactly the files bundled', () => {
    expect(EXAMPLES.map((e) => e.id).sort()).toEqual(exampleFiles());
  });

  it('loads each as a graph of the current format with a tool named as listed, a copy each time', async () => {
    for (const e of EXAMPLES) {
      const g = await loadExample(e.id);
      expect(g.format).toBe(2);
      expect(g.nodes.length).toBeGreaterThan(0);
      expect(g.tool?.name).toBe(e.name);
      const ids = g.nodes.map((n) => n.id);
      expect(new Set(ids).size).toBe(ids.length);
      for (const w of g.wires) { expect(ids).toContain(w.from); expect(ids).toContain(w.to); }
    }
    const a = await loadExample('twisted-tower');
    a.nodes.length = 0;
    expect((await loadExample('twisted-tower')).nodes.length).toBeGreaterThan(0);
  });

  it('gives every example, and its own tool, an icon that is a drawing and not a letter', async () => {
    const letter = (name: string, label: string) => String((scriptIcon(name, label) as { displayName?: string }).displayName).startsWith('Letter(');
    for (const e of EXAMPLES) {
      expect(letter(e.icon, e.name), e.id).toBe(false);
      expect(letter(String((await loadExample(e.id)).tool?.icon), e.name), `${e.id} tool`).toBe(false);
    }
    // A name no icon has still falls back to a letter.
    expect(letter('no-such-icon-name', 'Tower')).toBe(true);
  });

  it('refuses an example that is not there', async () => {
    await expect(loadExample('nope')).rejects.toThrow(/no example/);
  });
});

describe('uniqueName', () => {
  it('keeps a free name and numbers a taken one, without regard to case', () => {
    expect(uniqueName('Twisted tower', ['Graph 1'])).toBe('Twisted tower');
    expect(uniqueName('Twisted tower', ['twisted tower'])).toBe('Twisted tower 2');
    expect(uniqueName('Twisted tower', ['Twisted tower', 'Twisted tower 2'])).toBe('Twisted tower 3');
  });
});
