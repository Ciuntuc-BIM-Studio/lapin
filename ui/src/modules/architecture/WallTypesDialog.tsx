/**
 * Wall types: the project's IfcWallTypes and their layer sets. Pick one to
 * edit its name and layers (material, thickness, the core), or make a new
 * one. Layers are listed from the face right of the drawn direction to the
 * left one; the core carries the axis. Saving is one engine command; every
 * wall of the type follows.
 */

import { useEffect, useMemo, useState } from 'react';
import { ArrowDown, ArrowUp, Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { materialName, mm, ofClass, relatedOf, typeLayers } from '../../engine/model';

interface Row { material: string; thicknessMm: string; core: boolean }

const blank = (): Row[] => [{ material: 'Render', thicknessMm: '20', core: false }, { material: 'Brick', thicknessMm: '250', core: true }, { material: 'Plaster', thicknessMm: '15', core: false }];

export function WallTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'IfcWallType');
  const materials = ofClass(model, 'IfcMaterial');
  const current = useUi((s) => s.wallTypeId);
  const [editing, setEditing] = useState<string | null>(current ?? types[0]?.id ?? null);
  const [name, setName] = useState('');
  const [rows, setRows] = useState<Row[]>(blank());

  // Load the picked type into the editor.
  useEffect(() => {
    const t = editing ? nodes.get(editing) : undefined;
    if (!t) { setName(''); setRows(blank()); return; }
    setName(t.name);
    setRows(typeLayers(model, t.id).map((l) => ({ material: materialName(model, l.material), thicknessMm: mm(l.thickness), core: l.core === true })));
  }, [editing]);

  const close = () => useUi.setState({ dialog: null });
  const set = (i: number, patch: Partial<Row>) => setRows(rows.map((r, k) => (k === i ? { ...r, ...patch } : patch.core ? { ...r, core: false } : r)));
  const move = (i: number, d: number) => {
    const j = i + d;
    if (j < 0 || j >= rows.length) return;
    const next = [...rows];
    [next[i], next[j]] = [next[j], next[i]];
    setRows(next);
  };
  const total = rows.reduce((s, r) => s + (Number(r.thicknessMm.replace(',', '.')) || 0), 0);
  const layers = rows.map((r) => ({ material: r.material.trim(), thickness: Number(r.thicknessMm.replace(',', '.')) / 1000, core: r.core }));

  const save = async () => {
    if (editing) await execute('architecture.updateWallType', { id: editing, name, layers });
    else {
      const r = await execute('architecture.createWallType', { name, layers }) as { id?: string } | null;
      if (r?.id) { setEditing(r.id); useUi.setState({ wallTypeId: r.id }); }
    }
  };
  const use = (id: string) => { useUi.setState({ wallTypeId: id }); };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Wall types">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Wall types</span>
          <button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <ul className="type-list">
            {types.map((t) => (
              <li key={t.id}>
                <button type="button" className={`tree-item${editing === t.id ? ' on' : ''}`} onClick={() => setEditing(t.id)}>
                  {t.name}<span className="muted small"> · {relatedOf(model, t.id, 'IfcRelDefinesByType').length} walls{current === t.id ? ' · drawing' : ''}</span>
                </button>
              </li>
            ))}
            <li><button type="button" className={`tree-item${editing === null ? ' on' : ''}`} onClick={() => setEditing(null)}><Plus size={12} /> New type</button></li>
          </ul>
          <div className="type-edit">
            <label className="field-line"><span className="eyebrow">Name</span><input value={name} placeholder="(from its core and thickness)" onChange={(e) => setName(e.target.value)} /></label>
            <p className="muted small">Layers from the face right of the drawn direction to the left face. The core carries the wall's axis.</p>
            <table className="layer-edit">
              <thead><tr><th>Material</th><th>mm</th><th>Core</th><th /></tr></thead>
              <tbody>
                {rows.map((r, i) => (
                  <tr key={i}>
                    <td><input list="lapin-materials" value={r.material} onChange={(e) => set(i, { material: e.target.value })} /></td>
                    <td><input className="num" value={r.thicknessMm} onChange={(e) => set(i, { thicknessMm: e.target.value })} /></td>
                    <td><input type="radio" name="core" checked={r.core} onChange={() => set(i, { core: true })} /></td>
                    <td className="row-tools">
                      <button type="button" className="icon mini" title="Up" onClick={() => move(i, -1)}><ArrowUp size={12} /></button>
                      <button type="button" className="icon mini" title="Down" onClick={() => move(i, 1)}><ArrowDown size={12} /></button>
                      <button type="button" className="icon mini" title="Remove" onClick={() => setRows(rows.filter((_, k) => k !== i))}><Trash2 size={12} /></button>
                    </td>
                  </tr>
                ))}
                <tr className="total"><td>Total</td><td className="num">{Math.round(total)}</td><td colSpan={2} /></tr>
              </tbody>
            </table>
            <datalist id="lapin-materials">{materials.map((m) => <option key={m.id} value={m.name} />)}</datalist>
            <div className="row">
              <button type="button" className="btn" onClick={() => setRows([...rows, { material: 'Plaster', thicknessMm: '15', core: false }])}><Plus size={14} />Layer</button>
              <span className="grow" />
              {editing && <button type="button" className="btn" onClick={() => use(editing)}>Draw with it</button>}
              <button type="button" className="btn primary" onClick={() => void save()}>{editing ? 'Save type' : 'Create type'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
