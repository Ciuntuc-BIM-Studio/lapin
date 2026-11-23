/**
 * Find in project browser and Place on sheet… (forum topic 207): the view or sheet a pane or tab shows, found in the browser
 * (its set and kind unfolded, the browser unfolded, its row scrolled into sight and lit, the view selected), and where a view
 * sits on the sheets (the browser's marker before it, Place on sheet…'s answer). The pure parts are here for the tests; the
 * browser reveals the row (ProjectBrowser), PlaceOnSheetDialog places the view.
 */

import { createElement } from 'react';
import { create } from 'zustand';
import { FolderSearch, FileInput } from 'lucide-react';
import type { NodeDto } from '../engine/protocol';
import type { Pane } from '../app/store';
import { planViewIdOf } from '../viewport/storeyPlan';
import { sheetOfPaper } from '../viewport/paperView';
import type { BrowserItem, SetGroup } from './browserSets';
import type { MenuItem } from './ContextMenu';

/** The view, sheet or schedule a pane or a tab shows (a level's default plan as `plan:<storey>`, {3D} as `3d`); null for a module's own view. */
export function viewIdOfTab(t: Pane & { storeyId?: string | null }, nodes: ReadonlyMap<string, NodeDto>): string | null {
  switch (t.view) {
    case 'plan': return t.storeyId ? planViewIdOf(nodes, t.storeyId, t.planViewId) : t.planViewId;
    // A sheet's paper drawn on (Annotate) is its sheet's.
    case 'drawing': return sheetOfPaper(t.drawingViewId) ?? t.drawingViewId;
    case '3d': return t.view3dId ?? '3d';
    case 'sheet': return t.sheetId;
    case 'schedule': return t.scheduleId;
    default: return null;
  }
}

/** The browser's fold under a set for each kind of item (ProjectBrowser's `Kind` ids). */
const KIND_FOLD: Record<BrowserItem['kind'], string> = { plan: 'plans', section: 'sections', elevation: 'elevations', plane: 'planes', '3d': '3d', sheet: 'sheets', schedule: 'schedules', profile: 'profiles', site: 'site' };

/**
 * The folds to open so the item `id` shows: the Site's, or the first set holding it and its kind there (a callout's
 * detail view under Details); null when the browser lists it nowhere.
 */
export function foldsFor(groups: SetGroup[], siteItems: BrowserItem[], id: string): string[] | null {
  if (siteItems.some((i) => i.id === id)) return ['site'];
  for (const g of groups) {
    const item = g.items.find((i) => i.id === id);
    if (!item) continue;
    const set = g.set?.id ?? 'other';
    const kind = item.node?.props.calloutOf ? 'details' : KIND_FOLD[item.kind];
    return ['set:' + set, `${set}:${kind}`];
  }
  return null;
}

/** The collapsed folds with those given opened (the same set when none of them was closed). */
export function unfold(collapsed: ReadonlySet<string>, folds: string[]): Set<string> | null {
  if (!folds.some((f) => collapsed.has(f))) return null;
  const next = new Set(collapsed);
  for (const f of folds) next.delete(f);
  return next;
}

/** The sheets each view or schedule is on, in the sheets' number order (a view on two pages or boundaries counts its sheets once). */
export function placedOn(nodes: ReadonlyMap<string, NodeDto>): Map<string, NodeDto[]> {
  const on = new Map<string, NodeDto[]>();
  const sheets = [...nodes.values()].filter((n) => n.type === 'LapinSheet').sort((a, b) => sheetNumber(a).localeCompare(sheetNumber(b), undefined, { numeric: true }));
  for (const s of sheets) {
    const vps = Array.isArray(s.props.viewports) ? (s.props.viewports as { viewId?: unknown }[]) : [];
    for (const v of new Set(vps.map((x) => x?.viewId).filter((x): x is string => typeof x === 'string'))) {
      const list = on.get(v);
      if (list) list.push(s); else on.set(v, [s]);
    }
  }
  return on;
}

const sheetNumber = (s: NodeDto): string => String(s.props.number ?? '');

/** A sheet as the interface names it: A-101 · Plans. */
export const sheetLabel = (s: NodeDto): string => (sheetNumber(s) ? `${sheetNumber(s)} · ${s.name}` : s.name);

/** The marker's tooltip: On sheet A-101 · Plans, or On sheets A-101 · Plans, A-102 · Sections. */
export const onSheetTitle = (sheets: NodeDto[]): string => (sheets.length === 1 ? `On sheet ${sheetLabel(sheets[0])}` : `On sheets ${sheets.map(sheetLabel).join(', ')}`);

/** Whether a view can go on a sheet: a saved view (a LapinView), a level's default plan or a schedule; not {3D}, a sheet or a module's view. */
export function canPlace(id: string | null, nodes: ReadonlyMap<string, NodeDto>): boolean {
  if (!id) return false;
  if (id.startsWith('plan:')) return nodes.get(id.slice(5))?.type === 'IfcBuildingStorey';
  const n = nodes.get(id);
  return n?.type === 'LapinView' || n?.type === 'LapinSchedule' || n?.type === 'LapinProfileView';
}

/** The sheets that match what is typed (in the number or the name, any case), in number order. */
export function matchingSheets(nodes: ReadonlyMap<string, NodeDto>, typed: string): NodeDto[] {
  const q = typed.trim().toLowerCase();
  return [...nodes.values()].filter((n) => n.type === 'LapinSheet' && (!q || sheetLabel(n).toLowerCase().includes(q)))
    .sort((a, b) => sheetNumber(a).localeCompare(sheetNumber(b), undefined, { numeric: true }));
}

/** The paper a new sheet takes: the last sheet's (its size and orientation), else A3 landscape. */
export function paperForNew(nodes: ReadonlyMap<string, NodeDto>): Record<string, unknown> {
  const last = matchingSheets(nodes, '').at(-1);
  if (!last) return { paper: 'A3' };
  const p: Record<string, unknown> = { paper: last.props.paper ?? 'A3', orientation: last.props.orientation ?? 'landscape' };
  if (last.props.paper === 'custom') { p.paperWidth = last.props.paperWidth; p.paperHeight = last.props.paperHeight; }
  return p;
}

/** A request to the browser: the id to show, counted so the same one asked again shows again. */
export const useBrowserFind = create<{ id: string | null; n: number }>(() => ({ id: null, n: 0 }));

/** The hooks the app gives (kept out of this file so its tests need no panels or session). */
let hooks: { find?: (id: string) => void; place?: (id: string) => void } = {};
export function setBrowserFindHooks(h: typeof hooks): void { hooks = h; }

export const findInBrowser = (id: string): void => hooks.find?.(id);
export const placeOnSheet = (id: string): void => hooks.place?.(id);

/** A view's right-click (a plan, a section, an elevation, a 3D view): Find in project browser, and Place on sheet… for one that goes on sheets. */
export function viewSheetItems(viewId: string | null, nodes: ReadonlyMap<string, NodeDto>): MenuItem[] {
  if (!viewId) return [];
  const items: MenuItem[] = [{ label: 'Find in project browser', icon: createElement(FolderSearch, { size: 13 }), onSelect: () => findInBrowser(viewId) }];
  if (canPlace(viewId, nodes)) items.push({ label: 'Place on sheet…', icon: createElement(FileInput, { size: 13 }), onSelect: () => placeOnSheet(viewId) });
  return items;
}
