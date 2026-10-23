/**
 * Lapin's interface (mock of the target layout): the start screen, then the
 * workspace — ribbon tabs from the product's modules, the project browser,
 * the views with Atrium's HUD, the properties panel, the status bar. The
 * wall tool already builds through the engine; the rest is a mock.
 */

import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { useUi } from './app/store';
import { ensureStorey, execute, useSession } from './engine/session';
import { modules, productInfo } from './platform/modules';
import { ProjectBrowser } from './shell/ProjectBrowser';
import { PropertiesPanel } from './shell/PropertiesPanel';
import { Ribbon } from './shell/Ribbon';
import { StartScreen } from './shell/StartScreen';
import { ViewportHud } from './shell/ViewportHud';
import { Viewport3D, type PlanePick } from './viewport/Viewport3D';

function StatusBar() {
  const connected = useSession((s) => s.connected);
  const elements = useSession((s) => s.render.size);
  return (
    <footer className="status">
      <span className={connected ? 'ok' : 'off'}>{connected ? 'Engine connected' : 'Engine offline'}</span>
      <span>{elements} elements</span>
      <span>Units: m</span>
      <span className="grow" />
      <span className="mods">{productInfo.name} · {modules.map((m) => m.id).join(' · ')}</span>
    </footer>
  );
}

function Workspace() {
  const tool = useUi((s) => s.tool);
  const view = useUi((s) => s.view);
  const error = useSession((s) => s.error);
  const [start, setStart] = useState<PlanePick | null>(null);
  useEffect(() => { if (tool !== 'wall') setStart(null); }, [tool]);
  const pick = (p: PlanePick) => {
    if (tool !== 'wall') return;
    if (!start) { setStart(p); return; }
    const from = start;
    void ensureStorey().then((storeyId) => storeyId && execute('building.addWall', { storeyId, x1: from.x, y1: from.y, x2: p.x, y2: p.y }));
    setStart(p);
  };
  const hint = tool === 'wall' ? (start ? 'Click the wall’s end · Esc to stop' : 'Click the wall’s start') : null;
  return (
    <div className="workspace">
      <Ribbon />
      <div className="body">
        <ProjectBrowser />
        <main className="views">
          <div className="view-tabs">
            {[['3d', '{3D}'], ['plan', 'Ground floor']].map(([id, name]) => (
              <button type="button" key={id} className={`view-tab${view === id ? ' on' : ''}`} onClick={() => useUi.setState({ view: id })}>{name}<X size={11} /></button>
            ))}
          </div>
          <div className="view-area">
            <Viewport3D picking={tool === 'wall'} onPick={pick} />
            <ViewportHud hint={hint} />
            {error && <div className="error" role="alert">{error}</div>}
          </div>
        </main>
        <PropertiesPanel />
      </div>
      <StatusBar />
    </div>
  );
}

export function App() {
  const screen = useUi((s) => s.screen);
  const theme = useUi((s) => s.theme);
  useEffect(() => { document.documentElement.dataset.theme = theme; }, [theme]);
  return screen === 'start' ? <StartScreen /> : <Workspace />;
}
