import { beforeEach, describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { useSession } from '../engine/session';
import { useUi } from '../app/store';
import { useCad } from '../viewport/cad/session';
import { useTemporary } from '../viewport/temporary';
import type { RegionTest } from '../viewport/selectBox';
import { selectAll, selectAllIds } from './selectAll';

const node = (id: string, type: string, storeyId: string | null = 's', editable = true): NodeDto =>
  ({ id, type, name: id, parentId: storeyId, px: 0, py: 0, props: {}, editable, module: null, storeyId });

beforeEach(() => {
  const nodes = new Map([node('s', 'IfcBuildingStorey', null), node('a', 'IfcWall'), node('b', 'IfcSlab'), node('t', 'IfcWallType', null), node('ro', 'IfcWall', 's', false)].map((n) => [n.id, n]));
  useSession.setState({ nodes, relations: new Map() });
  useTemporary.setState({ hidden: new Set(), isolated: null });
  useUi.setState({ selected: [], selection: null });
  useCad.setState({ host: null });
});

describe('Select all (forum topic 130)', () => {
  it('in a plan, takes whatever the canvas draws, by the selection window\'s rule', () => {
    const drawn: Record<string, [{ x: number; y: number }, { x: number; y: number }][]> = { a: [[{ x: 0, y: 0 }, { x: 1, y: 0 }]], b: [[{ x: 0, y: 1 }, { x: 1, y: 1 }]], ghost: [] };
    const inRegion = (takes: RegionTest) => Object.keys(drawn).filter((id) => takes(drawn[id]));
    useUi.setState({ view: 'plan' });
    useCad.setState({ host: { inRegion } as never });
    selectAll();
    expect(useUi.getState().selected).toEqual(['a', 'b']);
    expect(useUi.getState().selection).toBe('b');
  });

  it('in 3D, takes the editable elements shown, not those hidden or outside an isolation', () => {
    useUi.setState({ view: '3d' });
    expect(selectAllIds()).toEqual(['a', 'b']);
    useTemporary.setState({ hidden: new Set(['b']), isolated: null });
    expect(selectAllIds()).toEqual(['a']);
    useTemporary.setState({ hidden: new Set(), isolated: new Set(['b']) });
    expect(selectAllIds()).toEqual(['b']);
  });

  it('leaves the selection alone when the view shows nothing', () => {
    useUi.setState({ view: 'plan', selected: ['a'], selection: 'a' });
    selectAll();
    expect(useUi.getState().selected).toEqual(['a']);
  });
});
