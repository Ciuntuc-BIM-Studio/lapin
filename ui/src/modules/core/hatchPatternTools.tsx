/**
 * Save as hatch pattern… (docs/plan-material-graphics.md, stage 7): drawn linework becomes a pattern of the project. SAVEHATCH takes the
 * selected linework, then the cell's two corners (or Enter: the drawing's own bounds), then a small dialog asks the name and the units and
 * `drafting.toPattern` makes it: exact line families when the lines fit a .pat, else a tile pattern.
 */

import { create } from 'zustand';
import type { NodeDto } from '../../engine/protocol';
import type { MenuItem } from '../../shell/ContextMenu';
import { useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { cadHost, cadLog, registerCommand, startCommand, withSelection, type Step } from '../../viewport/cad/session';
import type { Shape } from '../../viewport/cad/shapes';
import { registerConvertHandoff } from './convertHandoffs';
import { cellOf, type Cell } from './hatchPatternModel';

/** What the command has so far: the view, the linework, the cell (none: the linework's bounds). */
export interface SaveHatchRequest { viewId: string; ids: string[]; cell: Cell | null }
export const useSaveHatch = create<{ request: SaveHatchRequest | null }>(() => ({ request: null }));

/** Drawn linework: lines, polylines, arcs, circles and ellipses of a view (not texts, hatches or symbols). */
export const isHatchSource = (n: NodeDto | undefined): boolean =>
  !!n && n.type === 'IfcAnnotation' && typeof n.props.viewId === 'string' && !!n.props.shape && n.props.predefinedType !== 'SYMBOL' && n.props.predefinedType !== 'TEXT' && !n.props.hatch;

function saveAsHatch(): ReturnType<typeof withSelection> {
  return withSelection((ids) => {
    const nodes = useSession.getState().nodes;
    const sources = ids.filter((id) => isHatchSource(nodes.get(id)));
    const host = cadHost();
    if (!host) return null;
    if (!sources.length) { cadLog('Select drawn lines, arcs or circles to save as a hatch pattern.'); return null; }
    const open = (cell: Cell | null) => { useSaveHatch.setState({ request: { viewId: host.viewId, ids: sources, cell } }); useUi.setState({ dialog: 'saveHatch' }); return null; };
    return {
      prompt: 'First corner of the pattern\'s cell, or Enter for the drawing\'s bounds', want: 'point',
      enter: () => open(null),
      point: (a): Step => ({
        prompt: 'Opposite corner of the cell', want: 'point', base: a,
        preview: (c): Shape[] => [{ type: 'polyline', pts: [a, { x: c.x, y: a.y }, c, { x: a.x, y: c.y }], closed: true }],
        point: (c) => {
          const cell = cellOf(a, c);
          if (!cell) { cadLog('The cell needs a width and a height: click a corner that is not in line with the first.'); return null; }
          return open(cell);
        },
      }),
    };
  });
}
registerCommand({ names: ['SAVEHATCH', 'HATCHSAVE', 'SAVEPATTERN'], label: 'Save as hatch pattern', start: saveAsHatch });
registerConvertHandoff({ id: 'hatchPattern', label: 'Hatch pattern…', description: 'The drawn lines chosen saved as a hatch pattern of the project: the cell\'s corners are asked next, then its name and units.', run: () => void startCommand('SAVEHATCH') });

/** The right-click entry for drawn linework. */
export function hatchMenuItems(ids: string[], nodes: Map<string, NodeDto>): MenuItem[] {
  return ids.some((id) => isHatchSource(nodes.get(id))) ? [{ label: 'Save as hatch pattern…', onSelect: () => void startCommand('SAVEHATCH') }] : [];
}
