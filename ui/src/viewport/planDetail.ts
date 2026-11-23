/**
 * How much a plan draws, by its scale (the engine's PlanDetail): coarse at 1:1000 and smaller (an overview: a
 * track's axis), medium down to 1:200 (a site plan: its centre line and rails), fine at 1:200 and larger (a
 * detail plan: every rail edge and sleeper). A plan item drawn only at some levels says which.
 */

import type { PlanItemDto } from '../engine/protocol';

export const COARSE = 0, MEDIUM = 1, FINE = 2;

export const planDetail = (scale: number): number => (scale >= 1000 - 1e-9 ? COARSE : scale <= 200 + 1e-9 ? FINE : MEDIUM);

export const shownAt = (it: PlanItemDto, detail: number): boolean => detail >= (it.minDetail ?? COARSE) && detail <= (it.maxDetail ?? FINE);

/**
 * A plan item as a plan at 1:scale draws it (the engine's PlanItem.OnPaper): one sized on paper (an MEP device's symbol, a
 * rise's mark) scaled round its anchor from the scale it is drawn at, so 1:50 and 1:100 print it the same size.
 */
export function onPaper(it: PlanItemDto, scale: number): PlanItemDto {
  const p = it.paper;
  if (!p || p.length < 3 || !(scale > 0) || !(p[2] > 0) || Math.abs(scale - p[2]) < 1e-9) return it;
  const k = scale / p[2];
  const points = it.points.map((v, i) => (i % 2 === 0 ? p[0] + (v - p[0]) * k : p[1] + (v - p[1]) * k));
  return { ...it, points, paper: null };
}

/** A dash pattern's lengths on paper (mm), the engine's table (Dashes). */
export const DASHES: Record<string, number[]> = {
  dashed: [3, 1.5], hidden: [1.5, 1], center: [6, 1.5, 1, 1.5], dotted: [0.3, 1], dashdot: [4, 1, 0.3, 1], dashdotdot: [6, 1, 0.3, 1, 0.3, 1],
};
