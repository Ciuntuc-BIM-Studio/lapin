import { describe, expect, it } from 'vitest';
import { covariance, ellipseAxes, projectCovariance, splatBudget, toHalf, writePage } from './splats3d';
import { depthRow, sortBackToFront } from './splatSort';

/** IEEE half bits back to a number (what GLSL's unpackHalf2x16 does). */
function fromHalf(h: number): number {
  const s = h & 0x8000 ? -1 : 1, e = (h >> 10) & 0x1f, m = h & 0x3ff;
  if (e === 0) return s * m * 2 ** -24;
  if (e === 31) return m ? NaN : s * Infinity;
  return s * (1 + m / 1024) * 2 ** (e - 15);
}

describe('a Gaussian splat', () => {
  it('has the covariance R S² Rᵀ of its scales and rotation', () => {
    // No turn: the scales squared on the diagonal.
    expect(covariance([1, 2, 3], [1, 0, 0, 0])).toEqual([1, 0, 0, 4, 0, 9]);
    // 90° about z: x and y swap.
    const h = Math.SQRT1_2;
    const c = covariance([1, 2, 3], [h, 0, 0, h]);
    [4, 0, 0, 1, 0, 9].forEach((v, i) => expect(c[i]).toBeCloseTo(v, 12));
    // 45° about z: the xy term is (s1² − s0²)/2 · sin 90°... here (1 − 4)/2 with the sign of the turn.
    const q = [Math.cos(Math.PI / 8), 0, 0, Math.sin(Math.PI / 8)] as const;
    const d = covariance([1, 2, 3], q);
    expect(d[0]).toBeCloseTo(2.5, 12);
    expect(d[3]).toBeCloseTo(2.5, 12);
    expect(d[1]).toBeCloseTo(-1.5, 12);
  });

  it('projects to the screen as J W Σ Wᵀ Jᵀ, a round one as a circle of f·σ/z, plus the low-pass', () => {
    // A sphere of σ = 0.1 m, 10 m before a camera looking down −z, focal 1000 px: σ on screen 10 px, 100 px² (+ 0.3).
    const w = [1, 0, 0, 0, 1, 0, 0, 0, 1];
    const p = projectCovariance([0.01, 0, 0, 0.01, 0, 0.01], [0, 0, -10], w, 1000, 1000);
    expect(p.a).toBeCloseTo(100.3, 9);
    expect(p.d).toBeCloseTo(100.3, 9);
    expect(p.b).toBeCloseTo(0, 12);
    // An orthographic view: the same at any depth, f in pixels a metre.
    const o = projectCovariance([0.01, 0, 0, 0.01, 0, 0.01], [3, 2, -500], w, 50, 50, true);
    expect(o.a).toBeCloseTo(25.3, 9);
    // A needle along the view's depth seen off the axis leans on screen: J's third column.
    const n = projectCovariance([0, 0, 0, 0, 0, 1], [5, 0, -10], w, 1000, 1000);
    expect(n.a - 0.3).toBeCloseTo((1000 * 5 / 100) ** 2, 6);
    expect(n.d - 0.3).toBeCloseTo(0, 9);
    // The camera turned 90° about its y: what was the world's x is now its depth.
    const turned = [0, 0, -1, 0, 1, 0, 1, 0, 0];
    const t = projectCovariance([1, 0, 0, 0, 0, 0], [0, 0, -10], turned, 1000, 1000);
    expect(t.a - 0.3).toBeCloseTo(0, 9);
  });

  it('spans its quad over its ellipse to 3σ, the longer axis first and the other across it', () => {
    const { major, minor } = ellipseAxes(100, 0, 25);
    expect(major[0]).toBeCloseTo(30, 9);
    expect(major[1]).toBeCloseTo(0, 9);
    expect(Math.hypot(...minor)).toBeCloseTo(15, 9);
    const tilted = ellipseAxes(50, 30, 50);
    expect(Math.hypot(...tilted.major)).toBeCloseTo(3 * Math.sqrt(80), 9);
    expect(tilted.major[0] * tilted.minor[0] + tilted.major[1] * tilted.minor[1]).toBeCloseTo(0, 9);
    // Anticlockwise from the major: the quad's corners keep their winding, or it would be culled as a back face.
    expect(tilted.major[0] * tilted.minor[1] - tilted.major[1] * tilted.minor[0]).toBeGreaterThan(0);
    expect(Math.hypot(...ellipseAxes(1e12, 0, 1).major)).toBe(1024);
  });

  it('is written into its texture: centre as float bits, colour as RGBA, covariance as halves', () => {
    expect(fromHalf(toHalf(1))).toBe(1);
    expect(fromHalf(toHalf(-0.333))).toBeCloseTo(-0.333, 3);
    expect(fromHalf(toHalf(1e-5))).toBeCloseTo(1e-5, 6);
    expect(toHalf(1e6)).toBe(0x7c00);
    const tex = new Uint32Array(16);
    writePage(tex, 1, { positions: new Float32Array([1.5, -2, 3]), scales: new Float32Array([0.5, 1, 2]), rotations: new Float32Array([1, 0, 0, 0]), colors: new Int32Array([0x80ff2010 | 0]) });
    const f = new Float32Array(tex.buffer);
    expect([f[8], f[9], f[10]]).toEqual([1.5, -2, 3]);
    expect(new Uint8Array(tex.buffer, 11 * 4, 4)).toEqual(new Uint8Array([0x10, 0x20, 0xff, 0x80]));
    expect(fromHalf(tex[12] & 0xffff)).toBe(0.25);
    expect(fromHalf(tex[13] >>> 16)).toBe(1);
    expect(fromHalf(tex[14] >>> 16)).toBe(4);
  });

  it('is held to a budget by the display quality, halved in a browser', () => {
    expect(splatBudget('fast')).toBeLessThan(splatBudget('balanced'));
    expect(splatBudget('precise')).toBeGreaterThan(splatBudget('balanced'));
    expect(splatBudget('balanced', true)).toBe(splatBudget('balanced') / 2);
  });
});

describe('the splats\' sort', () => {
  it('orders them back to front along the view by a counting sort of their depths', () => {
    // A camera at the origin looking down −z (the identity view): the farthest is the most negative z.
    const row = depthRow([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
    const zs = [-5, -1, -20, -3, -3.01, -100];
    const pos = new Float32Array(zs.flatMap((z, i) => [i, 0, z]));
    const order = Array.from(sortBackToFront(pos, zs.length, row, new Uint32Array(zs.length)));
    expect(order).toEqual([5, 2, 0, 4, 3, 1]);
  });

  it('keeps the order of splats at one depth, sorts a part of them, and reuses its scratch', () => {
    const row = depthRow([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
    const pos = new Float32Array([0, 0, -2, 0, 0, -2, 0, 0, -2, 0, 0, -9]);
    const scratch = {};
    expect(Array.from(sortBackToFront(pos, 3, row, new Uint32Array(3), scratch))).toEqual([0, 1, 2]);
    expect(Array.from(sortBackToFront(pos, 4, row, new Uint32Array(4), scratch))).toEqual([3, 0, 1, 2]);
  });

  it('sorts a million in one pass, every index once, depths not decreasing along the view', () => {
    const n = 1_000_000;
    const pos = new Float32Array(3 * n);
    let seed = 1;
    const rnd = () => ((seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648);
    for (let i = 0; i < 3 * n; i++) pos[i] = rnd() * 100 - 50;
    // A view looking along −(1, 1, 1)/√3 from far: view z = −(x + y + z)/√3 − 200.
    const k = -1 / Math.sqrt(3);
    const row = [k, k, k, -200] as const;
    const order = sortBackToFront(pos, n, row, new Uint32Array(n));
    const seen = new Uint8Array(n);
    let last = -Infinity, bad = 0;
    for (const i of order) {
      seen[i]++;
      const z = k * (pos[3 * i] + pos[3 * i + 1] + pos[3 * i + 2]) - 200;
      // Within a 16-bit step of the range (≈ 173 m / 65536).
      if (z < last - 0.005) bad++;
      last = Math.max(last, z);
    }
    expect(seen.every((v) => v === 1)).toBe(true);
    expect(bad).toBe(0);
  });
});
