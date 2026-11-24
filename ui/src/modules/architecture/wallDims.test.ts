import { describe, expect, it } from 'vitest';
import { commitAcross, commitEnd, commitLength, hostOf, wallDims, type WallLineInfo } from './wallDims';

const W = (id: string, a: [number, number], b: [number, number], faces: [number, number] = [-0.15, 0.15], more: Partial<WallLineInfo> = {}): WallLineInfo =>
  ({ id, name: id, a: { x: a[0], y: a[1] }, b: { x: b[0], y: b[1] }, faces, joins: { start: [], end: [], path: [] }, ...more });

// A room 6 × 5: the host along y = 2 inside it (x from 1 to 4), walls 300 thick at y = 0 and y = 5 (parallel), x = 0 and x = 6 (across).
const south = W('S', [0, 0], [6, 0]);
const north = W('N', [0, 5], [6, 5], [-0.15, 0.15], { core: [-0.1, 0.1] });
const west = W('W', [0, 0], [0, 5]);
const east = W('E', [6, 0], [6, 5]);
const host = W('H', [1, 2], [4, 2], [-0.1, 0.1]);
const walls = [south, north, west, east, host];

const val = (d: { value: number }) => Math.round(d.value * 1e6) / 1e6;

describe('a wall\'s temporary dimensions (decision 693)', () => {
  it('measures from its axis to the near face of the parallel walls either side, and from each free end to the wall it runs toward', () => {
    const dims = wallDims(hostOf(host), walls, [], {});
    const by = Object.fromEntries(dims.map((d) => [d.key, d]));
    // Left (+y): the north wall's near face at 4.85; right: the south wall's at 0.15.
    expect(by['x:left'].target.id).toBe('N');
    expect(val(by['x:left'])).toBe(2.85);
    expect(by['x:right'].target.id).toBe('S');
    expect(val(by['x:right'])).toBe(1.85);
    // Its start at x = 1 runs west to the west wall's face at 0.15; its end at x = 4 runs east to 5.85.
    expect(by['e:start'].target.id).toBe('W');
    expect(val(by['e:start'])).toBe(0.85);
    expect(val(by['e:end'])).toBe(1.85);
    // The dimensions across stand on the stretch the two walls share, the host's middle here.
    expect(by['x:left'].station).toBeCloseTo(1.5);
  });

  it('a click on the reference end cycles near face, far face, core faces, axis; on its own end reference, near face, far face', () => {
    const at = (picks: Parameters<typeof wallDims>[3]) => wallDims(hostOf(host), walls, [], picks).find((d) => d.key === 'x:left')!;
    expect(val(at({ 'x:left': { opt: 1 } }))).toBe(3.15);
    expect(val(at({ 'x:left': { opt: 2 } }))).toBe(2.9);
    expect(val(at({ 'x:left': { opt: 3 } }))).toBe(3.1);
    expect(val(at({ 'x:left': { opt: 4 } }))).toBe(3);
    expect(val(at({ 'x:left': { opt: 5 } }))).toBe(2.85); // round again
    expect(val(at({ 'x:left': { own: 1 } }))).toBe(2.75); // its own left face
    expect(val(at({ 'x:left': { own: 2 } }))).toBe(2.95); // its far face
  });

  it('measures from the reference line it keeps, and leaves out walls it does not face, in line with it or joined at its end', () => {
    // Kept on its right face, 100 mm right of its axis.
    const dims = wallDims(hostOf(host, -0.1), walls, [], {});
    expect(val(dims.find((d) => d.key === 'x:right')!)).toBe(1.75);
    // A parallel wall beside no part of it, a wall in line with it: no dimension to them.
    const apart = W('A', [8, 3], [12, 3]);
    const inline = W('I', [4.5, 2], [5, 2]);
    const keys = wallDims(hostOf(host), [apart, inline, south], [], {}).map((d) => `${d.key}:${d.target.id}`);
    expect(keys).toEqual(['x:right:S']);
    // An end joined to a wall has no dimension (moving it would leave the join).
    const joined = { ...host, joins: { start: ['W'], end: [], path: [] } };
    expect(wallDims(hostOf(joined), walls, [], {}).map((d) => d.key)).not.toContain('e:start');
  });

  it('measures to a grid axis parallel to it too', () => {
    const axes = [{ id: 'g#B', name: 'B', a: { x: -1, y: 3 }, b: { x: 7, y: 3 } }];
    const left = wallDims(hostOf(host), walls, axes, {}).find((d) => d.key === 'x:left')!;
    expect(left.target.id).toBe('g#B');
    expect(val(left)).toBe(1);
  });

  it('a distance typed moves the wall square to itself, or its end along it, the reference staying', () => {
    const h = hostOf(host);
    const dims = wallDims(h, walls, [], {});
    // 2850 → 2000 from the north wall: the wall goes 850 north.
    const m = commitAcross(h, dims.find((d) => d.key === 'x:left')!, 2)!;
    expect(m.a.y).toBeCloseTo(2.85);
    expect(m.b.y).toBeCloseTo(2.85);
    expect(m.a.x).toBeCloseTo(1);
    // 1850 → 2500 from the south wall: 650 further north.
    expect(commitAcross(h, dims.find((d) => d.key === 'x:right')!, 2.5)!.a.y).toBeCloseTo(2.65);
    // The start 850 → 300 from the west wall: it moves west to x = 0.45, the end staying.
    const e = commitEnd(h, dims.find((d) => d.key === 'e:start')!, 0.3)!;
    expect(e.a.x).toBeCloseTo(0.45);
    expect(e.b.x).toBeCloseTo(4);
    // The end 1850 → 2850: back to x = 3, shorter.
    expect(commitEnd(h, dims.find((d) => d.key === 'e:end')!, 2.85)!.b.x).toBeCloseTo(3);
    // Nothing when it already reads so, or when the wall would have no length.
    expect(commitAcross(h, dims.find((d) => d.key === 'x:left')!, 2.85)).toBeNull();
    expect(commitEnd(h, dims.find((d) => d.key === 'e:end')!, 4.85)).toBeNull();
  });

  it('a wall being drawn measures from the line clicked to the walls beside it', () => {
    // From (1, 2) towards (3, 2), on a line with the body to its left (faces 0 to 0.2 left of it).
    const drawn = { a: { x: 1, y: 2 }, b: { x: 3, y: 2 }, ref: 0, faces: [0, 0.2] as [number, number], joins: { start: [], end: [], path: [] } };
    const dims = wallDims(drawn, [south, north, west, east], [], {});
    expect(val(dims.find((d) => d.key === 'x:left')!)).toBe(2.85);
    expect(val(dims.find((d) => d.key === 'x:right')!)).toBe(1.85);
    expect(val(dims.find((d) => d.key === 'e:end')!)).toBe(2.85);
  });
});

describe('a wall\'s length along its faces, and offsets from the corners (forum topic 240)', () => {
  // A room's corner at (0, 0): the west wall up x = 0, the south wall along y = 0, joined in a corner at their starts; both 300 thick,
  // a 200 core. The host runs east from the west wall at y = 2 (its start on the west wall's side: a T), its end free at x = 4.
  const joins = (start: string[] = [], end: string[] = [], path: string[] = []) => ({ start, end, path });
  const west = W('W', [0, 0], [0, 5], [-0.15, 0.15], { core: [-0.1, 0.1], joins: joins(['S'], [], ['H']) });
  const south = W('S', [0, 0], [6, 0], [-0.15, 0.15], { core: [-0.1, 0.1], joins: joins(['W']) });
  const host = W('H', [0, 2], [4, 2], [-0.1, 0.1], { joins: joins(['W']) });
  const room = [west, south, host];
  const by = (dims: ReturnType<typeof wallDims>) => Object.fromEntries(dims.map((d) => [d.key, d]));

  it('measures along each face from the face of the wall it joins (a T: its near face) to its free end', () => {
    const d = by(wallDims(hostOf(host), room, [], {}, { mode: 'faces', length: true }));
    expect(val(d['l:left'])).toBe(3.85);
    expect(val(d['l:right'])).toBe(3.85);
    expect(d['l:left'].kind).toBe('length');
    expect(d['l:left'].options[d['l:left'].opt].what).toBe('near face');
    expect(d['l:left'].ownOptions.map((o) => o.what)).toEqual(['end']);
  });

  it('at a corner, the inside face runs to the other\'s near face and the outside face to its far face', () => {
    const corner = hostOf(south);
    const faces = by(wallDims(corner, room, [], {}, { mode: 'faces', length: true }));
    expect(val(faces['l:left'])).toBe(5.85);   // inside the room (+y): from the west wall's inner face
    expect(val(faces['l:right'])).toBe(6.15);  // outside: from its outer face, the exterior corner
    const core = by(wallDims(corner, room, [], {}, { mode: 'core', length: true }));
    expect(val(core['l:left'])).toBe(5.9);
    expect(val(core['l:right'])).toBe(6.1);
    // Centrelines: one dimension, axis to axis.
    const axis = wallDims(corner, room, [], {}, { mode: 'axis', length: true }).filter((x) => x.kind === 'length');
    expect(axis.map((x) => [x.key, val(x)])).toEqual([['l:axis', 6]]);
  });

  it('a click on a witness end cycles that end\'s reference: near face, far face, core faces, axis', () => {
    const at = (opt: number) => val(by(wallDims(hostOf(host), room, [], { 'l:left': { opt } }, { mode: 'faces', length: true }))['l:left']);
    expect(at(1)).toBe(4.15); // the west wall's far face
    expect(at(2)).toBe(3.9);  // its near core face
    expect(at(3)).toBe(4.1);  // its far core face
    expect(at(4)).toBe(4);    // its axis
    expect(at(5)).toBe(3.85); // round again
  });

  it('a length typed moves the free end along the wall; with both ends joined, its end', () => {
    const h = hostOf(host);
    const left = by(wallDims(h, room, [], {}, { mode: 'faces', length: true }))['l:left'];
    const m = commitLength(h, left, 3.5)!;
    expect(m.a.x).toBeCloseTo(0);
    expect(m.b.x).toBeCloseTo(3.65);
    expect(commitLength(h, left, 3.85)).toBeNull();
    // Only its end free at the start: the start moves.
    const back = W('B', [4, 2], [0, 2], [-0.1, 0.1], { joins: joins([], ['W']) });
    const hb = hostOf(back);
    const d = by(wallDims(hb, [west, south, back], [], {}, { mode: 'faces', length: true }))['l:left'];
    expect(val(d)).toBe(3.85);
    const mb = commitLength(hb, d, 3)!;
    expect(mb.a.x).toBeCloseTo(3.15);
    expect(mb.b.x).toBeCloseTo(0);
  });

  it('without the option, no length dimension (columns, beams, a wall being drawn)', () => {
    expect(wallDims(hostOf(host), room, [], {}, { mode: 'faces' }).some((d) => d.kind === 'length')).toBe(false);
  });

  it('measures across to a parallel wall beyond the corner of the wall it joins: offsets from the interior and exterior corners', () => {
    // The west wall turns at its top into a wall running west, away from the host: beside no part of the host.
    const westT = W('W', [0, 0], [0, 5], [-0.15, 0.15], { joins: joins([], ['T'], ['H']) });
    const top = W('T', [0, 5], [-4, 5], [-0.15, 0.15], { joins: joins(['W']) });
    const d = by(wallDims(hostOf(host), [westT, top, host], [], {}));
    expect(d['x:left'].target.id).toBe('T');
    expect(val(d['x:left'])).toBe(2.85);  // to its inner face: the interior corner
    expect(d['x:left'].station).toBe(0);   // at the host's end by that corner
    const far = by(wallDims(hostOf(host), [westT, top, host], [], { 'x:left': { opt: 1 } }));
    expect(val(far['x:left'])).toBe(3.15); // to its outer face: the exterior corner
    // Not joined round that corner: left out, as before.
    const loose = W('L', [0, 5], [-4, 5]);
    expect(wallDims(hostOf(host), [westT, loose, host], [], {}).some((x) => x.target.id === 'L')).toBe(false);
  });
});
