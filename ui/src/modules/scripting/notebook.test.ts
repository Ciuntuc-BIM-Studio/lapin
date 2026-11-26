import { describe, expect, it } from 'vitest';
import {
  codeContextAbove,
  combineCodeCells,
  createCell,
  hasCellMarkers,
  insertCellAt,
  moveCell,
  parseNotebook,
  removeCell,
  serializeNotebook,
  targetCodeCellIndex,
} from './notebook';

describe('notebook percent-format parser & serializer', () => {
  it('opens a plain .py script as a single code cell and serializes it back unchanged', () => {
    const plain = 'x = 5\nprint(x)\n';
    expect(hasCellMarkers(plain)).toBe(false);
    const cells = parseNotebook(plain);
    expect(cells).toHaveLength(1);
    expect(cells[0].kind).toBe('code');
    expect(cells[0].source).toBe(plain);
    expect(serializeNotebook(cells)).toBe(plain);
  });

  it('round-trips markdown, prompt, and code cells cleanly', () => {
    const src = [
      '# %% [markdown]',
      '# ## Walls',
      '# Draws a perimeter.',
      '',
      '# %% [prompt]',
      '# Create 4 walls of height 3m',
      '',
      '# %%',
      'h = 3.0',
      'print(h)',
      '',
    ].join('\n');

    const cells = parseNotebook(src);
    expect(cells.map((c) => c.kind)).toEqual(['markdown', 'prompt', 'code']);
    expect(cells[0].source).toBe('## Walls\nDraws a perimeter.');
    expect(cells[1].source).toBe('Create 4 walls of height 3m');
    expect(cells[2].source).toBe('h = 3.0\nprint(h)');

    const reparsed = parseNotebook(serializeNotebook(cells));
    expect(reparsed.map((c) => ({ kind: c.kind, source: c.source }))).toEqual(
      cells.map((c) => ({ kind: c.kind, source: c.source })),
    );
  });

  it('preserves a leading plugin header before the first # %% as a code cell', () => {
    const src = '# lapin: tool=Walls\n\n# %% [markdown]\n# Docs\n\n# %%\nprint(1)\n';
    const cells = parseNotebook(src);
    expect(cells.map((c) => c.kind)).toEqual(['code', 'markdown', 'code']);
    expect(cells[0].source).toBe('# lapin: tool=Walls');
  });

  it('combines only non-empty code cells for Run All', () => {
    const cells = [
      createCell('markdown', '# Title'),
      createCell('code', 'a = 1'),
      createCell('prompt', 'add b'),
      createCell('code', 'b = a + 2'),
    ];
    expect(combineCodeCells(cells)).toBe('a = 1\n\nb = a + 2');
  });

  it('finds target code cell right after a prompt cell and collects code context above', () => {
    const c1 = createCell('code', 'storey = "s1"');
    const p1 = createCell('prompt', 'make a wall');
    const c2 = createCell('code', 'w = 1');
    const cells = [c1, p1, c2];

    expect(targetCodeCellIndex(cells, 1)).toBe(2);
    expect(targetCodeCellIndex([c1, p1], 1)).toBeNull();
    expect(codeContextAbove(cells, p1.id)).toContain('storey = "s1"');
    expect(codeContextAbove(cells, p1.id)).not.toContain('w = 1');
  });

  it('supports inserting, moving, and removing cells while keeping at least one cell', () => {
    const a = createCell('code', 'a');
    const b = createCell('markdown', 'b');
    const c = createCell('prompt', 'c');
    let list = insertCellAt([a], 1, b);
    list = insertCellAt(list, 2, c);
    expect(list.map((x) => x.source)).toEqual(['a', 'b', 'c']);

    list = moveCell(list, 2, -1);
    expect(list.map((x) => x.source)).toEqual(['a', 'c', 'b']);

    list = removeCell([a], a.id);
    expect(list).toHaveLength(1);
    expect(list[0].kind).toBe('code');
  });
});
