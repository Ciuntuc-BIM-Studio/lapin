import { beforeEach, describe, expect, it } from 'vitest';
import { useUi } from '../app/store';
import { useTemporary } from '../viewport/temporary';
import { temporaryItems } from './selectionMenu';

describe('the 3D right-click menu: temporary hide and isolate', () => {
  beforeEach(() => {
    useUi.setState({ selected: [] });
    useTemporary.setState({ hidden: new Set(), isolated: null });
  });
  it('offers nothing with nothing selected and nothing hidden', () => {
    expect(temporaryItems()).toEqual([]);
  });
  it('offers Hide (HH) and Isolate (HI) for a selection', () => {
    useUi.setState({ selected: ['w1'] });
    expect(temporaryItems().map((i) => [i.label, i.shortcut])).toEqual([['Hide', 'HH'], ['Isolate', 'HI']]);
  });
  it('offers Show all (HR) while something is hidden or isolated', () => {
    useTemporary.setState({ hidden: new Set(['w1']) });
    expect(temporaryItems().map((i) => [i.label, i.shortcut])).toEqual([['Show all', 'HR']]);
    useTemporary.setState({ hidden: new Set(), isolated: new Set(['w2']) });
    useUi.setState({ selected: ['w2'] });
    expect(temporaryItems().map((i) => i.label)).toEqual(['Hide', 'Isolate', 'Show all']);
  });
});
