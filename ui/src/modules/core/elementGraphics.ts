/**
 * An element's graphics in a view (decision 718, Revit's Override Graphics in View → By Element): the row the engine keeps
 * on the view (`elementGraphics`), what the dialog edits, and the command that writes it (`view.elementGraphics`, one undo step).
 */

import { useUi } from '../../app/store';

/** A line overridden: its colour, its weight on paper (mm), its line type. */
export interface LineRow { color?: string; weight?: number; lineType?: string }

export interface ElementGraphicsRow {
  cutHatch?: string; cutHatchScale?: number; cutHatchAngle?: number; cutFill?: string;
  surfaceHatch?: string; surfaceHatchScale?: number; surfaceHatchAngle?: number; surfaceFill?: string;
  hatchColor?: string; hatchHalftone?: boolean;
  cutLine?: LineRow; projectionLine?: LineRow;
  halftone?: boolean;
}

/** The view an element's graphics are set in now: the plan, section, elevation or 3D in front, without making one. */
export function frontViewIdNow(ui: { view: string; view3dId?: string | null; drawingViewId?: string | null; planViewId?: string | null } = useUi.getState()): string | null {
  if (ui.view === '3d') return ui.view3dId ?? '3d';
  if (ui.view === 'drawing') return ui.drawingViewId ?? null;
  if (ui.view === 'plan') return ui.planViewId ?? null;
  return null;
}

function cleanLine(l: LineRow | undefined): LineRow | undefined {
  if (!l) return undefined;
  const o: LineRow = {};
  if (l.color) o.color = l.color;
  if (typeof l.weight === 'number' && l.weight > 0) o.weight = l.weight;
  if (l.lineType) o.lineType = l.lineType;
  return Object.keys(o).length ? o : undefined;
}

/** A row as sent: empty fields, a halftone off and empty lines left out; null when nothing is left (the element as its material and category draw it). */
export function cleanRow(row: ElementGraphicsRow): ElementGraphicsRow | null {
  const o: ElementGraphicsRow = {};
  for (const k of ['cutHatch', 'cutFill', 'surfaceHatch', 'surfaceFill', 'hatchColor'] as const) if (row[k]) o[k] = row[k];
  for (const k of ['cutHatchScale', 'surfaceHatchScale'] as const) { const v = row[k]; if (typeof v === 'number' && v > 0) o[k] = v; }
  for (const k of ['cutHatchAngle', 'surfaceHatchAngle'] as const) { const v = row[k]; if (typeof v === 'number' && Number.isFinite(v)) o[k] = v; }
  if (row.hatchHalftone) o.hatchHalftone = true;
  if (row.halftone) o.halftone = true;
  const cut = cleanLine(row.cutLine);
  if (cut) o.cutLine = cut;
  const proj = cleanLine(row.projectionLine);
  if (proj) o.projectionLine = proj;
  return Object.keys(o).length ? o : null;
}

/** What several elements' rows share, field by field (a field they differ in left empty): the dialog's start for a selection. */
export function commonRow(rows: (ElementGraphicsRow | undefined)[]): ElementGraphicsRow {
  if (!rows.length) return {};
  const first = rows[0] ?? {};
  const o: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(first)) {
    const same = rows.every((r) => JSON.stringify((r as Record<string, unknown> | undefined)?.[k]) === JSON.stringify(v));
    if (same) o[k] = v;
  }
  return o as ElementGraphicsRow;
}

/** Where a change goes: the view in front, or every plan, section and elevation. */
export type Scope = 'view' | 'all';

/** The command's arguments: the row (null clears it) for the elements in the view, or in every view. */
export function elementGraphicsArgs(viewId: string, ids: string[], row: ElementGraphicsRow | null, scope: Scope): Record<string, unknown> {
  return { ...(scope === 'all' ? { scope: 'all' } : { id: viewId }), ids, value: row ? cleanRow(row) : null };
}

/** Whether an element has graphics of its own in a view (its row in the view's `elementGraphics`). */
export function hasOwnGraphics(view: { props: Record<string, unknown> } | undefined, id: string): boolean {
  const rows = view?.props.elementGraphics as Record<string, unknown> | undefined;
  return !!rows && typeof rows === 'object' && id in rows;
}
