import { describe, expect, it } from 'vitest';
import { inModel, viewFrameAt } from './viewFrame';

const south = { kind: 'elevation', frame: { origin: [-5, -10, -1], right: [1, 0, 0], up: [0, 0, 1], dir: [0, 1, 0] } };

describe('a section\'s or an elevation\'s frame for element.transform (decision 453)', () => {
  it('is the drawing\'s frame, none in a plan', () => {
    expect(viewFrameAt(south, { x: 2, y: 1 })).toEqual({ origin: [-5, -10, -1], right: [1, 0, 0], up: [0, 0, 1] });
    expect(viewFrameAt({ ...south, kind: 'plan' }, { x: 0, y: 0 })).toBeNull();
    expect(viewFrameAt(null)).toBeNull();
  });
  it('a move right and up in the view is along the view in plan and up in the model', () => {
    const east = viewFrameAt({ kind: 'elevation', frame: { origin: [0, 0, 0], right: [0, 1, 0], up: [0, 0, 1], dir: [-1, 0, 0] } })!;
    expect(inModel(east, 2, 0.5)).toEqual([0, 2, 0.5]);
  });
  it('a developed section takes the piece the point lies on, its x read from the drawing\'s', () => {
    const d = {
      kind: 'section', frame: { origin: [0, 0, 0], right: [1, 0, 0], up: [0, 0, 1], dir: [0, 1, 0] },
      frames: [
        { origin: [0, 0, 0], right: [1, 0, 0], up: [0, 0, 1], dir: [0, 1, 0], x0: 0, x1: 4 },
        { origin: [4, 0, 0], right: [0, 1, 0], up: [0, 0, 1], dir: [-1, 0, 0], x0: 4, x1: 9 },
      ],
    };
    const f = viewFrameAt(d, { x: 6, y: 0 })!;
    expect(f.right).toEqual([0, 1, 0]);
    // The drawing's x = 6 is 2 m along the second piece: (4, 2).
    expect([0, 1].map((k) => f.origin[k] + f.right[k] * 6)).toEqual([4, 2]);
  });
});
