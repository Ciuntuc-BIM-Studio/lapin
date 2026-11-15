/**
 * The arithmetic of an array in a row or a grid (forum 90), kept apart from the tool so it is tested alone: where each item
 * stands (the engine's order: column by column along a row, row by row, then level by level), and the element.array
 * arguments for a step given by two clicked points.
 */

export type V2 = { x: number; y: number };
export type V3 = [number, number, number];

/** What the dialog asked of a row or a grid whose steps are clicked in a plan. */
export interface StepJob {
  ids: string[];
  kind: 'row' | 'grid';
  /** The copies with the first (a row), the columns (a grid). */
  count: number;
  rows: number;
  levels: number;
  /** The rise from one copy (a row) or one column (a grid) to the next. */
  dz: number;
  levelStep: V3;
}

/** The offset of each item from the original, the original first. */
export function gridOffsets(count: number, rows: number, levels: number, step: V3, rowStep: V3 = [0, 0, 0], levelStep: V3 = [0, 0, 0]): V3[] {
  const out: V3[] = [];
  for (let l = 0; l < Math.max(1, levels); l++)
    for (let r = 0; r < Math.max(1, rows); r++)
      for (let c = 0; c < Math.max(1, count); c++)
        out.push([0, 1, 2].map((k) => step[k] * c + rowStep[k] * r + levelStep[k] * l) as V3);
  return out;
}

/** The step from the first clicked point to the second (and, in a grid, to the third: the step between rows). */
export const stepBetween = (a: V2, b: V2): V2 => ({ x: b.x - a.x, y: b.y - a.y });

/** The element.array arguments for a job whose column step (and row step) were clicked. */
export function stepArgs(job: StepJob, step: V2, rowStep?: V2): Record<string, unknown> {
  const s: V3 = [step.x, step.y, job.dz];
  if (job.kind === 'row') return { ids: job.ids, count: job.count, step: s };
  const o: Record<string, unknown> = { ids: job.ids, kind: 'grid', count: job.count, step: s, rows: job.rows, rowStep: [rowStep?.x ?? 0, rowStep?.y ?? 0, 0] };
  if (job.levels > 1) { o.levels = job.levels; o.levelStep = job.levelStep; }
  return o;
}

/** How many items a row or a grid makes. */
export const itemCount = (job: Pick<StepJob, 'kind' | 'count' | 'rows' | 'levels'>): number =>
  job.kind === 'row' ? job.count : job.count * Math.max(1, job.rows) * Math.max(1, job.levels);

/** Where an array is being made: a plan (its x and y the model's), a section, an elevation or a drafting view (its own), or 3D. */
export type ArrayPlace = 'plan' | 'view' | '3d';

/** What an array is made of and what it leaves out, and why (forum 90). */
export interface ArrayTargets {
  ids: string[];
  /** Elements selected in a section, an elevation or a drafting view: arrayed in a plan or in 3D. */
  skippedElements: number;
  /** Drafted shapes selected in 3D: arrayed in their own view. */
  skippedDrafts: number;
  /** Dimensions and tags: they measure or name their elements. */
  skippedNotes: number;
}

/** The drafting an array copies (forum 90): drafted shapes (lines, polylines, circles, arcs, hatches), texts and symbols. */
const ARRAYED_DRAFTING = new Set(['DRAFT', 'TEXT', 'SYMBOL']);

interface ArrayNode { type: string; storeyId?: string | null; props: { predefinedType?: unknown; viewId?: unknown } }

/**
 * What of a selection an array takes where it is made: the elements and groups (on a storey) in a plan or in 3D, the drafted
 * shapes, texts and symbols on a view in a plan or in that view, never a dimension or a tag; both at once in a plan, where
 * the two share the model's x and y. What is left out is counted, to say so.
 */
export function arrayTargets(selected: readonly string[], nodes: ReadonlyMap<string, ArrayNode>, place: ArrayPlace): ArrayTargets {
  const out: ArrayTargets = { ids: [], skippedElements: 0, skippedDrafts: 0, skippedNotes: 0 };
  for (const id of selected) {
    const n = nodes.get(id);
    if (!n) continue;
    if (n.type === 'IfcAnnotation' && typeof n.props.viewId === 'string') {
      if (!ARRAYED_DRAFTING.has(String(n.props.predefinedType ?? ''))) { out.skippedNotes++; continue; }
      if (place === '3d') { out.skippedDrafts++; continue; }
      out.ids.push(id);
      continue;
    }
    if (!n.storeyId && n.type !== 'IfcGroup') continue;
    if (place === 'view') { out.skippedElements++; continue; }
    out.ids.push(id);
  }
  return out;
}

/** What to tell of what an array left out (English: the interface translates it). */
export function arraySkipNotes(t: ArrayTargets): string[] {
  const o: string[] = [];
  if (t.skippedElements) o.push('Elements are arrayed in a plan or in 3D: here only the drafted shapes, texts and symbols were arrayed.');
  if (t.skippedDrafts) o.push('Drafted shapes are arrayed in their own view (a plan, a section, an elevation or a drafting view): only the elements were arrayed.');
  if (t.skippedNotes) o.push('Dimensions and tags measure or name their elements: they were left out of the array.');
  return o;
}
