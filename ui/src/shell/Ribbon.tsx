/**
 * The top bar and the ribbon: the tabs come from the product's modules. The
 * ribbon is large tools with their names, one band of icons (Compact), or a
 * band that drops over the view from its tab and folds once a tool is
 * picked (Focus) — View → Interface. Tooltips show each tool's keys, read
 * from the shortcuts' registry.
 */

import { quickToggleTheme, setPrefs, usePrefs } from '../app/prefs';
import { Fragment, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { ChevronDown, FolderOpen, Moon, Printer, Redo2, Save, Search, Sun, Undo2 } from 'lucide-react';
import { HelpMenu } from './HelpMenu';
import { BrandName } from './BrandMark';
import { useUi } from '../app/store';
import { ribbonTabs } from '../platform/modules';
import { redo, undo, useSession } from '../engine/session';
import { fileActions, GUEST_SECTIONS, HOST_SECTIONS, runFileAction, type FileSection } from './fileActions';
import { openProject, projectTitle, recentProjects } from '../engine/files';
import { isGuest } from '../engine/share';
import { Participants } from './Sharing';
import { MenuSub } from './MenuSub';
import { TabStrip, WorkspaceSwitch } from './TabStrip';
import { useInterface, visibleTabIds } from '../app/interface';
import { MY_TAB } from '../app/myTools';
import { MyToolsBand, toolDragProps, toolMenu } from './MyToolsBand';
import { pressTool } from './pulldown';
import { bindingText, hint, useBindings } from '../app/commands';
import { recordToolClick } from '../app/microTips';
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

/**
 * The quick-access group beside the brand: Open, Save, Undo, Redo and Publish, icons only (forum topic 257). Their tooltips
 * carry the keys and, for Undo and Redo, the step; a guest of a shared session has no Open, Save or Publish.
 */
function QuickAccess() {
  const doc = useSession((s) => s.doc);
  useBindings();
  const guest = isGuest();
  const keys = (id: string) => bindingText(id).split(', ')[0];
  const plain = (verb: string, id: string) => { const k = keys(id); return `${verb}${k ? ` (${k})` : ''}`; };
  return (
    <span className="quick-access" role="group" aria-label="Quick access">
      {!guest && <button type="button" className="qa-btn" onClick={() => void runFileAction('file.open')} title={plain('Open…', 'app.open')} aria-label="Open…"><FolderOpen size={15} strokeWidth={1.8} /></button>}
      {!guest && <button type="button" className="qa-btn" onClick={() => void runFileAction('file.save')} title={plain('Save', 'app.save')} aria-label="Save"><Save size={15} strokeWidth={1.8} /></button>}
      <button type="button" className="qa-btn" disabled={!doc?.canUndo} onClick={() => void undo()} title={undoTitle('Undo', doc?.undoLabel, keys('app.undo'))} aria-label="Undo"><Undo2 size={15} strokeWidth={1.8} /></button>
      <button type="button" className="qa-btn" disabled={!doc?.canRedo} onClick={() => void redo()} title={undoTitle('Redo', doc?.redoLabel, keys('app.redo'))} aria-label="Redo"><Redo2 size={15} strokeWidth={1.8} /></button>
      {!guest && <button type="button" className="qa-btn" onClick={() => void runFileAction('file.publish')} title="Publish…" aria-label="Publish…"><Printer size={15} strokeWidth={1.8} /></button>}
    </span>
  );
}

/** One File action as a menu item. */
function FileItem({ a, run }: { a: FileAction; run: (f: () => unknown) => () => void }) {
  return <button type="button" role="menuitem" data-menu={a.menu} onClick={run(a.run)}>{a.label}{a.keys && <Keys id={a.keys} />}</button>;
}

/** The recent projects, as the File menu's Open recent lists them. */
function RecentItems({ run }: { run: (f: () => unknown) => () => void }) {
  const paths = recentProjects();
  if (paths.length === 0) return <div className="menu-empty muted small">No recent projects</div>;
  return <>{paths.map((p) => <button type="button" role="menuitem" key={p} title={p} onClick={run(() => openProject(p))}><span data-no-i18n>{projectTitle(p)}</span></button>)}</>;
}

/** One section of the File menu: its heading and its entries, as the product has them (shell/fileActions.ts). */
function FileItems({ section, first, run }: { section: FileSection; first: boolean; run: (f: () => unknown) => () => void }) {
  const actions = fileActions();
  const shown = (ids: string[]) => ids.map((id) => actions.get(id)).filter((a): a is FileAction => Boolean(a) && (a!.shown?.() ?? true));
  const entries = section.ids.map((e) => {
    if (typeof e === 'string') { const a = shown([e])[0]; return a ? { key: e, node: <FileItem a={a} run={run} /> } : null; }
    if (e.recent) return { key: e.sub, node: <MenuSub label={e.sub}><RecentItems run={run} /></MenuSub> };
    const items = shown(e.ids ?? []);
    // A submenu with nothing shown here (Collaborate for a guest, Speckle in the browser) is left out.
    return items.length ? { key: e.sub, node: <MenuSub label={e.sub}>{items.map((a) => <FileItem key={a.id} a={a} run={run} />)}</MenuSub> } : null;
  }).filter((x): x is { key: string; node: JSX.Element } => x !== null);
  if (entries.length === 0) return null;
  return (
    <>
      {!first && <div className="menu-sep" role="separator" />}
      {section.head && <div className="menu-head">{section.head}</div>}
      {entries.map((x) => <Fragment key={x.key}>{x.node}</Fragment>)}
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
        <div className="menu" role="menu" onMouseLeave={() => setOpen(false)} onKeyDown={(e) => { if (e.key === 'Escape') setOpen(false); }}>
          {sections.map((sec, i) => <FileItems key={i} section={sec} first={i === 0} run={run} />)}
        </div>
      )}
    </span>
  );
}

/** A ribbon tool dragged onto a quick slot goes in it (decision 259); dragged onto the My tools tab, in My tools (decision 479); right-clicked, its menu offers My tools. */
const dragTool = (tl: ToolDef) => ({ ...toolDragProps(tl.id), onContextMenu: (e: React.MouseEvent) => toolMenu(e, tl) });

/**
 * Whether a large band is too wide for its window, so its small tools show their icons only (a tester's wish, forum topic 257).
 * The width the labels need is remembered while they are hidden: the labels come back once the window holds them again.
 */
function useTight(): [React.RefObject<HTMLDivElement>, boolean] {
  const el = useRef<HTMLDivElement>(null);
  const need = useRef(0);
  const [tight, setTight] = useState(false);
  useLayoutEffect(() => {
    const band = el.current;
    if (!band) return;
    const measure = () => {
      if (!tight) { if (band.scrollWidth > band.clientWidth + 1) { need.current = band.scrollWidth; setTight(true); } }
      else if (band.clientWidth >= need.current) setTight(false);
    };
    measure();
    if (typeof ResizeObserver === 'undefined') return;
    const ro = new ResizeObserver(measure);
    ro.observe(band);
    return () => ro.disconnect();
  }, [tight]);
  return [el, tight];
}

/** The band of the tab: large tools with their names and keys, or icons only. */
function Band({ tab: all, icons, onPicked, drop }: { tab: RibbonTabDef; icons: boolean; onPicked?: () => void; drop?: boolean }) {
  const tool = useUi((s) => s.tool);
  // A tool that works for some selections only (`enabled`) is asked again as the selection or the model changes.
  useUi((s) => s.selected);
  useSession((s) => s.version);
  const off = (tl: ToolDef) => (tl.enabled ? !tl.enabled() : false);
  useBindings();
  const [bandRef, tight] = useTight();
  // A group shown only while something holds (`shown`): drawn again when the preferences change (Developer extras).
  usePrefs();
  const tab = { ...all, groups: all.groups.filter((g) => g.shown?.() ?? true) };
  const press = (tl: ToolDef, el?: HTMLElement) => {
    pressTool(tl, el);
    if (!tl.items?.length) {
      recordToolClick(tl.id, tl.label, bindingText(tl.id).split(', ')[0] || tl.shortcut);
      onPicked?.();
    }
  };
  // A pulldown is lit while one of its tools is on, and marked by its arrow.
  const on = (tl: ToolDef) => tool === tl.id || !!tl.items?.some((x) => x.id === tool);
  const mark = (tl: ToolDef, size: number) => (tl.items?.length ? <ChevronDown className="pulldown-mark" size={size} strokeWidth={2} aria-hidden /> : null);
  if (icons) return (
    <div className={`band icons${drop ? ' drop' : ''}`} role="toolbar" aria-label={tab.label} data-i18n-ctx={tab.i18nCtx} onWheel={wheelAcross}>
      {tab.groups.map((g, gi) => (
        <div className="group-inline" key={g.label} role="group" aria-label={g.label}>
          {gi > 0 && <span className="group-rule" aria-hidden />}
          {[...g.tools, ...(g.small ?? [])].map((tl) => (
            <button type="button" key={tl.id} className={`tool icon-only${on(tl) ? ' on' : ''}`} disabled={off(tl)} onClick={(e) => press(tl, e.currentTarget)} {...dragTool(tl)} title={hint(tl.id, tl.label, g.label)} aria-label={tl.label} data-pulldown={tl.items?.length ? tl.id : undefined}>
              <tl.icon size={18} strokeWidth={1.5} />{mark(tl, 9)}
            </button>
          ))}
        </div>
      ))}
    </div>
  );
  return (
    <div className={`band${tight ? ' tight' : ''}`} ref={bandRef} role="toolbar" aria-label={tab.label} data-i18n-ctx={tab.i18nCtx} onWheel={wheelAcross}>
      {tab.groups.map((g) => (
        <div className="group" key={g.label}>
          <div className="group-tools">
            {g.tools.map((tl) => {
              const keys = bindingText(tl.id);
              return (
                <button type="button" key={tl.id} className={`tool${on(tl) ? ' on' : ''}`} disabled={off(tl)} onClick={(e) => press(tl, e.currentTarget)} {...dragTool(tl)} title={hint(tl.id, tl.label)} data-pulldown={tl.items?.length ? tl.id : undefined}>
                  <tl.icon size={22} strokeWidth={1.5} />
                  <span>{tl.label}{mark(tl, 10)}</span>
                  {keys && <kbd className="tool-key" data-no-i18n>{keys.split(', ')[0]}</kbd>}
                </button>
              );
            })}
            {g.small && g.small.length > 0 && (
              <div className="small-tools">
                {g.small.map((tl) => (
                  <button type="button" key={tl.id} className={`tool small${on(tl) ? ' on' : ''}`} disabled={off(tl)} onClick={(e) => press(tl, e.currentTarget)} {...dragTool(tl)} title={hint(tl.id, tl.label)} aria-label={tl.label} data-pulldown={tl.items?.length ? tl.id : undefined}>
                    <tl.icon size={14} strokeWidth={1.6} /><span>{tl.label}</span>{tl.items?.length ? <ChevronDown size={10} strokeWidth={2} aria-hidden /> : null}
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
  // Office's key tips: the large tools' shortcuts show only while Alt is held down alone (their tooltips always have them).
  const keysAlways = useInterface((i) => i.parts.keyTips === 'always');
  const [keytips, setKeytips] = useState(false);
  useEffect(() => {
    const down = (e: KeyboardEvent) => {
      if (e.key !== 'Alt') { setKeytips(false); return; }
      if (e.ctrlKey || e.metaKey || e.shiftKey || (e.target as HTMLElement | null)?.closest?.('input, select, textarea, [contenteditable=true]')) return;
      setKeytips(true);
    };
    const up = (e: KeyboardEvent) => { if (e.key === 'Alt') setKeytips(false); };
    const off = () => setKeytips(false);
    // Nothing is stopped or prevented here: Alt keeps meaning what views and grips make of it.
    window.addEventListener('keydown', down);
    window.addEventListener('keyup', up);
    window.addEventListener('blur', off);
    return () => { window.removeEventListener('keydown', down); window.removeEventListener('keyup', up); window.removeEventListener('blur', off); };
  }, []);
  const paletteKeys = bindingText('app.palette');
  return (
    <header className={`topbar ribbon-${mode}${keytips ? ' keytips' : ''}${keysAlways ? ' keys-always' : ''}`} ref={host}>
      <div className="tabs-row">
        <button type="button" className="brand" onClick={() => !isGuest() && useUi.setState({ screen: 'start' })} title="Start screen"><BrandName /></button>
        <QuickAccess />
        <FileMenu />
        <WorkspaceSwitch />
        <TabStrip tabs={tabs} current={tab?.id} open={mode === 'dropdown' && dropped} onPick={pickTab} />
        <Participants />
        <button type="button" className="search" onClick={() => useUi.setState({ dialog: 'palette' })}><Search size={14} />Commands… {paletteKeys && <kbd data-no-i18n>{paletteKeys.split(', ')[0]}</kbd>}</button>
        <button type="button" className="icon" title="Theme" onClick={() => { const next = quickToggleTheme(theme, usePrefs.getState().lastLight); setPrefs({ theme: next }); useUi.setState({ theme: next }); }}>{theme !== 'graphite' ? <Moon size={15} /> : <Sun size={15} />}</button>
        <HelpMenu />
      </div>
      {tab && mode === 'large' && (tab.id === MY_TAB ? <MyToolsBand icons={false} /> : <Band tab={tab} icons={false} />)}
      {tab && mode === 'icons' && (tab.id === MY_TAB ? <MyToolsBand icons /> : <Band tab={tab} icons />)}
      {tab && mode === 'dropdown' && dropped && (tab.id === MY_TAB ? <MyToolsBand icons drop onPicked={() => setDropped(false)} /> : <Band tab={tab} icons drop onPicked={() => setDropped(false)} />)}
    </header>
  );
}
