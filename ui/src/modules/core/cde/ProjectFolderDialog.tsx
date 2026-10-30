/**
 * Manage → Project folder: the folder the project is issued into (local, a
 * share, or a cloud folder synced to disk), its ISO 19650 containers, the
 * naming convention's fields with the name they make, and the issues made so
 * far, each with its files and Open folder (docs/plan-cde.md).
 */

import { useCallback, useEffect, useState } from 'react';
import { FolderOpen, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { execute, useSession } from '../../../engine/session';
import { isDesktop } from '../../../engine/files';
import { canIssue, cdeIssues, cdeSettings, devOverride, openFolder, pickFolder, type CdeSettings, type Issue } from './cde';

const when = (iso: string) => { const d = new Date(iso); return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }); };
const size = (bytes: number) => (bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} kB`);

const NAMING: [keyof CdeSettings['naming'], string, string][] = [
  ['project', 'Project code', '2–6 letters and digits: the project as the office codes it'],
  ['originator', 'Originator', '3–6 letters and digits: the office issuing the files'],
  ['volume', 'Volume / system', 'ZZ for every volume, XX for none, else the volume'],
  ['level', 'Level (model)', "The model's level field; a sheet takes its plans' storey"],
  ['role', 'Role (default)', "The default role; a sheet takes its discipline's"],
];

export function ProjectFolderDialog() {
  const nodes = useSession((s) => s.nodes);
  const [s, setS] = useState<CdeSettings | null>(null);
  const [issues, setIssues] = useState<Issue[]>([]);
  const [open, setOpen] = useState<string | null>(null);
  const close = () => useUi.setState({ dialog: null });
  const refresh = useCallback(async () => { setS(await cdeSettings()); setIssues(await cdeIssues()); }, []);
  useEffect(() => { void refresh(); }, [refresh, nodes]);
  const typed = !isDesktop() || devOverride();

  const setFolder = async (path: string | null) => {
    try { await execute('cde.setFolder', { path }); } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };
  const choose = async () => { const p = await pickFolder(s?.folder ?? null); if (p) await setFolder(p); };
  const setNaming = async (key: string, value: string) => {
    try { await execute('cde.setNaming', { [key]: value }); } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };

  if (!s) return null;
  return (
    <div className="dialog-backdrop" onClick={close}>
      <div className="dialog wide" role="dialog" aria-label="Project folder" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Project folder</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body cde">
          <p className="muted small">The folder the project is issued into: on this computer, on a network share, or a SharePoint or OneDrive folder synced to disk. No account, no server: what syncs the folder is the office's. The path is kept in the project.</p>
          <div className="version-save">
            {typed
              ? <input value={s.folder ?? ''} placeholder="The folder's full path" aria-label="Folder" onChange={(e) => setS({ ...s, folder: e.target.value })} onBlur={(e) => void setFolder(e.target.value.trim() || null)} />
              : <input value={s.folder ?? ''} placeholder="No folder chosen yet" aria-label="Folder" readOnly />}
            {isDesktop() && <button type="button" className="btn" onClick={() => void choose()}>Choose…</button>}
            {s.folder && isDesktop() && <button type="button" className="btn" title="Show the folder in the file manager" onClick={() => void openFolder(s.folder!)}><FolderOpen size={13} /> Open folder</button>}
            {s.folder && <button type="button" className="icon mini" title="Forget the folder" onClick={() => void setFolder(null)}><Trash2 size={12} /></button>}
          </div>
          {s.folder && !s.exists && <p className="muted small">The folder is not on this computer right now: a warning, not an error. An issue needs it.</p>}
          {!canIssue() && <p className="muted small">Issuing writes files, so it needs the desktop app; the checks and the naming work here.</p>}
          <h4>Containers (ISO 19650)</h4>
          <p className="muted small">{s.containers.join(' · ')} — made inside the folder on the first issue. A status code suggests the container: S0 in WIP, S1–S7 in Shared, A, B and CR in Published.</p>
          <h4>Naming (ISO 19650-2)</h4>
          <p className="muted small">Project-Originator-Volume-Level-Type-Role-Number, then the status and the revision: <code>{s.example}_S2_{s.nextRevision}.ifc</code></p>
          <p className="muted small">A sheet's level is its plans' storey (00 at ±0.00, 01 above, B1 below, ZZ for several), its role its discipline's, its number the digits of its sheet number.</p>
          <div className="cde-naming">
            {NAMING.map(([key, label, hint]) => (
              <label key={key} className="row" title={hint}><span className="muted small loc-label">{label}</span>
                {key === 'role'
                  ? <select value={s.naming.role} onChange={(e) => void setNaming('role', e.target.value)}>{s.roles.map((r) => <option key={r.code} value={r.code}>{r.code} · {r.label}</option>)}</select>
                  : <input key={`${key}:${s.naming[key]}`} defaultValue={s.naming[key]} onBlur={(e) => { if (e.target.value.trim() !== s.naming[key]) void setNaming(key, e.target.value); }} />}
              </label>
            ))}
          </div>
          <h4>Issues</h4>
          {issues.length === 0 && <p className="muted">No issue yet. Manage → Issue… writes the sheets and the model into a container, named by the convention, with a transmittal, and saves a version of the project named after the issue.</p>}
          {issues.length > 0 && (
            <table className="layer-table versions">
              <thead><tr><th>#</th><th>Issue</th><th>When</th><th>Revision</th><th>Status</th><th>Container</th><th>Files</th><th /></tr></thead>
              <tbody>
                {issues.map((i) => (
                  <IssueRows key={i.id} issue={i} open={open === i.id} onToggle={() => setOpen(open === i.id ? null : i.id)} />
                ))}
              </tbody>
            </table>
          )}
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}

function IssueRows({ issue, open, onToggle }: { issue: Issue; open: boolean; onToggle: () => void }) {
  const folder = issue.folder ? `${issue.folder}/${issue.container}` : null;
  return (
    <>
      <tr>
        <td className="num">{issue.number}</td>
        <td><button type="button" className="link" onClick={onToggle}>{issue.name}</button>{issue.note ? <span className="muted small"> · {issue.note}</span> : null}</td>
        <td>{when(issue.at)}</td>
        <td>{issue.revision}</td>
        <td>{issue.status}</td>
        <td>{issue.container}</td>
        <td className="num">{issue.files.length}</td>
        <td className="row-tools">
          {folder && isDesktop() && <button type="button" className="icon mini" title="Open folder" onClick={() => void openFolder(folder)}><FolderOpen size={12} /></button>}
          <button type="button" className="icon mini" title="Delete this issue's record (the files stay)" onClick={() => { if (window.confirm(`Forget issue ${issue.number} (${issue.name})? Its files stay in the folder.`)) void execute('cde.deleteIssue', { id: issue.id }); }}><Trash2 size={12} /></button>
        </td>
      </tr>
      {open && issue.files.map((f) => (
        <tr key={f.name} className="version-revision">
          <td />
          <td colSpan={5} className="muted"><code>{f.name}</code></td>
          <td className="num muted">{size(f.size)}</td>
          <td className="muted small" title={f.sha256}>{f.sha256.slice(0, 8)}</td>
        </tr>
      ))}
    </>
  );
}
