import { afterEach, describe, expect, it, vi } from 'vitest';
import type { NodeDto, RelationDto } from '../../engine/protocol';
import { useUi } from '../../app/store';
import { liveBegin, liveCommit, liveEnd, liveTaken, livePoint, useLive } from '../../viewport/liveInput';
import { SCALE_IMAGE_TOOL, scalableTrace, scaleTarget, startScaleImage, trueDistance } from './scaleImage';

const node = (id: string, type: string, props: Record<string, unknown> = {}) => [id, { id, type, name: id, props, editable: true } as unknown as NodeDto] as const;
const nodes = new Map<string, NodeDto>([
  node('img', 'LapinViewImage'),
  node('line', 'IfcAnnotation', { viewId: 'plan:s' }),
  node('pdf', 'IfcGroup'),
  node('wall', 'IfcWall'),
  node('mixed', 'IfcGroup'),
  node('empty', 'IfcGroup'),
]);
const rel = (id: string, relating: string, related: string[]): [string, RelationDto] => [id, { id, class: 'IfcRelAssignsToGroup', relating, related }];
const relations = new Map<string, RelationDto>([rel('r1', 'pdf', ['img', 'line']), rel('r2', 'mixed', ['wall'])]);

describe('Scale to a known distance (forum 143)', () => {
  afterEach(() => { liveEnd(); useUi.setState({ tool: null, selected: [], selection: null }); });

  it('takes an image, or a group of images and drafting (an imported PDF), and nothing else', () => {
    expect(scalableTrace('img', nodes, relations)).toBe(true);
    expect(scalableTrace('pdf', nodes, relations)).toBe(true);
    expect(scalableTrace('mixed', nodes, relations)).toBe(false);
    expect(scalableTrace('empty', nodes, relations)).toBe(false);
    expect(scalableTrace('line', nodes, relations)).toBe(false);
    // An image selected comes first; a PDF's group otherwise.
    expect(scaleTarget(['pdf', 'img'], nodes, relations)).toBe('img');
    expect(scaleTarget(['wall', 'pdf'], nodes, relations)).toBe('pdf');
    expect(scaleTarget(['wall', 'mixed'], nodes, relations)).toBeNull();
  });

  it('uses the distance typed at the cursor, else asks for it', () => {
    const ask = vi.fn(() => '50m');
    const parse = (s: string) => (s === '50m' ? 50 : null);
    expect(trueDistance(12, ask, parse)).toBe(12);
    expect(ask).not.toHaveBeenCalled();
    expect(trueDistance(undefined, ask, parse)).toBe(50);
    expect(trueDistance(undefined, () => null, parse)).toBeNull();
    expect(trueDistance(0, ask, parse)).toBeNull();
  });

  it('starts on what was imported, selected', () => {
    startScaleImage('pdf');
    expect(useUi.getState()).toMatchObject({ tool: SCALE_IMAGE_TOOL, selected: ['pdf'], selection: 'pdf' });
  });

  it('measures: the second point stays the cursor\'s, the length typed is the true distance', () => {
    const commit = vi.fn();
    liveBegin('tool', { x: 0, y: 0 }, commit, { measure: true });
    useLive.setState({ cursor: { x: 3, y: 4 }, len: '50m' });
    expect(livePoint()).toEqual({ x: 3, y: 4 });
    expect(liveCommit()).toBe(true);
    expect(commit).toHaveBeenCalledWith({ x: 3, y: 4 }, 50, undefined);
    useLive.setState({ len: '2400' });
    expect(liveTaken({ x: 1, y: 1 })).toEqual({ point: { x: 1, y: 1 }, typed: 2.4 });
    // Drawing (not measuring), the typed length puts the point.
    liveBegin('tool', { x: 0, y: 0 }, commit);
    useLive.setState({ cursor: { x: 3, y: 4 }, len: '10000' });
    expect(livePoint()!.x).toBeCloseTo(6, 9);
  });
});
