/**
 * The part's tree while a part is edited (docs/plan-studio-parametric.md §3.2): the origin's three planes (shown in 3D on hover
 * or click), the sketches, the steps (reordered, switched off, removed) and the parameters (their values edited for this
 * object), as the engine lists them (`studio.partTree`), asked again after each change of the model.
 */

import { useEffect } from 'react';
import * as THREE from 'three';
import { ArrowDown, ArrowUp, CircleCheckBig, Eye, EyeOff, PenTool, PencilRuler, Trash2 } from 'lucide-react';
import { create } from 'zustand';
import type { Overlay3DProps } from '../../platform/module';
import { showPropsTab } from '../../app/panels';
import { notify, useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { LengthField, NumberField } from '../../shell/cells';
import { formatLength } from '../../engine/unitsStore';
import { modelBounds } from '../../viewport/sectionBox';
import { useCamera } from '../../viewport/camera';
import { useScope } from '../../platform/scope';
import type { NodeDto } from '../../engine/protocol';
import { editedPartId, editPart, finishPart } from './editMode';
import { usePartTree } from './partTreeStore';
import { ParamsButton } from './PartParams';
import { editFamily } from './families/FamilyTools';
import { editSketch } from './sketch';
import { AddBody, AddSketch, BodyControls, ConstrainPartButton, LocalSketchActions } from './PartSketchTree';
import { enterPartSketch } from './PartSketchEdit';
import { chipOf, type SketchStatus } from '../../viewport/cad/constraints';
import { statusOf } from './partSketchModel';
import { refreshSketch, useSketch } from '../../viewport/cad/sketchStore';
import { PLANE_LABELS, paramRows, planeCorners, sketchRows, stepRows, type ParamRow, type PartPlane, type PartTree, type SketchRow } from './partTreeModel';

/** The origin plane shown in 3D: the one the pointer is over, else the one clicked. */
export const usePartPlane = create<{ hover: PartPlane | null; pinned: PartPlane | null }>(() => ({ hover: null, pinned: null }));

export { usePartTree } from './partTreeStore';

/** A sketch's constraints and how free it is, beside it in the tree; read again as the model changes. */
function SketchState({ viewId }: { viewId: string }) {
  const version = useSession((s) => s.version);
  const sketch = useSketch((s) => s.views[viewId]);
  useEffect(() => { void refreshSketch(viewId); }, [viewId, version]);
  if (!sketch?.constraints.length) return null;
  const n = sketch.constraints.length, chip = chipOf(sketch.status);
  return <span className={`muted small sketch-state ${chip.tone}`} data-testid="part-sketch-state">{n === 1 ? '1 constraint' : `${n} constraints`} · {chip.text}</span>;
}

const setValue = (id: string, key: string, value: number | null) => void execute('studio.setPartValues', { ids: [id], values: { [key]: value } });

function ParamValue({ id, p }: { id: string; p: ParamRow }) {
  if (p.derived) return <span className="muted" title={`= ${p.formula}`} data-no-i18n>{p.kind === 'length' ? formatLength(p.value) : String(Math.round(p.value * 1e6) / 1e6)}</span>;
  if (p.kind === 'bool') return <input type="checkbox" aria-label={p.label} checked={p.value !== 0} onChange={(e) => setValue(id, p.key, e.target.checked ? 1 : 0)} />;
  if (p.kind === 'length') return <LengthField metres={p.value} title={p.label} style={{ width: 76 }} onCommit={(m) => setValue(id, p.key, m)} />;
  return <NumberField value={Math.round(p.value * 1e6) / 1e6} title={p.label} style={{ width: 76 }} onCommit={(n) => setValue(id, p.key, n)} />;
}

const shownDefault = (p: ParamRow): string => (p.kind === 'bool' ? (p.default ? 'yes' : 'no') : p.kind === 'length' ? formatLength(p.default) : String(Math.round(p.default * 1e6) / 1e6));

function openSketch(tree: PartTree, row: SketchRow): void {
  if (row.opens === 'sketch') void enterPartSketch(tree.id, row.id);
  else if (row.opens === 'view') {
    // The sketch lives on its view, where the part's own isolation would hold its shapes out of reach: the edit mode ends, as the sketch opens.
    void finishPart().then(() => editSketch(tree.id));
  } else if (row.opens === 'body' && tree.partTypeId) editFamily(tree.partTypeId);
  else notify('That sketch is not in the model any more: redraw it to rebuild the solid.');
}

export function PartTreePanel() {
  const scope = useScope((s) => s.scope);
  const id = editedPartId(scope);
  const tree = usePartTree(id);
  const { hover, pinned } = usePartPlane();
  useEffect(() => { if (!id) usePartPlane.setState({ hover: null, pinned: null }); }, [id]);
  if (!id) return null;
  const sketches = tree ? sketchRows(tree) : [];
  const steps = tree ? stepRows(tree) : [];
  const params = tree ? paramRows(tree) : [];
  return (
    <div className="sel-card part-tree" role="region" aria-label="Part tree" data-testid="part-tree">
      <div className="row between">
        <b>Part tree</b>
        <button type="button" className="btn small" data-testid="part-finish" title="Leave the part (Tab or Esc)" onClick={() => void finishPart()}><CircleCheckBig size={12} />Finish part</button>
      </div>
      {!tree && <p className="muted small">Reading the part…</p>}
      {tree && (
        <>
          <div className="part-section">
            <div className="part-title">Origin</div>
            <ul className="part-list">
              {tree.planes.map((p) => (
                <li key={p} className={pinned === p ? 'on' : ''}>
                  <button type="button" className="link" data-part-plane={p} onMouseEnter={() => usePartPlane.setState({ hover: p })} onMouseLeave={() => usePartPlane.setState({ hover: null })}
                    onClick={() => usePartPlane.setState((s) => ({ pinned: s.pinned === p ? null : p }))}>
                    {pinned === p || hover === p ? <Eye size={12} /> : <EyeOff size={12} />}{PLANE_LABELS[p]}
                  </button>
                </li>
              ))}
            </ul>
          </div>
          <div className="part-section">
            <div className="part-title">Sketches</div>
            {!sketches.length && <p className="muted small">None.</p>}
            <ul className="part-list">
              {sketches.map((s) => (
                <li key={s.id} className={s.missing ? 'off' : ''}>
                  <button type="button" className="link" data-part-sketch={s.id} onClick={() => openSketch(tree, s)}><PenTool size={12} /><span data-no-i18n>{s.label}</span></button>
                  <span className="muted small" data-no-i18n>{s.plane}</span>
                  {s.viewId && !s.missing && <SketchState viewId={s.viewId} />}
                  {s.local && <span className="muted small" data-testid="part-sketch-dof">{chipOf(statusOf(tree.sketches.find((x) => x.id === s.id)!) as SketchStatus).text}</span>}
                  {s.local && <><span className="grow" /><LocalSketchActions partId={id} sketch={tree.sketches.find((x) => x.id === s.id)!} /></>}
                </li>
              ))}
            </ul>
            <ConstrainPartButton tree={tree} />
            <AddSketch partId={id} />
          </div>
          <div className="part-section">
            <div className="part-title">Steps</div>
            <ol className="part-list">
              {steps.map((s) => (
                <li key={s.index} className={s.suppressed ? 'off' : ''} data-part-step={s.index}>
                  <button type="button" className="link" onClick={() => { useUi.setState({ selection: id, selected: [id] }); showPropsTab('properties'); }}><span data-no-i18n>{s.label}</span></button>
                  {s.when != null && <span className="muted small">off when not <span data-no-i18n>{s.when}</span></span>}
                  <span className="grow" />
                  {s.canToggle && <button type="button" className="icon mini" title={s.suppressed ? 'Switched off: switch it on' : 'Switch it off (kept, not done)'} onClick={() => void execute('studio.updateFeature', { id, index: s.feature, set: { off: s.suppressed ? null : true } })}>{s.suppressed ? <EyeOff size={11} /> : <Eye size={11} />}</button>}
                  {s.feature != null && <>
                    <button type="button" className="icon mini" title="Earlier" disabled={!s.canUp} onClick={() => void execute('studio.moveFeature', { id, index: s.feature, to: s.feature! - 1 })}><ArrowUp size={11} /></button>
                    <button type="button" className="icon mini" title="Later" disabled={!s.canDown} onClick={() => void execute('studio.moveFeature', { id, index: s.feature, to: s.feature! + 1 })}><ArrowDown size={11} /></button>
                    <button type="button" className="icon mini" title="Remove the step" disabled={!s.canRemove} onClick={() => void execute('studio.removeFeature', { id, index: s.feature })}><Trash2 size={11} /></button>
                  </>}
                  {s.sketch && tree.sketches.some((x) => x.local && x.id === s.sketch) && <BodyControls tree={tree} step={s} />}
                </li>
              ))}
            </ol>
            <AddBody tree={tree} />
          </div>
          <div className="part-section">
            <div className="row between"><div className="part-title">Parameters</div><ParamsButton /></div>
            {!params.length && <p className="muted small">None.</p>}
            {params.length > 0 && (
              <table className="part-params">
                <thead><tr><th>Name</th><th>Value</th><th>Default</th><th /></tr></thead>
                <tbody>
                  {params.map((p) => (
                    <tr key={p.key} data-part-param={p.key} className={p.changed ? 'changed' : ''}>
                      <td title={p.key}><span data-no-i18n>{p.label}</span></td>
                      <td><ParamValue id={id} p={p} />{p.unit && <span className="muted small"> <span data-no-i18n>{p.unit}</span></span>}</td>
                      <td className="muted small" data-no-i18n>{p.derived ? `= ${p.formula}` : shownDefault(p)}</td>
                      <td className="muted small">{p.instance ? 'instance' : ''}{p.instance && p.published ? ' · ' : ''}{p.published ? 'published' : ''}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}
    </div>
  );
}

/** The origin plane in 3D: a translucent square through the part's origin, as large as the part, projected by the view's camera. */
export function PartPlane3D({ camera, canvas }: Overlay3DProps) {
  useCamera();
  const scope = useScope((s) => s.scope);
  const { hover, pinned } = usePartPlane();
  const id = editedPartId(scope);
  const node = useSession((s) => (id ? s.nodes.get(id) : undefined));
  const plane = hover ?? pinned;
  const cam = camera.current, r = canvas.current?.getBoundingClientRect();
  if (!id || !node || !plane || !cam || !r) return null;
  const st = useSession.getState();
  const item = st.render.get(id);
  const box = item ? modelBounds([item], 0, 0, st.meshes) : null;
  const at = Array.isArray(node.props.at) ? (node.props.at as number[]) : null;
  const centre = box ? [(box.min[0] + box.max[0]) / 2, (box.min[1] + box.max[1]) / 2, (box.min[2] + box.max[2]) / 2] : [0, 0, 0];
  const origin = at ? [at[0] ?? 0, at[1] ?? 0, at[2] ?? Number(node.props.z ?? box?.min[2] ?? 0)] : centre;
  const half = box ? Math.max(box.max[0] - box.min[0], box.max[1] - box.min[1], box.max[2] - box.min[2]) / 2 + 0.2 : 1;
  const turn = ((Number(node.props.rotation ?? 0) || 0) * Math.PI) / 180;
  const c = Math.cos(turn), s = Math.sin(turn);
  cam.updateMatrixWorld();
  const points = planeCorners(plane, half).map(([x, y, z]) => {
    const v = new THREE.Vector3(origin[0]! + x * c - y * s, origin[1]! + x * s + y * c, origin[2]! + z).project(cam);
    return v.z > 1 || v.z < -1 ? null : `${((v.x + 1) / 2) * r.width},${((1 - v.y) / 2) * r.height}`;
  });
  if (points.some((p) => !p)) return null;
  return (
    <svg className="grip-overlay face-sketch" data-sketch3d="part-plane" style={{ pointerEvents: 'none' }}>
      <polygon points={points.join(' ')} className="part-plane" />
    </svg>
  );
}

/** Properties of a Studio solid: the way into the part's edit mode (Tab, or a double click in 3D). */
export function EditPartSection({ node }: { node: NodeDto }) {
  const editing = editedPartId(useScope((s) => s.scope)) === node.id;
  const tree = usePartTree(node.props.operation === 'part' ? node.id : null);
  return (
    <dl className="fields" data-section="edit-part">
      <dt />
      <dd>
        <ConstrainPartButton tree={tree} className="btn small constrain-part" />
        {editing
          ? <button type="button" data-field="finish-part" onClick={() => void finishPart()}><CircleCheckBig size={13} /> Finish part</button>
          : <button type="button" data-field="edit-part" title="Tab, or a double click on it in 3D" onClick={() => editPart(node.id)}><PencilRuler size={13} /> Edit part</button>}
      </dd>
    </dl>
  );
}
