/**
 * Furniture models of your own (docs/plan-user-extensibility.md §3.5, decision 512): a GLB brought in with its licence (required: whose it
 * is and on what terms; it travels with the model) and its source; its size read from the file; each material of the file given a role
 * (upholstery, accent, body, top, legs, metal, glass, shade, foliage, pot) so a type's colours reach it; the stretch zones per axis as
 * two sliders (the ends outside the zone keep their size, the middle stretches); the sizes it may take, the height it hangs at.
 * The models are listed beside Lapin's when a type of a model is made, and go with the project's furniture library.
 */

import { useEffect, useState } from 'react';
import { FileUp, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { useSession } from '../../engine/session';
import { pickFile } from '../../engine/files';
import { LengthField } from '../../shell/cells';
import { ROLE_LABELS } from './furniture';
import { AXES, MODEL_ROLES, setZoneEnd, toggleZone, zonePercent, zonesArg, type Axis, type UserModel, type Zones } from './models';
import { deleteModel, importModel, updateModel, useUserModels } from './userModels';

const AXIS_LABEL: Record<Axis, string> = { x: 'Width', y: 'Depth', z: 'Height' };

interface Incoming { name: string; fileName: string; bytes: Uint8Array; licence: string; source: string; group: string }

export function UserModelsDialog() {
  const models = useUserModels();
  const [selected, setSelected] = useState<string | null>(null);
  const [incoming, setIncoming] = useState<Incoming | null>(null);
  const error = useSession((s) => s.error);
  const close = () => useUi.setState({ dialog: 'furnitureTypes' });
  useEffect(() => { if (!incoming && !models.some((m) => m.id === selected)) setSelected(models[0]?.id ?? null); }, [models.map((m) => m.id).join(','), incoming]);
  const current = models.find((m) => m.id === selected) ?? null;

  const choose = async () => {
    const f = await pickFile('Binary glTF (.glb)', 'glb');
    if (!f) return;
    setIncoming({ name: f.name.replace(/\.glb$/i, ''), fileName: f.name, bytes: f.bytes, licence: '', source: '', group: '' });
  };
  const doImport = async () => {
    if (!incoming || !incoming.licence.trim()) return;
    const r = await importModel(incoming.bytes, { name: incoming.name.trim() || incoming.fileName, licence: incoming.licence.trim(), ...(incoming.source.trim() ? { source: incoming.source.trim() } : {}), ...(incoming.group.trim() ? { group: incoming.group.trim() } : {}) });
    if (r) { setIncoming(null); setSelected(r.id); notify(`Model imported: ${r.materials.length} materials to give a role.`); }
  };

  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide templates furniture-models-dialog" role="dialog" aria-label="Furniture models" data-dialog="furnitureModels" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Your furniture models</span><button type="button" className="icon" onClick={close} title="Back to the furniture types"><X size={15} /></button></header>
        <div className="dialog-body tpl-body">
          <aside className="tpl-list">
            <div className="row"><button type="button" className="btn" data-field="model-import" onClick={() => void choose()}><FileUp size={13} />Import model (GLB)…</button></div>
            {models.length === 0 && <p className="muted small">None yet. A GLB (binary glTF) comes in with its licence; its size and materials are read from the file.</p>}
            <ul className="doc-templates">
              {models.map((m) => (
                <li key={m.id} className={m.id === selected && !incoming ? 'active' : undefined}>
                  <button type="button" data-user-model={m.name} onClick={() => { setIncoming(null); setSelected(m.id); }}><span className="sy-item-text"><strong data-no-i18n>{m.name}</strong><span className="muted small" data-no-i18n>{m.licence}</span></span></button>
                </li>
              ))}
            </ul>
          </aside>
          <main className="tpl-detail">
            {incoming ? (
              <div data-testid="model-import-form">
                <h3 className="tpl-title" data-no-i18n>{incoming.fileName}</h3>
                <dl className="fields">
                  <dt>Name</dt><dd><input data-field="model-name" value={incoming.name} onChange={(e) => setIncoming({ ...incoming, name: e.target.value })} /></dd>
                  <dt title="Whose it is and on what terms (CC0, CC-BY-4.0, your own…): it travels with the model">Licence</dt>
                  <dd><input data-field="model-licence" value={incoming.licence} placeholder="CC0-1.0, CC-BY-4.0, your own…" onChange={(e) => setIncoming({ ...incoming, licence: e.target.value })} /></dd>
                  <dt title="Where it comes from: the maker, the site, the URL">Source</dt>
                  <dd><input data-field="model-source" value={incoming.source} onChange={(e) => setIncoming({ ...incoming, source: e.target.value })} /></dd>
                  <dt title="The group it is listed under in the gallery">Group</dt>
                  <dd><input data-field="model-group" value={incoming.group} placeholder="My models" onChange={(e) => setIncoming({ ...incoming, group: e.target.value })} /></dd>
                </dl>
                {!incoming.licence.trim() && <p className="muted small">Say the licence: a model comes in with whose it is and on what terms.</p>}
                <div className="row">
                  <button type="button" className="btn primary" data-field="model-import-go" disabled={!incoming.licence.trim()} onClick={() => void doImport()}>Import</button>
                  <button type="button" className="btn" onClick={() => setIncoming(null)}>Cancel</button>
                </div>
              </div>
            ) : current ? <ModelEditor key={current.id + current.code} model={current} onGone={() => setSelected(null)} /> : <p className="muted">Import a model, or choose one.</p>}
            {error && <p className="error-text" role="alert">{error}</p>}
          </main>
        </div>
      </div>
    </div>
  );
}

function ModelEditor({ model, onGone }: { model: UserModel; onGone: () => void }) {
  const natural = model.natural?.length === 3 ? model.natural : [1, 1, 1];
  const [d, setD] = useState({
    name: model.name, licence: model.licence ?? '', source: model.source ?? '', group: model.group ?? '',
    size: model.size?.length === 3 ? model.size : natural, min: model.min?.length === 3 ? model.min : natural.map((x) => x * 0.25), max: model.max?.length === 3 ? model.max : natural.map((x) => x * 4),
    zones: (model.zones ?? {}) as Zones, slots: model.slots ?? {}, mount: model.mount ?? 0, against: model.against ?? true, seats: model.seats ?? false,
  });
  const set = <K extends keyof typeof d>(k: K, v: (typeof d)[K]) => setD({ ...d, [k]: v });
  const save = async () => {
    if (!d.licence.trim()) { notify('Say the model’s licence: it travels with it.'); return; }
    const r = await updateModel(model.id, { name: d.name.trim() || model.name, licence: d.licence.trim(), source: d.source.trim(), group: d.group.trim(), size: d.size, min: d.min, max: d.max, zones: zonesArg(d.zones), slots: d.slots, mount: d.mount, against: d.against, seats: d.seats });
    if (r) notify(model.types > 0 ? 'Model saved: its types follow.' : 'Model saved.');
  };
  const remove = async () => { if (await deleteModel(model.id) !== null) onGone(); };
  const triple = (key: 'size' | 'min' | 'max', label: string, title?: string) => (
    <>
      <dt title={title}>{label}</dt>
      <dd className="row">{d[key].map((v, i) => <LengthField key={i} metres={v} min={0.01} max={50} testId={`model-${key}-${AXES[i]}`} onCommit={(m) => { if (m != null) set(key, d[key].map((x, j) => (j === i ? m : x))); }} />)}</dd>
    </>
  );
  return (
    <div data-testid="user-model-editor">
      <dl className="fields">
        <dt>Name</dt><dd><input data-field="model-name" value={d.name} onChange={(e) => set('name', e.target.value)} /></dd>
        <dt title="Whose it is and on what terms: it travels with the model">Licence</dt><dd><input data-field="model-licence" value={d.licence} onChange={(e) => set('licence', e.target.value)} /></dd>
        <dt>Source</dt><dd><input data-field="model-source" value={d.source} onChange={(e) => set('source', e.target.value)} /></dd>
        <dt>Group</dt><dd><input data-field="model-group" value={d.group} placeholder="My models" onChange={(e) => set('group', e.target.value)} /></dd>
        {triple('size', 'Size (width, depth, height)', 'What a new type of this model starts with')}
        {triple('min', 'Least size')}
        {triple('max', 'Most size')}
        <dt title="The height its underside hangs at (a wall lamp, an upper unit); 0: on the floor">Hung at</dt>
        <dd><LengthField metres={d.mount} min={0} max={5} onCommit={(m) => { if (m != null) set('mount', m); }} /></dd>
        <dt>Stands against a wall</dt><dd><input type="checkbox" checked={d.against} onChange={(e) => set('against', e.target.checked)} /></dd>
        <dt>Seats</dt><dd><input type="checkbox" checked={d.seats} onChange={(e) => set('seats', e.target.checked)} /></dd>
      </dl>
      <h4 className="eyebrow">Materials of the file → roles</h4>
      <table className="layer-table" data-testid="model-slots">
        <thead><tr><th>Material in the file</th><th>Role</th></tr></thead>
        <tbody>
          {Object.entries(d.slots).map(([mat, role]) => (
            <tr key={mat}>
              <td data-no-i18n>{mat}</td>
              <td><select value={role} data-slot={mat} onChange={(e) => set('slots', { ...d.slots, [mat]: e.target.value })}>
                {[...new Set([role, ...MODEL_ROLES])].map((r) => <option key={r} value={r}>{ROLE_LABELS[r] ?? r}</option>)}
              </select></td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="muted small">A type of this model gives a material to each role: upholstery, legs, glass… The materials with the same role take the same colour.</p>
      <h4 className="eyebrow">Stretch zones</h4>
      <div data-testid="model-zones">
        {AXES.map((a) => {
          const z = zonePercent(d.zones[a]);
          return (
            <div key={a} className="row" data-zone-axis={a}>
              <label className="check small" style={{ width: 90 }}><input type="checkbox" checked={!!z} data-zone-toggle={a} onChange={(e) => set('zones', toggleZone(d.zones, a, e.target.checked))} /> {AXIS_LABEL[a]}</label>
              {z ? <>
                <input type="range" min={0} max={100} value={z[0]} data-zone-from={a} aria-label="Zone from" onChange={(e) => set('zones', setZoneEnd(d.zones, a, 0, Number(e.target.value) / 100))} />
                <input type="range" min={0} max={100} value={z[1]} data-zone-to={a} aria-label="Zone to" onChange={(e) => set('zones', setZoneEnd(d.zones, a, 1, Number(e.target.value) / 100))} />
                <span className="muted small" data-no-i18n>{z[0]}% – {z[1]}%</span>
              </> : <span className="muted small">scaled whole</span>}
            </div>
          );
        })}
      </div>
      <p className="muted small">Along a zone the model stretches; outside it the ends keep their size (a sofa's arms, a table's legs). An axis without a zone scales whole.</p>
      <div className="row">
        <button type="button" className="btn primary" data-field="model-save" onClick={() => void save()}>Save</button>
        <button type="button" className="btn danger" data-field="model-delete" title={model.types > 0 ? 'Types are made of it: delete or change them first' : undefined} onClick={() => void remove()}><Trash2 size={13} />Delete</button>
        <span className="muted small" data-no-i18n>{model.types > 0 ? `${model.types}` : ''}</span>
      </div>
    </div>
  );
}
