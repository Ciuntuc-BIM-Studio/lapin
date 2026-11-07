import { describe, expect, it } from 'vitest';
import { tapeOutcome } from './direct';
import { guideSegment, guidesInPlane, guidesOf, GUIDE_REACH } from './edges';
import { levelFrame } from '../workPlane';
import { SKETCHUP_KEYS, sketchupKeyFor } from '../Sketch3D';
import studio from '../../modules/studio';
import { readDisplay } from '../../app/display';

describe('stage C of the 3D sketch (decision 376)', () => {
  it('the tape measure makes a guide line parallel to an edge, or measures to a guide point', () => {
    const edge = { a: { x: 0, y: 0 }, b: { x: 4, y: 0 } };
    expect(tapeOutcome({ x: 1, y: 0 }, edge, { x: 2, y: 0.75 }, null)).toEqual({ kind: 'guideLine', a: { x: 0, y: 0.75 }, b: { x: 4, y: 0.75 }, distance: 0.75 });
    // Typed, on the cursor's side (below the edge).
    const t = tapeOutcome({ x: 1, y: 0 }, edge, { x: 2, y: -3 }, 0.5);
    expect(t.kind === 'guideLine' && t.a).toEqual({ x: 0, y: -0.5 });
    expect(tapeOutcome({ x: 0, y: 0 }, null, { x: 3, y: 4 }, null)).toEqual({ kind: 'guidePoint', from: { x: 0, y: 0 }, at: { x: 3, y: 4 }, distance: 5 });
    const p = tapeOutcome({ x: 0, y: 0 }, null, { x: 0, y: 9 }, 2);
    expect(p.kind === 'guidePoint' && p.at).toEqual({ x: 0, y: 2 });
    expect(tapeOutcome({ x: 0, y: 0 }, null, { x: 0, y: 0 }, null).kind).toBe('none');
  });

  it('guides are snapped to on a plane: lines lying in it, points on it, lines passing through it', () => {
    const nodes = new Map<string, { type: string; props: Record<string, unknown> }>([
      ['g1', { type: 'LapinGuide', props: { a: [0, 1, 3], b: [5, 1, 3] } }],
      ['g2', { type: 'LapinGuide', props: { a: [2, 2, 3] } }],
      ['g3', { type: 'LapinGuide', props: { a: [7, 7, 0], b: [7, 7, 1] } }],
      ['w', { type: 'IfcWall', props: {} }],
    ]);
    const guides = guidesOf(nodes);
    expect(guides).toHaveLength(3);
    expect(guideSegment(guides[0])![1].x).toBeCloseTo(GUIDE_REACH, 9);
    const r = guidesInPlane(guides, levelFrame(3));
    expect(r.edges).toHaveLength(1);
    expect(r.edges[0].a.y).toBeCloseTo(1, 9);
    expect(r.points).toContainEqual({ x: 2, y: 2 });
    expect(r.points.some((q) => Math.abs(q.x - 7) < 1e-9 && Math.abs(q.y - 7) < 1e-9)).toBe(true);
  });

  it('SketchUp\'s one-letter shortcuts start tools that exist, and are off by default', () => {
    const ids = new Set(studio.tabs!.flatMap((t) => t.groups.flatMap((g) => [...g.tools, ...(g.small ?? [])].map((x) => x.id))));
    for (const [key, id] of Object.entries(SKETCHUP_KEYS)) {
      if (id === 'select' || id.startsWith('cad:')) continue;
      expect(ids.has(id), `${key} → ${id}`).toBe(true);
    }
    expect(readDisplay(null).sketchupKeys).toBe(false);
    // While a value waits, its keys are not shortcuts (decision 378): R begins a radius, a digit is a digit, L still a line.
    expect(sketchupKeyFor('r', null)).toBe('studio.sketch.rect');
    expect(sketchupKeyFor('r', '')).toBeUndefined();
    expect(sketchupKeyFor('m', '600')).toBeUndefined();
    expect(sketchupKeyFor('l', '600')).toBe('studio.sketch.line');
    expect(readDisplay({ sketchupKeys: true }).sketchupKeys).toBe(true);
  });
});
