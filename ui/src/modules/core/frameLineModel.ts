/**
 * How a detail callout's boundary and an inset view's frame are drawn (issue #73): a pen weight (paper mm), a colour and a line
 * type, each the element's own (`lineWeight`, `lineColor`, `lineType`), else the project's in the drafting standards
 * (`calloutLine`, `insetFrame`: `standards.frameLine`), else Lapin's own. The engine resolves them the same way (FrameLines.cs).
 */

export interface FrameLine { weight?: number; color?: string; lineType?: string }
export type FrameKind = 'callout' | 'inset';

/** The pen weights offered (paper mm), as Visibility / Graphics offers them. */
export const FRAME_WEIGHTS = [0.13, 0.18, 0.25, 0.35, 0.5, 0.7, 1.0];

/** Lapin's own: a callout's boundary 0.35 black (grey while it refers to no view), an inset's frame 0.18 grey. */
export function lapinFrame(kind: FrameKind, refers = true): Required<FrameLine> {
  if (kind === 'inset') return { weight: 0.18, color: '#555555', lineType: 'continuous' };
  return refers ? { weight: 0.35, color: '#000000', lineType: 'continuous' } : { weight: 0.35, color: '#8f98a3', lineType: 'continuous' };
}

const num = (v: unknown) => (typeof v === 'number' && Number.isFinite(v) && v > 0 ? v : undefined);
const str = (v: unknown) => (typeof v === 'string' && v.length > 0 ? v : undefined);

/** A frame line read from the standards ({weight, color, lineType}); empty when there is none. */
export function readFrameLine(raw: unknown): FrameLine {
  const o = raw && typeof raw === 'object' ? raw as Record<string, unknown> : {};
  return { weight: num(o.weight), color: str(o.color), lineType: str(o.lineType) };
}

/** An element's own frame line, from its properties. */
export function ownFrameLine(props: Record<string, unknown>): FrameLine {
  return { weight: num(props.lineWeight), color: str(props.lineColor), lineType: str(props.lineType) };
}

/** What draws when a field is left empty: the project's, else Lapin's own. */
export function inheritedFrame(kind: FrameKind, project: FrameLine, refers = true): Required<FrameLine> {
  const own = lapinFrame(kind, refers);
  return { weight: project.weight ?? own.weight, color: project.color ?? own.color, lineType: project.lineType ?? own.lineType };
}

/** The weights a picker offers, the one set kept in the list when it is not one of them. */
export function weightChoices(current?: number): number[] {
  return current != null && !FRAME_WEIGHTS.some((w) => Math.abs(w - current) < 1e-9) ? [...FRAME_WEIGHTS, current].sort((a, b) => a - b) : FRAME_WEIGHTS;
}

/** The argument an element's field is set by (`view.updateCallout`, `view.updateInset`); empty: null, the project's again. */
export function ownPatch(field: keyof FrameLine, value: number | string | null | undefined): Record<string, unknown> {
  const key = field === 'weight' ? 'lineWeight' : field === 'color' ? 'lineColor' : 'lineType';
  return { [key]: value === '' || value == null ? null : value };
}

/** The argument the project's field is set by (`standards.frameLine`); empty: null, Lapin's own again. */
export function standardPatch(kind: FrameKind, field: keyof FrameLine, value: number | string | null | undefined): Record<string, unknown> {
  return { kind, [field]: value === '' || value == null ? null : value };
}
