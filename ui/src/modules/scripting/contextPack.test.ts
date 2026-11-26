import { describe, expect, it } from 'vitest';
import { buildContextPack, expandQueryTokens, matchCatalogue, matchRecipes } from './contextPack';
import type { Catalogue } from './logic';

const CAT: Catalogue = {
  commands: [
    { name: 'architecture.addWall', label: 'Wall', module: 'architecture', args: [{ name: 'storeyId', type: 'str' }, { name: 'x1', type: 'float' }, { name: 'height', type: 'float' }] },
    { name: 'structure.addColumn', label: 'Column', module: 'structure', args: [{ name: 'storeyId', type: 'str' }, { name: 'x', type: 'float' }, { name: 'y', type: 'float' }] },
    { name: 'architecture.addSlab', label: 'Floor', module: 'architecture', args: [{ name: 'storeyId', type: 'str' }, { name: 'points', type: 'list' }] },
  ],
  queries: [],
};

describe('Smart Context Pack (RO+EN pre-retrieval + cookbook + kernel vars)', () => {
  it('expands Romanian BIM terms with diacritics to English catalogue tokens', () => {
    const tokens = expandQueryTokens('Pune 6 stâlpi pe parter și un planșeu');
    expect(tokens).toContain('column');
    expect(tokens).toContain('storey');
    expect(tokens).toContain('slab');
  });

  it('pre-retrieves exact Python signatures for Romanian prompts', () => {
    const sigs = matchCatalogue(CAT, 'Creează pereți pe parter');
    expect(sigs[0]).toContain('lapin.architecture.add_wall(storey_id: str, x1: float, height: float)');
  });

  it('matches relevant few-shot cookbook recipes and assembles a bounded context pack with kernel vars', () => {
    const recipes = matchRecipes('Pune 4 stâlpi pe o grilă');
    expect(recipes.some((r) => r.id === 'column-grid')).toBe(true);

    const pack = buildContextPack({
      prompt: 'Pune 4 stâlpi pe o grilă',
      catalogue: CAT,
      kernelVars: [{ name: 'storey_id', type: 'str', preview: "'s1'" }],
      contextAbove: 'dx = 5.0',
    });
    expect(pack).toContain('lapin.structure.add_column');
    expect(pack).toContain("storey_id: str = 's1'");
    expect(pack).toContain('dx = 5.0');
  });
});
