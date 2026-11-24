import { describe, expect, it } from 'vitest';
import { markPaths } from './markPaths';

describe('marks lit over a view (forum topic 252)', () => {
  it('keys every path apart when one element owns several marks (its own and an inset\'s copy), and draws each in screen pixels', () => {
    const marks = [
      { id: 'w', segments: [0, 0, 1, 0] },
      { id: 'w', segments: [10, 10, 11, 10, 11, 10, 11, 11] },
      { id: 'd', segments: [] },
    ];
    const paths = markPaths(marks, 'hover', (p) => ({ x: p.x * 2, y: -p.y }));
    expect(new Set(paths.map((p) => p.key)).size).toBe(3);
    expect(paths[0].d).toBe('M0 0L2 0');
    expect(paths[1].d).toBe('M20 -10L22 -10M22 -10L22 -11');
    expect(paths[2].d).toBe('');
  });

  it('follows the view: the same marks drawn after a zoom are drawn where the zoom puts them', () => {
    const marks = [{ id: 'w', segments: [1, 1, 2, 1] }];
    expect(markPaths(marks, 'sel', (p) => ({ x: p.x * 3, y: p.y * 3 }))[0].d).toBe('M3 3L6 3');
  });
});
