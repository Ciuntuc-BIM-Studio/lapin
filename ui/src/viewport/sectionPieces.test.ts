import { describe, expect, it } from 'vitest';
import { sectionPieces } from './section3d';
import type { PenDto } from './drawing';

/** A developed section's drawing laid in 3D piece by piece: each piece takes what falls on its stretch of x, in its own x. */
describe('the pieces of a developed section', () => {
  const frame = (x0: number, x1: number, ox: number) => ({ origin: [ox, 0, 0], right: [1, 0, 0], up: [0, 0, 1], dir: [0, 1, 0], x0, x1 });
  const pen = (p: Partial<PenDto>): PenDto => ({ layer: 'CUT', stroke: '#000', width: 0.5, fill: null, ...p });

  it('is the drawing itself for a plain section', () => {
    const f = { origin: [0, 0, 0], right: [1, 0, 0], up: [0, 0, 1], dir: [0, 1, 0] };
    const pens = [pen({ segments: [0, 0, 5, 0] })];
    expect(sectionPieces({ frame: f, pens })).toEqual([{ frame: f, pens }]);
    expect(sectionPieces({ pens })).toEqual([]);
  });

  it('cuts segments, loops and texts to each piece and moves them to its own x', () => {
    const d = {
      frame: frame(0, 4, 0), frames: [frame(0, 4, 0), frame(4, 10, 4)],
      pens: [
        pen({ segments: [1, 1, 7, 1, 5, 0, 6, 2] }),
        pen({ stroke: null, fill: '#f00', loops: [[3, 0, 6, 0, 6, 2, 3, 2]] }),
        pen({ texts: [{ x: 2, y: 3, text: 'A', height: 2.5, angle: 0, anchor: 'middle', baseline: 'bottom', bold: false, font: 'sans' }, { x: 8, y: 3, text: 'B', height: 2.5, angle: 0, anchor: 'middle', baseline: 'bottom', bold: false, font: 'sans' }] }),
      ],
    };
    const [a, b] = sectionPieces(d);
    // The first piece (x 0…4): the long segment up to x = 4, the loop's part x 3…4, text A.
    expect(a.pens[0].segments).toEqual([1, 1, 4, 1]);
    expect(a.pens[1].loops).toEqual([[3, 0, 4, 0, 4, 2, 3, 2]]);
    expect(a.pens[2].texts!.map((t) => [t.text, t.x])).toEqual([['A', 2]]);
    // The second (x 4…10, laid from 0): the rest of the long segment, the whole short one, the loop's part x 4…6, text B at 4.
    expect(b.pens[0].segments).toEqual([0, 1, 3, 1, 1, 0, 2, 2]);
    expect(b.pens[1].loops).toEqual([[0, 0, 2, 0, 2, 2, 0, 2]]);
    expect(b.pens[2].texts!.map((t) => [t.text, t.x])).toEqual([['B', 4]]);
    expect(b.frame.origin).toEqual([4, 0, 0]);
  });

  it('leaves out a pen with nothing on a piece', () => {
    const d = { frames: [frame(0, 4, 0), frame(4, 10, 4)], pens: [pen({ segments: [5, 0, 6, 0] })] };
    const [a, b] = sectionPieces(d);
    expect(a.pens).toEqual([]);
    expect(b.pens[0].segments).toEqual([1, 0, 2, 0]);
  });
});
