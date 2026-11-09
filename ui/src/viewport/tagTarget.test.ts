import { beforeEach, describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { useCad } from './cad/session';
import { tagTargets, trackTagHover, tagToolPicking, useTagHover, viewHasTags } from './tagTarget';

const node = (id: string, props: Record<string, unknown>): NodeDto => ({ id, name: id, type: 'IfcAnnotation', props } as unknown as NodeDto);
const nodes = new Map<string, NodeDto>([
  ['t1', node('t1', { predefinedType: 'TAG', element: 'wall1', viewId: 'plan:g' })],
  ['t2', node('t2', { predefinedType: 'TAG', element: 'door1', viewId: 'plan:g' })],
  ['d1', node('d1', { predefinedType: 'DIMENSION', viewId: 'plan:g' })],
]);

describe('what a tag tags, highlighted (report #130)', () => {
  beforeEach(() => { useTagHover.setState({ element: null, tag: null }); useCad.setState({ command: null, step: null }); });

  it('is the element of each selected tag, of the hovered tag, and the one the TAG command hovers', () => {
    expect([...tagTargets(['t1'], nodes, { element: null, tag: null })]).toEqual(['wall1']);
    expect([...tagTargets(['t1', 'd1'], nodes, { element: null, tag: 't2' })].sort()).toEqual(['door1', 'wall1']);
    expect([...tagTargets([], nodes, { element: 'slab1', tag: null })]).toEqual(['slab1']);
    expect(tagTargets(['d1', 'unknown'], nodes, { element: null, tag: null }).size).toBe(0);
  });

  it('follows the cursor: the element while TAG waits for one, else the tag under it', () => {
    const o = { pickElement: () => 'wall9', pickTag: () => 't2', anyTags: true, busy: false };
    trackTagHover({ x: 0, y: 0 }, o);
    expect(useTagHover.getState()).toEqual({ element: null, tag: 't2' });
    useCad.setState({ command: 'TAG', step: { want: 'select' } as never });
    expect(tagToolPicking()).toBe(true);
    trackTagHover({ x: 0, y: 0 }, o);
    expect(useTagHover.getState()).toEqual({ element: 'wall9', tag: null });
    // A sub-element id (a part's) is not an element to tag; leaving clears it.
    trackTagHover({ x: 0, y: 0 }, { ...o, pickElement: () => 'wall9#1' });
    expect(useTagHover.getState().element).toBeNull();
    useCad.setState({ command: null, step: null });
    trackTagHover(null, { ...o, busy: true });
    expect(useTagHover.getState()).toEqual({ element: null, tag: null });
  });

  it('searches a view for tags only when it has some', () => {
    expect(viewHasTags(nodes, 'plan:g')).toBe(true);
    expect(viewHasTags(nodes, 'plan:other')).toBe(false);
    expect(viewHasTags(nodes, null)).toBe(false);
  });
});
