/**
 * A skirting board's section (decision 254), as the engine sweeps it (Skirtings.Profile): a polygon anticlockwise in
 * (d, z), d from the wall's face out to the thickness, z from the floor up to the height; and its preview as an SVG path.
 */

export type SkirtingProfile = 'flush' | 'bevelled' | 'moulded' | 'cove' | 'pencil' | 'bullnose';

export interface SkirtingSpec { name: string; profile: SkirtingProfile; height: number; thickness: number; material: string; color: string }

export const PROFILES: { id: SkirtingProfile; label: string }[] = [
  { id: 'flush', label: 'Flush (square)' },
  { id: 'bevelled', label: 'Bevelled (MDF)' },
  { id: 'moulded', label: 'Moulded (wooden)' },
  { id: 'cove', label: 'Cove foot (metal)' },
  { id: 'pencil', label: 'Pencil round' },
  { id: 'bullnose', label: 'Bullnose' },
];

type P = [number, number];

/** The section's points, as the engine's. */
export function profilePoints(s: Pick<SkirtingSpec, 'profile' | 'height' | 'thickness'>): P[] {
  const t = s.thickness;
  const h = s.height;
  const o: P[] = [[0, 0], [t, 0]];
  const arc = (cx: number, cy: number, r: number, from: number, to: number, n: number) => {
    for (let i = 1; i <= n; i++) {
      const a = ((from + ((to - from) * i) / n) * Math.PI) / 180;
      o.push([cx + r * Math.cos(a), cy + r * Math.sin(a)]);
    }
  };
  switch (s.profile) {
    case 'bevelled': {
      const run = t * 0.65;
      o.push([t, h - Math.min(run * 1.5, h * 0.4)], [t - run, h]);
      break;
    }
    case 'moulded': {
      const z0 = h * 0.6;
      const n = 10;
      o.push([t, z0]);
      for (let i = 1; i <= n; i++) {
        const k = i / n;
        o.push([t - (t - t * 0.4) * (0.5 - 0.5 * Math.cos(Math.PI * k)), z0 + (h - z0) * k]);
      }
      break;
    }
    case 'cove': {
      const plate = Math.max(0.0015, t * 0.15);
      const r = t - plate;
      arc(t, r, r, 270, 180, 8);
      o.push([plate, h]);
      break;
    }
    case 'pencil':
      o.push([t, h - t / 2]);
      arc(t / 2, h - t / 2, t / 2, 0, 180, 10);
      return o;
    case 'bullnose':
      o.push([t, h - t]);
      arc(0, h - t, t, 0, 90, 8);
      o.pop();
      break;
    default:
      o.push([t, h]);
  }
  o.push([0, h]);
  return o;
}

/** The section's area (m²). */
export function profileArea(s: Pick<SkirtingSpec, 'profile' | 'height' | 'thickness'>): number {
  const p = profilePoints(s);
  let a = 0;
  for (let i = 0; i < p.length; i++) { const [x1, y1] = p[i]; const [x2, y2] = p[(i + 1) % p.length]; a += x1 * y2 - x2 * y1; }
  return Math.abs(a) / 2;
}

/**
 * The section drawn in a box `w` × `h` px, the wall's face on the left and the floor at the bottom, `pad` px round it:
 * one scale for both ways (the board's true shape), its path closed.
 */
export function profilePath(s: Pick<SkirtingSpec, 'profile' | 'height' | 'thickness'>, w: number, h: number, pad = 8): string {
  const pts = profilePoints(s);
  const k = Math.min((w - 2 * pad) / Math.max(s.thickness, 1e-6), (h - 2 * pad) / Math.max(s.height, 1e-6));
  const f = (v: number) => Math.round(v * 100) / 100;
  return pts.map(([d, z], i) => `${i ? 'L' : 'M'}${f(pad + d * k)} ${f(h - pad - z * k)}`).join(' ') + ' Z';
}
