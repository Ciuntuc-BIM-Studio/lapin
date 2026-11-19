/** Double-click on a dimension: the value typed for the segment under the cursor drives what it measures (and, in a string, what follows); on a text tag, its words (issue #92). */

import { promptText } from '../shell/prompts';
import type { Vec2 } from '../platform/module';
import { execute, query, useSession } from '../engine/session';
import { formatLength, parseLength, unitName } from '../engine/unitsStore';
import { openTableEditor } from '../modules/drafting/table/tableStore';
import { askText } from '../shell/AskBox';

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
  // A drafting table opens its editor (issue #71).
  if (node?.props.predefinedType === 'TABLE') { openTableEditor(id); return true; }
  // A long table's part placed on its own (stage 5) opens its table.
  if (node?.props.predefinedType === 'TABLEPART' && typeof node.props.of === 'string') { openTableEditor(node.props.of); return true; }
  // A text tag (issue #92): its words asked again, the tag changed in one step.
  if (node?.props.predefinedType === 'TAG' && typeof node.props.text === 'string') {
    const text = await askText('Text tag', 'What it says ({fields} of the element live)', node.props.text);
    if (text != null && text.trim() && text !== node.props.text) await execute('drafting.updateTag', { id, text });
    return true;
  }
  if (node?.props.predefinedType !== 'DIMENSION') return false;
  const d = await query<DimensionInfo>('drafting.dimension', { id, ...(at ? { at: [at.x, at.y] } : {}) });
  if (!d || d.value == null) return true;
  if (d.orphan) { useSession.setState({ error: 'The dimension lost its element: free it in its properties first.' }); return true; }
  if (!d.driving) { useSession.setState({ error: 'Neither end of this segment holds on an element: there is nothing to move.' }); return true; }
  const which = (d.segments ?? 1) > 1 ? ` (segment ${(d.segment ?? 0) + 1} of ${d.segments})` : '';
  const answer = promptText('Dimension value ({unit}){which}: the element moves to match', formatLength(d.value), { unit: unitName(), which });
  const v = answer == null ? null : parseLength(answer);
  if (v != null && v > 0) await execute('drafting.setDimensionValue', { id, value: v, ...(d.segment != null ? { segment: d.segment } : {}) });
  return true;
}
