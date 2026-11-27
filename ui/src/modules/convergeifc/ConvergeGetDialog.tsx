/**
 * File → Collaborate → Converge IFC: Get… (docs/plan-converge-ifc.md): a version of an IFC file of a Converge IFC server
 * (the current one unless another is chosen) saved on this computer, then the Import IFC dialog, where the user chooses how
 * it comes in: recognised, as references or linked. Saved over a file the project already links, the link is read again.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { backdropClose } from '../../shell/Dialog';
import { getFromServer, gettable, listFiles, listVersions, useConverge, type ConvergeFile, type ConvergeVersion } from './convergeifc';
import { message, SignedIn, SignInButton, SignInFields, useAccountProjects, useSignInForm } from './ConvergeAccount';

/** A size as the list says it. */
export const sizeText = (bytes: number) => (bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`);

export function ConvergeGetDialog() {
  const close = () => useUi.setState({ dialog: null });
  const [error, setError] = useState<string | null>(null);
  const form = useSignInForm(setError);
  const { account, checking, projects, project, setProject } = useAccountProjects(setError);
  const [files, setFiles] = useState<ConvergeFile[] | null>(null);
  const [file, setFile] = useState('');
  const [versions, setVersions] = useState<ConvergeVersion[]>([]);
  const [version, setVersion] = useState(0);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setFiles(null); setFile('');
    if (!account || !project) return;
    let live = true;
    listFiles(project).then((list) => {
      if (!live) return;
      const mine = gettable(list);
      setFiles(mine);
      const last = useConverge.getState().file;
      setFile(mine.some((f) => f.id === last) ? last! : mine[0]?.id ?? '');
    }).catch((e) => setError(message(e)));
    return () => { live = false; };
  }, [account, project]);

  useEffect(() => {
    setVersions([]); setVersion(0);
    if (!account || !file) return;
    let live = true;
    listVersions(file).then((list) => {
      if (!live) return;
      setVersions(list);
      setVersion(list.find((v) => v.isCurrent)?.versionNo ?? list[0]?.versionNo ?? 0);
    }).catch((e) => setError(message(e)));
    return () => { live = false; };
  }, [account, file]);

  const chosen = files?.find((f) => f.id === file);

  const doGet = async () => {
    if (!chosen) return;
    setBusy(true); setError(null);
    // On success the Import IFC dialog takes this one's place.
    try { await getFromServer({ projectId: project, file: chosen, versionNo: version }); } catch (e) { setError(message(e)); } finally { setBusy(false); }
  };

  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog" role="dialog" aria-label="Get from Converge IFC" data-testid="converge-get" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Get from Converge IFC</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {checking ? <p className="muted">Looking for your account…</p> : !account ? <SignInFields form={form} /> : (
            <>
              <SignedIn account={account} />
              <dl className="fields">
                <dt>Project</dt>
                <dd><select className="converge-project" value={project} onChange={(e) => setProject(e.target.value)}>
                  {projects.length === 0 && <option value="">no project on this server</option>}
                  {projects.map((p) => <option key={p.id} value={p.id} data-no-i18n>{p.code} · {p.name}</option>)}
                </select></dd>
                <dt>File</dt>
                <dd><select className="converge-file" value={file} onChange={(e) => setFile(e.target.value)}>
                  {files?.length === 0 && <option value="">no IFC file in this project</option>}
                  {files?.map((f) => <option key={f.id} value={f.id} data-no-i18n>{f.fileName} · {f.status}</option>)}
                </select></dd>
                <dt>Version</dt>
                <dd><select className="converge-version" value={version} onChange={(e) => setVersion(Number(e.target.value))}>
                  {versions.map((v) => (
                    <option key={v.versionNo} value={v.versionNo} data-no-i18n>
                      {[v.revisionCode, v.uploadedAt?.slice(0, 10), v.uploadedBy, sizeText(v.sizeBytes), v.isCurrent ? '●' : ''].filter(Boolean).join(' · ')}
                    </option>
                  ))}
                </select></dd>
              </dl>
              <p className="muted small">The file is saved on this computer, its checksum checked, then the Import IFC dialog opens: bring it in recognised, as references or linked. Saving a newer version over a linked file updates the link.</p>
            </>
          )}
          {error && <p className="error small" data-testid="converge-error" data-no-i18n>{error}</p>}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" onClick={close}>Cancel</button>
          {!checking && !account && <SignInButton form={form} />}
          {account && <button type="button" className="btn primary" data-action="converge-get" disabled={busy || !chosen || !version} onClick={() => void doGet()}>{busy ? 'Getting…' : 'Get…'}</button>}
        </footer>
      </div>
    </div>
  );
}
