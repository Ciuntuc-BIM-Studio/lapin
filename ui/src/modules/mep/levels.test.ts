import { describe, expect, it } from 'vitest';
import type { Defaults } from './network';
import { SAME, change, elevationLevel, planeLevel, redrawLegs, resolve, runHeights, squared, syncLegs } from './levels';

const D = { plane: 'floor', floorElevation: 0.2, ceilingElevation: 2.4, wallElevation: 1.1 } as Defaults;
const P = (x: number, y: number) => ({ x, y });

describe('the height a run is drawn at', () => {
  it("reads a plane's elevation from the system, an elevation typed as it is, and the system's plane for none", () => {
    expect(resolve(planeLevel('ceiling'), D)).toEqual({ plane: 'ceiling', z: 2.4 });
    expect(resolve(planeLevel('wall'), D)).toEqual({ plane: 'wall', z: 1.1 });
    expect(resolve(elevationLevel(0.75), D)).toEqual({ plane: null, z: 0.75 });
    expect(resolve(SAME, D)).toEqual({ plane: 'floor', z: 0.2 });
    expect(resolve(planeLevel('wall'), null)).toEqual({ plane: 'wall', z: null });
  });

  it('keeps each leg at the height it was drawn at, starts a new path afresh and drops a leg taken back', () => {
    const ceiling = planeLevel('ceiling');
    let s = { ...syncLegs({ current: SAME, legs: [], first: null }, [P(0, 0)], () => planeLevel('floor')), chosen: false };
    expect(s.current).toEqual(planeLevel('floor'));
    s = { ...syncLegs(s, [P(0, 0), P(3, 0)], () => SAME), chosen: false };
    expect(s.legs).toEqual([planeLevel('floor')]);
    // C after the second point: the leg being drawn is on the ceiling, the one before stays on the floor.
    s = { ...syncLegs({ ...s, current: ceiling }, [P(0, 0), P(3, 0), P(6, 0)], () => SAME), chosen: false };
    expect(s.legs).toEqual([planeLevel('floor'), ceiling]);
    expect(syncLegs(s, [P(0, 0), P(3, 0)], () => SAME).legs).toEqual([planeLevel('floor')]);
    // Another first point: a new path, at the start's height.
    expect(syncLegs(s, [P(9, 9)], () => planeLevel('wall'))).toEqual({ current: planeLevel('wall'), legs: [], first: P(9, 9) });
    // A height chosen right after the first click, before the path was seen: kept.
    expect(syncLegs({ ...s, current: ceiling, chosen: true }, [P(9, 9)], () => planeLevel('wall')).current).toBe(ceiling);
  });

  it("sends a run on one plane as before, and one whose plane changed with a level per point, [in, out] at the rise", () => {
    expect(runHeights(3, [planeLevel('ceiling'), planeLevel('ceiling')], D)).toEqual({ plane: 'ceiling' });
    expect(runHeights(2, [SAME], D)).toEqual({});
    expect(runHeights(2, [elevationLevel(0.5)], D)).toEqual({ elevation: 0.5 });
    // Floor, then ceiling from the second point: the run's own plane the floor; the rise at point 1.
    expect(runHeights(4, [planeLevel('floor'), planeLevel('ceiling'), planeLevel('ceiling')], D))
      .toEqual({ plane: 'floor', levels: [null, [null, 2.4], 2.4, 2.4] });
    // Up to the ceiling and back down to the wall.
    expect(runHeights(4, [SAME, planeLevel('ceiling'), planeLevel('wall')], D)).toEqual({ levels: [null, [null, 2.4], [2.4, 1.1], 1.1] });
  });

  it("says a redrawn run's levels from its own elevation, and none when all its legs are at it", () => {
    expect(runHeights(3, [SAME, SAME], D, 0.2)).toEqual({});
    expect(runHeights(3, [planeLevel('ceiling'), planeLevel('ceiling')], D, 0.2)).toEqual({ levels: [2.4, 2.4, 2.4] });
    expect(runHeights(3, [SAME, planeLevel('wall')], D, 0.2)).toEqual({ levels: [null, [null, 1.1], 1.1] });
  });

  it("sends a redrawn path's changed legs only, the ones before the first change left to the old run's levels", () => {
    const own = planeLevel('floor');
    expect(redrawLegs([own, own], own, D)).toBeNull();
    expect(redrawLegs([own, planeLevel('ceiling'), planeLevel('ceiling')], own, D)).toEqual([null, 2.4, 2.4]);
    // Up and back down to the run's own plane: from the change on, every leg is said.
    expect(redrawLegs([own, planeLevel('ceiling'), own], own, D)).toEqual([null, 2.4, 0.2]);
    expect(redrawLegs([elevationLevel(1), elevationLevel(1)], own, D)).toEqual([1, 1]);
  });

  it('shows the rise or drop at the last point', () => {
    expect(change({ current: planeLevel('ceiling'), legs: [planeLevel('floor')] }, D)).toBeCloseTo(2.2);
    expect(change({ current: planeLevel('floor'), legs: [planeLevel('wall')] }, D)).toBeCloseTo(-0.9);
    expect(change({ current: planeLevel('floor'), legs: [] }, D)).toBeNull();
    expect(change({ current: SAME, legs: [planeLevel('floor')] }, D)).toBeNull();
  });

  it('keeps a leg square to the axes when no laying rule says otherwise', () => {
    expect(squared([P(0, 0)], P(3, 0.4))).toEqual(P(3, 0));
    expect(squared([P(0, 0)], P(0.4, -3))).toEqual(P(0, -3));
    expect(squared([], P(1, 1))).toEqual(P(1, 1));
  });
});
