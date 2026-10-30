/** A Studio solid in the inspector: what it is made by, its sketch, its steps. */

import { Fragment, useEffect, useState } from 'react';
import { ArrowDown, ArrowUp, PenTool, Trash2 } from 'lucide-react';
import type { NodeDto } from '../../engine/protocol';
import { execute, query, useSession } from '../../engine/session';
import { relatingOf } from '../../engine/model';
import { notify, useUi } from '../../app/store';
import { formatLength, parseLength, unitLabel } from '../../engine/unitsStore';
import { editSketch, type Feature, type FeatureStep } from './sketch';
import { solidFieldKeys } from './thick';

interface Offered { class: string; label: string; predefined: string[] }
let offered: Promise<Offered[] | null> | null = null;

/** What the card calls each operation. */
const OPS: Record<string, string> = {
  extrude: 'Extrude', sweep: 'Sweep', revolve: 'Revolve', loft: 'Loft', primitive: 'Primitive', prism: 'Push/pull', body: 'Body',
  ring: 'Ring', box: 'Box', wall: 'Wall strip', boxSlab: 'Box bottom or top',
};

/** The selected Studio solids (a change goes to all of them). */
const solidsSelected = (node: NodeDto): string[] => {
  const nodes = useSession.getState().nodes;
  const sel = useUi.getState().selected.filter((id) => nodes.get(id)?.props.operation);
  return sel.includes(node.id) ? sel : [node.id];
};

/** The class's fields a solid shows, by what it is made by (a sweep has no depth, a box no twist). */
export function featureFieldKeys(node: NodeDto): string[] | null {
  const common = ['cutPriority', 'exportAs'];
  switch (node.props.operation) {
    case 'extrude': return ['depth', 'offset', 'symmetric', 'taper', ...common];
    case 'sweep': return ['offset', 'place', 'placeOffset', 'twist', 'scaleEnd', 'rise', 'turns', ...common];
    case 'revolve': return ['offset', 'angle', ...common];
    case 'loft': return ['offset', 'smooth', ...common];
    case 'primitive': {
      const sizes: Record<string, string[]> = {
        box: ['width', 'length', 'height'], wedge: ['width', 'length', 'height'], pyramid: ['width', 'length', 'height'],
        cylinder: ['radius', 'height'], cone: ['radius', 'radius2', 'height'], sphere: ['radius'], torus: ['radius', 'radius2'],
      };
      return ['shape', ...(sizes[String(node.props.shape)] ?? []), 'rotation', ...common];
    }
    case 'prism': return common;
    case 'body': return ['rotation', ...common];
    // Rings, boxes, wall strips and a box's bottom or top (thick.ts).
    case 'ring': case 'box': case 'wall': case 'boxSlab': return solidFieldKeys(node);
    default: return null;
  }
}

/** A number typed in, committed on Enter or blur. */
function Num({ value, onCommit, length, title }: { value: number; onCommit: (n: number) => void; length?: boolean; title?: string }) {
  const shown = length ? formatLength(value) : String(Math.round(value * 1000) / 1000);
  const [text, setText] = useState(shown);
  useEffect(() => setText(shown), [shown]);
  const done = () => { const n = length ? parseLength(text) : Number(text.replace(',', '.')); if (n != null && Number.isFinite(n) && text !== shown) onCommit(n); else setText(shown); };
  return <input className="num" title={title} value={text} onChange={(e) => setText(e.target.value)} onBlur={done} onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); if (e.key === 'Escape') setText(shown); }} />;
}

/** A step's main number: its distance, radius, thickness, count or angle. */
const MAIN: Record<string, { key: string; length: boolean; label: string }> = {
  pushPull: { key: 'distance', length: true, label: 'Distance' },
  chamfer: { key: 'distance', length: true, label: 'Distance' },
  fillet: { key: 'radius', length: true, label: 'Radius' },
  shell: { key: 'thickness', length: true, label: 'Thickness' },
  array: { key: 'count', length: false, label: 'Copies' },
  rotate: { key: 'angle', length: false, label: 'Angle (°)' },
};

function Steps({ node, steps }: { node: NodeDto; steps: FeatureStep[] }) {
  const set = (index: number, patch: Record<string, unknown>) => void execute('studio.updateFeature', { id: node.id, index, set: patch });
  return (
    <div className="studio-steps-block">
      <div className="studio-steps-title" title="What is done to it after its operation, in order: each redone when the sketch changes">Steps</div>
      {!steps.length && <p className="muted small">None yet: push/pull a face, fillet an edge, shell it, split it… in 3D, from the Studio tab.</p>}
      <ol className="studio-steps">
        {steps.map((s, i) => {
          const main = MAIN[s.op];
          const value = main ? Number(s[main.key] ?? 0) : null;
          return (
            <li key={i} className={s.off ? 'off' : ''}>
              <span className="step-head">
                <input type="checkbox" title={s.off ? 'Switched off: switch it on' : 'Switch it off (kept, not done)'} checked={!s.off} onChange={(e) => set(i, { off: e.target.checked ? null : true })} />
                <span className="step-label" title={s.label}>{s.label}</span>
                <button type="button" className="icon mini" title="Earlier" disabled={i === 0} onClick={() => void execute('studio.moveFeature', { id: node.id, index: i, to: i - 1 })}><ArrowUp size={11} /></button>
                <button type="button" className="icon mini" title="Later" disabled={i === steps.length - 1} onClick={() => void execute('studio.moveFeature', { id: node.id, index: i, to: i + 1 })}><ArrowDown size={11} /></button>
                <button type="button" className="icon mini" title="Remove the step" onClick={() => void execute('studio.removeFeature', { id: node.id, index: i })}><Trash2 size={11} /></button>
              </span>
              {(main || s.op === 'shell' || s.op === 'slice') && <span className="step-edit">
              {main && value != null && <label><span className="muted">{main.label}</span> <Num value={value} length={main.length} title={main.label} onCommit={(n) => set(i, { [main.key]: n })} /></label>}
              {s.op === 'shell' && (
                <select className="cell" title="Where it is open" value={String(s.open ?? 'none')} onChange={(e) => set(i, { open: e.target.value })}>
                  <option value="none">closed</option><option value="end">open at its end</option><option value="start">open at its sketch</option><option value="both">open at both</option>
                </select>
              )}
              {s.op === 'slice' && (
                <select className="cell" title="The side kept" value={String(s.keep ?? 'back')} onChange={(e) => set(i, { keep: e.target.value })}>
                  <option value="back">behind</option><option value="front">in front</option>
                </select>
              )}
              </span>}
            </li>
          );
        })}
      </ol>
    </div>
  );
}

/** A primitive's or a body's place: the middle of its base. */
function Place({ node }: { node: NodeDto }) {
  const at = Array.isArray(node.props.at) ? (node.props.at as number[]) : [0, 0, 0];
  const place = (k: number, v: number) => { const next = [...at]; next[k] = v; void execute('studio.place', { id: node.id, at: next }); };
  return (
    <>
      {['x', 'y', 'z'].map((axis, k) => (
        <Fragment key={axis}>
          <dt title="The middle of its base">{k === 0 ? 'Placed at' : ''}</dt>
          <dd className="studio-xyz"><span className="muted">{axis}</span><Num length value={at[k] ?? 0} title={axis} onCommit={(v) => place(k, v)} /><span className="muted">{unitLabel()}</span></dd>
        </Fragment>
      ))}
    </>
  );
}

/** A face pushed or pulled on another element: its distance (pushed in, it cuts). */
function Pushed({ node }: { node: NodeDto }) {
  const d = Number(node.props.depth ?? 0);
  const host = typeof node.props.host === 'string' ? useSession.getState().nodes.get(node.props.host) : undefined;
  return (
    <>
      <dt>{d < 0 ? 'Pushed into' : 'Pulled from'}</dt><dd>{host?.name ?? '—'}</dd>
      <dt>Distance</dt>
      <dd><Num length value={d} onCommit={(n) => { if (Math.abs(n) > 1e-9) void execute('studio.setPush', { id: node.id, distance: n }); }} /> <span className="muted">{unitLabel()} (negative pushes in)</span></dd>
    </>
  );
}

/** A Studio solid in the inspector: its operation, class, type, sketch, steps and what went wrong. */
export function FeatureCard({ node }: { node: NodeDto }) {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const [f, setF] = useState<Feature | null>(null);
  const [classes, setClasses] = useState<Offered[]>([]);
  useEffect(() => { void query<Feature>('studio.feature', { id: node.id }).then(setF); }, [node.id, nodes]);
  useEffect(() => { offered ??= query<Offered[]>('studio.classes'); void offered.then((c) => setClasses(c ?? [])); }, []);
  if (!f) return null;
  const op = OPS[f.operation] ?? f.operation[0].toUpperCase() + f.operation.slice(1);
  const thick = ['ring', 'box', 'wall', 'boxSlab'].includes(f.operation);
  const box = f.operation === 'boxSlab' && typeof node.props.box === 'string' ? nodes.get(node.props.box) : undefined;
  const cls = typeof node.props.exportAs === 'string' ? node.props.exportAs : node.type;
  const pre = typeof node.props.predefinedType === 'string' ? node.props.predefinedType : '';
  const preList = classes.find((c) => c.class === cls)?.predefined ?? [];
  const type = relatingOf({ nodes, relations }, node.id, 'IfcRelDefinesByType');
  const types = [...nodes.values()].filter((n) => n.type === 'LapinSolidType');
  const sketched = ['extrude', 'sweep', 'revolve', 'loft', 'ring', 'box', 'wall'].includes(f.operation);
  const upToFace = () => {
    useUi.setState({ tool: 'studio.upTo', view: '3d' });
    pendingUpTo = node.id;
    notify('Pick, in 3D, the face of another element the extrusion runs up to.');
  };
  return (
    <>
    <dl className="fields">
      <dt>Studio</dt><dd>{op}{node.props.void ? <span className="muted small"> (cuts only, not drawn)</span> : null}</dd>
      <dt title="The IFC class it is written as: quantities, property sets and its cut priority follow it">IFC class</dt>
      <dd><select className="cell" value={cls} onChange={(e) => void execute('studio.setClass', { ids: solidsSelected(node), class: e.target.value })}>
        {!classes.some((c) => c.class === cls) && <option value={cls}>{cls}</option>}
        {classes.map((c) => <option key={c.class} value={c.class}>{c.label}</option>)}
      </select></dd>
      <dt>Predefined type</dt>
      <dd><select className="cell" value={pre} onChange={(e) => {
        let v: string | null = e.target.value || null;
        if (v === '__own') { v = window.prompt('The predefined type (capitals, e.g. SIDEWALK)')?.trim().toUpperCase() || null; if (!v) return; }
        void execute('studio.setClass', { ids: solidsSelected(node), predefinedType: v });
      }}>
        <option value="">—</option>
        {pre && !preList.includes(pre) && <option value={pre}>{pre}</option>}
        {preList.map((p) => <option key={p} value={p}>{p.toLowerCase().replace(/_/g, ' ')}</option>)}
        <option value="__own">other…</option>
      </select></dd>
      <dt>Type</dt>
      <dd><select className="cell" value={type?.id ?? ''} onChange={(e) => {
        const v = e.target.value;
        if (v === '__new') { const name = window.prompt('The new type\'s name'); if (name?.trim()) void execute('studio.setType', { ids: solidsSelected(node), name: name.trim() }); return; }
        void execute('studio.setType', v ? { ids: solidsSelected(node), typeId: v } : { ids: solidsSelected(node) });
      }}>
        <option value="">none</option>
        {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
        <option value="__new">new type…</option>
      </select></dd>
      {box && <><dt>Part of</dt><dd><button type="button" className="link" onClick={() => useUi.setState({ selection: box.id, selected: [box.id] })}>{box.name}</button></dd></>}
      {thick ? <><dt>Sketch</dt><dd>{f.view ?? '—'}</dd></> : sketched && <><dt>Sketch</dt><dd>{f.view ?? '—'} · outline, {f.holes} hole(s)</dd></>}
      {f.area != null && <><dt>Face</dt><dd>{f.area.toFixed(2)} m² net</dd></>}
      {f.volume != null && <><dt>Volume</dt><dd>{f.volume.toFixed(3)} m³</dd></>}
      {f.guide != null && <><dt>{f.operation === 'sweep' ? 'Path' : 'Axis'}</dt><dd className={f.guide === 'missing' ? 'error-text' : ''}>{f.guide === 'missing' ? 'missing: redraw it to rebuild' : f.guide}</dd></>}
      {f.operation === 'extrude' && (
        <>
          <dt title="The extrusion runs to a face of another element, and follows it">Up to</dt>
          <dd>
            {f.upTo ? <span className={f.upTo === 'missing' ? 'error-text' : ''}>{f.upTo === 'missing' ? 'its element is gone' : f.upTo} </span> : <span className="muted">its depth </span>}
            <button type="button" className="link" onClick={upToFace}>a face…</button>
            {f.upTo && <> · <button type="button" className="link" onClick={() => void execute('studio.setUpTo', { id: node.id })}>its depth</button></>}
          </dd>
        </>
      )}
      {(f.operation === 'primitive' || f.operation === 'body') && <Place node={node} />}
      {f.operation === 'prism' && <Pushed node={node} />}
      {f.problems?.length ? <><dt>Problems</dt><dd className="error-text small">{f.problems.join(' ')}</dd></> : null}
      {sketched && <>
        <dt />
        <dd><button type="button" onClick={() => void editSketch(node.id)}><PenTool size={13} /> Edit sketch</button></dd>
        <dd className="muted small">Every step stays editable: change the sketch, its holes or the path, and the solid follows.</dd>
      </>}
    </dl>
    <Steps node={node} steps={f.features ?? []} />
    </>
  );
}

/** The extrusion waiting for the face it runs up to (Up to → a face…). */
export let pendingUpTo: string | null = null;
export const takeUpTo = (): string | null => { const id = pendingUpTo; pendingUpTo = null; return id; };
