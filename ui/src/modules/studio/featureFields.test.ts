import { describe, expect, it } from 'vitest';
import { featureFieldKeys } from './FeatureCard';
import type { NodeDto } from '../../engine/protocol';

const solid = (operation: string, props: Record<string, unknown> = {}) => ({ id: 's', type: 'IfcBuildingElementProxy', name: 'Solid', props: { operation, ...props } } as unknown as NodeDto);

describe('a Studio solid\'s fields', () => {
  it('show its material whatever made it (forum topic 259)', () => {
    for (const op of ['extrude', 'sweep', 'revolve', 'loft', 'primitive', 'subd', 'prism']) expect(featureFieldKeys(solid(op, { shape: 'box' }))).toContain('material');
  });
  it('still keep each operation\'s own', () => {
    expect(featureFieldKeys(solid('extrude'))).toEqual(['depth', 'offset', 'symmetric', 'taper', 'material', 'cutPriority', 'exportAs']);
  });
});
