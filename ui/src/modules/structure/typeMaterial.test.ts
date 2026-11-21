import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { CONCRETE, defaultTypeMaterial, libraryMaterialName, typeMaterialName } from './typeMaterial';

const node = (id: string, type: string, name: string, props: Record<string, unknown> = {}) => ({ id, name, type, props, storeyId: null }) as unknown as NodeDto;

describe('a structure type\'s material (forum topic 190)', () => {
  it('defaults to concrete, a library profile\'s own material (steel when it says none)', () => {
    expect(defaultTypeMaterial(false)).toBe(CONCRETE);
    expect(defaultTypeMaterial(true)).toBe('Steel');
    expect(defaultTypeMaterial(true, 'timber')).toBe('Glued laminated timber');
    expect(libraryMaterialName('aluminium')).toBe('Aluminium');
  });

  it('is read by name: a column\'s or beam\'s material, a footing\'s concrete', () => {
    const nodes = new Map([['m1', node('m1', 'IfcMaterial', 'Steel')], ['m2', node('m2', 'IfcMaterial', 'Plain concrete')]]);
    expect(typeMaterialName(node('b', 'IfcBeamType', 'B', { material: 'm1' }), nodes)).toBe('Steel');
    expect(typeMaterialName(node('p', 'IfcFootingType', 'P', { concrete: 'm2', material: 'm1' }), nodes)).toBe('Plain concrete');
    expect(typeMaterialName(node('c', 'IfcColumnType', 'C'), nodes)).toBe('');
    expect(typeMaterialName(null, nodes)).toBe('');
  });
});
