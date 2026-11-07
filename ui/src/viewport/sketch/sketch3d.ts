/**
 * The 3D sketch's shared core (docs/plan-studio-sketch.md, decision 374).
 * Two ways of drawing on a plane in the 3D view lie over it:
 *
 * - the direct way, SketchUp's (Sketch3D.tsx): Studio's Line, Rectangle and
 *   Circle draw right on the face under the cursor, or on the storey's level
 *   in empty space, inferred by inference.ts, sized by a typed value (vcb.ts);
 * - the work-plane way, CAD's (Draft3D.tsx): Sketch on face makes the face's
 *   plane the active work plane, lit, and the command line's own commands
 *   (LINE, PLINE, RECTANGLE, CIRCLE, ARC…) draw in its x, y; Finish sketch.
 *
 * Both store the same thing, a sketch on the reference plane lying on the
 * face (studio.onFace; the storey's level in empty space), and end the same
 * way: a closed shape gets the push/pull arrow at once (Esc keeps a sketch).
 */

import { create } from 'zustand';
import type { Vec2 } from '../../platform/module';
import type { NodeDto } from '../../engine/protocol';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { useCamera, type CameraPose, type Vec3 } from '../camera';
import { beginAxisDrag, middleOf } from '../axisDrag';
import { fromJson, type Shape } from '../cad/shapes';
import { toWorld, type WorkFrame } from '../workPlane';

/** A face picked in 3D, for the engine: the element, the point and the normal. */
export interface FacePick { id: string; point: number[]; normal: number[] }

// ─── The active work plane (the CAD way) ────────────────────────────────────

export interface WorkPlaneState {
  /** The reference plane drafted on (a face's, made by Sketch on face). */
  planeId: string;
  name: string;
  /** The face it lies on, when it is one: its pick (for the push/pull) and its outline, lit. */
  pick?: FacePick;
  outline?: Vec3[];
  /** The drafts on the plane before the sketch began: Finish sketch pushes or pulls what came after. */
  before: string[];
}

export const useWorkPlane = create<{ plane: WorkPlaneState | null }>(() => ({ plane: null }));

/** The drafted shapes lying on a view. */
export const draftsOn = (viewId: string, nodes: ReadonlyMap<string, NodeDto>): NodeDto[] =>
  [...nodes.values()].filter((n) => n.props.viewId === viewId && n.type === 'IfcAnnotation' && !!n.props.shape);

export function setWorkPlane(p: Omit<WorkPlaneState, 'before'>): void {
  const before = draftsOn(p.planeId, useSession.getState().nodes).map((n) => n.id);
  useWorkPlane.setState({ plane: { ...p, before } });
}
export const clearWorkPlane = () => useWorkPlane.setState({ plane: null });

/** A closed shape's outline (a circle in chords), or null for an open one. */
export function closedOutline(s: Shape | null, n = 48): Vec2[] | null {
  if (!s) return null;
  if (s.type === 'polyline' && s.closed && s.pts.length >= 3) return s.pts;
  if (s.type === 'circle') return Array.from({ length: n }, (_, i) => ({ x: s.c.x + s.r * Math.cos((2 * Math.PI * i) / n), y: s.c.y + s.r * Math.sin((2 * Math.PI * i) / n) }));
  return null;
}

/**
 * What Finish sketch takes: the closed shapes drafted on the plane since the sketch began (the newest last), and the
 * open lines among them (welded first by JOIN, so a closed run of lines is a shape too).
 */
export function sketchResult(plane: WorkPlaneState, nodes: ReadonlyMap<string, NodeDto>): { closed: NodeDto[]; open: NodeDto[] } {
  const was = new Set(plane.before);
  const made = draftsOn(plane.planeId, nodes).filter((n) => !was.has(n.id));
  const shape = (n: NodeDto) => fromJson(n.props.shape as Record<string, unknown>);
  return {
    closed: made.filter((n) => closedOutline(shape(n)) !== null),
    open: made.filter((n) => { const s = shape(n); return s?.type === 'line' || (s?.type === 'polyline' && !s.closed); }),
  };
}

// ─── The push/pull that follows a closed shape (both ways) ───────────────────

const ON_FACE: Record<string, string> = {
  solid: 'A solid stands on the face, from the shape drawn on it: Push/Pull its end, or change the sketch, and it follows.',
  cut: 'Cut into the face, from the shape drawn on it: Push/Pull the pocket\'s floor to make it deeper or shallower.',
};

const selectMade = (r: unknown) => {
  const id = (r as { id?: string } | null)?.id;
  if (id) useUi.setState({ selected: [id], selection: id });
};

/**
 * The push/pull arrow on a closed shape, at once: out a solid of its own, in a cut into the face's element (on a face),
 * Esc or 0 keeps it a sketch. `stored`: what the engine is given for the shape (an outline or a circle not yet stored,
 * kept in the same step; or the id of a sketch already drafted). Off any face (a plane, the storey's level) the sketch
 * is extruded alone, on the storey given.
 */
export function pushPullShape(o: {
  outline: Vec3[]; normal: Vec3; pick?: FacePick; storeyId?: string | null;
  stored: { outline: Vec3[] } | { circle: { center: Vec3; radius: number } } | { sketchId: string };
}): void {
  const { pick, stored } = o;
  const p3 = (v: Vec3) => [v.x, v.y, v.z];
  const shapeArgs = 'sketchId' in stored ? { sketchId: stored.sketchId }
    : 'circle' in stored ? { circle: { center: p3(stored.circle.center), radius: stored.circle.radius } }
      : { outline: stored.outline.map(p3) };
  const keep = async () => {
    if ('sketchId' in stored || !pick) return;
    if (await execute('studio.onFace', { ...pick, ...shapeArgs })) notify('The shape stays on the face as a sketch: extrude it later, or draw over it.');
  };
  beginAxisDrag({
    title: 'Push/Pull', at: middleOf(o.outline), axes: [{ dir: o.normal }], preview: { kind: 'prism', outline: o.outline },
    hint: 'Out adds, in takes away · Esc keeps the sketch', zero: true,
    cancel: () => void keep(),
    commit: async (d) => {
      if (Math.abs(d) < 1e-9) { await keep(); return; }
      if (pick) {
        const r = await execute('studio.onFace', { ...pick, ...shapeArgs, distance: d }) as { how?: string; id?: string } | null;
        if (!r) return;
        if (r.how === 'solid') selectMade(r);
        notify(ON_FACE[r.how ?? ''] ?? 'Done.');
        return;
      }
      if (!('sketchId' in stored)) return;
      const r = await execute('studio.extrude', { profileId: stored.sketchId, depth: d, ...(o.storeyId ? { storeyId: o.storeyId } : {}) });
      if (r) { selectMade(r); notify('Extruded from the sketch: it follows the sketch, and Push/Pull changes its depth.'); }
    },
  });
}

/**
 * Finish sketch (the CAD way): the command running is ended, the open lines drawn welded, and the newest closed shape
 * on the plane gets the push/pull arrow; the work plane is let go. Nothing closed: the lines stay a sketch.
 */
export async function finishSketch(frame: WorkFrame | null, storeyId: string | null, cancelCommand: () => void): Promise<void> {
  const plane = useWorkPlane.getState().plane;
  cancelCommand();
  if (!plane) return;
  clearWorkPlane();
  let { closed, open } = sketchResult(plane, useSession.getState().nodes);
  if (!closed.length && open.length >= 2) {
    await execute('drafting.join', { ids: open.map((n) => n.id) });
    ({ closed, open } = sketchResult(plane, useSession.getState().nodes));
  }
  const last = closed[closed.length - 1];
  const outline = last ? closedOutline(fromJson(last.props.shape as Record<string, unknown>)) : null;
  if (!last || !outline || !frame) {
    notify(open.length ? 'The lines stay on the plane as a sketch: close an outline to push or pull it.' : 'Sketch finished.');
    return;
  }
  pushPullShape({
    outline: outline.map((p) => toWorld(frame, p)), normal: frame.normal, pick: plane.pick, storeyId,
    stored: { sketchId: last.id },
  });
}

// ─── Look at plane ─────────────────────────────────────────────────────────

/** The camera's pose looking square at a plane from its front (the side its normal faces), at a point of it. */
export function poseFacing(normal: Vec3, at: Vec3, distance: number): CameraPose {
  const lim = Math.PI / 2 - 0.01;
  const l = Math.hypot(normal.x, normal.y, normal.z) || 1;
  const n = { x: normal.x / l, y: normal.y / l, z: normal.z / l };
  const pitch = Math.max(-lim, Math.min(lim, Math.asin(Math.max(-1, Math.min(1, -n.z)))));
  const h = Math.hypot(n.x, n.y);
  // A level plane seen from above keeps north up (as the plan does).
  const azimuth = h > 1e-6 ? Math.atan2(-n.y, -n.x) : Math.PI / 2;
  return { azimuth, pitch, distance, target: at };
}

/** Look at plane: the 3D camera square to the plane, its middle (or the point given) in front. */
export function lookAtPlane(normal: Vec3, at: Vec3): void {
  const cam = useCamera.getState();
  cam.api?.orbitTo(poseFacing(normal, at, cam.distance));
}
