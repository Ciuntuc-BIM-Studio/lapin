/**
 * A beam-and-block floor type's rules (report #134), as the engine keeps them on the floor type (`beamBlock`): POT-like
 * precast joists at a spacing, infill blocks between them, a cast topping. Metres; the engine lays it out and builds it.
 */

export interface BeamBlockSpec {
  preset: string;
  joistWidth: number; joistHeight: number; flangeHeight: number; webWidth: number; joistMaterial: string;
  spacing: number; blockHeight: number; blockLength: number; blockMaterial: string;
  topping: number; toppingMaterial: string;
  direction: 'auto' | 'x' | 'y' | 'angle'; angle: number; bearing: number; mesh: string;
}

/** The first indicative preset, until the engine's list comes. */
export const BEAM_BLOCK_DEFAULT: BeamBlockSpec = {
  preset: 'POT + MIAKO 19/62.5', joistWidth: 0.16, joistHeight: 0.175, flangeHeight: 0.06, webWidth: 0.06, joistMaterial: 'Precast joist',
  spacing: 0.625, blockHeight: 0.19, blockLength: 0.25, blockMaterial: 'Ceramic infill block', topping: 0.06, toppingMaterial: 'Concrete C20/25',
  direction: 'auto', angle: 0, bearing: 0.125, mesh: 'Mesh 6/100-6/100',
};

/** The structure's depth: the blocks and the topping. */
export const beamBlockDepth = (s: BeamBlockSpec): number => Math.round((s.blockHeight + s.topping) * 1e6) / 1e6;

/** The spec as stored on a type (or a preset as the engine lists it): every field there, the default's where one is missing. */
export function beamBlockOf(stored: unknown): BeamBlockSpec | null {
  if (!stored || typeof stored !== 'object') return null;
  const o = stored as Record<string, unknown>;
  const out = { ...BEAM_BLOCK_DEFAULT } as unknown as Record<string, unknown>;
  for (const k of Object.keys(BEAM_BLOCK_DEFAULT)) {
    const v = o[k];
    if (typeof v === typeof out[k]) out[k] = v;
  }
  return out as unknown as BeamBlockSpec;
}

/** A field changed by hand: the spec is no longer the preset it started from. */
export function editBeamBlock(s: BeamBlockSpec, patch: Partial<BeamBlockSpec>): BeamBlockSpec {
  const next = { ...s, ...patch };
  const touched = Object.keys(patch).some((k) => k !== 'preset' && k !== 'direction' && k !== 'angle' && k !== 'bearing' && (patch as Record<string, unknown>)[k] !== (s as unknown as Record<string, unknown>)[k]);
  return touched && !('preset' in patch) ? { ...next, preset: '' } : next;
}

/** The type's layers with its core made as deep as the structure (as the engine will), the others kept. */
export function withCoreDepth<T extends { thickness: number; core: boolean }>(rows: T[], depth: number): T[] {
  let core = rows.findIndex((r) => r.core);
  if (core < 0 && rows.length > 0) core = rows.reduce((b, r, i) => (r.thickness > rows[b].thickness ? i : b), 0);
  return rows.map((r, i) => (i === core ? { ...r, thickness: depth, core: true } : r));
}

/** A floor's edges to pick its span from: each its index, its length and which way it runs (the joists bear on it, across it). */
export function spanEdges(contour: [number, number][]): { index: number; length: number; angle: number }[] {
  return contour.map((p, i) => {
    const q = contour[(i + 1) % contour.length];
    const dx = q[0] - p[0];
    const dy = q[1] - p[1];
    const deg = (Math.atan2(dx, -dy) * 180) / Math.PI;
    return { index: i, length: Math.hypot(dx, dy), angle: Math.round((((deg % 180) + 180) % 180) * 1e6) / 1e6 };
  }).filter((e) => e.length > 1e-6);
}

/** Flat x y pairs into an SVG points string, by a map from model to screen. */
export function svgPoints(flat: number[], X: (x: number) => number, Y: (y: number) => number): string {
  const out: string[] = [];
  for (let i = 0; i + 1 < flat.length; i += 2) out.push(`${X(flat[i]).toFixed(1)},${Y(flat[i + 1]).toFixed(1)}`);
  return out.join(' ');
}
