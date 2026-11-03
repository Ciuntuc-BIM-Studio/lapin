import { describe, expect, it } from 'vitest';
import { anchorCounts, boltLabel, nodeFields, partsRows, weldsText } from './nodes';

describe('the steel connections', () => {
  it('show the parameters of their kind, a base its ribs only when fixed', () => {
    expect(nodeFields('base', 'pinned')).not.toContain('stiffeners');
    expect(nodeFields('base', 'fixed')).toContain('stiffeners');
    expect(nodeFields('base')).toEqual(expect.arrayContaining(['anchor', 'anchors', 'anchorLength', 'grout']));
    expect(nodeFields('eaves')).toEqual(expect.arrayContaining(['extension', 'stiffeners', 'rows', 'cols']));
    expect(nodeFields('apex')).not.toContain('stiffeners');
    expect(nodeFields('post')).toContain('gap');
    expect(nodeFields('post')).not.toContain('cols');
  });

  it('name bolts by their diameter and offer the anchors a base takes', () => {
    expect(boltLabel(0.02)).toBe('M20');
    expect(boltLabel(0.027)).toBe('M27');
    expect(anchorCounts('pinned')).toEqual([2, 4]);
    expect(anchorCounts('fixed')).toEqual([4, 6, 8]);
  });

  it('list their parts plates first, and their welds by throat', () => {
    const rows = partsRows([
      { part: 'Bolt', size: 'M20×60', grade: '8.8', count: 12 },
      { part: 'Plate', size: 'PL 20×170×800', grade: 'S235', count: 1 },
      { part: 'Anchor', size: 'M24×860', grade: '5.6', count: 4 },
    ]);
    expect(rows.map((r) => r.part)).toEqual(['Plate', 'Bolt', 'Anchor']);
    expect(rows[1]).toEqual({ part: 'Bolt', text: 'M20×60 · 8.8', count: 12 });
    expect(weldsText([{ throat: 0.005, length: 3.2, count: 2 }, { throat: 0.007, length: 1.234, count: 1 }])).toBe('a5 · 3.20 m, a7 · 1.23 m');
  });
});
