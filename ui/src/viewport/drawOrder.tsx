/**
 * The draw order of a view's drafted elements (report #44, decision 339): lines, polylines, arcs, circles, rectangles,
 * hatches and fills, texts, dimensions, tags and a DXF's shapes on a view, brought to the front, a step forward past
 * what they overlap, a step backward, to the back of the drafting (still over the model) or behind the model; the
 * engine keeps each element's place on its view (`drawOrder`), one undo step, and sheets, the PDF and the DXF draw
 * it as the screen does. From the right-click menu, the Draft tab's Order group, the keys (Ctrl+Shift+] / Ctrl+] /
 * Ctrl+[ / Ctrl+Shift+[) and AutoCAD's DRAWORDER (DRO: DR is the door) on the command line, with its Above and Under objects.
 */

import { BringToFront, ChevronDown, ChevronUp, Layers2, SendToBack, type LucideIcon } from 'lucide-react';
import { notify, useUi } from '../app/store';
import { execute, useSession } from '../engine/session';
import type { NodeDto } from '../engine/protocol';
import type { MenuItem } from '../shell/ContextMenu';
import { cadHost, cadLog, registerCommand, withSelection, type Step } from './cad/session';

export type DrawOrderMove = 'front' | 'forward' | 'backward' | 'back' | 'behind' | 'above' | 'under';

/** The four classic moves and the one behind the model, as the menus list them, with their keys. */
export const DRAW_ORDER: { move: DrawOrderMove; label: string; shortcut?: string; icon: LucideIcon }[] = [
  { move: 'front', label: 'Bring to front', shortcut: 'Ctrl+Shift+]', icon: BringToFront },
  { move: 'forward', label: 'Bring forward', shortcut: 'Ctrl+]', icon: ChevronUp },
  { move: 'backward', label: 'Send backward', shortcut: 'Ctrl+[', icon: ChevronDown },
  { move: 'back', label: 'Send to back', shortcut: 'Ctrl+Shift+[', icon: SendToBack },
  { move: 'behind', label: 'Send behind the model', icon: Layers2 },
];

/** Whether a node is drafted on a view: an IfcAnnotation with its view (a line, a hatch, a text, a dimension, a tag). */
export const isDrafted = (n: NodeDto | undefined): boolean => !!n && n.type === 'IfcAnnotation' && typeof n.props.viewId === 'string';

/** The drafted elements among `ids`. */
export function draftedOf(ids: string[], nodes: Map<string, NodeDto>): string[] {
  return ids.filter((id) => isDrafted(nodes.get(id)));
}

/** The selection's drafted elements moved in their views' draw order; refs: what "above" and "under" go next to. */
export async function drawOrder(move: DrawOrderMove, ids?: string[], refs?: string[]): Promise<boolean> {
  const drafted = draftedOf(ids ?? useUi.getState().selected, useSession.getState().nodes);
  if (!drafted.length) { notify('Select drafted elements on a view: lines, hatches, texts, dimensions, tags.'); return false; }
  await execute('drafting.drawOrder', { ids: drafted, move, ...(refs ? { refs } : {}) });
  return true;
}

/** The right-click menu's entries for the selection, when it holds drafted elements (none otherwise). */
export function drawOrderItems(ids: string[], nodes: Map<string, NodeDto>): MenuItem[] {
  const drafted = draftedOf(ids, nodes);
  if (!drafted.length) return [];
  return DRAW_ORDER.map((d) => ({ label: d.label, icon: <d.icon size={13} />, onSelect: () => void drawOrder(d.move, drafted) }));
}

/** DRAWORDER's reference objects for Above and Under: clicked one by one, Enter when done. */
function refsStep(ids: string[], move: 'above' | 'under'): Step {
  const refs: string[] = [];
  const step = (): Step => ({
    prompt: `Select reference objects (${refs.length} chosen), Enter when done`, want: 'select',
    point: (p) => { const id = cadHost()?.pickAny(p); if (typeof id === 'string' && !ids.includes(id) && !refs.includes(id)) refs.push(id); return step(); },
    enter: async () => {
      if (!refs.length) { cadLog('No reference objects chosen.'); return null; }
      await drawOrder(move, ids, refs);
      return null;
    },
  });
  return step();
}

/** AutoCAD's DRAWORDER (DRO): the selection, then where it goes [Above objects/Under objects/Front/Back/behind Model] <Back>. */
export function drawOrderCommand(): ReturnType<typeof withSelection> {
  return withSelection((ids) => {
    const drafted = draftedOf(ids, useSession.getState().nodes);
    if (!drafted.length) { cadLog('Nothing drafted is selected: lines, hatches, texts, dimensions, tags.'); return null; }
    const go = (move: DrawOrderMove) => async () => { await drawOrder(move, drafted); return null; };
    const above = () => refsStep(drafted, 'above'), under = () => refsStep(drafted, 'under');
    return {
      prompt: 'Object ordering option [Above objects/Under objects/Front/Back/behind Model] <Back>', want: 'none',
      keywords: { A: above, ABOVE: above, U: under, UNDER: under, F: go('front'), FRONT: go('front'), B: go('back'), BACK: go('back'), M: go('behind'), MODEL: go('behind') },
      enter: go('back'),
    };
  });
}

// DR is Lapin's door (Revit's letters), so AutoCAD's alias is DRO here.
registerCommand({ names: ['DRAWORDER', 'DRO'], label: 'Draw order', start: drawOrderCommand });
