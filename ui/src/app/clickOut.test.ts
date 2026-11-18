import { afterEach, describe, expect, it } from 'vitest';
import { applyClickOut, clickOutAction, type PressTarget } from './clickOut';
import { DEFAULT_PREFS, readPrefs, setPrefs } from './prefs';
import { liveBegin, liveEnd, useLive } from '../viewport/liveInput';
import { useCad } from '../viewport/cad/session';

const on = (...sel: string[]): PressTarget => ({ closest: (s: string) => (sel.some((x) => s.split(',').map((t) => t.trim()).includes(x)) ? {} : null) });
const none: PressTarget = on();
const base = { enabled: true, buffer: '', liveTyped: false, dialog: false };

describe('click outside an input box accepts it (forum 155)', () => {
  afterEach(() => { liveEnd(); useCad.setState({ buffer: '' }); setPrefs({ clickOutAccepts: false }); });

  it('is off by default and read as off when unreadable', () => {
    expect(DEFAULT_PREFS.clickOutAccepts).toBe(false);
    expect(readPrefs(null).clickOutAccepts).toBe(false);
    expect(readPrefs({ clickOutAccepts: 'yes' }).clickOutAccepts).toBe(false);
    expect(readPrefs({ clickOutAccepts: true }).clickOutAccepts).toBe(true);
  });

  it('does nothing while off, with nothing typed, in dialogs, or for other buttons', () => {
    expect(clickOutAction(0, none, { ...base, enabled: false, buffer: '2400' })).toBeNull();
    expect(clickOutAction(0, none, base)).toBeNull();
    expect(clickOutAction(0, none, { ...base, buffer: 'x', dialog: true })).toBeNull();
    expect(clickOutAction(1, none, { ...base, buffer: 'x' })).toBeNull();
  });

  it('leaves clicks on the boxes themselves alone (Tab and typing keep working)', () => {
    expect(clickOutAction(0, on('input'), { ...base, liveTyped: true })).toBeNull();
    expect(clickOutAction(0, on('.live-bar'), { ...base, liveTyped: true })).toBeNull();
    expect(clickOutAction(0, on('.cad-prompt'), { ...base, buffer: '12' })).toBeNull();
    expect(clickOutAction(2, on('.live-box'), { ...base, liveTyped: true })).toBeNull();
  });

  it('a left click accepts the command line text, and the live value outside a plan or drawing', () => {
    expect(clickOutAction(0, none, { ...base, buffer: '2400' })).toBe('submit');
    expect(clickOutAction(0, on('.plan-svg'), { ...base, buffer: '2400' })).toBe('submit');
    expect(clickOutAction(0, none, { ...base, liveTyped: true })).toBe('live');
    // In a plan the click itself takes the typed point (liveTaken), as without the setting.
    expect(clickOutAction(0, on('.plan-svg'), { ...base, liveTyped: true })).toBeNull();
  });

  it('a right click discards what is typed', () => {
    expect(clickOutAction(2, none, { ...base, buffer: '2400' })).toBe('discard');
    expect(clickOutAction(2, on('.plan-svg'), { ...base, liveTyped: true })).toBe('discard');
  });

  it('takes the typed live value as Enter does, once', () => {
    const taken: Array<{ x: number; y: number; typed?: number }> = [];
    liveBegin('tool', { x: 0, y: 0 }, (p, typed) => taken.push({ ...p, typed }));
    useLive.setState({ cursor: { x: 1, y: 0 }, len: '2400' });
    expect(applyClickOut('live')).toBe(true);
    expect(taken).toHaveLength(1);
    expect(taken[0].x).toBeCloseTo(2.4, 9);
    expect(taken[0].typed).toBeCloseTo(2.4, 9);
    expect(useLive.getState().len).toBe('');
    expect(applyClickOut('live')).toBe(false);          // nothing left to take
  });

  it('discarding clears the typed values and the command line text, and keeps the command', () => {
    liveBegin('tool', { x: 0, y: 0 }, () => {});
    useLive.setState({ len: '30', ang: '45' });
    useCad.setState({ buffer: 'abc', command: 'LINE' });
    expect(applyClickOut('discard')).toBe(true);
    expect(useLive.getState().len).toBe('');
    expect(useLive.getState().ang).toBe('');
    expect(useCad.getState().buffer).toBe('');
    expect(useCad.getState().command).toBe('LINE');
    expect(useLive.getState().owner).toBe('tool');
    useCad.setState({ command: null });
  });
});
