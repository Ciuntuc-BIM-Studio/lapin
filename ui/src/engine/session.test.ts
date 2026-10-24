/** Folding the engine's diffs into the session, with the socket client mocked. */

import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Diff, NodeDto, RelationDto, RenderItem } from './protocol';

vi.mock('./client', () => ({
  EngineClient: class { onDiff() {} onStatus() {} start() {} call() { return Promise.resolve(null); } },
  engineUrl: () => Promise.resolve('ws://test'),
  LONG_TIMEOUT_MS: 1,
}));

import { applyDiff, useSession } from './session';
import { useUi } from '../app/store';

const node = (id: string, type = 'IfcWall', name = id): NodeDto => ({ id, type, name, parentId: null, px: 0, py: 0 } as unknown as NodeDto);
const rel = (id: string): RelationDto => ({ id, class: 'IfcRelAggregates', relating: 'a', related: ['b'] });
const item = (nodeId: string): RenderItem => ({ nodeId } as unknown as RenderItem);
const diff = (d: Partial<Diff>): Diff => ({
  version: 1, reset: false, nodes: [], removedNodes: [], render: [], renderRemoved: [], relations: [], removedRelations: [],
  state: { canUndo: false, canRedo: false, undoLabel: null, redoLabel: null, dirty: false, path: null, nodeCount: 0, edgeCount: 0 },
  ...d,
});
const ids = (m: Map<string, unknown>) => [...m.keys()].sort();

beforeEach(() => {
  useSession.setState({ nodes: new Map(), relations: new Map(), render: new Map(), doc: null, version: -1 });
  useUi.setState({ selection: null, selected: [], storeyId: null, wallTypeId: null, slabTypeId: null, doorTypeId: null, windowTypeId: null });
});

describe('applyDiff', () => {
  it('a reset replaces everything; an incremental diff adds, replaces and removes', () => {
    applyDiff(diff({ version: 1, reset: true, nodes: [node('a'), node('b')], relations: [rel('r1')], render: [item('a'), item('b')] }));
    expect(ids(useSession.getState().nodes)).toEqual(['a', 'b']);
    applyDiff(diff({ version: 2, nodes: [node('b', 'IfcWall', 'renamed'), node('c')], removedNodes: ['a'], removedRelations: ['r1'], relations: [rel('r2')], render: [item('c')], renderRemoved: ['a'] }));
    const s = useSession.getState();
    expect(ids(s.nodes)).toEqual(['b', 'c']);
    expect(s.nodes.get('b')!.name).toBe('renamed');
    expect(ids(s.relations)).toEqual(['r2']);
    expect(ids(s.render)).toEqual(['b', 'c']);
    expect(s.version).toBe(2);
    applyDiff(diff({ version: 3, reset: true, nodes: [node('z')] }));
    expect(ids(useSession.getState().nodes)).toEqual(['z']);
    expect(useSession.getState().render.size).toBe(0);
  });

  it('makes new maps but keeps the nodes it does not touch as they were', () => {
    applyDiff(diff({ version: 1, reset: true, nodes: [node('a'), node('b')] }));
    const before = useSession.getState().nodes;
    applyDiff(diff({ version: 2, nodes: [node('b', 'IfcWall', 'x')] }));
    const after = useSession.getState().nodes;
    expect(after).not.toBe(before);
    expect(after.get('a')).toBe(before.get('a'));
  });

  it('skips a diff no newer than what is shown, but never a reset', () => {
    applyDiff(diff({ version: 5, reset: true, nodes: [node('a')] }));
    applyDiff(diff({ version: 5, nodes: [node('late')] }));
    applyDiff(diff({ version: 3, removedNodes: ['a'] }));
    expect(ids(useSession.getState().nodes)).toEqual(['a']);
    expect(useSession.getState().version).toBe(5);
    applyDiff(diff({ version: 2, reset: true, nodes: [node('b')] }));
    expect(ids(useSession.getState().nodes)).toEqual(['b']);
    expect(useSession.getState().version).toBe(2);
  });

  it('prunes a selection the edit took away, keeping a grid axis while its grid lives', () => {
    applyDiff(diff({ version: 1, reset: true, nodes: [node('a'), node('b'), node('g', 'IfcGrid')] }));
    useUi.setState({ selection: 'a', selected: ['a', 'b', 'g#A', 'h#1'] });
    applyDiff(diff({ version: 2, removedNodes: ['a'] }));
    expect(useUi.getState().selection).toBeNull();
    expect(useUi.getState().selected).toEqual(['b', 'g#A']);
    useUi.setState({ selection: 'g#A' });
    applyDiff(diff({ version: 3, nodes: [node('c')] }));
    expect(useUi.getState().selection).toBe('g#A');
    applyDiff(diff({ version: 4, removedNodes: ['g'] }));
    expect(useUi.getState().selection).toBeNull();
    expect(useUi.getState().selected).toEqual(['b']);
  });

  it('forgets a storey and the tools\' types that went', () => {
    applyDiff(diff({ version: 1, reset: true, nodes: [node('s', 'IfcBuildingStorey'), node('wt', 'IfcWallType'), node('st', 'IfcSlabType'), node('dt', 'IfcDoorType'), node('wn', 'IfcWindowType')] }));
    useUi.setState({ storeyId: 's', wallTypeId: 'wt', slabTypeId: 'st', doorTypeId: 'dt', windowTypeId: 'wn' });
    applyDiff(diff({ version: 2, removedNodes: ['wt', 'dt'] }));
    let ui = useUi.getState();
    expect([ui.storeyId, ui.wallTypeId, ui.slabTypeId, ui.doorTypeId, ui.windowTypeId]).toEqual(['s', null, 'st', null, 'wn']);
    applyDiff(diff({ version: 3, removedNodes: ['s', 'st', 'wn'] }));
    ui = useUi.getState();
    expect([ui.storeyId, ui.slabTypeId, ui.windowTypeId]).toEqual([null, null, null]);
  });
});
