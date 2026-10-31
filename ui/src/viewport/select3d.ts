/**
 * Selecting by a region in 3D (docs/groups.md): with View → Select region (SR) on, a left drag draws a box or,
 * with Space, a lasso, as in plans: dragged to the right a window takes what lies wholly inside it, to the left a
 * crossing takes what it touches. An element is tested by its edges projected on the screen (its box's twelve
 * when it has none, and an instance's mesh box placed); what lies behind the eye is never wholly inside. While it
 * is on, the right button turns the view and the middle one moves it.
 */

import { create } from 'zustand';
import type { Seg, RegionTest } from './selectBox';
import type { SelectDrag } from './selectDrag';

export const useRegion3d = create<{ on: boolean; drag: SelectDrag | null }>(() => ({ on: false, drag: null }));

export const toggleRegion3d = (): void => useRegion3d.setState((s) => ({ on: !s.on, drag: null }));

/** A point in the world to the screen (px), or null behind the eye. */
export type Project = (x: number, y: number, z: number) => { x: number; y: number } | null;

/** The segments x y z, x y z … projected; `behind` when any end lies behind the eye (those are left out). */
export function projectSegments(flat: ArrayLike<number>, project: Project, out: Seg[] = []): { segs: Seg[]; behind: boolean } {
  let behind = false;
  for (let k = 0; k + 5 < flat.length; k += 6) {
    const a = project(flat[k], flat[k + 1], flat[k + 2]);
    const b = project(flat[k + 3], flat[k + 4], flat[k + 5]);
    if (!a || !b) { behind = true; continue; }
    out.push([a, b]);
  }
  return { segs: out, behind };
}

/** A box's twelve edges (min x y z, max x y z), as segments x y z x y z, placed by `place` when given. */
export function boxEdges(b: ArrayLike<number>, place?: (x: number, y: number, z: number) => [number, number, number]): number[] {
  const corner = (k: number): [number, number, number] => {
    const p: [number, number, number] = [b[k & 1 ? 3 : 0], b[k & 2 ? 4 : 1], b[k & 4 ? 5 : 2]];
    return place ? place(p[0], p[1], p[2]) : p;
  };
  const c = Array.from({ length: 8 }, (_, k) => corner(k));
  const out: number[] = [];
  for (const [i, j] of [[0, 1], [2, 3], [4, 5], [6, 7], [0, 2], [1, 3], [4, 6], [5, 7], [0, 4], [1, 5], [2, 6], [3, 7]]) out.push(...c[i], ...c[j]);
  return out;
}

/** The box round x y z points (min x y z, max x y z); null when there are none. */
export function boxOf(positions: ArrayLike<number>): number[] | null {
  if (positions.length < 3) return null;
  const b = [Infinity, Infinity, Infinity, -Infinity, -Infinity, -Infinity];
  for (let i = 0; i + 2 < positions.length; i += 3) for (let a = 0; a < 3; a++) { b[a] = Math.min(b[a], positions[i + a]); b[3 + a] = Math.max(b[3 + a], positions[i + a]); }
  return b;
}

/** Whether a region takes an element seen as these projected segments (a window refuses one partly behind the eye). */
export function takes(test: RegionTest, windowMode: boolean, seen: { segs: Seg[]; behind: boolean }): boolean {
  if (windowMode && seen.behind) return false;
  return test(seen.segs);
}
