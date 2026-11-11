/**
 * How a curtain wall member's profile sits on its member (report #128): turned, mirrored, aligned and moved, as the engine's
 * `MemberPlace` does it, so the dialog's picture is the member's section. x across the member, y out of the building; metres.
 */

export type Across = 'front' | 'center' | 'back';
export type Along = 'start' | 'center' | 'end';
export interface Place { rotate?: number; mirror?: boolean; across?: Across; along?: Along; dx?: number; dy?: number }
export type Ring = [number, number][];

export const isDefaultPlace = (p: Place | null | undefined): boolean =>
  !p || (!(p.rotate ?? 0) && !p.mirror && (p.across ?? 'center') === 'center' && (p.along ?? 'center') === 'center' && !(p.dx ?? 0) && !(p.dy ?? 0));

/** The rings as the place leaves them: mirrored across x, turned counter-clockwise, aligned by their box, moved. */
export function placeRings(rings: Ring[], place: Place | null | undefined): Ring[] {
  if (!rings.length || isDefaultPlace(place)) return rings;
  const p = place!;
  const a = ((p.rotate ?? 0) * Math.PI) / 180, c = Math.cos(a), s = Math.sin(a);
  const turned = rings.map((r) => {
    const t = r.map(([x, y]): [number, number] => { const mx = p.mirror ? -x : x; return [mx * c - y * s, mx * s + y * c]; });
    // Mirrored, a ring runs the other way round: kept as it was (outside anticlockwise, holes clockwise).
    return p.mirror ? t.reverse() : t;
  });
  const pts = turned.flat();
  const minX = Math.min(...pts.map((q) => q[0])), maxX = Math.max(...pts.map((q) => q[0]));
  const minY = Math.min(...pts.map((q) => q[1])), maxY = Math.max(...pts.map((q) => q[1]));
  const sx = (p.along === 'start' ? -minX : p.along === 'end' ? -maxX : 0) + (p.dx ?? 0);
  const sy = (p.across === 'front' ? -maxY : p.across === 'back' ? -minY : 0) + (p.dy ?? 0);
  return turned.map((r) => r.map(([x, y]): [number, number] => [x + sx, y + sy]));
}

/** A plain member's rectangle, centred, as a ring. */
export const boxRing = (width: number, depth: number): Ring => [[-width / 2, -depth / 2], [width / 2, -depth / 2], [width / 2, depth / 2], [-width / 2, depth / 2]];

/** A place for the engine: only what is not the default (none at all: null, which puts the member back). */
export function placeJson(p: Place): Place | null {
  if (isDefaultPlace(p)) return null;
  const o: Place = {};
  if (p.rotate) o.rotate = ((p.rotate % 360) + 360) % 360;
  if (p.mirror) o.mirror = true;
  if (p.across && p.across !== 'center') o.across = p.across;
  if (p.along && p.along !== 'center') o.along = p.along;
  if (p.dx) o.dx = p.dx;
  if (p.dy) o.dy = p.dy;
  return o;
}
