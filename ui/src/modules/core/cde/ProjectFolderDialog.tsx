/**
 * Manage → Project folder: the naming convention's fields with the name they
 * make, the issues made so far — kept in the project file, each with its
 * files (open or save one), Download as zip, Publish to folder and Delete
 * stored files, and what they take in the file — and, on the desktop, the
 * folder the project is also issued into (local, a share, or a cloud folder
 * synced to disk) with its ISO 19650 containers (docs/plan-cde.md). The
 * folder is optional: the project file holds the issues.
 */

import { useCallback, useEffect, useState } from 'react';
import { Archive, ArchiveX, Download, FolderOpen, FolderOutput, Trash2, X } from 'lucide-react';
import { notify, useUi } from '../../../app/store';
import { execute, useSession } from '../../../engine/session';
import { isDesktop } from '../../../engine/files';
import {
  canUseFolder, cdeIssues, cdeSettings, devOverride, downloadIssueZip, dropIssueFiles, formatSize, issueStorage, missingHashes, openFolder, openIssued, pickFolder,
  publishIssue, saveIssued, type CdeSettings, type Issue, type IssueStorage,
} from './cde';

const when = (iso: string) => { const d = new Date(iso); return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }); };

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
  const [storage, setStorage] = useState<IssueStorage | null>(null);
  const [missing, setMissing] = useState<Set<string>>(new Set());
  const [open, setOpen] = useState<string | null>(null);
  const close = () => useUi.setState({ dialog: null });
  const refresh = useCallback(async () => {
    setS(await cdeSettings());
    setIssues(await cdeIssues());
    setStorage(await issueStorage());
    setMissing(new Set(await missingHashes()));
  }, []);
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
          <p className="muted small">The project file holds every issue and its files. A project folder is where the issues are also exchanged, and it is optional: on this computer, on a network share, or a SharePoint or OneDrive folder synced to disk. No account, no server: what syncs the folder is the office's. The path is kept in the project.</p>
          {canUseFolder() && <div className="version-save">
            {typed
              ? <input value={s.folder ?? ''} placeholder="The folder's full path" aria-label="Folder" onChange={(e) => setS({ ...s, folder: e.target.value })} onBlur={(e) => void setFolder(e.target.value.trim() || null)} />
              : <input value={s.folder ?? ''} placeholder="No folder chosen yet" aria-label="Folder" readOnly />}
            {isDesktop() && <button type="button" className="btn" onClick={() => void choose()}>Choose…</button>}
            {s.folder && isDesktop() && <button type="button" className="btn" title="Show the folder in the file manager" onClick={() => void openFolder(s.folder!)}><FolderOpen size={13} /> Open folder</button>}
            {s.folder && <button type="button" className="icon mini" title="Forget the folder" onClick={() => void setFolder(null)}><Trash2 size={12} /></button>}
          </div>}
          {canUseFolder() && s.folder && !s.exists && <p className="muted small">The folder is not on this computer right now: a warning, not an error. The issues stay in the project file; publish them to the folder when it is back.</p>}
          {!canUseFolder() && <p className="muted small">A project folder is the desktop app's. Here the issues stay in the project file: open their files, or download an issue as a zip.</p>}
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
          {issues.length === 0 && <p className="muted">No issue yet. Manage → Issue… makes the sheets and the model, named by the convention, with a transmittal, keeps them in the project file and saves a version of the project named after the issue.</p>}
          {issues.length > 0 && (
            <>
              <p className="muted small" data-testid="cde-storage">Kept in the project file: <b>{formatSize(storage?.size ?? 0)}</b> <span>(identical files once)</span></p>
              <table className="layer-table versions" data-testid="cde-issues">
                <thead><tr><th>#</th><th>Issue</th><th>When</th><th>Revision</th><th>Status</th><th>Container</th><th>Files</th><th>In the file</th><th /></tr></thead>
                <tbody>
                  {issues.map((i) => (
                    <IssueRows key={i.id} issue={i} projectFolder={s.folder} size={storage?.issues.find((x) => x.id === i.id)?.size ?? 0} missing={missing} open={open === i.id} onToggle={() => setOpen(open === i.id ? null : i.id)} />
                  ))}
                </tbody>
              </table>
            </>
          )}
        </div>
        <footer className="dialog-foot"><span className="grow" /><button type="button" className="btn primary" onClick={close}>Done</button></footer>
      </div>
    </div>
  );
}

function IssueRows({ issue, size, missing, projectFolder, open, onToggle }: { issue: Issue; size: number; missing: Set<string>; projectFolder: string | null; open: boolean; onToggle: () => void }) {
  const folder = issue.folder ? `${issue.folder}/${issue.container}` : null;
  const kept = issue.files.filter((f) => f.stored && !missing.has(f.sha256));
  const publishTo = async () => {
    const picked = isDesktop() ? await pickFolder(null) : window.prompt('Folder to publish into (full path):')?.trim() || null;
    if (picked) await publishIssue(issue, picked);
  };
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
        <td className="num" title={`${kept.length} of ${issue.files.length} files kept in the project file`}>{kept.length ? formatSize(size) : '—'}</td>
        <td className="row-tools">
          {kept.length > 0 && <button type="button" className="icon mini" title="Download as zip" onClick={() => void downloadIssueZip(issue)}><Archive size={12} /></button>}
          {kept.length > 0 && canUseFolder() && <button type="button" className="icon mini" title="Publish to folder…: the project folder's container, or a folder you pick" onClick={() => void (projectFolder ? publishIssue(issue) : publishTo())}><FolderOutput size={12} /></button>}
          {folder && isDesktop() && <button type="button" className="icon mini" title="Open folder" onClick={() => void openFolder(folder)}><FolderOpen size={12} /></button>}
          {kept.length > 0 && <button type="button" className="icon mini" title="Delete stored files: the files leave the project file at the next save; the record, the names and the hashes stay" onClick={() => { if (window.confirm(`Delete the files issue ${issue.number} keeps in the project file? Its record, the file names and their hashes stay.`)) void dropIssueFiles(issue.id); }}><ArchiveX size={12} /></button>}
          <button type="button" className="icon mini" title="Delete this issue's record (the files sent stay where they went)" onClick={() => { if (window.confirm(`Forget issue ${issue.number} (${issue.name})? The files sent stay where they went; those kept only in the project file go with it.`)) void execute('cde.deleteIssue', { id: issue.id }); }}><Trash2 size={12} /></button>
        </td>
      </tr>
      {open && kept.length > 0 && canUseFolder() && (
        <tr className="version-revision">
          <td />
          <td colSpan={8} className="small">
            <button type="button" className="link" onClick={() => void publishTo()}><FolderOutput size={12} /> Publish to another folder…</button>
          </td>
        </tr>
      )}
      {open && issue.files.map((f) => {
        const here = f.stored && !missing.has(f.sha256);
        return (
          <tr key={f.name} className="version-revision">
            <td />
            <td colSpan={5} className="muted">
              {here
                ? <button type="button" className="link" title="Open the file kept in the project" onClick={() => void openIssued(issue.id, f.name)}><code>{f.name}</code></button>
                : <code title={f.kind === 'ifc' ? 'The IFC is kept in the project file only when asked; its name and hash are recorded' : 'Not kept in the project file; its name and hash are recorded'}>{f.name}</code>}
            </td>
            <td className="num muted">{formatSize(f.size)}</td>
            <td className="muted small" title={f.sha256}>{here ? 'kept' : 'listed'}</td>
            <td className="row-tools">{here && <button type="button" className="icon mini" title="Save a copy…" onClick={() => void saveIssued(issue.id, f.name)}><Download size={12} /></button>}</td>
          </tr>
        );
      })}
    </>
  );
}
