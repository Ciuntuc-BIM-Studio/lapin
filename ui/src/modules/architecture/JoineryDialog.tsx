/**
 * Doors and windows: the configurator. The project's door and window types
 * on the left; on the right the one being edited — its size, frame, grid of
 * columns and rows, each panel's operation — with the front elevation as
 * the engine builds it (seen from the interior) and what IFC calls it.
 * Saving is one engine command; every door or window of the type is
 * refitted to its new size.
 */

import { useEffect, useMemo, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { engine, execute, useSession } from '../../engine/session';
import { mm, ofClass, relatedOf } from '../../engine/model';
import { LintelRuleEditor, type Rule } from './LintelRule';

interface Panel { col: number; row: number; operation: string; colSpan?: number; rowSpan?: number; glazed?: boolean }
interface Spec {
  kind: 'door' | 'window'; name: string; mark: string; width: number; height: number; sillHeight: number;
  frame: { depth: number; width: number; mullion: number; transom: number; offset: number };
  sash: { width: number; depth: number; leafThickness: number; glassThickness: number };
  columns: number[]; rows: number[]; panels: Panel[];
  board: { interior: number; exterior: number }; threshold: number;
  colors: { frame: string; glass: string; leaf: string }; isExternal: boolean; thermalTransmittance?: number | null;
  lintel?: Rule | null;
}
interface Preview { problem: string | null; boxes: { role: string; x0: number; x1: number; z0: number; z1: number; y0: number; y1: number }[]; panels: { operation: string; x0: number; x1: number; z0: number; z1: number }[]; ifcType: string }

const ROLE_ORDER: Record<string, number> = { board: 0, frame: 1, leaf: 2, sash: 3, glass: 4, handle: 5 };
const weights = (text: string) => text.split(/[ ,;]+/).map(Number).filter((v) => v > 0);

/** A number field in mm, kept as text while typing. */
function Mm({ label, value, onChange }: { label: string; value: number; onChange: (m: number) => void }) {
  const [text, setText] = useState(mm(value));
  useEffect(() => setText(mm(value)), [value]);
  return (
    <label className="field-line"><span className="eyebrow">{label}</span>
      <input className="num" value={text} onChange={(e) => { setText(e.target.value); const v = Number(e.target.value.replace(',', '.')); if (Number.isFinite(v)) onChange(v / 1000); }} />
    </label>
  );
}

export function JoineryDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const info = useSession((s) => s.info);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = [...ofClass(model, 'IfcDoorType'), ...ofClass(model, 'IfcWindowType')];
  const catalog = info?.catalogs['architecture.joinery'] as unknown as { windowOperations: string[]; doorOperations: string[]; defaultDoor: Spec; defaultWindow: Spec } | undefined;
  const [editing, setEditing] = useState<string | null>(types[0]?.id ?? null);
  const [spec, setSpec] = useState<Spec | null>(null);
  const [cols, setCols] = useState('1');
  const [rows, setRows] = useState('1');
  const [preview, setPreview] = useState<Preview | null>(null);

  // Load the picked type (or a fresh default) into the editor.
  useEffect(() => {
    const t = editing ? nodes.get(editing) : undefined;
    const s = (t?.props.spec as unknown as Spec | undefined) ?? catalog?.defaultWindow ?? null;
    setSpec(s ? structuredClone(s) : null);
    if (s) { setCols(s.columns.join(' ')); setRows(s.rows.join(' ')); }
  }, [editing, catalog]);

  // The engine builds the preview from the spec as it is now.
  useEffect(() => {
    if (!spec) return;
    let live = true;
    void engine.call<Preview>('doc.query', { name: 'architecture.joineryPreview', args: { spec } }).then((p) => { if (live) setPreview(p); }).catch(() => undefined);
    return () => { live = false; };
  }, [spec]);

  if (!spec || !catalog) return null;
  const set = (patch: Partial<Spec>) => setSpec({ ...spec, ...patch });
  const operations = spec.kind === 'door' ? [...catalog.doorOperations, 'fixed'] : catalog.windowOperations;
  const close = () => useUi.setState({ dialog: null });
  const fresh = (kind: 'door' | 'window') => { setEditing(null); const d = structuredClone(kind === 'door' ? catalog.defaultDoor : catalog.defaultWindow); setSpec(d); setCols(d.columns.join(' ')); setRows(d.rows.join(' ')); };
  const setGrid = (c: string, r: string) => {
    setCols(c); setRows(r);
    const cw = weights(c), rw = weights(r);
    if (cw.length && rw.length) set({ columns: cw, rows: rw, panels: spec.panels.filter((p) => p.col < cw.length && p.row < rw.length) });
  };
  const cellOp = (col: number, row: number) => spec.panels.find((p) => p.col === col && p.row === row)?.operation ?? 'fixed';
  const setCell = (col: number, row: number, operation: string) =>
    set({ panels: [...spec.panels.filter((p) => !(p.col === col && p.row === row)), { col, row, operation }] });

  const save = async () => {
    if (editing) await execute('architecture.updateJoineryType', { id: editing, spec });
    else {
      const r = await execute('architecture.createJoineryType', { spec }) as { id?: string } | null;
      if (r?.id) setEditing(r.id);
    }
  };

  // The elevation: X across, Z up, scaled into the box; seen from the interior.
  const W = 300, H = 300, pad = 12;
  const k = Math.min((W - 2 * pad) / spec.width, (H - 2 * pad) / spec.height);
  const ex = (x: number) => W / 2 + x * k;
  const ez = (z: number) => H - pad - z * k;
  const boxes = [...(preview?.boxes ?? [])].sort((a, b) => (ROLE_ORDER[a.role] ?? 9) - (ROLE_ORDER[b.role] ?? 9));
  const fill = (role: string) => role === 'glass' ? spec.colors.glass : role === 'leaf' ? spec.colors.leaf : role === 'handle' ? '#5a5a5a' : role === 'board' ? '#dcdcdc' : spec.colors.frame;

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Doors and windows">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Doors and windows</span>
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
            <li><button type="button" className="tree-item" onClick={() => fresh('door')}><Plus size={12} /> New door</button></li>
            <li><button type="button" className="tree-item" onClick={() => fresh('window')}><Plus size={12} /> New window</button></li>
          </ul>
          <div className="joinery-edit">
            <div className="joinery-form">
              <label className="field-line"><span className="eyebrow">Name</span><input value={spec.name} onChange={(e) => set({ name: e.target.value })} /></label>
              <label className="field-line"><span className="eyebrow">Mark</span><input value={spec.mark} onChange={(e) => set({ mark: e.target.value })} /></label>
              <Mm label="Width" value={spec.width} onChange={(v) => set({ width: v })} />
              <Mm label="Height" value={spec.height} onChange={(v) => set({ height: v })} />
              {spec.kind === 'window' && <Mm label="Sill" value={spec.sillHeight} onChange={(v) => set({ sillHeight: v })} />}
              <Mm label="Frame depth" value={spec.frame.depth} onChange={(v) => set({ frame: { ...spec.frame, depth: v } })} />
              <Mm label="Frame width" value={spec.frame.width} onChange={(v) => set({ frame: { ...spec.frame, width: v } })} />
              <label className="field-line"><span className="eyebrow">Columns</span><input value={cols} title="Relative widths, e.g. 1 1 or 2 1" onChange={(e) => setGrid(e.target.value, rows)} /></label>
              <label className="field-line"><span className="eyebrow">Rows</span><input value={rows} title="Relative heights, bottom to top" onChange={(e) => setGrid(cols, e.target.value)} /></label>
              <p className="eyebrow">Panels · seen from inside</p>
              <table className="panel-grid">
                <tbody>
                  {[...spec.rows.keys()].reverse().map((row) => (
                    <tr key={row}>
                      {spec.columns.map((_, col) => (
                        <td key={col}>
                          <select value={cellOp(col, row)} onChange={(e) => setCell(col, row, e.target.value)}>
                            {operations.map((o) => <option key={o} value={o}>{o}</option>)}
                          </select>
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
              <label className="field-line"><span className="eyebrow">Frame</span><input type="color" value={spec.colors.frame} onChange={(e) => set({ colors: { ...spec.colors, frame: e.target.value } })} /></label>
              <LintelRuleEditor inherit rule={spec.lintel ?? null} onChange={(r) => set({ lintel: r })} />
              {spec.kind === 'door' && <label className="field-line"><span className="eyebrow">Leaf</span><input type="color" value={spec.colors.leaf} onChange={(e) => set({ colors: { ...spec.colors, leaf: e.target.value } })} /></label>}
            </div>
            <div className="joinery-preview">
              <svg viewBox={`0 0 ${W} ${H}`} width={W} height={H} aria-label="Front elevation">
                {boxes.map((b, i) => <rect key={i} x={ex(b.x0)} y={ez(b.z1)} width={(b.x1 - b.x0) * k} height={(b.z1 - b.z0) * k} fill={fill(b.role)} stroke="#1e2a3a" strokeWidth={0.5} opacity={b.role === 'glass' ? 0.7 : 1} />)}
                {(preview?.panels ?? []).filter((p) => p.operation.endsWith('-left') || p.operation.endsWith('-right')).map((p, i) => {
                  // The opening triangle: its apex on the side the handle is, as joinery makers draw it.
                  const hingeLeft = p.operation.endsWith('-left');
                  const apexX = hingeLeft ? ex(p.x1) : ex(p.x0), hx = hingeLeft ? ex(p.x0) : ex(p.x1);
                  return <polyline key={i} points={`${hx},${ez(p.z1)} ${apexX},${ez((p.z0 + p.z1) / 2)} ${hx},${ez(p.z0)}`} fill="none" stroke="#1e2a3a" strokeWidth={0.8} strokeDasharray="4 3" />;
                })}
              </svg>
              <p className="muted small">IFC: {preview?.ifcType ?? '—'}</p>
              {preview?.problem && <p className="error-text">{preview.problem}</p>}
            </div>
            <div className="row joinery-actions">
              <span className="grow" />
              {editing && <button type="button" className="btn" title="Delete the type (refused while elements use it)" onClick={() => void execute('element.delete', { id: editing }).then((r) => { if (r !== null) setEditing(null); })}><Trash2 size={14} />Delete type</button>}
              {editing && <button type="button" className="btn" onClick={() => useUi.setState(spec.kind === 'door' ? { doorTypeId: editing } : { windowTypeId: editing })}>Place with it</button>}
              <button type="button" className="btn primary" disabled={Boolean(preview?.problem)} onClick={() => void save()}>{editing ? 'Save type' : 'Create type'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
