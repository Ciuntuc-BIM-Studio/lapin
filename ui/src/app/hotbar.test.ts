import { describe, expect, it } from 'vitest';
import { defaultSlots, reach, readHotbars, slotsOf, withSlot, withSwap } from './hotbar';

const known = (id: string) => id !== 'gone';

describe('quick tools', () => {
  it('takes the modules\' own tools in a workspace no one has set, the first nine', () => {
    const defaults = ['select', 'wall', 'door', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h'];
    expect(slotsOf({}, 'architecture', defaults, known)).toEqual(defaults.slice(0, 9));
    expect(defaultSlots(['wall'])).toEqual(['wall', null, null, null, null, null, null, null, null]);
  });

  it('keeps one set per workspace and leaves empty a tool the edition lacks', () => {
    const hotbars = { structure: ['column', 'gone', null, 'beam', null, null, null, null, null] };
    expect(slotsOf(hotbars, 'structure', ['wall'], known)).toEqual(['column', null, null, 'beam', null, null, null, null, null]);
    expect(slotsOf(hotbars, 'architecture', ['wall'], known)[0]).toBe('wall');
  });

  it('puts a tool in one slot only: in another, it leaves the first', () => {
    const slots = defaultSlots(['wall', 'door', 'window']);
    expect(withSlot(slots, 5, 'wall')).toEqual([null, 'door', 'window', null, null, 'wall', null, null, null]);
    expect(withSlot(slots, 1, null)).toEqual(['wall', null, 'window', null, null, null, null, null, null]);
    expect(withSlot(slots, 9, 'roof')).toBe(slots);
  });

  it('swaps two slots, an empty one too', () => {
    const slots = defaultSlots(['wall', 'door']);
    expect(withSwap(slots, 0, 1).slice(0, 2)).toEqual(['door', 'wall']);
    expect(withSwap(slots, 0, 4).slice(0, 5)).toEqual([null, 'door', null, null, 'wall']);
    expect(withSwap(slots, 2, 2)).toBe(slots);
  });

  it('reaches with the number keys up to the last slot holding a tool', () => {
    expect(reach(defaultSlots([]))).toBe(0);
    expect(reach([null, null, 'wall', null, null, null, null, null, null])).toBe(3);
  });

  it('reads what was kept, leaving out what it cannot read', () => {
    expect(readHotbars(null)).toEqual({});
    expect(readHotbars({ mine: ['wall', 3, null], bad: 'x' })).toEqual({ mine: ['wall', null, null, null, null, null, null, null, null] });
  });
});
