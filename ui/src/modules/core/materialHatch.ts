/**
 * A layer table's hatch button (forum #30, decision 493): the material a row names, as material.list gives it (read once
 * per model version for every row of every table), and where its editor opens beside the button.
 */

import { query } from '../../engine/session';
import type { MaterialRow } from './MaterialGraphicsTable';

let cache: { version: unknown; list: Promise<MaterialRow[]> } | null = null;

/** The project's materials with their graphics, asked of the engine once for a model version (`nodes`, as the session holds it). */
export function materialsOf(version: unknown): Promise<MaterialRow[]> {
  if (!cache || cache.version !== version) cache = { version, list: query<MaterialRow[]>('material.list').then((r) => r ?? []).catch(() => []) };
  return cache.list;
}

/** A material by its name, as the layer tables name them; null for a name not made yet. */
export const materialNamed = (list: readonly MaterialRow[], name: string): MaterialRow | null => list.find((m) => m.name === name) ?? null;

/** Where an editor of `size` opens for a button at `at` (screen px): under it, else over it; kept inside the window with an 8 px margin. */
export function placeBeside(at: { left: number; top: number; bottom: number }, size: { w: number; h: number }, screen: { w: number; h: number }): { left: number; top: number } {
  const m = 8;
  const left = Math.max(m, Math.min(at.left, screen.w - size.w - m));
  const below = at.bottom + 4;
  const top = below + size.h + m <= screen.h ? below : Math.max(m, at.top - 4 - size.h);
  return { left, top };
}
