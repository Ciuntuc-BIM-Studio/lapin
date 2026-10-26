/** The top bar and the ribbon: the tabs come from the product's modules. */

import { useState } from 'react';
import { Moon, Search, Sun } from 'lucide-react';
import { HelpMenu } from './HelpMenu';
import { useUi } from '../app/store';
import { activate, ribbonTabs } from '../platform/modules';
import { redo, undo, useSession } from '../engine/session';
import { exportIfc, importDxf, importIfc, newProject, openProject, saveProject } from '../engine/files';

function FileMenu() {
  const [open, setOpen] = useState(false);
  const run = (f: () => unknown) => () => { setOpen(false); void f(); };
  return (
    <span className="menu-host">
      <button type="button" className={`tab${open ? ' on' : ''}`} onClick={() => setOpen(!open)}>File</button>
      {open && (
        <div className="menu" role="menu" onMouseLeave={() => setOpen(false)}>
          <button type="button" role="menuitem" onClick={run(newProject)}>New project<kbd>Ctrl N</kbd></button>
          <button type="button" role="menuitem" onClick={run(() => openProject())}>Open…<kbd>Ctrl O</kbd></button>
          <button type="button" role="menuitem" onClick={run(() => saveProject())}>Save<kbd>Ctrl S</kbd></button>
          <button type="button" role="menuitem" onClick={run(() => saveProject(true))}>Save as…<kbd>Ctrl ⇧ S</kbd></button>
          <button type="button" role="menuitem" onClick={run(() => importIfc())}>Import IFC…</button>
          <button type="button" role="menuitem" onClick={run(() => exportIfc())}>Export IFC…</button>
          <button type="button" role="menuitem" onClick={run(() => importDxf())}>Import DXF…</button>
          <button type="button" role="menuitem" onClick={run(() => useUi.setState({ screen: 'start' }))}>Start screen</button>
        </div>
      )}
    </span>
  );
}

export function Ribbon() {
  const tabs = ribbonTabs();
  const tabId = useUi((s) => s.tab) ?? tabs[0]?.id;
  const tool = useUi((s) => s.tool);
  const theme = useUi((s) => s.theme);
  const doc = useSession((s) => s.doc);
  const tab = tabs.find((t) => t.id === tabId) ?? tabs[0];
  return (
    <header className="topbar">
      <div className="tabs-row">
        <button type="button" className="brand" onClick={() => useUi.setState({ screen: 'start' })} title="Start screen">Lapin</button>
        <FileMenu />
        {tabs.map((t) => (
          <button type="button" key={t.id} className={`tab${t.id === tab?.id ? ' on' : ''}`} onClick={() => useUi.setState({ tab: t.id })}>{t.label}</button>
        ))}
        <span className="grow" />
        <button type="button" className="search"><Search size={14} />Commands… <kbd>Ctrl K</kbd></button>
        <button type="button" className="icon" disabled={!doc?.canUndo} onClick={() => void undo()} title={doc?.undoLabel ? `Undo ${doc.undoLabel} (Ctrl Z)` : 'Undo'}>↶</button>
        <button type="button" className="icon" disabled={!doc?.canRedo} onClick={() => void redo()} title={doc?.redoLabel ? `Redo ${doc.redoLabel} (Ctrl ⇧ Z)` : 'Redo'}>↷</button>
        <button type="button" className="icon" title="Theme" onClick={() => useUi.setState({ theme: theme === 'paper' ? 'graphite' : 'paper' })}>{theme === 'paper' ? <Moon size={15} /> : <Sun size={15} />}</button>
        <HelpMenu />
      </div>
      {tab && (
        <div className="band" role="toolbar" aria-label={tab.label}>
          {tab.groups.map((g) => (
            <div className="group" key={g.label}>
              <div className="group-tools">
                {g.tools.map((tl) => (
                  <button type="button" key={tl.id} className={`tool${tool === tl.id ? ' on' : ''}`} onClick={() => activate(tl)} title={tl.shortcut ? `${tl.label} (${tl.shortcut})` : tl.label}>
                    <tl.icon size={22} strokeWidth={1.5} />
                    <span>{tl.label}</span>
                  </button>
                ))}
                {g.small && g.small.length > 0 && (
                  <div className="small-tools">
                    {g.small.map((tl) => (
                      <button type="button" key={tl.id} className={`tool small${tool === tl.id ? ' on' : ''}`} onClick={() => activate(tl)} title={tl.shortcut ? `${tl.label} (${tl.shortcut})` : tl.label}>
                        <tl.icon size={14} strokeWidth={1.6} /><span>{tl.label}</span>
                      </button>
                    ))}
                  </div>
                )}
              </div>
              <div className="group-label">{g.label}</div>
            </div>
          ))}
        </div>
      )}
    </header>
  );
}
