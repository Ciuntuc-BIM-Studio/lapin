import { beforeEach, describe, expect, it } from 'vitest';
import { changed3d, forget3d, key3d, recall3d, remember3d } from './viewState3d';

describe('the 3D tab memory', () => {
  beforeEach(forget3d);

  it('keeps each 3D view its camera and section box apart', () => {
    const pose = { azimuth: 1, pitch: -0.4, distance: 12, target: { x: 1, y: 2, z: 3 } };
    remember3d(key3d(null), { pose, ortho: true });
    remember3d(key3d(null), { box: { min: [0, 0, 0], max: [4, 4, 3] } });
    remember3d(key3d('view1'), { box: null });
    expect(recall3d('3d')).toEqual({ pose, ortho: true, box: { min: [0, 0, 0], max: [4, 4, 3] } });
    expect(recall3d('view1')).toEqual({ box: null });
    expect(recall3d('other')).toBeUndefined();
  });

  it('a section box switched off is remembered as off, and forgetting empties everything', () => {
    remember3d('3d', { box: { min: [0, 0, 0], max: [1, 1, 1] } });
    remember3d('3d', { box: null });
    expect(recall3d('3d')?.box).toBeNull();
    forget3d();
    expect(recall3d('3d')).toBeUndefined();
  });

  it('a remount on the same view is no change; another view, or a new project, is', () => {
    expect(changed3d('3d')).toBe(true);
    expect(changed3d('3d')).toBe(false);
    expect(changed3d('v')).toBe(true);
    forget3d();
    expect(changed3d('v')).toBe(true);
  });
});
