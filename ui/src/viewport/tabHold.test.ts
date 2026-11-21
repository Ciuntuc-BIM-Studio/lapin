/** Holding Tab opens a list of what is under the pointer (issue 110): the model, then the timer and the keys over a fake view. */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { handleKeydown, type KeySnapshot } from '../app/keys';
import { resolveKeymap } from '../app/shortcuts';
import { setCycleSource, usePickCycle } from './pickCycle';
import {
  downVerdict, holdCancel, holdPhase, holdPointer, holdRefresh, holdRows, holdWheel, HOLD_MS, refreshList, tabHoldDown, tabHoldUp, upVerdict, useTabHold, wheelIndex,
} from './tabHold';

describe('the hold model', () => {
  it('holds for a quarter of a second', () => {
    expect(HOLD_MS).toBe(250);
  });

  it('starts a hold on a fresh Tab over two or more elements only; a repeat with nothing pending is the browser\'s', () => {
    expect(downVerdict('idle', false, 3)).toBe('swallow');
    expect(downVerdict('idle', false, 2)).toBe('swallow');
    expect(downVerdict('idle', false, 1)).toBe('pass');
    expect(downVerdict('idle', false, 0)).toBe('pass');
    expect(downVerdict('idle', true, 3)).toBe('pass');
    expect(downVerdict('pending', true, 3)).toBe('swallow');
    expect(downVerdict('open', true, 0)).toBe('swallow');
  });

  it('a tap before the threshold is a tap; after it the key takes what is lit', () => {
    expect(upVerdict('pending')).toBe('tap');
    expect(upVerdict('open')).toBe('take');
    expect(upVerdict('idle')).toBe('none');
  });

  it('the wheel goes down to the next and round both ways', () => {
    expect(wheelIndex(0, 3, 100)).toBe(1);
    expect(wheelIndex(2, 3, 100)).toBe(0);
    expect(wheelIndex(0, 3, -100)).toBe(2);
    expect(wheelIndex(1, 3, 0)).toBe(1);
    expect(wheelIndex(0, 0, 100)).toBe(0);
  });

  it('keeps the lit element lit as the pointer moves, else goes back to the first', () => {
    expect(refreshList({ ids: ['a', 'b', 'c'], index: 1 }, ['c', 'b'])).toEqual({ ids: ['c', 'b'], index: 1 });
    expect(refreshList({ ids: ['a', 'b', 'c'], index: 1 }, ['a', 'c'])).toEqual({ ids: ['a', 'c'], index: 0 });
    expect(refreshList({ ids: [], index: 0 }, ['x'])).toEqual({ ids: ['x'], index: 0 });
  });

  it('marks exactly one row current', () => {
    expect(holdRows(['a', 'b', 'c'], 1)).toEqual([{ id: 'a', current: false }, { id: 'b', current: true }, { id: 'c', current: false }]);
  });
});

describe('the hold over a view', () => {
  let under = ['w', 's', 'r'];
  let at = { x: 1, y: 1 };
  const taken: string[] = [];
  beforeEach(() => {
    vi.useFakeTimers();
    under = ['w', 's', 'r'];
    at = { x: 1, y: 1 };
    taken.length = 0;
    setCycleSource({ key: 'v', at: () => at, ids: () => under, tol: () => 0.1, take: () => taken.push(usePickCycle.getState().cycle ? usePickCycle.getState().cycle!.ids[usePickCycle.getState().cycle!.index] : '-') });
    holdPointer(100, 80);
  });
  afterEach(() => { holdCancel(); setCycleSource(null); vi.useRealTimers(); });

  it('a tap lets the Tab command run when the key comes up, and opens nothing', () => {
    expect(tabHoldDown(false)).toBe('swallow');
    expect(holdPhase()).toBe('pending');
    vi.advanceTimersByTime(HOLD_MS - 10);
    expect(useTabHold.getState().open).toBe(false);
    expect(tabHoldUp()).toBe('tap');
    expect(holdPhase()).toBe('idle');
    vi.advanceTimersByTime(1000);
    expect(useTabHold.getState().open).toBe(false);
    expect(taken).toEqual([]);
  });

  it('holding opens the list on the first element, the wheel moves it, releasing takes the current one', () => {
    tabHoldDown(false);
    vi.advanceTimersByTime(HOLD_MS + 1);
    expect(useTabHold.getState()).toMatchObject({ open: true, ids: ['w', 's', 'r'], index: 0 });
    expect(usePickCycle.getState().cycle?.index).toBe(0);
    expect(tabHoldDown(true)).toBe('swallow');
    expect(holdWheel(120)).toBe(true);
    expect(holdWheel(120)).toBe(true);
    expect(useTabHold.getState().index).toBe(2);
    expect(usePickCycle.getState().cycle?.index).toBe(2);
    expect(tabHoldUp()).toBe('take');
    expect(taken).toEqual(['r']);
    expect(useTabHold.getState().open).toBe(false);
    expect(holdWheel(120)).toBe(false);
  });

  it('the list follows the pointer and keeps what is lit while it is still there', () => {
    tabHoldDown(false);
    vi.advanceTimersByTime(HOLD_MS + 1);
    holdWheel(120);
    under = ['s', 'k'];
    at = { x: 2, y: 1 };
    holdRefresh();
    expect(useTabHold.getState()).toMatchObject({ ids: ['s', 'k'], index: 0 });
    under = ['k', 's'];
    holdRefresh();
    expect(useTabHold.getState()).toMatchObject({ ids: ['k', 's'], index: 1 });
  });

  it('with one element or none there is no hold: Tab keeps its other meanings', () => {
    under = ['w'];
    expect(tabHoldDown(false)).toBe('pass');
    expect(holdPhase()).toBe('idle');
    under = [];
    expect(tabHoldDown(false)).toBe('pass');
  });

  it('a pointer that left the elements before the threshold opens nothing', () => {
    tabHoldDown(false);
    under = ['w'];
    vi.advanceTimersByTime(HOLD_MS + 1);
    expect(useTabHold.getState().open).toBe(false);
    expect(holdPhase()).toBe('idle');
  });
});

describe('the Tab key in the router (issue 110)', () => {
  const KEYMAP = resolveKeymap([{ id: 'app.togglePanels', label: 't', category: 'x', defaults: ['Tab'], run: () => undefined }], { 'app.togglePanels': ['Tab'] });
  const snap = (): KeySnapshot => ({ screen: 'workspace', dialog: null, tool: null, selected: [], buffer: '', step: null, hasLast: false, hotbar: 5, keymap: KEYMAP });
  const down = (repeat = false) => {
    const acts: unknown[] = [];
    let prevented = false;
    handleKeydown({ key: 'Tab', code: 'Tab', ctrlKey: false, metaKey: false, shiftKey: false, altKey: false, repeat, defaultPrevented: false, target: null, preventDefault: () => { prevented = true; } } as never, snap, (i) => acts.push(i));
    return { acts, prevented };
  };
  beforeEach(() => { vi.useFakeTimers(); holdPointer(10, 10); });
  afterEach(() => { holdCancel(); setCycleSource(null); vi.useRealTimers(); });

  it('over several elements the keydown waits: no command runs until it is known whether it is a tap', () => {
    setCycleSource({ key: 'v', at: () => ({ x: 0, y: 0 }), ids: () => ['a', 'b'], tol: () => 1 });
    const r = down();
    expect(r.acts).toEqual([]);
    expect(r.prevented).toBe(true);
    expect(holdPhase()).toBe('pending');
  });

  it('over one element or none Tab runs at once, as before', () => {
    setCycleSource({ key: 'v', at: () => ({ x: 0, y: 0 }), ids: () => ['a'], tol: () => 1 });
    expect(down().acts).toEqual([{ kind: 'command', id: 'app.togglePanels' }]);
    expect(holdPhase()).toBe('idle');
  });
});
