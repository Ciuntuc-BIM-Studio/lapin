/**
 * Lapin's interface: the start screen, then the workspace — ribbon tabs from
 * the product's modules, the project browser, the plan or 3D view with
 * Atrium's HUD, the properties panel, the status bar.
 */

import { useEffect } from 'react';
import { X } from 'lucide-react';
import { notify, useUi, type ViewKind } from './app/store';
import { useShortcuts } from './app/keys';
import { useSession } from './engine/session';
import { projectTitle } from './engine/files';
import { dialog, modules, productInfo } from './platform/modules';
import { ProjectBrowser } from './shell/ProjectBrowser';
import { PropertiesPanel } from './shell/PropertiesPanel';
import { Ribbon } from './shell/Ribbon';
import { StartScreen } from './shell/StartScreen';
import { ViewportHud } from './shell/ViewportHud';
import { PlanView } from './viewport/PlanView';
import { Viewport3D } from './viewport/Viewport3D';
import { DrawingView } from './viewport/DrawingView';
import { SheetView } from './viewport/SheetView';
import { ScheduleView } from './viewport/ScheduleView';

function StatusBar() {
  const connected = useSession((s) => s.connected);
  const elements = useSession((s) => s.render.size);
  const doc = useSession((s) => s.doc);
  return (
    <footer className="status">
      <span className={connected ? 'ok' : 'off'}>{connected ? 'Engine connected' : 'Engine offline'}</span>
      <span>{projectTitle(doc?.path)}{doc?.dirty ? ' · unsaved' : ''}</span>
      <span>{elements} elements</span>
      <span>Units: m</span>
      <span className="grow" />
      <span className="mods">{productInfo.name} · {modules.map((m) => m.id).join(' · ')}</span>
    </footer>
  );
}

function Notice() {
  const notice = useUi((s) => s.notice);
  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => notify(null), 4000);
    return () => clearTimeout(t);
  }, [notice]);
  return notice ? <div className="notice" role="status">{notice}</div> : null;
}

function Workspace() {
  const view = useUi((s) => s.view);
  const drawingViewId = useUi((s) => s.drawingViewId);
  const drawingName = useSession((s) => (drawingViewId ? s.nodes.get(drawingViewId)?.name : undefined));
  const sheetId = useUi((s) => s.sheetId);
  const sheetName = useSession((s) => (sheetId ? s.nodes.get(sheetId)?.name : undefined));
  const scheduleId = useUi((s) => s.scheduleId);
  const scheduleName = useSession((s) => (scheduleId ? s.nodes.get(scheduleId)?.name : undefined));
  const error = useSession((s) => s.error);
  const open = useUi((s) => s.dialog);
  const Dialog = open ? dialog(open) : null;
  return (
    <div className="workspace">
      <Ribbon />
      <div className="body">
        <ProjectBrowser />
        <main className="views">
          <div className="view-tabs">
            {([['plan', 'Plan'], ['3d', '{3D}'], ...(drawingName ? [['drawing', drawingName]] : []), ...(sheetName ? [['sheet', sheetName]] : []), ...(scheduleName ? [['schedule', scheduleName]] : [])] as [ViewKind, string][]).map(([id, name]) => (
              <button type="button" key={id} className={`view-tab${view === id ? ' on' : ''}`} onClick={() => useUi.setState({ view: id })}>{name}<X size={11} /></button>
            ))}
          </div>
          <div className="view-area">
            {view === 'plan' ? <PlanView /> : view === 'drawing' ? <DrawingView /> : view === 'sheet' ? <SheetView /> : view === 'schedule' ? <ScheduleView /> : <Viewport3D />}
            <ViewportHud />
            {error && <div className="error" role="alert" onClick={() => useSession.setState({ error: null })}>{error}</div>}
            <Notice />
          </div>
        </main>
        <PropertiesPanel />
      </div>
      <StatusBar />
      {Dialog && <Dialog />}
    </div>
  );
}

export function App() {
  const screen = useUi((s) => s.screen);
  const theme = useUi((s) => s.theme);
  useShortcuts();
  useEffect(() => { document.documentElement.dataset.theme = theme; }, [theme]);
  return screen === 'start' ? <StartScreen /> : <Workspace />;
}
