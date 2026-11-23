/**
 * File → Speckle → Send… (docs/plan-speckle.md, S1): the model sent to a Speckle server as a new version of a model,
 * for Revit, Rhino, Power BI, Blender and Speckle's web viewer. The account (the server, Speckle's own by default, and a
 * personal access token kept in the system's secure store; Sign out forgets it), the project and the model (or a new
 * one), the version's message, what goes (the whole model, the model as the 3D view shows it, the selection); Send with
 * its progress, then the version's link, opened in the system's browser. Each element goes as a Speckle object keyed by
 * its GlobalId, so the next send updates the same objects.
 */

import { useEffect, useState } from 'react';
import { ExternalLink, X } from 'lucide-react';
import { useUi, notify } from '../../app/store';
import { backdropClose } from '../../shell/Dialog';
import { engine, useSession } from '../../engine/session';
import { projectTitle } from '../../engine/files';
import { begin, end } from '../../engine/ifcImport';
import { useLooks } from '../../platform/looks';
import { useClip3d } from '../../viewport/section3d';
import { listModels, listProjects, openVersion, restore, send, useSpeckle, type SendWhat, type SpeckleItem, type SpeckleSent } from './speckle';
import { message, SignedIn, SignInButton, SignInFields, useSignInForm, WIDE } from './SpeckleAccount';

const NEW_MODEL = '__new__';

export function SpeckleSendDialog() {
  const close = () => useUi.setState({ dialog: null });
  const account = useSpeckle((s) => s.account);
  const [checking, setChecking] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const form = useSignInForm(setError);
  const [projects, setProjects] = useState<SpeckleItem[]>([]);
  const [project, setProject] = useState<string>('');
  const [models, setModels] = useState<SpeckleItem[]>([]);
  const [model, setModel] = useState<string>(NEW_MODEL);
  const path = useSession((s) => s.doc?.path ?? null);
  const [modelName, setModelName] = useState(() => projectTitle(path));
  const [note, setNote] = useState('');
  const selected = useUi((s) => s.selected);
  const view3dId = useUi((s) => s.view3dId);
  const [what, setWhat] = useState<SendWhat>(() => (useUi.getState().selected.length > 0 ? 'selection' : 'model'));
  const [sent, setSent] = useState<SpeckleSent | null>(null);

  // The account the engine holds, or the one whose token the secure store keeps.
  useEffect(() => { void restore().finally(() => setChecking(false)); }, []);

  // Signed in: the projects, the last one sent to chosen again.
  useEffect(() => {
    if (!account) { setProjects([]); return; }
    let live = true;
    listProjects().then((list) => {
      if (!live) return;
      setProjects(list);
      const last = useSpeckle.getState().project;
      setProject(list.some((p) => p.id === last) ? last! : list[0]?.id ?? '');
    }).catch((e) => setError(message(e)));
    return () => { live = false; };
  }, [account]);

  // A project chosen: its models, the last one sent to chosen again, else a new one.
  useEffect(() => {
    if (!account || !project) { setModels([]); return; }
    let live = true;
    listModels(project).then((list) => {
      if (!live) return;
      setModels(list);
      const last = useSpeckle.getState().model;
      setModel(list.some((m) => m.id === last) ? last! : NEW_MODEL);
    }).catch((e) => setError(message(e)));
    return () => { live = false; };
  }, [account, project]);

  const doSend = async () => {
    setBusy(true); setError(null); setSent(null);
    begin('Sending to Speckle', 'Converting the model', () => { void engine.call('cancel.long', {}).catch(() => undefined); });
    try {
      const hidden = Object.entries(useLooks.getState().looks).filter(([, l]) => l.hidden).map(([id]) => id);
      const r = await send({
        projectId: project, modelId: model === NEW_MODEL ? null : model, modelName, message: note, what,
        viewId: view3dId, hidden, planes: useClip3d.getState().planes, ids: selected,
      });
      setSent(r);
      if (r.notes.length) notify(r.notes[0]);
    } catch (e) { setError(message(e)); } finally { end(); setBusy(false); }
  };

  const canSend = !!account && !!project && (model !== NEW_MODEL || modelName.trim().length > 0) && !(what === 'selection' && selected.length === 0);

  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog" role="dialog" aria-label="Send to Speckle" data-testid="speckle-send" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Send to Speckle</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {checking ? <p className="muted">Looking for your Speckle account…</p> : !account ? <SignInFields form={form} /> : (
            <>
              <SignedIn account={account} />
              <dl className="fields">
                <dt>Project</dt>
                <dd><select className="speckle-project" value={project} onChange={(e) => setProject(e.target.value)}>
                  {projects.length === 0 && <option value="">no project: make one in Speckle</option>}
                  {projects.map((p) => <option key={p.id} value={p.id} data-no-i18n>{p.name}</option>)}
                </select></dd>
                <dt>Model</dt>
                <dd><select className="speckle-model" value={model} onChange={(e) => setModel(e.target.value)}>
                  <option value={NEW_MODEL}>a new model…</option>
                  {models.map((m) => <option key={m.id} value={m.id} data-no-i18n>{m.name}</option>)}
                </select></dd>
                {model === NEW_MODEL && <>
                  <dt>Name</dt>
                  <dd><input className="speckle-model-name" style={WIDE} value={modelName} onChange={(e) => setModelName(e.target.value)} /></dd>
                </>}
                <dt>Message</dt>
                <dd><input className="speckle-message" style={WIDE} value={note} onChange={(e) => setNote(e.target.value)} placeholder="What changed in this version" /></dd>
                <dt>What</dt>
                <dd><select className="speckle-what" value={what} onChange={(e) => setWhat(e.target.value as SendWhat)}
                  title="As the view shows it: its storeys shown, its hidden elements and categories left out, the layers it shows, cut as it is cut">
                  <option value="model">the whole model</option>
                  <option value="view">as the 3D view shows it</option>
                  <option value="selection" disabled={selected.length === 0}>the selection</option>
                </select></dd>
              </dl>
              <p className="muted small">Each element goes as a Speckle object with its GlobalId, its surfaces in its materials' colours, and its properties: IFC class, type, storey, phase, layers, property sets and quantities. Sending again makes a new version of the same objects.</p>
              {sent && (
                <div className="speckle-sent" data-testid="speckle-sent">
                  <p>{`Sent: ${sent.elements} elements, as a new version.`}</p>
                  <button type="button" className="btn" data-action="speckle-open" onClick={() => void openVersion(sent.url).catch((e) => notify(message(e)))}><ExternalLink size={13} /> Open the version in Speckle</button>
                </div>
              )}
            </>
          )}
          {error && <p className="error small" data-testid="speckle-error" data-no-i18n>{error}</p>}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" onClick={close}>{sent ? 'Close' : 'Cancel'}</button>
          {!checking && !account && <SignInButton form={form} />}
          {account && <button type="button" className="btn primary" data-action="speckle-send" disabled={busy || !canSend} onClick={() => void doSend()}>{busy ? 'Sending…' : 'Send'}</button>}
        </footer>
      </div>
    </div>
  );
}
