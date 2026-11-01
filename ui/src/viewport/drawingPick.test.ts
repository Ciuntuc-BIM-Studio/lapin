import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { pickDrawing } from './drawingPick';

const node = (id: string, type: string): NodeDto => ({ id, type, name: id, props: {} } as unknown as NodeDto);
const nodes = new Map([['beam', node('beam', 'IfcBeam')], ['slab', node('slab', 'IfcSlab')], ['dim', node('dim', 'IfcAnnotation')], ['inset', node('inset', 'LapinInset')], ['bar', node('bar', 'LapinRebar')]]);
const square = (x0: number, y0: number, x1: number, y1: number) => [x0, y0, x1, y0, x1, y1, x0, y1];
const marks = [
  { id: 'dim', segments: [0, 0.5, 1, 0.5] },
  // The slab's cut holds the beam's: the smaller loop wins inside, the larger alone past it.
  { id: 'slab', segments: [], data: { loops: [square(-1, -1, 5, 5)] } },
  { id: 'beam', segments: [0, 0, 1, 0, 1, 0, 1, 1], data: { loops: [square(0, 0, 1, 1)] } },
];

describe('a section or elevation click', () => {
  it('takes the smallest cut loop holding the point when no line is under it', () => {
    expect(pickDrawing({ x: 0.5, y: 0.2 }, 0.05, marks, nodes)).toBe('beam');
    expect(pickDrawing({ x: 3, y: 3 }, 0.05, marks, nodes)).toBe('slab');
    expect(pickDrawing({ x: 10, y: 10 }, 0.05, marks, nodes)).toBeNull();
  });

  it('takes a line under the point first, an annotation before an element', () => {
    expect(pickDrawing({ x: 0.5, y: 0.5 }, 0.05, marks, nodes)).toBe('dim');
    expect(pickDrawing({ x: 1.02, y: 0.3 }, 0.05, marks, nodes)).toBe('beam');
  });

  it('skips a mark whose owner is not in the model', () => {
    expect(pickDrawing({ x: 0.5, y: 0.2 }, 0.05, [{ id: 'gone', segments: [], data: { loops: [square(0, 0, 1, 1)] } }], nodes)).toBeNull();
  });

  it('takes a bar seen through an inset view before the inset, the inset alone where nothing else is', () => {
    // The inset's lines drawn first (a stirrup across the dot), the bar's dot after it, a small circle as segments.
    const dot = Array.from({ length: 8 }, (_, k) => [2 + 0.04 * Math.cos(k * Math.PI / 4), 2 + 0.04 * Math.sin(k * Math.PI / 4), 2 + 0.04 * Math.cos((k + 1) * Math.PI / 4), 2 + 0.04 * Math.sin((k + 1) * Math.PI / 4)]).flat();
    const through = [{ id: 'inset', segments: [1.5, 2, 2.5, 2, 1.5, 3, 2.5, 3] }, { id: 'bar:projection', segments: dot }];
    expect(pickDrawing({ x: 2, y: 2 }, 0.05, through, nodes)).toBe('bar');
    expect(pickDrawing({ x: 2.4, y: 3 }, 0.05, through, nodes)).toBe('inset');
    // A bar lying on its stirrup: the side of the dot away from the stirrup's line takes the bar, the line itself the beam.
    const stirrup = [{ id: 'beam:stirrups', segments: [1.5, 1.955, 2.5, 1.955] }, ...through];
    expect(pickDrawing({ x: 2, y: 2.035 }, 0.05, stirrup, nodes)).toBe('bar');
    expect(pickDrawing({ x: 2.3, y: 1.955 }, 0.05, stirrup, nodes)).toBe('beam');
    // A tool that works on bars takes the bar under a stirrup's line.
    expect(pickDrawing({ x: 2, y: 1.97 }, 0.05, stirrup, nodes, (n) => n.type === 'LapinRebar')).toBe('bar');
  });
});
