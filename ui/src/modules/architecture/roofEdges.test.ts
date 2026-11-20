import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { groupGrips } from '../../viewport/grips';
import { mergeEdge, nextRoofEdgeKind, resolveRoofEdgeKinds, roofSlopeGrips } from './roofEdges';

const rect = [
  { x: 0, y: 0 },
  { x: 8, y: 0 },
  { x: 8, y: 4 },
  { x: 0, y: 4 },
];

function makeRoof(props: Record<string, unknown>): NodeDto {
  return {
    id: 'roof-1',
    type: 'IfcRoof',
    name: 'Roof',
    storeyId: 'storey-1',
    editable: true,
    props,
  } as unknown as NodeDto;
}

describe('mergeEdge', () => {
  it('gives one entry per side, the others as the roof', () => {
    expect(mergeEdge([], 3, 1, { kind: 'gable' })).toEqual([null, { kind: 'gable' }, null]);
  });
  it('keeps the edge’s other fields and drops the ones set to undefined', () => {
    expect(mergeEdge([{ kind: 'eave', pitch: 30 }], 1, 0, { overhang: 0.4 })).toEqual([{ kind: 'eave', pitch: 30, overhang: 0.4 }]);
    expect(mergeEdge([{ kind: 'eave', pitch: 30 }], 1, 0, { pitch: undefined })).toEqual([{ kind: 'eave' }]);
  });
  it('an edge with nothing of its own left, or reset, is null', () => {
    expect(mergeEdge([{ pitch: 30 }], 1, 0, { pitch: undefined })).toEqual([null]);
    expect(mergeEdge([{ pitch: 30 }, { kind: 'gable' }], 2, 1, null)).toEqual([{ pitch: 30 }, null]);
  });
  it('drops entries past the outline’s sides', () => {
    expect(mergeEdge([null, null, { kind: 'gable' }], 2, 0, { kind: 'eave' })).toEqual([{ kind: 'eave' }, null]);
  });
});

describe('resolveRoofEdgeKinds', () => {
  it('makes all edges eave for hip and mansard roofs', () => {
    expect(resolveRoofEdgeKinds('hip', rect)).toEqual(['eave', 'eave', 'eave', 'eave']);
    expect(resolveRoofEdgeKinds('mansard', rect)).toEqual(['eave', 'eave', 'eave', 'eave']);
  });

  it('marks short ends as gable on a gable rectangle', () => {
    expect(resolveRoofEdgeKinds('gable', rect)).toEqual(['eave', 'gable', 'eave', 'gable']);
  });

  it('respects ridgeDirection on a quad gable roof', () => {
    expect(resolveRoofEdgeKinds('gable', rect, 'y')).toEqual(['gable', 'eave', 'gable', 'eave']);
  });

  it('keeps one eave pair on a square gable roof', () => {
    const square = [{ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 4, y: 4 }, { x: 0, y: 4 }];
    expect(resolveRoofEdgeKinds('gable', square)).toEqual(['gable', 'eave', 'gable', 'eave']);
  });

  it('marks one low edge as eave and others as gable on a shed roof, flipping across when shedFlip is true', () => {
    expect(resolveRoofEdgeKinds('shed', rect)).toEqual(['eave', 'gable', 'gable', 'gable']);
    expect(resolveRoofEdgeKinds('shed', rect, 'auto', -1, true)).toEqual(['gable', 'gable', 'eave', 'gable']);
  });
});

describe('nextRoofEdgeKind', () => {
  it('toggles between eave and gable and returns undefined when back to auto', () => {
    expect(nextRoofEdgeKind('eave', undefined)).toBe('gable');
    expect(nextRoofEdgeKind('eave', 'gable')).toBeUndefined();
    expect(nextRoofEdgeKind('gable', undefined)).toBe('eave');
    expect(nextRoofEdgeKind('gable', 'eave')).toBeUndefined();
    expect(nextRoofEdgeKind('eave', 'eave')).toBe('gable');
  });
});

describe('roofSlopeGrips', () => {
  const contour = rect.map((p) => [p.x, p.y]);

  it('returns no slope indicators for flat or barrel roofs', () => {
    expect(roofSlopeGrips(makeRoof({ shape: 'flat', contour }))).toEqual([]);
    expect(roofSlopeGrips(makeRoof({ shape: 'barrel', contour }))).toEqual([]);
  });

  it('places inward slope arrows with pitch on eaves and G markers on gables', () => {
    const grips = roofSlopeGrips(makeRoof({ shape: 'gable', pitch: 35, contour }));
    expect(grips).toHaveLength(4);
    expect(grips.map((g) => g.slope)).toEqual([
      { kind: 'eave', override: false },
      { kind: 'gable', override: false },
      { kind: 'eave', override: false },
      { kind: 'gable', override: false },
    ]);
    expect(grips.map((g) => g.label)).toEqual(['35°', 'G', '35°', 'G']);
    // Edge 0 is bottom (0,0)->(8,0): midpoint (4,0), inward normal (0, 1).
    expect(grips[0].at).toEqual({ x: 4, y: 0 });
    expect(grips[0].dir).toEqual({ x: 0, y: 1 });
    // Edge 1 is right (8,0)->(8,4): midpoint (8,2), inward normal (-1, 0).
    expect(grips[1].at).toEqual({ x: 8, y: 2 });
    expect(grips[1].dir).toEqual({ x: -1, y: 0 });
  });

  it('applies per-edge overrides and toggles edge kind via architecture.updateRoof', () => {
    const roof = makeRoof({
      shape: 'gable',
      pitch: 30,
      contour,
      edges: [{ pitch: 42 }, { kind: 'eave' }, null, null],
    });
    const grips = roofSlopeGrips(roof);
    expect(grips[0].label).toBe('42°');
    expect(grips[0].slope).toEqual({ kind: 'eave', override: false });
    expect(grips[1].slope).toEqual({ kind: 'eave', override: true });
    expect(grips[1].label).toBe('30°');

    // Clicking edge 0 (auto eave, pitch 42) sets kind: 'gable' while keeping its pitch.
    expect(grips[0].click?.()).toEqual({
      command: 'architecture.updateRoof',
      args: { id: 'roof-1', edges: [{ pitch: 42, kind: 'gable' }, { kind: 'eave' }, null, null] },
    });
    // Clicking edge 1 (override eave on a gable end) resets it back to auto (null).
    expect(grips[1].click?.()).toEqual({
      command: 'architecture.updateRoof',
      args: { id: 'roof-1', edges: [{ pitch: 42 }, null, null, null] },
    });
  });

  it('stands a curved edge\'s indicator on its arc, not inside the roof at its chord\'s middle', () => {
    // Edge 0 bulges 1.5 m out of the roof (below y = 0 on this anticlockwise outline).
    const grips = roofSlopeGrips(makeRoof({ shape: 'hip', pitch: 30, contour, arcs: [-1.5, 0, 0, 0] }));
    expect(grips[0].at.x).toBeCloseTo(4, 9);
    expect(grips[0].at.y).toBeCloseTo(-1.5, 9);
    expect(grips[0].dir).toEqual({ x: 0, y: 1 });
    expect(grips[1].at).toEqual({ x: 8, y: 2 });
  });

  it('keeps slope grips separate from edgeMid grips at the same point', () => {
    const grips = roofSlopeGrips(makeRoof({ shape: 'hip', pitch: 30, contour }));
    const edgeMid = { key: 'e0', kind: 'edgeMid' as const, at: grips[0].at, owner: 'roof-1' };
    const grouped = groupGrips([edgeMid, grips[0]], 0.1);
    expect(grouped).toHaveLength(2);
  });
});
