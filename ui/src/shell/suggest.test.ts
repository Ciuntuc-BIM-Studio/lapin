import { describe, expect, it } from 'vitest';
import { accept, inBraces, keyOf, leadsOn, stepIndex } from './suggest';

describe('suggestions while typing', () => {
  it('are asked for only inside braces still open before the caret', () => {
    expect(inBraces('{= su', 5)).toBe(true);
    expect(inBraces('{el.name} m', 11)).toBe(false);
    expect(inBraces('{a} {= round(', 13)).toBe(true);
    expect(inBraces('{= su} m²', 4)).toBe(true);
    expect(inBraces('Area', 4)).toBe(false);
  });

  it('put an item in place of the word typed, the caret after it', () => {
    expect(accept('{= su} m²', { from: 3, to: 5 }, { insert: 'sum(', label: 'sum', kind: 'function' })).toEqual({ text: '{= sum(} m²', caret: 7 });
    expect(accept('{el.', { from: 1, to: 4 }, { insert: 'el.Area', label: 'Area', kind: 'field' })).toEqual({ text: '{el.Area', caret: 8 });
  });

  it('go on at once after a name ending in a dot', () => {
    expect(leadsOn({ insert: 'schedule.Rooms.', label: 'Rooms', kind: 'schedule' })).toBe(true);
    expect(leadsOn({ insert: 'sum(', label: 'sum', kind: 'function' })).toBe(false);
  });

  it('take the arrows, Enter, Tab and Esc, and leave Shift+Enter and the rest to the input', () => {
    expect(keyOf('ArrowDown', false)).toBe('down');
    expect(keyOf('ArrowUp', false)).toBe('up');
    expect(keyOf('Enter', false)).toBe('accept');
    expect(keyOf('Tab', false)).toBe('accept');
    expect(keyOf('Escape', false)).toBe('close');
    expect(keyOf('Enter', true)).toBeNull();
    expect(keyOf('a', false)).toBeNull();
    expect(stepIndex(2, 3, 1)).toBe(0);
    expect(stepIndex(0, 3, -1)).toBe(2);
  });
});
