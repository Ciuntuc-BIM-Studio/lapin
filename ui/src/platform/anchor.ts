/**
 * The insertion anchor (forum topic 226): which point of the element sits at the cursor when it is placed. A 3×3 choice on
 * the element's box (corners, edge middles, centre) for a column; for a door or a window, which of its edges (left, centre,
 * right) is at the cursor along the wall. The anchor is a placement aid: the element is stored as it always was (its
 * insertion point, its place along the wall), nothing of the anchor goes into the project. Remembered per tool, on this
 * computer. Pure but for the small store.
 */

import { create } from 'zustand';
import { pointAt, project, tangentAt, type Arc } from '../viewport/arc';
import type { Vec2 } from './module';

/** `h`: −1 the left edge, 0 the middle, 1 the right; `v`: −1 the bottom edge, 0 the middle, 1 the top (plan: up the screen). */
export interface Anchor { h: -1 | 0 | 1; v: -1 | 0 | 1 }
export const CENTRE: Anchor = { h: 0, v: 0 };

/** The 3×3 in reading order, top row first: the order the selector shows them in and the key steps through. */
export const ANCHORS: readonly Anchor[] = ([1, 0, -1] as const).flatMap((v) => ([-1, 0, 1] as const).map((h) => ({ h, v })));

/** Which anchors a tool offers: all nine (a box), or the three along a wall (a door, a window). */
export type AnchorKind = 'box' | 'along';
export const anchorsOf = (kind: AnchorKind): readonly Anchor[] => (kind === 'box' ? ANCHORS : ANCHORS.filter((a) => a.v === 0));

const KEY = 'lapin.anchors';
const clamp3 = (n: unknown): -1 | 0 | 1 => (n === -1 || n === 1 ? n : 0);

/** What was kept: tool id → anchor, anything unreadable at the centre. */
export function readAnchors(stored: unknown): Record<string, Anchor> {
  const out: Record<string, Anchor> = {};
  if (stored && typeof stored === 'object') for (const [k, v] of Object.entries(stored as Record<string, unknown>)) {
    const o = v as { h?: unknown; v?: unknown } | null;
    if (o && typeof o === 'object') out[k] = { h: clamp3(o.h), v: clamp3(o.v) };
  }
  return out;
}

const load = (): Record<string, Anchor> => { try { return readAnchors(JSON.parse(localStorage.getItem(KEY) ?? 'null')); } catch { return {}; } };
export const useAnchors = create<{ byTool: Record<string, Anchor> }>(() => ({ byTool: load() }));

/** The anchor a tool places with (the centre until one is chosen). */
export const anchorFor = (tool: string | null | undefined): Anchor => (tool ? useAnchors.getState().byTool[tool] : undefined) ?? CENTRE;

export function setAnchor(tool: string, a: Anchor, kind: AnchorKind = 'box'): void {
  const next = { h: a.h, v: kind === 'along' ? 0 : a.v } as Anchor;
  useAnchors.setState((s) => ({ byTool: { ...s.byTool, [tool]: next } }));
  try { localStorage.setItem(KEY, JSON.stringify(useAnchors.getState().byTool)); } catch { /* kept for this session */ }
}

/** The anchor `by` steps on from `a` in the order of the choices (a key's press), round. */
export function stepAnchor(a: Anchor, kind: AnchorKind, by = 1): Anchor {
  const list = anchorsOf(kind);
  const i = Math.max(0, list.findIndex((x) => x.h === a.h && x.v === (kind === 'along' ? 0 : a.v)));
  return list[(i + by + list.length * 8) % list.length];
}

/**
 * Where an element's insertion point goes so that its anchor lies at `at`. `box`: its extents about the insertion point
 * (x0 < x1, y0 < y1, turned `rotation` degrees about it). The centre anchor is the insertion point itself, whatever the
 * box: an element drawn off-centre is placed as it always was until another anchor is chosen.
 */
export function insertionAt(at: Vec2, a: Anchor, box: { x0: number; x1: number; y0: number; y1: number }, rotation = 0): Vec2 {
  const ax = a.h > 0 ? box.x1 : a.h < 0 ? box.x0 : 0, ay = a.v > 0 ? box.y1 : a.v < 0 ? box.y0 : 0;
  const r = (rotation * Math.PI) / 180, cs = Math.cos(r), sn = Math.sin(r);
  return { x: at.x - (ax * cs - ay * sn), y: at.y - (ax * sn + ay * cs) };
}

/**
 * +1 where the wall runs to the right on screen at `along` (its left edge is its start side), −1 where it runs to the left
 * (a vertical one: up is +1), so "left" and "right" read the same on the screen whichever way the wall was drawn.
 */
export function screenSense(arc: Arc, along: number): 1 | -1 {
  const t = tangentAt(arc, along);
  return t.x < -1e-9 || (Math.abs(t.x) <= 1e-9 && t.y < 0) ? -1 : 1;
}

/**
 * How far along the wall (m, along its axis) the opening's centre lies from the cursor's place: the anchor edge is at the
 * cursor, so a door's left edge on the cursor puts its centre half its width to the cursor's right.
 */
export function alongShift(arc: Arc, along: number, width: number, h: -1 | 0 | 1): number {
  return -h * screenSense(arc, along) * (width / 2);
}

/** A point on the wall's side moved along its axis to the grid: `step` m from the wall's start (0: where it was). Grid snap for what is placed along a host (forum topic 226). */
export function snapAlong(arc: Arc, at: Vec2, step: number): Vec2 {
  if (!(step > 0)) return at;
  const a = project(arc, at), s = Math.round(a / step) * step;
  if (Math.abs(s - a) < 1e-12) return at;
  const q = pointAt(arc, a), p = pointAt(arc, s);
  return { x: at.x + p.x - q.x, y: at.y + p.y - q.y };
}

/** A point moved `shift` m along the wall's axis from where it falls on it, its side of the wall kept: the opening's centre for a cursor that is at its anchor. */
export function shiftedAlong(arc: Arc, at: Vec2, shift: number): Vec2 {
  if (!shift) return at;
  const a = project(arc, at), q = pointAt(arc, a), p = pointAt(arc, a + shift);
  return { x: at.x + p.x - q.x, y: at.y + p.y - q.y };
}
