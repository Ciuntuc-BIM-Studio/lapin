import { beforeEach, describe, expect, it } from 'vitest';
import type { NodeDto, RelationDto } from '../engine/protocol';
import { useSession } from '../engine/session';
import { useUi } from '../app/store';
import { setDisplay } from '../app/display';
import { useScope } from './scope';
import { openGroup, shownSelection } from './groups';
import { assemblyPath, drillEscape, drillInside, drillLevel, drillTarget, pickClick, pickRegion, useDrill } from './assemblies';

const node = (id: string, type: string, storeyId: string | null = 's'): NodeDto => ({ id, type, name: id, parentId: storeyId, px: 0, py: 0, props: {}, editable: true, module: null, storeyId });
const rel = (id: string, cls: string, relating: string, related: string[]): [string, RelationDto] => [id, { id, class: cls, relating, related }];

/**
 * A hall `h` over a frame `f1` (its column `c1` and rafter `r1`) and a purlin `p`; a wall `w` on its own; a group `g` of
 * the walls `x` and `y`; a room `sp` on the storey `s` of the building `b` (the spatial structure: never a whole).
 */
beforeEach(() => {
  const nodes = new Map([
    node('b', 'IfcBuilding', null), node('s', 'IfcBuildingStorey', null), node('sp', 'IfcSpace'),
    node('h', 'LapinSteelHall'), node('f1', 'IfcElementAssembly'), node('c1', 'IfcColumn'), node('r1', 'LapinSteelMember'), node('p', 'LapinSteelMember'),
    node('w', 'IfcWall'), node('g', 'IfcGroup'), node('x', 'IfcWall'), node('y', 'IfcWall'),
  ].map((n) => [n.id, n]));
  const relations = new Map<string, RelationDto>([
    rel('r0', 'IfcRelAggregates', 'b', ['s']), rel('r1a', 'IfcRelAggregates', 's', ['sp']),
    rel('r2', 'IfcRelAggregates', 'h', ['f1', 'p']), rel('r3', 'IfcRelAggregates', 'f1', ['c1', 'r1']),
    rel('r4', 'IfcRelContainedInSpatialStructure', 's', ['h', 'f1', 'c1', 'r1', 'p', 'w', 'x', 'y']),
    rel('r5', 'IfcRelAssignsToGroup', 'g', ['x', 'y']),
  ]);
  useSession.setState({ nodes, relations });
  useScope.setState({ scope: null });
  useUi.setState({ selected: [], selection: null });
  useDrill.setState({ path: null });
  setDisplay({ drillSelect: false });
});

const sel = () => useUi.getState().selected;
const click = (id: string | null, mods: { add?: boolean; leaf?: boolean } = {}) => { pickClick(id, mods); return sel(); };

describe('the chain of wholes a part is in', () => {
  it('climbs the aggregation to the spatial structure, with the group on top', () => {
    expect(assemblyPath('c1')).toEqual(['h', 'f1', 'c1']);
    expect(assemblyPath('p')).toEqual(['h', 'p']);
    expect(assemblyPath('w')).toEqual(['w']);
    expect(assemblyPath('sp')).toEqual(['sp']);
    expect(assemblyPath('x')).toEqual(['g', 'x']);
    expect(assemblyPath('g')).toEqual(['g']);
  });

  it('picks one level below the selection on the chain, a sibling inside the same whole, else the top; Ctrl the part', () => {
    const chain = ['h', 'f1', 'c1'];
    expect(drillTarget(chain, null, [])).toBe('h');
    expect(drillTarget(chain, chain, ['h'])).toBe('f1');
    expect(drillTarget(chain, chain, ['f1'])).toBe('c1');
    expect(drillTarget(chain, chain, ['c1'])).toBe('c1');
    expect(drillTarget(['h', 'p'], chain, ['f1'])).toBe('p');
    expect(drillTarget(['h', 'f1', 'r1'], chain, ['c1'])).toBe('r1');
    expect(drillTarget(['w'], chain, ['c1'])).toBe('w');
    expect(drillTarget(chain, null, ['w'])).toBe('h');
    expect(drillTarget(chain, null, ['a', 'b'])).toBe('h');
    expect(drillTarget(chain, null, [], true)).toBe('c1');
    expect(drillTarget([], null, [])).toBeNull();
  });
});

describe('picking with drilling off (the default)', () => {
  it('is as it was: a part picks itself, a group\'s member its group, Shift adds, nothing lets go, Esc is not taken', () => {
    expect(click('c1')).toEqual(['c1']);
    expect(click('c1')).toEqual(['c1']);
    expect(click('x')).toEqual(['g']);
    expect(click('c1', { add: true })).toEqual(['g', 'c1']);
    expect(click('c1', { leaf: true })).toEqual(['c1']);
    expect(click(null)).toEqual([]);
    expect(pickRegion(['c1', 'x', 'y', 'w'])).toEqual(['c1', 'g', 'w']);
    useUi.setState({ selected: ['c1'], selection: 'c1' });
    expect(drillEscape()).toBe(false);
    expect(sel()).toEqual(['c1']);
    expect(drillInside(['h', 'f1', 'c1'], ['f1'])).toBeNull();
    expect([...shownSelection(['h'])]).toEqual(['h']);
  });
});

describe('picking with drilling on', () => {
  beforeEach(() => setDisplay({ drillSelect: true }));

  it('steps into the wholes click by click to the part, and Esc steps back up to nothing', () => {
    expect(click('c1')).toEqual(['h']);
    expect(drillLevel(useDrill.getState().path, sel())).toBe(0);
    expect(click('c1')).toEqual(['f1']);
    expect(click('c1')).toEqual(['c1']);
    expect(click('c1')).toEqual(['c1']);
    expect(drillLevel(useDrill.getState().path, sel())).toBe(2);
    expect(drillEscape()).toBe(true);
    expect(sel()).toEqual(['f1']);
    expect(drillEscape()).toBe(true);
    expect(sel()).toEqual(['h']);
    expect(drillEscape()).toBe(true);
    expect(sel()).toEqual([]);
    expect(drillEscape()).toBe(false);
  });

  it('takes a sibling inside the whole it is in, the top of another, and lets go on nothing', () => {
    click('c1'); click('c1');
    expect(sel()).toEqual(['f1']);
    expect(click('p')).toEqual(['p']);
    click('c1'); click('c1');
    expect(click('c1')).toEqual(['c1']);
    expect(click('r1')).toEqual(['r1']);
    expect(click('w')).toEqual(['w']);
    expect(click('c1')).toEqual(['h']);
    expect(click(null)).toEqual([]);
    expect(useDrill.getState().path).toBeNull();
    expect(click('c1')).toEqual(['h']);
  });

  it('takes the part itself with Ctrl/Cmd, adds the top with Shift', () => {
    expect(click('c1', { leaf: true })).toEqual(['c1']);
    expect(drillLevel(useDrill.getState().path, sel())).toBe(2);
    expect(drillEscape()).toBe(true);
    expect(sel()).toEqual(['f1']);
    expect(click('x', { add: true })).toEqual(['f1', 'g']);
    expect(click('y', { add: true, leaf: true })).toEqual(['f1', 'g', 'y']);
    // Several selected: no level, Esc not taken.
    expect(drillEscape()).toBe(false);
  });

  it('treats a group as the top of the chain; inside an opened group the chain starts below it', () => {
    expect(click('x')).toEqual(['g']);
    expect(click('x')).toEqual(['x']);
    expect(click('y')).toEqual(['y']);
    openGroup('g');
    expect(click('x')).toEqual(['x']);
    expect(pickRegion(['x', 'y'])).toEqual(['x', 'y']);
  });

  it('selects the tops by a box or a lasso, and draws a whole with its parts; dims what is outside the whole it is in', () => {
    expect(pickRegion(['c1', 'r1', 'p', 'x', 'w'])).toEqual(['h', 'g', 'w']);
    expect([...shownSelection(['h'])].sort()).toEqual(['c1', 'f1', 'h', 'p', 'r1']);
    expect([...shownSelection(['g'])].sort()).toEqual(['g', 'x', 'y']);
    click('c1');
    expect(drillInside(useDrill.getState().path, sel())).toBeNull();
    click('c1');
    expect([...drillInside(useDrill.getState().path, sel())!].sort()).toEqual(['c1', 'f1', 'h', 'p', 'r1']);
  });

  it('turned off, lets go of the chain and picks as before', () => {
    click('c1'); click('c1');
    setDisplay({ drillSelect: false });
    expect(useDrill.getState().path).toBeNull();
    expect(click('c1')).toEqual(['c1']);
  });
});
