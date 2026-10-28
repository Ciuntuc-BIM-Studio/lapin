/**
 * Lapin's interface: the start screen, then the workspace — ribbon tabs from
 * the product's modules, the project browser, the plan or 3D view with
 * Atrium's HUD, the properties panel, the status bar.
 */

import { useEffect, useRef, useState } from 'react';
import { AppWindow, Columns2, FolderTree, Layers, ListTree, PanelLeftOpen, PanelRightOpen, Rows2, SlidersHorizontal, X } from 'lucide-react';
import { closeOverlay, columns, setPanelWidth, showOverlay, togglePanel, usePanels } from './app/panels';
import { useInterface } from './app/interface';
import { bindingText } from './app/commands';
import { closePane, closeTab, focusPane, notify, openTab, splitPane, tabKey, useUi, type ViewTab } from './app/store';
import { PaneContext } from './app/pane';
import { openViewWindow } from './app/windows';
import { useShortcuts } from './app/keys';
import { useSession } from './engine/session';
import { unitLabel, useStandards } from './engine/unitsStore';
import { openStandards } from './modules/core/standardsTab';
import { isDesktop, projectTitle } from './engine/files';
import { backgrounds, dialog, modules, productInfo } from './platform/modules';
import { ProjectBrowser } from './shell/ProjectBrowser';
import { PropertiesPanel } from './shell/PropertiesPanel';
import { Ribbon } from './shell/Ribbon';
import { StartScreen } from './shell/StartScreen';
import { useHudFacts, ViewportHud } from './shell/ViewportHud';
import { CadPrompt } from './viewport/cad/CommandLine';
import { InterfaceDialog } from './shell/InterfaceDialog';
import { ShortcutsDialog } from './shell/ShortcutsDialog';
import { CommandPalette } from './shell/CommandPalette';
import { SelectionCard } from './shell/SelectionCard';
import { PanelFold } from './shell/PanelFold';
import { ContextMenuHost } from './shell/ContextMenu';
import { PlanView } from './viewport/PlanView';
import { Viewport3D } from './viewport/Viewport3D';
import { DrawingView } from './viewport/DrawingView';
import { SheetView } from './viewport/SheetView';
import { ScheduleView } from './viewport/ScheduleView';
import { GuestBar, JoinDialog, JoinGate, PresenceReporter, ShareDialog } from './shell/Sharing';
import { isGuest } from './engine/share';
import { connect } from './engine/session';

/**
 * The status bar: the engine, the project, its elements and units. With the
 * command line in the status line (Compact, Focus) it is one line: the
 * mode, the prompt with what is typed and the live inputs, the switches,
 * the level and view (the HUD's), then the project.
 */
function StatusBar() {
  const units = useStandards((s) => s.units);
  const connected = useSession((s) => s.connected);
  const elements = useSession((s) => s.render.size);
  const doc = useSession((s) => s.doc);
  const parts = useInterface((s) => s.parts);
  const facts = useHudFacts();
  const merged = parts.commandLine === 'status';
  return (
    <footer className={`status${merged ? ' line' : ''}`} data-testid={merged ? 'command-line' : undefined}>
      {merged
        ? <span className={`status-mode ${connected ? 'ok' : 'off'}`} title={connected ? 'Engine connected' : 'Engine offline'}>{facts.mode}</span>
        : <span className={connected ? 'ok' : 'off'}>{connected ? 'Engine connected' : 'Engine offline'}</span>}
      {merged && <CadPrompt inline />}
      {parts.hud === 'status' && <span className="status-hud">{facts.level} <span className="accent">{facts.elevation}</span> · {facts.view}</span>}
      <span>{projectTitle(doc?.path)}{doc?.dirty ? ' · unsaved' : ''}</span>
      <span>{elements} elements</span>
      <button type="button" className="status-link" title="The project's units: change them in Drafting standards → Project" onClick={() => openStandards('project')}>Units: {unitLabel(units) || units}</button>
      {!merged && <><span className="grow" /><span className="mods">{productInfo.name} · {modules.map((m) => m.id).join(' · ')}</span></>}
    </footer>
  );
}

/** The engine has been unreachable for a while, or could not be restarted: said until it is back. */
function EngineNotice() {
  const problem = useSession((s) => s.engineProblem);
  return problem ? <div className="notice" role="alert" style={{ position: 'fixed', zIndex: 40 }}>{problem}</div> : null;
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

/** A pane's tabs: every view opened so far (one list for the workspace), the pane's own marked; split, close, or open it in a window of its own (desktop). */
function PaneTabs({ i }: { i: number }) {
  const pane = useUi((s) => s.panes[i]);
  const count = useUi((s) => s.panes.length);
  const split = useUi((s) => s.split);
  const role = useUi((s) => s.windowRole);
  const tabs = useUi((s) => s.viewTabs);
  const storeyId = useUi((s) => s.storeyId);
  const nodes = useSession((s) => s.nodes);
  if (!pane) return null;
  const label = (t: ViewTab): string => {
    switch (t.view) {
      case 'plan': return t.planViewId ? nodes.get(t.planViewId)?.name ?? 'Plan' : t.storeyId ? nodes.get(t.storeyId)?.name ?? 'Plan' : 'Plan';
      case '3d': return t.view3dId ? nodes.get(t.view3dId)?.name ?? '3D' : '{3D}';
      case 'drawing': return (t.drawingViewId && nodes.get(t.drawingViewId)?.name) || 'Drawing';
      case 'sheet': return (t.sheetId && nodes.get(t.sheetId)?.name) || 'Sheet';
      default: return (t.scheduleId && nodes.get(t.scheduleId)?.name) || 'Schedule';
    }
  };
  const mine = tabKey({ ...pane, storeyId: pane.view === 'plan' ? storeyId : null });
  const current = tabs.find((t) => tabKey(t) === mine);
  return (
    <div className="view-tabs">
      <div className="view-tab-strip">
        {tabs.map((t) => {
          const key = tabKey(t);
          return (
            <button type="button" key={key} className={`view-tab${key === mine ? ' on' : ''}`} title={label(t)} onClick={() => openTab(i, t)}>
              {label(t)}
              <span className="close" role="button" aria-label="Close" onClick={(e) => { e.stopPropagation(); closeTab(key); }}><X size={11} /></span>
            </button>
          );
        })}
      </div>
      <span className="grow" />
      {role === 'main' && count < 2 && (
        <>
          <button type="button" className="icon mini" title="Split: two views side by side" onClick={() => splitPane('row')}><Columns2 size={13} /></button>
          <button type="button" className="icon mini" title="Split: one view above the other" onClick={() => splitPane('column')}><Rows2 size={13} /></button>
        </>
      )}
      {role === 'main' && count >= 2 && (
        <>
          <button type="button" className="icon mini" title={split === 'row' ? 'One above the other' : 'Side by side'} onClick={() => splitPane(split === 'row' ? 'column' : 'row')}>{split === 'row' ? <Rows2 size={13} /> : <Columns2 size={13} />}</button>
          <button type="button" className="icon mini" title="Close this pane" onClick={() => closePane(i)}><X size={13} /></button>
        </>
      )}
      {role === 'main' && isDesktop() && !isGuest() && <button type="button" className="icon mini" title="Open this view in a window of its own (for a second screen)" onClick={() => void openViewWindow(pane, current ? label(current) : 'View')}><AppWindow size={13} /></button>}
    </div>
  );
}

/** One pane: its tabs, its view, and over the active one the HUD, errors and notices. */
function PaneBody({ i }: { i: number }) {
  const view = useUi((s) => s.panes[i]?.view);
  const active = useUi((s) => s.pane === i);
  const error = useSession((s) => s.error);
  return (
    <PaneContext.Provider value={i}>
      <div className={`pane${active ? ' on' : ''}`} onPointerDownCapture={() => focusPane(i)} data-testid={`pane-${i}`}>
        <PaneTabs i={i} />
        <div className="view-area">
          {view === 'plan' ? <PlanView /> : view === 'drawing' ? <DrawingView /> : view === 'sheet' ? <SheetView /> : view === 'schedule' ? <ScheduleView /> : <Viewport3D />}
          {active && <ViewportHud />}
          {active && <SelectionCard />}
          {active && error && <div className="error" role="alert" onClick={() => useSession.setState({ error: null })}>{error}</div>}
          {active && <Notice />}
        </div>
      </div>
    </PaneContext.Provider>
  );
}

/** The panes of the workspace: one, or two with a separator to drag. */
function Panes() {
  const count = useUi((s) => s.panes.length);
  const split = useUi((s) => s.split);
  const [ratio, setRatio] = useState(0.5);
  const host = useRef<HTMLDivElement>(null);
  const onDown = (e: React.PointerEvent) => {
    (e.target as Element).setPointerCapture?.(e.pointerId);
    const move = (ev: PointerEvent) => {
      const r = host.current!.getBoundingClientRect();
      const t = split === 'row' ? (ev.clientX - r.left) / r.width : (ev.clientY - r.top) / r.height;
      setRatio(Math.max(0.15, Math.min(0.85, t)));
    };
    const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
  };
  return (
    <div ref={host} className={`panes split-${split}`}>
      <div className="pane-slot" style={{ flexBasis: count > 1 ? `${ratio * 100}%` : '100%' }}><PaneBody i={0} /></div>
      {count > 1 && <div className="pane-sep" onPointerDown={onDown} role="separator" />}
      {count > 1 && <div className="pane-slot" style={{ flexBasis: `${(1 - ratio) * 100}%` }}><PaneBody i={1} /></div>}
    </div>
  );
}

/** A separator beside a side panel: dragged, it sets the panel's width. */
function SideSep({ side }: { side: 'browser' | 'props' }) {
  const onDown = (e: React.PointerEvent) => {
    (e.target as Element).setPointerCapture?.(e.pointerId);
    const start = e.clientX;
    const width = side === 'browser' ? usePanels.getState().browserWidth : usePanels.getState().propsWidth;
    const move = (ev: PointerEvent) => setPanelWidth(side, width + (side === 'browser' ? ev.clientX - start : start - ev.clientX));
    const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
  };
  return <div className="side-sep" role="separator" aria-orientation="vertical" onPointerDown={onDown} onDoubleClick={() => togglePanel(side)} title="Drag to resize; double-click to fold or unfold" />;
}

/** A folded panel: a narrow rail; its icons open the panel over the view for a moment, the arrow unfolds it. */
function Rail({ side, label }: { side: 'browser' | 'props'; label: string }) {
  const hidden = usePanels((p) => p.railsHidden);
  const overlay = usePanels((p) => p.overlay);
  if (hidden) return <span />;
  const keys = bindingText(side === 'browser' ? 'app.toggleBrowser' : 'app.toggleProps').split(', ')[0];
  const pick = (tab?: 'project' | 'levels' | 'ifc') => (e: React.MouseEvent) => { e.stopPropagation(); showOverlay(side, tab); };
  const on = (tab?: string) => (overlay?.side === side && overlay.tab === tab ? ' on' : '');
  return (
    <aside className={`panel rail ${side}`} aria-label={label} data-rail={side}>
      <button type="button" className="icon mini" title={`Unfold the ${label.toLowerCase()}${keys ? ` (${keys})` : ''}`} onClick={() => togglePanel(side)}>{side === 'browser' ? <PanelLeftOpen size={15} /> : <PanelRightOpen size={15} />}</button>
      {side === 'browser' ? <>
        <button type="button" className={`icon rail-icon${on('project')}`} title="Project: views, sheets and schedules" onClick={pick('project')}><FolderTree size={16} /></button>
        <button type="button" className={`icon rail-icon${on('levels')}`} title="Levels" onClick={pick('levels')}><Layers size={16} /></button>
        <button type="button" className={`icon rail-icon${on('ifc')}`} title="IFC tree" onClick={pick('ifc')}><ListTree size={16} /></button>
      </> : <button type="button" className={`icon rail-icon${on(undefined)}`} title="Properties" onClick={pick()}><SlidersHorizontal size={16} /></button>}
      <span className="rail-label">{label}</span>
    </aside>
  );
}

/** A folded panel shown over the view for a moment: Escape or a click elsewhere puts it away. */
function PanelOverlay() {
  const overlay = usePanels((p) => p.overlay);
  const inset = usePanels((p) => (p.railsHidden ? 0 : 36));
  const browserWidth = usePanels((p) => p.browserWidth);
  const propsWidth = usePanels((p) => p.propsWidth);
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!overlay) return;
    const away = (e: MouseEvent) => {
      const t = e.target as Element;
      if (ref.current?.contains(t) || t.closest?.('[data-rail], .context-menu, .dialog-backdrop, .sel-card')) return;
      closeOverlay();
    };
    const esc = (e: KeyboardEvent) => { if (e.key === 'Escape' && !(e.target as HTMLElement)?.closest?.('input, select, textarea')) closeOverlay(); };
    window.addEventListener('mousedown', away);
    window.addEventListener('keydown', esc);
    return () => { window.removeEventListener('mousedown', away); window.removeEventListener('keydown', esc); };
  }, [overlay]);
  if (!overlay) return null;
  const browser = overlay.side === 'browser';
  return (
    <div ref={ref} className={`panel-overlay side ${browser ? 'left' : 'right'}`} style={browser ? { left: inset, width: browserWidth } : { right: inset, width: propsWidth }}>
      <PanelFold side={overlay.side} overlay />
      {browser ? <ProjectBrowser key={overlay.tab ?? "project"} show={overlay.tab} /> : <PropertiesPanel />}
    </div>
  );
}

/** The dialogs of working together (File → Share session…, Join session…), opened by the store's `dialog`. */
const shareDialogs: Record<string, () => React.JSX.Element | null> = { share: ShareDialog, join: JoinDialog, interface: InterfaceDialog, shortcuts: ShortcutsDialog, palette: CommandPalette };

function Workspace() {
  const open = useUi((s) => s.dialog);
  const Dialog = open ? dialog(open) ?? shareDialogs[open] ?? null : null;
  const panels = usePanels();
  const ui = useInterface();
  return (
    <div className={`workspace ui-${ui.preset} rows-${ui.parts.rows}`}>
      <Ribbon />
      <div className="body" style={{ gridTemplateColumns: columns(panels) }}>
        {panels.browserOpen ? <div className="side browser-side"><PanelFold side="browser" /><ProjectBrowser /></div> : <Rail side="browser" label="Project browser" />}
        {panels.browserOpen ? <SideSep side="browser" /> : <span />}
        <main className="views"><Panes /></main>
        {panels.propsOpen ? <SideSep side="props" /> : <span />}
        {panels.propsOpen ? <div className="side props-side"><PanelFold side="props" /><PropertiesPanel /></div> : <Rail side="props" label="Properties" />}
        <PanelOverlay />
      </div>
      <StatusBar />
      {Dialog && <Dialog />}
      <GuestBar />
      <PresenceReporter />
      {backgrounds().map((B, i) => <B key={i} />)}
    </div>
  );
}

/** A detached view window (desktop): one pane, no ribbon or panels; the tools stay in the main window. */
function ViewWindow() {
  const open = useUi((s) => s.dialog);
  const Dialog = open ? dialog(open) : null;
  return (
    <div className="workspace view-window">
      <main className="views"><PaneBody i={0} /></main>
      {Dialog && <Dialog />}
      {backgrounds().map((B, i) => <B key={i} />)}
    </div>
  );
}

export function App() {
  const screen = useUi((s) => s.screen);
  const theme = useUi((s) => s.theme);
  useShortcuts();
  useEffect(() => { document.documentElement.dataset.theme = theme; }, [theme]);
  const role = useUi((s) => s.windowRole);
  if (screen === 'join') return <JoinGate onJoin={() => void connect()} />;
  return <>{screen === 'start' ? <StartScreen /> : role === 'view' ? <ViewWindow /> : <Workspace />}<EngineNotice /><ContextMenuHost /></>;
}
