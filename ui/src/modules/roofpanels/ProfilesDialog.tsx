/**
 * Roof panel profiles… (docs/plan-roof-panels.md §3, stage S7): Lapin's profiles, read-only, and the project's own. A built-in
 * profile is changed by duplicating it into the project and editing the copy, so other projects and the roofs that keep the
 * built-in one never change; the copy keeps the original's article codes, so it prices as the original does. The editor shows the
 * sheet's fields one by one (widths, lap, module, joints, lengths or order range, colours, least pitches), and the rest of the
 * profile (accessories, support, fixings, ridge caps, flat sheet) as JSON, in the built-in profiles' own format. A profile a roof
 * uses is not deleted. A firm keeps its profiles in a .lapinpanels file: Export…, Import… (through the one import preview) and the
 * firm folders.
 */

import { backdropClose } from '../../shell/Dialog';
import { confirmAction, phrase } from '../../shell/prompts';
import { useEffect, useState } from 'react';
import { Copy, FileDown, FileUp, Plus, Save, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../app/store';
import { pickFile, safeFileName, saveFile } from '../../engine/files';
import { importLibraryText } from '../../platform/ImportPreview';
import { FirmFolderFiles } from '../core/FirmFolderFiles';
import { PendingDialog } from '../core/PendingDialog';
import { deleteProfile, duplicateProfile, exportProfiles, PROFILES_EXT, updateProfile, useProfileLibrary } from './projectProfiles';
import { lengthsOf, PROFILE_FIELDS, PROFILE_JSON_KEYS, saleOf, withSale, type BuiltInProfileDto, type PanelColour, type ProjectProfileDto } from './roofpanels';

type Selected = { kind: 'builtin'; profile: string } | { kind: 'project'; id: string } | null;

export function ProfilesDialog() {
  const lib = useProfileLibrary();
  const [selected, setSelected] = useState<Selected>(null);
  const close = () => useUi.setState({ dialog: null });
  useEffect(() => {
    if (!lib || selected) return;
    setSelected(lib.project[0] ? { kind: 'project', id: lib.project[0].id } : lib.builtin[0] ? { kind: 'builtin', profile: lib.builtin[0].profile } : null);
  }, [lib, selected]);
  if (!lib) return <PendingDialog title="Roof panel profiles" />;
  const builtin = selected?.kind === 'builtin' ? lib.builtin.find((b) => b.profile === selected.profile) ?? null : null;
  const project = selected?.kind === 'project' ? lib.project.find((p) => p.id === selected.id) ?? null : null;

  const duplicate = async (from: string) => {
    const r = await duplicateProfile(from);
    if (r) { setSelected({ kind: 'project', id: r.id }); notify('A copy of the profile is in the project: edit it here; the original stays as it is.'); }
  };
  const exportAll = async () => {
    const r = await exportProfiles();
    if (!r) return;
    const saved = await saveFile(new TextEncoder().encode(r.text), safeFileName(r.file, `Roof panel profiles.${PROFILES_EXT}`), 'application/json');
    if (saved) notify(phrase('Exported {file}.', { file: saved }));
  };
  const importFile = async (given?: { name?: string; bytes: Uint8Array }) => {
    const f = given ?? await pickFile('Roof panel profiles (.lapinpanels)', PROFILES_EXT);
    if (!f) return;
    // Through the one import preview: a profile of the same id updated (its roofs follow), kept beside, or skipped.
    await importLibraryText(new TextDecoder().decode(f.bytes), 'name' in f && typeof f.name === 'string' ? f.name : `profiles.${PROFILES_EXT}`);
  };

  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog wide templates roof-panel-profiles" role="dialog" aria-label="Roof panel profiles" data-dialog="roofPanelProfiles" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Roof panel profiles</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body tpl-body">
          <aside className="tpl-list">
            <div className="row wrap">
              <button type="button" className="btn" data-action="panel-profile-import" title="A .lapinpanels file: profiles the project lacks are added, those with the same id updated" onClick={() => void importFile()}><FileUp size={13} />Import…</button>
              <button type="button" className="btn" data-action="panel-profile-export" disabled={lib.project.length === 0} title="The project's profiles as a .lapinpanels file, for the firm's library or another project" onClick={() => void exportAll()}><FileDown size={13} />Export…</button>
            </div>
            <h4 className="eyebrow">This project's</h4>
            {lib.project.length === 0 && <p className="muted small">None of your own yet. Duplicate one of Lapin's to change its widths, laps, lengths, colours or accessories.</p>}
            <ul className="doc-templates">
              {lib.project.map((p) => (
                <li key={p.id} className={selected?.kind === 'project' && selected.id === p.id ? 'active' : undefined}>
                  <button type="button" data-panel-profile={p.profile} onClick={() => setSelected({ kind: 'project', id: p.id })}>
                    <span className="sy-item-text"><strong data-no-i18n>{p.name}</strong>{p.uses > 0 && <span className="muted small">{phrase('Roofs: {n}', { n: p.uses })}</span>}</span>
                  </button>
                </li>
              ))}
            </ul>
            <h4 className="eyebrow">Lapin's</h4>
            <ul className="doc-templates">
              {lib.builtin.map((b) => (
                <li key={b.profile} className={selected?.kind === 'builtin' && selected.profile === b.profile ? 'active' : undefined}>
                  <button type="button" data-panel-profile={b.profile} onClick={() => setSelected({ kind: 'builtin', profile: b.profile })}>
                    <span className="sy-item-text"><strong data-no-i18n>{b.name}</strong><span className="muted small">Read-only</span></span>
                  </button>
                </li>
              ))}
            </ul>
            <FirmFolderFiles ext={PROFILES_EXT} onPick={(f) => importFile(f)}
              save={{ disabled: lib.project.length === 0, get: async () => { const r = await exportProfiles(); return r ? { text: r.text, name: r.file.replace(/\.[^.]+$/, '') } : null; } }} />
          </aside>
          <main className="tpl-detail">
            {builtin && <BuiltInView p={builtin} onDuplicate={() => void duplicate(builtin.profile)} />}
            {project && <ProfileEditor key={project.id + JSON.stringify(project.def)} p={project} onDuplicate={() => void duplicate(project.profile)} onGone={() => setSelected(null)} />}
            {!builtin && !project && <p className="muted">Choose a profile.</p>}
          </main>
        </div>
      </div>
    </div>
  );
}

/** A built-in profile: its main numbers and Duplicate to edit. */
function BuiltInView({ p, onDuplicate }: { p: BuiltInProfileDto; onDuplicate: () => void }) {
  const s = p.summary;
  return (
    <div data-testid="panel-profile-builtin">
      <h3 data-no-i18n>{p.name}</h3>
      <p className="muted small">Lapin's own profile, indicative: it stays as it is in every project. Duplicate it to make a profile of this project you can change.</p>
      <dl className="fields">
        <dt>Maker</dt><dd data-no-i18n>{p.maker}</dd>
        <dt>Whole width (mm)</dt><dd className="num" data-no-i18n>{s.grossWidth}</dd>
        <dt>Useful width (mm)</dt><dd className="num" data-no-i18n>{s.usefulWidth}</dd>
        <dt>Slope lap (mm)</dt><dd className="num" data-no-i18n>{s.slopeLap}</dd>
        <dt>Module (mm)</dt><dd className="num" data-no-i18n>{s.module}</dd>
        <dt>Lengths (mm)</dt><dd className="num" data-no-i18n>{s.onOrder ? `${s.minLength} – ${s.maxLength}` : (s.lengths ?? []).join(', ')}</dd>
        <dt>Colours</dt><dd data-no-i18n>{s.colors.map((c) => c.code).join(', ')}</dd>
        <dt>Least pitch (°)</dt><dd className="num" data-no-i18n>{`${s.minPitch} / ${s.minPitchHipValley}`}</dd>
      </dl>
      <button type="button" className="btn primary" data-action="panel-profile-duplicate" onClick={onDuplicate}><Copy size={13} />Duplicate to edit</button>
    </div>
  );
}

/** A profile of the project, edited: the sheet's fields, its lengths, its colours, the rest as JSON; Save is one undo step. */
function ProfileEditor({ p, onDuplicate, onGone }: { p: ProjectProfileDto; onDuplicate: () => void; onGone: () => void }) {
  const [def, setDef] = useState<Record<string, unknown>>(() => ({ ...p.def }));
  const [sale, setSale] = useState(() => saleOf(p.def));
  const [lengthsText, setLengthsText] = useState(() => saleOf(p.def).lengths.join(', '));
  const [json, setJson] = useState(() => JSON.stringify(Object.fromEntries(PROFILE_JSON_KEYS.filter((k) => p.def[k] !== undefined).map((k) => [k, p.def[k]])), null, 2));
  const [jsonProblem, setJsonProblem] = useState<string | null>(null);
  const colours = (def.colors as PanelColour[] | undefined) ?? [];
  const set = (key: string, value: unknown) => setDef({ ...def, [key]: value });
  const setColour = (i: number, patch: Partial<PanelColour>) => set('colors', colours.map((c, k) => (k === i ? { ...c, ...patch } : c)));

  const save = async () => {
    let rest: Record<string, unknown>;
    try { rest = JSON.parse(json || '{}'); if (!rest || typeof rest !== 'object' || Array.isArray(rest)) throw new Error('an object'); }
    catch { setJsonProblem('The accessories and the rest are JSON: an object of the keys shown.'); return; }
    setJsonProblem(null);
    const out = withSale({ ...def }, { ...sale, lengths: lengthsOf(lengthsText) });
    for (const k of PROFILE_JSON_KEYS) delete out[k];
    Object.assign(out, rest);
    if (await updateProfile(p.id, out)) notify('Profile saved: the roofs that use it follow.');
  };
  const remove = async () => {
    if (p.uses > 0) { notify(phrase('{name} covers {roofs}: choose another profile for them first.', { name: p.name, roofs: p.roofs.join(', ') })); return; }
    if (!confirmAction('Delete the profile {name}?', { name: p.name })) return;
    if (await deleteProfile(p.id)) onGone();
  };
  return (
    <div data-testid="panel-profile-editor">
      <div className="row" style={{ justifyContent: 'space-between' }}>
        <h3 data-no-i18n>{p.name}</h3>
        {p.basedOn && <span className="muted small">{phrase('Copied from {name}', { name: p.basedOn })}</span>}
      </div>
      {p.problem && <p className="error small" data-no-i18n>{p.problem}</p>}
      {p.uses > 0 && <p className="muted small" data-testid="panel-profile-uses">{phrase('Used by {roofs}.', { roofs: p.roofs.join(', ') })}</p>}
      <dl className="fields">
        {PROFILE_FIELDS.map((f) => {
          const v = def[f.key];
          return (
            <FieldRow key={f.key} label={f.label}>
              {f.kind === 'bool' ? <input type="checkbox" data-field={`pp-${f.key}`} checked={v === true} onChange={(e) => set(f.key, e.target.checked)} />
                : f.kind === 'joints' ? (
                  <select className="cell" style={{ width: 'auto' }} data-field={`pp-${f.key}`} value={String(v ?? 'module')} onChange={(e) => set(f.key, e.target.value)}>
                    <option value="module">On the module (tile)</option>
                    <option value="staggered">Staggered (standing seam)</option>
                  </select>
                ) : f.kind === 'number' ? (
                  <input className="cell num" data-field={`pp-${f.key}`} inputMode="decimal" value={v == null ? '' : String(v)}
                    onChange={(e) => { const t = e.target.value.replace(',', '.'); const n = Number(t); set(f.key, t.trim() === '' ? undefined : Number.isFinite(n) ? n : v); }} />
                ) : (
                  <input className="cell" style={{ width: '100%', minWidth: 260 }} data-field={`pp-${f.key}`} value={v == null ? '' : String(v)} data-no-i18n onChange={(e) => set(f.key, e.target.value)} />
                )}
            </FieldRow>
          );
        })}
        <FieldRow label="Sold">
          <select className="cell" data-field="pp-sale" value={sale.kind} onChange={(e) => setSale({ ...sale, kind: e.target.value as 'stock' | 'order' })}>
            <option value="stock">In stock lengths</option>
            <option value="order">Made to order</option>
          </select>
        </FieldRow>
        {sale.kind === 'stock' ? (
          <FieldRow label="Lengths (mm)"><input className="cell" style={{ width: '100%', minWidth: 260 }} data-field="pp-lengths" value={lengthsText} placeholder="1180, 2230, 2930" onChange={(e) => setLengthsText(e.target.value)} /></FieldRow>
        ) : (['min', 'start', 'step', 'max'] as const).map((k) => (
          <FieldRow key={k} label={{ min: 'Shortest (mm)', start: 'Regular lengths from (mm)', step: 'Every (mm)', max: 'Longest (mm)' }[k]}>
            <input className="cell num" data-field={`pp-order-${k}`} inputMode="numeric" value={String(sale.order[k])}
              onChange={(e) => { const n = Math.round(Number(e.target.value)); if (Number.isFinite(n)) setSale({ ...sale, order: { ...sale.order, [k]: n } }); }} />
          </FieldRow>
        ))}
      </dl>
      <p className="eyebrow">Colours</p>
      <table className="layer-table" data-testid="panel-profile-colours">
        <thead><tr><th>Code</th><th>Name</th><th>Colour</th><th /></tr></thead>
        <tbody>
          {colours.map((c, i) => (
            <tr key={i}>
              <td><input className="cell" value={c.code} data-no-i18n onChange={(e) => setColour(i, { code: e.target.value })} /></td>
              <td><input className="cell" value={c.name} data-no-i18n onChange={(e) => setColour(i, { name: e.target.value })} /></td>
              <td><input type="color" value={c.rgb ?? '#6b6f73'} onChange={(e) => setColour(i, { rgb: e.target.value })} /></td>
              <td><button type="button" className="icon mini" title="Remove the colour" onClick={() => set('colors', colours.filter((_, k) => k !== i))}><Trash2 size={12} /></button></td>
            </tr>
          ))}
        </tbody>
      </table>
      <button type="button" className="btn small" data-action="panel-profile-colour" onClick={() => set('colors', [...colours, { code: '', name: '', rgb: '#6b6f73' }])}><Plus size={12} />Colour</button>
      <p className="eyebrow" title="In the format of Lapin's own profiles: each accessory's code, unit and counting rule, the support, the fixings, the ridge caps, the flat sheet">Accessories, support, fixings and flat sheet (JSON)</p>
      <textarea className="code" data-field="pp-json" rows={10} spellCheck={false} value={json} onChange={(e) => setJson(e.target.value)} style={{ width: '100%', fontFamily: 'var(--mono, monospace)', fontSize: 12 }} data-no-i18n />
      {jsonProblem && <p className="error small">{jsonProblem}</p>}
      <div className="row" style={{ gap: 8, marginTop: 8 }}>
        <button type="button" className="btn primary" data-action="panel-profile-save" onClick={() => void save()}><Save size={13} />Save</button>
        <button type="button" className="btn" data-action="panel-profile-copy" onClick={onDuplicate}><Copy size={13} />Duplicate</button>
        <span className="grow" />
        <button type="button" className="btn" data-action="panel-profile-delete" title={p.uses > 0 ? 'A roof uses it: choose another profile for it first' : undefined} onClick={() => void remove()}><Trash2 size={13} />Delete</button>
      </div>
    </div>
  );
}

function FieldRow({ label, children }: { label: string; children: React.ReactNode }) {
  return <><dt>{label}</dt><dd>{children}</dd></>;
}
