/**
 * Doors and windows: the configurator. The project's door and window types
 * on the left; on the right the one being edited — its size, frame, grid of
 * columns and rows, each panel's operation — with its drawings as the
 * engine's symbol engine makes them (the elevation from inside or outside,
 * the plan, the section), the project's symbol convention, and what IFC
 * calls it.
 * Saving is one engine command; every door or window of the type is
 * refitted to its new size.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { engine, execute, useSession } from '../../engine/session';
import { mm, ofClass, relatedOf } from '../../engine/model';
import { LintelRuleEditor } from './LintelRule';
import type { Rule } from './lintels';

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
interface Stroke { pts: [number, number][]; weight: string; closed: boolean; dashed: boolean }
interface Drawing { strokes: Stroke[] }
interface Drawings { elevation: Drawing; exterior: Drawing; plan: Drawing; section: Drawing }
type Preview2 = Preview & { drawings?: Drawings | null; symbols?: { convention: string; hardware: boolean } };

/** One of the symbol engine's drawings in a box: X across, Y up, fitted; strokes by weight (cut, outline, thin), dashed where asked. */
function Strokes({ drawing, w, h, flipX = false, label, children }: { drawing: Drawing | undefined; w: number; h: number; flipX?: boolean; label: string; children?: (sx: (x: number) => number, sy: (y: number) => number, k: number) => React.ReactNode }) {
  const pts = (drawing?.strokes ?? []).flatMap((s) => s.pts);
  if (pts.length === 0) return <svg viewBox={`0 0 ${w} ${h}`} width={w} height={h} aria-label={label} />;
  const xs = pts.map((p) => (flipX ? -p[0] : p[0])), ys = pts.map((p) => p[1]);
  const [x0, x1, y0, y1] = [Math.min(...xs), Math.max(...xs), Math.min(...ys), Math.max(...ys)];
  const pad = 10, k = Math.min((w - 2 * pad) / Math.max(x1 - x0, 1e-6), (h - 2 * pad) / Math.max(y1 - y0, 1e-6));
  const sx = (x: number) => (w - (x1 - x0) * k) / 2 + ((flipX ? -x : x) - x0) * k;
  const sy = (y: number) => h - (h - (y1 - y0) * k) / 2 - (y - y0) * k;
  const width = (wt: string) => wt === 'cut' ? 1.6 : wt === 'outline' ? 0.9 : 0.5;
  return (
    <svg viewBox={`0 0 ${w} ${h}`} width={w} height={h} aria-label={label}>
      {children?.(sx, sy, k)}
      {(drawing?.strokes ?? []).map((s, i) => (
        <polyline key={i} points={s.pts.map((p) => `${sx(p[0]).toFixed(1)},${sy(p[1]).toFixed(1)}`).join(' ') + (s.closed ? ` ${sx(s.pts[0][0]).toFixed(1)},${sy(s.pts[0][1]).toFixed(1)}` : '')}
          fill="none" stroke="#1e2a3a" strokeWidth={width(s.weight)} strokeDasharray={s.dashed ? '4 3' : undefined} strokeLinejoin="round" />
      ))}
    </svg>
  );
}

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
  const [preview, setPreview] = useState<Preview2 | null>(null);
  const [outside, setOutside] = useState(false);

  // The editor loads when the picked type changes, not on every diff (which
  // would throw away what is being typed): it reads the model through a ref.
  const modelRef = useRef(model);
  modelRef.current = model;
  // Load the picked type (or a fresh default) into the editor.
  useEffect(() => {
    const t = editing ? modelRef.current.nodes.get(editing) : undefined;
    const s = (t?.props.spec as unknown as Spec | undefined) ?? catalog?.defaultWindow ?? null;
    setSpec(s ? structuredClone(s) : null);
    if (s) { setCols(s.columns.join(' ')); setRows(s.rows.join(' ')); }
  }, [editing, catalog]);

  // The engine builds the preview from the spec as it is now.
  useEffect(() => {
    if (!spec) return;
    let live = true;
    void engine.call<Preview2>('doc.query', { name: 'architecture.joineryPreview', args: { spec } }).then((p) => { if (live) setPreview(p); }).catch(() => undefined);
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

  // The elevation's box; the strokes are fitted into it.
  const W = 300, H = 300;
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
              <Strokes drawing={outside ? preview?.drawings?.exterior : preview?.drawings?.elevation} w={W} h={H} label={outside ? 'Elevation from outside' : 'Elevation from inside'}>
                {(sx, sy, kk) => !outside && boxes.map((b, i) => <rect key={i} x={sx(b.x0)} y={sy(b.z1)} width={(b.x1 - b.x0) * kk} height={(b.z1 - b.z0) * kk} fill={fill(b.role)} stroke="none" opacity={b.role === 'glass' ? 0.7 : 1} />)}
              </Strokes>
              <div className="joinery-small">
                <figure><Strokes drawing={preview?.drawings?.plan} w={150} h={90} label="Plan" /><figcaption className="muted small">Plan</figcaption></figure>
                <figure><Strokes drawing={preview?.drawings?.section} w={90} h={150} label="Section" /><figcaption className="muted small">Section</figcaption></figure>
              </div>
              <div className="row joinery-symbols">
                <label className="small"><input type="checkbox" checked={outside} onChange={(e) => setOutside(e.target.checked)} /> From outside</label>
                <select className="mini-select" title="Opening symbols in elevation" value={preview?.symbols?.convention ?? 'handle'} onChange={(e) => void execute('architecture.joinerySymbols', { convention: e.target.value }).then(() => setSpec({ ...spec }))}>
                  <option value="handle">Apex at the handle (makers)</option><option value="hinge">Apex at the hinges (DIN 1356)</option>
                </select>
                <label className="small"><input type="checkbox" checked={preview?.symbols?.hardware ?? false} onChange={(e) => void execute('architecture.joinerySymbols', { hardware: e.target.checked }).then(() => setSpec({ ...spec }))} /> Hardware</label>
              </div>
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
