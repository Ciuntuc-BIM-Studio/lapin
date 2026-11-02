/**
 * Small vector helpers the views and the modules share: a point read from
 * an element's JSON, a point written back, the cross products, a number
 * shortened for a label.
 */

import type { Vec2 } from './module';

export interface Vec3 { x: number; y: number; z: number }

/** A point from JSON ([x, y, …]), its numbers coerced; null when it is not an array of two. */
export const pt = (raw: unknown): Vec2 | null => (Array.isArray(raw) && raw.length >= 2 ? { x: Number(raw[0]), y: Number(raw[1]) } : null);

/** A point from JSON only when both its coordinates are numbers. */
export const ptStrict = (raw: unknown): Vec2 | null => (Array.isArray(raw) && raw.length >= 2 && typeof raw[0] === 'number' && typeof raw[1] === 'number' ? { x: raw[0], y: raw[1] } : null);

/** A point as JSON: [x, y]. */
export const xy = (p: Vec2): number[] => [p.x, p.y];

/** The 2D cross product (the z of a × b). */
export const cross2 = (a: Vec2, b: Vec2): number => a.x * b.y - a.y * b.x;

/** The 3D cross product. */
export const cross3 = (a: Vec3, b: Vec3): Vec3 => ({ x: a.y * b.z - a.z * b.y, y: a.z * b.x - a.x * b.z, z: a.x * b.y - a.y * b.x });

/** A number for a label: whole from 100 up, else two decimals (an extent, a length in m). */
export const shortNumber = (v: number): string => (Math.abs(v) >= 100 ? Math.round(v).toString() : v.toFixed(2));
