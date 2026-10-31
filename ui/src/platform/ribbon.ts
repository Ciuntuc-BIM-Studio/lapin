/**
 * The ribbon's tabs from the product's modules: tabs with the same id are one
 * tab (a module adds its groups to another's: phases into Manage), in the
 * order of the lowest `order`; a tab's `include` brings shared groups after
 * its own (core's Modify tools on Architecture and Structure).
 */

import type { LapinModule, RibbonGroupDef, RibbonTabDef } from './module';

export function mergeTabs(modules: readonly LapinModule[], orders: Record<string, number> = {}): RibbonTabDef[] {
  const shared: Record<string, RibbonGroupDef> = {};
  for (const m of modules) Object.assign(shared, m.sharedGroups ?? {});
  const byId = new Map<string, RibbonTabDef>();
  for (const m of modules) for (const t of m.tabs ?? []) {
    const had = byId.get(t.id);
    if (!had) { byId.set(t.id, { ...t, groups: [...t.groups], include: [...(t.include ?? [])] }); continue; }
    had.groups.push(...t.groups);
    had.order = Math.min(had.order, t.order);
    for (const i of t.include ?? []) if (!had.include!.includes(i)) had.include!.push(i);
  }
  return [...byId.values()]
    .map((t) => ({ ...t, order: orders[t.id] ?? t.order, groups: [...t.groups, ...(t.include ?? []).map((i) => shared[i]).filter((g): g is RibbonGroupDef => !!g)] }))
    .sort((a, b) => a.order - b.order);
}
