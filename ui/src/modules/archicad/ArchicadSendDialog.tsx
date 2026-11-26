/**
 * File → Collaborate → Archicad: Send to Atrium… (docs/plan-archicad.md, A1): Archicad saves an IFC with the chosen translator, Lapin
 * puts it in the project's store on the project's Atrium server and makes its reference document there (a model, received), then
 * syncs the project's register. Needs the project connected to a server and signed in.
 */

import { useEffect, useMemo, useState } from 'react';
import { X } from 'lucide-react';
import { useUi, notify } from '../../app/store';
import { backdropClose } from '../../shell/Dialog';
import { DISCIPLINES } from '../../shell/browserSets';
import { need } from '../../engine/atrium';
import { historyStatus, serverOf, signedIn } from '../../engine/remote';
import { useLang, t } from '../../i18n';
import { folderNames, type FolderNode } from '../core/references/roleFolders';
import {
  defaultFileName, defaultTitle, GUIDE_URL, instanceLabel, instances, lastPort, pickInstance, pickTranslator, sendProblems, sendToServer, translators,
  type ArchicadInstance, type ArchicadTranslator, type ProposalOutcome, type SendStep,
} from './archicad';

const messageOf = (e: unknown) => (e instanceof Error ? e.message : String(e));
const STEPS: Record<SendStep, string> = {
  saving: 'Saving the IFC in Archicad…', uploading: 'Putting the file on the server…', creating: 'Making the reference document…', syncing: 'Updating the project\'s register…',
  proposing: 'Proposing the project information…',
};

export function ArchicadSendDialog() {
  const close = () => useUi.setState({ dialog: null });
  const lang = useLang((s) => s.lang);
  const [server, setServer] = useState<string | null | undefined>(undefined);
  const [project, setProject] = useState('');
  const [signed, setSigned] = useState(false);
  const [list, setList] = useState<ArchicadInstance[] | null>(null);
  const [port, setPort] = useState<number | null>(null);
  const [formats, setFormats] = useState<ArchicadTranslator[]>([]);
  const [translator, setTranslator] = useState('');
  const [title, setTitle] = useState('');
  const [fileEdited, setFileEdited] = useState(false);
  const [file, setFile] = useState('');
  const [folders, setFolders] = useState<{ id: string; text: string }[]>([]);
  const [folder, setFolder] = useState('');
  const [discipline, setDiscipline] = useState('');
  const [step, setStep] = useState<SendStep | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState<{ title: string; folder: string; proposal?: ProposalOutcome } | null>(null);
  const [proposeInfo, setProposeInfo] = useState(true);

  useEffect(() => {
    let live = true;
    historyStatus().then(async (s) => {
      if (!live) return;
      const url = serverOf(s);
      setServer(url);
      setProject(s?.remotes[0]?.project ?? '');
      setSigned(url ? await signedIn(url) : false);
    }).catch(() => { if (live) setServer(null); });
    instances().then((l) => { if (!live) return; setList(l); setPort(pickInstance(l, lastPort())?.port ?? null); }).catch((e) => { if (live) { setList([]); setError(messageOf(e)); } });
    return () => { live = false; };
  }, []);

  const chosen = list?.find((i) => i.port === port) ?? null;

  useEffect(() => {
    setFormats([]); setTranslator('');
    if (!chosen?.ok) return;
    let live = true;
    translators(chosen.port).then((l) => { if (live) { setFormats(l); setTranslator(pickTranslator(l)); } }).catch((e) => { if (live) setError(messageOf(e)); });
    return () => { live = false; };
  }, [chosen?.port, chosen?.ok]);

  // The title follows the Archicad project's name until it is typed over; the file follows the title.
  const [titleEdited, setTitleEdited] = useState(false);
  useEffect(() => { if (!titleEdited) setTitle(defaultTitle(chosen?.project)); }, [chosen?.project, titleEdited]);
  useEffect(() => { if (!fileEdited) setFile(defaultFileName(title)); }, [title, fileEdited]);

  useEffect(() => {
    if (!server || !project) return;
    let live = true;
    const l = lang === 'ro' ? 'ro' : 'en';
    need<{ folders: FolderNode[]; roleFolders?: Record<string, string> }>(server, 'GET', `atrium/1.0/projects/${encodeURIComponent(project)}/references/folders?lang=${l}`)
      .then((f) => {
        if (!live) return;
        const names = folderNames(f.folders ?? [], l);
        const all = [...names].map(([id, text]) => ({ id, text }));
        const models = f.roleFolders?.models ?? all[0]?.id ?? '';
        setFolders(all); setFolder((cur) => cur || models);
      })
      .catch(() => { if (live) setFolders([]); });
    return () => { live = false; };
  }, [server, project, lang]);

  const problems = useMemo(
    () => sendProblems({ server: server && signed ? server : null, port: chosen?.ok ? chosen.port : null, translator, title, folder, file }),
    [server, signed, chosen, translator, title, folder, file],
  );

  const doSend = async () => {
    if (!server || !chosen) return;
    setBusy(true); setError(null); setSent(null);
    try {
      const r = await sendToServer({ server, project, port: chosen.port, translator, title, folder, discipline, file, proposeInfo }, setStep);
      const text = folders.find((f) => f.id === folder)?.text ?? folder;
      setSent({ title: title.trim(), folder: text, proposal: r.proposal });
      notify('Sent to the server.');
    } catch (e) { setError(messageOf(e)); } finally { setBusy(false); setStep(null); }
  };

  const notConnected = server === null || (server !== undefined && !signed);

  return (
    <div className="dialog-backdrop" onMouseDown={backdropClose(close)}>
      <div className="dialog" role="dialog" aria-label="Send to Atrium" data-testid="archicad-send" onClick={(e) => e.stopPropagation()}>
        <header className="dialog-head"><span className="panel-title">Send to Atrium</span><button type="button" className="icon" onClick={close} title="Close"><X size={15} /></button></header>
        <div className="dialog-body">
          {server === undefined || list === null ? <p className="muted">Looking for Archicad and the server…</p> : (
            <>
              {server === null && <p data-testid="archicad-no-server">Connect the project to a server first: File → Collaborate…</p>}
              {server !== null && !signed && <p data-testid="archicad-signed-out">Sign in to the server first.</p>}
              {list.length === 0 ? (
                <p className="small">No Archicad found. <a href={GUIDE_URL} target="_blank" rel="noreferrer">How to set up the Archicad connector</a></p>
              ) : (
                <dl className="fields">
                  <dt>Archicad</dt>
                  <dd><select className="archicad-instance" value={port ?? ''} onChange={(e) => setPort(Number(e.target.value))}>
                    {list.map((i) => <option key={i.port} value={i.port} data-no-i18n>{instanceLabel(i, t('Untitled project'))}</option>)}
                  </select></dd>
                  <dt title="The IFC translator of Archicad's own settings that writes the file">IFC translator</dt>
                  <dd><select className="archicad-translator" value={translator} onChange={(e) => setTranslator(e.target.value)}>
                    {formats.map((f) => <option key={f.name} value={f.name} data-no-i18n>{f.name}</option>)}
                  </select></dd>
                  <dt>Title</dt>
                  <dd><input className="archicad-title" value={title} onChange={(e) => { setTitle(e.target.value); setTitleEdited(true); }} /></dd>
                  <dt>Folder</dt>
                  <dd><select className="archicad-folder" value={folder} onChange={(e) => setFolder(e.target.value)}>
                    {folders.map((f) => <option key={f.id} value={f.id} data-no-i18n>{f.text}</option>)}
                  </select></dd>
                  <dt>Discipline</dt>
                  <dd><input className="archicad-discipline" list="archicad-disciplines" value={discipline} onChange={(e) => setDiscipline(e.target.value)} />
                    <datalist id="archicad-disciplines">{DISCIPLINES.map((d) => <option key={d} value={d} />)}</datalist></dd>
                  <dt>File name</dt>
                  <dd><input className="archicad-file" value={file} onChange={(e) => { setFile(e.target.value); setFileEdited(true); }} /></dd>
                  <dt title="Archicad's Project Info (name, number, client, site, designer…) proposed to the project's information on the server; an owner applies it">Project information</dt>
                  <dd><label><input type="checkbox" className="archicad-propose-info" checked={proposeInfo} onChange={(e) => setProposeInfo(e.target.checked)} /> <span>Also propose the project information</span></label></dd>
                </dl>
              )}
              {chosen && !chosen.ok && <p className="error small" data-no-i18n>{chosen.problem}</p>}
              <p className="muted small">Archicad saves its model as an IFC with the chosen translator; Lapin puts the file on the project's server as a reference model. The demo version of Archicad may refuse to save.</p>
              {step && <p className="muted small" data-testid="archicad-step">{STEPS[step]}</p>}
              {sent && <p data-testid="archicad-sent"><span>Sent:</span> <b data-no-i18n>{sent.title}</b> <span>is in the folder</span> <b data-no-i18n>{sent.folder}</b> <span>on the server.</span></p>}
              {sent?.proposal && (sent.proposal.made
                ? <p className="small" data-testid="archicad-proposed"><span>Project information proposed:</span> <span data-no-i18n>#{sent.proposal.number}, {sent.proposal.changes}</span> <span>changes, for an owner to apply (Publishing → Project information).</span></p>
                : sent.proposal.reason === 'no_changes'
                  ? <p className="muted small" data-testid="archicad-proposed">Archicad's Project Info says nothing the server's project information lacks: nothing proposed.</p>
                  : <p className="error small" data-testid="archicad-proposed"><span>The project information was not proposed:</span> <span data-no-i18n>{sent.proposal.reason}</span></p>)}
            </>
          )}
          {error && <p className="error small" data-testid="archicad-error" data-no-i18n>{error}</p>}
        </div>
        <footer className="dialog-foot">
          <span className="grow" />
          <button type="button" onClick={close}>{sent ? 'Close' : 'Cancel'}</button>
          <button type="button" className="btn primary" data-action="archicad-send" disabled={busy || problems.length > 0 || notConnected} title={problems[0] ? t(problems[0]) : undefined} onClick={() => void doSend()}>{busy ? 'Sending…' : 'Send'}</button>
        </footer>
      </div>
    </div>
  );
}
