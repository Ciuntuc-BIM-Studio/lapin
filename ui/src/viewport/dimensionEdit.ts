/** Double-click on a dimension: the value typed for the segment under the cursor drives what it measures (and, in a string, what follows). */

import type { Vec2 } from '../platform/module';
import { execute, query, useSession } from '../engine/session';

export interface DimensionInfo {
  value: number | null;
  /** Each segment's value (a string of points). */
  values?: number[];
  /** The segment asked about. */
  segment?: number;
  segments?: number;
  orphan: boolean;
  driving: boolean;
  /** Whether each segment drives (an end of it holds on something). */
  drives?: boolean[];
  kind?: string;
}

/** `at`: where the double-click was (model coordinates): the segment nearest it is the one edited. */
export async function editDimension(id: string, at?: Vec2): Promise<boolean> {
  const node = useSession.getState().nodes.get(id);
  if (node?.props.predefinedType !== 'DIMENSION') return false;
  const d = await query<DimensionInfo>('drafting.dimension', { id, ...(at ? { at: [at.x, at.y] } : {}) });
  if (!d || d.value == null) return true;
  if (d.orphan) { useSession.setState({ error: 'The dimension lost its element: free it in its properties first.' }); return true; }
  if (!d.driving) { useSession.setState({ error: 'Neither end of this segment holds on an element: there is nothing to move.' }); return true; }
  const which = (d.segments ?? 1) > 1 ? ` (segment ${(d.segment ?? 0) + 1} of ${d.segments})` : '';
  const answer = window.prompt(`Dimension value (m)${which}: the element moves to match`, d.value.toFixed(3));
  const v = answer == null ? NaN : Number(answer.replace(',', '.'));
  if (v > 0) await execute('drafting.setDimensionValue', { id, value: v, ...(d.segment != null ? { segment: d.segment } : {}) });
  return true;
}
