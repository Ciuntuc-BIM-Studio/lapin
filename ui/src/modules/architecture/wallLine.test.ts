import { describe, expect, it } from 'vitest';
import { bandsOf, lineLeft, mirrorLine, wallGhost } from './wallLine';

// The engine's test wall (WallReferenceLineTests): 100 insulation outside, a 250 core, 15 plaster. Drawn along +x, its axis on
// the core's centre: the insulation (exterior) to the right, from −0.225 to −0.125, the plaster to the left up to 0.14.
const layers = [{ thickness: 0.1 }, { thickness: 0.25, core: true }, { thickness: 0.015 }];

describe('the line a wall is drawn on, and Space (decision 692)', () => {
  it('lays the layers out as the engine does: the exterior to the right, the core centred on the axis', () => {
    const b = bandsOf(layers);
    expect(b.map((x) => [x.from, x.to].map((v) => Math.round(v * 1e6) / 1e6))).toEqual([[-0.225, -0.125], [-0.125, 0.125], [0.125, 0.14]]);
    expect(b.map((x) => x.core)).toEqual([false, true, false]);
    // No core marked: the thickest layer is the core. No layers: the thickness given, centred.
    expect(bandsOf([{ thickness: 0.02 }, { thickness: 0.3 }])[1].core).toBe(true);
    expect(bandsOf([], 0.2)).toEqual([{ from: -0.1, to: 0.1, core: true }]);
  });

  it('finds the line clicked left of the axis, as WallReference.LeftOf does', () => {
    const b = bandsOf(layers);
    const at = (line: Parameters<typeof lineLeft>[1], offset = 0) => Math.round(lineLeft(b, line, offset) * 1e6) / 1e6;
    expect(at('core')).toBe(0);
    expect(at('coreExterior')).toBe(-0.125);
    expect(at('coreInterior')).toBe(0.125);
    expect(at('exterior')).toBe(-0.225);
    expect(at('interior')).toBe(0.14);
    expect(at('offset', 0.3)).toBe(0.3);
  });

  it('Space mirrors the line to the other side: the faces swap, an offset changes sign, the core centre stays', () => {
    expect(mirrorLine('exterior', 0)).toEqual({ line: 'interior', lineOffset: 0 });
    expect(mirrorLine('interior', 0)).toEqual({ line: 'exterior', lineOffset: 0 });
    expect(mirrorLine('coreExterior', 0)).toEqual({ line: 'coreInterior', lineOffset: 0 });
    expect(mirrorLine('coreInterior', 0)).toEqual({ line: 'coreExterior', lineOffset: 0 });
    expect(mirrorLine('offset', 0.4)).toEqual({ line: 'offset', lineOffset: -0.4 });
    expect(mirrorLine('core', 0.4)).toBeNull();
    // Twice is where it started.
    const once = mirrorLine('offset', -0.12)!;
    expect(mirrorLine(once.line, once.lineOffset)).toEqual({ line: 'offset', lineOffset: -0.12 });
  });

  it('the ghost lies on the side of the line the wall will be built: left of an exterior face, right of an interior one', () => {
    const b = bandsOf(layers);
    const path = { a: { x: 0, y: 0 }, b: { x: 4, y: 0 }, rise: 0 };
    const ys = (lines: { x: number; y: number }[][]) => lines.flat().map((p) => Math.round(p.y * 1e6) / 1e6);
    // Drawn on the exterior face: the body from the line (y = 0) up to 0.365 on its left.
    const ext = wallGhost(path, b, lineLeft(b, 'exterior', 0));
    expect(Math.min(...ys(ext))).toBe(0);
    expect(Math.max(...ys(ext))).toBe(0.365);
    // Space: the interior face, the body on the right, down to −0.365.
    const flipped = mirrorLine('exterior', 0)!;
    const int = wallGhost(path, b, lineLeft(b, flipped.line, flipped.lineOffset));
    expect(Math.min(...ys(int))).toBe(-0.365);
    expect(Math.max(...ys(int))).toBe(0);
    // The outline closed, and the core's faces drawn inside it.
    expect(ext[0][0]).toEqual(ext[0][ext[0].length - 1]);
    expect(ext.length).toBe(3);
  });

  it('draws a curved wall\'s ghost concentric with its arc', () => {
    const b = bandsOf([{ thickness: 0.2, core: true }]);
    const path = { a: { x: -2, y: 0 }, b: { x: 2, y: 0 }, rise: 2 }; // a half circle of radius 2 round (0, 0), bulging left (+y)
    const [outline] = wallGhost(path, b, lineLeft(b, 'exterior', 0));
    const r = outline.slice(0, -1).map((p) => Math.round(Math.hypot(p.x, p.y) * 1e6) / 1e6);
    // Drawn on its exterior face (its right): the body to the left of the arc, away from the centre, radii 2 to 2.2.
    expect(Math.max(...r)).toBe(2.2);
    expect(Math.min(...r)).toBe(2);
  });
});
