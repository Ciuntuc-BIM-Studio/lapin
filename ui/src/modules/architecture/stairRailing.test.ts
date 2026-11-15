import { describe, expect, it } from 'vitest';
import { railingPick, railingSide } from './StairRailingPanel';
import { partSelected } from '../../viewport/select3d';
import type { NodeDto } from '../../engine/protocol';

/** A stair's railings selected one by one (forum topic 102): stairId#railing:left, by a click in 3D or in a plan. */
describe('a stair railing selected on its own', () => {
  const stair = { id: 's1', type: 'IfcStair', name: 'Stair 1', props: {} } as unknown as NodeDto;

  it('is what a 3D click on the railing\'s bars or glass selects, nothing else of the stair', () => {
    expect(railingPick(stair, 'railing:left')).toBe('railing:left');
    expect(railingPick(stair, 'railing-glass:right')).toBe('railing:right');
    expect(railingPick(stair, 'flight:0')).toBeNull();
    expect(railingPick(stair, 'landing:0')).toBeNull();
  });

  it('reads its side back from its tag', () => {
    expect(railingSide('railing:left')).toBe('left');
    expect(railingSide('railing:right')).toBe('right');
    expect(railingSide('railing:middle')).toBeNull();
  });

  it('lights its own bars and glass in 3D, not the other side\'s', () => {
    expect(partSelected('railing:left', 'railing:left')).toBe(true);
    expect(partSelected('railing-glass:left', 'railing:left')).toBe(true);
    expect(partSelected('railing:right', 'railing:left')).toBe(false);
    expect(partSelected('railing-glass:right', 'railing:left')).toBe(false);
    expect(partSelected('flight:0', 'railing:left')).toBe(false);
  });
});
