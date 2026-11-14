import { describe, expect, it } from 'vitest';
import { addColour, addColours, cleanColours, coloursOfMaterials, createPalette, deletePalette, movePalette, moveColour, normalise, pushRecent, readKept, removeColour, renamePalette, splitAlpha, RECENT_MAX } from './colours';

describe('colours', () => {
  it('are #rrggbb in lower case, from #rgb or with an opacity left off', () => {
    expect(normalise('#ABC')).toBe('#aabbcc');
    expect(normalise('#B5532D')).toBe('#b5532d');
    expect(normalise('#b5532d80')).toBe('#b5532d');
    expect(normalise('red')).toBeNull();
    expect(normalise(5)).toBeNull();
    expect(splitAlpha('#b5532d80')).toEqual({ rgb: '#b5532d', alpha: '80' });
    expect(splitAlpha('#b5532d')).toEqual({ rgb: '#b5532d', alpha: '' });
    expect(cleanColours(['#AAA', '#aaaaaa', 'x', '#112233'])).toEqual(['#aaaaaa', '#112233']);
  });

  it('remember the latest first, each once, a limited number', () => {
    expect(pushRecent(['#111111', '#222222'], '#222222')).toEqual(['#222222', '#111111']);
    expect(pushRecent([], 'nope')).toEqual([]);
    let r: string[] = [];
    for (let i = 0; i < 30; i++) r = pushRecent(r, `#0000${(i + 16).toString(16)}`);
    expect(r).toHaveLength(RECENT_MAX);
    expect(r[0]).toBe('#00002d');
  });
});

describe('the user\'s palettes', () => {
  it('are made, renamed with names kept different, moved, filled and deleted', () => {
    let p = createPalette([], 'Facade', 'a');
    p = createPalette(p, 'Facade', 'b');
    expect(p.map((x) => x.name)).toEqual(['Facade', 'Facade 2']);
    p = renamePalette(p, 'b', 'Roof');
    p = renamePalette(p, 'a', '  ');
    expect(p.map((x) => x.name)).toEqual(['Facade', 'Roof']);
    expect(movePalette(p, 'b', -1).map((x) => x.id)).toEqual(['b', 'a']);
    expect(movePalette(p, 'a', -1).map((x) => x.id)).toEqual(['a', 'b']);
    p = addColour(addColour(p, 'a', '#ABCDEF'), 'a', '#abcdef');
    expect(p[0].colours).toEqual(['#abcdef']);
    expect(deletePalette(p, 'a').map((x) => x.id)).toEqual(['b']);
  });

  it('reorder and take out colours', () => {
    expect(moveColour(['#111111', '#222222', '#333333'], 0, 2)).toEqual(['#222222', '#333333', '#111111']);
    expect(moveColour(['#111111', '#222222'], 5, 0)).toEqual(['#111111', '#222222']);
    expect(removeColour(['#111111', '#222222'], 0)).toEqual(['#222222']);
    expect(addColours(['#111111'], ['#111111', '#222222'])).toEqual(['#111111', '#222222']);
  });

  it('take the colours of the materials in use', () => {
    const rows = [{ color: '#AA0000', inUse: true }, { color: '#00bb00', inUse: false }, { color: '#aa0000', inUse: true }];
    expect(coloursOfMaterials(rows)).toEqual(['#aa0000']);
    expect(coloursOfMaterials(rows, false)).toEqual(['#aa0000', '#00bb00']);
  });

  it('are read back as kept, what is unreadable left out', () => {
    const k = readKept({ recent: ['#112233', 'x'], palettes: [{ id: 'a', name: 'Mine', colours: ['#fff'] }, { name: '' }, 5] });
    expect(k.recent).toEqual(['#112233']);
    expect(k.palettes).toEqual([{ id: 'a', name: 'Mine', colours: ['#ffffff'] }]);
    expect(readKept('garbage')).toEqual({ recent: [], palettes: [] });
  });
});
