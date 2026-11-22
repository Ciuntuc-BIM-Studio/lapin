import { describe, expect, it } from 'vitest';
import * as THREE from 'three';
import { assignSlots, BakeQueue, blendAt, EYE_HEIGHT, onVoxels, placeProbes, probeBudget, roomAt, sampleTriangles, weakGpu, zoneDistance, type ProbeRoom, type Vec3 } from './probes';
import { atlasLayout, directionFace, faceDirection, levelOrigin, patchShader, PROBE_MAX } from './probes3d';

const room = (id: string, x0: number, y0: number, x1: number, y1: number, z0 = 0, z1 = 2.8, inside?: ProbeRoom['inside']): ProbeRoom => ({ id, min: [x0, y0, z0], max: [x1, y1, z1], inside });
/** Samples of a glossy floor over a rectangle, at its top (z). */
const floor = (x0: number, y0: number, x1: number, y1: number, z = 0): Vec3[] => {
  const p = new Float32Array([x0, y0, z, x1, y0, z, x1, y1, z, x0, y0, z, x1, y1, z, x0, y1, z]);
  return onVoxels(sampleTriangles(p, null, 0.5), 0.5);
};

describe('probe placement (docs/plan-render.md S3)', () => {
  it('puts one probe in a room with a glossy floor, 1.70 m over the floor, its box the room', () => {
    const [p, ...rest] = placeProbes([room('living', 0, 0, 6, 4.5)], floor(0.1, 0.1, 5.9, 4.4));
    expect(rest).toHaveLength(0);
    expect(p.room).toBe('living');
    expect(p.position).toEqual([3, 2.25, EYE_HEIGHT]);
    expect(p.min).toEqual([0, 0, 0]);
    expect(p.max).toEqual([6, 4.5, 2.8]);
    expect(p.zone).toEqual([0, 0, 6, 4.5]);
    expect(p.weight).toBeGreaterThan(50);
  });

  it('puts none in a room with nothing glossy, nor for glossy faces outside rooms', () => {
    expect(placeProbes([room('a', 0, 0, 5, 4)], [])).toEqual([]);
    expect(placeProbes([room('a', 0, 0, 5, 4)], floor(10, 10, 12, 12))).toEqual([]);
  });

  it('stands lower in a low room (0.6 m under its ceiling, never under half its height)', () => {
    expect(placeProbes([room('cellar', 0, 0, 4, 4, 0, 2.2)], floor(0, 0, 4, 4))[0].position[2]).toBeCloseTo(1.6, 6);
    expect(placeProbes([room('crawl', 0, 0, 4, 4, 0, 1)], floor(0, 0, 4, 4))[0].position[2]).toBeCloseTo(0.5, 6);
  });

  it('gives a long room a grid at 8 m, each probe its cell', () => {
    const probes = placeProbes([room('hall', 0, 0, 24, 6)], floor(0, 0, 24, 6));
    expect(probes).toHaveLength(3);
    const xs = probes.map((p) => p.position[0]).sort((a, b) => a - b);
    expect(xs).toEqual([4, 12, 20]);
    expect(probes.every((p) => p.position[1] === 3 && p.min[0] === 0 && p.max[0] === 24)).toBe(true);
    const zones = probes.map((p) => p.zone).sort((a, b) => a[0] - b[0]);
    expect(zones).toEqual([[0, 0, 8, 6], [8, 0, 16, 6], [16, 0, 24, 6]]);
    expect(new Set(probes.map((p) => p.key)).size).toBe(3);
  });

  it('keeps in a long room only the cells near something glossy', () => {
    const probes = placeProbes([room('hall', 0, 0, 40, 6)], floor(0, 0, 3, 6));
    expect(probes.map((p) => p.position[0])).toEqual([4]);
  });

  it('moves the probe of an L-shaped room inside it', () => {
    // An L: the box 0–6 × 0–6 less its 3–6 × 3–6 corner; the box's middle (3, 3) lies on the notch's corner, outside.
    const inside = (x: number, y: number) => x < 3 || y < 3;
    const [p] = placeProbes([room('L', 0, 0, 6, 6, 0, 2.8, (x, y) => inside(x, y) && !(x >= 2.9 && y >= 2.9))], floor(0, 0, 3, 2.5));
    expect(inside(p.position[0], p.position[1])).toBe(true);
  });

  it('gives each storey\'s room its own floor', () => {
    const rooms = [room('down', 0, 0, 5, 4, 0, 2.7), room('up', 0, 0, 5, 4, 3, 5.7)];
    expect(roomAt(rooms, [2, 2, 0])).toBe(0);
    expect(roomAt(rooms, [2, 2, 3])).toBe(1);
    expect(roomAt(rooms, [2, 2, 2.95])).toBe(1);
    const probes = placeProbes(rooms, [...floor(0, 0, 5, 4, 0), ...floor(0, 0, 5, 4, 3)]);
    expect(probes.map((p) => p.room).sort()).toEqual(['down', 'up']);
    expect(probes.find((p) => p.room === 'up')!.position[2]).toBeCloseTo(4.7, 6);
  });

  it('keeps the probes that serve the most when there are more than wanted', () => {
    const rooms = [room('big', 0, 0, 6, 6), room('small', 7, 0, 8, 1)];
    const probes = placeProbes(rooms, [...floor(0, 0, 6, 6), ...floor(7, 0, 8, 1)], 1);
    expect(probes.map((p) => p.room)).toEqual(['big']);
  });
});

describe('probe blending', () => {
  // Two rooms side by side, a 0.25 m wall between them (x 5 to 5.25).
  const probes = placeProbes([room('a', 0, 0, 5, 4), room('b', 5.25, 0, 10.25, 4)], [...floor(0, 0, 5, 4), ...floor(5.25, 0, 10.25, 4)]);
  const a = probes.findIndex((p) => p.room === 'a'), b = 1 - a;

  it('takes one room\'s probe alone well inside it', () => {
    const w = blendAt([2.5, 2, 0], probes);
    expect(w.weights[a]).toBeCloseTo(1, 6);
    expect(w.weights[b]).toBe(0);
    expect(w.outside).toBe(0);
  });

  it('blends the two over about a metre across their wall, evenly in its middle', () => {
    // On a room's border both count, its own more, nothing of the look outside.
    const edge = blendAt([5, 2, 0], probes);
    expect(edge.weights[a] + edge.weights[b]).toBeCloseTo(1, 6);
    expect(edge.weights[a]).toBeCloseTo(0.5 / 0.75, 6);
    // In the wall's middle (a doorway's floor) the two alike, and nothing of the look outside.
    const mid = blendAt([5.125, 2, 0], probes);
    expect(mid.weights[a]).toBeCloseTo(0.5, 9);
    expect(mid.weights[b]).toBeCloseTo(0.5, 9);
    expect(mid.outside).toBe(0);
    // Just inside, the other room still counts a little; half a metre in, not at all.
    const near = blendAt([4.9, 2, 0], probes);
    expect(near.weights[a]).toBeGreaterThan(near.weights[b]);
    expect(near.weights[b]).toBeGreaterThan(0);
    expect(blendAt([4.7, 2, 0], probes).weights[b]).toBe(0);
    // Smooth: no step across the wall.
    let last = 1;
    for (let x = 4; x <= 6.25; x += 0.05) {
      const w = blendAt([x, 2, 0], probes).weights[a];
      expect(w).toBeLessThanOrEqual(last + 1e-9);
      last = w;
    }
  });

  it('fades into the look outside past the outer walls, and keeps the other storeys out', () => {
    expect(blendAt([-0.1, 2, 0], probes).outside).toBeCloseTo(0.2, 6);
    expect(blendAt([-0.5, 2, 0], probes).outside).toBe(1);
    expect(blendAt([2.5, 2, 3.5], probes).outside).toBe(1);
  });

  it('measures the distance to a zone, negative inside', () => {
    expect(zoneDistance(1, 1, [0, 0, 4, 4])).toBe(-1);
    expect(zoneDistance(5, 2, [0, 0, 4, 4])).toBe(1);
    expect(zoneDistance(7, 8, [0, 0, 4, 4])).toBe(5);
  });
});

describe('probe slots', () => {
  const probes = placeProbes([0, 1, 2, 3].map((i) => room(`r${i}`, i * 10, 0, i * 10 + 5, 4)), [0, 1, 2, 3].flatMap((i) => floor(i * 10, 0, i * 10 + 5, 4)));

  it('gives the slots to the probes nearest the eye', () => {
    expect(assignSlots([], probes, [2, 2, 1.5], 2).sort()).toEqual(['r0', 'r1']);
    expect(assignSlots([], probes, [32, 2, 1.5], 2).sort()).toEqual(['r2', 'r3']);
  });

  it('keeps a probe in its slot unless another is clearly nearer', () => {
    const now = assignSlots([], probes, [2, 2, 1.5], 2);
    // A step towards r2: r1 is still about as near, kept where it was.
    expect(assignSlots(now, probes, [12, 2, 1.5], 2)).toEqual(now);
    const far = assignSlots(now, probes, [31, 2, 1.5], 2);
    expect(far.sort()).toEqual(['r2', 'r3']);
    // A slot keeps its place in the list (its picture is in its layer).
    const moved = assignSlots(now, probes, [22, 2, 1.5], 2);
    expect(moved[now.indexOf('r1')]).toBe('r1');
  });

  it('empties the slots of probes gone', () => {
    expect(assignSlots(['gone', null], probes.slice(0, 1), [0, 0, 0], 2)).toEqual(['r0', null]);
  });
});

describe('the bake scheduler', () => {
  it('bakes two faces a frame, the nearest probe first, one probe to its end', () => {
    const q = new BakeQueue(2);
    q.set(0, [20, 0, 1.7]);
    q.set(1, [2, 0, 1.7]);
    const eye: Vec3 = [0, 0, 1.6];
    const steps = [q.next(eye), q.next(eye), q.next(eye)];
    expect(steps.map((s) => s?.slot)).toEqual([1, 1, 1]);
    expect(steps.map((s) => s?.faces)).toEqual([[0, 1], [2, 3], [4, 5]]);
    expect(steps.map((s) => s?.finish)).toEqual([false, false, true]);
    expect(q.baked(1)).toBe(true);
    expect(q.baked(0)).toBe(false);
    expect(q.next(eye)?.slot).toBe(0);
    q.next(eye); q.next(eye);
    expect(q.next(eye)).toBeNull();
    expect(q.pending).toBe(false);
    expect(q.counts()).toEqual({ baked: 2, waiting: 0 });
  });

  it('bakes the unbaked before the stale, and a probe made stale while baking again after', () => {
    const q = new BakeQueue(2);
    q.set(0, [2, 0, 1.7]);
    for (let i = 0; i < 3; i++) q.next([0, 0, 0]);
    q.next([0, 0, 0]); // nothing left: null
    q.stale();
    q.set(1, [30, 0, 1.7]);
    // The far one has never been baked: it goes first, the near stale one still shown meanwhile.
    expect(q.next([0, 0, 0])?.slot).toBe(1);
    expect(q.baked(0)).toBe(true);
    q.stale(); // while slot 1 is being baked
    q.next([0, 0, 0]);
    expect(q.next([0, 0, 0])?.finish).toBe(true);
    expect(q.counts().waiting).toBe(2);
    const order = [];
    for (let s = q.next([0, 0, 0]); s; s = q.next([0, 0, 0])) if (s.finish) order.push(s.slot);
    expect(order).toEqual([0, 1]);
  });

  it('takes one face a frame on a weak chip', () => {
    const q = new BakeQueue(1);
    q.set(0, [0, 0, 0]);
    const faces = [];
    for (let s = q.next([0, 0, 0]); s; s = q.next([0, 0, 0])) faces.push(...s.faces);
    expect(faces).toEqual([0, 1, 2, 3, 4, 5]);
  });

  it('forgets a slot emptied or changed mid-bake', () => {
    const q = new BakeQueue(2);
    q.set(0, [0, 0, 0]);
    q.next([0, 0, 0]);
    q.set(0, [5, 0, 0]);
    expect(q.next([0, 0, 0])?.faces).toEqual([0, 1]);
    q.set(0, null);
    expect(q.next([0, 0, 0])).toBeNull();
  });
});

describe('probe budget and atlas', () => {
  it('draws none in the drawing, fewer and smaller on a weak chip', () => {
    expect(probeBudget('drawing', false)).toBeNull();
    expect(probeBudget('realistic', false)).toEqual({ slots: 10, size: 256, facesPerFrame: 2 });
    expect(probeBudget('realistic', true)!.size).toBe(128);
    expect(probeBudget('medium', true)!.slots).toBeLessThan(probeBudget('medium', false)!.slots);
    expect(probeBudget('realistic', false)!.slots).toBeLessThanOrEqual(PROBE_MAX);
    expect(weakGpu('ANGLE (Intel, Intel(R) UHD Graphics 620, OpenGL 4.1)', 16384)).toBe(true);
    expect(weakGpu('ANGLE (Google, Vulkan 1.3.0 (SwiftShader Device))', 8192)).toBe(true);
    expect(weakGpu('ANGLE (Apple, ANGLE Metal Renderer: Apple M1 Max, Unspecified Version)', 16384)).toBe(false);
    expect(weakGpu('ANGLE (NVIDIA, NVIDIA GeForce RTX 3060)', 4096)).toBe(true);
  });

  it('lays a probe\'s faces and their blur levels in its layer without overlap', () => {
    const { width, height, levels } = atlasLayout(256);
    expect([width, height, levels]).toEqual([768, 1024, 6]);
    let y = 0;
    for (let l = 0; l < levels; l++) {
      const o = levelOrigin(256, l);
      expect(o.y).toBeCloseTo(y, 9);
      y += 2 * o.face;
    }
    expect(y).toBeLessThanOrEqual(height);
    expect(levelOrigin(256, levels - 1).face).toBe(8);
  });

  it('maps a direction to its face and back', () => {
    for (const d of [[1, 0.2, -0.3], [-1, 0.5, 0.5], [0.1, 1, 0], [0.3, -1, 0.2], [0, 0, 1], [0.4, -0.2, -1]] as Vec3[]) {
      const { face, u, v } = directionFace(d);
      const back = faceDirection(face, u, v);
      const k = Math.max(...back.map(Math.abs)) / Math.max(...d.map(Math.abs));
      back.forEach((c, i) => expect(c).toBeCloseTo(d[i] * k, 9));
    }
  });

  it('patches three\'s physical shader where it reads its environment', () => {
    const { vertex, fragment } = patchShader(THREE.ShaderLib.physical.vertexShader, THREE.ShaderLib.physical.fragmentShader);
    expect(vertex).toContain('vProbeWorld = worldPosition.xyz;');
    expect(fragment).toContain('uniform highp sampler2DArray probeMaps;');
    expect(fragment).not.toContain('#include <envmap_physical_pars_fragment>');
    // The functions three's lights call are all defined again, none of them reading the environment map itself.
    for (const f of ['getIBLIrradiance', 'getIBLRadiance', 'getIBLAnisotropyRadiance', 'getIBLRetroRadiance']) expect(fragment).toContain(`vec3 ${f}(`);
    const body = fragment.slice(fragment.indexOf('uniform highp sampler2DArray probeMaps;'));
    expect(body.slice(0, body.indexOf('#include'))).not.toMatch(/textureLod\(\s*envMap/);
  });
});
