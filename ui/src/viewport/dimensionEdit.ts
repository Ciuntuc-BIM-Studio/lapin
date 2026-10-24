/** Double-click on a dimension: its value typed drives what it measures. */

import { execute, query, useSession } from '../engine/session';

export async function editDimension(id: string): Promise<boolean> {
  const node = useSession.getState().nodes.get(id);
  if (node?.props.predefinedType !== 'DIMENSION') return false;
  const d = await query<{ value: number; orphan: boolean; driving: boolean }>('drafting.dimension', { id });
  if (!d) return true;
  if (d.orphan) { useSession.setState({ error: 'The dimension lost its element: free it in its properties first.' }); return true; }
  if (!d.driving) { useSession.setState({ error: 'Neither end of this dimension holds on an element: there is nothing to move.' }); return true; }
  const answer = window.prompt('Dimension value (m): the element moves to match', d.value.toFixed(3));
  const v = answer == null ? NaN : Number(answer.replace(',', '.'));
  if (v > 0) await execute('drafting.setDimensionValue', { id, value: v });
  return true;
}
