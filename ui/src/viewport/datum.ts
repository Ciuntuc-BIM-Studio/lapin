/**
 * The temporary reference point (forum topic 226; Vectorworks' datum, AutoCAD's From): while a tool waits for a point, a key
 * (F6) drops a temporary 0,0 at the point the cursor is aimed at (a snap included). From it the next point is typed as ΔX and
 * ΔY at the cursor (or distance and angle: the live input's Space), and the point is taken relative to it. One click takes
 * the point and drops the reference. Plans and sections share it; the views draw its mark and read it for the live input.
 */

import { create } from 'zustand';
import type { Vec2 } from '../platform/module';
import { useUi } from '../app/store';
import { findTool } from '../platform/modules';
import { useCad } from './cad/session';

/** How the reference point's offset is typed: as ΔX and ΔY (the key), or as a distance and an angle (the plan's right-click menu, as before). */
export type DatumMode = 'xy' | 'polar';
export const useDatum = create<{ pt: Vec2 | null; mode: DatumMode }>(() => ({ pt: null, mode: 'polar' }));
export const setDatum = (pt: Vec2 | null, mode: DatumMode = 'polar'): void => useDatum.setState({ pt, mode });

/** The gestures that take a point a reference can be set before. */
const POINT_GESTURES = new Set(['point', 'twoPoints', 'polyline', 'polygon', 'rectangle', 'threePoints']);

/** Whether the active tool is waiting for a point a reference point may be set for. */
export function datumWanted(): boolean {
  const tool = findTool(useUi.getState().tool);
  return !!tool?.gesture && POINT_GESTURES.has(tool.gesture.kind);
}

/** The key: the reference point goes where the cursor is aimed; again on the same point (or with none under it) it is dropped. False when no tool waits for a point. */
export function datumHere(): boolean {
  if (!datumWanted()) return false;
  const at = useCad.getState().cursor;
  const cur = useDatum.getState().pt;
  if (!at || (cur && Math.hypot(cur.x - at.x, cur.y - at.y) < 1e-9)) { setDatum(null); return true; }
  setDatum({ x: at.x, y: at.y }, 'xy');
  return true;
}

/** A point `dx`, `dy` from the reference point, as typed. */
export const fromDatum = (d: Vec2, dx: number, dy: number): Vec2 => ({ x: d.x + dx, y: d.y + dy });
