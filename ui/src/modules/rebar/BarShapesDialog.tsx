/**
 * Bar shapes… (docs/plan-user-extensibility.md §6.3): the shape codes of the user's — BS 8666, ISO 4066, the office's —, each its
 * code, parameters (a key, a label, a default and a least) and legs (a length, a parameter's key or a formula, and a turn in
 * degrees counter-clockwise to the next leg), with the engine's own drawing of it as it is edited. A bar made of a shape keeps its
 * definition and follows its changes. A definition the engine cannot read is refused with the reason, shown. .lapinbarshapes files in and out.
 */

import { useEffect, useRef, useState } from 'react';
import { FileDown, FileUp, Plus, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { engine, useSession } from '../../engine/session';
import { pickFile, safeFileName, saveFile } from '../../engine/files';
import { LengthField } from '../../shell/cells';
import { FirmFolderFiles } from '../core/FirmFolderFiles';
import { PendingDialog } from '../core/PendingDialog';
import { createShape, deleteShape, exportShapes, importShapes, updateShape, useShapeLibrary } from './barShapes';
import { ShapePreview } from './ShapePreview';
import { addLeg, addParam, cleanDef, newShape, removeLeg, removeParam, setLeg, setParam, shapeProblem, type ShapeDef, type UserShape } from './shapeModel';

/** The engine's drawing of a definition, or why it refuses it (asked quietly: this runs as the user types). */
async function previewOf(def: ShapeDef): Promise<{ corners?: number[][]; error?: string }> {
  try { return await engine.call<{ corners: number[][] }>('doc.query', { name: 'rebar.shapePreview', args: { def: cleanDef(def) } }); }
  catch (e) { return { error: e instanceof Error ? e.message : String(e) }; }
}

export function BarShapesDialog() {
  const lib = useShapeLibrary();
  const [selected, setSelected] = useState<string | 'new' | null>(null);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => { if (lib && selected == null) setSelected(lib.shapes[0]?.id ?? null); }, [lib, selected]);
  if (!lib) return <PendingDialog title="Bar shapes" />;
  const current = lib.shapes.find((s) => s.id === selected) ?? null;

  const exportAll = async () => {
    const r = await exportShapes();
    if (!r) return;
    const saved = await saveFile(new TextEncoder().encode(r.text), safeFileName(r.file, 'shapes.lapinbarshapes'), 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };
  const importFile = async (given?: { bytes: Uint8Array }) => {
    const f = given ?? await pickFile('Bar shapes (.lapinbarshapes)', 'lapinbarshapes');
    if (!f) return;
    const r = await importShapes(new TextDecoder().decode(f.bytes), true);
    if (r) notify(`${r.added} shapes added, ${r.updated} updated.`);
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide templates bar-shapes-dialog" role="dialog" aria-label="Bar shapes" data-dialog="barShapes" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Bar shapes</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body tpl-body">
          <aside className="tpl-list">
            <div className="row wrap">
              <button type="button" className="btn" data-field="shape-new" onClick={() => setSelected('new')}><Plus size={13} />New</button>
              <button type="button" className="btn" data-field="shape-import" title="A .lapinbarshapes file: shapes the project lacks are added, those with the same code are updated" onClick={() => void importFile()}><FileUp size={13} />Import…</button>
              <button type="button" className="btn" data-field="shape-export" disabled={lib.shapes.length === 0} onClick={() => void exportAll()}><FileDown size={13} />Export…</button>
            </div>
            <h4 className="eyebrow">This project's</h4>
            {lib.shapes.length === 0 && <p className="muted small">None of your own yet. Make one: its code as your standard names it, its parameters and its legs.</p>}
            <ul className="doc-templates">
              {lib.shapes.map((s) => (
                <li key={s.id} className={s.id === selected ? 'active' : undefined}>
                  <button type="button" data-shape={s.def.code} onClick={() => setSelected(s.id)}><span className="sy-item-text"><strong data-no-i18n>{s.name}</strong>{s.uses > 0 && <span className="muted small">{`${s.uses} bars`}</span>}</span></button>
                </li>
              ))}
            </ul>
            <h4 className="eyebrow">Lapin's</h4>
            <ul className="doc-templates">{lib.builtin.map((b) => <li key={b.code}><span className="small" data-no-i18n>{b.label}</span></li>)}</ul>
            <FirmFolderFiles ext="lapinbarshapes" onPick={(f) => importFile(f)}
              save={{ disabled: lib.shapes.length === 0, get: async () => { const r = await exportShapes(); return r ? { text: r.text, name: r.file.replace(/\.[^.]+$/, '') } : null; } }} />
          </aside>
          <main className="tpl-detail">
            {selected === 'new' || current
              ? <ShapeEditor key={selected === 'new' ? 'new' : current!.id} shape={selected === 'new' ? null : current} onSaved={(id) => setSelected(id)} onGone={() => setSelected(null)} />
              : <p className="muted">Choose a shape, or make one.</p>}
          </main>
        </div>
      </div>
    </div>
  );
}

function ShapeEditor({ shape, onSaved, onGone }: { shape: UserShape | null; onSaved: (id: string) => void; onGone: () => void }) {
  const [def, setDef] = useState<ShapeDef>(() => shape?.def ?? newShape());
  const [corners, setCorners] = useState<number[][]>([]);
  const [refusal, setRefusal] = useState<string | null>(null);
  const seq = useRef(0);
  const local = shapeProblem(def);
  useEffect(() => {
    if (local) { setRefusal(local); return; }
    const mine = ++seq.current;
    const t = setTimeout(() => { void previewOf(def).then((r) => { if (mine !== seq.current) return; if (r.corners) { setCorners(r.corners); setRefusal(null); } else setRefusal(r.error ?? null); }); }, 200);
    return () => clearTimeout(t);
  }, [def, local]);

  const save = async () => {
    if (local) return;
    const clean = cleanDef(def);
    if (shape) { await updateShape(shape.id, clean); }
    else { const r = await createShape(clean); if (r) onSaved(r.id); }
    const err = useSession.getState().error;
    if (!err) notify('Bar shape saved.');
  };
  const remove = async () => {
    if (!shape) return;
    if (shape.uses > 0 && !window.confirm(`${shape.uses} bars are of this shape: they keep their own copy of it.`)) return;
    await deleteShape(shape.id);
    onGone();
  };
  const set = (patch: Partial<ShapeDef>) => setDef({ ...def, ...patch });
  const hook = (end: 'start' | 'end') => def.hooks?.[end]?.angle ?? 0;
  const setHook = (end: 'start' | 'end', angle: number) => {
    const hooks = { ...(def.hooks ?? {}) } as NonNullable<ShapeDef['hooks']>;
    if (angle > 0) hooks[end] = { angle }; else delete hooks[end];
    set({ hooks });
  };

  return (
    <div className="shape-editor" data-testid="shape-editor">
      <div className="row wrap">
        <label className="field-line"><span className="eyebrow">Code</span><input className="num" data-field="shape-code" value={def.code} onChange={(e) => set({ code: e.target.value })} style={{ width: 72 }} /></label>
        <label className="field-line grow"><span className="eyebrow">Name</span><input data-field="shape-label" value={def.label} onChange={(e) => set({ label: e.target.value })} /></label>
        <label className="field-line"><span className="eyebrow">Standard</span><input data-field="shape-standard" value={def.standard ?? ''} placeholder="BS 8666" onChange={(e) => set({ standard: e.target.value })} style={{ width: 110 }} /></label>
      </div>
      <div className="shape-editor-body" style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
        <div className="grow">
          <h4 className="eyebrow">Parameters</h4>
          <table className="layer-table" data-testid="shape-params">
            <thead><tr><th>Key</th><th>Label</th><th>Default</th><th>Least</th><th /></tr></thead>
            <tbody>
              {def.params.map((p, i) => (
                <tr key={i}>
                  <td><input className="num" style={{ width: 48 }} data-shape-param-key={i} defaultValue={p.key} key={`${i}${p.key}`} onBlur={(e) => { if (e.target.value !== p.key) setDef(setParam(def, i, { key: e.target.value.trim() })); }} /></td>
                  <td><input style={{ width: 80 }} defaultValue={p.label} key={`${i}l${p.label}`} onBlur={(e) => { if (e.target.value !== p.label) setDef(setParam(def, i, { label: e.target.value })); }} /></td>
                  <td><LengthField metres={p.default} min={0} onCommit={(m) => { if (m != null) setDef(setParam(def, i, { default: m })); }} /></td>
                  <td><LengthField metres={p.min ?? 0} min={0} onCommit={(m) => { if (m != null) setDef(setParam(def, i, { min: m })); }} /></td>
                  <td><button type="button" className="icon mini" title="Remove the parameter" onClick={() => setDef(removeParam(def, i))}><X size={11} /></button></td>
                </tr>
              ))}
            </tbody>
          </table>
          <button type="button" className="btn small" data-field="shape-add-param" onClick={() => setDef(addParam(def))}><Plus size={12} />Parameter</button>
          <h4 className="eyebrow">Legs</h4>
          <table className="layer-table" data-testid="shape-legs">
            <thead><tr><th>#</th><th title="A parameter's key, or a formula (A - 2 * R1)">Length</th><th title="Degrees, counter-clockwise, from this leg to the next (none after the last); a key or a formula too">Turn °</th><th /></tr></thead>
            <tbody>
              {def.legs.map((l, i) => (
                <tr key={i}>
                  <td className="muted small">{i + 1}</td>
                  <td><input className="mono" data-shape-leg-length={i} value={l.length} onChange={(e) => setDef(setLeg(def, i, { length: e.target.value }))} /></td>
                  <td>{i < def.legs.length - 1 ? <input className="mono num" style={{ width: 60 }} data-shape-leg-turn={i} value={l.turn ?? '0'} onChange={(e) => setDef(setLeg(def, i, { turn: e.target.value }))} /> : <span className="muted small">last</span>}</td>
                  <td>{def.legs.length > 1 && <button type="button" className="icon mini" title="Remove the leg" onClick={() => setDef(removeLeg(def, i))}><X size={11} /></button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <button type="button" className="btn small" data-field="shape-add-leg" onClick={() => setDef(addLeg(def))}><Plus size={12} />Leg</button>
          <div className="row">
            {(['start', 'end'] as const).map((end) => (
              <label key={end} className="field-line"><span className="eyebrow">{end === 'start' ? 'Hook at the start' : 'Hook at the end'}</span>
                <select value={hook(end)} onChange={(e) => setHook(end, Number(e.target.value))}>
                  <option value={0}>None</option><option value={90}>90°</option><option value={135}>135°</option><option value={180}>180°</option>
                </select>
              </label>
            ))}
          </div>
        </div>
        <div>
          <h4 className="eyebrow">Preview</h4>
          <ShapePreview corners={refusal ? [] : corners} legs={def.legs} />
          {refusal && <p className="error-text small" role="alert" data-testid="shape-refusal" data-no-i18n>{refusal}</p>}
        </div>
      </div>
      <div className="row">
        <button type="button" className="btn primary" data-field="shape-save" disabled={!!refusal} onClick={() => void save()}>{shape ? 'Save' : 'Add to the project'}</button>
        {shape && <button type="button" className="btn danger" data-field="shape-delete" onClick={() => void remove()}><Trash2 size={13} />Delete</button>}
      </div>
      <p className="muted small">Lengths are in the project's unit. A leg's length is a parameter's key (A) or a formula (A - 2 * R1); the turn is in degrees, counter-clockwise. Bars of the shape follow a change to it.</p>
    </div>
  );
}
