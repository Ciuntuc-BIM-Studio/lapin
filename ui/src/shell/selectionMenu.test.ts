import { beforeEach, describe, expect, it } from 'vitest';
import { useUi } from '../app/store';
import { useTemporary } from '../viewport/temporary';
import { selectSimilarHere, similarItems, temporaryItems } from './selectionMenu';
import { useSession } from '../engine/session';
import type { NodeDto } from '../engine/protocol';

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

describe('Select similar and Select all with the same… (forum topics 73, 250)', () => {
  const node = (id: string, type: string): NodeDto => ({ id, type, name: id, parentId: 's', px: 0, py: 0, props: {}, editable: true, module: null, storeyId: 's' } as NodeDto);
  beforeEach(() => {
    const nodes = new Map([node('w1', 'IfcWall'), node('w2', 'IfcWall'), node('c1', 'IfcColumn')].map((n) => [n.id, n]));
    useSession.setState({ nodes, relations: new Map() });
    useUi.setState({ view: '3d', selected: ['w1'], selection: 'w1' });
    useTemporary.setState({ hidden: new Set(), isolated: null });
  });

  it('offers Select similar in this view with its keys, and its class alone under Select all with the same…', () => {
    const items = similarItems();
    expect(items[0].label).toBe('Select similar in this view');
    expect(items[0].shortcut).toMatch(/A$/);
    const same = items.find((i) => i.label === 'Select all with the same…')!;
    expect(same.submenu![0]).toMatchObject({ label: 'Same class, in this view', hint: 'IfcWall' });
    same.submenu![0].onSelect();
    expect(useUi.getState().selected).toEqual(['w1', 'w2']);
  });

  it('Select similar (Ctrl+Shift+A) takes the class and type shown, and says so with nothing selected', () => {
    selectSimilarHere();
    expect(useUi.getState().selected).toEqual(['w1', 'w2']);
    useUi.setState({ selected: [], notice: null });
    selectSimilarHere();
    expect(useUi.getState().notice).toMatch(/^Select an element first/);
  });
});
