/**
 * Shared parameters (docs/plan-user-extensibility.md §7.3): the project's parameter definitions — name, kind, property set, choices,
 * a description — kept so tags and schedules find the same parameters everywhere, carried between projects as a .lapinparams file.
 * A parameter added in Properties by a definition's name takes its kind and set.
 */

import { useState } from 'react';
import { FileDown, FileUp, Plus, Trash2 } from 'lucide-react';
import { notify } from '../../../app/store';
import { pickFile, safeFileName, saveFile } from '../../../engine/files';
import { PARAM_KINDS, parseOptions, type ParamDef } from './classModel';
import { exportDefs, importDefs, removeDef, setDef, useParamDefs } from './classifications';

export function SharedParams() {
  const defs = useParamDefs();
  const [draft, setDraft] = useState<{ id?: string; name: string; kind: string; pset: string; options: string; description: string } | null>(null);

  const edit = (d: ParamDef) => setDraft({ id: d.id, name: d.name, kind: d.kind, pset: d.pset ?? '', options: (d.options ?? []).join(', '), description: d.description ?? '' });
  const save = async () => {
    if (!draft || !draft.name.trim()) return;
    const options = draft.kind === 'choice' ? parseOptions(draft.options) : undefined;
    if (draft.kind === 'choice' && (options?.length ?? 0) === 0) { notify('A choice parameter needs its choices, separated by commas.'); return; }
    const r = await setDef({ ...(draft.id ? { id: draft.id } : {}), name: draft.name.trim(), kind: draft.kind, ...(draft.pset.trim() ? { pset: draft.pset.trim() } : {}), ...(options ? { options } : {}), ...(draft.description.trim() ? { description: draft.description.trim() } : {}) });
    if (r) setDraft(null);
  };
  const exportAll = async () => {
    const r = await exportDefs();
    if (!r) return;
    const saved = await saveFile(new TextEncoder().encode(r.text), safeFileName(r.file, 'parameters.lapinparams'), 'application/json');
    if (saved) notify(`Exported ${saved}.`);
  };
  const importFile = async () => {
    const f = await pickFile('Shared parameters (.lapinparams)', 'lapinparams');
    if (!f) return;
    const r = await importDefs(new TextDecoder().decode(f.bytes));
    if (r) notify(`${r.added} parameters added, ${r.updated} updated.`);
  };

  return (
    <div data-testid="shared-params">
      <div className="row wrap">
        <button type="button" className="btn" data-field="param-new" onClick={() => setDraft({ name: '', kind: 'text', pset: '', options: '', description: '' })}><Plus size={13} />New</button>
        <button type="button" className="btn" data-field="param-import" onClick={() => void importFile()}><FileUp size={13} />Import .lapinparams…</button>
        <button type="button" className="btn" data-field="param-export" disabled={defs.length === 0} onClick={() => void exportAll()}><FileDown size={13} />Export .lapinparams…</button>
      </div>
      {defs.length === 0 && !draft && <p className="muted small">No shared parameters yet. A definition fixes a parameter's name, kind and property set, so every element and every tag means the same thing by it.</p>}
      {defs.length > 0 && (
        <table className="layer-table">
          <thead><tr><th>Name</th><th>Kind</th><th>Property set</th><th>Choices</th><th /></tr></thead>
          <tbody>
            {defs.map((d) => (
              <tr key={d.id} data-param-def={d.name}>
                <td><button type="button" className="link-button" onClick={() => edit(d)} data-no-i18n>{d.name}</button></td>
                <td>{d.kind}</td>
                <td data-no-i18n>{d.pset ?? ''}</td>
                <td className="small" data-no-i18n>{(d.options ?? []).join(', ')}</td>
                <td className="row-tools"><button type="button" className="icon mini" title="Remove the definition (parameters already on elements stay)" onClick={() => void removeDef(d.id)}><Trash2 size={12} /></button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {draft && (
        <dl className="fields param-def-form" data-testid="param-def-form">
          <dt>Name</dt><dd><input data-field="param-name" value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} /></dd>
          <dt>Kind</dt><dd><select data-field="param-kind" value={draft.kind} onChange={(e) => setDraft({ ...draft, kind: e.target.value })}>{PARAM_KINDS.map((k) => <option key={k} value={k}>{k}</option>)}</select></dd>
          <dt title="The IFC property set it goes to; empty: Lapin_Parameters. A set of yours never starts with Pset_.">Property set</dt>
          <dd><input data-field="param-pset" value={draft.pset} placeholder="Lapin_Parameters" onChange={(e) => setDraft({ ...draft, pset: e.target.value })} /></dd>
          {draft.kind === 'choice' && <><dt>Choices</dt><dd><input data-field="param-options" value={draft.options} placeholder="New, Existing, Demolish" onChange={(e) => setDraft({ ...draft, options: e.target.value })} /></dd></>}
          <dt>Description</dt><dd><input data-field="param-description" value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} /></dd>
          <dt /><dd className="row"><button type="button" className="btn primary" disabled={!draft.name.trim()} data-field="param-save" onClick={() => void save()}>{draft.id ? 'Save' : 'Add'}</button><button type="button" className="btn" onClick={() => setDraft(null)}>Cancel</button></dd>
        </dl>
      )}
    </div>
  );
}
