import { describe, expect, it } from 'vitest';
import { groupedSpecies, speciesGroup, speciesName, type Species } from './species';

const sp = (name: string, type: string, family?: string, ro?: string, latin?: string): Species => ({ name, type, height: 5, crown: 4, color: '#5a9440', share: 1, family, ro, latin });

describe('the planting library in the inspector', () => {
  it('groups the species and names them in the interface language', () => {
    const apple = sp('Apple', 'tree', 'fruit', 'Măr', 'Malus domestica');
    const fir = sp('Silver fir', 'tree', 'fir', 'Brad', 'Abies alba');
    const lime = sp('Lime (Tilia)', 'tree', 'broadleaf', 'Tei');
    const hazel = sp('Hazel', 'bush', 'shrub', 'Alun');
    const lawn = sp('Lawn', 'grass', 'ground', 'Gazon');
    expect([apple, fir, lime, hazel, lawn].map(speciesGroup)).toEqual(['Fruit trees', 'Conifers', 'Deciduous trees', 'Shrubs', 'Ground cover']);
    expect(groupedSpecies([lawn, hazel, apple]).map(([g]) => g)).toEqual(['Fruit trees', 'Shrubs', 'Ground cover']);
    expect(speciesName(apple, 'ro')).toBe('Măr');
    expect(speciesName(apple, 'en')).toBe('Apple');
    // An old project's species with no Romanian name keeps its own.
    expect(speciesName(sp('My tree', 'tree'), 'ro')).toBe('My tree');
    expect(speciesGroup(sp('My tree', 'tree'))).toBe('Deciduous trees');
  });
});
