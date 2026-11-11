/**
 * The tab row's middle (decision 367): the workspace switcher, the discipline whose tabs show, chosen in one click; and the
 * tabs on one line, those that do not fit gathered under More ▾ at its end (the current one always kept in sight), so the
 * row keeps its height and no name breaks onto a second line.
 */

import { useLayoutEffect, useRef, useState, type DragEvent } from 'react';
import { ChevronDown } from 'lucide-react';
import { chooseWorkspace, useInterface, visibleTabIds, WORKSPACE_IDS, WORKSPACE_LABELS, type WorkspaceId } from '../app/interface';
import { ribbonTabs } from '../platform/modules';
import { useUi } from '../app/store';
import { openContextMenu, type MenuItem } from './ContextMenu';
import { MY_TAB } from '../app/myTools';
import { addToMyTools, carriesTool, toolOfDrag } from './MyToolsBand';
import type { RibbonTabDef } from '../platform/module';

/** The workspace as a button, its choices in a menu: each discipline with tabs in this edition, all, the user's own once made; then Customise…. */
export function WorkspaceSwitch() {
  const s = useInterface();
  const all = ribbonTabs();
  const offered = WORKSPACE_IDS.filter((w) => (w === 'mine' ? s.mine !== null || s.workspace === 'mine' : w === 'all' || visibleTabIds({ ...s, workspace: w }, all).length > 1));
  const open = (e: React.MouseEvent<HTMLButtonElement>) => {
    const r = e.currentTarget.getBoundingClientRect();
    const items: MenuItem[] = offered.map((w) => ({ label: WORKSPACE_LABELS[w], checked: s.workspace === w, onSelect: () => chooseWorkspace(w as WorkspaceId) }));
    items.push({ label: 'Customise the tabs…', onSelect: () => useUi.setState({ dialog: 'interface' }) });
    openContextMenu(r.left, r.bottom + 2, items);
  };
  return (
    <button type="button" className="workspace-switch" data-testid="workspace-switch" title="Workspace: the tabs a discipline works with" onClick={open}>
      <span>{WORKSPACE_LABELS[s.workspace]}</span><ChevronDown size={13} />
    </button>
  );
}

/** How many tabs fit in a width: as many as their widths allow, leaving room for More ▾ when some are left out. */
export function fitTabs(widths: number[], available: number, more: number): number {
  const total = widths.reduce((a, b) => a + b, 0);
  if (total <= available) return widths.length;
  let used = more, n = 0;
  while (n < widths.length && used + widths[n] <= available) used += widths[n++];
  return n;
}

/** The tabs shown and those under More ▾: the first that fit, with the current one put in the last place when it would be left out. */
export function splitTabs<T extends { id: string }>(tabs: T[], fit: number, current: string | undefined): { shown: T[]; more: T[] } {
  if (fit >= tabs.length) return { shown: tabs, more: [] };
  const shown = tabs.slice(0, Math.max(0, fit));
  const i = tabs.findIndex((t) => t.id === current);
  if (i >= fit && shown.length > 0) shown[shown.length - 1] = tabs[i];
  else if (i >= fit) shown.push(tabs[i]);
  return { shown, more: tabs.filter((t) => !shown.includes(t)) };
}

export function TabStrip({ tabs, current, open, onPick }: { tabs: RibbonTabDef[]; current: string | undefined; open: boolean; onPick: (id: string) => void }) {
  const strip = useRef<HTMLDivElement>(null);
  const measure = useRef<HTMLDivElement>(null);
  const [fit, setFit] = useState(tabs.length);
  const key = tabs.map((t) => t.id).join();
  useLayoutEffect(() => {
    const el = strip.current, m = measure.current;
    if (!el || !m) return;
    const update = () => {
      const widths = [...m.children].map((c) => (c as HTMLElement).offsetWidth);
      const more = (m.lastElementChild as HTMLElement | null)?.offsetWidth ?? 70;
      setFit(fitTabs(widths.slice(0, tabs.length), el.clientWidth - 2, more));
    };
    update();
    const ro = new ResizeObserver(update);
    ro.observe(el);
    return () => ro.disconnect();
  }, [key, tabs.length]);
  const { shown, more } = splitTabs(tabs, fit, current);
  // A tool dragged from another tab onto the My tools tab goes in it (decision 479); held there a moment, the tab opens, so the tool can be dropped in its place.
  const [hot, setHot] = useState(false);
  const spring = useRef<number | undefined>(undefined);
  const mineProps = (id: string) => (id !== MY_TAB ? {} : {
    onDragOver: (e: DragEvent) => {
      if (!carriesTool(e)) return;
      e.preventDefault(); e.dataTransfer.dropEffect = 'copy';
      setHot(true);
      if (current !== MY_TAB && spring.current === undefined) spring.current = window.setTimeout(() => { spring.current = undefined; onPick(MY_TAB); }, 600);
    },
    onDragLeave: () => { setHot(false); window.clearTimeout(spring.current); spring.current = undefined; },
    onDrop: (e: DragEvent) => {
      e.preventDefault(); setHot(false);
      window.clearTimeout(spring.current); spring.current = undefined;
      const tool = toolOfDrag(e);
      if (tool) addToMyTools(tool);
    },
  });
  const openMore = (e: React.MouseEvent<HTMLButtonElement>) => {
    const r = e.currentTarget.getBoundingClientRect();
    openContextMenu(r.left, r.bottom + 2, more.map((t) => ({ label: t.label, ctx: t.i18nCtx, onSelect: () => onPick(t.id) })));
  };
  return (
    <div className="tab-strip" ref={strip}>
      {shown.map((t) => (
        <button type="button" key={t.id} className={`tab${t.id === current ? ' on' : ''}${open && t.id === current ? ' open' : ''}${hot && t.id === MY_TAB ? ' drop-hot' : ''}`} onClick={() => onPick(t.id)} data-i18n-ctx={t.i18nCtx} data-tab={t.id} {...mineProps(t.id)}>{t.label}</button>
      ))}
      {more.length > 0 && (
        <button type="button" className="tab more" data-testid="tabs-more" onClick={openMore} title={more.map((t) => t.label).join(' · ')}>More<ChevronDown size={13} /></button>
      )}
      {/* Every tab laid out out of sight, to know their widths (and More's, last). */}
      <div className="tab-measure" ref={measure} aria-hidden>
        {tabs.map((t) => <span key={t.id} className="tab-ghost" data-i18n-ctx={t.i18nCtx}>{t.label}</span>)}
        <span className="tab-ghost">More<ChevronDown size={13} /></span>
      </div>
    </div>
  );
}
