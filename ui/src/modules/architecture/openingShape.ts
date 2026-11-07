/**
 * A wall opening's outline for its configurator's picture (a tester's report #114, decision 392), as the engine's
 * JoineryShapes.Outline draws it: counter-clockwise from the bottom right, taken `inset` in (the reveal's finish), its
 * bottom at `bottom`. Metres, x from the opening's middle, y up from its sill.
 */

export type HeadKind = 'rect' | 'segmental' | 'elliptical' | 'round' | 'radiused' | 'circle';
export interface Pt { x: number; y: number }

/** The rise a head takes when none is given (the engine's JoineryShapes.Rise). */
export function riseOf(kind: HeadKind, width: number, height: number, rise: number | null): number {
  if (kind === 'round') return Math.min(width / 2, height);
  const r = rise ?? (kind === 'segmental' ? width / 6 : kind === 'elliptical' ? width / 4 : kind === 'radiused' ? 0.15 : 0);
  return Math.max(0, Math.min(r, kind === 'radiused' ? width / 2 : height));
}

const STEPS = 24;

export function outline(kind: HeadKind, width: number, height: number, rise: number | null, inset = 0, bottom = inset): Pt[] {
  const half = width / 2 - inset;
  if (kind === 'circle') {
    const [a, b] = [width / 2 - inset, height / 2 - inset];
    return Array.from({ length: 48 }, (_, i) => ({ x: a * Math.cos((2 * Math.PI * i) / 48), y: height / 2 + b * Math.sin((2 * Math.PI * i) / 48) }));
  }
  const f = riseOf(kind, width, height, rise);
  const spring = height - f;
  let head: Pt[];
  switch (kind) {
    case 'round':
    case 'elliptical': {
      const [a, b] = [half, Math.max(f - inset, 1e-6)];
      head = Array.from({ length: STEPS + 1 }, (_, i) => ({ x: a * Math.cos((Math.PI * i) / STEPS), y: spring + b * Math.sin((Math.PI * i) / STEPS) }));
      break;
    }
    case 'segmental': {
      if (!(f > 0)) { head = [{ x: half, y: height - inset }, { x: -half, y: height - inset }]; break; }
      const R = (width * width / 4 + f * f) / (2 * f);
      const cy = height - R;
      const rho = R - inset;
      const a0 = Math.acos(Math.min(1, half / rho));
      head = Array.from({ length: STEPS + 1 }, (_, i) => { const a = a0 + ((Math.PI - 2 * a0) * i) / STEPS; return { x: rho * Math.cos(a), y: cy + rho * Math.sin(a) }; });
      break;
    }
    case 'radiused': {
      const rc = f - inset;
      const cx = width / 2 - f;
      const cy = height - f;
      if (rc <= 1e-9) { head = [{ x: half, y: height - inset }, { x: -half, y: height - inset }]; break; }
      const right = Array.from({ length: STEPS / 2 + 1 }, (_, i) => { const a = (Math.PI / 2 * i) / (STEPS / 2); return { x: cx + rc * Math.cos(a), y: cy + rc * Math.sin(a) }; });
      head = [...right, ...right.slice().reverse().map((p) => ({ x: -p.x, y: p.y }))];
      break;
    }
    default:
      head = [{ x: half, y: height - inset }, { x: -half, y: height - inset }];
  }
  return [{ x: half, y: bottom }, ...head, { x: -half, y: bottom }];
}

/** A polygon's area (shoelace), positive counter-clockwise. */
export const area = (pts: Pt[]): number => pts.reduce((s, p, i) => { const q = pts[(i + 1) % pts.length]!; return s + p.x * q.y - q.x * p.y; }, 0) / 2;
