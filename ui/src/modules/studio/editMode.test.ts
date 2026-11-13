import { beforeEach, describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { useScope } from '../../platform/scope';
import { useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { editedPartId, editPart, finishPart, isEditable, partScope, selectableIds, tabIntent, type TabState } from './editMode';
import { paramRows, planeCorners, sketchRows, stepRows, type PartTree } from './partTreeModel';

const node = (id: string, props: Record<string, unknown> = {}): NodeDto => ({ id, type: 'IfcBuildingElementProxy', name: `Name ${id}`, props } as unknown as NodeDto);
const state = (over: Partial<TabState> = {}): TabState => ({
  editing: null, otherScope: false, selected: ['a'], selectedNode: node('a', { operation: 'part' }), tool: null, view: '3d', dialog: null, inField: false, ...over,
});

describe('the part edit mode', () => {
  beforeEach(() => {
    useScope.setState({ scope: null });
    useUi.setState({ selection: null, selected: [], tool: null, view: '3d' });
    useSession.setState({ nodes: new Map([['a', node('a', { operation: 'extrude' })], ['b', node('b', { operation: 'part' })], ['w', node('w')]]) });
  });

  it('opens only a Studio solid or a placed part', () => {
    expect(isEditable(node('a', { operation: 'part' }))).toBe(true);
    expect(isEditable(node('a', { operation: 'extrude' }))).toBe(true);
    expect(isEditable(node('w'))).toBe(false);
    expect(isEditable(null)).toBe(false);
  });

  it('enters on one part, isolates it and leaves again', async () => {
    expect(editPart('a')).toBe(true);
    expect(editedPartId()).toBe('a');
    expect(useUi.getState().selected).toEqual(['a']);
    await finishPart();
    expect(editedPartId()).toBeNull();
    expect(useScope.getState().scope).toBeNull();
  });

  it('refuses a plain element, and another edit scope', () => {
    expect(editPart('w')).toBe(false);
    expect(editedPartId()).toBeNull();
    useScope.setState({ scope: { id: 'mep:1', label: 'Heating', includes: () => true } });
    expect(editPart('a')).toBe(false);
    expect(editedPartId()).toBeNull();
  });

  it('lets only the part be selected, the rest staying out of reach', () => {
    const scope = partScope(node('a', { operation: 'part' }));
    expect(scope.includes(node('a'))).toBe(true);
    expect(scope.includes(node('b'))).toBe(false);
    expect(selectableIds(['a', 'b', 'w'], 'a')).toEqual(['a']);
    expect(selectableIds(['a', 'b', 'w'], null)).toEqual(['a', 'b', 'w']);
  });

  it('is not an edit mode of the part for another kind of scope', () => {
    expect(editedPartId({ id: 'mep:1', label: 'x', includes: () => true })).toBeNull();
  });
});

describe('Tab', () => {
  it('enters on one selected part in 3D', () => expect(tabIntent(state())).toBe('enter'));
  it('leaves when the mode is on', () => expect(tabIntent(state({ editing: 'a' }))).toBe('leave'));
  it('leaves Tab to the panels with nothing, several or a plain element selected', () => {
    expect(tabIntent(state({ selected: [], selectedNode: undefined }))).toBeNull();
    expect(tabIntent(state({ selected: ['a', 'b'] }))).toBeNull();
    expect(tabIntent(state({ selectedNode: node('w') }))).toBeNull();
  });
  it('does not steal Tab from a field, a dialog, a tool or another view', () => {
    expect(tabIntent(state({ inField: true }))).toBeNull();
    expect(tabIntent(state({ inField: true, editing: 'a' }))).toBeNull();
    expect(tabIntent(state({ dialog: 'families' }))).toBeNull();
    expect(tabIntent(state({ tool: 'wall' }))).toBeNull();
    expect(tabIntent(state({ editing: 'a', tool: 'studio.pushPull' }))).toBeNull();
    expect(tabIntent(state({ view: 'plan' }))).toBeNull();
    expect(tabIntent(state({ otherScope: true }))).toBeNull();
  });
});

const tree = (over: Partial<PartTree> = {}): PartTree => ({
  id: 'a', name: 'Table', kind: 'recipe', planes: ['xy', 'xz', 'yz'], sketches: [], steps: [], params: [], ...over,
});

describe("the part's tree as rows", () => {
  it('opens a recipe sketch on its view and a formula body in the editor', () => {
    const r = sketchRows(tree({ sketches: [{ id: 's1', role: 'profile', viewId: 'plan:1', plane: 'plan', shapeIds: ['d1'] }, { id: 's2', role: 'guide', plane: 'plan', shapeIds: [], missing: true }] }));
    expect(r.map((x) => x.opens)).toEqual(['view', 'none']);
    expect(r[1].missing).toBe(true);
    const f = sketchRows(tree({ kind: 'formula', sketches: [{ id: 'body:1', name: 'Top', plane: 'xy', shapeIds: [], points: [] }] }));
    expect(f[0]).toMatchObject({ label: 'Top', opens: 'body' });
  });

  it('lists the steps in order with their moves only among the features', () => {
    const r = stepRows(tree({ steps: [
      { index: 0, kind: 'extrude', label: 'Extrude' },
      { index: 1, kind: 'fillet', label: 'Fillet', feature: 0 },
      { index: 2, kind: 'shell', label: 'Shell', feature: 1, suppressed: true },
    ] }));
    expect(r[0]).toMatchObject({ feature: null, canUp: false, canDown: false, canRemove: false });
    expect(r[1]).toMatchObject({ feature: 0, canUp: false, canDown: true, canRemove: true });
    expect(r[2]).toMatchObject({ feature: 1, canUp: true, canDown: false, suppressed: true });
  });

  it("shows a body's condition and greys it when it is off", () => {
    const r = stepRows(tree({ kind: 'formula', steps: [{ index: 0, kind: 'extrude', label: 'Shelf', sketch: 'body:6', when: 'HasShelf', suppressed: true }, { index: 1, kind: 'extrude', label: 'Top', sketch: 'body:1' }] }));
    expect(r[0]).toMatchObject({ when: 'HasShelf', suppressed: true, canToggle: false });
    expect(r[1].when).toBeNull();
  });

  it("marks the parameters that differ from the part's own and keeps their flags", () => {
    const r = paramRows(tree({ params: [
      { key: 'W', label: 'Width', kind: 'length', default: 1.2, value: 1.6, instance: true, published: true },
      { key: 'A', label: 'Angle', kind: 'angle', default: 30, value: 30, instance: false, published: false, group: 'Roof' },
    ] }));
    expect(r[0]).toMatchObject({ changed: true, instance: true, unit: '' });
    expect(r[1]).toMatchObject({ changed: false, unit: '°', group: 'Roof', published: false });
  });

  it("keeps a text parameter's value a string and compares texts exactly", () => {
    const r = paramRows(tree({ params: [
      { key: 'T', label: 'Label', kind: 'text', default: 'oak', value: 'pine', instance: true, published: true },
      { key: 'U', label: 'Same', kind: 'text', default: 'oak', value: 'oak', instance: true, published: true },
    ] }));
    expect(r[0]).toMatchObject({ value: 'pine', default: 'oak', changed: true });
    expect(r[1].changed).toBe(false);
  });

  it("draws an origin plane as a square in that plane", () => {
    expect(planeCorners('xy', 1).every((p) => p[2] === 0)).toBe(true);
    expect(planeCorners('xz', 1).every((p) => p[1] === 0)).toBe(true);
    expect(planeCorners('yz', 1).every((p) => p[0] === 0)).toBe(true);
    expect(planeCorners('xz', 2)[2]).toEqual([2, 0, 2]);
  });
});
