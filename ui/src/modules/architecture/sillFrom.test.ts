import { describe, expect, it } from 'vitest';
import { finishedFloorOf, floorHintShown, sillFromOf } from './sillFrom';

const n = (props: Record<string, unknown>) => ({ props }) as never;

describe('sill reference', () => {
  it('reads what an opening says, structural when absent', () => {
    expect(sillFromOf(n({}))).toBe('structural');
    expect(sillFromOf(n({ sillFrom: 'finished' }))).toBe('finished');
    expect(sillFromOf(n({ sillFrom: 'x' }))).toBe('structural');
    expect(sillFromOf(null)).toBe('structural');
  });
  it('derives the finished floor: the wall, else its type, else 0', () => {
    expect(finishedFloorOf(n({ finishedFloor: 0.1 }), n({ finishedFloor: 0.2 }))).toBe(0.1);
    expect(finishedFloorOf(n({ finishedFloor: 0 }), n({ finishedFloor: 0.2 }))).toBe(0);
    expect(finishedFloorOf(n({}), n({ finishedFloor: 0.2 }))).toBe(0.2);
    expect(finishedFloorOf(n({}), undefined)).toBe(0);
  });
  it('hints only when finished is chosen over a floor of 0', () => {
    expect(floorHintShown('finished', 0)).toBe(true);
    expect(floorHintShown('finished', 0.12)).toBe(false);
    expect(floorHintShown('structural', 0)).toBe(false);
  });
});
