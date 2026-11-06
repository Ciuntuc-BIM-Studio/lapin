import { describe, expect, it } from 'vitest';
import { profileShape, tidyProfile } from './WallProfileDialog';

describe('a wall by its profile', () => {
  it('starts from a gable, a slope or steps', () => {
    expect(profileShape('gable', 6, 3, 5)).toEqual([[0, 3], [3, 5], [6, 3]]);
    expect(profileShape('slope', 6, 3, 4)).toEqual([[0, 3], [6, 4]]);
    // Three steps from 3 m to 5 m: each a flat run, the rise two points one over the other.
    expect(profileShape('steps', 6, 3, 5)).toEqual([[0, 3], [2, 3], [2, 4], [4, 4], [4, 5], [6, 5]]);
  });

  it('keeps its points in order, inside the wall and over its base', () => {
    expect(tidyProfile([[4, 3], [-1, 0], [7, 2.5]], 6)).toEqual([[0, 0.05], [4, 3], [6, 2.5]]);
  });
});
