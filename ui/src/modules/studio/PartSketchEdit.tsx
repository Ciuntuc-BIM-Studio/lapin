/**
 * A part's own sketch edited on the shared sketch canvas (decision 520; audit S11 stage 1, docs/plan-sketch-canvas.md). Entered from
 * the part tree (a click on the sketch) or by a double click on the part in its edit mode: the engine opens the sketch on a
 * reference plane of its own where this placed part puts it (`studio.editPartSketch`), the plane becomes the 3D view's work plane,
 * and the camera turns to look straight at it (animated; put back on leaving). There the sketch is drawn and edited as every sketch
 * is: the Draft commands (Line, Polyline, Rectangle, Circle, Arc, Offset, Trim, Extend, Fillet, Chamfer, Erase), the object snaps, the
 * grips, the constraint and dimension tools and their overlay, each its own undo step; the bar over the view (SketchBar) is the same
 * as an outline's. **Finish sketch** (or Esc with nothing in progress, or Tab) writes it back into the part's definition in one undo
 * step (`studio.finishPartSketch`, the shapes' and constraints' ids kept), **Cancel** drops it (`studio.cancelPartSketch`). The
 * sketch can also be opened flat (Open in 2D: the plane's own view, the same board).
 *
 * This overlay draws what the canvas does not: the sketch as this placed part's values make it (faint, behind) when they differ
 * from the part's defaults the board edits, with the note saying so.
 */

import { useEffect, useMemo } from 'react';
import * as THREE from 'three';
import type { Overlay3DProps, Vec2 } from '../../platform/module';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { t as tr } from '../../i18n';
import { useScope } from '../../platform/scope';
import { poseKey, useCamera } from '../../viewport/camera';
import { cancel, useCad } from '../../viewport/cad/session';
import { polylines } from '../../viewport/cad/shapes';
import { clearWorkPlane, setWorkPlane, useWorkPlane } from '../../viewport/sketch/sketch3d';
import { editedPartId, editPart } from './editMode';
import { usePartTree } from './PartTree';
import { editedSketch, localShapes, poseForSketch, sketchExtent, sketchToWorld, type SketchFrame } from './partSketchModel';
import { sketchHooks, usePartSketch } from './partSketchStore';
import type { PartTree, TreeSketch } from './partTreeModel';

// ─── Entering and leaving ─────────────────────────────────────────────────────────

/** Opens the sketch on its board and turns to it. Enters the part's edit mode first when it is not on. */
export async function enterPartSketch(partId: string, sketchId: string): Promise<boolean> {
  if (editedPartId() !== partId && !editPart(partId)) return false;
  const cur = usePartSketch.getState();
  if (cur.partId === partId && cur.sketchId === sketchId && cur.viewId) return true;
  // Another sketch open: written back first.
  if (cur.viewId && !(await finishPartBoard(cur.viewId))) return false;
  const tree = await query<PartTree>('studio.partTree', { id: partId });
  const found = tree?.sketches.find((s) => s.local && s.id === sketchId);
  const sk = found ? editedSketch(found).source : null;
  if (!sk?.frame) { notify('That sketch is not one of the part\'s own: use Constrain part first.'); return false; }
  const r = await execute('studio.editPartSketch', { partId, sketchId }) as { viewId?: string } | null;
  if (!r?.viewId) return false;
  const cam = useCamera.getState();
  const saved = { azimuth: cam.azimuth, pitch: cam.pitch, distance: cam.distance, target: { ...cam.target }, ortho: cam.ortho };
  const { centre, extent } = sketchExtent(localShapes(sk.shapes));
  cam.api?.orbitTo(poseForSketch(sk.frame, centre, extent, cam.fov), true);
  usePartSketch.setState({ partId, sketchId, viewId: r.viewId, saved });
  setWorkPlane({ planeId: r.viewId, name: useSession.getState().nodes.get(r.viewId)?.name ?? found?.name ?? sketchId });
  useUi.setState({ selected: [], selection: null, tool: null });
  return true;
}

/** The board let go in the interface: the camera as it was, the work plane off, back to 3D when its plane was shown flat. Not an undo step. */
function leaveBoard(viewId: string): void {
  const s = usePartSketch.getState();
  if (useCad.getState().command) cancel(true);
  if (useWorkPlane.getState().plane?.planeId === viewId) clearWorkPlane();
  const ui = useUi.getState();
  if (ui.view === 'drawing' && ui.drawingViewId === viewId) useUi.setState({ view: '3d', drawingViewId: null });
  if (s.viewId === viewId) {
    if (s.saved) useCamera.getState().api?.orbitTo({ azimuth: s.saved.azimuth, pitch: s.saved.pitch, distance: s.saved.distance, target: s.saved.target }, true);
    usePartSketch.setState({ partId: null, sketchId: null, viewId: null, saved: null });
  }
}

/** Finish sketch: the board written into the part (one undo step), then let go. False when the engine refused (the board stays). */
export async function finishPartBoard(viewId: string): Promise<boolean> {
  const of = useSession.getState().nodes.get(viewId)?.props.partSketchOf as { partId?: string } | undefined;
  const r = await execute('studio.finishPartSketch', { viewId }) as { partId?: string } | null;
  if (!r) { const e = useSession.getState().error; if (e) notify(e); return false; }
  leaveBoard(viewId);
  const id = r.partId ?? of?.partId;
  if (id && useSession.getState().nodes.has(id)) useUi.setState({ selected: [id], selection: id });
  return true;
}

/** Cancel: the board dropped, the part as it was. */
export async function cancelPartBoard(viewId: string): Promise<void> {
  await execute('studio.cancelPartSketch', { viewId });
  if (!useSession.getState().error) leaveBoard(viewId);
}

/** Leaves the sketch keeping its work (Esc, Tab, the edit mode ended): Finish. */
export function leavePartSketch(): void {
  const v = usePartSketch.getState().viewId;
  if (v) void finishPartBoard(v);
}

/** The sketch a double click on a part in its edit mode opens: the first of its own that has a body drawn from it, else the first. */
export const sketchToOpen = (tree: PartTree | null | undefined): TreeSketch | null => {
  const own = (tree?.sketches ?? []).filter((s) => s.local && s.frame);
  return own.find((s) => (s.bodies?.length ?? 0) > 0) ?? own[0] ?? null;
};

// ─── The overlay ───────────────────────────────────────────────────────────────────────

export function PartSketch3D({ camera, canvas }: Overlay3DProps) {
  useCamera();
  const { partId, sketchId, viewId } = usePartSketch();
  const scope = useScope((s) => s.scope);
  const tree = usePartTree(partId);
  const boardThere = useSession((s) => (viewId ? s.nodes.has(viewId) : false));
  const sketch: TreeSketch | undefined = tree?.sketches.find((s) => s.local && s.id === sketchId);
  const edited = useMemo(() => (sketch ? editedSketch(sketch) : null), [sketch]);
  const instanceShapes = useMemo(() => (edited?.differs ? localShapes(sketch?.shapes) : []), [edited, sketch?.shapes]);

  // It ends with the part's edit mode (the work kept), or with its board (undone away, finished elsewhere).
  useEffect(() => {
    if (!partId || !viewId) return;
    if (!boardThere) { leaveBoard(viewId); return; }
    if (!scope || editedPartId(scope) !== partId) leavePartSketch();
  }, [partId, viewId, scope, boardThere]);

  const cam = camera.current, rect = canvas.current?.getBoundingClientRect();
  const frame = sketch?.frame as SketchFrame | undefined;
  if (!partId || !sketch || !cam || !rect || !frame) return null;
  void poseKey();
  cam.updateMatrixWorld();
  const toScreen = (p: Vec2) => {
    const w = sketchToWorld(frame, p);
    const v = new THREE.Vector3(w.x, w.y, w.z).project(cam);
    return { x: ((v.x + 1) / 2) * rect.width, y: ((1 - v.y) / 2) * rect.height };
  };
  const pts = (line: Vec2[]) => line.map((p) => { const q = toScreen(p); return `${q.x.toFixed(1)},${q.y.toFixed(1)}`; }).join(' ');
  return (
    <>
      {instanceShapes.length > 0 && (
        <svg className="grip-overlay face-sketch part-sketch" data-testid="part-sketch" data-part-sketch-id={sketchId} style={{ pointerEvents: 'none' }}>
          <g className="sketch-overlay">
            {instanceShapes.flatMap((s) => polylines(s.shape).map((line, i) => <polyline key={`i${s.id}:${i}`} className="ps-instance" data-instance-shape={s.id} points={pts(line)} />))}
          </g>
        </svg>
      )}
      {instanceShapes.length === 0 && <span hidden data-testid="part-sketch" data-part-sketch-id={sketchId} />}
      {edited?.differs && <div className="part-sketch-note floating" data-testid="part-sketch-defaults">{tr('Editing the part: values shown are its defaults')}</div>}
    </>
  );
}

sketchHooks.leave = leavePartSketch;
sketchHooks.openFor = async (partId) => {
  const tree = await query<PartTree>('studio.partTree', { id: partId });
  const sk = sketchToOpen(tree);
  return sk ? enterPartSketch(partId, sk.id) : false;
};
