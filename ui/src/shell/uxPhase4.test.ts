import { describe, expect, it } from 'vitest';
import { evalMathPreview } from '../viewport/PrecisionPill';
import { equalSpacingMagnets, storeyAnchorPoints } from '../viewport/smartMagnets';
import { missionTargets } from '../app/onboarding';
import type { NodeDto } from '../engine/protocol';

const node = (id: string, type: string, storeyId: string | null, props: NodeDto['props'] = {}): NodeDto => ({
  id, type, name: id, storeyId, parentId: null, px: 0, py: 0, module: '', editable: true, props,
});

describe('Phase 4 game-like UX', () => {
  it('evaluates live arithmetic expressions for the unified 3D precision pill', () => {
    expect(evalMathPreview('6000/3')).toBe('= 2000');
    expect(evalMathPreview('2400+350')).toBe('= 2750');
    expect(evalMathPreview('5000')).toBeNull();
    expect(evalMathPreview('100/0')).toBeNull();
  });

  it('detects = | = equal-spacing Smart Magnets along X and Y', () => {
    const nodes = [
      node('c1', 'IfcColumn', 'st1', { x: 0, y: 0 }),
      node('c2', 'IfcColumn', 'st1', { x: 6, y: 0 }),
      node('c3', 'IfcColumn', 'st2', { x: 10, y: 0 }),
    ];
    const anchors = storeyAnchorPoints(nodes, 'st1');
    expect(anchors).toHaveLength(2);
    const hits = equalSpacingMagnets({ x: 3.02, y: 0.05 }, anchors, 0.08);
    expect(hits).toHaveLength(1);
    expect(hits[0].axis).toBe('x');
    expect(hits[0].mid.x).toBe(3);
    expect(equalSpacingMagnets({ x: 2.0, y: 0 }, anchors, 0.08)).toHaveLength(0);
  });

  it('provides role-specific dashed Ghost Targets for Guided Sandbox missions', () => {
    expect(missionTargets('architecture')[0]).toMatchObject({ id: 'target.wall4', kind: 'line', label: '6000 mm' });
    expect(missionTargets('structure').some((t) => t.id === 'target.col4' && t.kind === 'point')).toBe(true);
  });
});
