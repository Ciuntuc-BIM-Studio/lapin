/**
 * Hardscape types (site grading T4): the project's LapinHardscapeTypes and their layers, top down (paving, bedding, sub-base),
 * and the ready-made ones (indicative) to bring in. Saving is one engine command; every hardscape of the type follows.
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { ArrowDown, ArrowUp, Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { materialName, ofClass, relatedOf, typeLayers } from '../../engine/model';
import { LayerThickness } from '../core/LayerThickness';
import { MaterialPicker } from '../core/MaterialPicker';
import { deleteType } from '../core/deleteType';
import { HARDSCAPE_TYPE } from './hardscape';
import { useHardscapeTool, useHardscapeTypeEdit, usePresets } from './hardscapeTools';

interface Row { material: string; thickness: number }
const blank = (): Row[] => [{ material: 'Concrete pavers', thickness: 0.06 }, { material: 'Sand bedding', thickness: 0.04 }, { material: 'Crushed stone', thickness: 0.2 }];

export function HardscapeTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, HARDSCAPE_TYPE);
  const presets = usePresets().filter((p) => !types.some((t) => t.props.preset === p.key));
  const [editing, setEditing] = useState<string | null>(() => useHardscapeTypeEdit.getState().id ?? types[0]?.id ?? null);
  const [name, setName] = useState('');
  const [pavement, setPavement] = useState('RIGID');
  const [rows, setRows] = useState<Row[]>(blank());
  const modelRef = useRef(model);
  modelRef.current = model;
  // The editor loads when the picked type changes, not on every diff (which would throw away what is being typed).
  useEffect(() => {
    const m = modelRef.current;
    const t = editing ? m.nodes.get(editing) : undefined;
    if (!t) { setName(''); setPavement('RIGID'); setRows(blank()); return; }
    setName(t.name);
    setPavement(t.props.predefinedType === 'FLEXIBLE' ? 'FLEXIBLE' : 'RIGID');
    setRows(typeLayers(m, t.id).map((l) => ({ material: materialName(m, l.material), thickness: l.thickness })));
  }, [editing]);
  const set = (i: number, patch: Partial<Row>) => setRows(rows.map((r, k) => (k === i ? { ...r, ...patch } : r)));
  const move = (i: number, d: number) => { const next = [...rows]; const [r] = next.splice(i, 1); next.splice(i + d, 0, r); setRows(next); };
  const layers = rows.map((r) => ({ material: r.material.trim(), thickness: r.thickness }));
  const editingNode = editing ? nodes.get(editing) : undefined;
  const save = async () => {
    if (editing) await execute('civil.updateHardscapeType', { id: editing, name, layers, pavement });
    else {
      const r = await execute('civil.addHardscapeType', { name, layers, pavement }) as { id?: string } | null;
      if (r?.id) setEditing(r.id);
    }
  };
  const bring = async (key: string) => {
    const r = await execute('civil.addHardscapeType', { preset: key }) as { id?: string } | null;
    if (r?.id) setEditing(r.id);
  };
  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Hardscape types" data-testid="hardscape-types">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Hardscape types</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <ul className="type-list">
            {types.map((t) => (
              <li key={t.id}><button type="button" className={`tree-item${editing === t.id ? ' on' : ''}`} onClick={() => setEditing(t.id)}>{t.name}<span className="muted small"> · {relatedOf(model, t.id, 'IfcRelDefinesByType').length}</span></button></li>
            ))}
            <li><button type="button" className={`tree-item${editing === null ? ' on' : ''}`} onClick={() => setEditing(null)}><Plus size={12} /> New type</button></li>
            {presets.length > 0 && <li className="muted small" style={{ padding: '6px 4px 2px' }}>Ready-made (indicative)</li>}
            {presets.map((p) => (
              <li key={p.key}><button type="button" className="tree-item" data-testid={`hardscape-preset-${p.key}`} title="Bring it into the project, then change it as you like" onClick={() => void bring(p.key)}><Plus size={12} /> {p.name}</button></li>
            ))}
          </ul>
          <div className="type-edit">
            <label className="field-line"><span className="eyebrow">Name</span><input value={name} placeholder="(from its top layer and depth)" onChange={(e) => setName(e.target.value)} /></label>
            <label className="field-line"><span className="eyebrow">Pavement</span>
              <select value={pavement} data-testid="hardscape-pavement" title="IFC 4.3's kind of pavement: rigid (pavers, slabs, concrete) or flexible (asphalt, gravel)" onChange={(e) => setPavement(e.target.value)}>
                <option value="RIGID">Rigid</option>
                <option value="FLEXIBLE">Flexible</option>
              </select>
            </label>
            {editingNode?.props.indicative === true && <p className="muted small">Ready-made, indicative: check its layers against your own build-up.</p>}
            <p className="muted small">Layers from the top down.</p>
            <table className="layer-edit">
              <thead><tr><th>Material</th><th>Thickness</th><th /></tr></thead>
              <tbody>
                {rows.map((r, i) => (
                  <tr key={i}>
                    <td><MaterialPicker value={r.material} onChange={(material) => set(i, { material })} /></td>
                    <td className="num"><LayerThickness model={model} material={r.material} metres={r.thickness} onCommit={(m) => set(i, { thickness: m })} /></td>
                    <td className="row-tools">
                      <button type="button" className="icon mini" title="Move layer up" aria-label="Move layer up" disabled={i === 0} onClick={() => move(i, -1)}><ArrowUp size={12} /></button>
                      <button type="button" className="icon mini" title="Move layer down" aria-label="Move layer down" disabled={i === rows.length - 1} onClick={() => move(i, 1)}><ArrowDown size={12} /></button>
                      <button type="button" className="icon mini" title="Remove" disabled={rows.length < 2} onClick={() => setRows(rows.filter((_, k) => k !== i))}><Trash2 size={12} /></button></td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="row">
              <button type="button" className="btn" onClick={() => setRows([...rows, { material: 'Crushed stone', thickness: 0.1 }])}><Plus size={14} />Layer</button>
              <span className="grow" />
              {editing && <button type="button" className="btn" title="Delete the type (refused while hardscapes use it)" onClick={() => void deleteType(editing).then((ok) => { if (ok) setEditing(null); })}><Trash2 size={14} />Delete type</button>}
              {editing && <button type="button" className="btn" onClick={() => { useHardscapeTool.setState({ type: editing }); useUi.setState({ dialog: null }); }}>Draw with it</button>}
              <button type="button" className="btn primary" data-testid="hardscape-type-save" onClick={() => void save()}>{editing ? 'Save type' : 'Create type'}</button>
            </div>
            {error && <p className="error-text" role="alert">{error}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}
