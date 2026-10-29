/** The start screen, Atrium's way: the actions on the left; unsaved work to bring back and the recent projects on the right. */

import { useEffect, useState } from 'react';
import { FilePlus2, FolderOpen, History, Trash2, Users } from 'lucide-react';
import { JoinDialog } from './Sharing';
import { modules, productInfo } from '../platform/modules';
import { engineInBrowser, useSession } from '../engine/session';
import { discardWork, listRecovery, newProject, openProject, projectTitle, recentProjects, restoreWork, type RecoverableWork } from '../engine/files';
import { useUi } from '../app/store';
import { openLink } from './support';
import { LANGUAGES, setLang, useLang } from '../i18n';

const when = (iso: string) => new Date(iso).toLocaleString(undefined, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });

export function StartScreen() {
  const lang = useLang((x) => x.lang);
  const connected = useSession((s) => s.connected);
  const doc = useSession((s) => s.doc);
  const [work, setWork] = useState<RecoverableWork[]>([]);
  const recent = recentProjects();
  useEffect(() => { if (connected) void listRecovery().then(setWork); }, [connected]);
  const back = () => useUi.setState({ screen: 'workspace' });
  const joining = useUi((s) => s.dialog === 'join');
  return (
    <div className="start">
      <section className="start-left">
        <h1 className="start-brand">Lapin</h1>
        <p className="muted">BIM authoring for architects and engineers</p>
        <button type="button" className="btn primary big" disabled={!connected} onClick={() => void newProject()}><FilePlus2 size={18} />New project</button>
        <button type="button" className="btn big" disabled={!connected} onClick={() => void openProject()}><FolderOpen size={18} />Open project…</button>
        <button type="button" className="btn big" onClick={() => useUi.setState({ dialog: 'join' })}><Users size={18} />Join session…</button>
        {doc && (doc.path || doc.canUndo) && <button type="button" className="btn big" onClick={back}>Back to {projectTitle(doc.path)}</button>}
        {!connected && <p className="muted small">Starting the engine…</p>}
        <p className="eyebrow">Start here</p>
        <p className="muted small">A new project starts with a ground floor and its plan. Draw walls with <kbd>WA</kbd>; every element is native IFC.</p>
        <p className="eyebrow">This edition</p>
        <p className="muted small">{productInfo.name}: {modules.map((m) => m.name).join(' · ')}</p>
        {engineInBrowser && <>
          <p className="eyebrow">In the browser</p>
          <p className="muted small">Your project stays in this browser: nothing is sent anywhere. Save it to keep a file (Safari and Firefox save it as a download). Unsaved work is kept for you if the tab closes.</p>
          <p className="muted small">Not here, only in the desktop app: the AI bridge (MCP) and views in windows of their own. A large imported IFC file may not fit in the browser.</p>
        </>}
      </section>
      <section className="start-right">
        {work.length > 0 && (
          <>
            <p className="eyebrow">Unsaved work</p>
            {work.map((w) => (
              <div key={w.file} className="recent recover">
                <History size={18} />
                <span className="recent-body">
                  <span className="recent-name">{w.path ? projectTitle(w.path) : 'A new project'}</span>
                  <span className="muted small">{w.edits} edits{w.lastEdit ? ` · last: ${w.lastEdit}` : ''} · {when(w.at)}</span>
                </span>
                <button type="button" className="btn primary" onClick={() => void restoreWork(w.file)}>Recover</button>
                <button type="button" className="icon" title="Discard" onClick={() => void discardWork(w.file).then(() => setWork(work.filter((x) => x.file !== w.file)))}><Trash2 size={14} /></button>
              </div>
            ))}
          </>
        )}
        <p className="eyebrow">Recent</p>
        {recent.length === 0 && <p className="muted small">{engineInBrowser ? 'Open a project from a file on this computer.' : 'Projects you open or save appear here.'}</p>}
        {recent.map((path) => (
          <button type="button" key={path} className="recent" disabled={!connected} onClick={() => void openProject(path)}>
            <span className="thumb" aria-hidden="true" />
            <span className="recent-body">
              <span className="recent-name">{projectTitle(path)}</span>
              <span className="muted small">{path}</span>
            </span>
          </button>
        ))}
      </section>
      <footer className="start-foot muted small">
        <span>Lapin · Ciuntuc BIM Studio · <button type="button" className="link" onClick={() => openLink('community')}>Community on Skool</button> · <button type="button" className="link" onClick={() => openLink('site')}>ciuntucbimstudio.ro</button></span>
        <span>
          {LANGUAGES.filter((l) => l.id !== lang).map((l) => (
            <span key={l.id}><button type="button" className="link" data-no-i18n onClick={() => setLang(l.id)}>{l.name}</button>{' · '}</span>
          ))}Ctrl+N new · Ctrl+O open · Ctrl+S save
        </span>
      </footer>
      {joining && <JoinDialog />}
    </div>
  );
}
