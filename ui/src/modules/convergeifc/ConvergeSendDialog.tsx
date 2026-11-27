/**
 * File → Collaborate → Converge IFC: Send… (docs/plan-converge-ifc.md): the project exported as IFC and sent to a project of
 * a Converge IFC server, as a new file or a new version of one it holds. What it made is said with its revision.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from '../../app/store';
import { backdropClose } from '../../shell/Dialog';
import { useSession } from '../../engine/session';
import { projectTitle } from '../../engine/files';
import { listFiles, NEW_FILE, sendable, sendTarget, sendToServer, useConverge, type ConvergeFile, type ConvergeSent } from './convergeifc';
import { message, SignedIn, SignInButton, SignInFields, useAccountProjects, useSignInForm, WIDE } from './ConvergeAccount';

export function ConvergeSendDialog() {
  const close = () => useUi.setState({ dialog: null });
  const [error, setError] = useState<string | null>(null);
  const form = useSignInForm(setError);
  const { account, checking, projects, project, setProject } = useAccountProjects(setError);
  const path = useSession((s) => s.doc?.path ?? null);
  const [name, setName] = useState(() => `${projectTitle(path)}.ifc`);
  const [files, setFiles] = useState<ConvergeFile[]>([]);
  const [file, setFile] = useState(NEW_FILE);
  const [schema, setSchema] = useState('IFC4');
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState<ConvergeSent | null>(null);

  // A project chosen: its IFC files, the last one sent to (or the one with this name) chosen.
  useEffect(() => {
    setFiles([]); setFile(NEW_FILE);
    if (!account || !project) return;
    let live = true;
    listFiles(project).then((list) => {
      if (!live) return;
      const mine = sendable(list);
      setFiles(mine);
      setFile(sendTarget(mine, useConverge.getState().file, `${projectTitle(path)}.ifc`));
    }).catch((e) => setError(message(e)));
    return () => { live = false; };
  }, [account, project, path]);

  const chosen = projects.find((p) => p.id === project);
  const canSend = !!account && !!chosen?.canUpload && (file !== NEW_FILE || name.trim().length > 0);

  const doSend = async () => {
    setBusy(true); setError(null); setSent(null);
    try {
      const r = await sendToServer({ projectId: project, fileId: file, name, schema });
      // The list again before the news, so a new file is in it and chosen when the dialog says it was sent: the next send
      // adds a version to it. The news is said even if the list does not come.
      try { setFiles(sendable(await listFiles(project))); setFile(r.id); } finally { setSent(r); }
    } catch (e) { setError(message(e)); } finally { setBusy(false); }
  };

  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog" role="dialog" aria-label="Send to Converge IFC" data-testid="converge-send" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Send to Converge IFC</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {checking ? <p className="muted">Looking for your account…</p> : !account ? <SignInFields form={form} /> : (
            <>
              <SignedIn account={account} />
              <dl className="fields">
                <dt>Project</dt>
                <dd><select className="converge-project" value={project} onChange={(e) => { setProject(e.target.value); setSent(null); }}>
                  {projects.length === 0 && <option value="">no project on this server</option>}
                  {projects.map((p) => <option key={p.id} value={p.id} data-no-i18n>{p.code} · {p.name}</option>)}
                </select></dd>
                <dt>File</dt>
                <dd><select className="converge-file" value={file} onChange={(e) => { setFile(e.target.value); setSent(null); }}>
                  <option value={NEW_FILE}>a new file…</option>
                  {files.map((f) => <option key={f.id} value={f.id} data-no-i18n>{f.fileName} · {f.revisionCode} · {f.status}</option>)}
                </select></dd>
                {file === NEW_FILE && <>
                  <dt>Name</dt>
                  <dd><input className="converge-name" style={WIDE} value={name} onChange={(e) => setName(e.target.value)} /></dd>
                </>}
                <dt>Schema</dt>
                <dd><select className="converge-schema" value={schema} onChange={(e) => setSchema(e.target.value)}>
                  <option value="IFC4">IFC4: read everywhere</option>
                  <option value="IFC4X3">IFC4X3: terrain and infrastructure as IFC 4.3 names them</option>
                </select></dd>
              </dl>
              {chosen && !chosen.canUpload && <p className="error small">Your roles in this project do not allow sending files.</p>}
              <p className="muted small">The whole project goes as one IFC file. On the server it is work in progress until it is shared or published there.</p>
              {sent && <p data-testid="converge-sent">{sent.unchanged ? `Nothing sent: ${sent.fileName} is already like this on the server, at ${sent.revisionCode}.` : `Sent: ${sent.fileName}, now at ${sent.revisionCode}.`}</p>}
            </>
          )}
          {error && <p className="error small" data-testid="converge-error" data-no-i18n>{error}</p>}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" onClick={close}>{sent ? 'Close' : 'Cancel'}</button>
          {!checking && !account && <SignInButton form={form} />}
          {account && <button type="button" className="btn primary" data-action="converge-send" disabled={busy || !canSend} onClick={() => void doSend()}>{busy ? 'Sending…' : 'Send'}</button>}
        </footer>
      </div>
    </div>
  );
}
