import { describe, expect, it } from 'vitest';
import { frameGrips } from './index';
import type { NodeDto } from '../../engine/protocol';

describe('a timber-frame wall\'s grips in its frame elevation (audit §14)', () => {
  const node = { id: 'w1', type: 'IfcWall', props: {} } as unknown as NodeDto;
  const view = { view: 'v1', mark: { id: 'w1', segments: [], data: { frameGrips: [{ key: 'stud:0.6', at: [0.6, 1.2] }, { key: 'king:o1:L', at: [1.1, 1.0] }] } } };

  it('are the studs, kings and jacks the engine drew, each drag one wood.frameGrip', () => {
    const grips = frameGrips(node, view);
    expect(grips.map((g) => g.key)).toEqual(['frame:stud:0.6', 'frame:king:o1:L']);
    expect(grips[0].step!({ x: 0.75, y: 1.2 })).toEqual({ command: 'wood.frameGrip', args: { id: 'w1', viewId: 'v1', key: 'stud:0.6', point: [0.75, 1.2] } });
    expect(grips[0].remove!()).toEqual({ command: 'wood.frameGrip', args: { id: 'w1', viewId: 'v1', key: 'stud:0.6', remove: true } });
    expect(grips[1].remove).toBeUndefined();
  });

  it('are none outside a frame elevation', () => {
    expect(frameGrips(node, { view: 'plan', mark: { id: 'w1', segments: [] } })).toEqual([]);
    expect(frameGrips(node)).toEqual([]);
  });
});
