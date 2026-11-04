import { describe, expect, it } from 'vitest';
import { sweepJson, sweepRows } from './WallTypeSweeps';

describe('a wall type\'s sweeps in the Wall types dialog (decision 298)', () => {
  it('come back to the engine with their keys, their material by name and what the row does not show', () => {
    const stored = [
      { key: 'k1', profileId: 'p1', profile: { points: [] }, mode: 'add', side: 'right', height: 1, offset: 0.01, material: 'm1', flip: true, setbackEnd: 0.5 },
      { key: 'k2', profileId: 'p2', mode: 'cut', side: 'left', height: 2, fromTop: true, returns: 'none' },
    ];
    const rows = sweepRows(stored, (id) => (id === 'm1' ? 'Plaster' : '?'));
    expect(rows.map((r) => [r.mode, r.side, r.material, r.fromTop])).toEqual([['add', 'right', 'Plaster', false], ['cut', 'left', '', true]]);
    const back = rows.map(sweepJson);
    expect(back[0]).toMatchObject({ key: 'k1', profileId: 'p1', mode: 'add', material: 'Plaster', flip: true, setbackEnd: 0.5, offset: 0.01 });
    expect(back[0]).not.toHaveProperty('profile');
    expect(back[1]).toMatchObject({ key: 'k2', mode: 'cut', returns: 'none', fromTop: true });
    expect(back[1]).not.toHaveProperty('material');
  });

  it('a new row has no key: the engine gives it one', () => {
    expect(sweepJson({ profileId: null, mode: 'add', side: 'right', height: 0.3, fromTop: false, offset: 0, material: 'Plaster', returns: null, rest: {} })).not.toHaveProperty('key');
  });
});
