/**
 * A type's layer's own pens (decision 756; the forum: "for each scale, a set of pens for the core and for the cladding, as in
 * ArchiCAD"). The layer's `graphics` are a material's kind of view (the engine's MaterialLayer.Graphics): this edits their cut
 * outline (`cutLine`) and the lines between the layers (`separatorLine`), for all scales and per range of scales (`byScale`),
 * and keeps whatever else they hold (a hatch, a fill) as it is. Unset, the layer draws with its material's lines.
 */

import { stepProblem, type ScaleStep } from '../core/materialGraphicsModel';

export type PenLineKey = 'cutLine' | 'separatorLine';
export type PenField = 'weight' | 'color';
/** A layer's graphics as the type stores them; null: none. */
export type LayerGraphics = Record<string, unknown> & { cutLine?: PenSpec; separatorLine?: PenSpec; byScale?: ScaleStep[] };
export interface PenSpec { weight?: number; color?: string; lineType?: string }

const clone = (g: LayerGraphics | null | undefined): LayerGraphics => (g ? structuredClone(g) : {});

/** A layer's graphics as a stored layer has them (anything but an object: none). */
export function graphicsOf(raw: unknown): LayerGraphics | null {
  return raw && typeof raw === 'object' && !Array.isArray(raw) && Object.keys(raw).length > 0 ? structuredClone(raw as LayerGraphics) : null;
}

/** The steps, as stored. */
export const penSteps = (g: LayerGraphics | null | undefined): ScaleStep[] => (Array.isArray(g?.byScale) ? g!.byScale! : []);

/** A pen's field, for all scales (step null) or in a step. */
export function penOf(g: LayerGraphics | null | undefined, step: number | null, line: PenLineKey, field: PenField): number | string | undefined {
  const holder = step === null ? g : penSteps(g)[step];
  return (holder?.[line] as PenSpec | undefined)?.[field];
}

/** A line with a field set or taken off (undefined); an empty line goes. */
function withField(holder: Record<string, unknown>, line: PenLineKey, field: PenField, v: number | string | undefined) {
  const l = { ...((holder[line] as PenSpec | undefined) ?? {}) } as Record<string, unknown>;
  if (v === undefined) delete l[field]; else l[field] = v;
  if (Object.keys(l).length) holder[line] = l; else delete holder[line];
}

/** The graphics with a pen's field set (undefined: taken back to the material's); null when nothing is left. */
export function setPen(g: LayerGraphics | null | undefined, step: number | null, line: PenLineKey, field: PenField, v: number | string | undefined): LayerGraphics | null {
  const o = clone(g);
  if (step === null) withField(o, line, field, v);
  else {
    const steps = penSteps(o);
    if (!steps[step]) return tidy(o);
    withField(steps[step] as Record<string, unknown>, line, field, v);
  }
  return tidy(o);
}

/** Another range of scales, after the last: from the scale after the last range's (1:100 for the first). */
export function addPenStep(g: LayerGraphics | null | undefined, scales: readonly number[]): LayerGraphics {
  const o = clone(g);
  const steps = [...penSteps(o)];
  const last = steps[steps.length - 1];
  const after = last ? (last.to ?? last.from ?? 0) : 50;
  const from = [...scales].sort((a, b) => a - b).find((s) => s > after) ?? (after > 0 ? after * 2 : 100);
  steps.push({ from });
  o.byScale = steps;
  return o;
}

/** A range's end set (undefined: open). */
export function setPenStepRange(g: LayerGraphics | null | undefined, step: number, end: 'from' | 'to', v: number | undefined): LayerGraphics | null {
  const o = clone(g);
  const s = penSteps(o)[step];
  if (!s) return tidy(o);
  if (v === undefined) delete s[end]; else s[end] = v;
  return tidy(o);
}

export function removePenStep(g: LayerGraphics | null | undefined, step: number): LayerGraphics | null {
  const o = clone(g);
  o.byScale = penSteps(o).filter((_, i) => i !== step);
  return tidy(o);
}

/** The graphics with an empty list of steps taken off; null when nothing is left. A step being written keeps its place. */
function tidy(o: LayerGraphics): LayerGraphics | null {
  if (Array.isArray(o.byScale) && o.byScale.length === 0) delete o.byScale;
  return Object.keys(o).length ? o : null;
}

/** What a range says wrong (its scales), or null; a range not yet given any pen is left out when the type is saved. */
export function penStepProblem(g: LayerGraphics | null | undefined): { index: number; text: string } | null {
  const steps = penSteps(g);
  for (let i = 0; i < steps.length; i++) {
    const p = stepProblem({ ...steps[i], cutLine: steps[i].cutLine ?? { weight: 0.25 } });
    if (p) return { index: i, text: p };
  }
  return null;
}

/** The graphics as the type is saved with them: the ranges that set nothing left out; undefined when nothing is left. */
export function layerGraphicsJson(g: LayerGraphics | null | undefined): LayerGraphics | undefined {
  if (!g) return undefined;
  const o = clone(g);
  if (Array.isArray(o.byScale)) o.byScale = o.byScale.filter((s) => Object.keys(s).some((k) => k !== 'from' && k !== 'to'));
  return tidy(o) ?? undefined;
}

/** Whether the layer has pens of its own (the button lit). */
export const hasOwnPens = (g: LayerGraphics | null | undefined): boolean => layerGraphicsJson(g) !== undefined;
