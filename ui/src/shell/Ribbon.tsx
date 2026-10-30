/**
 * The top bar and the ribbon: the tabs come from the product's modules. The
 * ribbon is large tools with their names, one band of icons (Compact), or a
 * band that drops over the view from its tab and folds once a tool is
 * picked (Focus) — View → Interface. Tooltips show each tool's keys, read
 * from the shortcuts' registry.
 */

import { useEffect, useRef, useState } from 'react';
import { Moon, Redo2, Search, Sun, Undo2 } from 'lucide-react';
import { HelpMenu } from './HelpMenu';
import { useUi } from '../app/store';
import { activate, ribbonTabs } from '../platform/modules';
import { redo, undo, useSession } from '../engine/session';
import { exportIfc, importDxf, importIfc, newProject, openProject, saveProject, importFragments } from '../engine/files';
import { engineInBrowser } from '../engine/session';
import { isGuest, leave } from '../engine/share';
import { Participants } from './Sharing';
import { useInterface } from '../app/interface';
import { bindingText, hint, useBindings } from '../app/commands';
import type { RibbonTabDef, ToolDef } from '../platform/module';

/** A command's first binding, as a menu shows it. */
function Keys({ id }: { id: string }) {
  useBindings();
  const k = bindingText(id).split(', ')[0];
  return k ? <kbd data-no-i18n>{k}</kbd> : null;
}

/** The undo button's tooltip: what it undoes, and its keys. */
function undoTitle(verb: 'Undo' | 'Redo', label: string | null | undefined, keys: string): string {
  return `${verb}${label ? ` ${label}` : ''}${keys ? ` (${keys})` : ''}`;
}

/** Undo and Redo at the top of the File menu, with their keys. */
function UndoItems({ run }: { run: (f: () => unknown) => () => void }) {
  const doc = useSession((s) => s.doc);
  return (
    <>
      <button type="button" role="menuitem" disabled={!doc?.canUndo} onClick={run(undo)}>Undo<Keys id="app.undo" /></button>
      <button type="button" role="menuitem" disabled={!doc?.canRedo} onClick={run(redo)}>Redo<Keys id="app.redo" /></button>
      <div className="menu-sep" role="separator" />
    </>
  );
}

function FileMenu() {
  const [open, setOpen] = useState(false);
  const run = (f: () => unknown) => () => { setOpen(false); void f(); };
  // A guest works in the host's project: the host opens and saves it; a guest exports what they need, or leaves.
  if (isGuest()) return (
    <span className="menu-host">
      <button type="button" className={`tab${open ? ' on' : ''}`} onClick={() => setOpen(!open)}>File</button>
      {open && (
        <div className="menu" role="menu" onMouseLeave={() => setOpen(false)}>
          <UndoItems run={run} />
          <button type="button" role="menuitem" onClick={run(() => exportIfc())}>Export IFC…</button>
          <button type="button" role="menuitem" onClick={run(() => useUi.setState({ dialog: 'exportMesh' }))}>Export 3D…</button>
          <button type="button" role="menuitem" onClick={run(leave)}>Leave session</button>
        </div>
      )}
    </span>
  );
  return (
    <span className="menu-host">
      <button type="button" className={`tab${open ? ' on' : ''}`} onClick={() => setOpen(!open)}>File</button>
      {open && (
        <div className="menu" role="menu" onMouseLeave={() => setOpen(false)}>
          <UndoItems run={run} />
          <button type="button" role="menuitem" onClick={run(newProject)}>New project<Keys id="app.new" /></button>
          <button type="button" role="menuitem" onClick={run(() => openProject())}>Open…<Keys id="app.open" /></button>
          <button type="button" role="menuitem" onClick={run(() => saveProject())}>Save<Keys id="app.save" /></button>
          <button type="button" role="menuitem" onClick={run(() => saveProject(true))}>Save as…<Keys id="app.saveAs" /></button>
          <button type="button" role="menuitem" onClick={run(() => useUi.setState({ dialog: 'versions' }))}>Save version…</button>
          <button type="button" role="menuitem" onClick={run(() => importIfc())}>Import IFC…</button>
          <button type="button" role="menuitem" onClick={run(() => importIfc('link'))}>Link IFC…</button>
          <button type="button" role="menuitem" onClick={run(() => exportIfc())}>Export IFC…</button>
          <button type="button" role="menuitem" onClick={run(() => useUi.setState({ dialog: 'exportMesh' }))}>Export 3D…</button>
          <button type="button" role="menuitem" onClick={run(() => importDxf())}>Import DXF…</button>
          <button type="button" role="menuitem" onClick={run(() => importFragments())}>Import Fragments…</button>
          {!engineInBrowser && <button type="button" role="menuitem" onClick={run(() => useUi.setState({ dialog: 'share' }))}>Share session…</button>}
          <button type="button" role="menuitem" onClick={run(() => useUi.setState({ dialog: 'join' }))}>Join session…</button>
          <button type="button" role="menuitem" onClick={run(() => useUi.setState({ screen: 'start' }))}>Start screen</button>
        </div>
      )}
    </span>
  );
}

/** The band of the tab: large tools with their names and keys, or icons only. */
function Band({ tab, icons, onPicked, drop }: { tab: RibbonTabDef; icons: boolean; onPicked?: () => void; drop?: boolean }) {
  const tool = useUi((s) => s.tool);
  useBindings();
  const press = (tl: ToolDef) => { activate(tl); onPicked?.(); };
  if (icons) return (
    <div className={`band icons${drop ? ' drop' : ''}`} role="toolbar" aria-label={tab.label}>
      {tab.groups.map((g, gi) => (
        <div className="group-inline" key={g.label} role="group" aria-label={g.label}>
          {gi > 0 && <span className="group-rule" aria-hidden />}
          {[...g.tools, ...(g.small ?? [])].map((tl) => (
            <button type="button" key={tl.id} className={`tool icon-only${tool === tl.id ? ' on' : ''}`} onClick={() => press(tl)} title={hint(tl.id, tl.label, g.label)} aria-label={tl.label}>
              <tl.icon size={18} strokeWidth={1.5} />
            </button>
          ))}
        </div>
      ))}
    </div>
  );
  return (
    <div className="band" role="toolbar" aria-label={tab.label}>
      {tab.groups.map((g) => (
        <div className="group" key={g.label}>
          <div className="group-tools">
            {g.tools.map((tl) => {
              const keys = bindingText(tl.id);
              return (
                <button type="button" key={tl.id} className={`tool${tool === tl.id ? ' on' : ''}`} onClick={() => press(tl)} title={hint(tl.id, tl.label)}>
                  <tl.icon size={22} strokeWidth={1.5} />
                  <span>{tl.label}</span>
                  {keys && <kbd className="tool-key" data-no-i18n>{keys.split(', ')[0]}</kbd>}
                </button>
              );
            })}
            {g.small && g.small.length > 0 && (
              <div className="small-tools">
                {g.small.map((tl) => (
                  <button type="button" key={tl.id} className={`tool small${tool === tl.id ? ' on' : ''}`} onClick={() => press(tl)} title={hint(tl.id, tl.label)}>
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
  );
}

export function Ribbon() {
  const tabs = ribbonTabs();
  const tabId = useUi((s) => s.tab) ?? tabs[0]?.id;
  const theme = useUi((s) => s.theme);
  const doc = useSession((s) => s.doc);
  const mode = useInterface((s) => s.parts.ribbon);
  const tab = tabs.find((t) => t.id === tabId) ?? tabs[0];
  // Focus: the band drops from its tab, over the view, and folds after a tool, on Escape or a click elsewhere.
  const [dropped, setDropped] = useState(false);
  const host = useRef<HTMLElement>(null);
  useEffect(() => {
    if (!dropped) return;
    const away = (e: MouseEvent) => { if (!host.current?.contains(e.target as Node)) setDropped(false); };
    const esc = (e: KeyboardEvent) => { if (e.key === 'Escape') setDropped(false); };
    window.addEventListener('mousedown', away);
    window.addEventListener('keydown', esc, true);
    return () => { window.removeEventListener('mousedown', away); window.removeEventListener('keydown', esc, true); };
  }, [dropped]);
  useEffect(() => { if (mode !== 'dropdown') setDropped(false); }, [mode]);
  const pickTab = (id: string) => {
    if (mode === 'dropdown') setDropped(!(dropped && id === tab?.id));
    useUi.setState({ tab: id });
  };
  const undoKeys = bindingText('app.undo'), redoKeys = bindingText('app.redo'), paletteKeys = bindingText('app.palette');
  return (
    <header className={`topbar ribbon-${mode}`} ref={host}>
      <div className="tabs-row">
        <button type="button" className="brand" onClick={() => !isGuest() && useUi.setState({ screen: 'start' })} title="Start screen">Lapin</button>
        <FileMenu />
        {tabs.map((t) => (
          <button type="button" key={t.id} className={`tab${t.id === tab?.id ? ' on' : ''}${mode === 'dropdown' && dropped && t.id === tab?.id ? ' open' : ''}`} onClick={() => pickTab(t.id)}>{t.label}</button>
        ))}
        <span className="grow" />
        <Participants />
        <button type="button" className="search" onClick={() => useUi.setState({ dialog: 'palette' })}><Search size={14} />Commands… {paletteKeys && <kbd data-no-i18n>{paletteKeys.split(', ')[0]}</kbd>}</button>
        <span className="undo-redo" role="group" aria-label="Undo and redo">
          <button type="button" className="undo-btn" disabled={!doc?.canUndo} onClick={() => void undo()} title={undoTitle('Undo', doc?.undoLabel, undoKeys)} aria-label="Undo"><Undo2 size={16} strokeWidth={1.8} /><span className="undo-text">Undo</span></button>
          <button type="button" className="undo-btn" disabled={!doc?.canRedo} onClick={() => void redo()} title={undoTitle('Redo', doc?.redoLabel, redoKeys)} aria-label="Redo"><Redo2 size={16} strokeWidth={1.8} /><span className="undo-text">Redo</span></button>
        </span>
        <button type="button" className="icon" title="Theme" onClick={() => useUi.setState({ theme: theme === 'paper' ? 'graphite' : 'paper' })}>{theme === 'paper' ? <Moon size={15} /> : <Sun size={15} />}</button>
        <HelpMenu />
      </div>
      {tab && mode === 'large' && <Band tab={tab} icons={false} />}
      {tab && mode === 'icons' && <Band tab={tab} icons />}
      {tab && mode === 'dropdown' && dropped && <Band tab={tab} icons drop onPicked={() => setDropped(false)} />}
    </header>
  );
}
