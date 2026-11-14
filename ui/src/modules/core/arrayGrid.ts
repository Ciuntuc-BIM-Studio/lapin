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
