/** Floor types: the project's IfcSlabTypes and their layers, top to bottom. Saving is one engine command; every floor of the type follows. */

import { useEffect, useMemo, useRef, useState } from 'react';
import { ArrowDown, ArrowUp, ArrowUpDown, Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../app/store';
import { execute, useSession } from '../../engine/session';
import { materialName, ofClass, relatedOf, typeLayers } from '../../engine/model';
import { Cell, NumberField } from '../../shell/cells';
import { layerRankHint } from './layerRank';
import { flagsJson, flagsOf } from './layerFlags';
import { CATEGORY_TIP, LayerCategory } from './LayerCategory';
import { LayerThickness } from '../core/LayerThickness';
import { MaterialHatchButton } from '../core/MaterialHatchButton';
import { takeStash } from '../core/dialogReturn';
import { deleteType } from '../core/deleteType';
import { SaveTypeToLibrary } from '../core/SaveTypeToLibrary';
import { MaterialPicker } from '../core/MaterialPicker';
import { TypeFiles } from './TypeFiles';
import { moveLayer, reverseLayers } from './reverseLayers';
import { t } from '../../i18n';
import { TypeListActions, endsOf } from '../../shell/TypeListActions';

/** A layer as the editor holds it: the thickness in metres; its priority (null: its material's, else 50 for the core and 0 for the rest) and category (null: its material's). */
interface Row { material: string; thickness: number; core: boolean; note?: string; priority?: number | null; category?: string | null }
const blank = (): Row[] => [{ material: 'Floor finish', thickness: 0.02, core: false }, { material: 'Screed', thickness: 0.05, core: false }, { material: 'Reinforced concrete', thickness: 0.15, core: true }];
/** What a floor layer's priority says where a wall's layer goes on into it (topic #30). */
const PRIORITY_TIP = 'Where a wall\'s layer goes on into this floor, it passes the floor\'s layers ranked under it and stops at the first as high or higher; empty: its material\'s priority in the cut order, else 50 for the core and 0 for the rest';

export function SlabTypesDialog() {
  const nodes = useSession((s) => s.nodes);
  const relations = useSession((s) => s.relations);
  const error = useSession((s) => s.error);
  const model = useMemo(() => ({ nodes, relations }), [nodes, relations]);
  const types = ofClass(model, 'IfcSlabType');
  // Back from Materials (forum #45): the type and the unsaved edits as they were left.
  const kept = useRef<{ editing: string | null; name: string; rows: Row[] } | null | undefined>(undefined);
  if (kept.current === undefined) kept.current = takeStash<{ editing: string | null; name: string; rows: Row[] }>('slabTypes');
  const k0 = kept.current;
  const [editing, setEditing] = useState<string | null>(k0 ? k0.editing : types[0]?.id ?? null);
  const [name, setName] = useState(k0?.name ?? '');
  const [rows, setRows] = useState<Row[]>(k0?.rows ?? blank());
  const restoredFor = useRef<string | null | undefined>(k0 ? k0.editing : undefined);
  // The editor loads when the picked type changes, not on every diff (which
  // would throw away what is being typed): it reads the model through a ref.
  const modelRef = useRef(model);
  modelRef.current = model;
  useEffect(() => {
    if (restoredFor.current !== undefined && restoredFor.current === editing) return; // the edits kept, not the saved type
    restoredFor.current = undefined;
    const model = modelRef.current;
    const t = editing ? model.nodes.get(editing) : undefined;
    if (!t) { setName(''); setRows(blank()); return; }
    setName(t.name);
    setRows(typeLayers(model, t.id).map((l) => ({ material: materialName(model, l.material), thickness: l.thickness, core: l.core === true, note: l.note ?? '',
      priority: typeof l.priority === 'number' ? l.priority : null, category: flagsOf(l).category })));
  }, [editing]);
  const set = (i: number, patch: Partial<Row>) => setRows(rows.map((r, k) => (k === i ? { ...r, ...patch } : patch.core ? { ...r, core: false } : r)));
  const move = (i: number, d: number) => setRows(moveLayer(rows, i, d));
  const reverse = () => {
    if (window.confirm(t('Reverse the order of the layers? Once the type is saved, the floors of this type turn over: the top layer goes to the underside.'))) setRows(reverseLayers(rows));
  };
  // Its priority and category kept when saved (a floor's layers were saved without the priority before).
  const layers = rows.map((r) => ({ material: r.material.trim(), thickness: r.thickness, core: r.core, ...(r.note?.trim() ? { note: r.note.trim() } : {}),
    ...(r.priority != null ? { priority: r.priority } : {}), ...flagsJson({ category: r.category ?? null, down: false, up: false }, false) }));
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
            <li><TypeFiles kind="floor" onImported={(names) => { const t = ofClass(useSession.getState(), 'IfcSlabType').find((n) => n.name === names[0]); if (t) setEditing(t.id); }} /></li>
          </ul>
          <div className="type-edit">
            <label className="field-line"><span className="eyebrow">Name</span><input value={name} placeholder="(from its core and thickness)" onChange={(e) => setName(e.target.value)} /></label>
            {/* Duplicate, and the type's place in every list of floor types (forum #71). */}
            <TypeListActions id={editing} onDuplicated={setEditing} {...endsOf(types, editing)} />
            <p className="muted small">Layers from the top down.</p>
            <table className="layer-edit">
              <thead><tr><th>Material</th><th>Thickness</th><th>Core</th><th title={PRIORITY_TIP}>Priority</th><th title={CATEGORY_TIP}>Category</th><th title="How the layer is laid in this build-up (a build-up tag writes it)">Note</th><th /></tr></thead>
              <tbody>
                {rows.map((r, i) => (
                  <tr key={i}>
                    <td><span className="layer-material"><MaterialPicker value={r.material} onChange={(material) => set(i, { material })} /><MaterialHatchButton material={r.material} from={{ kind: 'slabTypes', snapshot: () => ({ editing, name, rows }) }} /></span></td>
                    <td className="num"><LayerThickness model={model} material={r.material} metres={r.thickness} onCommit={(m) => set(i, { thickness: m })} /></td>
                    <td><input type="radio" name="slab-core" checked={r.core} onChange={() => set(i, { core: true })} /></td>
                    <td><NumberField className="layer-priority" testId="layer-priority" value={r.priority ?? null} placeholder={String(layerRankHint(model, r.material, r.core))} blank min={0} max={100} integer
                      title={PRIORITY_TIP} onCommit={(n) => set(i, { priority: n })} /></td>
                    <td><LayerCategory value={r.category ?? null} onChange={(category) => set(i, { category })} /></td>
                    <td><Cell className="layer-note" testId="layer-note" value={r.note ?? ''} placeholder="—" title="How the layer is laid in this build-up (a build-up tag writes it)" onCommit={(t) => set(i, { note: t })} /></td>
                    <td className="row-tools">
                      <button type="button" className="icon mini" data-testid="layer-up" title="Move layer up" aria-label="Move layer up" disabled={i === 0} onClick={() => move(i, -1)}><ArrowUp size={12} /></button>
                      <button type="button" className="icon mini" data-testid="layer-down" title="Move layer down" aria-label="Move layer down" disabled={i === rows.length - 1} onClick={() => move(i, 1)}><ArrowDown size={12} /></button>
                      <button type="button" className="icon mini" title="Remove" onClick={() => setRows(rows.filter((_, k) => k !== i))}><Trash2 size={12} /></button></td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="row">
              <button type="button" className="btn" onClick={() => setRows([...rows, { material: 'Insulation', thickness: 0.05, core: false }])}><Plus size={14} />Layer</button>
              <button type="button" className="btn" data-testid="reverse-layers" title="Run the layers the other way: the top layer goes to the underside. Floors of the type follow once it is saved" disabled={rows.length < 2} onClick={reverse}><ArrowUpDown size={14} />Reverse layers</button>
              <span className="grow" />
              {editing && <SaveTypeToLibrary ids={[editing]} />}
              {editing && <button type="button" className="btn" title="Delete the type (refused while elements use it)" onClick={() => void deleteType(editing).then((ok) => { if (ok) setEditing(null); })}><Trash2 size={14} />Delete type</button>}
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
