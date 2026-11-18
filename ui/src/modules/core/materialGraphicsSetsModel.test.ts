import { describe, expect, it } from 'vitest';
import { copyName, duplicateEntry, emptyEntry, entriesProblem, entryLabel, matchFor, modeOf, moveEntry, moveEntryTo, patchEntryKind, removeEntryAt, specificity, usedBy, type SetEntry } from './materialGraphicsSetsModel';

describe('material graphics set entries', () => {
  it('rank the matches as the engine does: id, name, category, all', () => {
    expect(specificity({ material: 'm1' })).toBe(3);
    expect(specificity({ materialName: 'brick' })).toBe(2);
    expect(specificity({ category: 'Finish' })).toBe(1);
    expect(specificity({})).toBe(0);
    expect(modeOf({ materialName: 'x' })).toBe('name');
    expect(modeOf({})).toBe('all');
  });

  it('change the match mode, keeping what the entry had for it', () => {
    expect(matchFor('all', { material: 'm' }, ['Structure'])).toEqual({});
    expect(matchFor('category', {}, ['Structure', 'Finish'])).toEqual({ category: 'Structure' });
    expect(matchFor('name', { materialName: 'brick', category: 'Finish' }, [])).toEqual({ materialName: 'brick' });
  });

  it('read in a list', () => {
    expect(entryLabel({ match: { material: 'm1' } }, (id) => (id === 'm1' ? 'Brick' : undefined))).toBe('Brick');
    expect(entryLabel({ match: { materialName: 'brick' } }, () => undefined)).toBe('“brick”');
    expect(entryLabel({ match: {} }, () => undefined)).toBe('*');
  });

  it('reorder, duplicate and remove, never past the ends', () => {
    const l: SetEntry[] = [{ match: { category: 'A' } }, { match: { category: 'B' } }, { match: { category: 'C' } }];
    expect(moveEntry(l, 0, -1)).toBe(l);
    expect(moveEntry(l, 0, 1).map((e) => e.match.category)).toEqual(['B', 'A', 'C']);
    expect(moveEntryTo(l, 2, 0).map((e) => e.match.category)).toEqual(['C', 'A', 'B']);
    expect(duplicateEntry(l, 1)).toHaveLength(4);
    expect(removeEntryAt(l, 1).map((e) => e.match.category)).toEqual(['A', 'C']);
  });

  it('lay a graphics patch on a kind, and clear it', () => {
    const e = patchEntryKind(emptyEntry(), 'plan', { hatch: 'ANSI31' });
    expect(e.plan).toEqual({ hatch: 'ANSI31' });
    expect(patchEntryKind(e, 'plan', null).plan).toBeUndefined();
  });

  it('refuse an entry that names nothing', () => {
    expect(entriesProblem([{ match: { material: '' } }])).toMatch(/Entry 1/);
    expect(entriesProblem([{ match: {} }, { match: { materialName: 'brick' } }])).toBeNull();
  });

  it('name the styles that use a set, and a copy no set has', () => {
    expect(usedBy({ styles: ['a', 'b'] }, (id) => (id === 'a' ? 'Structural' : undefined))).toEqual(['Structural', 'b']);
    expect(copyName('Structure', ['Structure'])).toBe('Structure (copy)');
    expect(copyName('Structure', ['Structure', 'structure (copy)'])).toBe('Structure (copy 2)');
  });
});
