import { describe, expect, it } from 'vitest';
import type { NodeDto } from '../engine/protocol';
import { pickDrawing } from './drawingPick';

const node = (id: string, type: string): NodeDto => ({ id, type, name: id, props: {} } as unknown as NodeDto);
const nodes = new Map([['beam', node('beam', 'IfcBeam')], ['slab', node('slab', 'IfcSlab')], ['dim', node('dim', 'IfcAnnotation')]]);
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
});
