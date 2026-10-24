/** Floor types: the project's IfcSlabTypes and their layers, top to bottom. Saving is one engine command; every floor of the type follows. */

import { useEffect, useMemo, useRef, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { materialName, mm, ofClass, relatedOf, typeLayers } from '../../engine/model';

interface Row { material: string; thicknessMm: string; core: boolean }
const blank = (): Row[] => [{ material: 'Floor finish', thicknessMm: '20', core: false }, { material: 'Screed', thicknessMm: '50', core: false }, { material: 'Reinforced concrete', thicknessMm: '150', core: true }];

export function SlabTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'IfcSlabType');
  const [editing, setEditing] = useState<string | null>(types[0]?.id ?? null);
  const [name, setName] = useState('');
  const [rows, setRows] = useState<Row[]>(blank());
  // The editor loads when the picked type changes, not on every diff (which
  // would throw away what is being typed): it reads the model through a ref.
  const modelRef = useRef(model);
  modelRef.current = model;
  useEffect(() => {
    const model = modelRef.current;
    const t = editing ? model.nodes.get(editing) : undefined;
    if (!t) { setName(''); setRows(blank()); return; }
    setName(t.name);
    setRows(typeLayers(model, t.id).map((l) => ({ material: materialName(model, l.material), thicknessMm: mm(l.thickness), core: l.core === true })));
  }, [editing]);
  const set = (i: number, patch: Partial<Row>) => setRows(rows.map((r, k) => (k === i ? { ...r, ...patch } : patch.core ? { ...r, core: false } : r)));
  const layers = rows.map((r) => ({ material: r.material.trim(), thickness: Number(r.thicknessMm.replace(',', '.')) / 1000, core: r.core }));
  const save = async () => {
    if (editing) await execute('architecture.updateSlabType', { id: editing, name, layers });
    else {
      const r = await execute('architecture.createSlabType', { name, layers }) as { id?: string } | null;
      if (r?.id) { setEditing(r.id); useUi.setState({ slabTypeId: r.id }); }
    }
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Floor types">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Floor types</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <ul className="type-list">
            {types.map((t) => (
              <li key={t.id}><button type="button" className={`tree-item${editing === t.id ? ' on' : ''}`} onClick={() => setEditing(t.id)}>{t.name}<span className="muted small"> · {relatedOf(model, t.id, 'IfcRelDefinesByType').length}</span></button></li>
            ))}
            <li><button type="button" className={`tree-item${editing === null ? ' on' : ''}`} onClick={() => setEditing(null)}><Plus size={12} /> New type</button></li>
          </ul>
          <div className="type-edit">
            <label className="field-line"><span className="eyebrow">Name</span><input value={name} placeholder="(from its core and thickness)" onChange={(e) => setName(e.target.value)} /></label>
            <p className="muted small">Layers from the top down.</p>
            <table className="layer-edit">
              <thead><tr><th>Material</th><th>mm</th><th>Core</th><th /></tr></thead>
              <tbody>
                {rows.map((r, i) => (
                  <tr key={i}>
                    <td><input list="lapin-materials" value={r.material} onChange={(e) => set(i, { material: e.target.value })} /></td>
                    <td><input className="num" value={r.thicknessMm} onChange={(e) => set(i, { thicknessMm: e.target.value })} /></td>
                    <td><input type="radio" name="slab-core" checked={r.core} onChange={() => set(i, { core: true })} /></td>
                    <td className="row-tools"><button type="button" className="icon mini" title="Remove" onClick={() => setRows(rows.filter((_, k) => k !== i))}><Trash2 size={12} /></button></td>
                  </tr>
                ))}
              </tbody>
            </table>
            <datalist id="lapin-materials">{ofClass(model, 'IfcMaterial').map((m) => <option key={m.id} value={m.name} />)}</datalist>
            <div className="row">
              <button type="button" className="btn" onClick={() => setRows([...rows, { material: 'Insulation', thicknessMm: '50', core: false }])}><Plus size={14} />Layer</button>
              <span className="grow" />
              {editing && <button type="button" className="btn" title="Delete the type (refused while elements use it)" onClick={() => void execute('element.delete', { id: editing }).then((r) => { if (r !== null) setEditing(null); })}><Trash2 size={14} />Delete type</button>}
              {editing && <button type="button" className="btn" onClick={() => useUi.setState({ slabTypeId: editing })}>Draw with it</button>}
              <button type="button" className="btn primary" onClick={() => void save()}>{editing ? 'Save type' : 'Create type'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
