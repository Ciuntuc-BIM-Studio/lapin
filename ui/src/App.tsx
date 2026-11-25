/**
 * Lapin's interface: the start screen, then the workspace — ribbon tabs from
 * the product's modules, the project browser, the plan or 3D view with
 * Atrium's HUD, the properties panel, the status bar.
 */

import { useDialogLayer } from './shell/Dialog';
import { create } from 'zustand';
import { Fragment, lazy, Suspense, useEffect, useRef, useState } from 'react';
import { AppWindow, Bot, Box, Columns2, Footprints, Ruler, FolderTree, Layers, ListTree, PanelLeftOpen, PanelRightOpen, Rows2, SlidersHorizontal, Volume2, X } from 'lucide-react';
import { beginPanelDrag, DockOverlay, FloatingPanel, useKeepFloatsInWindow } from './shell/PanelDock';
import { closeOverlay, columns, panelsOn, setPanelWidth, setStackRatio, type PanelId, type Side, showOverlay, showPropsTab, syncToolPropsTab, togglePanel, usePanels, type PanelTab } from './app/panels';
import { assistantAvailable } from './assistant/available';
import { useInterface } from './app/interface';
import './app/highlight';
import { bindingText } from './app/commands';
import { closePane, closeTab, focusPane, isTableView, notify, openTab, splitPane, tabKey, useUi, type ViewTab } from './app/store';
import type { ToolDef } from './platform/module';
import { PaneContext } from './app/pane';
import { openViewWindow } from './app/windows';
import { beginTabDrag, DROP_LABELS, tabToWindow, useTabDrag } from './app/tabDrag';
import { openContextMenu, type MenuItem } from './shell/ContextMenu';
import { t as tr, useLang } from './i18n';
import { brand } from './platform/brand';
import { installTruncationTitles } from './shell/truncTitles';
import { resolveTheme, setPrefs, usePrefs } from './app/prefs';
import { CursorCoords } from './shell/CursorCoords';
import { NavStyleMenu } from './shell/NavStyleMenu';
import { CdeServerStatus } from './shell/CdeStart';
import { watchLocation } from './app/location';
import { useShortcuts } from './app/keys';
import { useXrayPeekKeys } from './app/xrayPeek';
import { activeStorey, useSession } from './engine/session';
import { useWalk } from './viewport/walk';
import { toggleQuickWalk } from './app/quickWalk';
import { SectionIcon } from './icons';
import { unitLabel, useStandards } from './engine/unitsStore';
import { openStandards } from './modules/core/standardsTab';
import { isDesktop, projectTitle } from './engine/files';
import { activate, backgrounds, dialog, findTool, moduleView, modules, overlays, productInfo } from './platform/modules';
import { ProjectBrowser } from './shell/ProjectBrowser';
import { PropertiesPanel } from './shell/PropertiesPanel';
import { ToolPropertiesPanel } from './shell/ToolPropertiesPanel';
import { Ribbon } from './shell/Ribbon';
import { StartScreen } from './shell/StartScreen';
import { useHudFacts, ViewportHud } from './shell/ViewportHud';
import { TabHoldList } from './viewport/TabHoldList';
import { CadPrompt } from './viewport/cad/CommandLine';
import { BusyOverlay } from './shell/BusyOverlay';
import { CommandPalette } from './shell/CommandPalette';
import { lazyPart } from './platform/lazyPart';
import { SelectionCard } from './shell/SelectionCard';
import { PanelFold } from './shell/PanelFold';
import { ContextMenuHost } from './shell/ContextMenu';
import { useViewTheme } from './app/viewTheme';
import { PlanView } from './viewport/PlanView';
import { Viewport3D } from './viewport/Viewport3D';
import { DrawingView } from './viewport/DrawingView';
import { SheetView } from './viewport/SheetView';
import { ScheduleView } from './viewport/ScheduleView';
import { GuestBar, JoinDialog, JoinGate, PresenceReporter, ShareDialog } from './shell/Sharing';
import { isGuest } from './engine/share';
import { connect } from './engine/session';
import { AskBox } from './shell/AskBox';
import { setBrowserFindHooks, useBrowserFind, viewIdOfTab, viewSheetItems } from './shell/browserFind';
import { openPlaceOnSheet, PlaceOnSheetDialog } from './shell/PlaceOnSheetDialog';
import { PartBoundary } from './shell/PartBoundary';
import { OnboardingMissions } from './shell/OnboardingMissions';
import { useOnboarding } from './app/onboarding';
import { PieMenu, usePiePointer } from './shell/PieMenu';
import { HealthRadar } from './shell/HealthRadar';
import { BuildReplay } from './shell/BuildReplay';
import { toggleTactileSounds } from './app/sounds';

// Find in project browser (forum topic 207): the browser unfolded (a floating one is always open), the view selected, and
// the browser asked to show its row; Place on sheet… opens its picker.
setBrowserFindHooks({
  find: (id) => {
    const p = usePanels.getState();
    if (!p.browserFloat && !p.browserOpen) togglePanel('browser');
    else if (p.overlay) closeOverlay();
    if (useSession.getState().nodes.has(id)) useUi.setState({ selection: id, selected: [id] });
    useBrowserFind.setState((s) => ({ id, n: s.n + 1 }));
  },
  place: openPlaceOnSheet,
});

/**
 * The model assistant's panel (docs/plan-assistant.md): loaded when first
 * shown, and not in the web build at all (VITE_ENGINE is "worker" there, so
 * the bundler drops the import with the branch).
 */
const AssistantPanel = import.meta.env.VITE_ENGINE !== 'worker' ? lazy(() => import('./assistant/AssistantPanel')) : null;

/** The right side: properties, Tool Properties (opens automatically when a tool is active), and on desktop the assistant. */
function RightPanel() {
  const tab = usePanels((p) => p.propsTab ?? 'properties');
  const toolId = useUi((s) => s.tool);
  const tool = findTool(toolId);
  const hasToolProps = !!(tool && (tool.typed || tool.options || tool.properties));
  useEffect(() => { syncToolPropsTab(hasToolProps); }, [hasToolProps]);
  const hasAssistant = !!(AssistantPanel && assistantAvailable());
  if (!hasAssistant && !hasToolProps && tab !== 'tool') return <PropertiesPanel />;
  return (
    <div className="side-tabbed">
      <div className="side-tabs" role="tablist">
        <button type="button" role="tab" className={`browser-tab${tab === 'properties' ? ' on' : ''}`} aria-selected={tab === 'properties'} onClick={() => showPropsTab('properties')}>Properties</button>
        {(hasToolProps || tab === 'tool') && <button type="button" role="tab" className={`browser-tab${tab === 'tool' ? ' on' : ''}`} aria-selected={tab === 'tool'} onClick={() => showPropsTab('tool')}>Tool Properties</button>}
        {hasAssistant && <button type="button" role="tab" className={`browser-tab${tab === 'assistant' ? ' on' : ''}`} aria-selected={tab === 'assistant'} onClick={() => showPropsTab('assistant')}>Assistant</button>}
      </div>
      {tab === 'assistant' && AssistantPanel ? <Suspense fallback={null}><AssistantPanel /></Suspense> : tab === 'tool' ? <ToolPropertiesPanel /> : <PropertiesPanel />}
    </div>
  );
}

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
  const soundsOn = usePrefs((p) => p.tactileSounds);
  const healthOn = usePrefs((p) => p.healthRadar);
  const facts = useHudFacts();
  const merged = parts.commandLine === 'status';
  return (
    <footer className={`status${merged ? ' line' : ''}`} data-testid={merged ? 'command-line' : undefined}>
      {merged
        ? <span className={`status-mode ${connected ? 'ok' : 'off'}`} title={connected ? 'Engine connected' : 'Engine offline'}>{facts.mode}</span>
        : <span className={connected ? 'ok' : 'off'}>{connected ? 'Engine connected' : 'Engine offline'}</span>}
      {merged && <CadPrompt inline />}
      {parts.hud === 'status' && <span className="status-hud">{facts.level} <span className="accent">{facts.elevation}</span> · {facts.view}</span>}
      <CursorCoords />
      <NavStyleMenu />
      {healthOn && <HealthRadar />}
      {/* Shown only while the sounds are on (Preferences → General → Flow extras): a click mutes them. */}
      {soundsOn && <button type="button" className="status-link sound-toggle" data-testid="sound-toggle"
        title="Tactile sounds on: click to mute" aria-label="Mute tactile sounds"
        onClick={toggleTactileSounds}><Volume2 size={12} /></button>}
      <span className="status-file" title={`${projectTitle(doc?.path)}${doc?.dirty ? ' · unsaved' : ''}`}>{projectTitle(doc?.path)}{doc?.dirty ? ' · unsaved' : ''}</span>
      <CdeServerStatus />
      <span>{elements} elements</span>
      <button type="button" className="status-link" title="The project's units: change them in Drafting standards → Project" onClick={() => openStandards('project')}>Units: {unitLabel(units) || units}</button>
      {!merged && <><span className="grow" /><span className="mods" title={`${productInfo.name} · ${modules.map((m) => m.id).join(' · ')}`}>{productInfo.name} · {modules.map((m) => m.id).join(' · ')}</span></>}
    </footer>
  );
}

/** The engine has been unreachable for a while, or could not be restarted: said until it is back. */
function EngineNotice() {
  const problem = useSession((s) => s.engineProblem);
  return problem ? <div className="notice" role="alert" style={{ position: 'fixed', zIndex: 500 }}>{problem}</div> : null;
}

function Notice() {
  const notice = useUi((s) => s.notice);
  const stays = useUi((s) => s.noticeStays);
  useEffect(() => {
    if (!notice || stays) return;
    const t = setTimeout(() => notify(null), 4000);
    return () => clearTimeout(t);
  }, [notice, stays]);
  if (!notice) return null;
  // A failure stays until closed (report #156).
  if (stays) return <div className="notice notice-status notice-problem" role="alert">{notice}<button type="button" className="icon" title="Close" onClick={() => notify(null)}><X size={13} /></button></div>;
  return <div className="notice notice-status" role="status">{notice}</div>;
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
  const dragRef = useRef<{ dragged: () => boolean } | null>(null);
  if (!pane) return null;
  const label = (t: ViewTab): string => {
    switch (t.view) {
      case 'plan': return t.planViewId ? nodes.get(t.planViewId)?.name ?? 'Plan' : t.storeyId ? nodes.get(t.storeyId)?.name ?? 'Plan' : 'Plan';
      case '3d': return t.view3dId ? nodes.get(t.view3dId)?.name ?? '3D' : '3D';
      case 'drawing': return (t.drawingViewId && nodes.get(t.drawingViewId)?.name) || 'Drawing';
      case 'sheet': return (t.sheetId && nodes.get(t.sheetId)?.name) || 'Sheet';
      case 'gantt': return moduleView('gantt')?.label ?? 'Gantt';
      case 'estimate': return moduleView('estimate')?.label ?? 'Estimate';
      case 'costTime': return moduleView('costTime')?.label ?? 'Cost over time';
      default: return (t.view !== 'schedule' ? moduleView(t.view)?.label : null) ?? ((t.scheduleId && nodes.get(t.scheduleId)?.name) || 'Schedule');
    }
  };
  const mine = tabKey({ ...pane, storeyId: pane.view === 'plan' ? storeyId : null });
  // A tab's own menu (decision 445): a window of its own, the other pane or a split, closing.
  const tabMenu = (x: number, y: number, t: ViewTab) => {
    const items: MenuItem[] = [];
    if (role === 'main' && !isGuest()) {
      if (isDesktop()) items.push({ label: 'Open in a new window', onSelect: () => void tabToWindow(t, label(t)) });
      if (count < 2) {
        items.push({ label: 'Split: open beside', onSelect: () => { splitPane('row'); openTab(1, t); } });
        items.push({ label: 'Split: open below', onSelect: () => { splitPane('column'); openTab(1, t); } });
      } else items.push({ label: 'Open in the other pane', onSelect: () => openTab(i === 0 ? 1 : 0, t) });
    }
    // The view or sheet in the project browser, and Place on sheet… (forum topic 207).
    items.push(...viewSheetItems(viewIdOfTab(t, nodes), nodes));
    items.push({ label: 'Close', onSelect: () => closeTab(tabKey(t)) });
    if (tabs.length > 1) items.push({ label: 'Close the others', onSelect: () => { for (const o of useUi.getState().viewTabs) if (tabKey(o) !== tabKey(t)) closeTab(tabKey(o)); } });
    openContextMenu(x, y, items);
  };
  const current = tabs.find((t) => tabKey(t) === mine);
  return (
    <div className="view-tabs">
      <QuickViews />
      <div className="view-tab-strip">
        {tabs.map((t) => {
          const key = tabKey(t);
          return (
            <button type="button" key={key} className={`view-tab${key === mine ? ' on' : ''}`} title={`${tr(label(t))} · ${tr('drag out for a window of its own, to the side to split')}`}
              onPointerDown={(e) => { if (e.button === 0 && !(e.target as HTMLElement).closest('.close') && role === 'main' && !isGuest()) dragRef.current = beginTabDrag(e, t, label(t)); }}
              onClick={() => { if (dragRef.current?.dragged()) { dragRef.current = null; return; } dragRef.current = null; openTab(i, t); }}
              onContextMenu={(e) => { e.preventDefault(); tabMenu(e.clientX, e.clientY, t); }}>
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

/** Whether the split open is the one the Plan + 3D button opened: only that one it closes, and only for it it is lit. */
const useQuickSplit = create<{ on: boolean }>(() => ({ on: false }));
useUi.subscribe((s) => { if (s.panes.length < 2 && useQuickSplit.getState().on) useQuickSplit.setState({ on: false }); });

/** 1-click live Plan + 3D split view; a second click closes it. A split the user made otherwise is left alone. */
function togglePlan3dSplit(): void {
  const ui = useUi.getState();
  if (ui.panes.length >= 2) {
    if (useQuickSplit.getState().on) { closePane(1); useQuickSplit.setState({ on: false }); }
    return;
  }
  const storeyId = ui.storeyId ?? activeStorey()?.id ?? null;
  splitPane('row');
  openTab(0, { view: 'plan', storeyId, planViewId: ui.planViewId, drawingViewId: null, sheetId: null, scheduleId: null, view3dId: null });
  openTab(1, { view: '3d', storeyId: null, planViewId: null, drawingViewId: null, sheetId: null, scheduleId: null, view3dId: ui.view3dId });
  useQuickSplit.setState({ on: true });
}

/** 3D, Section, Measure, Walk and Plan+3D in one click, before the view tabs (decision 458; plan-ux-game-like.md). */
function QuickViews() {
  const tool = useUi((s) => s.tool);
  const walking = useWalk((s) => s.start !== null);
  const split = useUi((s) => s.panes.length >= 2);
  const ours = useQuickSplit((s) => s.on);
  const splitOn = split && ours;
  const role = useUi((s) => s.windowRole);
  const missionsOpen = useOnboarding((s) => s.open);
  // The missions that teach Walk and Plan + 3D show the buttons while they are open.
  const extras = usePrefs((p) => p.quickWalkSplit) || missionsOpen;
  const quick: { id: string; icon: ToolDef['icon']; hint: string }[] = [
    { id: 'view.3d', icon: Box, hint: '3D' },
    { id: 'view.section', icon: SectionIcon, hint: 'Section' },
    { id: 'measure', icon: Ruler, hint: 'Measure' },
  ];
  return (
    <div className="quick-views" role="group" aria-label={tr('Views')}>
      {quick.map(({ id, icon: Icon, hint }) => {
        const def = findTool(id);
        if (!def) return null;
        return (
          <button type="button" key={id} className={`icon mini${tool === id ? ' on' : ''}`} data-testid={`quick-${id}`} aria-label={tr(hint)}
            title={def.shortcut ? `${tr(hint)} (${def.shortcut})` : tr(hint)} onClick={() => activate(def)}><Icon size={14} /></button>
        );
      })}
      {/* A Flow extra (Preferences → General): off, the quick views are as they were. */}
      {extras && <button type="button" className={`icon mini${walking ? ' on' : ''}`} data-testid="quick-view.walk" aria-label={tr('Walk in 3D')}
        title={tr('Walk in 3D (WASD, F to fly)')} onClick={toggleQuickWalk}><Footprints size={14} /></button>}
      {extras && role === 'main' && !isGuest() && (
        <button type="button" className={`icon mini${splitOn ? ' on' : ''}`} data-testid="quick-view.split" aria-label={tr('Plan + 3D')} disabled={split && !ours}
          title={splitOn ? tr('Close split view') : tr('Plan + 3D live split view')} onClick={togglePlan3dSplit}><Columns2 size={14} /></button>
      )}
    </div>
  );
}

/** A tab being dragged: its name at the pointer, the place it would go lit (decision 445). */
function TabDragOverlay() {
  const { label, at, drop } = useTabDrag();
  if (!label || !at) return null;
  return (
    <div className="tab-drag" aria-hidden>
      {drop && <div className={`tab-drop ${drop.kind}`} style={{ left: drop.rect.left, top: drop.rect.top, width: drop.rect.width, height: drop.rect.height }}><span>{tr(DROP_LABELS[drop.kind])}</span></div>}
      <div className="tab-ghost" style={{ left: at.x + 12, top: at.y + 8 }} data-no-i18n>{label}</div>
    </div>
  );
}

/** One pane: its tabs, its view, and over the active one the HUD, errors and notices. */
function PaneBody({ i }: { i: number }) {
  const view = useUi((s) => s.panes[i]?.view);
  const active = useUi((s) => s.pane === i);
  const error = useSession((s) => s.error);
  const viewTheme = useViewTheme();
  // The drawing areas wear the viewport theme (Preferences → General); tables (schedules, Gantt, estimate) stay with the panels.
  const canvas = view === 'plan' || view === 'drawing' || view === 'sheet' || !view || view === '3d';
  return (
    <PaneContext.Provider value={i}>
      <div className={`pane${active ? ' on' : ''}`} onPointerDownCapture={() => focusPane(i)} data-testid={`pane-${i}`}>
        <PaneTabs i={i} />
        <div className="view-area" data-theme={canvas ? viewTheme : undefined}>
          {view === 'plan' ? <PlanView /> : view === 'drawing' ? <DrawingView /> : view === 'sheet' ? <SheetView /> : view === 'schedule' ? <ScheduleView /> : view && view !== '3d' && ModuleView(view) ? <ModuleViewBody kind={view} /> : <Viewport3D />}
          {/* The Gantt and the estimate are tables with their own toolbar: no HUD, quick tools or selection card over them. */}
          {active && !isTableView(view) && <ViewportHud />}
          {active && !isTableView(view) && <SelectionCard />}
          {active && !isTableView(view) && <TabHoldList />}
          {active && !isTableView(view) && overlays().map((Overlay, k) => <PartBoundary key={k} part={Overlay.displayName ?? Overlay.name ?? 'Overlay'} overlay><Overlay /></PartBoundary>)}
          {active && error && <div className="error" role="alert" onClick={() => useSession.setState({ error: null })}>{error}</div>}
          {active && <Notice />}
        </div>
      </div>
    </PaneContext.Provider>
  );
}

/** A module's own view (the Gantt), when the product has the module. */
const ModuleView = (kind: string) => moduleView(kind)?.component ?? null;
function ModuleViewBody({ kind }: { kind: string }) {
  const View = ModuleView(kind);
  return View ? <View /> : null;
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
    const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); window.removeEventListener('pointercancel', up); };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
    window.addEventListener('pointercancel', up);
  };
  return (
    <div ref={host} className={`panes split-${split}`}>
      <div className="pane-slot" style={{ flexBasis: count > 1 ? `${ratio * 100}%` : '100%' }}><PaneBody i={0} /></div>
      {count > 1 && <div className="pane-sep" onPointerDown={onDown} role="separator" />}
      {count > 1 && <div className="pane-slot" style={{ flexBasis: `${(1 - ratio) * 100}%` }}><PaneBody i={1} /></div>}
    </div>
  );
}

/** A separator beside a side: dragged, it sets the side's width (its first open panel's). */
function SideSep({ side }: { side: Side }) {
  const onDown = (e: React.PointerEvent) => {
    (e.target as Element).setPointerCapture?.(e.pointerId);
    const start = e.clientX;
    const st = usePanels.getState();
    const first = panelsOn(st, side).find((p) => (p === 'browser' ? st.browserOpen : st.propsOpen)) ?? 'browser';
    const width = first === 'browser' ? st.browserWidth : st.propsWidth;
    const move = (ev: PointerEvent) => setPanelWidth(first, width + (side === 'left' ? ev.clientX - start : start - ev.clientX));
    const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); window.removeEventListener('pointercancel', up); };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
    window.addEventListener('pointercancel', up);
  };
  const fold = () => { const st = usePanels.getState(); const first = panelsOn(st, side).find((p) => (p === 'browser' ? st.browserOpen : st.propsOpen)); if (first) togglePanel(first); };
  return <div className="side-sep" role="separator" aria-orientation="vertical" onPointerDown={onDown} onDoubleClick={fold} title="Drag to resize; double-click to fold or unfold" />;
}

/** The divider between two panels stacked on one side: dragged, it shares their height. */
function StackSep({ host }: { host: React.RefObject<HTMLDivElement | null> }) {
  const onDown = (e: React.PointerEvent) => {
    (e.target as Element).setPointerCapture?.(e.pointerId);
    const move = (ev: PointerEvent) => {
      const r = host.current!.getBoundingClientRect();
      setStackRatio((ev.clientY - r.top) / r.height);
    };
    const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); window.removeEventListener('pointercancel', up); };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
    window.addEventListener('pointercancel', up);
  };
  return <div className="stack-sep" role="separator" aria-orientation="horizontal" onPointerDown={onDown} onDoubleClick={() => setStackRatio(0.5)} title="Drag to share the height; double-click to halve" />;
}

const PANEL_NAME: Record<PanelId, string> = { browser: 'Project browser', props: 'Properties' };

/** A panel folded inside a stack whose other panel is open: a thin bar to unfold it. */
function FoldedBar({ panel, side }: { panel: PanelId; side: Side }) {
  return (
    <button type="button" className="fold-bar" data-rail={panel} title={`Unfold the ${PANEL_NAME[panel].toLowerCase()}`} onClick={() => togglePanel(panel)}>
      {side === 'left' ? <PanelLeftOpen size={13} /> : <PanelRightOpen size={13} />}<span>{PANEL_NAME[panel]}</span>
    </button>
  );
}

/** One side of the workspace: its panels (open, folded to a bar, or all folded to rails), stacked when there are two. */
function SideColumn({ side }: { side: Side }) {
  const st = usePanels();
  const host = useRef<HTMLDivElement>(null);
  const ps = panelsOn(st, side);
  if (!ps.length) return <span />;
  const isOpen = (p: PanelId) => (p === 'browser' ? st.browserOpen : st.propsOpen);
  const open = ps.filter(isOpen);
  if (!open.length) {
    return <div className={`side-col on-${side} rails`}>{ps.map((p) => <Rail key={p} panel={p} side={side} label={PANEL_NAME[p]} />)}</div>;
  }
  const grow = (p: PanelId) => (open.length < 2 ? 1 : p === open[0] ? st.stackRatio : 1 - st.stackRatio);
  return (
    <div ref={host} className={`side-col on-${side}${ps.length > 1 ? ' stacked' : ''}`}>
      {ps.map((p, i) => isOpen(p) ? (
        <Fragment key={p}>
          {i > 0 && open.length > 1 && <StackSep host={host} />}
          <div className={`side ${p === 'browser' ? 'browser-side' : 'props-side'}`} data-panel-id={p} style={{ flex: `${grow(p)} 1 0` }} onPointerDown={(e) => beginPanelDrag(e, p)}>
            <PanelFold side={p} at={side} />{p === 'browser' ? <ProjectBrowser /> : <RightPanel />}
          </div>
        </Fragment>
      ) : <FoldedBar key={p} panel={p} side={side} />)}
    </div>
  );
}

/** A folded panel: a narrow rail; its icons open the panel over the view for a moment, the arrow unfolds it. */
function Rail({ panel: side, side: at, label }: { panel: PanelId; side: Side; label: string }) {
  const hidden = usePanels((p) => p.railsHidden);
  const overlay = usePanels((p) => p.overlay);
  if (hidden) return <span />;
  const keys = bindingText(side === 'browser' ? 'app.toggleBrowser' : 'app.toggleProps').split(', ')[0];
  const pick = (tab?: PanelTab) => (e: React.MouseEvent) => { e.stopPropagation(); showOverlay(side, tab); };
  const on = (tab?: string) => (overlay?.side === side && overlay.tab === tab ? ' on' : '');
  return (
    <aside className={`panel rail ${side}`} aria-label={label} data-rail={side}>
      <button type="button" className="icon mini" title={`Unfold the ${label.toLowerCase()}${keys ? ` (${keys})` : ''}`} onClick={() => togglePanel(side)}>{at === 'left' ? <PanelLeftOpen size={15} /> : <PanelRightOpen size={15} />}</button>
      {side === 'browser' ? <>
        <button type="button" className={`icon rail-icon${on('project')}`} title="Project: views, sheets and schedules" onClick={pick('project')}><FolderTree size={16} /></button>
        <button type="button" className={`icon rail-icon${on('levels')}`} title="Levels" onClick={pick('levels')}><Layers size={16} /></button>
        <button type="button" className={`icon rail-icon${on('ifc')}`} title="IFC tree" onClick={pick('ifc')}><ListTree size={16} /></button>
      </> : <>
        <button type="button" className={`icon rail-icon${on(undefined)}`} title="Properties" onClick={pick()}><SlidersHorizontal size={16} /></button>
        {AssistantPanel && assistantAvailable() && <button type="button" className="icon rail-icon" title="Assistant" onClick={(e) => { e.stopPropagation(); showPropsTab('assistant'); }}><Bot size={16} /></button>}
      </>}
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
  const browserSide = usePanels((p) => p.browserSide);
  const propsSide = usePanels((p) => p.propsSide);
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
  const at = browser ? browserSide : propsSide;
  const width = browser ? browserWidth : propsWidth;
  return (
    <div ref={ref} className={`panel-overlay side ${at}`} style={at === 'left' ? { left: inset, width } : { right: inset, width }}>
      <PanelFold side={overlay.side} at={at} overlay />
      {browser ? <ProjectBrowser key={overlay.tab ?? "project"} show={overlay.tab === 'assistant' ? undefined : overlay.tab} /> : <PropertiesPanel />}
    </div>
  );
}

/** The dialogs of working together (File → Share session…, Join session…), opened by the store's `dialog`. */
// Fetched the first time each is opened (platform/lazyPart.tsx).
const InterfaceDialog = lazyPart(() => import('./shell/InterfaceDialog').then((m) => m.InterfaceDialog), { title: 'Interface' });
const PreferencesDialog = lazyPart(() => import('./shell/PreferencesDialog').then((m) => m.PreferencesDialog), { title: 'Preferences' });
const QuickToolsDialog = lazyPart(() => import('./shell/QuickToolsDialog').then((m) => m.QuickToolsDialog), { title: 'Quick tools' });
const ShortcutsDialog = lazyPart(() => import('./shell/ShortcutsDialog').then((m) => m.ShortcutsDialog), { title: 'Keyboard shortcuts' });
const OptimizeIfcDialog = lazyPart(() => import('./shell/OptimizeIfcDialog').then((m) => m.OptimizeIfcDialog), { title: 'Optimize IFC' });
const ProjectTemplatesDialog = lazyPart(() => import('./shell/ProjectTemplatesDialog').then((m) => m.ProjectTemplatesDialog), { title: 'Project templates' });
const SaveTemplateDialog = lazyPart(() => import('./shell/ProjectTemplatesDialog').then((m) => m.SaveTemplateDialog), { title: 'Save as template' });

// Lapin Extensions (docs/plan-extensions.md): File → Extensions….
const ExtensionsDialog = lazyPart(() => import('./shell/ExtensionsDialog').then((m) => m.ExtensionsDialog), { title: 'Extensions' });
const MyToolsDialog = lazyPart(() => import('./shell/MyToolsDialog').then((m) => m.MyToolsDialog), { title: 'Customize My tools' });
const shareDialogs: Record<string, React.ComponentType> = { myTools: MyToolsDialog, share: ShareDialog, join: JoinDialog, interface: InterfaceDialog, preferences: PreferencesDialog, quickTools: QuickToolsDialog, shortcuts: ShortcutsDialog, palette: CommandPalette, optimizeIfc: OptimizeIfcDialog, projectTemplates: ProjectTemplatesDialog, saveTemplate: SaveTemplateDialog, extensions: ExtensionsDialog, placeOnSheet: PlaceOnSheetDialog };

function Workspace() {
  const open = useUi((s) => s.dialog);
  const Dialog = open ? dialog(open, shareDialogs) : null;
  const panels = usePanels();
  const ui = useInterface();
  useKeepFloatsInWindow();
  return (
    <div className={`workspace ui-${ui.preset} rows-${ui.parts.rows}`}>
      <Ribbon />
      <div className="body" style={{ gridTemplateColumns: columns(panels) }}>
        <SideColumn side="left" />
        {panelsOn(panels, 'left').some((p) => (p === 'browser' ? panels.browserOpen : panels.propsOpen)) ? <SideSep side="left" /> : <span />}
        <main className="views"><Panes /></main>
        {panelsOn(panels, 'right').some((p) => (p === 'browser' ? panels.browserOpen : panels.propsOpen)) ? <SideSep side="right" /> : <span />}
        <SideColumn side="right" />
        <PanelOverlay />
      </div>
      <FloatingPanel panel="browser"><ProjectBrowser /></FloatingPanel>
      <FloatingPanel panel="props"><RightPanel /></FloatingPanel>
      <DockOverlay />
      <StatusBar />
      <TabDragOverlay />
      {Dialog && <PartBoundary part={`Dialog ${open}`} overlay onClose={() => useUi.setState({ dialog: null })} resetKey={open}><Dialog /></PartBoundary>}
      <GuestBar />
      <PresenceReporter />
      <AskBox />
      <PartBoundary part="Missions" overlay><OnboardingMissions /></PartBoundary>
      <PartBoundary part="Radial menu" overlay><PieMenu /></PartBoundary>
      <PartBoundary part="Build replay" overlay><BuildReplay /></PartBoundary>
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
      {Dialog && <PartBoundary part={`Dialog ${open}`} overlay onClose={() => useUi.setState({ dialog: null })} resetKey={open}><Dialog /></PartBoundary>}
      <AskBox />
      <PartBoundary part="Missions" overlay><OnboardingMissions /></PartBoundary>
      <PartBoundary part="Radial menu" overlay><PieMenu /></PartBoundary>
      <PartBoundary part="Build replay" overlay><BuildReplay /></PartBoundary>
      {backgrounds().map((B, i) => <B key={i} />)}
    </div>
  );
}

export function App() {
  const screen = useUi((s) => s.screen);
  const theme = useUi((s) => s.theme);
  useShortcuts();
  useXrayPeekKeys();
  usePiePointer();
  useDialogLayer();
  useEffect(() => { watchLocation(); installTruncationTitles(); }, []);
  useEffect(() => { document.documentElement.dataset.theme = theme; }, [theme]);
  // The theme chosen is kept for the next start (a tester's report: dark came back light every time); a system choice follows the system.
  useEffect(() => { const p = usePrefs.getState(); if (p.theme !== 'system' && p.theme !== theme) setPrefs({ theme }); }, [theme]);
  const themeChoice = usePrefs((s) => s.theme);
  useEffect(() => {
    if (themeChoice !== 'system') return;
    const mq = window.matchMedia?.('(prefers-color-scheme: dark)');
    const apply = () => useUi.setState({ theme: resolveTheme('system') });
    apply();
    mq?.addEventListener?.('change', apply);
    return () => mq?.removeEventListener?.('change', apply);
  }, [themeChoice]);
  const role = useUi((s) => s.windowRole);
  // The window's title names the project, with a dot while it holds unsaved changes (forum topic 257): "Name • — Lapin".
  const doc = useSession((s) => s.doc);
  const lang = useLang((s) => s.lang);
  useEffect(() => {
    const title = screen === 'start' || !doc ? brand.name : `${doc.path ? projectTitle(doc.path) : tr('New project')}${doc.dirty ? ' •' : ''} — ${brand.name}`;
    document.title = title;
    if (isDesktop()) void import('@tauri-apps/api/window').then((w) => w.getCurrentWindow().setTitle(title)).catch(() => undefined);
  }, [screen, doc, doc?.path, doc?.dirty, lang]);
  if (screen === 'join') return <JoinGate onJoin={() => void connect()} />;
  return <>{screen === 'start' ? <StartScreen /> : role === 'view' ? <ViewWindow /> : <Workspace />}<EngineNotice /><BusyOverlay /><ContextMenuHost /></>;
}
