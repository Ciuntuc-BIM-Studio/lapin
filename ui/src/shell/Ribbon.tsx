/**
 * The top bar and the ribbon: the tabs come from the product's modules. The
 * ribbon is large tools with their names, one band of icons (Compact), or a
 * band that drops over the view from its tab and folds once a tool is
 * picked (Focus) — View → Interface. Tooltips show each tool's keys, read
 * from the shortcuts' registry.
 */

import { setPrefs, usePrefs } from '../app/prefs';
import { useEffect, useRef, useState } from 'react';
import { Moon, Redo2, Search, Sun, Undo2 } from 'lucide-react';
import { HelpMenu } from './HelpMenu';
import { BrandName } from './BrandMark';
import { useUi } from '../app/store';
import { activate, ribbonTabs } from '../platform/modules';
import { redo, undo, useSession } from '../engine/session';
import { fileActions, GUEST_SECTIONS, HOST_SECTIONS, type FileSection } from './fileActions';
import { isGuest } from '../engine/share';
import { Participants } from './Sharing';
import { TabStrip, WorkspaceSwitch } from './TabStrip';
import { useInterface, visibleTabIds } from '../app/interface';
import { MY_TAB } from '../app/myTools';
import { MyToolsBand, toolDragProps, toolMenu } from './MyToolsBand';
import { bindingText, hint, useBindings } from '../app/commands';
import type { FileAction, RibbonTabDef, ToolDef } from '../platform/module';

/**
 * A band wider than the window scrolls across with the mouse wheel too, not only by its scrollbar (a tester's wish, #89): the
 * wheel's turn (vertical) moves it sideways while it has somewhere to go; a trackpad's sideways swipe works as it did.
 */
export function wheelAcross(e: React.WheelEvent<HTMLElement>): void {
  const el = e.currentTarget;
  if (el.scrollWidth <= el.clientWidth + 1 || Math.abs(e.deltaY) <= Math.abs(e.deltaX)) return;
  // The band does not scroll up and down, so nothing else takes the wheel (React's wheel listener is passive: no preventDefault).
  el.scrollLeft += e.deltaY;
}

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

/** One section of the File menu: its heading and its actions, as the product has them (shell/fileActions.ts). */
function FileItems({ section, first, run }: { section: FileSection; first: boolean; run: (f: () => unknown) => () => void }) {
  const actions = fileActions();
  const shown = section.ids.map((id) => actions.get(id)).filter((a): a is FileAction => Boolean(a) && (a!.shown?.() ?? true));
  return (
    <>
      {!first && <div className="menu-sep" role="separator" />}
      {section.head && <div className="menu-head">{section.head}</div>}
      {shown.map((a) => (
        <button type="button" role="menuitem" key={a.id} data-menu={a.menu} onClick={run(a.run)}>{a.label}{a.keys && <Keys id={a.keys} />}</button>
      ))}
    </>
  );
}

function FileMenu() {
  const [open, setOpen] = useState(false);
  const run = (f: () => unknown) => () => { setOpen(false); void f(); };
  const sections = isGuest() ? GUEST_SECTIONS : HOST_SECTIONS;
  return (
    <span className="menu-host">
      <button type="button" className={`tab${open ? ' on' : ''}`} onClick={() => setOpen(!open)}>File</button>
      {open && (
        <div className="menu" role="menu" onMouseLeave={() => setOpen(false)}>
          <UndoItems run={run} />
          {sections.map((sec, i) => <FileItems key={i} section={sec} first={i === 0} run={run} />)}
        </div>
      )}
    </span>
  );
}

/** A ribbon tool dragged onto a quick slot goes in it (decision 259); dragged onto the My tools tab, in My tools (decision 479); right-clicked, its menu offers My tools. */
const dragTool = (tl: ToolDef) => ({ ...toolDragProps(tl.id), onContextMenu: (e: React.MouseEvent) => toolMenu(e, tl) });

/** The band of the tab: large tools with their names and keys, or icons only. */
function Band({ tab: all, icons, onPicked, drop }: { tab: RibbonTabDef; icons: boolean; onPicked?: () => void; drop?: boolean }) {
  const tool = useUi((s) => s.tool);
  useBindings();
  // A group shown only while something holds (`shown`): drawn again when the preferences change (Developer extras).
  usePrefs();
  const tab = { ...all, groups: all.groups.filter((g) => g.shown?.() ?? true) };
  const press = (tl: ToolDef) => { activate(tl); onPicked?.(); };
  if (icons) return (
    <div className={`band icons${drop ? ' drop' : ''}`} role="toolbar" aria-label={tab.label} data-i18n-ctx={tab.i18nCtx} onWheel={wheelAcross}>
      {tab.groups.map((g, gi) => (
        <div className="group-inline" key={g.label} role="group" aria-label={g.label}>
          {gi > 0 && <span className="group-rule" aria-hidden />}
          {[...g.tools, ...(g.small ?? [])].map((tl) => (
            <button type="button" key={tl.id} className={`tool icon-only${tool === tl.id ? ' on' : ''}`} onClick={() => press(tl)} {...dragTool(tl)} title={hint(tl.id, tl.label, g.label)} aria-label={tl.label}>
              <tl.icon size={18} strokeWidth={1.5} />
            </button>
          ))}
        </div>
      ))}
    </div>
  );
  return (
    <div className="band" role="toolbar" aria-label={tab.label} data-i18n-ctx={tab.i18nCtx} onWheel={wheelAcross}>
      {tab.groups.map((g) => (
        <div className="group" key={g.label}>
          <div className="group-tools">
            {g.tools.map((tl) => {
              const keys = bindingText(tl.id);
              return (
                <button type="button" key={tl.id} className={`tool${tool === tl.id ? ' on' : ''}`} onClick={() => press(tl)} {...dragTool(tl)} title={hint(tl.id, tl.label)}>
                  <tl.icon size={22} strokeWidth={1.5} />
                  <span>{tl.label}</span>
                  {keys && <kbd className="tool-key" data-no-i18n>{keys.split(', ')[0]}</kbd>}
                </button>
              );
            })}
            {g.small && g.small.length > 0 && (
              <div className="small-tools">
                {g.small.map((tl) => (
                  <button type="button" key={tl.id} className={`tool small${tool === tl.id ? ' on' : ''}`} onClick={() => press(tl)} {...dragTool(tl)} title={hint(tl.id, tl.label)}>
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

/** The My tools tab as the tab strip knows it; its band is MyToolsBand's. */
const MY_TAB_DEF: RibbonTabDef = { id: MY_TAB, label: 'My tools', groups: [], order: -1 };

export function Ribbon() {
  const workspace = useInterface((s) => s.workspace);
  const mine = useInterface((s) => s.mine);
  const hiddenTabs = useInterface((s) => s.hiddenTabs);
  // The workspace's tabs in its order (View → Interface…, the start screen); the current one gone, the first stands in.
  const all = ribbonTabs();
  const own = visibleTabIds({ workspace, mine, hiddenTabs }, all).map((id) => all.find((t) => t.id === id)!);
  // My tools (decision 479) stands first in every workspace; the tab the app opens on stays the first of the workspace.
  const tabs = [MY_TAB_DEF, ...own];
  const tabId = useUi((s) => s.tab) ?? own[0]?.id;
  const theme = useUi((s) => s.theme);
  const doc = useSession((s) => s.doc);
  const mode = useInterface((s) => s.parts.ribbon);
  const tab = tabs.find((t) => t.id === tabId) ?? own[0] ?? tabs[0];
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
        <button type="button" className="brand" onClick={() => !isGuest() && useUi.setState({ screen: 'start' })} title="Start screen"><BrandName /></button>
        <FileMenu />
        <WorkspaceSwitch />
        <TabStrip tabs={tabs} current={tab?.id} open={mode === 'dropdown' && dropped} onPick={pickTab} />
        <Participants />
        <button type="button" className="search" onClick={() => useUi.setState({ dialog: 'palette' })}><Search size={14} />Commands… {paletteKeys && <kbd data-no-i18n>{paletteKeys.split(', ')[0]}</kbd>}</button>
        <span className="undo-redo" role="group" aria-label="Undo and redo">
          <button type="button" className="undo-btn" disabled={!doc?.canUndo} onClick={() => void undo()} title={undoTitle('Undo', doc?.undoLabel, undoKeys)} aria-label="Undo"><Undo2 size={16} strokeWidth={1.8} /><span className="undo-text">Undo</span></button>
          <button type="button" className="undo-btn" disabled={!doc?.canRedo} onClick={() => void redo()} title={undoTitle('Redo', doc?.redoLabel, redoKeys)} aria-label="Redo"><Redo2 size={16} strokeWidth={1.8} /><span className="undo-text">Redo</span></button>
        </span>
        <button type="button" className="icon" title="Theme" onClick={() => { const next = theme === 'paper' ? 'graphite' : 'paper'; setPrefs({ theme: next }); useUi.setState({ theme: next }); }}>{theme === 'paper' ? <Moon size={15} /> : <Sun size={15} />}</button>
        <HelpMenu />
      </div>
      {tab && mode === 'large' && (tab.id === MY_TAB ? <MyToolsBand icons={false} /> : <Band tab={tab} icons={false} />)}
      {tab && mode === 'icons' && (tab.id === MY_TAB ? <MyToolsBand icons /> : <Band tab={tab} icons />)}
      {tab && mode === 'dropdown' && dropped && (tab.id === MY_TAB ? <MyToolsBand icons drop onPicked={() => setDropped(false)} /> : <Band tab={tab} icons drop onPicked={() => setDropped(false)} />)}
    </header>
  );
}
