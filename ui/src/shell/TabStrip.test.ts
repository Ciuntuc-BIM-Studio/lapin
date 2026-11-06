import { describe, expect, it } from 'vitest';
import { fitTabs, splitTabs } from './TabStrip';

describe('the tab strip', () => {
  it('keeps every tab when they fit, else as many as leave room for More', () => {
    expect(fitTabs([60, 80, 70], 300, 60)).toBe(3);
    expect(fitTabs([60, 80, 70, 90], 250, 60)).toBe(2);
    expect(fitTabs([200], 100, 60)).toBe(0);
  });

  it('keeps the current tab in sight, in the last place, when it would go under More', () => {
    const tabs = ['a', 'b', 'c', 'd', 'e'].map((id) => ({ id }));
    expect(splitTabs(tabs, 5, 'e')).toEqual({ shown: tabs, more: [] });
    const { shown, more } = splitTabs(tabs, 3, 'e');
    expect(shown.map((t) => t.id)).toEqual(['a', 'b', 'e']);
    expect(more.map((t) => t.id)).toEqual(['c', 'd']);
    expect(splitTabs(tabs, 3, 'b').shown.map((t) => t.id)).toEqual(['a', 'b', 'c']);
  });
});
