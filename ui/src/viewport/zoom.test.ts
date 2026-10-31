import { describe, expect, it } from 'vitest';
import { actualScale, boundsOf, framing, middleDouble, popHistory, pushHistory, recall, remember, clampWheel, wheelGesture, wheelPixels, wheelZoom, WHEEL_STEP } from './zoom';

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
    const w = (deltaX: number, deltaY: number, ctrlKey = false, shiftKey = false) => ({ deltaX, deltaY, deltaMode: 0, ctrlKey, shiftKey });
    expect(wheelGesture()(w(0, 4.000244140625), 0)).toBe('zoom');
    expect(wheelGesture()(w(0, -120), 0)).toBe('zoom');
    expect(wheelGesture()(w(0, 2.5, true), 0)).toBe('zoom');
    expect(wheelGesture()(w(12, 1), 0)).toBe('pan');
    expect(wheelGesture()(w(0, 30, false, true), 0)).toBe('pan');
    expect(wheelGesture()(w(1, 30), 0)).toBe('zoom');
  });

  it('decides pan or zoom once a run, on its first clear movement, so a Magic Mouse does not alternate (report #50)', () => {
    const w = (deltaX: number, deltaY: number) => ({ deltaX, deltaY, deltaMode: 0, ctrlKey: false, shiftKey: false });
    const g = wheelGesture(250);
    expect(g(w(1, 0), 0)).toBeNull();
    expect(g(w(0, 3), 10)).toBe('zoom');
    expect(g(w(9, 2), 20)).toBe('zoom');
    expect(g(w(0, -1), 100)).toBe('zoom');
    expect(g(w(9, 2), 400)).toBe('pan');
    expect(g(w(0, 40), 410)).toBe('pan');
    expect(g(w(0, 40), 420)).toBe('pan');
    expect(g({ ...w(0, 40), ctrlKey: true }, 430)).toBe('zoom');
    expect(g(w(5, 40), 440)).toBe('pan');
    expect(g({ deltaX: 0, deltaY: 1, deltaMode: 1, ctrlKey: false, shiftKey: false }, 1000)).toBe('zoom');
  });

  it('holds a wheel event to about a notch: an accelerated flick zooms in steps, not in one jump (report #50)', () => {
    const z = (deltaY: number, ctrlKey = false) => wheelZoom({ deltaY, deltaMode: 0, ctrlKey }, 800);
    expect(z(4)).toBeCloseTo(Math.exp(-0.006), 6);
    expect(z(120)).toBeCloseTo(Math.exp(-0.18), 6);
    expect(z(600)).toBeCloseTo(Math.exp(-WHEEL_STEP), 6);
    expect(z(-600)).toBeCloseTo(Math.exp(WHEEL_STEP), 6);
    expect(z(600, true)).toBeCloseTo(Math.exp(-0.25), 6);
    // Three's orbit controls read a shadowed deltaY, so 0.95^(px/100) is the same step. (A wheel event's deltas are getters on its prototype, as here.)
    class Wheel { constructor(private d: number, private m = 0, public ctrlKey = false) {} get deltaY() { return this.d; } get deltaMode() { return this.m; } currentTarget = null; }
    const wheel = (d: number, m = 0, ctrl = false) => new Wheel(d, m, ctrl) as unknown as WheelEvent;
    const big = wheel(2000);
    clampWheel(big);
    expect(Math.pow(0.95, big.deltaY / 100)).toBeCloseTo(Math.exp(-WHEEL_STEP), 6);
    const small = wheel(120);
    clampWheel(small);
    expect(small.deltaY).toBe(120);
    const pinch = wheel(2000, 0, true);
    clampWheel(pinch);
    expect(pinch.deltaY).toBe(2000);
    const lines = wheel(60, 1);
    clampWheel(lines);
    expect(lines.deltaMode).toBe(0);
    expect(lines.deltaY).toBeCloseTo(390.1, 0);
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
