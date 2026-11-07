import { describe, expect, it } from 'vitest';
import { click, finishOpen, preview, typed } from './direct';

describe('the direct sketch tools (decision 374)', () => {
  it('a line goes on point by point and closes on its first point', () => {
    let o = click('line', [], { x: 0, y: 0 });
    expect(o).toEqual({ kind: 'more', pts: [{ x: 0, y: 0 }] });
    o = click('line', [{ x: 0, y: 0 }], { x: 0, y: 0 });
    expect(o.kind).toBe('none');
    const three = [{ x: 0, y: 0 }, { x: 2, y: 0 }, { x: 2, y: 1 }];
    expect(click('line', three, { x: 0, y: 0 })).toEqual({ kind: 'closed', outline: three });
    // Two points closing on the first: not a shape yet.
    expect(click('line', three.slice(0, 2), { x: 0, y: 0 }).kind).toBe('more');
    expect(finishOpen('line', three.slice(0, 2))).toEqual({ kind: 'open', pts: three.slice(0, 2) });
    expect(finishOpen('line', three.slice(0, 1)).kind).toBe('none');
    expect(finishOpen('rect', three).kind).toBe('none');
  });

  it('a typed length goes the way the cursor points, a length<angle at its angle', () => {
    const o = typed('line', [{ x: 1, y: 1 }], { kind: 'length', value: 2 }, { x: 1, y: 5 });
    expect(o).toEqual({ kind: 'more', pts: [{ x: 1, y: 1 }, { x: 1, y: 3 }] });
    const a = typed('line', [{ x: 0, y: 0 }], { kind: 'lengthAngle', value: 2, deg: 90 }, null);
    expect(a.kind === 'more' && a.pts[1].x).toBeCloseTo(0, 9);
    expect(a.kind === 'more' && a.pts[1].y).toBeCloseTo(2, 9);
  });

  it('a rectangle by its corners or its typed size toward the cursor', () => {
    expect(click('rect', [{ x: 0, y: 0 }], { x: 2, y: 1 })).toEqual({ kind: 'closed', outline: [{ x: 0, y: 0 }, { x: 2, y: 0 }, { x: 2, y: 1 }, { x: 0, y: 1 }] });
    expect(click('rect', [{ x: 0, y: 0 }], { x: 2, y: 0 }).kind).toBe('none');
    const t = typed('rect', [{ x: 1, y: 1 }], { kind: 'pair', a: 1.2, b: 0.6 }, { x: 0, y: 0 });
    expect(t.kind === 'closed' && t.outline[2]).toEqual({ x: 1 - 1.2, y: 1 - 0.6 });
    expect(typed('rect', [{ x: 1, y: 1 }], { kind: 'length', value: 1 }, null).kind).toBe('none');
  });

  it('a circle by its radius, clicked or typed (300 or r300)', () => {
    const c = click('circle', [{ x: 0, y: 0 }], { x: 3, y: 4 });
    expect(c.kind === 'closed' && c.circle).toEqual({ c: { x: 0, y: 0 }, r: 5 });
    const r = typed('circle', [{ x: 0, y: 0 }], { kind: 'radius', value: 0.3 }, null);
    expect(r.kind === 'closed' && r.circle?.r).toBe(0.3);
    expect(typed('circle', [{ x: 0, y: 0 }], { kind: 'length', value: 0.3 }, null).kind).toBe('closed');
    expect(preview('circle', [{ x: 0, y: 0 }], { x: 1, y: 0 }).closed).toBe(true);
    expect(preview('line', [{ x: 0, y: 0 }], { x: 1, y: 0 })).toEqual({ pts: [{ x: 0, y: 0 }, { x: 1, y: 0 }], closed: false });
  });
});
