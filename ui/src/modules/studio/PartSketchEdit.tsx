/**
 * A part's own sketch edited in place (docs/plan-studio-parametric.md §3.2, decision 520). Entered from the part tree (a click on the
 * sketch) or by a double click on the part in its edit mode; the camera turns to look straight at the sketch's plane (animated; put
 * back on leaving), the rest of the model stays dimmed (the edit scope), and a 2D layer over the 3D view draws the sketch through the
 * camera from its frame: the shapes coloured by how free they are, the constraint glyphs and dimensions (the same drawing as a plan's
 * sketch, `ConstraintOverlay`), grips to drag points (the engine solves each move: `studio.partSketchDragPreview` while the pointer
 * moves, `studio.partSketchDrag` on release), the drawing tools (Line, Rectangle, Circle, Arc, Delete: each change sends the whole
 * list of shapes with their ids, `studio.partSketchSetShapes`) and the constraint tools (the CAD commands, their picks made on this
 * sketch, `studio.partSketchAddConstraint`). Esc or Finish sketch leaves it for the part's edit mode.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import * as THREE from 'three';
import { Check, Circle as CircleIcon, Minus, Spline, Square, Trash2 } from 'lucide-react';
import type { Overlay3DProps, Vec2 } from '../../platform/module';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { t as tr } from '../../i18n';
import { useScope } from '../../platform/scope';
import { poseKey, useCamera } from '../../viewport/camera';
import { chipOf, type ConstraintDto, type SketchStatus } from '../../viewport/cad/constraints';
import { cancel, feedPoint, finishStep, startCommand, submit, useCad, type CadHost } from '../../viewport/cad/session';
import { nearestOnShape, polylines, type Shape } from '../../viewport/cad/shapes';
import { useSketch } from '../../viewport/cad/sketchStore';
import { ConstraintOverlay, type LocalSource } from './ConstraintOverlay';
import { constraintCommandOn, DIMENSIONS, GEOMETRIC } from './constraintTools';
import { editedPartId, editPart } from './editMode';
import { usePartTree } from './PartTree';
import {
  dragShapes, editedSketch, gripsOf, localShapes, POINTS_NEEDED, recordedConstraints, poseForSketch, previewShapes, rayToSketch, shapeAt, shapeFrom, shapesPayload, sketchExtent,
  sketchToWorld, snapToGrip, statusOf, withoutShape, withShape, type DrawnPoint, type DrawTool, type Grip, type LocalShape, type SketchFrame, type SketchTool,
} from './partSketchModel';
import { sketchHooks, usePartSketch } from './partSketchStore';
import type { PartTree, TreeSketch } from './partTreeModel';

// ─── Entering and leaving ─────────────────────────────────────────────────────────

/** What the CAD host answers with: the sketch as the overlay last drew it (read by the picks, which run outside React). */
const live: { partId: string; sketchId: string; shapes: LocalShape[]; scale: number } = { partId: '', sketchId: '', shapes: [], scale: 100 };

const keyOf = (partId: string, sketchId: string) => `partsketch:${partId}:${sketchId}`;

function hostFor(partId: string, sketchId: string): CadHost {
  const hits = (p: Vec2) => {
    const tol = 8 / Math.max(live.scale, 1e-6);
    return live.shapes.map((s) => ({ id: s.id, shape: s.shape, d: nearestOnShape(s.shape, p).dist })).filter((h) => h.d <= tol).sort((a, b) => a.d - b.d);
  };
  return {
    viewId: keyOf(partId, sketchId),
    pxPerMetre: () => live.scale,
    pickDraft: (p) => hits(p)[0] ?? null,
    pickDrafts: hits,
    pickAny: (p) => hits(p)[0]?.id ?? null,
    shapeOf: (id) => live.shapes.find((s) => s.id === id)?.shape ?? null,
    async addConstraint(kind, refs, extra) {
      const r = await execute('studio.partSketchAddConstraint', { partId, sketchId, kind, refs, ...extra });
      return !!r;
    },
  };
}

/** Turns to the sketch: its camera, its host for the constraint commands. Enters the part's edit mode first when it is not on. */
export async function enterPartSketch(partId: string, sketchId: string): Promise<boolean> {
  if (editedPartId() !== partId && !editPart(partId)) return false;
  const tree = await query<PartTree>('studio.partTree', { id: partId });
  const found = tree?.sketches.find((s) => s.local && s.id === sketchId);
  // The sketch as the part stores it: what is edited (editedSketch).
  const sk = found ? editedSketch(found).source : null;
  if (!sk?.frame) { notify('That sketch is not one of the part\'s own: use Constrain part first.'); return false; }
  const cam = useCamera.getState();
  const state = usePartSketch.getState();
  const saved = state.partId ? state.saved : { azimuth: cam.azimuth, pitch: cam.pitch, distance: cam.distance, target: { ...cam.target }, ortho: cam.ortho };
  const { centre, extent } = sketchExtent(localShapes(sk.shapes));
  const pose = poseForSketch(sk.frame, centre, extent, cam.fov);
  cam.api?.orbitTo(pose, true);
  usePartSketch.setState({ partId, sketchId, tool: null, selected: null, saved });
  useCad.setState({ host: hostFor(partId, sketchId) });
  return true;
}

/**
 * The families dialog's way in: closes it, finds a placed part of the type in the project, opens its edit mode on the sketch. A part
 * nobody placed has no object to edit it on: say so.
 */
export async function editPartSketchIn(typeId: string, sketchId: string): Promise<void> {
  const st = useSession.getState();
  const typed = new Set<string>();
  for (const r of st.relations.values()) if (r.class === 'IfcRelDefinesByType' && r.relating === typeId) for (const id of r.related) typed.add(id);
  const instance = [...st.nodes.values()].find((n) => n.props.operation === 'part' && typed.has(n.id));
  if (!instance) { notify('Place the part first: its sketch is edited in the part\'s edit mode, on an object.'); return; }
  useUi.setState({ dialog: null });
  if (await enterPartSketch(instance.id, sketchId)) return;
}

/** Leaves the sketch: the camera as it was, the constraint commands off. Not an undo step. */
export function leavePartSketch(): void {
  const s = usePartSketch.getState();
  if (!s.partId) return;
  if (useCad.getState().command) cancel(true);
  if (useCad.getState().host?.viewId.startsWith('partsketch:')) useCad.setState({ host: null });
  if (s.saved) useCamera.getState().api?.orbitTo({ azimuth: s.saved.azimuth, pitch: s.saved.pitch, distance: s.saved.distance, target: s.saved.target }, true);
  usePartSketch.setState({ partId: null, sketchId: null, tool: null, selected: null, saved: null });
}

/** The sketch a double click on a part in its edit mode opens: the first of its own that has a body drawn from it, else the first. */
export const sketchToOpen = (tree: PartTree | null | undefined): TreeSketch | null => {
  const own = (tree?.sketches ?? []).filter((s) => s.local && s.frame);
  return own.find((s) => (s.bodies?.length ?? 0) > 0) ?? own[0] ?? null;
};

// ─── The view's projection ───────────────────────────────────────────────────────────

interface Projection {
  width: number; height: number;
  toScreen(frame: SketchFrame, p: Vec2): { x: number; y: number };
  toSketch(frame: SketchFrame, x: number, y: number): Vec2 | null;
}

function projectionOf(cam: THREE.Camera, r: DOMRect): Projection {
  cam.updateMatrixWorld();
  const px = (p: { x: number; y: number; z: number }) => {
    const v = new THREE.Vector3(p.x, p.y, p.z).project(cam);
    return { x: ((v.x + 1) / 2) * r.width, y: ((1 - v.y) / 2) * r.height };
  };
  return {
    width: r.width, height: r.height,
    toScreen: (f, p) => px(sketchToWorld(f, p)),
    toSketch: (f, x, y) => {
      const nx = (x / r.width) * 2 - 1, ny = 1 - (y / r.height) * 2;
      const a = new THREE.Vector3(nx, ny, -1).unproject(cam), b = new THREE.Vector3(nx, ny, 1).unproject(cam);
      const d = b.sub(a).normalize();
      return rayToSketch(f, { x: a.x, y: a.y, z: a.z }, { x: d.x, y: d.y, z: d.z });
    },
  };
}

// ─── The overlay ───────────────────────────────────────────────────────────────────────

const DRAW_ICONS: Record<DrawTool | 'delete', typeof Minus> = { line: Minus, rectangle: Square, circle: CircleIcon, arc: Spline, delete: Trash2 };
const TOOL_LABELS: Record<SketchTool, string> = { line: 'Line', rectangle: 'Rectangle', circle: 'Circle', arc: 'Arc (3 points)', delete: 'Delete' };

/** Grip and pick distances, in pixels. */
const GRIP_PX = 9, SNAP_PX = 10, PICK_PX = 8;

export function PartSketch3D({ camera, canvas }: Overlay3DProps) {
  useCamera();
  const sketchState = usePartSketch();
  const { partId, sketchId, tool, selected } = sketchState;
  const scope = useScope((s) => s.scope);
  const tree = usePartTree(partId);
  const svg = useRef<SVGSVGElement>(null);
  const command = useCad((s) => s.command);
  const step = useCad((s) => s.step);
  const cursor = useCad((s) => s.cursor);
  const [drawn, setDrawn] = useState<DrawnPoint[]>([]);
  const pts = useMemo(() => drawn.map((d) => d.p), [drawn]);
  const [hover, setHover] = useState<Vec2 | null>(null);
  const [drag, setDrag] = useState<{ grip: Grip; to: Vec2; ok: boolean; reason?: string; shapes: { id: string; shape: Shape }[] } | null>(null);
  const [typed, setTyped] = useState('');
  const sketch: TreeSketch | undefined = tree?.sketches.find((s) => s.local && s.id === sketchId);
  // Editing a part's sketch edits the part: the sketch as stored (solved with the defaults) is drawn and sent back; this object's
  // own (solved with its values) is drawn faintly behind when its values move it.
  const edited = useMemo(() => (sketch ? editedSketch(sketch) : null), [sketch]);
  const shapes = useMemo(() => localShapes(edited?.source.shapes), [edited]);
  const instanceShapes = useMemo(() => (edited?.differs ? localShapes(sketch?.shapes) : []), [edited, sketch?.shapes]);
  const constraints = (edited?.source.constraints ?? []) as ConstraintDto[];
  const status = useMemo(() => (edited ? (statusOf(edited.source) as SketchStatus) : null), [edited]);
  const cmdOn = !!command && constraintCommandOn();

  // It ends with the part's edit mode, or with the sketch (removed, undone away).
  useEffect(() => {
    if (!partId) return;
    if (!scope || editedPartId(scope) !== partId) leavePartSketch();
    else if (tree && !sketch) leavePartSketch();
  }, [partId, scope, tree, sketch]);
  // A drawing tool and a constraint command exclude each other; a tool changed drops its points.
  useEffect(() => { if (cmdOn && tool) usePartSketch.setState({ tool: null }); }, [cmdOn, tool]);
  useEffect(() => { setDrawn([]); setHover(null); }, [tool, sketchId]);
  useEffect(() => { setTyped(''); }, [step]);

  const cam = camera.current, rect = canvas.current?.getBoundingClientRect();
  const frame = edited?.source.frame;
  const proj = cam && rect && frame ? projectionOf(cam, rect) : null;
  const toScreen = (p: Vec2) => proj!.toScreen(frame!, p);
  const scale = useMemo(() => {
    if (!proj || !frame) return 100;
    const a = proj.toScreen(frame, { x: 0, y: 0 }), b = proj.toScreen(frame, { x: 1, y: 0 });
    return Math.max(Math.hypot(b.x - a.x, b.y - a.y), 1e-6);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [poseKey(), rect?.width, rect?.height, frame]);
  live.partId = partId ?? ''; live.sketchId = sketchId ?? ''; live.shapes = shapes; live.scale = scale;
  const grips = useMemo(() => gripsOf(shapes), [shapes]);

  const local: LocalSource | undefined = useMemo(() => (partId && sketchId ? {
    key: keyOf(partId, sketchId), shapes, constraints, status,
    update: (args) => execute('studio.partSketchUpdateConstraint', { partId, sketchId, ...args }),
    remove: (ids) => execute('studio.partSketchRemoveConstraint', { partId, sketchId, ids }),
  } : undefined), [partId, sketchId, shapes, constraints, status]);

  /** The shapes sent back (the part's definition), with what drawing recorded (one undo step). */
  const send = async (list: { id?: string; shape: Shape }[], recorded: unknown[] = []) => {
    if (!partId || !sketchId) return;
    const r = await execute('studio.partSketchSetShapes', { partId, sketchId, shapes: shapesPayload(list), ...(recorded.length ? { constraints: recorded } : {}) });
    if (!r) { const e = useSession.getState().error; if (e) notify(e); return; }
    const o = r as { dropped?: unknown[]; refused?: unknown[] };
    if (Array.isArray(o.dropped) && o.dropped.length) notify('Some constraints could not stay with the shapes and were dropped.');
    if (Array.isArray(o.refused) && o.refused.length) notify('Some constraints the drawing made could not hold and were left out.');
  };

  /** A pointer's point on the sketch, snapped onto an existing point (the grip it snapped to with it). */
  const pointAt = (e: { clientX: number; clientY: number }, snap = true): { p: Vec2; grip: Grip | null } | null => {
    const r = svg.current?.getBoundingClientRect();
    if (!r || !proj || !frame) return null;
    const p = proj.toSketch(frame, e.clientX - r.left, e.clientY - r.top);
    if (!p) return null;
    const g = snap ? snapToGrip(grips, p, SNAP_PX / scale) : null;
    return g ? { p: g.at, grip: g } : { p, grip: null };
  };
  const point = (e: { clientX: number; clientY: number }, snap = true): Vec2 | null => pointAt(e, snap)?.p ?? null;

  // Esc, Delete and Tab: taken before the shell's.
  useEffect(() => {
    if (!partId) return;
    const key = (e: KeyboardEvent) => {
      const inField = !!(e.target as HTMLElement | null)?.closest?.('input, textarea, select');
      const st = usePartSketch.getState();
      if (e.key === 'Escape') {
        if ((e.target as HTMLElement | null)?.closest?.('[data-testid="constraint-edit"]')) return;
        e.preventDefault(); e.stopImmediatePropagation();
        if (useCad.getState().command) cancel();
        else if (st.tool && pts.length) setDrawn([]);
        else if (st.tool) usePartSketch.setState({ tool: null });
        else if (st.selected) usePartSketch.setState({ selected: null });
        else leavePartSketch();
      } else if ((e.key === 'Delete' || e.key === 'Backspace') && st.selected && !inField && !constraintSelected()) {
        e.preventDefault(); e.stopImmediatePropagation();
        usePartSketch.setState({ selected: null });
        void send(withoutShape(shapes, st.selected));
      }
    };
    window.addEventListener('keydown', key, true);
    return () => window.removeEventListener('keydown', key, true);
  });

  if (!partId || !sketchId || !sketch || !frame || !proj) return null;

  const pts2 = (line: Vec2[]) => line.map((p) => { const q = toScreen(p); return `${q.x.toFixed(1)},${q.y.toFixed(1)}`; }).join(' ');
  const chip = chipOf(status);
  const capture = !!tool || cmdOn;

  const onMove = (e: React.PointerEvent) => {
    const p = point(e);
    if (!p) return;
    if (cmdOn) useCad.setState({ cursor: p });
    else setHover(p);
  };
  const onDown = (e: React.PointerEvent) => {
    if (e.button === 2) { if (cmdOn) { e.preventDefault(); void finishStep(); } return; }
    if (e.button !== 0) return;
    const at = pointAt(e);
    if (!at) return;
    const p = at.p;
    if (cmdOn) { useCad.setState({ cursor: p }); void feedPoint(p); return; }
    if (tool === 'delete') {
      const hit = shapeAt(shapes, p, PICK_PX / scale, (s, q) => nearestOnShape(s, q).dist);
      if (hit) void send(withoutShape(shapes, hit.id));
      return;
    }
    if (tool) {
      // Ctrl (or ⌘) at the press: the point is placed free, no constraint recorded (as the main CAD).
      const next = [...drawn, { ...at, free: e.ctrlKey || e.metaKey }];
      if (next.length < POINTS_NEEDED[tool]) { setDrawn(next); return; }
      const made = shapeFrom(tool, next.map((d) => d.p));
      setDrawn([]);
      if (made) void send(withShape(shapes, made), recordedConstraints(tool, made, next));
      else notify('Those points make no shape.');
    }
  };
  /** A grip pressed: the point follows the pointer (the engine's preview), and the engine moves it when released. */
  const gripDown = (g: Grip) => (e: React.PointerEvent) => {
    if (e.button !== 0 || capture) return;
    e.stopPropagation();
    try { (e.currentTarget as Element).setPointerCapture(e.pointerId); } catch { /* not every target captures */ }
    const x0 = e.clientX, y0 = e.clientY;
    let moved = false, ask = 0, last: Vec2 = g.at;
    const move = (ev: PointerEvent) => {
      if (!moved && Math.hypot(ev.clientX - x0, ev.clientY - y0) < 4) return;
      moved = true;
      const p = point(ev, false);
      if (!p) return;
      last = p;
      const n = ++ask;
      void query<{ ok: boolean; reason?: string; shapes?: unknown }>('studio.partSketchDragPreview', { partId, sketchId, shape: g.shape, part: g.part, to: [p.x, p.y], values: 'defaults' }).then((r) => {
        if (n !== ask || !r) return;
        setDrag({ grip: g, to: p, ok: r.ok, reason: r.reason, shapes: dragShapes(r.shapes) });
      });
    };
    const up = () => {
      window.removeEventListener('pointermove', move, true);
      window.removeEventListener('pointerup', up, true);
      ask++;
      if (moved) void execute('studio.partSketchDrag', { partId, sketchId, shape: g.shape, part: g.part, to: [last.x, last.y] }).then((r) => { if (!r) { const er = useSession.getState().error; if (er) notify(er); } }).finally(() => setDrag(null));
      else { setDrag(null); usePartSketch.setState({ selected: g.shape }); }
    };
    window.addEventListener('pointermove', move, true);
    window.addEventListener('pointerup', up, true);
  };

  const preview: Shape[] = tool && tool !== 'delete' ? previewShapes(tool, pts, hover) : cmdOn && step?.preview && cursor ? step.preview(cursor) : [];
  const labels = cmdOn && step?.labels && cursor ? step.labels(cursor) : [];
  const forwardWheel = (e: React.WheelEvent) => { canvas.current?.dispatchEvent(new WheelEvent('wheel', e.nativeEvent)); };

  return (
    <>
      <svg ref={svg} className="grip-overlay face-sketch part-sketch" data-sketch3d="part-sketch" data-testid="part-sketch" data-part-sketch-id={sketchId} style={{ pointerEvents: 'none' }}>
        <g className="sketch-overlay">
          {instanceShapes.flatMap((s) => polylines(s.shape).map((line, i) => <polyline key={`i${s.id}:${i}`} className="ps-instance" data-instance-shape={s.id} points={pts2(line)} />))}
          {shapes.map((s) => (
            <g key={s.id}>
              {s.inProfile && s.shape.type === 'polyline' && s.shape.closed && <polygon className="ps-profile" points={pts2(s.shape.pts)} />}
              {polylines(drag?.shapes.find((d) => d.id === s.id)?.shape ?? s.shape).map((line, i) => (
                <polyline key={i} className={`sk-shape free${selected === s.id ? ' ps-selected' : ''}${drag ? ' ps-dragged' : ''}`} data-part-shape={s.id} points={pts2(line)} />
              ))}
              {!capture && polylines(s.shape).map((line, i) => (
                <polyline key={`h${i}`} className="ps-hit" data-overlay-hit points={pts2(line)} onPointerDown={(e) => { if (e.button === 0) usePartSketch.setState({ selected: s.id }); }} />
              ))}
            </g>
          ))}
        </g>
        <ConstraintOverlay local={local} toScreen={toScreen} toWorld={(x, y) => proj.toSketch(frame, x, y) ?? { x: 0, y: 0 }} scale={scale} view={{ id: null }} />
        <g className="sketch-overlay">
          {!capture && grips.map((g) => {
            const at = drag && drag.grip.shape === g.shape && drag.grip.part === g.part ? drag.to : g.at;
            const q = toScreen(at);
            return <rect key={`${g.shape}:${g.part}`} className="ps-grip" data-overlay-hit data-grip={`${g.shape}:${g.part}`} x={q.x - GRIP_PX / 2} y={q.y - GRIP_PX / 2} width={GRIP_PX} height={GRIP_PX} onPointerDown={gripDown(g)} />;
          })}
          {preview.flatMap((s, k) => polylines(s).map((line, i) => <polyline key={`p${k}:${i}`} className="ps-preview" points={pts2(line)} />))}
          {labels.map((l, i) => { const q = toScreen(l.at); return <text key={`l${i}`} className="ps-label" x={q.x + 12} y={q.y - 10}>{tr(l.text)}</text>; })}
          {tool && tool !== 'delete' && pts.map((p, i) => { const q = toScreen(p); return <circle key={i} className="ps-point" cx={q.x} cy={q.y} r={3.5} />; })}
          {drag && !drag.ok && drag.reason && <text className="sk-reason" x={12} y={24}>{drag.reason}</text>}
        </g>
        {capture && (
          <rect className="ps-capture" data-overlay-hit x={0} y={0} width={proj.width} height={proj.height} style={{ pointerEvents: 'all', cursor: 'crosshair' }}
            onPointerMove={onMove} onPointerDown={onDown} onContextMenu={(e) => e.preventDefault()} onWheel={forwardWheel} />
        )}
      </svg>
      <div className="part-sketch-bar" data-testid="part-sketch-bar" role="toolbar" aria-label={tr('Sketch tools')}>
        <span className="part-sketch-name" data-no-i18n>{sketch.name ?? sketch.id}</span>
        <span className={`sketch-chip inline ${chip.tone}`} role="status" data-testid="sketch-chip" data-tone={chip.tone}>
          <span className="sketch-chip-dot" /><span data-testid="sketch-chip-text">{chip.text}</span>
        </span>
        <span className="part-sketch-group">
          {(['line', 'rectangle', 'circle', 'arc', 'delete'] as SketchTool[]).map((k) => {
            const Icon = DRAW_ICONS[k];
            return (
              <button key={k} type="button" className={`icon mini${tool === k ? ' on' : ''}`} data-part-sketch-tool={k} title={tr(TOOL_LABELS[k])} aria-pressed={tool === k}
                onClick={() => { if (useCad.getState().command) cancel(true); usePartSketch.setState({ tool: tool === k ? null : k, selected: null }); }}><Icon size={14} /></button>
            );
          })}
        </span>
        <span className="part-sketch-group">
          {[...GEOMETRIC, ...DIMENSIONS].map((c) => {
            const Icon = c.icon as React.ComponentType<{ size?: number }> | undefined;
            return (
              <button key={c.kind} type="button" className={`icon mini${command === c.command || (c.kind === 'dimension' && command === 'CDIM') ? ' on' : ''}`} data-part-sketch-constraint={c.kind} title={tr(c.label)}
                onClick={() => { usePartSketch.setState({ tool: null }); void startCommand(c.command); }}>{Icon ? <Icon size={14} /> : c.label.slice(0, 2)}</button>
            );
          })}
        </span>
        {edited?.differs && <span className="part-sketch-note" data-testid="part-sketch-defaults">{tr('Editing the part: values shown are its defaults')}</span>}
        <button type="button" className="btn small" data-testid="part-sketch-finish" title={tr('Back to the part (Esc)')} onClick={() => leavePartSketch()}><Check size={12} />{tr('Finish sketch')}</button>
        {(tool || cmdOn) && (
          <div className="part-sketch-prompt" data-testid="part-sketch-prompt">
            <span data-no-i18n>{tool ? (tool === 'delete' ? tr('Click a shape to delete it') : `${tr(TOOL_LABELS[tool])}: ${pts.length + 1} / ${POINTS_NEEDED[tool as DrawTool]}`) : step?.prompt ?? ''}</span>
            {cmdOn && step?.want === 'text' && (
              <input className="part-sketch-input" data-testid="part-sketch-value" value={typed} autoFocus aria-label={tr('Value')} onChange={(e) => setTyped(e.target.value)}
                onKeyDown={(e) => { e.stopPropagation(); if (e.key === 'Enter') { const v = typed; setTyped(''); void submit(v); } else if (e.key === 'Escape') cancel(); }} />
            )}
          </div>
        )}
      </div>
    </>
  );
}

/** Whether a constraint is selected in the overlay (Delete then takes the constraint, not the shape). */
const constraintSelected = (): boolean => useSketch.getState().selected.length > 0;

sketchHooks.leave = leavePartSketch;
sketchHooks.openFor = async (partId) => {
  const tree = await query<PartTree>('studio.partTree', { id: partId });
  const sk = sketchToOpen(tree);
  return sk ? enterPartSketch(partId, sk.id) : false;
};
