/**
 * Materials: every IfcMaterial of the project, its colour (3D) and how it
 * draws where a section or plan cuts it and in elevation: hatch, scale, fill
 * and pen, or a membrane line. A field it does not set comes from its name
 * (concrete, brick, insulation…). Hatch patterns can be imported from .pat
 * files; they travel with the project. A material can carry its own image
 * for 3D (OwnImage), which the 3D exports take along.
 */

import { useEffect, useState } from 'react';
import { FileUp, Plus, RotateCcw, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { execute, query, useSession } from '../../engine/session';
import { HatchSwatch } from './HatchSwatch';
import { OwnImage } from './OwnImage';
import { forgetPatterns, keepPatterns, myPatterns } from './myPatterns';

type Kind = 'section' | 'plan' | 'elevation';
/** IfcMaterial.Category as the interface offers it (Materials.Categories in the engine). */
const MATERIAL_CATEGORIES = ['Structure', 'Finish', 'Insulation', 'Membrane', 'Other'];

type Resolved = { hatch: string | null; hatchScale: number; fill: string | null; hatchPen?: number; hatchAngle?: number; hatchAlign?: 'view' | 'layer'; membrane: boolean; drawJoints: boolean; source: 'own' | 'name' };
type Own = { hatch?: string | null; hatchScale?: number; fill?: string | null; hatchPen?: number; hatchAngle?: number; hatchAlign?: 'view' | 'layer' };
const TEXTURES = ['none', 'brick', 'brick-end', 'concrete', 'wood', 'wood-end', 'plaster', 'stone', 'metal', 'insulation', 'earth', 'tiles'];
type Material = { id: string; name: string; color: string; inUse: boolean; section: Resolved; plan: Resolved; elevation: Resolved; own: Partial<Record<Kind, Own>> };
type Hatch = { name: string; description: string; solid: boolean; project: boolean };

const KINDS: { kind: Kind; label: string }[] = [{ kind: 'section', label: 'Cut (section)' }, { kind: 'plan', label: 'Cut (plan)' }, { kind: 'elevation', label: 'Elevation' }];

export function MaterialsDialog() {
  const nodes = useSession((s) => s.nodes);
  const error = useSession((s) => s.error);
  const [list, setList] = useState<Material[]>([]);
  const [hatches, setHatches] = useState<Hatch[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  const [mine, setMine] = useState(myPatterns);
  const [newName, setNewName] = useState('');
  // Every edit comes back as a diff: read the materials again.
  useEffect(() => {
    void query<Material[]>('material.list').then((r) => { if (r) { setList(r); setSelected((cur) => cur ?? r[0]?.id ?? null); } });
    void query<Hatch[]>('hatch.list').then((r) => r && setHatches(r));
  }, [nodes]);
  const m = list.find((x) => x.id === selected) ?? null;
  const update = (args: Record<string, unknown>) => m && void execute('material.update', { id: m.id, ...args });
  const setView = (kind: Kind, patch: Own | null) => update({ [kind]: patch });
  /** One field back to what the name (or, for a plan, the section) gives. */
  const clearField = (kind: Kind, field: keyof Own) => {
    const rest = { ...(m?.own[kind] ?? {}) };
    delete rest[field];
    update({ [kind]: Object.keys(rest).length ? rest : null, replace: true });
  };

  const importPat = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.pat,text/plain';
    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) return;
      const text = await file.text();
      const r = await execute('hatch.import', { text }) as { count: number; errors: string[] } | null;
      if (r) {
        keepPatterns(file.name, text);
        setMine(myPatterns());
        notify(`${r.count} hatch pattern${r.count === 1 ? '' : 's'} loaded${r.errors.length ? `, ${r.errors.length} line(s) skipped` : ''}, and kept in My patterns on this computer.`);
      }
    };
    input.click();
  };

  return (
    <div className="dialog-backdrop" role="dialog" aria-label="Materials">
      <div className="dialog wide">
        <header className="dialog-head">
          <span className="panel-title">Materials</span>
          <button type="button" className="icon" onClick={() => useUi.setState({ dialog: null })} title="Close"><X size={15} /></button>
        </header>
        <div className="dialog-body split">
          <div>
            <ul className="type-list">
              {list.map((x) => (
                <li key={x.id}>
                  <button type="button" className={`tree-item material-item${selected === x.id ? ' on' : ''}`} onClick={() => setSelected(x.id)}>
                    <span className="swatch" style={{ background: x.color }} />{x.name}{!x.inUse && <span className="muted small"> · unused</span>}
                  </button>
                </li>
              ))}
            </ul>
            <div className="row">
              <input value={newName} placeholder="New material" onChange={(e) => setNewName(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter' && newName.trim()) void execute('material.create', { name: newName.trim() }).then((r) => { const id = (r as { id?: string } | null)?.id; if (id) { setSelected(id); setNewName(''); } }); }} />
              <button type="button" className="icon" title="Add" disabled={!newName.trim()}
                onClick={() => void execute('material.create', { name: newName.trim() }).then((r) => { const id = (r as { id?: string } | null)?.id; if (id) { setSelected(id); setNewName(''); } })}><Plus size={14} /></button>
            </div>
            <button type="button" className="btn" onClick={importPat}><FileUp size={14} />Import .pat</button>
            {mine.length > 0 && (
              <div className="my-patterns">
                <span className="eyebrow" title="The .pat files you imported, kept on this computer for any project">My patterns</span>
                <ul className="plain">
                  {mine.map((f) => (
                    <li key={f.name} className="row">
                      <span className="grow small" title={f.names.join(', ')}>{f.name} <span className="muted">· {f.names.length}</span></span>
                      <button type="button" className="icon mini" title="Forget this file (projects keep what they use)" onClick={() => { forgetPatterns(f.name); setMine(myPatterns()); }}><Trash2 size={12} /></button>
                    </li>
                  ))}
                </ul>
                <button type="button" className="btn compact" onClick={() => void execute('hatch.import', { text: mine.map((f) => f.text).join('\n') }).then((r) => {
                  const n = (r as { count?: number } | null)?.count ?? 0;
                  notify(`${n} pattern${n === 1 ? '' : 's'} from My patterns in this project.`);
                })}>Add to this project</button>
              </div>
            )}
          </div>
          {m && (
            <div className="type-edit">
              <div className="row">
                <label className="field-line grow"><span className="eyebrow">Name</span>
                  <input key={m.id + m.name} defaultValue={m.name} onBlur={(e) => e.target.value.trim() && e.target.value.trim() !== m.name && update({ name: e.target.value.trim() })} /></label>
                <label className="field-line"><span className="eyebrow">Colour (3D)</span>
                  <input type="color" value={m.color} onChange={(e) => update({ color: e.target.value })} /></label>
                <label className="field-line" title="What the material is in a layered element (IfcMaterial.Category). A view style without finishes leaves out the layers of finish materials; without a category, plaster, render, paint, tiles, boards and floor coverings count as finishes."><span className="eyebrow">Category</span>
                  <select value={String(useSession.getState().nodes.get(m.id)?.props.category ?? '')} onChange={(e) => update({ category: e.target.value || null })}>
                    <option value="">by its name</option>
                    {MATERIAL_CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
                  </select></label>
              </div>
              <div className="row">
                {([['texture', 'Faces (3D)', 'guessed'], ['textureEnd', 'Ends, cut across (3D)', 'as the faces (brick: its holes)'], ['textureTop', 'Top and bottom (3D)', 'as the ends']] as const).map(([key, label, dflt]) => (
                  <label key={key} className="field-line"><span className="eyebrow">{label}</span>
                    <select value={String(useSession.getState().nodes.get(m.id)?.props[key] ?? '')} onChange={(e) => update({ [key]: e.target.value || null })}>
                      <option value="">{dflt}</option>
                      {TEXTURES.map((t) => <option key={t} value={t}>{t}</option>)}
                    </select></label>
                ))}
              </div>
              <OwnImage materialId={m.id} />
              <div className="row">
                <label className="check"><input type="checkbox" checked={m.section.membrane} onChange={(e) => update({ membrane: e.target.checked })} /> Membrane (a dashed line, not a band)</label>
                <label className="check" title="Off (the default): where this material goes on from one element into another (two walls' cores, footings round a corner) no line is drawn between them. On: the joint is drawn."><input type="checkbox" checked={m.section.drawJoints} onChange={(e) => update({ drawJoints: e.target.checked })} /> Draw joints</label>
              </div>
              <table className="material-views">
                <thead><tr><th /><th>Look</th><th>Hatch</th><th>Scale</th><th>Angle</th><th title="Where the hatch starts and turns: from the view's origin, or from each band it fills (along the band's longest edge)">Align</th><th>Fill</th><th>Pen</th></tr></thead>
                <tbody>
                  {KINDS.map(({ kind, label }) => {
                    const r = m[kind];
                    const own = m.own[kind];
                    const hatchValue = own && 'hatch' in own ? (own.hatch ?? '') : '*';
                    const fillMode = own?.fill === 'material' ? 'material' : own?.fill ? 'colour' : '';
                    return (
                      <tr key={kind}>
                        <th>{label}{kind !== 'section' && own && <button type="button" className="icon mini" title="As the section" onClick={() => setView(kind, null)}><RotateCcw size={11} /></button>}</th>
                        <td><HatchSwatch hatch={r.hatch} scale={r.hatchScale} fill={r.fill} membrane={kind !== 'elevation' && r.membrane} /></td>
                        <td>
                          <select value={hatchValue} onChange={(e) => (e.target.value === '*' ? clearField(kind, 'hatch') : setView(kind, { hatch: e.target.value || null }))}>
                            <option value="*">{kind === 'elevation' ? 'none (default)' : `from the name${r.source === 'name' && r.hatch ? ` (${r.hatch})` : ''}`}</option>
                            <option value="">none</option>
                            {hatches.map((h) => <option key={h.name} value={h.name}>{h.name}{h.project ? ' (project)' : ''}</option>)}
                          </select>
                        </td>
                        <td><input className="num" key={`${m.id}${kind}s${r.hatchScale}`} defaultValue={r.hatchScale}
                          onBlur={(e) => { const v = Number(e.target.value.replace(',', '.')); if (v > 0 && v !== r.hatchScale) setView(kind, { hatchScale: v }); }} /></td>
                        <td><input className="num" key={`${m.id}${kind}a${r.hatchAngle ?? 0}`} defaultValue={r.hatchAngle ?? 0}
                          onBlur={(e) => { const v = Number(e.target.value.replace(',', '.')); if (Number.isFinite(v) && v !== (r.hatchAngle ?? 0)) setView(kind, { hatchAngle: v }); }} /></td>
                        <td><select value={r.hatchAlign ?? 'view'} onChange={(e) => setView(kind, { hatchAlign: e.target.value as 'view' | 'layer' })}><option value="view">view</option><option value="layer">layer</option></select></td>
                        <td>
                          <select value={fillMode} onChange={(e) => (e.target.value === '' ? clearField(kind, 'fill') : setView(kind, { fill: e.target.value === 'material' ? 'material' : (r.fill ?? '#ffffff') }))}>
                            <option value="">none</option><option value="material">material colour</option><option value="colour">colour…</option>
                          </select>
                          {fillMode === 'colour' && <input type="color" value={own?.fill ?? '#ffffff'} onChange={(e) => setView(kind, { fill: e.target.value })} />}
                        </td>
                        <td><input className="num" key={`${m.id}${kind}p${r.hatchPen}`} defaultValue={r.hatchPen ?? ''} placeholder="—"
                          onBlur={(e) => { const v = Number(e.target.value); if (e.target.value && v >= 1 && v <= 16 && v !== r.hatchPen) setView(kind, { hatchPen: v }); }} /></td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
              <p className="muted small">Hatches are paper millimetres at 1:1; a scale enlarges them. A fill is off unless asked.</p>
              <div className="row">
                <button type="button" className="btn" onClick={() => void execute('material.reset', { id: m.id })}><RotateCcw size={14} />From the name</button>
                <span className="grow" />
                <button type="button" className="btn" disabled={m.inUse} title={m.inUse ? 'In use' : 'Delete'} onClick={() => void execute('material.delete', { id: m.id }).then(() => setSelected(null))}><Trash2 size={14} />Delete</button>
              </div>
              {error && <p className="error-text" role="alert">{error}</p>}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
