import { afterEach, describe, expect, it, vi } from 'vitest';
import { cancelsWheel, installPageGuards, pinchDelta, ZOOM_SURFACE } from './pageGuards';
import { wheelZoom } from '../viewport/zoom';

/** A target that is, or is not, inside a view. */
const inside = (surface: boolean) => ({ closest: (sel: string) => (sel === ZOOM_SURFACE && surface ? {} : null) }) as unknown as EventTarget;

describe('the page under the views (report #25)', () => {
  it('cancels the wheel over a view and every pinch, and leaves the panels their scroll', () => {
    expect(cancelsWheel({ ctrlKey: false, target: inside(true) })).toBe(true);
    expect(cancelsWheel({ ctrlKey: true, target: inside(false) })).toBe(true);
    expect(cancelsWheel({ ctrlKey: false, target: inside(false) })).toBe(false);
    expect(cancelsWheel({ ctrlKey: false, target: null })).toBe(false);
  });

  it('turns a pinch into the Ctrl-wheel the other browsers send, which zooms the view as far as the fingers', () => {
    expect(pinchDelta(1, 1) === 0).toBe(true);
    expect(pinchDelta(1, 0)).toBe(0);
    // Fingers apart (×1.1): a negative delta, zooming in by 1.1.
    const d = pinchDelta(1, 1.1);
    expect(d).toBeLessThan(0);
    expect(wheelZoom({ deltaY: d, deltaMode: 0, ctrlKey: true }, 800)).toBeCloseTo(1.1, 9);
  });

  it('zooms in proportion to the wheel: small trackpad steps a little, a notch one step', () => {
    const small = wheelZoom({ deltaY: 2, deltaMode: 0, ctrlKey: false }, 800);
    const notch = wheelZoom({ deltaY: 100, deltaMode: 0, ctrlKey: false }, 800);
    expect(small).toBeLessThan(1);
    expect(small).toBeGreaterThan(0.99);
    expect(notch).toBeCloseTo(Math.exp(-0.15), 9);
    // Lines (Firefox's mouse wheel) count as 16 px each.
    expect(wheelZoom({ deltaY: 3, deltaMode: 1, ctrlKey: false }, 800)).toBeCloseTo(Math.exp(-48 * 0.0015), 9);
    // Ctrl with a mouse wheel: held to a step a notch.
    expect(wheelZoom({ deltaY: 120, deltaMode: 0, ctrlKey: true }, 800)).toBeCloseTo(Math.exp(-0.25), 9);
  });
});

describe('Safari gestures', () => {
  const real = (globalThis as { WheelEvent?: unknown }).WheelEvent;
  afterEach(() => { (globalThis as { WheelEvent?: unknown }).WheelEvent = real; });

  it('are kept from zooming the page and zoom the view under the fingers', () => {
    class FakeWheel extends Event {
      ctrlKey: boolean; deltaY: number;
      constructor(type: string, init: { ctrlKey: boolean; deltaY: number; bubbles: boolean; cancelable: boolean }) { super(type, init); this.ctrlKey = init.ctrlKey; this.deltaY = init.deltaY; }
    }
    (globalThis as { WheelEvent?: unknown }).WheelEvent = FakeWheel;
    const view = Object.assign(new EventTarget(), { closest: () => view });
    const win = new EventTarget();
    const doc = Object.assign(new EventTarget(), { defaultView: win, elementFromPoint: () => view }) as unknown as Document;
    const wheels: number[] = [];
    view.addEventListener('wheel', (e) => wheels.push((e as FakeWheel).deltaY));
    const stop = installPageGuards(doc);

    const gesture = (type: string, scale: number) => {
      const e = Object.assign(new Event(type, { cancelable: true }), { scale, clientX: 10, clientY: 20 });
      doc.dispatchEvent(e);
      return e;
    };
    expect(gesture('gesturestart', 1).defaultPrevented).toBe(true);
    expect(gesture('gesturechange', 1.2).defaultPrevented).toBe(true);
    gesture('gestureend', 1.2);
    expect(wheels).toHaveLength(1);
    expect(wheels[0]).toBeCloseTo(-100 * Math.log(1.2), 9);

    stop();
    gesture('gesturestart', 1);
    gesture('gesturechange', 2);
    expect(wheels).toHaveLength(1);
  });

  it('do not zoom twice when the browser also sends the pinch as a Ctrl-wheel', () => {
    (globalThis as { WheelEvent?: unknown }).WheelEvent = class extends Event {};
    const view = Object.assign(new EventTarget(), { closest: () => view });
    const win = new EventTarget();
    const doc = Object.assign(new EventTarget(), { defaultView: win, elementFromPoint: () => view }) as unknown as Document;
    const seen = vi.fn();
    view.addEventListener('wheel', seen);
    const stop = installPageGuards(doc);
    // A trusted Ctrl-wheel cannot be made here: stand in for one.
    const ctrl = Object.assign(new Event('wheel', { cancelable: true }), { ctrlKey: true });
    Object.defineProperty(ctrl, 'isTrusted', { value: true });
    win.dispatchEvent(ctrl);
    expect(ctrl.defaultPrevented).toBe(true);
    doc.dispatchEvent(Object.assign(new Event('gesturestart', { cancelable: true }), { scale: 1, clientX: 0, clientY: 0 }));
    doc.dispatchEvent(Object.assign(new Event('gesturechange', { cancelable: true }), { scale: 1.5, clientX: 0, clientY: 0 }));
    expect(seen).not.toHaveBeenCalled();
    stop();
  });
});
