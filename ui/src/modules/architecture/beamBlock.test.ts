import { describe, expect, it } from 'vitest';
import { BEAM_BLOCK_DEFAULT, beamBlockDepth, beamBlockOf, editBeamBlock, spanEdges, svgPoints, withCoreDepth } from './beamBlock';

describe('beam and block floor types', () => {
  it('derives the depth from the blocks and the topping', () => {
    expect(beamBlockDepth(BEAM_BLOCK_DEFAULT)).toBe(0.25);
    expect(beamBlockDepth({ ...BEAM_BLOCK_DEFAULT, blockHeight: 0.23 })).toBe(0.29);
  });

  it('reads a stored spec, the missing fields from the default', () => {
    expect(beamBlockOf(null)).toBeNull();
    const s = beamBlockOf({ spacing: 0.5, blockHeight: 0.25, direction: 'x', joistMaterial: 7 })!;
    expect(s.spacing).toBe(0.5);
    expect(s.direction).toBe('x');
    expect(s.joistMaterial).toBe('Precast joist');
    expect(s.topping).toBe(0.06);
  });

  it('forgets the preset once a size is changed by hand, not for the span or the bearing', () => {
    expect(editBeamBlock(BEAM_BLOCK_DEFAULT, { spacing: 0.5 }).preset).toBe('');
    expect(editBeamBlock(BEAM_BLOCK_DEFAULT, { direction: 'x' }).preset).toBe(BEAM_BLOCK_DEFAULT.preset);
    expect(editBeamBlock(BEAM_BLOCK_DEFAULT, { bearing: 0.15 }).preset).toBe(BEAM_BLOCK_DEFAULT.preset);
    expect(editBeamBlock(BEAM_BLOCK_DEFAULT, { ...BEAM_BLOCK_DEFAULT, preset: 'Other' }).preset).toBe('Other');
  });

  it('makes the core as deep as the structure, the other layers kept', () => {
    const rows = [{ thickness: 0.02, core: false }, { thickness: 0.05, core: false }, { thickness: 0.15, core: true }];
    expect(withCoreDepth(rows, 0.25).map((r) => r.thickness)).toEqual([0.02, 0.05, 0.25]);
    expect(withCoreDepth([{ thickness: 0.02, core: false }, { thickness: 0.2, core: false }], 0.29)[1]).toEqual({ thickness: 0.29, core: true });
  });

  it('lists the edges to span from, the joists across each', () => {
    const e = spanEdges([[0, 0], [5, 0], [5, 4], [0, 4]]);
    expect(e.map((x) => x.angle)).toEqual([90, 0, 90, 0]);
    expect(e[1].length).toBe(4);
  });

  it('writes SVG points through a map', () => {
    expect(svgPoints([0, 0, 1, 2], (x) => x * 10, (y) => 100 - y * 10)).toBe('0.0,100.0 10.0,80.0');
  });
});
