/**
 * Curtain walls: the configurator. The project's curtain wall types on the
 * left; on the right the one being edited — how its grid divides it along
 * (U) and up (V), its mullions and transoms, its panels (glass or opaque),
 * their colours — with the elevation the engine lays out for a wall of the
 * length and height chosen. Saving is one engine command; every curtain wall
 * of the type is laid out again.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { engine, execute, useSession } from '../../engine/session';
import { mm, ofClass, relatedOf } from '../../engine/model';

interface Grid { mode: 'count' | 'spacing' | 'max' | 'offsets'; count?: number; spacing?: number; justify?: 'start' | 'center' | 'end'; offsets?: number[] }
interface Section { width: number; depth: number }
export interface CurtainSpec {
  u: Grid; v: Grid; mullion: Section; transom: Section; edge: boolean;
  panel: { thickness: number; kind: 'glass' | 'opaque' };
  frameColor: string; glassColor: string; opaqueColor: string;
}
interface Preview { problem: string | null; u?: number[]; v?: number[]; pieces?: { role: string; x0: number; x1: number; z0: number; z1: number }[] }

const MODES: [Grid['mode'], string][] = [['max', 'Bays no wider than'], ['count', 'Equal bays'], ['spacing', 'Fixed spacing'], ['offsets', 'At offsets']];

/** A number field in mm, kept as text while typing. */
function Mm({ label, value, onChange }: { label: string; value: number; onChange: (m: number) => void }) {
  const [text, setText] = useState(mm(value));
  useEffect(() => setText(mm(value)), [value]);
  return (
    <label className="field-line"><span className="eyebrow">{label}</span>
      <input className="num" value={text} onChange={(e) => { setText(e.target.value); const v = Number(e.target.value.replace(',', '.')); if (Number.isFinite(v) && v > 0) onChange(v / 1000); }} />
    </label>
  );
}

/** One direction of the grid: how it divides, and by how much. */
function GridFields({ label, grid, onChange }: { label: string; grid: Grid; onChange: (g: Grid) => void }) {
  const [offsets, setOffsets] = useState((grid.offsets ?? []).map(mm).join(' '));
  useEffect(() => setOffsets((grid.offsets ?? []).map(mm).join(' ')), [grid.offsets]);
  return (
    <>
      <label className="field-line"><span className="eyebrow">{label}</span>
        <select value={grid.mode} onChange={(e) => onChange({ ...grid, mode: e.target.value as Grid['mode'], count: grid.count ?? 3, spacing: grid.spacing ?? 1.5, justify: grid.justify ?? 'center' })}>
          {MODES.map(([m, l]) => <option key={m} value={m}>{l}</option>)}
        </select>
      </label>
      {grid.mode === 'count' && (
        <label className="field-line"><span className="eyebrow">Bays</span>
          <input className="num" type="number" min={1} max={200} value={grid.count ?? 1} onChange={(e) => onChange({ ...grid, count: Math.max(1, Math.round(Number(e.target.value) || 1)) })} />
        </label>
      )}
      {(grid.mode === 'spacing' || grid.mode === 'max') && <Mm label="Spacing" value={grid.spacing ?? 1.5} onChange={(v) => onChange({ ...grid, spacing: v })} />}
      {grid.mode === 'spacing' && (
        <label className="field-line"><span className="eyebrow">The rest</span>
          <select value={grid.justify ?? 'center'} onChange={(e) => onChange({ ...grid, justify: e.target.value as Grid['justify'] })}>
            <option value="center">split at both ends</option><option value="start">at the start</option><option value="end">at the end</option>
          </select>
        </label>
      )}
      {grid.mode === 'offsets' && (
        <label className="field-line"><span className="eyebrow">Offsets (mm)</span>
          <input value={offsets} title="Distances from the start (or the base), e.g. 1200 2400 3000"
            onChange={(e) => { setOffsets(e.target.value); onChange({ ...grid, offsets: e.target.value.split(/[ ,;]+/).map(Number).filter((x) => x > 0).map((x) => x / 1000) }); }} />
        </label>
      )}
    </>
  );
}

export function CurtainWallDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'IfcCurtainWallType');
  const fallback = info?.catalogs['architecture.curtainWallDefault'] as unknown as CurtainSpec | undefined;
  const [editing, setEditing] = useState<string | null>(useUi.getState().curtainTypeId ?? types[0]?.id ?? null);
  const [name, setName] = useState('');
  const [spec, setSpec] = useState<CurtainSpec | null>(null);
  const [size, setSize] = useState({ length: 6, height: 3 });
  const [preview, setPreview] = useState<Preview | null>(null);

  // Loaded when the picked type changes, not on every diff (that would throw away what is being typed).
  const modelRef = useRef(model);
  modelRef.current = model;
  useEffect(() => {
    const t = editing ? modelRef.current.nodes.get(editing) : undefined;
    const s = (t?.props.spec as unknown as CurtainSpec | undefined) ?? fallback ?? null;
    setSpec(s ? structuredClone(s) : null);
    setName(t?.name ?? 'Curtain wall');
  }, [editing, fallback]);

  useEffect(() => {
    if (!spec) return;
    let live = true;
    void engine.call<Preview>('doc.query', { name: 'architecture.curtainWallPreview', args: { spec, ...size } }).then((p) => { if (live) setPreview(p); }).catch(() => undefined);
    return () => { live = false; };
  }, [spec, size]);

  if (!spec) return null;
  const set = (patch: Partial<CurtainSpec>) => setSpec({ ...spec, ...patch });
  const close = () => useUi.setState({ dialog: null });
  const fresh = () => { setEditing(null); setSpec(structuredClone(fallback ?? spec)); setName(`Curtain wall ${types.length + 1}`); };
  const save = async () => {
    if (editing) await execute('architecture.updateCurtainWallType', { id: editing, name, spec });
    else {
      const r = await execute('architecture.createCurtainWallType', { name, spec }) as { id?: string } | null;
      if (r?.id) setEditing(r.id);
    }
  };

  // The elevation: along the wall to the right, up; scaled into the box.
  const W = 360, H = 260, pad = 12;
  const k = Math.min((W - 2 * pad) / size.length, (H - 2 * pad) / size.height);
  const ex = (x: number) => pad + x * k;
  const ez = (z: number) => H - pad - z * k;
  const infill = spec.panel.kind === 'glass' ? spec.glassColor : spec.opaqueColor;
  const pieces = [...(preview?.pieces ?? [])].sort((a, b) => (a.role === 'panel' ? 0 : 1) - (b.role === 'panel' ? 0 : 1));

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Curtain walls">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Curtain walls</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <ul className="type-list">
            {types.map((t) => (
              <li key={t.id}>
                <button type="button" className={`tree-item${editing === t.id ? ' on' : ''}`} onClick={() => setEditing(t.id)}>
                  {t.name}<span className="muted small"> · {relatedOf(model, t.id, 'IfcRelDefinesByType').length}</span>
                </button>
              </li>
            ))}
            <li><button type="button" className="tree-item" onClick={fresh}><Plus size={12} /> New curtain wall type</button></li>
          </ul>
          <div className="joinery-edit">
            <div className="joinery-form">
              <label className="field-line"><span className="eyebrow">Name</span><input value={name} onChange={(e) => setName(e.target.value)} /></label>
              <GridFields label="Along (mullions)" grid={spec.u} onChange={(u) => set({ u })} />
              <GridFields label="Up (transoms)" grid={spec.v} onChange={(v) => set({ v })} />
              <Mm label="Mullion width" value={spec.mullion.width} onChange={(v) => set({ mullion: { ...spec.mullion, width: v } })} />
              <Mm label="Mullion depth" value={spec.mullion.depth} onChange={(v) => set({ mullion: { ...spec.mullion, depth: v } })} />
              <Mm label="Transom width" value={spec.transom.width} onChange={(v) => set({ transom: { ...spec.transom, width: v } })} />
              <Mm label="Transom depth" value={spec.transom.depth} onChange={(v) => set({ transom: { ...spec.transom, depth: v } })} />
              <label className="field-line"><span className="eyebrow">Edge members</span><input type="checkbox" checked={spec.edge} onChange={(e) => set({ edge: e.target.checked })} /></label>
              <label className="field-line"><span className="eyebrow">Panels</span>
                <select value={spec.panel.kind} onChange={(e) => set({ panel: { ...spec.panel, kind: e.target.value as 'glass' | 'opaque' } })}>
                  <option value="glass">glass</option><option value="opaque">opaque</option>
                </select>
              </label>
              <Mm label="Panel thickness" value={spec.panel.thickness} onChange={(v) => set({ panel: { ...spec.panel, thickness: v } })} />
              <label className="field-line"><span className="eyebrow">Frame</span><input type="color" value={spec.frameColor} onChange={(e) => set({ frameColor: e.target.value })} /></label>
              <label className="field-line"><span className="eyebrow">{spec.panel.kind === 'glass' ? 'Glass' : 'Panel'}</span>
                <input type="color" value={infill} onChange={(e) => set(spec.panel.kind === 'glass' ? { glassColor: e.target.value } : { opaqueColor: e.target.value })} />
              </label>
            </div>
            <div className="joinery-preview">
              <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} aria-label="Elevation">
                {pieces.map((p, i) => (
                  <rect key={i} x={ex(p.x0)} y={ez(p.z1)} width={Math.max(0.5, (p.x1 - p.x0) * k)} height={Math.max(0.5, (p.z1 - p.z0) * k)}
                    fill={p.role === 'panel' ? infill : spec.frameColor} opacity={p.role === 'panel' && spec.panel.kind === 'glass' ? 0.6 : 1} stroke="#1e2a3a" strokeWidth={0.3} />
                ))}
              </svg>
              <div className="row">
                <Mm label="Preview length" value={size.length} onChange={(v) => setSize({ ...size, length: v })} />
                <Mm label="height" value={size.height} onChange={(v) => setSize({ ...size, height: v })} />
              </div>
              <p className="muted small">{(preview?.u?.length ?? 1) - 1} bays along × {(preview?.v?.length ?? 1) - 1} up · IFC: IfcCurtainWall over IfcMember and IfcPlate</p>
              {preview?.problem && <p className="error-text">{preview.problem}</p>}
            </div>
            <div className="row joinery-actions">
              <span className="grow" />
              {editing && <button type="button" className="btn" title="Delete the type (refused while curtain walls use it)" onClick={() => void execute('element.delete', { id: editing }).then((r) => { if (r !== null) setEditing(null); })}><Trash2 size={14} />Delete type</button>}
              {editing && <button type="button" className="btn" onClick={() => useUi.setState({ curtainTypeId: editing })}>Draw with it</button>}
              <button type="button" className="btn primary" disabled={Boolean(preview?.problem)} onClick={() => void save()}>{editing ? 'Save type' : 'Create type'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
