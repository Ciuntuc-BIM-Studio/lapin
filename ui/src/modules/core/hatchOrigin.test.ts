import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { hasHatchOrigin, hatchOriginOf, setOriginArgs, withCoordinate } from './hatchOrigin';

const node = (type: string, props: Record<string, unknown> = {}) => ({ id: 'a', type, props } as unknown as NodeDto);

describe('hatch origin', () => {
  it('reads the attribute', () => {
    expect(hatchOriginOf(node('IfcWall', { hatchOrigin: [0.5, 0.25] }))).toEqual([0.5, 0.25]);
    expect(hatchOriginOf(node('IfcWall'))).toBeNull();
    expect(hatchOriginOf(node('IfcWall', { hatchOrigin: ['x'] }))).toBeNull();
  });
  it('changes one coordinate and keeps the other', () => {
    expect(withCoordinate([1, 2], 1, 3)).toEqual([1, 3]);
    expect(withCoordinate(null, 0, 0.4)).toEqual([0.4, 0]);
    expect(withCoordinate([1, 2], 0, null)).toEqual([0, 2]);
  });
  it('sends one step for one or many elements, null clears', () => {
    expect(setOriginArgs(['a'], [0, 1])).toEqual({ id: 'a', origin: [0, 1] });
    expect(setOriginArgs(['a', 'b'], null)).toEqual({ ids: ['a', 'b'], origin: null });
  });
  it('offers walls and not rooms', () => {
    expect(hasHatchOrigin(node('IfcWall'))).toBe(true);
    expect(hasHatchOrigin(node('IfcSpace'))).toBe(false);
  });
});
