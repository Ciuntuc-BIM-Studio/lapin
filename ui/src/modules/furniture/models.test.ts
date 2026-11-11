import { describe, expect, it } from 'vitest';
import { byGroup, clampAxis, isLook, lookPatch, thumbnailUrl, zonedAxes, type FurnitureModelInfo } from './models';

const model = (id: string, group: string, zones: FurnitureModelInfo['zones'] = [[0.1, 0.9], null, [0.5, 0.7]]): FurnitureModelInfo => ({
  id, name: id, group, predefined: 'SOFA', size: [2, 0.9, 0.82], min: [0.54, 0.35, 0.74], max: [5, 1.8, 1.64], mount: 0, against: true,
  roles: ['upholstery', 'legs'], zones, thumbnail: `furniture-models/${id}.png`, licence: 'CC0-1.0', source: 'kenney-furniture-kit',
});

describe('furniture from models (decision 477)', () => {
  it('groups the gallery in the catalog’s order', () => {
    const g = byGroup([model('a', 'Sofas'), model('b', 'Chairs'), model('c', 'Sofas')]);
    expect(g.map(([k, l]) => [k, l.map((m) => m.id)])).toEqual([['Sofas', ['a', 'c']], ['Chairs', ['b']]]);
  });

  it('serves a preview from the interface’s own files, fetched when shown', () => {
    expect(thumbnailUrl(model('loungeSofa', 'Sofas'), '/')).toBe('/furniture-models/loungeSofa.png');
    expect(thumbnailUrl(model('loungeSofa', 'Sofas'), '/web')).toBe('/web/furniture-models/loungeSofa.png');
    expect(thumbnailUrl(model('loungeSofa', 'Sofas'), './')).toBe('./furniture-models/loungeSofa.png');
  });

  it('applies a look to the roles the model has and knows when it is on', () => {
    const look = { key: 'warm', label: 'Warm', materials: { upholstery: 'Velvet terracotta', legs: 'Walnut veneer', shade: 'Opal glass' } };
    expect(lookPatch(look, ['upholstery', 'legs'])).toEqual({ upholstery: 'Velvet terracotta', legs: 'Walnut veneer' });
    expect(isLook(look, ['upholstery', 'legs'], { upholstery: 'Velvet terracotta', legs: 'Walnut veneer' })).toBe(true);
    expect(isLook(look, ['upholstery', 'legs'], { upholstery: 'Velvet terracotta', legs: 'Oak solid wood' })).toBe(false);
    expect(isLook(look, ['foliage'], {})).toBe(false);
  });

  it('keeps a size in the model’s range and says which axes stretch by zones', () => {
    const m = model('s', 'Sofas');
    expect(clampAxis(m, 0, 9)).toBe(5);
    expect(clampAxis(m, 2, 0.1)).toBe(0.74);
    expect(clampAxis(m, 1, 1)).toBe(1);
    expect(zonedAxes(m)).toEqual(['Width', 'Height']);
    expect(zonedAxes(model('p', 'Plants', [null, null, null]))).toEqual([]);
  });
});
