/**
 * On-canvas handles in the 3D view (Studio's push/pull, split, slice, move):
 * a distance set along an axis by the mouse or typed, shown on the model as
 * it will come out before it is applied. A tool begins one with the axes it
 * moves along (a face's normal; x, y and z for a move) and what it shows: a
 * prism swept from a face (push/pull: what is added, or taken away), a plane
 * (split, slice) or a box (move); the overlay (AxisDrag3D.tsx) draws it and
 * takes the mouse and the keys until it is applied (a click, Enter) or
 * dropped (Esc).
 */

import { create } from 'zustand';
import type { Vec3 } from './camera';

export interface DragAxis {
  /** Unit direction a positive distance goes. */
  dir: Vec3;
  /** A name shown on the handle (x, y, z), none for a face's normal. */
  name?: string;
  /** Its colour (CSS); the accent when none. */
  color?: string;
}

export type DragPreview =
  /** The face swept by the distance: added when positive, taken away when negative. */
  | { kind: 'prism'; outline: Vec3[] }
  /** The face's plane moved by the distance (where a split or a slice cuts). */
  | { kind: 'plane'; outline: Vec3[] }
  /** A box carried by the distance (what a move takes). */
  | { kind: 'box'; min: Vec3; max: Vec3 };

export interface AxisDragSpec {
  /** The operation's name, in the chip over the view. */
  title: string;
  /** Where the handles stand (a face's middle, a box's centre). */
  at: Vec3;
  axes: DragAxis[];
  preview: DragPreview;
  /** The distance it starts at (m). */
  start?: number;
  /** Below this the distance is held. */
  min?: number;
  /** Whether a distance of nothing is applied too (a split in the face's own plane); otherwise it does nothing. */
  zero?: boolean;
  /** What the distance means, beside the value (in the chip). */
  hint?: string;
  /** Choices shown in the chip as toggles (a slice's side): their labels, the one on. */
  options?: { label: string; values: { value: string; label: string }[]; value: string };
  /** Applies it; the drag ends whatever it returns. */
  commit(distance: number, axis: number, option?: string): void | Promise<unknown>;
}

interface AxisDragState {
  spec: AxisDragSpec | null;
  /** The axis being set: the only one, else the handle grabbed last (z at first). */
  axis: number;
  distance: number;
  /** A typed value: while it parses, it is the distance and the mouse does not change it. */
  text: string;
  option: string | undefined;
}

export const useAxisDrag = create<AxisDragState>(() => ({ spec: null, axis: 0, distance: 0, text: '', option: undefined }));

/** The distance last applied by each operation, Enter's value when nothing was dragged or typed (as SketchUp repeats it). */
const lastBy = new Map<string, number>();
export const lastDistance = (title: string) => lastBy.get(title);

export function beginAxisDrag(spec: AxisDragSpec): void {
  useAxisDrag.setState({ spec, axis: spec.axes.length - 1, distance: spec.start ?? 0, text: '', option: spec.options?.value });
}

export function endAxisDrag(): void {
  useAxisDrag.setState({ spec: null, axis: 0, distance: 0, text: '', option: undefined });
}

/** Applied: the distance (the typed one, else the dragged one, else the last applied when nothing moved) along the axis set. */
export async function commitAxisDrag(typed: number | null): Promise<void> {
  const { spec, axis, distance, option } = useAxisDrag.getState();
  if (!spec) return;
  let d = typed ?? distance;
  if (typed == null && Math.abs(d - (spec.start ?? 0)) < 1e-9 && lastBy.has(spec.title)) d = lastBy.get(spec.title)!;
  endAxisDrag();
  if (Math.abs(d) < 1e-9 && !spec.zero) return;
  lastBy.set(spec.title, d);
  await spec.commit(d, axis, option);
}

const dot = (a: Vec3, b: Vec3) => a.x * b.x + a.y * b.y + a.z * b.z;
export const add = (a: Vec3, b: Vec3, k = 1): Vec3 => ({ x: a.x + b.x * k, y: a.y + b.y * k, z: a.z + b.z * k });

/**
 * How far along the line through `at` in the unit direction `dir` the point
 * nearest the ray (origin, dir) lies: the distance a drag sets; null when the
 * ray runs along the line (the axis seen end on: it cannot be dragged so).
 */
export function alongLine(at: Vec3, dir: Vec3, origin: Vec3, ray: Vec3): number | null {
  const w = { x: at.x - origin.x, y: at.y - origin.y, z: at.z - origin.z };
  const b = dot(dir, ray), c = dot(ray, ray), d = dot(dir, w), e = dot(ray, w);
  const den = c - b * b;
  if (Math.abs(den) < 1e-9 * c) return null;
  return (b * e - c * d) / den;
}

/** A dragged distance kept to the centimetre (Shift: to 10 cm), and not under the spec's least. */
export function snapDistance(d: number, coarse = false, min?: number): number {
  const step = coarse ? 0.1 : 0.01;
  const v = Math.round(d / step) * step;
  return Math.round((min != null ? Math.max(min, v) : v) * 1e6) / 1e6;
}

/** The middle of a face's outline (its corners' average: where its handle stands). */
export function middleOf(outline: Vec3[]): Vec3 {
  const n = Math.max(outline.length, 1);
  return outline.reduce((s, p) => ({ x: s.x + p.x / n, y: s.y + p.y / n, z: s.z + p.z / n }), { x: 0, y: 0, z: 0 });
}

/** The twelve edges of a box (min, max) carried by `by`. */
export function boxEdges3(min: Vec3, max: Vec3, by: Vec3 = { x: 0, y: 0, z: 0 }): [Vec3, Vec3][] {
  const c = (i: number): Vec3 => ({ x: (i & 1 ? max.x : min.x) + by.x, y: (i & 2 ? max.y : min.y) + by.y, z: (i & 4 ? max.z : min.z) + by.z });
  const pairs: [number, number][] = [[0, 1], [2, 3], [4, 5], [6, 7], [0, 2], [1, 3], [4, 6], [5, 7], [0, 4], [1, 5], [2, 6], [3, 7]];
  return pairs.map(([a, b]) => [c(a), c(b)]);
}

/** The x, y and z axes of a move, coloured as the 3D view's axes are by custom (x red, y green, z blue). */
export const XYZ: DragAxis[] = [
  { dir: { x: 1, y: 0, z: 0 }, name: 'x', color: '#d64541' },
  { dir: { x: 0, y: 1, z: 0 }, name: 'y', color: '#3f9c4a' },
  { dir: { x: 0, y: 0, z: 1 }, name: 'z', color: '#3a6fd8' },
];
