/**
 * File → Speckle → Receive… (docs/plan-speckle.md, S2): a version of a Speckle model into the project as references. The
 * account (as Send's), the project, the model and the version (the latest by default); where the objects go (on the storeys
 * their levels match, or all on the storey you are on) and where the version's coordinates start (the project base point,
 * as Send writes them, or its internal origin). Receive downloads the version, says what it holds, then brings it in as one
 * undo step; received again, the same model's references are updated in place, keeping their GlobalIds. A version sent from
 * this project (its objects are this project's elements) is offered as a copy, never over the elements themselves.
 * Storeys: by the version's levels, the missing storeys made from them (the default), or only the project's, or all on one.
 * Recognise elements (on by default, as the IFC import's recognition): the walls, floors, columns, beams, footings, doors and
 * windows whose bodies Lapin rebuilds within 1 mm or 0.5 % come in as native elements, the rest as references; the report says how many of each.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi, notify } from '../../app/store';
import { backdropClose } from '../../shell/Dialog';
import { engine } from '../../engine/session';
import { begin, end } from '../../engine/ifcImport';
import {
  listModels, listProjects, listVersions, receive, receivedSummary, restore, survey, useSpeckle,
  type ReceiveArgs, type ReceiveStoreys, type SpeckleItem, type SpeckleReceived, type SpeckleSurvey, type SpeckleVersion,
} from './speckle';
import { message, SignedIn, SignInButton, SignInFields, useSignInForm } from './SpeckleAccount';

const LATEST = '__latest__';

/** A version as the list names it: its message (else its id), when, and what sent it. */
function versionLabel(v: SpeckleVersion): string {
  const when = v.createdAt ? new Date(v.createdAt).toLocaleString() : '';
  return [v.message?.trim() || v.id, when, v.sourceApplication ?? ''].filter(Boolean).join(' · ');
}

export function SpeckleReceiveDialog() {
  const close = () => useUi.setState({ dialog: null });
  const account = useSpeckle((s) => s.account);
  const [checking, setChecking] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const form = useSignInForm(setError);
  const [projects, setProjects] = useState<SpeckleItem[]>([]);
  const [project, setProject] = useState('');
  const [models, setModels] = useState<SpeckleItem[]>([]);
  const [model, setModel] = useState('');
  const [versions, setVersions] = useState<SpeckleVersion[]>([]);
  const [version, setVersion] = useState(LATEST);
  const [storeys, setStoreys] = useState<ReceiveStoreys>('create');
  const [origin, setOrigin] = useState<'base' | 'internal'>('base');
  const [recognize, setRecognize] = useState(true);
  const [own, setOwn] = useState<SpeckleSurvey | null>(null);
  const [received, setReceived] = useState<SpeckleReceived | null>(null);

  useEffect(() => { void restore().finally(() => setChecking(false)); }, []);

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

  useEffect(() => {
    if (!account || !project) { setModels([]); return; }
    let live = true;
    listModels(project).then((list) => {
      if (!live) return;
      setModels(list);
      const last = useSpeckle.getState().model;
      setModel(list.some((m) => m.id === last) ? last! : list[0]?.id ?? '');
    }).catch((e) => setError(message(e)));
    return () => { live = false; };
  }, [account, project]);

  useEffect(() => {
    setVersion(LATEST); setOwn(null); setReceived(null);
    if (!account || !project || !model) { setVersions([]); return; }
    let live = true;
    listVersions(project, model).then((list) => { if (live) setVersions(list); }).catch((e) => setError(message(e)));
    return () => { live = false; };
  }, [account, project, model]);

  const args = (asCopy: boolean, versionId?: string | null): ReceiveArgs => ({
    projectId: project, modelId: model, versionId: versionId ?? (version === LATEST ? null : version), asCopy, origin, storeys, recognize,
    activeStoreyId: useUi.getState().storeyId,
  });

  /** Downloads and surveys the version, then receives it; a version of this project's own stops at the question. */
  const doReceive = async (asCopy: boolean) => {
    setBusy(true); setError(null); setReceived(null);
    begin('Receiving from Speckle', 'Downloading from Speckle', () => { void engine.call('cancel.long', {}).catch(() => undefined); });
    try {
      const s = own && asCopy ? own : await survey(args(asCopy));
      if (s.native > 0 && !asCopy) { setOwn(s); return; }
      setOwn(null);
      const r = await receive(args(asCopy, s.versionId));
      setReceived(r);
      notify(receivedSummary(r));
    } catch (e) { setError(message(e)); } finally { end(); setBusy(false); }
  };

  const skipped = received ? Object.entries(received.skipped) : [];
  const kept = received?.recognition ? Object.entries(received.recognition.kept) : [];

  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog" role="dialog" aria-label="Receive from Speckle" data-testid="speckle-receive" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Receive from Speckle</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
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
                  {models.length === 0 && <option value="">no model in this project</option>}
                  {models.map((m) => <option key={m.id} value={m.id} data-no-i18n>{m.name}</option>)}
                </select></dd>
                <dt>Version</dt>
                <dd><select className="speckle-version" value={version} onChange={(e) => { setVersion(e.target.value); setOwn(null); }}>
                  <option value={LATEST}>the latest</option>
                  {versions.map((v) => <option key={v.id} value={v.id} data-no-i18n>{versionLabel(v)}</option>)}
                </select></dd>
                <dt title="By the version's levels: each object on the storey at its level (5 cm), the missing storeys made from the version's levels with their names and elevations">Storeys</dt>
                <dd><select className="speckle-storeys" value={storeys} onChange={(e) => setStoreys(e.target.value as ReceiveStoreys)}>
                  <option value="create">by the version's levels, creating the missing ones</option>
                  <option value="levels">by the version's levels, only the project's storeys</option>
                  <option value="active">all on the current storey</option>
                </select></dd>
                <dt title="Send writes the model from the project base point; a model from another program may start at its internal origin">Origin</dt>
                <dd><select className="speckle-origin" value={origin} onChange={(e) => setOrigin(e.target.value as 'base' | 'internal')}>
                  <option value="base">the project base point</option>
                  <option value="internal">the internal origin</option>
                </select></dd>
                <dt title="Walls, floors, columns, beams, footings, doors and windows whose bodies Lapin rebuilds within 1 mm or 0.5 % become native elements; the rest stay references">Recognition</dt>
                <dd><label><input type="checkbox" className="speckle-recognize" checked={recognize} onChange={(e) => setRecognize(e.target.checked)} /> <span>Recognise elements</span></label></dd>
              </dl>
              <p className="muted small">The objects come in as references, with their surfaces, colours and properties, grouped by the model. Receiving a newer version updates them in place and keeps their GlobalIds; what the version no longer has is removed.</p>
              {recognize && <p className="muted small">An element becomes a native one only where its native body is the received one within 1 mm or 0.5 %, and where its own parameters (width, thickness, location line) agree with its body. A column or beam of a section Lapin has no parameters for takes its body's own; a door or window no configurator makes as it is stays a reference in its wall's native opening. Curtain walls stay references.</p>}
              {own && (
                <div className="speckle-own" data-testid="speckle-own">
                  <p>{`This version was sent from this project: ${own.native} of its objects are its own elements.`}</p>
                  <p className="muted small">They are already here. Received as a copy, the objects come in as references with ids of their own, beside the elements, which stay as they are.</p>
                </div>
              )}
              {received && (
                <div className="speckle-received" data-testid="speckle-received">
                  <p>{receivedSummary(received)}</p>
                  <dl className="fields small">
                    <dt>Added</dt><dd data-no-i18n>{received.added}</dd>
                    <dt>Updated</dt><dd data-no-i18n>{received.updated}</dd>
                    <dt>Unchanged</dt><dd data-no-i18n>{received.unchanged}</dd>
                    <dt>Removed</dt><dd data-no-i18n>{received.removed}</dd>
                    {received.shared > 0 && <><dt>Shared bodies</dt><dd data-no-i18n>{received.shared}</dd></>}
                    {received.lines > 0 && <><dt>With lines only</dt><dd data-no-i18n>{received.lines}</dd></>}
                    {received.levels && received.levels.made > 0 && <><dt>Storeys made</dt><dd data-no-i18n>{received.levels.made}</dd></>}
                    {received.recognition && <><dt>Recognised</dt><dd data-no-i18n>{received.recognition.recognized}</dd></>}
                    {received.recognition && <><dt>References</dt><dd data-no-i18n>{received.references ?? 0}</dd></>}
                  </dl>
                  {kept.length > 0 && <p className="muted small speckle-kept">Stayed references: <span data-no-i18n>{kept.map(([k, n]) => `${k} (${n})`).join('; ')}</span></p>}
                  {skipped.length > 0 && <p className="muted small">Left out: <span data-no-i18n>{skipped.map(([k, n]) => `${k} ${n}`).join(', ')}</span></p>}
                  {received.notes.map((n) => <p key={n} className="muted small">{n}</p>)}
                </div>
              )}
            </>
          )}
          {error && <p className="error small" data-testid="speckle-error" data-no-i18n>{error}</p>}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" onClick={close}>{received ? 'Close' : 'Cancel'}</button>
          {!checking && !account && <SignInButton form={form} />}
          {account && own && <button type="button" className="btn primary" data-action="speckle-receive-copy" disabled={busy} onClick={() => void doReceive(true)}>Receive as a copy</button>}
          {account && !own && <button type="button" className="btn primary" data-action="speckle-receive" disabled={busy || !project || !model} onClick={() => void doReceive(false)}>{busy ? 'Receiving…' : 'Receive'}</button>}
        </footer>
      </div>
    </div>
  );
}
