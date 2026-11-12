/** A layer table's hatch button (forum #30, decision 493). */

import { describe, expect, it, vi } from 'vitest';

const calls: string[] = [];
vi.mock('../../engine/session', () => ({
  query: async (name: string) => { calls.push(name); return [{ id: 'm1', name: 'Brick' }, { id: 'm2', name: 'Lime plaster' }]; },
}));

const { materialNamed, materialsOf, placeBeside } = await import('./materialHatch');

describe('a layer\'s hatch button', () => {
  it('reads the materials once for a model version, for every row', async () => {
    const v1 = new Map();
    const [a, b] = await Promise.all([materialsOf(v1), materialsOf(v1)]);
    expect(a).toBe(b);
    expect(calls).toEqual(['material.list']);
    await materialsOf(new Map());
    expect(calls).toEqual(['material.list', 'material.list']);
  });

  it('finds the row\'s material by its name, none for one not made yet', async () => {
    const list = await materialsOf(new Map());
    expect(materialNamed(list, 'Brick')?.id).toBe('m1');
    expect(materialNamed(list, 'Stone')).toBeNull();
  });

  it('opens its editor under the button, over it near the bottom, inside the window', () => {
    const screen = { w: 1000, h: 800 };
    expect(placeBeside({ left: 100, top: 200, bottom: 220 }, { w: 400, h: 200 }, screen)).toEqual({ left: 100, top: 224 });
    expect(placeBeside({ left: 900, top: 700, bottom: 720 }, { w: 400, h: 200 }, screen)).toEqual({ left: 592, top: 496 });
    expect(placeBeside({ left: 10, top: 20, bottom: 40 }, { w: 400, h: 900 }, screen)).toEqual({ left: 10, top: 8 });
  });
});
