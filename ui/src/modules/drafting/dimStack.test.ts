import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { dimLineOf, STACK_SPACING_MM, stackedAt, stackSpacingMm } from './dimStack';

const node = (id: string, type: string, props: Record<string, unknown>): NodeDto =>
  ({ id, type, name: id, parentId: null, px: 0, py: 0, props, editable: true, module: null, storeyId: null } as NodeDto);
const dim = (id: string, pts: number[][], at: number[], extra: Record<string, unknown> = {}) =>
  node(id, 'IfcAnnotation', { predefinedType: 'DIMENSION', viewId: 'v', pts, at, ...extra });
const map = (...ns: NodeDto[]) => new Map(ns.map((n) => [n.id, n]));

describe('dimension lines stacked at a set distance (forum topic 236)', () => {
  // At 1:100, 7 mm on paper is 0.7 m; the snap catches within 2 mm (0.2 m).
  const view = node('v', 'LapinView', { scale: 100 });

  it('reads a linear dimension\'s line, and none of an angle', () => {
    const l = dimLineOf(dim('a', [[0, 0], [5, 0]], [2, -1]))!;
    expect(l.u).toEqual({ x: 1, y: 0 });
    expect([l.lo, l.hi]).toEqual([0, 5]);
    expect(dimLineOf(node('x', 'IfcAnnotation', { predefinedType: 'DIMENSION', dimKind: 'angular' }))).toBeNull();
  });

  it('snaps a dragged line at the spacing from a parallel one, on either side, with the guide along it', () => {
    const nodes = map(view, dim('a', [[0, 0], [5, 0]], [2, -1]), dim('b', [[1, 0], [3, 0]], [2, -1.5]));
    const below = stackedAt(nodes, 'b', { x: 2, y: -1.75 })!;
    expect(below.from).toBe('a');
    expect(below.at.x).toBeCloseTo(2);
    expect(below.at.y).toBeCloseTo(-1.7);
    expect(below.guide.a.y).toBeCloseTo(-1);
    expect([below.guide.a.x, below.guide.b.x]).toEqual([0, 5]);
    const above = stackedAt(nodes, 'b', { x: 2, y: -0.4 })!;
    expect(above.at.y).toBeCloseTo(-0.3);
    // Too far from the stacked place: free.
    expect(stackedAt(nodes, 'b', { x: 2, y: -2.5 })).toBeNull();
  });

  it('leaves alone a dimension across, one far along, and one on another view', () => {
    const nodes = map(view,
      dim('b', [[1, 0], [3, 0]], [2, -1.5]),
      dim('across', [[0, 0], [0, 5]], [-1, 2]),
      dim('far', [[20, 0], [25, 0]], [22, -1]),
      dim('other', [[0, 0], [5, 0]], [2, -1], { viewId: 'w' }));
    expect(stackedAt(nodes, 'b', { x: 2, y: -1.7 })).toBeNull();
  });

  it('takes the spacing from the dimension rules, else 7 mm', () => {
    expect(stackSpacingMm(map(view))).toBe(STACK_SPACING_MM);
    const project = node('p', 'IfcProject', { dimensionRules: { exterior: { spacing: 10 } } });
    expect(stackSpacingMm(map(project))).toBe(10);
    const nodes = map(view, project, dim('a', [[0, 0], [5, 0]], [2, -1]), dim('b', [[1, 0], [3, 0]], [2, -1.5]));
    expect(stackedAt(nodes, 'b', { x: 2, y: -2.05 })!.at.y).toBeCloseTo(-2);
  });
});
