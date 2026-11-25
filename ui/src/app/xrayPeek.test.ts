import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { beginXrayPeek, canStartXrayPeek, endXrayPeek, useXrayPeek, xrayPeekHandlers, XRAY_PEEK_HOLD_MS } from './xrayPeek';
import { setPrefs } from './prefs';
import { useUi } from './store';
import { useCad } from '../viewport/cad/session';

describe('Hold-to-Peek X-Ray', () => {
  beforeEach(() => {
    useXrayPeek.setState({ active: false, prev: null });
    useUi.setState({ screen: 'workspace', view: '3d', dialog: null, visualStyle: 'shaded' });
    useCad.setState({ buffer: '', step: null });
  });

  it('allows peeking only in 3D workspace when idle and not already in xray', () => {
    const base = { screen: 'workspace', view: '3d', dialog: null, visualStyle: 'shaded' as const, buffer: '', hasStep: false, liveActive: false, hasValueOwner: false, walking: false };
    expect(canStartXrayPeek(base)).toBe(true);
    expect(canStartXrayPeek({ ...base, buffer: 'x' })).toBe(true);
    expect(canStartXrayPeek({ ...base, buffer: 'wa' })).toBe(false);
    expect(canStartXrayPeek({ ...base, view: 'plan' })).toBe(false);
    expect(canStartXrayPeek({ ...base, dialog: 'palette' })).toBe(false);
    expect(canStartXrayPeek({ ...base, visualStyle: 'xray' })).toBe(false);
    expect(canStartXrayPeek({ ...base, walking: true })).toBe(false);
  });

  it('switches to xray, clears a lone x from command buffer, and restores the prior style on release', () => {
    useUi.setState({ visualStyle: 'edges' });
    useCad.setState({ buffer: 'x' });
    expect(beginXrayPeek()).toBe(true);
    expect(useUi.getState().visualStyle).toBe('xray');
    expect(useCad.getState().buffer).toBe('');
    expect(useXrayPeek.getState()).toEqual({ active: true, prev: 'edges' });

    endXrayPeek();
    expect(useUi.getState().visualStyle).toBe('edges');
    expect(useXrayPeek.getState()).toEqual({ active: false, prev: null });
  });

  afterEach(() => { vi.useRealTimers(); setPrefs({ xrayPeek: false }); });

  const key = (repeat = false) => ({ key: 'x', code: 'KeyX', repeat, ctrlKey: false, metaKey: false, shiftKey: false, altKey: false, target: null,
    preventDefault: vi.fn(), stopImmediatePropagation: vi.fn() });

  it('does nothing on a hold while the extra is off', () => {
    vi.useFakeTimers();
    setPrefs({ xrayPeek: false });
    const h = xrayPeekHandlers();
    h.onDown(key() as never);
    vi.advanceTimersByTime(XRAY_PEEK_HOLD_MS + 50);
    expect(useXrayPeek.getState().active).toBe(false);
    const rep = key(true);
    h.onDown(rep as never);
    expect(rep.preventDefault).not.toHaveBeenCalled();
    h.dispose();
  });

  it('swallows the autorepeat of a held X, so the command line keeps what it had', () => {
    vi.useFakeTimers();
    setPrefs({ xrayPeek: true });
    const h = xrayPeekHandlers();
    const first = key();
    h.onDown(first as never);
    expect(first.preventDefault).not.toHaveBeenCalled(); // a tap still reaches the shortcuts
    useCad.setState({ buffer: 'x' }); // what the first press typed
    const early = key(true);
    h.onDown(early as never);
    expect(early.stopImmediatePropagation).toHaveBeenCalled();
    vi.advanceTimersByTime(XRAY_PEEK_HOLD_MS + 10);
    expect(useXrayPeek.getState().active).toBe(true);
    expect(useCad.getState().buffer).toBe('');
    for (let i = 0; i < 5; i++) {
      const rep = key(true);
      h.onDown(rep as never);
      expect(rep.preventDefault).toHaveBeenCalled();
      expect(rep.stopImmediatePropagation).toHaveBeenCalled();
    }
    h.onUp(key() as never);
    expect(useXrayPeek.getState().active).toBe(false);
    expect(useCad.getState().buffer).toBe('');
    expect(useUi.getState().visualStyle).toBe('shaded');
    h.dispose();
  });
});
