import { describe, expect, it } from 'vitest';
import type { ModelState } from '../../engine/model';
import type { NodeDto } from '../../engine/protocol';
import { layerRankHint } from './layerRank';

const node = (id: string, type: string, name: string, props: Record<string, unknown> = {}): NodeDto => ({ id, type, name, props } as unknown as NodeDto);
const model = (...nodes: NodeDto[]): ModelState => ({ nodes: new Map(nodes.map((n) => [n.id, n])), relations: new Map() });

describe('layerRankHint', () => {
  it('a core ranks 50 and any other layer 0 when the cut order says nothing of its material', () => {
    const m = model(node('b', 'IfcMaterial', 'Brick'));
    expect(layerRankHint(m, 'Brick', true)).toBe(50);
    expect(layerRankHint(m, 'Brick', false)).toBe(0);
    expect(layerRankHint(m, 'Unknown', true)).toBe(50);
  });
  it('takes its material’s priority in the cut order, by name whatever the case', () => {
    const m = model(node('c', 'IfcMaterial', 'Concrete'), node('o', 'LapinCutOrder', 'Cut order', { materials: { c: 100 } }));
    expect(layerRankHint(m, 'concrete ', false)).toBe(100);
    expect(layerRankHint(m, 'Concrete', true)).toBe(100);
  });
});
