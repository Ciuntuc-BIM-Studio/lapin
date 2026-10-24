import { describe, expect, it } from 'vitest';
import { actualScale, boundsOf, framing, middleDouble, popHistory, pushHistory, recall, remember, wheelPans, wheelPixels } from './zoom';

describe('zoom', () => {
  it('frames bounds in a box, padded and clamped', () => {
    const v = framing([0, 0, 10, 5], { w: 1000, h: 1000 }, 0.8);
    expect(v).toEqual({ cx: 5, cy: 2.5, scale: 80 });
    expect(framing([0, 0, 0.001, 0.001], { w: 100, h: 100 }, 1, 2, 400).scale).toBe(400);
  });

  it('knows paper size: a plan at 1:100 shows 10 mm a metre, 37.8 px on a 96 dpi screen', () => {
    expect(actualScale(100)).toBeCloseTo(37.8, 1);
    expect(actualScale(50)).toBeCloseTo(75.6, 1);
  });

  it('zooms on a mouse wheel, also the fractional one Chrome reports on macOS; pans sideways or with Shift', () => {
    expect(wheelPans({ deltaX: 0, deltaY: 4.000244140625, ctrlKey: false, shiftKey: false })).toBe(false);
    expect(wheelPans({ deltaX: 0, deltaY: -120, ctrlKey: false, shiftKey: false })).toBe(false);
    expect(wheelPans({ deltaX: 0, deltaY: 2.5, ctrlKey: true, shiftKey: false })).toBe(false);
    expect(wheelPans({ deltaX: 12, deltaY: 1, ctrlKey: false, shiftKey: false })).toBe(true);
    expect(wheelPans({ deltaX: 0, deltaY: 30, ctrlKey: false, shiftKey: true })).toBe(true);
    expect(wheelPans({ deltaX: 1, deltaY: 30, ctrlKey: false, shiftKey: false })).toBe(false);
  });

  it('takes a wheel in pixels, lines or pages', () => {
    expect(wheelPixels({ deltaY: 3, deltaMode: 0 }, 500)).toBe(3);
    expect(wheelPixels({ deltaY: 3, deltaMode: 1 }, 500)).toBe(48);
    expect(wheelPixels({ deltaY: 1, deltaMode: 2 }, 500)).toBe(500);
  });

  it('bounds points, none for no points', () => {
    expect(boundsOf([{ x: 1, y: 5 }, { x: -2, y: 3 }])).toEqual([-2, 3, 1, 5]);
    expect(boundsOf([])).toBeNull();
  });

  it('remembers a view per key and keeps ten zooms to go back to', () => {
    remember('plan:a', { cx: 1, cy: 2, scale: 3 });
    expect(recall('plan:a')).toEqual({ cx: 1, cy: 2, scale: 3 });
    expect(recall('plan:b')).toBeUndefined();
    for (let i = 0; i < 12; i++) pushHistory('k', i);
    expect(popHistory('k')).toBe(11);
    let n = 0;
    while (popHistory('k') !== undefined) n++;
    expect(n).toBe(9);
  });

  it('sees a middle button pressed twice quickly, not a left one', () => {
    const twice = middleDouble();
    expect(twice({ button: 1 })).toBe(false);
    expect(twice({ button: 1 })).toBe(true);
    expect(twice({ button: 1 })).toBe(false);
    expect(twice({ button: 0 })).toBe(false);
  });
});
