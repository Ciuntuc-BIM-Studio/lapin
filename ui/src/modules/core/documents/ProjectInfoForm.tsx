/**
 * The project's information (docs/plan-documents.md, Datele proiectului): its name, number, phase, client, site, designer and
 * who drew and checked it, written once and read by every document ({{project.*}}) and by the sheets' title blocks. Each field
 * is saved when it is left (Enter too) and only when it changed: one undo step each. Manage → Project information… shows it
 * alone; the Documents dialog shows it in its second tab.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { PendingDialog } from '../PendingDialog';
import { projectInfo, setProjectInfo } from './documents';
import type { ProjectInfo } from './docModel';

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
function InfoField({ name, label, value, list, multiline, onCommit }: { name: string; label: string; value: string; list?: string; multiline?: boolean; onCommit: (v: string) => void }) {
  const props = {
    defaultValue: value, 'data-field': `info-${name}`, 'aria-label': label,
    onBlur: (e: { target: { value: string } }) => { if (e.target.value.trim() !== value.trim()) onCommit(e.target.value); },
  };
  return (
    <label className="doc-info-row">
      <span className="muted small">{label}</span>
      {multiline
        ? <textarea key={value} {...props} rows={2} />
        : <input key={value} {...props} list={list} onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />}
    </label>
  );
}

const MULTILINE = new Set(['clientAddress', 'designerAddress', 'address']);

export function ProjectInfoForm({ info }: { info: ProjectInfo }) {
  const put = (key: string) => (v: string) => void setProjectInfo({ [key]: v.trim() || null });
  const text = (key: string) => (typeof info[key] === 'string' ? (info[key] as string) : '');
  return (
    <div className="doc-info" data-field="doc-info">
      <InfoField name="name" label="Project name" value={info.name ?? ''} onCommit={(v) => void setProjectInfo({ name: v.trim() || null })} />
      {info.fields.map((f) => (
        <InfoField key={f.key} name={f.key} label={f.label} value={text(f.key)} multiline={MULTILINE.has(f.key)}
          list={f.key === 'phase' ? 'doc-info-phases' : undefined} onCommit={put(f.key)} />
      ))}
      <datalist id="doc-info-phases">{info.phases.map((p) => <option key={p} value={p} />)}</datalist>
      <p className="muted small">Documents read these fields, and the sheets' title blocks take who drew and checked from here when a sheet does not say.</p>
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
