/**
 * What a 3D tab keeps while another view is in front (forum topic 62): its camera (pose and projection) and its section box.
 * The 3D viewport is unmounted when a plan or a sheet takes the pane, so what it held would start again from the defaults;
 * this memory, one entry per 3D view (a saved view's id, else "3d"), is written as the user changes them and read when the
 * viewport comes back. It lives for the session and is emptied when another project is opened (engine/files.ts). A saved 3D
 * view's own camera and box (the project's) are used only when nothing was kept for it this session.
 */

import type { CameraPose } from './camera';
import type { SectionBox } from './sectionBox';

export interface Kept3d {
  pose?: CameraPose;
  ortho?: boolean;
  /** The section box; null: switched off (kept as off). */
  box?: SectionBox | null;
}

const kept = new Map<string, Kept3d>();
let current: string | undefined;

/** A 3D view's key: its saved id, else "3d". */
export const key3d = (view3dId: string | null | undefined): string => view3dId ?? '3d';

export function remember3d(key: string, patch: Kept3d): void { kept.set(key, { ...kept.get(key), ...patch }); }
export function recall3d(key: string): Kept3d | undefined { return kept.get(key); }
export function forget3d(): void { kept.clear(); current = undefined; }

/** Whether `key` is not the 3D view the viewport showed last (also true for the first mount of a project); remembers it. */
export function changed3d(key: string): boolean {
  const was = current;
  current = key;
  return was !== key;
}
