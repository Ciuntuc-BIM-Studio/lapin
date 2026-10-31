import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../../engine/protocol';
import { frontViewId, makesNative, nativeArgs, shapeFits, solidArgs, solidFieldKeys, threePoints, viewOf } from './thick';
import { viewShowsField } from '../core/planeFields';

const node = (id: string, type: string, props: Record<string, unknown>): NodeDto => ({ id, type, name: id, props } as unknown as NodeDto);

describe("Studio's thick solids", () => {
  it('take only the shapes they are made from', () => {
    expect(shapeFits('ring', { type: 'circle' })).toBe(true);
    expect(shapeFits('ring', { type: 'arc' })).toBe(true);
    expect(shapeFits('ring', { type: 'polyline', closed: true })).toBe(true);
    expect(shapeFits('ring', { type: 'polyline', closed: false })).toBe(false);
    expect(shapeFits('box', { type: 'arc' })).toBe(false);
    expect(shapeFits('box', { type: 'polyline', closed: true })).toBe(true);
    expect(shapeFits('wall', { type: 'line' })).toBe(true);
    expect(shapeFits('wall', { type: 'polyline' })).toBe(true);
    expect(shapeFits('wall', null)).toBe(false);
  });

  it('make native walls on a plan unless a solid is asked for, and solids elsewhere', () => {
    const nodes = new Map([['s1', node('s1', 'LapinView', { kind: 'section' })], ['p1', node('p1', 'LapinView', { kind: 'plan', storeyId: 'st2' })]]);
    expect(viewOf('plan:st1', nodes)).toEqual({ kind: 'plan', storeyId: 'st1' });
    expect(viewOf('p1', nodes)).toEqual({ kind: 'plan', storeyId: 'st2' });
    expect(viewOf('s1', nodes).kind).toBe('section');
    expect(makesNative('plan', false)).toBe(true);
    expect(makesNative('plan', true)).toBe(false);
    expect(makesNative('section', false)).toBe(false);
    expect(makesNative('plane', false)).toBe(false);
  });

  it('send a box its bottom and top, and the walls the wall type', () => {
    const c = { op: 'box' as const, justify: 'outside', thickness: 0.25, height: 2, bottom: true, top: false };
    expect(solidArgs('d1', c)).toEqual({ profileId: 'd1', thickness: 0.25, height: 2, justify: 'outside', bottom: true, top: false });
    expect(solidArgs('d1', { ...c, op: 'wall', justify: 'left' })).toEqual({ profileId: 'd1', thickness: 0.25, height: 2, justify: 'left' });
    const shape = { type: 'polyline', pts: [[0, 0], [4, 0], [4, 3]], closed: true };
    expect(nativeArgs(shape, 'st1', 'wt1', c)).toEqual({ shape, height: 2, justify: 'outside', storeyId: 'st1', typeId: 'wt1', bottom: true, top: false });
    expect(nativeArgs(shape, null, null, { ...c, op: 'ring' })).toEqual({ shape, height: 2, justify: 'outside' });
  });

  it('show the fields of what they are', () => {
    expect(solidFieldKeys(node('a', 'IfcBuildingElementProxy', { operation: 'box' }))).toContain('topThickness');
    expect(solidFieldKeys(node('a', 'IfcBuildingElementProxy', { operation: 'wall' }))).not.toContain('bottom');
    expect(solidFieldKeys(node('a', 'IfcBuildingElementProxy', { operation: 'boxSlab' }))).toEqual(['cutPriority']);
    expect(solidFieldKeys(node('a', 'IfcBuildingElementProxy', { operation: 'extrude' }))).toContain('depth');
    expect(solidFieldKeys(node('a', 'IfcBuildingElementProxy', {}))).toBeNull();
  });
});

describe('reference planes', () => {
  it('show their place only on a plane, the sketch plane depth only on an elevation', () => {
    expect(viewShowsField('plane', 'tilt')).toBe(true);
    expect(viewShowsField('plan', 'tilt')).toBe(false);
    expect(viewShowsField('elevation', 'planeDepth')).toBe(true);
    expect(viewShowsField('section', 'planeDepth')).toBe(false);
    expect(viewShowsField('section', 'depth')).toBe(true);
  });

  it('need three points off a line', () => {
    expect(threePoints([[0, 0, 0], [1, 0, 0], [0, 1, 1]])).toBeNull();
    expect(threePoints([[0, 0, 0], [1, 0, 0], [2, 0, 0]])).not.toBeNull();
    expect(threePoints([[0, 0, 0], [1, 0, 0]])).not.toBeNull();
  });

  it('are made off the view in front', () => {
    expect(frontViewId({ view: 'plan', planViewId: null, storeyId: 'st1' }, null)).toBe('plan:st1');
    expect(frontViewId({ view: 'plan', planViewId: 'v1' }, 'st1')).toBe('v1');
    expect(frontViewId({ view: 'drawing', drawingViewId: 'e1' }, null)).toBe('e1');
    expect(frontViewId({ view: '3d' }, 'st1')).toBeNull();
  });
});
