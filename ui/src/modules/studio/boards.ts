/**
 * Studio's boards on the shared sketch canvas (audit S11 stage 1, docs/plan-sketch-canvas.md): an outline's sketch (on a plan of
 * its own), a part's own sketch (on a reference plane of its own) and the sketch on a face or a plane (the 3D view's work plane).
 * Each is a view the CAD session draws on; these say whose it is, what Finish writes back and what Cancel drops.
 */

import { activeStorey, execute, useSession } from '../../engine/session';
import type { NodeDto } from '../../engine/protocol';
import { cancel as cancelCommand } from '../../viewport/cad/session';
import { registerBoard, SKETCH_TOOLS, type Board } from '../../viewport/sketch/board';
import { clearWorkPlane, draftsOn, finishSketch, useWorkPlane } from '../../viewport/sketch/sketch3d';
import { frameOfView } from '../../viewport/workPlane';
import { cancelOutline, finishOutline } from './outlineSketch';
import { cancelPartBoard, finishPartBoard, leavePartSketch } from './PartSketchEdit';

const HINT = 'Draw with Draft, constrain with Studio → Constraints';

/** A floor's, an opening's, a ceiling's, a room's or a platform's outline (forum topic 97): Finish writes the element's outline. */
function outlineBoard(viewId: string, nodes: ReadonlyMap<string, NodeDto>): Board | null {
  const of = nodes.get(viewId)?.props.outlineSketchOf;
  if (typeof of !== 'string') return null;
  return {
    kind: 'outline', viewId, title: nodes.get(of)?.name ?? of, hint: HINT, tools: SKETCH_TOOLS, constraints: true,
    finish: () => finishOutline(viewId), cancel: () => cancelOutline(viewId),
  };
}

/** A part's own sketch (decision 520): Finish writes it into the part's definition; a part's sketch takes no ellipse. */
function partBoard(viewId: string, nodes: ReadonlyMap<string, NodeDto>): Board | null {
  const v = nodes.get(viewId);
  if (!v || !v.props.partSketchOf || typeof v.props.partSketchOf !== 'object') return null;
  return {
    kind: 'part', viewId, title: v.name.replace(/^Sketch: /, ''), hint: HINT, tools: SKETCH_TOOLS.filter((c) => c !== 'ELLIPSE'), constraints: true,
    finish: async () => { await finishPartBoard(viewId); }, cancel: () => cancelPartBoard(viewId), leave: leavePartSketch,
  };
}

/**
 * Studio's sketch on a face or a plane (decision 374): what is drawn is in the model as it is drawn. Finish welds the lines and gives
 * the newest closed shape the push/pull arrow; Cancel takes away what was drawn since the sketch began (one step); Esc leaves it,
 * what is drawn staying.
 */
function faceBoard(viewId: string, nodes: ReadonlyMap<string, NodeDto>): Board | null {
  const plane = useWorkPlane.getState().plane;
  if (!plane || plane.planeId !== viewId || !nodes.has(viewId)) return null;
  return {
    kind: 'face', viewId, title: plane.name, tools: SKETCH_TOOLS, constraints: false,
    finish: () => finishSketch(frameOfView(viewId, useSession.getState().nodes), plane.pick ? null : activeStorey()?.id ?? null, () => cancelCommand(true)),
    cancel: async () => {
      const was = new Set(plane.before);
      const made = draftsOn(viewId, useSession.getState().nodes).filter((n) => !was.has(n.id)).map((n) => n.id);
      if (made.length) await execute('element.delete', { ids: made });
      clearWorkPlane();
    },
    leave: clearWorkPlane,
  };
}

registerBoard(outlineBoard);
registerBoard(partBoard);
registerBoard(faceBoard);
