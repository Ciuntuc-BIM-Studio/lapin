import { describe, expect, it } from 'vitest';
import { visibleCrop } from './imageCrop';

describe('a picture cut to the screen (decision 399)', () => {
  it('draws a picture about the screen\'s size whole', () => {
    expect(visibleCrop({ x: 0, y: 0 }, { x: 800, y: 0 }, { x: 0, y: 600 }, 1000, 700)).toBe('all');
  });

  it('draws nothing of a picture off the screen', () => {
    expect(visibleCrop({ x: 2000, y: 0 }, { x: 2400, y: 0 }, { x: 2000, y: 300 }, 1000, 700)).toBeNull();
  });

  it('cuts an airborne slice zoomed in on a building to what the screen sees', () => {
    // 400 m at 140 px/m: 56 000 px wide, the screen 1200 × 750 looking at its middle.
    const W = 56_000, H = 63_000;
    const tl = { x: 600 - W / 2, y: 375 - H / 2 };
    const crop = visibleCrop(tl, { x: tl.x + W, y: tl.y }, { x: tl.x, y: tl.y + H }, 1200, 750, 0);
    expect(crop).not.toBe('all');
    if (!crop || crop === 'all') throw new Error('no crop');
    expect((crop.u1 - crop.u0) * W).toBeCloseTo(1200, 3);
    expect((crop.v1 - crop.v0) * H).toBeCloseTo(750, 3);
    expect(crop.u0).toBeCloseTo((W / 2 - 600) / W, 6);
  });

  it('keeps a margin round the screen and stays within the picture', () => {
    const crop = visibleCrop({ x: -10_000, y: -30 }, { x: 10_000, y: -30 }, { x: -10_000, y: 20_000 }, 1000, 700, 64);
    if (!crop || crop === 'all') throw new Error('no crop');
    expect(crop.v0).toBe(0);
    expect(crop.u0).toBeCloseTo((10_000 - 64) / 20_000, 6);
    expect(crop.u1).toBeCloseTo((10_000 + 1064) / 20_000, 6);
  });
});
