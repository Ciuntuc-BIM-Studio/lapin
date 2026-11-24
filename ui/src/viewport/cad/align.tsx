/**
 * ALIGN (AL; Modify → Align), Revit's Align (forum topics 109, 170, 171): the reference line first (a wall's face, axis or
 * reference line, a grid axis, a reference plane, a floor's or a column's edge, a drafted line…), then the line on the element
 * to move: the element moves square to the reference until that line lies on it (element.align, by element.transform's move:
 * a wall keeps its joins as a move does; a door or window slides along its wall), one undo step. A line not parallel to the
 * reference is turned onto its direction first in a plan (forum topic 234), by the smaller angle about the point picked.
 * After one, a new reference is asked, as Revit does; with Multiple (or Ctrl held on the pick) the same reference takes one
 * element after another until Enter or Esc. With Lock, an element aligned to a grid axis or a reference plane is linked to it
 * (element.lockTo) and follows it from then on. In a section or an elevation the lines are the view's, and the move goes up
 * or down in the model (decision 453's frame).
 */

import { create } from 'zustand';
import type { Vec2 } from '../../platform/module';
import { execute, useSession } from '../../engine/session';
import { arcOf, isArc } from '../arc';
import { referenceArc } from '../wallReference';
import { xy } from '../../platform/vec';
import { nearestLine, type Line, type Shape } from './shapes';
import { cadHost, cadLog, ctrlWasHeld, frameArg, registerCommand, submit, useCad, type Step } from './session';

export { nearestLine, segmentsOf, type Line } from './shapes';

/** The options the command bar shows: Multiple alignment and Lock, remembered between runs. */
export const useAlign = create<{ multiple: boolean; lock: boolean }>(() => ({ multiple: false, lock: false }));

/** How far from parallel (radians) two lines may be and still align: the engine's ElementAlign.AngleTolerance. */
export const ALIGN_TOLERANCE = 0.1 * Math.PI / 180;

/** Whether two lines run the same way (either sense), within the tolerance. */
export function parallel(r: Line, l: Line): boolean {
  const u = { x: r[1].x - r[0].x, y: r[1].y - r[0].y }, v = { x: l[1].x - l[0].x, y: l[1].y - l[0].y };
  const lu = Math.hypot(u.x, u.y), lv = Math.hypot(v.x, v.y);
  if (lu < 1e-12 || lv < 1e-12) return false;
  return Math.abs(u.x * v.y - u.y * v.x) / (lu * lv) <= Math.sin(ALIGN_TOLERANCE);
}

/** What an element offers to align by in this view: its lines here (a plan's modelling lines, a grid axis's or a plane's line), and a straight wall's, beam's or footing's axis and kept reference line. */
export function elementLines(id: string): Shape[] {
  const out: Shape[] = [...(cadHost()?.outlinesOf?.(id) ?? [])];
  if (!cadHost()?.isPlan?.()) return out;
  const n = useSession.getState().nodes.get(id);
  if (n) {
    const axis = arcOf(n.props);
    if (axis && !isArc(axis)) out.push({ type: 'line', a: axis.a, b: axis.b });
    const ref = referenceArc(n.props);
    if (ref && !isArc(ref.arc)) out.push({ type: 'line', a: ref.arc.a, b: ref.arc.b });
  }
  return out;
}

/** What lies under the cursor to align by: a drafted shape's straight piece, else an element's line nearest it. */
function lineUnder(at: Vec2): { id: string; line: Line } | null {
  const h = cadHost();
  if (!h) return null;
  const draft = h.pickDraft(at);
  if (draft) { const l = nearestLine([draft.shape], at); return l ? { id: draft.id, line: l } : null; }
  const id = h.pickAny(at);
  const l = id ? nearestLine(elementLines(id), at) : null;
  return id && l ? { id, line: l } : null;
}

/** Whether an alignment to this reference can be locked (element.lockTo): a grid's axis (grid#tag) or a reference plane. */
export function lockable(refId: string): boolean {
  const nodes = useSession.getState().nodes;
  const hash = refId.indexOf('#');
  if (hash > 0) return nodes.get(refId.slice(0, hash))?.type === 'IfcGrid';
  const n = nodes.get(refId);
  return n?.type === 'LapinView' && n.props.kind === 'plane';
}

const asShape = (l: Line): Shape => ({ type: 'line', a: l[0], b: l[1] });
const onOff = (b: boolean) => (b ? 'on' : 'off');
const keys = (back: () => Step): Record<string, () => Step> => {
  const multiple = () => { useAlign.setState({ multiple: !useAlign.getState().multiple }); return back(); };
  const lock = () => { useAlign.setState({ lock: !useAlign.getState().lock }); return back(); };
  return { M: multiple, MULTIPLE: multiple, L: lock, LOCK: lock };
};
const flags = () => `[Multiple: ${onOff(useAlign.getState().multiple)} / Lock: ${onOff(useAlign.getState().lock)}]`;

export function alignCommand(): Step {
  const reference = (): Step => ({
    prompt: `Align: pick the reference line (a face, an axis, a grid axis, an edge, a drafted line) ${flags()}`, want: 'pick',
    hint: 'Align: click the reference line (a wall\'s face or axis, a grid axis, a reference plane, an edge, a drafted line) · Esc cancels',
    keywords: keys(reference), pickInViews: true,
    preview: (c) => { const u = lineUnder(c); return u ? [asShape(u.line)] : []; },
    pick: (hit, at) => take(hit.id, nearestLine([hit.shape], at)),
    pickElement: (id, at) => take(id, nearestLine(elementLines(id), at)),
    element: () => { cadLog('Align works in a plan, a section or an elevation: pick the lines there.'); return reference(); },
    enter: () => null,
  });
  const take = (id: string, line: Line | null): Step => {
    if (!line) { cadLog('No straight line there: pick a face, an axis or an edge.'); return reference(); }
    return target(id, line, false);
  };
  const target = (refId: string, ref: Line, again: boolean): Step => {
    const go = async (id: string, line: Line | null, at: Vec2): Promise<Step | null> => {
      if (id === refId) { cadLog('Pick a line on another element: the reference stays where it is.'); return target(refId, ref, again); }
      if (!line) { cadLog('No straight line there: pick a face, an axis or an edge parallel to the reference.'); return target(refId, ref, again); }
      // Not parallel: in a plan, turned onto the reference's direction about the point picked, then moved (forum topic 234,
      // Revit's Align on lines), in the same undo step; in a section or an elevation a turn would tip the element over.
      const turn = !parallel(ref, line);
      if (turn && frameArg(at).frame) { cadLog('Not parallel to the reference: in a section or an elevation, Align moves an element square to it (Rotate it first).'); return target(refId, ref, again); }
      const multiple = useAlign.getState().multiple || ctrlWasHeld();
      const lock = useAlign.getState().lock && lockable(refId);
      if (useAlign.getState().lock && !lock) cadLog('Lock keeps an alignment to a grid axis or a reference plane only: aligned without it.');
      await execute('element.align', {
        ids: [id], reference: ref.map(xy), line: line.map(xy), referenceId: refId, ...(lock ? { lock: true } : {}), ...(turn ? { rotate: true, pivot: xy(at) } : {}), ...frameArg(at),
      });
      return multiple ? target(refId, ref, true) : reference();
    };
    return {
      prompt: again
        ? `Align: pick the next element's line, parallel to the reference ${flags()} (Enter to finish)`
        : `Align: pick the line on the element to move (turned parallel to the reference if it is not) ${flags()}`,
      want: 'pick', pickInViews: true,
      hint: again
        ? 'Align: click the next element\'s line, parallel to the reference · Enter or Esc finishes'
        : 'Align: click the line on the element to move (it turns parallel to the reference if it is not) · Ctrl: several · Esc cancels',
      keywords: keys(() => target(refId, ref, again)),
      preview: (c) => { const u = lineUnder(c); return [asShape(ref), ...(u && u.id !== refId ? [asShape(u.line)] : [])]; },
      pick: (hit, at) => go(hit.id, nearestLine([hit.shape], at), at),
      pickElement: (id, at) => go(id, nearestLine(elementLines(id), at), at),
      element: () => { cadLog('Align works in a plan, a section or an elevation: pick the lines there.'); return target(refId, ref, again); },
      enter: () => null,
    };
  };
  return reference();
}

/** The command bar's options while ALIGN runs: Multiple alignment and Lock (to a grid axis or a reference plane). */
export function AlignOptions() {
  const { multiple, lock } = useAlign();
  const step = useCad((s) => s.step);
  // The prompt shows the flags: a click re-asks the step through its keyword, so it says the new state.
  const toggle = (k: 'M' | 'L') => { if (step) void submit(k); };
  return (
    <span className="align-bar" data-testid="align-bar">
      <button type="button" className={`cad-toggle${multiple ? ' on' : ''}`} data-action="align-multiple" aria-pressed={multiple}
        title="Align one element after another to the same reference, until Enter or Esc (or hold Ctrl on the pick)" onClick={() => toggle('M')}>Multiple alignment</button>
      <button type="button" className={`cad-toggle${lock ? ' on' : ''}`} data-action="align-lock" aria-pressed={lock}
        title="Keep what is aligned to a grid axis or a reference plane linked to it: it follows when the axis or plane moves (and is pinned)" onClick={() => toggle('L')}>Lock</button>
    </span>
  );
}

registerCommand({ names: ['ALIGN', 'AL'], label: 'Align', start: alignCommand, options: AlignOptions });
