import { describe, expect, it } from 'vitest';
import { cssColor, drawingPoint, evenOddShapes, onCut, paperToModel, sectionPlanes, sectionPose, textFont, textQuad, type DrawingText, type ViewFrameDto } from './section3d';

// The engine's frame of a section drawn north from (3, −2) to (3, 2) (AnnotationTests): it looks west, its x runs north.
const F: ViewFrameDto = { origin: [3, -2, 0], right: [0, 1, 0], up: [0, 0, 1], dir: [-1, 0, 0] };
const kept = (planes: ReturnType<typeof sectionPlanes>, p: number[]) => planes.every((q) => q.normal[0] * p[0] + q.normal[1] * p[1] + q.normal[2] * p[2] + q.constant >= 0);

describe('a section seen in 3D', () => {
  it('lays a drawing point on the section plane: x along the line from its start, y up', () => {
    // The wall along y = 0 is cut 2 m along the line; 1.5 m up its cut is at (3, 0, 1.5).
    expect(drawingPoint(F, 2, 1.5)).toEqual({ x: 3, y: 0, z: 1.5 });
    // Lifted into the scene: westwards.
    expect(drawingPoint(F, 2, 1.5, 0.01).x).toBeCloseTo(2.99);
  });

  it('is not mirrored: looking along the section, the drawing runs to the right', () => {
    const [d, u, r] = [F.dir, F.up, F.right];
    const cross = [d[1] * u[2] - d[2] * u[1], d[2] * u[0] - d[0] * u[2], d[0] * u[1] - d[1] * u[0]];
    expect(cross.map((v) => v + 0)).toEqual(r);
  });

  it('cuts away what is in front of the line and, with a depth, what is beyond it', () => {
    const unlimited = sectionPlanes(F);
    expect(unlimited).toHaveLength(1);
    expect(kept(unlimited, [2, 0, 1])).toBe(true);    // west of the line: seen
    expect(kept(unlimited, [4, 0, 1])).toBe(false);   // east, between the viewer and the cut: gone
    expect(kept(unlimited, [-50, 0, 1])).toBe(true);
    const deep = sectionPlanes(F, 4);
    expect(deep).toHaveLength(2);
    expect(kept(deep, [0, 0, 1])).toBe(true);         // 3 m in
    expect(kept(deep, [-2, 0, 1])).toBe(false);       // 5 m in: past the depth
    expect(sectionPlanes(F, 1e4)).toHaveLength(1);    // the engine's unlimited
  });

  it('frames the section from the viewer\'s side, looking the way it looks (a little from the side)', () => {
    const p = sectionPose(F, [0, 0, 4, 3], Math.PI / 4);
    expect(Math.cos(p.azimuth)).toBeLessThan(-0.85);
    expect(p.pitch).toBeLessThan(0);
    expect(p.target).toEqual({ x: 3, y: 0, z: 1.5 });
  });

  it('fills loops even-odd: a loop inside another is its hole, one inside that an outline again', () => {
    const sq = (c: number, h: number) => [c - h, c - h, c + h, c - h, c + h, c + h, c - h, c + h];
    const shapes = evenOddShapes([sq(0, 1), sq(0, 3), sq(0, 0.5), sq(10, 1)]);
    expect(shapes).toHaveLength(3);
    const big = shapes.find((s) => s.outer[0] === -3)!;
    expect(big.holes).toEqual([sq(0, 1)]);
    expect(shapes.some((s) => s.outer[0] === -0.5 && s.holes.length === 0)).toBe(true);
  });

  it('lays the cut always, the annotations when asked, never what the model shows itself', () => {
    for (const l of ['CUT', 'FILL', 'HATCH', 'MEMBRANE']) expect(onCut(l, false)).toBe(true);
    for (const l of ['DIMENSION', 'LEVEL', 'TAG', 'TEXT', 'SECTION', 'DRAFT-FILL']) {
      expect(onCut(l, true)).toBe(true);
      expect(onCut(l, false)).toBe(false);
    }
    for (const l of ['SEEN', 'BEYOND', 'HIDDEN', 'OVERHEAD', 'ROOM-FILL']) expect(onCut(l, true)).toBe(false);
  });

  const text = (o: Partial<DrawingText>): DrawingText => ({ x: 0, y: 0, text: 'B-B', height: 2.5, angle: 0, anchor: 'middle', baseline: 'bottom', bold: false, font: 'sans', ...o });

  it('sizes a text as on paper at the view\'s scale: 2.5 mm at 1:100 is 0.25 m', () => {
    expect(paperToModel(2.5, 100)).toBeCloseTo(0.25);
    expect(paperToModel(2.5, 50)).toBeCloseTo(0.125);
    // A level's name (anchor start, on its baseline) at (14.6, 3.1), 4 ems wide, 0.9 over and 0.2 under its baseline, at 1:100.
    const q = textQuad(text({ x: 14.6, y: 3.1, anchor: 'start' }), 100, 4, 0.9, 0.2);
    const r = q.map((p) => [+p.x.toFixed(4), +p.y.toFixed(4)]);
    expect(r).toEqual([[14.6, 3.05], [15.6, 3.05], [15.6, 3.325], [14.6, 3.325]]);
  });

  it('anchors a text at its start, middle or end, on its baseline or its middle, as the 2D drawing does', () => {
    const xs = (anchor: string) => textQuad(text({ anchor }), 100, 4, 0.5, 0.5).map((p) => +p.x.toFixed(4));
    expect(xs('start')).toEqual([0, 1, 1, 0]);
    expect(xs('middle')).toEqual([-0.5, 0.5, 0.5, -0.5]);
    expect(xs('end')).toEqual([-1, 0, 0, -1]);
    // Centred on its line: as far over as under.
    const m = textQuad(text({ baseline: 'middle' }), 100, 2, 0.6, 0.6);
    expect(m[0].y).toBeCloseTo(-0.15);
    expect(m[3].y).toBeCloseTo(0.15);
  });

  it('turns a text round its anchor, anticlockwise: a vertical dimension\'s text reads bottom to top', () => {
    const q = textQuad(text({ x: 2, y: 1, anchor: 'start', angle: 90 }), 100, 4, 1, 0);
    // Its way runs up the drawing, its top towards −x (to the left, as on paper).
    expect(q[1].x).toBeCloseTo(2);
    expect(q[1].y).toBeCloseTo(2);
    expect(q[3].x).toBeCloseTo(1.75);
    expect(q[3].y).toBeCloseTo(1);
  });

  it('is not mirrored on the cut: a text reads left to right and upright from the side the section looks from', () => {
    const q = textQuad(text({ anchor: 'start' }), 100, 4, 1, 0).map((p) => drawingPoint(F, 2 + p.x, 1 + p.y));
    const sub = (a: { x: number; y: number; z: number }, b: { x: number; y: number; z: number }) => [a.x - b.x, a.y - b.y, a.z - b.z];
    const along = sub(q[1], q[0]), upward = sub(q[3], q[0]);
    // The viewer looks along dir: the screen's right is dir × up, its up the frame's up.
    const [d, u] = [F.dir, F.up];
    const screenRight = [d[1] * u[2] - d[2] * u[1], d[2] * u[0] - d[0] * u[2], d[0] * u[1] - d[1] * u[0]];
    const dot = (a: number[], b: number[]) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
    expect(dot(along, screenRight)).toBeGreaterThan(0);
    expect(dot(upward, u)).toBeGreaterThan(0);
    // Its face (bottom-left, bottom-right, top-right turning anticlockwise) faces the viewer, so it is drawn front side.
    const n = [along[1] * upward[2] - along[2] * upward[1], along[2] * upward[0] - along[0] * upward[2], along[0] * upward[1] - along[1] * upward[0]];
    expect(dot(n, d)).toBeLessThan(0);
  });

  it('sets a text\'s font as the 2D drawing does', () => {
    expect(textFont(text({ bold: true }), 64)).toBe('700 64px ui-sans-serif, system-ui, sans-serif');
    expect(textFont(text({ font: 'serif' }), 32)).toBe('400 32px ui-serif, Georgia, serif');
  });

  it('reads pen colours with their opacity', () => {
    expect(cssColor('#a0a0a0')).toEqual({ hex: '#a0a0a0', opacity: 1 });
    expect(cssColor('rgba(255, 0, 16, 0.5)')).toEqual({ hex: '#ff0010', opacity: 0.5 });
    expect(cssColor('none')).toBeNull();
    expect(cssColor(null)).toBeNull();
  });
});
