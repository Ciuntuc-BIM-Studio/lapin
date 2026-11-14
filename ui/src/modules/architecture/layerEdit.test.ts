import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { crossing, droppedOffset, layerGrips, outlineInView, placeEdge, type LayerEdges } from './layerEdit';
import { extentRows, removeArgs } from './layerExtents';

// A wall along x from 0 to 5, 3 m high: its first layer (y −0.14 to −0.125) and an end of it, as architecture.layerEdges tells them.
const layer = { layer: 0, name: 'Plaster', footprint: [[0, -0.14], [5, -0.14], [5, -0.125], [0, -0.125]], z0: 0, z1: 3 };
const end = { layer: 0, name: 'Plaster', edge: 'end', line: [[5, -0.1325, 0], [5, -0.1325, 3]], dir: [1, 0, 0], offset: 0, own: false };
const top = { layer: 0, name: 'Plaster', edge: 'top', line: [[0, -0.1325, 3], [5, -0.1325, 3]], dir: [0, 0, 1], offset: -0.1, own: true };
const data: LayerEdges = { layers: [layer], grips: [end, top] };
const node = { id: 'w', type: 'IfcWall', props: {}, editable: true } as unknown as NodeDto;
// A section across the wall at x = 2.5, looking along +x... and one along it, at y = −0.13, looking to +y (x to the right, z up).
const across = { origin: [2.5, -3, 0], right: [0, 1, 0], up: [0, 0, 1], dir: [-1, 0, 0] };
const along = { origin: [0, -0.13, 0], right: [1, 0, 0], up: [0, 0, 1], dir: [0, 1, 0] };

describe('edit layers (forum topic 89, stage 2)', () => {
  it('shows in a plan the edges that move sideways, at their middle', () => {
    expect(placeEdge(end, layer, {})).toEqual({ at: { x: 5, y: -0.1325 }, along: { x: 1, y: 0 }, per: 1, depth: 0 });
    expect(placeEdge(top, layer, {})).toBeNull();
  });

  it('shows in a section the edges moving in its plane, where the cut crosses them or runs along their layer', () => {
    // Across the wall: the top, where the cut crosses it, at the wall's y (3 m up); the end does not move in the cut's plane.
    const t = placeEdge(top, layer, { frame: across, kind: 'section' })!;
    expect(t.at.x).toBeCloseTo(2.8675);
    expect(t.at.y).toBeCloseTo(3);
    expect(t.along).toEqual({ x: 0, y: 1 });
    expect(placeEdge(end, layer, { frame: across, kind: 'section' })).toBeNull();
    // Along the wall, through the plaster: its end at 5 along, 1.5 up.
    const e = placeEdge(end, layer, { frame: along, kind: 'section' })!;
    expect(e.at).toEqual({ x: 5, y: 1.5 });
    // Beside the plaster: not cut, not shown; a developed section shows none.
    expect(placeEdge(end, layer, { frame: { ...along, origin: [0, 1, 0] }, kind: 'section' })).toBeNull();
    expect(placeEdge(end, layer, { frame: along, kind: 'section', developed: true })).toBeNull();
    expect(crossing(across, [[0, 0, 0], [1, 0, 0]])).toBeNull();
  });

  it('gives the offset a drag lands on, rounded, within a metre', () => {
    expect(droppedOffset(0, { x: 5, y: 0 }, { x: 1, y: 0 }, 1, { x: 5.02, y: 0.3 })).toBe(0.02);
    expect(droppedOffset(-0.1, { x: 0, y: 3 }, { x: 0, y: 1 }, 1, { x: 0, y: 2.95 })).toBe(-0.15);
    expect(droppedOffset(0.9, { x: 0, y: 0 }, { x: 1, y: 0 }, 1, { x: 0.5, y: 0 })).toBe(1);
  });

  it('makes grips whose drop is one setLayerExtent, its cut left as it is', () => {
    const plan = layerGrips(node, data, undefined);
    expect(plan.map((g) => g.key)).toEqual(['layer:0:end:']);
    expect(plan[0].step!({ x: 5.05, y: 0 })).toEqual({ command: 'architecture.setLayerExtent', args: { id: 'w', layer: 0, edge: 'end', offset: 0.05 } });
    expect(plan[0].step!({ x: 5, y: 0.2 })).toBeNull();
    expect(plan[0].constrain!({ x: 5.05, y: 2 }, false)).toEqual({ x: 5.05, y: -0.1325 });
    const section = layerGrips(node, data, { view: 's1', frame: along, kind: 'section' });
    expect(section.map((g) => [g.key, g.view])).toEqual([['layer:0:end:', 's1'], ['layer:0:top:', 's1']]);
    expect(section[1].step!({ x: 2.5, y: 3.2 })).toEqual({ command: 'architecture.setLayerExtent', args: { id: 'w', layer: 0, edge: 'top', offset: 0.1 } });
  });

  it('keeps in an elevation only the nearest of the edges seen at one point', () => {
    const behind = { ...end, layer: 1, line: [[5, 0.1325, 0], [5, 0.1325, 3]] };
    const seen = layerGrips(node, { layers: [layer], grips: [behind, end] }, { view: 'e', frame: along, kind: 'elevation' });
    expect(seen.map((g) => g.key)).toEqual(['layer:0:end:']);
  });

  it('draws a layer as its footprint in a plan, where a section cuts it, and as seen in an elevation', () => {
    expect(outlineInView(layer, {})).toHaveLength(1);
    const cut = outlineInView(layer, { frame: across, kind: 'section' });
    expect(cut).toHaveLength(1);
    expect(cut[0][0].x).toBeCloseTo(2.86);
    expect(cut[0][1].x).toBeCloseTo(2.875);
    expect(cut[0][2].y).toBe(3);
    expect(outlineInView(layer, { frame: along, kind: 'elevation' })[0].map((p) => p.x)).toEqual([0, 5, 5, 0]);
  });
});

describe('layer extents from a type (stage 4)', () => {
  it('shows the type\'s that the element does not set, then its own with the type\'s there', () => {
    const fromType = [{ layer: 0, edge: 'bottom', offset: 0.02 }, { layer: 0, edge: 'top', offset: 0.01 }];
    const own = [{ layer: 0, edge: 'top', offset: 0 }];
    expect(extentRows(own, fromType)).toEqual([
      { x: fromType[0], fromType: true },
      { x: own[0], fromType: false, typeOffset: 0.01 },
    ]);
  });

  it('takes an own extent away back to the type\'s where it has one', () => {
    expect(removeArgs('w', { layer: 0, edge: 'top', offset: 0 }, true)).toEqual({ id: 'w', layer: 0, edge: 'top', inherit: true });
    expect(removeArgs('s', { layer: 1, edge: 'edge', index: 2, offset: 0.1, cuts: 'order' }, false)).toEqual({ id: 's', layer: 1, edge: 'edge', offset: 0, cuts: 'order', index: 2 });
  });
});
