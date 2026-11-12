import { describe, expect, it } from 'vitest';
import { alongFor, alongOf, arcDimension, at, chooseSide, faceOn, faceScale, fitsAlong, sillFor, targetsOn, tempDims, toView, verticalDims, type OpeningRefs } from './openingDims';

// A 300 mm wall 6 m along +X (faces at ±0.15), a corner wall at its end turning left (its inner face at 5.85, outer 6.15),
// a T on its left face at 3 m (2.85 … 3.15), a layered T at 4.5 m on its right face; a 1.2 m window at 1.5 m and a door at 4 m.
const refs = (over: Partial<OpeningRefs> = {}): OpeningRefs => ({
  wallId: 'w', length: 6, start: [0, 0], dir: [1, 0], faces: { left: 0.15, right: -0.15 },
  ends: { left: [0, 5.85], right: [0, 6.15] },
  opening: { id: 'o', along: 1.5, width: 1.2 },
  walls: [
    { id: 'corner', name: 'Corner', sides: ['left', 'right'], left: { faces: [5.85, 6.15], axis: 6 }, right: { faces: [5.85, 6.15], axis: 6 } },
    { id: 't', name: 'Partition', sides: ['left'], left: { faces: [3.15, 2.85], axis: 3 }, right: { faces: [2.85, 3.15], axis: 3 } },
  ],
  openings: [{ id: 'd', name: 'D1', along: 4, width: 0.9 }],
  ...over,
});

describe("a door's or window's temporary dimensions", () => {
  it('go on the face more walls meet', () => {
    expect(chooseSide(refs())).toBe('left');
    const r = refs();
    r.walls![1].sides = ['right'];
    r.walls!.push({ id: 'x', name: 'X', sides: ['right'], left: { faces: [4.4, 4.6], axis: 4.5 }, right: { faces: [4.4, 4.6], axis: 4.5 } });
    expect(chooseSide(r)).toBe('right');
  });

  it("measure each edge to the nearest wall face or the wall's end, and to a neighbouring opening when it is nearer", () => {
    const dims = tempDims(refs(), 'left', 1.5, 1.2);
    // Towards the start: nothing meets the wall there, so its end, 0.9 from the edge at 0.9.
    const start = dims.find((d) => d.way === -1)!;
    expect(start.target.kind).toBe('end');
    expect(start.value).toBeCloseTo(0.9, 9);
    // Towards the end: the T's near face at 2.85, 750 from the edge at 2.1; the door (3.55) is further, so no second row.
    const end = dims.filter((d) => d.way === 1);
    expect(end).toHaveLength(1);
    expect(end[0].target.key).toBe('wall:t');
    expect(end[0].refS).toBeCloseTo(2.85, 9);
    expect(end[0].value).toBeCloseTo(0.75, 9);
  });

  it('add the neighbouring opening as a second row when it is nearer than the wall face', () => {
    // The door at 4 m, the window at 5.2 m (its edge at 4.6): towards the start the door's near edge (4.45) before the T (3.15).
    const dims = tempDims(refs(), 'left', 5.2, 1.2);
    const back = dims.filter((d) => d.way === -1);
    expect(back.map((d) => [d.row, d.target.key])).toEqual([[0, 'wall:t'], [1, 'opening:d']]);
    expect(back[0].value).toBeCloseTo(1.45, 9);
    expect(back[1].value).toBeCloseTo(0.15, 9);
  });

  it("cycle the reference: the corner's inner face, then its outer face, then its axis; the wall's end at the corner is that face, not a target of its own", () => {
    const r = refs({ walls: [refs().walls![0]], openings: [] });
    const ts = targetsOn(r, 'left', 1, 2.1);
    expect(ts.map((x) => x.key)).toEqual(['wall:corner']);
    expect(ts[0].options.map((o) => [o.what, o.s])).toEqual([['nearFace', 5.85], ['farFace', 6.15], ['axis', 6]]);
    const far = tempDims(r, 'left', 1.5, 1.2, { '1:0': { opt: 1 } }).find((d) => d.way === 1)!;
    expect(far.target.options[far.opt].what).toBe('farFace');
    expect(far.value).toBeCloseTo(6.15 - 2.1, 9);
    // Past the last option it comes round to the first.
    expect(tempDims(r, 'left', 1.5, 1.2, { '1:0': { opt: 3 } }).find((d) => d.way === 1)!.opt).toBe(0);
  });

  it("offer a layered wall's core faces between its faces and its axis, and another opening's edges and centre", () => {
    const r = refs({ walls: [{ id: 'l', name: 'Layered', sides: ['left'], left: { faces: [3, 3.29], core: [3.02, 3.27], axis: 3.145 }, right: { faces: [3, 3.29], axis: 3.145 } }] });
    const t = targetsOn(r, 'left', 1, 2.1)[0];
    expect(t.options.map((o) => o.what)).toEqual(['nearFace', 'farFace', 'nearCore', 'farCore', 'axis']);
    expect(t.options[2].s).toBe(3.02);
    const d = targetsOn(refs(), 'left', 1, 2.1).find((x) => x.kind === 'opening')!;
    expect(d.options.map((o) => [o.what, o.s])).toEqual([['nearEdge', 3.55], ['centre', 4], ['farEdge', 4.45]]);
  });

  it('leave out a wall the opening stands across', () => {
    // A T at 1.5 m on the other face, behind the window: neither side's reference.
    const r = refs({ walls: [{ id: 'behind', name: 'B', sides: ['right'], left: { faces: [1.35, 1.65], axis: 1.5 }, right: { faces: [1.35, 1.65], axis: 1.5 } }], openings: [] });
    expect(targetsOn(r, 'left', -1, 0.9).map((x) => x.key)).toEqual(['end:start']);
    expect(targetsOn(r, 'left', 1, 2.1).map((x) => x.key)).toEqual(['end:end']);
  });

  it('move the opening so that a typed value reads, the reference staying put', () => {
    // "This window's corner 1500 from the T's face": towards the end, its edge 1.5 short of 2.85.
    const end = tempDims(refs(), 'left', 1.5, 1.2).find((d) => d.way === 1)!;
    const along = alongFor(end, 1.5, 1.2);
    expect(along).toBeCloseTo(2.85 - 1.5 - 0.6, 9);
    expect(tempDims(refs(), 'left', along, 1.2).find((d) => d.way === 1)!.value).toBeCloseTo(1.5, 9);
    // "This window's axis 2500 from the door's axis": the door's centre, the window's centre.
    const r = refs();
    const toDoor = tempDims(r, 'left', 5.2, 1.2, { '-1:1': { opt: 1, own: 'centre' } }).find((d) => d.key === '-1:1')!;
    expect(toDoor.refS).toBe(4);
    expect(toDoor.value).toBeCloseTo(1.2, 9);
    expect(alongFor(toDoor, 2.5, 1.2)).toBeCloseTo(6.5, 9);
    // From the start: edge 0.9 from the wall's end.
    const start = tempDims(r, 'left', 1.5, 1.2).find((d) => d.way === -1)!;
    expect(alongFor(start, 0.3, 1.2)).toBeCloseTo(0.9, 9);
  });

  it("tell whether the opening still fits between the wall's ends", () => {
    expect(fitsAlong(0.6, 1.2, 6)).toBe(true);
    expect(fitsAlong(0.5, 1.2, 6)).toBe(false);
    expect(fitsAlong(5.5, 1.2, 6)).toBe(false);
  });

  it("place points by the wall's frame, a turned wall too", () => {
    const r = refs({ start: [10, 5], dir: [0, 1] });
    const p = at(r, 2, 0.15);
    expect(p.x).toBeCloseTo(9.85, 9);
    expect(p.y).toBeCloseTo(7, 9);
    expect(alongOf(r, p)).toBeCloseTo(2, 9);
  });

  // An arc from (0, 0) to (8, 0) rising 2 m to its left: radius 5 about (4, −3), 5·4·atan(½) long; its left face (outside) at radius 5.15.
  const L = 20 * Math.atan(0.5);
  const arc = (): OpeningRefs => ({
    wallId: 'a', curved: true, length: L, start: [0, 0], end: [8, 0], rise: 2, centre: [4, -3], radius: 5,
    faces: { left: 0.15, right: -0.15 }, scale: { left: 1.03, right: 0.97 }, ends: { left: [0, L], right: [0, L] },
    opening: { id: 'o', along: 2, width: 1.2 }, walls: [], openings: [],
  });

  it('measure along a curved wall on the face they are drawn on, and move the opening by the axis length that makes', () => {
    const r = arc();
    expect(faceScale(r, 'left')).toBe(1.03);
    const start = tempDims(r, 'left', 2, 1.2).find((d) => d.way === -1)!;
    // From the wall's start to the edge at 1.4 along the axis: 1.4 · 1.03 on the outside face.
    expect(start.value).toBeCloseTo(1.4 * 1.03, 9);
    const along = alongFor(start, 1.03, 1.2, 1.03);
    expect(along).toBeCloseTo(1.6, 9);
    expect(tempDims(r, 'left', along, 1.2).find((d) => d.way === -1)!.value).toBeCloseTo(1.03, 9);
  });

  it('place points round a curved wall, its faces at their own radius, and read them back', () => {
    const r = arc();
    const top = at(r, L / 2, 0);
    expect(top.x).toBeCloseTo(4, 9);
    expect(top.y).toBeCloseTo(2, 9);
    const out = at(r, L / 2, 0.15);
    expect(out.y).toBeCloseTo(2.15, 9);
    expect(at(r, 0, 0).x).toBeCloseTo(0, 9);
    expect(alongOf(r, { x: 4, y: 7 })).toBeCloseTo(L / 2, 9);
    expect(alongOf(r, at(r, 1.3, -0.1))).toBeCloseTo(1.3, 9);
    // Made permanent: the arc's length on the outside face between the two places.
    const a = arcDimension(r, 'left', 0, L / 2)!;
    expect(a.r).toBeCloseTo(5.15, 9);
    expect(a.r * Math.abs(a.sweep)).toBeCloseTo((L / 2) * 1.03, 9);
  });

  it('see a wall face-on in an elevation along it, from either side, and not one cut across or curved', () => {
    const r = refs();
    // Looking north (+y) at the wall along x: its right face (−y) faces the viewer.
    const north = { origin: [0, -10, 0], right: [1, 0, 0], up: [0, 0, 1], dir: [0, 1, 0] };
    expect(faceOn(r, north)).toBe('right');
    expect(faceOn(r, { origin: [0, 10, 0], right: [-1, 0, 0], up: [0, 0, 1], dir: [0, -1, 0] })).toBe('left');
    expect(faceOn(r, { origin: [-10, 0, 0], right: [0, 1, 0], up: [0, 0, 1], dir: [1, 0, 0] })).toBeNull();
    expect(faceOn(r, { origin: [0, 0, 0], right: [Math.cos(0.5), Math.sin(0.5), 0], up: [0, 0, 1], dir: [-Math.sin(0.5), Math.cos(0.5), 0] })).toBeNull();
    expect(faceOn(arc(), north)).toBeNull();
    expect(toView(north, { x: 3, y: 0.15 }, 2.4)).toEqual({ x: 3, y: 2.4 });
  });

  it("give an opening's sill and head heights over its storey's level, and the sill that makes a typed one", () => {
    const r = refs({ levels: { storey: 3, base: 3 }, opening: { id: 'o', along: 1.5, width: 1.2, sill: 0.9, bottom: 3.9, top: 5.4, fill: 'w' } });
    const [sill, head] = verticalDims(r);
    expect(sill.value).toBeCloseTo(0.9, 9);
    expect(head.value).toBeCloseTo(2.4, 9);
    expect(sillFor(r, sill, 1.1)).toBeCloseTo(1.1, 9);
    // A head at 2100: the window down by 300.
    expect(sillFor(r, head, 2.1)).toBeCloseTo(0.6, 9);
    expect(verticalDims(refs())).toEqual([]);
  });
});
