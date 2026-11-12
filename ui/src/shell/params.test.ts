import { describe, expect, it } from 'vitest';
import { selectionLike, selectionWhere, sharedValue } from './params';

// A tester's report (#74): with three windows selected, Side flipped only the first.
describe('who a change goes to', () => {
  const types: Record<string, string> = { w1: 'IfcWindow', w2: 'IfcWindow', w3: 'IfcWindow', d1: 'IfcDoor', wall: 'IfcWall' };
  const typeOf = (id: string) => types[id];

  it('goes to every selected element of the kind when the element is among them', () => {
    expect(selectionLike('w1', ['IfcWindow'], ['w1', 'w2', 'w3', 'wall'], typeOf)).toEqual(['w1', 'w2', 'w3']);
  });

  it('takes doors and windows together when asked', () => {
    expect(selectionLike('w2', ['IfcDoor', 'IfcWindow'], ['w1', 'd1', 'w2', 'wall'], typeOf)).toEqual(['w1', 'd1', 'w2']);
  });

  it('goes to the element alone when it is not selected, or not of the kinds', () => {
    expect(selectionLike('w1', ['IfcWindow'], ['w2', 'w3'], typeOf)).toEqual(['w1']);
    expect(selectionLike('wall', ['IfcWindow'], ['w1', 'wall'], typeOf)).toEqual(['wall']);
  });
});

// A forum user's report (topic #29): 23 beams selected and given a phase, only the first took it.
describe('a change any element takes (its phase, its discipline)', () => {
  const types: Record<string, string> = { b1: 'IfcBeam', b2: 'IfcBeam', wall: 'IfcWall', d1: 'IfcDoor', level: 'IfcBuildingStorey' };
  const takes = (id: string) => types[id] !== 'IfcBuildingStorey';

  it('goes to every selected element that takes it, whatever its class', () => {
    expect(selectionWhere('b1', ['b1', 'b2', 'wall', 'd1', 'level'], takes)).toEqual(['b1', 'b2', 'wall', 'd1']);
  });

  it('goes to the element alone when it is not among the selected', () => {
    expect(selectionWhere('b1', ['b2', 'wall'], takes)).toEqual(['b1']);
  });

  it('shows the value they share, else nothing (varies)', () => {
    const props: Record<string, Record<string, unknown>> = { b1: { phaseCreated: 'existing' }, b2: { phaseCreated: 'existing' }, wall: {} };
    const of = (id: string) => props[id];
    expect(sharedValue(['b1', 'b2'], 'phaseCreated', of)).toEqual({ value: 'existing', varies: false });
    expect(sharedValue(['b1', 'b2', 'wall'], 'phaseCreated', of)).toEqual({ value: 'existing', varies: true });
    expect(sharedValue(['wall'], 'phaseCreated', of)).toEqual({ value: undefined, varies: false });
  });
});
