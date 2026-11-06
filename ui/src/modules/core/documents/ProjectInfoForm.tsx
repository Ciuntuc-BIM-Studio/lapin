/**
 * The project's information (docs/plan-documents.md, Datele proiectului; decision 340): its name and the fields a document or a
 * title block asks, by group — the project, the client, the site and its permit, the designer, the people who sign — and the
 * project's own parameters, each with a unique key read as {project.<key>} by title blocks and {{project.<key>}} by documents.
 * Written once and read everywhere. Each field is saved when it is left (Enter too) and only when it changed: one undo step
 * each. Manage → Project information… shows it alone; the Documents dialog shows it in its second tab.
 */

import { useEffect, useState } from 'react';
import { Plus, Trash2, X } from 'lucide-react';
import { useUi } from '../../../app/store';
import { execute, useSession } from '../../../engine/session';
import { t as tr } from '../../../i18n';
import { PendingDialog } from '../PendingDialog';
import { projectInfo, setProjectInfo } from './documents';
import type { ProjectInfo, ProjectParameter } from './docModel';

/** The project's information as the engine keeps it, read again after every edit. */
export function useProjectInfo(): ProjectInfo | null {
  const version = useSession((s) => s.version);
  const [info, setInfo] = useState<ProjectInfo | null>(null);
  useEffect(() => {
    let live = true;
    void projectInfo().then((r) => { if (live && r) setInfo(r); });
    return () => { live = false; };
  }, [version]);
  return info;
}

/** A field committed when it is left or on Enter, only when it changed; `multiline` an address over a few lines. */
function InfoField({ name, label, value, list, multiline, token, onCommit }: { name: string; label: string; value: string; list?: string; multiline?: boolean; token?: string; onCommit: (v: string) => void }) {
  const props = {
    defaultValue: value, 'data-field': `info-${name}`, 'aria-label': label,
    onBlur: (e: { target: { value: string } }) => { if (e.target.value.trim() !== value.trim()) onCommit(e.target.value); },
  };
  return (
    <label className="doc-info-row">
      <span className="muted small">{label}{token && <code className="doc-info-token" data-no-i18n>{`{${token}}`}</code>}</span>
      {multiline
        ? <textarea key={value} {...props} rows={2} />
        : <input key={value} {...props} list={list} onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />}
    </label>
  );
}

const MULTILINE = new Set(['clientAddress', 'designerAddress', 'address']);
const TYPES: { id: ProjectParameter['type']; label: string }[] = [{ id: 'text', label: 'Text' }, { id: 'number', label: 'Number' }, { id: 'date', label: 'Date' }];

/** A parameter set; a refusal (a key taken, a value not of its type) shows where the engine's errors show. */
const setParameter = (args: Record<string, unknown>) => execute('project.setParameter', args);

/** The project's own parameters: each with its key, label, type, group and value; a new one by its key and label. */
function ParametersForm({ info }: { info: ProjectInfo }) {
  const custom = info.custom ?? [];
  const groups = info.groups ?? [];
  const [key, setKey] = useState('');
  const [label, setLabel] = useState('');
  const add = async () => {
    const k = key.trim();
    if (!k) return;
    await setParameter({ key: k, label: label.trim() || k, group: 'other' });
    setKey(''); setLabel('');
  };
  return (
    <section className="doc-info-group" data-field="info-parameters">
      <h4 className="eyebrow">The project's own parameters</h4>
      <p className="muted small">{'Anything a title block or a document needs that is not above: a unique key (letters, digits, _), read as {project.key} in title blocks and {{project.key}} in documents; exported in IFC with the project (Lapin_ProjectInformation).'}</p>
      {custom.length > 0 && (
        <table className="doc-info-params">
          <thead><tr><th>Key</th><th>Label</th><th>Type</th><th>Group</th><th>Value</th><th /></tr></thead>
          <tbody>
            {custom.map((p) => (
              <tr key={p.key} data-param={p.key}>
                <td><input key={p.key} defaultValue={p.key} aria-label="Key" data-no-i18n onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== p.key) void setParameter({ key: p.key, newKey: v }); }} /></td>
                <td><input key={p.label} defaultValue={p.label} aria-label="Label" data-no-i18n onBlur={(e) => { if (e.target.value.trim() !== p.label) void setParameter({ key: p.key, label: e.target.value }); }} /></td>
                <td>
                  <select value={p.type} aria-label="Type" onChange={(e) => void setParameter({ key: p.key, type: e.target.value, value: null })}>
                    {TYPES.map((t) => <option key={t.id} value={t.id}>{t.label}</option>)}
                  </select>
                </td>
                <td>
                  <select value={p.group} aria-label="Group" onChange={(e) => void setParameter({ key: p.key, group: e.target.value })}>
                    {groups.map((g) => <option key={g.key} value={g.key}>{g.label}</option>)}
                    {!groups.some((g) => g.key === p.group) && <option value={p.group} data-no-i18n>{p.group}</option>}
                  </select>
                </td>
                <td><input key={`${p.key}:${p.value ?? ''}`} type={p.type === 'date' ? 'date' : p.type === 'number' ? 'number' : 'text'} defaultValue={p.value ?? ''} aria-label="Value" data-field={`param-${p.key}`} data-no-i18n
                  onBlur={(e) => { if (e.target.value !== (p.value ?? '')) void setParameter({ key: p.key, value: e.target.value }); }}
                  onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} /></td>
                <td><button type="button" className="icon mini" title="Remove the parameter" onClick={() => void execute('project.removeParameter', { key: p.key })}><Trash2 size={12} /></button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <div className="row">
        <input value={key} onChange={(e) => setKey(e.target.value)} placeholder={tr('Key, e.g. permitAuthority')} aria-label="New parameter's key" data-field="param-new-key" data-no-i18n
          onKeyDown={(e) => { if (e.key === 'Enter') void add(); }} />
        <input value={label} onChange={(e) => setLabel(e.target.value)} placeholder={tr('Label')} aria-label="New parameter's label" data-field="param-new-label" data-no-i18n
          onKeyDown={(e) => { if (e.key === 'Enter') void add(); }} />
        <button type="button" className="btn" onClick={() => void add()} disabled={!key.trim()} data-field="param-add"><Plus size={13} />Add parameter</button>
      </div>
    </section>
  );
}

export function ProjectInfoForm({ info }: { info: ProjectInfo }) {
  const put = (key: string) => (v: string) => void setProjectInfo({ [key]: v.trim() || null });
  const text = (key: string) => (typeof info[key] === 'string' ? (info[key] as string) : '');
  const groups = info.groups ?? [{ key: 'project', label: 'Project' }];
  const fieldsOf = (g: string) => info.fields.filter((f) => (f.group ?? 'project') === g);
  return (
    <div className="doc-info" data-field="doc-info">
      {groups.filter((g) => g.key === 'project' || fieldsOf(g.key).length > 0).map((g) => (
        <section key={g.key} className="doc-info-group">
          <h4 className="eyebrow">{g.label}</h4>
          {g.key === 'project' && <InfoField name="name" label="Project name" value={info.name ?? ''} token="project.name" onCommit={(v) => void setProjectInfo({ name: v.trim() || null })} />}
          {fieldsOf(g.key).map((f) => (
            <InfoField key={f.key} name={f.key} label={f.label} value={text(f.key)} multiline={MULTILINE.has(f.key)} token={`project.${f.key}`}
              list={f.key === 'phase' ? 'doc-info-phases' : undefined} onCommit={put(f.key)} />
          ))}
        </section>
      ))}
      <datalist id="doc-info-phases">{info.phases.map((p) => <option key={p} value={p} />)}</datalist>
      <ParametersForm info={info} />
      <p className="muted small">Documents and title blocks read these fields; a sheet's title block takes who drew and checked from here when the sheet does not say.</p>
    </div>
  );
}

/** Manage → Project information…: the same form alone. */
export function ProjectInfoDialog() {
  const info = useProjectInfo();
  const close = () => useUi.setState({ dialog: null });
  if (!info) return <PendingDialog title="Project information" />;
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog project-info" role="dialog" aria-label="Project information" data-dialog="project-info" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Project information</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body"><ProjectInfoForm info={info} /></div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn" onClick={close}>Close</button></footer>
      </div>
    </div>
  );
}
