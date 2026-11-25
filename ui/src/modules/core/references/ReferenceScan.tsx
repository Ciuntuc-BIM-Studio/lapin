/**
 * Scan folder (the desktop's, when the project has a folder): what the project folder's Reference/ holds that the register does not,
 * with the folder and the type its path suggests, for the user to add; and the register's copies that are missing there or lie
 * elsewhere (docs/plan-cde-references.md §3.6). The register is the truth: a file gone from the disk is said, never deleted.
 */

import { useEffect, useState } from 'react';
import { FolderSearch } from 'lucide-react';
import { notify } from '../../../app/store';
import { useLang } from '../../../i18n';
import { formatSize } from '../format';
import { addRef, flattenFolders, nameIn, scanFolder, type RefFolder, type ScanResult, type TypeList } from './references';

export function ReferenceScan({ folders, types, onDone }: { folders: RefFolder[]; types: TypeList; onDone: () => void }) {
  const ui = useLang((s) => s.lang);
  const [scan, setScan] = useState<ScanResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [on, setOn] = useState<Set<string>>(new Set());
  const [choice, setChoice] = useState<Record<string, { folder?: string; type?: string }>>({});

  const run = async () => {
    setBusy(true);
    try {
      const r = await scanFolder();
      setScan(r);
      setOn(new Set((r?.files ?? []).map((f) => f.path)));
      setChoice(Object.fromEntries((r?.files ?? []).map((f) => [f.path, { folder: f.folder, type: f.type }])));
    } finally { setBusy(false); }
  };
  useEffect(() => { void run(); }, []);

  const add = async () => {
    if (!scan) return;
    setBusy(true);
    try {
      let n = 0;
      for (const f of scan.files.filter((x) => on.has(x.path))) {
        const c = choice[f.path] ?? {};
        const r = await addRef({ files: [{ path: f.path }], ...(c.folder ? { folder: c.folder } : {}), ...(c.type ? { type: c.type } : {}) });
        if (r) { n++; r.warnings.forEach((w) => notify(w)); }
      }
      if (n > 0) notify('Documents added from the folder.');
      onDone();
      await run();
    } finally { setBusy(false); }
  };

  const choices = flattenFolders(folders);
  return (
    <div className="refs-panel" data-testid="refs-scan">
      <div className="row">
        <h4 className="grow">Files in the project folder</h4>
        <button type="button" className="btn small" disabled={busy} onClick={() => void run()}><FolderSearch size={12} /> Scan again</button>
      </div>
      {scan?.root && <p className="muted small"><code>{scan.root}</code></p>}
      {scan?.warning && <p className="cde-warn small">{scan.warning}</p>}
      {scan?.truncated && <p className="cde-warn small">The folder holds more files than are listed.</p>}
      {scan && scan.files.length === 0 && <p className="muted small">Every file in the folder is in the register.</p>}
      {scan && scan.files.length > 0 && (
        <>
          <table className="layer-table">
            <thead><tr><th /><th>File</th><th>Size</th><th>Folder</th><th>Type</th></tr></thead>
            <tbody>
              {scan.files.map((f) => (
                <tr key={f.path}>
                  <td><input type="checkbox" aria-label="Add" checked={on.has(f.path)} onChange={(e) => setOn((p) => { const n = new Set(p); if (e.target.checked) n.add(f.path); else n.delete(f.path); return n; })} /></td>
                  <td title={f.relative}><code>{f.name}</code></td>
                  <td className="num">{formatSize(f.size)}</td>
                  <td><select aria-label="Folder" value={choice[f.path]?.folder ?? ''} onChange={(e) => setChoice((c) => ({ ...c, [f.path]: { ...c[f.path], folder: e.target.value || undefined } }))}>
                    <option value="" />{choices.map((c) => <option key={c.id} value={c.id}>{' '.repeat(c.depth)}{c.text}</option>)}</select></td>
                  <td><select aria-label="Type" value={choice[f.path]?.type ?? ''} onChange={(e) => setChoice((c) => ({ ...c, [f.path]: { ...c[f.path], type: e.target.value || undefined } }))}>
                    <option value="" />{types.types.map((t) => <option key={t.id} value={t.id}>{nameIn(t.name, ui)}</option>)}</select></td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="row"><span className="grow" /><button type="button" className="btn primary small" disabled={busy || on.size === 0} onClick={() => void add()}>Add selected</button></div>
        </>
      )}
      {scan && scan.missing.length > 0 && (
        <>
          <h4>Copies not where they go</h4>
          <ul className="small refs-missing">
            {scan.missing.map((m) => (
              <li key={`${m.id}:${m.name}`}><code>{m.name}</code> — {m.foundAt ? <span title={m.foundAt}>the same file lies in another folder (misplaced)</span> : <span title={m.expected}>missing from the folder</span>}</li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}
