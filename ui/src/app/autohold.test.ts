/** Autohold: a drag that goes on without the button, and the Tab rule that keeps it from colliding with the live box's Tab (issue 86). */

import { afterEach, describe, expect, it } from 'vitest';
import { autoholdOn, autoholdSet, autoholdTab, clickEnds, enterEffect, isDragOwner, releaseEnds, tabMeaning } from './autohold';
import { keyIntent, type KeyPress, type KeySnapshot } from './keys';
import { resolveKeymap } from './shortcuts';
import { liveAcceptAtCursor, liveBegin, liveCursor, liveEnd, useLive } from '../viewport/liveInput';

const KEYMAP = resolveKeymap([], {}, []);
const press = (key: string, more: Partial<KeyPress> = {}): KeyPress => ({ key, ctrlKey: false, metaKey: false, shiftKey: false, inField: false, inDialog: false, ...more });
const state = (live: NonNullable<KeySnapshot['live']>): KeySnapshot => ({ screen: 'workspace', dialog: null, tool: null, selected: [], buffer: '', step: null, hasLast: false, hotbar: 5, keymap: KEYMAP, live });
const live = (owner: string | null, autohold: boolean, typed = false) => ({ active: !!owner, owner, autohold, text: '', typed });

afterEach(() => { liveEnd(); autoholdSet(false); });

describe('the autohold rules', () => {
  it('names the drags: the selection\'s body and a grip, not a tool drawing a segment', () => {
    expect(isDragOwner('drag')).toBe(true);
    expect(isDragOwner('grip')).toBe(true);
    expect(isDragOwner('tool')).toBe(false);
    expect(isDragOwner('cad')).toBe(false);
    expect(isDragOwner(null)).toBe(false);
  });

  it('Tab is autohold at the start of a drag, then the box\'s next field; everywhere else the box\'s', () => {
    expect(tabMeaning('drag', false)).toBe('autohold');
    expect(tabMeaning('grip', false)).toBe('autohold');
    expect(tabMeaning('drag', true)).toBe('field');
    expect(tabMeaning('grip', true)).toBe('field');
    expect(tabMeaning('tool', false)).toBe('field');
    expect(tabMeaning('cad', false)).toBe('field');
    expect(tabMeaning(null, false)).toBe('field');
  });

  it('the button\'s release ends a drag unless autohold holds it; a click ends only a held one', () => {
    expect(releaseEnds(false)).toBe(true);
    expect(releaseEnds(true)).toBe(false);
    expect(clickEnds(true)).toBe(true);
    expect(clickEnds(false)).toBe(false);
  });

  it('Enter drops at the typed values, or, held with nothing typed, at the pointer', () => {
    expect(enterEffect(false, true)).toBe('typed');
    expect(enterEffect(true, true)).toBe('typed');
    expect(enterEffect(true, false)).toBe('pointer');
    expect(enterEffect(false, false)).toBe('none');
  });
});

describe('Tab in the key router', () => {
  it('during a drag the first Tab is autohold, the next ones go through the fields', () => {
    expect(keyIntent(press('Tab'), state(live('drag', false)))).toEqual({ kind: 'autohold' });
    expect(keyIntent(press('Tab'), state(live('grip', false)))).toEqual({ kind: 'autohold' });
    expect(keyIntent(press('Tab'), state(live('drag', true)))).toEqual({ kind: 'liveFocus' });
  });

  it('a tool drawing a segment keeps Tab as the next field (no collision)', () => {
    expect(keyIntent(press('Tab'), state(live('tool', false)))).toEqual({ kind: 'liveFocus' });
    expect(keyIntent(press('Tab'), state(live('cad', false)))).toEqual({ kind: 'liveFocus' });
  });

  it('Enter in a held drag with nothing typed accepts at the pointer; typed values commit as before; an ordinary drag does nothing new', () => {
    expect(keyIntent(press('Enter'), state(live('drag', true, false)))).toEqual({ kind: 'liveAccept' });
    expect(keyIntent(press('Enter'), state(live('drag', true, true)))).toEqual({ kind: 'liveCommit' });
    expect(keyIntent(press('Enter'), state(live('drag', false, true)))).toEqual({ kind: 'liveCommit' });
    expect(keyIntent(press('Enter'), state(live('drag', false, false)))).toBeNull();
  });
});

describe('the autohold state', () => {
  it('Tab switches it on only while a drag owns the box, and the end of the drag switches it off', () => {
    expect(autoholdTab()).toBe(false);
    liveBegin('tool', { x: 0, y: 0 }, () => undefined);
    expect(autoholdTab()).toBe(false);
    expect(autoholdOn()).toBe(false);
    liveEnd();
    liveBegin('drag', { x: 0, y: 0 }, () => undefined, { delta: true });
    expect(autoholdTab()).toBe(true);
    expect(autoholdOn()).toBe(true);
    // The second Tab is no longer autohold's.
    expect(autoholdTab()).toBe(false);
    expect(autoholdOn()).toBe(true);
    liveEnd('drag');
    expect(autoholdOn()).toBe(false);
  });

  it('a drag that changes hands lets autohold go', () => {
    liveBegin('grip', { x: 0, y: 0 }, () => undefined);
    autoholdTab();
    expect(autoholdOn()).toBe(true);
    liveEnd('grip');
    liveBegin('tool', { x: 0, y: 0 }, () => undefined);
    expect(autoholdOn()).toBe(false);
  });

  it('accepting at the pointer hands the cursor\'s point to the drag, or the typed values\' when there are some', () => {
    const got: { x: number; y: number }[] = [];
    liveBegin('drag', { x: 1, y: 1 }, (p) => got.push(p), { delta: true });
    liveCursor({ x: 4, y: 5 });
    expect(liveAcceptAtCursor()).toBe(true);
    expect(got).toEqual([{ x: 4, y: 5 }]);
    useLive.setState({ len: '2000', ang: '0' });
    liveAcceptAtCursor();
    expect(got[1].x).toBeCloseTo(3, 9);
    expect(got[1].y).toBeCloseTo(1, 9);
    liveEnd();
    expect(liveAcceptAtCursor()).toBe(false);
  });
});
