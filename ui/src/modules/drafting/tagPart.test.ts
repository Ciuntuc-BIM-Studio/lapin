import { describe, expect, it } from 'vitest';
import { TAG_PARTS, tagPartArgs } from './tag';

describe('a tag of a window\'s sill (forum topic 178, decision 727)', () => {
  it('names the sill chosen for a door or window, else sends the click so the engine finds the part it lands on', () => {
    expect(tagPartArgs('sill:outside', 'IfcWindow', { x: 1, y: 2 })).toEqual({ part: 'sill:outside' });
    expect(tagPartArgs('sill:inside', 'IfcDoor', undefined)).toEqual({ part: 'sill:inside' });
    // A wall has no sill: the click goes, and the engine tags the wall.
    expect(tagPartArgs('sill:outside', 'IfcWall', { x: 1, y: 2 })).toEqual({ pick: [1, 2] });
    expect(tagPartArgs('', 'IfcWindow', { x: 3, y: 4 })).toEqual({ pick: [3, 4] });
    expect(tagPartArgs('', 'IfcWindow', undefined)).toEqual({});
  });
  it('offers the element and both sills', () => {
    expect(TAG_PARTS.map(([k]) => k)).toEqual(['', 'sill:inside', 'sill:outside']);
  });
});
