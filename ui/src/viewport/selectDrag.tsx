/**
 * A selection region dragged with the mouse (screen px), shared by the plan and
 * the drawing views: a box from the press to the pointer, or a lasso once Space
 * is pressed during the drag, following the pointer from there (Space again
 * goes back to the box). A box is a window while the pointer is right of the
 * press and crossing left of it; a lasso fixes that choice when it begins, so
 * a loop that comes back round keeps its mode, as AutoCAD's does.
 */

import { useEffect } from 'react';
import type { Vec2 } from '../platform/module';
import { boxTest, lassoTest, type RegionTest } from './selectBox';

export interface SelectDrag {
  /** Where the drag began. */
  a: Vec2;
  /** Where the pointer is. */
  b: Vec2;
  /** The lasso's points so far, when it is one. */
  lasso?: Vec2[];
  /** A lasso's mode, fixed when it began. */
  window?: boolean;
}

/** A lasso takes a point every so many pixels; closer moves only move its end. */
const LASSO_STEP = 2;

export const isWindow = (d: SelectDrag): boolean => (d.lasso ? !!d.window : d.b.x >= d.a.x);

/** The drag followed to `to`. */
export function dragTo(d: SelectDrag, to: Vec2): SelectDrag {
  if (!d.lasso) return { a: d.a, b: to };
  const last = d.lasso[d.lasso.length - 1];
  const lasso = Math.hypot(to.x - last.x, to.y - last.y) >= LASSO_STEP ? [...d.lasso, to] : d.lasso;
  return { ...d, b: to, lasso };
}

/** The box turned into a lasso from here on (its mode as the box's now), or the lasso back into a box. */
export function toggleLasso(d: SelectDrag): SelectDrag {
  if (d.lasso) return { a: d.a, b: d.b };
  return { a: d.a, b: d.b, lasso: [d.a, d.b], window: d.b.x >= d.a.x };
}

/** The region's test in world coordinates. */
export function regionTestOf(d: SelectDrag, toWorld: (x: number, y: number) => Vec2): RegionTest {
  if (d.lasso) return lassoTest(d.lasso.map((p) => toWorld(p.x, p.y)), isWindow(d));
  return boxTest(toWorld(d.a.x, d.a.y), toWorld(d.b.x, d.b.y), isWindow(d));
}

/** Space while a region is dragged switches it between box and lasso, before the command line takes the key. */
export function useLassoKey(dragging: boolean, set: (f: (d: SelectDrag | null) => SelectDrag | null) => void) {
  useEffect(() => {
    if (!dragging) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== ' ' || e.repeat) return;
      e.preventDefault();
      e.stopPropagation();
      set((d) => (d ? toggleLasso(d) : d));
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [dragging, set]);
}

/** The region drawn over the view, in the window's or crossing's colours. */
export function SelectRegion({ d }: { d: SelectDrag }) {
  const cls = `select-box${isWindow(d) ? ' window' : ' crossing'}`;
  if (d.lasso) return <polygon className={`${cls} lasso`} points={d.lasso.map((p) => `${p.x},${p.y}`).join(' ')} />;
  return <rect className={cls} x={Math.min(d.a.x, d.b.x)} y={Math.min(d.a.y, d.b.y)} width={Math.abs(d.b.x - d.a.x)} height={Math.abs(d.b.y - d.a.y)} />;
}
