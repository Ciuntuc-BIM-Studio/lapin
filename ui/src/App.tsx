/**
 * Lapin on the BubbleBIM Pro engine (milestone M0): a ribbon with the wall
 * tool, undo and redo; the 3D view; the engine's status. The model lives in
 * the engine; the UI sends commands and draws what comes back.
 */

import { useState } from 'react';
import { BrickWall, FilePlus2, Redo2, Undo2 } from 'lucide-react';
import { ensureStorey, execute, newProject, redo, undo, useSession } from './engine/session';
import { Viewport3D, type PlanePick } from './viewport/Viewport3D';

export function App() {
  const connected = useSession((s) => s.connected);
  const doc = useSession((s) => s.doc);
  const error = useSession((s) => s.error);
  const info = useSession((s) => s.info);
  const elements = useSession((s) => s.render.size);
  const [tool, setTool] = useState<'wall' | null>(null);
  const [start, setStart] = useState<PlanePick | null>(null);

  const pick = (p: PlanePick) => {
    if (tool !== 'wall') return;
    if (!start) { setStart(p); return; }
    const from = start;
    void ensureStorey().then((storeyId) => storeyId && execute('building.addWall', { storeyId, x1: from.x, y1: from.y, x2: p.x, y2: p.y }));
    // The next wall starts where this one ended.
    setStart(p);
  };
  const stopTool = () => { setTool(null); setStart(null); };

  return (
    <div className="app" onKeyDown={(e) => { if (e.key === 'Escape') stopTool(); }} tabIndex={-1}>
      <header className="ribbon">
        <div className="brand">Lapin</div>
        <div className="group">
          <button type="button" onClick={() => void newProject()} disabled={!connected} title="New project"><FilePlus2 size={18} /><span>New</span></button>
        </div>
        <div className="group">
          <button type="button" className={tool === 'wall' ? 'active' : ''} disabled={!connected} title="Wall: click the start, then each end; Esc to stop"
            onClick={() => (tool === 'wall' ? stopTool() : setTool('wall'))}><BrickWall size={18} /><span>Wall</span></button>
        </div>
        <div className="group">
          <button type="button" onClick={() => void undo()} disabled={!doc?.canUndo} title={doc?.undoLabel ? `Undo ${doc.undoLabel}` : 'Undo'}><Undo2 size={18} /><span>Undo</span></button>
          <button type="button" onClick={() => void redo()} disabled={!doc?.canRedo} title={doc?.redoLabel ? `Redo ${doc.redoLabel}` : 'Redo'}><Redo2 size={18} /><span>Redo</span></button>
        </div>
      </header>
      <main className="main">
        <Viewport3D picking={tool !== null} onPick={pick} />
        {tool === 'wall' && <div className="hint">{start ? 'Click the wall’s end (Esc to stop)' : 'Click the wall’s start'}</div>}
        {error && <div className="error" role="alert">{error}</div>}
      </main>
      <footer className="status">
        <span className={connected ? 'ok' : 'off'}>{connected ? 'Engine connected' : 'Starting the engine…'}</span>
        <span>{elements} elements</span>
        {info && <span>BubbleBIM engine {info.engineVersion}</span>}
      </footer>
    </div>
  );
}
