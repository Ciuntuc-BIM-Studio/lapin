/**
 * The ribbon's tabs from the product's modules: tabs with the same id are one
 * tab (a module adds its groups to another's: phases into Manage), in the
 * order of the lowest `order`; a tab's `include` brings shared groups after
 * its own (core's Modify tools on Architecture and Structure).
 */

import type { LapinModule, RibbonGroupDef, RibbonTabDef, ToolDef } from './module';

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
    .map((t) => ({ ...t, order: orders[t.id] ?? t.order, groups: withShared(placed(t.groups), (t.include ?? []).map((i) => shared[i]).filter((g): g is RibbonGroupDef => !!g)) }))
    .sort((a, b) => a.order - b.order);
}

/**
 * The shared groups after a tab's own. A shared group with the label of one the tab has (Draft's own Modify; Architecture's own Joins)
 * is one group with it: the tab's tools first, then the shared ones. A shared tool the tab already has anywhere (the same label, or one
 * `alsoCalled` says) is left out, so a tab never shows two Move or two Offset buttons, wherever its own is. The shared tools left out
 * stay tools (their ids resolve).
 */
function withShared(own: RibbonGroupDef[], shared: RibbonGroupDef[]): RibbonGroupDef[] {
  const out = [...own];
  const names = (t: ToolDef) => [t.label, ...(t.alsoCalled ?? [])];
  const have = new Set(own.flatMap((g) => [...g.tools, ...(g.small ?? [])].flatMap(names)));
  const fresh = (t: ToolDef) => !names(t).some((n) => have.has(n));
  for (const s of shared) {
    const at = out.findIndex((g) => g.label === s.label);
    const tools = s.tools.filter(fresh), small = (s.small ?? []).filter(fresh);
    for (const t of [...tools, ...small]) names(t).forEach((n) => have.add(n));
    if (at < 0) { if (tools.length + small.length > 0) out.push({ ...s, tools, small }); continue; }
    const g = out[at];
    out[at] = { ...g, tools: [...g.tools, ...tools], small: [...(g.small ?? []), ...small] };
  }
  return out;
}

/** The groups with each that says so (`after`) moved right after the group it names, when the tab has it. */
function placed(groups: RibbonGroupDef[]): RibbonGroupDef[] {
  const out = groups.filter((g) => !g.after || !groups.some((x) => x.label === g.after));
  for (const g of groups) {
    if (!g.after || !out.some((x) => x.label === g.after) || out.includes(g)) continue;
    out.splice(out.findIndex((x) => x.label === g.after) + 1, 0, g);
  }
  return out;
}
