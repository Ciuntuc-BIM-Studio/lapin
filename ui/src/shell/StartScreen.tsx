/** The start screen, Atrium's way: the actions on the left; unsaved work to bring back, the templates to start from (decision 261) and the recent projects on the right. */

import { confirmAction } from './prompts';
import { useEffect, useState } from 'react';
import { ArrowLeft, CloudDownload, Feather, FileBox, FilePlus2, FolderOpen, Globe, History, LayoutTemplate, Server, Trash2, Users } from 'lucide-react';
import { JoinDialog } from './Sharing';
import { lazyPart } from '../platform/lazyPart';
import { OptimizeIfcDialog } from './OptimizeIfcDialog';
import { openOptimize } from '../engine/ifcOptimize';
import { brand, tagline } from '../platform/brand';
import { BrandName } from './BrandMark';
import { RoleSelector, WorkspaceSelect } from './WorkspacePicker';
import { thumbOf } from '../engine/recentThumbs';
import { savedTemplatePicture, startFrom, templates } from './templates';
import { ProjectTemplatesDialog } from './ProjectTemplatesDialog';
import { defaultTemplateOf, refreshTemplates, startFromTemplate, useTemplates } from '../engine/templates';
import { usePrefs } from '../app/prefs';
import { applyNewProjectStandard } from '../engine/newProjectStandard';
import { MaterialStandardChoice } from './MaterialStandardChoice';
import { engineInBrowser, useSession } from '../engine/session';
import { discardWork, enter, isDesktop, listRecovery, newProject, openProject, projectTitle, recentProjects, restoreWork, type RecoverableWork } from '../engine/files';
import { notify, useUi } from '../app/store';
import { openLink } from './support';
import { LANGUAGES, setLang, useLang, type Lang } from '../i18n';
import { CdeStart } from './CdeStart';
import { detectCde, useCde } from '../engine/cdeServer';

const when = (iso: string) => new Date(iso).toLocaleString(undefined, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });

const OpenFromServerDialog = lazyPart(() => import('../modules/core/RemoteDialog').then((m) => m.OpenFromServerDialog), { title: 'Open from server' });

/** The start screen: in front of a CDE server (engine/cdeServer.ts), the server's own until the user works offline. */
export function StartScreen() {
  const cde = useCde((s) => s.server);
  const offline = useCde((s) => s.offline);
  useEffect(() => { void detectCde(); }, []);
  if (cde && !offline) return <CdeStart />;
  return <LocalStart cde={Boolean(cde)} />;
}

/** A recent project's picture, kept when it was saved (engine/recentThumbs.ts); an icon while it has none. */
function RecentThumb({ path }: { path: string }) {
  const src = thumbOf(path);
  return <span className="thumb" aria-hidden="true">{src ? <img src={src} alt="" /> : <FileBox size={26} strokeWidth={1.4} />}</span>;
}

function LocalStart({ cde }: { cde: boolean }) {
  const lang = useLang((x) => x.lang);
  const roleCards = usePrefs((p) => p.roleCards);
  const connected = useSession((s) => s.connected);
  const doc = useSession((s) => s.doc);
  const [work, setWork] = useState<RecoverableWork[]>([]);
  const recent = recentProjects();
  useEffect(() => { if (connected) { void listRecovery().then(setWork); void refreshTemplates(); } }, [connected]);
  // The user's own project templates (decision 470), and the one File → New starts from.
  const saved = useTemplates((s) => s.list);
  const fromTemplate = defaultTemplateOf(usePrefs((p) => p.defaultTemplate), saved);
  const choosing = useUi((s) => s.dialog === 'projectTemplates');
  const fromSaved = async (id: string) => {
    if (doc?.dirty && !confirmAction('The project has unsaved changes. Start a new one anyway?')) return;
    try { await startFromTemplate(id); await applyNewProjectStandard(); enter(); } catch (e) { notify(e instanceof Error ? e.message : String(e)); }
  };
  // A project is open behind this screen: its button comes first, and Escape goes back to it too (app/keys.ts).
  const open = Boolean(doc && (doc.path || doc.canUndo));
  const back = () => useUi.setState({ screen: 'workspace' });
  const joining = useUi((s) => s.dialog === 'join');
  const cloning = useUi((s) => s.dialog === 'remoteClone');
  const optimizing = useUi((s) => s.dialog === 'optimizeIfc');
  return (
    <div className="start">
      <section className="start-left">
        <h1 className="start-brand"><BrandName big /></h1>
        <p className="muted">{tagline()}</p>
        {open && <button type="button" className="btn primary big" data-start="back" onClick={back}><ArrowLeft size={18} />Back to {projectTitle(doc?.path)}</button>}
        <button type="button" className={`btn big${open ? '' : ' primary'}`} disabled={!connected} onClick={() => void newProject()}><FilePlus2 size={18} />New project</button>
        <MaterialStandardChoice />
        {fromTemplate && <p className="muted small" data-start="default-template"><span>From the template</span> <span data-no-i18n>{fromTemplate.name}</span></p>}
        <button type="button" className="btn big" data-start="templates" disabled={!connected} onClick={() => useUi.setState({ dialog: 'projectTemplates' })}><LayoutTemplate size={18} />New from template…</button>
        <button type="button" className="btn big" disabled={!connected} onClick={() => void openProject()}><FolderOpen size={18} />Open project…</button>
        {isDesktop() && <button type="button" className="btn big" data-start="open-server" disabled={!connected} onClick={() => useUi.setState({ dialog: 'remoteClone' })}><CloudDownload size={18} />Open from server…</button>}
        {cde && <button type="button" className="btn big" data-start="cde-online" onClick={() => useCde.setState({ offline: false })}><Server size={18} />Server projects…</button>}
        <button type="button" className="btn big" onClick={() => useUi.setState({ dialog: 'join' })}><Users size={18} />Join session…</button>
        <button type="button" className="btn big" data-start="optimize-ifc" disabled={!connected} onClick={() => openOptimize()}><Feather size={18} />Optimize IFC…</button>
        {!connected && <p className="muted small">Starting the engine…</p>}
        <WorkspaceSelect />
        <p className="eyebrow">Start here</p>
        {brand.hint
          ? <p className="muted small">{brand.hint}</p>
          : <p className="muted small">Press WA to draw walls; every element is native IFC.</p>}
        {engineInBrowser && <>
          <p className="eyebrow">In the browser</p>
          <p className="muted small">Your project stays in this browser: nothing is sent anywhere. Save it to keep a file (Safari and Firefox save it as a download). Unsaved work is kept for you if the tab closes.</p>
          <p className="muted small">Not here, only in the desktop app: the AI bridge (MCP) and views in windows of their own. A large imported IFC file may not fit in the browser.</p>
        </>}
      </section>
      <section className="start-right">
        {roleCards && <RoleSelector />}
        {(templates().length > 0 || saved.length > 0) && <>
          <p className="eyebrow">Start from</p>
          <div className="templates" role="group" aria-label="Start from">
            {templates().map((t) => (
              <button type="button" key={t.id} className="template" data-template={t.id} disabled={!connected} onClick={() => void startFrom(t)}>
                {t.picture}
                <span className="template-name">{t.label}</span>
                <span className="muted small">{t.note}</span>
              </button>
            ))}
            {saved.map((t) => (
              <button type="button" key={`saved-${t.id}`} className="template" data-template={`saved:${t.id}`} disabled={!connected} onClick={() => void fromSaved(t.id)}>
                {savedTemplatePicture}
                <span className="template-name" data-no-i18n>{t.name}</span>
                <span className="muted small">Your template: its types, styles, storeys and sheets</span>
              </button>
            ))}
          </div>
        </>}
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
            <RecentThumb path={path} />
            <span className="recent-body">
              <span className="recent-name">{projectTitle(path)}</span>
              <span className="muted small">{path}</span>
            </span>
          </button>
        ))}
      </section>
      <footer className="start-foot muted small">
        <span>Lapin · Ciuntuc BIM Studio · <button type="button" className="link" onClick={() => openLink('forum')}>Forum</button> · <button type="button" className="link" onClick={() => openLink('community')}>Community on Skool</button> · <button type="button" className="link" onClick={() => openLink('site')}>lapinbim.com</button></span>
        <span>
          <label className="start-lang"><Globe size={14} aria-hidden /><select value={lang} aria-label="Language" onChange={(e) => setLang(e.target.value as Lang)}>
            {LANGUAGES.map((l) => <option key={l.id} value={l.id} data-no-i18n>{l.name}</option>)}
          </select></label>
          {' · '}Ctrl+N new · Ctrl+O open · Ctrl+S save
        </span>
      </footer>
      {joining && <JoinDialog />}
      {cloning && <OpenFromServerDialog />}
      {optimizing && <OptimizeIfcDialog />}
      {choosing && <ProjectTemplatesDialog />}
    </div>
  );
}
