import { describe, expect, it, vi } from 'vitest';
import {
  addDash, addFamily, cellOf, createArgs, dashKind, dashText, deletePattern, draftOf, draftProblem, freeName, newDraft, parseDashes, parseNum, patchFamily, previewArgs,
  canCopy, canEdit, keepsOpen, realSizeNote, removeDash, startFrom, removeFamily, renameImpact, resultNote, setDashKind, setDashLength, skippedNote, toPatternArgs, updateArgs, withOrigin, MAX_DASHES, MAX_FAMILIES,
  type Draft,
} from './hatchPatternModel';

const lines = (): Draft => ({ name: 'brick', description: ' courses ', units: 'model', families: [{ angle: 0, origin: [0, 0], deltaX: 0, deltaY: 75, dashes: [215, -10] }] });

describe('line families', () => {
  it('start as a continuous line at 45 degrees and grow by copying the last family', () => {
    const d = newDraft();
    expect(d.families).toEqual([{ angle: 45, origin: [0, 0], deltaX: 0, deltaY: 3, dashes: [] }]);
    const two = addFamily(patchFamily(d.families, 0, { angle: 90, dashes: [1, -1] }));
    expect(two).toHaveLength(2);
    expect(two[1]).toEqual({ angle: 90, origin: [0, 0], deltaX: 0, deltaY: 3, dashes: [1, -1] });
    expect(two[1].dashes).not.toBe(two[0].dashes);
    expect(removeFamily(two, 0)).toEqual([two[1]]);
    expect(addFamily(Array.from({ length: MAX_FAMILIES }, () => d.families[0]))).toHaveLength(MAX_FAMILIES);
  });

  it('change one origin number without the other', () => {
    expect(withOrigin({ angle: 0, origin: [1, 2], deltaX: 0, deltaY: 1, dashes: [] }, 1, 9)).toEqual([1, 9]);
  });

  it('read numbers with a comma or a dot and nothing else', () => {
    expect(parseNum('2,5')).toBe(2.5);
    expect(parseNum(' -3 ')).toBe(-3);
    expect(parseNum('1e2')).toBe(100);
    expect(parseNum('')).toBeNull();
    expect(parseNum('12abc')).toBeNull();
  });
});

describe('dash lists', () => {
  it('say dash, gap or dot by the sign, as a .pat does', () => {
    expect(dashKind(5)).toBe('dash');
    expect(dashKind(-2)).toBe('gap');
    expect(dashKind(0)).toBe('dot');
  });

  it('add a dash, a gap or a dot, up to the limit', () => {
    let d: number[] = [];
    d = addDash(d, 'dash', 4); d = addDash(d, 'gap', 2); d = addDash(d, 'dot');
    expect(d).toEqual([4, -2, 0]);
    expect(addDash(Array.from({ length: MAX_DASHES }, () => 1), 'dash')).toHaveLength(MAX_DASHES);
  });

  it('change a length keeping its kind, a kind keeping its length, and remove one', () => {
    expect(setDashLength([4, -2, 0], 1, 6)).toEqual([4, -6, 0]);
    expect(setDashLength([4, -2, 0], 2, 6)).toEqual([4, -2, 0]);
    expect(setDashKind([4, -2, 0], 0, 'gap')).toEqual([-4, -2, 0]);
    expect(setDashKind([4, -2, 0], 0, 'dot')).toEqual([0, -2, 0]);
    expect(setDashKind([4, -2, 0], 2, 'dash')).toEqual([4, -2, 2]);
    expect(removeDash([4, -2, 0], 1)).toEqual([4, 0]);
  });

  it('read and write the list as text', () => {
    expect(dashText([5, -2, 0, -2])).toBe('5 -2 0 -2');
    expect(parseDashes('5 -2, 0 -2')).toEqual([5, -2, 0, -2]);
    expect(parseDashes('5 x')).toBeNull();
    expect(parseDashes(Array.from({ length: MAX_DASHES + 1 }, () => '1').join(' '))).toBeNull();
  });
});

describe('what the commands are sent', () => {
  it('hatch.create: the name in capitals, a trimmed description, units and families', () => {
    expect(createArgs(lines())).toEqual({ name: 'BRICK', description: 'courses', units: 'model', families: [{ angle: 0, origin: [0, 0], deltaX: 0, deltaY: 75, dashes: [215, -10] }] });
    expect(createArgs({ ...lines(), description: '' })).not.toHaveProperty('description');
  });

  it('hatch.update: the new name only when it changed, and a tile pattern never sent its tile back', () => {
    expect(updateArgs('BRICK', lines())).not.toHaveProperty('newName');
    expect(updateArgs('BRICK', { ...lines(), name: 'bond' })).toMatchObject({ name: 'BRICK', newName: 'BOND', description: 'courses', units: 'model' });
    const tile: Draft = { name: 'DOTS', description: '', units: 'paper', families: [], tile: { width: 5, height: 5, lines: [[0, 0, 1, 1]], arcs: [] } };
    const u = updateArgs('DOTS', tile);
    expect(u).toEqual({ name: 'DOTS', description: '', units: 'paper' });
    expect(createArgs(tile)).toMatchObject({ tile: tile.tile });
    expect(createArgs(tile)).not.toHaveProperty('families');
  });

  it('hatch.preview: the pattern inline, a paper pattern at a factor and a real-size one at a view scale', () => {
    expect(previewArgs({ ...lines(), units: 'paper' }, 2, 60)).toMatchObject({ size: 60, scale: 2, pattern: { units: 'paper' } });
    const m = previewArgs(lines(), 50, 60);
    expect(m).toMatchObject({ viewScale: 50, scale: 1, pattern: { units: 'model' } });
    expect(realSizeNote('paper', 50)).toBeNull();
    expect(realSizeNote('model', 50)?.vars).toEqual({ mm: 0.02, scale: 50 });
  });

  it('read a definition back as a draft with its own copies', () => {
    const def = { name: 'X', description: 'd', units: 'model' as const, kind: 'lines' as const, project: true, families: lines().families, uses: 3 };
    const d = draftOf(def);
    expect(d.families[0].dashes).not.toBe(def.families[0].dashes);
    expect(d).toMatchObject({ name: 'X', units: 'model' });
  });
});

describe('checks before saving', () => {
  it('want one word for a name and a name not taken', () => {
    expect(draftProblem({ ...lines(), name: 'two words' })).toMatch(/one word/);
    expect(draftProblem({ ...lines(), name: 'brick' }, ['BRICK'])).toMatch(/exists/);
    expect(draftProblem({ ...lines(), name: 'brick' }, ['BRICK'], 'BRICK')).toBeNull();
  });

  it('want lines with a distance, and dashes that add up', () => {
    expect(draftProblem({ ...lines(), families: [] })).toMatch(/at least one/);
    expect(draftProblem({ ...lines(), families: [{ angle: 0, origin: [0, 0], deltaX: 0, deltaY: 0, dashes: [] }] })).toMatch(/distance/);
    expect(draftProblem({ ...lines(), families: [{ angle: 0, origin: [0, 0], deltaX: 0, deltaY: 3, dashes: [0, 0] }] })).toMatch(/add up/);
    expect(draftProblem(lines())).toBeNull();
  });

  it('give a free name', () => {
    expect(freeName('brick-copy', ['BRICK-COPY', 'BRICK-COPY-2'])).toBe('BRICK-COPY-3');
    expect(freeName('', [])).toBe('PATTERN');
  });

  it('count the places a rename moves', () => {
    expect(renameImpact('BRICK', 'bond', 4)).toEqual({ renamed: true, uses: 4 });
    expect(renameImpact('BRICK', 'brick', 4).renamed).toBe(false);
    expect(renameImpact('BRICK', '', 4).renamed).toBe(false);
  });
});

describe('deleting a pattern', () => {
  const deps = (count: number | null, yes: boolean) => ({
    usage: vi.fn(async () => count), confirm: vi.fn(() => yes), remove: vi.fn(async () => true),
  });

  it('asks plainly about a pattern nobody names, and sends no force', async () => {
    const d = deps(0, true);
    expect(await deletePattern('A', d)).toBe('deleted');
    expect(d.remove).toHaveBeenCalledWith('A', false);
  });

  it('says how many places name a used pattern and sends force only after a yes', async () => {
    const d = deps(3, true);
    expect(await deletePattern('A', d)).toBe('deleted');
    expect(d.confirm).toHaveBeenCalledWith(expect.stringContaining('{count}'), { name: 'A', count: 3 });
    expect(d.remove).toHaveBeenCalledWith('A', true);
  });

  it('deletes nothing when the user says no, or when the usage cannot be read', async () => {
    const no = deps(3, false);
    expect(await deletePattern('A', no)).toBe('cancelled');
    expect(no.remove).not.toHaveBeenCalled();
    const lost = deps(null, true);
    expect(await deletePattern('A', lost)).toBe('failed');
    expect(lost.confirm).not.toHaveBeenCalled();
  });

  it('reports a refused delete', async () => {
    const d = { ...deps(0, true), remove: vi.fn(async () => false) };
    expect(await deletePattern('A', d)).toBe('failed');
  });
});

describe('export and Save as hatch pattern', () => {
  it('warn about the tile patterns a .pat file leaves out', () => {
    expect(skippedNote([])).toBeNull();
    expect(skippedNote(['DOTS', 'WAVES'])?.vars).toEqual({ names: 'DOTS, WAVES' });
  });

  it('make a cell from two corners in any order, and none from a line', () => {
    expect(cellOf({ x: 2, y: 3 }, { x: 0, y: 1 })).toEqual({ width: 2, height: 2, origin: [0, 1] });
    expect(cellOf({ x: 0, y: 0 }, { x: 5, y: 0 })).toBeNull();
  });

  it('send drafting.toPattern with the cell, or with none for the bounds', () => {
    expect(toPatternArgs('v1', ['a', 'b'], { width: 0.1, height: 0.2, origin: [1, 1] }, 'my-pat', ' d ', 'paper'))
      .toEqual({ viewId: 'v1', ids: ['a', 'b'], cell: { width: 0.1, height: 0.2, origin: [1, 1] }, name: 'MY-PAT', description: 'd', units: 'paper' });
    expect(toPatternArgs('v1', ['a'], null, 'x', '', 'model')).toEqual({ viewId: 'v1', ids: ['a'], name: 'X', units: 'model' });
  });

  it('say what the pattern became', () => {
    expect(resultNote({ kind: 'tile' }).text).toMatch(/tile/);
    expect(resultNote({ kind: 'lines', families: [{}, {}] }).vars).toEqual({ n: 2 });
  });
});

describe('starting from an existing pattern (forum 275)', () => {
  const def = { name: 'BRICK', description: 'Courses', units: 'model' as const, kind: 'lines' as const, project: false, families: lines().families, uses: 3 };

  it('copies any pattern with lines or a tile into a new draft under a free name, the original untouched', () => {
    const d = startFrom(def, ['BRICK', 'BRICK-COPY']);
    expect(d.name).toBe('BRICK-COPY-2');
    expect(d.description).toBe('Courses');
    expect(d.units).toBe('model');
    expect(d.families).toEqual(def.families);
    expect(d.families[0]).not.toBe(def.families[0]);
    expect(d.families[0].dashes).not.toBe(def.families[0].dashes);
    expect(createArgs(d)).toEqual({ name: 'BRICK-COPY-2', description: 'Courses', units: 'model', families: def.families });
    const mine = startFrom({ ...def, name: 'MINE', project: true }, ['MINE']);
    expect(mine.name).toBe('MINE-COPY');
    const tile = { width: 10, height: 10, lines: [[0, 0, 10, 0]], arcs: [] };
    const t = startFrom({ ...def, kind: 'tile', families: undefined, tile }, []);
    expect(t.tile).toEqual(tile);
    expect(createArgs(t)).toMatchObject({ tile });
    expect(draftProblem(t, ['BRICK'])).toBeNull();
  });

  it('copies patterns that have lines or a tile; edits only the project\'s own', () => {
    expect(canCopy(def)).toBe(true);
    expect(canCopy({ ...def, kind: 'tile', families: undefined, tile: { width: 1, height: 1, lines: [], arcs: [] } })).toBe(true);
    expect(canCopy({ ...def, kind: 'solid', families: [] })).toBe(false);
    expect(canCopy({ ...def, kind: 'batt', families: [] })).toBe(false);
    expect(canEdit(def)).toBe(false);
    expect(canEdit({ ...def, project: true })).toBe(true);
    expect(canEdit(null)).toBe(false);
  });
});

describe('choosing a pattern in the list (forum 275)', () => {
  it('keeps the editor open when the pattern shown is clicked again, and reads any other', () => {
    // The pattern just made is the one selected: clicking its name again must not empty the editor (nothing reads it back).
    expect(keepsOpen('DIAGONAL-5', false, true, 'DIAGONAL-5')).toBe(true);
    expect(keepsOpen('DIAGONAL-5', false, true, 'BRICK')).toBe(false);
    // A new pattern being made, or nothing read yet: the click reads it.
    expect(keepsOpen(null, true, true, 'DIAGONAL-5')).toBe(false);
    expect(keepsOpen('DIAGONAL-5', false, false, 'DIAGONAL-5')).toBe(false);
  });
});
