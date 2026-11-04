import { describe, expect, it } from 'vitest';
import { selectionLike } from './params';

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
